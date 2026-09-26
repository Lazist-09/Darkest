#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_control_chars.py —— 契约载体（窗口 / 文档）里的 **C0 控制字符** 门禁。

WHY（架构 2026-09-26 实测报出，我复核确认）
--------------------------------------------------
`doc/windows/*.txt` 与 `doc/**/*.md` 里存在 **C0 控制字符**，形状是
**「某个词的首字母没了」**：`\b`atery → `atery`、`\v`（VT）、`\f`（FF）、`\x00`。

🔴 **根因（我复核到的实证）**：
   写入路径把 **`\\` + 字母** 当 **C 转义**解码了 —— `\\b`→退格(0x08) · `\\v`→VT(0x0B) ·
   `\\f`→FF(0x0C) · `\\0`→NUL(0x00) · `\\a`→BEL(0x07) · `\\t`→TAB(0x09) · `\\n`→真换行(0x0A)
   实测计数（主程序窗）：**0x08 ×34 · 0x07 ×22 · 0x0C ×11 · 0x0B ×9 · 0x00 ×2** ✓
   而实测里 **0x09(TAB) 与 0x0A(LF) 是【合法】的**（它们本来就是排版）⇒ 门禁只报其余 ✓

🔴 **为什么它是【缺陷】而不是排版问题**（架构判据）：
   **"这段文本是【契约载体】吗？是 ⇒ 里面的控制字符要当【缺陷】，不当排版问题"** ✓
   因为 `\\n` 会把一个表行**拆成两条记录**，而 `\\b` 会让**契约关键词变成不可检索**
   （实测：策划窗里 `value_source` grep = **0 命中**，`alue_source` = **1**）⚠️
   ⇒ 📌 **同族：纪律 BC「静默改错」**（不报错、grep 也不一定命中）✓

为什么需要一条**自有**门禁（不是自建癖）
--------------------------------------------------
**编译器挡不住 `.txt` 里的 `\\v`** —— **没有任何现成工具会喊** ✓
（与架构上周"能挡就别自建"不冲突：那条针对的是**编译器本来就能报**的错）✓

用法
------
    python tools/dsh/check_control_chars.py            # 报告全部命中 ⇒ 有 ⇒ exit 1
    python tools/dsh/check_control_chars.py --list     # 同（显式）
    python tools/dsh/check_control_chars.py --selfcheck  # 注入探针自检（正/反向）

