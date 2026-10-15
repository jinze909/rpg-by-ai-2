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
    // Authoritative import of ALREADY SHIPPED complete-body monster, Boss
    // and resident paintings. No generated head, waist graft or interpolation.
    // Unity owns the native SpriteRenderer.m_Sprite curves and Controller.
    // GameCore still selects exact source row/cell at runtime.
    public static class StarfallNativeActorAnimatorBuilder
    {
        private const string Sources = "Assets/Resources/Frontier/Art/";
        private const string Controllers = "Assets/Resources/Frontier/Actors/Native/";
        private const string Clips = "Assets/Animations/Frontier/ActorProduction/";
        private static readonly string[] Families =
            {"MossboundRaider","CinderstoneGuard","BriarBeast","EmberfallResidents"};
        private static readonly int[] Rows = {6,6,18,4};

        // Editable experimental actor atlases are NO LONGER auto-imported
        // into normal Play Mode. Artists may run this menu after a repaint;
        // this rollback does not restrict future genuine 2D reauthoring.
        [MenuItem("Starfall/Art/Preview Experimental Actor Animators")]
        public static void PreviewExperimentalActors()
        {
            EnsureBuilt();
        }

        public static void EnsureBuilt()
        {
            Directory.CreateDirectory(Controllers);
            Directory.CreateDirectory(Clips);
            for (int family = 0; family < Families.Length; family++)
            {
                string name = Families[family];
                string path = Sources + name + ".png";
                Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (atlas == null || atlas.width % 4 != 0 ||
                    atlas.height != Rows[family] * (atlas.width / 4))
                    throw new InvalidOperationException(
                        "Missing or distorted full-body source atlas: " + name);
                int cell = atlas.width / 4;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("No actor SpriteImporter: " + name);
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>().ToArray();
                bool validImport = importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Multiple &&
                    importer.spritePixelsPerUnit == cell &&
                    importer.filterMode == FilterMode.Point &&
                    !importer.mipmapEnabled &&
                    sprites.Length == Rows[family] * 4;
                if (!validImport)
                {
                    // Preserve the source PNG and existing GUID. Add Sprite
                    // sub-assets in its canonical 4-column action-row order.
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Multiple;
                    importer.spritePixelsPerUnit = cell;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;
                    SpriteMetaData[] cells = new SpriteMetaData[Rows[family] * 4];
                    for (int i = 0; i < cells.Length; i++)
                        cells[i] = new SpriteMetaData {
                            name = name + "_" + i,
                            rect = new Rect((i % 4) * cell,
                                atlas.height - (i / 4 + 1) * cell, cell, cell),
                            alignment = (int)SpriteAlignment.Custom,
                            pivot = new Vector2(.5f, 1f - 120f / 128f)
                        };
                    importer.spritesheet = cells;
                    importer.SaveAndReimport();
                }
                // Load by authored source-cell number, never arbitrary asset
                // enumeration order. Validate every anchor after reimport.
                Sprite[] ordered = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .OrderBy(sprite => CellIndex(sprite.name))
                    .ToArray();
                if (ordered.Length != Rows[family] * 4)
                    throw new InvalidOperationException("Native actor Sprite count: " + name);
                for (int i = 0; i < ordered.Length; i++)
                {
                    Sprite sprite = ordered[i];
                    if (CellIndex(sprite.name) != i ||
                        sprite.texture != AssetDatabase.LoadAssetAtPath<Texture2D>(path) ||
                        Mathf.Abs(sprite.rect.x - (i % 4) * cell) > .05f ||
                        Mathf.Abs(sprite.rect.y - (Rows[family] - i / 4 - 1) * cell) > .05f ||
                        Mathf.Abs(sprite.rect.width - cell) > .05f ||
                        Mathf.Abs(sprite.rect.height - cell) > .05f ||
                        Mathf.Abs(sprite.pixelsPerUnit - cell) > .05f ||
                        Vector2.Distance(sprite.pivot,
                            new Vector2(cell * .5f, cell * (1f - 120f / 128f))) > .05f)
                        throw new InvalidOperationException(
                            "Actor animation pivot/identity regression: " + name + ":" + i);
                }
                string controllerPath = Controllers + "Actor_" + name + ".controller";
                AnimatorController controller =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null)
                    controller = AnimatorController.CreateAnimatorControllerAtPath(
                        controllerPath);
                if (!controller.parameters.Any(p => p.name == "Pose" &&
                        p.type == AnimatorControllerParameterType.Int))
                    controller.AddParameter("Pose", AnimatorControllerParameterType.Int);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                for (int row = 0; row < Rows[family]; row++)
                {
                    string clipPath = Clips + "Actor_" + name + "_Pose_" + row + ".anim";
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        clip = new AnimationClip { frameRate = 12f };
                        AssetDatabase.CreateAsset(clip,clipPath);
                    }
                    // Every row uses four independent accepted complete-body
                    // source cells; no Animator-owned gameplay transitions.
                    ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[5];
                    for (int i = 0; i < 4; i++)
                        frames[i] = new ObjectReferenceKeyframe {
                            time = i / 12f, value = ordered[row * 4 + i] };
                    frames[4] = new ObjectReferenceKeyframe {
                        time = 4 / 12f, value = ordered[row * 4 + 3] };
                    AnimationUtility.SetObjectReferenceCurve(clip,
                        new EditorCurveBinding {path="",type=typeof(SpriteRenderer),
                            propertyName="m_Sprite"}, frames);
                    string stateName = "Pose_" + row;
                    AnimatorState state = machine.states.Select(x=>x.state)
                        .FirstOrDefault(x=>x.name==stateName);
                    if (state == null) state = machine.AddState(stateName);
                    state.motion = clip;
                    if (row == 0) machine.defaultState = state;
                }
                AssetDatabase.SaveAssets();
                // Independently re-read the authored Controller and curves:
                // file existence alone must not establish native readiness.
                for (int row = 0; row < Rows[family]; row++)
                {
                    string clipPath = Clips + "Actor_" + name + "_Pose_" + row + ".anim";
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(
                        clip,new EditorCurveBinding {path="",type=typeof(SpriteRenderer),
                            propertyName="m_Sprite"});
                    if (curve == null || curve.Length != 5)
                        throw new InvalidOperationException("Missing native Actor curve: " + clipPath);
                    for (int k = 0; k < 5; k++)
                        if (curve[k].value != ordered[row * 4 + Math.Min(k,3)] ||
                            Mathf.Abs(curve[k].time - k / 12f) > .001f)
                            throw new InvalidOperationException(
                                "Native Actor frame mismatch: " + clipPath + ":" + k);
                    if (!machine.states.Any(s => s.state.name == "Pose_" + row &&
                        s.state.motion == clip))
                        throw new InvalidOperationException("Missing native actor state: " + clipPath);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static int CellIndex(string name)
        {
            int idx, underscore = name.LastIndexOf('_');
            return underscore >= 0 &&
                int.TryParse(name.Substring(underscore + 1), out idx)
                ? idx : int.MaxValue;
        }
    }

    // When repainted art passes real visual review, production Animator
    // baking can be promoted again. This old prototype is optional in
    // Player builds; no rejected atlas can block normal game packaging.
}
#endif
