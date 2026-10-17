using Godot;
using NeoWizardry.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Presentation only. Cosmetic clocks/cues never advance combat, resources or gameplay RNG.
public partial class WorldCreature : Node3D
{
    private sealed record Clip(int Cycle,MeshInstance3D[] Frames,double Fps)
    { public double Duration=>Frames.Length/Fps; }
    private readonly Dictionary<int,Clip> _clips=[];
    private readonly Queue<int> _queue=[];
    private Clip _clip=null!;
    private MeshInstance3D? _visibleFrame;
    private double _time;
    private bool _loop;
    private Aabb _bounds;
    public int DefinitionId { get; private set; }
    public int Cycle=>_clip.Cycle;
    public int FrameCount=>_clips.Values.Sum(c=>c.Frames.Length);
    public int ClipCount=>_clips.Count;
    public bool Idle=>Cycle==1 && _loop;
    private static IEnumerable<MeshInstance3D> Meshes(Node n)
    {
        if(n is MeshInstance3D m)yield return m;
        foreach(var child in n.GetChildren())foreach(var mesh in Meshes(child))yield return mesh;
    }

    public static WorldCreature Load(string manifestPath,OriginalMonster definition,string databaseHash,Action<GltfState> textures)
    {
        if(new FileInfo(manifestPath).Length>16*1024*1024)throw new InvalidDataException("Creature manifest exceeds size limit");
        using var manifest=JsonDocument.Parse(File.ReadAllText(manifestPath));var root=manifest.RootElement;
        bool Same(string property,string value)=>string.Equals(root.GetProperty(property).GetString(),value,StringComparison.OrdinalIgnoreCase);
        var record=Convert.FromHexString(definition.RawRecordHex);
        string cycleName=Encoding.ASCII.GetString(record,0x189,48).Split('\0')[0];
        if(root.GetProperty("schema").GetInt32()!=1 || root.GetProperty("definition_id").GetInt32()!=definition.Id ||
           !Same("database_sha256",databaseHash) || !Same("record_sha256",definition.RecordSha256) || !Same("cycle_name",cycleName))
            throw new InvalidDataException("Creature definition/catalog binding mismatch");
        string path=Path.ChangeExtension(manifestPath,".glb");
        if(new FileInfo(path).Length>128*1024*1024)throw new InvalidDataException("Creature GLB exceeds size limit");
        using(var stream=File.OpenRead(path))
            if(!Same("mesh_sha256",Convert.ToHexString(SHA256.HashData(stream))))throw new InvalidDataException("Creature GLB hash mismatch");
        using var document=new GltfDocument();using var state=new GltfState();
        if(document.AppendFromFile(path,state)!=Error.Ok)throw new InvalidDataException("Creature GLB import failed");
        var scene=document.GenerateScene(state)??throw new InvalidDataException("Creature scene missing");
        var result=new WorldCreature {Name="ImportedCreature",DefinitionId=definition.Id};result.AddChild(scene);textures(state);
        var meshes=Meshes(scene).ToDictionary(m=>m.Name.ToString());var used=new HashSet<int>();Aabb? bounds=null;
        var clips=root.GetProperty("clips");if(clips.GetArrayLength() is <1 or >27)throw new InvalidDataException("Invalid creature clip count");
        foreach(var entry in clips.EnumerateArray())
        {
            int cycle=entry.GetProperty("cycle").GetInt32();
            if(cycle is not (1 or 4 or 6 or 19 or 20) || result._clips.ContainsKey(cycle))throw new InvalidDataException("Unsupported/duplicate creature cycle");
            var indices=entry.GetProperty("frames").EnumerateArray().Select(i=>i.GetInt32()).ToArray();
            if(indices.Length is <1 or >255 || entry.GetProperty("bounds").GetArrayLength()!=indices.Length)throw new InvalidDataException("Invalid creature frame count");
            var frames=new MeshInstance3D[indices.Length];
            for(int i=0;i<indices.Length;i++)
            {
                if(!used.Add(indices[i]) || !meshes.TryGetValue($"PVL_mesh_{indices[i]}",out var frame))throw new InvalidDataException("Missing/duplicate creature frame");
                var row=entry.GetProperty("bounds")[i];
                Vector3 Point(JsonElement p)=>new(p[0].GetSingle(),p[1].GetSingle(),p[2].GetSingle());
                var low=Point(row[0]);var high=Point(row[1]);var actual=WorldDoor.RenderBounds(frame);
                if(!low.IsFinite() || !high.IsFinite() || (actual.Position-low).Length()>0.002f || (actual.End-high).Length()>0.002f)
                    throw new InvalidDataException("Creature render frame bounds mismatch");
                bounds=bounds is {} previous?previous.Merge(actual):actual;
                frame.Visible=false;frames[i]=frame;
            }
            double fps=entry.GetProperty("fps").GetDouble();
            if(!double.IsFinite(fps) || fps<=0 || fps>240)throw new InvalidDataException("Invalid creature playback rate");
            result._clips.Add(cycle,new(cycle,frames,fps));
        }
        if(used.Count!=meshes.Count || bounds is null || !result._clips.ContainsKey(1))throw new InvalidDataException("Unowned creature geometry or missing IDLE");
        result._bounds=bounds.Value;result.ResetIdle();return result;
    }

