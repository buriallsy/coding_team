using UnityEngine;

namespace BorrowingSun
{
    public sealed partial class SkywellController
    {
        [Header("Weather Zone / 塔台局部天气")]
        [SerializeField, Tooltip("Collection surfaces. Only wings inside the local rain region contribute water.")]
        private Transform[] rainCollectors;
        [SerializeField, Min(0), Tooltip("Seconds for rain to reach the roofs and gutters before stored water begins increasing.")]
        private float rainCollectionDelay=.45f;

        [Header("Water / 储水")]
        [SerializeField, Min(.1f), Tooltip("Shared capacity of the four lift tanks; also the existing cargo ballast capacity.")]
        private float maxStoredWater=6;
        [SerializeField, Min(.01f), Tooltip("Water units per second with all four collectors in the weather region.")]
        private float rainFillRate=1.5f;
        [SerializeField, Min(.01f), Tooltip("Water units per second released through the existing hand valve.")]
        private float drainRate=1.5f;

        [Header("Lift / 升降与独立旋转")]
        [SerializeField, Tooltip("Non-rotating parent carrying the deck and cable attachment points.")]
        private Transform outerLiftRing;
        [SerializeField, Tooltip("Low deck height relative to Origin. Authored west bridge: 0m.")]
        private float platformLowY=0;
        [SerializeField, Tooltip("High deck height relative to Origin. Authored north/east bridges: 6m. Bridges must be adjusted separately if changed.")]
        private float platformHighY=6;
        [SerializeField, Min(.01f), Tooltip("Maximum deck travel in metres per second; changing this does not change fill rate.")]
        private float liftMoveSpeed=1.5f;
        [SerializeField] private SkywellRainFeedback rainFeedback;
        private float height, waterLevel, yaw=-90, targetYaw=-90, ballastAmount;
        private float requestedWindYaw=-90;
        private enum WaterFlow { Idle, Collecting, Draining, Transferring }
        private WaterFlow waterFlow;
        private bool recalling, cargoPausedForLight, controlledDescent;
        private MaterialPropertyBlock mechanismTint;
        private bool liftLatched;
        private float rainElapsed;

        public SkywellWeatherZone TowerWeatherZone=>WeatherStation(TowerStation)?WeatherStation(TowerStation).Zone:null;
        public float WeatherRadius=>TowerWeatherZone?TowerWeatherZone.Radius:0;
        public Vector3 WeatherCenter=>TowerWeatherZone?TowerWeatherZone.Center:origin;
        public float MaxStoredWater=>Mathf.Max(.1f,maxStoredWater);
        public float StoredWaterNormalized=>Mathf.Clamp01(waterLevel/MaxStoredWater);
        public float BallastNormalized=>Mathf.Clamp01(ballastAmount/MaxStoredWater);
        public float LiftTravelNormalized=>Mathf.InverseLerp(platformLowY,platformHighY,height);
        public float PlatformLowY=>platformLowY;
        public float PlatformHighY=>platformHighY;
        public Transform OuterLiftRing=>outerLiftRing;
        public bool LiftLatched=>liftLatched;
        public bool RainActive=>TowerWeatherZone&&TowerWeatherZone.IsRain;
        public bool CollectorInRain(Transform collector)=>collector&&TowerWeatherZone&&TowerWeatherZone.Contains(collector.position);
        public float CollectorCoverage
        {
            get
            {
                if(rainCollectors==null||rainCollectors.Length==0)return 0;
                int count=0;foreach(var collector in rainCollectors)if(CollectorInRain(collector))count++;
                return count/(float)rainCollectors.Length;
            }
        }
        public void ConfigureLiftMechanism(Transform ring,Transform[] collectors,SkywellRainFeedback feedback)
        {outerLiftRing=ring;rainCollectors=collectors;rainFeedback=feedback;}
        public bool DrainOpen=>controlledDescent;
        public bool RainFilling=>waterFlow==WaterFlow.Collecting&&waterLevel<MaxStoredWater-.001f;
        public bool WaterTransferring=>waterFlow==WaterFlow.Transferring&&waterLevel>.001f&&ballastAmount<MaxStoredWater-.001f;
        public float Height=>height;
        public float WaterLevel=>waterLevel;
        public float Yaw=>yaw;
        public float BallastAmount=>ballastAmount;
        public bool CargoPausedForLight=>cargoPausedForLight;
        private float HeightTarget=>!controlledDescent&&liftLatched?platformHighY:Mathf.Lerp(platformLowY,platformHighY,StoredWaterNormalized);
        public bool IsMoving=>recalling||Mathf.Abs(height-HeightTarget)>.01f||Mathf.Abs(yaw-targetYaw)>.01f||waterFlow==WaterFlow.Collecting&&height<platformHighY-.01f||controlledDescent&&height>platformLowY+.01f;
        public int DockedPort=>IsMoving?-1:
            Mathf.Abs(height-platformLowY)<.025f&&Mathf.Abs(yaw+90)<.1f?0:
            Mathf.Abs(height-platformHighY)<.025f&&Mathf.Abs(yaw)<.1f?1:
            Mathf.Abs(height-platformHighY)<.025f&&Mathf.Abs(yaw-90)<.1f?2:-1;

