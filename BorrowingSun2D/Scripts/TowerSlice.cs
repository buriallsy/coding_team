using System;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

public partial class TowerSlice : Node2D
{
    private const float FallDistance = 64f;

    private readonly TowerMechanism mechanism = new();
    private readonly RouteProgress route = new();
    private readonly CargoJourney cargo = new();
    private float lastGroundY;
    private bool deckCheckpoint;
    private float time;
    private float messageUntil;
    private string message = "";

    internal SlicePlayer Player { get; private set; } = null!;
    internal TowerRig2D Rig { get; private set; } = null!;
    internal SunCourtOptics2D Optics { get; private set; } = null!;
    internal CargoTrack2D CargoTrack { get; private set; } = null!;
    internal TowerSessionOverlay Overlay { get; private set; } = null!;
    internal MarketPlay2D? Market { get; private set; }
    internal bool IsMarketActive => Market is { Run.Phase: not MarketPhase.Tutorial };
    internal TowerScreen Screen { get; private set; }
    internal TowerState State => mechanism.State;
    internal bool TrySpendWater(float amount) => mechanism.TrySpendWater(amount);
    internal Weather SelectedWeather { get; private set; } = Weather.Rain;
    internal bool ReachedSunCourt => route.ReachedSunCourt;
    internal bool Completed => route.Completed;
    internal int Recoveries { get; private set; }
    internal MechanismResult? LastResult { get; private set; }
    internal CargoActionResult? LastCargoResult { get; private set; }
    internal CargoPhase CargoPhase => cargo.Phase;
    internal float CargoLightProgress => cargo.LightProgress;
    internal float CargoTravelProgress => cargo.TravelProgress;
    internal bool HasFruit => cargo.HasFruit;
    internal bool CargoReadyToLaunch => cargo.Phase == CargoPhase.Ready && Optics.ReceiverLit
        && mechanism.DockedPort == Port.East;
    internal int DeliveredCount => cargo.DeliveredCount;
    internal bool AtStation => Player.IsOnFloor() && Rig.IsAtStation(Player.Feet);
    internal bool AtSolarStation => Player.IsOnFloor() && Optics.IsAtStation(Player.Feet);
    internal bool DoorwayOccupied => Player.IsOnFloor() && Rig.IsAtDoorway(Player.Feet);
    internal bool IsAboardCargo => Player.IsOnFloor() && CargoTrack.IsAboard(Player.Feet);
    internal bool AtDeliveryStation => Player.IsOnFloor() && CargoTrack.IsAtDeliveryStation(Player.Feet);

    internal string Status
    {
        get
        {
            if (time < messageUntil) return message;
            if (IsMarketActive) return Market!.Hint;
            if (cargo.Phase == CargoPhase.Delivered) return "花庭已收货 · 交付 1/1 · R 重新开始";
            if (cargo.Phase == CargoPhase.Travelling) return "货车跨谷中 · 站稳等到站";
            if (cargo.Phase == CargoPhase.Arrived) return AtDeliveryStation
                ? "已到花庭旗标 · G 交付这箱水果" : "货车已到站 · 向右下车走到花庭旗标";
            if (mechanism.IsTurning) return "风正在转动桥舌 · 等待停稳";
            if (mechanism.Draining) return "排水下降 · F 停止排水";
            if (mechanism.IsLifting) return "集雨升塔 · Q 收回雨令";
            if (AtSolarStation) return SolarHint();
            if (mechanism.DockedPort == Port.East)
            {
                if (IsAboardCargo || Completed) return CargoHint();
                if (!ReachedSunCourt) return "先到采光庭 · 回塔用 Shift + E 逆转接北桥";
                return Optics.ReceiverLit ? "东桥已接通 · 接光盘已亮 · 向右走到旗标" : "东桥已接通 · 向右走到东岸旗标";
            }
            if (mechanism.DockedPort == Port.SunCourt) return ReachedSunCourt
                ? "采光庭已到达 · 经过采日镜，再回塔顺转接东桥" : "北桥已接通 · 向左走到采光庭旗标";
            if (mechanism.Latched) return "机械锁已扣合 · 选风，按 E 顺转一档";
            if (AtStation) return "对塔台施雨，升至采光庭的高度";
            if (mechanism.Height > 0f) return "塔台停在当前水位 · 可继续施雨";
            return "向右走过低位接桥，靠近塔台设备";
        }
    }

