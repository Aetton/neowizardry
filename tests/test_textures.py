import hashlib
import json
from pathlib import Path
import struct
import tempfile
import unittest
import zlib

from tools.pvl import glb, parse
from tools.slf import Archive, FormatError
from tools.textures import Textures, ifl, tga
from test_pvl import fixture as pvl_fixture
from test_slf import fixture as slf_fixture


def header(width=2, height=2, kind=2, bits=32, descriptor=0x28, map_type=0, first=0, entries=0, map_bits=0):
    return struct.pack('<BBBHHBHHHHBB', 0, map_type, kind, first, entries, map_bits, 0, 0, width, height, bits, descriptor)


def image(rgba):
    return header(width=len(rgba), height=1) + b''.join(bytes((b,g,r,a)) for r,g,b,a in rgba)


class TextureTest(unittest.TestCase):
    def test_all_origins_and_png_lossless_roundtrip(self):
        pixels = [(255,0,0,255), (0,255,0,0), (0,0,255,128), (255,255,0,255)]
        for descriptor in (0x08, 0x18, 0x28, 0x38):
            order = []
            for y in range(2):
                for x in range(2):
                    tx = 1-x if descriptor & 0x10 else x
                    ty = y if descriptor & 0x20 else 1-y
                    order.append(pixels[ty*2+tx])
            encoded = header(descriptor=descriptor) + b''.join(bytes((b,g,r,a)) for r,g,b,a in order)
            decoded = tga(encoded)
            self.assertEqual(decoded.rgba, bytes(c for p in pixels for c in p))
            self.assertEqual(decoded.alpha_mode, 'BLEND')
            png = decoded.png()
            self.assertEqual(png[:8], b'\x89PNG\r\n\x1a\n')
            offset, compressed = 8, bytearray()
            while offset < len(png):
                size = struct.unpack_from('>I', png, offset)[0]
                kind = png[offset+4:offset+8]
                payload = png[offset+8:offset+8+size]
                self.assertEqual(zlib.crc32(kind+payload), struct.unpack_from('>I', png, offset+8+size)[0])
                if kind == b'IDAT':
                    compressed.extend(payload)
                offset += size+12
            self.assertEqual(zlib.decompress(compressed), b'\0'+decoded.rgba[:8]+b'\0'+decoded.rgba[8:])

    def test_rle_raw_and_repeated_packets(self):
        # Four pixels: two red repeats, followed by a blue and a green raw pixel.
        data = header(width=4, height=1, kind=10, bits=24, descriptor=0x20)
        data += b'\x81\x00\x00\xff\x01\xff\x00\x00\x00\xff\x00'
        decoded = tga(data)
        self.assertEqual(decoded.rgba, bytes((255,0,0,255))*2 + bytes((0,0,255,255,0,255,0,255)))
        self.assertEqual(decoded.alpha_mode, 'OPAQUE')

    def test_palettes_16bit_and_grayscale(self):
        data = header(width=2, height=1, kind=1, bits=8, map_type=1, first=5, entries=2, map_bits=24, descriptor=0x20)
        data += b'\x00\x00\xff\x00\xff\x00\x05\x06'
        self.assertEqual(tga(data).rgba, bytes((255,0,0,255,0,255,0,255)))
        gray = header(width=1, height=1, kind=11, bits=16, descriptor=0x28) + b'\x80\x70\x80'
        self.assertEqual(tga(gray).rgba, bytes((112,112,112,128)))
        rgb555 = header(width=1, height=1, kind=2, bits=16, descriptor=0x21) + b'\x00\xfc'
        self.assertEqual(tga(rgb555).rgba, bytes((255,0,0,255)))

    def test_rejects_truncation_overrun_palette_and_interleaving(self):
        for data in (b'', header(), header(kind=10) + b'\x84\0\0\0\xff', header(descriptor=0xc0),
                     header(width=1, height=1, kind=1, bits=8, map_type=1, first=5, entries=1, map_bits=24) + b'\0\0\xff\x04'):
            with self.assertRaises(FormatError):
                tga(data)

    def test_ifl_filenames_spaces_crlf_and_rejects_escape(self):
        self.assertEqual(ifl(b'Frame one.tga\r\nframe2.TGA\r\n'), ['Frame one.tga','frame2.TGA'])
        for data in (b'', b'../bad.tga', b'C:\\bad.tga', b'other/file.tga', b'bad.png'):
            with self.assertRaises(FormatError):
                ifl(data)

    def test_textured_glb_embeds_frames_pixel_hashes_and_native_fps(self):
        with tempfile.TemporaryDirectory() as temporary:
            archive = Path(temporary) / 'LEVELS.SLF'
            pvl = bytearray(pvl_fixture())
            pvl[11+40:11+80] = b'Fire.ifl'.ljust(40, b'\0')
            struct.pack_into('<f', pvl, 11+0xfc, 1)
            struct.pack_into('<f', pvl, 11+0x111, 12)
            first = image([(255,0,0,255), (0,255,0,0)])
            second = image([(0,0,255,255), (255,255,0,255)])
            slf_fixture(archive, [('MONASTERY/BITMAPS/fire.IFL', b'one.tga\r\ntwo.tga\r\n', 0),
                                  ('Monastery/Bitmaps/one.tga', first, 0), ('Monastery/Bitmaps/two.tga', second, 0)])
            source = Textures(Archive(archive), 'Monastery/Monastery1.PVL')
            payload = glb(parse(pvl), source)
            size = struct.unpack_from('<I', payload, 12)[0]
            doc = json.loads(payload[20:20+size])
            self.assertEqual(len(doc['images']), 2)
            self.assertEqual(doc['images'][0]['extras']['neoPixelSha256'], hashlib.sha256(tga(first).rgba).hexdigest())
            material = doc['materials'][0]
            self.assertEqual(material['alphaMode'], 'MASK')
            self.assertEqual(material['extras']['neoAnimation']['fps'], 12)
            self.assertEqual(material['extras']['neoAnimation']['images'], [0,1])
            self.assertEqual(material['pbrMetallicRoughness']['baseColorTexture']['index'], 0)
            self.assertEqual(doc['samplers'][0]['wrapT'], 10497)
            self.assertEqual(source.frames('Fire.ifl')[0]['width'], 2)
            with self.assertRaises(KeyError):
                source.frames('missing.tga')
