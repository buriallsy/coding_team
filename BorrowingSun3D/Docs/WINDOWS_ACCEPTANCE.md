# Windows x64 验收交接

验收使用冻结 2D `2d-baseline`（`6175db93b0156b35fb736af2e28cc97aaa65d470`）与 3D 05D 代码候选 `fb4cbb451c028234dd093576fa8c0e6e72bb457e`。macOS 已复跑两版。Windows Server 托管任务负责无头路线，Windows 桌面另跑实际窗口与人工两趟。`3d-final` 等两端结果齐全。 [05D 对照及 macOS 证据](ROUND_05D.md) · [第 06 轮提交准备](ROUND_06_PREP.md)

## 托管无头验证

打开仓库 Actions 的 **Godot Windows headless**。查看同一次运行内的 2D 基线与 3D 候选步骤，保存运行链接及 `windows-godot-headless-<运行尝试次数>` 产物。产物含源码和工作流 SHA、环境信息及两版日志。2D 在冻结标签运行。3D 使用包含新验证脚本的分支检出，核对实际工程及共享源码与 05D 代码候选一致后运行。日志中的 `PASSED` 属于对应平台的自动按键路线。 [工作流](../../.github/workflows/godot-windows.yml)

## Windows 桌面复跑

准备 **Godot 4.7.1 .NET**、**.NET 8 SDK** 和 **PowerShell 7.2 或更新版本**。完整解压官方 `Godot_v4.7.1-stable_mono_win64.zip`，保留 `GodotSharp` 目录并使用包内 `_console.exe`。将命令中的安装路径换成本机路径。 [Godot 官方发行页](https://github.com/godotengine/godot-builds/releases/tag/4.7.1-stable)

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

两次验证都应输出 `GRAYBOX_ROUTE PASSED assertions=… failures=0` 与 `3D WINDOWS VERIFICATION PASSED`。默认输出在 `artifacts/verification/windows-3d`，可用 `-LogDir` 指定其它目录。两种模式分别保存 `metadata-*.txt`、`build-*.log`、`import-*.log` 和 `route-*.log`。窗口版以 1280×720 跑实际按键路线，在 `captures` 保存 `intro.png`、`cargo-ready.png`、`cargo-delivered.png` 等。最后一行启动正常 3D 窗口供人工操作。 [3D 操作路线](../README.md) · [3D 验证脚本](../../Tools/verify-windows-3d.ps1)

### 人工窗口核对

1. 两版各看一次正常开场。确认中文标题、目标、按键和默认焦点完整。分别试键盘确认与鼠标点击。在 1280×720 和一个较小窗口核对开场和签收页的可读性。
2. 从低桥走完升塔、调镜、接光、登车、到站和签收。记录第一次 `1/1`。配重不足与实体断光的发车拒绝可对应自动日志中的反例。
3. 在签收页按 `R`，检查回到西岸低桥，走完整条路线再次签收 `1/1`。保留开场、待发车、两次签收与重开的截图。若有第一次接触游戏的同学，另记卡点、误操作、是否独立完成与实际用时。 [两版共用验收情景](ROUND_05D.md#对照验收表)

## 回填记录

| 证据 | 2D 冻结基线 | 3D 05D 候选 |
| --- | --- | --- |
| Windows Server x64 托管无头 | 待记录运行链接、标签 SHA、通过项和日志。 | 待记录运行链接、候选与工作流 SHA、通过项和日志。 |
| Windows x64 桌面 | 待记录本机无头与人工窗口结果。 | 待记录本机无头、自动窗口与人工窗口结果。 |
| 人工签收两趟 | 待填第一次、`R` 回低桥、第二次的证据。 | 待填第一次、`R` 回低桥、第二次的证据。 |

每次记录 Windows 版本、架构、Godot 完整版本、.NET SDK、执行日期和使用的源码 SHA。失败时保留原日志与触发输入。证据文件可归档到 `BorrowingSun3D/Docs/Evidence/windows-05d/`，并在本页补相对链接。首次玩家观察和自动断言单独标记。
