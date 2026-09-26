# -*- coding: utf-8 -*-
"""Our own data inventory: darkest/data/*.json -> reports/unity_ref/00_our_data_inventory.md
ASCII-only stdout. CJK goes into the report file only.
"""
import io, os, json, collections

REPO = r'F:\GithubPro\Darkest'
DATA = os.path.join(REPO, 'darkest', 'data')
OUT = os.path.join(REPO, 'reports', 'unity_ref', '00_our_data_inventory.md')

os.makedirs(os.path.dirname(OUT), exist_ok=True)


def count_entries(node):
    """Return (kind, count) for the most plausible 'entry list' in this json."""
    if isinstance(node, list):
        return 'array', len(node)
    if isinstance(node, dict):
        best = None
        for k, v in node.items():
            if k.startswith('_'):
                continue
            if isinstance(v, list):
                cand = (k, len(v))
                if best is None or cand[1] > best[1]:
                    best = cand
            elif isinstance(v, dict):
                # dict-of-records (mapping container) -- report too
                vals = [x for x in v.values() if isinstance(x, (dict, list))]
                if vals:
                    cand = (k + ' (dict)', len(v))
                    if best is None or cand[1] > best[1]:
                        best = cand
        if best:
            return best[0], best[1]
    return '-', 0


def field_freq(entries):
    c = collections.Counter()
    for e in entries:
        if isinstance(e, dict):
            for k in e.keys():
                c[k] += 1
    return c


rows = []
detail = []
for name in sorted(os.listdir(DATA)):
    if not name.endswith('.json'):
        continue
    path = os.path.join(DATA, name)
    b = io.open(path, 'rb').read()
    try:
        d = json.loads(b.decode('utf-8'))
        ok = True
    except Exception as ex:
        rows.append((name, len(b), 'PARSE-FAIL', '-', 0, str(ex)[:60]))
        continue
    kind, n = count_entries(d)
    top = list(d.keys())[:12] if isinstance(d, dict) else ['<array>']
    rows.append((name, len(b), 'ok', kind, n, ', '.join(top)))

    # detail: field frequency of the entry list
    entries = []
    if isinstance(d, list):
        entries = d
    elif isinstance(d, dict):
        for k, v in d.items():
            if k.startswith('_'):
                continue
            if isinstance(v, list) and (kind.startswith(k) or len(v) == n and v and isinstance(v[0], dict)):
                entries = v
                break
    if entries and isinstance(entries[0], dict):
        ff = field_freq(entries)
        body = '\n'.join('| `%s` | %d |' % (k, v) for k, v in ff.most_common(40))
        detail.append((name, kind, n, body))

with io.open(OUT, 'w', encoding='utf-8', newline='\n') as f:
    f.write('# 我方数据总账（`darkest/data/*.json`）\n\n')
    f.write('> 由 `reports/unity_ref/_gen_our_inventory.py` 生成（python，实测计数）✓\n')
    f.write('> 用途：与参考项目 `Darkest-Dungeon-Unity` 的数据总账对账 ⇒ 得出「采用映射」✓\n\n')
    f.write('## 1. 一表看清\n\n')
    f.write('| 文件 | 字节 | 解析 | 条目容器 | 条目数 | 顶层 key |\n|---|---:|---|---|---:|---|\n')
    for r in rows:
        f.write('| `%s` | %d | %s | `%s` | %d | %s |\n' % r)
    f.write('\n## 2. 逐文件字段频次（前 40）\n\n')
    for name, kind, n, body in detail:
        f.write('### `%s`（`%s` · %d 条）\n\n| 字段 | 出现次数 |\n|---|---:|\n%s\n\n' % (name, kind, n, body))

print('files=%d  wrote=%s' % (len(rows), OUT))
for r in rows:
    print('%-28s %8d %-10s %6d' % (r[0], r[1], r[3], r[4]))
