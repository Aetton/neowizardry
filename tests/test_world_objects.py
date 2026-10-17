import hashlib
import struct
import unittest

from test_pvl import fixture as static_fixture
from tools.pvl import parse as parse_static
from tools.world_objects import ObjectsReader, parse, summary, FormatError


def pack(fmt, *values):
    return struct.pack('<'+fmt, *values)


def string(value, size):
    return value.encode().ljust(size, b'\0')


def trigger(*, name='ENTRY', action=0x34, positioned=True, door=False):
    result = pack('3B', 1, 4, 3)+string(name,128)
    result += pack('7B3i', not positioned,1,0,0,0,0,0,action,0,0)
    result += bytes(256)+pack('B',0)+bytes(256)+pack('BiB',0,0,0)+bytes(128)
    result += pack('3if4B4i',0,0,0,1,0,0,0,0,0,0,0,0)
    result += pack('3B',2,0,0)+bytes(256)+bytes(256)+pack('B',0)
    result += pack('2f',0,2)+bytes(256)+bytes(4)+bytes(128)
    if positioned:
        result += pack('B7fB',1,1,2,3,0,0,1,0,0)
    result += pack('B',door)
    if door:
        result += pack('BB9BH B3f',1,1,0,1,1,0,0,0,1,0,0,65535,1,4,5,6)+string('LINK',128)
    return result


def mesh(*, compressed=False):
    flags = 7 if compressed else 0
    result = pack('3iB10fB',4,3,1,flags,0,0,0,0,0,1,0,1,1,1,0)
    if compressed:
        result += pack('Bhf',2,2,1000)+pack('18h',*range(18))
        result += pack('3H6fHB',0,1,2,0,0,1,0,0,1,0,0)
    else:
        result += pack('9f',0,0,0,1,0,0,0,0,1)
        result += pack('3I6fIB',0,1,2,0,0,1,0,0,1,0,0)
    return result+pack('hB',1,0)+bytes(281)


def prop():
    result = pack('3B3fI',9,2,0,1,2,3,4)+string('DOOR',64)
    result += pack('b4H',2,0,0,1,1)
    result += pack('2b5Bf3Bf',11,1,1,0,0,0,1,15,0,1,0,1)
    result += bytes(50)+pack('5B',2,0,0,0,1)+pack('B',2)
    result += pack('2B',1,0)+mesh()
    result += pack('2B3i20f',0,2,0,0,2,*range(20))
    result += pack('B',1)+trigger(name='DOOR_TRIGGER',action=1,positioned=False,door=True)
    result += pack('3B',1,2,3)
    return result


def fixture():
    prefix = static_fixture()
    prefix = prefix[:parse_static(prefix).geometry_end]
    marker = pack('i',-1)
    result = prefix+pack('h',0)+marker+pack('i',0)+marker
    # A real serialized item includes four int fields after its 3-float position.
    result += pack('i',1)+string('ITEM',20)+pack('3f4iB',1,2,3,0,0,0,0,0)+bytes(19)+marker
    result += pack('i',0)+marker+pack('i',1)+prop()+marker
    result += pack('i',0)+marker+pack('i',0)+marker+pack('i',0)+marker
    result += pack('i',1)+trigger()+marker
    return result


class ObjectsTest(unittest.TestCase):
    def test_props_frames_door_state_and_arrival(self):
        data = fixture()
        result = parse(data+b'opaque-tail')
        self.assertEqual(result['sections']['triggers_end'],len(data))
        self.assertEqual(result['unparsed_tail_bytes'],11)
        self.assertEqual(result['pvl_sha256'],hashlib.sha256(data+b'opaque-tail').hexdigest())
        self.assertEqual(len(result['props']),1)
        p = result['props'][0]
        self.assertEqual(p['segments'],[(0,0),(1,1)])
        self.assertEqual(len(p['animation']['entries'][0]['path']['frames']),2)
        t = p['trigger']
        self.assertEqual(t['owner'],'props:0')
        self.assertTrue(t['door']['locked'])
        self.assertEqual(t['door']['linked_trigger'],'LINK')
        self.assertEqual(result['triggers'][0]['position'],[1,2,3])
        self.assertEqual(result['arrivals'],[result['triggers'][0]['start']])
        metadata = summary(result,'fixture.PVL')
        self.assertEqual((metadata['props'],metadata['doors'],metadata['arrivals']),(1,1,1))
        self.assertNotIn('DOOR_TRIGGER',str(metadata))

    def test_compressed_mesh_consumes_all_lod_frames(self):
        data = mesh(compressed=True)
        reader = ObjectsReader(data)
        parsed = reader.mesh()
        self.assertEqual(parsed['lod_frames'],2)
        self.assertEqual(reader.offset,len(data))

    def test_reject_truncation_at_every_object_byte(self):
        data = fixture()
        start = parse_static(data).geometry_end
        for end in range(start,len(data)):
            with self.assertRaises(FormatError,msg=str(end)):
                parse(data[:end])

    def test_reject_marker_version_counts_and_nonfinite_coordinates(self):
        data = fixture()
        result = parse(data)
        start = parse_static(data).geometry_end
        bad = bytearray(data)
        struct.pack_into('<h',bad,start,-1)
        with self.assertRaises(FormatError): parse(bytes(bad))
        bad = bytearray(data)
        struct.pack_into('<i',bad,start+2,0)
        with self.assertRaises(FormatError): parse(bytes(bad))
        for raw in (b'\x00\x04\xff',pack('3B',1,9,1)):
            with self.assertRaises(FormatError): ObjectsReader(raw).trigger('test')
        bad = bytearray(data)
        # The first positioned super trigger is the explicit arrival record.
        at = result['triggers'][0]['end']-31
        struct.pack_into('<f',bad,at,float('nan'))
        with self.assertRaises(FormatError): parse(bytes(bad))


if __name__ == '__main__':
    unittest.main()
