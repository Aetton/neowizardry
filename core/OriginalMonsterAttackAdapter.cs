using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

public static class OriginalMonsterAttackAdapter
{
    // Selected native attack byte +1; classifier table at 0x61D284, not attack-mode ordinal.
    public static MonsterAttackFatiguePolicy FatiguePolicy(int nativeAttackKind)
    {
        if(nativeAttackKind is <0 or >255)throw new ArgumentOutOfRangeException(nameof(nativeAttackKind));
        return nativeAttackKind is 7 or 13 or 14 or 15 or 21 or 25 or 33 or 43 or 49 or 50 or 52 or 53 or 54 or 56 or 59 or 85 or 86 or 87
            ?MonsterAttackFatiguePolicy.HalfHitAndFullPenetration:MonsterAttackFatiguePolicy.FullHitOnly;
    }

    // Runtime byte +0x16 and global DWORD 0x6850D5 are already resolved from the actor/context.
    public static PhysicalScoreScale ScoreScale(int nativeActorTag,int nativeGlobalSetting)
    {
        if(nativeActorTag is <0 or >255)throw new ArgumentOutOfRangeException(nameof(nativeActorTag));
        return (nativeActorTag,nativeGlobalSetting) switch {
            (1,0) or (2,2)=>PhysicalScoreScale.SixtyPercent,
            (1,2) or (2,0)=>PhysicalScoreScale.HundredFortyPercent,
            _=>PhysicalScoreScale.Unscaled};
    }
}
