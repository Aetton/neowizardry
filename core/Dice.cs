namespace NeoWizardry.Core;

// Prototype generator, not Wizardry 8's RNG. State is part of the save.
public static class Dice
{
    public const uint DefaultSeed = 0x4E57495A;

    public static int Roll(ref uint state, int sides)
    {
        if (sides is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(sides));
        // Use the upper 24 bits of a 32-bit LCG, rejecting the incomplete bucket.
        const uint domain = 1u << 24;
        uint limit = domain - domain % (uint)sides;
        uint value;
        do
        {
            state = unchecked(state * 1664525u + 1013904223u);
            value = state >> 8;
        } while (value >= limit);
        return (int)(value % (uint)sides) + 1;
    }
}
