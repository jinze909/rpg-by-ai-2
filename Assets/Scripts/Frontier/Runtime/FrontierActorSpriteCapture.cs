#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Second-stage actor bridge. The GameCore/FrontierActors frame clock
        // remains authoritative: Animator must NOT independently advance a
        // combat pose across windup, release, hit or death.
        [SerializeField] private bool useSpriteRendererActors = true;
        private const int ActorCaptureLayer = 30;
        private GameObject actorCaptureRoot;
        private Camera actorCaptureCamera;
        private SpriteRenderer actorCaptureRenderer;
        private Animator actorCaptureAnimator;
        private RenderTexture actorCaptureTexture;
        private readonly Dictionary<string, Sprite> actorFrameSprites =
            new Dictionary<string, Sprite>();
        private bool actorCaptureBroken;
        private int actorRenderedFrame = -1;
        private Sprite actorRenderedSprite;
        private bool actorRenderedFlip;

        // Returns false on a recoverable/native failure, so the EXACT same
        // whole source cell can still be drawn by the existing GUI fallback.
        private bool DrawSpriteRendererActor(Texture2D sheet, int columns,
            int rows, int frame, Point foot, float size, Color tint,
            bool flip, float angle)
        {
            if (!useSpriteRendererActors || actorCaptureBroken ||
                drawingOutline || sheet == null || angle != 0f ||
                columns <= 0 || rows <= 0 ||
                sheet.width % columns != 0 || sheet.height % rows != 0)
                return false;
            int width = sheet.width / columns;
            int height = sheet.height / rows;
            if (width <= 0 || height <= 0) return false;
            if (Event.current.type != EventType.Repaint) return true;
            try
            {
                EnsureActorCapture();
                frame = Math.Max(0, Math.Min(columns * rows - 1, frame));
                string key = sheet.GetInstanceID() + ":" +
                    columns + ":" + rows + ":" + frame;
                Sprite sprite;
                if (!actorFrameSprites.TryGetValue(key, out sprite))
                {
                    int x = frame % columns * width;
                    int y = sheet.height - (frame / columns + 1) * height;
                    sprite = Sprite.Create(sheet,
                        new Rect(x, y, width, height),
                        new Vector2(.5f, 1f - 120f / 128f),
                        height, 0, SpriteMeshType.FullRect);
                    actorFrameSprites.Add(key, sprite);
                }
                actorCaptureRenderer.sprite = sprite;
                actorCaptureRenderer.flipX = flip;
                // Legacy Actor() fills a square irrespective of original
                // atlas cell aspect ratio. Scale only the renderer child,
                // never the camera/root or the foot/pivot world position.
                actorCaptureRenderer.transform.localScale =
                    new Vector3((float)height / width, 1f, 1f);
                if (!actorCaptureTexture.IsCreated())
                {
                    actorCaptureTexture.Create();
                    actorRenderedFrame = -1;
                }
                if (actorRenderedFrame != Time.frameCount ||
                    actorRenderedSprite != sprite || actorRenderedFlip != flip)
                {
                    actorCaptureCamera.Render();
                    actorRenderedFrame = Time.frameCount;
                    actorRenderedSprite = sprite;
                    actorRenderedFlip = flip;
                }
                Color previous = GUI.color;
                try
                {
                    tint.a *= worldActorOpacity;
                    GUI.color = tint;
                    GUI.DrawTexture(new Rect(foot.x - size * .5f,
                        foot.y - size * 120f / 128f, size, size),
                        actorCaptureTexture);
                }
                finally { GUI.color = previous; }
                return true;
            }
            catch (Exception ex)
            {
                actorCaptureBroken = true;
                Debug.LogWarning("Starfall nonhero SpriteRenderer capture " +
                    "disabled; falling back to existing complete actor art: " +
                    ex.Message);
                ReleaseActorCapture();
                return false;
            }
        }

        private void EnsureActorCapture()
        {
            if (actorCaptureCamera != null) return;
            actorCaptureRoot = new GameObject("Starfall Nonhero Actor Capture");
            actorCaptureRoot.hideFlags = HideFlags.HideAndDontSave;
            actorCaptureRoot.transform.position = new Vector3(9200, 9200, 0);
            actorCaptureRoot.layer = ActorCaptureLayer;
            GameObject body = new GameObject("Whole Actor Sprite");
            body.hideFlags = HideFlags.HideAndDontSave;
            body.layer = ActorCaptureLayer;
            body.transform.SetParent(actorCaptureRoot.transform, false);
            actorCaptureRenderer = body.AddComponent<SpriteRenderer>();
            actorCaptureRenderer.sortingOrder = 0;
            // Prepared Animator slot for imported author-created clips; the
            // existing per-enemy GameCore state owns playback until such
            // controllers are genuinely published and verified.
            actorCaptureAnimator = body.AddComponent<Animator>();
            actorCaptureAnimator.enabled = false;
            actorCaptureAnimator.speed = 0f;
            actorCaptureTexture = new RenderTexture(
                256, 256, 16, RenderTextureFormat.ARGB32);
            actorCaptureTexture.filterMode = FilterMode.Point;
            actorCaptureTexture.useMipMap = false;
            actorCaptureTexture.Create();
            GameObject camera = new GameObject("Nonhero Actor Capture Camera");
            camera.hideFlags = HideFlags.HideAndDontSave;
            camera.layer = ActorCaptureLayer;
            camera.transform.SetParent(actorCaptureRoot.transform, false);
            camera.transform.localPosition = new Vector3(
                0f, 120f / 128f - .5f, -10f);
            actorCaptureCamera = camera.AddComponent<Camera>();
            actorCaptureCamera.enabled = false;
            actorCaptureCamera.orthographic = true;
            actorCaptureCamera.orthographicSize = .5f;
            actorCaptureCamera.aspect = 1f;
            actorCaptureCamera.clearFlags = CameraClearFlags.SolidColor;
            actorCaptureCamera.backgroundColor = new Color(0, 0, 0, 0);
            actorCaptureCamera.cullingMask = 1 << ActorCaptureLayer;
            actorCaptureCamera.targetTexture = actorCaptureTexture;
            actorCaptureCamera.allowHDR = false;
            actorCaptureCamera.allowMSAA = false;
        }

        private void ReleaseActorCapture()
        {
            foreach (Sprite sprite in actorFrameSprites.Values)
                if (sprite != null) Destroy(sprite);
            actorFrameSprites.Clear();
            if (actorCaptureCamera != null)
                actorCaptureCamera.targetTexture = null;
            if (actorCaptureRoot != null) Destroy(actorCaptureRoot);
            if (actorCaptureTexture != null)
            {
                actorCaptureTexture.Release();
                Destroy(actorCaptureTexture);
            }
            actorCaptureRoot = null;
            actorCaptureCamera = null;
            actorCaptureRenderer = null;
            actorCaptureAnimator = null;
            actorCaptureTexture = null;
            actorRenderedFrame = -1;
            actorRenderedSprite = null;
            actorRenderedFlip = false;
        }
    }
}
#endif
