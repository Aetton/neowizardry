using System.Buffers.Binary;
using System.Collections.Immutable;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

// A receipt belongs to the expedition, not to the scenario-reward executor.
public sealed record ExpeditionRewardReceipt(ImmutableDictionary<string,int> InitialExperience, bool Granted=false);
public sealed record ExpeditionRewardCapability(bool Ready, ImmutableArray<string> Blockers);

public static class ExpeditionRewards
{
    // MonsterManager 4E6780/4E53xx, Combat combat-end, GameplayCode 4EEF10.
    public static int Experience(MonsterRecordSnapshot record)
    {
        _=MonsterCombat.Compile(record);
        var raw=Convert.FromHexString(record.RawRecordHex);
        uint value=BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x26b));
        if(value==0)value=BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x181));
        if(value>int.MaxValue)throw new InvalidDataException("Monster XP exceeds the supported signed experience range");
        return (int)value;
    }

    public static int Experience(GameState game)=>checked(game.Melee!.Field!.Monsters!.Values.Sum(m=>Experience(m.Record)));
    private static void RequireSupported(GameState game)
    {
        foreach(var actor in game.Melee!.Field!.Monsters!.Values)RequireSupported(actor.Record);
    }

    // Do not silently replace NPC/direct/table loot or gold with an empty reward.
    // The first connected native encounter has no enabled treasure slots or gold.
    public static ExpeditionRewardCapability Capability(MonsterRecordSnapshot record)
    {
        _=Experience(record);var raw=Convert.FromHexString(record.RawRecordHex);
        var blockers=ImmutableArray.CreateBuilder<string>();
        if((raw[0xd0]&1)!=0)blockers.Add("npc_rewards");
        for(int slot=0;slot<8;slot++)
            if(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x1f3+slot*10+1))!=0)
                blockers.Add($"treasure_slot_{slot}");
        if(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x243))!=0 || raw[0x245]!=0)
            blockers.Add("gold_dice");
        return new(blockers.Count==0,blockers.ToImmutable());
    }

    public static void RequireSupported(MonsterRecordSnapshot record)
    {
        var capability=Capability(record);
        if(!capability.Ready)throw new InvalidDataException("Unsupported expedition rewards: "+string.Join(", ",capability.Blockers));
    }

    // Condition 16 does not exclude a living character from native XP eligibility.
    // Captivity is an authored expedition exclusion; native NPC captivity is not modeled.
    public static bool Eligible(Hero hero)=>hero.Hp>0 && !hero.Captive;

    internal static GameState Grant(GameState game,MonsterRecordSnapshot record)
    {
        RequireSupported(game);
        int count=game.Party.Count(Eligible);
        if(!game.RewardGranted || TacticalCombat.Enemies(game).Any(e=>e.Hp>0) || count==0)throw new InvalidOperationException("Native reward requires completed victory");
        int share=Experience(game)/count; // Native truncation discards the remainder.
        return game with {Party=game.Party.Select(h=>Eligible(h)?h with {Xp=checked(h.Xp+share)}:h).ToImmutableArray()};
    }

    internal static void Validate(WorldExpeditionState state,MonsterRecordSnapshot record)
    {
        if(state.Schema==1)
        {
            if(state.Rewards is not null)throw new InvalidDataException("Legacy expedition cannot acquire a reward receipt");
            return; // Legacy victories never retroactively grant newly connected rewards.
        }
        RequireSupported(state.Game);
        var receipt=state.Rewards;
        if(receipt?.InitialExperience is not {} initial || initial.Count!=state.Game.Party.Length ||
           initial.Any(p=>p.Value<0) || state.Game.Party.Any(h=>!initial.ContainsKey(h.Id)) ||
           receipt.Granted!=(state.Phase==WorldEncounterPhase.Victory))
            throw new InvalidDataException("Invalid expedition reward receipt");
        int count=state.Game.Party.Count(Eligible);
        int share=receipt.Granted && count>0?Experience(state.Game)/count:0;
        foreach(var hero in state.Game.Party)
            if((long)initial[hero.Id]+(receipt.Granted && Eligible(hero)?share:0)!=hero.Xp)
                throw new InvalidDataException("Expedition XP differs from its once-only reward receipt");
    }

    // First native level goal, shown as a milestone until character advancement exists.
    // GameplayCode 4EF090. Effective combat stats remain the authored training facts.
    public static int FirstLevelGoal(string profession)=>Enum.TryParse<OriginalProfession>(profession,out var p) && Enum.IsDefined(p)?p switch
    {
        OriginalProfession.Fighter or OriginalProfession.Rogue or OriginalProfession.Gadgeteer or OriginalProfession.Bard=>1000,
        OriginalProfession.Priest or OriginalProfession.Alchemist or OriginalProfession.Psionic or OriginalProfession.Mage=>1200,
        OriginalProfession.Lord or OriginalProfession.Valkyrie or OriginalProfession.Ranger or OriginalProfession.Samurai or OriginalProfession.Monk=>1400,
        OriginalProfession.Ninja or OriginalProfession.Bishop=>1600,
        _=>throw new InvalidDataException("Unsupported progression profession")
    }:throw new InvalidDataException("Unsupported progression profession");
}
