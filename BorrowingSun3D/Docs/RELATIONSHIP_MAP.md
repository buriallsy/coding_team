# 3D 关系图 · 第 05D 轮

`TowerGraybox` 负责输入、物理步进、会话切换和 HUD。塔机、到访和货运规则沿用冻结的 2D C# 源码。三维场景适配负责真实光路、塔桥和车台的碰撞与呈现。会话卡片只展示当前页面和实际签收计数，按键和按钮都通过关卡协调器进入原复位流程。

```mermaid
flowchart LR
    Input[Godot 输入] --> Scene[TowerGraybox]
    Scene -->|施令 / 推进| Mechanism[TowerMechanism · 共享]
    Mechanism -->|TowerState| Scene
    Scene -->|高度 / 朝向 / 停靠| Rig[TowerRig3D]
    Rig -->|移动和转动| Lift[实体塔台与接桥]
    Scene -->|移动| Player[GrayboxPlayer]
    Player -->|落脚位置| Scene
    Scene -->|到访北庭 / 东岸| Route[RouteProgress · 共享]
    Scene -->|北庭风晴令 / 更新| Optics[SunCourtOptics3D]
    Lift -->|塔镜世界姿态| Optics
    Optics -->|光源与三个有限圆面| Trace[Optics3D]
    Optics -->|提供逐段遮挡查询| Trace
    Optics -->|IntersectRay| Physics[Godot Physics3D]
    Trace -->|LightPath3D| Optics
    Optics -->|计算光段| Beam[光束网格和接光盘]
    Optics -->|TowerHit / ReceiverLit| Scene
    Scene -->|本帧受光 / 发车 / 确认交付| Cargo[CargoJourney · 共享]
    Cargo -->|转入配重| Mechanism
    Cargo -->|装货 / 行程 / 交付状态| Scene
    Scene -->|TravelProgress / HasFruit| Track[CargoTrack3D]
    Track -->|车台位置和果箱可见性| Car[实体货车]
    Track -->|车面和交付点| Scene
    Scene -->|塔机 / 光路 / 货运状态| Hud[HUD]
    Scene -->|Intro / Playing / Finished 与签收计数| Session[TowerSessionOverlay3D]
    Session -->|按钮 Accepted| Scene
    Session -->|读取字体| Font[TowerUiFont · 共享呈现]
    Verify[GrayboxRouteCheck] -->|实际按键| Input
```

| 关系 | 代码与场景 |
| --- | --- |
| `TowerGraybox` 持有规则实例和场景适配 | [关卡协调](../Scripts/TowerGraybox.cs) · [共享编译项](../BorrowingSun3D.csproj) |
| `TowerRig3D` 投影塔台姿态并切换桥碰撞 | [实体桥适配](../Scripts/TowerRig3D.cs) · [场景](../Scenes/TowerGraybox.tscn) |
| `SunCourtOptics3D` 持有北庭天气和校准状态，读取世界姿态，接入物理遮挡和显示 | [3D 采光适配](../Scripts/SunCourtOptics3D.cs) |
| `Optics3D` 使用几何和遮挡查询，返回入射及反射段 | [3D 光学求解](../Scripts/Optics3D.cs) |
| `CargoJourney` 根据实时受光装货，与 `TowerMechanism` 共享原水转移，并持有发车、行程和交付状态 | [共享货运规则](../../BorrowingSun2D/Scripts/Core/CargoJourney.cs) · [共享塔机规则](../../BorrowingSun2D/Scripts/Core/TowerMechanism.cs) |
| `CargoTrack3D` 把行程投影到实体车台，从车面和花庭标记提供交互位置，按状态显示果箱 | [实体货运适配](../Scripts/CargoTrack3D.cs) · [场景](../Scenes/TowerGraybox.tscn) |
| `TowerSessionOverlay3D` 根据 `TowerScreen` 显示开场或签收，按钮回调由 `TowerGraybox` 处理 | [界面脚本](../Scripts/TowerSessionOverlay3D.cs) · [界面场景](../Scenes/TowerSessionOverlay3D.tscn) · [共享字体](../../BorrowingSun2D/Scripts/TowerUiFont.cs) |
| `GrayboxRouteCheck` 向默认场景发按键并检查开场、落脚、停靠、光路、货车、交付及重开 | [验证入口](../Scripts/Verification/GrayboxRouteCheck.cs) |

[05A 规则接线](ROUND_05A.md#规则与场景接线) · [05B 光路](ROUND_05B.md) · [05C 货运](ROUND_05C.md) · [05D 首玩与验收](ROUND_05D.md) · [逐类变化与 CCR](CHANGE_MAP.md)
