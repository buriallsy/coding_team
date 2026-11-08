using UnityEngine;

namespace BorrowingSun
{
    // Feedback observes actual bell, optical and receiver events without controlling their gameplay states.
    public sealed class SkywellWorldFeedback : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform bell, handle;
        [SerializeField] private LineRenderer[] ripples;
        [SerializeField] private AudioSource bellSound;
        [Header("借晴结果观察 · 行走两米即可打断")]
        [SerializeField, Min(.1f)] private float firstBeamViewSeconds=3;
        [SerializeField, Range(1,5)] private float firstBeamViewScale=2;
        [SerializeField, Min(.1f)] private float villageViewSeconds=4.4f;
        [SerializeField, Range(1,5)] private float villageViewScale=3.5f;
        private SkywellFish fish;
        private AudioClip strike;
        private float ringUntil;
        private bool relayWasReached;
        private SettlementSunState previousVillageState=SettlementSunState.Unlit;
        public bool Ringing=>Time.time<ringUntil;
        public void Initialize(SkywellController c,Transform bellBody,Transform lever,LineRenderer[] waves,AudioSource audio)
        {court=c;bell=bellBody;handle=lever;ripples=waves;bellSound=audio;}
        private void Awake()=>EnsureAudio();
        private void EnsureAudio()
        {
            if(!fish)fish=GetComponent<SkywellFish>();
            if(strike)return;
            const int rate=22050;var samples=new float[rate];
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;samples[i]=.3f*(Mathf.Sin(t*2*Mathf.PI*520)+.35f*Mathf.Sin(t*2*Mathf.PI*1373))*Mathf.Exp(-t*6)*Mathf.Min(1,t*150);}
            strike=AudioClip.Create("Prototype_bell_strike",samples.Length,1,rate,false);strike.SetData(samples,0);
        }
        private void OnEnable(){EnsureAudio();if(fish){fish.LureSounded-=OnBell;fish.LureSounded+=OnBell;}if(court&&court.Journey){court.Journey.ResetOccurred-=ResetFeedback;court.Journey.ResetOccurred+=ResetFeedback;}}
        private void OnDisable(){if(fish)fish.LureSounded-=OnBell;if(court&&court.Journey)court.Journey.ResetOccurred-=ResetFeedback;}
        private void OnDestroy(){if(strike)Destroy(strike);}
        private void OnBell(Vector3 point)
        {
            EnsureAudio();ringUntil=Time.time+4;if(bellSound)bellSound.PlayOneShot(strike);
            if(court&&court.Journey&&court.Journey.InRefuge&&fish&&fish.Body) court.CameraRig.ShowPhysicalFeedback(Vector3.Lerp(point,fish.Body.position,.35f),7,.45f,1.3f);
        }
        public void ResetFeedback()
        {
            ringUntil=0;relayWasReached=false;previousVillageState=SettlementSunState.Unlit;
            if(bellSound)bellSound.Stop();if(court)court.CameraRig.ClearPhysicalFeedback();Refresh();
        }
        private void LateUpdate(){Refresh();ObserveBorrowedSun();}
        private void ObserveBorrowedSun()
        {
            if(!court||!court.CameraRig||!court.Optics)return;
            var optics=court.Optics;var collector=optics.Collector;var receiver=optics.SettlementReceiver;
            bool reached=collector&&collector.Active&&collector.IsLocked&&optics.HitsMovingMirror;
            var state=receiver?receiver.State:SettlementSunState.Unlit;
            // The village event takes priority if both physical results arrive in the same frame.
            if(receiver&&optics.MovingMirror&&state==SettlementSunState.Spreading&&previousVillageState!=SettlementSunState.Spreading)
            {
                var target=Vector3.Lerp(optics.MovingMirror.position,receiver.transform.position,.85f);
                court.CameraRig.ShowPhysicalFeedback(target,villageViewSeconds,.8f,villageViewScale);
            }
            else if(reached&&!relayWasReached&&optics.MovingMirror)
            {
                var target=Vector3.Lerp(collector.OutputOrigin,optics.MovingMirror.position,.5f);
                court.CameraRig.ShowPhysicalFeedback(target,firstBeamViewSeconds,.7f,firstBeamViewScale);
            }
            relayWasReached=reached;previousVillageState=state;
        }
        private void Refresh()
        {
            float remaining=ringUntil-Time.time;
            if(bell)bell.localRotation=Quaternion.Euler(0,0,Ringing?Mathf.Sin(remaining*17)*18*Mathf.Clamp01(remaining/4):0);
            if(handle)handle.localRotation=Quaternion.Euler(Ringing?Mathf.Sin(remaining*17)*22*Mathf.Clamp01(remaining/4):0,0,0);
            if(ripples==null)return;
            for(int i=0;i<ripples.Length;i++)
            {
                var line=ripples[i];if(!line)continue;line.enabled=Ringing;if(!Ringing||!court||!court.WindBell)continue;
                float phase=Mathf.Repeat((4-remaining)*.55f+i/(float)ripples.Length,1),radius=.5f+phase*4;
                var color=new Color(1,.7f,.2f,1-phase);line.startColor=line.endColor=color;
                for(int n=0;n<line.positionCount;n++){float angle=n*2*Mathf.PI/line.positionCount;line.SetPosition(n,court.WindBell.LurePosition+new Vector3(Mathf.Cos(angle)*radius,.1f,Mathf.Sin(angle)*radius));}
            }
        }
    }
}
