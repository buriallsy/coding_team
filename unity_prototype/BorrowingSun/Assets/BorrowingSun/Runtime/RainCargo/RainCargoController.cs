using System;
using UnityEngine;

namespace BorrowingSun
{
    [Serializable]
    public sealed class RainCargoBindings
    {
        public Vector3 offset = new Vector3(-6,0,66);
        public Transform aLift, bLift, cart, waterA, tankWater, counterweight, counterweightWater;
        public Transform station0, station1, valveA, valveB, shortcutLever, shortcutGate, card;
        public Transform source0, source1, rainBeam, unloadBeam, weightCable, platformCable;
        public Transform[] stops;
        public Renderer[] tunnelRoofs;
        public CourtPlayer player;
        public CourtCamera cameraRig;
        public LineRenderer routePreview;
    }

    /// <summary>A separate revision: carry stored rain through a covered freight street.</summary>
    public sealed class RainCargoController : MonoBehaviour, ICourtGameplay
    {
        [SerializeField] private RainCargoBindings bindings = new RainCargoBindings();
        private RainCargoState state = new RainCargoState();
        private Vector3 cartFrom, cartTo;
        private Quaternion cartRotationFrom, cartRotationTo;
        private float travel, duration;
        private bool ready, shortcutOpen;
        private bool cartWaitingForPlayer;
        private BoxCollider cartBody;
        private CharacterController playerBody;
        private RainCargoCutaway cutaway;
        private RainCargoPushAssist pushAssist;
        private string message;
        private float messageUntil;
        private readonly RaycastHit[] sight = new RaycastHit[24];
        private static readonly string[] StopNames = {"西岸集货院","接雨升降台","穿山道入口","岩下货街","配重梯前坪","配重货梯","东岸卸货廊","花庭接驳坞"};
        public RainCargoBindings Bindings => bindings;
        public RainCargoState State => state;
        public CourtPlayer Player => bindings.player;
        public CourtCamera CameraRig => bindings.cameraRig;
        public bool IsObserving => CameraRig != null && CameraRig.IsObserving;
        public bool ShortcutOpen => shortcutOpen;
        public bool CartWaitingForPlayer => pushAssist != null && pushAssist.Active ? pushAssist.Waiting : State.CartInMotion && cartWaitingForPlayer;
        public bool IsCartAssisting => pushAssist != null && pushAssist.Active;
        public bool IsCartTargeted => Target()==5;
        public Vector3 PushDirection => pushAssist != null ? pushAssist.Direction : Vector3.zero;
        public string CurrentStopName => StopNames[State.CartStop];
        public string Message => Time.unscaledTime < messageUntil ? message : "";
        public void Initialize(RainCargoBindings value) => bindings = value;

