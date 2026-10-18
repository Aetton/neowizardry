using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWizardry.Core;

public sealed record NativeWorldItem([property:JsonRequired] int LocationId,[property:JsonRequired] int DefinitionId,[property:JsonRequired] ExplorationPoint Position,
    [property:JsonRequired] byte StackCount,[property:JsonRequired] byte Charges,[property:JsonRequired] bool Identified,[property:JsonRequired] bool Hidden,[property:JsonRequired] string MeshFile,[property:JsonRequired] string MeshSha256)
{
    public ItemInstance Instance(ItemCatalog catalog,string id)=>OriginalItemAdapter.Convert(catalog.Find(DefinitionId),
        new(DefinitionId,StackCount,Charges,(byte)(Identified?1:0),0,0,0,0,0),id,Inventory.BagLocation).Item;
}
public sealed record NativeWorldItems([property:JsonRequired] int Schema,[property:JsonRequired] string PvlSha256,[property:JsonRequired] string StsSha256,[property:JsonRequired] int Level,[property:JsonRequired] ImmutableArray<NativeWorldItem> Members)
{
    private static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static NativeWorldItems Parse(string json,NativeWorldEncounter encounter,ItemCatalog catalog)
    {
        var result=JsonSerializer.Deserialize<NativeWorldItems>(json,Options)??throw new InvalidDataException("Empty native item manifest");
        result.Validate(encounter,catalog);return result;
    }
    public bool SameAs(NativeWorldItems? other)=>other is not null && JsonSerializer.Serialize(this,Options)==JsonSerializer.Serialize(other,Options);
    public void Validate(NativeWorldEncounter? encounter,ItemCatalog catalog)
    {
        if(Schema!=1 || encounter is null || Level!=encounter.Level || PvlSha256!=encounter.PvlSha256 || StsSha256!=encounter.StsSha256 ||
           Members.IsDefault || Members.Length>100000 || Members.Any(m=>m is null) || Members.Select(m=>m.LocationId).Distinct().Count()!=Members.Length)
            throw new InvalidDataException("Native item source binding mismatch");
        foreach(var member in Members)
        {
            if(member.LocationId<0 || member.Position is null || !double.IsFinite(member.Position.X) || !double.IsFinite(member.Position.Y) || !double.IsFinite(member.Position.Z) ||
               member.MeshFile!=$"item-{member.DefinitionId}.nwworlditem.glb" || member.MeshSha256 is not {Length:64} || !member.MeshSha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid native item placement/model");
            _=member.Instance(catalog,"validation");
        }
    }
}

public static class WorldItemPickup
{
    public static WorldExpeditionState Attach(WorldExpeditionState state,NativeWorldItems? source,ItemCatalog catalog)
    {
        if(source is null)return state;
        source.Validate(state.Native,catalog);
        return state with {WorldItems=source,CollectedItems=ImmutableHashSet<int>.Empty};
    }
    public static void Validate(WorldExpeditionState state,NativeWorldItems? expected,ItemCatalog catalog)
    {
        if(state.WorldItems is null)
        {if(expected is not null || state.CollectedItems is not null)throw new InvalidDataException("Missing world item source");return;}
        if(!state.WorldItems.SameAs(expected) || state.CollectedItems is null)throw new InvalidDataException("World item save binding mismatch");
        state.WorldItems.Validate(state.Native,catalog);
        if(state.CollectedItems.Any(id=>!state.WorldItems.Members.Any(m=>m.LocationId==id && !m.Hidden)))throw new InvalidDataException("Invalid collected placement identity");
    }
    public static WorldExpeditionState Take(WorldExpeditionState state,int locationId,ItemCatalog catalog)
    {
        Validate(state,state.WorldItems,catalog);
        if(state.Phase is WorldEncounterPhase.Active or WorldEncounterPhase.Defeat)throw new InvalidOperationException("Cannot collect items now");
        var source=state.WorldItems?.Members.SingleOrDefault(m=>m.LocationId==locationId)??throw new InvalidOperationException("Unknown placed item");
        if(source.Hidden || state.CollectedItems!.Contains(locationId))throw new InvalidOperationException("Item is hidden or already collected");
        long sequence=state.Game.NextItemSequence;
        var instance=source.Instance(catalog,Inventory.FreshId(ref sequence));
        var inventory=ItemInventory.Add(state.Game.Items!,instance,Inventory.Rules(catalog));
        var game=Inventory.Commit(state.Game,inventory,sequence,catalog);
        return state with {Game=game,CollectedItems=state.CollectedItems.Add(locationId)};
    }
}
