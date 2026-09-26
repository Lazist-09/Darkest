#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify_variant_level.py —— 在**变体级**验证"参考 = 一手 − 7 个怪" ✓

🔴 **为什么做**（承接 `67_*.md`）：
   那件在 **base 级**证明：**参考 94 base ⊂ 一手 101 目录**（= 101 − 7）✓
   ⇒ 📌 那就该问 **变体级**：**参考的 230 个变体文件 = 一手的变体集 − 那 7 个怪的变体吗？** ✓
      · 若**相等** ⇒ ✅ **参考的抽取方式彻底清楚了**（有损遍历）
      · 若**不等** ⇒ ⚠️ 还有别的机制（漏更多/多收）

🎖️ **判据**：**"我在【一个粒度】上证明了，换个粒度还成立吗？"** ✓
   📌 与"点查过不代表面查过"同族（`27_*.md`）✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/verify_variant_level.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ED = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
OUT = os.path.join(REPO, "reports", "unity_ref", "variant_level_check.json")

# 🔴 那 7 个被参考丢掉的怪（`62_*.md` 实测）
DROPPED = {"crow", "drowned_pirate", "nest", "skeleton_bearer",
           "swine_skiver", "virago_hateful", "virago_shroom"}


def main() -> int:
    # ---- 参考的变体集（从 A4 的产物）----
    mon = json.load(open(os.path.join(REPO, "reports", "unity_ref",
                                     "monsters_from_ref.json"), encoding="utf-8"))
    ref_var = {x["stem"] for x in mon["monsters"]}          # 如 `crow_A`
    ref_base = {re.sub(r"_[A-Z]$", "", s) for s in ref_var}
    print(f"[vv] 参考：**{len(ref_var)}** 变体 · **{len(ref_base)}** base ✓")

    # ---- 一手的变体集（扫 `monsters/<怪>/<怪>_X/<怪>_X.info.darkest`）----
    hand_var = set()
    mp = os.path.join(ED, "monsters")
    for d in sorted(os.listdir(mp)):
        dp = os.path.join(mp, d)
        if not os.path.isdir(dp):
            continue
        for sub in os.listdir(dp):
            sp = os.path.join(dp, sub)
            if os.path.isdir(sp) and re.fullmatch(re.escape(d) + r"_[A-Z]", sub):
                hand_var.add(sub)
    hand_base = {re.sub(r"_[A-Z]$", "", s) for s in hand_var}
    print(f"[vv] 一手：**{len(hand_var)}** 变体 · **{len(hand_base)}** base ✓")
    print()

    # ---- 三类比较 ----
    print(f"[vv] 🎖️ **变体级三方**")
    print(f"[vv]   参考 ∩ 一手     ：**{len(ref_var & hand_var)}**")
    print(f"[vv]   🔴 参考有、一手无：**{len(ref_var - hand_var)}**  {sorted(ref_var - hand_var)[:8]}")
    print(f"[vv]   🔴 一手有、参考无：**{len(hand_var - ref_var)}**  {sorted(hand_var - ref_var)[:12]}")
    print()

    # ---- 预测：一手 − 7 个怪 的变体 ----
    pred = {v for v in hand_var if re.sub(r"_[A-Z]$", "", v) not in DROPPED}
    print(f"[vv] 🎖️ 预测 `一手 − 那 7 个怪`：**{len(pred)}** 变体 ✓")
    print(f"[vv]   预测 == 参考？ **{pred == ref_var}**")
    if pred != ref_var:
        print(f"[vv]     🔴 预测有、参考无：**{len(pred - ref_var)}**  {sorted(pred - ref_var)[:10]}")
        print(f"[vv]     🔴 参考有、预测无：**{len(ref_var - pred)}**  {sorted(ref_var - pred)[:10]}")
    print()

    # ---- 那 7 个怪各自的变体数（看"漏"的粒度）----
    print(f"[vv] 那 7 个被丢的怪，各自的变体：")
    for d in sorted(DROPPED):
        vs = sorted(v for v in hand_var if v.startswith(d + "_"))
        print(f"[vv]   `{d}` ⇒ {len(vs)} 个：{vs}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "变体级验证：参考 == 一手 − 7 个怪？只读不落库 ✓",
                   "ref_variants": sorted(ref_var), "hand_variants": sorted(hand_var),
                   "predicted": sorted(pred), "equal": pred == ref_var,
                   "hand_not_ref": sorted(hand_var - ref_var),
                   "ref_not_hand": sorted(ref_var - hand_var)},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[vv] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
