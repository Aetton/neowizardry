namespace NeoWizardry.Core;

// The caller supplies actor-owned, accessible reserves in their search order.
// This reconstructs selection priority without emulating native slots or searching
// other actors' containers. It is not an equipment/actor eligibility service.
public static class OriginalProjectileReload
{
    public static ProjectileSeriesState ReplaceAmmunition(ProjectileSeriesState state,ItemDefinition weapon,
        string? preferredId,IReadOnlyList<string> fallbackIds,ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ProjectileSeries.Validate(state,rules);
        if(state.StopReason!=ProjectileStopReason.Depleted || !OriginalWeaponAdapter.UsesAmmunition(weapon))
            throw new InvalidOperationException("Resolve depleted launcher ammunition first");
        var preferred=Candidate(preferredId);
        var replacement=preferred??fallbackIds.Select(Candidate).FirstOrDefault(i=>i is not null && i.DefinitionId==state.Cost.DefinitionId);
        if(replacement is null)return state;
        var cost=OriginalWeaponAdapter.ProjectileExpenditure(weapon,catalog.Find(replacement.DefinitionId));
        return ProjectileSeries.ReplaceDepletedSource(state,replacement.Id,cost,rules);

        ItemInstance? Candidate(string? id)
        {
            var item=state.Inventory.Items.SingleOrDefault(i=>i.Id==id);
            return item is not null && (item.ResourceRemaining is null || item.ResourceRemaining>0) &&
                OriginalAmmunitionAdapter.Compatible(weapon,catalog.Find(item.DefinitionId))?item:null;
        }
    }

    public static ItemRefillResult Refill(ItemInventoryState inventory,string weaponId,string fuelId,
        ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ItemInventory.Validate(inventory,rules);
        var weapon=inventory.Items.Single(i=>i.Id==weaponId);var fuel=inventory.Items.Single(i=>i.Id==fuelId);
        var plan=OriginalAmmunitionAdapter.PrepareRefill(catalog.Find(weapon.DefinitionId),catalog.Find(fuel.DefinitionId));
        return ItemInventory.RefillResource(inventory,weaponId,fuelId,plan.Capacity,plan.RemoveEmptyFuel,rules);
    }

    public static ProjectileSeriesState RefillFromReserves(ProjectileSeriesState state,IReadOnlyList<string> fuelIds,
        ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ValidateEmptyWeapon(state,rules);
        int? icon=OriginalAmmunitionAdapter.FuelIcon(catalog.Find(state.Cost.DefinitionId).IconIndex);
        if(icon is null)throw new InvalidOperationException("No supported refill family");
        foreach(string id in fuelIds)
        {
            var item=state.Inventory.Items.SingleOrDefault(i=>i.Id==id);
            if(item is not null && id!=state.SourceId && (item.ResourceRemaining is null || item.ResourceRemaining>0) &&
                catalog.Find(item.DefinitionId).IconIndex==icon)
                return RefillDepletedWeapon(state,id,catalog,rules);
        }
        return state;
    }

    public static ProjectileSeriesState RefillDepletedWeapon(ProjectileSeriesState state,string fuelId,
        ItemCatalog catalog,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ValidateEmptyWeapon(state,rules);
        var result=Refill(state.Inventory,state.SourceId,fuelId,catalog,rules);
        if(result.Transferred==0)return state;
        return ProjectileSeries.ReplaceDepletedSource(state with {Inventory=result.State},state.SourceId,state.Cost,rules);
    }
    private static void ValidateEmptyWeapon(ProjectileSeriesState state,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ProjectileSeries.Validate(state,rules);
        if(state.StopReason!=ProjectileStopReason.Depleted || state.Cost.Kind!=ProjectileCostKind.Resource ||
            state.Inventory.Items.SingleOrDefault(i=>i.Id==state.SourceId) is not {ResourceRemaining:0})
            throw new InvalidOperationException("Resolve an empty retained shot weapon first");
    }

}
