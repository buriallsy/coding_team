# 第 05D 轮 · 首玩与双平台验收

**分支** `buriallsy/3d-graybox` · **起点** 05C `7f80c96` · **2D 基线** `2d-baseline`（`6175db9`）

将 2D 第 04 轮的开场、发车反馈和签收复玩接到完整 3D 路线。开场说明借雨升塔、借风接桥、北庭调镜和东岸送货。签收页显示实际的「水果交付 1/1」。`TowerGraybox` 管理开场、运行和签收三个会话状态，卡片只显示状态并回传按钮操作。进入游戏后仍用实体接桥、两段真实反射和车台碰撞完成交付。 [3D 操作](../README.md) · [2D 第 04 轮](../../BorrowingSun2D/Docs/2D-to-3D/ROUND_04.md) · [关系图](RELATIONSHIP_MAP.md)

## 对照验收表

两个版本均从实际默认入口开始。配重不足在正常路线趁未满时触发，失光用例在独立测试阶段用临时实体遮挡第二段反射。保留默认场景的完整重走证据。

| 用例 | 冻结 2D · `2d-baseline` | 3D · 05D 候选 |
| --- | --- | --- |
| 默认开场与开始 | 「借晴」「把一箱水果送到花庭」。开场中行走、天气和货运按键不推进场景。`Enter`、`Space` 或点击开始，`A/D` 行走。 | 同样的目标、输入锁和确认方式。使用 `WASD` 或方向键，向东过低桥，再转北找采日镜。 |
| 配重未满按 `G` | 接光盘亮、已装箱、玩家站上车，配重未满时拒绝，提示等待转水。 | 同一发车规则。检查 HUD 显示配重进度与拒绝原因，车保持停在东岸。 |
| 失光时按 `G` | 实际光束受遮，保留配重，提示恢复来光，车留原位。 | 用实体遮挡确认接光盘变暗，已装箱且配重满时也拒绝发车。恢复受光且玩家站稳在车台中央才放行。 |
| 发车、到站与签收 | 搭车到花庭，走到旗标按 `G`，签收页显示 `1/1`。重复按 `G` 仍只记一箱。 | 搭实体车沿东向跨谷，走下车到花庭标记按 `G`。签收页显示 `1/1`，停留时行走和重复 `G` 均不改变已签收的现场。 |
| `R` 与再玩 | `R` 直接回西岸低桥，复位后再次签收 `1/1`。签收页也可按 `Enter`、`Space` 或点「再玩一次」。 | 同样回西岸低桥，核对塔、镜、配重、货车和计数复位。用实际按键重新走完路线到 `1/1`，再试签收页确认按钮或按键。 |

## Windows 复跑

本页保留 05D 当时的对照记录。后续的 Windows 托管无头、开窗截图与人工验收进度见 [Windows 验收交接](WINDOWS_ACCEPTANCE.md)。

使用 Godot 4.7.1 .NET 的 Windows 控制台可执行文件和 .NET 8 SDK。先在独立的干净检出切到冻结基线，从仓库根目录运行。请替换示例安装路径。

```powershell
$godot = 'C:/Godot/Godot_v4.7.1-stable_mono_win64_console.exe'
git fetch origin --tags
git switch --detach 2d-baseline
git rev-parse HEAD
dotnet --version
& $godot --version
./Tools/verify.ps1 -GodotBin $godot
```

2D 脚本在 `artifacts/verification` 留下构建与路线日志。核对 `DOMAIN_CHECKS PASSED`、`TOWER_ROUTE PASSED` 和 `ROUND_04_ACCEPTANCE PASSED`。需用 Godot 自带本地包时，在脚本命令后加 `-Offline`。另开正常 2D 窗口完成一次交付和一次 `R` 后的交付。 [2D 手动路线与验收](../../BorrowingSun2D/Docs/2D-to-3D/ROUND_04.md)

3D 使用另一个干净检出，保留仓库根目录下的两个工程。检出本轮候选代码的完整 SHA，并在该检出的仓库根目录运行。

```powershell
$ErrorActionPreference = 'Stop'
$godot = 'C:/Godot/Godot_v4.7.1-stable_mono_win64_console.exe'
$candidate = 'fb4cbb451c028234dd093576fa8c0e6e72bb457e'
git fetch origin buriallsy/3d-graybox
if ($LASTEXITCODE -ne 0) { throw 'Candidate fetch failed.' }
git switch --detach $candidate
if ($LASTEXITCODE -ne 0) { throw 'Candidate checkout failed.' }
git rev-parse HEAD
dotnet --version
& $godot --version
$project = (Resolve-Path 'BorrowingSun3D').Path
$logs = (New-Item -ItemType Directory -Force 'artifacts/verification/round-05d').FullName
dotnet build (Join-Path $project 'BorrowingSun3D.csproj') 2>&1 | Tee-Object (Join-Path $logs 'build.log')
if ($LASTEXITCODE -ne 0) { throw '3D build failed.' }
& $godot --headless --path $project --editor --import --log-file (Join-Path $logs 'import.log')
if ($LASTEXITCODE -ne 0) { throw '3D import failed.' }
$routeLog = Join-Path $logs 'route-headless.log'
& $godot --headless --path $project --fixed-fps 60 --quit-after 5000 `
  --log-file $routeLog -- --verify
