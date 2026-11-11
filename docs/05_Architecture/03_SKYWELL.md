# L5 天井跨谷 · Unity 运行架构

L5 从水面取回晴牌开始。雨水先举升塔台，风使实体桥对接不同岸口，修好的采日镜和塔镜把太阳送进山阴村。同一批雨水随后转入运输配重，玩家登车跨谷，到花庭前站下车签收。鹿与云台是独立支线，主线交付条件不读取它的进度。本文以当前保存场景和 `BorrowingSun.Runtime` 源码为准，数值采用场景序列化值。 [保存场景][scene] · [控制器][controller]

## 从场景定位组件

保存场景只有一个顶层对象 `L5_Skywell`。根对象挂载协调、输入界面和主要过程组件。角色、镜头、机械、天气区与光学设备都在其下。`Player_Standard_Humanoid` 的 `CourtPlayer.gameplayOwner` 指向 `SkywellController`，共享角色通过 `ICourtGameplay` 回调本关。`Kenney_Original_Cyborg` 的 `StandardHumanoidVisual` 接收共享玩家移动与本关特殊姿态，`Camera_Main` 的 `CourtCamera` 管固定视角、肩视角及临时镜头。 [场景根与组件][scene-root] · [玩家绑定][scene-player] · [人形绑定][scene-humanoid] · [摄像机绑定][scene-camera] · [共享接口][gameplay] · [共享玩家][player] · [人形呈现][humanoid] · [共享镜头][camera]

| 保存场景位置 | 已保存的接线与职责 |
| --- | --- |
| `L5_Skywell` | `SkywellHUD`、`SkywellController`、`SkywellJourney`、`SkywellFish`、`SkywellVoyage`、`SkywellWindBell`、`SkywellWorldFeedback`、`SkywellStory`、`SkywellDeerGrove`、`SkywellDeerRide`、`SkywellCloudFootTravel`、`SkywellClimbAnimation`。 [根组件][scene-controller] · [支线与动画绑定][scene-grove] |
| 中央塔 | 控制器绑定 `Outer_lift_ring_NON_ROTATING`、`Rotor_PIVOT`、四处集雨屋面、两只上位机械锁、三岸绞盘、放水阀、配重桶和货车。`Fixed_lower_lift_house` 上的 `SkywellLiftSupport` 显示活塞与标尺，`Peripheral_rain_counterweights_FIXED` 上的 `SkywellRainFeedback` 显示集雨，`Automatic_water_route` 上的 `SkywellWaterRouting` 显示转水。 [中央塔字段][scene-controller] · [活塞][scene-lift] · [雨水反馈][scene-rain] · [转水显示][scene-water] |
| 水面与塔侧 | `SkywellJourney` 绑定水面网格、十个岸路点、九个可站碰撞体、船桨、船上晴牌、船、船泊位和梯脚/梯顶/出口。`Tower_side_ladder` 的 `SkywellLadderVisual` 跟随锚点。`East_sun_occluding_mountain_ridge` 的 `SkywellCaveCutaway` 处理观察镜头的剖面显示。 [水面绑定][scene-journey] · [梯子显示][scene-ladder] · [剖面绑定][scene-cutaway] |
| 采日镜、塔镜与山阴村 | `Sun` 上的 `SkywellSceneLighting` 配置方向光；`Terrain_sunlight_well` 的 `SkywellSunCollector` 驱动 `Fixed_reflector`；`M2_Reflection_rail_and_ballast` 的 `SkywellOptics` 绑定随塔旋转的 `Rotating_mirror` 与 `Settlement_sun_receiver`；接光盘连接 `Village_borrowed_light_network` 和货车上的水果。 [方向光][scene-lighting] · [采日镜][scene-collector] · [光路][scene-optics] · [接光盘][scene-receiver] · [灯网][scene-village] |
| 叙事和跨谷 | `Village_observation` 的 `SkywellObservation` 保存观察状态；根上的 `SkywellStory` 连到观察点、北庭维修点、守镜人与守灯人；`SkywellVoyage` 连到 `Freight_carrier`、`Arrival_release` 和七个世界坐标轨迹点。`backGate` 在当前保存场景为空。 [观察点][scene-observation] · [叙事字段][scene-story] · [运输绑定][scene-voyage] |
| 果树与云路 | `SkywellDeerGrove` 绑定果树、两类苹果、实际落果地面和云台组；`SkywellDeerRide` 绑定鹿、鞍、骑乘点和有序的六处支撑面。顺序是果树岛、四块云台、外山庭。`SkywellCloudFootTravel` 复用骑乘提供的支撑查询。 [果树与骑乘绑定][scene-grove] |

