using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public sealed record PreparedPhysicalWeapon(PhysicalDamageDice Damage,int ToHit,int LauncherDamagePercent,
    PhysicalStrengthScaling StrengthScaling,bool ModernWeapon);

// Original-data boundary: translate legacy weapon/icon conventions into explicit rule inputs.
// The caller must resolve equipped instances, availability, actor eligibility and consumption.
public static class OriginalWeaponAdapter
{
    public static bool UsesAmmunition(ItemDefinition weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return weapon.IconIndex is 11 or 12 or 13 or 37 or 131;
    }

    // The caller selects an available compatible source. Spell-use weapons, actor
    // gates and automatic replacement/reloading are separate execution paths.
    public static ProjectileCost ProjectileExpenditure(ItemDefinition weapon,ItemDefinition? ammunition=null)
    {
        _=PreparePrimary(weapon,ammunition);
        if((weapon.AttackModes&0x110)==0)throw new InvalidOperationException("Select a shooting or throwing weapon");
        var source=ammunition??weapon;
        var fields=source.Instance??throw new InvalidOperationException("Native metadata required");
        fields.Validate(source.RecordSha256);
        var raw=Convert.FromHexString(fields.RawRecordHex);
        if(ammunition is null && raw[0x63]!=0)
            throw new InvalidOperationException("Spell-use weapon requires its separate execution path");
        if(ammunition is null && weapon.TypeCode==2 && (fields.Flags41&0x80)!=0)
            return new(source.Id,ProjectileCostKind.None);
        if(ammunition is null && weapon.TypeCode==2 && fields.Mode!=1)
            throw new InvalidOperationException("Nonstack throwing continuation requires separate reconstruction");
        return fields.Mode switch {
            0=>new(source.Id,ProjectileCostKind.None),
            1=>new(source.Id,ProjectileCostKind.Quantity),
            >=2 and <=4=>new(source.Id,ProjectileCostKind.Resource,!RetainEmpty(raw)),
            _=>throw new InvalidOperationException("Unsupported projectile source mode")};
    }
    // 0x520750: capability predicate, not an inferred 'rechargeable' flag.
    internal static bool RetainEmpty(byte[] raw)=>
        (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x4e))&0x1ff)!=0 ||
        raw[0x62]!=0 || raw[0x8c]!=0 || raw.AsSpan(0x6c,9).ContainsAnyExcept((byte)0);

    public static PreparedPhysicalWeapon PreparePrimary(ItemDefinition weapon,ItemDefinition? ammunition=null)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if(weapon.TypeCode is <0 or >3)throw new ArgumentException("Select a weapon definition");
        bool launcher=UsesAmmunition(weapon);
        if(launcher && ammunition is null)throw new InvalidOperationException("Resolve ammunition before preparing an attack");
        if(!launcher && ammunition is not null)throw new ArgumentException("This weapon does not use separate ammunition");
        if(ammunition is not null && ammunition.TypeCode!=4 && !(weapon.IconIndex==131 && ammunition.TypeCode==2))
            throw new ArgumentException("Unsupported ammunition hand category");
        if(ammunition is not null && !OriginalAmmunitionAdapter.Compatible(weapon,ammunition))
            throw new ArgumentException("Ammunition is incompatible with this launcher stage");
        var source=ammunition??weapon;
        var strength=(weapon.AttackModes & ~0x190)!=0?PhysicalStrengthScaling.Full:
            weapon.IconIndex is 104 or 110 or 114 or 131 or 144?PhysicalStrengthScaling.None:PhysicalStrengthScaling.Half;
        return new(new(source.DamageConstant,source.DamageDice,source.DamageSides),
            checked(weapon.ToHit+(ammunition?.ToHit??0)),launcher?checked(weapon.DamageConstant*10):0,
            strength,weapon.UsageSkill==7);
    }
}
