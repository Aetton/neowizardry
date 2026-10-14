# Iteration 002 — original-item pipeline

## Delivered

- Read-only ordinary-installation overlay: loose files > highest enabled numeric PATCH.NNN > base archive. Sources and content hashes are recorded in catalog schema 2.
- Additional documented item fields: usage skill, range, signed initiative/to-hit, dice and constant, attack modes, weight, curse flag and signed bonus swings.
- .NET catalog loader, melee test slot with race/profession/gender restrictions, and source-bound equipment save/load. Iteration-001 saves remain readable.
- Godot item browser with name search, hero selection, statistics and test equipment assignment. The catalog is imported locally; no original databases are bundled.
- CI uses a generated synthetic catalog to exercise catalog→equipment→save/load in Godot without distributing originals.

## Original-byte findings

PATCH.001: 519421 bytes, SHA256 `74bbc59acc581ede5e6c148c47486e4f287ea3169deca260abc28d5e9f715936`.

It contains four resources. ITEMS.DBS, SPELLTABLES.DBS and STRINGDATA.DAT match DATA.SLF byte for byte in the supplied installation. `MONSTERS/GDEMON_DODGE.MON` is patch-only. The first iteration's item content was therefore correct, but its source selection did not yet model patch precedence.

Effective ITEMS.DBS source is PATCH.001; 819 records, table SHA256 `cbf0d8cc37cea6eff8fb287ba7f6e9f411ada0370a8666b41c20438484148901`.

Source fields and bit flags were cross-checked with Wizardry8Editor `src/item.cpp`, `src/item.h` and `src/character.h`. Numeric values are exported without guessing missing mechanics. Zero-dice damage fields are shown as a percentage bonus rather than misread as flat damage.

## Validation

- 12 Python tests passed: malformed archives, extraction safeguards, signed item fields, overlay order, disabled patches, duplicate patch numbers, loose-file ambiguity and symlinks.
- 89 portable C# checks passed; 92 checks with the private real catalog, including the faerie ninja restriction and dagger dice.
- Full Godot .NET build passed with zero warnings/errors.
- Real catalog (819 items) loaded in Godot; equipment and database-bound save/load smoke passed.
- Synthetic-catalog smoke passed using the same path as CI.
- Item browser rendered and visually inspected at 1280×800.

## Remaining limits

Equipping is a test assignment, not a simulated inventory economy. Original weapons do not yet change the experimental combat damage; curses are displayed but not enforced or removed. Only melee weapon types occupy the current test slot. Level geometry, canonical combat and the original Crock quest remain open.

LEVELS.SLF remains the same single 497064244-byte file on Drive; no internal level directory was obtained this iteration. The existing local audit command is the next input needed for real-level work.
