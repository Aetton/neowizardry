namespace NeoWizardry.Core;

public enum ProjectileCostKind { None, Quantity, Resource }
public enum ProjectileOutcome { Hit, Miss, Blocked }
public enum ProjectileStopReason { None, Finished, Depleted, ActorUnavailable, ContinuationUnavailable }
public sealed record ProjectileCost(int DefinitionId,ProjectileCostKind Kind,bool RemoveWhenEmpty=false);
public sealed record PendingProjectile(int Ordinal,ItemInstance Source,NeoWizardry.Core.Rules.ProjectileAimResult? Aim=null);
public sealed record ProjectileSeriesState(string Id,string SourceId,ProjectileCost Cost,
    int Remaining,int Completed,PendingProjectile? Pending,ProjectileStopReason StopReason);
public sealed record ProjectileCompletion(ProjectileSeriesState State,ItemInventoryState Inventory,ProjectileOutcome Outcome);

// Resource lifecycle only. Contact, damage, actor eligibility and target/range policy are resolved
// by the caller. No native slots, auto-reload, RNG, HP mutation or per-round scheduling here.
public static class ProjectileSeries
{
    public static ProjectileSeriesState Create(string id,ItemInventoryState inventory,string sourceId,ProjectileCost cost,
        int allocatedSwings,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var state=new ProjectileSeriesState(id,sourceId,cost,allocatedSwings,0,null,ProjectileStopReason.None);
        Validate(state,inventory,rules);
        if(!Available(state,inventory))throw new InvalidOperationException("Projectile source is depleted");
        return state;
    }

    public static ProjectileSeriesState Launch(ProjectileSeriesState state,ItemInventoryState inventory,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,inventory,rules);
        if(state.StopReason!=ProjectileStopReason.None || state.Pending is not null || !Available(state,inventory))
            throw new InvalidOperationException("Series cannot launch a projectile");
        return state with {Pending=new(checked(state.Completed+1),Source(state,inventory)!)};
    }

    // Before launch an interrupted action costs nothing. An in-flight projectile must finish;
    // actor incapacity after launch is supplied to Complete and stops subsequent launches.
    public static ProjectileSeriesState Stop(ProjectileSeriesState state,ItemInventoryState inventory,ProjectileStopReason reason,
        IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,inventory,rules);
        if(state.Pending is not null || state.StopReason!=ProjectileStopReason.None ||
            reason is not (ProjectileStopReason.ActorUnavailable or ProjectileStopReason.ContinuationUnavailable))
            throw new InvalidOperationException("Cannot interrupt this series");
        return state with {StopReason=reason};
    }

    public static ProjectileCompletion Complete(ProjectileSeriesState state,ItemInventoryState inventory,string seriesId,int ordinal,ProjectileOutcome outcome,
        bool actorCanContinue,bool deliveryCanContinue,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,inventory,rules);
        if(seriesId!=state.Id || state.Pending is null || ordinal!=state.Pending.Ordinal || !Enum.IsDefined(outcome))
            throw new InvalidOperationException("Unknown or already completed projectile");
        inventory=state.Cost.Kind switch {
            ProjectileCostKind.None=>inventory,
            ProjectileCostKind.Quantity=>ItemInventory.ConsumeUnit(inventory,state.SourceId,rules),
            ProjectileCostKind.Resource=>ItemInventory.SpendResource(inventory,state.SourceId,1,rules),
            _=>throw new InvalidOperationException("Unsupported projectile cost")};
        if(state.Cost.Kind==ProjectileCostKind.Resource && state.Cost.RemoveWhenEmpty &&
            inventory.Items.Single(i=>i.Id==state.SourceId).ResourceRemaining==0)
            inventory=ItemInventory.ConsumeUnit(inventory,state.SourceId,rules);
        var next=state with {Remaining=state.Remaining-1,Completed=ordinal,Pending=null};
        var reason=next.Remaining==0?ProjectileStopReason.Finished:
            !actorCanContinue?ProjectileStopReason.ActorUnavailable:
            !deliveryCanContinue?ProjectileStopReason.ContinuationUnavailable:
            !Available(next,inventory)?ProjectileStopReason.Depleted:ProjectileStopReason.None;
        next=next with {StopReason=reason};
        Validate(next,inventory,rules);
        return new(next,inventory,outcome);
    }

    // Replacement/refill preserves the allocation and event ordinal. Actor/target
    // interruption is terminal here and cannot be undone by adding ammunition.
    public static ProjectileSeriesState ReplaceDepletedSource(ProjectileSeriesState state,ItemInventoryState inventory,string sourceId,ProjectileCost cost,
        IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,inventory,rules);
        if(state.StopReason!=ProjectileStopReason.Depleted || state.Pending is not null)
            throw new InvalidOperationException("Only resource depletion can resume after reload");
        var next=state with {SourceId=sourceId,Cost=cost,StopReason=ProjectileStopReason.None};
        Validate(next,inventory,rules);
        if(!Available(next,inventory))throw new InvalidOperationException("Replacement source is depleted");
        return next;
    }

    public static void Validate(ProjectileSeriesState state,ItemInventoryState inventory,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ItemInventory.Validate(inventory,rules);
        ValidateStructure(state,inventory);
        if(!rules.TryGetValue(state.Cost.DefinitionId,out var rule) ||
            state.Cost.Kind==ProjectileCostKind.Quantity && rule.Resource!=ItemResourceKind.None ||
            state.Cost.Kind==ProjectileCostKind.Resource && rule.Resource==ItemResourceKind.None)
            throw new InvalidDataException("Projectile cost does not match item rules");
    }
    public static void ValidateStructure(ProjectileSeriesState state,ItemInventoryState inventory)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(string.IsNullOrWhiteSpace(state.Id) || string.IsNullOrWhiteSpace(state.SourceId) || state.Cost is null ||
            state.Cost.DefinitionId<0 || !Enum.IsDefined(state.Cost.Kind) ||
            !Enum.IsDefined(state.StopReason) || state.Completed<0 || state.Remaining<0 ||
            (long)state.Completed+state.Remaining is <1 or >int.MaxValue ||
            state.Cost.Kind!=ProjectileCostKind.Resource && state.Cost.RemoveWhenEmpty ||
            state.StopReason==ProjectileStopReason.Finished && state.Remaining!=0 ||
            state.StopReason!=ProjectileStopReason.Finished && state.Remaining==0)
            throw new InvalidDataException("Invalid projectile series");
        var source=Source(state,inventory);
        if(source is not null && source.DefinitionId!=state.Cost.DefinitionId ||
            state.Pending is not null && !Available(state,inventory))
            throw new InvalidDataException("Invalid projectile source");
        if(state.Pending is not null && (state.StopReason!=ProjectileStopReason.None ||
            state.Pending.Ordinal!=(long)state.Completed+1 || state.Pending.Source!=source))
            throw new InvalidDataException("In-flight source changed or pending ordinal is invalid");
    }
    private static ItemInstance? Source(ProjectileSeriesState state,ItemInventoryState inventory)=>inventory.Items.SingleOrDefault(i=>i.Id==state.SourceId);
    private static bool Available(ProjectileSeriesState state,ItemInventoryState inventory)
    {
        var source=Source(state,inventory);
        return source is not null && (state.Cost.Kind!=ProjectileCostKind.Resource || source.ResourceRemaining>0);
    }
}
