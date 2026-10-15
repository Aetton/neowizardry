namespace NeoWizardry.Core.Rules;

public enum CastingRiskBand { DarkGreen,LightGreen,Yellow,Orange,Red }
public enum CastingOutcome { Success,Fizzle,Backfire }
public sealed record CastingRiskInput(int BookSkill,int RealmSkill,int SpellLevel,int BaseManaCost,int Power,int EffectiveCasterLevel);
public sealed record CastingRisk(int EffectiveSkill,int RequiredSkill,int SkillFailure,int LevelPenalty,int FailureThreshold,CastingRiskBand Band);
public sealed record ResistanceRollBounds(int Pressure,int Baseline,int FirstExclusive,int SecondExclusive);
public sealed record SpellDamageResult(int Damage,int ReductionPercent);

public static class OriginalMagicRules
{
    private static readonly int[] SkillDifficulty=[30,40,50,58,64,70,76,81,86,90,94,97,100,102,105,107,110];
    private static readonly int[] MinimumLevels=[1,3,5,8,11,14,18];
    private static void Spell(int level,int cost,int power)
    {
        if(level is <1 or >7 || cost<0 || power is <1 or >7)throw new ArgumentOutOfRangeException(nameof(level));
    }
    // Selected book skill and effective class-level contribution must already be
    // resolved by the caller. Class/book selection, silence and learning are separate.
    // Ordinary player casting only; external situational modifiers are not included.
    public static CastingRisk OrdinaryCastingRisk(CastingRiskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);Spell(input.SpellLevel,input.BaseManaCost,input.Power);
        if(input.BookSkill is <0 or >125 || input.RealmSkill is <0 or >125 || input.EffectiveCasterLevel< -1)
            throw new ArgumentOutOfRangeException(nameof(input));
        int skill=(input.BookSkill+4*input.RealmSkill)/5;
        int required=SkillDifficulty[Math.Min(16,input.BaseManaCost/2+input.SpellLevel)]*input.Power/7;
        int failure=skill>=required?0:70*(required-skill)/required;
        int deficit=Math.Max(0,checked(MinimumLevels[input.SpellLevel-1]-input.EffectiveCasterLevel+input.Power-1));
        int penalty=checked(deficit*input.SpellLevel);int threshold=checked(failure+penalty);
        return new(skill,required,failure,penalty,threshold,RiskBand(threshold));
    }
    public static CastingRiskBand RiskBand(int threshold)=>threshold<0?throw new ArgumentOutOfRangeException(nameof(threshold)):
        threshold==0?CastingRiskBand.DarkGreen:threshold<=5?CastingRiskBand.LightGreen:threshold<=15?CastingRiskBand.Yellow:
        threshold<=40?CastingRiskBand.Orange:CastingRiskBand.Red;
    // Zero risk consumes no roll. A single zero-based d100 is shared by failure
    // and backfire, with strict '<' boundaries. Eligibility is a caller policy.
    public static CastingOutcome ResolveCasting(int threshold,int? roll,bool backfireEligible)
    {
        if(threshold<0)throw new ArgumentOutOfRangeException(nameof(threshold));
        if(threshold==0)
        {
            if(roll is not null)throw new ArgumentException("Safe casting consumes no failure roll");
            return CastingOutcome.Success;
        }
        if(roll is null or <0 or >=100)throw new ArgumentOutOfRangeException(nameof(roll));
        if(roll>=threshold)return CastingOutcome.Success;
        return backfireEligible && threshold>5 && roll<threshold/3?CastingOutcome.Backfire:CastingOutcome.Fizzle;
    }
    public static int PowerCastBonus(bool enabled,int effectiveSkill)
    {
        if(effectiveSkill is <0 or >125)throw new ArgumentOutOfRangeException(nameof(effectiveSkill));
        return enabled?effectiveSkill/4+1:0;
    }
    public static int ScaleAmount(int amount,int bonusPercent)
    {
        if(amount<0 || bonusPercent<0)throw new ArgumentOutOfRangeException(nameof(amount));
        return checked((int)(amount+(long)amount*bonusPercent/100));
    }
    // Base helper only; per-spell adjustments and the caller's half-Power-Cast
    // scaling must be applied separately. This is not a complete effect resolver.
    public static int BaseEffectStrength(int casterLevelTerm,int spellLevel,int baseManaCost,int power,bool allEnemies)
    {
        Spell(spellLevel,baseManaCost,power);
        if(casterLevelTerm<0)throw new ArgumentOutOfRangeException(nameof(casterLevelTerm));
        return Math.Max(0,checked(casterLevelTerm/2+(baseManaCost/2+spellLevel)/2+power-(allEnemies?3:0)));
    }
    // pressure = 3*(target level - resolved effect strength) + effective realm resistance.
    // The original 9999 damage sentinel is outside this arithmetic API: callers
    // must represent bypass/special effects explicitly rather than as fake damage.
    public static ResistanceRollBounds ResistanceBounds(int targetLevel,int effectStrength,int effectiveResistance)
    {
        if(targetLevel<0 || effectStrength<0)throw new ArgumentOutOfRangeException(nameof(targetLevel));
        int pressure=checked(3*(targetLevel-effectStrength)+effectiveResistance);
        return BoundsFromPressure(pressure);
    }
    private static ResistanceRollBounds BoundsFromPressure(int pressure)
    {
        if(pressure<=0)return new(pressure,0,0,0);
        int sum=checked(pressure+1);int baseline=sum/2;
        return new(pressure,baseline,sum-baseline,baseline);
    }
    public static SpellDamageResult ResistDamage(int rolledDamage,ResistanceRollBounds bounds,int? firstRoll,int? secondRoll)
    {
        if(rolledDamage<0 || rolledDamage==9999)throw new ArgumentOutOfRangeException(nameof(rolledDamage));
        ArgumentNullException.ThrowIfNull(bounds);
        if(bounds!=BoundsFromPressure(bounds.Pressure))throw new ArgumentException("Invalid resistance bounds");
        if(bounds.Pressure<=0)
        {
            if(firstRoll is not null || secondRoll is not null)throw new ArgumentException("No resistance rolls required");
            return new(rolledDamage,0);
        }
        if(firstRoll is null or <0 || firstRoll>=bounds.FirstExclusive || secondRoll is null or <0 || secondRoll>=bounds.SecondExclusive)
            throw new ArgumentOutOfRangeException(nameof(firstRoll));
        int reduction=(int)Math.Min(100,(long)bounds.Baseline+firstRoll.Value+secondRoll.Value);
        return new(rolledDamage-(int)((long)rolledDamage*reduction/100),reduction);
    }
}
