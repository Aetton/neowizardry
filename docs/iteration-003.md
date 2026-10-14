# Iteration 003 — first Android build

## Delivered

- Shared C# gameplay core, desktop .NET 8 and conditional Android .NET 9 target.
- Responsive landscape HUD, collapsible party/log panels, touch stick and swipe look with separate pointer ownership.
- Gesture reset on pause/resume; separate autosave written through a temporary file and rename, restored on startup.
- Packaged/app-private catalog access; the APK contains a clearly synthetic one-item demonstration catalog rather than original game data.
- Android SDK/template provisioning, signed ARM64 debug export, APK signature verification and CI artifact upload.

## Build evidence

Code commit: `744b16658feb0795319b3defe059c7bb50bfebef`.

- Android job succeeded: https://github.com/Aetton/neowizardry/actions/runs/38605090454
- Desktop/core/mobile smoke CI succeeded: https://github.com/Aetton/neowizardry/actions/runs/38605090389
- APK artifact: https://github.com/Aetton/neowizardry/actions/runs/38605090454/artifacts/11915282678
- APK bytes: 103208433 (approximately 98.4 MiB).
- APK SHA256: `f27f6293068cb6b9fe1dcb683b64e26cb627fbae177746c07808280e20f8f362`.
- Artifact ZIP SHA256 matched the GitHub digest: `7e459adf0c6814e01c6704b337b862afc60d08a3a4193c9ba29a6cda4d123be0`.
- APK signing schemes v2 and v3 verified; ARM64 native and game/core managed assemblies are present; synthetic demo JSON is present.

## Validation

12 Python tests. 100 portable C# checks including mobile gestures; 103 checks when using the private original catalog. Godot smoke passed for simultaneous touch movement/look, release/reset, background autosave and restoration. Existing combat/item/save smoke also passed. HUDs were rendered and inspected at wide-phone and tablet aspect ratios.

## Physical-device gate still open

The APK has been built and inspected, **not run on an Android device**. Device lifecycle, notches, physical touch target sizing, GPU compatibility, temperature, battery consumption and performance remain unverified. See docs/mobile.md for the checklist.

This is still the synthetic room and experimental battle system. The new platform work does not implement original combat formulas, real monastery geometry, magic or crafting. iOS remains a separate later gate.
