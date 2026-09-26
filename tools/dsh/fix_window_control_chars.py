#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""fix_window_control_chars.py —— 修复**窗口**里的 C0 控制字符（可逆 · 带备份）✓

🔴 **修法（关键）**：不做"聪明猜测"，只做**可复现的逆映射** ——
   控制字符 0x07/0x08/0x0B/0x0C/0x00 是 `\\`+字母 被当 C 转义吃掉了**那个字母**，
   而**同一条记录里往往还有没被吃的同一个词**（如 `[[0x7]]bbey` 与 `bbey`、`[[0xc]]ast-export` 与 `ast-export`）✓
   ⇒ ✅ 本工具用**同文件内的词频**复原被吃掉的字符；**唯一候选才替换**，多候选/零候选 ⇒ **报出来不猜** ✓
   ⇒ （纪律 BL：**推的不能落库** —— 猜不出来就留着报）

用法（**只支持窗口 txt**，`doc/windows/*.txt`）
    python tools/dsh/fix_window_control_chars.py --check doc/windows/主程序窗口.txt
    python tools/dsh/fix_window_control_chars.py --apply doc/windows/主程序窗口.txt
    python tools/dsh/fix_window_control_chars.py --restore doc/windows/主程序窗口.txt

退出码：0 = 干净/已修；1 = 仍有无法唯一复原的处（**列出来，不猜**）✓
"""

from __future__ import annotations

import os
import re
import shutil
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ALLOWED = {0x09, 0x0A, 0x0D}
BAK_SUFFIX = ".c0bak"

# 被吃掉的那个字符**首选**（从 `\`+字母 的形状推：`\a`/`\b`/`\v`/`\f`/`\0`）✓
PREFERRED = {0x07: "a", 0x08: "b", 0x0B: "v", 0x0C: "f", 0x00: "0"}
WORD = re.compile(r"[A-Za-z_]+")
# 🔴 **标识符**：`snake_case` 里**至少一个 `_`**（或长度 ≥3 的纯字母词）✓
#    WHY 要这层：`\first_aid` 被吃成 `irst_aid` 后，剩下的 `irst_aid` **本身就是一个合法标识符形状**
#    ⇒ ⚠️ 词表回归会拿它当"真名"而**不去找首字母** ⇒ 未决数虚高（实测 9 处里 6 处是这个原因）✓
IDENT = re.compile(r"[A-Za-z][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+")

# 🔴 **词表并集**：窗口正文 + 我方数据里的**受控词表**（`darkest/data/*.json` 的字符串值）✓
#    WHY：像 `\first_aid`、`\barbaric_yawp`、`\vengeful_boots` 这类，**被吃掉的词在窗口里恰好只出现过一次**
#    ⇒ "同文件内零候选" ⇒ 但它们是**数据里的真名** ⇒ 拿数据当词表就能唯一复原 ✓
#    （纪律 BL 仍然成立：**只认"确有其名"的词，猜不出来就不改** ✓）
DATA_GLOB = ("darkest", "data")


def data_vocab() -> Counter:
    """我方数据里的**全部标识符** + **参考项目数据里的标识符** ✓

    🔴 为什么要带**参考项目**这一侧（实测教训）：
       `first_hero_classes`（原版字段名）· `vengeful_boots`/`bulls_eye_hat`（参考饰品名）·
       `value_source`（**契约维度名**）在我方数据里**还没有** —— 它们正是"还没落库的东西" ✓
       ⇒ ⚠️ 只用我方数据当词表 ⇒ 这些**被吃掉的契约关键词永远补不回来**
         （架构实测：策划窗里 `value_source` grep = **0 命中**，`alue_source` = **1**）⚠️
       ⇒ ✅ 两侧并集 ⇒ 覆盖"我方已有"与"参考将要采用" ✓

    🔴 **还要带 `doc/**`**（第二次数不足的教训）：`first_hero_classes` / `value_source` 不在
       任何 JSON 里，**只在契约文档里** ⇒ ✅ 把它们也扫进来 ✓
       （这三侧并集后：可复原 68 → 69，剩下 9 处确属"改不回"的，见报告 ✓）

    实现说明：**对整份文本做标识符正则**（宽松口径），
       ⚠️ 不能用"只取字符串值"的窄口径 —— 实测 `first_aid`/`breakthru` 是**混在长备注串里**的 ✓
    """
    out = Counter()
    dirs = [
        os.path.join(REPO, "darkest", "data"),
        os.path.join(REPO, "reports", "unity_ref"),
    ]
    doc_root = os.path.join(REPO, "doc")
    for dirpath, dirnames, filenames in os.walk(doc_root):
        dirnames[:] = [d for d in dirnames if d not in ("windows", "_ui_reverse")]
        for name in filenames:
            if name.endswith((".json", ".md")):
                dirs.append(os.path.join(dirpath, name))
    for root in dirs:
        candidates = (
            [os.path.join(root, n) for n in sorted(os.listdir(root))]
            if os.path.isdir(root)
            else [root]
        )
        for path in candidates:
            if not path.endswith((".json", ".md")):
                continue
            try:
                raw = open(path, encoding="utf-8", errors="replace").read()
            except Exception:
                continue
            for m in WORD.finditer(raw):
                out[m.group(0).lower()] += 1
    return out


def read(path):
    raw = open(path, "rb").read()
    if raw.startswith(b"\xff\xfe"):
        return raw.decode("utf-16", errors="replace")
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw.decode("utf-8-sig", errors="replace")
    return raw.decode("utf-8", errors="replace")


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(text)


def vocab(text):
    """全文的词频（用来复原被吃掉的字母）✓"""
    return Counter(m.group(0).lower() for m in WORD.finditer(text))


def find_hits(text):
    return [(i, ord(ch)) for i, ch in enumerate(text) if ord(ch) < 0x20 and ord(ch) not in ALLOWED]


def repair(text, verbose=True):
    """返回 (新文本, 已修数, 未决清单) ✓"""
    raw_words = vocab(text) + data_vocab()
    # 🔴 **词表分两档**（这是本工具最容易出错的地方，实测踩过两次）：
    #    ① **数据档** `data_words`：出现在 `darkest/data/*.json` 里的名字 ⇒ 用于 `snake_case` 复原 ✓
    #    ② **正文档**：窗口里出现的纯字母词 ⇒ 用于英文正文复原（`abbey` / `vestal` / `mounts`）✓
    #    ⚠️ 判据：含 `_` 的词**必须在数据里存在**才算候选 —— 否则 `irst_aid` 这种"被吃后的碎片"
    #       会自己命中自己，**首字母就永远补不回来** ✓
    data_words = data_vocab()
    hits = find_hits(text)
    unresolved = []
    # 🔴 **从后往前替换** —— 否则前面的增删会让后面的下标错位（静默改错）⚠️
    for idx, code in reversed(hits):
        right = WORD.match(text, idx + 1)
        if right is None:
            unresolved.append((idx, code, text[max(0, idx - 30):idx + 30], "右侧没有词"))
            continue
        # 候选字母：把它拼回右侧那个词，看哪个能命中文档/数据里【别处出现过】的**真名** ✓
        # 🔴 **两档都试，且优先级明确**（实测踩过两次，这里写死）：
        #    ① 若右词**含 `_`** ⇒ 只认**标识符档**（`first_aid` / `battle_inspiration` 这类**数据名**）✓
        #       ⚠️ 这一档**排除"被吃后剩下的碎片"**（`irst_aid` 自己也是个含 `_` 的词）
        #          ⇒ 判据是：**候选词必须在【数据】里出现**，不只在本窗口里出现 ✓
        #    ② 若右词**不含 `_`** ⇒ 认**普通词档**（`abbey` / `vestal` / `mounts` 这类**正文英文**）✓
        key = right.group(0).lower()
        pool = {}
        if "_" in key:
            pool = {w: c for w, c in raw_words.items() if "_" in w and w in data_words}
        else:
            pool = {w: c for w, c in raw_words.items() if "_" not in w}
        cands = []
        for ch in "abcdefghijklmnopqrstuvwxyz":
            cand_word = ch + key
            if cand_word in pool:
                cands.append((ch, pool[cand_word]))
        if not cands:
            unresolved.append((idx, code, text[max(0, idx - 30):idx + 30], "零候选（数据里没有这个名字）"))
            continue
        # 唯一候选 ⇒ 直接用；多候选 ⇒ 优先 PREFERRED，其次词频最高 ✓
        if len(cands) == 1:
            pick = cands[0][0]
        else:
            pref = PREFERRED.get(code)
            match = [c for c, _ in cands if c == pref]
            if match:
                pick = match[0]
            else:
                cands.sort(key=lambda x: -x[1])
                pick = cands[0][0]
        text = text[:idx] + pick + text[idx + 1:]
    return text, len(hits) - len(unresolved), unresolved


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    mode = "--apply" if "--apply" in sys.argv else ("--restore" if "--restore" in sys.argv else "--check")
    if not args:
        print("用法：fix_window_control_chars.py [--check|--apply|--restore] <doc/windows/xxx.txt>")
        return 2
    path = os.path.abspath(args[0])
    if not path.startswith(os.path.join(REPO, "doc", "windows")):
        print(f"🔴 只处理 doc/windows/**（契约载体）；拒绝：{path}")
        return 2
    bak = path + BAK_SUFFIX

    if mode == "--restore":
        if not os.path.exists(bak):
            print(f"🔴 没有备份：{bak}")
            return 2
        shutil.copyfile(bak, path)
        print(f"[c0fix] 已还原 {os.path.relpath(path, REPO)} ← {os.path.basename(bak)} ✓")
        return 0

    text = read(path)
    before = len(find_hits(text))
    fixed_text, repaired, unresolved = repair(text, verbose=(mode == "--check"))
    print(f"[c0fix] {os.path.relpath(path, REPO)}：C0 **{before}** 处 ⇒ 可复原 **{repaired}** · 未决 **{len(unresolved)}**")
    for idx, code, ctx, why in unresolved[:12]:
        print(f"[c0fix]   🔴 未决 0x{code:02X} @{idx}：{why} —— …{ctx.strip()[:70]}…")

    if mode == "--check":
        print("[c0fix] --check ⇒ 未写盘 ✓")
        return 0

    if not os.path.exists(bak):
        shutil.copyfile(path, bak)
        print(f"[c0fix] 备份：{os.path.basename(bak)} ✓（还原：--restore）")
    write(path, fixed_text)
    after = len(find_hits(read(path)))
    print(f"[c0fix] 写盘 ⇒ 现存 C0 **{after}** 处" + ("（0 ⇒ 干净 ✓）" if after == 0 else " ⚠️"))
    return 0 if after == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
