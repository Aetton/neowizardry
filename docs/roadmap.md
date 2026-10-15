# NeoWizardry roadmap

## Current baseline
Iterations 001–011 delivered the SLF reader, ordinary patch/loose-file overlay, a local catalog of 819 items, a Godot client, independent C# core, variable parties, experimental combat and saves. Android launch was confirmed by the user. Assigned melee weapons use imported base damage dice and initiative with saved deterministic rolls. Windows launch was also confirmed by the user; preview builds update through the portable launcher. Shared item quantities, equip/unequip/transfer and one-time prototype loot now work; five synthetic spells demonstrate realm mana, power, multi-caster plans and saved duration effects. Two atomic synthetic crafting recipes demonstrate skill/knowledge gates and ingredient ownership. Windows previews now run on the persistent self-hosted Windows service. Canonical combat, original magic, crafting recipes and level geometry are not implemented.

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

## Persistent gates
- Early Android proof: touch UI, movement/look, item interaction, background saves, APK export and a separate physical-device test. iOS follows when macOS/Xcode and a test device are available.
- Parties of 1–8 are tested throughout; final upper limit remains open.
- No automatic solo equalization or unusual-build nerfs.
- Main-story completion must be demonstrated with solo; a synthetic captivity fixture does not prove the original quest is fixed.
- Original assets and full exported databases stay outside public git.
- Canonical formulas require documented evidence or original-game fixtures.

## Current blocker
LEVELS.SLF is 497064244 bytes and exceeds the Drive connector's 268435456-byte limit. A local archive inventory is still needed. This does not block accessible rules, magic, inventory or crafting research.

## Original-data research progress
Iteration 011 decodes 150 native spell metadata records and 160 indexed description entries, preserving provenance and unknown raw bytes. A separate read-only reference is available in the client. This does not establish original effect execution, learning, success/resistance, timing or recipe rules; see [spell format evidence](spell-format.md).
