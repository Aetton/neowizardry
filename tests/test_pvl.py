import json
import math
from pathlib import Path
import struct
import tempfile
import unittest

from tools.pvl import FormatError, export, glb, parse, summary
from test_slf import fixture as slf_fixture


def fixture(*, kind=4, sunlight=0x100, vertex_materials=-1, uv_index=0, vertex_index=0, material_index=0):
    raw = bytearray(struct.pack('<iihB', 1, 22, 1, kind))
    material = bytearray(297 if kind == 4 else 281)
    material[:5] = b'stone'
    material[40:49] = b'wall.tga\0'
    raw.extend(material)
    raw.extend(struct.pack('<i', -1))
    raw.extend(struct.pack('<iIiI', sunlight, 4, 5, 2))
    raw.extend(b'\0' * 8)
    raw.extend(struct.pack('<i', vertex_materials))
    for p in ((0, 0, 0), (1000, 0, 0), (0, 0, 1000), (1000, 0, 1000)):
        raw.extend(struct.pack('<fff', *p))
    for uv in ((0, 0), (1, 0), (0, 1), (1, 1), (0.5, 0.5)):
        raw.extend(struct.pack('<ff', *uv))
    if vertex_materials < 0:
        raw.extend(b'\xff' * 16)  # Marker-like bytes inside a section must not be scanned.
    raw.extend(struct.pack('<IIIIII', uv_index, 1, 2, 4, 1, 3))
    raw.extend(struct.pack('<IIIIII', vertex_index, 1, 2, 2, 1, 3))
    raw.extend(struct.pack('<ii', material_index, 0))
    raw.extend(struct.pack('<fff', 0, 1, 0) * 4)
    raw.extend(b'\0' * (4 * 12 + 2 * 16))
    if sunlight & 0xff00:
        raw.extend(b'\0' * 16)
    raw.extend(struct.pack('<ii', -1, -1))
    raw.extend(b'UNPARSED_AUTHORED_TAIL')
    return bytes(raw)


class PvlTest(unittest.TestCase):
    def test_conditional_sections_and_tail_boundary(self):
        for kind in (0, 4):
            for sunlight in (0, 0x100):
                for vertex_materials in (0, -1):
                    data = fixture(kind=kind, sunlight=sunlight, vertex_materials=vertex_materials)
                    level = parse(data)
                    self.assertEqual(len(level.meshes[0].indices), 2)
                    self.assertEqual(level.materials[0]['textures'][0], 'wall.tga')
                    self.assertEqual(data[level.geometry_end:], b'UNPARSED_AUTHORED_TAIL')
                    preview = summary(level, data, 'authored')['preview']
                    self.assertFalse(preview['collision'])
                    self.assertEqual(preview['render_bounds'], [[0, 0, -1.3], [1.3, 0, 0]])

    def test_rejects_indices_and_nonfinite_geometry(self):
        for options in ({'uv_index': 5}, {'vertex_index': 4}, {'material_index': 1}):
            with self.assertRaises(FormatError):
                parse(fixture(**options))
        data = bytearray(fixture())
        struct.pack_into('<f', data, 10 + 298 + 4 + 28, math.nan)
        with self.assertRaises(FormatError):
            parse(data)

    def test_rejects_truncation_counts_and_markers(self):
        data = fixture()
        for cut in (0, 9, 100, len(data) - len(b'UNPARSED_AUTHORED_TAIL') - 1):
            with self.assertRaises(FormatError):
                parse(data[:cut])
        bad = bytearray(data)
        struct.pack_into('<i', bad, 10 + 298, 0)
        with self.assertRaises(FormatError):
            parse(bad)
        struct.pack_into('<i', bad, 0, -1)
        with self.assertRaises(FormatError):
            parse(bad)

    def test_glb_preserves_uv_seams_and_triangle_count(self):
        payload = glb(parse(fixture()))
        magic, version, total = struct.unpack_from('<III', payload)
        self.assertEqual((magic, version, total), (0x46546c67, 2, len(payload)))
        json_size, kind = struct.unpack_from('<II', payload, 12)
        self.assertEqual(kind, 0x4e4f534a)
        doc = json.loads(payload[20:20+json_size])
        primitive = doc['meshes'][0]['primitives'][0]
        self.assertEqual(doc['accessors'][primitive['attributes']['POSITION']]['count'], 6)
        self.assertEqual(doc['accessors'][primitive['attributes']['TEXCOORD_0']]['count'], 6)
        self.assertNotIn('images', doc)
        offset = 20 + json_size
        binary_size, binary_type = struct.unpack_from('<II', payload, offset)
        self.assertEqual(binary_type, 0x004e4942)
        self.assertEqual(binary_size, doc['buffers'][0]['byteLength'])
        uv_view = doc['bufferViews'][doc['accessors'][primitive['attributes']['TEXCOORD_0']]['bufferView']]
        uvs = list(struct.iter_unpack('<ff', payload[offset+8+uv_view['byteOffset']:offset+8+uv_view['byteOffset']+uv_view['byteLength']]))
        self.assertEqual(uvs, [(0, 0), (0, 1), (1, 0), (0.5, 0.5), (1, 1), (1, 0)])

    def test_both_levels_must_parse_before_private_export(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / 'LEVELS.SLF'
            out = root / 'private'
            report = root / 'report.json'
            slf_fixture(archive, [('Monastery/Monastery1.PVL', fixture(), 0),
                                  ('Monastery/Monastery2.PVL', b'bad', 0)])
            with self.assertRaises(FormatError):
                export(archive, out, report)
            self.assertFalse(out.exists())
            self.assertFalse(report.exists())

    def test_successful_export_is_outside_original_and_preserves_source(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / 'Levels').mkdir()
            archive = root / 'Levels/LEVELS.SLF'
            slf_fixture(archive, [('Monastery/Monastery1.PVL', fixture(), 0),
                                  ('Monastery/Monastery2.PVL', fixture(kind=0), 0)])
            original = archive.read_bytes()
            with self.assertRaises(FormatError):
                export(archive, root / 'Levels/preview', root / 'report.json')
            self.assertFalse((root / 'Levels/preview').exists())
            export(archive, root / 'generated', root / 'report.json')
            self.assertEqual(archive.read_bytes(), original)
            self.assertEqual(len(json.loads((root / 'report.json').read_text())['levels']), 2)
            self.assertTrue((root / 'generated/monastery2.glb').is_file())
