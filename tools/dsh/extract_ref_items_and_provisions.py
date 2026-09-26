#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_items_and_provisions.py —— A10：抽取参考项目的**物品表 + 补给清单** ✓

两个源：
   ① `…/Data/Inventory/Items.bytes` —— **DD1 文本**（不是 Unity 二进制！实测首 64 字节
      就是 `inventory_item:\t.type "provision"…`）⇒ 62 行 · 每行 5 个字段：
      `.type` · `.id` · `.base_stack_limit` · `.purchase_gold_value` · `.sell_gold_value` ✓
   ② `…/Data/Mechanics/Provision.json` —— 3 张清单
      （`raid_starting_length_…` / `raid_starting_hero_class_…` / `default_store_…`）✓

🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓
📌 形态判定是**先做的**（纪律：**先判形态，再选解析器**）——
   `Maps/*.bytes` 是二进制、`Heroes/Info/*.bytes` 与本源都是文本 ⇒ **`.bytes` 后缀不定形态** ⚠️

用法：python tools/dsh/extract_ref_items_and_provisions.py
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
DATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
ITEMS = os.path.join(DATA, "Inventory", "Items.bytes")
PROV = os.path.join(DATA, "Mechanics", "Provision.json")
OUT = os.path.join(REPO, "reports", "unity_ref", "items_and_provisions_from_ref.json")
TRIM = re.compile(r",\s*([\]}])")

ITEM_RE = re.compile(
    r'^inventory_item:\s*\.type\s*"(?P<type>[^"]*)"\s*\.id\s*"(?P<id>[^"]*)"\s*'
    r"\.base_stack_limit\s*(?P<stack>\d+)\s*"
    r"\.purchase_gold_value\s*(?P<buy>\d+)\s*"
    r"\.sell_gold_value\s*(?P<sell>\d+)"
)


def main() -> int:
    # ---- 形态判定（先做，且留证）----
    raw_head = open(ITEMS, "rb").read(64)
    is_text = raw_head[:8].isascii() and b"inventory_item:" in raw_head
    print(f"[a10] `Items.bytes` 形态判定：首 64 字节 = {raw_head[:40]!r}")
    print(f"[a10]   ⇒ **{'DD1 文本' if is_text else '🔴 非文本（需换解析器）'}** ✓")
    if not is_text:
        print("[a10] 🔴 不是文本 ⇒ 停（**不猜**）")
        return 1

    items = []
    text = io.open(ITEMS, encoding="utf-8-sig", errors="replace").read()
    for lineno, line in enumerate(text.splitlines(), 1):
        s = line.strip()
        if not s:
            continue
        m = ITEM_RE.match(s)
        if m is None:
            print(f"[a10] 🔴 第 {lineno} 行解析失败（不静默跳过）：{s[:80]}")
            return 1
        items.append({
            "type": m["type"],
            "id": m["id"],
            "base_stack_limit": int(m["stack"]),
            "purchase_gold_value": int(m["buy"]),
            "sell_gold_value": int(m["sell"]),
            "line": lineno,
        })

    from collections import Counter
    bytype = Counter(i["type"] for i in items)
    print(f"[a10] `Items.bytes` ⇒ **{len(items)} 条** · 类型分布 {dict(bytype)} ✓")
    print(f"[a10]   字段全集（每行 5 个）：type · id · base_stack_limit · "
          f"purchase_gold_value · sell_gold_value ✓")

    # ---- Provision.json ----
    praw = open(PROV, encoding="utf-8-sig", errors="replace").read()
    try:
        prov = json.loads(praw)
        trimmed = False
    except json.JSONDecodeError:
        prov = json.loads(TRIM.sub(r"\1", praw))
        trimmed = True
    print(f"[a10] `Provision.json` ⇒ 顶层 {len(prov)} 张清单 · 尾随逗号 {trimmed} ✓")
    for k, v in prov.items():
        n = len(v)
        inner = [len(x) if isinstance(x, list) else 1 for x in v]
        print(f"[a10]   {k}: 外层 {n} 组 · 各组 {inner}")

    # 完整性自检：三张清单里出现的 id 必须是 Items.bytes 里的
    known = {i["id"] for i in items}
    used = set()
    for v in prov.values():
        for grp in v:
            if isinstance(grp, list):
                for it in grp:
                    if isinstance(it, dict):
                        used.add(it.get("id"))
    unknown = sorted(x for x in used if x and x not in known)
    print(f"[a10] 清单里引用的 id 去重 **{len([u for u in used if u])}** 个 ⇒ "
          f"🔴 **不在 `Items.bytes` 里的 {len(unknown)}**：{unknown}")
    print(f"[a10]   📌 空 id `\"\"` 也是合法的一项（`provision` 占位）✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": DATA,
            "note": "A10 抽取：Inventory/Items.bytes（DD1 文本 · 62 行 · 每条带 line 出处）"
                    "+ Mechanics/Provision.json（3 张清单）。本件【只抽不落库】✓",
            "items_bytes_is_text": is_text,
            "item_count": len(items),
            "items": items,
            "provision": prov,
            "provision_unknown_ids": unknown,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[a10] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
