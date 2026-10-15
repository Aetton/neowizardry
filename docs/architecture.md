# Architecture

`core/` contains immutable record-based transitions with no Godot dependency. It owns party state, round ordering, XP distribution, captivity and versioned serialization. `client/` references the core and owns 3D scene, collision, camera, UI and user data storage. `tools/` reads originals locally and produces inventories/catalogs/extracted assets under ignored paths.

The UI currently exposes 1–8 characters, with no six-character assumption in the core. The maximum is a prototype constraint, not the final design promise. Enemy parameters are independent of party size. Renderer timing does not affect combat order. `Magic.cs` owns synthetic spell definitions, target/order validation and realm pool spending. Each selected spell replaces its actor's physical attack inside the shared queue; multiple actors may receive orders in one round; skipped/dead actions consume neither mana nor RNG.

Canonical mechanics and experimental decisions must remain distinguishable. The current combat is a deterministic test rule: stable speed plus weapon initiative order, imported melee dice or unarmed fixture damage, first eligible target, XP pool divided over eligible survivors. It does not yet implement initiative rolls, formations, real action selection, distances inside the simulation, canonical conditions, classes, leveling or item-instance state, charges or encumbrance. Range is checked only in the prototype client. A future rule implementation must move authoritative spatial constraints into the core.

Current dialogue is one fixed synthetic keeper response, with no LLM and no canonical NPC data. Captivity is an explicit state transition demonstrating solo recoverability. It is not a quest parser or a reproduction of Crock. The item browser now consumes imported schema-3 catalogs (with schema-2 read compatibility); equipment consumes one owned copy from the shared party bag and returns the previous weapon on a swap. Original restriction flags remain enforced. Bag quantities and slots survive saves; configured test loot is granted once with victory.

Import pipeline preserves source hashes and raw codes instead of guessing meanings. `Resources` implements ordinary loose-file and numbered-patch precedence with source provenance; Parallel Worlds and localization remain out of scope. Imported catalogs feed the equipment inspector and base melee rolls.

Windows is the primary manual test platform. Its CI exports the shared client, tests the exported executable and publishes immutable previews. The PowerShell launcher validates package hashes and keeps build files separate from Godot user-data saves. Android exports are manually triggered for mobile-specific checks and milestones.

Source assets, full exported tables and local source paths must remain outside public git. Research reports may contain hashes, aggregate counts and format offsets; the actual content stays in the user's private asset storage.

`Crafting.cs` provides atomic fixture recipes over the shared bag, with saved skill and knowledge gates. Fixture recipe IDs apply only to the synthetic catalog. Magic effects persist on hero/enemy records; the core ticks them at a documented round boundary and resolves periodic victory through the existing reward path. These mechanisms do not establish original crafting or status rules.

`tools/spells.py` exports independently sourced table/description metadata and preserves unknown raw spell bytes. `SpellCatalog.cs` validates the read-only reference separately from `Magic.cs`: original numeric IDs do not enter the synthetic learned spellbook or execute combat rules. Public builds contain only an invented reference fixture, while full exports remain local.


## Inventory boundary — decision 2026-10-15

Reproduce gameplay capabilities on a clean model; do not reproduce the original storage layout or accidental repair/move behavior as runtime rules. `ItemInventory.cs` defines the target instance inventory: stable IDs, definition IDs, quantities, identification, explicit resource counters, curse state and one location per instance (container or equipment slot). Rules specify stack limits and resource kinds. Transfers preserve item properties; splitting/merging conserves quantity; equipment locations cannot overlap; recipe consumption/production validates the entire resulting state before returning it. Exhausting charges leaves the item present. Quantities/resources use integers, capacity is configurable, and runtime serialization contains no original slot bytes or provenance hashes.

`OriginalItemAdapter.cs` converts supported original data into this model and returns raw evidence separately. It refuses unresolved counter inconsistencies, overflow and unsupported curse flags. Original-format repairs are an import decision, never an automatic gameplay side effect. The original +10 field's full behavior still requires research; the adapter's accepted 0/1 interpretation follows the documented editor interpretation, not proof of every curse mechanic.

`Research/NativeStacks.cs` and `Research/NativeInventory.cs` are original-format research utilities, not a second gameplay inventory. Their slot-indexed snapshots exist to verify recovered behavior and local combinations. Neither the client nor canonical inventory depends on them. They deliberately guard against data loss even where the original discarded overflow or unchecked metadata.

Integration status: the prototype's `GameState.Bag`, equipment and fixture crafting still use their earlier aggregate representation. The typed instance core and save format are implemented independently; migrating gameplay and old saves to it is the next integration task. Do not build a permanent synchronization layer between two inventory states. Cut over bag, equipment, crafting and save migration together, retaining the existing prototype acceptance checks and adding instance selection in the UI.
