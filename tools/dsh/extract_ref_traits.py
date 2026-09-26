#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_traits.py —— A6：抽取参考项目的**折磨 / 美德 + 两张行为（act-out）表** ✓

源：`…/Assets/Resources/Data/JsonTraits.json` —— **12 条**（7 折磨 + 5 美德）✓
🔴 该文件**不是严格合法 JSON**（尾随逗号）⇒ 先清洗（与 `JsonAI.json` 同族的坑）✓

产出：`reports/unity_ref/traits_from_ref.json` —— 逐条带 `file` + `line` 出处 ✓
      🔴 **只抽不落库**（`darkest/**` 一个字节不动）⇒ 零行为 ✓

实测形状（本工具要处理的三块）：
   ① **12 条** `overstress_type` / `curio_tag` / `curio_tag_chance` / `keep_loot` / `buff_ids[]`
   ② **两张 act-out 表**：`combat_start_turn_act_outs` **14 项/条** · `reaction_act_outs` **15 项/条**
      ⇒ 12 × 14 = **168** · 12 × 15 = **180** ✓（**id 全集各 14 / 15 个**，12 条**共用同一套 id**）✓
   ③ 每项的形状：`{id, data{number_value, string_value}, chance}` ⇒ 🎖️
      **`data` 恒为 `{float, string}` 两槽**（与 buff 原语层同族：**阈值与枚举同槽，不需要 union**）✓

用法：python tools/dsh/extract_ref_traits.py
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
SRC = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonTraits.json"
OUT = os.path.join(REPO, "reports", "unity_ref", "traits_from_ref.json")


def locate_lines(text: str, needle: str):
    """找出某个 id 出现在第几行（供出处用）✓"""
    for i, line in enumerate(text.splitlines(), 1):
        if needle in line:
            return i
    return None


def main() -> int:
    raw = io.open(SRC, encoding="utf-8-sig", errors="replace").read()
    clean = re.sub(r",\s*([\]}])", r"\1", raw)
    try:
        data = json.loads(clean)
    except json.JSONDecodeError as exc:
        print(f"🔴 清洗后仍不合法：{exc}")
        return 1

    traits = data["traits"]
    print(f"[a6] 源 {len(raw)} bytes ⇒ 解析出 **{len(traits)} 条** ✓")

    out = []
    for t in traits:
        start = locate_lines(raw, f'"id": "{t["id"]}"')
        turn = t.get("combat_start_turn_act_outs") or []
        react = t.get("reaction_act_outs") or []
        out.append({
            "id": t["id"],
            "overstress_type": t.get("overstress_type"),
            "curio_tag": t.get("curio_tag"),
            "curio_tag_chance": t.get("curio_tag_chance"),
            "keep_loot": t.get("keep_loot"),
            "buff_ids": t.get("buff_ids") or [],
            "combat_start_turn_act_outs": turn,
            "reaction_act_outs": react,
            "file": "JsonTraits.json",
            "line": start,
        })

    # ---- 完整性自检（不许有字段大面积为空 —— 与 A4 同一条纪律）----
    print("[a6] 完整性自检：")
    for key in ("overstress_type", "curio_tag", "curio_tag_chance", "keep_loot"):
        n = sum(1 for r in out if r[key] is not None)
        print(f"[a6]   {key:20s} 有值 {n}/{len(out)}")
    print(f"[a6]   buff_ids 总数 {sum(len(r['buff_ids']) for r in out)}")
    print(f"[a6]   回合开始表 {sum(len(r['combat_start_turn_act_outs']) for r in out)} 项"
          f" · 反应表 {sum(len(r['reaction_act_outs']) for r in out)} 项")

    kinds = {}
    for r in out:
        kinds[r["overstress_type"]] = kinds.get(r["overstress_type"], 0) + 1
    print(f"[a6]   类型分布：{kinds}")

    ids_turn = {e["id"] for r in out for e in r["combat_start_turn_act_outs"]}
    ids_react = {e["id"] for r in out for e in r["reaction_act_outs"]}
    print(f"[a6]   act-out id 全集：回合开始 **{len(ids_turn)}** · 反应 **{len(ids_react)}** ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": SRC,
            "note": "A6 抽取：参考项目 JsonTraits.json 的 12 条（7 折磨 + 5 美德）"
                    "+ 两张 act-out 表。🔴 该文件有尾随逗号 ⇒ 已清洗。本件【只抽不落库】✓",
            "count": len(out),
            "act_out_ids": {
                "combat_start_turn": sorted(ids_turn),
                "reaction": sorted(ids_react),
            },
            "traits": out,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[a6] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
