using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BorrowingSun.Editor
{
    /// <summary>Creates editable scene geometry. Regeneration writes a separate scene, preserving the played scene.</summary>
    public static class CourtBuilder
    {
        public const string ScenePath = "Assets/BorrowingSun/Scenes/01_WaterCourt.unity";
        private const string Art = "Assets/BorrowingSun/Materials";
        private static Material stone, edge, wood, teal, copper, water, leaf, dark;
        private static Transform architecture;

        [MenuItem("Borrowing Sun/Create new water court copy")]
        public static void CreateCopy()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create(AssetDatabase.GenerateUniqueAssetPath(ScenePath));
        }

        public static void BuildFirstScene()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene exists; use the menu to create a separate copy.");
            Create(ScenePath);
        }

        private static void Create(string path)
        {
            Directory.CreateDirectory("Assets/BorrowingSun/Scenes");
            Directory.CreateDirectory(Art);
            Directory.CreateDirectory("Assets/BorrowingSun/Prefabs");
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            stone = MaterialAsset("Stone", new Color(.72f,.70f,.62f));
            edge = MaterialAsset("Chalk", new Color(.88f,.84f,.72f));
            wood = MaterialAsset("Cinnabar", new Color(.50f,.25f,.20f));
            teal = MaterialAsset("WeatherCircuit", new Color(.15f,.65f,.64f), .25f);
            copper = MaterialAsset("Copper", new Color(.90f,.55f,.25f));
            water = MaterialAsset("Water", new Color(.18f,.41f,.45f));
            leaf = MaterialAsset("Sapling", new Color(.40f,.62f,.38f));
            dark = MaterialAsset("Roof", new Color(.16f,.23f,.25f));
            architecture = new GameObject("01_Architecture_editable_2m_grid").transform;

            // All surface heights are design heights; boxes extend down from their surfaces.
            Platform("Entry_yard_0m", -11, 13, -14, -3, 0);
            Platform("North_landing_B_3m", -7, 1.5f, 3, 7, 3);
            Platform("West_return_L_3m", -7, -4, -1.5f, 3, 3);
            Platform("Stair_landing_3m", -9, -7, 4, 6, 3);
            Platform("Receiver_C_3m", 4, 13, -1.5f, 8, 3);
            Platform("Next_level_threshold_3m", 8, 13, 8, 13, 3);
            Cube("Pool_bed", new Vector3(0,-1.4f,0), new Vector3(8,.3f,6), dark, architecture);
            // Recessed water footprint stays below all moving deck surfaces.
            var waterSurface = Cube("Water_level", new Vector3(0,-.35f,0),new Vector3(7.7f,.08f,5.7f),water,architecture).transform;
            UnityEngine.Object.DestroyImmediate(waterSurface.GetComponent<Collider>());
            // Narrow ramp has a 1.05m clear width between rails; player can use it, cart cannot.
            var ramp = Cube("Pedestrian_stair_ramp",new Vector3(-8.25f,1.38f,-2),new Vector3(1.5f,.24f,Mathf.Sqrt(153)),stone,architecture);
            ramp.transform.rotation = Quaternion.Euler(-Mathf.Atan2(3,12)*Mathf.Rad2Deg,0,0);
            for (int i=0;i<=12;i++)
            {
                float z=-8+i, y=i*.25f;
                Cube("Stair_tread_mark",new Vector3(-8.25f,y+.012f,z),new Vector3(1.1f,.025f,.065f),edge,architecture,false);
                if(i%2==0) { Post(new Vector3(-8.855f,y,z)); Post(new Vector3(-7.645f,y,z)); }
            }
            Rail(new Vector3(-8.855f,1,-8),new Vector3(-8.855f,4,4));
            Rail(new Vector3(-7.645f,1,-8),new Vector3(-7.645f,4,4));
            // Guardrails leave the north unload and west/east bridge mouths open.
            Fence(new Vector3(-7,3,-1.5f),new Vector3(-7,3,4));
            Fence(new Vector3(-4,3,1.5f),new Vector3(-4,3,3));
            Fence(new Vector3(-7,3,7),new Vector3(1.5f,3,7));
            Fence(new Vector3(1.5f,3,3),new Vector3(1.5f,3,7));
            Fence(new Vector3(4,3,1.5f),new Vector3(4,3,8));
            Fence(new Vector3(4,3,-1.5f),new Vector3(13,3,-1.5f));
            Fence(new Vector3(13,3,-1.5f),new Vector3(13,3,13));
            Fence(new Vector3(8,3,8),new Vector3(8,3,13));
            Fence(new Vector3(-11,0,-14),new Vector3(13,0,-14));
            Fence(new Vector3(-11,0,-14),new Vector3(-11,0,-3));
            Fence(new Vector3(13,0,-14),new Vector3(13,0,-3));

            var lower = new GameObject("02_Float_lower_NS_3x6").transform;
            var upper = new GameObject("03_Float_upper_EW_8x3").transform;
            upper.position = new Vector3(0,3,0);
            Cube("Lower_deck",new Vector3(0,-.15f,0),new Vector3(3,.3f,6),edge,lower);
            Cube("Upper_deck",new Vector3(0,2.85f,0),new Vector3(8,.3f,3),edge,upper);
            // Visible vertical carriage links explain why both floors travel together.
            foreach(float x in new[]{-1.38f,1.38f}) foreach(float z in new[]{-1.38f,1.38f})
                Cube("Carriage_link",new Vector3(x,1.4f,z),new Vector3(.16f,2.8f,.16f),copper,lower);
            foreach(float x in new[]{-1.72f,1.72f}) foreach(float z in new[]{-2.65f,2.65f})
            {
                Cube("Guide_rail",new Vector3(x,3,z),new Vector3(.16f,7,.16f),dark,architecture);
                for(int i=0;i<=6;i++) Cube("Height_mark",new Vector3(x-.015f,i,z-.11f),new Vector3(.25f,.05f,.06f),teal,architecture,false);
            }
            for(int i=-2;i<=2;i++) Cube("Lower_deck_board",new Vector3(0,.008f,i),new Vector3(2.8f,.015f,.035f),wood,lower,false);
            for(int i=-3;i<=3;i++) Cube("Upper_deck_board",new Vector3(i,3.008f,0),new Vector3(.035f,.015f,2.8f),wood,upper,false);

            var rainMachine = new GameObject("04_Rain_pavilion_interaction").transform;
            rainMachine.position = new Vector3(-5,0,-7);
            Cube("Console",new Vector3(-5,.6f,-7),new Vector3(.9f,1.2f,.65f),wood,rainMachine);
            Cube("Card_socket",new Vector3(-5,1.23f,-7),new Vector3(.42f,.09f,.33f),copper,rainMachine,false);
            Cube("Weather_aperture",new Vector3(-5,1.75f,-6.55f),new Vector3(1.4f,1.15f,.22f),teal,rainMachine,false);
            foreach(float x in new[]{-6.4f,-3.6f}) Cube("Pavilion_column",new Vector3(x,1.5f,-6.3f),new Vector3(.2f,3,.2f),wood,rainMachine);
            // Half canopy leaves the front and lateral views unobstructed.
            Roof("Weather_pavilion_half_roof",new Vector3(-5,3.05f,-6.2f),new Vector3(3.5f,.28f,1.5f),rainMachine);
            Pipe(new Vector3(-5,.25f,-6),new Vector3(-5,.25f,-2.6f));
            Pipe(new Vector3(-5,.25f,-2.6f),new Vector3(-1.8f,.25f,-2.6f));
            var rainObj = new GameObject("Contained_rain");
            rainObj.transform.SetParent(rainMachine); rainObj.transform.position=new Vector3(-5,2.7f,-6.35f);
            rainObj.transform.rotation=Quaternion.Euler(90,0,0);
            var rain = rainObj.AddComponent<ParticleSystem>();
            var main=rain.main; main.startLifetime=.6f; main.startSpeed=3; main.startSize=.035f; main.startColor=new Color(.6f,.92f,.95f); main.playOnAwake=false;
            var emission=rain.emission; emission.rateOverTime=65;
            var shape=rain.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(1,.45f,.1f);
            rainObj.GetComponent<ParticleSystemRenderer>().sharedMaterial=teal;
            rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            // Keep the valve north of the cart's westbound swept volume (z <= 5.7m).
            var valve=Cube("05_Drain_valve",new Vector3(-2.5f,3.65f,6.35f),new Vector3(.6f,1.3f,.6f),wood,architecture).transform;
            Cube("Valve_handle",new Vector3(-2.5f,4.34f,6.35f),new Vector3(.9f,.12f,.15f),copper,valve);
            Pipe(new Vector3(-2.5f,3.2f,6.35f),new Vector3(-2.5f,3.2f,3.1f));

            var cart=new GameObject("06_Sapling_cart").transform;
            cart.position=new Vector3(0,0,-7);
            Cube("Cart_tray",new Vector3(0,.48f,-7),new Vector3(1.4f,.3f,2),wood,cart,false);
            foreach(float x in new[]{-.63f,.63f}) foreach(float z in new[]{-7.7f,-6.3f})
            {
                var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name="Wheel";
                wheel.transform.SetParent(cart); wheel.transform.position=new Vector3(x,.24f,z);
                wheel.transform.rotation=Quaternion.Euler(0,0,90); wheel.transform.localScale=new Vector3(.46f,.08f,.46f);
                wheel.GetComponent<Renderer>().sharedMaterial=dark; UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
            }
            foreach(float z in new[]{-7.55f,-6.5f})
            {
                Cube("Sapling_trunk",new Vector3(0,1.1f,z),new Vector3(.09f,1.2f,.09f),wood,cart,false);
                var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere); crown.name="Sapling_crown"; crown.transform.SetParent(cart);
                crown.transform.position=new Vector3(0,1.7f,z); crown.transform.localScale=new Vector3(.9f,.85f,.85f);
                crown.GetComponent<Renderer>().sharedMaterial=leaf; UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
            }
            var cartCollider=cart.gameObject.AddComponent<BoxCollider>(); cartCollider.center=new Vector3(0,1.1f,0); cartCollider.size=new Vector3(1.4f,2.2f,2);

            // Dock paint is supplementary: geometry still determines every viable connection.
            foreach(var p in new[]{new Vector3(0,.02f,-7),new Vector3(0,3.02f,5),new Vector3(-5.5f,3.02f,0),new Vector3(8,3.02f,5)})
                Cube("Dock_mark",p,new Vector3(1.8f,.025f,2.4f),copper,architecture,false);
            Cube("Destination_receiving_pad",new Vector3(8,3.035f,5),new Vector3(2.5f,.04f,3),teal,architecture,false);
            // Workshop courtyard, roof terraces and distant utility spine establish the city scale.
            for(int i=0;i<5;i++) House(new Vector3(-13,0,-9+i*5),new Vector3(3,3+i*.4f,4));
            for(int i=0;i<4;i++) House(new Vector3(16,3,1+i*6),new Vector3(4,4+i*.4f,5));
            for(int i=0;i<5;i++) House(new Vector3(-9+i*5,6+i%2*3,18+i%2*6),new Vector3(4,4,4));
            Cube("Mountain_foundation",new Vector3(0,-3,12),new Vector3(42,4,55),dark,architecture);
            for(int i=0;i<4;i++) Cube("Mountain_terrace",new Vector3(-8+i*6,4+i*3,32+i*5),new Vector3(22,12+i*2,15),stone,architecture);
            House(new Vector3(3,21,42),new Vector3(8,5,7));
            Pipe(new Vector3(14,6,4),new Vector3(14,6,29));
            Pipe(new Vector3(14,6,29),new Vector3(14,18,29));
            for(int i=0;i<6;i++) Cube("Utility_repeater",new Vector3(14.4f,6+i*2,29),new Vector3(.8f,.2f,.5f),teal,architecture,false);

            var cameraObject=new GameObject("Camera_Main");
            var camera=cameraObject.AddComponent<Camera>(); camera.tag="MainCamera";
            camera.orthographic=true; camera.orthographicSize=16.5f;
            camera.transform.position=new Vector3(23,27,-29); camera.transform.LookAt(new Vector3(1,1,1));
            camera.nearClipPlane=.1f; camera.farClipPlane=180;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.42f,.52f,.54f);
            cameraObject.AddComponent<AudioListener>(); cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var lightObj=new GameObject("Sun_soft_key"); var sun=lightObj.AddComponent<Light>(); sun.type=LightType.Directional;
            sun.intensity=1.8f; sun.color=new Color(1,.86f,.70f); sun.shadows=LightShadows.Soft;
            lightObj.transform.rotation=Quaternion.Euler(47,-28,0); RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.55f,.65f,.69f);
            RenderSettings.fog=true; RenderSettings.fogColor=camera.backgroundColor; RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogStartDistance=48; RenderSettings.fogEndDistance=110;

            var playerObj=new GameObject("07_Apprentice"); var cc=playerObj.AddComponent<CharacterController>();
            cc.height=1.65f; cc.radius=.3f; cc.center=new Vector3(0,.825f,0); cc.stepOffset=.2f; cc.slopeLimit=45;
            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name="Body"; body.transform.SetParent(playerObj.transform,false);
            body.transform.localPosition=new Vector3(0,.825f,0); body.transform.localScale=new Vector3(.55f,.825f,.55f);
            body.GetComponent<Renderer>().sharedMaterial=copper; UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var player=playerObj.AddComponent<CourtPlayer>(); var cameraRig=cameraObject.AddComponent<CourtCamera>();
            var systems=new GameObject("08_Court_runtime"); var court=systems.AddComponent<CourtController>(); var hud=systems.AddComponent<CourtHUD>();
            court.Initialize(lower,upper,cart,waterSurface,rainMachine,valve,player,cameraRig,rain);
            player.Initialize(court,camera,new Vector3(0,.1f,-11)); cameraRig.Initialize(court,player,camera); hud.Initialize(court,player,cameraRig);
            PlayerSettings.companyName="Borrowing Sun Prototype"; PlayerSettings.productName="借晴 - 水院白盒";
            PlayerSettings.defaultScreenWidth=1440; PlayerSettings.defaultScreenHeight=900; PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;
            PrefabUtility.SaveAsPrefabAsset(rainMachine.gameObject,"Assets/BorrowingSun/Prefabs/WeatherPavilion.prefab");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("COURT_SCENE_CREATED "+path);
        }

        private static Material MaterialAsset(string name,Color color,float emission=0)
        {
            string path=Art+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m!=null) return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name}; m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.15f);
            if(emission>0) {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
            AssetDatabase.CreateAsset(m,path); return m;
        }
        private static GameObject Cube(string name,Vector3 position,Vector3 size,Material mat,Transform parent,bool collision=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);g.transform.position=position;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collision) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        private static void Platform(string name,float x0,float x1,float z0,float z1,float y)
        {
            Cube(name,new Vector3((x0+x1)/2,y-.25f,(z0+z1)/2),new Vector3(x1-x0,.5f,z1-z0),stone,architecture);
            if(y>0) foreach(float x in new[]{x0+.25f,x1-.25f}) foreach(float z in new[]{z0+.25f,z1-.25f})
                Cube("Support",new Vector3(x,y/2-.3f,z),new Vector3(.35f,y+.1f,.35f),wood,architecture);
        }
        private static void Post(Vector3 foot) {Cube("Rail_post",foot+Vector3.up*.5f,new Vector3(.16f,1,.16f),wood,architecture);}
        private static void Rail(Vector3 a,Vector3 b)
        {
            var g=Cube("Rail_bar",(a+b)/2,new Vector3(.1f,.12f,Vector3.Distance(a,b)),wood,architecture);g.transform.rotation=Quaternion.LookRotation(b-a);
        }
        private static void Fence(Vector3 a,Vector3 b)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/2));for(int i=0;i<=count;i++) Post(Vector3.Lerp(a,b,(float)i/count));Rail(a+Vector3.up*.9f,b+Vector3.up*.9f);
        }
        private static void Pipe(Vector3 a,Vector3 b)
        {
            var g=Cube("Weather_service_conduit",(a+b)/2,new Vector3(.14f,.14f,Vector3.Distance(a,b)),teal,architecture,false);g.transform.rotation=Quaternion.LookRotation(b-a);
        }
        private static void Roof(string name,Vector3 center,Vector3 size,Transform parent)
        {
            Cube(name,center,size,dark,parent,false);
            Cube("Eave_copper_edge",center+new Vector3(0,0,-size.z/2),new Vector3(size.x+.2f,.10f,.12f),wood,parent,false);
        }
        private static void House(Vector3 foot,Vector3 size)
        {
            Cube("City_workshop",foot+Vector3.up*size.y*.5f,size,stone,architecture);
            Roof("Roof_terrace",foot+Vector3.up*(size.y+.1f),new Vector3(size.x+.7f,.35f,size.z+.7f),architecture);
            for(int i=-1;i<=1;i++) Cube("Workshop_window",foot+new Vector3(i*size.x*.23f,size.y*.55f,-size.z*.5f-.025f),new Vector3(.5f,.8f,.08f),teal,architecture,false);
        }
    }
}
