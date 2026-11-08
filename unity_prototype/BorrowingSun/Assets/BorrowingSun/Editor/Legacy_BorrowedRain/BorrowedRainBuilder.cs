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
    /// <summary>Independent, editable middle-level scene. Never regenerates the first court.</summary>
    public static class BorrowedRainBuilder
    {
        public const string ScenePath = "Assets/BorrowingSun/Scenes/03_BorrowedRain.unity";
        private static readonly Vector3 Offset = new Vector3(-6, 0, 66);
        private static Transform court, scenery;
        private static Material stone, chalk, wood, teal, copper, water, leaf, roof;
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));

        [MenuItem("Borrowing Sun/Borrowed Rain/1 Create middle level")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Middle level already exists. Open and edit the saved scene; regeneration is deliberately disabled.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            stone=Mat("Stone"); chalk=Mat("Chalk"); wood=Mat("Cinnabar"); teal=Mat("WeatherCircuit");
            copper=Mat("Copper"); water=Mat("Water"); leaf=Mat("Sapling"); roof=Mat("Roof");
            court=Group("01_Fixed_courts_and_maintenance"); scenery=Group("10_Interleaved_cliff_neighbourhood");
            Platform("West_cargo_court_15", -12,-6,-5,6,15);
            Platform("East_cargo_court_15", 6,12,-5,6,15);
            Platform("Central_fixed_refuge_15",0,2,-1.5f,6,15);
            // Fixed endpoint exits are outside the moving footprints, on their south side.
            Platform("A_south_low_exit",-3.75f,-2.25f,-3,-1.5f,15);
            Platform("A_south_high_exit",-3.75f,-2.25f,-3,-1.5f,18);
            Platform("A_low_gallery",-15,-2.25f,-5,-3.5f,15);
            Platform("A_low_exit_link",-3.75f,-2.25f,-5,-3,15);
            Platform("A_high_observation_spur",-3.75f,-1.5f,-6.75f,-3,18);
            Ramp("A_high_observation_ramp",new Vector3(-15,15,-6),new Vector3(-3,18,-6),1.5f);
            Platform("West_high_ramp_landing",-16.5f,-14.25f,-6.75f,-3.5f,15);
            Platform("B_south_low_exit",3.25f,4.75f,-3,-1.5f,12);
            Platform("B_south_high_exit",3.25f,4.75f,-3,-1.5f,15);
            Platform("B_lower_spur",3.25f,4.75f,-12.5f,-3,12);
            Platform("B_high_gallery",3.25f,12,-5,-3.5f,15);
            Platform("B_high_exit_link",3.25f,4.75f,-3.5f,-3,15);
            Platform("Lower_inspection_gallery",-16.5f,14,-12.5f,-10.5f,12);
            Ramp("West_lower_ramp",new Vector3(-15,12,-9),new Vector3(-3,15,-9),1.5f);
            Platform("West_bottom_ramp_landing",-16.5f,-14.25f,-11.5f,-8.25f,12);
            Platform("West_top_ramp_link",-3.75f,-1.5f,-9.75f,-5,15);
            Ramp("East_lower_ramp",new Vector3(13,12,-9),new Vector3(13,15,3),1.5f);
            Platform("East_bottom_ramp_landing",12.25f,13.75f,-11.5f,-8.25f,12);
            Platform("East_top_ramp_link",11.25f,13.75f,2.25f,4.5f,15);
            // Railings face the void, leaving all cross-deck and ramp mouths open.
            Fence(new Vector3(-12,15,6),new Vector3(-6,15,6));
            Fence(new Vector3(6,15,6),new Vector3(12,15,6));
            Fence(new Vector3(0,15,6),new Vector3(2,15,6));
            Fence(new Vector3(-16,12,-12.5f),new Vector3(14,12,-12.5f));
            Fence(new Vector3(-14.1f,15,-5),new Vector3(-6,15,-5));
            Fence(new Vector3(6,15,-5),new Vector3(12,15,-5));
            // Two visibly separate basins. Water meshes have no walkable collision.
            Box("A_pool_bed",new Vector3(-3,10.6f,0),new Vector3(6.8f,.4f,4),roof,court);
            Box("B_pool_bed",new Vector3(4,10.6f,0),new Vector3(4.8f,.4f,4),roof,court);
            var wa=Box("Water_A",new Vector3(-3,14.78f,0),new Vector3(6.6f,.06f,3.6f),water,court,false).transform;
            var wb=Box("Water_B",new Vector3(4,11.78f,0),new Vector3(4.6f,.06f,3.6f),water,court,false).transform;
            var a=Group("02_A_bridge_also_aqueduct",new Vector3(-3,15,0));
            var b=Group("03_B_cargo_lift",new Vector3(4,12,0));
            Box("A_moving_deck",new Vector3(-3,14.85f,0),new Vector3(6,.3f,3),chalk,a);
            Box("B_moving_deck",new Vector3(4,11.85f,0),new Vector3(4,.3f,3),chalk,b);
            for (int i=-5;i<=-1;i++) Box("A_board_joint",new Vector3(i,15.01f,0),new Vector3(.035f,.02f,2.9f),wood,a,false);
            for (int i=3;i<=5;i++) Box("B_board_joint",new Vector3(i,12.01f,0),new Vector3(.035f,.02f,2.9f),wood,b,false);
            foreach(float x in new[]{-6.3f,.3f,1.7f,6.3f})
            {
                Box("Vertical_float_guide",new Vector3(x,14.5f,1.35f),new Vector3(.12f,8,.12f),roof,court);
                for(int h=12;h<=18;h++) Box("Height_tick",new Vector3(x,h,1.24f),new Vector3(.32f,.055f,.05f),teal,court,false);
            }
            // Moving copper trough: outlet surface starts at 15.45, rises to 18.45.
            Box("Moving_gutter_floor",new Vector3(-3,15.42f,1.9f),new Vector3(5.7f,.06f,.65f),copper,a,false);
            foreach(float z in new[]{1.53f,2.27f}) Box("Moving_gutter_cheek",new Vector3(-3,15.59f,z),new Vector3(5.7f,.32f,.10f),wood,a,false);
            var movingOutlet=Group("Moving_gutter_outlet",new Vector3(-.15f,15.45f,1.9f),a);
            Box("Fixed_receiver_18_30",new Vector3(.40f,18.27f,1.9f),new Vector3(.8f,.06f,.8f),copper,court,false);
            Beam("Fixed_aqueduct_floor",new Vector3(.8f,18.27f,1.9f),new Vector3(4,18.12f,1.9f),.6f,.06f,copper,court,false);
            foreach(float z in new[]{1.51f,2.29f}) Beam("Fixed_aqueduct_cheek",new Vector3(.05f,18.42f,z),new Vector3(4.1f,18.24f,z),.10f,.28f,wood,court,false);
            foreach(float x in new[]{.75f,4.05f}) Box("Aqueduct_pier",new Vector3(x,15,2.45f),new Vector3(.20f,6.7f,.2f),wood,court);
            var remoteOutlet=Group("Fixed_remote_outlet_20",new Vector3(-5,20,1.9f),court);
            Aperture("Paired_cloud_exit",new Vector3(-5,20,1.9f),new Vector3(1.2f,.12f,1),court);
            Beam("Remote_outlet_support",new Vector3(-6.4f,15,2.7f),new Vector3(-6.4f,20,2.7f),.18f,.18f,wood,court);
            Beam("Remote_outlet_arm",new Vector3(-6.4f,20,2.7f),new Vector3(-5,20,1.9f),.14f,.14f,wood,court,false);
            // A visible drain catches failed rain; it never secretly fills A or B.
            Box("Waste_catcher",new Vector3(-.1f,11.4f,1.9f),new Vector3(1.2f,.2f,1.2f),water,court,false);
            Beam("Waste_channel",new Vector3(-.1f,11.4f,1.9f),new Vector3(-.1f,10.7f,7),.7f,.15f,roof,court,false);
            var good=Group("07_Flow_to_B",Vector3.zero); var waste=Group("07_Flow_to_waste",Vector3.zero);
            Beam("Connected_rain",new Vector3(-.15f,18.48f,1.9f),new Vector3(4,18.19f,1.9f),.35f,.035f,teal,good,false);
            Beam("B_falling_water",new Vector3(4,18.19f,1.9f),new Vector3(4,12,1.1f),.12f,.12f,teal,good,false);
            Beam("Unconnected_rain",new Vector3(-.1f,15.45f,1.9f),new Vector3(-.1f,11.5f,1.9f),.16f,.16f,teal,waste,false);
            good.gameObject.SetActive(false); waste.gameObject.SetActive(false);
            var s0=Station("04_S0_local_rain",new Vector3(-10,15,2),false);
            var s1=Station("04_S1_borrowed_rain",new Vector3(-9,15,-3.8f),true);
            Beam("S0_feed_A",new Vector3(-10,15.18f,2.6f),new Vector3(-6.1f,15.18f,2.6f),.18f,.18f,teal,court,false);
            AddLocalIntake();
            var va=Valve("05_Valve_A",new Vector3(-8,15,4.7f));
            var vb=Valve("05_Valve_B",new Vector3(9,15,4.7f));
            var cart=MakeCart();
            foreach(var p in new[]{new Vector3(-8,15,0),new Vector3(1,15,0),new Vector3(8,15,0)})
                Box("Cargo_stop",p+Vector3.up*.012f,new Vector3(2,.02f,1.6f),copper,court,false);
            Box("Destination_receiving_pad",new Vector3(8,15.02f,4),new Vector3(2.8f,.03f,2.8f),teal,court,false);
            var playerObj=new GameObject("08_Apprentice");
            var cc=playerObj.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.3f;cc.center=new Vector3(0,.9f,0);cc.stepOffset=.24f;cc.slopeLimit=48;cc.skinWidth=.03f;
            Vector3 spawn=new Vector3(-10.5f,15.06f,-.6f); playerObj.transform.position=Offset+spawn;
            var body=Primitive("Apprentice_body",PrimitiveType.Capsule,spawn+Vector3.up*.9f,new Vector3(.56f,.9f,.56f),copper,playerObj.transform,false);
            var player=playerObj.AddComponent<CourtPlayer>();
            var card=Box("Unique_rain_card",spawn+Vector3.up*1.7f,new Vector3(.27f,.38f,.09f),teal,null,false).transform;
            var cameraObj=new GameObject("Camera_Main");var cam=cameraObj.AddComponent<Camera>();cam.tag="MainCamera";
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.35f,.46f,.48f);
            cameraObj.AddComponent<AudioListener>();cameraObj.AddComponent<UniversalAdditionalCameraData>();
            var rig=cameraObj.AddComponent<CourtCamera>();rig.Initialize(null,player,cam);
            rig.ConfigureViews(Offset+new Vector3(26,41,-29),Offset+new Vector3(-1,15,-2),18.5f,
                Offset+new Vector3(11,21,-8),Offset+new Vector3(-1,17.5f,1.5f));
            var runtime=new GameObject("09_BorrowedRain_runtime");var controller=runtime.AddComponent<BorrowedRainController>();
            var bindings=new BorrowedRainBindings {worldOffset=Offset,aBridge=a,bLift=b,cart=cart,waterA=wa,waterB=wb,
                station0=s0,station1=s1,valveA=va,valveB=vb,cardToken=card,successfulFlow=good,wasteFlow=waste,
                player=player,cameraRig=rig,hudFont=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular"),
                localRain=Rain("S0_local_rain_drops",new Vector3(-10,17.5f,2.6f),s0),
                remoteRain=Rain("S1_remote_rain_drops",new Vector3(-5,20,1.9f),remoteOutlet),
                safeRecoveryPoints=new[]{Group("Safe_west",new Vector3(-7,15,-2.5f),court),Group("Safe_east",new Vector3(8,15,-2.5f),court)}};
            controller.Initialize(bindings);runtime.AddComponent<BorrowedRainHUD>().Initialize(controller);
            player.InitializeGameplay(controller,cam,Offset+spawn);
            Neighbourhood();
            var sunObj=new GameObject("Sun_soft_key");var sun=sunObj.AddComponent<Light>();sun.type=LightType.Directional;
            sun.intensity=1.6f;sun.color=new Color(1,.89f,.76f);sun.shadows=LightShadows.Soft;sunObj.transform.rotation=Quaternion.Euler(48,-32,0);RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.68f,.72f);RenderSettings.fog=false;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
            AssetDatabase.SaveAssets();Export();Debug.Log("BORROWED_RAIN_SCENE_CREATED "+ScenePath);
        }

        private static Material Mat(string name)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/BorrowingSun/Materials/"+name+".mat");
            if(!m)throw new InvalidOperationException("Missing shared material "+name);return m;
        }
        private static Transform Group(string name,Vector3 position=default,Transform parent=null)
        {var t=new GameObject(name).transform;t.SetParent(parent);t.position=Offset+position;return t;}
        private static GameObject Primitive(string name,PrimitiveType type,Vector3 p,Vector3 size,Material mat,Transform parent,bool collision)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,true);g.transform.position=Offset+p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        private static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,Transform parent,bool collision=true)=>Primitive(name,PrimitiveType.Cube,p,size,mat,parent,collision);
        private static void Platform(string name,float x0,float x1,float z0,float z1,float y)
        {
            Box(name,new Vector3((x0+x1)/2,y-.15f,(z0+z1)/2),new Vector3(x1-x0,.3f,z1-z0),stone,court);
            if(x1-x0>3 && z1-z0>3) foreach(float x in new[]{x0+.22f,x1-.22f}) foreach(float z in new[]{z0+.22f,z1-.22f})
                Box("Deck_support",new Vector3(x,(y+10.5f)/2-.15f,z),new Vector3(.18f,y-10.5f,.18f),wood,court);
        }
        private static void Beam(string name,Vector3 a,Vector3 b,float width,float thick,Material mat,Transform parent,bool collision=true)
        {var g=Box(name,(a+b)/2,new Vector3(width,thick,Vector3.Distance(a,b)),mat,parent,collision);g.transform.rotation=Quaternion.LookRotation(b-a);}
        private static void Ramp(string name,Vector3 a,Vector3 b,float width)
        {
            Beam(name,a-Vector3.up*.1f,b-Vector3.up*.1f,width,.2f,stone,court);
            Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized).normalized*(width/2-.08f);
            foreach(int s in new[]{-1,1}) Beam("Ramp_hand_rail",a+side*s+Vector3.up*.9f,b+side*s+Vector3.up*.9f,.08f,.09f,wood,court);
            for(int i=0;i<=12;i++)
            {Vector3 p=Vector3.Lerp(a,b,i/12f);Beam("Ramp_step_mark",p-side*.82f+Vector3.up*.015f,p+side*.82f+Vector3.up*.015f,.04f,.02f,chalk,court,false);}
        }
        private static void Fence(Vector3 a,Vector3 b)
        {
            int n=Mathf.CeilToInt(Vector3.Distance(a,b)/2);for(int i=0;i<=n;i++)Box("Rail_post",Vector3.Lerp(a,b,i/(float)n)+Vector3.up*.45f,new Vector3(.12f,.9f,.12f),wood,court);
            Beam("Rail_bar",a+Vector3.up*.85f,b+Vector3.up*.85f,.09f,.09f,wood,court);
        }
        private static void Aperture(string name,Vector3 center,Vector3 size,Transform parent)
        {
            Box(name,center,size,teal,parent,false);
            foreach(int s in new[]{-1,1}) Box("Copper_aperture_frame",center+Vector3.right*s*(size.x/2+.06f),new Vector3(.12f,size.y+.18f,size.z+.18f),copper,parent,false);
        }
        private static Transform Station(string name,Vector3 foot,bool remote)
        {
            var t=Group(name,foot);Box("Console",foot+Vector3.up*.5f,new Vector3(.75f,1,.6f),wood,t);
            Box("Card_socket",foot+Vector3.up*1.03f,new Vector3(.42f,.08f,.30f),copper,t,false);
            Aperture(remote?"Paired_window_entrance":"Local_rain_window",foot+new Vector3(0,1.75f,.5f),new Vector3(1.4f,1,.12f),t);
            foreach(float x in new[]{-1f,1f})Box("Station_rear_post",foot+new Vector3(x,1.35f,.65f),new Vector3(.14f,2.7f,.14f),wood,t);
            Box("Station_half_canopy",foot+new Vector3(0,2.75f,.65f),new Vector3(2.5f,.18f,1),roof,t,false);return t;
        }
        private static Transform Valve(string name,Vector3 foot)
        {
            var t=Group(name,foot);Box("Drain_stand",foot+Vector3.up*.45f,new Vector3(.45f,.9f,.45f),wood,t);
            Box("Drain_handle",foot+Vector3.up*.95f,new Vector3(.9f,.1f,.18f),copper,t);return t;
        }
        private static ParticleSystem Rain(string name,Vector3 p,Transform parent)
        {
            var t=Group(name,p,parent);t.rotation=Quaternion.Euler(90,0,0);var ps=t.gameObject.AddComponent<ParticleSystem>();
            var m=ps.main;m.startLifetime=1.1f;m.startSpeed=4;m.startSize=.045f;m.startColor=new Color(.65f,.94f,1);m.playOnAwake=false;m.simulationSpace=ParticleSystemSimulationSpace.World;
            var e=ps.emission;e.rateOverTime=90;var s=ps.shape;s.shapeType=ParticleSystemShapeType.Box;s.scale=new Vector3(.9f,.5f,.08f);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=teal;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return ps;
        }
        private static Transform MakeCart()
        {
            Vector3 p=new Vector3(-8,15,0);var t=Group("06_Sapling_cart",p);
            Box("Cart_tray",p+Vector3.up*.48f,new Vector3(1.4f,.3f,2),wood,t,false);
            foreach(float x in new[]{-.63f,.63f})foreach(float z in new[]{-.7f,.7f})
            {var w=Primitive("Cart_wheel",PrimitiveType.Cylinder,p+new Vector3(x,.24f,z),new Vector3(.46f,.08f,.46f),roof,t,false);w.transform.rotation=Quaternion.Euler(0,0,90);}
            foreach(float z in new[]{-.5f,.5f})
            {Box("Sapling_trunk",p+new Vector3(0,1.1f,z),new Vector3(.09f,1.2f,.09f),wood,t,false);Primitive("Sapling_crown",PrimitiveType.Sphere,p+new Vector3(0,1.65f,z),new Vector3(.8f,.8f,.8f),leaf,t,false);}
            var c=t.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,1.1f,0);c.size=new Vector3(1.4f,2.2f,2);t.rotation=Quaternion.Euler(0,90,0);return t;
        }
        private static void AddLocalIntake()
        {
            Box("S0_rain_collector",new Vector3(-10,15.24f,2.6f),new Vector3(1.3f,.06f,1),water,court,false);
            foreach(float x in new[]{-10.7f,-9.3f})Box("S0_collector_cheek",new Vector3(x,15.35f,2.6f),new Vector3(.12f,.24f,1.1f),copper,court,false);
            Box("S0_enclosed_pump",new Vector3(-8.7f,15.45f,2.6f),new Vector3(.55f,.55f,.55f),copper,court,false);
            Beam("S0_basin_inlet_elbow",new Vector3(-6.1f,15.18f,2.6f),new Vector3(-6.1f,15.18f,1.7f),.18f,.18f,teal,court,false);
            Beam("S0_basin_inlet",new Vector3(-6.1f,15.18f,1.7f),new Vector3(-6.1f,14.5f,1.7f),.18f,.18f,teal,court,false);
        }

        private static void Neighbourhood()
        {
            // Large cut rock masses carry the buildings. Central negative space stays open.
            Rock("West_rock_spine",new Vector3(-21,8,9),new Vector3(12,14,14),8);
            Rock("West_upper_crag",new Vector3(-21,19,20),new Vector3(12,8,12),-9);
            Rock("East_rock_spine",new Vector3(20,8,14),new Vector3(10,14,12),-8);
            Rock("East_upper_crag",new Vector3(21,19,25),new Vector3(13,8,12),9);
            Rock("North_high_ridge",new Vector3(-2,20,26),new Vector3(20,18,12),5);
            House("West_lower_workshop",new Vector3(-17,15,7),new Vector3(4,3.3f,4));
            House("West_upper_residence",new Vector3(-20,23,20),new Vector3(4.5f,3.6f,4));
            House("East_receiver_store",new Vector3(18,15,11),new Vector3(4,3.2f,4.5f));
            House("East_ridge_house",new Vector3(20,23,24),new Vector3(4.5f,3.7f,4));
            House("Upper_weather_exchange",new Vector3(-3,29,26),new Vector3(6,4,5));
            Beam("Distant_cross_valley_structure_unbuilt_route",new Vector3(-15,24,17),new Vector3(13,24,17),2,.35f,stone,scenery,false);
            Beam("District_weather_backbone",new Vector3(-16,22,10),new Vector3(-3,29,24),.2f,.2f,teal,scenery,false);
            Beam("District_weather_backbone",new Vector3(-3,29,24),new Vector3(17,23,17),.2f,.2f,teal,scenery,false);
        }
        private static void Rock(string name,Vector3 p,Vector3 size,float yaw)
        {var g=Primitive(name,PrimitiveType.Cube,p,size,roof,scenery,false);g.transform.rotation=Quaternion.Euler(0,yaw,0);}
        private static void House(string name,Vector3 foot,Vector3 size)
        {
            var h=Group(name,foot,scenery);Box("Wall_body",foot+Vector3.up*size.y/2,size,stone,h,false);
            float half=(size.x+.8f)/2,rise=half*Mathf.Tan(20*Mathf.Deg2Rad);
            foreach(int sign in new[]{-1,1})
            {var r=Box("Pitched_roof",foot+new Vector3(sign*half/2,size.y+rise/2,0),new Vector3(half/Mathf.Cos(20*Mathf.Deg2Rad),.18f,size.z+.8f),roof,h,false);r.transform.rotation=Quaternion.Euler(0,0,-sign*20);}
            Box("Roof_ridge",foot+Vector3.up*(size.y+rise),new Vector3(.14f,.15f,size.z+.9f),wood,h,false);
            Box("Door",foot+new Vector3(0,.95f,-size.z/2-.03f),new Vector3(.8f,1.9f,.08f),wood,h,false);
            foreach(int sign in new[]{-1,1})Box("Weather_window",foot+new Vector3(sign*size.x*.3f,1.6f,-size.z/2-.05f),new Vector3(.6f,.7f,.08f),teal,h,false);
            Box("House_rock_shelf",foot-Vector3.up*.4f,new Vector3(size.x+1.5f,.8f,size.z+1.5f),stone,h,false);
        }

        [MenuItem("Borrowing Sun/Borrowed Rain/Repair scenery framing")]
        public static void RepairSceneryFraming()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open the middle scene outside Play Mode.");
            void Set(string name,Vector3 position,Vector3? size=null,float? yaw=null)
            {
                var t=GameObject.Find("10_Interleaved_cliff_neighbourhood/"+name)?.transform;
                if(!t)throw new InvalidOperationException("Missing scenery "+name);
                Undo.RecordObject(t,"Retreat cliff silhouettes from the playfield");t.position=Offset+position;
                if(size.HasValue)t.localScale=size.Value;if(yaw.HasValue)t.rotation=Quaternion.Euler(0,yaw.Value,0);
            }
            Set("West_rock_spine",new Vector3(-21,8,9),new Vector3(12,14,14),8);
            Set("West_upper_crag",new Vector3(-21,19,20),new Vector3(12,8,12),-9);
            Set("East_rock_spine",new Vector3(20,8,14),new Vector3(10,14,12),-8);
            Set("East_upper_crag",new Vector3(21,19,25),new Vector3(13,8,12),9);
            Set("West_lower_workshop",new Vector3(-17,15,7));Set("West_upper_residence",new Vector3(-20,23,20));
            Set("East_receiver_store",new Vector3(18,15,11));Set("East_ridge_house",new Vector3(20,23,24));
            var fixedRoot=GameObject.Find("01_Fixed_courts_and_maintenance").transform;
            var rampLanding=fixedRoot.Find("West_high_ramp_landing");
            Undo.RecordObject(rampLanding,"Give observation ramp a proper turning landing");
            rampLanding.position=Offset+new Vector3(-15.375f,14.85f,-5.125f);rampLanding.localScale=new Vector3(2.25f,.3f,3.25f);
            void SetPad(string name,Vector3 p,Vector3 size)
            {var t=fixedRoot.Find(name);Undo.RecordObject(t,"Give maintenance ramps turning clearance");t.position=Offset+p;t.localScale=size;}
            SetPad("A_high_observation_spur",new Vector3(-2.625f,17.85f,-4.875f),new Vector3(2.25f,.3f,3.75f));
            SetPad("West_top_ramp_link",new Vector3(-2.625f,14.85f,-7.375f),new Vector3(2.25f,.3f,4.75f));
            SetPad("West_bottom_ramp_landing",new Vector3(-15.375f,11.85f,-9.875f),new Vector3(2.25f,.3f,3.25f));
            SetPad("Lower_inspection_gallery",new Vector3(-1.25f,11.85f,-11.5f),new Vector3(30.5f,.3f,2));
            SetPad("East_top_ramp_link",new Vector3(12.5f,14.85f,3.375f),new Vector3(2.5f,.3f,2.25f));
            var controller=UnityEngine.Object.FindAnyObjectByType<BorrowedRainController>();
            Undo.RecordObject(controller,"Keep recovery on connected banks");
            Undo.RecordObject(controller.CameraRig,"Align keyboard diagonals with the cargo route");
            controller.CameraRig.ConfigureViews(Offset+new Vector3(26,41,-29),Offset+new Vector3(-1,15,-2),18.5f,
                Offset+new Vector3(11,21,-8),Offset+new Vector3(-1,17.5f,1.5f));
            controller.Bindings.safeRecoveryPoints=controller.Bindings.safeRecoveryPoints.Where(t=>t && t.name!="Safe_middle").ToArray();
            if(!GameObject.Find("S0_rain_collector"))
            {court=fixedRoot;water=Mat("Water");copper=Mat("Copper");teal=Mat("WeatherCircuit");AddLocalIntake();}
            foreach(Transform t in fixedRoot)
            {
                var p=t.position-Offset;
                if(t.name=="Rail_post" && Vector3.Distance(p,new Vector3(-15,15.45f,-5))<.02f)
                {Undo.RecordObject(t,"Open observation ramp entrance");t.position=Offset+new Vector3(-14.1f,15.45f,-5);}
                if(t.name=="Rail_bar" && Vector3.Distance(p,new Vector3(-10.5f,15.85f,-5))<.02f)
                {Undo.RecordObject(t,"Open observation ramp entrance");t.position=Offset+new Vector3(-10.05f,15.85f,-5);t.localScale=new Vector3(.09f,.09f,8.1f);}
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Export();
            Debug.Log("BORROWED_RAIN_SCENERY_FRAMING_REPAIRED");
        }

        [MenuItem("Borrowing Sun/Borrowed Rain/2 Export complete Blender snapshot")]
        public static void Export()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path!=ScenePath)throw new InvalidOperationException("Open the saved BorrowedRain scene outside Play Mode.");
            var scene=EditorSceneManager.GetActiveScene();var objects=new List<CourtSceneSync.ObjectData>();
            foreach(var root in scene.GetRootGameObjects())foreach(var f in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer=f.GetComponent<MeshRenderer>();var mesh=f.sharedMesh;if(!renderer||!renderer.enabled||!mesh)continue;
                var matrix=f.transform.localToWorldMatrix;var nm=matrix.inverse.transpose;var mats=renderer.sharedMaterials;var subs=new List<CourtSceneSync.SubmeshData>();
                for(int i=0;i<mesh.subMeshCount;i++)
                {var m=mats.Length>0?mats[Math.Min(i,mats.Length-1)]:null;var c=m&&m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.gray;subs.Add(new CourtSceneSync.SubmeshData{triangles=mesh.GetTriangles(i),material=new CourtSceneSync.MaterialData{name=m?m.name:"Fallback",rgba=new[]{c.r,c.g,c.b,c.a}}});}
                objects.Add(new CourtSceneSync.ObjectData{id=GlobalObjectId.GetGlobalObjectIdSlow(f.gameObject).ToString(),name=f.name,path=HierarchyPath(f.transform),category=root.name.StartsWith("10_")?"scenery":root.name.StartsWith("01_")?"court":"gameplay",vertices=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray(),normals=mesh.normals.Select(n=>nm.MultiplyVector(n).normalized).ToArray(),submeshes=subs.ToArray()});
            }
            var cam=Camera.main;var data=new CourtSceneSync.SceneData{scene=scene.path,revision=DateTime.UtcNow.ToString("O"),units="metres; Unity X right Y up Z uphill; initial A0 B0",objects=objects.ToArray(),camera=new CourtSceneSync.CameraData{position=cam.transform.position,target=cam.transform.position+cam.transform.forward*40,orthographicSize=cam.orthographicSize,aspect=1.6f},lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).Where(l=>l.type==LightType.Directional).Select(l=>new CourtSceneSync.LightData{direction=l.transform.forward,intensity=l.intensity,rgba=new[]{l.color.r,l.color.g,l.color.b,l.color.a}}).ToArray()};
            var dir=Path.GetFullPath(Path.Combine(Root,"../art-source/borrowed-rain"));Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"unity-scene.json"),JsonUtility.ToJson(data));
            Debug.Log("BORROWED_RAIN_SNAPSHOT_EXPORTED "+objects.Count+" meshes");
        }
        private static string HierarchyPath(Transform t)=>t.parent?HierarchyPath(t.parent)+"/"+t.name:t.name;
    }
}
