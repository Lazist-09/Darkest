import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

ud = os.path.join(REF, "Upgrades", "Building")
for f in ["abbey.upgrades.json", "stage_coach.upgrades.json", "blacksmith.upgrades.json",
          "sanitarium.upgrades.json", "nomad_wagon.upgrades.json", "guild.upgrades.json",
          "camping_trainer.upgrades.json", "tavern.upgrades.json"]:
    d = load(os.path.join(ud, f))
    out("=" * 70)
    out("FILE", f)
    for t in d["trees"]:
        out(" TREE id=%s instanced=%s tags=%s reqs=%d" % (t["id"], t["is_instanced"], t["tags"], len(t["requirements"])))
        for r in t["requirements"]:
            cs = ", ".join("%s=%s" % (c["type"], c["amount"]) for c in r.get("currency_cost", []))
            out("   req code=%s prereq=%s cost[%s]" % (r["code"], r.get("prerequisite_requirements"), cs))
        extra = [k for k in t.keys() if k not in ("id", "is_instanced", "tags", "requirements")]
        if extra:
            out("   EXTRA KEYS:", extra)
            for k in extra:
                out("     %s = %s" % (k, json.dumps(t[k])[:600]))
