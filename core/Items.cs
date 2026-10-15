using System.Text.Json;
using System.Collections.Immutable;

namespace NeoWizardry.Core;

public sealed record Provenance(string Kind, string File, string Sha256);
public sealed record ItemDefinition(int Id, string Name, int TypeCode, int IconIndex,
    int ProfessionMask, int RaceMask, int GenderMask, string RecordSha256,
    int UsageSkill, int RangeCode, int Initiative, int ToHit, int DamageConstant,
    int DamageDice, int DamageSides, int AttackModes, int WeightTenthsLb,
    bool Cursed, int BonusSwings, NativeItemFields? Instance = null)
{
    // A zero-dice record stores a percentage bonus (e.g. a bow), not flat damage.
    public string DamageDescription => DamageDice == 0 && DamageSides == 0
        ? $"Damage bonus {DamageConstant*10}% (no standalone damage roll)"
        : $"{DamageDice}d{DamageSides} {(DamageConstant<0?"-":"+")} {Math.Abs((long)DamageConstant)}";
}

public sealed record ItemCatalog(int Schema, string Source, Provenance Provenance,
    int RecordSize, int Count, ImmutableArray<ItemDefinition> Items)
{
    public static ItemCatalog Parse(string json)
    {
        var result = JsonSerializer.Deserialize<ItemCatalog>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
            ?? throw new InvalidDataException("Empty item catalog");
        result.Validate();
        return result;
    }

    public void Validate() => CompiledItemCatalog.Get(this);

    internal void ValidateSource()
    {
        if (Schema is not (2 or 3) || RecordSize != 269 || Count <= 0 || Items.IsDefault || Count != Items.Length ||
            Source != "DATABASES/ITEMS.DBS" || Provenance is null || !Hash(Provenance.Sha256) ||
            Provenance.Kind is not ("archive" or "loose") || string.IsNullOrWhiteSpace(Provenance.File))
            throw new InvalidDataException("Unsupported or invalid item catalog");
        var ids = new HashSet<int>();
        foreach (var item in Items)
        {
            if (item is null || item.Id < 0 || !ids.Add(item.Id) || item.Name is null || !Hash(item.RecordSha256) ||
                item.TypeCode is < 0 or > 25 || item.IconIndex is < 0 or > 65535 ||
                item.ProfessionMask is < 0 or > 32767 || item.RaceMask is < 0 or > 65535 || item.GenderMask is < 0 or > 3 ||
                item.DamageConstant is < -32768 or > 32767 || item.DamageDice is < 0 or > 255 || item.DamageSides is < 0 or > 255 ||
                item.UsageSkill is < 0 or > 255 || item.RangeCode is < 0 or > 255 || item.AttackModes is < 0 or > 65535 ||
                item.WeightTenthsLb is < 0 or > 65535 || item.Initiative is < -128 or > 127 || item.ToHit is < -128 or > 127)
                throw new InvalidDataException("Invalid item record");
            if(Schema==3 && item.Instance is null)throw new InvalidDataException("Missing native instance fields");
            SourceRecordValidation.Item(item);
        }
    }

    private static bool Hash(string? value) => value is { Length:64 } && value.All(Uri.IsHexDigit);
    public ItemDefinition Find(int id) => CompiledItemCatalog.Get(this).Definitions.GetValueOrDefault(id)
        ?? throw new InvalidOperationException($"Unknown item ID: {id}");
}

public static class Equipment
{
    private static readonly Dictionary<string,int> Races = new(StringComparer.Ordinal) {
        ["Human"]=1,["Elf"]=2,["Dwarf"]=4,["Gnome"]=8,["Hobbit"]=16,["Faerie"]=32,
        ["Lizardman"]=64,["Dracon"]=128,["Felpurr"]=256,["Rawulf"]=512,["Mook"]=1024,
        ["Trynnie"]=2048,["TRang"]=4096,["Umpani"]=8192,["Rapax"]=16384,["Android"]=32768 };
    private static readonly Dictionary<string,int> Professions = new(StringComparer.Ordinal) {
        ["Fighter"]=1,["Lord"]=2,["Valkyrie"]=4,["Ranger"]=8,["Samurai"]=16,["Ninja"]=32,
        ["Monk"]=64,["Rogue"]=128,["Gadgeteer"]=256,["Bard"]=512,["Priest"]=1024,
        ["Alchemist"]=2048,["Bishop"]=4096,["Psionic"]=8192,["Mage"]=16384 };

