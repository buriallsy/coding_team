using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BorrowingSun.Editor
{
    /// <summary>Adds one optional grove; existing authored geometry and the four weather stations remain intact.</summary>
    public static class SkywellDeerGroveSetup
    {
        private const string RootName = "Revision_15_Hidden_deer_grove";
        private const string Art = "Assets/BorrowingSun/Art/SkywellDeerGrove";
        private const string NorthFloor = "M1_Rotation_and_lift/Fixed_quays_and_guide_frame/North_fixed_service_gallery/Outer_mountain_court";
        private static readonly Vector3[] PathPoints =
        {
            new Vector3(53.5f,26,114.6f), new Vector3(46,26,114.6f),
            new Vector3(42,26,114.6f), new Vector3(39.4f,26,113.8f),
            new Vector3(33,26.5f,113.6f), new Vector3(30,28,120),
            new Vector3(28,28,127), new Vector3(28,28,132)
        };
        private static readonly Vector3[] CloudPoints =
        {
            new Vector3(14,28.5f,136), new Vector3(9,28,133.5f), new Vector3(3.6f,26.6f,130.5f)
        };
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../validation/current/skywell/2026-10-19-deer-grove"));
        private static readonly List<string> CreatedAssets = new List<string>();

        [MenuItem("Borrowing Sun/Skywell/Add hidden deer grove")]
        public static void Apply()
        {
            var court = SavedCourt();
            var scene = court.gameObject.scene;
            if (court.transform.Find(RootName) || court.GetComponent<SkywellDeerGrove>() || court.Consoles.Length != 4 || court.WeatherTargets.Length != 4)
                throw new InvalidOperationException("Expected the saved four-target scene with no grove. Use Audit after installation.");
            if (AssetDatabase.IsValidFolder(Art))
                throw new InvalidOperationException("Grove asset folder already exists; inspect it before repeating setup.");
            var northFloor = Required(court.transform, NorthFloor).GetComponent<Collider>();
            if (!northFloor || !northFloor.enabled || northFloor.isTrigger)
                throw new InvalidOperationException("The original northern landing floor must have solid collision.");
            var originalStations = court.Consoles.ToArray();
            var originalTargets = court.WeatherTargets.ToArray();
            var originalZones = originalStations.Select(s => s.Zone).ToArray();
            var checks = CaptureUnchanged(court);
            var sun = RenderSettings.sun;
            var fog = RenderSettings.fogColor;
            var ambient = RenderSettings.ambientLight;
            var sky = RenderSettings.skybox;
            string backup = Backup(scene.path);
            CreatedAssets.Clear();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add hidden mountain grove and deer cloud route");
            try
            {
                EnsureFolder(Art);
                var rock = Material("Mountain_slate", new Color(.29f,.37f,.40f));
                var rockLight = Material("Mountain_face", new Color(.43f,.51f,.51f));
                var turf = Material("Grove_ground", new Color(.46f,.57f,.49f));
                var pathMaterial = Material("Stone_path", new Color(.59f,.64f,.60f));
                var bark = Material("Apple_bark", new Color(.32f,.25f,.20f));
                var leaf = Material("Apple_leaves", new Color(.29f,.48f,.32f));
                var leafLight = Material("Apple_leaf_light", new Color(.43f,.58f,.35f));
                var apple = Material("Red_apples", new Color(.77f,.20f,.14f));
                var deerWhite = Material("Deer_ivory", new Color(.85f,.88f,.80f));
                var deerDark = Material("Deer_hooves_eyes", new Color(.20f,.27f,.29f));
                var antler = Material("Deer_antlers", new Color(.67f,.55f,.34f));
                var cloud = Material("Cloud_translucent", new Color(.91f,.96f,.98f,.25f), true);
                var cloudFloor = Material("Cloud_landing", new Color(.80f,.89f,.90f));
                var sphere = SphereMesh("Faceted_round", 10, 5);
                var cylinder = PrismMesh("Faceted_cylinder", 10, .5f, .5f, 1);
                var root = Node(court.transform, RootName, Vector3.zero);
                var hiddenPath = Node(root, "Hidden_path_from_shaded_village", Vector3.zero);
                for (int i=0;i<PathPoints.Length-1;i++)
                {
                    var floor = AddMesh(hiddenPath, "Path_"+i.ToString("00"), RampMesh(PathPoints[i],PathPoints[i+1],2.2f,.45f,"Path_"+i), pathMaterial);
                    Solid(floor);
                    var marker = Node(hiddenPath,"Waypoint_"+i.ToString("00"),PathPoints[i]);
                    if (i>0)
                    {
                        var corner=AddMesh(hiddenPath,"Corner_"+i,PrismMesh("Corner_"+i,10,1.15f,1.05f,.40f),pathMaterial);
                        corner.position=PathPoints[i]-Vector3.up*.20f;
                        if(i==5)corner.localScale=new Vector3(.6f/1.15f,1,.6f/1.15f);
                        Solid(corner);
                    }
                }
                Node(hiddenPath,"Waypoint_07",PathPoints[PathPoints.Length-1]);
                Node(root,"Grove_entry",new Vector3(28,28,132));
                var island=AddMesh(root,"Grove_island_solid",PrismMesh("Grove_island",14,7.5f,3.8f,17),rock);
                island.position=new Vector3(25,19.5f,138);
                var islandCollider=Solid(island);
                var ground=AddMesh(root,"Grove_grass_surface",PrismMesh("Grove_grass",14,7.47f,7.47f,.035f),turf);
                ground.position=new Vector3(25,28.008f,138);
                // The grass has no second collision plane. Island top at Y=28 is the one authoritative floor.
                var shade=AddMesh(root,"Grove_sun_occluding_peak",PrismMesh("Grove_shade_peak",7,1.3f,6.3f,44),rock);
                shade.position=new Vector3(18,32,150);shade.localScale=new Vector3(1,1,1.2f);Solid(shade);
                var backdrop=AddMesh(root,"Distant_grove_peak",PrismMesh("Distant_peak",6,1.2f,5.2f,23),rockLight);
                backdrop.position=new Vector3(35,24.5f,153);backdrop.localScale=new Vector3(1,1,1.2f);
                // Decorative background is not a platform or a substitute for a gameplay occluder.
                var clouds=Node(root,"White_clouds_non_solid",Vector3.zero);
                Cloud(clouds,"Path_cloud_A",new Vector3(44,27.4f,117.2f),new Vector3(6.2f,2.1f,2.5f),sphere,cloud);
                Cloud(clouds,"Path_cloud_B",new Vector3(36,26.9f,111.9f),new Vector3(5.5f,1.6f,2.3f),sphere,cloud);
                Cloud(clouds,"Path_cloud_C",new Vector3(32,28.4f,123.7f),new Vector3(3.1f,2.2f,5.5f),sphere,cloud);
                Cloud(clouds,"Path_cloud_D",new Vector3(26.4f,28.5f,128.8f),new Vector3(3.2f,2.4f,3.4f),sphere,cloud);
                Cloud(clouds,"Island_cloud_south",new Vector3(23,26.1f,131.3f),new Vector3(9,3,4),sphere,cloud);
                Cloud(clouds,"Island_cloud_north",new Vector3(27,25.6f,144),new Vector3(10,3.5f,4.5f),sphere,cloud);
                Cloud(clouds,"Valley_cloud_low",new Vector3(16,19,127),new Vector3(12,4,7),sphere,cloud);

                var tree=Node(root,"Apple_tree_growth_root",new Vector3(24,28,139));
                var trunk=Shape(tree,"Trunk",cylinder,bark,new Vector3(0,1.5f,0),new Vector3(.55f,3,.55f));Solid(trunk);
                Branch(tree,"Branch_left",new Vector3(0,1.8f,0),new Vector3(-1.3f,3,0),.20f,cylinder,bark);
                Branch(tree,"Branch_right",new Vector3(0,2,0),new Vector3(1.4f,3.1f,.45f),.20f,cylinder,bark);
                Branch(tree,"Branch_back",new Vector3(0,2,0),new Vector3(.2f,3.4f,-1),.17f,cylinder,bark);
                Shape(tree,"Canopy_left",sphere,leaf,new Vector3(-1.1f,3.25f,0),new Vector3(2.6f,2.3f,2.5f));
                Shape(tree,"Canopy_right",sphere,leafLight,new Vector3(1,3.5f,.4f),new Vector3(2.6f,2.4f,2.7f));
                Shape(tree,"Canopy_crown",sphere,leaf,new Vector3(0,4.15f,-.45f),new Vector3(2.9f,2.3f,2.5f));
                var initialFruit=new[]{Fruit(tree,"Small_apple",new Vector3(1.85f,2.95f,.1f),sphere,cylinder,apple,bark)};
                var matureLocations=new[]{new Vector3(1.75f,3.45f,.9f),new Vector3(-1.7f,2.8f,.7f),new Vector3(.7f,2.8f,1.5f),new Vector3(-.8f,3,-1.35f),new Vector3(1.25f,3.4f,-1.25f),new Vector3(-1.9f,3.35f,-.5f)};
                var matureFruit=new Transform[matureLocations.Length];
                for(int i=0;i<matureFruit.Length;i++){matureFruit[i]=Fruit(tree,"Grown_apple_"+i,matureLocations[i],sphere,cylinder,apple,bark);matureFruit[i].gameObject.SetActive(false);}

                var deer=Deer(root,sphere,cylinder,deerWhite,deerDark,antler,out Transform saddle,out Transform mount,out Transform head);
                var home=Node(root,"Deer_home",new Vector3(27.5f,28,138));home.rotation=Quaternion.Euler(0,270,0);
                deer.SetPositionAndRotation(home.position,home.rotation);
                var cloudRoute=Node(root,"Unlocked_cloud_platforms",new Vector3(14,28,136));
                var cloudColliders=new List<Collider>();
                for(int i=0;i<CloudPoints.Length;i++)
                {
                    var top=AddMesh(cloudRoute,"Cloud_platform_"+i,PrismMesh("Cloud_platform_"+i,12,2.25f,1.65f,.75f),cloudFloor);
                    top.position=CloudPoints[i]-Vector3.up*.375f;cloudColliders.Add(Solid(top));
                    Node(cloudRoute,"Landing_"+i,CloudPoints[i]);
                    Cloud(cloudRoute,"Cloud_soft_rim_"+i,CloudPoints[i]-Vector3.up*.60f,new Vector3(5.3f,1.35f,4.8f),sphere,cloud);
                }
                cloudRoute.gameObject.SetActive(false);
                var ride=Undo.AddComponent<SkywellDeerRide>(court.gameObject);
                ride.Configure(court,deer,saddle,mount,new[]{islandCollider}.Concat(cloudColliders).Concat(new[]{northFloor}).ToArray(),home);
                var anchor=Node(root,"Grove_weather_state",new Vector3(25,29,138));
                var station=Undo.AddComponent<SkywellWeatherConsole>(anchor.gameObject);
                var zone=Undo.AddComponent<SkywellWeatherZone>(anchor.gameObject);
                zone.Initialize(station,anchor,8);
                var zs=new SerializedObject(zone);zs.FindProperty("halfHeight").floatValue=6;
                zs.FindProperty("weatherMaterial").objectReferenceValue=GroveWeatherMaterial();zs.ApplyModifiedProperties();
                var target=Undo.AddComponent<SkywellWeatherTarget>(tree.gameObject);
                var targetRenderers=tree.GetComponentsInChildren<Renderer>(true).Where(r=>!r.transform.name.Contains("Apple")&&!r.transform.name.Contains("apple")&&!r.transform.IsChildOf(initialFruit[0])&&!matureFruit.Any(f=>r.transform.IsChildOf(f))).ToArray();
                target.Configure(4,"山间苹果树",targetRenderers,new[]{trunk.GetComponent<Collider>()});
                var ts=new SerializedObject(target);
                ts.FindProperty("availableColor").colorValue=new Color(.18f,.78f,.94f,1);
                ts.FindProperty("hoveredColor").colorValue=new Color(.56f,1,1,1);
                ts.FindProperty("availableTint").floatValue=.5f;ts.FindProperty("hoveredTint").floatValue=.82f;
                ts.ApplyModifiedProperties();
                var grove=Undo.AddComponent<SkywellDeerGrove>(court.gameObject);
                grove.Configure(court,zone,tree,initialFruit,matureFruit,new[]{islandCollider},head,cloudRoute,ride);
                grove.ConfigureFeedingVisuals(Required(deer,"Long_neck"),Required(head,"Muzzle"));
                Undo.RecordObject(court,"Append grove weather and progression");
                court.ConfigureWeatherStations(originalStations.Concat(new[]{station}).ToArray());
                court.ConfigureWeatherTargets(originalTargets.Concat(new[]{target}).ToArray());
                court.ConfigureGrove(grove);
                foreach(var c in new Component[]{court,ride,grove,station,zone,target})EditorUtility.SetDirty(c);
                Physics.SyncTransforms();
                foreach(var check in checks)check();
                for(int i=0;i<4;i++)if(court.Consoles[i]!=originalStations[i]||court.WeatherTargets[i]!=originalTargets[i]||court.Consoles[i].Zone!=originalZones[i])throw new InvalidOperationException("Existing weather binding changed.");
                if(RenderSettings.sun!=sun||RenderSettings.fogColor!=fog||RenderSettings.ambientLight!=ambient||RenderSettings.skybox!=sky)throw new InvalidOperationException("Existing environment settings changed.");
                var lines=Inspect(court);
                lines.Insert(1,"Backup="+backup);
                lines.Add("PASS: All prior transforms and serialized components are unchanged except the Controller's appended grove references.");
                lines.Add("PASS: Original four targets, zones, weather state anchors, sun and background are retained.");
                lines.Add("SKYWELL_DEER_GROVE_SETUP_PASSED");
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save the additive grove scene.");
                Undo.CollapseUndoOperations(group);
                Directory.CreateDirectory(Evidence);File.WriteAllLines(Path.Combine(Evidence,"scene-setup.txt"),lines);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach(string asset in CreatedAssets)AssetDatabase.DeleteAsset(asset);
                if(AssetDatabase.IsValidFolder(Art))AssetDatabase.DeleteAsset(Art);
                throw;
            }
        }

        [MenuItem("Borrowing Sun/Skywell/Refine deer feeding visual references")]
        public static void RefineFeeding()
        {
            var court=SavedCourt();var grove=court.Grove;
            if(!grove)throw new InvalidOperationException("Install the grove before refining its feeding references.");
            var visual=Required(court.transform,RootName+"/White_deer_visual");
            var neck=Required(visual,"Long_neck");var muzzle=Required(visual,"Head/Muzzle");
            string backup=Backup(court.gameObject.scene.path);
            Undo.RecordObject(grove,"Bind existing deer neck and muzzle for feeding");
            grove.ConfigureFeedingVisuals(neck,muzzle);EditorUtility.SetDirty(grove);
            // Use the same soft particle asset as the original four zones; the fallback has square billboards.
            Undo.RecordObject(grove.Zone,"Use the authored soft weather particle material");
            var zoneData=new SerializedObject(grove.Zone);
            zoneData.FindProperty("weatherMaterial").objectReferenceValue=GroveWeatherMaterial();zoneData.ApplyModifiedProperties();
            EditorUtility.SetDirty(grove.Zone);
            if(grove.FeedingNeck!=neck||grove.FeedingMuzzle!=muzzle)throw new InvalidOperationException("Feeding visual references did not apply.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(court.gameObject.scene);
            if(!EditorSceneManager.SaveScene(court.gameObject.scene))throw new IOException("Could not save feeding visual references.");
            Directory.CreateDirectory(Evidence);
            File.WriteAllLines(Path.Combine(Evidence,"feeding-refine.txt"),new[]{DateTime.UtcNow.ToString("O"),"Backup="+backup,
                "Existing Grove neck/muzzle references and its local weather particle material are bound. Original transforms, collision and optics remain unchanged.",
                "SKYWELL_DEER_FEEDING_REFERENCES_PASSED"});
        }

        private static Material GroveWeatherMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/SkywellLocalWeatherSoft.mat");
            if(!material)throw new InvalidOperationException("The existing local weather particle material is missing.");
            return material;
        }

        [MenuItem("Borrowing Sun/Skywell/Refine hidden path uphill seam")]
        public static void RefinePathSeam()
        {
            var court=SavedCourt();var corner=Required(court.transform,RootName+"/Hidden_path_from_shaded_village/Corner_5");
            string backup=Backup(court.gameObject.scene.path);
            Undo.RecordObject(corner,"Reduce slope junction lip to a walkable step");
            corner.localScale=new Vector3(.6f/1.15f,1,.6f/1.15f);EditorUtility.SetDirty(corner);
            EditorSceneManager.MarkSceneDirty(court.gameObject.scene);
            if(!EditorSceneManager.SaveScene(court.gameObject.scene))throw new IOException("Could not save hidden path seam.");
            Directory.CreateDirectory(Evidence);File.WriteAllLines(Path.Combine(Evidence,"path-seam-refine.txt"),new[]{DateTime.UtcNow.ToString("O"),"Backup="+backup,
                "Only Corner_5 X/Z scale changed: top radius 1.15m to .6m. Original ramp meshes, player controller and step height remain unchanged.",
                "SKYWELL_DEER_PATH_SEAM_SAVED"});
        }

        [MenuItem("Borrowing Sun/Skywell/Audit hidden deer grove")]

        public static void Audit()
        {
            var court=SavedCourt();var lines=Inspect(court);
            var root=Required(court.transform,RootName);
            var path=Required(root,"Hidden_path_from_shaded_village");
            const string prefix="Waypoint_";
            var waypoints=path.Cast<Transform>()
                .Where(t=>t.name.StartsWith(prefix,StringComparison.Ordinal)&&int.TryParse(t.name.Substring(prefix.Length),out _))
                .OrderBy(t=>int.Parse(t.name.Substring(prefix.Length))).ToArray();
            if(waypoints.Length<2||waypoints.Select(t=>int.Parse(t.name.Substring(prefix.Length))).Distinct().Count()!=waypoints.Length)
                throw new InvalidOperationException("Saved cave route needs distinct numbered waypoints.");
            if(root.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.StartsWith("Path_cloud_",StringComparison.Ordinal)&&r.enabled&&r.gameObject.activeInHierarchy))
                throw new InvalidOperationException("The walking cave approach must not retain visible path clouds.");
            var grove=court.Grove;var platforms=grove.Ride.SafePlatforms;
            if(platforms==null||platforms.Length!=6||platforms.Any(p=>!p)||platforms.Distinct().Count()!=platforms.Length)
                throw new InvalidOperationException("Saved route must bind island, four distinct cloud colliders, then northern floor.");
            int cloudCount=platforms.Length-2;
            if(platforms[0].transform.IsChildOf(grove.CloudPlatforms)||platforms[platforms.Length-1].transform.IsChildOf(grove.CloudPlatforms))
                throw new InvalidOperationException("Island and northern floor must remain separate from the hidden cloud root.");
            var landings=Enumerable.Range(0,cloudCount).Select(i=>Required(grove.CloudPlatforms,"Landing_"+i)).ToArray();
            for(int i=0;i<cloudCount;i++)
                if(!platforms[i+1].transform.IsChildOf(grove.CloudPlatforms))
                    throw new InvalidOperationException("Saved cloud route ordering differs from its landing root: "+i);
            // Inspect also supports the historical Apply. Only this current Audit replaces
            // its old generated-coordinate report with the saved author scene references.
            lines.RemoveAll(line=>line.StartsWith("PASS: Physical island and three cloud landing meshes",StringComparison.Ordinal)||
                line.StartsWith("Path centres=",StringComparison.Ordinal)||line.StartsWith("Cloud landing tops=",StringComparison.Ordinal));
            lines.Add("PASS: Saved cave path has "+waypoints.Length+" ordered waypoints and no visible path clouds.");
            lines.Add("PASS: Physical island, "+cloudCount+" cloud landing colliders and original northern floor retain the existing player controller.");
            lines.Add("Saved cave centres="+string.Join(" -> ",waypoints.Select(p=>p.name+"="+p.position.ToString("F2"))));
            lines.Add("Saved cloud landing markers="+string.Join(" -> ",landings.Select(p=>p.name+"="+p.position.ToString("F2"))));
            lines.Add("Saved landing colliders="+string.Join(" -> ",platforms.Select(p=>p.name)));
            lines.Add("Read-only scene audit. This does not establish that jumping or the complete route has passed.");
            lines.Add("SKYWELL_DEER_GROVE_AUDIT_PASSED");
            string currentEvidence=Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-19-cave-four-clouds"));
            Directory.CreateDirectory(currentEvidence);File.WriteAllLines(Path.Combine(currentEvidence,"scene-audit.txt"),lines);
        }

        private static List<string> Inspect(SkywellController court)
        {
            var root=Required(court.transform,RootName);
            var grove=court.GetComponent<SkywellDeerGrove>();
            var ride=court.GetComponent<SkywellDeerRide>();
            if(!grove||!ride||court.Grove!=grove||grove.Ride!=ride||court.Consoles.Length!=5||court.WeatherTargets.Length!=5)
                throw new InvalidOperationException("Grove references or appended weather target are incomplete.");
            if(court.WeatherTargets[4].StationIndex!=4||court.Consoles[4].Zone!=grove.Zone||!grove.Zone.RegisteredResponders.Contains(grove))
                throw new InvalidOperationException("Grove weather binding is invalid.");
            if(!grove.FeedingNeck||!grove.FeedingMuzzle)throw new InvalidOperationException("The existing deer neck and muzzle must be explicitly bound for visible feeding.");
            if(grove.CloudPlatforms.gameObject.activeSelf||grove.IsGrown||grove.Unlocked)
                throw new InvalidOperationException("Author scene must start with a small tree and hidden cloud platforms.");
            var visual=Required(root,"White_deer_visual");
            if(visual.GetComponentsInChildren<Collider>(true).Length!=0||visual.GetComponentsInChildren<CharacterController>(true).Length!=0)
                throw new InvalidOperationException("Deer visual must not introduce a second physical controller or collision body.");
            var shade=Required(root,"Grove_sun_occluding_peak").GetComponent<Collider>();
            var sun=RenderSettings.sun;
            if(!sun)throw new InvalidOperationException("The existing directional sun is required for the physical shade check.");
            if(!shade.Raycast(new Ray(grove.Tree.position+Vector3.up*3,-sun.transform.forward),out _,200))
                throw new InvalidOperationException("The new mountain must actually shade the grove tree from the authored sun.");
            var newSolids=root.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled&&!c.isTrigger).ToArray();
            var optics=court.Optics;
            foreach(var collider in newSolids)
            {
                if(collider.Raycast(new Ray(optics.Collector.Plate.position,-sun.transform.forward),out _,200))
                    throw new InvalidOperationException("New geometry blocks the original collector's sunlight: "+collider.name);
                Vector3 from=optics.MovingMirror.position,to=optics.Receiver.position,delta=to-from;
                if(collider.Raycast(new Ray(from,delta.normalized),out _,delta.magnitude))
                    throw new InvalidOperationException("New geometry blocks the existing relay-to-village corridor: "+collider.name);
            }
            var lines=new List<string>{"UTC="+DateTime.UtcNow.ToString("O"),"Scene="+court.gameObject.scene.path};
            lines.Add("PASS: Optional grove is station 4; original station indices 0–3 stay unchanged.");
            lines.Add("PASS: One initial fruit; six mature fruit; growth/fruit/gratitude belong to SkywellDeerGrove.");
            lines.Add("PASS: Physical island and three cloud landing meshes; pure deer visual uses the existing player controller.");
            lines.Add("PASS: Authored sun physically blocked at tree; new solids do not intersect original collector sun ray or relay-to-village corridor.");
            lines.Add("Path centres="+string.Join(" -> ",PathPoints.Select(p=>p.ToString("F2"))));
            lines.Add("Cloud landing tops="+string.Join(" -> ",CloudPoints.Select(p=>p.ToString("F2"))));
            lines.Add("Local reference proposal only: platform scale and jumps still require real player-input PlayMode validation.");
            return lines;
        }

        private static Transform Deer(Transform root,Mesh round,Mesh cylinder,Material white,Material dark,Material antler,
            out Transform saddle,out Transform mount,out Transform head)
        {
            var deer=Node(root,"White_deer_visual",Vector3.zero);
            Shape(deer,"Body",round,white,new Vector3(0,1.20f,0),new Vector3(.83f,.92f,1.95f));
            Shape(deer,"Chest",round,white,new Vector3(0,1.35f,.59f),new Vector3(.77f,1.03f,.78f));
            Branch(deer,"Long_neck",new Vector3(0,1.40f,.65f),new Vector3(0,2.03f,.94f),.40f,cylinder,white);
            head=LocalNode(deer,"Head",new Vector3(0,2.08f,.94f));
            Shape(head,"Head_shape",round,white,new Vector3(0,0,.14f),new Vector3(.43f,.44f,.75f));
            Shape(head,"Muzzle",round,dark,new Vector3(0,-.06f,.47f),new Vector3(.30f,.22f,.28f));
            Shape(head,"Left_eye",round,dark,new Vector3(-.204f,.07f,.22f),Vector3.one*.075f);
            Shape(head,"Right_eye",round,dark,new Vector3(.204f,.07f,.22f),Vector3.one*.075f);
            var leftEar=Shape(head,"Left_ear",round,white,new Vector3(-.28f,.17f,-.12f),new Vector3(.19f,.48f,.18f));leftEar.localRotation=Quaternion.Euler(0,0,42);
            var rightEar=Shape(head,"Right_ear",round,white,new Vector3(.28f,.17f,-.12f),new Vector3(.19f,.48f,.18f));rightEar.localRotation=Quaternion.Euler(0,0,-42);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side;
                Branch(head,"Antler_stem_"+side,new Vector3(x*.16f,.16f,-.08f),new Vector3(x*.32f,.87f,-.27f),.075f,cylinder,antler);
                Branch(head,"Antler_outer_"+side,new Vector3(x*.26f,.55f,-.20f),new Vector3(x*.58f,.82f,-.12f),.055f,cylinder,antler);
                Branch(head,"Antler_tip_"+side,new Vector3(x*.32f,.80f,-.25f),new Vector3(x*.43f,1.06f,-.10f),.045f,cylinder,antler);
                Branch(head,"Antler_front_"+side,new Vector3(x*.20f,.39f,-.14f),new Vector3(x*.30f,.56f,.16f),.05f,cylinder,antler);
            }
            string[] names={"LF","RF","LB","RB"};
            for(int i=0;i<4;i++)
            {
                float x=(i%2==0?-.29f:.29f),z=i<2?.57f:-.57f;
                var leg=LocalNode(deer,names[i],new Vector3(x,1.11f,z));
                Branch(leg,"Upper_leg",Vector3.zero,new Vector3(0,-.52f,-.055f),.16f,cylinder,white);
                Branch(leg,"Lower_leg",new Vector3(0,-.52f,-.055f),new Vector3(0,-1.02f,0),.11f,cylinder,white);
                Shape(leg,"Hoof",round,dark,new Vector3(0,-1.025f,.025f),new Vector3(.18f,.16f,.25f));
            }
            var tail=Shape(deer,"Tail",round,white,new Vector3(0,1.39f,-1.02f),new Vector3(.20f,.23f,.48f));tail.localRotation=Quaternion.Euler(-35,0,0);
            saddle=LocalNode(deer,"Saddle",new Vector3(0,1.45f,0));
            mount=LocalNode(deer,"Mount_point",new Vector3(1.15f,0,0));
            return deer;
        }

        private static Transform Fruit(Transform parent,string name,Vector3 at,Mesh round,Mesh cylinder,Material red,Material bark)
        {
            var fruit=LocalNode(parent,name,at);
            Shape(fruit,"Fruit_body",round,red,Vector3.zero,new Vector3(.34f,.34f,.34f));
            Shape(fruit,"Fruit_stem",cylinder,bark,new Vector3(0,.19f,0),new Vector3(.025f,.13f,.025f));
            return fruit;
        }
        private static void Cloud(Transform parent,string name,Vector3 at,Vector3 scale,Mesh mesh,Material material)
        {
            var t=AddMesh(parent,name,mesh,material);t.position=at;t.localScale=scale;
            var r=t.GetComponent<MeshRenderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        private static Transform Shape(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale)
        {var t=AddMesh(parent,name,mesh,material);t.localPosition=position;t.localScale=scale;return t;}
        private static Transform Branch(Transform parent,string name,Vector3 a,Vector3 b,float diameter,Mesh mesh,Material material)
        {var t=Shape(parent,name,mesh,material,(a+b)*.5f,new Vector3(diameter,(b-a).magnitude,diameter));t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t;}
        private static Transform LocalNode(Transform parent,string name,Vector3 at)
        {var t=Node(parent,name,Vector3.zero);t.localPosition=at;return t;}
        private static Transform Node(Transform parent,string name,Vector3 at)
        {var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"Grove node");g.transform.SetParent(parent,false);g.transform.position=at;return g.transform;}
        private static Transform AddMesh(Transform parent,string name,Mesh mesh,Material material)
        {var t=LocalNode(parent,name,Vector3.zero);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;}
        private static Collider Solid(Transform t)
        {var c=t.gameObject.AddComponent<MeshCollider>();c.sharedMesh=t.GetComponent<MeshFilter>().sharedMesh;return c;}
        private static Material Material(string name,Color color,bool transparent=false)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);
            if(transparent)
            {
                m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",(float)CullMode.Back);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=(int)RenderQueue.Transparent;
            }
            string path=Art+"/"+name+".mat";AssetDatabase.CreateAsset(m,path);CreatedAssets.Add(path);return m;
        }
        private static Mesh RampMesh(Vector3 a,Vector3 b,float width,float depth,string name)
        {
            var d=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,d)*width*.5f;
            a-=d*.10f;b+=d*.10f;
            var tl=a-side;var tr=a+side;var fl=b-side;var fr=b+side;var down=Vector3.down*depth;
            var m=new MeshData();m.Quad(tl,fl,fr,tr);m.Quad(tl+down,tr+down,fr+down,fl+down);
            m.Quad(tl,tr,tr+down,tl+down);m.Quad(fr,fl,fl+down,fr+down);m.Quad(fl,tl,tl+down,fl+down);m.Quad(tr,fr,fr+down,tr+down);
            return m.Save(name);
        }
        private static Mesh PrismMesh(string name,int sides,float topRadius,float bottomRadius,float height)
        {
            var m=new MeshData();
            for(int i=0;i<sides;i++)
            {
                float a=2*Mathf.PI*i/sides,b=2*Mathf.PI*(i+1)/sides;
                Vector3 ta=new Vector3(Mathf.Cos(a)*topRadius,height*.5f,Mathf.Sin(a)*topRadius),tb=new Vector3(Mathf.Cos(b)*topRadius,height*.5f,Mathf.Sin(b)*topRadius);
                Vector3 ba=new Vector3(Mathf.Cos(a)*bottomRadius,-height*.5f,Mathf.Sin(a)*bottomRadius),bb=new Vector3(Mathf.Cos(b)*bottomRadius,-height*.5f,Mathf.Sin(b)*bottomRadius);
                m.Tri(Vector3.up*height*.5f,tb,ta);m.Tri(Vector3.down*height*.5f,ba,bb);m.Quad(ta,tb,bb,ba);
            }
            return m.Save(name);
        }
        private static Mesh SphereMesh(string name,int sectors,int rings)
        {
            var m=new MeshData();
            Vector3 V(int i,int j){float a=i*2*Mathf.PI/sectors,b=j*Mathf.PI/rings;return new Vector3(Mathf.Cos(a)*Mathf.Sin(b),Mathf.Cos(b),Mathf.Sin(a)*Mathf.Sin(b))*.5f;}
            for(int j=0;j<rings;j++)for(int i=0;i<sectors;i++){if(j>0)m.Tri(V(i,j),V(i+1,j),V(i,j+1));if(j<rings-1)m.Tri(V(i+1,j),V(i+1,j+1),V(i,j+1));}
            return m.Save(name);
        }
        private sealed class MeshData
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            public void Tri(Vector3 a,Vector3 b,Vector3 c){int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(start);indices.Add(start+1);indices.Add(start+2);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Tri(a,b,c);Tri(a,c,d);}
            public Mesh Save(string name){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();string path=Art+"/"+name+".asset";AssetDatabase.CreateAsset(mesh,path);CreatedAssets.Add(path);return mesh;}
        }
        private static void EnsureFolder(string path)
        {string parent=Path.GetDirectoryName(path).Replace('\\','/');if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        private static SkywellController SavedCourt()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=SkywellBuilder.ScenePath||scene.isDirty)throw new InvalidOperationException("Open the saved, clean L5 author scene outside Play Mode.");
            var courts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkywellController>(true)).ToArray();
            if(courts.Length!=1)throw new InvalidOperationException("Expected one L5 controller.");return courts[0];
        }
        private static Transform Required(Transform parent,string path)=>parent.Find(path)?parent.Find(path):throw new InvalidOperationException("Missing authored object: "+path);
        private static List<Action> CaptureUnchanged(SkywellController court)
        {
            var checks=new List<Action>();
            foreach(var c in court.GetComponentsInChildren<Component>(true))
            {
                if(!c||c==court)continue;
                if(c is Transform t)
                {
                    var parent=t.parent;var position=t.localPosition;var rotation=t.localRotation;var scale=t.localScale;
                    checks.Add(()=>{if(!t||t.parent!=parent||t.localPosition!=position||t.localRotation!=rotation||t.localScale!=scale)throw new InvalidOperationException("Unrelated transform changed: "+(t?t.name:"deleted"));});
                }
                else
                {
                    string json=EditorJsonUtility.ToJson(c);
                    checks.Add(()=>{if(!c||EditorJsonUtility.ToJson(c)!=json)throw new InvalidOperationException("Unrelated component changed: "+(c?c.name:"deleted"));});
                }
            }
            return checks;
        }
        private static string Backup(string path)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backups",DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmssfff")+"_before_deer_grove_scene"));
            Directory.CreateDirectory(folder);string source=Path.GetFullPath(Path.Combine(Application.dataPath,"..",path));
            File.Copy(source,Path.Combine(folder,"05_Skywell.unity"));File.Copy(source+".meta",Path.Combine(folder,"05_Skywell.unity.meta"));return folder;
        }
    }
}
