using System.Collections.Immutable;
using System.Text.Json.Nodes;
using NeoWizardry.Core;

static class EnemyAiChecks
{
    private static GameState Brain(GameState s,string id,EnemyBrain b)=>s with {Melee=s.Melee! with {Field=s.Melee!.Field! with {Brains=s.Melee.Field.Brains!.SetItem(id,b)}}};
    public static int Run()
    {
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}catch(InvalidOperationException){failed=true;}catch(ArgumentException){failed=true;}Check(failed,name);}
        var catalog=MeleeTraining.Catalog();var s=MeleeTraining.CreateTactical(2,1,catalog);string id=s.Enemy.Id;
        s=TacticalCombatChecks.Place(s,(id,4,1),("hero-1",2,0),("hero-2",1,2));s=s with {Party=s.Party.SetItem(1,s.Party[1] with {Hp=5})};
        string source=Game.Save(s);var brute=EnemyAi.Decide(s,id);
        var hunterState=EnemyAi.SetTactic(s,id,EnemyTactic.Hunter);var hunter=EnemyAi.Decide(hunterState,id);
        Check(brute.Order.Action==TacticalAction.Move && brute.Order.TargetId=="hero-1","Brute chooses shortest reachable approach");
        Check(hunter.Order.Action==TacticalAction.Move && hunter.Order.TargetId=="hero-2","Hunter prefers visible wounded hero over a nearer healthy hero");
        Check(Game.Save(s)==source && EnemyAi.Decide(s,id)==brute,"AI planning is pure deterministic and consumes no RNG");
        var near=TacticalCombatChecks.Place(hunterState,("hero-1",3,0),("hero-2",3,2));
        Check(EnemyAi.Decide(near,id).Order is {Action:TacticalAction.Attack,TargetId:"hero-2"},"Hunter chooses wounded adjacent target");
        near=near with {Party=near.Party.SetItem(1,near.Party[1] with {Hp=0})};
        Check(EnemyAi.Decide(near,id).Order.TargetId=="hero-1","Dead target immediately replaced");
        near=near with {Party=near.Party.SetItem(1,near.Party[1] with {Hp=5,Captive=true})};
        Check(EnemyAi.Decide(near,id).Order.TargetId=="hero-1","Captive hero cannot attract an attack");
        Check(EnemyAi.Decide(s,id,false).Order is {Action:TacticalAction.Wait,Destination:null},"Remaining attack group cannot buy a movement action");
        var squad=MeleeTraining.CreateTactical(2,2,catalog);var f=squad.Melee!.Field!;
        squad=TacticalCombatChecks.Place(squad,("hero-1",1,0),("hero-2",1,2),(squad.Enemy.Id,6,0),("Training Duelist 2",6,2));
        squad=Brain(squad,squad.Enemy.Id,f.Brains![squad.Enemy.Id] with {TargetId="hero-1",LastSeen=new(1,0),SeenRound=0,Mode=EnemyAiMode.Pursue});
        squad=EnemyAi.SetTactic(squad,"Training Duelist 2",EnemyTactic.Flanker);
        var flank=EnemyAi.Decide(squad,"Training Duelist 2");
        Check(flank.Order.TargetId=="hero-2" && flank.Reason.Contains("assigned allies 0"),"Flanker distributes pressure away from an ally's selected target");
        var crowded=TacticalCombatChecks.Place(squad,("hero-1",3,0),("hero-2",3,2),("Training Duelist 2",4,1));
        Check(EnemyAi.Decide(crowded,"Training Duelist 2").Order.TargetId=="hero-2","Adjacent flanker avoids a target already under active allied pressure");
        var ally=crowded.Melee!.Field!.Brains![crowded.Enemy.Id];
        Check(EnemyAi.Decide(Brain(crowded,crowded.Enemy.Id,ally with {Mode=EnemyAiMode.Retreat}),"Training Duelist 2").Order.TargetId=="hero-1","Retreating ally does not count as attack pressure");
        Check(EnemyAi.Decide(crowded with {Round=4},"Training Duelist 2").Order.TargetId=="hero-1","Expired ally intention does not count as attack pressure");
        var memory=MeleeTraining.CreateTactical(1,1,catalog);memory=TacticalCombatChecks.Place(memory,(id,4,0),("hero-1",2,0));
        memory=Brain(memory,id,memory.Melee!.Field!.Brains![id] with {Profile=new(EnemyTactic.Brute,Vision:2)});
        var observed=EnemyAi.Decide(memory,id);Check(observed.Brain.LastSeen==new BattleCell(2,0) && observed.Brain.SeenRound==0,"Visible target observation is recorded");
        memory=Brain(memory,id,observed.Brain) with {Round=1};memory=TacticalCombatChecks.Place(memory,("hero-1",0,0));
        var search=EnemyAi.Decide(memory,id);Check(search.Brain.Mode==EnemyAiMode.Search && search.Brain.LastSeen==new BattleCell(2,0),"Out-of-sight target is searched at its last known location");
        var hidden=TacticalCombatChecks.Place(memory,("hero-1",0,7));
        Check(EnemyAi.Decide(hidden,id).Order==search.Order,"Changing unseen current position does not redirect remembered pursuit");
        var expired=memory with {Round=5};var forgotten=EnemyAi.Decide(expired,id);
        Check(forgotten.Order.Action==TacticalAction.Wait && forgotten.Brain.TargetId is null && forgotten.Brain.Mode==EnemyAiMode.Idle,"Memory expires instead of tracking hidden heroes forever");
        var searched=TacticalCombatChecks.Place(hidden,(id,2,0));var lost=EnemyAi.Decide(searched,id);
        Check(lost.Brain.Mode==EnemyAiMode.Idle && lost.Brain.TargetId is null,"Reaching an empty last-known cell clears pursuit");
        var deadMemory=hidden with {Party=hidden.Party.SetItem(0,hidden.Party[0] with {Hp=0})};
        Check(EnemyAi.Decide(deadMemory,id).Brain.TargetId is null,"Dead remembered target is discarded");
        var retreat=MeleeTraining.CreateTactical(1,1,catalog);retreat=TacticalCombatChecks.Place(retreat,(id,4,2),("hero-1",2,2));retreat=EnemyAi.SetTactic(retreat,id,EnemyTactic.Skirmisher);
        var healthy=EnemyAi.Decide(retreat,id);Check(healthy.Brain.Mode==EnemyAiMode.Pursue,"Healthy skirmisher approaches");
        retreat=retreat with {Enemy=retreat.Enemy with {Hp=12}};var fleeing=EnemyAi.Decide(retreat,id);
        Check(fleeing.Brain.Mode==EnemyAiMode.Retreat && fleeing.Order.Destination is {} escape && TacticalCombat.Distance(escape,new(2,2))>2,"Skirmisher retreats at configured health threshold");
        Check(EnemyAi.Decide(retreat with {Enemy=retreat.Enemy with {Hp=13}},id).Brain.Mode!=EnemyAiMode.Retreat,"Retreat health threshold boundary");
        var cornered=TacticalCombatChecks.Place(retreat,(id,7,7),("hero-1",6,7));
        Check(EnemyAi.Decide(cornered,id).Order.Action==TacticalAction.Attack,"Cornered skirmisher fights instead of trying an impossible retreat");
        var blocked=Brain(hidden,id,search.Brain);blocked=blocked with {Melee=blocked.Melee! with {Field=blocked.Melee!.Field! with {Walls=Enumerable.Range(0,8).Select(y=>new BattleCell(3,y)).ToImmutableArray()}}};
        var waiting=EnemyAi.Decide(blocked,id);Check(waiting.Brain.Mode==EnemyAiMode.Blocked && waiting.Order.Action==TacticalAction.Wait,"Unreachable remembered position produces bounded wait");
        Check(EnemyAi.Decide(retreat with {Enemy=retreat.Enemy with {Hp=0}},id).Order.Action==TacticalAction.Wait,"Dead enemy emits no action");
        var legacy=JsonNode.Parse(Game.Save(MeleeTraining.CreateTactical(2,2,catalog)))!;legacy["Schema"]=5;legacy["Melee"]!["Field"]!.AsObject().Remove("Brains");
        var old=Game.Load(legacy.ToJsonString(),catalog);Check(old.Schema==6 && old.Melee!.Field!.Brains is null,"Schema-five tactical save retains legacy pursuit without fabricated brains");
        var forged=JsonNode.Parse(Game.Save(s))!;forged["Schema"]=5;Reject(()=>Game.Load(forged.ToJsonString(),catalog),"Old save schema cannot carry new brain state");
        var brain=s.Melee!.Field!.Brains![id];
        foreach(var bad in new[]{brain with {MaxHp=1},brain with {Profile=brain.Profile with {Vision=0}},brain with {Profile=brain.Profile with {Tactic=(EnemyTactic)90}},brain with {TargetId="hero-1"},brain with {TargetId="unknown",LastSeen=new(1,1),SeenRound=0},brain with {TargetId="hero-1",LastSeen=new(1,1),SeenRound=1}})
            Reject(()=>Game.Validate(Brain(s,id,bad)),"Invalid AI profile or memory rejected");
        Reject(()=>Game.Validate(s with {Melee=s.Melee! with {Field=s.Melee!.Field! with {Brains=ImmutableDictionary<string,EnemyBrain>.Empty}}}),"Partial brain sets rejected");
        // AI re-evaluates every attack group. Find a seed where the first selected hero dies,
        // and verify a later group attacks the other hero rather than wasting its budget.
        bool retargeted=false;
        for(uint seed=1;seed<=80 && !retargeted;seed++)
        {
            var duel=MeleeTraining.CreateTactical(2,1,catalog);duel=TacticalCombatChecks.Place(duel,(id,4,1),("hero-1",3,0),("hero-2",3,2));
            duel=duel with {RandomState=seed,Party=duel.Party.SetItem(0,duel.Party[0] with {Hp=1}),Melee=duel.Melee! with {Opponent=duel.Melee!.Opponent with {Attributes=duel.Melee.Opponent.Attributes with {Dexterity=125,Speed=125,Senses=125},CombatSkill=125,WeaponSkill=125}}};
            var round=TacticalCombat.Advance(duel,catalog,[new("hero-1",TacticalAction.Wait),new("hero-2",TacticalAction.Wait)]);
            retargeted=round.State.Party[0].Hp==0 && round.Events.Any(e=>e.StartsWith(id+" → hero-2"));
        }
        Check(retargeted,"AI retargets another adjacent hero after killing its first target during the round");
        for(int size=1;size<=8;size++)
        {
            var start=MeleeTraining.CreateTactical(size,8,catalog);var next=TacticalCombat.Advance(start,catalog).State;
            Check(start.Melee!.Field!.Brains!.Count==8 && next.Melee!.Field!.Brains!.Values.Any(b=>b.SeenRound==0),"All enemy brains participate in a mixed-role group");
            Check(Game.Save(TacticalCombat.Advance(next,catalog).State)==Game.Save(TacticalCombat.Advance(Game.Load(Game.Save(next),catalog),catalog).State),"Mixed-role AI memory and next-round decisions survive save/load exactly");
        }
        return checks;
    }
}
