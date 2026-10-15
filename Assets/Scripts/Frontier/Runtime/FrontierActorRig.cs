namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Pure sizing helper for monster/archer source atlases (not a limb
        // compositor). Preserves existing enemy world-size/flash geometry.
        private static float ActorCanvas(UnityEngine.Texture2D sheet,
            int columns,float density)
        {
            return sheet==null?128*density:
                sheet.width/(float)columns*density;
        }

        // Legacy HumanActor / ActorPart five-piece head/torso/leg puppet
        // compositor was retired from the actual world in 2026-10.
        // It is deliberately NOT available as a future renderer fallback:
        // a per-piece draw can separate neck, belt and pelvis despite sharing
        // the same mathematical foot anchor. Use one complete class-owned
        // released Sprite, or one complete original authored body Sprite.
    }
}
