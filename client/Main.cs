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
    private readonly NeoWizardry.Input.MobileControls _touchInput=new();
    private bool _touchMode;
    private bool _compact;
    private bool _ready;
    private bool _paused;
    private int _dialogs;
    private Control _ui=null!;
    private PanelContainer _partyPanel=null!, _logPanel=null!, _actionsPanel=null!;
    private GridContainer _actions=null!;
    private TouchPad _pad=null!;
    private Button _partyToggle=null!, _logToggle=null!;
    private Label _title=null!;
    private Rect2 _lookRect;
    private const string AutoSavePath="user://prototype-autosave.json";
    private ItemCatalog? _catalog;
    private bool _catalogRequested;
    private readonly Vector3 _enemyPosition = new(0, 1, -7);
    private const string SavePath = "user://prototype-save.json";
    private sealed record ClientSave(int Schema, GameState Game, float X, float Z, float Yaw, float Pitch, string? ItemDatabaseHash = null);

    public override void _Ready()
    {
        _touchMode=OS.HasFeature("mobile") || OS.GetCmdlineUserArgs().Contains("--touch");
        BuildRoom();
        BuildUi();
        LoadCatalog();
        Refresh();
        _ready=true;
        if(!OS.GetCmdlineUserArgs().Contains("--smoke") && !OS.GetCmdlineUserArgs().Contains("--mobile-smoke") && Godot.FileAccess.FileExists(AutoSavePath))
            Try(()=>LoadFrom(AutoSavePath));
        Say("Technical slice. Synthetic room, sentinel and dialogue; combat rules are experimental.");
        Say(_touchMode ? "Left pad — move · swipe the view — look · approach the sentinel to attack." : "WASD — move · right mouse — look · Esc — release cursor.");
        if (OS.GetCmdlineUserArgs().Contains("--smoke")) CallDeferred(nameof(Smoke));
        if (OS.GetCmdlineUserArgs().Contains("--mobile-smoke")) Callable.From(MobileSmoke).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--items")) Callable.From(ShowItems).CallDeferred();
    }

    private void LoadCatalog()
    {
        var args = OS.GetCmdlineUserArgs();
        int index = Array.IndexOf(args,"--catalog");
        _catalogRequested=index>=0;
        string? path=index>=0 && index+1<args.Length ? args[index+1] : null;
        if(_catalogRequested && path is null) { Say("No item catalog path selected.");return; }
        if(path is null)
        {
            if(Godot.FileAccess.FileExists("user://items.json"))path="user://items.json";
            else if(Godot.FileAccess.FileExists("res://demo-items.json"))path="res://demo-items.json";
            else if(OS.HasFeature("mobile")) { Say("No original item catalog loaded.");return; }
            else
            {
                string project=ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
                path=Path.Combine(Directory.GetParent(project)!.FullName,"local","items.json");
            }
        }
        if(path.StartsWith("user://") || path.StartsWith("res://"))
        {
            Try(()=>{_catalog=ItemCatalog.Parse(Godot.FileAccess.GetFileAsString(path));Say($"Loaded {_catalog.Count} items.");});return;
        }
        if(!Path.IsPathRooted(path))
        {
            string project=ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
            path=Path.GetFullPath(path,Directory.GetParent(project)!.FullName);
        }
        if(!System.IO.File.Exists(path)) { Say("No original item catalog loaded. The scene still works with test rules.");return; }
        Try(()=>{_catalog=ItemCatalog.Parse(System.IO.File.ReadAllText(path));Say($"Loaded {_catalog.Count} items from {_catalog.Provenance.File}.");});
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
        var layer=new CanvasLayer();AddChild(layer);
        _ui=new Control { MouseFilter=Control.MouseFilterEnum.Ignore };layer.AddChild(_ui);_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _title=new Label { Text="NEOWIZARDRY",MouseFilter=Control.MouseFilterEnum.Ignore };_title.AddThemeFontSizeOverride("font_size",22);_ui.AddChild(_title);
        _partyToggle=Button(_ui,"Party",()=>{_partyPanel.Visible=!_partyPanel.Visible;_touchInput.Reset();});
        _logToggle=Button(_ui,"Log",()=>{_logPanel.Visible=!_logPanel.Visible;_touchInput.Reset();});
        _partyPanel=Panel(_ui,Vector2.Zero,new(220,400));
        var column=new VBoxContainer();_partyPanel.AddChild(column);
        _size=new OptionButton { CustomMinimumSize=new(0,_touchMode?52:36) };for(int i=1;i<=8;i++)_size.AddItem($"{i} adventurer{(i==1?"":"s")}");column.AddChild(_size);
        Button(column,"New party",()=>{_state=Game.Create(_size.Selected+1);_player.Position=new(0,0.9f,2);Refresh();Say("New expedition.");});
        var scroll=new ScrollContainer { CustomMinimumSize=new(180,80),SizeFlagsVertical=Control.SizeFlags.ExpandFill };column.AddChild(scroll);
        _heroes=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };scroll.AddChild(_heroes);
        _quest=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart };column.AddChild(_quest);
        _logPanel=Panel(_ui,Vector2.Zero,new(250,400));var rc=new VBoxContainer();_logPanel.AddChild(rc);
        _enemy=new Label();rc.AddChild(_enemy);
        _log=new RichTextLabel { CustomMinimumSize=new(200,80),ScrollFollowing=true,SizeFlagsVertical=Control.SizeFlags.ExpandFill };rc.AddChild(_log);
        _actionsPanel=Panel(_ui,Vector2.Zero,new(900,80));_actions=new GridContainer();_actions.AddThemeConstantOverride("h_separation",8);_actions.AddThemeConstantOverride("v_separation",8);_actionsPanel.AddChild(_actions);
        Button(_actions,"Attack",Attack);Button(_actions,"Items",ShowItems);Button(_actions,"Save",Save);Button(_actions,"Load",Load);
        Button(_actions,"Keeper",()=>Say("Test keeper: The real monastery is still being researched."));
        Button(_actions,"Capture test",()=>Try(()=>{_state=Game.CaptureFirst(_state);Refresh();_touchInput.Reset();}));
        Button(_actions,"Escape",()=>Try(()=>{_state=Game.Escape(_state);Refresh();}));
        _pad=new TouchPad { MouseFilter=Control.MouseFilterEnum.Ignore };_ui.AddChild(_pad);
        GetViewport().SizeChanged+=LayoutUi;LayoutUi();
    }
    private Button Button(Control parent,string text,Action action)
    {
        var button=new Button { Text=text,CustomMinimumSize=new(0,_touchMode?56:40),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
        button.Pressed+=action;parent.AddChild(button);return button;
    }
    private void LayoutUi()
    {
        var size=GetViewport().GetVisibleRect().Size;
        var bounds=new Rect2(new Vector2(10,10),size-new Vector2(20,20));
        if(OS.HasFeature("mobile"))
        {
            var safe=DisplayServer.GetDisplaySafeArea();var pixels=GetWindow().Size;
            if(pixels.X>0 && pixels.Y>0 && safe.Size.X>0 && safe.Size.Y>0)
            {
                var factor=new Vector2(size.X/pixels.X,size.Y/pixels.Y);
                bounds=new Rect2((Vector2)safe.Position*factor+new Vector2(10,10),(Vector2)safe.Size*factor-new Vector2(20,20));
            }
        }
        bool compact=_touchMode || bounds.Size.X<900;
        if(compact!=_compact) { _partyPanel.Visible=!compact;_logPanel.Visible=!compact;_compact=compact; }
        _partyToggle.Visible=compact;_logToggle.Visible=compact;
        _title.Position=bounds.Position+new Vector2(4,4);
        _partyToggle.Position=new(bounds.End.X-180,bounds.Position.Y);_partyToggle.Size=new(84,_touchMode?52:40);
        _logToggle.Position=new(bounds.End.X-88,bounds.Position.Y);_logToggle.Size=new(84,_touchMode?52:40);
        int columns=Math.Max(3,Math.Min(7,(int)(bounds.Size.X/110)));
        _actions.Columns=columns;
        int rows=(7+columns-1)/columns;
        float actionHeight=rows*(_touchMode?56:40)+(rows-1)*8+28;
        _actionsPanel.Position=new(bounds.Position.X,bounds.End.Y-actionHeight);_actionsPanel.Size=new(bounds.Size.X,actionHeight);
        float top=bounds.Position.Y+(_touchMode?62:50);
        float height=Math.Max(160,_actionsPanel.Position.Y-top-10);
        _partyPanel.Position=new(bounds.Position.X,top);_partyPanel.Size=new(220,height);
        _logPanel.Position=new(bounds.End.X-250,top);_logPanel.Size=new(250,height);
        _pad.Visible=_touchMode;_pad.Position=new(bounds.Position.X+8,_actionsPanel.Position.Y-154);_pad.Size=new(144,144);
        _lookRect=new Rect2(new(bounds.Position.X,top),new(bounds.Size.X,Math.Max(0,_actionsPanel.Position.Y-top)));
        if(!_compact) _lookRect=new Rect2(new(bounds.Position.X+230,top),new(Math.Max(0,bounds.Size.X-490),Math.Max(0,height)));
    }
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
        Try(() => { var r=Game.Attack(_state,_catalog); _state=r.State; foreach(var e in r.Events) Say(e); Refresh(); });
    }
    private void Save() => Try(()=>{SaveTo(SavePath);Say("Saved.");});
    private void SaveTo(string path)
    {
        Game.Validate(_state);
        var snapshot=new ClientSave(1,_state,_player.Position.X,_player.Position.Z,_player.Rotation.Y,_pitch,_catalog?.Provenance.Sha256);
        string temporary=path+".tmp";
        using(var file=Godot.FileAccess.Open(temporary,Godot.FileAccess.ModeFlags.Write))
        {
            if(file is null)throw new IOException("Cannot open save");
            file.StoreString(JsonSerializer.Serialize(snapshot));file.Flush();
            if(file.GetError()!=Error.Ok)throw new IOException("Cannot write save");
        }
        if(DirAccess.RenameAbsolute(temporary,path)!=Error.Ok)throw new IOException("Cannot commit save");
    }
    private void Load() => Try(()=>{LoadFrom(SavePath);Say("Loaded.");});
    private void LoadFrom(string path)
    {
        using var file=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Read);
        if(file is null)throw new IOException("No saved expedition");
        var s=JsonSerializer.Deserialize<ClientSave>(file.GetAsText()) ?? throw new IOException("Empty save");
        Game.Validate(s.Game);Equipment.ValidateLoadedEquipment(s.Game,_catalog,s.ItemDatabaseHash);
        if(s.Schema!=1 || !float.IsFinite(s.X) || !float.IsFinite(s.Z) || !float.IsFinite(s.Yaw) || !float.IsFinite(s.Pitch) || Math.Abs(s.X)>5.3f || s.Z < -16.5f || s.Z>4.5f || Math.Abs(s.Pitch)>1.2f)
            throw new IOException("Invalid saved camera position");
        _state=s.Game;_player.Position=new(s.X,0.9f,s.Z);_player.Rotation=new(0,s.Yaw,0);_pitch=s.Pitch;_camera.Rotation=new(_pitch,0,0);_touchInput.Reset();Refresh();
    }
    public override void _Notification(int what)
    {
        if(!_ready)return;
        if(what==NotificationApplicationPaused)
        {
            _paused=true;_touchInput.Reset();Input.MouseMode=Input.MouseModeEnum.Visible;
            Try(()=>SaveTo(AutoSavePath));
        }
        if(what==NotificationApplicationResumed) { _paused=false;_touchInput.Reset(); }
    }

    private void ShowItems()
    {
        if (_catalog is null) { Say("Import your installation's item catalog using the README to open original items."); return; }
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog { Title="Items — test equipment", Size=new(920,620),Unresizable=true,WrapControls=false };
        AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree; dialog.CloseRequested+=dialog.QueueFree;dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var layout=new VBoxContainer(); dialog.AddChild(layout);
        var screen=GetViewport().GetVisibleRect().Size;
        var dialogSize=new Vector2I((int)Math.Min(920,screen.X-32),(int)Math.Min(620,screen.Y-32));
        bool narrow=dialogSize.X<740;
        layout.Position=new(12,12);layout.Size=new(dialogSize.X-24,dialogSize.Y-64);
        var search=new LineEdit { PlaceholderText="Search item name" };layout.AddChild(search);
        var hero=new OptionButton();foreach(var h in _state.Party)hero.AddItem(h.Name);layout.AddChild(hero);
        BoxContainer row=narrow ? new VBoxContainer() : new HBoxContainer();row.SizeFlagsVertical=Control.SizeFlags.ExpandFill;layout.AddChild(row);
        var list=new ItemList { CustomMinimumSize=new(narrow?0:Math.Min(360,dialogSize.X*0.4f),narrow?120:180),SizeFlagsVertical=Control.SizeFlags.ExpandFill,AutoHeight=false };row.AddChild(list);
        var detailColumn=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };row.AddChild(detailColumn);
        var detail=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(narrow?0:Math.Max(220,dialogSize.X-410),narrow?100:180) };detailColumn.AddChild(detail);
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
        assign.Pressed+=()=>Try(()=>{if(selected is null)return;_state=Equipment.Equip(_state,_state.Party[hero.Selected].Id,selected.Id,_catalog);Refresh();Say($"Assigned {selected.Name}: damage dice and initiative active. Combat is experimental.");Details();});
        Filter("");dialog.PopupCentered(dialogSize);
    }

    private static System.Numerics.Vector2 N(Vector2 value)=>new(value.X,value.Y);
    public override void _Input(InputEvent e)
    {
        if(!_touchMode || _paused)return;
        if(e is InputEventScreenTouch touch)
        {
            if(!touch.Pressed) { _touchInput.Release((int)touch.Index);return; }
            if(_dialogs>0 || (_compact && (_partyPanel.Visible || _logPanel.Visible)))return;
            var pad=_pad.GetGlobalRect();
            _touchInput.Press((int)touch.Index,N(touch.Position),pad.HasPoint(touch.Position),_lookRect.HasPoint(touch.Position),N(pad.GetCenter()));
        }
        if(e is InputEventScreenDrag drag)_touchInput.Drag((int)drag.Index,N(drag.Position),N(drag.Relative));
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if(e is InputEventMouseButton b && b.ButtonIndex==MouseButton.Right && !_touchMode)Input.MouseMode=b.Pressed?Input.MouseModeEnum.Captured:Input.MouseModeEnum.Visible;
        if(e is InputEventKey k && k.Pressed && k.Keycode==Key.Escape)Input.MouseMode=Input.MouseModeEnum.Visible;
        if(e is InputEventMouseMotion m && Input.MouseMode==Input.MouseModeEnum.Captured)Look(m.Relative,0.003f);
    }
    private void Look(Vector2 relative,float sensitivity)
    { _player.RotateY(-relative.X*sensitivity);_pitch=Math.Clamp(_pitch-relative.Y*sensitivity,-1.2f,1.2f);_camera.Rotation=new(_pitch,0,0); }
    public override void _PhysicsProcess(double delta)
    {
        if(!_ready)return;
        var look=_touchInput.ConsumeLook();if(!_paused && _dialogs==0)Look(new(look.X,look.Y),0.004f);
        _pad.Stick=new(_touchInput.Movement.X,_touchInput.Movement.Y);_pad.QueueRedraw();
        if(_paused || _dialogs>0 || !_state.Party.Any(h=>h.Hp>0 && !h.Captive)) { _player.Velocity=Vector3.Zero;return; }
        var d=new Vector3((Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0)+_touchInput.Movement.X,0,
            (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0)+_touchInput.Movement.Y);
        if(d.Length()>1)d=d.Normalized();d=_player.Transform.Basis*d;_player.Velocity=new(d.X*3,-2,d.Z*3);_player.MoveAndSlide();
    }
    public void MobileSmoke()
    {
        try
        {
            var pad=_pad.GetGlobalRect();var center=pad.GetCenter();
            _Input(new InputEventScreenTouch { Index=0,Pressed=true,Position=center });
            _Input(new InputEventScreenDrag { Index=0,Position=center+new Vector2(0,-60),Relative=new(0,-60) });
            if(_touchInput.Movement.Y>=-0.9f)throw new Exception("Touch movement failed");
            var view=_lookRect.GetCenter();_Input(new InputEventScreenTouch { Index=1,Pressed=true,Position=view });
            _Input(new InputEventScreenDrag { Index=1,Position=view+new Vector2(30,0),Relative=new(30,0) });
            var before=_player.Rotation.Y;_PhysicsProcess(1.0/60);
            if(_player.Rotation.Y==before)throw new Exception("Touch look failed");
            _state=Game.Attack(_state).State;var saved=Game.Save(_state);
            _Notification((int)NotificationApplicationPaused);
            if(_touchInput.MovePointer is not null || _touchInput.LookPointer is not null)throw new Exception("Pause left stuck gesture");
            _state=Game.Create(8);LoadFrom(AutoSavePath);
            if(Game.Save(_state)!=saved)throw new Exception("Background autosave failed");
            _Notification((int)NotificationApplicationResumed);
            GD.Print("PASS: multitouch movement/look, pause reset, atomic autosave and restore");GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
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
                    var expected=Game.Save(Game.Attack(_state,_catalog).State);
                    Attack();
                    if(Game.Save(_state)!=expected) throw new Exception("Client did not use equipped weapon");
                    Save(); var continuation=Game.Save(Game.Attack(_state,_catalog).State);
                    _state=Game.Create(8); Load();
                    if(Game.Save(Game.Attack(_state,_catalog).State)!=continuation) throw new Exception("Weapon roll continuation failed");
                    GD.Print($"PASS: imported catalog {_catalog.Count}, equipment and save binding");
                }
            }
            GD.Print("PASS: Godot C# scene, UI combat, save/load, solo escape"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
