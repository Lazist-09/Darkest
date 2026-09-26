#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""A3 落库器：我方 4 英雄的 `weapon` / `armour` **5 阶** —— 判据源改用【参考项目】。

判据源（用户指令 2026-09-26）：
    「不论所有问题、数值、逻辑，都先学习 `F:\\GithubPro\\Darkest-Dungeon-Unity` 的逻辑代码和数值，
      数值就采用这个项目的」
  ⇒ `doc/modules/dd1_baseline.md §32.1/§32.3` 的「一手（E 盘）> 二手 > 第三方」**作废**
  ⇒ 本工具把 4 个我方英雄的 5 阶数值**顶替**为参考项目的值 ✓

数据来源（**一手**：参考项目自己的 DD1 文本，不是我们转写过的中间件）：
    `…\\Assets\\Resources\\Data\\Heroes\\Info\\<英雄>.bytes` 的 `weapon:` / `armour:` 行
  🔴 并**双重校验**：逐字段与本仓冻结件 `reports/dd1_hero_tables_from_unity_ref.json` 对账，
     任何一处不等 ⇒ `exit 1`（**不静默落库**）✓

用法：
    python tools/dsh/land_ref_hero_tiers.py --check     # 只报读数，**不写盘**
    python tools/dsh/land_ref_hero_tiers.py             # 落库（幂等：再跑一遍应为 changed=0）

退出码：0 = 一致 / 落库成功；1 = 解析失败、阶数不对、或与冻结件不一致 ✓
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UNITS = os.path.join(REPO, "darkest", "data", "units.json")
FROZEN = os.path.join(REPO, "reports", "dd1_hero_tables_from_unity_ref.json")
DEFAULT_REF_ROOT = (
    r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info"
)

# 我方 4 原型 → 参考项目英雄（`_align` 里已记录过的配对；本轮不改配对）✓
MAP = {
    "warrior": "Hellion",
    "tank": "ManAtArms",
    "medic": "PlagueDoctor",
    "commissar": "Highwayman",
}

WEAPON_RE = re.compile(
    r'^weapon:\s+\.name\s+"(?P<name>[^"]+)"\s+\.atk\s+(?P<atk>-?\d+)%\s+'
    r"\.dmg\s+(?P<dmg_min>\d+)\s+(?P<dmg_max>\d+)\s+"
    r"\.crit\s+(?P<crit>\d+(?:\.\d+)?)%\s+\.spd\s+(?P<spd>-?\d+)"
)
ARMOUR_RE = re.compile(
    r'^armour:\s+\.name\s+"(?P<name>[^"]+)"\s+\.def\s+(?P<def>-?\d+)%\s+'
    r"\.prot\s+(?P<prot>-?\d+)\s+\.hp\s+(?P<hp>\d+)\s+\.spd\s+(?P<spd>-?\d+)"
)


def num(text: str):
    """源里写了小数点 ⇒ 存小数；没写 ⇒ 存整数（**照抄源文本的形态** ✓）。"""
    return float(text) if "." in text else int(text)


def parse_ref_hero(path: str) -> dict:
    """解析一个参考项目 `.bytes` 的 weapon/armour 行（**行号一并留下** ✓）。"""
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        lines = fh.read().splitlines()

    out = {"weapon": [], "armour": [], "lines": {"weapon": [], "armour": []}}
    for lineno, raw in enumerate(lines, 1):
        line = raw.strip()
        if line.startswith("weapon:"):
            m = WEAPON_RE.match(line)
            if m is None:
                raise SystemExit(f"[A3] 🔴 {path}:{lineno} 的 weapon 行解析失败（不静默跳过）\n      {line}")
            out["weapon"].append(
                {
                    "atk_pct": int(m["atk"]),
                    "dmg_min": int(m["dmg_min"]),
                    "dmg_max": int(m["dmg_max"]),
                    "crit_pct": num(m["crit"]),
                    "spd": int(m["spd"]),
                }
            )
            out["lines"]["weapon"].append(lineno)
        elif line.startswith("armour:"):
            m = ARMOUR_RE.match(line)
            if m is None:
                raise SystemExit(f"[A3] 🔴 {path}:{lineno} 的 armour 行解析失败（不静默跳过）\n      {line}")
            out["armour"].append(
                {
                    "def_pct": int(m["def"]),
                    "prot": int(m["prot"]),
                    "hp": int(m["hp"]),
                    "spd": int(m["spd"]),
                }
            )
            out["lines"]["armour"].append(lineno)

    for kind in ("weapon", "armour"):
        if len(out[kind]) != 5:
            raise SystemExit(f"[A3] 🔴 {path}: {kind} 行数 = {len(out[kind])}（参考项目必为 5 阶 0~4）")
    return out


