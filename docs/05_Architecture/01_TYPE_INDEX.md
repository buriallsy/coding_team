# Unity 脚本类型索引

核对日期为 2026-11-11。范围为作者工程 `Assets/` 中全部 C# 文件。表中按源码文件列出显式声明的类、结构体、接口和枚举，含嵌套类型。Partial 在它出现的文件都列出，唯一类型计数合并同一类型的声明。

运行时列的挂载计数来自保存的四个作者场景及项目预制体，按序列化 `m_Script` GUID 统计直接脚本记录。空白表示无直接记录，运行时创建、间接预制体实例和纯 C# 类型可没有该记录。行号对应本次源码快照。

| 范围 | C# 文件 | 类型声明 | 唯一类型 |
| --- | ---: | ---: | ---: |
| Runtime | 46 | 68 | 66 |
| Editor | 51 | 78 | 63 |
| Tests | 13 | 13 | 13 |
| Template | 2 | 3 | 3 |
| **合计** | **112** | **162** | **145** |

脚本的行为及协作关系见 [Shared 与独立关卡](02_SHARED_AND_EARLY_LEVELS.md)、[L5 天井](03_SKYWELL.md)、[编辑器与测试](04_EDITOR_AND_TESTS.md)。

## Runtime · Legacy_BorrowedRain

| 源文件 | 类型声明与行号 | 序列化直接记录 |
| --- | --- | --- |
| [BorrowedRainController.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Legacy_BorrowedRain/BorrowedRainController.cs) | `BorrowedRainBindings` (class，L7) · `BorrowedRainSupport` (enum，L20) · `BorrowedRainController` (class，L23) · `BorrowedRainController.Interaction` (enum，L451) · `BorrowedRainController.SafeReturnKind` (enum，L452) | 旧 L3 × 1 |
| [BorrowedRainHUD.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Legacy_BorrowedRain/BorrowedRainHUD.cs) | `BorrowedRainHUD` (class，L5) | 旧 L3 × 1 |
| [BorrowedRainState.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Legacy_BorrowedRain/BorrowedRainState.cs) | `BorrowedRainState` (class，L6) | — |

## Runtime · RainCargo

| 源文件 | 类型声明与行号 | 序列化直接记录 |
| --- | --- | --- |
| [RainCargoController.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/RainCargo/RainCargoController.cs) | `RainCargoBindings` (class，L7) · `RainCargoController` (class，L21) | L3 × 1 |
| [RainCargoCutaway.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/RainCargo/RainCargoCutaway.cs) | `RainCargoCutaway` (class，L8) | — |
| [RainCargoHUD.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/RainCargo/RainCargoHUD.cs) | `RainCargoHUD` (class，L6) | L3 × 1 |
| [RainCargoPushAssist.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/RainCargo/RainCargoPushAssist.cs) | `RainCargoPushAssist` (class，L6) · `RainCargoPushAssist.Phase` (enum，L8) | — |
| [RainCargoState.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/RainCargo/RainCargoState.cs) | `RainCargoState` (class，L6) | — |

## Runtime · Shared

| 源文件 | 类型声明与行号 | 序列化直接记录 |
| --- | --- | --- |
| [CourtCamera.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/CourtCamera.cs) | `CourtCamera` (class，L7) | L1 × 1, 旧 L3 × 1, L3 × 1, L5 × 1 |
| [CourtPlayer.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/CourtPlayer.cs) | `CourtPlayer` (class，L8) | L1 × 1, 旧 L3 × 1, L3 × 1, L5 × 1 |
| [ICourtGameplay.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/ICourtGameplay.cs) | `ICourtGameplay` (interface，L6) | — |
| [StandardHumanoidVisual.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Shared/StandardHumanoidVisual.cs) | `StandardHumanoidVisual` (class，L9) | L3 × 1, L5 × 1 |

## Runtime · Skywell

