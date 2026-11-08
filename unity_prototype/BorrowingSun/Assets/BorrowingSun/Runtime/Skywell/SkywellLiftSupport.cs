using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Presentation below the deck. SkywellController remains the movement and water authority.</summary>
    public sealed class SkywellLiftSupport : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform piston, waterGauge;
        public Transform Piston => piston;
        public Transform WaterGauge => waterGauge;

        public void Initialize(SkywellController controller, Transform ram, Transform gauge)
        { court=controller; piston=ram; waterGauge=gauge; Refresh(); }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if(!court || !piston || !waterGauge) return;
            // A primitive cylinder is two units tall. Its upper end stops below the moving deck.
            float top=court.Height-.42f, bottom=-3f;
            piston.localPosition=new Vector3(0,(top+bottom)*.5f,0);
            piston.localScale=new Vector3(3.2f,(top-bottom)*.5f,3.2f);
            float fill=Mathf.Max(.04f,court.StoredWaterNormalized*6);
            waterGauge.localPosition=new Vector3(0,-9.3f+fill*.5f,-4.88f);
            waterGauge.localScale=new Vector3(.3f,fill,.08f);
        }
    }
}
