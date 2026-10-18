"""Bounded read-only STS chunk/monster intake (retail chunk.cpp/LoadSaveGame.cpp).

Raw records stay private. Public output contains only structure and placement metadata.
"""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import struct
from tools.slf import Archive, FormatError
from tools.world_objects import parse as objects

class Reader:
    def __init__(self,data):self.data=data;self.pos=0
    def take(self,n):
        if n<0 or self.pos+n>len(self.data):raise FormatError('Truncated STS payload')
        value=self.data[self.pos:self.pos+n];self.pos+=n;return value
    def read(self,fmt):return struct.unpack('<'+fmt,self.take(struct.calcsize('<'+fmt)))
    def count(self):
        n=self.read('I')[0]
        if n>100000:raise FormatError('STS count outside supported bounds')
        return n

def chunk(r):
    name,grouped,at_end,extent=r.read('4sBBi')
    if grouped not in (0,1) or at_end not in (0,1) or not name.isalnum():raise FormatError('Invalid STS chunk header')
    return name.decode('ascii'),grouped,at_end,Reader(r.take(extent))

def monster_bank(r):
    groups=[];monsters=[]
    ng,nm=r.count(),r.count()
    for _ in range(ng):
        size=r.count()
        if not 0x28<=size<=0x12b:raise FormatError('Unsupported STS group size')
        raw=r.take(size);version=struct.unpack_from('<I',raw,0xc4)[0] if size>=0xc8 else 0
        encounter=r.read('B')[0] if version>=2 else 0
        groups.append({'id':struct.unpack_from('<i',raw)[0],'species':struct.unpack_from('<I',raw,0x18)[0], 'version':version,'encounter':bool(encounter)})
    for _ in range(nm):
        version=r.read('I')[0];size=r.count()
        if not 1<=version<=7 or not 0x2f<=size<=0x425:raise FormatError('Unsupported STS monster version/size')
        raw=r.take(size);script=r.read('B')[0]
        if script:
            r.take(64+4+4);r.take(r.count())
        unborn=r.read('B')[0] if version>=5 else 0
        if version>=2 and r.read('B')[0]:r.take(32)
        if version>=3:
            r.take(13);r.take(r.count()*12)
            if version>=4:r.take(12)
            if version>=6:r.take(1)
            if version>=7:r.take(1)
        position=list(struct.unpack_from('<3f',raw,0x17))
        if not all(math.isfinite(v) for v in position):raise FormatError('Nonfinite STS monster position')
        monsters.append(dict(zip(('id','group','species'),struct.unpack_from('<iiI',raw)))|{'position':position,'hp_max':struct.unpack_from('<i',raw,0x27)[0],'hp':struct.unpack_from('<I',raw,0x2b)[0],'unborn':bool(unborn),'script':bool(script)})
    if r.pos!=len(r.data):raise FormatError('Unconsumed STS monster tail')
    return {'groups':groups,'monsters':monsters}

def parse(data):
    r=Reader(data);name,grouped,at_end,root=chunk(r)
    if name!='RIFF' or not grouped or at_end or r.pos!=len(data):raise FormatError(f'Invalid STS RIFF envelope: {name}, grouped={grouped}, end={at_end}, consumed={r.pos}, size={len(data)}')
    levels=[]
    for _ in range(root.count()):
        name,grouped,at_end,level=chunk(root)
        if name!='LVLS' or not grouped:raise FormatError('Unsupported STS root chunk')
        count=level.count();level_id=level.read('I')[0];parts=[];bank=None
        for _ in range(count):
            name,grouped,at_end,part=chunk(level)
            meta={'id':name,'bytes':len(part.data),'at_end':bool(at_end)}
            if name=='MONS' and not at_end:
                bank=monster_bank(part);meta.update(groups=len(bank['groups']),monsters=len(bank['monsters']))
            if name=='MONG' and not at_end:
                version=part.read('i')[0]
                if version>4:part.take(4)
                part.take(4)
                if version>1:part.take(4)
                if version>2:part.take(7)
                meta.update(version=version,generators=part.count())
            parts.append(meta)
        if level.pos!=len(level.data):raise FormatError('STS level extent mismatch')
        levels.append({'level':level_id,'chunks':parts,'bank':bank})
    if root.pos!=len(root.data):raise FormatError('STS root extent mismatch')
    return {'schema':1,'sha256':hashlib.sha256(data).hexdigest(),'levels':levels}

def inspect(archive):
    source=Archive(archive)
    for n in (1,2):
        obj=objects(source.read(f'Monastery/Monastery{n}.PVL'))
        print(json.dumps({'level':n,'arrivals':[{'name':t['name'],'position':t['position'],'orientation':t.get('orientation')} for t in obj['triggers'] if t['start'] in obj['arrivals']]},separators=(',',':')))
        resource=f'Monastery/Monastery{n}.STS';result=parse(source.read(resource))
        print(json.dumps({'resource':resource,'status':result},separators=(',',':')))
        obj=objects(source.read(f'Monastery/Monastery{n}.PVL'))
        print(json.dumps({'level':n,'arrivals':[{'name':t['name'],'position':t['position'],'orientation':t.get('orientation')} for t in obj['triggers'] if t['start'] in obj['arrivals']]},separators=(',',':')))

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('archive',type=Path);a=p.parse_args()
    try:inspect(a.archive);return 0
    except (OSError,ValueError,KeyError,struct.error) as e:
        print('STS intake failed: '+str(e));return 1

if __name__=='__main__':raise SystemExit(main())
