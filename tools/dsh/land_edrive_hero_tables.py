#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""land_edrive_hero_tables.py -- re-land OUR 4 archetypes' 5-tier weapon/armour from the PRIMARY (E-drive).

WHY (the objective's hard requirement (a))
  Our tables were copied from the REFERENCE project (third-party).  The reconciliation
  (tools/dsh/reconcile_hero_tables_edrive_vs_ref.py) measured: of 495 compared hero-table fields,
  **402 agree and 97 differ** -- i.e. the third party is NOT identical to the primary.
  Our own rule (dd1_baseline 32.3) is: primary wins, and record the conflict.
  => so the substitution must come from the PRIMARY, not from the third party.

SAFETY (why this is zero-behaviour TODAY)
  The 5-tier tables are NOT consumed by combat yet (M1a landed them as data; the tier reading is
  M1c stage 3 / the tier wiring, still pending).  So re-landing them changes no battle number --
  and we prove it: build 0 errors + the full suite keeps its count + the mitigation baseline is untouched.

MAPPING (never hardcoded here)
  archetype -> E-drive hero is read from OUR OWN units.json `_align` field, e.g.
  "_align": "阶段 A 对齐来源：Hellion（参考项目 Darkest-Dungeon-Unity）逐阶照抄"  => hellion.

USAGE
  python tools/dsh/land_edrive_hero_tables.py            # dry run: print the delta only
  python tools/dsh/land_edrive_hero_tables.py --apply    # write units.json (backup first!)
"""

from __future__ import annotations

import io
import json
import os
import re
import shutil
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UNITS = os.path.join(REPO, "darkest", "data", "units.json")
EDRIVE = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
BACKUP_DIR = r"F:\GithubPro\Darkest-backup-20260921_003105-expflow"

WPN_RE = re.compile(
    r'\.name\s+"(?P<name>[a-z_]+_weapon_\d+)"\s+\.atk\s+(?P<atk>-?\d+(?:\.\d+)?)%\s+'
    r"\.dmg\s+(?P<dmin>-?\d+)\s+(?P<dmax>-?\d+)\s+\.crit\s+(?P<crit>-?\d+(?:\.\d+)?)%\s+"
    r"\.spd\s+(?P<spd>-?\d+)")
ARM_RE = re.compile(
    r'\.name\s+"(?P<name>[a-z_]+_armour_\d+)"\s+\.def\s+(?P<def>-?\d+(?:\.\d+)?)%\s+'
    r"\.prot\s+(?P<prot>-?\d+)\s+\.hp\s+(?P<hp>-?\d+)\s+\.spd\s+(?P<spd>-?\d+)")


def hero_from_align(align: str) -> str | None:
    """'…对齐来源：Hellion（参考项目…' -> 'hellion'  (no hardcoded table here)"""
    m = re.search(r"来源：\s*([A-Za-z][A-Za-z _-]*)", align or "")
    return m.group(1).strip().replace(" ", "_").replace("-", "_").lower() if m else None


def read_primary_hero(edrive: str, hero: str):
    p = os.path.join(edrive, "heroes", hero, "%s.info.darkest" % hero)
    if not os.path.isfile(p):
        return None
    txt = io.open(p, encoding="utf-8", errors="replace").read()
    wpn = [{"atk_pct": int(round(float(m.group("atk")))), "dmg_min": int(m.group("dmin")),
            "dmg_max": int(m.group("dmax")), "crit_pct": int(round(float(m.group("crit")))),
            "spd": int(m.group("spd"))} for m in WPN_RE.finditer(txt)]
    arm = [{"def_pct": int(round(float(m.group("def")))), "prot": int(m.group("prot")),
            "hp": int(m.group("hp")), "spd": int(m.group("spd"))} for m in ARM_RE.finditer(txt)]
    return {"weapon": wpn, "armour": arm}


def main() -> int:
    apply = "--apply" in sys.argv
    units = json.load(io.open(UNITS, encoding="utf-8"))

    deltas = []
    for u in units["units"]:
        if u["id"] not in ("warrior", "tank", "medic", "commissar"):
            continue
        hero = hero_from_align(u.get("_align", ""))
        if not hero:
            deltas.append((u["id"], "(无 _align ⇒ 跳过)", [], []))
            continue
        prim = read_primary_hero(EDRIVE, hero)
        if not prim:
            deltas.append((u["id"], hero + "（一手读不到 ⇒ 跳过）", [], []))
            continue
        wchg, achg = [], []
        for i, pv in enumerate(prim["weapon"]):
            cur = u.get("weapon") or []
            if i < len(cur) and cur[i] != pv:
                wchg.append((i, dict(cur[i]), dict(pv)))
        for i, pv in enumerate(prim["armour"]):
            cur = u.get("armour") or []
            if i < len(cur) and cur[i] != pv:
                achg.append((i, dict(cur[i]), dict(pv)))
        deltas.append((u["id"], hero, wchg, achg))
        if apply:
            u["weapon"] = prim["weapon"]
            u["armour"] = prim["armour"]
            u["_align"] = ("阶段 A 对齐来源：**一手 E 盘** %s/%s.info.darkest（逐阶照抄 ✓）"
                           "｜此前用的是第三方参考项目版；两者 %d 处不同 ⇒ 按 §32.3 **一手为准** ✓"
                           % (hero, hero, len(wchg) + len(achg)))

    print("=== 一手 vs 我们现存的 5 阶（逐原型） ===")
    for uid, hero, wchg, achg in deltas:
        print("  %-10s ← %-18s weapon 改 %d 阶 · armour 改 %d 阶" % (uid, hero, len(wchg), len(achg)))
        for i, old, new in (wchg + achg)[:4]:
            print("       [%d] %s  →  %s" % (i, json.dumps(old, ensure_ascii=False),
                                            json.dumps(new, ensure_ascii=False)))

    if apply:
        os.makedirs(BACKUP_DIR, exist_ok=True)
        dest = os.path.join(BACKUP_DIR, "units.before-edrive-tables.json")
        shutil.copyfile(UNITS, dest)
        io.open(UNITS, "w", encoding="utf-8").write(
            json.dumps(units, ensure_ascii=False, indent=2) + "\n")
        print("[land] APPLIED (backup: %s)" % os.path.basename(dest))
    else:
        print("[land] dry run -- pass --apply to write")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
