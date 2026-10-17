# Iteration 045 — native object and trigger intake

This milestone reads the PVL object/trigger prefix after static meshes and preserves object ownership, animation frame/segment records, trigger actions, recipient strings, requirements, door payload flags and positioned arrival records in a private `.nwobjects.json` file. It does not execute native actions, open doors, bypass locks or claim level-transition parity.

Independent format implementation uses retail loader layout evidence from [wizardry-8-decomp](https://github.com/agluszak/wizardry-8-decomp/tree/075cbd0b853c4670e94f6ae4fe48d53b52b5e819): ReadLevel.cpp, Prop.cpp, AnimObj.cpp, AniMesh.cpp, ReadMesh.cpp, PathAI.cpp and Trigger.cpp. No reconstructed code is copied. The existing [Wizardry8Editor](https://github.com/anonidiot/Wizardry8Editor) navigation reader is a second layout reference. Retail executable checks and original-file results follow below; inferred presentation policy remains separate from native behavior.

The bounded reader consumes lights, monster paths, items, empty missiles, props, bitmaps, cameras, environment and level triggers. Every bank boundary is checked at its exact position. Dynamic mesh vertex/material indices, animation frame identities, LOD counts, segment bounds, finite float arrays, signed counts and supported discriminator/version ranges are validated. Unsupported layouts fail; the bytes after the trigger bank remain explicitly unparsed (camera sway, clip planes, particles, named positions and automap). Thus this is not a full PVL decoder.

Four authored tests cover a path-driven prop and attached locked door, positioned arrival, compressed dynamic mesh, truncation at every byte of the object prefix, and malformed markers/counts/versions/coordinates. Original assets and full object/trigger records remain private on the runner; public reports contain hashes, offsets and aggregate counts only.

## Viewer inspection

An optional `.nwobjects.json` next to a GLB is bound to the same PVL SHA-256. N cycles explicit positioned arrival triggers (action 0x34 with an empty recipient string), in flight mode. The viewer uses the native loader's serialized-coordinate factor 500 followed by preview scale 0.0013 and mirrored Z. It adds an authored 1.25-unit camera offset and preserves the current view direction; this is inspection navigation, not a recovered spawn or level-transition executor. F still enters capsule walking at an aimed floor.

O toggles nonphysical object markers; attached door payloads show their stored open/locked flags. Marker origins come from serialized prop/trigger positions. These are inspection aids, not rendered native prop meshes or runtime door states. No trigger callback, recipient dispatch, lock/item logic, animation, world save or transition is executed.

Runtime checks compare prop/door/arrival counts with the original report, visit every explicit arrival, and retain texture/geometry/collision checks. Linux CI also rejects a sidecar whose PVL hash is changed while its GLB remains intact.

Retail executable cross-checks: the item reader at 0x004BC413–0x004BC490 consumes a 12-byte position followed by four 4-byte fields before its trigger gate. This differs from the older editor navigation skip (24 bytes total after the name); the implementation follows the executable (28 bytes). The door payload reader at 0x004418AE–0x00441915 reads the 2-byte item ID, position gate, all 12 position bytes and 128-byte linked name; position bytes are always present even when the gate is zero. Type-4 version > 1 extras occupy 36 bytes (three data ints, a float, four bytes and four additional ints). Source semantics are recorded as layout evidence, not proof of native action execution.

## Original intake

Both native PVLs passed the object-prefix reader on the original-data host: https://github.com/Aetton/neowizardry/actions/runs/38984748878.

| Level | Props | Bitmaps | Trigger records | Door payloads | Explicit arrivals | Trigger-bank end | Unparsed tail |
|---|---:|---:|---:|---:|---:|---:|---:|
| Monastery1 | 62 | 32 | 90 | 7 | 2 | 6,050,398 | 20,144 |
| Monastery2 | 52 | 26 | 166 | 4 | 2 | 4,073,665 | 4,878 |

All 172 prop/bitmap records use version 9. Trigger records consist of super triggers (60 / 62) and ambient-sound records (30 / 104). Door-payload counts are identified by serialized payload kind, not object names. Action-0x34 records total 3 / 6, of which only 2 / 2 have explicit positions and empty recipients; the viewer only navigates that supported subset. Remaining teleport/transition ownership and dispatch are unresolved. PVL monster and item banks are empty in both files; that does not imply the levels have no monsters or items, since other spawn/state resources are not decoded here.

## Validation

The unchanged client source from `116e0d564932dc2cdf426fd80717e7f7d8910d9e` passes both original Godot levels: https://github.com/Aetton/neowizardry/actions/runs/38985030568. Godot navigates every explicit arrival (2 / 2), verifies prop/door counts (62/7 and 52/4), and retains all texture, animation and capsule collision checks. Linux checks, including 56 Python tests, authored navigation/collision and rejection of a mismatched object-sidecar hash, pass after removing an optional `rg` dependency from the runner assertion: https://github.com/Aetton/neowizardry/actions/runs/38985155154.

Windows run 58 compiled, exported and uploaded its artifact, but release creation received HTTP 403 after main's workflow files changed during the build. This matches the [documented release permission rule](https://docs.github.com/en/rest/releases/releases#create-a-release). The Windows workflow now diagnoses that race explicitly and retains the recorded source commit; it never retags a built artifact as a different source. A fresh current-main build follows; no extra token permissions are requested.

Final Windows preview 59 is published from `86b26bee4d685bc03eddf559941fd96445c60199`: https://github.com/Aetton/neowizardry/releases/tag/windows-build-59. Windows export, smoke checks and the launcher's first online/offline download verification pass: https://github.com/Aetton/neowizardry/actions/runs/38985377639. Final Linux checks pass: https://github.com/Aetton/neowizardry/actions/runs/38985377642. Client/exporter/fixture source is unchanged from the successful original-level runtime check. Both native object sidecars are generated privately in `_work/wiz_data/NeoWizardryWorld`; the public game ZIP contains no original-derived resources.
