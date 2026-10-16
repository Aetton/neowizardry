import hashlib
import struct
import unittest

from tools.oct import FormatError, encode, parse


def fixture(flags=(4, 0, 0x1084, 0x804, 0x14, 8, 4), conditional=(6,)):
    header = bytearray(0xf5)
    struct.pack_into('<H', header, 0, 0x22)
    struct.pack_into('<III', header, 0x56, 1, 1, 1)
    struct.pack_into('<I', header, 0x6a, 1)
    struct.pack_into('<I', header, 0x6e, 1)
    struct.pack_into('<I', header, 0x86, 1)
    struct.pack_into('<I', header, 0x7e, 1)
    struct.pack_into('<H', header, 0x96, 1)
    result = header + b'\xff'*4 + b'\0'*(36+40+4+4) + b'\xff'*8
    result += b'\0'*(8+16) + b'\xff'*4
    gd = bytearray(0x68)
    struct.pack_into('<I', gd, 0, 1)
    struct.pack_into('<10i', gd, 28, 6, len(flags), 0, 0, 0, int(bool(conditional)), int(bool(conditional)), 0, len(conditional), 1)
    result.extend(gd)
    for p in ((-1000,0,-1000),(1000,0,-1000),(0,0,1000),(1000,0,-1000),(1000,2000,-1000),(1000,0,1000)):
        result.extend(struct.pack('<fff', *p))
    for index, flag in enumerate(flags):
        raw = bytearray(76)
        struct.pack_into('<II', raw, 0, flag, index)
        struct.pack_into('<iii', raw, 24, *((3,4,5) if index == 1 else (0,1,2)))
        struct.pack_into('<ffff', raw, 36, *((1,0,0,-1000) if index == 1 else (0,1,0,0)))
        result.extend(raw)
    if conditional:
        result.extend(struct.pack('<iii', 1, 1, 0))
        result.extend(struct.pack('<iii', 0, len(conditional), 0))
        result.extend(struct.pack('<'+'i'*len(conditional), *conditional))
    result.extend(b'\0'*68 + b'\xff'*4)
    return bytes(result)


class OctTest(unittest.TestCase):
    def test_full_file_boundaries_and_physical_flags(self):
        data = fixture()
        collision = parse(data)
        self.assertEqual([s.index for s in collision.selected], [0, 1])
        self.assertEqual(collision.report['walkable'], 1)
        self.assertEqual(collision.report['sections']['end'], len(data))
        self.assertEqual(collision.report['excluded'], {'crossing_environment':1, 'prop_geometry':1, 'conditional':2, 'inactive_processed':1})
        self.assertEqual(collision.report['source_sha256'], hashlib.sha256(data).hexdigest())

    def test_private_binary_preserves_source_ids_flags_and_coordinate_convention(self):
        result = encode(parse(fixture()), '12'*32)
        self.assertEqual(result[:8], b'NWCOL001')
        self.assertEqual(result[8:40], b'\x12'*32)
        self.assertEqual(struct.unpack_from('<I', result, 72)[0], 2)
        self.assertEqual(len(result), 76 + 56 * 2)
        first = struct.unpack_from('<II12f', result, 76)
        self.assertEqual(first[:2], (4,0))
        self.assertAlmostEqual(first[5], -1.3)
        self.assertAlmostEqual(first[7], 1.3)

    def test_rejects_truncation_trailing_bytes_version_and_marker(self):
        data = fixture()
        for cut in (0, 244, 248, 400, len(data)-1):
            with self.assertRaises(FormatError): parse(data[:cut])
        with self.assertRaises(FormatError): parse(data+b'junk')
        bad = bytearray(data)
        struct.pack_into('<H', bad, 0, 35)
        with self.assertRaises(FormatError): parse(bad)
        bad = bytearray(data)
        bad[245] = 0
        with self.assertRaises(FormatError): parse(bad)

    def test_rejects_invalid_surface_and_conditional_indices(self):
        with self.assertRaises(FormatError): parse(fixture(conditional=(100,)))
        data = bytearray(fixture())
        start = 369 + 104 + 72
        struct.pack_into('<i', data, start+24, 6)
        with self.assertRaises(FormatError): parse(data)
        data = bytearray(fixture())
        struct.pack_into('<f', data, 369+104, float('nan'))
        with self.assertRaises(FormatError): parse(data)
