using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    /// <summary>Drives real keyboard and pointer input in the player loop. Fixture placement is explicitly reported per check.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class SkywellPlayCheckDriver : MonoBehaviour
    {
        private void Update(){SkywellChecks.PlayTick();}
        private void OnApplicationFocus(bool focused){SkywellChecks.TraceCheckFocus(focused);}
    }
    public static partial class SkywellChecks
    {
        private static SkywellController court;
        private static IEnumerator sequence;
        public static bool IsRunning=>sequence!=null;
        private static Keyboard keyboard,previous;
        private static Mouse pointer,previousPointer;
        private static MouseState pointerInput;
        private static SkywellPlayCheckDriver driver;
        private static KeyboardState input;
        private static Vector3 navigationError;
        private static bool previousRunInBackground;
        private static InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private static List<string> report;
        private static string module;
        private static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        private static string ValidationFolder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell"));
        private static string Folder=>module==FlowerMarketModule?FlowerMarketFolder:ValidationFolder;

        [MenuItem("Borrowing Sun/Skywell/3 Run Borrowing Sun complete route")]
        public static void Run()=>StartCheck("SKYWELL_BORROWING_SUN_ROUTE",FullRoute());
        public static void RunPortfolio()=>StartCheck("SKYWELL_PORTFOLIO",FullRoute());
        public static void RunPortfolioShoulder()=>StartCheck("SKYWELL_PORTFOLIO_SHOULDER",FullRoute(true));
        public static void RunUnderwater()=>StartCheck("SKYWELL_SURFACE_PADDLE_BOAT",SurfaceWaterCheck());
        public static void RunOpenDockChecks()=>StartCheck("SKYWELL_OPEN_DOCK",OpenDockSequence());
        public static void RunCompactWeather()=>StartCheck("SKYWELL_WEATHER_INPUT",WeatherInputSequence());
        public static void RunFishTuning()=>StartCheck("SKYWELL_FISH_TUNING",FishTuningSequence());

        private static void StartCheck(string name,IEnumerator run)
        {
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||sequence!=null||SkywellSunChecks.IsRunning)
                throw new InvalidOperationException("Enter unpaused Play Mode with no other Skywell checks running.");
            court=UnityEngine.Object.FindAnyObjectByType<SkywellController>();
            if(!court)throw new InvalidOperationException("Open the Skywell scene.");
            module=name;Directory.CreateDirectory(Folder);
            report=new List<string>{DateTime.UtcNow.ToString("O"),name,"Production Input System, CharacterController, local weather and actual gameplay rays. Automated checks do not establish first-player understanding or fun."};
            previousRunInBackground=Application.runInBackground;previousBackgroundBehavior=InputSystem.settings.backgroundBehavior;
            Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            previous=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();input=new KeyboardState();navigationError=Vector3.zero;
            previousPointer=Mouse.current;pointer=InputSystem.AddDevice<Mouse>();pointer.MakeCurrent();
            pointerInput=new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)};
            driver=new GameObject("Skywell_input_check_driver"){hideFlags=HideFlags.HideAndDontSave}.AddComponent<SkywellPlayCheckDriver>();
            AssemblyReloadEvents.beforeAssemblyReload+=Abort;EditorApplication.playModeStateChanged+=OnPlay;
            if(!Application.isBatchMode){var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).Focus();}
            sequence=run;SaveProgress();
        }
        internal static void PlayTick()
        {
            if(sequence==null)return;
            Tick();
            if(keyboard!=null&&keyboard.added){keyboard.MakeCurrent();InputState.Change(keyboard,input);if(keyboard.tabKey.wasPressedThisFrame)TraceWeatherInput("Tab down submitted before production Update");}
            if(pointer!=null&&pointer.added){pointer.MakeCurrent();InputState.Change(pointer,pointerInput);}
        }
        private static IEnumerator FullRoute(bool shoulder=false,bool continueAfterDelivery=false)
        {
            report.Add("CONTINUOUS ROUTE: reset entrance to Delivered; no player placement, state injection, forced fish position or remote token requests.");
            if(shoulder)report.Add("SHOULDER ROUTE: actual Z after entrance reset selects shoulder view; the same continuous route and production input helpers are reused. Dialogue temporarily owns its close camera, while the selected gameplay mode must remain shoulder until the final R reset.");
            yield return SurfaceWaterSequence(shoulder);
            Require(court.Journey.HasReturnedToTower&&court.Journey.SunnyTokenObtained,"Real pickup and return unlocked the Sunny ability.");
            yield return Walk(Local(-2.8f,-3),"Ladder landing to tower aisle");
            yield return Walk(Local(-2.4f,-.8f),"Approach tower weather station");
            yield return SetWeather(SkywellToken.Rain,0);
            yield return CarryUntil(()=>court.Height>=court.PlatformHighY-.01f,15,"Rain fills counterweights and lifts the loaded deck");
            CheckLiftBase("high");
            var rain=court.GetComponentInChildren<SkywellRainFeedback>();if(rain)CheckCounterweights(rain,"full rain store");
            float storedWater=court.WaterLevel;
            yield return TurnTowerTo(0);
            Require(!court.Consoles[0].Zone.IsRain&&Mathf.Abs(court.WaterLevel-storedWater)<.01f,"Changing Rain to Wind preserves stored water and raised tower.");
            yield return ToCollectorStation();
            Require(court.Consoles[0].Token==SkywellToken.None&&court.TokenLocation(SkywellToken.Wind)=="在手中","Walking out of the tower weather zone automatically returns Wind.");
            var collector=court.Optics.Collector;
            Require(collector&&collector.FocusIndex==0&&!collector.Active&&!collector.IsRepaired,"The collector begins with an unpowered calibration pose and missing mirror heart.");
            yield return StoryContinuousLoop();
            if(shoulder)Require(court.CameraRig.IsShoulderView,"Shoulder gameplay mode survives the continuous story, observation and repair loop.");
            Require(collector.IsRepaired&&!court.Story.HasGem,"The continuous east-and-north journey repaired the collector before weather operation.");
            yield return SetWeather(SkywellToken.Wind,1);
            yield return WaitUntil(()=>collector.FocusIndex==1&&collector.IsLocked,5,"Local Wind physically turns the collector to its relay stop");
            Require(!collector.Active&&!court.BorrowedSunAtVillage,"Wind changes the real mirror pose without producing sunlight.");
            yield return SetWeather(SkywellToken.Sun,0);
            Require(collector.Active&&court.Optics.HitsMovingMirror,"Sunny and the Wind-aligned plate send a real beam into the central relay aperture.");
            Require(!court.BorrowedSunAtVillage,"The relay still needs its eastern orientation before the village receives light.");
            yield return Wait(1);
            Capture(Path.Combine(Folder,"borrowing-sun-first-long-beam.png"));
            yield return ToTowerConsole();
            Require(court.Consoles[1].Token==SkywellToken.None&&court.TokenLocation(SkywellToken.Sun)=="在手中"&&court.Consoles[1].Zone.IsSunny&&collector.Active,"Leaving the collector returns Sunny while its local clear sky and physical beam continue.");
            yield return TurnTowerTo(90);
            Require(court.DockedPort==2&&court.BorrowedSunAtVillage,"Wind alignment redirects real borrowed sunlight into the village receiver.");
            float conserved=court.WaterLevel+court.BallastAmount;
            var receiver=court.Optics.SettlementReceiver;
            yield return WaitUntil(()=>receiver.State==SettlementSunState.Spreading||receiver.RewardAvailable,8,"Receiver confirms real sunlight before village spread");
            Require(receiver.VillageNetwork,"Village visual sequence is linked to the existing receiver.");
            yield return WaitUntil(()=>receiver.RewardAvailable,15,"Village light sequence completes before fruit appears");
            Require(receiver.VillageNetwork.Complete&&receiver.fruitCrate.activeSelf&&receiver.fruitCrate.transform.IsChildOf(court.Cart),"Existing fruit appears on the existing cart after village illumination.");
            Require(receiver.RewardToastVisible,"Existing fruit notification appears once at reward completion.");
            Capture(Path.Combine(Folder,"borrowing-sun-village-fruit.png"));
            yield return Walk(Local(-2.4f,-3.7f),"Leave weather station while water transfers to transport counterweight");
            yield return WaitUntil(()=>court.Complete,15,"Stored water reaches transport ballast and departure becomes ready");
            Require(Mathf.Abs(conserved-court.WaterLevel-court.BallastAmount)<.02f,"Cargo transfer conserves the original Rain water.");
            Require(court.Height>court.PlatformHighY-.01f&&court.LiftLatched,"Mechanical latch retains tower height after transfer.");
            yield return Walk(Local(-2.8f,3.9f),"Reach cart-side boarding aisle");
            var voyage=court.Voyage;yield return Press(Key.E);
            yield return WaitUntil(()=>voyage.Phase==SkywellVoyagePhase.Boarded,13,"Real collision-aware boarding reaches the rider position");
            yield return Press(Key.Q);Require(voyage.Phase==SkywellVoyagePhase.Waiting&&court.Player.enabled,"Q cancels boarding without losing fruit or progress.");
            yield return Press(Key.E);yield return WaitUntil(()=>voyage.Phase==SkywellVoyagePhase.Boarded,13,"Reboarding remains possible");
            yield return Press(Key.E);Require(voyage.Phase==SkywellVoyagePhase.Riding,"E starts the existing cargo journey.");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"Cargo departure retains shoulder gameplay mode.");
            yield return WaitUntil(()=>voyage.Phase==SkywellVoyagePhase.Arrived,120,"Cargo reaches the real terminal and finishes docking");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"The actual cargo ride and docking retain shoulder gameplay mode.");
            Require(court.Consoles[0].Token==SkywellToken.None&&court.TokenLocation(SkywellToken.Wind)=="在手中","Leaving the tower aboard the carrier automatically returns Wind without a retrieval key.");
            Require(voyage.MaximumRiderDrift<.03f&&receiver.fruitCrate.activeSelf,"Rider and earned fruit stay on the real carrier through transit.");
            yield return Press(Key.E);Require(!voyage.Delivered,"Arrival aboard the carrier does not skip physical delivery.");
            yield return Walk(voyage.ExitPoint,"Walk off carrier to the actual handover marker");
            yield return Press(Key.E);Require(voyage.Delivered,"Existing Delivered state is reached at the terminal.");
            if(continueAfterDelivery)yield break;
            yield return Press(Key.E);Require(voyage.Delivered,"Repeated confirmation does not duplicate delivery.");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"Continuous shoulder traversal reaches physical delivery before reset.");
            Capture(Path.Combine(Folder,"borrowing-sun-delivered.png"));
            yield return Press(Key.R);yield return Wait(.4f);CheckReset();
            if(shoulder)Require(!court.CameraRig.IsShoulderView,"Final real R resets the completed shoulder route to fixed gameplay view.");
        }
        private static IEnumerator SurfaceWaterCheck()
        {yield return SurfaceWaterSequence();yield return Press(Key.R);yield return Wait(.3f);CheckReset();}
        private static IEnumerator SurfaceWaterSequence(bool shoulder=false)
        {
            court.ResetCourt();yield return Wait(.4f);var j=court.Journey;var fish=court.GetComponent<SkywellFish>();
            if(shoulder)yield return EnsureCameraMode(true);
            Require(j&&j.SurfaceConfigured&&j.PaddlePickup&&j.SunnyToken&&!j.PaddleObtained&&!j.SunnyTokenObtained&&!court.CanUseToken(SkywellToken.Sun),"Surface paddle and boat Sunny start separately unowned.");
            Require(!j.BelowGroundActive&&!court.Player.IsSwimming,"Player starts on the ground entrance.");
            float towerY=court.Rotor.position.y;Vector3 refuge=j.RefugePoint;
            yield return Walk(court.Origin+new Vector3(-18,0,0),"West bridge to the surface approach");
            foreach(var waypoint in j.SurfaceRouteWaypoints)
                yield return Walk(waypoint.position+Vector3.up*.03f,"Static shoreline ramp / "+waypoint.name,true);
            yield return Walk(j.RefugePoint,"Open water-edge dock");
            Require(j.InRefuge&&!court.Player.IsSwimming&&!j.ControlsMovement&&Mathf.Abs(court.Rotor.position.y-towerY)<.01f&&Vector3.Distance(refuge,j.RefugePoint)<.01f,"Actual static ramp reaches the open dock; no moving entry lift or tower movement.");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"Shoulder mode survives the whole static descent.");
            Vector3 stationPoint=court.Consoles[2].transform.position+Vector3.left*1.4f;stationPoint.y=j.RefugePoint.y;
            yield return Walk(stationPoint,"Local wind target at the open dock");
            yield return SetWeather(SkywellToken.Wind,1);yield return WaitUntil(()=>court.WindBell.WindSettled,6,"Left wind physically moves the bell east");
            Require(court.WindBell.BellPosition>.99f&&fish.LureCount>0,"Moving sound source rings on arrival and attracts the live fish.");
            yield return SetWeather(SkywellToken.Wind,0);yield return WaitUntil(()=>court.WindBell.WindSettled,6,"Right wind moves the bell west to open the eastern route");
            float bellX=court.WindBell.LurePosition.x;
            yield return Walk(new Vector3(-19,j.RefugePoint.y,93),"Reach manual bell at dock");
            int lures=fish.LureCount;yield return Press(Key.E);
            Require(fish.LureCount==lures+1&&fish.Phase==SkywellFishPhase.Investigate,"Manual bell refreshes attraction at its physically positioned source.");
            yield return Walk(j.RefugePoint,"Return to open dock exit aisle");
            yield return Walk(new Vector3(-12,j.SurfaceFeetHeight,94),"WASD leaves the open dock onto the water surface");
            Require(court.Player.IsSurfaceSwimming&&!j.InRefuge,"Leaving the dock starts horizontal surface movement without diving.");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"Actual surface swimming retains shoulder view.");
            yield return Walk(new Vector3(-8,j.SurfaceFeetHeight,87),"Surface route behind the physical cover column");
            yield return Walk(new Vector3(j.PaddlePickup.position.x,j.SurfaceFeetHeight,j.PaddlePickup.position.z),"Reach the floating paddle");
            yield return Press(Key.E);
            Require(j.PaddleObtained&&!j.SunnyTokenObtained&&!j.PaddlePickup.gameObject.activeSelf&&!court.CanUseToken(SkywellToken.Sun),"Surface E takes only the paddle; Sunny still waits on the boat.");
            Require(j.Recoveries==0,"Continuous bell and cover route reaches the paddle without forced recovery.");
            Require(court.Consoles[2].Token==SkywellToken.None&&!court.Consoles[2].Zone.IsWind&&court.TokenLocation(SkywellToken.Wind)=="在手中","Swimming out of the dock zone returns Wind and stops local wind.");
            Require(Mathf.Abs(bellX-court.WindBell.LurePosition.x)<.01f,"The bell retains its physical position after automatic Wind return.");
            Capture(Path.Combine(Folder,"surface-paddle.png"));
            Vector3 boatFront=j.ReturnShuttle.TransformPoint(new Vector3(0,.03f,-2.5f));boatFront.y=j.SurfaceFeetHeight;
            yield return Walk(boatFront,"Surface swim near the boat without aligning to its opening");
            Require(!j.InReturnCabin&&court.Player.IsSurfaceSwimming,"Surface approach remains outside the boat hull.");
            yield return BoardBoatFromNearby("Nearby E boards through the production collision-checked motion");
            Require(j.InReturnCabin&&!court.Player.IsSwimming&&!j.SunnyTokenObtained,"Boarding reaches the real deck without silently collecting Sunny.");
            yield return Press(Key.E);
            Require(j.SunnyTokenObtained&&court.CanUseToken(SkywellToken.Sun)&&!j.SunnyToken.gameObject.activeSelf&&j.Travel==SkywellTravel.Walking,"Separate E aboard the boat takes Sunny without starting the ride.");
            float boatY=j.ReturnShuttle.position.y;
            yield return Press(Key.E);Require(j.Travel==SkywellTravel.Returning,"Both paddle and Sunny allow E to start the water route.");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"Boat boarding and E departure retain shoulder gameplay mode.");
            float boatDeadline=Time.time+18;
            while(j.Travel==SkywellTravel.Returning)
            {
                if(Time.time>boatDeadline)throw new Exception("Boat did not reach its dock; "+court.Prompt());
                Require(Mathf.Abs(j.ReturnShuttle.position.y-boatY)<.001f,"Boat remains at its original water-surface height.");
                yield return Wait(.5f);
            }
            Require(j.Travel==SkywellTravel.Docked&&j.BoatDocked&&!j.HasReturnedToTower&&court.Player.enabled,"Boat stops beside the tower; it does not carry the player directly onto the central platform.");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"The real water-surface boat route docks without switching away from shoulder view.");
            Capture(Path.Combine(Folder,"borrowing-sun-boat-docked.png"));
            // For the market replay, walk inside the ladder's grab rectangle before holding W.
            // Otherwise the diagonal fixed view can walk sideways out of the rectangle while approaching.
            float ladderApproach=module==FlowerMarketModule?.7f:1.4f;
            yield return Walk(j.LadderBottom.position-j.LadderFacingDirection*ladderApproach,"Walk from the boat to the front ladder approach");
            yield return BeginBottomLadderWithW();
            if(shoulder)Require(court.CameraRig.IsShoulderView,"The actual ladder interaction starts in shoulder gameplay mode.");
            input=new KeyboardState(Key.W);
            yield return WaitUntil(()=>j.Travel==SkywellTravel.Walking,12,"Held W climbs the physical ladder and steps onto the central platform");
            input=new KeyboardState();yield return Wait(.2f);
            Require(j.HasReturnedToTower&&j.PaddleObtained&&j.SunnyTokenObtained&&court.Player.enabled,"Climbing completes the return while both acquired items remain owned.");
            if(shoulder)Require(court.CameraRig.IsShoulderView,"Held-W ladder climbing and the top exit retain shoulder gameplay mode.");
            Capture(Path.Combine(Folder,"borrowing-sun-ladder-arrived.png"));
        }
        private static IEnumerator OpenDockSequence()
        {
            court.ResetCourt();yield return Wait(.3f);
            report.Add("EXPLICIT EDGE FIXTURES: player placed at each seam; actual movement and collision remain enabled. Not a continuous route.");
            Require(court.Player.PuzzleEdgeProtectionEnabled,"Floor-support edge protection is active.");
            court.Player.Teleport(court.Origin+new Vector3(-10,.03f,0));yield return Wait(.3f);
            yield return Walk(court.Origin+new Vector3(-4.5f,0,0),"Connected low western seam");
            Require(!court.Complete&&!court.Voyage.TryPrimary(),"Open geometry does not bypass cargo prerequisites.");
            for(int port=1;port<=2;port++)
            {
                court.Player.Teleport(court.Origin+(port==1?new Vector3(0,6.03f,10):new Vector3(10,6.03f,0)));yield return Wait(.3f);
                for(float end=Time.time+2.5f;Time.time<end;){Direction(port==1?Vector3.back:Vector3.left);yield return null;}
                input=new KeyboardState();yield return Wait(.3f);var p=court.Player.FeetPosition-court.Origin;
                Require(Mathf.Abs(p.y-6)<.15f&&(port==1?p.z:p.x)>6.8f,"Disconnected high dock stops the player at the actual floor lip: "+port);
            }
        }
        private static IEnumerator WeatherInputSequence(){yield return TargetingSequence();}
        private static IEnumerator FishTuningSequence()
        {
            court.ResetCourt();yield return Wait(.3f);var f=court.GetComponent<SkywellFish>();var j=court.Journey;
            report.Add("EXPLICIT FISH FIXTURE: player placed on the open dock and selected surface positions; parameters temporarily tuned and restored. No progress injection.");
            court.Player.Teleport(j.RefugePoint+Vector3.up*.03f);yield return Wait(.3f);
            float lure=f.lureDuration,investigate=f.investigateSpeed,range=f.detectionRange,warn=f.warningDuration,chase=f.chaseSpeed,catchRange=f.catchDistance,lost=f.lostSightDuration;
            try
            {
                court.Player.Teleport(j.RefugePoint);f.lureDuration=1.2f;f.Lure();yield return Wait(.3f);
                Require(f.Phase==SkywellFishPhase.Investigate&&f.LureRemaining>0&&f.LureRemaining<1,"Exposed lure duration drives the production countdown.");
                f.lureDuration=.2f;yield return Wait(.15f);Require(f.Phase==SkywellFishPhase.Patrol,"A shortened attraction expires naturally.");
                f.lureDuration=2;f.investigateSpeed=1.7f;f.Lure();yield return Wait(.4f);float remaining=f.LureRemaining;f.Lure();yield return Wait(.1f);
                Require(f.LureRemaining>remaining&&Mathf.Abs(f.CurrentSpeed-1.7f)<.01f,"Repeat bell refreshes the timer without teleporting the fish.");
                yield return Wait(2.1f);
                court.Player.Teleport(new Vector3(-11,j.SurfaceFeetHeight,88));f.Body.position=new Vector3(-11,f.PatrolHeight,84);f.Body.forward=Vector3.forward;Physics.SyncTransforms();
                f.detectionRange=1;f.warningDuration=.2f;f.chaseSpeed=1.1f;f.catchDistance=.25f;
                yield return Wait(.15f);Require(!f.PlayerVisible,"Short detection radius excludes the distant swimmer.");
                f.detectionRange=8;yield return Wait(.6f);
                Require(!j.SunnyTokenObtained&&f.PlayerVisible&&f.Phase==SkywellFishPhase.Chase,"Fish threatens the exposed player before Sunny is acquired, without a key prerequisite.");
                f.detectionRange=.1f;f.lostSightDuration=.2f;yield return Wait(.4f);Require(f.Phase==SkywellFishPhase.Patrol,"Lost sight returns fish to patrol.");
            }
            finally{f.lureDuration=lure;f.investigateSpeed=investigate;f.detectionRange=range;f.warningDuration=warn;f.chaseSpeed=chase;f.catchDistance=catchRange;f.lostSightDuration=lost;}
        }
        private static void CheckReset()
        {
            var j=court.Journey;var receiver=court.Optics.SettlementReceiver;
            Require(!j.SunnyTokenObtained&&!j.PaddleObtained&&!j.BoatDocked&&!j.HasReturnedToTower&&j.SunnyToken.gameObject.activeSelf&&j.PaddlePickup.gameObject.activeSelf&&!court.CanUseToken(SkywellToken.Sun),"Replay restores the surface paddle, boat Sunny pickup, departure state and ladder progression.");
            Require(!j.BelowGroundActive&&!court.Player.IsSwimming&&!court.Player.IsSurfaceSwimming,"Replay restores ground movement without surface buoyancy.");
            Require(court.Consoles.All(s=>s.Token==SkywellToken.None&&s.Zone.Weather==SkywellToken.None),"Replay clears all physical sockets and local weather zones.");
            Require(court.WaterLevel<.01f&&court.BallastAmount<.01f&&Mathf.Abs(court.Height-court.PlatformLowY)<.01f&&Mathf.Abs(court.Yaw+90)<.01f,"Replay restores water, counterweight state, lift and rotation.");
            Require(court.Optics.Collector.FocusIndex==0&&!court.Optics.Collector.Active&&!court.BorrowedSunAtVillage,"Replay resets collector focus and actual beam state.");
            Require(!receiver.RewardAvailable&&!receiver.RewardToastVisible&&!receiver.fruitCrate.activeSelf&&receiver.VillageNetwork&&!receiver.VillageNetwork.Complete,"Replay clears village illumination, fruit and toast.");
            Require(!court.Voyage.Delivered&&court.Voyage.Phase==SkywellVoyagePhase.Waiting&&court.Voyage.Carrier.IsChildOf(court.Rotor),"Replay restores original carrier hierarchy and delivery state.");
            Require(court.WindBell.BellPosition==0&&court.GetComponent<SkywellFish>().Phase==SkywellFishPhase.Patrol,"Replay restores bell position and fish patrol.");
        }
        private static IEnumerator PickWeather(SkywellToken token)
        {
            TraceWeatherInput("before Tab hold for "+token);
            input=new KeyboardState(Key.Tab);yield return Wait(.45f);TraceWeatherInput("after unchanged .45-second Tab hold");Require(court.WeatherPickerOpen,"Holding Tab opens weather picker.");
            input=new KeyboardState(Key.Tab,token==SkywellToken.Rain?Key.Digit1:token==SkywellToken.Wind?Key.Digit2:Key.Digit3);yield return Wait(.15f);
            input=new KeyboardState();yield return Wait(.2f);Require(!court.WeatherPickerOpen&&court.SelectedWeather==token,"Release selects "+token+" without inserting it remotely.");
        }
        internal static void TraceCheckFocus(bool focused)
        {
            if(sequence==null||report==null)return;
            report.Add("ACTUAL FOCUS CALLBACK focused="+focused+" frame="+Time.frameCount+" t="+Time.time.ToString("F3"));SaveProgress();
            if(!focused)Finish(new OperationCanceledException("ENVIRONMENT INTERRUPTED: Unity lost actual application focus during automated input; no collision or gameplay failure is inferred."));
        }
        private static void TraceWeatherInput(string label)
        {
            if(!court||report==null)return;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            object Field(string name)=>typeof(SkywellController).GetField(name,flags)?.GetValue(court);
            var current=Keyboard.current;
            report.Add("WEATHER INPUT "+label+" frame="+Time.frameCount+" t="+Time.time.ToString("F3")+" unscaled="+Time.unscaledTime.ToString("F3")
                +" focused="+Application.isFocused+" cursor="+Cursor.lockState+" keyboard="+(current==null?-1:current.deviceId)+" expected="+(keyboard==null?-1:keyboard.deviceId)
                +" tabHeld="+(current!=null&&current.tabKey.isPressed)+" tabPressed="+(current!=null&&current.tabKey.wasPressedThisFrame)
                +" pickerHeld="+court.WeatherSelectionHeld+" pickerOpen="+court.WeatherPickerOpen+" targeting="+court.WeatherTargeting
                +" weatherBlocked="+court.WeatherInputBlocked+" observationBlocked="+court.ObservationInputBlocked+" journey="+court.Journey.Travel+" voyage="+(court.Voyage?court.Voyage.Phase.ToString():"none")
                +" primaryPending="+Field("primaryPending")+" pointerPicker="+Field("pointerPicker")+" closingFrame="+Field("weatherClosedFrame")+" pickerStarted="+Field("pickerStarted"));
            SaveProgress();
        }
        private static IEnumerator TurnTowerTo(float destination)
        {
            for(int attempt=0;Mathf.Abs(court.Yaw-destination)>.1f&&attempt<3;attempt++)
            {yield return SetWeather(SkywellToken.Wind,destination>court.Yaw?1:0);yield return CarryUntil(()=>!court.IsMoving,6,"Tower reaches the selected Wind stop");}
            Require(Mathf.Abs(court.Yaw-destination)<.1f,"Tower reaches heading "+destination);
        }
        private static IEnumerator ToCollectorStation()
        {
            yield return Walk(Local(-2.4f,1f),"Tower weather aisle");yield return Walk(Local(-2.4f,3.9f),"Pass cart-side clearance");yield return Walk(Local(0,4.8f),"North doorway");
            yield return Walk(court.Origin+new Vector3(0,6,10),"North docking seam");yield return Walk(court.Origin+new Vector3(0,6,20),"North bridge to collector spur");
            yield return Walk(court.Origin+new Vector3(-3,6,20),"Collector spur entrance");yield return Walk(court.Origin+new Vector3(-5,6,19.7f),"Collector local weather station");
        }
        private static IEnumerator ToTowerConsole()
        {
            yield return Walk(court.Origin+new Vector3(0,6,20),"Return from collector spur to bridge");yield return Walk(court.Origin+new Vector3(0,6,10),"Return along north freight bridge");
            yield return Walk(Local(0,4.8f),"Return through tower doorway");yield return Walk(Local(-2.8f,3.9f),"Return along cart aisle");yield return Walk(Local(-2.4f,-.8f),"Return to tower station");
        }
        private static void CheckLiftBase(string phase)
        {
            var support=court.GetComponentInChildren<SkywellLiftSupport>();if(!support)return;
            support.Refresh();Require(Vector3.Distance(support.transform.position,court.Origin)<.001f,"Fixed lift house stays on its foundation: "+phase);
            var piston=support.Piston.GetComponent<Renderer>().bounds;
            Require(Mathf.Abs(piston.max.y-(court.Rotor.position.y-.42f))<.01f,"Lift piston meets the deck underside: "+phase);
        }
        private static void CheckCounterweights(SkywellRainFeedback feedback,string phase)
        {
            Require(Mathf.Abs(court.Height-Mathf.Lerp(court.PlatformLowY,court.PlatformHighY,court.StoredWaterNormalized))<.08f||court.LiftLatched,"Deck follows stored water or its mechanical latch: "+phase);
            for(int i=0;i<feedback.CounterweightTanks.Length;i++)
            {
                var tank=feedback.CounterweightTanks[i];
                Require(!tank.IsChildOf(court.Rotor)&&Mathf.Abs(tank.localPosition.y-Mathf.Lerp(feedback.CounterweightTopY,feedback.CounterweightBottomY,court.LiftTravelNormalized))<.02f,"Counterweight descends opposite actual lift travel: "+i+" / "+phase);
                Require(Mathf.Abs(feedback.WaterLevels[i].localScale.y-Mathf.Max(.025f,court.StoredWaterNormalized*feedback.TankWaterDepth))<.02f,"Tank water display follows stored water: "+i+" / "+phase);
                Require(!feedback.Cables[i].transform.IsChildOf(court.Rotor)&&Mathf.Abs(feedback.Cables[i].GetPosition(0).y-(court.Origin.y+court.Height-.85f))<.02f,"Cable remains on the nonrotating outer lift: "+i+" / "+phase);
            }
        }
        private static void Capture(string path){if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot(path);}
        private static Vector3 Local(float x,float z)=>court.Rotor.TransformPoint(new Vector3(x,0,z));
        private static IEnumerator CarryUntil(Func<bool> condition,float timeout,string label)
        {
            float deadline=Time.time+timeout;
            while(!condition()){if(Time.time>deadline)throw new Exception("Timed out: "+label);yield return Carry(.2f);}
            Require(true,label);
        }
        private static IEnumerator Carry(float seconds)
        {
            Vector3 p=court.Rotor.InverseTransformPoint(court.Player.FeetPosition),last=court.Player.FeetPosition;float drift=0,step=0,end=Time.time+seconds;double timeout=EditorApplication.timeSinceStartup+seconds+15;
            while(Time.time<end)
            {
                drift=Mathf.Max(drift,Vector3.Distance(p,court.Rotor.InverseTransformPoint(court.Player.FeetPosition)));step=Mathf.Max(step,Vector3.Distance(last,court.Player.FeetPosition));last=court.Player.FeetPosition;
                if(drift>.13f||step>.8f)throw new Exception($"Carry discontinuity: drift={drift:F3}, step={step:F3}, height={court.Height:F2}, yaw={court.Yaw:F2}");
                if(EditorApplication.timeSinceStartup>timeout)throw new Exception("Game stopped advancing during carry.");yield return null;
            }
        }
        private static IEnumerator Walk(Vector3 target,string label,bool sloped=false)
        {
            navigationError=Vector3.zero;
            bool shoulderPrepared=false;float low=Mathf.Min(court.Player.FeetPosition.y,target.y)-.2f,high=Mathf.Max(court.Player.FeetPosition.y,target.y)+.4f;
            report.Add("START WALK: "+label);SaveProgress();double timeout=EditorApplication.timeSinceStartup+20;float traceAt=Time.time;int recoveries=court.Journey?court.Journey.Recoveries:0;
            while(Vector3.ProjectOnPlane(court.Player.FeetPosition-target,Vector3.up).magnitude>.3f)
            {
                if(court.Journey&&court.Journey.Recoveries!=recoveries)throw new Exception("Unexpected recovery during "+label+"; "+court.Prompt());
                if(sloped?(court.Player.FeetPosition.y<low||court.Player.FeetPosition.y>high||court.Player.IsSwimming):Mathf.Abs(court.Player.FeetPosition.y-target.y)>.25f)throw new Exception("Wrong height during "+label+": "+court.Player.FeetPosition);
                if(EditorApplication.timeSinceStartup>timeout){DiagnoseFloor(target);throw new Exception("Walk blocked: "+label+"; position="+court.Player.FeetPosition+"; target="+target);}
                if(Time.time>=traceAt){traceAt=Time.time+1;report.Add("NAV "+label+" t="+Time.time.ToString("0.0")+" feet="+court.Player.FeetPosition+" velocity="+court.Player.PlanarVelocity+" "+NavigationInputState());SaveProgress();}
                Vector3 wanted=Vector3.ProjectOnPlane(target-court.Player.FeetPosition,Vector3.up).normalized;
                if(court.CameraRig.IsShoulderView&&(!shoulderPrepared||Mathf.Abs(NavigationYawError(wanted))>.75f))
                {
                    yield return FaceNavigationDirection(wanted);shoulderPrepared=true;continue;
                }
                Direction(wanted);yield return null;
            }
            input=new KeyboardState();yield return Wait(.15f);Require(Mathf.Abs(court.Player.FeetPosition.y-target.y)<.25f,"WALK "+label+" reaches its authored height through collision");
        }
        private static void DiagnoseFloor(Vector3 target)
        {
            report.Add("DIAGNOSTIC target="+target+" actorEnabled="+court.Player.enabled+" observing="+court.IsObserving+" swim="+court.Player.IsSwimming+" "+NavigationInputState()+" prompt="+court.Prompt());
            foreach(var hit in Physics.RaycastAll(court.Player.FeetPosition+Vector3.up*.4f,Vector3.down,1))report.Add("FLOOR "+hit.transform.name+" at "+hit.point);
            foreach(var col in Physics.OverlapCapsule(court.Player.FeetPosition+Vector3.up*.36f,court.Player.FeetPosition+Vector3.up*1.45f,.34f))report.Add("OVERLAP "+col.name+" "+col.bounds);SaveProgress();
        }
        private static void Direction(Vector3 wanted)
        {
            var cam=court.CameraRig.ViewCamera.transform;var forward=Vector3.ProjectOnPlane(cam.forward,Vector3.up).normalized;var right=Vector3.ProjectOnPlane(cam.right,Vector3.up).normalized;
            if(court.CameraRig.IsShoulderView)
            {
                ValidateShoulderNavigation();
                if(Mathf.Abs(NavigationYawError(wanted))>1f)throw new Exception("Shoulder navigation must turn through real mouse input before W; "+NavigationInputState());
                // Free shoulder navigation turns the camera through real mouse input, then demonstrates forward W travel.
                pointerInput=new MouseState{position=pointerInput.position};input=new KeyboardState(Key.W);return;
            }
            // Alternate real eight-way key combinations to repay their angular error.
            // A fixed nearest key otherwise drifts sideways along an oblique narrow bridge.
            wanted=Vector3.ProjectOnPlane(wanted,Vector3.up).normalized;
            Vector3 corrected=wanted+navigationError,chosen=Vector3.zero;float best=float.NegativeInfinity;int bx=0,by=0;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++){if(x==0&&y==0)continue;Vector3 candidate=(right*x+forward*y).normalized;float dot=Vector3.Dot(candidate,corrected);if(dot>best){best=dot;bx=x;by=y;chosen=candidate;}}
            navigationError=Vector3.ClampMagnitude(navigationError+wanted-chosen,1f);
            var keys=new List<Key>();if(bx!=0)keys.Add(bx>0?Key.D:Key.A);if(by!=0)keys.Add(by>0?Key.W:Key.S);input=new KeyboardState(keys.ToArray());
        }
        private static float NavigationYawError(Vector3 wanted)
        {
            float desired=Mathf.Atan2(wanted.x,wanted.z)*Mathf.Rad2Deg;
            return Mathf.DeltaAngle(court.CameraRig.ViewCamera.transform.eulerAngles.y,desired);
        }
        private static void ValidateShoulderNavigation()
        {
            if(!Application.isFocused)throw new OperationCanceledException("ENVIRONMENT INTERRUPTED: shoulder mouse navigation requires actual Unity focus; "+NavigationInputState());
            if(!court.CameraRig.IsShoulderView||court.Player.ViewFacingActive)
                throw new Exception("Free shoulder orbit must not own the player's facing before navigation; "+NavigationInputState());
        }
        private static IEnumerator FaceNavigationDirection(Vector3 wanted)
        {
            if(!court.CameraRig.IsShoulderView)yield break;
            input=new KeyboardState();pointerInput=new MouseState{position=pointerInput.position};
            ValidateShoulderNavigation();
            if(Cursor.lockState!=CursorLockMode.Locked){yield return CameraMousePulse(Vector2.zero,1);yield return Wait(.2f);}
            if(Cursor.lockState!=CursorLockMode.Locked)throw new Exception("Real world click did not resume shoulder pointer lock; "+NavigationInputState());
            float sensitivity;
            using(var serialized=new SerializedObject(court.CameraRig))sensitivity=serialized.FindProperty("shoulderSensitivity").floatValue;
            double deadline=EditorApplication.timeSinceStartup+3;
            float startError=NavigationYawError(wanted);
            while(Mathf.Abs(NavigationYawError(wanted))>.5f)
            {
                ValidateShoulderNavigation();
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Real mouse input did not align shoulder navigation; "+NavigationInputState());
                // Deliberate visible turns, then W. No Transform/facing assignment or camera-state injection.
                float turn=Mathf.Clamp(NavigationYawError(wanted),-45,45);
                yield return CameraMousePulse(new Vector2(turn/Mathf.Max(.001f,sensitivity),0));
            }
            ValidateShoulderNavigation();
            report.Add("NAV TURN actual mouse yaw="+startError.ToString("F2")+"; aligned before W; "+NavigationInputState());SaveProgress();
        }
        private static IEnumerator MoveInDirection(Vector3 wanted,float seconds)
        {
            yield return FaceNavigationDirection(wanted);
            Direction(wanted);yield return Wait(seconds);input=new KeyboardState();yield return Wait(.15f);
        }
        private static string NavigationInputState()
        {
            var k=Keyboard.current;var rig=court.CameraRig;var actor=court.Player;
            return "viewForward="+rig.ViewCamera.transform.forward+" facing="+actor.FacingDirection+" viewFacing="+actor.ViewFacingActive+" carriedYaw="+actor.CarriedYaw.ToString("F2")
                +" shoulder="+rig.IsShoulderView+" cameraSuppressed="+rig.InputSuppressed+" autoPositioning="+actor.IsAutoPositioning+" pushing="+actor.IsPushingCart
                +" focused="+Application.isFocused+" cursor="+Cursor.lockState+" keyboard="+(k==null?-1:k.deviceId)
                +" W="+(k!=null&&k.wKey.isPressed)+" A="+(k!=null&&k.aKey.isPressed)+" S="+(k!=null&&k.sKey.isPressed)+" D="+(k!=null&&k.dKey.isPressed);
        }
        private static IEnumerator Press(Key key){input=new KeyboardState(key);yield return Wait(.15f);input=new KeyboardState();yield return Wait(.15f);}
        private static IEnumerator Wait(float seconds)
        {float end=Time.time+seconds;double timeout=EditorApplication.timeSinceStartup+seconds+15;while(Time.time<end){if(EditorApplication.timeSinceStartup>timeout)throw new Exception("Play Mode is not advancing.");yield return null;}}
        private static IEnumerator WaitUntil(Func<bool> condition,float seconds,string label)
        {float end=Time.time+seconds;double timeout=EditorApplication.timeSinceStartup+seconds+15;while(!condition()){if(Time.time>end||EditorApplication.timeSinceStartup>timeout)throw new Exception("Timed out: "+label);yield return null;}Require(true,label);}
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying||EditorApplication.isPaused)throw new Exception("Play Mode stopped or paused.");
                if(stack.Count==0)stack.Push(sequence);var top=stack.Peek();
                if(!top.MoveNext()){stack.Pop();(top as IDisposable)?.Dispose();if(stack.Count==0)Finish(null);}
                else if(top.Current is IEnumerator next)stack.Push(next);
            }
            catch(Exception e){Finish(e);}
        }
        private static void Require(bool condition,string label){if(!condition)throw new Exception(label);report.Add("PASS: "+label);SaveProgress();}
        private static void SaveProgress()=>File.WriteAllLines(Path.Combine(Folder,"running.txt"),report);
        private static void Abort()=>Finish(new Exception("Assembly reload interrupted checks."));
        private static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode&&sequence!=null)Finish(new Exception("Play Mode exited."));}
        private static void Finish(Exception error)
        {
            bool interrupted=error is OperationCanceledException;
            string lastNavigation=court?NavigationInputState():string.Empty;
            sequence=null;
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            if(driver){UnityEngine.Object.Destroy(driver.gameObject);driver=null;}
            AssemblyReloadEvents.beforeAssemblyReload-=Abort;EditorApplication.playModeStateChanged-=OnPlay;sequence=null;
            if(keyboard!=null&&keyboard.added){InputState.Change(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);}if(previous!=null&&previous.added)previous.MakeCurrent();keyboard=previous=null;
            if(pointer!=null&&pointer.added){InputState.Change(pointer,new MouseState());InputSystem.RemoveDevice(pointer);}if(previousPointer!=null&&previousPointer.added)previousPointer.MakeCurrent();pointer=previousPointer=null;
            Application.runInBackground=previousRunInBackground;InputSystem.settings.backgroundBehavior=previousBackgroundBehavior;
            if(error!=null&&court){report.Add((interrupted?"INTERRUPTED STATE: ":"FAIL STATE: ")+"feet="+court.Player.FeetPosition+" surface="+court.Player.IsSurfaceSwimming+" recoveries="+court.Journey.Recoveries+" fish="+court.GetComponent<SkywellFish>().Phase+" "+lastNavigation+" prompt="+court.Prompt());Capture(Path.Combine(Folder,interrupted?"interrupted.png":"failure.png"));}
            if(court&&error==null)court.ResetCourt();
            string result=error==null?module+"_PASSED":module+(interrupted?"_INTERRUPTED: ":"_FAILED: ")+error.Message;
            report.Add(result);File.WriteAllLines(Path.Combine(Folder,"playmode.txt"),report);
            if(error==null)Debug.Log(result);else if(interrupted)Debug.LogWarning(result);else Debug.LogError(result);
        }
    }
}
