#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_effects_consumption.py —— 952 条 effect 的**消费点对账** ✓

🔴 **为什么要做**（同法三次抓到东西）：
   ① `plot_quests` 13 字段 ⇒ **9 个无消费**（6 个有数据没人读）
   ② act-out 29 种 ⇒ **2 个无消费**（数据有值）
   ③ 本件：**952 条 effect** ⇒ ⇒ ❓
   ⇒ 📌 **"数据有、没人读"是本仓的高发问题** ⇒ 每层都该量一次 ✓

🔴 **判据（前两次用的同一条）**：
   **消费点 = 真正影响行为的引用** ——
   ✅ 算：`Data.Effects["名字"]` 被取出后有分支/被 apply
   ❌ 不算：**数据文件里的字符串**（`Effects.txt` 自己、`JsonTraits` 等的引用）
   ❌ 不算：**纯解析**（`DarkestJsonReader`）与 **纯定义**（`Effects.txt`）

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_effects_consumption.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys
from collections import Counter, defaultdict

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF = r"F:\GithubPro\Darkest-Dungeon-Unity"
SCRIPTS = os.path.join(REF, "Assets", "Scripts")
DATA = os.path.join(REF, "Assets", "Resources", "Data")
OUT = os.path.join(REPO, "reports", "unity_ref", "effects_consumption.json")

# 🔴 数据侧引用：哪些文件会【按名字】提到 effect
DATA_FILES = ("JsonTraits.json", "JsonQuests.json", "JsonLoot.json", "JsonTrinkets.json",
              "JsonQuirks.json", "JsonAI.json", "JsonCamping.json", "Narration.json")
SKILL_DIR = os.path.join(DATA, "Heroes", "Info")
MONSTER_DIR = os.path.join(DATA, "Monsters")


def main() -> int:
    fx = json.load(open(os.path.join(REPO, "reports", "unity_ref", "effects_from_ref.json"),
                        encoding="utf-8"))
    names = [e["name"] for e in fx["effects"]]
    print(f"[fx] 载入 **{len(names)}** 条 effect ✓")

    # ---- ① 数据侧：谁引用了它们 ----
    # 🔴 **口径（本件踩过坑，写死在这）**：
    #    ✅ **必须用【加引号的完整串】** `"名字"` —— 它问的是**"被【独立引用】了吗"** ✓
    #    ❌ **不能用【子串包含】** `if n in text` —— 会命中**嵌在更长名字里**的情况 ⚠️
    #    实测差异：**子串 ⇒ 被引用 822 / 孤儿 130**；**加引号 ⇒ 被引用 754 / 孤儿 198** ✓
    #    ⇒ 🔴 **68 个是【子串假命中】**（如 `Blight 2` 被 `Crabby Blight 2` 带出来）✓
    cited = defaultdict(set)
    def scan_text(text, label):
        for n in names:
            if f'"{n}"' in text:
                cited[n].add(label)

    for fn in DATA_FILES:
        p = os.path.join(DATA, fn)
        if os.path.isfile(p):
            scan_text(io.open(p, encoding="utf-8-sig", errors="replace").read(), fn)
    for d, label in ((SKILL_DIR, "Heroes/Info"), (MONSTER_DIR, "Monsters")):
        if os.path.isdir(d):
            for f in sorted(os.listdir(d)):
                if f.endswith(".meta"):
                    continue
                scan_text(io.open(os.path.join(d, f), encoding="utf-8-sig",
                                  errors="replace").read(), label)

    # ---- ② 代码侧：哪些 effect 名真的出现在 .cs 里（作为字符串字面量）----
    #   🔴 判据：**只有【字面量】才算"点名"**；`Data.Effects[变量]` 不算 ✓
    literal = set()
    code_text = []
    for dirpath, _dd, fns in os.walk(SCRIPTS):
        for f in fns:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            code_text.append((os.path.relpath(p, SCRIPTS), t))
    for n in names:
        pat = '"' + n + '"'
        for rel, t in code_text:
            if pat in t:
                literal.add(n)
                break

    print()
    print(f"[fx] 🎖️ **数据侧被引用**（去重）：**{len(cited)}** / {len(names)}")
    print(f"[fx] 🎖️ **代码侧有字面量**（去重）：**{len(literal)}** / {len(names)}")

    # 🔴 **两个口径必须分开报**（我上一版把它俩混了 ⇒ 数差 8）⚠️
    #    ① **纯数据口径**：`n not in cited` ⇒ 这是「**从未被数据点名**」✓
    #    ② **合并口径**：`n not in cited and n not in literal` ⇒ 这是「**数据与代码都没提**」✓
    #    📌 两者【不是同一个问题】：② 把"只在代码里出现"的算作"被提到" ⇒ **数会少** ✓
    #    🎖️ 判据：**"我这张表回答的是【哪个问题】？改一个条件，数会变吗？"** ✓
    never_data = [n for n in names if n not in cited]
    never_both = [n for n in never_data if n not in literal]
    only_code = [n for n in names if n not in cited and n in literal]
    print(f"[fx] 🔴 **从未被【数据】点名**：**{len(never_data)}** ✓ ← 本件主口径")
    print(f"[fx] 🔴 **数据与代码【都没提】**：**{len(never_both)}** "
          f"（= 上者 − 只在代码里的 {len(only_code)}）✓")
    print(f"[fx] 🎖️ **只在代码里**（数据没引用）：**{len(only_code)}** "
          f"⇒ ⚠️ 采用时【必须一并改代码】✓")
    print(f"[fx]   样例：{never_data[:15]}")
    never = never_data
    print()

    # ---- ③ 那"两边都没有"的，按前缀归类（看它们是什么）----
    pref = Counter()
    for n in never:
        pref[n.split()[0] if " " in n else n.split("_")[0]] += 1
    print("[fx] 那些从未被点名的 effect，按首词归类（前 15）：")
    for k, v in pref.most_common(15):
        print(f"[fx]   `{k}`  {v}")
    print()

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "note": "952 条 effect 的消费点对账（数据引用 + 代码字面量）。只读不落库 ✓",
            "total": len(names),
            "cited_in_data": {k: sorted(v) for k, v in cited.items()},
            "literal_in_code": sorted(literal),
            "never_named": never,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[fx] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
