namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Legacy HumanActor / ActorPart five-piece head/torso/leg puppet
        // compositor was retired from the actual world in 2026-10.
        // It is deliberately NOT available as a future renderer fallback:
        // a per-piece draw can separate neck, belt and pelvis despite sharing
        // the same mathematical foot anchor. Use one complete class-owned
        // released Sprite, or one complete original authored body Sprite.
    }
}
