# 借晴 · 三岸接驳

独立的 Godot C# 2D 原型。第 01 轮从西侧低岸步行上塔，借雨升高，借风接通北侧采光庭。到过北庭后步行回塔，再换接东岸，走到东岸旗标完成本轮。

## 运行

用 Godot **4.7.1 .NET** 版导入本目录的 `project.godot`，安装 .NET 8 SDK，打开后运行主场景（F5）。场景入口是 `Scenes/TowerSlice.tscn`，使用 Compatibility 渲染。灰盒用系统字体显示中文。macOS 优先读取系统华文黑体，其他环境按已安装的中文字体匹配。

| 按键 | 作用 |
| --- | --- |
| A / D 或 ← / → | 行走。开局沿西岸向右走过低位桥。 |
| 1 / 2 | 选雨 / 选风。 |
| E | 靠近塔上设备施令。雨满后机械锁扣合。选风后按西向、北庭、东岸的顺序转一档。 |
| Shift+E | 选风后反向转一档。 |
| Q | 收令并停下天气驱动，保留已有水量与角度。 |
| F | 塔朝入口时打开或关闭排水。可以在未完成升降时用它退回低位。 |
| R | 回到入口，重置塔、水、天气和完成状态。 |

高位接北桥后向左走到采光庭旗标，再向右走回塔上设备旁。再次按 `E` 施风接东岸，等桥停稳后向右走到旗标。站在任一侧的接桥门槛或升降中施风时，画面会提示退开或等待。失足会回到最近到达的安全平台。

## 工程入口

| 位置 | 职责 |
| --- | --- |
| `Scripts/Core/TowerMechanism.cs` | 纯 C# 的蓄雨、升降、锁扣、朝向、停靠和天气规则。`TowerState` 是只读快照。 |
| `Scripts/Core/RouteProgress.cs` | 纯 C# 的北庭到访与东岸完成状态，提供东岸附加条件扩展点。 |
| `Scripts/TowerSlice.cs` | 组装关卡，读输入并协调规则、场景同步、玩家、恢复与重置。 |
| `Scripts/TowerRig2D.cs` | 绑定场景节点、提供几何查询，按塔机状态同步升降和实体桥碰撞。 |
| `Scripts/SlicePlayer.cs` | `CharacterBody2D` 行走与落地，随活动平台移动。 |
| `Scripts/TowerView.cs` | 读取关卡状态，画水箱、桥舌、朝向和操作提示。 |
| `Scenes/TowerSlice.tscn` | 保存岸、塔台、接桥、设备、角色与终点的位置和碰撞。 |

[项目构思](Docs/2D-to-3D/DESIGN.md) · [制作计划](Docs/2D-to-3D/PRODUCTION_PLAN.md) · [项目规范](Docs/2D-to-3D/PROJECT_CONVENTIONS.md) · [当前版本](Docs/2D-to-3D/CURRENT_STATUS.md) · [第 01 轮设计与复测](Docs/2D-to-3D/ROUND_01.md)

[SOLID 整理记录](Docs/2D-to-3D/ARCHITECTURE_REFACTOR.md) · [对象关系图](Docs/2D-to-3D/RELATIONSHIP_MAP.svg) · [制作记录](Docs/ITERATIONS.md) · [第 00 轮测试归档](Docs/TESTING.md) · [第 00 轮 Figma 过程画板](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=471-55)
