# Iteration 043 — original monastery textures and IFL animation

The static monastery viewer now uses original texture references and UVs. The private exporter embeds lossless PNG images in each GLB; the viewer keeps imported materials and advances IFL frames at each material's PVL FPS. Original textures and generated GLBs remain beside the original installation on the runner, outside public git, artifacts and releases.

## Import and rendering

`python -m tools.pvl /path/Wizardry8/Levels/LEVELS.SLF --out local/world --report local/world-report.json --textures`

The existing Actions world workflow now supplies `--textures`. Texture references resolve case-insensitively under the level's `BITMAPS` directory, matching the inspected original editor. The first nonempty material texture slot is selected; `NoTexture` means an untextured material. IFL lines contain relative TGA filenames. Missing resources, unsafe paths, corrupt images and unsupported animation rates fail before either preview is overwritten; there are no silent gray texture fallbacks.

The standard-library TGA decoder supports ordinary and RLE true-color, indexed and grayscale images; 15/16/24/32-bit colors; palettes with nonzero first indices; and both horizontal and vertical origins. Images are normalized to top-left RGBA before PNG encoding. All stored 32-bit alpha bytes are retained even if the descriptor omits its alpha-bit count, matching Godot's TGA reader. Bounds checks reject truncated payloads, invalid palettes, overflowing RLE packets, excessive dimensions and interleaving. Reference cross-check: [Godot 4.5.1 TGA loader](https://github.com/godotengine/godot/blob/4.5.1-stable/modules/tga/image_loader_tga.cpp). This is an independent implementation, not copied code.

Source UVs stay unchanged. glTF textures repeat in both directions and use linear filtering/mipmaps. Pixel alpha selects opaque, cutout or blended rendering; a fractional native material opacity uses blending. A native opacity of zero enters a special translucent route in the editor; the preview keeps pixel alpha with normal blending and factor one instead of making the entire material invisible. Its native blend/water/reflection semantics remain unresolved. The fourth texture slot also maps to emission. Native texture color modulation, additive/glow behavior, exact transparency/sorting, original lighting, water reflections and original material parity are not claimed. Untextured diffuse/transmit values are retained with bounded colors.

IFL frame indices, material FPS and emission routing travel in private GLB extras. The NeoWizardry viewer consumes these extras; generic glTF viewers display the first frame. Each animation shares a deterministic elapsed-time clock and loops by frame count. Runtime frames use the imported images, with no external image files or file-path dependency after GLB generation.

## Verification

Six new tests cover TGA origin/channel order, PNG decompression and CRCs, RLE raw/repeat packets, palette/16-bit/grayscale decoding, malformed data and IFL path rejection, and GLB frame/material/alpha/FPS association. The full Python suite has 48 tests. The client builds with zero warnings/errors.

Original-resource Actions additionally verifies unchanged triangle counts and bounds, the expected number of textured surfaces and animation materials, every embedded PNG's dimensions and decoded base-level RGBA hash, and frame advance/loop behavior. Mipmap data is excluded from pixel hashes. These checks validate decoding and material loading; they do not substitute for comparison with the original game's rendered lighting/materials.

The same `--world .../monastery1.glb` command opens the textured preview once Actions has regenerated the private GLBs. Collision, native spawn points, doors, dynamic actors and exploration/combat persistence remain open.

## Original texture intake

The first original conversion run [38969584933](https://github.com/Aetton/neowizardry/actions/runs/38969584933) resolved all selected material textures and IFL frames and generated both private GLBs. Monastery1 embeds 254 images with 6 animated materials across 1373 textured surfaces; Monastery2 embeds 203 images with 6 animated materials across 731 textured surfaces. These are per-GLB image counts, not globally unique source-texture totals. All selected original images use uncompressed TGA type 2 with 24- or 32-bit pixels. Source alpha analysis found 18 nonopaque images in the first GLB and 30 in the second.

The initial runtime check stopped at numeric metadata parsing: Godot's Variant JSON roundtrip emits integral values as floating-point literals. The loader now accepts finite, nonnegative whole numeric values while rejecting fractional and out-of-range dimensions/indices. This fixes metadata reading without relaxing pixel-hash validation.

## Accepted runtime and release

[Original-resource run 38970134609](https://github.com/Aetton/neowizardry/actions/runs/38970134609) passed at commit `37b6904207d728a993bb48300124a83584a9ebc6`. Both runtime processes verified every embedded image's dimensions and decoded RGBA hash, all 2104 textured surfaces, all 12 animation-material cycles, and the existing triangle/bounds checks. The first level has 1373 textured surfaces / 6 animations, and the second 731 / 6. Missing image resources, pixel mismatches, incorrect counts, failed loops and runtime engine errors fail this check.

[Linux CI 38970134444](https://github.com/Aetton/neowizardry/actions/runs/38970134444) and [Windows build 38970134397](https://github.com/Aetton/neowizardry/actions/runs/38970134397) also passed. [Windows preview 53](https://github.com/Aetton/neowizardry/releases/tag/windows-build-53) contains the verified loader. Update the client as well as the private GLBs: earlier geometry-only clients override the imported materials with their gray debug surface.

The headless editor-import process still logs the previously recorded EditorSettings shutdown diagnostic and exits zero. The separate world-viewer runtime processes passed with no engine errors. This validates asset decoding, assignment and animation plumbing; a pixel-for-pixel comparison with original-game screenshots remains outside this acceptance scope.
