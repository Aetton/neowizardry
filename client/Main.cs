using Godot;
using NeoWizardry.Core;
using System.Text.Json;

public partial class Main : Node3D
{
    private GameState _state = Game.Create(1);
    private CharacterBody3D _player = null!;
    private Camera3D _camera = null!;
    private MeshInstance3D _sentinel = null!;
    private VBoxContainer _heroes = null!;
    private RichTextLabel _log = null!;
    private Label _enemy = null!;
    private Label _quest = null!;
    private OptionButton _size = null!;
    private float _pitch;
    private ItemCatalog? _catalog;
    private bool _catalogRequested;
    private readonly Vector3 _enemyPosition = new(0, 1, -7);
    private const string SavePath = "user://prototype-save.json";
    private sealed record ClientSave(int Schema, GameState Game, float X, float Z, float Yaw, float Pitch, string? ItemDatabaseHash = null);

    public override void _Ready()
    {
        BuildRoom();
        BuildUi();
        LoadCatalog();
        Refresh();
        Say("Technical slice. Synthetic room, sentinel and dialogue; combat rules are experimental.");
        Say("WASD — move · right mouse — look · Esc — release cursor · approach the sentinel to attack.");
        if (OS.GetCmdlineUserArgs().Contains("--smoke")) CallDeferred(nameof(Smoke));
        if (OS.GetCmdlineUserArgs().Contains("--items")) Callable.From(ShowItems).CallDeferred();
    }

    private void LoadCatalog()
    {
        var args = OS.GetCmdlineUserArgs();
        int index = Array.IndexOf(args,"--catalog");
        _catalogRequested=index>=0;
        string project = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
        string root = Directory.GetParent(project)!.FullName;
        string? path = index>=0 && index+1<args.Length ? args[index+1] : null;
        if(_catalogRequested && path is null) { Say("No item catalog path selected.");return; }
        if (path is null)
        {
            path = Path.Combine(root,"local","items.json");
        }
        if(!Path.IsPathRooted(path))path=Path.GetFullPath(path,root);
        if (!System.IO.File.Exists(path)) { Say("No original item catalog loaded. The scene still works with test rules."); return; }
        Try(() => { _catalog=ItemCatalog.Parse(System.IO.File.ReadAllText(path)); Say($"Loaded {_catalog.Count} original items from {_catalog.Provenance.File}."); });
    }

    private StandardMaterial3D Material(Color color) => new() { AlbedoColor = color, Roughness = 0.92f };
    private void Block(Vector3 position, Vector3 size, Color color, bool collision = true)
    {
        var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = Material(color) };
        AddChild(mesh);
        if (!collision) return;
        var body = new StaticBody3D { Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }

