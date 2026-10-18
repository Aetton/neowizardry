# Iteration 055 — native beach entrance and first placed encounter

New games now use the original `MO100` entrance and its saved axis/angle facing. The old list-order fallback selected `MO101` inside the monastery. User clarification: the initial **party spawn** belongs on the right side of the beach; this was not a request to move the first enemy there. Loading a campaign restores its saved position/facing rather than reapplying the new-game entrance.

## Source evidence

Reference: wizardry-8-decomp `075cbd0b853c4670e94f6ae4fe48d53b52b5e819`.

- `NPC Manager.cpp::ChooseNewGameStartLocation`: new game uses level 8, entrance 0.
- `Levels.cpp::LoadLevel`: level code plus two-digit entrance resolves `MO100`; positioned trigger supplies position and facing.
- `chunk.cpp`: disk headers are ten packed bytes, unlike the twelve-byte in-memory header. Shipped STS uses a zero grouped flag even though native callers explicitly open and read groups.
- `LoadSaveGame.cpp::LoadItemStatus/LoadMonsterGroup/LoadMonster`: counts, sized group/info records, scripts, unborn flag, navigation and versioned patrol/facing tails.

Read-only runner intake found 13 groups / 28 placed monsters on floor 1 and 12 / 28 on floor 2. The first floor's closest placed nonfriendly group to the beach entrance is group 102: three Crab definitions (253), location IDs 236/237/238, HP 9/8/14, with neutral disposition and no scripts. Source positions and HP are retained. No enemy position is invented relative to the player entrance. Other groups, generators and scripted encounters are not activated by this slice.

## Player behavior and persistence

Ordinary player mode replaces the authored Dusk Bat encounter with these three original crab representations at their STS positions. E/click near the group explicitly attacks it; initial neutrality is retained in the encounter data. There is no claim of recovered native continuous world AI, pursuit or hostility policy. The existing tactical battle grid and effective party facts remain authored.

All three actors use the shared combat executor. Victory requires every enemy to die; selecting the second/third enemy targets that actor. HP, actor records, initiative/RNG, inventory, world return point and reward receipt persist together. Native group identity and the source manifest are validated against the loaded content, including STS/PVL hashes, source location IDs, species and maximum HP. The original crab definitions have no enabled item/gold rewards. XP is summed across the group, then divided once among eligible survivors; repeat victory/reload cannot award again.

Expedition schema 3 adds a bound native placement snapshot. Legacy authored research saves retain schemas 1/2. Adding a native encounter changes the content hash and campaign path; previous authored campaign files are retained, not silently converted into a different roster. After updating an older installation profile, select the original installation once to prepare the additional content. New profiles remember the original installation for subsequent content updates.

Prepared content includes a private `.nwencounter.json`; original game bytes and derived models remain local. Preparation can seed model caches from previous private sets when source fingerprints match, with independent exporter mesh-hash validation. Cached GLBs are hardlinked when possible; all exporter writes replace files atomically, preserving older sets.

## Verification

Local: 79 Python tests; 4213 core/input checks including 451 expedition checks; client build with zero warnings/errors. The headless ordinary-start fixture checks named entrance/facing, independent save/resume, bestiary return, battle UI, victory, receipt and no respawn. The prior movement/folder checks remain enabled.

Linux, original-data audit and the packaged Windows EXE additionally exercise the production startup/encounter path. Actual original-data results and release are recorded in Linear after CI. A fixture pass alone does not establish the original crab encounter or a complete monastery campaign.
