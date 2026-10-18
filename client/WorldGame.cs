using Godot;
using NeoWizardry.Core;

public partial class WorldPreview
{
    private bool _gameReady,_gamePaused,_gameSaveBlocked;
    private Label? _gameHud,_gameNotice;
    private PanelContainer _gameMenu=null!;
    private double _autoSaveTime;
    private ExplorationCheckpoint? _initialGameWorld;
    private void InitializePlayerGame(CanvasLayer layer)
    {
        var top=new HBoxContainer {Position=new(18,16)};layer.AddChild(top);
        top.AddChild(new Label {Text="МОНАСТЫРЬ · ПЕРВЫЙ ЭТАЖ",MouseFilter=Control.MouseFilterEnum.Ignore});
        void Button(string text,Action action){var b=new Button {Text=text};b.Pressed+=action;top.AddChild(b);}
        Button("Инвентарь · I",ShowPlayerInventory);Button("Сохранить · F5",SavePlayerGame);Button("Меню · Esc",TogglePlayerMenu);
        _gameHud=new Label {Position=new(18,GetViewport().GetVisibleRect().Size.Y-88),MouseFilter=Control.MouseFilterEnum.Ignore,AutowrapMode=TextServer.AutowrapMode.WordSmart};layer.AddChild(_gameHud);
        _gameNotice=new Label {Position=new(18,60),Text="Загрузка монастыря…",MouseFilter=Control.MouseFilterEnum.Ignore,AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(850,0)};layer.AddChild(_gameNotice);
        _gameMenu=new PanelContainer {Visible=false,Position=new(32,110),CustomMinimumSize=new(440,0)};layer.AddChild(_gameMenu);
        var menu=new VBoxContainer();_gameMenu.AddChild(menu);
        menu.AddChild(new Label {Text="МОНАСТЫРЬ\nWASD — ходить · мышь — смотреть\nE / щелчок — дверь или встреча\nF5 — сохранить · F9 — загрузить\nI — инвентарь · Esc — меню"});
        void MenuButton(string text,Action action){var b=new Button {Text=text};b.Pressed+=action;menu.AddChild(b);}
        MenuButton("Продолжить",TogglePlayerMenu);MenuButton("Сохранить",SavePlayerGame);
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
            var arrival=_objects.Arrivals.FirstOrDefault(a=>a.Name.Contains("beach",StringComparison.OrdinalIgnoreCase) || a.Name.Contains("start",StringComparison.OrdinalIgnoreCase))??_objects.Arrivals.FirstOrDefault()
                ??throw new InvalidDataException("В монастыре не найдена исходная точка входа");
            Vector3 floor=PlayerFloor(arrival.Position);
            _walker.GlobalPosition=floor+Vector3.Up*(WorldWalker.Height/2+0.05f);_walker.Velocity=Vector3.Zero;
            _view.GlobalPosition=_walker.GlobalPosition+Vector3.Up*WorldWalker.EyeOffset;
            var target=_collision.Faces.Where(f=>f.Walkable).OrderBy(f=>f.Center.DistanceSquaredTo(floor+Vector3.Forward*3)).First().Center;
            if(new Vector2(target.X-floor.X,target.Z-floor.Z).Length()>0.2f)_view.LookAt(new(target.X,_view.GlobalPosition.Y,target.Z));
            // Place the existing authored encounter on a nearby clear floor, never inside the party.
            foreach(var offset in new[]{new Vector3(0,0,-4),new Vector3(4,0,0),new Vector3(-4,0,0),new Vector3(0,0,4)})
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
            if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")!="1" && File.Exists(_expeditionPath))
            {
                try{LoadExpedition();}
                catch(Exception error){_gameSaveBlocked=true;Notice("Сохранение не загружено и не будет перезаписано: "+error.Message+". Выберите «Новая группа» в меню для нового прохождения.");}
            }
            _gameReady=true;if(!BattleOpen)ResumePlayerWalking();
            if(!_gameSaveBlocked)Notice("Первый этаж монастыря · E — дверь / встреча · F5 — сохранить · Esc — меню");
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
        _expedition=WorldExpedition.Create(6,_expeditionItems,_expeditionMonsters,_expedition.MonsterDefinitionId,_initialGameWorld);
        _gameSaveBlocked=false;_battleHero=0;_creature?.ResetIdle();_gamePaused=false;_gameMenu.Hide();RefreshExpedition();ResumePlayerWalking();SavePlayerGame();
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
        if(!_gameReady)return;
        if(input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if(key.Keycode==Key.Escape){TogglePlayerMenu();return;}
            if(key.Keycode==Key.F5){SavePlayerGame();return;}
            if(key.Keycode==Key.F9){try{LoadExpedition();if(!BattleOpen)ResumePlayerWalking();}catch(Exception e){Notice("Не удалось загрузить: "+e.Message);}return;}
            if(key.Keycode==Key.I){ShowPlayerInventory();return;}
            if(!_gamePaused && !BattleOpen && key.Keycode is Key.E or Key.B)PlayerInteract();
        }
        if(_gamePaused || BattleOpen)return;
        if(input is InputEventMouseMotion motion && Input.MouseMode==Input.MouseModeEnum.Captured)
            _view.Rotation=new(Math.Clamp(_view.Rotation.X-motion.Relative.Y*0.003f,-1.45f,1.45f),_view.Rotation.Y-motion.Relative.X*0.003f,0);
        if(input is InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left})PlayerInteract();
    }
    private void PlayerInteract()
    {
        if(_expedition?.Phase==WorldEncounterPhase.Ready && _view.GlobalPosition.DistanceTo(_encounterAnchor)<3)
        {_beginEncounter=true;return;}
        Notice(_doors?.Use(_view)??"Здесь нет доступной двери");
    }
    private void ShowPlayerInventory()
    {
        if(_expedition is null || !_gameReady)return;
        _gamePaused=true;Input.MouseMode=Input.MouseModeEnum.Visible;
        var dialog=new AcceptDialog {Title="Группа и инвентарь",Size=new(760,520)};AddChild(dialog);
        var body=new VBoxContainer();dialog.AddChild(body);
        var hero=new OptionButton();foreach(var h in _expedition.Game.Party)hero.AddItem($"{h.Name} · {h.Hp}/{h.MaxHp} HP · {h.Xp} XP");body.AddChild(hero);
        var list=new ItemList {CustomMinimumSize=new(700,300)};body.AddChild(list);
        var bag=Inventory.Bag(_expedition.Game).ToArray();foreach(var item in bag)list.AddItem($"{_expeditionItems.Find(item.DefinitionId).Name} ×{item.Quantity}");
        var status=new Label();body.AddChild(status);
        var equip=new Button {Text="Экипировать выбранное",Disabled=BattleOpen};body.AddChild(equip);
        equip.Pressed+=()=>{try{if(list.GetSelectedItems().FirstOrDefault(-1) is int at && at>=0)
            {_expedition=_expedition with {Game=Equipment.EquipInstance(_expedition.Game,_expedition.Game.Party[hero.Selected].Id,bag[at].Id,_expeditionItems)};status.Text="Экипировано";RefreshPlayerHud();SavePlayerGame();}}
            catch(Exception e){status.Text=e.Message;}};
        void CloseInventory(){dialog.QueueFree();_gamePaused=_gameMenu.Visible;Input.MouseMode=_gamePaused || BattleOpen?Input.MouseModeEnum.Visible:Input.MouseModeEnum.Captured;}
        dialog.CloseRequested+=CloseInventory;dialog.Confirmed+=CloseInventory;dialog.PopupCentered();
    }
    private async System.Threading.Tasks.Task CheckPlayerStartup()
    {
        if(!_gameReady || !_walking || _expedition is null || _creature is null || _objects?.Visible==true || _expeditionStatus.Visible)
            throw new InvalidDataException("Ordinary startup did not enter the walking monastery with original creature and player HUD");
        for(int i=0;i<20;i++){_walker!.Step(1.0/60,Vector3.Zero);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
        if(!_walker!.IsOnFloor())throw new InvalidDataException("Player did not settle at monastery arrival");
        string path=_expeditionPath;_expeditionPath=Path.Combine(Path.GetTempPath(),"neo-player-start-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            SaveExpedition();var expected=_expedition!.Game;LoadExpedition();ResumePlayerWalking();
            if(_expedition.Game.Round!=expected.Round || !_walking)throw new InvalidDataException("Player save did not resume walking");
        }
        finally{File.Delete(_expeditionPath);_expeditionPath=path;}
        GD.Print($"PASS: ordinary startup enters walking monastery; {_expedition.Game.Party.Length} heroes, original creature, doors, inventory HUD, save/resume; no --world argument");GetTree().Quit();
    }
}
