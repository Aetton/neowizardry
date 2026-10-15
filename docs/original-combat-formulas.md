# Original physical attack calculations — verified subsets

Decision, 2026-10-15: use original combat/magic formulas as the remake baseline while implementing them as independent typed rules. Keep deliberate redesigns explicit. This is mathematical reconstruction, not preservation of original global state, offsets or memory layout.

Evidence: read-only disassembly of supplied `Wiz8.exe`, SHA256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Executable commercial version is not established. These are statically verified local paths; no original-game run or original save fixture was executed. Relevant code: `0x4EE130` and `0x4EE220`–`0x4EE656`. Profession/skill names agree with the pinned editor enums already used by the import pipeline.

## Scope and arithmetic

`Rules/OriginalCombatRules.cs` covers a usable single weapon with the other hand free, supplied effective attributes/skills, resolved weapon initiative and known load category. It does not resolve dual wielding, shields, hand availability, conditions, special weapon overrides, full contextual hit/penetration resolution or damage. Swing allocation, mode modifiers and ordinary character score builders are independent subsets below. The original character path forces one maximum swing for icon `0x90` at `0x4EE642`; in the supplied table that is Rocket Launcher. That override is outside this single-weapon arithmetic API.

Let `E` be the sum of class levels times their combat multipliers:

| Professions | Multiplier |
|---|---:|
| Fighter, Lord, Valkyrie, Ranger, Samurai, Ninja, Monk | 4 |
| Rogue, Gadgeteer, Bard | 3 |
| Priest, Alchemist, Bishop, Psionic, Mage | 2 |

`0x4EE196`–`0x4EE1F6` accumulates all positive class-history entries. The jump tables at `0x4EE204`/`0x4EE210` establish the groups. This is not just current level times current profession.

For effective weapon skill `W` and close/ranged skill `C`:

```
weighted = floor((4*W + 2*C) / 3)
S = floor(weighted * 10 / 20)
```

The numerator and unsigned divide are at `0x4EE3EB`–`0x4EE414`; the free-hand denominator 20 and final division are at `0x4EE41D`–`0x4EE460`. Other-hand branches alter both numerator and denominator; do not reuse this free-hand expression for dual wielding/shields.

Load contribution `L` is `0, -15, -30, -60, -120` for categories 0–4 (`0x4EE463`–`0x4EE4C4`). Skill code 7 (Modern Weapon) halves it toward zero (`0x4EE4C4`–`0x4EE4D7`), so category 1 becomes -7, not -8. Deriving categories from carried weight is a separate rule.

```
A = trunc((floor((DEX + SPD)/2) + E + S + L) / 3)
attacks = A >= 100 ? 3 : A >= 50 ? 2 : 1

V = trunc((SPD + 10*resolvedWeaponInitiative + E + S + L) / 3)
maximumSwings = V >= 100 ? 3 : V >= 67 ? 2 : 1
```

Here `trunc` means signed division toward zero. Attack arithmetic and inclusive thresholds: `0x4EE58E`–`0x4EE5EC`. Swing arithmetic/thresholds: `0x4EE5EF`–`0x4EE638`. The value is a maximum budget, not a promise of that many executed swings. Actual allocation and the traced continuation are described below.

## Resolved discrepancy

