01 / 定案说明

# 借晴：最终版本审阅
与分歧定案

AP6412 · Route B · djwcb2333 · 2026-11-09

> 按本轮指示，**沿用组员当前已实现的代码作为定案依据**。本说明补充“原先提出什么、最终采用什么、依据在哪里”，不提出替换玩法代码的改动。

## 版本锚点与审阅口径

| 对象 | 明确引用 |
| --- | --- |
| 课程交付分支 | buriallsy/3d-graybox；GitHub 当前头提交 a4bf80c。已在线比较确认与本地审阅提交一致。 |
| 2D 冻结基线 | 2d-baseline → 6175db93b0156b35fb736af2e28cc97aaa65d470。 |
| 3D 已测代码 | 05D → fb4cbb451c028234dd093576fa8c0e6e72bb457e。审阅头提交中的 3D 构建输入与该版本一致。 |
| 原并行提案 | 181ec16361f5c22fecd7594519111068cac55baf；原数值和框架记录保留为历史。 |
| 整合依据 | 6b4357a 合并 de74962 与 181ec16；冲突处沿用 Round 03 游戏实现，整合验证与文档。 |

这里的“分歧”指有记录可查的并行方案差异，不意味着双方曾就每一项发生口头争论。最终选项由现行代码、整合提交及本轮“按最终版算”的明确指示确定。下文“理由”是对采用该方案的技术解释，不冒充组员未记录的原话。

## 审阅结论：方案没有全盘替换，工作也并非没有作用

整合提交明确采用了我的独立领域测试、CI、CCR 工具及相关文档，并把它们调整到现行玩法。我的 GameSession / SunMirror / LightReceiver / DeliveryState 拆分方案没有成为生产代码主体；核心玩法采用组员版本。这是**保留已有游戏、整合验证和设计证据**的取舍。[E1][E2]

## 课程边界

以 Godot 2D → Godot 3D 的规则复用、空间适配、类职责和验证为讨论对象。unity-skywell 的 README 将 Unity 工程定位为概念来源；不把它混入本次课程移植的基线或 CCR。美术制作不在本说明范围内。[E9]

“定案”表示本次不再改玩法代码；仓库仍将 3D 标作已测候选，人工 Windows 验收与 3d-final 等交付手续尚待记录。本说明不创建标签，也不把待办改成已完成。


---

02 / 定案说明

# 玩法数值与操作：最终采用什么

以下九项是原提案与实际版本最直接的差异。采用现行规则后，2D 与 3D 通过共享 CargoJourney 和 TowerMechanism 保持一致。[E1][E3]

| 编号 / 议题 | 原先提案 | 最终采用与解释 |
| --- | --- | --- |
| D01 连续受光 | 3 秒 | 1.5 秒。装箱前断光重计；装箱后保留水果。采用已测试的连续计时门槛。 |
| D02 配重数量 | 6 单位全部转入；塔箱剩 0 | 6 单位原水中转 4 单位配重，塔箱留 2。水量仍守恒，不把“剩余水”视为额外水源。 |
| D03 转水触发 | T 手动启动 / 暂停 | 装箱后有光且满足东岸停靠时自动转水；失光暂停、恢复继续。移除额外手动模式的需求。 |
| D04 货车时长 | 12 秒 | 3.4 秒。行程进度 0-1 驱动车台，发车后失光不停车；不为凑三分钟延长车程。 |
| D05 发车与签收 | E 承担情境交互 | G 发车；到站后在花庭标记按 G 签收。E 继续用于设备施令，操作边界明确。 |
| D06 塔转向 | Z/X 选岸，再按 E | 选风后 E 顺转、Shift+E 逆转一档；0°、90°、180° 停点，端点有转向限位。 |
| D07 采日镜档位 | 90° / 135° / 180° 三档 | 待机 / 校准两态，由场景标记定义姿态；E 校准、Shift+E 回位。真实光线仍需经过两镜求交。 |
| D08 货运阶段 | DeliveryState 四阶段 | CargoJourney 六阶段：WaitingForLight、Loading、Ready、Travelling、Arrived、Delivered。装货与配重准备作为显式阶段。 |
| D09 通关时间 | 180 秒；参考 150-210 秒 | 保留为设计和试玩目标，不做硬性计时失败，也不宣称已由真人实测达到。原七段预算不成为计时器。 |

## 共同数值没有因此改变

原水上限 6 单位、塔行程 6 m、蓄雨与排水 1.5 单位/s、升降 1.8 m/s、转速 45°/s、停靠容差 0.001、转水 1 单位/s、交付目标 1/1 保持。当前满配重需要约 4 秒有效转水；该推导不含装货等待、走位和中途断光。

## 共同规则的闭环

发车仍要求：有水果、实时受光、东岸接轨、满配重、玩家已登车。到站不是自动获胜，必须在交付点确认；重复确认不增加计数。配重已投入时禁止排水释放机械锁，R 统一清空并恢复低桥。


