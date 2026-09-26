# -*- coding: utf-8 -*-
# Read-only statistics over F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\*.json
# NEVER loads a whole file into the agent context; only prints aggregates + a few samples.
import io, json, os, collections, sys, re

REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
OUT = io.open(r"F:\GithubPro\Darkest\reports\unity_ref\_scan_out.txt", "w", encoding="utf-8")

def w(*a):
    OUT.write(" ".join(str(x) for x in a) + "\n")

TRAILING = re.compile(r",(\s*[\]\}])")

def load(name):
    with io.open(os.path.join(REF, name), "r", encoding="utf-8-sig") as f:
        raw = f.read()
    try:
        return json.loads(raw)
    except Exception as e:
        w("!! %s strict JSON failed (%s); stripping trailing commas" % (name, e))
        return json.loads(TRAILING.sub(r"\1", raw))

def walk_keys(obj, prefix, freq, depth=0, maxdepth=6):
    """Flatten nested dict keys with dotted paths (lists = repeated element schema)."""
    if depth > maxdepth:
        return
    if isinstance(obj, dict):
        for k, v in obj.items():
            p = prefix + "." + k if prefix else k
            freq[p] += 1
            walk_keys(v, p, freq, depth + 1, maxdepth)
    elif isinstance(obj, list):
        for el in obj:
            walk_keys(el, prefix + "[]", freq, depth + 1, maxdepth)

def sample(entries, n=3, keys=None):
    out = []
    for e in entries[:n]:
        if keys:
            out.append({k: e.get(k) for k in keys})
        else:
            out.append(e)
    return out

def dump(name, top_keys, count, freq, smp, extra=""):
    w("=" * 78)
    w("### " + name)
    w("top-level keys:", top_keys)
    w("entry count:", count)
    if extra:
        w(extra)
    w("--- field frequency (dotted path -> #entries/occurrences) ---")
    for k, v in freq.most_common(400):
        w("   %-60s %d" % (k, v))
    w("--- samples ---")
    w(json.dumps(smp, indent=1, ensure_ascii=False)[:6000])
    w("")

# ---------------------------------------------------------------- JsonBuffs
b = load("JsonBuffs.json")
w("JsonBuffs top-level:", list(b.keys()))
buffs = b["buffs"]
w("JsonBuffs count:", len(buffs))
freq = collections.Counter()
for e in buffs:
    walk_keys(e, "", freq, 0, 4)
w("=== JsonBuffs field freq ===")
for k, v in freq.most_common(200):
    w("   %-55s %d" % (k, v))
w("=== JsonBuffs samples (first 3) ===")
w(json.dumps(buffs[:3], indent=1, ensure_ascii=False)[:3000])

# stat_type frequencies
st = collections.Counter(x.get("stat_type", "<none>") for x in buffs)
w("=== stat_type freq (count=%d distinct=%d) ===" % (sum(st.values()), len(st)))
for k, v in st.most_common():
    w("   %-45s %d" % (k, v))

# (stat_type, sub_type) pairs -- these are the PRIMITIVES
pair = collections.Counter((x.get("stat_type", "<none>"), x.get("stat_sub_type", "")) for x in buffs)
w("=== (stat_type, stat_sub_type) pairs: %d distinct ===" % len(pair))
for (a, c), v in pair.most_common():
    w("   %-45s | %-40s %d" % (a, c, v))

# stat_sub_type alone
sub = collections.Counter(x.get("stat_sub_type", "") for x in buffs)
w("=== stat_sub_type freq: %d distinct ===" % len(sub))
for k, v in sub.most_common():
    w("   %-45s %d" % (repr(k), v))

# rule_type
rt = collections.Counter(x.get("rule_type", "<none>") for x in buffs)
w("=== rule_type freq ===")
for k, v in rt.most_common():
    w("   %-45s %d" % (k, v))

# rule_data shape
rds = collections.Counter()
for x in buffs:
    rd = x.get("rule_data")
    if isinstance(rd, dict):
        rds[tuple(sorted(rd.keys()))] += 1
    else:
        rds[("<non-dict>",)] += 1
w("=== rule_data key-shapes ===")
for k, v in rds.most_common():
    w("   %-60s %d" % (str(k), v))

# id prefix groups
pref = collections.Counter()
import re
for x in buffs:
    i = x.get("id", "")
    m = re.match(r"^([A-Za-z_]+?)(?=[0-9_]*$)", i)
    pref[re.split(r"[_0-9]", i)[0]] += 1
w("=== buff id prefix (first token) top 40 ===")
for k, v in pref.most_common(40):
    w("   %-30s %d" % (k, v))

# amount distribution sanity
w("=== amount value stats ===")
vals = [x.get("amount") for x in buffs if isinstance(x.get("amount"), (int, float))]
w("count numeric:", len(vals), "min:", min(vals), "max:", max(vals))
w("zero amounts:", sum(1 for v in vals if v == 0))

