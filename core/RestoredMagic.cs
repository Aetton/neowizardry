using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using NeoWizardry.Core.Rules;

namespace NeoWizardry.Core;

// Explicit resolved training facts, not a character generation/class eligibility API.
public sealed record RestoredMagicFacts(string SpellDatabaseHash,int BookSkill,ImmutableArray<int> RealmSkills,
    int EffectiveCasterLevel,int StrengthLevel,bool PowerCastEnabled,int PowerCastSkill,ImmutableArray<int> LearnedSpells,
    int TargetLevel,ImmutableArray<int> Resistances);
public sealed record RestoredCastOrder(string CasterId,int SpellId,int Power,string? TargetId=null);
public sealed record PreparedRestoredCast(OriginalSpell Spell,RestoredMagicFacts Facts,int Power,int ManaCost,CastingRisk Risk,string TargetId);

public static class RestoredMagic
{
    private static readonly ConditionalWeakTable<SpellCatalog,object> ValidatedCatalogs=new();
    private static readonly ImmutableDictionary<int,string> Supported=new Dictionary<int,string>
    {
        [4]="323def4ad490e03ce010d1bac6a5da0d34b3291a48c6e3f25dd0d7d66bc54a86",
        [5]="a47fb8735854c994ab5490ec4fb98aabc853f47bb6178ff9c453600dcc080f96",
        [6]="b7a067dbb113e75ed4e1ff57928cb463f65c8edba3ffb5bd1acb4df5a946df47"
    }.ToImmutableDictionary();
    public static bool SupportedSpell(OriginalSpell spell)=>Supported.TryGetValue(spell.Id,out var hash) &&
        spell.RecordSha256.Equals(hash,StringComparison.OrdinalIgnoreCase);
    public static void Validate(RestoredMagicFacts f)
    {
        if(f is null || f.SpellDatabaseHash is not {Length:64} || !f.SpellDatabaseHash.All(Uri.IsHexDigit) ||
            f.BookSkill is <0 or >125 || f.RealmSkills.IsDefault || f.RealmSkills.Length!=6 || f.RealmSkills.Any(x=>x is <0 or >125) ||
            f.EffectiveCasterLevel is <1 or >255 || f.StrengthLevel is <0 or >255 || f.PowerCastSkill is <0 or >125 ||
            f.LearnedSpells.IsDefault || f.LearnedSpells.Distinct().Count()!=f.LearnedSpells.Length || f.LearnedSpells.Any(x=>!Supported.ContainsKey(x)) ||
            f.TargetLevel is <1 or >255 || f.Resistances.IsDefault || f.Resistances.Length!=6 || f.Resistances.Any(x=>x is <-255 or >255))
            throw new InvalidDataException("Invalid resolved magic facts");
    }
    public static PreparedRestoredCast Prepare(GameState state,RestoredCastOrder order,SpellCatalog spells)
    {
        ArgumentNullException.ThrowIfNull(order);
        _=ValidatedCatalogs.GetValue(spells,c=>{c.Validate();return new object();});
        var hero=state.Party.SingleOrDefault(h=>h.Id==order.CasterId)??throw new InvalidOperationException("Unknown caster");
        var facts=state.Melee?.Magic?.GetValueOrDefault(hero.Id)??throw new InvalidOperationException("This actor has no restored spellcasting profile");
        Validate(facts);
        if(hero.Hp==0 || hero.Captive || !TacticalCombat.Enemies(state).Any(e=>e.Hp>0) || hero.Effects is {Count:>0})throw new InvalidOperationException("Caster or target unavailable");
        if(!facts.SpellDatabaseHash.Equals(spells.Provenance.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Spell catalog identity mismatch");
        var spell=spells.Spells.SingleOrDefault(s=>s.Id==order.SpellId)??throw new InvalidOperationException("Unknown original spell");
        if(!facts.LearnedSpells.Contains(order.SpellId) || !SupportedSpell(spell))throw new InvalidOperationException("This original spell effect is not connected");
        var risk=OriginalMagicRules.OrdinaryCastingRisk(new(facts.BookSkill,facts.RealmSkills[spell.RealmCode],spell.Level,spell.Cost,order.Power,facts.EffectiveCasterLevel));
        string targetId=order.TargetId??(spell.Id==6?hero.Id:TacticalCombat.Enemies(state).First(e=>e.Hp>0).Id);
        if(spell.Id==6)
        {
            if(!state.Party.Any(h=>h.Id==targetId && h.Hp>0 && !h.Captive))throw new InvalidOperationException("Select a living party member for Heal Wounds");
        }
        else if(!TacticalCombat.Enemies(state).Any(e=>e.Id==targetId && e.Hp>0))throw new InvalidOperationException("Select the opponent for this offensive spell");
        int cost=checked(spell.Cost*order.Power);
        if((hero.Mana??[]).SingleOrDefault(m=>m.Realm==spell.Realm)?.Current<cost || !(hero.Mana??[]).Any(m=>m.Realm==spell.Realm))
            throw new InvalidOperationException("Not enough mana");
        return new(spell,facts,order.Power,cost,risk,targetId);
    }
    // Source/context fixed to ordinary player casting. The native target classifier
    // enables backfire for these two offensive rows, but not Heal Wounds (4ACA60).
    public static CastingOutcome Outcome(PreparedRestoredCast cast,ref uint rng)=>OriginalMagicRules.ResolveCasting(
        cast.Risk.FailureThreshold,cast.Risk.FailureThreshold==0?null:Dice.Exclusive(ref rng,100),cast.Spell.Id is 4 or 5);
    public static int Amount(PreparedRestoredCast cast,ref uint rng)
    {
        var s=cast.Spell;int dice=checked(s.DamageDice*cast.Power);
        if(dice>255)throw new InvalidOperationException("Native spell dice overflow");
        int rolled=s.DamageConstant;for(int i=0;i<dice;i++)rolled=checked(rolled+Dice.Roll(ref rng,s.DamageSides));
        return OriginalMagicRules.ScaleAmount(rolled,OriginalMagicRules.PowerCastBonus(cast.Facts.PowerCastEnabled,cast.Facts.PowerCastSkill));
    }
    public static (int Damage,int Reduction) Damage(PreparedRestoredCast cast,RestoredMagicFacts target,ref uint rng)
    {
        Validate(target);var s=cast.Spell;
        int rolled=Amount(cast,ref rng);
        int bonus=OriginalMagicRules.PowerCastBonus(cast.Facts.PowerCastEnabled,cast.Facts.PowerCastSkill);
        int strength=OriginalMagicRules.ScaleAmount(OriginalMagicRules.BaseEffectStrength(cast.Facts.StrengthLevel,s.Level,s.Cost,cast.Power,false),bonus/2);
        var bounds=OriginalMagicRules.ResistanceBounds(target.TargetLevel,strength,target.Resistances[s.RealmCode]);
        int? first=bounds.Pressure>0?Dice.Exclusive(ref rng,bounds.FirstExclusive):null;
        int? second=bounds.Pressure>0?Dice.Exclusive(ref rng,bounds.SecondExclusive):null;
        var result=OriginalMagicRules.ResistDamage(rolled,bounds,first,second);return(result.Damage,result.ReductionPercent);
    }
}
