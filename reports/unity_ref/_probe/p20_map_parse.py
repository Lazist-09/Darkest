import os, sys, struct, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

class Cur:
    def __init__(self, b):
        self.b = b; self.p = 0
    def i32(self):
        v = struct.unpack_from("<i", self.b, self.p)[0]; self.p += 4; return v
    def u8(self):
        v = self.b[self.p]; self.p += 1; return v
    def s(self):
        # .NET BinaryWriter 7-bit-encoded length prefix
        shift = 0; val = 0
        while True:
            byte = self.b[self.p]; self.p += 1
            val |= (byte & 0x7F) << shift
            if not (byte & 0x80):
                break
            shift += 7
        raw = self.b[self.p:self.p + val]; self.p += val
        return raw.decode("utf-8", "replace")

AREATYPE = {0: "Empty", 1: "Entrance", 2: "Tresure", 3: "Curio", 4: "Boss", 5: "Battle",
            6: "Trap", 7: "Hunger", 8: "Obstacle", 9: "Door", 10: "BattleCurio", 11: "BattleTresure"}
DIRECTION = {0: "Top", 1: "Bot", 2: "Left", 3: "Right"}

def read_prop(c):
    t = c.i32()
    if t == 9:          # Door
        return ("door", c.s(), DIRECTION.get(c.i32(), "?"))
    if t == 3:          # Curio
        q = c.u8()      # isQuestCurio
        return ("curio", "quest" if q else "normal", c.s())
    if t == 8:          # Obstacle
        return ("obstacle", c.s())
    if t == 6:          # Trap
        return ("trap", c.s())
    raise ValueError("prop type %d (%s) at %d" % (t, AREATYPE.get(t, "?"), c.p))

def read_area(c):
    d = {}
    d["id"] = c.s()
    d["x"] = c.i32(); d["y"] = c.i32()
    d["tex"] = c.s()
    d["type"] = c.i32(); d["know"] = c.i32()
    d["prop"] = read_prop(c) if c.u8() else None
    if c.u8():
        n = c.i32(); d["monsters"] = [c.s() for _ in range(n)]; d["cleared"] = c.u8()
    else:
        d["monsters"] = None
    return d

def parse(path):
    b = read_bytes(path)
    c = Cur(b)
    name = c.s(); gx = c.i32(); gy = c.i32(); start = c.s()
    nrooms = c.i32()
    rooms = []
    for _ in range(nrooms):
        r = read_area(c)
        nd = c.i32()
        r["doors"] = [(c.i32(), c.s(), DIRECTION.get(c.i32(), "?")) for _ in range(nd)]
        rooms.append(r)
    nhall = c.i32()
    halls = []
    for _ in range(nhall):
        hid = c.s(); a = c.s(); bb = c.s()
        nsec = c.i32()
        secs = [read_area(c) for _ in range(nsec)]
        halls.append({"id": hid, "a": a, "b": bb, "sectors": secs})
    nmash = c.i32()
    mash = [c.i32() for _ in range(nmash)]
    return b, c, dict(name=name, gx=gx, gy=gy, start=start, rooms=rooms, halls=halls, mash=mash)

md = os.path.join(REF, "Maps")
summary = []
for f in sorted(os.listdir(md)):
    if not f.endswith(".bytes"):
        continue
    try:
        b, c, d = parse(os.path.join(md, f))
    except Exception as e:
        out("=" * 74)
        out("Maps/%s <- PARSE FAILED: %s" % (f, e))
        continue
    consumed = c.p
    out("=" * 74)
    out("Maps/%s bytes=%d consumed=%d leftover=%d" % (f, len(b), consumed, len(b) - consumed))
    out("  Name=%r GridSize=(%d,%d) StartingRoomId=%r" % (d["name"], d["gx"], d["gy"], d["start"]))
    out("  rooms=%d hallways=%d shared_mash_ids=%s" % (len(d["rooms"]), len(d["halls"]), d["mash"]))
    tcount = collections.Counter(AREATYPE.get(r["type"], r["type"]) for r in d["rooms"])
    kcount = collections.Counter(r["know"] for r in d["rooms"])
    props = collections.Counter((r["prop"][0] if r["prop"] else "none") for r in d["rooms"])
    enc = sum(1 for r in d["rooms"] if r["monsters"])
    out("  room type codes=%s knowledge codes=%s props=%s rooms_with_encounter=%d" % (dict(tcount), dict(kcount), dict(props), enc))
    allsec = [s for h in d["halls"] for s in h["sectors"]]
    sprops = collections.Counter((s["prop"][0] if s["prop"] else "none") for s in allsec)
    senc = sum(1 for s in allsec if s["monsters"])
    out("  hall sectors=%d sector props=%s sectors_with_encounter=%d" % (len(allsec), dict(sprops), senc))
    doors = sum(len(r["doors"]) for r in d["rooms"])
    out("  total doors=%d  distinct monsters=%d" % (doors, len(set(m for r in d["rooms"] + allsec if r["monsters"] for m in r["monsters"]))))
    out("  room ids[0:6]=%s" % [r["id"] for r in d["rooms"][:6]])
    out("  hallway ids[0:4]=%s" % [h["id"] for h in d["halls"][:4]])
    out("  all room props: %s" % [(r["id"], r["prop"]) for r in d["rooms"] if r["prop"]])
    out("  hall sector props: %s" % [(s["id"], s["prop"]) for s in allsec if s["prop"]][:20])
    summary.append((f, d["name"], d["gx"], d["gy"], len(d["rooms"]), len(d["halls"]), len(allsec), consumed == len(b)))

out("")
out("### SUMMARY (file | Name | WxH | rooms | hallways | hall_sectors | byte-exact)")
for s in summary:
    out("  %-34s %-16s %2dx%-3d %3d %3d %3d %s" % s)
