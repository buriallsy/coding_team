using System;
using UnityEngine;

namespace BorrowingSun
{
    [DefaultExecutionOrder(-100)]
    public sealed partial class SkywellController : MonoBehaviour, ICourtGameplay
    {
        [SerializeField] private CourtPlayer player;
        [SerializeField] private CourtCamera cameraRig;
        [SerializeField] private Transform rotor, cart, water;
        [SerializeField] private Transform[] recallControls;
        [SerializeField] private Vector3 origin;
        [SerializeField] private SkywellWeatherConsole[] consoles;
        [SerializeField] private SkywellOptics optics;
        [SerializeField] private Transform mirrorControl, ballast;
        [SerializeField] private Transform[] supportClaws;
        [SerializeField] private SkywellJourney journey;
        [SerializeField] private SkywellVoyage voyage;
        [SerializeField] private SkywellObservation observation;
        [SerializeField] private SkywellStory story;
        [SerializeField] private SkywellDeerGrove grove;
        [SerializeField] private SkywellFlowerMarket flowerMarket;
        public SkywellFlowerMarket FlowerMarket=>flowerMarket&&flowerMarket.isActiveAndEnabled?flowerMarket:null;
        private bool MarketInputBlocked=>FlowerMarket&&flowerMarket.InputBlocked;
        private bool MarketWeatherAllowed(int station,SkywellToken token)=>
            !FlowerMarket||flowerMarket.AllowsWeather(station,token,voyage&&voyage.Delivered);
        public SkywellDeerGrove Grove=>grove;
        public void ConfigureGrove(SkywellDeerGrove value)=>grove=value;
        public SkywellStory Story=>story;
        public void ConfigureStory(SkywellStory value)=>story=value;
        private int observationClosedFrame = -1;
        [SerializeField] private Transform drainValve;
        [SerializeField] private float northCourtZ=32, eastGateZ=39;
        [Header("Wind / 风驱机械")]
        [SerializeField, Min(1), Tooltip("Central turntable angular speed in degrees per second while its linked zone has wind.")]
        private float windRotationSpeed=30;
        [Header("Opening / 起点地标观察")]
        [SerializeField, Tooltip("Briefly frames the collector, tower and shaded village. Walking away interrupts the view.")]
        private bool openingLandmarkView=true;
        [SerializeField, Min(.1f)] private float openingLandmarkDuration=4;
        private float messageUntil;
        private string message;
        private SkywellWindBell windBell;
        private readonly bool[] visited = {true,false,false};
        private readonly RaycastHit[] supportHits = new RaycastHit[16];

        // Keep these indices in sync with the authored consoles array; never reorder implicitly.
        private const int TowerStation=0, CollectorStation=1, RefugeStation=2, VillageStation=3;
        private SkywellWeatherConsole WeatherStation(int index)=>
            consoles!=null&&index>=0&&index<consoles.Length?consoles[index]:null;
        public SkywellWeatherZone RefugeWeatherZone=>WeatherStation(RefugeStation)?WeatherStation(RefugeStation).Zone:null;

