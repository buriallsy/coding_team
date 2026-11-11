# L5 花庭经营｜营业限时与日结

2026-11-11 · 续做 `05_Skywell_FlowerMarket.unity`。营业到时或主动收摊都先结算当晚，再由玩家开始次日。经营入口仍在原 L5 签收后的花庭小铺。 [花庭副本](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity) · [上轮双窗与快灶记录](Skywell_Flower_Market_Kitchen.md)

本页记录中央塔改线前的花庭布局。当前小铺位置和采集路线见中央塔记录。 [中央塔小铺与山头采集](Skywell_Tower_Market.md)

## 营业时间与操作

| 黄昏选择 | 采集与归店 | 营业默认时间 |
| --- | --- | ---: |
| 直接回店 | 从庭院走回小铺，按 `E` 进入柜台 | 120 秒 |
| 先去夜探 | 夜探最多 45 秒，可提前归店。到时停止采集，仍可在夜色中步行归店 | 60 秒 |

- 白天采集仍是 75 秒。黄昏选择和归店路程不占营业时间。去夜探后提前回店，营业仍是 60 秒。
- 进入柜台后可先看菜谱或按 `U` 升级。首次成功按 `E` 接单才开始计时；食材不足、接不到订单时仍可按 `N` 收摊。`1`／`2` 继续对应各窗口下锅或交餐。
- 柜台和庭院状态栏都显示剩余营业时间。`Q`／`Esc` 收起柜台后可走动，计时继续。需要继续下锅或交餐时，回柜台按 `E` 重开面板。
- 时间归零立即关闭订单并退料。主动按 `N` 也进入同一个日结。日结时仍是夜间，下一天的采集时钟要等玩家确认后才开始。

## 日结显示与跨日

日结显示第几天、收摊原因、当晚营业额、钱箱余额、累计营业额、退回的食材和快灶状态。下锅前的订单只取消预留；已下锅但未交餐的订单按实际使用的品种退料，含到时前刚好还在烹饪或已做好未送出的菜。收入按实际交餐记录。购买快灶减少钱箱余额，累计与当晚营业额只计收入。

在日结再次按 `N` 或点击进入次日。次日恢复白天并开始新一轮采集，余额、剩余食材、累计营业额、快灶和既有机关、签收进度结转。`R` 继续走原关卡完整重开，清空经营并回原入口。 [经营规则](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/FlowerMarketRun.cs) · [柜台接线](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/SkywellFlowerMarket.cs) · [HUD](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellHUD.cs)

## Inspector 试值

退出 Play Mode，在花庭副本的 Hierarchy 选中 `Flower_market_prototype`，在 Inspector 的 **Skywell Flower Market → Trading** 修改以下两项。

| Inspector 字段 | 默认值 | 对应路径 |
| --- | ---: | --- |
| `Direct Trading Seconds` | 120 秒 | 黄昏选择直接回店 |
| `Night Trading Seconds` | 60 秒 | 黄昏选择夜探后回店 |

这两项沿用同学 Unity 分支 `26b4488` 的营业试值。现有花庭副本继续使用 75／45 秒的采集节奏、两道菜、真实日照与本地雨。双窗和快灶的调参仍见上一轮记录。 [同学分支的规则与数值](https://github.com/buriallsy/coding_team/blob/26b44884532477593adaf5dea7809e7e49e419ec/docs/06_MarketModules/ARCHITECTURE.md#数值与本轮决定)

## 本轮实测记录

Unity 6000.5.4f1 的 macOS 前台 Game View 连续走完原入口、签收与两条黄昏路线。**393 项回放检查**和 **138 项 EditMode 测试**通过，其中 21 项花庭规则。五个场景、一个预制体和 **12,358 个组件**通过保存引用检查。

| 实测 | 结果 |
| --- | --- |
| 直接回店，120 秒 | 首次成功接单开始计时。按 Q 收起柜台，走开再回来，时钟继续且没有重开。第 1 天三单营业额 46 元，按 N 主动收摊，无退料。第二次按 N 才开始第 2 天 |
| 夜探后回店，60 秒 | 回店先花 36 元升级快灶，再接单。采样时剩 59.19 秒，此后游戏时钟走了 59.22 秒进入日结。截止前关掉柜台，倒计时自然归零，没有再按收摊键 |
| 双灶自动退料 | 截止时一灶仍在做菌菇便当，另一灶的果香菌菇盒已出餐未交。自动收摊退 **菌菇 ×3、雨夜果 ×1**，未交餐的两道菜均未收费 |
| 日结与次日 | 夜间日结显示本晚营业额 **12 元**、钱箱 **22 元**、累计营业额 **58 元**及快灶。按 N 天亮，结转余额、库存和升级。第 3 天未接单也可主动收摊。从日结按 R 恢复入口和日光，经营进度清空 |

复跑时打开当时的花庭副本，进入 Play Mode，选 **Borrowing Sun → Skywell → Check complete Flower Market route** 并保持 Unity 前台。实测覆盖编辑器回放与规则测试。原 L5 场景、摆渡与 Build Settings 逐字节一致。

[完整回放](../evidence/flower-market-settlement/playmode.txt) · [EditMode](../evidence/flower-market-settlement/editmode.xml) · [保存引用](../evidence/flower-market-settlement/references.txt)

[主动收摊日结](../evidence/flower-market-settlement/flower-market-direct-summary.png) · [到点前两灶](../evidence/flower-market-settlement/flower-market-trading-deadline.png) · [自动日结与退料](../evidence/flower-market-settlement/flower-market-timeout-summary.png) · [次日黎明](../evidence/flower-market-settlement/flower-market-dawn-after-night.png)
