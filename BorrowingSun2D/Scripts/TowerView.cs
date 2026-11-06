using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

public partial class TowerView : Node2D
{
    private static readonly Color Ink = new("eaf0ee");
    private static readonly Color Muted = new("98aeb1");
    private static readonly Color Line = new("40565a");
    private static readonly Color Stone = new("2b393d");
    private static readonly Color Rain = new("77bad9");
    private static readonly Color Wind = new("91d1bf");
    private static readonly Color PlayerColor = new("ffd88b");

    private TowerSlice slice = null!;
    private Font font = null!;

    public override void _Ready()
    {
        slice = GetParent<TowerSlice>();
        if (DisplayServer.GetName() == "headless")
        {
            Hide();
            SetProcess(false);
            return;
        }

        if (System.OperatingSystem.IsMacOS())
        {
            var localFont = new FontFile();
            if (localFont.LoadDynamicFont("/System/Library/Fonts/STHeiti Medium.ttc") == Error.Ok)
            {
                font = localFont;
                return;
            }
        }

        font = new SystemFont
        {
            FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "PingFang SC", "Arial" },
        };
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (font == null) return;
        TowerState state = slice.State;

        DrawRect(new Rect2(0, 0, 1280, 720), new Color("142023"));
        for (int x = 56; x <= 1224; x += 32)
            DrawLine(new Vector2(x, 147), new Vector2(x, 637), new Color("1c2a2c"));
        for (int y = 157; y <= 637; y += 24)
            DrawLine(new Vector2(56, y), new Vector2(1224, y), new Color("1c2a2c"));

        DrawLine(new Vector2(56, 137), new Vector2(1224, 137), Line);
        Write("借晴  /  塔台灰盒", 56, 74, 33, Ink);
        Write("01   从西侧低岸登上塔台，升高并转向，步行抵达采光庭。", 57, 109, 18, Muted);
        Write("侧视剖面   /   机械与接岸", 966, 74, 18, Muted);

        DrawGuide(slice.CourtFloor.Position.Y, "+6 m");
        DrawGuide(slice.EntryFloor.Position.Y, "0 m");
        DrawBank(slice.EntryFloor, "西侧低岸", "起点", slice.State.DockedPort == Port.Entry);
        DrawBank(slice.CourtFloor, "北侧高岸 · 采光庭", "步行到标记处", slice.State.DockedPort == Port.SunCourt);
        DrawGoal();
        DrawReservedShore();
        DrawTower(state);
        DrawPerson(slice.Player);
        DrawReadout(state);

