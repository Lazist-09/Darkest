#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_side_effects.py —— 核 `buildings.side_effects.results[].type`
   与 `Effects.txt` 的 `type` 是否**同一套词汇** ✓（承接 `52_*.md` 的待核）

🔴 **为什么要核**：
   若**同一套** ⇒ 建筑的 `side_effects` 可以直接用 A6a 的 Effect 表 ✓
   若**不同套** ⇒ ⚠️ **又是两个命名空间**（A8 那次把两个混成一个 ⇒ 23 个假报）✓
   ⇒ 🎖️ **判据**：**"这两个 `type` 是【同一个词汇表】还是【两个同名字段】？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_side_effects.py
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
BLD = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Buildings"
EFF = os.path.join(REPO, "reports", "unity_ref", "effects_from_ref.json")
OUT = os.path.join(REPO, "reports", "unity_ref", "side_effects_crosscheck.json")


def main() -> int:
    # ---- ① 建筑 side_effects.results[].type ----
    types = Counter()
    loc = {}
    for f in sorted(os.listdir(BLD)):
        if not f.endswith(".building.json"):
            continue
        raw = io.open(os.path.join(BLD, f), encoding="utf-8-sig", errors="replace").read()
        body = re.sub(r",\s*([\]}])", r"\1", raw)     # 容错尾随逗号
        try:
            d = json.loads(body)
        except Exception as e:
            print(f"[x] 🔴 `{f}` 解析失败：{e}")
            continue
        stack = [("", d)]
        while stack:
            path, node = stack.pop()
            if isinstance(node, dict):
                for k, v in node.items():
                    p = f"{path}.{k}" if path else k
                    # 🔴 **口径（踩过坑，写死在这）**：只收【`results[]` 自身的 `type`】✓
                    #    ❌ **不能只要求"路径里有 `.results[`"** —— 因为
                    #       `change_currency` 的 `data[]` **也在 `results` 内部** ⚠️
                    #       它的 `type` 是【货币名】（`gold`）而不是【动作类型】✓
                    #    实测差异：放宽 ⇒ **8 种（含 `gold` ×4）**；严格 ⇒ **7 种** ✓
                    #    🎖️ 判据：**"这个 `type` 是【动作】还是【数据项】？看它【直接挂在谁下面】"** ✓
                    if (k == "type" and isinstance(v, str)
                            and re.search(r"\.results\[\d+\]\.type$", p)):
                        types[v] += 1
                        loc.setdefault(v, set()).add(f.replace(".building.json", ""))
                    stack.append((p, v))
            elif isinstance(node, list):
                for i, v in enumerate(node):
                    stack.append((f"{path}[{i}]", v))

    print(f"[x] 建筑 `side_effects.results[].type`：**{len(types)} 种** · {sum(types.values())} 处 ✓")
    for k, v in types.most_common():
        print(f"[x]   `{k}` ×{v}  ← {sorted(loc[k])}")
    print()

    # ---- ② Effects.txt 的 `type` ----
    fx = json.load(open(EFF, encoding="utf-8"))
    eff_types = Counter()
    for e in fx["effects"]:
        t = e["fields"].get("type") or e["fields"].get("curio_result_type")
        if isinstance(t, str):
            eff_types[t] += 1
    print(f"[x] Effect 的 `type`/`curio_result_type`：**{len(eff_types)} 种** ✓")
    for k, v in eff_types.most_common(12):
        print(f"[x]   `{k}` ×{v}")
    print()

    # ---- ③ 交集 ----
    a, b = set(types), set(eff_types)
    print(f"[x] 🎖️ **交集 {len(a & b)}**：{sorted(a & b)}")
    print(f"[x] 🔴 只在建筑侧：{sorted(a - b)}")
    print(f"[x] 🔴 只在 Effect 侧（前 12）：{sorted(b - a)[:12]}")
    print()

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "note": "建筑 side_effects.results[].type vs Effects.txt 的 type。只读不落库 ✓",
            "building_types": dict(types),
            "effect_types": dict(eff_types),
            "intersection": sorted(a & b),
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[x] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
