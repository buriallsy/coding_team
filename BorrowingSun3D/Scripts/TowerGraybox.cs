using System;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun3D;

public partial class TowerGraybox : Node3D
{
    private const float FallHeight = -3f;

    private readonly TowerMechanism mechanism = new();
    private readonly RouteProgress route = new();
    private Weather selectedWeather = Weather.Rain;
    private bool deckCheckpoint;
    private string feedback = "";
    private float feedbackTime;
    private Label telemetry = null!;
    private Label cargoTelemetry = null!;
    private Label hint = null!;

    internal GrayboxPlayer Player { get; private set; } = null!;
    internal TowerRig3D Rig { get; private set; } = null!;
    internal SunCourtOptics3D Optics { get; private set; } = null!;
    internal CargoTrack3D CargoTrack { get; private set; } = null!;
    internal CargoJourney Cargo { get; } = new();
    internal TowerState State => mechanism.State;
    internal bool ReachedSunCourt => route.ReachedSunCourt;
    internal bool ReachedEast => route.Completed;
    internal MechanismResult? LastResult { get; private set; }
    internal CargoActionResult? LastCargoResult { get; private set; }
    internal int Recoveries { get; private set; }
    private bool AtSolarStation => Player.IsOnFloor() && Optics.IsAtStation(Player.Feet);
    private bool IsAboardCargo => Player.IsOnFloor() && CargoTrack.IsAboard(Player.Feet);
    private bool AtDeliveryStation => Player.IsOnFloor() && CargoTrack.IsAtDeliveryStation(Player.Feet);

    public override void _Ready()
    {
        Player = GetNode<GrayboxPlayer>("Player");
        Rig = new TowerRig3D(this);
        Optics = new SunCourtOptics3D(this);
        CargoTrack = new CargoTrack3D(this);
        BuildHud(GetNode<CanvasLayer>("Interface"));
        ResetRoute();
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--verify") >= 0)
            new GrayboxRouteCheck(this).Run();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("reset_slice"))
        {
            ResetRoute();
            return;
        }

