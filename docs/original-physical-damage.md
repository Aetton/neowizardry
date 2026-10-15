# Original physical damage and neutral melee composition

Evidence: read-only static analysis of the same supplied executable identified in [physical formulas](original-combat-formulas.md). No original game was executed; commercial version remains unestablished. The runtime offsets here are evidence only. Raw records, binary/disassembly and engine globals stay outside runtime rules and public git.

## Primary-hand attribute contributions

`OriginalPhysicalDamage.PrimaryHand` receives effective STR/DEX/SEN (0–125), the combined item ToHit contribution, explicit Strength policy, load category and Modern Weapon flag. It excludes dual-wield penalties and skill/effect buffs. A separate prepared-launcher bonus is added to its damage percentage by the caller.

For Full strength, divisor `d=1`; Half uses `d=2`; None suppresses both Strength terms:

```
strengthHit = trunc((STR-50)/(10*d))
strengthPercent = STR>=50 ? trunc(2*(STR-50)/d) : trunc((STR-50)/d)
dexterityHit = trunc((DEX-50)/10)
sensesHit = SEN>70 ? floor((SEN-70)/10) : SEN<30 ? -floor((30-SEN)/10) : 0
handToHit = combinedItemToHit + strengthHit + dexterityHit + sensesHit + handLoad
```

Hand-load values are `0,-1,-2,-4,-8`; Modern Weapon halves them toward zero. This is separate from the load contributions in attack/swing budgets and hit/penetration score builders. Proof: item ToHit and launcher setup `0x4EE656`–`0x4EE6C9`, Strength `0x4EE796`–`0x4EE7FB`, DEX/SEN `0x4EE7FE`–`0x4EE875`, load `0x4EE875`–`0x4EE8E2`. The offhand Strength-hit denominator and other offhand penalties differ and are not covered here. Ranged/close skill buffs at `0x4EE8E5`–`0x4EE92E` are not fabricated as zero for general gameplay; callers must resolve them before live integration.

## Import-boundary weapon preparation

`OriginalWeaponAdapter` converts the legacy conventions into `PreparedPhysicalWeapon`: a damage expression, combined item ToHit, launcher damage percentage, semantic Strength policy and Modern Weapon flag. The math module does not inspect original icon identifiers or item-table offsets.

The native code uses icon/category codes, not item TypeCode alone, for launcher identification. `0x51CCE0` with table `0x51CD28` identifies codes 11,12,13,37,131 as separate-ammunition weapons. In the supplied data these cover bows, crossbows, slings and Omniguns. Primary attack damage selects the resolved ammo expression at `0x5423EE`–`0x5424E6`; ordinary direct weapon damage selects the weapon expression. The launcher contributes `10 * signedConstant` as a percentage at `0x4EE6B4`–`0x4EE6C9`. Item ToHit can include the ammo contribution when the opposite hand is category 3 (`0x4EE681`–`0x4EE69A`); ammo category mapping, including the Omnigun thrown-ammo exception, is at `0x520234`–`0x520285`.

Strength policy comes from `0x4EE75A`–`0x4EE796`: if the supported mode mask has a bit outside Throw/Lash/Shoot (`mask & ~0x190`), use Full. Otherwise codes 104,110,114,131,144 jump to `0x4EE7FE` and **skip Strength entirely**; the remaining branch jumps to `0x4EE791` and selects Half. The two exact table targets are `0x4EE7FE` and `0x4EE791`, not two different divisors. This distinction matters for Omnigun. It is original baseline behavior, not the deferred Gadgeteer rework.

Preparation requires explicit ammo for launchers and rejects ammo on direct weapons. Iteration 022 adds the recovered definition compatibility predicate, including Omnigun stages. Preparation rejects incompatible separate ammo. It does not choose an ammo instance, check quantity/charges, consume it, reload, choose attack mode or enforce actor eligibility. Separate [reserve/refill helpers](original-ammunition-reload.md) provide scoped resource continuation; a complete attack controller remains external. Intrinsic-shot weapons such as the inspected rocket code are not automatically treated as separate-ammo launchers merely because they shoot.

## Dice and damage scaling

