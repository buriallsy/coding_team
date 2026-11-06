# 第 03 轮 · 一箱水果

这一轮把借光接到货运。东岸接光盘连续受光后，货车装上一箱水果。升塔存下的雨水流入货车配重，玩家搭车跨过东岸与交付站之间的缺口，到站确认 `1/1`。 [第 02 轮基线](https://github.com/buriallsy/coding_team/commit/e8d9b3e1ad1f43349df9b90249dd608c5df7c93e) · [制作计划](PRODUCTION_PLAN.md)

## 玩一遍

1. 从西岸走过低桥。塔上按 `1`、`E` 施雨，等升高后按 `2`、`E` 接北桥。
2. 走到北庭采日镜旁，按 `2`、`E` 调镜，再按 `3`、`E` 施晴。沿原路回塔，按 `2`、`E` 转向东岸。
3. 东盘受光后，沿东桥走到货车上。等水果出现、配重显示 `4/4`，按 `G` 发车。
4. 车停稳后向右走上交付站，到旗标处按 `G`，确认交付 `1/1`。`R` 回西岸低桥重开。

## 设计与代码

- **连续受光才装货。** 直接读取第 02 轮实际光路的 `ReceiverLit`。装货前连续受光 1.5 秒才出现水果，中途断光重计。一箱已经装车的水果会保留到交付。
- **塔和配重共用一份水。** `TowerMechanism` 持有总量与配重份额。从蓄好的 6 格水中以每秒 1 格转出，至配重满 4 格时留下 2 格在水箱。机械锁在整个转水期间维持高位。断光时保留已转入的水，恢复受光后从原进度继续。
- **发车与到站用不同条件。** `CargoJourney` 用东岸停靠、实时受光、配重满和玩家已经登车决定发车。已发车的货车按行程继续到站。确认交付还需要站到交付旗标旁，重复操作只记录一次。
- **跨谷由实体载具完成。** `CargoTrack2D` 把行程进度投影为 Godot 活动平台的位置；角色经物理碰撞登车、随车移动、到站再步行。`TowerSlice` 负责交互与统一重开，`TowerView` 显示雨水、配重、货车和交付状态。 [对象关系图](RELATIONSHIP_MAP.svg) · [运输规则](../../Scripts/Core/CargoJourney.cs) · [雨水与机械锁](../../Scripts/Core/TowerMechanism.cs)

转水后锁住原排水口，避免已有配重的塔在西向主动排空并释放机械锁。`R` 同时复位水箱、配重、灯光、货车和玩家。

## 规则专项

纯 C# 样例通过 22 项。用断续受光区分持续计时，用黑暗和缺配重分别检查发车门槛，也覆盖发车后断光、重复交付、排水保护和复位。样例给规则层传入受光值，场景测试另用真实光线与碰撞体验证。 [数值脚本](../Evidence/round-03/cargo-rules.cs.txt) · [数值结果](../Evidence/round-03/cargo-rules.log)

## 场景测试 · 2026-11-06

在 macOS arm64 使用 Godot 4.7.1 .NET 与 .NET SDK 8.0.424。干净生产副本构建 0 警告、0 错误。独立脚本从正式场景的默认入口发出按键，完成正常交付、断光测试和 `R` 后的正常重跑。正常路线无夹具，断光测试在第二段反射上放置光学碰撞体，玩家始终靠走路和车台碰撞移动。 [构建日志](../Evidence/round-03/clean-build.log) · [货运无头日志](../Evidence/round-03/round03-cargo.log)

| 验收 | 实测结果 |
| --- | --- |
| 东盘持续受光 | 初次命中和持续时间不足时均无水果。达到 1.5 秒后装一箱，并开始转水。 |
| 同一份雨水与机械锁 | 从塔箱 `6`、配重 `0` 到塔箱 `2`、配重 `4`。共采样 3460 个物理帧，总水量一直是 `6`，台高是 `6 m`，可见塔台和物理刚体始终在 `Y=300`。 |
| 部分转水时断光 | 挡光时塔箱约 `3.70`、配重约 `2.30`。55 个物理帧内保持水量、锁和水果，移除挡块后续转。 |
| 两种发车拒绝 | 已登车且受光，但配重未满时 `G` 被拒绝。配重已满但实时断光时，`G` 也被拒绝。两次行程均为 `0`。 |
| 发车后断光 | 实体车台从 `(1119,300)` 到 `(1183,300)`，移行 `64 px`。角色全程有落脚，最大相对漂移约 `0.314 px`，东盘仍暗时可在花庭按 `G` 交付。 |
| 交付与复位 | 到站后走到花庭旗标再按 `G`，得到 `1/1`。重复按 `G` 仍是 `1/1`。从已交付的花庭按 `R`，实走低桥、再开光和装货，重跑至 `1/1`。 |

货运专项通过 129 项。旧三岸内置、旧三岸独立按键和第 02 轮光路分别通过 112、71 和 57 项。 [三岸内置日志](../Evidence/round-03/round01-builtin.log) · [三岸按键日志](../Evidence/round-03/round01-independent.log) · [光路回归日志](../Evidence/round-03/round02-optics.log)

窗口首跑在低桥后的步行输入中断。重跑时记录原生窗口焦点，失焦释放键位后可在测试脚本内重新送出同一个行走按键。正常路线的窗口复跑通过 36 项，其中两项保存了行车与交付原帧。断光和复位重跑的证据来自完整无头专项。 [窗口首跑](../Evidence/round-03/round03-graphics-first-attempt.log) · [窗口复跑](../Evidence/round-03/round03-graphics.log) · [行车原帧](../Evidence/round-03/travelling.png) · [交付原帧](../Evidence/round-03/delivered.png)

## 复跑方式

在 `BorrowingSun2D` 项目目录运行。`BORROWINGSUN_GODOT_BIN` 指向 Godot .NET 可执行文件。独立测试在临时项目中编译和挂载，玩家仍从正式场景默认入口开始。

```sh
source_dir=$(pwd -P)
qa_dir=$(mktemp -d)
mkdir -p "$qa_dir/project/VerificationHarness"
cp BorrowingSun2D.csproj project.godot "$qa_dir/project/"
cp -R Scenes Scripts "$qa_dir/project/"
cp "$source_dir/Docs/Evidence/round-03/round03-cargo-probe.cs.txt" \
  "$qa_dir/project/VerificationHarness/Round03CargoProbe.cs"
cp "$source_dir/Docs/Evidence/round-03/round03-cargo-launcher.gd.txt" "$qa_dir/round03-cargo-launcher.gd"
(
  cd -P "$qa_dir/project"
  dotnet build
  "$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD" --fixed-fps 60 \
    --script "$qa_dir/round03-cargo-launcher.gd" --quit-after 14000 \
    --log-file "$qa_dir/round03-cargo.log"
)
```

以 `ROUND_03_CARGO_QA PASSED` 为完整路线的通过标记。窗口取证用 `--disable-vsync` 替换 `--headless`，将上限改为 `--quit-after 4500`，并在命令末尾加 `-- "$qa_dir/travelling.png" "$qa_dir/delivered.png"`。该模式完成一趟正常交付，结束标记为 `ROUND_03_CARGO_CAPTURE PASSED`。 [三岸回归](ROUND_01.md#复跑) · [光路回归](ROUND_02.md#复跑方式)
