using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWizardry.Core;

// Authored exploration-preview state, separate from campaign and native SAV formats.
public sealed record ExplorationPoint([property:JsonRequired] float X, [property:JsonRequired] float Y, [property:JsonRequired] float Z)
{
    [JsonIgnore] public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}
public sealed record ExplorationDoor([property:JsonRequired] long Id, [property:JsonRequired] int Frame,
    [property:JsonRequired] int Target, [property:JsonRequired] double Phase);
public sealed record ExplorationDoorLimit(long Id, int Frames, bool Restricted, int InitialFrame);
public sealed record ExplorationCheckpoint(int Schema, string ContentHash, ExplorationPoint Position,
    ExplorationPoint Rotation, float Speed, ImmutableArray<ExplorationDoor> Doors)
{
    public const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public void Validate(string contentHash, ImmutableArray<ExplorationDoorLimit> limits)
    {
        if(Schema != 1 || ContentHash is null || ContentHash.Length != 64 ||
           ContentHash.Any(c => !char.IsAsciiHexDigitLower(c)) || ContentHash != contentHash)
            throw new InvalidDataException("Exploration checkpoint schema/content mismatch");
        if(Position is null || Rotation is null || !Position.IsFinite || !Rotation.IsFinite ||
           Math.Abs(Rotation.X) > Math.PI || Math.Abs(Rotation.Y) > 1_000_000 || Rotation.Z != 0 ||
           !float.IsFinite(Speed) || Speed <= 0)
            throw new InvalidDataException("Invalid exploration camera");
        if(Doors.IsDefault || limits.IsDefault || Doors.Length > 1024 || Doors.Length != limits.Length)
            throw new InvalidDataException("Exploration door set differs from content");
        var byId = limits.ToDictionary(d => d.Id);
        var seen = new HashSet<long>();
        foreach(var door in Doors)
        {
            if(door is null || !seen.Add(door.Id) || !byId.TryGetValue(door.Id, out var limit) ||
               door.Frame < 0 || door.Frame >= limit.Frames || door.Target < 0 || door.Target >= limit.Frames ||
               !double.IsFinite(door.Phase) || door.Phase < 0 || door.Phase >= 1 ||
               (door.Frame == door.Target && door.Phase != 0) ||
               (door.Frame != door.Target && door.Target != 0 && door.Target != limit.Frames - 1) ||
               (limit.Restricted && (door.Frame != limit.InitialFrame || door.Target != limit.InitialFrame)))
                throw new InvalidDataException("Invalid or restricted exploration door pose");
        }
    }

    public string Serialize(string contentHash, ImmutableArray<ExplorationDoorLimit> limits)
    {
        Validate(contentHash, limits);
        return JsonSerializer.Serialize(this, Options);
    }

    public static ExplorationCheckpoint Parse(string json, string contentHash, ImmutableArray<ExplorationDoorLimit> limits)
    {
        if(System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("Exploration checkpoint exceeds size limit");
        var state = JsonSerializer.Deserialize<ExplorationCheckpoint>(json, Options)
            ?? throw new InvalidDataException("Empty exploration checkpoint");
        state.Validate(contentHash, limits);
        return state;
    }
}
