namespace NeoWizardry.Core.Rules;

// Entries are already-resolved eligible physical actions. Target/AI policy owns this list.
public sealed record MonsterPhysicalChoice(int AttackSlot,OriginalAttackMode Mode,string TargetId);
public sealed record MonsterAttackBudget(int TotalGroups,int MaximumSwings,int GroupsRemaining,
    int SwingsRemaining,int GroupsAttempted,int SwingsCompleted);
public sealed record MonsterGroupStart(MonsterAttackBudget State,bool Started);

public static class OriginalMonsterAttackSequence
{
    public static MonsterPhysicalChoice? SelectChoice(IReadOnlyList<MonsterPhysicalChoice> eligible,int? draw)
    {
        ArgumentNullException.ThrowIfNull(eligible);
        foreach(var choice in eligible)
        {
            ArgumentNullException.ThrowIfNull(choice);
            if(choice.AttackSlot is <0 or >2)throw new ArgumentOutOfRangeException(nameof(eligible));
            if(string.IsNullOrWhiteSpace(choice.TargetId))throw new ArgumentException("Resolved target identity required");
            _=OriginalCombatRules.ModeModifiers(choice.Mode);
        }
        if(eligible.Count==0)
        {
            if(draw is not null)throw new ArgumentException("No candidate consumes no choice draw");
            return null;
        }
        if(draw is not int index || index<0 || index>=eligible.Count)
            throw new ArgumentOutOfRangeException(nameof(draw));
        // Keep ordered entries and duplicates: native selects a list index, not a distinct action.
        return eligible[index];
    }

    public static IReadOnlyList<OriginalAttackMode> Modes(int mask)
    {
        if(mask is <0 or >65535)throw new ArgumentOutOfRangeException(nameof(mask));
        // Monster AI expands every allowed mode into candidate entries, then selects a list index.
        // It does not call the separate character-side nine-draw cyclic sampler.
        return Array.AsReadOnly(Enumerable.Range(0,9).Where(mode=>(mask&(1<<mode))!=0)
            .Select(mode=>(OriginalAttackMode)mode).ToArray());
    }

    public static MonsterAttackBudget Start(int groups,int maximumSwings)
    {
        var state=new MonsterAttackBudget(groups,maximumSwings,groups,0,0,0);
        Validate(state);return state;
    }

    public static MonsterGroupStart BeginGroup(MonsterAttackBudget state,bool eligible,int? swingDraw)
    {
        Validate(state);
        if(state.GroupsRemaining==0 || state.SwingsRemaining!=0)
            throw new InvalidOperationException("A new group requires remaining budget and no unfinished swings");
        var consumed=state with {GroupsRemaining=state.GroupsRemaining-1,GroupsAttempted=state.GroupsAttempted+1};
        if(!eligible)
        {
            if(swingDraw is not null)throw new ArgumentException("Rejected group consumes no swing draw");
            return new(consumed,false);
        }
        if(swingDraw is not int draw || draw<0 || draw>=state.MaximumSwings)
            throw new ArgumentOutOfRangeException(nameof(swingDraw));
        return new(consumed with {SwingsRemaining=draw+1},true);
    }

    // Completion follows any resolved hit/miss/block outcome. No actor or target mutations here.
    // cancelRemainingSwings is the caller-resolved native interruption/retarget result, not just HP==0.
    public static MonsterAttackBudget CompleteSwing(MonsterAttackBudget state,bool cancelRemainingSwings=false)
    {
        Validate(state);
        if(state.SwingsRemaining==0)throw new InvalidOperationException("No active swing to complete");
        return state with {SwingsRemaining=cancelRemainingSwings?0:state.SwingsRemaining-1,
            SwingsCompleted=state.SwingsCompleted+1};
    }

    public static void Validate(MonsterAttackBudget state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(state.TotalGroups is <0 or >255 || state.MaximumSwings is <0 or >255 ||
            state.TotalGroups>0 && state.MaximumSwings==0 || state.GroupsRemaining<0 || state.GroupsAttempted<0 ||
            state.GroupsRemaining>state.TotalGroups || state.GroupsAttempted>state.TotalGroups ||
            state.GroupsRemaining+state.GroupsAttempted!=state.TotalGroups || state.SwingsRemaining<0 ||
            state.SwingsRemaining>state.MaximumSwings || state.SwingsCompleted<0 ||
            (long)state.SwingsCompleted+state.SwingsRemaining>(long)state.GroupsAttempted*state.MaximumSwings)
            throw new ArgumentException("Invalid monster group/swing budget");
    }
}
