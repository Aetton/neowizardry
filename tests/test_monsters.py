import hashlib
from pathlib import Path
import struct
import tempfile
import unittest
from tests.test_slf import fixture
from tools.slf import Archive, Resources
from tools.monsters import monster_catalog
from tools.errors import FormatError

class MonsterTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name)
        row=bytearray(663)
        for offset,text in zip((0,48,96,144),('Fixture# internal','Fixtures','Creature','Creatures')):
            row[offset:offset+len(text)*2]=text.encode('utf-16le')
        self.row=bytes(row);self.data=struct.pack('<I',1)+self.row
    def read(self,data):
        path=self.root/'DATA.SLF';fixture(path,[('DATABASES/MONSTERS.DBS',data,0)])
        return monster_catalog(Resources(Archive(path)))
    def test_preserves_record_and_table_identity(self):
        c=self.read(self.data);r=c['monsters'][0]
        self.assertEqual(c['count'],1);self.assertEqual(c['record_size'],663)
        self.assertEqual(r['label'],'Fixture# internal');self.assertEqual(r['text_slots'][1],'Fixtures')
        self.assertEqual(bytes.fromhex(r['raw_record_hex']),self.row)
        self.assertEqual(r['record_sha256'],hashlib.sha256(self.row).hexdigest())
        self.assertEqual(c['database_sha256'],hashlib.sha256(self.data).hexdigest())
    def test_rejects_truncation_trailing_bytes_and_bad_count(self):
        for data in (b'',self.data[:-1],self.data+b'0',struct.pack('<I',2)+self.row,struct.pack('<I',0)):
            with self.subTest(size=len(data)),self.assertRaises(FormatError):self.read(data)
    def test_requires_terminated_utf16_slots(self):
        row=bytearray(self.row);row[:48]='x'.encode('utf-16le')*24
        with self.assertRaises(FormatError):self.read(struct.pack('<I',1)+row)
    def test_rejects_malformed_utf16(self):
        row=bytearray(self.row);row[:2]=b'\x00\xd8'
        with self.assertRaises(UnicodeError):self.read(struct.pack('<I',1)+row)
    def test_uses_overlay_resolution(self):
        base=self.root/'DATA.SLF';patch=self.root/'PATCH.001'
        fixture(base,[('DATABASES/MONSTERS.DBS',b'unsupported',0)])
        fixture(patch,[('DATABASES/MONSTERS.DBS',self.data,0)])
        self.assertEqual(monster_catalog(Resources(Archive(base),[Archive(patch)]))['provenance']['file'],'PATCH.001')
