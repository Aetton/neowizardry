using Godot;
using System.Security.Cryptography;
using System.Text.Json;

// Native frame geometry, with an explicitly authored interaction/closing policy.
public partial class WorldDoors : Node3D
{
    public List<WorldDoor> Doors { get; } = [];
    public int FrameCount => Doors.Sum(d=>d.Frames.Length);
    private WorldDoor? _inspection;
    private int _inspectionStart,_inspectionIndex=-1;
    private bool _returning;
    public bool IsInspecting => _inspection is not null;
    public int TriangleCount { get; private set; }
    public int InteractiveCount => Doors.Count(d=>d.BlockedReason.Length==0);

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if(node is MeshInstance3D mesh)yield return mesh;
        foreach(var child in node.GetChildren())foreach(var item in Meshes(child))yield return item;
    }

    public static WorldDoors Load(string path,string meshPath,string pvlHash,Action<GltfState> textures)
    {
        using var manifest=JsonDocument.Parse(System.IO.File.ReadAllText(path));
        var root=manifest.RootElement;
        if(root.GetProperty("schema").GetInt32()!=1 || pvlHash.Length!=64 || root.GetProperty("pvl_sha256").GetString()!=pvlHash)
            throw new InvalidDataException("Door manifest provenance mismatch");
        using(var stream=System.IO.File.OpenRead(meshPath))
            if(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()!=root.GetProperty("mesh_sha256").GetString())
                throw new InvalidDataException("Door frame GLB hash mismatch");
        using var document=new GltfDocument();using var state=new GltfState();
        if(document.AppendFromFile(meshPath,state)!=Error.Ok)throw new InvalidDataException("Door frame import failed");
        using var glbMetadata=JsonDocument.Parse(Godot.Json.Stringify(state.Json));
        if(glbMetadata.RootElement.GetProperty("extras").GetProperty("neoSourcePVL").GetString()!=pvlHash)
            throw new InvalidDataException("Door frame source mismatch");
        var scene=document.GenerateScene(state) ?? throw new InvalidDataException("Door scene missing");
        var result=new WorldDoors { Name="NativeDoorPreview" };
        result.AddChild(scene);
        textures(state);
        var meshes=Meshes(scene).ToDictionary(x=>x.Name.ToString());
        var used=new HashSet<int>();var ids=new HashSet<long>();
        var records=root.GetProperty("doors");
        if(records.GetArrayLength()>1024)throw new InvalidDataException("Too many door records");
        foreach(var record in records.EnumerateArray())
        {
            long id=record.GetProperty("id").GetInt64();
            if(id<0 || !ids.Add(id))throw new InvalidDataException("Duplicate door source ID");
            var indices=record.GetProperty("frames").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(indices.Length is <1 or >255)throw new InvalidDataException("Unsupported door frame count");
            var frames=new MeshInstance3D[indices.Length];
            for(int i=0;i<indices.Length;i++)
            {
                if(!used.Add(indices[i]) || !meshes.TryGetValue($"PVL_mesh_{indices[i]}",out var frame))
                    throw new InvalidDataException("Missing/duplicate door frame mesh");
                var expected=record.GetProperty("bounds")[i];
                Vector3 Point(JsonElement row)=>new(row[0].GetSingle(),row[1].GetSingle(),row[2].GetSingle());
                var low=Point(expected[0]);var high=Point(expected[1]);var box=WorldDoor.RenderBounds(frame);
                if(!low.IsFinite() || !high.IsFinite() || (box.Position-low).Length()>0.002f || (box.End-high).Length()>0.002f)
                    throw new InvalidDataException($"Native door frame bounds mismatch: expected {low}..{high}, got {box.Position}..{box.End}");
                frames[i]=frame;
                result.TriangleCount+=frame.Mesh.GetFaces().Length/3;
            }
            var fps=record.GetProperty("fps").GetSingle();
            if(!float.IsFinite(fps) || fps<=0 || fps>240)throw new InvalidDataException("Invalid door animation speed");
            var door=new WorldDoor { Name=$"Door_{id}",SourceId=id,DisplayName=record.GetProperty("name").GetString()??"Door",
                Fps=fps,BlockedReason=record.GetProperty("blocked_reason").GetString()??throw new InvalidDataException("Door eligibility missing") };
            result.AddChild(door);
            foreach(var frame in frames) { frame.GetParent().RemoveChild(frame);door.AddChild(frame); }
            door.Initialize(frames,(record.GetProperty("flags").GetUInt32()&1)!=0,record.GetProperty("open").GetBoolean());
            result.Doors.Add(door);
        }
        if(used.Count!=meshes.Count)throw new InvalidDataException("Unowned door frame geometry");
        return result;
    }

    public void CancelInspection()
    {
        if(_inspection is not null)_inspection.SetFrame(_inspectionStart);
        _inspection=null;
    }

    private void BeginInspection(WorldDoor door)
    {
        CancelInspection();_inspection=door;_inspectionStart=door.CurrentFrame;_returning=false;
        door.AnimateTo(_inspectionStart==0?door.Frames.Length-1:0);
    }

    public string PreviewNext(Camera3D view)
    {
        CancelInspection();
        if(Doors.Count==0)return "No door models loaded";
        if(Doors.Any(d=>d.CurrentFrame!=d.TargetFrame))return "Wait for the active door animation";
        _inspectionIndex=(_inspectionIndex+1)%Doors.Count;
        var door=Doors[_inspectionIndex];BeginInspection(door);
        var bounds=WorldDoor.RenderBounds(door.Frames[door.CurrentFrame]);var center=bounds.GetCenter();
        float span=Math.Max(bounds.Size.Length(),1);
        view.GlobalPosition=center+new Vector3(0.8f,0.45f,0.8f)*span;view.LookAt(center);
        return $"ANIMATION INSPECTION {_inspectionIndex+1}/{Doors.Count}: {door.DisplayName} · source pose restores before walking";
    }

    public string Use(Camera3D view)
    {
        if(IsInspecting)return "Animation inspection active; F restores source pose before walking";
        using var query=PhysicsRayQueryParameters3D.Create(view.GlobalPosition,view.GlobalPosition-view.GlobalBasis.Z*3,5);
        query.HitBackFaces=true;
        var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);
        if(hit.Count==0 || hit["collider"].AsGodotObject() is not StaticBody3D { } body || body.GetParent() is not WorldDoor door)
            return "Aim at a door within reach";
        return door.RequestUse();
    }

    public string? Step(double delta,WorldWalker? player)
    {
        string? message=null;
        foreach(var door in Doors)
        {
            bool moving=door.CurrentFrame!=door.TargetFrame;
            if(!door.Advance(delta,player))message="Door stopped: doorway occupied";
            else if(moving && door.CurrentFrame==door.TargetFrame)message=$"{door.DisplayName}: {(door.CurrentFrame==0?"closed":"open")}";
        }
        if(_inspection is not null && _inspection.CurrentFrame==_inspection.TargetFrame)
        {
            if(_returning) { CancelInspection();message="Animation inspection finished; original pose restored"; }
            else { _returning=true;_inspection.AnimateTo(_inspectionStart); }
        }
        return message;
    }

    public void CheckPhysics(WorldWalker walker)
    {
        var physical=Doors.Where(d=>(d.Body.CollisionLayer&1)!=0).ToArray();
        bool blocked=false;
        foreach(var door in physical)
        {
            var faces=door.Shapes[door.CurrentFrame].GetFaces();
            for(int i=0;i<faces.Length && !blocked;i+=3)
            {
                var normal=(faces[i+1]-faces[i]).Cross(faces[i+2]-faces[i]).Normalized();
                if(Math.Abs(normal.Y)>0.1f)continue;
                var point=(faces[i]+faces[i+1]+faces[i+2])/3;
                foreach(int side in new[]{1,-1})
                {
                    var direction=normal*side;var center=point+direction*(WorldWalker.Radius+0.15f);
                    if(!walker.CanStand(center))continue;
                    using var collision=new KinematicCollision3D();
                    if(walker.TestMove(new Transform3D(Basis.Identity,center),-direction*(WorldWalker.Radius+0.4f),collision) && collision.GetCollider()==door.Body)
                    { blocked=true;break; }
                }
            }
            if(blocked)break;
        }
        if(physical.Length>0 && !blocked)throw new InvalidDataException("Native physical door did not block capsule sweep");
        GD.Print($"PASS: door physics {physical.Length} source-collidable models; capsule blocking verified where applicable");
    }

    public void Check(int models,int frames,int triangles,int interactive,WorldWalker? player)
    {
        if(models!=Doors.Count || frames!=FrameCount || triangles!=TriangleCount || interactive!=InteractiveCount)
            throw new InvalidDataException("Door geometry counts differ from export report");
        foreach(var door in Doors)
        {
            int initial=door.CurrentFrame;
            for(int i=0;i<door.Frames.Length;i++)
            {
                door.SetFrame(i);
                if(door.Frames.Count(f=>f.Visible)!=1 || door.Shape.Shape!=door.Shapes[i])throw new InvalidDataException("Door frame/collision binding failed");
            }
            door.SetFrame(initial);
            if(door.BlockedReason.Length>0)
            {
                door.RequestUse();
                if(door.TargetFrame!=initial)throw new InvalidDataException("Restricted door accepted interaction");
            }
            else
            {
                if(player is not null && door.Frames.Length>1)
                {
                    var previous=player.GlobalPosition;
                    int next=initial==0?1:initial-1;
                    player.GlobalPosition=WorldDoor.RenderBounds(door.Frames[next]).GetCenter();
                    door.RequestUse();
                    bool accepted=door.Advance(1.0/door.Fps,player);
                    player.GlobalPosition=previous;
                    if((door.Body.CollisionLayer&1)!=0 && (accepted || door.CurrentFrame!=initial))throw new InvalidDataException("Occupied door did not stop");
                    door.SetFrame(initial);
                }
                door.RequestUse();
                for(int i=0;i<256;i++)door.Advance(1.0/door.Fps,null);
                if(door.CurrentFrame==initial && door.Frames.Length>1)throw new InvalidDataException("Door did not animate");
                door.RequestUse();
                for(int i=0;i<256;i++)door.Advance(1.0/door.Fps,null);
                if(door.CurrentFrame!=initial)throw new InvalidDataException("Door did not return to its endpoint");
            }
        }
        foreach(var door in Doors)
        {
            int start=door.CurrentFrame;BeginInspection(door);
            for(int i=0;i<512 && IsInspecting;i++)Step(1.0/door.Fps,null);
            if(IsInspecting || door.CurrentFrame!=start)throw new InvalidDataException("Door inspection did not restore source pose");
            BeginInspection(door);Step(1.0/door.Fps,null);CancelInspection();
            if(door.CurrentFrame!=start)throw new InvalidDataException("Cancelled door inspection changed pose");
        }
        GD.Print($"PASS: doors {Doors.Count} models, {FrameCount} frames, {TriangleCount} triangles, {InteractiveCount} eligible; restrictions and reversible animation verified");
    }
}

