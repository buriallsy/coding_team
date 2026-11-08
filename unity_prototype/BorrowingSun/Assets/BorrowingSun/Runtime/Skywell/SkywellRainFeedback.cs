using UnityEngine;

namespace BorrowingSun
{
    // All water, weather and lift state belongs to the controller. These are kinematic visuals.
    public sealed class SkywellRainFeedback : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [Header("Counterweights / 配重水箱")]
        [SerializeField, Tooltip("Four non-rotating tank assemblies, outside the rotating gangway's swept volume.")]
        private Transform[] counterweightTanks;
        [SerializeField, Tooltip("Empty/low-lift tank base Y relative to this fixed tower frame.")]
        private float counterweightTopY=1;
        [SerializeField, Tooltip("Full/high-lift tank base Y relative to this fixed tower frame. Default travel equals the 6m lift stroke.")]
        private float counterweightBottomY=-5;
        [SerializeField] private Transform[] visibleWaterLevels,waterSurfaces,tankInlets;
        [SerializeField, Min(.1f), Tooltip("Visible water depth at full capacity, in metres.")]
        private float tankWaterDepth=4.2f;

        [Header("Visual Mechanism / 滑轮与钢索")]
        [SerializeField] private Transform[] pulleys,liftCableAnchors,cableMarkers;
        [SerializeField] private LineRenderer[] cables;
        [SerializeField, Min(.1f), Tooltip("Degrees of pulley rotation per metre of deck movement (radius 0.778m ≈ 73.7 degrees/m).")]
        private float pulleyRotationPerMeter=73.66f;
        [SerializeField, Min(.1f)] private float pulleyRadius=.778f;

        [Header("Collection / 集雨翼和可见水路")]
        [SerializeField] private Transform[] collectors,gutterOutlets,flowDrops;
        [SerializeField] private LineRenderer[] downpipes,waterStreams,ripples;
        [SerializeField, Min(.1f), Tooltip("Flow speed along the visible gutter and telescoping downpipe, in metres/second.")]
        public float pipeFlowSpeed=4;

        [Header("Local Rain / 由链接的 WeatherZone 管理")]
        [SerializeField] private ParticleSystem rain;
        public ParticleSystem RainParticles=>rain;
        public void ConfigureWeatherZone(SkywellWeatherZone zone){if(zone)zone.RegisterResponder(this);}
        public bool Raining=>rain&&rain.isEmitting;
        public int LiveDrops=>rain?rain.particleCount:0;
        public Transform[] CounterweightTanks=>counterweightTanks;
        public Transform[] WaterLevels=>visibleWaterLevels;
        public Transform[] WaterSurfaces=>waterSurfaces;
        public Transform[] Pulleys=>pulleys;
        public LineRenderer[] Cables=>cables;
        public Transform[] Collectors=>collectors;
        public Transform[] FlowDrops=>flowDrops;
        public float CounterweightTopY=>counterweightTopY;
        public float CounterweightBottomY=>counterweightBottomY;
        public float TankWaterDepth=>tankWaterDepth;
        private float flowTime;
        private bool wasRaining;

