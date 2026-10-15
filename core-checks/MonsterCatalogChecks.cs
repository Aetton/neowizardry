using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using NeoWizardry.Core;

static class MonsterCatalogChecks
{
    public static int Run(MonsterCatalog? supplied=null)
    {
        int count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
        void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,message);}
        var raw=new byte[663];var slots=new[]{"Fixture# internal","Fixtures","Creature","Creatures"};
        for(int i=0;i<4;i++)Encoding.Unicode.GetBytes(slots[i]).CopyTo(raw,i*48);
        string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
        var table=new byte[667];BinaryPrimitives.WriteInt32LittleEndian(table,1);raw.CopyTo(table,4);
        var row=new OriginalMonster(0,slots[0],slots.ToImmutableArray(),Hash(raw),Convert.ToHexString(raw));
        var fixture=new MonsterCatalog(1,"DATABASES/MONSTERS.DBS",new("loose","synthetic-monsters",Hash(table)),663,1,Hash(table),[row]);
        fixture.Validate();Check(true,"Synthetic monster boundaries and text evidence");
        Reject(()=>(fixture with {Monsters=[row with {Label="Invented label"}]}).Validate(),"Typed label cannot disagree with text slots");
        Reject(()=>(fixture with {Monsters=[row with {TextSlots=row.TextSlots.SetItem(1,"Changed")}]}).Validate(),"Typed text cannot disagree with raw source");
        Reject(()=>(fixture with {Monsters=[row with {RecordSha256=new string('0',64)}]}).Validate(),"Record hash is required");
        Reject(()=>(fixture with {DatabaseSha256=new string('0',64)}).Validate(),"Complete native table hash is verified");
        Reject(()=>(fixture with {Monsters=[row with {Id=1}]}).Validate(),"Database order determines stable IDs");
        if(supplied is not null)
        {
            supplied.Validate();Check(supplied.Monsters.Length==supplied.Count,"Private original monster catalog validates every imported row");
            Check(supplied.Monsters.Any(m=>m.TextSlots.Any(t=>t.Contains("Slime"))),"Native monster names are searchable");
        }
        return count;
    }
}
