using System.Security.Cryptography;
using NeoWizardry.Core;

static class ItemInventoryChecks
{
    public static int Run(ItemDefinition prototype)
    {
        int checks=0;
        void Check(bool condition,string name) { if(!condition)throw new Exception(name);checks++; }
        void Reject(Action action,string name)
        {
            bool rejected=false;try {action();}catch(Exception){rejected=true;}
            Check(rejected,name);
        }
        var rules=new Dictionary<int,ItemInventoryRule>{{1,new(1,1000)},{2,new(2,1,ItemResourceKind.Charges)},{3,new(3)}};
        var bag=new ItemLocation("party");
        ItemInstance Stack(string id,int quantity)=>new(id,1,quantity,true,null,true,bag);
        var start=ItemInventory.Add(ItemInventory.Create(2),Stack("a",700),rules);
        var split=ItemInventory.Split(start,"a",300,"b",rules);
        Check(ItemInventory.Count(split,1)==700 && split.Items.Single(i=>i.Id=="a").Quantity==400,"Typed split conserves quantities above byte range");
        Reject(()=>ItemInventory.Split(split,"a",1,"c",rules),"Full inventory split is rejected atomically");
        Check(ItemInventory.Count(split,1)==700 && start.Items.Single().Quantity==700,"Rejected and successful operations preserve source state");
        var merged=ItemInventory.Merge(split,"a","b",rules);
        Check(merged.Moved==300 && merged.State.Items.Single().Id=="a","Full merge retains destination identity");
        var crowded=ItemInventory.Add(start,Stack("b",600),rules);
        var partial=ItemInventory.Merge(crowded,"a","b",rules);
        Check(partial.Moved==300 && partial.State.Items.Single(i=>i.Id=="b").Quantity==300 && ItemInventory.Count(partial.State,1)==1300,"Partial merge reports actual movement and preserves remainder");
        var moved=ItemInventory.Move(start,"a",new("hero-1"),rules);
        Check(moved.Items.Single().CurseRemoved && ItemInventory.Count(moved,1,"party")==0 && ItemInventory.Count(moved,1,"hero-1")==700,"Transfer preserves semantic flags and changes ownership once");
        var different=ItemInventory.Add(start,Stack("b",1) with {Identified=false},rules);
        Check(ItemInventory.Merge(different,"a","b",rules).Moved==0,"Unidentified stack cannot lose its identity in a merge");
        var curses=ItemInventory.Add(start,Stack("b",1) with {CurseRemoved=false},rules);
        Check(ItemInventory.Merge(curses,"a","b",rules).Moved==0,"Merge preserves distinct curse state");
        var charged=new ItemInstance("wand",2,1,true,400,false,bag);
        var resources=ItemInventory.Add(ItemInventory.Create(),charged,rules);
        var spent=ItemInventory.SpendResource(resources,"wand",400,rules);
        Check(spent.Items.Single().Quantity==1 && spent.Items.Single().ResourceRemaining==0,"Resource exhaustion preserves the item");
        Reject(()=>ItemInventory.SpendResource(spent,"wand",1,rules),"Insufficient charges rejected");
        Reject(()=>ItemInventory.SpendResource(start,"a",1,rules),"Quantity is not a charge pool");
        var equipment=ItemInventory.Move(resources,"wand",new("hero-1","hand"),rules);
        equipment=ItemInventory.Add(equipment,new("sword",3,1,true,null,false,new("hero-2","hand")),rules);
        Reject(()=>ItemInventory.Move(equipment,"wand",new("hero-2","hand"),rules),"Occupied equipment slot rejected");
        var swapped=ItemInventory.SwapLocations(equipment,"wand","sword",rules);
        Check(swapped.Items.Single(i=>i.Id=="wand").Location.ContainerId=="hero-2" && swapped.Items.Single(i=>i.Id=="sword").Location.ContainerId=="hero-1","Equipment swap changes locations without duplicating instances");
        Reject(()=>ItemInventory.Move(start,"a",new("hero-1","hand"),rules),"Stack cannot occupy one equipment slot");
        var output=new ItemInstance("result",3,1,true,null,false,bag);
        var transformed=ItemInventory.Transform(crowded,[new("a",700),new("b",600)],[output],rules);
        Check(transformed.Items.Single()==output && crowded.Items.Length==2,"Craft transaction frees capacity and leaves inputs untouched");
        Reject(()=>ItemInventory.Transform(crowded,[new("a",1)],[output],rules),"Craft capacity failure cannot consume ingredients");
        Reject(()=>ItemInventory.Transform(crowded,[new("a",701)],[output],rules),"Insufficient ingredients rejected");
        Reject(()=>ItemInventory.Transform(crowded,[new("a",1),new("a",1)],[output],rules),"Repeated ingredient selection rejected");
        Reject(()=>ItemInventory.Transform(crowded,[new("a",700)],[output with {Id="a"}],rules),"Craft output needs a fresh identity");
        Reject(()=>ItemInventory.Transform(equipment,[new("wand",1)],[output],rules),"Craft cannot silently consume equipped items");
        string json=ItemInventory.Save(partial.State,rules);
        Check(ItemInventory.Save(ItemInventory.Load(json,rules),rules)==json,"Typed inventory save roundtrip");
        Check(!json.Contains("raw") && !json.Contains("hash") && !json.Contains("slot_hex"),"Runtime save excludes original-format evidence");
        Check(ItemInventory.Create(501).Capacity==501,"Runtime capacity is not the original fixed 500-slot bound");
        Reject(()=>ItemInventory.Create(-1),"Negative capacity rejected at creation");
        Reject(()=>ItemInventory.Add(start,Stack("a",1),rules),"Duplicate instance identity rejected");
        Reject(()=>ItemInventory.Add(ItemInventory.Create(),charged with {ResourceRemaining=null},rules),"Resource items require explicit resource state");

        var raw=new byte[269];raw[0x66]=1;raw[0x6b]=10;
        var definition=prototype with {RecordSha256=Convert.ToHexString(SHA256.HashData(raw)),Instance=new(1,0,0,0,10,0,Convert.ToHexString(raw))};
        var slot=new NativeItemInstance(definition.Id,7,0,255,17,18,19,1,21);
        var imported=OriginalItemAdapter.Convert(definition,slot,"imported",bag);
        Check(imported.Item.Quantity==7 && imported.Item.Identified && imported.Item.CurseRemoved,"Import converts original counters and flags to explicit semantics");
        Check(imported.Evidence.RawSlotHex==Convert.ToHexString(slot.Encode()) && imported.Evidence.SourceRecordHash==definition.RecordSha256,"Import retains unknown bytes separately as evidence");
        Reject(()=>OriginalItemAdapter.Convert(definition,slot with {CountRaw=11},"overflow",bag),"Unresolved import overflow cannot enter canonical state");
        Reject(()=>OriginalItemAdapter.Convert(definition,slot with {ChargesRaw=2},"inconsistent",bag),"Import counter repairs stay outside canonical state");
        Reject(()=>OriginalItemAdapter.Convert(definition,slot with {UncursedRaw=2},"unknown",bag),"Unknown curse flags require an import decision");
        raw[0x66]=2;raw[0x6b]=0;
        var wand=definition with {RecordSha256=Convert.ToHexString(SHA256.HashData(raw)),Instance=new(2,0,0,0,0,0,Convert.ToHexString(raw))};
        var importedWand=OriginalItemAdapter.Convert(wand,slot with {CountRaw=0,ChargesRaw=12},"import-wand",bag);
        Check(importedWand.Item.Quantity==1 && importedWand.Item.ResourceRemaining==12 && OriginalItemAdapter.Rule(wand).Resource==ItemResourceKind.Charges,"Import distinguishes item quantity from charges");
        return checks;
    }
}
