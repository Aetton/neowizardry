using System.Text.Json;

namespace NeoWizardry.Core;

// These are prototype rules, NOT recovered Wizardry 8 combat formulas.
public sealed record Hero(string Id, string Name, string Race, string Profession,
    int Hp, int MaxHp, int Speed, int Power, int Xp = 0, bool Captive = false,
    string Gender = "Male", int? EquippedItemId = null);
public sealed record Enemy(string Id, int Hp, int Speed, int Power, int XpReward);
public sealed record GameState(int Schema, Hero[] Party, Enemy Enemy, int Round = 0,
    bool RewardGranted = false, string Quest = "none");
public sealed record RoundResult(GameState State, string[] Events);

public static class Game
{
    public const int SaveSchema = 1;

    public static GameState Create(int size)
    {
        if (size is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(size));
        var party = Enumerable.Range(1, size).Select(i =>
            new Hero($"hero-{i}", i == 1 ? "Faerie Ninja" : $"Companion {i}",
                i == 1 ? "Faerie" : "Human", "Ninja", 36, 36, 12-i, 7)).ToArray();
        return new(SaveSchema, party, new("Training Sentinel", 32, 4, 3, 120));
    }

    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Schema != SaveSchema || state.Party is null || state.Enemy is null ||
            state.Party.Length is < 1 or > 8 || state.Round < 0 ||
            state.Enemy.Hp < 0 || state.Enemy.Power < 0 || state.Enemy.Speed < 0 || state.Enemy.XpReward < 0 ||
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
    }

    public static RoundResult Attack(GameState state)
    {
        Validate(state);
        if (state.Enemy.Hp == 0 || !state.Party.Any(h => h.Hp > 0 && !h.Captive))
            return new(state, ["No combat action available."]);
        var heroes = state.Party.ToArray();
        var enemy = state.Enemy;
        var events = new List<string>();
        // Equal-speed actions have a stable key, so replay does not depend on renderer timing.
        var order = heroes.Where(h => h.Hp > 0 && !h.Captive)
            .Select(h => (Id: h.Id, Speed: h.Speed, IsEnemy: false))
            .Append((Id: "enemy", Speed: enemy.Speed, IsEnemy: true))
            .OrderByDescending(a => a.Speed).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray();
        foreach (var action in order)
        {
            if (enemy.Hp == 0) break;
            if (action.IsEnemy)
            {
                var target = Array.FindIndex(heroes, h => h.Hp > 0 && !h.Captive);
                if (target < 0) break;
                var h = heroes[target];
                var damage = Math.Min(h.Hp, enemy.Power);
                heroes[target] = h with { Hp = h.Hp-damage };
                events.Add($"{enemy.Id} → {h.Name}: {damage}");
            }
            else
            {
                var h = heroes.First(h => h.Id == action.Id);
                if (h.Hp == 0 || h.Captive) continue;
                var damage = Math.Min(enemy.Hp, h.Power);
                enemy = enemy with { Hp = enemy.Hp-damage };
                events.Add($"{h.Name} → {enemy.Id}: {damage}");
            }
        }
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
        return new(state with { Party = heroes, Enemy = enemy, Round = checked(state.Round+1), RewardGranted = granted }, events.ToArray());
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
