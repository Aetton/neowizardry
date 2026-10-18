using Godot;
using NeoWizardry.Core;

public partial class WorldPreview
{
    private bool _gameReady,_gamePaused,_gameSaveBlocked,_bestiaryOpen,_inventoryOpen;
    private CanvasLayer _playerLayer=null!;
    private CreatureGallery? _gameGallery;
    private Label? _gameHud,_gameNotice;
    private PanelContainer _gameMenu=null!;
    private double _autoSaveTime;
    private ExplorationCheckpoint? _initialGameWorld;
    private void InitializePlayerGame(CanvasLayer layer)
    {
        _playerLayer=layer;
        var top=new HBoxContainer {Position=new(18,16)};layer.AddChild(top);
        top.AddChild(new Label {Text="МОНАСТЫРЬ · ПЕРВЫЙ ЭТАЖ",MouseFilter=Control.MouseFilterEnum.Ignore});
        void Button(string text,Action action){var b=new Button {Text=text};b.Pressed+=action;top.AddChild(b);}
        Button("Инвентарь · I",ShowPlayerInventory);Button("Сохранить · F5",SavePlayerGame);Button("Меню · Esc",TogglePlayerMenu);
        _gameHud=new Label {Position=new(18,GetViewport().GetVisibleRect().Size.Y-88),MouseFilter=Control.MouseFilterEnum.Ignore,AutowrapMode=TextServer.AutowrapMode.WordSmart};layer.AddChild(_gameHud);
        _combatHint=new Label {MouseFilter=Control.MouseFilterEnum.Ignore};layer.AddChild(_combatHint);
        _itemHint=new Label {MouseFilter=Control.MouseFilterEnum.Ignore};layer.AddChild(_itemHint);
        _gameNotice=new Label {Position=new(18,60),Text="Загрузка монастыря…",MouseFilter=Control.MouseFilterEnum.Ignore,AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(850,0)};layer.AddChild(_gameNotice);
        _gameMenu=new PanelContainer {Visible=false,Position=new(32,110),CustomMinimumSize=new(440,0)};layer.AddChild(_gameMenu);
        var menu=new VBoxContainer();_gameMenu.AddChild(menu);
        menu.AddChild(new Label {Text="МОНАСТЫРЬ\nWASD — ходить · мышь — смотреть\nE / щелчок — взаимодействовать · B — атаковать\nF5 — сохранить · F9 — загрузить\nI — инвентарь · Esc — меню"});
        void MenuButton(string text,Action action){var b=new Button {Text=text};b.Pressed+=action;menu.AddChild(b);}
        _menuAttack=new Button {Text="Атаковать ближайшую группу · B"};menu.AddChild(_menuAttack);
        _menuAttack.Pressed+=()=>{_gamePaused=false;_gameMenu.Hide();Input.MouseMode=Input.MouseModeEnum.Captured;RequestPlayerAttack();};
        MenuButton("Продолжить",TogglePlayerMenu);MenuButton("Бестиарий · модели и анимации",ShowPlayerCreatures);MenuButton("Сохранить",SavePlayerGame);
        MenuButton("Загрузить",()=>{try{LoadExpedition();_gameSaveBlocked=false;_gamePaused=false;_gameMenu.Hide();if(!BattleOpen)ResumePlayerWalking();}catch(Exception e){Notice("Сохранение не загружено: "+e.Message);}});
        MenuButton("Новая группа · 6 героев",NewPlayerGame);MenuButton("Выйти",()=>{SavePlayerGame();GetTree().Quit();});
        RefreshPlayerHud();
    }
    private void Notice(string message){if(_gameNotice is not null)_gameNotice.Text=message;}
    private void RefreshPlayerHud()
    {
        if(_gameHud is null || _expedition is null)return;
        _gameHud.Position=new(18,GetViewport().GetVisibleRect().Size.Y-88);_gameHud.Size=new(Math.Max(300,GetViewport().GetVisibleRect().Size.X-36),80);
        _gameHud.Text=string.Join("    ",_expedition.Game.Party.Select((h,i)=>$"{i+1}. {h.Name}  {h.Hp}/{h.MaxHp} HP"))+"\n"+
            (_expedition.Phase==WorldEncounterPhase.Victory?"Встреча завершена · монастырь открыт для исследования":"WASD — ходить · E — взаимодействовать · I — инвентарь · Esc — меню");
    }
    private async void StartPlayerGame()
    {
        try
        {
            if(_collision is null || _walker is null || _objects is null || _expedition is null || _creature is null)
                throw new InvalidDataException("Отсутствуют данные пола, дверей или исходная модель существа. Повторите подготовку оригинала.");
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            // Native new game: level 8, entrance 0 -> Levels.cpp resolves MO100.
            // Arrival list order is not an entry policy (another arrival is in the corridor).
            var arrival=_objects.Arrivals.SingleOrDefault(a=>a.Name.Equals("MO100",StringComparison.OrdinalIgnoreCase))
                ??throw new InvalidDataException("Не найдена исходная стартовая точка MO100 на пляже");
            Vector3 floor=PlayerFloor(arrival.Position);
            _walker.GlobalPosition=floor+Vector3.Up*(WorldWalker.Height/2+0.05f);_walker.Velocity=Vector3.Zero;
            _view.GlobalPosition=_walker.GlobalPosition+Vector3.Up*WorldWalker.EyeOffset;
            var facing=arrival.Forward??Vector3.Forward;
            if(new Vector2(facing.X,facing.Z).LengthSquared()<0.000001f)throw new InvalidDataException("Стартовое направление не горизонтально");
            _view.LookAt(_view.GlobalPosition+new Vector3(facing.X,0,facing.Z).Normalized());
            // Place the existing authored encounter on a nearby clear floor, never inside the party.
            if(_nativeEncounter is null)foreach(var offset in new[]{new Vector3(0,0,-4),new Vector3(4,0,0),new Vector3(-4,0,0),new Vector3(0,0,4)})
            {
                try
                {
                    var point=PlayerFloor(floor+offset,2);
                    if(point.DistanceTo(floor)<2.5f || _collision.Ray(_view.GlobalPosition,point+Vector3.Up*1.25f).Count>0)continue;
                    _encounterAnchor=point+Vector3.Up*1.25f;_creature.Position=point;_encounterMarker.Position=_encounterAnchor;break;
                }
                catch(InvalidDataException){}
            }
            _initialGameWorld=CaptureCheckpoint();_expedition=_expedition with {World=_initialGameWorld};
            if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")!="1")
            {
                try{if(!File.Exists(_expeditionPath))MigratePreviousPlayerItems();if(File.Exists(_expeditionPath))LoadExpedition();}
                catch(Exception error){_gameSaveBlocked=true;Notice("Сохранение не загружено и не будет перезаписано: "+error.Message+". Выберите «Новая группа» в меню для нового прохождения.");}
            }
            _gameReady=true;if(!BattleOpen)ResumePlayerWalking();
            if(!_gameSaveBlocked)Notice(_expedition.Phase==WorldEncounterPhase.Victory?"Пляж очищен · E — подобрать / открыть дверь · F5 — сохранить":_nativeEncounter?.Disposition==0?"Существа на пляже нейтральны · B — атаковать (до 8 м) · F5 — сохранить · Esc — меню":"Первый этаж монастыря · E — дверь / встреча · F5 — сохранить · Esc — меню");
            if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")=="1")await CheckPlayerStartup();
        }
        catch(Exception error)
        {
            if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")=="1"){GD.PushError(error.ToString());GetTree().Quit(1);return;}
            LoadFailed?.Invoke(error.Message);QueueFree();
        }
    }
    private Vector3 PlayerFloor(Vector3 point,float radius=12)
    {
        if(_collision is null || _walker is null)throw new InvalidDataException("Нет коллизии монастыря");
        foreach(var offset in new[]{Vector3.Zero,new Vector3(0.6f,0,0),new Vector3(-0.6f,0,0),new Vector3(0,0,0.6f),new Vector3(0,0,-0.6f)})
        {
            var hit=_collision.Ray(point+offset+Vector3.Up*4,point+offset-Vector3.Up*8);
            if(_collision.IsWalkableHit(hit))
            {
                var floor=hit["position"].AsVector3();
                if(floor.DistanceTo(point)<=radius && _walker.CanStand(floor+Vector3.Up*(WorldWalker.Height/2+0.05f)))return floor;
            }
        }
        foreach(var floor in _collision.Faces.Where(f=>f.Walkable && f.Normal.Y>0.5f && f.Center.DistanceTo(point)<=radius).OrderBy(f=>f.Center.DistanceSquaredTo(point)))
            if(_walker.CanStand(floor.Center+Vector3.Up*(WorldWalker.Height/2+0.05f)))return floor.Center;
        throw new InvalidDataException("Нет безопасного пола рядом с исходной точкой входа");
    }
    private void ResumePlayerWalking()
    {
        if(_walker is null)return;
        var floor=PlayerFloor(_view.GlobalPosition-Vector3.Up*1.3f,3);
        _walker.GlobalPosition=floor+Vector3.Up*(WorldWalker.Height/2+0.05f);_walker.Velocity=Vector3.Zero;
        _view.GlobalPosition=_walker.GlobalPosition+Vector3.Up*WorldWalker.EyeOffset;
        _walking=true;_enterWalk=false;_looking=true;
        if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")!="1")Input.MouseMode=Input.MouseModeEnum.Captured;
    }
    private void TogglePlayerMenu()
    {
        _gamePaused=!_gamePaused;_gameMenu.Visible=_gamePaused;
        Input.MouseMode=_gamePaused || BattleOpen?Input.MouseModeEnum.Visible:Input.MouseModeEnum.Captured;
    }
    private void NewPlayerGame()
    {
        if(_expedition is null || _initialGameWorld is null)return;
        if(File.Exists(_expeditionPath))File.Move(_expeditionPath,_expeditionPath+".backup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        RestoreCheckpoint(_initialGameWorld);
        _expedition=_nativeEncounter is null?WorldExpedition.Create(6,_expeditionItems,_expeditionMonsters,_expedition.MonsterDefinitionId,_initialGameWorld):
            WorldExpedition.CreateNative(6,_expeditionItems,_expeditionMonsters,_nativeEncounter,_initialGameWorld);
        _expedition=WorldItemPickup.Attach(_expedition,_nativeItems,_expeditionItems);
        _gameSaveBlocked=false;_battleHero=0;foreach(var visual in _encounterCreatures.Values)visual.ResetIdle();_gamePaused=false;_gameMenu.Hide();RefreshExpedition();ResumePlayerWalking();SavePlayerGame();
    }
    private void SavePlayerGame()
    {
        if(!_gameReady || _expedition is null)return;
        if(_gameSaveBlocked){Notice("Автосохранение отключено: прежнее сохранение защищено. Начните новую группу через меню.");return;}
        try{SaveExpedition();Notice("Сохранено · F9 — загрузить");}
        catch(Exception error){Notice("Не удалось сохранить: "+error.Message);}
    }
    private void PlayerInput(InputEvent input)
    {
        if(!_gameReady || _bestiaryOpen || _inventoryOpen)return;
        if(input is InputEventKey key && key.Pressed && !key.Echo)
        {
            Key code=key.PhysicalKeycode!=Key.None?key.PhysicalKeycode:key.Keycode;
            if(code==Key.Escape){TogglePlayerMenu();return;}
            if(code==Key.F5){SavePlayerGame();return;}
            if(code==Key.F9){try{LoadExpedition();if(!BattleOpen)ResumePlayerWalking();}catch(Exception e){Notice("Не удалось загрузить: "+e.Message);}return;}
            if(code==Key.I){ShowPlayerInventory();return;}
            if(!_gamePaused && !BattleOpen && code==Key.B){RequestPlayerAttack();return;}
            if(!_gamePaused && !BattleOpen && code==Key.E)PlayerInteract();
        }
        if(_gamePaused || BattleOpen)return;
        if(input is InputEventMouseMotion motion && Input.MouseMode==Input.MouseModeEnum.Captured)
            _view.Rotation=new(Math.Clamp(_view.Rotation.X-motion.Relative.Y*0.003f,-1.45f,1.45f),_view.Rotation.Y-motion.Relative.X*0.003f,0);
        if(input is InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left})PlayerInteract();
    }
    private void PlayerInteract()
    {
        if(_expedition is null || BattleOpen || _expedition.Phase==WorldEncounterPhase.Defeat)return;
        if(TryCollectWorldItem())return;
        if(_expedition.Phase==WorldEncounterPhase.Ready && LookingAtPlayerEncounter())
        {RequestPlayerAttack();return;}
        Notice(_doors?.Use(_view)??"Здесь нет доступной двери");
    }
    private void ShowPlayerCreatures()
    {
        int at=Array.IndexOf(Args,"--world");string index=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(Args[at+1]))!,"creatures-index.json");
        if(!File.Exists(index)){Notice("Бестиарий отсутствует. Повторите подготовку данных оригинала.");return;}
        _gamePaused=true;_bestiaryOpen=true;Input.MouseMode=Input.MouseModeEnum.Visible;Visible=false;_playerLayer.Hide();
        CreatureGallery? gallery=null;
        gallery=new CreatureGallery {IndexPath=index,ReturnToGame=()=>
        {
            gallery?.QueueFree();_gameGallery=null;_bestiaryOpen=false;Visible=true;_playerLayer.Show();_view.Current=true;_gamePaused=true;_gameMenu.Show();Input.MouseMode=Input.MouseModeEnum.Visible;
        }};
        _gameGallery=gallery;GetParent().AddChild(gallery);
    }
    private void ShowPlayerInventory()
    {
        if(_expedition is null || !_gameReady || _inventoryOpen)return;
        _inventoryOpen=true;_gamePaused=true;Input.MouseMode=Input.MouseModeEnum.Visible;
        var dialog=new AcceptDialog {Title="Группа и инвентарь",Size=new(760,520)};AddChild(dialog);
        var body=new VBoxContainer();dialog.AddChild(body);
        var hero=new OptionButton();foreach(var h in _expedition.Game.Party)hero.AddItem($"{h.Name} · {h.Hp}/{h.MaxHp} HP · {h.Xp} XP");body.AddChild(hero);
        var list=new ItemList {CustomMinimumSize=new(700,300)};body.AddChild(list);
        var bag=Inventory.Bag(_expedition.Game).ToArray();foreach(var item in bag)list.AddItem($"{(item.Identified?_expeditionItems.Find(item.DefinitionId).Name:"Неопознанный предмет")} ×{item.Quantity}");
        var status=new Label();body.AddChild(status);
        var equip=new Button {Text="Экипировать выбранное",Disabled=BattleOpen};body.AddChild(equip);
        equip.Pressed+=()=>{try{if(list.GetSelectedItems().FirstOrDefault(-1) is int at && at>=0)
            {_expedition=_expedition with {Game=Equipment.EquipInstance(_expedition.Game,_expedition.Game.Party[hero.Selected].Id,bag[at].Id,_expeditionItems)};status.Text="Экипировано";RefreshPlayerHud();SavePlayerGame();}}
            catch(Exception e){status.Text=e.Message;}};
        void CloseInventory(){if(!_inventoryOpen)return;_inventoryOpen=false;dialog.QueueFree();_gamePaused=_gameMenu.Visible;Input.MouseMode=_gamePaused || BattleOpen?Input.MouseModeEnum.Visible:Input.MouseModeEnum.Captured;}
        dialog.CloseRequested+=CloseInventory;dialog.Confirmed+=CloseInventory;dialog.Canceled+=CloseInventory;dialog.PopupCentered();
    }
    private async System.Threading.Tasks.Task CheckPlayerStartup()
    {
        if(!_gameReady || !_walking || _expedition is null || _creature is null || _objects?.Visible==true || _expeditionStatus.Visible)
            throw new InvalidDataException("Ordinary startup did not enter the walking monastery with original creature and player HUD");
        var arrival=_objects!.Arrivals.Single(a=>a.Name.Equals("MO100",StringComparison.OrdinalIgnoreCase));
        if(new Vector2(_walker!.GlobalPosition.X-arrival.Position.X,_walker.GlobalPosition.Z-arrival.Position.Z).Length()>1)
            throw new InvalidDataException("New game drifted away from native beach entrance MO100");
        var facing=arrival.Forward??Vector3.Forward;
        var expectedForward=new Vector3(facing.X,0,facing.Z).Normalized();
        if((-_view.GlobalBasis.Z).Dot(expectedForward)<0.999f)throw new InvalidDataException("New game facing differs from native entrance");
        GD.Print("PASS: new game uses native beach entrance MO100 and its facing; save resume remains independent");
        for(int i=0;i<20;i++){_walker!.Step(1.0/60,Vector3.Zero);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        if(!_walker!.IsOnFloor())throw new InvalidDataException("Player did not settle at monastery arrival");
        string path=_expeditionPath;_expeditionPath=Path.Combine(Path.GetTempPath(),"neo-player-start-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            CheckNativeWorldItems();
            SaveExpedition();var expected=_expedition!.Game;var savedPosition=_view.GlobalPosition;var savedRotation=_view.Rotation;
            _view.GlobalPosition+=Vector3.Right*2;_view.Rotation+=new Vector3(0,0.5f,0);LoadExpedition();
            if(_view.GlobalPosition.DistanceTo(savedPosition)>0.001f || _view.Rotation.DistanceTo(savedRotation)>0.001f)
                throw new InvalidDataException("Saved exploration position/facing did not resume independently of new-game entrance");
            ResumePlayerWalking();
            if(_expedition.Game.Round!=expected.Round || !_walking)throw new InvalidDataException("Player save did not resume walking");
        }
        finally{File.Delete(_expeditionPath);_expeditionPath=path;}
        string beforeGallery=Game.Save(_expedition.Game);var position=_view.GlobalPosition;
        ShowPlayerCreatures();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(!_bestiaryOpen || _gameGallery is null)throw new InvalidDataException("Player bestiary did not open");
        _gameGallery.ReturnToGame?.Invoke();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(_bestiaryOpen || !Visible || _view.GlobalPosition!=position || Game.Save(_expedition.Game)!=beforeGallery)
            throw new InvalidDataException("Returning from player bestiary changed the campaign");
        GD.Print($"PASS: ordinary startup enters walking monastery; {_expedition.Game.Party.Length} heroes, original creature, doors, inventory HUD, save/resume, bestiary return; no --world argument");if(System.Environment.GetEnvironmentVariable("NEO_NATIVE_ENCOUNTER_SMOKE")=="1")
        {
            if(_nativeEncounter is null || _encounterCreatures.Count!=_nativeEncounter.Members.Length)throw new InvalidDataException("Placed encounter visuals missing");
            for(int i=0;i<_nativeEncounter.Members.Length;i++)
                if(_encounterCreatures[$"monster-{i+1}"].Position.DistanceTo(NativePosition(_nativeEncounter.Members[i].Position))>0.001f)
                    throw new InvalidDataException("Native encounter was relocated from source placement");
            _gameSaveBlocked=false;_gamePaused=true;
            await CheckPlayerAttackInput();
            CheckWorldExpedition();
            if(_expedition!.Phase!=WorldEncounterPhase.Victory)throw new InvalidDataException("Native encounter fixture did not reach victory");
            CheckPreviousItemMigration();
            GD.Print($"PASS: native STS group {_nativeEncounter.GroupId}, {_nativeEncounter.Members.Length} original placements/HP/visuals, battle/save/reload, once-only XP and no respawn");
        }
        GetTree().Quit();
    }
}
