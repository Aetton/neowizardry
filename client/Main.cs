using System.Collections.Immutable;
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
    private VBoxContainer _leftPortraits=null!,_rightPortraits=null!;
    private HeroPortrait[] _portraits=[];
    private ArenaCompass _compass=null!;
    private RealmDial _realms=null!;
    private Label _selection=null!;
    private string _selectedHeroId="hero-1";
    private string? _initialRealm;
    private Theme _hudTheme=null!;
    private const string AutoSavePath="user://prototype-autosave.json";
    private ItemCatalog? _catalog;
    private SpellCatalog? _spellCatalog;
    private MonsterCatalog? _monsterCatalog;
    private bool _monstersRequested;
    private bool _spellsRequested;
    private bool _catalogRequested;
    private bool _autosaveBlocked;
    private readonly Vector3 _enemyPosition = new(0, 1, -7);
    private const string SavePath = "user://prototype-save.json";
    private sealed record ClientSave(int Schema, GameState Game, float X, float Z, float Yaw, float Pitch, string? ItemDatabaseHash = null);

    public override void _Ready()
    {
        try { Initialize(); }
        catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }
    private void Initialize()
    {
        _touchMode=OS.HasFeature("mobile") || OS.GetCmdlineUserArgs().Contains("--touch");
        BuildRoom();
        BuildUi();
        LoadCatalog();
        if(_catalog is null && _catalogRequested)throw new InvalidDataException("Requested item catalog could not be loaded");
        _catalog??=MeleeTraining.Catalog();
        LoadSpellCatalog();
        LoadMonsterCatalog();
        LoadRecipeCatalog();
        _state=CreateExpedition(1);
        Refresh();
        _ready=true;
        if(!OS.GetCmdlineUserArgs().Contains("--smoke") && !OS.GetCmdlineUserArgs().Contains("--mobile-smoke") && Godot.FileAccess.FileExists(AutoSavePath))
            Try(()=>LoadFrom(AutoSavePath));
        if(OS.GetCmdlineUserArgs().Contains("--ui-preview"))NewParty(8);
        Say("Training room: restored ordinary melee rules; authored combatants and scenario initiative.");
        Say(_touchMode ? "Left pad — move · swipe the view — look · approach the sentinel to attack." : "WASD — move · right mouse — look · Esc — release cursor.");
        if (OS.GetCmdlineUserArgs().Contains("--smoke")) CallDeferred(nameof(Smoke));
        if (OS.GetCmdlineUserArgs().Contains("--mobile-smoke")) Callable.From(MobileSmoke).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--items")) Callable.From(ShowItems).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--grimoire")) Callable.From(ShowGrimoire).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--combinations")) Callable.From(ShowCombinations).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--craft")) Callable.From(ShowCrafting).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--tactics")) Callable.From(()=>{NewTactical(_state.Party.Length,3);ShowTactics();}).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--magic")) Callable.From(ShowMagic).CallDeferred();
    }

    private GameState CreateExpedition(int size,bool melee=true)
    {
        if(melee)return MeleeTraining.Create(size,_catalog??throw new InvalidOperationException("Missing melee catalog"),_spellCatalog);
        var state=Game.Create(size);
        if(_catalog is null)return state;
        var starter=_catalog.Items.OrderBy(i=>i.Id).Where(i=>Equipment.Denial(state.Party[0],i) is null).Take(Crafting.UsesFixture(_catalog)?1:2).ToArray();
        foreach(var item in starter) state=Inventory.Add(state,item.Id,1,_catalog);
        if(Crafting.UsesFixture(_catalog))
        {
            state=Inventory.Add(Inventory.Add(state,78,4,_catalog),79,2,_catalog);
            state=state with { KnownRecipes=Crafting.Recipes.Select(r=>r.Id).ToImmutableList() };
        }
        if(starter.Length>0)state=state with { Enemy=state.Enemy with { LootItemId=starter[^1].Id } };
        return state;
    }

    private static string ExternalCatalogRoot() => OS.HasFeature("editor")
        ? Directory.GetParent(ProjectSettings.GlobalizePath("res://").TrimEnd('/','\\'))!.FullName
        : Path.GetDirectoryName(OS.GetExecutablePath())!;

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
                path=Path.Combine(ExternalCatalogRoot(),"local","items.json");
            }
        }
        if(path.StartsWith("user://") || path.StartsWith("res://"))
        {
            Try(()=>{_catalog=ItemCatalog.Parse(Godot.FileAccess.GetFileAsString(path));Say($"Loaded {_catalog.Count} items.");});return;
        }
        if(!Path.IsPathRooted(path))
        {
            path=Path.GetFullPath(path,ExternalCatalogRoot());
        }
        if(!System.IO.File.Exists(path)) { Say("No imported item catalog loaded. Using practice equipment.");return; }
        Try(()=>{_catalog=ItemCatalog.Parse(System.IO.File.ReadAllText(path));Say($"Loaded {_catalog.Count} items from {_catalog.Provenance.File}.");});
    }

    private void LoadSpellCatalog()
    {
        var args=OS.GetCmdlineUserArgs();int index=Array.IndexOf(args,"--spells");_spellsRequested=index>=0;
        if(_spellsRequested && index+1>=args.Length) { Say("No spell catalog path selected.");return; }
        string root=ExternalCatalogRoot();
        string local=Path.Combine(root,"local","spells.json");
        string? path=_spellsRequested?args[index+1]:Godot.FileAccess.FileExists("user://spells.json")?"user://spells.json":System.IO.File.Exists(local)?local:Godot.FileAccess.FileExists("res://demo-items.spells.json")?"res://demo-items.spells.json":null;
        if(path is null)return;
        if(!path.StartsWith("user://") && !path.StartsWith("res://") && !Path.IsPathRooted(path))path=Path.GetFullPath(path,root);
        Try(()=>{
            string json=path.StartsWith("user://") || path.StartsWith("res://")?Godot.FileAccess.GetFileAsString(path):System.IO.File.ReadAllText(path);
            _spellCatalog=SpellCatalog.Parse(json);Say($"Loaded {_spellCatalog.Count} spell reference records.");
        });
    }
    private void LoadMonsterCatalog()
    {
        var args=OS.GetCmdlineUserArgs();int index=Array.IndexOf(args,"--monsters");_monstersRequested=index>=0;
        if(_monstersRequested && index+1>=args.Length){Say("No monster catalog path selected.");return;}
        string root=ExternalCatalogRoot(),local=Path.Combine(root,"local","monsters.json");
        string? path=_monstersRequested?args[index+1]:Godot.FileAccess.FileExists("user://monsters.json")?"user://monsters.json":System.IO.File.Exists(local)?local:null;
        if(path is null)return;
        if(!path.StartsWith("user://") && !Path.IsPathRooted(path))path=Path.GetFullPath(path,root);
        Try(()=>{_monsterCatalog=MonsterCatalog.Parse(path.StartsWith("user://")?Godot.FileAccess.GetFileAsString(path):System.IO.File.ReadAllText(path));Say($"Loaded {_monsterCatalog.Count} original monster reference records.");});
    }
    private void ShowMonsters()
    {
        if(_monsterCatalog is null){Say("Import MONSTERS.DBS using tools.slf monsters; load local/monsters.json or --monsters <path>.");return;}
        _dialogs++;_touchInput.Reset();Input.MouseMode=Input.MouseModeEnum.Visible;
        var dialog=new AcceptDialog {Theme=_hudTheme,Title="Original monster reference"};AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;
        dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var body=new VBoxContainer();dialog.AddChild(body);
        var search=new LineEdit {PlaceholderText="Search monster names"};body.AddChild(search);
        var list=new ItemList {CustomMinimumSize=new(360,200)};body.AddChild(list);
        var detail=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(360,100)};body.AddChild(detail);
        OriginalMonster[] shown=[];
        void Filter()
        {
            shown=_monsterCatalog.Monsters.Where(m=>m.TextSlots.Any(t=>t.Contains(search.Text,StringComparison.OrdinalIgnoreCase))).ToArray();
            list.Clear();foreach(var m in shown)list.AddItem($"{m.Id}: {m.Label}");
            detail.Text=$"{shown.Length} / {_monsterCatalog.Count} definitions. Verified text and defense fields; executable monster actors pending.";
        }
        search.TextChanged+=_=>Filter();list.ItemSelected+=i=>{var m=shown[(int)i];detail.Text=string.Join(" · ",m.TextSlots)+$"\nID {m.Id} · record {m.RecordSha256[..12]} · 663 native bytes preserved."+(m.Defense is { } d?$"\nAC {d.ArmorClass} · zones {string.Join("/",d.LocationWeights)}\nZone armor {string.Join("/",d.LocationArmor)} · mode armor {string.Join("/",d.ModeArmor)}":"");};
        Filter();dialog.PopupCentered(new Vector2I(400,400));
    }

    private void ShowGrimoire()
    {
        if(_spellCatalog is null) { Say("Import a spell reference catalog as user://spells.json or select it with --spells. See the README.");return; }
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog { Theme=_hudTheme,Title=_spellCatalog.Provenance.File.StartsWith("synthetic-")?"Synthetic spell reference":"Original spell reference",Unresizable=true,WrapControls=false };AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var body=new VBoxContainer();dialog.AddChild(body);
        var search=new LineEdit { PlaceholderText="Search original spell name" };body.AddChild(search);
        var dormant=new CheckButton { Text="Include level-zero entries" };body.AddChild(dormant);
        var list=new ItemList { AutoHeight=false,CustomMinimumSize=new(0,100),SizeFlagsVertical=Control.SizeFlags.ExpandFill };body.AddChild(list);
        var detail=new RichTextLabel { CustomMinimumSize=new(0,150),SizeFlagsVertical=Control.SizeFlags.ExpandFill };body.AddChild(detail);
        body.AddChild(new Label { Text="Read-only reference.\nImport does not teach spells or enable casting." });
        OriginalSpell[] shown=[];
        void Details(int index)
        {
            var s=shown[index];string[] schoolNames=["Alchemy","Wizardry","Divinity","Psionics"];
            string schools=string.Join(", ",s.SchoolFlags.Select((flag,i)=>$"{schoolNames[i]}={flag}"));
            detail.Text=$"{s.Name} [ID {s.Id}] · {s.Realm} · Level {s.Level}\nCost field: {s.Cost} · {s.Target} · {s.Usability} · Range code {s.RangeCode}\n{schools}\nDuration raw: {s.DurationBaseRaw} + {s.DurationPerPowerRaw}/power · Damage flag {s.DamageFlagRaw}, {s.DamageDice}d{s.DamageSides}+{s.DamageConstant}\n\n{s.Description}\n\nRecords: {_spellCatalog.Provenance.File} · Descriptions: {_spellCatalog.DescriptionProvenance.File}";
        }
        void Filter()
        {
            shown=_spellCatalog.Spells.Where(s=>(dormant.ButtonPressed || s.Level>0) && s.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToArray();
            list.Clear();foreach(var s in shown)list.AddItem($"{s.Name} · {s.Realm} · {s.Level}");
            if(shown.Length>0) { list.Select(0);Details(0); }else detail.Text="No matching entries.";
        }
        search.TextChanged+=_=>Filter();dormant.Toggled+=_=>Filter();list.ItemSelected+=i=>Details((int)i);
        var screen=GetViewport().GetVisibleRect().Size;var dimensions=new Vector2I((int)Math.Min(850,screen.X-32),(int)Math.Min(620,screen.Y-32));
        body.Position=new(12,12);body.Size=new(dimensions.X-24,dimensions.Y-64);Filter();dialog.PopupCentered(dimensions);
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
        var style = new StyleBoxFlat { BgColor = new(0.035f,0.042f,0.041f,0.96f), BorderColor = new(0.36f,0.33f,0.25f) };
        style.SetBorderWidthAll(2);
        style.SetContentMarginAll(7);
        panel.AddThemeStyleboxOverride("panel",style);
        parent.AddChild(panel);
        return panel;
    }

    private void BuildUi()
    {
        ApplyUiTheme();
        var layer=new CanvasLayer();AddChild(layer);
        _ui=new Control { Theme=_hudTheme,MouseFilter=Control.MouseFilterEnum.Ignore };layer.AddChild(_ui);_ui.Draw+=DrawChrome;_ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _title=new Label { Text="N E O W I Z A R D R Y",MouseFilter=Control.MouseFilterEnum.Ignore };_title.AddThemeFontSizeOverride("font_size",13);_ui.AddChild(_title);
        _partyToggle=Button(_ui,"Party",()=>{_partyPanel.Visible=!_partyPanel.Visible;_touchInput.Reset();});
        _logToggle=Button(_ui,"Log",()=>{_logPanel.Visible=!_logPanel.Visible;_touchInput.Reset();});
        _partyPanel=Panel(_ui,Vector2.Zero,new(220,300));_partyPanel.Visible=false;
        var column=new VBoxContainer();_partyPanel.AddChild(column);
        _size=new OptionButton { CustomMinimumSize=new(0,_touchMode?52:32) };for(int i=1;i<=8;i++)_size.AddItem($"{i} adventurer{(i==1?"":"s")}");column.AddChild(_size);
        Button(column,"New party",()=>NewParty(_size.Selected+1));
        var scroll=new ScrollContainer { CustomMinimumSize=new(180,60),SizeFlagsVertical=Control.SizeFlags.ExpandFill };column.AddChild(scroll);
        _heroes=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };scroll.AddChild(_heroes);
        _quest=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart };column.AddChild(_quest);
        _leftPortraits=new VBoxContainer();_rightPortraits=new VBoxContainer();_ui.AddChild(_leftPortraits);_ui.AddChild(_rightPortraits);
        _leftPortraits.AddThemeConstantOverride("separation",4);_rightPortraits.AddThemeConstantOverride("separation",4);
        _portraits=Enumerable.Range(0,8).Select(i=>{
            var card=new HeroPortrait { SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseDefaultCursorShape=Control.CursorShape.PointingHand };
            card.Pick=id=>{_selectedHeroId=id;Refresh();};(i<4?_leftPortraits:_rightPortraits).AddChild(card);return card;
        }).ToArray();
        _enemy=new Label { HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore };_ui.AddChild(_enemy);
        _logPanel=Panel(_ui,Vector2.Zero,new(400,120));
        _log=new RichTextLabel { ScrollFollowing=true,SizeFlagsVertical=Control.SizeFlags.ExpandFill };_log.AddThemeFontSizeOverride("normal_font_size",12);_logPanel.AddChild(_log);
        _compass=new ArenaCompass();_ui.AddChild(_compass);
        _realms=new RealmDial();_realms.Pick=realm=>{_initialRealm=realm;ShowMagic();};_ui.AddChild(_realms);
        _actionsPanel=Panel(_ui,Vector2.Zero,new(900,50));
        var strip=new HBoxContainer();_actionsPanel.AddChild(strip);
        _actions=new GridContainer();_actions.AddThemeConstantOverride("h_separation",4);_actions.AddThemeConstantOverride("v_separation",4);strip.AddChild(_actions);
        Button(_actions,"Attack",Attack);Button(_actions,"Magic",ShowMagic);Button(_actions,"Items",ShowItems);Button(_actions,"Craft",ShowCrafting);Button(_actions,"Journal",()=>{_logPanel.Visible=!_logPanel.Visible;});Button(_actions,"Menu",ShowMenu);
        _selection=new Label { VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore };strip.AddChild(_selection);
        _pad=new TouchPad { MouseFilter=Control.MouseFilterEnum.Ignore };_ui.AddChild(_pad);
        GetViewport().SizeChanged+=LayoutUi;LayoutUi();
    }
    private void ApplyUiTheme()
    {
        var theme=new Theme { DefaultFontSize=14 };
        var normal=HeroPortrait.Frame(new(.065f,.073f,.068f),new(.37f,.34f,.25f));
        var hover=HeroPortrait.Frame(new(.12f,.14f,.12f),new(.66f,.54f,.32f));
        foreach(var type in new[]{"Button","OptionButton"})
        {
            theme.SetStylebox("normal",type,normal);theme.SetStylebox("hover",type,hover);
            theme.SetStylebox("pressed",type,HeroPortrait.Frame(new(.025f,.045f,.043f),new(.75f,.61f,.35f)));
            theme.SetStylebox("disabled",type,HeroPortrait.Frame(new(.045f,.05f,.047f),new(.19f,.21f,.18f)));
            theme.SetColor("font_color",type,new(.85f,.81f,.68f));theme.SetColor("font_hover_color",type,new(.98f,.91f,.70f));
        }
        theme.SetStylebox("panel","AcceptDialog",HeroPortrait.Frame(new(.035f,.043f,.041f),new(.53f,.44f,.28f)));
        theme.SetColor("font_color","Label",new(.82f,.80f,.70f));
        theme.SetColor("default_color","RichTextLabel",new(.77f,.77f,.66f));
        _hudTheme=theme;GetWindow().Theme=theme;
    }
    private void NewParty(int size,bool melee=true)
    {
        _state=CreateExpedition(size,melee);_autosaveBlocked=false;_player.Position=new(0,.9f,2);_selectedHeroId=_state.Party[0].Id;Refresh();Say(melee?"New melee training expedition.":"New synthetic spell sandbox.");
    }
    private void ShowMenu()
    {
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog { Theme=_hudTheme,Title="Expedition",Unresizable=true,WrapControls=false };AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var scroll=new ScrollContainer();dialog.AddChild(scroll);var body=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };scroll.AddChild(body);
        var size=new OptionButton();for(int i=1;i<=8;i++)size.AddItem($"{i} adventurer{(i==1?"":"s")}");size.Select(_state.Party.Length-1);body.AddChild(size);
        Button(body,"New party",()=>Try(()=>{NewParty(size.Selected+1);dialog.QueueFree();}));
        var opponents=new OptionButton();for(int n=1;n<=8;n++)opponents.AddItem($"{n} opponents");opponents.Select(2);body.AddChild(opponents);
        Button(body,"New tactical battle",()=>Try(()=>{NewTactical(size.Selected+1,opponents.Selected+1);dialog.QueueFree();ShowTactics();}));
        Button(body,"Tactical field",ShowTactics);
        Button(body,"Spell sandbox",()=>Try(()=>{NewParty(size.Selected+1,false);dialog.QueueFree();}));
        Button(body,"Save",Save);Button(body,"Load",()=>{Load();size.Select(_state.Party.Length-1);});
        Button(body,"Monster reference",ShowMonsters);Button(body,"Spell reference",ShowGrimoire);Button(body,"Combat facts",ShowCombatFacts);
        Button(body,"Combination reference",ShowCombinations);
        Button(body,"Talk to keeper",()=>Say("Test keeper: The real monastery is still being researched."));
        Button(body,"Capture test",()=>Try(()=>{_state=Game.CaptureFirst(_state);Refresh();}));
        Button(body,"Escape",()=>Try(()=>{_state=Game.Escape(_state);Refresh();}));
        var screen=GetViewport().GetVisibleRect().Size;var dimensions=new Vector2I((int)Math.Min(400,screen.X-32),(int)Math.Min(490,screen.Y-32));
        scroll.Position=new(12,12);scroll.Size=new(dimensions.X-24,dimensions.Y-64);dialog.PopupCentered(dimensions);
    }
    private Button Button(Control parent,string text,Action action)
    {
        var button=new Button { Text=text,CustomMinimumSize=new(0,_touchMode?56:32),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };
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
        if(compact!=_compact) { _partyPanel.Visible=false;_logPanel.Visible=!compact;_compact=compact; }
        _partyToggle.Visible=compact;_logToggle.Visible=compact;
        _leftPortraits.Visible=!compact;_rightPortraits.Visible=!compact;_compass.Visible=!compact;_realms.Visible=!compact;_selection.Visible=!compact;
        _title.Position=bounds.Position+new Vector2(4,0);
        _partyToggle.Position=new(bounds.End.X-180,bounds.Position.Y);_partyToggle.Size=new(84,_touchMode?52:40);
        _logToggle.Position=new(bounds.End.X-88,bounds.Position.Y);_logToggle.Size=new(84,_touchMode?52:40);
        int columns=compact?Math.Max(3,Math.Min(6,(int)(bounds.Size.X/110))):6;_actions.Columns=columns;
        int rows=(6+columns-1)/columns;float actionHeight=compact?rows*(_touchMode?56:32)+(rows-1)*4+16:64;
        _actionsPanel.Position=new(bounds.Position.X,bounds.End.Y-actionHeight);_actionsPanel.Size=new(bounds.Size.X,actionHeight);
        _actions.SizeFlagsHorizontal=compact?Control.SizeFlags.ExpandFill:Control.SizeFlags.ShrinkBegin;
        foreach(var node in _actions.GetChildren().OfType<Button>())node.CustomMinimumSize=new(compact?0:78,_touchMode?56:32);
        float top=bounds.Position.Y+(compact?62:22);
        float dock=compact?0:128;
        float worldBottom=_actionsPanel.Position.Y-dock-6;
        float side=148;
        _leftPortraits.Position=new(bounds.Position.X,top);_leftPortraits.Size=new(side,worldBottom-top-4);
        _rightPortraits.Position=new(bounds.End.X-side,top);_rightPortraits.Size=new(side,worldBottom-top-4);
        _compass.Position=new(bounds.Position.X,worldBottom);_compass.Size=new(side,dock);
        _realms.Position=new(bounds.End.X-side,worldBottom);_realms.Size=new(side,dock);
        _enemy.Position=new(bounds.Position.X+(compact?0:side+12),top+4);_enemy.Size=new(bounds.Size.X-(compact?0:2*side+24),48);
        _partyPanel.Position=new(bounds.Position.X,top);_partyPanel.Size=new(240,Math.Max(160,_actionsPanel.Position.Y-top-10));
        _logPanel.Position=compact ? new(bounds.End.X-Math.Min(400,bounds.Size.X),top+52) : new(bounds.Position.X+side+8,worldBottom);
        _logPanel.Size=compact ? new(Math.Min(400,bounds.Size.X),Math.Max(140,_actionsPanel.Position.Y-top-62)) : new(bounds.Size.X-2*side-16,dock);
        _pad.Visible=_touchMode;_pad.Position=new(bounds.Position.X+8,_actionsPanel.Position.Y-154);_pad.Size=new(144,144);
        _lookRect=new Rect2(new(bounds.Position.X+(compact?0:side+8),top+52),new(bounds.Size.X-(compact?0:2*side+16),Math.Max(0,worldBottom-top-52)));
        _ui.QueueRedraw();
    }

    private void DrawChrome()
    {
        var size=GetViewport().GetVisibleRect().Size;var dark=new Color(.045f,.050f,.046f);var edge=new Color(.28f,.27f,.22f);
        float top=_compact?0:30;float bottom=_compact?size.Y-_actionsPanel.Position.Y:size.Y-_compass.Position.Y;
        if(top>0)_ui.DrawRect(new Rect2(0,0,size.X,top),dark);
        _ui.DrawRect(new Rect2(0,size.Y-bottom,size.X,bottom),dark);
        if(!_compact)
        {
            _ui.DrawRect(new Rect2(0,top,160,size.Y-bottom-top),dark);
            _ui.DrawRect(new Rect2(size.X-160,top,160,size.Y-bottom-top),dark);
        }
        _ui.DrawLine(new(0,size.Y-bottom),new(size.X,size.Y-bottom),edge,2);
        _ui.DrawLine(new(0,top),new(size.X,top),edge,2);
    }
    private void Try(Action action) { try { action(); } catch (Exception e) { Say(e.Message); } }
    private void Say(string message) { _log.AppendText(message+"\n"); }
    private void Refresh()
    {
        foreach (var child in _heroes.GetChildren()) { _heroes.RemoveChild(child); child.QueueFree(); }
        foreach (var h in _state.Party)
        {
            string equipment = Equipment.ItemId(_state,h.Id).HasValue && _catalog is not null ? $"\n{_catalog.Find(Equipment.ItemId(_state,h.Id)!.Value).Name}" : "";
            string effects=string.Join(" · ",(h.Effects ?? []).Select(e=>$"{e.Kind} {e.Rounds}r"));
            string mana=string.Join(" · ",(h.Mana ?? []).Select(p=>$"{p.Realm} {p.Current}/{p.Max}"));
            _heroes.AddChild(new Label { Text=$"{h.Name}\n{h.Race} · {h.Profession}\nHP {h.Hp}/{h.MaxHp}   XP {h.Xp}\n{mana}\n{effects}{equipment}{(h.Captive ? "\nCAPTIVE" : "")}", AutowrapMode=TextServer.AutowrapMode.WordSmart });
        }
        if(!_state.Party.Any(h=>h.Id==_selectedHeroId))_selectedHeroId=_state.Party[0].Id;
        var selected=_state.Party.Single(h=>h.Id==_selectedHeroId);
        int left=(_state.Party.Length+1)/2;
        for(int i=0;i<8;i++)
        {
            int index=i<4 ? i : left+i-4;
            bool occupied=i<4 ? i<left : index<_state.Party.Length;
            _portraits[i].Hero=occupied?_state.Party[index]:null;_portraits[i].Selected=occupied && _portraits[i].Hero!.Id==_selectedHeroId;_portraits[i].QueueRedraw();
        }
        _selection.Text=$"{selected.Name} · HP {selected.Hp}/{selected.MaxHp}\n"+(Equipment.ItemId(_state,selected.Id) is int id && _catalog is not null?_catalog.Find(id).Name:"Unarmed");
        _realms.Available=Magic.Spells.Where(s=>(selected.Spellbook ?? []).Contains(s.Id)).Select(s=>s.Realm).Distinct().ToArray();_realms.QueueRedraw();
        _compass.EnemyAlive=TacticalCombat.Enemies(_state).Any(e=>e.Hp>0);_compass.QueueRedraw();
        _title.Text=_state.Melee?.Field is not null?"Tactical laboratory · grid positions":_state.Melee is null?"Legacy training rules":"Melee training · restored physical rules";
        _enemy.Text=$"{_state.Enemy.Id} · HP {_state.Enemy.Hp} · Round {_state.Round}\n"+string.Join(" · ",(_state.Enemy.Effects ?? []).Select(e=>$"{e.Kind} {e.Rounds}r"));
        _quest.Text=$"Captivity fixture: {_state.Quest}";
        _sentinel.Visible=_state.Melee?.Field is null && _state.Enemy.Hp>0;
        if(_state.Melee?.Field is not null)_enemy.Text=$"Tactical field · Round {_state.Round} · {TacticalCombat.Enemies(_state).Count(e=>e.Hp>0)} opponents alive";
        _size.Select(_state.Party.Length-1);
    }
    private void Attack()
    {
        if(_state.Melee?.Field is not null){ShowTactics();return;}
        if (_state.Melee is null && _player.Position.DistanceTo(_enemyPosition)>4.5f) { Say("Too far from the sentinel. Move closer."); return; }
        Try(() => { var r=Game.Attack(_state,_catalog,_state.Melee is null?null:_player.Position.DistanceTo(_enemyPosition)); _state=r.State; foreach(var e in r.Events) Say(e); Refresh(); });
    }
    private void Save() => Try(()=>{SaveTo(SavePath);Say("Saved.");});

    private void Cast(SpellOrder order) => CastOrders([order]);
    private void CastOrders(IReadOnlyList<SpellOrder> orders)
    {
        if(_player.Position.DistanceTo(_enemyPosition)>4.5f)throw new InvalidOperationException("Too far from the sentinel.");
        var result=Game.CastRound(_state,orders,_catalog);_state=result.State;
        foreach(var entry in result.Events)Say(entry);
        Refresh();
    }

    private void ShowCombatFacts()
    {
        if(_state.Melee is not { } encounter){Say("This saved expedition uses legacy training rules. Start a new expedition for restored melee.");return;}
        _dialogs++;_touchInput.Reset();
        var dialog=new AcceptDialog {Theme=_hudTheme,Title="Melee training facts",Unresizable=false};AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;
        dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var scroll=new ScrollContainer {CustomMinimumSize=new(360,280)};dialog.AddChild(scroll);
        var text=new Label {Name="CombatFactsText",AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(340,0),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};scroll.AddChild(text);
        var lines=new List<string>{"Authored training combatants; recovered ordinary melee formulas.\nRecovered initiative for new parties; target selection is a training rule.\n"};
        foreach(var (name,facts) in _state.Party.Select(h=>(h.Name,encounter.Party[h.Id])).Append((_state.Enemy.Id,encounter.Opponent)))
            lines.Add($"{name}\nSTR {facts.Strength} · INT {facts.Attributes.Intelligence} · DEX {facts.Attributes.Dexterity} · SPD {facts.Attributes.Speed} · SEN {facts.Attributes.Senses}\n"+
                $"Combat {facts.CombatSkill} · weapon skill {facts.WeaponSkill} · stealth {facts.Stealth}\n"+
                string.Join(", ",facts.History.Select(h=>$"{h.Profession} level {h.Levels}"))+"\nBody protection: "+
                $"{facts.BodyArmor.Head}/{facts.BodyArmor.Torso}/{facts.BodyArmor.Legs}/{facts.BodyArmor.Hands}/{facts.BodyArmor.Feet}\n");
        text.Text=string.Join("\n",lines);dialog.PopupCentered(new Vector2I(440,400));
    }

    private void ShowRestoredMagic()
    {
        if(_spellCatalog is null || _state.Melee?.Magic is null)
        {Say("Import the original spell catalog, then start a new party to enable the training Bishop's Frost / Energy Blast / Heal Wounds.");return;}
        var casters=_state.Party.Where(h=>_state.Melee.Magic[h.Id].LearnedSpells.Length>0).ToArray();
        var spells=_spellCatalog.Spells.Where(RestoredMagic.SupportedSpell).ToArray();
        if(casters.Length==0 || spells.Length==0){Say("No supported learned original spells.");return;}
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog {Theme=_hudTheme,Title="Restored magic · training Bishop"};AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;
        dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var body=new VBoxContainer();dialog.AddChild(body);
        var caster=new OptionButton();foreach(var h in casters)caster.AddItem(h.Name);body.AddChild(caster);
        var spell=new OptionButton();foreach(var s in spells)spell.AddItem(s.Name);body.AddChild(spell);
        var power=new OptionButton();for(int i=1;i<=7;i++)power.AddItem($"Power {i}");body.AddChild(power);
        var target=new OptionButton();body.AddChild(target);
        var detail=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(350,135)};body.AddChild(detail);
        var cast=new Button {Name="RestoredCastButton",Text="Cast / resolve round"};body.AddChild(cast);
        RestoredCastOrder Order()=>new(casters[caster.Selected].Id,spells[spell.Selected].Id,power.Selected+1,
            spells[spell.Selected].Id==6?_state.Party[target.Selected].Id:_state.Enemy.Id);
        void Targets()
        {
            target.Clear();bool heal=spells[spell.Selected].Id==6;
            if(heal)foreach(var hero in _state.Party)target.AddItem($"{hero.Name} · {hero.Hp}/{hero.MaxHp} HP");
            else target.AddItem(_state.Enemy.Id);
            target.Disabled=!heal;
        }
        void Update()
        {
            try
            {
                var prepared=RestoredMagic.Prepare(_state,Order(),_spellCatalog);
                bool heal=prepared.Spell.Id==6;
                if(heal)for(int i=0;i<_state.Party.Length;i++)target.SetItemText(i,$"{_state.Party[i].Name} · {_state.Party[i].Hp}/{_state.Party[i].MaxHp} HP");
                detail.Text=$"{prepared.Spell.Realm} · {prepared.ManaCost} mana · failure {Math.Min(100,prepared.Risk.FailureThreshold)}% · {prepared.Risk.Band}\n"+
                    $"{prepared.Power*prepared.Spell.DamageDice}d{prepared.Spell.DamageSides}, then Power Cast"+(heal?"; heals selected ally.":" and target resistance.")+"\n"+
                    (heal?"Failure consumes mana. Full HP needs no healing dice.":"Failure consumes mana. Backfire can hit the caster.");
                cast.Disabled=false;
            }
            catch(Exception e){detail.Text=e.Message;cast.Disabled=true;}
        }
        caster.ItemSelected+=_=>Update();spell.ItemSelected+=_=>{Targets();Update();};power.ItemSelected+=_=>Update();target.ItemSelected+=_=>Update();
        cast.Pressed+=()=>Try(()=>
        {
            var result=MeleeCombat.Advance(_state,_catalog!,_player.Position.DistanceTo(_enemyPosition),[Order()],_spellCatalog);
            _state=result.State;foreach(var entry in result.Events)Say(entry);Refresh();Update();
        });
        Targets();Update();dialog.PopupCentered(new Vector2I(390,350));
    }

    private void ShowMagic()
    {
        if(_state.Melee?.Field is not null){ShowTactics();return;}
        if(_state.Melee is not null){ShowRestoredMagic();return;}
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog { Theme=_hudTheme,Title="Prototype magic",Unresizable=true,WrapControls=false };
        AddChild(dialog);dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;
        dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var body=new VBoxContainer();dialog.AddChild(body);
        var caster=new OptionButton();foreach(var h in _state.Party)caster.AddItem(h.Name);caster.Select(Array.FindIndex(_state.Party.ToArray(),h=>h.Id==_selectedHeroId));body.AddChild(caster);
        var spell=new OptionButton();foreach(var s in Magic.Spells)spell.AddItem($"{s.Name} · {s.Realm}");if(_initialRealm is not null)spell.Select(Math.Max(0,Array.FindIndex(Magic.Spells.ToArray(),s=>s.Realm==_initialRealm)));_initialRealm=null;body.AddChild(spell);
        var power=new OptionButton();for(int i=1;i<=3;i++)power.AddItem($"Power {i}");body.AddChild(power);
        var target=new OptionButton();foreach(var h in _state.Party)target.AddItem(h.Name);body.AddChild(target);
        var details=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(400,160) };body.AddChild(details);
        var cast=new Button { Text="Cast / next round" };body.AddChild(cast);
        var queued=new Dictionary<string,SpellOrder>();
        var plan=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart };body.AddChild(plan);
        var queue=new Button { Text="Queue / replace this caster's order" };body.AddChild(queue);
        var resolve=new Button { Text="Resolve queued round",Disabled=true };body.AddChild(resolve);
        var clear=new Button { Text="Clear orders" };body.AddChild(clear);
        body.AddChild(new Label { Text="Synthetic spells and formulas.\nOriginal success, resistance and Power Cast are pending.",AutowrapMode=TextServer.AutowrapMode.WordSmart });
        SpellOrder Order()
        {
            var s=Magic.Spells[spell.Selected];
            return new(_state.Party[caster.Selected].Id,s.Id,power.Selected+1,Magic.TargetsParty(s) ? _state.Party[target.Selected].Id : _state.Enemy.Id);
        }
        void Details()
        {
            var s=Magic.Spells[spell.Selected];var order=Order();
            target.Visible=Magic.TargetsParty(s);
            var denial=Magic.Denial(_state,order);
            if(_player.Position.DistanceTo(_enemyPosition)>4.5f)denial="Approach the sentinel to cast in battle.";
            var h=_state.Party[caster.Selected];
            int mana=(h.Mana ?? []).FirstOrDefault(p=>p.Realm==s.Realm)?.Current ?? 0;
            string effect=s.Effect switch {
                "ward"=>$"Reduce incoming sentinel damage by {2*order.Power}, 3 rounds",
                "bind"=>"Skip the enemy action this round (if cast before it)",
                "burn"=>$"{order.Power} damage at round end, 3 rounds",
                _=>$"{order.Power}d6 + {order.Power} {(s.Heals ? "healing" : "damage")}" };
            details.Text=$"{s.Name} · Power {order.Power}\n{effect}\nCost: {s.CostPerPower*order.Power} {s.Realm} mana · Available: {mana}\n\n{(denial ?? "Ready. This replaces the caster's weapon attack; everyone else takes their normal turn.")}";
            cast.Disabled=denial is not null;queue.Disabled=denial is not null;
            var invalid=queued.Values.Select(o=>Magic.Denial(_state,o)).FirstOrDefault(d=>d is not null);
            resolve.Disabled=queued.Count==0 || invalid is not null || _player.Position.DistanceTo(_enemyPosition)>4.5f;
            plan.Text="Queued: "+(queued.Count==0 ? "none" : string.Join(", ",queued.Values.Select(o=>$"{_state.Party.Single(h=>h.Id==o.HeroId).Name}: {Magic.Find(o.SpellId).Name} P{o.Power}")))+(invalid is null ? "" : $"\n{invalid}");
        }
        caster.ItemSelected+=_=>Details();spell.ItemSelected+=_=>Details();power.ItemSelected+=_=>Details();target.ItemSelected+=_=>Details();
        cast.Pressed+=()=>Try(()=>{Cast(Order());queued.Clear();Details();});
        queue.Pressed+=()=>{var o=Order();queued[o.HeroId]=o;Details();};
        clear.Pressed+=()=>{queued.Clear();Details();};
        resolve.Pressed+=()=>Try(()=>{CastOrders(queued.Values.ToArray());queued.Clear();Details();});
        var size=GetViewport().GetVisibleRect().Size;var dimensions=new Vector2I((int)Math.Min(640,size.X-32),(int)Math.Min(530,size.Y-32));
        var scroll=new ScrollContainer();dialog.RemoveChild(body);dialog.AddChild(scroll);scroll.AddChild(body);
        scroll.Position=new(12,12);scroll.Size=new(dimensions.X-24,dimensions.Y-64);body.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        details.CustomMinimumSize=new(Math.Min(400,dimensions.X-60),130);
        Details();dialog.PopupCentered(dimensions);
    }
    private void ShowCrafting()
    {
        if(_catalog is null) { Say("No item catalog loaded.");return; }
        if(!Crafting.UsesFixture(_catalog)) { ShowCombinations();return; }
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog { Theme=_hudTheme,Title="Test crafting",Unresizable=true,WrapControls=false };
        AddChild(dialog);dialog.Confirmed+=dialog.QueueFree;dialog.CloseRequested+=dialog.QueueFree;
        dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var scroll=new ScrollContainer();dialog.AddChild(scroll);
        var body=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };scroll.AddChild(body);
        var hero=new OptionButton();foreach(var h in _state.Party)hero.AddItem(h.Name);hero.Select(Array.FindIndex(_state.Party.ToArray(),h=>h.Id==_selectedHeroId));body.AddChild(hero);
        var recipe=new OptionButton();foreach(var r in Crafting.Recipes)recipe.AddItem(r.Name);body.AddChild(recipe);
        var detail=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart };body.AddChild(detail);
        var craft=new Button { Text="Craft one batch",CustomMinimumSize=new(0,56) };body.AddChild(craft);
        body.AddChild(new Label { Text="Synthetic recipes and skill gate.\nOriginal alchemy, gadgets and quest recipes are pending.",AutowrapMode=TextServer.AutowrapMode.WordSmart });
        CraftOrder Order()=>new(_state.Party[hero.Selected].Id,Crafting.Recipes[recipe.Selected].Id);
        void Details()
        {
            var r=Crafting.Recipes[recipe.Selected];var h=_state.Party[hero.Selected];
            var reason=Crafting.Denial(_state,Order(),_catalog);
            detail.Text=r.Name+"\n\n"+string.Join("\n",r.Ingredients.Select(i=>$"{_catalog.Find(i.ItemId).Name}: need {i.Count}, bag {Inventory.Count(_state,i.ItemId)}"))+
                $"\n\nResult: {_catalog.Find(r.Result.ItemId).Name} ×{r.Result.Count}"+
                (r.Skill is null ? "" : $"\n{r.Skill}: {(h.Skills ?? []).FirstOrDefault(v=>v.Id==r.Skill)?.Value ?? 0} / {r.MinimumSkill}")+
                $"\n\n{reason ?? "Ready. Equipped copies are not ingredients."}";
            craft.Disabled=reason is not null;
        }
        hero.ItemSelected+=_=>Details();recipe.ItemSelected+=_=>Details();
        craft.Pressed+=()=>Try(()=>{_state=Crafting.Craft(_state,Order(),_catalog);Refresh();Details();Say("Crafted "+Crafting.Recipes[recipe.Selected].Name+".");});
        var screen=GetViewport().GetVisibleRect().Size;
        var dimensions=new Vector2I((int)Math.Min(640,screen.X-32),(int)Math.Min(520,screen.Y-32));
        scroll.Position=new(12,12);scroll.Size=new(dimensions.X-24,dimensions.Y-64);detail.CustomMinimumSize=new(Math.Min(400,dimensions.X-60),180);
        Details();dialog.PopupCentered(dimensions);
    }

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
        _autosaveBlocked=true;
        using var file=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Read);
        if(file is null)throw new IOException("No saved expedition");
        string json=file.GetAsText();
        var s=JsonSerializer.Deserialize<ClientSave>(json) ?? throw new IOException("Empty save");
        using var document=JsonDocument.Parse(json);
        var game=Game.Load(document.RootElement.GetProperty("Game").GetRawText(),_catalog);
        Equipment.ValidateLoadedEquipment(game,_catalog,s.ItemDatabaseHash);
        if(s.Schema!=1 || !float.IsFinite(s.X) || !float.IsFinite(s.Z) || !float.IsFinite(s.Yaw) || !float.IsFinite(s.Pitch) || Math.Abs(s.X)>5.3f || s.Z < -16.5f || s.Z>4.5f || Math.Abs(s.Pitch)>1.2f)
            throw new IOException("Invalid saved camera position");
        _autosaveBlocked=false;_state=game;_player.Position=new(s.X,0.9f,s.Z);_player.Rotation=new(0,s.Yaw,0);_pitch=s.Pitch;_camera.Rotation=new(_pitch,0,0);_touchInput.Reset();Refresh();
    }
    public override void _Notification(int what)
    {
        if(!_ready)return;
        if(what==NotificationApplicationPaused)
        {
            _paused=true;_touchInput.Reset();Input.MouseMode=Input.MouseModeEnum.Visible;
            if(!_autosaveBlocked)Try(()=>SaveTo(AutoSavePath));
            else Say("Autosave paused: load a valid save or start a new expedition to resume.");
        }
        if(what==NotificationApplicationResumed) { _paused=false;_touchInput.Reset(); }
    }

    private void ShowItems()
    {
        if (_catalog is null) { Say("Import your installation's item catalog using the README to open original items."); return; }
        Input.MouseMode=Input.MouseModeEnum.Visible;_touchInput.Reset();_dialogs++;
        var dialog=new AcceptDialog { Theme=_hudTheme,Title="Party inventory", Size=new(920,620),Unresizable=true,WrapControls=false };
        AddChild(dialog);
        dialog.Confirmed+=dialog.QueueFree; dialog.CloseRequested+=dialog.QueueFree;dialog.TreeExited+=()=>{_dialogs=Math.Max(0,_dialogs-1);_touchInput.Reset();};
        var layout=new VBoxContainer(); dialog.AddChild(layout);
        var screen=GetViewport().GetVisibleRect().Size;
        var dialogSize=new Vector2I((int)Math.Min(920,screen.X-32),(int)Math.Min(620,screen.Y-32));
        bool narrow=dialogSize.X<740;
        layout.Position=new(12,12);layout.Size=new(dialogSize.X-24,dialogSize.Y-64);
        var search=new LineEdit { PlaceholderText="Search item name" };layout.AddChild(search);
        var browse=new CheckButton { Text="Browse full catalog" };layout.AddChild(browse);
        var hero=new OptionButton();foreach(var h in _state.Party)hero.AddItem(h.Name);hero.Select(Array.FindIndex(_state.Party.ToArray(),h=>h.Id==_selectedHeroId));layout.AddChild(hero);
        BoxContainer row=narrow ? new VBoxContainer() : new HBoxContainer();row.SizeFlagsVertical=Control.SizeFlags.ExpandFill;layout.AddChild(row);
        var list=new ItemList { Name="InventoryDefinitions",CustomMinimumSize=new(narrow?0:Math.Min(360,dialogSize.X*0.4f),narrow?120:180),SizeFlagsVertical=Control.SizeFlags.ExpandFill,AutoHeight=false };row.AddChild(list);
        var detailColumn=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };row.AddChild(detailColumn);
        var detailScroll=new ScrollContainer { SizeFlagsVertical=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(0,narrow?90:160) };detailColumn.AddChild(detailScroll);
        var detail=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,CustomMinimumSize=new(narrow?0:Math.Max(220,dialogSize.X-430),0) };detailScroll.AddChild(detail);
        var copies=new OptionButton { Name="InventoryInstances",ClipText=true };detailColumn.AddChild(copies);
        var stackActions=new HBoxContainer();detailColumn.AddChild(stackActions);
        var split=new Godot.Button {Name="InventorySplit",Text="Split one",Disabled=true};stackActions.AddChild(split);
        var merge=new Godot.Button {Name="InventoryMerge",Text="Merge compatible",Disabled=true};stackActions.AddChild(merge);
        var assign=new Godot.Button { Text="Equip from party bag",Disabled=true };detailColumn.AddChild(assign);
        var remove=new Godot.Button { Text="Return weapon to bag",Disabled=true };detailColumn.AddChild(remove);
        detailColumn.AddChild(new Label { Text="Shared bag; one equipped copy per hero.\nCurse effects and removal magic are pending." });
        ItemDefinition[] shown=[];
        ItemDefinition? selected=null;
        ItemInstance[] instances=[];
        ItemInstance? SelectedInstance()=>copies.Selected>=0 && copies.Selected<instances.Length?instances[copies.Selected]:null;
        string? MergeSource(ItemInstance destination)=>instances.FirstOrDefault(i=>i.Id!=destination.Id &&
            i.Identified==destination.Identified && i.CurseRemoved==destination.CurseRemoved && i.ResourceRemaining==destination.ResourceRemaining)?.Id;
        void Details()
        {
            var h=_state.Party[hero.Selected];
            remove.Disabled=!Equipment.ItemId(_state,h.Id).HasValue || h.Captive || h.Hp==0;
            var previous=SelectedInstance()?.Id;
            instances=selected is null?[]:Inventory.Bag(_state).Where(i=>i.DefinitionId==selected.Id).OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray();
            copies.Clear();for(int i=0;i<instances.Length;i++)
            {
                var instance=instances[i];copies.AddItem($"Copy {i+1} · ×{instance.Quantity}"+
                    (instance.ResourceRemaining is int remaining?$" · {Inventory.Rules(_catalog)[instance.DefinitionId].Resource}: {remaining}":"")+
                    (instance.Identified?"":" · unidentified")+(instance.CurseRemoved?" · curse removed":""));
            }
            if(instances.Length>0)copies.Select(Math.Max(0,Array.FindIndex(instances,i=>i.Id==previous)));
            var owned=SelectedInstance();copies.Disabled=instances.Length==0;
            split.Disabled=owned is null || owned.Quantity<2;
            merge.Disabled=owned is null || MergeSource(owned) is null || owned.Quantity>=Inventory.Rules(_catalog)[owned.DefinitionId].StackLimit;
            if(selected is null) { detail.Text="Choose an item.";assign.Disabled=true;return; }
            var reason=Equipment.Denial(h,selected);
            long count=Inventory.Count(_state,selected.Id);
            detail.Text=$"{selected.Name}  [ID {selected.Id}]\nIn bag: {count} · Equipped: {_state.Party.Count(p=>Equipment.ItemId(_state,p.Id)==selected.Id)}\n\n{selected.DamageDescription}\nInitiative {selected.Initiative:+0;-0;0} · To hit {selected.ToHit:+0;-0;0}\nWeight {selected.WeightTenthsLb/10m} lb\nCursed: {selected.Cursed}"+
                (owned is null?"":$"\nIdentified: {owned.Identified} · Curse removed: {owned.CurseRemoved}"+(owned.ResourceRemaining is int resource?$"\n{Inventory.Rules(_catalog)[owned.DefinitionId].Resource}: {resource}":""))+
                $"\n\n{(reason ?? (count==0 ? "No spare copy in the party bag." : "Available to equip."))}";
            assign.Disabled=reason is not null || h.Captive || h.Hp==0 || owned is null;
        }
        void Filter(string term)
        {
            int? selectedId=selected?.Id;
            shown=_catalog.Items.Where(i=>(browse.ButtonPressed || Inventory.Count(_state,i.Id)>0 || _state.Party.Any(h=>Equipment.ItemId(_state,h.Id)==i.Id)) && i.Name.Contains(term,StringComparison.OrdinalIgnoreCase)).ToArray();
            list.Clear();foreach(var i in shown)list.AddItem($"{i.Name} · bag {Inventory.Count(_state,i.Id)} · worn {_state.Party.Count(h=>Equipment.ItemId(_state,h.Id)==i.Id)}");
            selected=shown.FirstOrDefault(i=>i.Id==selectedId) ?? shown.FirstOrDefault();
            if(selected is not null)list.Select(Array.IndexOf(shown,selected));
            Details();
        }
        search.TextChanged+=Filter;
        browse.Toggled+=_=>Filter(search.Text);
        list.ItemSelected+=(long i)=>{selected=shown[(int)i];Details();};
        hero.ItemSelected+=(long _) => Details();
        assign.Pressed+=()=>Try(()=>{if(selected is null || SelectedInstance() is not ItemInstance instance)return;_state=Equipment.EquipInstance(_state,_state.Party[hero.Selected].Id,instance.Id,_catalog);Refresh();Say($"Equipped {selected.Name} from the party bag.");Filter(search.Text);});
        remove.Pressed+=()=>Try(()=>{_state=Equipment.Unequip(_state,_state.Party[hero.Selected].Id,_catalog);Refresh();Say("Weapon returned to the party bag.");Filter(search.Text);});
        copies.ItemSelected+=_=>Details();
        split.Pressed+=()=>Try(()=>{if(SelectedInstance() is not ItemInstance instance)return;_state=Inventory.Split(_state,instance.Id,1,_catalog);Refresh();Filter(search.Text);});
        merge.Pressed+=()=>Try(()=>{if(SelectedInstance() is not ItemInstance instance || MergeSource(instance) is not string source)return;
            var merged=Inventory.Merge(_state,instance.Id,source,_catalog);_state=merged.State;Say($"Merged {merged.Moved} units.");Refresh();Filter(search.Text);});
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
        if(_dialogs>0 || _paused)return;
        if(e is InputEventMouseButton b && b.ButtonIndex==MouseButton.Right && !_touchMode)Input.MouseMode=b.Pressed?Input.MouseModeEnum.Captured:Input.MouseModeEnum.Visible;
        if(e is InputEventKey k && k.Pressed && k.Keycode==Key.Escape)Input.MouseMode=Input.MouseModeEnum.Visible;
        if(e is InputEventMouseMotion m && Input.MouseMode==Input.MouseModeEnum.Captured)Look(m.Relative,0.003f);
    }
    private void Look(Vector2 relative,float sensitivity)
    { _player.RotateY(-relative.X*sensitivity);_pitch=Math.Clamp(_pitch-relative.Y*sensitivity,-1.2f,1.2f);_camera.Rotation=new(_pitch,0,0); }
    public override void _PhysicsProcess(double delta)
    {
        if(!_ready)return;
        _compass.PlayerPosition=_player.Position;_compass.Yaw=_player.Rotation.Y;_compass.QueueRedraw();
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
            CheckRecipeReference();
            var pad=_pad.GetGlobalRect();var center=pad.GetCenter();
            _Input(new InputEventScreenTouch { Index=0,Pressed=true,Position=center });
            _Input(new InputEventScreenDrag { Index=0,Position=center+new Vector2(0,-60),Relative=new(0,-60) });
            if(_touchInput.Movement.Y>=-0.9f)throw new Exception("Touch movement failed");
            var view=_lookRect.GetCenter();_Input(new InputEventScreenTouch { Index=1,Pressed=true,Position=view });
            _Input(new InputEventScreenDrag { Index=1,Position=view+new Vector2(30,0),Relative=new(30,0) });
            var before=_player.Rotation.Y;_PhysicsProcess(1.0/60);
            if(_player.Rotation.Y==before)throw new Exception("Touch look failed");
            _player.Position=new(0,0.9f,-4);
            _state=Game.Attack(_state,_catalog,_state.Melee is null?null:_player.Position.DistanceTo(_enemyPosition)).State;var saved=Game.Save(_state);
            _Notification((int)NotificationApplicationPaused);
            if(_touchInput.MovePointer is not null || _touchInput.LookPointer is not null)throw new Exception("Pause left stuck gesture");
            _state=Game.Create(8);LoadFrom(AutoSavePath);
            if(Game.Save(_state)!=saved)throw new Exception("Background autosave failed");
            _Notification((int)NotificationApplicationResumed);
            string valid=Godot.FileAccess.GetFileAsString(AutoSavePath);
            var invalid=System.Text.Json.Nodes.JsonNode.Parse(valid)!;invalid["Game"]!["Schema"]=99;
            string blocked=invalid.ToJsonString();
            using(var file=Godot.FileAccess.Open(AutoSavePath,Godot.FileAccess.ModeFlags.Write))file.StoreString(blocked);
            string beforeFailedLoad=Game.Save(_state);bool rejected=false;
            try {LoadFrom(AutoSavePath);}catch(Exception){rejected=true;}
            _Notification((int)NotificationApplicationPaused);
            if(!rejected || Game.Save(_state)!=beforeFailedLoad || Godot.FileAccess.GetFileAsString(AutoSavePath)!=blocked)
                throw new Exception("Failed save migration was overwritten by autosave");
            using(var file=Godot.FileAccess.Open(AutoSavePath,Godot.FileAccess.ModeFlags.Write))file.StoreString(valid);
            LoadFrom(AutoSavePath);_Notification((int)NotificationApplicationResumed);
            GD.Print("PASS: multitouch movement/look, pause reset, atomic autosave and restore; failed-load source protection");GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString());GetTree().Quit(1); }
    }

    private void CheckInventoryInterface()
    {
        if(_catalog is null)return;
        var original=_state;
        var stack=_catalog.Items.FirstOrDefault(i=>i.Instance is {Mode:1,MaxStack:>=3}) ??
            (Crafting.UsesFixture(_catalog)?_catalog.Find(78):null);
        if(stack is null)return;
        _state=Inventory.Add(Game.Create(1),stack.Id,3,_catalog);
        ShowItems();
        var dialog=GetChildren().OfType<AcceptDialog>().Last();
        try
        {
            var definitions=dialog.FindChild("InventoryDefinitions",true,false) as ItemList ??throw new Exception("Missing inventory definitions");
            definitions.Select(0);definitions.EmitSignal(ItemList.SignalName.ItemSelected,0L);
            var copies=dialog.FindChild("InventoryInstances",true,false) as OptionButton ??throw new Exception("Missing inventory instances");
            var split=dialog.FindChild("InventorySplit",true,false) as Godot.Button ??throw new Exception("Missing split action");
            var merge=dialog.FindChild("InventoryMerge",true,false) as Godot.Button ??throw new Exception("Missing merge action");
            if(split.Disabled || copies.ItemCount!=1)throw new Exception("Stack selection unavailable");
            split.EmitSignal(Godot.Button.SignalName.Pressed);
            if(copies.ItemCount!=2 || Inventory.Count(_state,stack.Id)!=3)throw new Exception("Inventory split UI lost units");
            copies.Select(1);copies.EmitSignal(OptionButton.SignalName.ItemSelected,1L);
            if(merge.Disabled)throw new Exception("Compatible merge unavailable");
            merge.EmitSignal(Godot.Button.SignalName.Pressed);
            if(copies.ItemCount!=1 || Inventory.Count(_state,stack.Id)!=3)throw new Exception("Inventory merge UI lost units");
            GD.Print("PASS: inventory instance selector, split and merge UI conserve units");
        }
        finally {dialog.Free();_state=original;Refresh();}
    }
    private void CheckLegacyClientSave()
    {
        if(_catalog is null)return;
        SaveTo(SavePath);
        string current=Godot.FileAccess.GetFileAsString(SavePath);
        var root=System.Text.Json.Nodes.JsonNode.Parse(current)!;
        var game=root["Game"]!;game["Schema"]=1;game.AsObject().Remove("Items");game.AsObject().Remove("NextItemSequence");game.AsObject().Remove("ProjectileActions");game.AsObject().Remove("NextProjectileSequence");game.AsObject().Remove("Melee");
        game["Bag"]=JsonSerializer.SerializeToNode(Inventory.Bag(_state).GroupBy(i=>i.DefinitionId)
            .Select(g=>new ItemStack(g.Key,checked((int)g.Sum(i=>(long)i.Quantity)))).ToArray());
        foreach(var hero in game["Party"]!.AsArray())hero!["EquippedItemId"]=Equipment.ItemId(_state,hero["Id"]!.GetValue<string>());
        string legacy=root.ToJsonString();
        using(var file=Godot.FileAccess.Open(SavePath,Godot.FileAccess.ModeFlags.Write))file.StoreString(legacy);
        var expected=_state;_state=Game.Create(8);LoadFrom(SavePath);
        if(_state.Schema!=Game.SaveSchema || _state.Party.Any(h=>Equipment.ItemId(_state,h.Id)!=Equipment.ItemId(expected,h.Id)) ||
            Inventory.Bag(expected).Any(i=>Inventory.Count(_state,i.DefinitionId)!=Inventory.Count(expected,i.DefinitionId)) ||
            Godot.FileAccess.GetFileAsString(SavePath)!=legacy)throw new Exception("Client legacy save migration failed or overwrote its source");
        SaveTo(SavePath);
        if(Godot.FileAccess.GetFileAsString(SavePath).Contains("EquippedItemId"))throw new Exception("Client persisted obsolete ownership fields");
        GD.Print("PASS: client legacy inventory save migrates without overwriting source on load");
    }

    public void Smoke()
    {
        try
        {
            CheckRecipeReference();
            if((_spellsRequested || Godot.FileAccess.FileExists("res://demo-items.spells.json")) && _spellCatalog is null)throw new Exception("Requested spell reference was not loaded");
            if(_spellCatalog is not null) { _spellCatalog.Validate();GD.Print($"PASS: spell reference {_spellCatalog.Count} records with raw record hashes"); }
            if(_monstersRequested && _monsterCatalog is null)throw new Exception("Requested monster catalog was not loaded");
            if(_monsterCatalog is not null){_monsterCatalog.Validate();ShowMonsters();GetChildren().OfType<AcceptDialog>().Last().Free();GD.Print($"PASS: monster reference {_monsterCatalog.Count} records and browser");}
            if(_catalogRequested && _catalog is null) throw new Exception("Requested smoke catalog was not loaded");
            _player.Position=new(0,0.9f,-4);
            CheckTacticalInterface();
            var expectedMelee=Game.Attack(_state,_catalog,_player.Position.DistanceTo(_enemyPosition));
            Attack();
            if(_state.Melee is null || _state.Round!=1 || Game.Save(_state)!=Game.Save(expectedMelee.State) ||
                !expectedMelee.Events.Any(e=>e.Contains("d100"))) throw new Exception("Combat/UI smoke failed");
            ShowCombatFacts();
            var factsWindow=GetChildren().OfType<AcceptDialog>().Last();
            if(factsWindow.FindChild("CombatFactsText",true,false) is not Label factsLabel || !factsLabel.Text.Contains("Authored training combatants"))
                throw new Exception("Missing authored combat facts UI");
            factsWindow.Free();
            GD.Print("PASS: restored melee attack button, contact thresholds and authored combat facts UI");
            if(_state.Melee?.Magic is not null && _spellCatalog is not null)
            {
                var snapshot=_state;_state=CreateExpedition(1);
                ShowRestoredMagic();GetChildren().OfType<AcceptDialog>().Last().Free();
                var cast=MeleeCombat.Advance(_state,_catalog!,3,[new(_state.Party[0].Id,5,3)],_spellCatalog);
                if(!cast.Events.Any(e=>e.Contains("Energy Blast PL3")) || cast.State.Party[0].Mana!.Single(m=>m.Realm=="Fire").Current!=36)throw new Exception("Original casting smoke failed");
                if(_spellCatalog.Spells.Any(s=>s.Id==6 && RestoredMagic.SupportedSpell(s)))
                {
                    _state=CreateExpedition(1);_state=_state with {Party=_state.Party.SetItem(0,_state.Party[0] with {Hp=_state.Party[0].MaxHp-1})};
                    ShowRestoredMagic();var healDialog=GetChildren().OfType<AcceptDialog>().Last();
                    var controls=healDialog.GetChildren().OfType<VBoxContainer>().Single();
                    var choices=controls.GetChildren().OfType<OptionButton>().ToArray();
                    int healing=Array.FindIndex(_spellCatalog.Spells.Where(RestoredMagic.SupportedSpell).ToArray(),s=>s.Id==6);
                    choices[1].Select(healing);choices[1].EmitSignal(OptionButton.SignalName.ItemSelected,(long)healing);
                    var healButton=controls.GetChildren().OfType<Godot.Button>().Single(b=>b.Name=="RestoredCastButton");
                    if(healButton.Disabled)throw new Exception("Heal Wounds UI unexpectedly disabled");
                    healButton.EmitSignal(Godot.Button.SignalName.Pressed);
                    if(_state.Round!=1 || _state.Party[0].Mana!.Single(m=>m.Realm=="Divine").Current!=39)throw new Exception("Heal Wounds button did not execute the shared round");
                    healDialog.Free();
                }
                _state=snapshot;GD.Print("PASS: restored magic UI, imported direct spell / Heal Wounds buttons, mana and shared combat executor");
            }
            Save(); _state=Game.Create(8); Load();
            if(_state.Party.Length!=1 || _state.Round!=1) throw new Exception("Client save/load failed");
            _state=Game.CaptureFirst(_state); Refresh();
            _state=Game.Escape(_state); Refresh();
            if (_catalog is not null)
            {
                var cane=(_state.Melee is not null?_catalog.Find(Equipment.ItemId(_state,_state.Party[0].Id)!.Value):_catalog.Items.FirstOrDefault(i=>i.Name=="Cane of Corpus" && Equipment.Denial(_state.Party[0],i) is null)) ??
                    _catalog.Items.FirstOrDefault(i=>Equipment.Denial(_state.Party[0],i) is null);
                if(cane is null) throw new Exception("Smoke catalog has no eligible melee weapon");
                {
                    if(Inventory.Count(_state,cane.Id)==0)_state=Inventory.Add(_state,cane.Id,1,_catalog);
                    _state=Equipment.Equip(_state,_state.Party[0].Id,cane.Id,_catalog); Save(); _state=Game.Create(8); Load();
                    if(Equipment.ItemId(_state,_state.Party[0].Id)!=cane.Id) throw new Exception("Imported equipment save/load failed");
                    var expected=Game.Save(Game.Attack(_state,_catalog,_state.Melee is null?null:_player.Position.DistanceTo(_enemyPosition)).State);
                    Attack();
                    if(Game.Save(_state)!=expected) throw new Exception("Client did not use equipped weapon");
                    Save(); var continuation=Game.Save(Game.Attack(_state,_catalog,_state.Melee is null?null:_player.Position.DistanceTo(_enemyPosition)).State);
                    _state=Game.Create(8); Load();
                    if(Game.Save(Game.Attack(_state,_catalog,_state.Melee is null?null:_player.Position.DistanceTo(_enemyPosition)).State)!=continuation) throw new Exception("Weapon roll continuation failed");
                    long copies=Inventory.Count(_state,cane.Id);
                    _state=Equipment.Unequip(_state,_state.Party[0].Id,_catalog);Save();Load();
                    if(Equipment.ItemId(_state,_state.Party[0].Id).HasValue || Inventory.Count(_state,cane.Id)!=copies+1)throw new Exception("Inventory unequip/save failed");
                    CheckLegacyClientSave();CheckInventoryInterface();
                    GD.Print($"PASS: imported catalog {_catalog.Count}, equipment and save binding");
                }
            }
            _state=CreateExpedition(1,false);
            var fire=_state.Party[0].Mana!.Single(p=>p.Realm=="Fire").Current;
            Cast(new("hero-1","test-spark",2,_state.Enemy.Id));
            if(_state.Party[0].Mana!.Single(p=>p.Realm=="Fire").Current!=fire-4)throw new Exception("Client spell casting failed");
            Save();var spellContinuation=Game.Save(Game.Cast(_state,new("hero-1","test-mend",1,"hero-1"),_catalog).State);
            _state=Game.Create(8);Load();Cast(new("hero-1","test-mend",1,"hero-1"));
            if(Game.Save(_state)!=spellContinuation)throw new Exception("Client magic save continuation failed");
            _state=CreateExpedition(2,false);
            CastOrders([new("hero-1","test-ward",1,"hero-1"),new("hero-2","test-burn",1,_state.Enemy.Id)]);
            if(_state.Party[0].Hp!=35 || _state.Enemy.Hp!=31 || _state.Party[0].Effects?.Single().Rounds!=2)throw new Exception("Client multi-caster effects failed");
            Save();var effectContinuation=Game.Save(Game.Attack(_state,_catalog,_state.Melee is null?null:_player.Position.DistanceTo(_enemyPosition)).State);Load();Attack();
            if(Game.Save(_state)!=effectContinuation)throw new Exception("Client duration save continuation failed");
            if(_catalog is not null && Crafting.UsesFixture(_catalog))
            {
                _state=CreateExpedition(1,false);
                _state=Crafting.Craft(_state,new("hero-1","test-tonic"),_catalog);
                Save();Load();
                if(Inventory.Count(_state,80)!=1 || Inventory.Count(_state,78)!=2)throw new Exception("Client recipe/save failed");
                _state=Crafting.Craft(_state,new("hero-1","test-staff"),_catalog);
                if(Inventory.Count(_state,81)!=1 || Inventory.Count(_state,77)!=0)throw new Exception("Client crafting chain failed");
                _state=Equipment.Equip(_state,"hero-1",81,_catalog);Save();Load();
                if(Equipment.ItemId(_state,_state.Party[0].Id)!=81)throw new Exception("Crafted weapon equip/save failed");
                GD.Print("PASS: crafting chain, ingredient counts and crafted equipment save");
            }
            GD.Print("PASS: Godot C# scene, UI combat, save/load, solo escape, multi-caster magic and duration effects"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
