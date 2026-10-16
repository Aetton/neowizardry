"""Strict static PVL geometry reader and private, untextured GLB preview exporter.

Format research: Wizardry8Editor/Window3DNavigator.cpp at
4d451e8e31106cc5d6ad86b69a00b0d2a3c26c01 (Anonymous Idiot, BSD-2-Clause).
Independent implementation; no original game assets are distributed.
"""
from __future__ import annotations

import argparse
import colorsys
from collections import Counter, defaultdict
from dataclasses import dataclass
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

from tools.slf import Archive, FormatError

PREVIEW_SCALE = 0.0013  # Editor viewing convention, not a claim about native units.


class Reader:
    def __init__(self, data: bytes):
        self.data, self.offset = data, 0

    def take(self, size: int) -> bytes:
        if size < 0 or size > len(self.data) - self.offset:
            raise FormatError(f"PVL truncated at {self.offset:#x} (need {size} bytes)")
        start = self.offset
        self.offset += size
        return self.data[start:self.offset]

    def unpack(self, fmt: str):
        return struct.unpack('<' + fmt, self.take(struct.calcsize('<' + fmt)))

    def marker(self):
        if self.unpack('i')[0] != -1:
            raise FormatError(f"PVL section marker mismatch at {self.offset - 4:#x}")

    def array(self, fmt: str, count: int):
        if not 0 <= count <= 2_000_000:
            raise FormatError('PVL array count outside supported bounds')
        size = struct.calcsize('<' + fmt)
        result = list(struct.iter_unpack('<' + fmt, self.take(count * size)))
        if 'f' in fmt and any(not math.isfinite(x) for row in result for x in row):
            raise FormatError('Non-finite PVL geometry value')
        return result


@dataclass
class Mesh:
    positions: list
    uv: list
    uv_indices: list
    indices: list
    materials: list
    normals: list
    start: int
    end: int


@dataclass
class Level:
    header_unknown: int
    materials: list
    meshes: list[Mesh]
    geometry_end: int
    trailing_bytes: int


def text(raw: bytes) -> str:
    return raw.split(b'\0', 1)[0].decode('latin1')


def parse(data: bytes) -> Level:
    r = Reader(data)
    mesh_count, unknown, material_count = r.unpack('iih')
    if not 0 <= mesh_count <= 100_000 or not 0 <= material_count <= 32767:
        raise FormatError('Unsupported PVL header counts')
    materials = []
    for _ in range(material_count):
        kind = r.unpack('B')[0]
        raw = r.take(297 if kind == 4 else 281)
        materials.append({'type': kind, 'name': text(raw[:40]),
                          'textures': [text(raw[i:i+40]) for i in (40, 80, 120, 160)]})
    r.marker()
    meshes = []
    for _ in range(mesh_count):
        start = r.offset
        sunlight, vertices, uv_count, faces = r.unpack('iIiI')
        r.take(8)
        vertex_materials = r.unpack('i')[0]
        positions = r.array('fff', vertices)
        uv = r.array('ff', uv_count)
        if vertex_materials < 0:
            r.take(vertices * 4)
        uv_indices = r.array('III', faces)
        indices = r.array('III', faces)
        face_materials = [x[0] for x in r.array('i', faces)]
        normals = r.array('fff', vertices)
        r.take(vertices * 12)  # static lighting
        r.take(faces * 16)  # polygon equations, preserved as unknown
        if (sunlight >> 8) & 255:
            r.take(vertices * 4)
        if any(i >= vertices for row in indices for i in row):
            raise FormatError('PVL vertex index out of range')
        if any(i >= uv_count for row in uv_indices for i in row):
            raise FormatError('PVL UV index out of range')
        if any(not 0 <= i < material_count for i in face_materials):
            raise FormatError('PVL material index out of range')
        end = r.offset
        r.marker()
        meshes.append(Mesh(positions, uv, uv_indices, indices, face_materials, normals, start, end))
    r.marker()
    return Level(unknown, materials, meshes, r.offset, len(data) - r.offset)


def transformed(position):
    # Explicit preview convention: preserve X/Y, mirror Z; reverse triangle winding below.
    return (position[0] * PREVIEW_SCALE, position[1] * PREVIEW_SCALE, -position[2] * PREVIEW_SCALE)


def bounds(points):
    if not points:
        return None
    return [[min(p[i] for p in points) for i in range(3)],
            [max(p[i] for p in points) for i in range(3)]]


