namespace NeoWizardry.Core;

public sealed record OriginalRefillPreparation(int Capacity,bool RemoveEmptyFuel);

// Original-data boundary only: icon/definition identifiers never become rules in
// the canonical inventory or projectile state machine. Actor eligibility is external.
public static class OriginalAmmunitionAdapter
{
    // 0x51C8F0. Compatibility is a definition predicate, not availability or equipment.
    public static bool Compatible(ItemDefinition weapon,ItemDefinition source)
    {
        ArgumentNullException.ThrowIfNull(weapon);ArgumentNullException.ThrowIfNull(source);
        return weapon.IconIndex switch {
            11 or 37=>source.IconIndex==22,
            12=>source.IconIndex==83,
            13=>source.IconIndex==23,
            104 or 110 or 114=>(source.TypeCode>3 || weapon.RangeCode==source.RangeCode) &&
                FuelIcon(weapon.IconIndex)==source.IconIndex,
            131=>OmnigunCompatible(weapon.Id,source),
            _=>false};
    }

    // The executable selects Omnigun stages by definition ID, not by caster/actor
    // level. Unsupported IDs do not silently inherit the highest-stage ammo set.
    private static bool OmnigunCompatible(int id,ItemDefinition source)
    {
        if(id is <599 or >610)return false;
        int icon=source.IconIndex;
        if(icon is 23 or 113)return true;
        if(id>=600 && icon==112)return true;
        if(id>=602 && (icon is 21 or 126 || source.TypeCode==2 && icon==0))return true;
        return id>=606 && icon is 22 or 83;
    }

    // 0x616E6C. Refill support is broader than 0x51C8F0's dispatch table: icons
    // 144/145 are present here, but are not accepted by that compatibility function.
    public static int? FuelIcon(int weaponIcon)=>weaponIcon switch {
        114=>113,110 or 104=>132,144=>143,145=>146,_=>null};

    public static OriginalRefillPreparation PrepareRefill(ItemDefinition weapon,ItemDefinition fuel)
    {
        ArgumentNullException.ThrowIfNull(weapon);ArgumentNullException.ThrowIfNull(fuel);
        var fields=weapon.Instance??throw new InvalidOperationException("Native weapon metadata required");
        fields.Validate(weapon.RecordSha256);
        var source=fuel.Instance??throw new InvalidOperationException("Native fuel metadata required");
        var compiled=source.Compile(fuel.RecordSha256);
        if(fields.Mode!=4 || FuelIcon(weapon.IconIndex) is not int icon || fuel.IconIndex!=icon || source.Mode is <1 or >4)
            throw new InvalidOperationException("Unsupported weapon/fuel refill pair");
        int capacity=checked(fields.AmountConstant+fields.AmountDice*fields.AmountSides);
        if(capacity is <1 or >255)throw new InvalidOperationException("Original shot capacity must fit in a positive byte");
        return new(capacity,!compiled.RetainEmpty);
    }
}
