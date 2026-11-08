using System;
using System.IO;
using System.Threading.Tasks;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun3D;

// Drives the production scene through real key events. The only fixture is a temporary optical blocker.
internal sealed class GrayboxRouteCheck(TowerGraybox scene)
{
    private const float PositionTolerance = .12f;
    private const float WaterTolerance = .003f;
    private const float LiftTolerance = .03f;
    private const uint OpticalOnlyLayer = 8;
    private const string CaptureOption = "--capture-dir=";

    private AnimatableBody3D lift = null!;
    private AnimatableBody3D carrier = null!;
    private CollisionShape3D bridge = null!;
    private CollisionObject3D bridgeBody = null!;
    private (StaticBody3D Body, PhysicsRayQueryParameters3D Ray)? blocker;
    private string? captureDirectory;
    private int assertions;
    private bool auditSupply;
    private int supplySamples;
    private float maxWaterDrift;
    private float maxLiftDrift;

    internal async void Run()
    {
        try
        {
            lift = scene.GetNode<AnimatableBody3D>("Lift");
            carrier = scene.GetNode<AnimatableBody3D>("CargoCarrier");
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
            GD.Print("SCOPE: 3D graybox with production Input Map and Physics3D; no player placement or state injection. "
                + "One optical-only StaticBody3D at a time is the only fixture. The replay is fixture-free.");
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
            await CalibrateAndLightNorth(checkParkedAim: true);
            await Capture("north-marker.png");
            await ReturnAndVisitEast();
            await Capture("east-receiver.png");
            await BoardCarrier();
            await CheckLaunchGates();
            await Capture("cargo-ready.png");
            await LaunchAndRide(blockAfterLaunch: true);
            await ConfirmDelivery();

            await ResetToEntrance("from the delivery marker");
            await WalkToStation();
            await RiseAndVisitNorth();
            await CalibrateAndLightNorth();
            await ReturnAndVisitEast();
            await FallAndRecover();
            await BoardCarrier();
            await WaitReady();
            await LaunchAndRide();
            await ConfirmDelivery();
            Require(scene.Cargo.DeliveredCount == 1 && scene.Optics.ReceiverLit,
                "the fixture-free route from the reset low bridge delivers 1/1 again");
            await Capture("cargo-delivered.png");

            Require(supplySamples > 0, "the cargo route audited conserved water and the actual high lift every physics step");
            GD.Print($"SUPPLY samples={supplySamples}, max_water_drift={maxWaterDrift:0.000000}, max_lift_drift_m={maxLiftDrift:0.000000}");

            GD.Print($"GRAYBOX_ROUTE PASSED assertions={assertions} failures=0");
            scene.GetTree().Quit();
        }
        catch (Exception failure)
        {
            GD.PushError($"GRAYBOX_ROUTE FAILED after {assertions} checks: {failure.Message}; "
                + $"state={scene.State}, feet={scene.Player?.GlobalPosition}, floor={scene.Player?.IsOnFloor()}, "
                + $"bridge={scene.Rig?.BridgeCollisionEnabled}, northVisited={scene.ReachedSunCourt}, "
                + $"eastVisited={scene.ReachedEast}, towerHit={scene.Optics?.TowerHit}, receiverLit={scene.Optics?.ReceiverLit}, "
                + $"cargo={scene.Cargo?.Phase}, lastCargoResult={scene.LastCargoResult}, "
                + $"recoveries={scene.Recoveries}, lastResult={scene.LastResult}");
            scene.GetTree().Quit(1);
        }
        finally
        {
            if (blocker is { } fixture) fixture.Body.QueueFree();
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

    private async Task CalibrateAndLightNorth(bool checkParkedAim = false)
    {
        if (checkParkedAim)
        {
            await Cast(Key.Key3);
            Require(scene.Optics.IsSunny && !scene.Optics.IsCalibrated && !scene.Optics.TowerHit && !scene.Optics.ReceiverLit,
                "sunny weather at the parked collector does not light the tower or the east receiver");
        }

        await Cast(Key.Key2);
        Require(scene.Optics.IsCalibrated, "actual 2 and E at the north station calibrate the collector");
        await Cast(Key.Key3);
        await Until(() => scene.Optics.IsSunny && scene.Optics.TowerHit, 60,
            "actual 3 and E clear the sky and send light to the tower");
        RequireNorthPath();

        if (checkParkedAim)
        {
            await Tap(Key.Q);
            Require(scene.Optics.IsCalibrated && !scene.Optics.IsSunny && !scene.Optics.Path.Incoming.HasValue,
                "Q recalls sunshine at the north station while keeping its calibrated aim");
            await Cast(Key.Key3);
            RequireNorthPath();
        }
    }

    private void RequireNorthPath()
    {
        var path = scene.Optics.Path;
        Require(scene.Optics.TowerHit && !scene.Optics.ReceiverLit
            && !scene.Cargo.HasFruit && scene.Cargo.LightProgress == 0f
            && path.Incoming is { Hit: LightHit3D.Collector } incoming
            && path.FirstReflection is { Hit: LightHit3D.TowerMirror } first
            && path.SecondReflection is { } second && second.To.X < second.From.X
            && ToGodot(incoming.To).DistanceTo(ToGodot(first.From)) < .02f,
            "at north, the collector reaches the tower and the second reflection points west, leaving the east receiver dark");
    }

    private void RequireEastPath()
    {
        var path = scene.Optics.Path;
        Require(scene.Optics.ReceiverLit && scene.Optics.TowerHit
            && path.Incoming is { Hit: LightHit3D.Collector } incoming
            && path.FirstReflection is { Hit: LightHit3D.TowerMirror } first
            && path.SecondReflection is { Hit: LightHit3D.Receiver } second
            && ToGodot(incoming.To).DistanceTo(ToGodot(first.From)) < .02f
            && ToGodot(first.To).DistanceTo(ToGodot(second.From)) < .02f
            && ToGodot(second.To).DistanceTo(scene.GetNode<Marker3D>("East/Receiver").GlobalPosition) < .55f,
            "at east, one continuous ray hits the collector, tower mirror and receiver in that order");
    }

    private async Task BlockSecondReflection(string context)
    {
        if (blocker.HasValue) throw new InvalidOperationException("The previous optical fixture is still active");
        var clear = scene.Optics.Path.SecondReflection
            ?? throw new InvalidOperationException("The fixture requires an existing clear second reflection");
        Require(clear.Hit == LightHit3D.Receiver, $"{context}: start from an actual unobstructed receiver hit");
        Vector3 from = ToGodot(clear.From);
        Vector3 to = ToGodot(clear.To);
        Vector3 midpoint = from.Lerp(to, .5f);
        var body = new StaticBody3D { Name = "TemporaryOpticalBlocker", CollisionLayer = OpticalOnlyLayer, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * .4f } });
        scene.AddChild(body);
        body.GlobalPosition = midpoint;
        var ray = PhysicsRayQueryParameters3D.Create(from, to, OpticalOnlyLayer);
        blocker = (body, ray);
        GD.Print($"FIXTURE {context}: layer={OpticalOnlyLayer}, centre={midpoint}, ray={from} -> {to}");

        await Frames(6);
        var hit = scene.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        Require(hit.TryGetValue("collider", out Variant collider) && collider.AsGodotObject() == body,
            $"{context}: the native 3D ray hits the temporary optical-only collider");
        Vector3 impact = hit["position"].AsVector3();
        Require(scene.Optics.TowerHit && !scene.Optics.ReceiverLit && scene.ReachedEast
            && scene.Optics.Path.SecondReflection is { Hit: LightHit3D.Obstacle } blocked
            && ToGodot(blocked.To).DistanceTo(impact) < .02f,
            $"{context}: the real second reflection stops at the same collision point and extinguishes the receiver");
        GD.Print($"TRACE {context}: blocked second reflection at {impact}");
    }

