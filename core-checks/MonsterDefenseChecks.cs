using System.Text.Json;
using NeoWizardry.Core.Rules;

static class MonsterDefenseChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject<T>(Action action,string name) where T:Exception
        {
            bool rejected=false;try{action();}catch(T){rejected=true;}Check(rejected,name);
        }
        var weights=new MonsterLocationValues(10,20,0,30,15,5,20);
        var zero=new MonsterLocationValues(0,0,0,0,0,0,0);
        var modes=new MonsterModeArmor(0,0,0,0,0,0,0,0,0);
        var definition=new MonsterDefenseDefinition(3,weights,zero,modes);
        var state=new MonsterDefenseState(0,0,0,0,false);
        var neutral=new MonsterDefenseContext(false,false,false,false);
        TargetDefenseThresholds Against(MonsterDefenseDefinition? d=null,MonsterDefenseState? s=null,
            MonsterHitLocation l=MonsterHitLocation.Zone0,OriginalAttackMode m=OriginalAttackMode.Swing,MonsterDefenseContext? c=null)=>
            OriginalMonsterDefense.Against(d??definition,s??state,l,m,c??neutral);
        int[] counts=new int[7];
        for(int draw=0;draw<100;draw++)
        {
            var selected=OriginalMonsterDefense.SelectLocation(weights,draw);
            int expected=draw<10?0:draw<30?1:draw<60?3:draw<75?4:draw<80?5:6;
            Check((int)selected.Location==expected && !selected.UsedFallback,"Monster draw preserves seven definition-dependent zones and skips zero weights");
            counts[(int)selected.Location]++;
        }
        Check(counts.SequenceEqual(new[]{10,20,0,30,15,5,20}),"Full d100 distribution matches native monster definition rather than character weights");
        Check(OriginalMonsterDefense.SelectLocation(zero,99)==new MonsterLocationSelection(MonsterHitLocation.Zone3,true),"Empty weights report native zone-three fallback");
        var shortWeights=zero with {Zone0=1};
        Check(!OriginalMonsterDefense.SelectLocation(shortWeights,0).UsedFallback && OriginalMonsterDefense.SelectLocation(shortWeights,1).UsedFallback,
            "Underweight distribution falls back only for uncovered draws");
        Check(OriginalMonsterDefense.SelectLocation(zero with {Zone0=255,Zone6=255},99).Location==MonsterHitLocation.Zone0,
            "Overweight byte distribution uses native cumulative comparison without normalization");
        Check(Against()==new TargetDefenseThresholds(7,10,0),"Neutral hit uses ten minus AC while penetration cancels AC");
        Check(Against(definition with {ArmorClass=-128})==new TargetDefenseThresholds(138,10,0),"Negative native AC remains signed");
        Check(Against(definition with {ArmorClass=127})==new TargetDefenseThresholds(-117,10,0),"Positive native AC is not clamped");
        Check(Against(s:state with {Incapacitated=true})==new TargetDefenseThresholds(-5,-2,0),"Incapacitation cap retains definition AC term in penetration");
        Check(Against(definition with {ArmorClass=20},state with {Incapacitated=true})==new TargetDefenseThresholds(-10,10,0),"Incapacitation does not improve already lower base");
        foreach(var (category,adjustment) in new[]{(0,0),(1,0),(2,1),(3,2),(4,4)})
            Check(Against(s:state with {FatigueCategory=category})==new TargetDefenseThresholds(7+adjustment,10+2*adjustment,adjustment),
                "Monster fatigue contribution is positive and integer-divided before two context additions");
        Check(Against(s:state with {ContextAdjustment1=-128,ContextAdjustment2=127})==new TargetDefenseThresholds(6,8,-1),"Signed context bytes add without unsigned reinterpretation");
        Check(Against(s:state with {PenetrationAdjustment=-128})==new TargetDefenseThresholds(7,-118,0),"Runtime armor adjustment affects only penetration");
        var armor=new MonsterLocationValues(-3,2,5,7,11,13,17);
        var varied=definition with {LocationArmor=armor,ModeArmor=new(-4,-3,-2,-1,0,1,2,3,4)};
        foreach(var mode in Enum.GetValues<OriginalAttackMode>())
        foreach(var location in Enum.GetValues<MonsterHitLocation>())
        {
            int[] expectedLocations={-3,2,5,7,11,13,17};
            int expected=14-(int)mode-expectedLocations[(int)location];
            Check(Against(varied,l:location,m:mode)==new TargetDefenseThresholds(7,expected,0),"All nine mode and seven zone indices select independent signed penetration terms");
        }
        var combat=neutral with {CombatActive=true};
        Check(Against(c:neutral with {TwoPointPenalty=true,TwoPointBonus=true,MeleeVulnerability=true})==Against(),"Combat-only predicates are ignored outside combat");
        Check(Against(c:combat with {TwoPointPenalty=true})==new TargetDefenseThresholds(5,6,-2),"Resolved two-point penalty applies twice to penetration");
        Check(Against(c:combat with {TwoPointBonus=true})==new TargetDefenseThresholds(9,14,2),"Resolved two-point bonus applies twice to penetration");
        Check(Against(c:combat with {TwoPointPenalty=true,TwoPointBonus=true})==Against(),"Independent context penalty and bonus can cancel");
        foreach(var mode in Enum.GetValues<OriginalAttackMode>())
        {
            int adjustment=new[]{0,1,2,3,5,6}.Contains((int)mode)?-4:0;
            Check(Against(m:mode,c:combat with {MeleeVulnerability=true})==new TargetDefenseThresholds(7+adjustment,10+2*adjustment,adjustment),"Melee vulnerability includes Kick and excludes Throw, Lash and Shoot");
            Check(Against(s:state with {Incapacitated=true},m:mode,c:combat with {MeleeVulnerability=true})==new TargetDefenseThresholds(-5,-2,0),"Incapacitation suppresses vulnerability independently of mode");
        }
        var snapshot=JsonSerializer.Deserialize<MonsterDefenseDefinition>(JsonSerializer.Serialize(varied));
        Check(snapshot==varied && Against(snapshot,l:MonsterHitLocation.Zone6,m:OriginalAttackMode.Shoot).Penetration==-11,
            "Derived calculation definition roundtrip preserves all zone/mode inputs");
        var thresholds=Against(varied,state with {ContextAdjustment1=-2},MonsterHitLocation.Zone3,OriginalAttackMode.Thrust);
        var actor=new PhysicalCombatAttributes(50,50,50,50);
        var scores=OriginalPhysicalScores.OrdinaryCharacter(new(100,0,0,0,0,actor,actor,OriginalAttackMode.Thrust,thresholds.Hit,thresholds.Penetration));
        Check(thresholds==new TargetDefenseThresholds(5,2,-2) && scores.HitScore==65 && scores.PenetrationScore==100,
            "Monster defense composes with character hit and penetration builders without AC conversion twice");
        Check(OriginalPhysicalScores.Penetrates(scores.PenetrationScore,99),"Connected penetration keeps inclusive d100 threshold");
        var swingDefense=Against(l:MonsterHitLocation.Zone6);
        var swingInput=new OrdinaryPhysicalScoreInput(100,0,0,0,0,actor,actor,OriginalAttackMode.Swing,swingDefense.Hit,swingDefense.Penetration);
        Check(OriginalMeleeStrike.Resolve(swingInput,new(0,1,6),0,0,0,49,[5]).Damage?.Damage==6 &&
            OriginalMeleeStrike.Resolve(swingInput,new(0,1,6),0,0,0,50,[]).Outcome==PhysicalStrikeOutcome.NotPenetrated,
            "Connected monster melee penetration equality decides whether damage dice exist");
        var shootDefense=Against(m:OriginalAttackMode.Shoot);
        var shot=OriginalPhysicalScores.OrdinaryCharacter(swingInput with {Mode=OriginalAttackMode.Shoot,Defense=shootDefense.Hit,ArmorDefense=shootDefense.Penetration});
        Check(OriginalProjectileStrike.Resolve(new(OriginalAttackMode.Shoot,ProjectileContactKind.Contact,shot.PenetrationScore,new(0,1,6)),59,[5]).Damage?.Damage==6 &&
            OriginalProjectileStrike.Resolve(new(OriginalAttackMode.Shoot,ProjectileContactKind.Contact,shot.PenetrationScore,new(0,1,6)),60,[]).Outcome==ProjectileStrikeOutcome.NotPenetrated,
            "Connected monster shooting uses fresh penetration threshold rather than melee contact rules");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterDefense.SelectLocation(weights,-1),"Reject negative location draw");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterDefense.SelectLocation(weights,100),"Reject out-of-range location draw");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterDefense.SelectLocation(weights with {Zone6=-1},0),"Validate even unselected negative weights");
        Reject<ArgumentOutOfRangeException>(()=>OriginalMonsterDefense.SelectLocation(weights with {Zone6=256},0),"Weights retain unsigned byte domain");
        Reject<ArgumentNullException>(()=>OriginalMonsterDefense.SelectLocation(null!,0),"Reject absent location distribution");
        Reject<ArgumentOutOfRangeException>(()=>Against(definition with {ArmorClass=128}),"AC signed byte domain");
        Reject<ArgumentOutOfRangeException>(()=>Against(definition with {LocationArmor=zero with {Zone6=-129}}),"Validate unselected zone armor");
        Reject<ArgumentOutOfRangeException>(()=>Against(definition with {ModeArmor=modes with {Shoot=128}}),"Validate unselected mode armor");
        Reject<ArgumentOutOfRangeException>(()=>Against(s:state with {PenetrationAdjustment=128}),"Runtime armor signed domain");
        Reject<ArgumentOutOfRangeException>(()=>Against(s:state with {ContextAdjustment1=-129}),"First context signed domain");
        Reject<ArgumentOutOfRangeException>(()=>Against(s:state with {ContextAdjustment2=128}),"Second context signed domain");
        Reject<ArgumentOutOfRangeException>(()=>Against(s:state with {FatigueCategory=5}),"Scoped ordinary fatigue category required");
        Reject<ArgumentOutOfRangeException>(()=>Against(l:(MonsterHitLocation)7),"Character and invalid location indices are not silently folded");
        Reject<ArgumentOutOfRangeException>(()=>Against(m:(OriginalAttackMode)9),"Unknown mode rejected");
        Reject<ArgumentNullException>(()=>OriginalMonsterDefense.Against(null!,state,0,0,neutral),"Definition required");
        Reject<ArgumentNullException>(()=>OriginalMonsterDefense.Against(definition,null!,0,0,neutral),"State required");
        Reject<ArgumentNullException>(()=>OriginalMonsterDefense.Against(definition,state,0,0,null!),"Context required");
        Reject<ArgumentNullException>(()=>Against(definition with {ModeArmor=null!}),"Mode armor required");
        return checks;
    }
}
