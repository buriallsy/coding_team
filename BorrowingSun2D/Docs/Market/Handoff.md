# 谷地外卖店：给组员的交接说明

整理日期：2026-11-11。分支：`feature/djwcb2333/market-loop`。

这次分享包含新的 2D 可玩原型，以及边界、数值、类职责、接口和接入 3D 的计划。原课程 3D 实现仍作为旧版保留；外卖玩法尚未移植到 3D。此次交接整理只补充文档与证据，没有再次修改玩法代码。

## 先试玩

用 **Godot 4.7.2 .NET** 导入仓库里的 `BorrowingSun2D/project.godot`，安装 .NET 8 SDK，构建后按 F5。使用 .NET 版编辑器，因为游戏包含 C# 脚本。不要从 Steam 的普通 Godot 版本打开。

先走完原运货路线，在签收页进入第 1 天。玩家回到中央外卖店，开始 180 秒白天探索。靠近食材按 C 采集；站在摆渡车板上按 G 往返。到黄昏选择直接营业，或夜探后营业。营业时分别给两个窗口投料、等待完成并送餐。36 金币可在下一次菜单阶段升级双灶，烹饪时间缩短 30%。完整按键和路线见[2D README](../../README.md)。

收入通过余额、当晚营业额、累计营业额和可购买的厨房升级体现。没有固定通关收入或天数期限。跨天库存与升级仅在当前运行会话内保留；没有关闭游戏后继续的磁盘存档。

## 本次要看的资料

| 内容 | 文件与用途 |
| --- | --- |
| 新版游戏 | [project.godot](../../project.godot)、[主场景](../../Scenes/TowerSlice.tscn)、`Scripts/`，均随仓库提交 |
| 策划与正式范围 | [Design.md](Design.md)：教学、采集、昼夜天气、菜单、订单、双灶、收入和升级 |
| 全部主要数值 | [Design.md](Design.md)：时段、刷新、采集、概率、摆渡、配方、售价与升级；旧提案数值见下方历史表 |
| UML / Class Map | [Design.md](Design.md) 中的 Mermaid 类图，按实际类职责绘制 |
| 架构、状态与接口 | [Design.md](Design.md) 中的系统架构图、昼夜状态图和输入输出表 |
| 本次变化与 CCR | [ChangeMap.md](ChangeMap.md)：新增、修改、保持及未来移植的统计口径；新版 3D CCR 尚未产生 |
| 2D 结束后怎么做 | [Roadmap.md](Roadmap.md)：人工收尾、冻结、共享规则、3D 场景适配、同条件复测 |
| 验证代码 | [领域测试](../../../Tests/DomainChecks/MarketChecks.cs)、[Godot 经营路线测试](../../Scripts/Verification/MarketRouteCheck.cs) |
| 验证记录 | [Evidence/README.md](Evidence/README.md)：既有测试日志、实际界面截图及本轮检查范围 |
| 手动上传 | [MANUAL_UPLOAD.md](../../../MANUAL_UPLOAD.md)，含 Desktop 和 PowerShell 两种方式 |

GitHub 会渲染 Markdown 中的 Mermaid 图；本地纯文本编辑器可能只显示图的源码。

## 原课程资料一并保留

| 内容 | 已包含文件 |
| --- | --- |
| 原范围、边界、数值决策 | [SCOPE_DECISIONS.md](../2D-to-3D/SCOPE_DECISIONS.md)、[NUMERIC_DECISIONS.md](../2D-to-3D/NUMERIC_DECISIONS.md) |
| 原 2D 类图、框架和关系 | [CLASS_MAPS.md](../2D-to-3D/CLASS_MAPS.md)、[关系图 SVG](../2D-to-3D/RELATIONSHIP_MAP.svg)、[交互架构](../2D-to-3D/architecture.html) |
| 原计划变化图 | [change-map-planned.svg](../2D-to-3D/change-map-planned.svg)、[CHANGE_AND_CCR.md](../2D-to-3D/CHANGE_AND_CCR.md) |
| 原 3D 类关系、实际变化与 CCR | [关系图](../../../BorrowingSun3D/Docs/RELATIONSHIP_MAP.md)、[CHANGE_MAP.md](../../../BorrowingSun3D/Docs/CHANGE_MAP.md)、[分析清单](../../../BorrowingSun3D/Docs/ccr-analysis.json) |
| 原最终分歧与确定举措 | [最终审阅说明](../../../BorrowingSun3D/Docs/FINAL_DECISIONS_REVIEW.md)、[对应 PDF](../../../BorrowingSun3D/Docs/output/pdf/Final_Decisions_Review.pdf)、[独立复核记录](../../../BorrowingSun3D/Docs/Evidence/2026-11-09-final-decisions/review-checks.txt) |
| 原课程报告与演示提纲 | [中文 PDF](../../../BorrowingSun3D/Docs/output/pdf/AP6412_Borrowing_Sun_Draft.pdf)、[英文 PDF](../../../BorrowingSun3D/Docs/output/pdf/AP6412_Borrowing_Sun_Draft_EN.pdf)、[15 分钟演示提纲](../../../BorrowingSun3D/Docs/DEMO_15_MIN.md) |

最终审阅说明的三份文件从原文档提交 `173c092a7b2a3abff0dddb0982d84f4f9e4768d0` 原样补入。其结论针对 `a4bf80c` 课程版本，不覆盖后来新增的外卖玩法。旧 CCR 的 `2/5 = 40%` 也只对应旧课程版本；新增外卖版不能沿用该百分比。

## 当前仍需补充的资料

- 完整 AI 原始对话尚未导出。现有 [AI 索引](../AI/djwcb2333/INDEX.md) 只是摘要，不能替代课程要求的完整提示、回复与纠正记录。
- 在本次可检查的 GDAT 工作目录里，没有找到早期单独的“个人 2D 转 3D 思路 PDF”。本次包含的是后来完成的最终审阅 PDF 和中英文课程报告 PDF，不将它们冒称那份早期文件。
- 外卖 2D 的正式冻结标签、真人完整试玩记录、外卖 3D 及其 CCR 尚待完成。当前分享是供组员试玩与讨论的分支，不等于整门课的最终提交材料已经齐备。

## 提交与上传边界

`77e815a` 是外卖可玩实现，`2d1eeac` 补充后续计划。其后的交接整理提交补入上述历史说明、数值补充、变化图、清单与证据。手动上传时推送整个 `feature/djwcb2333/market-loop` 分支即可，保留原有提交历史。

本地打包或提交不代表 GitHub 已收到。上传后在网页核对分支、最新提交和本文件，再把该分支链接发给组员。
