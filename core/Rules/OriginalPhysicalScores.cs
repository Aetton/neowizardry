namespace NeoWizardry.Core.Rules;

public sealed record PhysicalCombatAttributes(int Intelligence,int Dexterity,int Speed,int Senses);
public sealed record OrdinaryPhysicalScoreInput(int BaseAttackRating,int HandToHit,int HandPenetration,
    int AccuracyAdjustment,int PenetrationAdjustment,PhysicalCombatAttributes Attacker,PhysicalCombatAttributes Target,
    OriginalAttackMode Mode,int Defense,int ArmorDefense,int FatigueCategory=0,bool ModernWeapon=false,int ActionPenaltyCounter=0);
public sealed record PhysicalScoreBreakdown(int AttributeContribution,int HitFatiguePenalty,int PenetrationFatiguePenalty,
    int RepetitionPenalty,int UntargetedHitScore,int HitScore,int PenetrationScore);

// Character-side builders in ordinary context: no special +10 flags, post-target condition adjustment or final context scaling.
// Hand bonuses, effective attributes and mode/location-specific defenses must already be resolved.
public static class OriginalPhysicalScores
{
    public static PhysicalScoreBreakdown OrdinaryCharacter(OrdinaryPhysicalScoreInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateAttributes(input.Attacker,125);ValidateAttributes(input.Target,255);
        if(input.FatigueCategory is <0 or >4)throw new ArgumentOutOfRangeException(nameof(input));
        foreach(int value in new[]{input.AccuracyAdjustment,input.PenetrationAdjustment})
            if(value is <-128 or >127)throw new ArgumentOutOfRangeException(nameof(input));
        var mode=OriginalCombatRules.ModeModifiers(input.Mode);
        int fatigue=OriginalFatigueRules.ScorePenalty(input.FatigueCategory);
        int hitFatigue=input.ModernWeapon?fatigue/2:fatigue;
        int penFatigue=input.ModernWeapon?0:fatigue;
        int repetition=RepetitionPenalty(input.Attacker.Speed,input.BaseAttackRating,input.ActionPenaltyCounter);
        int attributes=(Sum(input.Attacker)-Sum(input.Target))/10;
        checked
        {
            int common=input.BaseAttackRating+5*input.HandToHit+5*input.AccuracyAdjustment;
            int untargeted=common+mode.Hit-hitFatigue-repetition;
            int hit=untargeted-5*input.Defense+attributes;
            int penetration=common+5*input.HandPenetration+5*input.PenetrationAdjustment+mode.Penetration-penFatigue-5*input.ArmorDefense+attributes;
            return new(attributes,hitFatigue,penFatigue,repetition,untargeted,hit,penetration);
        }
    }

    // Counter producer/lifecycle is still being traced; do not infer a swing index from it.
    // Native: max(0, trunc(50 - max(0, speed+BAR-25*counter-50)^0.75)), counter>0.
    // Exact integer fourth-root comparison removes platform-specific floating-point boundary drift.
    public static int RepetitionPenalty(int speed,int baseAttackRating,int counter)
    {
        if(speed is <0 or >125)throw new ArgumentOutOfRangeException(nameof(speed));
        return NativeRepetitionPenalty(speed,baseAttackRating,counter);
    }

    internal static int NativeRepetitionPenalty(int speed,int rating,int counter)
    {
        if(speed is <0 or >255)throw new ArgumentOutOfRangeException(nameof(speed));
        if(counter<0)throw new ArgumentOutOfRangeException(nameof(counter));
        if(counter==0)return 0;
        long reserve=Math.Max(0,(long)speed+rating-25L*counter-50);
        if(reserve>=185)return 0;
        long cube=reserve*reserve*reserve;
        int ceiling=0;
        while((long)ceiling*ceiling*ceiling*ceiling<cube)ceiling++;
        return Math.Max(0,50-ceiling);
    }

    // Ordinary comparison only. Caller supplies the existing zero-based d100 draw;
    // this does not request a second roll or decide hit, fumble, target selection or damage.
    public static bool Penetrates(int penetrationScore,int draw,bool automaticPenetration=false)
    {
        if(draw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(draw));
        return automaticPenetration || draw+1<=penetrationScore;
    }

    private static int Sum(PhysicalCombatAttributes a)=>a.Intelligence+a.Dexterity+a.Speed+a.Senses;
    private static void ValidateAttributes(PhysicalCombatAttributes a,int maximum)
    {
        ArgumentNullException.ThrowIfNull(a);
        foreach(int value in new[]{a.Intelligence,a.Dexterity,a.Speed,a.Senses})
            if(value<0 || value>maximum)throw new ArgumentOutOfRangeException(nameof(a));
    }
}
