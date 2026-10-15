# Original physical attack budget — first verified subset

Decision, 2026-10-15: use original combat/magic formulas as the remake baseline while implementing them as independent typed rules. Keep deliberate redesigns explicit. This is mathematical reconstruction, not preservation of original global state, offsets or memory layout.

Evidence: read-only disassembly of supplied `Wiz8.exe`, SHA256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Executable commercial version is not established. These are statically verified local paths; no original-game run or original save fixture was executed. Relevant code: `0x4EE130` and `0x4EE220`–`0x4EE656`. Profession/skill names agree with the pinned editor enums already used by the import pipeline.

## Scope and arithmetic

`Rules/OriginalCombatRules.cs` covers a usable single weapon with the other hand free, supplied effective attributes/skills, resolved weapon initiative and known load category. It does not resolve dual wielding, shields, hand availability, conditions, special weapon overrides, actual swing randomness, hit, penetration or damage. The original character path forces one maximum swing for icon `0x90` at `0x4EE642`; in the supplied table that is Rocket Launcher. That override is outside this single-weapon arithmetic API.

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

Here `trunc` means signed division toward zero. Attack arithmetic and inclusive thresholds: `0x4EE58E`–`0x4EE5EC`. Swing arithmetic/thresholds: `0x4EE5EF`–`0x4EE638`. The value is a maximum budget, not a promise of that many executed swings. Bonus swings and queue consumption still require tracing.

## Resolved discrepancy

The previously retained [guide model](https://steamcommunity.com/sharedfiles/filedetails/?id=899973393), section 5.4, uses decimal approximations and is still preserved in `LegacyResearch` as a source model. The binary arithmetic supersedes it for this covered branch. With DEX=SPD=125, C=W=100 and Rogue level 25, `S=100`, `E=75`, `A=100`: three attacks. Level 24 gives `A=99`: two. The old decimal expression's 599.9999 at level 25 is not an original rounding rule.

The character branch explicitly caps the base count at three; do not infer a fourth base attack by extrapolating the guide score to 900. Extra swings, special weapons and extra actions are separate mechanisms.

## Integration gate

These functions are not wired to the fixture sentinel battle. Real effective attributes, class history, hand/skill selection, initiative, hit/penetration, conditions and action consumption must be modeled together before switching the playable combat rules. The current prototype heroes lack those original statistics; assigning invented defaults would hide a balance change. Next static anchors: `Combat Attack.cpp` logging sites `0x53E676` (hit) and `0x53ED26` (penetration), then their builders and monster-side counterparts.
