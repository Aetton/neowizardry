namespace NeoWizardry.Core.Rules;

public sealed record MonsterStrikeResult(PhysicalStrikeOutcome Outcome,MonsterPhysicalScoreBreakdown Scores,PhysicalDamageResult? Damage);
public static class OriginalMonsterStrike
{
    // Neutral melee modes with caller-resolved fumble/redirect state and target absorption. Status/critical producers remain external.
    public static PhysicalStrikeOutcome Contact(MonsterPhysicalScoreBreakdown scores,int draw,bool fumbleResolved=false,bool redirected=false)
    {
        if(draw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(draw));
        if(scores.UntargetedHitScore<100 && !fumbleResolved)throw new InvalidOperationException("Resolve monster fumble selection before using this strike");
        if(redirected)
        {
            if(!fumbleResolved)throw new ArgumentException("Redirected contact requires resolved fumble");
            // 54052E..540538 forces hit, clears natural miss and automatic penetration.
            return OriginalPhysicalScores.Penetrates(scores.PenetrationScore,draw,false)?PhysicalStrikeOutcome.Damage:PhysicalStrikeOutcome.NotPenetrated;
        }
        bool automatic=draw<5;
        if(draw>=95 || !automatic && draw+1>scores.HitScore)return PhysicalStrikeOutcome.Miss;
        return OriginalPhysicalScores.Penetrates(scores.PenetrationScore,draw,automatic)?PhysicalStrikeOutcome.Damage:PhysicalStrikeOutcome.NotPenetrated;
    }
    public static MonsterStrikeResult Resolve(MonsterPhysicalScoreInput input,PhysicalDamageDice damage,int contact,IReadOnlyList<int> draws,int absorptionPercent=0,bool fumbleResolved=false,bool redirected=false)
    {
        if(input.Mode is OriginalAttackMode.Throw or OriginalAttackMode.Shoot)throw new ArgumentException("Projectile delivery is not connected");
        var scores=OriginalMonsterScores.Build(input);var outcome=Contact(scores,contact,fumbleResolved,redirected);
        if(outcome!=PhysicalStrikeOutcome.Damage)
        {
            if(draws.Count!=0)throw new ArgumentException("Stopped monster attack consumes no damage dice");return new(outcome,scores,null);
        }
        return new(outcome,scores,OriginalPhysicalDamage.Roll(damage,input.Mode==OriginalAttackMode.Berserk?2:1,0,0,absorptionPercent,draws));
    }
}
