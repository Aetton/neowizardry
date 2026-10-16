using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

// Complete private record snapshot; derived fields cannot be independently edited in saves.
public sealed record MonsterRecordSnapshot(int DefinitionId,string DatabaseSha256,string RecordSha256,string RawRecordHex);
public sealed record MonsterCombatant(MonsterRecordSnapshot Record,int MaxHp);
public sealed record CompiledMonster(string Label,PhysicalDamageDice Health,PhysicalCombatAttributes Attributes,int Initiative,
    int Groups,int MaximumSwings,int Rating,PhysicalDamageDice Damage,MonsterDefenseDefinition Defense);
public static class MonsterCombat
{
    private static readonly ConditionalWeakTable<MonsterRecordSnapshot,CompiledMonster> Cache=new();
    // Reviewed, ordinary one-slot Thrust slice. No guessed status/spell/ranged eligibility.
    private static readonly ImmutableDictionary<int,string> Allowed=new Dictionary<int,string>{
        [18]="fdae11562fdd033674d82346a270bc5e793cd732da208528e4e2afb20e606d1b",
        [282]="1910b7650bb045cef83b3acea628a10607a4e8cae2dc5b55fb5a6554cf789c74"}.ToImmutableDictionary();
    private static bool Hash(string? value)=>value is {Length:64} && value.All(Uri.IsHexDigit);
    public static bool Supported(OriginalMonster m)=>Allowed.TryGetValue(m.Id,out var hash) && hash.Equals(m.RecordSha256,StringComparison.OrdinalIgnoreCase);
    public static CompiledMonster Compile(MonsterRecordSnapshot record)=>Cache.GetValue(record,r=>
    {
        if(!Hash(r.DatabaseSha256) || !Hash(r.RecordSha256) || r.RawRecordHex is not {Length:1326} || !r.RawRecordHex.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid monster combat record");
        byte[] raw=Convert.FromHexString(r.RawRecordHex);
        if(!Convert.ToHexString(SHA256.HashData(raw)).Equals(r.RecordSha256,StringComparison.OrdinalIgnoreCase) ||
            !(Allowed.TryGetValue(r.DefinitionId,out var allowed) && allowed.Equals(r.RecordSha256,StringComparison.OrdinalIgnoreCase)) &&
            !(r.DefinitionId==0 && r.RecordSha256.Equals(FixtureHash,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Unsupported or modified ordinary monster record");
        string Text(int offset){var value=new UnicodeEncoding(false,false,true).GetString(raw,offset,48);int end=value.IndexOf('\0');if(end<0)throw new InvalidDataException("Unterminated monster label");return value[..end];}
        for(int i=0;i<4;i++)_=Text(i*48);
        PhysicalDamageDice DiceAt(int offset)=>new(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(offset)),raw[offset+2],raw[offset+3]);
        var hp=DiceAt(0xd6);var damage=DiceAt(0xe7+0x17);
        if(raw[0xcf]!=0 || hp.Constant<1 || hp.Dice>0 && hp.Sides==0 || raw[0xe5]==0 || raw[0xe6]==0 || raw[0xe7]!=1 || raw[0xe8]!=0 || raw[0xea]!=0 || raw[0xeb]<110 ||
            raw[(0xe7+5)..(0xe7+21)].Any(x=>x!=0) || BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0xfc))!=2 || damage.Dice>0 && damage.Sides==0 ||
            raw[0x109]!=0 || raw[0x12b]!=0)throw new InvalidDataException("Monster is outside the ordinary physical slice");
        int Stat(int offset)=>Math.Clamp((int)raw[offset],1,125); // Neutral signed modifiers; native 4E5D00 clamps effective stats.
        MonsterLocationValues Zones(int offset,bool signed)=>new(signed?(sbyte)raw[offset]:raw[offset],signed?(sbyte)raw[offset+1]:raw[offset+1],signed?(sbyte)raw[offset+2]:raw[offset+2],signed?(sbyte)raw[offset+3]:raw[offset+3],signed?(sbyte)raw[offset+4]:raw[offset+4],signed?(sbyte)raw[offset+5]:raw[offset+5],signed?(sbyte)raw[offset+6]:raw[offset+6]);
        var a=raw[0x16d..0x176].Select(b=>(int)(sbyte)b).ToArray();
        var defense=new MonsterDefenseDefinition((sbyte)raw[0x15d],Zones(0x15f,false),Zones(0x166,true),new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8]));
        return new(Text(0),hp,new(Stat(0xd2),Stat(0xd3),Stat(0xd4),Stat(0xd5)),raw[0xe4],raw[0xe5],raw[0xe6],raw[0xeb],damage,defense);
    });
    public static MonsterCombatant? Actor(GameState state,string id)=>state.Melee?.Field?.Monsters?.GetValueOrDefault(id);
    public static void Validate(GameState s)
    {
        var f=s.Melee!.Field!;if(f.Monsters is not {} monsters)return;
        var enemies=TacticalCombat.Enemies(s).ToArray();
        if(monsters.Count!=enemies.Length || s.Melee.Opponent is not null || s.Melee.OpponentWeaponId is not null || s.Melee.Magic is not null ||
            f.Opponents.Any(o=>o.Facts is not null || o.WeaponId is not null || o.Magic is not null) || f.Brains is null)throw new InvalidDataException("Mixed character/monster execution facts");
        foreach(var enemy in enemies)
        {
            if(!monsters.TryGetValue(enemy.Id,out var actor) || actor is null || actor.Record is null)throw new InvalidDataException("Missing monster actor");
            var d=Compile(actor.Record);long minimum=d.Health.Constant+d.Health.Dice,maximum=d.Health.Constant+(long)d.Health.Dice*d.Health.Sides;
            if(actor.MaxHp<minimum || actor.MaxHp>maximum || enemy.Hp>actor.MaxHp || enemy.Power!=0 || enemy.XpReward!=0 || enemy.LootItemId is not null || enemy.Speed!=d.Attributes.Speed ||
                f.Brains[enemy.Id].MaxHp!=actor.MaxHp)throw new InvalidDataException("Invalid monster HP or neutral scenario state");
        }
        if(monsters.Values.Select(m=>m.Record.DatabaseSha256).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1)throw new InvalidDataException("Mixed monster databases");
    }
    public static GameState Create(int size,int count,ItemCatalog items,MonsterCatalog monsters,int definitionId)
    {
        monsters.Validate();var row=monsters.Monsters.Single(m=>m.Id==definitionId);
        if(!Supported(row) && !(row.Id==0 && row.RecordSha256.Equals(FixtureHash,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("This monster's full ordinary attack slice is not connected");
        var record=new MonsterRecordSnapshot(row.Id,monsters.DatabaseSha256,row.RecordSha256,row.RawRecordHex);var compiled=Compile(record);
        var s=MeleeTraining.CreateTactical(size,count,items,ai:false);var f=s.Melee!.Field!;uint rng=s.RandomState;
        var native=ImmutableDictionary.CreateBuilder<string,MonsterCombatant>();var brains=ImmutableDictionary.CreateBuilder<string,EnemyBrain>();
        var ids=TacticalCombat.Enemies(s).Select(e=>e.Id).ToArray();var replacements=new Dictionary<string,Enemy>();
        int ordinal=0;
        foreach(var oldId in ids)
        {
            string id=$"monster-{++ordinal}";
            int hp=compiled.Health.Constant;for(int n=0;n<compiled.Health.Dice;n++)hp=checked(hp+Dice.Roll(ref rng,compiled.Health.Sides));
            native.Add(id,new(record,hp));brains.Add(id,new(new((EnemyTactic)((native.Count-1)%4)),hp));
            replacements.Add(oldId,new(id,hp,compiled.Attributes.Speed,0,0));
        }
        var weapons=f.Opponents.Select(o=>o.WeaponId).Append(s.Melee.OpponentWeaponId).ToHashSet();
        s=s with {Enemy=replacements[s.Enemy.Id],RandomState=rng,Items=s.Items! with {Items=s.Items!.Items.Where(i=>!weapons.Contains(i.Id)).ToImmutableArray()},
            Melee=s.Melee with {Opponent=null,OpponentWeaponId=null,Field=f with {Opponents=f.Opponents.Select(o=>o with {Actor=replacements[o.Actor.Id],Facts=null,WeaponId=null}).ToImmutableArray(),Positions=f.Positions.ToImmutableDictionary(p=>replacements.TryGetValue(p.Key,out var e)?e.Id:p.Key,p=>p.Value),Monsters=native.ToImmutable(),Brains=brains.ToImmutable()}}};
        MeleeCombat.Validate(s,items);return s;
    }
    private static readonly byte[] FixtureRaw=BuildFixture();
    private static readonly string FixtureHash=Convert.ToHexString(SHA256.HashData(FixtureRaw));
    private static byte[] BuildFixture()
    {
        var b=new byte[663];Encoding.Unicode.GetBytes("Practice creature").CopyTo(b,0);
        b[0xd1]=45;b[0xd2]=40;b[0xd3]=55;b[0xd4]=60;b[0xd5]=50;
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(0xd6),10);b[0xd8]=2;b[0xd9]=4;b[0xe4]=15;b[0xe5]=2;b[0xe6]=2;
        b[0xe7]=1;b[0xeb]=140;b[0xfc]=2;b[0x100]=1;b[0x101]=4;b[0x15d]=5;b[0x15f+3]=100;return b;
    }
    public static MonsterCatalog FixtureCatalog()
    {
        var data=new byte[667];BinaryPrimitives.WriteInt32LittleEndian(data,1);FixtureRaw.CopyTo(data,4);string hash=Convert.ToHexString(SHA256.HashData(data));
        return new(1,"DATABASES/MONSTERS.DBS",new("loose","synthetic-monster-combat",hash),663,1,hash,[new(0,"Practice creature",["Practice creature","","",""],FixtureHash,Convert.ToHexString(FixtureRaw))]);
    }
}
