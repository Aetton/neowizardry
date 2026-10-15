using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public sealed record AimedProjectileLaunch(ProjectileSeriesState State,ProjectileAimResult Aim);
public sealed record ProjectileAttackCompletion(ProjectileSeriesState State,ProjectileStrikeResult Strike);

// Compose ordinary post-delivery resolution with exactly one resource completion.
// Caller owns collision, actual target/defenses, source/weapon preparation, actor
// eligibility, RNG, HP application and committing the current immutable state once.
public static class OriginalProjectileAttack
{
    // Resolve aim without spending the projectile. Persist the whole returned flight
    // (including aim) if the caller saves during delivery. Collision remains external.
    public static AimedProjectileLaunch Launch(ProjectileSeriesState state,ProjectileAimInput input,int angleDraw,
        int? radiusDraw,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.AttackScore==9999)throw new InvalidOperationException("Native no-spread launch uses its separate delivery path");
        var pending=ProjectileSeries.Launch(state,rules);
        var aim=OriginalProjectileAim.Resolve(input,angleDraw,radiusDraw);
        return new(pending,aim);
    }

    public static ProjectileAttackCompletion Complete(ProjectileSeriesState state,string seriesId,int ordinal,
        ProjectileStrikeInput input,int? penetrationDraw,IReadOnlyList<int> damageDraws,bool actorCanContinue,
        bool deliveryCanContinue,IReadOnlyDictionary<int,ItemInventoryRule> rules)
    {
        ProjectileSeries.Validate(state,rules);
        if(state.Id!=seriesId || state.Pending?.Ordinal!=ordinal)
            throw new InvalidOperationException("Unknown or completed projectile");
        var strike=OriginalProjectileStrike.Resolve(input,penetrationDraw,damageDraws);
        var delivery=input.Contact switch {
            ProjectileContactKind.Miss=>ProjectileOutcome.Miss,
            ProjectileContactKind.Intercepted=>ProjectileOutcome.Blocked,
            _=>ProjectileOutcome.Hit};
        var completed=ProjectileSeries.Complete(state,seriesId,ordinal,delivery,actorCanContinue,deliveryCanContinue,rules);
        return new(completed.State,strike);
    }
}