控制器的天气站索引同时用于控制台与天气目标数组。指针点击设备提交意图。天气区以有限高度的圆柱范围或实际体积碰撞器判定设备是否受作用。 [场景索引][scene-controller] · [天气区 Contains][zone] · [天气目标][target]

| 索引 | 天气目标 | 场景内的对应天气区 |
| --- | --- | --- |
| 0 | `M1_Rotation_and_lift` | 中央塔，半径 12 m。 [目标][scene-target-tower] · [区域][scene-zone-tower] |
| 1 | `Fixed_reflector` | 向阳山采日镜，半径 5 m。 [目标][scene-target-collector] · [区域][scene-zone-collector] |
| 2 | `Fixed_refuge_FOUR_WALLS` | 水边避难所，半径 12 m，并绑定盒形体积。 [目标][scene-target-refuge] · [区域][scene-zone-refuge] |
| 3 | `Settlement_sun_receiver` | 山阴村，半径 8 m。 [目标][scene-target-village] · [区域][scene-zone-village] |
| 4 | `Apple_tree_growth_root` | 果树岛，半径 8 m。 [目标][scene-target-grove] · [区域][scene-zone-grove] |

## 类关系与状态归属

图中的箭头从调用者或持有引用的一端指向被依赖的一端，关系标签标出交接的数据或控制。三个 `SkywellController` 源文件编译成同一个类型。控制器以执行顺序 `-100` 在 `Update` 中回收令牌、处理输入、推进天气过渡、求解光路，最后推进机械；运输和支线分别推进自己的过程。 [控制器与更新循环][controller] · [天气输入分部][weather-input] · [机械分部][lift]

### 玩家、天气与机械

```mermaid
classDiagram
    direction LR
    class ICourtGameplay["ICourtGameplay"]
    class CourtPlayer["CourtPlayer"]
    class CourtCamera["CourtCamera"]
    class SkywellController["SkywellController"]
    class SkywellHUD["SkywellHUD"]
    class SkywellWeatherConsole["SkywellWeatherConsole"]
    class SkywellWeatherZone["SkywellWeatherZone"]
    class SkywellWeatherTarget["SkywellWeatherTarget"]
    class SkywellRainFeedback["SkywellRainFeedback"]
    class SkywellLiftSupport["SkywellLiftSupport"]
    class SkywellWaterRouting["SkywellWaterRouting"]
    SkywellController ..|> ICourtGameplay
    CourtPlayer --> ICourtGameplay : "互动和复位"
    SkywellController --> CourtPlayer : "塔台携带"
    SkywellController --> CourtCamera : "输入模式和镜头"
    SkywellHUD --> SkywellController : "提示和天气菜单"
    SkywellController --> SkywellWeatherTarget : "目标验证"
    SkywellController --> SkywellWeatherConsole : "插牌和回收"
    SkywellWeatherConsole --> SkywellWeatherZone : "绑定天气区"
    SkywellWeatherZone --> SkywellWeatherConsole : "读取生效天气"
    SkywellController --> SkywellWeatherZone : "天气范围和风向"
    SkywellRainFeedback --> SkywellController : "集雨表现"
    SkywellLiftSupport --> SkywellController : "高度表现"
    SkywellWaterRouting --> SkywellController : "转水表现"
```

| 类型 | 自己拥有的状态与边界 |
| --- | --- |
| `SkywellController` | 由控制器本体、`SkywellWeatherInput.cs` 和 `SkywellLiftMechanism.cs` 组成。拥有 E 输入仲裁、手持天气选择、远程操作锚点、令牌唯一归属、已到访岸口、集水量、配重水量、升降、偏航、锁止、绞盘和排水。私有 `InteractionKind`、`InteractionTarget` 管短按候选；`WaterFlow` 区分待机、集雨、排水、转水。 [主体][controller] · [输入][weather-input] · [机械][lift] |
| `SkywellWeatherConsole`、`SkywellToken` | 每站有一个物理令牌槽和独立的请求天气，负责过渡时间、回收和归零。`SkywellToken` 是 `None/Rain/Wind/Sun`。 [天气控制台][console] |
| `SkywellWeatherZone` | 读取控制台的生效天气，提供范围检测、水平风向和受影响组件登记，驱动局部雨、风、云等视觉。 [天气区][zone] |
| `SkywellWeatherTarget` | 保存站点索引、实际点击表面、距离与可见性、轮廓及材质缓存；私有 `HighlightSlot` 和 `MeshSurface` 支撑高亮与表面选择。 [天气目标][target] |
| `SkywellHUD` | 从控制器、旅程、光路、叙事、运输读取提示并绘制 IMGUI，天气选择交回控制器。 [界面][hud] |
| `SkywellRainFeedback`、`SkywellLiftSupport`、`SkywellWaterRouting` | 分别投影集雨水面、入流与绳索，升降活塞和刻度，转水路径和水轮。集水、锁止和配重仍由控制器推进。 [集雨反馈][rain] · [活塞][support] · [转水显示][routing] |

