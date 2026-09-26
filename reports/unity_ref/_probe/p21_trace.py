import os, sys, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

b = read_bytes(os.path.join(REF, "Maps", "DD_map1.bytes"))

class Cur:
    def __init__(s, b): s.b = b; s.p = 0
    def i32(s):
        v = struct.unpack_from("<i", s.b, s.p)[0]; s.p += 4; return v
    def u8(s):
        v = s.b[s.p]; s.p += 1; return v
    def s(s):
        sh = 0; val = 0
        while True:
            by = s.b[s.p]; s.p += 1
            val |= (by & 0x7f) << sh
            if not (by & 0x80): break
            sh += 7
        raw = s.b[s.p:s.p + val]; s.p += val
        return raw.decode("utf-8", "replace")

c = Cur(b)
out("Name", c.s()); out("gx", c.i32()); out("gy", c.i32()); out("start", c.s())
n = c.i32(); out("nrooms", n)
for i in range(n):
    p0 = c.p
    rid = c.s(); x = c.i32(); y = c.i32(); tex = c.s(); t = c.i32(); k = c.i32()
    hp = c.u8(); prop = None
    if hp:
        pt = c.i32()
        out("  !! room %d prop type=%d at %d" % (i, pt, c.p))
        if pt == 9:
            prop = ("door", c.s(), c.i32())
        elif pt == 3:
            q = c.u8(); prop = ("curio", q, c.s())
        elif pt == 8:
            prop = ("obstacle", c.s())
        elif pt == 6:
            prop = ("trap", c.s())
        else:
            out("  UNKNOWN PROP TYPE", pt, "room", rid)
            break
    hb = c.u8(); enc = None
    if hb:
        mc = c.i32(); enc = [c.s() for _ in range(mc)]; cl = c.u8()
    nd = c.i32()
    doors = []
    for _ in range(nd):
        dt = c.i32()
        if dt != 9:
            out("  UNKNOWN DOOR TYPE %d room=%s at %d" % (dt, rid, c.p)); raise SystemExit
        doors.append((c.s(), c.i32()))
    out("  room %2d %-14s (%2d,%2d) tex=%-28s type=%-2d know=%d prop=%s enc=%s doors=%d  [%d..%d]" % (
        i, rid, x, y, tex, t, k, prop, enc, nd, p0, c.p))
