#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_a_line_coverage.py —— 核**我方 A 线 12 项**是否都对着【参考的真实文件集】✓

🔴 **为什么做**：`63_*.md` 量出参考只有 **10 类 / 320 文件** ✓
   ⇒ 📌 那就该问：**我方的 A1~A12 抽取，是不是都落在这 320 里？** ✓
   ⇒ ⚠️ 若某项抽了参考**没有**的东西 ⇒ 那是**另一个来源**（一手/DLC/E 盘）⇒ 要标出来 ✓

🎖️ **判据**：**"我的每个采集项，都能指出【它来自参考的哪个文件】吗？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_a_line_coverage.py
"""

from __future__ import annotations

import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REFDATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
REPORTS = os.path.join(REPO, "reports", "unity_ref")

# 🔴 A 线各项 → 它的产物文件（本任务已落）
#    ⚠️ **我第一版凭记忆写了两个不存在的文件名**（`buff_primitives_from_ref.json` /
#       `dd1_hero_tables_from_unity_ref.json`）⇒ 误报"缺产物" ⚠️
#    ⇒ ✅ **正确做法：先 `listdir` 取真实文件名**（第 17 条判据）
ITEMS = {
    # 🔴 A1 的产物在 `reports/` 根下（**不在 `unity_ref/`**）⇒ 记**两个位置都找** ✓
    "A1 buff 原语": ("dd1_buff_primitives.json", ".."),
    "A2 dmg%": ("skill_dmg_mapping.json", "."),
    "A3 英雄档": ("hero_skills_from_ref.json", "."),
    "A4 怪物": ("monsters_from_ref.json", "."),
    "A5 brains": ("ai_from_ref.json", "."),
    "A6a Effect": ("effects_from_ref.json", "."),
    "A6b act-out": ("traits_from_ref.json", "."),
    "A7 饰品/怪癖": ("trinkets_quirks_from_ref.json", "."),
    "A8 任务/掉落": ("quests_loot_narration_from_ref.json", "."),
    "A9 建筑": ("buildings_from_ref.json", "."),
    "A9b 地牢": ("dungeons_from_ref.json", "."),
    "A10 物品": ("items_and_provisions_from_ref.json", "."),
    "A11 兑换": None,
    "A12 奇物": ("curios_from_ref.json", "."),
}


def main() -> int:
    print(f"[cov] 参考 `Data/` 的类别：")
    cats = []
    for n in sorted(os.listdir(REFDATA)):
        p = os.path.join(REFDATA, n)
        if os.path.isdir(p):
            cnt = sum(1 for dp, d, fs in os.walk(p) for f in fs if not f.endswith(".meta"))
            cats.append((n, cnt))
    tot = sum(c for _, c in cats)
    for n, c in cats:
        print(f"[cov]   {n:16s} {c:>5}")
    print(f"[cov]   **合计 {tot}** ✓")
    print()

    print(f"[cov] 🎖️ 逐项核：**产物文件存在？· 它的 `source` 指向哪？**")
    print()
    ok = miss = nosrc = 0
    for label, spec in ITEMS.items():
        if spec is None:
            print(f"[cov]   ⚠️ `{label}` ⇒ 本任务未留 JSON 产物（用 CSV/直读）⇒ 记**未核**")
            nosrc += 1
            continue
        fn, where = spec
        base = REPORTS if where == "." else os.path.dirname(REPORTS)
        p = os.path.join(base, fn)
        if not os.path.isfile(p):
            print(f"[cov]   🔴 `{label}` ⇒ 产物 `{fn}` **不存在**")
            miss += 1
            continue
        try:
            d = json.load(open(p, encoding="utf-8"))
        except Exception as e:
            print(f"[cov]   🔴 `{label}` ⇒ 解析失败 {e}")
            miss += 1
            continue
        # 🔴 **溯源键有几种写法**（`source` / `_source` / `note` / `_note`）⇒ 都要看 ✓
        #    🎖️ 判据：**"这个产物的溯源写在【哪个键】里？我只查了一个吗？"** ✓
        src = None
        if isinstance(d, dict):
            for k in ("source", "_source", "note", "_note"):
                if isinstance(d.get(k), str) and d[k]:
                    src = d[k]
                    break
            if src is None:
                # 兜底：整份 JSON 里搜路径
                blob = json.dumps(d, ensure_ascii=False)
                i = blob.find("Darkest-Dungeon-Unity")
                src = blob[max(0, i - 40):i + 60] if i > 0 else None

        n = d.get("count") if isinstance(d, dict) else None
        mark = "✅" if src and "Darkest-Dungeon-Unity" in str(src) else "⚠️"
        print(f"[cov]   {mark} `{label}`  ({n if n is not None else '?'} 条)")
        print(f"[cov]        `{str(src)[:110]}`")
        if src and "Darkest-Dungeon-Unity" in str(src):
            ok += 1
        else:
            nosrc += 1
    print()
    print(f"[cov] ✅ 指向参考：{ok} · ⚠️ 无 source/未核：{nosrc} · 🔴 缺产物：{miss}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
