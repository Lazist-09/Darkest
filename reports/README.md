# `reports/` —— 命名规则 · 哪份权威 · 保留策略

> 🔴 **为什么有这份**：策划 `#405`（外部评审）——「**没有"最新指针"也没有清理机制，三个月后没人翻得动**」⚠️
> 当时实况：**96 个 `.txt`**、无 `latest/`、无 README，且 **13:09 与 13:12 两轮近乎同质** 并排躺着 ✓

## 1. 命名规则（由 `tools/dsh/smoke.ps1` 产出）

| 文件名 | 含义 |
|---|---|
| `smoke_<case>_<stamp>.txt` | **逐例日志**（`<case>` = 用例名 · `<stamp>` = `yyyyMMdd_HHmm`） |
| `smoke_summary_<stamp>.txt` | **本轮摘要** —— 🔴 **权威**：它的 `RESULT: OK` / `RESULT: BAD n=N` 行**就是 CI 的判据输入** ✓ |
| `LATEST.md` | **人读指针**：本轮是哪个 `stamp`、哪份是权威 ✓ |
| `latest/` | **本轮文件的副本**（永远指向**最近一轮** ✓） |
| `archive/<stamp>/` | **旧轮次归档**（保留策略把旧文件**移**到这里，**不删除** ✓） |

## 2. 哪份权威（做题时只看这两行）

```
① 人看：`reports/LATEST.md`（或直接看 `reports/latest/`）
② 机器看：`reports/smoke_summary_<stamp>.txt` 里的 `RESULT:` 行（CI 只信它 ✓ —— 见 tools/dsh/README.md §4）
```

## 3. 保留策略（`tools/dsh/reports_hygiene.ps1` · **不需要 Godot**）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/reports_hygiene.ps1
powershell ... -File tools/dsh/reports_hygiene.ps1 -KeepPerPrefix 2   # 每个前缀留 2 份
powershell ... -File tools/dsh/reports_hygiene.ps1 -DryRun            # 只看会动谁
```

```
① `latest/` 重建为**最近一轮**的副本 ＋ 刷新 `LATEST.md` ✓
② **每个 `<前缀>` 只留最新 N 份**（默认 1）⇒ 多余的**移**到 `archive/<stamp>/`（**不删** ✓）
③ 可 `-DryRun` 先看效果 ✓
```

## 4. 🔴 口径（避免误读）

```
· **旧口径**：早期留档里的 `非环境ERROR=N` **含引擎退出泄漏** ⇒ 引用时必须写明"旧口径"（归档纪律：**不重写历史** ✓）
· **新口径**：`真错误` 与 `引擎退出泄漏` **分两列**，只有**真错误**判红（见 `tools/dsh/README.md` §3 ✓）
· 归档在 `archive/` 里的文件**仍然有效**，只是不在"本轮"位置 ✓
```
