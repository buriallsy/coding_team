# 交接 · 2026-11-06

当前第 01 轮覆盖「西岸上塔 → 雨升 → 北庭 → 回塔 → 东岸」。打开 [project.godot](../project.godot) 或读 [运行说明](../README.md)。课程报告可从 [第 01 轮记录](2D-to-3D/ROUND_01.md) 和 [迭代记录](ITERATIONS.md) 引用本轮用例与日志。下一轮制作实际反射光路，正式 2D 基线在整条主线完成后冻结。

## 第 00 轮归档

- **首轮实现**。2D 行走与活动平台使用真实碰撞。雨让 6 单位储水抬高塔台 6 m，机械锁保高。风转到北侧停点后桥舌才承托玩家。当时以北庭旗标为终点。
- **修复**。R 曾使场景节点先回到低位，物理体留在高位。复位已复用正常升降的物理同步更新，新增重走低桥、再次起升、运动中重置与完整独立路线回归。无头 26 条内置断言、28 条独立键码断言通过。图形运行 29 条断言通过，含真实终点捕获。
- **Figma**。原文件 `ntu` 的「作业-generative」页右侧新增 [Godot 2D 灰盒 / 过程记录](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=471-55)，含 [空间剖面](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=472-55)、[Flowchart](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=473-55)、[Relationship Map](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=474-55) 和 [测试迭代](https://www.figma.com/design/UINwS4sIWy2BFhFRFcJ0pB/ntu?node-id=475-55)。四张画板均为 1920×1080，逐张目检和文本边界检查完成。最后一页保留实跑终点画面，其余剖面、流程、关系和说明为可编辑元素。
- **首轮核对**。独立核对原 Unity `Assets`、`Packages` 与 `ProjectSettings` 的 600 个作者文件，前后 SHA-256 一致。
