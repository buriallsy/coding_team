# 第 02 轮 · 借光

本轮把北庭、塔台和东岸接成一条实际光路。玩家先在北庭用风调准采日镜，再施晴打开当地日照，步行回塔并转向东岸。两段反射都由有限镜面、实际法线与最近的遮挡决定。 [第 01 轮代码基线](https://github.com/buriallsy/coding_team/commit/5248bfd08085ec5a18285dad3c779aecbb804aa4) · [制作计划](PRODUCTION_PLAN.md)

## 玩一遍

1. 从西岸向右过低桥。塔上按 `1`、`E` 施雨，等机械锁扣合，再按 `2`、`E` 接北桥。
2. 向左走到北庭采日镜旁。选风按 `E` 把镜面调到接力位，按 `3`、`E` 施晴。此时能看到光打到塔镜，东岸盘仍暗。
3. 向右走回塔中央。按 `2`、`E` 转东，观察反射光能否打到东岸接光盘。桥和旗标仍可用步行验证。
4. 北庭选风后可用 `Shift+E` 将采日镜拨回初始方向，观察未对准时的光路。站在北庭设备旁按 `Q` 可收回当地晴令。`R` 重开后可从西岸重新走上塔。

## 设计取舍

- **晴令留在北庭。** 镜面朝向与当地天气分别记录。北庭只有一个当地天气槽，用风调镜后再用晴开光，可以对比初始朝向和接力位。走回塔台后，塔上的风令与北庭晴令可以同时生效。
- **镜面跟实际角度走。** 采日镜和塔镜各有可见的有限镜面。塔镜随转台朝向变化，北向把光反射到场景里的挡光板，东向把光送向接光盘。两处结果都来自光线求交。遮挡早于接盘时，光束停在实体上。
- **把到访和受光分别显示。** 北庭及东岸旗标继续记录步行进度。接光盘显示当前这一帧是否收到经过两面镜子的光，下一轮交付规则可以读取这个结果。

## 代码与场景

`TowerSlice` 按玩家所处设备分发操作。`SunCourtOptics2D` 持有北庭的调镜、日照与最新光路，读取场景锚点、台高和朝向，把近处实体遮挡作为物理查询提供给纯 C# `Optics2D.Trace()`。求解器计算有限线段交点和两次镜面反射，返回光段与命中。`TowerView` 使用同一份 `LightPath2D` 画光束、塔镜命中和东盘接光。 [对象关系图](RELATIONSHIP_MAP.svg) · [求解器](../../Scripts/Core/Optics2D.cs) · [作者场景](../../Scenes/TowerSlice.tscn)

剖面中的太阳入口、采日镜、塔镜和东盘由场景标记定位。光学查询包含地形、活动塔和挡光层。北向挡光板位于塔下方的光路上，只参与光学遮挡；塔镜的朝向在北、东两套作者姿态之间连续变化。

## 几何专项

纯 C# 数值样例通过 8 项，覆盖两次反射的实际交点、镜面错向、有限接收范围、盘面背面和两种遮挡位置。数值样例中的遮挡由回调给出，场景专项另用 Godot 的碰撞体和物理射线验证。 [数值脚本](../Evidence/round-02/optics-math.cs.txt) · [数值结果](../Evidence/round-02/optics-math.log)

## 场景测试 · 2026-11-06

在 macOS arm64 上使用 Godot 4.7.1 .NET 和 .NET SDK 8.0.424。生产工程干净构建 0 警告、0 错误。独立测试用 `InputEventKey` 经过项目的正式输入映射，从西岸开始走，玩家移动与桥承托使用正式场景的物理。 [构建日志](../Evidence/round-02/clean-build.log) · [光路专项日志](../Evidence/round-02/round02-optics.log)

| 用例 | 实测结果 |
| --- | --- |
| 正常按键到北庭调镜、施晴 | 入射从 `(312,142)` 命中采日镜 `(360,238)`，第一段反射命中塔镜约 `(660,176)`。北向第二段停在作者场景的挡光板 `(855,347.42)`，东盘仍暗。 |
| 正常按键回塔转东 | 北庭晴令持续，第一段反射仍到塔镜。第二段命中东盘正面约 `(1044,232)`，东盘亮起。继续沿实体东桥步行到旗标。 |
| 东向的实体遮挡夹具 | 测试临时加入一块光学遮挡层的 `StaticBody2D`，保留东向和晴令。第二段停在碰撞点约 `(840,202.25)`，塔镜仍有光，东盘熄灭。移除挡块后同一光路恢复。 |
| 用 `Shift+E` 把北镜拨偏 | 晴令仍能让入射光命中北镜，反射打偏，塔镜和东盘都暗。重新调镜并施晴后，塔镜恢复命中。北庭按 `Q` 清当地日照，保留镜面朝向。 |
| 从已接光的东岸按 `R` | 清日照、镜面调整、光路和进度。升降节点与物理体都回到 `(660,590)`，实走低桥再次抵达塔上设备。 |

无头光路专项 57 项、图形专项 59 项通过，图形额外验证两张原帧。旧三岸路线的 112 项内置检查和 71 项独立按键检查也通过。 [光路无头](../Evidence/round-02/round02-optics.log) · [光路图形](../Evidence/round-02/round02-graphics.log) · [内置回归](../Evidence/round-02/round01-builtin.log) · [按键回归](../Evidence/round-02/round01-independent.log)

另跑的旧窗口脚本在失焦后未完成第二张截图，旧路线本轮以两套无头回归为准。 [中断日志](../Evidence/round-02/round01-graphics-interrupted.log)

同机位运行原帧可直接比较北向时的挡光终点与东向时的接盘亮起。 [北向原帧](../Evidence/round-02/north-relay.png) · [东向原帧](../Evidence/round-02/east-receiver.png)

## 复跑方式

在 `BorrowingSun2D` 项目目录运行。将 `BORROWINGSUN_GODOT_BIN` 指向 Godot .NET 可执行文件。独立测试使用干净的项目副本，从默认入口发出按键；复制的验证脚本仅在临时工程中编译。

```sh
source_dir=$(pwd -P)
qa_dir=$(mktemp -d)
mkdir -p "$qa_dir/project/VerificationHarness"
cp BorrowingSun2D.csproj project.godot "$qa_dir/project/"
cp -R Scenes Scripts "$qa_dir/project/"
cp "$source_dir/Docs/Evidence/round-02/round02-optics-probe.cs.txt" \
  "$qa_dir/project/VerificationHarness/Round02OpticsProbe.cs"
cp "$source_dir/Docs/Evidence/round-02/round02-optics-launcher.gd.txt" "$qa_dir/round02-optics-launcher.gd"
(
  cd -P "$qa_dir/project"
  dotnet build
  "$BORROWINGSUN_GODOT_BIN" --headless --path "$PWD" --fixed-fps 60 \
    --script "$qa_dir/round02-optics-launcher.gd" --quit-after 8500 \
    --log-file "$qa_dir/round02-optics.log"
)
```

以日志中的 `ROUND_02_OPTICS_QA PASSED` 为通过标记。需保存两张窗口原帧时，去掉 `--headless`，并在同一命令末尾加 `-- "$qa_dir/north.png" "$qa_dir/east.png"`。三岸路线复测沿用 [第 01 轮命令](ROUND_01.md#复跑)。

单跑数值样例可将 `Scripts/Core/Optics2D.cs` 复制到一个 .NET 8 控制台项目，用 `optics-math.cs.txt` 替换 `Program.cs`，执行 `dotnet run`。
