using System.Security.Cryptography;
using System.Text.Json;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

static class ProjectileChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Throws(Action action,string name){bool threw=false;try{action();}catch(Exception){threw=true;}Check(threw,name);}
        var rules=new Dictionary<int,ItemInventoryRule>{{1,new(1,100)},{2,new(2,1,ItemResourceKind.Shots)},
            {3,new(3)},{4,new(4,1,ItemResourceKind.Charges)}};
        ItemInventoryState Bag(int count)=>new(1,4,[new("ammo",1,count,true,null,false,new("hero"))]);
        ProjectileSeriesState Begin(int count,int swings)=>ProjectileSeries.Create("attack",Bag(count),"ammo",new(1,ProjectileCostKind.Quantity),swings,rules);
        ProjectileSeriesState Finish(ProjectileSeriesState state,ProjectileOutcome outcome=ProjectileOutcome.Hit,bool actor=true,bool target=true)=>
            ProjectileSeries.Complete(state,state.Id,state.Pending!.Ordinal,outcome,actor,target,rules).State;
        foreach(var outcome in Enum.GetValues<ProjectileOutcome>())
        {
            var start=Begin(2,5);string before=ItemInventory.Save(start.Inventory,rules);
            var launched=ProjectileSeries.Launch(start,rules);
            Check(ItemInventory.Save(launched.Inventory,rules)==before && launched.Completed==0,"Launch does not spend or complete");
            var first=Finish(launched,outcome);
            Check(first.Inventory.Items.Single().Quantity==1 && first.Remaining==4 && first.Completed==1 && first.Pending is null,"Every outcome consumes exactly one attempt and one item");
            Check(ItemInventory.Save(start.Inventory,rules)==before,"Completion leaves input inventory intact");
            Throws(()=>ProjectileSeries.Complete(first,"attack",1,outcome,true,true,rules),"Duplicate completion without pending rejected");
            var next=ProjectileSeries.Launch(first,rules);
            Throws(()=>ProjectileSeries.Complete(next,"attack",1,outcome,true,true,rules),"Old projectile cannot complete a later pending shot");
            Throws(()=>ProjectileSeries.Complete(next,"other",2,outcome,true,true,rules),"Foreign series event rejected");
            var last=Finish(next,outcome);
            Check(last.Inventory.Items.Length==0 && last.StopReason==ProjectileStopReason.Depleted && last.Remaining==3,"Low ammo truncates bonus series after actual launches");
            Throws(()=>ProjectileSeries.Launch(last,rules),"Depleted series cannot launch");
        }
        var pending=ProjectileSeries.Launch(Begin(5,5),rules);
        Throws(()=>ProjectileSeries.Launch(pending,rules),"Only one projectile in flight");
        Throws(()=>ProjectileSeries.Stop(pending,ProjectileStopReason.ActorUnavailable,rules),"Cannot discard in-flight cost");
        Throws(()=>ProjectileSeries.Complete(pending,"attack",1,(ProjectileOutcome)99,true,true,rules),"Unknown contact outcome rejected");
        Check(Finish(pending,actor:false).StopReason==ProjectileStopReason.ActorUnavailable && Finish(pending,actor:false).Inventory.Items.Single().Quantity==4,"Incapacitated actor still pays for projectile already launched");
        Check(Finish(pending,target:false).StopReason==ProjectileStopReason.ContinuationUnavailable,"Unavailable continuation stops only after spending");
        foreach(var reason in new[]{ProjectileStopReason.ActorUnavailable,ProjectileStopReason.ContinuationUnavailable})
        {
            var start=Begin(5,5);var stopped=ProjectileSeries.Stop(start,reason,rules);
            Check(ItemInventory.Save(start.Inventory,rules)==ItemInventory.Save(stopped.Inventory,rules) && stopped.Completed==0,"Pre-launch interruption is free");
            Throws(()=>ProjectileSeries.Launch(stopped,rules),"Stopped series cannot relaunch");
        }
        var changed=pending with {Inventory=ItemInventory.Move(pending.Inventory,"ammo",new("elsewhere"),rules)};
        Throws(()=>Finish(changed),"Changed in-flight source rejected before spending");
        var restored=JsonSerializer.Deserialize<ProjectileSeriesState>(JsonSerializer.Serialize(pending))!;
        ProjectileSeries.Validate(restored,rules);
        Check(JsonSerializer.Serialize(Finish(restored,ProjectileOutcome.Miss))==JsonSerializer.Serialize(Finish(pending,ProjectileOutcome.Miss)),"Pending state roundtrip continues deterministically");
        var equipped=new ItemInventoryState(1,1,[new("throw",1,1,true,null,false,new("hero","main"))]);
        var throwing=ProjectileSeries.Create("throwing",equipped,"throw",new(1,ProjectileCostKind.Quantity),3,rules);
        var thrown=Finish(ProjectileSeries.Launch(throwing,rules));
        Check(thrown.Inventory.Items.Length==0 && thrown.StopReason==ProjectileStopReason.Depleted,"Equipped thrown singleton consumed atomically");
        Throws(()=>ItemInventory.Transform(equipped,[new("throw",1)],[],rules),"Crafting still rejects equipped ingredients");
        var gun=new ItemInventoryState(1,1,[new("gun",2,1,true,1,false,new("hero","main"))]);
        var shots=ProjectileSeries.Create("gun",gun,"gun",new(2,ProjectileCostKind.Resource),3,rules);
        var empty=Finish(ProjectileSeries.Launch(shots,rules));
        Check(empty.Inventory.Items.Single().ResourceRemaining==0 && empty.StopReason==ProjectileStopReason.Depleted,"Intrinsic-shot weapon survives empty");
        Throws(()=>ProjectileSeries.Create("dry",empty.Inventory,"gun",new(2,ProjectileCostKind.Resource),1,rules),"Empty gun cannot start");
        var charge=new ItemInventoryState(1,1,[new("fuel",4,1,true,1,false,new("hero"))]);
        var fuel=ProjectileSeries.Create("fuel",charge,"fuel",new(4,ProjectileCostKind.Resource,true),2,rules);
        Check(Finish(ProjectileSeries.Launch(fuel,rules)).Inventory.Items.Length==0,"Exhausted disposable charge source removed");
        var reusable=ProjectileSeries.Create("return",equipped,"throw",new(1,ProjectileCostKind.None),15,rules);
        for(int i=0;i<15;i++)reusable=Finish(ProjectileSeries.Launch(reusable,rules));
        Check(reusable.Completed==15 && reusable.StopReason==ProjectileStopReason.Finished && reusable.Inventory.Items.Single().Quantity==1,"Reusable thrown item survives all fifteen attempts");
        var budget=OriginalCombatRules.AllocateSwings(3,2,2).AllocatedSwings;
        var many=Begin(100,budget);
        for(int i=0;i<budget;i++)many=Finish(ProjectileSeries.Launch(many,rules));
        Check(many.Completed==5 && many.Inventory.Items.Single().Quantity==95,"Recovered swing allocation feeds resource lifecycle");
        var inventory=Bag(20);
        for(int attack=0;attack<3;attack++)
        {
            var series=ProjectileSeries.Create($"attack-{attack}",inventory,"ammo",new(1,ProjectileCostKind.Quantity),budget,rules);
            for(int swing=0;swing<budget;swing++)series=Finish(ProjectileSeries.Launch(series,rules),ProjectileOutcome.Miss);
            inventory=series.Inventory;
        }
        Check(inventory.Items.Single().Quantity==5,"Three attacks with five swings each spend fifteen distinct projectiles");
        Throws(()=>Begin(1,0),"Zero allocation rejected");
        Throws(()=>ProjectileSeries.Create("bad",gun,"gun",new(2,ProjectileCostKind.Quantity),1,rules),"Quantity policy cannot silently discard a charged weapon");
        Throws(()=>ProjectileSeries.Create("bad",equipped,"throw",new(1,ProjectileCostKind.Resource),1,rules),"Resource policy requires a resource counter");
        Throws(()=>ProjectileSeries.Create("bad",equipped,"throw",new(1,ProjectileCostKind.None,true),1,rules),"Remove-empty policy belongs only to resources");
        Throws(()=>ProjectileSeries.Validate(pending with {Completed=int.MaxValue},rules),"Reject inconsistent/overflowing persisted allocation");

        ItemDefinition Definition(int id,int type,int icon,int mode,int flags=0,int spell=0,int capability=-1,int attack=0)
        {
            var raw=new byte[269];raw[0x41]=(byte)flags;raw[0x66]=(byte)mode;raw[0x6b]=100;raw[0x63]=(byte)spell;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0x4e),(ushort)attack);
            if(capability>=0)raw[capability]=1;
            return new(id,"synthetic",type,icon,0,0,0,Convert.ToHexString(SHA256.HashData(raw)),0,2,0,0,0,1,4,attack,0,false,0,
                new(mode,0,0,0,100,flags,Convert.ToHexString(raw)));
        }
        var bow=Definition(10,1,11,0,attack:0x100);
        var arrow=Definition(11,4,0,1);
        Check(OriginalWeaponAdapter.ProjectileExpenditure(bow,arrow)==new ProjectileCost(11,ProjectileCostKind.Quantity),"Launcher consumes explicitly selected ammunition");
        var knife=Definition(12,2,0,1,attack:0x10);
        Check(OriginalWeaponAdapter.ProjectileExpenditure(knife).Kind==ProjectileCostKind.Quantity,"Ordinary throwing consumes quantity");
        Check(OriginalWeaponAdapter.ProjectileExpenditure(Definition(12,2,0,1,0x80,attack:0x10)).Kind==ProjectileCostKind.None,"Reusable throwing flag suppresses cost");
        Throws(()=>OriginalWeaponAdapter.ProjectileExpenditure(Definition(12,2,0,1,spell:98,attack:0x10)),"Spell-use throwing path explicitly excluded");
        Throws(()=>OriginalWeaponAdapter.ProjectileExpenditure(Definition(12,2,0,2,attack:0x10)),"Nonstack throwing continuation is explicitly excluded");
        Throws(()=>OriginalWeaponAdapter.ProjectileExpenditure(Definition(12,0,0,0,attack:1)),"Melee-only weapon is not a projectile source");
        Check(OriginalWeaponAdapter.ProjectileExpenditure(Definition(13,3,131,0,attack:0x100),Definition(12,2,0,1,0x80)).Kind==ProjectileCostKind.Quantity,"Reusable throwing flag does not suppress separate Omnigun ammunition cost");
        for(int bit=0;bit<9;bit++)
            Check(!OriginalWeaponAdapter.ProjectileExpenditure(bow,Definition(11,4,0,2,attack:1<<bit)).RemoveWhenEmpty,"Each attack capability retains empty resource");
        Check(OriginalWeaponAdapter.ProjectileExpenditure(Definition(12,3,104,4,attack:0x100)).RemoveWhenEmpty==false,"Weapon attack capability retains empty source");
        Check(OriginalWeaponAdapter.ProjectileExpenditure(bow,Definition(11,4,0,2)).RemoveWhenEmpty,"Capability-free fuel is discarded when empty");
        foreach(int offset in new[]{0x62,0x6c,0x6d,0x6e,0x6f,0x70,0x71,0x72,0x73,0x74,0x8c})
            Check(!OriginalWeaponAdapter.ProjectileExpenditure(bow,Definition(11,4,0,2,capability:offset)).RemoveWhenEmpty,"Recovered retain-empty capability branch");
        Check(OriginalWeaponAdapter.ProjectileExpenditure(bow,Definition(11,4,0,2,capability:0x75)).RemoveWhenEmpty,"Adjacent unknown byte is not a retain-empty capability");
        return checks;
    }
}
