# 第 05C 轮 · 跨谷交付

**分支** `buriallsy/3d-graybox` · **起点** 05B `acad91a` · **2D 基线** `2d-baseline`（`6175db9`）

把第 05B 轮的真实接光接到货运。东岸持续受光后装一箱水果，升塔时蓄下的雨水转入配重。玩家走上实体车台、搭车跨谷，到花庭标记确认交付 `1/1`。 [当前操作](../README.md) · [3D 关系图](RELATIONSHIP_MAP.md) · [2D 货运对照](../../BorrowingSun2D/Docs/2D-to-3D/ROUND_03.md)

## 设计与移植

| 行为 | 本轮接法 |
| --- | --- |
| 受光与装货 | `SunCourtOptics3D` 提供当帧的实际 `ReceiverLit`。持续接光 1.5 秒后出现一箱水果。完成计时前断光会清掉这段连续计时，装好的水果留到交付。 |
| 原水转配重 | 东岸停靠且实时受光时，原先储存的 6 单位雨水以 1 单位/秒自动转移，配重最多 4，塔箱留 2。机械锁维持 6 m 高位。断光暂停转水，保留已有配重，恢复受光后继续。 |
| 发车 | 货物、实时受光、东岸停靠和满配重都成立，且玩家已在车台中央落脚时，按 `G` 发车。 |
| 跨谷 | 发车后按共享的 3.4 秒行程运行。玩家由活动车台物理承托，行驶中暂停步行输入。此时断光，货车仍会到站。 |
| 花庭确认 | 停靠后按 `D` 走下车，在花庭标记旁落脚，按 `G` 确认。果箱消失，交付计数到 `1/1`，重复确认保留原计数。 |
| 重开 | `R` 回西岸，清除光路、校准、到访、货运和塔机状态，并让实体车台回发车端。 |

`CargoJourney` 通过编译链接沿用冻结的 2D 源码。每个物理帧先更新塔台姿态与光路，再以当帧受光推进货运规则、处理 `G`。`CargoTrack3D` 按 `TravelProgress` 移动实体车台，按 `HasFruit` 显示果箱。HUD 读取同一份塔机、光路和货运状态，显示发车拒绝原因、配重进度及交付结果。 [编译链接](../BorrowingSun3D.csproj) · [共享货运](../../BorrowingSun2D/Scripts/Core/CargoJourney.cs) · [场景协调](../Scripts/TowerGraybox.cs) · [车台适配](../Scripts/CargoTrack3D.cs)

## 实体车轨

发车标记为 `(13.38, 6, 0)`，到站标记为 `(20.62, 6, 0)`。车台沿东向移动 7.24 m，使用第 2 物理层的 `AnimatableBody3D` 承托角色。花庭岸面从 `X = 22.10 m` 开始，交付标记在 `(24.90, 6, 0)`。玩家用实际碰撞登车、随车、下车。到站状态允许走上花庭，签收仍取决于玩家站位和按键。 [作者场景](../Scenes/TowerGraybox.tscn) · [Godot 活动车体](https://docs.godotengine.org/en/stable/classes/class_animatablebody3d.html)

## 复跑

从仓库根目录执行。用 Godot 4.7.1 .NET 和 .NET 8 SDK。`BORROWINGSUN_GODOT_BIN` 指向 Godot 可执行文件。

```sh
cd -P BorrowingSun3D
log_dir=$(mktemp -d)
dotnet build BorrowingSun3D.csproj
"$BORROWINGSUN_GODOT_BIN" --headless --path "$(pwd -P)" --fixed-fps 60 \
  --quit-after 5000 --log-file "$log_dir/route.log" -- --verify
grep '^GRAYBOX_ROUTE PASSED' "$log_dir/route.log"
```

窗口取证去掉 `--headless`，按需加 `--resolution 1280x720 --audio-driver Dummy`，在 `--verify` 后追加 `--capture-dir="$log_dir"`。运行脚本从生产场景默认入口发出真实按键。断光用例使用临时实体遮挡，正常路线和重走按默认场景检查。 [验证入口](../Scripts/Verification/GrayboxRouteCheck.cs)

## 实测记录 · 2026-11-08

macOS 26.5.2 arm64，Godot 4.7.1 .NET，.NET SDK 8.0.424。从无 Godot 缓存的源码副本构建和导入，构建为 0 警告、0 错误。原有领域夹具通过 80 项，无头与窗口实际按键路线各通过 142 项。 [构建](Evidence/round-05c/graybox-build.log) · [导入](Evidence/round-05c/graybox-import.log) · [领域夹具](Evidence/round-05c/domain-checks.log) · [无头](Evidence/round-05c/graybox-headless.log) · [窗口](Evidence/round-05c/graybox-window.log)

| 用例 | 实测结果 |
| --- | --- |
| 连续接光与装箱 | 接近 1.5 秒时车上仍没有果箱，也没开始转水。持续接光达到阈值后出现一箱。 |
| 转水时塔高 | 逐帧采样 1539 次，塔箱与配重总量始终为 6，实体塔台高度始终为 6 m。满配重时塔箱剩 2、配重为 4。 |
| 断光暂停与续转 | 在第二段反射中插入物理挡块，接光盘熄灭。暂停时塔箱约 2.57、配重约 3.43，果箱和配重保留。移除挡块后从原进度续转。 |
| 发车门槛 | 按 `G` 验证有光但配重不足、断光且配重部分或已满三种情况，货车均留在发车点。恢复光且配重已满，车上的玩家按 `G` 才能发车。 [发车前画面](Evidence/round-05c/cargo-ready.png) |
| 断光后到站 | 发车后再挡光，货车保持行驶，实体车面承托玩家跨越 7.24 m 到站，到站时接光盘仍暗。到站时果箱仍在，交付为 `0/1`。 |
| 花庭签收与重走 | 在车上按 `G` 拒绝签收。实走到花庭标记后按 `G`，变为 `1/1`，重复按键也只计一次。按 `R` 后车回发车端，塔、配重和果箱复位，低桥重新接通。无挡块的第二趟再次交付 `1/1`。 [西岸入口](Evidence/round-05c/west-entrance.png) · [重走交付原帧](Evidence/round-05c/cargo-delivered.png) |

首轮窗口复核发现，遮挡移除后 HUD 会暂留旧的断光提示。现在接光状态或货运阶段改变时立即刷新该提示。最终窗口在相同测试点显示「配重已满，按 G 跨谷」。 [首轮画面](Evidence/round-05c/cargo-ready-first.png) · [复验画面](Evidence/round-05c/cargo-ready.png)
