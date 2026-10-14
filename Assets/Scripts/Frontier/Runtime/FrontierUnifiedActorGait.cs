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

        private Texture2D UnifiedActionGait(Texture2D action,int columns,int rows,
            int actionFrame,Texture2D walk,int walkFrame)
        {
            const int cell=256;
            if(action==null||walk==null||columns<=0||rows<=0||
                action.width!=columns*cell||action.height!=rows*cell||
                walk.width!=4*cell||walk.height!=3*cell||
                actionFrame<0||actionFrame>=columns*rows||
                walkFrame<0||walkFrame>=12)return null;
            // Frame order comes from the live Game.WalkFrame and
            // GameCore.DirectionRow, not from alphabetical file sorting.
            string key=action.GetInstanceID()+":"+actionFrame+":"+
                walk.GetInstanceID()+":"+walkFrame;
            Texture2D cached;
            if(unifiedGaitCache.TryGetValue(key,out cached))return cached;
            Color[] posePixels=action.GetPixels(
                actionFrame%columns*cell,
                action.height-(actionFrame/columns+1)*cell,cell,cell);
            Color[] feetPixels=walk.GetPixels(
                walkFrame%4*cell,
                walk.height-(walkFrame/4+1)*cell,cell,cell);
            // Both original sheets share a 256x256 cell with a 120/128
            // FOOT PIVOT. Preserve head, upper torso, weapon grip and side
            // accessories from the authored ACTION cell. In the core body
            // region, bridge the original pelvis over the authored walk
            // gait before changing the shin/boot silhouette.
            //
            // Unlike rendering two separate cropped sprites, this yields
            // exactly ONE image and ONE pivot: no leg/torso drift when the
            // action's facing or release phase changes.
            for(int yTop=208;yTop<=247;yTop++)
            {
                int y=cell-1-yTop;
                int left=yTop<217?104:yTop<224?94:88;
                int right=yTop<217?153:yTop<224?162:175;
                for(int x=left;x<=right;x++)
                {
                    int i=y*cell+x;
                    if(yTop<217)
                    {
                        // Preserve the original torso/hip overlap, adding
                        // missing walking cloth behind transparent gaps.
                        if(posePixels[i].a<.08f&&feetPixels[i].a>.08f)
                            posePixels[i]=feetPixels[i];
                    }
                    else
                    {
                        // Keep the authored weapon/cape outside the central
                        // pelvis/leg area rather than cutting entire rows.
                        posePixels[i]=feetPixels[i];
                    }
                }
            }
            Texture2D result=new Texture2D(cell,cell,TextureFormat.RGBA32,false);
            result.name="Unified_"+action.name+"_"+actionFrame+"_"+
                walk.name+"_"+walkFrame;
            result.filterMode=FilterMode.Point;
            result.wrapMode=TextureWrapMode.Clamp;
            result.SetPixels(posePixels);
            result.Apply(false,false);
            // 96 full RGBA action cells are at most about 24MiB, even after
            // long sessions cycling every vocation and cardinal camera row.
            // Evict an old generated frame instead of leaking CPU/GPU memory.
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
