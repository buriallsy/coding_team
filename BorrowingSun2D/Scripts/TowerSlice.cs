using System;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

public partial class TowerSlice : Node2D
{
    private const float FallDistance = 64f;

    private readonly TowerMechanism mechanism = new();
    private readonly RouteProgress route = new();
    private float lastGroundY;
    private bool deckCheckpoint;
    private float time;
    private float messageUntil;
    private string message = "";

    internal SlicePlayer Player { get; private set; } = null!;
    internal TowerRig2D Rig { get; private set; } = null!;
    internal TowerState State => mechanism.State;
    internal Weather SelectedWeather { get; private set; } = Weather.Rain;
    internal bool ReachedSunCourt => route.ReachedSunCourt;
    internal bool Completed => route.Completed;
    internal int Recoveries { get; private set; }
    internal MechanismResult? LastResult { get; private set; }
    internal bool AtStation => Player.IsOnFloor() && Rig.IsAtStation(Player.Feet);
    internal bool DoorwayOccupied => Player.IsOnFloor() && Rig.IsAtDoorway(Player.Feet);

    internal string Status
    {
        get
        {
            if (time < messageUntil) return message;
            if (Completed) return "已抵达东岸 · 三岸路线完成 · R 重新开始";
            if (mechanism.IsTurning) return "风正在转动桥舌 · 等待停稳";
            if (mechanism.Draining) return "排水下降 · F 停止排水";
            if (mechanism.IsLifting) return "集雨升塔 · Q 收回雨令";
            if (mechanism.DockedPort == Port.East) return ReachedSunCourt
                ? "东桥已接通 · 向右走到东岸旗标" : "先到采光庭 · 回塔用 Shift + E 逆转接北桥";
            if (mechanism.DockedPort == Port.SunCourt) return ReachedSunCourt
                ? "采光庭已到达 · 回塔选风，按 E 顺转接东桥" : "北桥已接通 · 向左走到采光庭旗标";
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
        lastGroundY = Rig.EntryFloor.Position.Y;

        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--verify") >= 0)
            new TowerRouteCheck(this).Run();
    }

    public override void _PhysicsProcess(double delta)
    {
        float step = (float)delta;
        time += step;
        if (Input.IsActionJustPressed("reset_slice"))
        {
            ResetSlice();
            return;
        }

        ReadCommands();
        mechanism.Tick(step);
        Rig.Synchronize(mechanism.State);
        Player.Step(Input.GetAxis("walk_left", "walk_right"), step);
        TrackPlayer();
    }

    private void ReadCommands()
    {
        if (Input.IsActionJustPressed("select_rain")) SelectedWeather = Weather.Rain;
        if (Input.IsActionJustPressed("select_wind")) SelectedWeather = Weather.Wind;
        if (Input.IsActionJustPressed("recall_weather") && mechanism.ActiveWeather != Weather.None)
        {
            mechanism.Recall();
            Say("天气令已收回 · 已存水和当前朝向保留");
        }

        bool reverseCast = Input.IsActionJustPressed("cast_weather_reverse", exactMatch: true);
        bool cast = reverseCast || Input.IsActionJustPressed("cast_weather", exactMatch: true);
        bool drain = Input.IsActionJustPressed("toggle_drain");
        if (!cast && !drain) return;
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
            _ => "水箱已满 · 可以换风",
        });
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
        Player.Respawn(deckCheckpoint ? Rig.Station + Vector2.Up : Rig.Spawn);
        lastGroundY = Player.Feet.Y;
        Recoveries++;
        Say("失足 · 已回到最近到达的安全平台");
    }

    private void ResetSlice()
    {
        mechanism.Reset();
        Rig.Synchronize(mechanism.State);
        Player.Respawn(Rig.Spawn);
        lastGroundY = Rig.EntryFloor.Position.Y;
        deckCheckpoint = false;
        route.Reset();
        Recoveries = 0;
        SelectedWeather = Weather.Rain;
        LastResult = null;
        messageUntil = 0f;
    }

    private void Say(string text)
    {
        message = text;
        messageUntil = time + 2f;
    }
}
