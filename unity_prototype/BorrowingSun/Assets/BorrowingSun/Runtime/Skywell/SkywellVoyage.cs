using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace BorrowingSun
{
    public enum SkywellVoyagePhase { Waiting, Boarding, Boarded, Riding, Docking, Arrived, Delivered }
    [DefaultExecutionOrder(-70)]
    public sealed class SkywellVoyage : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform carrier,backGate,terminalGate;
        [SerializeField] private Vector3[] route;
        private Transform initialParent;
        private Vector3 initialPosition,standingLocal;
        private Quaternion initialRotation;
        private Vector3[] boardingPath;
        private int boardStep,routeStep;
        private float boardDeadline,totalLength,travelled;
        private bool scenic;
        private Vector3 cameraOffset=new Vector3(14,12,-17);
        private float cameraSize=14;
        public SkywellVoyagePhase Phase {get;private set;}
        public Transform Carrier=>carrier;
        public float Progress=>totalLength>0?Mathf.Clamp01(travelled/totalLength):0;
        public float MaximumRiderDrift {get;private set;}
        public Vector3 ExitPoint=>route[route.Length-1]+Vector3.forward*7;
        public bool Delivered=>Phase==SkywellVoyagePhase.Delivered;
        public bool CanDeliver=>Phase==SkywellVoyagePhase.Arrived&&Vector3.Distance(court.Player.FeetPosition,ExitPoint)<2.1f;
        public string Prompt=>Delivered?"天井跨谷完成 · 水果货车已交付；可自由回望，R 重新试玩":Phase==SkywellVoyagePhase.Boarding?"正在沿车侧绕到登乘位，Q 可取消":Phase==SkywellVoyagePhase.Boarded?"已站稳 · E 开始跨谷   Q 下车":Phase==SkywellVoyagePhase.Riding?(scenic?"回望：下方就是出发时的第一关水庭":"配重缓缓下降，载台沿接好的轨道驶向空谷"):Phase==SkywellVoyagePhase.Docking?"载台正在转正泊靠，稍候下车":Phase==SkywellVoyagePhase.Arrived?(CanDeliver?"E 确认接驳交付 · 水果货车停留在货台，花庭接管运输":"花庭前站已到 · 沿载台后方登岸，到接驳标记确认交付"):court.Complete&&Vector3.Distance(court.Player.FeetPosition,carrier.position)<6?"E 登上运载台 · 自动就位":null;
        public void Initialize(SkywellController owner,Transform deck,Transform gate,Transform terminal,Vector3[] worldRoute)
        {court=owner;carrier=deck;backGate=gate;terminalGate=terminal;route=worldRoute;}
        private void Awake(){initialParent=carrier.parent;initialPosition=carrier.localPosition;initialRotation=carrier.localRotation;}
        private void Start(){ResetVoyage();}
        public bool TryPrimary()
        {
            if(CanDeliver){Phase=SkywellVoyagePhase.Delivered;court.ShowMessage("天井跨谷完成：同一份雨水驱动跨谷，水果货车已交给花庭。",8);return true;}
            if(Delivered)return true;
            if(Phase!=SkywellVoyagePhase.Waiting)return Phase!=SkywellVoyagePhase.Arrived;
            if(!court.Complete||Vector3.Distance(court.Player.FeetPosition,carrier.position)>6)return false;
            if(!PlanBoarding()){court.ShowMessage("登乘通路被挡住，请从载台侧面的空地靠近。",4);return true;}
            boardStep=0;boardDeadline=Time.time+12;Phase=SkywellVoyagePhase.Boarding;return true;
        }
        private bool PlanBoarding()
        {
            // Resolve the local route against the same supported-floor and capsule checks as normal pushing.
            // Nodes cover both sides of the carrier and the weather pedestal; no collider is disabled.
            var nodes=new List<Vector3>{court.Player.FeetPosition};
            if(!court.Player.TryAssistedGround(carrier.TransformPoint(new Vector3(0,0,-2.05f)),.3f,out var goal))return false;
            nodes.Add(goal);
            void Add(float x,float z){if(court.Player.TryAssistedGround(carrier.TransformPoint(new Vector3(x,0,z)),.3f,out var p))nodes.Add(p);}
            Add(0,-3.3f);
            foreach(float x in new[]{-4.2f,-2.2f,2.2f,4.2f})foreach(float z in new[]{-3.3f,-1.3f,1.3f,4.2f})Add(x,z);
            var distances=new float[nodes.Count];var previous=new int[nodes.Count];var visited=new bool[nodes.Count];
            for(int i=0;i<nodes.Count;i++){distances[i]=float.PositiveInfinity;previous[i]=-1;}distances[0]=0;
            for(int iteration=0;iteration<nodes.Count;iteration++)
            {
                int at=-1;for(int i=0;i<nodes.Count;i++)if(!visited[i]&&(at<0||distances[i]<distances[at]))at=i;
                if(at<0||float.IsPositiveInfinity(distances[at]))return false;
                if(at==1){var path=new List<Vector3>();for(int n=1;n>0;n=previous[n])path.Add(nodes[n]);path.Reverse();boardingPath=path.ToArray();return true;}
                visited[at]=true;
                for(int next=1;next<nodes.Count;next++)
                {
                    float d=distances[at]+Vector3.Distance(nodes[at],nodes[next]);
                    if(visited[next]||d>=distances[next]||!court.Player.CanWalkAssistedSegment(nodes[at],nodes[next]))continue;
                    distances[next]=d;previous[next]=at;
                }
            }
            return false;
        }
        private void Update()
        {
            var keyboard=Keyboard.current;
            if(Phase==SkywellVoyagePhase.Boarding)
            {
                if(!court.Complete||Time.time>boardDeadline||keyboard!=null&&keyboard.qKey.wasPressedThisFrame){CancelBoarding();return;}
                court.Player.SetAssistedTarget(boardingPath[boardStep],boardStep==boardingPath.Length-1?carrier.forward:boardingPath[boardStep]-court.Player.FeetPosition,false);
                if(Vector3.ProjectOnPlane(court.Player.FeetPosition-boardingPath[boardStep],Vector3.up).magnitude<.13f)
                {
                    boardStep++;
                    if(boardStep==boardingPath.Length){court.Player.ReleaseAssistedMovement();standingLocal=carrier.InverseTransformPoint(court.Player.FeetPosition);court.Player.enabled=false;Phase=SkywellVoyagePhase.Boarded;}
                }
            }
            else if(Phase==SkywellVoyagePhase.Boarded)
            {
                if(!court.Complete||keyboard!=null&&keyboard.qKey.wasPressedThisFrame){CancelBoarding();return;}
                if(keyboard!=null&&keyboard.eKey.wasPressedThisFrame&&!court.WeatherInputBlocked&&!court.ObservationInputBlocked)Depart();
            }
            else if(Phase==SkywellVoyagePhase.Riding)Advance();
            else if(Phase==SkywellVoyagePhase.Docking)
            {
                float oldYaw=carrier.eulerAngles.y;carrier.rotation=Quaternion.RotateTowards(carrier.rotation,Quaternion.Euler(0,180,0),45*Time.deltaTime);
                Physics.SyncTransforms();court.Player.CarryByPlatform(carrier.TransformPoint(standingLocal)-court.Player.FeetPosition,Mathf.DeltaAngle(oldYaw,carrier.eulerAngles.y));
                if(Quaternion.Angle(carrier.rotation,Quaternion.Euler(0,180,0))<.01f)
                {
                    Phase=SkywellVoyagePhase.Arrived;court.Player.ConfigureSwimming(false);court.Player.enabled=true;court.CameraRig.SetJourneyView(false);court.CameraRig.SetGameplayFocus(Vector3.zero,0);
                    court.ShowMessage("跨谷抵达花庭前站；载台已转正对齐，沿后方登岸。",6);
                }
            }
            if(Phase==SkywellVoyagePhase.Riding||Phase==SkywellVoyagePhase.Boarded||Phase==SkywellVoyagePhase.Docking)
                if(keyboard!=null&&keyboard.rKey.wasPressedThisFrame)court.ResetCourt();
            if(backGate)backGate.localPosition=new Vector3(0,Phase==SkywellVoyagePhase.Riding?.9f:3.8f,-2.7f);
            terminalGate.localPosition=new Vector3(0,Phase==SkywellVoyagePhase.Arrived||Delivered?4:.9f,5);
        }
        private void Depart()
        {
            carrier.SetParent(court.transform,true);routeStep=0;travelled=0;totalLength=Vector3.Distance(carrier.position,route[0]);
            for(int i=1;i<route.Length;i++)totalLength+=Vector3.Distance(route[i-1],route[i]);
            Phase=SkywellVoyagePhase.Riding;court.ShowMessage("跨谷发车：雨水已转入跨谷配重，机械锁保持塔台高位。",6);
        }
        private void Advance()
        {
            Vector3 old=carrier.position;Quaternion oldRotation=carrier.rotation;
            scenic=carrier.position.z<45&&carrier.position.z>8;
            float speed=scenic?3.2f:5.5f;
            Vector3 target=route[routeStep],delta=target-old;
            if(delta.sqrMagnitude>.001f)carrier.rotation=Quaternion.RotateTowards(carrier.rotation,Quaternion.LookRotation(Vector3.ProjectOnPlane(delta,Vector3.up).normalized),45*Time.deltaTime);
            carrier.position=Vector3.MoveTowards(old,target,speed*Time.deltaTime);travelled+=Vector3.Distance(old,carrier.position);
            Physics.SyncTransforms();court.Player.CarryByPlatform(carrier.TransformPoint(standingLocal)-court.Player.FeetPosition,Mathf.DeltaAngle(oldRotation.eulerAngles.y,carrier.eulerAngles.y));
            MaximumRiderDrift=Mathf.Max(MaximumRiderDrift,Vector3.Distance(carrier.InverseTransformPoint(court.Player.FeetPosition),standingLocal));
            cameraOffset=Vector3.Lerp(cameraOffset,scenic?new Vector3(18,24,-26):new Vector3(14,12,-17),1-Mathf.Exp(-Time.deltaTime*1.4f));
            cameraSize=Mathf.Lerp(cameraSize,scenic?27:14,1-Mathf.Exp(-Time.deltaTime*1.4f));
            court.CameraRig.SetJourneyView(true,cameraOffset,cameraSize);
            court.CameraRig.SetGameplayFocus(scenic?new Vector3(0,0,0):court.Player.FeetPosition,scenic?.4f:0);
            if(Vector3.Distance(carrier.position,target)<.01f)
            {
                routeStep++;if(routeStep>=route.Length)
                {
                    // The final rail approaches diagonally. Align the rear opening with the north gangway before releasing the rider.
                    Phase=SkywellVoyagePhase.Docking;
                }
            }
        }
        private void CancelBoarding(){court.Player.enabled=true;court.Player.ReleaseAssistedMovement();Phase=SkywellVoyagePhase.Waiting;court.ShowMessage("已取消登乘，可以继续检查天气与轨道。",3);}
        public void ResetVoyage()
        {
            carrier.SetParent(initialParent,false);carrier.localPosition=initialPosition;carrier.localRotation=initialRotation;
            Phase=SkywellVoyagePhase.Waiting;travelled=totalLength=0;MaximumRiderDrift=0;scenic=false;
            cameraOffset=new Vector3(14,12,-17);cameraSize=14;
            court.Player.enabled=true;court.Player.ReleaseAssistedMovement();court.CameraRig.SetJourneyView(false);court.CameraRig.SetGameplayFocus(Vector3.zero,0);
        }
    }
}
