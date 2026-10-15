# Iteration 026 — monster defense and seven hit zones

Recovered monster target branches in `0x542EE0`, `0x542FC0` and `0x5468D0`. Added `OriginalMonsterDefense` with explicit immutable calculation inputs: signed native AC, separate seven-zone and nine-mode armor modifiers, runtime penetration/context adjustments, stamina-derived fatigue, incapacitation cap and resolved combat predicates. Preserves the positive monster fatigue term and two context contributions to penetration. AC cancellation is conditional on the incapacitation cap, rather than incorrectly simplified away.

Recovered seven unsigned location weights at definition +0x15F, independent of character body armor and fixed weights. Exhaustive d100 selection preserves cumulative native ordering, zero weights, over/underweight records and diagnostic zone-3 fallback. Unknown anatomy labels remain Zone0..Zone6; raw pointers, database layout and action/status identifiers remain outside the core.

224 checks added: all 100 zone draws, all 63 mode/zone combinations, signed boundaries, incapacitation, fatigue/context ordering, combat gates and Kick/Throw/Lash/Shoot exceptions, serialization and connected character-attacks-monster melee/projectile calculations. 1345 portable C# checks including gestures; 1352 with 819 private items. 27 Python tests pass. Client builds with zero warnings/errors; private-data Godot gameplay and synthetic mobile smokes pass.

This implements ordinary monster defense from supplied facts. It does not import a native monster catalog, derive actor state, implement monster attacking scores, change live saves or switch the labelled fixture battle to original rules. Next: effective actor/hand selection and monster-side attack builders, then a representative live encounter. Original magic damage/healing and combination flows remain required; deferred TOC-22/23/24 remain separate.

Detailed [evidence and formulas](original-monster-defense.md). Tracking: [TOC-37](https://linear.app/tochka/issue/TOC-37), parent TOC-10. Static reconstruction is not an original-game runtime comparison.
