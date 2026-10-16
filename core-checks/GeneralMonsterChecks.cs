using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class GeneralMonsterChecks
{
    public static MonsterCatalog Fixture(Action<byte[]> edit)
    {
        var original=MonsterCombat.FixtureCatalog();var row=original.Monsters[0];var raw=Convert.FromHexString(row.RawRecordHex);edit(raw);
        var data=new byte[667];BinaryPrimitives.WriteInt32LittleEndian(data,1);raw.CopyTo(data,4);
        string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));string hash=Hash(data);
        return original with {DatabaseSha256=hash,Provenance=original.Provenance with {Sha256=hash},Monsters=[row with {RawRecordHex=Convert.ToHexString(raw),RecordSha256=Hash(raw)}]};
    }
    public static int Run(MonsterCatalog? originals=null)
    {
        int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool failed=false;try{action();}catch(ArgumentException){failed=true;}catch(InvalidOperationException){failed=true;}catch(InvalidDataException){failed=true;}Check(failed,name);}
        var items=MeleeTraining.Catalog();
        if(originals is not null)
        {
            int ready=0,blocked=0;var blockers=new Dictionary<string,int>();
            foreach(var row in originals.Monsters)
            {
                var capability=MonsterCombat.Capability(row);
                var snapshot=new MonsterRecordSnapshot(row.Id,originals.DatabaseSha256,row.RecordSha256,row.RawRecordHex,3);
                if(!capability.Ready)
                {
                    blocked++;foreach(var reason in capability.Blockers.Select(s=>s.Contains(':')?s[(s.IndexOf(':')+1)..]:s).Distinct())blockers[reason]=blockers.GetValueOrDefault(reason)+1;
                    Reject(()=>MonsterCombat.Compile(snapshot),"Every blocked row fails complete profile compilation");continue;
                }
                ready++;var d=MonsterCombat.Compile(snapshot);Check(d.Attacks.Length>0 && d.Attacks.All(a=>a.Modes.All(m=>m is not (OriginalAttackMode.Throw or OriginalAttackMode.Shoot))),"Ready profile retains every enabled supported attack/mode");
                var start=MonsterCombat.Create(8,1,items,originals,row.Id);var close=TacticalCombatChecks.Place(start,(start.Enemy.Id,2,0));
                var waits=close.Party.Select(h=>new TacticalOrder(h.Id,TacticalAction.Wait)).ToArray();
                var round=TacticalCombat.Advance(close,items,waits);Check(round.State.Round==1,"Every ready original record executes a real combat round");
                Check(Game.Save(TacticalCombat.Advance(round.State,items).State)==Game.Save(TacticalCombat.Advance(Game.Load(Game.Save(round.State),items),items).State),"Every ready original record resumes exactly after save/load");
                Check(Game.Save(start)==Game.Save(CompanionReplay(start)),"All original actor snapshots preserve caller state");
                GameState CompanionReplay(GameState state){_=TacticalCombat.Advance(state,items);return state;}
            }
            Check(ready+blocked==originals.Count,"All original records receive readiness or explicit blockers");
            var admitted=originals.Monsters.Where(MonsterCombat.Supported).Take(8).Select(m=>m.Id).ToArray();
            if(admitted.Length>0)
            {
                var mixed=MonsterCombat.CreateRoster(8,items,originals,admitted);Check(admitted.Select((id,i)=>mixed.Melee!.Field!.Monsters![$"monster-{i+1}"].Record.DefinitionId==id).All(ok=>ok),"Mixed native roster retains actor count");
                for(int n=0;n<8 && !mixed.RewardGranted && mixed.Party.Any(h=>h.Hp>0);n++)mixed=TacticalCombat.Advance(mixed,items).State;
                Check(Game.Save(Game.Load(Game.Save(mixed),items))==Game.Save(mixed),"Mixed original roster saves after repeated rounds");
            }
            Console.WriteLine($"PASS: audited {originals.Count} originals; {ready} physical profiles ready, {blocked} blocked: "+string.Join(", ",blockers.OrderBy(p=>p.Key).Select(p=>$"{p.Key}={p.Value}")));
            return checks;
        }
        bool exhaustive=true;
        for(int score=-30;score<=265;score++)for(int count=0;count<=15;count++)
        {
            int expected=score>=100?0:Math.Clamp((int)Math.Truncate(Math.Pow(100-score,3)*1e-5+.5)*count/10,0,100);
            exhaustive &= OriginalMonsterFumble.Threshold(score,count)==expected;
        }
        Check(exhaustive,"Cubic fumble arithmetic matches decoded native double constants over the complete neutral domain");
        for(int threshold=0;threshold<=100;threshold++)for(int roll=0;roll<=99;roll++)
            if(OriginalMonsterFumble.Redirects(threshold,roll)!=(roll>=5 && roll+1>100-threshold))throw new Exception("Fumble contact boundary");
        Check(true,"All fumble threshold/contact boundaries and natural-auto exemption");
        int time=90;bool timeline=true;for(int remaining=254;remaining>0;remaining--){int? next=OriginalInitiative.NextMonsterGroupTime(time,0,time,remaining);timeline &= next>=time && next<=100;time=next!.Value;}
        Check(timeline && OriginalInitiative.NextMonsterGroupTime(100,0,100,1)==100,"Byte-sized monster group budgets remain finite and monotonic at the round boundary");
        var input=new MonsterPhysicalScoreInput(200,0,0,new(50,60,70,80),new(50,60,70,80),OriginalAttackMode.Swing,0,0,0,MonsterAttackFatiguePolicy.FullHitOnly);
        foreach(var mode in Enum.GetValues<OriginalAttackMode>().Where(m=>m is not (OriginalAttackMode.Throw or OriginalAttackMode.Shoot)))
        {
            int times=mode==OriginalAttackMode.Berserk?2:1;
            var hit=OriginalMonsterStrike.Resolve(input with {Mode=mode},new(1,1,6),0,Enumerable.Repeat(0,times).ToArray());
            Check(hit.Damage!.Damage==2*times,"All supported melee modes use the native neutral repetition count");
        }
        Check(OriginalMonsterStrike.Resolve(input,new(16,0,0),0,[],50).Damage!.Damage==8,"Physical absorption halves native damage after rounding");
        Check(OriginalMonsterStrike.Resolve(input,new(16,0,0),0,[],100).Damage!.Damage==0,"Full absorption may reduce native damage to zero");
        var redirectScores=OriginalMonsterScores.Build(input with {Defense=100,ArmorDefense=0});
        Check(OriginalMonsterStrike.Contact(redirectScores,99,true,true)==PhysicalStrikeOutcome.Damage,"Redirected contact forces hit and clears natural-miss gate");
        redirectScores=OriginalMonsterScores.Build(input with {ArmorDefense=100});
        Check(OriginalMonsterStrike.Contact(redirectScores,0,true,true)==PhysicalStrikeOutcome.NotPenetrated,"Redirected fresh natural one grants no automatic penetration");
        var multi=Fixture(b=>{b[0xfc]=0xef;Array.Copy(b,0xe7,b,0x109,34);b[0x109+4]=110;b[0x109+21]=3;b[0xcf]=50;});multi.Validate();
        Check(MonsterCombat.Supported(multi.Monsters[0]),"Structural capabilities admit multi-slot/mode/kind/absorption without a species allowlist");
        var s=MonsterCombat.CreateRoster(3,items,multi,[0,0,0]);var compiled=MonsterCombat.Compile(s.Melee!.Field!.Monsters![s.Enemy.Id].Record);
        Check(compiled.Attacks.Length==2 && compiled.Attacks[0].Modes.Length==7 && compiled.Absorption==50,"All active slots/modes and absorption compile from their raw bytes");
        var actionModes=new HashSet<string>();
        for(uint seed=1;seed<=80;seed++)
        {
            var close=TacticalCombatChecks.Place(s with {RandomState=seed},("monster-1",2,0),("monster-2",7,6),("monster-3",7,7));
            var result=TacticalCombat.Advance(close,items,close.Party.Select(h=>new TacticalOrder(h.Id,TacticalAction.Wait)).ToArray());
            foreach(var mode in compiled.Attacks.SelectMany(a=>a.Modes))if(result.Events.Any(e=>e.Contains($"· {mode} ·")))actionModes.Add(mode.ToString());
            Check(Game.Save(result.State)==Game.Save(TacticalCombat.Advance(Game.Load(Game.Save(close),items),items,close.Party.Select(h=>new TacticalOrder(h.Id,TacticalAction.Wait)).ToArray()).State),"Mode selection and combat RNG replay exactly");
        }
        Check(actionModes.Count==7,"Live execution reaches all seven supported melee modes");
        foreach(var edit in new Action<byte[]>[]{b=>b[0xe1]=1,b=>b[0xe7+5]=1,b=>b[0xea]=3,b=>b[0xfc]=16,b=>b[0xe6]=0})
        {
            var bad=Fixture(edit);Check(!MonsterCombat.Capability(bad.Monsters[0]).Ready,"Incomplete actions/budgets produce explicit capability blockers");
            Reject(()=>MonsterCombat.Create(3,2,items,bad,0),"Actor factory never drops an unsupported action silently");
        }
        var unknownHash=multi.Monsters[0] with {RecordSha256=new string('0',64)};Check(!MonsterCombat.Supported(unknownHash),"Readiness rejects mismatched raw digest");
        var low=Fixture(b=>{b[0xeb]=0;b[0xfc]=1;b[0xe5]=4;});
        bool fumble=false;
        for(uint seed=1;seed<=400 && !fumble;seed++)
        {
            var start=MonsterCombat.Create(3,2,items,low,0) with {RandomState=seed};start=TacticalCombatChecks.Place(start,("monster-1",3,1),("monster-2",4,2),("hero-1",2,0),("hero-2",2,2),("hero-3",0,6));
            var result=TacticalCombat.Advance(start,items,start.Party.Select(h=>new TacticalOrder(h.Id,TacticalAction.Wait)).ToArray());
            fumble=result.Events.Any(e=>e.Contains("fumble redirects"));
        }
        Check(fumble,"Low-score live monster attack can redirect into an externally resolved alternate target");
        var save=JsonNode.Parse(Game.Save(s))!;save["Schema"]=7;Reject(()=>Game.Load(save.ToJsonString(),items),"Schema-seven saves cannot carry version-two profiles");
        var legacy=MonsterCombat.Create(2,2,items,MonsterCombat.FixtureCatalog(),0,profileVersion:1);var previous=JsonNode.Parse(Game.Save(legacy))!;previous["Schema"]=7;
        foreach(var actor in previous["Melee"]!["Field"]!["Monsters"]!.AsObject())actor.Value!["Record"]!.AsObject().Remove("ProfileVersion");
        var migrated=Game.Load(previous.ToJsonString(),items);Check(migrated.Melee!.Field!.Monsters!.Values.All(m=>m.Record.ProfileVersion==1),"Old record snapshots retain their legacy execution contract");
        Check(Game.Save(TacticalCombat.Advance(legacy,items).State)==Game.Save(TacticalCombat.Advance(migrated,items).State),"Legacy HP/choice/fumble RNG continuation remains unchanged");
        return checks;
    }
}
