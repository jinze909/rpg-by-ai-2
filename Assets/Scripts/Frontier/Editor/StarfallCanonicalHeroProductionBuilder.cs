#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Production, NOT a disconnected demo: bake the accepted source pixel
    /// atlases through exactly the same blonde-head compositor used in the
    /// live SpriteRenderer capture. Generate 63 native animation clips with
    /// actual SpriteRenderer.m_Sprite curves, plus three class controllers.
    /// All generated assets remain in their own Resources subdirectory and
    /// never overwrite any original Player/Walk atlas or its GUID.
    /// </summary>
    public static class StarfallCanonicalHeroProductionBuilder
    {
        private const string Generated = "Assets/Resources/Frontier/HeroAnimator/Canonical/";
        private const string Controllers = "Assets/Resources/Frontier/HeroAnimator/";
        private const string Clips = "Assets/Animations/Frontier/HeroProduction/";
        private const int Cell = CanonicalHeroSpriteFactory.Cell;
        private static readonly string[] Classes = { "Vanguard", "Ranger", "Arcanist" };
        private static readonly string[] Actions = { "Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death" };
        private static readonly string[] Facings = { "E", "N", "S" };
        private struct FrameSource
        {
            public string atlas;
            public int index;
            public FrameSource(string name, int frame) { atlas = name; index = frame; }
        }

        // The actual Frontier.unity is bootstrapped at runtime. A prefab-only
        // Animator build is NOT enough: ensure published Sprite curves/controllers
        // exist before its FIRST editor PlayMode, not only when BuildPlayer runs.
        // Keep the human-review gate separate from native PlayMode verification.
        [InitializeOnLoadMethod]
        private static void RegisterProductionPlayModeBake()
        {
            EditorApplication.playModeStateChanged -= OnProductionPlayModeChanged;
            EditorApplication.playModeStateChanged += OnProductionPlayModeChanged;
            EditorApplication.delayCall += EnsureWhenEditorIdle;
        }

        private static void EnsureWhenEditorIdle()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EnsurePublishedControllerAvailable();
        }

        private static void OnProductionPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                EnsurePublishedControllerAvailable();
        }

        private static void EnsurePublishedControllerAvailable()
        {
            if (!File.Exists(Generated + "Hero_Vanguard_Idle_E.png"))
            {
                Debug.LogError("Starfall: production canonical hero PNGs missing. "+
                    "SpriteRenderer will use intact original full-body art.");
                return;
            }
            try { EnsureBuilt(); }
            catch (Exception error)
            {
                // Safety fallback keeps the game playable. Never claim that
                // the native Animator path was built or artist-verified.
                Debug.LogError("Starfall: production hero Animator bake FAILED; "+
                    "runtime SpriteRenderer will use shipped complete-body "+
                    "frames without native Animator curves. " + error);
            }
        }

        /// <summary>
        /// A playable Unity project should not require an extra manual menu
        /// action to discover the already published three-profession sprites.
        /// Only create missing native assets; preserve present controllers,
        /// clips, their GUIDs, all scene content and player save files.
        /// </summary>
        public static void EnsureBuilt()
        {
            bool ready = true;
            int contactSevenCount = 0;
            var spriteBinding = new EditorCurveBinding {
                path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
            foreach (string profession in Classes)
            {
                string path = Controllers + "Production" + profession +
                    (profession == "Vanguard" ? ".controller" : ".overrideController");
                if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path) == null)
                    ready = false;
                for (int action = 0; action < Actions.Length; action++)
                    for (int facing = 0; facing < Facings.Length; facing++)
                    {
                        string key = Key(profession, action, facing);
                        FrameSource[] approved = FrameRefs(
                            Array.IndexOf(Classes, profession), action, facing);
                        VerifySameProfessionBody(profession, approved);
                        int expected = ExpandedCount(approved.Length, action);
                        // Existing .anim files are NOT necessarily current:
                        // Cast grew from 3 to 7 Sprite cells. A mere existence
                        // check silently left old curves active in gameplay.
                        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            Generated + key + ".png");
                        expected = PublishedFrameCount(atlas, approved.Length, action);
                        if ((action == 2 || action == 3) && expected == 7)
                            contactSevenCount++;
                        if (atlas == null || atlas.width != expected * Cell ||
                            atlas.height != Cell)
                            ready = false;
                        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                            Clips + key + ".anim");
                        if (clip == null)
                        {
                            ready = false;
                            continue;
                        }
                        ObjectReferenceKeyframe[] frames =
                            AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
                        if (!NativeClipMatchesPublished(frames, Generated + key + ".png",
                                expected, action))
                            ready = false;
                    }
            }
            if (contactSevenCount != 0 && contactSevenCount != 18)
                ready = false;
            // A 63-clip file audit does not prove that the production
            // Controller states/override mapping use those files. Wrong
            // class in an existing override previously stayed "ready".
            if (ready && !NativeControllersMatchPublished())
                ready = false;
            if (!ready) Build();
        }

        private static bool HasExactProductionTransition(
            AnimatorStateTransition transition, int action, int facing)
        {
            if (transition == null || transition.hasExitTime ||
                transition.duration > .001f || transition.canTransitionToSelf)
                return false;
            AnimatorCondition[] conditions = transition.conditions;
            return conditions.Length == 2 &&
                conditions.Any(c => c.parameter == "Action" &&
                    c.mode == AnimatorConditionMode.Equals &&
                    Mathf.Abs(c.threshold - action) < .001f) &&
                conditions.Any(c => c.parameter == "Facing" &&
                    c.mode == AnimatorConditionMode.Equals &&
                    Mathf.Abs(c.threshold - facing) < .001f);
        }

        private static bool NativeControllersMatchPublished()
        {
            const string basePath = Controllers + "ProductionVanguard.controller";
            AnimatorController basis = AssetDatabase.LoadAssetAtPath<AnimatorController>(basePath);
            if (basis == null || basis.layers.Length != 1 ||
                basis.layers[0].stateMachine == null)
                return false;
            if (!basis.parameters.Any(p => p.name == "Action" &&
                    p.type == AnimatorControllerParameterType.Int) ||
                !basis.parameters.Any(p => p.name == "Facing" &&
                    p.type == AnimatorControllerParameterType.Int))
                return false;
            AnimatorStateMachine machine = basis.layers[0].stateMachine;
            for (int action = 0; action < Actions.Length; action++)
                for (int facing = 0; facing < Facings.Length; facing++)
                {
                    string name = Actions[action] + "_" + Facings[facing];
                    AnimatorState[] states = machine.states
                        .Select(s => s.state).Where(s => s.name == name).ToArray();
                    if (states.Length != 1 ||
                        states[0].motion != AssetDatabase.LoadAssetAtPath<AnimationClip>(
                            Clips + Key("Vanguard", action, facing) + ".anim"))
                        return false;
                    AnimatorState state = states[0];
                    AnimatorStateTransition[] transitions = machine.anyStateTransitions
                        .Where(t => t.destinationState == state).ToArray();
                    if (transitions.Length != 1 ||
                        !HasExactProductionTransition(transitions[0], action, facing))
                        return false;
                }
            for (int cls = 1; cls < Classes.Length; cls++)
            {
                string profession = Classes[cls];
                AnimatorOverrideController controller =
                    AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                        Controllers + "Production" + profession + ".overrideController");
                if (controller == null || controller.runtimeAnimatorController != basis)
                    return false;
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(overrides);
                if (overrides.Count != Actions.Length * Facings.Length)
                    return false;
                for (int action = 0; action < Actions.Length; action++)
                    for (int facing = 0; facing < Facings.Length; facing++)
                    {
                        AnimationClip key = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                            Clips + Key("Vanguard", action, facing) + ".anim");
                        AnimationClip value = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                            Clips + Key(profession, action, facing) + ".anim");
                        if (overrides.Count(pair => pair.Key == key &&
                            pair.Value == value) != 1)
                            return false;
                    }
            }
            return true;
        }

        // The validated seven-cell set is optional until all 18 genuinely
        // hand-drawn contact sheets are published. Never invent the missing
        // seventh cell by modifying playback speed or sprite raster geometry.
        private static int PublishedFrameCount(Texture2D atlas, int sourceCount, int action)
        {
            int expected = ExpandedCount(sourceCount, action);
            if (atlas != null && atlas.height == Cell && expected == 6 &&
                (action == 2 || action == 3) && atlas.width == 7 * Cell)
                return 7;
            return expected;
        }

        private static bool NativeClipMatchesPublished(
            ObjectReferenceKeyframe[] keys, string atlasPath, int expected, int action)
        {
            // Count alone cannot detect same-length but incorrectly overridden
            // Ranger/Arcanist clips, wrong direction rows or moved foot pivots.
            if (keys == null || keys.Length != expected + 1) return false;
            const float sampleRate = 12f;
            for (int i = 0; i < keys.Length; i++)
            {
                Sprite sprite = keys[i].value as Sprite;
                if (sprite == null) return false;
                int index = i == expected
                    ? (action <= 1 ? 0 : expected - 1) : i;
                if (AssetDatabase.GetAssetPath(sprite) != atlasPath ||
                    Mathf.Abs(sprite.rect.x - index * Cell) > .05f ||
                    Mathf.Abs(sprite.rect.y) > .05f ||
                    Mathf.Abs(sprite.rect.width - Cell) > .05f ||
                    Mathf.Abs(sprite.rect.height - Cell) > .05f ||
                    Vector2.Distance(sprite.pivot,
                        new Vector2(CanonicalHeroSpriteFactory.FootX,
                            Cell - CanonicalHeroSpriteFactory.FootTop)) > .05f ||
                    Mathf.Abs(sprite.pixelsPerUnit -
                        CanonicalHeroSpriteFactory.PixelsPerUnit) > .05f ||
                    Mathf.Abs(keys[i].time - i / sampleRate) > .001f)
                    return false;
            }
            return true;
        }

        private static void VerifySameProfessionBody(string profession, FrameSource[] frames)
        {
            // Python's release guard is not enough: the fallback Unity Editor
            // baker must independently refuse the wrong profession's body
            // source, even when the frame canvas and pivot happen to match.
            int cls = Array.IndexOf(Classes, profession);
            if (cls < 0) throw new InvalidOperationException(
                "Unknown hero outfit: " + profession);
            foreach (FrameSource src in frames)
            {
                bool native = src.atlas == "Player" + profession ||
                    src.atlas == "Walk" + profession;
                bool shared = src.atlas == "PlayerRear" ||
                    src.atlas == "PlayerSouth" ||
                    src.atlas == "PlayerReactionsRear" ||
                    src.atlas == "PlayerReactionsSouth";
                if (!native && !(shared && src.index / 4 == cls))
                    throw new InvalidOperationException(
                        "Cross-profession body source blocked: " + profession +
                        " attempted " + src.atlas + ":" + src.index);
            }
        }

        [MenuItem("Starfall/Animation/Bake Accepted Hero To Live 3-Class Animator")]
        public static void Build()
        {
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory(Controllers);
            Directory.CreateDirectory(Clips);
            var atlasPaths = new Dictionary<string, string>();
            int installedSevenContacts = 0;
            foreach (string profession in Classes)
                for (int action = 0; action < Actions.Length; action++)
                    for (int facing = 0; facing < Facings.Length; facing++)
                    {
                        string key = Key(profession, action, facing);
                        FrameSource[] refs = FrameRefs(Array.IndexOf(Classes, profession),
                            action, facing);
                        VerifySameProfessionBody(profession, refs);
                        // Published, CI-checked PNGs are authoritative. An
                        // Editor Animator bake must NOT silently regenerate
                        // their accepted pixels with different C# rounding.
                        // Generate only when this resource is genuinely absent.
                        string installedPath = Generated + key + ".png";
                        int expectedCount = ExpandedCount(refs.Length, action);
                        if (File.Exists(installedPath))
                        {
                            Texture2D installed = AssetDatabase.LoadAssetAtPath<Texture2D>(
                                installedPath);
                            expectedCount = PublishedFrameCount(installed, refs.Length, action);
                            if ((action == 2 || action == 3) && expectedCount == 7)
                                installedSevenContacts++;
                            if (installed == null || installed.height != Cell ||
                                installed.width != expectedCount * Cell)
                                throw new InvalidOperationException(
                                    "Published hero atlas has incompatible frames: " + key);
                            atlasPaths.Add(key, installedPath);
                            continue;
                        }
                        // CI-approved, shipped complete-body RGBA is the
                        // ONLY permitted animation source. Silently recreating
                        // missing sheets using old mechanical limb interpolation
                        // would restore the very splits/jitter rejected by QA.
                        // Stop the build instead of inventing unreviewed art.
                        throw new InvalidOperationException(
                            "Missing approved full-body hero atlas: " + key +
                            ". Run verified Sprite export/publish on chatgpt/dev " +
                            "and preserve the established Sprite GUIDs.");
                    }
            if (installedSevenContacts != 0 && installedSevenContacts != 18)
                throw new InvalidOperationException(
                    "Mixed six/seven-cell Attack/Cast assets: all 18 "+
                    "hand-drawn contacts must be published atomically.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string profession in Classes)
                for (int action = 0; action < Actions.Length; action++)
                    for (int facing = 0; facing < Facings.Length; facing++)
                    {
                        string key = Key(profession, action, facing);
                        FrameSource[] refs = FrameRefs(Array.IndexOf(Classes, profession),
                            action, facing);
                        VerifySameProfessionBody(profession, refs);
                        string path = atlasPaths[key];
                        int publishedCount = PublishedFrameCount(
                            AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                            refs.Length, action);
                        ImportCanonicalSheet(path, key, publishedCount);
                        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<Sprite>()
                            .OrderBy(s => FrameIndex(s.name))
                            .ToArray();
                        if (frames.Length != publishedCount ||
                            frames.Any(s => s.rect.width != Cell || s.rect.height != Cell ||
                                Vector2.Distance(s.pivot,
                                    new Vector2(CanonicalHeroSpriteFactory.FootX,
                                        Cell - CanonicalHeroSpriteFactory.FootTop)) > .05f))
                            throw new InvalidOperationException("Canonical foot pivot/sheet import invalid: " + key);
                        string clipPath = Clips + key + ".anim";
                        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                        if (clip == null)
                        {
                            clip = new AnimationClip { frameRate = 12f };
                            AssetDatabase.CreateAsset(clip, clipPath);
                        }
                        var curve = new ObjectReferenceKeyframe[frames.Length + 1];
                        for (int i = 0; i < frames.Length; i++)
                            curve[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
                        curve[frames.Length] = new ObjectReferenceKeyframe {
                            time = frames.Length / 12f,
                            value = action <= 1 ? frames[0] : frames[frames.Length - 1] };
                        AnimationUtility.SetObjectReferenceCurve(clip,
                            new EditorCurveBinding { path = "", type = typeof(SpriteRenderer),
                                propertyName = "m_Sprite" }, curve);
                        SerializedObject settings = new SerializedObject(clip);
                        SerializedProperty loop = settings.FindProperty("m_AnimationClipSettings.m_LoopTime");
                        if (loop != null) { loop.boolValue = action <= 1; settings.ApplyModifiedProperties(); }
                        clips.Add(key, clip);
                    }

            const string basePath = Controllers + "ProductionVanguard.controller";
            AnimatorController baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(basePath);
            if (baseController == null)
                baseController = AnimatorController.CreateAnimatorControllerAtPath(basePath);
            // Fix stale parameter types, not only absent names: a float
            // "Action" can silently invalidate every transition.
            foreach (AnimatorControllerParameter parameter in
                     baseController.parameters.Where(p =>
                         (p.name == "Action" || p.name == "Facing") &&
                         p.type != AnimatorControllerParameterType.Int).ToArray())
                baseController.RemoveParameter(parameter);
            if (!baseController.parameters.Any(p => p.name == "Action"))
                baseController.AddParameter("Action", AnimatorControllerParameterType.Int);
            if (!baseController.parameters.Any(p => p.name == "Facing"))
                baseController.AddParameter("Facing", AnimatorControllerParameterType.Int);
            AnimatorStateMachine machine = baseController.layers[0].stateMachine;
            for (int action = 0; action < Actions.Length; action++)
                for (int facing = 0; facing < Facings.Length; facing++)
                {
                    string name = Actions[action] + "_" + Facings[facing];
                    AnimatorState state = machine.states.Select(s => s.state)
                        .FirstOrDefault(s => s.name == name);
                    if (state == null) state = machine.AddState(name);
                    // Rebuild ONLY the published production AnyState
                    // transition to this generated state when stale. Old
                    // wrong-class or missing conditions must never survive.
                    AnimatorStateTransition[] existing = machine.anyStateTransitions
                        .Where(t => t.destinationState == state).ToArray();
                    if (existing.Length != 1 ||
                        !HasExactProductionTransition(existing[0], action, facing))
                    {
                        foreach (AnimatorStateTransition obsolete in existing)
                            machine.RemoveAnyStateTransition(obsolete);
                        AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
                        transition.hasExitTime = false;
                        transition.duration = 0;
                        transition.canTransitionToSelf = false;
                        transition.AddCondition(AnimatorConditionMode.Equals, action, "Action");
                        transition.AddCondition(AnimatorConditionMode.Equals, facing, "Facing");
                    }
                    state.motion = clips[Key("Vanguard", action, facing)];
                    if (action == 0 && facing == 0) machine.defaultState = state;
                }
            for (int cls = 1; cls < Classes.Length; cls++)
            {
                string profession = Classes[cls];
                string path = Controllers + "Production" + profession + ".overrideController";
                AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                if (controller == null)
                {
                    controller = new AnimatorOverrideController(baseController);
                    AssetDatabase.CreateAsset(controller, path);
                }
                controller.runtimeAnimatorController = baseController;
                var mappings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                for (int action = 0; action < Actions.Length; action++)
                    for (int facing = 0; facing < Facings.Length; facing++)
                        mappings.Add(new KeyValuePair<AnimationClip, AnimationClip>(
                            clips[Key("Vanguard", action, facing)],
                            clips[Key(profession, action, facing)]));
                controller.ApplyOverrides(mappings);
            }
            AssetDatabase.SaveAssets();
            // A successful asset write is NOT a successful scene integration.
            // Re-read all 21 Vanguard states and both override tables from
            // Unity's serialized AssetDatabase before reporting success.
            if (!NativeControllersMatchPublished())
                throw new InvalidOperationException(
                    "Production hero Animator graph/overrides do not match "+
                    "all 63 source-verified Sprite clips after repair.");
            Debug.Log("Production hero SpriteRenderer clips baked from ACCEPTED original atlases: "
                + "3 classes x 7 actions x 3 facings, root=(128,240). "
                + "The runtime F8 lane can now load real Sprite curves. "
                + "Frames are full-body source-locked drawings; 18 central Attack/Cast contacts and native art acceptance remain separately unverified.");
        }


        // One deterministic pixel midpoint between consecutive *approved*
        // poses. Run also bridges the final pose back to the first; action
        // clips never loop into their windup. Keeps the approved 256px root.
        // The motion pass is an authoring scaffold, not a replacement for
        // manually drawn anticipation/contact/recovery art.
        private static int ExpandedCount(int original, int action)
        {
            if (original > 1 && (action == 2 || action == 3))
                return original * 2 - 2;
            if (original > 1 && (action == 4 || action == 5))
                return original * 2 - 1;
            return original;
        }

        private static string Key(string profession, int action, int facing)
        { return "Hero_" + profession + "_" + Actions[action] + "_" + Facings[facing]; }

        private static FrameSource[] FrameRefs(int cls, int action, int facing)
        {
            string p = "Player" + Classes[cls], walk = "Walk" + Classes[cls];
            if (action == 0)
                return new[] { new FrameSource(facing == 2 ? walk : p,
                    facing == 2 ? 8 : facing == 1 ? 1 : 0) };
            if (action == 1)
                return Enumerable.Range(facing * 4, 4).Select(i => new FrameSource(walk, i)).ToArray();
            if (action == 2)
            {
                if (facing == 1)
                    return new[] { new FrameSource(p, 1),
                        new FrameSource("PlayerRear", cls * 4 + 2),
                        new FrameSource("PlayerRear", cls * 4 + 3),
                        new FrameSource(p, 1) };
                if (facing == 2)
                    return new[] { new FrameSource(walk, 8),
                        new FrameSource("PlayerSouth", cls * 4),
                        new FrameSource("PlayerSouth", cls * 4 + 1),
                        new FrameSource(walk, 8) };
                return new[] { new FrameSource(p, 0), new FrameSource(p, 4),
                    new FrameSource(p, 5), new FrameSource(p, 0) };
            }
            if (action == 3)
            {
                // Shared accepted hero identity and exact foot registration.
                // Four authored source poses produce seven real Sprite cells.
                FrameSource idle = facing == 2 ? new FrameSource(walk, 8) :
                    new FrameSource(p, facing == 1 ? 1 : 0);
                string skillAtlas = facing == 1 ? "PlayerRear" :
                    facing == 2 ? "PlayerSouth" : p;
                return new[] { idle,
                    new FrameSource(skillAtlas, facing == 0 ? 6 : cls * 4 + 2),
                    new FrameSource(skillAtlas, facing == 0 ? 7 : cls * 4 + 3),
                    idle };
            }
            string reactions = facing == 1 ? "PlayerReactionsRear" :
                facing == 2 ? "PlayerReactionsSouth" : p;
            int dodge = facing == 0 ? 8 : cls * 4;
            int hit = facing == 0 ? 10 : cls * 4 + 2;
            int fallen = facing == 0 ? 11 : cls * 4 + 3;
            // Never flash straight from standing to a grounded corpse.
            // Approved prior poses are reused; no rejected drawings imported.
            FrameSource stand = facing == 2 ? new FrameSource(walk, 8) :
                new FrameSource(p, facing == 1 ? 1 : 0);
            if (action == 4) return new[] {
                stand, new FrameSource(reactions, dodge), stand };
            if (action == 5) return new[] {
                stand, new FrameSource(reactions, hit), stand };
            return new[] { stand, new FrameSource(reactions, hit),
                new FrameSource(reactions, dodge),
                new FrameSource(reactions, fallen) };
        }

        private static int FrameIndex(string name)
        {
            int index;
            int last = name.LastIndexOf('_');
            return last >= 0 && int.TryParse(name.Substring(last + 1), out index) ? index : int.MaxValue;
        }

        private static void ImportCanonicalSheet(string path, string key, int frameCount)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Sprite importer missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = CanonicalHeroSpriteFactory.PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            var sprites = new SpriteMetaData[frameCount];
            for (int i = 0; i < frameCount; i++)
                sprites[i] = new SpriteMetaData {
                    name = key + "_" + i,
                    rect = new Rect(i * Cell, 0, Cell, Cell),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = CanonicalHeroSpriteFactory.FootPivot
                };
            importer.spritesheet = sprites;
            importer.SaveAndReimport();
        }
    }
}
#endif
