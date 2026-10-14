# Magic workstream

The first casting slice uses two **synthetic** fixtures. It proves state, targeting, turn integration and save continuity. It does not reconstruct original spell data or formulas.

| Fixture | Realm pool | Target | Power | Mana cost | Test effect |
|---|---|---|---|---|---|
| Test Spark | Fire | Current enemy | 1–3 | 2 × power | power d6 + power damage, capped by enemy HP |
| Test Mend | Divine | Living, uncaptured party member below maximum HP | 1–3 | 2 × power | power d6 + power healing, capped at maximum HP |

New test heroes have both learned spells and 12/12 mana in each of those pools, regardless of class. This is deliberate fixture access, not a rule that every original class starts with magic. Existing saves missing mana/spellbooks gain neither automatically; use New party to try the new fixture.

## Orders and costs

Magic selects one caster, learned spell, power and target. Committing resolves a whole round: this hero casts instead of attacking with a weapon, other heroes attack normally and the enemy takes its turn. Casting uses the hero's test speed without a weapon initiative bonus. The queue is stable; a dead caster cannot execute a queued spell.

Order validation checks caster availability, learned spell, supported power, realm mana and target. Rejected orders do not resolve a round or spend anything. On execution, a healed target that has since died/become unavailable causes cancellation without mana/roll consumption. Casting is currently limited to an active battle and the client's sentinel range.

Valid fixture casts always succeed, spending only the selected realm pool and drawing effect dice from the saved prototype RNG. No fizzle roll is fabricated. Pool values, learned spells and the shared RNG survive saves. Self-healing preserves the already-deducted mana. Healing cannot resurrect a dead hero.

## Coverage and remaining research

| Mechanism | Current status |
|---|---|
| Learned spell ownership | Saved fixture IDs; real learning, books, schools and class gates pending |
| Realm resources | Independent Fire/Divine fixture pools; original pool derivation and other realms pending |
| Power and cost | Explicit fixture levels/costs; original ranges and scaling pending |
| Offensive/support effect | Direct damage and single-target healing fixtures |
| Round ordering | Shared action queue; full original initiative rules pending |
| Success/fizzle/backfire | Original evidence required; fixture success is guaranteed |
| Resistance and Power Cast | Not implemented; require source-backed formula/rounding fixtures |
| Durations, protection, states | Not implemented |
| Area, group and non-battle targeting | Not implemented |
| Multiple caster orders per round | Not implemented; currently one chosen caster |
| Books, scrolls, charges, gadgets, other casting sources | Dedicated future coverage; no assumed shared algorithm |
| Complete original spell catalog | Not imported or claimed |

The magic roadmap remains separate from inventory/crafting. Further spells should be added by verified effect mechanism and casting source rather than simply increasing a spell counter.

## Iteration 009 — shared casting plans and effects

The public fixture has three additional learned spells:

| Fixture | Realm / cost | Target | Test effect |
|---|---|---|---|
| Test Ward | Divine / 2 × power | Living/free hero, including at full HP | Reduces sentinel attack damage by 2 × power, minimum damage zero; 3 rounds |
| Test Bind | Divine / 3 × power | Enemy | Skips its action in the application round if cast before it; 1 round |
| Test Burn | Fire / 2 × power | Enemy | Power damage at each round end; 3 rounds |

Durations include the application round. Round-end burning occurs before victory/loot resolution, then durations decrement once. Effects expire at zero; finished/unavailable battles do not tick. Reapplying the same kind keeps the higher magnitude and longer remaining duration without adding stacks. These are explicit test rules; original status resistance, immunity, stacking and clock semantics require evidence. No success or resistance dice are invented for these fixtures.

The Magic window supports **Queue / replace this caster's order**, **Clear orders** and **Resolve queued round**. One order per hero; heroes without orders attack normally. The whole plan validates before any state changes. Submission order does not change initiative. A caster killed before acting spends nothing; an unavailable or already fully healed target cancels that healing action without cost. Effects, mana, spell ownership and RNG retain their saved continuation. A direct Cast button still supports one selected caster.

| Mechanism | Updated status |
|---|---|
| Duration, protection, skipped action, periodic damage | Implemented with synthetic fixtures and saved state |
| Multiple caster orders per round | Implemented in core and UI |
| Original timing, resistance/Power Cast and full catalogs | Still require original data and observed fixtures |
