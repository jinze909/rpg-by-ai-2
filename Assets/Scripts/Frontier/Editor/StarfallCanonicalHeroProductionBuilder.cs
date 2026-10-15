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

        /// <summary>
        /// A playable Unity project should not require an extra manual menu
        /// action to discover the already published three-profession sprites.
        /// Only create missing native assets; preserve present controllers,
        /// clips, their GUIDs, all scene content and player save files.
        /// </summary>
        public static void EnsureBuilt()
        {
            bool ready = true;
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
                        int expected = ExpandedCount(FrameRefs(
                            Array.IndexOf(Classes, profession), action, facing).Length, action);
                        // Existing .anim files are NOT necessarily current:
                        // Cast grew from 3 to 7 Sprite cells. A mere existence
                        // check silently left old curves active in gameplay.
                        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            Generated + key + ".png");
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
            if (!ready) Build();
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
            string[] names = { "PlayerVanguard", "PlayerRanger", "PlayerArcanist",
                "WalkVanguard", "WalkRanger", "WalkArcanist",
                "PlayerRear", "PlayerSouth", "PlayerReactionsRear", "PlayerReactionsSouth" };
            var textures = new Dictionary<string, Texture2D>();
            foreach (string name in names)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Resources/Frontier/Art/" + name + ".png");
                if (texture == null || texture.width != 1024 || texture.height != 768 ||
                    !texture.isReadable)
                    throw new InvalidOperationException("Accepted hero atlas missing, unreadable or resized: " + name);
                textures.Add(name, texture);
            }
            var atlasPaths = new Dictionary<string, string>();
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
                            if (installed == null || installed.height != Cell ||
                                installed.width != expectedCount * Cell)
                                throw new InvalidOperationException(
                                    "Published hero atlas has incompatible frames: " + key);
                            atlasPaths.Add(key, installedPath);
                            continue;
                        }
                        // Legacy Editor-only recovery for projects predating
                        // the published atlas handoff. Keep original art intact.
                        var authored = new List<Color[]>(refs.Length);
                        for (int sourceIndex = 0; sourceIndex < refs.Length; sourceIndex++)
                        {
                            FrameSource item = refs[sourceIndex];
                            Sprite baked = CanonicalHeroSpriteFactory.Bake(
                                textures[item.atlas], item.index,
                                textures[item.atlas], item.index,
                                facing, action == 0 || action == 1 ||
                                    item.atlas.StartsWith("Walk") ||
                                    (item.atlas == "Player" + profession && item.index <= 1),
                                action == 6 && sourceIndex == refs.Length - 1,
                                textures["WalkVanguard"]);
                            if (baked == null)
                                throw new InvalidOperationException("Accepted hero pose missing: " + key);
                            try { authored.Add(baked.texture.GetPixels()); }
                            finally
                            {
                                UnityEngine.Object.DestroyImmediate(baked.texture);
                                UnityEngine.Object.DestroyImmediate(baked);
                            }
                        }
                        List<Color[]> sequence = ExpandWithInbetweens(authored, action);
                        var combined = new Texture2D(Cell * sequence.Count, Cell,
                            TextureFormat.RGBA32, false);
                        try
                        {
                            for (int i = 0; i < sequence.Count; i++)
                                combined.SetPixels(i * Cell, 0, Cell, Cell, sequence[i]);
                            combined.Apply(false, false);
                            string path = Generated + key + ".png";
                            byte[] bytes = combined.EncodeToPNG();
                            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
                                File.WriteAllBytes(path, bytes);
                            atlasPaths.Add(key, path);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(combined); }
                    }
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
                        ImportCanonicalSheet(path, key, ExpandedCount(refs.Length, action));
                        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<Sprite>()
                            .OrderBy(s => FrameIndex(s.name))
                            .ToArray();
                        if (frames.Length != ExpandedCount(refs.Length, action) ||
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
                    if (state == null)
                    {
                        state = machine.AddState(name);
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
            Debug.Log("Production hero SpriteRenderer clips baked from ACCEPTED original atlases: "
                + "3 classes x 7 actions x 3 facings, root=(128,240). "
                + "The runtime F8 lane can now load real Sprite curves. "
                + "Additional in-betweens are deterministic pixel-motion approximations, NOT hand-redrawn/artist-approved.");
        }


        // One deterministic pixel midpoint between consecutive *approved*
        // poses. Run also bridges the final pose back to the first; action
        // clips never loop into their windup. Keeps the approved 256px root.
        // The motion pass is an authoring scaffold, not a replacement for
        // manually drawn anticipation/contact/recovery art.
        private static int ExpandedCount(int original, int action)
        {
            return original > 1 && action >= 1 && action <= 5
                ? original * 2 - (action == 1 ? 0 : 1) : original;
        }

        private static List<Color[]> ExpandWithInbetweens(List<Color[]> original, int action)
        {
            var result = new List<Color[]>(ExpandedCount(original.Count, action));
            bool interpolate = original.Count > 1 && action >= 1 && action <= 5;
            for (int i = 0; i < original.Count; i++)
            {
                result.Add(original[i]);
                if (interpolate && (i + 1 < original.Count || action == 1))
                    result.Add(MotionMidpoint(original[i], original[(i + 1) % original.Count]));
            }
            if (result.Count != ExpandedCount(original.Count, action))
                throw new InvalidOperationException("Hero frame count mismatch");
            return result;
        }

        // Source-silhouette interpolation deliberately uses ONE accepted
        // sprite for each generated midpoint. Mixing two distant raster
        // silhouettes produced doubled legs, ghosted weapon outlines and
        // fragmented checkerboard edges in Run/Attack/Cast.
        // Mirror the production Python limb-registration baker when PNGs are
        // absent. Separate left/right gait and hand/weapon trajectories so a
        // single centroid never pulls both limbs or leaves a frozen midpoint.
        private static bool MotionCentroid(Color[] pixels, int x0, int x1,
            int y0, int y1, out Vector2 center)
        {
            float sx = 0f, sy = 0f, mass = 0f;
            for (int y = y0; y < y1; y += 2)
                for (int x = x0; x < x1; x += 2)
                {
                    if (x >= 91 && x < 170 && y >= 57 && y < 118) continue;
                    float alpha = pixels[y * Cell + x].a;
                    if (alpha < .5f) continue;
                    sx += x * alpha;
                    sy += y * alpha;
                    mass += alpha;
                }
            center = mass > 0 ? new Vector2(sx / mass, sy / mass) : Vector2.zero;
            return mass > 0;
        }

        private static Vector2 RegionShift(Color[] first, Color[] next,
            int x0, int x1, int y0, int y1, int limitX, int limitY)
        {
            Vector2 a, b;
            if (!MotionCentroid(first, x0, x1, y0, y1, out a) ||
                !MotionCentroid(next, x0, x1, y0, y1, out b))
                return Vector2.zero;
            return new Vector2(
                Mathf.RoundToInt(Mathf.Clamp((b.x - a.x) * .5f, -limitX, limitX)),
                Mathf.RoundToInt(Mathf.Clamp((b.y - a.y) * .5f, -limitY, limitY)));
        }

        private static Color[] MotionMidpoint(Color[] first, Color[] next)
        {
            if (first.Length != Cell * Cell || next.Length != Cell * Cell)
                throw new InvalidOperationException("Expected full canonical 256px frames");
            Vector2 torso = RegionShift(first, next, 103, 154, 30, 64, 3, 3);
            Vector2 leftLeg = RegionShift(first, next, 89, 128, 23, 58, 7, 5);
            Vector2 rightLeg = RegionShift(first, next, 129, 171, 23, 58, 7, 5);
            Vector2 leftArm = RegionShift(first, next, 34, 121, 47, 145, 14, 10);
            Vector2 rightArm = RegionShift(first, next, 140, 230, 47, 145, 14, 10);
            var output = new Color[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            {
                float legs = Mathf.Clamp01((y - 22f) / 23f) *
                    Mathf.Clamp01((66f - y) / 20f);
                float shoulders = Mathf.Clamp01((y - 42f) / 22f);
                float torsoGate = Mathf.Clamp01((y - 22f) / 28f) *
                    Mathf.Clamp01((73f - y) / 18f);
                for (int x = 0; x < Cell; x++)
                {
                    int index = y * Cell + x;
                    if (x >= 91 && x < 170 && y >= 57 && y < 118)
                    {
                        output[index] = first[index];
                        continue;
                    }
                    float side = Mathf.Clamp01((Mathf.Abs(x - 128f) - 13f) / 29f);
                    float right = Mathf.Clamp01((x - 122f) / 14f);
                    float leftLegW = legs * (1f - right);
                    float rightLegW = legs * right;
                    float leftArmW = shoulders * side * (1f - right);
                    float rightArmW = shoulders * side * right;
                    float bodyW = torsoGate * (1f - side) * (1f - legs);
                    float total = leftLegW + rightLegW + leftArmW + rightArmW;
                    if (total > 1f)
                    {
                        leftLegW /= total;
                        rightLegW /= total;
                        leftArmW /= total;
                        rightArmW /= total;
                    }
                    float dx = torso.x * bodyW + leftLeg.x * leftLegW +
                        rightLeg.x * rightLegW + leftArm.x * leftArmW +
                        rightArm.x * rightArmW;
                    float dy = torso.y * bodyW + leftLeg.y * leftLegW +
                        rightLeg.y * rightLegW + leftArm.y * leftArmW +
                        rightArm.y * rightArmW;
                    output[index] = Pixel(first, x - Mathf.RoundToInt(dx),
                        y - Mathf.RoundToInt(dy));
                }
            }
            return output;
        }

        private static Color Pixel(Color[] pixels, int x, int y)
        {
            return x >= 0 && x < Cell && y >= 0 && y < Cell
                ? pixels[y * Cell + x] : Color.clear;
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
