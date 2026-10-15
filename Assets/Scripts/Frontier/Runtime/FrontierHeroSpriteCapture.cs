#if UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // Production player art is ONE complete SpriteRenderer body, captured
        // into the legacy GUI world while terrain/HUD are migrated separately.
        // The pre-existing full-body OnGUI path remains a bounded safety
        // fallback on missing resources, device errors, or explicit F8 opt-out.
        // Native Unity 2022.3 PlayMode visual sign-off remains UNVERIFIED.
        [SerializeField] private bool useSpriteRendererHero = true;
        private const int HeroCell = CanonicalHeroSpriteFactory.Cell;
        private const int HeroCaptureLayer = 31;
        private GameObject heroCaptureRoot;
        private Camera heroCaptureCamera;
        private SpriteRenderer heroCaptureRenderer;
        private Animator heroCaptureAnimator;
        // The current target remains discoverable by native GameView tests,
        // but each queued GUI draw retains its own stable captured pixels.
        private RenderTexture heroCaptureTexture;
        private sealed class HeroCaptureEntry
        {
            internal RenderTexture texture;
            internal int lastUsedFrame;
        }
        private const int MaxCachedHeroFrames = 48;
        private readonly Dictionary<string, HeroCaptureEntry> heroCapturedFrames =
            new Dictionary<string, HeroCaptureEntry>();
        // The 63 shipped canonical atlases are real production resources.
        // Only Sprite sub-rect wrappers belong to us; Resources owns textures.
        private readonly Dictionary<string, Sprite[]> heroPublishedSprites =
            new Dictionary<string, Sprite[]>();
        // SpriteImporter already owns most canonical Sprite sub-assets.
        // NEVER Object.Destroy an imported Resources Sprite. Only the rare
        // legacy fallback wrappers we explicitly Sprite.Create are ours.
        private readonly HashSet<Sprite> heroOwnedPublishedSprites =
            new HashSet<Sprite>();
        // Missing/stale Sprite atlas results are temporary during asset
        // reimport. Retry only every ~2 seconds, not on each Repaint, so a
        // corrected canonical texture resumes without forcing an F8 toggle.
        private readonly Dictionary<string,int> heroPublishedUnavailable =
            new Dictionary<string,int>();
        // An imported Animator can exist while its override still references
        // another class, stale clip or wrong direction. Never repeatedly
        // sample a known-mismatched native clip during this play session.
        // A transient Animator graph/import race must not permanently turn
        // off otherwise valid three-class native Sprite curves. Hold the exact
        // verified Sprite while cooling down; retry the native state later.
        private readonly Dictionary<string,int> heroRejectedNativeClips =
            new Dictionary<string,int>();
        private bool heroCaptureBroken;
        // A transient failed GPU capture should never force the canonical
        // hero onto the fallback lane for the entire gameplay session.
        // RenderTexture/driver recovery is attempted only after cooldown.
        private int heroCaptureRetryAt;
        private int heroCaptureProfession = -1;
        private bool heroProductionReady;
        // Unity may finish importing the production .controller after the
        // first GameView draw. Missing controllers require the same bounded
        // recovery as published sprites, not a permanent per-class fallback.
        private int heroControllerRetryAt;
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

        // Formal production renderer: a resolved canonical FULL-BODY Sprite
        // or the same equipment's canonical Idle. No Player*.png, legacy
        // heavy combo pose, body-part synthesis or source-sheet dependence.
        private bool DrawSpriteRendererHero(Point foot, Color tint, bool flip,
            int facing, bool locomotion)
        {
            if(!useSpriteRendererHero||drawingOutline||
                Game==null||Game.mode=="title")
                return false;
            if(heroCaptureBroken)
            {
                // Keep showing the SAME full-body published OnGUI Sprite
                // during cooldown. Do not spam GPU allocations each repaint.
                if(Time.frameCount < heroCaptureRetryAt)return false;
                heroCaptureBroken=false;
            }
            if(Event.current.type!=EventType.Repaint)return true;
            try
            {
                EnsureHeroCapture();
                int action,bakedFrames,publishedFrame;
                float cellPosition;
                if(!ResolveFullBodyHeroFrame(locomotion,facing,
                    out action,out bakedFrames,out publishedFrame,
                    out cellPosition))
                    return false;
                int equipment=Game.AppearanceWeapon;
                string nativeClipKey=equipment+":"+action+":"+facing;
                Sprite canonical;
                bool available=TryUsePublishedHeroFrame(equipment,action,
                    facing,publishedFrame,bakedFrames,out canonical);
                int rejectedUntil;
                bool recentlyRejected =
                    heroRejectedNativeClips.TryGetValue(nativeClipKey,
                        out rejectedUntil) && Time.frameCount < rejectedUntil;
                bool nativeReady=available && !recentlyRejected &&
                    TryUseProductionHeroClip(facing);
                if(nativeReady)
                {
                    // Animator never owns independent damage time.
                    // The current GameCore action-clock selects every key.
                    // The authoritative GameCore selector has already
                    // resolved a whole-body Sprite cell. Animator must sample
                    // INSIDE that discrete keyframe's interval rather than
                    // repeatedly floating along a 12fps boundary: tiny float
                    // errors previously sampled an adjacent Sprite, rejected
                    // the entire native clip and fell back mid-action.
                    // No animation timing, damage/contact or body pixels
                    // are synthesized here.
                    float normalized=(publishedFrame+.5f)/bakedFrames;
                    heroCaptureAnimator.SetInteger("Action",action);
                    heroCaptureAnimator.SetInteger("Facing",facing);
                    heroCaptureAnimator.Play(HeroLiveStateHash[action,facing],
                        0,normalized);
                    heroCaptureAnimator.Update(0f);
                    if(!HeroSpriteMatchesPublishedFrame(
                        heroCaptureRenderer.sprite,canonical))
                    {
                        // A stale cross-class Animator clip must never leak
                        // an unrelated equipment/body into a live scene.
                        heroRejectedNativeClips[nativeClipKey]=
                            Time.frameCount+120;
                        heroCaptureAnimator.enabled=false;
                        heroCaptureRenderer.sprite=canonical;
                        if(missingHeroBodyWarnings.Add("native-clip:"+nativeClipKey))
                            Debug.LogWarning(
                                "Starfall rejected mismatched native full-body "+
                                nativeClipKey+"; holding exact published Sprite "+
                                "and retrying the Animator after cooldown.");
                    }
                    else
                        heroRejectedNativeClips.Remove(nativeClipKey);
                }
                else
                {
                    if(heroCaptureAnimator!=null)
                        heroCaptureAnimator.enabled=false;
                    if(available)
                        heroCaptureRenderer.sprite=canonical;
                    else
                    {
                        Sprite idle;
                        if(!TryUsePublishedHeroFrame(equipment,0,facing,
                            0,1,out idle))return false;
                        heroCaptureRenderer.sprite=idle;
                        string missing="native:"+nativeClipKey;
                        if(missingHeroBodyWarnings.Add(missing))
                            Debug.LogWarning(
                                "Starfall missing canonical hero action "+
                                nativeClipKey+"; holding SAME equipped Idle "+
                                "(old Player model is forbidden).");
                    }
                }
                // A native Animator clip may contain a stale Transform scale
                // curve (or inherit the previous action's scale). Sprite
                // identity/rect/pivot checks alone cannot detect this: the
                // right 256px body can still render 1.5x larger on Attack_N.
                // Capture only the approved 1:1 root, after Animator.Update,
                // for every profession and facing. Never rescale the source
                // artwork, boots or the actual game-world actor position.
                heroCaptureRoot.transform.localScale =
                    new Vector3(1f,1f,1f);
                heroCaptureCamera.transform.localScale =
                    new Vector3(1f,1f,1f);
                heroCaptureCamera.orthographicSize =
                    HeroCell/(2f*CanonicalHeroSpriteFactory.PixelsPerUnit);
                heroCaptureRenderer.flipX = flip;
                // An E->W flip or windup->contact transition within a
                // single GUI repaint must never overwrite an earlier queued
                // GUI.DrawTexture (Unity may submit it later).
                Sprite capturedSprite = heroCaptureRenderer.sprite;
                string captureKey = capturedSprite.GetInstanceID() +
                    (flip ? ":left" : ":right");
                HeroCaptureEntry entry;
                bool needsRender = false;
                if (!heroCapturedFrames.TryGetValue(captureKey, out entry))
                {
                    EvictUnusedHeroCapture();
                    entry = new HeroCaptureEntry {
                        texture = new RenderTexture(HeroCell, HeroCell, 16,
                            RenderTextureFormat.ARGB32),
                        lastUsedFrame = Time.frameCount
                    };
                    entry.texture.filterMode = FilterMode.Point;
                    entry.texture.useMipMap = false;
                    // Record ownership before GPU allocation: release also
                    // cleans up a failed Create or Render.
                    heroCapturedFrames.Add(captureKey, entry);
                    entry.texture.Create();
                    needsRender = true;
                }
                else
                {
                    entry.lastUsedFrame = Time.frameCount;
                    if (!entry.texture.IsCreated())
                    {
                        entry.texture.Create();
                        needsRender = true;
                    }
                }
                heroCaptureTexture = entry.texture;
                // This render target belongs to ONE immutable, validated
                // whole-body Sprite + facing. Returning to that pose must
                // NEVER repaint its target just because some other pose was
                // sampled most recently: an earlier OnGUI draw can still
                // reference this exact RenderTexture in the same frame.
                // Native Animator is sampled above for validation, but its
                // verified Sprite pixels are identical to the cached image.
                if (needsRender)
                {
                    heroCaptureCamera.targetTexture = heroCaptureTexture;
                    try { heroCaptureCamera.Render(); }
                    finally { heroCaptureCamera.targetTexture = null; }
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
                heroCaptureRetryAt = Time.frameCount + 120;
                Debug.LogWarning("Starfall hero SpriteRenderer capture temporarily "
                    + "unavailable; using exact full-body OnGUI art, retry in "
                    + "120 frames: " + error.Message);
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
            int retryAt;
            if(heroPublishedUnavailable.TryGetValue(key,out retryAt)&&
                Time.frameCount<retryAt)return false;
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
                    heroPublishedUnavailable[key]=Time.frameCount+120;
                    if(atlas!=null&&missingHeroBodyWarnings.Add("invalid:"+key))
                        Debug.LogWarning(
                            "Starfall rejected malformed canonical hero atlas: "+
                            key+" (size/frames mismatch). Will retry.");
                    return false;
                }
                heroPublishedUnavailable.Remove(key);
                // Prefer the EXACT shipped imported Sprite sub-assets used
                // by Production Animator clips. Duplicating every cell with
                // Sprite.Create on GPU-only (isReadable=0) atlases previously
                // obscured native asset identity and created extra wrappers.
                // NEVER depend on Resources.LoadAll return ordering: assert
                // every source rect/pivot/PPU/class texture independently.
                string resource = "Frontier/HeroAnimator/Canonical/" + key;
                Sprite[] imported = Resources.LoadAll<Sprite>(resource);
                Sprite[] candidates = new Sprite[expectedFrames];
                int matched = 0;
                foreach (Sprite candidate in imported)
                {
                    if (candidate == null || candidate.texture != atlas ||
                        Mathf.Abs(candidate.rect.y) > .05f ||
                        Mathf.Abs(candidate.rect.width - HeroCell) > .05f ||
                        Mathf.Abs(candidate.rect.height - HeroCell) > .05f ||
                        Mathf.Abs(candidate.pixelsPerUnit -
                            CanonicalHeroSpriteFactory.PixelsPerUnit) > .05f ||
                        Vector2.Distance(candidate.pivot,
                            new Vector2(CanonicalHeroSpriteFactory.FootX,
                                HeroCell - CanonicalHeroSpriteFactory.FootTop)) > .05f)
                        continue;
                    int index = Mathf.RoundToInt(candidate.rect.x / HeroCell);
                    if (index < 0 || index >= expectedFrames ||
                        Mathf.Abs(candidate.rect.x - index * HeroCell) > .05f ||
                        candidates[index] != null) continue;
                    candidates[index] = candidate;
                    matched++;
                }
                if (matched == expectedFrames)
                    cells = candidates; // Shared Unity-owned imported assets.
                else
                {
                    // Controlled whole-frame fallback, not limb synthesis.
                    // Keep ownership of ONLY these new Sprite wrappers, and
                    // never destroy a shared Texture2D from Resources.
                    cells = new Sprite[expectedFrames];
                    for (int i = 0; i < cells.Length; i++)
                    {
                        cells[i] = Sprite.Create(atlas,
                            new Rect(i * HeroCell, 0, HeroCell, HeroCell),
                            CanonicalHeroSpriteFactory.FootPivot,
                            CanonicalHeroSpriteFactory.PixelsPerUnit, 0,
                            SpriteMeshType.FullRect);
                        heroOwnedPublishedSprites.Add(cells[i]);
                    }
                }
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
                // Never leave the old outfit's controller bound while its new
                // class resources are missing or waiting for import.
                heroProductionReady = false;
                heroControllerRetryAt = 0;
                heroCaptureAnimator.runtimeAnimatorController = null;
            }
            if (!heroProductionReady && Time.frameCount >= heroControllerRetryAt)
            {
                RuntimeAnimatorController controller =
                    Resources.Load<RuntimeAnimatorController>(
                        "Frontier/HeroAnimator/Production" +
                        HeroLiveProfessions[profession]);
                heroCaptureAnimator.runtimeAnimatorController = controller;
                heroProductionReady = controller != null;
                // No per-repaint Resources calls when a controller is absent;
                // once Unity imports it, native Animator playback resumes
                // without changing the equipped class or restarting the game.
                heroControllerRetryAt = Time.frameCount + 120;
            }
            if (!heroProductionReady) return false;
            if (!heroCaptureAnimator.enabled) heroCaptureAnimator.enabled = true;
            heroCaptureAnimator.speed = 0f;
            return true;
        }

        // Only retire targets from an earlier frame: current-frame IMGUI
        // commands can still point at any of their render textures.
        private void EvictUnusedHeroCapture()
        {
            if (heroCapturedFrames.Count < MaxCachedHeroFrames) return;
            string oldestKey = null;
            int oldestFrame = int.MaxValue;
            foreach (var pair in heroCapturedFrames)
                if (pair.Value.lastUsedFrame < Time.frameCount &&
                    pair.Value.lastUsedFrame < oldestFrame)
                {
                    oldestKey = pair.Key;
                    oldestFrame = pair.Value.lastUsedFrame;
                }
            if (oldestKey == null) return;
            RenderTexture old = heroCapturedFrames[oldestKey].texture;
            heroCapturedFrames.Remove(oldestKey);
            if (heroCaptureCamera != null &&
                heroCaptureCamera.targetTexture == old)
                heroCaptureCamera.targetTexture = null;
            if (heroCaptureTexture == old) heroCaptureTexture = null;
            old.Release();
            Destroy(old);
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
            // Allocate capture targets lazily, one per actual Sprite+flip.
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
            heroCaptureCamera.targetTexture = null;
            heroCaptureCamera.allowHDR = false;
            heroCaptureCamera.allowMSAA = false;
        }

        private void ReleaseHeroCapture()
        {
            // Destroy only our Sprite sub-rect wrappers, not shared Resources
            // textures which can be referenced by other game systems.
            // A Sprite.Create() can throw halfway through an atlas import.
            // Those partial wrappers are tracked in this ownership set but
            // not necessarily in heroPublishedSprites. Dispose ALL wrappers
            // we created, and never imported Unity-owned Sprite sub-assets.
            foreach(Sprite sprite in heroOwnedPublishedSprites)
                if(sprite!=null)Destroy(sprite);
            heroOwnedPublishedSprites.Clear();
            heroPublishedSprites.Clear();
            heroPublishedUnavailable.Clear();
            heroRejectedNativeClips.Clear();
            if (heroCaptureCamera != null) heroCaptureCamera.targetTexture = null;
            if (heroCaptureRoot != null) Destroy(heroCaptureRoot);
            foreach (HeroCaptureEntry entry in heroCapturedFrames.Values)
                if (entry.texture != null)
                {
                    entry.texture.Release();
                    Destroy(entry.texture);
                }
            heroCapturedFrames.Clear();
            heroCaptureCamera = null;
            heroCaptureRenderer = null;
            heroCaptureAnimator = null;
            heroCaptureRoot = null;
            heroCaptureTexture = null;
            heroCaptureProfession = -1;
            heroProductionReady = false;
            heroControllerRetryAt = 0;
            // Do not clear heroCaptureBroken/retryAt here: the caller may be
            // releasing after an exception, and must respect GPU cooldown.
        }
    }
}
#endif
