# Iteration 052 — original creature particle sprites and emitters

The creature importer now retains MON particle records semantically, imports their original TGA/IFL sprites and exports animated attachment tracks. Godot plays these as cosmetic billboard particles alongside the existing frame animation. This applies to the shared catalog pipeline; no replacement art is generated and original/derived assets remain private.

The supplied archives retain **582/595 renderable definitions, 193 model/script sets, 173 distinct GLB payloads, 2,136 clips and 66,242 frames**. Particle-bearing clips number **388**, containing **1,767 emitter instances across script sets**; **1,761 are supported**. The six blocked instances require native line-of-sight collision. They affect seven definitions through shared models. Of the 342 particle-bearing definitions, 335 have no blocked emitter. All previously exported geometry clips are preserved, including the greater-demon spell mesh with a repeated marker key; no new model blockers were introduced.

## Import and playback

- Versions 1–4 preserve record hashes, counts, timing, emission limits, speed/direction/cone, acceleration, lifetime/texture expiry, spread, bounds, flutter, opacity and frame windows. The mesh-v4 marker table is read as `(marker key, original vertex index)`. Anchors follow compressed or individual mesh frames and animation paths. An absent marker uses the stored source position, as the native attachment loader does.
- Sprite banks are embedded in the same hash-bound GLB, with the existing decoded pixel hashes. Each particle owns its IFL playback clock. Runtime billboards use a separate cosmetic random stream, retain already emitted particles when a clip changes, and expire them after emission stops. Death playback continues to drain particles after its final geometry frame is held. Cleared saves restore the completed pose without replaying the death burst.
- Both catch-up and single emission modes, capacity replacement, emission limits, stationary/fixed/forward/cone/random directions, acceleration, box/sphere bounds, velocity-scaled flutter and lifetime fading are represented. Playback uses bounded time slices to preserve short frame windows and prevent a long render frame from skipping an attack effect.
- The gallery reports supported and blocked emitter counts. Native line-of-sight effects remain explicitly blocked until a world-collision adapter exists; camera-relative CLOUD emitters also have an explicit blocker. A missing sprite or unsupported texture is reported per emitter without removing otherwise valid creature geometry.
- Export version 3 invalidates prior cached assets. Existing schema-1/2 geometry-only manifests still load. Re-export with the command in iteration 051 to obtain effects; a new executable alone cannot add sprites absent from an old private export.

This is independent preview playback, not original-renderer parity. Native frame-event crossing/subcycle choices, aimed spell trajectories, exact renderer timing, shader blending/lighting, world collision, sound and shake remain separate. Rendering an attack effect does not implement its damage or spell mechanics. Combat coverage, original spawning, rewards and progression are unchanged.

Reference evidence: [075cbd0](https://github.com/agluszak/wizardry-8-decomp/tree/075cbd0b853c4670e94f6ae4fe48d53b52b5e819), LevelFile W8LevelParticleRecord, ReadLevel ReadWorldParticles, ReadMesh marker loading, GrCycle UpdateParticleAttachments, Monster particle visibility/frame events, stParticle activation/update/billboard preparation. Parser and simulator are independent implementations.

## Validation

71 Python tests pass, including version defaults, truncation/non-finite rejection, invalid marker indices, moving compressed-frame anchors, repeated-key replacement, normalized emitter axes, native units, embedded particle sprite bindings and missing-sprite blockers. The client builds with zero warnings/errors; 4,196 gameplay/input checks pass. The authored Godot fixture exercises four particle-bearing clips, animated textures, limits/expiry and the connected expedition save/reward loop. The final normalized-axis export passes the complete local Godot gallery: all 582 definition bindings and 178 distinct model/clip configurations, every geometry frame/skin and supported sprite emitter. Emitter probes check forward/cone coordinate reflection, image bindings, finite motion/alpha, capacity/limits and expiry after emission stops.

Runtime source: `d12ef11d5ae588d0216bc2de0fab8c302ef36ddc`.

- Linux checks passed: https://github.com/Aetton/neowizardry/actions/runs/39082687487 .
- The self-hosted original-data audit passed with matching coverage (1767 emitters, 6 explicit LOS blockers, no lost geometry clips): https://github.com/Aetton/neowizardry/actions/runs/39082687535 . Both original monastery levels pass full creature playback, 794 XP, save continuation and no respawn; the complete gallery passes all 582 bindings / 178 configurations.
- Windows build, exported EXE, updater and first-download gates passed: https://github.com/Aetton/neowizardry/actions/runs/39082687524 . Preview: https://github.com/Aetton/neowizardry/releases/tag/windows-build-72 .
- The old schema-1 Dusk Bat asset still loads (5 clips / 156 frames), preserves its 794 XP reward and passes world/battle save continuation and no-respawn checks.
- Headless checks establish source binding and playback integrity; pixel-for-pixel comparison with the original renderer remains open.
