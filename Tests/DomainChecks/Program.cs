using System;
using System.Numerics;
using BorrowingSun2D.Core;

int assertions = 0;
CheckTower();
CheckRoute();
CheckOptics();
CheckCargo();
MarketChecks.Run(Check);
Console.WriteLine($"DOMAIN_CHECKS PASSED: {assertions} assertions; domain and geometry fixtures.");

void CheckTower()
{
    var tower = new TowerMechanism();
    Check(tower.DockedPort == Port.Entry, "initial low entry");
    Check(Cast(tower, Weather.Sunny) == MechanismResult.UnsupportedWeather, "sunny cannot become tower wind");
    Check(Cast(tower, Weather.None) == MechanismResult.UnsupportedWeather, "no token cannot become tower wind");
    Check(Cast(tower, Weather.Wind) == MechanismResult.NeedHigh, "low tower refuses a wind turn");
    Check(Cast(tower, Weather.Rain) == MechanismResult.Applied, "rain starts at the west entry");
    tower.Tick(1f);
    Check(Cast(tower, Weather.Wind) == MechanismResult.Moving, "wind cannot interrupt ascent");
    tower.Recall();
    TowerState held = tower.State;
    tower.Tick(1f);
    Check(Near(tower.Water, held.Water) && Near(tower.Height, held.Height), "rain recall preserves partial water and height");
    Raise(tower);
    Check(tower.Latched && Near(tower.Water, 6f) && Near(tower.Height, 6f), "six units reach the high lock");
    Check(tower.DockedPort == Port.None, "height alone gives no bridge at the west heading");
    Check(Cast(tower, Weather.Wind, occupied: true) == MechanismResult.DoorwayOccupied, "occupied doorway blocks wind");
    Check(Cast(tower, Weather.Wind, TurnDirection.Counterclockwise) == MechanismResult.TurnLimit, "west limit prevents reverse wrap");
    Check(Cast(tower, Weather.Wind) == MechanismResult.Applied, "clockwise wind begins a turn");
    tower.Tick(.5f);
    Check(tower.IsTurning && tower.DockedPort == Port.None, "the bridge stays open as a gap during rotation");
    tower.Recall();
    float heading = tower.Heading;
    tower.Tick(1f);
    Check(Near(tower.Heading, heading) && tower.DockedPort == Port.None, "wind recall preserves the partial angle");
    Turn(tower, Port.SunCourt);
    Turn(tower, Port.East);
    Check(Cast(tower, Weather.Wind) == MechanismResult.TurnLimit, "east limit prevents forward wrap");
    Turn(tower, Port.SunCourt, TurnDirection.Counterclockwise);
    Turn(tower, Port.None, TurnDirection.Counterclockwise);
    Check(Near(tower.Heading, 0f) && tower.Latched, "reverse wind returns to the high west heading");
    Check(tower.ToggleDrain(false) == MechanismResult.Applied, "the west-facing drain releases an uncommitted lock");
    tower.Tick(5f);
    Check(tower.DockedPort == Port.Entry && tower.Water == 0f && tower.Height == 0f, "draining settles at the real low entry state");
}

void CheckRoute()
{
    var route = new RouteProgress();
    Check(!route.Visit(Port.East) && !route.Completed, "east cannot finish an out-of-order visit");
    Check(route.Visit(Port.SunCourt) && route.ReachedSunCourt && !route.Completed, "north is a milestone");
    Check(!route.Visit(Port.SunCourt), "a second north visit does not repeat the milestone");
    Check(route.Visit(Port.East) && route.Completed, "east finishes the ordered shore route");
    Check(!route.Visit(Port.East), "east finish is recorded once");
    route.Reset();
    Check(!route.ReachedSunCourt && !route.Completed, "route reset clears both milestones");
}

