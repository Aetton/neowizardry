# Iteration 048 — connected world expedition and one encounter

An opt-in `--expedition` mode joins exploration, party/inventory and one physical encounter in one authoritative envelope. Example:

```powershell
NeoWizardry.exe -- --world "D:\actions-runner\_work\wiz_data\NeoWizardryWorld\monastery1.glb" --expedition --party-size 1
```

`--party-size` accepts 1–8 when creating a new expedition. N jumps to native arrival points. The orange marker at the first positioned arrival is an explicitly authored encounter placement, not a recovered native spawn. Approach within three preview units with clear static/door line of sight, then press B. A scrollable tactical panel opens over the same world: select a hero, tap an adjacent free cell to move or an adjacent enemy to attack, or resolve a default round. Geometry/camera/door animation pauses during active combat. Victory returns to exploration in flight; defeat retains the outcome and permits loading an earlier save. The marker is a placeholder sphere; native monster models/animations are the next workstream.

F5/F9 in this mode save/load one `WorldExpeditionState`: GameState (sole party, inventory, HP, combat positions/AI/conditions, RNG and reward latch), world checkpoint and encounter phase. Entering a battle never recreates those actors or their inventory. Ready/Active/Victory/Defeat must agree with combat state. Completed encounters cannot begin or resolve again. Loading mid-battle reopens the panel; loading victory hides the encounter marker. Camera always restores in flight, preserving the existing collision-synchronization boundary. There is no automatic save/restart after defeat.

Files use `user://expeditions/<world-content-hash>.json`, separate from preview checkpoints and training-room saves. The envelope binds the world/sidecars, item database, monster database and exact current monster record; changing catalogs fails closed before state mutation. Writes are flushed and replaced atomically, with a 16 MiB envelope bound. Failed loads preserve the live session and original file. Existing preview/training files are not migrated or overwritten.

Private catalogs are selected from explicit `--catalog` / `--monsters` paths, files beside the imported world, user data, then local source data. Missing catalogs fall back to clearly named practice equipment/creature; malformed or explicitly missing catalogs fail rather than silently falling back. Original intake now exports private item/monster catalogs beside the worlds. Dusk Bat (282), when supported, is preferred; otherwise the first admitted physical profile is used. These are imported monster combat facts with authored party effective stats, grid, AI and placement. XP/loot remain zero until original rewards are recovered; no invented original reward is granted.

## Validation

- 168 new core checks cover all party sizes, immutable encounter entry, exact mid-round continuation, authoritative inventory, victory/defeat persistence, invalid phases/catalog/world bindings and no respawn/reward replay.
- 3,930 total portable core/input checks and 59 Python importer tests pass locally.
- Client build is warning-free. Authored world smoke exercises reach rejection, actual battle-panel opening, save/load while active, failed-load preservation and terminal-state reload.
- Linux CI passed: https://github.com/Aetton/neowizardry/actions/runs/39062315530 . Complete Windows build/export/EXE/updater publication gates passed: https://github.com/Aetton/neowizardry/actions/runs/39062315451 . Preview: https://github.com/Aetton/neowizardry/releases/tag/windows-build-65 .
- Original intake exported private item/monster catalogs beside the worlds, and both native monastery levels passed a solo expedition through actual victory, mid-battle continuation, failed-load preservation and no respawn: https://github.com/Aetton/neowizardry/actions/runs/39062315452 . All validation uses runtime source 5dc23a7bcfd0d42096976d283474d8369ea2a050. No original creature model/animation or recovered native encounter-position claim is made.

Next: native creature model/animation import, followed by representative rewards, progression and events. This iteration connects a scoped physical game loop; it does not establish a complete monastery or native continuous-world battle solver.
