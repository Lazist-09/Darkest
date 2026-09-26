import os, sys, json, collections, csv, io as _io
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

cd = os.path.join(REF, "Curios")
for f in ["Obstacles.json", "Traps.json"]:
    out("=" * 78)
    out("FILE Curios/" + f)
    with io.open(os.path.join(cd, f), "r", encoding="utf-8-sig") as fh:
        raw = fh.read()
    out("RAW (%d bytes):" % len(raw))
    out(raw[:3500])

out("=" * 78)
out("FILE Curios/Curios.csv")
raw = read_bytes(os.path.join(cd, "Curios.csv"))
out("BYTES:", len(raw), "header hex:", raw[:8].hex())
text = raw.decode("utf-8-sig", "replace")
lines = text.splitlines()
out("LINES:", len(lines), "ENCODING guess: utf-8-sig ok")
for i, l in enumerate(lines[:6]):
    out("  [%d] %s" % (i, l[:400]))
out("")
out("CSV parse:")
rows = list(csv.reader(_io.StringIO(text)))
out("  header cols =", len(rows[0]))
out("  header      =", rows[0])
out("  data rows   =", len(rows) - 1)
out("  first row   =", rows[1] if len(rows) > 1 else None)
