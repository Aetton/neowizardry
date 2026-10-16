# MONSTERS.DBS — text and defense import

The supplied database is 394489 bytes: a little-endian DWORD count of 595 followed by 595 records of **663 bytes**. Four terminated UTF-16LE text slots, each 48 bytes, start at 0, 48, 96 and 144. Slot 0 contains editor-like labels; remaining naming semantics stay explicit text slots. Bytes following a terminator are retained.

## Loader evidence and correction (034)

Iteration 033 incorrectly referred to a 485-byte runtime monster definition. That size belongs to a separate effect table. The actual monster accessor `0x4E5720` allocates **0x297 = 663 bytes** (`0x4E5772`) and calls `0x54A8A0`. The loader calculates `4 + speciesID*663` at `0x54A90A..0x54A918`, seeks, and reads 663 bytes directly into the returned definition (`0x54A930..0x54A940`). Its subsequent calls at `0x54A962..0x54A981` process the four text slots; there is no numerical-field relocation. This connects the existing defense-consumer offsets to on-disk bytes.

| Offset | Imported field | Interpretation |
|---|---|---|
| 0x15D | ArmorClass | signed byte; `0x542F11` |
| 0x15E | AnatomyCode | raw byte; labels not localized/interpreted |
| 0x15F..0x165 | LocationWeights | seven unsigned bytes; native cumulative d100 selection |
| 0x166..0x16C | LocationArmor | seven signed zone modifiers |
| 0x16D..0x175 | ModeArmor | nine signed mode modifiers, Swing through Shoot |

Selection and penetration evidence are detailed in [monster defense](original-monster-defense.md). Weights are neither normalized nor forced to sum to 100: the original's zone-3 fallback remains explicit. Signed monster modifiers retain their original arithmetic; they are not reinterpreted as character equipment armor.

`python -m tools.slf monsters <DATA.SLF> --patch <PATCH.001> --out local/monsters.json` uses loose/patch/base precedence; `--loose-dir` is supported. Schema 2 retains stable ordinal IDs, text slots, raw rows, row and whole-table SHA-256, provenance and verified defense fields. Core compares every exported field with the raw record and compiles immutable definitions for OriginalMonsterDefense. Schema-1 references remain readable, but compiling their absent typed defense fields requires reimport; no silent inference on load.

Client loads `local/monsters.json`, `user://monsters.json`, or `--monsters <path>`. Menu → Monster reference searches names and displays armor and zone/mode values. All 595 original definitions now pass location selection for all 100 draws and all nine attack modes through the existing defense rules. This is a content-to-rules integration, not an executable monster actor. HP/level/stat producers, attack definitions, AI, initiative, encounters, XP and loot remain separate work. Original rows/assets remain private.
