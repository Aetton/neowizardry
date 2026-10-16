# Iteration 042 — original static monastery geometry

The next world-import step is a strict reader for the static-mesh prefix of PVL and an opt-in Godot geometry viewer. The importer uses the original archive on the Windows runner; original and generated geometry stay outside public git, artifacts and releases.

## Scope and format evidence

Research source: [Wizardry8Editor Window3DNavigator.cpp](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/Window3DNavigator.cpp), specifically LoadPVL and LoadMesh. This is an independent implementation of the documented byte layout. The file is authored by Anonymous Idiot and distributed under its BSD-2-Clause-style notice; no editor implementation is copied.

The prefix has signed mesh count, an unresolved 32-bit field and a signed 16-bit material count. Each material occupies 298 bytes for type 4 or 282 otherwise, including the leading type byte. After the material sentinel, every mesh contains a 28-byte header; positions; UVs; optional per-vertex material values; triangle UV indices; triangle vertex indices; material indices; normals; static lighting; polygon equations; and optional sunlight values. The importer validates exact sizes, finite geometry and index ranges, then requires the per-mesh and final static-geometry sentinels. It never searches for sentinel bytes inside payloads.

The remaining PVL tail is explicitly unparsed. Lights, monsters, items, missiles, props, bitmaps, cameras, fog and triggers are not imported. OCT collision, doors and transitions remain separate research. The editor itself describes its collision as an approximation rather than an OCT reconstruction; its material-name passthrough heuristics are not transferred.

## Private export and viewing

```sh
python -m tools.pvl /path/Wizardry8/Levels/LEVELS.SLF --out local/world --report local/monastery-geometry.json
```

Both monastery PVLs must parse before output is generated. GLBs contain original static triangles and per-corner UVs, grouped by source mesh/material. Expanding triangle corners preserves UV seams. Preview coordinates use `(x,y,-z) * 0.0013`, reversing winding; the scale is the editor's viewing convention, not recovered native world units. Materials use authored debug colors. Native texture references are reported but textures, original transparency, animation, lighting and collision are not reconstructed.

```powershell
.\NeoWizardry.exe -- --world "D:\actions-runner\_work\wiz_data\NeoWizardryWorld\monastery1.glb"
```

`--world` enters a separate static viewer, skipping the training room, game state and autosaves. WASD flies; Q/E moves down/up; right drag looks; wheel changes speed; Home frames the complete level. This is a desktop research viewer, not a playable monastery or a native spawn point.

Actions generates private GLBs beside the original installation at `wiz_data/NeoWizardryWorld`. Its public artifact contains only metadata reports. A separate Godot process loads each original-derived GLB, checks finite bounds and compares the imported triangle count with the PVL report. Timeouts, import errors and missing success markers fail the job.

## Validation

42 Python tests include six new geometry checks: conditional layouts and tail boundary; index/non-finite rejection; truncation/count/sentinel rejection; GLB structure, triangle counts and UV seams; both-level preflight; private export and original-source preservation. The client compiles with zero warnings/errors. Original-resource and Godot runtime results are recorded after the Actions run; synthetic fixtures alone do not validate original geometry.
