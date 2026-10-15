using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public sealed record ProjectileAttackCompletion(ProjectileSeriesState State,ProjectileStrikeResult Strike);

// Compose ordinary post-delivery resolution with exactly one resource completion.
// Caller owns collision, actual target/defenses, source/weapon preparation, actor
// eligibility, RNG, HP application and committing the current immutable state once.
public static class OriginalProjectileAttack
{
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
