using System;
using BorrowingSun.Market;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>IMGUI presentation only. All gameplay changes go through the module's guarded commands.</summary>
    internal sealed class SkywellMarketView
    {
        private readonly SkywellMarketModule owner;
        private MarketSession run => owner.Run;
        private SkywellController court => owner.Court;
        private GUIStyle textStyle, headingStyle, buttonStyle, mutedStyle;
        private Vector2 menuScroll, contentScroll;
        public SkywellMarketView(SkywellMarketModule owner) { this.owner = owner; }

        public void Draw()
        {
            if (run == null || !court || !court.Voyage || !court.Voyage.Delivered) return;
            EnsureStyles();
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            var previousEnabled = GUI.enabled;
            try
            {
                float scale = Mathf.Clamp(Screen.height / 900f, .65f, 1.6f);
                float width = Screen.width / scale, height = Screen.height / scale;
                GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
                if (!owner.IsShopOpen)
                {
                    if (court.ObservationInputBlocked || court.WeatherInputBlocked) return;
                    var status = new Rect(Mathf.Max(16, width / 2 - 220), 12, Mathf.Min(440, width - 32), 108);
                    Panel(status);
                    GUI.Label(new Rect(status.x + 12, status.y + 8, status.width - 24, 28), "花庭外卖店 · " + PhaseName(run.Phase), headingStyle);
                    GUI.Label(new Rect(status.x + 12, status.y + 41, status.width - 24, 58),
                        run.Phase == MarketPhase.Locked ? "运货已签收。到花庭店门短按 E，开始第一天。"
                        : "第 " + run.Day + " 天  ·  " + Mathf.CeilToInt(run.RemainingSeconds) + " 秒  ·  钱包 " + run.Cash + " 金币\n" + PhaseHint(run.Phase), textStyle);
                    return;
                }

                float panelWidth = Mathf.Min(760, width - 32), panelHeight = Mathf.Min(680, height - 32);
                var rect = new Rect((width - panelWidth) / 2, (height - panelHeight) / 2, panelWidth, panelHeight);
                Panel(rect);
                GUILayout.BeginArea(new Rect(rect.x + 22, rect.y + 16, rect.width - 44, rect.height - 32));
                GUILayout.Label("花庭外卖店 · " + PhaseName(run.Phase), headingStyle);
                GUILayout.Label("第 " + run.Day + " 天   钱包 " + run.Cash + "   累计营业额 " + run.TotalRevenue + "   "
                    + (run.KitchenUpgraded ? "厨房已升级" : "基础厨房"), mutedStyle);
                GUILayout.Space(12);
                DrawStock();
                GUILayout.Space(10);

                contentScroll = GUILayout.BeginScrollView(contentScroll);
                switch (run.Phase)
                {
                    case MarketPhase.Dusk: DrawDusk(); break;
                    case MarketPhase.Menu: DrawMenu(); break;
                    case MarketPhase.Business: DrawBusiness(); break;
                    case MarketPhase.Summary: DrawSummary(); break;
                    default: GUILayout.Label(PhaseHint(run.Phase), textStyle); break;
                }
                GUILayout.EndScrollView();
                if (owner.Feedback != null) GUILayout.Label(owner.Feedback, textStyle, GUILayout.MinHeight(48));
                GUILayout.Space(8);
                if (GUILayout.Button("返回场景（Esc）", buttonStyle, GUILayout.Height(32))) owner.DismissPanel();
                GUILayout.EndArea();
            }
            finally { GUI.matrix = previousMatrix; GUI.color = previousColor; GUI.enabled = previousEnabled; }
        }

        private void DrawStock()
        {
            string stock = "";
            foreach (IngredientId ingredient in Enum.GetValues(typeof(IngredientId)))
            {
                if (stock.Length > 0) stock += "   ";
                stock += SkywellMarketModule.IngredientName(ingredient) + " " + run.Stock(ingredient);
                int reserved = run.Reserved(ingredient);
                if (reserved > 0) stock += "（备菜 " + reserved + "）";
            }
            GUILayout.Label(stock, textStyle);
        }

        private void DrawDusk()
        {
            GUILayout.Label("天黑了。今晚先做什么？", headingStyle);
            var settings = owner.Settings;
            GUILayout.Label("直接返店：有 " + settings.BusinessSeconds.ToString("0") + " 秒营业时间。\n继续夜探：最多采集 "
                + settings.NightSeconds.ToString("0") + " 秒，之后营业时间为 " + settings.ShortBusinessSeconds.ToString("0")
                + " 秒。\n夜晚没有阳光；果树会长出月果，本地下雨时变成雨夜果。", textStyle);
            GUILayout.Space(18);
            if (GUILayout.Button("返回花庭 · 完整营业", buttonStyle, GUILayout.Height(48))) owner.ChooseEvening(false);
            if (GUILayout.Button("继续夜探 · 营业时间减半", buttonStyle, GUILayout.Height(48))) owner.ChooseEvening(true);
            GUILayout.Label("选择返店后仍需自己走到店门，路上不消耗营业时间。", mutedStyle);
        }

        private void DrawMenu()
        {
            GUILayout.Label("先定今晚的菜谱，再接单", headingStyle);
            GUILayout.Label("客人只点菜单中有食材的菜。两队分别预留食材，不会重复占用。", textStyle);
            menuScroll = GUILayout.BeginScrollView(menuScroll, GUILayout.MaxHeight(235));
            foreach (var recipe in run.Recipes)
            {
                bool selected = owner.IsOnMenu(recipe.Id);
                bool possible = run.CanMake(recipe.Id);
                var label = recipe.Name + "  ·  " + recipe.Price + " 金币  ·  "
                    + recipe.CookSeconds.ToString("0.#") + " 秒\n" + RecipeIngredients(recipe)
                    + (possible ? "" : "  ·  食材不足");
                bool choice = GUILayout.Toggle(selected, label, buttonStyle, GUILayout.MinHeight(52));
                if (choice != selected) owner.ToggleMenuItem(recipe.Id);
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("确认菜单并开始营业", buttonStyle, GUILayout.Height(42))) owner.OpenBusiness();
            GUI.enabled = !run.KitchenUpgraded && run.Cash >= owner.Settings.UpgradeCost;
            if (GUILayout.Button(owner.Settings.UpgradeCost + " 金币升级厨房 · 烹饪用时减少 "
                + Mathf.RoundToInt((1 - owner.Settings.UpgradeTimeMultiplier) * 100) + "%", buttonStyle, GUILayout.Height(36))) owner.UpgradeKitchen();
            GUI.enabled = true;
            if (GUILayout.Button("今晚休息，保留食材进入次日", buttonStyle, GUILayout.Height(32))) owner.NextDay();
        }

        private void DrawBusiness()
        {
            GUILayout.Label("还可营业 " + Mathf.CeilToInt(run.RemainingSeconds) + " 秒 · 今晚已收 " + run.TonightRevenue + " 金币", headingStyle);
            GUILayout.Label("按 1 / 2 为对应队伍放入食材，完成后再按同一键送餐。两台烹饪机可以同时工作。", textStyle);
            GUILayout.Space(12);
            for (int slot = 0; slot < 2; slot++)
            {
                var order = run.GetOrder(slot);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("第 " + (slot + 1) + " 队", headingStyle);
                if (order == null) GUILayout.Label("暂时没有食材足够的订单。", textStyle);
                else
                {
                    GUILayout.Label(order.Recipe.Name + " · " + order.Recipe.Price + " 金币", textStyle);
                    GUILayout.Label(RecipeIngredients(order.Recipe), mutedStyle);
                    string action = order.Stage == OrderStage.Waiting ? "放入配方食材，开始烹饪"
                        : order.Stage == OrderStage.Cooking ? "烹饪中 · " + order.RemainingSeconds.ToString("0.0") + " 秒"
                        : "送餐并收款";
                    GUI.enabled = order.Stage != OrderStage.Cooking;
                    if (GUILayout.Button((slot + 1) + " · " + action, buttonStyle, GUILayout.Height(38))) owner.SelectSlot(slot);
                    GUI.enabled = true;
                }
                GUILayout.EndVertical();
            }
            GUILayout.Space(8);
            if (GUILayout.Button("提前打烊 · 未送餐订单退还食材", buttonStyle, GUILayout.Height(36))) owner.CloseDay();
        }

        private void DrawSummary()
        {
            GUILayout.Label("今天辛苦了", headingStyle);
            GUILayout.Space(12);
            GUILayout.Label("今晚收入：" + run.LastNightRevenue + " 金币\n当前钱包：" + run.Cash + " 金币\n累计营业额：" + run.TotalRevenue + " 金币", textStyle);
            GUILayout.Space(14);
            GUILayout.Label(run.KitchenUpgraded ? "厨房已升级，之后每天都能更快备菜。"
                : run.Cash >= owner.Settings.UpgradeCost ? "你已攒够厨房升级费。下次开店前可花 " + owner.Settings.UpgradeCost + " 金币升级。"
                : "再攒 " + Math.Max(0, owner.Settings.UpgradeCost - run.Cash) + " 金币，就能升级厨房，让备菜快起来。", textStyle);
            GUILayout.Label("没有天数期限。剩余食材、钱和升级留到明天；修镜、签收及世界机关状态也会保留。", mutedStyle);
            GUILayout.Space(20);
            if (GUILayout.Button("进入第 " + (run.Day + 1) + " 天（N）", buttonStyle, GUILayout.Height(48))) owner.NextDay();
        }

        private static string RecipeIngredients(RecipeDefinition recipe)
        {
            string value = "";
            foreach (var amount in recipe.Ingredients)
            { if (value.Length > 0) value += " + "; value += SkywellMarketModule.IngredientName(amount.Ingredient) + " × " + amount.Quantity; }
            return value;
        }
        private static string PhaseName(MarketPhase phase)
        {
            switch (phase)
            {
                case MarketPhase.Locked: return "等你开店";
                case MarketPhase.Day: return "白天采集";
                case MarketPhase.Dusk: return "黄昏选择";
                case MarketPhase.NightForage: return "夜间采集";
                case MarketPhase.Returning: return "返回花庭";
                case MarketPhase.Menu: return "准备菜单";
                case MarketPhase.Business: return "营业中";
                default: return "今日结算";
            }
        }
        private static string PhaseHint(MarketPhase phase)
        {
            switch (phase)
            {
                case MarketPhase.Day: return "采集食材，等黄昏选择返店或夜探。";
                case MarketPhase.Dusk: return "在固定地面站稳后选择；店门 E 可重开面板。";
                case MarketPhase.NightForage: return "采集月果或雨夜果；店门 E 可提前返店。";
                case MarketPhase.Returning: return "走回花庭店门，短按 E。营业计时还没开始。";
                case MarketPhase.Menu: return "店门 E 配置菜谱；没有食材也可以休息到次日。";
                case MarketPhase.Business: return "店门 E 返回厨房。关闭面板仍继续营业计时。";
                case MarketPhase.Summary: return "店门 E 查看收入，并进入次日。";
                default: return "先签收货物，再到花庭店门短按 E。";
            }
        }
        private void EnsureStyles()
        {
            if (textStyle != null) return;
            var font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            textStyle = new GUIStyle(GUI.skin.label) { font = font ? font : GUI.skin.font, fontSize = 17, wordWrap = true };
            textStyle.normal.textColor = new Color(.92f, .96f, .96f);
            headingStyle = new GUIStyle(textStyle) { fontSize = 23, fontStyle = FontStyle.Bold };
            mutedStyle = new GUIStyle(textStyle) { fontSize = 14 };
            mutedStyle.normal.textColor = new Color(.75f, .82f, .85f);
            buttonStyle = new GUIStyle(GUI.skin.button) { font = textStyle.font, fontSize = 17, wordWrap = true };
        }
        private static void Panel(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.045f, .075f, .085f, .97f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
