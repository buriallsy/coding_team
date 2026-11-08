using System;
using System.IO;
using System.Threading.Tasks;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun3D;

// Drives the production scene through real key events. Physics queries only observe collision.
internal sealed class GrayboxRouteCheck(TowerGraybox scene)
{
    private const float PositionTolerance = .12f;
    private const string CaptureOption = "--capture-dir=";

    private AnimatableBody3D lift = null!;
    private CollisionShape3D bridge = null!;
    private CollisionObject3D bridgeBody = null!;
    private string? captureDirectory;
    private int assertions;

    internal async void Run()
    {
        try
        {
            lift = scene.GetNode<AnimatableBody3D>("Lift");
            bridge = scene.GetNode<CollisionShape3D>("Lift/BridgeCollision");
            bridgeBody = bridge.GetParent<CollisionObject3D>();
            string? captureArgument = Array.Find(OS.GetCmdlineUserArgs(), arg => arg.StartsWith(CaptureOption, StringComparison.Ordinal));
            captureDirectory = captureArgument?[CaptureOption.Length..];
            if (captureDirectory != null)
            {
                if (DisplayServer.GetName() == "headless")
                    throw new InvalidOperationException("Viewport capture requires a graphical Godot run");
                Directory.CreateDirectory(captureDirectory);
            }
            GD.Print("SCOPE: 3D graybox, configured main scene, production Input Map and Physics3D; no player placement or state injection.");
            await Frames(8);
            RequireEntrance("initial state");
            await Capture("west-entrance.png");

            await Walk(Key.D, () => scene.Rig.IsAtDoorway(scene.Player.GlobalPosition),
                "walk from the west low shore to the supported doorway");
            await Cast(Key.Key2);
            Require(scene.LastResult == MechanismResult.DoorwayOccupied && scene.State.Height == 0f,
                "wind is refused while the player is standing in the actual doorway");
            RequireBridge(Port.Entry, "low doorway refusal");

            await WalkToStation();
            await Cast(Key.Key2);
            Require(scene.LastResult == MechanismResult.NeedHigh && scene.State.Heading == 0f,
                "wind at the low station is refused");
            RequireBridge(Port.Entry, "low wind refusal");

            await RiseAndVisitNorth();
            await Capture("north-marker.png");
            await ResetToEntrance("from the north marker");
            await WalkToStation();
            await RiseAndVisitNorth();
            Require(scene.ReachedSunCourt && scene.Recoveries == 0,
                "the player physically reaches the north marker again after R");
            await FallAndRecover();

            GD.Print($"GRAYBOX_ROUTE PASSED assertions={assertions} failures=0");
            scene.GetTree().Quit();
        }
        catch (Exception failure)
        {
            GD.PushError($"GRAYBOX_ROUTE FAILED after {assertions} checks: {failure.Message}; "
                + $"state={scene.State}, feet={scene.Player?.GlobalPosition}, floor={scene.Player?.IsOnFloor()}, "
                + $"bridge={scene.Rig?.BridgeCollisionEnabled}, northVisited={scene.ReachedSunCourt}, "
                + $"recoveries={scene.Recoveries}, lastResult={scene.LastResult}");
            scene.GetTree().Quit(1);
        }
    }

    private async Task RiseAndVisitNorth()
    {
        Vector3 startFeet = scene.Player.GlobalPosition;
        float lowLiftY = PhysicsLiftPosition().Y;
        await Cast(Key.Key1);
        await Until(() => scene.State.Height > 1f, 120, "rain starts raising the occupied lift");
        Require(scene.State.IsLifting && scene.Player.GlobalPosition.Y > startFeet.Y + .8f
            && PhysicsLiftPosition().Y > lowLiftY + .8f && IsSupportedByDeck(),
            "the real moving lift carries the stationary player upward");
        RequireBridge(Port.None, "while rising");

        await Until(() => scene.State.Latched, 360, "rain fills the tower and latches the high deck");
        await Frames(4);
        Require(Mathf.IsEqualApprox(scene.State.Water, TowerMechanism.Capacity)
            && Mathf.IsEqualApprox(scene.State.Height, TowerMechanism.TravelMetres)
            && Mathf.Abs(PhysicsLiftPosition().Y - lowLiftY - TowerMechanism.TravelMetres) < PositionTolerance
            && IsSupportedByDeck(),
            "six units of rain hold both the actual lift and grounded rider six metres higher");
        RequireBridge(Port.None, "high but still west-facing");

        await Cast(Key.Key2);
        Require(scene.State.IsTurning && scene.State.Heading > 0f && scene.State.Heading < TowerMechanism.SunCourtHeading
            && scene.State.Latched && IsSupportedByDeck(),
            "wind starts a turn while the high deck stays locked under the player");
        RequireBridge(Port.None, "while turning north");

        await Until(() => scene.State.DockedPort == Port.SunCourt, 160, "the wind turn docks north");
        await Frames(4);
        Require(Mathf.IsEqualApprox(scene.State.Heading, TowerMechanism.SunCourtHeading) && IsSupportedByDeck(),
            "the lift is stopped at 90 degrees and the player remains supported");
        RequireBridge(Port.SunCourt, "at the north dock");

        await Walk(Key.W, () => scene.Player.GlobalPosition.Z <= scene.Rig.NorthMarker.Z + .15f,
            "walk north over the supported bridge to the actual marker");
        Require(scene.ReachedSunCourt && scene.Rig.IsAtNorthMarker(scene.Player.GlobalPosition),
            "the north milestone is recorded at the authored marker");
    }