        public SkywellVoyage Voyage=>voyage;
        public SkywellObservation Observation=>observation;
        public bool ObservationOpen=>observation&&observation.IsOpen;
        public bool ObservationInputBlocked=>ObservationOpen||observationClosedFrame==Time.frameCount||story&&story.InputBlocked;
        public void ConfigureObservation(SkywellObservation value)=>observation=value;
        public void CloseObservation()
        {
            if(observation)observation.Close();
            observationClosedFrame=Time.frameCount;
            // Keep the closing key out of the later camera/HUD updates in this frame.
            SyncCameraInteraction();
        }
        public void ConfigureVoyage(SkywellVoyage value)=>voyage=value;
        public SkywellJourney Journey=>journey;
        public void ConfigureJourney(SkywellJourney value)=>journey=value;
        public void ConfigureWeatherStations(SkywellWeatherConsole[] stations)
        {
            consoles=stations??Array.Empty<SkywellWeatherConsole>();
            
            if(TowerWeatherZone)TowerWeatherZone.RegisterResponder(this);
        }
        public Transform DrainValve=>drainValve;
        public void ConfigureDrain(Transform valve)=>drainValve=valve;
        public float NorthCourtZ=>northCourtZ;
        public float EastCourtDistance=>eastGateZ+1;
        public SkywellWindBell WindBell=>windBell;
        public CourtPlayer Player=>player;
        public CourtCamera CameraRig=>cameraRig;
        public Transform Rotor=>rotor;
        public Transform Cart=>cart;
        public SkywellWeatherConsole[] Consoles=>consoles;
        public SkywellOptics Optics=>optics;
        public Transform MirrorControl=>mirrorControl;
        public Transform[] RecallControls=>recallControls;
        public Vector3 Origin=>origin;
        public bool BorrowedSunAtVillage=>optics&&optics.Lit;
        public bool Complete=>DockedPort==2&&height>platformHighY-.01f&&ballastAmount>MaxStoredWater-.01f&&BorrowedSunAtVillage&&!cargoPausedForLight
            &&(!optics.SettlementReceiver||optics.SettlementReceiver.RewardAvailable);
        public bool IsObserving=>MarketInputBlocked||ObservationInputBlocked||WeatherInputBlocked||journey&&journey.ControlsMovement||cameraRig!=null&&cameraRig.IsObserving;
        public static string PortName(int index)=>index==0?"西侧货庭 · 低位":index==1?"北侧采光庭 · 高位":index==2?"东侧跨谷轨 · 高位":"门洞未接平";
        private void Awake()
        {
            windBell=GetComponent<SkywellWindBell>();
            if(TowerWeatherZone)TowerWeatherZone.RegisterResponder(this);
        }
        private void Start()=>ResetCourt();
        public bool IsInsideHazard(Vector3 feet)=>!(journey&&journey.BelowGroundActive)&&feet.y<origin.y-3;
        public bool IsPermanentGround(Vector3 feet)=>!OnRotor(feet);
        public bool OnRotor(Vector3 feet)
        {
            int n=Physics.RaycastNonAlloc(feet+Vector3.up*.18f,Vector3.down,supportHits,.45f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float best=float.PositiveInfinity;Transform support=null;
            for(int i=0;i<n;i++)
            {var hit=supportHits[i];if(hit.transform.IsChildOf(player.transform)||hit.normal.y<.6f||hit.distance>=best)continue;best=hit.distance;support=hit.transform;}
            return support!=null&&support.IsChildOf(rotor);
        }
        public bool DoorwayOccupied()
        {var p=rotor.InverseTransformPoint(player.FeetPosition);return Mathf.Abs(p.y)<1&&Mathf.Abs(p.x)<2.5f&&p.z>5.3f&&p.z<8.5f;}
        private void Update()
        {
            if(consoles==null||consoles.Length<2||!optics)return;
            ReturnDepartedTokens();
            if(FlowerMarket)flowerMarket.Tick(Time.deltaTime);
            UpdateWeatherInput();
            foreach(var station in consoles)if(station)station.Tick(Time.deltaTime);
            bool wasLit=BorrowedSunAtVillage;
            // The collector validates its own local weather, real sun access and mechanical focus.
            optics.Evaluate(true);
            if(wasLit&&!BorrowedSunAtVillage&&Mathf.Abs(yaw-targetYaw)>.1f)
            {ShowMessage("中继镜随平台转走，山阴暂时断光；已存的雨水与机械位置保留。",5);}
            TickMechanisms(Time.deltaTime);
        }

        private enum InteractionKind { None, Recall, Drain, Observation, Story }
        private struct InteractionTarget {public InteractionKind kind;public int index;}
        private InteractionTarget Target()
        {
            var result=new InteractionTarget();float best=2.7f*2.7f;
            void Consider(Transform t,InteractionKind kind,int index,float radius=2.7f)
            {
                if(!t||Mathf.Abs(player.FeetPosition.y-t.position.y)>1.5f)return;
                float distance=Vector3.ProjectOnPlane(player.FeetPosition-t.position,Vector3.up).sqrMagnitude;
                if(distance>=best||distance>radius*radius)return;best=distance;result=new InteractionTarget{kind=kind,index=index};
            }
            if(recallControls!=null)for(int i=0;i<Mathf.Min(visited.Length,recallControls.Length);i++)Consider(recallControls[i],InteractionKind.Recall,i);
            Consider(drainValve,InteractionKind.Drain,0);
            if(story&&story.isActiveAndEnabled)for(int i=0;i<3;i++)Consider(story.InteractionPoint(i),InteractionKind.Story,i,story.InteractionRadius);
            if(observation&&observation.Available)Consider(observation.transform,InteractionKind.Observation,0,observation.InteractionRadius);
            return result;
        }
        public string Prompt()
        {
            if(story&&story.DialogueOpen)return "E 继续 · Esc 离开";
            if(ObservationOpen)return "E / Esc 返回";
            if(Time.time<messageUntil)return message;
            var target=Target();
            if(grove&&grove.Prompt!=null)return grove.Prompt;
            if(journey&&journey.CurrentPrompt!=null)return journey.CurrentPrompt;
            if(target.kind==InteractionKind.Drain)return controlledDescent?"放水阀 · E 关闭，保留当前水位":"放水阀 · E 打开，回西向低岸下降";
            if(FlowerMarket&&voyage&&voyage.Delivered)
            {
                string marketPrompt=flowerMarket.Prompt(player.FeetPosition);
                if(marketPrompt!=null)return marketPrompt;
            }
            if(voyage&&voyage.Prompt!=null)return voyage.Prompt;
            if(WeatherTargeting)return TargetingPrompt;
            if(grove&&grove.Ride&&grove.Ride.FootDriver&&grove.Ride.FootDriver.Prompt!=null)return grove.Ride.FootDriver.Prompt;
            if(target.kind==InteractionKind.Story)return story.PromptFor(target.index);
            if(target.kind==InteractionKind.Observation)return "山阴村 · E 观测";
            if(target.kind==InteractionKind.Recall)return "绞盘 · E 回接";
            return "长按 E 高亮目标 · 点击施令";
        }
        public string Objective=>voyage&&voyage.Delivered?"完成：水果货车已抵达花庭。":voyage&&voyage.Phase==SkywellVoyagePhase.Arrived?"走上花庭前站，在接驳标记按 E 交付。":voyage&&voyage.Phase==SkywellVoyagePhase.Riding?"越过空谷，回望借晴的山城。":
            journey&&(!journey.SunnyTokenObtained||!journey.HasReturnedToTower)?journey.Objective:Complete?"山阴已得晴光；带上水果，登乘跨谷。":cargoPausedForLight?"恢复通向山阴的光路，可继续货运。":WaterTransferring?"雨水正自动转入跨谷配重。":height<platformHighY-.1f?"借雨注满外围水箱，让配重下降、平台升高。":!BorrowedSunAtVillage?"到向阳山取得阳光，再用中央中继镜把光送往山阴。":"山阴村落正在迎来晴光。";
        public void InteractPrimary()
        {
            // E is also sent by CourtPlayer while movement is suppressed. Consume it here once.
            if(MarketInputBlocked){flowerMarket.Serve();return;}
            if(story&&story.DialogueOpen){story.Advance();return;}
            if(story&&story.InputBlocked)return;
            if(observationClosedFrame==Time.frameCount)return;
            if(ObservationOpen){CloseObservation();return;}
            if(WeatherInputBlocked||FootAirborne)return;
            BeginPrimaryPress();
        }
        private void PerformPrimaryInteraction()
        {
            var target=Target();
            if(grove&&grove.Ride&&grove.Ride.TryPrimary())return;
            if(journey&&journey.TryPrimary())return;
            if(target.kind==InteractionKind.Drain){ToggleDrain();return;}
            if(FlowerMarket&&voyage&&voyage.Delivered&&flowerMarket.TryInteract(player))
            {SyncCameraInteraction();return;}
            if(voyage&&voyage.TryPrimary())return;
            if(target.kind==InteractionKind.Story){story.TryInteract(target.index);return;}
            if(target.kind==InteractionKind.Observation)
            {observation.Open();SyncCameraInteraction();return;}
            if(target.kind!=InteractionKind.Recall)return;
            int port=target.index;
            if(!visited[port]||Mathf.Abs(height-(port==0?platformLowY:platformHighY))>.05f){ShowMessage("绞盘只能回接已到访的同层岸口；升降仍需集雨");return;}
            if(DoorwayOccupied()){ShowMessage("先离开门槛，再转塔");return;}
            BeginRecall(port);ShowMessage("机械绞盘回接「"+PortName(port)+"」");
        }
        public void InteractSecondary()
        {
            if(MarketInputBlocked){flowerMarket.CloseShop();SyncCameraInteraction();return;}
            if(ObservationInputBlocked)return;
            if(WeatherSelectionHeld){CancelWeatherPicker();return;}
            if(WeatherTargeting||primaryPending){CancelWeatherTargeting();return;}
            if(journey)journey.TrySecondary();
        }
        private void ReturnDepartedTokens()
        {
            if(!player)return;
            for(int i=0;i<consoles.Length;i++)
            {
                var station=consoles[i];
                if(!station||station.Token==SkywellToken.None||WithinWeatherOperatingArea(i))continue;
                ReturnStationToken(i);
            }
        }
        private void ReturnStationToken(int index)
        {
            var station=WeatherStation(index);if(!station)return;
            if(index<remoteWeatherActive.Length)remoteWeatherActive[index]=false;
            var returned=station.ReturnToken();
            if(index==TowerStation&&(returned==SkywellToken.Rain||returned==SkywellToken.Wind))StopTowerDrive();
        }
        public bool CanUseToken(SkywellToken token)=>token==SkywellToken.Rain||token==SkywellToken.Wind||token==SkywellToken.Sun&&journey&&journey.SunnyTokenObtained;
        private static readonly int[] SingleAction={0},TwoActions={0,1};
        private static int[] WeatherActions(SkywellToken token)=>token==SkywellToken.Wind?TwoActions:SingleAction;
        private static string[] WeatherActionLabels(int station,SkywellToken token)=>station==SkywellDeerGrove.StationIndex?token==SkywellToken.Rain?new[]{"滋养苹果树"}:token==SkywellToken.Wind?new[]{"摇落苹果","摇落苹果"}:new[]{"尝试引入阳光"}:token==SkywellToken.Rain?new[]{"当地降雨"}:token==SkywellToken.Wind?station==TowerStation?new[]{"↶ 逆时针 90°","↷ 顺时针 90°"}:station==CollectorStation?new[]{"转向校光位","转向中央接力镜"}:new[]{"向西","向东"}:new[]{"当地放晴"};
        public string TokenLocation(SkywellToken token)
        {
            if(!CanUseToken(token))return "尚未取得 · 船上";
            if(consoles!=null)for(int i=0;i<consoles.Length;i++)if(consoles[i]&&consoles[i].Token==token)return i==TowerStation?"在塔台":i==CollectorStation?"在采光庭":i==RefugeStation?"在水边码头":i==VillageStation?"在山阴村":i==SkywellDeerGrove.StationIndex?"在苹果树":"正在作用";
            return "在手中";
        }
        // This is the authority for token ownership and tower safety. Input only submits intent.
        private bool TryApplyWeather(int station,SkywellToken token,int action)
        {
            if(!CanUseToken(token)||!MarketWeatherAllowed(station,token)||Array.IndexOf(WeatherActions(token),action)<0)return false;
            var console=WeatherStation(station);
            if(!console||!console.Zone){ShowMessage("天气区域尚未接线，无法施令。");return false;}
            if(station==SkywellDeerGrove.StationIndex&&token==SkywellToken.Sun)
            {ShowMessage("这里没有可用光源。",3);return false;}
            if(station==TowerStation&&DoorwayOccupied())
            {ShowMessage("先离开接桥门槛，再转动塔台。");return false;}
            if(station==TowerStation&&token==SkywellToken.Wind)
            {
                if(IsMoving){ShowMessage("先等本次动作停稳，再选择方向。");return false;}
                RequestWindTurn(action);
            }
            for(int i=0;i<consoles.Length;i++)
                if(i!=station&&consoles[i]&&consoles[i].Token==token)
                ReturnStationToken(i);
            console.Request(token);
            RecordWeatherOperatingArea(station);
            if(token==SkywellToken.Wind)console.Zone.SetWindDirection(action==0?Vector3.left:Vector3.right);
            if(station==RefugeStation&&token==SkywellToken.Wind&&windBell)windBell.SelectEndpoint(action);
            if(station==TowerStation)BeginTowerWeather(token);
            ShowMessage(SkywellWeatherConsole.Name(token)+" · "+WeatherActionLabels(station,token)[action]);
            return true;
        }
        public void ShowMessage(string text,float seconds=4){message=text;messageUntil=Time.time+seconds;}
        public void ResetCourt()
        {
            if(cameraRig)cameraRig.ClearPhysicalFeedback();
            ResetMechanismState();
            if(cameraRig)cameraRig.ResetGameplayView();
            if(voyage)voyage.ResetVoyage();
            // Journey restores movement and the spawn before notifying fish/feedback listeners.
            // Without Journey the controller owns that final player reset.
            if(journey){journey.ResetJourney();ShowMessage("",0);}
            else player.ResetPlayer();
            if(cameraRig&&openingLandmarkView)cameraRig.ShowPhysicalFeedback(origin+new Vector3(18,9,5),openingLandmarkDuration,.75f,5f);
        }
        public void ResetMechanisms()
        {
            ResetMechanismState();
            player.ResetPlayer();
        }
        private void ResetMechanismState()
        {
            if(FlowerMarket)flowerMarket.ResetMarket();
            if(grove)grove.ResetGrove();
            if(story)story.ResetStory();
            if(observation)observation.ResetProgress();
            CloseObservation();
            ResetWeatherInput();
            ResetLiftState();
            visited[0]=true;visited[1]=visited[2]=false;
            if(consoles!=null)foreach(var station in consoles)if(station){station.ResetConsole();if(station.Zone)station.Zone.ResetWeatherVisuals();}
            if(windBell)windBell.ResetBell();
            // Optics owns the collector and receiver reset; do not reset them again here.
            if(optics)optics.ResetOptics();
            ApplyPose();UpdateMechanismVisuals();if(rainFeedback)rainFeedback.Refresh(0);Physics.SyncTransforms();ShowMessage("",0);
        }
    }
}
