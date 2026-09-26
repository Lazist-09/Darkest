#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_quests_loot_narration.py —— A8：任务 / 战利品 / 旁白 / 队伍名 ✓

四个源（**我方全部无对应** ⇒ 这是"整表缺失"）：
   ① `JsonQuests.json`（148,877 B · 🔴 **有尾随逗号**）
      顶层 7 键，**没有"任务数组"** ⚠️（与 `PLAN_adoption §10.2 ①` 一致）：
      `stress_damage`(int) · `goals`(45) · `town_progression_goal_ids`(4) ·
      `types`(6) · **`plot_quests`(30)** · `generation`(5 子键) · `restriction`(1 子键)
      ⇒ **只有那 30 条 plot_quests 是真任务** ✓
   ② `JsonLoot.json`（31,728 B）—— `darkness_bonuses`(2) + **`loot_tables`(54)**
      🔴 而 `§10.2 ②` 已实测：**54 张表其实只有 33 个不同 id**（`H` 一个 id 就 13 个变体）⚠️
   ③ `Narration.json`（275,271 B）—— `filters`(1) + **`entries`(36)** ✓
   ④ `PartyNames.json`（20,748 B）—— **`party_names`(186)**
      🔴 而 `§10.2 ⑤` 已实测：**键只有 `{id, required_hero_class}`，文件里【没有名字字符串】** ⚠️

🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓
用法：python tools/dsh/extract_ref_quests_loot_narration.py
"""

from __future__ import annotations

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
DATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
OUT = os.path.join(REPO, "reports", "unity_ref", "quests_loot_narration_from_ref.json")
TRIM = re.compile(r",\s*([\]}])")


def load(name):
    raw = open(os.path.join(DATA, name), encoding="utf-8-sig", errors="replace").read()
    try:
        return json.loads(raw), len(raw), False
    except json.JSONDecodeError:
        return json.loads(TRIM.sub(r"\1", raw)), len(raw), True


def main() -> int:
    result = {}

    # ---- ① JsonQuests.json ----
    q, qb, q_tr = load("JsonQuests.json")
    print(f"[a8] `JsonQuests.json`（{qb} B · 尾随逗号 **{q_tr}**）⇒ 顶层 {len(q)} 键")
    for k, v in q.items():
        n = len(v) if hasattr(v, "__len__") else v
        print(f"[a8]   {k:28s} {type(v).__name__:6s} {n}")
    goals = q.get("goals") or []
    print(f"[a8]   `goals` {len(goals)} 条 · goal 的字段：{sorted({x for g in goals if isinstance(g, dict) for x in g})}")
    print(f"[a8]   `types` {len(q.get('types') or [])} 条："
          f"{[t.get('id') for t in (q.get('types') or []) if isinstance(t, dict)]}")
    pq = q.get("plot_quests") or []
    print(f"[a8]   `plot_quests` **{len(pq)}** 条（**只有这些是真任务**）✓")
    print(f"[a8]   plot_quest 的字段：{sorted({x for p in pq if isinstance(p, dict) for x in p})}")
    result["quests"] = q

    # ---- ② JsonLoot.json ----
    lt, lb, lt_tr = load("JsonLoot.json")
    tables = lt.get("loot_tables") or []
    ids = [t.get("id") for t in tables if isinstance(t, dict)]
    print()
    print(f"[a8] `JsonLoot.json`（{lb} B）⇒ `loot_tables` **{len(tables)}** 张 · "
          f"不同 id **{len(set(ids))}** ✓")
    print(f"[a8]   🔴 **表数 ≠ id 数**！（`§10.2 ②` 已记）⇒ "
          f"重复最多：{Counter(ids).most_common(3)} ✓")
    print(f"[a8]   `darkness_bonuses` {len(lt.get('darkness_bonuses') or [])} 条 ✓")
    result["loot"] = lt

    # ---- ③ Narration.json ----
    na, nb, na_tr = load("Narration.json")
    entries = na.get("entries") or []
    print()
    print(f"[a8] `Narration.json`（{nb} B）⇒ `entries` **{len(entries)}** · "
          f"`filters` **{len(na.get('filters') or [])}** ✓")
    ekeys = sorted({x for e in entries if isinstance(e, dict) for x in e})
    print(f"[a8]   entry 的字段：{ekeys}")
    print(f"[a8]   事件 id 样例：{[e.get('id') for e in entries[:8]]}")
    result["narration"] = na

    # ---- ④ PartyNames.json ----
    pn, pb, pn_tr = load("PartyNames.json")
    names = pn.get("party_names") or []
    print()
    print(f"[a8] `PartyNames.json`（{pb} B）⇒ `party_names` **{len(names)}** ✓")
    nkeys = sorted({x for n in names if isinstance(n, dict) for x in n})
    print(f"[a8]   🔴 键只有 **{nkeys}** ⇒ **文件里没有名字字符串**（`§10.2 ⑤` 一致）✓")
    print(f"[a8]   样例：{names[:4]}")
    result["party_names"] = pn

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": DATA,
            "note": "A8 抽取：JsonQuests / JsonLoot / Narration / PartyNames 四份"
                    "（**我方全部无对应** ⇒ 整表缺失）。本件【只抽不落库】✓",
            **result,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[a8] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
