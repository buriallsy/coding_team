using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowingSun
{
    public sealed class SkywellFlowerMarket : MonoBehaviour
    {
        [SerializeField] private Transform shop, mushrooms, rainFruit;
        [SerializeField] private SkywellWeatherZone rainZone;
        [SerializeField] private SkywellWeatherTarget rainTarget;
        [SerializeField, Min(.25f)] private float interactionRadius = 1.35f;
        [SerializeField, Min(0f)] private float regrowSeconds = 1f;

        private enum Point { None, Shop, Mushrooms, RainFruit }
        private float mushroomsReadyAt, fruitReadyAt;
        private int closedFrame = -1, commandFrame = -1;
        private string feedback;
        private float feedbackUntil;

        public FlowerMarketRun Run { get; } = new FlowerMarketRun();
        public Transform Shop => shop;
        public Transform Mushrooms => mushrooms;
        public Transform RainFruit => rainFruit;
        public SkywellWeatherZone RainZone => rainZone;
        public SkywellWeatherTarget RainTarget => rainTarget;
        public bool IsShopOpen { get; private set; }
        public bool InputBlocked => IsShopOpen || closedFrame == Time.frameCount;
        public bool FruitIsWet => rainFruit && rainZone && rainZone.IsRain && rainZone.Contains(rainFruit.position);
        public string Feedback => Time.time < feedbackUntil ? feedback : null;

        internal bool AllowsWeather(int station, SkywellToken token, bool delivered) =>
            !rainTarget || station != rainTarget.StationIndex ||
            delivered && Run.IsStarted && token == SkywellToken.Rain;

        internal bool TryInteract(CourtPlayer actor)
        {
            Point point = FindPoint(actor.FeetPosition);
            if (point == Point.None) return false;
            if (actor.IsSwimming || actor.IsExternallyDriven || actor.IsAutoPositioning ||
                !actor.TryGetComponent<CharacterController>(out var body) || !body.isGrounded)
            {
                Say("站稳后再交互。");
                return true;
            }

            if (point == Point.Shop)
            {
                if (Run.TryStart()) Say("第一天：先采菌菇，再试试用雨令催生雨露果。");
                IsShopOpen = true;
                commandFrame = Time.frameCount;
                return true;
            }

            if (!Run.IsStarted)
            {
                Say("先到花庭小铺开张。");
                return true;
            }

            if (point == Point.Mushrooms)
                Harvest(FlowerIngredient.Mushroom, ref mushroomsReadyAt, "采到菌菇 ×1。");
            else if (FruitIsWet)
                Harvest(FlowerIngredient.WeatherFruit, ref fruitReadyAt, "采到雨露果 ×1。");
            else
                Say("树下目前无雨。选雨令，长按 E 点击花庭的雨牌。");
            return true;
        }

        internal string Prompt(Vector3 feet)
        {
            if (!shop || Mathf.Abs(feet.y - shop.position.y) > .65f ||
                (feet - shop.position).sqrMagnitude > 225f) return null;
            if (Feedback != null) return Feedback;

            switch (FindPoint(feet))
            {
                case Point.Shop: return Run.IsStarted ? "E 打开花庭小铺" : "E 开始花庭经营";
                case Point.Mushrooms: return "E 采菌菇";
                case Point.RainFruit: return FruitIsWet ? "E 采雨露果" : "雨露果需要本地降雨 · 长按 E 点击雨牌";
                default: return Run.IsStarted ? "花庭小铺 · 找菌菇和雨露果，回店做菜" : "货已签收 · 到花庭小铺按 E 开始经营";
            }
        }

        internal void Tick(float deltaSeconds)
        {
            Run.Tick(deltaSeconds);
            if (!IsShopOpen || Keyboard.current == null) return;
            var keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame) CloseShop();
            else if (keyboard.digit1Key.wasPressedThisFrame) Cook(FlowerRecipe.MushroomBento);
            else if (keyboard.digit2Key.wasPressedThisFrame) Cook(FlowerRecipe.FruitBento);
            else if (keyboard.nKey.wasPressedThisFrame) NextDay();
        }

        internal void Cook(FlowerRecipe recipe)
        {
            if (!BeginShopCommand()) return;
            Say(Run.TryCook(recipe) ? "开始烹饪。" : "材料不足，或上一份餐还未交付。");
        }

        internal void Serve()
        {
            if (!BeginShopCommand()) return;
            int previousCash = Run.Cash;
            if (Run.TryServe()) Say($"已收款 +{Run.Cash - previousCash}。");
            else Say("选一道菜下锅，出餐后按 E 收款。");
        }

        internal void NextDay()
        {
            if (!BeginShopCommand()) return;
            if (!Run.TryNextDay())
            {
                Say("先把已下锅的餐做完并收款。");
                return;
            }

            mushroomsReadyAt = fruitReadyAt = Time.time;
            Say($"第 {Run.Day} 天 · 钱箱与食材已结转。");
        }

        internal void CloseShop()
        {
            if (!IsShopOpen) return;
            IsShopOpen = false;
            closedFrame = Time.frameCount;
        }

        internal void ResetMarket()
        {
            CloseShop();
            Run.Reset();
            mushroomsReadyAt = fruitReadyAt = feedbackUntil = 0f;
            feedback = null;
        }

        private void OnDisable() => CloseShop();

        private Point FindPoint(Vector3 feet)
        {
            if (Near(feet, shop)) return Point.Shop;
            if (Near(feet, mushrooms)) return Point.Mushrooms;
            return Near(feet, rainFruit) ? Point.RainFruit : Point.None;
        }

        private bool Near(Vector3 feet, Transform point) => point &&
            Mathf.Abs(feet.y - point.position.y) <= .65f &&
            (feet - point.position).sqrMagnitude <= interactionRadius * interactionRadius;

        private void Harvest(FlowerIngredient ingredient, ref float readyAt, string message)
        {
            if (Time.time < readyAt)
            {
                Say("食材正在长回来。");
                return;
            }

            if (!Run.TryHarvest(ingredient)) return;
            readyAt = Time.time + regrowSeconds;
            Say(message);
        }

        private bool BeginShopCommand()
        {
            if (!IsShopOpen || commandFrame == Time.frameCount) return false;
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