# distinct (stat_type,sub_type) with example ids
w("=== primitive -> 2 example ids ===")
byp = collections.defaultdict(list)
for x in buffs:
    byp[(x.get("stat_type"), x.get("stat_sub_type"))].append(x.get("id"))
for (a, c), ids in sorted(byp.items(), key=lambda kv: -len(kv[1])):
    w("   %-45s | %-38s n=%-5d ex=%s" % (a, c, len(ids), ids[:2]))

# ---------------------------------------------------------------- JsonAI
a = load("JsonAI.json")
w("")
w("=" * 78)
w("JsonAI top-level:", list(a.keys()))
mb = a["monster_brains"]
w("monster_brains count:", len(mb))
freq = collections.Counter()
for e in mb:
    walk_keys(e, "", freq, 0, 5)
w("=== JsonAI field freq ===")
for k, v in freq.most_common(200):
    w("   %-60s %d" % (k, v))
w("=== JsonAI first 2 samples ===")
w(json.dumps(mb[:2], indent=1, ensure_ascii=False)[:5000])
w("=== JsonAI one brain with non-empty desires ===")
for e in mb:
    ds = e.get("skill_selection_desires") or []
    if len(ds) > 2:
        w(json.dumps(e, indent=1, ensure_ascii=False)[:4000])
        break
des = collections.Counter()
for e in mb:
    for d in (e.get("skill_selection_desires") or []):
        des[d.get("type")] += 1
w("=== desire `type` freq (across all brains) ===")
for k, v in des.most_common():
    w("   %-45s %d" % (k, v))
w("=== desire data key shapes ===")
dks = collections.Counter()
for e in mb:
    for d in (e.get("skill_selection_desires") or []):
        dks[(d.get("type"), tuple(sorted((d.get("data") or {}).keys())))] += 1
for k, v in dks.most_common(60):
    w("   %-70s %d" % (str(k), v))
w("=== brain id list (all) ===")
w(", ".join(e.get("id", "?") for e in mb))
# cooldowns / other fields
w("=== brains with non-empty skill_cooldowns: %d ===" % sum(1 for e in mb if e.get("skill_cooldowns")))
for e in mb:
    if e.get("skill_cooldowns"):
        w("   ex:", json.dumps(e, ensure_ascii=False)[:600])
        break

# ---------------------------------------------------------------- JsonQuirks
q = load("JsonQuirks.json")
w("")
w("=" * 78)
w("JsonQuirks top-level:", list(q.keys()))
qs = q["quirks"]
w("quirks count:", len(qs))
freq = collections.Counter()
for e in qs:
    walk_keys(e, "", freq, 0, 4)
w("=== JsonQuirks field freq ===")
for k, v in freq.most_common(100):
    w("   %-55s %d" % (k, v))
w("is_disease:", collections.Counter(e.get("is_disease") for e in qs))
w("is_positive:", collections.Counter(e.get("is_positive") for e in qs))
w("classification:", collections.Counter(e.get("classification") for e in qs))
w("incompatible count:", sum(1 for e in qs if e.get("incompatible_quirks")))
w("curio_tag freq:", collections.Counter(e.get("curio_tag") for e in qs).most_common(20))
w("=== JsonQuirks samples ===")
w(json.dumps(qs[:3], indent=1, ensure_ascii=False)[:3000])
w("=== disease samples ===")
w(json.dumps([e for e in qs if e.get("is_disease")][:2], indent=1, ensure_ascii=False)[:2000])
w("=== quirk ids (all) ===")
w(", ".join(e.get("id", "?") for e in qs))

# ---------------------------------------------------------------- JsonTraits
t = load("JsonTraits.json")
w("")
w("=" * 78)
w("JsonTraits top-level:", list(t.keys()))
ts = t["traits"]
w("traits count:", len(ts))
freq = collections.Counter()
for e in ts:
    walk_keys(e, "", freq, 0, 5)
w("=== JsonTraits field freq ===")
for k, v in freq.most_common(120):
    w("   %-60s %d" % (k, v))
w("overstress_type:", collections.Counter(e.get("overstress_type") for e in ts))
w("=== JsonTraits samples ===")
w(json.dumps(ts[:3], indent=1, ensure_ascii=False)[:5000])
w("=== trait ids ===")
w(", ".join(e.get("id", "?") for e in ts))

# ---------------------------------------------------------------- JsonTrinkets
k = load("JsonTrinkets.json")
w("")
w("=" * 78)
w("JsonTrinkets top-level:", list(k.keys()))
tr = k["trinkets"]
w("trinkets count:", len(tr))
freq = collections.Counter()
for e in tr:
    walk_keys(e, "", freq, 0, 4)
w("=== JsonTrinkets field freq ===")
for kk, v in freq.most_common(100):
    w("   %-55s %d" % (kk, v))
