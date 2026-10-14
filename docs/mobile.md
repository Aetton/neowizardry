# Mobile prototype

## Implemented

- Responsive gold frame, collapsible party/log panels, enlarged touch actions and landscape orientation.
- Fixed left stick and swipe-to-look in the free view, with separate pointer ownership. UI panels and item dialogs block world gestures.
- Gesture reset on pause/resume; background autosave via temporary-file write and rename. Autosave is separate from manual save and is restored on startup.
- Safe-area layout hook for actual mobile displays; adaptive item-dialog dimensions.
- Independent desktop target (.NET 8) and Android target (.NET 9), retaining the same gameplay core.
- ARM64 debug APK export workflow, signature verification and CI artifact upload. Runtime trimming is disabled for the first Android prototype.

## Build and data

The **Android prototype APK** workflow provisions JDK 17, Android SDK and .NET, fetches official Godot 4.5.1 .NET templates and exports a signed debug APK. It generates a synthetic item catalog for the demonstration. Original Wizardry databases are never packaged.

Godot 4.5 Android C# export requires .NET 9; C# mobile support is experimental. References: [Android export](https://docs.godotengine.org/en/4.5/tutorials/export/exporting_for_android.html), [.NET migration](https://docs.godotengine.org/en/stable/tutorials/migrating/upgrading_to_godot_4.5.html).

Data precedence in the client: an explicitly selected catalog; otherwise `user://items.json`; otherwise packaged `res://demo-items.json`; otherwise the desktop installation's local catalog. Mobile loading uses Godot file access for packaged/app-private files.

On desktop, preview touch UI with:

```sh
godot --path client -- --touch
godot --headless --path client -- --touch --mobile-smoke --catalog /absolute/path/to/test-items.json
```

## Evidence and device gate

The core and touch state machine have automated checks. Godot desktop smoke injects touch events and verifies movement/look, pause reset, atomic save and restoration. HUDs were rendered at 1280×720 and 1024×768. These checks do not establish Android device behavior.

A built, signature-verified APK is a separate gate from running it on a physical device. Before declaring mobile support validated, test:

1. Install and launch, with desktop keyboard/mouse absent.
2. Move and look simultaneously; release fingers in different orders.
3. Open party/log/items; scroll and operate controls without moving the camera behind them.
4. Background during movement and combat, resume, then terminate and restart; verify state and absence of stuck gestures.
5. Check notch/safe area, readable type and comfortable touch targets on an actual phone and tablet.
6. Measure frame time, temperature, battery use and memory during a longer session.

The current room, combat and demo catalog are synthetic. iOS remains a later export/device gate requiring macOS/Xcode. Debug signing is temporary; different CI-generated keys can require replacing an existing prototype installation rather than updating it in place.