    private async Task UnblockSecondReflection()
    {
        var fixture = blocker ?? throw new InvalidOperationException("No optical fixture is active");
        fixture.Body.QueueFree();
        blocker = null;
        await Frames(6);
        Require(scene.GetWorld3D().DirectSpaceState.IntersectRay(fixture.Ray).Count == 0,
            "removing the fixture clears the native optical ray");
        RequireEastPath();
    }

    private async Task ReturnAndVisitEast()
    {
        await Walk(Key.S, () => scene.Player.GlobalPosition.Z >= scene.Rig.Station.Z - .12f,
            "walk south across the north bridge back to the tower console");
        Require(scene.Rig.IsAtStation(scene.Player.GlobalPosition) && !scene.Rig.IsAtDoorway(scene.Player.GlobalPosition)
            && IsSupportedByDeck() && scene.Optics.IsSunny && scene.Optics.TowerHit && !scene.Optics.ReceiverLit,
            "the north beam persists when leaving the solar station, with the east receiver still dark");

        await Cast(Key.Key2);
        Require(scene.State.IsTurning && scene.State.Heading > TowerMechanism.SunCourtHeading
            && scene.State.Heading < TowerMechanism.EastHeading && scene.State.Latched
            && IsSupportedByDeck() && !scene.Optics.ReceiverLit,
            "the east turn begins under the grounded player and the receiver remains dark while misaligned");
        RequireBridge(Port.None, "while turning east");

        await Until(() => scene.State.Heading >= 135f, 100, "the east turn reaches its midpoint");
        Require(scene.State.IsTurning && !scene.Optics.ReceiverLit,
            "at 135 degrees the reflected light still misses the east receiver");
        RequireBridge(Port.None, "halfway through the east turn");
        await Until(() => scene.State.DockedPort == Port.East, 160, "the wind turn docks east");
        await Frames(4);
        Require(Mathf.IsEqualApprox(scene.State.Heading, TowerMechanism.EastHeading) && IsSupportedByDeck(),
            "the lift stops at 180 degrees with the player still supported");
        RequireBridge(Port.East, "at the east dock");
        RequireEastPath();
        Require(!scene.ReachedEast, "lighting the receiver from the tower does not count as reaching east");
        auditSupply = true;
        await ConfirmSustainedLight();

        await Walk(Key.D, () => scene.Player.GlobalPosition.X >= scene.Rig.EastMarker.X - .15f,
            "walk east over the supported bridge to the actual east marker");
        Require(scene.ReachedSunCourt && scene.ReachedEast && scene.Rig.IsAtEastMarker(scene.Player.GlobalPosition)
            && scene.Optics.ReceiverLit,
            "the east milestone requires the physical crossing and the clear reflected beam stays on");
    }

