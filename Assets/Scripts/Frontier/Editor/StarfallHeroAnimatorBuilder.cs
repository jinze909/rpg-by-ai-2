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
    // Opt-in animation laboratory; never alters the accepted IMGUI sheets,
    // scene, gameplay state machine or SaveData v2.
    public static class StarfallHeroAnimatorBuilder
    {
        private const string Original = "Assets/Resources/Frontier/Art/";
        private const string Source = "Assets/Resources/Frontier/Art/HeroAnimator/Sources/";
        private const string Animation = "Assets/Animations/Frontier/HeroPreview/";
        private const string Prefabs = "Assets/Prefabs/Frontier/HeroPreview/";
        private const int Cell = 256;
        private const int Columns = 4;
        private const int Rows = 3;
        private const float PixelsPerUnit = 128f;
        // 120/128 screen-coordinate body anchor => 240/256 from top.
        // A Unity Sprite pivot is measured from the BOTTOM, so y=16/256.
        private static readonly Vector2 FootPivot = new Vector2(.5f, 1f / 16f);
        private static readonly string[] Classes = { "Vanguard", "Ranger", "Arcanist" };
        private static readonly string[] Actions = { "Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death" };
        private static readonly string[] Facings = { "E", "N", "S" };

        [MenuItem("Starfall/Animation/Build Three-Class Hero Preview")]
        public static void Build()
        {
            EnsureDirectory(Source);
            EnsureDirectory(Animation);
            EnsureDirectory(Prefabs);
            string[] shared = { "PlayerRear", "PlayerSouth", "PlayerReactionsRear", "PlayerReactionsSouth" };
            foreach (string stem in shared) CloneSheet(stem);
            foreach (string profession in Classes)
            {
                CloneSheet("Player" + profession);
                CloneSheet("Walk" + profession);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string stem in shared) ImportSheet(stem);
            foreach (string profession in Classes)
            {
                ImportSheet("Player" + profession);
                ImportSheet("Walk" + profession);
            }

            var sprites = new Dictionary<string, Sprite[]>();
            foreach (string stem in shared.Concat(Classes.SelectMany(c => new[] { "Player" + c, "Walk" + c })))
            {
                string file = Source + stem + ".png";
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(file)
                    .OfType<Sprite>()
                    .OrderBy(s => FrameNumber(s.name)).ToArray();
                if (frames.Length != Columns * Rows || frames.Any(s => s.rect.width != Cell || s.rect.height != Cell))
                    throw new InvalidOperationException("Animation import has unexpected atlas cells: " + file);
                if (frames.Any(s => Vector2.Distance(s.pivot, new Vector2(Cell * FootPivot.x, Cell * FootPivot.y)) > .02f))
                    throw new InvalidOperationException("Foot pivot mismatch: " + file);
                sprites.Add(stem, frames);
            }

            var clips = new Dictionary<string, AnimationClip>();
            for (int c = 0; c < Classes.Length; ++c)
                for (int a = 0; a < Actions.Length; ++a)
                    for (int f = 0; f < Facings.Length; ++f)
                    {
                        string profession = Classes[c];
                        string key = ClipKey(profession, a, f);
                        List<Sprite> sequence = Frames(sprites, c, a, f);
                        if (sequence.Count == 0 || sequence.Any(s => s == null))
                            throw new InvalidOperationException("Missing real source cells for " + key);
                        var clip = new AnimationClip { frameRate = 12 };
                        var curve = new ObjectReferenceKeyframe[sequence.Count];
                        for (int i = 0; i < curve.Length; i++)
                            curve[i] = new ObjectReferenceKeyframe { time = i / 12f, value = sequence[i] };
                        AnimationUtility.SetObjectReferenceCurve(clip,
                            new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" },
                            curve);
                        // A deliberately held pose changes its DURATION, not its
                        // number of physical bitmap files. New in-betweens require real art.
                        if (a == 0 || a == 1)
                        {
                            SerializedObject settings = new SerializedObject(clip);
                            SerializedProperty loop = settings.FindProperty("m_AnimationClipSettings.m_LoopTime");
                            if (loop != null) { loop.boolValue = true; settings.ApplyModifiedProperties(); }
                        }
                        string path = Animation + key + ".anim";
                        AssetDatabase.DeleteAsset(path); // generated-preview assets only
                        AssetDatabase.CreateAsset(clip, path);
                        clips.Add(key, clip);
                    }
            var controllerPath = Animation + "HeroPreviewBase.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Action", AnimatorControllerParameterType.Int);
            controller.AddParameter("Facing", AnimatorControllerParameterType.Int);
            AnimatorStateMachine graph = controller.layers[0].stateMachine;
            for (int a = 0; a < Actions.Length; ++a)
                for (int f = 0; f < Facings.Length; ++f)
                {
                    string key = ClipKey(Classes[0], a, f);
                    AnimatorState state = graph.AddState(Actions[a] + "_" + Facings[f]);
                    state.motion = clips[key];
                    if (a == 0 && f == 0) graph.defaultState = state;
                    AnimatorStateTransition transition = graph.AddAnyStateTransition(state);
                    transition.hasExitTime = false;
                    transition.duration = 0;
                    transition.canTransitionToSelf = false;
                    transition.AddCondition(AnimatorConditionMode.Equals, a, "Action");
                    transition.AddCondition(AnimatorConditionMode.Equals, f, "Facing");
                }
            AssetDatabase.SaveAssets();
            for (int c = 0; c < Classes.Length; ++c)
            {
                RuntimeAnimatorController runtimeController = controller;
                if (c != 0)
                {
                    var overrides = new AnimatorOverrideController(controller);
                    var replacements = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    for (int a = 0; a < Actions.Length; ++a)
                        for (int f = 0; f < Facings.Length; ++f)
                            replacements.Add(new KeyValuePair<AnimationClip, AnimationClip>(
                                clips[ClipKey(Classes[0], a, f)],
                                clips[ClipKey(Classes[c], a, f)]));
                    overrides.ApplyOverrides(replacements);
                    string overridePath = Animation + "HeroPreview" + Classes[c] + ".overrideController";
                    AssetDatabase.DeleteAsset(overridePath); // rebuild only generated-preview assets
                    AssetDatabase.CreateAsset(overrides, overridePath);
                    runtimeController = overrides;
                }
                var go = new GameObject("CanonicalBlondHero_" + Classes[c]);
                try
                {
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = Frames(sprites, c, 0, 0)[0];
                    renderer.sortingOrder = 100;
                    var animator = go.AddComponent<Animator>();
                    animator.runtimeAnimatorController = runtimeController;
                    go.AddComponent<StarfallHeroAnimatorPreview>();
                    PrefabUtility.SaveAsPrefabAsset(go, Prefabs + Classes[c] + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Starfall: three-class, seven-action, three-facing Animator preview prefabs built. "
                + "They use ACCEPTED existing cells as a bridge, NOT newly redrawn or fully QA-approved animations. "
                + "Production remains on the safe IMGUI renderer.");
        }

        // Generates a real runtime-loadable Animator state machine for the live
        // SpriteRenderer bridge in FrontierHeroSpriteCapture. It never writes
        // over accepted source PNGs or the separate preview controller.
        // The GameCore clock still chooses exact contact/leg frames: these
        // Animator states deliberately do not keyframe m_Sprite yet.
        [MenuItem("Starfall/Animation/Build Live SpriteRenderer State Bridge")]
        public static void BuildLiveStateBridge()
        {
            const string controllerPath = "Assets/Resources/Frontier/HeroAnimator/ProductionController.controller";
            const string clipsDirectory = "Assets/Animations/Frontier/HeroLiveStates/";
            EnsureDirectory("Assets/Resources/Frontier/HeroAnimator");
            EnsureDirectory(clipsDirectory);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller != null)
            {
                Debug.Log("Existing live Hero Animator Controller preserved; no authored transition or clip overwritten.");
                return;
            }
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Action", AnimatorControllerParameterType.Int);
            controller.AddParameter("Facing", AnimatorControllerParameterType.Int);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            string[] actions = { "Idle", "Run", "Attack", "Cast", "Dodge", "Hit", "Death" };
            string[] directions = { "E", "N", "S" };
            for (int action = 0; action < actions.Length; action++)
                for (int facing = 0; facing < directions.Length; facing++)
                {
                    string name = actions[action] + "_" + directions[facing];
                    string clipPath = clipsDirectory + "Live_" + name + ".anim";
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        clip = new AnimationClip { frameRate = 12 };
                        // Harmless, nonvisual constant track gives Animator an
                        // actual duration without overwriting the production
                        // SpriteRenderer's GameCore-registered art.
                        AnimationUtility.SetEditorCurve(clip,
                            new EditorCurveBinding { path = "", type = typeof(Transform),
                                propertyName = "m_LocalScale.z" },
                            AnimationCurve.Constant(0, .5f, 1));
                        AssetDatabase.CreateAsset(clip, clipPath);
                    }
                    AnimatorState state = stateMachine.AddState(name);
                    state.motion = clip;
                    if (action == 0 && facing == 0) stateMachine.defaultState = state;
                    AnimatorStateTransition change = stateMachine.AddAnyStateTransition(state);
                    change.hasExitTime = false;
                    change.duration = 0;
                    change.canTransitionToSelf = false;
                    change.AddCondition(AnimatorConditionMode.Equals, action, "Action");
                    change.AddCondition(AnimatorConditionMode.Equals, facing, "Facing");
                }
            AssetDatabase.SaveAssets();
            Debug.Log("Live seven-action, three-facing Animator bridge created. " +
                "Inspector opt-in on FrontierRuntime enables production SpriteRenderer compositing; " +
                "actual accepted source pixels and SaveData v2 remain untouched.");
        }

        private static string ClipKey(string profession, int action, int facing)
        {
            return "Hero_" + profession + "_" + Actions[action] + "_" + Facings[facing];
        }

        private static List<Sprite> Frames(Dictionary<string, Sprite[]> sheets, int classIndex, int action, int facing)
        {
            string profession = Classes[classIndex];
            string player = "Player" + profession, walk = "Walk" + profession;
            if (action == 0)
                return new List<Sprite> { facing == 2 ? sheets[walk][8] : sheets[player][facing == 1 ? 1 : 0] };
            if (action == 1)
                return Enumerable.Range(facing * 4, 4).Select(i => sheets[walk][i]).ToList();
            if (action == 2)
            {
                if (facing == 1)
                    return new List<Sprite> { sheets["PlayerRear"][classIndex * 4 + 2], sheets["PlayerRear"][classIndex * 4 + 3] };
                if (facing == 2)
                    return Enumerable.Range(classIndex * 4, 4).Select(i => sheets["PlayerSouth"][i]).ToList();
                return Enumerable.Range(4, 4).Select(i => sheets[player][i]).ToList();
            }
            // Existing legacy poses only: no fictional cast/dodge/death
            // in-betweens, and no imported user-rejected replacement art.
            if (action == 3)
            {
                if (facing == 1) return new List<Sprite> {
                    sheets["PlayerRear"][classIndex * 4 + 2], sheets["PlayerRear"][classIndex * 4 + 3] };
                if (facing == 2) return new List<Sprite> {
                    sheets["PlayerSouth"][classIndex * 4 + 2], sheets["PlayerSouth"][classIndex * 4 + 3] };
                return new List<Sprite> { sheets[player][6], sheets[player][7] };
            }
            int reaction = action == 4 ? 0 : action == 5 ? 2 : 3;
            if (facing == 1) return new List<Sprite> { sheets["PlayerReactionsRear"][classIndex * 4 + reaction] };
            if (facing == 2) return new List<Sprite> { sheets["PlayerReactionsSouth"][classIndex * 4 + reaction] };
            return new List<Sprite> { sheets[player][action == 4 ? 8 : action == 5 ? 10 : 11] };
        }

        private static void CloneSheet(string stem)
        {
            string src = Original + stem + ".png", dst = Source + stem + ".png";
            if (!File.Exists(src)) throw new FileNotFoundException("Required accepted atlas not found", src);
            if (!File.Exists(dst) || !File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst)))
                File.Copy(src, dst, true);
        }

        private static void ImportSheet(string stem)
        {
            string file = Source + stem + ".png";
            var importer = AssetImporter.GetAtPath(file) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Could not import " + file);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            var cells = new SpriteMetaData[Columns * Rows];
            for (int i = 0; i < cells.Length; ++i)
                cells[i] = new SpriteMetaData
                {
                    name = stem + "_" + i,
                    rect = new Rect((i % Columns) * Cell, (Rows - 1 - i / Columns) * Cell, Cell, Cell),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = FootPivot
                };
            importer.spritesheet = cells;
            importer.SaveAndReimport();
        }

        private static int FrameNumber(string name)
        {
            int n;
            int delimiter = name.LastIndexOf('_');
            return delimiter >= 0 && int.TryParse(name.Substring(delimiter + 1), out n) ? n : int.MaxValue;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
#endif
