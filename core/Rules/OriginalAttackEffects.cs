using System.Collections.Immutable;

namespace NeoWizardry.Core.Rules;

public sealed record AttackConditionRoute(int EffectIndex,int ConditionId,int RealmIndex,bool Permanent=false,bool Secondary=false);
public sealed record AttackEffectDurationPlan(int GateDraw,int Dice,int Sides,int Constant,bool Clamped);
public sealed record ConditionResistance(int RawScore,int ScaledScore,int Threshold);
public sealed record AttackConditionRequest(int ConditionId,int RealmIndex,int Strength,int? Duration,int SecondaryParameter);
public sealed record AttackConditionInput(int EffectIndex,int Probability,int Strength,int TargetLevel,int EffectiveResistance,
    int SecondaryParameter=0,bool Immune=false,PhysicalScoreScale Scale=PhysicalScoreScale.Unscaled);
public enum AttackConditionOutcome { NotTriggered,Immune,Resisted,Requested }
public sealed record AttackEffectDraw(string Stage,int Exclusive,int Value);
public sealed record AttackConditionResolution(AttackConditionOutcome Outcome,AttackConditionRequest? Request,ConditionResistance? Resistance,ImmutableArray<AttackEffectDraw> Draws);

// Selected post-hit condition requests only. Produces neither actor conditions nor ticks/actions.
// Native IDs stay at this boundary until their complete gameplay consumers are recovered.
public static class OriginalAttackEffects
{
    private static readonly int[] ConditionOffsets=[0,1,2,3,4,4,5,6,7,8,9,10,12,14,16,20,18,22,24,22];
    public static AttackConditionRoute? Route(int index)
    {
        if(index is <0 or >15)throw new ArgumentOutOfRangeException(nameof(index));
        return index switch {
            0=>new(index,15,2),1=>new(index,16,1),2=>new(index,7,1,Secondary:true),3=>new(index,9,5),
            4=>new(index,2,1,Permanent:true),6=>new(index,17,3),7=>new(index,12,0),8=>new(index,6,4),
            14=>new(index,4,2),15=>new(index,11,4),_=>null};
    }
    public static int Strength(int definitionLevel)
    {
        if(definitionLevel is <0 or >255)throw new ArgumentOutOfRangeException(nameof(definitionLevel));
        return definitionLevel+Math.Min(definitionLevel,15);
    }
    // Zero probability consumes no draw; nonzero values, including >=100, consume one.
    public static bool Triggers(int probability,int? draw)
    {
        if(probability is <0 or >255)throw new ArgumentOutOfRangeException(nameof(probability));
        if(probability==0){if(draw is not null)throw new ArgumentException("Disabled effect consumes no chance draw");return false;}
        if(draw is null or <0 or >99)throw new ArgumentOutOfRangeException(nameof(draw));
        return draw<probability;
    }
    // Neutral monster payload: zero percentage and level-dependent duration modifiers.
    // 5519C0 always requests a d4. Only zero-based 1 returns 2 and avoids the fallback dice.
    // Every triggered index runs this common stage, including permanent and special effects.
    public static AttackEffectDurationPlan DurationPlan(int strength,int gateDraw,bool secondaryStage=false,int secondaryConstant=0)
    {
        if(strength is <0 or >270 || gateDraw is <0 or >3 || secondaryConstant is <0 or >255)throw new ArgumentOutOfRangeException(nameof(strength));
        if(!secondaryStage && secondaryConstant!=0)throw new ArgumentException("Secondary parameter belongs to its second stage");
        int basis=strength/3+1;
        // 517950 stores the dice count in a byte, including the secondary 3*basis overflow.
        return gateDraw==1?new(gateDraw,0,0,2,!secondaryStage):new(gateDraw,secondaryStage?unchecked((byte)(3*basis)):basis,2,secondaryStage?secondaryConstant+1:2,!secondaryStage);
    }
    public static int Duration(AttackEffectDurationPlan plan,IReadOnlyList<int> draws)
    {
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(draws);
        // Require a plan produced by this neutral domain, not editable arbitrary dice.
        if(plan.GateDraw is <0 or >3 || plan.Dice<0 || plan.Dice>(plan.Clamped?91:255) ||
            plan.GateDraw==1 && (plan.Dice!=0 || plan.Sides!=0 || plan.Constant!=2) ||
            plan.GateDraw!=1 && (plan.Dice<1 || plan.Sides!=2 || plan.Clamped && plan.Constant!=2 || !plan.Clamped && ((plan.Dice%3!=0 && plan.Dice is not (2 or 5 or 8 or 11 or 14 or 17)) || plan.Constant is <1 or >256)))throw new ArgumentException("Invalid effect duration plan");
        if(draws.Count!=plan.Dice)throw new ArgumentException("Supply exactly one draw per duration die");
        int value=plan.Constant;foreach(int draw in draws){if(draw is <0 or >1)throw new ArgumentOutOfRangeException(nameof(draws));value+=draw+1;}
        return plan.Clamped?Math.Clamp(value,1,12):value;
    }
    // A request is not an applied status. Immunities, current state, stacking, ticks and
    // action/defense consumers must be connected before accepting the record into live combat.
    // Null duration represents the native permanent sentinel, never 9999 simulated rounds.
    public static AttackConditionRequest Request(int index,int strength,int commonDuration,int? secondaryDuration=null,int secondaryParameter=0)
    {
        var route=Route(index)??throw new InvalidOperationException("This effect needs a special delivery/resource policy");
        if(strength is <0 or >270 || commonDuration is <1 or >12 || secondaryParameter is <0 or >255)throw new ArgumentOutOfRangeException(nameof(strength));
        if(route.Secondary?secondaryDuration is null or <1:secondaryDuration is not null || secondaryParameter!=0)throw new ArgumentException("Supply the second duration stage only for secondary");
        return new(route.ConditionId,route.RealmIndex,strength,route.Permanent?null:route.Secondary?secondaryDuration:commonDuration,secondaryParameter);
    }
    // 5520D0 binary condition resistance differs from 552250 amount reduction.
    // Caller resolves immunities, target state, level, effective realm resistance and score scale.
    public static ConditionResistance Resistance(int targetLevel,int strength,int effectiveResistance,int conditionId,PhysicalScoreScale scale=PhysicalScoreScale.Unscaled)
    {
        if(targetLevel<0 || strength<0 || conditionId is <0 or >19)throw new ArgumentOutOfRangeException(nameof(conditionId));
        int raw=checked(checked(3*checked(targetLevel-strength))+effectiveResistance+ConditionOffsets[conditionId]);
        int scaled=OriginalMonsterScores.Scale(raw,scale);
        return new(raw,scaled,Math.Clamp(scaled,5,95));
    }
    public static bool Resisted(ConditionResistance resistance,int draw)
    {
        ArgumentNullException.ThrowIfNull(resistance);
        if(resistance.Threshold!=Math.Clamp(resistance.ScaledScore,5,95))throw new ArgumentException("Invalid resistance threshold");
        if(draw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(draw));
        return draw<resistance.Threshold;
    }
    // One selected ordinary condition, after the caller establishes a qualifying hit/live
    // target. Immutable explicit draws prove the stage ordering without changing any RNG.
    // Immune is the caller-resolved native eligibility result, not a replacement immunity model.
    public static AttackConditionResolution ResolveCondition(AttackConditionInput input,IReadOnlyList<int> draws)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(draws);
        var route=Route(input.EffectIndex)??throw new InvalidOperationException("Special/resource effect is not an ordinary condition");
        if(input.Strength is <0 or >270 || input.SecondaryParameter is <0 or >255 || !route.Secondary && input.SecondaryParameter!=0)throw new ArgumentOutOfRangeException(nameof(input));
        // Validate even on a non-triggered/immune path, without consuming a resistance draw.
        var resistance=Resistance(input.TargetLevel,input.Strength,input.EffectiveResistance,route.ConditionId,input.Scale);
        int cursor=0;var trace=ImmutableArray.CreateBuilder<AttackEffectDraw>();
        int Next(string stage,int exclusive)
        {
            if(cursor>=draws.Count)throw new ArgumentException("Missing draw for "+stage);
            int value=draws[cursor++];if(value<0 || value>=exclusive)throw new ArgumentOutOfRangeException(nameof(draws));
            trace.Add(new(stage,exclusive,value));return value;
        }
        AttackConditionResolution End(AttackConditionOutcome outcome,AttackConditionRequest? request=null,ConditionResistance? score=null)
        {
            if(cursor!=draws.Count)throw new ArgumentException("Unused effect draws");
            return new(outcome,request,score,trace.ToImmutable());
        }
        if(!Triggers(input.Probability,input.Probability==0?null:Next("chance",100)))return End(AttackConditionOutcome.NotTriggered);
        int RollDuration(bool secondary)
        {
            string stage=secondary?"secondary-duration":"common-duration";
            var plan=DurationPlan(input.Strength,Next(stage+"-gate",4),secondary,secondary?input.SecondaryParameter:0);
            var dice=Enumerable.Range(0,plan.Dice).Select(_=>Next(stage+"-die",2)).ToArray();
            return Duration(plan,dice);
        }
        int common=RollDuration(false);int? secondary=route.Secondary?RollDuration(true):null;
        var request=Request(input.EffectIndex,input.Strength,common,secondary,input.SecondaryParameter);
        if(input.Immune)return End(AttackConditionOutcome.Immune);
        return Resisted(resistance,Next("resistance",100))?End(AttackConditionOutcome.Resisted,score:resistance):End(AttackConditionOutcome.Requested,request,resistance);
    }
}
