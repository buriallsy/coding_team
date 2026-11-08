using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>Local saved-scene binding and evidence for the ladder's live-height presentation.</summary>
    public static class SkywellLadderHeightRevision
    {
        internal static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../validation/current/skywell/2026-10-23-ladder-height"));

        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != SkywellBuilder.ScenePath)
                throw new InvalidOperationException("Use the clean saved L5 scene outside Play.");
            var court = Object.FindFirstObjectByType<SkywellController>();
            var root = court.transform.Find("Revision_11_Boat_and_ladder/Tower_side_ladder");
            if (!root || root.GetComponent<SkywellLadderVisual>())
                throw new InvalidOperationException("Missing original ladder, or binding already applied; use Audit.");
            Directory.CreateDirectory(Evidence);
            File.Copy(scene.path, Path.Combine(Evidence, "scene-before.unity"), false);
            Capture(court, "ladder-low-before");
            var visual = Undo.AddComponent<SkywellLadderVisual>(root.gameObject);
            visual.Configure(court.Journey.LadderBottom, court.Journey.LadderTop,
                root.Find("Ladder_left_rail"), root.Find("Ladder_right_rail"),
                root.Cast<Transform>().Where(t => t.name.StartsWith("Ladder_rung_", StringComparison.Ordinal))
                    .Select(t => t.GetComponent<Renderer>()).ToArray());
            visual.Refresh();
            EditorUtility.SetDirty(visual);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save ladder height presentation.");
            Audit();
        }

        public static void Audit()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != SkywellBuilder.ScenePath)
                throw new InvalidOperationException("Audit the saved L5 scene outside Play.");
            var court = Object.FindFirstObjectByType<SkywellController>();
            var visual = court.GetComponentInChildren<SkywellLadderVisual>(true);
            var lines = new List<string> { DateTime.UtcNow.ToString("O"), "Saved low-height ladder presentation audit." };
            Verify(visual, court.Journey, lines);
            lines.Add("SKYWELL_LADDER_HEIGHT_SCENE_PASSED");
            Directory.CreateDirectory(Evidence);
            File.WriteAllLines(Path.Combine(Evidence, "scene-audit.txt"), lines);
        }

        internal static void Verify(SkywellLadderVisual visual, SkywellJourney journey, List<string> lines)
        {
            if (!visual || !visual.enabled || !visual.IsConfigured || visual.BottomAnchor != journey.LadderBottom || visual.TopAnchor != journey.LadderTop)
                throw new InvalidOperationException("The visual must read the existing traversal anchors.");
            float expectedTop = journey.LadderTop.position.y + visual.TopOverhang;
            float expectedBottom = journey.LadderBottom.position.y + visual.BottomOffset;
            foreach (var rail in new[] { visual.LeftRail, visual.RightRail })
            {
                var bounds = rail.GetComponent<Renderer>().bounds;
                if (Mathf.Abs(bounds.max.y - expectedTop) > .015f || Mathf.Abs(bounds.min.y - expectedBottom) > .015f)
                    throw new InvalidOperationException("Visible ladder rail differs from live landing height: " + rail.name);
            }
            var rungs = visual.Rungs.OrderBy(r => r.transform.position.y).ToArray();
            if (rungs.Length != 29) throw new InvalidOperationException("The authored 29-rung set must remain intact.");
            for (int i = 0; i < rungs.Length; i++)
            {
                // Renderer culling uses a sub-millimetre cutoff. Only assert states
                // outside a 2mm boundary band, where visibility is unambiguous.
                var bounds = rungs[i].bounds;
                bool fullyInside = bounds.max.y < expectedTop - .002f && bounds.min.y > expectedBottom + .002f;
                bool outside = bounds.max.y > expectedTop + .002f || bounds.min.y < expectedBottom - .002f;
                if (fullyInside && !rungs[i].enabled || outside && rungs[i].enabled)
                    throw new InvalidOperationException($"Rung visibility mismatch: {rungs[i].name}; maxY={bounds.max.y:F6}; railTop={expectedTop:F6}; enabled={rungs[i].enabled}.");
                if (i > 0 && Mathf.Abs(rungs[i].transform.position.y - rungs[i - 1].transform.position.y - .4f) > .001f)
                    throw new InvalidOperationException("Authored rung spacing changed.");
            }
            if (visual.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Ladder presentation must not change gameplay collision.");
            lines.Add($"PASS: rails {expectedBottom:F3} -> {expectedTop:F3}; landing={journey.LadderTop.position.y:F3}; visible rungs={rungs.Count(r => r.enabled)}/29; spacing=0.400m.");
        }

        internal static void Capture(SkywellController court, string name)
        {
            var holder = new GameObject("Temporary ladder-height evidence camera") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture target = null; Texture2D pixels = null; var previous = RenderTexture.active;
            try
            {
                var camera = holder.AddComponent<Camera>();
                if (Camera.main) camera.CopyFrom(Camera.main);
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 10;
                camera.nearClipPlane = .1f; camera.farClipPlane = 180;
                Vector3 focus = new Vector3(0, 21, 104);
                camera.transform.SetPositionAndRotation(focus + new Vector3(21, 7, -24), Quaternion.LookRotation(new Vector3(-21, -7, 24)));
                target = new RenderTexture(1536, 1024, 24); target.Create(); camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(1536, 1024, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1536, 1024), 0, 0); pixels.Apply();
                Directory.CreateDirectory(Evidence);
                File.WriteAllBytes(Path.Combine(Evidence, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels) Object.DestroyImmediate(pixels);
                if (target) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(holder);
            }
        }
    }

    public static partial class SkywellChecks
    {
        public static void RunLadderHeight() => StartCheck("SKYWELL_LADDER_HEIGHT", LadderHeightSequence());

        private static IEnumerator LadderHeightSequence()
        {
            court.ResetCourt(); yield return Wait(.3f);
            var visual = court.GetComponentInChildren<SkywellLadderVisual>(true);
            report.Add("LADDER HEIGHT FIXTURE: one explicit placement on the low tower deck, then direct local Rain request through the production console. Water collection and real platform motion remain active. Verifies live rails/rungs at low, intermediate, high and reset heights. Not a continuous entrance route or manual climbing test.");
            Require(visual && visual.IsConfigured, "Saved L5 binds ladder presentation to the original traversal anchors.");
            SkywellLadderHeightRevision.Verify(visual, court.Journey, report);
            Require(Mathf.Abs(court.Height - court.PlatformLowY) < .01f, "Reset begins at the original low platform height.");
            SkywellLadderHeightRevision.Capture(court, "ladder-low-after");
            court.Player.Teleport(court.Origin + new Vector3(-4, .03f, 2.5f)); yield return Wait(.2f);
            court.Consoles[0].Request(SkywellToken.Rain); yield return Wait(1.5f);
            Require(court.TowerWeatherZone.IsRain, "Production tower console accepts the Rain fixture.");
            float deadline = Time.time + 12; bool sawIntermediate = false; int samples = 0;
            while (court.Height < court.PlatformHighY - .01f)
            {
                if (Time.time > deadline) throw new InvalidOperationException("Rain did not raise the real platform.");
                yield return null;
                var diagnostics = new List<string>();
                SkywellLadderHeightRevision.Verify(visual, court.Journey, diagnostics); samples++;
                if (!sawIntermediate && court.Height > 2.8f && court.Height < 3.6f)
                {
                    sawIntermediate = true; report.AddRange(diagnostics);
                    SkywellLadderHeightRevision.Capture(court, "ladder-middle-after");
                }
            }
            yield return Wait(.15f);
            Require(sawIntermediate && samples > 10, "Visible ladder follows every sampled live lift frame without scaling rung spacing.");
            SkywellLadderHeightRevision.Verify(visual, court.Journey, report);
            SkywellLadderHeightRevision.Capture(court, "ladder-high-after");
            visual.enabled = false; yield return Wait(.1f);
            visual.enabled = true; yield return Wait(.1f);
            SkywellLadderHeightRevision.Verify(visual, court.Journey, report);
            Require(Mathf.Abs(court.Height - court.PlatformHighY) < .01f, "Presentation enable/disable does not change mechanical state.");
            court.ResetCourt(); yield return Wait(.3f);
            SkywellLadderHeightRevision.Verify(visual, court.Journey, report);
            Require(Mathf.Abs(court.Height - court.PlatformLowY) < .01f, "Reset retracts the visible ladder with the real landing.");
        }
    }
}
