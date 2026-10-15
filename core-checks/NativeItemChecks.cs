using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NeoWizardry.Core;

static class NativeItemChecks
{
    public static int Run(ItemDefinition prototype)
    {
        int checks=0;
        void Check(bool condition,string name) { if(!condition)throw new Exception(name);checks++; }
        void Reject(Action action,string name)
        {
            bool rejected=false;try { action(); }catch(Exception){rejected=true;}
            Check(rejected,name);
        }
        ItemDefinition Definition(int mode,int amountBase,int dice,int sides,int flags=0,int maxStack=0)
        {
            var raw=new byte[269];raw[0x66]=(byte)mode;raw[0x41]=(byte)flags;raw[0x6b]=(byte)maxStack;
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(0x67),(short)amountBase);
            raw[0x69]=(byte)dice;raw[0x6a]=(byte)sides;
            return SourceFixture.Item(prototype with {RecordSha256=Convert.ToHexString(SHA256.HashData(raw)),
                Instance=new(mode,amountBase,dice,sides,maxStack,flags,Convert.ToHexString(raw))});
        }
        var bytes=new byte[]{0x12,0x34,0x56,0x78,9,17,255,128,63,7,254,31};
        var slot=NativeItemInstance.Decode(bytes);
        Check(slot.ItemId==0x78563412 && slot.CountRaw==9 && slot.ChargesRaw==17 && slot.IsIdentified,"Native slot distinguishes quantity, charges and identification");
        Check(slot.Encode().SequenceEqual(bytes),"Native slot preserves all unknown and nonboolean flag bytes");
        bytes[7]=0;var encoded=slot.Encode();encoded[8]=0;
        Check(slot.Unknown7==128 && slot.Unknown8==63,"Native slot owns immutable values, not external byte buffers");
        var empty=slot with {ItemId=-1};
        Check(NativeItemInstance.Decode(empty.Encode())==empty && empty.IsEmpty,"Empty-slot sentinel preserves unused bytes");
        Reject(()=>NativeItemInstance.Decode(new byte[11]),"Truncated native slot rejected");
        Reject(()=>NativeItemInstance.Decode(new byte[13]),"Native slot rejects ambiguous trailing data");

        var stack=Definition(1,2,2,6,maxStack:5);
        var created=NativeItemFactory.Create(stack,[0,5]);
        Check(created.CountRaw==9 && created.ChargesRaw==0 && !created.IsIdentified,"Stack initial amount uses signed constant plus one-based dice");
        Check(NativeItemFactory.Create(stack,[],useMaximum:true).CountRaw==14,"Creation maximum is not clamped by stack merge limit");
        Check(NativeItemFactory.Create(Definition(1,-2,3,6),[0,0,0]).CountRaw==1,"Signed negative base retained in a safe roll range");
        Check(NativeItemFactory.Create(Definition(1,255,0,0),[]).CountRaw==255,"Native amount byte upper boundary");
        Reject(()=>NativeItemFactory.Create(Definition(1,256,0,0),[]),"Unsafe amount byte overflow rejected");
        Reject(()=>NativeItemFactory.Create(Definition(1,-2,1,6),[0]),"Unsafe negative amount range rejected");
        Check(NativeItemFactory.Create(Definition(1,-2,1,6),[],useMaximum:true).CountRaw==4,"Safe maximum path separate from unsafe random range");
        Reject(()=>NativeItemFactory.Create(stack,[]),"Missing dice results rejected");
        Reject(()=>NativeItemFactory.Create(stack,[0,5,0]),"Extra dice results rejected");
        Reject(()=>NativeItemFactory.Create(stack,[-1,0]),"Negative zero-based draw rejected");
        Reject(()=>NativeItemFactory.Create(stack,[6,0]),"Out-of-range zero-based draw rejected");
        Check(NativeItemFactory.Create(Definition(1,2,3,0),[]).CountRaw==5,"Zero-sided dice contribute one without RNG draws in normal path");
        Check(NativeItemFactory.Create(Definition(1,2,3,0),[],useMaximum:true).CountRaw==2,"Maximum path uses stored sides literally");
        var single=NativeItemFactory.Create(Definition(0,300,255,255),[]);
        Check(single.CountRaw==0 && single.ChargesRaw==0,"Single-item creation ignores amount fields and keeps raw count zero");
        foreach(int mode in new[]{2,3,4})
        {
            var charged=NativeItemFactory.Create(Definition(mode,2,2,6),[0,5]);
            Check(charged.CountRaw==0 && charged.ChargesRaw==9,$"Mode {mode} stores amount in charges byte, not stack count");
        }
        Check(NativeItemFactory.Create(Definition(2,0,0,0),[]).ChargesRaw==0,"Zero initial charges retained");
        Check(NativeItemFactory.Create(Definition(0,0,0,0,flags:1),[]).IdentificationRaw==1,"Database flag bit zero auto-identifies");
        Check(!NativeItemFactory.Create(Definition(0,0,0,0,flags:128),[]).IsIdentified,"Unrelated database flags do not auto-identify");
        var forced=NativeItemFactory.Create(Definition(0,0,0,0),[],forceIdentified:true,setUnknown11:true);
        Check(forced.IdentificationRaw==1 && forced.Unknown11==1 && forced.UncursedRaw==0 && forced.Unknown7==0,"Explicit constructor flags set only observed bytes");
        Reject(()=>NativeItemFactory.Create(Definition(254,0,0,0),[]),"Unknown preserved item mode is not executed");
        Reject(()=>NativeItemFactory.Create(prototype with {Instance=null},[]),"Legacy metadata is not silently promoted to native instance rules");

