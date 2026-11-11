# Borrowing Sun · Skywell

**Unity playable whitebox · 借晴 / 天井跨谷**

Borrowing Sun is a 3D puzzle about operating public weather machinery in a mountain settlement. In Skywell, the player retrieves an oar and a Sunny token, climbs the central tower, and uses rain and wind to reach the collector. Repairing and aligning the mirrors brings sunlight to a shaded village. A cargo car then carries the player and a fruit crate across the valley to the delivery point. The deer and four cloud platforms form an optional route.

The Unity prototype supplied the level and mechanic concepts for the separately developed AP6412 Godot port. [Godot course project and report](https://github.com/buriallsy/coding_team/tree/buriallsy/3d-graybox)

![Skywell in Unity Play Mode. The repaired collector and the central relay mirror carry sunlight toward the village.](docs/evidence/skywell-sun.png)

## Architecture · 工程结构

The architecture snapshot includes 29 editable Mermaid class and flow diagrams, a source index for 112 C# files, and a proposal for a small Flower Court shop after the existing cargo delivery. The proposal documents how gameplay, terrain, input, weather, and the one-way cargo route constrain the experiment.

本次整理包含 29 张类图与玩法流程图、112 个 C# 文件的索引，以及花庭签收后接入经营的方案。经营文档列出了现有天气、地形、交互与单向运输对原型的限制。

[Architecture index / 架构目录](docs/05_Architecture/README.md) · [Offline diagram atlas / 离线图册](docs/05_Architecture/DIAGRAMS.html) · [Market proposal / 经营接入](docs/05_Architecture/05_MARKET_INTEGRATION.md) · [Integration conflicts / 冲突清单](docs/05_Architecture/06_MARKET_CONFLICTS.md)

Download the atlas HTML and open it in a browser to view or export individual SVGs. 下载图册 HTML 后用浏览器打开，可逐张导出 SVG。

## Tower market whitebox · 中央塔经营白盒

The shop rides on the central tower. Use rain to raise the tower and wind to dock at each mountain, gather ingredients, then return to the tower to cook and settle the evening.

1. 切到 `unity-skywell` 后拉取。首次下载可用 `git clone --branch unity-skywell https://github.com/buriallsy/coding_team.git`。用 Unity **6000.5.4f1** 打开 `unity_prototype/BorrowingSun`，在花庭副本按 **Play**。 [经营场景 05_Skywell_FlowerMarket.unity](unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity)
2. 沿原入口取桨、取船上的晴令并爬梯回塔。塔上小铺按 `E` 开始第 1 天。借雨升到高位，再用风令接北桥到采光庭采菌菇。回塔转东，过东桥到山阴村采果，最后走回中央塔营业。
3. 按住 `Tab` 选令，长按 `E` 瞄准。塔上集雨装置用雨令蓄水上升，用风令左键顺转或右键逆转，等桥接平再走。回到西向可按 `E` 开放水阀降回低位。北庭和东岸原有绞盘可召回已到访的同层接桥。采集用短按 `E`。东岸果树位于山阴，对当地天气装置施雨后再采。
4. 黄昏按 `1` 直接回塔或按 `2` 夜探。夜里对东岸天气装置施雨可采雨夜果。回塔按 `E` 打开柜台，再按 `E` 接单计时。直接回店默认 **120 秒**，夜探后 **60 秒**。按 `1`／`2` 各自下锅，完成后再按同一键交餐。接单前可按 `U` 升级双灶。收起柜台也继续计时。
5. 时间到或按 `N` 收摊，未交餐的实扣食材退回。日结显示当晚和累计营业额、钱箱、退料及升级。再按 `N` 或点击开始次日，保留库存、收入和厨房升级。按 `R` 回入口。

在编辑模式选中 `Flower_market_prototype`，于 **Skywell Flower Market → Trading** 调整 **Direct Trading Seconds** 和 **Night Trading Seconds**。同一组件可调白天／夜探采集时间（105／45 秒）以及 **Kitchen** 的升级价格和烹饪时长。

**English quick start**. Open the market scene in Unity 6000.5.4f1. Retrieve the oar and the Sunny token, then climb back to the tower and tap `E` at its shop. Fill the lift tanks with Rain and turn the tower with Wind. Pick mushrooms on the north shore and fruit on the east shore, then return to the tower. At dusk choose a direct return (`1`) or a night outing (`2`). Press `E` at the counter to accept orders and start the 120- or 60-second timer. Press `1` or `2` at the corresponding burner to cook or serve. The clock runs with the counter closed. When time expires or you press `N`, review the evening summary, then press `N` to start the next day. Tune both trading times under **Skywell Flower Market → Trading** on `Flower_market_prototype`.

[中央塔经营实测 / Tower market playtest](docs/04_Iterations/Skywell_Tower_Market.md) · [营业限时与日结的改线前实测 / Trading timer and summary](docs/04_Iterations/Skywell_Flower_Market_Settlement.md)

## Play on macOS

[Download the Skywell 0.1.0 playtest ZIP](https://github.com/buriallsy/coding_team/releases/download/unity-skywell-macos-v0.1.0/Borrowing_Sun_Skywell_macOS_0.1.0.zip) (123 MiB) · [Release notes](https://github.com/buriallsy/coding_team/releases/tag/unity-skywell-macos-v0.1.0)

1. Download the playtest ZIP and extract it in Finder.
2. Double-click `Skywell_Playtest.app`. The bundle includes Apple silicon and Intel binaries and declares macOS 12 or later. Unity Editor installation is unnecessary for the downloaded app.
3. This development build uses an ad-hoc signature and has no Apple notarization. If macOS blocks it because it cannot verify the developer, confirm the ZIP came from the release above. After trying to open the app, use **System Settings → Privacy & Security → Open Anyway** and confirm. [Apple’s first-launch instructions](https://support.apple.com/en-us/102445)

The extracted app passed strict signature verification and 48 headless startup checks on Apple silicon. Intel startup and a full standalone GUI playthrough remain unverified.

## Open the Unity source project

1. Clone the Unity branch. In Unity Hub, add `unity_prototype/BorrowingSun` and open it with **Unity 6000.5.4f1**. Allow Unity to import the assets and resolve the locked packages.
2. In the Project window, open [05_Skywell.unity](unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity), then press **Play**.
3. To build Skywell on macOS, install the macOS build module and choose **Borrowing Sun > Skywell > Portfolio > Build macOS playable**. The regular Build Settings currently start with Water Court (L1). The dedicated Skywell command explicitly builds L5 and writes the development app to `unity_prototype/portfolio/L5_Skywell/08_Playable/`.

The project uses URP 17.5.0, Input System 1.19.0, and Unity Test Framework 1.7.0. Package versions are pinned in [Packages](unity_prototype/BorrowingSun/Packages).

| Input | Action in Skywell |
| --- | --- |
| `WASD` | Walk or swim relative to the camera. |
| Tap `E` | Interact, board, pick up an item, paddle, or advance a conversation. Use `E` at the cargo car and at the delivery marker. |
| Hold `Tab` | Choose a weather token. |
| Hold `E` and click | Aim at a visible device. Left click casts rain or sun. For wind, left click turns clockwise and right click turns counterclockwise. The dock wind chime uses left for east and right for west. |
| `W/S` on the ladder | Climb or descend. Release to stop. |
| `Z` | Switch between fixed and over-the-shoulder cameras. |
| `Space` | Jump on the optional cloud route when available. |
| `R` | Reset the level. |

The main route goes from the dock and boat to the tower ladder, then to the north collector and the east village. Inspect the collector, observe the shaded village, and speak with the residents to obtain the lightstone. Repair the collector, use wind to align it, cast sun, and turn the tower east to complete the reflected path. Board the cargo car and confirm delivery after getting off at the destination.

## Project layout

| Location | Contents |
| --- | --- |
| [Assets/BorrowingSun/Scenes](unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes) | Water Court (L1), Rain Cargo Street (L3), Skywell (L5), and the earlier Borrowed Rain scene. |
| [Runtime](unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime) | Shared player, camera, and weather code alongside the level systems. |
| [Editor](unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor) and [Tests](unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests) | Editor tools, scene and prefab reference checks, and EditMode tests. |
| [Validation](docs/VALIDATION.md) | Import and test results for this source snapshot, plus dated L5 play and build evidence. |

This branch contains the Unity source project, its serialized assets and `.meta` files, the locked packages and project settings, and selected verification material. Unity recreates its local cache on first import.

## Credits

The project author led world design, level layout, mechanic design, and playtest decisions. Codex assisted with prototype implementation, scene assembly, and verification. The Unity project includes [Kenney character assets](unity_prototype/BorrowingSun/Assets/BorrowingSun/Art/Characters/KenneyProtagonist/License.txt) and [villager assets](unity_prototype/BorrowingSun/Assets/BorrowingSun/Art/Characters/SkywellVillagers/LICENSE-Kenney.txt) under CC0, the [Mesh2Motion ladder animation](unity_prototype/BorrowingSun/Assets/BorrowingSun/Art/Characters/Mesh2MotionLadder/Source.txt) by Scott Petrovic under CC0, and [Noto Sans CJK SC](unity_prototype/BorrowingSun/Assets/BorrowingSun/Resources/Fonts/LICENSE.txt) under the SIL Open Font License 1.1.
