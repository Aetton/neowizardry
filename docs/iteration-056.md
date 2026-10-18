# Iteration 056 — placed first-floor items in the player campaign

The normal monastery player flow now loads original loose items, renders their original ITM models at saved STS positions and collects nearby visible items with E/click. An interaction hint appears when an item is close, in front of the player and unobstructed. Pickup preserves quantity, remaining resources and identification; it does not roll item creation dice. The bag displays unidentified items without revealing their names.

## Original boundary

Reference: wizardry-8-decomp `075cbd0b853c4670e94f6ae4fe48d53b52b5e819`, `LoadSaveGame.cpp::LoadItemStatus/LoadItem`, `item_spawning.h`, `layouts/item_instance.h`, `ItemManager.cpp::ActivateItem`, `Item.cpp::W8ItemRep::ReadFromFile` and `ReadMesh.cpp::ReadSingleLevelMeshBody/ReadMeshTransform`.

The ITEM chunk begins with the number of top-level groups. Each group is a chain of packed 0xad-byte world-item records; the saved next pointer signals another record, never a file offset to dereference. The decoder consumes exact chunk extents, bounds counts and rejects truncation/nonfinite positions. Original floor 1 contains 58 single-record groups: 37 visible placements and 21 hidden placements, covering 34 definitions. The hidden flag is retained; hidden items are neither rendered nor collectible while search mechanics remain unimplemented. These records are loose world items, not an implementation of TRES containers.

Model selection follows the record's internal model name, then native appearance lookup for an empty name, and the original questionmark fallback for a missing file. Version-2 ITM wraps the existing bounded dynamic-mesh reader; source local transforms, geometry, materials and textures are exported into private GLBs. No original assets are committed or included in public releases. Item yaw randomization, native pulse/rotation effects, lighting and exact native selection bounds are not claimed.

## Inventory and persistence

The manifest binds PVL/STS hashes, level, source location identity, native instance counters/identification, hidden state and model hashes. Pickup uses OriginalItemAdapter and the same GameState.Items inventory as equipment/combat. It creates one owned instance and marks the source location collected in one immutable transaction. Capacity failure leaves the world item untouched. Collection is blocked during battle/defeat, and hidden/unknown/already-collected identities are rejected. Native item manifests and every model join the prepared-content integrity marker; the world manifest joins the campaign content hash.

Campaign snapshots persist the bound item manifest and collected identity set. Load/new game/refresh apply the set to world visibility. Victory and save/reload cannot restore a collected placement or grant it twice. Research/legacy expeditions without item content remain supported.

A compatible native campaign from iteration 055 is migrated when its previous content hash matches the same current geometry/objects/doors/props/encounter without the added item manifest. The new snapshot preserves the exact party, inventory, RNG, XP, encounter phase, door state and exploration checkpoint, then attaches uncollected source items. The old file is retained. Foreign/incompatible or damaged saves are not silently converted. Adding the new private item content triggers in-app preparation; the remembered original installation path is reused where available.

## Validation

Local: 82 Python tests, 4227 core/input checks (465 connected expedition), client build with zero warnings/errors. Tests cover native chain framing and all truncations; ITM transforms and extents; model reuse; preserved stack count and charges; immutable pickup/capacity failure; hidden/duplicate pickup rejection; save/load source binding; and carrying collected items through battle/XP. Godot's ordinary-start fixture exercises real E targeting/pickup, model visibility, preserved instance state/RNG, reload and duplicate rejection. It also migrates a completed native campaign while retaining the old file. The existing research expedition smoke still passes.

Runtime source: `665134913b875372630359e993fc3d3b6f206712`. Linux run `39136055363` passed. Windows run `39136055393` passed and published [Windows 80](https://github.com/Aetton/neowizardry/releases/tag/windows-build-80), targeting that exact runtime commit. The packaged EXE exported 58 placements / 37 visible / 34 original models and passed player E pickup, preserved instance state/RNG, save/reload, duplicate prevention, the original three-crab fight (669 XP / six = 111 each) and migration of the completed native campaign. Original-data audit `39136055388` also passed: both original floors, production player startup, in-app preparation with the added item content, pickup/load/migration, the original crab encounter and the complete exported creature gallery. All runtime verification jobs are green.

The entire monastery scenario remains in progress: search, containers/treasure, remaining encounters, NPCs/events, keys/locks, transitions and native world AI are subsequent work under TOC-44.


## Next source boundary

Container work must not reinterpret the STS TRES payload as an item list. `Trigger.cpp::LoadTriggerActionData` reads version/count followed by 128-byte trigger names and 256-byte inline action state; the floor-1 chunk size (4232 bytes) accounts for 11 such records. Actual container behavior must be joined to the corresponding PVL trigger/action data. LOCK/LCKS separately restores named device/lock runtime state. `Monastery1.cpp::OnWheelStarActivated` requires the held star key (item 0x24c) and consumes it on success; its acquisition and the connected trigger chain remain unimplemented.
