// Compile-only declarations, never imported into Unity and never used as tests.
using System;
namespace UnityEngine { public static class GUILayout { public static bool Button(string text){return false;} } }
namespace NUnit.Framework
{
    public sealed class TestAttribute:Attribute {}
    public static class Assert
    { public static void AreEqual(object a,object b,string message=null){} public static void AreSame(object a,object b,string message=null){} public static void IsTrue(bool a,string message=null){} public static void IsFalse(bool a,string message=null){} public static void NotNull(object a,string message=null){} public static void Greater(object a,object b,string message=null){} public static void Less(object a,object b,string message=null){} }
}
namespace UnityEngine.TestTools
{ public sealed class UnityTestAttribute:Attribute {} public sealed class UnitySetUpAttribute:Attribute {} public sealed class UnityTearDownAttribute:Attribute {} }
namespace UnityEditor
{
    public sealed class MenuItem:Attribute {public MenuItem(string path){} }
    public class EditorWindow {public static T GetWindow<T>(string title) where T:new(){return new T();} public void Show(){} public void Repaint(){} }
    public static class EditorApplication {public static bool isPlaying;public static event Action update;public static bool ExecuteMenuItem(string path){return true;} }
    public enum MessageType {None,Info,Warning}
    public static class EditorGUILayout {public static void HelpBox(string text,MessageType type){} public static int Popup(string label,int index,string[] values){return index;} public static bool Toggle(string label,bool value){return value;} }
    public static class EditorUtility {public static string SaveFolderPanel(string a,string b,string c){return "";} }
    public enum BuildTarget {StandaloneWindows64,StandaloneOSX,StandaloneLinux64} public enum BuildOptions {None}
    public static class BuildPipeline {public static Build.Reporting.BuildReport BuildPlayer(string[] scenes,string path,BuildTarget target,BuildOptions options){return new Build.Reporting.BuildReport();} }
    public class AssetImporter {}
    public class AssetPostprocessor {public string assetPath="";public AssetImporter assetImporter;}
    public enum TextureImporterType {Default,Sprite} public enum TextureImporterNPOTScale {None} public enum TextureImporterCompression {Uncompressed}
    public class TextureImporter:AssetImporter {public TextureImporterType textureType;public UnityEngine.FilterMode filterMode;public bool mipmapEnabled,alphaIsTransparency,isReadable;public TextureImporterNPOTScale npotScale;public int maxTextureSize;public TextureImporterCompression textureCompression;}
    public class TrueTypeFontImporter:AssetImporter {public bool includeFontData;}
}
namespace UnityEditor.SceneManagement {public static class EditorSceneManager {public static bool SaveCurrentModifiedScenesIfUserWantsTo(){return true;}public static void OpenScene(string name){} } }
namespace UnityEditor.Build.Reporting {public enum BuildResult {Succeeded,Failed}public class BuildSummary {public BuildResult result;}public class BuildReport {public BuildSummary summary=new BuildSummary();} }
