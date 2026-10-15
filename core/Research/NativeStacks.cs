namespace NeoWizardry.Core.Research;

public sealed record NativeStackNormalization(NativeItemInstance Kept, NativeItemInstance[] Overflow);
public sealed record NativeSlotTransfer(NativeItemInstance Destination, NativeItemInstance Source);
public sealed record NativeStackMerge(NativeItemInstance Destination, NativeItemInstance Source, int Moved, string? Reason=null)
{
    // The original boolean reports complete source removal, not whether anything moved.
    public bool SourceFullyTransferred=>Moved>0 && Source.IsEmpty;
}

public static class NativeStacks
{
    public static NativeItemInstance Empty {get;}=new(-1,0,0,0,0,0,0,0,0);

    public static NativeStackNormalization Normalize(NativeItemInstance item,ItemDefinition definition,int extraSlots)
    {
        if(extraSlots<0)throw new InvalidOperationException("Invalid free-slot count");
        if(item.IsEmpty)return new(item,[]);
        var fields=Fields(item,definition);
        if(fields.Mode is <0 or >4)throw new InvalidOperationException("Unsupported native mode");
        var normalized=item;
        if(fields.Mode==1 && item.ChargesRaw>0)
            normalized=item with {CountRaw=item.CountRaw==0?item.ChargesRaw:item.CountRaw,ChargesRaw=0};
        else if(fields.Mode is >=2 and <=4 && item.CountRaw>0)
            normalized=item with {CountRaw=0,ChargesRaw=item.ChargesRaw==0?item.CountRaw:item.ChargesRaw};
        if(fields.Mode!=1)return new(normalized,[]);
        if(fields.MaxStack==0)throw new InvalidOperationException("Zero stack limit cannot preserve quantities");
        int excess=Math.Max(0,normalized.CountRaw-fields.MaxStack);
        int needed=(excess+fields.MaxStack-1)/fields.MaxStack;
        // Unlike the original full-bag clamp, this guard never discards excess units.
        if(needed>extraSlots)throw new InvalidOperationException("Not enough slots to preserve excess items");
        var overflow=new List<NativeItemInstance>();int remaining=normalized.CountRaw;
        while(remaining>fields.MaxStack)
        {
            int moved=Math.Min(remaining-fields.MaxStack,fields.MaxStack);
            // The original copies overflow through the move helper, clearing +10.
            overflow.Add(normalized with {CountRaw=(byte)moved,UncursedRaw=0});remaining-=moved;
        }
        return new(normalized with {CountRaw=(byte)remaining},overflow.ToArray());
    }

    // Counter normalization and destination overflow handling are separate, explicit steps.
    public static NativeStackMerge MergeNormalized(NativeItemInstance destination,NativeItemInstance source,ItemDefinition definition)
    {
        if(destination.IsEmpty || source.IsEmpty)return new(destination,source,0,"Empty slot");
        var fields=Fields(destination,definition);
        if(fields.Mode!=1)return new(destination,source,0,"Item is not stackable");
        if(destination.ItemId!=source.ItemId)return new(destination,source,0,"Different item IDs");
        if(destination.IdentificationRaw!=source.IdentificationRaw)return new(destination,source,0,"Different identification bytes");
        if(fields.MaxStack==0 || destination.CountRaw==0 || source.CountRaw==0 || destination.CountRaw>fields.MaxStack ||
            destination.ChargesRaw!=0 || source.ChargesRaw!=0)
            throw new InvalidOperationException("Normalize stack counters and destination overflow first");
        int moved=Math.Min(fields.MaxStack-destination.CountRaw,source.CountRaw);
        if(moved==0)return new(destination,source,0,"Destination full");
        return new(destination with {CountRaw=(byte)(destination.CountRaw+moved)},
            source.CountRaw==moved?Empty:source with {CountRaw=(byte)(source.CountRaw-moved)},moved);
    }

    public static NativeSlotTransfer MoveToEmpty(NativeItemInstance destination,NativeItemInstance source)
    {
        if(!destination.IsEmpty || source.IsEmpty || source.ItemId<0)
            throw new InvalidOperationException("Move requires an empty destination and an occupied source");
        return new(source with {UncursedRaw=0},Empty);
    }

    private static NativeItemFields Fields(NativeItemInstance item,ItemDefinition definition)
    {
        if(item.ItemId<0 || item.ItemId!=definition.Id)throw new InvalidOperationException("Native definition mismatch");
        var fields=definition.Instance??throw new InvalidOperationException("Native metadata required");
        fields.Validate(definition.RecordSha256);return fields;
    }
}
