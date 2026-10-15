#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Starfall.EditorTools
{
    // ONLY the accepted, illustrated original game sources listed in the
    // manifest are baked. The earlier low-resolution candidates remain
    // editable and opt-in; no repaint is silently promoted into the game.
    // This importer generates full-body Sprite curves from the SAME source
    // pixels already shown by OnGUI, without recoloring or torso layering.
    public static class StarfallAcceptedActorAnimatorBuilder
    {
        private const string SourceRoot = "Assets/Resources/Frontier/Art/";
        private const string ControllerRoot =
            "Assets/Resources/Frontier/Actors/Native/";
        private const string ClipRoot =
            "Assets/Animations/Frontier/AcceptedActors/";
        private const string ManifestPath =
            "Assets/Resources/Frontier/Actors/Native/AcceptedActorSources.json";

        [Serializable]
        private sealed class SourceManifest
        {
            public int version;
            public SourceSpec[] sources;
        }

        [Serializable]
        private sealed class SourceSpec
        {
            public string name;
            public int columns;
            public int rows;
        }

        [InitializeOnLoadMethod]
        private static void Register()
        {
            // Do not run on arbitrary asset postprocessing callbacks:
            // Unity must finish loading the original textures first.
            EditorApplication.delayCall += TryBakeAfterImport;
            EditorApplication.playModeStateChanged -= BeforePlay;
            EditorApplication.playModeStateChanged += BeforePlay;
        }

        private static void TryBakeAfterImport()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { EnsureBuilt(); }
            catch (Exception error)
            {
                // The accepted OnGUI and direct SpriteRenderer still show
                // complete original art if an Editor import isn't ready.
                // A Player BUILD has a separate hard failure below.
                Debug.LogWarning("Starfall accepted-actor Animator bake pending; " +
                    "original detailed Sprite fallback remains available: " +
                    error.Message);
            }
        }

        private static void BeforePlay(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                TryBakeAfterImport();
        }

        [MenuItem("Starfall/Art/Rebuild Accepted Detailed Actor Animators")]
        public static void RebuildApprovedVisuals()
        {
            EnsureBuilt();
        }

        public static void EnsureBuilt()
        {
            TextAsset manifestAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (manifestAsset == null)
                throw new InvalidOperationException(
                    "Missing accepted original actor atlas manifest");
            SourceManifest manifest =
                JsonUtility.FromJson<SourceManifest>(manifestAsset.text);
            if (manifest == null || manifest.version != 1 ||
                manifest.sources == null || manifest.sources.Length != 13 ||
                manifest.sources.Select(x => x.name).Distinct().Count() != 7)
                throw new InvalidOperationException(
                    "Accepted actor manifest changed without an audited art migration");

            EnsureFolder("Assets/Animations", "Frontier");
            EnsureFolder("Assets/Animations/Frontier", "AcceptedActors");
            foreach (SourceSpec source in manifest.sources)
                EnsureSource(source);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void EnsureSource(SourceSpec spec)
        {
            if (spec == null || String.IsNullOrEmpty(spec.name) ||
                spec.name.Any(c => !Char.IsLetterOrDigit(c)) ||
                spec.columns < 3 || spec.columns > 6 ||
                spec.rows < 1 || spec.rows > 18)
                throw new InvalidOperationException("Invalid accepted actor manifest spec");
            string png = SourceRoot + spec.name + ".png";
            string guid = AssetDatabase.AssetPathToGUID(png);
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            if (source == null || String.IsNullOrEmpty(guid) ||
                source.width % spec.columns != 0 ||
                source.height % spec.rows != 0)
                throw new InvalidOperationException(
                    "Missing or unsliceable high-detail original atlas: " + spec.name);
            int width = source.width / spec.columns;
            int height = source.height / spec.rows;
            TextureImporter importer = AssetImporter.GetAtPath(png) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    "No TextureImporter for accepted " + spec.name);

            if (importer.textureType != TextureImporterType.Sprite)
            {
                // Preserve the ORIGINAL PNG file and meta GUID. Unity creates
                // addressable Sprite sub-assets for Animator object curves,
                // but Resources.Load<Texture2D>(png) must still remain valid.
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = height;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                SpriteMetaData[] cells =
                    new SpriteMetaData[spec.columns * spec.rows];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = new SpriteMetaData {
                        name = spec.name + "_" + i,
                        rect = new Rect((i % spec.columns) * width,
                            source.height - (i / spec.columns + 1) * height,
                            width, height),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = new Vector2(.5f, .0625f)
                    };
                importer.spritesheet = cells;
                importer.SaveAndReimport();
            }
            else if (importer.spriteImportMode != SpriteImportMode.Multiple ||
                     Mathf.Abs(importer.spritePixelsPerUnit - height) > .01f ||
                     importer.filterMode != FilterMode.Point ||
                     importer.mipmapEnabled)
            {
                // An existing hand-authored Sprite layout is never silently
                // overwritten by a generic generated slicer.
                throw new InvalidOperationException(
                    "Unexpected existing importer; review before changing " + spec.name);
            }

            source = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            if (source == null || AssetDatabase.AssetPathToGUID(png) != guid)
                throw new InvalidOperationException(
                    "Original Texture2D/GUID not preserved: " + spec.name);

            Sprite[] imported = AssetDatabase.LoadAllAssetsAtPath(png)
                .OfType<Sprite>().OrderBy(s => CellIndex(s.name)).ToArray();
            int count = spec.columns * spec.rows;
            if (imported.Length != count)
                throw new InvalidOperationException(
                    "Missing accepted full-body Sprite cells: " + spec.name);

            for (int i = 0; i < count; i++)
            {
                Sprite sprite = imported[i];
                float x = (i % spec.columns) * width;
                float y = source.height - (i / spec.columns + 1) * height;
                if (CellIndex(sprite.name) != i || sprite.texture != source ||
                    Mathf.Abs(sprite.rect.x - x) > .05f ||
                    Mathf.Abs(sprite.rect.y - y) > .05f ||
                    Mathf.Abs(sprite.rect.width - width) > .05f ||
                    Mathf.Abs(sprite.rect.height - height) > .05f ||
                    Mathf.Abs(sprite.pixelsPerUnit - height) > .05f ||
                    Vector2.Distance(sprite.pivot,
                        new Vector2(width * .5f, height * .0625f)) > .05f)
                    throw new InvalidOperationException(
                        "Original source cell or root pivot mismatch: " +
                        spec.name + "/" + i);
            }

            string controllerPath =
                ControllerRoot + "Actor_" + spec.name + ".controller";
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(
                    controllerPath);
            if (!controller.parameters.Any(p => p.name == "Pose" &&
                    p.type == AnimatorControllerParameterType.Int))
                controller.AddParameter("Pose", AnimatorControllerParameterType.Int);
            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;
            for (int row = 0; row < spec.rows; row++)
            {
                string clipPath = ClipRoot + "Actor_" + spec.name +
                    "_Pose_" + row + ".anim";
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { frameRate = 12f };
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                // The old detailed Monsters.png has SIX different combat
                // poses, and the north/south Archer THREE cells. GameCore
                // still chooses the one exact source cell with no blending.
                ObjectReferenceKeyframe[] keys =
                    new ObjectReferenceKeyframe[spec.columns + 1];
                for (int column = 0; column < spec.columns; column++)
                    keys[column] = new ObjectReferenceKeyframe {
                        time = column / 12f,
                        value = imported[row * spec.columns + column] };
                keys[spec.columns] = new ObjectReferenceKeyframe {
                    time = spec.columns / 12f,
                    value = imported[(row + 1) * spec.columns - 1] };
                AnimationUtility.SetObjectReferenceCurve(clip,
                    new EditorCurveBinding {path = "",
                        type = typeof(SpriteRenderer), propertyName = "m_Sprite"},
                    keys);
                string stateName = "Pose_" + row;
                AnimatorState state = stateMachine.states.Select(s => s.state)
                    .FirstOrDefault(s => s.name == stateName);
                if (state == null) state = stateMachine.AddState(stateName);
                state.motion = clip;
                if (row == 0) stateMachine.defaultState = state;
            }
            AssetDatabase.SaveAssets();
            // This is the real Editor-side source/curve audit. The separate
            // offline C# harness does not pretend to run it.
            for (int row = 0; row < spec.rows; row++)
            {
                string clipPath = ClipRoot + "Actor_" + spec.name +
                    "_Pose_" + row + ".anim";
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(clip,
                        new EditorCurveBinding {path = "",
                            type = typeof(SpriteRenderer), propertyName = "m_Sprite"});
                if (keys == null || keys.Length != spec.columns + 1)
                    throw new InvalidOperationException(
                        "Missing production Sprite curve: " + clipPath);
                for (int col = 0; col < keys.Length; col++)
                {
                    int spriteIndex = row * spec.columns +
                        Math.Min(col, spec.columns - 1);
                    if (keys[col].value != imported[spriteIndex] ||
                        Mathf.Abs(keys[col].time - col / 12f) > .001f)
                        throw new InvalidOperationException(
                            "Native imported artwork drift: " +
                            spec.name + "/" + row + "/" + col);
                }
                if (!stateMachine.states.Any(s => s.state.name == "Pose_" + row &&
                    s.state.motion == clip))
                    throw new InvalidOperationException(
                        "Native accepted Actor state missing: " + clipPath);
            }
        }

        private static int CellIndex(string name)
        {
            int i, underscore = name.LastIndexOf('_');
            return underscore >= 0 &&
                Int32.TryParse(name.Substring(underscore + 1), out i)
                ? i : Int32.MaxValue;
        }
    }

    // Player builds must package playable real SpriteRenderer+Animator
    // Controllers for accepted monster, Boss and NPC art. Experimental
    // candidate assets are not required and cannot block production.
    internal sealed class StarfallAcceptedActorBuildGate :
        UnityEditor.Build.IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -850; } }

        public void OnPreprocessBuild(
            UnityEditor.Build.Reporting.BuildReport report)
        {
            try { StarfallAcceptedActorAnimatorBuilder.EnsureBuilt(); }
            catch (Exception error)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    "Approved detailed nonhero Animator build rejected: " +
                    error.Message);
            }
        }
    }
}
#endif
