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
    private double _textureTime;
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
            ConfigureTextures(state, args.Contains("--world-smoke"));
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
                $"TEXTURED WORLD PREVIEW · {texturedSurfaces} textured surfaces · {_textureCycles.Count} animated materials\nCollision / encounters pending\n" +
                "WASD fly · Q/E down/up · right drag look · wheel speed · Home overview · Esc release" };
            layer.AddChild(label);
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
                foreach(var (option, actual) in new[] {("--world-textured", texturedSurfaces), ("--world-animations", _textureCycles.Count)})
                {
                    var at = Array.IndexOf(args, option);
                    if(at >= 0 && (at + 1 >= args.Length || int.Parse(args[at + 1]) != actual))
                        throw new InvalidDataException($"World material count mismatch: {option}");
                }
                foreach(var animation in _textureCycles)animation.Check();
                GD.Print($"PASS: textures {texturedSurfaces} surfaces, {_textureCycles.Count} animations; decoded pixel hashes verified");
                GD.Print($"PASS: world preview {_triangles} triangles, {surfaces} surfaces, finite bounds; no collision");
                Callable.From(()=>GetTree().Quit()).CallDeferred();
            }
        }
        catch(Exception error) { GD.PushError(error.ToString());GetTree().Quit(1); }
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
                    if(pixels.GetWidth() != extras.GetProperty("width").GetInt32() || pixels.GetHeight() != extras.GetProperty("height").GetInt32())
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
                    var frames = animation.GetProperty("images").EnumerateArray().Select(x=>images[x.GetInt32()]).ToArray();
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
        if(_view is null || _smoke)return;
        _textureTime += delta;
        foreach(var animation in _textureCycles)animation.Apply(_textureTime);
        if(!DisplayServer.WindowIsFocused())return;
        var direction = new Vector3((Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0),
            (Input.IsPhysicalKeyPressed(Key.E)?1:0)-(Input.IsPhysicalKeyPressed(Key.Q)?1:0),
            (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0));
        if(direction.LengthSquared() > 0)_view.Position += _view.Basis * direction.Normalized() * _speed * (float)delta;
    }
}
