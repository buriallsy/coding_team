# 借晴 · 谷地外卖店 2D 原型

完成运货教学后，玩家从中央外卖店出发，白天采集食材，晚上选择夜探或开店。两个窗口可以同时烹饪，收款后可跨天攒钱升级厨房。

设计、UML 和系统架构见[外卖店设计说明](Docs/Market/Design.md)。2D 冻结条件、3D 接入顺序和已有图表见[后续计划](Docs/Market/Roadmap.md)。完整分享清单和验证边界见[交接说明](Docs/Market/Handoff.md)，本次逐类变化见[变化图与 CCR](Docs/Market/ChangeMap.md)。

## 运行

用 Godot **4.7.2 .NET** 版导入本目录的 `project.godot`，安装 .NET 8 SDK，打开后运行主场景（F5），在开场按 Enter 或点击开始。场景入口是 `Scenes/TowerSlice.tscn`，使用 Compatibility 渲染。灰盒用系统字体显示中文。macOS 优先读取系统华文黑体，其他环境按已安装的中文字体匹配。

| 按键 | 作用 |
| --- | --- |
| Enter / Space | 开始游戏，也可用鼠标点击开场按钮。 |
| A / D 或 ← / → | 行走。开局沿西岸向右走过低位桥。 |
| 1 / 2 / 3 | 选雨 / 选风 / 选晴。 |
| E | 塔上施雨或顺转一档。在北庭采日镜旁，风令调准镜面，晴令打开当地日照。 |
| Shift+E | 选风后在塔上逆转一档，或在北庭把采日镜拨回初始朝向。 |
| Q | 在北庭设备旁收回当地天气；其他位置收回塔台天气令。保留已有水量与角度。 |
| F | 转水前，塔朝入口时可开关排水。 |
| C | 教学结束后，靠近成熟食材采集。 |
| G | 教学中发车或确认交付。进入正式玩法后，站在车板上按键往返东岸和花庭。 |
| R | 教学中回到西岸低桥，重置本次货运。 |

高位接北桥后向左走到采日镜旁。用 `2`、`E` 调镜，用 `3`、`E` 施晴。向右回塔中央，用 `2`、`E` 转东，再沿东桥走上货车。等水果出现、配重到 `4/4` 且接光盘仍亮时按 `G`。到站后向右走到旗标，按 `G` 签收。点击「进入第 1 天」开始探索与经营。

## 工程入口

| 位置 | 职责 |
| --- | --- |
| `Scripts/Core/TowerMechanism.cs` | 纯 C# 的蓄雨、升降、锁扣、朝向、停靠和配重转水。`TowerState` 是只读快照。 |
| `Scripts/Core/CargoJourney.cs` | 连续接光计时、装货、发车、行程与交付状态。 |
| `Scripts/Core/RouteProgress.cs` | 纯 C# 的北庭到访与东岸完成状态，提供东岸附加条件扩展点。 |
| `Scripts/TowerSlice.cs` | 组装关卡，读输入并协调规则、场景同步、玩家、恢复与重置。 |
| `Scripts/TowerRig2D.cs` | 绑定场景节点、提供几何查询，按塔机状态同步升降和实体桥碰撞。 |
| `Scripts/SunCourtOptics2D.cs` | 管北庭日照和调镜，将场景锚点、朝向和遮挡交给光学求解器。 |
| `Scripts/Core/Optics2D.cs` | 纯 C# 求有限镜面交点、遮挡和两次反射，返回实际光段。 |
| `Scripts/CargoTrack2D.cs` | 绑定实体货车和交付站，将行程进度同步到活动车台。 |
| `Scripts/SlicePlayer.cs` | `CharacterBody2D` 行走与落地，随活动平台移动。 |
| `Scripts/TowerSessionOverlay.cs` | 开场和签收卡片、键盘焦点和按钮。 |
| `Scripts/TowerView.cs` | 读取关卡状态，画水箱、桥舌、朝向和操作提示。 |
| `Scenes/TowerSlice.tscn` | 保存岸、塔台、接桥、设备、角色与终点的位置和碰撞。 |

[项目构思](Docs/2D-to-3D/DESIGN.md) · [制作计划](Docs/2D-to-3D/PRODUCTION_PLAN.md) · [项目规范](Docs/2D-to-3D/PROJECT_CONVENTIONS.md) · [当前版本](Docs/2D-to-3D/CURRENT_STATUS.md) · [第 01 轮设计与复测](Docs/2D-to-3D/ROUND_01.md)

[SOLID 整理记录](Docs/2D-to-3D/ARCHITECTURE_REFACTOR.md) · [对象关系图](Docs/2D-to-3D/RELATIONSHIP_MAP.svg) · [制作记录](Docs/ITERATIONS.md) · [第 00 轮测试归档](Docs/TESTING.md) · [第 00 轮 Figma 过程画板](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=471-55)
