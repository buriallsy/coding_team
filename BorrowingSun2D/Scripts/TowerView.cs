using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

public partial class TowerView : Node2D
{
    private static readonly Color Background = new("142023");
    private static readonly Color Ink = new("eaf0ee");
    private static readonly Color Muted = new("98aeb1");
    private static readonly Color Line = new("40565a");
    private static readonly Color Stone = new("2b393d");
    private static readonly Color Rain = new("77bad9");
    private static readonly Color Wind = new("91d1bf");
    private static readonly Color PlayerColor = new("ffd88b");
    private static readonly Color Sunny = new("f3bd62");

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

        DrawRect(new Rect2(0, 0, 1280, 720), Background);
        for (int x = 56; x <= 1224; x += 32)
            DrawLine(new Vector2(x, 147), new Vector2(x, 637), new Color("1c2a2c"));
        for (int y = 157; y <= 637; y += 24)
            DrawLine(new Vector2(56, y), new Vector2(1224, y), new Color("1c2a2c"));

        DrawGuide(slice.Rig.CourtFloor.Position.Y, "+6 m");
        DrawGuide(slice.Rig.EntryFloor.Position.Y, "0 m");
        DrawBank(slice.Rig.EntryFloor, "西侧低岸", "起点", state.DockedPort == Port.Entry);
        DrawBank(slice.Rig.CourtFloor, "北侧高岸 · 采光庭", "途中站", state.DockedPort == Port.SunCourt);
        DrawBank(slice.Rig.EastFloor, "东侧高岸 · 货轨", "向右登车", state.DockedPort == Port.East);
        DrawFlag(slice.Rig.SunCourtMarker, slice.ReachedSunCourt);
        DrawFlag(slice.Rig.Goal, slice.Completed, showReachedLabel: false);
        DrawTower(state);
        DrawOptics();
        DrawCargo(state);
        DrawPerson(slice.Player);
        DrawReadout(state);

        DrawRect(new Rect2(0, 0, 1280, 137), Background);
        DrawLine(new Vector2(56, 137), new Vector2(1224, 137), Line);
        Write("借晴  /  塔台灰盒", 56, 74, 33, Ink);
        Write("第 03 轮   北庭借光 · 雨水转作配重 · 登车送水果到花庭", 57, 109, 18, Muted);
        Write("侧视剖面   /   借光与交付", 966, 74, 18, Muted);

