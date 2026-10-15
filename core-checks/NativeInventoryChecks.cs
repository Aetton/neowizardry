using NeoWizardry.Core.Research;
using System.Buffers.Binary;
using System.Security.Cryptography;
using NeoWizardry.Core;

static class NativeInventoryChecks
{
    public static int Run(ItemDefinition prototype)
    {
        int checks=0;
        void Check(bool condition,string name) { if(!condition)throw new Exception(name);checks++; }
        void Reject(Action action,string name)
        {
            bool rejected=false;try {action();}catch(Exception){rejected=true;}Check(rejected,name);
        }
        ItemDefinition Definition(int id,int mode,int maximum)
        {
            var raw=new byte[269];raw[0x66]=(byte)mode;raw[0x6b]=(byte)maximum;
            string name=$"Native fixture {id}";System.Text.Encoding.Unicode.GetBytes(name).CopyTo(raw,0);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0xb9),-1);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(0xbd),-1);
            return SourceFixture.Item(prototype with {Id=id,Name=name,RecordSha256=Convert.ToHexString(SHA256.HashData(raw)),
                Instance=new(mode,0,0,0,maximum,0,Convert.ToHexString(raw))});
        }
        var definition=Definition(0,1,5);var other=Definition(1,2,0);
        var catalog=new ItemCatalog(3,"DATABASES/ITEMS.DBS",new("loose","synthetic-native-inventory",new string('a',64)),269,2,[definition,other]);
        var raw=new NativeItemInstance(0,13,0,255,17,33,71,128,222);
        var normalized=NativeStacks.Normalize(raw,definition,2);
        Check(normalized.Kept.CountRaw==5 && normalized.Overflow.Select(i=>i.CountRaw).SequenceEqual(new byte[]{5,3}),"Oversize stack splits in original overflow order");
        Check(normalized.Kept.UncursedRaw==128 && normalized.Overflow.All(i=>i.UncursedRaw==0),"Overflow moves clear +10 while kept stack retains its byte");
        Check(normalized.Overflow.All(i=>i.IdentificationRaw==255 && i.Unknown7==17 && i.Unknown11==222),"Overflow retains identification and unknown metadata");
        Check(normalized.Kept.CountRaw+normalized.Overflow.Sum(i=>(int)i.CountRaw)==13,"Normalization conserves units");
        Reject(()=>NativeStacks.Normalize(raw,definition,1),"Insufficient overflow space rejects instead of clamping");
        Check(raw.CountRaw==13 && raw.UncursedRaw==128,"Rejected normalization leaves source bytes unchanged");
        var repaired=NativeStacks.Normalize(raw with {CountRaw=0,ChargesRaw=4},definition,0).Kept;
        Check(repaired.CountRaw==4 && repaired.ChargesRaw==0,"Stack moves misplaced charges into empty count");
        repaired=NativeStacks.Normalize(raw with {CountRaw=3,ChargesRaw=4},definition,0).Kept;
        Check(repaired.CountRaw==3 && repaired.ChargesRaw==0,"Existing stack count wins over stray charges");
        foreach(int mode in new[]{2,3,4})
        {
            var charged=raw with {ItemId=1,CountRaw=9,ChargesRaw=0};
            var result=NativeStacks.Normalize(charged,Definition(1,mode,0),0).Kept;
            Check(result.CountRaw==0 && result.ChargesRaw==9,$"Mode {mode} repairs misplaced count into charges");
            Check(NativeStacks.Normalize(charged with {ChargesRaw=6},Definition(1,mode,0),0).Kept.ChargesRaw==6,$"Mode {mode} preserves existing charges");
        }
        Check(NativeStacks.Normalize(raw with {CountRaw=9,ChargesRaw=7},Definition(0,0,0),0).Kept.ChargesRaw==7,"Single-mode raw counters are untouched");
        Reject(()=>NativeStacks.Normalize(raw,Definition(0,1,0),500),"Zero stack limit cannot destroy units");
        Reject(()=>NativeStacks.Normalize(raw,Definition(0,254,5),500),"Unknown normalization mode rejected");
        var empty=NativeStacks.Empty with {Unknown11=255};
        Check(NativeStacks.Normalize(empty,definition,0).Kept==empty,"Empty slot keeps unused raw bytes");

        var destination=raw with {CountRaw=3,IdentificationRaw=1};var source=destination with {CountRaw=4};
        var merged=NativeStacks.MergeNormalized(destination,source,definition);
        Check(merged.Moved==2 && merged.Destination.CountRaw==5 && merged.Source.CountRaw==2 && !merged.SourceFullyTransferred,"Partial merge moves units despite original false return");
        Check(merged.Destination.Unknown7==17 && merged.Source.Unknown11==222,"Partial merge keeps each slot's metadata");
        merged=NativeStacks.MergeNormalized(destination,source with {CountRaw=2},definition);
        Check(merged.SourceFullyTransferred && merged.Source==NativeStacks.Empty && merged.Destination.CountRaw==5,"Full source merge clears local source slot");
        Check(NativeStacks.MergeNormalized(destination with {CountRaw=5},source,definition).Moved==0,"Full destination does not consume source");
        Check(NativeStacks.MergeNormalized(destination,source with {IdentificationRaw=255},definition).Moved==0,"Nonzero identification values must match exactly to merge");
        Check(NativeStacks.MergeNormalized(destination with {IdentificationRaw=0},source with {IdentificationRaw=0},definition).Moved==2,"Two unidentified stacks may merge");
        Check(NativeStacks.MergeNormalized(destination,source with {ItemId=1},definition).Moved==0,"Different item IDs do not merge");
        Check(NativeStacks.MergeNormalized(source with {ItemId=1},source with {ItemId=1},other).Moved==0,"Charged items do not merge as stacks");
        Reject(()=>NativeStacks.MergeNormalized(raw with {IdentificationRaw=1},source,definition),"Overfull destination requires explicit normalization");
        Reject(()=>NativeStacks.MergeNormalized(destination,source with {ChargesRaw=1},definition),"Source's stray charge byte requires normalization");
        var moved=NativeStacks.MoveToEmpty(empty,raw);
        Check(moved.Destination==raw with {UncursedRaw=0} && moved.Source==NativeStacks.Empty,"Local move copies all bytes except observed +10 reset");
        Reject(()=>NativeStacks.MoveToEmpty(raw,raw),"Move cannot overwrite an occupied slot");

        var state=NativeInventory.Create(catalog,5);
        state=NativeInventory.Add(state,0,"original",raw,catalog);
        string before=NativeInventory.Save(state,catalog);
        Check(NativeInventory.Save(NativeInventory.Load(before,catalog),catalog)==before,"Native inventory snapshot preserves identities, slots and raw bytes");
        var expanded=NativeInventory.Normalize(state,0,["overflow-a","overflow-b"],catalog);
        Check(expanded.Entries.Select(e=>e.Slot).SequenceEqual(new[]{0,1,2}) && expanded.Entries[0].InstanceId=="original","Overflow allocates first free slots and retains original identity");
        Check(expanded.Entries.Sum(e=>(int)e.Item.CountRaw)==13,"Inventory overflow preserves ownership count");
        Reject(()=>NativeInventory.Normalize(state,0,["original","new"],catalog),"Overflow IDs must be new and unique");
        var full=state with {Capacity=1};string fullBefore=NativeInventory.Save(full,catalog);
        Reject(()=>NativeInventory.Normalize(full,0,[],catalog),"Full native inventory does not truncate overflow");
        Check(NativeInventory.Save(full,catalog)==fullBefore && NativeInventory.Save(state,catalog)==before,"Failure and successful copies leave original snapshots unchanged");
        var shifted=NativeInventory.Move(expanded,0,4,catalog);
        Check(shifted.Entries.Single(e=>e.Slot==4).InstanceId=="original" && shifted.Entries.Single(e=>e.Slot==4).Item.UncursedRaw==0,"Move retains instance identity and applies original +10 reset");
        Check(!shifted.Entries.Any(e=>e.Slot==0),"Move vacates source without compacting remake stable slots");
        Reject(()=>NativeInventory.Move(expanded,0,1,catalog),"Native move refuses occupied destination");
        Reject(()=>NativeInventory.Move(expanded,0,5,catalog),"Native move respects capacity");
        Reject(()=>NativeInventory.Add(state,0,"other",raw,catalog),"Duplicate slot rejected");
        Reject(()=>NativeInventory.Add(state,1,"original",raw,catalog),"Duplicate identity rejected");
        Reject(()=>NativeInventory.Add(state,1,"empty",empty,catalog),"Empty sentinel is not an owned instance");
        Reject(()=>NativeInventory.Load(before,catalog with {Provenance=catalog.Provenance with {Sha256=new string('b',64)}}),"Native save binds to item database hash");
        Reject(()=>NativeInventory.Load(before,catalog with {Schema=2}),"Native save does not invent instance data from schema 2");
        Reject(()=>NativeInventory.Create(catalog,501),"Native capacity does not exceed observed 500-slot bag bound");
        Reject(()=>NativeInventory.Load("null",catalog),"Empty native inventory JSON rejected");
        var mergeState=NativeInventory.Create(catalog,3);
        mergeState=NativeInventory.Add(mergeState,0,"target",destination,catalog);
        mergeState=NativeInventory.Add(mergeState,1,"source",source,catalog);
        var partial=NativeInventory.Merge(mergeState,0,1,catalog);
        Check(partial.Moved==2 && !partial.SourceFullyTransferred && partial.State.Entries.Length==2,"Native inventory commits partial merge as a change");
        Check(partial.State.Entries[1].InstanceId=="source" && partial.State.Entries[1].Item.CountRaw==2,"Partial source identity survives");
        var room=partial.State with {Entries=partial.State.Entries.Select(e=>e.Slot==0?NativeInventoryEntry.From(0,e.InstanceId,e.Item with {CountRaw=3}):e).ToArray()};
        var finished=NativeInventory.Merge(room,0,1,catalog);
        Check(finished.SourceFullyTransferred && finished.State.Entries.Length==1 && finished.State.Entries[0].InstanceId=="target","Full merge removes only exhausted source identity");
        Reject(()=>NativeInventory.Merge(mergeState,0,0,catalog),"Native instance cannot merge with itself");
        var differing=mergeState with {Entries=[mergeState.Entries[0],NativeInventoryEntry.From(1,"source",source with {Unknown11=3})]};
        string differingBefore=NativeInventory.Save(differing,catalog);
        Reject(()=>NativeInventory.Merge(differing,0,1,catalog),"Uncertain metadata mismatch is guarded rather than discarded");
        Check(NativeInventory.Save(differing,catalog)==differingBefore,"Metadata-guarded merge is atomic");

        var outputDefinition=Definition(2,1,3);var recipeRaw=Convert.FromHexString(outputDefinition.Instance!.RawRecordHex);
        BinaryPrimitives.WriteInt32LittleEndian(recipeRaw.AsSpan(0xb9),0);BinaryPrimitives.WriteInt32LittleEndian(recipeRaw.AsSpan(0xbd),1);
        recipeRaw[0xc9]=26;recipeRaw[0xca]=15;
        outputDefinition=outputDefinition with {RecordSha256=Convert.ToHexString(SHA256.HashData(recipeRaw)),Instance=outputDefinition.Instance with {RawRecordHex=Convert.ToHexString(recipeRaw)}};
        var craftCatalog=catalog with {Count=3,Items=[definition,Definition(1,1,5),outputDefinition]};
        var recipe=new OriginalRecipe(2,outputDefinition.Name,[0,1],[definition.Name,craftCatalog.Find(1).Name],26,15,new string('0',16),[],outputDefinition.RecordSha256,Convert.ToHexString(recipeRaw));
        var recipes=new RecipeCatalog(1,"reference_only","DATABASES/ITEMS.DBS",craftCatalog.Provenance,269,3,1,new(){{"-1",2}},[recipe]);
        NativeInventoryState Ingredients(int a,int b,int capacity)
        {
            var inventory=NativeInventory.Create(craftCatalog,capacity);
            inventory=NativeInventory.Add(inventory,0,"ingredient-a",new(0,(byte)a,0,1,0,0,0,0,0),craftCatalog);
            return NativeInventory.Add(inventory,1,"ingredient-b",new(1,(byte)b,0,1,0,0,0,0,0),craftCatalog);
        }
        var ingredients=Ingredients(4,2,3);string ingredientsBefore=NativeInventory.Save(ingredients,craftCatalog);
        var simulation=NativeInventory.SimulateCombination(ingredients,0,1,15,[],"crafted",[],craftCatalog,recipes);
        Check(simulation.Plan.FirstConsumed==2 && simulation.State.Entries[0].InstanceId=="ingredient-a" && simulation.State.Entries[0].Item.CountRaw==2,"Local combination retains partial ingredient identity and count");
        Check(simulation.State.Entries[1].InstanceId=="crafted" && simulation.State.Entries[1].Item.ItemId==2 && simulation.State.Entries[1].Item.CountRaw==2,"Local combination commits newly initialized output to recovered destination");
        Check(!simulation.State.Entries[1].Item.IsIdentified,"Local result identification follows output definition, not ingredients");
        Check(NativeInventory.Save(NativeInventory.Load(NativeInventory.Save(simulation.State,craftCatalog),craftCatalog),craftCatalog)==NativeInventory.Save(simulation.State,craftCatalog),"Combined inventory snapshot roundtrips");
        var bigResult=NativeInventory.SimulateCombination(Ingredients(5,5,2),0,1,15,[],"crafted",["result-overflow"],craftCatalog,recipes);
        Check(bigResult.State.Entries.Select(e=>e.Item.CountRaw).Order().SequenceEqual(new byte[]{2,3}),"Oversize crafted output partitions within available slots");
        Reject(()=>NativeInventory.SimulateCombination(Ingredients(5,4,2),0,1,15,[],"crafted",["overflow"],craftCatalog,recipes),"Insufficient final result capacity aborts local combination");
        Reject(()=>NativeInventory.SimulateCombination(ingredients,0,1,14,[],"crafted",[],craftCatalog,recipes),"Local combination enforces inclusive skill threshold");
        Reject(()=>NativeInventory.SimulateCombination(ingredients,0,0,15,[],"crafted",[],craftCatalog,recipes),"Local combination requires distinct slots");
        Reject(()=>NativeInventory.SimulateCombination(ingredients,0,1,15,[],"ingredient-b",[],craftCatalog,recipes),"Craft output cannot reuse a consumed identity");
        Reject(()=>NativeInventory.SimulateCombination(ingredients,0,1,15,[],"crafted",[],craftCatalog,recipes with {Provenance=recipes.Provenance with {Sha256=new string('b',64)}}),"Local combination requires matching catalog provenance");
        var easierRaw=recipeRaw.ToArray();easierRaw[0xca]=0;
        var easier=outputDefinition with {Id=3,RecordSha256=Convert.ToHexString(SHA256.HashData(easierRaw)),Instance=outputDefinition.Instance! with {RawRecordHex=Convert.ToHexString(easierRaw)}};
        var duplicateCatalog=craftCatalog with {Count=4,Items=[..craftCatalog.Items,easier]};
        var duplicateRecipes=recipes with {ItemCount=4,Count=2,Recipes=[recipe,recipe with {OutputId=3,SkillThreshold=0,RecordSha256=easier.RecordSha256,RawRecordHex=Convert.ToHexString(easierRaw)}]};
        Reject(()=>NativeInventory.SimulateCombination(ingredients,0,1,14,[],"crafted",[],duplicateCatalog,duplicateRecipes),"First matched recipe's skill failure does not fall through to easier duplicate");
        Check(NativeInventory.Save(ingredients,craftCatalog)==ingredientsBefore,"Local combination and failures preserve original snapshot");

        bool conserved=true;
        for(int limit=1;limit<=20;limit++)for(int count=1;count<=255;count++)
        {
            var d=Definition(0,1,limit);var parts=NativeStacks.Normalize(raw with {CountRaw=(byte)count},d,254);
            conserved&=parts.Kept.CountRaw+parts.Overflow.Sum(i=>(int)i.CountRaw)==count && parts.Kept.CountRaw<=limit && parts.Overflow.All(i=>i.CountRaw>0 && i.CountRaw<=limit);
        }
        Check(conserved,"5100 synthetic count/limit cases preserve all units within limits");
        return checks;
    }
}
