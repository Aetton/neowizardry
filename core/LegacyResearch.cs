namespace NeoWizardry.Core;

// Candidate model from peddroelm's own reported experiments, section 5.4:
// https://steamcommunity.com/sharedfiles/filedetails/?id=899973393
// Research only: unused by Game.Attack; no claim of binary-level verification.
public static class LegacyResearch
{
    public static decimal SingleClassAttackScore(int speed, int dexterity,
        int combatSkill, int weaponSkill, int level, int professionMultiplier)
    {
        foreach (var value in new[] { speed, dexterity, combatSkill, weaponSkill })
            if (value is < 0 or > 125) throw new ArgumentOutOfRangeException(nameof(value));
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        if (professionMultiplier is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(professionMultiplier));
        // Preserve the guide's decimal coefficients rather than silently substituting fractions.
        // White encumbrance, no dual wielding, no shield; multiclass is out of scope.
        return speed + dexterity + 0.666666m*combatSkill + 1.333333m*weaponSkill + 2m*level*professionMultiplier;
    }
}
