using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using NeoWizardry.Core;

// Synthetic evidence must describe the same values as its exported fields.
static class SourceFixture
{
    internal static ItemDefinition Item(ItemDefinition d)
    {
        var r=Convert.FromHexString(d.Instance!.RawRecordHex);
        var f=d.Instance;r[0x66]=(byte)f.Mode;r[0x41]=(byte)f.Flags41;r[0x6b]=(byte)f.MaxStack;
        BinaryPrimitives.WriteInt16LittleEndian(r.AsSpan(0x67),(short)f.AmountConstant);r[0x69]=(byte)f.AmountDice;r[0x6a]=(byte)f.AmountSides;
        Array.Clear(r,0,60);Encoding.Unicode.GetBytes(d.Name).CopyTo(r,0);
        void U16(int o,int v)=>BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(o),(ushort)v);
        r[0x3e]=(byte)d.TypeCode;U16(0x3f,d.IconIndex);U16(0x76,d.ProfessionMask);U16(0x78,d.RaceMask);r[0x7c]=(byte)d.GenderMask;
        r[0x46]=(byte)d.UsageSkill;r[0x47]=(byte)d.RangeCode;r[0x48]=unchecked((byte)d.Initiative);r[0x49]=unchecked((byte)d.ToHit);
        BinaryPrimitives.WriteInt16LittleEndian(r.AsSpan(0x4a),(short)d.DamageConstant);r[0x4c]=(byte)d.DamageDice;r[0x4d]=(byte)d.DamageSides;
        U16(0x4e,d.AttackModes);U16(0x8a,d.WeightTenthsLb);r[0x8c]=(byte)(d.Cursed?1:0);BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0xad),d.BonusSwings);
        return d with {RecordSha256=Convert.ToHexString(SHA256.HashData(r)),Instance=d.Instance with {RawRecordHex=Convert.ToHexString(r)}};
    }
    internal static OriginalSpell Spell(OriginalSpell s)
    {
        var r=Convert.FromHexString(s.RawRecordHex);
        Array.Clear(r,0x19c,132);Encoding.Unicode.GetBytes(s.Name).CopyTo(r,0x19c);
        void I32(int o,int v)=>BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(o),v);
        I32(0x14a,s.Cost);I32(0x157,s.Level);I32(0x234,s.RealmCode);I32(0x230,s.RangeCode);I32(0x238,s.TargetCode);I32(0x23c,s.UsabilityCode);
        int[] offsets=[0x149,0x15b,0x220,0x221];for(int i=0;i<4;i++)r[offsets[i]]=(byte)s.SchoolFlags[i];
        I32(0x14e,s.DurationBaseRaw);I32(0x145,s.DurationPerPowerRaw);I32(0x248,s.DamageFlagRaw);
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x152),(ushort)s.DamageConstant);r[0x154]=(byte)s.DamageDice;r[0x155]=(byte)s.DamageSides;
        return s with {RecordSha256=Convert.ToHexString(SHA256.HashData(r)),RawRecordHex=Convert.ToHexString(r)};
    }
}
