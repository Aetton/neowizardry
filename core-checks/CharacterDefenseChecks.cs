using System.Security.Cryptography;
using System.Text.Json;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class CharacterDefenseChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool threw=false;try{action();}catch(Exception){threw=true;}Check(threw,name);}
        foreach(var (current,expected) in new[]{(100,0),(51,0),(50,1),(31,1),(30,2),(16,2),(15,3),(6,3),(5,4),(0,4)})
            Check(OriginalFatigueRules.Category(current,100)==expected,"Stamina-derived fatigue thresholds are independent of encumbrance");
        Check(OriginalFatigueRules.Category(2,3)==0 && OriginalFatigueRules.Category(1,3)==1,"Remaining stamina percentage truncates before fatigue subtraction");
        Reject(()=>OriginalFatigueRules.Category(0,0),"Unresolved zero maximum stamina rejected");
        Reject(()=>OriginalFatigueRules.Category(11,10),"Overfilled stamina rejected");
        Reject(()=>OriginalFatigueRules.Category(-1,10),"Negative stamina rejected");
        Reject(()=>OriginalFatigueRules.Category(int.MaxValue,int.MaxValue),"Stamina percentage multiplication cannot silently wrap");
        var stats=new CharacterDefenseStats(50,0,0,0,false);
        var gear=new PreparedCharacterArmor(new(0,0,0,0,0),0,0);
        var state=new CharacterDefenseState(0,0,false,false,false);
        var effects=new CharacterDefenseEffects(0,0,0,false);
        var neutral=new CharacterDefenseContext(false,false,false);
        CharacterDefenseResult Build(CharacterDefenseStats? s=null,PreparedCharacterArmor? g=null,CharacterDefenseState? st=null,CharacterDefenseEffects? e=null)=>
            OriginalCharacterDefense.Build(s??stats,g??gear,st??state,e??effects);
        Check(Build().Base==0 && Build().DisplayedAverage==0,"Unmodified naked character has no invented armor default");
        foreach(var (speed,expected) in new[]{(0,-2),(9,-2),(10,-1),(19,-1),(20,0),(79,0),(80,1),(89,1),(90,2),(125,2)})
            Check(Build(stats with {Speed=speed}).Contributions.Speed==expected,"Effective Speed defense boundaries");
        foreach(var (skill,expected) in new[]{(0,0),(9,0),(10,1),(99,9),(100,10),(125,12)})
            Check(Build(stats with {Stealth=skill}).Contributions.Stealth==expected,"Effective Stealth contribution truncates by ten");
        Check(Build(stats with {ReflexionEnabled=true}).Base==1,"Enabled zero-point Reflexion still contributes one");
        Check(Build(stats with {Reflexion=125,ReflexionEnabled=false}).Base==0,"Disabled Reflexion skill contributes nothing");
        Check(Build(stats with {Reflexion=125,ReflexionEnabled=true}).Base==7,"Enabled Reflexion advances by twenty skill points");
        var shield=gear with {ShieldArmor=3};
        Check(Build(stats with {Shield=100},shield).Contributions.Shield==6,"Ordinary shield skill bonus capped by shield armor");
        var guarding=Build(stats with {Shield=100},shield,state with {DefensiveAction=true});
        Check(guarding.Contributions.Shield==7 && guarding.Contributions.Defensive==2 && guarding.Base==9,"Resolved defensive action strengthens shield formula and adds separate two-point defense");
        Check(Build(stats with {Shield=14},shield,state with {DefensiveAction=true}).Contributions.Shield==3 &&
            Build(stats with {Shield=15},shield,state with {DefensiveAction=true}).Contributions.Shield==4,"Enhanced shield skill divisor is fifteen");
        Check(Build(stats with {Shield=125},gear with {ShieldArmor=-2}).Contributions.Shield==-2,"Negative shield armor is not amplified by skill");
        Check(Build(g:gear with {OtherArmor=4},e:effects with {NaturalArmorBonus=true}).Base==6,"Resolved natural and item bonuses remain independent");
        foreach(var (category,expected) in new[]{(0,0),(1,0),(2,-1),(3,-2),(4,-4)})
        {
            Check(Build(st:state with {EncumbranceCategory=category}).Contributions.Encumbrance==expected,"Encumbrance defense penalty table");
            Check(Build(st:state with {FatigueCategory=category}).Contributions.Fatigue==expected,"Fatigue-derived defense penalty keeps its separate category");
        }
        var armored=Build(g:gear with {Body=new(2,10,4,-1,0),OtherArmor=1},e:effects with {SpellArmor=3,Penetration=2,Conditions=-1});
        Check(armored.Base==3 && armored.Locations==new BodyArmor(7,15,9,4,5),"Penetration-only and selected body armor do not leak into base defense");
        Check(armored.DisplayedAverage==10,"Display average uses 15/40/30/10/5 body weights and rounding");
        Check(Build(g:gear with {Body=new(-1,0,0,0,0)}).DisplayedAverage==0,"Negative weighted defense below half rounds toward zero after signed bias");
        Check(Build(g:gear with {Body=new(0,0,0,-5,0)}).DisplayedAverage==-1,"Negative half rounds away from zero");
        var noTorso=Build(g:gear with {Body=new(2,0,4,-1,0),OtherArmor=1},e:effects with {SpellArmor=3,Penetration=2,Conditions=-1});
        Check(noTorso.Base==armored.Base && noTorso.Locations.Head==armored.Locations.Head && noTorso.Locations.Torso==5,"Changing torso armor affects torso penetration rather than all-body avoidance");
        var incapacitated=Build(g:gear with {OtherArmor=100,Body=new(7,0,0,0,0)},st:state with {Incapacitated=true});
        Check(incapacitated.Base==-5 && incapacitated.Locations.Head==2,"Incapacitation clamps base first and preserves location armor");
        Check(Build(g:gear with {OtherArmor=-10},st:state with {Incapacitated=true}).Base==-10,"Incapacitation never improves already worse base defense");
        var suppressed=Build(stats with {Speed=100,Stealth=100,Shield=100,Reflexion=100,ReflexionEnabled=true},
            gear with {ShieldArmor=3,OtherArmor=2,Body=new(1,0,0,0,0)},
            state with {DefensiveAction=true,SuppressDerivedBonuses=true},effects with {SpellArmor=20,Penetration=20,Conditions=20,NaturalArmorBonus=true});
        Check(suppressed.Base==5 && suppressed.Locations.Head==6 && suppressed.Contributions.Shield==3 && suppressed.Contributions.Penetration==0,
            "Suppressed derived bonuses retain raw shield/accessory/body armor without skill or spell bonuses");
        int[] counts=new int[5];for(int draw=0;draw<100;draw++)counts[(int)OriginalCharacterDefense.SelectLocation(draw)]++;
        Check(counts.SequenceEqual(new[]{15,40,30,10,5}),"Every character hit-location draw matches the original table");
        foreach(var (draw,location) in new[]{(14,CharacterHitLocation.Head),(15,CharacterHitLocation.Torso),(54,CharacterHitLocation.Torso),
            (55,CharacterHitLocation.Legs),(84,CharacterHitLocation.Legs),(85,CharacterHitLocation.Hands),(94,CharacterHitLocation.Hands),(95,CharacterHitLocation.Feet)})
            Check(OriginalCharacterDefense.SelectLocation(draw)==location,"Hit-location cumulative boundaries are exclusive");
        var exposed=Build(stats with {Speed=90,Shield=100},shield with {Body=new(0,10,0,0,0)});
        var adjusted=OriginalCharacterDefense.Against(exposed,CharacterHitLocation.Torso,OriginalAttackMode.Swing,new(true,true,true));
        Check(adjusted.ContextAdjustment==-16 && adjusted.Hit==-8 && adjusted.Penetration==-14,"Penetration preserves two native dynamic adjustments while hit uses one");
        foreach(var mode in Enum.GetValues<OriginalAttackMode>())
        {
            int expected=mode is OriginalAttackMode.Throw or OriginalAttackMode.Lash or OriginalAttackMode.Shoot?0:-4;
            Check(OriginalCharacterDefense.Against(Build(),CharacterHitLocation.Head,mode,new(false,false,true)).ContextAdjustment==expected,
                "Melee-vulnerability exceptions cover Throw Lash Shoot, including Kick penalty");
        }
        Check(OriginalCharacterDefense.Against(incapacitated,CharacterHitLocation.Head,OriginalAttackMode.Swing,new(false,false,true)).ContextAdjustment==0,
            "Incapacitated flag suppresses the extra melee-vulnerability deduction");
        Check(OriginalCharacterDefense.Against(Build(stats with {Speed=0},gear with {ShieldArmor=-2}),CharacterHitLocation.Head,
            OriginalAttackMode.Swing,new(true,false,false)).ContextAdjustment==0,"Removing positive speed/shield bonuses never removes penalties");
        var target=OriginalCharacterDefense.Against(armored,CharacterHitLocation.Torso,OriginalAttackMode.Swing,neutral);
        Check(target.Hit==3 && target.Penetration==15 && target.Hit!=armored.DisplayedAverage,"Displayed average is not substituted for either native threshold");
        var attrs=new PhysicalCombatAttributes(50,50,50,50);
        var input=new OrdinaryPhysicalScoreInput(150,0,0,0,0,attrs,attrs,OriginalAttackMode.Swing,target.Hit,target.Penetration);
        var scores=OriginalPhysicalScores.OrdinaryCharacter(input);
        Check(scores.HitScore==135 && scores.PenetrationScore==75,"Defense producer composes with existing hit and penetration builders");
        Check(OriginalMeleeStrike.Resolve(input,new(0,1,6),0,0,0,79,[]).Outcome==PhysicalStrikeOutcome.NotPenetrated,
            "Character body armor can stop a melee contact using recovered defense facts");
        var shootTarget=OriginalCharacterDefense.Against(armored,CharacterHitLocation.Torso,OriginalAttackMode.Shoot,neutral);
        var shotScores=OriginalPhysicalScores.OrdinaryCharacter(input with {Mode=OriginalAttackMode.Shoot,Defense=shootTarget.Hit,ArmorDefense=shootTarget.Penetration});
        Check(OriginalProjectileStrike.Resolve(new(OriginalAttackMode.Shoot,ProjectileContactKind.Contact,shotScores.PenetrationScore,new(0,1,6)),85,[]).Outcome==ProjectileStrikeOutcome.NotPenetrated,
            "Same actual target/location facts drive separate projectile penetration");
        var restored=JsonSerializer.Deserialize<CharacterDefenseResult>(JsonSerializer.Serialize(armored))!;
        Check(OriginalCharacterDefense.Against(restored,CharacterHitLocation.Torso,OriginalAttackMode.Swing,neutral)==target,"Resolved character defense persists without native memory or invented stat defaults");

        ItemDefinition Item(int id,int type,int armor)
        {
            var raw=new byte[269];raw[0x62]=unchecked((byte)(sbyte)armor);
            return new(id,"synthetic",type,0,0,0,0,Convert.ToHexString(SHA256.HashData(raw)),0,0,0,0,0,0,0,0,0,false,0,new(0,0,0,0,0,0,Convert.ToHexString(raw)));
        }
        var selected=new CharacterDefenseEquipment(Head:Item(1,8,2),Torso:Item(2,6,10),Legs:Item(3,7,4),Hands:Item(4,9,-1),
            Feet:Item(5,10,0),Cloak:Item(6,12,1),Accessory1:Item(7,11,2),Accessory2:Item(8,11,-3),MainHand:Item(9,0,1),OffHand:Item(10,5,3));
        Check(OriginalDefenseAdapter.Prepare(selected)==new PreparedCharacterArmor(new(2,10,4,-1,0),3,1),"Explicit active equipment maps body pieces separately and groups shield/nonbody contributions");
        Check(OriginalDefenseAdapter.Prepare(new()).Body==new BodyArmor(0,0,0,0,0),"Absent armor means empty slots rather than missing-metadata repair");
        foreach(int value in new[]{-128,127})Check(OriginalDefenseAdapter.Prepare(new(Head:Item(1,8,value))).Body.Head==value,"Native armor byte is signed");
        Reject(()=>OriginalDefenseAdapter.Prepare(selected with {Head=selected.Head! with {RecordSha256=new string('0',64)}}),"Armor import boundary validates native record hash");
        Reject(()=>OriginalDefenseAdapter.Prepare(selected with {Head=selected.Head! with {Instance=null}}),"No native metadata means no inferred item armor");
        foreach(int value in new[]{-1,126})Reject(()=>Build(stats with {Stealth=value}),"Effective defense skills use supported domain");
        Reject(()=>Build(st:state with {EncumbranceCategory=5}),"Unknown encumbrance rejected");
        Reject(()=>Build(st:state with {FatigueCategory=-1}),"Invalid fatigue category rejected independently");
        Reject(()=>Build(e:effects with {Penetration=128}),"Signed defense-effect byte limit checked");
        foreach(int draw in new[]{-1,100})Reject(()=>OriginalCharacterDefense.SelectLocation(draw),"Character location draw must be zero-based d100");
        Reject(()=>OriginalCharacterDefense.Against(armored,(CharacterHitLocation)5,OriginalAttackMode.Swing,neutral),"Unknown location rejected");
        Reject(()=>OriginalCharacterDefense.Against(armored,CharacterHitLocation.Head,(OriginalAttackMode)99,neutral),"Unknown mode rejected");
        Reject(()=>Build(g:gear with {OtherArmor=int.MaxValue},e:effects with {NaturalArmorBonus=true}),"Defense accumulation overflow rejected");
        Reject(()=>Build(g:gear with {Body=new(int.MaxValue,0,0,0,0)}),"Display weighted-product overflow rejected");
        return checks;
    }
}
