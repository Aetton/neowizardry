"""Bounded TGA decoding, lossless PNG encoding and archive-local PVL textures.

Format cross-check: Godot 4.5.1 modules/tga/image_loader_tga.cpp (MIT).
Independent implementation. Decoded pixels and animation frames remain private.
"""
from __future__ import annotations

from dataclasses import dataclass
import hashlib
from pathlib import PurePosixPath
import struct
import zlib

from tools.slf import Archive, FormatError, canonical


@dataclass(frozen=True)
class Image:
    width: int
    height: int
    rgba: bytes
    source_bits: int
    source_type: int

    @property
    def alpha_mode(self):
        values = set(self.rgba[3::4])
        return 'BLEND' if values - {0, 255} else 'MASK' if 0 in values else 'OPAQUE'

    def png(self):
        def chunk(kind, data):
            return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
        rows = b''.join(b'\0' + self.rgba[y*self.width*4:(y+1)*self.width*4] for y in range(self.height))
        return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', self.width, self.height, 8, 6, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b''))


def tga(data: bytes) -> Image:
    offset = 0

    def take(size):
        nonlocal offset
        if size < 0 or size > len(data) - offset:
            raise FormatError('Truncated TGA payload')
        result = data[offset:offset+size]
        offset += size
        return result

    header = struct.unpack('<BBBHHBHHHHBB', take(18))
    id_size, map_type, kind, first, entries, map_bits, _, _, width, height, bits, descriptor = header
    if not width or not height or width * height > 16_777_216 or descriptor & 0xc0:
        raise FormatError('Unsupported TGA dimensions/interleaving')
    if map_type not in (0, 1) or kind not in (1, 2, 3, 9, 10, 11):
        raise FormatError('Unsupported TGA image type')
    base = kind - 8 if kind >= 9 else kind
    if (base == 1 and (map_type != 1 or bits not in (8, 16))) or (base == 2 and bits not in (15, 16, 24, 32)) or (base == 3 and bits not in (8, 16)):
        raise FormatError('Unsupported TGA pixel depth')
    take(id_size)

    def color(raw, depth, alpha):
        if depth in (15, 16):
            value = int.from_bytes(raw, 'little')
            expand = lambda x: (x << 3) | (x >> 2)
            return bytes((expand((value >> 10) & 31), expand((value >> 5) & 31), expand(value & 31),
                          255 if not alpha or value & 0x8000 else 0))
        if depth == 24:
            return bytes((raw[2], raw[1], raw[0], 255))
        if depth == 32:
            # Keep all stored alpha bytes; the descriptor is not always reliable in game assets.
            return bytes((raw[2], raw[1], raw[0], raw[3]))
        raise FormatError('Unsupported TGA palette depth')

    palette = []
    if map_type:
        if not entries or map_bits not in (15, 16, 24, 32):
            raise FormatError('Unsupported TGA palette')
        for _ in range(entries):
            palette.append(color(take((map_bits+7)//8), map_bits, map_bits in (16, 32)))

    def pixel():
        raw = take((bits+7)//8)
        if base == 1:
            index = int.from_bytes(raw, 'little') - first
            if not 0 <= index < len(palette):
                raise FormatError('TGA palette index outside table')
            return palette[index]
        if base == 3:
            return bytes((raw[0], raw[0], raw[0], raw[1] if bits == 16 else 255))
        return color(raw, bits, descriptor & 15)

    count = width * height
    sequential = bytearray()
    if kind >= 9:
        while len(sequential)//4 < count:
            packet = take(1)[0]
            length = (packet & 127) + 1
            if length > count - len(sequential)//4:
                raise FormatError('TGA RLE packet exceeds image')
            if packet & 128:
                sequential.extend(pixel() * length)
            else:
                for _ in range(length):
                    sequential.extend(pixel())
    else:
        for _ in range(count):
            sequential.extend(pixel())
    # PNG/glTF convention: first pixel is top left. Honor both TGA origin bits.
    rgba = bytearray(count * 4)
    for y in range(height):
        target_y = y if descriptor & 0x20 else height - 1 - y
        for x in range(width):
            target_x = width - 1 - x if descriptor & 0x10 else x
            source = (y * width + x) * 4
            target = (target_y * width + target_x) * 4
            rgba[target:target+4] = sequential[source:source+4]
    return Image(width, height, bytes(rgba), bits, kind)


def ifl(data: bytes) -> list[str]:
    # Original editor treats each nonempty line as one relative frame filename.
    names = [line.strip() for line in data.decode('latin1').splitlines() if line.strip()]
    if not names or len(names) > 4096:
        raise FormatError('Empty/oversized IFL animation')
    for name in names:
        if '/' in canonical(name) or not name.lower().endswith('.tga'):
            raise FormatError('IFL frame must be a plain TGA filename')
    return names


class Textures:
    def __init__(self, archive: Archive, resource: str):
        self.archive = archive
        self.folder = PurePosixPath(resource).parent.as_posix() + '/BITMAPS/'
        self.images = {}
        self.animations = {}

    def image(self, name):
        name = canonical(name)
        key = name.casefold()
        if key not in self.images:
            data = self.archive.read(name)
            decoded = tga(data)
            self.images[key] = {'name': name, 'png': decoded.png(), 'width': decoded.width,
                                'height': decoded.height, 'alpha_mode': decoded.alpha_mode,
                                'source_bits': decoded.source_bits, 'source_type': decoded.source_type,
                                'source_sha256': hashlib.sha256(data).hexdigest(),
                                'pixel_sha256': hashlib.sha256(decoded.rgba).hexdigest()}
        return self.images[key]

    def frames(self, name):
        if '/' in canonical(name):
            raise FormatError('PVL texture must be a plain filename')
        if name.lower().endswith('.ifl'):
            path = self.folder + name
            data = self.archive.read(path)
            names = ifl(data)
            self.animations[path] = {'frames': len(names), 'source_sha256': hashlib.sha256(data).hexdigest()}
        elif name.lower().endswith('.tga'):
            names = [name]
        else:
            raise FormatError('Unsupported PVL texture extension')
        return [self.image(self.folder + frame) for frame in names]

    def report(self):
        return {'images': [{k:v for k,v in image.items() if k != 'png'} for _,image in sorted(self.images.items())],
                'ifl': self.animations}
