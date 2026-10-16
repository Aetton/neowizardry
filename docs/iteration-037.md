# Iteration 037 — first original monster actors

**Menu → New monster battle** starts an opt-in physical monster encounter on the existing tactical field. Select party size, opponent count and monster definition. With the private original catalog loaded, the reviewed choices are **Dusk Bat (ID 282)** and **Shrieker Bat (ID 18)**. A separately labelled synthetic Practice creature is available without original data and exercises the public CI path. Existing original catalog JSON files work without reimport.

## Native facts and evidence

Static evidence uses the supplied EXE SHA-256 `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`. No original runtime comparison or commercial version identification is claimed. The 663-byte loader established in iteration 034 preserves numeric field offsets.

| Definition field | Offset | Evidence / use |
| --- | --- | --- |
| Base five attributes | D1–D5 | `4E5D00` loops D1+index, applies signed runtime adjustments via mapping 0/1/4/5/6, clamps 1–125, writes actor 247+index; INT/DEX/SPD/SEN correspond to D2–D5 |
| HP expression | D6: signed int16 constant, unsigned byte dice/sides | `4E39D2–4E39E8`, repeated at `4E654A–4E6560`, passes to `517970` and assigns maximum/current HP; `517970–5179A4` adds one-based dice to signed constant |
| Initial initiative | E4: unsigned byte | `4E8BA8–4E8BE1`: add exclusive rand(4), clamp -10..89, time = 90 minus value; same pure FirstTime helper, no character initiative formula |
| Attack groups / maximum swings | E5 / E6 | Existing iteration-028 reconstruction; separate monster budgets, independent of character BAR/class history |
| Selected attack record | E7, stride 22 hex | Existing selected-rating/mode/category evidence; this slice only permits the reviewed single enabled kind-0/category-0 Thrust slot |
| Attack rating | Attack +4 | `542720` monster score path; no character BAR substitution |
| Attack dice | Attack +17 hex | `542BB1–542BC9` passes to `517970`; minimum one at `542BC6–542BD0`; this neutral ordinary Thrust has one repetition |
| Physical absorption | CF | `542BE3–542C1C` adds definition CF and target runtime modifier for monster target; both reviewed records have CF=0, required by the gate |
| Native armor and seven zones | 15D–175 | Verified fields and OriginalMonsterDefense from iterations 026/034 |

The raw records remain in the user's existing private catalog. `MonsterRecordSnapshot` stores the complete selected private row in a save, with definition ID, database hash and record hash. Derived combat facts are compiled from those bytes through a weak cache and cannot be edited separately as typed overlays. Reviewed original record hashes are pinned; altered or unsupported records are rejected even after a caller recomputes a hash. The public Practice creature record is authored in code and is not original game content.

## Connected combat

Monster actors have no character-class facts and no inventory weapon. `MeleeEncounter.Opponent` and secondary character facts/weapons are null in this mode. Each actor rolls HP once from its native dice; the same value supplies actor maximum and AI maximum. Current HP, positions, record snapshots and brains remain authoritative and immutable.

The queue uses definition E4 for initial initiative, E5 for groups and E6 for swing allocation. The recovered monster score builder and a separate ordinary monster strike resolver attack the existing authored heroes. The natural d100 contact/penetration gates are shared numerically with the recovered ordinary path, and damage dice are requested only after contact and penetration pass. Monster damage does not receive the character's Strength/weapon scaling. Party attacks use the monster's effective comparison attributes and its native seven-zone defense instead of a character torso/armor model.

All previously authored grid movement, perception, memory, profile selection and retargeting rules remain active. Continuation timing reuses the laboratory's existing attack-group timeline; a full native monster action controller is not claimed. Neutral state is explicit: signed effects, fatigue, repetition context, special hit flags and target damage absorption are zero; native status and interruption producers remain open.

## Deliberate gates

Only the two pinned rows and the synthetic CI fixture are enabled. They have one enabled physical Thrust slot, no attack status payload in the checked bytes, zero base physical absorption and a neutral untargeted hit score of at least 100. Forest Mite was examined but excluded: rating 100 minus the Thrust hit modifier gives 90, requiring the unimplemented native fumble selection path. NPC records, other modes, multi-slot/mixed actions, innate special behavior and all other monster rows remain references.

Original spells are disabled for this monster battle. Target-level/resistance producers have not yet been connected, so training target magic profiles are not substituted. The existing original spell/character battle stays available separately. XP and loot are explicitly zero/unconnected; no opponent weapon is created or cloned as monster loot. This is a neutral ordinary physical slice, not full reproduction of either bat's native behavior or every possible condition/trait.

Game save schema **7** preserves monster snapshots, rolled maximum/current HP, AI and geometry. Schemas 1–6 migrate without fabricating monster actors; older schemas reject injected monster data. The selected complete record snapshot is enough to continue the physical encounter; party inventory still requires its matching item catalog. No original records, saves or disassembly are committed to git or the public package.

## Verification

`MonsterCombatChecks` uses an authored portable record for the public pipeline and conditionally exercises both pinned original rows with `--monsters local/monsters.json`. Checks cover exact HP RNG progression, absence of phantom class/weapon facts, real intrinsic Thrust attacks, native target zones, separate group/swing bounds, primary death with surviving enemies, exact saved continuation, malformed/modified records, mixed actor facts, invented rewards, schema-six migration, all 64 party/enemy count combinations, complete multi-monster victories and idempotent zero-reward completion. Pure strike checks cover minimum damage, natural miss without damage draws, rejected extra dice, unsupported modes and fumble gating.

Godot smoke starts the monster mode, resolves its actual tactical round button, verifies no enemy-owned weapons, opens native combat facts after closing the tactical modal, and checks exact save continuation. Public CI uses the Practice creature; the original bats require the private catalog and their checks are not claimed as public CI coverage. The menu now hides its modal before opening a battle field, fixing exclusive-child window collisions.

Next: native target magic facts, low-score fumble/retargeting, more complete attack/condition eligibility, XP/loot producers and additional monster definitions. Continuous world movement and native AI remain separate from the authored grid controller.
