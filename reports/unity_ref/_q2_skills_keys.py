# -*- coding: utf-8 -*-
# Read-only analysis of the Unity reference project (Darkest-Dungeon-Unity).
# Emits ASCII-only output to _q2_out.txt (no non-ASCII on stdout).
import io, os, re, json
from collections import Counter, OrderedDict

REF = r"F:\GithubPro\Darkest-Dungeon-Unity"
INFO_DIR = os.path.join(REF, r"Assets\Resources\Data\Heroes\Info")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_q2_out.txt")
lines_out = []
def w(s=""):
    lines_out.append(s)

KEY_RE = re.compile(r"^\.[A-Za-z_][A-Za-z0-9_]*$")
PREFIX_RE = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*):")

def read_text(p):
    with io.open(p, "r", encoding="utf-8", errors="replace") as f:
        return f.read()

def tokenize(line):
    """Split respecting double quotes; return list of tokens (quotes stripped)."""
    toks, cur, inq = [], "", False
    for ch in line:
        if ch == '"':
            inq = not inq
            continue
        if ch.isspace() and not inq:
            if cur:
                toks.append(cur)
                cur = ""
            continue
        cur += ch
    if cur:
        toks.append(cur)
    return toks

files = sorted(f for f in os.listdir(INFO_DIR) if f.endswith(".bytes"))
w("=== FILES (%d) ===" % len(files))
for f in files:
    w("  " + f)

cs_all = Counter()          # every .key on any combat_skill: line
cs_art = Counter()          # combat_skill: line WITHOUT .level  (art/visual block)
cs_stat = Counter()         # combat_skill: line WITH .level     (stat block)
cs_per_file = OrderedDict() # file -> (art_records, stat_records)
prefix_all = Counter()      # record prefixes across all files
prefix_per_file = OrderedDict()
other_prefix_keys = OrderedDict()  # prefix -> Counter(keys)  (non-combat_skill)
record_counts = Counter()

for f in files:
    path = os.path.join(INFO_DIR, f)
    txt = read_text(path)
    pf = Counter()
    art = stat = 0
    for raw in txt.splitlines():
        line = raw.strip()
        if not line:
            continue
        m = PREFIX_RE.match(line)
        if not m:
            continue
        prefix = m.group(1) + ":"
        pf[prefix] += 1
        prefix_all[prefix] += 1
        toks = tokenize(line)
        keys = [t for t in toks[1:] if KEY_RE.match(t)]
        if prefix == "combat_skill:":
            record_counts["combat_skill:"] += 1
            for k in keys:
                cs_all[k] += 1
            if ".level" in keys:
                stat += 1
                for k in keys:
                    cs_stat[k] += 1
            else:
                art += 1
                for k in keys:
                    cs_art[k] += 1
        else:
            record_counts[prefix] += 1
            if prefix not in other_prefix_keys:
                other_prefix_keys[prefix] = Counter()
            for k in keys:
                other_prefix_keys[prefix][k] += 1
    cs_per_file[f] = (art, stat)
    prefix_per_file[f] = pf

def dump_counter(title, c, total_label="records"):
    w("")
    w("=== %s ===" % title)
    w("total %s: %d ; distinct keys: %d" % (total_label, sum(c.values()), len(c)))
    for k, v in c.most_common():
        w("  %-24s x%d" % (k, v))

dump_counter("Q2a  combat_skill: ALL .keys (art + stat records)", cs_all, "key-occurrences")
dump_counter("Q2b  combat_skill: .keys in ART records (no .level)", cs_art, "key-occurrences")
dump_counter("Q2c  combat_skill: .keys in STAT records (has .level)", cs_stat, "key-occurrences")

w("")
w("=== Q2d  combat_skill: record counts per file (art_records / stat_records) ===")
for f, (a, s) in cs_per_file.items():
    w("  %-22s art=%d stat=%d total=%d" % (f, a, s, a + s))

w("")
w("=== Q2e  ALL record prefixes across 15 files ===")
tot = 0
for k, v in prefix_all.most_common():
    w("  %-22s x%d" % (k, v))
    tot += v
w("  TOTAL records: %d" % tot)

w("")
w("=== Q2f  record prefixes PER FILE ===")
for f in files:
    parts = ", ".join("%s x%d" % (k, v) for k, v in sorted(prefix_per_file[f].items()))
    w("  %s" % f)
    w("      " + parts)

w("")
w("=== Q2g  .keys used by NON-combat_skill record prefixes ===")
for pfx in sorted(other_prefix_keys.keys()):
    c = other_prefix_keys[pfx]
    parts = ", ".join("%s x%d" % (k, v) for k, v in c.most_common())
    w("  %s (x%d records) : %s" % (pfx, record_counts[pfx], parts))

# ---------------- Q3: damage-multiplier-ish fields ----------------
w("")
w("=== Q3  scan for damage percent / multiplier field names ===")
CAND = [".dmg", ".atk", ".crit", ".heal", ".damage_low_multiply", ".damage_high_multiply",
        ".attack_rating_add", ".crit_chance_add", ".damage_multiply", ".dmg_mod",
        ".damage_mod", ".dmg_mult", ".atk%", ".dmg%"]
w("candidate field names searched: " + ", ".join(CAND))

