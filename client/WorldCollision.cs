using Godot;

// Original processed GameData surfaces with a deliberately separate Godot movement policy.
public partial class WorldCollision : Node3D
{
    public sealed record Face(uint Flags, uint SourceId, Vector3 Normal, Vector3 A, Vector3 B, Vector3 C)
    {
        public Vector3 Center => (A+B+C)/3;
        public float AreaSquared => (B-A).Cross(C-A).LengthSquared();
        public bool Walkable => (Flags & 4) != 0;
    }
    public Face[] Faces { get; private set; } = [];
    public StaticBody3D Body { get; private set; } = null!;

    public static WorldCollision Load(string path, string pvlHash)
    {
        using var reader = new BinaryReader(System.IO.File.OpenRead(path));
        if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)) != "NWCOL001")throw new InvalidDataException("Unknown collision format");
        var source = Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant();
        if(pvlHash.Length != 64 || source != pvlHash)throw new InvalidDataException("Collision file belongs to a different PVL");
        if(reader.ReadBytes(32).Length != 32)throw new InvalidDataException("Truncated OCT provenance");
        var count = reader.ReadUInt32();
        if(count == 0 || count > 2_000_000 || reader.BaseStream.Length != 76L + count * 56L)
            throw new InvalidDataException("Invalid collision triangle count/size");
        var faces = new Face[count];
        var points = new Vector3[count*3];
        var ids = new HashSet<uint>();
        Vector3 Vector()
        {
            var result = new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            if(!result.IsFinite())throw new InvalidDataException("Non-finite collision vector");
            return result;
        }
        for(int i=0;i<count;i++)
        {
            var flags = reader.ReadUInt32();var id = reader.ReadUInt32();
            if((flags & 0x1898) != 0 || !ids.Add(id))throw new InvalidDataException("Unsupported/duplicate static collision surface");
            var face = new Face(flags,id,Vector(),Vector(),Vector(),Vector());
            if(!float.IsFinite(face.AreaSquared) || face.AreaSquared <= 0)throw new InvalidDataException("Degenerate collision surface");
            faces[i] = face;
            points[i*3] = face.A;points[i*3+1] = face.B;points[i*3+2] = face.C;
        }
        var shape = new ConcavePolygonShape3D { BackfaceCollision = true };
        shape.SetFaces(points);
        var resultNode = new WorldCollision { Name="OriginalStaticCollision",Faces=faces };
        resultNode.Body = new StaticBody3D { CollisionLayer=1, CollisionMask=2 };
        resultNode.Body.AddChild(new CollisionShape3D { Shape=shape });
        resultNode.AddChild(resultNode.Body);
        return resultNode;
    }

    public Godot.Collections.Dictionary Ray(Vector3 from, Vector3 to)
    {
        using var query = PhysicsRayQueryParameters3D.Create(from,to,1);
        query.HitBackFaces = true;
        return GetWorld3D().DirectSpaceState.IntersectRay(query);
    }

    public bool IsWalkableHit(Godot.Collections.Dictionary hit)
    {
        if(hit.Count == 0 || hit["collider"].AsGodotObject() != Body)return false;
        var face = hit["face_index"].AsInt32();
        return face >= 0 && face < Faces.Length && Faces[face].Walkable && hit["normal"].AsVector3().Y >= 0.5f;
    }
}

public partial class WorldWalker : CharacterBody3D
{
    public const float Height=1.6f, Radius=0.28f, EyeOffset=0.45f, Speed=3;
    public CapsuleShape3D Capsule { get; } = new() { Height=Height, Radius=Radius };
    public WorldWalker()
    {
        CollisionLayer=2;CollisionMask=1;
        FloorSnapLength=0.3f;FloorMaxAngle=Mathf.DegToRad(60);FloorStopOnSlope=true;
        AddChild(new CollisionShape3D { Shape=Capsule });
    }

    public bool CanStand(Vector3 center)
    {
        using var query = new PhysicsShapeQueryParameters3D { Shape=Capsule, Transform=new Transform3D(Basis.Identity,center),CollisionMask=1,Margin=0.01f };
        return GetWorld3D().DirectSpaceState.IntersectShape(query,1).Count == 0;
    }

    public void Step(double delta,Vector3 direction)
    {
        var velocity = Velocity;
        velocity.X=direction.X*Speed;velocity.Z=direction.Z*Speed;
        velocity.Y=IsOnFloor()?-0.1f:Math.Max(velocity.Y-9.8f*(float)delta,-30);
        Velocity=velocity;MoveAndSlide();
    }
}
