using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    public static partial class SkywellChecks
    {
        private const string FlowerMarketModule = "SKYWELL_FLOWER_MARKET";
        private const string FlowerMarketReplaySession = "BorrowingSun.Skywell.FlowerMarketReplay";
        private static string FlowerMarketFolder => Path.Combine(ValidationFolder, "flower-market");
        private static bool flowerMarketBatchStarted;

        [MenuItem("Borrowing Sun/Skywell/Check complete Flower Market route")]
        public static void RunFlowerMarket() =>
            StartCheck(FlowerMarketModule, FlowerMarketSequence());

        // Launch a focused graphical Editor, without -quit, to exercise the live input and camera.
        public static void RunFlowerMarketFromCommandLine()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start command-line Flower Market verification from Edit Mode.");

            string[] arguments = Environment.GetCommandLineArgs();
            int sceneOption = Array.IndexOf(arguments, "-flowerMarketScene");
            string scenePath = sceneOption >= 0 && sceneOption + 1 < arguments.Length
                ? arguments[sceneOption + 1]
                : null;
            if (string.IsNullOrEmpty(scenePath) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
                throw new ArgumentException("Pass -flowerMarketScene with the Assets-relative Flower Market scene path.");

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SessionState.SetBool(FlowerMarketReplaySession, true);
            RestoreFlowerMarketReplayCallback();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreFlowerMarketReplayCallback()
        {
            if (!SessionState.GetBool(FlowerMarketReplaySession, false)) return;
            EditorApplication.update -= StartFlowerMarketReplayInPlayMode;
            EditorApplication.update += StartFlowerMarketReplayInPlayMode;
        }

        private static void StartFlowerMarketReplayInPlayMode()
        {
            if (!SessionState.GetBool(FlowerMarketReplaySession, false) ||
                !EditorApplication.isPlaying || EditorApplication.isPaused || flowerMarketBatchStarted) return;
            EditorApplication.update -= StartFlowerMarketReplayInPlayMode;
            flowerMarketBatchStarted = true;
            try
            {
                RunFlowerMarket();
                EditorApplication.update += ExitFlowerMarketReplayAfterReport;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                ExitFlowerMarketReplay(1);
            }
        }

        private static void ExitFlowerMarketReplayAfterReport()
        {
            if (IsRunning) return;

            string path = Path.Combine(FlowerMarketFolder, "playmode.txt");
            string lastLine = File.Exists(path) ? File.ReadLines(path).LastOrDefault() : null;
            bool passed = lastLine == FlowerMarketModule + "_PASSED";
            if (!passed) Debug.LogError("Flower Market Play Mode did not produce a passing report at " + path);
            ExitFlowerMarketReplay(passed ? 0 : 1);
        }

        private static void ExitFlowerMarketReplay(int exitCode)
        {
            SessionState.EraseBool(FlowerMarketReplaySession);
            EditorApplication.update -= ExitFlowerMarketReplayAfterReport;
            EditorApplication.update -= StartFlowerMarketReplayInPlayMode;
            EditorApplication.Exit(exitCode);
        }

        private static IEnumerator FlowerMarketSequence()
        {
            var market = court.GetComponentInChildren<SkywellFlowerMarket>(true);
            Require(market && market.Shop && market.Mushrooms && market.RainFruit && market.RainZone && market.RainTarget,
                "The tower shop and both fixed mountain gathering stops are authored.");
            Require(market.Shop.IsChildOf(court.Rotor) && !market.Mushrooms.IsChildOf(court.Rotor) &&
                !market.RainFruit.IsChildOf(court.Rotor),
                "The shop rides the moving rotor while the north mushroom patch and east orchard stay on their own shores.");
            int stationIndex = market.RainTarget.StationIndex;
            Require(stationIndex == 3 && court.Consoles.Length == 5 &&
                court.Consoles[stationIndex] && court.Consoles[stationIndex].Zone == market.RainZone &&
                WeatherTargetForStation(stationIndex) == market.RainTarget,
                "The east orchard reuses the original village rain station and its actual target.");
            Require(!court.Voyage && !court.GetComponentsInChildren<SkywellVoyage>(true).Any(voyage => voyage.isActiveAndEnabled),
                "The market variant has no live finale cargo voyage.");
            yield return Wait(.2f);
            Require(!market.Run.IsStarted && !market.IsShopOpen, "The market is locked at the original west entrance.");
            Vector3 spawn = court.Player.FeetPosition;
            Vector3 mushrooms = market.Mushrooms.position;
            Vector3 rainFruit = market.RainFruit.position;
            Vector3 shopLocal = court.Rotor.InverseTransformPoint(market.Shop.position);
            report.Add($"TOWER MARKET ANCHORS shopLocal={shopLocal} northMushrooms={mushrooms} eastOrchard={rainFruit}");
            SaveProgress();

            report.Add("CONTINUOUS TOWER MARKET ROUTE: real entrance, surface boat and ladder, rain lift, wind docking, both mountain harvests and trading. No fixture placement or cargo voyage.");
            yield return SurfaceWaterSequence();
            Require(court.Journey.HasReturnedToTower && court.Journey.SunnyTokenObtained &&
                court.OnRotor(court.Player.FeetPosition) && !market.Run.IsStarted,
                "The existing water route and physical ladder return to the tower without automatically starting the market.");
            yield return Walk(Local(-2.8f, -3), "Ladder landing to the tower's rear aisle");
            yield return Walk(Local(2.4f, -3), "Rear aisle around the relay to the shop side");
            yield return Walk(market.Shop.position, "Reach the actual tower shop");
            yield return Press(Key.E);
            Require(market.Run.Day == 1 && market.Run.Phase == FlowerMarketPhase.DayGathering &&
                market.Run.CanGather && !market.IsShopOpen && market.Run.Cash == 0,
                "A separate short E at the central shop opens day one without an end-of-voyage signature.");
            float dayOpened = Time.time;
            Capture(Path.Combine(Folder, "tower-market-shop-low.png"));

            yield return Walk(Local(2.4f, -3), "Shop to the tower's rear aisle");
            yield return Walk(Local(-2.8f, -3), "Cross the rear aisle to the weather side");
            yield return Walk(Local(-2.4f, -.8f), "Approach the original tower weather station");
            yield return SetWeather(SkywellToken.Rain, 0);
            yield return CarryUntil(() => court.Height >= court.PlatformHighY - .01f, 15,
                "Real tower rain fills the counterweights and carries the player to the high deck.");
            CheckLiftBase("tower market high");
            var rain = court.GetComponentInChildren<SkywellRainFeedback>();
            if (rain) CheckCounterweights(rain, "tower market rain store");
            float storedWater = court.WaterLevel;
            Require(court.OnRotor(court.Player.FeetPosition) && court.LiftLatched &&
                Mathf.Abs(storedWater - court.MaxStoredWater) < .02f && court.BallastAmount < .01f &&
                Mathf.Abs(market.Shop.position.y - (court.Origin.y + court.Height)) < .05f,
                "The original rainwater raises and latches the occupied deck, and the shop rises with the same deck.");
            Capture(Path.Combine(Folder, "tower-market-shop-high.png"));

            yield return TurnTowerTo(0);
            Require(court.DockedPort == 1 && Mathf.Abs(court.WaterLevel - storedWater) < .02f,
                "Wind docks north without spending the stored rainwater.");
            float northDeparture = Time.time;
            yield return ToCollectorStation();
            yield return Walk(court.Origin + new Vector3(-3, 6, 20), "North court clear aisle toward the mushroom patch");
            yield return Walk(mushrooms, "Walk to the north mountain mushroom patch");
            float northArrival = Time.time;
            Require(!court.OnRotor(court.Player.FeetPosition) && court.Consoles[0].Token == SkywellToken.None,
                "The player crosses the real northern bridge onto fixed ground and the tower's Wind token returns.");
            yield return GatherFlowerMushrooms(market, 3);
            Require(market.Run.GetStock(FlowerIngredient.Mushroom) == 3,
                "Three mushrooms enter the market bag from the actual north mountain patch.");
            Capture(Path.Combine(Folder, "tower-market-north-harvest.png"));
            float northReturn = Time.time;
            yield return Walk(court.Origin + new Vector3(-3, 6, 20), "Return via the north court aisle");
            yield return ToTowerConsole();
            RecordFlowerTrip("north mushroom mountain", northDeparture, northArrival, northReturn);
            Require(court.OnRotor(court.Player.FeetPosition) && court.DockedPort == 1,
                "The north return walks back onto the same docked tower.");

            yield return TurnTowerTo(90);
            Require(court.DockedPort == 2 && court.LiftLatched && Mathf.Abs(court.WaterLevel - storedWater) < .02f,
                "A second actual Wind cast docks east and retains tower height and water.");
            float eastDeparture = Time.time;
            yield return ToEasternObservation();
            yield return Walk(court.Origin + new Vector3(48, 6, 0), "East landing to the orchard aisle");
            yield return Walk(rainFruit, "Walk from the east landing to the orchard on the fixed mountain");
            float eastArrival = Time.time;
            Require(!court.OnRotor(court.Player.FeetPosition) && !market.FruitIsWet &&
                market.RainZone.Contains(court.Player.FeetPosition) && market.RainZone.Contains(rainFruit),
                "The actual east crossing reaches a dry orchard and both the player and fruit are inside the village rain zone.");
            RecordFlowerCrop("east dry arrival", market);
            yield return CastFlowerMarketRain(market);
            Require(market.FruitIsWet && market.OrchardProduce == FlowerIngredient.WeatherFruit,
                "A real pointer cast of Rain at the village station changes this east orchard to rain fruit.");
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.WeatherFruit) == 1 &&
                market.Run.GetStock(FlowerIngredient.SunnyFruit) == 0 && market.Run.GetStock(FlowerIngredient.Mushroom) == 3,
                "Short E adds the east rain fruit while retaining the mushrooms from the other mountain.");
            RecordFlowerCrop("east rain harvest", market);
            Capture(Path.Combine(Folder, "tower-market-east-rain.png"));
            float eastReturn = Time.time;
            yield return FromEasternVillageToTower();
            RecordFlowerTrip("east orchard mountain", eastDeparture, eastArrival, eastReturn);
            yield return WaitForFlowerMarketRainReturn(market);
            Require(court.OnRotor(court.Player.FeetPosition) && court.DockedPort == 2 && court.BallastAmount < .01f &&
                Mathf.Abs(court.WaterLevel - storedWater) < .02f,
                "The east return walks onto the tower with its original rainwater still in the lift and none in cargo ballast.");

            yield return Walk(Local(-2.8f, -3), "Weather side to the rear aisle");
            yield return Walk(Local(2.4f, -3), "Cross behind the relay to the rotating shop side");
            yield return Walk(market.Shop.position, "Return to the central shop at the live east-facing pose");
            Require(court.OnRotor(court.Player.FeetPosition) && market.Run.Phase == FlowerMarketPhase.DayGathering &&
                market.Run.RemainingGatherSeconds > 0 &&
                Vector3.Distance(court.Rotor.InverseTransformPoint(market.Shop.position), shopLocal) < .01f,
                "Both mountain harvests return to the same rotating store before daylight gathering ends.");
            report.Add(FormattableString.Invariant(
                $"TOWER MARKET OUTING returnedIn={Time.time - dayOpened:F2}s daylightRemaining={market.Run.RemainingGatherSeconds:F2}s"));
            SaveProgress();
            yield return EnterFlowerMarketDusk(market);
            Capture(Path.Combine(Folder, "tower-market-dusk.png"));
            yield return Press(Key.Digit1);
            Require(market.Run.Day == 1 && market.Run.Phase == FlowerMarketPhase.Trading && market.IsShopOpen &&
                court.OnRotor(court.Player.FeetPosition),
                "Direct return opens trading on the central tower instead of boarding a carrier.");
            CheckFlowerTradingBudget(market, 120f, "Tower direct return");
            yield return AcceptFlowerOrders(market, FlowerIngredient.WeatherFruit);
            CheckFlowerTradingStarted(market, 120f, "Tower direct return");
            yield return StartFlowerStove(market, 0, FlowerRecipe.MushroomBento);
            yield return StartFlowerStove(market, 1, FlowerRecipe.FruitBento);
            Require(market.Run.GetStove(0).Stage == FlowerOrderStage.Cooking &&
                market.Run.GetStock(FlowerIngredient.Mushroom) == 0 && market.Run.GetStock(FlowerIngredient.WeatherFruit) == 0,
                "Both stoves cook at once on the tower using only the ingredients physically gathered on the two mountains.");
            RecordFlowerKitchen("tower parallel orders", market);
            Capture(Path.Combine(Folder, "tower-market-dual-stove.png"));
            yield return ServeFlowerStove(market, 0, 12);
            yield return ServeFlowerStove(market, 1, 34);
            yield return Press(Key.N);
            Require(market.IsSummaryOpen && market.Run.Day == 1 && !market.Run.ClosedByTimeLimit &&
                market.Run.EveningRevenue == 34 && market.Run.TotalRevenue == 34 && market.Run.Cash == 34 &&
                !market.Run.HasPendingOrders && court.OnRotor(court.Player.FeetPosition),
                "N opens the actual 34-value sales summary on the tower without moving the player or requiring a ferry.");
            RecordFlowerTrading("tower manual summary", market);
            Capture(Path.Combine(Folder, "tower-market-summary.png"));
            yield return Press(Key.N);
            Require(!market.IsSummaryOpen && market.Run.Day == 2 && market.Run.Phase == FlowerMarketPhase.DayGathering &&
                market.Run.EveningRevenue == 0 && market.Run.Cash == 34 && market.Run.TotalRevenue == 34 &&
                court.DockedPort == 2 && court.OnRotor(court.Player.FeetPosition) && court.LiftLatched &&
                Mathf.Abs(court.WaterLevel - storedWater) < .02f && court.BallastAmount < .01f,
                "Day two retains earnings and the physical east-facing tower state without a cargo stage or relocation.");

            yield return Walk(Local(2.4f, -3), "Leave the shop by the rear aisle");
            yield return Walk(Local(-2.8f, -3), "Cross the rear aisle to the wind controls");
            yield return Walk(Local(-2.4f, -.8f), "Reach the tower weather station for the west return");
            yield return TurnTowerTo(-90);
            yield return Walk(Local(-2.4f, -3), "Approach the west-facing drain valve");
            yield return Press(Key.E);
            Require(court.DrainOpen, "Actual E opens the original west-facing drain valve.");
            yield return CarryUntil(() => court.DockedPort == 0, 14, "The occupied tower descends and docks at the low west bridge.");
            Require(court.OnRotor(court.Player.FeetPosition) && court.WaterLevel < .02f &&
                market.Run.Day == 2 && market.Run.Cash == 34,
                "The physical descent retains the shop and the next day's carried earnings.");
            Capture(Path.Combine(Folder, "tower-market-west-low.png"));
            yield return Press(Key.R);
            yield return Wait(.4f);
            Require(court.DockedPort == 0 && Mathf.Abs(court.Height - court.PlatformLowY) < .01f &&
                court.WaterLevel < .01f && court.BallastAmount < .01f &&
                court.Consoles.All(slot => slot.Token == SkywellToken.None),
                "R restores the empty west-facing low deck and returns all weather tokens.");
            Require(!court.Journey.HasReturnedToTower && !court.Journey.SunnyTokenObtained && !court.Journey.PaddleObtained &&
                Vector3.Distance(court.Player.FeetPosition, spawn) < .5f &&
                !market.IsShopOpen && !market.IsDuskOpen && !market.IsSummaryOpen &&
                market.Run.Phase == FlowerMarketPhase.Locked && market.Run.Cash == 0 && market.Run.TotalRevenue == 0 &&
                !market.Run.OrdersAccepted && !market.Run.HasPendingOrders &&
                Enum.GetValues(typeof(FlowerIngredient)).Cast<FlowerIngredient>().All(ingredient => market.Run.GetStock(ingredient) == 0),
                "R returns to the original entrance and resets the lower route, tower shop and ingredient bag.");
        }

        private static IEnumerator EnterFlowerMarketDusk(SkywellFlowerMarket market)
        {
            int day = market.Run.Day;
            yield return Press(Key.E);
            Require(market.Run.Day == day && market.Run.Phase == FlowerMarketPhase.DuskChoice &&
                market.IsDuskOpen && !market.IsShopOpen && !market.Run.CanGather && !market.Run.IsNight,
                $"Short E at the shop opens the separate dusk choice on day {day}.");
            yield return CheckFlowerMarketModal(market, "The dusk choice");
        }

        private static IEnumerator CheckFlowerMarketModal(SkywellFlowerMarket market, string label)
        {
            Vector3 feet = court.Player.FeetPosition;
            input = new KeyboardState(Key.Tab, Key.W);
            yield return Wait(court.weatherHoldTime + .12f);
            Require(market.InputBlocked && !court.WeatherSelectionHeld && !court.WeatherPickerOpen &&
                Vector3.Distance(feet, court.Player.FeetPosition) < .1f,
                $"{label} suppresses both the weather selector and movement while open.");
            input = new KeyboardState();
            yield return Wait(.15f);
        }

        private static IEnumerator CastFlowerMarketRain(SkywellFlowerMarket market)
        {
            int stationIndex = market.RainTarget.StationIndex;
            yield return PickWeather(SkywellToken.Rain);
            bool wasShoulder = court.CameraRig.IsShoulderView;
            yield return BeginTargeting(market.RainTarget);
            yield return ClickWeatherPointer(market.RainTarget, requireHover: true);
            Require(court.Consoles[stationIndex].Token == SkywellToken.Rain,
                "A real pointer hit on the east village mechanism inserts the rain token.");
            yield return WaitUntil(() => market.RainZone.IsRain, 4,
                "The local rain zone becomes active after the weather transition.");
            Require(court.Consoles.Where((_, index) => index != stationIndex)
                .All(slot => slot.Token != SkywellToken.Rain && !slot.Zone.IsRain),
                "Rain remains local to the east village station and no other station receives it.");
            yield return ReleaseTargeting();
            yield return EnsureCameraMode(wasShoulder);
        }

        private static IEnumerator WaitForFlowerMarketRainReturn(SkywellFlowerMarket market)
        {
            yield return WaitUntil(() => !market.RainZone.IsRain &&
                court.Consoles[market.RainTarget.StationIndex].Token == SkywellToken.None, 4,
                "Leaving the patch automatically returns the local rain token.");
        }

        private static void RecordFlowerCrop(string label, SkywellFlowerMarket market)
        {
            report.Add($"FLOWER CROP {label}: day={market.Run.Day} phase={market.Run.Phase} sunlight={market.SunlightAvailable} " +
                $"orchardRay={market.OrchardReceivesSunlight} localRain={market.FruitIsWet} " +
                $"offer={market.OrchardProduce?.ToString() ?? "None"} " +
                $"sunny={market.Run.GetStock(FlowerIngredient.SunnyFruit)} rain={market.Run.GetStock(FlowerIngredient.WeatherFruit)} " +
                $"nightRain={market.Run.GetStock(FlowerIngredient.RainNightFruit)} villageRay={court.BorrowedSunAtVillage}");
            SaveProgress();
        }

        private static void RecordFlowerTrip(string name, float outbound, float atResource, float returning)
        {
            float backAtShop = Time.time;
            report.Add(FormattableString.Invariant(
                $"FLOWER TRIP {name}: out {atResource - outbound:F2}s, action {returning - atResource:F2}s, back {backAtShop - returning:F2}s, total {backAtShop - outbound:F2}s"));
            SaveProgress();
        }

        private static IEnumerator GatherFlowerMushrooms(SkywellFlowerMarket market, int count)
        {
            int initialStock = market.Run.GetStock(FlowerIngredient.Mushroom);
            for (int collected = 1; collected <= count; collected++)
            {
                yield return Press(Key.E);
                Require(market.Run.GetStock(FlowerIngredient.Mushroom) == initialStock + collected,
                    $"Short E collects mushroom {collected} from the authored patch.");
                if (collected < count) yield return Wait(1.25f);
            }
        }

        private static IEnumerator AcceptFlowerOrders(SkywellFlowerMarket market, FlowerIngredient reservedFruit)
        {
            int mushrooms = market.Run.GetStock(FlowerIngredient.Mushroom);
            int fruit = market.Run.GetStock(reservedFruit);
            yield return Press(Key.E);
            Require(market.Run.OrdersAccepted && market.Run.GetStove(0).Stage == FlowerOrderStage.Waiting &&
                market.Run.GetStove(0).Recipe == FlowerRecipe.MushroomBento &&
                market.Run.GetStove(1).Stage == FlowerOrderStage.Waiting &&
                market.Run.GetStove(1).Recipe == FlowerRecipe.FruitBento,
                "E accepts a waiting mushroom order at window A and a waiting fruit order at window B.");
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.Mushroom) == mushrooms &&
                market.Run.GetStock(reservedFruit) == fruit &&
                market.Run.GetReserved(FlowerIngredient.Mushroom) == 3 &&
                market.Run.GetReserved(reservedFruit) == 1 &&
                market.Run.GetReserved(FlowerIngredient.SunnyFruit) == 0,
                $"Accepted orders reserve three mushrooms and exactly one {reservedFruit}, even after repeated E.");
        }

        private static IEnumerator StartFlowerStove(SkywellFlowerMarket market, int slot, FlowerRecipe recipe)
        {
            yield return Press(slot == 0 ? Key.Digit1 : Key.Digit2);
            var stove = market.Run.GetStove(slot);
            Require(stove.Stage == FlowerOrderStage.Cooking && stove.Recipe == recipe && stove.RemainingSeconds > 0,
                $"Actual {slot + 1} starts {recipe} at its own stove.");
        }

        private static IEnumerator ServeFlowerStove(SkywellFlowerMarket market, int slot, int expectedCash)
        {
            yield return WaitUntil(() => market.Run.GetStove(slot).Stage == FlowerOrderStage.Ready, 8,
                $"Stove {slot + 1} becomes ready through its live cooking timer.");
            yield return Press(slot == 0 ? Key.Digit1 : Key.Digit2);
            Require(market.Run.Cash == expectedCash && market.Run.GetStove(slot).Stage != FlowerOrderStage.Ready,
                $"Actual {slot + 1} delivers its dish and raises cash to {expectedCash} exactly once.");
        }

        private static void RecordFlowerKitchen(string label, SkywellFlowerMarket market)
        {
            var first = market.Run.GetStove(0);
            var second = market.Run.GetStove(1);
            report.Add(FormattableString.Invariant(
                $"FLOWER KITCHEN {label}: day={market.Run.Day} upgraded={market.Run.KitchenUpgraded} cash={market.Run.Cash} revenue={market.Run.TotalRevenue} A={first.Stage}/{first.Recipe}/{first.RemainingSeconds:F2}s B={second.Stage}/{second.Recipe}/{second.RemainingSeconds:F2}s"));
            SaveProgress();
        }

        private static void CheckFlowerTradingBudget(SkywellFlowerMarket market, float expected, string route)
        {
            Require(Mathf.Approximately(market.Run.TradingDurationSeconds, expected) &&
                market.Run.RemainingTradingSeconds == 0 && !market.Run.OrdersAccepted,
                $"{route} offers the authored {expected:0}-second budget before E accepts orders.");
            RecordFlowerTrading(route + " before accepting", market);
        }

        private static float CheckFlowerTradingStarted(SkywellFlowerMarket market, float expected, string route)
        {
            float remaining = market.Run.RemainingTradingSeconds;
            Require(Mathf.Approximately(market.Run.TradingDurationSeconds, expected) &&
                remaining > expected - 1.5f && remaining <= expected,
                $"Accepting orders on the {route.ToLowerInvariant()} route starts the live {expected:0}-second clock.");
            RecordFlowerTrading(route + " clock started", market);
            return remaining;
        }

        private static void RecordFlowerTrading(string label, SkywellFlowerMarket market)
        {
            var run = market.Run;
            string clock = FormattableString.Invariant(
                $"budget={run.TradingDurationSeconds:F2}s remaining={run.RemainingTradingSeconds:F2}s");
            report.Add($"FLOWER TRADING {label}: day={run.Day} phase={run.Phase} {clock} timeout={run.ClosedByTimeLimit} " +
                $"evening={run.EveningRevenue} cash={run.Cash} revenue={run.TotalRevenue} upgraded={run.KitchenUpgraded} " +
                $"A={run.GetStove(0).Stage} B={run.GetStove(1).Stage} refundedMushrooms={run.GetRefundedStock(FlowerIngredient.Mushroom)} " +
                $"refundedRainNight={run.GetRefundedStock(FlowerIngredient.RainNightFruit)} " +
                $"refundedRain={run.GetRefundedStock(FlowerIngredient.WeatherFruit)} refundedSunny={run.GetRefundedStock(FlowerIngredient.SunnyFruit)}");
            SaveProgress();
        }
    }
}
