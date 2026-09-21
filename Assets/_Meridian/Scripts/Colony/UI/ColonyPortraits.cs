using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed partial class ColonyUI
    {
        readonly Dictionary<string,Texture2D> portraits=new Dictionary<string,Texture2D>();
        readonly Queue<ActorState> portraitRequests=new Queue<ActorState>();readonly HashSet<string> requestedPortraits=new HashSet<string>();
        Texture2D Portrait(ActorState actor)
        {
            string key=actor.id+":"+actor.profession;if(portraits.TryGetValue(key,out var image))return image;
            if(requestedPortraits.Add(key))portraitRequests.Enqueue(actor);return Texture2D.grayTexture;
        }
        void PreparePortrait()
        {
            if(portraitRequests.Count==0)return;var actor=portraitRequests.Dequeue();string key=actor.id+":"+actor.profession;requestedPortraits.Remove(key);if(portraits.ContainsKey(key))return;
            if(portraits.Count>=16){var oldest=portraits.First();Destroy(oldest.Value);portraits.Remove(oldest.Key);}
            var root=ColonyModels.Actor(actor);root.name="Temporary inspector portrait";root.transform.position=new Vector3(0,-10000,0);
            foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
            foreach(var collider in root.GetComponentsInChildren<Collider>())collider.enabled=false;
            var cameraObject=new GameObject("Portrait camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.12f,.15f);camera.fieldOfView=32;camera.nearClipPlane=.1f;camera.farClipPlane=5;
            camera.transform.position=root.transform.position+new Vector3(.18f,1.6f,2.05f);camera.transform.LookAt(root.transform.position+Vector3.up*1.4f);
            var lightObject=new GameObject("Portrait key light",typeof(Light));var light=lightObject.GetComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<31;light.intensity=1.5f;light.color=new Color(1,.92f,.82f);light.transform.rotation=Quaternion.Euler(30,-140,0);
            var target=RenderTexture.GetTemporary(128,128,16,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;var image=new Texture2D(128,128,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();portraits.Add(key,image);}
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Destroy(root);Destroy(cameraObject);Destroy(lightObject);}
        }
        void ReleasePortraits(){foreach(var portrait in portraits.Values)if(portrait)Destroy(portrait);portraits.Clear();}
    }
}
