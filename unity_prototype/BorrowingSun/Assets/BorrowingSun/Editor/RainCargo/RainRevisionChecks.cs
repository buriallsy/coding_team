using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BorrowingSun.Editor
{
    /// <summary>
    /// Regression probes for the first player's cliff/seam report. This runner deliberately
    /// tests unsupported endpoints, not just the centreline of an already solved cargo route.
    /// It operates on a temporary state object and restores the user's stable runtime state.
    /// </summary>
    public static class RainRevisionChecks
    {
        private static IEnumerator sequence;
        private static BorrowedRainController court;
        private static Keyboard keyboard, previousKeyboard;
        private static KeyboardState requested;
        private static bool pending;
        private static Vector3 offset;
        private static bool reproduce;
        private static readonly List<string> report = new List<string>();
        private static Dictionary<FieldInfo, object> controllerFields, playerFields, cameraFields;
        private static Vector3 playerPosition, cameraPosition;
        private static Quaternion playerRotation, cameraRotation, cartRotation;
        private static float cameraSize, cameraFov;
        private static bool cameraOrtho;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation/borrowed-rain-v2"));
        private const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [MenuItem("Borrowing Sun/Borrowed Rain/Revision edge regression checks")]
        public static void RunEdgeChecks() => Start(true);

        public static void RunProtectedChecks() => Start(false);

        private static void Start(bool includeUnprotectedBaseline)
        {
            if (sequence != null) throw new InvalidOperationException("Revision checks are already running.");
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.timeScale <= 0)
                throw new InvalidOperationException("Use unpaused Play Mode with a positive time scale.");
            court = UnityEngine.Object.FindAnyObjectByType<BorrowedRainController>();
            if (court == null || court.Player == null || court.CameraRig == null)
                throw new InvalidOperationException("Borrowed Rain runtime is required.");
            if (court.State.CartInMotion || court.State.WaterInMotion)
                throw new InvalidOperationException("Wait for the cart and water to stop before preserving the current state.");
            offset = court.Bindings.worldOffset;
            reproduce = includeUnprotectedBaseline;
            report.Clear();
            report.Add("Rain revision / actual temporary Input System keyboard / " + DateTime.UtcNow.ToString("O"));
            report.Add("Original logical state and player/camera transforms are preserved. Particle histories are not preserved.");
            report.Add("State setup and probe starts are teleported; each measured edge/ramp/crossing uses actual CourtPlayer.Update and CharacterController.Move.");
            report.Add("This probes movement safety and camera modes, not first-player comprehension or whether the puzzle is enjoyable.");
            report.Add($"BASELINE: A={court.State.WaterA:F3}, B={court.State.WaterB:F3}, card={court.State.CardStation}, cart={court.State.CartStop}, rain={court.State.RainActive}, player={court.Player.FeetPosition:F3}.");
            controllerFields = CaptureFields(court);
            playerFields = CaptureFields(court.Player);
            cameraFields = CaptureFields(court.CameraRig);
            playerPosition = court.Player.FeetPosition;
            playerRotation = court.Player.transform.rotation;
            cartRotation = court.CartTransform.rotation;
            Camera cam = court.CameraRig.ViewCamera;
            cameraPosition = cam.transform.position; cameraRotation = cam.transform.rotation;
            cameraSize = cam.orthographicSize; cameraFov = cam.fieldOfView; cameraOrtho = cam.orthographic;
            Directory.CreateDirectory(Output);
            previousKeyboard = Keyboard.current;
            try
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (window.GetType().Name == "GameView") { window.Focus(); break; }
                keyboard = InputSystem.AddDevice<Keyboard>("RainRevisionValidationKeyboard");
                keyboard.MakeCurrent();
                InputSystem.onBeforeUpdate += BeforeInput;
                AssemblyReloadEvents.beforeAssemblyReload += Abort;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                sequence = Check();
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish(false, e); }
        }

        private static IEnumerator Check()
        {
            SetPuzzle(true, false);
            court.CameraRig.ConfigureFollowView(court.Player, 10f, .18f);
            foreach (var step in Wait(.15f)) yield return step;
            if (reproduce)
            {
                court.Player.ConfigurePuzzleEdgeProtection(false);
                foreach (var step in Edge(World(-5.25f, 18.05f, -.65f), Vector3.left, true, "Old A high west endpoint")) yield return step;
                foreach (var step in Edge(World(-.75f, 18.05f, -.65f), Vector3.right, true, "Old A high east endpoint")) yield return step;
                foreach (var step in Edge(World(1.25f, 15.05f, -.65f), Vector3.right, true, "Old middle landing towards low B")) yield return step;
            }
            else report.Add("NOT RUN: unprotected baseline; use RunEdgeChecks on the preserved old geometry to reproduce it.");

            court.Player.ConfigurePuzzleEdgeProtection(true);
            foreach (var step in Edge(World(-5.25f, 18.05f, -.65f), Vector3.left, false, "Protected A high west endpoint")) yield return step;
            foreach (var step in Edge(World(-.75f, 18.05f, -.65f), Vector3.right, false, "Protected A high east endpoint")) yield return step;
            foreach (var step in Edge(World(1.25f, 15.05f, -.65f), Vector3.right, false, "Protected middle landing towards low B")) yield return step;
            foreach (var step in Edge(World(-.75f, 18.05f, -.65f), (Vector3.right + Vector3.back).normalized, false, "Protected A corner with diagonal pressure")) yield return step;
            foreach (var step in Edge(World(1.25f, 15.05f, -.65f), (Vector3.right + Vector3.back).normalized, false, "Protected middle corner with diagonal pressure")) yield return step;

            // Restore a genuinely connected cargo route. Edge protection must not become an invisible wall.
            SetPuzzle(false, true);
            court.Player.Teleport(World(-5.2f, 15.06f, -.9f));
            foreach (var step in Wait(.2f)) yield return step;
            foreach (var step in Walk(World(7.5f, 15, -.9f), 8, "A low / B high connected cargo crossings")) yield return step;
            court.Player.Teleport(World(-14.8f, 15.06f, -6));
            foreach (var step in Wait(.2f)) yield return step;
            foreach (var step in Walk(World(-3.2f, 17.95f, -6), 7, "Protected fixed observation ramp ascent")) yield return step;
            foreach (var step in Walk(World(-14.8f, 15.05f, -6), 7, "Protected fixed observation ramp descent")) yield return step;

            court.Player.Teleport(World(-10.5f, 15.06f, -2));
            court.CameraRig.ConfigureFollowView(court.Player, 10, .18f);
            foreach (var step in Wait(.2f)) yield return step;
            Require(!court.CameraRig.IsOverview && !court.IsObserving && Mathf.Abs(court.CameraRig.ViewCamera.orthographicSize - 10) < .01f,
                "Configured gameplay view is close orthographic size 10, not the inspection overview");
            Vector3 beforeCamera = court.CameraRig.ViewCamera.transform.position;
            foreach (var step in Walk(World(-8, 15, -2), 3, "Close camera follows a real walk")) yield return step;
            foreach (var step in Wait(.4f)) yield return step;
            Require(Vector3.Distance(beforeCamera, court.CameraRig.ViewCamera.transform.position) > 1.5f,
                "Near camera follows the moving player");
            foreach (var step in Press(Key.V)) yield return step;
            Require(court.CameraRig.IsOverview && !court.IsObserving && court.CameraRig.ViewCamera.orthographicSize > 13,
                "V selects the separate wide overview");
            foreach (var step in Press(Key.Escape)) yield return step;
            Require(!court.CameraRig.IsOverview && !court.IsObserving && Mathf.Abs(court.CameraRig.ViewCamera.orthographicSize - 10) < .01f,
                "Escape restores the close gameplay view");
            foreach (var step in Press(Key.C)) yield return step;
            Require(court.IsObserving && !court.CameraRig.ViewCamera.orthographic, "C selects the gutter observation view");
            Vector3 observationFeet = court.Player.FeetPosition;
            foreach (var step in Press(Key.W, .25f)) yield return step;
            Require(Vector3.Distance(observationFeet, court.Player.FeetPosition) < .08f,
                "Movement stays paused while in the observation view");
            foreach (var step in Press(Key.Escape)) yield return step;
            Require(!court.IsObserving && !court.CameraRig.IsOverview, "Escape from observation returns to close play");
        }

        private static IEnumerable<object> Edge(Vector3 start, Vector3 direction, bool expectFall, string label)
        {
            SetInput();
            court.Player.Teleport(start);
            foreach (var step in Wait(.18f)) yield return step;
            Vector3 actualStart = court.Player.FeetPosition, last = actualStart;
            float minY = last.y, maxStep = 0, horizontalTravel = 0;
            int firstFrame = Time.frameCount;
            double until = EditorApplication.timeSinceStartup + (expectFall ? 1.5f : 2f);
            bool fell = false;
            while (EditorApplication.timeSinceStartup < until)
            {
                SetWorldDirection(direction);
                yield return null;
                Vector3 now = court.Player.FeetPosition;
                maxStep = Mathf.Max(maxStep, Vector3.Distance(now, last));
                horizontalTravel += Vector3.ProjectOnPlane(now - last, Vector3.up).magnitude;
                minY = Mathf.Min(minY, now.y);
                last = now;
                if (minY < actualStart.y - .45f) { fell = true; if (expectFall) break; }
            }
            SetInput();
            foreach (var step in Wait(.12f)) yield return step;
            Require(Time.frameCount - firstFrame >= 5, label + " received actual game frames", false);
            Require(horizontalTravel > .18f, label + " received actual movement input", false);
            if (expectFall) Require(fell, label + " reproduces unsupported falling with protection disabled", false);
            else
            {
                Require(!fell && minY > actualStart.y - .23f, label + " did not maintain its walking height", false);
                Require(maxStep < 1.2f, label + " had a teleport/recovery discontinuity", false);
            }
            report.Add($"{(expectFall ? "REPRODUCED" : "PASS")}: {label}; frames={Time.frameCount-firstFrame}, path={horizontalTravel:F2}m, minY={minY:F3}, maxStep={maxStep:F3}, localStart={actualStart-offset:F3}, localEnd={court.Player.FeetPosition-offset:F3}.");
        }

        private static IEnumerable<object> Walk(Vector3 target, float timeout, string label)
        {
            Vector3 start = court.Player.FeetPosition, last = start;
            Vector3 segment = Vector3.ProjectOnPlane(target - start, Vector3.up);
            float length = segment.magnitude, maxCross = 0;
            Vector3 tangent = segment.normalized;
            double deadline = EditorApplication.timeSinceStartup + timeout;
            int firstFrame = Time.frameCount;
            while (Vector3.ProjectOnPlane(target - court.Player.FeetPosition, Vector3.up).magnitude > .17f)
            {
                Vector3 now = court.Player.FeetPosition;
                Vector3 fromStart = Vector3.ProjectOnPlane(now - start, Vector3.up);
                float along = Vector3.Dot(fromStart, tangent);
                Vector3 cross = fromStart - tangent * along;
                maxCross = Mathf.Max(maxCross, cross.magnitude);
                float expectedY = Mathf.Lerp(start.y, target.y, Mathf.Clamp01(along / length));
                string diagnostic = $"{label}; local={now-offset:F3}, expectedY={expectedY:F3}, lateral={cross.magnitude:F3}";
                Require(EditorApplication.timeSinceStartup <= deadline, "Timeout: " + diagnostic, false);
                Require(Vector3.Distance(now, last) < 1.2f, "Teleport/recovery discontinuity: " + diagnostic, false);
                Require(Mathf.Abs(now.y - expectedY) < .25f, "Off walking surface: " + diagnostic, false);
                Vector3 ahead = start + tangent * Mathf.Clamp(along + .45f, 0, length);
                SetWorldDirection((Vector3.ProjectOnPlane(ahead - now, Vector3.up) - cross * 2).normalized);
                last = now;
                foreach (var step in Wait(.025f)) yield return step;
            }
            SetInput();
            foreach (var step in Wait(.12f)) yield return step;
            report.Add($"PASS: {label}; {Time.frameCount-firstFrame} game frames, {length:F2}m, max lateral={maxCross:F3}, endpoint={court.Player.FeetPosition-offset:F3}.");
        }

        private static void SetPuzzle(bool aHigh, bool bHigh)
        {
            var state = new BorrowedRainState();
            if (aHigh || bHigh)
            {
                state.InsertCard(0); state.ToggleRain(0); state.Advance(10); state.RemoveCard(0);
            }
            if (bHigh)
            {
                state.InsertCard(1); state.ToggleRain(1); state.Advance(10); state.RemoveCard(1);
            }
            if (!aHigh) { state.ToggleDrain(0); state.Advance(10); state.ToggleDrain(0); }
            typeof(BorrowedRainController).GetField("state", PrivateFields).SetValue(court, state);
            court.Simulate(0);
            Physics.SyncTransforms();
        }

        private static void SetWorldDirection(Vector3 desired)
        {
            Transform cam = court.CameraRig.ViewCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            float best = float.NegativeInfinity;
            int bestX = 0, bestY = 0;
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
            EditorApplication.update -= Tick;
            InputSystem.onBeforeUpdate -= BeforeInput;
            AssemblyReloadEvents.beforeAssemblyReload -= Abort;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            sequence = null; pending = false;
            try
            {
                if (keyboard != null && keyboard.added)
                {
                    InputState.Change(keyboard, new KeyboardState());
                    InputSystem.RemoveDevice(keyboard);
                }
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                if (court != null)
                {
                    RestoreFields(court, controllerFields);
                    court.Simulate(0);
                    court.CartTransform.rotation = cartRotation;
                    court.Player.Teleport(playerPosition);
                    court.Player.transform.rotation = playerRotation;
                    RestoreFields(court.Player, playerFields);
                    RestoreFields(court.CameraRig, cameraFields);
                    var cam = court.CameraRig.ViewCamera;
                    cam.orthographic = cameraOrtho; cam.orthographicSize = cameraSize; cam.fieldOfView = cameraFov;
                    cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                    Physics.SyncTransforms();
                    report.Add($"RESTORED: A={court.State.WaterA:F3}, B={court.State.WaterB:F3}, card={court.State.CardStation}, cart={court.State.CartStop}, player={court.Player.FeetPosition:F3}.");
                }
            }
            catch (Exception cleanup) { report.Add("CLEANUP FAILED: " + cleanup); passed = false; }
            keyboard = null; previousKeyboard = null;
            if (error != null) report.Add("FAIL: " + error.Message);
            report.Add(passed ? "RAIN_REVISION_EDGE_PASSED" : "RAIN_REVISION_EDGE_FAILED");
            Directory.CreateDirectory(Output);
            string name = "edge-runtime-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt";
            File.WriteAllLines(Path.Combine(Output, name), report);
            File.WriteAllLines(Path.Combine(Output, "edge-runtime-latest.txt"), report);
            if (passed) Debug.Log("RAIN_REVISION_EDGE_PASSED");
            else Debug.LogError("RAIN_REVISION_EDGE_FAILED: " + (error?.Message ?? "cleanup failed"));
        }

        private static Dictionary<FieldInfo, object> CaptureFields(object owner)
        {
            var result = new Dictionary<FieldInfo, object>();
            foreach (var field in owner.GetType().GetFields(PrivateFields))
                if (!field.IsInitOnly) result.Add(field, field.GetValue(owner));
            return result;
        }
        private static void RestoreFields(object owner, Dictionary<FieldInfo, object> saved)
        { if (saved != null) foreach (var item in saved) item.Key.SetValue(owner, item.Value); }
        private static void Abort() => Finish(false, new InvalidOperationException("Assembly reload interrupted validation."));
        private static void OnPlayModeChanged(PlayModeStateChange value)
        { if (sequence != null && value == PlayModeStateChange.ExitingPlayMode) Finish(false, new InvalidOperationException("Play Mode exited.")); }
        private static void BeforeInput()
        {
            if (!pending || keyboard == null || !keyboard.added || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            InputSystem.QueueStateEvent(keyboard, requested); pending = false;
        }
        private static void SetInput(params Key[] keys) { requested = new KeyboardState(keys); pending = true; }
        private static IEnumerable<object> Press(Key key, float hold = .12f)
        {
            SetInput(key); foreach (var step in Wait(hold)) yield return step;
            SetInput(); foreach (var step in Wait(.12f)) yield return step;
        }
        private static IEnumerable<object> Wait(float seconds)
        { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) yield return null; }
        private static Vector3 World(float x, float y, float z) => offset + new Vector3(x, y, z);
        private static void Require(bool condition, string message, bool log = true)
        { if (!condition) throw new InvalidOperationException(message); if (log) report.Add("PASS: " + message); }
    }
}
