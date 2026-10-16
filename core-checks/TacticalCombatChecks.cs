using System.Collections.Immutable;
using System.Text.Json.Nodes;
using NeoWizardry.Core;

static class TacticalCombatChecks
{
    public static GameState Place(GameState s,params (string Id,int X,int Y)[] cells)
    {
        var f=s.Melee!.Field!;return s with {Melee=s.Melee with {Field=f with {Positions=cells.Aggregate(f.Positions,(p,c)=>p.SetItem(c.Id,new(c.X,c.Y)))}}};
    }
    public static int Run()
    {
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action a,string name){bool fail=false;try{a();}catch(InvalidOperationException){fail=true;}catch(InvalidDataException){fail=true;}Check(fail,name);}
        var catalog=MeleeTraining.Catalog();var s=MeleeTraining.CreateTactical(3,3,catalog);string before=Game.Save(s);
        var first=TacticalCombat.Advance(s,catalog);
        Check(first.State.Round==1 && first.State.Melee!.Field!.Positions[s.Enemy.Id].X==5,"Multiple opponents independently approach the party");
        Check(first.State.Party.All(h=>h.Hp==36) && first.State.Enemy.Hp==48,"No damage across the board from melee");
        Check(Game.Save(s)==before,"Tactical round preserves source snapshot");
        Check(Game.Save(first.State)==Game.Save(Game.Attack(s,catalog,999).State),"Tactical dispatch uses individual positions instead of legacy camera range");
        Check(Game.Save(TacticalCombat.Advance(first.State,catalog).State)==Game.Save(TacticalCombat.Advance(Game.Load(Game.Save(first.State),catalog),catalog).State),"Tactical save preserves all opponents positions RNG and continuation");
        var old=JsonNode.Parse(Game.Save(MeleeTraining.Create(2,catalog)))!;old["Schema"]=4;old["Melee"]!.AsObject().Remove("Field");
        var migrated=Game.Load(old.ToJsonString(),catalog);Check(migrated.Schema==Game.SaveSchema && migrated.Melee!.Field is null,"Schema-four duels migrate without invented positions");
        var forged=JsonNode.Parse(before)!;forged["Schema"]=4;Reject(()=>Game.Load(forged.ToJsonString(),catalog),"Old schema cannot carry a tactical field");
        Reject(()=>TacticalCombat.Advance(s,catalog,[new("hero-1",TacticalAction.Attack,s.Enemy.Id)]),"Reject out-of-reach target before RNG");
        Reject(()=>TacticalCombat.Advance(s,catalog,[new("hero-1",TacticalAction.Move,Destination:new(1,1))]),"Reject movement into occupied ally cell");
        Reject(()=>TacticalCombat.Advance(s,catalog,[new("hero-1",TacticalAction.Move,Destination:new(-1,0))]),"Reject outside board movement");
        Reject(()=>TacticalCombat.Advance(s,catalog,[new("hero-1",TacticalAction.Wait),new("hero-1",TacticalAction.Wait)]),"Reject duplicate actor orders");
        Reject(()=>TacticalCombat.Advance(s,catalog,[new(s.Enemy.Id,TacticalAction.Wait)]),"Player cannot issue enemy orders");
        Reject(()=>Game.Validate(Place(s,("hero-1",6,0))),"Reject overlapping living actors in save");
        Reject(()=>Game.Validate(Place(s,("hero-1",3,3))),"Reject actor embedded in wall");
        var corner=Place(s,("hero-1",2,3));Reject(()=>TacticalCombat.Advance(corner,catalog,[new("hero-1",TacticalAction.Move,Destination:new(3,2))]),"No diagonal corner cutting around walls");
        var sight=Place(s,("hero-1",2,3),(s.Enemy.Id,4,3));Check(!TacticalCombat.Visible(sight,"hero-1",s.Enemy.Id),"Pillar blocks sight");
        sight=Place(s,("hero-1",1,0),("hero-2",2,0),(s.Enemy.Id,4,0));Check(!TacticalCombat.Visible(sight,"hero-1",s.Enemy.Id),"Living ally blocks spell delivery");
        var race=Place(s,("hero-2",1,2),("hero-3",0,4));var raceRound=TacticalCombat.Advance(race,catalog,[new("hero-1",TacticalAction.Move,Destination:new(2,1)),new("hero-2",TacticalAction.Move,Destination:new(2,1))]);
        Check(raceRound.Events.Count(e=>e.Contains("move cancelled"))==1 && raceRound.State.Party.Count(h=>raceRound.State.Melee!.Field!.Positions[h.Id]==new BattleCell(2,1))==1,"Competing movement orders never overlap actors");
        var dead=s with {Enemy=s.Enemy with {Hp=0}};var afterPrimary=TacticalCombat.Advance(dead,catalog);
        Check(afterPrimary.State.Round==1 && afterPrimary.State.Melee!.Field!.Positions["Training Duelist 2"].X==5 && !afterPrimary.State.RewardGranted,"Other opponents continue after primary opponent dies; no early reward");
        var intoCorpse=TacticalCombat.Advance(Place(dead,(s.Enemy.Id,2,0)),catalog,[new("hero-1",TacticalAction.Move,Destination:new(2,0))]);
        Check(intoCorpse.State.Melee!.Field!.Positions["hero-1"]==new BattleCell(2,0),"Corpses do not block movement");
        bool cancelledDeadTarget=false;
        for(uint seed=1;seed<=40 && !cancelledDeadTarget;seed++)
        {
            var duel=Place(MeleeTraining.CreateTactical(2,2,catalog),("hero-1",1,0),("hero-2",2,1),("Training Duelist",2,0),("Training Duelist 2",7,7));
            duel=duel with {RandomState=seed,Enemy=duel.Enemy with {Hp=1}};
            var r=TacticalCombat.Advance(duel,catalog,[new("hero-1",TacticalAction.Attack,duel.Enemy.Id),new("hero-2",TacticalAction.Attack,duel.Enemy.Id)]);
            cancelledDeadTarget=r.State.Enemy.Hp==0 && r.Events.Any(e=>e.Contains("Attack cancelled: target dead")) && !r.Events.Any(e=>e.Contains(": Training Duelist ·"));
        }
        Check(cancelledDeadTarget,"Killed queued enemy never acts; queued attacks at a dead target cancel without retargeting");
        var defeat=MeleeTraining.CreateTactical(1,8,catalog);defeat=defeat with {Party=defeat.Party.SetItem(0,defeat.Party[0] with {Hp=1})};
        for(int n=0;n<100 && defeat.Party[0].Hp>0;n++)defeat=TacticalCombat.Advance(defeat,catalog).State;
        Check(defeat.Party[0].Hp==0 && !defeat.RewardGranted && defeat.Party[0].Xp==0,"Defeat ends the battle without victory rewards");
        Check(Game.Save(TacticalCombat.Advance(defeat,catalog).State)==Game.Save(defeat),"Dead party cannot continue turns");
        var blocked=s with {Melee=s.Melee! with {Field=s.Melee!.Field! with {Walls=Enumerable.Range(0,8).Select(y=>new BattleCell(4,y)).ToImmutableArray()}}};
        var blockedRound=TacticalCombat.Advance(blocked,catalog);Check(blockedRound.State.Melee!.Field!.Positions.SequenceEqual(blocked.Melee!.Field!.Positions),"Unreachable enemies wait without walking into obstacles");
        for(int size=1;size<=8;size++)for(int enemies=1;enemies<=8;enemies++)
        {
            var battle=MeleeTraining.CreateTactical(size,enemies,catalog);
            var round=TacticalCombat.Advance(battle,catalog);Game.Validate(round.State);
            Check(TacticalCombat.Enemies(round.State).Count()==enemies && round.State.Melee!.Field!.Positions.Count==size+enemies,$"Independent actor state for {size} heroes / {enemies} opponents");
        }
        for(uint seed=1;seed<=12;seed++)
        {
            var battle=MeleeTraining.CreateTactical(8,3,catalog) with {RandomState=seed};
            for(int round=0;round<250 && TacticalCombat.Enemies(battle).Any(e=>e.Hp>0) && battle.Party.Any(h=>h.Hp>0);round++)battle=TacticalCombat.Advance(battle,catalog).State;
            Check(battle.RewardGranted && !TacticalCombat.Enemies(battle).Any(e=>e.Hp>0),"Multi-opponent encounter reaches victory");
            Check(battle.Party.Sum(h=>h.Xp)==360 && Inventory.Bag(battle).Count()==3,"All enemy XP and exact weapon instances transferred once");
            Check(Game.Save(TacticalCombat.Advance(battle,catalog).State)==Game.Save(battle),"Finished battle grants no duplicate reward or extra rounds");
            Check(Game.Save(Game.Load(Game.Save(battle),catalog))==Game.Save(battle),"Victory ownership remains valid after save/load");
        }
        Check(Game.Save(s)==before,"Rejected orders and replay never mutate caller");return checks;
    }
    public static int RunSpells(SpellCatalog spells)
    {
        if(!spells.Spells.Any(s=>s.Id==6 && RestoredMagic.SupportedSpell(s)))return 0;
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        var catalog=MeleeTraining.Catalog();var s=MeleeTraining.CreateTactical(2,3,catalog,spells);
        s=s with {Melee=s.Melee! with {Magic=s.Melee!.Magic!.SetItem("hero-1",s.Melee.Magic["hero-1"] with {BookSkill=80,RealmSkills=Enumerable.Repeat(80,6).ToImmutableArray()})}};
        var f=s.Melee.Field!;s=Place(s,("Training Duelist 2",5,2));
        foreach(int spell in new[]{4,5})for(int power=1;power<=7;power++)
        {
            var first=TacticalCombat.Advance(s,catalog,spells:spells,casts:[new("hero-1",spell,power,"Training Duelist 2")]);
            Check(first.State.Melee!.Field!.Opponents[0].Actor.Hp<48 && first.State.Enemy.Hp==48,"Offensive original spell hits selected secondary opponent only");
            Check(first.State.Party[0].Mana!.Single(m=>m.Realm==(spell==4?"Water":"Fire")).Current==42-2*power,"Selected spell power spends exact realm mana");
            Check(Game.Save(first.State)==Game.Save(TacticalCombat.Advance(Game.Load(Game.Save(s),catalog),catalog,spells:spells,casts:[new("hero-1",spell,power,"Training Duelist 2")]).State),"Multi-target magic exact replay");
        }
        var primaryDead=s with {Enemy=s.Enemy with {Hp=0}};
        var remaining=TacticalCombat.Advance(primaryDead,catalog,spells:spells,casts:[new("hero-1",5,1,"Training Duelist 2")]);
        Check(remaining.State.Melee!.Field!.Opponents[0].Actor.Hp<48,"Magic still targets a surviving secondary enemy after primary death");
        var obstructed=Place(s,("hero-1",2,3),("Training Duelist 2",4,3));bool rejected=false;
        try{TacticalCombat.Advance(obstructed,catalog,spells:spells,casts:[new("hero-1",5,1,"Training Duelist 2")]);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && obstructed.Party[0].Mana!.All(m=>m.Current==42),"Blocked original spell rejected before cost");
        bool cancelled=false;
        for(uint seed=1;seed<=40 && !cancelled;seed++)
        {
            var duel=Place(s,("hero-1",1,0),("hero-2",2,1),(s.Enemy.Id,2,0),("Training Duelist 2",7,7),("Training Duelist 3",7,6));
            var profile=duel.Melee!.Magic!["hero-1"];
            duel=duel with {RandomState=seed,Enemy=duel.Enemy with {Hp=1},Party=duel.Party.SetItem(1,duel.Party[1] with {Mana=duel.Party[0].Mana}),
                Melee=duel.Melee with {Magic=duel.Melee.Magic.SetItem("hero-2",profile),Party=duel.Melee.Party.SetItem("hero-1",duel.Melee.Party["hero-1"] with {Attributes=duel.Melee.Party["hero-1"].Attributes with {Speed=125,Senses=125}})}};
            var r=TacticalCombat.Advance(duel,catalog,spells:spells,casts:[new("hero-2",5,1,duel.Enemy.Id)]);
            cancelled=r.State.Enemy.Hp==0 && r.Events.Any(e=>e.StartsWith("Spell cancelled")) && r.State.Party[1].Mana!.All(m=>m.Current==42);
        }
        Check(cancelled,"Queued original spell cancels without mana when an earlier ally kills its selected enemy");
        s=s with {Party=s.Party.SetItem(1,s.Party[1] with {Hp=1})};
        for(int power=1;power<=7;power++)
        {
            var healed=TacticalCombat.Advance(s,catalog,spells:spells,casts:[new("hero-1",6,power,"hero-2")]);
            Check(healed.State.Party[1].Hp>1 && healed.State.Party[1].Hp<=36 && healed.State.Party[0].Mana!.Single(m=>m.Realm=="Divine").Current==42-3*power,"Heal Wounds supports ally target and every power in tactical battle");
        }
        return checks;
    }
}
