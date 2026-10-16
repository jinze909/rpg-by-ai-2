using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // One authored atlas: 4 distinct arrow filament poses in row 0;
        // 4 plasma filament poses in row 1. Supplemental drawn VFX ONLY:
        // projectile head geometry, damage and physics are unchanged.
        private Texture2D projectileTrailArt;
        private float projectileTrailNextImport=float.NegativeInfinity;
        private float projectileTrailLastImport=float.NegativeInfinity;

        private Texture2D PublishedProjectileTrailArt()
        {
            if(clock<projectileTrailLastImport||
                clock>=projectileTrailNextImport)
            {
                projectileTrailLastImport=clock;
                projectileTrailNextImport=clock+2.5f;
                Texture2D imported=Resources.Load<Texture2D>(
                    "Frontier/Art/ProjectileTrails");
                // Half-written Editor imports must never replace the last
                // complete approved art; a new correct atlas reappears
                // without changing rooms or restarting a game.
                if(imported!=null&&imported.width==96&&
                    imported.height==48)
                    projectileTrailArt=imported;
            }
            return projectileTrailArt;
        }
    }
}
