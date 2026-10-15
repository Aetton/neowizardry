# Monster hit and penetration scores

Iteration 027. Static reconstruction of supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Commercial version unknown; no original-game execution comparison. Original records and disassembly stay private. These offsets describe selected runtime attack records and actors, not a claimed database/save layout.

## Selected attack and actor inputs

`0x542720` takes a monster actor, selected attack record, resolved mode and an untargeted flag. It reads **unsigned attack byte +4** as rating R, not the character BAR formula. Runtime signed byte +0x1DC is accuracy A; signed byte +0x1DD enters penetration as P. Four effective comparison attributes come from unsigned actor bytes +0x248..+0x24B; Speed used by the repetition term is +0x24A. Effective target attributes come from the matching character/monster target branch. Character actor construction/caps and monster definition import remain external.

`OriginalMonsterScores.Build` accepts explicit effective attributes, selected rating, signed adjustments, mode, fatigue policy, action counter, resolved hit flags, hit cap, scale and the already computed target defenses. It does not select attacks, infer rating from levels, use a prototype Power stat or traverse native pointers.

## Ordinary arithmetic

Let D be resolved target hit defense and AD its resolved location/mode penetration defense. Mode hit/penetration modifiers are the existing `OriginalCombatRules.ModeModifiers` values.

```
attributes = trunc((sum(attacker) - sum(target)) / 10)
rawUntargetedHit = R + 5*A + modeHit + hitFlags - hitFatigue - repetition
rawHit = rawUntargetedHit - 5*D + attributes
rawPenetration = R + 5*(A+P) + modePenetration - penetrationFatigue - 5*AD + attributes
```

Hit evidence: `0x542729`–`0x542755` builds R + 5A and mode; `0x542765`–`0x542782` adds +10 independently for each global +0x9A4/+0x9A5 flag; `0x542787`–`0x542804` applies fatigue and repetition. Targeted branch `0x542808`–`0x542922` subtracts five times defense via `0x542EE0` and adds the signed attribute difference divided by ten. Untargeted calls skip both target terms. No final probability clamp belongs in this builder.

Penetration evidence in the monster attack executor: `0x540AD4`–`0x540B13` builds R + 5(A+P) and mode penetration; `0x540B17`–`0x540B41` conditionally subtracts fatigue; `0x540B45`–`0x540B70` calls `0x542FC0` and subtracts five times its result; `0x540B74`–`0x540C74` adds attribute difference/10. `0x540C80` applies final scaling. Hit-only bonuses, repetition and hit cap do not enter this penetration path.

## Fatigue depends on native attack kind

The classifier is the selected attack record's **byte +1**, not the resolved attack-mode ordinal and not an equipment skill. Both branches scan 18 bytes at `0x61D284`:

`7,13,14,15,21,25,33,43,49,50,52,53,54,56,59,85,86,87`.

| Classification | Hit penalty | Penetration penalty |
|---|---|---|
| In the table | floor(fatigue penalty / 2) | full fatigue penalty |
| Outside the table | full fatigue penalty | zero |

This differs from the character Modern Weapon policy, where reduced hit fatigue accompanies **zero** penetration fatigue. Do not share those policies just because both halve the hit term. `OriginalMonsterAttackAdapter.FatiguePolicy` keeps native byte classification at the boundary; the rules use `MonsterAttackFatiguePolicy` without native IDs. Fatigue category is runtime +0x242, with the producer already documented in [monster defense](original-monster-defense.md). Ordinary category domain is 0..4; penalty table is 0/5/10/20/40.

## Repetition, hit cap and scaling order

The counter comes from DWORD +0x141 of the context referenced by actor +0x10 (`0x542731`, `0x5427B4`); its producer/lifecycle is still external. For counter > 0:

```
reserve = max(0, Speed + R - 25*counter - 50)
repetition = max(0, trunc(50 - reserve^0.75))
```

Evidence `0x5427B4`–`0x542804`. This shares arithmetic with character repetition but uses the selected attack's R and allows unsigned monster Speed up to 255. `OriginalPhysicalScores.NativeRepetitionPenalty` reuses the exact integer-root calculation; the public character helper retains its 0..125 Speed contract. No swing-index counter inference is introduced.

At `0x542926`–`0x54293E`, actor DWORD +0x87 nonzero limits the selected targeted/untargeted hit result to at most **10**, leaving lower/negative values intact. The semantic producer of that field is not inferred. This precedes `0x55CCB0`, so subsequent scaling can turn that cap into 6 or 14. Penetration does not use this hit cap.

`0x55CCB0` reads actor byte +0x16 and global DWORD `0x6850D5`. Recovered scaling matrix:

| Actor tag | Global setting | Scale |
|---|---|---|
| 1 | 0 | 60% |
| 1 | 2 | 140% |
| 2 | 0 | 140% |
| 2 | 2 | 60% |
| Any other pair | Any | unchanged |

These remain raw boundary values; no faction/difficulty names are asserted. `OriginalMonsterAttackAdapter.ScoreScale` resolves them to `PhysicalScoreScale`. Native arithmetic multiplies score by 3 or 7, multiplies that intermediate by 20, then divides by 100 toward zero. Checked widths preserve intermediate overflow boundaries; do not silently reduce fractions or introduce floating-point rounding. Both targeted and untargeted hit scores are capped before scaling; penetration is only scaled. The breakdown retains pre-cap/pre-scale values separately.

## Connected scope and validation

The output composes with `OriginalCharacterDefense.Against`: general character defense feeds D, selected body armor feeds AD; weighted displayed AC is not used. Tests connect a monster attack to a shielded character torso, then increase AD to stop penetration independently of hit and verify exact d100 equality. The calculation inputs roundtrip through JSON; this is not native actor import or a live save extension.

533 added checks include all 256 native kind bytes, nine modes, both fatigue policies/categories, signed attribute and effect boundaries, every 186 integer reserve values through repetition saturation, Speed up to 255, scaling matrix/truncation/intermediate overflow, hit flags, pre-scale cap, target defense composition and rejected invalid inputs. These are formula reconstruction tests, not captured original-runtime observations.

This closes ordinary monster **score arithmetic** from selected effective facts. It does not implement monster attack selection/budgets, context-counter lifecycle, fumble/redirection, special actions, monster damage modifier producers, spatial delivery or a symmetric live encounter. `0x54033E`/`0x5403B4` call targeted/untargeted hit separately; untargeted >=100 bypasses the cubic fumble term, but remaining target/redirect policy is not implemented here. Existing character-only `OriginalMeleeStrike` is not repurposed by pretending a monster is a player. Original magic damage/healing remains a required workstream.
