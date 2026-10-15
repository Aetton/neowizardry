# Iteration 027 — monster attacking scores

Recovered ordinary monster hit builder `0x542720` and the penetration arithmetic in `0x540AD4`–`0x540C80`. Added `OriginalMonsterScores` with explicit selected attack rating, signed runtime accuracy/penetration adjustments, effective attributes, mode, fatigue policy, resolved target defenses and external context counter. Shared exact repetition arithmetic retains character Speed <=125 and permits monster Speed <=255 without duplicating the nonlinear formula.

Recovered the native eighteen-kind fatigue classifier: table members halve hit fatigue and take full penetration fatigue; other attacks take full hit fatigue and no penetration fatigue. This is distinct from character Modern Weapon behavior. `OriginalMonsterAttackAdapter` owns native byte classification and raw actor-tag/global-setting scale mapping.

Recovered independent +10 hit flags, a hit-only cap at 10 and final 60%/100%/140% scaling. The cap precedes scaling and does not affect penetration. Breakdown exposes raw pre-cap/pre-scale scores and final scores, with signed truncation and checked native intermediate widths. No speculative faction/difficulty/status names or invented stats were introduced.

533 added checks cover all kind bytes, modes, fatigue categories, repetition reserves and signed boundaries, cap/scale order and character-defense composition. Portable C# total 1878 including gestures; private 819-item total 1885; private spell metadata total 1879. Python 27 tests. Client build has zero warnings/errors after restoring local Godot packages; private-data desktop gameplay and synthetic mobile smokes pass. See [detailed evidence and scope](original-monster-attacks.md).

The rules now calculate ordinary attacks **against** monsters and **by** monsters from supplied facts, but do not close original combat. Monster attack selection/budgets and damage-state producers, effective character/hand facts, counter lifecycle, fumble/redirect, world delivery and representative live integration remain open. Live battle and saves remain fixture-based. Representative original magic damage/healing and combinations remain required; TOC-22/23/24 stay separate.

Tracking [TOC-38](https://linear.app/tochka/issue/TOC-38), parent TOC-10. Static reconstruction is not original-game runtime validation.
