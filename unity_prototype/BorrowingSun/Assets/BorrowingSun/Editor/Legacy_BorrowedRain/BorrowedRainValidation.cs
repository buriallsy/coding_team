using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BorrowingSun.Editor
{
    /// <summary>Authored collider and deterministic integration checks for the separate middle-level scene.</summary>
    public static class BorrowedRainValidation
    {
        private static readonly List<string> Report = new List<string>();
        private static BorrowedRainController court;
        private static Vector3 offset;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../validation/borrowed-rain"));

        [MenuItem("Borrowing Sun/Borrowed Rain/Validate active scene")]
        public static void ValidateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run deterministic checks outside Play Mode.");
            court = UnityEngine.Object.FindAnyObjectByType<BorrowedRainController>();
            if (court == null) throw new InvalidOperationException("Open the Borrowed Rain scene first; L1 is never opened or reset by this check.");
            Report.Clear();
            Report.Add("Borrowed Rain / actual scene geometry and deterministic integration / " + DateTime.UtcNow.ToString("O"));
            Report.Add("Scene: " + EditorSceneManager.GetActiveScene().path);
            Report.Add("Unity: " + Application.unityVersion);
            offset = court.Bindings.worldOffset;
            Exception failure = null;
            try
            {
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0,
                            "No missing scripts: " + t.name, false);
                Require(court.Player != null && court.CameraRig != null && court.CameraRig.ViewCamera != null,
                    "Player, camera rig and view camera are connected");
                Require(court.Bindings.aBridge != null && court.Bindings.bLift != null && court.CartTransform != null,
                    "Both moving supports and the cart are connected");
                Require(Vector3.Distance(offset, new Vector3(-6, 0, 66)) < .001f, "Scene uses the agreed city coordinate anchor");
                court.ResetCourt();
                var cartRenderers = court.CartTransform.GetComponentsInChildren<Renderer>();
                Require(cartRenderers.Length > 0, "Cart has inspectable world-space geometry");
                Bounds cartBounds = cartRenderers[0].bounds;
                foreach (var renderer in cartRenderers.Skip(1)) cartBounds.Encapsulate(renderer.bounds);
                Require(cartBounds.size.z < 2.2f, "Cart's visible width leaves clearance within the 3 m route");
                Report.Add("Cart world bounds at the west stop: " + cartBounds);
                var font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(font != null && "借晴雨廊桥槽排水取回".All(font.HasCharacter), "Bundled Chinese font contains the gameplay glyphs");
                CheckInvalidValues();
                CheckWrongRainAndPartialHeight();
                CheckOccupiedSupports();
                CheckSolutionAndMotionInterlocks();
                CheckSupportIdentity();
                CheckFixedRecoveryGeometry();
                CheckRainGeometry();
                CheckFlowEndpoints();
                CheckRecoveryNetwork();
                court.ResetCourt();
                Require(court.State.IsALow && court.State.IsBLow && court.State.CardStation == -1 &&
                    !court.State.RainActive && !court.State.DrainAOpen && !court.State.DrainBOpen &&
                    court.State.CartStop == 0 && !court.State.CartInMotion && !court.State.Completed,
                    "Full reset restores both pools, card, cart and controls together");
                Report.Add("LIMIT: simulation is deterministic Editor evidence. It does not prove player comprehension, visual readability or a target-platform player build.");
                Report.Add("BORROWED_RAIN_EDITOR_VALIDATION_PASSED");
            }
            catch (Exception e)
            {
                failure = e;
                Report.Add("FAIL: " + e.Message);
                Report.Add("BORROWED_RAIN_EDITOR_VALIDATION_FAILED");
            }
            finally
            {
                if (court != null) court.ResetCourt();
                Physics.SyncTransforms();
                Directory.CreateDirectory(Output);
                File.WriteAllLines(Path.Combine(Output, "editor-validation.txt"), Report);
            }
            if (failure != null) throw failure;
            Debug.Log("BORROWED_RAIN_EDITOR_VALIDATION_PASSED");
        }

        private static void CheckInvalidValues()
        {
            court.ResetCourt();
            Require(!court.State.InsertCard(-1) && !court.State.InsertCard(2), "Only the two authored rain sockets accept the card");
            Require(!court.State.ToggleRain(0) && !court.State.ToggleRain(1), "Empty sockets cannot emit rain");
            court.State.Advance(float.NaN); court.State.Advance(float.PositiveInfinity); court.State.Advance(-1);
            Require(court.State.IsALow && court.State.IsBLow, "Invalid simulation deltas cannot poison water levels");
            Require(!court.TryMoveCart(-1, false), "Cart cannot reverse before the first stop");
        }

        private static void CheckWrongRainAndPartialHeight()
        {
            court.ResetCourt();
            Require(court.State.InsertCard(1) && court.State.ToggleRain(1), "Rain card powers the remote window");
            court.Simulate(10);
            Require(court.State.RainToWaste && !court.State.RainToB && court.State.IsALow && court.State.IsBLow,
                "Low A sends remote rain to waste; neither basin silently fills");
            Require(court.State.RemoveCard(1) && court.State.InsertCard(0) && court.State.ToggleRain(0), "The unique card moves from S1 to S0");
            court.Simulate(1.5f);
            float partial = court.State.WaterA;
            Require(partial > .2f && partial < 2.8f, "A can stop at a real intermediate height");
            court.State.RemoveCard(0); court.Simulate(5);
            Require(Mathf.Abs(court.State.WaterA - partial) < .001f, "Removing the card preserves intermediate A water");
            Require(!court.TryMoveCart(1, false), "A stopped midway does not form a traversable cart seam");
            court.State.InsertCard(1); court.State.ToggleRain(1); court.Simulate(5);
            Require(court.State.IsBLow && court.State.RainToWaste, "Partial A does not receive a hidden high-gutter shortcut");
            court.State.RemoveCard(1); court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(10);
            Require(court.State.IsAHigh, "Partial A can resume to its high endpoint");
            CheckMovingDeck(0, 18);
            CheckMovingDeck(1, 12);
            court.State.RemoveCard(0); court.Simulate(10);
            Require(court.State.IsAHigh && court.State.IsBLow, "A stays high when the local rain card is recovered");
        }

        private static void CheckOccupiedSupports()
        {
            court.ResetCourt();
            Move(1);
            Require(court.State.CartStop == 1, "Cart can park on low A");
            court.Player.Teleport(World(-3, 15.03f, -.85f));
            court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(10);
            Require(Mathf.Abs(court.CartTransform.position.y - 18) < .02f, "A carries its parked cart through the full 3 m rise");
            Require(Mathf.Abs(court.Player.FeetPosition.y - 18.03f) < .08f, "A carries the standing player once, without double displacement");
            Require(!court.TryMoveCart(-1, false) && !court.TryMoveCart(1, false), "A's cart is braked at its disconnected high endpoint");
            court.State.RemoveCard(0); court.State.ToggleDrain(0); court.Simulate(10);
            Require(Mathf.Abs(court.CartTransform.position.y - 15) < .02f, "A carries the parked cart back down");
            Move(-1);
            Require(court.State.CartStop == 0, "Cart can reverse to the fixed west deck after A returns");
            PrepareCrossing(); Move(1); Move(1); Move(1);
            Require(court.State.CartStop == 3, "Cart parks on high B");
            court.Player.Teleport(World(4, 15.03f, -.85f));
            court.State.ToggleDrain(1); court.Simulate(10);
            Require(court.State.IsBLow && Mathf.Abs(court.CartTransform.position.y - 12) < .02f,
                "An erroneous B drain carries the cart down instead of leaving it suspended");
            Require(Mathf.Abs(court.Player.FeetPosition.y - 12.03f) < .08f, "B carries its standing player down");
            Require(!court.TryMoveCart(-1, false) && !court.TryMoveCart(1, false), "Low B blocks both cart exits");
            CheckMovingDeck(0, 15); CheckMovingDeck(1, 12);
        }

        private static void CheckSolutionAndMotionInterlocks()
        {
            PrepareCrossing(true);
            for (float x = -9; x <= 9; x += .25f)
                foreach (float z in new[] { -.85f, 0f, .85f })
                    Ground(World(x, 15, z), "Completed cart route", .1f);
            Require(true, "Actual collider surfaces form the final route, sampled every 0.25 m across all seams");
            Require(court.TryMoveCart(1, false), "Aligned route begins cart travel");
            float a = court.State.WaterA, b = court.State.WaterB;
            Require(!court.State.RemoveCard(1) && !court.State.ToggleRain(1) &&
                    !court.State.ToggleDrain(0) && !court.State.ToggleDrain(1),
                "Card and both water controls reject changes during a cart transfer");
            court.State.Advance(20);
            Require(court.State.WaterA == a && court.State.WaterB == b && court.State.CartInMotion,
                "Water remains frozen until the cart animation owns a stable destination");
            court.Simulate(20);
            Move(-1); Move(1); Move(1); Move(1); Move(1); Move(1);
            Require(court.State.Delivered && !court.State.Completed, "Delivery on the east fixed deck still requires rain-card recovery");
            Require(court.State.RemoveCard(1) && court.State.Completed, "Recovering the card with the cargo route restored completes the level");
            court.State.ToggleDrain(1); court.Simulate(10);
            Require(Mathf.Abs(court.CartTransform.position.y - 15) < .02f, "Delivered cart keeps fixed-deck elevation after B is drained");
            Require(!court.State.Completed, "Delivery and card alone cannot count as restored service while the cargo route is disconnected");
            court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(10);
            court.State.RemoveCard(0); court.State.InsertCard(1); court.State.ToggleRain(1); court.Simulate(10);
            court.State.RemoveCard(1);
            Require(court.State.Delivered && !court.State.Completed, "Even a delivered cart and recovered card still require high A to be lowered");
            court.State.ToggleDrain(0); court.Simulate(10);
            Require(court.State.Completed, "Restoring A low / B high reinstates complete service without losing the delivered cart");
        }

        // leaveCard=true also covers delivery before card recovery; false covers recovery before delivery.
        private static void PrepareCrossing(bool leaveCard = false)
        {
            court.ResetCourt();
            court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(10);
            Require(court.State.IsAHigh && court.State.IsBLow, "S0 raises A independently of B");
            court.State.RemoveCard(0); court.State.InsertCard(1); court.State.ToggleRain(1); court.Simulate(10);
            Require(court.State.IsAHigh && court.State.IsBHigh && court.State.RainToB,
                "Raised A connects the fixed rain outlet and high gutter to B");
            CheckMovingDeck(0, 18); CheckMovingDeck(1, 15);
            Require(!court.TryMoveCart(1, false), "A high / B high is visibly different from a complete cargo route");
            court.State.ToggleRain(1);
            if (!leaveCard) court.State.RemoveCard(1);
            court.State.ToggleDrain(0); court.Simulate(10);
            Require(court.State.IsALow && court.State.IsBHigh, "Draining A preserves B and restores the route");
            CheckMovingDeck(0, 15); CheckMovingDeck(1, 15);
        }

        private static void CheckSupportIdentity()
        {
            var a = BorrowedRainController.DetermineSupport(World(-3, 16.5f, 0), 1.5f, 0, offset);
            var b = BorrowedRainController.DetermineSupport(World(4, 13.5f, 0), 0, 1.5f, offset);
            Require(a == BorrowedRainSupport.BridgeA && b == BorrowedRainSupport.LiftB,
                "Intermediate support detection identifies A and B independently");
            Require(BorrowedRainController.DetermineSupport(World(1, 15, 0), 0, 3, offset) == BorrowedRainSupport.None,
                "The fixed middle landing is not carried by either water level");
            Require(BorrowedRainController.DetermineSupport(World(-3, 17, 0), 0, 3, offset) == BorrowedRainSupport.None,
                "A player above a bridge is not mistaken for its supported rider");
        }

        private static void CheckMovingDeck(int which, float y)
        {
            float min = which == 0 ? -6 : 2, max = which == 0 ? 0 : 6;
            for (float x = min + .4f; x < max; x += .5f)
                foreach (float z in new[] { -.85f, .85f }) Ground(World(x, y, z), (which == 0 ? "A" : "B") + " deck", .1f);
            for (float z = -1.2f; z >= -2.5f; z -= .2f)
                Ground(World(which == 0 ? -3 : 4, y, z), "Moving support joins its fixed endpoint escape pad", .1f);
        }

        private static void CheckFixedRecoveryGeometry()
        {
            // These paths belong to the fixed infrastructure and must exist in every water state.
            SamplePath(new Vector3(-3, 15, -2.25f), new Vector3(-3, 15, -4.25f), "A low escape");
            SamplePath(new Vector3(-3, 18, -2.25f), new Vector3(-3, 18, -6), "A high observation landing");
            SamplePath(new Vector3(-15, 15, -6), new Vector3(-3, 18, -6), "A high escape ramp");
            SamplePath(new Vector3(4, 12, -2.25f), new Vector3(4, 12, -11.5f), "B low escape");
            SamplePath(new Vector3(4, 15, -2.25f), new Vector3(4, 15, -4.25f), "B high escape");
            SamplePath(new Vector3(-15, 12, -9), new Vector3(-3, 15, -9), "West lower maintenance ramp");
            SamplePath(new Vector3(13, 12, -9), new Vector3(13, 15, 3), "East maintenance ramp");
            SamplePath(new Vector3(-15, 12, -11.5f), new Vector3(13, 12, -11.5f), "Independent lower maintenance corridor");
            Require(true, "Fixed endpoint exits and sloped maintenance paths have supporting colliders at 0.3 m intervals");
        }

        private static void SamplePath(Vector3 start, Vector3 end, string label)
        {
            int count = Mathf.CeilToInt(Vector3.Distance(start, end) / .3f);
            for (int i = 0; i <= count; i++) Ground(offset + Vector3.Lerp(start, end, (float)i / count), label, .13f);
        }

        private static void CheckRainGeometry()
        {
            Transform Find(string name) => EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
            var receiver = Find("Fixed_receiver_18_30");
            var gutter = Find("Moving_gutter_outlet");
            var outlet = Find("Fixed_remote_outlet_20");
            Require(receiver != null && gutter != null && outlet != null, "Rain has distinct authored receiver, moving gutter and fixed window outlet");
            var renderer = receiver.GetComponent<Renderer>();
            Require(renderer != null && Mathf.Abs(renderer.bounds.max.y - 18.3f) < .035f,
                "The actual fixed receiver surface is at 18.30 m");
            court.ResetCourt();
            float fixedY = outlet.position.y;
            Require(Mathf.Abs(gutter.position.y - 15.45f) < .035f, "Low gutter outlet is at 15.45 m");
            court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(10);
            float drop = gutter.position.y - renderer.bounds.max.y;
            Require(drop > .1f && drop < .2f, "High gutter has a real downward 0.15 m fall into the receiver");
            Require(Mathf.Abs(outlet.position.y - fixedY) < .001f && outlet.position.y >= 20,
                "Remote rain outlet stays above the moving gutter instead of following the bridge");
            Require(gutter.position.z >= offset.z + 1.5f, "Gutter is outside the 3 m cargo path");
        }

        private static void CheckFlowEndpoints()
        {
            court.ResetCourt();
            var waste = court.Bindings.wasteFlow != null ? court.Bindings.wasteFlow.Find("Unconnected_rain") : null;
            var received = court.Bindings.successfulFlow != null ? court.Bindings.successfulFlow.Find("B_falling_water") : null;
            var gutter = court.Bindings.aBridge.Find("Moving_gutter_outlet");
            Require(waste != null && received != null && gutter != null, "Dynamic flow meshes expose their actual endpoints");
            court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(1.5f);
            court.State.RemoveCard(0); court.State.InsertCard(1); court.State.ToggleRain(1); court.Simulate(0);
            Require(Vector3.Distance(waste.TransformPoint(new Vector3(0, 0, -.5f)), gutter.position) < .015f,
                "Waste flow begins at the actual gutter even when A stops midway");
            Require(Mathf.Abs(waste.TransformPoint(new Vector3(0, 0, .5f)).y - (offset.y + 11.5f)) < .015f,
                "Waste flow retains its fixed receiving-channel endpoint");
            CheckParticleLanding(court.Bindings.remoteRain, gutter.position.y, "Remote rain stops at the intermediate gutter surface");
            court.State.RemoveCard(1); court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(10);
            court.State.RemoveCard(0); court.State.InsertCard(1); court.State.ToggleRain(1); court.Simulate(1.5f);
            Vector3 endpoint = received.TransformPoint(new Vector3(0, 0, .5f));
            float waterTop = court.Bindings.waterB.position.y + Mathf.Abs(court.Bindings.waterB.lossyScale.y) * .5f;
            Require(Mathf.Abs(endpoint.y - (waterTop + .025f)) < .015f && Mathf.Abs(endpoint.z - (offset.z + 1.68f)) < .015f,
                "B inlet follows the exposed water surface outside the moving cargo deck");
            Require(Mathf.Abs(received.TransformPoint(new Vector3(0, 0, -.5f)).y - (offset.y + 18.19f)) < .015f,
                "B inlet retains the fixed aqueduct spout above its changing water endpoint");
            CheckParticleLanding(court.Bindings.remoteRain, gutter.position.y, "Remote rain stops at the high gutter instead of falling through it");
            CheckParticleLanding(court.Bindings.localRain, court.Bindings.station0.position.y + .27f,
                "Local rain stops at the authored S0 collection pan");
        }

        private static void CheckParticleLanding(ParticleSystem rain, float expectedY, string label)
        {
            Require(rain != null, "Rain particle source exists");
            var main = rain.main;
            float predictedY = rain.transform.position.y + rain.transform.forward.y * main.startSpeed.constant * main.startLifetime.constant;
            Require(Mathf.Abs(predictedY - expectedY) < .025f, label);
        }

        private static void CheckRecoveryNetwork()
        {
            court.ResetCourt();
            court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(1.5f); court.State.RemoveCard(0);
            float a = court.State.WaterA;
            court.Player.Teleport(World(-.5f, 15 + a + .05f, 0));
            Require(court.GetInteractionPrompt().Contains("安全返回"), "A east-end intermediate rider has an explicit recovery action");
            court.InteractPrimary();
            Require(OnMainBank(court.Player.FeetPosition) && Mathf.Abs(court.State.WaterA - a) < .001f,
                "A east-end recovery chooses connected infrastructure, not the isolated middle refuge");
            court.State.InsertCard(0); court.State.ToggleRain(0); court.Simulate(10); court.State.RemoveCard(0);
            court.State.InsertCard(1); court.State.ToggleRain(1); court.Simulate(1.5f); court.State.RemoveCard(1);
            float b = court.State.WaterB;
            court.Player.Teleport(World(4, 12 + b + .05f, 0)); court.InteractPrimary();
            Require(OnMainBank(court.Player.FeetPosition) && Mathf.Abs(court.State.WaterB - b) < .001f,
                "B intermediate recovery chooses connected infrastructure and keeps its water");
            court.Player.Teleport(World(1, 15.05f, 3.5f));
            Require(!court.IsPermanentGround(court.Player.FeetPosition), "An isolated middle refuge does not overwrite the safe checkpoint");
            Require(court.GetInteractionPrompt().Contains("安全返回"), "Both disconnected middle seams expose a recovery action");
            court.InteractPrimary();
            Require(OnMainBank(court.Player.FeetPosition) && court.State.IsAHigh && Mathf.Abs(court.State.WaterB - b) < .001f && court.State.CardStation == -1,
                "Isolated middle-refuge recovery preserves the puzzle and returns to the maintenance network");
        }

        private static bool OnMainBank(Vector3 position)
        {
            Vector3 local = position - offset;
            return (local.x < -6.1f || local.x > 6.1f) && Mathf.Abs(local.y - 15) < .2f;
        }

        private static void Ground(Vector3 feet, string label, float tolerance)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(feet + Vector3.up * .28f, Vector3.down, .62f, ~0, QueryTriggerInteraction.Ignore);
            bool supported = hits.Any(hit => !(hit.collider is CharacterController) &&
                !hit.collider.transform.IsChildOf(court.CartTransform) && Mathf.Abs(hit.point.y - feet.y) <= tolerance);
            Require(supported, label + " has supporting collision at " + feet.ToString("F3"), false);
        }

        private static void Move(int direction)
        {
            int expected = court.State.CartStop + direction;
            Quaternion previousHeading = court.CartTransform.rotation;
            Require(court.TryMoveCart(direction, false), "Cart may transfer to stop " + expected);
            if (direction < 0)
            {
                court.Simulate(.2f);
                Require(Quaternion.Angle(previousHeading, court.CartTransform.rotation) < .1f,
                    "Reverse departure does not sweep the cart across the adjacent player's strip");
            }
            court.Simulate(20);
            Require(court.State.CartStop == expected && !court.State.CartInMotion, "Cart reaches stop " + expected);
            if (direction < 0) Require(Quaternion.Angle(previousHeading, court.CartTransform.rotation) < .1f,
                "Reversing the cart preserves heading rather than sweeping a 180-degree turn across the walking strip");
        }

        private static Vector3 World(float x, float y, float z) => offset + new Vector3(x, y, z);
        private static void Require(bool condition, string message, bool log = true)
        {
            if (!condition) throw new InvalidOperationException(message);
            if (log) Report.Add("PASS: " + message);
        }

        [MenuItem("Borrowing Sun/Borrowed Rain/Capture active overview")]
        public static void CaptureOverview()
        {
            var active = UnityEngine.Object.FindAnyObjectByType<BorrowedRainController>();
            if (active == null || active.CameraRig == null || active.CameraRig.ViewCamera == null)
                throw new InvalidOperationException("Open Borrowed Rain with its camera wired first.");
            var camera = active.CameraRig.ViewCamera;
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
                Directory.CreateDirectory(Output);
                File.WriteAllBytes(Path.Combine(Output, "overview.png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previous;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
            }
            Debug.Log("BORROWED_RAIN_OVERVIEW_CAPTURED");
        }
    }
}
