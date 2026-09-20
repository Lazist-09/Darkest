# `reports/recovered/` —— 事故恢复件（**不是源码，供人工复核后回填**）

> 🔴 **2026-09-20 事故**：主程序为执行 P0-1 拆分，脚本半途失败后误用
> `git checkout -- darkest/scripts/gameplay/sim/run/ExpeditionFlow.cs`
> ⇒ 该文件的**未提交改动（1185 行版本）**被还原成 HEAD 的 **579 行** ⇒ 丢失约 **606 行**。
> 抢救（备份 / 编辑器历史 / stash / fsck / 全盘搜索）**全部失败**；
> 唯一可行路径 = **事故前的编译产物里还留着它** ✓

## 恢复件来源（可复核）

```
源：darkest/.godot/mono/temp/bin/Debug/Darkest.dll（构建时间 09-20 15:15，晚于工作区最后状态 ⇒ 含丢失代码）
工具：ilspycmd 11.0（dotnet tool install -g ilspycmd）
命令：ilspycmd --type Darkest.Gameplay.Sim.Run.ExpeditionFlow -o reports/recovered <dll>
产物：Darkest.Gameplay.Sim.Run.ExpeditionFlow.decompiled.cs（1141 行）
命中：丢失的 10 个符号 10/10
      RevealSecretsWithinRooms · ResolveHunger · BindTraps · ResolveLandingTrap · EnableTileWalk ·
      TrapResistSourceDeclared · HungerBuffer · CanEatForHunger · TryStepTile · BacktrackExtraFor
```

## ⚠️ 能救回什么 / 救不回什么

```
✅ 能：方法体逻辑 · 方法签名 · 字段（在类头）· 控制流 · 常量
❌ 不能：注释与 XML 文档（DLL 不含；Darkest.xml 不存在）· 原始格式/空行 · 可能的 #if 分支
        · 局部变量名（反编译后多为 num/text 等）· 字段声明顺序
⇒ 📌 所以不能直接替换源码：必须由作者（或作者授权）把缺失区域逐块回填到 ExpeditionFlow.cs，并补齐注释 ✓
```

## 回填建议（给作者）

```
① 对照本目录的 .decompiled.cs 与 HEAD 版 ExpeditionFlow.cs
② 按方法名逐块回填（事故前的精确行号 + 函数清单见 doc/windows/架构窗口.txt 的 INCIDENT-LEAD-CHECKOUT-LOSS）
③ 回填后：dotnet build 绿 + 全量测试同一个数；作者自己的冒烟/读数复跑
④ 🔴 本目录是恢复件：回填完成、复核通过后请删除本目录（避免「两处真值」）✓
```
