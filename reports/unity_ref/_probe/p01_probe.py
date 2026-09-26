import io, json, os, sys, collections

REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"

def out(*a):
    s = " ".join(str(x) for x in a)
    sys.stdout.write(s.encode("ascii", "backslashreplace").decode("ascii") + "\n")

def load(p):
    with io.open(p, "r", encoding="utf-8-sig") as f:
        return json.load(f)

def typestruct(o, depth=0, maxd=3):
    """return a compact type descriptor"""
    if isinstance(o, dict):
        if depth >= maxd:
            return "dict(%d)" % len(o)
        return "{" + ", ".join("%s:%s" % (k, typestruct(v, depth+1, maxd)) for k, v in list(o.items())[:40]) + "}"
    if isinstance(o, list):
        if not o:
            return "list[0]"
        return "list[%d] of %s" % (len(o), typestruct(o[0], depth+1, maxd))
    if isinstance(o, str):
        return "str(%r)" % (o[:30],)
    if isinstance(o, bool):
        return "bool"
    if isinstance(o, int):
        return "int"
    if isinstance(o, float):
        return "float"
    if o is None:
        return "null"
    return type(o).__name__

# ---------- Buildings ----------
bd = os.path.join(REF, "Buildings")
files = sorted(f for f in os.listdir(bd) if f.endswith(".json"))
out("BUILDING FILES:", len(files))
for f in files:
    d = load(os.path.join(bd, f))
    out("=== %s ===" % f)
    out("  top-level type:", typestruct(d, 0, 6)[:4000])

out("")
out("=" * 60)
# ---------- Upgrades/Building ----------
ud = os.path.join(REF, "Upgrades", "Building")
ufs = sorted(f for f in os.listdir(ud) if f.endswith(".json"))
out("BUILDING UPGRADE FILES:", len(ufs))
for f in ufs:
    d = load(os.path.join(ud, f))
    out("=== %s ===" % f)
    out("  ", typestruct(d, 0, 5)[:3000])
