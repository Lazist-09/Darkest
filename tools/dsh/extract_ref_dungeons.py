#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_dungeons.py —— A12：抽取参考项目的**地牢定义**（`Dungeons/*.bytes`）✓

🔴 **形态判定先做**（纪律：先判形态，再选解析器）——
   实测：`Dungeons/*.bytes` **7 个全是【DD1 文本】** ✓
   而 `Maps/*.bytes` **7 个全是【Unity 二进制】**（含长度前缀 + `\x00`）⚠️
   📌 `.bytes` 后缀**不定形态** —— 同一目录下两种形态并存 ⇒ **不能按后缀选解析器** ✓

`Dungeons/*.bytes` 的段落结构（实测）：
   头部：`id:` · `is_released:` · `hall_variants:` · `room_variants:`（**空格分隔的名字表**）
   然后若干个 **`mash:` 段**，每段：`id:` + 若干**条目行**：
      `<kind>: [(.name <名字>)] .chance <权重> .types <怪物 stem>...`
   ⇒ 🎖️ **这是"遭遇/内容组合表"（mash）**：**每个条目就是一次遭遇或一处内容的名单** ✓
   🔴 **实测 `kind` 有 13 种**（不是我以为的 4 种 ⚠️ —— 我第一版硬编码 4 种，
      被 `Cove.bytes:48` 的 `stall:` **响亮挡下** ⇒ ✅ **fail-fast 起作用了**）✓：
      `hall`(355) · `room`(231) · `named`(70) · **`hall_curios`(66)** · **`stall`(61)** ·
      `boss`(27) · `room_curios`(27) · `room_treasures`(18) · `traps`(6) · `obstacles`(6) ·
      `secret_room_treasures`(5) ⇒ **共 872 个条目** ✓
   ⇒ 🔴 **第二次被挡下（更有价值）**：`props:` **本身是一个【段】**（不是裸列表 ⚠️）——
      我第一版把它当"裸列表" ⇒ 里面的 `hall_curios: .chance 10 .types discarded_pack`
      被判成"条目不在 mash 段内" ⇒ ✅ **又响了** ✓
   实测结构（`Cove.bytes:174-195`）：
      ```
      props:            ← **段名**
      hall_curios:      .chance 10 .types discarded_pack
      room_curios:      .chance 3  .types fish_idol
      room_treasures:   .chance 2  .types unlocked_strongbox
      traps:            .chance 1  .types lurker
      obstacles:        .chance 1  .types shipwreck
      secret_room_treasures: .chance 1 .types secret_stash
      .end
      ```
   ⇒ 🔴 **即：DD1 的内容分两大段 —— `mash:`（遭遇）+ `props:`（奇物/宝藏/陷阱/障碍）** ⚠️
   ⇒ 🔴 **`types` 里的名字【不全是怪物】**（`props` 段里是奇物）⇒ 交叉校验要**按段分开**做 ✓

🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓
用法：python tools/dsh/extract_ref_dungeons.py
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
DDIR = os.path.join(DATA, "Dungeons")
MDIR = os.path.join(DATA, "Maps")
OUT = os.path.join(REPO, "reports", "unity_ref", "dungeons_from_ref.json")
MONSTERS = os.path.join(REPO, "reports", "unity_ref", "monsters_from_ref.json")

ENTRY_RE = re.compile(
    r"^(?P<kind>[a-z_]+):\s*"
    r"(?:\.name\s+(?P<name>\S+)\s+)?"
    r"\.chance\s+(?P<chance>-?\d+)\s+"
    r"\.types\s+(?P<types>.+)$"
)
HEAD_RE = re.compile(r"^(?P<key>id|is_released|hall_variants|room_variants):\s*(?P<val>.*)$")
NON_ENTRY = {"props"}  # `props:` 是**段名**（段内条目仍带 `.chance`/`.types`）✓
SECTION_NAMES = {"mash", "props"}  # 实测的两大段 ✓


def looks_text(raw: bytes) -> bool:
    head = raw[:72]
    printable = sum(1 for b in head if 32 <= b < 127 or b in (9, 10, 13))
    return printable / max(1, len(head)) > 0.85


def parse_dungeon(path: str):
    text = io.open(path, encoding="utf-8-sig", errors="replace").read()
    head = {}
    mashing = []
    props = []
    cur = None
    cur_section = None
    for lineno, raw in enumerate(text.splitlines(), 1):
        s = raw.strip()
        if not s:
            continue
        # 🔴 段名：`mash:` / `props:`（**两个都要认** —— 我第一版只认 mash）✓
        if s.endswith(":") and s[:-1] in SECTION_NAMES:
            cur_section = s[:-1]
            if cur_section == "mash":
                cur = {"id": None, "entries": [], "line": lineno}
                mashing.append(cur)
            else:
                cur = None
            continue
        if s == ".end":
            cur = None
            cur_section = None
            continue
        m = ENTRY_RE.match(s)
        if m and m["kind"] != "id":
            if cur_section is None:
                raise SystemExit(f"🔴 {path}:{lineno} 条目不在任何段内（不静默跳过）：{s[:70]}")
            entry = {
                "kind": m["kind"],
                "name": m["name"],
                "chance": int(m["chance"]),
                "types": m["types"].split(),
                "line": lineno,
            }
            if cur_section == "mash" and cur is not None:
                cur["entries"].append(entry)
            else:
                props.append(entry)
            continue
        m = HEAD_RE.match(s)
        if m and cur_section is None:
            head[m["key"]] = m["val"].strip()
            continue
        if cur is not None and s.startswith("id:"):
            cur["id"] = int(s.split(":", 1)[1].strip())
            continue
        if cur_section is None:
            continue
        raise SystemExit(f"🔴 {path}:{lineno} 未识别的行（不静默跳过）：{s[:80]}")
    return head, mashing, props


