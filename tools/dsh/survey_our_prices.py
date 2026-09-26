#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""survey_our_prices.py —— 汇总我方的**全部金币数值**（买/卖两侧），供"改成参考量级会崩吗"用。

🔴 **为什么需要它**（承接 `23_economy_scale.md §7①`）：
   参考的金币（一次任务 3000~22500）与它的**物价**（火把 75 · 铲子 250）是**绑在一起**的 ✓
   ⇒ ⚠️ **要判断"把金币改成参考量级会不会崩"，必须先知道我方物价是多少** ✓
   📌 而它们**散在** `economy` / `buildings` / `heirlooms` / `trinkets` 等多处 ⇒ 本件汇总 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/survey_our_prices.py
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
OUT = os.path.join(REPO, "reports", "unity_ref", "our_prices.json")

# 🔴 只看**金币**（`gold`）；传家宝另算 ✓
GOLD_KEYS = ("gold", "gold_cost", "cost_gold", "price", "purchase_gold_value",
             "sell_gold_value", "battle_reward", "event_reward", "recruit_cost")


def walk(node, path, found):
    """收集所有 (路径, 键, 值)，值是数字时按"疑似金币"归类 ✓"""
    if isinstance(node, dict):
        for k, v in node.items():
            walk(v, f"{path}.{k}" if path else k, found)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            walk(v, f"{path}[{i}]", found)


def main() -> int:
    files = sorted(n for n in os.listdir(DATA) if n.endswith(".json"))
    print(f"[price] 我方 `darkest/data/` **{len(files)}** 个 json ✓")

    # ---- ① 白名单键：显式叫 gold/price 的 ----
    gold_hits = defaultdict(list)
    allnums = Counter()
    for name in files:
        try:
            d = json.load(open(os.path.join(DATA, name), encoding="utf-8"))
        except Exception:
            continue
        stack = [("", d)]
        while stack:
            path, node = stack.pop()
            if isinstance(node, dict):
                for k, v in node.items():
                    p = f"{path}.{k}" if path else k
                    if any(g in k.lower() for g in ("gold", "price", "cost", "reward")):
                        if isinstance(v, (int, float)):
                            gold_hits[name].append((p, k, v))
                    stack.append((p, v))
            elif isinstance(node, list):
                for i, v in enumerate(node):
                    stack.append((f"{path}[{i}]", v))

    print()
    print("[price] 🎖️ 我方显式含 `gold`/`price`/`cost`/`reward` 的数值键：")
    tot = 0
    for name in sorted(gold_hits):
        hits = gold_hits[name]
        tot += len(hits)
        vals = sorted({v for _, _, v in hits})
        print(f"[price]   **{name}**（{len(hits)} 处）⇒ 取值 {vals[:12]}"
              f"{'…' if len(vals) > 12 else ''}")
    print(f"[price]   合计 **{tot}** 处 ✓")

    # ---- ② `currency_cost` 里的 gold 成员（building/hero 升级）----
    print()
    print("[price] 🎖️ 升级表的 `currency_cost` 里 `type == \"gold\"` 的金额：")
    for name in ("buildings.json", "hero_upgrades.json"):
        p = os.path.join(DATA, name)
        if not os.path.isfile(p):
            continue
        d = json.load(open(p, encoding="utf-8"))
        amounts = []
        stack = [d]
        while stack:
            node = stack.pop()
            if isinstance(node, dict):
                if node.get("type") == "gold" and "amount" in node:
                    amounts.append(node["amount"])
                stack.extend(node.values())
            elif isinstance(node, list):
                stack.extend(node)
        c = Counter(amounts)
        print(f"[price]   **{name}**：{len(amounts)} 处 · {dict(c)}")
        print(f"[price]     ⇒ 🔴 **全部为 0** ⇒ 升级只花传家宝 ✓" if set(amounts) <= {0}
              else f"[price]     ⇒ 非零 {sorted(x for x in set(amounts) if x)}")

    # ---- ③ 传家宝（非金币）的价格对照 ----
    print()
    print("[price] 🎖️ 传家宝换算（A11 已实测：相对价值 portrait 6 : bust 3 : deed 3 : crest 2）")
    print("[price]   而参考一次任务给的传家宝（`heirloom_amount_table`）：")
    q = json.load(open(os.path.join(REPO, "reports", "unity_ref",
                                    "quests_loot_narration_from_ref.json"), encoding="utf-8"))
    amt = {x["type"]: x["amounts"] for x in q["quests"]["generation"]["rewards"]["heirloom_amount_table"]}
    for k, v in amt.items():
        print(f"[price]     {k:10s} {v}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "note": "我方全部金币/价格数值的汇总（供判断「改成参考量级会崩吗」）。只读不落库 ✓",
            "files_scanned": len(files),
            "explicit_gold_keys": {k: v for k, v in gold_hits.items()},
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[price] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
