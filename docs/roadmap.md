# NeoWizardry roadmap

## Current baseline
Status checked against code and iteration reports through **045**. Original monastery geometry, embedded textures and IFL frame animation are imported privately and verified in Godot on the self-hosted runner. Windows launch and Android launch were confirmed by the user. Godot client and independent C# core support parties of 1–8, saves, imported catalogs, experimental combat, five synthetic spells, two synthetic crafting recipes and a portable Windows updater. Windows previews use the persistent self-hosted service.

| Workstream | Recovered / implemented | Still required for original gameplay |
|---|---|---|
| Combat | Recovered physical rules, live multi-opponent grid battles, persisted positions/initiative, authored tactical AI, mixed original monster rosters, multi-slot/mode attacks, fumbles and connected condition 16; 79/595 original records admit supported profiles | Other conditions, magic/special/projectile delivery, native world AI/formation/time, effective party stat producers and original rewards; 516 monster records remain blocked |
| Characters | Prototype roster and party-size support; typed effective combat-attribute inputs; weighted original class-history arithmetic | Real character creation, effective stat/skill derivation, XP/leveling/multiclass progression and saves |
| Magic | 150-record metadata/reference, original ordinary casting-risk/backfire, Power Cast, base strength and resistance helpers | Learning/book/class policy, source/context modifiers, one complete original damage/healing path, resource/condition/training flow and live integration |
| Inventory | One authoritative typed instance model in gameplay, equipment, fixture crafting and schema-2 saves; old-save migration, split/merge/transfer/resources | Original use/recharge/curse policy and remaining equipment coverage |
| Crafting | 36-record original recipe reference, recovered native combination arithmetic and clean transaction simulation | Actor skill/time/progression policy, original combinations in gameplay and result previews; proper crafting menu remains a separate rework |
| World | SLF reader, patch/loose overlay, verified original archive and both monastery static PVL mesh prefixes; private GLB export and desktop fly viewer; 55532 triangles, 2104 textured surfaces and 12 animation materials; original OCT static collision and authored capsule walking; native object/trigger-prefix intake, arrival navigation and inspection markers | Native lighting/material parity, native movement parity, dynamic content, doors/transitions, encounters and connected exploration/combat saves |

Recovered ordinary character formulas now execute in the default new training duel (032), with real owned weapons, explicit authored effective facts, contact/penetration/HP, RNG continuation and scenario rewards. Iteration 033 adds recovered ordinary character initiative / repeated-attack scheduling and safe original Frost / Energy Blast through that executor. Actors, room, immediate delivery, ties and target selection remain authored; real stat derivation and complete original combat/magic remain integration gates. MONSTERS.DBS import now covers 595 record/text/hash definitions with a browser; Iteration 034 maps verified defense fields into the original defense rules; HP/attacks/AI and executable monster actors remain open. It also connects ordinary fizzle/backfire and Heal Wounds, with native reactions/progression still excluded.

## Audit gate — 2026-10-15

[Archive/code audit](audit-2026-10-15.md) found reproducible typed/raw catalog validation gaps, repeated full-catalog decoding in inventory operations and mutable arrays behind the snapshot contract. [Iteration 029](iteration-029.md) rejects item/spell field mismatches and gives item catalogs immutable storage, validated ID indices and reused inventory rules; selected armor/projectile metadata is compiled once. [Iteration 030](iteration-030.md) makes canonical game/inventory snapshots and spell/recipe catalog collections deeply immutable. [Iteration 031](iteration-031.md) removes flight-owned inventory and commits resolved projectile cost/HP/action transitions against the current GameState, with source reservations and schema-3 pending persistence. Full original actor/command integration and the remaining unified content/evidence boundary are still open. Projectile integration must retain one authoritative inventory. These are implementation tasks, separate from intentionally preserved original arithmetic.

Immediate integration prerequisites take priority over adding isolated arithmetic helpers:

