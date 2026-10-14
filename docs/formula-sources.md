# Formula research

[peddroelm: Game Mechanics Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=899973393), updated 2023-12-05. The author reports direct experiments; other passages reproduce Silicoid's disassembly findings. Original Kamrad posts were inaccessible during this session.

`LegacyResearch.SingleClassAttackScore` implements the section 5.4 model. Preconditions: white encumbrance, single class, no dual wielding or shield. It is **not used in combat** and has not been checked against the executable. Tests expose a discrepancy: published decimal coefficients yield 599.9999 at a reported level-25 breakpoint; exact fractions yield 600. Threshold comparison needs evidence.

Keep the published decimal coefficients. Exact threshold equality, intermediate rounding, multiclass accumulation and altered installations need independent evidence. Section 5.6 leaves queue details unresolved; Power Cast passages also contain conflicting conclusions. Do not turn those gaps into implicit design decisions.

Next: collect original-save test fixtures immediately below, at and above attack thresholds; compare observed attack counts. Then investigate hand-specific penalties and the initiative queue.
