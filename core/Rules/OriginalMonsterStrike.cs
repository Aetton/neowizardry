namespace NeoWizardry.Core.Rules;

public sealed record MonsterStrikeResult(PhysicalStrikeOutcome Outcome,MonsterPhysicalScoreBreakdown Scores,PhysicalDamageResult? Damage);
public static class OriginalMonsterStrike
{
    // Neutral ordinary physical Thrust; excludes fumbles, redirection, status/critical multipliers and target absorption.
    public static PhysicalStrikeOutcome Contact(MonsterPhysicalScoreBreakdown scores,int draw)
    {
        if(draw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(draw));
        if(scores.UntargetedHitScore<100)throw new InvalidOperationException("Resolve monster fumble selection before using this strike");
        bool automatic=draw<5;
        if(draw>=95 || !automatic && draw+1>scores.HitScore)return PhysicalStrikeOutcome.Miss;
        return OriginalPhysicalScores.Penetrates(scores.PenetrationScore,draw,automatic)?PhysicalStrikeOutcome.Damage:PhysicalStrikeOutcome.NotPenetrated;
    }
    public static MonsterStrikeResult Resolve(MonsterPhysicalScoreInput input,PhysicalDamageDice damage,int contact,IReadOnlyList<int> draws)
    {
        if(input.Mode!=OriginalAttackMode.Thrust)throw new ArgumentException("Only reviewed ordinary Thrust is connected");
        var scores=OriginalMonsterScores.Build(input);var outcome=Contact(scores,contact);
        if(outcome!=PhysicalStrikeOutcome.Damage)
        {
            if(draws.Count!=0)throw new ArgumentException("Stopped monster attack consumes no damage dice");return new(outcome,scores,null);
        }
        return new(outcome,scores,OriginalPhysicalDamage.Roll(damage,1,0,0,0,draws));
    }
}