The previously retained [guide model](https://steamcommunity.com/sharedfiles/filedetails/?id=899973393), section 5.4, uses decimal approximations and is still preserved in `LegacyResearch` as a source model. The binary arithmetic supersedes it for this covered branch. With DEX=SPD=125, C=W=100 and Rogue level 25, `S=100`, `E=75`, `A=100`: three attacks. Level 24 gives `A=99`: two. The old decimal expression's 599.9999 at level 25 is not an original rounding rule.

The character branch explicitly caps the base count at three; do not infer a fourth base attack by extrapolating the guide score to 900. Extra swings, special weapons and extra actions are separate mechanisms.

## Integration gate

These functions are not wired to the fixture sentinel battle. Real effective attributes, class history, hand/skill selection, initiative, hit/penetration, conditions and action consumption must be modeled together before switching the playable combat rules. The current prototype heroes lack those original statistics; assigning invented defaults would hide a balance change. Iteration 019 recovers the ordinary character builders below. Next gates are effective actor/hand/defense selection, full hit/fumble/damage and resource flow, then monster-side counterparts.


## Attacks versus swings, including projectiles (iteration 018)

`AllocateSwings(maximumSwings, weaponBonus, draw)` recovers the common allocation at `0x53DA4D`–`0x53DACE`:

```
baseSwings = RngExclusive(maximumSwings) + 1
allocatedSwings = baseSwings + weaponBonus
```

The base bound is 1–3 in the covered character budget. A draw is made even when the bound is 1. The caller supplies one resolved zero-based draw per attack; this helper never generates random numbers or spends resources. The weapon adjustment is added after the draw, not inside its random bound. There is no three-swing cap after that addition. The clean implementation checks overflow and rejects a nonpositive allocation; it does not reproduce malformed data becoming an unsigned counter. Catalog parsing independently preserves the full signed field.

The remaining **attack** counter for the selected hand is decremented once at `0x53D980`–`0x53D9BE`. The separate remaining **swing** counter is set by the allocation above. This allocation precedes projectile dispatch: the start branch at `0x53E17C`–`0x53E1AC` calls `0x549EF0` for attack types >=2. Continuation decrements the swing counter at `0x53F86E`–`0x53F876`, then can launch another projectile through the same helper at `0x53F9D7`–`0x53F9FE`. Therefore the common multiple-swing path applies to shooting and throwing as well as melee. `0x519AC0` returns the equipped item's range/type byte at record offset `0x47` (or zero for the other hand categories); it is not a target-availability function.

With three attacks and maximum base swings 3, a +2 weapon allocates 3–5 swings per attack: 9–15 for the round if every attack begins. With zero weapon bonus, the same budget allocates 1–3 per attack. DEX contributes to the attack count; SPD, skills, class history and initiative also matter in their respective formulas. This is not a generic Dexterity percentage proc. Each attack gets its own draw.

The private 819-record catalog has 13 nonzero weapon adjustments, all +1 or +2; the triple-shot crossbow has +2. These observations do not establish that every projectile executes, consumes identical resources, hits or penetrates. Original continuation checks character/hand state and updates item resources before continuing. Thrown-stack exhaustion, ammunition selection, reload, retargeting and interruption behavior still need complete reconstruction; no free ammunition is inferred. A separate flagged special branch at `0x53DAD1`–`0x53DB31` can add another 3/4 swings under narrower hand/context checks. Its eligibility is unresolved and is excluded from this API; it must not be applied to every ranged attack or named as a specific class ability without tracing its producer.

### Item import correction

At `0x53DAC7` the original reads a DWORD at item definition `+0xAD`, then adds it to the swing count. `tools/slf.py` now exports the full little-endian signed 32-bit adjustment, replacing its former signed-byte read; `ItemCatalog` no longer rejects values outside a byte. Integer JSON representation and schemas 2/3 are unchanged. Synthetic regression cases cover positive/negative upper bytes and both 32-bit limits. All 819 private rows decode identically before/after because their upper bytes agree with the old values. Older exports from other installations should be regenerated to recover any previously truncated upper bytes. The pinned editor's byte accessor disagrees with the executable's operand width; the executable is the evidence for this correction.

## Attack-mode hit and penetration modifiers

`ModeModifiers` implements the two native jump tables at `0x542E98` and `0x542EBC`, selected by `0x542E10` (zero first argument for hit, nonzero for penetration). Table targets `0x542E2B`–`0x542E55` return constants. Hit builder `0x541C68`–`0x541C77` adds the hit modifier. These are additive score contributions, not final percentages or damage multipliers.

| Mode | Native ordinal | Item mask bit | Hit | Penetration |
|---|---:|---:|---:|---:|
| Swing | 0 | 0x001 | 0 | 0 |
| Thrust | 1 | 0x002 | -10 | +10 |
| Bash | 2 | 0x004 | 0 | -5 |
| Berserk | 3 | 0x008 | -30 | -30 |
| Throw | 4 | 0x010 | -5 | +5 |
| Punch | 5 | 0x020 | +5 | -5 |
| Kick | 6 | 0x040 | -10 | 0 |
| Lash | 7 | 0x080 | +10 | -10 |
| Shoot | 8 | 0x100 | 0 | +10 |

The semantic enum uses ordinals, not database flags. Selection from an item's supported bitmask is a separate unresolved rule; passing the entire bitmask as a mode is invalid. This table alone is not a complete chance-to-hit, armor penetration or Berserk damage implementation. Full builders include target defense, attributes, conditions and repeated-action context that remain outside the recovered subset.


## Ordinary character score builders (iteration 019)

`OriginalPhysicalScores.OrdinaryCharacter` takes explicit effective attributes, resolved hand bonuses, attack mode, load category and resolved defenses. It covers the ordinary arithmetic of `0x541C00` and `0x541EC0` with the two special +10 hit flags inactive, no post-target condition modification and neutral final context scaling. It returns **raw scores**, not final probabilities. It neither chooses equipment/targets/hit locations nor spends RNG/resources. The `PhysicalCombatAttributes` value is a calculation input, not a completed character/progression model.

### Base attack rating

For the already-covered free-other-hand branch without dual-wield state or the special NPC/quest modifier:

```
BAR = 60 + trunc((floor(DEX/2) + 2*S + L)/3)
```

`S` and `L` are the attack-budget skill contribution and load contribution above. `0x4EE460` stores `S` in hand +0x0D; `0x4EE50D`–`0x4EE535` computes BAR and stores hand +0x11. Class-history experience `E` is not added here. `PhysicalAttackBudget.BaseAttackRating` exposes this additional result. Original dual-wield/context and special NPC/quest branches at `0x4EE4D7`–`0x4EE50B` and `0x4EE538`–`0x4EE58B` remain outside that API.

### Hit and penetration scores

Let `T` be the resolved hand ToHit term, `P` the additional resolved hand penetration term, `Q` the signed accuracy adjustment, `U` the signed penetration adjustment. The latter two are read as signed bytes; the hand terms are resolved integer values. Let `D` be the target defense returned for hit, and `B` the mode/location-specific defense returned for penetration. These are **not interchangeable** with a displayed aggregate armor statistic.

```
attributeTerm = trunc(((INT+DEX+SPD+SEN)attacker - (INT+DEX+SPD+SEN)target)/10)
common = BAR + 5*T + 5*Q
hitBeforeTarget = common + modeHit - hitLoadPenalty - repetitionPenalty
hitScore = hitBeforeTarget - 5*D + attributeTerm
penetrationScore = common + 5*P + 5*U + modePenetration - penetrationLoadPenalty - 5*B + attributeTerm
```

Hit hand/accuracy/mode arithmetic: `0x541C47`–`0x541C82`. Penetration hand terms (+0x21/+0x25), adjustments and mode: `0x541EFA`–`0x541F46`. The two target-defense helpers are `0x542EE0` and `0x542FC0`. Their outputs are multiplied by -5 at `0x541D45`–`0x541D54` and `0x541F87`–`0x541F96`. They include conditional and location/mode calculations not reconstructed by this API; the caller must supply their resolved results rather than inventing an armor lookup.

Attribute sums and truncation: hit `0x541D58`–`0x541E46`; penetration `0x541F9A`–`0x542092`. Character attributes come from effective fields; monster target attributes are unsigned bytes at runtime +0x248..+0x24B. Input validation distinguishes the player effective cap 125 from monster byte range 255. A difference of -9 contributes 0, not -1; -10 contributes -1. Neither raw score is clamped to [0,100].

### Different load penalties

Helper `0x52A7D0`, table `0x52A800`, returns `0,5,10,20,40` for known categories 0–4. Hit calls it at `0x541CAB`–`0x541CC4`; Modern Weapon (skill 7) halves the penalty before subtraction, e.g. category 1 gives 2. Penetration at `0x541F4A`–`0x541F6D` **skips** this penalty for skill 7. This is a second load effect, distinct from the load contribution already included in BAR and the attack/swing budgets.

### Nonlinear context-counter penalty

The hit builder reads a counter at `0x541CC8` and bypasses the following penalty when it is zero:

```
reserve = max(0, SPD + BAR - 25*counter - 50)
repetitionPenalty = max(0, trunc(50 - reserve^0.75))
```

Evidence: `0x541CD2`–`0x541D15`, exponent 0.75 at `0x5EBF40`, subtraction constant 50 at `0x5EE7B8`. The call thunk `0x5E1F6C` imports MSVCRT `_CIpow`; `0x5E1C22` imports `_ftol`. Import-name RVAs are mapped through the PE sections, not assumed to equal file offsets.

The clean helper uses `50-ceil(fourthRoot(reserve^3))`, clamped to zero; reserves >=185 are already saturated at zero. Thus the finite nontrivial calculation uses only small integer powers and comparisons, avoiding cross-platform floating-point boundary differences. Perfect-power checks include reserves 1,16,81, and all integer reserves 0..185 agree with an independent evaluation of the recovered expression. This is static mathematical validation, not execution of the original CRT.

The counter has a reset at `0x4E854F` and a producer at `0x5367AB`–`0x5367B2` under additional context checks. Its full producer/lifecycle is unresolved. `ActionPenaltyCounter` therefore requires an explicit resolved value; do **not** increment it automatically per swing, per attack or per movement step by assumption. This penalty appears only in the inspected hit builder, not the penetration builder.

### Penetration comparison and integration limits

The ordinary branch at `0x53ED37`–`0x53ED4D` passes when its one-based d100 is <= the penetration score, or an earlier automatic-penetration flag bypasses the comparison. `Penetrates` accepts the caller's existing zero-based draw and explicit automatic context; it does not generate another roll or infer automatic eligibility. Exhaustive d100 checks cover negative, zero, interior and above-100 thresholds. This comparison alone does not establish a hit or deal damage.

Full hit resolution remains open: the melee path also computes a cubic fumble term from an untargeted score (`0x53E57E`–`0x53E60B`), scales it using an eligible-target list and can redirect an attack. Projectile contact has its own delivery path. Post-target condition adjustment (`0x541E4A`–`0x541E9F`), special +10 flags and final context scaling (`0x55CC00`) are excluded from `OrdinaryCharacter`; their eligibility must be resolved explicitly before expanding coverage. Do not label the unresolved global mode as difficulty without proving its meaning. Monster-side attack builders, damage, hit-location derivation and complete condition/resource flow remain separate gates in the [roadmap](roadmap.md).
