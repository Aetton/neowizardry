using Godot;
using System.Diagnostics;
using System.Text.Json;

// The ordinary player entry point. Missing private content opens setup, never training.
public partial class MonasteryStart : Control
{
    private Label _status=null!;
    private Button _select=null!;
    private string _profile="",_cache="";
    private bool _busy;
    private Process? _importProcess;
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _profile=ProjectSettings.GlobalizePath("user://monastery-install.json");
        _cache=System.Environment.GetEnvironmentVariable("NEO_CONTENT_CACHE")??Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),"NeoWizardry","content");
        var background=new ColorRect {Color=new Color(0.025f,0.035f,0.045f)};background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(background);
        var center=new CenterContainer();center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);AddChild(center);
        var panel=new VBoxContainer {CustomMinimumSize=new(620,0)};panel.AddThemeConstantOverride("separation",20);center.AddChild(panel);
        panel.AddChild(new Label {Text="NEOWIZARDRY · МОНАСТЫРЬ",HorizontalAlignment=HorizontalAlignment.Center});
        _status=new Label {Text="Подготовка входа в монастырь…",AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(620,140)};panel.AddChild(_status);
        _select=new Button {Text="Выбрать папку установленной Wizardry 8"};_select.Pressed+=SelectInstallation;panel.AddChild(_select);
        panel.AddChild(new Label {Text="Оригинальные данные остаются на вашем компьютере.\nПодготовка моделей выполняется один раз и может занять несколько минут.",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var exit=new Button {Text="Выйти"};exit.Pressed+=()=>GetTree().Quit();panel.AddChild(exit);
        if(System.Environment.GetEnvironmentVariable("NEO_PICKER_SMOKE")=="1"){Callable.From(CheckFolderPicker).CallDeferred();return;}
        string? world=null,previousInstallation=null;
        var configured=System.Environment.GetEnvironmentVariable("NEO_WORLD_CACHE");
        if(!string.IsNullOrWhiteSpace(configured))world=Path.Combine(configured,"monastery1.glb");
        if(world is null && System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")!="1" && File.Exists(_profile))
        {
            try {using var json=JsonDocument.Parse(File.ReadAllText(_profile));world=json.RootElement.GetProperty("world").GetString();if(json.RootElement.TryGetProperty("installation",out var savedInstallation))previousInstallation=savedInstallation.GetString();}
            catch(Exception e) when(e is IOException or JsonException or KeyNotFoundException or InvalidOperationException){_status.Text="Не удалось прочитать настройки: "+e.Message;}
        }
        if(world is not null && Complete(world)){Callable.From(()=>Enter(world)).CallDeferred();return;}
        if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")=="1")
        {
            var original=System.Environment.GetEnvironmentVariable("NEO_WIZ8_ROOT");
            if(world is null && !string.IsNullOrWhiteSpace(original)){Callable.From(()=>Prepare(original)).CallDeferred();return;}
            if(world is null){GD.Print("PASS: ordinary startup shows original-data setup instead of training");GetTree().Quit();return;}
            GD.PushError("Configured player content is incomplete");GetTree().Quit(1);return;
        }
        _status.Text=(world is null?"Для входа в монастырь выберите папку оригинальной Wizardry 8.":"Обновились данные монастыря. Выберите папку оригинальной Wizardry 8 для подготовки новой версии.")+"\nИгра найдёт уровни, текстуры и модели и подготовит их автоматически.";
        var root=System.Environment.GetEnvironmentVariable("NEO_WIZ8_ROOT")??previousInstallation;
        if(string.IsNullOrWhiteSpace(root))
        {
            var candidates=new List<string>();
            foreach(var prefix in new[]{System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86),System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),"C:\\GOG Games","D:\\GOG Games"})
                foreach(var relative in new[]{"Steam/steamapps/common/Wizardry 8","Steam/steamapps/common/Wizardry8","Wizardry 8","Wizardry8"})
                {
                    string candidate=Path.Combine(prefix,relative);
                    if(File.Exists(Path.Combine(candidate,"Data","DATA.SLF")) && File.Exists(Path.Combine(candidate,"Levels","LEVELS.SLF")))candidates.Add(candidate);
                }
            var found=candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(found.Length==1)root=found[0];
        }
        if(!string.IsNullOrWhiteSpace(root))Callable.From(()=>Prepare(root)).CallDeferred();
    }
    private static bool Complete(string world)
    {
        if(!File.Exists(world))return false;
        string folder=Path.GetDirectoryName(Path.GetFullPath(world))!;
        return new[]{".nwcol",".nwobjects.json",".nwdoors.json",".nwdoors.glb",".nwprops.json",".nwprops.glb",".nwencounter.json"}.All(s=>File.Exists(Path.ChangeExtension(world,s))) &&
            new[]{"items.json","monsters.json","creatures-index.json"}.All(s=>File.Exists(Path.Combine(folder,s)));
    }
    private void SelectInstallation()
    {
        if(_busy)return;
        OpenInstallationPicker(Prepare);
    }
    private FileDialog OpenInstallationPicker(Action<string> selected)
    {
        var dialog=new FileDialog {
            FileMode=FileDialog.FileModeEnum.OpenDir,Access=FileDialog.AccessEnum.Filesystem,
            ModeOverridesTitle=false,Title="Папка установленной Wizardry 8",
            OkButtonText="Выбрать папку",CancelButtonText="Отмена"
        };
        AddChild(dialog);
        dialog.DirSelected+=root=>{dialog.QueueFree();selected(root);};
        dialog.Canceled+=dialog.QueueFree;
        // Embedded windows use the logical viewport (960 × 540), not the desktop size.
        // A fixed 600px height puts the confirmation row outside the game window.
        dialog.PopupCenteredClamped(new Vector2I(900,600),0.9f);
        return dialog;
    }
    private async void CheckFolderPicker()
    {
        try
        {
            foreach(var size in new[]{new Vector2I(960,540),new Vector2I(1280,800)})
            {
                GetWindow().Size=size;
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                string? selected=null;
                var dialog=OpenInstallationPicker(root=>selected=root);
                string folder=ProjectSettings.GlobalizePath("user://").TrimEnd('/','\\');
                dialog.CurrentDir=folder;
                for(int frame=0;frame<3;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var button=dialog.GetOkButton();
                var viewport=new Rect2(Vector2.Zero,GetViewport().GetVisibleRect().Size);
                var window=new Rect2(dialog.Position,dialog.Size);
                if(!button.IsVisibleInTree() || button.Disabled || button.Text!="Выбрать папку" ||
                    !viewport.Encloses(window) || !new Rect2(Vector2.Zero,dialog.Size).Encloses(button.GetGlobalRect()))
                    throw new InvalidOperationException($"Folder confirmation is inaccessible at {size}: window {window}, viewport {viewport}, button {button.GetGlobalRect()}");
                button.EmitSignal(Button.SignalName.Pressed);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                if(selected is null || Path.GetFullPath(selected).TrimEnd('/','\\')!=Path.GetFullPath(folder))
                    throw new InvalidOperationException("Folder confirmation did not select the current directory");
            }
            GD.Print("PASS: folder picker confirmation visible, inside viewport and selects current directory at 960x540 and 1280x800");
            GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private async void Prepare(string root)
    {
        if(_busy)return;_busy=true;_select.Disabled=true;
        _status.Text="Подготовка монастыря. Окно можно оставить открытым…";
        try
        {
            string app=OS.HasFeature("editor")?Directory.GetParent(ProjectSettings.GlobalizePath("res://").TrimEnd('/','\\'))!.FullName:Path.GetDirectoryName(OS.GetExecutablePath())!;
            string importer=Path.Combine(app,"importer"),python=Path.Combine(importer,"python","python.exe");
            if(!Directory.Exists(importer))importer=app;
            if(!File.Exists(python))python=OS.GetName()=="Windows"?"python":"python3";
            Directory.CreateDirectory(_cache);string result=Path.Combine(_cache,"result-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var start=new ProcessStartInfo(python) {WorkingDirectory=importer,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                foreach(var arg in new[]{"-u","-m","tools.prepare_game","--installation",root,"--out",_cache,"--result",result})start.ArgumentList.Add(arg);
                start.Environment["PYTHONUTF8"]="1";
                using var process=Process.Start(start)??throw new IOException("Не удалось запустить подготовку данных");
                _importProcess=process;
                string lastError="";
                process.OutputDataReceived+=(_,e)=>{if(e.Data is {} line && line.Length>0)Callable.From(()=>_status.Text="Подготовка монастыря…\n"+line[..Math.Min(line.Length,900)]).CallDeferred();};
                process.ErrorDataReceived+=(_,e)=>{if(e.Data is {} line)lastError=line;};
                process.BeginOutputReadLine();process.BeginErrorReadLine();await process.WaitForExitAsync();process.WaitForExit();
                if(process.ExitCode!=0)throw new IOException("Не удалось подготовить оригинальные данные: "+lastError);
                using var json=JsonDocument.Parse(File.ReadAllText(result));string world=json.RootElement.GetProperty("world").GetString()!;
                if(!Complete(world) || !Path.GetFullPath(world).StartsWith(Path.GetFullPath(_cache)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Подготовленный монастырь неполон");
                if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")!="1")ExplorationCheckpointFile.Write(_profile,JsonSerializer.Serialize(new {world,installation=root}));Enter(world);
            }
            finally {_importProcess=null;if(File.Exists(result))File.Delete(result);}
        }
        catch(Exception e){if(System.Environment.GetEnvironmentVariable("NEO_STARTUP_SMOKE")=="1"){GD.PushError(e.ToString());GetTree().Quit(1);return;}_status.Text="Не удалось открыть монастырь.\n"+e.Message+"\nВыберите папку оригинала и повторите подготовку.";}
        finally {_busy=false;_select.Disabled=false;}
    }
    public override void _ExitTree()
    {
        try{if(_importProcess is {HasExited:false})_importProcess.Kill(true);}catch(InvalidOperationException){}
    }
    private void Enter(string world)
    {
        _status.Text="Загрузка первого этажа…";
        var scene=new WorldPreview {GameMode=true,Arguments=["--world",Path.GetFullPath(world),"--expedition"]};
        scene.LoadFailed=message=>{_status.Text="Не удалось загрузить монастырь: "+message;Visible=true;};
        Visible=false;GetParent().AddChild(scene);
    }
}