        private void TickMechanisms(float dt)
        {
            cargoPausedForLight=liftLatched&&!controlledDescent&&ballastAmount>.001f&&!BorrowedSunAtVillage;
            rainElapsed=RainActive?rainElapsed+dt:0;
            ApplyWeatherAction();
            bool riding=OnRotor(player.FeetPosition);Vector3 local=rotor.InverseTransformPoint(player.FeetPosition);float previousYaw=yaw;
            if(waterFlow==WaterFlow.Collecting)waterLevel=Mathf.Min(MaxStoredWater,waterLevel+rainFillRate*CollectorCoverage*dt);
            else if(waterFlow==WaterFlow.Draining)waterLevel=Mathf.Max(0,waterLevel-drainRate*dt);
            else if(waterFlow==WaterFlow.Transferring&&BorrowedSunAtVillage&&Mathf.Abs(yaw-90)<.1f)
            {float amount=Mathf.Min(dt,waterLevel,MaxStoredWater-ballastAmount);waterLevel-=amount;ballastAmount+=amount;}
            height=Mathf.MoveTowards(height,HeightTarget,liftMoveSpeed*dt);
            if(!controlledDescent&&height>=platformHighY-.001f)liftLatched=true;
            yaw=Mathf.MoveTowards(yaw,targetYaw,windRotationSpeed*dt);
            if(Mathf.Abs(yaw-targetYaw)<.001f)recalling=false;
            if(controlledDescent&&height<platformLowY+.01f&&waterLevel<.01f)controlledDescent=false;
            ApplyPose();Physics.SyncTransforms();
            if(riding)player.CarryByPlatform(rotor.TransformPoint(local)-player.FeetPosition,yaw-previousYaw);
            if(DockedPort>=0)visited[DockedPort]=true;
            UpdateMechanismVisuals();
        }
        private void ApplyWeatherAction()
        {
            // A direction is captured once per use; wind does not repeat turns at a mechanical stop.
            if(!recalling)targetYaw=TowerWeatherZone&&TowerWeatherZone.IsWind&&TowerWeatherZone.Contains(rotor.position)?requestedWindYaw:yaw;
            bool aligned=height>platformHighY-.01f&&Mathf.Abs(yaw-90)<.1f&&Mathf.Abs(yaw-targetYaw)<.1f;
            bool canTransfer=aligned&&BorrowedSunAtVillage&&!controlledDescent&&(!voyage||voyage.Phase==SkywellVoyagePhase.Waiting);
            waterFlow=controlledDescent?WaterFlow.Draining:
                canTransfer&&waterLevel>.001f&&ballastAmount<MaxStoredWater-.001f?WaterFlow.Transferring:
                RainActive&&rainElapsed>=rainCollectionDelay&&CollectorCoverage>0?WaterFlow.Collecting:WaterFlow.Idle;
        }
        private float NextWindStop(int action)
        {
            float stop=action==1?Mathf.Floor((yaw+.1f)/90f)*90+90:Mathf.Ceil((yaw-.1f)/90f)*90-90;
            return Mathf.Clamp(stop,-90,90);
        }
        private void ToggleDrain()
        {
            if(controlledDescent){controlledDescent=false;ShowMessage("放水阀已关闭；水位保留。");return;}
            if(!OnRotor(player.FeetPosition)||IsMoving||Mathf.Abs(yaw+90)>.1f)
            {ShowMessage("先把平台转回西向低岸并停稳，再开放水阀。");return;}
            if(waterLevel<.01f&&height<platformLowY+.01f){ShowMessage("水槽已空，平台已在低处。");return;}
            StartControlledDescent();
            consoles[TowerStation].Request(SkywellToken.None);CancelWeatherTargeting();
            ShowMessage("放水阀打开：解开机械锁，平台随水位缓降；E 可关闭。",5);
        }
        private void ApplyPose()
        {
            if(outerLiftRing)
            {
                outerLiftRing.SetPositionAndRotation(origin+Vector3.up*height,Quaternion.identity);
                rotor.SetLocalPositionAndRotation(Vector3.zero,Quaternion.Euler(0,yaw,0));
            }
            else rotor.SetPositionAndRotation(origin+Vector3.up*height,Quaternion.Euler(0,yaw,0));
            if(water)water.position=origin+Vector3.up*(StoredWaterNormalized*6-.48f);
        }
        private void UpdateMechanismVisuals()
        {
            ballast.localPosition=new Vector3(9,-2+BallastNormalized*6-(voyage?6*voyage.Progress:0),4);
            foreach(var claw in supportClaws)claw.localScale=new Vector3(!controlledDescent&&liftLatched?2:.5f,.3f,.55f);
            if(mechanismTint==null)mechanismTint=new MaterialPropertyBlock();
            mechanismTint.SetColor("_BaseColor",liftLatched?new Color(1,.72f,.23f):new Color(.22f,.3f,.31f));
            foreach(var claw in supportClaws)claw.GetComponent<Renderer>().SetPropertyBlock(mechanismTint);
        }
        private void StartControlledDescent()
        {
            controlledDescent=true;
            liftLatched=false;
            cargoPausedForLight=false;
            recalling=false;
            targetYaw=requestedWindYaw=yaw;
        }
        private void BeginRecall(int port)
        {
            targetYaw=requestedWindYaw=-90+port*90;
            recalling=true;
            waterFlow=WaterFlow.Idle;
        }
        private void StopTowerDrive()
        {
            targetYaw=yaw;
            waterFlow=WaterFlow.Idle;
            recalling=false;
        }
        private void BeginTowerWeather(SkywellToken token)
        {
            StopTowerDrive();
            if(token==SkywellToken.Rain)controlledDescent=false;
        }
        private void RequestWindTurn(int action)=>requestedWindYaw=NextWindStop(action);
        private void ResetLiftState()
        {
            height=platformLowY;
            waterLevel=ballastAmount=rainElapsed=0;
            liftLatched=false;
            yaw=targetYaw=requestedWindYaw=-90;
            waterFlow=WaterFlow.Idle;
            recalling=cargoPausedForLight=controlledDescent=false;
        }

        private void OnValidate()
        {
            maxStoredWater=Mathf.Max(.1f,maxStoredWater);
            rainFillRate=Mathf.Max(.01f,rainFillRate);drainRate=Mathf.Max(.01f,drainRate);
            liftMoveSpeed=Mathf.Max(.01f,liftMoveSpeed);platformHighY=Mathf.Max(platformLowY+.1f,platformHighY);
            rainCollectionDelay=Mathf.Max(0,rainCollectionDelay);
            windRotationSpeed=Mathf.Max(1,windRotationSpeed);
        }
    }
}
