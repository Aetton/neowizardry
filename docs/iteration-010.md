# Iteration 010 — original-layout interface

Replaced the desktop development columns with an original-layout HUD: two portrait rails with vertical HP/MP bars, a lower journal, compass and realm dial, and a compact bottom command strip. Portrait selection feeds the existing Magic/Items/Craft windows. New party, saves and synthetic scenario controls moved into Menu. Controls/dialogs now share a dark metal/brass theme.

The map uses the existing synthetic room's coordinates. Portraits are procedural placeholders; original resources are not included. Stamina, levels, formation and other unimplemented mechanics are not invented for display. Touch mode retains its pad, large actions and overlay panels. See [interface behavior and references](interface.md).

## Validation

- Client builds without warnings/errors; existing gameplay and touch smoke checks pass locally.
- Graphical desktop inspection with eight heroes, plus magic dialog and touch-mode inspection.
- Hosted Linux and exported Windows verification pending before completion.
