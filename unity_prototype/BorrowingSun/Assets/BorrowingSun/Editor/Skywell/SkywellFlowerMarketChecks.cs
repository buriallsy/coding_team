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
            Require(market.Run.IsStarted && market.Run.Day == 1 && market.Run.Cash == 0 && market.IsShopOpen,
                "A separate short E at the shop starts day one and opens its panel.");
            yield return Press(Key.Digit2);
            Require(market.Run.OrderStage == FlowerOrderStage.Idle,
                "An empty bag cannot start the fruit recipe.");
            input = new KeyboardState(Key.Tab);
            yield return Wait(court.weatherHoldTime + .12f);
            Require(!court.WeatherSelectionHeld && !court.WeatherPickerOpen, "The shop panel blocks the weather selector.");
            input = new KeyboardState();
            yield return Wait(.15f);
            Capture(Path.Combine(Folder, "flower-market-shop.png"));
            yield return Press(Key.Q);
            Require(!market.IsShopOpen && !court.IsObserving, "Q closes the shop and restores ordinary movement.");

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
            yield return Walk(rainFruit, "Shop to weather fruit");
            float atRainFruit = Time.time;
            Require(!market.RainZone.IsRain && market.RainZone.Contains(court.Player.FeetPosition) &&
                market.RainZone.Contains(rainFruit), "The feet and fruit are inside the inactive local rain volume.");
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.WeatherFruit) == 0, "Short E cannot gather weather fruit while dry.");
            yield return PickWeather(SkywellToken.Rain);
            bool wasShoulder = court.CameraRig.IsShoulderView;
            yield return BeginTargeting(market.RainTarget);
            yield return ClickWeatherPointer(market.RainTarget, requireHover: true);
            Require(court.Consoles[stationIndex].Token == SkywellToken.Rain,
                "A real pointer hit on the Flower Court mechanism inserts the rain token.");
            yield return WaitUntil(() => market.RainZone.IsRain, 4, "The local rain zone becomes active after the weather transition.");
            Require(court.Consoles.Take(stationIndex).All(slot => slot.Token != SkywellToken.Rain && !slot.Zone.IsRain),
                "No original station receives Flower Court rain.");
            yield return ReleaseTargeting();
            yield return EnsureCameraMode(wasShoulder);
            Capture(Path.Combine(Folder, "flower-market-local-rain.png"));
            yield return Press(Key.E);
            Require(market.Run.GetStock(FlowerIngredient.WeatherFruit) == 1, "Short E gathers the fruit under actual local rain.");
            returning = Time.time;
            yield return Walk(shop, "Weather fruit to shop");
            RecordFlowerTrip("weather fruit", outbound, atRainFruit, returning);
            Require(!market.RainZone.Contains(court.Player.FeetPosition), "The shop is outside the rain operating area.");
            yield return WaitUntil(() => !market.RainZone.IsRain &&
                court.Consoles[stationIndex].Token == SkywellToken.None, 4, "Leaving the patch automatically returns the rain token.");

            yield return Press(Key.E);
            Require(market.IsShopOpen, "Short E reopens the shop with the gathered ingredients.");
            yield return CookAndServeFlowerOrder(market, Key.Digit1, FlowerRecipe.MushroomBento, 2, 1, 12);
            yield return CookAndServeFlowerOrder(market, Key.Digit2, FlowerRecipe.FruitBento, 1, 0, 34);
            Capture(Path.Combine(Folder, "flower-market-two-orders.png"));
            yield return Press(Key.N);
            Require(market.Run.Day == 2 && market.Run.Cash == 34 && market.Run.OrderStage == FlowerOrderStage.Idle &&
                market.Run.GetStock(FlowerIngredient.Mushroom) == 1 && market.Run.GetStock(FlowerIngredient.WeatherFruit) == 0,
                "Actual N starts day two and retains cash and the unused ingredient.");
            Require(court.Voyage.Delivered && originalCargo.activeSelf && court.LiftLatched &&
                Mathf.Abs(court.Height - towerHeight) < .01f && Mathf.Abs(court.Yaw - towerYaw) < .01f &&
                Mathf.Abs(court.WaterLevel - towerWater) < .01f && Mathf.Abs(court.BallastAmount - ballast) < .01f,
                "Rain, cooking and day advancement preserve the original signed cargo and tower.");
            Capture(Path.Combine(Folder, "flower-market-day-two.png"));

            if (!market.IsShopOpen) yield return Press(Key.E);
            Require(market.IsShopOpen, "The shop can open on day two before the reset test.");
            yield return Press(Key.R);
            yield return Wait(.4f);
            CheckReset();
            Require(!market.IsShopOpen && !market.Run.IsStarted && market.Run.Day == 0 && market.Run.Cash == 0 &&
                market.Run.GetStock(FlowerIngredient.Mushroom) == 0 && market.Run.GetStock(FlowerIngredient.WeatherFruit) == 0 &&
                market.Run.OrderStage == FlowerOrderStage.Idle && Vector3.Distance(court.Player.FeetPosition, spawn) < .5f,
                "Actual R while the shop is open clears the market and restores the original entrance.");
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
            yield return Press(Key.N);
            Require(market.Run.Day == 1 && market.Run.Cash == cashBeforeOrder,
                "A pending order rejects early serving and advancing the day.");
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
