namespace NeoWizardry.Core.Rules;

public static class OriginalFatigueRules
{
    // Current stamina and resolved usable maximum, after native drain/max-stat policy.
    // The original ratio first truncates remaining percentage, then subtracts from 100.
    public static int Category(int currentStamina,int maximumStamina)
    {
        if(maximumStamina<1 || currentStamina<0 || currentStamina>maximumStamina)
            throw new ArgumentOutOfRangeException(nameof(currentStamina));
        int fatigue=100-checked(currentStamina*100)/maximumStamina;
        return fatigue<50?0:fatigue<70?1:fatigue<85?2:fatigue<95?3:4;
    }
    public static int ScorePenalty(int category)=>category is >=0 and <=4?new[]{0,5,10,20,40}[category]:
        throw new ArgumentOutOfRangeException(nameof(category));
}
