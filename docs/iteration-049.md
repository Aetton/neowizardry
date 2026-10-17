# Iteration 049 — original creature frames and cosmetic playback

The expedition can now render its imported creature instead of the orange placeholder. The initial private export covers Dusk Bat, definition 282: **five clips, 156 frames, 37,128 frame triangles and seven original textures**. IDLE, WALK, ATTACK_CLOSE, DODGE and GET_HIT are selected by MLS cycle names; native per-cycle rates and valid frame endpoints are preserved. Definition lookup uses the narrow `cycle_name` field at MONSTERS.DBS +0x189, not the generic/display name slots. Resource selection retains loose Data > highest enabled patch > archive precedence, including the separate Data/Monsters/MONSTERS.SLF namespace.

Independent loader/layout evidence: [fixed decomp revision](https://github.com/agluszak/wizardry-8-decomp/tree/075cbd0b853c4670e94f6ae4fe48d53b52b5e819), particularly MonsterManager's cycle-name lookup, ReadOrCloneMonsterCycles, LoadGrCycle/ReadGrCycleData, AnimObjReadFromFile and ReadMesh. Existing independent dynamic mesh, TGA and GLB conversion code is reused. Original mesh-instance placement/scale is superseded by the actor presentation transform; decoded native vertex frames use the same `(x,y,-z)*0.0013` viewing conversion as the world and the MLS ScaleFactor. Random size/hover variation is not reproduced.

## Import and use

```powershell
python -m tools.creatures "C:/Wizardry8/Data/DATA.SLF" "C:/Wizardry8/Data/Monsters/MONSTERS.SLF" --definition 282 --loose-dir "C:/Wizardry8/Data" --out "C:/NeoWizardryWorld" --report "C:/NeoWizardryWorld/creature-report.json"
```

Add `--patch` for each enabled PATCH.NNN, as for item/monster catalogs. Generated `creature-282.nwcreature.glb` and `.json` stay beside the imported world and private catalogs. The original-level workflow creates these automatically outside the checkout, under the existing private world cache; no original-derived files are bundled in public releases.

In `--expedition` mode a matching asset replaces the marker; **V** focuses it. Battle entry focuses the creature automatically. A post-round cosmetic cue queue plays locomotion, attack and hurt clips without owning action outcomes. Attack cues currently observe the existing combat event text; movement and damage cues use authoritative state differences. This is authored presentation, not recovered native event-frame timing or a continuous-world movement controller. Rapid new orders replace the old cosmetic queue; gameplay never waits for animation or consumes RNG for it. DODGE is imported/tested but is not selected from uncertain miss/fumble semantics.

During battle, the expedition save retains the pre-battle exploration checkpoint. Cosmetic focus cannot overwrite the return position; victory restores that view. Loading active combat restores the same gameplay state and restarts cosmetic IDLE/focus. Cosmetic frame/time is intentionally not persisted. The creature disappears after victory and remains present after party defeat. Visual files are deliberately outside the expedition content hash, allowing presentation replacement without invalidating gameplay saves, but each load verifies exact definition/database/record binding, GLB SHA256, complete frame ownership and render-vertex bounds. A mismatched renderer cannot be applied to a saved encounter of another definition.

## Limits

Bat death uses native particle attachments; that cycle is unsupported and is not replaced by a fabricated native death animation. Particle cycles/root paths fail closed. Damage skin stages, default skin swaps/material overrides, sound/shake events, random scale/hover, smooth vertex interpolation and native animation/camera/lighting parity remain open. Ordinary missing assets retain the named placeholder; `--world-creature-required` makes missing assets an explicit failure for original-data checks. The same authored encounter placement, party stats, grid and reward limitations from iteration 048 remain.

## Validation

- 63 Python tests, including new compressed MON/frame/UV/rate/binding and malformed-input cases, pass locally.
- 3,930 portable core/input checks pass; client builds without warnings/errors.
- Godot checks authored clip visibility, IDLE looping, attack/hurt sequencing and return to IDLE, plus save/load and no gameplay mutation during playback.
- A local original-data run loads all five clips/156 frames, verifies embedded texture pixels/bounds, wins the encounter and preserves mid-battle continuation/no respawn.
- Linux CI passed, including refusal of a corrupted creature GLB: https://github.com/Aetton/neowizardry/actions/runs/39074204446 .
- Both original monastery levels loaded and bounds-checked creature 282 (5 clips / 156 frames / 7 textures), exercised loop/cue playback and completed the solo expedition with save continuation/no respawn: https://github.com/Aetton/neowizardry/actions/runs/39074204437 .
- Complete Windows build/export/EXE/updater publication gates passed: https://github.com/Aetton/neowizardry/actions/runs/39074204451 . Published preview: https://github.com/Aetton/neowizardry/releases/tag/windows-build-66 . All gates use runtime source d884888bfe93a3ff6a028dfb6c3bcee4629a012f.
- Headless checks do not establish visual comparison with the original renderer.
