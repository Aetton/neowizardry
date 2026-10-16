namespace NeoWizardry.Core.Rules;

public sealed record CharacterInitiativeFacts(int Level,bool SnakespeedEnabled,int SnakespeedSkill,int Adjustment=0,int LoadCategory=0);

public static class OriginalInitiative
{
    // Wiz8.exe 4EE000: effective unsigned SPD/SEN, total level, enabled expert skill,
    // signed runtime adjustment and resolved encumbrance. No floating-point breakpoints.
    public static int Character(int speed,int senses,CharacterInitiativeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if(speed is <0 or >125 || senses is <0 or >125 || facts.Level is <1 or >255 ||
            facts.SnakespeedSkill is <0 or >125 || facts.Adjustment is <-128 or >127 || facts.LoadCategory is <0 or >4)
            throw new ArgumentOutOfRangeException(nameof(facts));
        return (facts.Level+1)/2+speed/5+senses/5-10+(facts.SnakespeedEnabled?facts.SnakespeedSkill/10+1:0)
            +facts.Adjustment-new[]{0,1,2,4,8}[facts.LoadCategory];
    }
    // 4E8A23..4E8A9A: one exclusive-4 draw, add active hand's initiative only for
    // weapon orders, clamp the score before converting to an ascending queue time.
    public static int FirstTime(int initiative,int? weaponInitiative,int roll)
    {
        if(roll is <0 or >3 || weaponInitiative is <-128 or >127)throw new ArgumentOutOfRangeException(nameof(roll));
        return 90-(int)Math.Clamp((long)initiative+(weaponInitiative??0)+roll,-10,89);
    }
    // Ordinary single active hand continuation, 4EAD6D..4EAD9B. The native actor's
    // timeline anchor is supplied explicitly. Dual hands and quantized mode excluded.
    public static int? NextAttackTime(int scheduledTime,int timelineAnchor,int currentTime,int remainingAttacks)
    {
        if(remainingAttacks is <1 or >3)throw new ArgumentOutOfRangeException(nameof(remainingAttacks));
        return NextTime(scheduledTime,timelineAnchor,currentTime,remainingAttacks);
    }
    // Laboratory monster timeline uses the same spacing arithmetic with a byte-sized group budget.
    public static int? NextMonsterGroupTime(int scheduledTime,int timelineAnchor,int currentTime,int remainingGroups)
    {
        if(remainingGroups is <1 or >254)throw new ArgumentOutOfRangeException(nameof(remainingGroups));
        return NextTime(scheduledTime,timelineAnchor,currentTime,remainingGroups);
    }
    private static int? NextTime(int scheduledTime,int timelineAnchor,int currentTime,int remainingAttacks)
    {
        if(scheduledTime is <1 or >100 || currentTime<scheduledTime || currentTime>100 ||
            timelineAnchor<0 || timelineAnchor>currentTime)
            throw new ArgumentOutOfRangeException(nameof(currentTime));
        int next=scheduledTime+(timelineAnchor-currentTime+100)/(remainingAttacks+1);
        return next<=100?Math.Max(currentTime,next):null;
    }
}
