namespace NeoWizardry.Core;

public sealed record SkillValue(string Id, int Value);
public sealed record RecipeDefinition(string Id, string Name, ItemStack[] Ingredients,
    ItemStack Result, string? Skill = null, int MinimumSkill = 0);
public sealed record CraftOrder(string HeroId, string RecipeId);

// Public synthetic fixtures only. Original alchemy/gadget/quest combinations need separate evidence.
public static class Crafting
{
    public const string FixtureSource = "synthetic-test-catalog";
    public static readonly IReadOnlyList<RecipeDefinition> Recipes = Array.AsReadOnly(new[] {
        new RecipeDefinition("test-tonic", "Test tonic", [new(78,2),new(79,1)], new(80,1)),
        new RecipeDefinition("test-staff", "Tempered test staff", [new(77,1),new(80,1)], new(81,1), "test-craft",20)
    });

    public static bool UsesFixture(ItemCatalog catalog) => catalog.Provenance.File == FixtureSource;
    public static void ValidateRecipe(RecipeDefinition recipe, ItemCatalog catalog)
    {
        if(string.IsNullOrWhiteSpace(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Name) ||
            recipe.Ingredients is not { Length: >0 } || recipe.Result is null || recipe.Result.Count<=0 ||
            recipe.MinimumSkill<0 || (recipe.Skill is null && recipe.MinimumSkill!=0))
            throw new InvalidDataException("Invalid recipe.");
        var ids=new HashSet<int>();
        foreach(var ingredient in recipe.Ingredients)
        {
            if(ingredient is null || ingredient.Count<=0 || !ids.Add(ingredient.ItemId))
                throw new InvalidDataException("Invalid recipe ingredients.");
            catalog.Find(ingredient.ItemId);
        }
        catalog.Find(recipe.Result.ItemId);
    }

    public static string? Denial(GameState state, CraftOrder order, ItemCatalog catalog)
    {
        if(!UsesFixture(catalog))return "Original recipes have not been imported for this catalog.";
        var recipe=Recipes.FirstOrDefault(r=>r.Id==order.RecipeId);
        if(recipe is null)return "Unknown recipe.";
        if(!(state.KnownRecipes ?? []).Contains(recipe.Id))return "Recipe is not learned.";
        var hero=state.Party.FirstOrDefault(h=>h.Id==order.HeroId);
        if(hero is null)return "Unknown crafter.";
        if(hero.Hp==0 || hero.Captive)return "Crafter cannot act.";
        ValidateRecipe(recipe,catalog);
        if(recipe.Skill is not null && ((hero.Skills ?? []).FirstOrDefault(s=>s.Id==recipe.Skill)?.Value ?? -1)<recipe.MinimumSkill)
            return $"Requires {recipe.Skill} {recipe.MinimumSkill}.";
        foreach(var ingredient in recipe.Ingredients)
            if(Inventory.Count(state,ingredient.ItemId)<ingredient.Count)
                return $"Needs {catalog.Find(ingredient.ItemId).Name} ×{ingredient.Count} in the bag.";
        long remaining=Inventory.Count(state,recipe.Result.ItemId)-
            (recipe.Ingredients.FirstOrDefault(i=>i.ItemId==recipe.Result.ItemId)?.Count ?? 0);
        if(remaining+recipe.Result.Count>int.MaxValue)return "Result stack is full.";
        return null;
    }

    public static GameState Craft(GameState state, CraftOrder order, ItemCatalog catalog)
    {
        Game.Validate(state);catalog.Validate();Equipment.ValidateLoadedEquipment(state,catalog,catalog.Provenance.Sha256);
        var denial=Denial(state,order,catalog);
        if(denial is not null)throw new InvalidOperationException(denial);
        var recipe=Recipes.Single(r=>r.Id==order.RecipeId);
        var result=state;
        foreach(var ingredient in recipe.Ingredients)result=Inventory.Remove(result,ingredient.ItemId,ingredient.Count);
        return Inventory.Add(result,recipe.Result.ItemId,recipe.Result.Count,catalog);
    }
}