    public override void _Ready()
    {
        Player = GetNode<SlicePlayer>("Player");
        Rig = new TowerRig2D(this);
        Optics = GetNode<SunCourtOptics2D>("Optics");
        CargoTrack = new CargoTrack2D(this);
        lastGroundY = Rig.EntryFloor.Position.Y;
        Overlay = GetNode<TowerSessionOverlay>("OverlayLayer/SessionOverlay");
        Overlay.Accepted += AcceptScreen;

        string[] arguments = OS.GetCmdlineUserArgs();
        if (Array.IndexOf(arguments, "--verify") < 0 && Array.IndexOf(arguments, "--tutorial-only") < 0)
            Market = new MarketPlay2D(this);
        SetScreen(Array.IndexOf(arguments, "--skip-intro") >= 0 ? TowerScreen.Playing : TowerScreen.Intro);
        if (Array.IndexOf(arguments, "--verify") >= 0)
            new TowerRouteCheck(this).Run();
        else if (Array.IndexOf(arguments, "--verify-market") >= 0)
            new MarketRouteCheck(this).RunCheck();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMarketActive && Screen != TowerScreen.Intro && Input.IsActionJustPressed("reset_slice"))
        {
            ResetSlice();
            return;
        }

        if (Screen != TowerScreen.Playing)
        {
            if (Input.IsActionJustPressed("ui_accept")) AcceptScreen();
            return;
        }

        float step = (float)delta;
        MarketPlay2D? play = IsMarketActive ? Market : null;
        if (play != null && !play.Run.IsExploring)
        {
            play.TickClock(step);
            return;
        }

        time += step;
        ReadCommands();
        mechanism.Tick(step);
        Rig.Synchronize(mechanism.State);
        Optics.Tick(mechanism.Heading, sunlightAvailable: play == null || !play.Run.IsNight);
        if (play != null)
        {
            play.TickExploration(step);
            Player.Step(play.ShuttleMoving ? 0f : Input.GetAxis("walk_left", "walk_right"), step);
            TrackPlayer();
            play.TickClock(step);
            return;
        }

        cargo.Tick(step, mechanism, Optics.ReceiverLit);
        ReadCargoCommand();
        if (Screen == TowerScreen.Finished) return;

