# -*- coding: utf-8 -*-
"""map_number_to_envelope.py -- READ-ONLY.

Why: a `#NNN` number is *owned* by one letter (信封).  In the windows the
number appears in two places:
  * the letter's title line, as a trailing  （#NNN）  (sometimes spaced)
  * the letter's footer, as  state.md **#NNN**
So the topic of a number = the title line of the letter whose footer/header
carries that number.  This tool pairs them, so doc/state.md can be backfilled
from evidence instead of memory (纪律 AC / BL / BU).

Output: reports/number_to_envelope.md
"""
from __future__ import annotations

import os
import re
import sys

ROOT = r"F:\GithubPro\Darkest"
WINDOWS = os.path.join(ROOT, "doc", "windows")
LO, HI = 418, 479
LOOKBACK = 600          # lines above a footer in which its title may sit

HDR = re.compile(r"^#{2,4}\s*信封")
# trailing （#NNN） / ( #NNN )  anywhere on a title line
TITLE_NUM = re.compile(r"[（(]\s*#(\d{3,4})\s*[）)]")
FOOTER_NUM = re.compile(r"state\.md[^\n]{0,24}?#(\d{3,4})")
CLEAN = re.compile(r"[*`>]+")


def clip(s: str, n: int = 160) -> str:
    s = CLEAN.sub("", s)
    s = re.sub(r"\s+", " ", s).strip()
    return s if len(s) <= n else s[:n] + "..."


def main() -> int:
    owners: dict[int, list[str]] = {n: [] for n in range(LO, HI + 1)}
    for fn in sorted(os.listdir(WINDOWS)):
        if not fn.lower().endswith(".txt"):
            continue
        path = os.path.join(WINDOWS, fn)
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            lines = fh.read().splitlines()
        hdrs: list[tuple[int, str]] = [(i, l) for i, l in enumerate(lines) if HDR.match(l)]
        for i, line in enumerate(lines):
            for m in FOOTER_NUM.finditer(line):
                n = int(m.group(1))
                if not (LO <= n <= HI):
                    continue
                # nearest header at or above this footer
                cand = [h for h in hdrs if h[0] <= i]
                if cand and i - cand[-1][0] <= LOOKBACK:
                    hi_, hline = cand[-1]
                    owners[n].append("`%s:%d` 标题→ %s" % (fn, hi_ + 1, clip(hline)))
                else:
                    owners[n].append("`%s:%d` 页脚→ %s" % (fn, i + 1, clip(line)))
            for m in TITLE_NUM.finditer(line):
                n = int(m.group(1))
                if LO <= n <= HI and HDR.match(line):
                    owners[n].append("`%s:%d` 标题号→ %s" % (fn, i + 1, clip(line)))

    out = os.path.join(ROOT, "reports", "number_to_envelope.md")
    with open(out, "w", encoding="utf-8", newline="\n") as o:
        o.write("# 裁定号 × 拥有它的信封（只读工具产出）\n\n")
        o.write("- 工具：`tools/dsh/map_number_to_envelope.py`（只读）\n")
        o.write("- 判据：标题行 `### 信封…（#NNN）` 或 页脚 `state.md **#NNN**` ⇒ 该号属这封信\n")
        o.write("- 用途：补登 `doc/state.md` 时**逐号读议题**，不凭记忆（纪律 AC/BL）\n\n")
        for n in range(LO, HI + 1):
            recs = owners[n]
            if not recs:
                o.write("| #%d | 🔴 无归属信封（窗口内找不到页脚/标题号） |\n" % n)
                continue
            seen = []
            for r in recs:
                if r not in seen:
                    seen.append(r)
            o.write("| #%d | %s |\n" % (n, "<br>".join(seen)))

    have = sum(1 for n in owners if owners[n])
    print("numbers with an owning envelope : %d / %d" % (have, HI - LO + 1))
    print("numbers without one : %s" % (",".join("#%d" % n for n in range(LO, HI + 1) if not owners[n]) or "none"))
    print("report : %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
