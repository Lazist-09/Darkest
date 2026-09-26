# merge 冲突事故 · 主程序侧记录（2026-09-22）

## 现场（当场取真值）
· 检出时点：R91 打「prot 顶替」时
· MERGE_HEAD = c53ff458699334e111462b6901c6d87eb3d0b4f0
· MERGE_MSG = "Merge branch 'master' of https://github.com/Lazist-09/Darkest"
· 冲突 **58 个**（UU 31 · AA 26）+ 已暂存 13 + 改动 2 + 未跟踪 1 = **74 项在飞**
· 冲突面：.gitignore · darkest/data/buff_defs.json · darkest/data/unlocks.json · **26 个 UI .tscn（AA=双方各自新增）** · scripts/data/BuffDefsConfig.cs …
· 构建：**396 个 CS8300「遇到合并冲突标记」** ⇒ **整棵树当前不可构建** ✗

## 我这边（**未提交，刻意**）
· R91 prot 顶替 已改好并取到读数：
  · 改前：4 原型顶层 prot = 8/12/4/5 ⇒ 减伤 **21% / 29% / 12% / 14%**
  · 改后：prot = 0/0/0/0 ⇒ 减伤 **0%**（敌人 melee_soldier/ranged_archer/caster 未动：**参考项目里没有对应英雄** ✓）
  · 依据：参考项目 armour[].prot 逐阶 **0** ✓ **且** 策划一手 E 盘 reading 亦为 0 ✓ ⇒ **两源一致、无冲突** ✓
· 存盘：Darkest-backup-20260921_003105-expflow\units.after-protzero.json（改后）／units.before-protzero.json（改前）✓
· 🔴 **我没有提交**：merge 进行中提交 = 提交冲突标记（或生成 merge commit）**不是我的决定** ✓

## 建议（等裁定）
① 由**发起 merge 的人**处置（他知道要合哪一边）✓
② 或授权我在**隔离 worktree**（F:\GithubPro\Darkest.verify）里先落我的改动并验证 ⇒ 主树不受影响 ✓
③ git merge --abort 可回到"我最后一次全绿"的状态（远端提交仍在仓库里，不会丢 ✓）—— ⚠️ 但会**丢掉发起人已做的解决进度** ⇒ 我不擅自动手 ✓
