#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_adoption_rate.py —— 量 **各内容层的"采用率"**（我方条数 / 参考条数）✓

🔴 **为什么做**（承接 `161_*.md` 的 `curios` 6/60）：
   一个 6/60 ⇒ 📌 **那别的层呢？** ⇒ ✅ 本件**全面量一遍** ✓
   🎖️ **判据（第 313 条）**：**"我量出一个比例（6/60）——
      同类【别的层】也量了吗？（一个样本不构成'系统性'）"** ✓

🔴 **做法**：对每个有参考对应物的层 ⇒ 数**参考条数**与**我方条数** ⇒ 算比例 ✓
🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_adoption_rate.py
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
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
D = os.path.join(REPO, "darkest", "data")


def count_ours(f, key):
    p = os.path.join(D, f)
    if not os.path.isfile(p):
        return None
    d = json.load(open(p, encoding="utf-8"))
    # 🔴 按【键名】取，不按"第一个 list" —— 否则会取到 `rarities` 这类辅助表 ✓
    #    （实测踩过：`trinkets.json` 的第一个 list 是 `rarities` 14 条，真表 196 条）
    if isinstance(d, dict) and key in d and isinstance(d[key], list):
        return len([x for x in d[key] if not str(x.get("id", "")).startswith("__")])
    if isinstance(d, dict) and key in d and isinstance(d[key], dict):
        return len(d[key])
    return None


def main() -> int:
    print("[ar] 🔴 **各层的采用率**（我方条数 / 参考条数）")
    print()
    ROWS = [
        ("curios.json", "curios", "Curios/Curios.csv", "curio", 60),
        ("trinkets.json", "trinkets", "一手 196", "trinket", 196),
        ("buildings.json", "buildings", "Buildings/", "建筑", 8),
        ("hero_upgrades.json", "heroes", "upgrades/heroes", "英雄升级（职业数）", 15),
        ("quirks.json", "quirks", "quirk_library", "怪癖", 170),
        ("heirloom_exchange.json", "exchange_rates", "heirloom_exchange", "兑换", 12),
        ("buff_primitives.json", "primitives", "JsonBuffs", "buff 原语", 1801),
        ("hero_upgrades_flat.json", "x", "-", "-", 0),
    ]
    for f, key, refname, label, refn in ROWS:
        if f == "hero_upgrades_flat.json":
            continue
        n = count_ours(f, key)
        if n is None:
            print(f"[ar]   ⚪ `{f}` ⇒ **读不出条数**（键 `{key}` 不在）")
            continue
        pct = (n / refn * 100) if refn else 0
        tag = "🎖️" if pct >= 90 else ("⚠️" if pct >= 30 else "🔴")
        print(f"[ar]   {tag} **{label}**：`{f}` ⇒ **{n}** / 参考 **{refn}** "
              f"= **{pct:.0f}%**")
    print()
    print("[ar] 🎖️ **注**：'参考条数'来自本任务已核过的读数（非本件重数）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
