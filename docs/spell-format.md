# Native spell metadata research

Verified against the supplied DATA.SLF plus enabled PATCH.001. Implementation reads metadata only; the combat simulator still uses explicitly synthetic spells. No original spell has been made castable by this import.

## Evidence

Field layout was cross-checked against pinned Wizardry8Editor sources (format facts only; no source code copied):
- https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/dbHelper.cpp
- https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/spell.cpp
- https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/Localisation.cpp
- https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/spell.h
- https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/character.h

| Resource | Observed bytes | Evidence |
|---|---:|---|
| SPELLTABLES.DBS | 105608 | 8-byte header + 150 × 704-byte records |
| SPELLDESC.DBS | 25227 | Indexed UTF-16LE descriptions; 160 entries |
| SPELLEFFECT.DBS | 2573 | Footer index has 160 entries, all with identical 12-byte observed records; semantics unresolved |

The resolved spell table comes from PATCH.001. Its bytes happen to match DATA.SLF exactly: SHA256 `3526f12e544bcfcf132eda14072843040470b78b6e42be195cf02646a76a26bd`. This is precedence/provenance, not evidence that the patch changes spells. Descriptions resolve from DATA.SLF: SHA256 `a879ccba43211dc328b2e9d48ad17524a8e839b758fbfa9492103cd74ba765fc`.

There are 118 table rows with level >0 and 32 with level 0; these counts do not establish player-learnable spell coverage. Four positive-level rows have zero cost; the importer preserves them. A positive-cost filter would lose data.

## Spell record offsets

| Offset | Width | Exported field |
|---|---:|---|
| 0x145 | i32 | Duration per power, raw units |
| 0x149 | u8 | Alchemy book flag |
| 0x14a | i32 | Cost field |
| 0x14e | i32 | Base duration, raw units |
| 0x152 | u16 + u8 + u8 | Damage constant, dice, sides |
| 0x157 | i32 | Spell level field |
| 0x15b | u8 | Wizardry book flag |
| 0x19c | 132 bytes | Native UTF-16LE name field |
| 0x220 / 0x221 | u8 each | Divinity / Psionics book flags |
| 0x230 | i32 | Range code |
| 0x234 | i32 | Realm code |
| 0x238 | i32 | Target code |
| 0x23c | i32 | Usability code |
| 0x248 | i32 | Raw damage flag |

Unknown record bytes are preserved as private raw hex plus a per-record hash. School flags retain raw bytes, including unexpected values. Known realm/target/usability labels follow the editor enums; unknown codes remain visible as numbers. Native strings are imported without fanpatch/localization selection.

The description footer contains an entry count at EOF−8 and an opaque final word. The preceding count × 4 bytes are offsets. Each observed record has an 8-byte marker, a UTF-16 code-unit count and its text. The parser checks index/payload bounds, marker, text length, UTF-16 validity and overlaps. Exact shared pointers are allowed; surplus description entries are counted rather than silently equated to spell count. A table with fewer descriptions fails explicitly.

Duration constants are exported raw. The editor's comments suggest different round/time meanings in different contexts; this import does not manufacture a conversion. Damage fields do not establish power scaling, resistance reduction, fizzle/backfire or the complete effect list.

## Open research

SPELLEFFECT.DBS must not be assumed to contain the executable spell effect rules merely because of its name. All observed indexed records are structurally identical; no effect mechanics were inferred from that observation. Remaining spell-table bytes, script/executable behavior and original-game experiments are needed.

No resource filename in the supplied DATA.SLF directory directly identifies a recipe/combination table. This does not prove recipes are absent, hardcoded, or held in any particular DBS file. Ordinary combinations, Alchemy, gadgets, NPC recipes and quest combinations still need independent source identification.

LEVELS.SLF remains unavailable through the connector's file-size limit; world geometry has not been inspected in this step. Full original catalogs, descriptions and raw records remain under ignored local storage, outside public git/releases.
