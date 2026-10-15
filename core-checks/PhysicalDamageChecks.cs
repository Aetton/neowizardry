using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class PhysicalDamageChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,name);}
        Check(OriginalPhysicalDamage.PrimaryHand(50,50,50,0,PhysicalStrengthScaling.Full,0)==new PhysicalHandBonuses(0,0),"Neutral attributes have no hand bonuses");
        Check(OriginalPhysicalDamage.PrimaryHand(125,125,125,2,PhysicalStrengthScaling.Full,0)==new PhysicalHandBonuses(21,150),"Strength, Dexterity and Senses contribute with their own thresholds");
        Check(OriginalPhysicalDamage.PrimaryHand(49,49,29,0,PhysicalStrengthScaling.Full,0)==new PhysicalHandBonuses(0,-1),"Sub-ten negative hand contributions truncate toward zero");
        Check(OriginalPhysicalDamage.PrimaryHand(0,0,0,0,PhysicalStrengthScaling.Full,0)==new PhysicalHandBonuses(-13,-50),"Low attributes retain negative hand modifiers");
        Check(OriginalPhysicalDamage.PrimaryHand(125,50,50,0,PhysicalStrengthScaling.Half,0)==new PhysicalHandBonuses(3,75),"Half-strength weapons halve the percentage and use a twenty-point hit denominator");
        Check(OriginalPhysicalDamage.PrimaryHand(50,50,50,0,PhysicalStrengthScaling.Full,1,true).ToHit==0 &&
            OriginalPhysicalDamage.PrimaryHand(50,50,50,0,PhysicalStrengthScaling.Full,4,true).ToHit==-4,"Modern hand load adjustment truncates before addition");
        Reject(()=>OriginalPhysicalDamage.PrimaryHand(126,50,50,0,PhysicalStrengthScaling.Full,0),"Unresolved Strength cap rejected");
        Reject(()=>OriginalPhysicalDamage.PrimaryHand(50,50,50,0,(PhysicalStrengthScaling)99,0),"Unknown strength policy rejected");
        Check(OriginalPhysicalDamage.PrimaryHand(50,50,50,254,PhysicalStrengthScaling.Full,0).ToHit==254,
            "Combined weapon and ammunition accuracy retains two signed-byte contributions");
        var dice=new PhysicalDamageDice(2,1,6);
        Check(OriginalPhysicalDamage.Roll(dice,2,0,0,0,[0,5])==new PhysicalDamageResult(11,11,11),"Repeated damage rerolls full expression including its constant");
        Check(OriginalPhysicalDamage.Roll(new(0,1,2),2,0,0,0,[0,1]).Rolled==3,"Two independent dice can produce an odd total unlike doubling a single die");
        Check(OriginalPhysicalDamage.Roll(new(0,1,6),1,50,0,50,[2])==new PhysicalDamageResult(3,5,3),"Damage bonus rounds before absorption rounds a second time");
        Check(OriginalPhysicalDamage.Roll(new(-5,0,0),1,0,0,0,[])==new PhysicalDamageResult(-5,1,1),"Signed constant retains raw roll but pre-absorption damage has a one-point floor");
        Check(OriginalPhysicalDamage.Roll(dice,1,0,0,100,[5]).Damage==0,"Full absorption can remove the minimum damage");
        Check(OriginalPhysicalDamage.Roll(dice,1,0,0,200,[5]).Damage==0,"Over-full absorption clamps at zero rather than healing");
        Check(OriginalPhysicalDamage.Roll(dice,1,0,0,-50,[5]).Damage==12,"Resolved negative absorption amplifies damage");
        Reject(()=>OriginalPhysicalDamage.Roll(dice,2,0,0,0,[0]),"Missing second independent damage die rejected");
        Reject(()=>OriginalPhysicalDamage.Roll(dice,1,0,0,0,[6]),"Exclusive damage draw boundary enforced");
        Reject(()=>OriginalPhysicalDamage.Roll(new(0,1,0),1,0,0,0,[0]),"Dice without positive sides rejected");
        Reject(()=>OriginalPhysicalDamage.Roll(new(32768,1,6),1,0,0,0,[0]),"Unsigned-only damage constant rejected");
        Reject(()=>OriginalPhysicalDamage.Roll(dice,1,int.MaxValue,0,0,[0]),"Damage scaling overflow rejected");
        var weapon=new ItemDefinition(1,"synthetic weapon",0,0,32767,65535,3,new string('a',64),0,0,0,2,2,1,6,1,10,false,0);
        var ammo=weapon with { Id=2,Name="synthetic ammo",TypeCode=4,ToHit=3,DamageConstant=1,DamageDice=1,DamageSides=4 };
        var melee=OriginalWeaponAdapter.PreparePrimary(weapon);
        Check(melee==new PreparedPhysicalWeapon(dice,2,0,PhysicalStrengthScaling.Full,false),"Ordinary weapon adapter exposes explicit semantic damage inputs");
        var bow=weapon with { IconIndex=11,TypeCode=3,UsageSkill=8,RangeCode=3,DamageConstant=3,DamageDice=0,DamageSides=0,AttackModes=256 };
        var shot=OriginalWeaponAdapter.PreparePrimary(bow,ammo);
        Check(shot.Damage==new PhysicalDamageDice(1,1,4) && shot.ToHit==5 && shot.LauncherDamagePercent==30 && shot.StrengthScaling==PhysicalStrengthScaling.Half,
            "Launcher uses ammo dice, adds ammo accuracy and keeps its own constant as a percentage");
        foreach(int icon in new[]{104,110,114,131,144})
        {
            var native=weapon with { IconIndex=icon,AttackModes=256 };
            var prepared=OriginalWeaponAdapter.PreparePrimary(native,icon==131?ammo:null);
            Check(prepared.StrengthScaling==PhysicalStrengthScaling.None,"Native no-strength exception isolated in import adapter");
        }
        Check(OriginalPhysicalDamage.PrimaryHand(125,50,50,0,PhysicalStrengthScaling.None,0)==new PhysicalHandBonuses(0,0),
            "Native no-strength policy suppresses both accuracy and damage strength contributions");
        Reject(()=>OriginalWeaponAdapter.PreparePrimary(bow),"Ammunition must be resolved before preparing launcher attack");
        Reject(()=>OriginalWeaponAdapter.PreparePrimary(weapon,ammo),"Melee weapon cannot silently use an ammo damage expression");
        Reject(()=>OriginalWeaponAdapter.PreparePrimary(bow,ammo with { TypeCode=19 }),"Unknown ammunition hand category rejected");
        var budget=OriginalCombatRules.SingleWeapon(new(100,100,100,100,[new(OriginalProfession.Fighter,10)],0));
        var bonuses=OriginalPhysicalDamage.PrimaryHand(100,100,100,melee.ToHit,melee.StrengthScaling,0);
        var stats=new PhysicalCombatAttributes(100,100,100,100);
        var input=new OrdinaryPhysicalScoreInput(budget.BaseAttackRating,bonuses.ToHit,0,0,0,stats,stats,OriginalAttackMode.Swing,10,10);
        var result=OriginalMeleeStrike.Resolve(input,dice,bonuses.DamagePercent,0,0,49,[5]);
        Check(budget.BaseAttackRating==156 && result.Scores.HitScore==181 && result.Damage==new PhysicalDamageResult(8,16,16),
            "Class history, hand attributes, target scores, penetration and damage compose in one neutral melee strike");
        var low=input with { BaseAttackRating=100,HandToHit=0,Defense=10,ArmorDefense=15 };
        Check(OriginalMeleeStrike.Resolve(low,dice,0,0,0,49,[]).Outcome==PhysicalStrikeOutcome.NotPenetrated,"Same d100 may pass hit and fail armor penetration");
        Check(OriginalMeleeStrike.Resolve(low,dice,0,0,0,50,[]).Outcome==PhysicalStrikeOutcome.Miss,"Hit threshold equality uses one-based shared die");
        Check(OriginalMeleeStrike.Resolve(low with { Defense=100,ArmorDefense=100 },dice,0,0,0,4,[0]).Outcome==PhysicalStrikeOutcome.Damage,
            "First five d100 outcomes bypass ordinary hit and penetration comparisons");
        Check(OriginalMeleeStrike.Resolve(input,dice,0,0,0,95,[]).Outcome==PhysicalStrikeOutcome.Miss,"Natural 96 misses even with a score above 100");
        Check(OriginalMeleeStrike.Resolve(input with { Mode=OriginalAttackMode.Berserk },dice,0,0,0,20,[0,5]).Damage!.Rolled==11,
            "Neutral Berserk strike requests two independent full damage rolls");
        Reject(()=>OriginalMeleeStrike.Resolve(input,dice,0,0,0,99,[0]),"Missed strike rejects unused damage RNG draws");
        Reject(()=>OriginalMeleeStrike.Resolve(low with { BaseAttackRating=99 },dice,0,0,0,10,[0]),"Unresolved fumble/redirection path is not silently treated as ordinary melee");
        Reject(()=>OriginalMeleeStrike.Resolve(input with { Mode=OriginalAttackMode.Shoot },dice,0,0,0,10,[0]),"Projectile contact is not assumed to use melee delivery rules");
        // Exhaustive contact outcomes: no second penetration draw is supplied or generated.
        bool contact=true;
        for(int draw=0;draw<100;draw++)
        {
            var expected=draw>=95 || draw>=50 && draw>=5?PhysicalStrikeOutcome.Miss:
                draw>=25 && draw>=5?PhysicalStrikeOutcome.NotPenetrated:PhysicalStrikeOutcome.Damage;
            var actual=OriginalMeleeStrike.Resolve(low,dice,0,0,0,draw,expected==PhysicalStrikeOutcome.Damage?[0]:[]);
            contact &= actual.Outcome==expected;
        }
        Check(contact,"Every shared d100 outcome agrees with automatic, ordinary-hit, penetration and natural-miss intervals");
        return checks;
    }
}