w("rarities:", k.get("rarities"))
w("rarity freq:", collections.Counter(e.get("rarity") for e in tr).most_common())
w("price freq (top 25):", collections.Counter(e.get("price") for e in tr).most_common(25))
w("limit freq:", collections.Counter(e.get("limit") for e in tr).most_common())
w("origin_dungeon freq:", collections.Counter(e.get("origin_dungeon") for e in tr).most_common(15))
w("hero_class_requirements non-empty:", sum(1 for e in tr if e.get("hero_class_requirements")))
w("hero_class_requirements values:", collections.Counter(
    ", ".join(sorted(e.get("hero_class_requirements") or [])) for e in tr).most_common(30))
nb = collections.Counter(len(e.get("buffs") or []) for e in tr)
w("num buffs per trinket dist:", sorted(nb.items()))
w("=== JsonTrinkets samples ===")
w(json.dumps(tr[:3], indent=1, ensure_ascii=False)[:3000])
w("=== multi-class restricted sample ===")
for e in tr:
    if len(e.get("hero_class_requirements") or []) > 1:
        w(json.dumps(e, indent=1, ensure_ascii=False)[:800]); break
w("=== trinket ids (all) ===")
w(", ".join(e.get("id", "?") for e in tr))

# ---------------------------------------------------------------- JsonCamping
c = load("JsonCamping.json")
w("")
w("=" * 78)
w("JsonCamping top-level:", list(c.keys()))
w("configuration:", c.get("configuration"))
cs = c["skills"]
w("skills count:", len(cs))
freq = collections.Counter()
for e in cs:
    walk_keys(e, "", freq, 0, 5)
w("=== JsonCamping field freq ===")
for kk, v in freq.most_common(120):
    w("   %-60s %d" % (kk, v))
w("cost freq:", collections.Counter(e.get("cost") for e in cs).most_common())
w("use_limit freq:", collections.Counter(e.get("use_limit") for e in cs).most_common())
w("level freq:", collections.Counter(e.get("level") for e in cs).most_common())
ef = collections.Counter()
for e in cs:
    for x in (e.get("effects") or []):
        ef[(x.get("type"), x.get("sub_type"), x.get("selection"))] += 1
w("=== effect (type,sub_type,selection) freq ===")
for kk, v in ef.most_common(60):
    w("   %-80s %d" % (str(kk), v))
w("=== JsonCamping samples ===")
w(json.dumps(cs[:3], indent=1, ensure_ascii=False)[:3000])
w("=== camp skill ids ===")
w(", ".join(e.get("id", "?") for e in cs))

# ---------------------------------------------------------------- JsonLoot
l = load("JsonLoot.json")
w("")
w("=" * 78)
w("JsonLoot top-level:", list(l.keys()))
freq = collections.Counter()
walk_keys(l, "", freq, 0, 5)
w("=== JsonLoot full field freq (whole doc) ===")
for kk, v in freq.most_common(150):
    w("   %-60s %d" % (kk, v))
for key in l:
    v = l[key]
    if isinstance(v, list):
        w("list section '%s' len=%d" % (key, len(v)))
        if v:
            w("  sample:", json.dumps(v[0], ensure_ascii=False)[:900])
    else:
        w("section '%s' = %s" % (key, json.dumps(v, ensure_ascii=False)[:400]))

# ---------------------------------------------------------------- Narration
n = load("Narration.json")
w("")
w("=" * 78)
w("Narration top-level:", list(n.keys()))
w("filters:", n.get("filters"))
en = n["entries"]
w("entries count:", len(en))
w("tone freq:", collections.Counter(e.get("tone") for e in en).most_common())
w("entry id prefixes:", collections.Counter(str(e.get("id", "")).split("_")[0] for e in en).most_common(30))
total_ae = sum(len(e.get("audio_events") or []) for e in en)
w("total audio_events:", total_ae)
freq = collections.Counter()
for e in en:
    walk_keys(e, "", freq, 0, 4)
w("=== Narration field freq ===")
for kk, v in freq.most_common(100):
    w("   %-60s %d" % (kk, v))
w("=== Narration samples ===")
w(json.dumps(en[:2], indent=1, ensure_ascii=False)[:3000])
w("=== all narration ids ===")
w(", ".join(str(e.get("id")) for e in en))

# ---------------------------------------------------------------- PartyNames
p = load("PartyNames.json")
w("")
w("=" * 78)
w("PartyNames top-level:", list(p.keys()))
pn = p["party_names"]
w("party_names count:", len(pn))
freq = collections.Counter()
for e in pn:
    walk_keys(e, "", freq, 0, 3)
w("=== PartyNames field freq ===")
for kk, v in freq.most_common(50):
    w("   %-60s %d" % (kk, v))
w("=== PartyNames samples ===")
w(json.dumps(pn[:3], indent=1, ensure_ascii=False)[:1500])

OUT.close()
print("done")
