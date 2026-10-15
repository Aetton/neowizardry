namespace NeoWizardry.Core.Rules;

public enum MonsterAttackFatiguePolicy { FullHitOnly,HalfHitAndFullPenetration }
// Resolved policies from native helper 0x55CCB0, without guessing faction/difficulty names.
public enum PhysicalScoreScale { Unscaled,SixtyPercent,HundredFortyPercent }
public sealed record MonsterPhysicalScoreInput(int AttackRating,int AccuracyAdjustment,int PenetrationAdjustment,
    PhysicalCombatAttributes Attacker,PhysicalCombatAttributes Target,OriginalAttackMode Mode,
    int Defense,int ArmorDefense,int FatigueCategory,MonsterAttackFatiguePolicy FatiguePolicy,
    int ActionPenaltyCounter=0,bool HitBonus1=false,bool HitBonus2=false,bool LimitHitToTen=false,
    PhysicalScoreScale Scale=PhysicalScoreScale.Unscaled);
public sealed record MonsterPhysicalScoreBreakdown(int AttributeContribution,int HitFatiguePenalty,int PenetrationFatiguePenalty,
    int RepetitionPenalty,int RawUntargetedHitScore,int RawHitScore,int RawPenetrationScore,
    int UntargetedHitScore,int HitScore,int PenetrationScore);

public static class OriginalMonsterScores
{
    // Selected attack, effective facts and defenses already resolved. No target selection or RNG.
    public static MonsterPhysicalScoreBreakdown Build(MonsterPhysicalScoreInput input)
    {
        ArgumentNullException.ThrowIfNull(input);ValidateAttributes(input.Attacker);ValidateAttributes(input.Target);
        if(input.AttackRating is <0 or >255 || input.AccuracyAdjustment is <-128 or >127 ||
            input.PenetrationAdjustment is <-128 or >127 || !Enum.IsDefined(input.FatiguePolicy))
            throw new ArgumentOutOfRangeException(nameof(input));
        var mode=OriginalCombatRules.ModeModifiers(input.Mode);
        int fatigue=OriginalFatigueRules.ScorePenalty(input.FatigueCategory);
        bool reduced=input.FatiguePolicy==MonsterAttackFatiguePolicy.HalfHitAndFullPenetration;
        int hitFatigue=reduced?fatigue/2:fatigue,penFatigue=reduced?fatigue:0;
        int repetition=OriginalPhysicalScores.NativeRepetitionPenalty(input.Attacker.Speed,input.AttackRating,input.ActionPenaltyCounter);
        int attributes=(Sum(input.Attacker)-Sum(input.Target))/10;
        checked
        {
            int common=input.AttackRating+5*input.AccuracyAdjustment;
            int untargeted=common+mode.Hit+(input.HitBonus1?10:0)+(input.HitBonus2?10:0)-hitFatigue-repetition;
            int hit=untargeted-5*input.Defense+attributes;
            int penetration=common+5*input.PenetrationAdjustment+mode.Penetration-penFatigue-5*input.ArmorDefense+attributes;
            int cappedUntargeted=input.LimitHitToTen?Math.Min(untargeted,10):untargeted;
            int cappedHit=input.LimitHitToTen?Math.Min(hit,10):hit;
            return new(attributes,hitFatigue,penFatigue,repetition,untargeted,hit,penetration,
                Scale(cappedUntargeted,input.Scale),Scale(cappedHit,input.Scale),Scale(penetration,input.Scale));
        }
    }

    public static int Scale(int score,PhysicalScoreScale scale)
    {
        // Native multiplies by 3 or 7, then by 20, then divides by 100 toward zero.
        // Preserve those checked intermediate widths instead of algebraically reducing the fraction.
        return scale switch {
            PhysicalScoreScale.Unscaled=>score,
            PhysicalScoreScale.SixtyPercent=>checked(checked(checked(score*3)*20)/100),
            PhysicalScoreScale.HundredFortyPercent=>checked(checked(checked(score*7)*20)/100),
            _=>throw new ArgumentOutOfRangeException(nameof(scale))};
    }

    private static int Sum(PhysicalCombatAttributes a)=>a.Intelligence+a.Dexterity+a.Speed+a.Senses;
    private static void ValidateAttributes(PhysicalCombatAttributes attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        foreach(int value in new[]{attributes.Intelligence,attributes.Dexterity,attributes.Speed,attributes.Senses})
            if(value is <0 or >255)throw new ArgumentOutOfRangeException(nameof(attributes));
    }
}
