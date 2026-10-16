# Iteration 036 — enemy behavior, perception and memory

New tactical battles now assign saved behavior profiles to every opponent. Open **Menu → New tactical battle** (four opponents shows all four profiles), select an opponent and its tactic in the tactical window, and resolve rounds. The window shows each brain's state, selected target and last observed cell, plus explanations of decisions in the completed round. Profile changes commit immediately; draft player orders still commit only on Resolve round.

## Authored behavior profiles

| Profile | Approach / target policy | Special behavior |
| --- | --- | --- |
| Brute | Shortest reachable path to an attack cell of a visible hero | Stable ID ties; attack an adjacent available hero first |
| Hunter | Prefer the lower current/max HP fraction among reachable visible heroes | Among adjacent heroes, attack the most wounded; does not leave an adjacent fight to chase a distant victim |
| Flanker | Prefer targets with fewer living allies already assigned, then shorter / less crowded / less exposed approaches | Distribution uses current saved ally target intentions; these are not hard reservations, facing or a flanking damage bonus |
| Skirmisher | Ordinary approach while healthy | At or below 25% of authored maximum HP, choose a legal step that strictly increases minimum distance to perceived threats; when cornered, attack an adjacent hero or wait |

New scenarios cycle these four profiles across opponents. Default vision is seven Chebyshev cells, memory three rounds. The UI allows changing a tactic without changing HP, positions or the remaining combat facts. Training maximum HP is explicitly supplied as 48 when creating these authored actors; it is not inferred when loading old saves.

## Perception and action lifecycle

Targets must be living, free heroes inside vision with clear existing supercover line of sight. Walls and occupied living cells block sight. Without a visible reachable target, a brain may search its remembered target's last seen cell, not follow its current hidden coordinates. Reaching that empty cell clears memory; memory also expires or is discarded when the remembered hero is dead/captive. An unreachable remembered cell produces a bounded wait. There is no sound/hearing or shared sight model.

The planner knows the laboratory terrain and routes against the engine's current occupancy. This is target perception and memory, **not** a full fog-of-war implementation: pathfinding still uses authoritative occupancy, and flanker coordination can read allied target intentions.

Every enemy attack group re-evaluates visible adjacent targets against the current state, including changes caused by earlier heroes and enemies. Killing a target permits a later attack group to choose another adjacent target. There is no extra action budget: after beginning an attack, an enemy cannot spend a later attack group on movement. A movement/retreat consumes the whole turn. Individual swings do not retarget inside an attack group. Player orders retain their explicit target and previous cancellation semantics.

Planning is deterministic and does not draw RNG. Recovered initiative, ordinary character attack budgets, contact/damage and existing spell arithmetic are unchanged. Movement legality and supercover sight remain shared with player orders. Dead actors never execute brain decisions in the round executor.

## Saves and compatibility

Game schema **6** persists `TacticalField.Brains`: complete per-opponent profile, authored maximum HP, target identity, last observed cell, observation round and behavior state. Validation rejects missing/extra brains, invalid enum/domain values, incomplete memory, nonexistent hero identities, future observations and impossible coordinates.

Schemas 1–5 migrate without generating brains. Existing schema-five tactical encounters keep their legacy pursuit behavior, including its fixed target per round. Earlier save schemas cannot carry non-null brain data. A new tactical battle opts into the new AI. Battle/RNG/brain continuation after save/load is checked exactly.

## Verified scope

`EnemyAiChecks` covers nearest-path brute selection, wounded-target hunter selection, allied target distribution, dead/captive targets, pure deterministic planning, visible observations, last-known-cell search, expiry and completion, hidden-coordinate independence in a fixed route fixture, healthy/critical/cornered skirmishers, no movement after starting an attack, blocked memory, dead actors, schema-five migration, invalid memory/profile rejection, same-round retargeting after killing a hero, and exact saved continuation with eight mixed-profile enemies for every party size 1–8.

Existing tactical checks now run against default profiled brains, including 64 party/enemy size combinations and twelve complete multi-opponent victories. Godot smoke exercises real movement/round buttons, confirms brain perception, changes an opponent's tactic using the real dropdown signal, then verifies exact save continuation. Original-catalog spell tests remain conditional on the private catalog; public CI uses only distributable fixtures.

## Boundary

This is an authored tactical controller for the current **training character opponents**. It is not a reverse-engineered native Wizardry 8 monster AI and does not convert the 595 monster reference records into executable actors. Imported monster HP, levels, attacks/abilities and actor scheduling still require verified field extraction and integration. Monster spellcasting, ranged attacks, native faction/morale behavior, original continuous formation movement and reactions remain open. No private game data is published.