1. [TOC-40](https://linear.app/tochka/issue/TOC-40): verified immutable compiled definitions, cross-check known typed/raw fields and reuse indexed semantic rules.
2. [TOC-41](https://linear.app/tochka/issue/TOC-41) / [TOC-42](https://linear.app/tochka/issue/TOC-42): explicit original actors and command/state/save contracts; one complete original encounter with current-inventory resource completion. Then one original spell effect and one native combination.
3. [TOC-43](https://linear.app/tochka/issue/TOC-43): accessible monster/encounter/item/missile imports and NPC/facts content research. DATA.SLF availability is verified; these tracks do not depend on downloading LEVELS.SLF.

Measure evidence, import, execution, persistence and UI separately. Arithmetic case counts and fixture CI do not establish original gameplay coverage. The detailed execution backlog below remains open within these gates.


## Stages
1. **Combat:** recover and verify initiative, hit/penetration, weapon damage, attack/swing counts, range and formation. Integrate imported weapons.
2. **Characters:** creation, races/classes, attributes, skills, XP, leveling and multiclass progression. Preserve solo progression and unusual builds.
3. **Magic:** a dedicated workstream for spell learning, books/realms, power level, resource costs, success/fizzle/backfire, resistances, Power Cast, durations, states, targeting and alternative casting sources. First slice needs representative mechanics; complete spell coverage grows thereafter.
4. **Inventory and crafting:** implement item ownership, quantities, transfer, equipment and usage; reconstruct the original combination/recipe systems and their requirements. Recover original behavior as the baseline; plan a soft crafting rework with a proper menu rather than treating the original or prototype UX as the target.
5. **Original world:** inspect LEVELS.SLF and decode real geometry, collision, models, textures, animation, doors and transitions. Start with the monastery. This research can run alongside rules work once the local archive inventory is available.
6. **Playable slice:** connect a real monastery section, character progression, encounters, loot, representative magic, representative crafting, NPC dialogue, events and save/load.
7. **Complete monastery:** reproduce its required content and exit path; reach a meaningful demo.
8. **Dominus and campaign:** expand locations, quests, trade, recruitment, factions and main-story dependencies; reproduce and fix the actual Crock solo blocker.
9. **Polish and release:** QoL improvements backed by analysis, performance, stable saves, Linux/Windows builds and local import from an installed original.

Visual development starts with real-world import: Wizardry identity/frame, Grimrock-inspired materials and lighting, gradual replacement of original presentation.

## Magic acceptance
- Representative offensive, protective and support effects work in a reproducible battle sequence.
- A magic-oriented party is a required test configuration alongside the solo faerie ninja.
- Distinguish tested original behavior from research candidates and redesigns; do not silently resolve unknown rounding or Power Cast behavior.
- Track coverage by effect mechanism as well as spell count.

## Planned feature: passive attribute contribution to spell effects

User decision, 2026-10-15: [TOC-23](https://linear.app/tochka/issue/TOC-23/ficha-nebolshoj-passivnyj-bonus-harakteristiki-k-effektu-zaklinanij). Deferred remake design feature, separate from reconstructing original formulas.

Keep adjustable mana investment/power and add a small passive contribution from a caster attribute to the spell effect, including low-power casts. Character growth should keep economical casts useful throughout progression, while spending more mana still produces a substantial improvement. Both low-power efficiency and full-power impact should have situational value; neither should become the universally best choice.

The attribute, any school-specific mapping, scaling curve, coefficients and treatment of damage/healing/control remain design questions. Do not assume Intelligence, add character level directly, or apply one formula to every effect mechanism by default. Balance in early/mid/late game against effect per mana and effect per turn, and preview the resulting effect when selecting power. Current fixture formulas are unchanged.

## Crafting acceptance
- Identify the original systems, recipe sources and requirements before choosing the implementation.
- Check ordinary combinations, gated recipes and any required progression/quest combinations against original behavior.
- Successful operations consume exactly the specified quantities and create the specified result.
- Rejected operations do not lose ingredients or corrupt item state.
- Recipe restrictions and resulting state survive save/load; UI explains why a combination is unavailable.
- Small parties and solo must not acquire an accidental required-companion dependency.
- Start with verified representative recipes for the slice; full coverage is a separate gate.
- Do not assume every item-combination system shares the same algorithm or that recipe data necessarily lives in DBS.

## Planned feature: soft crafting rework

User decision, 2026-10-15: [TOC-22](https://linear.app/tochka/issue/TOC-22/ficha-myagkij-revork-krafta-i-polnocennoe-menyu). Deferred feature; current Test crafting and Combination reference are temporary.

- A proper crafting menu with recipe/result cards, search and filters.
- Crafter selection, skill requirements, available/missing ingredients and clear denial reasons.
- Ingredient-instance selection, batch quantity and a preview of consumption, result and leftovers before confirmation.
- Mouse/keyboard and touch support.
- Preserve the character of Alchemy, Engineering and combination chains while allowing targeted improvements; distinguish redesign decisions from recovered original rules.

Detailed UX and balance are to be designed when implementing the feature. Recovering the original rules does not commit the remake to reproducing the original interface or every awkward interaction.

## Planned feature: Gadgeteer progression and Omnigun rework

User decision, 2026-10-15: [TOC-24](https://linear.app/tochka/issue/TOC-24/ficha-revork-mehanika-krivoj-razvitiya-i-pozdnego-omnigana). Deferred remake design feature, related to the crafting rework (TOC-22).

Preserve the Gadgeteer's engineering/gadget identity while reworking its progression curve. The user's reported pain points are uneven/unrewarding development and an unsatisfactory high-level Omnigun, the class's main weapon. Specific mechanical causes are to be investigated rather than assumed.

Review early/mid/late-game contribution, gadget/component availability and useful upgrade pacing. Examine Omnigun stages for damage, accuracy, added effects and tactical value; late upgrades should feel desirable and should not force a loss of useful earlier capabilities. Assess the class in different party sizes, including solo, and coordinate gadget/crafting changes. Exact mechanics, upgrade choices and numerical balance remain open; no implementation changes are made with this backlog decision.

## Persistent gates
- Early Android proof: touch UI, movement/look, item interaction, background saves, APK export and a separate physical-device test. iOS follows when macOS/Xcode and a test device are available.
- Parties of 1–8 are tested throughout; final upper limit remains open.
- No automatic solo equalization or unusual-build nerfs.
- Main-story completion must be demonstrated with solo; a synthetic captivity fixture does not prove the original quest is fixed.
- Original assets and full exported databases stay outside public git.
- Canonical formulas require documented evidence or original-game fixtures.

## Current blocker
LEVELS.SLF exceeds the Drive connector limit. The supplied `wiz_data/Wizardry8` in the self-hosted runner work directory is now verified: [iteration 041](iteration-041.md) successfully audited 4956 active resources, including 426 monastery resources and both required PVLs. Resource access and the static PVL mesh prefix are verified. Base texture rendering and IFL animation are connected. Static OCT/GameData collision is decoded and connected to an authored Godot capsule. Native movement parity, lighting/material parity and dynamic PVL sections remain open. The immediate priority is a small connected monastery section with exploration → encounter → exploration and saved world state; complete monster/spell coverage is not a prerequisite to start.

## Original-data research progress
Iteration 011 decodes 150 native spell metadata records and 160 indexed description entries, preserving provenance and unknown raw bytes. A separate read-only reference is available in the client. This does not establish original effect execution, learning, success/resistance, timing or recipe rules; see [spell format evidence](spell-format.md).


## Inventory implementation decision

User decision, 2026-10-15: preserve inventory functionality on a clean instance model, without carrying original storage hacks into gameplay. The new typed core supports identity, quantity, resource counters, identification, equipment locations, atomic transfers/splits/merges and consumption/production. Raw original slots and recovery rules remain in the import/research boundary. Iteration 016 replaces the aggregate bag across gameplay, equipment, fixture crafting and saves with one authoritative instance inventory, adds schema-1 migration and instance selection/split/merge in Items. Next: connect representative original combinations with actor/skill policy and result previews, while keeping the broader crafting menu rework separate. See [architecture](architecture.md#inventory-boundary--decision-2026-10-15).


## Combat and magic baseline — user decision 2026-10-15

Use original formulas with a clean implementation. Current research priority: physical attack/hit/penetration and magic success/resistance/effect paths, recovered from the executable and checked against reported experiments. Iterations 017–028 supply pure attack-budget, swing-allocation, ordinary hit/penetration-score, primary-hand and physical-damage rules, a scoped connected melee calculation, explicit projectile resource lifecycle with reserve/refill continuation and neutral post-contact penetration/damage, resolved-coordinate aim perturbation, character base/location defense and fatigue, monster target defense with seven definition-dependent hit zones, ordinary monster attacking scores, scoped physical candidate selection and group/swing budgets, and casting/resistance helpers. Iteration 020 corrects the earlier BAR stack-slot error: BAR includes combat experience, not load. The ordinary score builders require resolved hand bonuses and defenses; they do not close target-selection, condition or actor-policy gaps. Next: resolve effective actor/hand/defense selection, trace hit/fumble and damage plus one complete damage/healing spell path, then representative live integration. Existing synthetic battle rules remain labelled until replaced. Preserve the separately requested deferred crafting, passive magic bonus and Gadgeteer/Omnigun reworks.


## Current execution order (iteration 044)

1. Inspect both textured monastery levels visually against the original. Base texture dependencies and IFL animation are connected; original lighting, special blend/water behavior and exact material parity remain open. Preserve source hashes and keep original-derived assets private.
2. Inspect native static collision walking in both levels, then decode required dynamic PVL sections and state owners for doors/transitions. Authored capsule movement does not establish original solver parity. Validate a small connected section before gameplay integration.
3. Connect exploration → encounter → exploration with persistent positions, resolved encounters and world interactions. Exercise solo and multi-character parties against representative supported original monsters.
4. Expand representative magic, rewards, NPC/events and progression needed by that section. Full monster/spell coverage continues independently; it is not a prerequisite for beginning world import.

## Historical execution order after roadmap review (iteration 028)

1. **Finish a complete ordinary physical attack path.** Complete hand/skill/offhand/effect selection, context counter lifecycle, defense and hit-location selection, fumble/retargeting, target-dependent damage context, projectile actor aim-profile/collision/actual-target policy and remaining interruption gates. Selected-source depletion and interruptions are covered in 021; 022 adds native compatibility, reserve selection and refill with remaining-series continuation. Iteration 023 connects ordinary post-delivery interception/penetration/damage/resource completion; 024 adds distance/spread aim perturbation and aimed pending launch; 025 builds character base/location defense from gear and effective facts, and corrects score-category naming from load to fatigue. Iteration 026 adds ordinary monster-as-target defense with native AC conversion, seven weighted zones, nine mode modifiers and resolved runtime context. Iteration 027 adds ordinary monster attacking hit/penetration arithmetic, kind-specific fatigue, repetition, hit cap and resolved score scaling. Iteration 028 adds scoped selection among resolved physical mode/slot/target entries and separate group/swing budget transitions. Full mixed-action AI eligibility, retarget/interruption policy, damage-state producers and native actor import remain open. End-of-attack refill ordering, actual delivery, special branches and thrown redistribution still need the complete controller. Ordinary primary-hand bonuses, damage math and neutral melee without redirection are now covered; these do not close the whole attack path. Monster score builders now exist; actor facts, monster damage-state producers and the full controller are still required before claiming symmetric live combat. Current score results are raw thresholds, not final hit percentages.
2. **Connect actor facts and one representative live encounter.** Model effective attributes, skills, class history and hand state explicitly; persist them without invented original defaults for old fixture saves. Reuse the recovered calculations for both execution and previews, with externally owned deterministic RNG. Include melee, shooting and throwing, resource exhaustion and solo/magic-party cases.
3. **Complete representative original magic and combination flows.** Trace costs through results and training, using the clean instance inventory and actor policies. Keep full content coverage and the deferred redesigns separate.
4. **Resume world import when LEVELS.SLF is locally available.** Rules work does not remove the archive gate. A real monastery slice remains the target for stage 6; no unrelated feature work is needed to proceed on the accessible rules.

Latest evidence: [iteration 032](iteration-032.md), [iteration 031](iteration-031.md), [iteration 030](iteration-030.md), [iteration 029](iteration-029.md), [iteration 028](iteration-028.md), [monster sequences](original-monster-sequences.md), [iteration 027](iteration-027.md), [monster attacks](original-monster-attacks.md), [iteration 026](iteration-026.md), [monster defense](original-monster-defense.md), [iteration 025](iteration-025.md), [character defense/fatigue](original-character-defense.md), [iteration 024](iteration-024.md), [projectile aim](original-projectile-aim.md), [iteration 023](iteration-023.md), [projectile strike](original-projectile-strike.md), [iteration 022](iteration-022.md), [ammo and refill](original-ammunition-reload.md), [iteration 021](iteration-021.md), [projectile resources](original-projectile-resources.md), [iteration 020](iteration-020.md), [physical damage](original-physical-damage.md), [physical formulas](original-combat-formulas.md), [magic formulas](original-magic-formulas.md). Current work: [TOC-40](https://linear.app/tochka/issue/TOC-40); actor and snapshot integration follow in TOC-41/42. Completed tracking: [TOC-28](https://linear.app/tochka/issue/TOC-28) (017) and [TOC-29](https://linear.app/tochka/issue/TOC-29) (018). Preserve TOC-22/23/24 as deferred crafting, passive magic and Gadgeteer/Omnigun reworks.

## Iteration 030 — deep immutable snapshots

[Iteration 030](iteration-030.md) closes AUD-04 for canonical GameState/ItemInventoryState and nested actor collections; spell/recipe references now also own immutable collections, including school flags, recipe ingredients/issues and inactive-pair counts. Save schema and JSON shapes remain compatible. AUD-05 remains open: ProjectileSeriesState still carries a separate inventory snapshot and must not be committed over the current GameState inventory. TOC-42 stays in progress until authoritative in-flight completion and the original action/save contracts are implemented.


## Iteration 031 — authoritative projectile transactions

[Iteration 031](iteration-031.md) addresses AUD-05: series state no longer carries inventory; the canonical executor commits resource cost, actual-target HP and pending ordinal against current GameState.Items. Source-instance reservations, multiple distinct pending shots, duplicate callback rejection, monotonic IDs, current-state refill and schema-1/2 → 3 migration are covered. Callers serialize commits to the current state. Godot fixture combat is unchanged; original actor eligibility/stats, geometry/collision, RNG planning and reward scheduling remain integration work.


## Iteration 032 — playable ordinary melee

[Iteration 032](iteration-032.md) connects the recovered character single-weapon attack/swing, defense/location, hit/penetration and damage rules to the default new expedition and Attack UI. Real owned weapons, clamped HP, exact-instance loot transfer, one-time scenario XP, core range validation and save schema 4 are covered for parties 1–8, including private original equipment. Training stats/body protection, effective-Speed scheduling, targets and RNG are explicitly authored; the opposing character is not an imported monster. Legacy saves retain their prior mode; synthetic spells stay in Spell sandbox. TOC-41 remains in progress for original actor/stat/hand/state producers, monster execution, original magic/combinations and fuller action policy.


## Iteration 035 — multi-opponent position laboratory

[Iteration 035](iteration-035.md) adds an explicit grid laboratory for 1–8 heroes and 1–8 authored opposing characters. Per-actor positions, one-step movement, obstacles, sight, selected targets, deterministic pathfinding, shared initiative, supported original single-target spells, all-opponents victory, exact-instance loot and schema-5 continuation are playable. Movement/AI/geometry are laboratory policies; imported monster execution, native formation/continuous motion, ranged/area effects and full magic coverage remain open. TOC-41/42/43 remain in progress.


## Iteration 036 — enemy behavior profiles

[Iteration 036](iteration-036.md) adds four authored tactical behavior profiles, target perception with expiring last-position memory, allied target distribution, wounded-target selection, low-HP retreat, same-round retargeting between attack groups, explanations/profile selection in the UI and schema-six brain persistence. Existing schema-five tactical saves keep legacy pursuit. Original monster actor import and native AI recovery remain open; training characters exercise the new controller without inventing monster stats. TOC-41/42/43 remain in progress.


## Iteration 037 — reviewed original monster actors

[Iteration 037](iteration-037.md) connects Dusk Bat and Shrieker Bat to an opt-in physical monster encounter: native HP dice, neutral effective attributes, initiative, group/swing budgets, intrinsic Thrust damage and seven-zone defense. Actors carry private validated record snapshots, no fake class/weapon facts, and use the existing authored tactical AI. Schema 7 preserves complete continuation; public tests use an authored creature. Other definitions, low-score fumbles, target magic resistance/level facts, status/special attacks, native AI, XP and loot remain open. TOC-41/42/43 remain in progress.


## Iteration 038 — complete audit and general physical profiles

[Iteration 038](iteration-038.md) replaces new-battle species allowlisting with complete-slot capability checks: 76/595 supplied records admit a neutral physical profile, 519 retain explicit blockers. Multi-slot/mode choice, seven melee modes, native cubic fumble arithmetic/redirected contact flags, actual-target damage/absorption, byte-sized group continuation, mixed rosters and a searchable READY/BLOCKED catalog are connected. Schema 8 preserves old profile-1 action/RNG semantics. Shrieker's missing spell action is now surfaced; its old physical-only saves remain compatible. Effects, categories 1–3, spells, projectiles, rewards and native world/AI policies remain open. This is progress on adding all monsters, not completion of all 595.

## Iteration 039 — original attack-effect request boundary

[Iteration 039](iteration-039.md) recovers sixteen raw payload chances, the +24F source-level strength producer, ten ordinary condition routes, common/secondary duration stages, binary condition resistance and a selected-effect executor with explicit draw traces. The browser exposes effect details for blocked records. All 595 original rows are inspected; 479 contain enabled payloads. Immunity producers, status persistence/ticks and action/stat consumers remain open; live admission stays 76/595 and saves remain schema 8. No status-bearing monster is declared complete from request arithmetic alone.

## Iteration 040 — first connected native condition

[Iteration 040](iteration-040.md) connects condition 16 to post-damage application, maximum-duration merge, action cancellation, incapacitation defense, per-actor saved timers and the tactical grid. Native target level/base resistances are compiled for monsters; party targets remain explicit training fixtures. Timers use an authored one-unit-per-round clock, not native world-time parity. Schema 9/profile 3 preserves earlier profile contracts. RFS-81, Soldier Ant and Wasp raise supported coverage to 79/595 records; 516 remain blocked. Other statuses, spells, special/projectile delivery, world/native AI and rewards remain open.


## Iteration 041 — runner-based monastery intake

[Iteration 041](iteration-041.md) adds a scoped original-resource audit on the Windows runner, explicit `wiz_data/Wizardry8` discovery and metadata-only artifacts. Both self-hosted workflows reject originals inside checkout; the supplied folder remains outside it. Synthetic tests cover inventory, hashes and extraction rejection. Actual original PVL decoding and playable world integration remain open.


## Iteration 042 — static monastery geometry

[Iteration 042](iteration-042.md) adds a strict static PVL reader, private GLB export with UV seams preserved, and an independent desktop fly viewer. Actions validates original-derived meshes in Godot against parser triangle counts. OCT collision, native materials/lighting, doors, triggers, encounters and exploration/battle persistence remain open; static viewing does not close the playable-slice milestone.


## Iteration 043 — textured monastery viewer

[Iteration 043](iteration-043.md) connects original TGA textures, pixel alpha, source UVs and IFL material animation through self-contained private GLBs. Both levels pass Godot checks for all embedded pixel hashes, 2104 textured surfaces, 12 animation-material cycles and unchanged geometry bounds/counts. Windows preview 53 contains the verified viewer. Original lighting, special material/water parity, OCT collision and playable-world integration remain open.


## Iteration 044 — original static collision and walking

[Iteration 044](iteration-044.md) reads the original OCT/GameData physical banks and exports private collision sidecars bound to their PVL source hash. Both original levels pass Godot checks for walkable floor rays, capsule settling and blocking-wall sweeps (24,702 / 13,241 selected surfaces). Windows preview 56 adds F to enter/leave authored capsule walking while preserving flight and texture animation. Dynamic doors, transitions, original solver parity, encounters and world persistence remain open; this does not close the playable-slice milestone.


## Iteration 045 — native object records and arrival navigation

[Iteration 045](iteration-045.md) reads the PVL object/trigger prefix with exact bank boundaries and bounded dynamic mesh/animation layouts. Original intake finds 62 / 52 props, 32 / 26 bitmaps and 7 / 4 serialized door payloads. N navigates the 2 / 2 explicit positioned arrival triggers; O shows inspection markers and stored door flags. Full records stay private and bind to the same PVL hash as the GLB. The remaining PVL tail, rendered native prop meshes, runtime door states/animation, locks/items/recipient dispatch, transitions and world persistence remain open. This is preparation for interactions, not playable doors or recovered native spawning.
