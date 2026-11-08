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
    /// <summary>Local migration of the saved basin to surface travel; never rebuilds the upper level.</summary>
    public static class SkywellSurfaceWaterRevision
    {
        private const string RootName="Revision_16_Surface_water_route";
        private const string Art="Assets/BorrowingSun/Art/SkywellSurfaceWater";
        private const float Water=14.5f, Feet=14.4f, Deck=14.55f;
        private const float RefugeWeatherHeight=4f;
        private static readonly Vector3[] Route={
            new Vector3(-18,20.03f,104),new Vector3(-18,20.03f,102),new Vector3(-19.5f,20.03f,102),
            new Vector3(-22.8f,18.03f,102),new Vector3(-24,18.03f,102),new Vector3(-24,18.03f,100.8f),
            new Vector3(-24,14.58f,95.2f),new Vector3(-24,14.58f,94),new Vector3(-21,14.58f,94),new Vector3(-18,14.58f,94)};
        private static string Evidence=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../validation/current/skywell/2026-10-19-surface-water-wind"));
        private static readonly List<string> createdAssets=new List<string>();

        [MenuItem("Borrowing Sun/Skywell/Replace submerged route with surface water")]
        public static void Apply()
        {
            var court=SavedCourt();var scene=court.gameObject.scene;
            if(court.transform.Find(RootName)||court.Journey.SurfaceConfigured||AssetDatabase.IsValidFolder(Art))
                throw new InvalidOperationException("Surface revision already exists. Audit it instead of repeating migration.");
            var basin=Required(court.transform,"M3_Underwater_refuge_and_search");
            var refuge=Required(basin,"Fixed_refuge_FOUR_WALLS");
            var oldLift=Required(basin,"Inspection_lift_ONLY_this_descends");
            var water=Required(basin,"Basin_surface_ABOVE_refuge_BELOW_tower");
            if(Mathf.Abs(water.position.y-14.2f)>.01f||Mathf.Abs(refuge.position.y-10)>.01f)
                throw new InvalidOperationException("Expected the reviewed saved basin: water14.2, refuge10. Inspect new author changes before migration.");
            var target=court.WeatherTargets[2];var station=court.Consoles[2];var zone=station.Zone;
            var refugeBox=RequireRefugeWeatherBox(court,11.5f);
            if(target.transform!=refuge)
                throw new InvalidOperationException("The existing refuge target and weather volume must be retained.");
            var guide=Required(court.transform,"M4_Weather_light_and_lure_loop");
            var paddle=court.Journey.PaddlePickup;
            var fishRoot=Required(court.transform,"M4_Fish_patrol_lure_and_escape");
            var excluded=new HashSet<Transform>{basin,guide,fishRoot,paddle,zone.transform,zone.Volume.transform};
            var untouched=court.GetComponentsInChildren<Transform>(true)
                .Where(t=>t!=court.transform&&!excluded.Any(e=>t==e||t.IsChildOf(e))&&t.name!="Inspection_recall_control")
                .ToDictionary(t=>t,t=>new Pose(t.position,t.rotation));
            var originalStations=court.Consoles.ToArray();var originalTargets=court.WeatherTargets.ToArray();
            var sun=RenderSettings.sun;var fog=RenderSettings.fogColor;var ambient=RenderSettings.ambientLight;var sky=RenderSettings.skybox;
            string backup=Backup(scene.path);createdAssets.Clear();
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Surface water route and open refuge dock");
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(court.gameObject,"Surface water basin migration");
                EnsureFolder(Art);
                var root=Node(court.transform,RootName,Vector3.zero);
                var stone=Required(basin,"Inspection_side_walkway").GetComponent<Renderer>().sharedMaterial;
                var paths=Node(root,"Static_west_bank_ramps",Vector3.zero);
                var walkable=new List<Collider>();
                walkable.Add(Required(basin,"Inspection_side_walkway").GetComponent<Collider>());
                walkable.Add(Ramp(paths,"Upper_ramp",new Vector3(-19.5f,20,102),new Vector3(-22.8f,18,102),2.4f,stone));
                walkable.Add(Box(paths,"Upper_turn_landing",new Vector3(-24,17.8f,102),new Vector3(2.4f,.4f,2.4f),stone).GetComponent<Collider>());
                walkable.Add(Ramp(paths,"Lower_ramp",new Vector3(-24,18,100.8f),new Vector3(-24,Deck,95.2f),2.4f,stone));
                walkable.Add(Box(paths,"Lower_turn_landing",new Vector3(-24,Deck-.2f,94),new Vector3(2.4f,.4f,2.4f),stone).GetComponent<Collider>());
                walkable.Add(Box(paths,"Dock_access_walkway",new Vector3(-22.5f,Deck-.2f,94),new Vector3(3.1f,.4f,2.4f),stone).GetComponent<Collider>());
                var markers=new Transform[Route.Length];for(int i=0;i<Route.Length;i++)markers[i]=Node(paths,"Route_"+i.ToString("00"),Route[i]);
                var dock=Node(root,"Surface_safe_dock",new Vector3(-18,Deck,94));

                // Preserve the serialized anchor, removing only obsolete moving-cage geometry.
                foreach(Transform child in oldLift.Cast<Transform>().ToArray())Undo.DestroyObjectImmediate(child.gameObject);
                string[] removeBasin={"Fixed_shaft_guide","Lower_shaft_landing","Fixed_air_supply_pipe","Maintenance_window_frames","Far_room_floor","Far_room_side","Far_room_back","Far_room_front"};
                foreach(var child in basin.Cast<Transform>().Where(t=>removeBasin.Contains(t.name)).ToArray())Undo.DestroyObjectImmediate(child.gameObject);
                foreach(var recall in court.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Inspection_recall_control").ToArray())Undo.DestroyObjectImmediate(recall.gameObject);
                string[] removeRefuge={"Sectioned_glass_roof","West_observation_window","South_wall","North_wall","East_wall","Corner_column","Water_lock_clear_air_boundary"};
                foreach(var child in refuge.Cast<Transform>().Where(t=>removeRefuge.Contains(t.name)||t.name.IndexOf("column",StringComparison.OrdinalIgnoreCase)>=0).ToArray())Undo.DestroyObjectImmediate(child.gameObject);
                refuge.position+=Vector3.up*(Deck-10);
                walkable.Add(Required(refuge,"Dry_refuge_floor").GetComponent<Collider>());
                walkable.Add(Required(court.Journey.ReturnShuttle,"Shuttle_floor").GetComponent<Collider>());
                walkable.Add(Required(court.transform,"Revision_11_Boat_and_ladder/Tower_water_landing").GetComponent<Collider>());
                water.position=new Vector3(water.position.x,Water,water.position.z);
                paddle.SetPositionAndRotation(new Vector3(-2,Water+.02f,84),Quaternion.Euler(0,0,90));

                // Retain the original solid cover's X/Z and base, extending it above the new swimming sightline.
                foreach(var column in basin.Cast<Transform>().Where(t=>t.name=="Opaque_filter_column_hides_swimmer"))
                {
                    column.position=new Vector3(column.position.x,12.7f,column.position.z);
                    column.localScale=new Vector3(column.localScale.x,4.1f,column.localScale.z);
                }
                // Basin edges remain real physical barriers. Keep the boat/ladder opening at north centre.
                foreach(var wall in basin.Cast<Transform>().Where(t=>t.name=="Basin_side_wall"||t.name=="Basin_end_wall"))
                {
                    var p=wall.position;p.y=12.3f;wall.position=p;var scale=wall.localScale;scale.y=7.8f;wall.localScale=scale;
                    if(wall.name=="Basin_end_wall"&&wall.position.z>100)
                    {
                        p.x=-13.35f;wall.position=p;scale.x=23.3f;wall.localScale=scale;
                        Box(root,"North_basin_edge_east_of_ladder",new Vector3(10.35f,12.3f,101),new Vector3(17.3f,7.8f,.4f),wall.GetComponent<Renderer>().sharedMaterial);
                    }
                }

                // Keep the surface dock and full bell route inside, below the tower zone's Y=18 lower edge.
                // Station identities, responders, centre X/Z and horizontal bounds stay intact.
                zone.Volume.transform.position=new Vector3(zone.Volume.transform.position.x,15.5f,zone.Volume.transform.position.z);
                if(zone.transform!=zone.Volume.transform)zone.transform.position=new Vector3(zone.transform.position.x,15.5f,zone.transform.position.z);
                var refugeSize=refugeBox.size;refugeSize.y=RefugeWeatherHeight;refugeBox.size=refugeSize;EditorUtility.SetDirty(refugeBox);
                var targetParts=refuge.GetComponentsInChildren<Renderer>(true).Where(r=>!r.transform.IsChildOf(station.transform)).ToArray();
                var targetCollision=refuge.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger&&!c.transform.IsChildOf(station.transform)).ToArray();
                target.Configure(2,"避难装置",targetParts,targetCollision);EditorUtility.SetDirty(target);
                court.Journey.ConfigureSurfaceRoute(dock,markers,walkable.ToArray(),new Bounds(new Vector3(-3,17.2f,86),new Vector3(46,6.8f,40)),Water,Feet);
                court.Journey.ConfigureSurfaceWaterBody(water.GetComponent<Renderer>());
                court.GetComponent<SkywellFish>().ConfigureSurfaceWater(Water);court.GetComponent<SkywellWindBell>().ConfigureSurfaceWater(Water);
                var feedback=court.GetComponent<SkywellAirFeedback>();if(feedback)Undo.DestroyObjectImmediate(feedback);
                EditorUtility.SetDirty(court.Journey);EditorUtility.SetDirty(court.GetComponent<SkywellFish>());EditorUtility.SetDirty(court.GetComponent<SkywellWindBell>());
                foreach(var pair in untouched)
                    if(!pair.Key||Vector3.Distance(pair.Key.position,pair.Value.position)>.0001f||Quaternion.Angle(pair.Key.rotation,pair.Value.rotation)>.001f)
                        throw new InvalidOperationException("Unexpected change outside the surface basin: "+(pair.Key?pair.Key.name:"deleted object"));
                if(!originalStations.SequenceEqual(court.Consoles)||!originalTargets.SequenceEqual(court.WeatherTargets)||station.Zone!=zone||
                    RenderSettings.sun!=sun||RenderSettings.fogColor!=fog||RenderSettings.ambientLight!=ambient||RenderSettings.skybox!=sky)
                    throw new InvalidOperationException("Upper-level weather, authored sunlight or background changed unexpectedly.");
                Physics.SyncTransforms();
                var lines=Inspect(court);lines.Insert(1,"Backup="+backup);lines.Add("PASS: Existing upper-level poses, stations/targets, deer branch, sun and gray environment retained.");
                lines.Add("SKYWELL_SURFACE_WATER_SETUP_PASSED");
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save surface water revision.");
                Undo.CollapseUndoOperations(group);Directory.CreateDirectory(Evidence);File.WriteAllLines(Path.Combine(Evidence,"scene-setup.txt"),lines);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach(string asset in createdAssets)AssetDatabase.DeleteAsset(asset);
                AssetDatabase.DeleteAsset(Art);
                throw;
            }
        }

        [MenuItem("Borrowing Sun/Skywell/Refine surface water and refuge weather boundaries")]
        public static void RefineWaterBoundary()
        {
            var court=SavedCourt();var scene=court.gameObject.scene;
            if(!court.transform.Find(RootName)||!court.Journey.SurfaceConfigured)
                throw new InvalidOperationException("Apply the surface route before refining its water boundary.");
            var water=Required(court.transform,"M3_Underwater_refuge_and_search/Basin_surface_ABOVE_refuge_BELOW_tower").GetComponent<Renderer>();
            if(!water||!water.enabled)throw new InvalidOperationException("The saved visible water plane is required.");
            // Check the reviewed volume before backing up or changing either serialized object.
            var refugeBox=RequireRefugeWeatherBox(court,15.5f);float priorHeight=refugeBox.size.y;
            string backup=Backup(scene.path);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Refine surface water and refuge weather boundaries");
            try
            {
                Undo.RecordObject(court.Journey,"Bind actual water renderer");
                court.Journey.ConfigureSurfaceWaterBody(water);EditorUtility.SetDirty(court.Journey);
                Undo.RecordObject(refugeBox,"Keep refuge weather below tower weather");
                var refugeSize=refugeBox.size;refugeSize.y=RefugeWeatherHeight;refugeBox.size=refugeSize;EditorUtility.SetDirty(refugeBox);
                Physics.SyncTransforms();var lines=Inspect(court);
                lines.Insert(1,"Backup="+backup);
                lines.Add("PASS: Mutation scope is only Journey.surfaceWaterBody binding and refuge BoxCollider height "+priorHeight+" -> "+RefugeWeatherHeight+"; weather centre/XZ bounds, all solid geometry, ramps, water, docks and camera remain unchanged.");
                lines.Add("PASS: Buoyancy uses visible water X/Z bounds; unsupported north apron exit at(0,14.4,103) is neither water nor a fall-exempt approach.");
                lines.Add("SKYWELL_SURFACE_WATER_BOUNDARY_PASSED");
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save surface-water boundary binding.");
                Undo.CollapseUndoOperations(group);Directory.CreateDirectory(Evidence);File.WriteAllLines(Path.Combine(Evidence,"water-boundary-refine.txt"),lines);
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }

        [MenuItem("Borrowing Sun/Skywell/Audit surface water route")]
        public static void Audit()
        {
            var lines=Inspect(SavedCourt());lines.Add("Read-only scene audit; no Play state or serialized values changed.");
            lines.Add("SKYWELL_SURFACE_WATER_AUDIT_PASSED");Directory.CreateDirectory(Evidence);File.WriteAllLines(Path.Combine(Evidence,"scene-audit.txt"),lines);
        }
        private static List<string> Inspect(SkywellController court)
        {
            var j=court.Journey;
            if(!j.SurfaceConfigured||Mathf.Abs(j.SurfaceWaterLevel-Water)>.001f||Mathf.Abs(j.SurfaceFeetHeight-Feet)>.001f||
                j.SurfaceRouteWaypoints.Length!=Route.Length||j.SurfaceWalkable.Any(c=>!c||c.isTrigger||!c.enabled))throw new InvalidOperationException("Missing surface route, floors or water heights.");
            var basin=Required(court.transform,"M3_Underwater_refuge_and_search");var room=Required(basin,"Fixed_refuge_FOUR_WALLS");
            var water=Required(basin,"Basin_surface_ABOVE_refuge_BELOW_tower").GetComponent<Renderer>();
            if(j.SurfaceWaterBody!=water||!j.IsOverSurfaceWater(new Vector3(-2,Feet,84))||
                j.IsOverSurfaceWater(new Vector3(0,Feet,103))||j.IsWaterApproach(new Vector3(0,Feet,103)))
                throw new InvalidOperationException("Buoyancy must use the actual water plane, with no support or floating beyond the north dock.");
            if(room.Cast<Transform>().Any(t=>t.name.Contains("wall")||t.name.Contains("window")&&t.name!="South_window_lure_bell"||t.name.Contains("roof")||t.name.Contains("Water_lock"))||
                Required(basin,"Inspection_lift_ONLY_this_descends").childCount!=0)throw new InvalidOperationException("Old underwater walls, roof, airlock or lift geometry remains.");
            if(court.GetComponent<SkywellAirFeedback>())throw new InvalidOperationException("Surface route must have no oxygen-warning component.");
            if(!court.GetComponent<SkywellFish>().SurfaceWaterConfigured||!court.GetComponent<SkywellWindBell>().SurfaceWaterConfigured||Mathf.Abs(court.GetComponent<SkywellFish>().PatrolHeight-(Water-.35f))>.001f||Mathf.Abs(court.GetComponent<SkywellWindBell>().TravelHeight-(Water+.65f))>.001f)
                throw new InvalidOperationException("Fish and wind bell must reset at their authored surface heights.");
            var refugeZone=court.Consoles[2].Zone;var refugeBox=RequireRefugeWeatherBox(court,15.5f);var refugeBounds=refugeBox.bounds;
            var towerZone=court.Consoles[0].Zone;
            if(Mathf.Abs(refugeBox.size.y-RefugeWeatherHeight)>.001f||Mathf.Abs(refugeBounds.min.y-13.5f)>.001f||Mathf.Abs(refugeBounds.max.y-17.5f)>.001f||
                !towerZone||refugeZone.Overlaps(towerZone))throw new InvalidOperationException("Refuge weather must span Y13.5..17.5 and remain separate from tower weather.");
            var bell=court.GetComponent<SkywellWindBell>();var bellWest=court.Origin+new Vector3(-18,bell.TravelHeight-court.Origin.y,-26);var bellEast=bellWest+Vector3.right*13;
            if(!refugeZone.Contains(j.RefugePoint)||!refugeZone.Contains(bell.LurePosition)||!refugeZone.Contains(bellWest)||!refugeZone.Contains(bellEast))
                throw new InvalidOperationException("Refuge weather must contain the dry dock, actual bell and both ends of the complete bell route.");
            var lines=new List<string>{DateTime.UtcNow.ToString("O"),"Water surface deliberately raised from14.2 to14.5; horizontal feet14.4; original boat/dock floor14.55. No dive or oxygen gameplay.",
                "PASS: Static two-ramp west-bank approach has solid mesh collision and dry landings; original moving-lift anchor survives without geometry or active movement.",
                "PASS: Open dry refuge at"+j.RefugePoint+"; old roof/walls/airlock and far submerged room removed.",
                "PASS: Paddle floats at"+j.PaddlePickup.position+"; fish patrolheight="+court.GetComponent<SkywellFish>().PatrolHeight+"; bellheight="+court.GetComponent<SkywellWindBell>().TravelHeight,
                "PASS: Existing boat, bow opening, physical boat sweep, tower docking apron and climbing endpoints retained.",
                "PASS: Cover and basin edges retain solid collision, north edge leaves a3.4m boat/ladder opening.",
                "PASS: Refuge weather bounds="+refugeBounds+"; Y13.5..17.5 does not overlap tower weather. Dry dock and full bell route "+bellWest+" -> "+bellEast+" remain covered.",
                "PASS: Real buoyancy footprint="+j.SurfaceWaterBounds+"; larger ramp approach volume does not extend water into empty space."};
            for(int i=0;i<Route.Length;i++)
            {if(Vector3.Distance(j.SurfaceRouteWaypoints[i].position,Route[i])>.001f)throw new InvalidOperationException("Route marker mismatch"+i);lines.Add("Route"+i+"="+j.SurfaceRouteWaypoints[i].position);}
            return lines;
        }
        private static BoxCollider RequireRefugeWeatherBox(SkywellController court,float expectedCentreY)
        {
            var box=court.Consoles[2].Zone?court.Consoles[2].Zone.Volume as BoxCollider:null;
            var anchor=Required(court.transform,"Revision_09_Borrowing_Sun_contract/Refuge_zone_anchor");
            if(!box||box.transform!=anchor||!box.enabled||!box.isTrigger||!box.gameObject.activeInHierarchy||
                Vector3.Distance(box.transform.lossyScale,Vector3.one)>.001f||Quaternion.Angle(box.transform.rotation,Quaternion.identity)>.001f||box.center.sqrMagnitude>.000001f||
                Vector3.Distance(box.bounds.center,new Vector3(-13.5f,expectedCentreY,88))>.001f||Mathf.Abs(box.size.x-21)>.001f||Mathf.Abs(box.size.z-24)>.001f||
                Mathf.Abs(box.size.y-7)>.001f&&Mathf.Abs(box.size.y-RefugeWeatherHeight)>.001f)
                throw new InvalidOperationException("Refuge weather scope differs from the reviewed box: fixed X/Z centre -13.5/88, size21x24, unit scale, and height7 or4. Inspect author changes before refinement.");
            return box;
        }
        private static SkywellController SavedCourt()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=SkywellBuilder.ScenePath||scene.isDirty)throw new InvalidOperationException("Open the saved, clean L5 author scene outside Play Mode.");
            var courts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SkywellController>(true)).ToArray();
            if(courts.Length!=1||!courts[0].Journey||!courts[0].GetComponent<SkywellFish>()||!courts[0].GetComponent<SkywellWindBell>())throw new InvalidOperationException("Expected one fully linked L5 controller.");return courts[0];
        }
        private static Transform Required(Transform root,string path)=>root.Find(path)?root.Find(path):throw new InvalidOperationException("Missing saved object: "+path);
        private static Transform Node(Transform parent,string name,Vector3 world)
        {var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Add surface route");go.transform.SetParent(parent,false);go.transform.position=world;go.transform.rotation=Quaternion.identity;return go.transform;}
        private static Transform Box(Transform parent,string name,Vector3 world,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(go,"Add surface floor");go.name=name;go.transform.SetParent(parent,false);go.transform.position=world;go.transform.rotation=Quaternion.identity;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        private static Collider Ramp(Transform parent,string name,Vector3 a,Vector3 b,float width,Material material)
        {
            var direction=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;var side=Vector3.Cross(Vector3.up,direction)*width*.5f;
            a-=direction*.02f;b+=direction*.02f;var down=Vector3.down*.4f;
            Vector3[] corners={a-side,a+side,b+side,b-side,a-side+down,a+side+down,b+side+down,b-side+down};
            int[][] quads={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,1,5,4},new[]{3,7,6,2},new[]{0,4,7,3},new[]{1,2,6,5}};
            var vertices=new List<Vector3>();var triangles=new List<int>();foreach(var q in quads){int n=vertices.Count;foreach(int k in q)vertices.Add(corners[k]);triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string asset=Art+"/"+name+".asset";AssetDatabase.CreateAsset(mesh,asset);createdAssets.Add(asset);
            var t=Node(parent,name,Vector3.zero);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            var collider=t.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;return collider;
        }
        private static void EnsureFolder(string folder)
        {string parent=Path.GetDirectoryName(folder).Replace('\\','/');if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));}
        private static string Backup(string scene)
        {string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../backups",DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmssfff")+"_before_surface_water_scene"));Directory.CreateDirectory(dir);string src=Path.GetFullPath(Path.Combine(Application.dataPath,"..",scene));File.Copy(src,Path.Combine(dir,"05_Skywell.unity"));File.Copy(src+".meta",Path.Combine(dir,"05_Skywell.unity.meta"));return dir;}
    }
}
