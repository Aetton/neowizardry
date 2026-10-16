using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

// Explicit, authored practice scenario. Never applied while loading an old save.
public static class MeleeTraining
{
    public static ItemCatalog Catalog()
    {
        var raw=new byte[269];Encoding.Unicode.GetBytes("Practice sword").CopyTo(raw,0);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0x76),32767);
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0x78),65535);raw[0x7c]=3;
        raw[0x4c]=1;raw[0x4d]=6;raw[0x4e]=1;raw[0x6b]=1;
        string hash=Convert.ToHexString(SHA256.HashData(raw));
        var item=new ItemDefinition(0,"Practice sword",0,0,32767,65535,3,hash,0,0,0,0,0,1,6,1,0,false,0,
            new(0,0,0,0,1,0,Convert.ToHexString(raw)));
        var catalog=new ItemCatalog(3,"DATABASES/ITEMS.DBS",new("loose","synthetic-melee-training",hash),269,1,[item]);
        catalog.Validate();return catalog;
    }
    public static GameState CreateTactical(int size,int opponents,ItemCatalog catalog,SpellCatalog? spells=null,bool ai=true)
    {
        if(opponents is <1 or >8)throw new ArgumentOutOfRangeException(nameof(opponents));
        var s=Create(size,catalog,spells);var original=s.Melee!;
        var extra=ImmutableArray.CreateBuilder<TacticalOpponent>();
        int definition=Equipment.ItemId(s,s.Party[0].Id)!.Value;
        for(int n=1;n<opponents;n++)
        {
            var before=s.Items!.Items.Select(i=>i.Id).ToHashSet();s=Inventory.Add(s,definition,1,catalog);
            var owned=s.Items!.Items.Single(i=>!before.Contains(i.Id));string id=$"Training Duelist {n+1}";
            extra.Add(new(s.Enemy with {Id=id},original.Opponent,owned.Id,original.Magic?.GetValueOrDefault(s.Enemy.Id)));
        }
        var positions=s.Party.Select((h,i)=>(h.Id,Cell:new BattleCell(1,i))).ToImmutableDictionary(p=>p.Id,p=>p.Cell).Add(s.Enemy.Id,new(6,0));
        for(int n=0;n<extra.Count;n++)positions=positions.Add(extra[n].Actor.Id,new(6,n+1));
        var field=new TacticalField(8,8,positions,[new(3,3),new(3,4)],extra.ToImmutable());
        var items=s.Items!;
        foreach(var o in extra)items=ItemInventory.Move(items,o.WeaponId!,new(o.Actor.Id,Equipment.WeaponSlot),Inventory.Rules(catalog));
        s=s with {Items=items,Melee=original with {Field=field}};MeleeCombat.Validate(s,catalog);return ai?EnemyAi.Enable(s,48):s;
    }
    private static MeleeCharacterFacts Facts(OriginalProfession profession,bool opponent=false)=>new(opponent?60:80,
        new(50,opponent?60:80,opponent?60:80,70),opponent?60:80,opponent?60:80,[new(profession,10)],20,
        opponent?new(18,18,18,18,18):new(0,0,0,0,0),new(10,false,0));
    private static bool Supported(ItemDefinition item)=>item.Name.Length>0 && item.TypeCode is 0 or 1 && (item.AttackModes&1)!=0 && item.DamageDice>0 &&
        item.BonusSwings is >=0 and <=15 && item.ToHit>=0 && item.Instance is {Mode:0} fields && fields.Compile(item.RecordSha256).SpellUse==0;

    public static GameState Create(int size,ItemCatalog catalog,SpellCatalog? spells=null)
    {
        var state=Game.Create(size);
        bool magic=spells is not null && spells.Spells.Any(RestoredMagic.SupportedSpell);
        if(magic)state=state with {Party=state.Party.SetItem(0,state.Party[0] with {Name="Faerie Bishop",Profession="Bishop",Spellbook=[],Mana=[new("Fire",42,42),new("Water",42,42),new("Divine",42,42)]})};
        var facts=ImmutableDictionary.CreateBuilder<string,MeleeCharacterFacts>();
        foreach(var hero in state.Party)
        {
            var weapon=catalog.Items.OrderBy(i=>i.Id).FirstOrDefault(i=>Supported(i) && Equipment.Denial(hero,i) is null)
                ??throw new InvalidOperationException($"No ordinary Swing weapon available for {hero.Name}");
            var before=state.Items!.Items.Select(i=>i.Id).ToHashSet();state=Inventory.Add(state,weapon.Id,1,catalog);
            var instance=state.Items!.Items.Single(i=>!before.Contains(i.Id));
            state=Equipment.EquipInstance(state,hero.Id,instance.Id,catalog);
            facts.Add(hero.Id,Facts(Enum.Parse<OriginalProfession>(hero.Profession)));
        }
        // The sparring partner uses a second owned copy of the first hero's eligible weapon.
        // Its effective class is the same authored class, not a monster disguised as a hero.
        int opponentWeapon=Equipment.ItemId(state,state.Party[0].Id)!.Value;
        var ids=state.Items!.Items.Select(i=>i.Id).ToHashSet();state=Inventory.Add(state,opponentWeapon,1,catalog);
        string opponentId=state.Items!.Items.Single(i=>!ids.Contains(i.Id)).Id;
        var enemy=state.Enemy with {Id="Training Duelist",Hp=48,Power=0,LootItemId=null};
        var encounter=new MeleeEncounter(MeleeCombat.Ruleset,catalog.Provenance.Sha256,facts.ToImmutable(),
            Facts(Enum.Parse<OriginalProfession>(state.Party[0].Profession),true),opponentId);
        state=state with {Enemy=enemy,Melee=encounter,Items=ItemInventory.Move(state.Items!,opponentId,new(enemy.Id,Equipment.WeaponSlot),Inventory.Rules(catalog))};
        if(magic)
        {
            RestoredMagicFacts Profile(bool caster)=>new(spells!.Provenance.Sha256,caster?40:0,
                Enumerable.Repeat(caster?40:0,6).ToImmutableArray(),10,10,false,0,
                caster?spells.Spells.Where(RestoredMagic.SupportedSpell).Select(s=>s.Id).ToImmutableArray():[],10,[0,0,0,0,0,0]);
            state=state with {Melee=state.Melee! with {Magic=state.Party.ToImmutableDictionary(h=>h.Id,h=>Profile(h.Id==state.Party[0].Id)).Add(enemy.Id,Profile(false))}};
        }
        MeleeCombat.Validate(state,catalog);return state;
    }
}