| 源文件 | 类型声明与行号 | 序列化直接记录 |
| --- | --- | --- |
| [SkywellAirFeedback.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellAirFeedback.cs) | `SkywellAirFeedback` (class，L5) | — |
| [SkywellCaveCutaway.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellCaveCutaway.cs) | `SkywellCaveCutaway` (class，L9) | L5 × 1 |
| [SkywellClimbAnimation.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellClimbAnimation.cs) | `SkywellClimbAnimation` (class，L7) | L5 × 1 |
| [SkywellCloudFootTravel.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellCloudFootTravel.cs) | `SkywellCloudFootTravel` (class，L8) | L5 × 1 |
| [SkywellController.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellController.cs) | `SkywellController` (class, partial，L7) · `SkywellController.InteractionKind` (enum，L121) · `SkywellController.InteractionTarget` (struct，L122) | L5 × 1 |
| [SkywellDeerGrove.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellDeerGrove.cs) | `SkywellDeerGrove` (class，L8) · `SkywellDeerGrove.GrovePhase` (enum，L41) · `SkywellDeerGrove.Fruit` (class，L42) | L5 × 1 |
| [SkywellDeerRide.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellDeerRide.cs) | `SkywellDeerRide` (class，L9) | L5 × 1 |
| [SkywellFish.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellFish.cs) | `SkywellFishPhase` (enum，L5) · `SkywellFish` (class，L7) | L5 × 1 |
| [SkywellHUD.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellHUD.cs) | `SkywellHUD` (class，L6) | L5 × 1 |
| [SkywellJourney.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellJourney.cs) | `SkywellTravel` (enum，L8) · `SkywellClimbPhase` (enum，L9) · `SkywellJourney` (class，L11) | L5 × 1 |
| [SkywellJumpAssist.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellJumpAssist.cs) | `SkywellJumpAssist` (class，L8) | — |
| [SkywellLadderVisual.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellLadderVisual.cs) | `SkywellLadderVisual` (class，L8) | L5 × 1 |
| [SkywellLiftMechanism.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellLiftMechanism.cs) | `SkywellController` (class, partial，L5) · `SkywellController.WaterFlow` (enum，L33) | — |
| [SkywellLiftSupport.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellLiftSupport.cs) | `SkywellLiftSupport` (class，L6) | L5 × 1 |
| [SkywellObservation.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellObservation.cs) | `SkywellObservation` (class，L6) | L5 × 1 |
| [SkywellOptics.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellOptics.cs) | `SkywellOptics` (class，L6) | L5 × 1 |
| [SkywellRainFeedback.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellRainFeedback.cs) | `SkywellRainFeedback` (class，L6) | L5 × 1 |
| [SkywellSceneLighting.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSceneLighting.cs) | `SkywellSceneLighting` (class，L8) | L5 × 1 |
| [SkywellSmokeProbe.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSmokeProbe.cs) | `SkywellSmokeProbe` (class，L10) | — |
| [SkywellStory.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellStory.cs) | `SkywellStory` (class，L8) · `SkywellStory.Completion` (enum，L90) | L5 × 1 |
| [SkywellSunCollector.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSunCollector.cs) | `SkywellSunCollector` (class，L7) | L5 × 1 |
| [SkywellSunReceiver.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellSunReceiver.cs) | `SettlementSunState` (enum，L5) · `SkywellSunReceiver` (class，L7) | L5 × 1 |
| [SkywellVillageLightNetwork.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellVillageLightNetwork.cs) | `SkywellVillageLightNetwork` (class，L6) | L5 × 1 |
| [SkywellVoyage.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellVoyage.cs) | `SkywellVoyagePhase` (enum，L7) · `SkywellVoyage` (class，L9) | L5 × 1 |
| [SkywellWaterRouting.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWaterRouting.cs) | `SkywellWaterRouting` (class，L6) | L5 × 1 |
| [SkywellWeatherConsole.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherConsole.cs) | `SkywellToken` (enum，L5) · `SkywellWeatherConsole` (class，L11) | L5 × 5 |
| [SkywellWeatherInput.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherInput.cs) | `SkywellController` (class, partial，L7) | — |
| [SkywellWeatherTarget.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherTarget.cs) | `SkywellWeatherTarget` (class，L9) · `SkywellWeatherTarget.HighlightSlot` (class，L233) · `SkywellWeatherTarget.MeshSurface` (class，L245) | L5 × 5 |
| [SkywellWeatherZone.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWeatherZone.cs) | `SkywellWeatherZone` (class，L7) | L5 × 5 |
| [SkywellWindBell.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWindBell.cs) | `SkywellWindBell` (class，L7) | L5 × 1 |
| [SkywellWorldFeedback.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/Skywell/SkywellWorldFeedback.cs) | `SkywellWorldFeedback` (class，L6) | L5 × 1 |

