# Formula research

[peddroelm: Game Mechanics Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=899973393), updated 2023-12-05. The author reports direct experiments; other passages reproduce Silicoid's disassembly findings. Original Kamrad posts were inaccessible during this session.

`LegacyResearch.SingleClassAttackScore` implements the section 5.4 model. Preconditions: white encumbrance, single class, no dual wielding or shield. It is **not used in combat** and has not been checked against the executable. Tests expose a discrepancy: published decimal coefficients yield 599.9999 at a reported level-25 breakpoint; exact fractions yield 600. Threshold comparison needs evidence.

Keep the published decimal coefficients. Exact threshold equality, intermediate rounding, multiclass accumulation and altered installations need independent evidence. Section 5.6 leaves queue details unresolved; Power Cast passages also contain conflicting conclusions. Do not turn those gaps into implicit design decisions.

Iteration 017 independently traced the supplied executable: the covered free-other-hand character branch uses integer arithmetic and inclusive thresholds, resolving the level-25 discrepancy. `Rules/OriginalCombatRules` supersedes the decimal candidate inside that documented scope; `LegacyResearch` remains a faithful copy of the published source model, not a combat dependency. See [attack-budget evidence](original-combat-formulas.md).

The same static pass recovered ordinary player casting-risk arithmetic, failure/backfire boundaries, Power Cast's integer bonus, a base strength helper and common resistance reduction. See [magic arithmetic evidence](original-magic-formulas.md). The guide is a map of questions, not the authority for unresolved rounding and eligibility. Actor resolution, contextual modifiers, complete effects, physical hit/penetration and original-game comparison fixtures remain required before live integration.
