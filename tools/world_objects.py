"""Read the bounded PVL object/trigger prefix; keep original records private.

Independent layout implementation using retail ReadLevel/Prop/AnimObj/Trigger
loader evidence (wizardry-8-decomp 075cbd0); no runtime action emulation.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

from tools.pvl import Reader, parse as static_level, text, transformed
from tools.slf import Archive, FormatError


class ObjectsReader(Reader):
    def value(self, fmt):
        return self.unpack(fmt)[0]

    def byte(self):
        return self.value('B')

    def count(self, fmt='i', limit=100_000):
        n = self.value(fmt)
        if not 0 <= n <= limit:
            raise FormatError(f'PVL object count outside supported bounds at {self.offset:#x}')
        return n

    def version(self, low, high, fmt='B'):
        v = self.value(fmt)
        if not low <= v <= high:
            raise FormatError(f'Unsupported PVL object version {v} at {self.offset:#x}')
        return v

    def floats(self, count):
        return [row[0] for row in self.array('f', count)]

    def vector(self):
        return self.floats(3)

    def string(self, size):
        return text(self.take(size))

    def path(self):
        self.version(0, 0)
        mode = self.version(0, 2)
        cursor = self.value('i')
        self.take(4)
        count = self.count(limit=100_000)
        # Position + axis/angle, optionally followed by a scale vector.
        frames = self.array('10f' if mode == 2 else '7f', count)
        return {'mode': mode, 'cursor': cursor, 'frames': frames}

    def materials(self):
        result = []
        for _ in range(self.count('h', 32767)):
            kind = self.version(0, 4)
            raw = self.take(297 if kind == 4 else 281)
            result.append({'kind': kind, 'name': text(raw[:40]),
                           'textures': [text(raw[i:i+40]) for i in (40,80,120,160)]})
        return result

    def mesh(self):
        start = self.offset
        version = self.version(1, 4, 'i')
        vertices, faces = self.count(limit=2_000_000), self.count(limit=2_000_000)
        if not vertices or not faces:
            raise FormatError('Empty dynamic mesh is unsupported')
        flags = self.byte() if version > 2 else 0
        if flags & ~7:
            raise FormatError('Unknown dynamic mesh flags')
        transform = self.floats(10) if version > 1 else None
        if version > 3:
            self.take(self.count('b', 127) * 4)
        lods = 1
        if flags & 1:
            compression = self.version(1, 2)
            lods = self.count('h', 32767)
            if not lods or not flags & 2:
                raise FormatError('Unsupported dynamic vertex compression')
            if compression == 2 and self.floats(1)[0] <= 0:
                raise FormatError('Invalid dynamic compression scale')
            self.take(vertices * lods * 6)
        else:
            self.array('fff', vertices)
        rows = self.array('3H6fHB' if flags & 4 else '3I6fIB', faces)
        materials = self.materials()
        if any(any(i >= vertices for i in row[:3]) or row[-2] >= len(materials) for row in rows):
            raise FormatError('Dynamic mesh vertex/material index outside bounds')
        return {'start':start, 'end':self.offset, 'version':version, 'vertices':vertices,
                'faces':faces, 'lod_frames':lods, 'transform':transform, 'materials':materials}

    def animation_mesh(self):
        count = self.count('B', 255)
        if not count:
            raise FormatError('Empty animation mesh')
        meshes, ids = [], set()
        for _ in range(count):
            frame = self.byte()
            if frame >= count or frame in ids:
                raise FormatError('Duplicate/out-of-range animation frame')
            ids.add(frame)
            mesh = self.mesh()
            meshes.append(dict(mesh, frame=frame))
            if mesh['lod_frames'] > 1:
                if mesh['lod_frames'] != count:
                    raise FormatError('Animation/LOD frame counts disagree')
                break
        return {'frame_count': count, 'meshes':meshes}

    def animation(self):
        version = self.version(1, 11)
        groups = self.count('b', 3)
        playing, method, behaviour, cycle, paths = self.unpack('5B')
        if paths not in (0, 1):
            raise FormatError('Unsupported animation path mode')
        fps = self.floats(1)[0] if version >= 3 else 15.0
        first = self.byte() if version >= 5 else 0
        last = self.byte() if version >= 11 else 0
        if version >= 6:
            self.byte(); self.floats(1)
        self.take(50)
        self.take(groups)
        if version >= 7:
            self.array('6f', self.byte())
        if version >= 8:
            for _ in range(self.count('b', 127)):
                light_version = self.version(1, 3)
                self.floats(8)
                definition = (1 if light_version == 2 else 0) if light_version < 3 else self.version(0, 2)
                if definition == 1:
                    self.take(60)
                elif definition == 2:
                    self.take(129)
        path = self.path() if not paths and version >= 9 and self.byte() else None
        if version >= 10:
            self.byte()
        entries = []
        for group in range(groups):
            n = self.count('b', 127) if paths else 1
            for _ in range(n):
                channel = self.version(0, 2)
                mesh = self.animation_mesh()
                entry_path = self.path() if paths else None
                entries.append({'group':group, 'channel':channel, 'mesh':mesh, 'path':entry_path})
        return {'version':version, 'fps':fps, 'playing':playing, 'method':method,
                'behaviour':behaviour, 'cycle':cycle, 'path_mode':paths, 'first':first,
                'last':last, 'path':path, 'entries':entries}

    def door(self):
        version = self.version(1, 1)
        flags = self.unpack('9B')
        item = self.value('H')
        positioned = self.byte()
        position = self.vector()
        return {'version':version, **dict(zip(('open','lockable','locked','auto_locking','one_way',
                'secret','found','jammable','jammed'), (bool(f & 1) for f in flags))),
                'item':item, 'position':position if positioned else None, 'linked_trigger':self.string(128)}

    def action_data(self, allow_geometry=True):
        if not self.byte():
            return None
        kind = self.version(1, 2 if allow_geometry else 1)
        if kind == 1:
            return self.door()
        self.byte(); self.array('fff', 36); self.take(2)
        return {'legacy_geometry':True}

    def trigger(self, owner):
        start = self.offset
        envelope = self.byte()
        kind = self.version(1, 4)
        version = self.version(1, {1:4, 2:5, 3:5, 4:3}[kind])
        result = {'start':start, 'owner':owner, 'kind':kind, 'version':version,
                  'name':'', 'action':None, 'recipients':'', 'position':None, 'door':None}
        if kind == 1:
            bounce, states = self.unpack('ii')
            self.floats(1)
            radius, action, unused, animate = self.unpack('4i')
            packed, enabled = self.unpack('BB')
            result.update(name=self.string(128), recipients=self.string(256), action=action,
                          state_count=states, cycle_bounce=bounce, enabled=bool(enabled))
            self.take(128)
            if version >= 2:
                self.floats(1); self.take(64)
            if version >= 3:
                result['door'] = self.action_data(False)
            if version >= 4:
                result['action_value'] = self.value('i')
        elif kind == 2:
            radius = self.floats(1)[0]
            position = self.vector()
            action, searchable, linked, enabled = self.unpack('iiBB')
            result.update(name=self.string(128), recipients=self.string(256), action=action,
                          position=position, radius=radius, enabled=bool(enabled))
            if version >= 2:
                result['plane_flag'] = self.byte()
                result['plane'] = self.array('fff', 4)
            if version >= 3:
                result['orientation'] = self.floats(4)
                self.byte(); result['action_string'] = self.string(128)
            if version >= 4:
                self.byte(); result['action_value'] = self.value('i')
            if version >= 5 and self.byte():
                self.version(2, 2); self.byte(); self.array('fff', 36); self.take(2)
        elif kind == 3:
            self.take(28); self.floats(10); self.take(128)
            if version >= 2:
                self.take(2)
            if version >= 3:
                self.floats(10)
            if version >= 4:
                result['name'] = self.string(128)
            if version >= 5:
                self.byte()
        else:
            result['name'] = self.string(128)
            packed, enabled, linked, deactivate, keep, flag, toggles = self.unpack('7B')
            action, alternate, fallback = self.unpack('3i')
            result.update(action=action, alternate_action=alternate, fallback_action=fallback,
                          enabled=bool(enabled), recipients=self.string(256))
            self.byte(); result['required_item'] = self.string(256)
            self.byte(); result['action_value'] = self.value('i')
            self.byte(); self.take(128)
            if version >= 2:
                result['data'] = list(self.unpack('3i'))
                self.floats(1); self.take(20)
            result['state_count'], animate, bounce = self.unpack('3B')
            result['required_states'] = self.string(256)
            result['modify_state'] = self.string(256)
            result['state_mode'] = self.byte()
            result['ranges'] = self.floats(2)
            result['action_string'] = self.string(256)
            self.take(4)
            if version >= 3:
                self.take(128)
            if not packed & 1:
                representation = self.version(0, 2)
                result['representation'] = representation
                if representation == 1:
                    result['position'] = self.vector()
                    result['orientation'] = self.floats(4)
                elif representation == 2:
                    result['plane'] = self.array('fff', 4)
                if self.byte():
                    self.byte(); self.floats(1); self.take(128)
            result['door'] = self.action_data()
        result['end'] = self.offset
        return result

    def prop(self, bank, index):
        start = self.offset
        version = self.version(1, 9)
        result = {'start':start, 'version':version, 'name':'', 'position':None, 'flags':0,
                  'bank':bank, 'index':index, 'trigger':None, 'segments':[]}
        if version <= 3:
            self.take(3)
            fps = self.floats(1)[0] if version >= 2 else 15.0
            result['animation'] = {'fps':fps, 'entries':[{'mesh':self.animation_mesh()}]}
        else:
            frames = self.byte()
            if version >= 5:
                self.byte(); result['position'] = self.vector()
            if version >= 6:
                result['flags'] = self.value('I')
            if version >= 7:
                result['name'] = self.string(64)
            if version >= 8:
                segments = self.array('HH', self.count('b', 127))
                if any(frame >= frames for frame, tag in segments):
                    raise FormatError('Prop segment frame outside bounds')
                result['segments'] = segments
            result['animation'] = self.animation()
            result['frame_count'] = frames
        if version >= 3 and self.byte():
            result['trigger'] = self.trigger(f'{bank}:{index}')
        if version >= 9 and self.byte():
            result['footstep'] = list(self.unpack('BB'))
        result['end'] = self.offset
        return result


def parse(data):
    level = static_level(data)
    r = ObjectsReader(data)
    r.offset = level.geometry_end
    sections = {'geometry_end':r.offset}
    lights = r.count('h', 32767)
    for _ in range(lights):
        version = r.version(0, 2, 'h')
        head = r.take(6)
        r.floats(8)
        if version >= 2:
            r.take(20)
            if head[2] & 2:
                flags = r.value('I'); r.take(56)
                if flags & 0x10:
                    r.path()
    r.marker(); sections['lights_end'] = r.offset
    monsters = r.count()
    for _ in range(monsters):
        r.take(20); r.path()
    r.marker(); sections['monsters_end'] = r.offset
    triggers = []
    items = r.count()
    for index in range(items):
        r.take(20); r.vector(); r.take(16)
        if r.byte():
            triggers.append(r.trigger(f'item:{index}'))
        r.take(19)
    r.marker(); sections['items_end'] = r.offset
    if r.count() != 0:
        raise FormatError('Nonempty PVL missile bank is unsupported')
    r.marker(); sections['missiles_end'] = r.offset
    props = []
    for bank in ('props','bitmaps'):
        props.extend(r.prop(bank, i) for i in range(r.count()))
        r.marker(); sections[bank+'_end'] = r.offset
    cameras = r.count()
    for _ in range(cameras):
        r.take(8); scale = r.byte(); r.take(20)
        if scale:
            r.floats(1)
        r.path()
    r.marker(); sections['cameras_end'] = r.offset
    environment = r.value('i')
    if environment:
        r.byte(); r.floats(5)
        mode = r.version(0, 2)
        if mode:
            r.vector()
        if mode == 2:
            r.floats(4)
        for _ in range(2):
            if r.byte():
                r.take(768)
    r.marker(); sections['environment_end'] = r.offset
    triggers.extend(r.trigger(f'level:{i}') for i in range(r.count()))
    r.marker(); sections['triggers_end'] = r.offset
    triggers += [p['trigger'] for p in props if p['trigger'] is not None]
    arrivals = [t for t in triggers if t['action'] == 0x34 and not t['recipients'] and t['position'] is not None]
    return {'schema':1, 'pvl_sha256':hashlib.sha256(data).hexdigest(), 'source_bytes':len(data),
            'sections':sections, 'unparsed_tail_bytes':len(data)-r.offset,
            'lights':lights, 'monsters':monsters, 'items':items, 'cameras':cameras,
            'props':props, 'triggers':triggers, 'arrivals':[t['start'] for t in arrivals]}


def summary(objects, resource):
    props, triggers = objects['props'], objects['triggers']
    return {k:objects[k] for k in ('schema','pvl_sha256','source_bytes','sections','unparsed_tail_bytes')} | {
        'resource':resource, 'props':sum(p['bank']=='props' for p in props),
        'bitmaps':sum(p['bank']=='bitmaps' for p in props), 'triggers':len(triggers),
        'doors':sum(t['door'] is not None and 'legacy_geometry' not in t['door'] for t in triggers),
        'arrivals':len(objects['arrivals']), 'prop_versions':dict(Counter(p['version'] for p in props)),
        'trigger_kinds':dict(Counter(t['kind'] for t in triggers)),
        'actions':dict(Counter(t['action'] for t in triggers if t['action'] is not None)),
        'lights':objects['lights'], 'monsters':objects['monsters'], 'items':objects['items'],
        'cameras':objects['cameras']}


def export(archive, out, report):
    source = Archive(archive)
    values = []
    for number in (1,2):
        resource = f'Monastery/Monastery{number}.PVL'
        objects = parse(source.read(resource))
        values.append((number, objects, summary(objects, resource)))
    if out.resolve().is_relative_to(archive.resolve().parent):
        raise FormatError('Object export must be outside the original Levels directory')
    out.mkdir(parents=True, exist_ok=True)
    for number, objects, metadata in values:
        path = out / f'monastery{number}.nwobjects.json'
        if path.is_symlink():
            raise FormatError('Refusing symlink object destination')
        path.write_text(json.dumps(objects, separators=(',',':'), allow_nan=False)+'\n', encoding='utf-8')
        print(f"Monastery{number}: {metadata['props']} props, {metadata['triggers']} triggers, {metadata['doors']} door payloads, {metadata['arrivals']} arrivals")
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps({'schema':1,'levels':[v[2] for v in values]}, indent=2)+'\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    try:
        export(args.archive, args.out, args.report)
        return 0
    except (OSError, ValueError, KeyError) as error:
        print(f'World objects failed ({type(error).__name__}): {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
