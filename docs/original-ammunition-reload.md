# Original ammunition compatibility and refill

Iteration 022; static inspection of the supplied executable, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Commercial version is not established. No original runtime comparison. Addresses and numeric categories describe this executable; proprietary records and disassembly are not published.

## Definition compatibility

`0x51C8F0` uses weapon item WORD +0x3F (the imported IconIndex/category), then candidate category/type/range. Table at `0x51CB90` with selector bytes at `0x51CBA8` dispatches as follows:

| Weapon category | Candidate condition | Branch |
|---|---|---|
| 11, 37 | category 22 | `0x51C991` |
| 12 | category 83 | `0x51C9D1` |
| 13 | category 23 | `0x51C9B1` |
| 104, 110, 114 | category from fuel table below; additionally, if candidate type <=3, its range byte +0x47 must equal weapon range | `0x51C9F1` |
| 131 | Omnigun stage table below | `0x51CA64` |
| Other | false | `0x51CB87` |

These are definition predicates, not an equipment, ownership, resource availability or actor eligibility decision. `OriginalAmmunitionAdapter.Compatible` reconstructs this predicate. `OriginalWeaponAdapter.PreparePrimary` now rejects incompatible separate ammo before preparing damage; its existing typed category gate remains (type 4, or type 2 for Omnigun).

Omnigun dispatch is keyed by **weapon definition ID**, not directly by character level. `0x51CA64` subtracts 599, bounds against 11, then uses tables `0x51CC24`/`0x51CC34`:

| Definition IDs | Accepted candidate categories/types |
|---|---|
| 599 | 23, 113 |
| 600–601 | previous set plus 112 |
| 602–605 | previous set plus 21, 126, or type 2 with category 0 |
| 606–610 | previous set plus 22, 83 |
| Outside 599–610 | false |

Branches are `0x51CA82`, `0x51CAAB`, `0x51CAE2`, `0x51CB30`. Generic category zero requires type 2; it does not permit arbitrary category-zero items. This preserves the original baseline and does not implement the deferred Gadgeteer rework or establish how character level upgrades the weapon definition.

## Resource refill

The WORD-pair table at `0x616E6C`, terminated by zero, maps weapon category to fuel category:

| Weapon | Fuel |
|---|---|
| 114 | 113 |
| 110 | 132 |
| 104 | 132 |
| 144 | 143 |
| 145 | 146 |

Do not conflate this with compatibility dispatch: 144/145 are present in the refill table but absent from `0x51C8F0` dispatch. `0x51EA90` requires destination mode 4, finds its fuel category and calls `0x5213C0` to search the character's first 12 slots then 8 additional slots. The final search argument is true. No party-wide search is called in this path.

Transfer helper `0x51E9F0`:

1. Compute maximum `signed WORD(+0x67) + byte(+0x69) * byte(+0x6A)` from the destination definition. No random draw.
2. Source available units are count byte +4 for mode 1, otherwise charge byte +5.
3. Transfer `min(maximum - current, source units)` into destination charge byte.
4. Invoke generic spend `0x51E760(..., false)` once per transferred unit. Its recovered keep/remove-empty capability policy still applies.

The clean operation `ItemInventory.RefillResource` transfers these units atomically without a per-unit loop, preserves leftovers and removes exhausted quantity stacks or disposable resource sources. `OriginalAmmunitionAdapter.PrepareRefill` checks the native pair and mode, supplies maximum capacity and empty-fuel retention. Reject capacity outside 1..255 and a destination already above maximum instead of reproducing byte wrap/unsigned underflow. The supplied private catalog has four mode-4 weapons using these pairs, all with valid byte capacities; fuel definitions use modes 1/2. Only aggregate inspection is published.

## Replacement and continuation

`0x51EB90` first checks the preferred reserve at character +0xFC9 with `0x51C8F0`. If not compatible/present, it searches for the **previous ammunition definition ID** through `0x520F90` (first 12 slots, then 8 additional slots). It does not choose an arbitrary other compatible variant as the fallback. A successful selection is moved via `0x51FD20` into the depleted ammo slot. Inventory movement, messages and equipment recalculation are original engine/storage details.

`OriginalProjectileReload.ReplaceAmmunition` preserves preferred-compatible → previous-definition fallback priority. The application supplies the preferred instance and ordered, accessible actor-owned fallback IDs. There is no implicit global container scan or old native slot array. Missing IDs and empty resources are skipped. A compatible preferred variant may change the ammo damage formula, so prepare the next projectile from the **new** source; do not retain the previous ammunition's dice/accuracy snapshot.

`RefillDepletedWeapon` refills one explicitly selected fuel source. `RefillFromReserves` searches an explicitly ordered accessible list for its matching nonempty fuel category, uses one source and leaves other reserves alone, matching the single-source transfer call. The native search checks identity/category and can encounter stale empty records; the clean selector skips empty resource sources. It does not synthesize charges from an empty retained item.

`ProjectileSeries.ReplaceDepletedSource` resumes the same ID, ordinal and remaining allocation. It does not reroll or reset attacks/swings. A pending projectile cannot replace its source. Finished, actor-unavailable and continuation-unavailable series cannot resume through reload. Iteration 022 gives actor/continuation interruption priority over simultaneous depletion, preventing a reload from reviving an invalid continuation. Save/replay tests cover depleted and pending reloaded states.

## Remaining integration boundary

These are callable independent-core operations; the Godot training battle remains a labelled fixture. The caller owns actor/launcher binding, access/curse/equipment rules, eligibility and current-state commits. No live actor model is implied.

The native common completion attempts replacement/refill **before** final swing decrement and actor checks, including the last swing. The series continuation wrappers in this slice only operate on a Depleted series with swings remaining; they do not reproduce end-of-attack automatic replenishment or refill on incapacitation. The independent `Refill` inventory operation can be called outside a continuing series, but a full attack controller still needs to schedule native ordering. Projectile trajectory/contact, conditions, automatic retargeting, thrown opposite-hand redistribution, special spell-use execution and nonstack throwing remain outside this slice. Full ranged encounter parity is not claimed.
