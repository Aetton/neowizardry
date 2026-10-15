namespace NeoWizardry.Core;

// A recovered local operation, deliberately separate from GameState/fixture crafting.
// Instance creation, skill advancement, inventory placement and time are not modeled.
public sealed record NativeIngredient(string InstanceId, int ItemId, int Mode, int Quantity, bool Identified);
public sealed record NativeCombination(int OutputId, int FirstId, int SecondId, int OutputMode, int SkillCode, int SkillThreshold);
public sealed record NativeCombinationPlan(int OutputId, int FirstConsumed, int SecondConsumed,
    int FirstRemaining, int SecondRemaining, int OutputQuantity, string Destination);

public static class NativeCombinations
{
    public static NativeCombinationPlan Plan(NativeCombination recipe, NativeIngredient first,
        NativeIngredient second, int actorSkillValue)
    {
        if(recipe.OutputId<0 || recipe.FirstId<0 || recipe.SecondId<0 ||
            recipe.SkillCode is <0 or >255 || recipe.SkillThreshold is <0 or >255 || actorSkillValue<0)
            throw new InvalidOperationException("Invalid combination input.");
        Validate(first);Validate(second);
        if(first.InstanceId==second.InstanceId)throw new InvalidOperationException("Select two separate item instances.");
        if(!((first.ItemId==recipe.FirstId && second.ItemId==recipe.SecondId) ||
             (first.ItemId==recipe.SecondId && second.ItemId==recipe.FirstId)))
            throw new InvalidOperationException("Ingredients do not match.");
        if(!first.Identified || !second.Identified)throw new InvalidOperationException("Both ingredients must be identified.");
        // The executable sign-extends this field. Only known skill indices and -1
        // are supported; a mod's unknown code is not an invented skill.
        if(recipe.SkillCode!=255 && recipe.SkillCode>40)throw new InvalidOperationException("Unsupported skill code.");
        if(recipe.SkillCode!=255 && actorSkillValue<recipe.SkillThreshold)
            throw new InvalidOperationException("Insufficient skill.");
        if(recipe.OutputMode is <0 or >4)throw new InvalidOperationException("Unsupported output item mode.");
        int batch=first.Mode==1 && second.Mode==1?Math.Min(first.Quantity,second.Quantity):1;
        int firstUsed=first.Mode==1?batch:1,secondUsed=second.Mode==1?batch:1;
        string destination=first.Mode==1 && second.Mode==1
            ?(second.Quantity<=first.Quantity?"second":"first")
            :(first.Mode==1?"second":"first");
        return new(recipe.OutputId,firstUsed,secondUsed,first.Quantity-firstUsed,second.Quantity-secondUsed,
            recipe.OutputMode==1?batch:1,destination);
    }

    private static void Validate(NativeIngredient item)
    {
        if(string.IsNullOrWhiteSpace(item.InstanceId) || item.ItemId<0 || item.Mode is <0 or >4 ||
            item.Quantity is <1 or >255 || (item.Mode!=1 && item.Quantity!=1))
            throw new InvalidOperationException("Invalid item instance.");
    }
}
