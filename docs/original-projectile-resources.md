# Original projectile completion and resources

Static reconstruction against the supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Commercial version is not established. Addresses are virtual addresses in this binary, not portable symbols. No original-game execution comparison has been made; proprietary bytes and rows stay private.

## Dispatch correction

Iteration 018 incorrectly identified `0x549EF0` as the projectile launch helper. At `0x53E18C` the actual preparation call is `0x544B60`; `0x53E1A7` calls the adjacent sound helper `0x549EF0`. The latter reads the item's sound index at +0xC5 and calls `0x5499D0`. Continuation calls preparation at `0x53F9E7` and sound at `0x53F9FE`. The common swing allocation and repeated shooting/throwing conclusion remain valid. Preparation calls `0x544630` at `0x544D13`; the creator calls `0x4A2D30` at `0x544A99`, copies source/target snapshots and assigns the pending global pointer at +0x8C0. These observations do not recover projectile spread, collision, final hit probability or callback semantics.

## Common completion

| Native path | Observed transition |
|---|---|
| `0x53E8C3`, `0x53E918`, `0x53F45A` → `0x53F474` | Miss, blocked contact and resolved-hit paths converge before resource handling. Resource cost is not conditional on damage. |
| `0x53F4FF`–`0x53F559` | If an ammunition slot is selected, call `0x51E760(character, slot, true)` once. A depleted count invokes the separate replacement helper `0x51EB90`. |
| `0x53F733`–`0x53F746` | With no separate ammo, ordinary direct resource handling requires hand combat skill 17 and no use-spell byte at item +0x63. Spell-use execution is separate. |
| `0x53F74C`–`0x53F801` | Type 2 thrown source: flag +0x41 bit 0x80 skips cost. Otherwise decrement stack quantity, or invoke generic spending for nonstack modes, then run redistribution. If still absent/zero count, set remaining swings to one before the common decrement. |
| `0x53F803`–`0x53F84A` | Other direct source: generic spend with force-clear false, then try intrinsic-shot refill via `0x51EA90` when its charge byte is zero. |
| `0x53F86E`–`0x53F876` | Decrement remaining swings once. |
| `0x53F879` onward | Zero HP or status DWORD >=15 stops continuation. Remaining paths recheck usable hand, target and range and can retarget before another projectile. These predicates are caller inputs in the clean lifecycle. |

## Generic spending

`0x51E760` reads item mode +0x66. Mode 0 is unchanged except food (type 22; outside projectile scope). Mode 1 decrements one positive count byte and clears the slot at zero. Other modes decrement a positive charge byte; if it reaches zero, `0x520750` decides whether to retain the empty item. An already-zero counter is cleared only when force-clear is true. The clean lifecycle refuses to launch from an empty resource source, so it never needs this stale-slot cleanup branch.

`0x520750` returns true if any attack-mode bit 0–8 is set, byte +0x62 is nonzero, any byte +0x6C..+0x74 is nonzero, or byte +0x8C is nonzero. This is a capability predicate; it is not named or treated as a single rechargeable flag. An intrinsic-shot weapon with a supported attack mode therefore survives at zero shots; a capability-free charge source is removed.

`0x51EB90` checks launcher icons 11/12/13/37/131, tries a candidate from another slot with compatibility helper `0x51C8F0`, then searches for the previous ammo definition via `0x520F90`. Full compatibility and selection policy remain unresolved. `0x51EA90` handles mode-4 refill with a lookup at `0x616E6C` and helper `0x51E9F0`; the complete fuel/refill calculation is not implemented.

Thrown redistribution `0x51ED30` moves or splits a matching type-2 opposite-hand stack: filling main takes ceil(n/2), filling offhand takes floor(n/2). This slot choreography is evidence, not the new inventory architecture. Automatic selection/refill must be implemented separately before claiming native continuation parity.

## Clean implementation and scope

`ProjectileSeries` owns an immutable inventory transition and one allocated swing series. `Launch` records a pending ordinal and selected source snapshot without spending. `Complete` consumes one quantity/resource unit for a supplied Hit/Miss/Blocked outcome, clears pending, advances the ordinal and stops on exhaustion, actor incapacity or unavailable continuation. A reusable source costs zero. `Stop` before launch costs nothing; it cannot discard an in-flight projectile. Final completion is marked Finished. These states do not mutate HP or calculate contact/damage.

Series ID plus ordinal reject duplicate/foreign/stale completions against the **current** state. As with all immutable commands, the application must commit the returned state once; replaying an obsolete entire state is not prevented by a global event ledger. Persist the state with the caller's serializer, validate it on restore, and keep series IDs unique in that caller's encounter. Tests cover a pending JSON roundtrip.

The selected instance must retain its snapshot while in flight. A changed source fails before spending, making resource reservation an explicit caller responsibility. This module does not accept arbitrary inventory edits during flight. A launcher and its ammo compatibility/equipment remain caller gates; the ammo stack may live in an explicitly selected container. Equipment still holds one copy per slot. `ItemInventory.ConsumeUnit` can remove that equipped singleton for throwing; crafting retains its existing prohibition on equipped ingredients.

`OriginalWeaponAdapter.ProjectileExpenditure` translates validated native definitions to typed cost policies for an explicitly selected shooting/throwing weapon and source. It rejects direct spell-use weapons and non-reusable nonstack type-2 throwing, whose unusual count-based continuation is not reconstructed in the clean model. Ordinary mode-1 ammo/thrown, direct resource modes, retained empty sources and reusable throwing are covered. It does not establish actor eligibility, automatic replenishment or full ammo compatibility. Without automatic replenishment, selected-source depletion stops this series; the original may replace the source and continue. No new per-swing RNG is introduced here.

The playable training battle remains a fixture. No canonical actor stats, projectile contact calculation, original save import or full ranged encounter is implied by this module.
