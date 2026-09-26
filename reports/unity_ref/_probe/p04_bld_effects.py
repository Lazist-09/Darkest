import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

bd = os.path.join(REF, "Buildings")

def dump(o, indent, path):
    pad = " " * indent
    if isinstance(o, dict):
        for k, v in o.items():
            if isinstance(v, (dict, list)):
                out("%s%s:" % (pad, k))
                dump(v, indent + 2, path + "." + k)
            else:
                out("%s%s = %s" % (pad, k, json.dumps(v)))
    elif isinstance(o, list):
        for i, v in enumerate(o):
            if isinstance(v, (dict, list)):
                out("%s[%d]" % (pad, i))
                dump(v, indent + 2, path)
            else:
                out("%s[%d] = %s" % (pad, i, json.dumps(v)))

for f in sorted(x for x in os.listdir(bd) if x.endswith(".json")):
    out("=" * 78)
    out("BUILDING FILE:", f)
    d = load(os.path.join(bd, f))
    dump(d, 1, "")
