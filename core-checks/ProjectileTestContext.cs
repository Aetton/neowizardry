using System.Text.Json.Serialization;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

// Test-only fixture bundle: the old arithmetic/resource cases keep an inventory beside
// their series. Production flight state contains no inventory. Authoritative game-state
// transitions are exercised separately, directly through ProjectileExecution.
sealed record ProjectileTestContext(ProjectileSeriesState Series,ItemInventoryState Inventory)
{
    [JsonIgnore] public string Id=>Series.Id;
    [JsonIgnore] public string SourceId=>Series.SourceId;
    [JsonIgnore] public ProjectileCost Cost=>Series.Cost;
    [JsonIgnore] public int Remaining=>Series.Remaining;
    [JsonIgnore] public int Completed {get=>Series.Completed;init=>Series=Series with {Completed=value};}
    [JsonIgnore] public PendingProjectile? Pending=>Series.Pending;
    [JsonIgnore] public ProjectileStopReason StopReason=>Series.StopReason;
}
sealed record ProjectileTestCompletion(ProjectileTestContext State,ProjectileOutcome Outcome);
sealed record ProjectileTestLaunch(ProjectileTestContext State,ProjectileAimResult Aim);
sealed record ProjectileTestStrike(ProjectileTestContext State,ProjectileStrikeResult Strike);

static class ProjectileTestSeries
{
    public static ProjectileTestContext Create(string id,ItemInventoryState inventory,string sourceId,ProjectileCost cost,
        int allocatedSwings,IReadOnlyDictionary<int,ItemInventoryRule> rules)=>
        new(ProjectileSeries.Create(id,inventory,sourceId,cost,allocatedSwings,rules),inventory);
    public static ProjectileTestContext Launch(ProjectileTestContext state,IReadOnlyDictionary<int,ItemInventoryRule> rules)=>
        state with {Series=ProjectileSeries.Launch(state.Series,state.Inventory,rules)};
    public static ProjectileTestContext Stop(ProjectileTestContext state,ProjectileStopReason reason,IReadOnlyDictionary<int,ItemInventoryRule> rules)=>
        state with {Series=ProjectileSeries.Stop(state.Series,state.Inventory,reason,rules)};
    public static void Validate(ProjectileTestContext state,IReadOnlyDictionary<int,ItemInventoryRule> rules)=>
        ProjectileSeries.Validate(state.Series,state.Inventory,rules);
    public static ProjectileTestContext ReplaceDepletedSource(ProjectileTestContext state,string id,ProjectileCost cost,IReadOnlyDictionary<int,ItemInventoryRule> rules)=>
        state with {Series=ProjectileSeries.ReplaceDepletedSource(state.Series,state.Inventory,id,cost,rules)};
    public static ProjectileTestCompletion Complete(ProjectileTestContext state,string id,int ordinal,ProjectileOutcome outcome,bool actor,bool delivery,
        IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var r=ProjectileSeries.Complete(state.Series,state.Inventory,id,ordinal,outcome,actor,delivery,rules);
        return new(new(r.State,r.Inventory),r.Outcome);
    }
}
static class ProjectileTestAttack
{
    public static ProjectileTestLaunch Launch(ProjectileTestContext state,ProjectileAimInput input,int angleDraw,int? radiusDraw,
        IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var r=OriginalProjectileAttack.Launch(state.Series,state.Inventory,input,angleDraw,radiusDraw,rules);
        return new(new(r.State,state.Inventory),r.Aim);
    }
    public static ProjectileTestStrike Complete(ProjectileTestContext state,string id,int ordinal,ProjectileStrikeInput input,
        int? penetrationDraw,IReadOnlyList<int> damageDraws,bool actor,bool delivery,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var r=OriginalProjectileAttack.Complete(state.Series,state.Inventory,id,ordinal,input,penetrationDraw,damageDraws,actor,delivery,rules);
        return new(new(r.State,r.Inventory),r.Strike);
    }
}
static class ProjectileTestReload
{
    public static ItemRefillResult Refill(ItemInventoryState inventory,string weapon,string fuel,ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)=>
        OriginalProjectileReload.Refill(inventory,weapon,fuel,catalog,rules);
    public static ProjectileTestContext ReplaceAmmunition(ProjectileTestContext state,ItemDefinition weapon,string? preferred,IReadOnlyList<string> fallback,
        ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)=>
        state with {Series=OriginalProjectileReload.ReplaceAmmunition(state.Series,state.Inventory,weapon,preferred,fallback,catalog,rules)};
    public static ProjectileTestContext RefillFromReserves(ProjectileTestContext state,IReadOnlyList<string> fuel,ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var r=OriginalProjectileReload.RefillFromReserves(state.Series,state.Inventory,fuel,catalog,rules);return new(r.State,r.Inventory);
    }
    public static ProjectileTestContext RefillDepletedWeapon(ProjectileTestContext state,string fuel,ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var r=OriginalProjectileReload.RefillDepletedWeapon(state.Series,state.Inventory,fuel,catalog,rules);return new(r.State,r.Inventory);
    }
}
