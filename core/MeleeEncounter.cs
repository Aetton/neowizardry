using System.Collections.Immutable;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

// Authored effective facts for the ordinary character/character slice. These are not
// imported characters, derived original stats, monster definitions or hidden defaults.
public sealed record MeleeCharacterFacts(int Strength,PhysicalCombatAttributes Attributes,int CombatSkill,int WeaponSkill,
    ImmutableArray<ProfessionLevels> History,int Stealth,BodyArmor BodyArmor,CharacterInitiativeFacts? Initiative=null);
public sealed record MeleeEncounter(string Ruleset,string ItemDatabaseHash,ImmutableDictionary<string,MeleeCharacterFacts> Party,
    MeleeCharacterFacts Opponent,string? OpponentWeaponId,ImmutableDictionary<string,RestoredMagicFacts>? Magic=null);

public static class MeleeCombat
{
    public const string Ruleset="ordinary-character-duel-v1";
    public const double Reach=4.5; // Authored Godot training-room distance, not original world units.

    public static void ValidateStructure(GameState state)
    {
        if(state.Melee is not { } encounter)return;
        if(encounter.Ruleset!=Ruleset || encounter.ItemDatabaseHash is not {Length:64} || !encounter.ItemDatabaseHash.All(Uri.IsHexDigit) ||
            encounter.Party is null || encounter.Party.Count!=state.Party.Length || state.Party.Any(h=>h.Id==state.Enemy.Id || !encounter.Party.ContainsKey(h.Id)) ||
            state.ProjectileActions is {Count:>0})
            throw new InvalidDataException("Invalid or mixed melee encounter");
        foreach(var facts in encounter.Party.Values.Append(encounter.Opponent))ValidateFacts(facts);
        var initiativeCount=encounter.Party.Values.Append(encounter.Opponent).Count(f=>f.Initiative is not null);
        if(initiativeCount!=0 && initiativeCount!=state.Party.Length+1)throw new InvalidDataException("Mixed initiative profiles");
        if(encounter.Magic is { } magic)
        {
            if(magic.Count!=state.Party.Length+1 || !magic.ContainsKey(state.Enemy.Id) || state.Party.Any(h=>!magic.ContainsKey(h.Id)) || initiativeCount==0)
                throw new InvalidDataException("Incomplete restored magic profiles");
            foreach(var facts in magic.Values)RestoredMagic.Validate(facts);
        }
        if(!(state.Enemy.Hp==0 && encounter.OpponentWeaponId is null) &&
            (state.Items!.Items.SingleOrDefault(i=>i.Id==encounter.OpponentWeaponId) is not {Quantity:1} weapon ||
            weapon.Location!=new ItemLocation(state.Enemy.Id,Equipment.WeaponSlot)))
            throw new InvalidDataException("Missing opponent-owned melee weapon");
    }
    private static void ValidateFacts(MeleeCharacterFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);ArgumentNullException.ThrowIfNull(facts.Attributes);ArgumentNullException.ThrowIfNull(facts.BodyArmor);
        if(facts.Strength is <0 or >125 || facts.Stealth is <0 or >125 || facts.History.IsDefaultOrEmpty)
            throw new InvalidDataException("Invalid effective melee facts");
        foreach(var location in Enum.GetValues<CharacterHitLocation>())
            if(facts.BodyArmor.At(location) is <-128 or >127)throw new InvalidDataException("Invalid authored body protection");
        if(facts.Initiative is not null)_=OriginalInitiative.Character(facts.Attributes.Speed,facts.Attributes.Senses,facts.Initiative);
        _=OriginalCombatRules.SingleWeapon(new(facts.Attributes.Dexterity,facts.Attributes.Speed,facts.CombatSkill,facts.WeaponSkill,facts.History.ToArray(),0));
        _=OriginalPhysicalScores.OrdinaryCharacter(new(100,0,0,0,0,facts.Attributes,facts.Attributes,OriginalAttackMode.Swing,0,0));
    }
    private static ItemDefinition Weapon(GameState state,string actorId,ItemCatalog catalog)
    {
        var item=actorId==state.Enemy.Id?state.Items!.Items.Single(i=>i.Id==state.Melee!.OpponentWeaponId):Equipment.Instance(state,actorId);
        if(item is null)throw new InvalidOperationException("Equip a single ordinary melee weapon before attacking");
        var definition=catalog.Find(item.DefinitionId);
        if(definition.TypeCode is not (0 or 1) || (definition.AttackModes&1)==0 || definition.DamageDice==0 || definition.DamageSides==0 ||
            definition.Instance is not {Mode:0} fields || fields.Compile(definition.RecordSha256).SpellUse!=0)
            throw new InvalidOperationException("This encounter supports a free offhand and ordinary Swing weapons without resource/spell use");
        return definition;
    }
    private static (PhysicalAttackBudget Budget,PreparedPhysicalWeapon Weapon,PhysicalHandBonuses Hand) Prepare(MeleeCharacterFacts facts,ItemDefinition weapon)
    {
        var prepared=OriginalWeaponAdapter.PreparePrimary(weapon);
        var budget=OriginalCombatRules.SingleWeapon(new(facts.Attributes.Dexterity,facts.Attributes.Speed,facts.CombatSkill,
            facts.WeaponSkill,facts.History.ToArray(),weapon.Initiative,ModernWeapon:prepared.ModernWeapon));
        var hand=OriginalPhysicalDamage.PrimaryHand(facts.Strength,facts.Attributes.Dexterity,facts.Attributes.Senses,
            prepared.ToHit,prepared.StrengthScaling,0,prepared.ModernWeapon);
        if((long)budget.BaseAttackRating+5L*hand.ToHit<100 || weapon.BonusSwings<0 || weapon.BonusSwings>15)
            throw new InvalidOperationException("Unsupported low-score fumble or extreme swing adjustment in this ordinary encounter");
        return (budget,prepared,hand);
    }
    public static void Validate(GameState state,ItemCatalog catalog)
    {
        Game.Validate(state);
        if(state.Melee is not { } encounter || !encounter.ItemDatabaseHash.Equals(catalog.Provenance.Sha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Melee encounter requires its item catalog and ruleset");
        ItemInventory.Validate(state.Items!,Inventory.Rules(catalog));Equipment.ValidateSelection(state,catalog);
    }

    public static RoundResult Advance(GameState state,ItemCatalog catalog,double distance,IReadOnlyList<RestoredCastOrder>? castOrders=null,SpellCatalog? spells=null)
    {
        Validate(state,catalog);
        if(!double.IsFinite(distance) || distance<0)throw new ArgumentOutOfRangeException(nameof(distance));
        if(distance>Reach)throw new InvalidOperationException("Too far from the opponent. Move closer.");
        if(state.Enemy.Hp==0 || !state.Party.Any(h=>h.Hp>0 && !h.Captive))return new(state,["No melee action available."]);
        var encounter=state.Melee!;
        var casts=new Dictionary<string,PreparedRestoredCast>();
        foreach(var order in castOrders??[])
        {
            if(order is null || !casts.TryAdd(order.CasterId,RestoredMagic.Prepare(state,order,spells??throw new InvalidOperationException("Spell catalog required"))))
                throw new InvalidOperationException("Duplicate original spell order");
        }
        foreach(var h in state.Party.Where(h=>h.Hp>0 && !h.Captive))
        {
            if(h.Effects is {Count:>0})throw new InvalidOperationException("Synthetic spell effects cannot enter the restored melee encounter");
            if(!casts.ContainsKey(h.Id))_=Prepare(encounter.Party[h.Id],Weapon(state,h.Id,catalog));
        }
        if(state.Enemy.Effects is {Count:>0})throw new InvalidOperationException("Synthetic enemy effects cannot enter the restored melee encounter");
        _=Prepare(encounter.Opponent,Weapon(state,state.Enemy.Id,catalog));
        var party=state.Party.ToArray();var enemy=state.Enemy;uint rng=state.RandomState;
        var events=new List<string>();
        // The character opponent is scheduled as a character, never as a native monster.
        bool initiative=encounter.Opponent.Initiative is not null;
        var queue=new List<(string Id,bool Enemy,int Time,int Remaining,int Last)>();
        foreach(var entry in party.Where(h=>h.Hp>0 && !h.Captive).Select(h=>(h.Id,Enemy:false)).Append((enemy.Id,Enemy:true)))
        {
            var facts=entry.Enemy?encounter.Opponent:encounter.Party[entry.Id];
            var weapon=Weapon(state,entry.Id,catalog);var prepared=Prepare(facts,weapon);
            int time=initiative?OriginalInitiative.FirstTime(OriginalInitiative.Character(facts.Attributes.Speed,facts.Attributes.Senses,facts.Initiative!),
                casts.ContainsKey(entry.Id)?null:weapon.Initiative,Dice.Roll(ref rng,4)-1):125-facts.Attributes.Speed;
            queue.Add((entry.Id,entry.Enemy,time,casts.ContainsKey(entry.Id)?1:prepared.Budget.Attacks,0));
        }
        while(queue.Count>0)
        {
            var turn=queue.OrderBy(x=>x.Time).ThenBy(x=>x.Enemy).ThenBy(x=>x.Id,StringComparer.Ordinal).First();queue.Remove(turn);
            if(enemy.Hp==0)break;
            var actor=turn.Enemy?null:party.Single(h=>h.Id==turn.Id);
            if(actor is not null && (actor.Hp==0 || actor.Captive))continue;
            if(!turn.Enemy && casts.TryGetValue(turn.Id,out var cast))
            {
                int index=Array.FindIndex(party,h=>h.Id==turn.Id);var caster=party[index];
                int targetIndex=Array.FindIndex(party,h=>h.Id==cast.TargetId);
                if(cast.Spell.Id==6 && (targetIndex<0 || party[targetIndex].Hp==0 || party[targetIndex].Captive))
                {
                    events.Add($"Queue {turn.Time}: {caster.Name} · {cast.Spell.Name} cancelled: target unavailable; no mana spent");continue;
                }
                var outcome=RestoredMagic.Outcome(cast,ref rng);
                party[index]=caster with {Mana=caster.Mana!.Select(m=>m.Realm==cast.Spell.Realm?m with {Current=m.Current-cast.ManaCost}:m).ToImmutableList()};
                string result;
                if(outcome==CastingOutcome.Fizzle)result="Fizzle, no effect";
                else if(cast.Spell.Id==6)
                {
                    var target=party[targetIndex];int missing=target.MaxHp-target.Hp;
                    int restored=missing==0?0:Math.Min(missing,RestoredMagic.Amount(cast,ref rng));
                    party[targetIndex]=target with {Hp=target.Hp+restored};result=$"{target.Name}: +{restored} HP";
                }
                else
                {
                    bool backfire=outcome==CastingOutcome.Backfire;
                    var hit=RestoredMagic.Damage(cast,encounter.Magic![backfire?turn.Id:enemy.Id],ref rng);
                    int removed=Math.Min(backfire?party[index].Hp:enemy.Hp,hit.Damage);
                    if(backfire)party[index]=party[index] with {Hp=party[index].Hp-removed};else enemy=enemy with {Hp=enemy.Hp-removed};
                    result=$"{(backfire?"Backfire → "+caster.Name:enemy.Id)}: {removed} HP, resistance {hit.Reduction}%";
                }
                events.Add($"Queue {turn.Time}: {caster.Name} · {cast.Spell.Name} PL{cast.Power}: {result}, {cast.ManaCost} {cast.Spell.Realm} mana");
                continue;
            }
            var facts=turn.Enemy?encounter.Opponent:encounter.Party[turn.Id];
            var weapon=Weapon(state,turn.Id,catalog);var prepared=Prepare(facts,weapon);
            events.Add($"Queue {turn.Time}: {(turn.Enemy?enemy.Id:actor!.Name)}");
            for(int attack=0;attack<(initiative?1:prepared.Budget.Attacks) && enemy.Hp>0;attack++)
            {
                if(!party.Any(h=>h.Hp>0 && !h.Captive))break;
                int swings=OriginalCombatRules.AllocateSwings(prepared.Budget.MaximumSwings,weapon.BonusSwings,
                    Dice.Roll(ref rng,prepared.Budget.MaximumSwings)-1).AllocatedSwings;
                for(int swing=0;swing<swings && enemy.Hp>0;swing++)
                {
                    int target=turn.Enemy?Array.FindIndex(party,h=>h.Hp>0 && !h.Captive):-1;
                    if(turn.Enemy && target<0)break;
                    string targetId=turn.Enemy?party[target].Id:enemy.Id;
                    var defenseFacts=turn.Enemy?encounter.Party[targetId]:encounter.Opponent;
                    var targetWeapon=Weapon(state,targetId,catalog);
                    var armor=OriginalDefenseAdapter.Prepare(new(MainHand:targetWeapon)) with {Body=defenseFacts.BodyArmor};
                    var defense=OriginalCharacterDefense.Build(new(defenseFacts.Attributes.Speed,defenseFacts.Stealth,0,0,false),
                        armor,new(0,0,false,false,false),new(0,0,0,false));
                    var location=OriginalCharacterDefense.SelectLocation(Dice.Roll(ref rng,100)-1);
                    var thresholds=OriginalCharacterDefense.Against(defense,location,OriginalAttackMode.Swing,new(false,false,false));
                    var input=new OrdinaryPhysicalScoreInput(prepared.Budget.BaseAttackRating,prepared.Hand.ToHit,0,0,0,
                        facts.Attributes,defenseFacts.Attributes,OriginalAttackMode.Swing,thresholds.Hit,thresholds.Penetration,
                        ModernWeapon:prepared.Weapon.ModernWeapon); // Counter=0 is explicit neutral scenario context, not swing index.
                    var scores=OriginalPhysicalScores.OrdinaryCharacter(input);int contact=Dice.Roll(ref rng,100)-1;
                    var outcome=OriginalMeleeStrike.Contact(scores,contact);
                    int[] draws=outcome==PhysicalStrikeOutcome.Damage?Enumerable.Range(0,prepared.Weapon.Damage.Dice)
                        .Select(_=>Dice.Roll(ref rng,prepared.Weapon.Damage.Sides)-1).ToArray():[];
                    var strike=OriginalMeleeStrike.Resolve(input,prepared.Weapon.Damage,prepared.Hand.DamagePercent,0,0,contact,draws);
                    int hp=turn.Enemy?party[target].Hp:enemy.Hp;int damage=Math.Min(hp,strike.Damage?.Damage??0);
                    if(turn.Enemy)party[target]=party[target] with {Hp=hp-damage};else enemy=enemy with {Hp=hp-damage};
                    events.Add($"{(turn.Enemy?enemy.Id:actor!.Name)} → {(turn.Enemy?party[target].Name:enemy.Id)} · {weapon.Name} · {location}: "+
                        $"{strike.Outcome}, {damage} HP [d100 {contact+1}; hit {scores.HitScore}; pen {scores.PenetrationScore}]");
                }
            }
            if(initiative && turn.Remaining>1 && enemy.Hp>0 &&
                OriginalInitiative.NextAttackTime(turn.Time,turn.Last,turn.Time,turn.Remaining-1) is int nextTime)
                queue.Add((turn.Id,turn.Enemy,nextTime,turn.Remaining-1,turn.Last));
        }
        var next=state with {Party=party.ToImmutableArray(),Enemy=enemy,Round=checked(state.Round+1),RandomState=rng};
        // Existing training rewards, not original XP/progression rules.
        if(enemy.Hp==0 && !state.RewardGranted)
        {
            var eligible=next.Party.Where(h=>h.Hp>0 && !h.Captive).ToArray();
            if(eligible.Length>0)
            {
                int share=enemy.XpReward/eligible.Length,extra=enemy.XpReward%eligible.Length,index=0;
                next=next with {Party=next.Party.Select(h=>eligible.Any(e=>e.Id==h.Id)?h with {Xp=checked(h.Xp+share+(index++<extra?1:0))}:h).ToImmutableArray(),RewardGranted=true};
                if(encounter.OpponentWeaponId is string loot)
                    next=next with {Items=ItemInventory.Move(next.Items!,loot,Inventory.BagLocation,Inventory.Rules(catalog)),
                        Melee=encounter with {OpponentWeaponId=null}};
                events.Add($"Victory. {enemy.XpReward} scenario XP; reward granted once.");
            }
        }
        Game.Validate(next);return new(next,events.ToImmutableArray());
    }
}