The definition at item +0x4A is a signed WORD constant followed by unsigned byte dice count and sides. `0x517970`–`0x51799F` sign-extends the constant and adds one exclusive random result +1 per die. Import now uses `<h`, replacing `<H`; catalogs retain integer JSON and schemas 2/3, but old exports containing unsigned-only values above 32767 must be regenerated. All 819 supplied records keep identical values (none have negative signed constants); synthetic imports cover both signed bounds.

The common character damage builder at `0x54253D`–`0x542561` invokes that complete expression once per **resolved damage repetition**:

```
rolled = sum(constant + sum(draw+1 for each die) for each repetition)
scaled = max(1, trunc((rolled * (100 + handDamagePercent + effectDamagePercent) + 50)/100))
damage = max(0, trunc((scaled * (100 - effectiveAbsorptionPercent) + 50)/100))
```

This is re-rolling the expression, including its constant, rather than multiplying one random result. Independent `2d2` can total 3; twice a single `d2` cannot. A repeated damage expression is also distinct from allocating several swings/projectiles: each landed swing can have its own damage repetitions.

Proof: scaling and one-point minimum `0x54261F`–`0x542651`; target percentage and zero floor `0x542662`–`0x5426D7`. Hand percentage is runtime hand +0x29, effect percentage is a signed byte. Monster and character targets derive effective absorption differently; `Roll` takes an already-resolved percentage and does not implement those target state lookups. Negative absorption is not silently clamped into resistance. Both stages add 50 and truncate toward zero; they are separate rounding operations. The first minimum is before absorption, so full absorption can still produce zero. Checked arithmetic rejects overflow rather than emulating 32-bit wrapping.

The multiplier-building branches `0x542225`–`0x54238E` include target state, Berserk, special flags/skills and weapon-versus-target categories. `Roll` takes their resolved repetition count; it does not invent eligibility. The neutral melee wrapper below uses only the ordinary multiplier 1 and neutral Berserk multiplier 2. Critical effects, target-specific additional changes and post-damage state changes are not covered. The original also consumes a feedback-only RNG draw for some repeated hits (`0x5425E3`–`0x54261C`); this math layer does not preserve the original RNG sequence or couple sound feedback to combat randomness.

## Connected ordinary melee calculation

`OriginalMeleeStrike.Resolve` composes the ordinary score builder and damage calculation for weapon modes Swing/Thrust/Bash/Lash/Berserk, neutral context and externally resolved defenses/absorption. It accepts a single zero-based contact d100 and the subsequent damage draws. It has no GameState, HP mutation, inventory or save dependency.

- Require untargeted hit score >=100: the native cubic fumble term is zero at `0x53E57E`–`0x53E58B`. Lower scores require unresolved fumble/redirection policy and are rejected explicitly.
- One-based 1–5 bypass ordinary contact and penetration comparisons; 96–100 miss. Flags: `0x53E50F`–`0x53E530`; natural miss branch `0x53E880`–`0x53E8C3`.
- Otherwise contact succeeds at `roll<=hitScore` (`0x53EB4B`–`0x53EB68`), then penetration uses that **same** roll with an inclusive comparison (`0x53ED37`–`0x53ED4D`). No second penetration roll is invented.
- A miss or failed penetration requests no damage draws; supplying unused draws is rejected. A landed neutral Berserk hit requests two full damage expressions; other covered neutral modes request one.

The test scenario connects class history and attributes through BAR, hand bonuses, target scores, shared d100 and damage. It is not a whole combat round or live original battle. Projectile contact/delivery, redirected attacks, low-score fumbles, target/equipment/actor validation, ammo/throw depletion, interruptions, HP effects, special conditions and monster-side attacks remain explicit gates. A critical item effect is not implied by a high physical damage roll.

## Playable fixture safeguard

The experimental battle continues to use its labelled fixture rules. Its imported-weapon damage is now floored at one after the raw dice roll, preventing newly supported negative signed constants from healing the enemy. Unarmed fixture Power and original-stat/save migration behavior are unchanged. This safeguard does not silently replace the playable battle with the new neutral melee calculation.
