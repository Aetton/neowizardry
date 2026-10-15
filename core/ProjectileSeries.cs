namespace NeoWizardry.Core;

public enum ProjectileCostKind { None, Quantity, Resource }
public enum ProjectileOutcome { Hit, Miss, Blocked }
public enum ProjectileStopReason { None, Finished, Depleted, ActorUnavailable, ContinuationUnavailable }
public sealed record ProjectileCost(int DefinitionId,ProjectileCostKind Kind,bool RemoveWhenEmpty=false);
public sealed record PendingProjectile(int Ordinal,ItemInstance Source);
public sealed record ProjectileSeriesState(string Id,ItemInventoryState Inventory,string SourceId,ProjectileCost Cost,
    int Remaining,int Completed,PendingProjectile? Pending,ProjectileStopReason StopReason);
public sealed record ProjectileCompletion(ProjectileSeriesState State,ProjectileOutcome Outcome);

// Resource lifecycle only. Contact, damage, actor eligibility and target/range policy are resolved
// by the caller. No native slots, auto-reload, RNG, HP mutation or per-round scheduling here.
public static class ProjectileSeries
{
    public static ProjectileSeriesState Create(string id,ItemInventoryState inventory,string sourceId,ProjectileCost cost,
        int allocatedSwings,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var state=new ProjectileSeriesState(id,inventory,sourceId,cost,allocatedSwings,0,null,ProjectileStopReason.None);
        Validate(state,rules);
        if(!Available(state))throw new InvalidOperationException("Projectile source is depleted");
        return state;
    }

    public static ProjectileSeriesState Launch(ProjectileSeriesState state,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);
        if(state.StopReason!=ProjectileStopReason.None || state.Pending is not null || !Available(state))
            throw new InvalidOperationException("Series cannot launch a projectile");
        return state with {Pending=new(checked(state.Completed+1),Source(state)!)};
    }

    // Before launch an interrupted action costs nothing. An in-flight projectile must finish;
    // actor incapacity after launch is supplied to Complete and stops subsequent launches.
    public static ProjectileSeriesState Stop(ProjectileSeriesState state,ProjectileStopReason reason,
        IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);
        if(state.Pending is not null || state.StopReason!=ProjectileStopReason.None ||
            reason is not (ProjectileStopReason.ActorUnavailable or ProjectileStopReason.ContinuationUnavailable))
            throw new InvalidOperationException("Cannot interrupt this series");
        return state with {StopReason=reason};
    }

    public static ProjectileCompletion Complete(ProjectileSeriesState state,string seriesId,int ordinal,ProjectileOutcome outcome,
        bool actorCanContinue,bool deliveryCanContinue,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);
        if(seriesId!=state.Id || state.Pending is null || ordinal!=state.Pending.Ordinal || !Enum.IsDefined(outcome))
            throw new InvalidOperationException("Unknown or already completed projectile");
        var inventory=state.Cost.Kind switch {
            ProjectileCostKind.None=>state.Inventory,
            ProjectileCostKind.Quantity=>ItemInventory.ConsumeUnit(state.Inventory,state.SourceId,rules),
            ProjectileCostKind.Resource=>ItemInventory.SpendResource(state.Inventory,state.SourceId,1,rules),
            _=>throw new InvalidOperationException("Unsupported projectile cost")};
        if(state.Cost.Kind==ProjectileCostKind.Resource && state.Cost.RemoveWhenEmpty &&
            inventory.Items.Single(i=>i.Id==state.SourceId).ResourceRemaining==0)
            inventory=ItemInventory.ConsumeUnit(inventory,state.SourceId,rules);
        var next=state with {Inventory=inventory,Remaining=state.Remaining-1,Completed=ordinal,Pending=null};
        var reason=next.Remaining==0?ProjectileStopReason.Finished:
            !actorCanContinue?ProjectileStopReason.ActorUnavailable:
            !Available(next)?ProjectileStopReason.Depleted:
            !deliveryCanContinue?ProjectileStopReason.ContinuationUnavailable:ProjectileStopReason.None;
        next=next with {StopReason=reason};
        Validate(next,rules);
        return new(next,outcome);
    }

    public static void Validate(ProjectileSeriesState state,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ItemInventory.Validate(state.Inventory,rules);
        if(string.IsNullOrWhiteSpace(state.Id) || string.IsNullOrWhiteSpace(state.SourceId) || state.Cost is null ||
            !rules.TryGetValue(state.Cost.DefinitionId,out var rule) || !Enum.IsDefined(state.Cost.Kind) ||
            !Enum.IsDefined(state.StopReason) || state.Completed<0 || state.Remaining<0 ||
            (long)state.Completed+state.Remaining is <1 or >int.MaxValue ||
            state.Cost.Kind==ProjectileCostKind.Quantity && rule.Resource!=ItemResourceKind.None ||
            state.Cost.Kind==ProjectileCostKind.Resource && rule.Resource==ItemResourceKind.None ||
            state.Cost.Kind!=ProjectileCostKind.Resource && state.Cost.RemoveWhenEmpty ||
            state.StopReason==ProjectileStopReason.Finished && state.Remaining!=0 ||
            state.StopReason!=ProjectileStopReason.Finished && state.Remaining==0)
            throw new InvalidDataException("Invalid projectile series");
        var source=Source(state);
        if(source is not null && source.DefinitionId!=state.Cost.DefinitionId ||
            state.StopReason==ProjectileStopReason.None && !Available(state))
            throw new InvalidDataException("Invalid projectile source");
        if(state.Pending is not null && (state.StopReason!=ProjectileStopReason.None ||
            state.Pending.Ordinal!=(long)state.Completed+1 || state.Pending.Source!=source))
            throw new InvalidDataException("In-flight source changed or pending ordinal is invalid");
    }
    private static ItemInstance? Source(ProjectileSeriesState state)=>state.Inventory.Items.SingleOrDefault(i=>i.Id==state.SourceId);
    private static bool Available(ProjectileSeriesState state)
    {
        var source=Source(state);
        return source is not null && (state.Cost.Kind!=ProjectileCostKind.Resource || source.ResourceRemaining>0);
    }
}
