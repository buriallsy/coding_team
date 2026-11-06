using System;
using System.Threading.Tasks;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

// Drive the production scene through its Input Map. Collision queries only observe the world.
internal sealed class TowerRouteCheck(TowerSlice scene)
{
    private const float StationX = 655f;

    internal async void Run()
    {
        try
        {
            GD.Print("SCOPE: Round 01, default entrance, production Input Map and Physics2D, no player placement or state injection");
            CheckFinishExtension();
            await Frames(5);
            RequireEntrance("initial state");
            await Pulse("cast_weather");
            Require(scene.LastResult is null && scene.State.Water == 0f, "remote weather from the entrance is refused");
            await WalkToStation();
            await Pulse("select_wind");
            await Pulse("cast_weather");
            Require(scene.LastResult == MechanismResult.NeedHigh, "wind at low height is refused");
            RequireBridges(Port.Entry, "low wind rejection");

            await CheckRainControls();
            await FillTower();
            await Pulse("select_wind");
            await Pulse("cast_weather_reverse");
            Require(scene.LastResult == MechanismResult.TurnLimit && scene.State.Heading == 0f,
                "reverse wind at the west end cannot wrap to east");

            await FallAndRecover("walk_left");
            await FallAndRecover("walk_right");
            await DockNorth(pauseMidTurn: true);
            await VisitNorthAndEast(checkDoorways: true);
            await CheckReverseFromEast();

            await ResetToEntrance("after reaching east and returning to the tower");
            await WalkToStation();
            await FillTower();
            await DockNorth();
            await VisitNorthAndEast();
            Require(scene.Completed && scene.Recoveries == 0, "the complete route works again after reset without recovery");

            await ResetToEntrance("from the east flag");
            await WalkToStation();
            await FillTower();
            await DockNorth();
            await DockEast();
            await Walk("walk_right", () => scene.Player.Feet.X >= scene.Rig.Goal.X, "walk to east without visiting north", keepSupport: true);
            Require(!scene.ReachedSunCourt && !scene.Completed, "the east flag alone does not complete the route");

            await ResetToEntrance("after the out-of-order visit");
            await WalkToStation();
            await Pulse("cast_weather");
            await Frames(45);
            Require(scene.State.IsLifting && IsSupportedByDeck(), "the player rides another ascent before the in-motion reset");
            await ResetToEntrance("while the lift is moving");
            await WalkToStation();

            GD.Print("TOWER_ROUTE PASSED");
            scene.GetTree().Quit();
        }
        catch (Exception failure)
        {
            GD.PushError($"TOWER_ROUTE FAILED: {failure.Message}; state={scene.State}, feet={scene.Player.Feet}, "
                + $"deck={scene.Rig.Deck.Position}, floor={scene.Player.IsOnFloor()}, left={scene.Rig.BridgeCollisionEnabled}, "
                + $"east={scene.Rig.EastBridgeCollisionEnabled}, northVisited={scene.ReachedSunCourt}, lastResult={scene.LastResult}");
            scene.GetTree().Quit(1);
        }
    }

    private static void CheckFinishExtension()
    {
        var route = new GatedRouteProgress { EastFinishAllowed = true };
        Require(!route.Visit(Port.East) && !route.Completed,
            "route extension: an open gate still requires the north visit");
        Require(route.Visit(Port.SunCourt) && route.ReachedSunCourt && !route.Completed,
            "route extension: visiting north records the milestone");
        route.EastFinishAllowed = false;
        Require(!route.Visit(Port.East) && !route.Completed && route.ReachedSunCourt,
            "route extension: a closed gate preserves the milestone and blocks east completion");
        route.EastFinishAllowed = true;
        Require(route.Visit(Port.East) && route.Completed && !route.Visit(Port.East),
            "route extension: reopening the gate permits completion once");
        route.Reset();
        Require(!route.Completed && !route.ReachedSunCourt && !route.Visit(Port.East),
            "route extension: reset restores the north prerequisite");
    }

    private sealed class GatedRouteProgress : RouteProgress
    {
        internal bool EastFinishAllowed { get; set; }

        protected override bool MeetsEastFinishCondition() => EastFinishAllowed;
    }

