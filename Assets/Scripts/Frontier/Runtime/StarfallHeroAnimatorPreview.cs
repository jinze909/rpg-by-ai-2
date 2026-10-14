using UnityEngine;

namespace Starfall
{
    // Standalone prefab preview controller for the new SpriteRenderer + Animator
    // artwork lane. Not attached to FrontierRuntime or the save/gameplay clock.
    // Drop a generated profession prefab into an empty scene and press 1..5,
    // up/down/right/left to inspect each real imported atlas sequence.
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class StarfallHeroAnimatorPreview : MonoBehaviour
    {
        private Animator animator;
        private SpriteRenderer actor;
        private int facing;
        private int action;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            actor = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) action = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) action = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) action = 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) action = 3;
            if (Input.GetKeyDown(KeyCode.Alpha5)) action = 4;
            if (Input.GetKeyDown(KeyCode.UpArrow)) facing = 1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) facing = 2;
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                facing = 0;
                actor.flipX = false;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                facing = 0;
                // East/West mirroring is only provisional for this preview.
                // Archer grip and asymmetric gear need directional redraw/QA.
                actor.flipX = true;
            }
            animator.SetInteger("Action", action);
            animator.SetInteger("Facing", facing);
        }
    }
}
