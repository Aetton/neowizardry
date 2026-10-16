using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public sealed record MonsterAttackEffectReference(int Index,int Probability,AttackConditionRoute? Condition);
public sealed record MonsterAttackReference(int Slot,int NativeKind,int NativeCategory,int Rating,
    ImmutableArray<OriginalAttackMode> Modes,PhysicalDamageDice Damage,int SecondaryParameter,ImmutableArray<MonsterAttackEffectReference> Effects);
public sealed record MonsterEffectReference(int DefinitionLevel,int EffectStrength,ImmutableArray<MonsterAttackReference> Attacks);

// Read-only decoding is allowed for blocked rows; never promotes them to executable profiles.
public static class MonsterAttackInspector
{
    public static MonsterEffectReference Inspect(OriginalMonster row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if(row.RawRecordHex is not {Length:1326} || !row.RawRecordHex.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid monster reference bytes");
        var raw=Convert.FromHexString(row.RawRecordHex);
        if(!Convert.ToHexString(SHA256.HashData(raw)).Equals(row.RecordSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Monster reference digest mismatch");
        var attacks=ImmutableArray.CreateBuilder<MonsterAttackReference>();
        for(int slot=0;slot<3;slot++)
        {
            int at=0xe7+slot*34;if(raw[at]==0)continue;
            var effects=Enumerable.Range(0,16).Select(i=>new MonsterAttackEffectReference(i,raw[at+5+i],OriginalAttackEffects.Route(i))).ToImmutableArray();
            attacks.Add(new(slot,raw[at+1],raw[at+3],raw[at+4],OriginalMonsterAttackSequence.Modes(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(at+21))).ToImmutableArray(),
                new(BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(at+23)),raw[at+25],raw[at+26]),raw[at+27],effects));
        }
        return new(raw[0x24f],OriginalAttackEffects.Strength(raw[0x24f]),attacks.ToImmutable());
    }
}
