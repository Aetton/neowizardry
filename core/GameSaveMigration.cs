using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NeoWizardry.Core;

// Only the load boundary understands obsolete aggregate ownership fields.
public static class GameSaveMigration
{
    public static GameState Load(string json,ItemCatalog? catalog=null)
    {
        var root=JsonNode.Parse(json)?.AsObject()??throw new InvalidDataException("Empty save");
        int schema=root["Schema"]?.GetValue<int>()??throw new InvalidDataException("Missing save version");
        if(schema is 1 or 2 or 3 && root.ContainsKey("Melee"))throw new InvalidDataException("Melee encounter requires save schema 4");
        if(schema is 1 or 2 && (root.ContainsKey("ProjectileActions") || root.ContainsKey("NextProjectileSequence")))
            throw new InvalidDataException("Projectile state requires save schema 3");
        if(schema is 2 or 3 or 4)
        {
            if(root["Melee"] is JsonObject melee && melee["Field"] is not null)throw new InvalidDataException("Tactical field requires save schema 5");
        }
        if(schema<=5 && root["Melee"] is JsonObject oldMelee && oldMelee["Field"] is JsonObject oldField && oldField["Brains"] is not null)
            throw new InvalidDataException("Enemy brains require save schema 6");
        if(schema<=6 && root["Melee"] is JsonObject priorMelee && priorMelee["Field"] is JsonObject priorField && priorField["Monsters"] is not null)
            throw new InvalidDataException("Monster actors require save schema 7");
        if(schema is 2 or 3)
        {
            if(root.ContainsKey("Bag") || root["Party"] is JsonArray oldParty && oldParty.Any(h=>h?.AsObject().ContainsKey("EquippedItemId")==true))
                throw new InvalidDataException("Conflicting legacy inventory fields in instance save");
            var previous=JsonSerializer.Deserialize<GameState>(json)??throw new InvalidDataException("Empty save");
            var upgraded=previous with {Schema=Game.SaveSchema};
            if(upgraded.ProjectileActions is {Count:>0} && catalog is null)throw new InvalidDataException("Saved projectiles require their catalog");
            Game.Validate(upgraded);
            if(catalog is not null){Equipment.ValidateLoadedEquipment(upgraded,catalog,catalog.Provenance.Sha256);ProjectileExecution.Validate(upgraded,catalog);}
            return upgraded;
        }
        if(schema is 4 or 5 or 6 || schema==Game.SaveSchema)
        {
            if(root.ContainsKey("Bag") || root["Party"] is JsonArray party && party.Any(h=>h?.AsObject().ContainsKey("EquippedItemId")==true))
                throw new InvalidDataException("Conflicting legacy inventory fields in instance save");
            var current=(JsonSerializer.Deserialize<GameState>(json)??throw new InvalidDataException("Empty save")) with {Schema=Game.SaveSchema};
            Game.Validate(current);
            if((current.ProjectileActions is {Count:>0} || current.Melee is not null) && catalog is null)
                throw new InvalidDataException("Saved combat actions require their item catalog");
            if(catalog is not null)
            {
                Equipment.ValidateLoadedEquipment(current,catalog,catalog.Provenance.Sha256);
                ProjectileExecution.Validate(current,catalog);
                if(current.Melee is not null)MeleeCombat.Validate(current,catalog);
            }
            return current;
        }
        if(schema!=1 || root.ContainsKey("Items") || root.ContainsKey("NextItemSequence"))throw new InvalidDataException("Unsupported or mixed save version");
        var legacy=JsonSerializer.Deserialize<GameState>(json)??throw new InvalidDataException("Empty save");
        var state=legacy with {Schema=Game.SaveSchema,Items=ItemInventory.Create(Inventory.DefaultCapacity),NextItemSequence=1};
        Game.Validate(state);
        var bag=root["Bag"]?.Deserialize<ItemStack[]>()??[];var definitions=new HashSet<int>();
        foreach(var stack in bag)
            if(stack is null || stack.ItemId<0 || stack.Count<1 || !definitions.Add(stack.ItemId))throw new InvalidDataException("Invalid legacy bag");
        var equipped=new List<(string HeroId,int ItemId)>();
        foreach(var member in root["Party"]!.AsArray())
            if(member!["EquippedItemId"] is JsonNode id)
            {
                int itemId=id.GetValue<int>();if(itemId<0)throw new InvalidDataException("Invalid legacy equipment");
                equipped.Add((member["Id"]!.GetValue<string>(),itemId));
            }
        if(bag.Length==0 && equipped.Count==0)
        {
            Equipment.ValidateLoadedEquipment(state,catalog,catalog?.Provenance.Sha256);return state;
        }
        if(catalog is null)throw new InvalidDataException("Legacy item migration requires the original item catalog");
        catalog.Validate();long sequence=1;uint random=state.RandomState;var items=new List<ItemInstance>();
        foreach(var stack in bag.OrderBy(i=>i.ItemId))
            items.AddRange(Inventory.CreateItems(stack.ItemId,stack.Count,Inventory.BagLocation,catalog,ref sequence,ref random,state.Items!.Capacity-items.Count,migrating:true));
        foreach(var worn in equipped)
            items.AddRange(Inventory.CreateItems(worn.ItemId,1,new(worn.HeroId,Equipment.WeaponSlot),catalog,ref sequence,ref random,state.Items!.Capacity-items.Count,migrating:true));
        state=Inventory.Commit(state,state.Items! with {Items=items.ToImmutableArray()},sequence,catalog);
        Equipment.ValidateLoadedEquipment(state,catalog,catalog.Provenance.Sha256);return state;
    }
}
