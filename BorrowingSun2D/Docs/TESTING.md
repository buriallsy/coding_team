# 2026-11-06 · 2D 灰盒测试记录

当前双镜光路见 [第 02 轮验收](2D-to-3D/ROUND_02.md)，三岸与低桥基线见 [第 01 轮验收](2D-to-3D/ROUND_01.md)。下方是第 00 轮的原始测试过程与复位问题，复现时使用 [第 00 轮提交](https://github.com/buriallsy/coding_team/commit/f6900c483680de7f1d21a4768720753b6f1512d7)。

## 环境与运行方式

使用 macOS arm64、Godot `4.7.1.stable.mono.official.a13da4feb` 和 .NET 8 SDK `8.0.424`，在实际 Godot 物理运行中测试。工程入口是 `Scenes/TowerSlice.tscn`。两条自动路线分别使用 Input Map 动作与 Godot `InputEventKey`，由生产场景处理。测试代码读取状态和碰撞，玩家的正常步行、随台运动、桥启闭和抵达均由场景执行。

在本工程目录运行以下命令。`BORROWINGSUN_GODOT_BIN` 指向 Godot .NET 可执行文件。

```sh
dotnet build
"$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD" --fixed-fps 60 -- --verify
qa_dir=$(mktemp -d)
cp Docs/Evidence/independent-route.gd.txt "$qa_dir/independent-route.gd"
"$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD" --script "$qa_dir/independent-route.gd"
```

macOS 沙箱中的运行日志保留了系统 CA 读取提示。关卡测试依赖本地资源，最终进程正常退出。无头测试跳过图形绘制，图形画面采用下文独立记录。

## 测试过程与证据

| 运行 | 方法 | 结果 |
| --- | --- | --- |
| 首次内置路线 | 默认入口连续输入，包含上塔、随台升降、转桥、过桥、抵达、R 后初始状态检查 | 20 条断言通过。这个覆盖只检查 R 后的初态。[日志](Evidence/00-initial-route.log) |
| 首次独立键码路线 | 首趟到高位试探缺桥，R 后实际走低位桥，继续复测 | 在第二趟发现低位桥无法承托角色，随后高位路线断言连锁失败。[失败日志](Evidence/01-independent-reset-failure.log) |
| 复位碰撞诊断 | 读取活动台节点、真实物理刚体的变换，向低桥与高桥射线探测 | 节点位于 `(660,590)`，物理刚体滞留在 `(660,300)`，低桥无命中、高桥命中。[诊断日志](Evidence/02-reset-collision-diagnostic.log) |
| 修复后内置回归 | 走到采光庭，R 后真实再上塔，再升降途中 R 并重走低桥 | 26 条断言通过，退出 0。[日志](Evidence/03-built-in-route-pass.log) |
| 修复后独立键码回归 | 用实际按键映射测设备范围、门槛、收令、排水、高位缺桥、转桥与抵达。高位和移动中各 R 一次，再走低桥 | 28 条断言通过，退出 0。两次节点与物理体均回到 `(660,590)`，低桥命中、高桥无命中。到达采光庭时角色约在 `(237.93,282.93)` 且站在实体地面。[日志](Evidence/04-independent-route-pass.log) |
| 无缓存副本 | 只复制工程配置、场景和 C# 源码到独立目录，用本机 Godot .NET SDK 缓存构建后直接运行 | 构建 0 警告、0 错误，26 条断言通过、退出 0。[日志](Evidence/05-clean-copy-route.log) |
| 图形运行 | 用同一个键码脚本在 macOS 图形环境跑默认场景，到旗标后读出 Godot viewport 原帧 | 29 条断言通过，含画面捕获，退出 0。截图中可见满水、+6 m、高位锁、北桥接通和抵达提示。[日志](Evidence/06-rendered-route-pass.log) · [1280×720 原帧](Evidence/sun-court.png) |

独立键码用例源文件为 [independent-route.gd.txt](Evidence/independent-route.gd.txt)。记录中的坐标是 Godot 2D 画面坐标，游戏内读数用相对升降米数。测试结论覆盖本版路线、碰撞、边界和复位，首次玩家理解和跨平台运行待后续验证。

默认启动的窗口也已打开并目检中文 HUD。桌面控制在窗口焦点切换时停止，因此完整路线证据以独立脚本驱动的真实 Godot 场景为准。
