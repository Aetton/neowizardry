# Iteration 047 — exploration preview checkpoints

The monastery viewer now supports **F5** to save a checkpoint and **F9** to restore it. The checkpoint contains camera position/orientation, flight speed and every door's current frame, target frame and fractional animation phase. Loading always resumes in flight, clears queued door/walking requests and stops the inactive capsule. Press F at an aimed floor to enter walking again. This avoids restoring an occupied capsule before physics has synchronized the restored door shapes.

These are authored preview checkpoints, independent of expedition saves and the native SAV format. They do not implement campaign state, quest facts, level transitions, encounters, walking velocity or automatic resume. There is one manual checkpoint per exact content identity, under `user://exploration/<content-hash>.json`; changed imports retain the old file but select a different identity. Moving identical imported files to another directory preserves that identity.

Identity includes the main GLB plus the presence and SHA256 of collision, object, door-manifest and door-geometry sidecars. Validation requires the exact door ID set and frame limits. Restricted doors must retain their imported initial pose; a checkpoint cannot unlock them. Full validation precedes visible/physical mutation. Invalid, missing, truncated, oversized or mismatched files leave the current scene and saved file unchanged. F5 writes through a flushed temporary file in the destination directory and replaces the checkpoint only after writing succeeds. No background save can overwrite a failed load.

Saving while P animation inspection is active is refused. F9 may leave inspection only after the checkpoint has passed validation. Source flags remain unchanged. Door animation resumes from the saved phase; a small negative phase caused by the existing frame-boundary tolerance is clamped to zero.

## Validation

- 59 Python importer tests pass.
- 3,762 portable checks including mobile gestures pass, including 33 new checkpoint checks: serialization, malformed/foreign/restricted poses, size limits, file replacement, failed-write preservation, failed-rename cleanup, source identity and malformed UTF-8.
- Godot .NET client builds with zero warnings/errors.
- Authored world runtime smoke passes: camera roundtrip, moving-door phase/target continuation, refusal to capture temporary inspection, rejection before mutation, existing door/capsule/static-collision checks.
- Original monastery data and Windows export are not newly validated locally for this change. Previous original-level/Windows validation remains recorded in iteration 046; these checkpoints still need a Windows/native-level check before being called release-validated.

The next connected-world gate remains exploration → encounter → exploration with shared party/inventory and saved world/encounter identity. This checkpoint is only its world-state foundation.
