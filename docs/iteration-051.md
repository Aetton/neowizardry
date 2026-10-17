# Iteration 051 — catalog-wide original creature representation

The private importer now visits **all 595 MONSTERS.DBS definitions**, groups their cycle names case-insensitively and exports shared geometry plus per-definition binding manifests. The supplied DATA.SLF + MONSTERS.SLF produce **582 renderable definitions**, from **193 available model/script sets** (173 distinct GLB payloads), with **2,136 clips/variants, 66,242 unique frames and 28,436,059 frame triangles** counted across script sets. The 13 blocked definitions remain explicit in the index; no unrelated model substitutes for a missing original.

## Common import and playback

- All 27 native cycle families and numbered/repeated subcycles are retained in source order, including spice/talk/spell, different attack modes and deaths. Shared MON data uses the same frame nodes across clips; rate and variant metadata remain separate.
- MLS default SKINSWAP tables are applied in order. Damage stages derive from the default table, matching AddDamageStage/ReplaceDamageStageTexture rather than cascading the previous damaged stage. PNGs for all stages are embedded; IFL skin animation uses its material rate. Opacity/glow receive explicit preview material handling; this is not native shader parity.
- MON particle records (versions 1–4) are bounded and read past correctly so the real underlying geometry/animation is available. **Emitters themselves remain unrendered**, recorded on each affected clip and in the gallery. No particle effect is fabricated. This matters for elementals and gib/death/spell effects.
- Manifest schema 2 supports variants, shared hash-named GLBs and damage stages. Schema-1 assets remain readable. Exact monster/database/record binding, GLB hashes, frame bounds, ownership and decoded image pixels are checked. Gallery checks distinguish clip configuration even when the GLB payload is shared.
- The connected expedition uses health-derived skin stages and plays the first available original death subcycle, holding its final frame. Loading a cleared encounter restores its final cosmetic death pose. These cosmetic choices never advance gameplay RNG or change HP/items/saves. The source damage-stage mapping is `(maxHP - HP) * stages / maxHP`, capped to the final stage.

Evidence is pinned to [decomp revision 075cbd0](https://github.com/agluszak/wizardry-8-decomp/tree/075cbd0b853c4670e94f6ae4fe48d53b52b5e819): Monster ReadOrCloneMonsterCycles/ParseMonsterCycleName/ReplaceSkinTexture, stModelInstance AddDamageStage/ReplaceDamageStageTexture, GrCycle ReadGrCycleData, ReadLevel ReadWorldParticles, AnimObjReadFromFile, MonsterManager damage appearance and AniMesh frame bounds. Parsing and conversion are independent implementations.

## Export and open

```powershell
python -m tools.creatures "C:/Wizardry8/Data/DATA.SLF" "C:/Wizardry8/Data/Monsters/MONSTERS.SLF" --all --loose-dir "C:/Wizardry8/Data" --out "C:/NeoWizardryWorld" --report "C:/NeoWizardryWorld/creatures-report.json"
NeoWizardry.exe --creatures "C:/NeoWizardryWorld/creatures-index.json"
```

Include each enabled `--patch` as for existing catalog import. For a source checkout use `godot --path client -- --creatures <absolute index path>`. The browser searches all definitions by name/ID, selects every exported clip/variant, loops animations and switches default/damage skins. Blocked records and clips with omitted emitters are labeled. It is independent of combat capability: rendering a creature does not admit its spells/statuses/AI to gameplay. The expedition still uses the authored Dusk Bat encounter; native spawning and all-monster battle coverage are separate workstreams.

Outputs remain private beside the imported world. Shared GLBs prevent hundreds of duplicate record-specific meshes. The batch cache hashes source archives/patches/relevant loose files and exporter/parser code; cached GLB content is checked before reuse. Missing/corrupted caches rebuild. The first full export is substantial (about 2.1 GB counted across script sets); subsequent unchanged imports reuse verified files. No originals or derived models/catalogs are in the public repository or Windows package. The original-data workflow exports the private catalog and runs the complete gallery smoke on the self-hosted machine.

## Remaining definitions and limits

- IDs **361–371** refer to `death06`–`death16`, whose MLS and fallback MON resources are absent from the supplied archives.
- IDs **396 and 436** use Bela. Source cycle endpoints exceed actual frame counts (including IDLE); native GetAniMeshFrame also rejects out-of-range frames. These remain blocked instead of silently clamping the source data.
- 342 renderable definitions include at least one omitted particle attachment. Geometry availability is not full effect fidelity. Sound/shake events, native event-frame combat timing, particle emitters, random size/hover/bob, interpolation and original lighting/blending remain open.
- Per-actor cosmetic subcycle selection is deterministic first-available; original random choice and event semantics are not claimed. Imported spell animations do not implement spell mechanics.

## Validation

67 Python tests cover variants, geometry reuse, default/damage/IFL skins, bounded particle prefixes, missing optional clips, full index accounting, record-specific shared bindings and cache-corruption rebuilding. Portable gameplay/input checks remain 4,196. The client builds without warnings/errors. Synthetic Godot checks exercise texture stages/IFL, all clip variants, death holding and the expedition save/reward loop. Full original-data gallery and CI publication validation are recorded after completion.
