import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

p = os.path.join(REF, "JsonQuests.json")
d = load(p)
out("TOP KEYS:", list(d.keys()))
for k, v in d.items():
    out("  %-30s %s" % (k, typestruct(v, 0, 1)))
out("")
out("FULL SKELETON:")
for k in d:
    out("== %s ==" % k)
    out(json.dumps(d[k], indent=1)[:2500])
    out("")