def summary(level: Level, data: bytes, resource: str) -> dict:
    return {'schema': 1, 'resource': resource, 'source_sha256': hashlib.sha256(data).hexdigest(),
            'source_bytes': len(data), 'header_unknown': level.header_unknown,
            'meshes': len(level.meshes), 'materials': len(level.materials),
            'vertices': sum(len(m.positions) for m in level.meshes),
            'triangles': sum(len(m.indices) for m in level.meshes),
            'geometry_end': level.geometry_end, 'unparsed_tail_bytes': level.trailing_bytes,
            'native_bounds': bounds([p for m in level.meshes for p in m.positions]),
            'material_types': dict(Counter(m['type'] for m in level.materials)),
            'texture_references': sorted({t for m in level.materials for t in m['textures'] if t}),
            'preview': {'scale': PREVIEW_SCALE, 'axes': '(x,y,-z)',
                        'render_bounds': bounds([transformed(m.positions[i]) for m in level.meshes for face in m.indices for i in face]),
                        'textures': False,
                        'collision': False, 'native_spawn': False, 'gameplay_connected': False}}


def glb(level: Level) -> bytes:
    binary = bytearray()
    doc = {'asset': {'version': '2.0', 'generator': 'NeoWizardry static PVL preview'},
           'scene': 0, 'scenes': [{'nodes': []}], 'nodes': [], 'meshes': [],
           'bufferViews': [], 'accessors': [], 'materials': []}
    for i, material in enumerate(level.materials):
        # Authored debug colors; native materials/textures are not reconstructed here.
        hue = (i * 0.61803398875) % 1
        rgb = colorsys.hsv_to_rgb(hue, 0.18, 0.72)
        doc['materials'].append({'name': material['name'], 'doubleSided': True,
                                 'pbrMetallicRoughness': {'baseColorFactor': [*rgb, 1],
                                                          'metallicFactor': 0, 'roughnessFactor': 1}})

    def accessor(rows, width, kind, include_bounds=False):
        offset = len(binary)
        for row in rows:
            binary.extend(struct.pack('<' + 'f' * width, *row))
        view = len(doc['bufferViews'])
        doc['bufferViews'].append({'buffer': 0, 'byteOffset': offset,
                                  'byteLength': len(binary) - offset, 'target': 34962})
        item = {'bufferView': view, 'componentType': 5126, 'count': len(rows), 'type': kind}
        if include_bounds:
            box = bounds(rows)
            item.update(min=box[0], max=box[1])
        doc['accessors'].append(item)
        return len(doc['accessors']) - 1

    for number, mesh in enumerate(level.meshes):
        groups = defaultdict(list)
        for face, material in enumerate(mesh.materials):
            groups[material].append(face)
        primitives = []
        for material, faces in sorted(groups.items()):
            positions, uv = [], []
            for face in faces:
                for corner in (0, 2, 1):
                    positions.append(transformed(mesh.positions[mesh.indices[face][corner]]))
                    uv.append(mesh.uv[mesh.uv_indices[face][corner]])
            primitives.append({'attributes': {'POSITION': accessor(positions, 3, 'VEC3', True),
                                               'TEXCOORD_0': accessor(uv, 2, 'VEC2')},
                               'mode': 4, 'material': material})
        if primitives:
            doc['nodes'].append({'name': f'PVL_mesh_{number}', 'mesh': len(doc['meshes'])})
            doc['scenes'][0]['nodes'].append(len(doc['nodes']) - 1)
            doc['meshes'].append({'primitives': primitives})
    if not doc['meshes']:
        raise FormatError('PVL has no renderable static triangles')
    doc['buffers'] = [{'byteLength': len(binary)}]
    payload = json.dumps(doc, separators=(',', ':'), allow_nan=False).encode()
    payload += b' ' * (-len(payload) % 4)
    binary += b'\0' * (-len(binary) % 4)
    total = 12 + 8 + len(payload) + 8 + len(binary)
    return (struct.pack('<III', 0x46546c67, 2, total) + struct.pack('<II', len(payload), 0x4e4f534a)
            + payload + struct.pack('<II', len(binary), 0x004e4942) + binary)


def export(archive: Path, out: Path, report: Path):
    source = Archive(archive)
    reports = []
    # Parse both before writing any output, so unsupported levels cannot appear successful.
    levels = []
    for number in (1, 2):
        resource = f'Monastery/Monastery{number}.PVL'
        data = source.read(resource)
        level = parse(data)
        levels.append((number, level, data, resource))
    if out.resolve().is_relative_to(archive.resolve().parent):
        raise FormatError('Preview destination must be outside the original Levels directory')
    payloads = [glb(level) for _, level, _, _ in levels]
    out.mkdir(parents=True, exist_ok=True)
    for (number, level, data, resource), payload in zip(levels, payloads):
        target = out / f'monastery{number}.glb'
        if target.is_symlink():
            raise FormatError('Refusing symlink preview destination')
        target.write_bytes(payload)
        record = summary(level, data, resource)
        record['glb_sha256'] = hashlib.sha256(payload).hexdigest()
        record['glb_bytes'] = len(payload)
        reports.append(record)
        print(f"Monastery{number}: {record['meshes']} meshes, {record['triangles']} triangles")
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
    except (OSError, FormatError, KeyError, ValueError, struct.error) as error:
        print(f'PVL import failed: {type(error).__name__}: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
