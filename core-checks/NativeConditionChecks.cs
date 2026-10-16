using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class NativeConditionChecks
{
    public static int Run(MonsterCatalog? originals=null)
    {
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool fail=false;try{action();}catch(ArgumentException){fail=true;}catch(InvalidOperationException){fail=true;}catch(InvalidDataException){fail=true;}Check(fail,name);}
        var items=MeleeTraining.Catalog();
        if(originals is not null)
        {
            var added=originals.Monsters.Where(m=>MonsterCombat.Supported(m) && MonsterAttackInspector.Inspect(m).Attacks.Any(a=>a.Effects[1].Probability>0)).ToArray();
            Check(added.Select(m=>m.Id).Order().SequenceEqual(new[]{52,268,383}),"Three original condition-16 definitions cross the complete-action gate");
            foreach(var row in added)
            {
                var s=MonsterCombat.Create(2,1,items,originals,row.Id);Check(s.Melee!.Field!.Conditions is not null,"New originals include explicit party target facts and condition state");
                Reject(()=>MonsterCombat.Create(2,1,items,originals,row.Id,2),"Profile two retains its historical no-effect admission boundary");
                s=s with {Party=s.Party.Select(h=>h with {Hp=1000,MaxHp=1000}).ToImmutableArray()};
                s=TacticalCombatChecks.Place(s,("hero-1",2,2),("hero-2",0,6),(s.Enemy.Id,3,2));
                bool applied=false;for(uint seed=1;seed<=2000 && !applied;seed++)
                {var r=TacticalCombat.Advance(s with {RandomState=seed},items,s.Party.Select(h=>new TacticalOrder(h.Id,TacticalAction.Wait)).ToArray());applied=r.Events.Any(e=>e.Contains("condition 16 Requested"));}
                Check(applied,"Each newly admitted original record produces a real condition request against a living training target");
            }
            return checks;
        }
        bool timers=true;for(int current=0;current<=12;current++)
        {
            for(int incoming=2;incoming<=12;incoming++)timers &= OriginalCondition16.Merge(current,incoming)==Math.Max(current,incoming);
            for(int elapsed=0;elapsed<=20;elapsed++)timers &= OriginalCondition16.Advance(current,elapsed)==Math.Max(0,current-elapsed);
        }
        Check(timers,"All finite timer merge and elapsed/expiry boundaries");
        var catalog=GeneralMonsterChecks.Fixture(b=>{b[0xe7+6]=255;b[0xeb]=255;b[0x24f]=30;b[0xe4]=255;b[0xe5]=2;b[0xe6]=2;b[0xd6]=0xe8;b[0xd7]=3;b[0xd8]=0;b[0xe7+23]=1;b[0xe7+24]=0;b[0xe7+25]=0;});
        var start=MonsterCombat.Create(2,1,items,catalog,0);start=TacticalCombatChecks.Place(start,("hero-1",2,2),("hero-2",0,6),(start.Enemy.Id,3,2));
        var orders=new[]{new TacticalOrder("hero-1",TacticalAction.Attack,start.Enemy.Id),new TacticalOrder("hero-2",TacticalAction.Wait)};
        var allWait=start.Party.Select(h=>new TacticalOrder(h.Id,TacticalAction.Wait)).ToArray();
        RoundResult? active=null;GameState? source=null;
        for(uint seed=1;seed<=80 && active is null;seed++)
        {
            var s=start with {RandomState=seed};var result=TacticalCombat.Advance(s,items,orders);
            if(result.Events.Any(e=>e.Contains("condition 16 Requested")) && result.Events.Any(e=>e.Contains("hero-1: native condition 16 cancels"))){active=result;source=s;}
        }
        Check(active is not null,"Actual monster damage applies condition 16 and cancels a queued hero attack in the same round");
        var affected=active!.State;int longest=active.Events.Where(e=>e.Contains("condition 16 Requested")).Select(e=>int.Parse(e[(e.LastIndexOf(' ')+1)..])).Max();
        Check(NativeConditions.Remaining(affected,"hero-1")==longest-1,"Multiple actual applications retain maximum timer and consume one round-end unit");
        Check(NativeConditions.Remaining(source!,"hero-1")==0,"Condition application never mutates the caller state");
        Check(Game.Save(TacticalCombat.Advance(affected,items,allWait).State)==Game.Save(TacticalCombat.Advance(Game.Load(Game.Save(affected),items),items,allWait).State),"Active native timer, target facts and RNG continue exactly after save/load");
        var f=start.Melee!.Field!;var c=f.Conditions!;
        var frozen=start with {Melee=start.Melee with {Field=f with {Conditions=c with {Condition16=ImmutableDictionary<string,int>.Empty.Add("hero-1",1).Add(start.Enemy.Id,12)}}}};
        var expiry=TacticalCombat.Advance(frozen,items,orders);Check(!expiry.Events.Any(e=>e.StartsWith("hero-1 →")) && NativeConditions.Remaining(expiry.State,"hero-1")==0,"Last unit blocks this round then expires at its end");
        var recovered=TacticalCombat.Advance(expiry.State,items,orders);Check(recovered.Events.Any(e=>e.StartsWith("hero-1 →")),"Recovered hero acts again against an incapacitated living monster");
        var blocked=frozen with {Melee=frozen.Melee! with {Field=frozen.Melee.Field! with {Conditions=c with {Condition16=ImmutableDictionary<string,int>.Empty.Add("hero-1",12)}}}};
        var comparison=TacticalCombat.Advance(start with {RandomState=42},items,allWait);
        var capped=TacticalCombat.Advance(blocked with {RandomState=42},items,allWait);
        int Score(RoundResult r)=>int.Parse(Regex.Match(r.Events.First(e=>e.StartsWith(start.Enemy.Id+" (") && e.Contains("hit ")),@"hit (-?\d+)").Groups[1].Value);
        Check(Score(capped)-Score(comparison)==40,"Live condition flag caps character defense and raises the attack's targeted hit score");
        var old=MonsterCombat.Create(2,1,items,MonsterCombat.FixtureCatalog(),0,2);var oldJson=JsonNode.Parse(Game.Save(old))!;oldJson["Schema"]=8;
        var migrated=Game.Load(oldJson.ToJsonString(),items);Check(migrated.Melee!.Field!.Conditions is null && migrated.Melee.Field.Monsters!.Values.All(m=>m.Record.ProfileVersion==2),"Schema-eight migration fabricates no target or timer facts");
        Check(Game.Save(TacticalCombat.Advance(old,items).State)==Game.Save(TacticalCombat.Advance(migrated,items).State),"Old profile-two combat retains exact action RNG");
        var injected=JsonNode.Parse(Game.Save(start))!;injected["Schema"]=8;Reject(()=>Game.Load(injected.ToJsonString(),items),"Old schema cannot smuggle new condition facts/profile three");
        Reject(()=>Game.Load(Game.Save(start with {Melee=start.Melee with {Field=f with {Conditions=null}}}),items),"Condition-aware encounter cannot omit target facts");
        foreach(int bad in new[]{0,-1,13})
        {
            var invalid=start with {Melee=start.Melee with {Field=f with {Conditions=c with {Condition16=c.Condition16.Add("hero-1",bad)}}}};
            Reject(()=>Game.Load(Game.Save(invalid),items),"Native timers reject zero, negative and out-of-domain durations");
        }
        var foreign=start with {Melee=start.Melee with {Field=f with {Conditions=c with {Condition16=c.Condition16.Add("unknown",2)}}}};Reject(()=>Game.Load(Game.Save(foreign),items),"Native condition target must exist on this board");
        var missing=start with {Melee=start.Melee with {Field=f with {Conditions=c with {PartyTargets=c.PartyTargets.Remove("hero-1")}}}};Reject(()=>Game.Load(Game.Save(missing),items),"Party condition resistance facts must cover every hero");
        var selfCatalog=GeneralMonsterChecks.Fixture(b=>{b[0xe7+6]=255;b[0xeb]=0;b[0x16d+1]=127;b[0x166+3]=127;b[0xd6]=0xe8;b[0xd7]=3;b[0xd8]=0;b[0xe7+23]=1;b[0xe7+24]=0;b[0xe7+25]=0;});
        bool self=false;
        for(uint seed=1;seed<=200 && !self;seed++)
        {
            var s=MonsterCombat.Create(1,1,items,selfCatalog,0) with {RandomState=seed};s=TacticalCombatChecks.Place(s,("hero-1",2,2),(s.Enemy.Id,3,2));
            var r=TacticalCombat.Advance(s,items,[new("hero-1",TacticalAction.Wait)]);
            self=r.Events.Any(e=>e.StartsWith(s.Enemy.Id+" → "+s.Enemy.Id+": native condition 16 Requested"));
            if(self)Check(NativeConditions.Remaining(r.State,s.Enemy.Id)>0 && Game.Save(Game.Load(Game.Save(r.State),items))==Game.Save(r.State),"Fumble uses actual monster target resistance/state and persists self-incapacitation");
        }
        Check(self,"Real fumble can apply condition 16 to the attacking monster and stop its series");
        var protectedParty=start with {Melee=start.Melee with {Party=start.Melee.Party.SetItem("hero-1",start.Melee.Party["hero-1"] with {BodyArmor=new(127,127,127,127,127)})}};
        bool stopped=false;for(uint seed=1;seed<=80 && !stopped;seed++)
        {
            var r=TacticalCombat.Advance(protectedParty with {RandomState=seed},items,allWait);
            if(r.Events.Any(e=>e.Contains("NotPenetrated")) && !r.Events.Any(e=>e.Contains(" Damage,")))
            {stopped=true;Check(!r.Events.Any(e=>e.Contains("native condition 16 ")) && NativeConditions.Remaining(r.State,"hero-1")==0,"Stopped physical damage consumes no on-hit effect attempt");}
        }
        Check(stopped,"Armored fixture reaches the no-penetration effect gate in live execution");
        return checks;
    }
}
