using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public sealed record PreparedPhysicalWeapon(PhysicalDamageDice Damage,int ToHit,int LauncherDamagePercent,
    PhysicalStrengthScaling StrengthScaling,bool ModernWeapon);

// Original-data boundary: translate legacy weapon/icon conventions into explicit rule inputs.
// The caller must resolve equipped instances, ammo compatibility, availability and consumption.
public static class OriginalWeaponAdapter
{
    public static bool UsesAmmunition(ItemDefinition weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        return weapon.IconIndex is 11 or 12 or 13 or 37 or 131;
    }

    public static PreparedPhysicalWeapon PreparePrimary(ItemDefinition weapon,ItemDefinition? ammunition=null)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if(weapon.TypeCode is <0 or >3)throw new ArgumentException("Select a weapon definition");
        bool launcher=UsesAmmunition(weapon);
        if(launcher && ammunition is null)throw new InvalidOperationException("Resolve ammunition before preparing an attack");
        if(!launcher && ammunition is not null)throw new ArgumentException("This weapon does not use separate ammunition");
        if(ammunition is not null && ammunition.TypeCode!=4 && !(weapon.IconIndex==131 && ammunition.TypeCode==2))
            throw new ArgumentException("Unsupported ammunition hand category");
        var source=ammunition??weapon;
        var strength=(weapon.AttackModes & ~0x190)!=0?PhysicalStrengthScaling.Full:
            weapon.IconIndex is 104 or 110 or 114 or 131 or 144?PhysicalStrengthScaling.None:PhysicalStrengthScaling.Half;
        return new(new(source.DamageConstant,source.DamageDice,source.DamageSides),
            checked(weapon.ToHit+(ammunition?.ToHit??0)),launcher?checked(weapon.DamageConstant*10):0,
            strength,weapon.UsageSkill==7);
    }
}