## Runtime · WaterCourt

| 源文件 | 类型声明与行号 | 序列化直接记录 |
| --- | --- | --- |
| [CourtController.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/WaterCourt/CourtController.cs) | `CourtController` (class，L6) · `CourtController.Interaction` (enum，L291) · `MovingDeckSupport` (enum，L294) | L1 × 1 |
| [CourtHUD.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/WaterCourt/CourtHUD.cs) | `CourtHUD` (class，L5) | L1 × 1 |
| [CourtState.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Runtime/WaterCourt/CourtState.cs) | `CourtState` (class，L6) | — |

## Editor · Legacy_BorrowedRain

| 源文件 | 类型声明与行号 |
| --- | --- |
| [BorrowedRainBuilder.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Legacy_BorrowedRain/BorrowedRainBuilder.cs) | `BorrowedRainBuilder` (class，L14) |
| [BorrowedRainPlayModeChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Legacy_BorrowedRain/BorrowedRainPlayModeChecks.cs) | `BorrowedRainPlayModeChecks` (class，L13) |
| [BorrowedRainValidation.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Legacy_BorrowedRain/BorrowedRainValidation.cs) | `BorrowedRainValidation` (class，L14) |

## Editor · RainCargo

| 源文件 | 类型声明与行号 |
| --- | --- |
| [RainCargoBuilder.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/RainCargo/RainCargoBuilder.cs) | `RainCargoBuilder` (class，L14) |
| [RainCargoDropChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/RainCargo/RainCargoDropChecks.cs) | `RainCargoDropChecks` (class，L14) · `RainCargoDropChecks.DropProbe` (class，L202) |
| [RainCargoPlayChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/RainCargo/RainCargoPlayChecks.cs) | `RainCargoPlayChecks` (class，L16) |
| [RainCargoPushChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/RainCargo/RainCargoPushChecks.cs) | `RainCargoPushChecks` (class，L14) · `RainCargoPushChecks.MotionProbe` (class，L224) |
| [RainRevisionChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/RainCargo/RainRevisionChecks.cs) | `RainRevisionChecks` (class，L18) |

## Editor · Shared

| 源文件 | 类型声明与行号 |
| --- | --- |
| [CourtSceneSync.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Shared/CourtSceneSync.cs) | `CourtSceneSync` (class，L14) · `CourtSceneSync.MaterialData` (class，L22) · `CourtSceneSync.SubmeshData` (class，L23) · `CourtSceneSync.ObjectData` (class，L24) · `CourtSceneSync.CameraData` (class，L30) · `CourtSceneSync.LightData` (class，L31) · `CourtSceneSync.SceneData` (class，L32) |
| [ProjectValidation.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Shared/ProjectValidation.cs) | `ProjectValidation` (class，L13) · `ProjectValidation.TestResults` (class，L17) |
| [ProjectWorkRequest.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Shared/ProjectWorkRequest.cs) | `ProjectWorkRequest` (class，L11) · `ProjectWorkRequest.Request` (class，L13) · `ProjectWorkRequest.Result` (class，L14) |
| [StandardHumanoidInstaller.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Shared/StandardHumanoidInstaller.cs) | `StandardHumanoidInstaller` (class，L11) |

## Editor · Skywell

