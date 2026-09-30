# -*- coding: utf-8 -*-
"""collect_decision_numbers.py -- READ-ONLY.

Why: state.md's registry table stops at #417 while #418..#479 are
already in flight across doc/**, reports/**, skills/** with no row.
Discipline AC (measure before ruling) + BR (count before writing):
I must not invent 61 rows from memory -- I must first collect every
citation of each number, then write each row from its evidence.

What it does:
  * scans text files under the given roots
  * for every `#NNN` in [LO, HI], records (path, line_no, snippet)
  * scores snippets (decision words > presence) and keeps the best few
  * writes a UTF-8 report (console stays GBK-safe: only ASCII on stdout)

Output: reports/state_backfill_<LO>_<HI>_sources.md
"""
from __future__ import annotations

import os
import re
import sys

ROOT = r"F:\GithubPro\Darkest"
ROOTS = ["doc", "reports", "skills", "tools", "CHANGELOG.md"]
LO, HI = 418, 479

EXTS = {".md", ".txt", ".py", ".cs", ".json", ".ps1", ".yml", ".yaml"}
# files whose own text is the registry -- cited separately, not as evidence
REGISTRY = os.path.join("doc", "state.md")

NUM_RE = re.compile(r"#(\d{3,4})")
DECISION_WORDS = [
    "裁定", "确认", "结论", "立纪律", "纪律", "收下", "决定",
    "已定", "改为", "取", "执行", "登记", "投出", "下发",
]
NOISE_HINTS = ["state.md"]


def iter_files():
    for r in ROOTS:
        p = os.path.join(ROOT, r)
        if os.path.isfile(p):
            yield p
            continue
        for dirpath, dirnames, filenames in os.walk(p):
            dirnames[:] = [d for d in dirnames if d not in {".git", "__pycache__", "node_modules"}]
            for fn in filenames:
                if os.path.splitext(fn)[1].lower() in EXTS:
                    yield os.path.join(dirpath, fn)


def snippet(line: str, m: re.Match) -> str:
    a = max(0, m.start() - 90)
    b = min(len(line), m.end() + 150)
    s = line[a:b].strip()
    s = re.sub(r"\s+", " ", s)
    if a > 0:
        s = "..." + s
    if b < len(line):
        s = s + "..."
    return s


def score(path: str, line: str, m: re.Match) -> int:
    sc = 0
    for w in DECISION_WORDS:
        if w in line:
            sc += 2
    # a citation that says what the number *is* usually sits next to a delimiter
    if re.search(r"#%d[^\d]{0,3}(?:（|\(|:|：|=|→|⇒)" % int(m.group(1)), line):
        sc += 3
    if "state.md" in path:
        sc -= 50
    # letters/reports by a role tend to restate the ruling
    if "DELIVERY" in line or "投递" in line:
        sc += 1
    return sc


def main() -> int:
    hits: dict[int, list[tuple[int, str, int, str]]] = {n: [] for n in range(LO, HI + 1)}
    files = 0
    for path in iter_files():
        if ".tools" in path or "\\reports\\archive\\" in path:
            continue
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                lines = fh.read().splitlines()
        except OSError:
            continue
        files += 1
        rel = os.path.relpath(path, ROOT)
        for i, line in enumerate(lines, 1):
            for m in NUM_RE.finditer(line):
                n = int(m.group(1))
                if LO <= n <= HI:
                    hits[n].append((score(rel, line, m), rel, i, snippet(line, m)))

    out_path = os.path.join(ROOT, "reports", "state_backfill_%d_%d_sources.md" % (LO, HI))
    empty = []
    with open(out_path, "w", encoding="utf-8", newline="\n") as out:
        out.write("# #%d~#%d 引用来源汇总（只读工具产出 · 供补登 state.md 用）\n\n" % (LO, HI))
        out.write("- 工具：`tools/dsh/collect_decision_numbers.py`（只读）\n")
        out.write("- 扫描：%s（%d 个文本文件）\n" % (", ".join(ROOTS), files))
        out.write("- 用途：**纪律 AC**（裁前先量）—— 61 行不许凭记忆写，逐行按这里的一手引用写。\n\n")
        for n in range(LO, HI + 1):
            recs = hits[n]
            if not recs:
                empty.append(n)
                out.write("## #%d  —— 🔴 零引用（全项目搜不到）\n\n" % n)
                continue
            recs.sort(key=lambda r: (-r[0], r[1], r[2]))
            seen: set[str] = set()
            kept = []
            for sc, rel, i, sn in recs:
                key = rel
                if key in seen:
                    continue
                seen.add(key)
                kept.append((sc, rel, i, sn))
                if len(kept) >= 4:
                    break
            out.write("## #%d  （共 %d 处引用，显示 %d 处）\n\n" % (n, len(recs), len(kept)))
            for sc, rel, i, sn in kept:
                out.write("- `%s:%d` %s\n" % (rel, i, sn))
            out.write("\n")

    print("files scanned : %d" % files)
    print("numbers with 0 citations : %d" % len(empty))
    if empty:
        print("zero-citation list : %s" % ",".join("#%d" % n for n in empty))
    tot = sum(len(v) for v in hits.values())
    print("total citations : %d" % tot)
    print("report : %s" % out_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
