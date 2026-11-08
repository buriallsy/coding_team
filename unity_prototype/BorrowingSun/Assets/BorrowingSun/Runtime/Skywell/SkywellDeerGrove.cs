using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Owns the optional tree/fruit/gratitude progression. Weather and riding remain separately owned.</summary>
    public sealed class SkywellDeerGrove : MonoBehaviour
    {
        public const int StationIndex=4;
        [SerializeField] private SkywellController court;
        [SerializeField] private SkywellWeatherZone zone;
        [SerializeField] private Transform treeGrowthRoot,deerHead,cloudPlatformsRoot;
        [SerializeField] private Transform deerNeck,deerMuzzle;
        [SerializeField] private Transform[] smallFruit=Array.Empty<Transform>(),grownFruit=Array.Empty<Transform>();
        [SerializeField] private Collider[] fruitLandingSurfaces=Array.Empty<Collider>();
        [SerializeField] private SkywellDeerRide ride;
        [Header("林间支线 / Grove")]
        [SerializeField,Min(.1f)] private float growthSeconds=2.5f;
        [SerializeField,Range(1.05f,2)] private float grownScale=1.25f;
        [SerializeField,Min(.1f)] private float deerWalkSpeed=1.8f;
        [SerializeField,Range(2.8f,5)] private float deerOrbitRadius=3.5f;
        [SerializeField,Min(.1f)] private float deerOrbitSpeed=1.05f;
        [SerializeField,Min(.2f)] private float feedingSeconds=2.2f,cloudRevealSeconds=1.6f;
        [SerializeField,Min(.1f)] private float fruitGravity=10;
        [SerializeField,Min(0)] private float fruitWindDrift=.45f;
        private Fruit[] fruit=Array.Empty<Fruit>();
        private Collider[] cloudColliders=Array.Empty<Collider>();
        private bool[] cloudColliderEnabled=Array.Empty<bool>();
        private Vector3 treeStartScale;
        private Transform[] cloudVisuals=Array.Empty<Transform>();
        private Vector3[] cloudVisualScales=Array.Empty<Vector3>();
        private Quaternion headStartRotation,neckStartRotation;
        private Vector3 headStartPosition,neckStartPosition,neckStartScale,neckShoulderLocal,neckHeadOffset,muzzleHeadOffset;
        private bool initialized,smallReleased,matureReleased;
        private float growth,phaseTime,orbitAngle;
        private Vector3 mealPosition,mealStartScale;
        private Fruit mealFruit;
        private GrovePhase phase;
        private readonly RaycastHit[] hits=new RaycastHit[24];
        private enum GrovePhase { Waiting, Approaching, Feeding, Revealing, Ready }
        private sealed class Fruit
        {
            public Transform visual,parent;
            public Vector3 localPosition,localScale,velocity;
            public Quaternion localRotation;
            public bool mature,falling,landed;
        }
        public SkywellWeatherZone Zone=>zone;
        public SkywellDeerRide Ride=>ride;
        public Transform Tree=>treeGrowthRoot;
        public Transform CloudPlatforms=>cloudPlatformsRoot;
        public Transform FeedingNeck=>deerNeck;
        public Transform FeedingMuzzle=>deerMuzzle;
        public Vector3 FeedingPoint=>mealPosition;
        public int EatenFruitCount { get { int n=0;foreach(var f in fruit)if(f.mature&&f.landed&&f.visual&&!f.visual.gameObject.activeSelf)n++;return n; } }
        public float Growth=>growth;
        public bool IsGrown=>growth>=1;
        public bool SmallFruitDropped=>smallReleased;
        public bool MatureFruitDropped=>matureReleased;
        public bool IsApproachingFruit=>phase==GrovePhase.Approaching;
        public bool IsThanking=>phase==GrovePhase.Approaching||phase==GrovePhase.Feeding||phase==GrovePhase.Revealing;
        public bool Unlocked=>phase==GrovePhase.Ready;
        public int LandedMatureFruitCount { get { int n=0;foreach(var f in fruit)if(f.mature&&f.landed)n++;return n; } }
        public int VisibleFruitCount { get { int n=0;foreach(var f in fruit)if(f.visual&&f.visual.gameObject.activeInHierarchy)n++;return n; } }
        public string Prompt=>ride&&ride.Prompt!=null?ride.Prompt:zone&&court&&zone.Contains(court.Player.FeetPosition)?phase==GrovePhase.Approaching?"鹿正走向落下的苹果。":phase==GrovePhase.Feeding?"鹿正吃着落下的苹果。":phase==GrovePhase.Revealing?"鹿踏起云雾，云台正在显现。":smallReleased&&!IsGrown?"小鹿望着稀疏的枝头。":null:null;

        public void Configure(SkywellController owner,SkywellWeatherZone weatherZone,Transform tree,Transform[] initialFruit,
            Transform[] matureFruit,Collider[] landingSurfaces,Transform head,Transform clouds,SkywellDeerRide deerRide)
        {
            court=owner;zone=weatherZone;treeGrowthRoot=tree;smallFruit=initialFruit??Array.Empty<Transform>();
            grownFruit=matureFruit??Array.Empty<Transform>();fruitLandingSurfaces=landingSurfaces??Array.Empty<Collider>();
            deerHead=head;cloudPlatformsRoot=clouds;ride=deerRide;initialized=false;
            if(zone)zone.RegisterResponder(this);
            EnsureInitialState();
        }
        public void ConfigureFeedingVisuals(Transform neck,Transform muzzle)
        {deerNeck=neck;deerMuzzle=muzzle;CaptureFeedingPose();}
        private void Awake(){EnsureInitialState();if(zone)zone.RegisterResponder(this);}
        private void EnsureInitialState()
        {
            if(initialized)return;
            initialized=true;treeStartScale=treeGrowthRoot?treeGrowthRoot.localScale:Vector3.one;
            CaptureFeedingPose();
            var visuals=new List<Transform>();
            if(cloudPlatformsRoot)foreach(var renderer in cloudPlatformsRoot.GetComponentsInChildren<Renderer>(true))
                if(!visuals.Contains(renderer.transform))visuals.Add(renderer.transform);
            cloudVisuals=visuals.ToArray();cloudVisualScales=new Vector3[cloudVisuals.Length];
            for(int i=0;i<cloudVisuals.Length;i++)cloudVisualScales[i]=cloudVisuals[i].localScale;
            ResetOrbitAngle();
            cloudColliders=cloudPlatformsRoot?cloudPlatformsRoot.GetComponentsInChildren<Collider>(true):Array.Empty<Collider>();
            cloudColliderEnabled=new bool[cloudColliders.Length];
            for(int i=0;i<cloudColliders.Length;i++)cloudColliderEnabled[i]=cloudColliders[i].enabled;
            fruit=new Fruit[smallFruit.Length+grownFruit.Length];
            for(int i=0;i<fruit.Length;i++)
            {
                bool mature=i>=smallFruit.Length;var t=mature?grownFruit[i-smallFruit.Length]:smallFruit[i];
                fruit[i]=new Fruit{visual=t,mature=mature,parent=t?t.parent:null,localPosition=t?t.localPosition:Vector3.zero,
                    localScale=t?t.localScale:Vector3.one,localRotation=t?t.localRotation:Quaternion.identity};
            }
        }
        private void Update()
        {
            if(!zone||!treeGrowthRoot||!ride)return;
            EnsureInitialState();float dt=Time.deltaTime;bool inside=zone.Contains(treeGrowthRoot.position);
            if(inside&&zone.IsRain&&!IsGrown)
            {
                growth=Mathf.MoveTowards(growth,1,dt/Mathf.Max(.1f,growthSeconds));
                treeGrowthRoot.localScale=treeStartScale*Mathf.Lerp(1,grownScale,Mathf.SmoothStep(0,1,growth));
                if(IsGrown)foreach(var f in fruit)if(f.mature&&f.visual)f.visual.gameObject.SetActive(true);
            }
            if(inside&&zone.IsWind)
            {
                if(!smallReleased){smallReleased=true;ReleaseFruit(false);}
                if(IsGrown&&!matureReleased){matureReleased=true;ReleaseFruit(true);}
            }
            TickFruit(dt);
            TickGratitude(dt);
            if(phase==GrovePhase.Waiting)TickOrbit(dt);
        }
        private void ReleaseFruit(bool mature)
        {
            foreach(var f in fruit)
            {
                if(f.mature!=mature||!f.visual)continue;
                f.visual.SetParent(transform,true);f.visual.gameObject.SetActive(true);f.falling=true;
                // Once detached, gravity owns the fall. Stopping the wind cannot glue fruit back onto a branch.
                f.velocity=zone.WindDirection*fruitWindDrift;
            }
        }
        private void TickFruit(float dt)
        {
            foreach(var f in fruit)
            {
                if(!f.falling||!f.visual)continue;
                Vector3 from=f.visual.position;f.velocity+=Vector3.down*(fruitGravity*dt);Vector3 delta=f.velocity*dt;
                int count=Physics.RaycastNonAlloc(from,delta.normalized,hits,delta.magnitude+.13f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                float nearest=float.PositiveInfinity;RaycastHit landing=default;
                for(int i=0;i<count;i++)
                {
                    var hit=hits[i];if(hit.normal.y<.5f||hit.distance>=nearest||Array.IndexOf(fruitLandingSurfaces,hit.collider)<0)continue;
                    nearest=hit.distance;landing=hit;
                }
                if(nearest<float.PositiveInfinity)
                {
                    f.visual.position=landing.point+Vector3.up*.12f;f.falling=false;f.landed=true;f.velocity=Vector3.zero;
                    if(f.mature&&phase==GrovePhase.Waiting){mealFruit=f;mealPosition=f.visual.position;mealStartScale=f.visual.localScale;phase=GrovePhase.Approaching;phaseTime=0;}
                }
                else f.visual.position=from+delta;
            }
        }
        private void TickGratitude(float dt)
        {
            if(phase==GrovePhase.Waiting||phase==GrovePhase.Ready)return;
            var deer=ride.DeerTransform;if(!deer)return;
            if(phase==GrovePhase.Approaching)
            {
                Vector3 toward=Vector3.ProjectOnPlane(mealPosition-deer.position,Vector3.up);
                // World-space float rounding can leave a residual smaller than one position ULP.
                // Accept a 1 mm arrival band; movement still targets the original 1.2 m feeding distance.
                if(toward.magnitude>1.201f)
                {
                    Vector3 direction=toward.normalized;
                    // A deer returning from the far side goes around the trunk instead of crossing it.
                    Vector3 radial=Vector3.ProjectOnPlane(deer.position-treeGrowthRoot.position,Vector3.up);
                    float along=Mathf.Clamp01(-Vector3.Dot(radial,toward)/Mathf.Max(.001f,toward.sqrMagnitude));
                    if((radial+toward*along).magnitude<1.15f)
                    {
                        Vector3 goalRadial=Vector3.ProjectOnPlane(mealPosition-treeGrowthRoot.position,Vector3.up);
                        float sign=Mathf.Sign(Vector3.SignedAngle(radial,goalRadial,Vector3.up));
                        direction=Vector3.Cross(Vector3.up,radial.normalized)*(sign==0?1:sign);
                    }
                    Vector3 next=deer.position+direction*Mathf.Min(deerWalkSpeed*dt,toward.magnitude-1.2f);
                    ride.SetUnriddenPose(next,Quaternion.Slerp(deer.rotation,Quaternion.LookRotation(direction),dt*5));
                }
                else {phase=GrovePhase.Feeding;phaseTime=0;}
                return;
            }
            phaseTime+=dt;
            if(phase==GrovePhase.Feeding)
            {
                ride.SetUnriddenPose(deer.position,deer.rotation);
                AnimateFeeding(Mathf.Clamp01(phaseTime/Mathf.Max(.2f,feedingSeconds)));
                if(phaseTime<feedingSeconds)return;
                RestoreFeedingPose();
                phase=GrovePhase.Revealing;phaseTime=0;
                if(cloudPlatformsRoot){SetCloudReveal(.05f);cloudPlatformsRoot.gameObject.SetActive(true);}
                for(int i=0;i<cloudColliders.Length;i++)if(cloudColliders[i])cloudColliders[i].enabled=false;
                return;
            }
            float fraction=Mathf.Clamp01(phaseTime/Mathf.Max(.2f,cloudRevealSeconds));
            SetCloudReveal(Mathf.Lerp(.05f,1,Mathf.SmoothStep(0,1,fraction)));
            if(fraction<1)return;
            for(int i=0;i<cloudColliders.Length;i++)if(cloudColliders[i])cloudColliders[i].enabled=cloudColliderEnabled[i];
            phase=GrovePhase.Ready;ride.SetAvailable(true);
            if(court)court.ShowMessage("鹿向你低头致意，靠近后按 E 骑乘。",4);
        }
        private void CaptureFeedingPose()
        {
            headStartPosition=deerHead?deerHead.localPosition:Vector3.zero;
            headStartRotation=deerHead?deerHead.localRotation:Quaternion.identity;
            muzzleHeadOffset=deerHead&&deerMuzzle?deerHead.InverseTransformPoint(deerMuzzle.position):new Vector3(0,-.06f,.47f);
            if(!deerNeck)return;
            neckStartPosition=deerNeck.localPosition;neckStartRotation=deerNeck.localRotation;neckStartScale=deerNeck.localScale;
            neckShoulderLocal=neckStartPosition-neckStartRotation*Vector3.up*(neckStartScale.y*.5f);
            neckHeadOffset=deerHead?deerHead.InverseTransformPoint(deerNeck.TransformPoint(Vector3.up*.5f)):Vector3.zero;
        }
        private void RestoreFeedingPose()
        {
            if(deerHead){deerHead.localPosition=headStartPosition;deerHead.localRotation=headStartRotation;}
            if(deerNeck){deerNeck.localPosition=neckStartPosition;deerNeck.localRotation=neckStartRotation;deerNeck.localScale=neckStartScale;}
        }
        private void AnimateFeeding(float progress)
        {
            // Lower, visibly eat, then lift the head before the invitation. The mouth meets the real landed apple.
            float bend=progress<.3f?Mathf.SmoothStep(0,1,progress/.3f):progress>.72f?1-Mathf.SmoothStep(0,1,(progress-.72f)/.28f):1;
            if(deerHead&&deerHead.parent)
            {
                float chew=progress>=.3f&&progress<=.72f?Mathf.Sin((progress-.3f)*Mathf.PI*18)*2:0;
                Quaternion lowRotation=headStartRotation*Quaternion.Euler(25+chew,0,0);
                Vector3 lowPosition=deerHead.parent.InverseTransformPoint(mealPosition)-lowRotation*muzzleHeadOffset;
                deerHead.localPosition=Vector3.Lerp(headStartPosition,lowPosition,bend);
                deerHead.localRotation=Quaternion.Slerp(headStartRotation,lowRotation,bend);
                if(deerNeck&&deerNeck.parent)
                {
                    Vector3 top=deerNeck.parent.InverseTransformPoint(deerHead.TransformPoint(neckHeadOffset));
                    Vector3 axis=top-neckShoulderLocal;
                    deerNeck.localPosition=(neckShoulderLocal+top)*.5f;
                    if(axis.sqrMagnitude>.0001f)deerNeck.localRotation=Quaternion.FromToRotation(Vector3.up,axis);
                    deerNeck.localScale=new Vector3(neckStartScale.x,axis.magnitude,neckStartScale.z);
                }
            }
            if(mealFruit!=null&&mealFruit.visual)
            {
                float eaten=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.66f,progress));
                mealFruit.visual.localScale=mealStartScale*(1-eaten);
                if(eaten>=1)mealFruit.visual.gameObject.SetActive(false);
                // Keep landed=true: eating changes the visible apple, never the already-earned physical landing.
            }
        }
        private void ResetOrbitAngle()
        {
            if(!ride||!ride.DeerTransform||!treeGrowthRoot)return;
            Vector3 offset=ride.DeerTransform.position-treeGrowthRoot.position;
            orbitAngle=Mathf.Atan2(offset.x,offset.z);
        }
        private void TickOrbit(float dt)
        {
            var deer=ride.DeerTransform;if(!deer||ride.IsAvailable||ride.IsMounted)return;
            float radius=Mathf.Max(2.8f,deerOrbitRadius);
            orbitAngle+=Mathf.Max(.1f,deerOrbitSpeed)*dt/radius;
            Vector3 target=treeGrowthRoot.position+new Vector3(Mathf.Sin(orbitAngle)*radius,0,Mathf.Cos(orbitAngle)*radius);
            target.y=deer.position.y; // The authored circular route remains on the same island floor.
            Vector3 next=Vector3.MoveTowards(deer.position,target,deerOrbitSpeed*dt);
            Vector3 direction=next-deer.position;
            if(direction.sqrMagnitude<.000001f)return;
            ride.SetUnriddenPose(next,Quaternion.Slerp(deer.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-dt*6)));
        }
        private void SetCloudReveal(float fraction)
        {
            // Each mesh grows around its own authored centre; route roots and landing markers never move.
            for(int i=0;i<cloudVisuals.Length;i++)if(cloudVisuals[i])cloudVisuals[i].localScale=cloudVisualScales[i]*fraction;
        }
        public void ResetGrove()
        {
            EnsureInitialState();if(ride)ride.ResetRide();ResetOrbitAngle();growth=phaseTime=0;smallReleased=matureReleased=false;phase=GrovePhase.Waiting;mealPosition=Vector3.zero;mealFruit=null;mealStartScale=Vector3.one;
            if(treeGrowthRoot)treeGrowthRoot.localScale=treeStartScale;
            RestoreFeedingPose();
            foreach(var f in fruit)
            {
                f.falling=f.landed=false;f.velocity=Vector3.zero;if(!f.visual)continue;
                f.visual.SetParent(f.parent,false);f.visual.localPosition=f.localPosition;f.visual.localRotation=f.localRotation;f.visual.localScale=f.localScale;
                f.visual.gameObject.SetActive(!f.mature);
            }
            SetCloudReveal(1);if(cloudPlatformsRoot)cloudPlatformsRoot.gameObject.SetActive(false);
            for(int i=0;i<cloudColliders.Length;i++)if(cloudColliders[i])cloudColliders[i].enabled=cloudColliderEnabled[i];
        }
    }
}
