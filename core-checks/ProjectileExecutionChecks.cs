using System.Collections.Immutable;
using System.Text.Json.Nodes;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class ProjectileExecutionChecks
{
    public static int Run(ItemDefinition prototype)
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name)
        {
            bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}
            catch(InvalidDataException){rejected=true;}catch(ArgumentException){rejected=true;}
            Check(rejected,name);
        }
        ItemDefinition Definition(int id,int mode,int type,int icon,int amount=0)=>SourceFixture.Item(prototype with {
            Id=id,RaceMask=65535,ProfessionMask=32767,GenderMask=3,Name=$"Projectile fixture {id}",TypeCode=type,IconIndex=icon,AttackModes=icon==132?0:256,Cursed=false,
            Instance=new(mode,amount,0,0,100,0,new string('0',538))});
        var arrow=Definition(1,1,4,22);var gun=Definition(2,4,3,104,5);var fuel=Definition(3,2,4,132,3);
        var bow=Definition(4,0,3,11);var melee=Definition(5,0,0,1);
        var catalog=new ItemCatalog(3,"DATABASES/ITEMS.DBS",new("loose","synthetic-projectile-execution",new string('a',64)),269,5,[arrow,gun,fuel,bow,melee]);
        catalog.Validate();var rules=Inventory.Rules(catalog);
        var bag=Inventory.BagLocation;
        GameState Start()=>Game.Create(2) with {Items=new(1,20,[new("ammo",1,3,true,null,false,bag),
            new("other",1,4,true,null,false,bag),new("gun",2,1,true,1,false,bag),new("fuel",3,1,true,3,false,bag),new("spare",5,1,true,null,false,bag)])};
        var hero=new ProjectileActor(ProjectileActorKind.Hero,"hero-1");
        var ally=new ProjectileActor(ProjectileActorKind.Hero,"hero-2");
        var enemy=new ProjectileActor(ProjectileActorKind.Enemy,Start().Enemy.Id);
        var aim=new ProjectileAimInput(new(0,0,0),new(100,0,0),80);
        var strike=new ProjectileStrikeInput(OriginalAttackMode.Shoot,ProjectileContactKind.Contact,100,new(0,1,6));
        ProjectileActionCreated Begin(GameState state,int swings=3,string source="ammo",ProjectileCost? cost=null,ProjectileActor? actor=null)=>
            ProjectileExecution.Begin(state,actor??hero,source,cost??new(1,ProjectileCostKind.Quantity),swings,catalog);
        GameState Launch(ProjectileActionCreated action)=>ProjectileExecution.Launch(action.State,action.ActionId,aim,90,48,catalog);
        ProjectileExecutionResult Finish(GameState state,string id,int ordinal=1,ProjectileActor? target=null)=>
            ProjectileExecution.Complete(state,id,ordinal,target??enemy,strike,99,[5],true,catalog);
        ProjectileSeriesState Series(GameState state,string id)=>state.ProjectileActions![id].Series;
        var start=Start();string startJson=Game.Save(start);
        var created=Begin(start);var flight=Launch(created);
        Check(Game.Save(start)==startJson && flight.Items==created.State.Items,"Launch reserves a source without spending or changing prior game state");
        Check(Series(flight,created.ActionId).Pending?.Aim is not null,"Game state stores resolved aim with the pending projectile");
        var json=JsonNode.Parse(Game.Save(flight))!;
        Check(json["ProjectileActions"]![created.ActionId]!["Series"]!["Inventory"] is null &&
            typeof(ProjectileSeriesState).GetProperties().All(p=>p.PropertyType!=typeof(ItemInventoryState)),"Persisted series cannot own another inventory snapshot");
        // Real canonical inventory operations run while delivery is pending.
        var during=Inventory.Split(flight,"other",2,catalog);
        during=Equipment.EquipInstance(during,"hero-2","spare",catalog);
        during=Inventory.Add(during,5,2,catalog);
        during=Inventory.SpendResource(during,"fuel",1,catalog);
        during=during with {Round=7,RandomState=12345,Party=during.Party.SetItem(1,during.Party[1] with {Hp=12})};
        string duringJson=Game.Save(during);
        var completed=Finish(during,created.ActionId);
        Check(completed.State.Items!.Items.Single(i=>i.Id=="ammo").Quantity==2 && completed.AppliedDamage==6 && completed.State.Enemy.Hp==26,
            "Current-state completion applies damage and exactly one arrow cost together");
        Check(completed.State.Items.Items.Single(i=>i.Id=="spare").Location.ContainerId=="hero-2" &&
            completed.State.Items.Items.Where(i=>i.Id!="ammo").SequenceEqual(during.Items!.Items.Where(i=>i.Id!="ammo")),
            "Unrelated split, transfer and newly acquired loot survive completion");
        Check(completed.State.Party[1].Hp==12 && completed.State.Round==7 && completed.State.RandomState==12345 &&
            completed.State.NextItemSequence==during.NextItemSequence,"Unrelated actor, turn, identity and RNG changes survive completion");
        Check(Game.Save(during)==duringJson,"Successful atomic completion leaves previous snapshot unchanged");
        Reject(()=>Finish(completed.State,created.ActionId),"Duplicate callback against current state cannot damage or spend twice");
        var next=ProjectileExecution.Launch(completed.State,created.ActionId,aim,0,0,catalog);
        Reject(()=>Finish(next,created.ActionId),"Delayed callback cannot finish a later ordinal");
        Reject(()=>Finish(flight,"foreign"),"Foreign action callback is rejected");
        Reject(()=>Finish(flight,created.ActionId,target:hero with {Id="missing"}),"Unknown collision actor rejects the whole transaction");
        Reject(()=>ProjectileExecution.Complete(flight,created.ActionId,1,enemy,strike,99,[],true,catalog),"Invalid draw plan cannot partly spend the source");
        Check(Game.Save(during)==duringJson,"Rejected callbacks leave current snapshot intact");
        Reject(()=>Inventory.Split(flight,"ammo",1,catalog),"Canonical splitting cannot change a reserved source");
        Reject(()=>Inventory.Remove(flight,1,1,catalog),"Canonical consumption cannot spend a reserved source");
        Reject(()=>Game.Validate(flight with {Items=ItemInventory.ConsumeUnit(flight.Items!,"ammo",rules)}),"Source mismatch is detected even if a caller bypasses inventory commands");
        Reject(()=>ProjectileExecution.Stop(flight,created.ActionId,ProjectileStopReason.ActorUnavailable,catalog),"Interruption cannot discard an in-flight cost");
        Reject(()=>ProjectileExecution.Forget(flight,created.ActionId,catalog),"Pending action cannot be retired before completion");
        var secondActor=Begin(flight,actor:ally);
        Reject(()=>Launch(secondActor),"Two actors cannot reserve the same source simultaneously");
        Reject(()=>Begin(flight),"Actor cannot start another active series");
        var parallel=Begin(flight,source:"other",actor:ally);
        var parallelFlight=Launch(parallel);
        var parallelFirst=Finish(parallelFlight,created.ActionId).State;
        var parallelLast=Finish(parallelFirst,parallel.ActionId).State;
        Check(parallelLast.Items!.Items.Single(i=>i.Id=="ammo").Quantity==2 && parallelLast.Items.Items.Single(i=>i.Id=="other").Quantity==3 &&
            parallelLast.Enemy.Hp==20,"Distinct in-flight sources finish against the latest shared inventory and HP");
        var serialized=Game.Save(during);var loaded=Game.Load(serialized,catalog);
        Check(Game.Save(loaded)==serialized,"Full pending game roundtrips with source reservation, aim, RNG and catalog identity");
        Check(Game.Save(Finish(loaded,created.ActionId).State)==Game.Save(completed.State),"Save/load continuation matches uninterrupted completion without re-aiming or extra spending");
        Reject(()=>Game.Load(serialized),"Pending actions cannot load without their catalog");
        Reject(()=>Game.Load(serialized,catalog with {Provenance=catalog.Provenance with {Sha256=new string('b',64)}}),"Wrong content identity cannot resume a flight");
        var unavailable=flight with {Party=flight.Party.SetItem(0,flight.Party[0] with {Hp=0})};
        var deadFinish=Finish(unavailable,created.ActionId);
        Check(deadFinish.State.Items!.Items.Single(i=>i.Id=="ammo").Quantity==2 && Series(deadFinish.State,created.ActionId).StopReason==ProjectileStopReason.ActorUnavailable,
            "Actor death after launch still pays for the shot and stops continuation");
        var self=Finish(flight with {Party=flight.Party.SetItem(0,flight.Party[0] with {Hp=1})},created.ActionId,target:hero);
        Check(self.AppliedDamage==1 && self.State.Party[0].Hp==0 && Series(self.State,created.ActionId).StopReason==ProjectileStopReason.ActorUnavailable,
            "Actual self-collision clamps damage and stops the now-dead actor");
        var redirected=Finish(flight,created.ActionId,target:ally);
        Check(redirected.State.Party[1].Hp==30 && redirected.State.Enemy.Hp==32,"Actual collided ally receives damage instead of the originally aimed enemy");
        foreach(var contact in new[]{ProjectileContactKind.Miss,ProjectileContactKind.Intercepted})
        {
            var result=ProjectileExecution.Complete(flight,created.ActionId,1,contact==ProjectileContactKind.Miss?null:enemy,
                strike with {Contact=contact},null,[],false,catalog);
            Check(result.AppliedDamage==0 && result.State.Enemy.Hp==32 && result.State.Items!.Items.Single(i=>i.Id=="ammo").Quantity==2 &&
                Series(result.State,created.ActionId).StopReason==ProjectileStopReason.ContinuationUnavailable,"No-damage delivery spends once and respects continuation interruption");
        }
        var armor=ProjectileExecution.Complete(flight,created.ActionId,1,enemy,strike with {PenetrationScore=0},99,[],true,catalog);
        Check(armor.Strike.Outcome==ProjectileStrikeOutcome.NotPenetrated && armor.AppliedDamage==0 && armor.State.Items!.Items.Single(i=>i.Id=="ammo").Quantity==2,
            "Armor stopping contact still commits one projectile cost");
        var stopped=ProjectileExecution.Stop(created.State,created.ActionId,ProjectileStopReason.ActorUnavailable,catalog);
        Check(stopped.Items==created.State.Items && Series(stopped,created.ActionId).Completed==0,"Pre-launch interruption costs nothing in canonical state");
        var retired=ProjectileExecution.Forget(stopped,created.ActionId,catalog);
        var renewed=Begin(Game.Load(Game.Save(retired),catalog));
        Check(renewed.ActionId!=created.ActionId && renewed.State.NextProjectileSequence==3,"Retired action IDs are never reused");
        Reject(()=>Finish(renewed.State,created.ActionId),"Retired callback cannot attach to a new action");
        var gunAction=Begin(Start(),source:"gun",cost:new(2,ProjectileCostKind.Resource));
        var empty=Finish(Launch(gunAction),gunAction.ActionId).State;
        Check(Series(empty,gunAction.ActionId).StopReason==ProjectileStopReason.Depleted && empty.Items!.Items.Single(i=>i.Id=="gun").ResourceRemaining==0,
            "Retained gun depletes inside authoritative state");
        Reject(()=>Begin(empty),"Depleted unfinished action keeps its actor allocation until reloaded or explicitly retired");
        var refilled=ProjectileExecution.RefillFromReserves(Game.Load(Game.Save(empty),catalog),gunAction.ActionId,["fuel"],catalog);
        Check(refilled.Items!.Items.Single(i=>i.Id=="gun").ResourceRemaining==3 && refilled.Items.Items.All(i=>i.Id!="fuel") &&
            Series(refilled,gunAction.ActionId).Completed==1,"Reload consumes current fuel and preserves action allocation and ordinal");
        var gunAgain=ProjectileExecution.Launch(refilled,gunAction.ActionId,aim,0,0,catalog);
        var gunDone=Finish(gunAgain,gunAction.ActionId,2).State;
        Check(gunDone.Items!.Items.Single(i=>i.Id=="gun").ResourceRemaining==2,"Reloaded shot spends one current resource after save/load");
        var disposable=Begin(Start(),source:"fuel",cost:new(3,ProjectileCostKind.Resource,true));
        var disposableState=disposable.State;
        for(int i=1;i<=3;i++)
        {
            disposableState=ProjectileExecution.Launch(disposableState,disposable.ActionId,aim,0,0,catalog);
            disposableState=Finish(disposableState,disposable.ActionId,i).State;
        }
        Check(disposableState.Items!.Items.All(i=>i.Id!="fuel") && Series(disposableState,disposable.ActionId).StopReason==ProjectileStopReason.Finished,
            "Disposable resource is removed only after its last completed shot");
        var thrown=Begin(Start() with {Items=new(1,4,[new("ammo",1,1,true,null,false,new("hero-1",Equipment.WeaponSlot))])},swings:1);
        var throwDone=Finish(Launch(thrown),thrown.ActionId);
        Check(throwDone.State.Items!.Items.Length==0 && throwDone.AppliedDamage==6,"Equipped singleton removal and HP damage commit atomically");
        var enemyShot=Begin(Start(),actor:enemy,source:"other");
        var enemyHit=Finish(Launch(enemyShot),enemyShot.ActionId,target:hero);
        Check(enemyHit.State.Party[0].Hp==30 && enemyHit.State.Enemy.Hp==32 && enemyHit.State.Items!.Items.Single(i=>i.Id=="other").Quantity==3,
            "Enemy-side command applies current hero HP and its resolved source cost in one transition");
        var invalidAim=flight.ProjectileActions![created.ActionId];
        var pending=invalidAim.Series.Pending!;
        var badAim=flight with {ProjectileActions=flight.ProjectileActions.SetItem(created.ActionId,invalidAim with {
            Series=invalidAim.Series with {Pending=pending with {Aim=pending.Aim! with {RadiusDraw=null}}}})};
        Reject(()=>Game.Validate(badAim),"Invalid persisted draw metadata cannot resume an aimed flight");
        var overflow=Start() with {NextProjectileSequence=long.MaxValue};
        bool overflowed=false;try{Begin(overflow);}catch(OverflowException){overflowed=true;}
        Check(overflowed && overflow.ProjectileActions is null,"Identity overflow cannot publish a partially created action");
        var old=JsonNode.Parse(Game.Save(Start()))!.AsObject();old["Schema"]=2;old.Remove("ProjectileActions");old.Remove("NextProjectileSequence");
        var migrated=Game.Load(old.ToJsonString(),catalog);
        Check(migrated.Schema==3 && migrated.ProjectileActions is null && migrated.NextProjectileSequence==1 && migrated.Items!.Items.SequenceEqual(Start().Items!.Items),
            "Schema-two saves migrate without invented projectiles or resource changes");
        var downgraded=JsonNode.Parse(serialized)!;downgraded["Schema"]=2;
        Reject(()=>Game.Load(downgraded.ToJsonString(),catalog),"Pending actions cannot hide inside a downgraded save version");
        var unsupported=flight with {ProjectileActions=flight.ProjectileActions!.SetItem(created.ActionId,
            flight.ProjectileActions[created.ActionId] with {Ruleset="future-rules"})};
        Reject(()=>Game.Validate(unsupported),"Unsupported saved projectile ruleset is rejected instead of reinterpreted");
        var wrongSequence=flight with {NextProjectileSequence=1};
        Reject(()=>Game.Validate(wrongSequence),"Saved sequence cannot reuse an action identity");
        return checks;
    }
}
