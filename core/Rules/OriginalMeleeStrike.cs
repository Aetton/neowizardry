namespace NeoWizardry.Core.Rules;

public enum PhysicalStrikeOutcome { Miss,NotPenetrated,Damage }
public sealed record PhysicalStrikeResult(PhysicalStrikeOutcome Outcome,PhysicalScoreBreakdown Scores,PhysicalDamageResult? Damage);

public static class OriginalMeleeStrike
{
    // Ordinary weapon melee, neutral context, resolved defenses and target damage modifier.
    // Scope excludes redirection: native untargeted score >=100 gives zero cubic fumble term.
    // One contact d100 is shared by hit and penetration. Damage draws exist only after both pass.
    public static PhysicalStrikeResult Resolve(OrdinaryPhysicalScoreInput input,PhysicalDamageDice profile,
        int handDamagePercent,int effectDamagePercent,int absorptionPercent,int contactDraw,IReadOnlyList<int> damageDraws)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(profile);ArgumentNullException.ThrowIfNull(damageDraws);
        if(input.Mode is not (OriginalAttackMode.Swing or OriginalAttackMode.Thrust or OriginalAttackMode.Bash or OriginalAttackMode.Berserk or OriginalAttackMode.Lash))
            throw new ArgumentException("Only ordinary weapon melee is covered");
        if(contactDraw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(contactDraw));
        var scores=OriginalPhysicalScores.OrdinaryCharacter(input);
        if(scores.UntargetedHitScore<100)throw new InvalidOperationException("Fumble target selection must be resolved for this score");
        bool automatic=contactDraw<5;
        if(contactDraw>=95 || !automatic && contactDraw+1>scores.HitScore)
            return NoDamage(PhysicalStrikeOutcome.Miss);
        if(!OriginalPhysicalScores.Penetrates(scores.PenetrationScore,contactDraw,automatic))
            return NoDamage(PhysicalStrikeOutcome.NotPenetrated);
        int repetitions=input.Mode==OriginalAttackMode.Berserk?2:1;
        var damage=OriginalPhysicalDamage.Roll(profile,repetitions,handDamagePercent,effectDamagePercent,absorptionPercent,damageDraws);
        return new(PhysicalStrikeOutcome.Damage,scores,damage);

        PhysicalStrikeResult NoDamage(PhysicalStrikeOutcome outcome)
        {
            if(damageDraws.Count!=0)throw new ArgumentException("A missed or stopped strike consumes no damage dice");
            return new(outcome,scores,null);
        }
    }
}
