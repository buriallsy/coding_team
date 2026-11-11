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

## Flower Court market whitebox · 花庭经营白盒

The Flower Court market starts after the cargo handover. Gather ingredients, choose whether to explore after dusk, and cook orders on two burners. Close the shop before the timer expires or let it close automatically, then review the evening summary and carry your cash, ingredients, and kitchen upgrade into the next day.

1. 已有仓库用 `git switch unity-skywell` 切分支，再用 `git pull --ff-only` 拉取。首次下载可用 `git clone --branch unity-skywell https://github.com/buriallsy/coding_team.git`。用 Unity Hub 和 **6000.5.4f1** 打开 `unity_prototype/BorrowingSun`。
2. 打开 `Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity`，按 **Play**。走完下方的 L5 路线并签收，在花庭西北侧小铺按 `E` 开始第 1 天。靠近菌菇或果树短按 `E` 采集。 [场景文件](unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity)
3. 白天到时，在花庭地面站稳就会弹出黄昏选择。想立即试玩分支，可提前回柜台按 `E` 结束白天采集。
4. **直接营业**按 `1`。从庭院其他位置选择后，走回柜台按 `E`。直接回店默认可营业 **120 秒**。**夜探**按 `2`。走到果树附近，按住 `Tab` 再按 `1` 选雨令，松开 `Tab` 后长按 `E` 并左键点击集雨盆。下雨后短按 `E` 采雨夜果。夜探到时或提前回来，都可在小铺按 `E` 打开柜台。这条路线默认可营业 **60 秒**。
5. 柜台按 `E`，成功接单才开始计时。按 `Q`／`Esc` 回庭院时仍可看到倒计时。窗口有订单时按对应的 `1` 或 `2` 下锅，可同时做两道菜。完成后再按同一个键交餐收款。准备至少 3 份菌菇和 1 份果实，就能同时做菌菇便当和果香菌菇盒。
6. 接单前可花 **36 元**按 `U` 升级厨房，两口灶一起提速，之后每天都有效。到点自动收摊，也可提前按 `N`。收摊释放未下锅订单的预留，并退回未交付订单用掉的食材。
7. 日结显示本晚营业额、钱箱、累计营业额、退料和厨房状态。再按 `N` 或点击按钮进入次日，结转钱箱、库存和升级。按 `R` 从原入口重开。

**调数值**。退出 Play Mode，在花庭副本的 Hierarchy 搜索 `Flower_market_prototype`，在 Inspector 打开 **Skywell Flower Market**。**Day Gather Seconds**／**Night Gather Seconds** 默认 75／45 秒。**Trading** 下的 **Direct Trading Seconds**／**Night Trading Seconds** 分别调整直接回店和夜探后的营业时间，默认 120／60 秒。**Kitchen** 下的 **Kitchen Upgrade Cost** 默认 36 元，**Mushroom Cook Seconds**／**Fruit Cook Seconds** 默认 3／4 秒，对应两个 **Upgraded** 字段默认 2.1／2.8 秒。改完保存场景再按 **Play**。

**English quick start**. Open the Flower Market scene in Unity 6000.5.4f1, complete delivery, and tap `E` at the northwest shop to start day one. Gather with `E`. At dusk press `1` to return to the shop or `2` to explore at night. For a quick dusk test, tap `E` again at the shop. At night, hold `Tab` and press `1` to select Rain. Release `Tab`, then hold `E` and left-click the basin. Tap `E` at the orchard to gather a rain-night fruit. Return to the shop and tap `E` to open the counter. Tap `E` at the counter to accept orders and start the timer. The default is 120 seconds after a direct return and 60 after a night outing. The timer continues when you close the counter with `Q` or `Esc`. Press `1` or `2` to cook at the corresponding window. Press the same key again when ready to serve and collect payment. Press `U` before accepting orders to upgrade both burners for 36. The shop closes at zero or when you press `N`. Read the evening summary, then press `N` again to start the next day. In Edit Mode, select `Flower_market_prototype` and open **Skywell Flower Market → Trading** to tune **Direct Trading Seconds** and **Night Trading Seconds** independently.

营业限时、退料与日结的调参和实测见本轮记录。 [营业限时与日结 / Trading timer and summary](docs/04_Iterations/Skywell_Flower_Market_Settlement.md) · [双窗与快灶 / Dual burners and upgrade](docs/04_Iterations/Skywell_Flower_Market_Kitchen.md) · [上轮昼夜实测 / Day-night playtest](docs/04_Iterations/Skywell_Flower_Market_Day_Night.md)

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
