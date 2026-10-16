# Iteration 040 — first live native condition

Native **condition ID 16**, produced by attack-effect index **1**, now changes the tactical battle. The supplied catalog admits **79 / 595 records**, up from 76; **516 remain blocked**. Newly admitted records are **52 RFS-81**, **268 Soldier Ant**, and **383 Wasp**. These are database records, including an RPC definition, not a claim about world encounters or 79 distinct species.

## Recovered state consumers

Static evidence uses the same private EXE SHA-256 as iteration 039. No original-runtime comparison is claimed. IDs remain numeric until names are verified from primary text resources.

| Boundary | Native evidence | Connected behavior |
| --- | --- | --- |
| Effect request | `5434DF–543527` | Index 1 requests condition 16, realm 1; chance, strength, duration and binary resistance use iteration 039's rules |
| Duration merge | `523196–5231EE` for characters; `523CD0–523D44` for monsters | Longer duration replaces shorter; repeated applications do not add durations |
| Aggregate flag | `50EAE8–50EAF4`, table `50EC74`; condition 16 selects `50EBE5` | Sets aggregate byte +45 without a stat delta; character +1770+45 = +17B5, monster +1DB+45 = +220 |
| Defense cap | Existing recovered `4EEC3B–4EEC54`, `542F28–542F38` | Incapacitated base defense is capped at at most −5; an already worse defense stays worse |
| Action eligibility | Character status checks including `52FE2D`, monster checks including `531548` and continuation at `54104F–541058` | The selected condition prevents new actions and cancels queued actions/remaining swings after application |
| Timer subtraction | `5236A0–5237DA`, `524110–5241D6`; expiry `523330`, `523F40` | Finite timers subtract elapsed units and clear on expiry |

Only condition 16 is admitted. Effects producing other IDs, special action categories, projectile modes and spell actions still block a definition. The subtype immunity table scanned by `551BA0` at `6171A8` has three records (types 20/22/17), none listing condition 16. Character special protection dispatch for IDs 2..15 does not apply to ID 16; terminal-state checks are represented by the laboratory's live-target gate. This does not generalize immunity rules to other statuses.

## Live execution

A version-three monster's selected slot retains its index-1 chance. Only positive committed damage against a still-living actual target attempts the effect. Miss, failed penetration, zero damage and lethal damage do not consume its chance/duration/resistance draws. The pure request executor remains the arithmetic authority; the battle owns the external draws and commits the resulting timer immutably.

Target level and base realm resistance for a monster come from definition **+24F** and unsigned **+176..+17B**. Existing neutral runtime adjustments and score scaling remain neutral. The selected effect uses realm index 1. For party characters, **MeleeTraining** explicitly supplies and saves authored target facts: **level 10, six zero resistances**. These are training fixtures, not derived character progression, equipment resistance, an imported character or invented monster class facts. Combat facts displays them.

Redirection uses the actual target's facts and condition state. A monster can incapacitate itself after a fumble, cancel its remaining swings and skip later groups. Living incapacitated actors remain on the board and can be targeted. A newly affected hero loses a queued action in that same round. Both party and monster defense paths consume the condition flag. No synthetic `bind`/`ward` status is reused.

`NativeConditionBattle` stores separate party target facts and per-actor condition-16 timers. A derived incapacitation flag cannot be edited independently from its timer. Validation rejects missing party facts, foreign actor IDs, zero/negative/out-of-domain timers and condition data attached to unsupported encounter profiles.

## Explicit clock boundary

The tactical laboratory advances **one timer unit at the end of each completed round**, including the application round. This clock placement is **authored**. Native world time, effect scheduling intervals and fractional timing have not been connected. A request lasting 2 units therefore retains 1 after its application round, blocks the next round, then expires. A timer at 1 still blocks the current round before expiring at its end. Finished battles do not create extra rounds to tick effects.

The subtraction, maximum-duration merge, flag and combat consumers are recovered; **native real-time duration parity is not claimed**. Other statuses, damage-over-time, curing, stamina/unconsciousness producers and full native action-controller behavior remain open.

## Saves and interface

Save schema **9** introduces monster **ProfileVersion 3** and explicit condition battle data. New rosters use that profile. Profiles 1/2 retain their prior admission/action/RNG contracts and receive no fabricated condition target facts on migration. Schema-eight and older saves reject injected version-three/condition fields. Existing catalogs need no reimport.

The tactical grid shows **C16:remaining** on the affected actor. Resolving a round updates the indicator and logs application/resistance, blocked actions and expiry. Combat facts lists party fixture targets and native monster level/resistance/timers. Monster reference and mixed rosters admit the three new complete supported action sets while preserving blockers on other records.

## Verification

- Portable authored fixtures exercise actual post-damage application, queued hero cancellation, no effect after failed penetration, maximum-duration refresh, expiry/recovery, character defense-score change, self-directed monster application/cancellation, active-state save/load replay and malformed save rejection.
- Profile-two schema-eight continuation retains exact RNG and no condition overlay; existing legacy-profile-one checks remain.
- The private 595-record audit executes every admitted original record and save continuation. Each of the three newly admitted original records separately produces a real condition request against a living training target. The latter uses explicitly enlarged party fixture HP so the target survives; monster records are unchanged.
- The final private suite passes **7,855 checks including gestures**. Python's **32 tests** pass. Client builds with zero warnings/errors.
- Godot smoke renders timers on both party/monster cells, presses the real Resolve control, checks decrement and saved continuation, and still exercises mixed-roster creation and original catalog reference. Touch/mobile smoke passes.

Remaining coverage is visible rather than silently truncated: 475 records still have an unsupported enabled payload; blockers overlap with spell, projectile and special-category blockers. Native AI/world geometry, original character producers, rewards and the other condition consumers remain separate work.
