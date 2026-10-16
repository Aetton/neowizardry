# Original casting and resistance arithmetic — first verified subset

Baseline decision and executable provenance are shared with [physical attack research](original-combat-formulas.md). `Rules/OriginalMagicRules.cs` contains pure integer functions with supplied, bounded random results. No original offsets, globals, actor arrays or 9999-damage sentinel enter game state. The functions are not yet used by the synthetic spells in `Game.CastRound`.

This is static reconstruction of local arithmetic. Full book selection, class eligibility, learning, silence, casting source, target selection, skill training, time, mana side effects and complete effect execution remain outside the module. The executable's runtime spell record is 447 bytes; this is different from the imported 704-byte on-disk record. Runtime offsets below are evidence, not a proposed save layout.

## Ordinary player casting risk

`0x4FA4D0` prepares a player cast; `0x4FF4B0` computes UI risk colors using parallel arithmetic. Let `B` be the already-selected effective spellbook skill, `R` the effective realm skill, `p` power 1–7, `s` spell level 1–7, `m` base mana cost, and `c` the already-resolved effective caster class-level term.

```
K = floor((B + 4*R) / 5)
i = min(16, trunc(m/2) + s)
T = floor(D[i] * p / 7)
Fskill = K >= T ? 0 : floor(70*(T-K)/T)
Flevel = max(0, MinimumClassLevel[s] - c + p - 1) * s
F = Fskill + Flevel
```

Difficulty table `D`, indices 0–16, at VA `0x616DF0`:

```
30, 40, 50, 58, 64, 70, 76, 81, 86, 90, 94, 97, 100, 102, 105, 107, 110
```

Minimum class levels for spell levels 1–7 are `1, 3, 5, 8, 11, 14, 18` (`0x4ACBA0`). Hybrid offsets and previous-class accumulation are resolved by callers in the original, not folded into those minimums.

| Calculation | Execution evidence | UI evidence |
|---|---|---|
| Realm/book weighted skill | `0x4FA7E7`–`0x4FA81D` | `0x4FF4FB`–`0x4FF53C` |
| Difficulty index, power and skill failure | `0x4FA820`–`0x4FA898` | `0x4FF53F`–`0x4FF5B4` |
| Class-level deficit | `0x4FA950`–`0x4FA980` | `0x4FF68F`–`0x4FF6BF` |
| Color boundaries | — | `0x4FF74E`–`0x4FF78A` |

Colors: F=0 dark green; 1–5 light green; 6–15 yellow; 16–40 orange; >40 red. Do not clamp `F` to 100 before outcome resolution: it is a threshold and can exceed 100. The actual failure probability then saturates, while the backfire interval still changes. For example a raw threshold 245 gives 81 backfire outcomes when backfire is eligible, not the 33 obtained by clamping to 100 first.

The original also has situational modifiers (`0x4FA980`–`0x4FAA43` and UI counterpart `0x4FF6BF`–`0x4FF74E`), including a global mode/context branch and a spell-ID-specific +50 path in execution. They are not implemented by `OrdinaryCastingRisk`. The global context branch's field meaning is unresolved; do not label it a difficulty modifier by guessing. The skill selector `0x4FF7F0` has enabled-book and comparative-risk branches; the caller must not blindly substitute the highest displayed current skill. Class-level helpers `0x4FF3B0`/`0x4FFBD0` apply class offsets and eligible history; neither helper is exposed as a complete actor resolver yet.

Level-zero rows and automatic-power value 8 are outside ordinary executable power 1–7 input. Importing their metadata does not make them ordinary learnable/castable spells.

## Failure and backfire

At `0x4FB736`–`0x4FB7C1`, zero threshold skips the failure roll. Otherwise a single zero-based random value `r` in [0,99] is used:

```
r >= F -> success
r < F  -> failure
on failure, eligible backfire and F > 5 and r < floor(F/3) -> backfire
otherwise -> fizzle
```

`0x4FB77C`–`0x4FB78E` disables the ordinary backfire band at F<=5. Other branches suppress/change backfire eligibility for source/context/spell cases and call `0x4ACA60`; the API therefore takes an explicit caller-resolved eligibility flag. It does not claim every cast can backfire. The bound helper `0x40EFA0` returns modulo its argument, hence exclusive upper bounds; its RNG sequence is not reproduced.

