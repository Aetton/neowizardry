using System.Collections.Immutable;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public enum ProjectileActorKind { Hero, Enemy }
public sealed record ProjectileActor(ProjectileActorKind Kind,string Id);
public sealed record ProjectileActionState(ProjectileActor Actor,string ItemDatabaseHash,string Ruleset,ProjectileSeriesState Series);
public sealed record ProjectileActionCreated(GameState State,string ActionId);
public sealed record ProjectileExecutionResult(GameState State,ProjectileStrikeResult Strike,int AppliedDamage);

// Authoritative transaction boundary for resolved projectile inputs. Callers submit callbacks
// to their current GameState and serialize commits (or use compare-and-swap); never commit a
// returned state over a newer one. Geometry, effective stats and source eligibility stay external.
public static class ProjectileExecution
{
    public const string RulesetIdentity="ordinary-projectile-v1";
    private static ImmutableDictionary<string,ProjectileActionState> Actions(GameState state)=>state.ProjectileActions??ImmutableDictionary<string,ProjectileActionState>.Empty;

    public static ProjectileActionCreated Begin(GameState state,ProjectileActor actor,string sourceId,ProjectileCost cost,
        int allocatedSwings,ItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(actor);Validate(state,catalog);
        if(!CanAct(state,actor))throw new InvalidOperationException("Projectile actor cannot start");
        if(Actions(state).Values.Any(a=>a.Actor==actor && a.Series.StopReason is ProjectileStopReason.None or ProjectileStopReason.Depleted))
            throw new InvalidOperationException("Actor already has an active projectile action");
        string id=$"projectile-{state.NextProjectileSequence}";
        var series=ProjectileSeries.Create(id,state.Items!,sourceId,cost,allocatedSwings,Inventory.Rules(catalog));
        var action=new ProjectileActionState(actor,catalog.Provenance.Sha256,RulesetIdentity,series);
        var next=state with {ProjectileActions=Actions(state).Add(id,action),NextProjectileSequence=checked(state.NextProjectileSequence+1)};
        Validate(next,catalog);return new(next,id);
    }

    public static GameState Launch(GameState state,string actionId,ProjectileAimInput input,int angleDraw,int? radiusDraw,ItemCatalog catalog)
    {
        Validate(state,catalog);var action=Find(state,actionId);
        if(!CanAct(state,action.Actor))throw new InvalidOperationException("Projectile actor cannot launch");
        if(Actions(state).Values.Any(a=>a.Series.Pending?.Source.Id==action.Series.SourceId))
            throw new InvalidOperationException("Projectile source is already reserved");
        var launch=OriginalProjectileAttack.Launch(action.Series,state.Items!,input,angleDraw,radiusDraw,Inventory.Rules(catalog));
        return Put(state,action with {Series=launch.State},catalog);
    }

    public static ProjectileExecutionResult Complete(GameState state,string actionId,int ordinal,ProjectileActor? actualTarget,
        ProjectileStrikeInput input,int? penetrationDraw,IReadOnlyList<int> damageDraws,bool deliveryCanContinue,ItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(input);Validate(state,catalog);var action=Find(state,actionId);
        // Validate target identity even on intercepted/no-damage contacts. Misses have no actor target.
        if(input.Contact==ProjectileContactKind.Miss ? actualTarget is not null : actualTarget is null || !Exists(state,actualTarget))
            throw new InvalidOperationException("Supply the actual collided actor, or no actor for a miss");
        var completed=OriginalProjectileAttack.Complete(action.Series,state.Items!,actionId,ordinal,input,penetrationDraw,
            damageDraws,CanAct(state,action.Actor),deliveryCanContinue,Inventory.Rules(catalog));
        int damage=actualTarget is null?0:Math.Min(Hp(state,actualTarget),completed.Strike.Damage?.Damage??0);
        var next=state with {Items=completed.Inventory};
        if(actualTarget is not null && damage>0)
            next=actualTarget.Kind==ProjectileActorKind.Enemy?next with {Enemy=next.Enemy with {Hp=next.Enemy.Hp-damage}}:
                next with {Party=next.Party.Select(h=>h.Id==actualTarget.Id?h with {Hp=h.Hp-damage}:h).ToImmutableArray()};
        // Self-hit or death caused by this completion must also stop continuation.
        var series=completed.State;
        if(series.StopReason==ProjectileStopReason.None && !CanAct(next,action.Actor))
            series=series with {StopReason=ProjectileStopReason.ActorUnavailable};
        next=Put(next,action with {Series=series},catalog);
        return new(next,completed.Strike,damage);
    }

