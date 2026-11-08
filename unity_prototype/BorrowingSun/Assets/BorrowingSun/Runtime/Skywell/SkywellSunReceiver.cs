using UnityEngine;

namespace BorrowingSun
{
    public enum SettlementSunState { Unlit=0, Receiving=1, Reward=2, Spreading=3 }
    /// <summary>Receives verified reflected gameplay rays only; global lighting is never queried.</summary>
    public sealed class SkywellSunReceiver : MonoBehaviour
    {
        [Header("有效反射光接收")]
        [Range(.5f,5),Tooltip("圆形接收面的有效半径（米），只接受正面射线。")]
        public float apertureRadius=1.4f;
        [Range(.2f,20),Tooltip("连续接光确认时间；随后光依次传入村庄，完成才装车。中途断光重试，不影响已取得奖励。")]
        public float receivingSeconds=4;
        [Range(1,10),Tooltip("水果装车后，右下角获得提示停留的秒数。")]
        public float rewardToastSeconds=4;
        [Header("白盒反馈")]
        public Renderer crystal;
        public Renderer[] chargeMarkers;
        public GameObject fruitCrate;
        public Light settlementFill;
        [SerializeField] private SkywellVillageLightNetwork villageNetwork;
        [Range(0,5)] public float receivingWarmth=1.5f;
        public SettlementSunState State{get;private set;}
        public float Progress{get;private set;}
        public bool RayValid{get;private set;}
        public bool RewardAvailable=>State==SettlementSunState.Reward;
        public SkywellVillageLightNetwork VillageNetwork=>villageNetwork;
        public bool RewardToastVisible=>RewardAvailable&&Time.unscaledTime<rewardToastUntil;
        private float rewardToastUntil;
        private MaterialPropertyBlock tint;
        private void Awake(){ResetReceiver();}
        public bool Intersect(Vector3 origin,Vector3 direction,out float distance)
        {
            distance=0;float facing=Vector3.Dot(direction,transform.forward);
            if(facing>=-.0001f)return false;
            distance=Vector3.Dot(transform.position-origin,transform.forward)/facing;
            if(distance<.05f)return false;
            return Vector3.Distance(origin+direction*distance,transform.position)<=Mathf.Max(.1f,apertureRadius);
        }
        // Called only by SkywellOptics after aperture, reflection and solid-occluder checks.
        public void ReceiveGameplayRay(bool valid,float deltaTime)
        {
            RayValid=valid;
            if(!RewardAvailable)
            {
                if(!valid){Progress=0;State=SettlementSunState.Unlit;if(villageNetwork)villageNetwork.ResetNetwork();}
                else if(Progress<1){Progress=Mathf.Min(1,Progress+Mathf.Max(0,deltaTime)/Mathf.Max(.1f,receivingSeconds));State=Progress>=1?SettlementSunState.Spreading:SettlementSunState.Receiving;}
                else
                {
                    State=SettlementSunState.Spreading;
                    if(villageNetwork){villageNetwork.Advance(deltaTime);if(villageNetwork.Complete)State=SettlementSunState.Reward;}
                }
                if(RewardAvailable)rewardToastUntil=Time.unscaledTime+rewardToastSeconds;
            }
            RefreshVisuals();
        }
        public void ResetReceiver(){State=SettlementSunState.Unlit;Progress=0;RayValid=false;rewardToastUntil=0;if(villageNetwork)villageNetwork.ResetNetwork();RefreshVisuals();}
        private void RefreshVisuals()
        {
            if(tint==null)tint=new MaterialPropertyBlock();
            bool alive=RayValid||RewardAvailable;
            Paint(crystal,alive?new Color(1,.75f,.25f):new Color(.12f,.22f,.3f));
            if(chargeMarkers!=null)for(int i=0;i<chargeMarkers.Length;i++)
            {bool lit=(i+1f)/chargeMarkers.Length<=Progress;Paint(chargeMarkers[i],lit?new Color(1,.65f,.12f):new Color(.12f,.18f,.22f));}
            if(fruitCrate)fruitCrate.SetActive(RewardAvailable);
            if(settlementFill){settlementFill.enabled=alive;settlementFill.intensity=receivingWarmth*(RewardAvailable?1:Progress);}
        }
        private void Paint(Renderer r,Color color)
        {if(!r)return;tint.SetColor("_BaseColor",color);r.SetPropertyBlock(tint);}
    }
}
