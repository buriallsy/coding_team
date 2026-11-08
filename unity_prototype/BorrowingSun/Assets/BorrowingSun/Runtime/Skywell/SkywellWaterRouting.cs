using UnityEngine;

namespace BorrowingSun
{
    // Presentation only: the controller owns the conserved reservoir-to-ballast transfer.
    public sealed class SkywellWaterRouting : MonoBehaviour
    {
        [SerializeField] private SkywellController court;
        [SerializeField] private Transform handle,outlet,inlet,bucket;
        [SerializeField] private Transform[] drops;
        public void Initialize(SkywellController owner,Transform wheel,Transform source,Transform bend,Transform destination,Transform[] markers)
        {court=owner;handle=wheel;outlet=source;inlet=bend;bucket=destination;drops=markers;}
        private void LateUpdate()
        {
            handle.localRotation=Quaternion.Euler(0,court.DrainOpen?90:0,0);
            for(int i=0;i<drops.Length;i++)
            {
                drops[i].gameObject.SetActive(court.WaterTransferring);
                if(!court.WaterTransferring)continue;
                float t=Mathf.Repeat(Time.time*.4f+(float)i/drops.Length,1);
                Vector3 bend=new Vector3(inlet.position.x,outlet.position.y,outlet.position.z);
                Vector3 end=bucket.position+Vector3.up*1.4f;
                drops[i].position=t<.4f?Vector3.Lerp(outlet.position,bend,t/.4f):t<.7f?Vector3.Lerp(bend,inlet.position,(t-.4f)/.3f):Vector3.Lerp(inlet.position,end,(t-.7f)/.3f);
            }
        }
    }
}
