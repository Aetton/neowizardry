# Iteration 038 — structural monster coverage and general physical profiles

The request to add the remaining monsters now runs through a complete capability audit, not a two-species allowlist. On the supplied private database: **76 / 595 records** can execute the current neutral physical profile; **519 remain blocked** by missing mechanics. These are database records, including NPC/debug records and duplicate variants, not 76 distinct species. This iteration does **not** finish all monsters.

## Admission and coverage

`MonsterCombat.Capability` checks the raw record digest, HP expression, physical budgets and **every enabled attack slot**. A new profile is admitted only when all enabled actions fit the implemented path. No unsupported slot or effect is silently removed to make a monster appear ready.

| Blocker on the supplied database | Records |
| --- | ---: |
| Enabled attack effect payload | 479 |
| Nonzero spell action probability | 97 |
| Projectile mode in an enabled slot | 169 |
| Special category 1 | 117 |
| Special category 2 | 104 |
| Special category 3 | 67 |
| No physical attack | 5 |
| No physical budget | 1 |

Counts overlap; their sum is not the blocked total. Payload bytes are retained as unsupported native data, not assigned speculative condition names. Category 1 remains conservatively blocked even though it shares part of the native contact branch; its complete delivery/action policy has not been connected. Magic, native reactions/status producers, XP/loot and world AI remain open.

**Menu → Monster reference** shows READY/BLOCKED for all records, a ready-only filter, exact blocker codes for the selected row, and a button to fight an admitted row. **Menu → New monster battle** lists admitted records. **Menu → Mixed monster battle** chooses 1–8 individual definitions, preserving a separate HP roll and brain for each actor. Examples newly admitted include ordinary bats, roaches, rats, crabs, fish, mites, some elementals and humanoid records. Movement, sight and target selection remain authored grid policies.

The wider audit also finds that **Shrieker Bat has a spell-action field**, so it is no longer advertised as ready for a new general profile. Its old explicitly physical-only saved encounter remains loadable under the legacy contract. The previous two-row demonstration did not implement that spell action; this is now visible instead of implied by species admission.

## Physical paths

Version-two records compile all enabled slots and low-nine-bit attack modes. Supported modes are Swing, Thrust, Bash, Berserk, Punch, Kick and Lash; Throw/Shoot require projectile delivery and block admission. For the authored AI-selected adjacent target, the executor enumerates slot/mode entries in slot then mode order and selects one entry with externally owned exclusive RNG, including a one-entry list. It preserves the native entry multiplicity rather than selecting each slot with equal probability. A mode/slot is retained for that attack group. Native mixed-action AI eligibility and mid-group target/action reassessment are not claimed.

Each selected slot supplies its rating, native kind/fatigue classification and signed-constant damage expression. Neutral Berserk rolls the entire expression twice; other supported modes roll it once. The physical absorption byte at definition CF is applied to party hits and redirected monster hits on a monster, with existing rounding and zero floor. There is no fabricated character weapon or class history.

A larger group budget exposed a former character-only limit in continuation scheduling. Monster continuation now accepts the native byte-sized group domain through a separate API. A 255-group boundary test proves monotonic, finite consumption, including zero spacing at time 100. Character hand-budget limits remain unchanged. This is still the laboratory timeline policy, not a complete native action controller.

## Fumble arithmetic and redirection

Static evidence is the same supplied EXE hash documented in iteration 037. At `5403CB–54045D`, with neutral untargeted score S and already resolved alternate-candidate count N:

- If S >= 100, base = 0; otherwise base = trunc((100-S)^3 * 0.00001 + 0.5).
- Threshold = clamp(floor(N * base / 10), 0, 100).
- Natural original 1–5 bypasses redirection. Otherwise redirect if the original one-based d100 exceeds `100-threshold`.
- A redirect selects an alternate list entry with exclusive RNG and requests a fresh contact roll.

Constants are decoded from `5EE7A8` (1e-5), `5EE7B0` (3.0) and `5EBE80` (0.5). Integer implementation is checked against the decoded floating formula for every neutral rating/mode score and all laboratory candidate counts.

Importantly, `54052E–540538` **forces the redirected hit**, clears natural miss and disables automatic penetration. `54092D–540949` skips the ordinary hit comparison, while `540C95–540CA2` still checks penetration against the fresh roll. Thus a redirected fresh natural one does not bypass armor, and a fresh 96–100 does not automatically miss. Separate tests cover both cases.

`544010` excludes the intended target from alternatives, includes the attacking monster itself, and builds character/monster candidates through native eligibility helpers. The laboratory supplies living adjacent visible alternate heroes and monsters, plus the attacker; this is an **authored candidate eligibility policy**, not recovery of those native spatial/status helpers. Original target-zone/delivery RNG ordering is not claimed. Redirection is per attempt; the group's intended target remains until its normal cancellation/AI group boundary. Actual-target HP, native armor/absorption and self-death are committed against the current immutable state. There is no recursive second fumble.

## Saves

Game schema **8** introduces record `ProfileVersion=2`. It stores raw records and digests, not editable derived overlays. New general profiles accept structurally supported user catalogs/mods; the digest is an integrity check, not proof of original content or a cryptographic signature. Full catalog identity is validated on creation.

Old record snapshots default to version 1 and retain their two-record/fixture admission, fixed-Thrust execution and RNG contract. Schema-7 migration creates no new attacks or capability facts. Old schemas reject injected version-two profiles. Existing private catalog JSON does not need reimport; no private game records are published.

## Verification

`GeneralMonsterChecks` audits all 595 private records: every blocked row rejects compilation; every admitted row retains all enabled supported slots/modes, executes a real round, and continues exactly after save/load. A mixed original roster runs repeated rounds. These private checks are conditional on `--monsters`, not claimed as public CI coverage.

Portable authored fixtures cover all seven modes in live execution, multi-slot/mode retention, entry selection/replay, absorption, payload/spell/category/projectile blockers, raw digest mismatch, real low-score redirection, exhaustive threshold/contact boundaries, post-redirect hit/penetration flags, byte-sized group scheduling, and schema-seven continuation without RNG changes. Legacy checks for the original two physical snapshots remain.

Godot smoke exercises the actual mixed-roster Start button, the tactical Resolve button, native facts, profile-version state and saved continuation. Menu reference/facts windows now hide the existing exclusive modal before opening another. No claims of native condition/magic/ranged/world behavior are made for the 519 blocked records.
