using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>Local author-scene edit: low boat gunwales and solid lower ladder backing.</summary>
    public static class SkywellDirectBoardingRevision
    {
        private const string RootName = "Revision_17_Direct_boarding_and_ladder";
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../validation/current/skywell/2026-10-22-direct-boarding-climb"));
        private static SkywellController SavedCourt()
        {
            var s = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || s.isDirty || s.path != SkywellBuilder.ScenePath)
                throw new InvalidOperationException("A clean saved L5 scene outside Play is required.");
            return Object.FindAnyObjectByType<SkywellController>() ?? throw new InvalidOperationException("Missing L5.");
        }
        public static void Apply()
        {
            var c = SavedCourt(); var j = c.Journey; var scene = c.gameObject.scene;
            if (c.transform.Find(RootName)) throw new InvalidOperationException("Direct boarding revision already exists; use Audit.");
            var boat = j.ReturnShuttle;
            var sides = boat.Cast<Transform>().Where(t => t.name == "Shuttle_glass_side" || t.name == "Shuttle_rear").ToArray();
            var roof = boat.Find("Shuttle_roof");
            var ladder = c.transform.Find("Revision_11_Boat_and_ladder/Tower_side_ladder");
            if (sides.Length != 3 || !roof || !ladder) throw new InvalidOperationException("Unexpected saved boat/ladder geometry.");
            if (sides.Any(t => Mathf.Abs(t.localScale.y - 3) > .01f)) throw new InvalidOperationException("Boat walls already changed; inspect author edits.");
            var climbClip = SkywellClimbAnimationSetup.ImportClimbAnimation();
            var wood = ladder.Find("Ladder_left_rail").GetComponent<Renderer>().sharedMaterial;
            var towerStone = c.transform.Find("Revision_11_Boat_and_ladder/Tower_water_landing").GetComponent<Renderer>().sharedMaterial;
            var preserved = new[] { j.LadderBottom, j.LadderTop, j.LadderExit, j.BoatDock, c.Rotor.Find("Doorway_gangway"), boat.Find("Shuttle_floor") }
                .ToDictionary(t => t, t => new Pose(t.position, t.rotation));
            Directory.CreateDirectory(Evidence);
            File.Copy(scene.path, Path.Combine(Evidence, "scene-before.unity"), false);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Direct boat boarding and guarded ladder");
            try
            {
                foreach (var side in sides)
                {
                    Undo.RecordObject(side, "Lower boat gunwale");
                    var pos = side.localPosition; pos.y = .325f; side.localPosition = pos;
                    var size = side.localScale; size.y = .65f; side.localScale = size;
                    var renderer = side.GetComponent<Renderer>(); Undo.RecordObject(renderer, "Wooden boat gunwale"); renderer.sharedMaterial = wood;
                }
                Undo.DestroyObjectImmediate(roof.gameObject);
                var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "Guard ladder approach");
                root.transform.SetParent(c.transform, false); root.transform.position = Vector3.zero;
                // A visible solid joins the landing to the tower. Its top remains below the
                // lowest climbing exit, so moving from the ladder onto the ring stays physical.
                Box(root.transform, "Ladder_solid_backing", new Vector3(0, 17.15f, 102.2f), new Vector3(2.4f, 5.6f, 1.3f), towerStone);
                Box(root.transform, "Ladder_approach_left_guard", new Vector3(-1.12f, 15.08f, 101.4f), new Vector3(.16f, 1.06f, 1.2f), wood);
                Box(root.transform, "Ladder_approach_right_guard", new Vector3(1.12f, 15.08f, 101.4f), new Vector3(.16f, 1.06f, 1.2f), wood);
                SkywellClimbAnimationSetup.Bind(j, c.Player, climbClip);
                foreach (var pair in preserved)
                    if (Vector3.Distance(pair.Key.position, pair.Value.position) > .0001f || Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) > .001f)
                        throw new InvalidOperationException("Unrelated route anchor changed: " + pair.Key.name);
                EditorUtility.SetDirty(j); Physics.SyncTransforms();
                var lines = Inspect(c); lines.Add("PASS: boat deck/dock, ladder anchors and original projecting gangway retained.");
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save direct boarding geometry.");
                lines.Add("DIRECT_BOARDING_SCENE_PASSED"); File.WriteAllLines(Path.Combine(Evidence, "scene-apply.txt"), lines);
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
        public static void Audit()
        {
            var lines = Inspect(SavedCourt()); lines.Add("DIRECT_BOARDING_SCENE_PASSED");
            Directory.CreateDirectory(Evidence); File.WriteAllLines(Path.Combine(Evidence, "scene-audit.txt"), lines);
        }
        public static void BindCurrentAnimation()
        {
            var c = SavedCourt();
            var clip = SkywellClimbAnimationSetup.ImportClimbAnimation();
            SkywellClimbAnimationSetup.Bind(c.Journey, c.Player, clip);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            if (!EditorSceneManager.SaveScene(c.gameObject.scene)) throw new IOException("Failed to save ladder animation binding.");
            Audit();
        }
        private static List<string> Inspect(SkywellController c)
        {
            var lines = new List<string> { DateTime.UtcNow.ToString("O") };
            var j = c.Journey; var boat = j.ReturnShuttle;
            var animation = j.GetComponent<SkywellClimbAnimation>();
            if (!animation || !animation.HasClimbAnimation || !animation.Visual.ClimbAnimation.isLooping)
                throw new InvalidOperationException("Missing retargeted Humanoid ladder loop.");
            lines.Add("PASS: optional L5 Humanoid ladder clip=" + animation.Visual.ClimbAnimation.name);
            if (boat.Find("Shuttle_roof")) throw new InvalidOperationException("Old enclosed cabin roof remains.");
            var sides = boat.Cast<Transform>().Where(t => t.name == "Shuttle_glass_side" || t.name == "Shuttle_rear").ToArray();
            if (sides.Length != 3 || sides.Any(t => Mathf.Abs(t.localScale.y - .65f) > .001f || !t.GetComponent<Collider>().enabled))
                throw new InvalidOperationException("Expected three solid low gunwales.");
            var root = c.transform.Find(RootName);
            if (!root) throw new InvalidOperationException("Missing ladder backing.");
            foreach (var name in new[] { "Ladder_solid_backing", "Ladder_approach_left_guard", "Ladder_approach_right_guard" })
            {
                var t = root.Find(name); var col = t ? t.GetComponent<BoxCollider>() : null;
                if (!col || !col.enabled || col.isTrigger || !t.GetComponent<Renderer>().enabled) throw new InvalidOperationException("Missing visible solid: " + name);
                lines.Add("PASS: " + name + " bounds=" + col.bounds);
            }
            if (root.Find("Ladder_solid_backing").GetComponent<Collider>().bounds.max.y > j.LadderTop.position.y - .06f)
                throw new InvalidOperationException("Backing blocks lowest ladder exit.");
            lines.Add("PASS: open-topped boat, 0.65m solid gunwales, unchanged floor and ladder front clearance.");
            lines.Add("Boat=" + boat.position + " ladder=" + j.LadderBottom.position + " -> " + j.LadderTop.position + " exit=" + j.LadderExit.position);
            return lines;
        }
        private static void Box(Transform parent, string name, Vector3 world, Vector3 scale, Material material)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Undo.RegisterCreatedObjectUndo(g, "Visible ladder guard");
            g.name = name; g.transform.SetParent(parent, false); g.transform.position = world; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
