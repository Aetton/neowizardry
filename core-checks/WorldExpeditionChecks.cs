using System.Collections.Immutable;
using NeoWizardry.Core;
using System.Buffers.Binary;
using System.Security.Cryptography;

public static class WorldExpeditionChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool failed=false;try{action();}catch(Exception e) when(e is InvalidDataException or InvalidOperationException or System.Text.Json.JsonException){failed=true;}Check(failed,name);}
        var items=MeleeTraining.Catalog();var monsters=MonsterCombat.FixtureCatalog();string hash=new('a',64);
        var world=new ExplorationCheckpoint(1,hash,new(1,2,3),new(0,0,0),2,[]);
        ImmutableArray<ExplorationDoorLimit> limits=[];
        string Save(WorldExpeditionState s)=>WorldExpedition.Save(s,hash,limits,items,monsters);
        WorldExpeditionState Load(string json)=>WorldExpedition.Load(json,hash,limits,items,monsters);
        for(int size=1;size<=8;size++)
        {
            var source=WorldExpedition.Create(size,items,monsters,0,world);string initial=Save(source);
            Check(Save(Load(initial))==initial,"Ready expedition roundtrip");
            var active=WorldExpedition.Begin(source);
            Check(Save(source)==initial && active.Phase==WorldEncounterPhase.Active,"Begin is immutable and keeps shared game state");
            Check(ReferenceEquals(active.Game,source.Game),"Entering encounter never recreates party/items or consumes RNG");
            Reject(()=>WorldExpedition.Begin(active),"Active encounter cannot respawn");
            Reject(()=>WorldExpedition.Advance(source,items),"Cannot fight before entering encounter");
            string inventory=System.Text.Json.JsonSerializer.Serialize(source.Game.Items);
            for(int round=0;round<120 && active.Phase==WorldEncounterPhase.Active;round++)
            {
                var loaded=Load(Save(active));
                var direct=WorldExpedition.Advance(active,items).State;
                var resumed=WorldExpedition.Advance(loaded,items).State;
                Check(Save(direct)==Save(resumed),"Mid-battle save resumes exact RNG HP positions AI and inventory");
                active=direct;
            }
            Check(active.Phase==WorldEncounterPhase.Victory,"Fixed fixture encounter can be won by every party size including solo");
            Check(active.Game.Items!.Items.Length==size && System.Text.Json.JsonSerializer.Serialize(active.Game.Items)==inventory,"World victory preserves authoritative inventory without fabricated drops");
            Check(active.Game.Party.Sum(h=>h.Xp)==0,"No fabricated original XP reward");
            var won=Load(Save(active));
            Check(won.Phase==WorldEncounterPhase.Victory && won.Game.RewardGranted && won.Game.Enemy.Hp==0,"Cleared encounter survives restart");
            Reject(()=>WorldExpedition.Begin(won),"Cleared encounter cannot respawn");
            Reject(()=>WorldExpedition.Advance(won,items),"Cleared encounter cannot grant repeat reward");
            Reject(()=>Save(won with {Phase=WorldEncounterPhase.Ready}),"Cannot relabel completed combat as new encounter");
            var dead=source with {Game=source.Game with {Party=source.Game.Party.Select(h=>h with {Hp=0}).ToImmutableArray()},Phase=WorldEncounterPhase.Defeat};
            Check(Load(Save(dead)).Phase==WorldEncounterPhase.Defeat,"Defeat persists without resurrecting party");
            Reject(()=>Save(dead with {Phase=WorldEncounterPhase.Active}),"Defeat cannot be relabeled active");
        }
        var sample=WorldExpedition.Create(1,items,monsters,0,world);
        foreach(var invalid in new[]{sample with {Schema=3},sample with {EncounterId="other"},sample with {ItemDatabaseHash=new('b',64)},
            sample with {MonsterDatabaseHash=new('b',64)},sample with {MonsterDefinitionId=999},sample with {Phase=(WorldEncounterPhase)999},
            sample with {World=world with {ContentHash=new('b',64)}},sample with {Phase=WorldEncounterPhase.Victory}})
            Reject(()=>Save(invalid),"Invalid expedition binding/state rejected");
        Reject(()=>Load("null"),"Null expedition rejected");
        Reject(()=>Load(Save(sample)[..^1]),"Truncated expedition rejected");
        Reject(()=>Load(new string(' ',WorldExpedition.MaximumBytes+1)),"Oversized expedition rejected");

        MonsterCatalog RewardCatalog(uint xp,uint overridden=0,Action<byte[]>? edit=null)
        {
            var row=monsters.Monsters[0];var raw=Convert.FromHexString(row.RawRecordHex);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x181),xp);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x26b),overridden);edit?.Invoke(raw);
            row=row with {RawRecordHex=Convert.ToHexString(raw),RecordSha256=Convert.ToHexString(SHA256.HashData(raw))};
            var db=new byte[4+raw.Length];BinaryPrimitives.WriteInt32LittleEndian(db,1);raw.CopyTo(db,4);
            string digest=Convert.ToHexString(SHA256.HashData(db));
            return monsters with {Monsters=[row],DatabaseSha256=digest,Provenance=monsters.Provenance with {Sha256=digest}};
        }
        MonsterRecordSnapshot Record(MonsterCatalog c)=>new(0,c.DatabaseSha256,c.Monsters[0].RecordSha256,c.Monsters[0].RawRecordHex,3);
        foreach(uint overridden in new uint[]{0,101})for(int size=1;size<=8;size++)
        {
            var catalog=RewardCatalog(794,overridden);int xp=(int)(overridden==0?794:overridden);
            string RewardSave(WorldExpeditionState s)=>WorldExpedition.Save(s,hash,limits,items,catalog);
            var source=WorldExpedition.Create(size,items,catalog,0,world);var active=WorldExpedition.Begin(source);
            string inventory=System.Text.Json.JsonSerializer.Serialize(active.Game.Items);
            for(int round=0;round<120 && active.Phase==WorldEncounterPhase.Active;round++)
            {
                var loaded=WorldExpedition.Load(RewardSave(active),hash,limits,items,catalog);
                var direct=WorldExpedition.Advance(active,items);
                Check(RewardSave(direct.State)==RewardSave(WorldExpedition.Advance(loaded,items).State),"Native XP save continuation includes terminal reward");
                if(direct.State.Phase==WorldEncounterPhase.Victory)
                    Check(direct.Events.Any(e=>e.StartsWith("Native reward:")),"Native XP receipt is visible in combat events");
                active=direct.State;
            }
            Check(active.Rewards is {Granted:true} && active.Game.Party.All(h=>h.Xp==xp/size),"Native division discards remainder for solo through eight heroes");
            Check(System.Text.Json.JsonSerializer.Serialize(active.Game.Items)==inventory,"Empty native treasure leaves every owned instance unchanged");
            Check(source.Rewards is {Granted:false} && source.Game.Party.All(h=>h.Xp==0),"Original expedition remains immutable");
            Reject(()=>WorldExpedition.Advance(active,items),"Native receipt prevents repeat rewards");
            Reject(()=>RewardSave(active with {Rewards=active.Rewards! with {Granted=false}}),"Missing victory receipt rejected");
            Reject(()=>RewardSave(active with {Game=active.Game with {Party=active.Game.Party.SetItem(0,active.Game.Party[0] with {Xp=active.Game.Party[0].Xp+1})}}),"Incorrect XP division rejected");
            Reject(()=>RewardSave(source with {Rewards=null}),"New expedition requires reward receipt");
            // Already-cleared and active 048/049 saves retain their old zero-reward behavior.
            var legacy=source with {Schema=1,Rewards=null};var old=WorldExpedition.Begin(WorldExpedition.Load(RewardSave(legacy),hash,limits,items,catalog));
            for(int round=0;round<120 && old.Phase==WorldEncounterPhase.Active;round++)old=WorldExpedition.Advance(old,items).State;
            Check(old.Phase==WorldEncounterPhase.Victory && old.Rewards is null && old.Game.Party.All(h=>h.Xp==0),"Legacy continuation does not retroactively award native XP");
            Check(RewardSave(WorldExpedition.Load(RewardSave(old),hash,limits,items,catalog))==RewardSave(old),"Legacy completed save roundtrips without new rewards");
        }
        var eligibleCatalog=RewardCatalog(794);var eligibility=WorldExpedition.Create(4,items,eligibleCatalog,0,world);
        var accumulated=WorldExpedition.Create(1,items,eligibleCatalog,0,world);
        accumulated=accumulated with {Game=accumulated.Game with {Party=accumulated.Game.Party.SetItem(0,accumulated.Game.Party[0] with {Xp=500})},
            Rewards=new(accumulated.Rewards!.InitialExperience.SetItem(accumulated.Game.Party[0].Id,500))};
        accumulated=WorldExpedition.Begin(accumulated);
        for(int round=0;round<120 && accumulated.Phase==WorldEncounterPhase.Active;round++)accumulated=WorldExpedition.Advance(accumulated,items).State;
        Check(accumulated.Game.Party[0].Xp==1294,"Native reward adds to previously earned XP");
        WorldExpedition.Validate(accumulated,hash,limits,items,eligibleCatalog);
        string legacyJson=Save(sample with {Schema=1,Rewards=null}).Replace(",\"Rewards\":null","");
        Check(Load(legacyJson) is {Schema:1,Rewards:null},"Pre-receipt JSON without the new field remains readable");
        eligibility=eligibility with {Game=eligibility.Game with {Party=eligibility.Game.Party.SetItem(1,eligibility.Game.Party[1] with {Hp=0})
            .SetItem(2,eligibility.Game.Party[2] with {Captive=true})}};
        // A surviving unconscious character still participates in native XP distribution.
        eligibility=WorldExpedition.Begin(eligibility);bool timerAtVictory=false;
        for(int round=0;round<120 && eligibility.Phase==WorldEncounterPhase.Active;round++)
        {
            var game=eligibility.Game;var field=game.Melee!.Field!;
            eligibility=eligibility with {Game=game with {Melee=game.Melee with {Field=field with {Conditions=field.Conditions! with {Condition16=field.Conditions.Condition16.SetItem(game.Party[3].Id,12)}}}}};
            eligibility=WorldExpedition.Advance(eligibility,items).State;
            timerAtVictory=NativeConditions.Remaining(eligibility.Game,eligibility.Game.Party[3].Id)>0;
        }
        Check(eligibility.Phase==WorldEncounterPhase.Victory && timerAtVictory && eligibility.Game.Party[0].Xp==397 && eligibility.Game.Party[3].Xp==397 &&
            eligibility.Game.Party[1].Xp==0 && eligibility.Game.Party[2].Xp==0,"Living condition-16 character receives XP; dead/captive receive none");
        foreach(var edit in new Action<byte[]>[]{r=>r[0xd0]|=1,r=>r[0x1f4]=1,r=>r[0x1f4]=255,r=>r[0x243]=1,r=>r[0x245]=1})
        {
            var unsupported=RewardCatalog(794,edit:edit);
            Check(!ExpeditionRewards.Capability(Record(unsupported)).Ready,"Unsupported treasure/gold/NPC branch explicitly blocked");
            Reject(()=>WorldExpedition.Create(1,items,unsupported,0,world),"Unsupported loot cannot silently become empty drops");
        }
        Reject(()=>WorldExpedition.Create(1,items,RewardCatalog(uint.MaxValue),0,world),"Unsupported wide XP range rejected before battle");
        Check(ExpeditionRewards.FirstLevelGoal("Ninja")==1600 && ExpeditionRewards.FirstLevelGoal("Bishop")==1600 &&
            ExpeditionRewards.FirstLevelGoal("Fighter")==1000 && ExpeditionRewards.FirstLevelGoal("Mage")==1200 &&
            ExpeditionRewards.FirstLevelGoal("Monk")==1400,"Native first level goals used by expedition UI");
        Reject(()=>ExpeditionRewards.FirstLevelGoal("invented"),"Unknown progression profession rejected");
        Console.WriteLine($"PASS: {checks} connected expedition checks");return checks;
    }
}
