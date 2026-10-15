using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NeoWizardry.Core;

static class AmmunitionChecks
{
    public static int Run()
    {
        int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name){bool threw=false;try{action();}catch(Exception){threw=true;}Check(threw,name);}
        ItemDefinition Definition(int id,int type,int icon,int mode=0,int capacity=0,int dice=0,int sides=0,int attack=0,int range=3,bool retain=false)
        {
            var raw=new byte[269];raw[0x3e]=(byte)type;BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0x3f),(ushort)icon);
            raw[0x47]=(byte)range;raw[0x66]=(byte)mode;raw[0x6b]=100;
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x67),(short)capacity);raw[0x69]=(byte)dice;raw[0x6a]=(byte)sides;
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0x4e),(ushort)attack);if(retain)raw[0x62]=1;
            return SourceFixture.Item(new(id,"synthetic",type,icon,0,0,0,Convert.ToHexString(SHA256.HashData(raw)),0,range,0,0,0,1,4,attack,0,false,0,
                new(mode,capacity,dice,sides,100,0,Convert.ToHexString(raw))));
        }
        var bow=Definition(1,3,11,attack:256);var arrow=Definition(2,4,22,1);
        var otherArrow=Definition(3,4,22,1);var bolt=Definition(4,4,83,1);var slingStone=Definition(5,4,23,1);
        foreach(var (icon,accepted) in new[]{(11,22),(37,22),(12,83),(13,23)})
        {
            var weapon=Definition(1,3,icon,attack:256);
            foreach(var source in new[]{arrow,bolt,slingStone})
                Check(OriginalAmmunitionAdapter.Compatible(weapon,source)==(source.IconIndex==accepted),"Ordinary launcher family mapping");
        }
        Reject(()=>OriginalWeaponAdapter.PreparePrimary(bow,bolt),"Prepared damage cannot accept a bolt in a bow");
        foreach(int stage in new[]{599,600,601,602,603,604,605,606,607,608,609,610})
        {
            int[] allowed=stage switch {599=>[23,113],600 or 601=>[23,113,112],>=602 and <=605=>[23,113,112,21,126,0],_=>[23,113,112,21,126,0,22,83]};
            var omni=Definition(stage,3,131,attack:256);
            foreach(int icon in new[]{23,113,112,21,126,0,22,83,132})
                Check(OriginalAmmunitionAdapter.Compatible(omni,Definition(20,2,icon,1))==allowed.Contains(icon),"Omnigun staged ammo families");
            Check(!OriginalAmmunitionAdapter.Compatible(omni,Definition(21,4,0,1)),"Generic icon zero requires thrown type");
        }
        foreach(int stage in new[]{598,611})
            Check(!OriginalAmmunitionAdapter.Compatible(Definition(stage,3,131,attack:256),slingStone),"Unknown Omnigun definition does not inherit a stage");
        foreach(var (weaponIcon,fuelIcon) in new[]{(114,113),(110,132),(104,132),(144,143),(145,146)})
        {
            var weapon=Definition(30,3,weaponIcon,4,5,2,6,256);
            var fuel=Definition(31,4,fuelIcon,2);
            Check(OriginalAmmunitionAdapter.FuelIcon(weaponIcon)==fuelIcon,"Native refill pair");
            Check(OriginalAmmunitionAdapter.PrepareRefill(weapon,fuel)==new OriginalRefillPreparation(17,true),"Refill uses maximum formula without RNG");
            Check(OriginalAmmunitionAdapter.Compatible(weapon,fuel)==(weaponIcon is 104 or 110 or 114),"Compatibility dispatch and refill table retain different scopes");
        }
        var gun=Definition(6,3,104,4,5,attack:256);var fuelCharge=Definition(7,4,132,2);
        var fuelStack=Definition(8,4,132,1);var retainedFuel=Definition(9,4,132,2,retain:true);
        Check(!OriginalAmmunitionAdapter.Compatible(gun,Definition(10,3,132,2,range:2)),"Weapon-category candidate must have matching range");
        Check(OriginalAmmunitionAdapter.Compatible(gun,Definition(10,3,132,2,range:3)),"Matching range permits compatible weapon-category candidate");
        Reject(()=>OriginalAmmunitionAdapter.PrepareRefill(gun,fuelCharge with {IconIndex=113}),"Wrong fuel rejected");
        Reject(()=>OriginalAmmunitionAdapter.PrepareRefill(bow,fuelCharge),"Only native mode-four destination is refillable");
        Reject(()=>OriginalAmmunitionAdapter.PrepareRefill(Definition(6,3,104,4,-1),fuelCharge),"Negative capacity rejected instead of unsigned wrap");
        Reject(()=>OriginalAmmunitionAdapter.PrepareRefill(Definition(6,3,104,4,250,1,6),fuelCharge),"Capacity above byte limit rejected");
        Reject(()=>OriginalAmmunitionAdapter.PrepareRefill(gun,fuelCharge with {RecordSha256=new string('0',64)}),"Native refill metadata hash verified");
        var definitions=new[]{bow,arrow,otherArrow,bolt,slingStone,gun,fuelCharge,fuelStack,retainedFuel};
        var catalog=new ItemCatalog(3,"DATABASES/ITEMS.DBS",new("loose","synthetic",new string('a',64)),269,definitions.Length,[..definitions]);
        catalog.Validate();var rules=definitions.Select(OriginalItemAdapter.Rule).ToDictionary(r=>r.DefinitionId);
        ItemInstance Item(string id,int definition,int quantity=1,int? resource=null,bool equipped=false)=>
            new(id,definition,quantity,true,resource,false,new("hero",equipped?"main":null));
        ItemInventoryState Inventory(params ItemInstance[] items)=>new(1,items.Length,[..items]);
        ProjectileSeriesState Complete(ProjectileSeriesState state,bool actor=true,bool target=true)
        {
            state=ProjectileSeries.Launch(state,rules);
            return ProjectileSeries.Complete(state,state.Id,state.Pending!.Ordinal,ProjectileOutcome.Miss,actor,target,rules).State;
        }
        var bag=Inventory(Item("selected",2),Item("same",2,2),Item("preferred",3,3),Item("wrong",4,10));
        var series=ProjectileSeries.Create("bow",bag,"selected",OriginalWeaponAdapter.ProjectileExpenditure(bow,arrow),5,rules);
        var depleted=Complete(series);string before=JsonSerializer.Serialize(depleted);
        var replaced=OriginalProjectileReload.ReplaceAmmunition(depleted,bow,"preferred",["same"],catalog,rules);
        Check(replaced.SourceId=="preferred" && replaced.Cost.DefinitionId==3 && replaced.Completed==1 && replaced.Remaining==4,"Compatible preferred reserve wins over old definition and preserves allocation");
        Check(JsonSerializer.Serialize(depleted)==before,"Replacement leaves input unchanged");
        var fallback=OriginalProjectileReload.ReplaceAmmunition(depleted,bow,"wrong",["preferred","same"],catalog,rules);
        Check(fallback.SourceId=="same","Fallback keeps old ammo definition instead of choosing another compatible family variant");
        Check(OriginalProjectileReload.ReplaceAmmunition(depleted,bow,null,["preferred"],catalog,rules)==depleted,"No old-definition reserve leaves series depleted");
        Check(OriginalProjectileReload.ReplaceAmmunition(depleted,bow,"gone",["missing","same"],catalog,rules).SourceId=="same","Stale reserve selection is skipped safely");
        var completedAgain=Complete(replaced);
        Check(completedAgain.Completed==2 && completedAgain.Inventory.Items.Single(i=>i.Id=="preferred").Quantity==2,"Resumed shot spends new source and advances original ordinal");
        var pendingAgain=ProjectileSeries.Launch(replaced,rules);
        Reject(()=>ProjectileSeries.Complete(pendingAgain,"bow",1,ProjectileOutcome.Hit,true,true,rules),"Previous-source completion cannot finish replacement shot");
        Reject(()=>ProjectileSeries.ReplaceDepletedSource(pendingAgain,"same",new(2,ProjectileCostKind.Quantity),rules),"Cannot replace a source in flight");
        foreach(var (actor,target,reason) in new[]{(false,true,ProjectileStopReason.ActorUnavailable),(true,false,ProjectileStopReason.ContinuationUnavailable)})
        {
            var stopped=Complete(series,actor,target);
            Check(stopped.StopReason==reason,"Interruption takes precedence over simultaneous depletion");
            Reject(()=>OriginalProjectileReload.ReplaceAmmunition(stopped,bow,"preferred",["same"],catalog,rules),"Reload cannot revive actor/target interrupted series");
        }
        var last=Complete(ProjectileSeries.Create("last",bag,"selected",new(2,ProjectileCostKind.Quantity),1,rules));
        Reject(()=>ProjectileSeries.ReplaceDepletedSource(last,"same",new(2,ProjectileCostKind.Quantity),rules),"Reload cannot extend exhausted swing allocation");
        var gunBag=Inventory(Item("gun",6,resource:1,equipped:true),Item("fuel",7,resource:3));
        var gunSeries=ProjectileSeries.Create("gun",gunBag,"gun",OriginalWeaponAdapter.ProjectileExpenditure(gun),5,rules);
        var emptyGun=Complete(gunSeries);
        var reloaded=OriginalProjectileReload.RefillDepletedWeapon(emptyGun,"fuel",catalog,rules);
        Check(reloaded.Inventory.Items.Single().ResourceRemaining==3 && reloaded.SourceId=="gun" && reloaded.Completed==1 && reloaded.Remaining==4,"Fuel transfer removes disposable empty source and preserves series identity");
        for(int i=0;i<3;i++)reloaded=Complete(reloaded);
        Check(reloaded.Completed==4 && reloaded.Remaining==1 && reloaded.StopReason==ProjectileStopReason.Depleted,"Refilled series stops again at real resource limit");
        var partial=Inventory(Item("gun",6,resource:4,equipped:true),Item("fuel",7,resource:10));
        string partialBefore=ItemInventory.Save(partial,rules);
        var topped=OriginalProjectileReload.Refill(partial,"gun","fuel",catalog,rules);
        Check(topped.Transferred==1 && topped.State.Items.Single(i=>i.Id=="gun").ResourceRemaining==5 && topped.State.Items.Single(i=>i.Id=="fuel").ResourceRemaining==9,"Refill conserves units and leaves surplus fuel");
        Check(ItemInventory.Save(partial,rules)==partialBefore,"Atomic refill leaves source state unchanged");
        var full=OriginalProjectileReload.Refill(topped.State,"gun","fuel",catalog,rules);
        Check(full.Transferred==0 && full.State==topped.State,"Full weapon consumes no fuel");
        var quantity=OriginalProjectileReload.Refill(Inventory(Item("gun",6,resource:0),Item("fuel",8,3)),"gun","fuel",catalog,rules);
        Check(quantity.Transferred==3 && quantity.State.Items.Length==1,"Stack fuel transfers one quantity per shot and removes empty stack");
        var keep=OriginalProjectileReload.Refill(Inventory(Item("gun",6,resource:0),Item("fuel",9,resource:2)),"gun","fuel",catalog,rules);
        Check(keep.Transferred==2 && keep.State.Items.Single(i=>i.Id=="fuel").ResourceRemaining==0,"Capability-bearing empty fuel is retained");
        var zero=OriginalProjectileReload.Refill(keep.State,"gun","fuel",catalog,rules);
        Check(zero.Transferred==0 && zero.State==keep.State,"Empty retained fuel creates no shots");
        Reject(()=>OriginalProjectileReload.Refill(partial,"gun","gun",catalog,rules),"Self-refill rejected atomically");
        Reject(()=>ItemInventory.RefillResource(partial,"gun","fuel",3,true,rules),"Over-capacity destination is not repaired by spending fuel");
        Reject(()=>ItemInventory.RefillResource(bag,"same","preferred",5,true,rules),"Nonresource destination rejected");
        var reserveBag=Inventory(Item("gun",6,resource:1,equipped:true),Item("empty",9,resource:0),
            Item("wrong",4,10),Item("first",7,resource:1),Item("second",7,resource:5));
        var reserveSeries=Complete(ProjectileSeries.Create("reserves",reserveBag,"gun",OriginalWeaponAdapter.ProjectileExpenditure(gun),5,rules));
        var selectedFuel=OriginalProjectileReload.RefillFromReserves(reserveSeries,["missing","wrong","empty","first","second"],catalog,rules);
        Check(selectedFuel.Inventory.Items.Single(i=>i.Id=="gun").ResourceRemaining==1 &&
            selectedFuel.Inventory.Items.Single(i=>i.Id=="second").ResourceRemaining==5,"Refill searches explicit reserves in order, skips missing/wrong/empty and uses one source");
        Check(OriginalProjectileReload.RefillFromReserves(reserveSeries,["empty","wrong"],catalog,rules)==reserveSeries,"No usable fuel leaves series depleted without consuming anything");
        Reject(()=>OriginalProjectileReload.RefillFromReserves(gunSeries,[],catalog,rules),"Empty search cannot bypass refill eligibility gate");
        var restored=JsonSerializer.Deserialize<ProjectileSeriesState>(JsonSerializer.Serialize(emptyGun))!;
        var replay=OriginalProjectileReload.RefillDepletedWeapon(restored,"fuel",catalog,rules);
        Check(JsonSerializer.Serialize(replay)==JsonSerializer.Serialize(OriginalProjectileReload.RefillDepletedWeapon(emptyGun,"fuel",catalog,rules)),"Reload from persisted depleted state is deterministic");
        var pendingReload=ProjectileSeries.Launch(replay,rules);
        var restoredPending=JsonSerializer.Deserialize<ProjectileSeriesState>(JsonSerializer.Serialize(pendingReload))!;
        Check(JsonSerializer.Serialize(ProjectileSeries.Complete(restoredPending,"gun",2,ProjectileOutcome.Blocked,true,true,rules))==
            JsonSerializer.Serialize(ProjectileSeries.Complete(pendingReload,"gun",2,ProjectileOutcome.Blocked,true,true,rules)),"Pending replacement projectile survives save and blocked resolution");
        return checks;
    }
}