### 叙事、光路和运输

```mermaid
classDiagram
    direction LR
    class SkywellController["SkywellController"]
    class SkywellStory["SkywellStory"]
    class SkywellObservation["SkywellObservation"]
    class SkywellSunCollector["SkywellSunCollector"]
    class SkywellOptics["SkywellOptics"]
    class SkywellSunReceiver["SkywellSunReceiver"]
    class SkywellVillageLightNetwork["SkywellVillageLightNetwork"]
    class SkywellWeatherZone["SkywellWeatherZone"]
    class SkywellVoyage["SkywellVoyage"]
    class SkywellSceneLighting["SkywellSceneLighting"]
    class CourtPlayer["CourtPlayer"]
    SkywellController --> SkywellStory : "对话入口"
    SkywellController --> SkywellObservation : "打开观察"
    SkywellController --> SkywellOptics : "求光路并读取实时受光"
    SkywellController --> SkywellVoyage : "短按互动和重置"
    SkywellStory --> SkywellObservation : "是否已看旧景"
    SkywellStory --> SkywellSunCollector : "安装集光石"
    SkywellSunCollector --> SkywellWeatherZone : "北庭风和晴"
    SkywellSceneLighting ..> SkywellSunCollector : "同一方向光"
    SkywellOptics --> SkywellSunCollector : "取真实反射出射线"
    SkywellOptics --> SkywellSunReceiver : "求交并传入本帧结果"
    SkywellSunReceiver --> SkywellVillageLightNetwork : "推进或重置灯网"
    SkywellObservation --> SkywellSunReceiver : "读取村庄奖励"
    SkywellVoyage --> SkywellController : "检查发车条件"
    SkywellVoyage --> CourtPlayer : "碰撞登车和载台携带"
```

| 类型 | 自己拥有的状态与边界 |
| --- | --- |
| `SkywellObservation`、`SkywellStory` | 观察组件保存开关与 `HasViewed`；叙事组件保存对话页、检查镜心、集光石获得和消耗，私有 `Completion` 区分赠石与维修。确认最后一页时才提交赠石或安装。 [观察][observation] · [叙事][story] |
| `SkywellSceneLighting`、`SkywellSunCollector` | 前者设定作者方向光、环境光和背景；后者读取同一个太阳与本地天气，拥有维修、焦点索引、实际镜姿、锁定及太阳遮挡状态，并给出实算反射方向。 [场景照明][lighting] · [采日镜][collector] |
| `SkywellOptics` | 拥有本帧射线、最近遮挡、实际塔镜命中、反射计数和接光结果。线段显示与真实求交共用结果。 [光学求解][optics] |
| `SkywellSunReceiver`、`SettlementSunState`、`SkywellVillageLightNetwork` | 接光盘拥有有限口径、本帧是否受光、持续接光进度和货物奖励，状态为 `Unlit/Receiving/Spreading/Reward`；灯网拥有逐节点进度及各处照明表现。 [接光盘][receiver] · [灯网][village] |
| `SkywellVoyage`、`SkywellVoyagePhase` | 拥有登车路径、载台上站位、运输路段和到站/交付状态。状态为 `Waiting/Boarding/Boarded/Riding/Docking/Arrived/Delivered`。 [运输][voyage] |

### 水面、鹿与云台

