import hashlib
import struct
import unittest
from unittest.mock import patch
from tools.level_status import parse,build,item_bank,Reader
from tools.slf import FormatError


def chunk(name,payload,grouped=0):
    return struct.pack('<4sBBi',name.encode(),grouped,0,len(payload))+payload


def fixture(*,species=0,script=False):
    group=bytearray(0x12b)
    struct.pack_into('<i',group,0,7);struct.pack_into('<I',group,0x18,species)
    struct.pack_into('<I',group,0xc4,3)
    mons=struct.pack('<II',1,3)+struct.pack('<I',len(group))+group+b'\0'
    for i in range(3):
        record=bytearray(0x425)
        struct.pack_into('<iiI',record,0,20+i,7,species)
        struct.pack_into('<3f',record,0x17,100+i*20,0,50)
        struct.pack_into('<iI',record,0x27,16,12+i)
        mons+=struct.pack('<II',7,len(record))+record+bytes([script])
        if script:mons+=bytes(64+8)+struct.pack('<I',2)+bytes(2)
        mons+=b'\0\1'+bytes(32)+bytes(13)+struct.pack('<I',1)+bytes(12+12+2)
    payload=struct.pack('<II',1,8)+chunk('MONS',mons)
    return chunk('RIFF',struct.pack('<I',1)+chunk('LVLS',payload))


class LevelStatusTest(unittest.TestCase):
    def test_item_chains_preserve_instances_and_reject_truncation(self):
        raw=bytearray(0xad)
        struct.pack_into('<ii',raw,0,42,0)
        struct.pack_into('<i6B',raw,9,123,5,7,1,0,0,0)
        struct.pack_into('<3f',raw,0x15,10,20,30)
        struct.pack_into('<I',raw,0x29,1)
        tail=bytes(raw);struct.pack_into('<I',raw,0x31,0x123456)
        data=struct.pack('<I',1)+raw+tail
        bank=item_bank(Reader(data))
        self.assertEqual(len(bank),1);self.assertEqual(len(bank[0]),2)
        self.assertEqual(bank[0][0]['definition_id'],123)
        self.assertEqual(bank[0][0]['position'],[10,20,30])
        self.assertEqual(bank[0][0]['stack_count'],5)
        self.assertEqual(bank[0][0]['charges'],7)
        for end in range(len(data)):
            with self.assertRaises(FormatError):item_bank(Reader(data[:end]))
        with self.assertRaises(FormatError):item_bank(Reader(data+b'x'))

    def test_native_packed_headers_group_flags_and_monster_tail(self):
        data=fixture(script=True);result=parse(data);bank=result['levels'][0]['bank']
        self.assertEqual(result['sha256'],hashlib.sha256(data).hexdigest())
        self.assertEqual(bank['groups'][0]['id'],7)
        self.assertEqual([m['id'] for m in bank['monsters']],[20,21,22])
        self.assertEqual([m['hp'] for m in bank['monsters']],[12,13,14])
        self.assertTrue(all(m['script'] for m in bank['monsters']))
        self.assertEqual(bank['monsters'][2]['position'],[140,0,50])

    def test_every_truncation_and_trailing_data_rejected(self):
        data=fixture()
        for end in range(len(data)):
            with self.assertRaises(FormatError):parse(data[:end])
        for bad in (data+b'extra',b'FAIL'+data[4:]):
            with self.assertRaises(FormatError):parse(bad)

    def test_selects_source_group_using_named_beach_entrance(self):
        data=fixture()
        class Archive:
            def read(self,name):return b'authored PVL' if name.endswith('.PVL') else data
        objects={'pvl_sha256':'a'*64,'arrivals':[1,2],'triggers':[
            {'start':1,'name':'MO101','position':[999,999,999]},
            {'start':2,'name':'MO100','position':[0,0,0]}]}
        with patch('tools.level_status.objects',return_value=objects):
            result=build(Archive())
        self.assertEqual((result['level'],result['group_id'],len(result['members'])),(8,7,3))
        self.assertEqual(result['members'][1]['location_id'],21)
        self.assertEqual(result['members'][1]['position'],dict(x=120,y=0,z=50))
        self.assertNotIn('raw_record_hex',str(result))
        with patch('tools.level_status.objects',return_value=objects):
            class Scripted(Archive):
                def read(self,name):return b'authored PVL' if name.endswith('.PVL') else fixture(script=True)
            with self.assertRaises(FormatError):build(Scripted())
        objects['triggers'][1]['name']='MO199'
        with patch('tools.level_status.objects',return_value=objects):
            with self.assertRaises(FormatError):build(Archive())

if __name__=='__main__':unittest.main()
