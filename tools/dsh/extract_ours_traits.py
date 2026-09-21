#!/usr/bin/env python3
"""extract_ours_traits.py -- project OUR 7 affliction/virtue buffs into data/traits.json (M3 step 1).

WHY derived, not hand-typed
  The project's discipline is "numbers come from a source, never from transcription".
  These 7 entries already exist inside darkest/data/buff_defs.json; M3 moves them to
  their own table (traits.json) *without changing a single value*.
  => generating the file from buff_defs.json guarantees a faithful move.

WHAT it does
  writes darkest/data/traits.json with:
    kind        : "affliction" (polarity negative) | "virtue" (polarity positive)
    ends_at     : the buff's duration.type, verbatim
    modifiers   : verbatim copy
    hooks       : verbatim copy
    source      : verbatim copy (traceability, per the project's origin discipline)
  plus origin: "ours" -- these are OUR morale-system traits (the original's semantics,
  our data) so they can never be mistaken for an E-drive extract.

USAGE
  python tools/dsh/extract_ours_traits.py [--root <repo root>]
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

# The 7 = the two duration families (measured; this is the discriminator, not the id name).
FAMILIES = {
    "until_morale_50": "affliction",
    "until_battle_end_or_morale_zero": "virtue",
}

# 🔴 MEASURED BOUNDARY (keep it visible, do not silently drop):
#   the duration-family discriminator also matches `bound` (until_morale_50) and
#   `pep_talk` (until_battle_end_or_morale_zero).  The card's category (1) says **7**,
#   and both of these belong to its category (3) "self-added" -- different reasons:
#     * `bound`     : the original's buff table has **no** counterpart (measured: 0 hits)
#     * `pep_talk`  : it is granted by a SKILL, not produced by the resolve check
#   => they are excluded here **with the reason written down**, and the counts are printed
#      so a 7-vs-9 disagreement can never hide.
SELF_ADDED = {
    "bound": "原版 buff 表 0 命中 (自加, 卡里 ③)",
    "pep_talk": "技能给的 (不是决心判定产物, 卡里 ③)",
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", type=Path, default=Path("."))
    args = ap.parse_args()

    src = args.root / "darkest" / "data" / "buff_defs.json"
    dst = args.root / "darkest" / "data" / "traits.json"
    buffs = json.loads(src.read_text(encoding="utf-8"))["buffs"]

    traits = []
    excluded = []
    for b in buffs:
        dur = (b.get("duration") or {}).get("type")
        kind = FAMILIES.get(dur)
        if kind is None:
            continue
        if b["id"] in SELF_ADDED:
            excluded.append((b["id"], SELF_ADDED[b["id"]]))
            continue
        entry = {
            "id": b["id"],
            "name": b.get("name", b["id"]),
            "kind": kind,
            "ends_at": dur,
            "origin": "ours",
        }
        if b.get("modifiers"):
            entry["modifiers"] = b["modifiers"]
        if b.get("hooks"):
            entry["hooks"] = b["hooks"]
        if b.get("source"):
            entry["source"] = b["source"]
        traits.append(entry)

    out = {
        "_note": "M3 第 1 步：折磨/美德从 buff_defs 搬到本表（**值原样** · 由 extract_ours_traits.py 生成 ✓）",
        "traits": traits,
    }
    dst.write_text(json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")

    aff = [t["id"] for t in traits if t["kind"] == "affliction"]
    vir = [t["id"] for t in traits if t["kind"] == "virtue"]
    print(f"[traits] wrote {dst}: {len(traits)} entries")
    print(f"[traits] affliction {len(aff)} = {aff}")
    print(f"[traits] virtue     {len(vir)} = {vir}")
    print(f"[traits] excluded (自加, 卡里 ③): {excluded}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
