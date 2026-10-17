using System.Collections.Immutable;
using NeoWizardry.Core;

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
        foreach(var invalid in new[]{sample with {Schema=2},sample with {EncounterId="other"},sample with {ItemDatabaseHash=new('b',64)},
            sample with {MonsterDatabaseHash=new('b',64)},sample with {MonsterDefinitionId=999},sample with {Phase=(WorldEncounterPhase)999},
            sample with {World=world with {ContentHash=new('b',64)}},sample with {Phase=WorldEncounterPhase.Victory}})
            Reject(()=>Save(invalid),"Invalid expedition binding/state rejected");
        Reject(()=>Load("null"),"Null expedition rejected");
        Reject(()=>Load(Save(sample)[..^1]),"Truncated expedition rejected");
        Reject(()=>Load(new string(' ',WorldExpedition.MaximumBytes+1)),"Oversized expedition rejected");
        Console.WriteLine($"PASS: {checks} connected expedition checks");return checks;
    }
}
