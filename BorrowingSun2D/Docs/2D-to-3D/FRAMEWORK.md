# 借晴｜范围与架构入口 / Scope & Architecture

状态：2026-11-07，架构分支；不是 `2d-baseline`，不是完整 2D 交付。

负责人 `djwcb2333` 的工作范围：正式玩法边界、职责分类、框架搭建、Class Map、变化与 CCR 口径。AI 协助实现与核对，成员仍须理解、复核并署名；不能将未复核结果描述为个人独立完成。

| 入口 | 内容 |
|---|---|
| [明确了具体边界和数值](NUMERIC_DECISIONS.md) | 本轮完整数值表、180 秒预算、实现状态与验收数字 |
| [正式范围与全部决策](SCOPE_DECISIONS.md) | 固定范围、180 秒体验目标、输入和全部初始参数 |
| [Class Map 与架构](CLASS_MAPS.md) | 导入灰盒、当前实际框架、未来场景接入；明确区分事实与预测 |
| [变化清单与 CCR](CHANGE_AND_CCR.md) | 灰盒到框架的真实变化，2D→3D 的待测清单，统计规则 |
| [验收与跨平台矩阵](ACCEPTANCE.md) | 领域、几何、原有物理路线及完整关卡的验收范围 |
| [实现与交接](INTEGRATION.md) | 更新顺序、空间约定、接口调用及后续实现任务 |
| [本次验证结果](../../Docs/Evidence/2026-11-07-framework/RESULTS.md) | 实际构建、断言、限制与证据 |
| [可视化架构板](architecture.html) | 离线可打开的架构图和决策摘要 |

## 工程位置与运行

仓库中的游戏入口：`BorrowingSun2D/project.godot`。
用 Godot 4.7.1 .NET 导入这个文件，构建 C# 后按 F5。
当前生产场景仍是原有塔台灰盒；新增领域和光学框架尚未接入完整的三岸场景，不能从 F5 进入水果交付流程。

领域框架不依赖 Godot，可用 .NET 8 或兼容 SDK 运行：

```sh
dotnet run --project Tests/DomainChecks
python Tools/ccr.py
```

Windows 也可运行 `Tools/verify.ps1 -GodotBin <Godot控制台程序路径>`。离线环境可增加 `-Offline`，使用 Godot 自带包和已安装的 .NET 8 targeting pack；该选项只创建被忽略的 `.local/` 构建环境。

本分支完成的是边界和可执行框架，不提前创建冻结标签，不把领域夹具测试当成玩家完整通关。正式冻结以 [验收门槛](ACCEPTANCE.md) 为准。
