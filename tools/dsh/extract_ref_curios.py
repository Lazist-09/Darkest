#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_curios.py —— A12 下游：抽取参考项目的**奇物 / 障碍 / 陷阱** ✓

三个源（三种形态，正好各不同）：
   ① `Curios/Curios.csv` —— **CSV**（51,069 bytes · **18 列** · 前 3 行是表头/标题）
      🔴 我方现有抽取器**只处理 json/bytes/txt** ⇒ **这是第一个 csv 形态** ⚠️
   ② `Curios/Obstacles.json` —— JSON（`{props: [{name, fail_effects, health, torchlight, ancestor_talk}]}`）
   ③ `Curios/Traps.json` —— JSON（`{props: [{name, success_effects, fail_effects, health,
      difficulty_variations[]}]}`）🔴 **有尾随逗号**（`PLAN_adoption §10.2 ⑤` 已记）

🎖️ **为什么要抽它们**：A12 的 `props` 段引用了 **57 个名字**（奇物/宝藏/陷阱/障碍），
   而**我方无奇物表、参考的这三份未抽** ⇒ **那 57 个名字当时【未与任何表校验】** ⚠️
   ⇒ 本件把它们补上 ⇒ **A12 的 props 段才可校验** ✓

🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓
用法：python tools/dsh/extract_ref_curios.py
"""

from __future__ import annotations

import csv
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
CDIR = os.path.join(DATA, "Curios")
OUT = os.path.join(REPO, "reports", "unity_ref", "curios_from_ref.json")
DUNGEONS = os.path.join(REPO, "reports", "unity_ref", "dungeons_from_ref.json")
TRIM = re.compile(r",\s*([\]}])")


def load_json(path):
    raw = open(path, encoding="utf-8-sig", errors="replace").read()
    try:
        return json.loads(raw), False
    except json.JSONDecodeError:
        return json.loads(TRIM.sub(r"\1", raw)), True


def parse_curios_csv(path):
    """CSV：**每条奇物是一个 15 行的块** —— 块的第 1 行是标题、第 2 行是表头 ✓

    🔴 **我第一版数错了（573 vs 60）** ⚠️ —— 值得记下：
       第一版把**每个含 `ID STRING` 的行**都当数据行 ⇒ 数出 **573 个 id**
       ⇒ 🔴 而实际上 `ID STRING` 是**每条奇物块的表头** ⇒ 那 573 个里有
          `REGION FOUND`×60 · `FULL CURIO?`×60 · `TAGS`×60 · `Item Interactions`×60 …
          —— **全是字段名，不是奇物** ⚠️
       ⇒ ✅ 正确形状：**表头行 60 个 · 间隔恒 15 行** ⇒ **60 条奇物** ✓
          （与 `PLAN_adoption §9①` 的"60 条"**逐数相符** ✓）
    🎖️ **纪律 BH 又一次**：**换个口径（按块 vs 按行），数就从 60 变成 573** ⇒
       **我读错了口径** ✓
    """
    text = io.open(path, encoding="utf-8-sig", errors="replace", newline="").read()
    rows = list(csv.reader(io.StringIO(text)))
    hdr_rows = [i for i, r in enumerate(rows)
                if len(r) > 4 and r[2].strip() == "ID STRING" and r[4].strip() == "RESULT TYPES"]
    if not hdr_rows:
        raise SystemExit("🔴 Curios.csv 里找不到块表头行（`ID STRING` + `RESULT TYPES`）⇒ 不猜")
    header = [c.strip() for c in rows[hdr_rows[0]]]

    out = []
    for h in hdr_rows:
        title = rows[h - 1]
        # 块体 = 表头之后到下一个标题行之前
        end = next((x for x in hdr_rows if x > h), len(rows))
        body = rows[h + 1:end - 1]

        def cell(row_idx, col_name):
            ci = header.index(col_name)
            r = rows[row_idx]
            return r[ci].strip() if ci < len(r) else ""

        # 段内小表：`REGION FOUND` / `ALL` / `FULL CURIO?` / `TAGS` / `Item Interactions`
        meta, results = {}, []
        for ri in range(h + 1, end - 1):
            r = rows[ri]
            key = r[2].strip() if len(r) > 2 else ""
            if not key:
                continue
            if key in ("REGION FOUND", "ALL", "FULL CURIO?", "TAGS"):
                meta.setdefault(key, []).append(
                    [c.strip() for c in r[3:] if c.strip()])
            elif key == "Item Interactions":
                continue
            elif r[4].strip():  # 有 `RESULT TYPES` ⇒ 是一条结果
                results.append({
                    "result_type": r[4].strip(),
                    "weight": r[5].strip(),
                    "chance": r[6].strip(),
                    "result_1": r[7].strip(),
                    "r1_weight": r[8].strip(),
                    "r1_pct": r[9].strip(),
                    "result_2": r[10].strip(),
                    "r2_weight": r[11].strip(),
                    "r2_pct": r[12].strip(),
                    "result_3": r[13].strip(),
                    "r3_weight": r[14].strip(),
                    "r3_pct": r[15].strip(),
                    "string": r[16].strip(),
                    "notes": r[17].strip() if len(r) > 17 else "",
                    "row": ri + 1,
                })
        out.append({
            "index": title[1].strip() if len(title) > 1 else "",
            "display": title[2].strip() if len(title) > 2 else "",
            "kind": title[4].strip() if len(title) > 4 else "",
            "id": cell(h + 1, "ID STRING"),
            "header_row": h + 1,
            "meta": meta,
            "results": results,
        })
    return header, out


def main() -> int:
    result = {}

    # ---- ① Curios.csv ----
    header, curios = parse_curios_csv(os.path.join(CDIR, "Curios.csv"))
    print(f"[curio] `Curios.csv` ⇒ 表头 **{len(header)} 列** · **奇物 {len(curios)} 条** ✓")
    print(f"[curio]   表头：{header}")
    ids = [c["id"] for c in curios if c["id"]]
    from collections import Counter as _C
    print(f"[curio]   有 id 的 **{len(ids)}** · 去重 **{len(set(ids))}** ✓")
    print(f"[curio]   kind 分布：{dict(_C(c['kind'] for c in curios))} ✓")
    print(f"[curio]   样例：{[ (c['index'], c['display'], c['id']) for c in curios[:4] ]}")
    result["curios"] = {"header": header, "items": curios, "file": "Curios/Curios.csv"}

    # ---- ② Obstacles.json ----
    obs, t_obs = load_json(os.path.join(CDIR, "Obstacles.json"))
    print(f"[curio] `Obstacles.json` ⇒ props **{len(obs.get('props') or [])}** 条 · "
          f"尾随逗号 {t_obs} ✓")
    result["obstacles"] = {"items": obs.get("props") or [], "file": "Curios/Obstacles.json"}

    # ---- ③ Traps.json ----
    traps, t_tr = load_json(os.path.join(CDIR, "Traps.json"))
    tl = traps.get("props") or []
    print(f"[curio] `Traps.json` ⇒ props **{len(tl)}** 条 · 尾随逗号 {t_tr} ✓")
    for t in tl:
        dv = t.get("difficulty_variations") or []
        print(f"[curio]     {t.get('name'):20s} 难度变体 {len(dv)}")
    result["traps"] = {"items": tl, "file": "Curios/Traps.json"}

    # ---- 🎖️ 与 A12 的 props 段交叉校验（这是本件的目的）----
    if os.path.isfile(DUNGEONS):
        dun = json.load(open(DUNGEONS, encoding="utf-8"))
        used = sorted({t for d in dun["dungeons"] for e in d["props"] for t in e["types"]})
        known = set(ids)
        known |= {o.get("name") for o in (obs.get("props") or [])}
        known |= {t.get("name") for t in tl}
        known.discard(None)
        unknown = [u for u in used if u not in known]
        print()
        print(f"[curio] 🎖️ 与 A12 `props` 段交叉校验：引用名字去重 **{len(used)}** ⇒ "
              f"**未命中 {len(unknown)}**")
        if unknown:
            print(f"[curio]   🔴 未命中：{unknown}")
        result["a12_props_used"] = used
        result["a12_props_unknown"] = unknown

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": CDIR,
            "note": "A12 下游：Curios.csv（**CSV 形态，第一个**）+ Obstacles.json + Traps.json。"
                    "目的 = 校验 A12 `props` 段引用的 57 个名字。本件【只抽不落库】✓",
            **result,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[curio] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
