namespace NeoWizardry.Core.Rules;

public enum OriginalProfession { Fighter,Lord,Valkyrie,Ranger,Samurai,Ninja,Monk,Rogue,Gadgeteer,Bard,Priest,Alchemist,Bishop,Psionic,Mage }
public sealed record ProfessionLevels(OriginalProfession Profession,int Levels);
public sealed record SingleWeaponInput(int Dexterity,int Speed,int CombatSkill,int WeaponSkill,
    ProfessionLevels[] History,int WeaponInitiative,int LoadCategory=0,bool ModernWeapon=false);
public sealed record PhysicalAttackBudget(int CombatExperience,int SkillContribution,int LoadPenalty,
    int AttackMetric,int Attacks,int SwingMetric,int MaximumSwings);

// Local character-side arithmetic recovered from the supplied executable.
// Inputs are effective values. No hand selection, conditions, RNG or world state here.
public static class OriginalCombatRules
{
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
        return new(experience,skill,penalty,attack,attack>=100?3:attack>=50?2:1,swing,swing>=100?3:swing>=67?2:1);
    }
}
