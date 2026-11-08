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
    private Label hint = null!;

    internal GrayboxPlayer Player { get; private set; } = null!;
    internal TowerRig3D Rig { get; private set; } = null!;
    internal SunCourtOptics3D Optics { get; private set; } = null!;
    internal TowerState State => mechanism.State;
    internal bool ReachedSunCourt => route.ReachedSunCourt;
    internal bool ReachedEast => route.Completed;
    internal MechanismResult? LastResult { get; private set; }
    internal int Recoveries { get; private set; }
    private bool AtSolarStation => Player.IsOnFloor() && Optics.IsAtStation(Player.Feet);

    public override void _Ready()
    {
        Player = GetNode<GrayboxPlayer>("Player");
        Rig = new TowerRig3D(this);
        Optics = new SunCourtOptics3D(this);
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
        Optics.Tick();
        Player.Step(Input.GetVector("walk_west", "walk_east", "walk_north", "walk_south"), step);
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
            _ => "This action is unavailable here.",
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

        Player.Respawn(deckCheckpoint ? Rig.Station + Vector3.Up * .08f : Rig.Spawn);
        Recoveries++;
        Say("Back at the last safe platform.");
    }

    private void ResetRoute()
    {
        mechanism.Reset();
        route.Reset();
        Rig.Synchronize(State);
        Optics.Reset();
        Player.Respawn(Rig.Spawn);
        selectedWeather = Weather.Rain;
        deckCheckpoint = false;
        Recoveries = 0;
        LastResult = null;
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
            return Optics.ReceiverLit ? "East receiver lit and reached. Press R to replay."
                : "East receiver reached. Check the north mirror and beam.";
        }
        if (mechanism.DockedPort == Port.SunCourt)
            return Optics.TowerHit ? "Tower mirror lit. Use 2 then E at the tower to turn east."
                : "North bridge connected. Hold W / Up to the north mirror.";
        if (mechanism.Latched) return "Tower locked. Press 2, then E, to turn the bridge north.";
        if (Player.IsOnFloor() && Rig.IsAtStation(Player.Feet)) return "At the console. Press 1, then E, to collect rain.";
        if (mechanism.Height > 0f) return "Rain paused. Press 1, then E at the console, to resume.";
        return "Hold D / Right across the west bridge to the tower console.";
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
        lines.AddChild(hint = HudLine("", 16, Colors.White));
        lines.AddChild(HudLine("WASD / arrows   ·   1 Rain   ·   2 Wind   ·   3 Sun   ·   E Cast", 13, Colors.LightGray));
        lines.AddChild(HudLine("Shift+E Reverse   ·   Q Recall   ·   F Drain   ·   R Restart", 13, Colors.LightGray));
    }

    private static Label HudLine(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