public partial class WorldDoor : Node3D
{
    public long SourceId { get; set; }
    public string DisplayName { get; set; }="Door";
    public string BlockedReason { get; set; }="";
    public float Fps { get; set; }
    public MeshInstance3D[] Frames { get; private set; }=[];
    public ConcavePolygonShape3D[] Shapes { get; private set; }=[];
    public CollisionShape3D Shape { get; private set; }=null!;
    public StaticBody3D Body { get; private set; }=null!;
    public int CurrentFrame { get; private set; }
    public int TargetFrame { get; private set; }
    private double _phase;

    public static Aabb RenderBounds(MeshInstance3D frame)
    {
        var vertices=frame.Mesh.GetFaces();
        if(vertices.Length==0)throw new InvalidDataException("Empty door frame geometry");
        var box=new Aabb(vertices[0],Vector3.Zero);
        foreach(var vertex in vertices)box=box.Expand(vertex);
        return box;
    }

    public void Initialize(MeshInstance3D[] frames,bool collidable,bool open)
    {
        Frames=frames;Shapes=new ConcavePolygonShape3D[frames.Length];
        for(int i=0;i<frames.Length;i++)
        {
            var faces=frames[i].Mesh.GetFaces();var valid=new List<Vector3>();
            for(int k=0;k<faces.Length;k+=3)
            {
                if(!faces[k].IsFinite() || !faces[k+1].IsFinite() || !faces[k+2].IsFinite())throw new InvalidDataException("Non-finite door geometry");
                if((faces[k+1]-faces[k]).Cross(faces[k+2]-faces[k]).LengthSquared()>0)valid.AddRange(faces[k..(k+3)]);
            }
            if(valid.Count==0)throw new InvalidDataException("Door frame has no physical triangles");
            Shapes[i]=new ConcavePolygonShape3D { BackfaceCollision=true };Shapes[i].SetFaces(valid.ToArray());
            frames[i].Visible=false;
        }
        Body=new StaticBody3D { CollisionLayer=(uint)(collidable?5:4),CollisionMask=2 };
        Shape=new CollisionShape3D();Body.AddChild(Shape);AddChild(Body);
        SetFrame(open?frames.Length-1:0);
    }