    private async Task ConfirmSustainedLight()
    {
        float lightProgress = scene.Cargo.LightProgress;
        Require(lightProgress is > 0f and < .25f && scene.Cargo.Phase == CargoPhase.WaitingForLight
            && !scene.CargoTrack.FruitVisible && scene.State.Counterweight == 0f,
            "the initial real receiver hit starts the light timer without awarding a crate");

        float secondsRemaining = (1f - lightProgress) * CargoJourney.RequiredLightSeconds;
        int ticksBeforeThreshold = Mathf.FloorToInt(secondsRemaining * Engine.PhysicsTicksPerSecond) - 3;
        await Frames(ticksBeforeThreshold);
        Require(scene.Optics.ReceiverLit && scene.Cargo.LightProgress is > .9f and < 1f
            && !scene.Cargo.HasFruit && !scene.CargoTrack.FruitVisible && scene.State.Counterweight == 0f,
            "just short of 1.5 seconds of clear light there is still no crate or transferred water");
        await Until(() => scene.Cargo.HasFruit, 8, "the continuous 1.5-second beam earns the single crate");
        Require(scene.Cargo.Phase == CargoPhase.Loading && scene.CargoTrack.FruitVisible && scene.Cargo.DeliveredCount == 0,
            "the earned crate appears on the car and begins loading without counting as delivered");
    }

