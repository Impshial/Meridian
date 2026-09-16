using UnityEngine;
using UnityEngine.UI;

namespace Meridian
{
    public sealed class PlanetFlagIndicatorGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();Color tint=color;
            Vector2 bottom=new Vector2(-12,-20),top=new Vector2(8,17);
            Vector2 side=new Vector2(-(top-bottom).y,(top-bottom).x).normalized*1.35f;
            Quad(mesh,bottom-side,bottom+side,top+side,top-side,tint);
            Quad(mesh,new Vector2(7,16),new Vector2(29,11),new Vector2(21,-1),new Vector2(1,5),tint);
            Color dim=tint;dim.a=.3f;
            Quad(mesh,new Vector2(-20,-23),new Vector2(-20,-21),new Vector2(-4,-21),new Vector2(-4,-23),dim);
        }
        static void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int n=mesh.currentVertCount;mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);
            mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
