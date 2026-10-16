"""Strict OCT v0x22 / processed GameData v1 intake; private static collision export.

Layout research: wizardry-8-decomp 075cbd0b853c4670e94f6ae4fe48d53b52b5e819.
Independent byte reader; no recovered implementation is copied.
"""
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

from tools.slf import Archive, FormatError

MAX_COUNT = 2_000_000
SCALE = 0.0013


class Reader:
    def __init__(self, data):
        self.data, self.offset = data, 0

    def take(self, size):
        if size < 0 or size > len(self.data) - self.offset:
            raise FormatError(f'OCT truncated at {self.offset:#x}, need {size} bytes')
        start = self.offset
        self.offset += size
        return self.data[start:self.offset]

    def unpack(self, fmt):
        return struct.unpack('<' + fmt, self.take(struct.calcsize('<' + fmt)))

    def skip(self, count, stride):
        if not 0 <= count <= MAX_COUNT:
            raise FormatError(f'Unsupported OCT count at {self.offset:#x}: {count}')
        self.take(count * stride)

    def marker(self, expected=0xffffffff):
        if self.unpack('I')[0] != expected:
            raise FormatError(f'OCT marker mismatch at {self.offset-4:#x}')

    def packed_bits(self, expected=None):
        self.marker(0xdeadd00d)
        count, size = self.unpack('II')
        if not 0 < count <= MAX_COUNT or (expected is not None and count != expected):
            raise FormatError('OCT bit-array count mismatch')
        self.take(size)  # Huffman payload is not needed for the GD physical surface bank.
        self.marker(0xdeadd00d)


def count(value):
    if not 0 <= value <= MAX_COUNT:
        raise FormatError(f'Unsupported OCT count {value}')
    return value


def finite(values):
    if any(not math.isfinite(v) for v in values):
        raise FormatError('Non-finite OCT physical geometry')


def point(p):
    return (p[0] * SCALE, p[1] * SCALE, -p[2] * SCALE)


@dataclass(frozen=True)
class Surface:
    flags: int
    index: int
    vertices: tuple
    plane: tuple


@dataclass
class Collision:
    vertices: list
    surfaces: list[Surface]
    selected: list[Surface]
    report: dict