## Power Cast and base effect strength

The activation flag and effective skill are read at `0x4FA651`–`0x4FA67A`:

```
bonusPercent = enabled ? floor(PowerCastSkill/4) + 1 : 0
scaledAmount = amount + floor(amount*bonusPercent/100)
```

The integer percentage helper is `0x517F90`; overflow is guarded in the remake instead of emulating machine wrap. Enabled skill 0 gives 1%; skill 100 gives 26%; skill 125 gives 32%. No new passive-attribute bonus is part of this recovered baseline (TOC-23 remains an explicit deferred design feature).

The base strength helper `0x4FF790` computes:

```
strength = max(0, floor(casterLevelTerm/2) + floor((floor(m/2)+s)/2) + p - (allEnemies ? 3 : 0))
```

Its player caller uses a separate class-level resolver (`0x4FFBD0`), not necessarily total character level. The inspected path applies half the integer Power Cast percentage to strength at `0x4FBCBC`–`0x4FBCD0`. Per-spell adjustments at `0x4FBC76`–`0x4FBCBC` occur before this scaling and are not part of `BaseEffectStrength`. Do not apply the strength formula or the full bonus blindly to every effect, heal, duration or damage path.

## Resistance reduction

The common helper `0x552250` reads a target's effective realm resistance and level. It accepts an already-resolved strength term from its caller. For ordinary damage (excluding its special 9999 sentinel):

```
Y = 3*(targetLevel - effectStrength) + effectiveResistance
if Y <= 0: unchanged damage, no resistance draws
N = Y + 1
H = floor(N/2)
a = RngExclusive(N-H)
b = RngExclusive(H)
reductionPercent = min(100, H+a+b)
remaining = rolledDamage - floor(rolledDamage*reductionPercent/100)
```

Target branches: monster base resistance plus signed runtime modifier at `0x552264`–`0x5522B4`; character effective realm resistance at `0x5522B6`–`0x5522DF`. Pressure and draws: `0x5522E5`–`0x552312`. Cap and subtraction: `0x552315`–`0x552333`.

For even positive Y the two draw bounds differ. At Y=4 they are 3 and 2 with baseline 2; they are not two identical Y/2 dice. For damage 101 and reduction 5%, the result is 96. One-point damage survives every reduction below 100%, because the reduction is floored before subtraction. A resistance value alone is not a fixed reduction percentage.

The original skips the helper entirely for damage value 9999 (`0x552255`–`0x55225E`). The clean arithmetic API rejects that sentinel; a future effect resolver must represent special/bypass effects explicitly. Call sites pass differing precomputed strength terms; do not universally replace them with `2*casterLevel` from a simplified source formula. Base damage dice, Power Cast scaling before reduction and effect-specific branches still require tracing end to end.

## Next integration gate

Ordinary character hit/penetration score arithmetic is now covered in iteration 019; full contextual resolution and monster-side counterparts remain open. Recover exact condition/resistance derivation and effective actor attributes/class history/book selection, then trace one direct-damage and one healing spell from order through costs, result and training. Only then switch representative live actions from fixtures. Keep the pure formula layer, actor validation, random draw service, effect resolver and presentation separate; previews must use the same rules without consuming RNG.

## Iteration 033: first live direct-damage subset

The training Mage now uses pinned Frost (4) and Energy Blast (5) records. Only ordinary zero-risk casts enter the executor; the public risk/backfire arithmetic does not imply a recovered failure lifecycle. Book selection and class/strength-level terms are explicit authored facts. The current pure-Mage single-class fixture uses level 10 for both terms; no general hybrid/history resolver is claimed.

Damage packet preparation `0x4FBD07..0x4FBD26` copies runtime +0x51 (signed constant, byte dice count, byte sides), multiplying the dice-count byte by selected power. These two supported rows have zero constant and 1d7. `0x551A20..0x551A51` rolls through `0x517970` then applies packet +0x24 Power Cast percentage with `0x517F90`; the sentinel path is excluded. Packet strength at +0x18 is passed to the realm resistance helper at `0x544FBE..0x544FDA`. Base strength and half-Power-Cast preparation remain as documented above; neither supported spell is in the special strength-adjustment table starting at ID 70.

