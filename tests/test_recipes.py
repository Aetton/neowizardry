import hashlib
from pathlib import Path
import struct
import tempfile
import unittest

from tools.recipes import recipe_catalog
from tools.slf import Archive, Resources, FormatError
from test_slf import fixture


def record(name, pair=(-1, -1), skill=255, threshold=0):
    r = bytearray(269)
    text = name.encode('utf-16le')
    r[:len(text)] = text
    struct.pack_into('<ii', r, 0xb9, *pair)
    r[0xc9:0xcb] = bytes((skill, threshold))
    return bytes(r)


def table(*records):
    return struct.pack('<I', len(records)) + b''.join(records)


class RecipeTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def catalog(self, data, patched=None):
        base = self.root/'DATA.SLF'
        fixture(base, [('DATABASES/ITEMS.DBS', data, 0)])
        patches = []
        if patched is not None:
            patch = self.root/'PATCH.001'
            fixture(patch, [('DATABASES/ITEMS.DBS', patched, 0)])
            patches.append(Archive(patch))
        return recipe_catalog(Resources(Archive(base), patches))

    def test_sources_repeated_ingredients_and_raw_evidence(self):
        r = record('Fixture result', (0, 0), 23, 35)
        data = table(record('Fixture part'), r, record('Inactive', (-2, -2)))
        c = self.catalog(table(record('Old')), data)
        self.assertEqual(c['provenance']['file'], 'PATCH.001')
        self.assertEqual(c['mode'], 'reference_only')
        self.assertEqual(c['inactive_pairs'], {'-1': 1, '-2': 1})
        s = c['recipes'][0]
        self.assertEqual(s['ingredient_ids'], [0, 0])
        self.assertEqual(s['ingredient_names'], ['Fixture part']*2)
        self.assertEqual((s['output_id'], s['skill_code'], s['skill_threshold']), (1, 23, 35))
        self.assertEqual(bytes.fromhex(s['raw_record_hex']), r)
        self.assertEqual(s['record_sha256'], hashlib.sha256(r).hexdigest())
        self.assertEqual(s['issues'], [])

    def test_anomalies_are_retained_and_flagged(self):
        c = self.catalog(table(record('Part'), record('', (0, 0), 0),
                               record('Bad', (-1, 99)), record('Unknown skill', (0, 0), 254)))
        self.assertEqual(c['count'], 3)
        self.assertEqual(c['recipes'][0]['issues'], ['unnamed_output'])
        self.assertEqual(c['recipes'][1]['ingredient_names'], [None, None])
        self.assertIn('ingredient_out_of_range', c['recipes'][1]['issues'])
        self.assertEqual(c['recipes'][2]['skill_code'], 254)

    def test_zero_threshold_and_duplicate_pairs_not_discarded(self):
        c = self.catalog(table(record('Part'), record('First', (0, 0), 0),
                               record('Second', (0, 0), 255)))
        self.assertEqual([r['output_id'] for r in c['recipes']], [1, 2])
        self.assertEqual([r['skill_threshold'] for r in c['recipes']], [0, 0])

    def test_wrong_layout_rejected(self):
        valid = table(record('Part'))
        for data in [b'', valid[:-1], valid+b'x', struct.pack('<I', 2)+valid[4:]]:
            with self.subTest(size=len(data)), self.assertRaises(FormatError):
                self.catalog(data)


if __name__ == '__main__':
    unittest.main()
