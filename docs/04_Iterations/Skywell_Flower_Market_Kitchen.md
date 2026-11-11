# L5 花庭经营｜双窗与快灶

2026-11-11 · 续做 `05_Skywell_FlowerMarket.unity`。柜台现在要先接客，再为两扇窗口分别下锅和交餐。营业所得可留到下一晚，开单前换快灶。 [花庭副本](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity) · [上轮昼夜回路](Skywell_Flower_Market_Day_Night.md)

## 柜台流程

1. 完成原 L5 跨谷并签收，走到花庭小铺，短按 `E` 开始第 1 天。白天采集 75 秒，黄昏可直接回店，或先夜探 45 秒。沿用局部降雨和真实受光判定，白天可采晴果或雨露果，夜雨可采雨夜果，干夜无果。
2. 回到柜台进入营业界面。当晚接单前，若钱箱至少有 36 元，按 `U` 可换快灶。一局只买一次，两口灶当晚及之后都加快。若尚未攒够，正常接单并留到之后再买。
3. 按 `E` 接单。有材料才分配订单，两个窗口都从同一份可用库存预留。只有足够做一单时，另一扇窗空着。预留的食材仍在库存里，实际下锅才扣除。
4. `1` 和 `2` 分别处理对应窗口。待投料时按键下锅，烹饪中等待，菜好了再按同一键交餐。两窗各有自己的倒计时，可同时烹饪。每份餐交付时只收款一次。
5. `N` 收摊，释放未投料订单的预留，退回已投料但未交餐的原食材，再直接进入下一天。已交餐的收入、剩余库存与快灶跨日保留，签收和机关进度仍在。`R` 按原完整重开逻辑清空经营并返回关卡入口。

例如，仅有两份菌菇可接一份菌菇便当。带回四份菌菇后，两扇窗口可各接一份；先按 `1`，再按 `2`，便于看到两口灶错开开始、各自完成。若只交付一份就收摊，只有该份收入进入钱箱，另一份未交餐的原料退回。

## Inspector 试值

退出 Play Mode，在花庭副本的 Hierarchy 选中 `Flower_market_prototype`，在 Inspector 的 **Skywell Flower Market → Kitchen** 下修改。下列为本轮默认试值，便于分别比较基础灶与快灶的实际等待时间。

| Inspector 字段 | 默认值 | 对应用途 |
| --- | ---: | --- |
| `Kitchen Upgrade Cost` | 36 元 | 一次性快灶价格 |
| `Mushroom Cook Seconds` | 3 秒 | 基础灶的菌菇便当 |
| `Fruit Cook Seconds` | 4 秒 | 基础灶的果香菌菇盒 |
| `Upgraded Mushroom Cook Seconds` | 2.1 秒 | 快灶的菌菇便当 |
| `Upgraded Fruit Cook Seconds` | 2.8 秒 | 快灶的果香菌菇盒 |

菌菇便当仍用菌菇 ×2，售价 12 元。果香菌菇盒仍用菌菇 ×1 和任一果实 ×1，售价 22 元。窗内订单要记住实际用的果实种类，收摊才可退回原品种。6 份菌菇可以做三单便当，刚好攒够 36 元。5 份菌菇加 1 份果实可做两单便当和一单果香盒，共收 46 元。 [菜谱、订单与结转规则](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/FlowerMarketRun.cs) · [柜台输入与参数接线](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/SkywellFlowerMarket.cs)

## 参考与改动边界

