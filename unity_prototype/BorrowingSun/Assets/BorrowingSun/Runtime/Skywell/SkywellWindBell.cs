using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Local wind moves the real sound source along its rail; its position remains when the wind stops.</summary>
    [DefaultExecutionOrder(-95)]
    public sealed class SkywellWindBell : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform vent; // Keep the serialized key; this Transform is the wind bell.
        [SerializeField, Tooltip("The refuge station's local weather zone, linked to the bell drive.")]
        private SkywellWeatherZone windZone;
        [SerializeField] private bool surfaceWaterConfigured;
        [SerializeField, Tooltip("Bell centre relative to the court origin, above the visible basin water.")]
        private float surfaceBellOffset=-4.85f;
        [Header("水面风铃调参")]
        [Min(.1f), InspectorName("钟轨全程移动时间（秒）")] public float bellTravelDuration=3.333333f;
        private float bellPosition,bellTarget;
        private bool windPending;
        private SkywellFish fish;
        public bool WindSettled=>Mathf.Abs(bellPosition-bellTarget)<.01f;
        public float BellPosition=>bellPosition;
        public Vector3 LurePosition=>vent.position;
        public SkywellWeatherZone WindZone=>windZone;
        public bool SurfaceWaterConfigured=>surfaceWaterConfigured;
        public float TravelHeight=>(court?court.Origin.y:0)+(surfaceWaterConfigured?surfaceBellOffset:-9);
        public void ConfigureSurfaceWater(float waterWorldY)
        {
            if(!court)throw new System.InvalidOperationException("Initialize the bell's court before configuring its water level.");
            if(float.IsNaN(waterWorldY)||float.IsInfinity(waterWorldY))throw new System.ArgumentOutOfRangeException(nameof(waterWorldY));
            surfaceWaterConfigured=true;surfaceBellOffset=waterWorldY-court.Origin.y+.65f;ApplyBellPosition();
        }
        private void OnValidate(){bellTravelDuration=Mathf.Max(.1f,bellTravelDuration);}
        public void Initialize(SkywellController owner,Transform soundVent,SkywellWeatherZone zone)
        {court=owner;vent=soundVent;ConfigureWeatherZone(zone);}
        public void ConfigureWeatherZone(SkywellWeatherZone zone)
        {windZone=zone;if(windZone)windZone.RegisterResponder(this);}
        private void Awake(){fish=GetComponent<SkywellFish>();BindWeatherZone();}
        private void Start()=>BindWeatherZone();
        private void BindWeatherZone()
        {
            if(!windZone&&court)windZone=court.RefugeWeatherZone;
            if(windZone)windZone.RegisterResponder(this);
        }
        public void SelectEndpoint(int endpoint){bellTarget=Mathf.Clamp01(endpoint);windPending=true;}
        public void ResetBell(){bellPosition=bellTarget=0;windPending=false;ApplyBellPosition();}
        private void Update()
        {
            if(!vent||!court)return;
            if(!windZone)BindWeatherZone();
            if(windZone&&windZone.IsWind&&windZone.Contains(vent.position)&&windPending)
            {
                bellPosition=Mathf.MoveTowards(bellPosition,bellTarget,Time.deltaTime/Mathf.Max(.1f,bellTravelDuration));
                ApplyBellPosition();
                if(WindSettled)
                {
                    bellPosition=bellTarget;ApplyBellPosition();windPending=false;
                    if(!fish)fish=GetComponent<SkywellFish>();
                    if(fish)fish.LureAt(vent.position);
                }
            }
        }
        private void ApplyBellPosition()
        {
            if(!vent)return;
            if(surfaceWaterConfigured&&court)
                vent.position=court.Origin+new Vector3(Mathf.Lerp(-18,-5,bellPosition),surfaceBellOffset,-26);
            else vent.localPosition=new Vector3(Mathf.Lerp(-18,-5,bellPosition),-9,-26);
        }
    }
}
