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
    /// <summary>New authored revision; never overwrites the old two-courtyard scene.</summary>
    public static class RainCargoBuilder
    {
        public const string ScenePath="Assets/BorrowingSun/Scenes/03_RainCargoStreet.unity";
        private static readonly Vector3 O=new Vector3(-6,0,66);
        private static Transform court,scenery;
        private static Material stone,chalk,wood,teal,copper,water,leaf,roof;
        private static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        [MenuItem("Borrowing Sun/Rain Cargo/1 Create revised street")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before creating the revision.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("The revision already exists. Open it and make targeted edits; regeneration is disabled.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            stone=Mat("Stone");chalk=Mat("Chalk");wood=Mat("Cinnabar");teal=Mat("WeatherCircuit");copper=Mat("Copper");water=Mat("Water");leaf=Mat("Sapling");roof=Mat("Roof");
            court=Group("01_Walkable_street_and_terraces");scenery=Group("10_Mountain_and_residential_layers");
            // West work yard, elevated receiving eave, lower rain-shadow street, eastern freight dock.
            Floor("West_freight_court",-24,-10,-9,9,15);
            Floor("Middle_turning_court",-2,8,-6,.1f,15);
            Floor("Middle_south_passing_strip",-2,8,-3,0,15);
            Ramp("Covered_freight_descent",new Vector3(4,15,0),new Vector3(4,12,18),5.2f);
            Floor("Rain_shadow_street",1,24,15,22,12);
            Floor("East_receiving_terrace",32,41,14,35,15);
            Ramp("West_fixed_stair",new Vector3(-22,15,-5),new Vector3(-10,18,-5),3.4f);
            Floor("West_high_landing",-10.2f,-8.2f,-6.9f,-2.6f,18);
            Floor("A_high_to_upper_street",-3.5f,.5f,-9,-2.6f,18);
            Floor("Upper_cistern_eaves",-4,9,-23,-9,18);
            Floor("Upper_shortcut_landing",8,14,-24,-21.4f,18);
            Ramp("Shortcut_return_stair",new Vector3(12,15,-12),new Vector3(12,18,-22),3.4f);
            Floor("Shortcut_lower_vertical_link",10.3f,13.7f,-12,-4,15);
            Floor("Shortcut_lower_cross_link",4,14,-7,-4,15);
            // Separate maintenance route allows a missed B ride to be recovered without a reset.
            Floor("B_low_side_link",19,22,8,15,12);
            Floor("B_low_maintenance_gallery",19,45,7.3f,10.7f,12);
            Ramp("B_fixed_recovery_stair",new Vector3(43.5f,12,9),new Vector3(43.5f,15,21),3.0f);
            Floor("B_high_recovery_landing",40,45,20.8f,24.5f,15);
            // Lift A stays at the old 3 m height range, with a generous 5.2 m deck.
            var a=Group("02_A_receiver_lift",new Vector3(-6,15,0));
            Box("A_deck",new Vector3(-6,14.85f,0),new Vector3(8,.3f,5.2f),chalk,a);
            var wa=Box("Water_A",new Vector3(-6,14.68f,0),new Vector3(8.8f,.08f,6.1f),water,court,false).transform;
            Box("A_pool_bed",new Vector3(-6,11.1f,0),new Vector3(9.2f,.4f,6.4f),roof,court);
            foreach(float x in new[]{-10.35f,-1.65f})foreach(float z in new[]{-2.95f,2.95f})
            {Box("A_lift_guide",new Vector3(x,16,z),new Vector3(.16f,8,.16f),wood,court);for(int y=15;y<=18;y++)Box("A_height_tick",new Vector3(x,y,z-.12f),new Vector3(.42f,.07f,.08f),teal,court,false);}
            Fence(new Vector3(-10,15,2.6f),new Vector3(-2,15,2.6f),a);
            // B is a freight elevator with a small moving counterweight, not an implausibly large flooded basin.
            var b=Group("03_B_counterweight_lift",new Vector3(28,12,18));
            Box("B_deck",new Vector3(28,11.85f,18),new Vector3(8,.3f,5.6f),chalk,b);
            var weight=Group("B_counterweight",new Vector3(28,12,13.2f),court);
            TankShell("Counterweight",new Vector3(28,12,13.2f),weight);
            var bw=Box("Stored_counterweight_water",new Vector3(28,12.13f,13.2f),new Vector3(1.26f,.015f,1.05f),water,weight,false).transform;
            foreach(float x in new[]{24,32})Box("Freight_gantry",new Vector3(x,17.5f,22.8f),new Vector3(.32f,12,.32f),wood,court);
            Box("Gantry_top_beam",new Vector3(28,23.5f,22.8f),new Vector3(9,.35f,.4f),wood,court);
            Beam("Pulley_crossbar",new Vector3(28,23.5f,13.2f),new Vector3(28,23.5f,22.8f),.2f,.2f,wood,court,false);
            Beam("Weight_cable",new Vector3(28,23.5f,13.2f),new Vector3(28,13.1f,13.2f),.05f,.05f,copper,court,false);
            Beam("Platform_cable",new Vector3(28,23.5f,15.3f),new Vector3(28,12.2f,15.3f),.05f,.05f,copper,court,false);
            Fence(new Vector3(24,12,20.8f),new Vector3(32,12,20.8f),b);
            var s0=Station("S0_raise_lift",new Vector3(-19,15,5));
            var s1=Station("S1_collect_at_eaves",new Vector3(4,18,-17));
            var va=Lever("A_drain_control",new Vector3(6,15,-4));
            var vb=Lever("B_drain_control",new Vector3(22,12,21));
            var shortcut=Lever("Upper_shortcut_latch",new Vector3(7,18,-20));
            var gate=Box("Shortcut_gate_opens_from_above",new Vector3(12,16.1f,-12),new Vector3(3.3f,2.2f,.22f),wood,court).transform;
            var source0=Group("Local_rain_spout",new Vector3(-8,21,0),court);
            var source1=Group("High_eaves_spout",new Vector3(-6.72f,23,0),court);
            Box("Eaves_rain_catcher",new Vector3(-6.72f,22.92f,0),new Vector3(1.7f,.15f,1.5f),teal,court,false);
            Beam("Roof_weather_conduit",new Vector3(4,22,-16.3f),new Vector3(-6.72f,23,0),.22f,.22f,teal,court,false);
            Beam("S0_supply",new Vector3(-19,15.3f,5.6f),new Vector3(-10.5f,15.3f,5.6f),.15f,.15f,teal,court,false);
            Beam("S0_supply_riser",new Vector3(-10.5f,15.3f,5.6f),new Vector3(-8,21,0),.15f,.15f,teal,court,false);
            var rain=Box("Active_rain_stream",new Vector3(-8,18,0),new Vector3(.13f,6,.13f),teal,court,false).transform;
            var unload=Box("Tank_to_weight_hose",new Vector3(28,14,20),new Vector3(.13f,2,.13f),teal,court,false).transform;
            rain.gameObject.SetActive(false);unload.gameObject.SetActive(false);
            var cart=Cart(out Transform tw);
            Vector3[] positions={new Vector3(-16,15,0),new Vector3(-6,15,0),new Vector3(4,15,0),new Vector3(4,12,18),new Vector3(20,12,18),new Vector3(28,12,18),new Vector3(36,15,18),new Vector3(36,15,30)};
            var stops=new Transform[8];
            for(int i=0;i<8;i++)
            {
                Transform parent=i==1?a:i==5?b:court;stops[i]=Group("Freight_stop_"+i,positions[i],parent);
                Box("Cargo_dock_"+i,positions[i]+Vector3.up*.018f,new Vector3(3.4f,.025f,3.4f),i==7?teal:copper,parent,false);
                // Gold guide rails trace each actual transport segment. Dynamic previews add availability, not the solution.
                if(i>0 && i!=1 && i!=2 && i!=5 && i!=6)Track(positions[i-1],positions[i],court);
            }
            Track(new Vector3(-24,15,0),new Vector3(-10,15,0),court);Track(new Vector3(-10,15,0),new Vector3(-2,15,0),a);
            Track(new Vector3(-2,15,0),new Vector3(4,15,0),court);Track(new Vector3(24,12,18),new Vector3(32,12,18),b);
            Track(new Vector3(20,12,18),new Vector3(24,12,18),court);Track(new Vector3(32,15,18),new Vector3(36,15,18),court);
            // Cliff edges have visible rails; lift mouths intentionally stay open and use player edge protection.
            Fence(new Vector3(-24,15,9),new Vector3(-10,15,9),court);
            Fence(new Vector3(-24,15,-9),new Vector3(-10,15,-9),court);
            Fence(new Vector3(-4,18,-23),new Vector3(8,18,-23),court);
            Fence(new Vector3(-4,18,-23),new Vector3(-4,18,-10),court);
            Fence(new Vector3(1,12,22),new Vector3(23,12,22),court);
            Fence(new Vector3(32,15,35),new Vector3(41,15,35),court);
            Fence(new Vector3(19,12,7.3f),new Vector3(45,12,7.3f),court);
            Fence(new Vector3(41,15,25),new Vector3(41,15,35),court);
            // Layered buildings cling to mountain masses; only the marked freight tunnel roof cuts away during play.
            var roofs=new List<Renderer>();
            Rock("West_bedrock",new Vector3(-24,10,14),new Vector3(18,10,17),10);
            Rock("Upper_eaves_bedrock",new Vector3(-1,10,-18),new Vector3(15,15,16),-7);
            Rock("Tunnel_back_ridge",new Vector3(12,17.5f,28),new Vector3(18,16,13),7);
            Rock("East_ridge",new Vector3(39,10,40),new Vector3(19,12,14),-9);
            Rock("High_city_spine",new Vector3(23,28,44),new Vector3(26,24,15),5);
            var tunnelRoof=Box("Cutaway_rock_tunnel_roof",new Vector3(12,17.2f,18.5f),new Vector3(12,2.6f,7.8f),roof,scenery,false);
            roofs.Add(tunnelRoof.GetComponent<Renderer>());
            for(int i=0;i<3;i++)
            {
                float x=7+i*5;
                Box("Tunnel_rear_pier",new Vector3(x,14.2f,21.65f),new Vector3(.5f,4.4f,.5f),stone,court);
                Beam("Tunnel_arch_rib",new Vector3(x,16.1f,15),new Vector3(x,16.1f,22),.28f,.28f,wood,court,false);
                Box("Rain_shadow_light",new Vector3(x,15.2f,21.2f),new Vector3(.6f,.12f,.12f),teal,court,false);
            }
            House("Western_workshop",new Vector3(-20,15,11),new Vector3(6,4,5));
            House("Upper_house_in_rock",new Vector3(-24,20,19),new Vector3(5,4,5));
            House("Rain_eaves_workshop",new Vector3(6,18,-26),new Vector3(6,4,5));
            House("Above_tunnel_residence",new Vector3(12,25.5f,29),new Vector3(6,4,5));
            House("Cliff_window_house",new Vector3(25,28,37),new Vector3(6,4,5));
            House("Receiving_warehouse",new Vector3(37,16,39),new Vector3(7,4,6));
            House("Far_upper_weather_hall",new Vector3(21,40,45),new Vector3(9,5,7));
            Beam("Future_district_connection_silhouette",new Vector3(5,30,33),new Vector3(34,30,33),3,.4f,stone,scenery,false);
            // Tall destination mast remains a readable landmark above the intervening rock.
            Box("Destination_mast",new Vector3(36,22,31.5f),new Vector3(.25f,14,.25f),wood,court);
            Box("Destination_flag",new Vector3(37.2f,27,31.5f),new Vector3(2.5f,1.6f,.12f),teal,court,false);
            var actor=new GameObject("08_Apprentice");var cc=actor.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=new Vector3(0,.9f,0);cc.stepOffset=.24f;cc.slopeLimit=48;cc.skinWidth=.03f;
            Vector3 spawn=new Vector3(-20,15.06f,-2.5f);actor.transform.position=O+spawn;
            Prim("Apprentice",PrimitiveType.Capsule,spawn+Vector3.up*.9f,new Vector3(.56f,.9f,.56f),copper,actor.transform,false);
            var player=actor.AddComponent<CourtPlayer>();
            var card=Box("Unique_rain_card",spawn+Vector3.up*1.7f,new Vector3(.27f,.38f,.09f),teal,null,false).transform;
            var camObj=new GameObject("Camera_Main");var cam=camObj.AddComponent<Camera>();cam.tag="MainCamera";cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.32f,.43f,.45f);camObj.AddComponent<AudioListener>();camObj.AddComponent<UniversalAdditionalCameraData>();
            var rig=camObj.AddComponent<CourtCamera>();rig.Initialize(null,player,cam);
            rig.ConfigureViews(O+new Vector3(54,66,-40),O+new Vector3(9,16,5),36,O+new Vector3(7,25,-8),O+new Vector3(-6,19,0));
            rig.ConfigureFollowView(player,10.5f);player.ConfigurePuzzleEdgeProtection();
            var runtime=new GameObject("09_RainCargo_runtime");var controller=runtime.AddComponent<RainCargoController>();
            var lineObj=new GameObject("Freight_next_destination_preview");var line=lineObj.AddComponent<LineRenderer>();line.positionCount=2;line.useWorldSpace=true;line.widthMultiplier=.14f;
            line.sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));line.sharedMaterial.color=Color.white;
            // Persist preview material to avoid a scene-only transient material reference.
            string previewMat="Assets/BorrowingSun/Materials/RainCargoRoute.mat";if(!File.Exists(previewMat))AssetDatabase.CreateAsset(line.sharedMaterial,previewMat);else line.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(previewMat);
            controller.Initialize(new RainCargoBindings{offset=O,aLift=a,bLift=b,cart=cart,waterA=wa,tankWater=tw,counterweight=weight,counterweightWater=bw,
                station0=s0,station1=s1,valveA=va,valveB=vb,shortcutLever=shortcut,shortcutGate=gate,card=card,source0=source0,source1=source1,rainBeam=rain,unloadBeam=unload,weightCable=court.Find("Weight_cable"),platformCable=court.Find("Platform_cable"),stops=stops,tunnelRoofs=roofs.ToArray(),player=player,cameraRig=rig,routePreview=line});
            player.InitializeGameplay(controller,cam,O+spawn);runtime.AddComponent<RainCargoHUD>().Initialize(controller);
            var sun=new GameObject("Sun_soft_key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.6f;sun.color=new Color(1,.89f,.76f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-32,0);RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.68f,.72f);RenderSettings.fog=false;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();Export();Debug.Log("RAIN_CARGO_REVISION_CREATED");
        }
        private static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/"+n+".mat");
        private static Transform Group(string n,Vector3 p=default,Transform parent=null){var t=new GameObject(n).transform;t.SetParent(parent,true);t.position=O+p;return t;}
        private static GameObject Prim(string n,PrimitiveType type,Vector3 p,Vector3 size,Material m,Transform parent,bool collision=true)
        {var g=GameObject.CreatePrimitive(type);g.name=n;g.transform.SetParent(parent,true);g.transform.position=O+p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        private static GameObject Box(string n,Vector3 p,Vector3 size,Material m,Transform parent,bool collision=true)=>Prim(n,PrimitiveType.Cube,p,size,m,parent,collision);
        private static void Floor(string n,float x0,float x1,float z0,float z1,float y)
        {Box(n,new Vector3((x0+x1)/2,y-.18f,(z0+z1)/2),new Vector3(x1-x0,.36f,z1-z0),stone,court);foreach(float x in new[]{x0+.25f,x1-.25f})foreach(float z in new[]{z0+.25f,z1-.25f})Box("Terrace_stilt",new Vector3(x,(y+9)/2,z),new Vector3(.22f,y-9,.22f),wood,court,false);}
        private static void Beam(string n,Vector3 a,Vector3 b,float w,float h,Material m,Transform parent,bool collision=true)
        {var g=Box(n,(a+b)/2,new Vector3(w,h,Vector3.Distance(a,b)),m,parent,collision);g.transform.rotation=Quaternion.LookRotation(b-a);}
        private static void Ramp(string n,Vector3 a,Vector3 b,float width)
        {Beam(n,a-Vector3.up*.1f,b-Vector3.up*.1f,width,.2f,stone,court);Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized).normalized*(width/2-.08f);foreach(int s in new[]{-1,1})Beam("Stair_side_rail",a+side*s+Vector3.up*.9f,b+side*s+Vector3.up*.9f,.09f,.09f,wood,court);for(int i=1;i<20;i++){var p=Vector3.Lerp(a,b,i/20f);Beam("Stair_tread_mark",p-side*.94f,p+side*.94f,.035f,.02f,chalk,court,false);}}
        private static void Fence(Vector3 a,Vector3 b,Transform parent)
        {int count=Mathf.CeilToInt(Vector3.Distance(a,b)/2.6f);for(int i=0;i<=count;i++)Box("Edge_post",Vector3.Lerp(a,b,i/(float)count)+Vector3.up*.45f,new Vector3(.12f,.9f,.12f),wood,parent);Beam("Edge_rail",a+Vector3.up*.86f,b+Vector3.up*.86f,.09f,.09f,wood,parent);}
        private static void Track(Vector3 a,Vector3 b,Transform parent)
        {Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized).normalized*.85f;foreach(int s in new[]{-1,1})Beam("Copper_freight_rail",a+side*s+Vector3.up*.035f,b+side*s+Vector3.up*.035f,.045f,.035f,copper,parent,false);int count=Mathf.CeilToInt(Vector3.Distance(a,b)/3);for(int i=1;i<count;i++){var p=Vector3.Lerp(a,b,i/(float)count);var d=(b-a).normalized;Beam("Forward_chevron",p-side*.4f-d*.3f+Vector3.up*.045f,p+Vector3.up*.045f,.1f,.025f,copper,parent,false);Beam("Forward_chevron",p+side*.4f-d*.3f+Vector3.up*.045f,p+Vector3.up*.045f,.1f,.025f,copper,parent,false);}}
        private static Transform Station(string n,Vector3 p)
        {var t=Group(n,p);Box("Console",p+Vector3.up*.5f,new Vector3(.8f,1,.65f),wood,t);Box("Card_socket",p+Vector3.up*1.05f,new Vector3(.45f,.1f,.4f),copper,t,false);Box("Weather_window",p+new Vector3(0,1.9f,.7f),new Vector3(1.6f,1.1f,.15f),teal,t,false);foreach(float s in new[]{-1.1f,1.1f})Box("Window_pillar",p+new Vector3(s,1.5f,.7f),new Vector3(.17f,3,.17f),wood,t);Box("Half_canopy",p+new Vector3(0,3,.7f),new Vector3(2.8f,.22f,1.2f),roof,t,false);return t;}
        private static Transform Lever(string n,Vector3 p)
        {var t=Group(n,p);Box("Stand",p+Vector3.up*.45f,new Vector3(.5f,.9f,.5f),wood,t);Box("Handle",p+Vector3.up*.95f,new Vector3(.95f,.15f,.15f),copper,t);return t;}
        private static void TankShell(string n,Vector3 p,Transform parent)
        {Box(n+"_floor",p+Vector3.up*.06f,new Vector3(1.5f,.12f,1.3f),copper,parent,false);foreach(float x in new[]{-.72f,.72f})Box(n+"_cheek",p+new Vector3(x,.57f,0),new Vector3(.08f,1.05f,1.3f),wood,parent,false);foreach(float z in new[]{-.62f,.62f})Box(n+"_rim",p+new Vector3(0,1.05f,z),new Vector3(1.5f,.1f,.08f),copper,parent,false);}
        private static Transform Cart(out Transform tank)
        {
            Vector3 p=new Vector3(-16,15,0);var t=Group("06_Water_and_sapling_cart",p);
            Box("Cart_tray",p+Vector3.up*.55f,new Vector3(1.8f,.3f,3.1f),wood,t,false);
            foreach(float x in new[]{-.85f,.85f})foreach(float z in new[]{-1.1f,1.1f}){var w=Prim("Wheel",PrimitiveType.Cylinder,p+new Vector3(x,.27f,z),new Vector3(.52f,.12f,.52f),roof,t,false);w.transform.rotation=Quaternion.Euler(0,0,90);}
            TankShell("Cart_water_tank",p+new Vector3(0,1.06f,-.72f),t);
            tank=Box("Carried_rain",p+new Vector3(0,1.2f,-.72f),new Vector3(1.26f,.015f,1.05f),water,t,false).transform;
            Box("Sapling_trunk",p+new Vector3(0,1.25f,1),new Vector3(.1f,1.4f,.1f),wood,t,false);
            Prim("Sapling_crown",PrimitiveType.Sphere,p+new Vector3(0,2.0f,1),new Vector3(1.0f,1.0f,1.0f),leaf,t,false);
            var collider=t.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.25f,0);collider.size=new Vector3(1.8f,2.5f,3.1f);t.rotation=Quaternion.Euler(0,90,0);return t;
        }
        private static void Rock(string n,Vector3 p,Vector3 scale,float yaw){var g=Box(n,p,scale,roof,scenery,false);g.transform.rotation=Quaternion.Euler(0,yaw,0);}
        private static void House(string n,Vector3 p,Vector3 size)
        {var t=Group(n,p,scenery);Box("Wall",p+Vector3.up*size.y/2,size,stone,t,false);float half=(size.x+.8f)/2;foreach(int sign in new[]{-1,1}){var r=Box("Pitched_roof",p+new Vector3(sign*half/2,size.y+half*.18f,0),new Vector3(half/.94f,.22f,size.z+1),roof,t,false);r.transform.rotation=Quaternion.Euler(0,0,-sign*20);}Box("Door",p+new Vector3(0,1,-size.z/2-.02f),new Vector3(.9f,2,.06f),wood,t,false);foreach(float x in new[]{-size.x*.3f,size.x*.3f})Box("Window",p+new Vector3(x,1.9f,-size.z/2-.04f),new Vector3(.85f,.85f,.08f),teal,t,false);Box("Cliff_veranda",p+new Vector3(0,-.18f,-1),new Vector3(size.x+2,.36f,size.z+2),chalk,t,false);}

        [MenuItem("Borrowing Sun/Rain Cargo/2 Export complete Blender snapshot")]
        public static void Export()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorSceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open the saved Rain Cargo street outside Play Mode.");
            var scene=EditorSceneManager.GetActiveScene();var objects=new List<CourtSceneSync.ObjectData>();
            foreach(var root in scene.GetRootGameObjects())foreach(var f in root.GetComponentsInChildren<MeshFilter>(false))
            {var renderer=f.GetComponent<MeshRenderer>();var mesh=f.sharedMesh;if(!renderer||!renderer.enabled||!mesh)continue;var matrix=f.transform.localToWorldMatrix;var nm=matrix.inverse.transpose;var mats=renderer.sharedMaterials;var subs=new List<CourtSceneSync.SubmeshData>();
                for(int i=0;i<mesh.subMeshCount;i++){var m=mats.Length>0?mats[Math.Min(i,mats.Length-1)]:null;var c=m&&m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.gray;subs.Add(new CourtSceneSync.SubmeshData{triangles=mesh.GetTriangles(i),material=new CourtSceneSync.MaterialData{name=m?m.name:"Fallback",rgba=new[]{c.r,c.g,c.b,c.a}}});}
                objects.Add(new CourtSceneSync.ObjectData{id=GlobalObjectId.GetGlobalObjectIdSlow(f.gameObject).ToString(),name=f.name,path=PathOf(f.transform),category=root.name.StartsWith("10_")?"scenery":root.name.StartsWith("01_")?"court":"gameplay",vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray(),normals=mesh.normals.Select(n=>nm.MultiplyVector(n).normalized).ToArray(),submeshes=subs.ToArray()});}
            var cam=Camera.main;var data=new CourtSceneSync.SceneData{scene=scene.path,revision=DateTime.UtcNow.ToString("O"),units="metres; Unity Y up; initial A0 tank0 B0",objects=objects.ToArray(),camera=new CourtSceneSync.CameraData{position=cam.transform.position,target=cam.transform.position+cam.transform.forward*40,orthographicSize=cam.orthographicSize,aspect=cam.aspect},lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).Where(l=>l.type==LightType.Directional).Select(l=>new CourtSceneSync.LightData{direction=l.transform.forward,intensity=l.intensity,rgba=new[]{l.color.r,l.color.g,l.color.b,l.color.a}}).ToArray()};
            var dir=Path.GetFullPath(Path.Combine(Root,"../art-source/rain-cargo"));Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"unity-scene.json"),JsonUtility.ToJson(data));Debug.Log("RAIN_CARGO_SNAPSHOT_EXPORTED "+objects.Count);
        }
        private static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;

        [MenuItem("Borrowing Sun/Rain Cargo/Repair ramp mouths")]
        public static void RepairRampMouths()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorSceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open RainCargo outside Play Mode.");
            Action<string,Vector3,Vector3> set=(name,position,scale)=>{var go=GameObject.Find(name);if(go==null)throw new InvalidOperationException("Missing "+name);Undo.RecordObject(go.transform,"Align ramp landing");go.transform.position=O+position;go.transform.localScale=scale;};
            set("West_high_landing",new Vector3(-9.2f,17.82f,-4.75f),new Vector3(2,.36f,4.3f));
            set("Upper_shortcut_landing",new Vector3(11,17.82f,-22.7f),new Vector3(6,.36f,2.6f));
            set("A_high_to_upper_street",new Vector3(-1.5f,17.82f,-5.8f),new Vector3(4,.36f,6.4f));
            // A tilted beam's upper face begins 1.64 cm beyond its centreline endpoint.
            // Overlap the fixed floor so the supported-footprint guard sees continuous ground.
            set("Middle_turning_court",new Vector3(3,14.82f,-2.95f),new Vector3(10,.36f,6.1f));
            var duplicate=GameObject.Find("Middle_south_passing_strip");if(duplicate!=null){Undo.RecordObject(duplicate,"Remove coplanar duplicate visual");duplicate.SetActive(false);}
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Export();
        }

        [MenuItem("Borrowing Sun/Rain Cargo/Refine cliff silhouettes")]
        public static void RefineCliffs()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorSceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open the revision outside Play Mode.");
            if(GameObject.Find("11_Cliff_silhouette_revision")!=null)return;
            var scene=EditorSceneManager.GetActiveScene();var env=scene.GetRootGameObjects().First(g=>g.name.StartsWith("10_"));
            string meshPath="Assets/BorrowingSun/Art/Environment/RainCargoCliff.asset";
            Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                Vector2[] ring={new Vector2(-.5f,-.28f),new Vector2(-.28f,-.5f),new Vector2(.28f,-.5f),new Vector2(.5f,-.28f),new Vector2(.5f,.28f),new Vector2(.28f,.5f),new Vector2(-.28f,.5f),new Vector2(-.5f,.28f)};
                var vertices=new List<Vector3>();var triangles=new List<int>();
                Action<Vector3,Vector3,Vector3> tri=(a,b,c)=>{int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);};
                for(int i=0;i<8;i++)
                {var p=ring[i];var q=ring[(i+1)%8];var a=new Vector3(p.x,-.5f,p.y);var b=new Vector3(q.x,-.5f,q.y);var c=new Vector3(q.x*.76f,.5f,q.y*.82f);var d=new Vector3(p.x*.76f,.5f,p.y*.82f);tri(a,c,b);tri(a,d,c);tri(Vector3.up*.5f,c,d);tri(Vector3.down*.5f,a,b);}
                mesh=new Mesh{name="Bevelled_mountain_plateau"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);
            }
            foreach(var f in env.GetComponentsInChildren<MeshFilter>())
            {
                if(f.name.Contains("bedrock")||f.name=="Tunnel_back_ridge"||f.name=="East_ridge"||f.name=="High_city_spine")
                {Undo.RecordObject(f,"Shape mountain rather than housing block");f.sharedMesh=mesh;}
                if(f.name=="Cliff_veranda")
                {Undo.RecordObject(f.transform,"Separate coplanar courtyard surfaces");f.transform.position+=Vector3.up*.045f;}
            }
            var marker=new GameObject("11_Cliff_silhouette_revision");Undo.RegisterCreatedObjectUndo(marker,"Refine cliff silhouettes");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Export();
        }
    }
}
