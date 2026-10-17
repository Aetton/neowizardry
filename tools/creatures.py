"""Private MLS/MON creature frames, independently read from retail loader evidence.

Reference: wizardry-8-decomp 075cbd0, Monster/GrCycle/AnimObj/ReadMesh.
Only bounded channel-2 geometry is exported; particle cycles fail closed.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys

from tools import pvl, world_doors
from tools.world_objects import ObjectsReader
from tools.slf import Archive, Resources, FormatError, canonical
from tools.textures import Textures

CYCLES = {'IDLE':1, 'WALK':4, 'ATTACK_CLOSE':6, 'DODGE':19, 'GET_HIT':20}


class CreatureResources:
    def __init__(self, data, monsters, patches=(), loose=None):
        self.shared = Resources(Archive(data), [Archive(p) for p in patches], loose)
        self.monsters = Archive(monsters)

    def resolve(self, name):
        name = canonical(name)
        try:
            return self.shared.resolve(name)
        except KeyError:
            if not name.upper().startswith('MONSTERS/'):
                raise
            data = self.monsters.read(name[len('MONSTERS/'):])
            return data, {'kind':'archive', 'file':self.monsters.path.name, 'sha256':hashlib.sha256(data).hexdigest()}

    def read(self, name):
        return self.resolve(name)[0]


def basename(name):
    if not re.fullmatch(r'[A-Za-z0-9_ -]{1,48}', name):
        raise FormatError('Unsupported creature resource basename')
    return name


def mls(data):
    if len(data) > 1024*1024:
        raise FormatError('MLS exceeds supported size')
    selected, scale, default_skin = {}, 1.0, True
    for line in data.decode('latin1').splitlines():
        tokens = line.split('#',1)[0].split()
        if not tokens:
            continue
        name = tokens[0].upper()
        if name == 'SKIN':
            default_skin = len(tokens)==2 and tokens[1].upper()=='DEFAULT'
        if name == 'SKINSWAP' and default_skin:
            raise FormatError('Default creature skin swaps are unsupported')
        if name in ('OPACITY','GLOW'):
            raise FormatError('Creature material overrides are unsupported')
        if name == 'SCALEFACTOR':
            if len(tokens)!=2: raise FormatError('Invalid MLS scale')
            scale = float(tokens[1])
            if not math.isfinite(scale) or not 0 < scale <= 100:
                raise FormatError('Unsupported MLS scale')
        if name in CYCLES and name not in selected:
            if len(tokens) not in (2,3): raise FormatError('Invalid MLS cycle')
            fps = float(tokens[2]) if len(tokens)==3 else None
            if fps is not None and (not math.isfinite(fps) or not 0 < fps <= 240):
                raise FormatError('Unsupported MLS playback rate')
            selected[name] = (basename(tokens[1]), fps)
    if 'IDLE' not in selected:
        raise FormatError('MLS has no supported IDLE cycle')
    return scale, selected


def mon(data):
    r = ObjectsReader(data, geometry=True)
    r.version(1,1)
    if r.byte(): raise FormatError('Creature root paths are unsupported')
    if r.byte(): raise FormatError('Creature particle attachments are unsupported')
    animation = r.animation()
    if r.offset != len(data): raise FormatError('Unexpected MON trailing bytes')
    active = [e for e in animation['entries'] if e['channel']==2]
    if not active: raise FormatError('Creature has no channel-2 geometry')
    counts = [len((e.get('path') or animation.get('path'))['frames']) if e.get('path') or animation.get('path') else e['mesh']['frame_count'] for e in active]
    if len(set(counts))!=1 or not 1 <= counts[0] <= 255:
        raise FormatError('Inconsistent creature frame spans')
    first, last = animation['first'], animation['last'] or counts[0]-1
    if not 0 <= first <= last < counts[0]: raise FormatError('Invalid creature cycle endpoints')
    if not math.isfinite(animation['fps']) or not 0 < animation['fps'] <= 240:
        raise FormatError('Invalid MON playback rate')
    return animation, first, last


def build(resources, definition_id):
    database, db_source = resources.resolve('DATABASES/MONSTERS.DBS')
    if len(database)<4: raise FormatError('Truncated creature database')
    count = struct.unpack_from('<I',database)[0]
    if len(database)!=4+count*663 or not 0 <= definition_id < count:
        raise FormatError('Invalid creature definition')
    record = database[4+definition_id*663:4+(definition_id+1)*663]
    slot = record[0x189:0x1b9]
    if b'\0' not in slot: raise FormatError('Unterminated creature cycle name')
    name = basename(slot.split(b'\0',1)[0].decode('ascii'))
    resource = f'MONSTERS/{name}.MLS'
    text, mls_source = resources.resolve(resource)
    scale, choices = mls(text)
    materials, material_ids, meshes, clips, sources = [],{},[],[],[]
    total_faces = 0
    for cycle,(filename,fps) in choices.items():
        path = f'MONSTERS/{filename}.MON'
        data, provenance = resources.resolve(path)
        animation, first, last = mon(data)
        frames, boxes = [],[]
        for frame in range(first,last+1):
            geometry = world_doors.frame_geometry({'animation':animation,'position':[0,0,0]},frame,materials,material_ids)
            geometry.positions = [[v*scale for v in row] for row in geometry.positions]
            total_faces += len(geometry.indices)
            if total_faces > 2_000_000: raise FormatError('Creature frame triangle budget exceeded')
            frames.append(len(meshes));meshes.append(geometry)
            boxes.append(pvl.bounds([pvl.transformed(geometry.positions[v]) for face in geometry.indices for v in face]))
        clips.append({'cycle':CYCLES[cycle], 'name':cycle, 'frames':frames, 'bounds':boxes,
                      'fps':fps or animation['fps'], 'first':first, 'last':last})
        sources.append({'resource':path, **provenance})
    textures = Textures(resources,resource)
    payload = pvl.glb(pvl.Level(0,materials,meshes,0,0),textures)
    manifest = {'schema':1, 'definition_id':definition_id,
                'database_sha256':hashlib.sha256(database).hexdigest(), 'record_sha256':hashlib.sha256(record).hexdigest(),
                'cycle_name':name, 'mesh_sha256':hashlib.sha256(payload).hexdigest(), 'scale':scale,
                'clips':clips, 'textures':textures.report(), 'sources':[{'resource':'DATABASES/MONSTERS.DBS',**db_source},{'resource':resource,**mls_source},*sources]}
    report = {'schema':1,'definition_id':definition_id,'clips':len(clips),'frames':len(meshes),
              'frame_triangles':total_faces,'images':len(textures.images),
              'mesh_sha256':manifest['mesh_sha256'], 'database_sha256':manifest['database_sha256'],
              'record_sha256':manifest['record_sha256'], 'limitations':['particle/death effects','damage skins','random scale/hover','native event timing']}
    return payload, manifest, report


def export(resources, definition_id, out, report):
    payload, manifest, metadata = build(resources,definition_id)
    out.mkdir(parents=True,exist_ok=True)
    for suffix,data in (('.nwcreature.glb',payload),('.nwcreature.json',(json.dumps(manifest,allow_nan=False)+'\n').encode())):
        path = out/f'creature-{definition_id}{suffix}'
        if path.is_symlink(): raise FormatError('Refusing symlink creature destination')
        path.write_bytes(data)
    report.parent.mkdir(parents=True,exist_ok=True)
    report.write_text(json.dumps(metadata,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(f"Creature {definition_id}: {metadata['clips']} clips, {metadata['frames']} frames, {metadata['images']} images")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('data',type=Path);parser.add_argument('monsters',type=Path)
    parser.add_argument('--definition',type=int,default=282)
    parser.add_argument('--patch',action='append',type=Path,default=[])
    parser.add_argument('--loose-dir',type=Path)
    parser.add_argument('--out',type=Path,required=True);parser.add_argument('--report',type=Path,required=True)
    args = parser.parse_args()
    try:
        if any(target.resolve().is_relative_to(source.parent.resolve()) for target in (args.out,args.report) for source in (args.data,args.monsters)):
            raise FormatError('Creature outputs must be outside original archive directories')
        if args.loose_dir and args.out.resolve().is_relative_to(args.loose_dir.resolve()):
            raise FormatError('Creature export must be outside original Data directory')
        export(CreatureResources(args.data,args.monsters,args.patch,args.loose_dir),args.definition,args.out,args.report)
        return 0
    except (OSError,ValueError,KeyError) as error:
        print(f'Creature export failed ({type(error).__name__}): {error}',file=sys.stderr);return 1


if __name__=='__main__':sys.exit(main())