    public static string? Denial(Hero hero, ItemDefinition item)
    {
        if (item.TypeCode is not (0 or 1)) return "Only melee weapons are supported in this test slot.";
        if (item.DamageDice <= 0 || item.DamageSides <= 0) return "This weapon has no supported standalone damage roll.";
        if (!Races.TryGetValue(hero.Race,out int race) || (item.RaceMask & race)==0) return "Race restriction.";
        if (!Professions.TryGetValue(hero.Profession,out int profession) || (item.ProfessionMask & profession)==0) return "Profession restriction.";
        int gender = hero.Gender == "Male" ? 1 : hero.Gender == "Female" ? 2 : 0;
        if (gender==0 || (item.GenderMask & gender)==0) return "Gender restriction.";
        return null;
    }

    public const string WeaponSlot="weapon";
    public static ItemInstance? Instance(GameState state,string heroId)=>state.Items!.Items.SingleOrDefault(i=>i.Location==new ItemLocation(heroId,WeaponSlot));
    public static int? ItemId(GameState state,string heroId)=>Instance(state,heroId)?.DefinitionId;

    public static GameState Equip(GameState state,string heroId,int itemId,ItemCatalog catalog)
    {
        Inventory.Validate(state,catalog);
        if(Instance(state,heroId) is ItemInstance current && current.DefinitionId==itemId)return EquipInstance(state,heroId,current.Id,catalog);
        var item=Inventory.Bag(state).Where(i=>i.DefinitionId==itemId).OrderBy(i=>i.Id,StringComparer.Ordinal).FirstOrDefault()
            ??throw new InvalidOperationException("No spare copy in the party bag.");
        return EquipInstance(state,heroId,item.Id,catalog);
    }
    public static GameState EquipInstance(GameState state,string heroId,string instanceId,ItemCatalog catalog)
    {
        Inventory.Validate(state,catalog);var hero=state.Party.FirstOrDefault(h=>h.Id==heroId)??throw new InvalidOperationException("Unknown hero");
        if(hero.Captive || hero.Hp==0)throw new InvalidOperationException("Hero cannot change equipment");
        var item=state.Items!.Items.SingleOrDefault(i=>i.Id==instanceId)??throw new InvalidOperationException("Unknown item instance");
        var denial=Denial(hero,catalog.Find(item.DefinitionId));if(denial is not null)throw new InvalidOperationException(denial);
        if(Instance(state,heroId)?.Id==instanceId)return state;
        if(item.Location!=Inventory.BagLocation)throw new InvalidOperationException("Select a spare instance from the party bag.");
        var result=state;
        if(item.Quantity>1)
        {
            result=Inventory.Split(state,instanceId,1,catalog);
            item=result.Items!.Items.Single(i=>!state.Items.Items.Any(old=>old.Id==i.Id));
        }
        var previous=Instance(result,heroId);var rules=Inventory.Rules(catalog);
        var items=previous is null?ItemInventory.Move(result.Items!,item.Id,new(heroId,WeaponSlot),rules):
            ItemInventory.SwapLocations(result.Items!,item.Id,previous.Id,rules);
        return Inventory.Commit(result,items,result.NextItemSequence,catalog);
    }
    public static GameState Unequip(GameState state,string heroId,ItemCatalog catalog)
    {
        Inventory.Validate(state,catalog);var hero=state.Party.FirstOrDefault(h=>h.Id==heroId)??throw new InvalidOperationException("Unknown hero");
        if(hero.Captive || hero.Hp==0)throw new InvalidOperationException("Hero cannot change equipment");
        var item=Instance(state,heroId);if(item is null)return state;
        return Inventory.Commit(state,ItemInventory.Move(state.Items!,item.Id,Inventory.BagLocation,Inventory.Rules(catalog)),state.NextItemSequence,catalog);
    }
    public static void ValidateLoadedEquipment(GameState state,ItemCatalog? catalog,string? expectedHash)
    {
        Game.Validate(state);
        if(state.Items!.Items.Length==0 && !state.Enemy.LootItemId.HasValue)return;
        if(catalog is null || catalog.Provenance.Sha256!=expectedHash)
            throw new InvalidDataException("Save requires the same imported item database.");
        Inventory.Validate(state,catalog);
        if(state.Enemy.LootItemId.HasValue)catalog.Find(state.Enemy.LootItemId.Value);
        ValidateSelection(state,catalog);
    }
    internal static void ValidateSelection(GameState state,ItemCatalog catalog)
    {
        foreach(var hero in state.Party)
            if(ItemId(state,hero.Id) is int id && Denial(hero,catalog.Find(id)) is string denial)throw new InvalidDataException(denial);
    }
}