    private async Task CheckRainControls()
    {
        float originalFeet = scene.Player.Feet.Y;
        await Pulse("select_rain");
        await Pulse("cast_weather");
        await Frames(66);
        Require(scene.State.Height is > 1f and < 3f && scene.Player.Feet.Y < originalFeet - 40f && IsSupportedByDeck(),
            "rain raises the physical deck and carries the stationary player");
        RequireBridges(Port.None, "rain in motion");
        await Pulse("select_wind");
        await Pulse("cast_weather");
        Require(scene.LastResult == MechanismResult.Moving && scene.State.ActiveWeather == Weather.Rain,
            "wind is refused while the lift is moving");

        await Pulse("recall_weather");
        await Frames(12);
        TowerState paused = scene.State;
        await Frames(30);
        Require(!scene.State.IsLifting && scene.State.Water == paused.Water && scene.State.Height == paused.Height,
            "recall stops filling and preserves the intermediate height");

        await Pulse("toggle_drain");
        await Frames(28);
        Require(scene.State.Height < paused.Height - .4f && scene.State.Water < paused.Water && IsSupportedByDeck(),
            "draining lowers the deck and carries the player downward");
        await Pulse("toggle_drain");
        Require(!scene.State.Draining && !scene.State.IsLifting, "a second drain interaction stops the rollback");
    }

    private async Task FillTower()
    {
        await Pulse("select_rain");
        await Pulse("cast_weather");
        await Until(() => scene.State.Latched, 290, "wait until the full tank latches the deck");
        await Frames(4);
        Require(scene.State.Water == TowerMechanism.Capacity && scene.State.Height == TowerMechanism.TravelMetres
            && IsSupportedByDeck(), "full water holds the occupied deck at the upper height");
        RequireBridges(Port.None, "high west stop");
    }

    private async Task FallAndRecover(string action)
    {
        int previousRecoveries = scene.Recoveries;
        await Walk(action, () => scene.Recoveries > previousRecoveries, $"fall into the real gap by holding {action}");
        await Frames(4);
        Require(scene.Recoveries == previousRecoveries + 1 && scene.AtStation && IsSupportedByDeck()
            && scene.State.Latched && !scene.Completed && !scene.ReachedSunCourt,
            "a missed bridge returns the player to the deck without granting progress");
    }

    private async Task DockNorth(bool pauseMidTurn = false)
    {
        await Pulse("select_wind");
        await Pulse("cast_weather");
        Require(scene.State.IsTurning && scene.State.Latched, "north wind turns while the high lock holds");
        RequireBridges(Port.None, "turning from west to north");
        if (pauseMidTurn)
        {
            await Frames(35);
            await Pulse("recall_weather");
            float pausedHeading = scene.State.Heading;
            await Frames(15);
            Require(pausedHeading is > 0f and < TowerMechanism.SunCourtHeading && scene.State.Heading == pausedHeading
                && !scene.State.IsTurning, "recall holds a partial turn");
            RequireBridges(Port.None, "paused between stops");
            await Pulse("cast_weather");
        }

        await Until(() => scene.State.DockedPort == Port.SunCourt && scene.Rig.BridgeCollisionEnabled, 150,
            "north wind reaches its first stop");
        RequireBridges(Port.SunCourt, "north dock");
    }

    private async Task VisitNorthAndEast(bool checkDoorways = false)
    {
        await Walk("walk_left", () => scene.Player.Feet.X <= scene.Rig.SunCourtMarker.X,
            "walk across the supported north bridge to the north flag", keepSupport: true);
        Require(scene.ReachedSunCourt && !scene.Completed, "the north flag records a milestone without ending the route");

        if (checkDoorways)
        {
            await Pulse("cast_weather");
            Require(scene.LastResult is null && scene.State.Heading == TowerMechanism.SunCourtHeading,
                "the north shore cannot turn the tower remotely");
            await Walk("walk_right", () => scene.DoorwayOccupied, "return to the actual north bridge threshold", keepSupport: true);
            await Pulse("cast_weather");
            Require(scene.LastResult == MechanismResult.DoorwayOccupied, "the north threshold refuses a turn to east");
            RequireBridges(Port.SunCourt, "north threshold rejection");
        }

        await WalkToStation();
        Require(scene.ReachedSunCourt && !scene.Completed, "the north milestone survives walking back to the station");
        await DockEast();
        await Walk("walk_right", () => scene.Completed, "walk across the supported east bridge to the east flag", keepSupport: true);
        Require(scene.Player.Feet.X >= scene.Rig.Goal.X && scene.Player.IsOnFloor() && scene.ReachedSunCourt,
            "completion requires standing on the actual east landing after visiting north");
    }

    private async Task DockEast()
    {
        await Pulse("cast_weather");
        Require(scene.State.IsTurning && scene.State.Latched && scene.State.Water == TowerMechanism.Capacity,
            "the next wind cast turns toward east without spending the stored water");
        RequireBridges(Port.None, "turning from north to east");
        await Until(() => scene.State.DockedPort == Port.East && scene.Rig.EastBridgeCollisionEnabled, 150,
            "east wind reaches the next stop");
        RequireBridges(Port.East, "east dock");
    }

