# Iteration 039 — native on-hit effect requests

This iteration reconstructs the next boundary behind the **479 records with enabled attack payloads**. It adds executable, independently tested **condition-request arithmetic**, not applied actor statuses. Playable admission remains **76 / 595 records**, with **519 blocked**. No status-bearing monster is promoted on the strength of parsing its bytes.

## Evidence and scope

Static reconstruction uses the supplied `Wiz8.exe`, SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. Original bytes/disassembly remain private. There is no original-runtime comparison.

The monster executor copies selected attack bytes **+5..+20** into the sixteen chance entries at `540DA5–540DE3`. At `540DEA–540E10` it reads definition **+24F** as the source level L and prepares **strength = L + min(L,15)**. This is not byte D1 or an estimate from the creature label. Selected byte **+1B** supplies the separate parameter used by effect index 2 (`540E19–540E30`).

`MonsterAttackInspector` exposes enabled slots, unsigned chances, native category/kind, modes, rating, damage, source level and the extra parameter directly from digest-checked raw rows, including rows blocked from combat. It does not alter the source or accept derived overlays. Existing catalog JSON needs no reimport; save schema/action RNG are unchanged.

## Chance and dispatch

`5433FA–543429` checks current target state before processing each effect, skips a zero chance without RNG, and otherwise requests an exclusive d100. The effect triggers iff **draw < raw chance**. Raw values above 100 guarantee the chance gate but still request the draw. Each nonzero entry has its own chance draw, in ascending index order; there is no single shared roll for the attack's payload.

The switch at `54347C–54348B`, table `543D74`, connects ten ordinary condition routes:

| Effect index | Condition ID | Realm index | Duration handling |
| --- | ---: | ---: | --- |
| 0 | 15 | 2 | Common |
| 1 | 16 | 1 | Common |
| 2 | 7 | 1 | Separate second duration stage |
| 3 | 9 | 5 | Common |
| 4 | 2 | 1 | Permanent sentinel |
| 6 | 17 | 3 | Common |
| 7 | 12 | 0 | Common |
| 8 | 6 | 4 | Common |
| 14 | 4 | 2 | Common |
| 15 | 11 | 4 | Common |

IDs remain native numeric boundary values; localized status names are not guessed. Indices **5,9,10,11,12,13** require special eligibility, death or resource policies and reject ordinary-condition resolution. The ordinary-route list does not mean those ten conditions already affect a live actor.

## Duration and RNG ordering

`54342F–54347C` computes **B = floor(strength/3)+1** and executes a common duration stage **before dispatch**, even for an index whose branch subsequently ignores that duration. `5519C0` with the monster's neutral duration fields always requests one exclusive d4. Only zero-based draw **1** yields 2 and avoids fallback dice. Other draws cause `517950` to initialize **1 + B d2**, then `551A20` rolls it and the caller adds one. The common result is therefore **2 + B d2**, clamped to **1..12**.

Index 2 (`54352C–543583`) repeats the d4 gate after that common stage. Gate 1 again gives 2; otherwise its result is **parameter + 1 + (3B) d2**, with no common 12 cap. The dice count is stored as a **byte** by `517950`: high modded levels can wrap 273 dice to 17. Tests cover the full byte-level domain, although supplied original records do not need that extreme.

Index 4 passes native **9999** as a permanent-duration sentinel. The core represents this with a null request duration, not a timer of 9999 invented rounds. Its earlier common-stage draws still count.

`ResolveCondition` executes one selected ordinary condition against explicit caller-supplied draws. It returns a trace of each requested bound/stage and one of NotTriggered / Immune / Resisted / Requested. Chance failure consumes no duration or resistance draws. Caller-resolved immunity is checked after duration preparation and consumes no resistance draw. Missing, extra or out-of-range draws reject the entire calculation. The helper owns no mutable RNG or actor state. A complete native sixteen-entry controller, target-state reevaluation and actual status commits remain separate.

## Binary condition resistance

`551BA0` performs target eligibility/immunity checks, then calls `5520D0`. Its ordinary arithmetic is:

**raw = 3 × (target level − effect strength) + effective realm resistance + condition offset**.

Offsets for condition IDs 0..19, recovered at `6172A0`, are **0,1,2,3,4,4,5,6,7,8,9,10,12,14,16,20,18,22,24,22**. Target-specific score scaling follows (`55CC00` for characters / `55CCB0` for monsters), then the threshold is clamped to **5..95**. An exclusive d100 **below** the threshold resists; equality passes. This is a binary condition gate, not the two-roll amount reduction used by `552250` and existing spell damage.

The rules accept already resolved effective resistance, target level, scale and immunity. They do not infer immunity from a name, substitute the source's level for the target's, or derive original character progression/equipment facts from the training fixture. Native subtype immunity tables, additional character protections, existing target conditions, stacking/replacement, expiry, periodic damage, action interruption and defense/stat consumers remain open.

## Interface and validation

**Menu → Monster reference** now shows source level/strength and each enabled slot's effect chances/routes, including blocked records. Long details scroll independently of the list. The Fight button remains disabled for an unsupported row.

Portable tests cover all 256 raw chance values; all byte-sized definition levels and four duration gates; condition routes; permanent/secondary requests; the secondary byte overflow; all condition offsets, three scales and every d100 at clamp/equality boundaries; complete selected-effect stage ordering; immunity; malformed draw plans; and checked overflow. Authored records exercise decoding without publishing game data.

Private checks inspect all **595** original rows and all enabled payloads, while the existing admitted-record live-round/save-continuation suite still runs. Godot smoke selects an actual payload-bearing row, checks the rendered effect details and disabled Fight control, then exercises the existing mixed roster and combat/save UI. These conditional original-data checks are not claimed as public CI coverage.

Next integration boundary: recover and connect native status lifecycle/eligibility and its action/stat consumers, then admit only records whose **entire** enabled action set is implemented. This iteration alone adds no new live monster profiles.
