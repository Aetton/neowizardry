using System.Buffers.Binary;
using System.Text;

namespace NeoWizardry.Core;

// Import boundary: compare every exported field with its source bytes. Descriptions
// belong to a separate database and cannot be authenticated by a spell-record hash.
internal static class SourceRecordValidation
{
    private static readonly Encoding Utf16=new UnicodeEncoding(false,false,true);
    private static string Name(byte[] raw,int offset,int length)
    {
        int end=offset;
        while(end<offset+length && (raw[end]!=0 || raw[end+1]!=0))end+=2;
        try{return Utf16.GetString(raw,offset,end-offset);}
        catch(DecoderFallbackException e){throw new InvalidDataException("Invalid source name encoding",e);}
    }
    internal static void Item(ItemDefinition item)
    {
        if(item.Instance is null)return;
        item.Instance.Validate(item.RecordSha256);
        var r=Convert.FromHexString(item.Instance.RawRecordHex);
        int U16(int o)=>BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(o));
        if(item.Name!=Name(r,0,60) || item.TypeCode!=r[0x3e] || item.IconIndex!=U16(0x3f) ||
            item.ProfessionMask!=U16(0x76) || item.RaceMask!=U16(0x78) || item.GenderMask!=r[0x7c] ||
            item.UsageSkill!=r[0x46] || item.RangeCode!=r[0x47] || item.Initiative!=unchecked((sbyte)r[0x48]) ||
            item.ToHit!=unchecked((sbyte)r[0x49]) || item.DamageConstant!=BinaryPrimitives.ReadInt16LittleEndian(r.AsSpan(0x4a)) ||
            item.DamageDice!=r[0x4c] || item.DamageSides!=r[0x4d] || item.AttackModes!=U16(0x4e) ||
            item.WeightTenthsLb!=U16(0x8a) || item.Cursed!=(r[0x8c]==1) ||
            item.BonusSwings!=BinaryPrimitives.ReadInt32LittleEndian(r.AsSpan(0xad)))
            throw new InvalidDataException($"Item {item.Id} fields disagree with source record");
    }
    internal static void Spell(OriginalSpell s,byte[] r)
    {
        int I32(int o)=>BinaryPrimitives.ReadInt32LittleEndian(r.AsSpan(o));
        if(s.Name!=Name(r,0x19c,132) || s.Cost!=I32(0x14a) || s.Level!=I32(0x157) ||
            s.RealmCode!=I32(0x234) || s.RangeCode!=I32(0x230) || s.TargetCode!=I32(0x238) || s.UsabilityCode!=I32(0x23c) ||
            !s.SchoolFlags.SequenceEqual(new int[]{r[0x149],r[0x15b],r[0x220],r[0x221]}) ||
            s.DurationBaseRaw!=I32(0x14e) || s.DurationPerPowerRaw!=I32(0x145) || s.DamageFlagRaw!=I32(0x248) ||
            s.DamageConstant!=BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(0x152)) || s.DamageDice!=r[0x154] || s.DamageSides!=r[0x155])
            throw new InvalidDataException($"Spell {s.Id} fields disagree with source record");
    }
}
