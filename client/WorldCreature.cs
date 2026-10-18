using Godot;
using NeoWizardry.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Presentation only. Cosmetic clocks/cues never advance combat, resources or gameplay RNG.
public partial class WorldCreature : Node3D
{
    private sealed record Clip(int Cycle,int Variant,string Label,MeshInstance3D[] Frames,double Fps)
    { public double Duration=>Frames.Length/Fps; public CreatureParticles[] Emitters=[]; public int First; }
    private readonly Dictionary<(int,int),Clip> _clips=[];
    private readonly Queue<int> _queue=[];
    private Clip _clip=null!;
    private MeshInstance3D? _visibleFrame;
    private double _time;
    private bool _loop,_dead;
    private static int JsonInteger(JsonElement value)
    {
        double number=value.GetDouble();
        if(!double.IsFinite(number) || number!=Math.Truncate(number) || number<int.MinValue || number>int.MaxValue)
            throw new InvalidDataException("Invalid creature integer metadata");
        return (int)number;
    }
    private double _skinTime;
    private readonly List<(BaseMaterial3D Material,Texture2D[][] Textures,double[] Fps,BaseMaterial3D.TransparencyEnum[] Modes,bool Emissive)> _skins=[];
    public int SkinCount { get; private set; }=1;
    public int SkinStage { get; private set; }
    public string[] ClipNames=>_clips.Values.Select(c=>$"{c.Label} [{c.Cycle}:{c.Variant}] · {c.Frames.Length} frames").ToArray();
    public bool HasCycle(int cycle)=>_clips.Values.Any(c=>c.Cycle==cycle);
    private Aabb _bounds;
    public int DefinitionId { get; private set; }
    public int Cycle=>_clip.Cycle;
    public int FrameCount=>_clips.Values.SelectMany(c=>c.Frames).Distinct().Count();
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
        int schema=root.GetProperty("schema").GetInt32();
        if(schema is not (1 or 2) || root.GetProperty("definition_id").GetInt32()!=definition.Id ||
           !Same("database_sha256",databaseHash) || !Same("record_sha256",definition.RecordSha256) || !Same("cycle_name",cycleName))
            throw new InvalidDataException("Creature definition/catalog binding mismatch");
        string path=Path.ChangeExtension(manifestPath,".glb");
        if(root.TryGetProperty("mesh_file",out var meshFile))
        {
            string file=meshFile.GetString()??"";
            if(!System.Text.RegularExpressions.Regex.IsMatch(file,@"^model-[0-9a-f]{64}\.glb$"))throw new InvalidDataException("Unsafe creature mesh filename");
            path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!,file);
        }
        if(new FileInfo(path).Length>128*1024*1024)throw new InvalidDataException("Creature GLB exceeds size limit");
        using(var stream=File.OpenRead(path))
            if(!Same("mesh_sha256",Convert.ToHexString(SHA256.HashData(stream))))throw new InvalidDataException("Creature GLB hash mismatch");
        using var document=new GltfDocument();using var state=new GltfState();
        if(document.AppendFromFile(path,state)!=Error.Ok)throw new InvalidDataException("Creature GLB import failed");
        var scene=document.GenerateScene(state)??throw new InvalidDataException("Creature scene missing");
        var result=new WorldCreature {Name="ImportedCreature",DefinitionId=definition.Id};
        try
        {
        result.AddChild(scene);textures(state);result.ConfigureSkins(state,root,schema);
        var particleImages=new Dictionary<string,Texture2D>();
        using(var particleJson=JsonDocument.Parse(Godot.Json.Stringify(state.Json)))
            if(particleJson.RootElement.TryGetProperty("extras",out var px) && px.TryGetProperty("neoParticleImages",out var pi))
                foreach(var property in pi.EnumerateObject())
                {
                    int image=JsonInteger(property.Value);
                    if(image<0 || image>=state.GetImages().Count)throw new InvalidDataException("Invalid particle sprite binding");
                    particleImages.Add(property.Name,state.GetImages()[image]);
                }
        var meshes=Meshes(scene).ToDictionary(m=>m.Name.ToString());var used=new HashSet<int>();Aabb? bounds=null;
        var clips=root.GetProperty("clips");if(clips.GetArrayLength() is <1 or >128)throw new InvalidDataException("Invalid creature clip count");
        foreach(var entry in clips.EnumerateArray())
        {
            int cycle=entry.GetProperty("cycle").GetInt32();
            int variant=schema==1?0:entry.GetProperty("variant").GetInt32();
            if(cycle is <0 or >26 || variant is <0 or >127 || result._clips.ContainsKey((cycle,variant)))throw new InvalidDataException("Unsupported/duplicate creature cycle variant");
            var indices=entry.GetProperty("frames").EnumerateArray().Select(i=>i.GetInt32()).ToArray();
            if(indices.Length is <1 or >255 || entry.GetProperty("bounds").GetArrayLength()!=indices.Length)throw new InvalidDataException("Invalid creature frame count");
            if(indices.Distinct().Count()!=indices.Length)throw new InvalidDataException("Duplicate frame within creature clip");
            var frames=new MeshInstance3D[indices.Length];
            for(int i=0;i<indices.Length;i++)
            {
                if(indices[i]<0 || !meshes.TryGetValue($"PVL_mesh_{indices[i]}",out var frame))throw new InvalidDataException("Missing creature frame");
                used.Add(indices[i]);
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
            var clip=new Clip(cycle,variant,entry.GetProperty("name").GetString()??"",frames,fps) {First=entry.GetProperty("first").GetInt32()};
            if(entry.TryGetProperty("emitters",out var emitters))
            {
                if(emitters.GetArrayLength()>256)throw new InvalidDataException("Creature emitter budget exceeded");
                clip.Emitters=emitters.EnumerateArray().Where(e=>e.GetProperty("blocked_reason").GetString()=="")
                    .Select(e=>new CreatureParticles(result,e,particleImages,frames.Length)).ToArray();
            }
            result._clips.Add((cycle,variant),clip);
        }
        if(used.Count!=meshes.Count || bounds is null || !result.HasCycle(1))throw new InvalidDataException("Unowned creature geometry or missing IDLE");
        result._bounds=bounds.Value;result.ResetIdle();return result;
        }
        catch {result.Free();throw;}
    }

    private void Start(int cycle,bool loop)
    {
        StartClip(_clips.Values.FirstOrDefault(c=>c.Cycle==cycle)??_clips.Values.First(c=>c.Cycle==1),loop);
    }
    private void StartClip(Clip clip,bool loop)
    {
        if(_clip is not null)foreach(var emitter in _clip.Emitters)emitter.Stop();
        _dead=false;_clip=clip;_loop=loop;_time=0;ShowFrame(0);
        foreach(var emitter in clip.Emitters)emitter.Begin();
    }
    public void PlayClip(int index,bool loop=false)
    {
        if(index<0 || index>=_clips.Count)throw new ArgumentOutOfRangeException(nameof(index));
        _queue.Clear();StartClip(_clips.Values.ElementAt(index),loop);
    }
    public void PlayDeath(bool completed=false)
    {
        _queue.Clear();if(!HasCycle(21)){Visible=false;return;}
        Start(21,false);_dead=true;
        if(completed){_time=_clip.Duration;ShowFrame(_clip.Frames.Length-1);}
    }
    private void ConfigureSkins(GltfState state,JsonElement manifest,int schema)
    {
        SkinCount=schema==1?1:manifest.GetProperty("skin_stages").GetInt32();
        if(SkinCount is <1 or >16)throw new InvalidDataException("Invalid creature skin count");
        using var json=JsonDocument.Parse(Godot.Json.Stringify(state.Json));var root=json.RootElement;
        var images=state.GetImages();var materials=state.GetMaterials();int index=0;
        foreach(var definition in root.GetProperty("materials").EnumerateArray())
        {
            if(definition.TryGetProperty("extras",out var extras) && extras.TryGetProperty("neoCreatureSkins",out var skins))
            {
                if(index>=materials.Count || materials[index] is not BaseMaterial3D material || skins.GetArrayLength()!=SkinCount)
                    throw new InvalidDataException("Invalid creature skin material binding");
                var frames=new List<Texture2D[]>();var rates=new List<double>();
                foreach(var skin in skins.EnumerateArray())
                {
                    var indices=skin.GetProperty("images").EnumerateArray().Select(JsonInteger).ToArray();
                    double fps=skin.GetProperty("fps").GetDouble();
                    if(indices.Length is <1 or >4096 || indices.Any(i=>i<0 || i>=images.Count || images[i] is null) || !double.IsFinite(fps) || fps<=0 || fps>240)
                        throw new InvalidDataException("Invalid creature skin image/rate");
                    frames.Add(indices.Select(i=>images[i]).ToArray());rates.Add(fps);
                }
                var modes=frames.Select(stage=>
                {
                    int alpha=stage.Max(texture=>{using var image=texture.GetImage();return (int)image.DetectAlpha();});
                    return material.AlbedoColor.A<1 || alpha==(int)Image.AlphaMode.Blend?BaseMaterial3D.TransparencyEnum.Alpha:
                        alpha==(int)Image.AlphaMode.Bit?BaseMaterial3D.TransparencyEnum.AlphaScissor:BaseMaterial3D.TransparencyEnum.Disabled;
                }).ToArray();
                _skins.Add((material,frames.ToArray(),rates.ToArray(),modes,definition.TryGetProperty("emissiveTexture",out _)));
            }
            index++;
        }
        SetSkinStage(0);
    }
    public void SetSkinStage(int stage)
    {
        if(stage<0 || stage>=SkinCount)throw new ArgumentOutOfRangeException(nameof(stage));
        SkinStage=stage;ApplySkinFrame();
    }
    private void ApplySkinFrame()
    {
        foreach(var (material,stages,rates,modes,emissive) in _skins)
        {
            var frames=stages[SkinStage];var texture=frames[(int)((_skinTime*rates[SkinStage])%frames.Length)];
            material.Transparency=modes[SkinStage];material.AlbedoTexture=texture;if(emissive)material.EmissionTexture=texture;
        }
    }
    public void SetHealth(int hp,int maxHp)
    {
        if(maxHp<=0 || hp<0 || hp>maxHp)throw new ArgumentOutOfRangeException(nameof(hp));
        SetSkinStage((int)Math.Min(SkinCount-1,((long)maxHp-hp)*SkinCount/maxHp));
    }
    public static void VerifyTextures(GltfState state)
    {
        using var json=JsonDocument.Parse(Godot.Json.Stringify(state.Json));var root=json.RootElement;
        if(!root.TryGetProperty("images",out var definitions))return;
        var images=state.GetImages();int index=0;
        foreach(var definition in definitions.EnumerateArray())
        {
            var extras=definition.GetProperty("extras");
            if(index>=images.Count || images[index] is null)throw new InvalidDataException("Missing creature embedded image");
            using var pixels=images[index++].GetImage();pixels.ClearMipmaps();pixels.Convert(Image.Format.Rgba8);
            if(pixels.GetWidth()!=JsonInteger(extras.GetProperty("width")) || pixels.GetHeight()!=JsonInteger(extras.GetProperty("height")) ||
                !Convert.ToHexString(SHA256.HashData(pixels.GetData())).Equals(extras.GetProperty("neoPixelSha256").GetString(),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Creature embedded texture pixels differ from export");
        }
    }
    private void ShowFrame(int frame)
    {
        if(_visibleFrame is not null)_visibleFrame.Visible=false;
        _visibleFrame=_clip.Frames[frame];_visibleFrame.Visible=true;
    }
    public void ResetIdle(){_queue.Clear();Start(1,true);}
    public void Cues(IEnumerable<int> cycles)
    {
        _queue.Clear();foreach(int cycle in cycles.Where(HasCycle).Take(64))_queue.Enqueue(cycle);
        if(_queue.TryDequeue(out int first))Start(first,false);
    }
    public void Step(double delta)
    {
        if(!double.IsFinite(delta) || delta<0)throw new InvalidDataException("Invalid cosmetic animation time");
        while(delta>0)
        {
            double slice=Math.Min(delta,1.0/Math.Max(60,_clip.Fps));delta-=slice;
            StepSlice(slice);
        }
    }
    private void StepSlice(double delta)
    {
        if(IsInsideTree())
        {
            int frame=Math.Min((int)(_time*_clip.Fps),_clip.Frames.Length-1);
            foreach(var clip in _clips.Values)foreach(var emitter in clip.Emitters)
                emitter.Step(delta,frame,frame+_clip.First,clip==_clip && !(_dead && _time>=_clip.Duration));
        }
        _skinTime=(_skinTime+delta)%86400;ApplySkinFrame();
        _time+=delta;
        if(_dead && _time>=_clip.Duration){_time=_clip.Duration;ShowFrame(_clip.Frames.Length-1);return;}
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
        // Death debris can span far beyond the standing body. Frame the pose
        // being inspected, rather than the union of every attack/death frame.
        var bounds=GlobalTransform*(_visibleFrame is {} frame?WorldDoor.RenderBounds(frame):_bounds);var center=bounds.GetCenter();
        view.GlobalPosition=center+new Vector3(0,0.2f,1)*Math.Max(bounds.Size.Length()*1.25f,1);
        LookAt(new Vector3(view.GlobalPosition.X,GlobalPosition.Y,view.GlobalPosition.Z));view.LookAt(center);
    }
    public void CheckFraming(Camera3D view)
    {
        var pose=GlobalTransform*WorldDoor.RenderBounds(_visibleFrame!);
        if(!view.GlobalPosition.IsFinite() || view.GlobalPosition.DistanceTo(pose.GetCenter())>Math.Max(pose.Size.Length()*2+1,2))
            throw new InvalidDataException("Creature camera is framed for distant animation debris instead of the displayed pose");
    }
    public void Check()
    {
        var all=_clips.Values.SelectMany(c=>c.Frames).Distinct().ToArray();
        foreach(var clip in _clips.Values)
        {
            StartClip(clip,false);
            for(int i=0;i<clip.Frames.Length;i++)
            {
                var previous=_visibleFrame;
                ShowFrame(i);
                if(!clip.Frames[i].Visible || previous!=_visibleFrame && previous?.Visible==true)
                    throw new InvalidDataException("Creature frame visibility mismatch");
            }
            if(all.Count(m=>m.Visible)!=1)throw new InvalidDataException("Creature clip left extra frames visible");
        }
        ResetIdle();Step(_clip.Duration*3+0.001);
        if(!Idle)throw new InvalidDataException("Creature idle did not loop");
        foreach(var clip in _clips.Values.Where(c=>c.Cycle!=1))
        {
            StartClip(clip,false);Step(clip.Duration+0.001);
            if(!Idle)throw new InvalidDataException("Creature one-shot did not return to IDLE");
        }
        if(HasCycle(6) && HasCycle(20))
        {
            Cues([6,20]);Step(_clips.Values.First(c=>c.Cycle==6).Duration+0.001);
            if(Cycle!=20)throw new InvalidDataException("Creature attack/hit sequence failed");
            Step(_clips.Values.First(c=>c.Cycle==20).Duration+0.001);if(!Idle)throw new InvalidDataException("Creature cue sequence failed to finish");
        }
        for(int stage=0;stage<SkinCount;stage++)
        {
            SetSkinStage(stage);
            _skinTime=0;ApplySkinFrame();
            if(_skins.Any(s=>s.Material.AlbedoTexture!=s.Textures[stage][0]))throw new InvalidDataException("Creature skin stage not applied");
            foreach(var skin in _skins.Where(s=>s.Textures[stage].Length>1))
            {
                _skinTime=1.01/skin.Fps[stage];ApplySkinFrame();
                if(skin.Material.AlbedoTexture!=skin.Textures[stage][1])throw new InvalidDataException("Creature IFL skin did not advance");
            }
        }
        SetHealth(1,10);if(SkinStage!=Math.Min(SkinCount-1,9*SkinCount/10))throw new InvalidDataException("Creature health skin mapping failed");
        SetSkinStage(0);
        if(HasCycle(21)){PlayDeath();Step(_clip.Duration*2);if(Cycle!=21 || !_dead || _visibleFrame!=_clip.Frames[^1])throw new InvalidDataException("Creature death did not hold last frame");}
        foreach(var clip in _clips.Values)
        {
            foreach(var emitter in clip.Emitters)emitter.Check(clip.First,clip.Frames.Length);
        }
        foreach(var clip in _clips.Values)foreach(var emitter in clip.Emitters)emitter.Clear();
        ResetIdle();GD.Print($"PASS: creature {DefinitionId}: {ClipCount} clips, {FrameCount} bounds-verified frames, {SkinCount} skins, {_clips.Values.Sum(c=>c.Emitters.Length)} sprite emitters, loop/cue/particle playback");
    }
}
