using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public sealed record CharacterDefenseEquipment(ItemDefinition? Head=null,ItemDefinition? Torso=null,ItemDefinition? Legs=null,
    ItemDefinition? Hands=null,ItemDefinition? Feet=null,ItemDefinition? Cloak=null,ItemDefinition? Accessory1=null,
    ItemDefinition? Accessory2=null,ItemDefinition? MainHand=null,ItemDefinition? OffHand=null);

// Boundary for an explicitly resolved active equipment set. No bag scan, inactive
// weapon sets or native slot indices enter the canonical defense model.
public static class OriginalDefenseAdapter
{
    public static PreparedCharacterArmor Prepare(CharacterDefenseEquipment selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        int shield=0,other=0;
        foreach(var item in new[]{selected.Cloak,selected.Accessory1,selected.Accessory2,selected.MainHand,selected.OffHand})
            if(item is not null)
            {
                int armor=Armor(item);
                if(item.TypeCode==5)shield=checked(shield+armor);else other=checked(other+armor);
            }
        return new(new(Armor(selected.Head),Armor(selected.Torso),Armor(selected.Legs),Armor(selected.Hands),Armor(selected.Feet)),shield,other);
    }
    private static int Armor(ItemDefinition? item)
    {
        if(item is null)return 0;
        var fields=item.Instance??throw new InvalidOperationException("Native armor metadata required");
        fields.Validate(item.RecordSha256);
        return unchecked((sbyte)Convert.FromHexString(fields.RawRecordHex)[0x62]);
    }
}
