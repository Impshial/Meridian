using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meridian
{
    /// <summary>Replaceable visual; the footprint and all placement decisions live in SurfaceParameters/LandingPlacement.</summary>
    public sealed class DropshipPreview : MonoBehaviour
    {
        private readonly List<Renderer> renderers=new List<Renderer>();
        private Material hullMaterial,lineMaterial;
        private LineRenderer landingOutline,accessOutline;
        private bool initialized;
        public void Initialize(SurfaceParameters settings,Material materialTemplate)
        {
            if(initialized)return;initialized=true;
            hullMaterial=new Material(materialTemplate){name="Survey Dropship Preview"};
            MakeTransparent(hullMaterial);
            lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Survey Footprint Outline"};
            float width=settings.shipWidth,length=settings.shipLength;
            Part("Cargo hull",PrimitiveType.Cube,new Vector3(0,3.8f,0),new Vector3(width*.66f,5.3f,length*.64f));
            Part("Armored nose",PrimitiveType.Sphere,new Vector3(0,3.9f,length*.32f),new Vector3(width*.65f,4.7f,length*.36f));
            Part("Command canopy",PrimitiveType.Cube,new Vector3(0,6.8f,length*.21f),new Vector3(width*.36f,1.3f,length*.25f));
            Part("Rear ramp",PrimitiveType.Cube,new Vector3(0,.7f,-length*.48f),new Vector3(width*.36f,.55f,length*.20f));
            for(int side=-1;side<=1;side+=2)
            {
                Part("Engine nacelle",PrimitiveType.Capsule,new Vector3(side*width*.4f,3,0),new Vector3(width*.19f,length*.34f,width*.19f),Quaternion.Euler(90,0,0));
                foreach(float z in new[]{-length*.28f,length*.24f})
                {
                    Part("Landing leg",PrimitiveType.Cylinder,new Vector3(side*width*.39f,1.15f,z),new Vector3(.7f,1.15f,.7f));
                    Part("Support pad",PrimitiveType.Cube,new Vector3(side*width*.39f,.15f,z),new Vector3(width*.185f,.3f,length*.12f));
                }
            }
            float margin=settings.safetyMargin;
            landingOutline=Outline("Landing safety perimeter",width+margin*2,length+margin*2,0);
            accessOutline=Outline("Deployment access",width+margin*2,settings.accessLength,-length*.5f-margin-settings.accessLength*.5f);
            SetState(false,false);
        }
        public void Present(Vector3 position,float heading,bool valid,bool confirmed)
        {
            transform.SetPositionAndRotation(position+Vector3.up*.08f,Quaternion.Euler(0,heading,0));
            if(!gameObject.activeSelf)gameObject.SetActive(true);
            SetState(valid,confirmed);
        }
        public void Hide()=>gameObject.SetActive(false);
        void SetState(bool valid,bool confirmed)
        {
            Color color=confirmed?new Color(1,.70f,.30f,.78f):valid?new Color(.25f,.85f,.62f,.58f):new Color(1,.25f,.22f,.52f);
            if(hullMaterial)hullMaterial.SetColor("_BaseColor",color);
            color.a=1;
            if(lineMaterial)lineMaterial.SetColor("_BaseColor",color);
        }
        void Part(string label,PrimitiveType shape,Vector3 position,Vector3 scale,Quaternion? rotation=null)
        {
            var part=GameObject.CreatePrimitive(shape);part.name=label;part.transform.SetParent(transform,false);
            part.transform.localPosition=position;part.transform.localScale=scale;part.transform.localRotation=rotation??Quaternion.identity;
            Destroy(part.GetComponent<Collider>());
            var renderer=part.GetComponent<Renderer>();renderer.sharedMaterial=hullMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderers.Add(renderer);
        }
        LineRenderer Outline(string label,float width,float length,float z)
        {
            var line=new GameObject(label,typeof(LineRenderer)).GetComponent<LineRenderer>();line.transform.SetParent(transform,false);
            line.useWorldSpace=false;line.loop=true;line.positionCount=4;line.widthMultiplier=.45f;
            line.sharedMaterial=lineMaterial;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.SetPositions(new[]{new Vector3(-width*.5f,.25f,z-length*.5f),new Vector3(width*.5f,.25f,z-length*.5f),
                new Vector3(width*.5f,.25f,z+length*.5f),new Vector3(-width*.5f,.25f,z+length*.5f)});
            return line;
        }
        static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface",1);material.SetFloat("_ZWrite",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=(int)RenderQueue.Transparent;
        }
        void OnDestroy(){if(hullMaterial)Destroy(hullMaterial);if(lineMaterial)Destroy(lineMaterial);}
    }
}
