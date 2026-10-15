using System.Text.Json;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class MonsterScoreChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject<T>(Action action,string name) where T:Exception
        {
            bool rejected=false;try{action();}catch(T){rejected=true;}Check(rejected,name);
        }
        var actor=new PhysicalCombatAttributes(100,100,100,100);
        var target=new PhysicalCombatAttributes(80,80,80,80);
        var input=new MonsterPhysicalScoreInput(100,2,3,actor,target,OriginalAttackMode.Thrust,4,6,3,
            MonsterAttackFatiguePolicy.HalfHitAndFullPenetration);
        var scores=OriginalMonsterScores.Build(input);
        Check(scores==new MonsterPhysicalScoreBreakdown(8,10,20,0,90,78,93,90,78,93),"Monster native attack rating, signed effects, mode, defenses and attributes combine independently of character BAR");
        var standard=OriginalMonsterScores.Build(input with {FatiguePolicy=MonsterAttackFatiguePolicy.FullHitOnly});
        Check(standard.HitScore==68 && standard.PenetrationScore==113 && standard.HitFatiguePenalty==20 && standard.PenetrationFatiguePenalty==0,
            "Ordinary non-table monster attack applies full hit fatigue and no penetration fatigue");
        Check(OriginalMonsterScores.Build(input with {FatigueCategory=1}).HitFatiguePenalty==2,"Odd reduced fatigue truncates before subtraction");
        var repeated=OriginalMonsterScores.Build(input with {ActionPenaltyCounter=1});
        Check(repeated.RepetitionPenalty==12 && repeated.HitScore==66 && repeated.PenetrationScore==93,"Monster context counter penalizes only hit");
        var special=OriginalMonsterScores.Build(input with {HitBonus1=true,HitBonus2=true});
        Check(special.HitScore==98 && special.UntargetedHitScore==110 && special.PenetrationScore==93,"Independent native +10 flags affect targeted and untargeted hit only");
        var capped=OriginalMonsterScores.Build(input with {LimitHitToTen=true,Scale=PhysicalScoreScale.HundredFortyPercent});
        Check(capped.RawHitScore==78 && capped.RawUntargetedHitScore==90 && capped.HitScore==14 && capped.UntargetedHitScore==14 && capped.PenetrationScore==130,
            "Hit cap applies after target comparison and before scaling, while penetration remains uncapped");
        var bad=input with {AttackRating=0,AccuracyAdjustment=-2,Defense=10,ArmorDefense=10,FatigueCategory=0,Mode=OriginalAttackMode.Swing,Attacker=target};
        var badScores=OriginalMonsterScores.Build(bad with {LimitHitToTen=true});
        Check(badScores.UntargetedHitScore==-10 && badScores.HitScore==-60,"Ten-point cap never raises negative scores");
        Check(OriginalMonsterScores.Build(input with {Defense=-100,ArmorDefense=-100}).HitScore>100 &&
            OriginalMonsterScores.Build(input with {Defense=100,ArmorDefense=100}).PenetrationScore<0,"Scores remain raw thresholds without percentage clamping");
        Check(OriginalMonsterScores.Build(input with {Attacker=new(255,255,255,255),Target=new(0,0,0,0)}).AttributeContribution==102,
            "Effective monster attributes preserve full unsigned byte range");
        foreach(int difference in new[]{-11,-10,-9,0,9,10,11})
            Check(OriginalMonsterScores.Build(input with {Attacker=target with {Intelligence=80+difference}}).AttributeContribution==difference/10,
                "Signed attribute difference truncates toward zero");
        int[] kinds={7,13,14,15,21,25,33,43,49,50,52,53,54,56,59,85,86,87};
        for(int kind=0;kind<=255;kind++)
            Check(OriginalMonsterAttackAdapter.FatiguePolicy(kind)==(kinds.Contains(kind)?MonsterAttackFatiguePolicy.HalfHitAndFullPenetration:MonsterAttackFatiguePolicy.FullHitOnly),
                "Every native attack-kind byte matches the recovered eighteen-entry classifier");
        foreach(var (category,full,half) in new[]{(0,0,0),(1,5,2),(2,10,5),(3,20,10),(4,40,20)})
        {
            var a=OriginalMonsterScores.Build(input with {FatigueCategory=category});
            var b=OriginalMonsterScores.Build(input with {FatigueCategory=category,FatiguePolicy=MonsterAttackFatiguePolicy.FullHitOnly});
            Check(a.HitFatiguePenalty==half && a.PenetrationFatiguePenalty==full && b.HitFatiguePenalty==full && b.PenetrationFatiguePenalty==0,
                "Both monster fatigue policies preserve every category boundary");
        }
        foreach(var mode in Enum.GetValues<OriginalAttackMode>())
        {
            var a=OriginalMonsterScores.Build(input with {Mode=mode});
            int[] hit={0,-10,0,-30,-5,5,-10,10,0},pen={0,10,-5,-30,5,-5,0,-10,10};
            Check(a.HitScore==88+hit[(int)mode] && a.PenetrationScore==83+pen[(int)mode],"All nine resolved attack modes select independent hit and penetration modifiers");
        }
        foreach(int reserve in Enumerable.Range(0,186))
        {
            var a=OriginalMonsterScores.Build(input with {AttackRating=75,Attacker=actor with {Speed=reserve},ActionPenaltyCounter=1});
            Check(a.RepetitionPenalty==Math.Max(0,(int)(50-Math.Pow(reserve,0.75))),"Monster nonlinear term matches every unsaturated integer reserve");
        }
        Check(OriginalMonsterScores.Build(input with {AttackRating=255,Attacker=actor with {Speed=255},ActionPenaltyCounter=1}).RepetitionPenalty==0,
            "Monster Speed above character cap is accepted by shared nonlinear arithmetic");
        Check(OriginalMonsterScores.Build(input with {ActionPenaltyCounter=int.MaxValue}).RepetitionPenalty==50,"Large external context counter cannot wrap reserve arithmetic");
        foreach(var (tag,setting,scale) in new[]{(1,0,PhysicalScoreScale.SixtyPercent),(1,2,PhysicalScoreScale.HundredFortyPercent),
            (2,0,PhysicalScoreScale.HundredFortyPercent),(2,2,PhysicalScoreScale.SixtyPercent),(1,1,PhysicalScoreScale.Unscaled),
            (2,-1,PhysicalScoreScale.Unscaled),(0,0,PhysicalScoreScale.Unscaled),(255,2,PhysicalScoreScale.Unscaled)})
            Check(OriginalMonsterAttackAdapter.ScoreScale(tag,setting)==scale,"Resolved native actor tag and setting select exact scale without inferred faction names");
        foreach(int raw in new[]{-101,-11,-2,-1,0,1,2,11,101})
        {
            Check(OriginalMonsterScores.Scale(raw,PhysicalScoreScale.SixtyPercent)==raw*60/100,"Sixty-percent scale keeps signed division order");
            Check(OriginalMonsterScores.Scale(raw,PhysicalScoreScale.HundredFortyPercent)==raw*140/100,"Hundred-forty-percent scale keeps signed division order");
            Check(OriginalMonsterScores.Scale(raw,PhysicalScoreScale.Unscaled)==raw,"Neutral scale preserves signed value");
        }
        var defender=OriginalCharacterDefense.Build(new(90,20,100,0,false),new(new(0,5,0,0,0),3,0),new(0,0,false,false,false),new(0,0,0,false));
        var defenses=OriginalCharacterDefense.Against(defender,CharacterHitLocation.Torso,OriginalAttackMode.Thrust,new(false,false,false));
        var connected=input with {AttackRating=200,AccuracyAdjustment=0,PenetrationAdjustment=0,FatigueCategory=0,Attacker=target,Defense=defenses.Hit,ArmorDefense=defenses.Penetration};
        var againstHero=OriginalMonsterScores.Build(connected);
        Check(defenses.Hit==10 && defenses.Penetration==15 && againstHero.HitScore==140 && againstHero.PenetrationScore==135,
            "Monster attack composes with character base and selected torso armor without using display average");
        var barrier=OriginalMonsterScores.Build(connected with {ArmorDefense=30});
        Check(barrier.HitScore==140 && barrier.PenetrationScore==60 && OriginalPhysicalScores.Penetrates(barrier.PenetrationScore,59) &&
            !OriginalPhysicalScores.Penetrates(barrier.PenetrationScore,60),"Armor stops penetration independently of hit with inclusive shared threshold");
        var saved=JsonSerializer.Deserialize<MonsterPhysicalScoreInput>(JsonSerializer.Serialize(input));
        Check(saved==input && OriginalMonsterScores.Build(saved!)==scores,"Calculation input roundtrip preserves monster attack policy and outcomes");
        Reject<ArgumentNullException>(()=>OriginalMonsterScores.Build(null!),"Input required");
        Reject<ArgumentNullException>(()=>OriginalMonsterScores.Build(input with {Attacker=null!}),"Attacker facts required");
        Reject<ArgumentNullException>(()=>OriginalMonsterScores.Build(input with {Target=null!}),"Target facts required");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {AttackRating=-1}),"Negative rating rejected");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {AttackRating=256}),"Rating retains byte domain");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {AccuracyAdjustment=128}),"Accuracy signed byte domain");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {PenetrationAdjustment=-129}),"Penetration signed byte domain");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {Attacker=actor with {Speed=256}}),"Speed retains unsigned byte domain");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {Target=target with {Intelligence=-1}}),"Negative target attribute rejected");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {FatigueCategory=5}),"Unknown fatigue category rejected");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {ActionPenaltyCounter=-1}),"Counter cannot be negative");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {Mode=(OriginalAttackMode)9}),"Unknown mode rejected");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {FatiguePolicy=(MonsterAttackFatiguePolicy)2}),"Unknown fatigue policy rejected");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterScores.Build(input with {Scale=(PhysicalScoreScale)3}),"Unknown scale rejected");
        Reject<OverflowException>(()=>OriginalMonsterScores.Build(input with {Defense=int.MinValue}),"Defense multiplication cannot wrap");
        Reject<OverflowException>(()=>OriginalMonsterScores.Build(input with {ArmorDefense=int.MaxValue}),"Armor multiplication cannot wrap");
        Reject<OverflowException>(()=>OriginalMonsterScores.Scale(int.MaxValue,PhysicalScoreScale.SixtyPercent),"Native-width scale multiplication cannot wrap");
        Reject<OverflowException>(()=>OriginalMonsterScores.Scale(40_000_000,PhysicalScoreScale.SixtyPercent),"Scale preserves native intermediate overflow boundary despite algebraic fraction reduction");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackAdapter.FatiguePolicy(-1),"Native attack kind cannot be negative");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackAdapter.FatiguePolicy(256),"Native attack kind cannot exceed byte");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterAttackAdapter.ScoreScale(256,0),"Actor scale tag remains byte");
        return checks;
    }
}
