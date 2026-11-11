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
                "The Flower Court shop, both resource stops and the local rain mechanism are authored.");
            int stationIndex = market.RainTarget.StationIndex;
            Require(stationIndex == 5 && court.Consoles.Length == 6 &&
                court.Consoles[stationIndex] && court.Consoles[stationIndex].Zone == market.RainZone &&
                WeatherTargetForStation(stationIndex) == market.RainTarget,
                "The Flower Court rain station is appended after the five original stations.");
            yield return Wait(.2f);
            Require(!market.Run.IsStarted && !market.IsShopOpen, "The market starts locked before the voyage.");
            Vector3 spawn = court.Player.FeetPosition;

            yield return FullRoute(continueAfterDelivery: true);
            Require(court.Voyage.Delivered && !market.Run.IsStarted && !market.IsShopOpen,
                "The original E signature leaves the delivered voyage intact and does not open the market.");
            Require(court.BorrowedSunAtVillage && court.Optics.SettlementReceiver.RewardAvailable,
                "The original live collector beam and its earned reward are still present after signing in daylight.");
            float towerHeight = court.Height;
            float towerYaw = court.Yaw;
            float towerWater = court.WaterLevel;
            float ballast = court.BallastAmount;
            var originalCargo = court.Optics.SettlementReceiver.fruitCrate;

            Vector3 shop = market.Shop.position;
            Vector3 mushrooms = market.Mushrooms.position;
            Vector3 rainFruit = market.RainFruit.position;
            Require(Vector3.Distance(shop, mushrooms) > 1.35f && Vector3.Distance(shop, rainFruit) > 1.35f,
                "Both ingredient stops require leaving the shop.");
            report.Add($"FLOWER ANCHORS shop={shop} mushrooms={mushrooms} rainFruit={rainFruit}");
            SaveProgress();

            yield return Walk(shop, "Flower Court handover to shop");
            yield return Press(Key.E);
            Require(market.Run.Phase == FlowerMarketPhase.DayGathering && market.Run.Day == 1 &&
                market.Run.Cash == 0 && market.Run.CanGather && !market.IsDuskOpen && !market.IsShopOpen,
                "A separate short E starts daylight gathering with no modal and an empty cashbox.");
            float dayRemaining = market.Run.RemainingGatherSeconds;
            Require(dayRemaining > 0, "The live daytime gathering countdown starts on day one.");

            float outbound = Time.time;
            yield return Walk(mushrooms, "Shop to dry mushrooms");
            float atMushrooms = Time.time;
            for (int collected = 1; collected <= 4; collected++)
            {
                yield return Press(Key.E);
                Require(market.Run.GetStock(FlowerIngredient.Mushroom) == collected,
                    $"Short E collects mushroom {collected} from the authored patch.");
                if (collected < 4) yield return Wait(1.25f);
            }
            float returning = Time.time;
            yield return Walk(shop, "Dry mushrooms to shop");
            RecordFlowerTrip("dry mushrooms", outbound, atMushrooms, returning);

            outbound = Time.time;
            yield return Walk(rainFruit, "Shop to the daylight orchard");
            float atRainFruit = Time.time;
            Require(market.Run.RemainingGatherSeconds < dayRemaining, "The daytime clock advances during actual gathering and travel.");
            Require(!market.RainZone.IsRain && market.RainZone.Contains(court.Player.FeetPosition) &&
                market.RainZone.Contains(rainFruit), "The feet and fruit are inside the inactive local rain volume.");
            RecordFlowerCrop("daylight arrival", market);
            Require(market.SunlightAvailable && market.OrchardReceivesSunlight &&
                market.OrchardProduce == FlowerIngredient.SunnyFruit,
                "The authored daytime orchard receives actual sunlight and offers SunnyFruit while dry.");
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.SunnyFruit) == 1 &&
                market.Run.GetStock(FlowerIngredient.WeatherFruit) == 0,
                "Short E picks one SunnyFruit in the dry daylight orchard.");
            RecordFlowerCrop("daylight", market);
            Capture(Path.Combine(Folder, "flower-market-sunny-day.png"));
            yield return CastFlowerMarketRain(market);
            Require(market.OrchardProduce == FlowerIngredient.WeatherFruit,
                "Actual local rain changes the daytime orchard to WeatherFruit.");
            Capture(Path.Combine(Folder, "flower-market-local-rain.png"));
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.WeatherFruit) == 1 &&
                market.Run.GetStock(FlowerIngredient.SunnyFruit) == 1,
                "Short E picks one WeatherFruit under daytime rain and preserves the earlier SunnyFruit.");
            RecordFlowerCrop("daytime rain", market);
            returning = Time.time;
            yield return Walk(shop, "Daylight orchard to shop");
            RecordFlowerTrip("weather fruit", outbound, atRainFruit, returning);
            Require(!market.RainZone.Contains(court.Player.FeetPosition), "The shop is outside the rain operating area.");
            yield return WaitForFlowerMarketRainReturn(market);

            yield return EnterFlowerMarketDusk(market);
            Capture(Path.Combine(Folder, "flower-market-dusk-choice.png"));
            yield return Press(Key.Digit1);
            Require(market.Run.Day == 1 && market.Run.Phase == FlowerMarketPhase.Trading && market.IsShopOpen &&
                !market.IsDuskOpen && market.Run.IsNight && !market.Run.CanGather,
                "Actual 1 chooses direct return at the shop and opens trading without a night outing.");
            yield return WaitUntil(() => !market.SunlightAvailable && !court.Optics.Collector.Active &&
                !court.BorrowedSunAtVillage, 4, "Nightfall extinguishes the actual collector beam and village ray.");
            Require(court.Voyage.Delivered && originalCargo.activeSelf && court.Optics.SettlementReceiver.RewardAvailable,
                "Nightfall preserves the original reward and signed delivery even while the live ray is off.");
            Capture(Path.Combine(Folder, "flower-market-shop.png"));
            yield return Press(Key.Q);
            Require(!market.IsShopOpen && !court.IsObserving && market.Run.Phase == FlowerMarketPhase.Trading,
                "Q hides trading and restores movement while preserving the trading phase.");
            yield return Press(Key.E);
            Require(market.IsShopOpen, "A separate short E reopens trading with the gathered ingredients.");
            yield return CheckFlowerMarketModal(market, "The trading panel");
            yield return CookAndServeFlowerOrder(market, Key.Digit1, FlowerRecipe.MushroomBento, 2, 1, 12);
            yield return CookAndServeFlowerOrder(market, Key.Digit2, FlowerRecipe.FruitBento, 1, 0, 34);
            Capture(Path.Combine(Folder, "flower-market-two-orders.png"));
            yield return Press(Key.N);
            Require(market.Run.Day == 2 && market.Run.Phase == FlowerMarketPhase.DayGathering && !market.IsShopOpen &&
                !market.Run.IsNight && market.Run.Cash == 34 && market.Run.OrderStage == FlowerOrderStage.Idle &&
                market.Run.GetStock(FlowerIngredient.Mushroom) == 1 &&
                market.Run.GetStock(FlowerIngredient.SunnyFruit) == 1 && market.SunlightAvailable,
                "Actual N closes trading for dawn on day two and carries forward cash and unused ingredients.");
            Capture(Path.Combine(Folder, "flower-market-day-two.png"));

            yield return EnterFlowerMarketDusk(market);
            yield return Press(Key.Digit2);
            Require(market.Run.Day == 2 && market.Run.Phase == FlowerMarketPhase.NightExploration && market.Run.IsNight &&
                market.Run.CanGather && market.Run.RemainingGatherSeconds > 0 && !market.IsDuskOpen && !market.IsShopOpen,
                "Actual 2 closes the dusk choice and starts timed night exploration with free movement.");
            float nightRemaining = market.Run.RemainingGatherSeconds;
            yield return Walk(rainFruit, "Shop to the night orchard");
            Require(market.Run.RemainingGatherSeconds < nightRemaining, "The night clock advances during actual travel.");
            Require(!market.SunlightAvailable && !market.OrchardReceivesSunlight &&
                !market.FruitIsWet && !market.OrchardProduce.HasValue,
                "The dry orchard has no sunlight or produce at night.");
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.RainNightFruit) == 0 &&
                market.Run.GetStock(FlowerIngredient.SunnyFruit) == 1 &&
                market.Run.GetStock(FlowerIngredient.WeatherFruit) == 0,
                "Short E in the dry night orchard leaves all fruit stocks unchanged.");
            RecordFlowerCrop("dry night", market);
            Capture(Path.Combine(Folder, "flower-market-night-dry.png"));
            yield return CastFlowerMarketRain(market);
            Require(!market.SunlightAvailable && !market.OrchardReceivesSunlight && market.FruitIsWet &&
                market.OrchardProduce == FlowerIngredient.RainNightFruit,
                "Actual rain at night makes the orchard produce RainNightFruit without daylight.");
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.RainNightFruit) == 1 &&
                market.Run.GetStock(FlowerIngredient.SunnyFruit) == 1 &&
                market.Run.GetStock(FlowerIngredient.WeatherFruit) == 0,
                "Short E picks one RainNightFruit at night without producing either daylight fruit.");
            RecordFlowerCrop("night rain", market);
            Capture(Path.Combine(Folder, "flower-market-night-rain.png"));
            yield return Walk(shop, "Night orchard to the Flower Court shop");
            yield return WaitForFlowerMarketRainReturn(market);
            yield return Press(Key.E);
            Require(market.Run.Day == 2 && market.Run.Phase == FlowerMarketPhase.Trading && market.IsShopOpen &&
                market.Run.GetStock(FlowerIngredient.RainNightFruit) == 1,
                "A separate short E ends night exploration and opens trading with the rain-night crop.");
            Capture(Path.Combine(Folder, "flower-market-night-trading.png"));
            yield return Press(Key.Digit2);
            Require(market.Run.OrderStage == FlowerOrderStage.Cooking &&
                market.Run.GetStock(FlowerIngredient.RainNightFruit) == 0 &&
                market.Run.GetStock(FlowerIngredient.SunnyFruit) == 1,
                "The night fruit takes precedence over sunny fruit when starting the fruit recipe.");
            yield return Press(Key.N);
            Require(market.Run.Day == 3 && market.Run.Phase == FlowerMarketPhase.DayGathering && !market.IsShopOpen &&
                !market.Run.IsNight && market.Run.Cash == 34 && market.Run.OrderStage == FlowerOrderStage.Idle &&
                market.Run.GetStock(FlowerIngredient.RainNightFruit) == 1 &&
                market.Run.GetStock(FlowerIngredient.Mushroom) == 1,
                "Actual N closes trading for day-three dawn, refunds the pending recipe and preserves cash.");
            yield return WaitUntil(() => market.SunlightAvailable && market.OrchardReceivesSunlight &&
                court.BorrowedSunAtVillage, 4, "Dawn restores actual orchard sunlight and the original village light path.");
            RecordFlowerCrop("dawn after night", market);
            Capture(Path.Combine(Folder, "flower-market-dawn-after-night.png"));
            Require(court.Voyage.Delivered && originalCargo.activeSelf && court.LiftLatched &&
                Mathf.Abs(court.Height - towerHeight) < .01f && Mathf.Abs(court.Yaw - towerYaw) < .01f &&
                Mathf.Abs(court.WaterLevel - towerWater) < .01f && Mathf.Abs(court.BallastAmount - ballast) < .01f,
                "Both dusk branches, local rain, trading and dawn preserve the signed cargo, tower, water and ballast.");

            yield return EnterFlowerMarketDusk(market);
            yield return Press(Key.Digit2);
            Require(market.Run.IsNight && !market.SunlightAvailable, "The reset test starts from the live night state.");
            yield return Press(Key.R);
            yield return Wait(.4f);
            CheckReset();
            Require(!market.IsShopOpen && !market.IsDuskOpen && market.Run.Phase == FlowerMarketPhase.Locked &&
                market.Run.Day == 0 && market.Run.Cash == 0 && !market.Run.IsNight && market.SunlightAvailable &&
                Enum.GetValues(typeof(FlowerIngredient)).Cast<FlowerIngredient>().All(ingredient => market.Run.GetStock(ingredient) == 0) &&
                market.Run.OrderStage == FlowerOrderStage.Idle && Vector3.Distance(court.Player.FeetPosition, spawn) < .5f,
                "Actual R from night clears the market and restores the original entrance and daylight.");
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
                "A real pointer hit on the Flower Court mechanism inserts the rain token.");
            yield return WaitUntil(() => market.RainZone.IsRain, 4,
                "The local rain zone becomes active after the weather transition.");
            Require(court.Consoles.Take(stationIndex).All(slot => slot.Token != SkywellToken.Rain && !slot.Zone.IsRain),
                "No original station receives Flower Court rain.");
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

        private static IEnumerator CookAndServeFlowerOrder(SkywellFlowerMarket market, Key key,
            FlowerRecipe recipe, int mushroomStock, int fruitStock, int expectedCash)
        {
            int cashBeforeOrder = market.Run.Cash;
            yield return Press(key);
            Require(market.Run.CurrentRecipe == recipe && market.Run.OrderStage == FlowerOrderStage.Cooking &&
                market.Run.CookTimeRemaining > 0 && market.Run.GetStock(FlowerIngredient.Mushroom) == mushroomStock &&
                market.Run.GetStock(FlowerIngredient.WeatherFruit) == fruitStock,
                $"{key} starts {recipe} and consumes the correct ingredients exactly once.");
            yield return Press(Key.E);
            Require(market.Run.Day == 1 && market.Run.Cash == cashBeforeOrder,
                "A pending order rejects early serving.");
            yield return WaitUntil(() => market.Run.OrderStage == FlowerOrderStage.Ready, 8,
                $"{recipe} becomes ready through the live cooking timer.");
            yield return Press(Key.E);
            Require(market.Run.OrderStage == FlowerOrderStage.Served && market.Run.Cash == expectedCash,
                $"E serves {recipe} for the expected total of {expectedCash}.");
            yield return Press(Key.E);
            Require(market.Run.Cash == expectedCash, "Repeated E cannot collect the same order payment twice.");
        }
    }
}
