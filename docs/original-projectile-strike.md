# Original projectile contact, interception and penetration

Iteration 023. Static reconstruction against the supplied executable, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Commercial version unknown; no original runtime comparison. Addresses describe this binary. No proprietary records or disassembly are published.

## Delivery status producer

Projectile preparation `0x544B60` invokes creator `0x544630`. Creation at `0x544A99` calls `0x4A2D30`; source and target data are copied at `0x544ACA`–`0x544AEC`. The normal pending path resets global status +0x8C4 to zero at `0x544B10`.

Actor collision callback `0x4A4720` establishes an actual actor target and reaches `0x4A48AC`. For the ordinary projectile metadata path (metadata byte +0x154 is zero), it starts status BL=1 at `0x4A48CC`. It reads a resolved interception byte from character +0x17B9 (`0x4A48EF`) or monster +0x224 (`0x4A4910`). With a positive byte it consumes a fresh exclusive draw bounded by 100, adds one and changes BL to 2 when roll <= byte (`0x4A4921`–`0x4A493A`). Zero consumes no interception draw. Bytes above 100 still consume a draw and always pass this comparison. Metadata +0x154 nonzero bypasses this ordinary interception test and is outside the new resolver's scope.

`0x4A494C` or `0x4A49A1` writes the status to global +0x8C4. The latter also copies the actual collision target into +0x8C5 (`0x4A49A7`–`0x4A49B7`). Therefore the collided actor need not be the originally selected target. Its interception/defense/damage facts must be resolved by the caller. The producer of the interception byte is not reconstructed here; do not call it a universal shield/block statistic or derive it from an invented skill formula.

There is also a direct-contact path at `0x5446AD` that writes status 1 without constructing a world projectile, subject to earlier target checks. Its eligibility is not implemented by the new pure resolver.

## Character attack consumer

| Native state/branch | Observed behavior |
|---|---|
| `0x53E4E7`–`0x53E4EA` | Range/type >=2 branches into projectile completion, bypassing the melee hit/fumble setup. |
| `0x53E7C3`–`0x53E7F1` | Status 1 = actor contact; 2 = interception; other completion status = miss. A zero while a projectile is still pending is not an instruction to complete it as a miss. |
| `0x53E7F3`–`0x53E805` | Contact sets local +0x11=false (miss), +0x12=true (automatic hit comparison), +0x13=false (ordinary penetration). Two pushes temporarily shift these byte addresses by eight. |
| `0x53E805`–`0x53E85B` | Special trait/hand/weapon branch can set local +0x13=true. Its full actor eligibility is excluded from the new neutral path. |
| `0x53E860`–`0x53E86C` | Contact consumes a new d100 and adds one. Miss/interception branches skip this draw. |
| `0x53EB4B`–`0x53EB68` | Local +0x12 bypasses the ordinary hit comparison. The draw after projectile contact is not another hit roll. |
| `0x53ECE2`–`0x53ED22` | Resolve the actual target's penetration threshold with `0x541EC0`, using the resolved hand/source. |
| `0x53ED30`–`0x53ED4D` | Local +0x13 bypasses penetration only for the special branch. Otherwise the fresh one-based draw must be <= penetration threshold. The diagnostic call temporarily shifts +0x13 to +0x2F. |
| `0x53ED4D` onward | Passed penetration enters damage; failed penetration skips it. Common completion eventually reaches resource spending already traced in 021. |

Ordinary projectile contact **does not** use melee's automatic penetration on natural 1–5 or natural miss on 96–100. A post-contact roll of 1 can fail against threshold zero; a roll of 100 can penetrate threshold 100. The neutral path still needs a penetration draw at these deterministic thresholds, preserving original draw consumption. This does not mean that an aimed projectile is guaranteed to collide with an actor.

## Clean calculation and resource composition

`OriginalProjectileStrike.ActorContact` implements the ordinary resolved interception-byte comparison after an externally established actor collision. `Resolve` accepts only Shoot/Throw and a typed delivery result. Miss/Intercepted require no penetration/damage draws. Contact requires a fresh d100; failure requires no damage dice; success rolls the ordinary physical damage expression once and applies the recovered hand/effect/absorption arithmetic. The special automatic-penetration branch, repetition eligibility and later conditions/effects are outside this scope.

`OriginalProjectileAttack.Complete` composes this neutral calculation with one `ProjectileSeries.Complete`. Every valid resolved outcome spends one selected quantity/resource unit, including miss, interception and failed penetration. Contact maps to the series' delivery Hit even if armor prevents damage; Hit there describes contact, not HP loss. A penetrating but fully absorbed strike can also have zero damage. Errors, stale/foreign/duplicate completions and invalid draw plans do not partially mutate inventory. As before, the caller commits current immutable state once; there is no global replay/event ledger.

The caller owns actual-target identity and scores, original actor/hand policies, correct prepared weapon/ammunition snapshot, additive launcher/hand damage percentage, collision, RNG and HP application. Reload retains the same ordinal/allocation, and the next projectile must use the replacement source's damage/accuracy facts. Depleted and pending states continue deterministically after serialization.

## Geometry and remaining gates

`0x5454C0` perturbs an aim vector, calls angle/d100 RNG and adjusts a score using distance before world projectile creation. This aim threshold is not the final chance of damaging a target: collision, interception and penetration are later stages. The new implementation deliberately does not substitute a simple d100 hit gate for that geometry. Collision volumes, movement, spread, callback eligibility, interception-byte derivation, special automatic penetration, target-location/condition policy and full native end-of-attack reload ordering remain open.

The Godot training battle remains a labelled fixture. This slice supplies connected post-delivery math and resource transitions, not a complete original ranged encounter or live original character model.