        CargoTrack.Synchronize(cargo.TravelProgress);
        float walkAxis = cargo.Phase == CargoPhase.Travelling ? 0f : Input.GetAxis("walk_left", "walk_right");
        Player.Step(walkAxis, step);
        TrackPlayer();
    }

    private void ReadCommands()
    {
        if (Input.IsActionJustPressed("select_rain")) SelectedWeather = Weather.Rain;
        if (Input.IsActionJustPressed("select_wind")) SelectedWeather = Weather.Wind;
        if (Input.IsActionJustPressed("select_sunny")) SelectedWeather = Weather.Sunny;
        if (Input.IsActionJustPressed("recall_weather")) RecallWeather();

        bool reverseCast = Input.IsActionJustPressed("cast_weather_reverse", exactMatch: true);
        bool cast = reverseCast || Input.IsActionJustPressed("cast_weather", exactMatch: true);
        bool drain = Input.IsActionJustPressed("toggle_drain");
        if (!cast && !drain) return;
        if (cast && AtSolarStation)
        {
            CastAtSolarStation(reverseCast);
            return;
        }

        if (!AtStation && !DoorwayOccupied)
        {
            LastResult = null;
            Say("先站上塔台，靠近中央的天气设备");
            return;
        }

        if (reverseCast && SelectedWeather != Weather.Wind)
        {
            LastResult = null;
            Say("先按 2 选风令，再逆转塔台");
            return;
        }

        TurnDirection direction = reverseCast ? TurnDirection.Counterclockwise : TurnDirection.Clockwise;
        LastResult = cast ? mechanism.Cast(SelectedWeather, DoorwayOccupied, direction)
            : mechanism.ToggleDrain(DoorwayOccupied);
        if (Market is { Run.IsExploring: true } play && cast && SelectedWeather == Weather.Rain
            && LastResult is MechanismResult.Applied or MechanismResult.Full)
        {
            play.Raining = true;
            play.Say("降雨持续影响资源 · 在中央按 Q 收回");
            if (LastResult == MechanismResult.Full)
            {
                messageUntil = 0f;
                return;
            }
        }
        if (LastResult == MechanismResult.Applied)
        {
            messageUntil = 0f;
            return;
        }

        Say(LastResult switch
        {
            MechanismResult.DoorwayOccupied => "先离开接桥门槛，再操作塔台",
            MechanismResult.Moving => "塔台正在运动，停稳后再操作",
            MechanismResult.NeedHigh => "先用雨把塔台升至高位",
            MechanismResult.NeedEntryHeading => "先用风转回入口朝向，再放水",
            MechanismResult.Empty => "水箱已空",
            MechanismResult.TurnLimit => reverseCast ? "已到西向限位 · 按 E 顺转" : "已到东向限位 · 用 Shift + E 逆转",
            MechanismResult.UnsupportedWeather => "晴令用于北岸采日镜 · 塔台可用雨令或风令",
            MechanismResult.CounterweightCommitted => "雨水已分给运输配重 · 机械锁保持高位",
            _ => "水箱已满 · 可以换风",
        });
    }

    private void ReadCargoCommand()
    {
        if (!Input.IsActionJustPressed("cargo_action", exactMatch: true)) return;
        LastCargoResult = cargo.Phase is CargoPhase.Arrived or CargoPhase.Delivered
            ? cargo.TryDeliver(AtDeliveryStation)
            : cargo.TryLaunch(mechanism, Optics.ReceiverLit, IsAboardCargo);
        if (LastCargoResult == CargoActionResult.Applied)
        {
            messageUntil = 0f;
            if (cargo.Phase == CargoPhase.Delivered) SetScreen(TowerScreen.Finished);
            return;
        }

        Say(LastCargoResult switch
        {
            CargoActionResult.NeedFruit => "村庄持续接光后，水果才会装上货车",
            CargoActionResult.NeedLight => "接光盘失光 · 保留配重，恢复来光才可发车",
            CargoActionResult.NeedCounterweight => "配重还未满 · 等雨水转入配重",
            CargoActionResult.NeedEastDock => "塔台需停稳并接通东桥",
            CargoActionResult.NeedBoarding => "走上货车，站到车面中央再按 G 发车",
            CargoActionResult.NeedDeliveryStation => "走到花庭旗标再按 G 交付",
            CargoActionResult.AlreadyDelivered => "水果已交付 · 1/1",
            _ => "货车跨谷中 · 等待抵达花庭",
        });
    }

    private void CastAtSolarStation(bool reverseCast)
    {
        if (Market is { Run.IsNight: true } play && SelectedWeather == Weather.Sunny)
        {
            play.Say("夜间没有阳光 · 仍可用风和雨");
            return;
        }
        if (SelectedWeather == Weather.Wind) Optics.AimWithWind(!reverseCast);
        else if (SelectedWeather == Weather.Sunny && !reverseCast) Optics.ClearSky();
        else
        {
            LastResult = MechanismResult.UnsupportedWeather;
            Say("采日镜用风校准、用晴借光 · 风令 Shift + E 让镜面回位");
            return;
        }

        LastResult = MechanismResult.Applied;
        messageUntil = 0f;
    }

    private void RecallWeather()
    {
        if (AtSolarStation)
        {
            if (Optics.ActiveWeather == Weather.None) return;
            Optics.Recall();
            Say("采日镜天气已收回 · 镜面朝向保留");
            return;
        }

        if (Market is { Run.IsExploring: true } play && AtStation && play.Raining)
        {
            play.Raining = false;
            play.Say("已收回降雨");
        }
        if (mechanism.ActiveWeather == Weather.None) return;
        mechanism.Recall();
        Say("塔台天气已收回 · 已存水和当前朝向保留");
    }

    private string SolarHint()
    {
        if (Optics.ReceiverLit) return "接光盘已亮 · 日光经过采日镜与塔镜";
        if (Optics.TowerHit) return "光已到塔镜 · 保持晴天，回塔顺转接东桥";
        if (Optics.IsCalibrated) return "采日镜已对准塔镜 · 3 选晴，按 E 借光";
        return "采日镜 · 2 选风，按 E 校准，之后可用晴令借光";
    }

    private string CargoHint()
    {
        if (!Optics.ReceiverLit) return HasFruit
            ? "接光盘失光 · 配重保留，恢复来光后可继续发车准备" : "接光盘仍暗 · 回北庭检查采日镜与晴天";
        if (CargoReadyToLaunch) return IsAboardCargo
            ? "水果已装车 · 配重已满 · G 发车" : "水果已装车 · 配重已满，向右走上货车";
        return HasFruit
            ? $"水果已装车 · 原雨水正在转入配重 {mechanism.Counterweight:0.0}/{CargoJourney.CounterweightRequired:0}"
            : $"村庄持续接光 {CargoLightProgress:P0} · 等待水果装车";
    }

    private void TrackPlayer()
    {
        Vector2 feet = Player.Feet;
        if (Player.IsOnFloor())
        {
            lastGroundY = feet.Y;
            if (AtStation && !DoorwayOccupied) deckCheckpoint = true;
            Port port = mechanism.DockedPort;
            if (Rig.IsAtShoreFlag(port, feet) && route.Visit(port)) messageUntil = 0f;
            return;
        }

        if (feet.Y - lastGroundY <= FallDistance && feet.Y <= 690f) return;
        Vector2 recovery = IsMarketActive && Market!.ShuttleMoving ? CargoTrack.SafeFeet
            : cargo.Phase is CargoPhase.Travelling or CargoPhase.Arrived or CargoPhase.Delivered
            ? CargoTrack.SafeFeet : deckCheckpoint ? Rig.Station + Vector2.Up : Rig.Spawn;
        Player.Respawn(recovery);
        lastGroundY = Player.Feet.Y;
        Recoveries++;
        if (IsMarketActive) Market!.Say("失足，已返回安全平台");
        Say("失足 · 已回到最近到达的安全平台");
    }

    internal void ResetForMarket() => ResetSlice(marketStart: true);

    internal void ReturnToStore()
    {
        Player.Respawn(Market!.Store + Vector2.Up);
        lastGroundY = Player.Feet.Y;
        deckCheckpoint = true;
    }

    private void ResetSlice(bool marketStart = false)
    {
        mechanism.Reset();
        cargo.Reset();
        Rig.Synchronize(mechanism.State);
        CargoTrack.Synchronize(cargo.TravelProgress);
        Optics.Reset();
        // The lift marker can still be at the old height until the physics server applies the reset.
        Vector2 start = marketStart ? new Vector2(Market!.Store.X, Rig.EntryFloor.Position.Y - 1f) : Rig.Spawn;
        Player.Respawn(start);
        lastGroundY = Rig.EntryFloor.Position.Y;
        deckCheckpoint = marketStart;
        route.Reset();
        Recoveries = 0;
        SelectedWeather = Weather.Rain;
        LastResult = null;
        LastCargoResult = null;
        messageUntil = 0f;
        SetScreen(TowerScreen.Playing);
    }

    private void AcceptScreen()
    {
        if (Screen == TowerScreen.Intro) SetScreen(TowerScreen.Playing);
        else if (Screen == TowerScreen.Finished && Market != null) Market.BeginRun();
        else if (Screen == TowerScreen.Finished) ResetSlice();
    }

    private void SetScreen(TowerScreen screen)
    {
        Screen = screen;
        Overlay.Present(screen, DeliveredCount, tutorial: Market != null);
    }

    private void Say(string text)
    {
        message = text;
        messageUntil = time + 2f;
    }
}
