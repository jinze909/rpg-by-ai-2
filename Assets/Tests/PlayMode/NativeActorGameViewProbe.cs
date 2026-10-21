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
        public int columns,rows,frame;
        public bool flip;
        public bool completed,drawn;
        public Sprite capturedSprite;
        public bool capturedFlip;
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
                    atlas,columns,rows,frame,new Point(128,128),96f,
                    Color.white,flip,0f});
                var rendererField=typeof(FrontierRuntime).GetField(
                    "actorCaptureRenderer",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var renderer=rendererField==null?null:
                    rendererField.GetValue(runtime) as SpriteRenderer;
                if(renderer==null)throw new System.MissingFieldException(
                    "Native actor capture SpriteRenderer missing");
                capturedSprite=renderer.sprite;
                capturedFlip=renderer.flipX;
            }
            catch(System.Exception ex){failure=ex;}
            finally {completed=true;}
        }
    }
}
#endif
