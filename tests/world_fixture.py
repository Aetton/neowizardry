"""Authored floor/wall fixture for the real Godot controller smoke test."""
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from test_oct import fixture as oct_fixture
from test_world_objects import fixture as pvl_fixture
from tools import oct, pvl, world_objects, world_doors, world_props

root = Path(sys.argv[1])
root.mkdir(parents=True, exist_ok=True)
source = pvl_fixture(locked=False)
source_hash = hashlib.sha256(source).hexdigest()
(root / 'fixture.glb').write_bytes(pvl.glb(pvl.parse(source), source_hash=source_hash))
(root / 'fixture.nwcol').write_bytes(oct.encode(oct.parse(oct_fixture()), source_hash))
objects=world_objects.parse(source)
(root / 'fixture.nwobjects.json').write_text(json.dumps(objects), encoding='utf-8')
door_glb,door_manifest,door_report=world_doors.build(source)
(root / 'fixture.nwdoors.glb').write_bytes(door_glb)
(root / 'fixture.nwdoors.json').write_text(json.dumps(door_manifest),encoding='utf-8')

props_glb,props_manifest=world_props.build(source)
(root/'fixture.nwprops.glb').write_bytes(props_glb)
(root/'fixture.nwprops.json').write_text(json.dumps(props_manifest),encoding='utf-8')

# Optional authored creature: the source of the binding is the C# fixture catalog.
if len(sys.argv)>2:
    from test_creatures import ParticleResources
    from tools import creatures
    catalog=json.loads(Path(sys.argv[2]).read_text())
    record=catalog['monsters'][0]
    payload,manifest,_=creatures.build(ParticleResources(),0)
    manifest.update(database_sha256=catalog['database_sha256'],record_sha256=record['record_sha256'],
                    cycle_name=bytes.fromhex(record['raw_record_hex'])[0x189:0x1b9].split(b'\0')[0].decode('ascii'))
    (root/'monsters.json').write_text(json.dumps(catalog),encoding='utf-8')
    (root/'creature-0.nwcreature.glb').write_bytes(payload)
    (root/'creature-0.nwcreature.json').write_text(json.dumps(manifest),encoding='utf-8')
    (root/'creatures-index.json').write_text(json.dumps({'schema':1,'database_sha256':catalog['database_sha256'],
        'definitions':[{'id':0,'label':record['label'],'status':'ready','manifest':'creature-0.nwcreature.json'}]}),encoding='utf-8')