        private void Start()
        {
            if(bindings.stops == null || bindings.stops.Length != 8 || Player == null || CameraRig == null || bindings.aLift == null || bindings.bLift == null)
            { Debug.LogError("运雨街缺少接线。",this); enabled=false; return; }
            Player.InitializeGameplay(this,CameraRig.ViewCamera,Player.SpawnPosition);
            Player.ConfigurePuzzleEdgeProtection();
            Player.ConfigureSafeDrop(IsSafeDropLanding);
            if(!CacheSafetyColliders())
            { Debug.LogError("运雨街需要苗车 BoxCollider 与玩家 CharacterController，才能安全让行。",this);enabled=false;return; }
            cutaway=GetComponent<RainCargoCutaway>();
            if(cutaway==null)cutaway=gameObject.AddComponent<RainCargoCutaway>();
            cutaway.Initialize(this);
            pushAssist=new RainCargoPushAssist(this,cartBody);
            ready=true; ResetCourt();
        }
        private void OnEnable(){if(cutaway!=null)cutaway.enabled=true;}
        private void OnDisable(){if(cutaway!=null)cutaway.enabled=false;if(pushAssist!=null)pushAssist.Suspend();}
        private void Update() { if(ready) Simulate(Time.deltaTime); }
        public int Support(Vector3 feet)
        {
            Vector3 p=feet-bindings.offset;
            if(p.x>-9.99f && p.x<-2.01f && Mathf.Abs(p.z)<2.59f && Mathf.Abs(p.y-(15+State.WaterA))<.35f)return 1;
            if(p.x>24.01f && p.x<31.99f && p.z>15.21f && p.z<20.79f && Mathf.Abs(p.y-(12+State.WaterB))<.35f)return 5;
            return 0;
        }
        public void Simulate(float dt)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)return;
            int support=Player!=null?Support(Player.FeetPosition):0;
            float a=State.WaterA,b=State.WaterB;
            State.Advance(dt);
            SetY(bindings.aLift,15+State.WaterA); SetY(bindings.bLift,12+State.WaterB);
            SetY(bindings.waterA,14.68f+State.WaterA);
            if(Player!=null && support!=0)Player.CarryByPlatform(support==1?State.WaterA-a:State.WaterB-b);
            if(IsCartAssisting)pushAssist.Advance(dt);
            else if(State.CartInMotion)
            {
                float nextTravel=Mathf.Min(1,travel+dt/duration);
                Quaternion nextRotation=Quaternion.Slerp(cartRotationFrom,cartRotationTo,Mathf.Clamp01(nextTravel/.12f));
                Vector3 nextPosition=Vector3.Lerp(cartFrom,cartTo,Mathf.SmoothStep(0,1,Mathf.Clamp01((nextTravel-.12f)/.88f)));
                if(PlayerBlocksCartSweep(bindings.cart.position,bindings.cart.rotation,nextPosition,nextRotation,dt))
                {
                    if(!cartWaitingForPlayer)ShowMessage("苗车已停下让行。请走到铜轨一侧，车会继续前往「"+StopNames[State.PendingCartStop]+"」。",6);
                    cartWaitingForPlayer=true;
                }
                else
                {
                    if(cartWaitingForPlayer)ShowMessage("已经让开，苗车继续驶向「"+StopNames[State.PendingCartStop]+"」。");
                    cartWaitingForPlayer=false;
                    travel=nextTravel;
                    bindings.cart.SetPositionAndRotation(nextPosition,nextRotation);
                    // CharacterController may update after this controller. Its Move must see the
                    // new collider pose, not a stale transform from the last physics step.
                    Physics.SyncTransforms();
                    if(travel>=1){State.CompleteCartMove();ShowMessage("已到："+StopNames[State.CartStop]);}
                }
            }
            else bindings.cart.position=bindings.stops[State.CartStop].position;
            // The small tank and counterweight vessel have the same visible capacity.
            FillWater(bindings.tankWater,State.TankWater,new Vector3(0,1.19f,-.72f));
            SetY(bindings.counterweight,12-State.WaterB);
            FillWater(bindings.counterweightWater,State.WaterB,new Vector3(0,.13f,0));
            if(bindings.weightCable!=null)Beam(bindings.weightCable,bindings.offset+new Vector3(28,23.5f,13.2f),bindings.counterweight.position+Vector3.up*1.1f);
            if(bindings.platformCable!=null)Beam(bindings.platformCable,bindings.offset+new Vector3(28,23.5f,15.3f),bindings.bLift.position+new Vector3(0,.2f,-2.7f));
            Transform holder=State.CardStation==0?bindings.station0:State.CardStation==1?bindings.station1:Player.transform;
            bindings.card.position=holder.position+Vector3.up*(State.CardStation<0?1.7f:1.18f);
            bindings.rainBeam.gameObject.SetActive(State.RainActive);
            if(State.RainActive)
            {
                Vector3 from=State.CardStation==0?bindings.source0.position:bindings.source1.position;
                Vector3 to=State.CardStation==0?bindings.waterA.position+new Vector3(-2,0,0):
                    State.RainToTank?bindings.tankWater.position+Vector3.up*.4f:bindings.offset+new Vector3(-6.7f,14.7f,0);
                Beam(bindings.rainBeam,from,to);
            }
            bindings.unloadBeam.gameObject.SetActive(State.Transferring);
            if(State.Transferring)Beam(bindings.unloadBeam,bindings.tankWater.position,bindings.counterweightWater.position);
            UpdateRoutePreview();
            Vector3 p=Player.FeetPosition-bindings.offset;
            bool atEaves=Mathf.Abs(p.y-18)<1 && Vector2.Distance(new Vector2(p.x,p.z),new Vector2(4,-17))<5.2f;
            CameraRig.SetGameplayFocus(bindings.offset+new Vector3(-6,19,0),atEaves ? .34f : 0);
            bool inTunnel=p.x>5 && p.x<19 && p.z>13 && p.z<25 && p.y<16;
            if(bindings.tunnelRoofs!=null)foreach(var r in bindings.tunnelRoofs)if(r!=null)r.enabled=!inTunnel;
            if(Mathf.Abs(a-State.WaterA)+Mathf.Abs(b-State.WaterB)>.00001f)Physics.SyncTransforms();
        }
        private void FillWater(Transform t,float amount,Vector3 floor)
        {
            float h=Mathf.Max(.015f,amount/3*.82f);
            t.localScale=new Vector3(1.26f,h,1.05f);t.localPosition=floor+Vector3.up*h*.5f;
        }
        private void SetY(Transform t,float y){var p=t.position;p.y=bindings.offset.y+y;t.position=p;}
        private static void Beam(Transform t,Vector3 from,Vector3 to)
        {t.position=(from+to)/2;t.rotation=Quaternion.LookRotation(to-from,Vector3.right);t.localScale=new Vector3(.13f,.13f,Vector3.Distance(from,to));}
        public void ResetCourt()
        {
            if(cutaway!=null)cutaway.Restore();
            if(pushAssist!=null)pushAssist.Cancel();
            State.Reset();travel=0;cartWaitingForPlayer=false;shortcutOpen=false;bindings.shortcutGate.gameObject.SetActive(true);
            bindings.cart.rotation=Quaternion.Euler(0,90,0);Player.ResetPlayer();CameraRig.SetGameplayFocus(default,0);CameraRig.ExitObservation();Simulate(0);Physics.SyncTransforms();
            ShowMessage("苗车的水箱是空的。把苗木送到岩壁另一侧的花庭接驳坞。",6);
        }
        public bool TryMoveCart(int direction,bool proximity=true)
        {
            if(IsCartAssisting)return false;
            if(proximity&&!NearCart()){ShowMessage("靠近苗车，E 向前、Q 倒推。");return false;}
            if(pushAssist!=null)
            {
                bool accepted=pushAssist.TryBegin(direction,out string why);
                ShowMessage(accepted?"自动绕到推车端，随后推往「"+StopNames[State.PendingCartStop]+"」。":why,5);
                return accepted;
            }
            if(!State.CanMoveCart(direction,out string reason)){ShowMessage(reason);return false;}
            cartFrom=bindings.cart.position;cartTo=bindings.stops[State.CartStop+direction].position;
            cartRotationFrom=bindings.cart.rotation;
            Vector3 heading=(cartTo-cartFrom)*(direction<0?-1:1);heading.y=0;
            cartRotationTo=Quaternion.LookRotation(heading);travel=0;duration=Mathf.Max(.9f,Vector3.Distance(cartFrom,cartTo)/3/.88f);
            Vector3 departure=cartFrom+Vector3.ClampMagnitude(cartTo-cartFrom,.65f);
            if(PlayerBlocksCartSweep(cartFrom,cartRotationFrom,cartFrom,cartRotationTo,Time.deltaTime)
                ||PlayerBlocksCartSweep(cartFrom,cartRotationTo,departure,cartRotationTo,Time.deltaTime))
            {ShowMessage("请站到铜轨一侧，避开车头和转向范围，再按 E / Q。",6);return false;}
            cartWaitingForPlayer=false;
            bool ok=State.BeginCartMove(direction);
            if(ok)ShowMessage("苗车驶向「"+StopNames[State.CartStop+direction]+"」；沿铜轨跟上。",4);
            return ok;
        }

        private bool CacheSafetyColliders()
        {
            if(cartBody==null&&bindings.cart!=null)cartBody=bindings.cart.GetComponent<BoxCollider>();
            if(playerBody==null&&Player!=null)playerBody=Player.GetComponent<CharacterController>();
            return cartBody!=null&&playerBody!=null;
        }

        internal bool PlayerBlocksCartSweep(Vector3 from,Quaternion fromRotation,Vector3 to,Quaternion toRotation,float dt)
        {
            if(!CacheSafetyColliders())return true;
            // CourtPlayer's configured walking speed is 4 m/s. Include a whole frame of possible
            // later player input, controller skin and a standing margin; long frames brake early.
            float clearance=IsCartAssisting ? .04f+playerBody.skinWidth : .15f+playerBody.skinWidth+4f*Mathf.Max(0,dt);
            int samples=Mathf.Max(1,Mathf.Max(Mathf.CeilToInt(Vector3.Distance(from,to)/.1f),
                Mathf.CeilToInt(Quaternion.Angle(fromRotation,toRotation)/4f)));
            for(int i=0;i<=samples;i++)
            {
                float t=(float)i/samples;
                if(PlayerOverlapsCartPose(Vector3.Lerp(from,to,t),Quaternion.Slerp(fromRotation,toRotation,t),clearance))return true;
            }
            return false;
        }

        private bool PlayerOverlapsCartPose(Vector3 position,Quaternion rotation,float clearance)
        {
            Vector3 cartScale=bindings.cart.lossyScale;
            Vector3 boxCenter=position+rotation*Vector3.Scale(cartBody.center,cartScale);
            Vector3 half=Vector3.Scale(cartBody.size,new Vector3(Mathf.Abs(cartScale.x),Mathf.Abs(cartScale.y),Mathf.Abs(cartScale.z)))*.5f;
            Vector3 playerScale=playerBody.transform.lossyScale;
            float radius=playerBody.radius*Mathf.Max(Mathf.Abs(playerScale.x),Mathf.Abs(playerScale.z));
            float segmentHalf=Mathf.Max(0,playerBody.height*Mathf.Abs(playerScale.y)*.5f-radius);
            Vector3 local=Quaternion.Inverse(rotation)*(playerBody.transform.TransformPoint(playerBody.center)-boxCenter);
            // Both the vehicle and capsule remain upright in this prototype; the capsule's
            // centre segment is parallel to the yaw-oriented box's Y axis.
            float x=Mathf.Max(0,Mathf.Abs(local.x)-half.x);
            float y=Mathf.Max(0,Mathf.Abs(local.y)-half.y-segmentHalf);
            float z=Mathf.Max(0,Mathf.Abs(local.z)-half.z);
            float safeRadius=radius+clearance;
            return x*x+y*y+z*z<=safeRadius*safeRadius;
        }
        public void InteractPrimary()
        {
            if(!isActiveAndEnabled||IsCartAssisting)return;
            switch(Target())
            {
                case 0: case 1:
                    int station=Target();
                    if(State.CardStation!=station){ShowMessage(State.CardStation<0?"Q 插入雨牌；E 开雨。":"雨牌还在另一座亭。");return;}
                    State.ToggleRain(station);ShowMessage(State.RainActive?(station==0?"雨水抬升接雨台。":"檐口开始落雨；观察苗车的水箱。"):"停雨，已经留下的水保留。");break;
                case 2: State.ToggleDrain(0);ShowMessage(State.DrainAOpen?"接雨台下降；车里的水不会一起排掉。":"接雨台停止排水。");break;
                case 3: State.ToggleDrain(1);ShowMessage("配重箱排水状态已切换。");break;
                case 4:
                    shortcutOpen=true;bindings.shortcutGate.gameObject.SetActive(false);ShowMessage("检修捷径已打开：屋檐与穿山道入口连通。",5);break;
                case 5:
                    if(State.CartStop==5&&!State.IsBHigh)
                    {if(State.TransferWater())ShowMessage("水箱接入配重斗。随车搭乘，抵达对岸高台。",6);else{State.CanTransferWater(out string why);ShowMessage(why);}return;}
                    TryMoveCart(1);break;
                default: ShowMessage("靠近雨亭、排水阀、检修门或苗车；V 可查看街区总览。");break;
            }
        }
        public void InteractSecondary()
        {
            if(!isActiveAndEnabled||IsCartAssisting)return;
            int target=Target();
            if(target==5){TryMoveCart(-1);return;}
            if(target!=0&&target!=1)return;
            bool taking=State.CardStation==target;
            bool ok=taking?State.RemoveCard(target):State.InsertCard(target);
            ShowMessage(ok?(taking?"雨牌已带走，水留在原处。":"雨牌入座，E 开雨。"):"先取回另一座亭的雨牌，或等设备停稳。");
        }
        public string Prompt()
        {
            if(IsCartAssisting)return CartWaitingForPlayer?"推车通路暂时受阻，已停住":pushAssist.Positioning?"自动绕到推车端…":"推往「"+StopNames[State.PendingCartStop]+"」…";
            switch(Target())
            {
                case 0:return StationPrompt(0,"升台雨亭");
                case 1:return StationPrompt(1,"屋檐集雨亭");
                case 2:return "接雨台排水阀 · E "+(State.DrainAOpen?"关阀":"排水下降");
                case 3:return "配重水箱阀 · E "+(State.DrainBOpen?"关阀":"排水复位");
                case 4:return shortcutOpen?"检修捷径已连通":"检修门 · E 从上层解锁捷径";
                case 5:
                    if(State.CartInMotion)return CartWaitingForPlayer?"苗车让行中 · 请走到铜轨一侧，离开后自动继续":"苗车行进中 · 沿铜轨跟随";
                    if(State.CartStop==5&&!State.IsBHigh)return "苗车水箱 → 配重斗 · E 卸水搭乘   Q 倒推";
                    if(State.Delivered)return State.Completed?"苗车和雨牌已到位":"苗车已到位 · 还需带走雨牌";
                    return "E 推往「"+NextStop+"」 · Q 倒推一站";
                default:return "沿铜轨查看货路；金色是货物，青色是雨水";
            }
        }
        private string StationPrompt(int i,string name)=>name+(State.CardStation==i?(State.RainActive?" · E 停雨   Q 取牌":" · E 开雨   Q 取牌"):State.CardStation<0?" · Q 插雨牌":" · 雨牌在另一亭");
        public string Region
        {
            get{Vector3 p=Player.FeetPosition-bindings.offset;if(p.x>31)return "东岸 · 花庭接驳坞";if(p.x>22)return "雨影区 · 配重货梯";if(p.z>10)return "岩下 · 穿山货街";if(p.y>17.5f&&p.z<-7)return "上层 · 集雨屋檐";return "西岸 · 集货院";}
        }
        public string Objective => State.Completed?"完成：把雨送进了下不了雨的街道。":"把苗车送到花庭接驳坞，并带走雨牌。";
        public string NextStop => State.Delivered?"花庭接驳坞":StopNames[Mathf.Min(7,State.CartStop+1)];
        public string CargoStatus
        {
            get{if(IsCartAssisting)return (CartWaitingForPlayer?"步行通路有障碍，暂停推行":pushAssist.Positioning?"正在自动就位":"正在推行")+" · 目标「"+StopNames[State.PendingCartStop]+"」";
                if(CartWaitingForPlayer)return "行进暂时受阻 · 目标「"+StopNames[State.PendingCartStop]+"」";
                if(State.Transferring)return "配重斗接水中";if(State.CartInMotion)return "驶向 "+StopNames[State.PendingCartStop];
                if(State.CartStop==5&&!State.IsBHigh){State.CanTransferWater(out string t);return State.TankFull?"可卸水抬升":t;}
                return State.CanMoveCart(1,out string reason)?"下一站："+NextStop:reason;}
        }
        private void UpdateRoutePreview()
        {
            var line=bindings.routePreview;if(line==null)return;
            line.enabled=State.CartStop<7 && !State.CartInMotion;
            if(!line.enabled)return;
            Vector3 a=bindings.cart.position+Vector3.up*.15f,b=bindings.stops[State.CartStop+1].position+Vector3.up*.15f;
            line.SetPosition(0,a);line.SetPosition(1,b);
            bool open=State.CanMoveCart(1,out _);line.startColor=line.endColor=open?new Color(.35f,1,.86f):new Color(1,.56f,.17f);
        }
        private int Target()
        {
            if(IsCartAssisting||NearCart())return 5;
            int result=-1;float distance=float.PositiveInfinity;
            Consider(bindings.station0,0,2.6f,ref result,ref distance);Consider(bindings.station1,1,2.6f,ref result,ref distance);
            Consider(bindings.valveA,2,2.6f,ref result,ref distance);Consider(bindings.valveB,3,2.6f,ref result,ref distance);
            Consider(bindings.shortcutLever,4,2.6f,ref result,ref distance);return result;
        }
        private bool NearCart()
        {
            if(Player==null||bindings.cart==null||!CacheSafetyColliders())return false;
            if(Mathf.Abs(Player.FeetPosition.y-bindings.cart.position.y)>1.35f)return false;
            Vector3 from=Player.FeetPosition+Vector3.up*.9f;
            Vector3 closest=cartBody.ClosestPoint(from);
            if(Vector3.ProjectOnPlane(closest-from,Vector3.up).sqrMagnitude>2.2f*2.2f)return false;
            Vector3 ray=closest-from;
            if(ray.sqrMagnitude<.001f)return true;
            int count=Physics.RaycastNonAlloc(from,ray.normalized,sight,ray.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==sight.Length)return false;
            for(int i=0;i<count;i++)
            {
                Transform hit=sight[i].transform;
                if(hit==Player.transform||hit.IsChildOf(Player.transform)||hit==bindings.cart||hit.IsChildOf(bindings.cart))continue;
                return false;
            }
            return true;
        }
        private void Consider(Transform t,int kind,float radius,ref int result,ref float distance)
        {if(!Near(t,radius))return;float d=(t.position-Player.FeetPosition).sqrMagnitude;if(d>=distance)return;distance=d;result=kind;}
        private bool Near(Transform t,float radius)
        {
            Vector3 d=t.position-Player.FeetPosition;if(Mathf.Abs(d.y)>1.5f||new Vector2(d.x,d.z).sqrMagnitude>radius*radius)return false;
            Vector3 from=Player.FeetPosition+Vector3.up,to=t.position+Vector3.up*.6f,ray=to-from;
            int count=Physics.RaycastNonAlloc(from,ray.normalized,sight,ray.magnitude,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var h=sight[i].transform;if(h==Player.transform||h.IsChildOf(Player.transform)||h==t||h.IsChildOf(t))continue;return false;}return count<sight.Length;
        }
        public bool IsPermanentGround(Vector3 feet)=>Support(feet)==0&&!IsInsideHazard(feet);
        private bool IsSafeDropLanding(Vector3 feet)
        {
            if(IsInsideHazard(feet)||!CacheSafetyColliders())return false;
            // A ray can see the cart's roof as a lower surface. Cargo is never a drop destination.
            Bounds occupied=cartBody.bounds;
            occupied.Expand(new Vector3(.7f,.25f,.7f));
            if(occupied.Contains(feet))return false;
            int support=Support(feet);
            return support==1?!State.AMoving:support==5?!State.BMoving:true;
        }
        public bool IsInsideHazard(Vector3 feet){Vector3 p=feet-bindings.offset;return p.y<10.8f||p.x<-36||p.x>55||p.z<-35||p.z>48;}
        public void ShowMessage(string text,float seconds=4f){message=text;messageUntil=Time.unscaledTime+seconds;}
    }
}
