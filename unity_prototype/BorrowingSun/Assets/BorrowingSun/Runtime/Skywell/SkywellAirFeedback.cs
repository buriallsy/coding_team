using UnityEngine;
namespace BorrowingSun
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SkywellAirFeedback : MonoBehaviour
    {
        private SkywellJourney journey;private AudioSource source;private AudioClip warning;private float nextPulse;private int previousBand;
        public int WarningPulses {get;private set;}
        private void Awake()
        {
            journey=GetComponent<SkywellJourney>();source=GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=.18f;
            const int rate=22050;var samples=new float[rate/4];
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;samples[i]=Mathf.Sin(t*2*Mathf.PI*420)*Mathf.Sin(Mathf.PI*i/samples.Length)*.5f;}
            warning=AudioClip.Create("Underwater air warning",samples.Length,1,rate,false);warning.SetData(samples,0);
        }
        private void Update()
        {
            if(!journey||journey.SurfaceConfigured){if(source.isPlaying)source.Stop();previousBand=0;return;}
            int band=!journey.BelowGroundActive||journey.HasBreathableAir?0:journey.Air<=journey.criticalAirThreshold?2:journey.Air<=journey.lowAirThreshold?1:0;
            if(band>0&&(band!=previousBand||Time.time>=nextPulse)){source.PlayOneShot(warning);WarningPulses++;nextPulse=Time.time+(band==2?1.4f:3.2f);}
            previousBand=band;
        }
        private void OnDestroy(){if(warning)Destroy(warning);}
    }
}
