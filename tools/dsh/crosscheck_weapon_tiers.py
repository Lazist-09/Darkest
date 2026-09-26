#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_weapon_tiers.py —— 核 **我方 4 英雄的 `weapon` 5 阶 vs 参考** ✓

🔴 **为什么做**（承接 `167_*.md` 的成果）：
   本件已证**护甲侧 4/4 完全一致** ⇒ 📌 **那【武器侧】呢？** ⇒ ✅ 本件核 ✓
   🎖️ **判据（第 345 条）**：**"我核了 A 侧（护甲）——
      【对称的 B 侧】（武器）核了吗？"** ✓

🔴 **参考侧**：`Heroes/Info/*.bytes` 的 `weapon:` 段（`.dmg` 区间 + `.crit` + `.spd`）✓
🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_weapon_tiers.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
D = os.path.join(REPO, "darkest", "data")
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info"

MAP = {"warrior": "Hellion", "tank": "ManAtArms", "medic": "PlagueDoctor",
       "commissar": "Highwayman"}

# 🔴 参考 weapon 段实测形态：
#    `weapon: .name "hellion_weapon_0" .atk 0% .dmg 6 12 .crit 2.5% .spd 4`
#    ⇒ ⚠️ `.atk` 夹在 `.name` 与 `.dmg` 之间 ⇒ 正则必须容忍它（首版漏了 ⇒ 0 阶）✓
WPAT = re.compile(r"weapon:\s*\.name\s+\"([^\"]+)\"\s*\.atk\s+(-?[0-9.]+)%\s*"
                  r"\.dmg\s+([0-9.]+)\s+([0-9.]+)\s*\.crit\s+([0-9.]+)%\s*\.spd\s+(-?[0-9.]+)")


def main() -> int:
    d = json.load(open(os.path.join(D, "units.json"), encoding="utf-8"))
    ours = {u["id"]: u.get("weapon") for u in d["units"]}
    print("[cw] 我方 `weapon` 5 阶：")
    for k, v in ours.items():
        if v:
            print(f"[cw]   `{k}` ⇒ {json.dumps(v, ensure_ascii=False)[:220]}")
    print()

    print("[cw] 🔴 参考 weapon 段：")
    for mine, refname in MAP.items():
        p = os.path.join(REF, f"{refname}.bytes")
        if not os.path.isfile(p):
            print(f"[cw]   🔴 `{refname}.bytes` 不存在")
            continue
        t = io.open(p, encoding="utf-8-sig", errors="replace").read()
        rows = WPAT.findall(t)
        print(f"[cw]   `{refname}`（↔ `{mine}`）⇒ {len(rows)} 阶")
        for r in rows[:6]:
            print(f"[cw]       {r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
