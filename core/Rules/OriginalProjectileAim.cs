namespace NeoWizardry.Core.Rules;

// Coordinates are in the caller's resolved original world units. No Godot scale,
// native pointers or collision geometry enter this pure calculation.
public readonly record struct ProjectilePoint(double X,double Y,double Z)
{
    public static ProjectilePoint operator +(ProjectilePoint a,ProjectilePoint b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static ProjectilePoint operator -(ProjectilePoint a,ProjectilePoint b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static ProjectilePoint operator *(ProjectilePoint a,double scale)=>new(a.X*scale,a.Y*scale,a.Z*scale);
    [System.Text.Json.Serialization.JsonIgnore]
    public double Length=>Math.Sqrt(X*X+Y*Y+Z*Z);
}
public enum ProjectileAimProfile { Standard,Alternate }
public sealed record ProjectileAimInput(ProjectilePoint Source,ProjectilePoint Target,int AttackScore,
    ProjectileAimProfile Profile=ProjectileAimProfile.Standard);
public sealed record ProjectileAimSpread(int DistancePenalty,int AdjustedScore,double NearRadius,double FarRadius,double Radius);
public sealed record ProjectileAimResult(ProjectileAimInput Input,ProjectilePoint AimPoint,double Distance,int AngleDraw,
    int? RadiusDraw,ProjectileAimSpread? Spread)
{
    public int DrawCount=>RadiusDraw is null?1:2;
}

public static class OriginalProjectileAim
{
    // Reconstructs 0x5454C0 in clean double precision, not an x87 emulator. Inputs
    // are resolved launch/target points and score; output is an aim point, not a hit.
    // The caller's native score-9999 bypass and direct-contact path are separate.
    public static ProjectileAimResult Resolve(ProjectileAimInput input,int angleDraw,int? radiusDraw)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidatePoint(input.Source);ValidatePoint(input.Target);
        if(!Enum.IsDefined(input.Profile) || angleDraw is <0 or >=360)throw new ArgumentOutOfRangeException(nameof(angleDraw));
        var delta=input.Target-input.Source;ValidatePoint(delta);
        double distance=delta.Length;
        if(!double.IsFinite(distance))throw new ArgumentOutOfRangeException(nameof(input));
        // Native angle RNG precedes this early return; no d100 is consumed below one unit.
        if(distance<1)
        {
            if(radiusDraw is not null)throw new ArgumentException("Sub-unit aim consumes only the angle draw");
            return new(input,input.Target,distance,angleDraw,null,null);
        }
        if(radiusDraw is not int draw || draw is <0 or >99)throw new ArgumentException("Aim consumes a fresh zero-based d100");
        var spread=Spread(distance,input.AttackScore,input.Profile,draw);
        // a is perpendicular to delta; b=a cross delta fixes native orientation.
        var a=new ProjectilePoint(-delta.X*delta.Y,delta.X*delta.X+delta.Z*delta.Z,-delta.Z*delta.Y);
        var b=new ProjectilePoint(a.Y*delta.Z-a.Z*delta.Y,a.Z*delta.X-a.X*delta.Z,a.X*delta.Y-a.Y*delta.X);
        double angle=angleDraw*(Math.PI/180);
        var direction=Unit(a)*Math.Cos(angle)+Unit(b)*Math.Sin(angle);
        // Exact vertical delta makes both native basis vectors zero. Preserve zero
        // offset while still consuming the angle and radius draws; do not invent a basis.
        var point=input.Target+Unit(direction)*spread.Radius;ValidatePoint(point);
        return new(input,point,distance,angleDraw,draw,spread);
    }

    // Saved flight metadata is checked without re-running spread/trigonometry or drawing RNG.
    public static void ValidateSaved(ProjectileAimResult aim)
    {
        ArgumentNullException.ThrowIfNull(aim);ArgumentNullException.ThrowIfNull(aim.Input);
        ValidatePoint(aim.Input.Source);ValidatePoint(aim.Input.Target);ValidatePoint(aim.AimPoint);
        if(!Enum.IsDefined(aim.Input.Profile) || aim.Input.AttackScore==9999 || aim.AngleDraw is <0 or >=360 ||
            !double.IsFinite(aim.Distance) || aim.Distance<0 ||
            (aim.Distance<1 ? aim.RadiusDraw is not null || aim.Spread is not null :
                aim.RadiusDraw is not (>=0 and <=99) || aim.Spread is null))
            throw new InvalidDataException("Invalid saved projectile aim");
        if(aim.Spread is { } spread && (spread.DistancePenalty<0 || !double.IsFinite(spread.NearRadius) ||
            !double.IsFinite(spread.FarRadius) || !double.IsFinite(spread.Radius) ||
            spread.NearRadius<0 || spread.FarRadius<0 || spread.Radius<0))
            throw new InvalidDataException("Invalid saved projectile spread");
    }

    public static ProjectileAimSpread Spread(double distance,int attackScore,ProjectileAimProfile profile,int radiusDraw)
    {
        if(!double.IsFinite(distance) || distance<1 || !Enum.IsDefined(profile) || radiusDraw is <0 or >99)
            throw new ArgumentOutOfRangeException(nameof(distance));
        int distancePenalty=checked((int)Math.Truncate(distance*0.002));
        int score=checked(checked(100-distancePenalty)*attackScore)/100;
        double nearSlope=profile==ProjectileAimProfile.Standard?0.08:Math.Min(500/distance,1.75);
        double farSlope=profile==ProjectileAimProfile.Standard?0.2:1.75;
        double near=nearSlope*distance;
        double far=score>=300?near:score>0?near+(300-score)*(1.0/300)*(farSlope-nearSlope)*distance:farSlope*distance;
        int roll=radiusDraw+1;
        double radius;
        if(roll<=Math.Min(95,score))radius=(double)roll/score*near;
        else if(score>0)radius=near+(double)(roll-score)/roll*(far-near);
        else
        {
            int shifted=checked(roll+score);
            radius=shifted<=0?far:near+(100-shifted)*0.01*(far-near);
        }
        if(!double.IsFinite(near) || !double.IsFinite(far) || !double.IsFinite(radius))
            throw new ArgumentOutOfRangeException(nameof(distance));
        return new(distancePenalty,score,near,far,radius);
    }

    private static ProjectilePoint Unit(ProjectilePoint point)
    {
        double length=point.Length;
        if(!double.IsFinite(length))throw new ArgumentOutOfRangeException(nameof(point));
        return length==0?default:point*(1/length);
    }
    private static void ValidatePoint(ProjectilePoint point)
    {
        if(!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
            throw new ArgumentOutOfRangeException(nameof(point));
    }
}
