"""First native monster import: record boundaries and four text slots only.

Do not reinterpret the 663-byte disk rows as 485-byte runtime definitions.
Combat fields remain evidence bytes until the loader mapping is recovered.
"""
from __future__ import annotations
import hashlib
import struct
from .errors import FormatError
from .slf import Resources

MONSTER_STRIDE = 663
TEXT_OFFSETS = (0, 48, 96, 144)


def monster_catalog(resources: Resources) -> dict:
    data, provenance = resources.resolve('DATABASES/MONSTERS.DBS')
    if len(data) < 4:
        raise FormatError('Truncated monster table')
    count = struct.unpack_from('<I', data)[0]
    if not count or len(data) != 4 + count * MONSTER_STRIDE:
        raise FormatError('MONSTERS.DBS does not match the supported fixed-record layout')
    rows = []
    for index in range(count):
        record = data[4 + index * MONSTER_STRIDE:4 + (index + 1) * MONSTER_STRIDE]
        texts = []
        for offset in TEXT_OFFSETS:
            slot = record[offset:offset+48].decode('utf-16le')
            if '\0' not in slot:
                raise FormatError('Unterminated monster text slot')
            texts.append(slot.split('\0', 1)[0])
        rows.append({'id': index, 'label': texts[0], 'text_slots': texts,
                     'record_sha256': hashlib.sha256(record).hexdigest(), 'raw_record_hex': record.hex()})
    return {'schema': 1, 'source': 'DATABASES/MONSTERS.DBS', 'provenance': provenance,
            'record_size': MONSTER_STRIDE, 'count': count,
            'database_sha256': hashlib.sha256(data).hexdigest(), 'monsters': rows}