同学的独立 Unity 分支 `feature/djwcb2333/unity-market` 在 `26b4488` 已实现双窗口、库存预留、菜单阶段升级、退款和日结。本轮在现有花庭副本采用这些订单与升级规则，继续使用自己的两道菜、75 秒／45 秒采集节奏、实地回店和收摊直接跨日。柜台指令和反馈由现有适配与 HUD 接线，纯经营规则负责两窗、共享库存、钱箱与升级状态。保留原 L5 场景、货运和既有昼夜、光学、天气作为回归基线。 [同学分支提交](https://github.com/buriallsy/coding_team/commit/26b44884532477593adaf5dea7809e7e49e419ec) · [同学分支的数值与职责](https://github.com/buriallsy/coding_team/blob/26b44884532477593adaf5dea7809e7e49e419ec/docs/06_MarketModules/ARCHITECTURE.md) · [HUD 接线](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellHUD.cs)

## 本轮实测记录

在 macOS 的 Unity 6000.5.4f1 前台 Game View，从原入口按键走完取桨、爬塔、借光、发车与花庭签收。继续通过键盘操作柜台，用游戏的指针瞄准集雨盆并施雨，完成直接营业和夜探两条路线。**372 项玩法检查通过**。EditMode **133 项通过**，其中 16 项花庭规则。五个场景及一个预制体的 **12,358 个组件**通过保存引用检查。

| 验收项 | 结果 |
| --- | --- |
| 第 1 天接单、并行和收款 | 采 5 份菌菇、1 份晴果、1 份雨露果。按 E 预留 3 份菌菇和 1 份雨露果，实物库存到下锅才扣。两窗同时烹饪，提前按键无法收款。分别交餐后仅原窗口补单。菌菇便当、果香菌菇盒、再一份菌菇便当，共收 **46 元**。已收款的订单无法重复收费 |
| 第 2 天升级与并行 | 干夜无果，施雨后采到雨夜果。收款后当晚不能插入升级，第 2 天接单前按 U 花 **36 元**，钱箱变 **10 元**，累计营业额仍是 **46 元**。重复按 U 未扣款，两个已配置的烹饪时长都变短 |
| 收摊与第 3 天 | 第 2 天交付便当收 12 元，果香盒做完后未交餐。按 N 退回 **1 份菌菇和 1 份雨夜果**。第 3 天保留 **22 元**余额、**58 元**累计营业额、快灶和原有晴果。两个窗口清空，日光与实际光路恢复 |
| 反例和复位 | 前台实测钱不够、接单前下锅、提前收款、重复收款、接单后买升级及重复购买。EditMode 另测少料只开一窗、待投料释放预留、同时退回已完成和仍在烹饪的两单、使用自定义费用与时间。夜里按 R 清空经营和升级，返回原入口 |

并行截图是在顺序按下两个窗口键之后抓取。原灶剩余 **2.22／3.62 秒**，快灶剩余 **1.31／2.42 秒**。这是第二口灶启动后同一时刻的两窗读数。完整配方时长由 Inspector 设置为 **3／4 秒**和 **2.1／2.8 秒**，菜单在升级后显示新的时长。

| 分段路线 | 出门 | 采集及操作 | 回店 | 合计 |
| --- | ---: | ---: | ---: | ---: |
| 采 5 份菌菇 | 1.30 秒 | 7.22 秒 | 1.26 秒 | 9.78 秒 |
| 采晴果、施雨并采雨露果 | 2.25 秒 | 4.66 秒 | 2.23 秒 | 9.14 秒 |
| 干夜拒采、施雨并采雨夜果 | 2.24 秒 | 4.29 秒 | 2.19 秒 | 8.73 秒 |

分段为固定视角回放的游戏时钟读数。复跑时打开花庭副本，进入 Play Mode 后选 **Borrowing Sun → Skywell → Check complete Flower Market route**，保持 Unity 在前台。本轮验证的是 macOS 编辑器回放和规则测试，独立试玩包与 Windows 桌面未测。花庭副本只新增五项序列化数值。原 L5 场景、摆渡脚本、相关 `.meta` 和 Build Settings 均与 `f54878e` 逐字节一致。

[完整回放日志](../evidence/flower-market-kitchen/playmode.txt) · [EditMode XML](../evidence/flower-market-kitchen/editmode.xml) · [保存引用检查](../evidence/flower-market-kitchen/references.txt)

[两窗接单](../evidence/flower-market-kitchen/flower-market-orders-accepted.png) · [双灶同时烹饪](../evidence/flower-market-kitchen/flower-market-dual-stove.png) · [收款至 34 元](../evidence/flower-market-kitchen/flower-market-two-orders.png) · [升级菜单](../evidence/flower-market-kitchen/flower-market-kitchen-upgraded.png) · [升级后双灶](../evidence/flower-market-kitchen/flower-market-upgraded-dual-stove.png) · [第 3 天黎明](../evidence/flower-market-kitchen/flower-market-dawn-after-night.png)
