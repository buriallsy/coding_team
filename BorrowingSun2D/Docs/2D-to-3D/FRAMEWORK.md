# 借晴｜范围与架构 / Scope & Architecture

当前生产场景是 **Round 03 的完整 2D 灰盒**。借雨升塔、借风接桥、经双镜送光，再用原水作配重跨谷交付一箱水果。用 Godot 4.7.1 .NET 和 .NET 8 SDK 导入 [project.godot](../../project.godot)，按 `F5` 开局。 [操作与证据](ROUND_03.md) · [当前状态](CURRENT_STATUS.md) · [整合复测](../Evidence/2026-11-08-round03-integration/RESULTS.md)

Round 04 补教程、交互提示与必要视觉，完成试玩和干净检出复测后冻结 `2d-baseline`。Round 05 从冻结版本制作同规则的 Godot 3D，并核对类变化与 CCR。 [团队制作计划](PRODUCTION_PLAN.md)

| 文档 | 内容 |
|---|---|
| [范围与决策](SCOPE_DECISIONS.md) | 当前行为约定、体验目标与后续范围 |
| [数值与控制](NUMERIC_DECISIONS.md) | Round 03 的实际参数及并行框架的初值差异 |
| [Class Map](CLASS_MAPS.md) · [可视化架构板](architecture.html) | 当前代码关系、历史框架和待复核的 3D 对应关系 |
| [变化与 CCR](CHANGE_AND_CCR.md) | 已发生的 2D 开发变化、冻结口径和迁移记录办法 |
| [接入与交接](INTEGRATION.md) | 当前物理帧顺序、规则入口和场景边界 |
| [验收与平台矩阵](ACCEPTANCE.md) | 已有证据和冻结前待验证项 |

## 并行框架贡献 · 2026-11-07

`djwcb2333` 在 Round 00 导入版上整理了范围、职责分类、类图、纯 C# 候选框架、领域测试与 CCR 工具。这些资料和 Windows 灰盒测试保留其原始版本口径。当前说明按已接通的 Round 03 场景、输入与规则核对。 [原始提交](https://github.com/buriallsy/coding_team/commit/181ec16361f5c22fecd7594519111068cac55baf) · [历史验证](../Evidence/2026-11-07-framework/RESULTS.md) · [贡献与 AI 交流索引](../AI/djwcb2333/INDEX.md)
