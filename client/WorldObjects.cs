using Godot;
using System.Text.Json;

// Research navigation/inspection only. Native trigger actions are never executed here.
public partial class WorldObjects : Node3D
{
    public sealed record Point(long SourceId, string Name, Vector3 Position, string Description);
    public Point[] Arrivals { get; private set; } = [];
    public int PropCount { get; private set; }
    public int TriggerCount { get; private set; }
    public int DoorCount { get; private set; }
    private readonly List<Point> _points = [];

    public static WorldObjects Load(string path, string pvlHash)
    {
        if(new FileInfo(path).Length > 32*1024*1024)throw new InvalidDataException("Object sidecar exceeds supported size");
        using var doc=JsonDocument.Parse(System.IO.File.ReadAllText(path));
        var root=doc.RootElement;
        if(root.GetProperty("schema").GetInt32()!=1 || pvlHash.Length!=64 || root.GetProperty("pvl_sha256").GetString()!=pvlHash)
            throw new InvalidDataException("Object sidecar schema/provenance mismatch");
        var result=new WorldObjects { Name="OriginalObjectInspection",Visible=false };
        Vector3 Position(JsonElement value)
        {
            if(value.GetArrayLength()!=3)throw new InvalidDataException("Invalid native object position");
            var p=new Vector3(value[0].GetSingle(),value[1].GetSingle(),value[2].GetSingle());
            if(!p.IsFinite())throw new InvalidDataException("Non-finite native object position");
            // Native prop/trigger loaders multiply these serialized coordinates by 500.
            var scaled=new Vector3(p.X,p.Y,-p.Z)*0.65f;
            if(!scaled.IsFinite())throw new InvalidDataException("Object position overflow");
            return scaled;
        }
        string Name(JsonElement value)=>value.GetProperty("name").GetString() ?? "";
        string Door(JsonElement trigger)
        {
            var door=trigger.GetProperty("door");
            if(door.ValueKind!=JsonValueKind.Object || door.TryGetProperty("legacy_geometry",out _))return "";
            return $"door · {(door.GetProperty("open").GetBoolean()?"open":"closed")} · {(door.GetProperty("locked").GetBoolean()?"locked":"unlocked")}";
        }
        var props=root.GetProperty("props");
        var triggers=root.GetProperty("triggers");
        if(props.GetArrayLength()>100_000 || triggers.GetArrayLength()>100_000)throw new InvalidDataException("Too many original objects");
        result.PropCount=props.EnumerateArray().Count(p=>p.GetProperty("bank").GetString()=="props");
        foreach(var prop in props.EnumerateArray())
        {
            if(prop.GetProperty("position").ValueKind==JsonValueKind.Null)continue;
            var description="prop";
            var trigger=prop.GetProperty("trigger");
            if(trigger.ValueKind==JsonValueKind.Object && Door(trigger) is { Length:>0 } door)description=door;
            result._points.Add(new Point(prop.GetProperty("start").GetInt64(),Name(prop),Position(prop.GetProperty("position")),description));
        }
        var positioned=new Dictionary<long,Point>();
        var ids=new HashSet<long>();
        foreach(var trigger in triggers.EnumerateArray())
        {
            result.TriggerCount++;
            long id=trigger.GetProperty("start").GetInt64();
            if(id<0 || !ids.Add(id))throw new InvalidDataException("Duplicate/invalid native trigger ID");
            if(Door(trigger).Length>0)result.DoorCount++;
            var p=trigger.GetProperty("position");
            if(p.ValueKind==JsonValueKind.Null)continue;
            var action=trigger.GetProperty("action");
            var point=new Point(id,Name(trigger),Position(p),"trigger");
            result._points.Add(point);
            if(action.ValueKind==JsonValueKind.Number && action.GetInt32()==0x34 && trigger.GetProperty("recipients").GetString()=="")
                positioned.Add(id,point with { Description="arrival" });
        }
        var arrivalIds=root.GetProperty("arrivals").EnumerateArray().Select(x=>x.GetInt64()).ToArray();
        if(arrivalIds.Distinct().Count()!=arrivalIds.Length || arrivalIds.Length!=positioned.Count || arrivalIds.Any(id=>!positioned.ContainsKey(id)))
            throw new InvalidDataException("Invalid native arrival references");
        result.Arrivals=arrivalIds.Select(id=>positioned[id]).ToArray();
        foreach(var point in result._points)
        {
            var color=point.Description.StartsWith("door")?Colors.Orange:Colors.Cyan;
            var node=new Node3D { Position=point.Position };
            node.AddChild(new MeshInstance3D { Mesh=new SphereMesh { Radius=0.1f,Height=0.2f },
                MaterialOverride=new StandardMaterial3D { AlbedoColor=color,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded } });
            node.AddChild(new Label3D { Text=$"{point.Name}\n{point.Description}",Position=Vector3.Up*0.25f,
                Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,FontSize=24,PixelSize=0.003f });
            result.AddChild(node);
        }
        return result;
    }
}