    private void BuildRoom()
    {
        var environment = new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.025f,0.03f,0.04f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.34f,0.4f,0.5f), AmbientLightEnergy = 0.5f } };
        AddChild(environment);
        Block(new(0,-0.2f,-6), new(12,0.4f,22), new(0.22f,0.24f,0.27f));
        Block(new(0,4.2f,-6), new(12,0.4f,22), new(0.12f,0.13f,0.16f));
        for (int side = -1; side <= 1; side += 2)
            for (int z = 0; z < 11; z++)
                for (int y = 0; y < 4; y++)
                    Block(new(side*5.8f,y+0.5f,3-z*2), new(0.6f,0.96f,1.94f), new(0.23f+y*0.015f,0.25f,0.29f));
        Block(new(0,2,-17), new(12,4,0.6f), new(0.2f,0.23f,0.27f));
        Block(new(0,2,5), new(12,4,0.6f), new(0.2f,0.23f,0.27f));
        for (int z = 0; z < 3; z++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Block(new(side*4.8f,1.9f,-z*6),new(0.35f,3.8f,0.6f),new(0.3f,0.29f,0.27f));
                var light = new OmniLight3D { Position = new(side*4.3f,2.8f,-z*6), LightColor = new(1,0.63f,0.27f), LightEnergy = 2.6f, OmniRange = 7 };
                AddChild(light);
                Block(light.Position,new(0.18f,0.4f,0.18f),new(1,0.58f,0.18f),false);
            }
            Block(new(0,3.8f,-z*6),new(10,0.4f,0.6f),new(0.3f,0.29f,0.27f));
        }
        _sentinel = new MeshInstance3D { Position = _enemyPosition, Mesh = new CapsuleMesh { Radius = 0.55f, Height = 2 }, MaterialOverride = Material(new(0.52f,0.26f,0.17f)) };
        AddChild(_sentinel);
        var sentinelBody = new StaticBody3D { Position = _enemyPosition };
        sentinelBody.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.55f, Height = 2 } });
        AddChild(sentinelBody);
        _player = new CharacterBody3D { Position = new(0,0.9f,2) };
        _player.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.8f } });
        _camera = new Camera3D { Position = new(0,0.65f,0), Current = true, Fov = 72 };
        _player.AddChild(_camera);
        AddChild(_player);
    }

    private PanelContainer Panel(Control parent, Vector2 pos, Vector2 size)
    {
        var panel = new PanelContainer { Position = pos, Size = size };
        var style = new StyleBoxFlat { BgColor = new(0.035f,0.045f,0.055f,0.95f), BorderColor = new(0.52f,0.42f,0.23f) };
        style.SetBorderWidthAll(3);
        style.SetContentMarginAll(14);
        panel.AddThemeStyleboxOverride("panel",style);
        parent.AddChild(panel);
        return panel;
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer(); AddChild(layer);
        var ui = new Control(); ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); ui.MouseFilter = Control.MouseFilterEnum.Ignore; layer.AddChild(ui);
        var title = new Label { Text = "NEOWIZARDRY  /  FIRST INCISION", Position = new(28,16) }; title.AddThemeFontSizeOverride("font_size",24); ui.AddChild(title);
        var left = Panel(ui,new(20,66),new(224,600));
        var column = new VBoxContainer(); left.AddChild(column);
        column.AddChild(new Label { Text = "PARTY" });
        _size = new OptionButton(); for (int i=1; i<=8; i++) _size.AddItem($"{i} adventurer{(i==1 ? "" : "s")}"); column.AddChild(_size);
        Button(column,"New party",() => { _state=Game.Create(_size.Selected+1); _player.Position=new(0,0.9f,2); Refresh(); Say("New expedition."); });
        var scroll = new ScrollContainer { CustomMinimumSize = new(190,380), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        column.AddChild(scroll);
        _heroes = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(_heroes);
        _quest = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; column.AddChild(_quest);
        var right = Panel(ui,new(958,66),new(302,600));
        var rc = new VBoxContainer(); right.AddChild(rc);
        _enemy = new Label(); rc.AddChild(_enemy);
        _log = new RichTextLabel { CustomMinimumSize = new(270,510), ScrollFollowing = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; rc.AddChild(_log);
        var bottom = Panel(ui,new(20,686),new(1240,94));
        var buttons = new HBoxContainer(); buttons.AddThemeConstantOverride("separation",10); bottom.AddChild(buttons);
        Button(buttons,"Attack / next round",Attack);
        Button(buttons,"Keeper: monastery",() => Say("Test keeper: This is a synthetic corridor. The real monastery importer is still being researched."));
        Button(buttons,"Items",ShowItems);
        Button(buttons,"Save",Save);
        Button(buttons,"Load",Load);
        Button(buttons,"Test captivity",() => Try(() => { _state=Game.CaptureFirst(_state); Refresh(); Say("Captured. Self-escape remains available even for a solo hero."); }));
        Button(buttons,"Escape",() => Try(() => { _state=Game.Escape(_state); Refresh(); Say("Escaped without hiring another hero."); }));
    }

    private void Button(Control parent, string text, Action action)
    { var b=new Godot.Button { Text=text }; b.Pressed += action; parent.AddChild(b); }
    private void Try(Action action) { try { action(); } catch (Exception e) { Say(e.Message); } }
    private void Say(string message) { _log.AppendText(message+"\n\n"); }
    private void Refresh()
    {
        foreach (var child in _heroes.GetChildren()) { _heroes.RemoveChild(child); child.QueueFree(); }
        foreach (var h in _state.Party)
        {
            string equipment = h.EquippedItemId.HasValue && _catalog is not null ? $"\n{_catalog.Find(h.EquippedItemId.Value).Name}" : "";
            _heroes.AddChild(new Label { Text=$"{h.Name}\n{h.Race} · {h.Profession}\nHP {h.Hp}/{h.MaxHp}   XP {h.Xp}{equipment}{(h.Captive ? "\nCAPTIVE" : "")}", AutowrapMode=TextServer.AutowrapMode.WordSmart });
        }
        _enemy.Text=$"{_state.Enemy.Id}\nHP {_state.Enemy.Hp} · Round {_state.Round}";
        _quest.Text=$"Captivity fixture: {_state.Quest}";
        _sentinel.Visible=_state.Enemy.Hp>0;
        _size.Select(_state.Party.Length-1);
    }
    private void Attack()
    {
        if (_player.Position.DistanceTo(_enemyPosition)>4.5f) { Say("Too far from the sentinel. Move closer."); return; }
        Try(() => { var r=Game.Attack(_state); _state=r.State; foreach(var e in r.Events) Say(e); Refresh(); });
    }
    private void Save() => Try(() =>
    {
        var s=new ClientSave(1,_state,_player.Position.X,_player.Position.Z,_player.Rotation.Y,_pitch,_catalog?.Provenance.Sha256);
        using var file=Godot.FileAccess.Open(SavePath,Godot.FileAccess.ModeFlags.Write);
        if(file is null) throw new IOException("Cannot open save");
        file.StoreString(JsonSerializer.Serialize(s)); Say("Saved.");
    });
    private void Load() => Try(() =>
    {
        using var file=Godot.FileAccess.Open(SavePath,Godot.FileAccess.ModeFlags.Read);
        if(file is null) throw new IOException("No saved expedition");
        var s=JsonSerializer.Deserialize<ClientSave>(file.GetAsText()) ?? throw new IOException("Empty save");
        Game.Validate(s.Game);
        Equipment.ValidateLoadedEquipment(s.Game,_catalog,s.ItemDatabaseHash);
        if(s.Schema!=1 || !float.IsFinite(s.X) || !float.IsFinite(s.Z) || !float.IsFinite(s.Yaw) || !float.IsFinite(s.Pitch) ||
            Math.Abs(s.X)>5 || s.Z < -16 || s.Z>4 || Math.Abs(s.Pitch)>1.2f) throw new IOException("Invalid saved camera position");
        _state=s.Game; _player.Position=new(s.X,0.9f,s.Z); _player.Rotation=new(0,s.Yaw,0); _pitch=s.Pitch; _camera.Rotation=new(_pitch,0,0); Refresh(); Say("Loaded.");
    });

    private void ShowItems()
    {
        if (_catalog is null) { Say("Import your installation's item catalog using the README to open original items."); return; }
        Input.MouseMode=Input.MouseModeEnum.Visible;
        var dialog=new AcceptDialog { Title="Original items — test equipment", Size=new(920,620),Unresizable=true,WrapControls=false };
        AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree; dialog.CloseRequested+=dialog.QueueFree;
        var layout=new VBoxContainer(); dialog.AddChild(layout);
        layout.Position=new(12,12);layout.Size=new(896,550);
        var search=new LineEdit { PlaceholderText="Search item name" };layout.AddChild(search);
        var hero=new OptionButton();foreach(var h in _state.Party)hero.AddItem(h.Name);layout.AddChild(hero);
        var row=new HBoxContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill };layout.AddChild(row);
        var list=new ItemList { CustomMinimumSize=new(360,420),SizeFlagsVertical=Control.SizeFlags.ExpandFill,AutoHeight=false };row.AddChild(list);
        var detailColumn=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };row.AddChild(detailColumn);
        var detail=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(460,300) };detailColumn.AddChild(detail);
        var assign=new Godot.Button { Text="Assign test melee weapon",Disabled=true };detailColumn.AddChild(assign);
        detailColumn.AddChild(new Label { Text="Imported data; experimental battle rules.\nOwnership and curses are pending." });
        ItemDefinition[] shown=[];
        ItemDefinition? selected=null;
        void Details()
        {
            if(selected is null) { detail.Text="Choose an item.";assign.Disabled=true;return; }
            var h=_state.Party[hero.Selected];
            var reason=Equipment.Denial(h,selected);
            detail.Text=$"{selected.Name}  [ID {selected.Id}]\n\n{selected.DamageDescription}\nInitiative {selected.Initiative:+0;-0;0} · To hit {selected.ToHit:+0;-0;0}\nWeight {selected.WeightTenthsLb/10m} lb\nCursed: {selected.Cursed}\n\n{(reason ?? "Race, profession and gender permit this weapon.")}\n\nSource: {_catalog.Provenance.File}";
            assign.Disabled=reason is not null || h.Captive || h.Hp==0;
        }
        void Filter(string term)
        {
            shown=_catalog.Items.Where(i=>i.Name.Contains(term,StringComparison.OrdinalIgnoreCase)).ToArray();
            list.Clear();foreach(var i in shown)list.AddItem($"{i.Id}: {i.Name}");selected=null;Details();
        }
        search.TextChanged+=Filter;
        list.ItemSelected+=(long i)=>{selected=shown[(int)i];Details();};
        hero.ItemSelected+=(long _) => Details();
        assign.Pressed+=()=>Try(()=>{if(selected is null)return;_state=Equipment.Equip(_state,_state.Party[hero.Selected].Id,selected.Id,_catalog);Refresh();Say($"Assigned {selected.Name}. Battle still uses experimental rules.");Details();});
        Filter("");dialog.PopupCentered(new(920,620));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if(e is InputEventMouseButton b && b.ButtonIndex==MouseButton.Right) Input.MouseMode=b.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        if(e is InputEventKey k && k.Pressed && k.Keycode==Key.Escape) Input.MouseMode=Input.MouseModeEnum.Visible;
        if(e is InputEventMouseMotion m && Input.MouseMode==Input.MouseModeEnum.Captured)
        { _player.RotateY(-m.Relative.X*0.003f); _pitch=Math.Clamp(_pitch-m.Relative.Y*0.003f,-1.2f,1.2f); _camera.Rotation=new(_pitch,0,0); }
    }
    public override void _PhysicsProcess(double delta)
    {
        if(!_state.Party.Any(h => h.Hp>0 && !h.Captive)) { _player.Velocity=Vector3.Zero; return; }
        var d=new Vector3((Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0),0,
            (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0));
        d=_player.Transform.Basis*d.Normalized(); _player.Velocity=new(d.X*3,-2,d.Z*3); _player.MoveAndSlide();
    }

    public void Smoke()
    {
        try
        {
            if(_catalogRequested && _catalog is null) throw new Exception("Requested smoke catalog was not loaded");
            _player.Position=new(0,0.9f,-4);
            Attack();
            if(_state.Round!=1 || _state.Enemy.Hp!=25) throw new Exception("Combat/UI smoke failed");
            Save(); _state=Game.Create(8); Load();
            if(_state.Party.Length!=1 || _state.Round!=1) throw new Exception("Client save/load failed");
            _state=Game.CaptureFirst(_state); Refresh();
            _state=Game.Escape(_state); Refresh();
            if (_catalog is not null)
            {
                var cane=_catalog.Items.FirstOrDefault(i=>i.Name=="Cane of Corpus") ??
                    _catalog.Items.FirstOrDefault(i=>Equipment.Denial(_state.Party[0],i) is null);
                if(cane is null) throw new Exception("Smoke catalog has no eligible melee weapon");
                {
                    _state=Equipment.Equip(_state,_state.Party[0].Id,cane.Id,_catalog); Save(); _state=Game.Create(8); Load();
                    if(_state.Party[0].EquippedItemId!=cane.Id) throw new Exception("Imported equipment save/load failed");
                    GD.Print($"PASS: imported catalog {_catalog.Count}, equipment and save binding");
                }
            }
            GD.Print("PASS: Godot C# scene, UI combat, save/load, solo escape"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
