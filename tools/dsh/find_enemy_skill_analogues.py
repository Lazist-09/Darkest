#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""find_enemy_skill_analogues.py —— 给我方**敌方 8 条技能**找参考原型（**按效果，不按名字**）✓

🔴 **为什么做**（承接 `117_*.md §6①`）：
   我方敌方 8 条**没有映射**（`§43` 只覆盖英雄）⇒
   📌 但它们**可能**借参考的【效果形状】⇒ ✅ 本件**按效果**去参考里找 ✓
   🎖️ **判据（第 141 条）**：**"按名字找不到 ——
      那就按【效果形状】找（位移/增益/群攻/Debuff）"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/find_enemy_skill_analogues.py
"""

from __future__ import annotations

import io
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
CODE = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"

# 我方 8 条技能的"效果形状"
SHAPES = [
    ("近战单目标伤害", ["melee", "target_ranks"]),
    ("自身前进 + 下次攻击加成", ["self_forward", "next_attack", "forward"]),
    ("远程单目标伤害", ["ranged", "target_ranks"]),
    ("远程 + 士气削减（威吓）", ["stress", "damage_low_multiply", "debuff"]),
    ("自身后撤", ["self_backward", "backward", "retreat"]),
    ("精神伤害 + 韧性削减", ["resilience", "mental", "mental_damage"]),
    ("群攻（AOE）", ["aoe", "all_targets", "target_group"]),
    ("移动（通用）", ["move", "displacement", "MoveEffect"]),
]


def main() -> int:
    print("=" * 76)
    print("🔴 逐个'效果形状'去参考里找（**数据 + 代码**）")
    print("=" * 76)
    for label, pats in SHAPES:
        print(f"\n--- 【{label}】 ⇒ 探针 {pats} ---")
        for pat in pats:
            n_data = n_code = 0
            sample = []
            for root, is_code in ((REF, False), (CODE, True)):
                for dp, _d, ns in os.walk(root):
                    for f in ns:
                        if f.endswith(".meta"):
                            continue
                        if is_code and not f.endswith(".cs"):
                            continue
                        if not is_code and f.endswith(".cs"):
                            continue
                        p = os.path.join(dp, f)
                        try:
                            t = io.open(p, encoding="utf-8-sig",
                                        errors="replace").read()
                        except Exception:
                            continue
                        c = t.count(pat)
                        if c:
                            if is_code:
                                n_code += c
                            else:
                                n_data += c
                            if len(sample) < 2:
                                sample.append(
                                    ("code" if is_code else "data",
                                     os.path.relpath(p, root)))
            verdict = "✅ 有" if (n_data or n_code) else "🔴 无"
            print(f"    {verdict} `{pat}` ⇒ data **{n_data}** · code **{n_code}**")
            for kind, rel in sample:
                print(f"        [{kind}] {rel}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
