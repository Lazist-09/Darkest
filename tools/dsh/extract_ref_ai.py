#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_ai.py —— A5：抽取参考项目的**怪物 AI**（`JsonAI.json` · 160 brains）✓

🔴 **形态**：`JsonAI.json`（**306,243 B** · 🔴 **有尾随逗号** —— 与 `§6` 一致）
   顶层 `monster_brains` **160 条** · 每条 3 个字段：
      `id` · `skill_cooldowns[]` · **`skill_selection_desires[]`** · **`target_selection_desires[]`**
   🎖️ **两类 desire 的 `data` 槽不同**（实测）：
      · `skill_selection_desires` 的 `data` = `{base_chance, …}`（如 `hp_ratio_treshold`）
      · `target_selection_desires` 的 `data` = `{base_chance, specific_combat_skill_id,
        is_exclusive_desire, is_enemy_target_desire, is_friendly_target_desire, …}`
   🔴 **`§6` 已记的坑**：**技能欲望 `base_chance × 100`，而目标欲望 `× 1`** ⇒
      ⚠️ **两侧缩放不一致**（各自独立归一 ⇒ 行为无错）⇒ ✅ **我方实现时应统一** ✓
      本件**实测复核**这条（见输出）✓

🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓
用法：python tools/dsh/extract_ref_ai.py
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
SRC = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonAI.json"
OUT = os.path.join(REPO, "reports", "unity_ref", "ai_from_ref.json")
MONSTERS = os.path.join(REPO, "reports", "unity_ref", "monsters_from_ref.json")
TRIM = re.compile(r",\s*([\]}])")


def main() -> int:
    raw = open(SRC, encoding="utf-8-sig", errors="replace").read()
    try:
        d = json.loads(raw)
        trimmed = False
    except json.JSONDecodeError:
        d = json.loads(TRIM.sub(r"\1", raw))
        trimmed = True
    brains = d["monster_brains"]
    print(f"[a5] `JsonAI.json`（{len(raw)} B · 尾随逗号 **{trimmed}**）⇒ "
          f"`monster_brains` **{len(brains)}** ✓")

    # ---- 完整性自检 ----
    ids = [b.get("id") for b in brains]
    print(f"[a5] id 有值 **{sum(1 for i in ids if i)}/{len(brains)}** · "
          f"去重 **{len(set(ids))}** ✓")
    cd = sum(1 for b in brains if b.get("skill_cooldowns"))
    sd = sum(1 for b in brains if b.get("skill_selection_desires"))
    td = sum(1 for b in brains if b.get("target_selection_desires"))
    print(f"[a5] 有 `skill_cooldowns` **{cd}** · 有技能欲望 **{sd}** · 有目标欲望 **{td}** ✓")

    # ---- desire 类型与 data 槽 ----
    sk_types, tg_types = Counter(), Counter()
    sk_keys, tg_keys = Counter(), Counter()
    for b in brains:
        for x in b.get("skill_selection_desires") or []:
            sk_types[x.get("type")] += 1
            sk_keys.update((x.get("data") or {}).keys())
        for x in b.get("target_selection_desires") or []:
            tg_types[x.get("type")] += 1
            tg_keys.update((x.get("data") or {}).keys())
    print()
    print(f"[a5] **技能欲望** type（{len(sk_types)} 种）：{dict(sk_types)}")
    print(f"[a5]   它的 `data` 键（{len(sk_keys)} 种）：{dict(sk_keys)}")
    print(f"[a5] **目标欲望** type（{len(tg_types)} 种）：{dict(tg_types)}")
    print(f"[a5]   它的 `data` 键（{len(tg_keys)} 种）：{dict(tg_keys)}")

    # ---- 🎖️ 复核 `§6` 的"×100 vs ×1" ----
    print()
    print("[a5] 🎖️ 复核 `§6` 的「技能欲望 `×100` · 目标欲望 `×1`」：")
    sk_vals = [x["data"].get("base_chance") for b in brains
               for x in (b.get("skill_selection_desires") or [])
               if isinstance(x.get("data"), dict) and "base_chance" in x["data"]]
    tg_vals = [x["data"].get("base_chance") for b in brains
               for x in (b.get("target_selection_desires") or [])
               if isinstance(x.get("data"), dict) and "base_chance" in x["data"]]
    print(f"[a5]   技能欲望 base_chance：{len(sk_vals)} 个 · 最小 {min(sk_vals):g} · "
          f"最大 {max(sk_vals):g} · 值分布 {sorted(set(sk_vals))[:12]}")
    print(f"[a5]   目标欲望 base_chance：{len(tg_vals)} 个 · 最小 {min(tg_vals):g} · "
          f"最大 {max(tg_vals):g} · 值分布 {sorted(set(tg_vals))[:12]}")
    print(f"[a5]   ⇒ {'✅ 确认两者量纲不同（与 §6 一致）' if max(sk_vals) > 10 * max(tg_vals) else '⚠️ 与 §6 不一致，需再核'}")

    # ---- 🎖️ 与 A4 的 join 复核 ----
    if os.path.isfile(MONSTERS):
        ms = json.load(open(MONSTERS, encoding="utf-8"))
        used = {r["monster_brain"] for r in ms["monsters"] if r["monster_brain"]}
        known = set(ids)
        orphan = sorted(known - used)
        missing = sorted(used - known)
        print()
        print(f"[a5] 🎖️ 与 A4 的 join 复核：怪物引用 **{len(used)}** 个 brain（去重）")
        print(f"[a5]   🔴 怪物引用了但**本表没有**的：**{len(missing)}** {missing[:5]}")
        print(f"[a5]   🎖️ **本表有但没有怪物引用（孤儿）**：**{len(orphan)}** {orphan}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": SRC,
            "note": "A5 抽取：JsonAI.json 的 160 brains（含两类 desire 的完整正文）。"
                    "本件【只抽不落库】✓",
            "count": len(brains),
            "skill_desire_types": sorted(sk_types),
            "target_desire_types": sorted(tg_types),
            "brains": brains,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[a5] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
