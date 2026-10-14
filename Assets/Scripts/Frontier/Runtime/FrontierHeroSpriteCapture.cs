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
                Texture2D original = unifiedAction != null ? unifiedAction : sheet;
                int spriteFrame = unifiedAction != null ? 0 : frame;
                string key = original.GetInstanceID() + ":" + spriteFrame + ":" +
                    sheet.GetInstanceID() + ":" + frame + ":" + facing + ":" +
                    (locomotion ? "walk" : "pose") + ":" + (death ? "dead" : "live");
                Sprite sprite;
                if (!heroSprites.TryGetValue(key, out sprite))
                {
                    sprite = BakeCanonicalHeroFrame(original, spriteFrame, sheet, frame,
                        GameCore.DirectionRow(Game.VisualFacing), locomotion, death);
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
                heroCaptureRenderer.flipX = flip;
                // The core decides when damage, movement and recovery happen.
                // Animator tracks the same seven logical actions, never fires
                // a second damage event or changes SaveData.
                if (heroCaptureAnimator != null && heroCaptureAnimator.runtimeAnimatorController != null)
                {
                    heroCaptureAnimator.SetInteger("Action", Game.HeroAnimationState);
                    heroCaptureAnimator.SetInteger("Facing", facing);
                }
                if (heroCaptureFrame != Time.frameCount)
                {
                    heroCaptureCamera.Render();
                    heroCaptureFrame = Time.frameCount;
                }
                Color old = GUI.color;
                try
                {
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

        private void EnsureHeroCapture()
        {
            if (heroCaptureCamera != null) return;
            heroCaptureRoot = new GameObject("Starfall Hero SpriteRenderer Capture");
            heroCaptureRoot.hideFlags = HideFlags.HideAndDontSave;
            heroCaptureRoot.transform.position = new Vector3(9000, 9000, 0);
            heroCaptureRoot.layer = HeroCaptureLayer;
            heroCaptureRenderer = heroCaptureRoot.AddComponent<SpriteRenderer>();
            heroCaptureRenderer.sortingOrder = 100;
            // Animator is an action-state bridge; the legacy accepted
            // raster (including the original blonde head) is still selected
            // synchronously by GameCore and SpriteRenderer each simulation tick.
            heroCaptureAnimator = heroCaptureRoot.AddComponent<Animator>();
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(
                "Frontier/HeroAnimator/ProductionController");
            if (controller != null) heroCaptureAnimator.runtimeAnimatorController = controller;
            else heroCaptureAnimator.enabled = false;
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
            // The original source sheets are marked Read/Write. Preserve
            // existing authored gear/limbs and replace ONLY the registered
            // head with WalkVanguard's accepted single-protagonist identity.
            const int cellsPerRow = 4;
            int cols = source.width / HeroCell;
            if (cols < 1 || source.height % HeroCell != 0 || index < 0 ||
                index >= cols * (source.height / HeroCell)) return null;
            Color[] colors = source.GetPixels(index % cols * HeroCell,
                source.height - (index / cols + 1) * HeroCell,
                HeroCell, HeroCell);
            if (!death && walkSheets != null && walkSheets.Length > 0 &&
                walkSheets[0] != null)
            {
                Rect head, canonical;
                if (ActorFrameRegistration.TryHead(registration, registrationIndex, out head))
                {
                    if (locomotion && ActorFrameRegistration.TryStableHead(registration,
                        registrationIndex / cellsPerRow, out canonical)) head = canonical;
                    Texture2D identity = walkSheets[0];
                    int sourceHeadFrame = facing * 4;
                    Rect reference;
                    if (ActorFrameRegistration.TryStableHead(identity, facing, out reference))
                    {
                        int hx = Mathf.RoundToInt(head.x), hy = Mathf.RoundToInt(head.y);
                        int hw = Mathf.RoundToInt(head.width), hh = Mathf.RoundToInt(head.height);
                        for (int row = hy; row < hy + hh; row++)
                            for (int col = hx; col < hx + hw; col++)
                                if (row >= 0 && row < HeroCell && col >= 0 && col < HeroCell)
                                    colors[(HeroCell - 1 - row) * HeroCell + col] = Color.clear;
                        int w = Mathf.RoundToInt(reference.width), h = Mathf.RoundToInt(reference.height);
                        Color[] hair = identity.GetPixels(sourceHeadFrame % 4 * HeroCell +
                            Mathf.RoundToInt(reference.x),
                            identity.height - (sourceHeadFrame / 4 + 1) * HeroCell +
                            HeroCell - Mathf.RoundToInt(reference.y + reference.height), w, h);
                        int destX = locomotion ? (HeroCell - w) / 2 :
                            Mathf.RoundToInt(head.x + head.width / 2f - w / 2f);
                        int destY = locomotion ? Mathf.RoundToInt(reference.y) :
                            Mathf.RoundToInt(head.y + head.height - h);
                        for (int row = 0; row < h; row++)
                            for (int col = 0; col < w; col++)
                            {
                                int px = destX + col, py = destY + row;
                                if (px < 0 || px >= HeroCell || py < 0 || py >= HeroCell) continue;
                                Color upper = hair[(h - 1 - row) * w + col];
                                if (upper.a <= 0) continue;
                                int pos = (HeroCell - 1 - py) * HeroCell + px;
                                Color lower = colors[pos];
                                float alpha = upper.a + lower.a * (1 - upper.a);
                                colors[pos] = alpha > 0
                                    ? new Color((upper.r * upper.a + lower.r * lower.a * (1 - upper.a)) / alpha,
                                        (upper.g * upper.a + lower.g * lower.a * (1 - upper.a)) / alpha,
                                        (upper.b * upper.a + lower.b * lower.a * (1 - upper.a)) / alpha, alpha)
                                    : Color.clear;
                            }
                    }
                }
            }
            Texture2D baked = new Texture2D(HeroCell, HeroCell, TextureFormat.RGBA32, false);
            baked.filterMode = FilterMode.Point;
            baked.SetPixels(colors);
            baked.Apply(false, false);
            // Unity pivot Y is bottom-origin: (128, 16) means the SAME
            // top-origin foot coordinate (128, 240) as OnGUI.
            return Sprite.Create(baked, new Rect(0, 0, HeroCell, HeroCell),
                new Vector2(.5f, .0625f), HeroPixelsPerUnit);
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
        }
    }
}
#endif
