#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""three_way_buff_primitives.py -- buff PRIMITIVE layer, three-source cross-check.

WHY (planner ruling 2026-09-26, user's words, verbatim):
    "选1, 如果E盘读不到数值就用参考"
    => CALIBER: one-hand E-drive FIRST; the reference project is the FALLBACK,
       used ONLY for layers the E-drive cannot answer.
    The primitive layer currently shipped (`darkest/data/buff_primitives.json`,
    1801 rows, `_source` = the REFERENCE project) therefore has to be re-checked:
    the E-drive DOES have `shared/buffs/base.buffs.json`.

WHAT THIS MEASURES (no writing, read-only on all three sources)
    A. one-hand  : E:\\...\\DarkestDungeon\\shared\\buffs\\base.buffs.json
    B. shipped   : darkest/data/buff_primitives.json            (origin=?)
    C. reference : <Unity project>\\Assets\\Resources\\Data\\JsonBuffs.json

    counts / id-set overlap / id-set differences / field-shape of one sample row.

OUTPUT IS ASCII-ONLY (this console is GBK; printing CJK kills the tool).
"""
import io
import json
import os
import sys

E_ROOT = r'E:\SteamLibrary\steamapps\common\DarkestDungeon'
ONE_HAND = os.path.join(E_ROOT, 'shared', 'buffs', 'base.buffs.json')
SHIPPED = os.path.join('darkest', 'data', 'buff_primitives.json')
REFERENCE = r'F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonBuffs.json'


def load_json(path):
    """Load JSON, retrying as latin-1 if utf-8 fails (non-standard encodings)."""
    raw = io.open(path, 'rb').read()
    for enc in ('utf-8-sig', 'utf-8', 'latin-1'):
        try:
            return json.loads(raw.decode(enc)), enc
        except Exception:  # noqa: BLE001
            continue
    raise SystemExit('PARSE-FAIL %s' % path)


def as_rows(obj):
    """Return the entity list of a JSON document, whatever it is wrapped in."""
    if isinstance(obj, list):
        return obj
    if isinstance(obj, dict):
        # prefer the longest list-valued key
        cands = [(len(v), k) for k, v in obj.items() if isinstance(v, list)]
        if cands:
            cands.sort(reverse=True)
            return obj[cands[0][1]]
        return [obj]
    return []


def ids_of(rows):
    out = set()
    for r in rows:
        if isinstance(r, dict):
            for k in ('id', 'Id', 'ID', 'name'):
                if k in r and isinstance(r[k], str):
                    out.add(r[k])
                    break
    return out


def main():
    print('=== A. one-hand E-drive ===')
    ok_a = os.path.exists(ONE_HAND)
    print('path exists      : %s' % ok_a)
    rows_a = []
    if ok_a:
        obj, enc = load_json(ONE_HAND)
        rows_a = as_rows(obj)
        print('size             : %d bytes' % os.path.getsize(ONE_HAND))
        print('encoding used    : %s' % enc)
        print('wrapper type     : %s' % type(obj).__name__)
        if isinstance(obj, dict) and not isinstance(obj.get(''), list):
            print('top-level keys   : %s' % (list(obj.keys())[:8],))
        print('ROWS             : %d' % len(rows_a))
        if rows_a and isinstance(rows_a[0], dict):
            print('row keys         : %s' % (sorted(rows_a[0].keys()),))

    print()
    print('=== B. shipped darkest/data/buff_primitives.json ===')
    obj_b, enc_b = load_json(SHIPPED)
    rows_b = as_rows(obj_b)
    print('encoding used    : %s' % enc_b)
    print('ROWS             : %d' % len(rows_b))
    if isinstance(obj_b, dict):
        print('_source          : %s' % obj_b.get('_source'))
    if rows_b and isinstance(rows_b[0], dict):
        print('row keys         : %s' % (sorted(rows_b[0].keys()),))

    print()
    print('=== C. reference project JsonBuffs.json ===')
    ok_c = os.path.exists(REFERENCE)
    print('path exists      : %s' % ok_c)
    rows_c = []
    if ok_c:
        obj_c, enc_c = load_json(REFERENCE)
        rows_c = as_rows(obj_c)
        print('size             : %d bytes' % os.path.getsize(REFERENCE))
        print('ROWS             : %d' % len(rows_c))
        if rows_c and isinstance(rows_c[0], dict):
            print('row keys         : %s' % (sorted(rows_c[0].keys()),))

    print()
    print('=== D. id-set comparison (口径: 实体数, not 引用数) ===')
    ia, ib, ic = ids_of(rows_a), ids_of(rows_b), ids_of(rows_c)
    print('one-hand ids     : %d' % len(ia))
    print('shipped  ids     : %d' % len(ib))
    print('reference ids    : %d' % len(ic))
    print('one-hand == shipped ?           %s' % (ia == ib))
    print('one-hand & shipped              : %d' % len(ia & ib))
    print('in one-hand, NOT shipped        : %d' % len(ia - ib))
    print('in shipped, NOT one-hand        : %d' % len(ib - ia))
    print('in reference, NOT one-hand      : %d' % len(ic - ia))
    print('in one-hand, NOT reference      : %d' % len(ia - ic))
    if ia - ib:
        print('sample in one-hand only         : %s' % (sorted(ia - ib)[:8],))
    if ib - ia:
        print('sample in shipped only          : %s' % (sorted(ib - ia)[:8],))

    print()
    print('=== E. sample row, one-hand vs shipped (same id if possible) ===')
    common = sorted(ia & ib)
    if common:
        target = common[0]
        print('id: %s' % target)
        for label, rows in (('one-hand', rows_a), ('shipped', rows_b)):
            for r in rows:
                if isinstance(r, dict) and r.get('id') == target:
                    print('%-9s: %s' % (label, json.dumps(r, ensure_ascii=True, sort_keys=True)))
                    break
    return 0


if __name__ == '__main__':
    sys.exit(main())
