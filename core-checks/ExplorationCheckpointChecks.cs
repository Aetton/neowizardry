using System.Collections.Immutable;
using NeoWizardry.Core;

public static class ExplorationCheckpointChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool condition,string name) { if(!condition)throw new Exception(name);checks++; }
        void Reject(Action action,string name)
        {
            bool rejected=false;
            try { action(); } catch(Exception error) when(error is InvalidDataException or IOException or System.Text.Json.JsonException)
            { rejected=true; }
            Check(rejected,name);
        }
        string hash=new('a',64);
        ImmutableArray<ExplorationDoorLimit> limits=[new(7,3,false,0),new(8,4,true,3)];
        var source=new ExplorationCheckpoint(1,hash,new(1,2,3),new(0.5f,-1,0),2.5f,[new(7,1,2,0.5),new(8,3,3,0)]);
        string Save(ExplorationCheckpoint state)=>state.Serialize(hash,limits);
        var json=Save(source);
        Check(Save(ExplorationCheckpoint.Parse(json,hash,limits))==json,"Exploration camera/phase/target roundtrip");
        Reject(()=>ExplorationCheckpoint.Parse(json,new('b',64),limits),"Reject foreign geometry");
        foreach(var broken in new[] {
            source with { Schema=2 },source with { Position=new(float.NaN,0,0) },
            source with { Rotation=new(0,0,1) },source with { Speed=float.PositiveInfinity },source with { Speed=0 },
            source with { Doors=default },source with { Doors=[] },source with { Doors=[source.Doors[0],source.Doors[0]] },
            source with { Doors=[source.Doors[0] with { Id=9 },source.Doors[1]] },
            source with { Doors=[source.Doors[0] with { Frame=3 },source.Doors[1]] },
            source with { Doors=[source.Doors[0] with { Target=-1 },source.Doors[1]] },
            source with { Doors=[source.Doors[0] with { Phase=-0.01 },source.Doors[1]] },
            source with { Doors=[source.Doors[0] with { Phase=1 },source.Doors[1]] },
            source with { Doors=[source.Doors[0] with { Phase=double.NaN },source.Doors[1]] },
            source with { Doors=[source.Doors[0] with { Target=1 },source.Doors[1]] },
            source with { Doors=[source.Doors[0],source.Doors[1] with { Frame=0,Target=0 }] }
        })Reject(()=>Save(broken),"Reject invalid exploration checkpoint");
        Reject(()=>ExplorationCheckpoint.Parse(json.Replace("\"Phase\":0.5", "\"Unknown\":0.5"),hash,limits),"Reject missing/unknown door field");
        Reject(()=>ExplorationCheckpoint.Parse("null",hash,limits),"Reject null checkpoint");
        Reject(()=>ExplorationCheckpoint.Parse(json[..^1],hash,limits),"Reject truncated checkpoint");
        Reject(()=>ExplorationCheckpoint.Parse(new string(' ',ExplorationCheckpoint.MaximumBytes+1),hash,limits),"Reject oversized checkpoint");
        var directory=Path.Combine(Path.GetTempPath(),$"neowizardry-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string world=Path.Combine(directory,"level.glb"),file=Path.Combine(directory,"checkpoint.json");
            File.WriteAllText(world,"authored world");
            string first=ExplorationCheckpointFile.ContentHash(world);
            var moved=Path.Combine(directory,"renamed.glb");File.Copy(world,moved);
            Check(ExplorationCheckpointFile.ContentHash(moved)==first,"Content identity independent of installation path");
            File.WriteAllText(Path.ChangeExtension(world,".nwdoors.json"),"authored doors");
            Check(ExplorationCheckpointFile.ContentHash(world)!=first,"Sidecar presence participates in checkpoint identity");
            first=ExplorationCheckpointFile.ContentHash(world);
            File.WriteAllText(Path.ChangeExtension(world,".nwdoors.json"),"changed doors");
            Check(ExplorationCheckpointFile.ContentHash(world)!=first,"Changed sidecar invalidates checkpoint identity");
            ExplorationCheckpointFile.Write(file,json);
            Check(ExplorationCheckpointFile.Read(file)==json,"Checkpoint file roundtrip");
            ExplorationCheckpointFile.Write(file,json+" ");
            Check(ExplorationCheckpointFile.Read(file)==json+" ","Checkpoint replaces previous file");
            Reject(()=>ExplorationCheckpointFile.Write(file,new string(' ',ExplorationCheckpoint.MaximumBytes+1)),"Oversized write rejected before replacement");
            Check(ExplorationCheckpointFile.Read(file)==json+" ","Failed write preserves previous checkpoint");
            Reject(()=>ExplorationCheckpointFile.Write(directory,json),"Invalid destination rejects final rename");
            Check(!Directory.EnumerateFiles(directory,".checkpoint-*.tmp").Any(),"Failed rename cleans temporary checkpoint");
            File.WriteAllBytes(file,[0xff]);
            bool invalidUtf8=false;
            try { ExplorationCheckpointFile.Read(file); } catch(System.Text.DecoderFallbackException) { invalidUtf8=true; }
            Check(invalidUtf8,"Malformed UTF-8 checkpoint rejected");
            Check(File.ReadAllBytes(file).SequenceEqual(new byte[]{0xff}),"Failed read preserves original bytes");
        }
        finally { Directory.Delete(directory,true); }
        Console.WriteLine($"PASS: {checks} exploration checkpoint checks");
        return checks;
    }
}
