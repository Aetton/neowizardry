import hashlib
import json
import struct
import unittest
from pathlib import Path
import tempfile
from unittest.mock import patch
from tools import creatures
from tools.slf import FormatError


def fixture_mon(texture=''):
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
    material[40:40+len(texture)]=texture.encode('ascii')
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


class SkinResources(FixtureResources):
    def resolve(self,name):
        if name=='MONSTERS/test.MLS':
            data=b'IDLE test_idle 20\nATTACK_CLOSE1 test_idle\nATTACK_CLOSE2 test_idle 30\nDIE test_idle\nSKIN DEFAULT\nSKINSWAP old.tga base.ifl\nSKIN\nSKINSWAP base.ifl hurt.tga\n'
        elif name=='MONSTERS/test_idle.MON':data=fixture_mon('old.tga')
        elif name=='MONSTERS/BITMAPS/base.ifl':data=b'base1.tga\nbase2.tga\n'
        elif name.startswith('MONSTERS/BITMAPS/'):
            data=struct.pack('<BBBHHBHHHHBB',0,0,2,0,0,0,0,0,1,1,32,0x28)+{'base1.tga':b'\0\0\xff\xff','base2.tga':b'\0\xff\0\xff','hurt.tga':b'\xff\0\0\x80'}[name.rsplit('/',1)[1]]
        else:return super().resolve(name)
        return data,{'kind':'loose','file':name,'sha256':hashlib.sha256(data).hexdigest()}


class CreatureTests(unittest.TestCase):
    def test_compressed_frames_uv_binding_and_native_rates(self):
        animation,first,last=creatures.mon(fixture_mon())
        self.assertEqual((first,last,animation['fps']),(0,1,15))
        payload,manifest,report=creatures.build(FixtureResources(),0)
        self.assertEqual((report['clips'],report['frames'],report['frame_triangles']),(3,2,2))
        self.assertEqual([c['fps'] for c in manifest['clips']],[20,15,15])
        self.assertEqual(manifest['clips'][0]['bounds'][0],[[0.0,0.0,-0.0],[1.3,1.3,-0.0]])
        self.assertEqual(manifest['clips'][0]['bounds'][1][1][0],1.625)
        self.assertEqual(manifest['mesh_sha256'],hashlib.sha256(payload).hexdigest())
        length=struct.unpack_from('<I',payload,12)[0];doc=json.loads(payload[20:20+length])
        self.assertEqual(len(doc['meshes']),2)
        self.assertEqual(manifest['clips'][0]['frames'],manifest['clips'][1]['frames'])
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
        for text in (b'WALK bat',b'IDLE ../bat',b'IDLE bat nan',b'IDLE bat 0',b'SCALEFACTOR -1\nIDLE bat',b'IDLE bat\nSKINSWAP ../a b',b'IDLE bat\nOPACITY 2',b'IDLE bat\nGLOW nan'):
            with self.assertRaises((FormatError,ValueError)):creatures.mls(text)
        self.assertEqual(creatures.mls(b'# source\nScaleFactor 2\nIDLE bat 40\n')[0],2)

    def test_all_cycle_families_variants_default_damage_and_animated_skins(self):
        payload,manifest,report=creatures.build(SkinResources(),0)
        self.assertEqual([(c['cycle'],c['variant']) for c in manifest['clips']],[(1,0),(6,0),(6,1),(21,0)])
        self.assertEqual((report['skin_stages'],report['images'],report['frames']),(2,3,2))
        length=struct.unpack_from('<I',payload,12)[0];doc=json.loads(payload[20:20+length])
        skins=doc['materials'][0]['extras']['neoCreatureSkins']
        self.assertEqual([len(s['images']) for s in skins],[2,1])
        self.assertNotIn('neoAnimation',doc['materials'][0]['extras'])
        self.assertEqual([i['name'].rsplit('/',1)[1] for i in doc['images']],['base1.tga','base2.tga','hurt.tga'])
        self.assertEqual([creatures.cycle_id(n) for n in ('SPICE3','TALK_SPICE2','ATTACK_THRUST','SPELL','DIE2')],[2,23,10,25,21])

    def test_particle_prefix_is_bounded_and_omission_is_explicit(self):
        data=fixture_mon();particle=data[:2]+b'\1'+struct.pack('<i',1)+b'\4'+bytes(0x225)+data[3:]
        animation,first,last=creatures.mon(particle)
        self.assertEqual((len(animation['particles']),first,last),(1,0,1))
        for end in (3,6,7,100,0x225+7):
            with self.assertRaises(FormatError):creatures.mon(particle[:end])
        class Particles(FixtureResources):
            def resolve(self,name):
                if name.endswith('.MON'):return particle,{'kind':'loose','file':name,'sha256':hashlib.sha256(particle).hexdigest()}
                return super().resolve(name)
        _,manifest,_=creatures.build(Particles(),0)
        self.assertEqual(len(manifest['particle_clips']),3)
        self.assertIn('particle emitters',manifest['limitations'])

    def test_missing_optional_animation_is_reported_without_replacing_it(self):
        class Missing(FixtureResources):
            def resolve(self,name):
                if name=='MONSTERS/test.MLS':return b'IDLE test_idle\nDIE missing',{'kind':'loose','file':name,'sha256':'a'*64}
                if name=='MONSTERS/missing.MON':raise KeyError(name)
                return super().resolve(name)
        _,manifest,_=creatures.build(Missing(),0)
        self.assertEqual(len(manifest['clips']),1)
        self.assertEqual(manifest['blocked_clips'][0]['cycle'],21)

    def test_batch_accounts_for_all_definitions_shares_geometry_and_checks_cache_hash(self):
        raw=FixtureResources().database[4:];second=bytearray(raw);second[:4]='X\0'.encode('utf-16le')
        resources=FixtureResources(struct.pack('<I',2)+raw+second)
        with tempfile.TemporaryDirectory() as temporary,patch.object(creatures,'fingerprint',return_value='fixture'):
            out=Path(temporary)/'assets';report=Path(temporary)/'report.json'
            summary=creatures.export_all(resources,out,report)
            self.assertEqual((summary['renderable'],summary['totals']['models']),(2,1))
            a=json.loads((out/'creature-0.nwcreature.json').read_text());b=json.loads((out/'creature-1.nwcreature.json').read_text())
            self.assertEqual(a['mesh_file'],b['mesh_file']);self.assertNotEqual(a['record_sha256'],b['record_sha256'])
            with patch.object(creatures,'build',side_effect=AssertionError('cache should be reused')):creatures.export_all(resources,out,report)
            (out/a['mesh_file']).write_bytes(b'corrupted')
            creatures.export_all(resources,out,report)
            self.assertEqual(hashlib.sha256((out/a['mesh_file']).read_bytes()).hexdigest(),a['mesh_sha256'])

    def test_database_bounds_and_model_name_use_cycle_field(self):
        for data in (b'',struct.pack('<I',2)+bytes(663),struct.pack('<I',1)+bytes(663)):
            with self.assertRaises(FormatError):creatures.build(FixtureResources(data),0)
        with self.assertRaises(FormatError):creatures.build(FixtureResources(),1)
        resources=FixtureResources();changed=bytearray(resources.database);changed[4+0x189:4+0x1b9]=b'../escape'.ljust(48,b'\0')
        with self.assertRaises(FormatError):creatures.build(FixtureResources(bytes(changed)),0)


if __name__=='__main__':unittest.main()
