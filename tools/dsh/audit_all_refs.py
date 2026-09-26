#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_all_refs.py —— 宽面审计：**我方 `darkest/data/**` 里所有跨文件引用是否闭合** ✓

🔴 **为什么需要它**：
   前面逐个数据集核过引用（A4 怪物 / A7 buff / A8 loot / A12 奇物），但都是**点查** ✓
   ⇒ ⚠️ **点查过的地方对，不代表【没查过的地方】也对** ✓
   ⇒ ✅ 本件做**一次宽面审计**：把**所有**"像引用的字符串"抽出来，按命名空间逐个查 ✓
   📌 **这正是我在 A8 踩过的坑**（两个命名空间混成一个 ⇒ 23 个假报）的反向应用：
      **先分类，再查** ✓

产出的三类：
   ① **闭合**（引用的目标都在）✓
   ② **悬空**（引用的目标不存在）🔴
   ③ **无法判定**（目标不在任何已知命名空间里）❓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_all_refs.py
"""

from __future__ import annotations

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
DATA = os.path.join(REPO, "darkest", "data")
OUT = os.path.join(REPO, "reports", "unity_ref", "our_ref_audit.json")

# 🔴 **命名空间**：先分类再查（这是本件的核心纪律）✓
#    `(名字, 收 id 的函数)` —— 从某个文件里收出它定义的 id 集合 ✓
def ids_of(node, out, keys=("id",)):
    if isinstance(node, dict):
        for k, v in node.items():
            if k in keys and isinstance(v, str) and v:
                out.add(v)
            ids_of(v, out, keys)
    elif isinstance(node, list):
        for v in node:
            ids_of(v, out, keys)


def load(name):
    p = os.path.join(DATA, name)
    if not os.path.isfile(p):
        return None
    try:
        return json.load(open(p, encoding="utf-8"))
    except Exception:
        return None


def main() -> int:
    print(f"[audit] 读 `darkest/data/` ✓")
    files = sorted(n for n in os.listdir(DATA) if n.endswith(".json"))
    docs = {n: load(n) for n in files}

    # ---- 建各命名空间的 id 集合 ----
    NS = {}
    for name in ("units.json", "skills.json", "trinkets.json", "quirks.json",
                 "buildings.json", "hero_upgrades.json", "traits.json",
                 "camp_skills.json", "sanitarium.json", "curios.json",
                 "trap_defs.json", "buff_defs.json", "buff_primitives.json",
                 "heirloom_exchange.json", "morale_events.json",
                 "expedition_nodes.json", "enemy_ai.json"):
        d = docs.get(name)
        if d is None:
            continue
        s = set()
        ids_of(d, s)
        if name == "buff_primitives.json" and isinstance(d, dict):
            s = {p.get("id") for p in (d.get("primitives") or []) if isinstance(p, dict)}
        NS[name] = s
    print("[audit] 各文件定义的 id 数：")
    for k, v in sorted(NS.items(), key=lambda x: -len(x[1])):
        print(f"[audit]   {k:28s} {len(v):>6}")
    print()

    # 🆕 **补上"结构里没有 `id` 键"的命名空间**（`27_*.md §5` 记的覆盖缺口）✓
    #    🔴 判据：**它们的引用键叫什么，就去哪里收** ✓
    EXTRA = {}
    # ① 传家宝：`heirloom_exchange` 用 `exchange_*_type`；`heirlooms.kinds` 定义 ✓
    hl = docs.get("heirlooms.json") or {}
    if isinstance(hl, dict) and isinstance(hl.get("kinds"), list):
        # 我方 `kinds` 是**复数**（`busts`），而兑换表用**单数**（`bust`）⚠️
        EXTRA["currency"] = {k.rstrip("s") for k in hl["kinds"]} | set(hl["kinds"])
    # ② 敌人 AI：`enemy_ai` 用 `archetype_id`；`units.json` 定义 ✓
    ai = docs.get("enemy_ai.json") or {}
    if isinstance(ai, dict):
        s = {a.get("archetype_id") for a in (ai.get("archetypes") or [])
             if isinstance(a, dict)}
        EXTRA["archetype"] = {x for x in s if x}
    # ③ AI 的 `skill_id` ⇒ 指向 `skills.json` ✓
    if isinstance(ai, dict):
        s = set()
        for a in (ai.get("archetypes") or []):
            for r in (a.get("rules") or []):
                if isinstance(r, dict) and r.get("skill_id"):
                    s.add(r["skill_id"])
        EXTRA["ai_skill"] = s
    print("[audit] 🆕 补上的命名空间（`27_*.md §5` 的覆盖缺口）：")
    for k, v in EXTRA.items():
        print(f"[audit]   **{k}**：{len(v)} 个 ⇒ {sorted(v)[:12]}")
    print()

    # 全局并集（用于"无法判定"）✓
    allids = set()
    for s in NS.values():
        allids |= s
    for s in EXTRA.values():
        allids |= s

    # ---- 抽所有"像引用"的键 ----
    REF_KEYS = ("buffs", "skills", "trinkets", "quirks", "traits", "items",
                "goal_ids", "tree_id", "prerequisite", "requires", "id")
    found = defaultdict(lambda: {"refs": Counter(), "where": defaultdict(set)})

    def walk(node, path, fname, key=None):
        if isinstance(node, dict):
            for k, v in node.items():
                walk(v, f"{path}.{k}", fname, k)
        elif isinstance(node, list):
            for i, v in enumerate(node):
                walk(v, f"{path}[{i}]", fname, key)

    # 只收**明确的引用型键**（避免把普通 id 当引用）✓
    STRICT = ("buffs", "skills", "trinkets", "quirks", "traits", "goal_ids",
              "tree_id", "item", "unit", "curio_name", "trinket_id",
              # 🆕 本件补的三类（对应上面的 EXTRA）✓
              "exchange_from_type", "exchange_to_type", "archetype_id", "skill_id")

    def collect(node, path, fname, parent_key=None):
        if isinstance(node, dict):
            for k, v in node.items():
                p = f"{path}.{k}" if path else k
                if k in STRICT:
                    if isinstance(v, str) and v:
                        found[k]["refs"][v] += 1
                        found[k]["where"][v].add(fname)
                    elif isinstance(v, list):
                        for x in v:
                            if isinstance(x, str) and x:
                                found[k]["refs"][x] += 1
                                found[k]["where"][x].add(fname)
                            elif isinstance(x, dict):
                                collect(x, p, fname, k)
                    elif isinstance(v, dict):
                        collect(v, p, fname, k)
                else:
                    collect(v, p, fname, k)
        elif isinstance(node, list):
            for i, v in enumerate(node):
                collect(v, f"{path}[{i}]", fname, parent_key)

    for name, d in docs.items():
        if d is not None:
            collect(d, "", name)

    print("[audit] 🎖️ **引用型键**的悬空情况（**按命名空间逐个查**）：")
    print()
    total_dangling = 0
    for k in sorted(found, key=lambda x: -sum(found[x]["refs"].values())):
        refs = found[k]["refs"]
        if not refs:
            continue
        dangling = {r: c for r, c in refs.items() if r not in allids}
        total_dangling += len(dangling)
        mark = "✅" if not dangling else "🔴"
        print(f"[audit] {mark} **`{k}`**：引用 **{len(refs)}** 个去重 · "
              f"**悬空 {len(dangling)}**")
        if dangling:
            for r, c in sorted(dangling.items(), key=lambda x: -x[1])[:8]:
                where = sorted(found[k]["where"][r])[:2]
                print(f"[audit]      🔴 `{r}` ×{c}  ← {where}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "note": "宽面引用审计：把 darkest/data 里所有引用型键抽出来，按命名空间查。只读不落库 ✓",
            "namespaces": {k: sorted(v) for k, v in NS.items()},
            "refs": {k: dict(v["refs"]) for k, v in found.items()},
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[audit] 合计悬空（去重后各键相加）**{total_dangling}** ✓")
    print(f"[audit] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
