# Architecture

`core/` contains immutable record-based transitions with no Godot dependency. It owns party state, round ordering, XP distribution, captivity and versioned serialization. `client/` references the core and owns 3D scene, collision, camera, UI and user data storage. `tools/` reads originals locally and produces inventories/catalogs/extracted assets under ignored paths.

The UI currently exposes 1–8 characters, with no six-character assumption in the core. The maximum is a prototype constraint, not the final design promise. Enemy parameters are independent of party size. Renderer timing does not affect combat order.

Canonical mechanics and experimental decisions must remain distinguishable. The current combat is a deterministic test rule: stable speed plus weapon initiative order, imported melee dice or unarmed fixture damage, first eligible target, XP pool divided over eligible survivors. It does not yet implement initiative rolls, formations, real action selection, distances inside the simulation, conditions, classes, leveling or full inventory ownership. Range is checked only in the prototype client. A future rule implementation must move authoritative spatial constraints into the core.

Current dialogue is one fixed synthetic keeper response, with no LLM and no canonical NPC data. Captivity is an explicit state transition demonstrating solo recoverability. It is not a quest parser or a reproduction of Crock. The item browser now consumes imported schema-2 catalogs; equipment is an immutable metadata assignment with original restriction flags, now consumed by base melee damage and provisional initiative.

Import pipeline preserves source hashes and raw codes instead of guessing meanings. `Resources` implements ordinary loose-file and numbered-patch precedence with source provenance; Parallel Worlds and localization remain out of scope. Imported catalogs feed the equipment inspector and base melee rolls.

Windows is the primary manual test platform. Its CI exports the shared client, tests the exported executable and publishes immutable previews. The PowerShell launcher validates package hashes and keeps build files separate from Godot user-data saves. Android exports are manually triggered for mobile-specific checks and milestones.

Source assets, full exported tables and local source paths must remain outside public git. Research reports may contain hashes, aggregate counts and format offsets; the actual content stays in the user's private asset storage.
