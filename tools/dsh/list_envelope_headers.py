# -*- coding: utf-8 -*-
"""list_envelope_headers.py -- READ-ONLY.

Why: a decision number `#NNN` is created by an *envelope* (信封) in one of the
four windows; the envelope's title IS the topic of that number.  Backfilling
doc/state.md must read that topic, not guess it (纪律 AC / BL / BU).

Iron detail (this is why v1 missed 53 of 62 numbers): in these windows a
footer and the next header are often *glued onto one line*, e.g.

    ... state.md **#467**### 信封：2026-09-22 · ...（#468）

so the header is NOT at line start and `^###` never sees it.  v2 therefore
splits the whole file text on every occurrence of `### 信封` (anywhere), takes
the segment up to the next one, and reads from that segment:
  * number  = first  （#NNN）  in the title part, else first  state.md **#NNN**
  * topic   = first line of the segment, clipped

Output: reports/envelope_headers_by_number.md   (console stays ASCII)
"""
from __future__ import annotations

import os
import re
import sys

ROOT = r"F:\GithubPro\Darkest"
WINDOWS = os.path.join(ROOT, "doc", "windows")
LO, HI = 418, 479

HDR = re.compile(r"#{2,4}\s*信封")
TITLE_NUM = re.compile(r"[（(]\s*#(\d{3,4})\s*[）)]")
FOOTER_NUM = re.compile(r"state\.md[^\n]{0,24}?#(\d{3,4})")
CLEAN = re.compile(r"[*`>]+")
TITLE_LEN = 400


def clip(s: str, n: int = 170) -> str:
    s = CLEAN.sub("", s)
    s = re.sub(r"\s+", " ", s).strip()
    return s if len(s) <= n else s[:n] + "..."


def main() -> int:
    by_num: dict[int, list[str]] = {}
    unnamed: list[str] = []
    total = 0
    for fn in sorted(os.listdir(WINDOWS)):
        if not fn.lower().endswith(".txt"):
            continue
        with open(os.path.join(WINDOWS, fn), "r", encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        starts = [m.start() for m in HDR.finditer(text)]
        for k, s in enumerate(starts):
            e = starts[k + 1] if k + 1 < len(starts) else len(text)
            seg = text[s:e]
            total += 1
            title = clip(seg[:TITLE_LEN], 150)
            m = TITLE_NUM.search(seg[:TITLE_LEN])
            if not m:
                m = FOOTER_NUM.search(seg)
            if m:
                n = int(m.group(1))
                if LO <= n <= HI:
                    by_num.setdefault(n, []).append("`%s` %s" % (fn, title))
                    continue
            unnamed.append("`%s` %s" % (fn, title))

    out = os.path.join(ROOT, "reports", "envelope_headers_by_number.md")
    with open(out, "w", encoding="utf-8", newline="\n") as o:
        o.write("# 信封标题 x 裁定号（只读工具产出 · v2 按 `### 信封` 切段）\n\n")
        o.write("- 工具：`tools/dsh/list_envelope_headers.py`（只读）\n")
        o.write("- 扫描：`doc/windows/*.txt`，信封共 **%d** 条\n" % total)
        o.write("- v2 修正：页脚与下一个标题常被粘在同一行 ⇒ 不再用 `^###`，按出现位置切段\n\n")
        o.write("| 号 | 信数 | 信封标题（= 该号的议题） |\n|---|---|---|\n")
        for n in sorted(by_num):
            recs = by_num[n]
            o.write("| #%d | %d | %s |\n" % (n, len(recs), "<br>".join(recs)))
        o.write("\n## 无法定位号的信封（%d 条）\n\n" % len(unnamed))
        for t in unnamed:
            o.write("- %s\n" % t)

    dup = sorted(n for n, r in by_num.items() if len(r) > 1)
    missing = [n for n in range(LO, HI + 1) if n not in by_num]
    print("envelopes scanned : %d" % total)
    print("numbers located : %d / %d" % (len(by_num), HI - LO + 1))
    print("numbers with 2+ envelopes : %s" % (",".join("#%d" % n for n in dup) or "none"))
    print("numbers with NO envelope : %s" % (",".join("#%d" % n for n in missing) or "none"))
    print("report : %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
