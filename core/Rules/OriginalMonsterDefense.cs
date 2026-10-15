namespace NeoWizardry.Core.Rules;

// Native monster zones are definition-dependent; do not assign character body-part names.
public enum MonsterHitLocation { Zone0,Zone1,Zone2,Zone3,Zone4,Zone5,Zone6 }
public sealed record MonsterLocationValues(int Zone0,int Zone1,int Zone2,int Zone3,int Zone4,int Zone5,int Zone6)
{
    public int At(MonsterHitLocation location)=>location switch {
        MonsterHitLocation.Zone0=>Zone0,MonsterHitLocation.Zone1=>Zone1,MonsterHitLocation.Zone2=>Zone2,
        MonsterHitLocation.Zone3=>Zone3,MonsterHitLocation.Zone4=>Zone4,MonsterHitLocation.Zone5=>Zone5,
        MonsterHitLocation.Zone6=>Zone6,_=>throw new ArgumentOutOfRangeException(nameof(location))};
}
public sealed record MonsterModeArmor(int Swing,int Thrust,int Bash,int Berserk,int Throw,int Punch,int Kick,int Lash,int Shoot)
{
    public int At(OriginalAttackMode mode)=>mode switch {
        OriginalAttackMode.Swing=>Swing,OriginalAttackMode.Thrust=>Thrust,OriginalAttackMode.Bash=>Bash,
        OriginalAttackMode.Berserk=>Berserk,OriginalAttackMode.Throw=>Throw,OriginalAttackMode.Punch=>Punch,
        OriginalAttackMode.Kick=>Kick,OriginalAttackMode.Lash=>Lash,OriginalAttackMode.Shoot=>Shoot,
        _=>throw new ArgumentOutOfRangeException(nameof(mode))};
}
public sealed record MonsterDefenseDefinition(int ArmorClass,MonsterLocationValues LocationWeights,
    MonsterLocationValues LocationArmor,MonsterModeArmor ModeArmor);
// Signed runtime bytes +0x1E0/+0x1DF/+0x226; their effect/status producers are external.
public sealed record MonsterDefenseState(int PenetrationAdjustment,int ContextAdjustment1,int ContextAdjustment2,
    int FatigueCategory,bool Incapacitated);
// Resolved combat-context predicates. No speculative action, condition or trait names.
public sealed record MonsterDefenseContext(bool CombatActive,bool TwoPointPenalty,bool TwoPointBonus,bool MeleeVulnerability);
public sealed record MonsterLocationSelection(MonsterHitLocation Location,bool UsedFallback);

public static class OriginalMonsterDefense
{
    public static MonsterLocationSelection SelectLocation(MonsterLocationValues weights,int draw)
    {
        ValidateLocations(weights,0,255);
        if(draw is <0 or >99)throw new ArgumentOutOfRangeException(nameof(draw));
        int cumulative=0;
        for(int i=0;i<7;i++)
        {
            cumulative+=weights.At((MonsterHitLocation)i);
            if(draw<cumulative)return new((MonsterHitLocation)i,false);
        }
        // Native diagnostic branch falls back to index 3, without normalizing the weights.
        return new(MonsterHitLocation.Zone3,true);
    }

    public static TargetDefenseThresholds Against(MonsterDefenseDefinition definition,MonsterDefenseState state,
        MonsterHitLocation location,OriginalAttackMode mode,MonsterDefenseContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(definition.ModeArmor);
        SignedByte(definition.ArmorClass);SignedByte(state.PenetrationAdjustment);
        SignedByte(state.ContextAdjustment1);SignedByte(state.ContextAdjustment2);
        ValidateLocations(definition.LocationWeights,0,255);ValidateLocations(definition.LocationArmor,-128,127);
        foreach(var m in Enum.GetValues<OriginalAttackMode>())SignedByte(definition.ModeArmor.At(m));
        int locationArmor=definition.LocationArmor.At(location),modeArmor=definition.ModeArmor.At(mode);
        int fatigue=OriginalFatigueRules.ScorePenalty(state.FatigueCategory);
        int basis=10-definition.ArmorClass;
        if(state.Incapacitated)basis=Math.Min(basis,-5);
        int adjustment=state.ContextAdjustment1+state.ContextAdjustment2+fatigue/10;
        if(context.CombatActive)
        {
            if(context.TwoPointPenalty)adjustment-=2;
            if(context.TwoPointBonus)adjustment+=2;
            if(context.MeleeVulnerability && !state.Incapacitated &&
                mode is not (OriginalAttackMode.Throw or OriginalAttackMode.Lash or OriginalAttackMode.Shoot))adjustment-=4;
        }
        // Both helpers add K. Preserve the definition AC term even after the incapacitation cap.
        int hit=basis+adjustment;
        int penetration=hit+state.PenetrationAdjustment-modeArmor-locationArmor+definition.ArmorClass+adjustment;
        return new(hit,penetration,adjustment);
    }

    private static void SignedByte(int value)
    {
        if(value is <-128 or >127)throw new ArgumentOutOfRangeException(nameof(value));
    }
    private static void ValidateLocations(MonsterLocationValues values,int minimum,int maximum)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach(var location in Enum.GetValues<MonsterHitLocation>())
            if(values.At(location)<minimum || values.At(location)>maximum)throw new ArgumentOutOfRangeException(nameof(values));
    }
}
