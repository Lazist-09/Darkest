import os, sys, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

d = load(os.path.join(REF, "JsonLoot.json"))
lt = d["loot_tables"]
out("LOOT TABLES:", len(lt))
out("KEYS:", list(lt[0].keys()))
ids = [t["id"] for t in lt]
out("IDS:", ids)
out("(id,difficulty,dungeon) triples:")
for t in lt:
    out("   %-4s diff=%-2s dungeon=%-8s entries=%d" % (t["id"], t["difficulty"], repr(t["dungeon"]), len(t["entries"])))
out("")
out("DISTINCT ENTRY TYPES:", sorted(set(e["type"] for t in lt for e in t["entries"])))
cnt = collections.Counter(e["type"] for t in lt for e in t["entries"])
out("ENTRY TYPE COUNTS:", dict(cnt))
out("TOTAL ENTRIES:", sum(len(t['entries']) for t in lt))
out("")
out("HEIRLOOM ENTRIES ACROSS ALL LOOT TABLES:")
n = 0
for t in lt:
    for e in t["entries"]:
        if e["type"] == "heirloom":
            n += 1
            out("   table=%s diff=%s dungeon=%s -> %s" % (t["id"], t["difficulty"], t["dungeon"], json.dumps(e)))
out("   COUNT =", n)
out("")
out("ENTRY TYPE 'item'/'gold' SAMPLES:")
for t in lt[:6]:
    for e in t["entries"]:
        if e["type"] in ("item", "gold", "trinket", "currency"):
            out("   table=%s diff=%s %s" % (t["id"], t["difficulty"], json.dumps(e)))
out("")
out("ENTRY TYPE 'table' TARGET SET:", sorted(set(e["data"].get("table") for t in lt for e in t["entries"] if e["type"] == "table")))
out("ENTRY TYPE data key sets per type:")
dks = collections.defaultdict(set)
for t in lt:
    for e in t["entries"]:
        dks[e["type"]].update(e["data"].keys())
for k, v in sorted(dks.items()):
    out("   %-12s %s" % (k, sorted(v)))
out("")
out("ALL ENTRIES of table A (full):")
out(json.dumps(next(t for t in lt if t["id"] == "A"), indent=1)[:4000])
