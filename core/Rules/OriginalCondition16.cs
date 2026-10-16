namespace NeoWizardry.Core.Rules;

// Selected native condition 16: 522FE0/523C00 merge by maximum, 5236A0 subtracts elapsed units.
// Its aggregate producer 50EAC0 sets incapacitation byte +45 (50EBE5); no stat delta.
public static class OriginalCondition16
{
    public static int Merge(int current,int incoming)
    {
        if(current is <0 or >12 || incoming is <2 or >12)throw new ArgumentOutOfRangeException(nameof(current));
        return Math.Max(current,incoming);
    }
    public static int Advance(int current,int elapsed)
    {
        if(current is <0 or >12 || elapsed<0)throw new ArgumentOutOfRangeException(nameof(current));
        return Math.Max(0,current-elapsed);
    }
}
