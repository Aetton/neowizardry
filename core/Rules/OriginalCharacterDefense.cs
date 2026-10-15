namespace NeoWizardry.Core.Rules;

public enum CharacterHitLocation { Head,Torso,Legs,Hands,Feet }
public sealed record BodyArmor(int Head,int Torso,int Legs,int Hands,int Feet)
{
    public int At(CharacterHitLocation location)=>location switch {
        CharacterHitLocation.Head=>Head,CharacterHitLocation.Torso=>Torso,CharacterHitLocation.Legs=>Legs,
        CharacterHitLocation.Hands=>Hands,CharacterHitLocation.Feet=>Feet,_=>throw new ArgumentOutOfRangeException(nameof(location))};
}
public sealed record PreparedCharacterArmor(BodyArmor Body,int ShieldArmor,int OtherArmor);
public sealed record CharacterDefenseStats(int Speed,int Stealth,int Shield,int Reflexion,bool ReflexionEnabled);
public sealed record CharacterDefenseState(int EncumbranceCategory,int FatigueCategory,bool DefensiveAction,
    bool SuppressDerivedBonuses,bool Incapacitated);
public sealed record CharacterDefenseEffects(int SpellArmor,int Penetration,int Conditions,bool NaturalArmorBonus);
public sealed record CharacterDefenseContributions(int Natural,int Speed,int Stealth,int Shield,int OtherEquipment,
    int SpellArmor,int Penetration,int Encumbrance,int Conditions,int Fatigue,int Defensive,int Reflexion);
public sealed record CharacterDefenseResult(int Base,BodyArmor Locations,int DisplayedAverage,
    CharacterDefenseContributions Contributions,bool Incapacitated);
// These booleans are already-resolved native context predicates, not inferred status names.
public sealed record CharacterDefenseContext(bool RemoveSpeedAndShield,bool FourPointPenalty,bool MeleeVulnerability);
public sealed record TargetDefenseThresholds(int Hit,int Penetration,int ContextAdjustment);

public static class OriginalCharacterDefense
{
    public static CharacterHitLocation SelectLocation(int draw)
    {
        if(draw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(draw));
        return draw<15?CharacterHitLocation.Head:draw<55?CharacterHitLocation.Torso:
            draw<85?CharacterHitLocation.Legs:draw<95?CharacterHitLocation.Hands:CharacterHitLocation.Feet;
    }

    // Effective stats, selected active equipment and resolved state/effects only.
    // Character creation, skill training, native save parsing and status/action producers are external.
    public static CharacterDefenseResult Build(CharacterDefenseStats stats,PreparedCharacterArmor equipment,
        CharacterDefenseState state,CharacterDefenseEffects effects)
    {
        ArgumentNullException.ThrowIfNull(stats);ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(equipment.Body);ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(effects);
        foreach(int value in new[]{stats.Speed,stats.Stealth,stats.Shield,stats.Reflexion})
            if(value is <0 or >125)throw new ArgumentOutOfRangeException(nameof(stats));
        foreach(int value in new[]{effects.SpellArmor,effects.Penetration,effects.Conditions})
            if(value is <-128 or >127)throw new ArgumentOutOfRangeException(nameof(effects));
        if(state.EncumbranceCategory is <0 or >4 || state.FatigueCategory is <0 or >4)
            throw new ArgumentOutOfRangeException(nameof(state));
        checked
        {
            bool active=!state.SuppressDerivedBonuses;
            int speed=active?(stats.Speed>=80?1:0)+(stats.Speed>=90?1:0)-(stats.Speed<20?1:0)-(stats.Speed<10?1:0):0;
            int shield=equipment.ShieldArmor;
            if(active && shield>0)
                shield+=state.DefensiveAction?Math.Min(stats.Shield/15,3*shield/2):Math.Min(stats.Shield/20,shield);
            var c=new CharacterDefenseContributions(active && effects.NaturalArmorBonus?2:0,speed,active?stats.Stealth/10:0,
                shield,equipment.OtherArmor,active?effects.SpellArmor:0,active?effects.Penetration:0,
                active?-new[]{0,0,1,2,4}[state.EncumbranceCategory]:0,active?effects.Conditions:0,
                active?-OriginalFatigueRules.ScorePenalty(state.FatigueCategory)/10:0,active && state.DefensiveAction?2:0,
                active && stats.ReflexionEnabled?1+stats.Reflexion/20:0);
            // Penetration is deliberately excluded from Base, but applies to each body location.
            int basis=c.Natural+c.Speed+c.Stealth+c.Shield+c.OtherEquipment+c.SpellArmor+c.Encumbrance+
                c.Conditions+c.Fatigue+c.Defensive+c.Reflexion;
            if(state.Incapacitated)basis=Math.Min(basis,-5);
            int shared=basis+c.Penetration;
            var locations=new BodyArmor(shared+equipment.Body.Head,shared+equipment.Body.Torso,shared+equipment.Body.Legs,
                shared+equipment.Body.Hands,shared+equipment.Body.Feet);
            int weighted=15*locations.Head+40*locations.Torso+30*locations.Legs+10*locations.Hands+5*locations.Feet;
            int average=(weighted+(weighted>=0?50:-50))/100;
            return new(basis,locations,average,c,state.Incapacitated);
        }
    }

    public static TargetDefenseThresholds Against(CharacterDefenseResult target,CharacterHitLocation location,
        OriginalAttackMode mode,CharacterDefenseContext context)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(target.Contributions);ArgumentNullException.ThrowIfNull(target.Locations);ArgumentNullException.ThrowIfNull(context);
        _=OriginalCombatRules.ModeModifiers(mode);
        checked
        {
            int adjustment=context.RemoveSpeedAndShield?-Math.Max(0,target.Contributions.Speed)-Math.Max(0,target.Contributions.Shield):0;
            if(context.FourPointPenalty)adjustment-=4;
            if(context.MeleeVulnerability && !target.Incapacitated && mode is not (OriginalAttackMode.Throw or OriginalAttackMode.Lash or OriginalAttackMode.Shoot))
                adjustment-=4;
            // 0x542FC0 invokes 0x542EE0 (which adds context) and 0x5468D0 again.
            // Preserve both contributions; this is not a deduplicated display AC lookup.
            return new(target.Base+adjustment,target.Locations.At(location)+2*adjustment,adjustment);
        }
    }
}