| 源文件 | 类型声明与行号 |
| --- | --- |
| [SkywellBoatRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellBoatRevision.cs) | `SkywellBoatRevision` (class，L12) |
| [SkywellBorrowingSunMigration.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellBorrowingSunMigration.cs) | `SkywellBorrowingSunMigration` (class，L14) · `SkywellBorrowingSunMigration.TransformRecord` (class，L336) · `SkywellBorrowingSunMigration.RayRecord` (class，L337) · `SkywellBorrowingSunMigration.MigrationReport` (class，L338) |
| [SkywellBuilder.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellBuilder.cs) | `SkywellBuilder` (class, partial，L14) |
| [SkywellCameraChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellCameraChecks.cs) | `SkywellChecks` (class, partial，L11) |
| [SkywellCameraSetup.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellCameraSetup.cs) | `SkywellCameraSetup` (class，L13) · `SkywellCameraSetup.AuthorView` (class，L103) |
| [SkywellCaveRouteRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellCaveRouteRevision.cs) | `SkywellCaveRouteRevision` (class，L13) |
| [SkywellChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellChecks.cs) | `SkywellPlayCheckDriver` (class，L15) · `SkywellChecks` (class, partial，L20) |
| [SkywellClimbAnimationSetup.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellClimbAnimationSetup.cs) | `SkywellClimbAnimationSetup` (class，L12) |
| [SkywellCloudRouteRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellCloudRouteRevision.cs) | `SkywellCloudRouteRevision` (class，L11) |
| [SkywellCollectorShape.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellCollectorShape.cs) | `SkywellCollectorShape` (class，L13) · `SkywellCollectorShape.Geometry` (class，L165) |
| [SkywellCollectorViews.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellCollectorViews.cs) | `SkywellChecks` (class, partial，L9) |
| [SkywellCourtyardRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellCourtyardRevision.cs) | `SkywellCourtyardRevision` (class，L13) · `SkywellBuilder` (class, partial，L143) |
| [SkywellDeerGroveChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellDeerGroveChecks.cs) | `SkywellChecks` (class, partial，L12) |
| [SkywellDeerGroveSetup.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellDeerGroveSetup.cs) | `SkywellDeerGroveSetup` (class，L14) · `SkywellDeerGroveSetup.MeshData` (class，L413) |
| [SkywellDeerRideChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellDeerRideChecks.cs) | `SkywellChecks` (class, partial，L11) |
| [SkywellDeerRouteChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellDeerRouteChecks.cs) | `SkywellChecks` (class, partial，L13) |
| [SkywellDirectBoardingRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellDirectBoardingRevision.cs) | `SkywellDirectBoardingRevision` (class，L13) |
| [SkywellLadderHeightRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellLadderHeightRevision.cs) | `SkywellLadderHeightRevision` (class，L14) · `SkywellChecks` (class, partial，L117) |
| [SkywellNpcAppearance.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellNpcAppearance.cs) | `SkywellNpcAppearance` (class，L13) |
| [SkywellObservationChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellObservationChecks.cs) | `SkywellChecks` (class, partial，L10) |
| [SkywellObservationSetup.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellObservationSetup.cs) | `SkywellObservationSetup` (class，L10) |
| [SkywellPortfolioTools.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellPortfolioTools.cs) | `SkywellBuilder` (class, partial，L13) · `SkywellBuilder.PortfolioView` (class，L15) · `SkywellBuilder.PortfolioViews` (class，L16) |
| [SkywellRefugeChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellRefugeChecks.cs) | `SkywellChecks` (class, partial，L11) |
| [SkywellRemoteReturnChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellRemoteReturnChecks.cs) | `SkywellChecks` (class, partial，L8) |
| [SkywellStoryChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellStoryChecks.cs) | `SkywellChecks` (class, partial，L10) |
| [SkywellStorySetup.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellStorySetup.cs) | `SkywellStorySetup` (class，L11) · `SkywellStorySetup.MeshData` (class，L144) |
| [SkywellSunChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellSunChecks.cs) | `SkywellSunCheckDriver` (class，L10) · `SkywellSunChecks` (class，L12) |
| [SkywellSurfaceWaterRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellSurfaceWaterRevision.cs) | `SkywellSurfaceWaterRevision` (class，L13) |
| [SkywellTargetingChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellTargetingChecks.cs) | `SkywellChecks` (class, partial，L13) · `SkywellChecks.WeatherHighlightSnapshot` (class，L281) |
| [SkywellTraversalFlowRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellTraversalFlowRevision.cs) | `SkywellTraversalFlowRevision` (class，L12) |
| [SkywellTuningWindow.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellTuningWindow.cs) | `SkywellTuningWindow` (class，L7) · `SkywellFishInspector` (class，L56) |
| [SkywellViews.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellViews.cs) | `SkywellBuilder` (class, partial，L9) |
| [SkywellWeatherChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellWeatherChecks.cs) | `SkywellChecks` (class, partial，L12) |
| [SkywellWeatherMaterialFix.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellWeatherMaterialFix.cs) | `SkywellWeatherMaterialFix` (class，L14) |
| [SkywellWeatherTargetSetup.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/Skywell/SkywellWeatherTargetSetup.cs) | `SkywellWeatherTargetSetup` (class，L13) · `SkywellWeatherTargetSetup.Binding` (class，L358) |

