using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoWizardry.Core;

public enum WorldEncounterPhase { Ready, Active, Victory, Defeat }
public sealed record WorldExpeditionState(int Schema, string EncounterId, string ItemDatabaseHash,
    string MonsterDatabaseHash, int MonsterDefinitionId, GameState Game, ExplorationCheckpoint World,
    WorldEncounterPhase Phase = WorldEncounterPhase.Ready, ExpeditionRewardReceipt? Rewards=null,NativeWorldEncounter? Native=null);

// One persisted encounter: legacy authored placement or an explicitly bound STS group.
public static class WorldExpedition
{
    public const int MaximumBytes = 16 * 1024 * 1024;
    public const string EncounterId = "monastery-arrival-physical-v1";
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };

    public static WorldExpeditionState Create(int size, ItemCatalog items, MonsterCatalog monsters,
        int definitionId, ExplorationCheckpoint world)
    {
        var game=MonsterCombat.Create(size,1,items,monsters,definitionId);
        ExpeditionRewards.RequireSupported(game.Melee!.Field!.Monsters![game.Enemy.Id].Record);
        return new(2,EncounterId,items.Provenance.Sha256,monsters.DatabaseSha256,definitionId,game,world,
            Rewards:new(game.Party.ToImmutableDictionary(h=>h.Id,h=>h.Xp)));
    }

    public static WorldExpeditionState CreateNative(int size,ItemCatalog items,MonsterCatalog monsters,
        NativeWorldEncounter encounter,ExplorationCheckpoint world)
    {
        encounter.Validate(monsters);
        var game=MonsterCombat.CreateRoster(size,items,monsters,encounter.Members.Select(m=>m.DefinitionId).ToArray());
        var field=game.Melee!.Field!;
        var native=field.Monsters!;var brains=field.Brains!;
        var replacements=new Dictionary<string,Enemy>();
        for(int i=0;i<encounter.Members.Length;i++)
        {
            string id=$"monster-{i+1}";var member=encounter.Members[i];
            var old=TacticalCombat.Enemies(game).Single(e=>e.Id==id);
            replacements[id]=old with {Hp=member.Hp};
            native=native.SetItem(id,native[id] with {MaxHp=member.MaxHp});
            brains=brains.SetItem(id,brains[id] with {MaxHp=member.MaxHp});
        }
        game=game with {Enemy=replacements[game.Enemy.Id],Melee=game.Melee with {Field=field with {
            Monsters=native,Brains=brains,Opponents=field.Opponents.Select(o=>o with {Actor=replacements[o.Actor.Id]}).ToImmutableArray()}}};
        MeleeCombat.Validate(game,items);
        return new(3,encounter.Id,items.Provenance.Sha256,monsters.DatabaseSha256,encounter.Members[0].DefinitionId,game,world,
            Rewards:new(game.Party.ToImmutableDictionary(h=>h.Id,h=>h.Xp)),Native:encounter);
    }

    public static void Validate(WorldExpeditionState state, string worldHash, ImmutableArray<ExplorationDoorLimit> limits,
        ItemCatalog items, MonsterCatalog monsters,NativeWorldEncounter? expectedNative=null)
    {
        if(state is null || state.Schema is not (1 or 2 or 3) || expectedNative is not null && state.Schema!=3 || (state.Schema==3?state.Native is null || !state.Native.SameAs(expectedNative) || state.EncounterId!=state.Native.Id:state.EncounterId!=EncounterId || state.Native is not null) || state.Game is null || state.World is null ||
           !string.Equals(state.ItemDatabaseHash,items.Provenance.Sha256,StringComparison.OrdinalIgnoreCase) ||
           !string.Equals(state.MonsterDatabaseHash,monsters.DatabaseSha256,StringComparison.OrdinalIgnoreCase) || !Enum.IsDefined(state.Phase))
            throw new InvalidDataException("Expedition schema or content binding mismatch");
        state.World.Validate(worldHash,limits);
        Game.Validate(state.Game);MeleeCombat.Validate(state.Game,items);
        var field=state.Game.Melee?.Field;
        int count=state.Schema==3?state.Native!.Members.Length:1;
        if(field?.Monsters is null || field.Monsters.Count!=count || field.Opponents.Length!=count-1)
            throw new InvalidDataException("Expedition encounter actors differ from content");
        state.Native?.Validate(monsters);
        for(int i=0;i<count;i++)
        {
            string id=$"monster-{i+1}";int definitionId=state.Schema==3?state.Native!.Members[i].DefinitionId:state.MonsterDefinitionId;
            if(!field.Monsters.TryGetValue(id,out var placed) || placed.Record.ProfileVersion!=3 || placed.Record.DefinitionId!=definitionId ||
               !string.Equals(placed.Record.DatabaseSha256,monsters.DatabaseSha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Expedition actor identity differs from placement");
            var definition=monsters.Monsters.SingleOrDefault(m=>m.Id==definitionId);
            if(definition is null || !MonsterCombat.Supported(definition) ||
               !string.Equals(placed.Record.RawRecordHex,definition.RawRecordHex,StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(placed.Record.RecordSha256,definition.RecordSha256,StringComparison.OrdinalIgnoreCase) ||
               state.Schema==3 && (placed.MaxHp!=state.Native!.Members[i].MaxHp ||
                   state.Phase==WorldEncounterPhase.Ready && TacticalCombat.Enemies(state.Game).Single(e=>e.Id==id).Hp!=state.Native.Members[i].Hp))
                throw new InvalidDataException("Expedition monster record differs from current catalog");
        }
        if(state.MonsterDefinitionId!=field.Monsters[state.Game.Enemy.Id].Record.DefinitionId)
            throw new InvalidDataException("Primary expedition definition mismatch");
        var actor=field.Monsters[state.Game.Enemy.Id];
        ExpeditionRewards.Validate(state,actor.Record);
        bool alive=state.Game.Party.Any(h=>h.Hp>0 && !h.Captive), enemy=TacticalCombat.Enemies(state.Game).Any(e=>e.Hp>0);
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
        ExpeditionRewards.Validate(state,state.Game.Melee!.Field!.Monsters![state.Game.Enemy.Id].Record);
        var result=TacticalCombat.Advance(state.Game,items,orders);
        var phase=!TacticalCombat.Enemies(result.State).Any(e=>e.Hp>0)?WorldEncounterPhase.Victory:
            result.State.Party.Any(h=>h.Hp>0 && !h.Captive)?WorldEncounterPhase.Active:WorldEncounterPhase.Defeat;
        var next=state with {Game=result.State,Phase=phase};var events=result.Events;
        if(phase==WorldEncounterPhase.Victory && state.Schema>=2)
        {
            if(state.Rewards is not {Granted:false})throw new InvalidOperationException("Expedition reward already granted or missing");
            var record=result.State.Melee!.Field!.Monsters![result.State.Enemy.Id].Record;
            var rewarded=ExpeditionRewards.Grant(result.State,record);
            next=next with {Game=rewarded,Rewards=state.Rewards with {Granted=true}};
            int count=rewarded.Party.Count(ExpeditionRewards.Eligible),total=ExpeditionRewards.Experience(rewarded);
            events=events.Where(e=>!e.StartsWith("Victory:",StringComparison.Ordinal)).ToImmutableArray()
                .Add($"Native reward: {total} XP / {count} eligible heroes = {total/count} each; no item or gold drops in this record. Saved once.");
        }
        return (next,events);
    }

    public static string Save(WorldExpeditionState state, string hash, ImmutableArray<ExplorationDoorLimit> limits,
        ItemCatalog items, MonsterCatalog monsters,NativeWorldEncounter? expectedNative=null)
    {
        Validate(state,hash,limits,items,monsters,expectedNative);
        var json=JsonSerializer.Serialize(state,Options);
        if(Encoding.UTF8.GetByteCount(json)>MaximumBytes)throw new InvalidDataException("Expedition save exceeds size limit");
        return json;
    }

    public static WorldExpeditionState Load(string json, string hash, ImmutableArray<ExplorationDoorLimit> limits,
        ItemCatalog items, MonsterCatalog monsters,NativeWorldEncounter? expectedNative=null)
    {
        if(Encoding.UTF8.GetByteCount(json)>MaximumBytes)throw new InvalidDataException("Expedition save exceeds size limit");
        var state=JsonSerializer.Deserialize<WorldExpeditionState>(json,Options) ?? throw new InvalidDataException("Empty expedition save");
        Validate(state,hash,limits,items,monsters,expectedNative);
        return state;
    }
}
