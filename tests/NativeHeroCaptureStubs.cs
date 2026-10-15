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
        public string name;
        public static void Destroy(Object value) { }
        // Unity gives every live atlas its own instance identity. Returning
        // a constant hid real cache collisions in multi-atlas probes.
        private static int nextInstanceId;
        private readonly int instanceId =
            System.Threading.Interlocked.Increment(ref nextInstanceId);
        public int GetInstanceID() { return instanceId; }
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
        private SpriteRenderer attachedSprite;
        public GameObject(string name) { this.name = name; }
        public T AddComponent<T>() where T : Component, new()
        {
            T component = new T();
            var sprite = component as SpriteRenderer;
            if (sprite != null) attachedSprite = sprite;
            var animator = component as Animator;
            if (animator != null) animator.targetRenderer = attachedSprite;
            return component;
        }
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
        public readonly int width,height;
        public RenderTexture(int width,int height,int depth,RenderTextureFormat format)
        { this.width=width; this.height=height; }
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
    public class RuntimeAnimatorController : Object
    {
        // Fake controller class identity ONLY for offline testing.
        public string className;
    }
    public class Animator : Behaviour
    {
        public RuntimeAnimatorController runtimeAnimatorController;
        public SpriteRenderer targetRenderer;
        public float speed;
        public float lastNormalizedSample;
        public int sampleCount;
        public static bool InjectWrongSpriteOnce;
        private int action, facing, pose;
        private static readonly string[] Actions =
            { "Idle","Run","Attack","Cast","Dodge","Hit","Death" };
        private static readonly string[] Faces = { "E","N","S" };
        public static int StringToHash(string state) { return 0; }
        public void Play(int state,int layer,float normalizedTime)
        {
            lastNormalizedSample = normalizedTime;
            sampleCount++;
        }
        public void SetInteger(string parameter,int value)
        {
            if (parameter=="Action") action=value;
            if (parameter=="Facing") facing=value;
            if (parameter=="Pose") pose=value;
        }
        public void Update(float deltaTime)
        {
            // Pixel-free simulation of a Unity Sprite object-reference
            // curve at 12fps. The EXACT native production C# invokes this
            // only after supplying GameCore's Action/Facing/cell sample.
            // No Unity Graph or GPU behaviour is claimed.
            if (!enabled || runtimeAnimatorController==null ||
                targetRenderer==null || action<0 || action>=Actions.Length ||
                facing<0 || facing>=Faces.Length) return;
            // Native nonhero bridge is also driven by a hard GameCore row,
            // never by Animator autonomous time. This emulates only its
            // imported Sprite curve, not Unity's GPU or graph validation.
            if(runtimeAnimatorController.className!=null &&
                runtimeAnimatorController.className.StartsWith("Actor_"))
            {
                string nonhero="Frontier/Art/"+
                    runtimeAnimatorController.className.Substring(6);
                Sprite[] actorFrames=Resources.LoadAll<Sprite>(nonhero);
                int col=(int)Math.Floor(lastNormalizedSample*4f);
                int selected=pose*4+Math.Max(0,Math.Min(3,col));
                if(selected<0 || selected>=actorFrames.Length)return;
                if(InjectWrongSpriteOnce)
                {
                    InjectWrongSpriteOnce=false;
                    targetRenderer.sprite=Sprite.Create(
                        new Texture2D(128,128,TextureFormat.RGBA32,false),
                        new Rect(0,0,128,128),new Vector2(.5f,.0625f),
                        128f,0,SpriteMeshType.FullRect);
                }
                else targetRenderer.sprite=actorFrames[selected];
                return;
            }
            string key="Frontier/HeroAnimator/Canonical/Hero_"+
                runtimeAnimatorController.className+"_"+Actions[action]+"_"+
                Faces[facing];
            Sprite[] frames=Resources.LoadAll<Sprite>(key);
            if(frames.Length==0)return;
            int slot=(int)Math.Floor(lastNormalizedSample*frames.Length);
            if(slot<0)slot=0;
            if(slot>=frames.Length)slot=frames.Length-1;
            if(InjectWrongSpriteOnce)
            {
                InjectWrongSpriteOnce=false;
                targetRenderer.sprite=Sprite.Create(
                    new Texture2D(256,256,TextureFormat.RGBA32,false),
                    new Rect(0,0,256,256),new Vector2(128,16),128f,0,
                    SpriteMeshType.FullRect);
            }
            else targetRenderer.sprite=frames[slot];
        }
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
        public static bool InjectGpuFailureOnce;
        public void Render()
        {
            if(InjectGpuFailureOnce)
            {
                InjectGpuFailureOnce=false;
                throw new InvalidOperationException("simulated lost GPU target");
            }
            RenderedTargets.Add(targetTexture);
        }
    }
    public static class Resources
    {
        // Registered source textures only serve offline native-C# probes.
        public static readonly Dictionary<string,Object> Registered =
            new Dictionary<string,Object>();
        public static readonly Dictionary<string,Sprite[]> RegisteredSprites =
            new Dictionary<string,Sprite[]>();
        public static T Load<T>(string path) where T : Object
        {
            Object found;
            return Registered.TryGetValue(path,out found) ? found as T : null;
        }
        public static T[] LoadAll<T>(string path) where T : Object
        {
            Sprite[] sprites;
            if(typeof(T)==typeof(Sprite) &&
                RegisteredSprites.TryGetValue(path,out sprites))
                return (T[])(object)sprites;
            return new T[0];
        }
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
        // Only the offline native capture probe controls these fields.
        // Actual damage-clock selection is independently tested against
        // GameCore in RuntimeCombatInputChecks (216 real boundary cases).
        public int ProbeAction=2, ProbeFrames=6, ProbeFrame=2;
        public float ProbePosition=2f;
        private bool ResolveFullBodyHeroFrame(bool movement,int facing,
            out int action,out int frames,out int frame,out float position)
        {
            action=ProbeAction;frames=ProbeFrames;
            frame=ProbeFrame;position=ProbePosition;
            return true;
        }
    }
}
