// Offline syntax/layout harness only. This is not Unity and is never imported into Assets.
// It records the production OnGUI calls for layout inspection; actual Editor/Player QA is still required.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace UnityEngine
{
    public class Object { public string name; public bool destroyed; public int GetInstanceID(){return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);} public static T FindObjectOfType<T>() where T:class { return null; } public static void Destroy(Object o) {o.destroyed=true;} }
    public class GameObject:Object { public GameObject(string name="") { this.name=name; } public T AddComponent<T>() where T:new() { return new T(); } }
    public class MonoBehaviour:Object { public GameObject gameObject=new GameObject(); public object StartCoroutine(System.Collections.IEnumerator routine){return null;} }
    public struct Color
    {
        public float r,g,b,a; public Color(float r,float g,float b,float a=1) {this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white=new Color(1,1,1),black=new Color(0,0,0);
        public static Color Lerp(Color a,Color b,float t) {t=Mathf.Clamp01(t); return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
    }
    public static class ColorUtility { public static bool TryParseHtmlString(string h,out Color c) {try { c=new Color(Convert.ToInt32(h.Substring(1,2),16)/255f,Convert.ToInt32(h.Substring(3,2),16)/255f,Convert.ToInt32(h.Substring(5,2),16)/255f);return true;}catch{c=Color.white;return false;} } }
    public enum TextureFormat { RGBA32 } public enum FilterMode {Point,Bilinear} public enum TextureWrapMode {Clamp}
    public class Texture2D:Object
    {
        public int width,height; public FilterMode filterMode; public TextureWrapMode wrapMode; public Color[] pixels; public bool isReadable=true; public int mipmapCount=1;
        public static Texture2D whiteTexture=new Texture2D(1,1,TextureFormat.RGBA32,false){name="white"};
        public Texture2D(int w,int h,TextureFormat f,bool mip) {width=w;height=h;}
        public int pixelReads;
        public Color[] GetPixels(){pixelReads++;if(!isReadable)throw new InvalidOperationException("Texture is not readable: "+name);return pixels??new Color[width*height];}
        // Match the native Unity rectangular GetPixels contract. Origin is
        // bottom-left; this stub is intentionally a pixel-layout check, not GPU.
        public Color[] GetPixels(int x,int y,int w,int h)
        {
            if(x<0||y<0||w<=0||h<=0||x+w>width||y+h>height)
                throw new ArgumentException("Texture sample outside source "+name);
            var original=GetPixels();var block=new Color[w*h];
            for(int row=0;row<h;row++)
                Array.Copy(original,(y+row)*width+x,block,row*w,w);
            return block;
        }
        public void SetPixels(Color[] c) {pixels=c;} public void Apply(bool m,bool r){isReadable=!r;}
    }
    public class Font:Object {public static Font CreateDynamicFontFromOSFont(string[] n,int s){return new Font();}}
    public class TextAsset:Object {public string text;}
    public class AudioClip:Object {public static AudioClip Create(string n,int l,int c,int r,bool s){return new AudioClip{name=n};} public bool SetData(float[] f,int o){return true;}}
    public class AudioSource:Object {public float volume,pitch=1;public bool loop,isPlaying;public AudioClip clip;public readonly List<string> heard=new List<string>();public readonly List<float> gains=new List<float>();public void Play(){isPlaying=true;}public void Stop(){isPlaying=false;}public void PlayOneShot(AudioClip c,float gain=1){heard.Add(c.name);gains.Add(gain);} }
    public static class Application {public static int targetFrameRate;public static string persistentDataPath=".starfall-checks/saves",dataPath="Assets";}
    public sealed class WaitForEndOfFrame {}
    public static class ScreenCapture {public static void CaptureScreenshot(string path){} }
    public static class Time {public static float unscaledDeltaTime=.02f;}
    public enum EventType {Repaint,Layout,KeyDown,KeyUp,Used}
    public static class Screen {public static int width=1280,height=800;}
    public struct Vector3 { public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
    public struct Quaternion
    {
        public float zAngle; public static Quaternion identity;
        public static Quaternion Euler(float x,float y,float z) { if(x!=0||y!=0)throw new NotSupportedException("Offline GUI supports only 2D rotations");return new Quaternion {zAngle=z}; }
    }
    // Real 2D affine algebra for the Unity operations used by the renderer.
    // This still does not emulate the GPU, GUIClip or native font rendering.
    public struct Matrix4x4
    {
        public float m00,m01,m03,m10,m11,m13;
        public static Matrix4x4 identity=new Matrix4x4 {m00=1,m11=1};
        public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)
        { double a=q.zAngle*Math.PI/180;float c=(float)Math.Cos(a),n=(float)Math.Sin(a);return new Matrix4x4 {m00=c*s.x,m01=-n*s.y,m03=p.x,m10=n*s.x,m11=c*s.y,m13=p.y}; }
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)
        { return new Matrix4x4 {m00=a.m00*b.m00+a.m01*b.m10,m01=a.m00*b.m01+a.m01*b.m11,m03=a.m00*b.m03+a.m01*b.m13+a.m03,m10=a.m10*b.m00+a.m11*b.m10,m11=a.m10*b.m01+a.m11*b.m11,m13=a.m10*b.m03+a.m11*b.m13+a.m13}; }
        public Vector3 MultiplyPoint3x4(Vector3 p) {return new Vector3(m00*p.x+m01*p.y+m03,m10*p.x+m11*p.y+m13,p.z);}
    }
    public enum KeyCode {J,Space,Q,E,F,H,I,T,Z,P,B,C,G,N,Tab,K,M,L,Escape,F1,Return,R,LeftArrow,RightArrow,UpArrow,DownArrow,LeftShift,RightShift,LeftControl,W=110,A,S,D,V,X,Y,O,JoystickButton0=30,JoystickButton1,JoystickButton2,JoystickButton3,JoystickButton4,JoystickButton5,JoystickButton6,JoystickButton7,JoystickButton8,Alpha1=100,Alpha2,Alpha3,Alpha4,Alpha5,Alpha6,PageUp,PageDown,Home,End,Backspace=8,F2=283}
    public static class Input {public static HashSet<KeyCode> keys=new HashSet<KeyCode>(),held=new HashSet<KeyCode>();public static HashSet<int> buttons=new HashSet<int>();public static Dictionary<string,float> axes=new Dictionary<string,float>();public static Vector3 mousePosition; public static float GetAxisRaw(string a){float v;return axes.TryGetValue(a,out v)?v:0;}public static bool GetMouseButton(int b){return buttons.Contains(b);}public static bool GetKey(KeyCode k){return held.Contains(k);}public static bool GetKeyDown(KeyCode k){return keys.Contains(k);}public static void Clear(){keys.Clear();held.Clear();buttons.Clear();axes.Clear();}}
    public static class Mathf {public static float Min(float a,float b){return Math.Min(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}public static float Exp(float a){return (float)Math.Exp(a);}public static float Sin(float a){return (float)Math.Sin(a);}public static float Clamp01(float a){return Math.Max(0,Math.Min(1,a));}public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp01(t);} }
    public enum TextAnchor {UpperLeft,MiddleCenter}
    public class RectOffset {public int left,right,top,bottom;public RectOffset(int a,int b,int c,int d){left=a;right=b;top=c;bottom=d;} }
    public class GUIStyleState {public Texture2D background;public Texture2D[] scaledBackgrounds=new Texture2D[0];public Color textColor=Color.white;}
    public class GUIStyle
    {
        public Font font;public bool wordWrap,richText;public int fontSize;public TextAnchor alignment;public RectOffset padding;
        public GUIStyleState normal=new GUIStyleState(),hover=new GUIStyleState(),active=new GUIStyleState(),focused=new GUIStyleState(),onNormal=new GUIStyleState(),onHover=new GUIStyleState(),onActive=new GUIStyleState(),onFocused=new GUIStyleState();
        static GUIStyleState Copy(GUIStyleState s){return new GUIStyleState {background=s.background,scaledBackgrounds=(Texture2D[])s.scaledBackgrounds.Clone(),textColor=s.textColor};}
        public GUIStyle(){}public GUIStyle(GUIStyle g){font=g.font;wordWrap=g.wordWrap;richText=g.richText;fontSize=g.fontSize;alignment=g.alignment;padding=g.padding;normal=Copy(g.normal);hover=Copy(g.hover);active=Copy(g.active);focused=Copy(g.focused);onNormal=Copy(g.onNormal);onHover=Copy(g.onHover);onActive=Copy(g.onActive);onFocused=Copy(g.onFocused);}
    }
    public struct Rect
    {
        public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;this.width=w;this.height=h;}
        public bool Contains(Vector2 p){return p.x>=x&&p.y>=y&&p.x<x+width&&p.y<y+height;}
    }
    public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
    public class Event {public static Event current=new Event();public Vector2 mousePosition;public EventType type=EventType.Repaint;public void Use(){type=EventType.Used;}}
    public class GUISkin
    {
        public GUIStyle label=new GUIStyle(),button=new GUIStyle();
        public GUISkin()
        {
            var background=new Texture2D(1,1,TextureFormat.RGBA32,false) {name="native-button-background",pixels=new[]{new Color(.35f,.35f,.35f)}};
            foreach(var s in new[]{button.normal,button.hover,button.active,button.focused,button.onNormal,button.onHover,button.onActive,button.onFocused}) {s.background=background;s.scaledBackgrounds=new[]{background};}
        }
    }
    public static class Debug {public static void LogWarning(object o){Console.Error.WriteLine(o);}public static void LogError(object o){Console.Error.WriteLine(o);}public static void Log(object o){Console.WriteLine(o);} }
    public static class Resources
    {
        // Offline-only asset import sandbox: exercises a complete 18-file
        // sprite release without touching accepted on-disk source art.
        public static readonly Dictionary<string,Texture2D> TextureOverrides =
            new Dictionary<string,Texture2D>();
        public static int weatherWispImportLoads;
        public static T Load<T>(string p) where T:class
        {
            if(typeof(T)==typeof(TextAsset)) {
                string visualText="Assets/Resources/"+p+".txt";
                if(File.Exists(visualText))
                    return new TextAsset {text=File.ReadAllText(visualText)} as T;
                return new TextAsset {text="campaign"} as T;
            }
            if(typeof(T)==typeof(Texture2D)) {
                if(p=="Frontier/Art/WeatherWisps")weatherWispImportLoads++;
                Texture2D candidate;
                if(TextureOverrides.TryGetValue(p,out candidate))
                    return candidate as T;
                string path="Assets/Resources/"+p+".png";
                if(!File.Exists(path))return null;
                var header=File.ReadAllBytes(path); int w=(header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19],h=(header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23];
                string meta=File.Exists(path+".meta")?File.ReadAllText(path+".meta"):"";
                var t=new Texture2D(w,h,TextureFormat.RGBA32,false){name="resource:"+p,isReadable=meta.Contains("isReadable: 1")};
                string pixelPath=".starfall-checks/resourcepixels/"+Path.GetFileNameWithoutExtension(path)+".rgba";
                if(File.Exists(pixelPath)){var bytes=File.ReadAllBytes(pixelPath);t.pixels=new Color[w*h];for(int i=0;i<w*h;i++)t.pixels[i]=new Color(bytes[i*4]/255f,bytes[i*4+1]/255f,bytes[i*4+2]/255f,bytes[i*4+3]/255f);}
                return t as T;
            }
            if(typeof(T)==typeof(Font))return new Font() as T;return null;
        }
    }
    public static partial class JsonUtility {}
    public enum RuntimeInitializeLoadType {AfterSceneLoad}
    public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
    public static class GUI
    {
        public sealed class TextureCall {public Rect rect,uv;public Matrix4x4 matrix;public Texture2D texture;public Color color;}
        public sealed class ButtonCall {public Rect rect;public GUIStyle style;public bool enabled;public Texture2D background;}
        public static Matrix4x4 matrix=Matrix4x4.identity;public static Color color=Color.white;public static bool enabled=true;public static GUISkin skin=new GUISkin();
        public static float pixelsPerPoint=1; public static Rect? queuedClick;
        public static List<TextureCall> textureCalls=new List<TextureCall>();public static List<ButtonCall> buttonCalls=new List<ButtonCall>();
        public static List<string> commands=new List<string>();public static Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();public static HashSet<string> exportedTextures=new HashSet<string>();
        public static string S(string s){return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r")+"\"";}
        static string N(float n){return n.ToString(CultureInfo.InvariantCulture);}
        static string R(Rect r){return "["+N(r.x)+","+N(r.y)+","+N(r.width)+","+N(r.height)+"]";}
        static string C(Color c){return "["+N(c.r)+","+N(c.g)+","+N(c.b)+","+N(c.a)+"]";}
        static string M(Matrix4x4 m){return "["+N(m.m00)+","+N(m.m01)+","+N(m.m03)+","+N(m.m10)+","+N(m.m11)+","+N(m.m13)+"]";}
        public static void DrawTexture(Rect r,Texture2D t){DrawTextureWithTexCoords(r,t,new Rect(0,0,1,1));}
        public static void DrawTextureWithTexCoords(Rect r,Texture2D t,Rect uv){textures[t.name]=t;textureCalls.Add(new TextureCall {rect=r,uv=uv,matrix=matrix,texture=t,color=color});commands.Add("{\"k\":\"texture\",\"name\":"+S(t.name)+",\"r\":"+R(r)+",\"uv\":"+R(uv)+",\"color\":"+C(color)+",\"m\":"+M(matrix)+",\"screen\":["+Screen.width+","+Screen.height+"]}");}
        public static void Label(Rect r,string s){Label(r,s,new GUIStyle());}
        public static void Label(Rect r,string s,GUIStyle style){var p=style.padding;commands.Add("{\"k\":\"text\",\"text\":"+S(s)+",\"padding\":["+(p==null?"0,0,0,0":p.left+","+p.right+","+p.top+","+p.bottom)+"],\"r\":"+R(r)+",\"size\":"+style.fontSize+",\"color\":"+C(style.normal.textColor)+",\"center\":"+(style.alignment==TextAnchor.MiddleCenter?"true":"false")+",\"m\":"+M(matrix)+",\"screen\":["+Screen.width+","+Screen.height+"]}");}
        public static bool Button(Rect r,string text,GUIStyle s)
        {
            Texture2D background=pixelsPerPoint>1&&s.normal.scaledBackgrounds.Length>0?s.normal.scaledBackgrounds[0]:s.normal.background;
            buttonCalls.Add(new ButtonCall {rect=r,style=s,enabled=enabled,background=background});if(background!=null)DrawTexture(r,background);Label(r,text,s);
            bool hit=enabled&&queuedClick.HasValue&&r.x==queuedClick.Value.x&&r.y==queuedClick.Value.y;if(hit)queuedClick=null;return hit;
        }
        public static void Export(string folder,string name)
        {
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,name+".json"),"["+string.Join(",",commands)+"]");
            foreach(var pair in textures)
            {
                var t=pair.Value;if(t.name.StartsWith("resource:") || t.pixels==null || !exportedTextures.Add(pair.Key))continue;
                var rgba=new byte[t.pixels.Length*4];for(int i=0;i<t.pixels.Length;i++){var c=t.pixels[i];rgba[i*4]=(byte)(Mathf.Clamp01(c.r)*255);rgba[i*4+1]=(byte)(Mathf.Clamp01(c.g)*255);rgba[i*4+2]=(byte)(Mathf.Clamp01(c.b)*255);rgba[i*4+3]=(byte)(Mathf.Clamp01(c.a)*255);}
                File.WriteAllBytes(Path.Combine(folder,t.name.Replace("/","_").Replace(":","_")+".rgba"),rgba);
                File.WriteAllText(Path.Combine(folder,t.name.Replace("/","_").Replace(":","_")+".size"),t.width+","+t.height);
            }
            commands.Clear();textureCalls.Clear();buttonCalls.Clear();
        }
    }
}
namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode {Single} public struct Scene {public string name;}
    public static class SceneManager {public static event Action<Scene,LoadSceneMode> sceneLoaded;public static Scene GetActiveScene(){return new Scene {name="Frontier"};}}
}
class LayoutHarness
{
    const System.Reflection.BindingFlags Private=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
    static void Invoke(object o,string method){o.GetType().GetMethod(method,Private).Invoke(o,null);}
    static void Set(object o,string name,object value){o.GetType().GetField(name,Private).SetValue(o,value);}
    static void Export(Starfall.FrontierRuntime app,string folder,string name){app.Game.toastTime=0;Invoke(app,"OnGUI");UnityEngine.GUI.Export(folder,name);}
    static void Narrative(Starfall.FrontierRuntime app,string folder,string name)
    {
        var c=app.Game.conversation;if(c==null)return;Export(app,folder,name);
        var pages=Starfall.DialogueLayout.Pages(app.Game.L(c.text,c.textEn),app.Game.save.largeText);
        for(int page=1;page<pages.Count;page++){Set(app,"dialoguePage",page);Export(app,folder,name+"-page"+page);}
    }
    static int Main(string[] args)
    {
        var app=new Starfall.FrontierRuntime();Invoke(app,"Awake");if(app.Game==null)return 1;
#if RESIDENT_ONLY
        ActorRegistrationChecks.VerifyResidentAnimation(app);
        // Mandatory nonhero CI must execute the real boss draw method too:
        // phase thresholds, matched flash atlas cells and the GameCore clock.
        ActorRegistrationChecks.VerifyBossMovingFlash(app);
        RenderStateChecks.VerifyMalformedTransientRecordsAreSkippedByProductionDrawGuards(app);
        return 0;
#elif CONTINUITY_ONLY
        ActorRegistrationChecks.Run(app,args[0]);RuntimeContinuityChecks.Run(app,args[0]);return 0;
#else
        Export(app,args[0],"title");app.Game.NewGame(0);app.Game.Close();Invoke(app,"Update");Export(app,args[0],"town");
        app.Game.Enter(1);app.Game.save.x=21;app.Game.save.y=9;Invoke(app,"Update");Export(app,args[0],"forest");
        foreach(string mode in new[]{"inventory","journal","talents","map"}){app.Game.mode=mode;Export(app,args[0],mode);}
        app.Game.NewGame(1);Narrative(app,args[0],"dialogue");app.Game.Close();app.Game.save.stage=3;
        foreach(string flag in new[]{"exp_intro","harbor_report","desert_report","mountain_report","frontier_settled","gate_5","gate_6","gate_7","companion_0","companion_1","companion_2","relic_0","relic_2","relic_3","relic_4","relic_5"})app.Game.save.flags.Add(flag);
        app.Game.SkipTutorial();app.Game.save.frontierEnding=3;app.Game.save.companion=0;app.Game.save.relics=new[]{0,4};app.Game.save.shards=24;app.Game.save.tea=3;
        app.Game.save.mirrors=new[]{1,2,1};app.Game.save.projects=new[]{2,1,1};app.Game.save.reputation=new[]{4,5,4};
        for(int i=0;i<2;i++)
        {
            app.Game.save.english=i==1;string suffix=i==1?"-en":"";app.Game.Enter(0);Invoke(app,"Update");
            app.Game.mode="title";Export(app,args[0],"title"+suffix);
            foreach(string mode in new[]{"camp","companions","loadout","bounties","map","inventory","talents","pause","help","fish"}){app.Game.mode=mode;Export(app,args[0],mode+suffix);}
            app.Game.mode="journal";Set(app,"journalTab",0);Set(app,"selectedQuest",1);Export(app,args[0],"letters"+suffix);
            Set(app,"journalTab",1);Set(app,"recordsPage",0);Export(app,args[0],"codex"+suffix);Set(app,"recordsPage",1);Export(app,args[0],"codex-page2"+suffix);Set(app,"recordsPage",0);Set(app,"journalTab",2);Export(app,args[0],"factions"+suffix);
            for(int room=5;room<=7;room++){app.Game.Enter(room);app.Game.save.x=18;app.Game.save.y=room==6?12:14;Invoke(app,"Update");Set(app,"camera",app.Game.Position);Export(app,args[0],new[]{"port","desert","frost"}[room-5]+suffix);}
            app.Game.Enter(0);app.Game.StartAbyss(1);Invoke(app,"Update");Set(app,"camera",app.Game.Position);Export(app,args[0],"abyss"+suffix);
            app.Game.ReturnToTown();app.Game.Choose(0);app.Game.Enter(5);foreach(var o in app.Game.Map.objects)if(o.kind=="captain"){app.Game.Talk(o);break;}Narrative(app,args[0],"story"+suffix);app.Game.Close();
        }
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)
        {
            app.Game.save.english=language==1;app.Game.save.largeText=large==1;string suffix=(language==1?"-en":"")+(large==1?"-large":"");
            app.Game.mode="help";for(int topic=0;topic<12;topic++){Set(app,"helpTopic",topic);Export(app,args[0],"qa-help-"+topic+suffix);}
            app.Game.Enter(0);app.Game.enemies.Clear();app.Game.mode="world";for(int step=0;step<8;step++){app.Game.save.tutorialBits=(1<<step)-1;Export(app,args[0],"qa-tutorial-"+step+suffix);}
            app.Game.SkipTutorial();app.Game.mode="journal";Set(app,"journalTab",0);for(int quest=0;quest<Starfall.GameCore.QuestIds.Length;quest++){Set(app,"selectedQuest",quest);Export(app,args[0],"qa-quest-"+quest+suffix);}
            foreach(string mode in new[]{"inventory","loadout","talents","companions","camp","bounties","map","pause"}){app.Game.mode=mode;Export(app,args[0],"qa-"+mode+suffix);}
            app.Game.mode="world";app.Game.save.bounty=0;for(int quest=0;quest<Starfall.GameCore.QuestIds.Length;quest++){app.Game.save.trackedQuest=Starfall.GameCore.QuestIds[quest];Export(app,args[0],"qa-hud-"+quest+suffix);}app.Game.save.bounty=-1;app.Game.save.trackedQuest="main";
            app.Game.mode="inventory";for(int item=0;item<8;item++){Set(app,"selectedItem",item);Export(app,args[0],"qa-item-"+item+suffix);}
            for(int record=0;record<app.Game.data.lore.Length;record++){app.Game.save.flags.Add("history_"+record);app.Game.OpenCodex(record);Narrative(app,args[0],"qa-record-"+record+suffix);app.Game.Close();}
            for(int room=0;room<8;room++){foreach(var o in app.Game.data.maps[room].objects){app.Game.Enter(room);app.Game.Talk(o);if(app.Game.mode=="dialog")Narrative(app,args[0],"qa-npc-"+o.id+suffix);app.Game.Close();}}
        }
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)
        {
            app.Game.save.english=language==1;app.Game.save.largeText=large==1;string suffix=(language==1?"-en":"")+(large==1?"-large":"");
            app.Game.Enter(0);app.Game.Close();app.Game.save.harborPledge=3;app.Game.save.desertChoice=1;app.Game.save.mountainChoice=2;
            string[] sampleZh={"莉雅收到你的信，托你把失联侦察员带回家。", "下一步：前往荆棘密林，与侦察员交谈。", "完成训练场：攻击、观察预告、闪避与职业战技。", "侦察员回家了，并送来一份热汤。", "打开隐藏路线：旧巡路队的藤封。", "巡路队的旧日补给已找回：硬币30、碎片3、炖汤1、熟练度8。", "沙海调度公开，山野议会支持增加。", "高原的名字公开，山野议会支持增加。"};
            string[] sampleEn={"Lyra received your letter and asked you to bring the missing scout home.", "Next: reach Thornwood and speak to the scout.", "Practice complete: attack, read a tell, dodge and use your technique.", "The scout made it home and brought you a warm stew.", "Hidden route opened: road-warden vine seal.", "Road-warden cache recovered: 30 coins, 3 shards, 1 stew, 8 weapon mastery.", "Desert allocations published; Assembly trust grew.", "Mountain names published; Assembly trust grew."};
            for(int i=0;i<8;i++)app.Game.RecordMoment(sampleZh[i], sampleEn[i]);app.Game.save.flags.Add("scout_saved");
            app.Game.mode="journal";Set(app,"journalTab",3);Export(app,args[0],"journey"+suffix);
            app.Game.mode="journal";Set(app,"journalTab",1);Set(app,"recordsPage",1);Export(app,args[0],"qa-records-page2"+suffix);Set(app,"recordsPage",0);
            app.Game.mode="saves";Set(app,"slotData",new Starfall.SaveData[]{new Starfall.SaveData{room=7,stage=3,level=30},new Starfall.SaveData{room=5,level=5},null,new Starfall.SaveData{room=8,level=12}});Set(app,"slotWarnings",new string[]{null,"Recovered backup.",null,null});Export(app,args[0],"saves"+suffix);
            Set(app,"saveConfirmation","write");Export(app,args[0],"qa-save-write"+suffix);Set(app,"saveConfirmation","read");Export(app,args[0],"qa-save-read"+suffix);Set(app,"saveConfirmation",null);
            app.Game.mode="title";Set(app,"replacePrompt",true);Export(app,args[0],"qa-replace"+suffix);Set(app,"replacePrompt",false);
            app.Game.mode="inventory";for(int weapon=0;weapon<3;weapon++)for(int style=0;style<3;style++){Set(app,"inspectedWeapon",weapon);Set(app,"inspectedStyle",style);Export(app,args[0],"qa-build-"+weapon+"-"+style+suffix);}
            app.Game.mode="world";app.Game.StartPractice();Set(app,"camera",app.Game.Position);for(int stage=0;stage<3;stage++){app.Game.practiceStage=stage;Export(app,args[0],"qa-practice-"+stage+suffix);}app.Game.EndPractice();Set(app,"camera",app.Game.Position);
            foreach(int kind in new[]{0,1,2,3}){app.Game.enemies.Clear();var foe=new Starfall.Enemy(21,kind,app.Game.save.x+2,app.Game.save.y,120,kind==3);foe.state=1;foe.target=app.Game.Position;app.Game.enemies.Add(foe);Export(app,args[0],"qa-tell-"+kind+suffix);}app.Game.enemies.Clear();
            Set(app,"resumeBrief",app.Game.GuidanceHint());Set(app,"resumeTime",8f);Export(app,args[0],"qa-resume"+suffix);Set(app,"resumeTime",0f);
            if(large==0){app.Game.StartPractice();Set(app,"camera",app.Game.Position);Export(app,args[0],"practice"+(language==1?"-en":""));app.Game.EndPractice();Set(app,"camera",app.Game.Position);}
            Set(app,"controllerUsed",true);app.Game.mode="pause";Export(app,args[0],"qa-pad-pause"+suffix);app.Game.mode="help";Set(app,"helpTopic",0);Export(app,args[0],"qa-pad-help"+suffix);
            app.Game.mode="world";for(int step=0;step<8;step++){app.Game.save.tutorialBits=(1<<step)-1;Export(app,args[0],"qa-pad-tutorial-"+step+suffix);}app.Game.SkipTutorial();app.Game.StartPractice();Set(app,"camera",app.Game.Position);for(int stage=0;stage<3;stage++){app.Game.practiceStage=stage;Export(app,args[0],"qa-pad-practice-"+stage+suffix);}app.Game.EndPractice();Set(app,"controllerUsed",false);
        }
        app.Game.save.largeText=true;app.Game.save.english=true;string story="";
        for(int paragraph=0;paragraph<12;paragraph++)story+="The tide carried every unsent letter toward a harbor beyond the glass desert.\n\n";
        app.Game.conversation=new Starfall.Conversation("米拉","Mira",story,story,new Starfall.Choice("结束","Finish","close"));app.Game.mode="dialog";Export(app,args[0],"qa-dialogue-long");
        UnityEngine.Input.keys.Add(UnityEngine.KeyCode.Alpha1);Invoke(app,"Update");UnityEngine.Input.keys.Clear();if(app.Game.mode!="dialog")throw new Exception("Numbered choices skipped unread narrative");
        int pageCount=Starfall.DialogueLayout.Pages(story,true).Count;for(int page=1;page<pageCount;page++){UnityEngine.Input.keys.Add(UnityEngine.KeyCode.Return);Invoke(app,"Update");UnityEngine.Input.keys.Clear();Export(app,args[0],"qa-dialogue-page-"+page);}
        UnityEngine.Input.keys.Add(UnityEngine.KeyCode.Alpha1);Invoke(app,"Update");UnityEngine.Input.keys.Clear();if(app.Game.mode!="world")throw new Exception("Read narrative could not choose its outcome");
        RuntimePolishChecks.Run(app); VisualRuntimeChecks.Run(app,args[0]); RenderStateChecks.Run(app,args[0]);ActorRegistrationChecks.Run(app,args[0]);RuntimeContinuityChecks.Run(app,args[0]); Console.WriteLine("PASS Runtime dialogue input gating and bilingual layout states");return 0;
#endif
    }
}
