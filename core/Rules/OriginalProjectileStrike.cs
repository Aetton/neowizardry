namespace NeoWizardry.Core.Rules;

public enum ProjectileContactKind { Miss,Contact,Intercepted }
public enum ProjectileStrikeOutcome { Miss,Intercepted,NotPenetrated,Damage }
public sealed record ProjectileStrikeInput(OriginalAttackMode Mode,ProjectileContactKind Contact,int PenetrationScore,
    PhysicalDamageDice Damage,int HandDamagePercent=0,int EffectDamagePercent=0,int AbsorptionPercent=0);
public sealed record ProjectileStrikeResult(ProjectileStrikeOutcome Outcome,int? PenetrationRoll,PhysicalDamageResult? Damage);

public static class OriginalProjectileStrike
{
    // Ordinary actor collision, projectile metadata +0x154 == 0. The caller has
    // already established collision and resolved the actor's native interception byte.
    // This is not a trajectory/hit-probability function or shield-stat derivation.
    public static ProjectileContactKind ActorContact(int interceptionChance,int? interceptionDraw)
    {
        if(interceptionChance is <0 or >255)throw new ArgumentOutOfRangeException(nameof(interceptionChance));
        if(interceptionChance==0)
        {
            if(interceptionDraw is not null)throw new ArgumentException("Zero interception consumes no draw");
            return ProjectileContactKind.Contact;
        }
        if(interceptionDraw is not int draw || draw is <0 or >99)
            throw new ArgumentException("Supply exactly one resolved d100 for positive interception");
        return draw+1<=interceptionChance?ProjectileContactKind.Intercepted:ProjectileContactKind.Contact;
    }

    // Ordinary shooting/throwing after delivery, with no special auto-penetration
    // eligibility, repetition or target-specific effect. Contact bypasses the melee
    // hit comparison, then uses a fresh penetration d100. No melee natural 1..5/96..100.
    public static ProjectileStrikeResult Resolve(ProjectileStrikeInput input,int? penetrationDraw,IReadOnlyList<int> damageDraws)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.Damage);
        ArgumentNullException.ThrowIfNull(damageDraws);
        if(input.Mode is not (OriginalAttackMode.Shoot or OriginalAttackMode.Throw) || !Enum.IsDefined(input.Contact))
            throw new ArgumentException("Select an ordinary shooting/throwing delivery result");
        if(input.Contact!=ProjectileContactKind.Contact)
        {
            if(penetrationDraw is not null)throw new ArgumentException("No penetration draw before contact");
            return NoDamage(input.Contact==ProjectileContactKind.Miss?ProjectileStrikeOutcome.Miss:ProjectileStrikeOutcome.Intercepted,null);
        }
        if(penetrationDraw is not int draw || draw is <0 or >99)
            throw new ArgumentException("Actor contact consumes one fresh penetration d100");
        if(!OriginalPhysicalScores.Penetrates(input.PenetrationScore,draw))
            return NoDamage(ProjectileStrikeOutcome.NotPenetrated,draw+1);
        var damage=OriginalPhysicalDamage.Roll(input.Damage,1,input.HandDamagePercent,input.EffectDamagePercent,input.AbsorptionPercent,damageDraws);
        return new(ProjectileStrikeOutcome.Damage,draw+1,damage);

        ProjectileStrikeResult NoDamage(ProjectileStrikeOutcome outcome,int? roll)
        {
            if(damageDraws.Count!=0)throw new ArgumentException("Missed, intercepted or unpenetrated projectiles consume no damage dice");
            return new(outcome,roll,null);
        }
    }
}
