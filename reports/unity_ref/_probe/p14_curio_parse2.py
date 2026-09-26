import os, sys, json, csv, io as _io, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

p = os.path.join(REF, "Curios", "Curios.csv")
text = read_bytes(p).decode("utf-8-sig", "replace")
rows = list(csv.reader(_io.StringIO(text)))

def g(r, i):
    return r[i] if i < len(r) else ""

starts = [i for i, r in enumerate(rows)
          if g(r, 0) == "" and g(r, 1).strip().isdigit() and g(r, 2).strip()]
bounds = starts + [len(rows)]
out("ROWS:", len(rows), "CURIOS:", len(starts))

MAIN = "RESULT TYPES"
curios = []
for bi, s in enumerate(starts):
    blk = rows[s:bounds[bi + 1]]
    head = blk[0]
    c = {"index": int(g(head, 1)), "name": g(head, 2).strip(),
         "quality": g(head, 4).strip(), "main": [], "inter": [], "id": None, "region": None}
    hm = next((i for i, r in enumerate(blk) if g(r, 4).strip() == MAIN), None)
    hi = next((i for i, r in enumerate(blk) if g(r, 4).strip() == "ITEM"), None)
    # metadata
    for i, r in enumerate(blk):
        if g(r, 2).strip() == "ID STRING":
            c["id"] = g(r, 3).strip() or g(blk[i + 1], 2).strip()
        if g(r, 2).strip() == "REGION FOUND":
            c["region"] = g(r, 3).strip() or g(blk[i + 1], 2).strip()
    if hm is not None:
        stop = hi if hi is not None else len(blk)
        for r in blk[hm + 1:stop]:
            rt = g(r, 4).strip()
            if not rt:
                continue
            c["main"].append({
                "type": rt, "weight": g(r, 5).strip(), "chance": g(r, 6).strip(),
                "r1": (g(r, 7).strip(), g(r, 8).strip(), g(r, 9).strip()),
                "r2": (g(r, 10).strip(), g(r, 11).strip(), g(r, 12).strip()),
                "r3": (g(r, 13).strip(), g(r, 14).strip(), g(r, 15).strip()),
            })
    if hi is not None:
        for r in blk[hi + 1:]:
            it = g(r, 4).strip()
            if not it:
                continue
            c["inter"].append({
                "item": it, "type": g(r, 5).strip(),
                "r1": (g(r, 7).strip(), g(r, 8).strip(), g(r, 9).strip()),
                "r2": (g(r, 10).strip(), g(r, 11).strip(), g(r, 12).strip()),
                "r3": (g(r, 13).strip(), g(r, 14).strip(), g(r, 15).strip()),
                "string": g(r, 16).strip(), "notes": g(r, 17).strip(),
            })
    curios.append(c)

out("QUALITY CODES (col4):", dict(collections.Counter(c["quality"] for c in curios)))
out("REGION:", dict(collections.Counter(c["region"] for c in curios)))
out("MAIN RESULT ROWS TOTAL:", sum(len(c["main"]) for c in curios))
out("RESULT TYPE FREQ:", dict(collections.Counter(m["type"] for c in curios for m in c["main"]).most_common()))
out("ITEM-INTERACTION ROWS TOTAL:", sum(len(c["inter"]) for c in curios))
out("INTERACTION ITEM FREQ:", dict(collections.Counter(x["item"] for c in curios for x in c["inter"]).most_common()))
out("INTERACTION RESULT TYPE FREQ:", dict(collections.Counter(x["type"] for c in curios for x in c["inter"]).most_common()))
out("")
out("CURIOS with a Loot main-result:")
for c in curios:
    for m in c["main"]:
        if m["type"] == "Loot":
            out("   %-24s region=%-16s w=%s pct=%-8s r1=%s r2=%s r3=%s" % (
                c["name"], c["region"], m["weight"], m["chance"], m["r1"], m["r2"], m["r3"]))
out("")
out("ALL 'Heirloom' / H-table references in curio results:")
for c in curios:
    for m in c["main"] + c["inter"]:
        s = json.dumps(m)
        if "heirloom" in s.lower() or '"H"' in s or "H\"" in s:
            out("   %-24s %s" % (c["name"], s))
out("")
out("CURIO TABLE (index | name | quality | region | #main | #inter):")
for c in curios:
    out("  %3d | %-26s | %-6s | %-16s | %2d | %2d" % (c["index"], c["name"], c["quality"], c["region"], len(c["main"]), len(c["inter"])))
out("")
out("SAMPLE FULL CURIO BLOCK (Heirloom Chest):")
hc = next(c for c in curios if c["id"] == "heirloom_chest")
out(json.dumps(hc, indent=1))
out("")
out("RESULT-TYPE WORDING SAMPLES (distinct r1 values on main rows):")
vals = collections.Counter()
for c in curios:
    for m in c["main"]:
        for k in ("r1", "r2", "r3"):
            if m[k][0]:
                vals[m[k][0]] += 1
out(json.dumps(dict(vals.most_common()), indent=1))
