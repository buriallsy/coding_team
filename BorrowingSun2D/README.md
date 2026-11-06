# 借晴 · 塔台灰盒

独立的 Godot C# 2D 原型。当前目标是从西侧低岸步行上塔，借雨升高，再借风接通北侧采光庭，沿实体桥走到旗标。采光庭是本版终点。

## 运行

用 Godot **4.7.1 .NET** 版导入本目录的 `project.godot`，安装 .NET 8 SDK，打开后运行主场景（F5）。场景入口是 `Scenes/TowerSlice.tscn`，使用 Compatibility 渲染。灰盒用系统字体显示中文。macOS 优先读取系统华文黑体，其他环境按已安装的中文字体匹配。

| 按键 | 作用 |
| --- | --- |
| A / D 或 ← / → | 行走。开局沿西岸向右走过低位桥。 |
| 1 / 2 | 选雨 / 选风。 |
| E | 靠近塔上设备施令。雨满后机械锁扣合，选风后桥舌转到采光庭。 |
| Q | 收令并停下天气驱动，保留已有水量与角度。 |
| F | 塔朝入口时打开或关闭排水。可以在未完成升降时用它退回低位。 |
| R | 回到入口，重置塔、水、天气和完成状态。 |

高位接桥后，向左走过桥并抵达旗标，画面显示「已抵达采光庭」。桥口留有失足恢复，站在门槛上或升降中发转向指令时会提示等待或退开。

## 工程入口

| 位置 | 职责 |
| --- | --- |
| `Scripts/Core/TowerMechanism.cs` | 纯 C# 的蓄雨、升降、锁扣、朝向、停靠和天气规则。`TowerState` 是只读快照。 |
| `Scripts/TowerSlice.cs` | 组装关卡。读输入，调用规则，移动平台并切换桥的实体碰撞，处理抵达与重置。 |
| `Scripts/SlicePlayer.cs` | `CharacterBody2D` 行走与落地，随活动平台移动。 |
| `Scripts/TowerView.cs` | 读取关卡状态，画水箱、桥舌、朝向和操作提示。 |
| `Scenes/TowerSlice.tscn` | 保存岸、塔台、接桥、设备、角色与终点的位置和碰撞。 |

[项目构思](Docs/2D-to-3D/DESIGN.md) · [制作计划](Docs/2D-to-3D/PRODUCTION_PLAN.md) · [项目规范](Docs/2D-to-3D/PROJECT_CONVENTIONS.md) · [当前版本](Docs/2D-to-3D/CURRENT_STATUS.md)

[制作记录](Docs/ITERATIONS.md) · [测试记录](Docs/TESTING.md) · [Figma 过程画板](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=471-55)