if ($LASTEXITCODE -ne 0 -or -not (Select-String -LiteralPath $routeLog -Pattern '^GRAYBOX_ROUTE PASSED .*failures=0' -Quiet)) {
    throw '3D route did not report success.'
}
```

正常开窗用 `& $godot --path $project`。手动核对开场汉字与键位、默认焦点和点击开始、配重提示、签收页 `1/1` 与 `R` 后再次交付。窗口证据建议覆盖 1280×720 和小窗口，留存开场、发车前、两次签收的截图。另可将自动路线命令去掉 `--headless`，将 `--log-file` 换成独立窗口日志，并在 `--verify` 后附 `"--capture-dir=$logs"`。该次窗口用实际按键验证脚本复跑，另存 `intro.png`、`cargo-ready.png`、`cargo-delivered.png`。 [3D 验证入口](../Scripts/Verification/GrayboxRouteCheck.cs)

## macOS 复跑

使用与 Windows 相同的两份源码参考。2D 在 `2d-baseline` 的独立检出运行 `Tools/verify-macos.sh`，传入实际的 `BORROWINGSUN_GODOT_BIN`，再开窗确认交付和重玩。3D 在同一个 05D 候选 SHA 的干净检出运行下列命令。

```sh
git rev-parse HEAD
dotnet --version
"$BORROWINGSUN_GODOT_BIN" --version
cd -P BorrowingSun3D
log_dir=$(mktemp -d)
dotnet build BorrowingSun3D.csproj
"$BORROWINGSUN_GODOT_BIN" --headless --path "$(pwd -P)" --editor --import --log-file "$log_dir/import.log"
"$BORROWINGSUN_GODOT_BIN" --headless --path "$(pwd -P)" --fixed-fps 60 \
  --quit-after 5000 --log-file "$log_dir/route-headless.log" -- --verify
grep '^GRAYBOX_ROUTE PASSED .*failures=0' "$log_dir/route-headless.log"
```

正常窗口可手动核对开场、发车提示、签收和重走。发车遮挡反例由自动路线中的实体挡块复现。自动窗口取证在另一次运行去掉 `--headless`、换用窗口日志，并在 `--verify` 后追加 `--capture-dir="$log_dir"`。 [macOS 2D 脚本和历史记录](../../BorrowingSun2D/Docs/2D-to-3D/ROUND_04.md#macos-复跑)

## 实测与签收 · 2026-11-08

macOS 26.5.2 arm64，Godot 4.7.1 .NET，.NET SDK 8.0.424。2D 从标签 `2d-baseline` 导出，3D 从独立无缓存副本构建。3D 构建零警告、零错误。 [构建](Evidence/round-05d/3d-build.log) · [Godot 导入](Evidence/round-05d/3d-import.log)

| 参考 | macOS | Windows |
| --- | --- | --- |
| 2D `2d-baseline`（`6175db9`） | 无头路线 154 项，覆盖发车反例、交付和 `R` 重走。1280×720 窗口的一趟正常路线通过 46 项，重新截图核对开场及结束页。 [无头](Evidence/round-05d/2d-headless.log) · [窗口](Evidence/round-05d/2d-window.log) · [开场](Evidence/round-05d/2d-intro.png) · [签收](Evidence/round-05d/2d-finished.png) | 待验收。 |
| 3D 05D 代码候选（`fb4cbb4`） | 无头与 1280×720 窗口各通过 160 项。开场默认聚焦「开始」，玩法输入停住，按 `Enter` 进入。配重不足或断光均拒发车，发车后断光仍到站。`G` 签收显示 `1/1`，`R` 恢复西岸低桥并再次交付，第二次结束页按 `Space` 也能重开。 [无头](Evidence/round-05d/3d-headless.log) · [窗口](Evidence/round-05d/3d-window.log) · [开场](Evidence/round-05d/3d-intro.png) · [待发车](Evidence/round-05d/3d-ready.png) · [签收](Evidence/round-05d/3d-finished.png) | 待验收。 |

窗口复核发现开场按键说明末字单独换行。缩短文案后，1280×720 和 960×600 窗口下的开场卡片、按键和焦点均完整。960×600 窗口按项目比例输出 960×540 游戏画面，本次只核对开场。初次图形取证还遇到等待自动绘制信号而停住，改为在截图时主动绘制后通过。2D 的改动只用于隔离副本里的窗口取图探针。基线无头验收仍使用原探针。 [小窗口开场](Evidence/round-05d/3d-intro-small.png) · [小窗口取图日志](Evidence/round-05d/3d-small-window.log) · [Godot 主动绘制接口](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html#class-renderingserver-method-force-draw)

本轮窗口和无头路线均由实际按键事件自动复跑。Windows 验收需要使用同一个候选代码 SHA，记录系统、Godot、SDK、完整路线结果和窗口证据。两端验收通过后，核对 [变化图与 CCR](CHANGE_MAP.md) 以及最终发布提交中的构建输入，再打 `3d-final` 标签。
