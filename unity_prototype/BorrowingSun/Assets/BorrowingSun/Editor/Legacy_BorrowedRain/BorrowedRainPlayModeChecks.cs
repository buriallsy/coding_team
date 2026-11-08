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
    /// <summary>Uses a temporary keyboard through actual player Update. The new scene is reset on every exit path.</summary>
    public static class BorrowedRainPlayModeChecks
    {
        private static IEnumerator sequence;
        private static BorrowedRainController court;
        private static Keyboard testKeyboard, previousKeyboard;
        private static KeyboardState requested;
        private static bool pending;
        private static readonly List<string> Report = new List<string>();
        private static Vector3 offset;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation/borrowed-rain"));

        [MenuItem("Borrowing Sun/Borrowed Rain/Run Play Mode interaction checks")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Enter unpaused Play Mode in the Borrowed Rain scene first.");
            if (sequence != null) throw new InvalidOperationException("Borrowed Rain checks are already running.");
            court = UnityEngine.Object.FindAnyObjectByType<BorrowedRainController>();
            if (court == null) throw new InvalidOperationException("Borrowed Rain controller is absent; the water-court scene is not affected.");
            offset = court.Bindings.worldOffset;
            Report.Clear();
            Report.Add("Borrowed Rain / actual Input System and player Update / " + DateTime.UtcNow.ToString("O"));
            Report.Add("Interaction positions are teleported to isolate controls. Separate real walking probes cross the A endpoint exit and the completed 16 m cargo route.");
            Report.Add("This is automated runtime evidence, not a first-player comprehension test.");
            Directory.CreateDirectory(Output);
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetType().Name == "GameView") { window.Focus(); break; }
            previousKeyboard = Keyboard.current;
            try
            {
                testKeyboard = InputSystem.AddDevice<Keyboard>("BorrowedRainValidationKeyboard");
                testKeyboard.MakeCurrent();
                InputSystem.onBeforeUpdate += BeforeInputUpdate;
                AssemblyReloadEvents.beforeAssemblyReload += AbortForReload;
                EditorApplication.playModeStateChanged += OnPlayModeChange;
                sequence = Check();
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish(false, e); }
        }

        private static void Tick()
        {
            if (sequence == null) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                    throw new InvalidOperationException("Play Mode stopped or paused during the check.");
                if (sequence.MoveNext()) return;
                Finish(true, null);
            }
            catch (Exception e) { Finish(false, e); }
        }

        private static void AbortForReload() => Finish(false, new InvalidOperationException("Assembly reload interrupted the check."));
        private static void OnPlayModeChange(PlayModeStateChange state)
        {
            if (sequence != null && state == PlayModeStateChange.ExitingPlayMode)
                Finish(false, new InvalidOperationException("Play Mode exited before validation completed."));
        }

        private static void Finish(bool passed, Exception error)
        {
            EditorApplication.update -= Tick;
            InputSystem.onBeforeUpdate -= BeforeInputUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= AbortForReload;
            EditorApplication.playModeStateChanged -= OnPlayModeChange;
            sequence = null;
            pending = false;
            // Never queue a release that depends on a future Update after unregistering the callback.
            // This isolated device is synchronously cleared and removed; the physical keyboard is untouched.
            try
            {
                if (testKeyboard != null && testKeyboard.added)
                {
                    InputState.Change(testKeyboard, new KeyboardState());
                    InputSystem.RemoveDevice(testKeyboard);
                }
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            }
            catch (Exception cleanup) { Report.Add("CLEANUP: " + cleanup.Message); passed = false; }
            finally
            {
                testKeyboard = null; previousKeyboard = null;
                try { if (court != null) court.ResetCourt(); }
                catch (Exception cleanup) { Report.Add("RESET FAILED: " + cleanup.Message); passed = false; }
            }
            if (error != null) Report.Add("FAIL: " + error.Message);
            Report.Add(passed ? "BORROWED_RAIN_PLAYMODE_PASSED" : "BORROWED_RAIN_PLAYMODE_FAILED");
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Path.Combine(Output, "playmode-interactions.txt"), Report);
            if (passed) Debug.Log("BORROWED_RAIN_PLAYMODE_PASSED");
            else if (error != null) Debug.LogException(error);
            else Debug.LogError("BORROWED_RAIN_PLAYMODE_FAILED: cleanup failure");
        }

        private static IEnumerator Check()
        {
            court.ResetCourt();
            foreach (var step in Wait(.3f)) yield return step;
            var font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(font != null && font.HasCharacter('借') && font.HasCharacter('雨') && font.HasCharacter('槽'),
                "Bundled Chinese HUD font is available at runtime");
            Vector3 start = court.Player.FeetPosition;
            foreach (var step in Press(Key.W, .4f)) yield return step;
            Require(Vector3.Distance(start, court.Player.FeetPosition) > .6f, "Synthetic W reaches the real CharacterController Update");
            foreach (var step in Press(Key.R)) yield return step;
            Require(Vector3.Distance(court.Player.FeetPosition, court.Player.SpawnPosition) < .3f, "R resets to the new scene's spawn");

            // Both pools are still low: prove the observation platform and the opposite bank
            // remain reachable through fixed geometry before any puzzle has been solved.
            court.Player.Teleport(World(-14.8f, 15.05f, -4.25f));
            foreach (var step in Wait(.2f)) yield return step;
            foreach (var step in WalkTo(World(-16, 15, -4.25f), 4, "Observation landing west bypass")) yield return step;
            foreach (var step in WalkTo(World(-16, 15, -6), 4, "Observation landing behind the ramp handrail end")) yield return step;
            foreach (var step in WalkTo(World(-15, 15, -6), 4, "Observation ramp entrance through its open west end")) yield return step;
            foreach (var step in WalkTo(World(-3, 18, -6), 10, "Fixed upper observation ramp")) yield return step;
            foreach (var step in WalkTo(World(-2, 18, -6), 4, "Upper observation ramp open east endpoint")) yield return step;
            foreach (var step in WalkTo(World(-2, 18, -3.5f), 4, "Upper observation landing beyond the side handrail")) yield return step;
            foreach (var step in WalkTo(World(-3, 18, -3.5f), 4, "Upper observation spur turn")) yield return step;
            foreach (var step in WalkTo(World(-3, 18, -2.3f), 4, "Upper observation spur")) yield return step;
            Require(court.State.IsALow && court.State.IsBLow && Mathf.Abs(court.Player.FeetPosition.y - 18) < .18f,
                "The high gutter observation platform is walkable while both water systems are off");
            court.Player.Teleport(World(-7, 15.05f, -2.5f));
            foreach (var step in Wait(.2f)) yield return step;
            foreach (var step in WalkTo(World(-7, 15, -4.25f), 4, "West fixed court to maintenance gallery")) yield return step;
            foreach (var step in WalkTo(World(-3, 15, -4.25f), 5, "West upper gallery")) yield return step;
            foreach (var step in WalkTo(World(-2.2f, 15, -5.75f), 4, "West lower-ramp landing east bypass")) yield return step;
            foreach (var step in WalkTo(World(-2.2f, 15, -9), 5, "West lower-ramp access outside the side handrail")) yield return step;
            foreach (var step in WalkTo(World(-3, 15, -9), 4, "West descending ramp open high endpoint")) yield return step;
            foreach (var step in WalkTo(World(-15, 12, -9), 10, "West descending inspection ramp")) yield return step;
            foreach (var step in WalkTo(World(-16, 12, -9), 4, "West ramp bottom open endpoint")) yield return step;
            foreach (var step in WalkTo(World(-16, 12, -11.5f), 4, "West ramp bottom turning landing")) yield return step;
            foreach (var step in WalkTo(World(13, 12, -11.5f), 15, "Continuous lower inspection corridor")) yield return step;
            foreach (var step in WalkTo(World(13, 12, -9), 4, "East ramp bottom landing")) yield return step;
            foreach (var step in WalkTo(World(13, 15, 3), 10, "East ascending inspection ramp")) yield return step;
            foreach (var step in WalkTo(World(13, 15, 4), 4, "East ramp landing beyond the side handrail")) yield return step;
            foreach (var step in WalkTo(World(11, 15, 4), 4, "East fixed-bank access")) yield return step;
            Require(court.State.IsALow && court.State.IsBLow && court.State.CardStation == -1,
                "West-to-east maintenance traversal succeeds with both cargo gaps disconnected and no weather card installed");

            foreach (var step in Approach(court.Bindings.station1)) yield return step;
            Require(court.GetInteractionPrompt().Contains("S1"), "S1 is reachable and has the correct interaction prompt");
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.CardStation == 1, "Q inserts the unique rain card in S1");
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Wait(.8f)) yield return step;
            Require(court.State.RainToWaste && court.State.IsALow && court.State.IsBLow,
                "Trying remote rain with low A produces waste instead of filling B");
            Require(court.Bindings.wasteFlow != null && court.Bindings.wasteFlow.gameObject.activeInHierarchy,
                "Wrong-height rain has visible waste-flow feedback");
            foreach (var step in Press(Key.C)) yield return step;
            Require(court.IsObserving, "C opens the gutter observation camera");
            foreach (var step in Press(Key.Escape)) yield return step;
            Require(!court.IsObserving, "Escape restores the main camera");
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.CardStation == -1 && !court.State.RainActive, "Q recovers the card and stops remote rain");

            foreach (var step in Approach(court.Bindings.station0)) yield return step;
            Require(court.GetInteractionPrompt().Contains("S0"), "S0 is reachable from its control side");
            foreach (var step in Press(Key.Q)) yield return step;
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.WaterA > .65f, 4, "Partial A rise")) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            float partialA = court.State.WaterA;
            foreach (var step in Wait(.4f)) yield return step;
            Require(partialA > .5f && partialA < 2.6f && Mathf.Abs(court.State.WaterA - partialA) < .01f,
                "Removing the local card leaves A at an intermediate water level");
            // Near A's east end the isolated middle refuge used to be the closest recovery point.
            court.Player.Teleport(World(-.5f, 15 + partialA + .05f, 0));
            foreach (var step in Wait(.15f)) yield return step;
            Require(court.GetInteractionPrompt().Contains("安全返回"), "A stopped midway offers an explicit recovery action");
            foreach (var step in Press(Key.E)) yield return step;
            Require(BorrowedRainController.DetermineSupport(court.Player.FeetPosition, court.State.WaterA, court.State.WaterB, offset) == BorrowedRainSupport.None,
                "E returns an intermediate-height rider to fixed infrastructure");
            Require(IsMainBank(court.Player.FeetPosition), "A east-end recovery reaches the maintenance network instead of an isolated middle refuge");
            Require(Mathf.Abs(court.State.WaterA - partialA) < .01f && court.State.CardStation == -1 && court.State.CartStop == 0,
                "Safe return preserves water, card and cart state");
            court.Player.Teleport(World(0, -5, 0));
            foreach (var step in Wait(.35f)) yield return step;
            Require(court.Player.FeetPosition.y > 11.7f && Mathf.Abs(court.State.WaterA - partialA) < .01f,
                "Falling out recovers to fixed ground without clearing the puzzle");

            foreach (var step in Approach(court.Bindings.station0)) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            foreach (var step in Press(Key.E)) yield return step;
            court.Player.Teleport(World(-3, 15 + court.State.WaterA + .05f, -.8f));
            foreach (var step in Until(() => court.State.IsAHigh, 7, "A reaches high endpoint")) yield return step;
            foreach (var step in Wait(.15f)) yield return step;
            Require(Mathf.Abs(court.Player.FeetPosition.y - 18) < .13f, "The real player rides A during active simulation");
            foreach (var step in WalkTo(World(-3, 18, -2.3f), 4, "A high endpoint exit")) yield return step;
            Require(court.Player.FeetPosition.z < offset.z - 1.6f && Mathf.Abs(court.Player.FeetPosition.y - 18) < .18f,
                "Player walks off high A onto its fixed side landing without a jump or teleport");

            foreach (var step in Approach(court.Bindings.station0)) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.IsAHigh && court.State.CardStation == -1, "Recovering S0 rain keeps A raised");
            foreach (var step in Approach(court.Bindings.station1)) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.WaterB > .65f, 4, "Partial B rise")) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            float partialB = court.State.WaterB;
            court.Player.Teleport(World(4, 12 + partialB + .05f, 0));
            foreach (var step in Wait(.15f)) yield return step;
            Require(court.GetInteractionPrompt().Contains("安全返回"), "B stopped midway offers the same explicit recovery action");
            foreach (var step in Press(Key.E)) yield return step;
            Require(IsMainBank(court.Player.FeetPosition) && Mathf.Abs(court.State.WaterB - partialB) < .01f,
                "B intermediate-height recovery reaches a connected main bank and preserves B water");
            court.Player.Teleport(World(1, 15.05f, 3.5f));
            foreach (var step in Wait(.15f)) yield return step;
            Require(court.GetInteractionPrompt().Contains("安全返回"), "The isolated middle refuge offers recovery when both cargo seams are disconnected");
            foreach (var step in Press(Key.E)) yield return step;
            Require(IsMainBank(court.Player.FeetPosition) && court.State.IsAHigh && Mathf.Abs(court.State.WaterB - partialB) < .01f && court.State.CardStation == -1,
                "Middle-refuge E returns to the fixed maintenance network without clearing earned water or the card");
            foreach (var step in Approach(court.Bindings.station1)) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.IsBHigh, 7, "Remote gutter fills B")) yield return step;
            Require(court.State.IsAHigh && court.State.RainToB && court.Bindings.successfulFlow != null &&
                court.Bindings.successfulFlow.gameObject.activeInHierarchy, "Raised gutter now visibly directs rain into B");
            foreach (var step in Press(Key.E)) yield return step;
            Require(!court.State.RainActive && court.State.CardStation == 1, "Stopping rain can leave the card in its socket");
            foreach (var step in Approach(court.Bindings.valveA)) yield return step;
            Require(court.GetInteractionPrompt().Contains("A 池"), "A drain has a reachable, basin-specific prompt");
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.IsALow, 7, "Independent A drain")) yield return step;
            Require(court.State.IsBHigh, "A drains while the previously earned B height remains");

            // Cart world half-width .70 + player radius .30 + skin .03 requires >1.03 m separation.
            // The 3 m bridge leaves only a narrow passing strip, explicitly exercised here.
            court.Player.Teleport(World(-8, 15.05f, -1.12f));
            foreach (var step in Wait(.2f)) yield return step;
            foreach (var step in WalkTo(World(8, 15, -1.12f), 12, "Completed cargo route, west to east")) yield return step;
            Require(court.Player.FeetPosition.x > offset.x + 7.6f && Mathf.Abs(court.Player.FeetPosition.y - 15) < .18f,
                "Player actually crosses A, the middle landing and B using keyboard movement");
            foreach (var step in MoveCart(1, 1)) yield return step;
            foreach (var step in MoveCart(-1, 0)) yield return step;
            foreach (var step in MoveCart(1, 1)) yield return step;
            foreach (var step in MoveCart(1, 2)) yield return step;
            foreach (var step in MoveCart(1, 3)) yield return step;
            foreach (var step in MoveCart(1, 4)) yield return step;
            foreach (var step in MoveCart(1, 5)) yield return step;
            Require(court.State.Delivered && !court.State.Completed, "Keyboard-driven delivery retains the rain-card objective");
            foreach (var step in Approach(court.Bindings.station1)) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.Completed, "Recovering the card after delivery completes the level");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "hud-completion.png"));
            foreach (var step in Wait(.3f)) yield return step;
            foreach (var step in Press(Key.R)) yield return step;
            Require(court.State.IsALow && court.State.IsBLow && court.State.CardStation == -1 && court.State.CartStop == 0 &&
                !court.State.Completed && !court.State.RainActive && !court.State.CartInMotion,
                "Runtime R fully resets the completed middle-level state");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "hud-reset.png"));
            foreach (var step in Wait(.3f)) yield return step;
        }

        private static IEnumerable<object> Approach(Transform target)
        {
            // S1 sits beside the south rail; 1.2 m south would place the actor inside that rail.
            Vector3 p = target.position + Vector3.back * (target == court.Bindings.station1 ? .75f : 1.2f);
            p.y = offset.y + 15.06f;
            court.Player.Teleport(p);
            foreach (var step in Wait(.2f)) yield return step;
        }

        private static IEnumerable<object> MoveCart(int direction, int expected)
        {
            Vector3 p = court.CartTransform.position + Vector3.back * 1.12f + Vector3.up * .06f;
            court.Player.Teleport(p);
            foreach (var step in Wait(.15f)) yield return step;
            Require(court.GetInteractionPrompt().Contains("苗车"), "Cart has a direct interaction at stop " + court.State.CartStop);
            foreach (var step in Press(direction > 0 ? Key.E : Key.Q)) yield return step;
            foreach (var step in Until(() => !court.State.CartInMotion && court.State.CartStop == expected, 8, "Cart reaches " + expected)) yield return step;
            Require(court.State.CartStop == expected, (direction > 0 ? "E advances" : "Q reverses") + " the cart to stop " + expected);
        }

        private static IEnumerable<object> WalkTo(Vector3 target, float timeout, string label)
        {
            double end = EditorApplication.timeSinceStartup + timeout;
            Vector3 start = court.Player.FeetPosition, last = start;
            Vector3 horizontalSegment = Vector3.ProjectOnPlane(target - start, Vector3.up);
            float segmentLength = horizontalSegment.magnitude;
            Vector3 tangent = segmentLength > .001f ? horizontalSegment / segmentLength : Vector3.zero;
            float travelled = 0, maximumCrossTrack = 0;
            int firstFrame = Time.frameCount;
            while (Vector2.Distance(new Vector2(court.Player.FeetPosition.x, court.Player.FeetPosition.z), new Vector2(target.x, target.z)) > .16f)
            {
                Vector3 now = court.Player.FeetPosition;
                float stepDistance = Vector3.Distance(now, last);
                Vector3 horizontalFromStart = Vector3.ProjectOnPlane(now - start, Vector3.up);
                float along = Vector3.Dot(horizontalFromStart, tangent);
                Vector3 crossTrack = horizontalFromStart - tangent * along;
                maximumCrossTrack = Mathf.Max(maximumCrossTrack, crossTrack.magnitude);
                float fraction = segmentLength > .001f ? Mathf.Clamp01(along / segmentLength) : 1;
                float expectedHeight = Mathf.Lerp(start.y, target.y, fraction);
                string diagnostic = $" world={now:F3}, local={now-offset:F3}, expectedY={expectedHeight:F3}, crossTrack={crossTrack.magnitude:F3}, maxCrossTrack={maximumCrossTrack:F3}, step={stepDistance:F3}, A={court.State.WaterA:F3}, B={court.State.WaterB:F3}, cart={court.CartTransform.position-offset:F3}";
                if (EditorApplication.timeSinceStartup > end)
                    throw new InvalidOperationException(label + " timed out;" + diagnostic);
                Require(stepDistance < 1.2f, label + " had no teleport/recovery discontinuity;" + diagnostic, false);
                Require(Mathf.Abs(now.y - expectedHeight) < .23f, label + " remains on the authored walking surface;" + diagnostic, false);
                travelled += stepDistance; last = now;
                // A camera-relative eight-way heading rarely aligns exactly with a world-space path.
                // Steering at the distant endpoint accumulated metres of drift on long bridges.
                // Keep the original authored segment, look only 0.45 m ahead and correct lateral error.
                Vector3 ahead = start + tangent * Mathf.Clamp(along + .45f, 0, segmentLength);
                Vector3 desired = (Vector3.ProjectOnPlane(ahead - now, Vector3.up) - crossTrack * 2).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(court.CameraRig.ViewCamera.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(court.CameraRig.ViewCamera.transform.right, Vector3.up).normalized;
                float best = float.NegativeInfinity; int bestX = 0, bestY = 0;
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    float alignment = Vector3.Dot((right * x + forward * y).normalized, desired);
                    if (alignment <= best) continue;
                    best = alignment; bestX = x; bestY = y;
                }
                var keys = new List<Key>();
                if (bestX != 0) keys.Add(bestX > 0 ? Key.D : Key.A);
                if (bestY != 0) keys.Add(bestY > 0 ? Key.W : Key.S);
                SetInput(keys.ToArray());
                foreach (var step in Wait(.025f)) yield return step;
            }
            SetInput();
            foreach (var step in Wait(.12f)) yield return step;
            Report.Add($"WALK: {label}; {Time.frameCount - firstFrame} game frames, {travelled:F2} m travelled, max lateral deviation {maximumCrossTrack:F3} m; {start:F3} -> {court.Player.FeetPosition:F3}.");
        }

        private static IEnumerable<object> Press(Key key, float hold = .12f)
        {
            SetInput(key);
            foreach (var step in Wait(hold)) yield return step;
            SetInput();
            foreach (var step in Wait(.12f)) yield return step;
        }
        private static void SetInput(params Key[] keys) { requested = new KeyboardState(keys); pending = true; }
        private static void BeforeInputUpdate()
        {
            if (!pending || testKeyboard == null || !testKeyboard.added || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            InputSystem.QueueStateEvent(testKeyboard, requested);
            pending = false;
        }
        private static IEnumerable<object> Wait(float seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end) yield return null;
        }
        private static IEnumerable<object> Until(Func<bool> condition, float timeout, string label)
        {
            double end = EditorApplication.timeSinceStartup + timeout;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > end) throw new InvalidOperationException(label + " timed out.");
                yield return null;
            }
        }
        private static Vector3 World(float x, float y, float z) => offset + new Vector3(x, y, z);
        private static bool IsMainBank(Vector3 position)
        {
            Vector3 local = position - offset;
            return (local.x < -6.1f || local.x > 6.1f) && Mathf.Abs(local.y - 15) < .2f;
        }
        private static void Require(bool condition, string message, bool log = true)
        {
            if (!condition) throw new InvalidOperationException(message);
            if (log) Report.Add("PASS: " + message);
        }
    }
}
