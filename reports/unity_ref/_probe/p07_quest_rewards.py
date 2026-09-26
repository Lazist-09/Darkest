import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

d = load(os.path.join(REF, "JsonQuests.json"))
g = d["generation"]
out("GENERATION KEYS:", list(g.keys()))
for k, v in g.items():
    out("-- %s : %s" % (k, typestruct(v, 0, 1)))
out("")
rw = g["rewards"]
out("REWARDS KEYS:", list(rw.keys()))
out("")
out("### heirloom_type_map")
out(json.dumps(rw.get("heirloom_type_map"), indent=1))
out("")
out("### heirloom_amount_table")
out(json.dumps(rw.get("heirloom_amount_table"), indent=1)[:8000])
out("")
out("### resolve_xp_reward")
out(json.dumps(rw.get("resolve_xp_reward"), indent=1)[:3000])
out("")
out("### trinket_chances")
out(json.dumps(rw.get("trinket_chances"), indent=1)[:3000])
out("")
out("### item_table")
out(json.dumps(rw.get("item_table"), indent=1)[:8000])
for k in rw:
    out("REWARDKEY %s : %s" % (k, typestruct(rw[k], 0, 2)))
