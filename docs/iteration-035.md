# Iteration 035 — multi-opponent tactical laboratory

The previous ordinary character duel remains available. **Menu → New tactical battle** starts an explicitly authored 8 × 8 grid laboratory with the selected 1–8 heroes and 1–8 independently owned character opponents. Menu → Tactical field reopens it; Attack and Magic open the same field while this mode is active. No private original catalog data is published.

## Playable scope

Choose a hero, Move / Attack / Spell, then a cell. Wait consumes the hero's round; Clear removes queued orders. Resolve round schedules all living free actors with recovered character initiative. Melee attack groups interleave with the same continuation arithmetic as the ordinary duel. Unordered heroes attack an adjacent enemy selected by stable ID, or wait. Enemy AI selects an adjacent hero or uses breadth-first search for a reachable adjacent attack cell; it moves one cell this round. Ties are deterministic. Enemies decide their action when first scheduled, against current positions, and keep that target for remaining attack groups.

Each actor owns a position, HP, effective melee facts and weapon instance. Primary enemy compatibility fields remain canonical for that one actor only; additional enemies are stored in `Melee.Field.Opponents`. All actors use the recovered **character** physical rules. These opponents are training duelists, **not imported monster actors**.

## Explicit laboratory rules

- Eight directions, one-cell movement consuming the whole round. A diagonal move cannot pass a wall or occupied corner. This is a testing grid, not native world-unit movement or formation recovery.
- Adjacent, visible melee targets only. Pillars and living free actors block sight. Supercover sight also blocks diagonal corner cutting. Supported single-target spells have no additional distance limit on this small board; they require clear sight. No ranged weapon delivery, area templates, facing, flanking bonus, attacks of opportunity or native collision claims.
- All player orders are validated before RNG or state changes. Initial occupied cells are unavailable even when another actor plans to leave. Two orders for the same initially free destination may both be submitted; initiative resolves the winner and the other move cancels.
- Corpses and captive heroes do not block movement/sight. Dead/captive actors do not act. Targets that die or become unreachable before execution cancel the action; there is no automatic retarget. A cancelled spell spends no mana or effect draws. Completed spells preserve the existing risk, backfire, damage/resistance and healing behavior.
- A party move does not carry an attack. Positions are independent of the exploration camera. There is no second 3D group-combat rendering; the tactical window is the interactive combat surface.
- Frost, Energy Blast and Heal Wounds remain the only connected original spells. Offensive spells can select any living opponent, healing any living free hero, powers 1–7. The other 147 catalog entries remain reference data.
- Victory requires **all** opponents dead. Total authored XP is divided among living free heroes once. Exact owned opponent weapons transfer to the shared bag once; no cloning of loot. Defeat grants no reward.

## Persistence and validation

Game save schema **5** preserves every opponent, position, obstacle, weapon ownership and RNG state. Schemas 1–4 migrate without creating tactical facts. Schema 4 cannot smuggle in a tactical field. Existing duels and synthetic fixtures keep their mode. Orders in the tactical window are a transient draft: closing the window discards them, and the UI says so. Save records the last committed battle state.

Core validation rejects overlapping living actors, duplicate identities, invalid coordinates/obstacles, incomplete profiles and missing opponent-owned weapons. The tactical executor reuses the ordinary weapon restrictions and recovered physical/magic helpers. Preflight and execution operate on immutable snapshots.

## Verification

`TacticalCombatChecks` covers 64 party/enemy size combinations, blocked reach/sight/corners, competing movement, corpses, primary-opponent death, killed queued enemy and dead-target cancellation, defeat, 12 complete multi-enemy victories, XP/owned-loot conservation, no repeated rewards, schema-four migration and exact continuation after save/load. With the private original spell catalog supplied, it also checks selected-secondary-enemy Frost/Energy Blast at every power, exact realm cost and replay, and ally Heal Wounds at every power. These private spell tests are conditional and are not part of public fixture-only CI.

Godot smoke exercises the actual tactical cell button and Resolve round button, confirms the movement of a hero and three opponents, and checks save continuation. Existing melee, original spell UI, inventory, catalog, migration and synthetic sandbox checks remain in place. Windows CI runs the exported client smoke and launcher/package checks.

## Still open

Original continuous movement, party-relative formation, native AI target eligibility/retargeting, imported monster HP/attacks/AI, conditions/interruption producers, projectiles/collision, area/ongoing spells and complete original magic coverage. The grid creates a place to test those additions; it does not recover their policies.
