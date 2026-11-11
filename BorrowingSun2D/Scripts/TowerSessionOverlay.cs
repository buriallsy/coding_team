using System;
using Godot;

namespace BorrowingSun2D;

internal enum TowerScreen { Intro, Playing, Finished }

public partial class TowerSessionOverlay : Control
{
    private Label kicker = null!;
    private Label title = null!;
    private Label summary = null!;
    private Label instructions = null!;
    private Label controls = null!;
    private Label confirmHint = null!;

    internal Button ActionButton { get; private set; } = null!;
    internal event Action? Accepted;

    public override void _Ready()
    {
        var content = GetNode<VBoxContainer>("SafeArea/Center/Card/Content");
        kicker = content.GetNode<Label>("Kicker");
        title = content.GetNode<Label>("Title");
        summary = content.GetNode<Label>("Summary");
        instructions = content.GetNode<Label>("Instructions");
        controls = content.GetNode<Label>("Controls");
        confirmHint = content.GetNode<Label>("ConfirmHint");
        ActionButton = content.GetNode<Button>("Action");
        ActionButton.Pressed += () => Accepted?.Invoke();

        if (DisplayServer.GetName() != "headless")
            Theme = new Theme { DefaultFont = TowerUiFont.Load() };
    }

    internal void Present(TowerScreen screen, int deliveredCount, bool tutorial = false)
    {
        if (screen == TowerScreen.Playing)
        {
            ActionButton.ReleaseFocus();
            Hide();
            return;
        }

        bool finished = screen == TowerScreen.Finished;
        kicker.Text = finished ? "跨 谷 完 成" : "天 井 跨 谷";
        title.Text = finished ? "花庭已收货" : "借晴";
        summary.Text = finished ? $"水果交付 {deliveredCount}/1" : "把一箱水果送到花庭";
        instructions.Text = finished
            ? "从西侧低岸重新出发，再借一趟晴。"
            : "借雨升塔，借风接桥。到北庭调镜施晴，再回塔转东，让日光照亮村庄。";
        controls.Text = "A / D 行走   ·   1 雨 / 2 风 / 3 晴   ·   E 施令   ·   G 发车 / 交付";
        controls.Visible = !finished;
        ActionButton.Text = finished ? "再玩一次" : "开始";
        confirmHint.Text = finished ? "R / Enter / Space 或点击按钮" : "Enter / Space 或点击按钮";
        if (tutorial)
        {
            kicker.Text = finished ? "教 学 完 成" : "谷 地 外 卖 店";
            instructions.Text = finished
                ? "签收完成。接下来从中央店铺出发，白天采集食材，夜晚接单营业。"
                : instructions.Text + "\n完成运货教学后，开始探索与经营。";
            ActionButton.Text = finished ? "进入第 1 天" : "开始教学";
            confirmHint.Text = finished ? "Enter / Space 进入第 1 天 · R 重练" : confirmHint.Text;
        }
        Show();
        ActionButton.GrabFocus();
    }
}
