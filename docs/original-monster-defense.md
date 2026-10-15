# Monster defense and seven hit zones

Iteration 026. Static reconstruction of supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Commercial version unknown; no original-game execution comparison. Original records/disassembly stay private. Offsets below distinguish the definition returned by `0x4E5720` from the runtime actor returned by `0x4E5620`; these are not a claimed on-disk database layout.

## Target defense consumers

`0x542EE0` branches on target kind 3 for monsters. `0x542F11` sign-extends definition byte +0x15D, then computes `B = 10 - AC`. Runtime byte +0x220 nonzero caps B at at most -5 (`0x542F28`–`0x542F38`); it does not raise an already lower B. The result is B plus context K from `0x5468D0`.

Monster penetration branch `0x542FDF`–`0x54303F` first receives the context-bearing hit result, then sign-extends runtime +0x1E0, definition +0x16D indexed by **attack mode**, definition +0x166 indexed by **hit zone**, and definition AC +0x15D. It calls K again:

```
hitDefense = B + K
penetrationDefense = hitDefense + runtimePenetration - modeArmor - zoneArmor + AC + K
```

The AC term cancels out only when B was not reduced by the incapacitation cap. Do not simplify penetration to `10 + runtimePenetration - modeArmor - zoneArmor + 2*K` unconditionally. Do not treat monster zone/mode modifiers as additive character equipment armor: their signs and model differ. Raw defenses can be negative or exceed 100; these are inputs to score builders, not probabilities.

## Dynamic context K

Monster branch `0x5468E7`–`0x54699F`:

| Term | Operation | Evidence |
|---|---|---|
| Runtime adjustments | Signed byte +0x226 plus signed byte +0x1DF | `0x54690A`–`0x546921` |
| Fatigue | **Add** unsigned `scorePenalty(category)/10` for ordinary categories 0..4 | `0x546918`–`0x54693C` |
| Combat two-point penalty | -2 when global combat context is active, either global +0x9A4/+0x9A5 is nonzero and definition +0xD0 bit 1 is set | `0x54693E`–`0x546964` |
| Combat two-point bonus | +2 when active combat context and runtime +0x2E1 equals 1 | `0x546967`–`0x546972` |
| Combat melee vulnerability | -4 when active combat context and runtime +0x2E1 equals 4, not incapacitated, and mode is not Throw/Lash/Shoot | `0x546975`–`0x54699F` |

Kick does receive the vulnerability penalty. The first two signed adjustments and fatigue apply outside combat too. Context predicates are supplied as resolved flags; their player-facing action/condition meanings are not guessed. TwoPointPenalty/TwoPointBonus/MeleeVulnerability are calculation inputs; the native action resolver must enforce any mutual exclusion of the latter two. Both defense consumers add K, so penetration receives it twice, as in the character branch.

Fatigue category is runtime DWORD +0x242. Its producer at `0x52C0AA`–`0x52C0F0` and `0x52C1D2`–`0x52C216` computes `100 - floor(100*current/maximum)` from runtime +0x33/+0x2F, using the same 50/70/85/95 category thresholds as characters. `OriginalFatigueRules.Category` can supply it from resolved stamina facts. This also supports the semantic identification, rather than relying only on shared table usage. The **positive** defense contribution follows native instructions even though it looks counterintuitive. `0x52A7D0` returns 0/5/10/20/40 for categories 0..4 and passes other arguments through; this scoped ordinary API rejects those out-of-domain inputs rather than emulating corrupted/special raw DWORDs and unsigned division.

## Seven definition-dependent zones

`0x53EA38`–`0x53EADE` resolves the monster definition and requests one exclusive d100. It cumulatively adds **seven unsigned weights** at +0x15F..+0x165, choosing the first cumulative total strictly greater than the zero-based draw. Zero weights are skipped. Corresponding **signed zone armor** is +0x166..+0x16C; **nine signed mode modifiers** follow at +0x16D..+0x175 (Swing through Shoot).

If no weight covers the draw, native logs a diagnostic and uses zone index 3 (`0x53EA85`–`0x53EA9D`). `SelectLocation` returns `UsedFallback=true` so the caller can surface that diagnostic. It does not normalize underweight/overweight definitions: a weight above 100 can cover all draws; later weights then cannot be selected. Monster zone labels depend on definition data (+0x15E and a separate text table); the core retains Zone0..Zone6 instead of inventing anatomy or reusing character Head/Torso/etc. Label localization is outside this calculation.

## Clean boundary and scope

`OriginalMonsterDefense` consumes immutable named records for AC, seven weights, seven zone modifiers, nine mode modifiers, resolved runtime adjustments/fatigue/incapacitation and context flags. Every native-byte input is range-checked, including unselected table entries. No bags, native pointers, databases, RNG ownership or engine state enter the rules. It returns the existing `TargetDefenseThresholds` contract, usable by `OriginalPhysicalScores`, `OriginalMeleeStrike` and `OriginalProjectileStrike`.

Checks cover all 100 location draws, all 63 mode/zone combinations, zero/under/overweight distributions, native fallback, signed bytes, incapacitation before AC cancellation, positive fatigue and doubled context, combat gates, mode exceptions, serialized calculation inputs, connected melee/projectile threshold equality and rejected invalid inputs. These are reconstruction checks, not captured original-runtime observations. Derived input serialization does not add monster actors to live saves.

Native monster catalog parsing/provenance, runtime effect/action producers, monster attacking score builders, effective actor/hand resolution, special branches and spatial collision remain open. The live fixture battle is unchanged. This closes the ordinary **monster-as-target defense consumer**, not symmetric original combat or a real imported encounter.
