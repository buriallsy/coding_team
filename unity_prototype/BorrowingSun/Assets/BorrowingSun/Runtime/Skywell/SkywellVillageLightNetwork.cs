using UnityEngine;

namespace BorrowingSun
{
    /// <summary>Receiver-owned payoff sequence; it never awards cargo or creates gameplay sunlight.</summary>
    public sealed class SkywellVillageLightNetwork : MonoBehaviour
    {
        [SerializeField] private Renderer[] nodes;
        [SerializeField] private Light[] nodeLights;
        [Min(.1f),SerializeField] private float secondsPerNode=.55f;
        [SerializeField] private Color coldColor=new Color(.18f,.3f,.37f),warmColor=new Color(1,.7f,.26f);
        [Range(0,5),SerializeField] private float warmIntensity=1.3f;
        private MaterialPropertyBlock tint;
        private float elapsed;
        public int NodeCount=>nodes==null?0:nodes.Length;
        public int LitNodeCount{get;private set;}
        public bool Complete=>NodeCount>0&&LitNodeCount==NodeCount;
        public float Duration=>NodeCount*secondsPerNode;
        public float Progress=>NodeCount>0?(float)LitNodeCount/NodeCount:0;
        public void Advance(float deltaTime)
        {
            elapsed+=Mathf.Max(0,deltaTime);LitNodeCount=Mathf.Min(NodeCount,Mathf.FloorToInt(elapsed/Mathf.Max(.1f,secondsPerNode)));Refresh();
        }
        public void ResetNetwork(){elapsed=0;LitNodeCount=0;Refresh();}
        private void Refresh()
        {
            if(tint==null)tint=new MaterialPropertyBlock();
            for(int i=0;i<NodeCount;i++)
            {
                bool lit=i<LitNodeCount;
                if(nodes[i]){tint.SetColor("_BaseColor",lit?warmColor:coldColor);tint.SetColor("_EmissionColor",lit?warmColor:Color.black);nodes[i].SetPropertyBlock(tint);}
                if(nodeLights!=null&&i<nodeLights.Length&&nodeLights[i]){nodeLights[i].enabled=lit;nodeLights[i].intensity=lit?warmIntensity:0;nodeLights[i].color=warmColor;}
            }
        }
    }
}
