using System;
using System.Collections.Generic;
using System.Numerics;
using BorrowingSun2D.Core;

int count = 0;
void Check(bool value, string name)
{
    if (!value) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name); count++;
}
bool Near(float a, float b) => Math.Abs(a - b) < 0.002f;
void Reject(Action action, string name)
{
    try { action(); } catch (ArgumentException) { Check(true, name); return; }
    throw new Exception("FAIL expected rejection: " + name);
}
void Raise(TowerMechanism tower)
{
    tower.Cast(Weather.Rain, false);
    for (int i = 0; i < 600; i++) tower.Tick(1f / 60f);
}
void Dock(TowerMechanism tower, Port port)
{
    Check(tower.RequestDock(port, false) == MechanismResult.Applied, "request " + port);
    for (int i = 0; i < 300; i++) tower.Tick(1f / 60f);
}

var tower = new TowerMechanism();
Check(tower.DockedPort == Port.Entry, "initial low entry");
Check(tower.Cast(Weather.Sun, false) == MechanismResult.Unsupported, "sun cannot become tower wind");
Check(tower.Cast(Weather.None, false) == MechanismResult.Unsupported, "none is not wind");
Check(tower.RequestDock(Port.Cargo, false) == MechanismResult.NeedHigh, "low tower refuses cargo turn");
Raise(tower);
Check(tower.Latched && Near(tower.Water, 6) && Near(tower.Height, 6), "full water high lock");
Check(tower.DockedPort == Port.None, "height alone gives no high bridge");
Check(tower.RequestDock(Port.Cargo, true) == MechanismResult.DoorwayOccupied, "occupied doorway blocks turn");
Check(tower.RequestDock(Port.None, false) == MechanismResult.Unsupported, "invalid target rejected");
Check(tower.Cast(Weather.Wind, false) == MechanismResult.Applied, "legacy wind retained");
tower.Tick(.5f);
Check(tower.IsTurning && tower.DockedPort == Port.None, "no bridge while turning");
tower.Recall();
float stopped = tower.Heading;
tower.Tick(1);
Check(Near(tower.Heading, stopped) && tower.DockedPort == Port.None, "recall does not invent docking");
Dock(tower, Port.SunCourt);
Check(tower.DockedPort == Port.SunCourt, "north stop");
Check(tower.TransferToBallast(1) == 0, "cannot transfer at north");
Dock(tower, Port.Cargo);
Check(tower.DockedPort == Port.Cargo, "east is distinct from north");
tower.TransferToBallast(2);
Check(Near(tower.Water, 4) && Near(tower.Ballast, 2), "transfer conserves water");
tower.Tick(2);
Check(Near(tower.Height, 6) && tower.Latched, "transfer keeps lock and height");
Check(tower.Cast(Weather.Rain, false) == MechanismResult.ConditionsNotMet, "no refill after transfer begins");
Check(tower.ToggleDrain(false) == MechanismResult.ConditionsNotMet, "ballast prevents destructive drain");
tower.TransferToBallast(100);
Check(Near(tower.Water, 0) && Near(tower.Ballast, 6), "transfer clamps to available water");
tower.Reset();
Check(tower.Ballast == 0 && tower.Water == 0 && tower.DockedPort == Port.Entry, "tower reset clears both reservoirs");
Reject(() => tower.Tick(float.NaN), "reject NaN delta");
Reject(() => tower.TransferToBallast(-1), "reject negative transfer delta");

var mirror = new SunMirror();
Check(mirror.NormalDegrees == 90 && !mirror.SunEnabled, "mirror starts uncalibrated");
mirror.Cast(Weather.Wind);
Check(mirror.NormalDegrees == 135, "wind selects next mirror detent");
mirror.Cast(Weather.Sun);
Check(mirror.SunEnabled && mirror.NormalDegrees == 135, "sun preserves mirror angle");
Check(mirror.Cast(Weather.Rain) == MechanismResult.Unsupported && mirror.SunEnabled, "invalid mirror command preserves state");
mirror.Recall();
Check(!mirror.SunEnabled && mirror.NormalDegrees == 135, "local recall preserves calibration");
mirror.Cast(Weather.Wind); mirror.Cast(Weather.Wind);
Check(mirror.AngleIndex == 0, "three detents cycle");

var receiver = new LightReceiver();
receiver.Tick(2.9f, true);
Check(!receiver.CargoUnlocked, "short illumination earns no cargo");
receiver.Tick(.1f, false);
Check(receiver.ContinuousSeconds == 0, "interruption resets dwell");
receiver.Tick(3, true);
Check(receiver.CargoUnlocked, "continuous light unlocks one cargo");
receiver.Tick(1, false);
Check(receiver.CargoUnlocked && !receiver.IsLit, "unlocked cargo survives lost light");
receiver.Reset();
Check(!receiver.CargoUnlocked, "reset clears reward");

