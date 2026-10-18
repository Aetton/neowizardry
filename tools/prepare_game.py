"""Prepare a private playable monastery from a locally installed Wizardry 8.

Shipped with the portable app and its stdlib-only Python runtime. No original
assets are downloaded or published. A failed preparation never replaces a set.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import uuid
from tools import creatures, oct, pvl, world_objects, world_doors, world_props, level_status
from tools.slf import FormatError, item_catalog


def child(root,*parts):
    for part in parts:
        if not root.is_dir():return root/part
        matches=[p for p in root.iterdir() if p.name.casefold()==part.casefold()]
        if len(matches)>1:raise FormatError('Ambiguous original installation filename')
        root=matches[0] if matches else root/part
    return root


def installation(root):
    root=Path(root).resolve()
    for candidate in (root,root.parent):
        levels=child(candidate,'Levels','LEVELS.SLF')
        if not levels.is_file():levels=child(candidate,'Data','Levels','LEVELS.SLF')
        data=child(candidate,'Data','DATA.SLF');monsters=child(candidate,'Data','Monsters','MONSTERS.SLF')
        if all(p.is_file() for p in (levels,data,monsters)):
            patches=child(candidate,'Patches')
            overlay=sorted((p for p in patches.iterdir() if p.name.upper().startswith('PATCH.') and p.suffix[1:].isdigit()),key=lambda p:int(p.suffix[1:])) if patches.is_dir() else []
            return candidate,levels,data,monsters,overlay
    raise FormatError('Select the Wizardry 8 installation containing Levels/LEVELS.SLF and Data/DATA.SLF + Data/Monsters/MONSTERS.SLF')


def file_hash(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()


def ready(target,key):
    try:
        manifest=json.loads((target/'game-content.json').read_text())
        files=manifest.get('files')
        return manifest.get('schema')==1 and manifest.get('fingerprint')==key and isinstance(files,dict) and {'monastery1.glb','monastery1.nwencounter.json','creature-282.nwcreature.json','items.json','monsters.json'}.issubset(files) and all(Path(name).name==name and not (target/name).is_symlink() and (target/name).is_file() and file_hash(target/name)==digest for name,digest in files.items())
    except (OSError,ValueError,KeyError):return False


def seed_models(out,stage,key):
    # Exporter independently checks source fingerprints and mesh hashes before reuse.
    sets=out/'sets'
    if not sets.is_dir():return
    for prior in sets.iterdir():
        if not prior.is_dir() or prior.is_symlink() or len(prior.name)!=64 or any(c not in '0123456789abcdef' for c in prior.name):continue
        for cache in prior.glob('model-*.json'):
            if cache.is_symlink() or (stage/cache.name).exists():continue
            try:
                value=json.loads(cache.read_text());name=value['manifest']['mesh_file']
                mesh=prior/name
                if value['fingerprint']!=key or Path(name).name!=name or not name.startswith('model-') or not name.endswith('.glb') or mesh.is_symlink() or not mesh.is_file():continue
                target=stage/name
                if not target.exists():
                    try:os.link(mesh,target)
                    except OSError:shutil.copyfile(mesh,target)
                shutil.copyfile(cache,stage/cache.name)
            except (OSError,KeyError,ValueError):continue


def prepare(root,out):
    root,levels,data,monsters,patches=installation(root)
    out=Path(out).resolve()
    if out==root or out.is_relative_to(root):raise FormatError('Private cache must be outside the original installation')
    resources=creatures.CreatureResources(data,monsters,patches,child(root,'Data'))
    resource_key=creatures.fingerprint(resources)
    digest=hashlib.sha256(resource_key.encode())
    for path in [levels,*[Path(__file__).with_name(n) for n in ('prepare_game.py','oct.py','world_props.py','level_status.py')]]:
        with path.open('rb') as stream:
            for chunk in iter(lambda:stream.read(1024*1024),b''):digest.update(chunk)
    key=digest.hexdigest();target=out/'sets'/key
    if ready(target,key):
        print('Reusing prepared monastery',flush=True);return target/'monastery1.glb'
    stage=out/('preparing-'+uuid.uuid4().hex);stage.mkdir(parents=True)
    try:
        for title,exporter,kwargs in (
            ('Monastery geometry and original textures',pvl.export,{'textured':True}),
            ('Walkable floors and walls',oct.export,{}),
            ('Objects and arrivals',world_objects.export,{}),
            ('Native door animations',world_doors.export,{}),
            ('Original furniture and objects',world_props.export,{})):
            print(title,flush=True);exporter(levels,stage,stage/(exporter.__module__.split('.')[-1]+'-report.json'),**kwargs)
        encounter=level_status.export(levels,stage)
        print('Original item and creature catalogs',flush=True)
        creatures.json_file(stage/'items.json',item_catalog(resources.shared))
        seed_models(out,stage,resource_key)
        creatures.export_all(resources,stage,stage/'creatures-report.json')
        required=['monastery1'+suffix for suffix in ('.glb','.nwcol','.nwobjects.json','.nwdoors.glb','.nwdoors.json','.nwprops.glb','.nwprops.json','.nwencounter.json')]+['items.json','monsters.json','creatures-index.json','creature-282.nwcreature.json']
        if not all((stage/name).is_file() for name in required):raise FormatError('Prepared monastery is missing required playable content')
        for definition_id in {282,*[m['definition_id'] for m in encounter['members']]}:
            name=f'creature-{definition_id}.nwcreature.json';required.append(name)
            creature=json.loads((stage/name).read_text());required.append(creature['mesh_file'])
        creatures.json_file(stage/'game-content.json',{'schema':1,'fingerprint':key,'files':{name:file_hash(stage/name) for name in required}})
        target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists():
            # A damaged old set is retained for diagnosis; it is not selected.
            target.rename(target.with_name(key+'-incomplete-'+uuid.uuid4().hex))
        stage.rename(target)
        return target/'monastery1.glb'
    finally:
        if stage.exists():shutil.rmtree(stage)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--installation',type=Path,required=True);parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--result',type=Path,required=True);args=parser.parse_args()
    try:
        world=prepare(args.installation,args.out)
        creatures.json_file(args.result,{'schema':1,'world':str(world)})
        print('Ready to enter the monastery',flush=True);return 0
    except (OSError,ValueError,KeyError) as error:
        print('Preparation failed: '+str(error),file=sys.stderr);return 1

if __name__=='__main__':sys.exit(main())