```mermaid
classDiagram
    direction LR
    class SkywellController["SkywellController"]
    class SkywellJourney["SkywellJourney"]
    class SkywellFish["SkywellFish"]
    class SkywellWindBell["SkywellWindBell"]
    class SkywellWorldFeedback["SkywellWorldFeedback"]
    class SkywellClimbAnimation["SkywellClimbAnimation"]
    class SkywellLadderVisual["SkywellLadderVisual"]
    class SkywellCaveCutaway["SkywellCaveCutaway"]
    class SkywellDeerGrove["SkywellDeerGrove"]
    class SkywellDeerRide["SkywellDeerRide"]
    class SkywellCloudFootTravel["SkywellCloudFootTravel"]
    class SkywellJumpAssist["SkywellJumpAssist"]
    class CourtPlayer["CourtPlayer"]
    class CourtCamera["CourtCamera"]
    class StandardHumanoidVisual["StandardHumanoidVisual"]
    SkywellController --> SkywellJourney : "互动和复位"
    SkywellController --> SkywellWindBell : "风令选择铃端点"
    SkywellController --> SkywellDeerGrove : "访问支线和复位"
    SkywellJourney --> CourtPlayer : "水面运动和爬梯接管"
    SkywellFish --> SkywellJourney : "监听引鱼和复位并调用回岸"
    SkywellWindBell --> SkywellFish : "到达铃位后引鱼"
    SkywellWorldFeedback --> SkywellFish : "监听铃声"
    SkywellWorldFeedback --> SkywellJourney : "监听复位"
    SkywellClimbAnimation --> SkywellJourney : "读取爬梯状态"
    SkywellClimbAnimation --> StandardHumanoidVisual : "播放爬梯姿势"
    SkywellLadderVisual ..> SkywellJourney : "使用共同的梯脚和梯顶锚点"
    SkywellCaveCutaway --> CourtPlayer : "读取人物位置"
    SkywellCaveCutaway --> CourtCamera : "读取实际视线"
    SkywellDeerGrove --> SkywellDeerRide : "解锁与鹿未载人姿势"
    SkywellDeerRide --> CourtPlayer : "骑乘外部运动"
    SkywellCloudFootTravel --> CourtPlayer : "徒步外部运动"
    SkywellCloudFootTravel --> SkywellDeerRide : "已解锁和支撑查询"
    SkywellDeerRide *-- SkywellJumpAssist : "骑乘跳跃辅助"
    SkywellCloudFootTravel *-- SkywellJumpAssist : "徒步跳跃辅助"
```

| 类型 | 自己拥有的状态与边界 |
| --- | --- |
| `SkywellJourney`、`SkywellTravel`、`SkywellClimbPhase` | 拥有船桨、晴牌、划船靠泊、回塔、辅助登船、攀梯与水面恢复。`SkywellTravel` 声明 `Walking/Descending/Swimming/Ascending/Returning/Docked/Climbing/Boarding`；保存场景采用水面路线。`SkywellClimbPhase` 将爬梯呈现分为 `None/Approach/Climbing/StepOff`。 [旅程][journey] |
| `SkywellFish`、`SkywellFishPhase`、`SkywellWindBell` | 鱼负责 `Patrol/Investigate/Warning/Chase`、视锥和物理遮挡、跟踪与捕获。风铃拥有端点请求和实际移动，达到目标后将引鱼事件交给鱼。 [鱼][fish] · [风铃][bell] |
| `SkywellWorldFeedback`、`SkywellClimbAnimation`、`SkywellLadderVisual`、`SkywellCaveCutaway` | 分别表现铃声/声波与借晴反馈、攀爬动画、梯子可见结构和洞壁遮挡。剖面切换 Renderer 的阴影模式，原碰撞器仍参与查询。 [反馈][feedback] · [攀爬动画][climb] · [梯子][ladder] · [剖面][cutaway] |
| `SkywellDeerGrove` | 拥有生长、脱落果实的重力和落地、鹿进食及云台显现；私有 `GrovePhase` 为 `Waiting/Approaching/Feeding/Revealing/Ready`，私有 `Fruit` 保存每个果实的位置、成熟及运动状态。 [果树支线][grove] |
| `SkywellDeerRide`、`SkywellCloudFootTravel`、`SkywellJumpAssist` | 骑乘拥有跨越顺序、最近安全面、卸载和鹿返程；徒步拥有解锁后当下支撑、移动、恢复与交还常规行走；两者各自持有可序列化的 `SkywellJumpAssist`，处理离地宽限、按键缓冲、起跳速度和空中转向。 [骑乘][ride] · [徒步][foot] · [跳跃辅助][jump] |
| `SkywellAirFeedback`、`SkywellSmokeProbe` | 前者保留空气紧张提示实现，保存场景无此组件；后者是 `-skywell-smoke` 明确开启的包内启动探针，正常关卡场景没有序列化实例。 [空气反馈][air] · [启动探针][smoke] |

### 可选辅助类

```mermaid
classDiagram
    direction LR
    class SkywellAirFeedback {
        +WarningPulses
    }
    class SkywellSmokeProbe {
        -StartOnlyWhenRequested()
    }
    SkywellAirFeedback --> SkywellJourney : 旧水下空气状态
    SkywellAirFeedback --> AudioSource : 警示声
    SkywellSmokeProbe ..> SkywellController : 探测启动与接线
```

