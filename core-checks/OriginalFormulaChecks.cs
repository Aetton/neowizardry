using NeoWizardry.Core.Rules;

static class OriginalFormulaChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,name);}
        PhysicalAttackBudget Budget(int level,OriginalProfession profession,int combat=100,int weapon=100,int dex=125,int speed=125,int initiative=0,int load=0,bool modern=false)=>
            OriginalCombatRules.SingleWeapon(new(dex,speed,combat,weapon,[new(profession,level)],initiative,load,modern));
        Check(Budget(24,OriginalProfession.Rogue).Attacks==2 && Budget(25,OriginalProfession.Rogue).Attacks==3,"Exact integer rogue level-25 threshold resolves decimal-guide discrepancy");
        Check(Budget(18,OriginalProfession.Fighter).Attacks==2 && Budget(19,OriginalProfession.Fighter).Attacks==3,"Fighter ordinary skill threshold");
        Check(Budget(37,OriginalProfession.Mage).Attacks==2 && Budget(38,OriginalProfession.Mage).Attacks==3,"Caster threshold");
        Check(Budget(14,OriginalProfession.Samurai,100,125).Attacks==2 && Budget(15,OriginalProfession.Samurai,100,125).Attacks==3,"Primary weapon threshold");
        Check(Budget(16,OriginalProfession.Fighter,125,100).Attacks==2 && Budget(17,OriginalProfession.Fighter,125,100).Attacks==3,"Primary combat skill threshold");
        Check(Budget(1,OriginalProfession.Mage,0,0,0,0).Attacks==1 && Budget(1000,OriginalProfession.Fighter,125,125).Attacks==3,"One minimum and three hard maximum attacks in this character branch");
        Check(OriginalCombatRules.CombatExperience([new(OriginalProfession.Fighter,10),new(OriginalProfession.Mage,5)])==50,"Combat experience accumulates weighted class levels");
        foreach(var p in Enum.GetValues<OriginalProfession>())
            Check(OriginalCombatRules.CombatExperience([new(p,1)])==(p<=OriginalProfession.Monk?4:p<=OriginalProfession.Bard?3:2),"All profession combat groups");
        Check(Budget(1,OriginalProfession.Fighter,0,0,0,0,0,1).LoadPenalty==-15 && Budget(1,OriginalProfession.Fighter,0,0,0,0,0,1,true).LoadPenalty==-7,"Modern weapon load penalty halves toward zero");
        Check(Budget(1,OriginalProfession.Fighter,0,0,0,125,0,4).LoadPenalty==-120,"Heavy load contribution preserved");
        Check(Budget(19,OriginalProfession.Fighter,0,0,0,125).SwingMetric==67 && Budget(19,OriginalProfession.Fighter,0,0,0,125).MaximumSwings==2,"Second swing budget starts at integer metric 67");
        Check(Budget(18,OriginalProfession.Fighter,0,0,0,125).MaximumSwings==1,"Below second swing boundary");
        Check(Budget(25,OriginalProfession.Fighter,0,0,0,100,10).MaximumSwings==3 && Budget(25,OriginalProfession.Fighter,0,0,0,100,9).MaximumSwings==2,"Third swing threshold includes resolved weapon initiative");
        Check(Budget(1,OriginalProfession.Mage,0,0,0,0,-128,4).MaximumSwings==1,"Negative swing metric retains minimum budget");
        Reject(()=>Budget(1,OriginalProfession.Mage,126),"Unresolved uncapped skill rejected");
        Reject(()=>Budget(1,OriginalProfession.Mage,100,100,125,125,0,5),"Unknown load category rejected");
        Reject(()=>OriginalCombatRules.CombatExperience([new(OriginalProfession.Fighter,1),new(OriginalProfession.Fighter,2)]),"Repeated class entries rejected");
        Reject(()=>OriginalCombatRules.CombatExperience([new(OriginalProfession.Mage,0)]),"Empty class level entry rejected");

        var safe=new CastingRiskInput(100,100,1,2,1,20);
        var risk=OriginalMagicRules.OrdinaryCastingRisk(safe);
        Check(risk.EffectiveSkill==100 && risk.RequiredSkill==7 && risk.FailureThreshold==0,"Casting skill and threshold use original integer weights");
        var weighted=OriginalMagicRules.OrdinaryCastingRisk(safe with {BookSkill=20,RealmSkill=100,Power=7});
        Check(weighted.EffectiveSkill==84 && weighted.RequiredSkill==50 && weighted.FailureThreshold==0,"Realm skill has fourfold weight in resolved casting skill");
        var severe=OriginalMagicRules.OrdinaryCastingRisk(new(0,0,7,50,7,-1));
        Check(severe.RequiredSkill==110 && severe.SkillFailure==70 && severe.LevelPenalty==175 && severe.FailureThreshold==245,"Risk threshold above 100 is retained before backfire selection");
        var cap=OriginalMagicRules.OrdinaryCastingRisk(new(0,0,7,int.MaxValue,1,50));
        Check(cap.RequiredSkill==15 && cap.SkillFailure==70,"Spell difficulty index caps before power scaling");
        var low=OriginalMagicRules.OrdinaryCastingRisk(new(100,100,3,6,2,5));
        Check(low.SkillFailure==0 && low.LevelPenalty==3,"Level penalty survives sufficient skill");
        foreach(var pair in new[]{(0,CastingRiskBand.DarkGreen),(1,CastingRiskBand.LightGreen),(5,CastingRiskBand.LightGreen),(6,CastingRiskBand.Yellow),(15,CastingRiskBand.Yellow),(16,CastingRiskBand.Orange),(40,CastingRiskBand.Orange),(41,CastingRiskBand.Red)})
            Check(OriginalMagicRules.RiskBand(pair.Item1)==pair.Item2,"Casting color boundary");
        Check(OriginalMagicRules.ResolveCasting(0,null,true)==CastingOutcome.Success,"Safe casts require no failure draw");
        Check(OriginalMagicRules.ResolveCasting(5,0,true)==CastingOutcome.Fizzle,"Five-percent risk cannot backfire in ordinary branch");
        Check(OriginalMagicRules.ResolveCasting(6,1,true)==CastingOutcome.Backfire && OriginalMagicRules.ResolveCasting(6,2,true)==CastingOutcome.Fizzle && OriginalMagicRules.ResolveCasting(6,6,true)==CastingOutcome.Success,"Strict backfire and failure boundaries share one zero-based roll");
        Check(OriginalMagicRules.ResolveCasting(245,80,true)==CastingOutcome.Backfire && OriginalMagicRules.ResolveCasting(245,81,true)==CastingOutcome.Fizzle,"Unclamped risk changes backfire interval");
        Check(OriginalMagicRules.ResolveCasting(245,0,false)==CastingOutcome.Fizzle,"Caller can suppress backfire without removing failure");
        bool counts=true;
        for(int threshold=1;threshold<=300;threshold++)
        {
            var outcomes=Enumerable.Range(0,100).Select(roll=>OriginalMagicRules.ResolveCasting(threshold,roll,true)).ToArray();
            counts &= outcomes.Count(o=>o!=CastingOutcome.Success)==Math.Min(100,threshold) &&
                outcomes.Count(o=>o==CastingOutcome.Backfire)==(threshold<=5?0:Math.Min(100,threshold/3));
        }
        Check(counts,"All d100 outcomes agree with failure/backfire interval sizes for 300 thresholds");
        Reject(()=>OriginalMagicRules.ResolveCasting(0,0,true),"Unneeded safe-cast roll rejected");
        Reject(()=>OriginalMagicRules.ResolveCasting(1,null,true),"Missing risky-cast roll rejected");
        Reject(()=>OriginalMagicRules.ResolveCasting(5,100,true),"One-based d100 input rejected");
        Reject(()=>OriginalMagicRules.OrdinaryCastingRisk(safe with {Power=8}),"Automatic power sentinel is not an executable power");
        Reject(()=>OriginalMagicRules.OrdinaryCastingRisk(safe with {SpellLevel=0}),"Special level-zero metadata not promoted to ordinary player spell");
        Check(OriginalMagicRules.PowerCastBonus(false,100)==0 && OriginalMagicRules.PowerCastBonus(true,0)==1 && OriginalMagicRules.PowerCastBonus(true,100)==26 && OriginalMagicRules.PowerCastBonus(true,125)==32,"Power Cast activation and integer steps");
        Check(OriginalMagicRules.ScaleAmount(50,26)==63 && OriginalMagicRules.ScaleAmount(7,13)==7,"Power Cast percentage truncates before addition");
        Check(OriginalMagicRules.BaseEffectStrength(20,3,6,7,false)==20 && OriginalMagicRules.BaseEffectStrength(20,3,6,7,true)==17,"Base effect strength and all-enemies adjustment");
        Check(OriginalMagicRules.ScaleAmount(17,OriginalMagicRules.PowerCastBonus(true,100)/2)==19,"Half Power Cast strengthens base effect at the observed call site");
        Reject(()=>OriginalMagicRules.ScaleAmount(int.MaxValue,100),"Clean implementation rejects arithmetic overflow");

        var none=OriginalMagicRules.ResistanceBounds(10,20,0);
        Check(none.Pressure==-30 && OriginalMagicRules.ResistDamage(100,none,null,null)==new SpellDamageResult(100,0),"Nonpositive resistance pressure consumes no draws");
        var odd=OriginalMagicRules.ResistanceBounds(10,10,3);
        Check(odd==new ResistanceRollBounds(3,2,2,2),"Odd pressure uses baseline and two exclusive bounds");
        var even=OriginalMagicRules.ResistanceBounds(10,10,4);
        Check(even==new ResistanceRollBounds(4,2,3,2),"Even pressure produces unequal random bounds");
        Check(OriginalMagicRules.ResistDamage(101,even,2,1)==new SpellDamageResult(96,5),"Damage subtracts floored reduction rather than flooring remaining fraction");
        Check(OriginalMagicRules.ResistDamage(1,OriginalMagicRules.ResistanceBounds(10,10,99),49,0).Damage==1,"Sub-100 resistance reduction retains one-point damage");
        Check(OriginalMagicRules.ResistDamage(101,OriginalMagicRules.ResistanceBounds(10,10,200),0,0).Damage==0,"Reduction percentage caps at full damage");
        var level=OriginalMagicRules.ResistanceBounds(20,10,0);
        Check(level.Pressure==30 && OriginalMagicRules.ResistanceBounds(20,11,0).Pressure==27,"Resolved effect strength lowers resistance pressure by three per point");
        Reject(()=>OriginalMagicRules.ResistDamage(10,even,3,0),"Exclusive resistance draw boundary enforced");
        Reject(()=>OriginalMagicRules.ResistDamage(10,even,null,null),"Two resistance draws required for positive pressure");
        Reject(()=>OriginalMagicRules.ResistDamage(10,none,0,0),"No RNG fabricated for bypassed reduction");
        Reject(()=>OriginalMagicRules.ResistDamage(10,even with {Baseline=3},0,0),"Inconsistent caller-supplied roll bounds rejected");
        Reject(()=>OriginalMagicRules.ResistDamage(9999,none,null,null),"Original sentinel must be represented explicitly by a higher layer");
        bool bounded=true;
        for(int pressure=1;pressure<=210;pressure++)
        {
            var bounds=OriginalMagicRules.ResistanceBounds(1,1,pressure);
            foreach(var rolls in new[]{(0,0),(bounds.FirstExclusive-1,bounds.SecondExclusive-1)})
            {
                var value=OriginalMagicRules.ResistDamage(37,bounds,rolls.Item1,rolls.Item2);
                bounded &= value.Damage is >=0 and <=37 && value.ReductionPercent is >=0 and <=100;
            }
        }
        Check(bounded,"Resistance extremes remain within damage bounds across 210 pressures");
        return checks;
    }
}
