using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class MonsterCombatChecks
{
    public static int Run(MonsterCatalog? originals=null)
    {
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool failed=false;try{action();}catch(ArgumentException){failed=true;}catch(InvalidOperationException){failed=true;}catch(InvalidDataException){failed=true;}Check(failed,name);}
        var items=MeleeTraining.Catalog();var catalog=originals??MonsterCombat.FixtureCatalog();var ids=originals is null?new[]{0}:catalog.Monsters.Where(m=>m.Id is 18 or 282).Select(m=>m.Id).ToArray();
        foreach(int id in ids)
        {
            var s=MonsterCombat.Create(3,3,items,catalog,id,profileVersion:1);string source=Game.Save(s);var native=s.Melee!.Field!.Monsters!;var d=MonsterCombat.Compile(native[s.Enemy.Id].Record);
            Check(s.Melee.Opponent is null && s.Melee.Field.Opponents.All(o=>o.Facts is null && o.WeaponId is null),"Native monster has no invented character facts or weapon");
            Check(s.Items!.Items.Length==3 && s.Items.Items.All(i=>s.Party.Any(h=>h.Id==i.Location.ContainerId)),"Only party-owned weapons exist in a native battle");
            Check(TacticalCombat.Enemies(s).All(e=>e.Hp>=d.Health.Constant+d.Health.Dice && e.Hp<=d.Health.Constant+d.Health.Dice*d.Health.Sides && e.XpReward==0),"Native HP rolls stay within imported dice; no invented XP");
            var setup=MeleeTraining.CreateTactical(3,3,items,ai:false);uint rng=setup.RandomState;
            foreach(var e in TacticalCombat.Enemies(s))
            {
                int rolled=d.Health.Constant;for(int n=0;n<d.Health.Dice;n++)rolled+=Dice.Roll(ref rng,d.Health.Sides);
                Check(e.Hp==rolled && native[e.Id].MaxHp==rolled && s.Melee.Field.Brains![e.Id].MaxHp==rolled,"One authoritative HP roll feeds actor and brain maximum");
            }
            Check(s.RandomState==rng,"Monster HP generation has exact externally-owned RNG progression");
            var first=TacticalCombat.Advance(s,items);Check(first.State.Round==1 && first.Events.Any(e=>e.StartsWith("AI monster-")),"Native monsters use the shared tactical AI");
            Check(Game.Save(s)==source,"Monster combat preserves source snapshot");
            Check(Game.Save(TacticalCombat.Advance(first.State,items).State)==Game.Save(TacticalCombat.Advance(Game.Load(Game.Save(first.State),items),items).State),"Private record snapshot HP positions and AI resume exactly");
            var close=TacticalCombatChecks.Place(s,("hero-1",1,0),("hero-2",0,2),("hero-3",0,4),("monster-1",2,0),("monster-2",7,6),("monster-3",7,7));
            var fight=TacticalCombat.Advance(close,items,close.Party.Select(h=>new TacticalOrder(h.Id,TacticalAction.Wait)).ToArray());
            Check(fight.Events.Any(e=>e.Contains("· Thrust ·")),"Original monster attack score and intrinsic damage path executes against hero");
            Check(fight.Events.Count(e=>e.Contains("· Thrust ·"))<=d.Groups*d.MaximumSwings,"Monster group and swing budgets bound attacks independently of classes");
            bool nativeZone=false;
            for(uint seed=1;seed<=6 && !nativeZone;seed++)
            {
                var attack=TacticalCombat.Advance(close with {RandomState=seed},items);
                nativeZone=attack.Events.Any(e=>e.StartsWith("hero-1 → monster-1 · Zone"));
            }
            Check(nativeZone,"Hero attacks use native seven-zone monster defense");
            var primaryDead=s with {Enemy=s.Enemy with {Hp=0}};Check(TacticalCombat.Advance(primaryDead,items).State.Round==1,"Other monsters keep acting after primary death");
            var actor=native[s.Enemy.Id];
            GameState Changed(MonsterCombatant replacement)=>s with {Melee=s.Melee with {Field=s.Melee.Field with {Monsters=native.SetItem(s.Enemy.Id,replacement)}}};
            Reject(()=>Game.Validate(Changed(actor with {MaxHp=100000})),"Reject impossible monster maximum HP");
            Reject(()=>Game.Validate(Changed(actor with {Record=actor.Record with {RawRecordHex=new string('0',1326)}})),"Reject corrupted native record snapshot");
            var raw=Convert.FromHexString(actor.Record.RawRecordHex);raw[0xeb]++;
            Reject(()=>Game.Validate(Changed(actor with {Record=actor.Record with {RawRecordHex=Convert.ToHexString(raw),RecordSha256=Convert.ToHexString(SHA256.HashData(raw))}})),"Rehashed modified record cannot bypass reviewed monster slice");
            Reject(()=>Game.Validate(s with {Melee=s.Melee with {Opponent=setup.Melee!.Opponent}}),"Reject monster/character fact mixing");
            Reject(()=>Game.Validate(s with {Enemy=s.Enemy with {XpReward=120}}),"Reject invented native rewards");
            var legacy=JsonNode.Parse(source)!;legacy["Schema"]=6;Reject(()=>Game.Load(legacy.ToJsonString(),items),"Schema six cannot smuggle in native actors");
            for(uint seed=1;seed<=4;seed++)
            {
                var battle=MonsterCombat.Create(8,3,items,catalog,id,profileVersion:1) with {RandomState=seed};
                for(int round=0;round<160 && !battle.RewardGranted && battle.Party.Any(h=>h.Hp>0);round++)battle=TacticalCombat.Advance(battle,items).State;
                Check(battle.RewardGranted && TacticalCombat.Enemies(battle).All(e=>e.Hp==0),"Full multi-monster battle reaches victory");
                Check(battle.Party.Sum(h=>h.Xp)==0 && !Inventory.Bag(battle).Any() && battle.Items!.Items.Length==8,"Monster victory invents no loot weapon or XP");
                Check(Game.Save(TacticalCombat.Advance(battle,items).State)==Game.Save(battle),"Monster victory is idempotent");
            }
        }
        if(originals is not null)return checks;
        for(int party=1;party<=8;party++)for(int enemies=1;enemies<=8;enemies++)
        {
            var s=MonsterCombat.Create(party,enemies,items,catalog,0,profileVersion:1);var next=TacticalCombat.Advance(s,items).State;
            Check(next.Melee!.Field!.Monsters!.Count==enemies && next.Melee.Field.Positions.Count==party+enemies,"All party/monster group sizes validate");
        }
        var charSave=JsonNode.Parse(Game.Save(MeleeTraining.CreateTactical(2,2,items)))!;charSave["Schema"]=6;charSave["Melee"]!["Field"]!.AsObject().Remove("Monsters");
        var old=Game.Load(charSave.ToJsonString(),items);Check(old.Schema==Game.SaveSchema && old.Melee!.Field!.Monsters is null && old.Melee.Field.Brains is not null,"Schema-six enemy AI migrates without monster facts");
        var input=new MonsterPhysicalScoreInput(140,0,0,new(40,55,60,50),new(50,80,80,70),OriginalAttackMode.Thrust,0,0,0,MonsterAttackFatiguePolicy.FullHitOnly);
        var score=OriginalMonsterScores.Build(input);var damage=new PhysicalDamageDice(-2,1,6);
        Check(OriginalMonsterStrike.Resolve(input,damage,0,[0]).Damage!.Damage==1,"Neutral monster damage floors at one after signed constant dice");
        Check(OriginalMonsterStrike.Resolve(input,damage,95,[]).Outcome==PhysicalStrikeOutcome.Miss,"Monster natural miss consumes no damage dice");
        Reject(()=>OriginalMonsterStrike.Resolve(input,damage,99,[0]),"Missed monster strike rejects extra damage RNG");
        Reject(()=>OriginalMonsterStrike.Contact(OriginalMonsterScores.Build(input with {AttackRating=100}),5),"Low untargeted monster score requires fumble implementation");
        Reject(()=>OriginalMonsterStrike.Resolve(input with {Mode=OriginalAttackMode.Throw},damage,0,[0]),"Reject projectile monster modes");
        return checks;
    }
}