    private void Start(int cycle,bool loop)
    {
        _clip=_clips.GetValueOrDefault(cycle)??_clips[1];_loop=loop;_time=0;ShowFrame(0);
    }
    private void ShowFrame(int frame)
    {
        if(_visibleFrame is not null)_visibleFrame.Visible=false;
        _visibleFrame=_clip.Frames[frame];_visibleFrame.Visible=true;
    }
    public void ResetIdle(){_queue.Clear();Start(1,true);}
    public void Cues(IEnumerable<int> cycles)
    {
        _queue.Clear();foreach(int cycle in cycles.Where(_clips.ContainsKey).Take(64))_queue.Enqueue(cycle);
        if(_queue.TryDequeue(out int first))Start(first,false);
    }
    public void Step(double delta)
    {
        if(!double.IsFinite(delta) || delta<0)throw new InvalidDataException("Invalid cosmetic animation time");
        _time+=delta;
        while(_time>=_clip.Duration)
        {
            if(_loop){_time%=_clip.Duration;break;}
            double remaining=_time-_clip.Duration;
            if(_queue.TryDequeue(out int next))Start(next,false);else Start(1,true);
            _time=remaining;
        }
        ShowFrame(Math.Min((int)(_time*_clip.Fps),_clip.Frames.Length-1));
    }
    public override void _Process(double delta){if(IsVisibleInTree())Step(delta);}
    public void Focus(Camera3D view)
    {
        var bounds=GlobalTransform*_bounds;var center=bounds.GetCenter();
        view.GlobalPosition=center+new Vector3(0,0.2f,1)*Math.Max(bounds.Size.Length()*1.25f,1);
        LookAt(new Vector3(view.GlobalPosition.X,GlobalPosition.Y,view.GlobalPosition.Z));view.LookAt(center);
    }
    public void Check()
    {
        foreach(var clip in _clips.Values)
        {
            Start(clip.Cycle,false);
            for(int i=0;i<clip.Frames.Length;i++)
            {
                ShowFrame(i);
                if(Meshes(this).Count(m=>m.Visible)!=1 || !clip.Frames[i].Visible)throw new InvalidDataException("Creature frame visibility mismatch");
            }
        }
        ResetIdle();Step(_clip.Duration*3+0.001);
        if(!Idle)throw new InvalidDataException("Creature idle did not loop");
        foreach(var clip in _clips.Values.Where(c=>c.Cycle!=1))
        {
            Cues([clip.Cycle]);Step(clip.Duration+0.001);
            if(!Idle)throw new InvalidDataException("Creature one-shot did not return to IDLE");
        }
        if(_clips.ContainsKey(6) && _clips.ContainsKey(20))
        {
            Cues([6,20]);Step(_clips[6].Duration+0.001);
            if(Cycle!=20)throw new InvalidDataException("Creature attack/hit sequence failed");
            Step(_clips[20].Duration+0.001);if(!Idle)throw new InvalidDataException("Creature cue sequence failed to finish");
        }
        ResetIdle();GD.Print($"PASS: creature {DefinitionId}: {ClipCount} clips, {FrameCount} bounds-verified frames, loop/cue playback");
    }
}
