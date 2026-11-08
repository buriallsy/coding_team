using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>One incremental migration of the saved L5 author scene. Never invokes a historical builder.</summary>
    public static class SkywellBorrowingSunMigration
    {
        private const string ScenePath = "Assets/BorrowingSun/Scenes/05_Skywell.unity";
        private const string RevisionName = "Revision_09_Borrowing_Sun_contract";
        private const string MaterialFolder = "Assets/BorrowingSun/Materials/";
        private static readonly Vector3 RelayLocal = new Vector3(0, 6.5f, 0);
        private static readonly Vector3 CollectorPosition = new Vector3(-8, 29.5f, 128);
        private static readonly Vector3 SolvedRelayPosition = new Vector3(0, 32.5f, 108);
        private static readonly Vector3 CorrectedMastXZ = new Vector3(-3.507072f, 0, -.797074f);

        [MenuItem("Borrowing Sun/Skywell/Migrate saved scene to Borrowing Sun contract")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved 05_Skywell author scene, exit Play Mode, and resolve unsaved edits before migrating.");
            var root = scene.GetRootGameObjects().Single(g => g.name == "L5_Skywell").transform;
            if (root.Find(RevisionName)) throw new InvalidOperationException("This migration is already present. Inspect the saved scene instead of rerunning it.");

            // Resolve every old reference before changing anything. The saved scene, not an old generator, supplies these.
            var court = One<SkywellController>(root);
            var journey = One<SkywellJourney>(root);
            var optics = One<SkywellOptics>(root);
            var windBell = One<SkywellWindBell>(root);
            var receiver = One<SkywellSunReceiver>(root);
            var lighting = One<SkywellSceneLighting>(root);
            var feedback = One<SkywellRainFeedback>(root);
            var rotor = Find(root, "Rotor_PIVOT");
            var mirror = Find(root, "Rotating_mirror");
            var mast = Find(root, "Rotating_mirror_mast");
            var plate = Find(root, "Fixed_reflector");
            var source = Find(root, "Sun_aperture");
            var underwater = Find(root, "M3_Underwater_refuge_and_search");
            var weatherLoop = Find(root, "M4_Weather_light_and_lure_loop");
            var oldToken = Find(root, "Support_pin");
            var stations = ReadArray<SkywellWeatherConsole>(court, "consoles");
            if (stations.Length != 3 || stations.Any(s => !s)) throw new InvalidOperationException("Expected the three authored L5 weather stations.");
            if (!lighting.sunLight || lighting.sunLight.type != LightType.Directional) throw new InvalidOperationException("The authored global Directional Light is required.");
            if (root.GetComponentsInChildren<Light>(true).Count(l => l.type == LightType.Directional) != 1)
                throw new InvalidOperationException("The level must have one global sun before migration.");
            RequireProperties(optics, "collector", "movingMirror", "fixedMirror", "receiver", "relaySurface", "movingAperture");
            RequireProperties(journey, "sunnyToken");
            RequireProperties(receiver, "villageNetwork");
            RequireProperties(windBell, "windZone");

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string workspace = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string backup = Path.Combine(workspace, "backups", "2026-09-24_before_borrowing_sun_scene_" + stamp);
            string sceneFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath));
            Directory.CreateDirectory(backup);
            File.Copy(sceneFile, Path.Combine(backup, "05_Skywell.unity"));
            File.Copy(sceneFile + ".meta", Path.Combine(backup, "05_Skywell.unity.meta"));
            File.WriteAllText(Path.Combine(backup, "RESTORE.txt"), "Saved author-scene backup made immediately before SkywellBorrowingSunMigration.Apply.\nRestore the .unity and matching .meta to " + ScenePath + ".\nRuntime source backup is managed by the task's separate pre-change snapshot.\n");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Migrate Skywell Borrowing Sun contract");
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Migrate Skywell Borrowing Sun contract");
            var before = new List<TransformRecord> { Record(mirror), Record(mast), Record(plate), Record(stations[1].transform), Record(oldToken) };
            try
            {
                lighting.ApplySettings();
                var revision = Empty(RevisionName, root, root.position);
                var stone = Material("Stone");
                var metal = Material("SkywellLiftMechanism");
                var copper = Material("Copper");
                var glow = EmissiveMaterial();

                // A: the optical pivot stays on the rotor axis at every Wind stop. The corrected Blender
                // offset is retained for the support mast, outside the cart footprint, with an overhead arm.
                Undo.RecordObjects(new Object[] { mirror, mast }, "Separate relay and cargo");
                mirror.localPosition = RelayLocal;
                Vector3 incident = (SolvedRelayPosition - CollectorPosition).normalized;
                Vector3 outgoing = (receiver.transform.position - SolvedRelayPosition).normalized;
                Vector3 solvedNormal = (incident - outgoing).normalized;
                mirror.localRotation = Quaternion.Inverse(Quaternion.Euler(0, 90, 0)) * Quaternion.LookRotation(solvedNormal, Vector3.up);
                var mirrorFace = mirror.Find("Reflective_face");
                var mirrorBack = mirror.Find("Mirror_back");
                if (!mirrorFace || !mirrorBack) throw new InvalidOperationException("Authored mirror surfaces are missing.");
                mirrorFace.localScale = new Vector3(3, 3, .08f);
                mirrorBack.localScale = new Vector3(3.2f, 3.2f, .08f);
                mirrorFace.GetComponent<Renderer>().sharedMaterial = glow;
                mast.localPosition = CorrectedMastXZ + Vector3.up * 4.2f;
                mast.localRotation = Quaternion.identity;
                mast.localScale = new Vector3(.22f, 8.4f, .22f);
                var support = Empty("Relay_overhead_cantilever", rotor, rotor.position);
                support.localPosition = Vector3.zero; support.localRotation = Quaternion.identity;
                LocalBeam("Cantilever_above_player_and_cart", support, CorrectedMastXZ + Vector3.up * 8.4f, Vector3.up * 8.4f, .2f, metal);
                LocalBox("Relay_top_bearing", support, new Vector3(0, 8.15f, 0), new Vector3(.32f, .5f, .32f), copper, true);

                var well = Empty("Terrain_sunlight_well", revision, Vector3.zero);
                // Open south toward the relay and open above toward the actual world sun.
                Box("Carved_well_west_cheek", well, new Vector3(-12.5f, 28.8f, 130), new Vector3(1.5f, 6, 8), stone, true);
                Box("Carved_well_east_cheek", well, new Vector3(-2.5f, 29, 133), new Vector3(1.5f, 6.8f, 4), stone, true);
                Box("Carved_well_back_rock", well, new Vector3(-8, 29.2f, 135), new Vector3(10, 6.4f, 2), stone, true);
                Box("Lightwell_rim_west", well, new Vector3(-12.5f, 32, 130), new Vector3(1.8f, .25f, 8.2f), copper, true);
                Box("Lightwell_rim_east", well, new Vector3(-2.5f, 32.5f, 133), new Vector3(1.8f, .25f, 4.2f), copper, true);
                Box("Lightwell_rim_back", well, new Vector3(-8, 32.5f, 135), new Vector3(10.3f, .25f, 2.3f), copper, true);
                // Keep the existing plate and carriage GUIDs. They are now stationary terrain machinery.
                plate.position = CollectorPosition;
                var collectorMast = plate.parent.Find("Mirror_mast");
                if (collectorMast) { collectorMast.localPosition = new Vector3(0, 1.5f, 0); collectorMast.localScale = new Vector3(.14f, 2.8f, .14f); }
                plate.Find("Reflective_face").localScale = new Vector3(3.6f, 3.6f, .08f);
                plate.Find("Mirror_back").localScale = new Vector3(3.8f, 3.8f, .08f);
                plate.Find("Reflective_face").GetComponent<Renderer>().sharedMaterial = glow;
                stations[1].transform.position = new Vector3(-5, 26, 129);
                stations[1].name = "Sunridge_weather_SINGLE_SLOT";
                Find(root, "Mirror_angle_handwheel").name = "Collector_focus_handwheel";
                var parking = Empty("Collector_focus_0_calibration", well, new Vector3(-8, 32.5f, 133));
                var calibration = Box("Mechanical_calibration_target", well, parking.position, new Vector3(1.1f, 1.1f, .18f), glow, true);
                calibration.rotation = Quaternion.LookRotation((CollectorPosition - parking.position).normalized);
                var relayTarget = Empty("Collector_focus_1_high_axis", well, SolvedRelayPosition);
                var sunlightSample = Empty("Sun_visibility_sample", well, CollectorPosition - lighting.sunLight.transform.forward * .15f);
                var collector = Undo.AddComponent<SkywellSunCollector>(well.gameObject);
                Ref(collector, "sunLight", lighting.sunLight); Ref(collector, "collectorPlate", plate);
                Ref(collector, "sunlightSample", sunlightSample);
                Refs(collector, "focusTargets", new Object[] { parking, relayTarget });
                Refs(collector, "sunSelfColliders", plate.GetComponentsInChildren<Collider>(true).Cast<Object>().ToArray());
                Ref(collector, "plateSurface", plate.Find("Reflective_face").GetComponent<Renderer>());
                Ref(collector, "incomingSunRay", Beam("Actual_sun_into_lightwell", well, .12f));

                var villageConsole = Object.Instantiate(stations[1], revision);
                Undo.RegisterCreatedObjectUndo(villageConsole.gameObject, "Create village weather station");
                villageConsole.name = "Shaded_village_weather_SINGLE_SLOT";
                villageConsole.transform.position = new Vector3(53, 26, 111);
                RemoveNamed(villageConsole.transform, "Sun_aperture");
                // Each core zone is distinct. The underwater box includes the existing bell rail, not the tower.
                var towerAnchor = Empty("Tower_zone_FIXED_origin", revision, new Vector3(0, 26, 108));
                var towerZone = Zone("WeatherZone_Tower", revision, stations[0], towerAnchor, 12);
                Set(towerZone, "halfHeight", p => p.floatValue = 8);
                var ridgeAnchor = Empty("Sunridge_zone_anchor", revision, new Vector3(-8, 28, 128));
                var ridgeZone = Zone("WeatherZone_Sunridge", revision, stations[1], ridgeAnchor, 5);
                Set(ridgeZone, "halfHeight", p => p.floatValue = 6);
                var refugeAnchor = Empty("Refuge_zone_anchor", revision, new Vector3(-13.5f, 11.5f, 88));
                var refugeVolume = Undo.AddComponent<BoxCollider>(refugeAnchor.gameObject);
                refugeVolume.isTrigger = true; refugeVolume.size = new Vector3(21, 7, 24);
                var refugeZone = Zone("WeatherZone_Refuge", revision, stations[2], refugeAnchor, 12, refugeVolume);
                var villageAnchor = Empty("Village_zone_anchor", revision, new Vector3(54, 27, 108));
                var villageZone = Zone("WeatherZone_ShadedVillage", revision, villageConsole, villageAnchor, 8);
                Set(villageZone, "halfHeight", p => p.floatValue = 7);
                var rain = root.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Rain_throughout_local_weather_zone");
                Ref(towerZone, "rain", rain);
                var weatherMaterial = rain.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                foreach(var zone in new[] { towerZone, ridgeZone, refugeZone, villageZone }) Ref(zone, "weatherMaterial", weatherMaterial);
                feedback.ConfigureWeatherZone(towerZone);
                Ref(collector, "weatherZone", ridgeZone);
                windBell.ConfigureWeatherZone(refugeZone);
                court.ConfigureWeatherStations(new[] { stations[0], stations[1], stations[2], villageConsole });
                towerZone.RegisterResponder(court); towerZone.RegisterResponder(feedback);
                ridgeZone.RegisterResponder(collector); refugeZone.RegisterResponder(windBell);

                Ref(optics, "collector", collector); Ref(optics, "movingMirror", mirror); Ref(optics, "fixedMirror", null);
                Ref(optics, "relaySurface", mirrorFace.GetComponent<Renderer>());
                Set(optics, "movingAperture", p => p.vector2Value = new Vector2(1.5f, 1.5f));
                Set(optics, "maxRayDistance", p => p.floatValue = 180);
                Set(optics, "reflectionLimit", p => p.intValue = 3);
                Remove(source); // This was the old weather-station gameplay emitter.

                oldToken.name = "Sunny_token_underwater";
                oldToken.position = new Vector3(8.5f, 10, 72);
                oldToken.gameObject.SetActive(true);
                foreach (Transform child in oldToken.Cast<Transform>().ToArray()) Remove(child);
                var tokenFace = LocalBox("Recovered_sunny_weather_token", oldToken, new Vector3(0, 1.4f, 0), new Vector3(.95f, 1.2f, .16f), copper, false);
                tokenFace.rotation = Quaternion.LookRotation((new Vector3(-14.5f, 12, 94) - tokenFace.position).normalized);
                var emblem = LocalBox("Sunny_token_sun_disc", tokenFace, new Vector3(0, 0, -.58f), new Vector3(.42f, .34f, .12f), glow, false);
                emblem.localRotation = Quaternion.identity;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    var ray = LocalBox("Sun_emblem_ray_" + i, tokenFace, new Vector3(Mathf.Cos(angle) * .32f, Mathf.Sin(angle) * .26f, -.58f), new Vector3(.1f, .055f, .12f), glow, false);
                    ray.localRotation = Quaternion.Euler(0, 0, i * 45);
                }
                Ref(journey, "sunnyToken", oldToken);
                RemoveNamed(underwater, "Inspection_key", "Keyed_search_gate");
                RemoveNamed(rotor, "Support_pin_socket");
                RemoveNamed(weatherLoop, "Underwater_guided_sun_0", "Underwater_guided_sun_1", "Fixed_light_guide_window");
                RemoveNamed(root, "Near_key_display_tray");

                var networkRoot = Empty("Village_borrowed_light_network", revision, Vector3.zero);
                var network = Undo.AddComponent<SkywellVillageLightNetwork>(networkRoot.gameObject);
                var nodePositions = new[] { new Vector3(49.6f, 33, 106), new Vector3(51, 28.2f, 108), new Vector3(53.5f, 27.5f, 110), new Vector3(55, 28.3f, 104), new Vector3(58, 29.8f, 110), new Vector3(60, 31.4f, 104) };
                var nodes = new Renderer[nodePositions.Length]; var lamps = new Light[nodePositions.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    var node = Box("Village_light_node_" + i, networkRoot, nodePositions[i], new Vector3(.5f, .55f, .35f), glow, false);
                    nodes[i] = node.GetComponent<Renderer>(); lamps[i] = Undo.AddComponent<Light>(node.gameObject);
                    lamps[i].type = LightType.Point; lamps[i].range = 5; lamps[i].intensity = 0; lamps[i].enabled = false;
                    lamps[i].shadows = LightShadows.None;
                    if (i > 0) WorldBeam("Village_light_conduit_" + i, networkRoot, nodePositions[i - 1], nodePositions[i], .06f, copper, false);
                }
                Refs(network, "nodes", nodes.Cast<Object>().ToArray()); Refs(network, "nodeLights", lamps.Cast<Object>().ToArray());
                Set(network, "secondsPerNode", p => p.floatValue = .45f);
                Ref(receiver, "villageNetwork", network); Set(receiver, "receivingSeconds", p => p.floatValue = 1.5f);
                // Face the real incoming reflected ray. No angle/state predicate substitutes for an intersection.
                receiver.transform.rotation = Quaternion.LookRotation((SolvedRelayPosition - receiver.transform.position).normalized, Vector3.up);

                // An empty slot and a short repeated sun glyph make the missing ability visible at the first approach.
                var entry = Empty("Entry_missing_sunny_marker", revision, new Vector3(-27.5f, 20, 109.6f));
                LocalBox("Empty_token_socket_pedestal", entry, new Vector3(0, .7f, 0), new Vector3(.8f, 1.4f, .65f), stone, true);
                LocalBox("Empty_sunny_socket", entry, new Vector3(0, 1.55f, 0), new Vector3(.65f, .12f, .32f), copper, false);

                collector.ResetCollector(); optics.Evaluate(false); receiver.ResetReceiver(); network.ResetNetwork();
                Physics.SyncTransforms();
                var zones = new[] { towerZone, ridgeZone, refugeZone, villageZone };
                for (int i = 0; i < zones.Length; i++) for (int j = i + 1; j < zones.Length; j++)
                    if (zones[i].Overlaps(zones[j])) throw new InvalidOperationException("Authored weather zones overlap: " + zones[i].name + " / " + zones[j].name);
                EditorUtility.SetDirty(court); EditorUtility.SetDirty(journey); EditorUtility.SetDirty(optics);
                EditorUtility.SetDirty(windBell); EditorUtility.SetDirty(receiver); EditorUtility.SetDirty(feedback);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity did not save the migrated author scene.");
                Undo.CollapseUndoOperations(undo);

                string reportFolder = Path.Combine(workspace, "unity_prototype/validation/current/skywell/borrowing-sun");
                Directory.CreateDirectory(reportFolder);
                var report = new MigrationReport {
                    utc = DateTime.UtcNow.ToString("O"), scene = scene.path, backup = backup,
                    correctedBlender = "art-source/skywell/_pre-cleanup-interactive-20260917-0510.blend",
                    geometryDecision = "Contract A: axis-elevated optical pivot; corrected Blender off-axis support location retained as mast footprint. Optical center intentionally differs from Blender to keep incoming light interceptable through Wind rotation.",
                    before = before.ToArray(), after = new[] { Record(mirror), Record(mast), Record(plate), Record(stations[1].transform), Record(oldToken), Record(villageConsole.transform) },
                    zones = new[] { Record(towerAnchor), Record(ridgeAnchor), Record(refugeAnchor), Record(villageAnchor) },
                    collectorSun = CheckRay(CollectorPosition + (-lighting.sunLight.transform.forward) * .23f, -lighting.sunLight.transform.forward, 250, plate),
                    collectorToHighRelay = CheckSegment(CollectorPosition, SolvedRelayPosition, plate, mirror),
                    highRelayToVillage = CheckSegment(SolvedRelayPosition, receiver.transform.position, mirror, receiver.transform),
                    villageSun = CheckRay(new Vector3(55, 29, 105), -lighting.sunLight.transform.forward, 250),
                    tokenView = CheckSegment(new Vector3(-14.5f, 12, 94), new Vector3(8.5f, 11.4f, 72), oldToken),
                    limits = "Static saved-scene rays only. Tower is saved low at yaw -90; high-axis segment probes use explicit proposed world endpoints. This does not verify Play Mode, rotating sweep, full route, or first-player understanding."
                };
                string reportPath = Path.Combine(reportFolder, "scene-migration.json");
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Debug.Log("SKYWELL_BORROWING_SUN_MIGRATED " + reportPath);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undo);
                throw;
            }
        }

        private static SkywellWeatherZone Zone(string name, Transform parent, SkywellWeatherConsole station, Transform anchor, float radius, Collider volume = null)
        {
            var go = Empty(name, parent, anchor.position);
            var zone = Undo.AddComponent<SkywellWeatherZone>(go.gameObject);
            zone.Initialize(station, anchor, radius, volume);
            EditorUtility.SetDirty(zone); EditorUtility.SetDirty(station);
            return zone;
        }

        private static T One<T>(Transform root) where T : Component => root.GetComponentsInChildren<T>(true).Single();
        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat") ?? throw new InvalidOperationException("Missing existing material: " + name);
        private static Material EmissiveMaterial()
        {
            const string assetPath = MaterialFolder + "SkywellBorrowedSunGlow.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing) return existing;
            var material = new Material(Material("SkywellReceiverGlow")) { name = "SkywellBorrowedSunGlow" };
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }
        private static void Remove(Transform target) { if (target) Undo.DestroyObjectImmediate(target.gameObject); }
        private static void RemoveNamed(Transform root, params string[] names)
        { foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToArray()) if (t) Remove(t); }
        private static Transform Empty(string name, Transform parent, Vector3 world)
        {
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create Borrowing Sun scene object");
            go.transform.SetParent(parent, false); go.transform.position = world; go.transform.rotation = Quaternion.identity;
            return go.transform;
        }
        private static Transform Box(string name, Transform parent, Vector3 world, Vector3 size, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Create Borrowing Sun whitebox"); go.transform.SetParent(parent, false);
            go.transform.position = world; go.transform.rotation = Quaternion.identity; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
            return go.transform;
        }
        private static Transform LocalBox(string name, Transform parent, Vector3 local, Vector3 size, Material material, bool solid)
        {
            var t = Box(name, parent, parent.TransformPoint(local), size, material, solid);
            t.localRotation = Quaternion.identity; return t;
        }
        private static void LocalBeam(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material material)
        {
            var t = LocalBox(name, parent, (a + b) / 2, new Vector3(thickness, thickness, Vector3.Distance(a, b)), material, true);
            t.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
        }
        private static void WorldBeam(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material material, bool solid)
        { var t = Box(name, parent, (a + b) / 2, new Vector3(thickness, thickness, Vector3.Distance(a, b)), material, solid); t.rotation = Quaternion.LookRotation(b - a, Vector3.up); }
        private static LineRenderer Beam(string name, Transform parent, float width)
        {
            var t = Empty(name, parent, Vector3.zero); var line = Undo.AddComponent<LineRenderer>(t.gameObject);
            line.sharedMaterial = Material("SkywellLightBeam"); line.useWorldSpace = true; line.positionCount = 2;
            line.startWidth = line.endWidth = width; line.numCapVertices = 3; line.enabled = false;
            return line;
        }
        private static void RequireProperties(Object target, params string[] fields)
        { var so = new SerializedObject(target); foreach (var f in fields) if (so.FindProperty(f) == null) throw new InvalidOperationException(target.GetType().Name + " lacks migrated field " + f); }
        private static void Set(Object target, string field, Action<SerializedProperty> write, bool required = true)
        {
            var so = new SerializedObject(target); var property = so.FindProperty(field);
            if (property == null) { if (required) throw new InvalidOperationException(target.GetType().Name + " lacks field " + field); return; }
            write(property); so.ApplyModifiedProperties(); EditorUtility.SetDirty(target);
        }
        private static void Ref(Object target, string field, Object value, bool required = true) => Set(target, field, p => p.objectReferenceValue = value, required);
        private static void Refs(Object target, string field, Object[] values)
        { Set(target, field, p => { p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }); }
        private static T[] ReadArray<T>(Object target, string field) where T : Object
        {
            var p = new SerializedObject(target).FindProperty(field); if (p == null) throw new InvalidOperationException("Missing " + field);
            return Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).objectReferenceValue as T).ToArray();
        }
        private static TransformRecord Record(Transform t) => new TransformRecord { name = t.name, localPosition = t.localPosition, worldPosition = t.position, localEuler = t.localEulerAngles, localScale = t.localScale };
        private static RayRecord CheckSegment(Vector3 a, Vector3 b, params Transform[] exclude) => CheckRay(a + (b - a).normalized * .025f, (b - a).normalized, Vector3.Distance(a, b) - .05f, exclude);
        private static RayRecord CheckRay(Vector3 origin, Vector3 direction, float distance, params Transform[] exclude)
        {
            var hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !exclude.Any(t => t && (h.transform == t || h.transform.IsChildOf(t)))).OrderBy(h => h.distance).ToArray();
            return new RayRecord { origin = origin, direction = direction, distance = distance, obstruction = hits.Length > 0 ? hits[0].transform.name : "", hitDistance = hits.Length > 0 ? hits[0].distance : -1 };
        }
        [Serializable] private sealed class TransformRecord { public string name; public Vector3 localPosition, worldPosition, localEuler, localScale; }
        [Serializable] private sealed class RayRecord { public Vector3 origin, direction; public float distance, hitDistance; public string obstruction; }
        [Serializable] private sealed class MigrationReport
        {
            public string utc, scene, backup, correctedBlender, geometryDecision, limits;
            public TransformRecord[] before, after, zones;
            public RayRecord collectorSun, collectorToHighRelay, highRelayToVillage, villageSun, tokenView;
        }
    }
}
