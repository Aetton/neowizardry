# Native item-combination references

## Evidence

Read-only analysis of the supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`, connects combination logic to the existing 269-byte item records. This hash identifies the exact inspected file; its commercial version is not established here. No original executable or exported table is committed.

The assertion string `pMergePCItem->iItemNo != -1` at file offset `0x21AAD4` is referenced by code at VA `0x51F32A`, inside the routine starting at `0x51F2F0`. The adjacent source assertion identifies `PC Item.cpp`. Disassembly with `objdump -d -Mintel` establishes:

- At `0x51F390`, the item database pointer is loaded from `0x68516C`; at `0x51F396` the scan begins at record offset `0xB9`. The loop advances by `0x10D` at `0x51F3BC` and is bounded by the count at `0x683F78`.
- `0x51F3A0`–`0x51F3B5` compares two signed 32-bit ingredient IDs at offsets `0xB9` and `0xBD` against the two supplied items, accepting either order.
- `0x51F3D3` reads byte `0xC9`. `0xFF` bypasses the skill comparison. Otherwise it reads an actor field indexed by this signed skill code, and at `0x51F403`–`0x51F410` compares it to unsigned byte `0xCA`, accepting greater than or equal.
- The scan index is passed to the item creation routine at `0x51F676`–`0x51F67C`. Thus each scanned record describes a potential output item, not a separate recipe ID namespace.

Skill codes 23 (Engineering), 26 (Alchemy), and 0 (Sword) agree with the independently maintained [Wizardry8Editor skill enum](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/character.h). The exporter preserves codes as numbers. A zero threshold with code 0 must not silently be rewritten to the `0xFF` no-skill sentinel.

## Layout

`DATABASES/ITEMS.DBS` has a 4-byte little-endian record count followed by exactly count × 269 bytes. Item IDs are zero-based record indices.

| Record offset | Encoding | Interpretation |
|---|---|---|
| `0x00` | 60 bytes UTF-16LE | Output item name |
| `0xB9` | signed int32 LE | First ingredient ID |
| `0xBD` | signed int32 LE | Second ingredient ID |
| `0xC1`–`0xC8` | 8 bytes | Preserved, unresolved by this analysis |
| `0xC9` | byte | Skill code; `0xFF` skips this comparison |
| `0xCA` | byte | Minimum skill value |

The ordinary patch resolver selects `PATCH.001` in the supplied installation, resource SHA-256 `cbf0d8cc37cea6eff8fb287ba7f6e9f411ada0370a8666b41c20438484148901`. Its 819 item records contain 749 pairs `(-1,-1)`, 34 pairs `(-2,-2)` and 36 other pairs. The exporter omits those two uniform negative pairs and reports their separate counts. It does not assign a gameplay meaning to `-2`.

Of 36 retained entries, 35 have named outputs: 20 use code 23, 12 code 26, two code 255, and one code 0/threshold 0. The remaining record has an empty output name, ingredients `(0,0)` and code 0/threshold 0. It is retained with `unnamed_output`, rather than promoted to an executable recipe or silently discarded. Its cause and reachability require further investigation. Repeated ingredient IDs and duplicate ingredient pairs are preserved in database order.

## Import and limits

```sh
python -m tools.slf recipes /path/to/Data/DATA.SLF --patch /path/to/Patches/PATCH.001 --out local/recipes.json
```

Include all enabled patches and loose overrides, as for item/spell imports. Output contains source provenance, raw record bytes, per-record hashes, names, numeric references and anomaly flags. Out-of-range references remain inspectable with an explicit issue; malformed table sizes reject import. No original records or names are bundled in public fixtures.

This is a **reference-only export**, not connected to the Craft action. It establishes ingredient matching and the direct skill threshold, but does not establish ingredient quantities, item charges, identification, equipped-item handling, failure behavior, discovery, time costs, profession gates elsewhere, or NPC crafting. The routine contains further branches for stack handling and item removal that have not yet been reconstructed. Offsets `0xC1`–`0xC8` remain raw. Existing synthetic crafting rules must not be substituted for these unresolved rules.

Cosmic Forge 4.38 and its author's tutorial were also inspected. The tutorial concentrates on world/editor operations and did not supply a combination schema. The author's [feature description](https://spershin.wixsite.com/mad-god-tiny-hut/cosmic-forge) explicitly limits new spell effects because effects are largely hardcoded. That supports executable research for magic; it does not prove that all crafting data is hardcoded. This investigation found combination fields in `ITEMS.DBS` instead.
