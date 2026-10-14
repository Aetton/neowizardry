# Iteration 010 — original-layout interface

Replaced the desktop development columns with an original-layout HUD: two portrait rails with vertical HP/MP bars, a lower journal, compass and realm dial, and a compact bottom command strip. Portrait selection feeds the existing Magic/Items/Craft windows. New party, saves and synthetic scenario controls moved into Menu. Controls/dialogs now share a dark metal/brass theme.

The map uses the existing synthetic room's coordinates. Portraits are procedural placeholders; original resources are not included. Stamina, levels, formation and other unimplemented mechanics are not invented for display. Touch mode retains its pad, large actions and overlay panels. See [interface behavior and references](interface.md).

## Validation

- Client builds without warnings/errors; existing gameplay and touch smoke checks pass locally.
- Graphical desktop inspection with eight heroes, plus magic dialog and touch-mode inspection.
- Hosted Linux passed; Windows export and both exported EXE smoke checks passed for preview 12. Its public launcher discovery received HTTP 403 from GitHub. Added an anonymous public Atom release-feed fallback, with manifest/package SHA256 validation unchanged; complete verification passed in [Windows run 38656363897](https://github.com/Aetton/neowizardry/actions/runs/38656363897), publishing [preview 13](https://github.com/Aetton/neowizardry/releases/tag/windows-build-13). [Linux run 38656363918](https://github.com/Aetton/neowizardry/actions/runs/38656363918) also passed. Windows updater checks now include feed selection and API-failure fallback (17 checks).
