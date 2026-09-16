using System.Collections;
using UnityEngine;

namespace Meridian
{
    public sealed class PlanetFlag : MonoBehaviour
    {
        [SerializeField] private Transform plantingVisual;
        [SerializeField,Range(.02f,.15f)] private float size=.085f;
        [SerializeField,Range(.35f,.6f)] private float duration=.45f;
        [SerializeField,Range(25,35)] private float screenLean=30;
        [SerializeField] private Vector2 screenSizeRange=new Vector2(34,56);
        private Coroutine planting;
        private Transform facingPivot;
        private Transform planet;
        private Camera viewingCamera;
        private PlanetSelectionIndicator indicator;
        private Vector3 anchorPosition,anchorDirection;
        private bool planted;
        public bool IsPlanting => planting!=null;
        public Vector3 AnchorLocalPosition=>anchorPosition;
        public Vector3 AnchorLocalDirection=>anchorDirection;
        public void Configure(Transform visual)=>plantingVisual=visual;
        public void ConfigureCamera(Camera camera)=>viewingCamera=camera;
        public void Plant(Transform planet,Vector3 localPosition,Vector3 direction)
        {
            if(planting!=null)StopCoroutine(planting);
            this.planet=planet;anchorPosition=localPosition;anchorDirection=direction.normalized;planted=true;
            transform.SetParent(planet,false);transform.localPosition=localPosition;
            transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction);
            transform.localScale=Vector3.one;
            if(!facingPivot)
            {
                // Geographic anchor, presentation facing and planting translation have separate owners.
                facingPivot=new GameObject("Flag Facing").transform;facingPivot.SetParent(transform,false);
                plantingVisual.SetParent(facingPivot,false);plantingVisual.localRotation=Quaternion.identity;
            }
            facingPivot.localScale=Vector3.one*size;
            if(!viewingCamera)viewingCamera=Camera.main;
            if(!indicator)indicator=PlanetSelectionIndicator.Create();
            gameObject.SetActive(true);planting=StartCoroutine(Animate());
            Present();
        }
        void LateUpdate(){if(planted)Present();}
        void Present()
        {
            if(!viewingCamera || !planet || !facingPivot)return;
            Vector3 worldAnchor=planet.TransformPoint(anchorPosition);
            Vector3 outward=planet.TransformDirection(anchorDirection).normalized;
            Vector3 toCamera=(viewingCamera.transform.position-worldAnchor).normalized;
            Vector3 screenPoint=viewingCamera.WorldToScreenPoint(worldAnchor);
            bool farSide=Vector3.Dot(outward,toCamera)<=0;
            float uiScale=Mathf.Max(.65f,Screen.height/1080f);
            bool inView=screenPoint.z>viewingCamera.nearClipPlane && screenPoint.x>34*uiScale &&
                screenPoint.x<Screen.width-64*uiScale && screenPoint.y>132*uiScale && screenPoint.y<Screen.height-68*uiScale;
            bool visible=!farSide && inView;
            facingPivot.gameObject.SetActive(visible);
            if(visible)indicator.Hide();
            else indicator.Show(viewingCamera,worldAnchor,planet.position,farSide);

            // Tangential screen lean plus outward lift keeps the silhouette readable and above the surface.
            float angle=screenLean*Mathf.Deg2Rad;
            Vector3 screenUp=viewingCamera.transform.up*Mathf.Cos(angle)+viewingCamera.transform.right*Mathf.Sin(angle);
            Vector3 tangent=Vector3.ProjectOnPlane(screenUp,outward);
            if(tangent.sqrMagnitude<.001f)tangent=Vector3.ProjectOnPlane(viewingCamera.transform.right,outward);
            Vector3 mastUp=(tangent.normalized*.8660254f+outward*.5f).normalized;
            Vector3 facing=Vector3.ProjectOnPlane(viewingCamera.transform.forward,mastUp).normalized;
            if(facing.sqrMagnitude<.001f)facing=Vector3.ProjectOnPlane(viewingCamera.transform.up,mastUp).normalized;
            facingPivot.rotation=Quaternion.LookRotation(facing,mastUp);
            float pixels=Mathf.Clamp(44*uiScale,screenSizeRange.x,screenSizeRange.y);
            float depth=Mathf.Max(.1f,screenPoint.z);
            float worldPerPixel=2*depth*Mathf.Tan(viewingCamera.fieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Max(1,Screen.height);
            float projectedMast=Vector3.ProjectOnPlane(mastUp,viewingCamera.transform.forward).magnitude;
            float worldSize=Mathf.Clamp(pixels*worldPerPixel/Mathf.Max(.35f,projectedMast),.015f,.4f);
            // The globe has unit scale; divide by inherited scale so future prefab scaling cannot change marker size.
            float inherited=Mathf.Max(.0001f,transform.lossyScale.x);
            facingPivot.localScale=Vector3.one*(worldSize/inherited);
        }
        IEnumerator Animate()
        {
            float elapsed=0;
            while(elapsed<duration)
            {
                float t=elapsed/duration;
                float lift=t<.78f?Mathf.Lerp(.7f,-.035f,Mathf.SmoothStep(0,1,t/.78f)):Mathf.Lerp(-.035f,0,(t-.78f)/.22f);
                plantingVisual.localPosition=Vector3.up*lift;
                elapsed+=Time.unscaledDeltaTime;yield return null;
            }
            plantingVisual.localPosition=Vector3.zero;planting=null;
        }
        void OnDisable(){if(planting!=null)StopCoroutine(planting);planting=null;if(indicator)indicator.Hide();}
        void OnDestroy(){if(indicator)Destroy(indicator.gameObject);}
    }
}
