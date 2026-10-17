# NeoWizardry

Improved Wizardry 8 remake research project. **This is a technical slice, not a recreated monastery or a complete RPG.**

Godot 4.5.1 .NET client + independent .NET 8 core + dependency-free Python 3.10+ asset tools.

![Technical slice: synthetic corridor and party UI](docs/preview.png)

## What works

- Read-only SLF index, SHA-256 inventory and selective extraction, verified against an original Steam `DATA.SLF` containing 3314 entries.
- A catalog of 819 original item records: IDs, names, item type/icon indices and equipment restriction masks. Generated catalogs stay local; original assets are not bundled.
- A synthetic 3D corridor with free movement, collisions, mouse look, party size 1–8, a training sentinel, round resolution, XP, sample dialogue and save/load.
- A solo captivity/escape regression fixture. This proves an engine state transition, **not** that the original Crock quest has been reproduced.
- **Menu → New tactical battle** adds an 8 × 8 test field with 1–8 heroes and 1–8 opponents, individual positions, move/attack/wait/spell orders, obstacles, sight checks and enemy pathfinding. Movement/AI are authored laboratory rules; see [iteration 035](docs/iteration-035.md).
- Tactical enemies now have Brute / Hunter / Flanker / Skirmisher profiles, sight and expiring target memory, same-round retargeting and low-HP retreat. Inspect and change their tactics on the field; see [iteration 036](docs/iteration-036.md). These are authored policies for the current training opponents.
- New training parties use recovered character initiative, interleaved melee attack groups and explicit saved effective facts.
- With an original spell catalog loaded, the training Bishop casts Frost / Energy Blast / Heal Wounds at powers 1–7 through the shared battle executor, with realm mana and recovered damage/resistance arithmetic. Ordinary risk, fizzle/backfire and healing are connected; broader original conditions/effects/progression remain open; five synthetic spells remain in the separate sandbox.
- MONSTERS.DBS: 595 native reference records and Menu → Monster reference search. **Menu → New monster battle** admits complete supported combat profiles by their fields (79/595 in the supplied database), including multiple slots/modes, fumbles, absorption and native condition 16. **Mixed monster battle** selects individual enemies; **Monster reference** shows readiness/blockers for every row. The 516 blocked records still need other effects, magic or delivery mechanics. Grid AI is authored and XP/loot remain open. See [iteration 040](docs/iteration-040.md).
- Monster attack effects have recovered chance, source strength, ten ordinary condition routes, duration stages and binary resistance calculations, with explicit draw traces. Condition 16 now applies after damage, blocks actions, caps defense and persists across saves; its timer uses an explicit one-unit-per-round laboratory clock. Other status consumers remain open. See [iteration 039](docs/iteration-039.md) and [iteration 040](docs/iteration-040.md).
- Deterministic core transitions and versioned saves, tested independently of Godot. Imported melee damage dice and weapon initiative are active; the prototype RNG state survives saves.
- Original-item browser with search, raw weapon statistics and a melee equipment slot backed by the shared party bag enforcing race/profession/gender restrictions. Equipped items survive saves; loading requires the same source item database.
- Ordinary resource overlay: loose data files, then descending `PATCH.NNN`, then base archive, with source provenance.

Starting characters, opponent facts, room, targeting and rewards are **authored training fixtures**. The connected original formula subset and its remaining gates are documented in [iteration 038](docs/iteration-038.md), [iteration 037](docs/iteration-037.md), [iteration 036](docs/iteration-036.md), [iteration 035](docs/iteration-035.md) and [iteration 034](docs/iteration-034.md). No enemy scaling by party size. Reward is shared between living, uncaptured heroes, so solo retains the concentration of XP.

## Windows testing

