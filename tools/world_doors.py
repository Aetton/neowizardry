"""Private native door-frame preview; no native trigger callback emulation."""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import sys

from tools import pvl, world_objects
from tools.slf import Archive, FormatError
from tools.textures import Textures


def rotate(position, angle, axis):
    if angle == 0:
        return list(position)
    length = math.sqrt(sum(x*x for x in axis))
    if not 0.999 <= length <= 1.001:
        raise FormatError('Door path rotation axis is not unit length')
    x,y,z = [v/length for v in axis]
    a,b,c = position
    co,si = math.cos(angle),math.sin(angle)
    dot = x*a+y*b+z*c
    return [a*co+(y*c-z*b)*si+x*dot*(1-co),
            b*co+(z*a-x*c)*si+y*dot*(1-co),
            c*co+(x*b-y*a)*si+z*dot*(1-co)]


def frame_geometry(prop, frame, materials, material_ids):
    animation = prop['animation']
    entries = [e for e in animation['entries'] if e.get('channel',2)==2]
    if not entries:
        raise FormatError('Door has no active channel-2 geometry')
    positions, indices, uv, uv_indices, face_materials = [],[],[],[],[]
    for entry in entries:
        definition = entry['mesh']
        meshes = definition['meshes']
        if len(meshes)==1:
            mesh = meshes[0]
        else:
            mesh = next((m for m in meshes if m['frame']==frame),None)
            if mesh is None:
                raise FormatError('Door mesh frame is missing')
        variants = mesh['positions']
        if len(variants)>1 and frame>=len(variants):
            raise FormatError('Door vertex frame is missing')
        vertices = variants[frame if len(variants)>1 else 0]
        path = entry.get('path') or animation.get('path')
        if path:
            if frame >= len(path['frames']):
                raise FormatError('Door path frame is missing')
            key = path['frames'][frame]
            origin = [v*500 for v in key[:3]]
            angle, axis = key[3],key[4:7]
            scale = key[7:10] if path['mode']==2 else [1,1,1]
        else:
            origin = [v*500 for v in (prop['position'] or [0,0,0])]
            angle,axis,scale = 0,[0,1,0],[1,1,1]
        base = len(positions)
        for vertex in vertices:
            rotated = rotate([v*s for v,s in zip(vertex,scale)],angle,axis)
            positions.append([v+o for v,o in zip(rotated,origin)])
        local_materials = []
        for material in mesh['materials']:
            key = json.dumps(material,sort_keys=True,allow_nan=False)
            if key not in material_ids:
                material_ids[key] = len(materials)
                materials.append(material)
            local_materials.append(material_ids[key])
        for polygon in mesh['polygons']:
            indices.append(tuple(base+v for v in polygon[:3]))
            start = len(uv)
            uv.extend([polygon[3:5],polygon[5:7],polygon[7:9]])
            uv_indices.append((start,start+1,start+2))
            face_materials.append(local_materials[polygon[-2]])
    return pvl.Mesh(positions,uv,uv_indices,indices,face_materials,[],0,0)


def restriction(trigger):
    door = trigger['door']
    if not trigger.get('enabled',False): return 'disabled'
    if door['secret'] and not door['found']: return 'undiscovered'
    if door['jammed']: return 'jammed'
    if door['locked']: return 'locked; party/key integration pending'
    if trigger.get('required_item'): return 'required item integration pending'
    if trigger.get('required_states') or trigger.get('modify_state'): return 'world state integration pending'
    if trigger['recipients'] or door['linked_trigger']: return 'linked trigger integration pending'
    if door['one_way'] or door['auto_locking']: return 'direction/timer integration pending'
    if trigger['action'] not in (1,2): return 'unsupported trigger action'
    if trigger['action']==1 and trigger.get('data',[0,0,0])[0]!=0: return 'automatic closing timer pending'
    return ''