    private async Task WalkToStation()
    {
        await Walk(Key.D, () => scene.Player.GlobalPosition.X >= scene.Rig.Station.X - .12f,
            "walk east across supported surfaces to the centre of the tower");
        Require(scene.Rig.IsAtStation(scene.Player.GlobalPosition) && !scene.Rig.IsAtDoorway(scene.Player.GlobalPosition)
            && IsSupportedByDeck(), "the player stands on the real deck clear of the doorway");
    }

    private async Task ResetToEntrance(string context)
    {
        await Tap(Key.R);
        await Frames(8);
        RequireEntrance($"R {context}");
    }

    private async Task FallAndRecover()
    {
        int recoveries = scene.Recoveries;
        bool wasAirborne = false;
        try
        {
            for (int frame = 0; frame < 240 && scene.Recoveries == recoveries; frame++)
            {
                KeepHeld(Key.D);
                await Frames(1);
                wasAirborne |= !scene.Player.IsOnFloor();
            }
        }
        finally
        {
            Send(Key.D, false);
        }

        await Frames(15);
        Require(wasAirborne && scene.Recoveries == recoveries + 1 && IsSupportedByDeck()
            && scene.ReachedSunCourt && scene.State.DockedPort == Port.SunCourt,
            "walking off the real north shore returns the airborne player to the live deck and preserves the milestone");
    }

    private void RequireEntrance(string context)
    {
        TowerState state = scene.State;
        Require(scene.Player.IsOnFloor() && scene.Player.GlobalPosition.DistanceTo(scene.Rig.Spawn) < PositionTolerance
            && state.Water == 0f && state.Height == 0f && state.Heading == 0f && !state.Latched
            && state.ActiveWeather == Weather.None && !scene.ReachedSunCourt && scene.Recoveries == 0
            && scene.LastResult == null && Mathf.Abs(PhysicsLiftPosition().Y - scene.Rig.Station.Y) < PositionTolerance,
            $"{context}: restore the west spawn, zero mechanism state and clear the milestone");
        RequireBridge(Port.Entry, context);
    }

    private void RequireBridge(Port expected, string context)
    {
        bool connected = expected is Port.Entry or Port.SunCourt;
        Vector3 centre = bridge.GlobalPosition;
        var ray = PhysicsRayQueryParameters3D.Create(centre + Vector3.Up, centre + Vector3.Down, bridgeBody.CollisionLayer);
        var hit = scene.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        bool nativeHit = hit.TryGetValue("collider", out Variant collider) && collider.AsGodotObject() == bridgeBody
            && bridgeBody.ShapeOwnerGetOwner(bridgeBody.ShapeFindOwner(hit["shape"].AsInt32())) == bridge;
        Require(scene.State.DockedPort == expected && scene.Rig.BridgeCollisionEnabled == connected && nativeHit == connected,
            $"{context}: expected dock={expected}, bridge flag={scene.Rig.BridgeCollisionEnabled}, native bridge hit={nativeHit}");
    }

    private bool IsSupportedByDeck()
        => scene.Player.IsOnFloor() && scene.Rig.IsOnDeck(scene.Player.GlobalPosition)
            && Mathf.Abs(scene.Player.GlobalPosition.Y - scene.Rig.Station.Y) < PositionTolerance;

    private Vector3 PhysicsLiftPosition()
        => PhysicsServer3D.BodyGetState(lift.GetRid(), PhysicsServer3D.BodyState.Transform).AsTransform3D().Origin;

    private async Task Walk(Key key, Func<bool> reached, string context)
    {
        Vector3 start = scene.Player.GlobalPosition;
        int recoveries = scene.Recoveries;
        try
        {
            for (int frame = 0; frame < 360 && !reached(); frame++)
            {
                KeepHeld(key);
                await Frames(1);
                if (!scene.Player.IsOnFloor() || scene.Recoveries != recoveries)
                    throw new InvalidOperationException($"{context}: the player lost physical support");
            }
        }
        finally
        {
            Send(key, false);
        }

        await Frames(3);
        Require(reached() && scene.Player.GlobalPosition.DistanceTo(start) > .2f
            && scene.Player.IsOnFloor() && scene.Recoveries == recoveries, context);
    }

    private async Task Cast(Key weather)
    {
        await Tap(weather);
        await Tap(Key.E);
    }

    private async Task Tap(Key key)
    {
        Send(key, true);
        try
        {
            await Frames(4);
        }
        finally
        {
            Send(key, false);
        }
        await Frames(4);
    }

    private static void KeepHeld(Key key)
    {
        // A window focus change can clear held keys during a graphical run.
        if (!Input.IsPhysicalKeyPressed(key)) Send(key, true);
    }

    private static void Send(Key key, bool pressed)
    {
        using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
        Input.ParseInputEvent(input);
    }

    private async Task Until(Func<bool> reached, int maxFrames, string context)
    {
        for (int frame = 0; frame < maxFrames && !reached(); frame++) await Frames(1);
        Require(reached(), context);
    }

    private async Task Frames(int count)
    {
        for (int frame = 0; frame < count; frame++)
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task Capture(string fileName)
    {
        if (captureDirectory == null) return;

        await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);
        await scene.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = scene.GetViewport().GetTexture().GetImage();
        string path = Path.Combine(captureDirectory, fileName);
        if (image.SavePng(path) != Error.Ok)
            throw new IOException($"Unable to save viewport capture: {path}");
        GD.Print($"CAPTURE {fileName} {image.GetSize()}");
    }

    private void Require(bool passed, string context)
    {
        if (!passed) throw new InvalidOperationException(context);
        assertions++;
        GD.Print($"PASS {context} | feet={scene.Player.GlobalPosition}, lift={PhysicsLiftPosition()}, dock={scene.State.DockedPort}");
    }
}
