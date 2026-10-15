using System.Text.Json;
using NeoWizardry.Core;

static class CatalogIntegrityChecks
{
    internal static int Run(ItemDefinition prototype,OriginalSpell spell)
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
        void Reject(Action action,string name)
        {
            bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,name);
        }
        var item=SourceFixture.Item(prototype with {Instance=new(1,0,0,0,10,0,new string('0',538))});
        var catalog=new ItemCatalog(3,"DATABASES/ITEMS.DBS",new("loose","synthetic-integrity",new string('a',64)),269,1,[item]);
        catalog.Validate();
        ItemDefinition[] mutations=[item with {Name="Changed"},item with {TypeCode=item.TypeCode==0?1:0},
            item with {IconIndex=item.IconIndex^1},item with {ProfessionMask=item.ProfessionMask^1},
            item with {RaceMask=item.RaceMask^1},item with {GenderMask=item.GenderMask^1},item with {UsageSkill=item.UsageSkill^1},
            item with {RangeCode=item.RangeCode^1},item with {Initiative=item.Initiative==127?126:127},item with {ToHit=item.ToHit==127?126:127},
            item with {DamageConstant=item.DamageConstant==32767?-32768:32767},item with {DamageDice=item.DamageDice^1},
            item with {DamageSides=item.DamageSides^1},item with {AttackModes=item.AttackModes^1},item with {WeightTenthsLb=item.WeightTenthsLb^1},
            item with {Cursed=!item.Cursed},item with {BonusSwings=item.BonusSwings^1},item with {RecordSha256=new string('b',64)}];
        foreach(var changed in mutations)
        {
            var altered=catalog with {Items=[changed]};
            Reject(altered.Validate,"Catalog rejects item field detached from valid source hash");
            Reject(()=>Inventory.Add(Game.Create(1),changed.Id,1,altered),"Inventory rejects unvalidated content before ownership changes");
        }
        var rules=Inventory.Rules(catalog);
        Check(ReferenceEquals(rules,Inventory.Rules(catalog)),"Inventory reuses compiled rules");
        var input=new[]{item};var isolated=catalog with {Items=[..input]};input[0]=mutations[0];
        Check(isolated.Find(item.Id)==item,"Catalog does not retain caller array aliases");
        var snapshot=catalog.Items;var changedItem=SourceFixture.Item(item with {Instance=item.Instance! with {MaxStack=7}});
        var changedCatalog=catalog with {Items=[changedItem]};
        Check(Inventory.Rules(changedCatalog)[item.Id].StackLimit==7 && rules[item.Id].StackLimit==10 && snapshot[0]==item,
            "Record copy compiles its own rules without changing the prior catalog");
        var malformed=catalog with {Count=2};Reject(()=>Inventory.Rules(malformed),"Cache cannot bypass changed catalog metadata");
        var options=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower};
        Check(ItemCatalog.Parse(JsonSerializer.Serialize(catalog,options)).Find(item.Id)==item,"Immutable item catalog JSON roundtrips");
        Check(ParallelEnumerable.Range(0,32).All(_=>ReferenceEquals(rules,Inventory.Rules(catalog))),"Concurrent readers share compiled rules");
        var spells=new SpellCatalog(1,"DATABASES/SPELLTABLES.DBS",catalog.Provenance,"DATABASES/SPELLDESC.DBS",catalog.Provenance,704,1,[spell]);
        OriginalSpell[] changes=[spell with {Name="Changed"},spell with {Cost=spell.Cost^1},spell with {Level=9999},spell with {RealmCode=-123},
            spell with {RangeCode=spell.RangeCode^1},spell with {TargetCode=spell.TargetCode^1},spell with {UsabilityCode=spell.UsabilityCode^1},
            spell with {SchoolFlags=[spell.SchoolFlags[0]^1,..spell.SchoolFlags.Skip(1)]},spell with {SchoolFlags=[spell.SchoolFlags[0],spell.SchoolFlags[1]^1,spell.SchoolFlags[2],spell.SchoolFlags[3]]},
            spell with {SchoolFlags=[spell.SchoolFlags[0],spell.SchoolFlags[1],spell.SchoolFlags[2]^1,spell.SchoolFlags[3]]},spell with {SchoolFlags=[..spell.SchoolFlags.Take(3),spell.SchoolFlags[3]^1]},
            spell with {DurationBaseRaw=spell.DurationBaseRaw^1},spell with {DurationPerPowerRaw=spell.DurationPerPowerRaw^1},spell with {DamageFlagRaw=spell.DamageFlagRaw^1},
            spell with {DamageConstant=spell.DamageConstant^1},spell with {DamageDice=spell.DamageDice^1},spell with {DamageSides=spell.DamageSides^1}];
        foreach(var changed in changes)Reject(()=>(spells with {Spells=[changed]}).Validate(),"Spell fields must match source bytes");
        var unknown=SourceFixture.Spell(spell with {Level=9999,RealmCode=-123});
        (spells with {Spells=[unknown]}).Validate();checks++; // Preserve unknown native values when source-backed.
        (spells with {Spells=[spell with {Description="Separate description source"}]}).Validate();checks++;
        return checks;
    }
}
