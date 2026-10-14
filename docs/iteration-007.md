# Iteration 007 — first magic slice

## Delivered

- Saved learned-spell IDs and independent realm mana pools with validation.
- Two synthetic casting fixtures: offensive Test Spark and supportive Test Mend. Power 1–3 changes the cost and number of dice.
- Casting replaces the chosen hero's weapon action within the same deterministic battle queue; other heroes and the sentinel still take their turns.
- Realm-specific spending, healing caps, dead/captured target restrictions and cancellation of spells whose actor/target becomes unavailable before execution.
- Mana, spellbook and RNG save continuity. Older saves remain readable without retroactive magic grants.
- Magic UI with caster/spell/power/target selectors, cost preview and explicit denial reason; HUD shows realm mana. Action-grid height now derives from its actual number of buttons.

All spell data, cost/effect numbers, starting mana and guaranteed success are **test rules**. Original learning, skills, power range, success/fizzle/backfire, resistances, Power Cast, durations and alternative casting sources remain research. Only one selected caster order per round is currently exposed. See [magic coverage](magic.md).

## Validation

181 portable C# checks; 185 with the private original catalog. New cases include per-realm spending, physical-action replacement, self-healing, healing limits, invalid orders, death before casting, target death before healing, ally victory before a queued spell, save continuation, old saves and Mage-configured parties 1–8 with conserved XP. Other party members use fixture physical attacks in these multi-member tests.

12 Python tests pass; Godot build succeeds without warnings/errors. Client smoke casts offensive and healing spells and checks save continuation; original-catalog smoke passes. Magic dialog was rendered and inspected on Linux at 1280×800.

This report records a casting foundation, not completion of the original magic system.

## Build evidence

- Casting commit: `e7acce840c454cdaba6d6b4957056882982771ab`.
- Dialog fit correction: `22dafed3a26fad6ee30ec10cf4a705d11a335838`.
- Portable/core CI: https://github.com/Aetton/neowizardry/actions/runs/38648225687
- Windows export, exported-game smoke and preview: https://github.com/Aetton/neowizardry/actions/runs/38648225599
- Windows preview: https://github.com/Aetton/neowizardry/releases/tag/windows-build-6

Run the existing launcher to update. Use New party, approach the sentinel, open Magic and try Spark at different powers. After taking damage, select Mend and the injured hero. Existing saves retain their previous spellbook/mana state, which may be empty; no retroactive class progression is invented.

Windows CI succeeded, including the public launcher's download and offline check. The exported Windows EXE executed both smoke modes; its magic smoke casts Spark and Mend through the client and checks saved continuation. 13 updater regression checks passed. New-build user-device feedback remains pending.
