"""Verified native spell metadata; combat/effect execution is deliberately not inferred."""
from __future__ import annotations
import hashlib
import struct
from .errors import FormatError
from .slf import Resources

SPELL_STRIDE = 704


def descriptions(data: bytes) -> tuple[list[str], dict]:
    if len(data) < 13:
        raise FormatError("Truncated spell descriptions")
    count = struct.unpack_from('<I', data, len(data)-8)[0]
    index = len(data)-8-4*count
    if count == 0 or index < 5:
        raise FormatError("Invalid spell description index")
    strings, spans = [], set()
    for i in range(count):
        offset = struct.unpack_from('<I', data, index+4*i)[0]
        if offset < 5 or offset+12 > index:
            raise FormatError("Spell description pointer outside payload")
        if data[offset:offset+8] != b'\x01\x00\x00\x00\x00\xff\xff\xff':
            raise FormatError("Unsupported spell description record marker")
        length = struct.unpack_from('<I', data, offset+8)[0]
        end = offset+12+2*length
        if end > index:
            raise FormatError("Truncated spell description text")
        strings.append(data[offset+12:end].decode('utf-16le').rstrip('\0'))
        spans.add((offset,end))
    ordered = sorted(spans)
    if any(a[1] > b[0] for a,b in zip(ordered,ordered[1:])):
        raise FormatError("Overlapping spell description records")
    return strings, {"count": count, "index_offset": index, "trailer_hex": data[-8:].hex()}


def spell_catalog(resources: Resources) -> dict:
    table, provenance = resources.resolve('DATABASES/SPELLTABLES.DBS')
    desc, desc_source = resources.resolve('DATABASES/SPELLDESC.DBS')
    if len(table) < 8:
        raise FormatError("Truncated spell table")
    count = struct.unpack_from('<I', table)[0]
    if count == 0 or len(table) != 8+count*SPELL_STRIDE:
        raise FormatError("SPELLTABLES.DBS does not match verified fixed-record layout")
    texts, desc_index = descriptions(desc)
    if len(texts) < count:
        raise FormatError("Spell description table has fewer entries than spell records")
    spells = []
    for i in range(count):
        r = table[8+i*SPELL_STRIDE:8+(i+1)*SPELL_STRIDE]
        integer = lambda offset: struct.unpack_from('<i', r, offset)[0]
        spells.append({
            "id": i, "name": r[0x19c:0x220].decode('utf-16le').split('\0',1)[0],
            "description": texts[i], "cost": integer(0x14a), "level": integer(0x157),
            "realm_code": integer(0x234), "range_code": integer(0x230),
            "target_code": integer(0x238), "usability_code": integer(0x23c),
            "school_flags": [r[p] for p in (0x149,0x15b,0x220,0x221)],
            "duration_base_raw": integer(0x14e), "duration_per_power_raw": integer(0x145),
            "damage_flag_raw": integer(0x248), "damage_constant": struct.unpack_from('<H',r,0x152)[0],
            "damage_dice": r[0x154], "damage_sides": r[0x155],
            "record_sha256": hashlib.sha256(r).hexdigest(), "raw_record_hex": r.hex()})
    return {"schema": 1, "source": "DATABASES/SPELLTABLES.DBS", "provenance": provenance,
            "description_source": "DATABASES/SPELLDESC.DBS", "description_provenance": desc_source,
            "record_size": SPELL_STRIDE, "header_tail_hex": table[4:8].hex(),
            "description_index": desc_index, "count": count, "spells": spells}