Windows is the primary manual testing target. Download the launcher ZIP from a [Windows preview release](https://github.com/Aetton/neowizardry/releases), extract it once and double-click `RunNeoWizardry.cmd`. It checks for updates, verifies the downloaded package and launches the game. Game builds live under `%LOCALAPPDATA%/NeoWizardry`; saves remain in Godot's separate user-data directory. No Godot editor or .NET SDK installation is required. See [Windows preview guide](docs/windows.md).

## Run from source

Install .NET SDK 8 and **Godot 4.5.1 .NET** (the standard Godot binary does not run C#). From the repository root:

```sh
python -m unittest discover -s tests -v
dotnet run --project core-checks
dotnet build client/NeoWizardry.csproj
godot --path client
```

Replace `godot` with the path to your Godot .NET executable if it is not in PATH.

For multiple opponents, choose the party and opponent counts in **Menu → New tactical battle**. Choose an actor, an order and a target cell, then **Resolve round**. Attack/Magic reopen the tactical field; **Save/Load** retains all committed positions and opponent states.

WASD moves; hold the right mouse button to look; Esc releases the cursor. Start **Menu → New party**, approach the Training Duelist and press **Attack**. New expeditions execute the recovered ordinary melee rules; **Combat facts** shows the authored effective stats and **Spell sandbox** keeps the previous synthetic spell tests available. Existing saves retain their legacy combat mode. Open **Menu** for party size, **New party**, **Save/Load** and test-scenario controls. Click a side portrait to select the actor used by Magic, Items and Craft. **Save/Load** preserves the party, battle, captivity and camera. **Test captivity/Escape** demonstrates the solo regression fixture. The keeper's fixed line is sample text, not extracted story content.

```sh
godot --headless --path client -- --smoke
```

The smoke check intentionally writes `prototype-save.json` into Godot's user data directory. Do not use it as a real campaign save.

New expeditions receive a limited **test** starter bag; the training sentinel drops one configured test item on victory. These are not original starting equipment or loot tables. In **Items**, owned items are shown by default; **Browse full catalog** is inspection only and cannot grant unowned weapons. Old saves retain equipped copies but do not receive a retroactive starter bag.

New test parties can use **Magic** near the sentinel. Test Spark damages it; Test Mend heals an injured living party member. Casting replaces one hero's weapon attack for the round. Powers 1–3, costs and effects are fixtures; see [magic coverage](docs/magic.md). Existing saves do not receive retroactive learned spells or mana. Test Ward, Bind and Burn add protection, action skipping and timed damage. Queue one order per caster and resolve them together in one round.

New public-fixture parties can also use **Craft**: herbs and a vial make a test tonic; combine it with the unequipped test staff to make a tempered staff. The window shows quantities and skill gates. These recipes apply only to the synthetic catalog; see [crafting coverage](docs/crafting.md). Start a new party for test ingredients and recipe knowledge.

## Import your installation

No originals, full exported databases or copyrighted textures belong in this repository. Put local outputs in the ignored `local/` directory.

```sh
python -m tools.slf audit "/path/to/Steam/steamapps/common/Wizardry8" --out local/installation-audit.json
python -m tools.slf index "/path/to/Wizardry8/Data/DATA.SLF" --out local/data-index.json
python -m tools.slf items "/path/to/Wizardry8/Data/DATA.SLF" --out local/items.json
python -m tools.slf extract "/path/to/Wizardry8/Data/DATA.SLF" --pattern "DATABASES/*" --out local/extracted
```

Windows PowerShell uses the same commands, with your Windows installation path. Do not run extraction twice into the same destination: existing files are deliberately protected.

For the supplied Steam installation, include its enabled patch:

```sh
python -m tools.slf items "/path/to/Wizardry8/Data/DATA.SLF" --patch "/path/to/Wizardry8/Patches/PATCH.001" --out local/items.json
```

Repeat `--patch` for every enabled `PATCH.NNN`; do not pass disabled `.BAK` files. Use `--loose-dir "/path/to/Wizardry8/Data"` to include loose-file overrides. Open **Items** in the client; `local/items.json` loads automatically. An external catalog can be selected with `godot --path client -- --catalog "/path/to/items.json"`. The shared party bag tracks quantities and equipment ownership. Transfer a weapon by returning it to the bag and equipping it on another eligible hero. Items now selects individual spare copies and supports stack split/merge. Identity, quantity, identification, resource counters and equipment locations survive saves. Core resource spending is supported; use effects, recharge, curse removal and full original combat remain open. Schema-1/2/3/4/5/6/7/8 saves migrate to game schema 9 on load without invented combat facts; missing historical charge counters become zero. Assigned melee weapons now use their imported base damage dice and initiative in the experimental battle.

`audit` reads SLF files and `PATCH.NNN` locally and produces a small inventory. The Drive connector cannot download `LEVELS.SLF` because its 497,064,244 bytes exceed the connector's 256 MiB limit. Local indexing avoids that restriction. Parallel Worlds and localization overlays remain unsupported.

## Original spell reference

Export a private metadata catalog from your installation (include all enabled patches and loose overrides as for items):

```sh
python -m tools.slf spells "/path/to/Wizardry8/Data/DATA.SLF" --patch "/path/to/Wizardry8/Patches/PATCH.001" --out local/spells.json
godot --path client -- --spells local/spells.json --grimoire
```

The source client detects `local/spells.json`; packaged builds also accept `spells.json` in Godot's user-data directory, or an explicit `--spells` path. Open **Menu → Spell reference**. Public previews include only a clearly labeled synthetic entry. Reference metadata does not teach spells or enable canonical casting. See [format evidence and limits](docs/spell-format.md).

## Original combination reference

```sh
python -m tools.slf recipes "/path/to/Wizardry8/Data/DATA.SLF" --patch "/path/to/Wizardry8/Patches/PATCH.001" --out local/recipes.json
```

Open **Menu → Combination reference** after importing. The source client detects `local/recipes.json`; exported builds accept `user://recipes.json` or an explicit path:

```sh
godot --path client -- --recipes local/recipes.json --combinations
```

Search by result or ingredient name; flagged entries are hidden by default. Public previews contain one invented combination reference only. The recovered native batch/identification/skill calculation is tested separately from the shared-bag fixture: native instance creation and inventory integration remain open. See [format evidence and unresolved behavior](docs/recipe-format.md).

## Next gates

1. Obtain and validate the real `LEVELS.SLF` directory, then decode PVL geometry and collisions.
2. Extend the verified ordinary patch/loose overlay to localization and special installation layouts; identify the executable baseline.
3. Recover canonical combat/race/class/skill formulas; replace experimental rules only with evidence and regression checks.
4. Reconstruct the real monastery, NPC dialogue and Crock event; validate solo without altering its late-game strength.
5. Replace the temporary gold frame with the planned Wizardry visual identity and Grimrock-inspired materials/lighting.

See [format report](docs/formats.md), [architecture](docs/architecture.md) and [iteration report](docs/iteration-001.md).
Latest progress: [iteration 032](docs/iteration-032.md). Archive/code review: [audit 2026-10-15](docs/audit-2026-10-15.md). Native item data: [slot layout, quantities and charges](docs/item-instance-format.md). Desktop HUD: [original-layout interface](docs/interface.md).
Mobile prototype: [touch controls, Android build and device checklist](docs/mobile.md). Preview on desktop with `godot --path client -- --touch`.

Original [physical](docs/original-combat-formulas.md) rules now drive a scoped playable [ordinary character duel](docs/iteration-032.md). Original [magic](docs/original-magic-formulas.md) remains independent reference/math work. Physical coverage includes attack budgets, per-attack swing allocation for the common melee/projectile path, weapon adjustments, attack-mode modifiers, ordinary character scores, primary-hand bonuses and [physical damage with neutral melee composition](docs/original-physical-damage.md). Full canonical combat is still in reconstruction. The older [guide model](docs/formula-sources.md) remains a source comparison.


## Original monastery geometry preview

Iteration 042 adds a separate desktop viewer for original static geometry. Import locally with `python -m tools.pvl /path/Wizardry8/Levels/LEVELS.SLF --out local/world --report local/world-report.json --textures`, then launch the Windows game with `NeoWizardry.exe -- --world "C:\path\local\world\monastery1.glb"` (or `monastery2.glb`). On the self-hosted runner, Actions retains generated files at `_work/wiz_data/NeoWizardryWorld`.

WASD flies, Q/E moves vertically, right drag looks, wheel adjusts speed and Home restores overview. Iteration 043 adds original embedded textures, pixel alpha and IFL frame animation. Iteration 044 loads native static OCT/GameData collision from a matching `.nwcol` sidecar. Aim the center crosshair at a floor and press F to walk; F returns to flight. WASD walks with a capsule and gravity. Generate sidecars with `python -m tools.oct /path/Wizardry8/Levels/LEVELS.SLF --out local/world --report local/collision-report.json` after regenerating GLBs with the current exporter. Native lighting/material parity, native spawn points, dynamic doors/transitions and encounters remain pending. Original geometry is not bundled in the downloadable game. See [iteration 042](docs/iteration-042.md), [iteration 043](docs/iteration-043.md) and [iteration 044](docs/iteration-044.md).

Iteration 045 adds optional native object inspection: export with `python -m tools.world_objects /path/Wizardry8/Levels/LEVELS.SLF --out local/world --report local/objects-report.json`. Keep each `.nwobjects.json` beside its matching GLB. N cycles native explicit arrival points in flight mode; O toggles object markers with stored door flags. These are research navigation tools; native prop rendering, door actions/locks, level transitions and world saves are not connected. The self-hosted audit generates the sidecars automatically. See [iteration 045](docs/iteration-045.md).

Iteration 046 adds native door frame models: `python -m tools.world_doors /path/Wizardry8/Levels/LEVELS.SLF --out local/world --report local/doors-report.json`. Keep `.nwdoors.glb` and `.nwdoors.json` beside the main GLB. P previews the next door's native animation in flight and restores its original pose; R tries constrained manual interaction within reach. Locks, disabled triggers and unresolved state/link/timer conditions return a reason. F restores inspection poses before walking. Door/quest persistence and the original trigger controller remain pending. See [iteration 046](docs/iteration-046.md).

Iteration 047 adds **F5 save / F9 load** checkpoints to the world viewer: camera, flight speed and door animation state survive restarts. Checkpoints are tied to the exact imported world and sidecars, remain separate from expedition saves and refuse temporary animation-inspection poses. Loading resumes in flight; use F at an aimed floor to walk. Quest/campaign saves and encounter integration remain pending. See [iteration 047](docs/iteration-047.md).

Iteration 048 adds an opt-in connected expedition: append `--expedition --party-size 1` to the world command (1–8 supported). N reaches the first arrival/orange encounter marker; B within reach starts a physical battle panel. F5/F9 save party, inventory, world and combat together; defeated enemies stay defeated after loading. Catalogs beside the world are detected automatically. Creature rendering is still a placeholder; see [iteration 048](docs/iteration-048.md).

Iteration 049 replaces the expedition marker with the imported creature when matching `creature-<definition>.nwcreature.glb/.json` files exist beside the world. **V** focuses the model; entry to battle selects a creature view and victory restores exploration position. Initial original asset: Dusk Bat, five animation clips and seven textures. The original-level workflow exports it privately; manual import uses `python -m tools.creatures`. See [iteration 049](docs/iteration-049.md) for limits, including unsupported particle death.
