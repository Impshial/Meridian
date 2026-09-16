using UnityEngine;
using UnityEngine.UI;

namespace Meridian
{
    public sealed class ClickPulseGraphic : MaskableGraphic
    {
        float progress;
        public void SetProgress(float value){progress=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();const int segments=64;
            float radius=Mathf.Lerp(9,42,1-(1-progress)*(1-progress));
            const float halfStroke=.75f;Color tint=color;tint.a*=(1-progress)*(1-progress);
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                Vector2 v0=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),v1=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int start=mesh.currentVertCount;
                mesh.AddVert(v0*(radius-halfStroke),tint,Vector2.zero);mesh.AddVert(v0*(radius+halfStroke),tint,Vector2.zero);
                mesh.AddVert(v1*(radius+halfStroke),tint,Vector2.zero);mesh.AddVert(v1*(radius-halfStroke),tint,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
