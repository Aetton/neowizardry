using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeoWizardry.Core;

static class SnapshotChecks
{
    public static int Run(SpellCatalog spells,RecipeCatalog recipes)
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name)
        {
            bool rejected=false;
            try{action();}catch(NotSupportedException){rejected=true;}
            Check(rejected,name);
        }
        void Invalid(Action action,string name)
        {
            bool rejected=false;
            try{action();}catch(InvalidDataException){rejected=true;}
            Check(rejected,name);
        }
        var seed=Game.Create(1);
        Invalid(()=>Game.Validate(seed with {Party=default}),"Default party is rejected as invalid data");
        Invalid(()=>ItemInventory.ValidateStructure(new(1,5,default)),"Default item collection is rejected as invalid data");
        Invalid(()=>(spells with {Spells=default}).Validate(),"Default spell collection is rejected as invalid data");
        Invalid(()=>(spells with {Spells=[spells.Spells[0] with {SchoolFlags=default}]}).Validate(),"Default school flags are rejected as invalid data");
        Invalid(()=>(recipes with {Recipes=default}).Validate(),"Default recipe collection is rejected as invalid data");
        Invalid(()=>(recipes with {Recipes=[recipes.Recipes[0] with {IngredientIds=default}]}).Validate(),"Default ingredient IDs are rejected as invalid data");
        Invalid(()=>(recipes with {Recipes=[recipes.Recipes[0] with {IngredientNames=default}]}).Validate(),"Default ingredient names are rejected as invalid data");
        Invalid(()=>(recipes with {Recipes=[recipes.Recipes[0] with {Issues=default}]}).Validate(),"Default recipe issues are rejected as invalid data");
        var mana=new[]{new ManaPool("Fire",12,12)};
        var learned=new[]{"test-spark"};
        var skills=new[]{new SkillValue("test-craft",25)};
        var effects=new[]{new StatusEffect("ward",2,3)};
        var enemyEffects=new[]{new StatusEffect("burn",1,3)};
        var party=new[]{seed.Party[0] with {Mana=mana.ToImmutableList(),Spellbook=learned.ToImmutableList(),
            Skills=skills.ToImmutableList(),Effects=effects.ToImmutableList()}};
        var instances=new[]{new ItemInstance("item",1,2,true,null,false,new("party"))};
        var known=new[]{"test-tonic"};
        var original=seed with {Party=party.ToImmutableArray(),Enemy=seed.Enemy with {Effects=enemyEffects.ToImmutableList()},
            Items=new(1,5,instances.ToImmutableArray()),KnownRecipes=known.ToImmutableList()};
        string before=Game.Save(original);
        // Retaining mutable construction buffers cannot change a published snapshot.
        party[0]=party[0] with {Power=123};mana[0]=mana[0] with {Current=0};learned[0]="unknown";
        skills[0]=skills[0] with {Value=0};effects[0]=effects[0] with {Magnitude=6};
        enemyEffects[0]=enemyEffects[0] with {Magnitude=6};instances[0]=instances[0] with {Quantity=999};known[0]="unknown";
        Check(Game.Save(original)==before,"Construction aliases cannot mutate nested game snapshot");
        var copy=original with {Round=1};
        Reject(()=>((IList<Hero>)copy.Party)[0]=party[0],"Party cannot mutate shared snapshot through IList");
        Reject(()=>((IList<ItemInstance>)copy.Items!.Items)[0]=instances[0],"Inventory cannot mutate shared snapshot through IList");
        Reject(()=>((IList<ManaPool>)copy.Party[0].Mana!)[0]=mana[0],"Mana cannot mutate shared snapshot through IList");
        Reject(()=>((IList<string>)copy.Party[0].Spellbook!)[0]="unknown","Spellbook cannot mutate shared snapshot through IList");
        Reject(()=>((IList<SkillValue>)copy.Party[0].Skills!)[0]=skills[0],"Skills cannot mutate shared snapshot through IList");
        Reject(()=>((IList<StatusEffect>)copy.Party[0].Effects!)[0]=effects[0],"Hero effects cannot mutate shared snapshot through IList");
        Reject(()=>((IList<StatusEffect>)copy.Enemy.Effects!)[0]=enemyEffects[0],"Enemy effects cannot mutate shared snapshot through IList");
        Reject(()=>((IList<string>)copy.KnownRecipes!)[0]="unknown","Known recipes cannot mutate shared snapshot through IList");
        var edited=copy with {Party=copy.Party.SetItem(0,copy.Party[0] with {
            Mana=copy.Party[0].Mana!.SetItem(0,new("Fire",0,12)),
            Skills=copy.Party[0].Skills!.SetItem(0,new("test-craft",50)),Effects=[]}),
            Items=copy.Items! with {Items=copy.Items.Items.SetItem(0,copy.Items.Items[0] with {Quantity=1})},
            Enemy=copy.Enemy with {Effects=[]},KnownRecipes=[]};
        Game.Validate(edited);
        Check(Game.Save(original)==before && copy.Party[0].Mana![0].Current==12,"Nested persistent edits leave both prior snapshots intact");
        var loaded=Game.Load(before);
        Check(Game.Save(loaded)==before,"Immutable collections retain current-schema JSON representation");
        Check(JsonNode.Parse(before)!["Party"] is JsonArray && JsonNode.Parse(before)!["Items"]!["Items"] is JsonArray,
            "Game and inventory serialize as arrays, not collection implementation objects");
        Check(loaded.Party[0].Effects!.Count==1 && loaded.Enemy.Effects!.Count==1 && loaded.KnownRecipes!.Count==1,
            "Nested optional lists roundtrip without losing effects and recipes");
        // Reference catalogs need the same guarantee before any validation/index cache can be safe.
        var spellCopy=spells with {};
        string spellBefore=JsonSerializer.Serialize(spells);
        Reject(()=>((IList<OriginalSpell>)spellCopy.Spells)[0]=spellCopy.Spells[0] with {Level=9999},"Spell catalog entries are immutable");
        Reject(()=>((IList<int>)spellCopy.Spells[0].SchoolFlags)[0]=99,"Spell school flags are immutable");
        var changedSpell=spellCopy with {Spells=spellCopy.Spells.SetItem(0,spellCopy.Spells[0] with {
            SchoolFlags=spellCopy.Spells[0].SchoolFlags.SetItem(0,99)})};
        Check(JsonSerializer.Serialize(spells)==spellBefore && changedSpell.Spells[0].SchoolFlags[0]==99,
            "Changing copied spell flags cannot invalidate an older verified catalog");
        var recipeCopy=recipes with {};
        string recipeBefore=JsonSerializer.Serialize(recipes);
        var recipe=recipeCopy.Recipes[0];
        Reject(()=>((IList<OriginalRecipe>)recipeCopy.Recipes)[0]=recipe with {SkillThreshold=99},"Recipe catalog entries are immutable");
        Reject(()=>((IList<int>)recipe.IngredientIds)[0]=99,"Recipe ingredient IDs are immutable");
        Reject(()=>((IList<string?>)recipe.IngredientNames)[0]="changed","Recipe ingredient names are immutable");
        Reject(()=>((ICollection<string>)recipe.Issues).Add("changed"),"Recipe issues are immutable");
        Reject(()=>((IDictionary<string,int>)recipeCopy.InactivePairs)["-1"]=999,"Inactive recipe counts are immutable");
        var changedRecipe=recipeCopy with {Recipes=recipeCopy.Recipes.SetItem(0,recipe with {IngredientIds=recipe.IngredientIds.SetItem(0,99)}),
            InactivePairs=recipeCopy.InactivePairs.SetItem("-1",999)};
        Check(JsonSerializer.Serialize(recipes)==recipeBefore && changedRecipe.InactivePairs["-1"]==999,
            "Recipe edits do not mutate a previous catalog");
        var options=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower};
        Check(SpellCatalog.Parse(JsonSerializer.Serialize(spells,options)).Spells[0].SchoolFlags.SequenceEqual(spells.Spells[0].SchoolFlags),
            "Immutable spell catalog roundtrip retains source-backed flags");
        Check(RecipeCatalog.Parse(JsonSerializer.Serialize(recipes,options)).InactivePairs["-1"]==recipes.InactivePairs["-1"],
            "Immutable recipe dictionary retains exported JSON object shape");
        return checks;
    }
}
