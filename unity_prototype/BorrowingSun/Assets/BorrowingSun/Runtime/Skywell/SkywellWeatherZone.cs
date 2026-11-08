using System;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>One physical station's local weather. Machines query only their explicitly linked zone.</summary>
    public sealed class SkywellWeatherZone : MonoBehaviour
    {
        [Header("Weather Zone / 局部天气边界")]
        [SerializeField] private SkywellWeatherConsole station;
        [SerializeField, Tooltip("A fixed anchor can keep the tower climate centered while its control pedestal rotates.")]
        private Transform centerAnchor;
        [SerializeField, Min(.5f)] private float radius=12;
        [SerializeField, Min(1), Tooltip("Cylinder half-height when no explicit volume is supplied.")]
        private float halfHeight=14;
        [SerializeField, Tooltip("Optional enabled trigger volume for an irregular region. Overrides radius and half-height.")]
        private Collider volume;
        [SerializeField, Min(.05f)] private float transitionDuration=.35f;
        [SerializeField, Range(0,2), Tooltip("Extra visual edge only. Gameplay responders still use the exact zone boundary.")]
        private float visualFalloff=.6f;
        [SerializeField, Tooltip("Explicitly linked local responders, shown for authoring/debugging. No global broadcast.")]
        private MonoBehaviour[] responders=Array.Empty<MonoBehaviour>();

        [Header("Rain / 降雨")]
        [SerializeField, Range(10,1500)] private float rainRate=700;
        [SerializeField, Min(2)] private float rainSpeed=16;
        [SerializeField, Min(1)] private float rainCeiling=13;
        [SerializeField, Range(.02f,.2f)] private float rainWidth=.075f;
        [SerializeField, Tooltip("Reuse the tower's authored rain particles instead of spawning a second rain system.")]
        private ParticleSystem rain;

        [Header("Wind / 风向与反馈")]
        [SerializeField, Range(.2f,12)] private float windStrength=4;
        [SerializeField] private Vector3 windDirection=Vector3.right;
        [SerializeField, Range(5,100)] private float windParticleRate=24;
        [SerializeField, Tooltip("Optional weather particle material; this is visual atmosphere, never a gameplay beam.")]
        private Material weatherMaterial;
        private ParticleSystem wind,cloud;
        private Material ownedMaterial;
        private readonly ParticleSystem.Particle[] particleBuffer=new ParticleSystem.Particle[2048];
        private float visualRain,visualWind,visualCloud;
        private bool initializedVisuals,overlapReported;

        public SkywellWeatherConsole Station=>station;
        public SkywellToken ActiveToken=>station?station.Token:SkywellToken.None;
        public SkywellToken ActiveWeather=>isActiveAndEnabled&&station&&station.isActiveAndEnabled?station.ActiveWeather:SkywellToken.None;
        public SkywellToken Weather=>ActiveWeather;
        public bool IsRain=>ActiveWeather==SkywellToken.Rain;
        public bool IsWind=>ActiveWeather==SkywellToken.Wind;
        public bool IsSunny=>ActiveWeather==SkywellToken.Sun;
        public float Radius=>radius;
        public Vector3 Center=>centerAnchor?centerAnchor.position:transform.position;
        public Collider Volume=>volume;
        public Vector3 WindDirection=>Vector3.ProjectOnPlane(windDirection,Vector3.up).sqrMagnitude>.001f?Vector3.ProjectOnPlane(windDirection,Vector3.up).normalized:Vector3.right;
        public MonoBehaviour[] RegisteredResponders=>responders;
        public ParticleSystem RainParticles=>rain;
        public bool RainVisible=>rain&&rain.isEmitting;
        public bool WindVisible=>wind&&wind.isEmitting;
        public float CloudAmount=>visualCloud;
        public Bounds GameplayBounds=>volume?volume.bounds:new Bounds(Center,new Vector3(radius*2,halfHeight*2,radius*2));

        public void Initialize(SkywellWeatherConsole weatherStation,Transform anchor=null,float zoneRadius=12,Collider region=null,ParticleSystem rainOverride=null)
        {
            station=weatherStation;centerAnchor=anchor;radius=Mathf.Max(.5f,zoneRadius);volume=region;
            if(rainOverride)rain=rainOverride;
            if(station)station.ConfigureZone(this);
            if(Application.isPlaying)EnsureVisuals();
        }
        public void RegisterResponder(MonoBehaviour responder)
        {
            if(!responder)return;
            if(responders==null)responders=Array.Empty<MonoBehaviour>();
            foreach(var linked in responders)if(linked==responder)return;
            Array.Resize(ref responders,responders.Length+1);responders[responders.Length-1]=responder;
        }
        public bool Contains(Vector3 point)
        {
            if(volume)return volume.enabled&&volume.gameObject.activeInHierarchy&&(volume.ClosestPoint(point)-point).sqrMagnitude<.0001f;
            Vector3 delta=point-Center;
            return Mathf.Abs(delta.y)<=halfHeight&&delta.x*delta.x+delta.z*delta.z<=radius*radius;
        }
        public void SetWindDirection(Vector3 direction)
        {windDirection=Vector3.ProjectOnPlane(direction,Vector3.up).sqrMagnitude>.001f?Vector3.ProjectOnPlane(direction,Vector3.up).normalized:Vector3.right;}
        private void Awake()
        {
            if(station)station.ConfigureZone(this);
            EnsureVisuals();
        }
        private void Start()=>CheckOverlapWarnings();
        private void OnValidate()
        {
            radius=Mathf.Max(.5f,radius);halfHeight=Mathf.Max(1,halfHeight);rainSpeed=Mathf.Max(2,rainSpeed);
            transitionDuration=Mathf.Max(.05f,transitionDuration);rainCeiling=Mathf.Max(1,rainCeiling);
            overlapReported=false;
        }
        private void LateUpdate()
        {
            EnsureVisuals();
            float step=Time.deltaTime/Mathf.Max(.05f,transitionDuration);
            // Removing rain/wind immediately stops new weather; existing water/positions belong to the responders.
            visualRain=IsRain?Mathf.MoveTowards(visualRain,1,step):0;
            visualWind=IsWind?Mathf.MoveTowards(visualWind,1,step):0;
            visualCloud=Mathf.MoveTowards(visualCloud,IsSunny?0:1,step);
            ConfigureRain();ConfigureWind();ConfigureCloud();
            SetEmission(rain,IsRain?rainRate*visualRain:0,true);
            SetEmission(wind,IsWind?windParticleRate*visualWind:0,true);
            SetEmission(cloud,visualCloud*3,IsSunny);
            TrimVisualParticles(rain);TrimVisualParticles(wind);TrimVisualParticles(cloud);
        }
        public void ResetWeatherVisuals()
        {
            visualRain=visualWind=0;visualCloud=1;
            StopAndClear(rain);StopAndClear(wind);StopAndClear(cloud);
            windDirection=Vector3.right;
        }
        private void EnsureVisuals()
        {
            if(initializedVisuals||!Application.isPlaying)return;
            if(!weatherMaterial)
            {
                var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if(!shader)shader=Shader.Find("Sprites/Default");
                if(shader)
                {
                    ownedMaterial=new Material(shader){name="Local weather prototype",hideFlags=HideFlags.DontSave};
                    if(ownedMaterial.HasProperty("_Surface"))ownedMaterial.SetFloat("_Surface",1);
                    if(ownedMaterial.HasProperty("_Blend"))ownedMaterial.SetFloat("_Blend",0);
                    if(ownedMaterial.HasProperty("_SrcBlend"))ownedMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if(ownedMaterial.HasProperty("_DstBlend"))ownedMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    if(ownedMaterial.HasProperty("_ZWrite"))ownedMaterial.SetFloat("_ZWrite",0);
                    ownedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");ownedMaterial.renderQueue=3000;
                    weatherMaterial=ownedMaterial;
                }
            }
            if(!rain)rain=CreateParticles("Local_rain",new Color(.55f,.82f,.92f,.8f),.075f);
            else {var main=rain.main;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=false;}
            // New zones use the same readable falling streaks as the authored tower rain.
            var rainRenderer=rain.GetComponent<ParticleSystemRenderer>();
            rainRenderer.renderMode=ParticleSystemRenderMode.Stretch;rainRenderer.velocityScale=.025f;rainRenderer.lengthScale=4;
            wind=CreateParticles("Local_wind",new Color(.85f,.87f,.75f,.5f),.12f);
            cloud=CreateParticles("Local_cloud_cover",new Color(.48f,.55f,.60f,.18f),2.5f);
            var windRenderer=wind.GetComponent<ParticleSystemRenderer>();windRenderer.renderMode=ParticleSystemRenderMode.Stretch;windRenderer.velocityScale=.08f;windRenderer.lengthScale=2.5f;
            var rainCollision=rain.collision;rainCollision.enabled=true;rainCollision.type=ParticleSystemCollisionType.World;rainCollision.mode=ParticleSystemCollisionMode.Collision3D;
            rainCollision.quality=ParticleSystemCollisionQuality.Medium;rainCollision.lifetimeLoss=1;rainCollision.collidesWith=Physics.DefaultRaycastLayers;
            initializedVisuals=true;ResetWeatherVisuals();
        }
        private ParticleSystem CreateParticles(string label,Color color,float size)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=2048;
            main.startLifetime=2;main.startSpeed=0;main.startSize=size;main.startColor=color;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();if(weatherMaterial)renderer.sharedMaterial=weatherMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return particles;
        }
        private void ConfigureRain()
        {
            var bounds=GameplayBounds;
            float top=volume?bounds.max.y:Center.y+Mathf.Min(rainCeiling,halfHeight);
            rain.transform.SetPositionAndRotation(new Vector3(bounds.center.x,top,bounds.center.z),Quaternion.identity);
            var main=rain.main;main.startSize=rainWidth;main.startSpeed=0;main.startLifetime=Mathf.Max(.2f,(top-bounds.min.y)/rainSpeed);main.maxParticles=2048;
            var velocity=rain.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=0;velocity.y=-rainSpeed;velocity.z=0;
            var shape=rain.shape;
            if(volume){shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(bounds.size.x+visualFalloff*2,.05f,bounds.size.z+visualFalloff*2);shape.rotation=Vector3.zero;}
            else {shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=radius+visualFalloff;shape.scale=Vector3.one;shape.rotation=new Vector3(90,0,0);}
        }
        private void ConfigureWind()
        {
            var bounds=GameplayBounds;wind.transform.SetPositionAndRotation(bounds.center,Quaternion.identity);
            var main=wind.main;main.startLifetime=3;main.startSize=.12f;
            var velocity=wind.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            var d=WindDirection*windStrength;velocity.x=d.x;velocity.y=.05f;velocity.z=d.z;
            var shape=wind.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(bounds.size.x,Mathf.Min(5,bounds.size.y),bounds.size.z);
        }
        private void ConfigureCloud()
        {
            var bounds=GameplayBounds;cloud.transform.position=bounds.center+Vector3.up*Mathf.Min(5,bounds.extents.y*.5f);
            var main=cloud.main;main.startLifetime=2.5f;main.startSize=new ParticleSystem.MinMaxCurve(2,3.5f);main.startColor=new Color(.48f,.55f,.60f,.18f*visualCloud);
            var shape=cloud.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(bounds.size.x,1,bounds.size.z);
            var velocity=cloud.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.1f;velocity.y=0;velocity.z=.04f;
        }
        private static void SetEmission(ParticleSystem particles,float rate,bool clearWhenStopped)
        {
            if(!particles)return;
            var emission=particles.emission;emission.enabled=true;emission.rateOverTime=rate;
            if(rate>.01f){if(!particles.isPlaying)particles.Play();}
            else if(particles.isPlaying||clearWhenStopped&&particles.particleCount>0)
                particles.Stop(true,clearWhenStopped?ParticleSystemStopBehavior.StopEmittingAndClear:ParticleSystemStopBehavior.StopEmitting);
        }
        private void TrimVisualParticles(ParticleSystem particles)
        {
            if(!particles||particles.particleCount==0)return;
            int count=particles.GetParticles(particleBuffer);var bounds=GameplayBounds;bounds.Expand(visualFalloff*2);
            float r=radius+visualFalloff;
            for(int i=0;i<count;i++)
            {
                var p=particleBuffer[i].position;var delta=p-Center;
                bool inside=volume?bounds.Contains(p):Mathf.Abs(delta.y)<=halfHeight+visualFalloff&&delta.x*delta.x+delta.z*delta.z<=r*r;
                if(!inside)particleBuffer[i].remainingLifetime=0;
            }
            particles.SetParticles(particleBuffer,count);
        }
        private static void StopAndClear(ParticleSystem particles){if(particles)particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        private void OnDisable(){StopAndClear(rain);StopAndClear(wind);StopAndClear(cloud);}
        private void OnDestroy(){if(ownedMaterial)Destroy(ownedMaterial);}
        public bool Overlaps(SkywellWeatherZone other)
        {
            if(!other||other==this||!GameplayBounds.Intersects(other.GameplayBounds))return false;
            if(volume||other.volume)return true; // Conservative debug warning for authored irregular volumes; no priority/blending is invented.
            Vector3 delta=Center-other.Center;return delta.x*delta.x+delta.z*delta.z<(radius+other.radius)*(radius+other.radius);
        }
        private void CheckOverlapWarnings()
        {
            if(overlapReported)return;overlapReported=true;
            var zones=FindObjectsByType<SkywellWeatherZone>();
            for(int i=Array.IndexOf(zones,this)+1;i<zones.Length;i++)
                if(Overlaps(zones[i]))Debug.LogWarning("Local weather zone bounds overlap: "+name+" / "+zones[i].name+". Keep responders explicitly linked; adjust the authored bounds.",this);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=IsRain?new Color(.25f,.7f,1,.7f):IsWind?new Color(.75f,.85f,.65f,.7f):IsSunny?new Color(1,.8f,.25f,.7f):new Color(.65f,.7f,.75f,.5f);
            if(volume)Gizmos.DrawWireCube(volume.bounds.center,volume.bounds.size);
            else
            {
                for(int ring=-1;ring<=1;ring++)for(int i=0;i<48;i++)
                {float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;Gizmos.DrawLine(Center+new Vector3(Mathf.Cos(a)*radius,ring*halfHeight,Mathf.Sin(a)*radius),Center+new Vector3(Mathf.Cos(b)*radius,ring*halfHeight,Mathf.Sin(b)*radius));}
                Gizmos.DrawLine(Center-Vector3.up*halfHeight,Center+Vector3.up*halfHeight);
            }
            if(responders!=null)foreach(var responder in responders)if(responder)Gizmos.DrawLine(Center,responder.transform.position);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(Center+Vector3.up*2,$"{name} · {ActiveWeather} · token {ActiveToken} · responders {responders?.Length??0}");
            foreach(var other in FindObjectsByType<SkywellWeatherZone>())if(Overlaps(other))
            {Gizmos.color=Color.red;Gizmos.DrawLine(Center,other.Center);UnityEditor.Handles.Label(Vector3.Lerp(Center,other.Center,.5f),"天气边界重叠");}
#endif
        }
    }
}
