using Godot;
using NeoWizardry.Core;
using System.Text;
using System.Text.Json;

// A presentation browser. Asset readiness never admits a monster to gameplay.
public partial class CreatureGallery : Node3D
{
    private sealed record Entry(int Id,string Label,string Status,string? Manifest,string? Reason);
    private Entry[] _entries=[];
    private readonly List<Entry> _shown=[];
    private MonsterCatalog _catalog=null!;
    private string _folder="";
    private WorldCreature? _actor;
    private Camera3D _camera=null!;
    private ItemList _list=null!;
    private Label _status=null!;
    private OptionButton _clips=null!,_skins=null!;
    private CheckButton _loop=null!,_spin=null!;
    private bool _smoke;
    public string? IndexPath {get;init;}
    public Action? ReturnToGame {get;init;}
    public override async void _Ready()
    {
        try
        {
            var args=OS.GetCmdlineUserArgs();int at=Array.IndexOf(args,"--creatures");
            if(IndexPath is null && (at<0 || at+1>=args.Length))throw new InvalidDataException("--creatures requires creatures-index.json");
            string path=Path.GetFullPath(IndexPath??args[at+1]);_folder=Path.GetDirectoryName(path)!;
            if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Creature index exceeds size limit");
            _catalog=MonsterCatalog.Parse(File.ReadAllText(Path.Combine(_folder,"monsters.json")));
            using var json=JsonDocument.Parse(File.ReadAllText(path));var root=json.RootElement;
            if(root.GetProperty("schema").GetInt32()!=1 || root.GetProperty("database_sha256").GetString()!=_catalog.DatabaseSha256)
                throw new InvalidDataException("Creature index/catalog binding mismatch");
            _entries=root.GetProperty("definitions").EnumerateArray().Select(e=>new Entry(e.GetProperty("id").GetInt32(),
                e.GetProperty("label").GetString()??"",e.GetProperty("status").GetString()??"",
                e.TryGetProperty("manifest",out var m)?m.GetString():null,e.TryGetProperty("reason",out var r)?r.GetString():null)).ToArray();
            if(_entries.Length!=_catalog.Count || _entries.Select(e=>e.Id).Distinct().Count()!=_entries.Length)
                throw new InvalidDataException("Creature index must account for every catalog definition");
            foreach(var entry in _entries)
            {
                if(entry.Id<0 || entry.Id>=_catalog.Count || entry.Status is not ("ready" or "partial" or "blocked") ||
                    entry.Label!=_catalog.Monsters[entry.Id].Label || entry.Status!="blocked" && entry.Manifest!=$"creature-{entry.Id}.nwcreature.json")
                    throw new InvalidDataException("Invalid creature index entry");
            }
            _smoke=args.Contains("--creatures-smoke");Build();Filter("");
            if(_smoke)
            {
                var models=new HashSet<string>();int renderable=0;
                foreach(var entry in _entries.Where(e=>e.Status!="blocked"))
                {
                    // All definition bindings are verified, even when geometry is shared.
                    string manifest=Path.Combine(_folder,entry.Manifest!);
                    using var data=JsonDocument.Parse(File.ReadAllText(manifest));var asset=data.RootElement;
                    var definition=_catalog.Monsters[entry.Id];
                    string cycle=Encoding.ASCII.GetString(Convert.FromHexString(definition.RawRecordHex),0x189,48).Split('\0')[0];
                    if(asset.GetProperty("definition_id").GetInt32()!=entry.Id || asset.GetProperty("record_sha256").GetString()!=definition.RecordSha256 ||
                        asset.GetProperty("database_sha256").GetString()!=_catalog.DatabaseSha256 || !string.Equals(asset.GetProperty("cycle_name").GetString(),cycle,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Shared creature definition binding mismatch");
                    renderable++;
                    string key=asset.TryGetProperty("mesh_file",out var file)?file.GetString()!:entry.Manifest!;
                    // Identical geometry can carry different clip timing/variants.
                    key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                        key+asset.GetProperty("clips").GetRawText()+(asset.TryGetProperty("skin_stages",out var stages)?stages.GetRawText():"1"))));
                    if(!models.Add(key))continue;
                    LoadEntry(entry);_actor!.CheckFraming(_camera);_actor.Check();
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    ReleaseActor();
                    if(models.Count%8==0)GC.Collect();
                }
                GD.Print($"PASS: creature gallery {renderable}/{_entries.Length} definitions bound, {models.Count} distinct model/clip sets; every exported clip/frame/skin checked");
                GetTree().Quit();
            }
            else if(_shown.FirstOrDefault(e=>e.Status!="blocked") is {} first)LoadEntry(first);
        }
        catch(Exception error){if(ReturnToGame is not null){ReturnToGame();}else {GD.PushError(error.ToString());GetTree().Quit(1);}}
    }
    private void Build()
    {
        _camera=new Camera3D {Current=true,Position=new(0,1,5)};AddChild(_camera);
        AddChild(new DirectionalLight3D {RotationDegrees=new(-40,-35,0),LightEnergy=1.4f});
        AddChild(new WorldEnvironment {Environment=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,
            BackgroundColor=new Color(0.06f,0.075f,0.1f),AmbientLightSource=Godot.Environment.AmbientSource.Color,
            AmbientLightColor=Colors.White,AmbientLightEnergy=0.65f}});
        var canvas=new CanvasLayer();AddChild(canvas);
        var panel=new PanelContainer {Position=new(12,12),Size=new(390,GetViewport().GetVisibleRect().Size.Y-24)};canvas.AddChild(panel);
        var body=new VBoxContainer();panel.AddChild(body);
        body.AddChild(new Label {Text=$"ORIGINAL CREATURE GALLERY · {_entries.Length} definitions"});
        if(ReturnToGame is not null)
        {
            var back=new Button {Text="Вернуться в монастырь"};back.Pressed+=ReturnToGame;body.AddChild(back);
        }
        var search=new LineEdit {PlaceholderText="Search name / definition ID"};search.TextChanged+=Filter;body.AddChild(search);
        _list=new ItemList {CustomMinimumSize=new(380,220),SizeFlagsVertical=Control.SizeFlags.ExpandFill};
        _list.ItemSelected+=i=>{try{LoadEntry(_shown[(int)i]);}catch(Exception e){_status.Text=$"Load failed: {e.Message}";}};body.AddChild(_list);
        _clips=new OptionButton();_clips.ItemSelected+=i=>
        {if(_actor is not null){_actor.PlayClip((int)i,_loop.ButtonPressed);_actor.Focus(_camera);}};body.AddChild(_clips);
        _loop=new CheckButton {Text="Loop selected animation"};body.AddChild(_loop);
        _loop.Toggled+=on=>{if(_actor is not null && _clips.Selected>=0)_actor.PlayClip(_clips.Selected,on);};
        _skins=new OptionButton();_skins.ItemSelected+=i=>_actor?.SetSkinStage((int)i);body.AddChild(_skins);
        _spin=new CheckButton {Text="Rotate model",ButtonPressed=true};body.AddChild(_spin);
        _status=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(380,125)};body.AddChild(_status);
    }
    private void Filter(string search)
    {
        _list.Clear();_shown.Clear();
        foreach(var entry in _entries.Where(e=>$"{e.Id} {e.Label}".Contains(search,StringComparison.OrdinalIgnoreCase)))
        {_shown.Add(entry);_list.AddItem($"{entry.Id} · {entry.Label} · {entry.Status}");}
    }
    private void ReleaseActor(){if(_actor is not null){RemoveChild(_actor);_actor.Free();_actor=null;}}
    private void LoadEntry(Entry entry)
    {
        if(entry.Status=="blocked")
        {ReleaseActor();_clips.Clear();_skins.Clear();_status.Text=$"{entry.Id} · {entry.Label}\nUnavailable: {entry.Reason}";return;}
        var candidate=WorldCreature.Load(Path.Combine(_folder,entry.Manifest!),_catalog.Monsters[entry.Id],_catalog.DatabaseSha256,WorldCreature.VerifyTextures);
        ReleaseActor();_actor=candidate;AddChild(candidate);candidate.Focus(_camera);
        // Leave the control panel clear of the model without changing the imported geometry.
        _camera.HOffset=-Math.Max(candidate.Scale.X,0.5f);
        _clips.Clear();foreach(string clip in candidate.ClipNames)_clips.AddItem(clip);
        _skins.Clear();for(int i=0;i<candidate.SkinCount;i++)_skins.AddItem(i==0?"Default skin":$"Damage skin {i}");
        _actor.ResetIdle();_clips.Select(Array.FindIndex(candidate.ClipNames,n=>n.StartsWith("IDLE")));
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(_folder,entry.Manifest!)));var root=json.RootElement;
        int blocked=root.TryGetProperty("blocked_clips",out var b)?b.GetArrayLength():0;
        int particles=0,omitted=0;
        foreach(var clip in root.GetProperty("clips").EnumerateArray())
            if(clip.TryGetProperty("emitters",out var emitters))
                foreach(var emitter in emitters.EnumerateArray()){particles++;if(emitter.GetProperty("blocked_reason").GetString()!="")omitted++;}
        _status.Text=$"{entry.Id} · {entry.Label}\n{candidate.ClipCount} clips · {candidate.FrameCount} unique frames · {candidate.SkinCount} skins\n"+
            $"Unavailable clips: {blocked}; particle emitters: {particles-omitted}/{particles} active; {omitted} blocked.\nPresentation browser; combat support is tracked separately.";
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(ReturnToGame is not null && input is InputEventKey {Pressed:true,Echo:false,Keycode:Key.Escape})
        {ReturnToGame();GetViewport().SetInputAsHandled();}
    }
    public override void _Process(double delta){if(!_smoke && _actor is not null && _spin.ButtonPressed)_actor.RotateY((float)delta*0.35f);}
}
