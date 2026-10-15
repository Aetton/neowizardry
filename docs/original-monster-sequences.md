# Monster physical candidates and group/swing budgets

Iteration 028. Static reconstruction of the supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Local research environment was reset between turns; repository and original EXE were recovered, and this hash was reverified before continuing. Commercial version unknown; no original-game runtime comparison. Proprietary files/disassembly remain outside git.

## Candidate identity includes the target

Monster definitions have three attack records, stride 0x22, at runtime definition +0xE7. Slot index is 0..2; enabled byte is record +0. `0x51A730` selects the corresponding category at record +3 for ordinary action 0. Selected slot comes from combat context +0x11, while selected mode/action data is stored separately on the actor. This is runtime evidence, not a complete on-disk monster database format.

Physical candidate generation inside `0x531CE0` loops eligible slots and targets. For each qualifying target, `0x532190`–`0x5321BC` (character target) and `0x532262`–`0x532291` (monster target) enumerate mode ordinals **0..8**. A mode is appended only when the WORD at definition +0xFC + slot*0x22 contains that bit. Each candidate carries the selected mode, attack slot and target. Slot iteration advances by 0x22 at `0x5322A8`–`0x5322BA`. Eligibility includes prior skill/status/range/relationship/target branches; those are not replaced with guessed predicates here.

`0x5323F0` calls the candidate generator, obtains list length, returns without a choice draw for an empty list, otherwise calls exclusive `rand(listLength)` at `0x532426`–`0x532434`, retrieves that index, and copies mode/slot/target from the selected entry (`0x532445`–`0x5324A6`). This selects a **list entry**, not a distinct attack, distinct mode or distinct target. Do not deduplicate entries or choose each target first with equal probability: more eligible combinations can change its probability.

`OriginalMonsterAttackSequence.Modes` expands exactly the low nine bits in ascending order. Higher WORD bits are ignored; a mask with no supported bits yields no modes. This is **not** the separate nine-draw cyclic sampler found in the character/item mode helpers `0x542CA0`/`0x542DB0`. Monster candidate expansion makes no mode RNG request.

`SelectChoice` takes an explicitly supplied, already resolved **all-physical** candidate list of `(AttackSlot, Mode, TargetId)` and an external exclusive draw. Empty input requires no draw. It preserves order and multiplicity. It does not generate the original complete list, resolve target eligibility or choose between physical attacks and spells/special actions. Filtering physical candidates out of a mixed original AI list and then sampling would change its probabilities; a future full AI selector must handle the entire native candidate domain before projecting a physical selection into this core.

## Two independent budgets

| Fact | Native source | Consumer |
|---|---|---|
| Number of attack groups | Unsigned definition byte +0xE5 | `0x53247C`–`0x532495` initializes context total/remaining +0x05/+0x09 when the outer first-allocation flag is set |
| Maximum swings per group | Unsigned definition byte +0xE6 | `0x53FFA4`–`0x53FFB7` calls exclusive rand(maximum), adds 1 and writes context +0x0D |

Group and swing counts do not use the character attack-budget/BAR formulas. Definition gates in `0x53D4B0` reject unavailable attack data and inspect both count bytes before later execution. The scoped budget API requires nonzero swing maximum for a positive group budget instead of invoking a zero-range RNG. Zero groups allocate nothing.

At group entry `0x53FEA0`, context +0x09 must be positive. `0x53FF3C`–`0x53FF45` **decrements remaining groups before** the call to `0x53D4B0` and target checks. If the group is rejected, it does not reach the swing draw. Therefore `BeginGroup(state, eligible:false, swingDraw:null)` returns a consumed group and `Started=false`, with no swing allocation. It does not silently refund the group, retry it or stop all future groups. The outer controller owns that decision.

`BeginGroup` with resolved eligibility true requests one externally supplied draw in `[0,MaximumSwings)`, yielding `draw+1`. It requires no unfinished current swings. It does not sample candidate/target policy itself; later native target reassessment can change actor mode/slot/target without reinitializing the outer group budget.

## Swing completion and interruption boundary

Ordinary monster execution converges at `0x541020`. `0x541045` decrements context +0x0D after the resolved attack attempt, independently of hit/miss/penetration outcome. At `0x541048`–`0x541058`, remaining swings are cleared for the observed conjunction of zero runtime +0x2B and runtime +0x107 >=15; this is not equivalent to indiscriminately stopping any zero-HP actor.

Further continuation branches inspect current swing count, definition +0x247, current action category, target identity and availability, and can call `0x5323F0` again with first-allocation disabled (`0x54106A`–`0x5410A7`). Target/action failure can clear remaining swings at `0x541147`–`0x54114B`; matching/valid target gates follow at `0x541156`–`0x5411A9`. Those state/retarget predicates remain external.

`CompleteSwing` consumes exactly one active swing and increments the derived completion count. Optional `cancelRemainingSwings` is the **already resolved** interruption result; it discards only the unfinished current swings and preserves future group budget. It does not derive actor status from prototype HP or infer a new target. The caller must run this only after an attempted swing actually completes, and own one authoritative state transition. It does not provide a global replay ledger or a pending projectile lifecycle; use the existing projectile series/delivery boundary when appropriate.

`MonsterAttackBudget` is an immutable derived calculation snapshot with explicit total/max/remaining/attempted/completed counters. Validation enforces byte-sized native input counts, conserved group budget and feasible completed/remaining swings. Invalid input throws before publishing a transition. Roundtrip tests resume calculation snapshots; no live game save schema or actor fields are introduced.

## Penalty counter is still separate

The monster hit repetition input remains context DWORD +0x141, as documented in iteration 027. Context initialization resets it at `0x4E86BF`. This iteration identifies group +0x09 and swing +0x0D lifecycle, **not** the producer/increment policy of +0x141. Never feed `SwingsCompleted` or `GroupsAttempted` into `ActionPenaltyCounter` merely because the names suggest a connection.

## Validation and open work

60 additional portable checks cover all 65,536 native WORD masks as one exhaustive property, candidate order/multiplicity/target identity, exact RNG absence/ownership, group consumption before rejection, minimum/maximum swing allocation, completion and cancellation, input immutability, snapshot continuation, invalid states and a full 255*255 budget yielding 65,025 completed swings. A chosen mode composes with character torso defense and the monster score builder, with exact penetration equality checks. These are static-reconstruction tests, not captured original-runtime samples.

Full mixed-action candidate generation/eligibility, actor/hand/target facts, interruption and retarget policy, repetition-counter producer, monster damage-state producers, original magic effects and a representative live encounter remain open. The labelled fixture battle is unchanged.
