# 第 05B 轮 · 东岸与双反射

**分支** `buriallsy/3d-graybox` · **起点** 05A `c1209a8` · **2D 基线** `2d-baseline`（`6175db9`）

从西岸上塔，借雨升到高位，借风接北桥。到北庭用风校准采日镜、用晴开放入射光。沿原桥回塔，再用风接东桥，走到东岸接光盘。到达东岸与接光是两个独立结果。货运接续留到下一轮。 [当前操作](../README.md) · [当前关系图](RELATIONSHIP_MAP.md)

## 设计与移植

| 2D 基线 | 05B 落地 |
| --- | --- |
| `TowerMechanism` 的蓄雨、机械锁与三个停点 | 共用同一份源码。0°、90°、180° 对应西、北、东，塔台在 Y 轴升降并绕 Y 轴转动。高位朝西时仍无接桥。 |
| `RouteProgress` 的北庭后到东岸顺序 | 共用同一份源码。检测角色真实落脚及岸上标记，再交给 `Visit`。到达东岸后继续读取实时受光。 |
| 2D 剖面中的两组投影桥 | 3D 用同一实体桥板连三岸。东岸桥口从塔轴外 4.20 m 开始，桥板在三处有效停靠时承托角色。 |
| `SunCourtOptics2D` 中的北庭局部天气 | `SunCourtOptics3D` 管风校准、晴开光、收令和复位。离开北庭后本地晴态保留，塔台施风只改塔机。 |
| `Optics2D` 的有限线段、正面和遮挡 | `Optics3D` 计算有限圆面与射线的最近交点、正面和真实反射，再向 Godot 查询光段上的实体。可见光束与接光反馈来自同一个 `LightPath3D`。 |

固定的太阳方向与镜面姿态由场景定义。北庭校准到预设姿态后，第一段反射打向高位塔镜。塔镜法线随升降台真实旋转：朝北时，第二段反射向西；朝东时，第二段在无遮挡时才打进接光盘的有限孔径。接桥是停靠规则，接光是几何结果。场景中的岸面、塔台与专用遮挡层参与射线查询，角色层不参与。 [物理射线参数](https://docs.godotengine.org/en/stable/classes/class_physicsrayqueryparameters3d.html)

## 复跑

用 Godot 4.7.1 .NET 和 .NET 8 SDK。`BORROWINGSUN_GODOT_BIN` 指向 Godot 可执行文件。下列命令从仓库根开始。

```sh
cd -P BorrowingSun3D
log_dir=$(mktemp -d)
dotnet build BorrowingSun3D.csproj
"$BORROWINGSUN_GODOT_BIN" --headless --path "$(pwd -P)" --fixed-fps 60 \
  --quit-after 5000 --log-file "$log_dir/route.log" -- --verify
grep '^GRAYBOX_ROUTE PASSED' "$log_dir/route.log"
```

窗口验证可去掉 `--headless`，添加 `--resolution 1280x720 --audio-driver Dummy`。在 `--verify` 后追加 `--capture-dir="$log_dir"` 可保存窗口原帧。

## 实测记录 · 2026-11-08

从无 Godot 缓存的源码副本构建和导入。macOS 26.5.2 arm64，Godot 4.7.1 .NET，.NET SDK 8.0.424。3D 构建 0 警告、0 错误，原有领域夹具通过 80 项。默认场景用实际按键事件跑无头和窗口测试，各通过 84 项。 [构建](Evidence/round-05b/graybox-build.log) · [导入](Evidence/round-05b/graybox-import.log) · [领域夹具](Evidence/round-05b/domain-checks.log) · [无头](Evidence/round-05b/graybox-headless.log) · [窗口](Evidence/round-05b/graybox-window.log)

| 用例 | 实测结果 |
| --- | --- |
| 西岸过低桥上塔 | 玩家落脚于真实桥面，门槛拒绝施令，塔在低位拒绝转向。施雨后玩家随台升高 6 m。 |
| 北向的第一段光 | 采日镜在原位时直接施晴，光到不了塔镜。用风校准后施晴，采日镜反射命中塔镜，塔镜反射向西，东岸盘保持暗。按 `Q` 收晴，光熄灭，校准保留。 [北向原帧](Evidence/round-05b/north-marker.png) |
| 回塔再转东 | 从北庭按 `S` 走回塔，北庭晴态保留。转到 135° 时仍未命中接光盘，桥板也未接通。180° 停稳后同一块桥板接东岸，两段反射依次命中塔镜和接光盘。按 `D` 走过实体桥，才记东岸到访。 [东向原帧](Evidence/round-05b/east-receiver.png) |
| 东向遮挡反例 | 在第二段光路中插入边长 0.40 m 的测试用 `StaticBody3D`。塔镜继续受光，第二段停在与物理射线一致的撞点（约 `4.10, 7.46, 0.00`），接光盘变暗。移走挡块，保持相同朝向即可恢复。 |
| `R` 后重走 | 在东岸重开，清空光路、镜面校准、天气和到访状态。实体低桥恢复后从西岸再走全程，第二次到东岸并接光。随后从东岸失足，回到当前高位塔台，到访保留。 [西岸原帧](Evidence/round-05b/west-entrance.png) |

正常路线与重走均由键盘事件完成。遮挡反例的挡块仅由验证脚本插入和移除。窗口原帧核对了北向光线向西、东向光线抵达接光盘和对应 HUD。当前实测平台为 macOS。
