# Iteration 041 — monastery resource intake through the Windows runner

The next milestone is a connected section of the original monastery. Combat baseline 040 is sufficient to begin world import; full monster and spell coverage remains a separate workstream.

## Intake

The user identified `wiz_data/Wizardry8` in the self-hosted runner's work directory as the original installation source. `Original monastery resource audit` checks that directory in the checkout, its runner workspace and the parent `_work` directory. It does not search other installations or disks. An explicit repository variable `WIZ8_ROOT` (installation directory) or runner environment `NEO_WIZ8_ROOT` can disambiguate locations.

The world and Windows preview workflows use `checkout` with `clean: false`, and `wiz_data/` is ignored by git. This protects the user-supplied original folder when it resides inside the shared checkout. Original resources are read only and are never added to git or release assets.

`python -m tools.world_audit ".../Wizardry8/Levels/LEVELS.SLF" --out local/monastery-report.json` validates the SLF directory, hashes the archive and each active `Monastery/` entry, and checks for both `Monastery1.PVL` and `Monastery2.PVL`. Only the metadata report is uploaded by Actions. It contains counts, resource names, sizes and hashes; installation paths and original resource bytes are omitted. `--extract local/monastery` optionally extracts the selected files locally and refuses existing targets.

## Evidence boundary

The two PVL names are investigation targets from Wizardry8Editor's Level.cpp at commit `4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01`; that file provides the level-name mapping, not a geometry decoder. This iteration does not claim to decode PVL geometry, collision, doors, transitions or encounters. Base-archive inventory also does not resolve patch/loose overrides or external texture/model dependencies.

The Drive connector rejects the 497064244-byte archive at its 268435456-byte limit. An unauthenticated direct-download probe reached a Google sign-in page. Actions therefore reads the explicitly supplied runner folder instead of relying on an unauthenticated Drive download.

## Validation

36 Python tests pass, including four new synthetic-archive checks: scoped inventory/hash provenance; missing/inactive required levels; private extraction and overwrite refusal; corrupt archive rejection. These fixtures do not validate an original PVL parser. The Actions run separately establishes whether the runner folder is populated and the original archive can be read.
