import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

m = os.path.join(REF, "Mechanics")
for f in ["HeirloomExchange.json", "Campaign.json", "Roster.json"]:
    out("=" * 78)
    out("FILE Mechanics/" + f)
    p = os.path.join(m, f)
    with io.open(p, "r", encoding="utf-8-sig") as fh:
        raw = fh.read()
    out("RAW BYTES:", len(raw))
    out(raw[:6000])
    out("...")
    out("TYPESTRUCT:", typestruct(load(p), 0, 8)[:4000])
