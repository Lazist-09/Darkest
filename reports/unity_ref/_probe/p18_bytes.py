import os, sys, json, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

dd = os.path.join(REF, "Dungeons")
for f in sorted(os.listdir(dd)):
    if not f.endswith(".bytes"):
        continue
    raw = read_bytes(os.path.join(dd, f))
    out("=" * 70)
    out("FILE Dungeons/%s  bytes=%d" % (f, len(raw)))
    out("  first 16 hex:", raw[:16].hex())
    txt = raw.decode("utf-8", "replace")
    lines = txt.split("\n")
    out("  lines:", len(lines), "  printable-ascii ratio: %.3f" % (
        sum(1 for c in txt[:4000] if 32 <= ord(c) < 127 or c in "\r\n\t") / max(1, len(txt[:4000]))))
    out("  first 12 lines:")
    for l in lines[:12]:
        out("    |" + l[:160])

md = os.path.join(REF, "Maps")
for f in sorted(os.listdir(md)):
    if not f.endswith(".bytes"):
        continue
    raw = read_bytes(os.path.join(md, f))
    out("=" * 70)
    out("FILE Maps/%s  bytes=%d" % (f, len(raw)))
    out("  first 32 hex:", raw[:32].hex())
    txt = raw.decode("utf-8", "replace")
    lines = txt.split("\n")
    out("  lines:", len(lines))
    out("  first 12 lines:")
    for l in lines[:12]:
        out("    |" + l[:160])

out("=" * 70)
raw = read_bytes(os.path.join(REF, "Inventory", "Items.bytes"))
out("FILE Inventory/Items.bytes bytes=%d" % len(raw))
out("  first 40 hex:", raw[:40].hex())
out("  first 400 raw:")
out("  " + repr(raw[:400]))
