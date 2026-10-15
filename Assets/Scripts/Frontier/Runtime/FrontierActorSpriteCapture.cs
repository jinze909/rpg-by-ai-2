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
        // GUI.DrawTexture commands may be deferred within one repaint.
        // Reusing one mutable RenderTexture for multiple actors lets later
        // captures overwrite the pixels sampled by earlier GUI commands.
        // Each unique complete-body pose/flip therefore owns a stable target.
        private sealed class CapturedActorFrame
        {
            internal RenderTexture texture;
            internal int lastUsedFrame;
        }
        private const int MaxCachedActorFrames = 192;
        private readonly Dictionary<string, CapturedActorFrame> actorCapturedFrames =
            new Dictionary<string, CapturedActorFrame>();
        private readonly Dictionary<string, Sprite> actorFrameSprites =
            new Dictionary<string, Sprite>();
        private bool actorCaptureBroken;

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
                string captureKey = key + (flip ? ":flipped" : ":normal");
                CapturedActorFrame entry;
                bool needsRender = false;
                if (!actorCapturedFrames.TryGetValue(captureKey, out entry))
                {
                    EvictUnusedActorCapture();
                    entry = new CapturedActorFrame {
                        texture = new RenderTexture(256, 256, 16,
                            RenderTextureFormat.ARGB32),
                        lastUsedFrame = Time.frameCount
                    };
                    entry.texture.filterMode = FilterMode.Point;
                    entry.texture.useMipMap = false;
                    // Own the GPU target before Create/Render so an exception
                    // can safely release it in ReleaseActorCapture().
                    actorCapturedFrames.Add(captureKey, entry);
                    entry.texture.Create();
                    needsRender = true;
                }
                else
                {
                    entry.lastUsedFrame = Time.frameCount;
                    if (!entry.texture.IsCreated())
                    {
                        entry.texture.Create();
                        needsRender = true; // device/context loss
                    }
                }
                if (needsRender)
                {
                    actorCaptureRenderer.sprite = sprite;
                    actorCaptureRenderer.flipX = flip;
                    // Legacy Actor() fills a square regardless of atlas cell
                    // aspect ratio. Preserve the exact foot/pivot position.
                    actorCaptureRenderer.transform.localScale =
                        new Vector3((float)height / width, 1f, 1f);
                    actorCaptureCamera.targetTexture = entry.texture;
                    actorCaptureCamera.Render();
                    actorCaptureCamera.targetTexture = null;
                }
                Color previous = GUI.color;
                try
                {
                    tint.a *= worldActorOpacity;
                    GUI.color = tint;
                    GUI.DrawTexture(new Rect(foot.x - size * .5f,
                        foot.y - size * 120f / 128f, size, size),
                        entry.texture);
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

        // Only recycle targets from a PREVIOUS frame. Never destroy a
        // texture that another actor's pending GUI command still references.
        // A single crowded repaint may exceed the soft limit temporarily.
        private void EvictUnusedActorCapture()
        {
            if (actorCapturedFrames.Count < MaxCachedActorFrames) return;
            string oldestKey = null;
            int oldestFrame = int.MaxValue;
            foreach (var item in actorCapturedFrames)
                if (item.Value.lastUsedFrame < Time.frameCount &&
                    item.Value.lastUsedFrame < oldestFrame)
                {
                    oldestKey = item.Key;
                    oldestFrame = item.Value.lastUsedFrame;
                }
            if (oldestKey == null) return;
            RenderTexture old = actorCapturedFrames[oldestKey].texture;
            actorCapturedFrames.Remove(oldestKey);
            if (actorCaptureCamera != null &&
                actorCaptureCamera.targetTexture == old)
                actorCaptureCamera.targetTexture = null;
            old.Release();
            Destroy(old);
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
            foreach (CapturedActorFrame entry in actorCapturedFrames.Values)
                if (entry.texture != null)
                {
                    entry.texture.Release();
                    Destroy(entry.texture);
                }
            actorCapturedFrames.Clear();
            actorCaptureRoot = null;
            actorCaptureCamera = null;
            actorCaptureRenderer = null;
            actorCaptureAnimator = null;
        }
    }
}
#endif
