using System.Collections.Immutable;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class RestoredRoundChecks
{
    public static int Run()
    {
        int count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
        var ordinary=new CharacterInitiativeFacts(10,false,0);
        Check(OriginalInitiative.Character(80,70,ordinary)==25,"Native initiative integer terms");
        Check(OriginalInitiative.Character(0,0,new(1,false,0))==-9,"Low attributes retain signed initiative");
        Check(OriginalInitiative.Character(100,100,new(1,true,0))==32,"Enabled Snakespeed at zero grants one");
        Check(OriginalInitiative.Character(100,100,new(1,true,125,LoadCategory:4))==36,"Snakespeed floor and severe load");
        foreach(int speed in Enumerable.Range(0,126))foreach(int sense in new[]{0,4,5,49,50,99,100,125})
            Check(OriginalInitiative.Character(speed,sense,ordinary)==5+speed/5+sense/5-10,"Attribute breakpoints");
        for(int roll=0;roll<4;roll++)
        {
            Check(OriginalInitiative.FirstTime(25,3,roll)==62-roll,"Weapon initial queue time");
            Check(OriginalInitiative.FirstTime(25,null,roll)==65-roll,"Magic ignores active weapon initiative");
        }
        Check(OriginalInitiative.FirstTime(-100,-128,0)==100 && OriginalInitiative.FirstTime(300,127,3)==1,"Queue clamps before inversion");
        Check(OriginalInitiative.NextAttackTime(60,0,60,2)==73 && OriginalInitiative.NextAttackTime(73,0,73,1)==86,"Remaining attack spacing and end-of-round boundary");
        var cat=MeleeTraining.Catalog();var state=MeleeTraining.Create(2,cat);
        Check(Game.Save(Game.Load(Game.Save(state),cat))==Game.Save(state),"Initiative facts persist");
        var legacy=state with {Melee=state.Melee! with {Party=state.Melee.Party.ToImmutableDictionary(x=>x.Key,x=>x.Value with {Initiative=null}),Opponent=state.Melee.Opponent with {Initiative=null}}};
        Check(Game.Attack(legacy,cat,3).State.Round==1,"Old authored encounter keeps its stored speed policy");
        bool interleaved=false;
        for(uint seed=1;seed<=30;seed++)
        {
            var round=Game.Attack(state with {RandomState=seed,Enemy=state.Enemy with {Hp=500}},cat,3);
            var queue=round.Events.Where(e=>e.StartsWith("Queue")).ToArray();
            Check(queue.Length>3,"Multiple attacks re-enter queue");
            interleaved|=queue.Length>3 && !queue[0].Split(':')[1].Equals(queue[1].Split(':')[1]);
        }
        Check(interleaved,"Actors interleave instead of finishing their entire attack budget consecutively");
        return count;
    }
    public static int RunSpells(SpellCatalog spells)
    {
        if(!spells.Spells.Any(RestoredMagic.SupportedSpell))return 0;
        int count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
        void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
        var catalog=MeleeTraining.Catalog();var state=MeleeTraining.Create(2,catalog,spells);
        Check(state.Party[0].Profession=="Mage" && state.Melee!.Party[state.Party[0].Id].History[0].Profession==OriginalProfession.Mage,"Training mage identity and melee class agree");
        foreach(var spell in spells.Spells.Where(RestoredMagic.SupportedSpell))for(int power=1;power<=7;power++)
        {
            var order=new RestoredCastOrder(state.Party[0].Id,spell.Id,power);var before=Game.Save(state);
            var prepared=RestoredMagic.Prepare(state,order,spells);
            Check(prepared.ManaCost==power*2 && prepared.Risk.FailureThreshold==0,"Imported spell power and safe cost");
            var result=MeleeCombat.Advance(state,catalog,3,[order],spells);
            Check(Game.Save(state)==before,"Original cast leaves source snapshot intact");
            Check(result.State.Party[0].Mana!.Single(m=>m.Realm==spell.Realm).Current==42-power*2,"Only caster realm pays original cost");
            Check(result.Events.Count(e=>e.Contains($"{spell.Name} PL"))==1,"Cast replaces all weapon attacks and executes once");
            Check(Game.Save(MeleeCombat.Advance(Game.Load(before,catalog),catalog,3,[order],spells).State)==Game.Save(result.State),"Original cast RNG and mana resume after save/load");
        }
        var command=new RestoredCastOrder(state.Party[0].Id,5,7);
        var poor=state with {Party=state.Party.SetItem(0,state.Party[0] with {Mana=[new("Fire",1,42),new("Water",42,42)]})};
        Reject(()=>MeleeCombat.Advance(poor,catalog,3,[command],spells),"Insufficient mana rejected atomically");
        Reject(()=>MeleeCombat.Advance(state,catalog,3,[command,command],spells),"Duplicate caster orders rejected");
        Reject(()=>MeleeCombat.Advance(state,catalog,5,[command],spells),"Out of room range rejected");
        var risky=state with {Melee=state.Melee! with {Magic=state.Melee.Magic!.SetItem(command.CasterId,state.Melee.Magic[command.CasterId] with {BookSkill=0,RealmSkills=[0,0,0,0,0,0],EffectiveCasterLevel=1})}};
        Reject(()=>MeleeCombat.Advance(risky,catalog,3,[command],spells),"Unrecovered risky cast lifecycle stays gated");
        var noCaster=state with {Party=state.Party.SetItem(0,state.Party[0] with {Hp=0})};
        Reject(()=>MeleeCombat.Advance(noCaster,catalog,3,[command],spells),"Dead caster cannot submit");
        var source=spells.Spells.Single(s=>s.Id==5);
        var tampered=spells with {Spells=spells.Spells.Replace(source,source with {Cost=0})};
        Reject(()=>MeleeCombat.Advance(state,catalog,3,[command],tampered),"Pinned record hash cannot authenticate forged typed spell fields");
        bool skipped=false;
        var lethal=state with {Party=state.Party.SetItem(0,state.Party[0] with {Hp=1}),Melee=state.Melee! with {Opponent=state.Melee.Opponent with {Initiative=new(255,true,125)}}};
        for(uint seed=1;seed<=40;seed++)
        {
            var round=MeleeCombat.Advance(lethal with {RandomState=seed},catalog,3,[command],spells);
            if(round.State.Party[0].Hp!=0 || round.Events.Any(e=>e.Contains("Energy Blast PL")))continue;
            Check(round.State.Party[0].Mana!.Single(m=>m.Realm=="Fire").Current==42,"Caster killed before queue time spends no mana and executes no spell");
            skipped=true;break;
        }
        Check(skipped,"Initiative can prevent a later cast");
        var f=state.Melee!.Magic![command.CasterId];var p=RestoredMagic.Prepare(state,command,spells);uint a=123,b=123;
        var baseline=RestoredMagic.Damage(p,f with {TargetLevel=1,Resistances=[0,0,0,0,0,0]},ref a);
        var immune=RestoredMagic.Damage(p,f with {TargetLevel=255,Resistances=[255,255,255,255,255,255]},ref b);
        Check(baseline.Damage>0 && immune.Damage==0 && immune.Reduction==100,"Live direct spell composes native resistance bounds");
        return count;
    }
}
