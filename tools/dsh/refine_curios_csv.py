#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""refine_curios_csv.py —— 精修 `Curios.csv`：**拆出纯 TAGS、并按列位正确取元信息** ✓

🔴 我上一版（`extract_ref_curios.py`）**把元信息段收粗了** —— 本件修正，理由如下：

实测一块（`Unlocked Strongbox`，15 行）的真实布局：
```
row  3: 1:1 | 2:<显示名> | 4:<kind>          ← 标题行（**序号/显示名/类型**）
row  4: 2:ID STRING | 4:RESULT TYPES | …     ← **块表头（18 列）**
row  5: 2:<真 id>  | 4:Nothing | 7:N/A …     ← ★ **真 id 在这一行**（第 1 个结果行）
row  6: 2:REGION FOUND | 4:Loot | 5:3 | …    ← 元信息：本区掉落
row  7: 2:ALL          | 4:Quirk             ← 元信息：通用结果类型
row  8: 2:FULL CURIO?  | 4:Effect | 5:1 …    ← 元信息：**拿齐整件**的结果
row  9: 2:Yes          | 4:Purge             ← 🔴 **`row 7/8` 的【续行】**（列 2 空 ⇒ 值在列 4）
row 10: 2:TAGS         | 4:Scouting          ← 标签段开始
row 11: 2:Treasure     | 3:All | 4:Teleport  ← 🔴 **`TAGS` 的【续行】**（`All` 在**列 3**）
row 12: 4:Disease                            ← 🔴 第三行
row 13: 2:Item Interactions | 4:ITEM | …     ← 段内小表（表头）
row 14-17: （空 / 数据）
```

🔴 **我上一版的三个错**（都记下来）：
   ① **`TAGS` 只取了「同一行的列 3+」** ⇒ **漏掉续行** ⇒ 标签不完整 ⚠️
   ② **`REGION FOUND` / `ALL` / `FULL CURIO?` 也没收续行**（`Yes`/`Purge` 就是续行）⚠️
   ③ **表头列的对应错位**：元信息行的值**不在表头那 18 列的语义上**
   🎖️ ⇒ **即：CSV 的列在元信息行里【换了语义】**（同一份 CSV 里 `列 4` 有两种含义）⚠️
      📌 **纪律 AU 的又一实例**：**同一列号，在不同行里口径不同** ⇒ 必须**按段解析** ✓

🔴 **本版又量了一次 —— 用【分布】而不是【看一条】定段长**（纪律 AU/BH）：
   `TAGS` 行 到 `Item Interactions` 行 之间的行数 ⇒ 实测 **60/60 条都是 3 行** ✓
   （只看第一条会以为只有 1 行 —— 那条恰好续行在第 11/12 行，被 `Yes`/`Purge` 遮住了）
   📌 **段内取值列实测**：`(列2,列3,列4)` 的组合只有三种
      `(T,F,T)` 91 次 · `(T,T,T)` 62 次 · `(F,F,T)` 27 次 ⇒ **标签在这三列里** ✓
   ⇒ ✅ **判据：一个段的值 = 段首行到下一段首行之间的【列 2/3/4】全部非空值** ✓

