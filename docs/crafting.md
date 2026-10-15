# Crafting fixture

The public test catalog now contains five synthetic items. **Craft** opens two test recipes:

| Recipe | Ingredients in shared bag | Result | Requirement |
|---|---|---|---|
| Test tonic | 2 Test herbs + 1 Test vial | 1 Test tonic | Learned recipe, living/free crafter |
| Tempered test staff | 1 Restricted test staff + 1 Test tonic | 1 Tempered test staff | Learned recipe, test-craft skill ≥20 |

New expeditions using the synthetic catalog receive a staff, four herbs, two vials, both recipe IDs and test-craft 25 on every hero. This is a fixture, not original starting equipment, Alchemy or Engineering progression. The tonic is an intermediate ingredient; drinking it is not implemented. The tempered staff can be equipped and used by the same race/class restrictions as the original test staff.

Each click makes one batch. Core validation runs before changes. Ingredients are taken only from the bag; equipped copies cannot disappear into a recipe. The result is added after exact ingredient consumption. Missing ingredients, unavailable actors, unknown/unlearned recipes, insufficient skill and output overflow reject the operation without changing the source state, RNG or round. Crafting does not advance a round in this fixture.

Skills and learned recipe IDs are saved explicitly. Old saves do not receive them retroactively; start a new party to use the fixture. Imported original catalogs do not use synthetic recipe IDs. Their Craft button remains unavailable: native combination references can now be exported for inspection, but are not yet executable recipes. See [recipe format](recipe-format.md).

## Evidence gate for original crafting

Do not turn these recipes or gates into Wizardry 8 rules. Establish separately which original operations use ordinary combinations, Alchemy, Engineering/gadgets, NPC crafting and quest scripting. For each representative recipe record source provenance, ingredient/result IDs and counts, actor/skill/class requirements, learning/discovery, failure behavior, charges/item-instance handling and whether time advances. Some requirements may live in executable logic or scripts rather than a single database. Compare observed original-game results before calling a rule canonical.
