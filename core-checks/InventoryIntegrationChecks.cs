using System.Security.Cryptography;
using System.Text.Json.Nodes;
using NeoWizardry.Core;

static class InventoryIntegrationChecks
{
    public static int Run(ItemCatalog catalog,ItemCatalog crafting)
    {
        int checks=0;
        void Check(bool value,string name) {if(!value)throw new Exception(name);checks++;}
        void Reject(Action action,string name)
        {bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,name);}
        JsonObject Legacy(GameState source)
        {
            var node=JsonNode.Parse(Game.Save(source))!.AsObject();node["Schema"]=1;
            node.Remove("Items");node.Remove("NextItemSequence");return node;
        }
        var source=Inventory.Add(Game.Create(2),77,2,catalog);
        var first=source.Items!.Items[0] with {Identified=false,CurseRemoved=true};
        source=source with {Items=source.Items with {Items=[first,source.Items.Items[1]]}};
        var worn=Equipment.EquipInstance(source,"hero-1",first.Id,catalog);
        Check(Equipment.Instance(worn,"hero-1")==first with {Location=new("hero-1",Equipment.WeaponSlot)},"Equip changes only instance location");
        var returned=Equipment.Unequip(worn,"hero-1",catalog);
        Check(returned.Items!.Items.Single(i=>i.Id==first.Id)==first,"Unequip preserves identity and semantic flags");
        var common=catalog with {Items=[catalog.Find(77) with {RaceMask=65535}]};
        var transferred=Equipment.EquipInstance(returned,"hero-2",first.Id,common);
        Check(Equipment.Instance(transferred,"hero-2")?.Id==first.Id && Inventory.Count(transferred,77)==1,"Cross-hero transfer preserves the exact instance");
        Reject(()=>Equipment.EquipInstance(transferred,"hero-1",first.Id,common),"Equipped instance cannot be duplicated on another hero");
        var other=Inventory.Bag(worn).Single();
        var swapped=Equipment.EquipInstance(worn,"hero-1",other.Id,catalog);
        Check(Equipment.Instance(swapped,"hero-1")?.Id==other.Id && Inventory.Bag(swapped).Single()==first,"Same-definition swap preserves both different instances");
        Check(Game.Save(Game.Load(Game.Save(swapped),catalog))==Game.Save(swapped),"Full instance ownership survives validated game save/load");
        Reject(()=>Equipment.Unequip(worn with {Party=[worn.Party[0] with {Hp=0},worn.Party[1]]},"hero-1",catalog),"Dead hero cannot return weapon");
        Reject(()=>Game.Validate(source with {Items=source.Items! with {Items=[first with {Location=new("missing","weapon")}]}}),"Unknown equipment owner rejected");
        Reject(()=>Game.Validate(source with {Items=source.Items! with {Items=[first with {Location=new("hero-1","unknown")}]}}),"Unsupported equipment location rejected");
        Reject(()=>Game.Validate(source with {NextItemSequence=1}),"Identity allocator cannot reuse saved IDs");
        string oldId=Inventory.Bag(source).First().Id;
        var deleted=Inventory.Remove(Inventory.Remove(source,77,1,catalog),77,1,catalog);
        var replenished=Inventory.Add(Game.Load(Game.Save(deleted)),77,1,catalog);
        Check(replenished.Items!.Items.Single().Id!=oldId && replenished.NextItemSequence>source.NextItemSequence,"Consumed instance identity is never recycled after reload");

        var herbs=Inventory.Add(Game.Create(1),78,4,crafting);
        var split=Inventory.Split(herbs,herbs.Items!.Items.Single().Id,1,crafting);
        Check(Inventory.Count(split,78)==4 && split.Items!.Items.Length==2,"Gameplay split conserves stack units");
        var merged=Inventory.Merge(split,split.Items!.Items[0].Id,split.Items.Items[1].Id,crafting);
        Check(merged.Moved>0 && merged.State.Items!.Items.Single().Quantity==4,"Gameplay merge reports movement and conserves units");
        var full=herbs with {Items=herbs.Items! with {Capacity=1}};
        var packed=Inventory.Add(full,78,3,crafting);
        Check(packed.Items!.Items.Single().Id==herbs.Items!.Items.Single().Id && Inventory.Count(packed,78)==7,"Full inventory can receive compatible units without a new slot");
        Reject(()=>Inventory.Split(full,full.Items!.Items.Single().Id,1,crafting),"Full inventory split fails atomically");
        var huge=Inventory.Add(Inventory.Add(Game.Create(1),78,int.MaxValue,crafting),78,1,crafting);
        Check(Inventory.Count(huge,78)==(long)int.MaxValue+1,"Aggregated display counts do not overflow signed integer");
        var craftStart=Inventory.Add(Inventory.Add(Inventory.Add(Inventory.Add(Game.Create(1),78,4,crafting),79,2,crafting),77,1,crafting),80,1,crafting)
            with {KnownRecipes=["test-tonic"]};
        craftStart=craftStart with {Items=craftStart.Items! with {Capacity=craftStart.Items.Items.Length}};
        var result=Crafting.Craft(craftStart,new("hero-1","test-tonic"),crafting);
        Check(Inventory.Count(result,80)==2 && Inventory.Count(result,78)==2 && Inventory.Count(result,79)==1 && result.Items!.Items.Length==craftStart.Items!.Items.Length,"Craft output can pack atomically into an existing stack at full capacity");
        Check(Inventory.Count(craftStart,80)==1,"Craft packing leaves source state intact");