## Editor · WaterCourt

| 源文件 | 类型声明与行号 |
| --- | --- |
| [CourtBuilder.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/WaterCourt/CourtBuilder.cs) | `CourtBuilder` (class，L12) |
| [CourtMountainRevision.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/WaterCourt/CourtMountainRevision.cs) | `CourtMountainRevision` (class，L12) |
| [CourtPlayModeChecks.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/WaterCourt/CourtPlayModeChecks.cs) | `CourtPlayModeChecks` (class，L14) |
| [CourtValidation.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Editor/WaterCourt/CourtValidation.cs) | `CourtValidation` (class，L14) |

## Tests · Editor

| 源文件 | 类型声明与行号 |
| --- | --- |
| [BorrowedRainStateTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/BorrowedRainStateTests.cs) | `BorrowedRainStateTests` (class，L5) |
| [CourtPlayerExternalMotionTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/CourtPlayerExternalMotionTests.cs) | `CourtPlayerExternalMotionTests` (class，L6) |
| [CourtPlayerViewFacingTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/CourtPlayerViewFacingTests.cs) | `CourtPlayerViewFacingTests` (class，L6) |
| [CourtStateTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/CourtStateTests.cs) | `CourtStateTests` (class，L6) |
| [RainCargoStateTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/RainCargoStateTests.cs) | `RainCargoStateTests` (class，L5) |
| [SkywellBoatJourneyTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellBoatJourneyTests.cs) | `SkywellBoatJourneyTests` (class，L9) |
| [SkywellCloudRouteTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellCloudRouteTests.cs) | `SkywellCloudRouteTests` (class，L8) |
| [SkywellJumpAssistTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellJumpAssistTests.cs) | `SkywellJumpAssistTests` (class，L6) |
| [SkywellSolarRulesTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellSolarRulesTests.cs) | `SkywellSolarRulesTests` (class，L8) |
| [SkywellStoryTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellStoryTests.cs) | `SkywellStoryTests` (class，L8) |
| [SkywellSurfaceFishTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellSurfaceFishTests.cs) | `SkywellSurfaceFishTests` (class，L8) |
| [SkywellWeatherReturnTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellWeatherReturnTests.cs) | `SkywellWeatherReturnTests` (class，L7) |
| [SkywellWeatherTargetTests.cs](../../unity_prototype/BorrowingSun/Assets/BorrowingSun/Tests/Editor/SkywellWeatherTargetTests.cs) | `SkywellWeatherTargetTests` (class，L7) |

## Template · TutorialInfo

| 源文件 | 类型声明与行号 |
| --- | --- |
| [ReadmeEditor.cs](../../unity_prototype/BorrowingSun/Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs) | `ReadmeEditor` (class，L11) |
| [Readme.cs](../../unity_prototype/BorrowingSun/Assets/TutorialInfo/Scripts/Readme.cs) | `Readme` (class，L4) · `Readme.Section` (class，L12) |
