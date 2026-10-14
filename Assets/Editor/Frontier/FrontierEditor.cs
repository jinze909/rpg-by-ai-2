using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Starfall.Editor
{
    public static class FrontierEditor
    {
        private const string Scene = "Assets/Scenes/Frontier.unity";
        [MenuItem("Starfall/Play Adventure")]
        public static void Play()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Starfall.EditorTools.StarfallCanonicalHeroProductionBuilder.EnsureBuilt();
            EditorSceneManager.OpenScene(Scene); EditorApplication.isPlaying = true;
        }
        [MenuItem("Starfall/Build/Windows")]
        public static void Windows() { Build(BuildTarget.StandaloneWindows64, "StarfallFrontier.exe"); }
        [MenuItem("Starfall/Build/macOS")]
        public static void Mac() { Build(BuildTarget.StandaloneOSX, "StarfallFrontier.app"); }
        [MenuItem("Starfall/Build/Linux")]
        public static void Linux() { Build(BuildTarget.StandaloneLinux64, "StarfallFrontier"); }
        private static void Build(BuildTarget target, string filename)
        {
            string folder = EditorUtility.SaveFolderPanel("Build Starfall Frontier", "", "StarfallFrontier");
            if (string.IsNullOrEmpty(folder)) return;
            Starfall.EditorTools.StarfallCanonicalHeroProductionBuilder.EnsureBuilt();
            var report = BuildPipeline.BuildPlayer(new[] { Scene }, Path.Combine(folder, filename), target, BuildOptions.None);
            Debug.Log("Starfall build: " + report.summary.result);
        }
        // For licensed CI: Unity -batchmode -quit -projectPath . -executeMethod Starfall.Editor.FrontierEditor.BuildWindowsBatch
        public static void BuildWindowsBatch()
        {
            Directory.CreateDirectory("Builds/Windows");
            Starfall.EditorTools.StarfallCanonicalHeroProductionBuilder.EnsureBuilt();
            var report = BuildPipeline.BuildPlayer(new[] { Scene }, "Builds/Windows/StarfallFrontier.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Unity player build failed.");
        }
    }
    public sealed class FrontierImports : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Resources/Frontier/Art/") && assetPath.EndsWith(".png") && !assetPath.EndsWith("TitleVista.png") && !assetPath.EndsWith("/Portraits.png"))
            { var atlas = (TextureImporter)assetImporter; string name = Path.GetFileNameWithoutExtension(assetPath); atlas.textureType = TextureImporterType.Default; atlas.filterMode = FilterMode.Point; atlas.mipmapEnabled = false; atlas.npotScale = TextureImporterNPOTScale.None; atlas.maxTextureSize = 4096; atlas.alphaIsTransparency = true; atlas.textureCompression = TextureImporterCompression.Uncompressed; atlas.isReadable = (name.StartsWith("Player") && name != "PlayerPortraits") || name.StartsWith("Walk") || name.StartsWith("Companion") || name == "Monsters" || name == "Bosses" || name == "EnemyArcherVertical"; return; }
            if (assetPath == "Assets/Resources/Frontier/Art/TitleVista.png" || assetPath == "Assets/Resources/Frontier/Art/Portraits.png")
            { var art = (TextureImporter)assetImporter; art.textureType = TextureImporterType.Default; art.filterMode = FilterMode.Bilinear; art.mipmapEnabled = false; art.npotScale = TextureImporterNPOTScale.None; art.maxTextureSize = 2048; return; }
            if (assetPath != "Assets/Resources/Frontier/ViolaMoves.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
        }
        private void OnPreprocessAsset()
        {
            if (!assetPath.Contains("Frontier/Fonts/") || !(assetImporter is TrueTypeFontImporter)) return;
            var importer = (TrueTypeFontImporter)assetImporter;
            importer.includeFontData = true;
        }
    }
}
