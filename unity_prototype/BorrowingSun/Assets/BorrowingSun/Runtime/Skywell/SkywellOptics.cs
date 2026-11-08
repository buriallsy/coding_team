using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Finite apertures, physical reflections and solid occluders. Only the solar collector supplies light.</summary>
    public sealed class SkywellOptics : MonoBehaviour
    {
        [SerializeField] private SkywellSunCollector collector;
        [SerializeField] private Transform movingMirror, fixedMirror, receiver;
        [SerializeField] private LineRenderer[] beams;
        [SerializeField] private SkywellSunReceiver settlementReceiver;
        [Header("实际光路")]
        [Min(1),SerializeField] private float maxRayDistance=180;
        [Range(1,6),SerializeField] private int reflectionLimit=3;
        [SerializeField] private LayerMask occlusionMask=~0;
        [SerializeField] private Vector2 movingAperture=new Vector2(1.5f,1.5f),fixedAperture=new Vector2(1.05f,1.05f);
        [SerializeField] private Renderer relaySurface;
        [SerializeField] private bool drawDebugRays=true;
        private readonly RaycastHit[] hits=new RaycastHit[96];
        private MaterialPropertyBlock tint;
        public SkywellSunCollector Collector=>collector;
        public bool HitsMovingMirror{get;private set;}
        public bool HitsFixedMirror{get;private set;}
        public string LastObstruction{get;private set;}
        public SkywellSunReceiver SettlementReceiver=>settlementReceiver;
        public bool SettlementLit{get;private set;}
        public bool Lit{get;private set;}
        public int Reflections{get;private set;}
        public bool UsesFixedMirror{get;private set;}
        public Transform MovingMirror=>movingMirror;
        public Transform FixedMirror=>fixedMirror;
        public Transform Receiver=>receiver;
        public Vector3 SourcePoint=>collector?collector.OutputOrigin:transform.position;
        public float MaxRayDistance=>maxRayDistance;
        public void ResetSettlement(){SettlementLit=false;if(settlementReceiver)settlementReceiver.ResetReceiver();}
        public void ResetOptics(){if(collector)collector.ResetCollector();Evaluate(false);ResetSettlement();}
        public void Evaluate(bool enabled)
        {
            Lit=false;Reflections=0;UsesFixedMirror=false;SettlementLit=false;HitsMovingMirror=HitsFixedMirror=false;LastObstruction=null;
            if(beams!=null)foreach(var line in beams)if(line)line.enabled=false;
            float dt=Application.isPlaying?Time.deltaTime:0;
            if(collector)collector.Tick(dt);
            if(enabled&&collector&&collector.Active&&settlementReceiver&&beams!=null)
            {
                Vector3 p=collector.OutputOrigin,d=collector.OutputDirection;
                Transform previous=null;
                for(int bounce=0;bounce<beams.Length&&bounce<=reflectionLimit;bounce++)
                {
                    float distance=maxRayDistance;Transform next=null;bool reflective=false;
                    for(int m=0;m<2;m++)
                    {
                        var mirror=m==0?movingMirror:fixedMirror;
                        if(!mirror||mirror==previous||!Intersection(p,d,mirror,m==0?movingAperture:fixedAperture,out float t)||t>=distance)continue;
                        distance=t;next=mirror;reflective=Vector3.Dot(d,mirror.forward)>0;
                    }
                    bool reaches=settlementReceiver.Intersect(p,d,out float along)&&along<distance;
                    if(reaches){distance=along;next=null;}
                    int count=Physics.RaycastNonAlloc(p+d*.025f,d,hits,Mathf.Max(0,distance-.05f),occlusionMask,QueryTriggerInteraction.Ignore);
                    if(count==hits.Length){LastObstruction="遮挡查询容量不足";next=null;reaches=false;distance=0;}
                    for(int i=0;i<count;i++)
                    {
                        var hit=hits[i];var t=hit.transform;
                        // Only optical surfaces are excluded. Terrain and collector architecture remain real blockers.
                        if(collector.IsCollectorCollider(hit.collider)||IsPartOf(t,movingMirror)||IsPartOf(t,fixedMirror)||IsPartOf(t,receiver))continue;
                        float alongHit=hit.distance+.025f;
                        if(alongHit>=distance)continue;
                        distance=alongHit;next=null;reaches=false;LastObstruction=t.name;
                    }
                    var line=beams[bounce];if(line){line.enabled=true;line.positionCount=2;line.SetPosition(0,p);line.SetPosition(1,p+d*distance);}
                    if(reaches){Lit=Reflections>0&&HitsMovingMirror;UsesFixedMirror=HitsFixedMirror;SettlementLit=Lit;break;}
                    if(!next||!reflective||Reflections>=reflectionLimit)break;
                    HitsMovingMirror|=next==movingMirror;HitsFixedMirror|=next==fixedMirror;
                    p+=d*distance;d=Vector3.Reflect(d,next.forward).normalized;previous=next;Reflections++;
                }
            }
            if(settlementReceiver)settlementReceiver.ReceiveGameplayRay(SettlementLit,dt);
            if(relaySurface)
            {
                if(tint==null)tint=new MaterialPropertyBlock();
                var c=HitsMovingMirror?new Color(1,.85f,.4f):new Color(.34f,.58f,.64f);
                tint.SetColor("_BaseColor",c);tint.SetColor("_EmissionColor",HitsMovingMirror?c*.5f:Color.black);relaySurface.SetPropertyBlock(tint);
            }
        }
        private static bool IsPartOf(Transform candidate,Transform root)=>root&&(candidate==root||candidate.IsChildOf(root));
        public static bool Intersection(Vector3 p,Vector3 d,Transform mirror,Vector2 halfAperture,out float distance)
        {
            distance=0;if(!mirror)return false;
            float dot=Vector3.Dot(d,mirror.forward);if(Mathf.Abs(dot)<=.0001f)return false;
            distance=Vector3.Dot(mirror.position-p,mirror.forward)/dot;if(distance<.05f)return false;
            Vector3 point=mirror.InverseTransformPoint(p+d*distance);
            return Mathf.Abs(point.x)<=halfAperture.x&&Mathf.Abs(point.y)<=halfAperture.y;
        }
        private void OnDrawGizmosSelected()
        {
            if(!drawDebugRays)return;
            Gizmos.color=Lit?Color.yellow:Color.cyan;
            if(beams!=null)foreach(var line in beams)if(line&&line.enabled&&line.positionCount>=2)Gizmos.DrawLine(line.GetPosition(0),line.GetPosition(1));
            if(movingMirror){Gizmos.color=new Color(1,.4f,.1f);Gizmos.DrawWireSphere(movingMirror.position,movingAperture.magnitude);}
        }
    }
}
