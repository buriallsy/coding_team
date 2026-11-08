using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace BorrowingSun
{
    // Descending/Ascending retain their serialized numeric values; the surface route never enters them.
    public enum SkywellTravel { Walking, Descending, Swimming, Ascending, Returning, Docked, Climbing, Boarding }
    public enum SkywellClimbPhase { None, Approach, Climbing, StepOff }
    [DefaultExecutionOrder(-90)]
    public sealed class SkywellJourney : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform entryLift, refuge, lureControl;
        [SerializeField, FormerlySerializedAs("pin"), Tooltip("Sunny is collected aboard the boat. Refuge recovery retains it; replay restores it.")]
        private Transform sunnyToken;
        [Header("船桨、靠岸与爬梯")]
        [SerializeField] private Transform paddlePickup, boatDock, ladderBottom, ladderTop, ladderExit;
        [Min(.1f)] public float boatSpeed=4.5f;
        [Header("靠近船 / 任意可达船边上船")]
        [Min(.1f), Tooltip("Maximum horizontal distance from the physical deck edge for E boarding; no facing requirement.")] public float boardingReach=1.25f;
        [Min(.1f), Tooltip("Maximum collision-checked boarding movement speed.")] public float boardingMoveSpeed=3.6f;
        [Min(.1f), Tooltip("Feet clearance above the deck when crossing the visible low gunwale.")] public float boardingClearance=1.0f;
        [Header("靠近梯脚 W / 梯顶 S")]
        [Min(.1f)] public float ladderGrabDistance=1.25f;
        [Min(.1f)] public float ladderGrabHalfWidth=.8f;
        [Min(.1f), Tooltip("Boat acceleration and braking on the existing collision-checked water route.")] public float boatAcceleration=8;
        [Min(.1f), Tooltip("按住 W 上爬 / S 下爬的速度（米/秒），松键原位停住。")] public float climbSpeed=.8f;
        [Min(.1f), Tooltip("Maximum speed when aligning with the ladder or stepping onto the landing.")] public float climbAlignSpeed=3.4f;
        [Min(.03f)] public float climbAlignSmoothTime=.1f;
        [Min(.2f), Tooltip("Release ladder control when real collision prevents progress, without relocating the player.")] public float climbBlockedTimeout=.65f;
        [SerializeField, Tooltip("Water surface height relative to the Skywell origin.")] private float waterSurfaceOffset=-5.5f;
        [SerializeField, Tooltip("Horizontal swimming feet height relative to the origin; the boat floor is one ordinary 0.15 m step above this whitebox waterline.")] private float surfaceSwimOffset=-5.6f;
        [Header("水面路线 / 静态缓坡与开放码头")]
        [SerializeField] private Transform surfaceDock;
        [SerializeField] private Transform[] surfaceRouteWaypoints;
        [SerializeField] private Collider[] surfaceWalkable;
        [SerializeField] private Bounds surfaceRegion;
        [SerializeField, Tooltip("The actual authored water plane. Its visible X/Z bounds define buoyancy, independently of the larger ramp/dock approach region.")]
        private Renderer surfaceWaterBody;
        public Renderer SurfaceWaterBody=>surfaceWaterBody;
        public Bounds SurfaceWaterBounds=>surfaceWaterBody?surfaceWaterBody.bounds:default;
        public void ConfigureSurfaceWaterBody(Renderer waterBody){surfaceWaterBody=waterBody;}
        public bool IsOverSurfaceWater(Vector3 feet)
        {
            if(!surfaceWaterBody||!surfaceWaterBody.enabled||!surfaceWaterBody.gameObject.activeInHierarchy)return false;
            var bounds=surfaceWaterBody.bounds;feet.y=bounds.center.y;
            return bounds.Contains(feet);
        }
        public bool SurfaceConfigured=>surfaceDock&&surfaceRouteWaypoints!=null&&surfaceRouteWaypoints.Length>0&&surfaceWalkable!=null&&surfaceWalkable.Length>0;
        public float SurfaceWaterLevel=>court.Origin.y+waterSurfaceOffset;
        public float SurfaceFeetHeight=>court.Origin.y+surfaceSwimOffset;
        public Transform SurfaceDock=>surfaceDock;
        public Transform[] SurfaceRouteWaypoints=>surfaceRouteWaypoints;
        public Collider[] SurfaceWalkable=>surfaceWalkable;
        public Bounds SurfaceRegion=>surfaceRegion;
        public void ConfigureSurfaceRoute(Transform dock,Transform[] route,Collider[] walkable,Bounds region,float waterWorldY,float feetWorldY)
        {surfaceDock=dock;surfaceRouteWaypoints=route;surfaceWalkable=walkable;surfaceRegion=region;waterSurfaceOffset=waterWorldY-court.Origin.y;surfaceSwimOffset=feetWorldY-court.Origin.y;}
        // Legacy serialized values remain readable for older tools; surface travel never consumes air.
        [HideInInspector]
        [Min(1), InspectorName("氧气容量")] public float airCapacity=45;
        [HideInInspector, Min(.1f), InspectorName("舱内补气速度（秒 / 秒）")] public float airRefillRate=18;
        [HideInInspector, Min(.1f), InspectorName("低氧提醒阈值")] public float lowAirThreshold=15;
        [HideInInspector, Min(.1f), InspectorName("紧急提醒阈值")] public float criticalAirThreshold=7;
        private void OnValidate()
        {
            airCapacity=Mathf.Max(1,airCapacity);airRefillRate=Mathf.Max(.1f,airRefillRate);
            lowAirThreshold=Mathf.Clamp(lowAirThreshold,.1f,airCapacity);criticalAirThreshold=Mathf.Clamp(criticalAirThreshold,.1f,lowAirThreshold);
            boardingReach=Mathf.Max(.1f,boardingReach);boardingMoveSpeed=Mathf.Max(.1f,boardingMoveSpeed);boardingClearance=Mathf.Max(.1f,boardingClearance);
            ladderGrabDistance=Mathf.Max(.1f,ladderGrabDistance);ladderGrabHalfWidth=Mathf.Max(.1f,ladderGrabHalfWidth);
            boatSpeed=Mathf.Max(.1f,boatSpeed);boatAcceleration=Mathf.Max(.1f,boatAcceleration);climbSpeed=Mathf.Max(.1f,climbSpeed);
            climbAlignSpeed=Mathf.Max(.1f,climbAlignSpeed);climbAlignSmoothTime=Mathf.Max(.03f,climbAlignSmoothTime);climbBlockedTimeout=Mathf.Max(.2f,climbBlockedTimeout);
        }

        private Vector3 liftStart;
        public bool LiftCalling=>false;
        public Vector3 UpperLanding=>SurfaceConfigured?surfaceRouteWaypoints[0].position:entryLift.parent.TransformPoint(liftStart);
        public Vector3 LowerLanding=>SurfaceConfigured?surfaceDock.position:UpperLanding-Vector3.up*10;
        [SerializeField] private Transform returnShuttle;
        [SerializeField, Tooltip("Dry feet-position volume inside the boat. Keep the open-front boundary inside its floor lip.")]
        private Bounds returnCabinInterior=new Bounds(new Vector3(0,1.15f,.1f),new Vector3(2.5f,2.6f,2.4f));
        private Vector3 shuttleStart;
        private CharacterController playerBody;
        private readonly RaycastHit[] boatHits=new RaycastHit[48];
        private readonly RaycastHit[] supportHits=new RaycastHit[12];
        private Vector3 climbAlignVelocity;
        private Collider boardingDeck;
        private readonly Collider[] boardingOverlaps=new Collider[24];
        private readonly Vector3[] boardingWaypoints=new Vector3[3];
        private Vector3 boardingVelocity;
        private int boardingWaypoint;
        private float boardingBlockedTime;
        private bool climbNeedsRelease;
        public bool IsBoarding=>Travel==SkywellTravel.Boarding;
        public bool IsClimbing=>Travel==SkywellTravel.Climbing;
        public SkywellClimbPhase ClimbPhase=>!IsClimbing?SkywellClimbPhase.None:climbApproaching?SkywellClimbPhase.Approach:
            climbSteppingOff?SkywellClimbPhase.StepOff:SkywellClimbPhase.Climbing;
        public float ClimbVelocity {get;private set;}
        public float ClimbProgress01=>ladderBottom&&ladderTop?Mathf.Clamp01(Vector3.Dot(court.Player.FeetPosition-ladderBottom.position,
            (ladderTop.position-ladderBottom.position).normalized)/Mathf.Max(.01f,Vector3.Distance(ladderBottom.position,ladderTop.position))):0;
        public Vector3 LadderFacingDirection
        {
            get
            {
                Vector3 direction=ladderExit&&ladderTop?Vector3.ProjectOnPlane(ladderExit.position-ladderTop.position,Vector3.up):Vector3.forward;
                return direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            }
        }
        private float climbBlockedTime,boatVelocity;
        private static readonly Vector3[] FootOffsets={Vector3.left,Vector3.right,Vector3.forward,Vector3.back,
            new Vector3(-.7071f,0,-.7071f),new Vector3(-.7071f,0,.7071f),new Vector3(.7071f,0,-.7071f),new Vector3(.7071f,0,.7071f)};
        private bool climbApproaching,climbSteppingOff,climbFromTop,boatBlocked;
        public Transform ReturnShuttle=>returnShuttle;
        public Transform PaddlePickup=>paddlePickup;
        public Transform BoatDock=>boatDock;
        public Transform LadderBottom=>ladderBottom;
        public Transform LadderTop=>ladderTop;
        public Transform LadderExit=>ladderExit;
        public bool BoatDocked {get;private set;}
        public bool PaddleObtained {get;private set;}
        public void ConfigureReturnShuttle(Transform shuttle){returnShuttle=shuttle;boardingDeck=null;if(shuttle)shuttleStart=shuttle.position;}
        public void ConfigureBoatRoute(Transform paddle,Transform dock,Transform bottom,Transform top,Transform exit)
        {paddlePickup=paddle;boatDock=dock;ladderBottom=bottom;ladderTop=top;ladderExit=exit;}
        private bool surfaceView,surfaceEdgeOverride,previousEdgeProtection;
        public event System.Action LureRequested;
        public event System.Action ResetOccurred;
        public SkywellTravel Travel {get;private set;}
        public bool SunnyTokenObtained {get;private set;}
        public bool HasReturnedToTower {get;private set;}
        public float Air=>airCapacity;
        public float AirCapacity=>airCapacity;
        public int Recoveries {get;private set;}
        // Only the authored basin and approach ramp are exempt from the ordinary tower fall plane.
        public bool IsWaterApproach(Vector3 feet)=>SurfaceConfigured&&surfaceRegion.Contains(feet)&&feet.y<court.Origin.y-.2f&&
            (IsOverSurfaceWater(feet)||SurfaceFloorAt(feet,out _));
        // Docked/Swimming describe progress; neither can grant permanent fall immunity beyond the water or a real floor.
        public bool BelowGroundActive=>ControlsMovement||court&&court.Player&&IsWaterApproach(court.Player.FeetPosition);
        public bool ControlsMovement=>Travel==SkywellTravel.Returning||Travel==SkywellTravel.Climbing||IsBoarding;
        public Transform EntryLift=>entryLift; // Preserved authoring reference: now a non-moving entry anchor.
        public Transform SunnyToken=>sunnyToken;
        public Vector3 RefugePoint=>surfaceDock?surfaceDock.position:refuge.position;
        public Vector3 LurePoint=>court.WindBell?court.WindBell.LurePosition:court.Origin+new Vector3(-18,waterSurfaceOffset+.65f,-26);
        public bool InReturnCabin=>returnShuttle&&court&&court.Player&&returnCabinInterior.Contains(returnShuttle.InverseTransformPoint(court.Player.FeetPosition));
        public bool InRefuge
        {
            get
            {
                if(!court||!court.Player)return false;
                var p=court.Player.FeetPosition;
                return InReturnCabin||BoatDocked&&ladderBottom&&Vector3.Distance(p,ladderBottom.position)<2.6f||
                    surfaceDock&&Mathf.Abs(p.y-surfaceDock.position.y)<.6f&&Mathf.Abs(p.x-surfaceDock.position.x)<3.9f&&Mathf.Abs(p.z-surfaceDock.position.z)<3.9f;
            }
        }
        public bool HasBreathableAir=>true;
        public string Objective=>HasReturnedToTower?null:Travel==SkywellTravel.Climbing?"沿塔侧爬梯上到中心平台。":BoatDocked?"船已靠岸；走到塔侧梯脚，朝爬梯按 W 上爬。":Travel==SkywellTravel.Returning?"沿水面划向中央建筑，在塔侧靠岸。":!PaddleObtained?(BelowGroundActive?"找到两根柱子之间漂浮的船桨；敲钟引开鱼，利用柱后遮挡通过。":"沿西岸缓坡走到水边码头，取回水面上的船桨。"):!SunnyTokenObtained?"靠近船边按 E 上船，晴牌就在船上。":"船桨和晴牌已收好；登船划向中央建筑。";
        public string Status=>"船桨 · "+(PaddleObtained?"已取得":"漂在水面")+"　晴牌 · "+(SunnyTokenObtained?"已取得":"在船上");
        public string CurrentPrompt
        {
            get
            {
                if(Travel==SkywellTravel.Climbing)return "按住 W / ↑ 上爬 · S / ↓ 下爬 · 松开停住";
                if(IsBoarding)return "正在翻过船舷，登上甲板";
                if(ControlsMovement)return "船正沿水面划行；靠岸后从塔侧爬梯上去";
                if(BoatDocked&&ladderBottom&&Near(ladderBottom.position,2))return "朝梯脚按 W / ↑，直接攀爬";
                if(BoatDocked&&ladderExit&&Near(ladderExit.position,1.8f))return "背向梯口按 S / ↓ 下梯 · W / S 上下攀爬";
                if(InReturnCabin&&!SunnyTokenObtained&&sunnyToken&&Near(sunnyToken.position,2.3f))return "E 拿取船上的晴牌";
                if(InReturnCabin&&!BoatDocked)return PaddleObtained&&SunnyTokenObtained?"E 划船到中央建筑":"先取回水面上的船桨，并拿好船上的晴牌";
                if(InRefuge&&lureControl&&Near(lureControl.position,2))return "E 敲钟";
                if(!PaddleObtained&&paddlePickup&&Near(paddlePickup.position,2.1f))return "E 取回船桨";
                if(CanReachBoat(out _))return "E 上船";
                if(Travel==SkywellTravel.Docked)return "船已靠岸；朝前方爬梯按 W 登上中心平台";
                return null;
            }
        }
        public void Initialize(SkywellController owner,Transform lift,Transform token,Transform room,Transform lure)
        {court=owner;entryLift=lift;sunnyToken=token;refuge=room;lureControl=lure;liftStart=lift.localPosition;}
        private void Awake(){if(entryLift)liftStart=entryLift.localPosition;if(returnShuttle)shuttleStart=returnShuttle.position;}
        private void OnDisable()
        {
            if(!court||!court.Player)return;
            if(ControlsMovement)court.Player.enabled=true;
            Travel=SkywellTravel.Walking;surfaceView=false;RestoreEdgeProtection();
            climbApproaching=climbSteppingOff=boatBlocked=false;
            climbAlignVelocity=boardingVelocity=Vector3.zero;climbBlockedTime=boardingBlockedTime=boatVelocity=ClimbVelocity=0;
            court.Player.ConfigureSwimming(false);court.Player.OxygenUrgency=0;
            if(court.CameraRig)court.CameraRig.SetJourneyView(false);
        }
        public bool Near(Vector3 p,float distance)=>Vector3.Distance(court.Player.FeetPosition,p)<distance;
        private void Update()
        {
            if(!court||!court.Player)return;
            if(ControlsMovement&&Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame){court.ResetCourt();return;}
            court.Player.OxygenUrgency=0;
            if(IsBoarding){TickBoarding(Time.deltaTime);return;}
            if(Travel==SkywellTravel.Returning){TickBoat(Time.deltaTime);return;}
            if(Travel==SkywellTravel.Climbing)
            {
                var keyboard=Keyboard.current;
                float axis=keyboard==null?0:(keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed?1:0);
                TickClimb(court.ObservationInputBlocked||court.WeatherInputBlocked?0:axis,Time.deltaTime);return;
            }
            TickSurfaceMovement();
            var input=Keyboard.current;
            float forward=input==null?0:(input.wKey.isPressed||input.upArrowKey.isPressed?1:0)-(input.sKey.isPressed||input.downArrowKey.isPressed?1:0);
            if(Mathf.Abs(forward)<.01f)climbNeedsRelease=false;
            bool lateral=input!=null&&(input.aKey.isPressed||input.dKey.isPressed||input.leftArrowKey.isPressed||input.rightArrowKey.isPressed);
            if(Application.isFocused&&!lateral&&(input==null||!input.eKey.isPressed)&&court.CameraRig)
                TryAutoClimb(forward,court.CameraRig.transform.forward);
        }
        private void TickSurfaceMovement()
        {
            if(!SurfaceConfigured)return;
            Vector3 feet=court.Player.FeetPosition;
            bool inRegion=IsWaterApproach(feet);
            bool waterLevel=inRegion&&feet.y<=SurfaceFeetHeight+.5f;
            if(waterLevel&&!surfaceEdgeOverride)
            {previousEdgeProtection=court.Player.PuzzleEdgeProtectionEnabled;surfaceEdgeOverride=true;court.Player.ConfigurePuzzleEdgeProtection(false);}
            else if(!waterLevel)RestoreEdgeProtection();
            // On a high ramp the ordinary CharacterController still owns falling and walking.
            // Buoyancy begins only as the feet actually reach the surface; it never pulls a player down from a ramp.
            bool swimming=inRegion&&IsOverSurfaceWater(feet)&&feet.y<=SurfaceFeetHeight+.18f&&!HasSurfaceSupport(feet);
            if(swimming)
            {
                Travel=SkywellTravel.Swimming;
                if(!court.Player.IsSurfaceSwimming)court.Player.ConfigureSurfaceSwimming(true,SurfaceFeetHeight);
            }
            else
            {
                if(court.Player.IsSwimming)court.Player.ConfigureSwimming(false);
                Travel=BoatDocked&&inRegion?SkywellTravel.Docked:SkywellTravel.Walking;
            }
            if(surfaceView!=inRegion)
            {
                surfaceView=inRegion;
                court.CameraRig.SetJourneyView(inRegion,new Vector3(9,10,-12),9);
            }
        }
        private void RestoreEdgeProtection()
        {
            if(!surfaceEdgeOverride||!court||!court.Player)return;
            court.Player.ConfigurePuzzleEdgeProtection(previousEdgeProtection);surfaceEdgeOverride=false;
        }
        private bool HasSurfaceSupport(Vector3 feet)
        {
            // Explicit dry floors only: the basin bottom and cover pillars cannot turn open water into dry land.
            if(SurfaceFloorAt(feet,out _))return true;
            if(!playerBody)playerBody=court.Player.GetComponent<CharacterController>();
            if(!playerBody)return false;
            // Only the swim/walk handoff uses footprint support. The fall exemption above requires
            // centre support, so a capsule touching a dry ledge cannot record a checkpoint beyond it.
            // The capsule can already rest on a lip before its centre passes the edge. Sample only
            // the actual foot footprint, and reject samples reached through a solid hull or wall.
            float radius=Mathf.Max(.05f,playerBody.radius-playerBody.skinWidth*2);
            foreach(var offset in FootOffsets)
            {
                Vector3 sample=feet+offset*radius;
                if(!SurfaceFloorAt(sample,out var floor)||floor.point.y>feet.y+playerBody.stepOffset+.02f)continue;
                Vector3 from=feet+Vector3.up*.35f;
                Vector3 to=new Vector3(sample.x,Mathf.Max(feet.y,floor.point.y)+.35f,sample.z),delta=to-from;
                int count=Physics.RaycastNonAlloc(from,delta.normalized,supportHits,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count==supportHits.Length)continue;
                bool blocked=false;
                for(int i=0;i<count;i++)if(!supportHits[i].transform.IsChildOf(court.Player.transform)){blocked=true;break;}
                if(!blocked)return true;
            }
            return false;
        }
        private bool SurfaceFloorAt(Vector3 feet,out RaycastHit support)
        {
            support=default;var ray=new Ray(feet+Vector3.up*.26f,Vector3.down);
            foreach(var floor in surfaceWalkable)
                if(floor&&floor.enabled&&floor.gameObject.activeInHierarchy&&floor.Raycast(ray,out var hit,.56f)&&hit.normal.y>.55f)
                {support=hit;return true;}
            return false;
        }
        private void TickBoat(float delta)
        {
            Vector3 target=new Vector3(boatDock.position.x,shuttleStart.y,boatDock.position.z);
            float remaining=Vector3.Distance(returnShuttle.position,target);
            float desiredSpeed=Mathf.Min(boatSpeed,Mathf.Sqrt(2*Mathf.Max(.1f,boatAcceleration)*remaining));
            boatVelocity=Mathf.MoveTowards(boatVelocity,desiredSpeed,Mathf.Max(.1f,boatAcceleration)*delta);
            Vector3 step=Vector3.MoveTowards(returnShuttle.position,target,boatVelocity*delta)-returnShuttle.position;
            if(!BoatPathClear(step,Vector3.Distance(returnShuttle.position+step,target)<.001f))
            {
                if(!boatBlocked)court.ShowMessage("前方停靠路线被挡住，船已停下。R 可重置。",5);
                boatBlocked=true;boatVelocity=0;return;
            }
            boatBlocked=false;returnShuttle.position+=step;court.Player.CarryByPlatform(step,0);Physics.SyncTransforms();
            if(Vector3.Distance(returnShuttle.position,target)>.01f)return;
            BoatDocked=true;Travel=SkywellTravel.Docked;court.Player.enabled=true;court.Player.ConfigureSwimming(false);
            boatVelocity=0;court.ShowMessage("船已靠岸。朝塔侧梯脚按 W 直接上爬，S 可下爬。",6);
        }
        private bool BoatPathClear(Vector3 step,bool dockingStep)
        {
            if(step.sqrMagnitude<.000001f)return true;
            Vector3 center=returnShuttle.TransformPoint(new Vector3(0,1.4f,.025f));
            int count=Physics.BoxCastNonAlloc(center,new Vector3(1.55f,1.7f,1.525f),step.normalized,boatHits,returnShuttle.rotation,step.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==boatHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                Transform hit=boatHits[i].transform;
                if(hit.IsChildOf(returnShuttle)||hit.IsChildOf(court.Player.transform))continue;
                // The open bow may meet the dock at the exact route endpoint; no overlap is crossed.
                if(dockingStep&&boatHits[i].distance>=step.magnitude-.001f)continue;
                return false;
            }
            return true;
        }
        public bool TryPrimary()
        {
            if(ControlsMovement)return true;

            if(InReturnCabin&&!SunnyTokenObtained&&sunnyToken&&Near(sunnyToken.position,2.3f))
            {SunnyTokenObtained=true;sunnyToken.gameObject.SetActive(false);court.ShowMessage("已拿到晴牌；带上船桨就能划到中央建筑。",5);return true;}
            if(InReturnCabin&&!BoatDocked)
            {
                if(!PaddleObtained||!SunnyTokenObtained){court.ShowMessage(!PaddleObtained?"还缺船桨：它漂在两根柱子之间。":"先拿好船上的晴牌。",5);return true;}
                if(!boatDock||!ladderBottom||!ladderTop||!ladderExit){court.ShowMessage("船的停靠点尚未配置。",4);return true;}
                court.Player.OxygenUrgency=0;court.Player.ConfigureSwimming(false);
                court.Player.enabled=false;Travel=SkywellTravel.Returning;boatBlocked=false;boatVelocity=0;return true;
            }
            if(InRefuge&&lureControl&&Near(lureControl.position,2)){LureRequested?.Invoke();return true;}
            if(!PaddleObtained&&paddlePickup&&Near(paddlePickup.position,2.1f))
            {PaddleObtained=true;paddlePickup.gameObject.SetActive(false);court.ShowMessage("已取回船桨。靠近任意可达船边按 E 上船，晴牌就在船上。",5);return true;}
            if(CanReachBoat(out var landing)){StartBoarding(landing);return true;}
            return BelowGroundActive;
        }
        private bool CanReachBoat(out Vector3 landing)
        {
            landing=default;
            if(!returnShuttle||InReturnCabin||ControlsMovement||!court||!court.Player)return false;
            if(!boardingDeck)
            {
                var floor=returnShuttle.Find("Shuttle_floor");
                if(floor)boardingDeck=floor.GetComponent<Collider>();
            }
            if(!boardingDeck||!boardingDeck.enabled||!boardingDeck.gameObject.activeInHierarchy)return false;
            if(!playerBody)playerBody=court.Player.GetComponent<CharacterController>();
            if(!playerBody||!playerBody.enabled||court.Player.IsExternallyDriven||court.Player.IsDropping)return false;
            var bounds=boardingDeck.bounds;var feet=court.Player.FeetPosition;
            if(Mathf.Abs(feet.y-bounds.max.y)>1.25f)return false;
            Vector3 nearest=new Vector3(Mathf.Clamp(feet.x,bounds.min.x,bounds.max.x),feet.y,Mathf.Clamp(feet.z,bounds.min.z,bounds.max.z));
            if(Vector3.Distance(feet,nearest)>boardingReach)return false;
            // Leave enough real deck under the entire capsule after crossing any of the four sides.
            float inset=playerBody.radius+.65f;
            if(bounds.size.x<=inset*2||bounds.size.z<=inset*2)return false;
            landing=new Vector3(Mathf.Clamp(feet.x,bounds.min.x+inset,bounds.max.x-inset),bounds.max.y+.035f,
                Mathf.Clamp(feet.z,bounds.min.z+inset,bounds.max.z-inset));
            var ray=new Ray(landing+Vector3.up*.2f,Vector3.down);
            return boardingDeck.Raycast(ray,out var hit,.3f)&&hit.normal.y>.8f;
        }
        private void StartBoarding(Vector3 landing)
        {
            if(court.ObservationInputBlocked||court.WeatherInputBlocked)return;
            Vector3 start=court.Player.FeetPosition;
            float clearance=Mathf.Max(start.y,landing.y)+boardingClearance;
            boardingWaypoints[0]=new Vector3(start.x,clearance,start.z);
            boardingWaypoints[1]=new Vector3(landing.x,clearance,landing.z);
            boardingWaypoints[2]=landing;
            Vector3 previous=start;
            foreach(var target in boardingWaypoints)
            {
                if(!BoardingSegmentClear(previous,target))
                {court.ShowMessage("这里的上船路线被挡住了，靠近能翻过的船边再试。",3);return;}
                previous=target;
            }
            RestoreEdgeProtection();court.Player.ConfigureSwimming(false);court.Player.enabled=false;
            Travel=SkywellTravel.Boarding;boardingWaypoint=0;boardingBlockedTime=0;boardingVelocity=Vector3.zero;
        }
        private void BoardingCapsule(Vector3 feet,out Vector3 lower,out Vector3 upper,out float radius)
        {
            radius=Mathf.Max(.05f,playerBody.radius-playerBody.skinWidth);
            Vector3 center=feet+playerBody.center;
            float half=Mathf.Max(0,playerBody.height*.5f-playerBody.radius);
            lower=center-Vector3.up*half;upper=center+Vector3.up*half;
        }
        private bool BoardingSegmentClear(Vector3 from,Vector3 to)
        {
            BoardingCapsule(from,out var lower,out var upper,out float radius);
            Vector3 delta=to-from;int count=0;
            if(delta.sqrMagnitude>.000001f)
            {
                count=Physics.CapsuleCastNonAlloc(lower,upper,radius,delta.normalized,boatHits,delta.magnitude,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count==boatHits.Length)return false;
                for(int i=0;i<count;i++)if(!boatHits[i].transform.IsChildOf(court.Player.transform))return false;
            }
            BoardingCapsule(to,out lower,out upper,out radius);
            count=Physics.OverlapCapsuleNonAlloc(lower,upper,radius,boardingOverlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==boardingOverlaps.Length)return false;
            for(int i=0;i<count;i++)if(!boardingOverlaps[i].transform.IsChildOf(court.Player.transform))return false;
            return true;
        }
        private void TickBoarding(float delta)
        {
            Vector3 feet=court.Player.FeetPosition,target=boardingWaypoints[boardingWaypoint];
            Vector3 next=Vector3.SmoothDamp(feet,target,ref boardingVelocity,.08f,boardingMoveSpeed,delta);
            Vector3 step=next-feet;
            // Recheck the swept capsule after authoring/preflight: a moving obstacle may enter the route.
            bool clear=BoardingSegmentClear(feet,next);
            if(clear)playerBody.Move(step);
            float advance=step.sqrMagnitude>.000001f?Vector3.Dot(court.Player.FeetPosition-feet,step.normalized):0;
            if(!clear||step.sqrMagnitude>.00000001f&&advance<step.magnitude*.15f)boardingBlockedTime+=delta;
            else boardingBlockedTime=0;
            if(boardingBlockedTime>=climbBlockedTimeout)
            {
                Travel=BoatDocked?SkywellTravel.Docked:SkywellTravel.Walking;court.Player.enabled=true;
                boardingVelocity=Vector3.zero;boardingBlockedTime=0;
                court.ShowMessage("上船途中遇到阻挡，已停在原处。",3);return;
            }
            if(Vector3.Distance(court.Player.FeetPosition,target)>.045f)return;
            if(++boardingWaypoint<boardingWaypoints.Length){boardingVelocity=Vector3.zero;return;}
            Travel=BoatDocked?SkywellTravel.Docked:SkywellTravel.Walking;court.Player.enabled=true;
            boardingVelocity=Vector3.zero;boardingBlockedTime=0;
            court.ShowMessage(!SunnyTokenObtained?"已上船。按 E 拿取船上的晴牌。":!PaddleObtained?"已上船；还需要取回水面上的船桨。":BoatDocked?"船已靠岸；从前方梯脚按 W 上爬。":"已上船。按 E 划向中央建筑。",4);
        }
        private bool TryAutoClimb(float forwardAxis,Vector3 cameraForward)
        {
            if(!BoatDocked||!ladderBottom||!ladderTop||!ladderExit||climbNeedsRelease||ControlsMovement
                ||!court.Player.enabled||court.IsObserving||court.Player.IsExternallyDriven||court.Player.IsDropping)return false;
            Vector3 view=Vector3.ProjectOnPlane(cameraForward,Vector3.up).normalized;
            Vector3 feet=court.Player.FeetPosition,facing=LadderFacingDirection;
            Vector3 right=Vector3.Cross(Vector3.up,facing);
            Vector3 fromBottom=feet-ladderBottom.position;
            float front=Vector3.Dot(fromBottom,-facing);
            if(forwardAxis>.5f&&Mathf.Abs(fromBottom.y)<.7f&&front>=-.1f&&front<=ladderGrabDistance
                &&Mathf.Abs(Vector3.Dot(fromBottom,right))<=ladderGrabHalfWidth&&Vector3.Dot(view,facing)>.35f)
            {StartClimb(false);return true;}
            Vector3 fromTop=feet-ladderTop.position;
            float landingSide=Vector3.Dot(fromTop,facing);
            if(forwardAxis<-.5f&&Mathf.Abs(fromTop.y)<.6f&&landingSide>=-.1f&&landingSide<=Vector3.Distance(ladderExit.position,ladderTop.position)+.3f
                &&Mathf.Abs(Vector3.Dot(fromTop,right))<=ladderGrabHalfWidth&&Vector3.Dot(view,facing)>.35f)
            {StartClimb(true);return true;}
            return false;
        }
        private void StartClimb(bool fromTop)
        {
            if(!ladderBottom||!ladderTop||!ladderExit)return;
            if(!playerBody)playerBody=court.Player.GetComponent<CharacterController>();
            RestoreEdgeProtection();court.Player.ConfigureSwimming(false);court.Player.OxygenUrgency=0;court.Player.enabled=false;
            Travel=SkywellTravel.Climbing;climbFromTop=fromTop;climbApproaching=true;climbSteppingOff=false;ClimbVelocity=0;
            climbAlignVelocity=Vector3.zero;climbBlockedTime=0;
        }
        private void TickClimb(float axis,float delta)
        {
            ClimbVelocity=0;
            Vector3 feet=court.Player.FeetPosition;
            if(climbApproaching)
            {
                Vector3 target=climbFromTop?ladderTop.position:ladderBottom.position;
                if(Vector3.Distance(feet,target)>.045f)
                {MoveClimbTransition(target,delta);return;}
                climbApproaching=false;climbAlignVelocity=Vector3.zero;
            }
            if(climbSteppingOff)
            {
                MoveClimbTransition(ladderExit.position,delta);
                if(Travel!=SkywellTravel.Climbing)return;
                if(Vector3.Distance(court.Player.FeetPosition,ladderExit.position)<.06f)
                {
                    Travel=SkywellTravel.Walking;HasReturnedToTower=true;court.Player.enabled=true;climbNeedsRelease=true;ClimbVelocity=0;
                    climbSteppingOff=false;climbAlignVelocity=Vector3.zero;
                    court.ShowMessage("已登上中心平台；晴牌可带到向阳山的采光装置使用。",5);
                }
                return;
            }
            if(Mathf.Abs(axis)<.01f){climbBlockedTime=0;return;}
            Vector3 direction=(ladderTop.position-ladderBottom.position).normalized;
            float length=Vector3.Distance(ladderBottom.position,ladderTop.position);
            float progress=Mathf.Clamp(Vector3.Dot(feet-ladderBottom.position,direction)+axis*climbSpeed*delta,0,length);
            Vector3 targetFeet=ladderBottom.position+direction*progress;
            if(!MoveClimbWithCollision(targetFeet-feet,delta))return;
            float actual=Vector3.Dot(court.Player.FeetPosition-ladderBottom.position,direction);
            if(axis>0&&actual>=length-.06f)climbSteppingOff=true;
            else if(axis<0&&actual<=.06f){Travel=SkywellTravel.Docked;court.Player.enabled=true;climbNeedsRelease=true;ClimbVelocity=0;}
        }
        private void MoveClimbTransition(Vector3 target,float delta)
        {
            Vector3 feet=court.Player.FeetPosition;
            Vector3 next=Vector3.SmoothDamp(feet,target,ref climbAlignVelocity,climbAlignSmoothTime,climbAlignSpeed,delta);
            MoveClimbWithCollision(next-feet,delta);
        }
        private bool MoveClimbWithCollision(Vector3 displacement,float delta)
        {
            Vector3 before=court.Player.FeetPosition;playerBody.Move(displacement);
            ClimbVelocity=delta>0?Vector3.Dot(court.Player.FeetPosition-before,(ladderTop.position-ladderBottom.position).normalized)/delta:0;
            float requested=displacement.magnitude;
            float advanced=requested>.00001f?Vector3.Dot(court.Player.FeetPosition-before,displacement/requested):0;
            if(requested>.0001f&&advanced<requested*.15f)climbBlockedTime+=delta;
            else climbBlockedTime=0;
            if(climbBlockedTime<climbBlockedTimeout)return true;
            climbAlignVelocity=Vector3.zero;climbBlockedTime=0;climbApproaching=climbSteppingOff=false;
            Travel=BoatDocked?SkywellTravel.Docked:SkywellTravel.Walking;climbNeedsRelease=true;ClimbVelocity=0;
            court.Player.ConfigureSwimming(false);court.Player.enabled=true;
            court.ShowMessage("梯口被挡住了，松开方向键，挪开一点再朝梯脚按 W。",3);
            return false;
        }
        public bool TrySecondary()=>BelowGroundActive;
        public void RecoverToRefuge(string reason)
        {
            if(!BelowGroundActive)return;
            Recoveries++;Travel=SkywellTravel.Walking;court.Player.enabled=true;court.Player.ConfigureSwimming(false);court.Player.Teleport(RefugePoint+Vector3.up*.05f);
            court.ShowMessage(reason,6);court.Player.OxygenUrgency=0;court.CameraRig.SetJourneyView(true,new Vector3(9,10,-12),9);
            climbApproaching=climbSteppingOff=false;climbAlignVelocity=boardingVelocity=Vector3.zero;climbBlockedTime=boardingBlockedTime=boatVelocity=ClimbVelocity=0;
        }
        public void ResetJourney()
        {
            SunnyTokenObtained=PaddleObtained=BoatDocked=HasReturnedToTower=climbNeedsRelease=false;Recoveries=0;Travel=SkywellTravel.Walking;
            RestoreEdgeProtection();court.Player.OxygenUrgency=0;
            if(sunnyToken)sunnyToken.gameObject.SetActive(true);if(paddlePickup)paddlePickup.gameObject.SetActive(true);
            if(returnShuttle)returnShuttle.position=shuttleStart;
            climbApproaching=climbSteppingOff=boatBlocked=surfaceView=false;
            climbAlignVelocity=boardingVelocity=Vector3.zero;climbBlockedTime=boardingBlockedTime=boatVelocity=ClimbVelocity=0;
            court.Player.enabled=true;court.Player.ConfigureSwimming(false);court.CameraRig.SetJourneyView(false);court.Player.ResetPlayer();
            ResetOccurred?.Invoke();
        }
    }
}
