import hashlib
import json
import struct
import unittest
from tools import creatures
from tools.slf import FormatError


def fixture_mon():
    pack=lambda f,*v:struct.pack('<'+f,*v)
    header=pack('3B2b5Bf3Bf',1,0,0,11,1,1,0,1,1,0,15,0,1,0,1)
    # Animation version 11: discarded bytes, channel hints, bounds/lights/path/ignored.
    header+=bytes(50)+bytes([2,0,0,0,0,2,2,0])
    transform=[0,0,0,0,0,1,0,1,1,1]
    mesh=pack('3iB10fB',4,3,1,7,*transform,0)
    mesh+=pack('Bhf',2,2,1000)
    mesh+=pack('18h',0,0,0,0,2000,0,2000,0,0, 0,0,0,0,2000,0,2500,0,0)
    mesh+=pack('3H6fHB',0,1,2,0,0,0,1,1,0,0,0)
    material=bytearray(297);struct.pack_into('<f',material,0xfc,1);struct.pack_into('<f',material,0x111,1)
    return header+mesh+pack('hB',1,4)+bytes(material)


class FixtureResources:
    def __init__(self, database=None):
        if database is None:
            raw=bytearray(663);raw[0x189:0x189+5]=b'test\0';database=struct.pack('<I',1)+raw
        self.database=database
    def resolve(self,name):
        data=self.database if name=='DATABASES/MONSTERS.DBS' else b'IDLE test_idle 20\nATTACK_CLOSE test_idle\nGET_HIT test_idle\n' if name=='MONSTERS/test.MLS' else fixture_mon()
        return data,{'kind':'loose','file':name,'sha256':hashlib.sha256(data).hexdigest()}
    def read(self,name):return self.resolve(name)[0]


class CreatureTests(unittest.TestCase):
    def test_compressed_frames_uv_binding_and_native_rates(self):
        animation,first,last=creatures.mon(fixture_mon())
        self.assertEqual((first,last,animation['fps']),(0,1,15))
        payload,manifest,report=creatures.build(FixtureResources(),0)
        self.assertEqual((report['clips'],report['frames'],report['frame_triangles']),(3,6,6))
        self.assertEqual([c['fps'] for c in manifest['clips']],[20,15,15])
        self.assertEqual(manifest['clips'][0]['bounds'][0],[[0.0,0.0,-0.0],[1.3,1.3,-0.0]])
        self.assertEqual(manifest['clips'][0]['bounds'][1][1][0],1.625)
        self.assertEqual(manifest['mesh_sha256'],hashlib.sha256(payload).hexdigest())
        length=struct.unpack_from('<I',payload,12)[0];doc=json.loads(payload[20:20+length])
        self.assertEqual(len(doc['meshes']),6)
        self.assertEqual(manifest['record_sha256'],hashlib.sha256(FixtureResources().database[4:]).hexdigest())

    def test_bad_mon_prefix_trailing_and_truncation(self):
        data=fixture_mon()
        for end in range(len(data)):
            with self.assertRaises(FormatError):creatures.mon(data[:end])
        for at in (0,1,2):
            bad=bytearray(data);bad[at]=2
            with self.assertRaises(FormatError):creatures.mon(bytes(bad))
        with self.assertRaises(FormatError):creatures.mon(data+b'extra')

    def test_mls_paths_rates_scale_and_missing_idle(self):
        for text in (b'WALK bat',b'IDLE ../bat',b'IDLE bat nan',b'IDLE bat 0',b'SCALEFACTOR -1\nIDLE bat',b'IDLE bat\nSKIN DEFAULT\nSKINSWAP a b',b'IDLE bat\nOPACITY 0.2'):
            with self.assertRaises((FormatError,ValueError)):creatures.mls(text)
        self.assertEqual(creatures.mls(b'# source\nScaleFactor 2\nIDLE bat 40\n')[0],2)

    def test_database_bounds_and_model_name_use_cycle_field(self):
        for data in (b'',struct.pack('<I',2)+bytes(663),struct.pack('<I',1)+bytes(663)):
            with self.assertRaises(FormatError):creatures.build(FixtureResources(data),0)
        with self.assertRaises(FormatError):creatures.build(FixtureResources(),1)
        resources=FixtureResources();changed=bytearray(resources.database);changed[4+0x189:4+0x1b9]=b'../escape'.ljust(48,b'\0')
        with self.assertRaises(FormatError):creatures.build(FixtureResources(bytes(changed)),0)


if __name__=='__main__':unittest.main()
