# Iteration 054 — walking up stairs

The player could descend stairs but stopped at their risers when walking upward. `WorldWalker.Step` only applied gravity and `CharacterBody3D.MoveAndSlide`; floor snapping follows descending surfaces but does not step up a vertical face.

The shared production walking controller now attempts a step when a grounded character meets a wall during horizontal movement. Its maximum rise is 0.35 world units. It sweeps the full capsule upward and forward, probes the tread beyond the capsule's rounded toe, checks the supporting surface against the existing floor angle limit, and sweeps downward to a clear standing position. The displacement uses the current frame's requested horizontal movement; this does not add a horizontal teleport or change walking speed. The path must remain clear of walls, closed doors and ceilings. Flight mode is unchanged.

No level data, original installation, prepared content or save format changes are needed. This is a Godot movement policy, not a claim that the original Wizardry movement solver has been reproduced.

## Regression check

`NEO_WALKER_SMOKE=1` runs the actual `WorldWalker` against a separate physics fixture:

- Ascend six 0.30-high steps made from triangle collision surfaces, then reach the upper landing.
- Descend the same flight and settle on the lower floor.
- Remain blocked by a full-height collision surface, representing a wall or closed door.
- Reject a 0.50-high obstacle and a step with insufficient headroom.

The test fails with the previous controller: the capsule remains at approximately `(0, 0.799, -0.917)` before the first riser. It passes with the corrected controller. Local client build completed with no warnings or errors. Both Linux CI and the packaged Windows EXE run this physics regression before release.

The fixture reproduces stair geometry but does not establish that every staircase in the original monastery has been traversed. The original monastery audit also remains enabled for world loading, floor contact, wall collision, doors and normal player startup.

Runtime commit: `cd3c3da5d43f5507c035ffa910dbca49a0a9acb3`.
