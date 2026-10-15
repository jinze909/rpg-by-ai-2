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
        private const int HeroCell = CanonicalHeroSpriteFactory.Cell;
        private const int HeroCaptureLayer = 31;
        private GameObject heroCaptureRoot;
        private Camera heroCaptureCamera;
        private SpriteRenderer heroCaptureRenderer;
        private Animator heroCaptureAnimator;
        private RenderTexture heroCaptureTexture;
        private readonly Dictionary<string, Sprite> heroSprites = new Dictionary<string, Sprite>();
        private readonly Queue<string> heroSpriteOrder = new Queue<string>();
        // The 63 shipped canonical atlases are real production resources.
        // Only Sprite sub-rect wrappers belong to us; Resources owns textures.
        private readonly Dictionary<string, Sprite[]> heroPublishedSprites =
            new Dictionary<string, Sprite[]>();
        private readonly HashSet<string> heroPublishedUnavailable =
            new HashSet<string>();
        // An imported Animator can exist while its override still references
        // another class, stale clip or wrong direction. Never repeatedly
        // sample a known-mismatched native clip during this play session.
        private readonly HashSet<string> heroRejectedNativeClips =
            new HashSet<string>();
        private int heroCaptureFrame = -1;
        private Sprite heroCaptureRenderedSprite;
        private bool heroCaptureRenderedFlip;
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
                // All three renderer lanes sample the SAME movement/contact
                // clock. Neither GPU Animator nor shipped-sheet playback
                // invents damage, projectile or hit-stop timing.
                int action, bakedFrames, publishedFrame;
                float cellPosition;
                if (!ResolveFullBodyHeroFrame(locomotion,facing,
                        out action,out bakedFrames,out publishedFrame,
                        out cellPosition))
                    return false;
                // This exact frame contract is also used by the live OnGUI
                // fallback; no second clock or interpolated puppet anatomy.
                // Heavy combo swings use the approved 6/7 sword poses,
                // NOT the 4/5 normal-attack atlas. GameCore already
                // differentiates them through PlayerPose. Preserve that
                // distinction until a dedicated heavy atlas is authored.
                bool specialHeavy = action == 2 && (pose == 6 || pose == 7);
                bool allowStandardClip = unifiedAction == null && !specialHeavy;
                Sprite expectedPublished = null;
                bool canVerifyNative = allowStandardClip &&
                    TryUsePublishedHeroFrame(Game.AppearanceWeapon, action, facing,
                        publishedFrame, bakedFrames, out expectedPublished);
                string nativeClipKey = Game.AppearanceWeapon + ":" + action + ":" + facing;
                bool hasClip = canVerifyNative &&
                    !heroRejectedNativeClips.Contains(nativeClipKey) &&
                    TryUseProductionHeroClip(facing);
                if (hasClip)
                {
                    float normalized = (cellPosition + .20f) / bakedFrames;
                    heroCaptureAnimator.SetInteger("Action", action);
                    heroCaptureAnimator.SetInteger("Facing", facing);
                    heroCaptureAnimator.Play(HeroLiveStateHash[action, facing], 0, normalized);
                    heroCaptureAnimator.Update(0f);
                    // A non-null Sprite is insufficient: an old Animator
                    // override can silently draw a different class/direction
                    // or stale three-frame Cast contact. Pixel identity is
                    // compared to the verified published runtime atlas.
                    if (!HeroSpriteMatchesPublishedFrame(heroCaptureRenderer.sprite,
                            expectedPublished))
                    {
                        heroRejectedNativeClips.Add(nativeClipKey);
                        heroCaptureAnimator.enabled = false;
                        heroCaptureRenderer.sprite = expectedPublished;
                        Debug.LogWarning("Starfall replaced mismatched native hero clip "
                            + nativeClipKey + " with verified published Sprite");
                    }
                }
                else
                {
                    if (heroCaptureAnimator != null) heroCaptureAnimator.enabled = false;
                    Sprite published;
                    if (allowStandardClip &&
                        TryUsePublishedHeroFrame(Game.AppearanceWeapon, action, facing,
                            publishedFrame, bakedFrames, out published))
                    {
                        // Production resource already contains the accepted
                        // blonde head, shared foot pivot, all middle frames.
                        // Do not recomposite its upper/lower body or rescale it.
                        heroCaptureRenderer.sprite = published;
                    }
                    else
                    {
                        // No dynamic torso/leg synthesis is permitted.
                        // A missing published atlas uses only one ORIGINAL
                        // full-body source pose with the immutable foot root.
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
                }
                heroCaptureRenderer.flipX = flip;
                // Two GUI repaint passes can sample different profession,
                // facing or animation poses inside the SAME Unity frame.
                // Never display a stale camera texture after a Sprite switch.
                if (!heroCaptureTexture.IsCreated())
                {
                    heroCaptureTexture.Create();
                    heroCaptureFrame = -1;
                }
                if (heroCaptureFrame != Time.frameCount ||
                    heroCaptureRenderedSprite != heroCaptureRenderer.sprite ||
                    heroCaptureRenderedFlip != flip)
                {
                    heroCaptureCamera.Render();
                    heroCaptureFrame = Time.frameCount;
                    heroCaptureRenderedSprite = heroCaptureRenderer.sprite;
                    heroCaptureRenderedFlip = flip;
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
                    float density = CanonicalHeroSpriteFactory.GuiDensity;
                    GUI.DrawTexture(new Rect(
                        foot.x - CanonicalHeroSpriteFactory.FootX * density,
                        foot.y - CanonicalHeroSpriteFactory.FootTop * density,
                        HeroCell * density, HeroCell * density), heroCaptureTexture);
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

        private bool TryUsePublishedHeroFrame(int profession, int action,
            int facing, int frame, int expectedFrames, out Sprite sprite)
        {
            sprite = null;
            if (profession < 0 || profession >= HeroLiveProfessions.Length ||
                action < 0 || action >= HeroLiveActions.Length ||
                facing < 0 || facing >= HeroLiveFacings.Length) return false;
            string key = "Hero_" + HeroLiveProfessions[profession] + "_" +
                HeroLiveActions[action] + "_" + HeroLiveFacings[facing];
            if (heroPublishedUnavailable.Contains(key)) return false;
            Sprite[] cells;
            if (!heroPublishedSprites.TryGetValue(key, out cells))
            {
                Texture2D atlas = Resources.Load<Texture2D>(
                    "Frontier/HeroAnimator/Canonical/" + key);
                // Reject stale or truncated publishers atomically. Never
                // display the wrong contact/recovery or another class outfit.
                if (atlas == null || atlas.height != HeroCell ||
                    atlas.width != expectedFrames * HeroCell)
                {
                    heroPublishedUnavailable.Add(key);
                    if (atlas != null)
                        Debug.LogWarning("Starfall rejected incompatible hero sheet: " + key);
                    return false;
                }
                cells = new Sprite[expectedFrames];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = Sprite.Create(atlas,
                        new Rect(i * HeroCell, 0, HeroCell, HeroCell),
                        CanonicalHeroSpriteFactory.FootPivot,
                        CanonicalHeroSpriteFactory.PixelsPerUnit, 0,
                        SpriteMeshType.FullRect);
                heroPublishedSprites.Add(key, cells);
            }
            if (frame < 0 || frame >= cells.Length) return false;
            sprite = cells[frame];
            return sprite != null;
        }

        private static bool HeroSpriteMatchesPublishedFrame(
            Sprite animated, Sprite published)
        {
            if (animated == null || published == null) return false;
            // Different Sprite objects are expected: imported .anim sprites
            // versus runtime Sprite.Create wrappers. Texture, source rect,
            // PPU and exact boot Pivot together establish pixel parity.
            if (animated.texture != published.texture) return false;
            Rect a = animated.rect, b = published.rect;
            Vector2 pivotA = animated.pivot, pivotB = published.pivot;
            return Mathf.Abs(a.x - b.x) < .05f &&
                Mathf.Abs(a.y - b.y) < .05f &&
                Mathf.Abs(a.width - b.width) < .05f &&
                Mathf.Abs(a.height - b.height) < .05f &&
                Vector2.Distance(pivotA, pivotB) < .05f &&
                Mathf.Abs(animated.pixelsPerUnit - published.pixelsPerUnit) < .05f;
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

        private void EnsureHeroCapture()
        {
            if (heroCaptureCamera != null) return;
            heroCaptureRoot = new GameObject("Starfall Hero SpriteRenderer Capture");
            heroCaptureRoot.hideFlags = HideFlags.HideAndDontSave;
            heroCaptureRoot.transform.position = new Vector3(9000, 9000, 0);
            heroCaptureRoot.layer = HeroCaptureLayer;
            // Enclose current body and future weapon/effect child renderers
            // in one stable draw-order unit. IMGUI-world occlusion still uses
            // its existing depth-sorted capture rectangle.
            var sorting = heroCaptureRoot.AddComponent<UnityEngine.Rendering.SortingGroup>();
            sorting.sortingOrder = 100;
            heroCaptureRenderer = heroCaptureRoot.AddComponent<SpriteRenderer>();
            heroCaptureRenderer.sortingOrder = 0;
            heroCaptureAnimator = heroCaptureRoot.AddComponent<Animator>();
            heroCaptureAnimator.enabled = false; // no silent independent timeline
            heroCaptureAnimator.speed = 0f;
            heroCaptureTexture = new RenderTexture(HeroCell, HeroCell, 16, RenderTextureFormat.ARGB32);
            heroCaptureTexture.filterMode = FilterMode.Point;
            heroCaptureTexture.useMipMap = false;
            heroCaptureTexture.Create();
            var cameraObject = new GameObject("Hero Sprite Capture Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(heroCaptureRoot.transform, false);
            float centerAboveFeet = (CanonicalHeroSpriteFactory.FootTop - HeroCell * .5f)
                / CanonicalHeroSpriteFactory.PixelsPerUnit;
            cameraObject.transform.localPosition = new Vector3(0, centerAboveFeet, -10);
            cameraObject.layer = HeroCaptureLayer;
            heroCaptureCamera = cameraObject.AddComponent<Camera>();
            heroCaptureCamera.enabled = false;
            heroCaptureCamera.orthographic = true;
            heroCaptureCamera.orthographicSize =
                HeroCell / (2f * CanonicalHeroSpriteFactory.PixelsPerUnit);
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
            // Destroy only our Sprite sub-rect wrappers, not shared Resources
            // textures which can be referenced by other game systems.
            foreach (Sprite[] cells in heroPublishedSprites.Values)
                foreach (Sprite sprite in cells)
                    if (sprite != null) Destroy(sprite);
            heroPublishedSprites.Clear();
            heroPublishedUnavailable.Clear();
            heroRejectedNativeClips.Clear();
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
            heroCaptureRenderedSprite = null;
            heroCaptureRenderedFlip = false;
            heroCaptureProfession = -1;
            heroProductionReady = false;
        }
    }
}
#endif
