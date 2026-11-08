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
    internal TowerState State => mechanism.State;
    internal bool ReachedSunCourt => route.ReachedSunCourt;
    internal MechanismResult? LastResult { get; private set; }
    internal int Recoveries { get; private set; }

    public override void _Ready()
    {
        Player = GetNode<GrayboxPlayer>("Player");
        Rig = new TowerRig3D(this);
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
        Player.Step(Input.GetVector("walk_west", "walk_east", "walk_north", "walk_south"), step);
        TrackPlayer();
        RefreshHud();
    }

    private void ReadCommands()
    {
        if (Input.IsActionJustPressed("select_rain")) selectedWeather = Weather.Rain;
        if (Input.IsActionJustPressed("select_wind")) selectedWeather = Weather.Wind;
        if (Input.IsActionJustPressed("recall_weather") && mechanism.ActiveWeather != Weather.None)
        {
            mechanism.Recall();
            Say("Weather recalled. Stored water and current heading remain.");
        }

        bool reverse = Input.IsActionJustPressed("cast_weather_reverse", exactMatch: true);
        bool cast = reverse || Input.IsActionJustPressed("cast_weather", exactMatch: true);
        bool drain = Input.IsActionJustPressed("toggle_drain");
        if (!cast && !drain) return;

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
            _ => "This action is unavailable here.",
        });
    }

    private void TrackPlayer()
    {
        Vector3 feet = Player.Feet;
        if (Player.IsOnFloor())
        {
            if (Rig.IsAtStation(feet) && !Rig.IsAtDoorway(feet)) deckCheckpoint = true;
            if (mechanism.DockedPort == Port.SunCourt && Rig.IsAtNorthMarker(feet)
                && route.Visit(Port.SunCourt)) feedbackTime = 0f;
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
        if (mechanism.DockedPort == Port.East) return "Facing east. Select wind, then Shift+E to return north.";
        if (ReachedSunCourt) return "North court reached. Press R to replay from the west bank.";
        if (mechanism.DockedPort == Port.SunCourt) return "North bridge connected. Hold W / Up to reach the flag.";
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
            Port.East => "East heading",
            _ => "Undocked",
        };
        telemetry.Text = $"{selectedWeather} selected  |  Water {mechanism.Water:0.0}/{TowerMechanism.Capacity:0}  |  "
            + $"Lift {mechanism.Height:0.0} m  |  {dock}";
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
        lines.AddChild(HudLine("WASD / arrows   ·   1 Rain   ·   2 Wind   ·   E Cast   ·   Shift+E Reverse", 13, Colors.LightGray));
        lines.AddChild(HudLine("Q Recall   ·   F Drain   ·   R Restart", 13, Colors.LightGray));
    }

    private static Label HudLine(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