        var legacy=Legacy(Game.Create(1));legacy["Bag"]=new JsonArray(new JsonObject{["ItemId"]=77,["Count"]=2});
        legacy["Party"]![0]!["EquippedItemId"]=77;string legacyJson=legacy.ToJsonString();
        var migrated=Game.Load(legacyJson,catalog);
        Check(Inventory.Count(migrated,77)==2 && Equipment.ItemId(migrated,"hero-1")==77 && migrated.Items!.Items.Length==3,"Legacy bag and worn copy migrate without loss or duplication");
        Check(Game.Save(Game.Load(legacyJson,catalog))==Game.Save(migrated),"Legacy migration assigns deterministic identities");
        Check(Game.Save(Game.Load(Game.Save(migrated),catalog))==Game.Save(migrated),"Migrated save uses only the new representation");
        string saved=Game.Save(migrated);
        Check(!saved.Contains("EquippedItemId") && !saved.Contains("\"Bag\"") && !saved.Contains("RawSlot"),"Current game save has one authoritative ownership representation");
        Reject(()=>Game.Load(legacyJson),"Legacy owned items require a catalog at migration");
        var duplicate=JsonNode.Parse(legacyJson)!.AsObject();duplicate["Bag"]!.AsArray().Add(new JsonObject{["ItemId"]=77,["Count"]=1});
        Reject(()=>Game.Load(duplicate.ToJsonString(),catalog),"Invalid duplicate legacy stacks cannot be normalized away");
        var mixed=JsonNode.Parse(saved)!.AsObject();mixed["Bag"]=new JsonArray();
        Reject(()=>Game.Load(mixed.ToJsonString(),catalog),"Mixed current and legacy ownership fields rejected");
        var oldMixed=JsonNode.Parse(legacyJson)!.AsObject();oldMixed["Items"]=null;
        Reject(()=>Game.Load(oldMixed.ToJsonString(),catalog),"Legacy save cannot also inject instance state");

        ItemDefinition Native(int id,int mode,int maximum,int amount)
        {
            var bytes=new byte[269];bytes[0x66]=(byte)mode;bytes[0x6b]=(byte)maximum;
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x67),(short)amount);
            return SourceFixture.Item(catalog.Find(77) with {Id=id,RecordSha256=Convert.ToHexString(SHA256.HashData(bytes)),
                Instance=new(mode,amount,0,0,maximum,0,Convert.ToHexString(bytes))});
        }
        var native=catalog with {Schema=3,Count=2,Items=[Native(77,1,5,0),Native(78,2,0,12)]};
        var legacyNative=Legacy(Game.Create(1));legacyNative["Bag"]=new JsonArray(new JsonObject{["ItemId"]=77,["Count"]=13},new JsonObject{["ItemId"]=78,["Count"]=2});
        var nativeMigration=Game.Load(legacyNative.ToJsonString(),native);
        Check(Inventory.Count(nativeMigration,77)==13 && nativeMigration.Items!.Items.Count(i=>i.DefinitionId==77)==3 && nativeMigration.Items.Items.Where(i=>i.DefinitionId==77).All(i=>i.Quantity<=5),"Legacy native stacks partition safely into typed limits");
        Check(nativeMigration.Items!.Items.Where(i=>i.DefinitionId==78).All(i=>i.Quantity==1 && i.ResourceRemaining==0) && nativeMigration.RandomState==Game.Create(1).RandomState,"Missing historical charges migrate exhausted without RNG or a free refill");
        var charged=Inventory.Add(Game.Create(1),78,2,native);string chargedId=charged.Items!.Items[0].Id;
        var spent=Inventory.SpendResource(charged,chargedId,5,native);
        var equippedCharge=Equipment.EquipInstance(spent,"hero-1",chargedId,native);
        Check(Equipment.Instance(equippedCharge,"hero-1")?.ResourceRemaining==7 && Inventory.Count(equippedCharge,78)==1,"Gameplay resource spending and equip preserve per-instance counters");
        var chargeReload=Game.Load(Game.Save(equippedCharge),native);
        Check(Equipment.Instance(chargeReload,"hero-1")==Equipment.Instance(equippedCharge,"hero-1"),"Resource counters resume exactly after game save/load");
        var differentCharge=Inventory.Bag(chargeReload).Single();
        var chargeSwap=Equipment.EquipInstance(chargeReload,"hero-1",differentCharge.Id,native);
        Check(Equipment.Instance(chargeSwap,"hero-1")?.ResourceRemaining==12 && Inventory.Bag(chargeSwap).Single().ResourceRemaining==7,"Same-definition charged weapon swap retains each counter");
        var corrupt=charged with {Items=charged.Items! with {Items=[charged.Items.Items[0] with {ResourceRemaining=null}]}};
        Reject(()=>Game.Load(Game.Save(corrupt),native),"Catalog-aware load rejects incompatible resource state");
        return checks;
    }
}
