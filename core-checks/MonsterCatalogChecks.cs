using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using NeoWizardry.Core;
using NeoWizardry.Core.Rules;

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
        var combatRaw=(byte[])raw.Clone();combatRaw[0x15d]=3;combatRaw[0x15e]=9;combatRaw[0x162]=100;combatRaw[0x169]=4;combatRaw[0x16d]=254;
        combatRaw.CopyTo(table,4);
        var fields=new ImportedMonsterDefense(3,9,[0,0,0,100,0,0,0],[0,0,0,4,0,0,0],[-2,0,0,0,0,0,0,0,0]);
        var combat=fixture with {Schema=2,DatabaseSha256=Hash(table),Monsters=[row with {RawRecordHex=Convert.ToHexString(combatRaw),RecordSha256=Hash(combatRaw),Defense=fields}]};
        combat.Validate();var definition=combat.Defense(0);
        Check(ReferenceEquals(definition,combat.Defense(0)),"Immutable monster defense is compiled once per catalog identity");
        var location=OriginalMonsterDefense.SelectLocation(definition.LocationWeights,99);
        var defense=OriginalMonsterDefense.Against(definition,new(0,0,0,0,false),location.Location,OriginalAttackMode.Swing,new(false,false,false,false));
        Check(location.Location==MonsterHitLocation.Zone3 && defense.Hit==7 && defense.Penetration==8,"Imported signed mode/zone armor executes the recovered monster defense path");
        Reject(()=>(combat with {Monsters=[combat.Monsters[0] with {Defense=fields with {ArmorClass=4}}]}).Validate(),"Typed AC cannot disagree with raw evidence");
        Reject(()=>(combat with {Monsters=[combat.Monsters[0] with {Defense=fields with {ModeArmor=[254,0,0,0,0,0,0,0,0]}}]}).Validate(),"Signed mode byte cannot be replaced with its unsigned value");
        Reject(()=>(combat with {Monsters=[combat.Monsters[0] with {Defense=fields with {LocationWeights=[100]}}]}).Validate(),"Incomplete zone table rejected");
        Reject(()=>(combat with {Monsters=[combat.Monsters[0] with {Defense=null}]}).Validate(),"Schema 2 requires explicit defense evidence");
        if(supplied is not null)
        {
            supplied.Validate();Check(supplied.Monsters.Length==supplied.Count,"Private original monster catalog validates every imported row");
            if(supplied.Schema==2)foreach(var monster in supplied.Monsters)
            {
                var d=supplied.Defense(monster.Id);
                for(int draw=0;draw<100;draw++)
                {
                    var selected=OriginalMonsterDefense.SelectLocation(d.LocationWeights,draw);
                    foreach(var mode in Enum.GetValues<OriginalAttackMode>())_=OriginalMonsterDefense.Against(d,new(0,0,0,0,false),selected.Location,mode,new(false,false,false,false));
                }
                Check(true,"Every original monster definition composes with location and all nine mode defenses");
            }
            Check(supplied.Monsters.Any(m=>m.TextSlots.Any(t=>t.Contains("Slime"))),"Native monster names are searchable");
        }
        return count;
    }
}