def parse(data: bytes) -> Collision:
    r = Reader(data)
    header = r.take(0xf5)
    def u32(offset): return count(struct.unpack_from('<I', header, offset)[0])
    def u16(offset): return struct.unpack_from('<H', header, offset)[0]
    if u16(0) != 0x22:
        raise FormatError('Only OCT version 0x22 is supported')
    grid = struct.unpack_from('<III', header, 0x56)
    branches, leaves, polygons = u32(0x6a), u32(0x6e), u32(0x72)
    submeshes, meshes = u32(0x66), u32(0x9e)
    poly_stream, gd_stream = u32(0x82), u32(0x86)
    props, particles = u32(0xbd), u32(0xc1)
    path_nodes = u32(0x7e)
    sections = {}
    r.marker()
    r.skip(branches, 0x24)
    r.skip(leaves, 0x28)
    r.skip(poly_stream, 4)
    cells = grid[0] * grid[1] * grid[2]
    if cells < 250000:
        r.skip(cells, 4)
    r.skip(polygons, 4)
    r.skip(u32(0x92), 2)
    r.skip(gd_stream, 4)
    r.skip(u32(0x8a), 2)
    regions = u16(0x96)
    if regions > 1:
        r.skip(regions, 0xe8)
    r.marker()
    sections['spatial_end'] = r.offset
    if submeshes:
        r.skip(submeshes + 1, 16)
        if meshes:
            r.packed_bits(meshes)
        if particles:
            r.skip(meshes + 1, 2)
            r.skip(u16(0xc5), 2)
        if props:
            r.skip(meshes + 1, 2)
            r.skip(u16(0xc7), 2)
    r.marker()
    sections['mesh_links_end'] = r.offset
    if path_nodes:
        r.skip(path_nodes, 8)
        paths, frames, nodes, _ = r.unpack('IIII')
        for value in (paths, frames, nodes): count(value)
        if frames >= 2 and nodes >= 2:
            r.skip(paths, 0x44)
            r.skip(frames, 6)
            r.skip(nodes, 8)
    if props and header[0xb8]:
        r.packed_bits(props)
    r.marker()
    sections['game_data_start'] = r.offset
    if not gd_stream:
        raise FormatError('OCT has no processed GameData bank')
    gd = r.take(0x68)
    if struct.unpack_from('<I', gd)[0] != 1:
        raise FormatError('Unsupported processed GameData version')
    counts = struct.unpack_from('<10i', gd, 28)
    for value in counts: count(value)
    vertex_count, surface_count, trigger_base, trigger_count, integrated, interfaces, states, triggers, cond_count, environs = counts
    finite(struct.unpack_from('<6f', gd, 4))
    vertices = list(struct.iter_unpack('<fff', r.take(vertex_count * 12)))
    for p in vertices: finite(p)
    surfaces = []
    for index in range(surface_count):
        raw = r.take(0x4c)
        flags, stored_index = struct.unpack_from('<II', raw)
        ids = struct.unpack_from('<iii', raw, 24)
        plane = struct.unpack_from('<ffff', raw, 36)
        finite(plane)
        if stored_index != index or any(not 0 <= v < vertex_count for v in ids):
            raise FormatError(f'Invalid GameData surface index {index}')
        surfaces.append(Surface(flags, index, ids, plane))
    interface_rows = list(struct.iter_unpack('<iii', r.take(interfaces * 12)))
    state_rows = list(struct.iter_unpack('<iii', r.take(states * 12)))
    conditional = [x[0] for x in struct.iter_unpack('<i', r.take(cond_count * 4))]
    if any(not 0 <= v < surface_count for v in conditional):
        raise FormatError('Conditional surface outside GameData bank')
    for _, length, first in interface_rows:
        if length < 0 or first < 0 or first + length > states:
            raise FormatError('Invalid GameData interface state slice')
    for _, length, first in state_rows:
        if length < 0 or first < 0 or first + length > cond_count:
            raise FormatError('Invalid GameData conditional polygon slice')
    r.skip(environs, 0x44)
    r.marker()
    if r.offset != len(data):
        raise FormatError(f'Unexpected OCT tail: {len(data)-r.offset} bytes')
    selected, skipped = [], Counter()
    conditional = set(conditional)
    for surface in surfaces:
        if surface.index in conditional or surface.flags & 0x10:
            skipped['conditional'] += 1
        elif surface.flags & 0x1080:
            skipped['crossing_environment'] += 1
        elif surface.flags & 0x800:
            skipped['prop_geometry'] += 1
        elif surface.flags & 8:
            skipped['inactive_processed'] += 1
        else:
            a, b, c = (vertices[i] for i in surface.vertices)
            ab, ac = [b[i]-a[i] for i in range(3)], [c[i]-a[i] for i in range(3)]
            cross = (ab[1]*ac[2]-ab[2]*ac[1], ab[2]*ac[0]-ab[0]*ac[2], ab[0]*ac[1]-ab[1]*ac[0])
            if sum(v*v for v in cross) <= 1e-12:
                skipped['degenerate'] += 1
            else:
                selected.append(surface)
    if not selected:
        raise FormatError('OCT has no selected static collision surfaces')
    sections['end'] = r.offset
    used = [vertices[i] for s in selected for i in s.vertices]
    report = {'schema': 1, 'oct_version': 34, 'source_sha256': hashlib.sha256(data).hexdigest(),
              'source_bytes': len(data), 'branches': branches, 'leaves': leaves, 'mesh_count': meshes,
              'path_nodes': path_nodes, 'vertices': vertex_count, 'surfaces': surface_count,
              'trigger_base': trigger_base, 'trigger_surfaces': trigger_count, 'integrated': integrated,
              'interfaces': interfaces, 'states': states, 'triggers': triggers, 'environments': environs,
              'conditional_indices': len(conditional), 'selected': len(selected), 'excluded': dict(skipped),
              'walkable': sum(bool(s.flags & 4) for s in selected),
              'flags': dict(Counter(hex(s.flags) for s in surfaces)), 'sections': sections,
              'native_bounds': [[min(p[i] for p in used) for i in range(3)], [max(p[i] for p in used) for i in range(3)]],
              'scope': 'Static ordinary GD surfaces only; conditional/prop/crossing/environment behavior and native movement solver are not implemented.'}
    return Collision(vertices, surfaces, selected, report)


def encode(collision: Collision, pvl_hash: str):
    result = bytearray(b'NWCOL001' + bytes.fromhex(pvl_hash) + bytes.fromhex(collision.report['source_sha256']))
    result.extend(struct.pack('<I', len(collision.selected)))
    for surface in collision.selected:
        normal = surface.plane[:3]
        points = [point(collision.vertices[i]) for i in surface.vertices]
        result.extend(struct.pack('<II12f', surface.flags, surface.index, normal[0], normal[1], -normal[2],
                                  *(v for p in points for v in p)))
    return bytes(result)


def export(archive, out, report):
    source = Archive(archive)
    values = []
    for number in (1, 2):
        name = f'Monastery/Monastery{number}'
        data = source.read(name + '.OCT')
        collision = parse(data)
        pvl_hash = hashlib.sha256(source.read(name + '.PVL')).hexdigest()
        values.append((number, collision, encode(collision, pvl_hash)))
    if out.resolve().is_relative_to(archive.resolve().parent):
        raise FormatError('Collision destination must be outside original Levels directory')
    out.mkdir(parents=True, exist_ok=True)
    reports = []
    for number, collision, payload in values:
        path = out / f'monastery{number}.nwcol'
        if path.is_symlink(): raise FormatError('Unsafe collision destination')
        path.write_bytes(payload)
        item = dict(collision.report, resource=f'Monastery/Monastery{number}.OCT',
                    export_sha256=hashlib.sha256(payload).hexdigest())
        reports.append(item)
        print(f"Monastery{number}: {item['surfaces']} GD surfaces, {item['selected']} static colliders, {item['walkable']} walkable")
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps({'schema': 1, 'levels': reports}, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    try:
        export(args.archive, args.out, args.report)
        return 0
    except (FormatError, OSError, KeyError, struct.error) as error:
        print(f'OCT import failed: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
