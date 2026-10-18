using Godot;
using System.Text.Json;
using System.Security.Cryptography;

public partial class WorldProps : Node3D
{
    public int Count {get;private set;}
    public static WorldProps Load(string path,string source,Action<GltfState> textures)
    {
        using var json=JsonDocument.Parse(File.ReadAllText(path));var root=json.RootElement;
        if(root.GetProperty("schema").GetInt32()!=1 || root.GetProperty("pvl_sha256").GetString()!=source)throw new InvalidDataException("Prop source mismatch");
        var result=new WorldProps {Name="OriginalProps"};var records=root.GetProperty("props");
        if(records.GetArrayLength()>100000)throw new InvalidDataException("Too many props");
        if(records.GetArrayLength()==0)return result;
        string meshPath=Path.ChangeExtension(path,".glb");
        using(var stream=File.OpenRead(meshPath))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(root.GetProperty("mesh_sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Prop GLB hash mismatch");
        using var document=new GltfDocument();using var state=new GltfState();
        if(document.AppendFromFile(meshPath,state)!=Error.Ok)throw new InvalidDataException("Prop import failed");
        var scene=document.GenerateScene(state)??throw new InvalidDataException("Missing prop scene");result.AddChild(scene);textures(state);
        IEnumerable<MeshInstance3D> Meshes(Node node){if(node is MeshInstance3D mesh)yield return mesh;foreach(var child in node.GetChildren())foreach(var item in Meshes(child))yield return item;}
        var meshes=Meshes(scene).ToDictionary(m=>m.Name.ToString());var owned=new HashSet<int>();
        foreach(var record in records.EnumerateArray())
        {
            int index=record.GetProperty("mesh").GetInt32();
            if(!owned.Add(index) || !meshes.TryGetValue($"PVL_mesh_{index}",out var mesh))throw new InvalidDataException("Missing/duplicate prop geometry");
            var bounds=record.GetProperty("bounds");Vector3 V(JsonElement p)=>new(p[0].GetSingle(),p[1].GetSingle(),p[2].GetSingle());
            var actual=WorldDoor.RenderBounds(mesh);
            if((actual.Position-V(bounds[0])).Length()>0.002f || (actual.End-V(bounds[1])).Length()>0.002f)throw new InvalidDataException("Prop pose bounds mismatch");
            if(record.GetProperty("collidable").GetBoolean())
            {
                var body=new StaticBody3D {CollisionLayer=1,CollisionMask=2};
                body.AddChild(new CollisionShape3D {Shape=mesh.Mesh.CreateTrimeshShape()});mesh.AddChild(body);
            }
            result.Count++;
        }
        if(owned.Count!=meshes.Count)throw new InvalidDataException("Unowned prop mesh");return result;
    }
}
