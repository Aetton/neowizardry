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
