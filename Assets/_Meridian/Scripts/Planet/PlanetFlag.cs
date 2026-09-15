using System.Collections;
using UnityEngine;

namespace Meridian
{
    public sealed class PlanetFlag : MonoBehaviour
    {
        [SerializeField] private Transform plantingVisual;
        [SerializeField,Range(.02f,.15f)] private float size=.085f;
        [SerializeField,Range(.35f,.6f)] private float duration=.45f;
        private Coroutine planting;
        public bool IsPlanting => planting!=null;
        public void Configure(Transform visual)=>plantingVisual=visual;
        public void Plant(Transform planet,Vector3 localPosition,Vector3 direction)
        {
            if(planting!=null)StopCoroutine(planting);
            transform.SetParent(planet,false);transform.localPosition=localPosition;
            transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction);
            transform.localScale=Vector3.one*size;
            gameObject.SetActive(true);planting=StartCoroutine(Animate());
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
        void OnDisable(){if(planting!=null)StopCoroutine(planting);planting=null;}
    }
}
