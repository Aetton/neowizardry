using System.Text.Json;

namespace NeoWizardry.Core;

public sealed record Provenance(string Kind, string File, string Sha256);
public sealed record ItemDefinition(int Id, string Name, int TypeCode, int IconIndex,
    int ProfessionMask, int RaceMask, int GenderMask, string RecordSha256,
    int UsageSkill, int RangeCode, int Initiative, int ToHit, int DamageConstant,
    int DamageDice, int DamageSides, int AttackModes, int WeightTenthsLb,
    bool Cursed, int BonusSwings)
{
    // A zero-dice record stores a percentage bonus (e.g. a bow), not flat damage.
    public string DamageDescription => DamageDice == 0 && DamageSides == 0
        ? $"Damage bonus {DamageConstant*10}% (no standalone damage roll)"
        : $"{DamageDice}d{DamageSides} + {DamageConstant}";
}

public sealed record ItemCatalog(int Schema, string Source, Provenance Provenance,
    int RecordSize, int Count, ItemDefinition[] Items)
{
    public static ItemCatalog Parse(string json)
    {
        var result = JsonSerializer.Deserialize<ItemCatalog>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
            ?? throw new InvalidDataException("Empty item catalog");
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Schema != 2 || RecordSize != 269 || Count <= 0 || Items is null || Count != Items.Length ||
            Source != "DATABASES/ITEMS.DBS" || Provenance is null || !Hash(Provenance.Sha256) ||
            Provenance.Kind is not ("archive" or "loose") || string.IsNullOrWhiteSpace(Provenance.File))
            throw new InvalidDataException("Unsupported or invalid item catalog");
        var ids = new HashSet<int>();
        foreach (var item in Items)
        {
            if (item is null || item.Id < 0 || !ids.Add(item.Id) || item.Name is null || !Hash(item.RecordSha256) ||
                item.TypeCode is < 0 or > 25 || item.IconIndex is < 0 or > 65535 ||
                item.ProfessionMask is < 0 or > 32767 || item.RaceMask is < 0 or > 65535 || item.GenderMask is < 0 or > 3 ||
                item.DamageConstant is < 0 or > 65535 || item.DamageDice is < 0 or > 255 || item.DamageSides is < 0 or > 255 ||
                item.UsageSkill is < 0 or > 255 || item.RangeCode is < 0 or > 255 || item.AttackModes is < 0 or > 65535 ||
                item.WeightTenthsLb is < 0 or > 65535 || item.Initiative is < -128 or > 127 || item.ToHit is < -128 or > 127 ||
                item.BonusSwings is < -128 or > 127)
                throw new InvalidDataException("Invalid item record");
        }
    }

    private static bool Hash(string? value) => value is { Length:64 } && value.All(Uri.IsHexDigit);
    public ItemDefinition Find(int id) => Items.FirstOrDefault(i => i.Id == id)
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

    public static GameState Equip(GameState state, string heroId, int itemId, ItemCatalog catalog)
    {
        Game.Validate(state); catalog.Validate();
        int index = Array.FindIndex(state.Party,h => h.Id==heroId);
        if (index<0) throw new InvalidOperationException("Unknown hero");
        var hero = state.Party[index];
        if (hero.Captive || hero.Hp==0) throw new InvalidOperationException("Hero cannot change equipment");
        var item = catalog.Find(itemId);
        var denial = Denial(hero,item);
        if (denial is not null) throw new InvalidOperationException(denial);
        if (hero.EquippedItemId == itemId) return state;
        // Work on copies: a rejected swap never consumes the replacement or removes the old weapon.
        var result = Inventory.Remove(state,itemId,1);
        if (hero.EquippedItemId.HasValue) result = Inventory.Add(result,hero.EquippedItemId.Value,1,catalog);
        var party = state.Party.ToArray();
        party[index] = hero with { EquippedItemId = itemId };
        return result with { Party = party };
    }

    public static GameState Unequip(GameState state, string heroId, ItemCatalog catalog)
    {
        Game.Validate(state); catalog.Validate();
        int index = Array.FindIndex(state.Party,h => h.Id==heroId);
        if (index<0) throw new InvalidOperationException("Unknown hero");
        var hero = state.Party[index];
        if (hero.Captive || hero.Hp==0) throw new InvalidOperationException("Hero cannot change equipment");
        if (!hero.EquippedItemId.HasValue) return state;
        var result = Inventory.Add(state,hero.EquippedItemId.Value,1,catalog);
        var party = state.Party.ToArray();
        party[index] = hero with { EquippedItemId=null };
        return result with { Party=party };
    }

    public static void ValidateLoadedEquipment(GameState state, ItemCatalog? catalog, string? expectedHash)
    {
        Game.Validate(state);
        if (!state.Party.Any(h => h.EquippedItemId.HasValue) && (state.Bag?.Length ?? 0)==0 && !state.Enemy.LootItemId.HasValue) return;
        if (catalog is null || catalog.Provenance.Sha256 != expectedHash)
            throw new InvalidDataException("Save requires the same imported item database.");
        catalog.Validate();
        foreach (var stack in state.Bag ?? []) catalog.Find(stack.ItemId);
        if (state.Enemy.LootItemId.HasValue) catalog.Find(state.Enemy.LootItemId.Value);
        foreach (var hero in state.Party.Where(h=>h.EquippedItemId.HasValue))
        {
            var denial = Denial(hero,catalog.Find(hero.EquippedItemId!.Value));
            if (denial is not null) throw new InvalidDataException(denial);
        }
    }
}
