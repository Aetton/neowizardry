# Iteration 046 — native door geometry and constrained interaction

Private door frame export reads native prop meshes (including compressed vertex frames, per-face UV/material indices and channel-2 parts), reconstructs path poses, and binds both GLB and manifest to source hashes. It uses the independently reconstructed loader layouts from [the fixed decomp research revision](https://github.com/agluszak/wizardry-8-decomp/tree/075cbd0b853c4670e94f6ae4fe48d53b52b5e819), especially Prop::AttachAnimationInstances, PathAI, ReadMesh and Trigger::Run. No source implementation is copied.

Serialized float vertices are multiplied by 500; compressed shorts use 500/compression_scale. Path position, rotation and optional scale replace the model-instance placement; without a path, the prop representation location is used. The final viewing transform remains `(x,y,-z)*0.0013`. Each frame is baked separately; smooth quaternion interpolation and native solver parity are not claimed. Mesh-transform fields consumed by ReadMesh are replaced by the prop/path placement as in the prop attachment path. Only the active channel-2 geometry is included, not all prop animation channels.

Interaction scope is an authored, reversible preview of unconditionally eligible door endpoints. Locked, jammed, undiscovered, item/state-dependent, linked, one-way, timed and unsupported action records fail closed with a reason. Original inventory, quest flags, callbacks, recipient dispatch and transition integration are not provided by the isolated world viewer. Source flag 0x01 controls physical collision; all rendered doors can be picked for inspection. Original-derived door frames and embedded textures remain private beside the source cache; public reports contain aggregate counts and frame bounds.

R picks the nearest door within an authored 3-unit reach, respecting static wall occlusion. Eligible doors animate between the first/last exported frames at the source playback rate; the current frame's collision shape follows its visible mesh. Collision is two-sided in this preview. A conservative capsule-AABB occupancy check can stop a frame change to avoid moving geometry through the player. This check is authored, may reject some physically clear moves, and is not the original swept-body solver. Door state is session-only. O still shows stored source flags rather than the temporary preview pose.

Runtime smoke checks all frame bounds and render/collision bindings against export metadata, refuses restricted interaction, animates eligible doors in both directions and exercises the occupancy stop. Source PVL and door-GLB hashes are checked independently. Linux also corrupts the authored door GLB and requires an explicit hash-mismatch failure. Texture pixels/IFL cycles and existing floor/wall/capsule checks remain enabled.

Original intake passed on both levels: https://github.com/Aetton/neowizardry/actions/runs/39040981593. Action 1 uses Data1 for its automatic closing timer; action 2 uses none of the three action data ints. Unused Data2/Data3 sentinels do not block manual preview. Active timers, disabled triggers, linked and quest-state owners remain blocked.

P in flight mode cycles door models and previews a round trip through their native frames, including restricted doors. It never unlocks or activates their triggers. The pre-inspection pose is restored on completion, cancellation, Home/N navigation, or before F enters walking (with a physics-frame delay for collision synchronization). Runtime smoke also checks this restoration for every door. This inspection is separate from R's constrained manual interaction.

Door bounds are checked from the actual imported render vertices with a 0.002 preview-unit tolerance for importer precision. Expected bounds include only referenced face vertices, consistent with GLB export. Occupancy and inspection focus use the same geometry-derived bounds.


## Final validation

| Level | Models | Frames | Frame triangles | Source-collidable | Manual interaction eligible |
|---|---:|---:|---:|---:|---:|
| Monastery1 | 7 | 166 | 5,060 | 7 | 2 |
| Monastery2 | 4 | 109 | 1,308 | 4 | 0 |

Final source `4d1c2c8d230b8ed8166d9d0a7f3652cd01a2ec1e` passes [Linux checks](https://github.com/Aetton/neowizardry/actions/runs/39041827351) and [both original-level checks](https://github.com/Aetton/neowizardry/actions/runs/39041827337). The Linux run covers 59 Python tests, default client/core/mobile behavior, authored eligible door movement and occupancy refusal, physical capsule blocking, and rejection of corrupt door geometry/object provenance. Original runs verify every door frame's imported bounds, render/collision binding, restricted requests, reversible eligible animation and inspection restoration, a capsule sweep blocked by a native door body, and existing texture/IFL/static-world checks. Native geometry placement still requires visual comparison against the original; headless checks do not establish native shading or smooth-animation parity.

Windows preview 62 is published from that final source: https://github.com/Aetton/neowizardry/releases/tag/windows-build-62. Export, runtime smoke and the launcher's first online/offline download verification pass: https://github.com/Aetton/neowizardry/actions/runs/39041827331. Original frame models, embedded textures and manifests remain private in `_work/wiz_data/NeoWizardryWorld`; only the code/runtime ZIP is released. Source door/quest state changes are not persisted by this preview.
