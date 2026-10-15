"""Read-only combination references from the verified ITEMS.DBS record layout.

This does not establish crafting consumption, failure, class or discovery rules.
See docs/recipe-format.md for executable evidence and unresolved fields.
"""
from __future__ import annotations

from collections import Counter
import hashlib
import struct
from .errors import FormatError
from .slf import Resources

ITEM_STRIDE = 269


def recipe_catalog(resources: Resources) -> dict:
    data, provenance = resources.resolve('DATABASES/ITEMS.DBS')
    if len(data) < 4:
        raise FormatError('Truncated item table')
    count = struct.unpack_from('<I', data)[0]
    if len(data) != 4 + count * ITEM_STRIDE:
        raise FormatError('ITEMS.DBS does not match verified fixed-record layout')
    records = [data[4+i*ITEM_STRIDE:4+(i+1)*ITEM_STRIDE] for i in range(count)]
    names = [r[:60].decode('utf-16le').split('\0', 1)[0] for r in records]
    references, inactive = [], Counter()
    for i, r in enumerate(records):
        ingredients = list(struct.unpack_from('<ii', r, 0xb9))
        if ingredients in ([-1, -1], [-2, -2]):
            inactive[str(ingredients[0])] += 1
            continue
        issues = []
        if not names[i]:
            issues.append('unnamed_output')
        if any(not 0 <= item < count for item in ingredients):
            issues.append('ingredient_out_of_range')
        elif any(not names[item] for item in ingredients):
            issues.append('unnamed_ingredient')
        references.append({
            'output_id': i, 'output_name': names[i],
            # Keep repeated IDs: two equal ingredients are not one ingredient.
            'ingredient_ids': ingredients,
            'ingredient_names': [names[j] if 0 <= j < count else None for j in ingredients],
            'skill_code': r[0xc9], 'skill_threshold': r[0xca],
            'unresolved_c1_c8_hex': r[0xc1:0xc9].hex(),
            'issues': issues, 'record_sha256': hashlib.sha256(r).hexdigest(),
            'raw_record_hex': r.hex(),
        })
    return {'schema': 1, 'mode': 'reference_only', 'source': 'DATABASES/ITEMS.DBS',
            'provenance': provenance, 'record_size': ITEM_STRIDE, 'item_count': count,
            'count': len(references), 'inactive_pairs': dict(sorted(inactive.items())),
            'recipes': references}
