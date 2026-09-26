import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

bd = os.path.join(REF, "Buildings")
files = sorted(f for f in os.listdir(bd) if f.endswith(".json"))
out("BUILDING FILES:", len(files), files)
out("")
for f in files:
    d = load(os.path.join(bd, f))
    out("=== %s ===" % f)
    out("  ", typestruct(d, 0, 8)[:5000])
    out("")

out("=" * 70)
ud = os.path.join(REF, "Upgrades", "Building")
ufs = sorted(f for f in os.listdir(ud) if f.endswith(".json"))
out("BUILDING UPGRADE FILES:", len(ufs), ufs)
out("")
for f in ufs:
    d = load(os.path.join(ud, f))
    out("=== %s ===" % f)
    out("  ", typestruct(d, 0, 8)[:5000])
    out("")
