#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using UnityEngine;

namespace Starfall.Tests
{
    // Actual Unity OnGUI Repaint is required for FrontierRuntime's private
    // native production bridge. Reflection outside Repaint skips GPU draws,
    // creating a false positive in a Test Runner coroutine.
    public sealed class NativeActorGameViewProbe : MonoBehaviour
    {
        public FrontierRuntime runtime;
        public Texture2D atlas;
        public int columns,rows;
        public bool completed,drawn;
        public System.Exception failure;
        private void OnGUI()
        {
            if(completed||runtime==null||atlas==null||
                Event.current.type!=EventType.Repaint)return;
            try
            {
                var method=typeof(FrontierRuntime).GetMethod(
                    "DrawSpriteRendererActor",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                if(method==null)throw new System.MissingMethodException(
                    "Production nonhero native capture missing");
                drawn=(bool)method.Invoke(runtime,new object[]{
                    atlas,columns,rows,0,new Point(128,128),96f,
                    Color.white,false,0f});
            }
            catch(System.Exception ex){failure=ex;}
            finally {completed=true;}
        }
    }
#endif
}
#endif
