using UnityEngine;

namespace Meridian
{
    /// <summary>Gesture ownership is decided once on press; maximum excursion cannot be undone by dragging back.</summary>
    public sealed class GlobeGesture
    {
        public bool Captured {get;private set;}
        public bool Dragged {get;private set;}
        private Vector2 start;
        public void Press(Vector2 point,bool startsOnGlobe,bool startsOnUI)
        {Reset();start=point;Captured=startsOnGlobe&&!startsOnUI;}
        public void Move(Vector2 point,float threshold)
        {if(Captured && (point-start).sqrMagnitude>threshold*threshold)Dragged=true;}
        public bool Release(bool overUI)
        {bool click=Captured&&!Dragged&&!overUI;Reset();return click;}
        public void Reset(){Captured=false;Dragged=false;}
    }
}
