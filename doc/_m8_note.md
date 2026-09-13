
---

### 【来自主程序】M8.0 一(c) 接线完成 加 A1 复测（全部达标）

- 接线：DirectorBridge.BuildFromRes(host, sortie, growth) 传名册出征 6 人则套阵型模板，并按槽位顺序把等级投影作用到我方单位；BattleDirector 仍单场纯（名册由组合根读、以快照传入）
- 配人策略：FormationSortie.SelectForTemplate 按模板槽位原型从名册配人（每位只用一次）
  保住经典编成（tank warrior commissar medic warrior medic）同时个体化（等级与特质各不相同）
- 冒烟实测出征名单：石墙(tank Lv2)、老铁(warrior Lv2)、血徽(commissar Lv2)、白手(medic Lv2)、灰手(warrior Lv1)、麻布(medic Lv1)

- A1 复测（#286 四 的要求；单场新队口径）：**胜率 100% ／ 死门 0.33 ／ 阵亡 0.09 ／ 回合 5.36**
  门槛 大于等于85% ／ 小于等于0.4 ／ 小于等于0.1 ／ 4到6：**全部达标**
  回合数与改前的 5.36 一致：编成来源改变后 A1 不变，因为配人策略保住了同原型编成（个体化只改等级与特质）
- 端到端冒烟 EXIT=0 无 ERROR；全量 341 项 / 341 通过
- M8.0 一（英雄个体化）至此完成：(a) 名册数据加 P22 ／ (b) 等级与特质投影 ／ (c) 阵型模板化加名册出征加 A1 复测
- 下一步按 #283 顺序：二 金钱（来源与光照档加战斗数挂钩）
