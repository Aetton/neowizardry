using System.Text.Json;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class ProjectileAimChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Near(double actual,double expected,string name)=>Check(Math.Abs(actual-expected)<=1e-9*Math.Max(1,Math.Abs(expected)),name);
        void Point(ProjectilePoint actual,ProjectilePoint expected,string name)=>Check((actual-expected).Length<1e-8,name);
        void Reject(Action action,string name){bool threw=false;try{action();}catch(Exception){threw=true;}Check(threw,name);}
        var axis=new ProjectileAimInput(default,new(1000,0,0),100);
        var aim=OriginalProjectileAim.Resolve(axis,0,48);
        Check(aim.DrawCount==2 && aim.Spread!.DistancePenalty==2 && aim.Spread.AdjustedScore==98,"Distance reduces raw aim score before radius draw");
        Near(aim.Spread!.NearRadius,80,"Standard near radius is eight percent of distance");
        Near(aim.Spread.FarRadius,160.8,"Standard far envelope interpolates by adjusted score");
        Near(aim.Spread.Radius,40,"Inner radius uses one-based draw divided by adjusted score");
        Point(aim.AimPoint,new(1000,40,0),"Zero angle follows native positive Y basis");
        Point(OriginalProjectileAim.Resolve(axis,90,48).AimPoint,new(1000,0,-40),"Ninety degrees follows native cross-product orientation");
        Point(OriginalProjectileAim.Resolve(axis,180,48).AimPoint,new(1000,-40,0),"Opposite angle reverses Y deviation");
        Point(OriginalProjectileAim.Resolve(axis,270,48).AimPoint,new(1000,0,40),"Three-quarter angle reverses Z deviation");
        Point(OriginalProjectileAim.Resolve(axis with {Target=new(0,0,1000)},90,48).AimPoint,new(40,0,1000),"Z-axis target keeps native perpendicular basis orientation");
        var translated=axis with {Source=new(10,20,30),Target=new(1010,20,30)};
        Point(OriginalProjectileAim.Resolve(translated,90,48).AimPoint,aim.Input.Target+new ProjectilePoint(10,20,-10),"Translation preserves relative deviation");
        var below=OriginalProjectileAim.Spread(499.999,100,ProjectileAimProfile.Standard,0);
        var edge=OriginalProjectileAim.Spread(500,100,ProjectileAimProfile.Standard,0);
        Check(below.DistancePenalty==0 && below.AdjustedScore==100 && edge.DistancePenalty==1 && edge.AdjustedScore==99,"Distance-penalty integer boundary at five hundred source units");
        Check(OriginalProjectileAim.Spread(50500,50,ProjectileAimProfile.Standard,0).AdjustedScore==0 &&
            OriginalProjectileAim.Spread(50500,150,ProjectileAimProfile.Standard,0).AdjustedScore==-1,"Negative distance-adjusted score truncates toward zero rather than flooring or clamping");
        var zeroLow=OriginalProjectileAim.Spread(1000,0,ProjectileAimProfile.Standard,0);
        var zeroHigh=OriginalProjectileAim.Spread(1000,0,ProjectileAimProfile.Standard,99);
        Near(zeroLow.Radius,198.8,"Nonpositive score branch uses shifted draw without division by score");
        Near(zeroHigh.Radius,80,"Zero score with draw one hundred reaches near radius");
        Near(OriginalProjectileAim.Spread(100,-100,ProjectileAimProfile.Standard,99).Radius,20,"Nonpositive shifted draw reaches far envelope");
        Near(OriginalProjectileAim.Spread(100,-99,ProjectileAimProfile.Standard,99).Radius,19.88,"Shifted draw immediately above zero interpolates toward far radius");
        Near(OriginalProjectileAim.Spread(100,95,ProjectileAimProfile.Standard,94).Radius,8,"Inner branch includes roll ninety-five at threshold equality");
        double ninetySix=OriginalProjectileAim.Spread(100,95,ProjectileAimProfile.Standard,95).Radius;
        Near(ninetySix,8+(1.0/96)*8.2,"Next roll uses outer interpolation rather than an automatic miss");
        Near(OriginalProjectileAim.Spread(1,300,ProjectileAimProfile.Standard,94).Radius,0.08*95/300,"High scores still use inner ninety-five-roll cap");
        Near(OriginalProjectileAim.Spread(1,300,ProjectileAimProfile.Standard,95).Radius,0.08,"Roll ninety-six uses equal near/far envelope for score three hundred");
        var high=OriginalProjectileAim.Spread(1,301,ProjectileAimProfile.Standard,99);
        Near(high.FarRadius,high.NearRadius,"Far envelope saturates at adjusted score three hundred");
        var alt=OriginalProjectileAim.Resolve(axis with {Profile=ProjectileAimProfile.Alternate},0,48);
        Near(alt.Spread!.NearRadius,500,"Alternate profile near radius caps at five hundred for distant target");
        Near(alt.Spread.FarRadius,1341.6666666666667,"Alternate profile far envelope uses slope one point seven five");
        Point(alt.AimPoint,new(1000,250,0),"Alternate profile generates its own resolved aim point");
        var altClose=OriginalProjectileAim.Spread(100,100,ProjectileAimProfile.Alternate,49);
        Near(altClose.NearRadius,175,"Alternate short-distance near slope is capped at one point seven five");
        Near(altClose.FarRadius,175,"Alternate short-distance envelope can have equal bounds");
        Near(altClose.Radius,87.5,"Equal envelope still retains inner draw scaling");
        var signed=OriginalProjectileAim.Spread(10000,250,ProjectileAimProfile.Alternate,95);
        Check(signed.AdjustedScore==200 && signed.Radius<0,"Alternate outer branch may produce signed deviation and must not clamp it");
        Near(signed.Radius,-5638.888888888889,"Signed deviation preserves original positive-score outer interpolation");
        var flipped=OriginalProjectileAim.Resolve(axis with {Target=new(10000,0,0),AttackScore=250,Profile=ProjectileAimProfile.Alternate},0,95);
        Point(flipped.AimPoint,new(10000,signed.Radius,0),"Negative deviation reverses direction instead of becoming an invented miss");
        var sub=OriginalProjectileAim.Resolve(axis with {Target=new(0.5,0,0),AttackScore=int.MaxValue},359,null);
        Check(sub.DrawCount==1 && sub.RadiusDraw is null && sub.Spread is null,"Sub-unit distance consumes angle before early return, with no score/d100 computation");
        Point(sub.AimPoint,new(0.5,0,0),"Sub-unit distance keeps original target");
        Point(OriginalProjectileAim.Resolve(axis with {Target=default},1,null).AimPoint,default,"Coincident source and target still permit angle-only path");
        Check(OriginalProjectileAim.Resolve(axis with {Target=new(1,0,0)},0,0).DrawCount==2,"Exactly one unit enters full spread path");
        var vertical=OriginalProjectileAim.Resolve(axis with {Target=new(0,1000,0)},90,48);
        Check(vertical.DrawCount==2 && vertical.Spread!.Radius>0,"Vertical basis degeneracy still consumes both native draws");
        Point(vertical.AimPoint,new(0,1000,0),"Exact vertical vector preserves native zero-basis result without invented sideways fallback");
        var oblique=new ProjectileAimInput(new(1,2,3),new(4,6,15),75);
        bool geometry=true;var sum=default(ProjectilePoint);
        for(int angle=0;angle<360;angle++)
        {
            var result=OriginalProjectileAim.Resolve(oblique,angle,50);
            var delta=oblique.Target-oblique.Source;var offset=result.AimPoint-oblique.Target;
            geometry &= Math.Abs(offset.X*delta.X+offset.Y*delta.Y+offset.Z*delta.Z)<1e-9 &&
                Math.Abs(offset.Length-result.Spread!.Radius)<1e-9;
            sum+=offset;
        }
        Check(geometry,"All integer angles preserve transverse plane and selected radial distance");
        Check(sum.Length<1e-9,"Full angle sweep is centered on intended target");
        foreach(int angle in new[]{-1,360})Reject(()=>OriginalProjectileAim.Resolve(axis,angle,0),"Angle draw exclusive range");
        foreach(int draw in new[]{-1,100})Reject(()=>OriginalProjectileAim.Resolve(axis,0,draw),"Radius draw exclusive range");
        Reject(()=>OriginalProjectileAim.Resolve(axis,0,null),"Full-distance aim requires its d100");
        Reject(()=>OriginalProjectileAim.Resolve(axis with {Target=new(0.5,0,0)},0,0),"Sub-unit path rejects unused d100");
        Reject(()=>OriginalProjectileAim.Resolve(axis with {Profile=(ProjectileAimProfile)99},0,0),"Unknown spread profile rejected");
        foreach(double bad in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Reject(()=>OriginalProjectileAim.Resolve(axis with {Source=new(bad,0,0)},0,0),"Nonfinite origin rejected");
            Reject(()=>OriginalProjectileAim.Spread(bad,100,ProjectileAimProfile.Standard,0),"Nonfinite spread distance rejected");
        }
        Reject(()=>OriginalProjectileAim.Spread(0.99,100,ProjectileAimProfile.Standard,0),"Scalar full-spread helper rejects sub-unit input");
        Reject(()=>OriginalProjectileAim.Spread(1000,int.MaxValue,ProjectileAimProfile.Standard,0),"Aim multiplier overflow rejected instead of signed wrap");
        Reject(()=>OriginalProjectileAim.Spread(double.MaxValue,100,ProjectileAimProfile.Standard,0),"Distance integer conversion overflow rejected");
        Reject(()=>OriginalProjectileAim.Resolve(axis with {Target=new(1e200,0,0)},0,0),"Nonfinite derived distance rejected");

        var rules=new Dictionary<int,ItemInventoryRule>{{1,new(1,100)}};
        var bag=new ItemInventoryState(1,1,[new("ammo",1,2,true,null,false,new("hero"))]);
        var series=ProjectileSeries.Create("aim",bag,"ammo",new(1,ProjectileCostKind.Quantity),2,rules);
        string before=JsonSerializer.Serialize(series);
        var flight=OriginalProjectileAttack.Launch(series,axis,90,48,rules);
        Check(flight.State.Pending!.Ordinal==1 && flight.State.Inventory.Items.Single().Quantity==2 && flight.State.Completed==0,"Aimed launch records pending projectile without resource cost");
        Point(flight.Aim.AimPoint,new(1000,0,-40),"Aimed launch includes resolved point for caller-owned delivery");
        Reject(()=>OriginalProjectileAttack.Launch(flight.State,axis,0,0,rules),"Second aimed launch cannot steal pending source");
        Reject(()=>OriginalProjectileAttack.Launch(series,axis,0,null,rules),"Invalid aim plan cannot commit partial pending state");
        Reject(()=>OriginalProjectileAttack.Launch(series,axis with {AttackScore=9999},0,0,rules),"Native no-spread sentinel cannot silently enter ordinary perturbation launch");
        Check(JsonSerializer.Serialize(series)==before,"Failed or successful launch leaves input state unchanged");
        string saved=JsonSerializer.Serialize(flight);var restored=JsonSerializer.Deserialize<AimedProjectileLaunch>(saved)!;
        Check(JsonSerializer.Serialize(restored)==saved,"Whole aimed flight roundtrips including vector coordinates and draw metadata");
        var strike=new ProjectileStrikeInput(OriginalAttackMode.Shoot,ProjectileContactKind.Contact,100,new(0,1,6));
        var first=OriginalProjectileAttack.Complete(restored.State,"aim",1,strike,99,[5],true,true,rules);
        Check(first.Strike.Damage!.Damage==6 && first.State.Inventory.Items.Single().Quantity==1,"Caller-resolved actor collision continues through penetration damage and one ammo cost");
        var second=OriginalProjectileAttack.Launch(first.State,axis,0,0,rules);
        var miss=OriginalProjectileAttack.Complete(second.State,"aim",2,strike with {Contact=ProjectileContactKind.Miss},null,[],true,true,rules);
        Check(miss.State.StopReason==ProjectileStopReason.Finished && miss.State.Completed==2 && miss.State.Inventory.Items.Length==0,"Aimed second launch may miss and still completes original budget and resource spending");
        Reject(()=>OriginalProjectileAttack.Complete(miss.State,"aim",2,strike,0,[0],true,true,rules),"Saved aim does not permit duplicate resource completion");
        return checks;
    }
}
