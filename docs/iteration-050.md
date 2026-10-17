# Iteration 050 — native expedition XP and reward persistence

New expeditions award the effective original MONSTERS.DBS experience after victory. Definition 282, Dusk Bat, carries **794 XP**, no override and no enabled treasure slots or gold dice. Solo receives 794; two eligible heroes receive 397 each; three receive 264 each (the remainder is discarded); eight receive 99 each. This changes the connected expedition only. Standalone monster laboratories retain their zero-scenario-reward contract and authored training encounters retain their existing rewards.

Evidence is pinned to [decomp revision 075cbd0](https://github.com/agluszak/wizardry-8-decomp/tree/075cbd0b853c4670e94f6ae4fe48d53b52b5e819): MonsterManager uses the nonzero +0x26b override, otherwise +0x181; Combat divides the pool by CountActiveCharacters before AwardPartyExperience; Targeting CanPartySlotParticipate accepts occupied living characters below DEAD, including condition 16; GameplayCode AwardPartyExperience updates each eligible character. Captivity remains an explicit authored expedition exclusion. There are no summoned, friendly, NPC or scripted bonus actors in this encounter.

ItemManager DropMonsterLoot gates each of eight entries by its nonzero signed count, then compares Random(100) **<=** chance, rolls a drop count and dispatches direct items/table generation; gold uses a separate dice expression. Those nonempty branches and NPC rewards are **not implemented** here. New expeditions reject these records with named blockers instead of silently dropping loot. Dusk Bat's empty result is supported and leaves every owned item and the item identity sequence unchanged. No generic currency, ground-item pickup or item-table resolver is claimed.

## State and UI

Expedition schema 2 adds an immutable receipt containing initial per-hero XP and the grant flag. Victory commits the authoritative GameState XP, phase and receipt together. Validation checks exact eligibility/division against the currently bound monster record, initial XP and receipt. Mid-battle saves preserve the same result; reloading victory cannot grant again or respawn. The reward adds no RNG draws. The current signed Hero.Xp representation supports values through int.MaxValue; wider native record rewards are rejected before battle, and arithmetic overflow cannot silently wrap.

Schema-1 expedition saves remain readable, including JSON without a receipt field. They retain their original zero-native-reward contract even if loaded mid-battle. Cleared older saves receive no retroactive reward. A new expedition is required for native rewards.

The HUD shows each hero's earned XP and the original profession's first-level goal: Fighter/Rogue/Gadgeteer/Bard 1000, Priest/Alchemist/Psionic/Mage 1200, Lord/Valkyrie/Ranger/Samurai/Monk 1400, Ninja/Bishop 1600 (GameplayCode CalcXPGoal, level 1). This is a displayed milestone, not a claim that the authored training combat facts derive from a level-1 character. Actual level-up choices, stat/HP/skill growth, later goals, class history and effective stat production remain open. Existing combat facts are unchanged.

## Validation

- Portable checks cover native base/override rewards for solo through eight heroes, truncation, exact save continuation, receipt corruption, no repeat awards, preserved inventory, previously accumulated XP and old-save compatibility.
- A living hero with condition 16 shares XP; dead and captive heroes do not. Unsupported NPC/direct/table/gold branches fail closed.
- 63 Python tests pass; the client builds with zero warnings/errors.
- Local Godot loads the original Dusk Bat's five clips/156 frames, wins the authored fixture encounter, verifies 794 XP and exact terminal reload, and preserves the world return point.
- Original monastery runner and Windows publication checks are pending at implementation commit time.

No original assets or derived catalogs are committed or bundled in public releases.
