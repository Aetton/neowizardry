"""Private native first-floor loose items and original ITM geometry.

Placement: LoadItemStatus/LoadItem (packed 0xad-byte chains).
Models: ActivateItem, ItemRep::ReadFromFile, ReadSingleLevelMesh.
Fallback model names are the native item appearance lookup, not display names.
"""
import hashlib
import json
import struct
from tools import pvl,world_doors,world_objects,level_status
from tools.slf import Archive,FormatError,item_catalog
from tools.textures import Textures

FALLBACK_NAMES=['Dagger', 'Long Sword', 'Bipennis', 'Battle Axe', 'Flail', 'Mace', 'Hammer', 'Staff', 'Halberd', 'Spear', 'Bo Stick', 'Bow', 'Crossbow', 'Sling', 'Great Sword', 'Rapier', 'Katana', 'Long Staff', 'Wand', 'Magic Stave', 'Spellbook', 'Shuriken', 'Arrows', 'Bullets', 'Kite Shield', 'Basnet', 'Plate Torso', 'Plate Leggings', 'Gauntlets', 'Sollerets', 'Amulet', 'Ring', 'Blue Potion', 'Scroll', 'Powder', 'Key', 'Locket', 'Great Bow', 'Round Shield', 'Wizard Cone', 'Leather Helm', 'Cuirass', 'Leather Top', 'Greaves', 'Leather Leggings', 'Gloves', 'Sandals', 'Buskins', 'Boots', 'Book', 'Ankh', 'Ninjato', 'War Hammer', 'Flamberge', 'Bullwhip', 'Sai', 'Nunchuka', 'Glaive', 'Black Sword', 'Fire Sword', 'Upper Robes', 'Lower Robes', 'Halter', 'Skirt', 'Skullcap', 'Feathered Cap', 'Mitre', 'Breast Plate', 'Kabuto', 'Upper Toseido', 'Lower Toseido', 'Burgonet', 'Fur Leggings', 'Cloak', 'Mantis Gloves', 'Mantis Boots', 'Garland', "Cat 'O Nine Tails", 'Silver Cross', 'Ruby Talisman', 'Purple Amulet', 'Bracelet', 'Necklace', 'Quarrels', 'Jade Figure', 'Chain Coif', 'Upper Chain', 'Lower Chain', 'Mail Mittens', 'Chain Hosen', 'Lance', 'Armet', 'Ninja Cowl', 'Upper Ninja Garb', 'Lower Ninja Garb', 'Tabi Boots', 'Heaume', 'Box Helm', 'Diamond Ring', 'Bag', 'Red Potion', 'Green Potion', 'Purple Potion', 'Yellow Potion', 'Power Glove', "T'Rang Staff", 'Bronze Gauntlets', 'Comm Link', 'Light Sword', 'Magic Bag', 'Phaser', 'ID Card', 'Darts', 'Powder Shot', 'Musket', 'Coin', 'Stone', 'Stick', 'Short Sword', 'UNDEFINED', 'Bagpipes', 'Lyre', 'Lute', 'Horn', 'Map', 'Bracelet', 'Shuriken', 'UNDEFINED', 'Wand', 'Yellow Potion', 'Medium Shield', 'UNDEFINED', 'UNDEFINED', 'UNDEFINED', 'UNDEFINED', 'UNDEFINED', 'Saxophone', 'Violin', 'Drum', 'Bullroarer', 'bomb', 'emptybottle', 'stix', 'rocket', 'rocketlauncher']

def model(data):
    reader=world_objects.ObjectsReader(data,geometry=True)
    reader.version(2,2)
    mesh=reader.mesh()
    if reader.offset!=len(data):raise FormatError('Unconsumed ITM tail')
    if mesh['lod_frames']!=1:raise FormatError('Animated item geometry requires animation import')
    animation={'entries':[{'channel':2,'mesh':{'meshes':[mesh]}}]}
    if mesh['transform']:
        animation['path']={'mode':2,'frames':[mesh['transform']]}
    materials=[]
    geometry=world_doors.frame_geometry({'animation':animation,'position':[0,0,0]},0,materials,{})
    return pvl.Level(0,materials,[geometry],0,0)

def export(archive,out,resources):
    archive=Archive(archive);data=archive.read('Monastery/Monastery1.STS')
    status=level_status.parse(data)
    levels=[v for v in status['levels'] if v['level']==8]
    if len(levels)!=1 or levels[0]['items'] is None:raise FormatError('Missing native floor item bank')
    if out.resolve().is_relative_to(archive.path.resolve().parent):raise FormatError('Items must be exported outside original Levels directory')
    catalog=item_catalog(resources);by_id={i['id']:i for i in catalog['items']};members=[];models={};seen=set()
    out.mkdir(parents=True,exist_ok=True)
    for group in levels[0]['items']:
        for item in group:
            id=item['id'];definition=item['definition_id']
            if id<0 or id in seen or any(item['instance_flags']) or item['flags']&~1:raise FormatError('Unsupported native item instance state')
            seen.add(id)
            if definition not in models:
                raw=bytes.fromhex(by_id[definition]['instance']['raw_record_hex'])
                name=pvl.text(raw[0x8d:0xad])
                if not name:
                    appearance=struct.unpack_from('<H',raw,0x3f)[0]
                    if appearance>=len(FALLBACK_NAMES):raise FormatError('Unknown native item appearance')
                    name=FALLBACK_NAMES[appearance]
                resource='ITEMS3D/'+name+( '' if name.lower().endswith('.itm') else '.ITM')
                try:source=resources.read(resource)
                except (KeyError,FileNotFoundError):
                    resource='ITEMS3D/questionmark.ITM';source=resources.read(resource)
                payload=pvl.glb(model(source),Textures(resources,resource),hashlib.sha256(source).hexdigest())
                filename=f'item-{definition}.nwworlditem.glb';target=out/filename
                if target.is_symlink():raise FormatError('Unsafe item model destination')
                target.write_bytes(payload);models[definition]=(filename,hashlib.sha256(payload).hexdigest())
            filename,digest=models[definition]
            members.append({'location_id':id,'definition_id':definition,'position':dict(zip(('x','y','z'),item['position'])),
                'stack_count':item['stack_count'],'charges':item['charges'],'identified':item['identified'],
                'hidden':bool(item['flags']&1),'mesh_file':filename,'mesh_sha256':digest})
    result={'schema':1,'pvl_sha256':hashlib.sha256(archive.read('Monastery/Monastery1.PVL')).hexdigest(),
        'sts_sha256':status['sha256'],'level':8,'members':members}
    target=out/'monastery1.nwitems.json'
    if target.is_symlink():raise FormatError('Unsafe item manifest destination')
    target.write_text(json.dumps(result,allow_nan=False,separators=(',',':'))+'\n',encoding='utf-8')
    print(f"Native floor items: {len(members)} placements, {sum(not m['hidden'] for m in members)} visible, {len(models)} original models",flush=True)
    return result

if __name__=='__main__':
    import argparse
    from tools.prepare_game import installation,child
    from tools.slf import Resources
    from pathlib import Path
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('archive',type=Path);parser.add_argument('--out',type=Path,required=True);args=parser.parse_args()
    root,_,data,_,patches=installation(args.archive.parent.parent)
    export(args.archive,args.out,Resources(Archive(data),[Archive(p) for p in patches],child(root,'Data')))