保存的 L5 场景中没有这两者的实例。`SkywellSmokeProbe` 由独立试玩启动参数按需创建。 [空气反馈][air] · [启动探针][smoke]

## 主线与分支

```mermaid
flowchart TD
    A["西岸入口沿缓坡下行"] --> B["水面取桨并上船取晴牌"]
    B --> C["短按 E 划船到塔侧"]
    C --> D["面向爬梯按 W 到中央塔"]
    D --> E["在塔台施雨升高 6 米并锁止"]
    E --> F["风转至北口并走到采光庭"]
    F --> G["检查缺失的镜心并听守镜人提示"]
    G --> H["回塔转东并走到山阴村观察点"]
    H --> I["短按 E 观察旧景并与守灯人完成对话"]
    I --> J["拿集光石回北庭完成维修对话"]
    J --> K["北庭施风转至中央接力镜焦点"]
    K --> L["换晴并检查采日镜打中塔镜"]
    L --> M["回塔时晴牌回手且当地晴空持续"]
    M --> N["风转至东口并把实际光线送到山阴村"]
    N --> O["灯网逐处点亮并在货车生成水果"]
    N --> P["锁止保持塔高且原雨水转入运输配重"]
    O --> Q["东口高位、实时受光、满配重和奖励均满足"]
    P --> Q
    Q --> R["短按 E 实体登车，另按 E 发车"]
    R --> S["跨谷转正泊靠，走下车到花庭标记"]
    S --> T["短按 E 签收"]
    A -.-> U["果树岛与鹿和云台支线"]
    T --> V["R 回到入口"]
```

首次北庭查镜与守镜人提供路线线索。赠石的实际前置是打开东岸观察，随后完成守灯人对话；维修在带石完成北庭维修对话时提交。主线连续路线夹具从入口使用生产移动、对话和天气输入走到交付，并覆盖短按 `Q` 取消登车后重试。 [叙事条件][story] · [主线验证入口][route-checks] · [连续叙事段][story-checks]

### 短按 E 与当地天气

```mermaid
flowchart TD
    A["CourtPlayer 提交 E"] --> B{"有正在进行的对话或观察吗"}
    B -->|"有"| C["推进对话或关闭观察"]
    B -->|"无"| D{"达到 0.32 秒长按阈值吗"}
    D -->|"先松开"| E["依序检查骑乘、水面、放水、运输、叙事、观察、绞盘"]
    D -->|"继续按住"| F["进入可见目标瞄准并显示高亮"]
    H["长按 Tab 或 HUD 天气图标打开选择器"] --> I["菜单内用 1、2、3 或左右键选雨风晴"]
    I --> J["释放以确认手持令牌"]
    J --> F
    F --> K{"点击命中合格目标吗"}
    K -->|"否"| L["保留现状并显示阻挡原因"]
    K -->|"是"| M["请求插牌并设置本地天气或设备方向"]
    M --> N{"离开范围或把同一令牌插到别处吗"}
    N -->|"是，雨或风"| O["令牌回手并停止该站驱动"]
    N -->|"是，晴"| P["令牌回手并保留当地晴空"]
    N -->|"否"| Q["保持当前站的天气"]
```

- 远程施令读取有效目标表面、实际可见性与 **45 m** 距离。玩家在区外施令时记录原位，超过 **3.5 m** 或失去目标距离即回牌；本地施令按天气区边界回牌。塔台门槛有人时拒绝施令，塔台正在运动时拒绝再次给风。 [目标与远程范围][weather-input] · [塔台安全][controller] · [保存输入参数][scene-controller]
- 左键提交雨、晴和顺向风，右键只提交反向风。对塔台，顺向依次 `西 -90° → 北 0° → 东 +90°`；对采日镜，左键选中央接力镜，右键选校光位；水边风令选择铃的端点。天气区只能改变本站天气。接光盘需要物理入射。 [施令映射][weather-input] · [控制器派发][controller] · [光路][optics]
- 晴牌只有登船取得后可用。回收晴牌与当地晴空分开，重新施加其他天气或全关复位会改变当地晴空。天气选择器、瞄准模式可用 `Q` 或 `Esc` 退出；航程组件另在登车与已登车阶段处理 `Q`。 [令牌和取消][controller] · [天气状态][console] · [运输][voyage]

### 升降、接岸与同一份雨水