---

03 / 定案说明

# 架构与三维适配：保留现行边界

| 编号 / 议题 | 原先提案或问题 | 最终采用与解释 |
| --- | --- | --- |
| D10 类的拆分 | GameSession 组合 SunMirror、LightReceiver、DeliveryState | 沿用 TowerSlice / TowerGraybox 协调；CargoJourney 合并受光、装货与运输状态；SunCourtOptics 持有天气和校准。按真实职责画图，不为类名一致重写。 |
| D11 光学范围 | 二维夹具默认 100 总程；曾考虑先用约定平面验证 | 采用真实空间的双反射。2D 每段查询上限 1500 场景单位；3D 每段 24 个世界单位。范围语义、单位与总程概念不能混写。 |
| D12 镜面表达 | 有限二维镜面迁移方式待选 | 2D 有限线段 → 3D 有限圆面；读取世界中心与法线，查询实际遮挡。塔镜继承实体偏航，不用“朝向正确”直接替代接光。 |
| D13 三维移动 | 3 m/s 初值 | 采用 GrayboxPlayer 的 4.2 世界单位/s、重力 18；Y 高度、XZ 移动。WASD 与方向键输入，无新增跳跃系统。 |
| D14 交互距离 | 统一 1.5 m 初值 | 按设备分配：塔 / 采日镜半径 1.45，登车 0.65，交付点 0.75；高度容差 0.27，入口还检查落地状态。 |
| D15 共享方式 | 预期规则复用，需验证实际边界 | 3D csproj 直接链接 TowerMechanism、RouteProgress、CargoJourney 的 2D 源文件。保留同级两工程目录，不复制后独立修改。 |
| D16 核心统计 | 原框架 6 个候选类，尚未冻结 | 采用正式 2D 冻结的 5 类；包括混合职责的 SunCourtOptics2D。按拥有的玩法职责划界，不按文件夹名一刀切。 |

## 三维光学与交互补充参数

| 参数 | 最终实现值 | 依据 |
| --- | --- | --- |
| 采日镜 / 塔镜 / 接盘圆面半径 | 0.62 / 0.54 / 0.52 | SunCourtOptics3D；世界单位。 |
| 求交容差 | 0.0001 | Optics2D 与 Optics3D；不同于停靠容差。 |
| 货车安全恢复余量 | 0.04 | CargoTrack3D；安全脚点位于车面上方。 |
| 显示基准 | 1280×720 | 两版按比例适配窗口；显示尺寸不等于物理尺度。 |

顺序核对：机关推进 → 实体同步 → 本帧光路 → 货运推进与发车检查 → 货车同步 → 玩家移动与安全恢复。两版实际协调器保留这种依赖顺序；视图读取同一光路结果。以上结论由实际源码核对，不仅依据类图。[E3][E4]


---

04 / 定案说明

# 变化图与 CCR：从候选变成可复算结论

原先我们坚持“未冻结不填百分比”。现在 2d-baseline 和固定 3D 代码都已存在，因此采用仓库已记录、且本次复算通过的结果。[E5]

| 冻结端原类 | 3D 对应 | 定案分类 |
| --- | --- | --- |
| TowerMechanism | 同一份 2D 源码 | 共享：水、配重、锁、升降、转向与停靠。 |
| RouteProgress | 同一份 2D 源码 | 共享：先北后东的到访状态；不等同最终交付。 |
| CargoJourney | 同一份 2D 源码 | 共享：受光、装箱、转水、发车、行程与签收。 |
| Optics2D | Optics3D | 修改：有限线段变为三维圆面与三维向量计算。 |
| SunCourtOptics2D | SunCourtOptics3D | 修改：保留局部状态职责，适配世界姿态及三维物理遮挡。 |

> **CCR =（2 个修改 + 0 个移除）÷ 5 个冻结核心类 × 100% = 40.00%**
> 共享 3 类；修改 2 类；移除 0 类；独立新增核心职责 0 类。

## 计数的最终解释

Optics3D 和 SunCourtOptics3D 虽是新文件，但分别接替原核心职责，各按一次修改处理。不能同时当作“原类移除”与“新增核心职责”重复计算，也不因维度适配必改就排除光学核心。

TowerSlice / TowerGraybox、角色、桥、货车、输入和界面在变化图中记录为场景与引擎适配。SunCourtOptics 即使包含显示和接线，也持有能改变结果的天气及校准状态，所以仍计入核心。此次采用仓库已有审计口径，不借重新分类压低 CCR。

## 本次独立核对

