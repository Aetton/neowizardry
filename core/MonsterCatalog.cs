using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NeoWizardry.Core;

// Native content reference, deliberately separate from executable monster actors.
public sealed record OriginalMonster(int Id,string Label,ImmutableArray<string> TextSlots,string RecordSha256,string RawRecordHex);
public sealed record MonsterCatalog(int Schema,string Source,Provenance Provenance,int RecordSize,int Count,
    string DatabaseSha256,ImmutableArray<OriginalMonster> Monsters)
{
    public static MonsterCatalog Parse(string json)
    {
        var catalog=JsonSerializer.Deserialize<MonsterCatalog>(json,new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower})
            ??throw new InvalidDataException("Empty monster catalog");
        catalog.Validate();return catalog;
    }
    private static bool Hash(string? text)=>text is {Length:64} && text.All(Uri.IsHexDigit);
    public void Validate()
    {
        if(Schema!=1 || Source!="DATABASES/MONSTERS.DBS" || RecordSize!=663 || Count<1 || Monsters.IsDefault || Count!=Monsters.Length ||
            !Hash(DatabaseSha256) || Provenance is null || Provenance.Kind is not ("archive" or "loose") ||
            string.IsNullOrWhiteSpace(Provenance.File) || !Hash(Provenance.Sha256))throw new InvalidDataException("Invalid monster catalog");
        using var database=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,Count);database.AppendData(header);
        var utf16=new UnicodeEncoding(false,false,true);
        for(int index=0;index<Count;index++)
        {
            var row=Monsters[index];
            if(row is null || row.Id!=index || row.TextSlots.IsDefault || row.TextSlots.Length!=4 || row.TextSlots.Any(t=>t is null) ||
                row.Label!=row.TextSlots[0] || !Hash(row.RecordSha256) || row.RawRecordHex is not {Length:1326} || !row.RawRecordHex.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid monster record");
            var raw=Convert.FromHexString(row.RawRecordHex);database.AppendData(raw);
            if(!Convert.ToHexString(SHA256.HashData(raw)).Equals(row.RecordSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Monster record hash mismatch");
            for(int slot=0;slot<4;slot++)
            {
                var text=utf16.GetString(raw,slot*48,48);int end=text.IndexOf('\0');
                if(end<0 || text[..end]!=row.TextSlots[slot])throw new InvalidDataException("Monster text differs from native evidence");
            }
        }
        if(!Convert.ToHexString(database.GetHashAndReset()).Equals(DatabaseSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Monster database identity mismatch");
    }
}
