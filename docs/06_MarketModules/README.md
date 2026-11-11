# Unity 花庭经营模块

这轮在 Unity 内重新实现经营模块。Godot 2D 只提供玩法验证结果，没有复制其源码、引用其 DLL 或把 Godot 场景接入 Unity。

模块在 `5b83325204060e1312c46f36f61e5489f6a9cf70` 的花庭白盒上继续开发。原 `05_Skywell.unity` 和 `05_Skywell_FlowerMarket.unity` 保留；新入口为独立的 `05_Skywell_MarketModules.unity`。原跨谷路径与 `SkywellVoyage` 不属于本轮改动范围。

## 打开与试玩

1. 用 Unity Hub 打开 `unity_prototype/BorrowingSun`，编辑器版本 **6000.5.4f1**。等待编译和资产导入完成。
2. 打开 [05_Skywell_MarketModules.unity](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_MarketModules.unity)，或使用菜单 **Borrowing Sun → Market Modules → Open playable scene**。
3. 按 Play，完成原 L5 的取桨、晴令、修镜、送光与花庭签收。签收本次输入不会同时启动经营；走到花庭西北侧小铺，再短按 E 开始第 1 天。
4. 白天沿花庭原白盒的安全路线采菌菇或环境果实。施雨仍使用 Tab 选雨、长按 E 瞄准并点击花庭雨盆；采集用近处短按 E。
5. 黄昏选择直接回店或夜采。探索到时后仍可安全走回店，抵达店铺才配置菜单。菜单确认后才出现顾客订单。
6. 开店前勾选菜谱并确认，至少一道须有食材；营业中按 `1` / `2` 对应两个窗口分别投料，完成后再按同一键交餐，也可点界面按钮。两个窗口可同时烹饪。收入进入钱包；营业结束显示日结，次日保留库存、收入、升级和世界进度。
7. 在菜单阶段可购买厨房升级。R 仍为整局重开，日结不触发原关卡重置。

玩法数值、职责边界和测试入口见 [架构与数值](ARCHITECTURE.md)。当前核验范围和待做的 Unity 操作见 [验证状态](VALIDATION.md)。

## 三个场景的用途

| 场景 | 用途 |
| --- | --- |
| `05_Skywell.unity` | 原 L5 教学与跨谷交付回归 |
| `05_Skywell_FlowerMarket.unity` | 组员原花庭小范围白盒回归：两菜、单灶、收款、次日 |
| `05_Skywell_MarketModules.unity` | 本轮独立模块：阶段、双窗、库存预留、真实夜间、日结和厨房升级 |

新副本仍引用项目的共享 Controller、天气与光学脚本，因此“原场景文件没有修改”不等于自动证明原场景运行没有回归。新模块以可选接点接入；原场景没有新模块时走原逻辑，实际回归仍须执行。

## 重新生成与检查副本

菜单 **Borrowing Sun → Market Modules → Create copy from Flower Market** 只在目标场景不存在时创建新副本。目标已存在会拒绝覆盖。生成器保留原场景、原场景元数据和原运输脚本，不修改项目启动场景列表。

菜单 **Borrowing Sun → Market Modules → Validate open scene bindings** 检查当前新场景中的组件、点位和天气引用。它只验证接线，不能代替 Play Mode、真实输入或真人试玩。

## 这次没有包含

没有新增美术模块、顾客耐心失败、固定收入通关期限、磁盘存档或双向跨谷。经营进度只在当前运行会话内跨日保留。全地图资源扩展和往返运输需另立验证范围。
