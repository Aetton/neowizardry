namespace NeoWizardry.Core.Rules;

// Ordinal attack-mode index, not the item database bitmask.
public enum OriginalAttackMode { Swing,Thrust,Bash,Berserk,Throw,Punch,Kick,Lash,Shoot }
public sealed record PhysicalModeModifiers(int Hit,int Penetration);
public sealed record PhysicalSwingAllocation(int BaseSwings,int WeaponBonus,int AllocatedSwings);

public enum OriginalProfession { Fighter,Lord,Valkyrie,Ranger,Samurai,Ninja,Monk,Rogue,Gadgeteer,Bard,Priest,Alchemist,Bishop,Psionic,Mage }
public sealed record ProfessionLevels(OriginalProfession Profession,int Levels);
public sealed record SingleWeaponInput(int Dexterity,int Speed,int CombatSkill,int WeaponSkill,
    ProfessionLevels[] History,int WeaponInitiative,int LoadCategory=0,bool ModernWeapon=false);
public sealed record PhysicalAttackBudget(int CombatExperience,int SkillContribution,int LoadPenalty,
    int AttackMetric,int Attacks,int SwingMetric,int MaximumSwings,int BaseAttackRating);

// Local character-side arithmetic recovered from the supplied executable.
// Inputs are effective values. No hand selection, conditions, RNG or world state here.
public static class OriginalCombatRules
{
    // Common allocation precedes melee/projectile dispatch in the original path.
    // One exclusive draw per attack, including maximumSwings=1. The caller owns RNG.
    // Allocation does not guarantee execution: equipment, target and resources can interrupt it.
    public static PhysicalSwingAllocation AllocateSwings(int maximumSwings,int weaponBonus,int draw)
    {
        if(maximumSwings is <1 or >3)throw new ArgumentOutOfRangeException(nameof(maximumSwings));
        if(draw<0 || draw>=maximumSwings)throw new ArgumentOutOfRangeException(nameof(draw));
        int baseSwings=draw+1;
        int allocated=checked(baseSwings+weaponBonus);
        if(allocated<1)throw new ArgumentOutOfRangeException(nameof(weaponBonus),"Adjustment leaves no executable swings");
        return new(baseSwings,weaponBonus,allocated);
    }

    // These are additive score modifiers, not final percentages or damage multipliers.
    public static PhysicalModeModifiers ModeModifiers(OriginalAttackMode mode)=>mode switch
    {
        OriginalAttackMode.Swing=>new(0,0),
        OriginalAttackMode.Thrust=>new(-10,10),
        OriginalAttackMode.Bash=>new(0,-5),
        OriginalAttackMode.Berserk=>new(-30,-30),
        OriginalAttackMode.Throw=>new(-5,5),
        OriginalAttackMode.Punch=>new(5,-5),
        OriginalAttackMode.Kick=>new(-10,0),
        OriginalAttackMode.Lash=>new(10,-10),
        OriginalAttackMode.Shoot=>new(0,10),
        _=>throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static int CombatExperience(IReadOnlyList<ProfessionLevels> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if(history.Count==0)throw new ArgumentException("Class history is required");
        int result=0;var seen=new HashSet<OriginalProfession>();
        foreach(var entry in history)
        {
            if(entry is null || !Enum.IsDefined(entry.Profession) || entry.Levels<1 || !seen.Add(entry.Profession))
                throw new ArgumentException("Invalid class history");
            int multiplier=entry.Profession<=OriginalProfession.Monk?4:entry.Profession<=OriginalProfession.Bard?3:2;
            result=checked(result+entry.Levels*multiplier);
        }
        return result;
    }
    // Scope: usable single weapon, free other hand, no special one-swing override.
    // MaximumSwings is a budget, not a promise that every swing executes.
    public static PhysicalAttackBudget SingleWeapon(SingleWeaponInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        foreach(int value in new[]{input.Dexterity,input.Speed,input.CombatSkill,input.WeaponSkill})
            if(value is <0 or >125)throw new ArgumentOutOfRangeException(nameof(input));
        if(input.LoadCategory is <0 or >4 || input.WeaponInitiative is <-128 or >127)throw new ArgumentOutOfRangeException(nameof(input));
        int experience=CombatExperience(input.History);
        int skill=((4*input.WeaponSkill+2*input.CombatSkill)/3)*10/20;
        int penalty=new[]{0,-15,-30,-60,-120}[input.LoadCategory];
        if(input.ModernWeapon)penalty/=2;
        int attack=checked((input.Dexterity+input.Speed)/2+experience+skill+penalty)/3;
        int swing=checked(input.Speed+10*input.WeaponInitiative+experience+skill+penalty)/3;
        int rating=60+(input.Dexterity/2+2*skill+penalty)/3;
        return new(experience,skill,penalty,attack,attack>=100?3:attack>=50?2:1,swing,swing>=100?3:swing>=67?2:1,rating);
    }
}
