# Iteration 001 — 2026-10-14

Repository: https://github.com/Aetton/neowizardry

## Delivered

- Read-only SLF CLI with inventory, hashing, extraction and installation audit.
- Verified original DATA.SLF: 149080821 bytes, 3314 entries, 13 database-directory files extracted.
- 819 item records parsed using documented names, type/icon indices and restriction masks. Catalog and originals remain private.
- Independent .NET 8 core: immutable transitions, deterministic sample rounds, conserved XP, versioned saves and solo captivity/escape fixture.
- Godot 4.5.1 .NET client: procedural 3D corridor, collisions, free movement, 1–8 party selection, sample combat, fixed keeper dialogue and save/load.
- Format, architecture and mechanics reports; repeatable CI workflow.

## Validation

- Python: 8 tests passed.
- .NET core: 63 checks passed, covering 1–8 parties, state isolation, repeatable rounds, XP conservation, duplicate reward prevention, death before queued action, schema validation and solo escape.
- Full client csproj build: passed, zero warnings/errors.
- Godot .NET headless scene: passed — UI combat, save/load and solo escape.
- GitHub CI has been defined; its remote execution status is separate from the local checks above.

## Explicit limits

This is a technical slice, not a playable reconstruction of the original monastery. The scene, characters, training sentinel and dialogue are synthetic. Combat formulas are experimental. Leveling, equipment, canonical NPCs/quests, formations and real level import are not implemented.

LEVELS.SLF exists on Drive, but its 497064244 bytes exceed the connector limit of 268435456 bytes. Its internal contents have not been read. The provided local audit command indexes it without transferring the original archive.

Patch priority, loose-file overrides and localization remain unresolved. The original Crock event and Miles workaround have not been reproduced; a synthetic solo-captivity test must not be mistaken for fixing the canonical quest.

## Next iteration

Get a local LEVELS.SLF inventory, inspect PVL and decode real geometry/collisions. Keep the experimental test rules until original combat rules can be tied to reproducible evidence. Continue original quest research separately.
