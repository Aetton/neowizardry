using System.Text.Json;

namespace NeoWizardry.Core;

// These are prototype rules, NOT recovered Wizardry 8 combat formulas.
public sealed record Hero(string Id, string Name, string Race, string Profession,
    int Hp, int MaxHp, int Speed, int Power, int Xp = 0, bool Captive = false,
    string Gender = "Male", int? EquippedItemId = null, ManaPool[]? Mana = null, string[]? Spellbook = null, SkillValue[]? Skills = null, StatusEffect[]? Effects = null);
public sealed record Enemy(string Id, int Hp, int Speed, int Power, int XpReward, int? LootItemId = null, StatusEffect[]? Effects = null);
public sealed record GameState(int Schema, Hero[] Party, Enemy Enemy, int Round = 0,
    bool RewardGranted = false, string Quest = "none", uint RandomState = Dice.DefaultSeed, ItemStack[]? Bag = null, string[]? KnownRecipes = null);
public sealed record RoundResult(GameState State, string[] Events);

public static class Game
{
    public const int SaveSchema = 1;

    public static GameState Create(int size)
    {
        if (size is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(size));
        var party = Enumerable.Range(1, size).Select(i =>
            new Hero($"hero-{i}", i == 1 ? "Faerie Ninja" : $"Companion {i}",
                i == 1 ? "Faerie" : "Human", "Ninja", 36, 36, 12-i, 7,
                Mana: [new("Fire",12,12),new("Divine",12,12)], Spellbook: Magic.Spells.Select(s=>s.Id).ToArray(), Skills: [new("test-craft",25)])).ToArray();
        return new(SaveSchema, party, new("Training Sentinel", 32, 4, 3, 120), Bag: []);
    }

    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Schema != SaveSchema || state.Party is null || state.Enemy is null ||
            state.Party.Length is < 1 or > 8 || state.Round < 0 ||
            state.Enemy.Hp < 0 || state.Enemy.Power < 0 || state.Enemy.Speed < 0 || state.Enemy.XpReward < 0 || state.Enemy.LootItemId < 0 ||
            string.IsNullOrWhiteSpace(state.Enemy.Id) || state.Quest is not ("none" or "captive" or "escaped"))
            throw new InvalidDataException("Invalid save state");
        var ids = new HashSet<string>();
        foreach (var h in state.Party)
            if (h is null || string.IsNullOrWhiteSpace(h.Id) || !ids.Add(h.Id) || h.MaxHp <= 0 ||
                h.Hp < 0 || h.Hp > h.MaxHp || h.Xp < 0 || h.Power < 0 || h.Speed < 0 || h.EquippedItemId < 0 ||
                h.Gender is not ("Male" or "Female"))
                throw new InvalidDataException("Invalid party member");
        if (state.RewardGranted && state.Enemy.Hp != 0)
            throw new InvalidDataException("Reward before victory");
        var itemIds = new HashSet<int>();
        foreach (var stack in state.Bag ?? [])
            if (stack is null || stack.ItemId < 0 || stack.Count <= 0 || !itemIds.Add(stack.ItemId))
                throw new InvalidDataException("Invalid party bag");
        foreach(var hero in state.Party)
        {
            Magic.ValidateHero(hero);
            var skills=new HashSet<string>();
            foreach(var skill in hero.Skills ?? [])
                if(skill is null || string.IsNullOrWhiteSpace(skill.Id) || skill.Value<0 || !skills.Add(skill.Id))
                    throw new InvalidDataException("Invalid skills");
            Magic.ValidateEffects(hero.Effects,false);
        }
        Magic.ValidateEffects(state.Enemy.Effects,true);
        var recipes=new HashSet<string>();
        foreach(var recipe in state.KnownRecipes ?? [])
            if(recipe is null || !recipes.Add(recipe) || !Crafting.Recipes.Any(r=>r.Id==recipe))
                throw new InvalidDataException("Invalid known recipe");
    }

    public static RoundResult Attack(GameState state, ItemCatalog? catalog = null) => ResolveRound(state,catalog,[]);

    public static RoundResult Cast(GameState state, SpellOrder order, ItemCatalog? catalog = null)
    {
        return CastRound(state,[order],catalog);
    }

    public static RoundResult CastRound(GameState state, IReadOnlyList<SpellOrder> orders, ItemCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(orders);
        Validate(state);
        if(orders.Count==0)throw new InvalidOperationException("No spell orders selected.");
        var actors=new HashSet<string>();
        foreach(var order in orders)
        {
            ArgumentNullException.ThrowIfNull(order);
            if(!actors.Add(order.HeroId))throw new InvalidOperationException("One order per caster per round.");
            var denial=Magic.Denial(state,order);
            if(denial is not null)throw new InvalidOperationException(denial);
        }
        return ResolveRound(state,catalog,orders);
    }

    private static RoundResult ResolveRound(GameState state, ItemCatalog? catalog, IReadOnlyList<SpellOrder> casts)
    {
        Validate(state);
        if (state.Enemy.Hp == 0 || !state.Party.Any(h => h.Hp > 0 && !h.Captive))
            return new(state, ["No combat action available."]);
        Equipment.ValidateLoadedEquipment(state, catalog, catalog?.Provenance.Sha256);
        var selected=casts.ToDictionary(c=>c.HeroId);
        var heroes = state.Party.ToArray();
        var weapons = heroes.ToDictionary(h => h.Id,
            h => h.EquippedItemId.HasValue ? catalog!.Find(h.EquippedItemId.Value) : null);
        uint randomState = state.RandomState;
        var enemy = state.Enemy;
        var events = new List<string>();
        // Equal-speed actions have a stable key, so replay does not depend on renderer timing.
        var order = heroes.Where(h => h.Hp > 0 && !h.Captive)
            .Select(h => (Id: h.Id, Speed: (long)h.Speed + (selected.ContainsKey(h.Id) ? 0 : weapons[h.Id]?.Initiative ?? 0), IsEnemy: false))
            .Append((Id: "enemy", Speed: (long)enemy.Speed, IsEnemy: true))
            .OrderByDescending(a => a.Speed).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray();
        foreach (var action in order)
        {
            if (enemy.Hp == 0) break;
            if (action.IsEnemy)
            {
                if((enemy.Effects ?? []).Any(e=>e.Kind=="bind"))
                { events.Add($"{enemy.Id}: bound; action skipped.");continue; }
                var target = Array.FindIndex(heroes, h => h.Hp > 0 && !h.Captive);
                if (target < 0) break;
                var h = heroes[target];
                int ward=(h.Effects ?? []).FirstOrDefault(e=>e.Kind=="ward")?.Magnitude ?? 0;
                var damage = Math.Min(h.Hp, Math.Max(0,enemy.Power-ward));
                heroes[target] = h with { Hp = h.Hp-damage };
                events.Add($"{enemy.Id} → {h.Name}: {damage}");
            }
            else
            {
                var h = heroes.First(h => h.Id == action.Id);
                if (h.Hp == 0 || h.Captive) continue;
                if(selected.TryGetValue(h.Id,out var cast))
                {
                    var spell=Magic.Find(cast.SpellId);
                    int targetIndex=Magic.TargetsParty(spell) ? Array.FindIndex(heroes,t=>t.Id==cast.TargetId && t.Hp>0 && !t.Captive) : -1;
                    if(Magic.TargetsParty(spell) && (targetIndex<0 || (spell.Heals && heroes[targetIndex].Hp==heroes[targetIndex].MaxHp)))
                    { events.Add($"{h.Name}: spell target unavailable; no mana spent.");continue; }
                    int casterIndex=Array.FindIndex(heroes,c=>c.Id==h.Id);
                    heroes[casterIndex]=Magic.Spend(h,spell,cast.Power);
                    if(spell.Effect!="direct")
                    {
                        var effect=new StatusEffect(spell.Effect,spell.Effect=="ward" ? 2*cast.Power : cast.Power,spell.Effect=="bind" ? 1 : 3);
                        if(spell.Effect=="ward")heroes[targetIndex]=heroes[targetIndex] with { Effects=Magic.Apply(heroes[targetIndex].Effects,effect) };
                        else enemy=enemy with { Effects=Magic.Apply(enemy.Effects,effect) };
                        events.Add($"{h.Name}: {spell.Name} P{cast.Power} → {cast.TargetId} ({effect.Rounds} rounds, {spell.CostPerPower*cast.Power} {spell.Realm} mana)");
                        continue;
                    }
                    int amount=cast.Power;
                    for(int i=0;i<cast.Power;i++)amount+=Dice.Roll(ref randomState,6);
                    if(spell.Heals)
                    {
                        var target=heroes[targetIndex];
                        int restored=Math.Min(target.MaxHp-target.Hp,amount);
                        heroes[targetIndex]=target with { Hp=target.Hp+restored };
                        events.Add($"{h.Name}: {spell.Name} P{cast.Power} → {target.Name}: +{restored} HP (roll {amount}, {spell.CostPerPower*cast.Power} {spell.Realm} mana)");
                    }
                    else
                    {
                        int hit=Math.Min(enemy.Hp,amount);enemy=enemy with { Hp=enemy.Hp-hit };
                        events.Add($"{h.Name}: {spell.Name} P{cast.Power} → {enemy.Id}: {hit} (roll {amount}, {spell.CostPerPower*cast.Power} {spell.Realm} mana)");
                    }
                    continue;
                }
                var weapon = weapons[h.Id];
                int rolled = h.Power;
                if (weapon is not null)
                {
                    rolled = weapon.DamageConstant;
                    for (int i = 0; i < weapon.DamageDice; i++)
                        rolled += Dice.Roll(ref randomState, weapon.DamageSides);
                }
                var damage = Math.Min(enemy.Hp, rolled);
                enemy = enemy with { Hp = enemy.Hp-damage };
                events.Add($"{h.Name} → {enemy.Id}: {damage}" + (weapon is null ? "" :
                    $" [{weapon.Name}: {weapon.DamageDescription} = {rolled}]") );
            }
        }
        if(enemy.Hp>0 && (enemy.Effects ?? []).FirstOrDefault(e=>e.Kind=="burn") is { } burn)
        {
            int damage=Math.Min(enemy.Hp,burn.Magnitude);
            enemy=enemy with { Hp=enemy.Hp-damage };
            events.Add($"{enemy.Id}: burning {damage}.");
        }
        // Durations include the application round. Each resolved round advances once.
        if(enemy.Effects is not null)enemy=enemy with { Effects=Magic.Tick(enemy.Effects) };
        heroes=heroes.Select(h=>h.Effects is null ? h : h with { Effects=Magic.Tick(h.Effects) }).ToArray();
        bool granted = state.RewardGranted;
        if (enemy.Hp == 0 && !granted)
        {
            var eligible = heroes.Select((h, i) => (h, i)).Where(x => x.h.Hp > 0 && !x.h.Captive).ToArray();
            if (eligible.Length > 0)
            {
                int share = enemy.XpReward / eligible.Length, extra = enemy.XpReward % eligible.Length;
                for (int j = 0; j < eligible.Length; j++)
                {
                    var (h, i) = eligible[j];
                    heroes[i] = h with { Xp = checked(h.Xp+share+(j < extra ? 1 : 0)) };
                }
                granted = true;
                events.Add($"Victory. {enemy.XpReward} XP divided between {eligible.Length} survivors.");
            }
        }
        var result = state with { Party = heroes, Enemy = enemy, Round = checked(state.Round+1), RewardGranted = granted,
            RandomState = randomState };
        if (granted && !state.RewardGranted && enemy.LootItemId.HasValue)
        {
            result = Inventory.Add(result,enemy.LootItemId.Value,1,catalog!);
            events.Add($"Loot: {catalog!.Find(enemy.LootItemId.Value).Name} ×1 added to the party bag.");
        }
        return new(result, events.ToArray());
    }

    // A regression fixture for the solo captivity softlock, not a canonical Crock quest implementation.
    public static GameState CaptureFirst(GameState state)
    {
        Validate(state);
        if (state.Quest != "none") throw new InvalidOperationException("Captivity event already used");
        int index = Array.FindIndex(state.Party, h => h.Hp > 0 && !h.Captive);
        if (index < 0) throw new InvalidOperationException("No living hero to capture");
        var party = state.Party.ToArray();
        party[index] = party[index] with { Captive = true };
        return state with { Party = party, Quest = "captive" };
    }

    public static GameState Escape(GameState state)
    {
        Validate(state);
        if (state.Quest != "captive") throw new InvalidOperationException("No captivity event");
        return state with { Party = state.Party.Select(h => h with { Captive = false }).ToArray(), Quest = "escaped" };
    }

    public static string Save(GameState state)
    {
        Validate(state);
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }

    public static GameState Load(string json)
    {
        var state = JsonSerializer.Deserialize<GameState>(json) ?? throw new InvalidDataException("Empty save");
        Validate(state);
        return state;
    }
}
