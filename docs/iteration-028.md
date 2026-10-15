# Iteration 028 — monster physical choice and two-level attack budgets

Recovered physical candidate expansion and scoped list-index selection: candidates are attack-slot/mode/target tuples, modes are expanded from the low-nine native mask bits, and empty lists consume no choice draw. Kept target identity, ordering and multiplicity; no guessed equal-target weighting or character cyclic mode sampler. Full mixed-action AI eligibility remains external.

Recovered definition +0xE5 group count, +0xE6 maximum swings, initialization and execution ordering. Added immutable `MonsterAttackBudget`: a group is consumed before its eligibility gate; an admitted group allocates draw+1 swings; every completed attempt consumes one swing; a resolved interruption may discard unfinished swings without inventing completions or consuming future group budgets. No original pointer/list/global storage model enters gameplay.

60 added checks include all 65,536 mask values, candidate/RNG boundaries, rejected groups, completion/cancellation, roundtrip and the full 65,025-swing input domain limit. Portable C# total 1938 including gestures; Python 27 tests. Client builds with zero warnings/errors; synthetic desktop gameplay and mobile Godot smokes pass. Private catalogs from the previous ephemeral environment were not recovered or rerun this iteration; the original EXE was recovered and its existing SHA-256 reverified. Client/CI checks use the synthetic fixtures.

The native hit-repetition counter +0x141 remains separate from group +0x09 and swing +0x0D counters. Its increment policy is not invented from derived completion counts. Full mixed AI selection, actor/hand facts, interruption/retarget policy, damage-state producers, world delivery and live encounter remain open. Original magic damage/healing and combinations remain required; TOC-22/23/24 remain separate. Live battle/saves are unchanged fixtures.

Detailed [evidence and scope](original-monster-sequences.md). Tracking [TOC-39](https://linear.app/tochka/issue/TOC-39), parent TOC-10. Static reconstruction is not original-game runtime validation.
