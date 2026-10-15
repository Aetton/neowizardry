using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace NeoWizardry.Core;

public sealed record NativeItemFields(int Mode, int AmountConstant, int AmountDice, int AmountSides,
    int MaxStack, [property:JsonPropertyName("flags_41")] int Flags41, string RawRecordHex)
{
    public void Validate(string recordHash)
    {
        if(Mode is <0 or >255 || AmountConstant is <short.MinValue or >short.MaxValue ||
            AmountDice is <0 or >255 || AmountSides is <0 or >255 || MaxStack is <0 or >255 || Flags41 is <0 or >255 ||
            RawRecordHex is not {Length:538} || !RawRecordHex.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid native item fields");
        var raw=Convert.FromHexString(RawRecordHex);
        if(!Convert.ToHexString(SHA256.HashData(raw)).Equals(recordHash,StringComparison.OrdinalIgnoreCase) ||
            raw[0x66]!=Mode || BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x67))!=AmountConstant ||
            raw[0x69]!=AmountDice || raw[0x6a]!=AmountSides || raw[0x6b]!=MaxStack || raw[0x41]!=Flags41)
            throw new InvalidDataException("Native item fields disagree with source record");
    }
}

// A 12-byte local item slot, not a parser for a whole character/save file.
// Raw bytes/flags are retained even for empty slots and unrecognized values.
public sealed record NativeItemInstance(int ItemId, byte CountRaw, byte ChargesRaw, byte IdentificationRaw,
    byte Unknown7, byte Unknown8, byte Unknown9, byte UncursedRaw, byte Unknown11)
{
    public bool IsEmpty=>ItemId==-1;
    public bool IsIdentified=>IdentificationRaw!=0;
    public static NativeItemInstance Decode(ReadOnlySpan<byte> raw)
    {
        if(raw.Length!=12)throw new InvalidDataException("Native item slot must be exactly 12 bytes");
        return new(BinaryPrimitives.ReadInt32LittleEndian(raw),raw[4],raw[5],raw[6],raw[7],raw[8],raw[9],raw[10],raw[11]);
    }
    public byte[] Encode()
    {
        var raw=new byte[12];BinaryPrimitives.WriteInt32LittleEndian(raw,ItemId);
        new byte[]{CountRaw,ChargesRaw,IdentificationRaw,Unknown7,Unknown8,Unknown9,UncursedRaw,Unknown11}.CopyTo(raw,4);
        return raw;
    }
    public NativeIngredient ToIngredient(string instanceId,ItemDefinition definition)
    {
        if(IsEmpty || ItemId<0 || definition.Id!=ItemId)throw new InvalidOperationException("Instance definition mismatch");
        var fields=definition.Instance??throw new InvalidOperationException("Native metadata required");
        fields.Validate(definition.RecordSha256);
        return new(instanceId,ItemId,fields.Mode,fields.Mode==1?CountRaw:1,IsIdentified);
    }
}

public static class NativeItemFactory
{
    // Draws are already-resolved zero-based results, not the prototype RNG.
    // This preserves draw consumption while leaving original RNG reconstruction separate.
    public static NativeItemInstance Create(ItemDefinition definition,IReadOnlyList<int> draws,
        bool useMaximum=false,bool forceIdentified=false,bool setUnknown11=false)
    {
        var fields=definition.Instance??throw new InvalidOperationException("Native metadata required");
        fields.Validate(definition.RecordSha256);
        if(definition.Id<0 || fields.Mode is <0 or >4)throw new InvalidOperationException("Unsupported native item definition");
        int needed=fields.Mode!=0 && !useMaximum && fields.AmountSides>0?fields.AmountDice:0;
        if(draws is null || draws.Count!=needed || draws.Any(d=>d<0 || d>=fields.AmountSides))
            throw new InvalidOperationException("Incorrect native amount draws");
        int amount=0;
        if(fields.Mode!=0)
        {
            int min=fields.AmountConstant+fields.AmountDice;
            int max=fields.AmountConstant+fields.AmountDice*Math.Max(1,fields.AmountSides);
            // Reject unsafe byte wrapping; this guard is stricter than the original assertion.
            if(useMaximum)min=max=fields.AmountConstant+fields.AmountDice*fields.AmountSides;
            if(min<0 || max>255)throw new InvalidOperationException("Native amount cannot fit in a byte");
            amount=useMaximum?max:fields.AmountConstant+fields.AmountDice+draws.Sum();
        }
        return new(definition.Id,fields.Mode==1?(byte)amount:(byte)0,
            fields.Mode is >=2 and <=4?(byte)amount:(byte)0,
            (byte)(forceIdentified || (fields.Flags41&1)!=0?1:0),0,0,0,0,(byte)(setUnknown11?1:0));
    }

    public static NativeItemInstance CreateCombinationOutput(ItemDefinition definition,NativeCombinationPlan plan,IReadOnlyList<int> draws)
    {
        if(plan.OutputId!=definition.Id || plan.OutputQuantity is <1 or >255)
            throw new InvalidOperationException("Combination output mismatch");
        var created=Create(definition,draws);
        if(definition.Instance!.Mode==1)return created with {CountRaw=(byte)plan.OutputQuantity};
        if(plan.OutputQuantity!=1)throw new InvalidOperationException("Nonstack result must be one instance");
        return created;
    }
}
