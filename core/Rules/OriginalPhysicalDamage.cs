namespace NeoWizardry.Core.Rules;

public enum PhysicalStrengthScaling { None,Half,Full }
public sealed record PhysicalDamageDice(int Constant,int Dice,int Sides);
public sealed record PhysicalDamageResult(int Rolled,int Scaled,int Damage);
public sealed record PhysicalHandBonuses(int ToHit,int DamagePercent);

public static class OriginalPhysicalDamage
{
    // Ordinary primary-hand contribution, no dual wielding, buffs or launcher percentage.
    // The caller resolves the native strength policy (including weapon/icon exceptions).
    public static PhysicalHandBonuses PrimaryHand(int strength,int dexterity,int senses,int weaponToHit,
        PhysicalStrengthScaling strengthScaling,int loadCategory,bool modernWeapon=false)
    {
        foreach(int value in new[]{strength,dexterity,senses})
            if(value is <0 or >125)throw new ArgumentOutOfRangeException(nameof(strength));
        if(weaponToHit is <-256 or >254 || !Enum.IsDefined(strengthScaling) || loadCategory is <0 or >4)
            throw new ArgumentOutOfRangeException(nameof(strengthScaling));
        int divisor=strengthScaling==PhysicalStrengthScaling.Half?2:1;
        int strengthHit=strengthScaling==PhysicalStrengthScaling.None?0:(strength-50)/(10*divisor);
        int hit=weaponToHit+strengthHit+(dexterity-50)/10;
        if(senses>70)hit+=(senses-70)/10;
        else if(senses<30)hit-=(30-senses)/10;
        int load=new[]{0,-1,-2,-4,-8}[loadCategory];
        if(modernWeapon)load/=2;
        hit+=load;
        int percent=strengthScaling==PhysicalStrengthScaling.None?0:(strength>=50?2*(strength-50):strength-50)/divisor;
        return new(hit,percent);
    }

    // Re-roll the entire signed-constant dice expression for each resolved repetition.
    // Random values are zero-based and externally owned; no RNG sequence is emulated.
    public static PhysicalDamageResult Roll(PhysicalDamageDice profile,int repetitions,int handDamagePercent,
        int effectDamagePercent,int absorptionPercent,IReadOnlyList<int> draws)
    {
        ArgumentNullException.ThrowIfNull(profile);ArgumentNullException.ThrowIfNull(draws);
        if(profile.Constant is <short.MinValue or >short.MaxValue || profile.Dice is <0 or >255 ||
            profile.Sides is <0 or >255 || (profile.Dice>0 && profile.Sides==0) || repetitions<1)
            throw new ArgumentOutOfRangeException(nameof(profile));
        if(effectDamagePercent is <-128 or >127)throw new ArgumentOutOfRangeException(nameof(effectDamagePercent));
        int needed=checked(repetitions*profile.Dice);
        if(draws.Count!=needed)throw new ArgumentException("Supply exactly one resolved draw per die");
        int rolled=checked(profile.Constant*repetitions);
        foreach(int draw in draws)
        {
            if(draw<0 || draw>=profile.Sides)throw new ArgumentOutOfRangeException(nameof(draws));
            rolled=checked(rolled+draw+1);
        }
        int scaled=Math.Max(1,Scale(rolled,checked(100+handDamagePercent+effectDamagePercent)));
        int damage=Math.Max(0,Scale(scaled,checked(100-absorptionPercent)));
        return new(rolled,scaled,damage);
    }

    // Native signed division toward zero after adding 50, including negative intermediates.
    private static int Scale(int amount,int percent)=>checked((checked(amount*percent)+50)/100);
}
