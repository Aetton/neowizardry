"""Private MLS/MON creature frames, independently read from retail loader evidence.

Reference: wizardry-8-decomp 075cbd0, Monster/GrCycle/AnimObj/ReadMesh.
Channel-2 geometry is exported for all named cycles/variants. Particle records
are bounded and reported; their emitters are not approximated as native effects.
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

CYCLES = {name:i for i,name in enumerate(('BIRTH','IDLE','SPICE','TRANSITION','WALK','DRAW',
    'ATTACK_CLOSE','ATTACK_RANGED','UNUSED','ATTACK_SWING','ATTACK_THRUST','ATTACK_BASH',
    'ATTACK_MELEE','ATTACK_THROW','ATTACK_PUNCH','ATTACK_KICK','ATTACK_LASH','ATTACK_SHOOT',
    'ATTACK_SPECIAL','DODGE','GET_HIT','DIE','TURN','TALK_SPICE','TALK','SPELL','SPECIAL'))}
EXPORT_VERSION = 2


def cycle_id(name):
    # Native ParseMonsterCycleName accepts numbered variants after the base token.
    return next((value for base,value in CYCLES.items() if re.fullmatch(re.escape(base)+r'\d*',name.upper())),None)



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


def script(data):
    if len(data) > 1024*1024:
        raise FormatError('MLS exceeds supported size')
    selected, scale, stages, current, opacity, glow = [], 1.0, [[]], 0, None, 0.0
    for line in data.decode('latin1').splitlines():
        tokens = line.split('#',1)[0].split()
        if not tokens:
            continue
        name = tokens[0].upper()
        if name == 'SKIN':
            if len(tokens)>2 or len(tokens)==2 and tokens[1].upper()!='DEFAULT':
                raise FormatError('Invalid MLS skin stage')
            if len(tokens)==1:
                stages.append([]);current=len(stages)-1
            else: current=0
        elif name == 'SKINSWAP':
            if len(tokens)!=3 or any('/' in canonical(t) for t in tokens[1:]):
                raise FormatError('Invalid MLS skin replacement')
            stages[current].append(tokens[1:])
        elif name in ('SCALEFACTOR','OPACITY','GLOW'):
            if len(tokens)!=2: raise FormatError('Invalid MLS material/scale value')
            value=float(tokens[1])
            if not math.isfinite(value):raise FormatError('Non-finite MLS material/scale value')
            if name=='SCALEFACTOR':
                if not 0<value<=100:raise FormatError('Unsupported MLS scale')
                scale=value
            else:
                if not 0<=value<=1:raise FormatError('Unsupported MLS material override')
                if name=='OPACITY':opacity=value
                else:glow=value
        elif cycle_id(name) is not None:
            if len(tokens) not in (2,3):raise FormatError('Invalid MLS cycle')
            fps=float(tokens[2]) if len(tokens)==3 else None
            if fps is not None and (not math.isfinite(fps) or not 0<fps<=240):
                raise FormatError('Unsupported MLS playback rate')
            selected.append({'cycle':cycle_id(name),'name':name,'file':basename(tokens[1]),'fps':fps})
    if not any(c['cycle']==1 for c in selected):raise FormatError('MLS has no supported IDLE cycle')
    if len(selected)>128 or len(stages)>16:raise FormatError('MLS clip/skin budget exceeded')
    return {'scale':scale,'clips':selected,'skins':stages,'opacity':opacity,'glow':glow}


def mls(data):
    # Legacy caller view; the complete parser retains all subcycles in source order.
    spec=script(data)
    return spec['scale'], {c['name']:(c['file'],c['fps']) for c in spec['clips']}


def mon(data):
    r = ObjectsReader(data, geometry=True)
    r.version(1,1)
    if r.version(0,1):raise FormatError('Creature root paths are unsupported')
    particles=[]
    if r.version(0,1):
        for _ in range(r.count(limit=4096)):
            version=r.version(1,4)
            raw=r.take({1:0x216,2:0x218,3:0x21d,4:0x225}[version])
            particles.append({'version':version,'record_sha256':hashlib.sha256(raw).hexdigest()})
    animation = r.animation()
    if r.offset != len(data): raise FormatError('Unexpected MON trailing bytes')
    animation['particles']=particles
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


def apply_skins(materials,spec):
    def replace(name,pairs):
        for old,new in pairs:
            if name.casefold()==old.casefold():name=new
        return name
    for material in materials:
        original=material['textures']
        base=[replace(t,spec['skins'][0]) for t in original]
        selected=next((i for i,t in enumerate(base) if t and t.casefold()!='notexture'),None)
        material['textures']=base
        if selected is not None:
            material['creature_skins']=[base[selected]]+[replace(base[selected],pairs) for pairs in spec['skins'][1:]]
        material['creature_opacity']=spec['opacity']
        material['creature_glow']=spec['glow']


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
    spec=script(text);scale=spec['scale']
    materials, material_ids, meshes, clips, sources = [],{},[],[],[]
    total_faces, variants, geometry_cache, blocked, particle_clips = 0,{}, {}, [],[]
    for choice in spec['clips']:
        cycle=choice['cycle'];path=f"MONSTERS/{choice['file']}.MON"
        variant=variants.get(cycle,0);variants[cycle]=variant+1
        try:
            data, provenance = resources.resolve(path)
            sources.append({'resource':path, **provenance})
            animation, first, last = mon(data)
            key=hashlib.sha256(data).hexdigest()
            if key in geometry_cache:
                frames,boxes=geometry_cache[key]
            else:
                frames,boxes=[],[]
                candidate=[]
                for frame in range(first,last+1):
                    geometry = world_doors.frame_geometry({'animation':animation,'position':[0,0,0]},frame,materials,material_ids)
                    geometry.positions = [[v*scale for v in row] for row in geometry.positions]
                    candidate.append(geometry)
                count_faces=sum(len(g.indices) for g in candidate)
                if total_faces+count_faces>2_000_000:raise FormatError('Creature frame triangle budget exceeded')
                for geometry in candidate:
                    frames.append(len(meshes));meshes.append(geometry)
                    boxes.append(pvl.bounds([pvl.transformed(geometry.positions[v]) for face in geometry.indices for v in face]))
                total_faces+=count_faces;geometry_cache[key]=(frames,boxes)
            if animation['particles']:
                particle_clips.append({'cycle':cycle,'variant':variant,'attachments':len(animation['particles'])})
            clips.append({'cycle':cycle,'variant':variant,'name':choice['name'],'frames':frames,'bounds':boxes,
                'fps':choice['fps'] or animation['fps'],'first':first,'last':last,
                'particle_attachments':len(animation['particles'])})
        except (KeyError,FormatError,ValueError) as error:
            blocked.append({'cycle':cycle,'variant':variant,'resource':path,'reason':str(error)})
    if not any(c['cycle']==1 for c in clips):raise FormatError('No renderable IDLE: '+json.dumps(blocked))
    # Failed optional clips may have interned materials; remove unreferenced ones.
    used=sorted({i for mesh in meshes for i in mesh.materials}); remap={old:new for new,old in enumerate(used)}
    materials=[materials[i] for i in used]
    for mesh in meshes:mesh.materials=[remap[i] for i in mesh.materials]
    apply_skins(materials,spec)
    textures = Textures(resources,resource)
    payload = pvl.glb(pvl.Level(0,materials,meshes,0,0),textures)
    if len(payload)>128*1024*1024:raise FormatError('Creature GLB exceeds runtime size limit')
    limitations=['particle emitters' if particle_clips else None,'native event timing/sound','random scale/hover','native lighting/blend parity']
    limitations=[x for x in limitations if x]
    manifest = {'schema':2,'export_version':EXPORT_VERSION,'definition_id':definition_id,
        'database_sha256':hashlib.sha256(database).hexdigest(),'record_sha256':hashlib.sha256(record).hexdigest(),
        'cycle_name':name,'mesh_sha256':hashlib.sha256(payload).hexdigest(),'scale':scale,
        'skin_stages':len(spec['skins']),'clips':clips,'blocked_clips':blocked,'particle_clips':particle_clips,
        'textures':textures.report(),'limitations':limitations,
        'sources':[{'resource':'DATABASES/MONSTERS.DBS',**db_source},{'resource':resource,**mls_source},*sources]}
    report={'schema':2,'definition_id':definition_id,'clips':len(clips),'frames':len(meshes),
        'frame_triangles':total_faces,'images':len(textures.images),'skin_stages':len(spec['skins']),
        'mesh_sha256':manifest['mesh_sha256'],'database_sha256':manifest['database_sha256'],
        'record_sha256':manifest['record_sha256'],'blocked_clips':blocked,'particle_clips':particle_clips,'limitations':limitations}
    return payload,manifest,report


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


def write_file(path,data):
    if path.is_symlink():raise FormatError('Refusing symlink creature destination')
    path.parent.mkdir(parents=True,exist_ok=True)
    temporary=path.with_name(path.name+'.tmp')
    if temporary.is_symlink():raise FormatError('Refusing symlink temporary destination')
    temporary.write_bytes(data);temporary.replace(path)


def json_file(path,value):
    write_file(path,(json.dumps(value,allow_nan=False,separators=(',',':'))+'\n').encode())


def fingerprint(resources):
    digest=hashlib.sha256()
    paths=[resources.shared.base.path,resources.monsters.path,*[p.path for p in resources.shared.patches]]
    # Include relevant loose overrides, including newly created files. No mtime cache.
    if resources.shared.loose:
        paths+=sorted(p for p in resources.shared.loose.values() if p.suffix.casefold() in ('.mls','.mon','.tga','.ifl','.dbs'))
    paths += [Path(__file__).with_name(n) for n in ('creatures.py','world_objects.py','world_doors.py','pvl.py','textures.py','slf.py')]
    for path in paths:
        digest.update(str(path).encode())
        with path.open('rb') as stream:
            for chunk in iter(lambda:stream.read(1024*1024),b''):digest.update(chunk)
    return digest.hexdigest()


def export_all(resources,out,report):
    from tools.monsters import monster_catalog
    catalog=monster_catalog(resources);key=fingerprint(resources)
    out.mkdir(parents=True,exist_ok=True);json_file(out/'monsters.json',catalog)
    groups={};entries=[];totals={'models':0,'frames':0,'frame_triangles':0,'bytes':0,'clips':0,'particle_clips':0,'blocked_clips':0}
    for row in catalog['monsters']:
        raw=bytes.fromhex(row['raw_record_hex']);name=raw[0x189:0x1b9].split(b'\0')[0].decode('ascii')
        groups.setdefault(name.casefold(),[]).append(row)
    for ordinal,rows in enumerate(groups.values(),1):
        first=rows[0];model_key=hashlib.sha256(bytes.fromhex(first['raw_record_hex'])[0x189:0x1b9].lower()).hexdigest()[:24]
        cache=out/f'model-{model_key}.json';shared=None;cached=False
        if cache.exists() and not cache.is_symlink():
            try:
                candidate=json.loads(cache.read_text())
                mesh=out/candidate['manifest']['mesh_file']
                if candidate['fingerprint']==key and mesh.is_file() and not mesh.is_symlink() and hashlib.sha256(mesh.read_bytes()).hexdigest()==candidate['manifest']['mesh_sha256']:
                    shared=candidate;cached=True
            except (KeyError,ValueError,OSError):pass
        if shared is None:
            try:
                payload,manifest,metadata=build(resources,first['id'])
                filename=f"model-{manifest['mesh_sha256']}.glb";write_file(out/filename,payload)
                manifest['mesh_file']=filename
                shared={'fingerprint':key,'manifest':manifest,'report':metadata,'bytes':len(payload)}
                json_file(cache,shared)
            except (KeyError,ValueError,OSError) as error:
                for row in rows:entries.append({'id':row['id'],'label':row['label'],'status':'blocked','reason':str(error)})
                print(f"Model {ordinal}/{len(groups)}: BLOCKED definitions {','.join(str(r['id']) for r in rows)}: {error}",flush=True)
                continue
        manifest=shared['manifest'];metadata=shared['report'];totals['models']+=1
        for field in ('frames','frame_triangles','clips'):totals[field]+=metadata[field]
        totals['bytes']+=shared['bytes'];totals['particle_clips']+=len(manifest['particle_clips']);totals['blocked_clips']+=len(manifest['blocked_clips'])
        for row in rows:
            bound=dict(manifest,definition_id=row['id'],record_sha256=row['record_sha256'])
            filename=f"creature-{row['id']}.nwcreature.json";json_file(out/filename,bound)
            entries.append({'id':row['id'],'label':row['label'],'status':'partial' if manifest['blocked_clips'] or manifest['particle_clips'] else 'ready',
                'manifest':filename,'clips':len(manifest['clips']),'skin_stages':manifest['skin_stages'],
                'blocked_clips':len(manifest['blocked_clips']),'particle_clips':len(manifest['particle_clips'])})
        print(f"Model {ordinal}/{len(groups)}: {metadata['clips']} clips, {metadata['frames']} frames, {metadata['skin_stages']} skins"+(' (cached)' if cached else ''),flush=True)
    entries.sort(key=lambda e:e['id'])
    index={'schema':1,'database_sha256':catalog['database_sha256'],'definitions':entries}
    json_file(out/'creatures-index.json',index)
    summary={'schema':1,'definitions':len(entries),'renderable':sum(e['status']!='blocked' for e in entries),
        'blocked':[{'id':e['id'],'reason':e['reason']} for e in entries if e['status']=='blocked'],
        'partial':sum(e['status']=='partial' for e in entries),'totals':totals,
        'database_sha256':catalog['database_sha256'],'limitations':['particle emitters','sound/native event timing','random scale/hover','native shading/blend parity']}
    json_file(report,summary);print(json.dumps(summary),flush=True)
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('data',type=Path);parser.add_argument('monsters',type=Path)
    parser.add_argument('--definition',type=int,default=282)
    parser.add_argument('--all',action='store_true',help='Export every database definition, sharing model geometry and reporting blockers')
    parser.add_argument('--patch',action='append',type=Path,default=[])
    parser.add_argument('--loose-dir',type=Path)
    parser.add_argument('--out',type=Path,required=True);parser.add_argument('--report',type=Path,required=True)
    args = parser.parse_args()
    try:
        if any(target.resolve().is_relative_to(source.parent.resolve()) for target in (args.out,args.report) for source in (args.data,args.monsters)):
            raise FormatError('Creature outputs must be outside original archive directories')
        if args.loose_dir and args.out.resolve().is_relative_to(args.loose_dir.resolve()):
            raise FormatError('Creature export must be outside original Data directory')
        resources=CreatureResources(args.data,args.monsters,args.patch,args.loose_dir)
        if args.all:
            if export_all(resources,args.out,args.report)['renderable']==0:raise FormatError('No renderable creature definitions')
        else:export(resources,args.definition,args.out,args.report)
        return 0
    except (OSError,ValueError,KeyError) as error:
        print(f'Creature export failed ({type(error).__name__}): {error}',file=sys.stderr);return 1


if __name__=='__main__':sys.exit(main())