    public void SetFrame(int index)
    {
        foreach(var frame in Frames)frame.Visible=false;
        CurrentFrame=TargetFrame=index;Frames[index].Visible=true;Shape.Shape=Shapes[index];_phase=0;
    }

    public void AnimateTo(int frame) { TargetFrame=frame;_phase=0; }

    public string RequestUse()
    {
        if(BlockedReason.Length>0)return $"{DisplayName}: {BlockedReason}";
        if(CurrentFrame!=TargetFrame)return $"{DisplayName}: moving";
        TargetFrame=CurrentFrame==0?Frames.Length-1:0;_phase=0;
        return $"{DisplayName}: {(TargetFrame==0?"closing":"opening")}";
    }

    public bool Advance(double delta,WorldWalker? player)
    {
        if(CurrentFrame==TargetFrame)return true;
        _phase+=delta*Fps;
        while(_phase+1e-6>=1 && CurrentFrame!=TargetFrame)
        {
            int next=CurrentFrame+Math.Sign(TargetFrame-CurrentFrame);
            if(player is not null && (Body.CollisionLayer&1)!=0)
            {
                var bounds=RenderBounds(Frames[next]).Grow(0.05f);
                var size=new Vector3(WorldWalker.Radius*2,WorldWalker.Height,WorldWalker.Radius*2);
                var capsuleBounds=new Aabb(player.GlobalPosition-size/2,size);
                if(bounds.Intersects(capsuleBounds)) { TargetFrame=CurrentFrame;_phase=0;return false; }
            }
            Frames[CurrentFrame].Visible=false;CurrentFrame=next;Frames[next].Visible=true;Shape.Shape=Shapes[next];_phase-=1;
        }
        return true;
    }
}