def main() -> int:
    # ---- 形态判定（两个目录都判，留证）----
    print("[a12] 形态判定：")
    d_files = sorted(n for n in os.listdir(DDIR) if not n.endswith(".meta"))
    m_files = sorted(n for n in os.listdir(MDIR) if not n.endswith(".meta"))
    d_text = sum(1 for n in d_files if looks_text(open(os.path.join(DDIR, n), "rb").read()))
    m_text = sum(1 for n in m_files if looks_text(open(os.path.join(MDIR, n), "rb").read()))
    print(f"[a12]   `Dungeons/` {len(d_files)} 个 ⇒ **文本 {d_text}** ✓")
    print(f"[a12]   `Maps/`     {len(m_files)} 个 ⇒ **文本 {m_text}** "
          f"⇒ 🔴 **{'全二进制' if m_text == 0 else '混合'}** ⚠️")

    dungeons = []
    for n in d_files:
        head, mash, props = parse_dungeon(os.path.join(DDIR, n))
        dungeons.append({
            "file": f"Dungeons/{n}",
            "name": n.replace(".bytes", ""),
            "head": head,
            "mash_count": len(mash),
            "mash": mash,
            "props": props,
        })

    print()
    print("[a12] 逐文件：")
    tot_e = 0
    for d in dungeons:
        ne = sum(len(m["entries"]) for m in d["mash"])
        tot_e += ne
        print(f"[a12]   {d['name']:10s} id={d['head'].get('id'):>4s} "
              f"released={d['head'].get('is_released'):>5s} "
              f"hall_variants={d['head'].get('hall_variants'):>2s} "
              f"room_variants={d['head'].get('room_variants'):>22s} "
              f"mash {d['mash_count']:2d} 段 · 条目 {ne:3d}")
    print(f"[a12]   ⇒ 合计 **{sum(d['mash_count'] for d in dungeons)} 个 mash 段 / {tot_e} 个条目** ✓")

    # ---- 🎖️ 与 A4 交叉校验：**按段分开验**（`props` 段里是奇物，不是怪物）----
    known_m = set()
    if os.path.isfile(MONSTERS):
        ms = json.load(open(MONSTERS, encoding="utf-8"))
        known_m = {r["stem"] for r in ms["monsters"]} | {r["type"] for r in ms["monsters"]}
    monster_used = sorted({t for d in dungeons for m in d["mash"] for e in m["entries"]
                           for t in e["types"]})
    content_used = sorted({t for d in dungeons for e in d["props"] for t in e["types"]})
    unknown = [t for t in monster_used if t not in known_m]
    print()
    print("[a12] 🎖️ 与 A4 交叉校验（**按段分开验** —— `props` 段里是奇物不是怪物）✓")
    print(f"[a12]   **`mash` 段**（遭遇）⇒ 怪物名去重 **{len(monster_used)}** · "
          f"**未在 A4 怪物表里的 {len(unknown)}**")
    if unknown:
        print(f"[a12]     🔴 未命中：{unknown[:20]}")
    print(f"[a12]   **`props` 段**（奇物/宝藏/陷阱/障碍）⇒ 名字去重 **{len(content_used)}** "
          f"（**要另找奇物表** —— A4 只管怪物）✓")
    # ---- kind 分布（两段分开）----
    from collections import Counter
    kc_m = Counter(e["kind"] for d in dungeons for m in d["mash"] for e in m["entries"])
    kc_p = Counter(e["kind"] for d in dungeons for e in d["props"])
    print(f"[a12] `mash` 段的 kind 分布：{dict(kc_m)} ✓")
    print(f"[a12] `props` 段的 kind 分布：{dict(kc_p)} ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": DDIR,
            "note": "A12 抽取：Dungeons/*.bytes（7 个文件，**DD1 文本**）。"
                    "🔴 Maps/*.bytes 是 Unity 二进制 ⇒ **只作参考，不入本表** ✓ 本件【只抽不落库】✓",
            "dungeon_count": len(dungeons),
            "entry_total": tot_e,
            "maps_dir_is_binary": m_text == 0,
            "maps_file_count": len(m_files),
            "unknown_monster_refs": unknown,
            "dungeons": dungeons,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[a12] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
