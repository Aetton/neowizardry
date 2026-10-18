using Godot;

// Exercise the production capsule motor against risers, not an artificial ramp.
public partial class WorldWalkerSmoke : Node3D
{
    public override void _Ready()=>Callable.From(Check).CallDeferred();
    private void Box(Vector3 center,Vector3 size,bool triangles=false)
    {
        var body=new StaticBody3D {Position=center,CollisionLayer=1,CollisionMask=2};
        Shape3D shape;
        if(triangles)
        {
            using var mesh=new BoxMesh {Size=size};
            shape=new ConcavePolygonShape3D {Data=mesh.GetFaces(),BackfaceCollision=true};
        }
        else shape=new BoxShape3D {Size=size};
        body.AddChild(new CollisionShape3D {Shape=shape});AddChild(body);
    }
    private async Task Frames(WorldWalker walker,int count,Vector3 direction)
    {
        for(int i=0;i<count;i++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            walker.Step(1.0/60,direction);
        }
    }
    private async Task<WorldWalker> Start(Vector3 feet)
    {
        var walker=new WorldWalker {Position=feet+Vector3.Up*(WorldWalker.Height/2+0.025f)};AddChild(walker);
        await Frames(walker,20,Vector3.Zero);
        if(!walker.IsOnFloor())throw new InvalidDataException("Walking fixture has no floor");
        return walker;
    }
    private async void Check()
    {
        try
        {
            // Isolated lanes keep the blocking tests independent of the staircase.
            Box(new(0,-0.5f,0),new(80,1,30));
            for(int i=0;i<6;i++)Box(new(0,0.15f*(i+1),-1.5f-i*0.6f),new(3,0.3f*(i+1),0.6f),true);
            Box(new(0,0.9f,-6),new(3,1.8f,3));
            Box(new(10,1,-1.5f),new(3,2,0.4f)); // wall / closed door
            Box(new(20,0.25f,-1.5f),new(3,0.5f,0.6f)); // too high
            Box(new(30,0.1f,-1.5f),new(3,0.2f,0.6f));
            Box(new(30,1.85f,-0.9f),new(3,0.2f,3)); // head clearance below lift height
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            var stairs=await Start(new(0,0,0));
            await Frames(stairs,120,Vector3.Forward);
            if(stairs.Position.Z>-5 || Math.Abs(stairs.Position.Y-2.6f)>0.08f || !stairs.IsOnFloor())
                throw new InvalidDataException($"Capsule cannot ascend six triangle steps: {stairs.Position}");
            await Frames(stairs,120,Vector3.Back);
            await Frames(stairs,15,Vector3.Zero);
            if(stairs.Position.Z<-1 || Math.Abs(stairs.Position.Y-0.8f)>0.08f || !stairs.IsOnFloor())
                throw new InvalidDataException($"Capsule cannot descend staircase: {stairs.Position}");
            foreach(float x in new[]{10f,20f,30f})
            {
                var blocked=await Start(new(x,0,0));
                await Frames(blocked,65,Vector3.Forward);
                if(blocked.Position.Z<-1.2f || blocked.Position.Y>0.85f)
                    throw new InvalidDataException($"Capsule crossed wall, tall step or low ceiling in lane {x}: {blocked.Position}");
                blocked.QueueFree();
            }
            GD.Print("PASS: stair walking ascends six triangle steps, descends, blocks walls/closed doors, tall steps and low ceilings");
            GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
