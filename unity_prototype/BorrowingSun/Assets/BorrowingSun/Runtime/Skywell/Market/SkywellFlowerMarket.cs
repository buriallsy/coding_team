using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    public sealed class SkywellFlowerMarket : MonoBehaviour
    {
        [SerializeField] private Transform shop, mushrooms, rainFruit;
        [SerializeField] private SkywellWeatherZone rainZone;
        [SerializeField] private SkywellWeatherTarget rainTarget;
        [SerializeField] private SkywellSceneLighting lighting;
        [SerializeField] private Transform orchardSunSample;
        [SerializeField] private TextMesh orchardLabel;
        [SerializeField] private Renderer orchardCanopy;
        [SerializeField] private Light shopLamp;
        [SerializeField, Min(.25f)] private float interactionRadius = 1.35f;
        [SerializeField, Min(0f)] private float regrowSeconds = 1f;
        [SerializeField, Min(1f)] private float dayGatherSeconds = 75f;
        [SerializeField, Min(1f)] private float nightGatherSeconds = 45f;
        [Header("Trading")]
        [SerializeField, Min(1f)] private float directTradingSeconds = 120f;
        [SerializeField, Min(1f)] private float nightTradingSeconds = 60f;
        [Header("Kitchen")]
        [SerializeField, Min(1)] private int kitchenUpgradeCost = 36;
        [SerializeField, Min(.1f)] private float mushroomCookSeconds = 3f;
        [SerializeField, Min(.1f)] private float fruitCookSeconds = 4f;
        [SerializeField, Min(.1f)] private float upgradedMushroomCookSeconds = 2.1f;
        [SerializeField, Min(.1f)] private float upgradedFruitCookSeconds = 2.8f;

        private enum Point { None, Shop, Mushrooms, Orchard }
        private enum LightPeriod { Day, Dusk, Night }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly RaycastHit[] sunHits = new RaycastHit[16];
        private FlowerMarketRun run;
        private CourtPlayer player;
        private MaterialPropertyBlock canopyTint;
        private FlowerIngredient? lastProduce;
        private LightPeriod lightPeriod;
        private float mushroomsReadyAt, fruitReadyAt;
        private int closedFrame = -1, commandFrame = -1;
        private string feedback;
        private float feedbackUntil;

        public FlowerMarketRun Run => run ??= new FlowerMarketRun(dayGatherSeconds, nightGatherSeconds,
            mushroomCookSeconds, fruitCookSeconds, upgradedMushroomCookSeconds,
            upgradedFruitCookSeconds, kitchenUpgradeCost, directTradingSeconds, nightTradingSeconds);
        public Transform Shop => shop;
        public Transform Mushrooms => mushrooms;
        public Transform RainFruit => rainFruit;
        public SkywellWeatherZone RainZone => rainZone;
        public SkywellWeatherTarget RainTarget => rainTarget;
        public bool IsShopOpen { get; private set; }
        public bool IsDuskOpen { get; private set; }
        public bool IsSummaryOpen => Run.Phase == FlowerMarketPhase.DaySummary;
        public bool InputBlocked => IsShopOpen || IsDuskOpen || IsSummaryOpen || closedFrame == Time.frameCount;
        public float DirectTradingSeconds => directTradingSeconds;
        public float NightTradingSeconds => nightTradingSeconds;
        public bool FruitIsWet => rainFruit && rainZone && rainZone.IsRain && rainZone.Contains(rainFruit.position);
        public bool SunlightAvailable => !Run.IsNight && lighting && lighting.sunLight &&
            lighting.sunLight.enabled && lighting.sunLight.intensity > 0f;
        public bool OrchardReceivesSunlight => SunlightAvailable && IsOrchardUnoccluded();
        public FlowerIngredient? OrchardProduce => Run.GetOrchardProduce(FruitIsWet,
            Run.CanGather && !Run.IsNight && !FruitIsWet && OrchardReceivesSunlight);
        public string Feedback => Time.time < feedbackUntil ? feedback : null;

        internal bool AllowsWeather(int station, SkywellToken token, bool unlocked) =>
            !rainTarget || station != rainTarget.StationIndex ||
            token != SkywellToken.Rain || unlocked && Run.CanGather;

        internal bool TryInteract(CourtPlayer actor, bool towerMoving)
        {
            Point point = FindPoint(actor.FeetPosition);
            if (point == Point.None) return false;
            if (!CanStandOnGround(actor) || point == Point.Shop && towerMoving)
            {
                Say("站稳并等塔停下后再操作。");
                return true;
            }

            if (point == Point.Shop)
            {
                UseShop();
                return true;
            }

            if (!Run.CanGather)
            {
                Say(Run.IsStarted ? "采集时段已结束，回小铺继续。" : "先到花庭小铺开始第一天。");
                return true;
            }

            if (point == Point.Mushrooms)
            {
                Harvest(FlowerIngredient.Mushroom, ref mushroomsReadyAt);
                return true;
            }

            var produce = OrchardProduce;
            if (produce.HasValue) Harvest(produce.Value, ref fruitReadyAt);
            else Say(Run.IsNight ? "夜里无晴果。借东岸的雨可采雨夜果。" : "果树没有直射日光。借东岸的雨可采雨露果。");
            return true;
        }

        internal string Prompt(Vector3 feet)
        {
            var point = FindPoint(feet);
            if (point == Point.None) return null;
            if (Feedback != null) return Feedback;

            if (point == Point.Shop)
            {
                return Run.Phase switch
                {
                    FlowerMarketPhase.Locked => "E 开始第一天",
                    FlowerMarketPhase.DayGathering => "E 提前结束白天采集",
                    FlowerMarketPhase.DuskChoice => "E 选择黄昏行程",
                    FlowerMarketPhase.Trading when Run.OrdersAccepted => "E 返回柜台处理订单",
                    _ => "E 回店营业"
                };
            }

            if (Run.Phase == FlowerMarketPhase.DuskChoice) return "黄昏到了 · 站稳后选择行程";
            if (Run.Phase == FlowerMarketPhase.ReturningToShop) return "采集结束 · 回中央塔按 E 营业";
            if (Run.Phase == FlowerMarketPhase.Trading) return "回中央塔按 E 打开柜台";
            if (Run.CanGather && point == Point.Mushrooms) return "E 采菌菇";
            if (Run.CanGather && point == Point.Orchard)
            {
                var produce = OrchardProduce;
                if (produce.HasValue) return "E 采" + IngredientName(produce.Value);
                return Run.IsNight ? "夜间无晴果 · 施雨可采雨夜果" : "此处无直射日光 · 施雨可采雨露果";
            }

            return Run.IsStarted ? "采集后回中央塔小铺" : "到中央塔小铺按 E 开始经营";
        }

        internal void Tick(float deltaSeconds, CourtPlayer actor, bool canOpenDusk)
        {
            player = actor;
            bool wasTrading = Run.Phase == FlowerMarketPhase.Trading;
            Run.Tick(deltaSeconds);
            if (wasTrading && IsSummaryOpen) OpenSummary();
            if (Run.Phase == FlowerMarketPhase.DuskChoice && !IsDuskOpen && canOpenDusk && CanStandOnGround(actor))
                OpenDusk();
            RefreshLighting();
            RefreshOrchard();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (IsSummaryOpen)
            {
                if (keyboard.nKey.wasPressedThisFrame) StartNextDay();
                return;
            }
            if (IsDuskOpen)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) ChooseDusk(false, actor);
                else if (keyboard.digit2Key.wasPressedThisFrame) ChooseDusk(true, actor);
                return;
            }
            if (!IsShopOpen) return;
            if (keyboard.escapeKey.wasPressedThisFrame) HideShop();
            else if (keyboard.nKey.wasPressedThisFrame) EndTrading();
            else if (keyboard.uKey.wasPressedThisFrame) UpgradeKitchen();
            else if (keyboard.digit1Key.wasPressedThisFrame) UseStove(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) UseStove(1);
        }

        internal void ChooseDusk(bool explore, CourtPlayer actor)
        {
            if (!IsDuskOpen || !BeginCommand() || !Run.TryChooseDusk(explore)) return;
            IsDuskOpen = false;
            closedFrame = Time.frameCount;
            if (explore) Say("夜探开始。夜里无晴果，借雨可采雨夜果。");
            else if (Near(actor.FeetPosition, shop)) OpenTrading();
            else Say("回中央塔小铺按 E 营业。");
            RefreshLighting();
            RefreshOrchard();
        }

        internal void AcceptOrders()
        {
            if (!IsShopOpen || !BeginCommand()) return;
            if (Run.OrdersAccepted)
            {
                Say("按窗口的数字键下锅或交餐。");
                return;
            }
            Say(Run.TryAcceptOrders() ? "已接单，按 1 或 2 给对应窗口下锅。" : "食材不足，按 N 收摊查看日结。");
        }

        internal void UpgradeKitchen()
        {
            if (!IsShopOpen || !BeginCommand()) return;
            if (Run.KitchenUpgraded) Say("厨房已升级，两口灶都已提速。");
            else if (Run.OrdersAccepted) Say("明天接单前可以升级厨房。");
            else if (Run.TryUpgradeKitchen()) Say($"支付 {Run.KitchenUpgradeCost} 元 · 两口灶已提速。");
            else Say($"钱箱不足 {Run.KitchenUpgradeCost} 元。");
        }

        internal void UseStove(int slot)
        {
            if (!IsShopOpen || !BeginCommand()) return;
            switch (Run.GetStove(slot).Stage)
            {
                case FlowerOrderStage.Waiting:
                    if (Run.TryCook(slot)) Say($"窗口 {slot + 1} 已下锅。");
                    break;
                case FlowerOrderStage.Ready:
                    int previousCash = Run.Cash;
                    if (Run.TryServe(slot)) Say($"窗口 {slot + 1} 已交餐，收款 +{Run.Cash - previousCash} 元。");
                    break;
                case FlowerOrderStage.Cooking:
                    Say($"窗口 {slot + 1} 正在烹饪。");
                    break;
                default:
                    Say(Run.OrdersAccepted ? "这个窗口暂无订单。" : "先按 E 接单。");
                    break;
            }
        }

        internal void EndTrading()
        {
            if (!IsShopOpen || !BeginCommand() || !Run.TryCloseShop()) return;
            OpenSummary();
        }

        internal void StartNextDay()
        {
            if (!IsSummaryOpen || !BeginCommand() || !Run.TryStartNextDay()) return;
            closedFrame = Time.frameCount;
            mushroomsReadyAt = fruitReadyAt = Time.time;
            RefreshLighting();
            RefreshOrchard();
            Say($"第 {Run.Day} 天 · 钱箱、食材和厨房状态已结转。");
        }

        private void OpenSummary()
        {
            HideShop();
            commandFrame = Time.frameCount;
            feedbackUntil = 0f;
        }

        internal void HideShop()
        {
            if (!IsShopOpen) return;
            IsShopOpen = false;
            closedFrame = Time.frameCount;
        }

        internal void ResetMarket()
        {
            HideShop();
            IsDuskOpen = false;
            closedFrame = Time.frameCount;
            Run.Reset();
            mushroomsReadyAt = fruitReadyAt = feedbackUntil = 0f;
            feedback = null;
            RefreshLighting();
            RefreshOrchard();
        }

        private void OnDisable()
        {
            HideShop();
            IsDuskOpen = false;
            if (Application.isPlaying)
            {
                if (lighting) lighting.RestoreAuthoredLighting();
                if (shopLamp) shopLamp.enabled = false;
            }
            lightPeriod = LightPeriod.Day;
        }

        private void UseShop()
        {
            switch (Run.Phase)
            {
                case FlowerMarketPhase.Locked:
                    Run.TryStart();
                    Say("第一天 · 借雨升塔，借风接桥。北庭采菌菇，东岸采果。");
                    break;
                case FlowerMarketPhase.DayGathering:
                    Run.TryEndDay();
                    OpenDusk();
                    break;
                case FlowerMarketPhase.DuskChoice:
                    OpenDusk();
                    break;
                case FlowerMarketPhase.NightExploration:
                    Run.TryReturnToShop();
                    OpenTrading();
                    break;
                default:
                    OpenTrading();
                    break;
            }
            RefreshLighting();
            RefreshOrchard();
        }

        private void OpenDusk()
        {
            IsDuskOpen = true;
            commandFrame = Time.frameCount;
            feedbackUntil = 0f;
        }

        private void OpenTrading()
        {
            if (Run.Phase != FlowerMarketPhase.Trading && !Run.TryOpenShop()) return;
            IsShopOpen = true;
            commandFrame = Time.frameCount;
            feedbackUntil = 0f;
        }

        private void RefreshLighting()
        {
            LightPeriod next = Run.IsNight ? LightPeriod.Night :
                Run.Phase == FlowerMarketPhase.DuskChoice ? LightPeriod.Dusk : LightPeriod.Day;
            if (next == lightPeriod) return;

            lightPeriod = next;
            if (shopLamp) shopLamp.enabled = next == LightPeriod.Night;
            if (!lighting) return;
            switch (next)
            {
                case LightPeriod.Dusk:
                    lighting.SetRuntimeLighting(new Color(.31f,.22f,.30f), new Color(.36f,.28f,.32f),
                        new Color(1f,.65f,.39f), .85f);
                    break;
                case LightPeriod.Night:
                    lighting.SetRuntimeLighting(new Color(.07f,.11f,.18f), new Color(.30f,.36f,.47f),
                        lighting.sunColor, 0f);
                    break;
                default:
                    lighting.RestoreAuthoredLighting();
                    break;
            }
        }

        private bool IsOrchardUnoccluded()
        {
            if (!orchardSunSample || lighting.sunLight.type != LightType.Directional) return false;
            Vector3 towardSun = -lighting.sunLight.transform.forward;
            if (towardSun.y <= .01f) return false;
            int count = Physics.RaycastNonAlloc(orchardSunSample.position + towardSun * .08f, towardSun,
                sunHits, 250f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == sunHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!player || !sunHits[i].transform.IsChildOf(player.transform)) return false;
            return true;
        }

        private void RefreshOrchard()
        {
            var produce = OrchardProduce;
            if (produce == lastProduce && canopyTint != null) return;
            lastProduce = produce;
            if (orchardLabel) orchardLabel.text = produce.HasValue ? IngredientName(produce.Value) : "果树";
            if (!orchardCanopy) return;
            canopyTint ??= new MaterialPropertyBlock();
            Color color = produce switch
            {
                FlowerIngredient.SunnyFruit => new Color(1f,.65f,.18f),
                FlowerIngredient.WeatherFruit => new Color(.15f,.76f,.61f),
                FlowerIngredient.RainNightFruit => new Color(.63f,.49f,.94f),
                _ => new Color(.32f,.44f,.39f)
            };
            orchardCanopy.GetPropertyBlock(canopyTint);
            canopyTint.SetColor(BaseColor, color);
            orchardCanopy.SetPropertyBlock(canopyTint);
        }

        private static bool CanStandOnGround(CourtPlayer actor) =>
            actor && !actor.IsSwimming && !actor.IsExternallyDriven && !actor.IsAutoPositioning &&
            actor.TryGetComponent<CharacterController>(out var body) && body.isGrounded;

        private Point FindPoint(Vector3 feet)
        {
            if (Near(feet, shop)) return Point.Shop;
            if (Near(feet, mushrooms)) return Point.Mushrooms;
            return Near(feet, rainFruit) ? Point.Orchard : Point.None;
        }

        private bool Near(Vector3 feet, Transform point) => point &&
            Mathf.Abs(feet.y - point.position.y) <= .65f &&
            (feet - point.position).sqrMagnitude <= interactionRadius * interactionRadius;

        private void Harvest(FlowerIngredient ingredient, ref float readyAt)
        {
            if (Time.time < readyAt)
            {
                Say("食材正在长回来。");
                return;
            }

            if (!Run.TryHarvest(ingredient)) return;
            readyAt = Time.time + regrowSeconds;
            Say($"采到{IngredientName(ingredient)} ×1。");
        }

        internal static string IngredientName(FlowerIngredient ingredient) => ingredient switch
        {
            FlowerIngredient.Mushroom => "菌菇",
            FlowerIngredient.SunnyFruit => "晴果",
            FlowerIngredient.WeatherFruit => "雨露果",
            FlowerIngredient.RainNightFruit => "雨夜果",
            _ => "食材"
        };

        private bool BeginCommand()
        {
            if (commandFrame == Time.frameCount) return false;
            commandFrame = Time.frameCount;
            return true;
        }

        private void Say(string text)
        {
            feedback = text;
            feedbackUntil = Time.time + 3f;
        }
    }
}