On the safe ordinary path, the cast's selected power is written through the consumed-power output (`0x4FCE23..0x4FCE35`); the player caller multiplies consumed power by base cost and debits that realm (`0x4FAB75..0x4FAB89`). Do not extrapolate those costs to failed/backfired/interrupted casts. Successful spell execution normally includes skill-training work in the original; the training encounter leaves its authored effective skills fixed and does not claim that progression subsystem.

`RestoredMagic` prepares supported commands, while `MeleeCombat.Advance` owns scheduling, mutation and commit. A safe cast consumes no failure roll. Dice.Exclusive adds larger bounded draws for resistance without changing the pre-existing 1..255 Dice.Roll sequence or contract. Spatial delivery is immediate and authored; full native projectile/target/effect dispatch remains open. Current limits and validation are in [iteration 033](iteration-033.md).

## Iteration 034: ordinary risk outcomes and Heal Wounds

The restricted neutral training executor now admits ordinary risky Frost / Energy Blast and Heal Wounds. It still does not reproduce the entire original casting subsystem.

The single outcome draw and thresholds are as above. `0x4ACA60` classifies backfire eligibility by native target code (+0x137) and selected spell ID. For target 3, the table at `0x4ACAD8` selects `0x4ACA93`, enabling Frost (4) / Energy Blast (5); the exceptions are other IDs. For target 1 it selects `0x4ACAA5`; Heal Wounds (6) maps through byte index 3 at `0x4ACAEC` to the false branch. Healing can fizzle but does not backfire in this path. This is an explicit three-record policy, not a generalized target classifier.

On offensive backfire, `0x4FB7C3..0x4FB7FB` calls `0x4FEA50`; target-3 entry `0x4FEB25` reaches `0x4FE740` except for two other spell IDs. The player target branch `0x4FE7BD..0x4FE7D5` assigns the caster's character ID to the target and marks +0x1D. The executor applies damage/resistance to the caster, not the original enemy. Failure consumes no damage/resistance draws; success/backfire rolls the corresponding effect. Native presentation/reaction RNG is outside the remake's saved RNG parity claim.

The fizzle branch `0x4FB862..0x4FB8AF` proceeds to the shared consumed-power output at `0x4FCE23`, as does the backfire path through normal preparation. Both write selected power, then `0x4FAB75..0x4FAB89` debits realm mana as cost times that power. This establishes the ordinary completed-command cost; interrupted, source-item and automatic-power commands remain excluded.

Heal Wounds has dispatch entry `0x54BB8A`: normal execution reaches `0x54F190`. Character target handling at `0x54F226..0x54F27B` computes missing HP, skips healing dice at full HP, otherwise rolls the prepared dice packet (`0x517970`), applies full integer Power Cast (+0xD4), clamps the amount and calls `0x52ADD0`. That helper excludes dead/full targets and clamps resulting HP to maximum (`0x52AE1F..0x52AE4B`). No realm-resistance draws enter normal healing. The supported record is 4d4, multiplied by power during packet preparation. Full-health casting still pays the selected mana cost.

New authored parties use a level-10 Bishop with all selected book/realm effective skills set to 40, equal across enabled books, and 42 Fire/Water/Divine mana. This avoids inventing cross-book eligibility for the previous Mage; previous saved Mage facts and learned IDs remain unchanged. Power 7 on Frost / Energy Blast has threshold 14 (4 backfire results, 10 fizzle, 86 success). Caster/strength levels, no Power Cast, target level/resistances and zero timeline anchor remain authored resolved facts.

The original calls reaction/event hooks including `0x52E690` (a 70% branch after fizzle at `0x4FB81D..0x4FB85F`) and skill progression work after casting. Their full lifecycle, native condition/event production, interruption, animation timing and continuous-combat contexts remain unimplemented. The live training subset commits outcome, HP/mana and its documented RNG schedule, not all native reactions. An unavailable healing target cancels without cost under the authored room targeting policy. Out-of-combat healing is not exposed yet.