```mermaid
flowchart TD
    A["西口低位和空水槽"] --> B["塔天气区降雨超过 0.45 秒"]
    B --> C["四处集雨面按覆盖率蓄水至 6 单位"]
    C --> D["外环升高并携带站在转台碰撞面上的玩家"]
    D --> E["高位机械锁扣住 6 米高度"]
    E --> F["塔风推动 90 度或用绞盘回接同层已访问岸口"]
    F --> G{"高位东向已对齐且实时接光吗"}
    G -->|"是，运输仍等待"| H["以每秒 1 单位将水槽转入配重"]
    H --> I{"光线保持吗"}
    I -->|"失光"| J["暂停转水并保留高度和已转水"]
    J --> G
    I -->|"持续至满"| K["配重达到 6 单位"]
    K --> L{"奖励和实时接光仍成立吗"}
    L -->|"成立"| M["允许登乘与发车检查"]
    E --> N["回西向停稳并在转台操作放水阀"]
    N --> O["机械锁释放，按剩余水量下降"]
    O -->|"再次短按 E"| P["关阀并保留余水"]
```

保存场景参数为集雨和排水基准各 **1.5 单位/秒**、升降 **1.5 m/s**、转动 **30°/s**。转水分支按相同水量扣减水槽并增加配重，塔高由机械锁保持。集雨分支单独将水槽限制为 6 单位。实体桥随 Rotor 移动，`DockedPort` 同时检查静止、高度和角度。西口对应低位，北、东口对应高位。控制器按脚下向下射线的最近支撑判断玩家是否站在 Rotor 上，并依据同一局部站位更新玩家位移和偏航。 [场景参数][scene-controller] · [机械推进与对接][lift] · [支撑判定][controller]

### 采日镜、塔镜和山阴村

```mermaid
flowchart TD
    A["集光石装进采日镜"] --> B["北庭风转到中央接力镜焦点并锁定"]
    B --> C["换晴并检查方向太阳的真实入射"]
    C --> D{"晴覆盖镜面、太阳无遮挡、锁定和朝向均有效吗"}
    D -->|"否"| E["无有效出射线"]
    D -->|"是"| F["用实际镜面法向反射太阳方向"]
    F --> G{"塔镜有限矩形在最近固体之前且正面受光吗"}
    G -->|"否"| H["光束结束在最近交点或射程末端"]
    G -->|"是"| I["用随塔旋转的镜面法向做第二次反射"]
    I --> J{"二次反射在最近固体前命中接光盘正面吗"}
    J -->|"否"| H
    J -->|"是"| K["接光持续 1.5 秒"]
    K --> L["六处村灯依序点亮，各 0.45 秒"]
    L --> M["货车水果奖励可用"]
    K -->|"奖励前失光"| N["进度与村灯归零"]
    L -->|"奖励前失光"| N
    M -->|"奖励后失光"| O["水果保留，实时来光状态关闭"]
```

- 保存场景的链路是 `Sun → Fixed_reflector → Rotating_mirror → Settlement_sun_receiver`。`Fixed_reflector` 是采日镜自身的镜面；光学求解器另外保留的可选 `fixedMirror` 字段为空。镜台给出的第一束已由 `Vector3.Reflect` 求得，`SkywellOptics.Reflections` 从这束出射光开始计数。 [太阳反射][collector] · [求交与反射][optics] · [保存光学字段][scene-optics]
- 风只推动实际镜面向两个焦点停位移动。场景保存角速度 **60°/s**、锁定容差 **0.25°**。换晴后必须由当地晴空覆盖已修复的镜面、朝向正确、已锁止，且向真实太阳探测无阻挡。太阳及每段光线分别做物理遮挡查询。 [焦点参数][scene-collector] · [采日镜驱动][collector] · [段内遮挡查询][optics]
- 塔镜矩形半尺寸 **1.5 × 1.5 m**，接光盘半径 **1.4 m**，光学段最大距离 **180 m**。塔的东向停位让镜面的真实朝向具备几何条件，受光每帧仍由遮挡、有限口径与反射计算决定。转水和发车另外要求东口对齐及实时接光。 [塔镜设置][scene-optics] · [接光盘设置][scene-receiver] · [村灯设置][scene-village] · [发车条件][controller]

### 跨谷货运和实体交付

```mermaid
flowchart TD
    A["Waiting 等待"] --> B{"东口高位且满配重、实时来光、已有水果吗"}
    B -->|"未满足"| A
    B -->|"满足且距载台 6 米内短按 E"| C["计算地面节点间的胶囊可通过路径"]
    C -->|"无可走路径"| A
    C -->|"有可走路径"| D["Boarding 依碰撞辅助行走"]
    D -->|"失去条件、超时或按 Q"| A
    D -->|"抵达载台实地站位"| E["Boarded 保存玩家的载台局部站位"]
    E -->|"失去条件或按 Q"| A
    E -->|"再按 E"| F["Riding 按七个轨迹点携带玩家和水果"]
    F --> G["Docking 以每秒 45 度转正至世界偏航 180 度"]
    G --> H["Arrived 打开终端通道并恢复行走"]
    H --> I["离开载台走到接驳标记 2.1 米内"]
    I -->|"短按 E"| J["Delivered 交付一次"]
```

