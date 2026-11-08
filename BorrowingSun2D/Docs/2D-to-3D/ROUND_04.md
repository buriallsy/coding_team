# 第 04 轮 · 首玩与验收

这一轮补齐从开场、首次操作到花庭签收的界面。验收基于第 03 轮的双镜、升塔、转水和实体货运。 [第 03 轮整合复测](../Evidence/2026-11-08-round03-integration/RESULTS.md) · [冻结前计划](PRODUCTION_PLAN.md#第-04-轮冻结前)

## 试玩步骤

1. 打开项目后按 `Enter`、`Space` 或点击开始。用 `D` 向右走上塔，按 `1`、`E` 施雨，等塔升高并扣锁。
2. 按 `2`、`E` 接北桥，向左走到采日镜。按 `2`、`E` 调镜，再按 `3`、`E` 借光。
3. 回塔按 `2`、`E` 转东，向右走上货车。观察东盘与配重，提示可发车时按 `G`。
4. 到花庭走近交付旗标，按 `G` 签收。结束页显示交付 `1/1`。按 `R` 直接回西岸，再跑到 `1/1`。 [完整键位](../../README.md)

## 本轮复测 · 2026-11-08

在 macOS arm64 用 Godot 4.7.1 .NET 与 .NET SDK 8.0.424，从干净副本构建，零警告、零错误。自动输入走过正式场景的低桥、升降塔、北庭、东岸和车轨。正常两趟与临时实体挡块用例各自记录。 [构建日志](../Evidence/round-04/clean-build.log) · [本轮完整日志](../Evidence/round-04/round04-acceptance.log)

| 用例 | 结果 |
| --- | --- |
| 开场 | 正常进入先显示引导。开场期间移动、天气、货运和复位键不推进场景。按 `Enter` 从实际入口开始。 |
| 发车提示 | 配重未满显示转水进度，按 `G` 拒绝并说明原因。挡住真实光束时保留配重，显示失光并拒绝发车。恢复受光、配重满且站在车上时显示 `G 发车`。 |
| 签收与重开 | 搭实体车到站，走到旗标按 `G`，结束页显示 `1/1`。重复按 `G` 仍为一箱。按 `R` 从西岸低桥出发，再完成 `1/1`。 |
| 回归 | 完整验收 154 项通过。三岸内置 112 项、独立按键 71 项、双镜光路 57 项、领域 80 项、CCR 工具 7 项也通过。 |

结束页显示签收时的现场。首轮验证沿用旧夹具，在签收后移除挡块并等待背景重新接光。调整后的验收用物理射线确认临时挡块已移除，同时核对结束页上的单次签收和重开行为。 [回归对照](../Evidence/round-04/source-and-regression-comparison.log)

1280×720 窗口的一趟正常路线通过 46 项检查并取到三张原帧。在 960×600 窗口里也确认了开场卡片和按钮完整显示。Windows 验收与首次玩家观察待补。 [开场](../Evidence/round-04/intro.png) · [可发车](../Evidence/round-04/ready.png) · [签收页](../Evidence/round-04/finished.png) · [小窗口](../Evidence/round-04/intro-960x600.png) · [窗口日志](../Evidence/round-04/round04-graphics.log) · [尺寸检查](../Evidence/round-04/round04-resized.log)

## Windows 验收

用 Godot 4.7.1 .NET 和 .NET 8 SDK。在单独的干净检出里打开 PowerShell，从仓库根目录运行下面的命令。将示例路径换成 Godot .NET 自带的控制台可执行文件。

```powershell
git fetch origin buriallsy/round-04-acceptance
git switch --detach FETCH_HEAD
git rev-parse HEAD
./Tools/verify.ps1 -GodotBin 'C:/Godot/Godot_v4.7.1-stable_mono_win64_console.exe'
```

若使用 Godot 自带的本地包，在脚本命令末尾加 `-Offline`。脚本把日志写入 `artifacts/verification`。运行后用编辑器打开 `BorrowingSun2D/project.godot`，按 F5 看正常开场，按上面路线手动完成两趟，核对中文提示、结束页和 `R`。请记录候选提交 SHA、Windows 版本、Godot 与 .NET 版本，以及脚本结果和两趟的签收截图。首玩观察单独记录实际卡点、误操作和用时。

## macOS 复跑

同样在仓库根目录执行。将变量换成所用 Godot .NET 应用内的可执行文件。

```sh
BORROWINGSUN_GODOT_BIN=/Applications/Godot_mono.app/Contents/MacOS/Godot bash Tools/verify-macos.sh
```

离线环境可同时设置 `BORROWINGSUN_NUGET_SOURCE`，值为 Godot .NET 应用中的 `GodotSharp/Tools/nupkgs` 目录。macOS 脚本用两个干净副本验证正式场景和带探针的场景。 [macOS 脚本结果](../Evidence/round-04/macos-verifier.log)

两端脚本以 `DOMAIN_CHECKS PASSED`、`TOWER_ROUTE PASSED` 和 `ROUND_04_ACCEPTANCE PASSED` 确认运行结果。第 04 轮路线从正常开场启动，保留实际走路、调镜、受光、转水、乘车和重开。日志统一在 `artifacts/verification`。

## 冻结门槛

以同一个候选提交汇总 macOS 与 Windows 的干净检出结果。两端均完成并重开再完成后，确认类图、CCR 候选类和剩余限制，再创建 `2d-baseline`。第 05 轮从这个标签开始做 3D。 [完整验收矩阵](ACCEPTANCE.md)
