# Iteration 009 — crafting and magic effect plumbing

Delivered two atomic crafting recipes with skill/knowledge gates and a Craft window, plus three synthetic effect spells and multi-caster planning. The five-item public fixture supports herbs/vial → tonic → tempered staff → equip/save/use. Recipe execution conserves quantities; unavailable ingredients or actor state cannot consume items, and equipped copies are excluded.

Status effects cover damage reduction, an action skip and periodic damage. Their remaining durations survive saves. The shared queue accepts one casting order per actor and validates a whole plan atomically. Other actors retain their physical attacks. Synthetic rule choices and the boundaries of original reconstruction are documented in [crafting](crafting.md) and [magic](magic.md).

The existing save schema remains readable: missing recipe knowledge, skills and effects remain absent. Existing spell ownership is preserved; new parties receive the expanded synthetic spellbook. Full original catalogs never receive fixture recipe mappings.

## Validation

- 236 C# checks including mobile gestures: quantities, equipped ingredient exclusion, skill threshold, solo/1–8 parties, overflow/rejections, status timing/expiry, multi-caster ordering, periodic victory, save continuation and old-save defaults.
- 12 Python archive/overlay tests passed.
- Client built without warnings/errors; headless gameplay and mobile smoke passed. Client smoke now crafts, equips and reloads a weapon, executes a multi-caster effect round and checks continuation after save/load.
- Windows exported build and publication: pending verification before completion.

## Research boundary

The next meaningful content work requires original data/behavior, rather than more synthetic rules: spell definitions and effect codes; ordinary/alchemy/gadget/NPC/quest recipe sources and restrictions; actor progression/class/skill records; monastery geometry/collision, encounter and dialogue/event data. Unknown fields must retain raw values/provenance. Full tables and assets stay outside public git. No original spells, recipes, balance or monastery geometry are claimed by this iteration.
