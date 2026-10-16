import hashlib
from pathlib import Path
import tempfile
import unittest

from tools.world_audit import audit
from tools.slf import FormatError
from test_slf import fixture


class WorldAuditTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.archive = self.root / 'LEVELS.SLF'

    def populate(self):
        fixture(self.archive, [('MONASTERY\\Monastery1.PVL', b'authored-one', 0),
                               ('Monastery/Monastery2.PVL', b'authored-two', 0),
                               ('Monastery/material.bin', b'authored-material', 0),
                               ('Swamp/Swamp.PVL', b'unrelated', 0)])

    def test_scoped_inventory_and_provenance(self):
        self.populate()
        result = audit(self.archive)
        self.assertEqual(result['missing_required'], [])
        self.assertEqual(result['active_entries'], 4)
        self.assertEqual(len(result['monastery']), 3)
        self.assertEqual(result['sha256'], hashlib.sha256(self.archive.read_bytes()).hexdigest())
        self.assertEqual(result['monastery'][1]['sha256'], hashlib.sha256(b'authored-one').hexdigest())
        self.assertFalse(result['geometry_decoded'])
        self.assertNotIn(str(self.root), str(result))

    def test_missing_or_inactive_required_level(self):
        fixture(self.archive, [('Monastery/Monastery1.PVL', b'authored', 0),
                               ('Monastery/Monastery2.PVL', b'old', 1)])
        result = audit(self.archive)
        self.assertEqual(result['missing_required'], ['monastery/monastery2.pvl'])
        output = self.root / 'private'
        with self.assertRaises(FormatError):
            audit(self.archive, output)
        self.assertFalse(output.exists())

    def test_private_extraction_matches_manifest_and_refuses_overwrite(self):
        self.populate()
        output = self.root / 'private'
        result = audit(self.archive, output)
        for entry in result['monastery']:
            data = (output / entry['name']).read_bytes()
            self.assertEqual(hashlib.sha256(data).hexdigest(), entry['sha256'])
        self.assertFalse((output / 'Swamp').exists())
        with self.assertRaises(FormatError):
            audit(self.archive, output)

    def test_rejects_corrupt_archive(self):
        self.archive.write_bytes(b'not an archive')
        with self.assertRaises(FormatError):
            audit(self.archive)
