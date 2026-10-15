// Minimal compile-time-only declarations for the ACTUAL native hero renderer.
// These are NOT Unity, have no graphics and must never be included in Assets.
// Compiles FrontierHeroSpriteCapture.cs under UNITY_5_3_OR_NEWER to catch
// C# / API call regressions that standard mono gameplay checks silently skip.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    public class Object
    {
        public static void Destroy(Object value) { }
        public int GetInstanceID() { return 1; }
    }
    public class Component : Object
    {
        public Transform transform = new Transform();
    }
    public class MonoBehaviour : Component { }
    public class GameObject : Object
    {
        public string name;
        public HideFlags hideFlags;
        public int layer;
        public Transform transform = new Transform();
        public GameObject(string name) { this.name = name; }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
    }
    public class Transform
    {
        public Vector3 position, localPosition, localScale;
        public void SetParent(Transform target, bool preserveWorldPosition) { }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static float Distance(Vector2 a, Vector2 b)
        {
            float dx=a.x-b.x,dy=a.y-b.y;
            return (float)Math.Sqrt(dx*dx+dy*dy);
        }
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z)
        {this.x=x;this.y=y;this.z=z;}
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1f)
        {this.r=r;this.g=g;this.b=b;this.a=a;}
    }
    public struct Rect
    {
        public float x,y,width,height;
        public Rect(float x,float y,float width,float height)
        {this.x=x;this.y=y;this.width=width;this.height=height;}
    }
    public enum HideFlags { HideAndDontSave }
    public enum FilterMode { Point }
    public enum TextureWrapMode { Clamp }
    public enum TextureFormat { RGBA32 }
    public enum SpriteMeshType { FullRect }
    public enum RenderTextureFormat { ARGB32 }
    public enum CameraClearFlags { SolidColor }
    public class Texture : Object { }
    public class Texture2D : Texture
    {
        public int width,height;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Texture2D(int width,int height,TextureFormat format,bool mipChain)
        {this.width=width;this.height=height;}
        public Color[] GetPixels(int x,int y,int width,int height)
        {return new Color[width*height];}
        public void SetPixels(Color[] pixels) { }
        public void Apply(bool updateMipmaps,bool makeNoLongerReadable) { }
    }
    public sealed class Sprite : Object
    {
        public Texture2D texture;
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit;
        public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot,
            float pixelsPerUnit,uint extrude,SpriteMeshType meshType)
        {
            return new Sprite { texture=texture,rect=rect,pivot=pivot,
                pixelsPerUnit=pixelsPerUnit };
        }
    }
    public class RenderTexture : Texture
    {
        public FilterMode filterMode;
        public bool useMipMap;
        public bool created;
        public int releaseCount;
        public RenderTexture(int width,int height,int depth,RenderTextureFormat format) { }
        public bool IsCreated() { return created; }
        public void Create() { created=true; }
        public void Release() { created=false; releaseCount++; }
    }
    public class Behaviour : Component { public bool enabled; }
    public class SpriteRenderer : Behaviour
    {
        public Sprite sprite;
        public bool flipX;
        public int sortingOrder;
    }
    public class RuntimeAnimatorController : Object { }
    public class Animator : Behaviour
    {
        public RuntimeAnimatorController runtimeAnimatorController;
        public float speed;
        public static int StringToHash(string state) { return 0; }
        public void Play(int state,int layer,float normalizedTime) { }
        public void SetInteger(string parameter,int value) { }
        public void Update(float deltaTime) { }
    }
    public class Camera : Behaviour
    {
        public bool orthographic,allowHDR,allowMSAA;
        public float orthographicSize,aspect;
        public int cullingMask;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public RenderTexture targetTexture;
        public static readonly List<RenderTexture> RenderedTargets =
            new List<RenderTexture>();
        public void Render() { RenderedTargets.Add(targetTexture); }
    }
    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
        public static T[] LoadAll<T>(string path) where T : Object
        {return new T[0];}
    }
    public enum EventType { Repaint }
    public class Event
    {
        public EventType type=EventType.Repaint;
        public static Event current=new Event();
    }
    public static class GUI
    {
        public static Color color;
        public static readonly List<Texture> DrawnTextures =
            new List<Texture>();
        public static void DrawTexture(Rect target,Texture image)
        { DrawnTextures.Add(image); }
    }
    public static class Time { public static int frameCount; }
    public static class Mathf
    {
        public static float Abs(float value) { return Math.Abs(value); }
        public static int RoundToInt(float value)
        {return (int)Math.Round(value);}
    }
    public static class Debug
    {
        public static void LogWarning(object warning) { }
    }
}
namespace UnityEngine.Rendering
{
    public class SortingGroup : UnityEngine.Component
    {
        public int sortingOrder;
    }
}
namespace Starfall
{
    public struct Point
    {
        public float x,y;
        public Point(float x,float y){this.x=x;this.y=y;}
    }
    public class GameCore
    {
        public int AppearanceWeapon;
        public string mode="world";
    }
    // Real FrontierRuntime is a multi-file partial; provide only members
    // defined elsewhere, never fake the new native capture method.
    public sealed partial class FrontierRuntime : UnityEngine.MonoBehaviour
    {
        public GameCore Game = new GameCore();
        private bool drawingOutline;
        private float worldActorOpacity=1f;
        private readonly HashSet<string> missingHeroBodyWarnings =
            new HashSet<string>();
        private bool ResolveFullBodyHeroFrame(bool movement,int facing,
            out int action,out int frames,out int frame,out float position)
        {
            action=movement?1:0;frames=4;frame=0;position=0;
            return true;
        }
    }
}
