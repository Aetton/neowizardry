using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWizardry.Core;

public enum WorldEncounterPhase { Ready, Active, Victory, Defeat }
public sealed record WorldExpeditionState(int Schema, string EncounterId, string ItemDatabaseHash,
    string MonsterDatabaseHash, int MonsterDefinitionId, GameState Game, ExplorationCheckpoint World,
    WorldEncounterPhase Phase = WorldEncounterPhase.Ready);

// One authored encounter; native spawn tables, rewards and continuous-world combat are not implied.
public static class WorldExpedition
{
    public const int MaximumBytes = 16 * 1024 * 1024;
    public const string EncounterId = "monastery-arrival-physical-v1";
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };

    public static WorldExpeditionState Create(int size, ItemCatalog items, MonsterCatalog monsters,
        int definitionId, ExplorationCheckpoint world) => new(1, EncounterId, items.Provenance.Sha256,
            monsters.DatabaseSha256, definitionId, MonsterCombat.Create(size,1,items,monsters,definitionId), world);

    public static void Validate(WorldExpeditionState state, string worldHash, ImmutableArray<ExplorationDoorLimit> limits,
        ItemCatalog items, MonsterCatalog monsters)
    {
        if(state is null || state.Schema!=1 || state.EncounterId!=EncounterId || state.Game is null || state.World is null ||
           !string.Equals(state.ItemDatabaseHash,items.Provenance.Sha256,StringComparison.OrdinalIgnoreCase) ||
           !string.Equals(state.MonsterDatabaseHash,monsters.DatabaseSha256,StringComparison.OrdinalIgnoreCase) || !Enum.IsDefined(state.Phase))
            throw new InvalidDataException("Expedition schema or content binding mismatch");
        state.World.Validate(worldHash,limits);
        Game.Validate(state.Game);MeleeCombat.Validate(state.Game,items);
        var field=state.Game.Melee?.Field;
        if(field?.Monsters is not {Count:1} || field.Opponents.Length!=0 ||
           !field.Monsters.TryGetValue(state.Game.Enemy.Id,out var actor) || actor.Record.ProfileVersion!=3 ||
           actor.Record.DefinitionId!=state.MonsterDefinitionId ||
           !string.Equals(actor.Record.DatabaseSha256,monsters.DatabaseSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expedition encounter actors differ from content");
        var definition=monsters.Monsters.SingleOrDefault(m=>m.Id==state.MonsterDefinitionId);
        if(definition is null || !MonsterCombat.Supported(definition) ||
           !string.Equals(actor.Record.RawRecordHex,definition.RawRecordHex,StringComparison.OrdinalIgnoreCase) ||
           !string.Equals(actor.Record.RecordSha256,definition.RecordSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Expedition monster record differs from current catalog");
        bool alive=state.Game.Party.Any(h=>h.Hp>0 && !h.Captive), enemy=state.Game.Enemy.Hp>0;
        if(state.Phase==WorldEncounterPhase.Ready && (state.Game.Round!=0 || !alive || !enemy || state.Game.RewardGranted) ||
           state.Phase==WorldEncounterPhase.Active && (!alive || !enemy || state.Game.RewardGranted) ||
           state.Phase==WorldEncounterPhase.Victory && (!alive || enemy || !state.Game.RewardGranted) ||
           state.Phase==WorldEncounterPhase.Defeat && (alive || !enemy || state.Game.RewardGranted))
            throw new InvalidDataException("Expedition phase contradicts combat state");
    }

    public static WorldExpeditionState Begin(WorldExpeditionState state)
    {
        if(state.Phase!=WorldEncounterPhase.Ready)throw new InvalidOperationException("This encounter cannot start again");
        return state with { Phase=WorldEncounterPhase.Active };
    }

    public static (WorldExpeditionState State, ImmutableArray<string> Events) Advance(WorldExpeditionState state,
        ItemCatalog items, IReadOnlyList<TacticalOrder>? orders=null)
    {
        if(state.Phase!=WorldEncounterPhase.Active)throw new InvalidOperationException("No active world encounter");
        var result=TacticalCombat.Advance(state.Game,items,orders);
        var phase=result.State.Enemy.Hp==0?WorldEncounterPhase.Victory:
            result.State.Party.Any(h=>h.Hp>0 && !h.Captive)?WorldEncounterPhase.Active:WorldEncounterPhase.Defeat;
        return (state with {Game=result.State,Phase=phase},result.Events);
    }

    public static string Save(WorldExpeditionState state, string hash, ImmutableArray<ExplorationDoorLimit> limits,
        ItemCatalog items, MonsterCatalog monsters)
    {
        Validate(state,hash,limits,items,monsters);
        var json=JsonSerializer.Serialize(state,Options);
        if(Encoding.UTF8.GetByteCount(json)>MaximumBytes)throw new InvalidDataException("Expedition save exceeds size limit");
        return json;
    }

    public static WorldExpeditionState Load(string json, string hash, ImmutableArray<ExplorationDoorLimit> limits,
        ItemCatalog items, MonsterCatalog monsters)
    {
        if(Encoding.UTF8.GetByteCount(json)>MaximumBytes)throw new InvalidDataException("Expedition save exceeds size limit");
        var state=JsonSerializer.Deserialize<WorldExpeditionState>(json,Options) ?? throw new InvalidDataException("Empty expedition save");
        Validate(state,hash,limits,items,monsters);
        return state;
    }
}
