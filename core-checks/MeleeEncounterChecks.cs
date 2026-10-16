using System.Collections.Immutable;
using System.Text.Json.Nodes;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class MeleeEncounterChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name)
        {
            bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(InvalidDataException){rejected=true;}catch(ArgumentException){rejected=true;}
            Check(rejected,name);
        }
        var catalog=MeleeTraining.Catalog();var start=MeleeTraining.Create(1,catalog);
        RoundResult Step(GameState state)=>Game.Attack(state,catalog,3);
        string before=Game.Save(start);var first=Step(start);
        Check(first.State.Round==1 && first.State.RandomState!=start.RandomState && first.Events.Any(e=>e.Contains("d100")),"Playable melee round consumes RNG and reports real contact thresholds");
        Check(Game.Save(start)==before,"Full original-rule round preserves the source snapshot");
        Check(start.Melee!.Party["hero-1"].History[0].Levels==10 && Equipment.Instance(start,"hero-1") is not null &&
            start.Items!.Items.Single(i=>i.Id==start.Melee.OpponentWeaponId).Location.ContainerId==start.Enemy.Id,
            "Authored facts and both real owned weapons are explicit, not Hero.Power placeholders");
        var changedPower=start with {Party=start.Party.SetItem(0,start.Party[0] with {Power=999,Speed=0}),Enemy=start.Enemy with {Power=999,Speed=125}};
        var changedResult=Step(changedPower);
        Check(changedResult.State.Party[0].Hp==first.State.Party[0].Hp && changedResult.State.Enemy.Hp==first.State.Enemy.Hp &&
            changedResult.State.RandomState==first.State.RandomState,"Legacy Power and Speed fields do not secretly drive restored combat facts");
        Check(Game.Save(Game.Load(Game.Save(first.State),catalog))==Game.Save(first.State),"Authored melee facts, inventory and RNG survive save/load");
        Check(Game.Save(Step(Game.Load(Game.Save(first.State),catalog)).State)==Game.Save(Step(first.State).State),"Next melee round resumes exactly after save/load");
        Reject(()=>Game.Load(before),"Restored melee requires its catalog on load");
        Reject(()=>Game.Load(before,catalog with {Provenance=catalog.Provenance with {Sha256=new string('b',64)}}),"Wrong catalog identity cannot resume restored melee");
        Reject(()=>Game.Attack(start,catalog),"Melee command requires resolved world distance");
        foreach(double distance in new[]{double.NaN,double.PositiveInfinity,-1,4.51})Reject(()=>Game.Attack(start,catalog,distance),"Invalid/out-of-range command cannot consume a round or RNG");
        Check(Game.Save(start)==before,"Denied distance leaves inventory, actors and RNG intact");
        var unarmed=Equipment.Unequip(start,"hero-1",catalog);
        var savedUnarmed=Game.Load(Game.Save(unarmed),catalog);
        Reject(()=>Step(savedUnarmed),"Missing weapon is a command denial, not invented unarmed damage");
        Check(savedUnarmed.Party[0].Hp==36 && savedUnarmed.Round==0,"Unarmed saves remain valid and do not execute combat");
        var rearmed=Equipment.Equip(savedUnarmed,"hero-1",0,catalog);
        Check(Game.Save(Step(rearmed).State)==Game.Save(first.State),"Re-equipping the same owned weapon restores exact round behavior");
        Reject(()=>Game.Cast(start,new("hero-1","test-spark",1,start.Enemy.Id),catalog),"Synthetic spells cannot silently execute fixture combat inside restored melee");
        Check(Magic.Denial(start,new("hero-1","test-spark",1,start.Enemy.Id)) is not null,"Magic availability reports the same restored-combat boundary");
        var captive=Game.CaptureFirst(start);
        Check(ReferenceEquals(Step(captive).State,captive),"No eligible actor means no melee round and no RNG");
        var exited=Game.Escape(captive);
        Check(Step(exited).State.Round==1,"Captivity/escape preserve effective combat facts");
        var low=start with {Melee=start.Melee with {Party=start.Melee.Party.SetItem("hero-1",start.Melee.Party["hero-1"] with {
            Attributes=new(0,0,0,0),Strength=0,CombatSkill=0,WeaponSkill=0,History=[new(OriginalProfession.Mage,1)]})}};
        Reject(()=>Step(low),"Unimplemented low-score fumble path is rejected instead of replaced with fixture damage");
        var unknown=start with {Melee=start.Melee with {Ruleset="future-melee"}};
        Reject(()=>Game.Validate(unknown),"Unknown melee ruleset cannot reinterpret a saved battle");
        var noActor=start with {Melee=start.Melee with {Party=ImmutableDictionary<string,MeleeCharacterFacts>.Empty}};
        Reject(()=>Game.Validate(noActor),"Missing actor facts cannot acquire fabricated defaults");
        var old3=JsonNode.Parse(Game.Save(Game.Create(1)))!.AsObject();old3["Schema"]=3;old3.Remove("Melee");
        var migrated=Game.Load(old3.ToJsonString());
        Check(migrated.Schema==Game.SaveSchema && migrated.Melee is null && Game.Attack(migrated).State.Enemy.Hp==25,"Schema-three legacy battle remains legacy after migration");
        var downgraded=JsonNode.Parse(before)!;downgraded["Schema"]=3;
        Reject(()=>Game.Load(downgraded.ToJsonString(),catalog),"New actor/ruleset fields cannot hide in downgraded schema");
        var outcomes=new HashSet<string>();
        for(uint seed=1;seed<=40;seed++)
        {
            var seeded=start with {RandomState=seed};var result=Step(seeded);
            foreach(var e in result.Events)
                foreach(var outcome in new[]{"Miss,","NotPenetrated,","Damage,"})if(e.Contains(outcome))outcomes.Add(outcome);
            Check(result.State.Party.All(h=>h.Hp>=0 && h.Hp<=h.MaxHp) && result.State.Enemy.Hp>=0,"Recovered roll outcomes never heal or underflow HP");
        }
        Check(outcomes.SetEquals(new[]{"Miss,","NotPenetrated,","Damage,"}),"Live executor actually reaches miss, armor stop and damage outcomes: "+string.Join("|",outcomes));
        for(int size=1;size<=8;size++)
        {
            var state=MeleeTraining.Create(size,catalog);var original=Game.Save(state);string lootId=state.Melee!.OpponentWeaponId!;
            for(int round=0;round<100 && state.Enemy.Hp>0 && state.Party.Any(h=>h.Hp>0 && !h.Captive);round++)state=Step(state).State;
            Check(state.Enemy.Hp==0 || state.Party.All(h=>h.Hp==0 || h.Captive),$"Authored melee scenario {size} ends rather than running fixture Power forever");
            Check(Game.Save(MeleeTraining.Create(size,catalog))==original,$"Scenario {size} construction is deterministic");
            if(state.Enemy.Hp==0)
            {
                Check(state.RewardGranted && state.Party.Sum(h=>h.Xp)==120,$"Scenario {size} grants exactly one XP pool");
                Check(state.Items!.Items.Count(i=>i.Id==lootId)==1 && state.Items.Items.Single(i=>i.Id==lootId).Location==Inventory.BagLocation &&
                    state.Items.Items.Length==size+1 && state.Melee!.OpponentWeaponId is null,"Victory transfers the opponent's exact item without cloning it");
                string victory=Game.Save(state);
                Check(Game.Save(Step(Game.Load(victory,catalog)).State)==victory,"Completed battle cannot award XP/loot or advance RNG again");
            }
        }
        var two=MeleeTraining.Create(2,catalog);
        two=two with {Party=two.Party.SetItem(0,two.Party[0] with {Hp=0})};
        var survivor=Step(two);
        Check(survivor.Events.All(e=>!e.StartsWith(two.Party[0].Name+" →")),"Dead queued actors never execute restored strikes");
        return checks;
    }
    public static int RunCatalog(ItemCatalog catalog)
    {
        int checks=0;
        for(int size=1;size<=8;size++)
        {
            var state=MeleeTraining.Create(size,catalog);
            var round=Game.Attack(state,catalog,3);
            if(round.State.Round!=1 || !round.Events.Any(e=>e.Contains("d100")))throw new Exception("Imported catalog did not enter restored melee");
            checks++;
            if(Game.Save(Game.Load(Game.Save(round.State),catalog))!=Game.Save(round.State))throw new Exception("Imported melee round failed persistence");
            checks++;
        }
        Console.WriteLine("PASS: restored melee with original owned weapons for parties 1–8 and saved effective facts");
        return checks;
    }

}
