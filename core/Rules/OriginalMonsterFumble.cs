namespace NeoWizardry.Core.Rules;

public static class OriginalMonsterFumble
{
    // Native 5403CB..54045D: round((100-score)^3*1e-5), then floor(N*base/10), clamp 0..100.
    // Scope is the neutral definition-rating/mode domain; candidate eligibility is supplied externally.
    public static int Threshold(int untargetedScore,int candidates)
    {
        if(untargetedScore is <-30 or >265 || candidates is <0 or >1024)throw new ArgumentOutOfRangeException(nameof(untargetedScore));
        if(untargetedScore>=100)return 0;
        long gap=100-untargetedScore;long basis=(gap*gap*gap+50000)/100000;
        return (int)Math.Clamp(basis*candidates/10,0,100);
    }
    public static bool Redirects(int threshold,int contactDraw)
    {
        if(threshold is <0 or >100 || contactDraw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(threshold));
        // Natural 1..5 bypasses redirection. A natural miss may redirect and receive a fresh roll.
        return contactDraw>=5 && contactDraw+1>100-threshold;
    }
}
