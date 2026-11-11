using System;
using System.Collections.Generic;
using System.Linq;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

internal sealed class MarketPanel
{
    private readonly MarketPlay2D play;
    private readonly Control root;
    private readonly Button returnButton;
    private readonly Dictionary<string, CheckBox> menuChoices = new();
    private readonly Label[] orders = new Label[TakeawayShop.WindowCount];
    private readonly Button[] actions = new Button[TakeawayShop.WindowCount];
    private MarketPhase shownPhase;
    private Control? modal;
    private Label? heading;
    private Label? stock;
    private Label? till;
    private Label? receipt;
    private Button? openButton;
    private MarketRun Run => play.Run;

    internal MarketPanel(Node scene, MarketPlay2D play)
    {
        this.play = play;
        var layer = new CanvasLayer { Layer = 6 };
        scene.AddChild(layer);
        root = new Control { Name = "MarketPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (DisplayServer.GetName() != "headless")
            root.Theme = new Theme { DefaultFont = TowerUiFont.Load(), DefaultFontSize = 18 };

        returnButton = AddButton(root, "返店选菜", () => Run.OpenMenu(), "ReturnToShop");
        returnButton.FocusMode = Control.FocusModeEnum.None;
        returnButton.Position = new Vector2(1042, 89);
        returnButton.Size = new Vector2(174, 38);
        returnButton.Hide();
    }

    internal void Refresh()
    {
        returnButton.Visible = Run.Phase == MarketPhase.NightForage;
        if (shownPhase != Run.Phase)
        {
            shownPhase = Run.Phase;
            BuildPhase();
        }
        if (shownPhase == MarketPhase.Menu && openButton != null)
            openButton.Disabled = !menuChoices.Values.Any(choice => choice.ButtonPressed);

        if (shownPhase != MarketPhase.Business || heading == null || stock == null || till == null) return;
        heading.Text = $"第 {Run.Day} 天  ·  营业剩 {Mathf.CeilToInt(Run.Remaining)} 秒";
        stock.Text = "库存    " + MarketText.Stock(Run.Bag);
        till.Text = $"本晚实收 {Run.TonightRevenue}  ·  钱箱 {Run.Balance}  ·  累计营业额 {Run.TotalRevenue} 金币";
        for (int slot = 0; slot < orders.Length; slot++)
        {
            CustomerOrder? order = Run.Shop.OrderAt(slot);
            orders[slot].Text = order == null ? "暂无能做的订单" :
                $"{order.Recipe.Name}  ·  {order.Recipe.Price} 金币\n{MarketText.Ingredients(order.Recipe)}";
            actions[slot].Text = order?.State switch
            {
                OrderState.Waiting => "投料并烹饪",
                OrderState.Cooking => $"烹饪中  {order.CookRemaining:0.0} 秒",
                OrderState.Ready => "送餐并收款",
                _ => "窗口空闲",
            };
            actions[slot].Disabled = order == null || order.State == OrderState.Cooking;
        }
    }

    private void BuildPhase()
    {
        if (modal != null)
        {
            root.RemoveChild(modal);
            modal.QueueFree();
            modal = null;
        }
        menuChoices.Clear();
        openButton = null;
        if (shownPhase is MarketPhase.Tutorial or MarketPhase.Day or MarketPhase.NightForage) return;

        VBoxContainer body = BuildCard();
        switch (shownPhase)
        {
            case MarketPhase.Dusk:
                AddLabel(body, "天黑了，今晚怎么安排？", 30);
                AddLabel(body, "夜间可以采到月果和雨夜果。出门会用掉一半营业时间。");
                AddButton(body, $"回店经营 · {MarketRun.BusinessSeconds} 秒", () => Run.ChooseNight(false), "DirectShop");
                AddButton(body, $"先探索 {MarketRun.DaySeconds / 2} 秒，再营业 {MarketRun.BusinessSeconds / 2} 秒",
                    () => Run.ChooseNight(true), "NightForage");
                break;
            case MarketPhase.Menu:
                AddLabel(body, "今晚的菜单", 30);
                AddLabel(body, MarketText.Stock(Run.Bag));
                AddLabel(body, "勾选要卖的菜。确认开店后，客人才会按库存点餐。");
                foreach (Recipe recipe in RecipeBook.All)
                {
                    var option = new CheckBox
                    {
                        Name = "Recipe_" + recipe.Id,
                        Disabled = !Run.Bag.Covers(recipe.Ingredients),
                    };
                    body.AddChild(option);
                    menuChoices.Add(recipe.Id, option);
                }
                AddKitchenOffer(body);
                openButton = AddButton(body, "确认菜单并开店", () => Run.OpenShop(menuChoices
                    .Where(item => item.Value.ButtonPressed).Select(item => item.Key)), "OpenShop");
                AddButton(body, "今晚休息，进入下一天", () => Run.EndEvening(), "Rest");
                break;
            case MarketPhase.Business:
                heading = AddLabel(body, "", 27);
                stock = AddLabel(body, "");
                till = AddLabel(body, "");
                AddLabel(body, "两口灶可以同时开火。把餐送到对应窗口后收款，该队才接下一单。");
                var windows = new HBoxContainer();
                windows.AddThemeConstantOverride("separation", 18);
                body.AddChild(windows);
                for (int i = 0; i < orders.Length; i++)
                {
                    int slot = i;
                    var window = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
                    windows.AddChild(window);
                    AddLabel(window, $"窗口 {i + 1}", 22);
                    orders[i] = AddLabel(window, "");
                    actions[i] = AddButton(window, "", () => HandleOrder(slot), $"Order{slot}");
                }
                receipt = AddLabel(body, "送出第一份餐后，钱会进钱箱。", 19);
                receipt.AddThemeColorOverride("font_color", new Color("f5cc7c"));
                AddButton(body, "打烊，结算到下一天", () => Run.EndEvening(), "CloseShop");
                break;
        }
    }

    private void AddKitchenOffer(VBoxContainer body)
    {
        var funds = AddLabel(body, "");
        var upgrade = new Button { Name = "UpgradeKitchen", CustomMinimumSize = new Vector2(0, 36) };
        body.AddChild(upgrade);
        upgrade.Pressed += () =>
        {
            if (Run.TryUpgradeKitchen()) RefreshOffer();
        };
        RefreshOffer();

        void RefreshOffer()
        {
            funds.Text = $"钱箱 {Run.Balance} 金币  ·  累计营业额 {Run.TotalRevenue}  ·  昨晚实收 {Run.LastNightRevenue}";
            upgrade.Text = Run.Shop.KitchenUpgraded
                ? "双口快灶已装好 · 两口灶都已提速"
                : $"改善小店 · 换双口快灶  {MarketRun.KitchenUpgradeCost} 金币  ·  烹饪耗时减少 {TakeawayShop.UpgradeTimeReductionPercent}%";
            upgrade.Disabled = Run.Shop.KitchenUpgraded || Run.Balance < MarketRun.KitchenUpgradeCost;
            foreach (Recipe recipe in RecipeBook.All)
                menuChoices[recipe.Id].Text =
                    $"{recipe.Name}   {MarketText.Ingredients(recipe)}   {Run.Shop.CookDuration(recipe):0.#} 秒 / {recipe.Price} 金币";
        }
    }

    private bool HandleOrder(int slot)
    {
        if (Run.Shop.OrderAt(slot) is not { State: OrderState.Ready } order) return Run.TryCook(slot);
        if (!Run.TryServe(slot)) return false;
        if (receipt != null) receipt.Text = $"+{order.Recipe.Price} 金币 · 窗口 {slot + 1} 已收款";
        return true;
    }

    private VBoxContainer BuildCard()
    {
        var frame = new Control { Name = "MarketDialog" };
        root.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var scrim = new ColorRect { Color = new Color("101719d9"), MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddChild(scrim);
        scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer();
        frame.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(800, 0) };
        center.AddChild(card);
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("1b292b"),
            ContentMarginLeft = 26, ContentMarginRight = 26,
            ContentMarginTop = 20, ContentMarginBottom = 20,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        });
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        card.AddChild(body);
        modal = frame;
        return body;
    }

    private static Label AddLabel(Control parent, string text, int size = 18)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(label);
        return label;
    }

    private Button AddButton(Control parent, string text, Func<bool> command, string name)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 36) };
        parent.AddChild(button);
        button.Pressed += () =>
        {
            if (command()) play.SynchronizePhase();
        };
        return button;
    }
}
