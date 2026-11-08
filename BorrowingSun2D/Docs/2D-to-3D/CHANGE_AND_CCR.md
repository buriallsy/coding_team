# 变化图与 CCR / Change Map and Core Change Ratio

## 实际变化 · 导入灰盒到 Round 03

| 项目 | 变化 | 依据 |
|---|---|---|
| `TowerMechanism` | 从两岸机关发展为三岸停靠。持有同一份原水和配重，处理高位锁、等量转水与排水保护。 | [三岸与低桥](ROUND_01.md) · [货运测试](ROUND_03.md) |
| `RouteProgress` / `TowerRig2D` | 从 `TowerSlice` 分出旗标顺序与实际场景几何投影。 | [结构整理](ARCHITECTURE_REFACTOR.md) |
| `SunCourtOptics2D` / `Optics2D` | 新增北庭局部天气、调镜、有限镜面、遮挡和双反射。 | [光路测试](ROUND_02.md) |
| `CargoJourney` / `CargoTrack2D` | 新增持续接光装货、转水、条件发车、行程、实体载具及一次交付。 | [完整主线](ROUND_03.md) |
| `TowerSlice` / `SlicePlayer` / `TowerView` | 适配三岸、光路与实体货运，按实际状态同步碰撞、输入和反馈。 | [现行 Class Map](CLASS_MAPS.md) |

这些变化发生在 2D 基线冻结前。`djwcb2333` 在 2026-11-07 并行分支新增 `SunMirror`、`LightReceiver`、`DeliveryState`、`GameSession`、领域夹具、跨平台工具和 CCR 方法。该分支的变化图单独保留。 [并行分支原始变化记录](https://github.com/buriallsy/coding_team/blob/181ec16361f5c22fecd7594519111068cac55baf/BorrowingSun2D/Docs/2D-to-3D/CHANGE_AND_CCR.md)

## Round 05 迁移预测 · 待实现和复核

[待验证变化图](change-map-planned.svg) 记录当前设想。塔机、到访和货运的纯规则可以优先尝试共用。二维求交和混合采光职责需要在 3D 场景重新核对，角色、镜头、实体桥和货轨由各版本适配。实际共享、修改、移除和新增以冻结后的 3D 源码和同组实测为准。 [制作计划](PRODUCTION_PLAN.md)

## CCR 口径

`CCR = (行为被修改或移除的原 2D 核心类数 ÷ 2D 冻结时的核心类总数) × 100%`

1. Round 03 的五个候选是 `TowerMechanism`、`RouteProgress`、`CargoJourney`、`Optics2D` 和混合职责 `SunCourtOptics2D`。Round 04 根据实际冻结代码重新核对分母。
2. 光学求解与局部天气属于核心玩法。冻结时检查 `TowerSlice` 等协调层是否仍拥有实质玩法规则。输入、纯显示、测试、常量、枚举和只读数据按实际职责归类。
3. 仅搬文件、改名、排版或注释的类仍可归为 `shared`，行为核对以代码和测试为准。拆分或合并按原类逐个追踪，每个原类只计一次。
4. 逐类登记 `shared`、`modified` 或 `removed`，附原职责、对应 3D 路径、理由和证据。新增 3D 核心类单列，沿用原 2D 分母。

[分析清单](ccr-analysis.json) 当前保留候选，CCR 为 **PENDING**。在仓库根运行 `python Tools/ccr.py` 可查看状态。Round 04 复测通过、团队创建真实 `2d-baseline` 后，保存冻结清单。

```sh
python Tools/ccr.py --freeze 2d-baseline --output frozen-core.json
```

工具从标签读取提交与源码 blob。Round 05 完成后，在清单补 `final_ref`、逐类 `reviews` 和 `new_core_classes`，再运行 `python Tools/ccr.py --analysis <文件>`。工具核验标签、路径与证据字段，由成员判断行为变动。 [类图](CLASS_MAPS.md) · [冻结验收](ACCEPTANCE.md)
