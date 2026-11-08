using UnityEngine;

namespace BorrowingSun
{
    public enum SkywellToken { None, Rain, Wind, Sun }

    /// <summary>
    /// Local weather state and token ownership. The serialized component identity is retained
    /// for existing zone references; physical console visuals and input have been retired.
    /// </summary>
    public sealed class SkywellWeatherConsole : MonoBehaviour
    {
        [SerializeField] private SkywellWeatherZone zone;
        private const float TransitionDuration=1.2f;
        private float elapsed=TransitionDuration;
        private SkywellToken requestedWeather;
        public SkywellToken Token {get;private set;}
        public SkywellToken ActiveToken=>Busy?SkywellToken.None:Token;
        public SkywellToken ActiveWeather=>Busy?SkywellToken.None:requestedWeather;
        public bool Busy=>elapsed<TransitionDuration;
        public SkywellWeatherZone Zone=>zone;
        public void ConfigureZone(SkywellWeatherZone value)=>zone=value;
        public void Request(SkywellToken next)
        {
            if(Token==next&&requestedWeather==next&&!Busy)return;
            // Reclaiming Sunny must not interrupt the persistent local sunlight.
            if(next==SkywellToken.Sun&&requestedWeather==next&&!Busy){Token=next;return;}
            Token=requestedWeather=next;elapsed=0;
        }
        public SkywellToken ReturnToken()
        {
            SkywellToken returned=Token;
            if(returned==SkywellToken.None)return returned;
            Token=SkywellToken.None;
            if(requestedWeather!=SkywellToken.Sun){requestedWeather=SkywellToken.None;elapsed=0;}
            return returned;
        }
        public void ResetConsole(){Token=requestedWeather=SkywellToken.None;elapsed=TransitionDuration;}
        public void Tick(float delta)=>elapsed=Mathf.Min(TransitionDuration,elapsed+delta);
        public static string Name(SkywellToken t)=>t==SkywellToken.Rain?"雨令":t==SkywellToken.Wind?"风令":t==SkywellToken.Sun?"晴令":"无天气";
    }
}