    private async Task BoardCarrier()
    {
        await Walk(Key.D, () => scene.Player.GlobalPosition.X >= scene.CargoTrack.Departure.X - .12f,
            "walk across the east floor and board the stationary car at its real departure point");
        Require(scene.CargoTrack.IsAboard(scene.Player.GlobalPosition)
            && PhysicsBodyPosition(carrier).DistanceTo(scene.CargoTrack.Departure) < PositionTolerance
            && scene.Cargo.TravelProgress == 0f && scene.Cargo.DeliveredCount == 0,
            "the grounded player is on the car and its native body is still at departure");
    }

    private async Task CheckLaunchGates()
    {
        Require(scene.Optics.ReceiverLit && scene.Cargo.HasFruit && scene.Cargo.Phase == CargoPhase.Loading
            && scene.State.Counterweight is > 0f and < CargoJourney.CounterweightRequired,
            "the real car is boarded while the original water is still filling the counterweight");
        await Tap(Key.G);
        Require(scene.LastCargoResult == CargoActionResult.NeedCounterweight && scene.Cargo.TravelProgress == 0f,
            "actual G refuses departure with a lit receiver and an underfilled counterweight");

        await BlockSecondReflection("partial transfer");
        TowerState paused = scene.State;
        Require(paused.Counterweight is > 0f and < CargoJourney.CounterweightRequired,
            "the blocked beam interrupts a genuinely partial transfer");
        await Frames(55);
        Require(scene.Cargo.Phase == CargoPhase.Loading && scene.CargoTrack.FruitVisible && !scene.Optics.ReceiverLit
            && Mathf.Abs(scene.State.Water - paused.Water) < WaterTolerance
            && Mathf.Abs(scene.State.Counterweight - paused.Counterweight) < WaterTolerance,
            "darkness pauses both tanks without losing the crate or the transferred water");
        TraceCargo("partial transfer held");
        await Tap(Key.G);
        Require(scene.LastCargoResult == CargoActionResult.NeedLight && scene.Cargo.TravelProgress == 0f,
            "actual G refuses departure while the partial load is dark");
        await UnblockSecondReflection();
        await Until(() => scene.State.Counterweight > paused.Counterweight + .04f, 60,
            "removing the blocker resumes the transfer from the retained amount");
        await WaitReady();

        await BlockSecondReflection("full load before departure");
        await Tap(Key.G);
        Require(scene.LastCargoResult == CargoActionResult.NeedLight && scene.Cargo.TravelProgress == 0f
            && Mathf.Abs(scene.State.Counterweight - CargoJourney.CounterweightRequired) < WaterTolerance,
            "actual G also refuses dark departure at a full 4/4 counterweight");
        await UnblockSecondReflection();
    }

    private async Task WaitReady()
    {
        await Until(() => scene.Cargo.Phase == CargoPhase.Ready && scene.Optics.ReceiverLit, 300,
            "the clear beam fills the cargo counterweight");
        Require(Mathf.Abs(scene.State.Counterweight - CargoJourney.CounterweightRequired) < WaterTolerance
            && Mathf.Abs(scene.State.Water - (TowerMechanism.Capacity - CargoJourney.CounterweightRequired)) < WaterTolerance
            && scene.CargoTrack.FruitVisible && scene.Cargo.DeliveredCount == 0,
            "at 4/4 the original tank retains two units and the loaded car still awaits G");
        TraceCargo("ready to launch");
    }

