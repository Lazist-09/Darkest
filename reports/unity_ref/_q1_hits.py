# -*- coding: utf-8 -*-
# Q1: exact per-hit table for 5 hero skill ids across the whole Assets tree (read-only).
import io, os, re, json
from collections import OrderedDict

REF = r"F:\GithubPro\Darkest-Dungeon-Unity"
ASSETS = os.path.join(REF, "Assets")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_q1_out.txt")
IDS = ["smite", "zealous_accusation", "holy_lance", "battle_heal", "inspiring_cry"]
SKIP_EXT = {".meta"}
NUM_FIELDS = [".dmg", ".atk", ".crit", ".heal", ".effect", ".launch", ".target",
              ".level", ".damage_low_multiply", ".damage_high_multiply", "damage_low_multiply",
              "damage_high_multiply", "DamageMod"]

lines = []
def w(s=""):
    lines.append(s)

# collect hits
hits = OrderedDict((i, OrderedDict()) for i in IDS)   # id -> relpath -> [(lineno, text)]
for root, dirs, files in os.walk(ASSETS):
    for fn in files:
        if os.path.splitext(fn)[1].lower() in SKIP_EXT:
            continue
        p = os.path.join(root, fn)
        try:
            with io.open(p, "r", encoding="utf-8", errors="strict") as f:
                txt = f.read()
        except Exception:
            continue
        if not any(i in txt for i in IDS):
            continue
        rel = os.path.relpath(p, REF)
        for n, raw in enumerate(txt.splitlines(), 1):
            for i in IDS:
                if i in raw:
                    hits[i].setdefault(rel, []).append((n, raw.strip()))

def has_num(text):
    return [f for f in NUM_FIELDS if f in text]

w("=== Q1 RAW HIT TABLE ===")
w("root: " + ASSETS)
w("ids: " + ", ".join(IDS))
w("")
for i in IDS:
    w("### id substring: %s" % i)
    total = sum(len(v) for v in hits[i].values())
    w("  files: %d ; matching lines: %d" % (len(hits[i]), total))
    for rel, rows in hits[i].items():
        anynum = any(has_num(t) for _, t in rows)
        w("  -- %s  lines=%d  has_numeric_field=%s" % (rel, len(rows), "YES" if anynum else "no"))
        # full listing only when the file looks numeric-relevant or small
        if anynum or len(rows) <= 15:
            for n, t in rows:
                w("       L%-6d %s" % (n, t[:200]))
        else:
            w("       linenos: " + ",".join(str(n) for n, _ in rows))
            w("       sample L%d: %s" % (rows[0][0], rows[0][1][:200]))
    w("")

# aggregate: numeric-bearing files only
w("=== Q1 NUMERIC-BEARING FILES (union over the 5 ids) ===")
numfiles = OrderedDict()
for i in IDS:
    for rel, rows in hits[i].items():
        nf = set()
        for _, t in rows:
            nf.update(has_num(t))
        if nf:
            e = numfiles.setdefault(rel, {"ids": set(), "fields": set(), "lines": []})
            e["ids"].add(i)
            e["fields"].update(nf)
            e["lines"].extend(rows)
for rel, e in numfiles.items():
    w("  %s" % rel)
    w("      ids: %s" % ", ".join(sorted(e["ids"])))
    w("      numeric fields present: %s" % ", ".join(sorted(e["fields"])))
    w("      lines: %d" % len(e["lines"]))
w("")
w("=== Q1 FILES WITH ZERO NUMERIC FIELDS ===")
for i in IDS:
    for rel, rows in hits[i].items():
        if rel in numfiles:
            continue
        w("  %-60s (%s) lines=%d" % (rel, i, len(rows)))

# exact values for the 5 hero skills in Crusader.bytes
w("")
w("=== Q1b  Crusader.bytes: the 5 queried ids, stat records (exact) ===")
C = os.path.join(ASSETS, r"Resources\Data\Heroes\Info\Crusader.bytes")
with io.open(C, "r", encoding="utf-8", errors="replace") as f:
    cl = f.read().splitlines()
for n, raw in enumerate(cl, 1):
    t = raw.strip()
    if not t.startswith("combat_skill:"):
        continue
    for i in IDS:
        if '.id "%s"' % i in t:
            w("  L%-4d %s" % (n, t[:200]))
            break

with io.open(OUT, "w", encoding="ascii", errors="replace") as f:
    f.write("\n".join(lines) + "\n")
print("WROTE " + OUT)
print("lines: %d" % len(lines))