void CheckOptics()
{
    var collector = new OpticalSurface2D(new(2f, 0f), new(-1f, 1f), .7f);
    var relay = new OpticalSurface2D(new(2f, 2f), new(1f, -1f), .7f);
    var receiver = new OpticalSurface2D(new(4f, 2f), -Vector2.UnitX, .5f);
    LightPath2D Trace(Func<Vector2, Vector2, Vector2?>? obstacle = null) =>
        Optics2D.Trace(Vector2.Zero, Vector2.UnitX, collector, relay, receiver, obstacle ?? ((_, _) => null));

    LightPath2D path = Trace();
    Check(path.Incoming?.Hit == LightHit2D.Collector && path.TowerHit && path.ReceiverLit,
        "finite mirrors reflect in order from collector to relay to receiver");
    Check(Vector2.Distance(path.FirstReflection!.Value.To, relay.Center) < .002f
        && Vector2.Distance(path.SecondReflection!.Value.To, receiver.Center) < .002f,
        "reflected segments end at the authored target points");

    var beforeCollector = new Vector2(1f, 0f);
    path = Trace((from, to) => BlockAt(from, to, beforeCollector));
    Check(path.Incoming?.Hit == LightHit2D.Obstacle && path.FirstReflection is null,
        "a physical obstruction before the collector prevents the first bounce");
    var beforeRelay = new Vector2(2f, 1f);
    path = Trace((from, to) => BlockAt(from, to, beforeRelay));
    Check(path.FirstReflection?.Hit == LightHit2D.Obstacle && path.SecondReflection is null,
        "an obstruction between mirrors prevents the second bounce");
    var beforeReceiver = new Vector2(3f, 2f);
    path = Trace((from, to) => BlockAt(from, to, beforeReceiver));
    Check(path.TowerHit && !path.ReceiverLit && path.SecondReflection?.To == beforeReceiver,
        "an obstruction truncates the outgoing beam before the receiver");
    Check(!Trace((from, to) => BlockAt(from, to, receiver.Center)).ReceiverLit,
        "the physical query wins over a coincident receiver endpoint");

    LightPath2D backside = Optics2D.Trace(Vector2.Zero, Vector2.UnitX, collector,
        relay with { Normal = -relay.Normal }, receiver, (_, _) => null);
    Check(backside.TowerHit && backside.SecondReflection is null, "the relay back face cannot reflect");
    Check(!Optics2D.Trace(Vector2.Zero, Vector2.UnitX, collector, relay,
        receiver with { Normal = -receiver.Normal }, (_, _) => null).ReceiverLit, "the receiver back face cannot accept light");
    Check(!Optics2D.Trace(Vector2.Zero, Vector2.UnitX, collector,
        relay with { Center = new Vector2(4f, 2f) }, receiver, (_, _) => null).TowerHit, "the finite relay aperture can miss");
    Check(!Optics2D.Trace(new Vector2(3f, 2f), Vector2.UnitX, collector, relay, receiver, (_, _) => null).ReceiverLit,
        "a direct receiver hit cannot skip either mirror");
    Check(Optics2D.Trace(Vector2.Zero, Vector2.UnitX, collector, relay, receiver, (_, _) => null, 1f).FirstReflection is null,
        "finite ray range can prevent the first mirror hit");
    Check(Optics2D.Trace(Vector2.Zero, Vector2.Zero, collector, relay, receiver, (_, _) => null).Incoming is null,
        "zero direction emits no optical segments");
}

