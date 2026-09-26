import os, sys, json, csv, io as _io, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

p = os.path.join(REF, "Curios", "Curios.csv")
text = read_bytes(p).decode("utf-8-sig", "replace")
rows = list(csv.reader(_io.StringIO(text)))
out("ROWS:", len(rows), "COLS(max):", max(len(r) for r in rows))

def g(r, i):
    return r[i] if i < len(r) else ""

# block start: col0 == '' and col1 numeric and col2 non-empty and col3 non-empty
blocks = []
for i, r in enumerate(rows):
    if g(r, 0) == "" and g(r, 1).strip().isdigit() and g(r, 2).strip():
        blocks.append(i)
out("DETECTED BLOCKS:", len(blocks))
out("first 8 block indices:", blocks[:8])
out("")
# group into blocks ending at next block start
bounds = blocks + [len(rows)]
curios = []
for bi in range(len(blocks)):
    s, e = bounds[bi], bounds[bi + 1]
    blk = rows[s:e]
    head = blk[0]
    curios.append({
        "index": int(g(head, 1)),
        "name": g(head, 2).strip(),
        "quality": g(head, 3).strip(),
        "row_start": s, "rows": blk,
    })
out("PARSED CURIOS:", len(curios))
qual = collections.Counter(c["quality"] for c in curios)
out("QUALITY (col3) COUNTS:", dict(qual))
idx = [c["index"] for c in curios]
out("INDEX RANGE:", min(idx), "..", max(idx), "unique:", len(set(idx)))
dups = [k for k, v in collections.Counter(idx).items() if v > 1]
out("DUPLICATE INDICES:", sorted(dups))
out("")

# result types present as first cell of a row right after a header row
RT_HDR = "RESULT TYPES"
res_types = collections.Counter()
main_rows = 0
n_curio_with_results = 0
for c in curios:
    blk = c["rows"]
    hdrs = [i for i, r in enumerate(blk) if g(r, 3).strip() == RT_HDR]
    if hdrs:
        i = hdrs[0]
        # main result rows follow until a row whose col3 is ITEM (item-interaction header) or blank
        j = i + 1
        got = 0
        while j < len(blk):
            r = blk[j]
            if g(r, 2).strip() == "Item Interactions" or g(r, 3).strip() == "ITEM":
                break
            if j > i + 1:
                rt = g(r, 4).strip()
                if rt:
                    res_types[rt] += 1
                    got += 1
                    main_rows += 1
            j += 1
        if got:
            n_curio_with_results += 1
out("MAIN RESULT ROWS (RESULT TYPES col4):", main_rows)
out("CURIOS WITH >=1 MAIN RESULT ROW:", n_curio_with_results)
out("RESULT TYPE FREQUENCY:", dict(res_types.most_common()))
out("")

# item interaction rows
inter = 0
inter_items = collections.Counter()
inter_types = collections.Counter()
for c in curios:
    blk = c["rows"]
    hi = None
    for i, r in enumerate(blk):
        if g(r, 2).strip() == "Item Interactions":
            hi = i
            break
    if hi is None:
        continue
    for r in blk[hi + 1:]:
        item = g(r, 4).strip()
        if item:
            inter += 1
            inter_items[item] += 1
            inter_types[g(r, 5).strip()] += 1
out("ITEM-INTERACTION ROWS:", inter)
out("DISTINCT INTERACTION ITEMS (%d):" % len(inter_items), dict(inter_items.most_common()))
out("INTERACTION RESULT TYPES:", dict(inter_types.most_common()))
out("")

# metadata ID STRING / REGION / TAGS / category
ids = []
regions = collections.Counter()
cats = collections.Counter()
for c in curios:
    blk = c["rows"]
    idv = None
    for i, r in enumerate(blk):
        if g(r, 2).strip() == "ID STRING":
            # value may be on same row (col3) or next row col2
            v = g(r, 3).strip()
            if not v:
                v = g(blk[i + 1], 2).strip()
            idv = v
            break
    ids.append(idv)
    c["id"] = idv
    for i, r in enumerate(blk):
        if g(r, 2).strip() == "REGION FOUND":
            regions[g(blk[i + 1], 2).strip()] += 1
            break
    for r in blk:
        v = g(r, 2).strip()
        if v in ("Treasure", "Haunted", "Knowledge", "Worship", "Trinket", "Food", "Drink", "Fountain", "Decoration", "Hoard", "Sacred", "Unholy", "Curio"):
            cats[v] += 1
out("ID STRINGS distinct:", len(set(ids)), "nulls:", sum(1 for i in ids if not i))
out("REGION COUNTS:", dict(regions.most_common()))
out("CATEGORY(col2 in known set) COUNTS:", dict(cats.most_common()))
out("")
out("ALL CURIO NAMES + ID + QUALITY:")
for c in curios:
    out("  %3d %-28s %-24s %s" % (c["index"], c["name"][:28], (c["id"] or "?")[:24], c["quality"]))
