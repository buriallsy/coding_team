# 2D → 3D 变化图与 CCR

冻结端为 `2d-baseline`（`6175db93b0156b35fb736af2e28cc97aaa65d470`）。清单的 `final_ref` 固定到本轮已测的 05D 代码提交 `fb4cbb451c028234dd093576fa8c0e6e72bb457e`。候选 CCR 使用这份代码计算。`3d-final` 留待 macOS 和 Windows 验收。 [2D 口径](../../BorrowingSun2D/Docs/2D-to-3D/CHANGE_AND_CCR.md) · [机器可读清单](ccr-analysis.json) · [05D 验收](ROUND_05D.md)

## 冻结核心逐类对照

| 2D 冻结类 | 3D 对应 | 审计结论 |
| --- | --- | --- |
| `TowerMechanism` | 同一份源码经项目文件编译链接 | **共享。** 蓄放雨水、升降、高位机械锁、转向、停靠和原水转配重共用同一实现。 [规则](../../BorrowingSun2D/Scripts/Core/TowerMechanism.cs) · [3D 编译链接](../BorrowingSun3D.csproj) |
| `RouteProgress` | 同一份源码经项目文件编译链接 | **共享。** 两版都记录北庭先到访和东岸到访。空间落点由各自的场景提供。 [规则](../../BorrowingSun2D/Scripts/Core/RouteProgress.cs) · [3D 编译链接](../BorrowingSun3D.csproj) |
| `CargoJourney` | 同一份源码经项目文件编译链接 | **共享。** 持续受光、一次装箱、配重、发车、跨谷计时与单次交付共用同一实现。 [规则](../../BorrowingSun2D/Scripts/Core/CargoJourney.cs) · [3D 编译链接](../BorrowingSun3D.csproj) |
| `Optics2D` | `Optics3D` | **修改。** 有限镜面由 2D 线段变为 3D 圆面。两版都按几何求交、判断有效入射面，并计算双反射和最近遮挡。候选版也检查三维向量与射程的有限性。 [2D 求解](../../BorrowingSun2D/Scripts/Core/Optics2D.cs) · [3D 求解](../Scripts/Optics3D.cs) · [3D 光路实测](ROUND_05B.md) |
| `SunCourtOptics2D` | `SunCourtOptics3D` | **修改。** 两版都持有北庭天气、校准与实时光路。2D 使用随塔向连续投影的镜面角度，3D 镜面继承实体塔台偏航并通过世界姿态与 `Physics3D` 计算遮挡。该类虽有呈现和场景接线，也拥有可改变结果的局部状态和光路入口，计入核心。 [2D 接线](../../BorrowingSun2D/Scripts/SunCourtOptics2D.cs) · [3D 接线](../Scripts/SunCourtOptics3D.cs) · [3D 镜面标记](../Scenes/TowerGraybox.tscn) |

**候选 CCR = 2 ÷ 5 × 100% = 40.00%。** 分母为冻结端五个核心类，分子为行为实现发生修改或被移除的原类。本次三类共享、两类修改、零类移除。新增的两个 3D 光学类分别承接原类职责，各计作一次修改。3D 独有新增核心职责为零，单列于分母之外。 [清单](ccr-analysis.json)

## 按职责排除

| 迁移变化 | 范围 |
| --- | --- |
| `TowerSlice` → `TowerGraybox` | 接入输入、实际站位、物理帧顺序、重开与 HUD。逐项核对后，塔机、到访、发车和交付的规则判定仍各自落在共享核心，局部天气和光路落在对应光学类。 |
| `SlicePlayer` → `GrayboxPlayer`，`TowerRig2D` → `TowerRig3D`，`CargoTrack2D` → `CargoTrack3D` | 角色控制、桥和载具承担真实碰撞与场景投影。共享塔机和货运规则接收本版的停靠、光路、站位结果。 |
| `TowerView`、`TowerSessionOverlay`、`TowerUiFont` 与 3D HUD、开场和签收界面 | 首玩引导、会话界面和字体归入交互与呈现。`TowerUiFont` 是额外共享的呈现源码。 |
| 场景资源、标记、测试、枚举、只读快照和光段记录 | 资源与测试用于实现和验收。数据类型附着于其拥有者，本次按类的玩法职责计数。 |

## 复算

在仓库根目录运行。需要重新核对冻结输出时，可用 `--freeze` 写到尚不存在的临时文件，然后对比其中的五条路径和 blob。清单已经保留相同的 Git 对象身份及逐类审计依据。

```sh
audit_dir=$(mktemp -d)
python3 Tools/ccr.py --freeze 2d-baseline --output "$audit_dir/frozen-core.json"
python3 Tools/ccr.py --analysis BorrowingSun3D/Docs/ccr-analysis.json
git diff --exit-code 2d-baseline fb4cbb451c028234dd093576fa8c0e6e72bb457e -- BorrowingSun2D/Scripts/Core/
```

当前工具核验标签指向、冻结 blob、原类映射和候选路径，审阅者负责行为归类。本轮结果和清单对应到同一份代码。玩法同等性和双平台签收由实际游戏验收记录。 [CCR 输出](Evidence/round-05d/ccr.log) [CCR 工具](../../Tools/ccr.py) · [3D 类关系](RELATIONSHIP_MAP.md)