void CheckCargo()
{
    var tower = new TowerMechanism();
    var cargo = new CargoJourney();
    Check(Near(CargoJourney.RequiredLightSeconds, 1.5f) && Near(CargoJourney.CounterweightRequired, 4f)
        && Near(CargoJourney.TravelSeconds, 3.4f), "Round 03 reward, ballast and travel values");
    Check(cargo.TryLaunch(tower, true, true) == CargoActionResult.NeedFruit, "light alone cannot launch before the fruit reward");
    Raise(tower);
    Turn(tower, Port.SunCourt);
    Turn(tower, Port.East);
    float halfLightTime = CargoJourney.RequiredLightSeconds / 2f;
    cargo.Tick(halfLightTime, tower, true);
    Check(!cargo.HasFruit && Near(cargo.LightProgress, .5f), "short illumination earns no crate");
    cargo.Tick(.1f, tower, false);
    Check(!cargo.HasFruit && cargo.LightProgress == 0f, "interruption resets the incomplete dwell");
    cargo.Tick(CargoJourney.RequiredLightSeconds, tower, true);
    Check(cargo.HasFruit && cargo.Phase == CargoPhase.Loading && tower.Counterweight == 0f,
        "continuous illumination earns one crate and begins the transfer phase");
    cargo.Tick(1f, tower, true);
    Check(Near(tower.Water, 5f) && Near(tower.Counterweight, 1f) && tower.Latched && Near(tower.Height, 6f),
        "automatic transfer partitions the original water and preserves the lift height");
    cargo.Tick(2f, tower, false);
    Check(cargo.HasFruit && Near(tower.Water, 5f) && Near(tower.Counterweight, 1f), "lost light pauses transfer without losing cargo or water");
    Check(cargo.TryLaunch(tower, true, true) == CargoActionResult.NeedCounterweight, "underfilled cargo cannot depart");
    cargo.Tick(5f, tower, true);
    Check(cargo.Phase == CargoPhase.Ready && Near(tower.Water, 2f) && Near(tower.Counterweight, 4f),
        "restored light resumes transfer and caps it at four of six units");
    Check(cargo.TryLaunch(tower, false, true) == CargoActionResult.NeedLight, "full ballast cannot depart in darkness");
    Check(cargo.TryLaunch(tower, true, false) == CargoActionResult.NeedBoarding, "departure requires a rider on the carrier");
    Turn(tower, Port.SunCourt, TurnDirection.Counterclockwise);
    Check(cargo.TryLaunch(tower, true, true) == CargoActionResult.NeedEastDock, "departure requires the east dock");
    Turn(tower, Port.None, TurnDirection.Counterclockwise);
    Check(tower.ToggleDrain(false) == MechanismResult.CounterweightCommitted && tower.Latched,
        "committed water prevents draining the locked tower");
    Turn(tower, Port.SunCourt);
    Turn(tower, Port.East);
    Check(cargo.TryLaunch(tower, true, true) == CargoActionResult.Applied, "fruit, live light, east dock, ballast and rider allow departure");
    Check(cargo.TryLaunch(tower, true, true) == CargoActionResult.AlreadyDeparted, "departure cannot be triggered twice");
    Check(cargo.TryDeliver(true) == CargoActionResult.NeedArrival, "delivery cannot be confirmed during travel");
    cargo.Tick(CargoJourney.TravelSeconds / 2f, tower, false);
    Check(cargo.Phase == CargoPhase.Travelling && Near(cargo.TravelProgress, .5f), "the carrier continues in darkness");
    cargo.Tick(CargoJourney.TravelSeconds, tower, false);
    Check(cargo.Phase == CargoPhase.Arrived && cargo.DeliveredCount == 0, "arrival in darkness still needs confirmation");
    Check(cargo.TryDeliver(false) == CargoActionResult.NeedDeliveryStation, "confirmation requires the delivery station");
    Check(cargo.TryDeliver(true) == CargoActionResult.Applied && cargo.DeliveredCount == 1 && !cargo.HasFruit,
        "one confirmation delivers the single crate");
    Check(cargo.TryDeliver(true) == CargoActionResult.AlreadyDelivered && cargo.DeliveredCount == 1, "delivery is counted once");
    tower.Reset();
    cargo.Reset();
    Check(tower.DockedPort == Port.Entry && tower.Water == 0f && tower.Counterweight == 0f && !tower.Latched
        && cargo.Phase == CargoPhase.WaitingForLight && cargo.LightProgress == 0f && cargo.TravelProgress == 0f
        && !cargo.HasFruit && cargo.DeliveredCount == 0, "reset clears the whole delivery loop");
}

void Raise(TowerMechanism tower)
{
    Check(Cast(tower, Weather.Rain) == MechanismResult.Applied, "rain is accepted before the high lock");
    StepTower(tower, 300);
}

void Turn(TowerMechanism tower, Port expected, TurnDirection direction = TurnDirection.Clockwise)
{
    Check(Cast(tower, Weather.Wind, direction) == MechanismResult.Applied, $"wind starts toward {expected} ({direction})");
    StepTower(tower, 135);
    Check(tower.DockedPort == expected && !tower.IsTurning, $"tower settles at {expected} ({direction})");
}

void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL " + name);
    Console.WriteLine("PASS " + name);
    assertions++;
}

static MechanismResult Cast(TowerMechanism tower, Weather weather,
    TurnDirection direction = TurnDirection.Clockwise, bool occupied = false) => tower.Cast(weather, occupied, direction);

static void StepTower(TowerMechanism tower, int frames)
{
    for (int frame = 0; frame < frames; frame++) tower.Tick(1f / 60f);
}

static Vector2? BlockAt(Vector2 from, Vector2 to, Vector2 point)
{
    Vector2 segment = to - from;
    float fraction = Vector2.Dot(point - from, segment) / segment.LengthSquared();
    if (fraction < 0f || fraction > 1.0001f) return null;
    return Vector2.DistanceSquared(from + segment * fraction, point) < .00001f ? point : null;
}

static bool Near(float a, float b) => Math.Abs(a - b) < .002f;