用法：python tools/dsh/refine_curios_csv.py
"""

from __future__ import annotations

import csv
import io
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Curios\Curios.csv"
OUT = os.path.join(REPO, "reports", "unity_ref", "curios_csv_refined.json")

META_KEYS = ("REGION FOUND", "ALL", "FULL CURIO?", "TAGS")
# 段内小表（遇到它就结束元信息段）✓
SUB_TABLE = ("Item Interactions",)
# 一行"结果"的必要列（按块表头的列位）✓
RESULT_TYPE_COL = 4
WEIGHT_COL = 5


def main() -> int:
    text = io.open(SRC, encoding="utf-8-sig", errors="replace", newline="").read()
    rows = list(csv.reader(io.StringIO(text)))
    hdr = [i for i, r in enumerate(rows)
           if len(r) > 4 and r[2].strip() == "ID STRING" and r[4].strip() == "RESULT TYPES"]
    print(f"[refine] 块表头 **{len(hdr)}** 个 · 间隔 "
          f"{sorted({hdr[i + 1] - hdr[i] for i in range(len(hdr) - 1)})} ✓")

    out = []
    for bi, h in enumerate(hdr):
        end = hdr[bi + 1] - 1 if bi + 1 < len(hdr) else len(rows)
        title = rows[h - 1]
        item = {
            "index": title[1].strip() if len(title) > 1 else "",
            "display": title[2].strip() if len(title) > 2 else "",
            "kind": title[4].strip() if len(title) > 4 else "",
            "header_row": h + 1,
            "id": "",
            "results": [],
            "meta": {k: {"rows_raw": []} for k in META_KEYS},
            "item_interactions_header": None,
        }

        # ---- 逐行扫块体：**先切段，再取值**（段长由"下一段首行"决定）----
        # 🔴 段首行 = 列 2 命中 META_KEYS 或 SUB_TABLE 的行 ✓
        seg_starts = []
        for ri in range(h + 1, end):
            c2 = rows[ri][2].strip() if len(rows[ri]) > 2 else ""
            if c2 in META_KEYS or c2 in SUB_TABLE:
                seg_starts.append((ri, c2))
        seg_starts.append((end, None))  # 哨兵

        # 真 id：块表头后**第一个**列 2 非空且不在任何段里的行 ✓
        first_seg = seg_starts[0][0]
        for ri in range(h + 1, first_seg):
            c2 = rows[ri][2].strip() if len(rows[ri]) > 2 else ""
            if c2:
                item["id"] = c2
                break

        # 取每个段的值（列 2/3/4 的全部非空值，**去掉段首那个键名**）✓
        for si in range(len(seg_starts) - 1):
            start, key = seg_starts[si]
            stop = seg_starts[si + 1][0]
            vals = []
            for ri in range(start, stop):
                r = rows[ri]
                for ci in (2, 3, 4, 5):
                    if ci < len(r):
                        v = r[ci].strip()
                        if v and not (ri == start and ci == 2):  # 跳过段首键名
                            vals.append(v)
            if key in META_KEYS:
                item["meta"][key]["values"] = vals
                item["meta"][key]["span"] = stop - start
            else:
                item["item_interactions_header"] = start + 1

        # ---- 结果行：块表头之后、第一个段之前 + `FULL CURIO?` 段之后的结果 ----
        for ri in range(h + 1, first_seg):
            r = rows[ri]
            c4 = r[4].strip() if len(r) > 4 else ""
            if c4:
                item["results"].append({
                    "row": ri + 1,
                    "result_type": c4,
                    "weight": r[WEIGHT_COL].strip() if len(r) > WEIGHT_COL else "",
                    "chance": r[6].strip() if len(r) > 6 else "",
                    "r1": r[7].strip() if len(r) > 7 else "",
                    "r2": r[10].strip() if len(r) > 10 else "",
                    "r3": r[13].strip() if len(r) > 13 else "",
                    "string": r[16].strip() if len(r) > 16 else "",
                })
        out.append(item)

    # ---- 汇总打印 ----
    print(f"[refine] 解析出 **{len(out)}** 条 ✓")
    n_id = sum(1 for x in out if x["id"])
    print(f"[refine]   有真 id 的 **{n_id}/{len(out)}** ✓")
    print(f"[refine]   有 TAGS 的 **{sum(1 for x in out if x['meta']['TAGS']['values'])}** ✓")
    print()
    print("=== 样例 3 条（修正后）===")
    for x in out[:3]:
        print(f"  #{x['index']} {x['display']} kind={x['kind']} id={x['id']}")
        print(f"     TAGS = {x['meta']['TAGS']['values']}")
        print(f"     REGION FOUND = {x['meta']['REGION FOUND']['values']}")
        print(f"     FULL CURIO? = {x['meta']['FULL CURIO?']['values']}")
        print(f"     结果行 {len(x['results'])} 条")
    print()

    # TAG 词表（去掉明显是数值/百分比的）
    import re
    tagset = {}
    for x in out:
        for v in x["meta"]["TAGS"]["values"]:
            tagset[v] = tagset.get(v, 0) + 1
    clean = {k: v for k, v in tagset.items()
             if not re.fullmatch(r"[\d.]+%?|[\d.]+%|<- # Draws|\d+ - \w+", k)}
    print(f"=== TAGS 词表（去噪后 {len(clean)} 个）===")
    for k, v in sorted(clean.items(), key=lambda x: -x[1]):
        print(f"   {k:28s} {v}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": SRC,
            "note": "精修版：修正上一版【元信息未收续行】+【列位语义按段变】两个问题。"
                    "TAGS 现在是纯标签。本件【只抽不落库】✓",
            "count": len(out),
            "tags_vocabulary": sorted(clean),
            "items": out,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[refine] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
