namespace NeoWizardry.Core;

public sealed record ManaPool(string Realm, int Current, int Max);
public sealed record SpellDefinition(string Id, string Name, string Realm, bool Heals, int CostPerPower);
public sealed record SpellOrder(string HeroId, string SpellId, int Power, string TargetId);

// Synthetic casting fixtures. These are not original spell data or success/resistance formulas.
public static class Magic
{
    public static readonly IReadOnlyList<SpellDefinition> Spells = Array.AsReadOnly(new[] {
        new SpellDefinition("test-spark","Test Spark","Fire",false,2),
        new SpellDefinition("test-mend","Test Mend","Divine",true,2) });
    public static SpellDefinition Find(string id) => Spells.FirstOrDefault(s=>s.Id==id)
        ?? throw new InvalidOperationException("Unknown spell");
    public static void ValidateHero(Hero hero)
    {
        var realms=new HashSet<string>();
        foreach(var pool in hero.Mana ?? [])
            if(pool is null || pool.Realm is not ("Fire" or "Divine") || !realms.Add(pool.Realm) ||
                pool.Max<0 || pool.Current<0 || pool.Current>pool.Max)
                throw new InvalidDataException("Invalid mana pool");
        var spells=new HashSet<string>();
        foreach(var spell in hero.Spellbook ?? [])
            if(spell is null || !spells.Add(spell) || !Spells.Any(s=>s.Id==spell))
                throw new InvalidDataException("Invalid spellbook");
    }
    public static string? Denial(GameState state, SpellOrder order)
    {
        var caster=state.Party.FirstOrDefault(h=>h.Id==order.HeroId);
        if(caster is null)return "Unknown caster.";
        if(caster.Hp==0 || caster.Captive)return "Caster cannot act.";
        var spell=Spells.FirstOrDefault(s=>s.Id==order.SpellId);
        if(spell is null)return "Unknown spell.";
        if(!(caster.Spellbook ?? []).Contains(spell.Id))return "Spell is not learned.";
        if(order.Power is <1 or >3)return "Test power must be 1–3.";
        if((caster.Mana ?? []).FirstOrDefault(p=>p.Realm==spell.Realm)?.Current is not int mana || mana<spell.CostPerPower*order.Power)
            return $"Not enough {spell.Realm} mana.";
        if(state.Enemy.Hp==0)return "Battle is already finished.";
        if(spell.Heals)
        {
            var target=state.Party.FirstOrDefault(h=>h.Id==order.TargetId);
            if(target is null || target.Hp==0 || target.Captive)return "Healing requires a living, free party member.";
            if(target.Hp==target.MaxHp)return "Target is already at full health.";
        }
        else if(order.TargetId!=state.Enemy.Id)return "Offensive spell requires the enemy target.";
        return null;
    }
    public static Hero Spend(Hero hero, SpellDefinition spell, int power) => hero with {
        Mana=(hero.Mana ?? []).Select(p=>p.Realm==spell.Realm ? p with { Current=p.Current-spell.CostPerPower*power } : p).ToArray() };
}
