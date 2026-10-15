using NeoWizardry.Core.Rules;

static class PhysicalScoreChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,name);}
        var actor=new PhysicalCombatAttributes(100,100,100,100);
        var target=new PhysicalCombatAttributes(80,80,80,80);
        var input=new OrdinaryPhysicalScoreInput(100,2,3,1,-1,actor,target,OriginalAttackMode.Thrust,4,6,3);
        var score=OriginalPhysicalScores.OrdinaryCharacter(input);
        Check(score==new PhysicalScoreBreakdown(8,20,20,0,85,73,93),"Hand, effect, mode, defense and attributes combine in the native order");
        var modern=OriginalPhysicalScores.OrdinaryCharacter(input with { ModernWeapon=true });
        Check(modern.HitLoadPenalty==10 && modern.PenetrationLoadPenalty==0 && modern.HitScore==83 && modern.PenetrationScore==113,
            "Modern weapons halve hit load penalty and bypass penetration load penalty");
        Check(OriginalPhysicalScores.OrdinaryCharacter(input with { LoadCategory=1,ModernWeapon=true }).HitLoadPenalty==2,
            "Odd modern hit load penalty truncates before subtraction");
        var repeated=OriginalPhysicalScores.OrdinaryCharacter(input with { ActionPenaltyCounter=1 });
        Check(repeated.RepetitionPenalty==12 && repeated.HitScore==61 && repeated.PenetrationScore==93,
            "Context counter penalty affects hit builder but not penetration builder");
        var equal=input with { Attacker=target,Target=target,LoadCategory=0 };
        var near=OriginalPhysicalScores.OrdinaryCharacter(equal with { Attacker=target with { Intelligence=71 } });
        var negative=OriginalPhysicalScores.OrdinaryCharacter(equal with { Attacker=target with { Intelligence=70 } });
        Check(near.AttributeContribution==0 && negative.AttributeContribution==-1,"Negative attribute difference truncates toward zero rather than floor");
        Check(OriginalPhysicalScores.OrdinaryCharacter(equal with { Attacker=target with { Senses=89 } }).AttributeContribution==0 &&
            OriginalPhysicalScores.OrdinaryCharacter(equal with { Attacker=target with { Senses=90 } }).AttributeContribution==1,
            "Positive attribute contribution has integer ten-point steps");
        Check(OriginalPhysicalScores.OrdinaryCharacter(equal with { BaseAttackRating=250 }).HitScore>100 &&
            OriginalPhysicalScores.OrdinaryCharacter(equal with { Defense=100 }).HitScore<0,
            "Raw scores are not prematurely clamped to percentages");
        Check(OriginalPhysicalScores.OrdinaryCharacter(equal with { Target=new(255,255,255,255) }).AttributeContribution==-70,
            "Monster target attributes retain native byte range beyond player cap");
        Reject(()=>OriginalPhysicalScores.OrdinaryCharacter(input with { Attacker=actor with { Speed=126 } }),"Player attributes require resolved cap");
        Reject(()=>OriginalPhysicalScores.OrdinaryCharacter(input with { Target=target with { Intelligence=256 } }),"Target attributes cannot exceed native byte");
        Reject(()=>OriginalPhysicalScores.OrdinaryCharacter(input with { AccuracyAdjustment=128 }),"Effect adjustment preserves signed byte domain");
        Reject(()=>OriginalPhysicalScores.OrdinaryCharacter(input with { LoadCategory=5 }),"Unknown load category rejected");
        Reject(()=>OriginalPhysicalScores.OrdinaryCharacter(input with { HandToHit=int.MaxValue }),"Score overflow rejected without machine wrapping");
        Reject(()=>OriginalPhysicalScores.OrdinaryCharacter(input with { Mode=(OriginalAttackMode)99 }),"Unknown attack mode rejected in builder");
        foreach(var row in new[]{(75,50),(76,49),(91,42),(156,23),(259,0),(260,0)})
            Check(OriginalPhysicalScores.RepetitionPenalty(0,row.Item1,1)==row.Item2,"Nonlinear penalty has exact boundary and perfect-power behavior");
        Check(OriginalPhysicalScores.RepetitionPenalty(0,int.MinValue,0)==0 &&
            OriginalPhysicalScores.RepetitionPenalty(0,int.MinValue,1)==50 &&
            OriginalPhysicalScores.RepetitionPenalty(125,int.MaxValue,int.MaxValue)==50,"Counter bypass and extreme values cannot overflow reserve calculation");
        // Independent floating implementation of the recovered expression, over every unsaturated integer reserve.
        bool powersAgree=true;
        for(int reserve=0;reserve<=185;reserve++)
            powersAgree &= OriginalPhysicalScores.RepetitionPenalty(0,reserve+75,1)==Math.Max(0,(int)(50-Math.Pow(reserve,0.75)));
        Check(powersAgree,"Integer root implementation matches all finite nonlinear penalty inputs before saturation");
        Reject(()=>OriginalPhysicalScores.RepetitionPenalty(100,100,-1),"Negative context counter rejected");
        Reject(()=>OriginalPhysicalScores.RepetitionPenalty(126,100,0),"Invalid speed rejected even when context penalty is bypassed");
        foreach(int threshold in new[]{-50,0,1,5,50,99,100,150})
        {
            int count=Enumerable.Range(0,100).Count(draw=>OriginalPhysicalScores.Penetrates(threshold,draw));
            Check(count==Math.Clamp(threshold,0,100),"Every d100 outcome respects inclusive one-based penetration comparison");
        }
        Check(OriginalPhysicalScores.Penetrates(50,49) && !OriginalPhysicalScores.Penetrates(50,50),"Penetration succeeds at equality");
        Check(OriginalPhysicalScores.Penetrates(-100,99,true),"Explicit original automatic-penetration context bypasses comparison");
        Reject(()=>OriginalPhysicalScores.Penetrates(100,-1),"Negative d100 draw rejected");
        Reject(()=>OriginalPhysicalScores.Penetrates(100,100,true),"Automatic context does not permit invalid draw");
        var low=new SingleWeaponInput(0,0,0,0,[new(OriginalProfession.Mage,1)],0,4);
        Check(OriginalCombatRules.SingleWeapon(low).BaseAttackRating==20,"Base attack rating retains load contribution inside division");
        var trained=low with { Dexterity=125,CombatSkill=100,WeaponSkill=100,LoadCategory=0 };
        Check(OriginalCombatRules.SingleWeapon(trained).BaseAttackRating==147,"Free-hand base rating uses half Dexterity and twice weighted skill");
        Check(OriginalCombatRules.SingleWeapon(trained with { History=[new(OriginalProfession.Fighter,50)] }).BaseAttackRating==147,
            "Class combat experience does not enter ordinary base attack rating");
        return checks;
    }
}
