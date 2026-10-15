using System.Text.Json;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class ProjectileStrikeChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool threw=false;try{action();}catch(Exception){threw=true;}Check(threw,name);}
        var dice=new PhysicalDamageDice(2,1,6);
        var shot=new ProjectileStrikeInput(OriginalAttackMode.Shoot,ProjectileContactKind.Contact,50,dice);
        Check(OriginalProjectileStrike.ActorContact(0,null)==ProjectileContactKind.Contact,"Zero interception takes no d100");
        Check(OriginalProjectileStrike.ActorContact(25,24)==ProjectileContactKind.Intercepted,"Interception inclusive one-based threshold");
        Check(OriginalProjectileStrike.ActorContact(25,25)==ProjectileContactKind.Contact,"Interception boundary immediately above threshold");
        Check(OriginalProjectileStrike.ActorContact(100,99)==ProjectileContactKind.Intercepted,"Full interception blocks natural 100");
        Check(OriginalProjectileStrike.ActorContact(255,99)==ProjectileContactKind.Intercepted,"Native byte above 100 still consumes a draw and blocks");
        Reject(()=>OriginalProjectileStrike.ActorContact(0,0),"Reject unused interception draw");
        Reject(()=>OriginalProjectileStrike.ActorContact(1,null),"Positive interception requires a draw");
        foreach(int draw in new[]{-1,100})Reject(()=>OriginalProjectileStrike.ActorContact(50,draw),"Reject invalid interception d100");
        foreach(int chance in new[]{-1,256})Reject(()=>OriginalProjectileStrike.ActorContact(chance,0),"Reject interception outside byte domain");
        foreach(var mode in new[]{OriginalAttackMode.Shoot,OriginalAttackMode.Throw})
        {
            var input=shot with {Mode=mode};
            Check(OriginalProjectileStrike.Resolve(input,49,[5])==new ProjectileStrikeResult(ProjectileStrikeOutcome.Damage,50,new(8,8,8)),"Projectile penetration equality permits one normal damage roll");
            Check(OriginalProjectileStrike.Resolve(input,50,[]).Outcome==ProjectileStrikeOutcome.NotPenetrated,"Projectile penetration does not reroll after failure");
            Check(OriginalProjectileStrike.Resolve(input with {PenetrationScore=0},0,[]).Outcome==ProjectileStrikeOutcome.NotPenetrated,"Natural one is not automatic projectile penetration");
            Check(OriginalProjectileStrike.Resolve(input with {PenetrationScore=100},99,[0]).Outcome==ProjectileStrikeOutcome.Damage,"Natural 100 is not a projectile miss after established contact");
            foreach(var contact in new[]{ProjectileContactKind.Miss,ProjectileContactKind.Intercepted})
            {
                var stopped=OriginalProjectileStrike.Resolve(input with {Contact=contact},null,[]);
                Check(stopped.Damage is null && stopped.PenetrationRoll is null,"Miss/interception consumes no penetration or damage dice");
                Reject(()=>OriginalProjectileStrike.Resolve(input with {Contact=contact},0,[]),"Reject unused penetration draw on delivery failure");
                Reject(()=>OriginalProjectileStrike.Resolve(input with {Contact=contact},null,[0]),"Reject unused damage draw on delivery failure");
            }
        }
        Reject(()=>OriginalProjectileStrike.Resolve(shot,null,[0]),"Contact requires penetration draw even when threshold is known");
        Reject(()=>OriginalProjectileStrike.Resolve(shot with {PenetrationScore=0},0,[0]),"Failed penetration rejects damage dice");
        Reject(()=>OriginalProjectileStrike.Resolve(shot,0,[]),"Successful penetration requires all damage dice");
        Reject(()=>OriginalProjectileStrike.Resolve(shot with {Mode=OriginalAttackMode.Swing},0,[0]),"Melee cannot enter projectile resolver");
        Reject(()=>OriginalProjectileStrike.Resolve(shot with {Contact=(ProjectileContactKind)99},0,[0]),"Reject unknown delivery status");
        foreach(int draw in new[]{-1,100})Reject(()=>OriginalProjectileStrike.Resolve(shot,draw,[0]),"Reject invalid penetration d100");
        bool exhaustive=true;
        foreach(int score in new[]{int.MinValue,-1,0,1,5,50,95,99,100,int.MaxValue})
            for(int draw=0;draw<100;draw++)
            {
                bool passes=draw+1<=score;
                var result=OriginalProjectileStrike.Resolve(shot with {PenetrationScore=score},draw,passes?[0]:[]);
                exhaustive &= result.Outcome==(passes?ProjectileStrikeOutcome.Damage:ProjectileStrikeOutcome.NotPenetrated) && result.PenetrationRoll==draw+1;
            }
        Check(exhaustive,"All 100 d100 outcomes at ten threshold boundaries use only penetration comparison");
        var scaled=OriginalProjectileStrike.Resolve(shot with {HandDamagePercent=50,AbsorptionPercent=50},0,[2]);
        Check(scaled.Damage==new PhysicalDamageResult(5,8,4),"Projectile uses recovered two-stage damage scaling");
        Check(OriginalProjectileStrike.Resolve(shot with {AbsorptionPercent=100},0,[5]).Damage!.Damage==0,"A penetrating projectile can be fully absorbed");
        var attrs=new PhysicalCombatAttributes(50,50,50,50);
        var lowAccuracy=OriginalPhysicalScores.OrdinaryCharacter(new(0,0,0,0,0,attrs,attrs,OriginalAttackMode.Shoot,100,0));
        Check(lowAccuracy.HitScore==-500 && lowAccuracy.PenetrationScore==10 &&
            OriginalProjectileStrike.Resolve(shot with {PenetrationScore=lowAccuracy.PenetrationScore},9,[0]).Outcome==ProjectileStrikeOutcome.Damage,
            "After collision a low melee hit score does not add a second accuracy/fumble gate");

        var rules=new Dictionary<int,ItemInventoryRule>{{1,new(1,100)},{2,new(2,1,ItemResourceKind.Shots)}};
        ItemInventoryState Bag()=>new(1,3,[new("arrow",1,3,true,null,false,new("hero")),new("reserve",1,5,true,null,false,new("hero"))]);
        var start=ProjectileSeries.Create("attack",Bag(),"arrow",new(1,ProjectileCostKind.Quantity),5,rules);
        var flight=ProjectileSeries.Launch(start,rules);string before=JsonSerializer.Serialize(flight);
        foreach(var (contact,penetration,outcome) in new[]{
            (ProjectileContactKind.Miss,(int?)null,ProjectileStrikeOutcome.Miss),
            (ProjectileContactKind.Intercepted,(int?)null,ProjectileStrikeOutcome.Intercepted),
            (ProjectileContactKind.Contact,(int?)99,ProjectileStrikeOutcome.NotPenetrated),
            (ProjectileContactKind.Contact,(int?)49,ProjectileStrikeOutcome.Damage)})
        {
            var result=OriginalProjectileAttack.Complete(flight,"attack",1,shot with {Contact=contact},penetration,
                outcome==ProjectileStrikeOutcome.Damage?[5]:[],true,true,rules);
            Check(result.Strike.Outcome==outcome && result.State.Inventory.Items.Single(i=>i.Id=="arrow").Quantity==2 &&
                result.State.Completed==1 && result.State.Remaining==4,"Every delivery/penetration outcome completes once and spends exactly one arrow");
            Check(JsonSerializer.Serialize(flight)==before,"Composed attack does not mutate pending input state");
            Reject(()=>OriginalProjectileAttack.Complete(result.State,"attack",1,shot,0,[0],true,true,rules),"Duplicate composed completion rejected");
        }
        Reject(()=>OriginalProjectileAttack.Complete(flight,"wrong",1,shot,0,[0],true,true,rules),"Foreign series ID rejected before resolution");
        Reject(()=>OriginalProjectileAttack.Complete(flight,"attack",2,shot,0,[0],true,true,rules),"Wrong pending ordinal rejected");
        Reject(()=>OriginalProjectileAttack.Complete(flight,"attack",1,shot,99,[0],true,true,rules),"Invalid draw plan cannot partially spend inventory");
        Check(JsonSerializer.Serialize(flight)==before,"Rejected completion preserves entire input state");
        var restored=JsonSerializer.Deserialize<ProjectileSeriesState>(before)!;
        Check(JsonSerializer.Serialize(OriginalProjectileAttack.Complete(restored,"attack",1,shot,49,[5],true,true,rules))==
            JsonSerializer.Serialize(OriginalProjectileAttack.Complete(flight,"attack",1,shot,49,[5],true,true,rules)),"Post-contact damage/resource completion replays after pending save");
        var series=start;
        for(int i=0;i<3;i++)
        {
            series=ProjectileSeries.Launch(series,rules);
            series=OriginalProjectileAttack.Complete(series,"attack",i+1,shot,49,[0],true,true,rules).State;
        }
        Check(series.StopReason==ProjectileStopReason.Depleted && series.Completed==3,"Connected strikes exhaust selected ammo before five-swing budget");
        series=ProjectileSeries.ReplaceDepletedSource(series,"reserve",new(1,ProjectileCostKind.Quantity),rules);
        for(int i=3;i<5;i++)
        {
            series=ProjectileSeries.Launch(series,rules);
            series=OriginalProjectileAttack.Complete(series,"attack",i+1,shot,49,[0],true,true,rules).State;
        }
        Check(series.StopReason==ProjectileStopReason.Finished && series.Completed==5 && series.Inventory.Items.Single().Quantity==3,"Reloaded source completes original allocation with no extra strikes or resource loss");
        var gunBag=new ItemInventoryState(1,1,[new("gun",2,1,true,1,false,new("hero","main"))]);
        var gunFlight=ProjectileSeries.Launch(ProjectileSeries.Create("gun",gunBag,"gun",new(2,ProjectileCostKind.Resource),3,rules),rules);
        var blocked=OriginalProjectileAttack.Complete(gunFlight,"gun",1,shot with {Contact=OriginalProjectileStrike.ActorContact(50,49)},null,[],false,true,rules);
        Check(blocked.Strike.Outcome==ProjectileStrikeOutcome.Intercepted && blocked.State.Inventory.Items.Single().ResourceRemaining==0 &&
            blocked.State.StopReason==ProjectileStopReason.ActorUnavailable,"Intercepted in-flight shot spends final charge even if actor becomes unavailable");
        return checks;
    }
}
