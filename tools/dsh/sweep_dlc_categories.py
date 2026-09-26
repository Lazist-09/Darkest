#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""sweep_dlc_categories.py —— 清一手 `dlc/` 的**全部类别**，量化我方的缺口 ✓

🔴 **为什么做**（承接 `61`/`62_*.md`）：
   已知：一手把数据散在 **本体 + `dlc/<id>/` + `modes/<mode>/`** 三处 ✓
   已清：`heroes/`（漏 3 英雄）· `monsters/`（漏 29）✓
   ⇒ 🔴 **别的类别未清**（`campaign/` `inventory/` `dungeons/` `trinkets/` `curios/` …）⚠️
   ⇒ ✅ 本件做**全类别清单** ✓

🎖️ **判据**：**"我扫了目录"必须指明【扫了哪些位置】；而"位置"要【逐个类别】列，不能笼统**
   ⇒ 本件就是把它**做全** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/sweep_dlc_categories.py
"""

from __future__ import annotations

import os
import sys
from collections import defaultdict

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

ED = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
DATA_EXT = (".json", ".darkest", ".txt", ".csv")
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "reports", "unity_ref", "dlc_category_sweep.json")


def cats(root):
    """⇒ {类别: 文件数}（只数数据文件）✓"""
    out = defaultdict(int)
    if not os.path.isdir(root):
        return out
    for dirpath, _d, files in os.walk(root):
        for f in files:
            if f.endswith(".meta"):
                continue
            if os.path.splitext(f)[1].lower() in DATA_EXT:
                rel = os.path.relpath(dirpath, root).replace("\\", "/")
                top = rel.split("/")[0] if rel != "." else "(root)"
                out[top] += 1
    return out


def main() -> int:
    base = cats(ED)
    dlc_root = os.path.join(ED, "dlc")
    modes_root = os.path.join(ED, "modes")

    print("=" * 78)
    print("① 一手【本体】的数据文件（按类别）")
    print("=" * 78)
    for k in sorted(base, key=lambda x: -base[x]):
        print(f"  {k:24s} {base[k]:>6}")

    print()
    print("=" * 78)
    print("② 一手【DLC】的数据文件（逐 DLC → 逐类别）")
    print("=" * 78)
    dlc_by = {}
    for d in sorted(os.listdir(dlc_root)):
        p = os.path.join(dlc_root, d)
        if not os.path.isdir(p):
            continue
        c = cats(p)
        dlc_by[d] = dict(c)
        print(f"  **{d}**  合计 {sum(c.values())}")
        for k in sorted(c, key=lambda x: -c[x]):
            print(f"      {k:22s} {c[k]:>5}")

    print()
    print("=" * 78)
    print("③ 一手【游戏模式】的数据文件")
    print("=" * 78)
    if os.path.isdir(modes_root):
        for m in sorted(os.listdir(modes_root)):
            p = os.path.join(modes_root, m)
            if not os.path.isdir(p):
                continue
            c = cats(p)
            print(f"  **{m}**  合计 {sum(c.values())} ⇒ {dict(c)}")

    # 🔴 DLC 有、本体没有的【类别】（= 只在 DLC 里存在的类别）
    print()
    print("=" * 78)
    print("🔴 ④ 只在 DLC 里出现的【类别】（本体没有 ⇒ 只扫本体会整类漏掉）")
    print("=" * 78)
    dlc_cats = set()
    for c in dlc_by.values():
        dlc_cats |= set(c)
    only_dlc = sorted(dlc_cats - set(base))
    if only_dlc:
        for k in only_dlc:
            tot = sum(c.get(k, 0) for c in dlc_by.values())
            print(f"  🔴 **{k}** ⇒ DLC 里 {tot} 个文件（本体 0）")
    else:
        print("  ✅ 无（DLC 的类别本体都有）")

    import json
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "一手 dlc/modes 的全类别清单。只读不落库 ✓",
                   "base": dict(base), "dlc": dlc_by, "only_in_dlc": only_dlc},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"写出 {os.path.relpath(OUT, os.path.dirname(os.path.dirname(os.path.dirname(OUT))))} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
