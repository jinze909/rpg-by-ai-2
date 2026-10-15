// COMPILE-ONLY Unity 2022.3 surface for the production accepted-actor
// Sprite importer. NEVER ship this under Assets; Editor native QA still needed.
using System;
using UnityEngine;
namespace UnityEngine
{
    public class Object { public string name; }
    public class Texture2D : Object { public int width, height; }
    public class TextAsset : Object { public string text; }
    public class Sprite : Object
    {
        public Texture2D texture;
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit;
    }
    public class SpriteRenderer : Object { }
    public class AnimationClip : Object { public float frameRate; }
    public class AnimationCurve { }
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y){this.x=x;this.y=y;}
        public static float Distance(Vector2 a,Vector2 b){return 0f;}
    }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float w,float h)
        {this.x=x;this.y=y;this.width=w;this.height=h;}
    }
    public enum FilterMode { Point }
    public enum SpriteAlignment { Custom }
    public static class Mathf { public static float Abs(float v){return Math.Abs(v);} }
    public static class Debug { public static void LogWarning(object o){} }
    public static class JsonUtility { public static T FromJson<T>(string json){return default(T);} }
    public static class Resources
    {
        public static T Load<T>(string path) where T:UnityEngine.Object{return null;}
    }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InitializeOnLoadMethodAttribute:Attribute{}
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem:Attribute{public MenuItem(string title){}}
    public enum PlayModeStateChange{ExitingEditMode}
    public static class EditorApplication
    {
        public static bool isPlayingOrWillChangePlaymode;
        public static Action delayCall;
        public static event Action<PlayModeStateChange> playModeStateChanged;
    }
    public class AssetImporter
    {
        public static AssetImporter GetAtPath(string path){return null;}
    }
    public enum TextureImporterType { Default, Sprite }
    public enum SpriteImportMode { Single, Multiple }
    public enum TextureImporterCompression { Uncompressed }
    public sealed class SpriteMetaData
    {
        public string name;
        public Rect rect;
        public int alignment;
        public Vector2 pivot;
    }
    public sealed class TextureImporter:AssetImporter
    {
        public TextureImporterType textureType;
        public SpriteImportMode spriteImportMode;
        public float spritePixelsPerUnit;
        public FilterMode filterMode;
        public bool mipmapEnabled,alphaIsTransparency;
        public TextureImporterCompression textureCompression;
        public SpriteMetaData[] spritesheet;
        public void SaveAndReimport(){}
    }
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T:UnityEngine.Object
        {return null;}
        public static string AssetPathToGUID(string path){return "";}
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path)
        {return new UnityEngine.Object[0];}
        public static bool IsValidFolder(string path){return false;}
        public static string CreateFolder(string parent,string folder){return "";}
        public static void SaveAssets(){}
        public static void CreateAsset(UnityEngine.Object obj,string path){}
    }
    public sealed class ObjectReferenceKeyframe
    {
        public float time;
        public UnityEngine.Object value;
    }
    public struct EditorCurveBinding
    {
        public string path,propertyName;
        public Type type;
    }
    public static class AnimationUtility
    {
        public static EditorCurveBinding[] GetCurveBindings(AnimationClip clip)
        { return new EditorCurveBinding[0]; }
        public static EditorCurveBinding[] GetObjectReferenceCurveBindings(
            AnimationClip clip)
        { return new EditorCurveBinding[0]; }
        public static void SetEditorCurve(
            AnimationClip clip,EditorCurveBinding binding,AnimationCurve curve){}
        public static ObjectReferenceKeyframe[] GetObjectReferenceCurve(
            AnimationClip clip,EditorCurveBinding binding){return null;}
        public static void SetObjectReferenceCurve(
            AnimationClip clip,EditorCurveBinding binding,
            ObjectReferenceKeyframe[] keys){}
    }
}
namespace UnityEditor.Animations
{
    using UnityEngine;
    public enum AnimatorControllerParameterType { Int }
    public class AnimatorControllerParameter
    {
        public string name;
        public AnimatorControllerParameterType type;
    }
    public class AnimatorState
    {
        public string name;
        public AnimationClip motion;
    }
    public struct ChildAnimatorState { public AnimatorState state; }
    public class AnimatorStateMachine
    {
        public ChildAnimatorState[] states=new ChildAnimatorState[0];
        public AnimatorState defaultState;
        public AnimatorState AddState(string name){return new AnimatorState();}
    }
    public class AnimatorControllerLayer
    {
        public AnimatorStateMachine stateMachine=new AnimatorStateMachine();
    }
    public class AnimatorController:UnityEngine.Object
    {
        public AnimatorControllerParameter[] parameters=new AnimatorControllerParameter[0];
        public AnimatorControllerLayer[] layers=new[]{new AnimatorControllerLayer()};
        public static AnimatorController CreateAnimatorControllerAtPath(string path)
        {return new AnimatorController();}
        public void AddParameter(string name,AnimatorControllerParameterType type){}
    }
}
namespace UnityEditor.Build
{
    public interface IPreprocessBuildWithReport
    {
        int callbackOrder{get;}
        void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report);
    }
    public class BuildFailedException:Exception
    {
        public BuildFailedException(string description):base(description){}
    }
}
namespace UnityEditor.Build.Reporting
{
    public class BuildReport{}
}
