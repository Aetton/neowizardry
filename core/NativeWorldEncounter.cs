using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWizardry.Core;

public sealed record NativeWorldMonster([property:JsonRequired] int LocationId,[property:JsonRequired] int DefinitionId,[property:JsonRequired] ExplorationPoint Position,[property:JsonRequired] int MaxHp,[property:JsonRequired] int Hp);
public sealed record NativeWorldEncounter([property:JsonRequired] int Schema,[property:JsonRequired] string PvlSha256,[property:JsonRequired] string StsSha256,[property:JsonRequired] int Level,[property:JsonRequired] int GroupId,[property:JsonRequired] int Disposition,
    [property:JsonRequired] ImmutableArray<NativeWorldMonster> Members)
{
    [JsonIgnore] public string Id=>$"sts:{StsSha256}:{Level}:{GroupId}";
    private static readonly JsonSerializerOptions Options=new() {PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    private static bool Hash(string s)=>s is {Length:64} && s.All(char.IsAsciiHexDigitLower);
    public static NativeWorldEncounter Parse(string json,string pvlHash,MonsterCatalog catalog)
    {
        var result=JsonSerializer.Deserialize<NativeWorldEncounter>(json,Options)??throw new InvalidDataException("Empty native encounter");
        result.Validate(catalog);
        if(result.PvlSha256!=pvlHash)throw new InvalidDataException("Native encounter belongs to another PVL");
        return result;
    }
    public void Validate(MonsterCatalog catalog)
    {
        if(Schema!=1 || !Hash(PvlSha256) || !Hash(StsSha256) || Level!=8 || GroupId<0 || Disposition is <0 or >1 ||
           Members.IsDefaultOrEmpty || Members.Length>8 || Members.Any(m=>m is null || m.LocationId<0 || m.Position is null || !m.Position.IsFinite || m.MaxHp<1 || m.Hp<1 || m.Hp>m.MaxHp) ||
           Members.Select(m=>m.LocationId).Distinct().Count()!=Members.Length)
            throw new InvalidDataException("Invalid native encounter placement");
        foreach(var member in Members)
        {
            var definition=catalog.Monsters.SingleOrDefault(m=>m.Id==member.DefinitionId)??throw new InvalidDataException("Unknown placed monster");
            var record=new MonsterRecordSnapshot(definition.Id,catalog.DatabaseSha256,definition.RecordSha256,definition.RawRecordHex,3);
            var compiled=MonsterCombat.Compile(record);ExpeditionRewards.RequireSupported(record);
            if(member.MaxHp<compiled.Health.Constant+compiled.Health.Dice || member.MaxHp>compiled.Health.Constant+(long)compiled.Health.Dice*compiled.Health.Sides)
                throw new InvalidDataException("Placed monster HP outside source definition bounds");
        }
    }
    public bool SameAs(NativeWorldEncounter? other)=>other is not null && JsonSerializer.Serialize(this,Options)==JsonSerializer.Serialize(other,Options);
}