        DrawLine(new Vector2(56, 637), new Vector2(1224, 637), Line);
        Write(slice.Status, 56, 670, 23, slice.DeliveredCount > 0 ? Wind : Ink);
        Write("A/D 或 ←/→ 行走   1 雨 / 2 风 / 3 晴   E 施令   Shift+E 风反向   Q 收令   F 放水   G 发车 / 交付   R 重置", 56, 699, 16, Muted);
    }

    private void DrawGuide(float y, string text)
    {
        DrawDashedLine(new Vector2(486, y), new Vector2(1218, y), Line, 1f, 7f);
        Write(text, 806, y - 9f, 16, Muted);
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

    private void DrawFlag(Vector2 mark, bool reached, bool showReachedLabel = true)
    {
        DrawLine(mark, mark + new Vector2(0, -38), Wind, 2f);
        DrawColoredPolygon(new[] { mark + new Vector2(0, -38), mark + new Vector2(19, -32), mark + new Vector2(0, -25) },
            reached ? PlayerColor : Wind);
        DrawLine(mark + new Vector2(-12, -1), mark + new Vector2(12, -1), Wind, 3f);
        if (reached && showReachedLabel) Write("已抵达", mark.X + 42f, mark.Y - 22f, 17, Wind);
    }

    private void DrawTower(TowerState state)
    {
        Rect2 deck = slice.Rig.Deck;
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

        DrawBridge(slice.Rig.BridgeHinge, slice.Rig.BridgeCollisionEnabled);
        DrawBridge(slice.Rig.EastBridgeHinge, slice.Rig.EastBridgeCollisionEnabled);
        if (slice.Rig.BridgeCollisionEnabled) DrawBridge(slice.Rig.BridgeExtension, true);
        if (slice.Rig.EastBridgeCollisionEnabled) DrawBridge(slice.Rig.EastBridgeExtension, true);
        if (state.IsTurning)
        {
            float eastProgress = Mathf.InverseLerp(TowerMechanism.SunCourtHeading, TowerMechanism.EastHeading, state.Heading);
            float side = Mathf.Lerp(-1f, 1f, Mathf.Clamp(eastProgress, 0f, 1f));
            Vector2 joint = new(deck.GetCenter().X + side * 106f, top - 7f);
            DrawDashedLine(joint, joint + new Vector2(side * 46f, -12f), Wind, 1.5f, 5f);
        }

        Vector2 station = new(deck.GetCenter().X + 43f, top - 37f);
        Color token = slice.SelectedWeather switch { Weather.Rain => Rain, Weather.Wind => Wind, _ => Sunny };
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

    private void DrawOptics()
    {
        SunCourtOptics2D optics = slice.Optics;
        Rect2 stop = optics.BeamStopBounds;
        DrawRect(stop, new Color("303539"));
        DrawRect(stop, Line, false, 1.5f);
        Write("遮光岩", stop.Position.X - 2f, stop.End.Y + 17f, 14, Muted);

        LightPath2D path = optics.Path;
        DrawLight(path.Incoming);
        DrawLight(path.FirstReflection);
        DrawLight(path.SecondReflection);

        DrawCircle(optics.SunOrigin, 6f, optics.IsSunny ? Sunny : Line);
        Vector2 collector = SunCourtOptics2D.ToScene(optics.Collector.Center);
        Vector2 relay = SunCourtOptics2D.ToScene(optics.Relay.Center);
        Vector2 receiver = SunCourtOptics2D.ToScene(optics.Receiver.Center);
        DrawLine(collector, optics.StationPosition, Line, 3f);
        DrawLine(relay, new Vector2(relay.X, slice.Rig.Deck.Position.Y), Line, 2f);
        DrawLine(receiver, new Vector2(receiver.X, slice.Rig.EastFloor.Position.Y), Line, 3f);
        DrawCircle(receiver, 22f, optics.ReceiverLit ? Sunny : Line, false, 1.5f, true);
        DrawSurface(optics.Collector, optics.IsCalibrated ? Wind : Muted);
        DrawSurface(optics.Relay, optics.TowerHit ? Sunny : Muted);
        DrawSurface(optics.Receiver, optics.ReceiverLit ? Sunny : Muted);
        Write("采日镜", collector.X - 27f, collector.Y - 20f, 16, Ink);
        if (slice.AtSolarStation) Write("E 施令", collector.X - 22f, collector.Y + 29f, 15, Wind);
        Write("塔镜", relay.X - 15f, relay.Y - 16f, 15, Muted);
        Write("接光盘", receiver.X - 28f, receiver.Y - 28f, 16, Ink);
    }

    private void DrawLight(LightSegment2D? segment)
    {
        if (segment is not { } ray) return;
        Vector2 from = SunCourtOptics2D.ToScene(ray.From);
        Vector2 to = SunCourtOptics2D.ToScene(ray.To);
        DrawLine(from, to, new Color(Sunny, .24f), 8f, true);
        DrawLine(from, to, Sunny, 2f, true);
        if (ray.Hit == LightHit2D.Obstacle) DrawCircle(to, 4f, Sunny, false, 1.5f, true);
    }

    private void DrawSurface(OpticalSurface2D surface, Color color)
    {
        Vector2 center = SunCourtOptics2D.ToScene(surface.Center);
        Vector2 normal = SunCourtOptics2D.ToScene(surface.Normal);
        Vector2 halfSpan = new Vector2(-normal.Y, normal.X) * surface.HalfLength;
        DrawLine(center - halfSpan, center + halfSpan, color, 4f, true);
        DrawCircle(center, 3f, Ink);
    }

    private void DrawCargo(TowerState state)
    {
        CargoTrack2D track = slice.CargoTrack;
        Rect2 carrier = track.CarrierFloor;
        Rect2 delivery = track.DeliveryFloor;
        float center = carrier.GetCenter().X;
        bool settled = slice.CargoPhase is CargoPhase.Arrived or CargoPhase.Delivered;
        bool ready = slice.CargoPhase == CargoPhase.Ready && slice.Optics.ReceiverLit;
        Color dock = ready || settled || slice.CargoPhase == CargoPhase.Travelling ? Wind : Muted;

        Vector2 rail = track.Departure + new Vector2(-22f, 21f);
        DrawDashedLine(rail, track.Terminal + new Vector2(21f, 21f), Line, 2f, 7f);
        DrawRect(new Rect2(delivery.Position + new Vector2(8f, delivery.Size.Y),
            new Vector2(delivery.Size.X - 16f, 53f)), Stone);
        DrawRect(delivery, new Color("46575a"));
        DrawLine(delivery.Position, new Vector2(delivery.End.X, delivery.Position.Y), settled ? Wind : Muted, 2f);
        DrawFlag(track.Goal, slice.DeliveredCount > 0, showReachedLabel: false);
        Write("花庭", track.Goal.X - 17f, track.Goal.Y - 73f, 17, Ink);
        if (slice.CargoPhase == CargoPhase.Arrived)
            Write("G 交付", track.Goal.X - 22f, track.Goal.Y - 53f, 15, Wind);

        DrawRect(carrier, new Color("436464"));
        DrawLine(carrier.Position, new Vector2(carrier.End.X, carrier.Position.Y), dock, 2f);
        DrawCircle(new Vector2(carrier.Position.X + 11f, carrier.End.Y + 3f), 4f, dock);
        DrawCircle(new Vector2(carrier.End.X - 11f, carrier.End.Y + 3f), 4f, dock);

        Rect2 weight = new(center + 7f, carrier.End.Y + 14f, 20f, 24f);
        DrawLine(new Vector2(weight.GetCenter().X, carrier.End.Y), new Vector2(weight.GetCenter().X, weight.Position.Y), Line, 2f);
        if (slice.CargoPhase == CargoPhase.Loading && slice.Optics.ReceiverLit && state.DockedPort == Port.East)
        {
            Vector2 pipeBend = new(920f, weight.GetCenter().Y);
            DrawDashedLine(new Vector2(848f, 538f), pipeBend, Rain, 1.5f, 5f);
            DrawDashedLine(pipeBend, new Vector2(weight.Position.X, pipeBend.Y), Rain, 1.5f, 5f);
        }

        DrawRect(weight, Background);
        float fill = (weight.Size.Y - 4f) * state.Counterweight / CargoJourney.CounterweightRequired;
        DrawRect(new Rect2(weight.Position.X + 2f, weight.End.Y - 2f - fill, weight.Size.X - 4f, fill), Rain);
        DrawRect(weight, Line, false, 1.5f);

        if (slice.HasFruit)
        {
            Rect2 crate = new(center - 26f, carrier.Position.Y - 17f, 15f, 15f);
            DrawRect(crate, new Color("885836"));
            DrawRect(crate, Sunny, false, 1.5f);
            DrawCircle(crate.Position + new Vector2(4f, 4f), 2.5f, Sunny);
            DrawCircle(crate.Position + new Vector2(10f, 4f), 2.5f, Sunny);
        }

        if (slice.CargoPhase is CargoPhase.WaitingForLight or CargoPhase.Loading or CargoPhase.Ready)
            Write(ready && slice.IsAboardCargo ? "G 发车" : "货车", center - 19f, carrier.Position.Y - 47f, 15, dock);
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
        DrawRect(new Rect2(956, 359, 268, 266), new Color("1b2a2d"));
        DrawLine(new Vector2(956, 359), new Vector2(1224, 359), Line);
        Write("塔台与货运", 975, 390, 20, Ink);
        Write($"塔箱   {state.Water:0.0} / {TowerMechanism.Capacity:0}", 975, 420, 17, Rain);
        Write($"台高   +{state.Height:0.0} m", 975, 443, 17, Ink);
        Write(state.Latched ? "高位锁 · 已扣合" : "高位锁 · 待升高", 975, 466, 17, state.Latched ? PlayerColor : Muted);
        Write(slice.ReachedSunCourt ? "采光庭 · 已到访" : "采光庭 · 待到访", 975, 490, 17, slice.ReachedSunCourt ? Wind : Muted);
        Write(state.DockedPort switch
        {
            Port.Entry => "接岸   西侧低岸",
            Port.SunCourt => "接岸   北侧采光庭",
            Port.East => "接岸   东侧货轨",
            _ => "接岸   未接通",
        }, 975, 515, 17, state.DockedPort != Port.None ? Wind : Muted);

        SunCourtOptics2D optics = slice.Optics;
        string light = optics.ReceiverLit ? "接光盘已亮"
            : optics.TowerHit ? "已到塔镜 · 接光盘仍暗"
            : optics.IsSunny ? "未到塔镜" : "未借日光";
        Write($"光路   {light}", 975, 540, 17, optics.IsSunny ? Sunny : Muted);

        Write($"配重   {state.Counterweight:0.0} / {CargoJourney.CounterweightRequired:0}     交付 {slice.DeliveredCount}/1",
            975, 566, 17, slice.DeliveredCount > 0 ? Wind : Rain);
        string cargo = slice.CargoPhase switch
        {
            CargoPhase.WaitingForLight => $"受光确认 {slice.CargoLightProgress:P0}",
            CargoPhase.Loading => optics.ReceiverLit && state.DockedPort == Port.East ? "水果 1 箱 · 正在转水" : "水果 1 箱 · 转水暂停",
            CargoPhase.Ready => optics.ReceiverLit && state.DockedPort == Port.East ? "水果 1 箱 · 等待发车" : "水果 1 箱 · 等待来光",
            CargoPhase.Travelling => $"水果 1 箱 · 跨谷 {slice.CargoTravelProgress:P0}",
            CargoPhase.Arrived => "水果 1 箱 · 已到花庭",
            _ => "花庭 · 已签收",
        };
        Write(cargo, 975, 594, 17, slice.HasFruit || slice.DeliveredCount > 0 ? Sunny : Muted);

        Vector2 pivot = new(1161, 460);
        DrawCircle(pivot, 33f, Line, false, 1.5f, true);
        DrawCircle(pivot, 4f, Muted);
        Vector2 tip = pivot + Vector2.Left.Rotated(Mathf.DegToRad(state.Heading)) * 30f;
        DrawLine(pivot, tip, Wind, 3f, true);
        DrawCircle(tip, 4f, Wind);
        Write("北", pivot.X - 7f, pivot.Y - 41f, 14, Muted);
        Write("西", pivot.X - 50f, pivot.Y + 5f, 14, Muted);
        Write("东", pivot.X + 38f, pivot.Y + 5f, 14, Muted);
    }

    private void Write(string text, float x, float y, int size, Color color) =>
        DrawString(font, new Vector2(x, y), text, HorizontalAlignment.Left, -1f, size, color);
}
