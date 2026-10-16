# Iteration 041 — monastery resource intake through the Windows runner

The next milestone is a connected section of the original monastery. Combat baseline 040 is sufficient to begin world import; full monster and spell coverage remains a separate workstream.

## Intake

The user identified `wiz_data/Wizardry8` in the self-hosted runner's work directory as the original installation source. `Original monastery resource audit` checks that directory in the runner workspace and the parent `_work` directory, outside the checkout. It does not search other installations or disks. An explicit repository variable `WIZ8_ROOT` (installation directory) or runner environment `NEO_WIZ8_ROOT` can disambiguate locations.

The world and Windows preview workflows reject `wiz_data` inside the checkout before running checkout. The runner currently uses the checkout archive fallback because Git is unavailable; that fallback replaces the checkout even with `clean: false`. Originals must remain outside the checkout, as they do in the verified `_work/wiz_data/Wizardry8` location. `wiz_data/` is also ignored by git. Original resources are read only and are never added to git or release assets.

`python -m tools.world_audit ".../Wizardry8/Levels/LEVELS.SLF" --out local/monastery-report.json` validates the SLF directory, hashes the archive and each active `Monastery/` entry, and checks for both `Monastery1.PVL` and `Monastery2.PVL`. Only the metadata report is uploaded by Actions. It contains counts, resource names, sizes and hashes; installation paths and original resource bytes are omitted. `--extract local/monastery` optionally extracts the selected files locally and refuses existing targets.

## Evidence boundary

The two PVL names are investigation targets from Wizardry8Editor's Level.cpp at commit `4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01`; that file provides the level-name mapping, not a geometry decoder. This iteration does not claim to decode PVL geometry, collision, doors, transitions or encounters. Base-archive inventory also does not resolve patch/loose overrides or external texture/model dependencies.

The Drive connector rejects the 497064244-byte archive at its 268435456-byte limit. An unauthenticated direct-download probe reached a Google sign-in page. Actions therefore reads the explicitly supplied runner folder instead of relying on an unauthenticated Drive download.

## Validation

36 Python tests pass, including four new synthetic-archive checks: scoped inventory/hash provenance; missing/inactive required levels; private extraction and overwrite refusal; corrupt archive rejection. These fixtures do not validate an original PVL parser. The Actions run separately establishes whether the runner folder is populated and the original archive can be read.

## Original archive result

[Audit run 38965906954](https://github.com/Aetton/neowizardry/actions/runs/38965906954) succeeded on the Windows runner. The original archive is 497064244 bytes, SHA-256 `38e63c2109ac903ee122fe214264f331b598bdef0357d921303af56c1de081de`, with 4956 active resources. Its 426 monastery resources total 34457024 bytes: 407 TGA, 9 IFL and two each of OCT, PVL, RLK, STS and WPT.

| Resource | Bytes | SHA-256 |
|---|---:|---|
| MONASTERY/MONASTERY1.PVL | 6070542 | 24e068333c31818279d039caaaef073f5909c3419c0942472386a57fd9d7e665 |
| MONASTERY/MONASTERY2.PVL | 4078543 | 0fe6b0d2431e7bafe9766072ddb29e5b3f52dfe9a2f2e53ddba9079c7888625f |

The resource-access gate is closed. Next: investigate PVL sections and external dependencies on the runner, then decode geometry/collision before connecting exploration and combat. The audit does not establish that every dependency of the level is in the Monastery directory.
