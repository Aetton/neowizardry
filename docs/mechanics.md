# Preserve / fix / research matrix

| System | Direction | Evidence and current implementation |
|---|---|---|
| Solo and variable parties | Preserve | Explicit project requirement. Core/client tested for 1–8. Final upper limit open. |
| Solo XP concentration | Preserve | Design requirement; fixed test reward conserved across all party sizes. Exact original XP rules still unknown. |
| Race/class identity and restrictions | Preserve | Original item restriction masks parsed. Race/class enums and equipment checks implemented; progression pending. |
| Phase/round-based combat | Preserve structure | New training parties use recovered character initiative and interleaved single-hand attack groups (033). Full formation, native timing contexts and distance rules remain pending. A statically recovered independent single-weapon attack/swing budget is documented in [original formulas](original-combat-formulas.md); it is not yet wired to battle. |
| Strong unusual builds | Preserve | No automatic enemy scaling or special nerf to the faerie ninja. Starting stats and combat are temporary fixtures. |
| Crock captivity softlock | Fix after reproduction | User-reported original issue. Solo escape fixture passes; actual original event not reproduced. |
| Slow battle presentation | Improve | Client resolves a round immediately. Full animation speed/QoL options pending. |
| Skill grinding | Research before redesign | No skill gain system yet; no assumed replacement formula. |
| Inventory/equipment/spells | Preserve depth, improve UI | Shared bag ownership, restricted equipment, imported melee base dice, multi-caster fixture spells/effects and atomic fixture crafting; canonical spells and recipes pending. |
| Dialogue | Preserve keywords and deterministic knowledge | One fixed synthetic keeper line. Canonical dialogue import and semantic interface pending. |
| Art and UI | Modernize | Grimrock-inspired material/light direction; Wizardry framing target. Current scene and frame are placeholders. |

Reproducible **prototype** combat: a solo hero starts at 36 HP, speed 11, power 7; sentinel at 32 HP, speed 4, power 3. First round ends at hero HP 33 and sentinel HP 25. Fifth round kills the sentinel before its action; solo hero has 24 HP and receives 120 XP. This is a controlled test case, not a claim about the original game.


## Original-formula baseline decision — 2026-10-15

User decision: combat and magic keep the original formulas as their baseline; implementation uses clean typed state and isolated calculations. Preserve integer rounding, bounds, thresholds and random-draw semantics when they affect gameplay. Source offsets, shared global arrays and storage sentinels stay in research/adapters. Explicit deferred redesigns (including TOC-23's passive spell-effect bonus) remain separate from baseline reconstruction. Every recovered helper states its scope; do not claim a complete system from a local arithmetic match.
