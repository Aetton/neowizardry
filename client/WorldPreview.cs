using Godot;
using System.Collections.Immutable;
using NeoWizardry.Core;

// Shared original world: explicit research controls and the ordinary walking player entry.
public partial class WorldPreview : Node3D
{
    public bool GameMode {get;init;}
    public string[]? Arguments {get;init;}
    public Action<string>? LoadFailed {get;set;}
    private string[] Args=>Arguments??OS.GetCmdlineUserArgs();
    private Camera3D _view = null!;
    private Vector3 _center;
    private float _span;
    private float _speed;
    private bool _looking;
    private bool _smoke;
    private int _triangles;
    private double _textureTime;
    private WorldCollision? _collision;
    private WorldWalker? _walker;
    private WorldObjects? _objects;
    private WorldDoors? _doors;
    private bool _useDoor,_inspectDoor;
    private bool _saveCheckpoint,_loadCheckpoint;
    private string _contentHash="",_checkpointPath="";
    private Label _checkpointStatus=null!;
    private Label _doorStatus=null!;
    private int _arrivalIndex=-1;
    private bool _walking, _enterWalk;
    private Label _movementStatus = null!;
    private readonly List<TextureCycle> _textureCycles = [];
    private sealed record TextureCycle(BaseMaterial3D Material, Texture2D[] Frames, double Fps, bool Emissive)
    {
        public void Apply(double time)
        {
            var frame = (int)(Math.Floor(time * Fps) % Frames.Length);
            Material.AlbedoTexture = Frames[frame];
            if(Emissive)Material.EmissionTexture = Frames[frame];
        }
        public void Check()
        {
            Apply(1 / Fps + 0.000001);
            if(Material.AlbedoTexture.GetRid() != Frames[1].GetRid())throw new InvalidDataException("IFL frame advance failed");
            Apply(Frames.Length / Fps + 0.000001);
            if(Material.AlbedoTexture.GetRid() != Frames[0].GetRid())throw new InvalidDataException("IFL loop failed");
            Apply(0);
        }
    }

    public static bool Requested => OS.GetCmdlineUserArgs().Contains("--world");