    private async Task LaunchAndRide(bool blockAfterLaunch = false)
    {
        Vector3 start = PhysicsBodyPosition(carrier);
        Vector3 offset = scene.Player.GlobalPosition - start;
        int recoveries = scene.Recoveries;
        await Tap(Key.G);
        Require(scene.LastCargoResult == CargoActionResult.Applied && scene.Cargo.Phase == CargoPhase.Travelling
            && scene.Cargo.TravelProgress > 0f, "actual G launches the occupied car with a clear receiver and full counterweight");
        await Until(() => PhysicsBodyPosition(carrier).X > start.X + .12f, 60,
            "the native car body starts translating with its grounded rider");
        if (blockAfterLaunch)
        {
            await BlockSecondReflection("already in motion");
            Require(scene.Cargo.Phase == CargoPhase.Travelling && !scene.Optics.ReceiverLit,
                "losing light after G does not cancel the ongoing trip");
        }

        float maxRelativeDrift = 0f;
        for (int frame = 0; frame < 300 && scene.Cargo.Phase == CargoPhase.Travelling; frame++)
        {
            await Frames(1);
            Vector3 liveOffset = scene.Player.GlobalPosition - PhysicsBodyPosition(carrier);
            maxRelativeDrift = Mathf.Max(maxRelativeDrift, liveOffset.DistanceTo(offset));
            if (!scene.Player.IsOnFloor() || scene.Recoveries != recoveries || (blockAfterLaunch && scene.Optics.ReceiverLit))
                throw new InvalidOperationException("The rider lost physical support or the expected dark travel state");
        }

        await Frames(5);
        Vector3 actual = PhysicsBodyPosition(carrier);
        Vector3 terminal = scene.CargoTrack.Terminal;
        Require(scene.Cargo.Phase == CargoPhase.Arrived && scene.Cargo.TravelProgress >= .999f
            && actual.DistanceTo(terminal) < PositionTolerance && scene.CargoTrack.CarrierPosition.DistanceTo(terminal) < PositionTolerance,
            "the native and rendered car both arrive at the authored terminal");
        Require(actual.DistanceTo(start) > 7f && maxRelativeDrift < .20f
            && scene.Player.IsOnFloor() && scene.CargoTrack.IsAboard(scene.Player.GlobalPosition) && scene.Recoveries == recoveries,
            $"the car physically carries the grounded player {actual.DistanceTo(start):0.00} m with drift {maxRelativeDrift:0.000} m");
        Require(scene.Cargo.DeliveredCount == 0 && scene.CargoTrack.FruitVisible
            && (!blockAfterLaunch || !scene.Optics.ReceiverLit),
            "arrival retains the crate and does not auto-deliver it");
        TraceCargo(blockAfterLaunch ? "arrived in darkness" : "arrived in light");
        if (blockAfterLaunch) await UnblockSecondReflection();
    }

    private async Task ConfirmDelivery()
    {
        await Tap(Key.G);
        Require(scene.LastCargoResult == CargoActionResult.NeedDeliveryStation && scene.Cargo.DeliveredCount == 0,
            "actual G on the arrived car refuses delivery away from the garden marker");
        await Walk(Key.D, () => scene.Player.GlobalPosition.X >= scene.CargoTrack.Goal.X - .12f,
            "walk off the real arrived car onto the delivery shore and reach its goal");
        Require(scene.CargoTrack.IsAtDeliveryStation(scene.Player.GlobalPosition) && scene.Cargo.DeliveredCount == 0,
            "reaching the actual garden goal still requires a G confirmation");
        await Tap(Key.G);
        Require(scene.LastCargoResult == CargoActionResult.Applied && scene.Cargo.Phase == CargoPhase.Delivered
            && scene.Cargo.DeliveredCount == 1 && !scene.CargoTrack.FruitVisible,
            "actual G at the delivery marker confirms 1/1 and removes the crate from the car");
        await Tap(Key.G);
        Require(scene.Cargo.Phase == CargoPhase.Delivered && scene.Cargo.DeliveredCount == 1,
            "repeated actual G cannot count the delivery twice");
        TraceCargo("delivered 1/1");
    }

