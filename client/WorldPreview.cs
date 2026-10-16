using Godot;

// Research viewer only: static geometry has no collision, encounter or save semantics.
public partial class WorldPreview : Node3D
{
    private Camera3D _view = null!;
    private Vector3 _center;
    private float _span;
    private float _speed;
    private bool _looking;
    private bool _smoke;
    private int _triangles;

    public static bool Requested => OS.GetCmdlineUserArgs().Contains("--world");

    public override void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var index = Array.IndexOf(args, "--world");
            if(index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--"))
                throw new InvalidDataException("Use --world /absolute/path/monastery1.glb");
            var path = Path.GetFullPath(args[index + 1]);
            if(!System.IO.File.Exists(path))throw new FileNotFoundException("World preview GLB is missing");
            using var document = new GltfDocument();
            using var state = new GltfState();
            var error = document.AppendFromFile(path, state);
            if(error != Error.Ok)throw new InvalidDataException($"GLB import failed: {error}");
            var scene = document.GenerateScene(state) ?? throw new InvalidDataException("GLB has no scene");
            AddChild(scene);
            Aabb? bounds = null;
            int surfaces = 0;
            foreach(var mesh in Meshes(scene))
            {
                if(mesh.Mesh is not ArrayMesh geometry)throw new InvalidDataException("Expected imported ArrayMesh");
                var box = mesh.GlobalTransform * mesh.GetAabb();
                bounds = bounds is {} previous ? previous.Merge(box) : box;
                for(int surface = 0; surface < geometry.GetSurfaceCount(); surface++)
                {
                    surfaces++;
                    if(geometry.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
                        throw new InvalidDataException("World preview requires triangle primitives");
                    var count = geometry.SurfaceGetArrayIndexLen(surface);
                    _triangles += (count > 0 ? count : geometry.SurfaceGetArrayLen(surface)) / 3;
                }
                // Preview colors are authored; do not imply native material reconstruction.
                mesh.MaterialOverride = new StandardMaterial3D {
                    AlbedoColor = new Color(0.66f, 0.69f, 0.73f),
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    Roughness = 1
                };
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
                "STATIC GEOMETRY PREVIEW · textures / collision / encounters pending\n" +
                "WASD fly · Q/E down/up · right drag look · wheel speed · Home overview · Esc release" };
            layer.AddChild(label);
            _smoke = args.Contains("--world-smoke");
            if(_smoke)
            {
                var expected = Array.IndexOf(args, "--world-triangles");
                if(expected >= 0 && (expected + 1 >= args.Length || int.Parse(args[expected + 1]) != _triangles))
                    throw new InvalidDataException("Imported world triangle count differs from PVL report");
                GD.Print($"PASS: world preview {_triangles} triangles, {surfaces} surfaces, finite bounds; no collision");
                Callable.From(()=>GetTree().Quit()).CallDeferred();
            }
        }
        catch(Exception error) { GD.PushError(error.ToString());GetTree().Quit(1); }
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if(node is MeshInstance3D mesh)yield return mesh;
        foreach(var child in node.GetChildren())foreach(var item in Meshes(child))yield return item;
    }

    private void Home()
    {
        _view.Position = _center + new Vector3(0.55f, 0.65f, 0.8f) * _span;
        _view.LookAt(_center);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if(_view is null)return;
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
        if(input is InputEventKey key && key.Pressed)
        {
            if(key.Keycode == Key.Home)Home();
            if(key.Keycode == Key.Escape) { _looking = false;Input.MouseMode = Input.MouseModeEnum.Visible; }
        }
    }

    public override void _Process(double delta)
    {
        if(_view is null || _smoke || !DisplayServer.WindowIsFocused())return;
        var direction = new Vector3((Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0),
            (Input.IsPhysicalKeyPressed(Key.E)?1:0)-(Input.IsPhysicalKeyPressed(Key.Q)?1:0),
            (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0));
        if(direction.LengthSquared() > 0)_view.Position += _view.Basis * direction.Normalized() * _speed * (float)delta;
    }
}
