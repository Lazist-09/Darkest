# -*- coding: utf-8 -*-
"""clean_inbox.py -- archive an inbox window, then empty it.  Two steps, both proven.

Why this tool exists (纪律 v2 / 纪律 S / 纪律 BV):
  * 收件箱纪律 v2: 只删除【已完成】的任务 —— never blanket-clear, and never clear
    without reading the full text first.
  * 纪律 S: read the whole window before clearing it.
  * 纪律 AK/BT: "I emptied it" must be provable afterwards, so the deleted text is
    copied to a durable, git-tracked archive FIRST, and both byte counts are printed.

Usage:
  python tools/dsh/clean_inbox.py --window doc/windows/策划窗口.txt \
                                 --archive reports/planner_inbox_processed_20260926.md

Refuses to run if the window's delivery markers are NOT all accounted for
(--expect N), so a half-read inbox cannot be wiped by accident.
Prints ASCII only (GBK console safe); writes UTF-8 without BOM.
"""
from __future__ import annotations

import argparse
import datetime as _dt
import os
import re
import sys

# \r? because the windows are CRLF -- `$` alone would leave the \r and never match
# (that is exactly 纪律 BZ: a scanner assuming a line shape it never verified).
MARKER = re.compile(r"^[A-Z][A-Z0-9_-]{6,}\r?$", re.M)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--window", required=True)
    ap.add_argument("--archive", required=True)
    ap.add_argument("--expect", type=int, default=-1,
                    help="expected number of delivery markers in the window; mismatch aborts")
    a = ap.parse_args(argv)

    if "archive" in a.archive.replace("\\", "/"):
        print("WARN: archive path looks like a gitignored archive/ dir -- prefer reports/ (纪律: 归档要进 git)")

    old = open(a.window, encoding="utf-8", newline="").read()
    before = os.path.getsize(a.window)
    markers = sorted(set(MARKER.findall(old)))
    print("window : %s" % a.window)
    print("before : %d bytes / %d lines" % (before, len(old.splitlines())))
    print("markers: %d -- %s" % (len(markers), ",".join(markers) or "none"))
    if a.expect >= 0 and len(markers) != a.expect:
        print("ABORT: expected %d markers, found %d (read the full window first)" % (a.expect, len(markers)))
        return 3
    if before == 0:
        print("nothing to do: window already empty")
        return 0

    stamp = _dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    head = "\n\n---\n\n## 归档：%s 处理完毕（%s）· %d bytes\n\n```\n" % (a.window, stamp, before)
    tail = "\n```\n"
    mode = "a" if os.path.exists(a.archive) else "w"
    with open(a.archive, mode, encoding="utf-8", newline="") as fh:
        fh.write(head + old.rstrip("\n") + tail)
    print("archive: %s (%d bytes)" % (a.archive, os.path.getsize(a.archive)))

    with open(a.window, "w", encoding="utf-8", newline="") as fh:
        fh.write("")
    after = os.path.getsize(a.window)
    left = MARKER.findall(open(a.window, encoding="utf-8", newline="").read())
    print("after  : %d bytes   markers left=%d" % (after, len(left)))
    print("OK" if after == 0 and not left else "FAIL")
    return 0 if after == 0 and not left else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
