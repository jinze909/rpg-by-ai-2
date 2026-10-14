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
            return original > 1 && action >= 1 && action <= 3
                ? original * 2 - (action == 1 ? 0 : 1) : original;
        }

        private static List<Color[]> ExpandWithInbetweens(List<Color[]> original, int action)
        {
            var result = new List<Color[]>(ExpandedCount(original.Count, action));
            bool interpolate = original.Count > 1 && action >= 1 && action <= 3;
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

        private static readonly int[] Ordered4 = {
            0, 8, 2, 10, 12, 4, 14, 6,
            3, 11, 1, 9, 15, 7, 13, 5
        };

        private static Color[] MotionMidpoint(Color[] first, Color[] next)
        {
            if (first.Length != Cell * Cell || next.Length != Cell * Cell)
                throw new InvalidOperationException("Expected full canonical 256px frames");
            float ax = 0, ay = 0, aw = 0, bx = 0, by = 0, bw = 0;
            // Exclude extended weapons when estimating the BODY movement.
            // Boot contact stays at the immutable canonical foot anchor.
            for (int y = 28; y < 118; y += 2)
                for (int x = 86; x < 172; x += 2)
                {
                    int i = y * Cell + x;
                    float a = first[i].a, b = next[i].a;
                    ax += a * x; ay += a * y; aw += a;
                    bx += b * x; by += b * y; bw += b;
                }
            int dx = aw > 1 && bw > 1
                ? Mathf.Clamp(Mathf.RoundToInt(((bx / bw) - (ax / aw)) * .5f), -6, 6) : 0;
            int dy = aw > 1 && bw > 1
                ? Mathf.Clamp(Mathf.RoundToInt(((by / bw) - (ay / aw)) * .5f), -5, 5) : 0;
            var output = new Color[Cell * Cell];
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    float anchorWeight = Mathf.Clamp01((y - 18f) / 64f);
                    int ox = Mathf.RoundToInt(dx * anchorWeight);
                    int oy = Mathf.RoundToInt(dy * anchorWeight);
                    Color a = Pixel(first, x - ox, y - oy);
                    Color b = Pixel(next, x + ox, y + oy);
                    int i = y * Cell + x;
                    if (a.a > .5f && b.a > .5f)
                    {
                        // Never superimpose faces. Same blonde identity in
                        // both poses; the canonical hair/face remains crisp.
                        bool head = x >= 91 && x < 170 && y >= 57 && y < 118;
                        output[i] = head ? a : Color.Lerp(a, b, .5f);
                    }
                    else
                    {
                        // Ordered alpha quantization keeps point-filtered
                        // silhouettes opaque rather than ghostly double limbs.
                        bool second = Ordered4[(y & 3) * 4 + (x & 3)] < 8;
                        output[i] = second ? b : a;
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
                return new[] {
                    new FrameSource(facing == 1 ? "PlayerRear" : facing == 2 ? "PlayerSouth" : p,
                        facing == 0 ? 6 : cls * 4 + 2),
                    new FrameSource(facing == 1 ? "PlayerRear" : facing == 2 ? "PlayerSouth" : p,
                        facing == 0 ? 7 : cls * 4 + 3)
                };
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
