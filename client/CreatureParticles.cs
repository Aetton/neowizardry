using Godot;
using System.Text.Json;

// Cosmetic simulation. It owns its random stream and never accesses GameState.
internal sealed class CreatureParticles
{
    private sealed class Particle
    {
        public required MeshInstance3D Mesh;
        public required StandardMaterial3D Material;
        public Vector3 Position,Velocity;
        public double Age;
        public float FlutterAngle;
    }
    private readonly Node3D _owner;
    private readonly JsonElement _data;
    private readonly Texture2D[] _images;
    private readonly Vector3[] _anchors;
    private readonly List<Particle> _particles=[];
    private readonly Basis _rotation;
    private readonly Random _random=new(0x57495a);
    private double _clock,_emissionClock;
    private int _emitted;
    private readonly float _scale;
    private readonly QuadMesh _quad;
    public bool Enabled {get;set;}
    public int LiveCount=>_particles.Count;
    public int Emitted=>_emitted;
    private int I(string name)=>_data.GetProperty(name).GetInt32();
    private float F(string name)=>_data.GetProperty(name).GetSingle();
    private bool B(string name)=>_data.GetProperty(name).GetBoolean();
    private static Vector3 V(JsonElement row)=>new(row[0].GetSingle(),row[1].GetSingle(),row[2].GetSingle());
    private Vector3 Native(Vector3 value)=>new(value.X*_scale,value.Y*_scale,-value.Z*_scale);
    private float Unit()=>(float)_random.NextDouble();
    public CreatureParticles(Node3D owner,JsonElement data,IReadOnlyDictionary<string,Texture2D> images,int frames)
    {
        _owner=owner;_data=data.Clone();
        _anchors=data.GetProperty("anchors").EnumerateArray().Select(V).ToArray();
        _images=data.GetProperty("images").EnumerateArray().Select(x=>images[x.GetString()!]).ToArray();
        _scale=0.0013f*F("scale");
        var axes=data.GetProperty("basis").EnumerateArray().Select(V).ToArray();
        _rotation=new Basis(axes[0],axes[1],axes[2]);
        if(_anchors.Length!=frames || _anchors.Any(v=>!v.IsFinite()) || _images.Length is <1 or >4096 || I("capacity") is <0 or >4096 ||
            I("interval_ms")<1 || F("size")<0 || F("sprite_fps")<=0 || !float.IsFinite(_scale))
            throw new InvalidDataException("Invalid creature particle metadata");
        float size=Math.Max(0.00001f,F("size")*_scale);
        _quad=new QuadMesh {Size=new Vector2(size,size)};
    }
    public void Begin(){_emitted=0;_emissionClock=0;Enabled=true;}
    public void Stop(){Enabled=false;}
    private bool Window(int frame)
    {
        int first=I("start_frame"),last=I("end_frame");
        return first==-1 || last==-1 || first==last || frame>=first && frame<last;
    }
    public void Step(double delta,int frame,int sourceFrame,bool selected)
    {
        _clock+=delta;_emissionClock+=delta;
        var transform=_owner.GlobalTransform;
        Vector3 anchor=transform*_anchors[Math.Clamp(frame,0,_anchors.Length-1)];
        Basis basis=transform.Basis.Orthonormalized()*_rotation;
        var accel=Native(V(_data.GetProperty("acceleration"))*500);
        foreach(var particle in _particles.ToArray())
        {
            particle.Age+=delta;
            double life=I("lifetime_ms")/1000.0;
            if(I("expiry")==1)life=_images.Length>1?_images.Length/F("sprite_fps"):life==0?1:life;
            particle.Velocity+=accel*(float)delta;particle.Position+=particle.Velocity*(float)delta;
            var relative=basis.Inverse()*(particle.Position-anchor);
            var origin=V(_data.GetProperty("bounds_origin"));origin.X=0;
            var extent=Native(V(_data.GetProperty("bounds_extent"))*250).Abs();
            var center=Native(origin*500);
            bool outside=I("bounds_mode")==2 && F("bounds_radius")>0 && relative.DistanceTo(center)>F("bounds_radius")*500*_scale ||
                I("bounds_mode")==1 && ((relative-center).Abs().X>extent.X || (relative-center).Abs().Y>extent.Y || (relative-center).Abs().Z>extent.Z);
            if(particle.Age>life || outside){particle.Mesh.Free();particle.Material.Dispose();_particles.Remove(particle);continue;}
            var position=particle.Position;
            if(B("flutter") && particle.Velocity.Y<0)
            {
                float period=F("flutter_period_ms")/1000;
                float flutter=(period>0?MathF.Sin((float)(_clock%period)/period*MathF.Tau):0)*F("flutter_amplitude")*_scale;
                float offset=Math.Min(1000*_scale,-particle.Velocity.Y)*0.001f/_scale*flutter;
                position+=new Vector3(MathF.Cos(particle.FlutterAngle)*offset,0,MathF.Sin(particle.FlutterAngle)*offset);
            }
            particle.Mesh.GlobalPosition=position;
            int image=Math.Min(_images.Length-1,(int)(particle.Age*F("sprite_fps")));
            particle.Material.AlbedoTexture=_images[image];
            float alpha=I("expiry")==0?(float)Math.Clamp((life-particle.Age)/0.5,0,1):1;
            particle.Material.AlbedoColor=new Color(1,1,1,alpha*F("opacity"));
        }
        if(!selected || !Enabled || !Window(sourceFrame) || I("emission_mode")==0){_emissionClock=0;return;}
        double interval=I("interval_ms")/1000.0;
        int attempts=I("emission_mode")==1?(_emissionClock>=interval?1:0):(int)Math.Max(0,Math.Ceiling(_emissionClock/interval)-1);
        // Capacity and native emission limits bound work even after a long pause.
        attempts=Math.Min(attempts,I("capacity")+1);
        for(int i=0;i<attempts;i++)
        {
            if(I("limit")!=0 && _emitted>=unchecked((uint)I("limit"))){_emissionClock=0;break;}
            if(_particles.Count>=I("capacity"))
            {
                if(V(_data.GetProperty("bounds_origin")).X!=0 && _particles.Count>0){_particles[0].Mesh.Free();_particles[0].Material.Dispose();_particles.RemoveAt(0);}
                else {_emissionClock=0;break;}
            }
            float speed=I("speed_mode")==0?0:I("speed_mode")==1?F("speed"):F("speed_min")+Unit()*(F("speed_max")-F("speed_min"));
            Vector3 direction=Vector3.Zero;
            if(I("direction_mode")==2)direction=basis.Z;
            if(I("direction_mode")==1)direction=V(_data.GetProperty("fixed_direction"));
            if(I("direction_mode")==3)
            {
                var cone=_data.GetProperty("cone");
                direction=basis*(new Basis(Vector3.Up,Mathf.DegToRad((Unit()-0.5f)*cone[0].GetSingle()))*
                    (new Basis(Vector3.Right,Mathf.DegToRad((Unit()-0.5f)*cone[1].GetSingle()))*Vector3.Back));
            }
            if(I("direction_mode")==4)direction=new Vector3(Unit()-0.5f,Unit()-0.5f,Unit()-0.5f).Normalized();
            var velocity=direction*speed*500*_scale;
            Vector3 spread=V(_data.GetProperty("spread"));
            var offset=I("emission_mode")==2?Native(new Vector3((Unit()-0.5f)*spread.X*500,(Unit()-0.5f)*spread.Y*500,Unit()*spread.Z*500)):Vector3.Zero;
            var material=new StandardMaterial3D {ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode=BaseMaterial3D.BillboardModeEnum.Enabled,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode=BaseMaterial3D.CullModeEnum.Disabled,AlbedoTexture=_images[0],AlbedoColor=new Color(1,1,1,F("opacity"))};
            var mesh=new MeshInstance3D {Name="NativeParticle",Mesh=_quad,MaterialOverride=material,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            _owner.AddChild(mesh);
            var particle=new Particle {Mesh=mesh,Material=material,Position=anchor+basis*offset,Velocity=velocity,FlutterAngle=Unit()*MathF.Tau};
            mesh.GlobalPosition=particle.Position;_particles.Add(particle);_emitted++;
            _emissionClock=I("emission_mode")==1?0:_emissionClock-interval;
        }
    }
    public void Check(int first,int frames)
    {
        Clear();Begin();
        int start=I("start_frame"),end=I("end_frame");
        int source=start!=-1 && end!=-1 && start!=end?Math.Max(first,start):first;
        if(source>=first+frames || !Window(source))return;
        int frame=source-first;
        double interval=I("interval_ms")/1000.0;
        Step(interval*2.1,frame,source,true);
        if(I("capacity")>0 && I("emission_mode")>0 && Emitted==0)
            throw new InvalidDataException("Native particle emitter failed to emit");
        Step(0.001,frame,source,true);
        foreach(var particle in _particles)
            if(!particle.Position.IsFinite() || !particle.Velocity.IsFinite() || !particle.Mesh.GlobalPosition.IsFinite() ||
                !_images.Contains(particle.Material.AlbedoTexture) || particle.Material.AlbedoColor.A is <0 or >1)
                throw new InvalidDataException("Invalid native particle simulation/render state");
        if(LiveCount>I("capacity") || I("limit")>0 && Emitted>I("limit"))
            throw new InvalidDataException("Native particle capacity/emission limit violated");
        Stop();Step(Math.Max(I("lifetime_ms")/1000.0,_images.Length/F("sprite_fps"))+1,frame,source,false);
        if(LiveCount!=0)throw new InvalidDataException("Native particles did not expire after emission stopped");
        Clear();
    }
    public void Clear(){foreach(var particle in _particles){particle.Mesh.Free();particle.Material.Dispose();}_particles.Clear();_emissionClock=0;}
}
