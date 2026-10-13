using System;
namespace Starfall
{
    /// <summary>
    /// Shared deterministic presentation math. No Unity classes, no saved state:
    /// visible world cells and props only change alpha, never existence,
    /// navigation, combat or the identity of the canonical protagonist.
    /// </summary>
    public static class WorldStreaming
    {
        public static float Smooth01(float value)
        {
            if(value<=0)return 0f;
            if(value>=1)return 1f;
            return value*value*(3f-2f*value);
        }
        public static float Blend(float northwest,float northeast,float southwest,
            float southeast,float horizontal,float vertical)
        {
            float x=Smooth01(horizontal),y=Smooth01(vertical);
            float top=northwest+(northeast-northwest)*x;
            float bottom=southwest+(southeast-southwest)*x;
            return top+(bottom-top)*y;
        }
        public static float EdgeOpacity(float x,float y,float width,float height)
        {
            // An entering prop begins feathering in *outside* the frame.
            // The 220px apron covers the tallest authored tree or obelisk.
            const float outside=130f, feather=220f;
            float left=Smooth01((x+outside)/feather);
            float right=Smooth01((width+outside-x)/feather);
            float top=Smooth01((y+outside)/feather);
            float bottom=Smooth01((height+outside-y)/feather);
            return Math.Min(Math.Min(left,right),Math.Min(top,bottom));
        }
    }
}
