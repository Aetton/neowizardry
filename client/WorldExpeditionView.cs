using System.Collections.Immutable;
using Godot;
using NeoWizardry.Core;

public partial class WorldPreview
{
    private WorldExpeditionState? _expedition;
    private WorldCreature? _creature;
    private ItemCatalog _expeditionItems=null!;
    private MonsterCatalog _expeditionMonsters=null!;
    private Label _expeditionStatus=null!,_battleFeedback=null!;
    private PanelContainer _battlePanel=null!;
    private VBoxContainer _battleBody=null!;
    private MeshInstance3D _encounterMarker=null!;
    private Vector3 _encounterAnchor;
    private string _expeditionPath="";
    private int _battleHero;
    private bool _beginEncounter;
    private bool BattleOpen => _expedition?.Phase is WorldEncounterPhase.Active or WorldEncounterPhase.Defeat;

    private void InitializeExpedition(string[] args,CanvasLayer layer)
    {
        if(!args.Contains("--expedition") && !args.Contains("--world-expedition-smoke"))return;
        string? CatalogPath(string option,string name)
        {
            int at=Array.IndexOf(args,option);
            if(at>=0)
            {
                if(at+1>=args.Length || args[at+1].StartsWith("--"))throw new InvalidDataException($"Missing {option} path");
                return Path.GetFullPath(args[at+1]);
            }
            string user=ProjectSettings.GlobalizePath($"user://{name}.json");
            int worldAt=Array.IndexOf(args,"--world");
            string beside=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[worldAt+1]))!,$"{name}.json");
            string root=OS.HasFeature("editor")?Directory.GetParent(ProjectSettings.GlobalizePath("res://").TrimEnd('/','\\'))!.FullName:Path.GetDirectoryName(OS.GetExecutablePath())!;
            string local=Path.Combine(root,"local",$"{name}.json");
            return File.Exists(beside)?beside:File.Exists(user)?user:File.Exists(local)?local:null;
        }
        var itemPath=CatalogPath("--catalog","items");var monsterPath=CatalogPath("--monsters","monsters");
        _expeditionItems=itemPath is null?MeleeTraining.Catalog():ItemCatalog.Parse(File.ReadAllText(itemPath));
        _expeditionMonsters=monsterPath is null?MonsterCombat.FixtureCatalog():MonsterCatalog.Parse(File.ReadAllText(monsterPath));
        // Dusk Bat is a reviewed profile. Other catalogs must provide an explicitly supported row.
        var definition=_expeditionMonsters.Monsters.FirstOrDefault(m=>m.Id==282 && MonsterCombat.Supported(m))
            ??_expeditionMonsters.Monsters.FirstOrDefault(MonsterCombat.Supported)
            ??throw new InvalidDataException("No supported physical monster in the selected catalog");
        int size=GameMode?6:1,partyAt=Array.IndexOf(args,"--party-size");
        if(partyAt>=0 && (partyAt+1>=args.Length || !int.TryParse(args[partyAt+1],out size)))throw new InvalidDataException("Invalid --party-size");
        _expedition=WorldExpedition.Create(size,_expeditionItems,_expeditionMonsters,definition.Id,CaptureCheckpoint());
        WorldExpedition.Validate(_expedition,_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters);
        _expeditionPath=ProjectSettings.GlobalizePath($"user://{(GameMode?"campaign":"expeditions")}/{_contentHash}.json");
        _encounterAnchor=(_objects?.Arrivals.FirstOrDefault()?.Position??_center)+Vector3.Up*1.25f;
        _encounterMarker=new MeshInstance3D {Position=_encounterAnchor,Mesh=new SphereMesh {Radius=0.3f,Height=0.6f},
            MaterialOverride=new StandardMaterial3D {AlbedoColor=Colors.OrangeRed,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded}};
        AddChild(_encounterMarker);
        _encounterMarker.AddChild(new Label3D {Text=$"{definition.Label}\nB: encounter · model pending",Position=Vector3.Up*0.6f,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=24,PixelSize=0.003f});
        int worldAt=Array.IndexOf(args,"--world");
        string creaturePath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[worldAt+1]))!,$"creature-{definition.Id}.nwcreature.json");
        if(File.Exists(creaturePath))
        {
            _creature=WorldCreature.Load(creaturePath,definition,_expeditionMonsters.DatabaseSha256,state=>ConfigureTextures(state,args.Contains("--world-smoke")));
            _creature.Position=_encounterAnchor-Vector3.Up*1.25f;AddChild(_creature);
        }
        else if(args.Contains("--world-creature-required"))throw new InvalidDataException("Required original creature asset missing");
        _expeditionStatus=new Label {Position=new(16,224)};layer.AddChild(_expeditionStatus);
        _checkpointStatus.Text="EXPEDITION · F5 save party/world/battle · F9 load · resumes in flight";
        _battlePanel=new PanelContainer {Visible=false};
        _battlePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight);
        var screen=GetViewport().GetVisibleRect().Size;
        float width=Math.Min(460,screen.X-32);
        _battlePanel.Position=new(screen.X-width-16,16);_battlePanel.Size=new(width,screen.Y-32);layer.AddChild(_battlePanel);
        var scroll=new ScrollContainer();_battlePanel.AddChild(scroll);
        _battleBody=new VBoxContainer();scroll.AddChild(_battleBody);
        RefreshExpedition();
    }

    private void RefreshExpedition(string? message=null)
    {
        if(_expedition is null)return;
        var s=_expedition.Game;
        _expeditionStatus.Text=$"EXPEDITION · {_expedition.Phase} · B near encounter to fight · V focus creature · authored encounter/grid\n"+
            string.Join(" · ",s.Party.Select((h,i)=>$"P{i+1} {h.Hp}/{h.MaxHp} HP · {h.Xp} XP"))+"\n"+
            ( _expedition.Schema==1?"Legacy expedition · no native rewards":
                string.Join(" · ",s.Party.Select((h,i)=>$"P{i+1}: first level goal {ExpeditionRewards.FirstLevelGoal(h.Profession)} XP")))+"\n"+
            $"{s.Items!.Items.Length} owned instances · "+
            (_expedition.Rewards?.Granted==true?"Reward saved · native record has no items/gold":"Character advancement pending");
        _encounterMarker.Visible=_creature is null && _expedition.Phase==WorldEncounterPhase.Ready;
        if(_creature is not null)
        {
            _creature.Visible=_expedition.Phase!=WorldEncounterPhase.Victory || _creature.HasCycle(21);
            _creature.SetHealth(s.Enemy.Hp,s.Melee!.Field!.Monsters![s.Enemy.Id].MaxHp);
        }
        _battlePanel.Visible=BattleOpen;
        if(GameMode){_expeditionStatus.Visible=false;RefreshPlayerHud();}
        foreach(var child in _battleBody.GetChildren()){_battleBody.RemoveChild(child);child.QueueFree();}
        if(!BattleOpen)return;
        _looking=false;Input.MouseMode=Input.MouseModeEnum.Visible;
        _battleBody.AddChild(new Label {Text=$"{_expedition.Phase} · round {s.Round}\nF5 saves this battle · F9 loads expedition",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        if(_expedition.Phase==WorldEncounterPhase.Defeat)
        {
            _battleBody.AddChild(new Label {Text="Party defeated. Load an earlier expedition with F9.\nNo automatic restart or item/HP reset."});return;
        }
        var hero=new OptionButton();foreach(var h in s.Party)hero.AddItem($"{h.Name} · {h.Hp} HP");
        hero.Select(_battleHero);hero.ItemSelected+=i=>{_battleHero=(int)i;RefreshExpedition();};_battleBody.AddChild(hero);
        _battleBody.AddChild(new Label {Text="Tap adjacent free cell to move, adjacent enemy to attack.\nEach tap resolves one round; other heroes attack nearby or wait."});
        var grid=new GridContainer {Columns=s.Melee!.Field!.Width};_battleBody.AddChild(grid);
        var f=s.Melee.Field;
        for(int y=0;y<f.Height;y++)for(int x=0;x<f.Width;x++)
        {
            var cell=new BattleCell(x,y);var id=f.Positions.FirstOrDefault(p=>p.Value==cell && TacticalCombat.Alive(s,p.Key)).Key;
            int ordinal=Array.FindIndex(s.Party.ToArray(),h=>h.Id==id);bool enemy=id==s.Enemy.Id;
            var button=new Button {Text=f.Walls.Contains(cell)?"#":ordinal>=0?$"P{ordinal+1}":enemy?$"E\n{s.Enemy.Hp}":"·",
                CustomMinimumSize=new(42,40),Disabled=f.Walls.Contains(cell)};
            button.Pressed+=()=>ExpeditionAction(()=>
            {
                var h=_expedition!.Game.Party[_battleHero];
                var order=enemy?new TacticalOrder(h.Id,TacticalAction.Attack,s.Enemy.Id):
                    id==h.Id?new TacticalOrder(h.Id,TacticalAction.Wait):new TacticalOrder(h.Id,TacticalAction.Move,Destination:cell);
                ResolveExpedition([order]);
            });grid.AddChild(button);
        }
        var wait=new Button {Text="Resolve round · attack nearby / wait"};wait.Pressed+=()=>ExpeditionAction(()=>ResolveExpedition([]));_battleBody.AddChild(wait);
        _battleFeedback=new Label {Text=message??"",AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(420,0)};_battleBody.AddChild(_battleFeedback);
    }

    private void ExpeditionAction(Action action)
    {
        try {action();}
        catch(Exception error) when(error is InvalidOperationException or InvalidDataException)
        {if(_battleFeedback is not null)_battleFeedback.Text=error.Message;else _expeditionStatus.Text=error.Message;}
    }

    private void ResolveExpedition(IReadOnlyList<TacticalOrder> orders)
    {
        var before=_expedition!;
        var result=WorldExpedition.Advance(before,_expeditionItems,orders);_expedition=result.State;
        if(_creature is not null)
        {
            string enemy=before.Game.Enemy.Id;
            var cues=result.Events.Where(e=>e.StartsWith(enemy+" → ")).Select(_=>6).ToList();
            if(before.Game.Melee!.Field!.Positions[enemy]!=_expedition.Game.Melee!.Field!.Positions[enemy])cues.Insert(0,4);
            if(_expedition.Game.Enemy.Hp<before.Game.Enemy.Hp)cues.Add(20);
            _creature.Cues(cues);
        }
        if(_expedition.Phase==WorldEncounterPhase.Victory)
        {
            _creature?.PlayDeath();
            RestoreCheckpoint(_expedition.World);
            _walking=_enterWalk=false;
            if(GameMode)ResumePlayerWalking();
            _checkpointStatus.Text="Encounter cleared · exploration resumed in flight · F5 saves victory";
        }
        RefreshExpedition(string.Join("\n",result.Events.TakeLast(6)));
        if(GameMode)SavePlayerGame();
    }

    private void TryBeginExpeditionEncounter()
    {
        _beginEncounter=false;
        if(_expedition is null || _expedition.Phase!=WorldEncounterPhase.Ready)return;
        if(_doors?.IsInspecting==true){_checkpointStatus.Text="Finish animation inspection before entering combat";return;}
        if(_view.GlobalPosition.DistanceTo(_encounterAnchor)>3){_checkpointStatus.Text="Approach the orange encounter marker (N jumps to native arrival)";return;}
        using var ray=PhysicsRayQueryParameters3D.Create(_view.GlobalPosition,_encounterAnchor,1);
        ray.HitBackFaces=true;
        if(GetWorld3D().DirectSpaceState.IntersectRay(ray).Count>0){_checkpointStatus.Text="Encounter marker is occluded";return;}
        _expedition=WorldExpedition.Begin(_expedition with {World=CaptureCheckpoint()});
        _walking=_enterWalk=_useDoor=_inspectDoor=false;RefreshExpedition();
        _creature?.Focus(_view);
    }

    private void SaveExpedition()
    {
        var candidate=_expedition! with {World=BattleOpen?_expedition!.World:CaptureCheckpoint()};
        var json=WorldExpedition.Save(candidate,_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters);
        ExplorationCheckpointFile.Write(_expeditionPath,json,WorldExpedition.MaximumBytes);
        _expedition=candidate;
    }

    private void LoadExpedition()
    {
        var candidate=WorldExpedition.Load(ExplorationCheckpointFile.Read(_expeditionPath,WorldExpedition.MaximumBytes),
            _contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters);
        if(_creature is not null && candidate.MonsterDefinitionId!=_creature.DefinitionId)
            throw new InvalidDataException("Saved encounter does not match the loaded creature renderer");
        // World validation (including speed) completes before the shared game state is replaced.
        RestoreCheckpoint(candidate.World);_expedition=candidate;_battleHero=0;_beginEncounter=false;RefreshExpedition();
        if(candidate.Phase==WorldEncounterPhase.Victory)_creature?.PlayDeath(completed:true);
        else _creature?.ResetIdle();
        if(BattleOpen)_creature?.Focus(_view);
    }

    private void CheckWorldExpedition()
    {
        if(_expedition is null)throw new InvalidDataException("Expedition mode was not initialized");
        string path=_expeditionPath;
        _expeditionPath=Path.Combine(Path.GetTempPath(),$"neo-world-expedition-{Guid.NewGuid():N}.json");
        try
        {
            var original=_expedition;
            if(_creature is not null)
            {
                string game=Game.Save(_expedition.Game);_creature.Check();
                if(Game.Save(_expedition.Game)!=game)throw new InvalidDataException("Cosmetic animation changed gameplay state");
            }
            _view.GlobalPosition=_encounterAnchor+Vector3.One*_span*5;
            TryBeginExpeditionEncounter();
            if(_expedition.Phase!=WorldEncounterPhase.Ready)throw new InvalidDataException("Distant encounter started");
            _view.GlobalPosition=_encounterAnchor+Vector3.Up*0.1f;
            var entryPosition=_view.GlobalPosition;
            TryBeginExpeditionEncounter();
            if(_expedition.Phase!=WorldEncounterPhase.Active || !_battlePanel.Visible)
                throw new InvalidDataException("Arrival encounter did not open battle UI");
            ResolveExpedition([]);SaveExpedition();
            if(Vector(_expedition.World.Position)!=entryPosition)throw new InvalidDataException("Cosmetic battle camera overwrote exploration position");
            var expected=Game.Save(WorldExpedition.Advance(_expedition,_expeditionItems).State.Game);
            ResolveExpedition([]);LoadExpedition();
            if(Game.Save(WorldExpedition.Advance(_expedition,_expeditionItems).State.Game)!=expected || !_battlePanel.Visible || _walking)
                throw new InvalidDataException("Mid-battle expedition continuation failed");
            var before=WorldExpedition.Save(_expedition,_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters);
            File.WriteAllText(_expeditionPath,"{broken");bool rejected=false;
            try {LoadExpedition();}catch(System.Text.Json.JsonException){rejected=true;}
            if(!rejected || WorldExpedition.Save(_expedition,_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters)!=before || File.ReadAllText(_expeditionPath)!="{broken")
                throw new InvalidDataException("Failed expedition load mutated state or source");
            for(int round=0;round<160 && _expedition.Phase==WorldEncounterPhase.Active;round++)ResolveExpedition([]);
            if(_expedition.Phase is not (WorldEncounterPhase.Victory or WorldEncounterPhase.Defeat))throw new InvalidDataException("World encounter did not terminate");
            var outcome=_expedition.Phase;SaveExpedition();
            string terminal=WorldExpedition.Save(_expedition,_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters);
            if(outcome==WorldEncounterPhase.Victory)
            {
                var record=_expedition.Game.Melee!.Field!.Monsters![_expedition.Game.Enemy.Id].Record;
                int count=_expedition.Game.Party.Count(ExpeditionRewards.Eligible),share=ExpeditionRewards.Experience(record)/count;
                if(_expedition.Rewards is not {Granted:true} receipt || _expedition.Game.Party.Any(h=>
                    h.Xp!=receipt.InitialExperience[h.Id]+(ExpeditionRewards.Eligible(h)?share:0)))
                    throw new InvalidDataException("Native expedition reward missing or incorrectly divided");
                GD.Print($"PASS: native reward {ExpeditionRewards.Experience(record)} XP, {share} each, {count} eligible; no items/gold in record");
            }
            if(outcome==WorldEncounterPhase.Victory && _view.GlobalPosition!=entryPosition)
                throw new InvalidDataException("Victory did not restore exploration camera");
            _expedition=original;LoadExpedition();TryBeginExpeditionEncounter();
            if(_expedition.Phase!=outcome || _encounterMarker.Visible || _battlePanel.Visible!=(outcome==WorldEncounterPhase.Defeat))
                throw new InvalidDataException("Completed world encounter respawned on reload");
            if(WorldExpedition.Save(_expedition,_contentHash,CheckpointLimits,_expeditionItems,_expeditionMonsters)!=terminal)
                throw new InvalidDataException("Terminal reload changed reward receipt or expedition state");
            GD.Print($"PASS: expedition {_expedition.Game.Party.Length} heroes; world/battle save continuation, failed-load preservation, {outcome}, no respawn");
        }
        finally {File.Delete(_expeditionPath);_expeditionPath=path;}
    }
}