        public void Initialize(SkywellController owner,Transform[] tanks,Transform[] volumes,Transform[] surfaces,Transform[] inlets,
            Transform[] wheels,Transform[] anchors,Transform[] markers,LineRenderer[] ropes,Transform[] roofs,Transform[] gutters,
            Transform[] drops,LineRenderer[] pipes,LineRenderer[] streams,LineRenderer[] waves,ParticleSystem shower)
        {
            court=owner;counterweightTanks=tanks;visibleWaterLevels=volumes;waterSurfaces=surfaces;tankInlets=inlets;
            pulleys=wheels;liftCableAnchors=anchors;cableMarkers=markers;cables=ropes;collectors=roofs;gutterOutlets=gutters;
            flowDrops=drops;downpipes=pipes;waterStreams=streams;ripples=waves;rain=shower;
            pipeFlowSpeed=4;
            Refresh(0);
        }
        private void LateUpdate()=>Refresh(Time.deltaTime);
        public void Refresh(float dt)
        {
            if(!court||!rain||counterweightTanks==null)return;
            bool raining=Application.isPlaying&&court.RainActive;
            flowTime=raining?flowTime+dt:0;
            for(int i=0;i<counterweightTanks.Length;i++)
            {
                var tank=counterweightTanks[i];var p=tank.localPosition;
                p.y=Mathf.Lerp(counterweightTopY,counterweightBottomY,court.LiftTravelNormalized);tank.localPosition=p;
                float depth=Mathf.Max(.025f,court.StoredWaterNormalized*tankWaterDepth);
                visibleWaterLevels[i].localPosition=new Vector3(0,.15f+depth*.5f,0);
                visibleWaterLevels[i].localScale=new Vector3(1.48f,depth,1.48f);
                waterSurfaces[i].localPosition=new Vector3(0,.15f+depth,0);
                pulleys[i].localRotation=Quaternion.Euler(0,0,-(court.Height-court.PlatformLowY)*pulleyRotationPerMeter);
                UpdateCable(i);
                Vector3 start=gutterOutlets[i].position,end=tankInlets[i].position;
                downpipes[i].SetPosition(0,start);downpipes[i].SetPosition(1,end);
                bool flowing=raining&&court.CollectorInRain(collectors[i]);
                waterStreams[i].enabled=flowing;
                waterStreams[i].SetPosition(0,start);waterStreams[i].SetPosition(1,end);
                var ripple=ripples[i];ripple.enabled=flowing;
                if(flowing)
                {
                    float radius=.15f+Mathf.Repeat(flowTime*1.4f+i*.23f,1)*.55f;
                    for(int k=0;k<ripple.positionCount;k++)
                    {float a=k*2*Mathf.PI/ripple.positionCount;ripple.SetPosition(k,collectors[i].position+new Vector3(Mathf.Cos(a)*radius,.12f,Mathf.Sin(a)*radius));}
                }
                int perWing=flowDrops.Length/counterweightTanks.Length;
                for(int d=0;d<perWing;d++)
                {
                    var drop=flowDrops[i*perWing+d];drop.gameObject.SetActive(flowing);
                    if(!flowing)continue;
                    Vector3 roof=collectors[i].position+Vector3.up*.12f;
                    float gutterLength=Vector3.Distance(roof,start),pipeLength=Vector3.Distance(start,end);
                    float distance=Mathf.Repeat(flowTime*pipeFlowSpeed+d*(gutterLength+pipeLength)/perWing,gutterLength+pipeLength);
                    drop.position=distance<gutterLength?Vector3.Lerp(roof,start,distance/gutterLength):Vector3.Lerp(start,end,(distance-gutterLength)/pipeLength);
                }
            }
            if(raining&&!wasRaining)
            {
                if(court.CameraRig)court.CameraRig.ShowPhysicalFeedback(court.Origin+new Vector3(6,3,-6),3,.25f,1.1f);
            }
            wasRaining=raining;
        }
        private void UpdateCable(int i)
        {
            Vector3 center=pulleys[i].parent.position,radial=pulleys[i].parent.right;
            var line=cables[i];int arcPoints=line.positionCount-2;
            line.SetPosition(0,liftCableAnchors[i].position);
            for(int k=0;k<arcPoints;k++)
            {
                float angle=Mathf.PI-k*Mathf.PI/(arcPoints-1);
                line.SetPosition(k+1,center+radial*(Mathf.Cos(angle)*pulleyRadius)+Vector3.up*(Mathf.Sin(angle)*pulleyRadius));
            }
            Vector3 tankAnchor=counterweightTanks[i].TransformPoint(new Vector3(0,4.55f,0));
            line.SetPosition(line.positionCount-1,tankAnchor);
            // A contrasting cable ferrule moves with the same material point, making cable motion legible without texture scrolling.
            Vector3 top=center-radial*pulleyRadius;
            cableMarkers[i].position=Vector3.Lerp(liftCableAnchors[i].position,top,.12f);
        }
        private void OnDisable()
        {
            if(ripples!=null)foreach(var line in ripples)if(line)line.enabled=false;
            if(waterStreams!=null)foreach(var line in waterStreams)if(line)line.enabled=false;
            if(flowDrops!=null)foreach(var drop in flowDrops)if(drop)drop.gameObject.SetActive(false);
            wasRaining=false;flowTime=0;
        }
        private void OnDrawGizmosSelected()
        {
            if(!court)return;Gizmos.color=new Color(.15f,.7f,1,.5f);
            Vector3 previous=court.WeatherCenter+Vector3.right*court.WeatherRadius;
            for(int i=1;i<=64;i++){float a=i*Mathf.PI*2/64;var next=court.WeatherCenter+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*court.WeatherRadius;Gizmos.DrawLine(previous,next);previous=next;}
        }
    }
}
