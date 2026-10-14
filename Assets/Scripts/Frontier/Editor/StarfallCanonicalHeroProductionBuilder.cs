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
                        var combined = new Texture2D(Cell * refs.Length, Cell,
                            TextureFormat.RGBA32, false);
                        try
                        {
                            for (int i = 0; i < refs.Length; i++)
                            {
                                var item = refs[i];
                                Sprite baked = CanonicalHeroSpriteFactory.Bake(
                                    textures[item.atlas], item.index,
                                    textures[item.atlas], item.index,
                                    facing, action == 0 || action == 1,
                                    action == 6, textures["WalkVanguard"]);
                                if (baked == null)
                                    throw new InvalidOperationException("Hero pixel registry failed: " + key + " frame " + i);
                                try { combined.SetPixels(i * Cell, 0, Cell, Cell, baked.texture.GetPixels()); }
                                finally
                                {
                                    UnityEngine.Object.DestroyImmediate(baked.texture);
                                    UnityEngine.Object.DestroyImmediate(baked);
                                }
                            }
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
                        ImportCanonicalSheet(path, key, refs.Length);
                        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<Sprite>()
                            .OrderBy(s => FrameIndex(s.name))
                            .ToArray();
                        if (frames.Length != refs.Length ||
                            frames.Any(s => s.rect.width != Cell || s.rect.height != Cell ||
                                Vector2.Distance(s.pivot, new Vector2(128, 16)) > .05f))
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
                + "These are inherited source poses; unique missing in-betweens are NOT claimed complete.");
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
                    return new[] { new FrameSource("PlayerRear", cls * 4 + 2),
                        new FrameSource("PlayerRear", cls * 4 + 3) };
                if (facing == 2)
                    return Enumerable.Range(cls * 4, 4)
                        .Select(i => new FrameSource("PlayerSouth", i)).ToArray();
                return Enumerable.Range(4, 4).Select(i => new FrameSource(p, i)).ToArray();
            }
            if (action == 3)
                return new[] {
                    new FrameSource(facing == 1 ? "PlayerRear" : facing == 2 ? "PlayerSouth" : p,
                        facing == 0 ? 6 : cls * 4 + 2),
                    new FrameSource(facing == 1 ? "PlayerRear" : facing == 2 ? "PlayerSouth" : p,
                        facing == 0 ? 7 : cls * 4 + 3)
                };
            int pose = action == 4 ? 0 : action == 5 ? 2 : 3;
            return new[] { new FrameSource(
                facing == 1 ? "PlayerReactionsRear" :
                facing == 2 ? "PlayerReactionsSouth" : p,
                facing == 0 ? action == 4 ? 8 : action == 5 ? 10 : 11 : cls * 4 + pose) };
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
