using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    /// <summary>
    /// Temporary-keyboard checks for the authored rain-cargo revision. The main delivery
    /// route is walked continuously; only the explicitly labelled initial probes teleport.
    /// </summary>
    public static class RainCargoPlayChecks
    {
        private static RainCargoController court;
        private static IEnumerator sequence;
        private static Keyboard keyboard, previousKeyboard;
        private static KeyboardState requested;
        private static bool pending;
        private static int requestedSerial, queuedSerial, queuedFrame, dynamicUpdates;
        private static bool observeKey, lastObservedHeld;
        private static Key observedKey;
        private static int observedDownEdges, observedHeldUpdates, observedUpEdges, lastDownDynamic;
        private static readonly List<string> observedKeyFrames = new List<string>();
        private static Vector3 offset;
        private static readonly List<string> report = new List<string>();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation/rain-cargo"));

        [MenuItem("Borrowing Sun/Rain Cargo/Run actual-input delivery checks")]
        public static void Run()
        {
            if (sequence != null) throw new InvalidOperationException("Rain cargo checks already running.");
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.timeScale <= 0)
                throw new InvalidOperationException("Use unpaused Play Mode with a positive time scale.");
            court = UnityEngine.Object.FindAnyObjectByType<RainCargoController>();
            if (court == null || court.Player == null) throw new InvalidOperationException("RainCargoController is required.");
            offset = court.Bindings.offset;
            report.Clear();
            report.Add("Rain cargo revision / real Input System keyboard / " + DateTime.UtcNow.ToString("O"));
            report.Add("The court is reset before the main delivery and on exit. Initial isolated S1/edge probes teleport; the main delivery walks continuously from spawn.");
            report.Add("This checks physical routes, state effects and controls. It does not establish first-player comprehension or puzzle quality.");
            report.Add($"INITIAL: player={court.Player.FeetPosition:F3}, A={court.State.WaterA:F3}, B={court.State.WaterB:F3}, tank={court.State.TankWater:F3}, cart={court.State.CartStop}, card={court.State.CardStation}.");
            Directory.CreateDirectory(Output);
            previousKeyboard = Keyboard.current;
            requestedSerial = queuedSerial = dynamicUpdates = 0;
            queuedFrame = -1;
            try
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (window.GetType().Name == "GameView") { window.Focus(); break; }
                keyboard = InputSystem.AddDevice<Keyboard>("RainCargoValidationKeyboard");
                keyboard.MakeCurrent();
                InputSystem.onBeforeUpdate += BeforeInput;
                InputSystem.onAfterUpdate += AfterInput;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                sequence = Check();
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish(false, e); }
        }

        private static IEnumerator Check()
        {
            court.ResetCourt();
            foreach (var step in Wait(.25f)) yield return step;
            Require(court.Player.PuzzleEdgeProtectionEnabled, "New scene enables puzzle edge protection");
            Require(court.CameraRig.FollowViewEnabled && !court.CameraRig.IsOverview && !court.IsObserving && court.CameraRig.ViewCamera.orthographicSize <= 11,
                "New scene starts in the close follow camera");

            // Isolated rule probe, not evidence that the upper plaza is reachable before A is raised.
            court.Player.Teleport(World(4, 18.06f, -15.5f));
            foreach (var step in Wait(.2f)) yield return step;
            Require(court.Prompt().Contains("集雨亭"), "Isolated upper station probe reaches the proper interaction");
            report.Add("S1 BEFORE INPUT: " + InputDiagnostic());
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.CardStation == 1, "S1 Q must insert the unique card; " + InputDiagnostic());
            foreach (var step in Press(Key.E)) yield return step;
            Require(court.State.RainActive && court.State.CardStation == 1, "S1 E must start rain with its inserted card; " + InputDiagnostic());
            foreach (var step in Wait(.35f)) yield return step;
            report.Add("S1 AFTER RAIN: " + InputDiagnostic());
            Require(court.State.RainToWaste && court.State.TankWater == 0 && court.Bindings.rainBeam.gameObject.activeInHierarchy,
                "Rain at the upper outlet cannot fill an absent or low cart");
            foreach (var step in Press(Key.Q)) yield return step;
            court.Player.Teleport(World(-2.8f, 15.06f, -1.6f));
            foreach (var step in Wait(.2f)) yield return step;
            foreach (var step in PressIntoEdge(Vector3.back, 1.2f, "Low A cannot walk into the unsupported upper-plaza connector")) yield return step;

            court.ResetCourt();
            foreach (var step in Wait(.25f)) yield return step;
            report.Add("MAIN DELIVERY START: no more Teleport calls until cleanup; follows the authored player route.");
            foreach (var step in Walk(World(-16, 15, -2), 5, "Spawn to the cart side")) yield return step;
            Require(court.Prompt().Contains("接雨升降台"), "Cart prompt names the next spatial destination");
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.CartStop == 0 && !court.State.CartInMotion, "Q at the initial cart stop stays at the route start");
            foreach (var step in MoveCart(1, 1)) yield return step;
            foreach (var step in Walk(World(-6, 15, -1.8f), 5, "Player crosses the low A west dock beside the cart")) yield return step;
            foreach (var step in Walk(World(-12, 15, -1.8f), 4, "Return from low A to the west court")) yield return step;
            foreach (var step in Walk(World(-19, 15, 3.2f), 5, "West court to the local rain station")) yield return step;
            Require(court.Prompt().Contains("升台雨亭"), "S0 is reached by walking from the cargo platform");
            foreach (var step in Press(Key.Q)) yield return step;
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.IsAHigh, 8, "S0 raises A with the cart aboard")) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.CardStation == -1 && court.State.TankWater == 0 && Mathf.Abs(court.Bindings.cart.position.y - offset.y - 18) < .05f,
                "Taking back the rain card preserves raised A and its empty cart");

            foreach (var step in Walk(World(-23, 15, 1), 4, "West court outside the upper-ramp side rail")) yield return step;
            foreach (var step in Walk(World(-23, 15, -5), 4, "Approach the open low end of the upper ramp")) yield return step;
            foreach (var step in Walk(World(-22, 15, -5), 3, "Upper-ramp entrance")) yield return step;
            foreach (var step in Walk(World(-10, 18, -5), 7, "West upper-ramp ascent")) yield return step;
            foreach (var step in Walk(World(-9.2f, 18, -5), 3, "High west landing turn")) yield return step;
            foreach (var step in Walk(World(-9.2f, 18, -1.8f), 4, "Walk from the high landing onto A")) yield return step;
            foreach (var step in Walk(World(-2.8f, 18, -1.8f), 5, "Raised A becomes a route to the upper rain plaza")) yield return step;
            foreach (var step in Walk(World(-2.8f, 18, -10), 5, "Upper connector to the rain plaza")) yield return step;
            foreach (var step in Walk(World(4, 18, -15.5f), 5, "Upper plaza to S1 control side")) yield return step;
            Require(court.Prompt().Contains("集雨亭"), "S1 is reached by the actual gated player route");
            foreach (var step in Press(Key.Q)) yield return step;
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.TankFull, 8, "Upper outlet fills the aligned cart tank")) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.IsAHigh && court.State.CardStation == -1 && !court.State.RainActive && court.State.TankFull,
                "Removing the S1 card leaves a full portable tank on high A");
            Require(!court.State.CanMoveCart(1, out _) && court.Bindings.routePreview.enabled,
                "The full cart still cannot cross the lower cargo dock until A descends");

            foreach (var step in Walk(World(7, 18, -15.5f), 3, "Pass around the S1 window pillar")) yield return step;
            foreach (var step in Walk(World(7, 18, -18.5f), 4, "Upper plaza to the shortcut lever")) yield return step;
            Require(court.Prompt().Contains("检修门"), "Shortcut lever is reachable from its upper side");
            foreach (var step in Press(Key.E)) yield return step;
            Require(court.ShortcutOpen && !court.Bindings.shortcutGate.gameObject.activeInHierarchy,
                "E opens the physical shortcut gate");
            foreach (var step in Walk(World(8.3f, 18, -18.5f), 3, "Pass beside the shortcut lever stand")) yield return step;
            foreach (var step in Walk(World(8.5f, 18, -23.1f), 4, "Plaza around the fence end to the shortcut upper landing")) yield return step;
            foreach (var step in Walk(World(12, 18, -23.1f), 4, "Pass behind the shortcut ramp side-rail endpoint")) yield return step;
            foreach (var step in Walk(World(12, 18, -22), 3, "Shortcut ramp open upper entrance")) yield return step;
            foreach (var step in Walk(World(12, 15, -12), 6, "Continuous descent through the unlocked shortcut")) yield return step;
            foreach (var step in Walk(World(12, 15, -5.5f), 5, "Shortcut lower corridor")) yield return step;
            foreach (var step in Walk(World(6, 15, -5.5f), 4, "Lower link to the A drain")) yield return step;
            Require(court.Prompt().Contains("接雨台排水阀"), "A drain is accessible after the shortcut descent");
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.IsALow, 8, "A descends without discarding cart water")) yield return step;
            Require(court.State.TankFull && court.State.CardStation == -1, "Stored rain survives the return to cargo height");
            foreach (var step in Walk(World(0, 15, -4.7f), 4, "Return through the middle cargo court")) yield return step;
            foreach (var step in Walk(World(-.8f, 15, -1.8f), 3, "Approach A's low eastern connection")) yield return step;
            foreach (var step in Walk(World(-6, 15, -1.8f), 4, "Return to the full cart on low A")) yield return step;
            foreach (var step in MoveCart(1, 2)) yield return step;
            foreach (var step in Walk(World(4, 15, -1.8f), 5, "Follow the full cart across A to the tunnel approach")) yield return step;
            foreach (var step in Walk(World(2.2f, 15, -1.8f), 3, "Stand west of the cart's freight-ramp turn")) yield return step;
            foreach (var step in MoveCart(1, 3)) yield return step;
            Require(court.State.TankFull && court.Player.FeetPosition.y-offset.y<12.8f,
                "The player continuously pushes the full cart down into the covered route");
            // The actor is already following behind the cart on the descending ramp.
            // A second E now owns the turn and approach; the player need not manually clear a precise safety zone.
            foreach (var step in MoveCart(1, 4)) yield return step;
            foreach (var step in Walk(World(12, 12, 16), 5, "Walk through the covered freight street")) yield return step;
            Require(court.Bindings.tunnelRoofs != null && court.Bindings.tunnelRoofs.Length > 0, "Tunnel has authored camera cutaway roof renderers");
            foreach (var roof in court.Bindings.tunnelRoofs) Require(roof != null && !roof.enabled, "Covered street roof is cut away while the player is underneath", false);
            report.Add("PASS: All authored tunnel roofs are cut away while the player is inside; screenshot is separate visual evidence.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "tunnel-game.png"));
            foreach (var step in Walk(World(20, 12, 16), 5, "Emerge at the eastern counterweight approach")) yield return step;
            foreach (var step in MoveCart(1, 5)) yield return step;
            foreach (var step in Walk(World(28, 12, 16), 5, "Player crosses onto low B beside the full cart")) yield return step;
            Require(court.Support(court.Player.FeetPosition) == 5, "Player stands on the moving B platform before unloading");
            foreach (var step in MoveCart(-1, 4)) yield return step;
            foreach (var step in Walk(World(20, 12, 16), 5, "Follow the reversible cart back off low B")) yield return step;
            Require(court.State.TankFull && court.State.IsBLow, "Reversing the loaded cart preserves its rain");
            foreach (var step in MoveCart(1, 5)) yield return step;
            foreach (var step in Walk(World(28, 12, 16), 5, "Return to B for the delivery lift")) yield return step;
            Require(court.Prompt().Contains("卸水搭乘"), "Platform prompt offers the actual tank-to-counterweight action");
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in RideB()) yield return step;
            Require(court.State.TankWater < .001f && court.State.IsBHigh && Mathf.Abs(court.Player.FeetPosition.y - offset.y - 15) < .15f,
                "Tank water is transferred to B and the real player rides to the upper dock");
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.CartStop == 5 && !court.State.CartInMotion, "A raised B blocks reversal into the low cargo street");
            foreach (var step in MoveCart(1, 6)) yield return step;
            foreach (var step in Walk(World(36, 15, 16), 5, "Walk from raised B onto the east upper dock")) yield return step;
            foreach (var step in Walk(World(34, 15, 16), 3, "Stand beside the cart's east-dock turn")) yield return step;
            foreach (var step in MoveCart(1, 7)) yield return step;
            foreach (var step in Walk(World(34, 15, 30), 7, "Final approach through the east freight terrace")) yield return step;
            Require(court.State.Completed && court.State.CartStop == 7 && court.State.CardStation == -1,
                "All eight cargo stops complete with the rain card recovered");
            foreach (var step in Press(Key.V)) yield return step;
            Require(court.CameraRig.IsOverview, "V opens the district overview in the new scene");
            foreach (var step in Press(Key.Escape)) yield return step;
            Require(!court.CameraRig.IsOverview && !court.IsObserving && court.CameraRig.ViewCamera.orthographicSize <= 11,
                "Escape returns to the close gameplay view");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "completion-game.png"));
            foreach (var step in Wait(.3f)) yield return step;
            foreach (var step in Press(Key.R)) yield return step;
            Require(court.State.CartStop == 0 && court.State.IsALow && court.State.IsBLow && court.State.TankWater == 0 &&
                court.State.CardStation == -1 && !court.ShortcutOpen && court.Bindings.shortcutGate.gameObject.activeInHierarchy,
                "R resets cart, both lifts, portable water, rain card and shortcut gate");
        }

        private static IEnumerable<object> MoveCart(int direction, int expected)
        {
            Require(!court.State.CartInMotion && !court.IsCartAssisting, "Cart must be stationary before an input move", false);
            Require(Vector3.ProjectOnPlane(court.Player.FeetPosition-court.Bindings.cart.position,Vector3.up).magnitude<4,
                "Player is close enough to operate the cart: "+court.Prompt(),false);
            int old = court.State.CartStop;
            Vector3 actorStart = court.Player.FeetPosition;
            Vector3 lastActor = actorStart;
            float maxActorStep = 0;
            bool sawFollowing = false;
            foreach (var step in Press(direction > 0 ? Key.E : Key.Q))
            {
                CheckFollowingPlayer(ref lastActor, ref maxActorStep, old, expected);
                sawFollowing |= court.Player.IsPushingCart;
                yield return step;
            }
            foreach (var step in Until(() => !court.IsCartAssisting && !court.State.CartInMotion && court.State.CartStop == expected, 24, "Cart " + old + " -> " + expected))
            {
                CheckFollowingPlayer(ref lastActor, ref maxActorStep, old, expected);
                sawFollowing |= court.Player.IsPushingCart;
                yield return step;
            }
            CheckFollowingPlayer(ref lastActor, ref maxActorStep, old, expected);
            Require(sawFollowing && !court.Player.IsAutoPositioning && !court.Player.IsPushingCart && Vector3.Distance(actorStart,court.Player.FeetPosition)>1,
                "A push follows continuously to its stop and then releases manual movement",false);
            report.Add($"PASS: {(direction > 0 ? "E" : "Q")} assists cart {old} -> {expected}; local={court.Bindings.cart.position-offset:F3}, tank={court.State.TankWater:F3}, playerStart={actorStart-offset:F3}, playerEnd={court.Player.FeetPosition-offset:F3}, maxActorStep={maxActorStep:F3}.");
            // Subsequent puzzle walks leave the handle sideways first, rather than cutting
            // diagonally through the cart. At stop3, the next E handles the narrow ramp-end turn.
            if(expected!=3)
            {
                Vector3 side=court.Player.FeetPosition;side.y=court.Bindings.stops[expected].position.y;
                if(expected==7)side.x=court.Bindings.stops[expected].position.x-2;
                else side.z=court.Bindings.stops[expected].position.z-1.8f;
                foreach(var step in Walk(side,4,"Leave the pushing handle for the next puzzle walk"))yield return step;
            }
        }

        private static void CheckFollowingPlayer(ref Vector3 last, ref float maximum, int from, int to)
        {
            Vector3 now=court.Player.FeetPosition;maximum=Mathf.Max(maximum,Vector3.Distance(last,now));
            Require(maximum<1.2f,$"Cart {from}->{to} caused a teleport/recovery discontinuity of {maximum:F3}m; local={now-offset:F3}",false);
            bool supported=false;
            foreach(var hit in Physics.RaycastAll(now+Vector3.up*.4f,Vector3.down,1.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                Transform t=hit.transform;
                if(t==court.Player.transform||t.IsChildOf(court.Player.transform)||t==court.Bindings.cart||t.IsChildOf(court.Bindings.cart)||hit.normal.y<.65f)continue;
                if(Mathf.Abs(now.y-hit.point.y)<.28f)supported=true;
            }
            Require(supported,$"Cart {from}->{to} left the pushing actor without real floor support; local={now-offset:F3}",false);last=now;
        }

        private static IEnumerable<object> RideB()
        {
            double end = EditorApplication.timeSinceStartup + 8;
            Vector3 last = court.Player.FeetPosition;
            float maxError = 0, maxStep = 0;
            int frame = Time.frameCount;
            while (!court.State.IsBHigh || court.State.Transferring)
            {
                Require(EditorApplication.timeSinceStartup <= end, "B transfer/ride timed out", false);
                Vector3 now = court.Player.FeetPosition;
                maxError = Mathf.Max(maxError, Mathf.Abs(now.y - offset.y - 12 - court.State.WaterB));
                maxStep = Mathf.Max(maxStep, Vector3.Distance(last, now));
                Require(maxError < .15f && maxStep < 1.2f, "Player lost moving-platform support during transfer", false);
                last = now; yield return null;
            }
            report.Add($"PASS: B lift ride; {Time.frameCount-frame} frames, max height error={maxError:F3}, max step={maxStep:F3}.");
        }

        private static IEnumerable<object> PressIntoEdge(Vector3 direction, float seconds, string label)
        {
            Vector3 start = court.Player.FeetPosition, last = start;
            float minY = start.y, maxStep = 0;
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end)
            {
                SetWorldDirection(direction); yield return null;
                Vector3 now = court.Player.FeetPosition;
                minY = Mathf.Min(minY, now.y); maxStep = Mathf.Max(maxStep, Vector3.Distance(last, now)); last = now;
            }
            SetInput(); foreach (var step in Wait(.12f)) yield return step;
            Require(minY > start.y - .23f && maxStep < 1.2f, label + " failed: dropping or recovery discontinuity", false);
            report.Add($"PASS: {label}; minY={minY:F3}, max step={maxStep:F3}, end={court.Player.FeetPosition-offset:F3}.");
        }

        private static IEnumerable<object> Walk(Vector3 target, float timeout, string label)
        {
            Vector3 start = court.Player.FeetPosition, last = start;
            Vector3 segment = Vector3.ProjectOnPlane(target - start, Vector3.up);
            float length = segment.magnitude, maxCross = 0;
            Vector3 tangent = segment.normalized;
            double deadline = EditorApplication.timeSinceStartup + timeout;
            int frame = Time.frameCount;
            while (Vector3.ProjectOnPlane(target - court.Player.FeetPosition, Vector3.up).magnitude > .17f)
            {
                Vector3 now = court.Player.FeetPosition;
                Vector3 from = Vector3.ProjectOnPlane(now - start, Vector3.up);
                float along = Vector3.Dot(from, tangent);
                Vector3 cross = from - tangent * along;
                maxCross = Mathf.Max(maxCross, cross.magnitude);
                float expectedY = Mathf.Lerp(start.y, target.y, length > .001f ? Mathf.Clamp01(along / length) : 1);
                string detail = $"{label}; local={now-offset:F3}, expectedY={expectedY:F3}, lateral={cross.magnitude:F3}, prompt={court.Prompt()}";
                Require(EditorApplication.timeSinceStartup <= deadline, "Timeout: " + detail, false);
                Require(Vector3.Distance(now, last) < 1.2f, "Teleport/recovery: " + detail, false);
                Require(Mathf.Abs(now.y - expectedY) < .25f, "Off walking surface: " + detail, false);
                Vector3 ahead = start + tangent * Mathf.Clamp(along + .45f, 0, length);
                SetWorldDirection((Vector3.ProjectOnPlane(ahead - now, Vector3.up) - cross * 2).normalized);
                last = now;
                foreach (var step in Wait(.025f)) yield return step;
            }
            SetInput(); foreach (var step in Wait(.12f)) yield return step;
            report.Add($"WALK: {label}; frames={Time.frameCount-frame}, distance={length:F2}m, max lateral={maxCross:F3}, end={court.Player.FeetPosition-offset:F3}.");
            File.WriteAllLines(Path.Combine(Output, "playmode-running.txt"), report);
        }

        private static void SetWorldDirection(Vector3 desired)
        {
            Transform cam = court.CameraRig.ViewCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            float best = float.NegativeInfinity; int bx = 0, by = 0;
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                float alignment = Vector3.Dot((right * x + forward * y).normalized, desired);
                if (alignment <= best) continue; best = alignment; bx = x; by = y;
            }
            var keys = new List<Key>();
            if (bx != 0) keys.Add(bx > 0 ? Key.D : Key.A);
            if (by != 0) keys.Add(by > 0 ? Key.W : Key.S);
            SetInput(keys.ToArray());
        }

        private static void Tick()
        {
            if (sequence == null) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Play Mode stopped or paused.");
                if (sequence.MoveNext()) return;
                Finish(true, null);
            }
            catch (Exception e) { Finish(false, e); }
        }
        private static void Finish(bool passed, Exception error)
        {
            EditorApplication.update -= Tick; InputSystem.onBeforeUpdate -= BeforeInput; InputSystem.onAfterUpdate -= AfterInput;
            AssemblyReloadEvents.beforeAssemblyReload -= Abort; EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            sequence = null; pending = false; observeKey = false;
            try
            {
                if (keyboard != null && keyboard.added)
                { InputState.Change(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard); }
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                if (court != null) { court.ResetCourt(); report.Add("CLEANUP: rain cargo court reset to its authored initial state."); }
            }
            catch (Exception cleanup) { report.Add("CLEANUP FAILED: " + cleanup); passed = false; }
            keyboard = previousKeyboard = null;
            if (error != null) report.Add("FAIL: " + error.Message);
            report.Add(passed ? "RAIN_CARGO_PLAYMODE_PASSED" : "RAIN_CARGO_PLAYMODE_FAILED");
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Path.Combine(Output, "playmode-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"), report);
            File.WriteAllLines(Path.Combine(Output, "playmode.txt"), report);
            if (passed) Debug.Log("RAIN_CARGO_PLAYMODE_PASSED");
            else Debug.LogError("RAIN_CARGO_PLAYMODE_FAILED: " + (error?.Message ?? "cleanup failure"));
        }
        private static void Abort() => Finish(false, new InvalidOperationException("Assembly reload interrupted validation."));
        private static void OnPlayModeChanged(PlayModeStateChange value)
        { if (sequence != null && value == PlayModeStateChange.ExitingPlayMode) Finish(false, new InvalidOperationException("Play Mode exited.")); }
        private static void BeforeInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            dynamicUpdates++;
            if (!pending || keyboard == null || !keyboard.added) return;
            InputSystem.QueueStateEvent(keyboard, requested);
            queuedSerial = requestedSerial;
            queuedFrame = Time.frameCount;
            pending = false;
        }
        private static void AfterInput()
        {
            if (!observeKey || keyboard == null || !keyboard.added || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            var control = keyboard[observedKey];
            bool held = control.isPressed, down = control.wasPressedThisFrame, up = control.wasReleasedThisFrame;
            if (held) observedHeldUpdates++;
            if (down && lastDownDynamic != dynamicUpdates) { observedDownEdges++; lastDownDynamic = dynamicUpdates; }
            if (up) observedUpEdges++;
            if ((held != lastObservedHeld || down || up) && observedKeyFrames.Count < 10)
                observedKeyFrames.Add($"frame{Time.frameCount}/dyn{dynamicUpdates}: held={held}, down={down}, up={up}");
            lastObservedHeld = held;
        }
        private static void SetInput(params Key[] keys)
        { requested = new KeyboardState(keys); requestedSerial++; pending = true; }
        private static IEnumerable<object> Press(Key key, float hold = .12f)
        {
            int beforeFrame = Time.frameCount, beforeDynamic = dynamicUpdates;
            string before = InputDiagnostic();
            observedKey = key; observeKey = true; lastObservedHeld = false;
            observedDownEdges = observedHeldUpdates = observedUpEdges = 0; lastDownDynamic = -1; observedKeyFrames.Clear();
            SetInput(key);
            foreach (var step in WaitForInput(requestedSerial, hold, key + " press")) yield return step;
            // EditorApplication.update reads InputSystem's separate editor state buffer.
            // Observe the processed event in Dynamic instead of inspecting the editor buffer
            // after a wall-time hold; that buffer can report false while gameplay received Q.
            Require(observedDownEdges == 1 && observedHeldUpdates > 0,
                $"Synthetic {key} must produce exactly one processed press edge; edges={observedDownEdges}, heldUpdates={observedHeldUpdates}; {string.Join("; ", observedKeyFrames)}; " + InputDiagnostic(), false);
            SetInput();
            foreach (var step in WaitForInput(requestedSerial, .12f, key + " release")) yield return step;
            observeKey = false;
            Require(observedDownEdges == 1, "Synthetic " + key + " generated another press edge before release; " + string.Join("; ", observedKeyFrames), false);
            Require(!lastObservedHeld && observedUpEdges >= 1,
                "Synthetic " + key + " did not release in the gameplay input buffer; " + string.Join("; ", observedKeyFrames), false);
            report.Add($"INPUT {key}: gameFrames={Time.frameCount-beforeFrame}, dynamicUpdates={dynamicUpdates-beforeDynamic}, downEdges={observedDownEdges}, heldUpdates={observedHeldUpdates}, upEdges={observedUpEdges}; events=[{string.Join("; ", observedKeyFrames)}]; before=[{before}]; after=[{InputDiagnostic()}].");
            File.WriteAllLines(Path.Combine(Output, "playmode-running.txt"), report);
        }
        private static IEnumerable<object> WaitForInput(int serial, float minimumSeconds, string label)
        {
            double start = EditorApplication.timeSinceStartup;
            int firstFrame = Time.frameCount;
            // Wall time alone can elapse while Unity is importing/rendering without a game Update.
            // Require the event to enter a Dynamic update and at least two real game frames to run.
            while (queuedSerial < serial || Time.frameCount < firstFrame + 2 || EditorApplication.timeSinceStartup - start < minimumSeconds)
            {
                Require(EditorApplication.timeSinceStartup - start < 10,
                    label + " did not reach real game updates within 10 seconds; " + InputDiagnostic(), false);
                yield return null;
            }
        }
        private static string InputDiagnostic()
        {
            if (court == null) return "controller absent";
            var s = court.State;
            bool beam = court.Bindings.rainBeam != null && court.Bindings.rainBeam.gameObject.activeInHierarchy;
            return $"frame={Time.frameCount}, dyn={dynamicUpdates}, queued={queuedSerial}/{requestedSerial}@{queuedFrame}, focused={Application.isFocused}, runBackground={Application.runInBackground}, deviceEnabled={keyboard?.enabled}, tempCurrent={Keyboard.current==keyboard}, " +
                $"Q={keyboard?.qKey.isPressed}, E={keyboard?.eKey.isPressed}, card={s.CardStation}, rain={s.RainActive}, waste={s.RainToWaste}, beam={beam}, " +
                $"A={s.WaterA:F3}, B={s.WaterB:F3}, tank={s.TankWater:F3}, cart={s.CartStop}, local={court.Player.FeetPosition-offset:F3}, " +
                $"prompt={court.Prompt()}, message={court.Message}";
        }
        private static IEnumerable<object> Wait(float seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) yield return null; }
        private static IEnumerable<object> Until(Func<bool> test, float timeout, string label)
        {
            double deadline = EditorApplication.timeSinceStartup + timeout;
            while (!test())
            { Require(EditorApplication.timeSinceStartup <= deadline, label + " timed out; " + court.Message, false); yield return null; }
        }
        private static Vector3 World(float x, float y, float z) => offset + new Vector3(x, y, z);
        private static void Require(bool condition, string message, bool log = true)
        { if (!condition) throw new InvalidOperationException(message); if (log) report.Add("PASS: " + message); }
    }
}
