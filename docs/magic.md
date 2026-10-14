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
