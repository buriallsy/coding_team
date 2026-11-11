# L5 花庭经营白盒

2026-11-11 · Unity 6000.5.4f1。打开 `05_Skywell_FlowerMarket.unity`，从原 L5 入口取桨和晴令、借雨升塔、修镜送光、登车并在花庭签收。签收之后走到小铺，再短按 `E` 开始经营。场景副本有自己的 GUID。原 `05_Skywell.unity`、它的元数据、原航线及 `SkywellVoyage.cs` 字节保持一致。 [花庭白盒场景](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity) · [接入方案](../05_Architecture/05_MARKET_INTEGRATION.md) · [冲突分析](../05_Architecture/06_MARKET_CONFLICTS.md)

现行操作与数值见 [花庭昼夜回路](Skywell_Flower_Market_Day_Night.md)。本页保留首版白盒的复测记录。

## 场地与走法

小铺放在花庭固定平台的西北侧。签收点和登岸过板继续留给原货运。菌菇靠西南侧，雨果靠东南侧，玩家能选择近处补货，或多走一段并操纵天气。柜台和雨盆用实体盒体，食材标记不拦路。原屋、临崖栏杆和货台闸门按原逻辑保留。

| 固定点 | 世界坐标（米） | 操作 |
| --- | --- | --- |
| 签收点 | `(-26, 35, 3)` | 下车到标记，短按 `E` 完成原货运 |
| 小铺 | `(-30.15, 35, 5.15)` | 短按 `E` 开店，`Q` 或 `Esc` 回庭院 |
| 菌菇 | `(-29.75, 35, 0.65)` | 落地靠近后短按 `E`，每次 1 份 |
| 雨露果 | `(-21.85, 35, 0.05)` | 局部有雨时落地靠近，短按 `E` 采 1 份 |
| 花庭集雨盆 | `(-21.85, 35, 1.60)` | 用 `Tab` 选雨令，长按 `E` 左键点击实体雨盆 |
| 花庭雨区 | `(-22.45, 35, 0.90)` | 半径 3 m、垂直半高 2.5 m。施令站位与果点在区内，小铺、菌菇和签收点在区外 |

用保存场景中的平台碰撞体，以 25 cm 步距检查签收、小铺、菌菇、施令站位和雨果之间的七段路线。净空以原到站开闸姿态检查，保存副本时恢复原闭闸初始位。副本只在花庭新增经营场地与第六个雨令站；原五个天气站继续使用原索引。地形检查没有移动船、桥或原光学遮挡。

## 数值交接

本轮只测一轮采集、营业和次日。两道菜共用一个灶位。未完成的订单要先交餐，才能按 `N` 或点「次日」。次日结转现金与未消耗食材，货运签收、塔位、水量和剧情仍是同一世界。没有经营倒计时，便于先测纯路程、施雨和烹饪时间。

| 项目 | 白盒试值 | 修改位置 |
| --- | --- | --- |
| 菌菇便当 | 菌菇 ×2，3 秒，12 元 | `FlowerMarketRun.TryGetRecipe` |
| 雨果菌菇盒 | 菌菇 ×1 + 雨露果 ×1，4 秒，22 元 | `FlowerMarketRun.TryGetRecipe` |
| 单次采集 / 刷新 | 1 份 / 1 秒 | `SkywellFlowerMarket.regrowSeconds` |
| 采集/小铺交互半径 | 1.35 m，高度差上限 0.65 m | `SkywellFlowerMarket.interactionRadius` |
| 施令至生效 | 原天气槽切换 1.2 秒 | `SkywellWeatherConsole` 现行参数，本轮沿用 |

本轮回放和规则测试包含试值下的库存与收款断言。改动菜谱或售价后，用新预期复跑，并记录对应的采集批次和收益。

店内可按 `1` 或 `2` 下锅，完成后按 `E` 交餐收款，也可点按钮。花庭自己的雨区由现有令牌系统驱动，离区会收回雨令。干燥的果点仍能互动并说明需要下雨。

[经营规则与菜谱](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/FlowerMarketRun.cs) · [点位与天气接线](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/SkywellFlowerMarket.cs)

## 实走与复测

在 macOS 的 Unity 6000.5.4f1 图形 Game View，从原入口连续使用生产 Input System 键鼠、角色碰撞和实际施令射线，完整跑到花庭签收与次日，**312 项通过**。回放期间没有摆放角色、写入天气结果或跳过原货运。

| 路线 | 从店出发 | 采集和操作 | 回店 | 全程 |
| --- | ---: | ---: | ---: | ---: |
| 小铺 → 菌菇 → 小铺 | 1.26 s | 5.31 s | **1.24 s** | 7.81 s |
| 小铺 → 雨露果 → 小铺 | 2.70 s | 4.28 s | **2.68 s** | 9.66 s |

测量取游戏时钟、固定视角、实际沿途碰撞。菌菇操作段包含 4 次采集和刷新间隔。雨露果操作段包含干燥反例、选雨令、长按瞄准、鼠标命中、等到局部雨生效及采 1 份。两条路的步行段可单独对照，操作段按上述批量和输入节奏解读。

- 货运签收保持原样。签收本身不会开店，随后步行到小铺的短按 `E` 才开启第 1 天。用完店内操作后 `Q` 恢复行走。店内 `Tab` 不会开天气面板。
- 干燥时采雨果失败。用真实指针点击花庭集雨盆后，该区实际下雨，雨果可采。五个原天气站均未收到花庭雨，回到店铺前离区会自动收回雨令。
- 菌菇便当到账 12，雨果菌菇盒再到账 22，提前收款、重复收款和带未完成订单跨日均被拒绝。第 2 天保留 **34 元和 1 份菌菇**。签收、塔位、原水与配重保持，店内按 `R` 则清掉经营并回原入口。
- 西北柜台位置本轮验证了可走、可站稳、可交互，回店不经过登岸板。首次玩家是否能迅速识别招牌和两个采集点，还需另找玩家实试。

同学可以用 **1.24 s / 2.68 s** 的回程和各自的出门时间来算移动开销，用采集和操作段来估算重复采摘与施雨成本。当前一轮回放固定走向且熟悉操作，适合作为白盒基线。先用相同的出门批次比较两道菜的每分钟收入，再决定雨果菜价是否补足施雨与路程。

复跑时打开副本、进入 Play Mode，选择 **Borrowing Sun → Skywell → Check complete Flower Market route**，保持 Unity 窗口前台。首次前台回放因失焦中断。批处理随后实走至码头，但原爬梯需要窗口实际获焦。最终通过的是前台完整回放，批处理只用于制场和静态验证。

另跑了 **122 项 EditMode**，含 5 项经营规则，全部通过。保存引用检查通过 5 场景、1 预制体、12,352 个组件。图形回放有原天气目标对非凸碰撞体调用 `ClosestPoint` 的既有日志；经营雨盆使用盒体，已单独验证指针命中。 [EditMode XML](../evidence/flower-market/editmode.xml) · [保存引用检查](../evidence/flower-market/references.txt)

[完整 Play Mode 记录](../evidence/flower-market/playmode.txt) · [场景净空记录](../evidence/flower-market/authoring.txt) · [开店原帧](../evidence/flower-market/flower-market-shop.png) · [本地降雨原帧](../evidence/flower-market/flower-market-local-rain.png) · [两单完成原帧](../evidence/flower-market/flower-market-two-orders.png) · [次日结转原帧](../evidence/flower-market/flower-market-day-two.png)