    public override void _Ready()
    {
        try
        {
            var args = Args;
            var index = Array.IndexOf(args, "--world");
            if(index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--"))
                throw new InvalidDataException("Use --world /absolute/path/monastery1.glb");
            var path = Path.GetFullPath(args[index + 1]);
            if(!System.IO.File.Exists(path))throw new FileNotFoundException("World preview GLB is missing");
            _contentHash=ExplorationCheckpointFile.ContentHash(path);
            _checkpointPath=ProjectSettings.GlobalizePath($"user://exploration/{_contentHash}.json");
            using var document = new GltfDocument();
            using var state = new GltfState();
            var error = document.AppendFromFile(path, state);
            if(error != Error.Ok)throw new InvalidDataException($"GLB import failed: {error}");
            var scene = document.GenerateScene(state) ?? throw new InvalidDataException("GLB has no scene");
            AddChild(scene);
            ConfigureTextures(state, args.Contains("--world-smoke"));
            var staticAnimationCount=_textureCycles.Count;
            var collisionPath = Path.ChangeExtension(path,".nwcol");
            var objectsPath=Path.ChangeExtension(path,".nwobjects.json");
            var doorsPath=Path.ChangeExtension(path,".nwdoors.json");
            string? pvlHash=null;
            if(System.IO.File.Exists(collisionPath) || System.IO.File.Exists(objectsPath) || System.IO.File.Exists(doorsPath))
            {
                using var metadata = System.Text.Json.JsonDocument.Parse(Godot.Json.Stringify(state.Json));
                var root = metadata.RootElement;
                pvlHash = root.TryGetProperty("extras",out var extras) && extras.TryGetProperty("neoSourcePVL",out var sourceHash)?sourceHash.GetString():null;
            }
            if(System.IO.File.Exists(collisionPath))
            {
                _collision = WorldCollision.Load(collisionPath,pvlHash ?? "");AddChild(_collision);
                _walker = new WorldWalker();AddChild(_walker);
            }
            if(System.IO.File.Exists(objectsPath))
            {
                _objects=WorldObjects.Load(objectsPath,pvlHash ?? "");AddChild(_objects);
            }
            if(System.IO.File.Exists(doorsPath))
            {
                _doors=WorldDoors.Load(doorsPath,Path.ChangeExtension(path,".nwdoors.glb"),pvlHash??"",s=>ConfigureTextures(s,args.Contains("--world-smoke")));
                AddChild(_doors);
            }
            if(pvlHash is not null && File.Exists(Path.ChangeExtension(path,".nwprops.json")))
                AddChild(WorldProps.Load(Path.ChangeExtension(path,".nwprops.json"),pvlHash,s=>ConfigureTextures(s,args.Contains("--world-smoke"))));
            Aabb? bounds = null;
            int surfaces = 0, texturedSurfaces = 0;
            foreach(var mesh in Meshes(scene))
            {
                if(mesh.Mesh is not ArrayMesh geometry)throw new InvalidDataException("Expected imported ArrayMesh");
                var box = mesh.GlobalTransform * mesh.GetAabb();
                bounds = bounds is {} previous ? previous.Merge(box) : box;
                for(int surface = 0; surface < geometry.GetSurfaceCount(); surface++)
                {
                    surfaces++;
                    if(geometry.SurfaceGetMaterial(surface) is BaseMaterial3D { AlbedoTexture: not null })texturedSurfaces++;
                    if(geometry.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
                        throw new InvalidDataException("World preview requires triangle primitives");
                    var count = geometry.SurfaceGetArrayIndexLen(surface);
                    _triangles += (count > 0 ? count : geometry.SurfaceGetArrayLen(surface)) / 3;
                }

            }
            if(bounds is not {} world || surfaces == 0 || _triangles == 0 || !world.Position.IsFinite() || !world.Size.IsFinite())
                throw new InvalidDataException("World preview has no finite triangle bounds");
            _center = world.GetCenter();
            _span = Math.Max(world.Size.Length(), 1);
            _speed = Math.Max(_span / 12, 1);
            _view = new Camera3D { Current = true, Fov = 60, Near = 0.02f, Far = Math.Max(_span * 10, 1000) };
            AddChild(_view);
            Home();
            AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -30, 0), LightEnergy = 1.2f });
            AddChild(new WorldEnvironment { Environment = new Godot.Environment {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.06f, 0.075f, 0.09f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = 0.65f
            }});
            var layer = new CanvasLayer();AddChild(layer);
            var label = new Label { Position = new Vector2(16, 16), Text =
                $"{Path.GetFileNameWithoutExtension(path)} · {surfaces} surfaces · {_triangles:N0} triangles\n" +
                $"TEXTURED WORLD PREVIEW · {texturedSurfaces} textured surfaces · {_textureCycles.Count} animated materials\nStatic collision: {(_collision is null?"not loaded":_collision.Faces.Length.ToString("N0"))} · encounters pending\n" +
                "WASD move · F walk at aimed floor / return to flight · Q/E fly vertically · right drag look · Home overview" };
            layer.AddChild(label);
            _movementStatus = new Label { Position=new Vector2(16,112),Text="FLIGHT · aim at a floor and press F to walk" };
            layer.AddChild(_movementStatus);
            layer.AddChild(new Label { Position=new Vector2(16,140),Text=_objects is null?"Native object data not loaded":
                $"N next native arrival ({_objects.Arrivals.Length}) · O object markers · {_objects.PropCount} props · {_objects.DoorCount} stored door states · actions pending" });
            var crosshair=new Label { Text="+",MouseFilter=Control.MouseFilterEnum.Ignore };
            crosshair.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            crosshair.Position-=new Vector2(4,10);layer.AddChild(crosshair);
            _doorStatus=new Label { Position=new Vector2(16,168),Text=_doors is null?"Door geometry not loaded":
                 $"P inspect door animation · R use aimed door · {_doors.Doors.Count} native models · {_doors.InteractiveCount} eligible · F5 checkpoints" };
            layer.AddChild(_doorStatus);
            _checkpointStatus=new Label { Position=new Vector2(16,196),Text="F5 save checkpoint · F9 load · resumes in flight · separate from expedition saves" };
            layer.AddChild(_checkpointStatus);
            InitializeExpedition(args,layer,pvlHash);
            if(GameMode)
            {
                foreach(var child in layer.GetChildren())if(child is Label)child.Set("visible",false);
                _objects?.Hide();InitializePlayerGame(layer);Callable.From(StartPlayerGame).CallDeferred();
            }
            _smoke = args.Contains("--world-smoke");
            if(_smoke)
            {
                var expected = Array.IndexOf(args, "--world-triangles");
                if(expected >= 0 && (expected + 1 >= args.Length || int.Parse(args[expected + 1]) != _triangles))
                    throw new InvalidDataException("Imported world triangle count differs from PVL report");
                var expectedBounds = Array.IndexOf(args, "--world-bounds");
                if(expectedBounds >= 0)
                {
                    if(expectedBounds + 1 >= args.Length)throw new InvalidDataException("Missing expected world bounds");
                    var values = args[expectedBounds + 1].Split(',').Select(x=>float.Parse(x,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    if(values.Length != 6 || values.Any(x=>!float.IsFinite(x)))throw new InvalidDataException("Invalid expected world bounds");
                    var minimum = new Vector3(values[0],values[1],values[2]);
                    var maximum = new Vector3(values[3],values[4],values[5]);
                    var tolerance = Math.Max(_span * 0.00001f, 0.0001f);
                    if(world.Position.DistanceTo(minimum) > tolerance || world.End.DistanceTo(maximum) > tolerance)
                        throw new InvalidDataException("Imported world bounds differ from PVL report");
                }
                foreach(var (option, actual) in new[] {("--world-textured", texturedSurfaces), ("--world-animations", staticAnimationCount)})
                {
                    var at = Array.IndexOf(args, option);
                    if(at >= 0 && (at + 1 >= args.Length || int.Parse(args[at + 1]) != actual))
                        throw new InvalidDataException($"World material count mismatch: {option}");
                }
                foreach(var animation in _textureCycles)animation.Check();
                GD.Print($"PASS: textures {texturedSurfaces} surfaces, {_textureCycles.Count} animations; decoded pixel hashes verified");
                GD.Print($"PASS: world preview {_triangles} triangles, {surfaces} surfaces, finite bounds");
                if(_objects is not null)
                {
                    foreach(var (option,actual) in new[] {("--world-objects",_objects.PropCount),("--world-arrivals",_objects.Arrivals.Length),("--world-doors",_objects.DoorCount)})
                    {
                        var at=Array.IndexOf(args,option);
                        if(at>=0 && (at+1>=args.Length || int.Parse(args[at+1])!=actual))throw new InvalidDataException($"Native object count mismatch: {option}");
                    }
                    for(int i=0;i<_objects.Arrivals.Length;i++)
                    {
                        JumpToArrival(i);
                        if(!_view.GlobalPosition.IsEqualApprox(_objects.Arrivals[i].Position+Vector3.Up*1.25f))throw new InvalidDataException("Arrival navigation failed");
                    }
                    Home();
                    GD.Print($"PASS: objects {_objects.PropCount} props, {_objects.DoorCount} door payloads, {_objects.Arrivals.Length} native arrivals navigated");
                }
                else if(args.Contains("--world-objects"))throw new InvalidDataException("Native object sidecar missing");
                if(args.Contains("--world-door-models"))
                {
                    if(_doors is null)throw new InvalidDataException("Native door geometry missing");
                    int Expected(string option) { var at=Array.IndexOf(args,option);if(at<0 || at+1>=args.Length)throw new InvalidDataException("Missing door smoke count");return int.Parse(args[at+1]); }
                    _doors.Check(Expected("--world-door-models"),Expected("--world-door-frames"),Expected("--world-door-triangles"),Expected("--world-door-interactive"),_walker);
                }
                CheckpointSmoke();
                if(args.Contains("--world-collision-smoke"))Callable.From(CollisionSmoke).CallDeferred();
                else Callable.From(()=>GetTree().Quit()).CallDeferred();
            }
        }
        catch(Exception error)
        {
            if(GameMode && LoadFailed is not null){LoadFailed(error.Message);QueueFree();}
            else {GD.PushError(error.ToString());GetTree().Quit(1);}
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if(_view is null || _smoke)return;
        if(GameMode && (!_gameReady || _gamePaused))return;
        if(_saveCheckpoint || _loadCheckpoint)
        {
            bool save=_saveCheckpoint;_saveCheckpoint=_loadCheckpoint=false;
            try
            {
                if(save)
                {
                    if(_expedition is not null)SaveExpedition();
                    else
                    {
                        var checkpoint=CaptureCheckpoint();
                        ExplorationCheckpointFile.Write(_checkpointPath,checkpoint.Serialize(_contentHash,CheckpointLimits));
                    }
                    _checkpointStatus.Text="Checkpoint saved · F9 restores in flight";
                }
                else
                {
                    if(_expedition is not null)LoadExpedition();
                    else
                    {
                        var checkpoint=ExplorationCheckpoint.Parse(ExplorationCheckpointFile.Read(_checkpointPath),_contentHash,CheckpointLimits);
                        RestoreCheckpoint(checkpoint);
                    }
                    _checkpointStatus.Text="Checkpoint loaded in flight · F enters walking at aimed floor";
                }
            }
            catch(Exception error) when(error is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or System.Text.DecoderFallbackException)
            { _checkpointStatus.Text=$"Checkpoint {(save?"save":"load")} failed: {error.Message}"; }
            return;
        }
        if(GameMode && !_walking && !BattleOpen)ResumePlayerWalking();
        if(GameMode)
        {
            _autoSaveTime+=delta;
            if(_autoSaveTime>=15){_autoSaveTime=0;SavePlayerGame();}
        }
        if(_beginEncounter)TryBeginExpeditionEncounter();
        if(BattleOpen)return;
        if(_inspectDoor)
        {
            _inspectDoor=false;_walking=false;_enterWalk=false;
            _doorStatus.Text=_doors?.PreviewNext(_view)??"No native door geometry loaded";
        }
        if(_useDoor) { _useDoor=false;_doorStatus.Text=_doors?.Use(_view)??"No native door geometry loaded"; }
        bool wasInspecting=_doors?.IsInspecting==true;
        var doorMessage=_doors?.Step(delta,_walking?_walker:null);
        if(doorMessage is not null)_doorStatus.Text=doorMessage;
        if(_enterWalk)
        {
            if(wasInspecting || _doors is { IsInspecting:true }) { _doors?.CancelInspection();return; }
            _enterWalk=false;
            if(_collision is null || _walker is null) { _movementStatus.Text="No original collision data loaded";return; }
            var hit = _collision.Ray(_view.GlobalPosition,_view.GlobalPosition-_view.GlobalBasis.Z*_span*2);
            if(!_collision.IsWalkableHit(hit)) { _movementStatus.Text="Aim at a walkable floor, then press F";return; }
            var center=hit["position"].AsVector3()+Vector3.Up*(WorldWalker.Height/2+0.05f);
            if(!_walker.CanStand(center)) { _movementStatus.Text="Not enough room for the walking capsule";return; }
            _walker.GlobalPosition=center;_walker.Velocity=Vector3.Zero;_walking=true;
        }
        if(!_walking || _walker is null)return;
        var direction = Vector3.Zero;
        if(DisplayServer.WindowIsFocused())
        {
            var input=new Vector3((Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0),0,
                (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0));
            direction=new Basis(Vector3.Up,_view.GlobalRotation.Y)*input.Normalized();
        }
        _walker.Step(delta,direction);
        _view.GlobalPosition=_walker.GlobalPosition+Vector3.Up*WorldWalker.EyeOffset;
        _movementStatus.Text=_walker.IsOnFloor()?"WALKING · F returns to flight":"AIRBORNE · F returns to flight";
    }

    private ImmutableArray<ExplorationDoorLimit> CheckpointLimits => _doors?.CheckpointLimits ?? [];
    private static ExplorationPoint Point(Vector3 value)=>new(value.X,value.Y,value.Z);
    private static Vector3 Vector(ExplorationPoint value)=>new(value.X,value.Y,value.Z);

    private ExplorationCheckpoint CaptureCheckpoint() => new(1,_contentHash,Point(_view.GlobalPosition),
        Point(_view.Rotation),_speed,_doors?.CaptureCheckpoint() ?? []);

    private void RestoreCheckpoint(ExplorationCheckpoint checkpoint)
    {
        checkpoint.Validate(_contentHash,CheckpointLimits);
        if(checkpoint.Speed>_span*2)throw new InvalidDataException("Checkpoint flight speed exceeds this world's limit");
        _doors?.RestoreCheckpoint(checkpoint.Doors);
        _walking=_enterWalk=_useDoor=_inspectDoor=false;
        if(_walker is not null)_walker.Velocity=Vector3.Zero;
        _view.GlobalPosition=Vector(checkpoint.Position);_view.Rotation=Vector(checkpoint.Rotation);_speed=checkpoint.Speed;
        _looking=false;Input.MouseMode=Input.MouseModeEnum.Visible;
        _movementStatus.Text="FLIGHT · checkpoint restored";
    }

    private void CheckpointSmoke()
    {
        var initial=CaptureCheckpoint();
        string Encode()=>CaptureCheckpoint().Serialize(_contentHash,CheckpointLimits);
        var encoded=Encode();
        _view.GlobalPosition+=Vector3.One;_speed*=0.5f;
        var loaded=ExplorationCheckpoint.Parse(encoded,_contentHash,CheckpointLimits);
        RestoreCheckpoint(loaded);
        if(Encode()!=encoded || _walking || _enterWalk)throw new InvalidDataException("Exploration checkpoint roundtrip failed");
        if(_doors is { Doors.Count:>0 })
        {
            foreach(var door in _doors.Doors.Where(d=>d.BlockedReason.Length==0))
            {
                door.RequestUse();door.Advance(0.5/door.Fps,null);
                var moving=Encode();
                door.Advance(256.0/door.Fps,null);
                RestoreCheckpoint(ExplorationCheckpoint.Parse(moving,_contentHash,CheckpointLimits));
                if(Encode()!=moving || door.CurrentFrame==door.TargetFrame)throw new InvalidDataException("Moving door checkpoint lost its phase/target");
                door.Advance(256.0/door.Fps,null);
                if(door.CurrentFrame!=door.TargetFrame)throw new InvalidDataException("Restored door did not finish movement");
                RestoreCheckpoint(initial);
            }
            _doors.PreviewNext(_view);
            bool rejected=false;
            try { CaptureCheckpoint(); } catch(InvalidOperationException) { rejected=true; }
            if(!rejected)throw new InvalidDataException("Animation inspection leaked into exploration checkpoint");
        }
        RestoreCheckpoint(initial);
        var invalid=loaded with { ContentHash=new string('0',64) };
        bool refused=false;
        try { RestoreCheckpoint(invalid); } catch(InvalidDataException) { refused=true; }
        if(!refused || Encode()!=encoded)throw new InvalidDataException("Checkpoint rejection changed live state");
        GD.Print("PASS: exploration checkpoints restore camera and door motion, reject inspection and foreign content without mutation");
    }

    private async void CollisionSmoke()
    {
        try
        {
            if(_collision is null || _walker is null)throw new InvalidDataException("Original collision data missing");
            var args=Args;var at=Array.IndexOf(args,"--world-colliders");
            if(at<0 || at+1>=args.Length || int.Parse(args[at+1])!=_collision.Faces.Length)
                throw new InvalidDataException("Collision surface count mismatch");
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            _doors?.CheckPhysics(_walker);
            WorldCollision.Face? floor=null;
            foreach(var face in _collision.Faces.Where(f=>f.Walkable && f.Normal.Y>0.99f && f.AreaSquared>0.1f).OrderByDescending(f=>f.AreaSquared).Take(256))
            {
                var hit=_collision.Ray(face.Center+Vector3.Up*0.15f,face.Center-Vector3.Up*0.15f);
                if(_collision.IsWalkableHit(hit) && _walker.CanStand(face.Center+Vector3.Up*(WorldWalker.Height/2+0.05f))) { floor=face;break; }
            }
            if(floor is null)throw new InvalidDataException("No clear original floor found for capsule test");
            _walker.GlobalPosition=floor.Center+Vector3.Up*(WorldWalker.Height/2+0.05f);
            _walker.Velocity=Vector3.Zero;
            for(int step=0;step<45;step++)
            {
                _walker.Step(1.0/60,Vector3.Zero);
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            }
            if(!_walker.IsOnFloor() || Math.Abs(_walker.GlobalPosition.Y-floor.Center.Y-WorldWalker.Height/2)>0.12f)
                throw new InvalidDataException("Capsule did not settle on original floor");
            bool blocked=false;
            foreach(var face in _collision.Faces.Where(f=>Math.Abs(f.Normal.Y)<0.05f && f.Normal.LengthSquared()>0.5f && f.AreaSquared>0.1f).OrderByDescending(f=>f.AreaSquared).Take(256))
            {
                var normal=face.Normal.Normalized();
                var center=face.Center+normal*(WorldWalker.Radius+0.15f);
                if(!_walker.CanStand(center))continue;
                using var contact=new KinematicCollision3D();
                var transform=new Transform3D(Basis.Identity,center);
                if(_walker.TestMove(transform,-normal*(WorldWalker.Radius+0.4f),contact) &&
                   contact.GetNormal().Dot(normal)>0.8f && !_walker.TestMove(transform,normal*0.1f)) { blocked=true;break; }
            }
            if(!blocked)throw new InvalidDataException("Capsule wall sweep did not find a blocking original wall");
            GD.Print($"PASS: collision {_collision.Faces.Length} static surfaces; native walkable floor, settled capsule and blocking wall sweep");
            if(args.Contains("--world-expedition-smoke"))CheckWorldExpedition();
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString());GetTree().Quit(1); }
    }

    private static int JsonInteger(System.Text.Json.JsonElement value)
    {
        // Godot Variant JSON roundtrips integral metadata as e.g. 128.0.
        var number = value.GetDouble();
        if(!double.IsFinite(number) || number < 0 || number > int.MaxValue || number != Math.Truncate(number))
            throw new InvalidDataException("Expected nonnegative integral texture metadata");
        return (int)number;
    }

    private void ConfigureTextures(GltfState state, bool verifyPixels)
    {
        using var json = System.Text.Json.JsonDocument.Parse(Godot.Json.Stringify(state.Json));
        var root = json.RootElement;
        var images = state.GetImages();
        if(root.TryGetProperty("images", out var definitions))
        {
            int index = 0;
            foreach(var definition in definitions.EnumerateArray())
            {
                if(definition.TryGetProperty("extras", out var extras) && extras.TryGetProperty("neoPixelSha256", out var digest))
                {
                    if(index >= images.Count || images[index] is null)throw new InvalidDataException("Missing embedded image");
                    using var pixels = images[index].GetImage() ?? throw new InvalidDataException("Image pixels unavailable");
                    if(pixels.GetWidth() != JsonInteger(extras.GetProperty("width")) || pixels.GetHeight() != JsonInteger(extras.GetProperty("height")))
                        throw new InvalidDataException("Texture dimensions differ from TGA export");
                    if(verifyPixels)
                    {
                        pixels.ClearMipmaps();
                        pixels.Convert(Image.Format.Rgba8);
                        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels.GetData())).ToLowerInvariant();
                        if(hash != digest.GetString())throw new InvalidDataException("Decoded PNG pixels differ from TGA export");
                    }
                }
                index++;
            }
        }
        if(root.TryGetProperty("materials", out var materialDefinitions))
        {
            var materials = state.GetMaterials();
            int index = 0;
            foreach(var definition in materialDefinitions.EnumerateArray())
            {
                if(definition.TryGetProperty("extras", out var extras) && extras.TryGetProperty("neoAnimation", out var animation))
                {
                    if(index >= materials.Count || materials[index] is not BaseMaterial3D material)throw new InvalidDataException("Missing animated material");
                    var frames = animation.GetProperty("images").EnumerateArray().Select(x=>images[JsonInteger(x)]).ToArray();
                    var fps = animation.GetProperty("fps").GetDouble();
                    if(frames.Length < 2 || frames.Any(x=>x is null) || !double.IsFinite(fps) || fps <= 0 || fps > 240)
                        throw new InvalidDataException("Invalid IFL animation");
                    _textureCycles.Add(new TextureCycle(material, frames, fps, animation.GetProperty("emissive").GetBoolean()));
                }
                index++;
            }
        }
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if(node is MeshInstance3D mesh)yield return mesh;
        foreach(var child in node.GetChildren())foreach(var item in Meshes(child))yield return item;
    }

    private void JumpToArrival(int index)
    {
        if(_objects is null || index<0 || index>=_objects.Arrivals.Length)return;
        _doors?.CancelInspection();
        _arrivalIndex=index;_walking=false;_enterWalk=false;
        var point=_objects.Arrivals[index];
        _view.GlobalPosition=point.Position+Vector3.Up*1.25f;
        _movementStatus.Text=$"ARRIVAL {index+1}/{_objects.Arrivals.Length} · {point.Name} · aim at floor and press F to walk";
    }

    private void Home()
    {
        _doors?.CancelInspection();
        _walking=false;_enterWalk=false;
        _view.Position = _center + new Vector3(0.55f, 0.65f, 0.8f) * _span;
        _view.LookAt(_center);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if(_view is null)return;
        if(GameMode){PlayerInput(input);return;}
        if(BattleOpen && input is not InputEventKey)return;
        if(input is InputEventMouseButton button)
        {
            if(button.ButtonIndex == MouseButton.Right)
            { _looking = button.Pressed;Input.MouseMode = _looking ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible; }
            if(button.Pressed && button.ButtonIndex == MouseButton.WheelUp)_speed = Math.Min(_speed * 1.3f, _span * 2);
            if(button.Pressed && button.ButtonIndex == MouseButton.WheelDown)_speed = Math.Max(_speed / 1.3f, 0.1f);
        }
        if(input is InputEventMouseMotion motion && _looking)
        {
            _view.Rotation = new Vector3(Math.Clamp(_view.Rotation.X - motion.Relative.Y * 0.003f, -1.55f, 1.55f),
                _view.Rotation.Y - motion.Relative.X * 0.003f, 0);
        }
        if(input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if(key.Keycode == Key.F5)_saveCheckpoint=true;
            if(key.Keycode == Key.F9)_loadCheckpoint=true;
            if(key.Keycode == Key.B)_beginEncounter=true;
            if(key.Keycode == Key.V && _creature is {Visible:true})
            { _walking=_enterWalk=false;_doors?.CancelInspection();_creature.Focus(_view); }
            if(BattleOpen)return;
            if(key.Keycode == Key.P)_inspectDoor=true;
            if(key.Keycode == Key.R)_useDoor=true;
            if(key.Keycode == Key.O && _objects is not null)_objects.Visible=!_objects.Visible;
            if(key.Keycode == Key.N)
            {
                if(_objects is not null && _objects.Arrivals.Length>0)JumpToArrival((_arrivalIndex+1)%_objects.Arrivals.Length);
                else _movementStatus.Text="No native arrival points loaded";
            }
            if(key.Keycode == Key.Home) { Home();_movementStatus.Text="FLIGHT · overview"; }
            if(key.Keycode == Key.F)
            {
                if(_walking) { _walking=false;_movementStatus.Text="FLIGHT"; }
                else _enterWalk=true;
            }
            if(key.Keycode == Key.Escape) { _looking = false;Input.MouseMode = Input.MouseModeEnum.Visible; }
        }
    }

    public override void _Process(double delta)
    {
        if(_view is null || _smoke)return;
        if(GameMode){RefreshPlayerHud();if(!_gameReady || _gamePaused)return;}
        _textureTime += delta;
        foreach(var animation in _textureCycles)animation.Apply(_textureTime);
        if(GameMode || BattleOpen || _walking || !DisplayServer.WindowIsFocused())return;
        var direction = new Vector3((Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0),
            (Input.IsPhysicalKeyPressed(Key.E)?1:0)-(Input.IsPhysicalKeyPressed(Key.Q)?1:0),
            (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0));
        if(direction.LengthSquared() > 0)_view.Position += _view.Basis * direction.Normalized() * _speed * (float)delta;
    }
}
