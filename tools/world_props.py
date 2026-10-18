"""Private original prop/bitmap geometry at its saved initial pose.

Door representations are exported separately. Trigger scripts are not guessed.
"""
import argparse
import hashlib
import json
from pathlib import Path
from tools import pvl,world_objects,world_doors
from tools.slf import Archive,FormatError
from tools.textures import Textures


def build(data,textures=None):
    objects=world_objects.parse(data,geometry=True);materials=[];ids={};meshes=[];props=[];blocked=[]
    for prop in objects['props']:
        trigger=prop['trigger']
        if trigger is not None and trigger.get('door') is not None and not trigger['door'].get('legacy_geometry'):continue
        try:
            geometry=world_doors.frame_geometry(prop,prop['animation'].get('first',0),materials,ids)
            if not geometry.indices:raise FormatError('Prop has no visible triangles')
            index=len(meshes);meshes.append(geometry)
            props.append({'id':prop['start'],'name':prop['name'],'mesh':index,'collidable':bool(prop['flags']&1),
                'bounds':pvl.bounds([pvl.transformed(geometry.positions[v]) for face in geometry.indices for v in face])})
        except (ValueError,KeyError) as error:blocked.append({'id':prop['start'],'name':prop['name'],'reason':str(error)})
    payload=pvl.glb(pvl.Level(0,materials,meshes,0,0),textures,objects['pvl_sha256']) if meshes else b''
    return payload,{'schema':1,'pvl_sha256':objects['pvl_sha256'],'mesh_sha256':hashlib.sha256(payload).hexdigest(),
        'props':props,'blocked':blocked,'triangles':sum(len(m.indices) for m in meshes)}


def export(archive,out,report):
    source=Archive(archive);out.mkdir(parents=True,exist_ok=True);reports=[]
    for number in (1,2):
        name=f'Monastery/Monastery{number}.PVL'
        payload,manifest=build(source.read(name),Textures(source,name))
        stem=out/f'monastery{number}'
        for suffix,content in (('.nwprops.glb',payload),('.nwprops.json',(json.dumps(manifest,allow_nan=False)+'\n').encode())):
            target=stem.with_suffix(suffix)
            if target.is_symlink():raise FormatError('Unsafe prop destination')
            target.write_bytes(content)
        reports.append({'level':number,'props':len(manifest['props']),'triangles':manifest['triangles'],'blocked':manifest['blocked']})
        print(f"Monastery{number}: {len(manifest['props'])} original props/bitmaps, {manifest['triangles']} triangles, {len(manifest['blocked'])} blocked",flush=True)
    report.write_text(json.dumps({'schema':1,'levels':reports},allow_nan=False)+'\n')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('archive',type=Path);parser.add_argument('--out',type=Path,required=True);parser.add_argument('--report',type=Path,required=True)
    args=parser.parse_args();export(args.archive,args.out,args.report)
