# Native item slots and local initialization

## Scope and source

Read-only static analysis of the supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`, together with the patched `ITEMS.DBS`. The local initializer at VA `0x51C020` is called by the previously recovered [combination routine](recipe-format.md). These observations have not been checked against an interactive original-game run.

`NativeItemInstance` preserves one 12-byte slot. It is not a parser for a complete original save/character file and supplies no persistent instance identity by itself. `NativeItemFactory` reconstructs the local value initialized before inventory placement, not the complete operation with equipment, cursor, owner or progression side effects.

## Database fields

| Item-record offset | Encoding | Meaning in initializer |
|---|---|---|
| `0x41` | byte | Flags; bit 0 makes the initialized item identified |
| `0x66` | byte | 0 single, 1 stack, 2 charges, 3 uses, 4 shots |
| `0x67` | **signed** int16 LE | Base amount for modes 1–4 |
| `0x69` | byte | Number of amount dice |
| `0x6A` | byte | Sides per amount die |
| `0x6B` | byte | Maximum stack metadata; not a clamp in this initializer |

The [independent item reader](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/item.cpp) documents the mode/slot fields but reads the base as unsigned. The inspected EXE explicitly sign-extends it at `0x51C1A5` and `0x51797E`. The importer therefore keeps it signed. No negative bases occur in the provided table; a synthetic negative-base fixture verifies that the distinction survives import.

New `items` exports use catalog schema 3 and add an `instance` object to each item: `mode`, `amount_constant`, `amount_dice`, `amount_sides`, `max_stack`, `flags_41`, and the full `raw_record_hex`. The C# reader checks the raw hash and these fields against the record. Unknown mode/flag values remain exportable and inspectable; unsupported modes cannot be executed by the factory. Schema 2 remains readable, with absent native fields left absent. Reimport to obtain the additional data.

The supplied patched resource has SHA-256 `cbf0d8cc37cea6eff8fb287ba7f6e9f411ada0370a8666b41c20438484148901`. Its 819 entries contain:

| Mode | Entries |
|---|---:|
| Single | 613 |
| Stack | 127 |
| Charges | 68 |
| Uses | 7 |
| Shots | 4 |

216 entries set identification flag bit 0. All supported amount maxima in modes 1–4 fit a byte. Six charged definitions have a zero maximum; these remain zero rather than being silently raised to one. Single-mode records can have nonzero amount fields, which this initializer ignores.

## Slot layout

The [independent reader's constructor](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/item.cpp) and the executable's stride/writes agree on:

| Offset | Size | Field |
|---|---:|---|
| `+0` | 4 | Item ID bits, little-endian; `0xFFFFFFFF` denotes empty |
| `+4` | 1 | Raw stack count |
| `+5` | 1 | Raw charges/uses/shots |
| `+6` | 1 | Identification byte; nonzero passes the combination gate |
| `+7`–`+9` | 3 | Unknown, retained verbatim |
| `+10` | 1 | Raw uncursed field (editor interpretation), retained verbatim |
| `+11` | 1 | Unknown byte; an initializer argument can set it to 1 |

Codec roundtrips preserve all bits, including unused bytes in empty slots, nonboolean flags and unrecognized ID patterns. Decode requires exactly 12 bytes. Runtime adapters reject empty/mismatched definitions rather than normalizing away evidence. A single or charged item has one normalized ingredient instance even if its unused raw count is zero; charges are not treated as copies.

## Initializer evidence

- The local path clears the 12-byte value at `0x51C08B`–`0x51C09B`; the owner argument is null in the combination call. The definition ID is assigned at `0x51C18F`.
- `0x51C199`–`0x51C19F` skips amount generation for mode 0. For nonzero modes, the checked stored maximum is base + dice × sides at `0x51C1A5`–`0x51C1C3`.
- The argument tested at `0x51C1DF` selects the stored maximum or calls the dice helper at `0x51C1F7`.
- Helper `0x517970` starts from the signed base and adds `0x40EFA0(sides) + 1` once per die. The bounded random helper returns zero immediately for sides 0, without calling its random source; thus this degenerate normal path adds one per die. The maximum path instead uses stored sides literally. No such zero-sided nonzero-dice amount occurs in the supplied mode 1–4 records.
- `0x51C205`–`0x51C239` writes the amount to slot `+4` for mode 1 or `+5` for modes 2–4. It does not apply the maximum-stack field.
- `0x51C23C`–`0x51C254` identifies the result when requested explicitly or when database flag bit 0 is set. `0x51C258`–`0x51C265` conditionally sets `+11`; its gameplay meaning remains unresolved.

The factory accepts already-resolved **zero-based die results**, not a replacement random algorithm. It requires the exact number of results and bounds each result to its sides. Mode 0 and the stored-maximum path require no results. Unsafe amounts outside 0–255 are rejected; this is a deliberate implementation guard rather than emulation of the original assertion followed by byte wrapping.

## Combination output

The combination call passes zero for all three initializer flags at `0x51F670`–`0x51F674`. The initializer runs before `0x51F681`–`0x51F68F` overwrites a stackable output's count with the batch. `CreateCombinationOutput` retains that sequence: it still requires initialization dice even when the count will be replaced. Charged outputs keep the newly initialized charge amount. The local identification value follows the definition flag; later identification/inventory side effects are outside this model.

This closes local slot decoding and amount initialization, not original crafting in `GameState`. The clean instance core now supplies identity, placement, quantity merging, resource spending and atomic consumption/production independently of the original byte layout. Gameplay integration, whole-save import, recharge, actor eligibility, skill progression and time still need their own verified implementations. Deferred crafting, magic and Gadgeteer rework features do not change these recovered baseline rules.


## Recovered stack helpers (iteration 015)

Evidence: the same executable hash recorded above, inspected without executing it.

- `0x51F900` merges stacks after normalizing the destination via `0x51FB40`. It requires mode 1, matching definition IDs and exact equality of identification byte +6. It transfers the smaller of free room and source quantity. AL is true only when the source is completely consumed; partial transfer mutates both slots while returning false (`0x51FB20`–`0x51FB31`). Other metadata is not compared.
- `0x51FB40` repairs misplaced counters: modes 2–4 move count to charges only when charges are zero, then clear count; mode 1 moves charges to count only when count is zero, then clears charges. Mode 0 is left alone. Overflow produces chunks up to definition maximum, keeping the original at maximum. A 13-count stack with maximum 5 becomes 5 + 5 + 3.
- The original bag overflow path tests 500 entries (`0x51FC1A`–`0x51FC26`). When no space remains, it can clamp the source without preserving all remaining quantity (`0x51FCEA`–`0x51FD00`). The research utility instead rejects insufficient capacity before returning any mutation. A zero maximum is also rejected, rather than clearing the stack.
- `0x51FE30` clears source byte +10 before copying the 12-byte slot (`0x51FEA6`–`0x51FEB5`) and empties the source afterward. Owner, cursor and compaction side effects are outside the local model. This does not establish that ordinary movement should clear the remake's semantic curse state.

These are import/research findings, not requirements to reproduce inventory bugs. The research snapshot refuses merges with differing non-count metadata; the canonical typed inventory has its own explicit compatibility and capacity validation. No original source bytes or fixed slot layout enter canonical runtime saves.
