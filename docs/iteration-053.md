# Iteration 053 — ordinary player startup enters the monastery

Previous releases launched the training room by default. Original monastery geometry, doors and creature effects required developer flags and separately prepared private assets. Therefore those engine features were not available through the normal player launch. This iteration fixes that entry path.

## Player flow

Double-click the existing Windows launcher or exported EXE. If a prepared installation is configured, enter the first monastery floor directly and resume its campaign save. Otherwise the app locates a standard Steam/GOG installation when unambiguous, or opens a directory picker for the user's Wizardry 8 installation. It prepares original geometry, textures, collision, object data, original prop/bitmap poses, doors, item catalog and all available creature models through the shipped importer. The Windows package includes portable Python; no SDK/editor/Python installation or command-line flags are needed. Original assets stay local, outside the public package.

Preparation runs asynchronously with progress. Output sets are staged and promoted only after required files are verified; failure preserves earlier sets. A fingerprint and file hashes allow reuse and detect damaged content. The selected world is saved in the user's application data and survives launcher updates. An invalid setup/load shows a repair path, never silently falls back to training.

The first floor opens at a native arrival, projected to nearby clear walkable ground. Six heroes share the existing party/inventory/battle state. The player walks with collision and mouse look; debug flight, marker inspection, pose browsing and camera teleports are not the default controls. E/click interacts with doors or the connected encounter. I opens the party inventory; equipment changes use the shared ownership/restriction rules. Esc opens the menu; F5/F9 save/load. Victory returns to walking; campaign saves are separate from old training/research saves. Automatic saves run periodically and after resolved battle actions; a failed existing-save load blocks overwriting it until the player explicitly starts a new party. Starting over retains the old file as a backup.

Non-door native props and bitmap meshes now have a private pose export and are rendered with original textures; source-collidable props also block the walking capsule. Door geometry is excluded from that export to avoid duplication. Saved content binding includes the prop representation.

## Scope that remains open

This makes the monastery the actual player entry point and exposes its imported floor geometry and supported interactions. **It does not make the complete original monastery campaign finished.** The current connected encounter and party setup are authored; native spawn tables, NPC dialogue/quests, containers/loot, lock/key/state dispatch, transitions and original event scripting still need integration. Prop geometry uses its initial pose; arbitrary prop action scripts are not invented. Unsupported source representations remain listed in the private prop report. The old training laboratory remains available explicitly with `--training`; explicit world/gallery/research flags still work.

## Validation

75 Python tests and 4,196 core/input checks pass locally. The client builds with zero warnings/errors. A no-argument Godot launch with prepared synthetic content enters walking mode with six heroes, original-style creature data, doors, player HUD and save/resume. A no-content launch opens setup instead of training. Existing world/door/collision/expedition smoke checks still pass.

Windows CI now tests the ordinary packaged EXE with no developer arguments, both without content and against private original monastery assets. The original-data workflow separately exercises the in-app preparation path on a player cache and the full original gallery. Results are recorded after completion.
