using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Local wind aims the reflector; Sunny clears the sky and the scene sun supplies the ray.</summary>
    [DisallowMultipleComponent]
    public sealed class SkywellSunCollector : MonoBehaviour
    {
        [Header("太阳与本地天气")]
        [SerializeField] private Light sunLight;
        [SerializeField] private SkywellWeatherZone weatherZone;
        [SerializeField] private Transform sunlightSample;
        [SerializeField] private Collider[] sunSelfColliders;
        [Min(20),SerializeField] private float sunProbeDistance=250;
        [SerializeField] private LayerMask sunOcclusionMask=~0;
        [Header("风驱采光板与两个机械停点")]
        [SerializeField] private Transform collectorPlate;
        [Tooltip("0：就地校光靶；1：中央高位轴心。目标只定义机械停点，不传送光线。")]
        [SerializeField] private Transform[] focusTargets;
        [Min(1),SerializeField] private float focusDegreesPerSecond=60;
        [Range(.05f,2),SerializeField] private float lockTolerance=.25f;
        [SerializeField] private Renderer plateSurface;
        [SerializeField] private LineRenderer incomingSunRay;
        [Header("镜面反馈")]
        [SerializeField] private Color inactiveTint=new Color(.72f,.84f,.90f);
        [SerializeField] private Color activeTint=new Color(1,.96f,.82f);
        [Min(0),SerializeField] private float activeEmission=.35f;
        [Header("镜心修复")]
        [SerializeField] private bool requiresRepair;
        [SerializeField] private GameObject installedGem;
        public bool IsRepaired {get;private set;}
        public void ConfigureRepair(GameObject gem)
        {requiresRepair=true;installedGem=gem;IsRepaired=false;if(installedGem)installedGem.SetActive(false);}
        public bool TryInstallGem()
        {if(IsRepaired||!requiresRepair)return false;IsRepaired=true;if(installedGem)installedGem.SetActive(true);return true;}
        private readonly RaycastHit[] sunHits=new RaycastHit[64];
        private MaterialPropertyBlock tint;
        private bool wasLocked;
        public int FocusIndex{get;private set;}
        public bool HasDirectSunlight{get;private set;}
        public bool IsLocked{get;private set;}
        public bool Active{get;private set;}
        public string SunObstruction{get;private set;}
        public string FocusLabel=>FocusIndex==0?"就地校光":"中央接力镜";
        public Vector3 OutputOrigin=>collectorPlate?collectorPlate.position:transform.position;
        public Vector3 OutputDirection=>sunLight&&collectorPlate?Vector3.Reflect(sunLight.transform.forward,collectorPlate.forward).normalized:Vector3.zero;
        public Light SunLight=>sunLight;
        public SkywellWeatherZone WeatherZone=>weatherZone;
        public Transform Plate=>collectorPlate;
        public Transform[] FocusTargets=>focusTargets;
        public int LockCount{get;private set;}
        private void Awake(){if(weatherZone)weatherZone.RegisterResponder(this);ResetCollector();}
        public void ResetCollector()
        {
            IsRepaired=!requiresRepair;if(installedGem)installedGem.SetActive(IsRepaired);
            FocusIndex=0;LockCount=0;Active=HasDirectSunlight=IsLocked=wasLocked=false;SunObstruction=null;
            if(TryFocusRotation(out var rotation))collectorPlate.rotation=rotation;
            RefreshVisuals();
        }
        public void Tick(float deltaTime)
        {
            Active=false;HasDirectSunlight=false;IsLocked=false;SunObstruction=null;
            bool windDrive=collectorPlate&&weatherZone&&weatherZone.IsWind&&weatherZone.Contains(OutputOrigin)
                &&focusTargets!=null&&focusTargets.Length==2;
            if(windDrive)
            {
                // The console's west/east wind selects the existing two mechanical stops.
                int windFocus=weatherZone.WindDirection.x<0?0:1;
                if(FocusIndex!=windFocus){FocusIndex=windFocus;wasLocked=false;}
            }
            if(!collectorPlate||!sunLight||sunLight.type!=LightType.Directional||!TryFocusRotation(out var target)){RefreshVisuals();return;}
            // Losing wind stops at the actual pose, including mid-turn; Sunny cannot finish the movement.
            if(windDrive)collectorPlate.rotation=Quaternion.RotateTowards(collectorPlate.rotation,target,Mathf.Max(0,deltaTime)*focusDegreesPerSecond);
            IsLocked=Quaternion.Angle(collectorPlate.rotation,target)<=lockTolerance;
            if(IsLocked&&!wasLocked)LockCount++;
            wasLocked=IsLocked;
            HasDirectSunlight=CheckSunVisibility();
            // The exposed -Z face must physically receive the incident sun ray.
            Active=IsRepaired&&weatherZone&&weatherZone.IsSunny&&weatherZone.Contains(OutputOrigin)&&HasDirectSunlight&&IsLocked&&Vector3.Dot(sunLight.transform.forward,collectorPlate.forward)>.001f;
            RefreshVisuals();
        }
        private bool TryFocusRotation(out Quaternion rotation)
        {
            rotation=Quaternion.identity;
            if(!sunLight||!collectorPlate||focusTargets==null||FocusIndex>=focusTargets.Length||!focusTargets[FocusIndex])return false;
            var outgoing=(focusTargets[FocusIndex].position-collectorPlate.position).normalized;
            var normal=sunLight.transform.forward-outgoing;
            if(normal.sqrMagnitude<.00001f)return false;
            rotation=Quaternion.LookRotation(normal.normalized,Vector3.up);return true;
        }
        private bool CheckSunVisibility()
        {
            if(!sunLight.enabled||sunLight.intensity<=0)return false;
            var towardSun=-sunLight.transform.forward;
            if(towardSun.y<=.01f){SunObstruction="太阳低于地平线";return false;}
            var origin=sunlightSample?sunlightSample.position:OutputOrigin;
            int count=Physics.RaycastNonAlloc(origin+towardSun*.08f,towardSun,sunHits,sunProbeDistance,sunOcclusionMask,QueryTriggerInteraction.Ignore);
            if(count==sunHits.Length){SunObstruction="太阳遮挡查询容量不足";return false;}
            float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                if(IsCollectorCollider(sunHits[i].collider)||sunHits[i].distance>=nearest)continue;
                nearest=sunHits[i].distance;SunObstruction=sunHits[i].collider.name;
            }
            return SunObstruction==null;
        }
        public bool IsCollectorCollider(Collider candidate)
        {
            if(sunSelfColliders==null)return false;
            for(int i=0;i<sunSelfColliders.Length;i++)if(sunSelfColliders[i]==candidate)return true;
            return false;
        }
        private void RefreshVisuals()
        {
            if(plateSurface)
            {
                if(tint==null)tint=new MaterialPropertyBlock();
                var color=Active?activeTint:inactiveTint;
                tint.SetColor("_BaseColor",color);tint.SetColor("_EmissionColor",Active?color*activeEmission:Color.black);plateSurface.SetPropertyBlock(tint);
            }
            if(incomingSunRay)
            {
                incomingSunRay.enabled=Active;
                if(Active){incomingSunRay.positionCount=2;incomingSunRay.SetPosition(0,OutputOrigin-sunLight.transform.forward*9);incomingSunRay.SetPosition(1,OutputOrigin);}
            }
        }
        private void OnDrawGizmosSelected()
        {
            if(!collectorPlate||!sunLight)return;
            Gizmos.color=HasDirectSunlight?Color.yellow:Color.red;Gizmos.DrawRay(OutputOrigin,-sunLight.transform.forward*15);
            Gizmos.color=Color.cyan;Gizmos.DrawRay(OutputOrigin,OutputDirection*12);
            if(focusTargets!=null)foreach(var target in focusTargets)if(target)Gizmos.DrawWireSphere(target.position,.4f);
        }
    }
}
