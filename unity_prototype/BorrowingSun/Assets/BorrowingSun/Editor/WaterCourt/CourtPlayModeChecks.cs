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
    // Runs the real player Update/input path. Teleports between stations to isolate interactions
    // from route finding; it does not claim to replace a person's unassisted playthrough.
    public static class CourtPlayModeChecks
    {
        private static IEnumerator sequence;
        private static CourtController court;
        private static KeyboardState requestedInput;
        private static bool inputPending;
        private static readonly List<string> report = new List<string>();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation"));

        [MenuItem("Borrowing Sun/Run Play Mode interaction checks")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Enter unpaused Play Mode in 01_WaterCourt first.");
            if (sequence != null) throw new InvalidOperationException("An interaction check is already running.");
            court = UnityEngine.Object.FindAnyObjectByType<CourtController>();
            if (court == null || Keyboard.current == null) throw new InvalidOperationException("Court and keyboard are required.");
            report.Clear();
            report.Add("Play Mode / Input System keyboard state events / " + DateTime.UtcNow.ToString("O"));
            report.Add("Positions between stations are teleported; route finding is not tested.");
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetType().Name == "GameView") { window.Focus(); break; }
            InputSystem.onBeforeUpdate += BeforeInputUpdate;
            sequence = Check();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode ended during the check.");
                if (sequence.MoveNext()) return;
                Finish(true, null);
            }
            catch (Exception e) { Finish(false, e); }
        }

        private static void Finish(bool passed, Exception error)
        {
            EditorApplication.update -= Tick;
            sequence = null;
            Release();
            InputSystem.onBeforeUpdate -= BeforeInputUpdate;
            if (court != null) court.ResetCourt();
            if (error != null) report.Add("FAIL: " + error.Message);
            report.Add(passed ? "PLAYMODE_INTERACTIONS_PASSED" : "PLAYMODE_INTERACTIONS_FAILED");
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Path.Combine(Output, "playmode-interactions.txt"), report);
            if (passed) Debug.Log("COURT_PLAYMODE_INTERACTIONS_PASSED");
            else Debug.LogException(error);
        }

        private static IEnumerator Check()
        {
            court.ResetCourt();
            foreach (var step in WaitSeconds(.3f)) yield return step;
            var font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(font != null && font.HasCharacter('借') && font.HasCharacter('晴') && font.HasCharacter('雨'), "Bundled Chinese font contains the HUD glyphs");

            Vector3 origin = court.Player.FeetPosition;
            int frame = Time.frameCount;
            foreach (var step in Press(Key.W, .5f)) yield return step;
            report.Add($"Movement probe: frames {Time.frameCount-frame}, from {origin:F3} to {court.Player.FeetPosition:F3}, player enabled {court.Player.enabled}, keyboard enabled {Keyboard.current.enabled}, app focus {Application.isFocused}.");
            Require(Vector3.Distance(origin, court.Player.FeetPosition) > 1f, "W moves the CharacterController in actual Update");
            foreach (var step in Press(Key.R)) yield return step;
            Require(Vector3.Distance(court.Player.FeetPosition, court.Player.SpawnPosition) < .3f, "R restores the spawn");

            foreach (var step in MoveCart(new Vector3(0, .1f, -9), 1)) yield return step;
            court.Player.Teleport(new Vector3(-5, .1f, -8.6f));
            foreach (var step in WaitSeconds(.15f)) yield return step;
            Require(court.GetInteractionPrompt().Contains("Q"), "Nearby weather pavilion offers Q");
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.CardInserted, "Q inserts the rain card");
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.IsHigh, 7, "Rain fills the lift")) yield return step;
            Require(Mathf.Abs(court.CartTransform.position.y - 3) < .05f, "Lower deck carries the cart to 3m");
            foreach (var step in Press(Key.C)) yield return step;
            Require(court.IsObserving, "C selects pavilion observation");
            foreach (var step in Press(Key.Escape)) yield return step;
            Require(!court.IsObserving, "Esc restores the main view");

            foreach (var step in MoveCart(new Vector3(0, 3.1f, 1.8f), 2)) yield return step;
            foreach (var step in MoveCart(new Vector3(1.7f, 3.1f, 5), 3)) yield return step;
            foreach (var step in MoveCart(new Vector3(-5.5f, 3.1f, 6.6f), 4)) yield return step;

            court.Player.Teleport(new Vector3(-2.5f, 3.1f, 4.6f));
            foreach (var step in WaitSeconds(.15f)) yield return step;
            Require(court.GetInteractionPrompt().Contains("排水"), "Nearby valve offers the drain action");
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => court.State.IsLow, 7, "Valve lowers the lift")) yield return step;
            Require(Mathf.Abs(court.CartTransform.position.y - 3) < .05f, "Fixed landing preserves cart height");

            foreach (var step in MoveCart(new Vector3(-5.5f, 3.1f, 1.8f), 5)) yield return step;
            foreach (var step in MoveCart(new Vector3(-2, 3.1f, 0), 6)) yield return step;
            foreach (var step in MoveCart(new Vector3(8, 3.1f, 1.4f), 7)) yield return step;
            Require(court.State.Delivered && !court.State.Completed, "Delivery keeps the card recovery objective");
            court.Player.Teleport(new Vector3(-5, .1f, -8.6f));
            foreach (var step in WaitSeconds(.15f)) yield return step;
            foreach (var step in Press(Key.Q)) yield return step;
            Require(court.State.Completed, "Q recovers the card and completes the level");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "hud-completion.png"));
            foreach (var step in WaitSeconds(.3f)) yield return step;

            foreach (var step in Press(Key.R)) yield return step;
            Require(court.State.IsLow && court.State.CartStop == 0 && !court.State.CardInserted && !court.State.Completed, "R resets all gameplay state after completion");
            court.Player.Teleport(new Vector3(0, -2, 0));
            foreach (var step in WaitSeconds(.3f)) yield return step;
            Require(court.Player.FeetPosition.y > -.1f, "Falling out returns the player to safe ground");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "mountain-gameplay-hud.png"));
            foreach (var step in WaitSeconds(.3f)) yield return step;
        }

        private static IEnumerable<object> MoveCart(Vector3 playerPosition, int stop)
        {
            court.Player.Teleport(playerPosition);
            foreach (var step in WaitSeconds(.15f)) yield return step;
            Require(court.GetInteractionPrompt().Contains("向前推"), "Cart prompt at stop " + court.State.CartStop);
            foreach (var step in Press(Key.E)) yield return step;
            foreach (var step in Until(() => !court.State.CartInMotion && court.State.CartStop == stop, 9, "Cart reaches stop " + stop)) yield return step;
            Require(court.State.CartStop == stop, "E pushes to stop " + stop);
        }

        private static IEnumerable<object> Press(Key key, float hold = .12f)
        {
            requestedInput = new KeyboardState(key);
            inputPending = true;
            foreach (var step in WaitSeconds(hold)) yield return step;
            Release();
            foreach (var step in WaitSeconds(.12f)) yield return step;
        }

        private static void Release()
        {
            requestedInput = new KeyboardState();
            inputPending = true;
        }

        private static void BeforeInputUpdate()
        {
            // Editor updates have a separate input state buffer. Queue during the gameplay
            // update so events reach the same buffer that CourtPlayer.Update reads.
            if (!inputPending || InputState.currentUpdateType != InputUpdateType.Dynamic || Keyboard.current == null) return;
            InputSystem.QueueStateEvent(Keyboard.current, requestedInput);
            inputPending = false;
        }

        private static IEnumerable<object> WaitSeconds(float seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end) yield return null;
        }

        private static IEnumerable<object> Until(Func<bool> condition, float timeout, string description)
        {
            double end = EditorApplication.timeSinceStartup + timeout;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > end) throw new InvalidOperationException(description + " timed out.");
                yield return null;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.Add("PASS: " + message);
        }
    }
}