        DrawLine(new Vector2(56, 637), new Vector2(1224, 637), Line);
        Write(slice.Status, 56, 670, 23, slice.Completed ? Wind : Ink);
        Write("A / D 或 ← / →  行走     1 雨 / 2 风  选令     E  施令     Q  收令     F  放水     R  重置", 56, 699, 17, Muted);
    }

    private void DrawGuide(float y, string text)
    {
        DrawDashedLine(new Vector2(486, y), new Vector2(1218, y), Line, 1f, 7f);
        Write(text, 872, y - 9f, 16, Muted);
    }

    private void DrawBank(Rect2 surface, string title, string subtitle, bool connected)
    {
        Vector2 corner = surface.Position;
        Vector2 edge = new(surface.End.X, corner.Y);
        float extent = Mathf.Min(76f, 637f - corner.Y);
        DrawColoredPolygon(new[]
        {
            corner, edge, edge + new Vector2(-22, extent * .62f), edge + new Vector2(-88, extent),
            corner + new Vector2(0, extent),
        }, Stone);
        DrawRect(surface, new Color("46575a"));
        DrawLine(corner, edge, connected ? Wind : Muted, 2f);
        Write(title, corner.X + 16f, corner.Y - 48f, 21, Ink);
        Write(subtitle, corner.X + 16f, corner.Y - 23f, 15, Muted);
    }

    private void DrawGoal()
    {
        Vector2 mark = slice.Goal;
        DrawLine(mark, mark + new Vector2(0, -38), Wind, 2f);
        DrawColoredPolygon(new[] { mark + new Vector2(0, -38), mark + new Vector2(19, -32), mark + new Vector2(0, -25) },
            slice.Completed ? PlayerColor : Wind);
        DrawLine(mark + new Vector2(-12, -1), mark + new Vector2(12, -1), Wind, 3f);
        if (slice.Completed) Write("已抵达", mark.X + 27f, mark.Y - 22f, 17, Wind);
    }

    private void DrawReservedShore()
    {
        const float y = 300f;
        DrawRect(new Rect2(963, y, 257, 23), new Color("223134"));
        DrawDashedLine(new Vector2(963, y), new Vector2(1220, y), Line, 2f, 10f);
        Write("东岸 · 后续场地", 974, y - 16f, 17, Muted);
    }

    private void DrawTower(TowerState state)
    {
        Rect2 deck = slice.Deck;
        float top = deck.Position.Y;
        DrawRect(new Rect2(630, 257, 61, 356), new Color("243336"));
        DrawLine(new Vector2(644, 253), new Vector2(644, 613), Line, 3f);
        DrawLine(new Vector2(677, 253), new Vector2(677, 613), Line, 3f);
        DrawRect(new Rect2(602, 612, 117, 17), Stone);

        float tankTop = Mathf.Lerp(335f, 519f, state.Height / TowerMechanism.TravelMetres);
        DrawLine(new Vector2(824, 253), new Vector2(824, 613), Line, 4f);
        DrawCircle(new Vector2(790, 258), 11f, Line);
        DrawCircle(new Vector2(824, 258), 11f, Line);
        DrawLine(new Vector2(749, top + 15f), new Vector2(790, 258), Muted, 1.5f, true);
        DrawLine(new Vector2(790, 258), new Vector2(824, 258), Muted, 1.5f);
        DrawLine(new Vector2(824, 258), new Vector2(824, tankTop), Muted, 1.5f);
        DrawRect(new Rect2(800, tankTop, 48, 64), new Color("1c3037"));
        float waterHeight = 58f * state.Water / TowerMechanism.Capacity;
        DrawRect(new Rect2(803, tankTop + 61f - waterHeight, 42, waterHeight), new Color("3f8099"));
        DrawRect(new Rect2(800, tankTop, 48, 64), Rain, false, 1.5f);
        Write("雨水箱", 800, tankTop + 84f, 15, Muted);

        DrawRect(deck, new Color("536266"));
        DrawLine(deck.Position, new Vector2(deck.End.X, top), Ink, 2f);
        if (state.Latched)
        {
            DrawRect(new Rect2(627, top + 16f, 17f, 8f), PlayerColor);
            DrawRect(new Rect2(677, top + 16f, 17f, 8f), PlayerColor);
        }

        DrawBridge(slice.BridgeHinge, slice.BridgeCollisionEnabled);
        if (slice.BridgeCollisionEnabled) DrawBridge(slice.BridgeExtension, true);
        if (state.IsTurning)
        {
            float reach = 22f + 36f * Mathf.Sin(Mathf.Pi * state.Heading / TowerMechanism.SunCourtHeading);
            Vector2 joint = new(deck.Position.X - 8f, top - 6f);
            DrawDashedLine(joint, joint + new Vector2(-reach, -12f), Wind, 1.5f, 5f);
        }

        Vector2 station = new(deck.GetCenter().X + 43f, top - 37f);
        Color token = slice.SelectedWeather == Weather.Rain ? Rain : Wind;
        DrawLine(station, station + new Vector2(0, 35f), Muted, 2f);
        DrawCircle(station, 16f, new Color("213135"));
        DrawCircle(station, 16f, token, false, slice.AtStation ? 3f : 1f, true);
        Write(slice.AtStation ? "E" : "令", station.X - 6f, station.Y + 6f, 16, token);

        if (state.ActiveWeather != Weather.Rain) return;
        for (int i = 0; i < 18; i++)
        {
            float x = 578f + i * 13f;
            float y = 236f + Mathf.PosMod((float)Time.GetTicksMsec() * .24f + i * 43f, top - 236f);
            DrawLine(new Vector2(x, y), new Vector2(x - 3f, y + 12f), new Color(.49f, .73f, .85f, .48f), 1.5f);
        }
    }

    private void DrawBridge(Rect2 bridge, bool connected)
    {
        DrawRect(bridge, connected ? new Color("4a736d") : new Color("405456"));
        DrawLine(bridge.Position, new Vector2(bridge.End.X, bridge.Position.Y), connected ? Wind : Muted, 2f);
    }

    private void DrawPerson(SlicePlayer player)
    {
        Vector2 feet = player.Feet;
        DrawLine(feet + new Vector2(-4, -10), feet + new Vector2(-5, -1), PlayerColor, 3f, true);
        DrawLine(feet + new Vector2(4, -10), feet + new Vector2(5, -1), PlayerColor, 3f, true);
        DrawRect(new Rect2(feet + new Vector2(-7, -23), new Vector2(14, 14)), PlayerColor);
        DrawCircle(feet + new Vector2(0, -28), 6f, PlayerColor);
        DrawCircle(feet + new Vector2(3f * player.Facing, -29f), 1.4f, new Color("273237"));
    }

    private void DrawReadout(TowerState state)
    {
        DrawRect(new Rect2(956, 359, 268, 223), new Color("1b2a2d"));
        DrawLine(new Vector2(956, 359), new Vector2(1224, 359), Line);
        Write("塔台状态", 975, 391, 20, Ink);
        Write($"存水   {state.Water:0.0} / {TowerMechanism.Capacity:0}", 975, 426, 17, Rain);
        Write($"台高   +{state.Height:0.0} m", 975, 455, 17, Ink);
        Write(state.Latched ? "高位锁 · 已扣合" : "高位锁 · 待升高", 975, 484, 17, state.Latched ? PlayerColor : Muted);
        Write(state.DockedPort switch
        {
            Port.Entry => "接岸   西侧低岸",
            Port.SunCourt => "接岸   北侧采光庭",
            _ => "接岸   未接通",
        }, 975, 553, 17, slice.BridgeCollisionEnabled ? Wind : Muted);

        Vector2 pivot = new(1161, 460);
        DrawCircle(pivot, 33f, Line, false, 1.5f, true);
        DrawCircle(pivot, 4f, Muted);
        Vector2 tip = pivot + Vector2.Left.Rotated(Mathf.DegToRad(state.Heading)) * 30f;
        DrawLine(pivot, tip, Wind, 3f, true);
        DrawCircle(tip, 4f, Wind);
        Write("北", pivot.X - 7f, pivot.Y - 41f, 14, Muted);
        Write("西", pivot.X - 50f, pivot.Y + 5f, 14, Muted);
    }

    private void Write(string text, float x, float y, int size, Color color) =>
        DrawString(font, new Vector2(x, y), text, HorizontalAlignment.Left, -1f, size, color);
}
