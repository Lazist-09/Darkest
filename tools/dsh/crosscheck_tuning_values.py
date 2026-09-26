#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_tuning_values.py —— 对 `tuning.json` 做**内容层**比对 ✓

🔴 **为什么做**（承接 `165_*.md` 的判据 335）：
   本任务已证 `tuning.json` 的**键名是我方自命名**（31 个键只 3~4 命中）⇒
   📌 **而【值】可能抄了参考** ⇒ ✅ 本件**逐值比** ✓
   🎖️ **判据（第 335 条）**：**"键名自造 + 值抄参考 ——
      这种组合下，'抄自哪'的答案是'值层面'，不是'文件层面'"** ✓

🔴 **做法**：挑几个**机制参数**（本任务已核过有参考对应的）⇒
   拿我方值去参考里找同名概念的公式/数值 ⇒ 比 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_tuning_values.py
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
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"


def main() -> int:
    t = json.load(open(os.path.join(D, "tuning.json"), encoding="utf-8"))
    print(f"[tv] `tuning.json` 顶层键 ⇒ **{len(t)}** ✓")
    print(f"[tv]   {list(t)[:14]}")
    print()

    # 🔴 本任务已核过有参考对应的机制（`142/144/146_*.md`）
    KNOWN = {
        "physical_mitigation": "物理减伤（我方 `def/(def+div)` vs 参考？）",
        "mental_reduction": "精神减伤",
        "damage_floor": "伤害下限（我方 1 · 参考 0）",
        "morale": "士气刻度（我方 0~100 起手 50）",
        "crit_multiplier": "暴击倍率",
    }
    for k, desc in KNOWN.items():
        v = t.get(k)
        print(f"[tv] 🔴 `{k}` ⇒ {json.dumps(v, ensure_ascii=False)[:150]}")
        print(f"[tv]     （{desc}）")

    print()
    print("[tv] 🎖️ **参考侧有没有这些机制**：")
    for probe in ("physical_mitigation", "damage_floor", "crit_multiplier",
                  "prot", "mitigation"):
        hits = []
        for dp, _d, ns in os.walk(REF):
            if dp[len(REF):].count(os.sep) > 4:
                _d[:] = []
                continue
            for f in ns:
                if f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                try:
                    x = io.open(p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    continue
                if re.search(probe, x, re.I):
                    hits.append(os.path.relpath(p, REF))
        tag = "✅" if hits else "🔴"
        print(f"[tv]   {tag} `{probe}` ⇒ **{len(hits)}** 份" + (f"（{hits[0]}）" if hits else ""))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
