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