def cross_check(hero: str, parsed: dict, frozen: dict) -> int:
    """与仓内冻结件逐字段对账（**两道独立读数**）；返回比较的字段数 ✓。"""
    ref = frozen.get("heroes", {}).get(hero)
    if ref is None:
        raise SystemExit(f"[A3] 🔴 冻结件 {os.path.basename(FROZEN)} 里没有英雄 {hero}")
    keys_w = [("atk_pct", "atk"), ("dmg_min", "dmg_min"), ("dmg_max", "dmg_max"),
              ("crit_pct", "crit"), ("spd", "spd")]
    keys_a = [("def_pct", "def"), ("prot", "prot"), ("hp", "hp"), ("spd", "spd")]
    compared = 0
    for i in range(5):
        for mine, theirs in keys_w:
            compared += 1
            a, b = parsed["weapon"][i][mine], ref["weapon"][i][theirs]
            if isinstance(a, (int, float)) and isinstance(b, (int, float)) and float(a) != float(b):
                raise SystemExit(
                    f"[A3] 🔴 {hero} weapon[{i}].{theirs}: 源 .bytes = {a} ／ 冻结件 = {b} ⇒ 两读数不一致，停 ✓"
                )
        for mine, theirs in keys_a:
            compared += 1
            a, b = parsed["armour"][i][mine], ref["armour"][i][theirs]
            if isinstance(a, (int, float)) and isinstance(b, (int, float)) and float(a) != float(b):
                raise SystemExit(
                    f"[A3] 🔴 {hero} armour[{i}].{theirs}: 源 .bytes = {a} ／ 冻结件 = {b} ⇒ 两读数不一致，停 ✓"
                )
    return compared


def align_note(hero: str, parsed: dict) -> str:
    wl, al = parsed["lines"]["weapon"], parsed["lines"]["armour"]
    src = (
        r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info"
        + f"\\{hero}.bytes"
    )
    # 🔴 注记里**不写"本次改动 N 个字段"** —— 那是【读数】，第二遍会变成 0
    #    ⇒ 会让本工具**字节不幂等**（读数归报告，注记只放【不会随时间变的出处】✓）
    return (
        "阶段 A 对齐来源（**A3 · 2026-09-26 改用参考项目**）："
        f"`{src}` 的 `weapon:` 第 {wl[0]}-{wl[-1]} 行 + `armour:` 第 {al[0]}-{al[-1]} 行（逐阶照抄 ✓）"
        "｜判据源已按用户指令改为【参考项目】⇒ `dd1_baseline §32.3` 的「一手为准」作废 ✓"
    )


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ref-root", default=DEFAULT_REF_ROOT)
    ap.add_argument("--check", action="store_true", help="只报读数，不写盘")
    args = ap.parse_args()

    frozen = json.load(open(FROZEN, encoding="utf-8"))
    data = json.load(open(UNITS, encoding="utf-8"))
    by_id = {u["id"]: u for u in data["units"]}

    parsed_by_hero, compared_total = {}, 0
    for hid, hero in MAP.items():
        if hid not in by_id:
            raise SystemExit(f"[A3] 🔴 units.json 里没有我方原型 {hid}")
        path = os.path.join(args.ref_root, hero + ".bytes")
        if not os.path.isfile(path):
            raise SystemExit(f"[A3] 🔴 参考项目文件不存在：{path}")
        parsed = parse_ref_hero(path)
        compared_total += cross_check(hero, parsed, frozen)
        parsed_by_hero[hid] = parsed

    # ---- 计算差异（before → after）----
    diffs, changed_by_hero = [], {}
    for hid, hero in MAP.items():
        parsed = parsed_by_hero[hid]
        unit = by_id[hid]
        n = 0
        for kind, fields in (("weapon", ["atk_pct", "dmg_min", "dmg_max", "crit_pct", "spd"]),
                             ("armour", ["def_pct", "prot", "hp", "spd"])):
            old = unit.get(kind) or []
            if len(old) != 5:
                raise SystemExit(f"[A3] 🔴 units.json 的 {hid}.{kind} 不是 5 阶（不静默补）")
            for i in range(5):
                for f in fields:
                    ov, nv = old[i][f], parsed[kind][i][f]
                    if float(ov) != float(nv):
                        diffs.append((hid, hero, f"{kind}[{i}].{f}", ov, nv))
                        n += 1
        changed_by_hero[hid] = n

    print(f"[A3] 参考源：{args.ref_root}")
    print(f"[A3] 与冻结件对账：{compared_total} 个字段（4 英雄 × 45）⇒ **全部一致** ✓")
    print(f"[A3] 我方 4 英雄 5 阶字段：{4 * 45} 个 ⇒ **不同 {len(diffs)} 个**")
    for hid in MAP:
        print(f"[A3]   {hid:10s} ← {MAP[hid]:13s} 改动 {changed_by_hero[hid]:2d} 个字段")
    if diffs:
        print("[A3] 逐字段差异（我方 → 参考）：")
        for hid, hero, path_, ov, nv in diffs:
            print(f"[A3]   {hid:10s} {path_:22s} {ov!s:>6s} → {nv!s:<6s} (← {hero})")

    if args.check:
        print("[A3] --check ⇒ 未写盘 ✓")
        return 0

    for hid, hero in MAP.items():
        unit = by_id[hid]
        parsed = parsed_by_hero[hid]
        unit["weapon"] = parsed["weapon"]
        unit["armour"] = parsed["armour"]
        unit["_align"] = align_note(hero, parsed)

    before = open(UNITS, encoding="utf-8", newline="").read()
    after = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    with open(UNITS, "w", encoding="utf-8", newline="") as fh:
        fh.write(after)

    sha = lambda s: hashlib.sha256(s.encode("utf-8")).hexdigest()[:16]  # noqa: E731
    print(f"[A3] 写盘：sha256 {sha(before)} → {sha(after)} · bytes {len(before.encode('utf-8'))} → {len(after.encode('utf-8'))}")
    if diffs:
        print(f"[A3] ✅ 已落 {len(diffs)} 个字段（4 英雄 · 判据源 = 参考项目）✓")
    else:
        print("[A3] ✅ 已经一致（幂等：本次无字段改动）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
