# Preserve / fix / research matrix

| System | Direction | Evidence and current implementation |
|---|---|---|
| Solo and variable parties | Preserve | Explicit project requirement. Core/client tested for 1–8. Final upper limit open. |
| Solo XP concentration | Preserve | Design requirement; fixed test reward conserved across all party sizes. Exact original XP rules still unknown. |
| Race/class identity and restrictions | Preserve | Original item restriction masks parsed. Race/class enums, progression and equipment checks pending. |
| Phase/round-based combat | Preserve structure | Prototype uses deterministic speed order. Original initiative, hit/damage formulas, formations and distance rules pending. |
| Strong unusual builds | Preserve | No automatic enemy scaling or special nerf to the faerie ninja. Starting stats and combat are temporary fixtures. |
| Crock captivity softlock | Fix after reproduction | User-reported original issue. Solo escape fixture passes; actual original event not reproduced. |
| Slow battle presentation | Improve | Client resolves a round immediately. Full animation speed/QoL options pending. |
| Skill grinding | Research before redesign | No skill gain system yet; no assumed replacement formula. |
| Inventory/equipment/spells | Preserve depth, improve UI | Item catalog only; no simulated inventory or spell rules yet. |
| Dialogue | Preserve keywords and deterministic knowledge | One fixed synthetic keeper line. Canonical dialogue import and semantic interface pending. |
| Art and UI | Modernize | Grimrock-inspired material/light direction; Wizardry framing target. Current scene and frame are placeholders. |

Reproducible **prototype** combat: a solo hero starts at 36 HP, speed 11, power 7; sentinel at 32 HP, speed 4, power 3. First round ends at hero HP 33 and sentinel HP 25. Fifth round kills the sentinel before its action; solo hero has 24 HP and receives 120 XP. This is a controlled test case, not a claim about the original game.
