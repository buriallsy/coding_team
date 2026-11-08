# 从这里开始

两版都从西岸上塔、调镜借光，最后搭实体货车把一箱水果送到花庭。2D 已冻结为 `2d-baseline`（`6175db9`）。3D 以第 05D 轮已测代码 `fb4cbb4` 为验收候选。两版已完成 Windows 托管开窗预验，实机人工验收待补。

1. 用 Godot 4.7.1 .NET 与 .NET 8 SDK 导入对应工程，按 F5 后从开场卡片开始。3D 工程需要同级的 2D 源码。 [2D 运行与按键](BorrowingSun2D/README.md) · [3D 运行与按键](BorrowingSun3D/README.md)
2. 对照两版的类关系与迁移变化。CCR 候选值为 `2/5 = 40%`。 [2D 类图](BorrowingSun2D/Docs/2D-to-3D/RELATIONSHIP_MAP.svg) · [3D 类图](BorrowingSun3D/Docs/RELATIONSHIP_MAP.md) · [变化图与复算](BorrowingSun3D/Docs/CHANGE_MAP.md)
3. 按固定版本复跑开场、发车反例、签收 `1/1` 与 `R` 后重走。 [macOS 已有证据](BorrowingSun3D/Docs/ROUND_05D.md#实测与签收--2026-11-08) · [Windows 复跑与验收记录](BorrowingSun3D/Docs/WINDOWS_ACCEPTANCE.md)
4. 审阅报告初稿并按演示提纲彩排。Windows 实机验收补齐后再核对最终提交和 `3d-final`。 [报告 PDF](BorrowingSun3D/Docs/output/pdf/AP6412_Borrowing_Sun_Draft.pdf) · [可编辑源稿](BorrowingSun3D/Docs/AP6412_REPORT_DRAFT.md) · [15 分钟演示](BorrowingSun3D/Docs/DEMO_15_MIN.md) · [交付清单](BorrowingSun3D/Docs/ROUND_06_PREP.md)

同学在 11 月 7 日的架构分支与测试记录保留在历史。 [原始分支提交](https://github.com/buriallsy/coding_team/commit/181ec16361f5c22fecd7594519111068cac55baf) · [该分支验证记录](BorrowingSun2D/Docs/Evidence/2026-11-07-framework/RESULTS.md)