def build(data, textures=None):
    objects = world_objects.parse(data,geometry=True)
    materials, material_ids, meshes, doors = [],{},[],[]
    for prop in objects['props']:
        trigger = prop['trigger']
        if trigger is None or trigger['door'] is None or trigger['door'].get('legacy_geometry'):
            continue
        animation = prop['animation']
        entries = [e for e in animation['entries'] if e.get('channel',2)==2]
        if not entries: raise FormatError('Native door lacks active geometry')
        path_counts = [len((e.get('path') or animation.get('path'))['frames']) for e in entries if e.get('path') or animation.get('path')]
        counts = path_counts or [e['mesh']['frame_count'] for e in entries]
        count = counts[0]
        if not 1 <= count <= 255 or any(n!=count for n in counts):
            raise FormatError('Unsupported native door frame span')
        fps = animation['fps']
        if not math.isfinite(fps) or not 0 < fps <= 240:
            raise FormatError('Unsupported native door animation speed')
        frames = []
        for frame in range(count):
            frames.append(len(meshes))
            meshes.append(frame_geometry(prop,frame,materials,material_ids))
        doors.append({'id':prop['start'], 'trigger_id':trigger['start'], 'name':prop['name'],
                      'frames':frames, 'bounds':[pvl.bounds([pvl.transformed(meshes[i].positions[v]) for face in meshes[i].indices for v in face]) for i in frames],
                      'fps':fps, 'open':trigger['door']['open'],
                      'blocked_reason':restriction(trigger) or ('no moving frames' if count<2 else ''), 'flags':prop['flags']})
    if not doors:
        raise FormatError('No native door geometry')
    level = pvl.Level(0,materials,meshes,0,0)
    payload = pvl.glb(level,textures,objects['pvl_sha256'])
    manifest = {'schema':1,'pvl_sha256':objects['pvl_sha256'],
                'mesh_sha256':hashlib.sha256(payload).hexdigest(), 'doors':doors}
    metadata = {'schema':1, 'pvl_sha256':objects['pvl_sha256'], 'doors':len(doors),
                'frames':len(meshes), 'triangles':sum(len(m.indices) for m in meshes),
                'interactive':sum(not d['blocked_reason'] for d in doors),
                'collidable':sum(bool(d['flags']&1) for d in doors),
                'restrictions':dict(Counter(d['blocked_reason'] for d in doors if d['blocked_reason'])),
                'frame_bounds':[pvl.bounds([pvl.transformed(m.positions[v]) for face in m.indices for v in face]) for m in meshes]}
    return payload,manifest,metadata


def export(archive, out, report, textured=True):
    source = Archive(archive)
    values = []
    for number in (1,2):
        resource = f'Monastery/Monastery{number}.PVL'
        textures = Textures(source,resource) if textured else None
        payload, manifest, metadata = build(source.read(resource),textures)
        values.append((number,payload,manifest,dict(metadata,resource=resource)))
    if out.resolve().is_relative_to(archive.resolve().parent):
        raise FormatError('Door export must be outside original Levels directory')
    out.mkdir(parents=True,exist_ok=True)
    for number,payload,manifest,metadata in values:
        for suffix,content in (('.nwdoors.glb',payload),('.nwdoors.json',(json.dumps(manifest,allow_nan=False)+'\n').encode())):
            path = out/f'monastery{number}{suffix}'
            if path.is_symlink(): raise FormatError('Refusing symlink door destination')
            path.write_bytes(content)
        print(f"Monastery{number}: {metadata['doors']} native door models, {metadata['frames']} frames, {metadata['interactive']} eligible for manual interaction")
    report.parent.mkdir(parents=True,exist_ok=True)
    report.write_text(json.dumps({'schema':1,'levels':[v[3] for v in values]},allow_nan=False,indent=2)+'\n',encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive',type=Path)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--report',type=Path,required=True)
    args = parser.parse_args()
    try:
        export(args.archive,args.out,args.report)
        return 0
    except (OSError,ValueError,KeyError) as error:
        print(f'Door export failed ({type(error).__name__}): {error}',file=sys.stderr)
        return 1


if __name__=='__main__': sys.exit(main())
