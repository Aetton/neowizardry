using System.Collections.Immutable;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

// First connected native condition: ID 16. Other IDs require their own consumers.
public sealed record ConditionTargetFacts(int Level,ImmutableArray<int> Resistances);
public sealed record NativeConditionBattle(ImmutableDictionary<string,ConditionTargetFacts> PartyTargets,
    ImmutableDictionary<string,int> Condition16);
public static class NativeConditions
{
    public static int Remaining(GameState s,string id)=>s.Melee?.Field?.Conditions?.Condition16.GetValueOrDefault(id)??0;
    public static bool Incapacitated(GameState s,string id)=>Remaining(s,id)>0;
    internal static NativeConditionBattle Create(ImmutableDictionary<string,ConditionTargetFacts> authoredTargets)=>new(authoredTargets,ImmutableDictionary<string,int>.Empty);
    internal static void Validate(GameState s)
    {
        var f=s.Melee!.Field!;bool modern=f.Monsters?.Values.Any(m=>m.Record.ProfileVersion==3)==true;
        if(!modern){if(f.Conditions is not null)throw new InvalidDataException("Native conditions require version-three monster profiles");return;}
        if(f.Monsters!.Values.Any(m=>m.Record.ProfileVersion!=3) || f.Conditions is not {} c || c.PartyTargets is null || c.Condition16 is null ||
            c.PartyTargets.Count!=s.Party.Length || s.Party.Any(h=>!c.PartyTargets.ContainsKey(h.Id)))throw new InvalidDataException("Incomplete native condition encounter");
        foreach(var target in c.PartyTargets.Values)
            if(target is null || target.Level is <0 or >255 || target.Resistances.IsDefault || target.Resistances.Length!=6 || target.Resistances.Any(r=>r is <-255 or >255))throw new InvalidDataException("Invalid authored condition target facts");
        var ids=s.Party.Select(h=>h.Id).Concat(TacticalCombat.Enemies(s).Select(e=>e.Id)).ToHashSet();
        if(c.Condition16.Any(p=>!ids.Contains(p.Key) || p.Value is <1 or >12))throw new InvalidDataException("Invalid native condition timer");
    }
    // Native 522FE0/523C00 replace a timer only with a longer one, never add durations.
    internal static GameState Apply(GameState s,string id,int duration)
    {
        if(duration is <2 or >12)throw new ArgumentOutOfRangeException(nameof(duration));
        var f=s.Melee!.Field!;var c=f.Conditions??throw new InvalidOperationException("No condition encounter");
        if(!f.Positions.ContainsKey(id))throw new InvalidOperationException("Unknown condition target");
        return s with {Melee=s.Melee with {Field=f with {Conditions=c with {Condition16=c.Condition16.SetItem(id,OriginalCondition16.Merge(Remaining(s,id),duration))}}}};
    }
    // Native timer subtraction/expiry, with an authored laboratory clock: one unit per
    // completed round, including the round of application. No real-time/native clock parity.
    internal static GameState EndRound(GameState s,List<string> events)
    {
        if(s.Melee?.Field?.Conditions is not {} c)return s;
        var timers=c.Condition16.ToBuilder();foreach(var (id,time) in c.Condition16)
        {int next=OriginalCondition16.Advance(time,1);if(next==0){timers.Remove(id);events.Add($"{id}: native condition 16 expired");}else timers[id]=next;}
        return s with {Melee=s.Melee with {Field=s.Melee.Field with {Conditions=c with {Condition16=timers.ToImmutable()}}}};
    }
    internal static GameState OnMonsterDamage(GameState s,string attacker,string target,int chance,ref uint rng,List<string> events)
    {
        if(chance==0 || !TacticalCombat.Alive(s,target))return s;
        var source=MonsterCombat.Compile(MonsterCombat.Actor(s,attacker)!.Record);
        int level,resistance;
        if(MonsterCombat.Actor(s,target) is {} monster)
        {var d=MonsterCombat.Compile(monster.Record);level=d.Level;resistance=d.Resistances[1];}
        else{var f=s.Melee!.Field!.Conditions!.PartyTargets[target];level=f.Level;resistance=f.Resistances[1];}
        var input=new AttackConditionInput(1,chance,OriginalAttackEffects.Strength(source.Level),level,resistance);
        var draws=new List<int>{Dice.Exclusive(ref rng,100)};
        if(OriginalAttackEffects.Triggers(chance,draws[0]))
        {
            int gate=Dice.Exclusive(ref rng,4);draws.Add(gate);var plan=OriginalAttackEffects.DurationPlan(input.Strength,gate);
            for(int i=0;i<plan.Dice;i++)draws.Add(Dice.Exclusive(ref rng,2));
            draws.Add(Dice.Exclusive(ref rng,100));
        }
        var result=OriginalAttackEffects.ResolveCondition(input,draws);
        if(result.Request is {} request)s=Apply(s,target,request.Duration!.Value);
        events.Add($"{attacker} → {target}: native condition 16 {result.Outcome}"+(result.Request is {} r?$" · duration {r.Duration}":""));return s;
    }
}
