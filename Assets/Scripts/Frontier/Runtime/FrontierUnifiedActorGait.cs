using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // RETIRED. The former compositor warped individual shins/boots inside
        // an unrelated upper-body action pose. That created hard outlines,
        // discontinuous hips and false frame-to-frame articulation even
        // without mixing class textures. All playable hero poses are now one
        // published, foot-registered FULL-BODY Sprite selected by GameCore.
        //
        // Keep this cleanup entry point only for existing FrontierRuntime
        // lifecycle callers; there are no runtime-generated puppet textures
        // to allocate or retain.
        private void ClearUnifiedActionGait() { }
    }
}