运输读取控制器的 `Complete`，其含义是当前可发车；运输组件自己的 `Delivered` 记录真正签收。登乘路径采样真实可站地面并验证连接上的胶囊通行。发车后沿路点按 **5.5 m/s** 或回望段 **3.2 m/s** 前进，持续按载台局部站位携带玩家；配重视觉随航程下降，原光路状态不参与推进已发车的航程。最终路点为 `(-26, 35, -4)`，交付点为世界向前偏移 7 m 后的 `(-26, 35, 3)`。 [运输与交付][voyage] · [控制器完成谓词][controller] · [七个轨迹点][scene-voyage]

### 水面、鱼与塔侧爬梯

```mermaid
flowchart TD
    A["沿西岸实体坡道到水边"] --> B{"在水面范围且脚下无已登记陆地吗"}
    B -->|"是"| C["进入水面游泳"]
    B -->|"否"| D["保持地面行走"]
    C --> E{"鱼在距离和视锥内看见游泳者吗"}
    E -->|"连续看见"| F["警戒后追击"]
    F -->|"近距离捕获"| G["回避难所并保留已经取到的物品"]
    F -->|"丢失视线"| H["回巡逻"]
    E -->|"遮挡或进入避难所"| H
    I["水边风令推动铃到选定端点，或在避难所按 E 操作铃"] --> J["鱼调查铃声位置"]
    C --> K["靠两根柱间按 E 拿桨"]
    D --> K
    G --> K
    J --> K
    K --> L["在可达船侧短按 E，经胶囊检查登船"]
    L --> M["船内短按 E 取晴牌"]
    M --> N["船桨和晴牌都到手后另按 E 划船"]
    N -->|"检测到障碍"| O["在障碍前停车"]
    N -->|"航道畅通"| P["靠泊中央塔"]
    P --> Q["面对梯脚按 W 或上键进入攀爬"]
    Q --> R["W 上爬，S 下爬，松键停在梯上"]
    R --> S["通过顶部碰撞检查后踏上塔台"]
```

图示选用先取桨的顺序。桨与晴牌有独立状态，先上船拿晴牌也有效；二者均取得后才允许划船。靠泊用船体盒形投射，攀梯用实际角色碰撞位移。现行入口走水面网格和干地支撑面，水面游泳不使用共享角色深水升降输入。 [水面与登船][journey] · [鱼视线和回收][fish] · [风铃][bell] · [保存水面参数][scene-journey] · [共享游泳输入][player]

### 果树、白鹿与徒步云路

```mermaid
flowchart TD
    A["果树岛施雨"] --> B["连续长成成熟苹果"]
    B --> C["改施风使苹果脱离树枝"]
    C --> D["成熟果实受重力并碰到实际地面"]
    D --> E["白鹿靠近、进食，云台逐步显现"]
    E --> F["显现结束后启用云台碰撞和骑乘"]
    F --> G["短按 E 骑鹿，WASD 或方向键移动，Space 跳"]
    G -->|"失足"| H["回最近安全支撑面"]
    H --> G
    G -->|"依序稳定落在果树岛、四块云台、外山庭"| I["终点稳定后卸载玩家并解锁徒步云路"]
    I --> J["鹿自行按云台反向跃回果树岛"]
    I --> K["玩家可徒步用 Space 跨越附近云台"]
    K -->|"失足或支撑不足"| L["回最近安全支撑面"]
    L --> K
    K -->|"落在可走端点"| M["交还共享常规步行"]
```

幼果可先被吹落，成熟果实实际着地才推进鹿的进食。保存场景的成长、进食、云台显现分别为 **2.5 s、2.2 s、1.6 s**。骑乘在稳定支撑上可短按 `E` 主动下鹿；自动徒步解锁要求实际按顺序落在六处支撑面并在终点稳定 **0.3 s**。骑乘和徒步各自通过共享角色的外部移动所有权处理本模式的碰撞和恢复。 [果树规则][grove] · [保存支线参数][scene-grove] · [骑乘与有序落点][ride] · [徒步与恢复][foot] · [共享外部运动][player]

### 整关 R 复位