退出码：0 = 干净；1 = 有 C0 控制字符（**契约载体受损**）；2 = 用法/环境错 ✓
"""

from __future__ import annotations

import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# 🔴 允许的控制字符：**TAB(0x09) · LF(0x0A) · CR(0x0D)** —— 它们是排版，不是损坏 ✓
ALLOWED = {0x09, 0x0A, 0x0D}

# 扫描面：**契约载体**（窗口是人与人的接口；契约文档是数据的接口）✓
ROOTS = [
    os.path.join(REPO, "doc"),
    os.path.join(REPO, "skills"),
    os.path.join(REPO, "reports"),
    os.path.join(REPO, "tools"),
]
EXTS = (".txt", ".md", ".py", ".ps1", ".cs", ".json", ".csproj", ".sln")

# 🔴 `reports/unity_ref/_probe/**` 是**探测脚本的 scratch**，会故意构造控制字符
#    ⇒ 按**路径级白名单**放行（与 `check_data_discipline` 的 46 条同形）✓
# 🔴 `reports/archive/**` 是**历史归档**（当天的快照副本）—— 它们**保留损坏原样**是有意的：
#    归档的价值就是"当时是什么样" ⇒ ⚠️ **改归档 = 篡改历史**（纪律 AR 的精神）✓
#    ⇒ ✅ 归档**放行**，但在报告里**单列一行读数**（不是不报，是**分开报**）✓
SKIP_DIRS = {
    os.path.join(REPO, "reports", "unity_ref", "_probe"),
    os.path.join(REPO, "reports", "archive"),
}

# C 转义 → 它会吃掉的那个字符（让报告能说"本该是什么"）✓
ESCAPE_HINT = {
    0x07: "\\a（BEL）—— 多半是 `\\a`+字母，如 `\\award` → `ward`",
    0x08: "\\b（BS）—— 多半是 `\\b`+字母，如 `\\battle` → `attle`",
    0x0B: "\\v（VT）—— 多半是 `\\v`+字母，如 `\\vestal` → `estal`",
    0x0C: "\\f（FF）—— 多半是 `\\f`+字母，如 `\\first` → `irst`",
    0x00: "\\0（NUL）—— 多半是 `\\0`+字符",
    0x1B: "\\e（ESC）",
}


def iter_files():
    for root in ROOTS:
        if not os.path.isdir(root):
            continue
        for dirpath, dirnames, filenames in os.walk(root):
            if any(dirpath.startswith(s) for s in SKIP_DIRS):
                dirnames[:] = []
                continue
            for name in sorted(filenames):
                if name.endswith(EXTS):
                    yield os.path.join(dirpath, name)


def scan_text(text: str):
    """返回 [(lineno, col, code, context)] ✓"""
    hits = []
    line = 1
    col = 0
    start = 0
    for idx, ch in enumerate(text):
        o = ord(ch)
        if ch == "\n":
            line += 1
            col = 0
            start = idx + 1
            continue
        col += 1
        if o < 0x20 and o not in ALLOWED:
            ctx = text[start:idx].strip()[-40:] + "[[" + hex(o) + "]]" + text[idx + 1:idx + 41].strip()
            hits.append((line, col, o, ctx))
    return hits


def decode_bytes(raw: bytes):
    """🔴 **先认 BOM 再解码** —— 否则 UTF-16 文件会被当 UTF-8 读成一堆 NUL 假报 ✓
    （我第一版就踩了这个：`rerun_*.txt` 报 3242 处"0x00"，**实际是 UTF-16-LE 的 ASCII 高位** ⚠️
      ⇒ 纪律 BH「宽松/严格模式」的实例：**换个解码器，数就变了 ⇒ 说明我读错了**）✓
    """
    if raw.startswith(b"\xff\xfe"):
        return raw.decode("utf-16", errors="replace"), "utf-16"
    if raw.startswith(b"\xfe\xff"):
        return raw.decode("utf-16", errors="replace"), "utf-16-be"
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw.decode("utf-8-sig", errors="replace"), "utf-8-bom"
    return raw.decode("utf-8", errors="replace"), "utf-8"


def scan_file(path: str):
    raw = open(path, "rb").read()
    text, _enc = decode_bytes(raw)
    return scan_text(text)


def report(paths_with_hits) -> int:
    total_files = 0
    total_hits = 0
    for path, hits in paths_with_hits:
        total_files += 1
        total_hits += len(hits)
        rel = os.path.relpath(path, REPO)
        print(f"[c0] 🔴 {rel}: {len(hits)} 处 C0 控制字符")
        for line, col, code, ctx in hits[:8]:
            hint = ESCAPE_HINT.get(code, f"0x{code:02X}")
            print(f"[c0]      L{line} col{col} 0x{code:02X}  {ctx}")
            print(f"[c0]         ⇒ 形状：{hint}")
        if len(hits) > 8:
            print(f"[c0]      … 另有 {len(hits) - 8} 处（同类）")
    if total_hits:
        print(f"[c0] RESULT: 🔴 FAIL —— {total_hits} 处 C0 控制字符 分布在 {total_files} 个文件 ✓")
        print("[c0] 判据：**契约载体里的控制字符是【缺陷】，不是排版问题**"
              "（`\\n` 会拆表行 · `\\b` 会让关键词不可检索）✓")
        print("[c0] 修法：把最早的 `\\`+字母 写法改成【转义或全角】**不要**让写入路径去解释它 ✓")
        return 1
    print(f"[c0] RESULT: OK —— 扫描 {len(list(iter_files()))} 个契约载体文件，0 处 C0 控制字符 ✓")
    return 0


def selfcheck() -> int:
    """双向探针：① 注入坏字符 ⇒ 必须被抓 ② 合法 TAB/CRLF ⇒ 不许误报 ✓"""
    probe_bad = "ok\vbad\bbad\fbad\x00bad\n"
    hits_bad = scan_text(probe_bad)
    probe_good = "ok\ttab\r\ncrlf\r\n" + "正文\n"
    hits_good = scan_text(probe_good)
    print(f"[c0:selfcheck] ① 注入坏字符 ⇒ 命中 {len(hits_bad)} 处（期望 4）")
    print(f"[c0:selfcheck] ② 合法 TAB/CR/LF ⇒ 命中 {len(hits_good)} 处（期望 0）")

    # ③ 🔴 **UTF-16 不许被误报**（我第一版就栽在这 ⇒ 反向探针必须覆盖它）✓
    utf16 = "标题：中文内容\nok\n".encode("utf-16")
    t16, enc = decode_bytes(utf16)
    hits16 = scan_text(t16)
    print(f"[c0:selfcheck] ③ UTF-16 文件（BOM）⇒ 解码为 {enc} · 命中 {len(hits16)} 处（期望 0）")

    ok = len(hits_bad) == 4 and len(hits_good) == 0 and len(hits16) == 0 and enc == "utf-16"
    print(f"[c0:selfcheck] {'PASS' if ok else 'FAIL'}（intact={ok}）")
    return 0 if ok else 1


def main() -> int:
    if "--selfcheck" in sys.argv:
        return selfcheck()

    paths_with_hits = []
    for path in iter_files():
        hits = scan_file(path)
        if hits:
            paths_with_hits.append((path, hits))
    return report(paths_with_hits)


if __name__ == "__main__":
    raise SystemExit(main())