    private async Task ResetToEntrance(string context)
    {
        auditSupply = false;
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
                KeepHeld(Key.W);
                await Frames(1);
                wasAirborne |= !scene.Player.IsOnFloor();
            }
        }
        finally
        {
            Send(Key.W, false);
        }

        await Frames(15);
        Require(wasAirborne && scene.Recoveries == recoveries + 1 && IsSupportedByDeck()
            && scene.ReachedSunCourt && scene.ReachedEast && scene.State.DockedPort == Port.East,
            "walking off the real east shore returns the airborne player to the live deck and preserves both milestones");
    }

    private void RequireEntrance(string context)
    {
        TowerState state = scene.State;
        Require(scene.Player.IsOnFloor() && scene.Player.GlobalPosition.DistanceTo(scene.Rig.Spawn) < PositionTolerance
            && state.Water == 0f && state.Height == 0f && state.Heading == 0f && !state.Latched
            && state.ActiveWeather == Weather.None && !scene.ReachedSunCourt && !scene.ReachedEast && scene.Recoveries == 0
            && scene.LastResult == null && Mathf.Abs(PhysicsLiftPosition().Y - scene.Rig.Station.Y) < PositionTolerance,
            $"{context}: restore the west spawn, zero mechanism state and clear both milestones");
        Require(!scene.Optics.IsSunny && !scene.Optics.IsCalibrated && !scene.Optics.TowerHit && !scene.Optics.ReceiverLit
            && !scene.Optics.Path.Incoming.HasValue,
            $"{context}: clear the collector pose, local sunshine and optical hits");
        Require(state.Counterweight == 0f && scene.Cargo.Phase == CargoPhase.WaitingForLight
            && scene.Cargo.LightProgress == 0f && scene.Cargo.TravelProgress == 0f && scene.Cargo.DeliveredCount == 0
            && !scene.CargoTrack.FruitVisible && scene.LastCargoResult == null
            && scene.CargoTrack.CarrierPosition.DistanceTo(scene.CargoTrack.Departure) < PositionTolerance
            && PhysicsBodyPosition(carrier).DistanceTo(scene.CargoTrack.Departure) < PositionTolerance,
            $"{context}: clear ballast, crate and delivery and return the native car to departure");
        RequireBridge(Port.Entry, context);
    }

    private void RequireBridge(Port expected, string context)
    {
        bool connected = expected is Port.Entry or Port.SunCourt or Port.East;
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

    private Vector3 PhysicsLiftPosition() => PhysicsBodyPosition(lift);

    private static Vector3 PhysicsBodyPosition(AnimatableBody3D body)
        => PhysicsServer3D.BodyGetState(body.GetRid(), PhysicsServer3D.BodyState.Transform).AsTransform3D().Origin;

    private void AuditSupply()
    {
        TowerState state = scene.State;
        float highY = scene.Rig.EastMarker.Y;
        float waterDrift = Mathf.Abs(state.Water + state.Counterweight - TowerMechanism.Capacity);
        float heightDrift = Mathf.Abs(state.Height - TowerMechanism.TravelMetres);
        float liftDrift = Mathf.Abs(PhysicsLiftPosition().Y - highY);
        float deckDrift = Mathf.Abs(scene.Rig.Station.Y - highY);
        maxWaterDrift = Mathf.Max(maxWaterDrift, waterDrift);
        maxLiftDrift = Mathf.Max(maxLiftDrift, Mathf.Max(heightDrift, Mathf.Max(liftDrift, deckDrift)));
        supplySamples++;
        if (waterDrift > WaterTolerance || maxLiftDrift > LiftTolerance || !state.Latched || state.DockedPort != Port.East)
            throw new InvalidOperationException($"Cargo changed total water or the real high lift: {state}; lift={PhysicsLiftPosition()}");
    }

    private void TraceCargo(string context)
        => GD.Print($"TRACE {context}: phase={scene.Cargo.Phase}, light={scene.Optics.ReceiverLit}, "
            + $"water={scene.State.Water:0.000000}, ballast={scene.State.Counterweight:0.000000}, "
            + $"height={scene.State.Height:0.000}, lift_y={PhysicsLiftPosition().Y:0.000}, "
            + $"car={PhysicsBodyPosition(carrier)}, rider={scene.Player.GlobalPosition}, delivered={scene.Cargo.DeliveredCount}");

    private static Vector3 ToGodot(System.Numerics.Vector3 point) => new(point.X, point.Y, point.Z);

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
        {
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (auditSupply) AuditSupply();
        }
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