        float step = (float)delta;
        feedbackTime = Mathf.Max(0f, feedbackTime - step);
        ReadCommands();
        mechanism.Tick(step);
        Rig.Synchronize(State);
        bool receiverWasLit = Optics.ReceiverLit;
        CargoPhase previousCargoPhase = Cargo.Phase;
        Optics.Tick();
        Cargo.Tick(step, mechanism, Optics.ReceiverLit);
        if (receiverWasLit != Optics.ReceiverLit || previousCargoPhase != Cargo.Phase)
            feedbackTime = 0f;
        ReadCargoCommand();
        CargoTrack.Synchronize(Cargo.TravelProgress, Cargo.HasFruit);
        Vector2 walk = Cargo.Phase == CargoPhase.Travelling ? Vector2.Zero
            : Input.GetVector("walk_west", "walk_east", "walk_north", "walk_south");
        Player.Step(walk, step);
        TrackPlayer();
        RefreshHud();
    }

    private void ReadCommands()
    {
        if (Input.IsActionJustPressed("select_rain")) selectedWeather = Weather.Rain;
        if (Input.IsActionJustPressed("select_wind")) selectedWeather = Weather.Wind;
        if (Input.IsActionJustPressed("select_sunny")) selectedWeather = Weather.Sunny;
        if (Input.IsActionJustPressed("recall_weather")) RecallWeather();

        bool reverse = Input.IsActionJustPressed("cast_weather_reverse", exactMatch: true);
        bool cast = reverse || Input.IsActionJustPressed("cast_weather", exactMatch: true);
        bool drain = Input.IsActionJustPressed("toggle_drain");
        if (!cast && !drain) return;
        if (cast && AtSolarStation)
        {
            CastAtSolarStation(reverse);
            return;
        }

        Vector3 feet = Player.Feet;
        bool doorway = Player.IsOnFloor() && Rig.IsAtDoorway(feet);
        bool atStation = Player.IsOnFloor() && Rig.IsAtStation(feet);
        if (!atStation && !doorway)
        {
            LastResult = null;
            Say("Stand near the weather console on the tower deck.");
            return;
        }
        if (reverse && selectedWeather != Weather.Wind)
        {
            LastResult = null;
            Say("Select wind with 2 before reversing the tower.");
            return;
        }

        LastResult = cast
            ? mechanism.Cast(selectedWeather, doorway, reverse ? TurnDirection.Counterclockwise : TurnDirection.Clockwise)
            : mechanism.ToggleDrain(doorway);
        if (LastResult == MechanismResult.Applied)
        {
            feedbackTime = 0f;
            return;
        }

        Say(LastResult switch
        {
            MechanismResult.DoorwayOccupied => "Step away from the bridge threshold to cast.",
            MechanismResult.Moving => "Wait for the tower to stop.",
            MechanismResult.NeedHigh => "Fill the tank with rain to raise the tower first.",
            MechanismResult.NeedEntryHeading => "Turn back to the west heading before draining.",
            MechanismResult.Empty => "The tank is empty.",
            MechanismResult.Full => "The tank is full. Select wind to turn.",
            MechanismResult.TurnLimit => "End of travel. Turn in the other direction.",
            MechanismResult.UnsupportedWeather => "Use sunny weather at the north mirror.",
            MechanismResult.CounterweightCommitted => "Rainwater is in the ballast. The tower stays locked.",
            _ => "This action is unavailable here.",
        });
    }

    private void ReadCargoCommand()
    {
        if (!Input.IsActionJustPressed("cargo_action", exactMatch: true)) return;
        LastCargoResult = Cargo.Phase is CargoPhase.Arrived or CargoPhase.Delivered
            ? Cargo.TryDeliver(AtDeliveryStation)
            : Cargo.TryLaunch(mechanism, Optics.ReceiverLit, IsAboardCargo);
        if (LastCargoResult == CargoActionResult.Applied)
        {
            feedbackTime = 0f;
            return;
        }

        Say(LastCargoResult switch
        {
            CargoActionResult.NeedFruit => "Keep the east receiver lit to load the fruit crate.",
            CargoActionResult.NeedLight => "Receiver dark. Restore light before departure.",
            CargoActionResult.NeedCounterweight => "Wait for rainwater to fill the ballast.",
            CargoActionResult.NeedEastDock => "Dock the tower at the east shore before departure.",
            CargoActionResult.NeedBoarding => "Board the centre of the cargo car, then press G.",
            CargoActionResult.NeedDeliveryStation => "Walk to the Flower Court marker to deliver.",
            CargoActionResult.AlreadyDelivered => "Delivered 1/1. Press R to restart.",
            _ => "The car is crossing the valley. Wait for arrival.",
        });
    }

    private void CastAtSolarStation(bool reverse)
    {
        if (selectedWeather == Weather.Wind) Optics.AimWithWind(!reverse);
        else if (selectedWeather == Weather.Sunny && !reverse) Optics.ClearSky();
        else
        {
            LastResult = MechanismResult.UnsupportedWeather;
            Say("The north mirror uses wind to aim and sun to collect.");
            return;
        }

        LastResult = MechanismResult.Applied;
        feedbackTime = 0f;
    }

    private void RecallWeather()
    {
        if (AtSolarStation)
        {
            if (Optics.ActiveWeather == Weather.None) return;
            Optics.Recall();
            Say("North weather recalled. The mirror keeps its aim.");
            return;
        }
        if (mechanism.ActiveWeather == Weather.None) return;
        mechanism.Recall();
        Say("Tower weather recalled. Water and heading remain.");
    }

    private void TrackPlayer()
    {
        Vector3 feet = Player.Feet;
        if (Player.IsOnFloor())
        {
            if (Rig.IsAtStation(feet) && !Rig.IsAtDoorway(feet)) deckCheckpoint = true;
            if (mechanism.DockedPort == Port.SunCourt && Rig.IsAtNorthMarker(feet)
                && route.Visit(Port.SunCourt)) feedbackTime = 0f;
            if (mechanism.DockedPort == Port.East && Rig.IsAtEastMarker(feet)
                && route.Visit(Port.East)) feedbackTime = 0f;
            return;
        }
        if (feet.Y >= FallHeight) return;

        Vector3 recovery = Cargo.Phase is CargoPhase.Travelling or CargoPhase.Arrived or CargoPhase.Delivered
            ? CargoTrack.SafeFeet : deckCheckpoint ? Rig.Station + Vector3.Up * .08f : Rig.Spawn;
        Player.Respawn(recovery);
        Recoveries++;
        Say("Back at the last safe platform.");
    }

    private void ResetRoute()
    {
        mechanism.Reset();
        route.Reset();
        Cargo.Reset();
        Rig.Synchronize(State);
        Optics.Reset();
        CargoTrack.Synchronize(Cargo.TravelProgress, Cargo.HasFruit);
        Player.Respawn(Rig.Spawn);
        selectedWeather = Weather.Rain;
        deckCheckpoint = false;
        Recoveries = 0;
        LastResult = null;
        LastCargoResult = null;
        feedbackTime = 0f;
        RefreshHud();
    }

    private void Say(string message)
    {
        feedback = message;
        feedbackTime = 2f;
    }

    private string CurrentHint()
    {
        if (feedbackTime > 0f) return feedback;
        if (Cargo.Phase == CargoPhase.Delivered) return "Delivered 1/1 at Flower Court. Press R to restart.";
        if (Cargo.Phase == CargoPhase.Travelling) return "Crossing the valley. Wait for the car to arrive.";
        if (Cargo.Phase == CargoPhase.Arrived) return AtDeliveryStation
            ? "Flower Court reached. Press G to deliver the crate."
            : "Car arrived. Walk right to the Flower Court marker.";
        if (mechanism.IsTurning) return "Bridge turning. Stay near the console.";
        if (mechanism.Draining) return "Draining. Press F to stop.";
        if (mechanism.IsLifting) return "Collecting rain. Stay on the tower until it locks.";
        if (AtSolarStation)
        {
            if (!Optics.IsCalibrated) return "North mirror: press 2, then E, to aim at the tower.";
            if (Optics.ActiveWeather != Weather.Sunny) return "Mirror aimed. Press 3, then E, to collect sunlight.";
            if (Optics.TowerHit) return "Tower mirror lit. Hold S to return, then turn east.";
            return "North court is sunny. Check the path to the tower.";
        }
        if (mechanism.DockedPort == Port.East)
        {
            if (!ReachedSunCourt) return "Visit north first. On the tower, use 2 then Shift+E.";
            if (!ReachedEast) return "East bridge connected. Hold D / Right to the receiver.";
            return CargoHint();
        }
        if (mechanism.DockedPort == Port.SunCourt)
            return Optics.TowerHit ? "Tower mirror lit. Use 2 then E at the tower to turn east."
                : "North bridge connected. Hold W / Up to the north mirror.";
        if (mechanism.Latched) return "Tower locked. Press 2, then E, to turn the bridge north.";
        if (Player.IsOnFloor() && Rig.IsAtStation(Player.Feet)) return "At the console. Press 1, then E, to collect rain.";
        if (mechanism.Height > 0f) return "Rain paused. Press 1, then E at the console, to resume.";
        return "Hold D / Right across the west bridge to the tower console.";
    }

    private string CargoHint()
    {
        if (!Optics.ReceiverLit) return Cargo.Phase == CargoPhase.Loading
            ? "Receiver dark. Ballast transfer paused; water retained."
            : "Receiver dark. Restore the beam from the north mirror.";
        return Cargo.Phase switch
        {
            CargoPhase.WaitingForLight => "Keep the receiver lit to load one fruit crate.",
            CargoPhase.Loading => "Fruit loaded. Rainwater is filling the ballast.",
            CargoPhase.Ready when IsAboardCargo => "Ballast full. Press G to cross the valley.",
            CargoPhase.Ready => "Ballast full. Walk right onto the cargo car.",
            _ => "Walk to the Flower Court marker.",
        };
    }

    private void RefreshHud()
    {
        string dock = mechanism.DockedPort switch
        {
            Port.Entry => "West",
            Port.SunCourt => "North",
            Port.East => "East",
            _ => "Undocked",
        };
        string light = Optics.ReceiverLit ? "East lit" : Optics.TowerHit ? "Tower lit" : "Dark";
        telemetry.Text = $"{selectedWeather}  |  Water {mechanism.Water:0.0}/{TowerMechanism.Capacity:0}  |  "
            + $"Lift {mechanism.Height:0.0} m  |  {dock}  |  {light}";
        float ballast = MathF.Floor(mechanism.Counterweight * 10f) / 10f;
        cargoTelemetry.Text = $"Crate {(Cargo.HasFruit ? 1 : 0)}/1  |  "
            + $"Ballast {ballast:0.0}/{CargoJourney.CounterweightRequired:0}  |  Delivered {Cargo.DeliveredCount}/1";
        hint.Text = CurrentHint();
    }

    private void BuildHud(CanvasLayer layer)
    {
        var card = new PanelContainer { Position = new Vector2(20, 20), MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(.06f, .10f, .12f, .95f),
            ContentMarginLeft = 15,
            ContentMarginRight = 15,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        });
        var lines = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        lines.AddThemeConstantOverride("separation", 4);
        layer.AddChild(card);
        card.AddChild(lines);
        lines.AddChild(HudLine("BORROWING SUN   /   3D GRAYBOX", 14, new Color(.50f, .86f, .80f)));
        lines.AddChild(telemetry = HudLine("", 16, Colors.White));
        lines.AddChild(cargoTelemetry = HudLine("", 16, Colors.White));
        lines.AddChild(hint = HudLine("", 16, Colors.White));
        lines.AddChild(HudLine("WASD / arrows   ·   1 Rain   ·   2 Wind   ·   3 Sun   ·   E Cast", 13, Colors.LightGray));
        lines.AddChild(HudLine("Shift+E Reverse   ·   Q Recall   ·   F Drain   ·   G Cargo   ·   R Restart", 13, Colors.LightGray));
    }

    private static Label HudLine(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
