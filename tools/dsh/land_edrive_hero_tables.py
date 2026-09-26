#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""land_edrive_hero_tables.py -- 历史工具：把我方 4 原型 5 阶数值改回【一手 E 盘】。

🔴🔴 【已被取代 · 默认拒绝运行】(2026-09-26 · A3)
  用户指令改判了判据源：「数值一律采用参考项目 `F:\\GithubPro\\Darkest-Dungeon-Unity`」
  ⇒ 本工具做的正是**相反**的事（把数值改回 E 盘）⇒ 跑它 = **反向操作** ⚠️
  ⇒ ✅ 现行工具：`python tools/dsh/land_ref_hero_tiers.py [--check]`
  ⇒ 本文件保留只为**历史对照**：默认 `exit 2` 拒绝；`--apply` 硬性禁止（永不写盘）✓

⚠️ 它为什么保留而不是删掉：`reports/edrive_vs_reference_hero_tables.md` 那份"一手 vs 参考"的对账
   要用它复算（"我们当初从一手抄了什么"）；**删了就没法回答**"改动是不是只来自参考" ✓
"""

from __future__ import annotations

import io
import json
import os
import re
import sys

# ⚠️ Windows 控制台是 GBK ⇒ 不设这一行，任何 emoji/中文 print 都会 **UnicodeEncodeError 崩掉**
#    （崩溃退出码也是 1 ⇒ 会把"我拒绝运行"伪装成"脚本坏了"，**两者必须能分辨** ✓）
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover - 老 Python / 非文本流
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UNITS = os.path.join(REPO, "darkest", "data", "units.json")
EDRIVE = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
BACKUP_DIR = r"F:\GithubPro\Darkest-backup-20260921_003105-expflow"

SUPERSEDED = """🔴🔴 本工具【已被取代】—— 判据源已改（用户指令 2026-09-26）
   现行判据：数值一律【采用参考项目】`F:\\GithubPro\\Darkest-Dungeon-Unity`
   本工具做的是相反的事（把数值改回【一手 E 盘】）⇒ **默认拒绝运行** ✓
   ✅ 要落库/要核对，请用：python tools/dsh/land_ref_hero_tiers.py [--check]
   🕰️ 只看历史 dry run（不写盘）⇒ 加 `--i-know-superseded`；`--apply` **已被硬性禁止** ✓"""

WPN_RE = re.compile(
    r'\.name\s+"(?P<name>[a-z_]+_weapon_\d+)"\s+\.atk\s+(?P<atk>-?\d+(?:\.\d+)?)%\s+'
    r"\.dmg\s+(?P<dmin>-?\d+)\s+(?P<dmax>-?\d+)\s+\.crit\s+(?P<crit>-?\d+(?:\.\d+)?)%\s+"
    r"\.spd\s+(?P<spd>-?\d+)")
ARM_RE = re.compile(
    r'\.name\s+"(?P<name>[a-z_]+_armour_\d+)"\s+\.def\s+(?P<def>-?\d+(?:\.\d+)?)%\s+'
    r"\.prot\s+(?P<prot>-?\d+)\s+\.hp\s+(?P<hp>-?\d+)\s+\.spd\s+(?P<spd>-?\d+)")


def hero_from_align(align: str) -> str | None:
    """'…对齐来源：Hellion（参考项目…' -> 'hellion'  (no hardcoded table here)

    ⚠️ 2026-09-26：A3 把 `_align` 的写法改成「…改用参考项目）：`…\\Hellion.bytes` 的 …」
       ⇒ **两种写法都要认**（旧：`来源：<Hero>` · 新：`\\<Hero>.bytes`）✓
    """
    m = re.search(r"来源：\s*([A-Za-z][A-Za-z _-]*)", align or "")
    if not m:
        m = re.search(r"\\+([A-Za-z][A-Za-z]*)\.bytes", align or "")
    return m.group(1).strip().replace(" ", "_").replace("-", "_").lower() if m else None


def read_primary_hero(edrive: str, hero: str):
    p = os.path.join(edrive, "heroes", hero, "%s.info.darkest" % hero)
    if not os.path.isfile(p):
        return None
    txt = io.open(p, encoding="utf-8", errors="replace").read()
    wpn = [{"atk_pct": int(round(float(m.group("atk")))), "dmg_min": int(m.group("dmin")),
            "dmg_max": int(m.group("dmax")), "crit_pct": int(round(float(m.group("crit")))),
            "spd": int(m.group("spd"))} for m in WPN_RE.finditer(txt)]
    arm = [{"def_pct": int(round(float(m.group("def")))), "prot": int(m.group("prot")),
            "hp": int(m.group("hp")), "spd": int(m.group("spd"))} for m in ARM_RE.finditer(txt)]
    return {"weapon": wpn, "armour": arm}


def main() -> int:
    # 🔴🔴 已被取代：先拦两道（默认拒绝 + `--apply` 硬禁），**再**做任何读盘/写盘 ✓
    if "--i-know-superseded" not in sys.argv:
        print(SUPERSEDED)
        return 2
    if "--apply" in sys.argv:
        print(SUPERSEDED)
        print("🔴 `--apply` 已被【硬性禁止】：它会写回 E 盘值并把 `_align` 覆盖回旧口径（静默改错）✓")
        return 2

    units = json.load(io.open(UNITS, encoding="utf-8"))

    deltas = []
    for u in units["units"]:
        if u["id"] not in ("warrior", "tank", "medic", "commissar"):
            continue
        hero = hero_from_align(u.get("_align", ""))
        if not hero:
            # 🔴 响亮失败：**不许静默跳过**（否则会打印"0 处不同"，把"读不出"伪装成"一致"）
            print("🔴 %s 的 `_align` 里读不出英雄名 ⇒ 正则已过期"
                  "\n   实测 _align = %s" % (u["id"], u.get("_align")))
            return 2
        prim = read_primary_hero(EDRIVE, hero)
        if not prim:
            # 🔴 响亮失败（不再是"跳过"）：读不到一手却继续 ⇒ 会打印"改 0 阶"，被读成"两边一样"⚠️
            print("🔴 一手读不到 %s（%s\\heroes\\%s\\%s.info.darkest 不存在）⇒ 拒绝输出对照表 ✓"
                  % (u["id"], EDRIVE, hero, hero))
            return 2
        wchg, achg = [], []
        for i, pv in enumerate(prim["weapon"]):
            cur = u.get("weapon") or []
            if i < len(cur) and cur[i] != pv:
                wchg.append((i, dict(cur[i]), dict(pv)))
        for i, pv in enumerate(prim["armour"]):
            cur = u.get("armour") or []
            if i < len(cur) and cur[i] != pv:
                achg.append((i, dict(cur[i]), dict(pv)))
        deltas.append((u["id"], hero, wchg, achg))

    print("=== 一手 vs 我们现存的 5 阶（逐原型 · **仅历史对照**） ===")
    for uid, hero, wchg, achg in deltas:
        print("  %-10s ← %-18s weapon 改 %d 阶 · armour 改 %d 阶" % (uid, hero, len(wchg), len(achg)))
        for i, old, new in (wchg + achg)[:4]:
            print("       [%d] %s  →  %s" % (i, json.dumps(old, ensure_ascii=False),
                                            json.dumps(new, ensure_ascii=False)))

    print("[land] dry run only —— 本工具已被 land_ref_hero_tiers.py 取代，永不写盘 ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
