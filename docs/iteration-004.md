# Iteration 004 — melee weapons in the battle

## Delivered

Assigned melee weapons now roll their imported `DamageDice` × `DamageSides` plus `DamageConstant` instead of the hero's fixed test power. The log reports the weapon, expression, rolled total and actual HP removed. Hero speed plus weapon initiative determines the stable round order. This combination is a provisional initiative rule, not a recovered full Wizardry 8 formula.

Unarmed heroes retain the controlled power-7 fixture. Enemy damage, guaranteed hits, single attacks, target selection and sentinel stats remain experimental. Strength/skill multipliers, armor absorption, penetration, criticals, bonus swings, curses and weapon attack modes are not simulated. A zero-dice percentage record cannot be assigned as a standalone melee damage source.

## Reproducible rolls and saves

The prototype uses its own generator, **not the original RNG**. A 32-bit state advances modulo 2^32 using `state * 1664525 + 1013904223`. Its upper 24 bits are mapped to a die by rejection of the incomplete modulo bucket. The default seed is `0x4E57495A`; no wall-clock seed or renderer timing enters battle resolution. This is a deterministic development fixture, not a final gameplay randomness policy.

`GameState.RandomState` is saved. Old schema-1 saves missing this optional field receive the default seed. No roll is consumed by an unarmed action, finished battle, captive party or actor killed before its queued attack. Equipped combat requires a validated catalog; client save loading continues to require the same imported database hash.

## Validation

- 125 portable C# checks; 129 with the private original catalog. Checks include RNG vectors and rejection, all supported die bounds, save continuation, initiative before a lethal enemy, skipped actions, weapon combat/XP conservation for parties 1–8 and original Cane of Corpus base dice.
- 12 Python asset-tool tests pass.
- Godot .NET build succeeds without warnings/errors. Client combat/equipment/save smoke passes with 819 original records. Touch/pause/autosave smoke passes.
- Original item data remains local and is excluded from public source and APKs. Android CI uses the synthetic staff fixture.

## Mobile feedback

On 2026-10-14 the user confirmed basic APK operation: “Норм, пашет”. Model/Android version, lifecycle details and performance measurements remain unreported. This closes the basic launch uncertainty, not the complete device checklist.

## Next

Verify hit/penetration and complete initiative formulas against original-game evidence before adding them to combat. Magic and crafting remain dedicated roadmap stages; this increment does not implement either.

## CI and Android artifact

Code commit: `4d250ba780e2ed023301d16fdaf2d8c4bd340d3f`.

- Core/client CI passed: https://github.com/Aetton/neowizardry/actions/runs/38623526206
- Android export and signature verification passed: https://github.com/Aetton/neowizardry/actions/runs/38623526140
- ARM64 debug APK artifact: https://github.com/Aetton/neowizardry/actions/runs/38623526140/artifacts/11923280658
- APK: 103208433 bytes; SHA256 `da0cc808b183ab105c06bc039e7d65b1df35def941bb66343c9194240f4e6efa`.
- Downloaded ZIP matched the GitHub artifact digest `5e8f72f32ec0ba990f83138afff075e9a43d90faf82a720d73b90b441450733a`. ARM64 and managed assemblies, synthetic one-item catalog and absence of SLF/DBS files checked.

The workflow generates a fresh debug key. This APK requires uninstalling the previous differently signed prototype; uninstalling removes its app-local saves. Stable development signing is a separate follow-up. The new APK has not yet received device feedback.
