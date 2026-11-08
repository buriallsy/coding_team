# Windows x64 验收交接

验收使用冻结 2D `2d-baseline`（`6175db93b0156b35fb736af2e28cc97aaa65d470`）与 3D 05D 代码候选 `fb4cbb451c028234dd093576fa8c0e6e72bb457e`。macOS 已复跑两版，Windows Server 已完成托管无头和开窗预验。Windows 实机人工窗口仍待复跑，完成后再确认 `3d-final`。 [05D 对照及 macOS 证据](ROUND_05D.md) · [第 06 轮提交准备](ROUND_06_PREP.md)

## 托管无头验证

2026-11-09，GitHub Actions 的 Windows Server 2025 x64 任务通过。环境为 Godot 4.7.1 .NET 与 .NET SDK 8.0.424。工作流提交 `9c36355` 的 3D 构建输入经核对与 `fb4cbb4` 一致。2D 实际按键路线通过 154 项，3D 通过 160 项，均为零失败。 [运行与完整产物](https://github.com/buriallsy/coding_team/actions/runs/42501063399) · [持久日志摘要](Evidence/round-06/windows-headless.log)

打开仓库 Actions 的 **Godot Windows headless**。查看同一次运行内的 2D 基线与 3D 候选步骤，保存运行链接及 `windows-godot-headless-<运行尝试次数>` 产物。产物含源码和工作流 SHA、环境信息及两版日志。2D 在冻结标签运行。3D 使用包含新验证脚本的分支检出，核对实际工程及共享源码与 05D 代码候选一致后运行。日志中的 `PASSED` 属于对应平台的自动按键路线。 [工作流](../../.github/workflows/godot-windows.yml)

## 托管开窗预验

2026-11-09（新加坡时间），在 GitHub Actions 的 Windows Server 2025 x64 图形会话中运行两版。PowerShell、Explorer 与桌面合成器同在会话 2。Godot 为 `4.7.1.stable.mono.official.a13da4feb`，.NET SDK 为 `8.0.424`。两版由 Windows 显示驱动开窗，经 ANGLE 走 Direct3D 11，使用 Microsoft Basic Render Driver。测试申请 1280×720 窗口，桌面为 1024×768，实际保存的游戏视口为 1028×578。 [环境原始记录](Evidence/windows-05d/hosted/metadata.txt)

从各自主场景启动，探针以 60 fps 固定步进向 Godot 发送 `Enter`、移动、天气、`G` 和 `R` 的键盘事件。两版均从开场走到花庭按 `G` 签收 `1/1`，再从签收页按 `R` 回到西岸，走过实体低桥并重走全线，再次签收 `1/1`。2D 开窗跑的是两趟正常路线，记录配重等待提示，拒发用例见其无头日志。3D 第一趟覆盖配重不足和实体遮挡断光的拒发用例，第二趟全程使用默认场景。两版原图均已核对，开场和结束页的中文、操作提示、分数与按钮焦点可辨认。 [2D 拒发的无头记录](Evidence/round-05d/2d-headless.log)

首跑取证使用 QA 分支提交 `6d25c0a`。两版构建输入匹配各自固定版本，工作流只在临时测试副本内调整探针。2D 加入 `R` 后完整第二趟与截图，3D 在原有路线中增加截图与显示驱动记录。 [取证脚本与工作流](https://github.com/buriallsy/coding_team/commit/6d25c0a28e4a11cea3a757ebb061414823d44e1c)

| 版本 | 结果 | 原始视口截图 |
| --- | --- | --- |
| 2D `6175db9` | 90 项通过，0 项失败。 [运行日志](Evidence/windows-05d/hosted/2d/route.log) | [开场](Evidence/windows-05d/hosted/2d/intro.png) · [待发车](Evidence/windows-05d/hosted/2d/cargo-ready.png) · [首次交付](Evidence/windows-05d/hosted/2d/cargo-delivered-first.png) · [R 回低桥](Evidence/windows-05d/hosted/2d/west-after-r.png) · [再次交付](Evidence/windows-05d/hosted/2d/cargo-delivered.png) |
| 3D `fb4cbb4` | 160 项通过，0 项失败。 [运行日志](Evidence/windows-05d/hosted/3d/route.log) | [开场](Evidence/windows-05d/hosted/3d/intro.png) · [待发车](Evidence/windows-05d/hosted/3d/cargo-ready.png) · [首次交付](Evidence/windows-05d/hosted/3d/cargo-delivered-first.png) · [R 回低桥](Evidence/windows-05d/hosted/3d/west-after-r.png) · [再次交付](Evidence/windows-05d/hosted/3d/cargo-delivered.png) |

首跑的两版路线已退出并输出 `PASSED`。Actions 在后置截图检查标红，原因是测试脚本要求精确的 16:9 整数比例，实际视口取整的高度差为 0.25 像素。仓库保留该次原始 PNG 和路线日志。原始产物 ZIP 的 SHA-256 为 `abf58c6485edc687ebfa2845d4f517e43728bfe134b0f1c3cd1b6a694348694c`。 [首跑任务](https://github.com/buriallsy/coding_team/actions/runs/42502404839) · [完整产物](https://github.com/buriallsy/coding_team/actions/runs/42502404839/artifacts/13626431731)

QA 脚本允许不超过 1 像素的高度取整误差，保留 960×540 的最小视口要求。修正后重跑两版，路线、截图检查与产物上传均通过。 [QA 修正](https://github.com/buriallsy/coding_team/commit/e57b3c88f4d752d98053d63eeb33ae8f5447bfe2) · [复跑任务](https://github.com/buriallsy/coding_team/actions/runs/42503364565/job/128800674252) · [复跑产物](https://github.com/buriallsy/coding_team/actions/runs/42503364565/artifacts/13626142641)

这轮证据覆盖托管图形会话中的渲染和自动输入。Windows 实机上的鼠标点击、键盘焦点切换、默认显卡驱动以及人工两趟仍待复跑。

## Windows 桌面复跑

准备 **Godot 4.7.1 .NET**、**.NET 8 SDK** 和 **PowerShell 7.2 或更新版本**。完整解压官方 `Godot_v4.7.1-stable_mono_win64.zip`，保留 `GodotSharp` 目录并使用包内 `_console.exe`。将命令中的安装路径换成本机路径。 [Godot 官方发行页](https://github.com/godotengine/godot-builds/releases/tag/4.7.1-stable)

如果 `dotnet --version` 默认选中了另一个大版本，用 `dotnet --list-sdks` 找到已安装的 8.0 SDK，在两个临时验收检出共同的上级目录运行 `dotnet new globaljson --sdk-version 8.0.424 --roll-forward latestPatch`。将示例版本换成本机的 8.0 SDK 版本，再从两个检出分别确认 `dotnet --version`。

### 2D 冻结基线

在单独的干净检出根目录运行。脚本在 `artifacts/verification` 留下构建及无头路线日志。核对 `DOMAIN_CHECKS PASSED`、`TOWER_ROUTE PASSED` 和 `ROUND_04_ACCEPTANCE PASSED`。

```powershell
$ErrorActionPreference = 'Stop'
$godot = 'C:/Godot/Godot_v4.7.1-stable_mono_win64_console.exe'
git fetch origin --tags
if ($LASTEXITCODE -ne 0) { throw 'Baseline tag fetch failed.' }
git switch --detach 2d-baseline
if ($LASTEXITCODE -ne 0) { throw '2D baseline checkout failed.' }
git rev-parse HEAD
dotnet --version
& $godot --version
./Tools/verify.ps1 -GodotBin $godot
& $godot --path (Resolve-Path 'BorrowingSun2D').Path
```

最后一行启动正常 2D 窗口供人工操作。需要使用 Godot 随附的本地 NuGet 包时，可给 `verify.ps1` 加 `-Offline`。 [2D 操作路线](../../BorrowingSun2D/README.md) · [2D 验收脚本](../../Tools/verify.ps1)

### 3D 05D 候选

在另一个干净检出根目录运行，保留同级 `BorrowingSun2D` 和 `BorrowingSun3D`。检出最新的 `buriallsy/3d-graybox` 交付分支，核对构建输入与已测候选一致，使用分支随附的 Windows 验证脚本。记录 `git rev-parse HEAD` 输出和固定的候选 SHA。

```powershell
$ErrorActionPreference = 'Stop'
$godot = 'C:/Godot/Godot_v4.7.1-stable_mono_win64_console.exe'
$candidate = 'fb4cbb451c028234dd093576fa8c0e6e72bb457e'
git fetch origin buriallsy/3d-graybox
if ($LASTEXITCODE -ne 0) { throw '3D branch fetch failed.' }
git switch --detach FETCH_HEAD
if ($LASTEXITCODE -ne 0) { throw '3D branch checkout failed.' }
git rev-parse HEAD
$inputs = @('BorrowingSun3D', ':(exclude)BorrowingSun3D/Docs', ':(exclude)BorrowingSun3D/README.md',
    'BorrowingSun2D/Scripts/Core', 'BorrowingSun2D/Scripts/TowerUiFont.cs',
    'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.Config', 'global.json')
git diff --exit-code $candidate HEAD -- @inputs
if ($LASTEXITCODE -ne 0) { throw '3D build inputs differ from the 05D candidate.' }
./Tools/verify-windows-3d.ps1 -GodotBin $godot
./Tools/verify-windows-3d.ps1 -GodotBin $godot -Window
& $godot --path (Resolve-Path 'BorrowingSun3D').Path
```

两次验证都应输出 `GRAYBOX_ROUTE PASSED assertions=… failures=0` 与 `3D WINDOWS VERIFICATION PASSED`。默认输出在 `artifacts/verification/windows-3d`，可用 `-LogDir` 指定其它目录。两种模式分别保存 `metadata-*.txt`、`build-*.log`、`import-*.log` 和 `route-*.log`。窗口版请求 1280×720，实际视口以 `captures` 里的 `intro.png`、`cargo-ready.png`、`cargo-delivered.png` 等截图尺寸为准。最后一行启动正常 3D 窗口供人工操作。 [3D 操作路线](../README.md) · [3D 验证脚本](../../Tools/verify-windows-3d.ps1)

### 人工窗口核对

1. 两版各看一次正常开场。确认中文标题、目标、按键和默认焦点完整。分别试键盘确认与鼠标点击。在 1280×720 和一个较小窗口核对开场和签收页的可读性。
2. 从低桥走完升塔、调镜、接光、登车、到站和签收。记录第一次 `1/1`。配重不足与实体断光的发车拒绝可对应自动日志中的反例。
3. 在签收页按 `R`，检查回到西岸低桥，走完整条路线再次签收 `1/1`。保留开场、待发车、两次签收与重开的截图。若有第一次接触游戏的同学，另记卡点、误操作、是否独立完成与实际用时。 [两版共用验收情景](ROUND_05D.md#对照验收表)

## 回填记录

| 证据 | 2D 冻结基线 | 3D 05D 候选 |
| --- | --- | --- |
| Windows Server 2025 x64 托管无头 | `6175db9`，154 项通过。领域夹具另通过 80 项。 | `fb4cbb4`，160 项通过。工作流来源 `9c36355`。 |
| Windows Server 2025 x64 托管开窗 | `6175db9`，90 项通过。 | `fb4cbb4`，160 项通过。 |
| Windows x64 桌面 | 待记录本机无头与人工窗口结果。 | 待记录本机无头、自动窗口与人工窗口结果。 |
| 人工签收两趟 | 待填第一次、`R` 回低桥、第二次的证据。 | 待填第一次、`R` 回低桥、第二次的证据。 |

每次记录 Windows 版本、架构、Godot 完整版本、.NET SDK、执行日期和使用的源码 SHA。失败时保留原日志与触发输入。证据文件可归档到 `BorrowingSun3D/Docs/Evidence/windows-05d/`，并在本页补相对链接。首次玩家观察和自动断言单独标记。
