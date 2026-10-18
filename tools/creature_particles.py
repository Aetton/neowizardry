"""Independent MON emitter decoder (retail ReadWorldParticles/Monster evidence).

All distances below are converted explicitly to the preview convention. Original
records, sprite pixels and attachment tracks remain in the private asset export.
"""
import hashlib
import math
import struct
from tools import pvl, world_doors
from tools.slf import FormatError


def decode(raw, version):
    def number(offset, fmt='f'):return struct.unpack_from('<'+fmt,raw,offset)[0]
    def vector(offset):return list(struct.unpack_from('<3f',raw,offset))
    result={'version':version,'record_sha256':hashlib.sha256(raw).hexdigest(),
        'name':raw[:64].split(b'\0')[0].decode('latin1'),
        'location':vector(0x40),'angle':number(0x4c),'axis':vector(0x50),
        'capacity':number(0x68,'I'),'spread':vector(0x6c),
        'acceleration':vector(0x7c) if number(0x78,'i') else [0,0,0],
        'expiry':int(number(0x88,'i')!=0),'bounds_mode':number(0x8c,'i'),
        'bounds_origin':vector(0x90),'bounds_radius':number(0x9c),'bounds_extent':vector(0xa0),
        'lifetime_ms':number(0xac,'I'),'emission_mode':min(2,max(0,number(0xb0,'i'))),
        'interval_ms':max(1,number(0xb4,'I')),'los':bool(number(0xb8,'i')),
        'speed_mode':min(2,max(0,number(0xbc,'i'))),'speed':number(0xc0),
        'speed_min':number(0xc4),'speed_max':number(0xc8),'size':number(0xcc),
        'flutter':bool(number(0xd0,'i')),'flutter_amplitude':number(0xd4),'flutter_period_ms':number(0xd8),
        'direction_mode':min(4,max(0,number(0xdc,'i'))),'cone': [number(0xe0),number(0xe4)],
        'initially_active':bool(number(0xe8,'i')),
        'attachment_key':number(0x216,'h') if version>=2 else -1,
        'limit':number(0x218,'i') if version>=3 else 0,
        'sorted':bool(number(0x21c,'B')) if version>=3 else False,
        'start_frame':number(0x21d,'i') if version>=4 else -1,
        'end_frame':number(0x221,'i') if version>=4 else -1}
    # Read the embedded material through the same bounded parser as mesh materials.
    from tools.world_objects import ObjectsReader
    kind=raw[0xec];size=298 if kind==4 else 282
    result['material']=ObjectsReader(struct.pack('<h',1)+raw[0xec:0xec+size]).materials()[0]
    def finite(value):
        if isinstance(value,float) and not math.isfinite(value):raise FormatError('Non-finite creature emitter parameter')
        if isinstance(value,dict):
            for item in value.values():finite(item)
        if isinstance(value,(list,tuple)):
            for item in value:finite(item)
    finite(result)
    if result['capacity']>4096:raise FormatError('Creature emitter capacity exceeds budget')
    return result


def track(animation, key, fallback, first, last, scale):
    points=[];mapped=0
    for frame in range(first,last+1):
        point=[v*500 for v in fallback];found=False
        for entry in animation['entries']:
            if entry['channel']!=2:continue
            meshes=entry['mesh']['meshes']
            mesh=meshes[0] if len(meshes)==1 else next(m for m in meshes if m['frame']==frame)
            index=next((index for marker,index in mesh['vertex_map'] if marker==key),None)
            if index is None:continue
            variants=mesh['positions'];point=variants[frame if len(variants)>1 else 0][index]
            path=entry.get('path') or animation.get('path')
            if path:
                row=path['frames'][frame];sizes=row[7:10] if path['mode']==2 else [1,1,1]
                point=[a+b*500 for a,b in zip(world_doors.rotate([a*b for a,b in zip(point,sizes)],row[3],row[4:7]),row[:3])]
            found=True;break
        mapped+=found;points.append([v*scale for v in pvl.transformed(point)])
    return points,mapped


def prepare(raw, animation, first, last, scale, textures):
    result=dict(raw);result.pop('material')
    result['anchors'],result['mapped_frames']=track(animation,raw['attachment_key'],raw['location'],first,last,scale)
    result['scale']=scale
    def direction(point, correction):
        point=world_doors.rotate(point,raw['angle'],raw['axis'])
        point=world_doors.rotate(point,correction,[1,0,0])
        return [point[0],point[1],-point[2]]
    # Reflection C R C maps both the native local and world coordinate systems.
    result['basis']=[direction([1,0,0],math.pi/2),direction([0,1,0],math.pi/2),direction([0,0,-1],math.pi/2)]
    result['fixed_direction']=direction([0,0,-1],-math.pi/2)
    material=raw['material'];selected=next((t for t in material['textures'] if t and t.casefold()!='notexture'),None)
    frames=textures.frames(selected) if selected else []
    fps=material['fps'] if len(frames)>1 else 1
    if not math.isfinite(fps) or not 0<fps<=240:raise FormatError('Invalid creature particle sprite rate')
    result['sprite_fps']=fps;result['images']=[frame['name'].casefold() for frame in frames]
    result['opacity']=material['opacity'] if material['opacity']>0 else 1
    result['blocked_reason']='native line-of-sight collision requires world collision adapter' if raw['los'] else ''
    if raw['name'].startswith('CLOUD'):result['blocked_reason']='camera-relative emitter requires native camera adapter'
    if not frames:result['blocked_reason']='particle material has no supported sprite'
    return result,frames
