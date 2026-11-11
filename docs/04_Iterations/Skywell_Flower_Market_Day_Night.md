# 花庭经营 · 昼夜回路

2026-11-11 · Unity 6000.5.4f1。打开 `05_Skywell_FlowerMarket.unity`，从原入口走到花庭签收，再到西北侧小铺短按 `E` 开始第一天。 [花庭副本](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell_FlowerMarket.unity) · [上一轮白盒](Skywell_Flower_Market_Whitebox.md)

## 一天怎么过

| 时段 | 玩法 |
| --- | --- |
| 白天 | 75 秒内在庭院采集。也可提前回小铺按 `E` 进入黄昏 |
| 黄昏 | 白天到时，在庭院地面站稳后弹出选择。按 `1` 直接回店营业，按 `2` 去夜探。选择夜探后才开始夜探计时 |
| 夜探 | 45 秒内采集。时间到后仍可走回小铺，也可提前回来。到店按 `E` 营业。按住 `Tab` 再按 `1` 选雨令，松开 `Tab` 后长按 `E` 并左键点集雨盆 |
| 营业 | `1` 菌菇便当，`2` 果香菌菇盒，`E` 交餐收款。`Q` 或 `Esc` 收起面板，回柜台再按 `E` 打开 |
| 收摊 | 按 `N` 进入次日。现金和库存结转，未交付的订单按实际食材退回，天亮恢复原日光。按 `R` 回原入口并重置本局 |

果树沿用白盒里的同一株，移到 `(-23.55, 35, 0.55)`，树冠直射采样在 `(-23.55, 35.65, 0.55)`。旧点受屋顶遮挡，新点仍在原半径 3 米的花庭雨区内。Unity 保存场景检查确认树冠直射射线畅通，并以 25 cm 步距核对果树与小铺之间的落地与胶囊净空。小铺添了暖灯，夜间以环境光保留台面与雨盆的辨识度。庭院沿用已有碰撞和灰盒模型。

## 果树与数值

| 采集时段 | 树冠直射 | 局部雨 | 果树产出 |
| --- | --- | --- | --- |
| 白天 | 有 | 无 | 晴果 |
| 白天 | 任意 | 有 | 雨露果 |
| 白天 | 无 | 无 | 无果 |
| 夜探 | 无 | 无 | 无果 |
| 夜探 | 无 | 有 | 雨夜果 |

果树按当前环境换色和显示名称。已进背包的果实保持原品种。夜间关闭原太阳光照与实时反射，花庭雨令仍可作用。次日恢复太阳强度与实际光路。

| 调整项 | 本轮试值 | 位置 |
| --- | --- | --- |
| 白天与夜探 | 75 秒 / 45 秒 | `Flower_market_prototype` 上的 **Skywell Flower Market**，`Day Gather Seconds` / `Night Gather Seconds` |
| 单点采集与刷新 | 1 份 / 1 秒 | 同组件 `Regrow Seconds` |
| 局部雨的生效等待 | 1.2 秒 | 沿用 `SkywellWeatherConsole` |
| 菌菇便当 | 菌菇 ×2，3 秒，12 元 | `FlowerMarketRun.TryGetRecipe` |
| 果香菌菇盒 | 菌菇 ×1、任意果实 ×1，4 秒，22 元 | 同上。按雨夜果、雨露果、晴果顺序取库存 |

当前三种果实共用同一道菜价，夜探增加可补货的时段和雨夜果来源。可用本轮步行与施雨分段做盈亏对照，再定夜探奖励和时长。 [时段与菜谱规则](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/FlowerMarketRun.cs) · [场景与天气接线](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/Market/SkywellFlowerMarket.cs)

## 实测

macOS 前台 Unity Game View 从原入口连续实走到花庭，再用实际键盘和指针完成两条黄昏分支。**346 项玩法检查**通过。两个采集时段均在行走中推进，回放用柜台提前结束，计时到期的状态转换由规则测试覆盖。EditMode **128 项**通过，其中 11 项为花庭规则。五个场景和一个预制体的保存引用检查通过。

| 实测时刻 | 树冠直射 | 花庭局部雨 | 果树与库存 |
| --- | --- | --- | --- |
| 第 1 天白天干燥 | 有 | 无 | 采到 1 份晴果 |
| 第 1 天白天施雨 | 有 | 有 | 采到 1 份雨露果，原晴果仍在背包 |
| 第 2 天干燥夜探 | 无 | 无 | 短按 `E` 拒绝采果，原晴果仍在背包 |
| 第 2 天夜探施雨 | 无 | 有 | 采到 1 份雨夜果 |
| 第 3 天黎明 | 恢复 | 无 | 果树恢复供应晴果，背包保留 1 份晴果和 1 份雨夜果 |

第 1 天选直接营业，完成两道菜共收 **34 元**。第 2 天夜探回店后，用雨夜果开始做菜，随后收摊，退回 1 份菌菇和 1 份雨夜果。第 3 天钱箱仍为 **34 元**。夜间原反射光束熄灭，货物、签收、塔位、水量和配重保持。黎明恢复实际光路时会触发原光路反馈镜头约 3 秒。夜里按 `R` 恢复原入口和日光。

| 路线 | 出门 | 采集及操作 | 回店 | 合计 |
| --- | ---: | ---: | ---: | ---: |
| 白天采 4 份菌菇 | 1.32 s | 5.58 s | 1.25 s | 8.15 s |
| 白天采晴果，再施雨采雨露果 | 2.28 s | 4.64 s | 2.23 s | 9.15 s |

上表为本次固定视角回放的游戏时钟读数。夜探采到第一份雨夜果的原帧显示还剩 38 秒，包含走到果树、干夜拒采、选令、指针施雨和实际采集。调时长时可把这张原帧用作首份产出的余量，回店和黎明镜头另计。

复跑时打开花庭副本，进入 Play Mode 后选 **Borrowing Sun → Skywell → Check complete Flower Market route**，保持 Unity 窗口前台。原 L5 场景、摆渡脚本和 Build Settings 与 `5b83325` 逐字节一致。

[完整实走日志](../evidence/flower-market-day-night/playmode.txt) · [日照与净空检查](../evidence/flower-market-day-night/authoring.txt) · [EditMode XML](../evidence/flower-market-day-night/editmode.xml) · [保存引用检查](../evidence/flower-market-day-night/references.txt)

[白天晴果](../evidence/flower-market-day-night/flower-market-sunny-day.png) · [白天雨露果](../evidence/flower-market-day-night/flower-market-local-rain.png) · [黄昏选择](../evidence/flower-market-day-night/flower-market-dusk-choice.png) · [干燥夜晚](../evidence/flower-market-day-night/flower-market-night-dry.png) · [雨夜果与店灯](../evidence/flower-market-day-night/flower-market-night-rain.png) · [夜探后营业](../evidence/flower-market-day-night/flower-market-night-trading.png) · [黎明恢复光路](../evidence/flower-market-day-night/flower-market-dawn-after-night.png)
