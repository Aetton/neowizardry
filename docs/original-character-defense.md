# Character defense, body armor and fatigue

Iteration 025. Static reconstruction of supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Commercial version unknown. No original-game execution comparison. Semantic attribute/skill names are cross-checked with the already retained [editor enum reference](https://github.com/anonidiot/Wizardry8Editor/blob/4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01/src/character.h); formulas and offsets below come from the executable. Proprietary records/disassembly remain private.

## Correction: fatigue is not equipment load

Earlier score documentation/API called the category read from character +0x169E `LoadCategory`. This was a semantic identification error. The inventory encumbrance category is +0xBC9; +0x169E is fatigue. Hit/penetration builders at `0x541CAB` and `0x541F58` read **fatigue**. Attack/swing budgets and primary-hand encumbrance still use +0xBC9.

`OriginalPhysicalScores` now calls the input `FatigueCategory`, and its breakdown fields are `HitFatiguePenalty`/`PenetrationFatiguePenalty`. The integer penalty table and Modern Weapon treatment are unchanged; no existing live fixture stats are migrated or assigned invented values. This is a calculation-input API rename, not a game save schema change. Do not feed the inventory load category into that renamed input when connecting actors.

`0x52A48A`–`0x52A4EF` derives fatigue after the resolved maximum/current stamina update:

```
fatiguePercent = 100 - floor(100 * currentStamina / usableMaximumStamina)
category = 0 below 50, 1 below 70, 2 below 85, 3 below 95, otherwise 4
scorePenalty = [0,5,10,20,40][category]
```

`OriginalFatigueRules` implements this ratio and lookup. Usable maximum already includes drain/max-stat policy; that producer remains external. Reject zero maximum, overfilled/negative stamina and unsafe original-width multiplication. For max=100, current=51/50 maps to categories 0/1; 31/30 to 1/2; 16/15 to 2/3; 6/5 to 3/4. Remaining percentage truncates before subtraction.

## Character defense producer

`0x4EE9D0` builds character AC components. Effective Speed is +0x14D, Shield skill +0x287, Stealth +0x345, Reflexion enabled/effective value +0x741/+0x747. The named input records require effective facts; they do not generate them from race, class level, training or old prototype Power.

| Contribution | Recovered arithmetic | Evidence |
|---|---|---|
| Natural armor | +2 when resolved trait query 0x16 succeeds | `0x4EEAA0`–`0x4EEAAF` |
| Speed | +1 at >=80 and >=90; -1 at <20 and <10 | `0x4EEAB3`–`0x4EEADF` |
| Stealth | floor(effective skill /10) | `0x4EEAE5`–`0x4EEB03` |
| Shield equipment | Sum signed item +0x62 for type 5 in applicable nonbody active slots | `0x4EEA31`–`0x4EEA91` |
| Ordinary shield skill | For positive shield armor H, add min(floor(Shield/20), H) | `0x4EEB28`–`0x4EEB74` |
| Defensive-action shield skill | Instead add min(floor(Shield/15), floor(3H/2)) | `0x4EEB32`–`0x4EEB54` |
| Other equipment | Sum signed +0x62 for non-shield items in applicable nonbody slots | `0x4EEA80`–`0x4EEA84` |
| Spell armor / penetration | Signed aggregate bytes +0x1774 / +0x1775, kept separate | `0x4EEB74`–`0x4EEB89`, `0x4EEC03`–`0x4EEC15` |
| Encumbrance | 0,0,-1,-2,-4 for category +0xBC9 | `0x4EEBA9`–`0x4EEBD7` |
| Conditions | Signed aggregate byte +0x17BB | `0x4EEB8F`–`0x4EEB9A` |
| Fatigue defense | -trunc(scorePenalty(+0x169E)/10) | `0x4EEBDD`–`0x4EEC01` |
| Defensive action | Additional +2 | `0x4EEB98`–`0x4EEBA2` |
| Reflexion | If enabled: 1+floor(skill/20) | `0x4EEB09`–`0x4EEB22` |

The defensive-action predicate is resolved from the combat context/action tests at `0x4EE9EA`–`0x4EEA16`, not inferred from a class trait. Query 0x16's full race/class eligibility remains external. `CharacterDefenseState`/`CharacterDefenseEffects` deliberately receive these resolved flags and aggregate bytes rather than guess their producers.

Native status >=18 at `0x4EEA93` skips derived contributions, while already gathered shield/other-equipment contributions remain. The input `SuppressDerivedBonuses` preserves that branch independently from incapacitation byte +0x17B5. Suppression removes skill enhancement, spell/penetration/condition bonuses and other derived components, but leaves selected body armor. Incapacitation later caps base defense at min(base,-5); it does not raise a worse negative base.

## Base, location and displayed average are different

At `0x4EEC1B`–`0x4EEC39`, base defense sums component indices 0..11 **except penetration index 6**. Incapacitation cap follows at `0x4EEC3B`–`0x4EEC54`. For each body location, `0x4EEC64`–`0x4EED00` computes:

```
locationDefense = cappedBase + penetrationAdjustment + selectedPieceArmor
```

Native location indices 0..4 map to active slots 0/4/10/5/11: Head/Torso/Legs/Hands/Feet. Body item +0x62 is signed. Nonbody active slots 1/2/3/6/7 contribute cloak/accessories/main/offhand globally; alternate weapon slots 8/9 and bag contents are excluded. These numeric slots remain evidence only. `OriginalDefenseAdapter.Prepare` takes an explicit semantic active equipment set, validates native record hashes and extracts signed armor into `PreparedCharacterArmor`. Equipment eligibility, curse/ownership and active-set selection belong to the caller.

Weights at `0x616308` are 15/40/30/10/5. `0x4EED07`–`0x4EED65` computes displayed average as trunc((weightedSum + signBias)/100), with +50 for nonnegative sum and -50 for negative sum. That display statistic is not the hit or penetration input.

The same distribution selects a character hit location with one exclusive d100 at `0x53EAEE`–`0x53EB26`. `SelectLocation` maps 0..14 Head, 15..54 Torso, 55..84 Legs, 85..94 Hands, 95..99 Feet. Monster location weights are a separate definition-dependent path.

## Context adjustment is applied twice to penetration

Character target helper `0x542EE0` returns base + K, calling `0x5468D0`. `0x542FC0` first calls that helper, adds (selected location - base), and calls `0x5468D0` **again**. With a stable resolved context snapshot:

```
hitDefense = base + K
penetrationDefense = locationDefense + 2*K
```

Do not deduplicate these calls or replace them with displayed AC. Whether the double contribution was intentional is not established; it is preserved as the original baseline.

For the character branch of `0x5468D0`, the resolved predicates can:

- Remove positive Speed and Shield contributions under the two special attack flags; negative penalties are not removed.
- Deduct four under another combat/action predicate.
- Deduct another four for the resolved vulnerable-action predicate if not incapacitated, except Throw/Lash/Shoot. Kick still receives this deduction.

Evidence: `0x5469AF`–`0x546A47` and shared mode gate `0x546981`–`0x54699F`. The booleans in `CharacterDefenseContext` are named by their arithmetic effects, not guessed condition/action names. Combat/out-of-combat action producers remain external. `Against` returns the two explicit thresholds for the existing physical-score builder.

## Scope and integration

`OriginalCharacterDefense.Build` now produces character defense from effective skills/stats, imported active gear and resolved state/effects. Tests connect it to ordinary hit/penetration scores, neutral melee and post-contact projectile penetration. It does not import native characters, populate live heroes, derive attributes/skill bonuses, resolve state/action predicates, implement monster defense or change the labelled fixture battle. Derived snapshots serialize in tests; this is not a new live character-save format. Recompute them from authoritative actor facts in the future controller.
