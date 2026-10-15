using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWizardry.Core.Research;

// Persistent IDs and stable slot indices are remake bookkeeping, not original save fields.
public sealed record NativeInventoryEntry(int Slot,string InstanceId,string RawSlotHex)
{
    [JsonIgnore] public NativeItemInstance Item=>NativeItemInstance.Decode(Convert.FromHexString(RawSlotHex));
    public static NativeInventoryEntry From(int slot,string id,NativeItemInstance item)=>new(slot,id,Convert.ToHexString(item.Encode()));
}
public sealed record NativeInventoryState(int Schema,string ItemDatabaseHash,int Capacity,NativeInventoryEntry[] Entries);
public sealed record NativeInventoryMerge(NativeInventoryState State,int Moved,bool SourceFullyTransferred,string? Reason);
public sealed record NativeCombinationSimulation(NativeInventoryState State,NativeCombinationPlan Plan);

// Independent native inventory snapshot; no implicit migration from GameState's shared bag.
public static class NativeInventory
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower};
    public static NativeInventoryState Create(ItemCatalog catalog,int capacity=500)
    {
        var state=new NativeInventoryState(1,catalog.Provenance.Sha256,capacity,[]);Validate(state,catalog);return state;
    }
    public static void Validate(NativeInventoryState state,ItemCatalog catalog)
    {
        catalog.Validate();
        if(catalog.Schema!=3 || state.Schema!=1 || state.Capacity is <0 or >500 || state.Entries is null ||
            state.Entries.Length>state.Capacity || state.ItemDatabaseHash is not {Length:64} ||
            !state.ItemDatabaseHash.All(Uri.IsHexDigit) || !state.ItemDatabaseHash.Equals(catalog.Provenance.Sha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native inventory requires the same schema-3 item database");
        var slots=new HashSet<int>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in state.Entries)
        {
            if(entry is null || entry.Slot<0 || entry.Slot>=state.Capacity || !slots.Add(entry.Slot) ||
                string.IsNullOrWhiteSpace(entry.InstanceId) || !ids.Add(entry.InstanceId) ||
                entry.RawSlotHex is not {Length:24} || !entry.RawSlotHex.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid native inventory entry");
            var item=entry.Item;
            if(item.IsEmpty || item.ItemId<0 || catalog.Find(item.ItemId).Instance is null)
                throw new InvalidDataException("Native inventory entry requires an occupied known item slot");
        }
        if(!state.Entries.Select(e=>e.Slot).SequenceEqual(state.Entries.Select(e=>e.Slot).Order()))
            throw new InvalidDataException("Native inventory entries must be in slot order");
    }
    public static string Save(NativeInventoryState state,ItemCatalog catalog) { Validate(state,catalog);return JsonSerializer.Serialize(state,Json); }
    public static NativeInventoryState Load(string json,ItemCatalog catalog)
    {
        var state=JsonSerializer.Deserialize<NativeInventoryState>(json,Json)??throw new InvalidDataException("Empty native inventory");
        Validate(state,catalog);return state;
    }
    public static NativeInventoryState Add(NativeInventoryState state,int slot,string id,NativeItemInstance item,ItemCatalog catalog)
    {
        Validate(state,catalog);
        var next=state with {Entries=state.Entries.Append(NativeInventoryEntry.From(slot,id,item)).OrderBy(e=>e.Slot).ToArray()};
        Validate(next,catalog);return next;
    }
    public static NativeInventoryState Move(NativeInventoryState state,int from,int to,ItemCatalog catalog)
    {
        Validate(state,catalog);var entry=Find(state,from);
        if(to<0 || to>=state.Capacity || state.Entries.Any(e=>e.Slot==to))throw new InvalidOperationException("Destination slot unavailable");
        var moved=NativeStacks.MoveToEmpty(NativeStacks.Empty,entry.Item);
        return state with {Entries=state.Entries.Select(e=>e.Slot==from?NativeInventoryEntry.From(to,e.InstanceId,moved.Destination):e).OrderBy(e=>e.Slot).ToArray()};
    }
    public static NativeInventoryMerge Merge(NativeInventoryState state,int destinationSlot,int sourceSlot,ItemCatalog catalog)
    {
        Validate(state,catalog);
        if(destinationSlot==sourceSlot)throw new InvalidOperationException("Cannot merge an instance with itself");
        var destination=Find(state,destinationSlot);var source=Find(state,sourceSlot);
        var result=NativeStacks.MergeNormalized(destination.Item,source.Item,catalog.Find(destination.Item.ItemId));
        if(result.Moved==0)return new(state,0,false,result.Reason);
        // Original compatibility checks only ID and identification. Preserve uncertain
        // metadata by refusing a destructive merge until an explicit policy exists.
        if((destination.Item with {CountRaw=0})!=(source.Item with {CountRaw=0}))
            throw new InvalidOperationException("Stack metadata differ; merge would discard source information");
        var entries=state.Entries.Where(e=>e.Slot!=sourceSlot || !result.SourceFullyTransferred)
            .Select(e=>e.Slot==destinationSlot?NativeInventoryEntry.From(e.Slot,e.InstanceId,result.Destination):
                e.Slot==sourceSlot?NativeInventoryEntry.From(e.Slot,e.InstanceId,result.Source):e).ToArray();
        return new(state with {Entries=entries},result.Moved,result.SourceFullyTransferred,null);
    }
    public static NativeInventoryState Normalize(NativeInventoryState state,int slot,IReadOnlyList<string> overflowIds,ItemCatalog catalog)
    {
        Validate(state,catalog);var entry=Find(state,slot);
        var result=NativeStacks.Normalize(entry.Item,catalog.Find(entry.Item.ItemId),state.Capacity-state.Entries.Length);
        if(overflowIds is null || overflowIds.Count!=result.Overflow.Length)throw new InvalidOperationException("Supply one fresh ID per overflow stack");
        var occupied=state.Entries.Select(e=>e.Slot).ToHashSet();
        var free=Enumerable.Range(0,state.Capacity).Where(i=>!occupied.Contains(i)).Take(result.Overflow.Length).ToArray();
        var entries=state.Entries.Select(e=>e.Slot==slot?NativeInventoryEntry.From(slot,e.InstanceId,result.Kept):e)
            .Concat(result.Overflow.Select((item,i)=>NativeInventoryEntry.From(free[i],overflowIds[i],item))).OrderBy(e=>e.Slot).ToArray();
        var next=state with {Entries=entries};Validate(next,catalog);return next;
    }
    // Local research simulation only: caller supplies the relevant skill value and
    // resolved dice. Actor eligibility, training/time and world state are absent.
    public static NativeCombinationSimulation SimulateCombination(NativeInventoryState state,int firstSlot,int secondSlot,
        int actorSkillValue,IReadOnlyList<int> draws,string outputId,IReadOnlyList<string> overflowIds,ItemCatalog catalog,RecipeCatalog recipes)
    {
        Validate(state,catalog);recipes.Validate();
        if(!recipes.Provenance.Sha256.Equals(catalog.Provenance.Sha256,StringComparison.OrdinalIgnoreCase) || recipes.ItemCount!=catalog.Count)
            throw new InvalidOperationException("Combination and item databases differ");
        if(firstSlot==secondSlot)throw new InvalidOperationException("Select two separate instances");
        var first=Find(state,firstSlot);var second=Find(state,secondSlot);
        var recipe=recipes.Recipes.FirstOrDefault(r=>(r.IngredientIds[0]==first.Item.ItemId && r.IngredientIds[1]==second.Item.ItemId) ||
            (r.IngredientIds[1]==first.Item.ItemId && r.IngredientIds[0]==second.Item.ItemId))
            ??throw new InvalidOperationException("No matching combination");
        if(recipe.Issues.Length!=0)throw new InvalidOperationException("Flagged combination is not executable");
        var definition=catalog.Find(recipe.OutputId);
        if(!definition.RecordSha256.Equals(recipe.RecordSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Combination output record differs");
        foreach(var entry in new[]{first,second})
            if(NativeStacks.Normalize(entry.Item,catalog.Find(entry.Item.ItemId),0).Kept!=entry.Item)
                throw new InvalidOperationException("Normalize ingredient counters first");
        if(overflowIds is null || new[]{outputId}.Concat(overflowIds).Any(id=>string.IsNullOrWhiteSpace(id) || state.Entries.Any(e=>e.InstanceId==id)) ||
            new[]{outputId}.Concat(overflowIds).Distinct(StringComparer.Ordinal).Count()!=overflowIds.Count+1)
            throw new InvalidOperationException("Result instances require fresh unique IDs");
        var plan=NativeCombinations.Plan(new(recipe.OutputId,recipe.IngredientIds[0],recipe.IngredientIds[1],definition.Instance!.Mode,recipe.SkillCode,recipe.SkillThreshold),
            first.Item.ToIngredient(first.InstanceId,catalog.Find(first.Item.ItemId)),second.Item.ToIngredient(second.InstanceId,catalog.Find(second.Item.ItemId)),actorSkillValue);
        var output=NativeItemFactory.CreateCombinationOutput(definition,plan,draws);
        int destination=plan.Destination=="first"?firstSlot:secondSlot;
        var entries=state.Entries.Where(e=>e.Slot!=firstSlot && e.Slot!=secondSlot).ToList();
        if(plan.FirstRemaining>0)entries.Add(NativeInventoryEntry.From(firstSlot,first.InstanceId,first.Item with {CountRaw=(byte)plan.FirstRemaining}));
        if(plan.SecondRemaining>0)entries.Add(NativeInventoryEntry.From(secondSlot,second.InstanceId,second.Item with {CountRaw=(byte)plan.SecondRemaining}));
        entries.Add(NativeInventoryEntry.From(destination,outputId,output));
        var draft=state with {Entries=entries.OrderBy(e=>e.Slot).ToArray()};
        return new(Normalize(draft,destination,overflowIds,catalog),plan);
    }
    private static NativeInventoryEntry Find(NativeInventoryState state,int slot)=>state.Entries.FirstOrDefault(e=>e.Slot==slot)
        ??throw new InvalidOperationException("Native inventory slot is empty");
}
