using System.Collections.Generic;
using BorrowingSun.Market;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    /// <summary>Composition root for the Unity market. It adapts court facts and player commands to the domain.</summary>
    [DisallowMultipleComponent]
    public sealed class SkywellMarketModule : MonoBehaviour, ISkywellMarketInteraction
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform shop, mushrooms, rainFruit;
        [SerializeField] private SkywellWeatherZone rainZone;
        [SerializeField] private SkywellWeatherTarget rainTarget;
        [SerializeField, Min(.25f)] private float interactionRadius = 1.35f;
        [SerializeField, Min(.1f)] private float regrowSeconds = 8f;

        private MarketSession run;
        private ForageState mushroomState, fruitState;
        private SkywellForageEnvironment environment;
        private SkywellMarketView view;
        private readonly MarketSettings settings = new MarketSettings();
        private MarketPhase displayedPhase;
        private bool focused = true, modalPending, restorePanelOnFocus;
        private CourtPlayer queuedHarvester;
        private Point queuedPoint;
        private int openedFrame = -1, closedFrame = -1, commandFrame = -1;
        private int selectedSlot;
        private string feedback;
        private float feedbackUntil;
        private readonly HashSet<string> menu = new HashSet<string>();
        private enum Point { None, Shop, Mushrooms, Fruit }

        public MarketSession Run { get { EnsureRuntime(); return run; } }
        public MarketSettings Settings => settings;
        public SkywellController Court => court;
        public Transform Shop => shop;
        public Transform Mushrooms => mushrooms;
        public Transform RainFruit => rainFruit;
        public SkywellWeatherZone RainZone => rainZone;
        public SkywellWeatherTarget RainTarget => rainTarget;
        public ForageState MushroomState { get { EnsureRuntime(); return mushroomState; } }
        public ForageState FruitState { get { EnsureRuntime(); return fruitState; } }
        public ForageEnvironment LastFruitEnvironment { get; private set; }
        public bool IsShopOpen { get; private set; }
        public bool InputBlocked => IsShopOpen || closedFrame == Time.frameCount;
        public bool AllowsSunlight => run == null || !run.IsNight;
        public string Feedback => Time.unscaledTime < feedbackUntil ? feedback : null;

        public void Configure(SkywellController owner, Transform shopPoint, Transform mushroomPoint,
            Transform fruitPoint, SkywellWeatherZone fruitRainZone, SkywellWeatherTarget fruitRainTarget)
        {
            environment?.Restore();
            court = owner; shop = shopPoint; mushrooms = mushroomPoint; rainFruit = fruitPoint;
            rainZone = fruitRainZone; rainTarget = fruitRainTarget;
            environment = null;
            EnsureRuntime();
        }

        private void Awake() { if (!court) court = GetComponent<SkywellController>(); EnsureRuntime(); }
        private void OnGUI()
        {
            if (run == null) return;
            if (view == null) view = new SkywellMarketView(this);
            view.Draw();
        }
        private void EnsureRuntime()
        {
            if (run == null)
            {
                run = new MarketSession(settings);
                mushroomState = new ForageState(ForageKind.MushroomPatch, Mathf.Max(.1f, regrowSeconds));
                fruitState = new ForageState(ForageKind.WeatherTree, Mathf.Max(.1f, regrowSeconds));
                displayedPhase = MarketPhase.Locked;
            }
            if (environment == null && court) environment = new SkywellForageEnvironment(court);
        }

        public bool AllowsWeather(int station, SkywellToken token, bool delivered)
        {
            if (token == SkywellToken.Sun && !AllowsSunlight) return false;
            return !rainTarget || station != rainTarget.StationIndex
                || delivered && Run.Phase != MarketPhase.Locked && token == SkywellToken.Rain;
        }

        public void Tick(float seconds)
        {
            EnsureRuntime();
            if (!focused) return;
            run.Tick(seconds);
            environment?.ApplyNight(run.IsNight);
            if (run.Phase != displayedPhase)
            {
                displayedPhase = run.Phase;
                if (run.Phase == MarketPhase.Dusk || run.Phase == MarketPhase.Summary) modalPending = true;
                if (run.Phase == MarketPhase.Returning)
                { CloseShop(); Say("采集已结束。走回花庭店门，短按 E 选择今晚的菜谱。"); }
            }
            if (modalPending && SafeToOpen(court ? court.Player : null))
            { modalPending = false; OpenPanel(); }
            if (!IsShopOpen || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame) CloseShop();
            if (Keyboard.current.nKey.wasPressedThisFrame && run.Phase == MarketPhase.Summary) NextDay();
            if (Keyboard.current.digit1Key.wasPressedThisFrame && run.Phase == MarketPhase.Business) SelectSlot(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame && run.Phase == MarketPhase.Business) SelectSlot(1);
        }

        // Called after the court has applied weather and evaluated the real optical path this frame.
        public void AfterWorldTick(float seconds)
        {
            EnsureRuntime();
            if (!focused) { queuedHarvester = null; return; }
            LastFruitEnvironment = environment != null ? environment.Sample(rainFruit, rainZone, run.IsNight)
                : new ForageEnvironment(run.IsNight, false, false);
            mushroomState.Tick(run.CanHarvest ? seconds : 0, new ForageEnvironment(run.IsNight, false, false));
            fruitState.Tick(run.CanHarvest ? seconds : 0, LastFruitEnvironment);
            if (!queuedHarvester) return;
            var actor = queuedHarvester;
            var point = queuedPoint;
            queuedHarvester = null;
            if (!SafeToInteract(actor) || FindPoint(actor.FeetPosition) != point) { Say("站稳后再采集。"); return; }
            var state = point == Point.Mushrooms ? mushroomState : fruitState;
            var ingredient = state.CurrentIngredient;
            var result = run.TryHarvest(state);
            Say(result == MarketActionResult.Success ? "采到了 " + IngredientName(ingredient.Value) + " × 1。"
                : !run.CanHarvest ? "这段时间只能返店或经营，不能继续采集。"
                : !state.IsMature ? "还没有成熟，稍等 " + Mathf.CeilToInt(state.RemainingSeconds) + " 秒。"
                : "这里白天需要实际阳光或本地降雨，才能长出水果。");
        }

        public bool TryInteract(CourtPlayer actor)
        {
            if (!actor) return false;
            Point point = FindPoint(actor.FeetPosition);
            if (point == Point.None) return false;
            if (!SafeToInteract(actor)) { Say("先在岸上站稳，再交互。"); return true; }
            if (!court || !court.Voyage || !court.Voyage.Delivered) { Say("先完成运货并签收，再来开店。"); return true; }
            if (point == Point.Shop)
            {
                if (Run.Phase == MarketPhase.Locked)
                {
                    run.TryStart();
                    Say("第 1 天开始。采集蘑菇；给果树施雨，或寻找树上的阳光。黄昏再决定是否夜探。");
                }
                else if (run.Phase == MarketPhase.Day) Say("现在是白天，先准备食材。黄昏会让你选择返店或夜探。");
                else if (run.Phase == MarketPhase.NightForage)
                { run.TryReturnToShop(); run.TryArriveAtShop(); OpenPanel(); }
                else if (run.Phase == MarketPhase.Returning)
                { run.TryArriveAtShop(); OpenPanel(); }
                else OpenPanel();
                return true;
            }
            if (Run.Phase == MarketPhase.Locked) { Say("先到花庭店门短按 E，开始第一天。"); return true; }
            queuedHarvester = actor; queuedPoint = point;
            return true;
        }

        public string Prompt(Vector3 feet)
        {
            if (Feedback != null) return Feedback;
            switch (FindPoint(feet))
            {
                case Point.Shop:
                    return Run.Phase == MarketPhase.Locked ? "花庭外卖店 · 签收后短按 E 开始第一天"
                        : Run.Phase == MarketPhase.NightForage ? "花庭外卖店 · E 提前结束夜探，准备开店"
                        : Run.Phase == MarketPhase.Day ? "花庭外卖店 · 白天备料，黄昏开店" : "花庭外卖店 · E 进入";
                case Point.Mushrooms: return ForagePrompt("蘑菇点", MushroomState);
                case Point.Fruit: return ForagePrompt("果树", FruitState);
                default: return modalPending ? "先在固定地面站稳，再决定下一步。" : null;
            }
        }

        private string ForagePrompt(string name, ForageState state)
        {
            if (!Run.CanHarvest) return name + " · 现在不能采集";
            if (!state.IsMature) return name + " · " + Mathf.CeilToInt(state.RemainingSeconds) + " 秒后成熟";
            if (!state.CurrentIngredient.HasValue) return name + " · 白天等待阳光或本地降雨";
            return name + " · " + IngredientName(state.CurrentIngredient.Value) + " · 短按 E 采集";
        }

        private bool SafeToInteract(CourtPlayer actor) => actor && !actor.IsSwimming && !actor.IsExternallyDriven
            && !actor.IsAutoPositioning && actor.TryGetComponent<CharacterController>(out var body) && body.isGrounded;
        private bool SafeToOpen(CourtPlayer actor) => SafeToInteract(actor) && court && !court.OnRotor(actor.FeetPosition)
            && !court.ObservationInputBlocked && !court.WeatherInputBlocked
            && !(court.Journey && court.Journey.ControlsMovement)
            && !(court.Grove && court.Grove.Ride && court.Grove.Ride.IsMounted);
        private Point FindPoint(Vector3 feet) => Near(feet, shop) ? Point.Shop
            : Near(feet, mushrooms) ? Point.Mushrooms : Near(feet, rainFruit) ? Point.Fruit : Point.None;
        private bool Near(Vector3 feet, Transform point) => point && Mathf.Abs(feet.y - point.position.y) <= .65f
            && (new Vector2(feet.x, feet.z) - new Vector2(point.position.x, point.position.z)).sqrMagnitude <= interactionRadius * interactionRadius;

        private void OpenPanel()
        {
            IsShopOpen = true; openedFrame = Time.frameCount;
            if (run.Phase == MarketPhase.Menu && menu.Count == 0)
                foreach (var recipe in run.Recipes) if (run.CanMake(recipe.Id)) menu.Add(recipe.Id);
        }
        public void CloseShop()
        {
            if (!IsShopOpen) return;
            IsShopOpen = false; closedFrame = Time.frameCount;
        }
        private bool BeginCommand() => focused && IsShopOpen && Time.frameCount > openedFrame
            && commandFrame != Time.frameCount && ClaimFrame();
        private bool ClaimFrame() { commandFrame = Time.frameCount; return true; }
        public bool IsOnMenu(string recipeId) => menu.Contains(recipeId);
        public void ToggleMenuItem(string recipeId)
        {
            if (Run.Phase != MarketPhase.Menu || !BeginCommand()) return;
            if (!menu.Remove(recipeId)) menu.Add(recipeId);
        }
        public void DismissPanel() { if (BeginCommand()) CloseShop(); }
        private void Say(string value) { feedback = value; feedbackUntil = Time.unscaledTime + 6; }
        public void PrimaryCommand() { if (run != null && run.Phase == MarketPhase.Business) SelectSlot(selectedSlot); }
        public void ChooseEvening(bool explore)
        {
            if (!BeginCommand()) return;
            if (run.TryChooseEvening(explore) != MarketActionResult.Success) return;
            CloseShop();
            Say(explore ? "夜探开始，共 " + settings.NightSeconds.ToString("0") + " 秒。夜探后营业时间减半。"
                : "今晚营业 " + settings.BusinessSeconds.ToString("0") + " 秒。先走回花庭店门，短按 E 备菜单。");
        }
        public void OpenBusiness()
        {
            if (!BeginCommand()) return;
            Say(run.TryOpenBusiness(menu) == MarketActionResult.Success ? "开店了。两位顾客可同时备菜；完成后分别送餐收款。" : "请勾选至少一道食材足够的菜。");
        }
        public void SelectSlot(int slot)
        {
            if (!BeginCommand()) return;
            selectedSlot = slot;
            var order = run.GetOrder(slot);
            if (order == null) { Say("这一队还没有可接的订单，食材已不足。"); return; }
            if (order.Stage == OrderStage.Ready)
            { var result = run.TryServe(slot); Say(result == MarketActionResult.Success ? "送餐收款 +" + order.Recipe.Price + " 金币。" : "这份订单已处理。"); }
            else if (order.Stage == OrderStage.Waiting)
            { Say(run.TryCook(slot) == MarketActionResult.Success ? "已放入食材，正在烹饪。你可以同时准备另一队的菜。" : "暂时无法开始烹饪。"); }
            else Say("这份菜还在烹饪，先照顾另一队。");
        }
        public void CloseDay()
        {
            if (!BeginCommand()) return;
            if (run.TryClose() == MarketActionResult.Success) Say("打烊了。未送出的订单已退回食材。");
        }
        public void NextDay()
        {
            if (!BeginCommand()) return;
            if (run.TryNextDay() != MarketActionResult.Success) return;
            menu.Clear(); mushroomState.Reset(); fruitState.Reset(); CloseShop();
            environment?.ApplyNight(false);
            Say("新的一天开始。库存、收入和厨房升级已保留；机关与签收进度也还在。");
        }
        public void UpgradeKitchen()
        {
            if (!BeginCommand()) return;
            Say(run.TryUpgradeKitchen() == MarketActionResult.Success ? "花费 " + settings.UpgradeCost + " 金币升级厨房，之后烹饪用时减少 "
                + Mathf.RoundToInt((1 - settings.UpgradeTimeMultiplier) * 100) + "%。"
                : "升级需要 " + settings.UpgradeCost + " 金币，且只能购买一次。");
        }
        public void ResetMarket()
        {
            EnsureRuntime(); CloseShop(); run.Reset(); mushroomState.Reset(); fruitState.Reset(); menu.Clear();
            queuedHarvester = null; modalPending = false; displayedPhase = MarketPhase.Locked;
            feedback = null; feedbackUntil = 0; environment?.ApplyNight(false);
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) { restorePanelOnFocus = IsShopOpen; queuedHarvester = null; CloseShop(); }
            else if (restorePanelOnFocus) { modalPending = true; restorePanelOnFocus = false; }
        }
        private void OnDisable() { CloseShop(); queuedHarvester = null; environment?.Restore(); }

        public static string IngredientName(IngredientId ingredient)
        {
            switch (ingredient)
            {
                case IngredientId.Mushroom: return "蘑菇";
                case IngredientId.RainFruit: return "雨果";
                case IngredientId.MoonFruit: return "月果";
                case IngredientId.StormFruit: return "雨夜果";
                default: return "晴果";
            }
        }
    }
}
