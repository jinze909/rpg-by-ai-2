using System;
using System.Collections.Generic;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Unified, root-anchored sprite composites built only from ACCEPTED
        // project pixel atlases. No independent puppet legs, rotation center,
        // unknown faces, or third-party generated body parts are introduced.
        private const int UnifiedGaitCacheMaxFrames=96;
        private readonly Dictionary<string,Texture2D> unifiedGaitCache=
            new Dictionary<string,Texture2D>();
        private readonly Queue<string> unifiedGaitOrder=new Queue<string>();

        // Class-preserving FULL-BODY gait: action art supplies EVERY rendered
        // pixel, including shoulders, belt, hips, legs and boots. The class's
        // walking atlas is a cadence reference ONLY, never a color source.
        // Previously the lower 205..247px strip was pasted from Walk<profession>
        // over Player/Rear/South, creating a hard cloth/armor discontinuity.
        private Texture2D UnifiedActionGait(Texture2D action,int columns,int rows,
            int actionFrame,Texture2D walk,int walkFrame,int profession)
        {
            const int cell=256;
            if(action==null||walk==null||columns<=0||rows<=0||
                action.width!=columns*cell||action.height!=rows*cell||
                walk.width!=4*cell||walk.height!=3*cell||
                actionFrame<0||actionFrame>=columns*rows||
                walkFrame<0||walkFrame>=12||
                profession<0||profession>=3||
                walkSheets==null||playerSheets==null||
                walkSheets[profession]!=walk)
                return null;
            bool personal=action==playerSheets[profession];
            bool shared=(action==playerRear||action==playerSouth||
                action==playerReactionsRear||action==playerReactionsSouth);
            // Every row of a shared action sheet belongs to exactly ONE
            // profession. Reject mismatched row IDs, never silently paste
            // a Vanguard upper body over Ranger/Arcanist legs.
            if(!personal&&!(shared&&actionFrame/4==profession))
                return null;
            string key=profession+":"+action.GetInstanceID()+":"+
                actionFrame+":"+walkFrame;
            Texture2D cached;
            if(unifiedGaitCache.TryGetValue(key,out cached))return cached;
            Color[] source=action.GetPixels(
                actionFrame%columns*cell,
                action.height-(actionFrame/columns+1)*cell,cell,cell);
            Color[] composed=(Color[])source.Clone();
            // Advance one stride on the ORIGINAL single full-body silhouette,
            // with opposing left/right boot translation. No replacement lower
            // half, no straight waist cut, and no class-palette introduction.
            // Preserve foot contact and keep shoulders/hair/weapon unmodified.
            int phase=walkFrame%4;
            int[] motionLeft={-3,-1,3,1};
            int[] motionRight={3,1,-3,-1};
            for(int top=218;top<=243;top++)
            {
                float topGate=Math.Min(1f,(top-218)/9f);
                float soleGate=Math.Min(1f,(244-top)/8f);
                float weight=Math.Max(0f,Math.Min(topGate,soleGate));
                int y=cell-1-top;
                for(int x=89;x<=173;x++)
                {
                    float leftWeight=Math.Max(0f,Math.Min(1f,(132-x)/12f));
                    float rightWeight=Math.Max(0f,Math.Min(1f,(x-124)/12f));
                    float divisor=leftWeight+rightWeight;
                    if(divisor<.001f)continue;
                    float offset=(motionLeft[phase]*leftWeight+
                        motionRight[phase]*rightWeight)/divisor*weight;
                    int sample=x-(int)Math.Round(offset);
                    if(sample<0||sample>=cell)continue;
                    composed[y*cell+x]=source[y*cell+sample];
                }
            }
            Texture2D result=new Texture2D(cell,cell,TextureFormat.RGBA32,false);
            result.name="FullBodyGait_Class"+profession+"_"+action.name+"_"+
                actionFrame+"_"+walkFrame;
            result.filterMode=FilterMode.Point;
            result.wrapMode=TextureWrapMode.Clamp;
            result.SetPixels(composed);
            result.Apply(false,false);
            // Bounded generated textures; never modify owned Resources.
            if(unifiedGaitOrder.Count>=UnifiedGaitCacheMaxFrames)
            {
                string oldest=unifiedGaitOrder.Dequeue();
                Texture2D retired;
                if(unifiedGaitCache.TryGetValue(oldest,out retired))
                {
                    unifiedGaitCache.Remove(oldest);
                    if(retired!=null)UnityEngine.Object.Destroy(retired);
                }
            }
            unifiedGaitCache.Add(key,result);
            unifiedGaitOrder.Enqueue(key);
            return result;
        }

        private void ClearUnifiedActionGait()
        {
            foreach(Texture2D sheet in unifiedGaitCache.Values)
                if(sheet!=null)UnityEngine.Object.Destroy(sheet);
            unifiedGaitCache.Clear();
            unifiedGaitOrder.Clear();
        }
    }
}
