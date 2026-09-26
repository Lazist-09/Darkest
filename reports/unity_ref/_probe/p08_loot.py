import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

p = os.path.join(REF, "JsonLoot.json")
d = load(p)
out("TOP KEYS:", list(d.keys()))
for k, v in d.items():
    out("  %-25s %s" % (k, typestruct(v, 0, 1)))
out("")
for k, v in d.items():
    out("== %s ==" % k)
    out(json.dumps(v, indent=1)[:5000])
    out("")