| 核对项 | 结果 |
| --- | --- |
| GitHub 分支身份 | a4bf80c 与在线 buriallsy/3d-graybox 比较 identical，0 个差异提交。 |
| 2D 核心保持 | 2d-baseline 与审阅提交的 Scripts/Core 无差异；三个共享类的源码对象相同。 |
| 3D 构建输入保持 | 审阅提交的 3D 项目（排除文档与 README）、共享 Core、共享字体及根级构建配置，与 fb4cbb4 无差异。 |
| CCR 工具输出 | CCR = 2/5 = 40.00%；工具校验 Git 身份与映射，行为分类仍需审阅解释。 |

课程重点是能说明为什么三类保持、两类改变，以及变化是否局部且可验证。40% 不是总代码改动量、代码覆盖率或评分，也不意味着全部工程有 60% 可直接复用。


---

05 / 定案说明

# 验证边界、记录补充与上传范围

| 证据层 | 结果与边界 |
| --- | --- |
| 本次 Windows 领域复跑 | 80 项领域与二维几何断言通过；构建 0 警告、0 错误。SDK 10.0.400，目标 net8.0，离线使用本机 8.0.26 参考包。 |
| 本次 CCR 工具复跑 | 7 项测试通过；2/5 = 40.00% 复算通过。默认构建最初因 NuGet 网络受限失败，随后以隔离离线配置完成；未修改项目代码。 |
| 仓库 macOS 记录 | 2D：154 无头、46 窗口；3D：160 无头、160 窗口。属于既有记录，本次未在 macOS 重跑。 |
| 仓库 Windows 托管记录 | 2D：154 无头、90 窗口；3D：160 无头、160 窗口。托管开窗验证不是 Windows 实机人工操作。 |
| 本次没有执行的验证 | 没有重新运行完整 Godot 场景或真人试玩。人工 Windows 签收、首次玩家实际用时与正式 3d-final 状态沿用仓库待办。 |

## 对既往说明的修正

此前 PDF 只对照到 de74962，不能继续用于说明当前项目尚无完整 3D 或你的工作未被采用。本说明取代其中的当前状态判断；保留那份 PDF 作为当时的思路记录。现有 2D 与 3D、共享规则、整合提交和双平台自动证据均应据实承认。

## 本次确认与贡献表述

采用组员当前实现，保持玩法代码、场景及参数不动；新增本定案说明、对应 PDF 和复核记录。本人可据实说明参与了边界提案、参数整理、验证框架、CCR 工具及本次对照审阅；生产游戏实现与既有测试记录仍按原作者和提交归属。该说明不代替个人署名反思或完整 AI 原始对话。

## 证据入口（固定提交链接）

- [E1 原始参数提案](https://github.com/buriallsy/coding_team/blob/181ec16361f5c22fecd7594519111068cac55baf/BorrowingSun2D/Docs/2D-to-3D/NUMERIC_DECISIONS.md)
- [E2 整合提交与实际保留内容](https://github.com/buriallsy/coding_team/commit/6b4357abde2f38e19c17bf61a12ff80335fb7f8a)
- [E3 最终货运规则](https://github.com/buriallsy/coding_team/blob/a4bf80cd6725dbc923201d3fa338ee2e1b07cddb/BorrowingSun2D/Scripts/Core/CargoJourney.cs)
- [E4 三维规则链接与实际类关系](https://github.com/buriallsy/coding_team/blob/a4bf80cd6725dbc923201d3fa338ee2e1b07cddb/BorrowingSun3D/Docs/RELATIONSHIP_MAP.md)
- [E5 变化图与冻结清单](https://github.com/buriallsy/coding_team/blob/a4bf80cd6725dbc923201d3fa338ee2e1b07cddb/BorrowingSun3D/Docs/CHANGE_MAP.md)
- [E6 Windows 验收证据与边界](https://github.com/buriallsy/coding_team/blob/a4bf80cd6725dbc923201d3fa338ee2e1b07cddb/BorrowingSun3D/Docs/WINDOWS_ACCEPTANCE.md)
- [E7 整合复测与贡献记录](https://github.com/buriallsy/coding_team/blob/a4bf80cd6725dbc923201d3fa338ee2e1b07cddb/BorrowingSun2D/Docs/Evidence/2026-11-08-round03-integration/RESULTS.md)
- [E8 05D 两版对照与 macOS 记录](https://github.com/buriallsy/coding_team/blob/a4bf80cd6725dbc923201d3fa338ee2e1b07cddb/BorrowingSun3D/Docs/ROUND_05D.md)
- [E9 Unity 分支的来源定位](https://github.com/buriallsy/coding_team/blob/ac1bca8/README.md)


附：本次复核记录位于 Evidence/2026-11-09-final-decisions/review-checks.txt。文档由 djwcb2333 发起，AI 辅助核对与整理；不新增成员签名，不代表组员已审阅本说明。


[下载本说明 PDF](output/pdf/Final_Decisions_Review.pdf) · [本次复核记录](Evidence/2026-11-09-final-decisions/review-checks.txt)
