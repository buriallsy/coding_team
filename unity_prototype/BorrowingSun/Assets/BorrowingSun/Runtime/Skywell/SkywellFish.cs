using UnityEngine;

namespace BorrowingSun
{
    public enum SkywellFishPhase { Patrol, Investigate, Warning, Chase }
    [DefaultExecutionOrder(-80)]
    public sealed class SkywellFish : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private SkywellJourney journey;
        [SerializeField] private Transform fish, warning;
        [Header("水面巡游")]
        [SerializeField] private bool surfaceWaterConfigured;
        [SerializeField, Tooltip("Water level relative to the court origin; configured by the surface-basin revision.")]
        private float surfaceWaterOffset = -5.5f;
        [SerializeField, Min(.05f), Tooltip("Fish centre below the visible water surface.")]
        private float surfaceDepth = .35f;
        [Header("引诱（秒，从敲钟起计，含游向钟的时间）")]
        [Range(.5f,30f), InspectorName("钟声吸引时长"), Tooltip("从敲钟开始计时，包含游过去和停留的时间。重复敲钟重新计时；Play Mode 修改立即影响本次计时。")]
        public float lureDuration=8f;
        [Header("速度（米 / 秒）")]
        [Min(.1f), InspectorName("巡游速度")] public float patrolSpeed=1.35f;
        [Min(.1f), InspectorName("游向钟声速度")] public float investigateSpeed=2.3f;
        [Min(.1f), InspectorName("追逐速度")] public float chaseSpeed=3.55f;
        [Min(1f), InspectorName("转向速度（度 / 秒）")] public float turnSpeed=150f;
        [Header("发现与追逐")]
        [Min(.1f), InspectorName("发现距离（米）")] public float detectionRange=7f;
        [Range(1,360), InspectorName("视野总角度（度）")] public float fieldOfView=162.75f;
        [Min(.05f), InspectorName("发现后警觉时间（秒）")] public float warningDuration=1.3f;
        [Min(.05f), InspectorName("失去视线后放弃时间（秒）")] public float lostSightDuration=1.4f;
        [Min(.01f), InspectorName("警觉消退速度"), Tooltip("每秒减少多少秒的警觉积累。")]
        public float warningDecay=2f;
        [Min(.1f), InspectorName("捕获距离（米）")] public float catchDistance=1.15f;
        private Vector3[] route,nodes;
        private readonly RaycastHit[] hits=new RaycastHit[32];
        private readonly float[] costs=new float[10];
        private readonly int[] previous=new int[10];
        private readonly bool[] visited=new bool[10];
        private int patrolIndex,recoveries;
        private float warningTime,lostTime,nextPath;
        private float lureStartedAt=float.NegativeInfinity;
        private Vector3 investigate,hop;
        private SkywellWindBell windBell;
        private Renderer[] skin;private MaterialPropertyBlock tint;
        public float WarningProgress=>Mathf.Clamp01(warningTime/Mathf.Max(.05f,warningDuration));
        public float LureRemaining=>Phase==SkywellFishPhase.Investigate?Mathf.Max(0,lureDuration-(Time.time-lureStartedAt)):0;
        public bool SurfaceWaterConfigured=>surfaceWaterConfigured;
        public float PatrolHeight=>(court?court.Origin.y:0)+(surfaceWaterConfigured?surfaceWaterOffset-Mathf.Max(.05f,surfaceDepth):-9);
        public void ConfigureSurfaceWater(float waterWorldY)
        {
            if(!court)throw new System.InvalidOperationException("Initialize the fish's court before configuring its water level.");
            if(float.IsNaN(waterWorldY)||float.IsInfinity(waterWorldY))throw new System.ArgumentOutOfRangeException(nameof(waterWorldY));
            surfaceWaterConfigured=true;surfaceWaterOffset=waterWorldY-court.Origin.y;
            route=nodes=null;ResetFish();
        }
        private void OnValidate()
        {
            lureDuration=Mathf.Max(.1f,lureDuration);patrolSpeed=Mathf.Max(.1f,patrolSpeed);investigateSpeed=Mathf.Max(.1f,investigateSpeed);
            chaseSpeed=Mathf.Max(.1f,chaseSpeed);turnSpeed=Mathf.Max(1,turnSpeed);
            detectionRange=Mathf.Max(.1f,detectionRange);fieldOfView=Mathf.Clamp(fieldOfView,1,360);warningDuration=Mathf.Max(.05f,warningDuration);
            lostSightDuration=Mathf.Max(.05f,lostSightDuration);warningDecay=Mathf.Max(.01f,warningDecay);catchDistance=Mathf.Max(.1f,catchDistance);
            surfaceDepth=Mathf.Max(.05f,surfaceDepth);route=nodes=null;
        }
        public float CurrentSpeed {get;private set;}
        public SkywellFishPhase Phase {get;private set;}
        public Transform Body=>fish;
        public int LureCount {get;private set;}
        public int Chases {get;private set;}
        public bool PlayerVisible {get;private set;}
        public string Status=>Phase==SkywellFishPhase.Warning?"鱼正在警觉：绕到柱后躲开视线":Phase==SkywellFishPhase.Chase?"鱼在追来：利用柱子遮挡，游回岸边":Phase==SkywellFishPhase.Investigate?"鱼正被水面钟声吸引":"鱼正在巡游；先在岸边确认方向";
        public void Initialize(SkywellController c,SkywellJourney j,Transform model,Transform alert)
        {court=c;journey=j;fish=model;warning=alert;skin=null;route=nodes=null;EnsureRuntimeData();}
        private void Awake()=>EnsureRuntimeData();
        // Play-mode script reload can discard nonserialized rendering caches without repeating Awake.
        private void EnsureRuntimeData()
        {
            if(!windBell)windBell=GetComponent<SkywellWindBell>();
            if(tint==null)tint=new MaterialPropertyBlock();
            if(skin==null&&fish)skin=fish.GetComponentsInChildren<Renderer>();
            if(route!=null||!court)return;
            var o=court.Origin;
            float y=PatrolHeight-o.y;
            route=new[]{o+new Vector3(-11,y,-35),o+new Vector3(-11,y,-24),o+new Vector3(-3,y,-24),o+new Vector3(-3,y,-35)};
            nodes=new[]{route[0],route[1],route[2],route[3],o+new Vector3(-18,y,-26),o+new Vector3(-11,y,-18),o+new Vector3(2,y,-26),o+new Vector3(1,y,-35)};
        }
        private void OnEnable(){EnsureRuntimeData();if(journey){journey.LureRequested-=Lure;journey.ResetOccurred-=ResetFish;journey.LureRequested+=Lure;journey.ResetOccurred+=ResetFish;}}
        private void OnDisable(){if(journey){journey.LureRequested-=Lure;journey.ResetOccurred-=ResetFish;}}
        public void ResetFish()
        {EnsureRuntimeData();if(route==null||!fish||!journey)return;Phase=SkywellFishPhase.Patrol;fish.position=route[0];fish.rotation=Quaternion.LookRotation(Vector3.forward);patrolIndex=1;warningTime=lostTime=nextPath=0;lureStartedAt=float.NegativeInfinity;LureCount=Chases=0;CurrentSpeed=0;PlayerVisible=false;recoveries=journey.Recoveries;if(warning)warning.gameObject.SetActive(false);RefreshSkin();}
        public event System.Action<Vector3> LureSounded;
        public Vector3 InvestigationPoint=>investigate;
        public void Lure(){LureAt(windBell?windBell.LurePosition:journey.LurePoint);}
        public void LureAt(Vector3 point){LureCount++;investigate=point;lureStartedAt=Time.time;Phase=SkywellFishPhase.Investigate;nextPath=0;LureSounded?.Invoke(point);}
        private void Update()
        {
            EnsureRuntimeData();
            if(!court||!journey||!fish||route==null)return;
            if(journey.Recoveries!=recoveries){recoveries=journey.Recoveries;Phase=SkywellFishPhase.Patrol;warningTime=lostTime=0;}
            // Surface swimming exposes the lower capsule beside the fish. Sample that actual
            // body point so the old head-height offset cannot make contact geometrically impossible.
            Vector3 player=court.Player.FeetPosition+Vector3.up*(surfaceWaterConfigured?.35f:.9f);
            Vector3 delta=player-fish.position;float distance=delta.magnitude;
            bool eligible=journey.Travel==SkywellTravel.Swimming&&!journey.InRefuge;
            PlayerVisible=eligible&&distance<detectionRange&&Vector3.Dot(fish.forward,delta.normalized)>Mathf.Cos(fieldOfView*.5f*Mathf.Deg2Rad)&&Clear(fish.position,player,.08f);
            if(!eligible&&(Phase==SkywellFishPhase.Chase||Phase==SkywellFishPhase.Warning)){Phase=SkywellFishPhase.Patrol;warningTime=0;}
            if(PlayerVisible)
            {
                lostTime=0;
                if(Phase!=SkywellFishPhase.Chase){Phase=SkywellFishPhase.Warning;warningTime+=Time.deltaTime;if(warningTime>warningDuration){Phase=SkywellFishPhase.Chase;Chases++;}}
            }
            else
            {
                warningTime=Mathf.Max(0,warningTime-Time.deltaTime*warningDecay);lostTime+=Time.deltaTime;
                if((Phase==SkywellFishPhase.Warning||Phase==SkywellFishPhase.Chase)&&lostTime>lostSightDuration)Phase=SkywellFishPhase.Patrol;
            }
            if(eligible&&Phase==SkywellFishPhase.Chase&&distance<catchDistance)
            {journey.RecoverToRefuge("被鱼追上：已回到岸边；已取得的船桨和晴牌保留。");Phase=SkywellFishPhase.Patrol;return;}
            Vector3 target;float speed;
            if(Phase==SkywellFishPhase.Warning){target=player;speed=0;}
            else if(Phase==SkywellFishPhase.Chase){target=player;speed=chaseSpeed;}
            else if(Phase==SkywellFishPhase.Investigate&&LureRemaining>0){target=investigate;speed=investigateSpeed;}
            else {Phase=SkywellFishPhase.Patrol;target=route[patrolIndex];speed=patrolSpeed;if(Vector3.Distance(fish.position,target)<.5f)patrolIndex=(patrolIndex+1)%route.Length;}
            CurrentSpeed=speed;
            // Sound remains at the real bell, and sight/capture still use the real swimmer.
            // Only the navigation target is projected onto the configured surface patrol layer.
            target.y=surfaceWaterConfigured?PatrolHeight:Mathf.Clamp(target.y,court.Origin.y-10.5f,court.Origin.y-7.4f);
            Vector3 direction=target-fish.position;
            if(speed>0&&direction.magnitude>.3f)
            {
                if(Clear(fish.position,target,.6f))hop=target;
                else if(Time.time>nextPath){hop=FindHop(target);nextPath=Time.time+.35f;}
                direction=hop-fish.position;
                Vector3 step=Vector3.ClampMagnitude(direction,speed*Time.deltaTime);
                if(Clear(fish.position,fish.position+step,.6f))fish.position+=step;
            }
            if(direction.sqrMagnitude>.02f)fish.rotation=Quaternion.RotateTowards(fish.rotation,Quaternion.LookRotation(direction.normalized),turnSpeed*Time.deltaTime);
            if(warning)
            {
                warning.gameObject.SetActive(Phase==SkywellFishPhase.Warning||Phase==SkywellFishPhase.Chase);
                warning.position=fish.position+Vector3.up*1.3f;
                warning.localScale=Vector3.one*(Phase==SkywellFishPhase.Chase?.65f:.3f+.3f*WarningProgress);
            }
            RefreshSkin();
        }
        private void RefreshSkin()
        {
            EnsureRuntimeData();
            tint.SetColor("_BaseColor",Phase==SkywellFishPhase.Chase?new Color(.95f,.22f,.08f):Phase==SkywellFishPhase.Warning?new Color(1,.65f,.1f):new Color(.23f,.38f,.4f));
            if(skin!=null)foreach(var r in skin)if(r)r.SetPropertyBlock(tint);
        }
        // Collision casts constrain both sight and navigation; glass walls block the fish too.
        public bool Clear(Vector3 a,Vector3 b,float radius)
        {
            var v=b-a;float length=v.magnitude;if(length<.001f)return true;
            int count=Physics.SphereCastNonAlloc(a,radius,v/length,hits,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(court.Player.transform)&&!hits[i].transform.IsChildOf(fish))return false;
            return true;
        }
        private Vector3 FindHop(Vector3 target)
        {
            int n=nodes.Length;for(int i=0;i<n;i++){costs[i]=Clear(fish.position,nodes[i],.65f)?Vector3.Distance(fish.position,nodes[i]):float.PositiveInfinity;previous[i]=-1;visited[i]=false;}
            for(int iteration=0;iteration<n;iteration++)
            {
                int best=-1;for(int i=0;i<n;i++)if(!visited[i]&&(best<0||costs[i]<costs[best]))best=i;
                if(best<0||float.IsPositiveInfinity(costs[best]))break;visited[best]=true;
                for(int i=0;i<n;i++)if(!visited[i]&&Clear(nodes[best],nodes[i],.65f)){float next=costs[best]+Vector3.Distance(nodes[best],nodes[i]);if(next<costs[i]){costs[i]=next;previous[i]=best;}}
            }
            int end=-1;float score=float.PositiveInfinity;
            for(int i=0;i<n;i++)if(Clear(nodes[i],target,.65f)){float next=costs[i]+Vector3.Distance(nodes[i],target);if(next<score){end=i;score=next;}}
            if(end<0)return fish.position;while(previous[end]>=0)end=previous[end];return nodes[end];
        }
    }
}
