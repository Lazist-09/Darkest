#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_effects.py —— A6 的**直接前置**：抽参考项目的 `Effects.txt`（952 条）✓

🔴 **为什么它是前置**（承接 `43_actout_data_semantics.md §4`）：
   act-out 的 `string_value`（6 种）**全部指向 Effect 表**（`Data.Effects[...]`）✓
   ⇒ ⚠️ **我方【没有这张表】** ⇒ 只搬 act-out 会得到**空壳** ✓
   ⇒ ✅ **所以 A6 的落库顺序：先 Effect 表 → 再 act-out** ✓
🎖️ 而 Effect 表**自己又引用 buff 原语**（`combat_stat_buff` 353 · `buff_ids` 90）
   ⇒ 📌 **完整依赖链：act-out → Effect → buff 原语（A1 已落）** ✓

形状（实测）：
   `effect: .name "X" .target "…" .chance 100% .<效果字段>… .on_hit true .on_miss false .queue …`
   **952 条** · **68 个字段** · 前 4 行是注释（`//`）✓
   高频字段：`name/target` 952 · `on_hit` 950 · `on_miss` 947 · `chance` 941 ·
      `curio_result_type` 739 · `queue` 447 · `combat_stat_buff` 353 · `duration` 207 ·
      `damage_low_multiply`/`damage_high_multiply` 139 · `buff_ids` 90 ·
      `summon_*` 47 组 · `stress` 51 · `healstress` 52 · `heal` 47 · …
   ⇒ 🎖️ **即：Effect 是"效果的唯一表达层"** —— 伤害/压力/治疗/召唤/位移/上 buff 全在它里面 ✓

🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓
用法：python tools/dsh/extract_ref_effects.py
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
SRC = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Mechanics\Effects.txt"
OUT = os.path.join(REPO, "reports", "unity_ref", "effects_from_ref.json")
POOL = os.path.join(REPO, "darkest", "data", "buff_primitives.json")

# 🔴 `key value` 对；value 可以是 `"带空格串"` / `数字[%]` / `裸词` ✓
# 🔴🔴 **必须允许【大写字母】**（第 60 条判据）：
#    原版有 **4 个驼峰键**：`dotPoison` ×51 · `dotBleed` ×44 · `keyStatus` ×27 ·
#    `monsterType` ×20 ⇒ **共 142 处** ✓
#    我第一版写的是 `[a-z_]+`（**只小写**）⇒ 🔴 **这 4 个字段被静默丢掉** ⚠️
#    ⇒ 📌 症状：**产物里 `dotPoison`/`dotBleed` 出现 0 次**（而它们是 DoT 的【核心数值】）✓
#    🎖️ 教训：**"没匹配上"不会报错 —— 它会【安静地少几个字段】** ✓
KV = re.compile(r'\.([A-Za-z_]+)\s+("(?:[^"]*)"|[^\s.]+)')


def parse_value(raw: str):
    if raw.startswith('"') and raw.endswith('"'):
        return raw[1:-1]
    if raw.endswith("%"):
        try:
            return float(raw[:-1]) / 100.0      # 🔴 百分比 ⇒ **分数**（与参考一致）
        except ValueError:
            return raw
    try:
        return int(raw)
    except ValueError:
        try:
            return float(raw)
        except ValueError:
            return raw


def main() -> int:
    text = io.open(SRC, encoding="utf-8-sig", errors="replace").read()
    lines = text.splitlines()
    print(f"[fx] `Effects.txt`：**{len(lines)}** 行 · {os.path.getsize(SRC)} B ✓")

    effects = []
    odd = []
    for lineno, line in enumerate(lines, 1):
        s = line.strip()
        if not s or s.startswith("//"):
            continue
        if not s.startswith("effect:"):
            # 🔴 **实测：文件里有 1 行【坏分隔线】** —— L504 是 `/----…`（**只有一个斜杠**）⚠️
            #    而正常的是 `//----…` ⇒ 📌 **参考数据的笔误** ✓
            #    ✅ 处置：**只放行"纯符号行"**，其余一律响亮失败（不静默跳过）✓
            if set(s) <= set("/-=* "):
                odd.append((lineno, s[:60]))
                continue
            raise SystemExit(f"🔴 L{lineno} 不是 effect 也不是注释（不静默跳过）：{s[:80]}")
        fields = {}
        for m in KV.finditer(s):
            fields[m.group(1)] = parse_value(m.group(2))
        if "name" not in fields:
            raise SystemExit(f"🔴 L{lineno} 没有 `.name`（不静默跳过）：{s[:80]}")
        effects.append({"name": fields.pop("name"), "fields": fields, "line": lineno})

    if odd:
        print(f"[fx] 🔴 **放行了 {len(odd)} 行【纯符号分隔线】**（参考数据的笔误）：")
        for lineno, s in odd:
            print(f"[fx]     L{lineno}: `{s}`")
            print(f"[fx]        ⇒ 正常应为 `//-----`（**两个斜杠**）⇒ 📌 **参考数据里少了一个** ✓")
    print(f"[fx] 解析出 **{len(effects)}** 条 ✓")
    names = [e["name"] for e in effects]
    print(f"[fx] name 去重 **{len(set(names))}** "
          f"{'（✅ 无重复）' if len(set(names)) == len(names) else '🔴 有重复'}")
    keys = Counter(k for e in effects for k in e["fields"])
    print(f"[fx] 字段全集 **{len(keys)}** 种 ✓")
    print(f"[fx] 高频 12：{[ (k, v) for k, v in keys.most_common(12) ]}")

    # ---- 🎖️ 与 A1 的 buff 池交叉校验（Effect → buff 的引用）----
    pool = json.load(open(POOL, encoding="utf-8"))
    pids = {p["id"] for p in pool["primitives"]}
    refd = []
    for e in effects:
        for k in ("combat_stat_buff", "buff_ids"):
            v = e["fields"].get(k)
            if isinstance(v, str) and v:
                refd.append(v)
            elif isinstance(v, list):
                refd.extend(v)
    uniq = sorted(set(refd))
    miss = [x for x in uniq if x not in pids]
    print(f"[fx] 🎖️ 引用 buff 名 **{len(uniq)}** 个（去重）⇒ "
          f"**未在 A1 池（{len(pids)} 条）里的 {len(miss)}**")
    if miss:
        print(f"[fx]    样例：{miss[:10]}")
    print(f"[fx]    📌 注意：`combat_stat_buff 353` 多数是**布尔开关**（`1`/`true`）而非名字 ⇒ 上面只算了"
          f" `buff_ids` 那种真名字 ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": SRC,
            "note": "A6 的直接前置：Effects.txt（952 条 effect · 68 字段）。"
                    "🔴 百分比一律转成【分数】（与参考一致）✓ 本件【只抽不落库】✓",
            "count": len(effects),
            "field_names": sorted(keys),
            "effects": effects,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[fx] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
