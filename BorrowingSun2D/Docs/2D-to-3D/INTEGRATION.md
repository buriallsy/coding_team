# 生产接线与交接 / Integration

## Round 03 的状态入口

正式场景从 [TowerSlice.tscn](../../Scenes/TowerSlice.tscn) 启动。`TowerSlice` 持有塔机、到访、货运规则以及 2D 场景适配层。塔和配重由同一个 `TowerMechanism` 记账，步行旗标归 `RouteProgress`，装货与最终交付归 `CargoJourney`。`SunCourtOptics2D` 持有北庭天气、采日镜状态和当帧光路。 [实际对象关系](RELATIONSHIP_MAP.svg)

每个正常物理帧按 [TowerSlice](../../Scripts/TowerSlice.cs) 的顺序推进。

1. `ReadCommands()` 按设备和站位分派选牌、施令、收令与排水。
2. `mechanism.Tick(step)` 推进塔机，再用 `Rig.Synchronize(mechanism.State)` 同步升降和对应实体桥。
3. `Optics.Tick(mechanism.Heading)` 用当前镜面、日照方向、接光盘和遮挡求实际光路。
4. `cargo.Tick(step, mechanism, Optics.ReceiverLit)` 使用本帧接光推进装货、转水或车程。随后 `ReadCargoCommand()` 处理 `G`，判定发车或到站交付。
5. `CargoTrack.Synchronize(cargo.TravelProgress)` 更新实体车台，再 `Player.Step()` 和 `TrackPlayer()`，处理移动、到访和恢复。行车中水平输入为零，角色仍使用物理承托。

`R` 复位塔机和货运规则，经平时的 `Rig.Synchronize` 与 `CargoTrack.Synchronize` 恢复实体，再清光学设备并将玩家送回入口。复位后要重新走过真实低桥。 [复跑和证据](ROUND_03.md)

## 规则与物理边界

| 调用 | 状态与调用约束 |
|---|---|
| `TowerMechanism.Cast` / `Recall` / `ToggleDrain` | 塔上指令由实际施令范围与门槛占用约束；塔机验证天气、台高、朝向与运动。 |
| `SunCourtOptics2D.AimWithWind` / `ClearSky` / `Recall` | 北庭设备按近距离分派。风在待机和校准间切换，晴天持久到当地换天气或收令。 |
| `Optics2D.Trace` | 输入有限镜面、接光盘、光源和实体遮挡回调；返回实际光段与命中。`TowerView` 读取同一结果。 |
| `CargoJourney.Tick` / `TowerMechanism.TransferToCounterweight` | 实时受光给连续计时，装货后可转水。塔机只在东向停稳时从同一份原水记入配重，机械锁保持高位。 |
| `CargoJourney.TryLaunch` | `G` 发车，检查水果、实时接光、东向停靠、配重和实际车上站位。已发车的行程由时间推进。 |
| `CargoJourney.TryDeliver` | 到站后读取花庭交付点的实体站位，`G` 确认一次。 |

光学求解使用 `System.Numerics.Vector2` 表示由 Godot 场景适配传入的二维坐标。当前半径、方向、法线和遮挡使用同一个场景坐标系；光路的默认查询上限为每段 1500 场景单位。光学遮挡层与玩家移动层有不同用途，北向反射在仅拦光的挡板终止。 [二维求解器](../../Scripts/Core/Optics2D.cs) · [场景适配](../../Scripts/SunCourtOptics2D.cs) · [参数](NUMERIC_DECISIONS.md)

## 验证与后续接入

- 当前仓库的领域验证和跨平台工具源自 `djwcb2333` 的并行分支。领域与几何可独立运行；真实场景的输入、碰撞、载具、失光和重开仍按 [Round 03 复跑说明](ROUND_03.md) 验证。 [验收矩阵](ACCEPTANCE.md) · [整合复测](../Evidence/2026-11-08-round03-integration/RESULTS.md)
- Round 04 在现行主线上补教程、交互与必要视觉，记录真人和干净检出证据。冻结后，Round 05 用实际 3D 节点替换角色、桥、镜面几何和货轨适配，并对照同一组玩法用例。 [制作计划](PRODUCTION_PLAN.md)
- 2026-11-07 的 `GameSession.Resolve`、手动转水、镜面档位与相应坐标转换属于当时的候选接线。原文和验证保留供追溯。 [历史接线](https://github.com/buriallsy/coding_team/blob/181ec16361f5c22fecd7594519111068cac55baf/BorrowingSun2D/Docs/2D-to-3D/INTEGRATION.md) · [历史验证](../Evidence/2026-11-07-framework/RESULTS.md)
