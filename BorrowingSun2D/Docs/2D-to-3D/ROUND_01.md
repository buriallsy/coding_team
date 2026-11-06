# 第 01 轮 三岸接驳

玩家借雨上升后，先接北桥走到采光庭，再从北庭步行回到同一座塔，换接东岸。北庭旗标记录第一次抵达，东岸旗标记录本轮完成。这个次序为下一轮「去北庭调采日镜，再转向东岸送光」建立了实际可走的路线。 [上轮 MVP](https://github.com/buriallsy/coding_team/commit/f6900c483680de7f1d21a4768720753b6f1512d7) · [制作计划](PRODUCTION_PLAN.md)

## 设计取舍

- **东岸与北庭共用高位。** 东岸放在剖面右侧，左右间隔与桥舌长度对称。保留同一个水平塔面，转台改变哪侧桥舌伸出，让玩家仍可在同一画面内比较桥头与台面。
- **风令每次转一档。** `E` 按西向、北庭、东岸的顺序转，`Shift+E` 反向。到边界后要反向施令，回到西向可以用 `F` 放水。`Q` 收令时保留当下朝向，停在两档之间时保留真实缺口。
- **桥的承托跟停靠状态走。** 高度和朝向匹配并停稳后，才启用对应桥的碰撞。左右门槛都阻止转向。北庭到达后提示返回塔台，先回到塔内再换接东岸。
- **复位仍用正常的物理同步。** 按 `R` 后，西岸低桥恢复，东桥与两岸抵达进度一并清除。复测以角色重新走过桥为准，同时检查两侧桥的碰撞状态。

| 位置与朝向 | 实际接通 |
| --- | --- |
| 低位 · 0° 西向 | 左桥接西岸 |
| 高位锁定 · 90° 北向 | 左桥接北庭 |
| 高位锁定 · 180° 东向 | 右桥接东岸 |
| 升降、转动、高位西向或未对齐 | 两侧桥舌缩回 |

## 代码与场景

`TowerMechanism` 新增东向停点与方向判定，继续持有水量、机械锁、角度与停靠。`TowerSlice` 读取停靠结果，驱动两侧桥舌的真实碰撞并记下旗标进度。`TowerView` 从同一份状态绘制塔面、桥头与提示。`SlicePlayer` 沿用原有走路与随台移动。 [机关规则](../../Scripts/Core/TowerMechanism.cs) · [输入与碰撞](../../Scripts/TowerSlice.cs) · [东岸场景节点](../../Scenes/TowerSlice.tscn)

## 预定验收

1. 从默认入口实走西岸、塔台、北庭、塔台、东岸。分别核对高位锁、北庭里程碑和东岸完成。
2. 在高位未接或转动途中检查实体缺口。站在左右门槛尝试换向，再退回塔内操作。验证左右桥只在各自对应停点承托。
3. 从东岸沿桥返回塔台并反向接回北庭。到达后和运动中按 `R`，再次从西岸步行跨过低桥。

## 实际测试 · 2026-11-06

在 macOS arm64、Godot 4.7.1 .NET、.NET SDK 8.0.424 的独立源码副本测试。两条自动路线都从默认入口启动，由角色和平台的正常物理逻辑处理操作。测试脚本读取落脚状态、机关状态与射线结果。

| 运行 | 实际结果 |
| --- | --- |
| 干净副本构建 | 0 警告、0 错误。[构建日志](../Evidence/round-01/clean-build.log) |
| 内置路线 | 107 条通过，退出 0。走通西岸、北庭、回塔、东岸。额外先接东岸走到旗标，确认缺少北庭到访时完成条件不会触发。[内置日志](../Evidence/round-01/built-in-route.log) |
| 独立按键 | 71 条通过，退出 0。用实际键码连续跑了两遍正向路线，测了远距离施令、左右门槛、东向限位、逆转恢复北桥和两侧缺口落下回台。[独立日志](../Evidence/round-01/independent-route.log) · [键码脚本](../Evidence/round-01/independent-route.gd.txt) |
| 复位回归 | 在东岸旗标、高位停靠、缺口失足后和升降途中分别按 `R`。活动台节点与物理刚体都回到 `(660,590)`。射线命中低桥，两侧高桥均无残留碰撞。每次都再次步行上塔，其中一趟从复位后走完三岸路线。[独立日志](../Evidence/round-01/independent-route.log) |
| 图形复测 | 73 条通过，退出 0。其中两条检查北庭和东岸原帧的保存。北庭时左桥接通，回塔转东后右桥接通，两个画面用同一剖面机位。[窗口运行日志](../Evidence/round-01/rendered-route.log) · [北庭原帧](../Evidence/round-01/north-court.png) · [东岸原帧](../Evidence/round-01/east-shore.png) |

图形首跑走完了第一趟，第二趟在从北庭回塔时持续移动中断，后续四条断言失败。首跑未记录焦点，原因未确认。最终轮沿用项目默认垂直同步、补记窗口焦点和输入超时，复跑同一路线通过。目检首跑画面时发现东岸旗标文字压住角色边缘，最终轮把文字右移 15 px，并核对两张原帧。 [窗口首跑日志](../Evidence/round-01/rendered-first-attempt.log)

无头日志包含 macOS 沙箱的系统 CA 读取错误，两条路线的进程均正常退出。证据范围为本轮本机自动走位与物理规则，首次玩家理解及其他平台待实测。

## 复跑

在 `BorrowingSun2D` 项目目录运行。将 `BORROWINGSUN_GODOT_BIN` 指向 Godot .NET 的可执行文件。

```sh
cd -P .
dotnet build
qa_dir=$(mktemp -d)
"$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD" --fixed-fps 60 \
  --log-file "$qa_dir/built-in-route.log" --quit-after 5500 -- --verify
cp Docs/Evidence/round-01/independent-route.gd.txt "$qa_dir/independent-route.gd"
"$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD" --fixed-fps 60 \
  --log-file "$qa_dir/independent-route.log" --script "$qa_dir/independent-route.gd" --quit-after 6500
```

以日志末尾的 `TOWER_ROUTE PASSED` 与 `ROUND_01_INDEPENDENT_QA PASSED` 为通过标记。

在有图形桌面的环境可运行同一个独立脚本并保存两张原帧。

```sh
"$BORROWINGSUN_GODOT_BIN" --path "$PWD" --fixed-fps 60 \
  --log-file "$qa_dir/rendered-route.log" --script "$qa_dir/independent-route.gd" \
  --quit-after 6500 -- "$qa_dir/north-court.png" "$qa_dir/east-shore.png"
```
