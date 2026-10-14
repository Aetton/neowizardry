# Iteration 006 — shared inventory and test loot

## Delivered

- A shared party bag with positive item quantities. Equipped copies occupy hero slots and are excluded from spare bag quantities.
- Equipping consumes one owned copy; swapping returns the previous weapon; unequipping returns one copy. Repeating the same operation cannot create another copy.
- Transfer through the shared bag, preserving race/profession/gender restrictions and captured/dead-hero equipment restrictions. Failed operations leave the source state unchanged.
- Owned items are shown by default in Items; full-catalog browsing remains available for inspection. Seeing an item in the catalog does not grant ownership.
- Limited starter equipment for new **test expeditions** and one configured item from the training sentinel on victory. Loot is awarded once with the victory reward, including after save/reload.
- Bag quantities, equipment and planned enemy loot survive saves and require the matching item database. Older saves without a bag retain equipped copies; unequipping returns that one owned copy. They do not receive retroactive starter grants or loot.

The core groups identical IDs by quantity. This is the foundation for inventory operations, not a complete original inventory: item instances, identification, charges, durability, encumbrance, poison effects, curse mechanics/removal, commerce and recipes are still pending. Test starting equipment and loot are not original-game tables. Magic and crafting remain dedicated roadmap stages.

## Validation

- 148 portable C# checks; 152 with the private original 819-item catalog. New checks cover copy conservation, two heroes sharing a limited bag, swapping, rejection rollback, invalid stacks, overflow, old equipped-save migration, bag-only catalog binding and one-time loot across reload.
- 12 Python tests pass. Godot .NET build succeeds without warnings/errors; original-catalog client smoke passes. Inventory modal rendered and inspected at 1280×800 on Linux.
- Windows CI: https://github.com/Aetton/neowizardry/actions/runs/38646478696 — succeeded, including exported EXE smoke, 13 updater checks and public launcher download/offline mode.
- Portable/core CI: https://github.com/Aetton/neowizardry/actions/runs/38646478682 — succeeded.
- Code commit: `17b470dc34214d87c93605eb01324b6cac26b9b3`.
- Preview: https://github.com/Aetton/neowizardry/releases/tag/windows-build-4

Original datasets remain local. Windows preview packages the synthetic staff catalog.

## User check

Launch the existing `RunNeoWizardry.cmd` to update. Use **New party** for the new starter bag, then open **Items**, equip/return a weapon and defeat the sentinel. Check the bag count and save/reload it. A previous save retains its earlier equipment but does not acquire fresh starter items automatically.

The user confirmed the preceding Windows build's basic operation on 2026-10-15: “Запустил, прогнал, пашет”. This iteration has passed CI; user-device feedback for this new build is still pending.
