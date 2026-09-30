---

### 信封：2026-09-26 · 【来自策划】✅ **`F:\GithubPro` 下那 7 份备份我清理了（释放 7.36 GB）** —— 删前逐份取证，独有内容已救出

> 🔴 **送达判据**：`Select-String -Path doc\windows\主程序窗口.txt -Pattern 'DELIVERY-DESIGNER-BACKUP-CLEANUP-20260926' -SimpleMatch` 命中即送达。
> 🔴 **同时投**：`doc\windows\架构窗口.txt`（同标记）· **归档副本**：`reports/planner_20260926_to_lead_backup_cleanup.md`
> 📌 **为什么必须告诉你们**：那是**你们的备份**（起因 = 架构的命令「事故响应(O-98)…**全员备份提示**」；主程序的交接文档把它写成流程「改前备份、改后复验」）⚠️

```
DELIVERY-DESIGNER-BACKUP-CLEANUP-20260926
```

#### ✅ ① 删了什么（用户裁「处理掉吧」）

```
REMOVED Darkest.verify                           freed   69.2 MB
REMOVED Darkest-backup-20260921_000640           freed  906.8 MB
REMOVED Darkest-backup-20260921_003105-expflow   freed    0.4 MB
REMOVED Darkest-backup-20260921_011818           freed  908.0 MB
REMOVED Darkest-backup-20260921_124311           freed  907.9 MB
REMOVED Darkest-backup-20260921_124836           freed  907.9 MB
REMOVED Darkest-backup-20260921_235637           freed 3835.8 MB
                                         合计 = 7.36 GB
🔴 **没动**：`F:\GithubPro\Darkest`（活仓库）· `Darkest-Dungeon-Unity`（参考项目 · 第三方出处，工具在读它）
```

#### 🔴 ② 删前两条取证（**这就是"凭什么能删"**）

```
🛠️ 工具：`tools/dsh/audit_backup_uniqueness.ps1`（只读）→ `reports/backup_uniqueness_20260926.md`
① **提交零缺失**：6 个副本的 refs **全部命中活仓库的 object store**（`git cat-file --batch-check`，缺 **0** 个）
   ⇒ 📌 **git 历史一份都不会丢** ✅
② **独有内容已救出**：对每份"相对它自己 HEAD 有改动/未跟踪"的文件（每份 9~253 个）逐个算 **git blob sha1**，
   再问活仓库"这个内容在不在你的 object store 里" ⇒ 🔴 **186 个文件的内容【别处没有】**
   ⇒ ✅ **已全部救出**到 `.tools/rescue/backup-20260921/`（**8.22 MB** · 分副本子目录）
   ⇒ ✅ **逐文件复核**：重算 sha1 与清单比对 ⇒ **0 处不符**
   ⇒ 清单（进 git）：`reports/backup_rescue_manifest_20260926.tsv`（190 行）
🎖️ **判据（新立 · 同族纪律 AC/BC）**：
   🔴 **「删一个仓库副本之前，先证两件：① 提交 0 缺 ② 独有内容已救出 —— 两条都证不出 ⇒ 不许删」** ✅
```

#### 📌 ③ 救出来的是什么（你们可能想认领）

```
· `Darkest.verify`（20 个）· `…_000640`（36）· `…_011818`（34）· `…_124311`（34）· `…_124836`（34）· `…_235637`（9）
  ＋ `…_003105-expflow`（19 —— 它本来就不是仓库副本，是拆分前的 `.before-split`/`.recovered` 快照包）
· 典型内容：`doc/modules/dd1_baseline.md` 与四方 `SKILL.md` 的【中间版本】· `.workbuddy/memory/*.md` ·
  `darkest/tests/{SecretFlow,Revisit,Stealth,GridVision*}.cs` · `BACKUP_INFO.txt`（`…_235637` 那份终检读数）
⇒ 🔴 **注意**：`.tools/` 在 `.gitignore`（**本机有效、不进 git**）—— 与 `doc/_backups/` 同款约定
   ⇒ ⚠️ **残余风险**：那 8.22 MB 目前**只在本机**。若你们要它进 git（+8.22 MB），说一句我就迁到 `doc/_rescue/` ✅
```

#### ✅ ④ 我改了两处【指向已删路径】的文档（加日期注，没动你们正文）

```
· `reports/final_verification.md` **§4 安全网** 之后加了一条 `> 🔴 2026-09-26 更新（策划）`（写清取证与去处）
· `reports/HANDOVER_lead_programmer.md` **§2③ 改前备份** 之后同上
📌 **这两处原本指向 `F:\GithubPro\Darkest-backup-*`（现已不存在）⇒ 不注就会误导后来者** ⚠️
```

#### 📋 ⑤ 对你们流程的影响：**纪律没变，只是"副本用完要清"**

```
✅ **"改前备份、改后复验"照旧**（显式路径 · `-F` 消息文件 · 备份 · 复验）
🆕 **只加一步**：**备份副本用完 ⇒ 按上面那两条取证后清掉**（否则每轮留一份 ~900 MB ⇒ 现在已积 7.36 GB）
   ⇒ 📌 判据：**"这份备份现在还有【只有它有】的东西吗？" 答不出 ⇒ 先取证，再决定删/留** ✅
```

- **阻塞 / 待裁定**：无（一条通知 + 一处请你们确认：那 8.22 MB 要不要进 git）
- **权威在哪**：`doc/state.md` **`#484`** · `reports/backup_uniqueness_20260926.md`（取证）· `reports/backup_rescue_manifest_20260926.tsv`（清单）
