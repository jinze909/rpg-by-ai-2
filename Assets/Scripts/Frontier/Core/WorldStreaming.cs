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
        private static float Lattice(int x,int y,int room)
        {
            // Stable integer hash: no Unity.Random state or frame-time seeds.
            unchecked
            {
                uint v=(uint)(x*73856093^y*19349663^(room+1)*83492791);
                v^=v>>13;
                v*=1274126177u;
                return (v&65535u)/65535f;
            }
        }
        public static float BiomeVariation(int x,int y,int room)
        {
            // Smooth 8x8-tile value noise, continuous at every lattice edge.
            const float cell=8f;
            int gx=(int)Math.Floor(x/cell),gy=(int)Math.Floor(y/cell);
            float tx=x/cell-gx,ty=y/cell-gy;
            return Blend(Lattice(gx,gy,room),Lattice(gx+1,gy,room),
                Lattice(gx,gy+1,room),Lattice(gx+1,gy+1,room),tx,ty);
        }
        public static float TerrainVeilOpacity(float readiness)
        {
            // Never hide already-renderable geometry behind a flat dark tile.
            // Only a light atmospheric veil fades; geometry stays legible,
            // even on instantaneous camera jumps or large scene transitions.
            return .3f*(1f-Smooth01(readiness));
        }
        public static int PredictPrewarmLead(float cameraTargetDelta)
        {
            // A bounded extra eight tiles in the direction of travel;
            // unbounded teleports must not prebuild the entire world.
            if(float.IsNaN(cameraTargetDelta)||float.IsInfinity(cameraTargetDelta))
                return 0;
            return (int)Math.Max(-8,Math.Min(8,Math.Round(cameraTargetDelta*1.5f)));
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
