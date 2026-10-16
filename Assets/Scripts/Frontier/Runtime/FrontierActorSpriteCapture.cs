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
            // The GPU capture may initially contain the lossless whole-body
            // fallback while Unity finishes importing a native Controller.
            internal bool nativeClipVerified;
        }
        private const int MaxCachedActorFrames = 192;
        // Limit estimated color + 16-bit depth GPU targets as well as count.
        // A 320/512px Boss pose costs much more than a 128px NPC pose.
        // In-flight same-frame GUI submissions may transiently exceed this
        // budget: those targets MUST remain intact until the next repaint.
        private const long MaxCachedActorBytes = 64L * 1024L * 1024L;
        private long actorCachedBytes;
        private static long ActorCaptureCost(int width,int height)
        {
            return (long)width*height*6L;
        }
        private readonly Dictionary<string, CapturedActorFrame> actorCapturedFrames =
            new Dictionary<string, CapturedActorFrame>();
        private readonly Dictionary<string, Sprite> actorFrameSprites =
            new Dictionary<string, Sprite>();
        // Imported Unity Sprite sub-assets are shared, not owned. Cache
        // complete, frame-rect verified sets for the 17 native families.
        private readonly Dictionary<int,Sprite[]> actorImportedFrameSets =
            new Dictionary<int,Sprite[]>();
        // Partial Editor SpriteImporter updates are common during art builds.
        // Do not rescan every imported Sprite for each unseen movement or
        // Boss attack cell while a family is incomplete; retry in 120 frames.
        private readonly Dictionary<int,int> actorImportedRetryAt =
            new Dictionary<int,int>();
        private readonly HashSet<Sprite> actorOwnedFrameSprites =
            new HashSet<Sprite>();
        // An actual imported SpriteRenderer.m_Sprite curve can drive any
        // verified four-column authored actor family. GameCore, not Animator,
        // owns the row and exact frame (windup/release/hit/death/dialogue).
        // Unknown art uses the already verified exact-cell Sprite fallback.
        // Accepted original detailed atlases take precedence over the four
        // retained experimental candidates. This does NOT regenerate art or
        // change the source PNG import GUIDs. Every source is the same one
        // already selected by FrontierActors.DrawEnemyActor/DrawNpc.
        private static readonly string[] NativeActorFamilies = {
            "Monsters", "WalkMonsters", "Bosses", "WalkBossA", "WalkBossB",
            "Npcs", "EnemyArcherVertical",
            "CompanionMira", "CompanionIvo", "CompanionSen",
            "WalkMira", "WalkIvo", "WalkSen",
            "MossboundRaider", "CinderstoneGuard", "BriarBeast",
            "EmberfallResidents"
        };
        private static readonly int[] NativeActorColumnCounts =
            {6,4,4,4,4,4,3,3,3,3,4,4,4,4,4,4,4};
        private static readonly int[] NativeActorRowCounts =
            {3,3,8,4,4,4,2,4,4,4,3,3,3,6,6,18,4};
        private static readonly int[] ActorPoseHashes = CreateActorPoseHashes();
        private static int[] CreateActorPoseHashes()
        {
            int[] hashes = new int[18];
            for (int i = 0; i < hashes.Length; i++)
                hashes[i] = Animator.StringToHash("Base Layer.Pose_" + i);
            return hashes;
        }
        private readonly Dictionary<string,RuntimeAnimatorController> actorNativeControllers =
            new Dictionary<string,RuntimeAnimatorController>();
        private readonly Dictionary<string,int> actorNativeRetryAt =
            new Dictionary<string,int>();
        private readonly Dictionary<string,int> actorNativeRejectedPoseUntil =
            new Dictionary<string,int>();
        private readonly Dictionary<string,Texture2D> actorNativeSource =
            new Dictionary<string,Texture2D>();
        private bool actorCaptureBroken;
        // Recreating a lost render target should not require a scene reload.
        // Retry at most once per 120 frames; until then the SAME source
        // complete-body frame remains visible through Actor() fallback.
        private int actorCaptureRetryAt;

        // Returns false on a recoverable/native failure, so the EXACT same
        // whole source cell can still be drawn by the existing GUI fallback.
        private bool DrawSpriteRendererActor(Texture2D sheet, int columns,
            int rows, int frame, Point foot, float size, Color tint,
            bool flip, float angle)
        {
            if (!useSpriteRendererActors ||
                drawingOutline || sheet == null || angle != 0f ||
                columns <= 0 || rows <= 0 ||
                sheet.width % columns != 0 || sheet.height % rows != 0)
                return false;
            int width = sheet.width / columns;
            int height = sheet.height / rows;
            if (width <= 0 || height <= 0) return false;
            // The previous fixed 256px target silently downsampled 320px
            // Boss keyframes before they reached the screen. Preserve source
            // pixel density for monsters, NPCs and oversized Boss canvases.
            // Very large unreviewed art safely uses the existing exact-cell
            // fallback instead of allocating unbounded GPU textures.
            int capturePixels = Math.Max(128, Math.Max(width, height));
            if (capturePixels > 512) return false;
            if (actorCaptureBroken)
            {
                if (Time.frameCount < actorCaptureRetryAt) return false;
                actorCaptureBroken = false;
            }
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
                    sprite = FindImportedActorFrame(sheet,columns,rows,
                        frame,width,height);
                    if(sprite==null)
                    {
                        // Incomplete or invalid native art may still be
                        // shown as the SAME whole authored source cell.
                        sprite=Sprite.Create(sheet,new Rect(x,y,width,height),
                            new Vector2(.5f, 1f - 120f / 128f),
                            height,0,SpriteMeshType.FullRect);
                        actorOwnedFrameSprites.Add(sprite);
                    }
                    actorFrameSprites.Add(key,sprite);
                }
                string captureKey = key + (flip ? ":flipped" : ":normal");
                CapturedActorFrame entry;
                bool needsRender = false;
                if (!actorCapturedFrames.TryGetValue(captureKey, out entry))
                {
                    EvictUnusedActorCapture(capturePixels);
                    entry = new CapturedActorFrame {
                        texture = new RenderTexture(capturePixels, capturePixels, 16,
                            RenderTextureFormat.ARGB32),
                        lastUsedFrame = Time.frameCount
                    };
                    entry.texture.filterMode = FilterMode.Point;
                    entry.texture.useMipMap = false;
                    // Own the GPU target before Create/Render so an exception
                    // can safely release it in ReleaseActorCapture().
                    actorCapturedFrames.Add(captureKey, entry);
                    actorCachedBytes += ActorCaptureCost(capturePixels,capturePixels);
                    if (!entry.texture.Create() || !entry.texture.IsCreated())
                        throw new InvalidOperationException(
                            "Nonhero whole-actor GPU target creation failed");
                    needsRender = true;
                }
                else
                {
                    // Do not rewrite a RenderTexture earlier in this GUI
                    // frame: a queued draw may still reference its old pixels.
                    bool alreadySubmitted = entry.lastUsedFrame == Time.frameCount;
                    entry.lastUsedFrame = Time.frameCount;
                    if (!entry.texture.IsCreated())
                    {
                        // Repaint can outlive a lost GPU context. Do not
                        // advertise an invisible/invalid native actor.
                        if (!entry.texture.Create() || !entry.texture.IsCreated())
                            throw new InvalidOperationException(
                                "Nonhero whole-actor GPU target recovery failed");
                        needsRender = true; // device/context loss
                    }
                    else if (!entry.nativeClipVerified && !alreadySubmitted &&
                             NativeActorEligible(sheet, columns, rows) &&
                             TryPlayNativeActorPose(sheet, columns, rows, frame, sprite))
                    {
                        // Recapture in the NEXT safe frame after a Controller
                        // imports. Previously a cached fallback remained
                        // forever and the real Animator was never used.
                        needsRender = true;
                        entry.nativeClipVerified = true;
                    }
                }
                if (needsRender)
                {
                    if (!entry.nativeClipVerified)
                        entry.nativeClipVerified =
                            TryPlayNativeActorPose(sheet, columns, rows, frame, sprite);
                    if (!entry.nativeClipVerified)
                    {
                        // True lossless fallback: never show an unrelated
                        // imported animation sheet, stitched head or wrong pose.
                        actorCaptureAnimator.enabled = false;
                        actorCaptureRenderer.sprite = sprite;
                    }
                    // Native Pose_X animation may import stale Transform
                    // position, scale or capture-camera curves. The correct
                    // high-detail Sprite can otherwise suddenly grow, wander
                    // above the planted shadow, or change size on a Boss
                    // state transition even though the source cell is valid.
                    // Reset the isolated OFFSCREEN rig AFTER sampling its
                    // SpriteRenderer.m_Sprite curve, never the game actor.
                    actorCaptureRoot.transform.localScale =
                        new Vector3(1f,1f,1f);
                    actorCaptureRoot.transform.position =
                        new Vector3(9200f,9200f,0f);
                    actorCaptureRoot.transform.localRotation = Quaternion.identity;
                    actorCaptureRenderer.transform.localRotation = Quaternion.identity;
                    actorCaptureRenderer.color = Color.white;
                    actorCaptureRenderer.transform.localPosition =
                        new Vector3(0f,0f,0f);
                    actorCaptureRenderer.flipX = flip;
                    // Original rectangular sheets retain their cell ratio.
                    actorCaptureRenderer.transform.localScale =
                        new Vector3((float)height / width, 1f, 1f);
                    actorCaptureCamera.transform.localScale =
                        new Vector3(1f,1f,1f);
                    actorCaptureCamera.transform.localRotation = Quaternion.identity;
                    actorCaptureCamera.transform.localPosition =
                        new Vector3(0f,120f/128f-.5f,-10f);
                    actorCaptureCamera.orthographicSize = .5f;
                    actorCaptureCamera.aspect = 1f;
                    actorCaptureCamera.targetTexture = entry.texture;
                    try { actorCaptureCamera.Render(); }
                    finally { actorCaptureCamera.targetTexture = null; }
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
                actorCaptureRetryAt = Time.frameCount + 120;
                Debug.LogWarning("Starfall nonhero SpriteRenderer capture " +
                    "temporarily unavailable; retrying after 120 frames and " +
                    "drawing the exact complete-body source cell: " + ex.Message);
                ReleaseActorCapture();
                return false;
            }
        }

        private Sprite FindImportedActorFrame(Texture2D source,int columns,
            int rows,int frame,int width,int height)
        {
            if(!NativeActorEligible(source,columns,rows))return null;
            int atlasId=source.GetInstanceID();
            Sprite[] cells;
            if(!actorImportedFrameSets.TryGetValue(atlasId,out cells))
            {
                int retryAt;
                if(actorImportedRetryAt.TryGetValue(atlasId,out retryAt)&&
                    Time.frameCount < retryAt)return null;
                Sprite[] published=Resources.LoadAll<Sprite>(
                    "Frontier/Art/"+source.name);
                if(published==null||published.Length<columns*rows)
                {
                    actorImportedRetryAt[atlasId]=Time.frameCount+120;
                    return null;
                }
                // LoadAll order is unspecified; index imported art using
                // ORIGINAL atlas rectangles and published pixel-foot pivots.
                // Do not transition one animation into a partially imported
                // different cell family.
                Sprite[] ordered=new Sprite[columns*rows];
                int matched=0;
                foreach(Sprite candidate in published)
                {
                    if(candidate==null||candidate.texture!=source||
                        Mathf.Abs(candidate.rect.width-width)>.05f||
                        Mathf.Abs(candidate.rect.height-height)>.05f||
                        Mathf.Abs(candidate.pixelsPerUnit-height)>.05f||
                        Vector2.Distance(candidate.pivot,new Vector2(
                            width*.5f,height*(1f-120f/128f)))>.05f)
                        continue;
                    int col=Mathf.RoundToInt(candidate.rect.x/width);
                    int row=Mathf.RoundToInt(
                        (source.height-candidate.rect.y)/height-1f);
                    if(col<0||col>=columns||row<0||row>=rows||
                        Mathf.Abs(candidate.rect.x-col*width)>.05f||
                        Mathf.Abs(candidate.rect.y-
                            (source.height-(row+1)*height))>.05f)
                        continue;
                    int index=row*columns+col;
                    if(ordered[index]!=null)continue;
                    ordered[index]=candidate;matched++;
                }
                if(matched!=ordered.Length)
                {
                    // Never publish a mixed/partially reimported family.
                    actorImportedRetryAt[atlasId]=Time.frameCount+120;
                    return null;
                }
                actorImportedRetryAt.Remove(atlasId);
                cells=ordered;
                actorImportedFrameSets.Add(atlasId,cells);
            }
            return cells[frame];
        }

        private static bool NativeActorEligible(Texture2D source, int columns,
            int rows)
        {
            if (source == null) return false;
            int kind = Array.IndexOf(NativeActorFamilies, source.name);
            return kind >= 0 && columns == NativeActorColumnCounts[kind] &&
                rows == NativeActorRowCounts[kind];
        }

        private bool TryPlayNativeActorPose(Texture2D source, int columns,
            int rows, int frame, Sprite expected)
        {
            if (actorCaptureAnimator == null || !NativeActorEligible(
                    source, columns, rows) || frame < 0) return false;
            string family = source.name;
            Texture2D installed;
            if (!actorNativeSource.TryGetValue(family, out installed) ||
                installed != source)
            {
                installed = Resources.Load<Texture2D>("Frontier/Art/" + family);
                if (installed != source) return false;
                actorNativeSource[family] = installed;
            }
            RuntimeAnimatorController controller;
            if (!actorNativeControllers.TryGetValue(family, out controller))
            {
                int retryAt;
                if (actorNativeRetryAt.TryGetValue(family, out retryAt) &&
                    Time.frameCount < retryAt) return false;
                controller = Resources.Load<RuntimeAnimatorController>(
                    "Frontier/Actors/Native/Actor_" + family);
                if (controller == null)
                {
                    actorNativeRetryAt[family] = Time.frameCount + 120;
                    return false;
                }
                actorNativeControllers[family] = controller;
                actorNativeRetryAt.Remove(family);
            }
            int row = frame / columns;
            string poseKey = family + ":" + row;
            int rejectedUntil;
            if (actorNativeRejectedPoseUntil.TryGetValue(poseKey, out rejectedUntil) &&
                Time.frameCount < rejectedUntil) return false;
            if (actorCaptureAnimator.runtimeAnimatorController != controller)
                actorCaptureAnimator.runtimeAnimatorController = controller;
            actorCaptureAnimator.enabled = true;
            actorCaptureAnimator.speed = 0f;
            actorCaptureAnimator.SetInteger("Pose", row);
            // Always sample INSIDE the authoritative GameCore cell's 12fps
            // interval. Never let Animator interpolate its own combat clock.
            actorCaptureAnimator.Play(ActorPoseHashes[row], 0,
                ((frame % columns) + .5f) / columns);
            actorCaptureAnimator.Update(0f);
            Sprite actual = actorCaptureRenderer.sprite;
            if (actual != null && expected != null &&
                actual.texture == source &&
                Mathf.Abs(actual.rect.x - expected.rect.x) < .05f &&
                Mathf.Abs(actual.rect.y - expected.rect.y) < .05f &&
                Mathf.Abs(actual.rect.width - expected.rect.width) < .05f &&
                Mathf.Abs(actual.rect.height - expected.rect.height) < .05f &&
                Vector2.Distance(actual.pivot, expected.pivot) < .05f &&
                Mathf.Abs(actual.pixelsPerUnit - expected.pixelsPerUnit) < .05f)
            {
                actorNativeRejectedPoseUntil.Remove(poseKey);
                return true;
            }
            // A stale or cross-identity clip is a deterministic FAIL at
            // runtime: hold the exact source Sprite, then permit bounded
            // recovery following Unity asset reimport.
            actorCaptureAnimator.enabled = false;
            actorNativeRejectedPoseUntil[poseKey] = Time.frameCount + 120;
            actorNativeControllers.Remove(family);
            return false;
        }

        // Only recycle targets from a PREVIOUS frame. Never destroy a
        // texture that another actor's pending GUI command still references.
        // A single crowded repaint may exceed the soft limit temporarily.
        private void EvictUnusedActorCapture(int requestedPixels)
        {
            long incomingBytes = ActorCaptureCost(requestedPixels,requestedPixels);
            while (actorCapturedFrames.Count >= MaxCachedActorFrames ||
                   actorCachedBytes + incomingBytes > MaxCachedActorBytes)
            {
                string oldestKey = null;
                int oldestFrame = int.MaxValue;
                foreach (var item in actorCapturedFrames)
                    if (item.Value.lastUsedFrame < Time.frameCount &&
                        item.Value.lastUsedFrame < oldestFrame)
                    {
                        oldestKey = item.Key;
                        oldestFrame = item.Value.lastUsedFrame;
                    }
                // All candidates may still be queued for THIS repaint.
                // Never destroy a submitted frame to meet a memory target.
                if (oldestKey == null) return;
                RenderTexture old = actorCapturedFrames[oldestKey].texture;
                actorCapturedFrames.Remove(oldestKey);
                // The original implementation retired GPU targets but
                // accumulated one Sprite.Create wrapper for EVERY visited
                // NPC/monster/Boss cell across rooms. A companion's two
                // mirrored captures also share that wrapper: release the
                // temporary Sprite only after its LAST GPU variant goes.
                // Last-use < current frame guarantees no live GUI command
                // depends on either the old texture or this wrapper.
                int split = oldestKey.LastIndexOf(':');
                if (split > 0)
                {
                    string frameKey = oldestKey.Substring(0, split);
                    if (!actorCapturedFrames.ContainsKey(frameKey + ":normal") &&
                        !actorCapturedFrames.ContainsKey(frameKey + ":flipped"))
                    {
                        Sprite retired;
                        if (actorFrameSprites.TryGetValue(frameKey, out retired))
                        {
                            actorFrameSprites.Remove(frameKey);
                            if (actorCaptureRenderer != null &&
                                actorCaptureRenderer.sprite == retired)
                                actorCaptureRenderer.sprite = null;
                            // Resources Sprite sub-assets belong to Unity,
                            // never to the temporary capture target cache.
                            if(retired!=null&&actorOwnedFrameSprites.Remove(retired))
                                Destroy(retired);
                        }
                    }
                }
                if (actorCaptureCamera != null &&
                    actorCaptureCamera.targetTexture == old)
                    actorCaptureCamera.targetTexture = null;
                actorCachedBytes = Math.Max(0L, actorCachedBytes -
                    ActorCaptureCost(old.width,old.height));
                old.Release();
                Destroy(old);
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
            foreach(Sprite sprite in actorOwnedFrameSprites)
                if(sprite!=null)Destroy(sprite);
            actorOwnedFrameSprites.Clear();
            actorFrameSprites.Clear();
            actorImportedFrameSets.Clear();
            actorImportedRetryAt.Clear();
            actorNativeControllers.Clear();
            actorNativeSource.Clear();
            actorNativeRetryAt.Clear();
            actorNativeRejectedPoseUntil.Clear();
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
            actorCachedBytes = 0L;
            actorCaptureRoot = null;
            actorCaptureCamera = null;
            actorCaptureRenderer = null;
            actorCaptureAnimator = null;
        }
    }
}
#endif
