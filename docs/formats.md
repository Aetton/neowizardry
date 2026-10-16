# Original data baseline — 2026-10-14

## Verified bytes

Original provided via the user's Google Drive, `Legacy/Wizardry8/Data/DATA.SLF`:

| Property | Value |
|---|---|
| Size | 149080821 bytes |
| SHA-256 | `12ce5055097da6710de995b2465713781190cb00515cd81a0c26fd7732d348cd` |
| Header archive name | `DATA.SLF` |
| Header base | `Data\` |
| Total entries | 3314 |
| Active entries | 3314 |
| Extracted database-directory files | 13 |
| Parsed item records | 819 |

## SLF layout implemented

The reader follows the format already used by [Wizardry8Editor](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/SLFFile.cpp), then verifies ranges against the actual archive. This project does not claim to have discovered the format.

| Offset | Size | Interpretation |
|---|---|---|
| 0 | 256 | Zero-padded archive name |
| 256 | 256 | Zero-padded base folder |
| 512 | 4 | Directory entry count, little-endian |
| 516 | 4 | Header used count, preserved but not used as active-file authority |
| 520 | 12 | Remaining header, not semantically decoded |
| EOF − count × 280 | count × 280 | Directory |

Each directory entry has a 256-byte path, a little-endian 32-bit offset and size, a state byte at 264 and remaining bytes not decoded. State 0 is treated as active; other states are retained in the inventory and skipped for reads. Paths are normalized for Windows separators and case-insensitive lookup. Base folder is metadata, not automatically prepended to entry names.

No decompression is needed for the observed resources. Invalid table sizes, out-of-payload offsets, overlaps, duplicate active names, path traversal, symlink escapes and extraction overwrite are rejected. This reader is for ordinary local files, not an adversarial concurrently changing filesystem. Malformed archives may leave partial extraction output if the source changes during extraction.

## Resource inventory

| Extension | Count |
|---|---:|
| STI | 1327 |
| TGA | 753 |
| ITM | 348 |
| WAV | 228 |
| MON | 171 |
| NSF | 165 |
| MLS | 156 |
| IFL | 63 |
| MSG | 51 |
| MPL | 32 |
| DBS | 12 |
| TXT | 4 |
| PCX | 2 |
| INI | 1 |
| DAT | 1 |

The database directory includes encounter tables, facts, hit sounds, item descriptions, items, item tables, levels, missile tables, monsters, NPCs, spell descriptions, spell effects and spell tables. ITEMS.DBS has a supported field subset. Iteration 011 also decodes native SPELLTABLES.DBS metadata and indexed SPELLDESC.DBS descriptions; see [spell format research](spell-format.md). Other database schemas and executable effect semantics remain unresolved.

## ITEMS.DBS fields implemented

Layout cross-checked with [dbHelper.cpp](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/dbHelper.cpp) and [item.cpp](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/item.cpp), and actual size `220315 = 4 + 819 × 269`.

| Record offset | Field |
|---|---|
| 0x00–0x3b | 30 UTF-16LE code units of name |
| 0x3e | Raw item type code |
| 0x3f | 16-bit icon index |
| 0x76 | 16-bit profession mask |
| 0x78 | 16-bit race mask |
| 0x7c | Gender mask byte |

Masks are exported as raw values. Subsequent iterations add selected weapon statistics, spell metadata and combination references without inventing unresolved mechanics. Iteration 014 adds item creation/slot fields in schema 3, retaining schema-2 reader compatibility; see [native instance evidence](item-instance-format.md). Localization and special-case mechanics remain separate. Unsupported fixed-record lengths fail explicitly.

## Levels

The original `LEVELS.SLF` was verified on the self-hosted Windows runner in iteration 041: 497064244 bytes, SHA-256 `38e63c2109ac903ee122fe214264f331b598bdef0357d921303af56c1de081de`, 4956 active entries. Both monastery PVLs are present among 426 monastery resources. Drive's transfer-size limit no longer blocks research.

[Iteration 042](iteration-042.md) documents the strictly parsed static PVL mesh prefix and private GLB export. Texture names and UVs are available; texture rendering, OCT collision and the remainder of PVL are not decoded. See the report for exact per-level prefix boundaries and remaining byte counts.

## Existing tools

Wizardry8Editor has a BSD-2-Clause-style file license, an existing SLF reader and documented item offsets. It is the main source consulted for this iteration. Older SLF viewers and Cosmic Forge remain useful research candidates; neither was executed in this environment. No editor code or original assets were copied into this repository.


## Prototype save schema 2

The outer client envelope remains schema 1 (camera and item database hash). Its nested game is schema 2 with `Items` (typed schema-1 instance inventory) and `NextItemSequence`. Bag ownership and weapon ownership are locations on those instances, not extra hero fields. The loader migrates game schema 1 using its `Bag` and hero `EquippedItemId`, requiring a matching catalog at the client boundary. It does not rewrite source files on load. Missing legacy resource counters become zero; unidentified/curse state was not recorded by those old prototype saves. Full original game save decoding is unrelated and remains unimplemented. See [inventory architecture](architecture.md#inventory-boundary--decision-2026-10-15).

Iteration 033 begins [MONSTERS.DBS import](monster-format.md): 595 fixed 663-byte rows, four native text slots, full evidence/hashes and a reference browser. Semantic combat fields are still unresolved.

Iteration 034 confirms the direct 663-byte native monster loader and adds schema-2 defense fields (signed AC, seven weights/zone modifiers and nine mode modifiers) connected to the defense rules. This corrects the prior 485-byte runtime-monster warning; see monster-format.md.
