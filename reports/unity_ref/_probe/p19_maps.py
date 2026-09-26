import os, sys, struct, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

md = os.path.join(REF, "Maps")
for f in sorted(os.listdir(md)):
    if not f.endswith(".bytes"):
        continue
    b = read_bytes(os.path.join(md, f))
    out("=" * 74)
    out("Maps/%s bytes=%d" % (f, len(b)))
    out("  hex head:", b[:24].hex())
    p = 0
    # try 1-byte length-prefixed string
    n = b[p]; p += 1
    title = b[p:p + n].decode("ascii", "replace"); p += n
    a, c = struct.unpack_from("<ii", b, p); p += 8
    out("  1-byte-len title=%r  then int32 x2 = (%d, %d)  offset=%d" % (title, a, c, p))
    # try reading a length-prefixed strings
    q = p
    strs = []
    ok = True
    for i in range(a):
        if q >= len(b):
            ok = False; break
        L = b[q]; q += 1
        if L == 0 or q + L > len(b):
            ok = False; break
        s = b[q:q + L]
        if not all(32 <= ch < 127 for ch in s):
            ok = False; break
        strs.append(s.decode("ascii")); q += L
    out("  reading %d x[1-byte-len]strings -> ok=%s consumed=%d of %d" % (a, ok, q - p, len(b)))
    if ok:
        out("  first 6:", strs[:6], " last 3:", strs[-3:])
        out("  room-like count:", sum(1 for s in strs if "room" in s or "_to_" in s))
        out("  next bytes after list:", b[q:q + 24].hex())
    else:
        out("  failed at index %d, byte=0x%02x" % (len(strs), b[q] if q < len(b) else 0))

# count occurrences of key markers
out("")
out("=" * 74)
out("MARKER COUNTS in Maps/*.bytes")
for f in sorted(os.listdir(md)):
    if not f.endswith(".bytes"):
        continue
    b = read_bytes(os.path.join(md, f))
    out("  %-34s rooms=%3d  links=%3d  plot_=%2d  curr=%d" % (
        f, b.count(b"room"), b.count(b"_to_") // 1,
        b.count(b"plot_"), 0))

out("")
out("=" * 74)
out("MARKER COUNTS in Dungeons/*.bytes (text)")
for f in sorted(os.listdir(os.path.join(REF, "Dungeons"))):
    if not f.endswith(".bytes"):
        continue
    b = read_bytes(os.path.join(REF, "Dungeons", f))
    t = b.decode("utf-8-sig", "replace")
    lines = [l for l in t.splitlines() if l.strip()]
    out("  %-14s lines=%3d nonblank=%3d  'mash:'=%d  '.chance'=%d  'hall:'=%d  'named:'=%d  'room:'=%d" % (
        f, len(b.split(b"\r\n")), len(lines), t.count("mash:"), t.count(".chance"),
        t.count("hall:"), t.count("named:"), t.count("room:")))
