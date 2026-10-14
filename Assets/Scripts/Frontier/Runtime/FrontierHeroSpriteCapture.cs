#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // The normal production game remains on proven OnGUI. Toggle in the
        // FrontierRuntime Inspector after native PlayMode/art QA to compare the
        // SpriteRenderer lane in the REAL scene (not a disconnected prefab).
        [SerializeField] private bool useSpriteRendererHero;
        private const int HeroCell = 256;
        private const int HeroCaptureLayer = 31;
        private const float HeroPixelsPerUnit = 128f;
        private GameObject heroCaptureRoot;
        private Camera heroCaptureCamera;
        private SpriteRenderer heroCaptureRenderer;
        private Animator heroCaptureAnimator;
        private RenderTexture heroCaptureTexture;
        private readonly Dictionary<string, Sprite> heroSprites = new Dictionary<string, Sprite>();
        private readonly Queue<string> heroSpriteOrder = new Queue<string>();
        private int heroCaptureFrame = -1;
        private bool heroCaptureBroken;
        private int heroCaptureProfession = -1;
        private bool heroProductionReady;
        private static readonly string[] HeroLiveActions =
            { "Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death" };
        private static readonly string[] HeroLiveFacings = { "E", "N", "S" };
        private static readonly string[] HeroLiveProfessions = { "Vanguard", "Ranger", "Arcanist" };
        private static readonly int[,] HeroLiveStateHash = CreateHeroLiveStates();

        private static int[,] CreateHeroLiveStates()
        {
            var hashes = new int[7, 3];
            for (int a = 0; a < 7; a++)
                for (int f = 0; f < 3; f++)
                    hashes[a, f] = Animator.StringToHash("Base Layer."
                        + HeroLiveActions[a] + "_" + HeroLiveFacings[f]);
            return hashes;
        }

        private bool DrawSpriteRendererHero(Texture2D sheet, int frame, Point foot,
            Color tint, bool flip, int facing, int pose, bool locomotion,
            bool death, Texture2D unifiedAction)
        {
            if (!useSpriteRendererHero || heroCaptureBroken || drawingOutline ||
                Game == null || Game.mode == "title" || sheet == null)
                return false;
            // Other non-world UI still displays the existing depth-sorted world.
            if (Event.current.type != EventType.Repaint) return true;
            try
            {
                EnsureHeroCapture();
                bool hasClip = unifiedAction == null && TryUseProductionHeroClip(facing);
                if (hasClip)
                {
                    // The canonical baked Sprite asset belongs to its Animator
                    // Controller. Align the frame with the actual GameCore
                    // action/release clock; no second independent damage timer.
                    int action = Game.HeroAnimationState;
                    int frames = HeroClipFrames(action, facing);
                    int step = HeroClipStep(action, facing, frames);
                    float normalized = (step + .20f) / frames;
                    heroCaptureAnimator.SetInteger("Action", action);
                    heroCaptureAnimator.SetInteger("Facing", facing);
                    heroCaptureAnimator.Play(HeroLiveStateHash[action, facing], 0, normalized);
                    heroCaptureAnimator.Update(0f);
                    if (heroCaptureRenderer.sprite == null)
                        throw new InvalidOperationException("Baked Animator has no canonical Sprite frame");
                }
                else
                {
                    // The game is runnable before an Editor bake and while
                    // attack/walk upper/lower-body combination is active.
                    // Existing accepted-frame compositor is a full fallback,
                    // not a replacement artwork set.
                    if (heroCaptureAnimator != null) heroCaptureAnimator.enabled = false;
                    Texture2D original = unifiedAction != null ? unifiedAction : sheet;
                    int spriteFrame = unifiedAction != null ? 0 : frame;
                    string key = original.GetInstanceID() + ":" + spriteFrame + ":" +
                        sheet.GetInstanceID() + ":" + frame + ":" + facing + ":" +
                        (locomotion ? "walk" : "pose") + ":" + (death ? "dead" : "live");
                    Sprite sprite;
                    if (!heroSprites.TryGetValue(key, out sprite))
                    {
                        sprite = BakeCanonicalHeroFrame(original, spriteFrame, sheet, frame,
                            facing, locomotion, death);
                        if (sprite == null) return false;
                        heroSprites.Add(key, sprite);
                        heroSpriteOrder.Enqueue(key);
                        while (heroSpriteOrder.Count > 144)
                        {
                            string oldest = heroSpriteOrder.Dequeue();
                            Sprite removed;
                            if (!heroSprites.TryGetValue(oldest, out removed)) continue;
                            heroSprites.Remove(oldest);
                            Texture2D texture = removed.texture;
                            Destroy(removed);
                            if (texture != null) Destroy(texture);
                        }
                    }
                    heroCaptureRenderer.sprite = sprite;
                }
                heroCaptureRenderer.flipX = flip;
                if (heroCaptureFrame != Time.frameCount)
                {
                    heroCaptureCamera.Render();
                    heroCaptureFrame = Time.frameCount;
                }
                Color old = GUI.color;
                try
                {
                    // Match the established actor fade / occlusion shader in
                    // HumanActor; capture should never punch through a wall.
                    tint.a *= worldActorOpacity;
                    GUI.color = tint;
                    // 256 source pixels * .75 GUI density. Foot pixel (128,240)
                    // stays at the SAME ScreenPoint as the legacy HumanActor.
                    GUI.DrawTexture(new Rect(foot.x - 96, foot.y - 180, 192, 192),
                        heroCaptureTexture);
                }
                finally { GUI.color = old; }
                return true;
            }
            catch (Exception error)
            {
                heroCaptureBroken = true;
                Debug.LogWarning("Starfall hero SpriteRenderer capture disabled, "
                    + "using accepted OnGUI art: " + error.Message);
                ReleaseHeroCapture();
                return false;
            }
        }

        private bool TryUseProductionHeroClip(int facing)
        {
            int profession = Game.AppearanceWeapon;
            if (profession < 0 || profession >= HeroLiveProfessions.Length ||
                facing < 0 || facing >= HeroLiveFacings.Length)
                return false;
            if (heroCaptureProfession != profession)
            {
                heroCaptureProfession = profession;
                RuntimeAnimatorController controller =
                    Resources.Load<RuntimeAnimatorController>("Frontier/HeroAnimator/Production"
                        + HeroLiveProfessions[profession]);
                heroCaptureAnimator.runtimeAnimatorController = controller;
                heroProductionReady = controller != null;
            }
            if (!heroProductionReady) return false;
            if (!heroCaptureAnimator.enabled) heroCaptureAnimator.enabled = true;
            heroCaptureAnimator.speed = 0f;
            return true;
        }

        private static int HeroClipFrames(int action, int facing)
        {
            if (action == 1) return 4; // 4 accepted, rooted walk poses
            if (action == 2) return facing == 1 ? 2 : 4;
            if (action == 3) return 2; // legacy cast recovery placeholders
            return 1; // idle, dodge, hit, death: no invented frames
        }

        private int HeroClipStep(int action, int facing, int frames)
        {
            if (frames <= 1) return 0;
            if (action == 1) return Game.WalkFrame % frames;
            ActorAction playing = Game.playerAction;
            if (playing == null) return 0;
            if (!playing.released)
            {
                if (frames == 2) return 0;
                float interval = Mathf.Max(.0001f, playing.contact);
                return playing.elapsed < interval * .55f ? 0 : 1;
            }
            if (frames == 2) return 1;
            return playing.elapsed - playing.contact < .12f ? 2 : 3;
        }

        private void EnsureHeroCapture()
        {
            if (heroCaptureCamera != null) return;
            heroCaptureRoot = new GameObject("Starfall Hero SpriteRenderer Capture");
            heroCaptureRoot.hideFlags = HideFlags.HideAndDontSave;
            heroCaptureRoot.transform.position = new Vector3(9000, 9000, 0);
            heroCaptureRoot.layer = HeroCaptureLayer;
            heroCaptureRenderer = heroCaptureRoot.AddComponent<SpriteRenderer>();
            heroCaptureRenderer.sortingOrder = 100;
            heroCaptureAnimator = heroCaptureRoot.AddComponent<Animator>();
            heroCaptureAnimator.enabled = false; // no silent independent timeline
            heroCaptureAnimator.speed = 0f;
            heroCaptureTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            heroCaptureTexture.filterMode = FilterMode.Point;
            heroCaptureTexture.useMipMap = false;
            heroCaptureTexture.Create();
            var cameraObject = new GameObject("Hero Sprite Capture Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(heroCaptureRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, .875f, -10);
            cameraObject.layer = HeroCaptureLayer;
            heroCaptureCamera = cameraObject.AddComponent<Camera>();
            heroCaptureCamera.enabled = false;
            heroCaptureCamera.orthographic = true;
            heroCaptureCamera.orthographicSize = 1;
            heroCaptureCamera.aspect = 1;
            heroCaptureCamera.clearFlags = CameraClearFlags.SolidColor;
            heroCaptureCamera.backgroundColor = new Color(0, 0, 0, 0);
            heroCaptureCamera.cullingMask = 1 << HeroCaptureLayer;
            heroCaptureCamera.targetTexture = heroCaptureTexture;
            heroCaptureCamera.allowHDR = false;
            heroCaptureCamera.allowMSAA = false;
        }

        private Sprite BakeCanonicalHeroFrame(Texture2D source, int index,
            Texture2D registration, int registrationIndex, int facing,
            bool locomotion, bool death)
        {
            // Runtime and Editor now share the EXACT pixel compositor.
            Texture2D acceptedHead = walkSheets != null && walkSheets.Length > 0
                ? walkSheets[0] : null;
            return CanonicalHeroSpriteFactory.Bake(source, index, registration,
                registrationIndex, facing, locomotion, death, acceptedHead);
        }

        private void ReleaseHeroCapture()
        {
            foreach (Sprite sprite in heroSprites.Values)
            {
                if (sprite == null) continue;
                Texture2D texture = sprite.texture;
                Destroy(sprite);
                if (texture != null) Destroy(texture);
            }
            heroSprites.Clear();
            heroSpriteOrder.Clear();
            if (heroCaptureCamera != null) heroCaptureCamera.targetTexture = null;
            if (heroCaptureRoot != null) Destroy(heroCaptureRoot);
            if (heroCaptureTexture != null)
            {
                heroCaptureTexture.Release();
                Destroy(heroCaptureTexture);
            }
            heroCaptureCamera = null;
            heroCaptureRenderer = null;
            heroCaptureAnimator = null;
            heroCaptureRoot = null;
            heroCaptureTexture = null;
            heroCaptureFrame = -1;
            heroCaptureProfession = -1;
            heroProductionReady = false;
        }
    }
}
#endif
