namespace NeoWizardry.Core;

public sealed record OriginalItemEvidence(string SourceRecordHash,string RawSlotHex);
public sealed record AdaptedOriginalItem(ItemInstance Item,OriginalItemEvidence Evidence);

// Import boundary only. The game consumes ItemInventoryRule/ItemInstance, never raw slots.
public static class OriginalItemAdapter
{
    public static ItemInventoryRule Rule(ItemDefinition definition)
    {
        var fields=definition.Instance??throw new InvalidOperationException("Native metadata required for import");
        fields.Validate(definition.RecordSha256);
        return fields.Mode switch {
            0=>new(definition.Id),1 when fields.MaxStack>0=>new(definition.Id,fields.MaxStack),
            2=>new(definition.Id,1,ItemResourceKind.Charges),3=>new(definition.Id,1,ItemResourceKind.Uses),
            4=>new(definition.Id,1,ItemResourceKind.Shots),_=>throw new InvalidOperationException("Unsupported imported item mode")};
    }
    public static AdaptedOriginalItem Convert(ItemDefinition definition,NativeItemInstance raw,string id,ItemLocation location)
    {
        var rule=Rule(definition);
        if(raw.IsEmpty || raw.ItemId!=definition.Id || raw.UncursedRaw>1)
            throw new InvalidOperationException("Imported instance has unsupported identity or curse flags");
        bool stack=definition.Instance!.Mode==1;
        if(stack && (raw.CountRaw==0 || raw.ChargesRaw!=0 || raw.CountRaw>rule.StackLimit) ||
            rule.Resource!=ItemResourceKind.None && raw.CountRaw!=0)
            throw new InvalidOperationException("Resolve inconsistent counters or overflow in the import layer");
        var item=new ItemInstance(id,definition.Id,stack?raw.CountRaw:1,raw.IsIdentified,
            rule.Resource==ItemResourceKind.None?null:raw.ChargesRaw,raw.UncursedRaw==1,location);
        var rules=new Dictionary<int,ItemInventoryRule>{{rule.DefinitionId,rule}};
        ItemInventory.Validate(new(1,1,[item]),rules);
        return new(item,new(definition.RecordSha256,System.Convert.ToHexString(raw.Encode())));
    }
}
