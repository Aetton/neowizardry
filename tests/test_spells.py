import hashlib
from pathlib import Path
import struct
import tempfile
import unittest
from tools.slf import Archive, Resources, FormatError
from tools.spells import descriptions, spell_catalog
from test_slf import fixture


def desc_fixture(texts):
    payload=bytearray(b'\x01\0\0\0\0');offsets=[]
    for text in texts:
        offsets.append(len(payload));raw=text.encode('utf-16le')
        payload.extend(b'\x01\0\0\0\0\xff\xff\xff'+struct.pack('<I',len(raw)//2)+raw)
    index=len(payload)
    payload.extend(struct.pack('<'+'I'*len(offsets),*offsets))
    payload.extend(struct.pack('<II',len(offsets),0))
    return bytes(payload),index


def spell_table():
    r=bytearray(704);name='Fixture ember'.encode('utf-16le');r[0x19c:0x19c+len(name)]=name
    for off,value in [(0x14a,0),(0x157,1),(0x234,0),(0x230,-1),(0x238,3),(0x23c,1),(0x145,30),(0x14e,720),(0x248,1)]:
        struct.pack_into('<i',r,off,value)
    r[0x149]=1;r[0x15b]=7 # preserve an unknown school flag, do not collapse to boolean
    struct.pack_into('<HBB',r,0x152,4,2,6)
    return struct.pack('<II',1,1)+r


class SpellTest(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name)
        self.table=spell_table();self.desc,self.idx=desc_fixture(['Synthetic description','Unreferenced extra'])

    def resources(self,table=None,desc=None):
        base=self.root/'DATA.SLF';patch=self.root/'PATCH.001'
        fixture(base,[('DATABASES/SPELLTABLES.DBS',self.table,0),('DATABASES/SPELLDESC.DBS',self.desc if desc is None else desc,0)])
        fixture(patch,[('DATABASES/SPELLTABLES.DBS',self.table if table is None else table,0)])
        return Resources(Archive(base),[Archive(patch)])

    def test_verified_fields_and_sources(self):
        c=spell_catalog(self.resources());s=c['spells'][0]
        self.assertEqual((s['name'],s['cost'],s['level'],s['range_code']),('Fixture ember',0,1,-1))
        self.assertEqual((s['damage_constant'],s['damage_dice'],s['damage_sides']),(4,2,6))
        self.assertEqual(s['school_flags'],[1,7,0,0])
        self.assertEqual((s['duration_base_raw'],s['duration_per_power_raw']),(720,30))
        self.assertEqual(c['provenance']['file'],'PATCH.001');self.assertEqual(c['description_provenance']['file'],'DATA.SLF')
        self.assertEqual(c['description_index']['count'],2)
        self.assertEqual(bytes.fromhex(s['raw_record_hex']),self.table[8:])
        self.assertEqual(s['record_sha256'],hashlib.sha256(self.table[8:]).hexdigest())
        self.assertEqual(s['description'],'Synthetic description')

    def test_bad_table_size(self):
        for table in [b'',self.table[:-1],self.table+b'0',struct.pack('<II',2,1)+self.table[8:]]:
            with self.subTest(size=len(table)),self.assertRaises(FormatError):spell_catalog(self.resources(table=table))

    def test_description_pointer_and_length_bounds(self):
        for offset in [0,self.idx,self.idx-11,2**32-1]:
            b=bytearray(self.desc);struct.pack_into('<I',b,self.idx,offset)
            with self.subTest(offset=offset),self.assertRaises(FormatError):descriptions(b)
        b=bytearray(self.desc);struct.pack_into('<I',b,13,2**32-1)
        with self.assertRaises(FormatError):descriptions(b)

    def test_description_index_and_marker(self):
        for data in [b'',self.desc[:12],self.desc[:-8]+struct.pack('<II',2**32-1,0)]:
            with self.assertRaises(FormatError):descriptions(data)
        b=bytearray(self.desc);b[5]=2
        with self.assertRaises(FormatError):descriptions(b)

    def test_missing_descriptions_fail(self):
        one,_=desc_fixture(['One'])
        table=struct.pack('<II',2,1)+self.table[8:]*2
        with self.assertRaises(FormatError):spell_catalog(self.resources(table=table,desc=one))

    def test_invalid_utf16_is_not_replaced(self):
        b=bytearray(self.table);b[8+0x19c:8+0x19e]=b'\x00\xd8'
        with self.assertRaises(UnicodeError):spell_catalog(self.resources(table=b))

    def test_command_line_format_error_is_clean(self):
        import subprocess,sys
        self.resources()
        path=self.root/'BROKEN.SLF';fixture(path,[('DATABASES/SPELLTABLES.DBS',b'bad',0),('DATABASES/SPELLDESC.DBS',self.desc,0)])
        result=subprocess.run([sys.executable,'-m','tools.slf','spells',str(path),'--out',str(self.root/'no.json')],capture_output=True,text=True)
        self.assertEqual(result.returncode,1);self.assertIn('Error: Truncated spell table',result.stderr)
        self.assertNotIn('Traceback',result.stderr);self.assertFalse((self.root/'no.json').exists())

    def test_shared_description_pointer_is_valid(self):
        b=bytearray(self.desc);struct.pack_into('<I',b,self.idx+4,5)
        self.assertEqual(descriptions(b)[0],['Synthetic description']*2)


if __name__=='__main__':unittest.main()
