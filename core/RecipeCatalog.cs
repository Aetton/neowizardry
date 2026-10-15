using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NeoWizardry.Core;

public sealed record OriginalRecipe(int OutputId, string OutputName, int[] IngredientIds,
    string?[] IngredientNames, int SkillCode, int SkillThreshold, string UnresolvedC1C8Hex,
    string[] Issues, string RecordSha256, string RawRecordHex)
{
    public string Skill => SkillCode switch { 255=>"No skill comparison",23=>"Engineering",26=>"Alchemy",0=>"Sword",_=>$"Skill code {SkillCode}" };
}

public sealed record RecipeCatalog(int Schema, string Mode, string Source, Provenance Provenance,
    int RecordSize, int ItemCount, int Count, Dictionary<string,int> InactivePairs, OriginalRecipe[] Recipes)
{
    public static RecipeCatalog Parse(string json)
    {
        var catalog=JsonSerializer.Deserialize<RecipeCatalog>(json,new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower })
            ?? throw new InvalidDataException("Empty combination catalog");
        catalog.Validate();return catalog;
    }
    public void Validate()
    {
        if(Schema!=1 || Mode!="reference_only" || Source!="DATABASES/ITEMS.DBS" || RecordSize!=269 ||
            ItemCount<0 || Count<0 || Recipes is null || Count!=Recipes.Length || InactivePairs is null ||
            InactivePairs.Any(p=>p.Key is not ("-1" or "-2") || p.Value<0) ||
            (long)Count+InactivePairs.Values.Sum(x=>(long)x)!=ItemCount ||
            Provenance is null || Provenance.Kind is not ("archive" or "loose") ||
            string.IsNullOrWhiteSpace(Provenance.File) || !Hash(Provenance.Sha256))
            throw new InvalidDataException("Unsupported combination catalog");
        var ids=new HashSet<int>();
        foreach(var r in Recipes)
        {
            if(r is null || r.OutputId<0 || r.OutputId>=ItemCount || !ids.Add(r.OutputId) ||
                r.OutputName is null || r.IngredientIds is not {Length:2} || r.IngredientNames is not {Length:2} ||
                r.SkillCode is <0 or >255 || r.SkillThreshold is <0 or >255 ||
                r.Issues is null || r.Issues.Any(x=>x is null) || !Hash(r.RecordSha256) ||
                r.RawRecordHex is not {Length:538} || !r.RawRecordHex.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid combination record");
            var raw=Convert.FromHexString(r.RawRecordHex);
            if(!Convert.ToHexString(SHA256.HashData(raw)).Equals(r.RecordSha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Combination record hash mismatch");
            var name=new UnicodeEncoding(false,false,true).GetString(raw,0,60).Split('\0')[0];
            if(name!=r.OutputName || BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0xb9))!=r.IngredientIds[0] ||
                BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0xbd))!=r.IngredientIds[1] ||
                raw[0xc9]!=r.SkillCode || raw[0xca]!=r.SkillThreshold ||
                !Convert.ToHexString(raw.AsSpan(0xc1,8)).Equals(r.UnresolvedC1C8Hex,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Combination fields disagree with raw record");
            if(r.IngredientIds.SequenceEqual(new[]{-1,-1}) || r.IngredientIds.SequenceEqual(new[]{-2,-2}))
                throw new InvalidDataException("Inactive pair included as combination");
            var expected=new List<string>();
            if(name.Length==0)expected.Add("unnamed_output");
            bool outside=r.IngredientIds.Any(x=>x<0 || x>=ItemCount);
            if(outside)expected.Add("ingredient_out_of_range");
            else if(r.IngredientNames.Any(string.IsNullOrEmpty))expected.Add("unnamed_ingredient");
            for(int i=0;i<2;i++)
                if((r.IngredientIds[i]<0 || r.IngredientIds[i]>=ItemCount) && r.IngredientNames[i] is not null)
                    throw new InvalidDataException("Invalid ingredient name reference");
            if(!r.Issues.SequenceEqual(expected))throw new InvalidDataException("Incorrect combination anomaly flags");
        }
        if(!Recipes.Select(r=>r.OutputId).SequenceEqual(Recipes.Select(r=>r.OutputId).Order()))
            throw new InvalidDataException("Combination records must preserve database order");
    }
    private static bool Hash(string? text)=>text is {Length:64} && text.All(Uri.IsHexDigit);
}
