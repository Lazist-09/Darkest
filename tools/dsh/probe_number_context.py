# -*- coding: utf-8 -*-
"""probe_number_context.py -- READ-ONLY one-off probe.

Usage:
  python tools/dsh/probe_number_context.py 441 445 459 464 469
  python tools/dsh/probe_number_context.py 441 --files doc/modules/dd1_baseline.md

Prints, for every `#NNN` given, every citation in the chosen files with
+-CONTEXT characters around the match.  Writes to reports/_probe_numbers.txt
(console stays ASCII so GBK codepages cannot kill it).
For backfilling doc/state.md: the topic of a number must be READ, not guessed
(纪律 AC/BL).
"""
from __future__ import annotations

import os
import re
import sys

ROOT = r"F:\GithubPro\Darkest"
DEFAULT_FILES = [
    "doc/Project_Memory.md",
    "doc/modules/dd1_baseline.md",
    "doc/state.md",
]
CONTEXT = 200


def main(argv: list[str]) -> int:
    nums: list[int] = []
    files = list(DEFAULT_FILES)
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--files":
            i += 1
            files = [argv[i]]
        elif a.isdigit():
            nums.append(int(a))
        i += 1
    if not nums:
        print("usage: probe_number_context.py NNN [NNN...] [--files path]")
        return 2

    out_path = os.path.join(ROOT, "reports", "_probe_numbers.txt")
    with open(out_path, "w", encoding="utf-8", newline="\n") as o:
        for n in nums:
            pat = re.compile(r"#%d(?![0-9])" % n)
            o.write("\n" + "=" * 70 + "\n### #%d\n" % n)
            found = 0
            for rel in files:
                path = os.path.join(ROOT, rel)
                if not os.path.isfile(path):
                    continue
                with open(path, "r", encoding="utf-8", errors="replace") as fh:
                    for ln, line in enumerate(fh, 1):
                        for m in pat.finditer(line):
                            found += 1
                            a = max(0, m.start() - CONTEXT)
                            b = min(len(line), m.end() + CONTEXT)
                            o.write("\n- `%s:%d`\n  %s%s%s\n" % (
                                rel, ln,
                                "..." if a > 0 else "",
                                re.sub(r"\s+", " ", line[a:b]).strip(),
                                "..." if b < len(line) else "",
                            ))
            if not found:
                o.write("\n(no citation in the probed files)\n")
    print("numbers probed : %s" % ",".join("#%d" % n for n in nums))
    print("files : %s" % ", ".join(files))
    print("report : %s" % out_path)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
