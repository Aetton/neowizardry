# NeoWizardry

Improved Wizardry 8 remake research project. **Iteration 0.1 is a technical slice, not a recreated monastery or a complete RPG.**

Godot 4.5.1 .NET client + independent .NET 8 core + dependency-free Python 3.10+ asset tools.

![Technical slice: synthetic corridor and party UI](docs/preview.png)

## What works

- Read-only SLF index, SHA-256 inventory and selective extraction, verified against an original Steam `DATA.SLF` containing 3314 entries.
- A catalog of 819 original item records: IDs, names, item type/icon indices and equipment restriction masks. Generated catalogs stay local; original assets are not bundled.
- A synthetic 3D corridor with free movement, collisions, mouse look, party size 1–8, a training sentinel, round resolution, XP, sample dialogue and save/load.
- A solo captivity/escape regression fixture. This proves an engine state transition, **not** that the original Crock quest has been reproduced.
- Deterministic core transitions and versioned saves, tested independently of Godot.
- Original-item browser with search, raw weapon statistics and a test melee equipment slot enforcing race/profession/gender restrictions. Equipped items survive saves; loading requires the same source item database.
- Ordinary resource overlay: loose data files, then descending `PATCH.NNN`, then base archive, with source provenance.

Combat numbers and starting characters are **experimental placeholders**, not recovered Wizardry 8 formulas. No enemy scaling by party size. Reward is shared between living, uncaptured heroes, so solo retains the concentration of XP.

## Run

Install .NET SDK 8 and **Godot 4.5.1 .NET** (the standard Godot binary does not run C#). From the repository root:

```sh
python -m unittest discover -s tests -v
dotnet run --project core-checks
dotnet build client/NeoWizardry.csproj
godot --path client
```

Replace `godot` with the path to your Godot .NET executable if it is not in PATH.

WASD moves; hold the right mouse button to look; Esc releases the cursor. Approach the sentinel and press **Attack / next round**. Party size applies when pressing **New party**. **Save/Load** preserves the party, battle, captivity and camera. **Test captivity/Escape** demonstrates the solo regression fixture. The keeper's fixed line is sample text, not extracted story content.

```sh
godot --headless --path client -- --smoke
```

The smoke check intentionally writes `prototype-save.json` into Godot's user data directory. Do not use it as a real campaign save.

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

Repeat `--patch` for every enabled `PATCH.NNN`; do not pass disabled `.BAK` files. Use `--loose-dir "/path/to/Wizardry8/Data"` to include loose-file overrides. Open **Items** in the client; `local/items.json` loads automatically. An external catalog can be selected with `godot --path client -- --catalog "/path/to/items.json"`. This is an equipment inspector: ownership, inventory transfer, curse removal and weapon-based combat are not implemented.

`audit` reads SLF files and `PATCH.NNN` locally and produces a small inventory. The Drive connector cannot download `LEVELS.SLF` because its 497,064,244 bytes exceed the connector's 256 MiB limit. Local indexing avoids that restriction. Parallel Worlds and localization overlays remain unsupported.

## Next gates

1. Obtain and validate the real `LEVELS.SLF` directory, then decode PVL geometry and collisions.
2. Extend the verified ordinary patch/loose overlay to localization and special installation layouts; identify the executable baseline.
3. Recover canonical combat/race/class/skill formulas; replace experimental rules only with evidence and regression checks.
4. Reconstruct the real monastery, NPC dialogue and Crock event; validate solo without altering its late-game strength.
5. Replace the temporary gold frame with the planned Wizardry visual identity and Grimrock-inspired materials/lighting.

See [format report](docs/formats.md), [architecture](docs/architecture.md) and [iteration report](docs/iteration-001.md).
Latest progress: [iteration 002](docs/iteration-002.md).

The [formula research module](docs/formula-sources.md) contains a first source-backed candidate calculation; it remains separate from the experimental combat rules.
