using UnityEngine;

namespace BorrowingSun
{
    /// <summary>One authored viewpoint and its illustration; does not change weather or puzzle progress.</summary>
    public sealed class SkywellObservation : MonoBehaviour
    {
        [SerializeField] private Texture2D scenery;
        [SerializeField] private SkywellSunReceiver receiver;
        [SerializeField, Range(.5f, 2.7f)] private float interactionRadius = 2.2f;
        [SerializeField] private string title = "山阴村";
        [SerializeField, TextArea(2, 4)] private string shadedCaption =
            "屋里的灯亮着，山影却罩住了整座村子。高处的接光盘，还没有等到日光。";
        [SerializeField, TextArea(2, 4)] private string litCaption =
            "这是村庄尚未得光时的旧景。如今，借来的阳光已经抵达了这里。";

        public bool IsOpen { get; private set; }
        public bool HasViewed { get; private set; }
        public void ResetProgress(){Close();HasViewed=false;}
        public bool Available => isActiveAndEnabled && scenery;
        public float InteractionRadius => interactionRadius;
        public Texture2D Image => scenery;
        private bool VillageLit => receiver && receiver.RewardAvailable;
        public string Title => VillageLit ? title + " · 旧景" : title;
        public string Caption => VillageLit ? litCaption : shadedCaption;

        public void Configure(Texture2D image, SkywellSunReceiver villageReceiver)
        { scenery = image; receiver = villageReceiver; }

        public void Open() { if (Available) { IsOpen = true; HasViewed = true; } }
        public void Close() => IsOpen = false;
        private void OnDisable() => Close();
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .75f, .3f, .8f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
