# Iteration 044 — processed OCT physical surfaces

The next world milestone uses physical geometry from the OCT GameData bank, not visible PVL triangles or material-name collision heuristics. The static scope excludes conditional/switch-controlled surfaces, prop geometry, crossing/environment boundaries and inactive records. Dynamic doors, triggers and environmental movement responses remain open.

## Format evidence

Research sources are the independent public reconstruction [wizardry-8-decomp](https://github.com/agluszak/wizardry-8-decomp/tree/075cbd0b853c4670e94f6ae4fe48d53b52b5e819): Octree.cpp, OctPreTree.h, OctPath.cpp, GDFileIO.cpp and geometry.h. Reconstructed code was read as layout evidence; none is copied. Local original executable SHA-256 remains `18a74ff61c65b8a2d4cfa11ffce82ad7fef022a94eaf0c2f217e479e981420d2`.

The local executable confirms the OCT header's 0xf5 read at 0x42bcf5 and processed GameData header's 0x68 read at 0x44926f. The physical surface reader at 0x449460–0x44947b multiplies the surface count by 76. These match the reconstructed layouts. Further physical behavior is treated separately from the layout evidence.

OCT version 0x22 contains count-sized branches (36 bytes), leaves (40), leaf polygon and GD streams, optional small leaf grid, polygon lookup, region/trigger streams and region volumes (232). After the sentinel come submesh records (16), framed compressed bit arrays and optional particle/prop links; path cells (8) and conditional path arrays; optional prop sun bits; and another sentinel. The reader skips compressed bit-array payloads by their explicit lengths and validates both DEADD00D framing words; it does not decode or use those masks as movement geometry.

Processed GameData v1 has a 104-byte header, 12-byte vertices, 76-byte physical surfaces, interface/state slices (12 bytes each), conditional surface indices and 68-byte environment records. Exact sentinels and end-of-file, counts, surface/vertex indices, conditional slices and finite physical positions/planes are checked. The spatial/path/environment contents are not fully reconstructed merely because their serialized lengths are consumed.

The native surface test at 0x41cf90 skips flags 0x18 and treats 0x1080 as special crossing/environment surfaces. The exporter additionally excludes all conditional-bank entries and prop geometry (0x800), since their state owners are not connected. Walkable flags (4), source surface IDs and plane normals are preserved in the private collision file. No native movement-solver parity is claimed.

`python -m tools.oct /path/Wizardry8/Levels/LEVELS.SLF --out local/world --report local/collision-report.json` reads both originals before writing. The NWCOL001 sidecar contains PVL/OCT provenance hashes and static triangles using the same `(x,y,-z) * 0.0013` preview convention. Original-derived collision bytes remain private beside the original installation; only the metadata report is uploaded by Actions.

Four initial synthetic tests cover exact layout/end boundaries, physical selection, binary provenance/coordinates and malformed inputs. Original-resource findings and controller validation follow below.
