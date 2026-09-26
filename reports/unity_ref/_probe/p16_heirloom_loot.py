import os, sys, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

d = load(os.path.join(REF, "JsonLoot.json"))
lt = d["loot_tables"]

def key(t):
    return t["id"]

# 1) entries whose payload type is heirloom
out("### entries with data.type == 'heirloom'")
tot = 0
by_table = collections.Counter()
for t in lt:
    for e in t["entries"]:
        if e["type"] == "item" and e["data"].get("type") == "heirloom":
            tot += 1
            by_table["%s(diff=%s,dungeon=%s)" % (t["id"], t["difficulty"], t["dungeon"])] += 1
out("TOTAL heirloom-payload entries:", tot)
for k, v in sorted(by_table.items()):
    out("   %-32s %d" % (k, v))
out("")
out("### distinct payload types across all loot entries (data.type)")
pt = collections.Counter()
for t in lt:
    for e in t["entries"]:
        if e["type"] == "item":
            pt[e["data"].get("type")] += 1
out(dict(pt.most_common()))
out("")

# 2) who references H
out("### tables that reference the heirloom table 'H'")
refs = collections.defaultdict(list)
for t in lt:
    for e in t["entries"]:
        if e["type"] == "table":
            refs[e["data"]["table"]].append("%s/d%s/%s" % (t["id"], t["difficulty"], t["dungeon"]))
for k in sorted(refs):
    out("   -> %-14s referenced by (%d): %s" % (k, len(refs[k]), refs[k][:14]))
out("")
out("### full table H (all difficulties/dungeons)")
for t in lt:
    if t["id"] == "H":
        out("  H diff=%s dungeon=%-8s : %s" % (t["difficulty"], t["dungeon"],
            "; ".join("%s=%s:%s x%s" % (e["type"], e["data"].get("type"), e["data"].get("id"), e["data"].get("amount"))
                      for e in t["entries"] if e["type"] != "nothing")))
out("")
out("### darkness_bonuses (raw)")
out(json.dumps(d["darkness_bonuses"], indent=1))