    public static GameState Stop(GameState state,string actionId,ProjectileStopReason reason,ItemCatalog catalog)
    {
        Validate(state,catalog);var action=Find(state,actionId);
        return Put(state,action with {Series=ProjectileSeries.Stop(action.Series,state.Items!,reason,Inventory.Rules(catalog))},catalog);
    }
    public static GameState ReplaceAmmunition(GameState state,string actionId,ItemDefinition weapon,string? preferredId,
        IReadOnlyList<string> fallbackIds,ItemCatalog catalog)
    {
        Validate(state,catalog);var action=Find(state,actionId);
        if(!CanAct(state,action.Actor))throw new InvalidOperationException("Projectile actor cannot reload");
        var series=OriginalProjectileReload.ReplaceAmmunition(action.Series,state.Items!,weapon,preferredId,fallbackIds,catalog,Inventory.Rules(catalog));
        return Put(state,action with {Series=series},catalog);
    }
    public static GameState RefillFromReserves(GameState state,string actionId,IReadOnlyList<string> fuelIds,ItemCatalog catalog)
    {
        Validate(state,catalog);var action=Find(state,actionId);
        if(!CanAct(state,action.Actor))throw new InvalidOperationException("Projectile actor cannot refill");
        var refill=OriginalProjectileReload.RefillFromReserves(action.Series,state.Items!,fuelIds,catalog,Inventory.Rules(catalog));
        return Put(state with {Items=refill.Inventory},action with {Series=refill.State},catalog);
    }
    // Terminal entries can be discarded without making their IDs reusable. Depleted entries
    // retain their remaining budget until the caller chooses to stop or reload them.
    public static GameState Forget(GameState state,string actionId,ItemCatalog catalog)
    {
        Validate(state,catalog);var action=Find(state,actionId);
        if(action.Series.Pending is not null || action.Series.StopReason==ProjectileStopReason.None)
            throw new InvalidOperationException("Cannot forget an active projectile action");
        var next=state with {ProjectileActions=Actions(state).Remove(actionId)};Validate(next,catalog);return next;
    }

    public static void Validate(GameState state,ItemCatalog catalog)
    {
        Game.Validate(state);var rules=Inventory.Rules(catalog);ItemInventory.Validate(state.Items!,rules);
        foreach(var action in Actions(state).Values)
        {
            if(!action.ItemDatabaseHash.Equals(catalog.Provenance.Sha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Projectile action requires the same item database");
            ProjectileSeries.Validate(action.Series,state.Items!,rules);
        }
    }
    // Game.Validate calls this without needing the private source catalog. In-flight source
    // equality is an optimistic reservation checked by every canonical inventory commit/save.
    public static void ValidateStructure(GameState state)
    {
        if(state.NextProjectileSequence<1)throw new InvalidDataException("Invalid projectile identity sequence");
        var sources=new HashSet<string>(StringComparer.Ordinal);
        var actors=new HashSet<ProjectileActor>();
        foreach(var (id,action) in Actions(state))
        {
            if(action is null || action.Actor is null || !Exists(state,action.Actor) || action.Series is null ||
                action.Ruleset!=RulesetIdentity || id!=action.Series.Id || !id.StartsWith("projectile-",StringComparison.Ordinal) ||
                !long.TryParse(id.AsSpan(11),out long sequence) || sequence<1 || sequence>=state.NextProjectileSequence ||
                id!=$"projectile-{sequence}" || action.ItemDatabaseHash is not {Length:64} || !action.ItemDatabaseHash.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid projectile action");
            ProjectileSeries.ValidateStructure(action.Series,state.Items!);
            if(action.Series.StopReason is ProjectileStopReason.None or ProjectileStopReason.Depleted && !actors.Add(action.Actor))
                throw new InvalidDataException("Actor has multiple unfinished projectile actions");
            if(action.Series.Pending is { } pending)
            {
                if(!sources.Add(pending.Source.Id))throw new InvalidDataException("Projectile source reserved twice");
                if(pending.Aim is null)throw new InvalidDataException("Missing saved projectile aim");
                OriginalProjectileAim.ValidateSaved(pending.Aim);
            }
        }
    }
    private static ProjectileActionState Find(GameState state,string id)=>Actions(state).GetValueOrDefault(id)
        ??throw new InvalidOperationException("Unknown or retired projectile action");
    private static GameState Put(GameState state,ProjectileActionState action,ItemCatalog catalog)
    {
        var next=state with {ProjectileActions=Actions(state).SetItem(action.Series.Id,action)};
        Validate(next,catalog);return next;
    }
    private static bool Exists(GameState state,ProjectileActor actor)=>actor.Kind switch {
        ProjectileActorKind.Hero=>state.Party.Any(h=>h.Id==actor.Id),ProjectileActorKind.Enemy=>state.Enemy.Id==actor.Id,_=>false};
    private static int Hp(GameState state,ProjectileActor actor)=>actor.Kind==ProjectileActorKind.Enemy?state.Enemy.Hp:state.Party.Single(h=>h.Id==actor.Id).Hp;
    private static bool CanAct(GameState state,ProjectileActor actor)=>Exists(state,actor) && Hp(state,actor)>0 &&
        (actor.Kind!=ProjectileActorKind.Hero || !state.Party.Single(h=>h.Id==actor.Id).Captive);
}