# hero info files: whole-file scan per candidate
w("")
w("--- Q3a hero Info/*.bytes: occurrences of candidate fields ---")
hero_field_counter = Counter()
hero_pct_counter = Counter()
hero_samples = {}
for f in files:
    txt = read_text(os.path.join(INFO_DIR, f))
    toks_all = []
    for raw in txt.splitlines():
        toks_all.extend(tokenize(raw))
    # walk tokens; value follows key
    i = 0
    while i < len(toks_all):
        t = toks_all[i]
        if KEY_RE.match(t) and t in CAND:
            val = toks_all[i + 1] if i + 1 < len(toks_all) else ""
            hero_field_counter[t] += 1
            if val.endswith("%"):
                hero_pct_counter[t] += 1
                hero_samples.setdefault(t, [])
                if len(hero_samples[t]) < 4:
                    hero_samples[t].append("%s @%s -> %s" % (f, t, val))
        i += 1
for k, v in hero_field_counter.most_common():
    w("  %-24s occurrences=%d (percent-valued=%d)" % (k, v, hero_pct_counter.get(k, 0)))
for k, s in hero_samples.items():
    for x in s:
        w("      sample: " + x)

# monster txt: flat .dmg pairs
w("")
w("--- Q3b Monsters/*.txt: .atk / .dmg value shapes ---")
MON = os.path.join(REF, r"Assets\Resources\Data\Monsters")
mon_files = sorted(f for f in os.listdir(MON) if f.endswith(".txt"))
mon_atk_pct = mon_dmg_flat = mon_dmg_pct = mon_skill_records = 0
mon_samples = []
for f in mon_files:
    txt = read_text(os.path.join(MON, f))
    for raw in txt.splitlines():
        line = raw.strip()
        if not line.startswith("skill:"):
            continue
        mon_skill_records += 1
        toks = tokenize(line)
        for i, t in enumerate(toks):
            if t == ".atk" and i + 1 < len(toks) and toks[i + 1].endswith("%"):
                mon_atk_pct += 1
            if t == ".dmg" and i + 2 < len(toks):
                a, b = toks[i + 1], toks[i + 2]
                if a.endswith("%"):
                    mon_dmg_pct += 1
                else:
                    mon_dmg_flat += 1
                    if len(mon_samples) < 3:
                        mon_samples.append("%s: .dmg %s %s" % (f, a, b))
w("  monster files scanned: %d ; 'skill:' stat records: %d" % (len(mon_files), mon_skill_records))
w("  .atk with %% : %d   .dmg flat(min max): %d   .dmg with %% : %d" % (mon_atk_pct, mon_dmg_flat, mon_dmg_pct))
for s in mon_samples:
    w("      sample: " + s)

# Effects.txt
w("")
w("--- Q3c Mechanics/Effects.txt: effect multiplier fields ---")
EFF = os.path.join(REF, r"Assets\Resources\Data\Mechanics\Effects.txt")
eff_txt = read_text(EFF)
eff_lines = eff_txt.splitlines()
w("  total lines: %d" % len(eff_lines))
eff_counter = Counter()
eff_samples = {}
eff_pct = Counter()
for raw in eff_lines:
    line = raw.strip()
    if not line.startswith("effect:"):
        continue
    toks = tokenize(line)
    for i, t in enumerate(toks):
        if KEY_RE.match(t):
            eff_counter[t] += 1
            v = toks[i + 1] if i + 1 < len(toks) else ""
            if v.endswith("%"):
                eff_pct[t] += 1
                eff_samples.setdefault(t, [])
                if len(eff_samples[t]) < 3:
                    nm = toks[1] if len(toks) > 1 else "?"
                    eff_samples[t].append("effect \"%s\" -> %s" % (nm, v))
w("  distinct effect keys: %d" % len(eff_counter))
for k in [".damage_low_multiply", ".damage_high_multiply", ".attack_rating_add",
          ".crit_chance_add", ".healstress", ".torch_increase", ".heal", ".chance"]:
    if k in eff_counter:
        w("  %-24s records=%d (percent-valued=%d)" % (k, eff_counter[k], eff_pct.get(k, 0)))
w("  --- top 20 effect keys overall ---")
for k, v in eff_counter.most_common(20):
    w("      %-24s x%d" % (k, v))
for k in [".damage_low_multiply", ".damage_high_multiply"]:
    for s in eff_samples.get(k, [])[:3]:
        w("      sample: " + s)

# where is damage_low_multiply referenced in code
w("")
w("--- Q3d code references (Assets/Scripts) ---")
for root, dirs, fs in os.walk(os.path.join(REF, "Assets", "Scripts")):
    for f in fs:
        if not f.endswith(".cs"):
            continue
        p = os.path.join(root, f)
        t = read_text(p)
        for n, l in enumerate(t.splitlines(), 1):
            for pat in ("damage_low_multiply", "damage_high_multiply", "DamageMod", ".dmg", ".atk"):
                if pat in l:
                    w("  %s:%d: %s" % (os.path.relpath(p, REF), n, l.strip()[:180]))
                    break

with io.open(OUT, "w", encoding="ascii", errors="replace") as f:
    f.write("\n".join(lines_out) + "\n")
print("WROTE " + OUT)
print("lines: %d" % len(lines_out))