var solver = new Optics2D();
var surfaces = new List<OpticalSurface>
{
    new(new(1.5f,-.5f),new(2.5f,.5f),new(-1,1),SurfaceKind.SunMirror),
    new(new(1.5f,1.5f),new(2.5f,2.5f),new(1,-1),SurfaceKind.TowerMirror),
    new(new(4,1.5f),new(4,2.5f),new(-1,0),SurfaceKind.Receiver)
};
LightPath Trace(bool sun = true) => solver.Trace(Vector2.Zero, Vector2.UnitX, surfaces, sun);
Check(Trace().ReceiverHit && Trace().Segments.Count == 3, "real two-mirror finite path hits receiver");
Check(!Trace(false).ReceiverHit && Trace(false).Segments.Count == 0, "sun off emits no light");
surfaces.Add(new(new(3,1),new(3,3),new(-1,0),SurfaceKind.Blocker));
Check(!Trace().ReceiverHit, "occlusion before receiver blocks path");
surfaces.RemoveAt(3);
surfaces[1] = surfaces[1] with { FrontNormal = new(-1,1) };
Check(!Trace().ReceiverHit, "back face cannot reflect");
surfaces[1] = surfaces[1] with { FrontNormal = new(1,-1) };
var missed = new List<OpticalSurface>(surfaces);
missed[0] = missed[0] with { A = new(2.5f,.5f), B = new(3.5f,1.5f) };
Check(!solver.Trace(Vector2.Zero, Vector2.UnitX, missed, true).ReceiverHit, "finite aperture miss");
Check(!solver.Trace(new(3,2),Vector2.UnitX,surfaces,true).ReceiverHit, "direct receiver hit cannot skip mirrors");
Check(!solver.Trace(Vector2.Zero,Vector2.UnitX,surfaces,true,2.5f).ReceiverHit, "ray range exhausted");
surfaces.Add(new(new(4,1.5f),new(4,2.5f),new(-1,0),SurfaceKind.Blocker));
Check(!Trace().ReceiverHit, "coincident blocker wins");
surfaces.Reverse();
Check(!Trace().ReceiverHit, "coincident blocker independent of ordering");
surfaces.Reverse(); surfaces.RemoveAt(3);
Reject(()=>solver.Trace(Vector2.Zero,Vector2.Zero,surfaces,true), "invalid direction rejected");
var bad = new List<OpticalSurface>(surfaces);
bad[0] = bad[0] with { FrontNormal = Vector2.UnitX };
Reject(()=>solver.Trace(Vector2.Zero,Vector2.UnitX,bad,true), "nonperpendicular normal rejected");

// Domain integration fixture: spatial facts are supplied explicitly, not a playable scene test.
var game = new GameSession();
Check(game.RequestTransfer() == MechanismResult.ConditionsNotMet, "transfer gated before unlock");
Raise(game.Tower);
Dock(game.Tower, Port.Cargo);
game.Mirror.Cast(Weather.Wind); game.Mirror.Cast(Weather.Sun);
game.Resolve(3, Trace().ReceiverHit);
Check(game.Receiver.CargoUnlocked, "geometry hit feeds reward rule");
Check(game.RequestTransfer() == MechanismResult.Applied, "explicit transfer request");
game.Resolve(2, true);
Check(Near(game.Tower.Ballast, 2), "session transfer");
game.Resolve(1, false);
Check(Near(game.Tower.Ballast, 2) && game.TransferRequested, "lost light pauses without losing water or request");
Check(game.TryDepart(true) == DeliveryResult.NoLight, "lost light blocks departure");
game.Resolve(1, true);
Check(game.TryDepart(true) == DeliveryResult.NeedBallast, "insufficient ballast blocks departure");
game.Resolve(3, true);
Check(game.TryDepart(false) == DeliveryResult.NeedPassenger, "passenger required");
Check(game.TryDepart(true) == DeliveryResult.Applied, "departure accepted");
Check(game.TryDepart(true) == DeliveryResult.WrongPhase, "cannot depart twice");
game.Mirror.Recall();
game.Resolve(12, false);
Check(game.Delivery.Phase == DeliveryPhase.Arrived, "departed cart arrives despite lost light");
Check(game.Delivery.ConfirmDelivery(true,true) == DeliveryResult.NeedDisembark, "must get off at station");
Check(game.Delivery.ConfirmDelivery(false,false) == DeliveryResult.NeedDisembark, "must be at station");
Check(game.Delivery.ConfirmDelivery(true,false) == DeliveryResult.Applied, "delivery confirmation");
Check(game.Delivery.ConfirmDelivery(true,false) == DeliveryResult.WrongPhase && game.Delivery.DeliveredCount == 1, "delivery exactly once");
game.Reset();
Check(game.Tower.Water == 0 && game.Tower.Ballast == 0 && !game.Receiver.CargoUnlocked
    && !game.Mirror.SunEnabled && game.Delivery.DeliveredCount == 0 && !game.TransferRequested, "global domain reset");
for (int run = 0; run < 2; run++)
{
    Raise(game.Tower); Dock(game.Tower,Port.Cargo); game.Mirror.Cast(Weather.Sun);
    game.Resolve(3,true); game.RequestTransfer(); game.Resolve(6,true);
    Check(game.TryDepart(true) == DeliveryResult.Applied, "repeat complete domain run departure");
    game.Resolve(12,false); game.Delivery.ConfirmDelivery(true,false);
    Check(game.Delivery.DeliveredCount == 1, "repeat complete domain run delivery");
    game.Reset();
}
Console.WriteLine($"DOMAIN_CHECKS PASSED: {count} assertions; spatial integration fixture, not full gameplay.");
