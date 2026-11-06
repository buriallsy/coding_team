using System;
using System.Threading.Tasks;
using BorrowingSun2D.Core;
using Godot;

namespace BorrowingSun2D;

// A normal scene instance driven through its Input Map. This runner never places the player
// or changes the mechanism directly, including before the deliberately missed bridge.
internal sealed class TowerRouteCheck(TowerSlice scene)
{
    internal async void Run()
    {
        try
        {
            await Frames(4);
            Require(scene.State.DockedPort == Port.Entry && scene.BridgeCollisionEnabled && scene.Player.IsOnFloor(),
                "spawn on the low entry with the bridge physically connected");
            await Walk("walk_right", () => scene.Player.Feet.X >= 655f);
            Require(scene.AtStation && scene.Recoveries == 0, "walk continuously from the entry onto the deck");

            float originalFeet = scene.Player.Feet.Y;
            await Pulse("cast_weather");
            await Frames(66);
            float liftedFeet = scene.Player.Feet.Y;
            Require(scene.State.Height is > 1f and < 3f && liftedFeet < originalFeet - 40f
                && IsSupportedByDeck() && !scene.BridgeCollisionEnabled,
                "rain raises the physical deck and carries the stationary player");
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

            await Pulse("select_rain");
            await Pulse("cast_weather");
            await Until(() => scene.State.Latched, 290, "reach high lock after restoring rain");
            await Frames(5);
            Require(scene.State.Height == TowerMechanism.TravelMetres && IsSupportedByDeck()
                && scene.State.DockedPort == Port.None && !scene.BridgeCollisionEnabled,
                "height alone leaves a real gap to the sun court");

            await Pulse("select_wind");
            await Walk("walk_left", () => scene.DoorwayOccupied);
            await Pulse("cast_weather");
            Require(scene.LastResult == MechanismResult.DoorwayOccupied && !scene.State.IsTurning,
                "a player in the bridge threshold prevents rotation");

            await Walk("walk_left", () => scene.Recoveries == 1);
            await Frames(4);
            Require(scene.AtStation && scene.State.Latched && !scene.Completed && !scene.BridgeCollisionEnabled,
                "walking into the unbridged gap falls and recovers on the deck without completing");

            await Pulse("cast_weather");
            await Frames(35);
            Require(scene.State.IsTurning && scene.State.Latched && !scene.BridgeCollisionEnabled,
                "wind preserves high lock and leaves the opening unsupported while turning");
            await Pulse("recall_weather");
            float pausedHeading = scene.State.Heading;
            await Frames(15);
            Require(pausedHeading is > 0f and < TowerMechanism.SunCourtHeading && scene.State.Heading == pausedHeading
                && !scene.State.IsTurning && !scene.BridgeCollisionEnabled,
                "recall can stop midway without connecting the gap");
            await Pulse("cast_weather");
            await Until(() => scene.State.DockedPort == Port.SunCourt && scene.BridgeCollisionEnabled, 150,
                "the bridge collider becomes active after the wind stop");

            await Walk("walk_left", () => scene.Completed);
            Require(scene.Player.IsOnFloor() && scene.Player.Feet.X <= scene.Goal.X && scene.Recoveries == 1,
                "walk across the connected bridge and reach the sun-court goal");

            await Pulse("reset_slice");
            await Frames(5);
            Require(scene.State.DockedPort == Port.Entry && scene.State.Water == 0f && scene.State.Heading == 0f
                && !scene.Completed && !scene.State.Latched && scene.Recoveries == 0
                && scene.BridgeCollisionEnabled && scene.Player.IsOnFloor() && scene.Player.Feet.X < 200f,
                "reset restores the entry, player, mechanism, and bridge");

            await Walk("walk_right", () => scene.Player.Feet.X >= 655f);
            Require(scene.AtStation && scene.Recoveries == 0, "walk onto the physical low bridge again after reset");
            await Pulse("cast_weather");
            await Frames(45);
            Require(scene.State.IsLifting && IsSupportedByDeck(), "start the lift again after reset");
            await Pulse("reset_slice");
            await Frames(5);
            Require(scene.State.DockedPort == Port.Entry && scene.Player.IsOnFloor() && scene.Player.Feet.X < 200f
                && scene.Recoveries == 0, "reset from a moving platform clears the carry and restores the spawn");
            await Walk("walk_right", () => scene.Player.Feet.X >= 655f);
            Require(scene.AtStation && scene.Recoveries == 0, "walk onto the low bridge after resetting in motion");

            GD.Print("TOWER_ROUTE PASSED");
            scene.GetTree().Quit();
        }
        catch (Exception failure)
        {
            GD.PushError($"TOWER_ROUTE FAILED: {failure.Message}; state={scene.State}, "
                + $"feet={scene.Player.Feet}, deck={scene.Deck.Position}, floor={scene.Player.IsOnFloor()}, "
                + $"bridge={scene.BridgeCollisionEnabled}, lastResult={scene.LastResult}");
            scene.GetTree().Quit(1);
        }
    }

    private bool IsSupportedByDeck() => scene.Player.IsOnFloor()
        && Mathf.Abs(scene.Player.Feet.Y - scene.Deck.Position.Y) < 3f;

    private async Task Walk(string action, Func<bool> done)
    {
        Input.ActionPress(action);
        try { await Until(done, 225, $"reach destination by holding {action}"); }
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
