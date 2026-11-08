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
    /// <summary>Rebinds weather selection to existing machinery; never rebuilds level geometry.</summary>
    public static class SkywellWeatherTargetSetup
    {
        private static readonly string[] LegacyParts =
        {
            "Shared_console_base", "Common_telescopic_mast", "ONE_token_slot", "Visible_token",
            "Rain_collector_fold", "Wind_vanes_fold", "Sun_reflector_fold"
        };
        private static readonly string[] StationPaths =
        {
            "M1_Rotation_and_lift/Outer_lift_ring_NON_ROTATING/Rotor_PIVOT/Weather_console_SINGLE_SLOT",
            "M2_Reflection_rail_and_ballast/Sunridge_weather_SINGLE_SLOT",
            "M3_Underwater_refuge_and_search/Fixed_refuge_FOUR_WALLS/Refuge_weather_SINGLE_SLOT",
            "Revision_09_Borrowing_Sun_contract/Shaded_village_weather_SINGLE_SLOT"
        };
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../validation/current/skywell/2026-10-19-weather-targets"));

        [MenuItem("Borrowing Sun/Skywell/Bind weather to actual machinery")]
        public static void Apply()
        {
            var court = SavedCourt();
            var scene = court.gameObject.scene;
            if (court.GetComponentsInChildren<SkywellWeatherTarget>(true).Length != 0)
                throw new InvalidOperationException("Weather targets already exist. Run the read-only audit instead of repeating migration.");
            var stations = ValidateStations(court);
            var remove = LegacyRoots(stations);
            var deleted = new HashSet<Object>();
            foreach (var root in remove)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    deleted.Add(t.gameObject);
                    foreach (var component in t.GetComponents<Component>()) if (component) deleted.Add(component);
                }
            ValidateDeletionReferences(court, stations, deleted);
            var bindings = ResolveBindings(court);
            var unchanged = CaptureUnchanged(court, deleted);
            var stationZones = stations.Select(s => s.Zone).ToArray();
            int removedRenderers = deleted.Count(o => o is Renderer);
            int removedColliders = deleted.Count(o => o is Collider);
            int removedObjects = deleted.Count(o => o is GameObject);
            string backup = BackupScene(scene.path);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Select weather on actual machinery");
            try
            {
                foreach (var station in stations)
                {
                    Undo.RecordObject(station, "Remove weather pedestal visuals");
                    var serialized = new SerializedObject(station);
                    var legacyHeads=serialized.FindProperty("heads");
                    if(legacyHeads!=null)legacyHeads.arraySize=0;
                    var legacyDisplay=serialized.FindProperty("tokenDisplay");
                    if(legacyDisplay!=null)legacyDisplay.objectReferenceValue=null;
                    serialized.ApplyModifiedProperties();
                }
                foreach (var root in remove) Undo.DestroyObjectImmediate(root.gameObject);
                var targets = new SkywellWeatherTarget[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                {
                    var binding = bindings[i];
                    targets[i] = Undo.AddComponent<SkywellWeatherTarget>(binding.Root.gameObject);
                    targets[i].Configure(i, binding.Label, binding.Renderers, binding.Colliders);
                    EditorUtility.SetDirty(targets[i]);
                }
                Undo.RecordObject(court, "Bind four weather targets");
                court.ConfigureWeatherTargets(targets);
                EditorUtility.SetDirty(court);
                VerifyUnchanged(unchanged);
                for (int i = 0; i < stations.Length; i++)
                    if (court.Consoles[i] != stations[i] || stations[i].Zone != stationZones[i])
                        throw new InvalidOperationException("Weather state anchors or zones changed during visual migration.");
                var lines = InspectApplied(court);
                lines.Insert(1, "Backup=" + backup);
                lines.Insert(2, "Removed only old console visuals: GameObjects=" + removedObjects +
                    "; Renderers=" + removedRenderers + "; Colliders=" + removedColliders);
                lines.Add("PASS: Existing transforms, geometry, materials, colliders and other serialized components are unchanged.");
                lines.Add("PASS: Console order, state anchors and WeatherZone references are retained.");
                lines.Add("SKYWELL_WEATHER_TARGET_SETUP_PASSED");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save weather-target scene.");
                Undo.CollapseUndoOperations(group);
                Directory.CreateDirectory(Evidence);
                File.WriteAllLines(Path.Combine(Evidence, "scene-setup.txt"), lines);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        [MenuItem("Borrowing Sun/Skywell/Audit weather target bindings")]
        public static void Audit()
        {
            var court = SavedCourt();
            var lines = InspectApplied(court);
            lines.Add("Read-only scene audit: no objects, materials, collision or weather state modified.");
            lines.Add("SKYWELL_WEATHER_TARGET_AUDIT_PASSED");
            Directory.CreateDirectory(Evidence);
            File.WriteAllLines(Path.Combine(Evidence, "scene-audit.txt"), lines);
        }

        [MenuItem("Borrowing Sun/Skywell/Refine weather target highlight")]
        public static void RefineHighlight()
        {
            var court = SavedCourt();
            InspectApplied(court); // Validate all current bindings without repeating the historical four-station migration.
            var targets = court.WeatherTargets;
            Color available = new Color(.18f, .78f, .94f, 1);
            Color hovered = new Color(.56f, 1, 1, 1);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Refine weather target highlight colours");
            try
            {
                foreach (var target in targets)
                {
                    Undo.RecordObject(target, "Adjust weather target highlight");
                    var serialized = new SerializedObject(target);
                    serialized.FindProperty("availableColor").colorValue = available;
                    serialized.FindProperty("hoveredColor").colorValue = hovered;
                    serialized.FindProperty("availableTint").floatValue = .5f;
                    serialized.FindProperty("hoveredTint").floatValue = .82f;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }
                var lines = InspectApplied(court);
                foreach (var target in targets)
                {
                    var serialized = new SerializedObject(target);
                    if (serialized.FindProperty("availableColor").colorValue != available ||
                        serialized.FindProperty("hoveredColor").colorValue != hovered ||
                        serialized.FindProperty("availableTint").floatValue != .5f ||
                        serialized.FindProperty("hoveredTint").floatValue != .82f)
                        throw new InvalidOperationException("Highlight values did not apply to " + target.DisplayName + ".");
                    lines.Add("PASS: " + target.StationIndex + " " + target.DisplayName +
                        "; available=(0.18,0.78,0.94,1) tint=0.5; hovered=(0.56,1,1,1) tint=0.82");
                }
                lines.Add("Only current targets' serialized highlight colours and tint strengths changed; existing geometry and weather bindings retained.");
                lines.Add("SKYWELL_WEATHER_TARGET_HIGHLIGHT_PASSED");
                EditorSceneManager.MarkSceneDirty(court.gameObject.scene);
                if (!EditorSceneManager.SaveScene(court.gameObject.scene))
                    throw new IOException("Could not save refined weather-target highlight.");
                Undo.CollapseUndoOperations(group);
                Directory.CreateDirectory(Evidence);
                File.WriteAllLines(Path.Combine(Evidence, "highlight-refine.txt"), lines);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static SkywellController SavedCourt()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != SkywellBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved, clean L5 author scene outside Play Mode.");
            var courts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SkywellController>(true)).ToArray();
            if (courts.Length != 1) throw new InvalidOperationException("Expected exactly one L5 controller in the active scene.");
            return courts[0];
        }

        private static SkywellWeatherConsole[] ValidateStations(SkywellController court,bool allowGrove=false)
        {
            int expectedCount=StationPaths.Length+(allowGrove&&court.Grove?1:0);
            if (court.Consoles == null || court.Consoles.Length != expectedCount)
                throw new InvalidOperationException("Expected the original Tower/Collector/Refuge/Village station order, followed by the authored grove when present.");
            var stations = court.Consoles;
            for (int i = 0; i < StationPaths.Length; i++)
            {
                var expected = Required(court.transform, StationPaths[i]);
                if (!stations[i] || stations[i].transform != expected || !stations[i].Zone || stations[i].Zone.Station != stations[i])
                    throw new InvalidOperationException("Station/zone mismatch at index " + i + ".");
                if (expected.GetComponent<Renderer>() || expected.GetComponent<Collider>())
                    throw new InvalidOperationException("Unexpected geometry on weather state anchor " + expected.name + ".");
            }
            if(expectedCount>StationPaths.Length)
            {
                var station=stations[SkywellDeerGrove.StationIndex];
                if(!station||!station.Zone||station.Zone.Station!=station||court.Grove.Zone!=station.Zone)
                    throw new InvalidOperationException("The appended grove station must retain its own linked local weather authority.");
            }
            return stations;
        }

        private static Transform[] LegacyRoots(SkywellWeatherConsole[] stations)
        {
            var result = new List<Transform>();
            foreach (var station in stations)
            {
                if (station.transform.childCount != LegacyParts.Length)
                    throw new InvalidOperationException("Unexpected children under " + station.name + "; inspect before deleting anything.");
                foreach (string name in LegacyParts) result.Add(Required(station.transform, name));
            }
            return result.ToArray();
        }

        private static Binding[] ResolveBindings(SkywellController court)
        {
            var root = court.transform;
            var tower = Required(root, "M1_Rotation_and_lift");
            var ring = Required(tower, "Outer_lift_ring_NON_ROTATING");
            var fixedFrame = Required(tower, "Fixed_quays_and_guide_frame");
            var stationRoots = court.Consoles.Select(c => c.transform).ToList();
            // Cargo and the separate drain interaction are not parts of the weather target.
            var excluded = new List<Transform>(stationRoots)
            {
                Required(ring, "Rotor_PIVOT/Freight_carrier"),
                Required(ring, "Rotor_PIVOT/Physical_reservoir_drain")
            };
            return new[]
            {
                Bind(tower, "中央升降塔", new[] { ring,
                    Required(fixedFrame, "Fixed_lower_lift_house"),
                    Required(fixedFrame, "Peripheral_rain_counterweights_FIXED") }, excluded),
                Bind(Required(root, "M2_Reflection_rail_and_ballast/Fixed_mirror_carriage/Fixed_reflector"), "采日镜", null, stationRoots),
                Bind(Required(root, "M3_Underwater_refuge_and_search/Fixed_refuge_FOUR_WALLS"), "避难装置", null, stationRoots),
                Bind(Required(root, "Revision_07_Borrowing_Sun/Settlement_sun_receiver"), "村庄接光盘", null, stationRoots)
            };
        }

        private static Binding Bind(Transform root, string label, Transform[] parts, List<Transform> excluded)
        {
            if (parts == null) parts = new[] { root };
            bool Included(Component c) => !excluded.Any(e => c.transform == e || c.transform.IsChildOf(e));
            var renderers = parts.SelectMany(p => p.GetComponentsInChildren<MeshRenderer>(true))
                .Where(Included).Cast<Renderer>().Distinct().ToArray();
            var colliders = parts.SelectMany(p => p.GetComponentsInChildren<Collider>(true))
                .Where(c => Included(c) && !c.isTrigger).Distinct().ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("No real visible geometry for " + label + ".");
            return new Binding { Root = root, Label = label, Renderers = renderers, Colliders = colliders };
        }

        private static List<string> InspectApplied(SkywellController court)
        {
            var stations = ValidateStations(court,true);
            var bindings = ResolveBindings(court);
            var serialized = new SerializedObject(court);
            var array = serialized.FindProperty("weatherTargets");
            if (array == null || array.arraySize != stations.Length || court.GetComponentsInChildren<SkywellWeatherTarget>(true).Length != stations.Length)
                throw new InvalidOperationException("Expected one serialized weather target per authored weather station.");
            var lines = new List<string> { DateTime.UtcNow.ToString("O") };
            for (int i = 0; i < bindings.Length; i++)
            {
                var target = array.GetArrayElementAtIndex(i).objectReferenceValue as SkywellWeatherTarget;
                var expected = bindings[i];
                if (!target || target.transform != expected.Root || target.StationIndex != i || target.DisplayName != expected.Label ||
                    !SameObjects(target.HighlightRenderers, expected.Renderers) || !SameObjects(target.HitColliders, expected.Colliders))
                    throw new InvalidOperationException("Target geometry or index mismatch at " + i + ".");
                var station = new SerializedObject(stations[i]);
                var heads = station.FindProperty("heads");
                var tokenDisplay = station.FindProperty("tokenDisplay");
                if ((heads != null && heads.arraySize != 0) || (tokenDisplay != null && tokenDisplay.objectReferenceValue) ||
                    stations[i].transform.childCount != 0)
                    throw new InvalidOperationException("Old weather console visuals remain at station " + i + ".");
                if ((i == 1 || i == 3) && expected.Colliders.Length != 0)
                    throw new InvalidOperationException("Collector and village targets must retain their collider-free optical geometry.");
                int readable = expected.Renderers.Count(r => r.GetComponent<MeshFilter>() &&
                    r.GetComponent<MeshFilter>().sharedMesh && r.GetComponent<MeshFilter>().sharedMesh.isReadable);
                if (expected.Colliders.Length == 0 && readable == 0)
                    throw new InvalidOperationException("Target has neither physical colliders nor readable mesh surfaces: " + expected.Label);
                lines.Add("PASS: " + i + " " + expected.Label + " root=" + HierarchyPath(expected.Root) +
                    "; mesh renderers=" + expected.Renderers.Length + "; solid colliders=" + expected.Colliders.Length + "; readable meshes=" + readable);
                lines.Add("State anchor=" + HierarchyPath(stations[i].transform) + "; zone=" + HierarchyPath(stations[i].Zone.transform));
            }
            if(court.Grove)
            {
                var target=array.GetArrayElementAtIndex(SkywellDeerGrove.StationIndex).objectReferenceValue as SkywellWeatherTarget;
                if(!target||target.StationIndex!=SkywellDeerGrove.StationIndex||!court.Grove.Tree||
                    target.HighlightRenderers.Length==0||target.HighlightRenderers.Any(r=>!r||!r.transform.IsChildOf(court.Grove.Tree))||
                    target.HitColliders.Any(c=>!c||!c.transform.IsChildOf(court.Grove.Tree)))
                    throw new InvalidOperationException("The grove target must be bound to its actual tree geometry at appended station 4.");
                bool clickable=target.HitColliders.Length>0||target.HighlightRenderers.Any(r=>r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh&&r.GetComponent<MeshFilter>().sharedMesh.isReadable);
                if(!clickable)throw new InvalidOperationException("The apple tree has no physical or readable-mesh hit geometry.");
                lines.Add("PASS: 4 "+target.DisplayName+" appended with its own local zone and visible tree geometry; original stations 0–3 retain their exact bindings.");
            }
            lines.Add("PASS: Collector and village target selection uses their existing meshes; no new collider is introduced.");
            return lines;
        }

        private static bool SameObjects<T>(T[] actual, T[] expected) where T : Object =>
            actual != null && actual.Length == expected.Length && actual.All(x => x) &&
            new HashSet<T>(actual).SetEquals(expected);

        private static void ValidateDeletionReferences(SkywellController court, SkywellWeatherConsole[] stations, HashSet<Object> deleted)
        {
            foreach (var component in court.GetComponentsInChildren<Component>(true))
            {
                if (!component || deleted.Contains(component) || component is Transform) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || !property.objectReferenceValue ||
                        !deleted.Contains(property.objectReferenceValue)) continue;
                    bool expected = stations.Contains(component as SkywellWeatherConsole) &&
                        (property.propertyPath.StartsWith("heads.Array.data[", StringComparison.Ordinal) || property.propertyPath == "tokenDisplay");
                    if (!expected) throw new InvalidOperationException("External reference to old console geometry: " +
                        component.name + "." + property.propertyPath);
                }
            }
        }

        private static List<Action> CaptureUnchanged(SkywellController court, HashSet<Object> deleted)
        {
            var checks = new List<Action>();
            foreach (var component in court.GetComponentsInChildren<Component>(true))
            {
                if (!component || deleted.Contains(component) || component == court || component is SkywellWeatherConsole) continue;
                if (component is Transform t)
                {
                    var parent = t.parent; var position = t.localPosition; var rotation = t.localRotation; var scale = t.localScale;
                    checks.Add(() => { if (!t || t.parent != parent || t.localPosition != position || t.localRotation != rotation || t.localScale != scale)
                        throw new InvalidOperationException("Unrelated transform changed: " + (t ? HierarchyPath(t) : "deleted")); });
                }
                else
                {
                    var original = EditorJsonUtility.ToJson(component);
                    checks.Add(() => { if (!component || EditorJsonUtility.ToJson(component) != original)
                        throw new InvalidOperationException("Unrelated serialized component changed: " + (component ? component.name : "deleted")); });
                }
            }
            return checks;
        }

        private static void VerifyUnchanged(List<Action> checks) { foreach (var check in checks) check(); }
        private static Transform Required(Transform root, string path) => root.Find(path) ? root.Find(path) :
            throw new InvalidOperationException("Missing authored object: " + HierarchyPath(root) + "/" + path);
        private static string HierarchyPath(Transform t) => t.parent ? HierarchyPath(t.parent) + "/" + t.name : t.name;

        private static string BackupScene(string scenePath)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../backups",
                DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmssfff") + "_before_weather_target_scene"));
            Directory.CreateDirectory(folder);
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scenePath));
            File.Copy(source, Path.Combine(folder, "05_Skywell.unity"), false);
            File.Copy(source + ".meta", Path.Combine(folder, "05_Skywell.unity.meta"), false);
            return folder;
        }

        private sealed class Binding
        {
            public Transform Root;
            public string Label;
            public Renderer[] Renderers;
            public Collider[] Colliders;
        }
    }
}
