# Original projectile aim perturbation

Iteration 024. Static reconstruction of `0x5454C0` in the supplied executable, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Commercial version unknown. No original-game execution comparison; proprietary executable bytes and disassembly remain private.

This helper changes a target point before projectile creation. It does **not** determine actor collision, interception, penetration or damage. Do not label its adjusted score or d100 branch as the final hit probability.

## Caller and resolved inputs

`0x544A5A` checks the resolved score against 9999. That sentinel bypasses this helper completely. Otherwise `0x544A65`–`0x544A75` supplies source/target vectors, score and a boolean selecting the alternate profile. The creator subsequently launches the projectile toward the modified point at `0x544A99`.

The caller sets the profile boolean from a nonzero character DWORD +0xA31 (`0x544857`–`0x54487B`) or monster DWORD +0x87 (`0x54499E`–`0x5449AB`). Their semantic producer is not yet reconstructed. The implementation deliberately calls the profiles Standard/Alternate; it does not infer blindness, weapon skill, throwing vs shooting or a class trait from these offsets.

Positions and units are already resolved by the caller. Character/monster launch points, target body offsets, direct-contact eligibility, moving-target prediction and conversion to Godot coordinates remain separate. Original distance units are not assumed to be meters.

## Draw order and distance

The helper computes `delta = target - source`, constructs perpendicular vectors and consumes an exclusive angle draw 0..359 at `0x545587`. Conversion uses PI at `0x5EE7A0` and 1/180 at `0x5EC300`. The distance check at `0x54567B`–`0x54569E` returns without changing the target if distance <1. That path **has already consumed the angle** but consumes no d100. Exactly one unit enters the full spread path.

For full spread, the second exclusive draw 0..99 at `0x54577D` becomes one-based r=1..100. It is not reused as the later penetration or interception draw.

## Scalar formula

Let d be distance and S the supplied attack score. Native constants and operations:

| Term | Formula | Evidence |
|---|---|---|
| Distance penalty p | trunc(d * 0.002) | `0x5456F8`–`0x545700`, constant `0x5EBF50`; conversion thunk imports MSVCRT `_ftol` |
| Adjusted score s | trunc(((100-p) * S) / 100) | `0x545705`–`0x545725` |
| Standard slopes a, b | 0.08, 0.2 | `0x5456DA`–`0x5456EF`, 0.08 at `0x5EE7D8` |
| Alternate slopes a, b | min(500/d, 1.75), 1.75 | `0x5456AB`–`0x5456D8`, constants `0x5EC150`/`0x5EE7E0` |
| Near deviation n | a*d | `0x5456F0`–`0x5456F4` |
| Far deviation f, s>=300 | n | `0x54572A`–`0x545746` |
| f, 0<s<300 | n + (300-s)*(1/300)*(b-a)*d | `0x545748`–`0x545769`; constants `0x5EE7D0`/`0x5EE7C8` |
| f, s<=0 | b*d | `0x54576D`–`0x545775` |

The signed integer score can become negative beyond p=100; the native formula does not clamp it. Clean code uses checked multiplication/conversion rather than copying native integer wrap or invalid floating conversion.

The signed deviation multiplier q is chosen at `0x545785`–`0x5457E6`:

- If r <= min(95,s): q = (r/s)*n.
- Otherwise if s>0: q = n + ((r-s)/r)*(f-n). The denominator is **r**, not 100-s.
- Otherwise set t=r+s. If t<=0: q=f. Else q=n+(100-t)*0.01*(f-n).

The outer branch is not an automatic miss. q can be negative for the alternate profile at some high-score/distance/draw combinations; preserve its sign rather than clamp, take its absolute value or convert it to a miss. For example, d=10000, S=250 gives s=200, n=500, f=6166.666..., r=96 gives q=-5638.888.... This reverses the offset direction. Whether the projectile collides with anything is a later spatial question.

## Perpendicular direction

Writing delta=(x,y,z), the helper's stored products establish:

- A=(-x*y, x*x+z*z, -z*y).
- B=A cross delta.
- U=unit(A)*cos(angle) + unit(B)*sin(angle).
- perturbed target = source + delta + unit(U)*q.

Evidence: product stores `0x545511`–`0x545583`, sine normalization `0x5455AD`–`0x5455FA`, cosine normalization `0x545600`–`0x54564D`, addition `0x545657`–`0x545677`, final normalization and coordinate writes `0x5457EA`–`0x545888`. Zero vector normalization is skipped. For an exact Y-axis delta, both A and B are zero; the point stays unchanged while both draws are still consumed. The baseline implementation preserves this observed degeneracy instead of inventing a different basis.

For a positive X-axis target, angle 0 offsets toward positive Y, and angle 90 toward negative Z. This orientation matters for deterministic draw-driven replay.

## Implementation and limits

`OriginalProjectileAim` provides the scalar breakdown and perturbed point using typed coordinates and caller-supplied draws. It implements the reconstructed formulas in **double precision** with finite/overflow guards. The executable uses x87 intermediates, explicit float stores and x87 trigonometry. Bit-exact x87 parity, original collision equivalence and branch behavior arbitrarily close to floating boundaries are not claimed; they need original runtime fixtures or a separate numerical equivalence study. Tests use analytic axis examples and geometric invariants with tolerances, not a purported original executable oracle.

`OriginalProjectileAttack.Launch` composes ordinary aim with a pending resource series, without spending ammo. `AimedProjectileLaunch` keeps both series and aim snapshot, so the caller can serialize the complete flight and resume later without rerolling. The ordinary wrapper explicitly rejects score 9999; its separate no-spread delivery path is not silently perturbed. Direct-contact/no-spread paths can still use the existing resource lifecycle separately.

The caller must resolve physical collision from the aim point; it can then pass the actual actor's post-delivery facts into `OriginalProjectileAttack.Complete`. Tests demonstrate launch/save → externally supplied contact → penetration/damage/resource completion, and a later miss. No new collision implementation, automatic target hit or live Godot actor model is implied. The training battle remains a labelled fixture.
