namespace NeoWizardry.Core;

// Recipe quantities and legacy-save input only; never authoritative owned items.
public sealed record ItemStack(int ItemId, int Count);

public static class Inventory
{
    // Prototype resource guard, configurable in ItemInventoryState; not a balance rule.
    public const int DefaultCapacity=4096;
    public static readonly ItemLocation BagLocation=new("party");
    public static IEnumerable<ItemInstance> Bag(GameState state)=>state.Items!.Items.Where(i=>i.Location==BagLocation);
    public static long Count(GameState state,int itemId)=>Bag(state).Where(i=>i.DefinitionId==itemId).Sum(i=>(long)i.Quantity);
    public static IReadOnlyDictionary<int,ItemInventoryRule> Rules(ItemCatalog catalog)=>CompiledItemCatalog.Get(catalog).Rules;

    public static void ValidateStructure(GameState state)
    {
        if(state.Items is null || state.NextItemSequence<1)throw new InvalidDataException("Missing instance inventory or identity sequence");
        ItemInventory.ValidateStructure(state.Items);
        foreach(var item in state.Items.Items)
        {
            if(item.Location!=BagLocation && (item.Location.EquipmentSlot!=Equipment.WeaponSlot || !state.Party.Any(h=>h.Id==item.Location.ContainerId) && !(state.Melee is not null && (item.Location.ContainerId==state.Enemy.Id || state.Melee.Field?.Opponents.Any(o=>o.Actor.Id==item.Location.ContainerId)==true))))
                throw new InvalidDataException("Unknown inventory location");
            if(item.Id.StartsWith("item-",StringComparison.Ordinal) && long.TryParse(item.Id.AsSpan(5),out long sequence) && sequence>=state.NextItemSequence)
                throw new InvalidDataException("Item identity sequence would reuse an existing ID");
        }
    }
    public static void Validate(GameState state,ItemCatalog catalog)
    {
        Game.Validate(state);ItemInventory.Validate(state.Items!,Rules(catalog));
    }
    internal static string FreshId(ref long sequence)=>$"item-{checked(sequence++):D12}";
    internal static GameState Commit(GameState state,ItemInventoryState items,long sequence,ItemCatalog catalog)
    {
        ItemInventory.Validate(items,Rules(catalog));
        var result=state with {Items=items,NextItemSequence=sequence};Game.Validate(result);return result;
    }
    // Creation works with semantic fields. Native byte layouts are not involved.
    internal static ItemInstance[] CreateItems(int itemId,int quantity,ItemLocation location,ItemCatalog catalog,
        ref long sequence,ref uint randomState,int available,bool migrating=false)
    {
        if(quantity<=0)throw new ArgumentOutOfRangeException(nameof(quantity));
        var definition=catalog.Find(itemId);var rule=Rules(catalog)[itemId];
        long required=((long)quantity+rule.StackLimit-1)/rule.StackLimit;
        if(required>available)throw new InvalidOperationException("Inventory capacity exceeded.");
        var result=new List<ItemInstance>((int)required);
        while(quantity>0)
        {
            int count=Math.Min(quantity,rule.StackLimit);int? resource=null;
            if(rule.Resource!=ItemResourceKind.None)
            {
                // Legacy aggregate saves never recorded remaining resources. Do not grant a refill.
                int amount=0;
                if(!migrating)
                {
                    var fields=definition.Instance!;amount=fields.AmountConstant;
                    for(int die=0;die<fields.AmountDice;die++)amount=checked(amount+(fields.AmountSides==0?1:Dice.Roll(ref randomState,fields.AmountSides)));
                    if(amount<0)throw new InvalidOperationException("Invalid initial item resource.");
                }
                resource=amount;
            }
            result.Add(new(FreshId(ref sequence),itemId,count,true,resource,false,location));quantity-=count;
        }
        return result.ToArray();
    }
    private static int OutputRoom(GameState state,int definitionId,int removed=0)=>(int)Math.Min(int.MaxValue,
        (long)state.Items!.Capacity-state.Items.Items.Length+removed+Bag(state).Count(i=>i.DefinitionId==definitionId));
    public static GameState Add(GameState state,int itemId,int count,ItemCatalog catalog)
    {
        Validate(state,catalog);long sequence=state.NextItemSequence;uint random=state.RandomState;
        var output=CreateItems(itemId,count,BagLocation,catalog,ref sequence,ref random,OutputRoom(state,itemId));
        var items=ItemInventory.Transform(state.Items!,[],output,Rules(catalog),mergeOutputs:true);
        return Commit(state with {RandomState=random},items,sequence,catalog);
    }
    internal static ItemConsumption[] Select(GameState state,IReadOnlyList<ItemStack> costs)
    {
        var consumed=new List<ItemConsumption>();var definitions=new HashSet<int>();
        foreach(var cost in costs)
        {
            if(cost.Count<=0 || !definitions.Add(cost.ItemId))throw new InvalidOperationException("Invalid item costs");
            int remaining=cost.Count;
            foreach(var item in Bag(state).Where(i=>i.DefinitionId==cost.ItemId).OrderBy(i=>i.Id,StringComparer.Ordinal))
            {
                int amount=Math.Min(remaining,item.Quantity);consumed.Add(new(item.Id,amount));remaining-=amount;if(remaining==0)break;
            }
            if(remaining!=0)throw new InvalidOperationException("Not enough items in the party bag.");
        }
        return consumed.ToArray();
    }
    public static GameState Transform(GameState state,IReadOnlyList<ItemStack> costs,ItemStack result,ItemCatalog catalog)
    {
        Validate(state,catalog);var consumed=Select(state,costs);
        int removed=consumed.Count(c=>state.Items!.Items.Single(i=>i.Id==c.InstanceId).Quantity==c.Quantity);
        long sequence=state.NextItemSequence;uint random=state.RandomState;
        var output=CreateItems(result.ItemId,result.Count,BagLocation,catalog,ref sequence,ref random,OutputRoom(state,result.ItemId,removed));
        return Commit(state with {RandomState=random},ItemInventory.Transform(state.Items!,consumed,output,Rules(catalog),mergeOutputs:true),sequence,catalog);
    }
    public static GameState Remove(GameState state,int itemId,int count,ItemCatalog catalog)
    {
        Validate(state,catalog);
        return Commit(state,ItemInventory.Transform(state.Items!,Select(state,[new(itemId,count)]),[],Rules(catalog)),state.NextItemSequence,catalog);
    }
    public static GameState Split(GameState state,string id,int count,ItemCatalog catalog)
    {
        Validate(state,catalog);long sequence=state.NextItemSequence;
        return Commit(state,ItemInventory.Split(state.Items!,id,count,FreshId(ref sequence),Rules(catalog)),sequence,catalog);
    }
    public static (GameState State,int Moved) Merge(GameState state,string destination,string source,ItemCatalog catalog)
    {
        Validate(state,catalog);var merged=ItemInventory.Merge(state.Items!,destination,source,Rules(catalog));
        return (Commit(state,merged.State,state.NextItemSequence,catalog),merged.Moved);
    }
    public static GameState SpendResource(GameState state,string id,int amount,ItemCatalog catalog)
    {
        Validate(state,catalog);return Commit(state,ItemInventory.SpendResource(state.Items!,id,amount,Rules(catalog)),state.NextItemSequence,catalog);
    }
}
