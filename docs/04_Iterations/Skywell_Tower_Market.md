# L5 经营｜中央塔小铺与山头采集

小铺装在中央塔的旋转平台，柜台与灯跟随塔升降、转向。北侧采光庭采菌菇，东侧山阴村施雨后采果。玩家靠原有实体接桥在两岸与塔之间来回，采到的原料用于中央塔的双灶。 [经营副本](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity)

| 位置 | 可做的事 | 到达方式 |
| --- | --- | --- |
| 中央塔 | 开始采集、选择黄昏路线、接单、出餐与日结 | 沿原入口取桨、拿晴令并爬梯回塔，之后在塔上按 E |
| 北侧采光庭 | 采菌菇 | 雨令蓄水升 6 米，风令转向北桥 |
| 东侧山阴村 | 采果，利用当地雨区改变果实 | 回塔转向东桥。夜间干燥无果，东岸降雨后可采雨夜果 |
| 西侧低岸 | 原入口与低位回程 | 转回西向，在塔上开放水阀降回低位 |

经营从回塔解锁。东向亮村仍使用原来的镜面反射与遮挡判定，塔的蓄水留给升降，旧货车环游及装车奖励从经营副本移除。采集和归店用原有天气与桥体，北庭、东岸保留已到访同层的绞盘召回。原 L5 与摆渡另由原场景提供。 [接桥与升降规则](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellLiftMechanism.cs)

首次成功接单起算营业。直接回店默认 120 秒，夜探后默认 60 秒。时间到或按 N 关闭订单并退料，日结再按 N 才开始次日。在 `Flower_market_prototype` 的 **Skywell Flower Market → Trading** 可分别调整营业时长。沿用双灶、升级与收入结转。改线前的到点退料回放单独保留。 [营业试值与自动日结实测](Skywell_Flower_Market_Settlement.md)

## 实走记录

macOS Unity 6000.5.4f1 前台 Game View 从原入口以键鼠连续实走，**236 项检查通过**。EditMode **138 项**通过，五个场景、一个预制体及 **12,358 个组件**通过保存引用检查。

| 验收项 | 实测结果 |
| --- | --- |
| 入店与接桥 | 沿原水路取桨和晴令，爬梯回塔后短按 E 开店。塔上的雨令将玩家和柜台升高 6 米。两次风令分别接北桥、东桥，水留在升降储水槽 |
| 北庭与东岸 | 在北庭采 3 份菌菇。东岸果树实测没有直射日光，干燥时无果。对东岸原天气装置施雨后采到 1 份雨露果，离开雨区收回雨令 |
| 回塔经营 | 两座山头采集后回到同一座转台。实测两口灶同时下锅，分别交餐收 12 元与 22 元。主动收摊后日结为 34 元，按 N 进入第二天，余额、塔位和水量都结转 |
| 放水和重开 | 风令从东转北再转西，按 E 开原放水阀，搭塔降回西侧低位。按 R 回原入口，储水、库存和收入恢复开局 |

| 路段 | 出发 | 采集与施雨 | 回塔 | 合计 |
| --- | ---: | ---: | ---: | ---: |
| 北庭菌菇 | 10.01 秒 | 3.97 秒 | 8.42 秒 | 22.40 秒 |
| 东岸果树 | 16.18 秒 | 4.28 秒 | 14.94 秒 | 35.39 秒 |

分段是游戏时钟读数。白天试跑 75 秒时在东桥回程进入黄昏。调到 **105 秒**后，从柜台开局、蓄水、两次转塔、两处采集到走回柜台用时 **83.59 秒**，余 **21.20 秒**。夜探采集继续用 45 秒。营业与日结规则的自动收摊、双灶退料和快灶结转见改线前的独立回放，中央塔这轮验证直接回店的经营路径。 [75 秒回放节选](../evidence/tower-market/day-window-75s.txt) · [改线前的自动日结](Skywell_Flower_Market_Settlement.md)

复跑可在经营副本进入 Play Mode，选择 **Borrowing Sun → Skywell → Check complete Flower Market route**，保持 Unity 窗口在前台。本轮实测环境是 macOS 编辑器。原 L5 场景、摆渡、原水路脚本和 Build Settings 的文件内容均与发布基线一致。

[完整回放](../evidence/tower-market/playmode.txt) · [EditMode](../evidence/tower-market/editmode.xml) · [保存引用](../evidence/tower-market/references.txt)

[中央塔低位开店](../evidence/tower-market/tower-market-shop-low.png) · [升到高位](../evidence/tower-market/tower-market-shop-high.png) · [北庭采集](../evidence/tower-market/tower-market-north-harvest.png) · [东岸施雨采果](../evidence/tower-market/tower-market-east-rain.png) · [双灶并行](../evidence/tower-market/tower-market-dual-stove.png) · [塔上日结](../evidence/tower-market/tower-market-summary.png) · [降回西岸](../evidence/tower-market/tower-market-west-low.png)
