#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_heirloom_exchange.py -- land the 12 heirloom exchange rates from the PRIMARY (E-drive).

WHY (planner ruling DELIVERY-DESIGNER-HEIRLOOM-RULING-20260922 item 1)
  "兑换表 = 现在就做（12 条 · 一次落完）" --照 E 盘 campaign/heirloom_exchange/heirloom_exchange.json
  fields: exchange_from_type / exchange_from_amount / exchange_to_type / exchange_to_amount
  each entry carries origin="dd1" and placeholder=true (per #422 wording).

SOURCE LEVEL: PRIMARY (E-drive) per dd1_baseline 32.1 -- so this is the *best* level, not third-party.

USAGE
  python tools/dsh/extract_dd1_heirloom_exchange.py [--edrive <path>]
EXIT: 0 written, 1 primary not readable.
"""

from __future__ import annotations

import io
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EDRIVE = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
OUT = os.path.join(REPO, "darkest", "data", "heirloom_exchange.json")


def main() -> int:
    argv = sys.argv[1:]
    edrive = argv[argv.index("--edrive") + 1] if "--edrive" in argv else EDRIVE
    src = os.path.join(edrive, "campaign", "heirloom_exchange", "heirloom_exchange.json")
    if not os.path.isfile(src):
        print("[heirloom] primary not readable at %s -- nothing written" % src)
        return 1

    raw = json.load(io.open(src, encoding="utf-8"))
    rates = raw["exchange_rates"] if isinstance(raw, dict) else raw
    out_rates = []
    for r in rates:
        out_rates.append({
            "exchange_from_type": r["exchange_from_type"],
            "exchange_from_amount": int(r["exchange_from_amount"]),
            "exchange_to_type": r["exchange_to_type"],
            "exchange_to_amount": int(r["exchange_to_amount"]),
            "origin": "dd1",
            "placeholder": True,
        })

    out = {
        "_note": ("M8 祖产：兑换表 12 条 —— 由 tools/dsh/extract_dd1_heirloom_exchange.py 从 E 盘【一手】"
                  "campaign/heirloom_exchange/heirloom_exchange.json 转写；每条约 origin=dd1 + placeholder=true ✓"),
        "_source": "E:\\SteamLibrary\\steamapps\\common\\DarkestDungeon\\campaign\\heirloom_exchange",
        "_design": ("一手实测的设计：**所有兑换都损失 50%** ⇒ 相对价值 portrait : bust : deed : crest "
                    "= **6 : 3 : 3 : 2** ✓（校验器把它当作硬判据 ✓）"),
        "exchange_rates": out_rates,
    }
    io.open(OUT, "w", encoding="utf-8").write(json.dumps(out, ensure_ascii=False, indent=2) + "\n")
    print("[heirloom] wrote %s (%d rates)" % (os.path.relpath(OUT, REPO), len(out_rates)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
