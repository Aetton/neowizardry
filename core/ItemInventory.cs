using System.Text.Json;

namespace NeoWizardry.Core;

public enum ItemResourceKind { None, Charges, Uses, Shots }
public sealed record ItemInventoryRule(int DefinitionId,int StackLimit=1,ItemResourceKind Resource=ItemResourceKind.None);
public sealed record ItemLocation(string ContainerId,string? EquipmentSlot=null);
public sealed record ItemInstance(string Id,int DefinitionId,int Quantity,bool Identified,int? ResourceRemaining,
    bool CurseRemoved,ItemLocation Location);
public sealed record ItemInventoryState(int Schema,int Capacity,ItemInstance[] Items);
public sealed record ItemConsumption(string InstanceId,int Quantity);
public sealed record ItemMergeResult(ItemInventoryState State,int Moved);

// Canonical inventory model. Original slot layouts, repairs and byte flags do not
// enter this state. One instance record owns its location, including equipment.
public static class ItemInventory
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower};
    public static ItemInventoryState Create(int capacity=int.MaxValue)=>capacity>=0?new(1,capacity,[]):throw new ArgumentOutOfRangeException(nameof(capacity));
    public static void ValidateStructure(ItemInventoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if(state.Schema!=1 || state.Capacity<0 || state.Items is null || state.Items.Length>state.Capacity)
            throw new InvalidDataException("Invalid inventory");
        var ids=new HashSet<string>(StringComparer.Ordinal);var equipment=new HashSet<(string,string)>();
        foreach(var item in state.Items)
        {
            if(item is null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id) || item.DefinitionId<0 ||
                item.Quantity<1 || item.ResourceRemaining<0 || item.Location is null || string.IsNullOrWhiteSpace(item.Location.ContainerId))
                throw new InvalidDataException("Invalid item instance");
            if(item.Location.EquipmentSlot is not null && (string.IsNullOrWhiteSpace(item.Location.EquipmentSlot) || item.Quantity!=1 ||
                !equipment.Add((item.Location.ContainerId,item.Location.EquipmentSlot))))
                throw new InvalidDataException("Equipment location is invalid or occupied");
        }
    }
    public static void Validate(ItemInventoryState state,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ValidateStructure(state);
        if(rules.Any(p=>p.Value is null || p.Key!=p.Value.DefinitionId || p.Key<0 || p.Value.StackLimit<1 ||
            !Enum.IsDefined(p.Value.Resource) || (p.Value.Resource!=ItemResourceKind.None && p.Value.StackLimit!=1)))
            throw new InvalidDataException("Invalid item rules");
        foreach(var item in state.Items)
            if(!rules.TryGetValue(item.DefinitionId,out var rule) || item.Quantity>rule.StackLimit ||
                (rule.Resource==ItemResourceKind.None?item.ResourceRemaining is not null:item.ResourceRemaining is null))
                throw new InvalidDataException("Instance does not match item rules");
    }
    public static string Save(ItemInventoryState state,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);return JsonSerializer.Serialize(Sorted(state),Json);
    }
    public static ItemInventoryState Load(string json,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        var state=JsonSerializer.Deserialize<ItemInventoryState>(json,Json)??throw new InvalidDataException("Empty inventory");
        Validate(state,rules);return Sorted(state);
    }
    public static ItemInventoryState Add(ItemInventoryState state,ItemInstance item,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);return Checked(state with {Items=state.Items.Append(item).ToArray()},rules);
    }
    public static ItemInventoryState Move(ItemInventoryState state,string id,ItemLocation location,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);var item=Find(state,id);
        return Checked(state with {Items=state.Items.Select(i=>i.Id==id?item with {Location=location}:i).ToArray()},rules);
    }
    public static ItemInventoryState SwapLocations(ItemInventoryState state,string firstId,string secondId,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);var first=Find(state,firstId);var second=Find(state,secondId);
        return Checked(state with {Items=state.Items.Select(i=>i.Id==firstId?i with {Location=second.Location}:
            i.Id==secondId?i with {Location=first.Location}:i).ToArray()},rules);
    }
    public static ItemInventoryState Split(ItemInventoryState state,string id,int quantity,string newId,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);var item=Find(state,id);
        if(quantity<=0 || quantity>=item.Quantity || state.Items.Any(i=>i.Id==newId))throw new InvalidOperationException("Invalid stack split");
        return Checked(state with {Items=state.Items.Select(i=>i.Id==id?i with {Quantity=i.Quantity-quantity}:i)
            .Append(item with {Id=newId,Quantity=quantity}).ToArray()},rules);
    }
    public static ItemMergeResult Merge(ItemInventoryState state,string destinationId,string sourceId,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);var destination=Find(state,destinationId);var source=Find(state,sourceId);
        if(destinationId==sourceId)throw new InvalidOperationException("Cannot merge an instance with itself");
        if(destination.DefinitionId!=source.DefinitionId || destination.Identified!=source.Identified ||
            destination.ResourceRemaining!=source.ResourceRemaining || destination.CurseRemoved!=source.CurseRemoved ||
            destination.Location!=source.Location || destination.Location.EquipmentSlot is not null)
            return new(state,0);
        int moved=Math.Min(rules[destination.DefinitionId].StackLimit-destination.Quantity,source.Quantity);
        if(moved==0)return new(state,0);
        var items=state.Items.Where(i=>i.Id!=sourceId || i.Quantity!=moved)
            .Select(i=>i.Id==destinationId?i with {Quantity=i.Quantity+moved}:i.Id==sourceId?i with {Quantity=i.Quantity-moved}:i).ToArray();
        return new(Checked(state with {Items=items},rules),moved);
    }
    public static ItemInventoryState SpendResource(ItemInventoryState state,string id,int amount,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);var item=Find(state,id);
        if(amount<=0 || item.ResourceRemaining is null || item.ResourceRemaining<amount)throw new InvalidOperationException("Not enough item resource");
        return Checked(state with {Items=state.Items.Select(i=>i.Id==id?i with {ResourceRemaining=i.ResourceRemaining-amount}:i).ToArray()},rules);
    }
    // Combat/use may consume the equipped singleton as well as a bag stack. Crafting
    // keeps its own stricter equipment policy in Transform; no generic bypass flag.
    public static ItemInventoryState ConsumeUnit(ItemInventoryState state,string id,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        Validate(state,rules);var item=Find(state,id);
        return Checked(state with {Items=state.Items.Where(i=>i.Id!=id || item.Quantity>1)
            .Select(i=>i.Id==id?i with {Quantity=i.Quantity-1}:i).ToArray()},rules);
    }
    // Final-state validation makes a recipe transaction atomic even at full capacity.
    // Recipe/actor/skill policies belong to the calling gameplay service.
    public static ItemInventoryState Transform(ItemInventoryState state,IReadOnlyList<ItemConsumption> consumed,
        IReadOnlyList<ItemInstance> produced,IReadOnlyDictionary<int,ItemInventoryRule> rules,bool mergeOutputs=false)
    {
        Validate(state,rules);var costs=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var cost in consumed)
        {
            if(cost is null || string.IsNullOrWhiteSpace(cost.InstanceId) || cost.Quantity<=0 || !costs.TryAdd(cost.InstanceId,cost.Quantity))
                throw new InvalidOperationException("Invalid or repeated ingredient selection");
            var item=Find(state,cost.InstanceId);
            if(item.Quantity<cost.Quantity || item.Location.EquipmentSlot is not null)throw new InvalidOperationException("Ingredient unavailable");
        }
        if(produced.Any(i=>i is null || state.Items.Any(old=>old.Id==i.Id)))throw new InvalidOperationException("Results require fresh instance IDs");
        var remaining=state.Items.Where(i=>!costs.TryGetValue(i.Id,out int amount) || amount<i.Quantity)
            .Select(i=>costs.TryGetValue(i.Id,out int amount)?i with {Quantity=i.Quantity-amount}:i).ToList();
        // Validate outputs independently before optional packing. Capacity applies to the final state.
        Validate(new(1,int.MaxValue,produced.ToArray()),rules);
        foreach(var output in produced)
        {
            int quantity=output.Quantity;
            if(mergeOutputs && output.Location.EquipmentSlot is null)
                for(int i=0;i<remaining.Count && quantity>0;i++)
                {
                    var target=remaining[i];
                    if(target.DefinitionId!=output.DefinitionId || target.Identified!=output.Identified ||
                        target.ResourceRemaining!=output.ResourceRemaining || target.CurseRemoved!=output.CurseRemoved || target.Location!=output.Location)continue;
                    int moved=Math.Min(rules[target.DefinitionId].StackLimit-target.Quantity,quantity);
                    if(moved>0){remaining[i]=target with {Quantity=target.Quantity+moved};quantity-=moved;}
                }
            if(quantity>0)remaining.Add(output with {Quantity=quantity});
        }
        return Checked(state with {Items=remaining.ToArray()},rules);
    }
    public static long Count(ItemInventoryState state,int definitionId,string? container=null)=>state.Items
        .Where(i=>i.DefinitionId==definitionId && (container is null || i.Location.ContainerId==container)).Sum(i=>(long)i.Quantity);
    private static ItemInstance Find(ItemInventoryState state,string id)=>state.Items.FirstOrDefault(i=>i.Id==id)
        ??throw new InvalidOperationException("Unknown item instance");
    private static ItemInventoryState Sorted(ItemInventoryState state)=>state with {Items=state.Items.OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray()};
    private static ItemInventoryState Checked(ItemInventoryState state,IReadOnlyDictionary<int,ItemInventoryRule> rules) {Validate(state,rules);return Sorted(state);}
}
