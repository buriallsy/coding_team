using BorrowingSun.Market;
using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Reads the existing world without writing weather, delivery or mechanism progress.</summary>
    internal sealed class SkywellForageEnvironment
    {
        private readonly Light sun;
        private readonly bool originalSunEnabled;
        private readonly RaycastHit[] hits = new RaycastHit[64];

        public SkywellForageEnvironment(SkywellController court)
        {
            sun = court && court.Optics && court.Optics.Collector
                ? court.Optics.Collector.SunLight : RenderSettings.sun;
            originalSunEnabled = sun && sun.enabled;
        }

        public void ApplyNight(bool night)
        {
            if (sun) sun.enabled = !night && originalSunEnabled;
        }

        public void Restore() { if (sun) sun.enabled = originalSunEnabled; }

        public ForageEnvironment Sample(Transform point, SkywellWeatherZone localRain, bool night)
        {
            bool rain = point && localRain && localRain.isActiveAndEnabled
                && localRain.IsRain && localRain.Contains(point.position);
            return new ForageEnvironment(night, rain, !night && HasDirectSun(point));
        }

        private bool HasDirectSun(Transform point)
        {
            if (!point || !sun || !sun.enabled || sun.intensity <= 0 || sun.type != LightType.Directional) return false;
            Vector3 direction = -sun.transform.forward;
            if (direction.y <= .01f) return false;
            Vector3 origin = point.position + Vector3.up * .4f + direction * .08f;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, 250, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].transform;
                if (hit != point && !hit.IsChildOf(point)) return false;
            }
            return true;
        }
    }
}
