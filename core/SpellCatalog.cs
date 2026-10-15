using System.Security.Cryptography;
using System.Text.Json;

namespace NeoWizardry.Core;

// Original metadata is a reference catalog, not a learned spellbook or executable combat rule.
public sealed record OriginalSpell(int Id, string Name, string Description, int Cost, int Level,
    int RealmCode, int RangeCode, int TargetCode, int UsabilityCode, int[] SchoolFlags,
    int DurationBaseRaw, int DurationPerPowerRaw, int DamageFlagRaw,
    int DamageConstant, int DamageDice, int DamageSides, string RecordSha256, string RawRecordHex)
{
    public string Realm => Code(RealmCode,["Fire","Water","Air","Earth","Mental","Divine"]);
    public string Target => Code(TargetCode,["Caster","One ally","Party","One enemy","Enemy group","Cone","Radius","All enemies","Point","Adventuring","Adventuring traps"]);
    public string Usability => Code(UsabilityCode,["Any time","Combat","Non-combat","NPC interaction","Traps"]);
    private static string Code(int value, string[] names) => value>=0 && value<names.Length ? names[value] : $"Unknown code {value}";
}

public sealed record SpellCatalog(int Schema, string Source, Provenance Provenance,
    string DescriptionSource, Provenance DescriptionProvenance, int RecordSize, int Count, OriginalSpell[] Spells)
{
    public static SpellCatalog Parse(string json)
    {
        var catalog=JsonSerializer.Deserialize<SpellCatalog>(json,new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower })
            ?? throw new InvalidDataException("Empty spell catalog");
        catalog.Validate();return catalog;
    }
    public void Validate()
    {
        if(Schema!=1 || Source!="DATABASES/SPELLTABLES.DBS" || DescriptionSource!="DATABASES/SPELLDESC.DBS" ||
            RecordSize!=704 || Count<=0 || Spells is null || Count!=Spells.Length)
            throw new InvalidDataException("Unsupported spell catalog");
        foreach(var p in new[]{Provenance,DescriptionProvenance})
            if(p is null || p.Kind is not ("archive" or "loose") || string.IsNullOrWhiteSpace(p.File) || !Hash(p.Sha256))
                throw new InvalidDataException("Invalid spell provenance");
        var ids=new HashSet<int>();
        foreach(var s in Spells)
        {
            if(s is null || s.Id<0 || !ids.Add(s.Id) || s.Name is null || s.Description is null ||
                s.SchoolFlags is not { Length:4 } || s.SchoolFlags.Any(f=>f is <0 or >255) ||
                s.DamageConstant is <0 or >65535 || s.DamageDice is <0 or >255 || s.DamageSides is <0 or >255 ||
                !Hash(s.RecordSha256) || s.RawRecordHex is not { Length:1408 } || !s.RawRecordHex.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid spell record");
            var digest=Convert.ToHexString(SHA256.HashData(Convert.FromHexString(s.RawRecordHex)));
            if(!digest.Equals(s.RecordSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Spell record hash mismatch");
        }
    }
    private static bool Hash(string? s)=>s is {Length:64} && s.All(Uri.IsHexDigit);
}