```mermaid
flowchart TD
    A["R 由普通角色、旅程控制段或运输控制段接收"] --> B["SkywellController.ResetCourt"]
    B --> C["清镜头反馈并复位果树、鹿、观察和叙事"]
    C --> D["清天气输入与五站，清水量配重，西向回低位"]
    D --> E["重置风铃、采日镜、接光盘和村灯"]
    E --> F["同步塔与碰撞并恢复游戏镜头"]
    F --> G["载台回作者位置并进入等待"]
    G --> H["晴牌与船桨回位，船回起点，角色回入口"]
    H --> I["Journey 发出 ResetOccurred"]
    I --> J["鱼回巡逻并清铃声与借晴提示"]
```

`ResetCourt` 是完整重玩的入口。`ResetMechanisms` 只清机制并复位角色，可供专项检查。`RecoverToRefuge` 只恢复水面移动并把玩家放回避难所，保留已拿到的桨和晴牌。角色在攀梯、受控船行等阶段停用时由 `SkywellJourney` 接住 `R`；运输在已登车、行驶、泊靠阶段接住 `R`。 [全关复位][controller-reset] · [旅程复位][journey-reset] · [运输接键][voyage] · [共享输入][player]

## 对照入口

- 当前保存几何和引用从 `05_Skywell.unity` 查看，游戏角色的朝向、场景组件序列化字段和物理表面以该文件为准。 [保存场景][scene]
- 连续路线、目标施令、故事、鱼与支线的运行验证可从 `Editor/Skywell` 查脚本。主线与专项夹具各自记录输入和摆位范围；历史 `playmode.txt` 当前存的是 2026-10-28 的天气截图专项。本文根据保存文件和源码核对。 [连续路线夹具][route-checks] · [专项夹具目录][editor-checks] · [现有天气专项报告][weather-report]

[scene]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity
[scene-root]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L128076
[scene-camera]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L16272
[scene-player]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L34065
[scene-humanoid]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L23886
[scene-controller]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L49891
[scene-journey]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L49997
[scene-voyage]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L50096
[scene-story]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L50259
[scene-grove]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L50315
[scene-optics]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L19894
[scene-collector]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L26673
[scene-receiver]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L87039
[scene-village]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L33625
[scene-observation]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L73620
[scene-lighting]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L114299
[scene-rain]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L101238
[scene-water]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L61848
[scene-lift]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L109331
[scene-ladder]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L111477
[scene-cutaway]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L66123
[scene-target-tower]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L7127
[scene-target-collector]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L108698
[scene-target-refuge]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L39833
[scene-target-village]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L87068
[scene-target-grove]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L98772
[scene-zone-tower]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L126782
[scene-zone-collector]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L100928
[scene-zone-refuge]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L55962
[scene-zone-village]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L2908
[scene-zone-grove]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Scenes/05_Skywell.unity#L89919
[controller]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellController.cs#L84
[controller-reset]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellController.cs#L244
[weather-input]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherInput.cs#L34
[lift]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellLiftMechanism.cs#L69
[console]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherConsole.cs#L17
[zone]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherZone.cs#L44
[target]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherTarget.cs
[hud]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellHUD.cs
[rain]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellRainFeedback.cs
[support]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellLiftSupport.cs
[routing]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWaterRouting.cs
[observation]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellObservation.cs
[story]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellStory.cs#L129
[lighting]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSceneLighting.cs
[collector]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSunCollector.cs#L45
[optics]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellOptics.cs#L37
[receiver]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSunReceiver.cs#L32
[village]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellVillageLightNetwork.cs
[voyage]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellVoyage.cs#L23
[journey]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellJourney.cs#L189
[journey-reset]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellJourney.cs#L499
[fish]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellFish.cs#L86
[bell]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWindBell.cs#L45
[feedback]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWorldFeedback.cs
[climb]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellClimbAnimation.cs
[ladder]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellLadderVisual.cs
[cutaway]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellCaveCutaway.cs
[grove]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellDeerGrove.cs#L102
[ride]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellDeerRide.cs#L116
[foot]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellCloudFootTravel.cs
[jump]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellJumpAssist.cs
[air]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellAirFeedback.cs
[smoke]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSmokeProbe.cs#L12
[player]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/CourtPlayer.cs#L185
[gameplay]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/ICourtGameplay.cs
[humanoid]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/StandardHumanoidVisual.cs
[camera]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/CourtCamera.cs
[route-checks]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellChecks.cs#L74
[story-checks]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellStoryChecks.cs#L31
[editor-checks]: ../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell
[weather-report]: ../evidence/2026-10-28-weather-capture.txt