        var ingredient=created.ToIngredient("slot-a",stack);
        Check(ingredient.InstanceId=="slot-a" && ingredient.Quantity==9 && ingredient.Mode==1 && !ingredient.Identified,"Native slot adapts to recovered combination planner");
        var chargeDefinition=Definition(2,2,2,6);
        var chargeIngredient=(created with {IdentificationRaw=255}).ToIngredient("slot-b",chargeDefinition);
        Check(chargeIngredient.Quantity==1 && chargeIngredient.Identified,"Nonstack instance normalizes to one, raw count and charges remain separate");
        Reject(()=>empty.ToIngredient("empty",stack),"Empty slot is not an ingredient");
        Reject(()=>created.ToIngredient("wrong",stack with {Id=stack.Id+1}),"Definition ID mismatch rejected");
        var plan=NativeCombinations.Plan(new(prototype.Id,0,1,1,255,0),new("a",0,1,7,true),new("b",1,1,3,true),0);
        var output=NativeItemFactory.CreateCombinationOutput(stack,plan,[0,5]);
        Check(output.CountRaw==3 && output.ChargesRaw==0,"Crafted stack overrides rolled amount with batch after creation");
        Reject(()=>NativeItemFactory.CreateCombinationOutput(stack,plan,[]),"Crafted stack still requires initializer dice even when amount is overwritten");
        var chargedOutput=NativeItemFactory.CreateCombinationOutput(chargeDefinition,plan with {OutputQuantity=1},[0,5]);
        Check(chargedOutput.ChargesRaw==9 && chargedOutput.CountRaw==0,"Crafted charged result keeps newly initialized charges");
        Reject(()=>NativeItemFactory.CreateCombinationOutput(chargeDefinition,plan,[0,5]),"Nonstack craft result cannot contain a stack quantity");

        var catalog=new ItemCatalog(3,"DATABASES/ITEMS.DBS",new("loose","synthetic-native-fields",new string('a',64)),269,1,[stack]);
        var options=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower};
        Check(ItemCatalog.Parse(JsonSerializer.Serialize(catalog,options)).Items[0].Instance==stack.Instance,"Schema 3 native metadata JSON roundtrip");
        Reject(()=> (catalog with {Items=[stack with {Instance=null}]}).Validate(),"Schema 3 requires native metadata");
        Reject(()=> (catalog with {Items=[stack with {Instance=stack.Instance! with {AmountConstant=-2}}]}).Validate(),"Native fields must agree with raw signed bytes");
        Reject(()=> (catalog with {Items=[stack with {Instance=stack.Instance! with {RawRecordHex=new string('f',538)}}]}).Validate(),"Native source bytes must match record hash");
        Check(ItemCatalog.Parse(JsonSerializer.Serialize(catalog with {Schema=2,Items=[prototype with {Instance=null}]},options)).Schema==2,"Schema 2 remains supported without invented instance fields");
        return checks;
    }
}