    private async Task CheckReverseFromEast()
    {
        await Walk("walk_left", () => scene.DoorwayOccupied, "return to the actual east bridge threshold", keepSupport: true);
        await Pulse("cast_weather_reverse");
        Require(scene.LastResult == MechanismResult.DoorwayOccupied, "the east threshold refuses reverse wind");
        RequireBridges(Port.East, "east threshold rejection");
        await Walk("walk_left", () => scene.Player.Feet.X <= StationX + 6f,
            "walk back across the east bridge to the station", keepSupport: true);
        Require(scene.AtStation && !scene.DoorwayOccupied, "return from the east side clears the doorway");

        await Pulse("cast_weather");
        Require(scene.LastResult == MechanismResult.TurnLimit, "forward wind at the east end cannot wrap to west");
        RequireBridges(Port.East, "east end rejection");
        await Pulse("cast_weather_reverse");
        Require(scene.State.IsTurning, "reverse wind starts the return from east to north");
        RequireBridges(Port.None, "turning from east to north");
        await Until(() => scene.State.DockedPort == Port.SunCourt && scene.Rig.BridgeCollisionEnabled, 150,
            "reverse wind docks north again");
        RequireBridges(Port.SunCourt, "north dock after reverse wind");
    }

    private async Task ResetToEntrance(string context)
    {
        await Pulse("reset_slice");
        await Frames(8);
        RequireEntrance($"reset {context}");
    }

    private void RequireEntrance(string context)
    {
        Require(scene.State.Water == 0f && scene.State.Height == 0f && scene.State.Heading == 0f
            && scene.State.ActiveWeather == Weather.None && !scene.State.Latched && !scene.Completed
            && !scene.ReachedSunCourt && scene.Recoveries == 0 && scene.Player.IsOnFloor() && scene.Player.Feet.X < 200f,
            $"{context}: restore the entrance, zero mechanism state and clear both flags");
        RequireBridges(Port.Entry, context);
        Require(!HasBridgeCollision(scene.Rig.BridgeExtension.GetCenter().X, scene.Rig.CourtFloor.Position.Y),
            $"{context}: the high left bridge has no stale collision");
    }

    private void RequireBridges(Port port, string context)
    {
        bool left = port is Port.Entry or Port.SunCourt;
        bool east = port == Port.East;
        float deckY = scene.Rig.Deck.Position.Y;
        float leftX = scene.Rig.BridgeExtension.GetCenter().X;
        float eastX = scene.Rig.EastBridgeExtension.GetCenter().X;
        bool hitLeft = HasBridgeCollision(leftX, deckY);
        bool hitEast = HasBridgeCollision(eastX, deckY);
        Require(scene.State.DockedPort == port && scene.Rig.BridgeCollisionEnabled == left
            && scene.Rig.EastBridgeCollisionEnabled == east && hitLeft == left && hitEast == east,
            $"{context}: physical bridge probes at Y={deckY:0.00}, left={hitLeft}, east={hitEast}, dock={scene.State.DockedPort}");
    }

    private bool HasBridgeCollision(float x, float y)
    {
        var query = PhysicsRayQueryParameters2D.Create(new Vector2(x, y - 10f), new Vector2(x, y + 16f), 2);
        return scene.GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0;
    }

    private bool IsSupportedByDeck() => scene.Player.IsOnFloor()
        && Mathf.Abs(scene.Player.Feet.Y - scene.Rig.Deck.Position.Y) < 3f;

    private async Task WalkToStation()
    {
        await Walk("walk_right", () => scene.Player.Feet.X >= StationX, "walk across supported surfaces to the station", keepSupport: true);
        Require(scene.AtStation && !scene.DoorwayOccupied, "reach the station on the deck away from either doorway");
    }

    private async Task Walk(string action, Func<bool> done, string name, bool keepSupport = false)
    {
        int recoveries = scene.Recoveries;
        Input.ActionPress(action);
        try
        {
            for (int i = 0; i < 300 && !done(); i++)
            {
                await Frames(1);
                if (keepSupport && (!scene.Player.IsOnFloor() || scene.Recoveries != recoveries))
                    throw new InvalidOperationException($"{name}: player lost support");
            }
            Require(done(), name);
        }
        finally { Input.ActionRelease(action); }
        await Frames(2);
    }

    private async Task Pulse(string action)
    {
        Input.ActionPress(action);
        await Frames(1);
        Input.ActionRelease(action);
        await Frames(2);
    }

    private async Task Until(Func<bool> predicate, int maxFrames, string name)
    {
        for (int i = 0; i < maxFrames && !predicate(); i++) await Frames(1);
        Require(predicate(), name);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        GD.Print($"PASS {name}");
    }
}
