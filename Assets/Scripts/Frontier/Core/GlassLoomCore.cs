using System;

namespace Starfall
{
    // The Glasswell Homeward Lens is a new authored, physically connected
    // daytime/nighttime optical puzzle in the real Glasswind desert map.
    // All rotations and active light are transient. Only the two exclusive
    // earned endings use existing SaveData v2 flags.
    public sealed partial class GameCore
    {
        private int glassLoomStage;
        private float glassLoomTime,glassLoomHeatClock;
        public bool GlassLoomBeamActive
        {get{return glassLoomStage==2&&!Night&&GlassLoomPathClear;}}
        private readonly int[] glassLoomRotations=new int[3];
        private static readonly int[] glassLoomCode={2,0,1};
        public bool GlassLoomActive { get { return glassLoomStage>0; } }
        public bool GlassLoomComplete
        { get { return Has("glassloom_public")||Has("glassloom_private"); } }
        public bool GlassLoomPublic { get { return Has("glassloom_public"); } }
        public bool GlassLoomPrivate { get { return Has("glassloom_private"); } }
        public int GlassLoomStage { get { return glassLoomStage; } }
        public float GlassLoomTime { get { return GlassLoomActive?glassLoomTime:0f; } }
        public Point GlassLoomTarget
        { get { return glassLoomStage==2?new Point(76.5f,50.5f):
            new Point(60.5f,50.5f); } }
        public string GlassLoomObjective
        {
            get
            {
                if(glassLoomStage==1)
                    return L("转动三镜：蓝—透明—铜，白天聚光",
                        "TURN 3 LENSES: BLUE-CLEAR-COPPER · DAYLIGHT");
                if(glassLoomStage==2)
                    return L("入夜后在接收台收取真正的归光",
                        "CAPTURE HOMEWARD LIGHT AT NIGHT");
                return L("回光井选择归路","RETURN TO GLASSWELL · CHOOSE ROUTE");
            }
        }
        public int GlassLoomOrientation(int index)
        { return index>=0&&index<3?glassLoomRotations[index]:-1; }
        public bool GlassLoomAligned(int index)
        { return index>=0&&index<3&&glassLoomRotations[index]==glassLoomCode[index]; }
        public bool GlassLoomOpticsAligned
        { get { return GlassLoomAligned(0)&&GlassLoomAligned(1)&&GlassLoomAligned(2); } }
        public bool GlassLoomPathClear
        {
            get
            {
                if(save.room!=6)return false;
                Point[] points={new Point(60.5f,50.5f),
                    new Point(64.5f,50.5f),new Point(68.5f,50.5f),
                    new Point(72.5f,50.5f),new Point(76.5f,50.5f)};
                foreach(Point p in points)if(!CanStand(p))return false;
                for(int i=0;i<points.Length-1;i++)
                    if(!ClearAttackPath(points[i],points[i+1]))return false;
                return true;
            }
        }
        private void ResetGlassLoom()
        {
            glassLoomStage=0;glassLoomTime=glassLoomHeatClock=0;
            hazards.RemoveAll(h=>h.glassLoomOwned);
            for(int i=0;i<3;i++)glassLoomRotations[i]=0;
        }
        private int GlassLoomMirrorIndex(WorldObject o)
        {
            if(o==null||o.kind!="glassloommirror")return -1;
            for(int i=0;i<3;i++)
                if(o.id=="glassloom_mirror_"+(i+1))return i;
            return -1;
        }
        private bool GlassLoomSourceValid(WorldObject o)
        {
            return o!=null&&save.room==6&&
                SourceBelongsToActiveMap(o)&&
                HasPhysicalInteractionReach(o);
        }
        private string GlassLoomFaceName(int face,bool english)
        {
            string[] zh={"透明","铜色","蓝色","紫色"};
            string[] en={"Clear","Copper","Blue","Violet"};
            return (english?en:zh)[Math.Max(0,Math.Min(3,face))];
        }
        private bool TalkGlassLoom(WorldObject o)
        {
            if(o==null||save.room!=6||
                (o.kind!="glassloomsource"&&o.kind!="glassloommirror"&&
                 o.kind!="glassloomreceiver"))return false;
            if(!GlassLoomSourceValid(o))return true;
            if(o.kind=="glassloommirror")
            {
                int index=GlassLoomMirrorIndex(o);
                if(index<0)return true;
                if(glassLoomStage!=1)
                {
                    Dialog(o.name,o.en,
                        GlassLoomComplete?
                        "晶片的折射角已经写入两地共用的归路图，不能重复领取校准奖励。":
                        "没有入射日光时，透镜只是沙中的一块旧玻璃。先到西侧光井读回原始路谱。",
                        GlassLoomComplete?
                        "The lens angle is now filed in the shared homeward chart. The unique first-clear reward cannot be reclaimed.":
                        "Without an incoming daylight ray this is only buried glass. Read the original score at the western Glasswell.",
                        C("收起铭牌","Close marker","close"));
                    return true;
                }
                Dialog(o.name,o.en,
                    "第"+(index+1)+"镜的晶面是"+
                    GlassLoomFaceName(glassLoomRotations[index],false)+
                    "，一共有四个方向：透明、铜色、蓝色、紫色。\n\n古谱写着“朝蓝风转第一镜，正午第二镜透明，暮星第三镜映铜”。每次实际转镜消耗3点耐力。",
                    "Lens "+(index+1)+" currently reads "+
                    GlassLoomFaceName(glassLoomRotations[index],true)+
                    ". Four faces: Clear, Copper, Blue, Violet.\n\nThe original score says: first the blue wind, second clear noon, third copper dusk. Each real rotation costs 3 stamina.",
                    C("转动一格 · 3耐力","Rotate one face · 3 energy",
                      "glassloom_turn"),
                    C("保持当前镜面","Keep orientation","close"));
                return true;
            }
            if(o.kind=="glassloomreceiver")
            {
                if(glassLoomStage==2)
                {
                    Dialog(o.name,o.en,
                        "接收台的铜杯里有一只断弦的旧船铃。只有真实夜色落下时，白天收集的三道折光才会在这里聚成家人的归航讯号。\n\n当前："+
                        (Night?"夜间，可归光。":"白昼，请等待真正的夜色。"),
                        "An old boat bell sits in the receiver. The three daylight reflections converge into a family homing signal only after actual night falls.\n\nCurrent: "+
                        (Night?"Night · capture ready.":"Daylight · wait for true night."),
                        C("收取夜间归光","Capture night light","glassloom_capture"),
                        C("继续守候","Keep watch","close"));
                    return true;
                }
                Dialog(o.name,o.en,
                    "这只接收器还未收到完整的白昼折光。去西面的光井启动光学线路。",
                    "The receiver has no complete daytime charge. Start the optical circuit at the western Glasswell.",
                    C("查看刻痕","Read etched route","close"));
                return true;
            }
            if(GlassLoomComplete)
            {
                Dialog(o.name,o.en,
                    GlassLoomPublic?
                    "光井现在把安全日照路线公开刻在沙海的通用路标上。日间穿越沙海时，你的攻击会多出3点基础伤害。首胜奖励只发一次。":
                    "你将完整的月路图交回居民，避免旧军队复用灯光追踪旅人。沙海夜间的移动速度额外提高0.5。首胜奖励只发一次。",
                    GlassLoomPublic?
                    "Glasswell now publishes the daylight approach on shared trail markers. Your daytime desert attacks deal +3 base damage. First-clear rewards are one-time.":
                    "The full night trail is returned to the families, keeping military trackers from following its light. Your nighttime desert movement gains +0.5 speed. First-clear rewards are one-time.",
                    C("阅读归路记载","Review route archive","close"));
                return true;
            }
            if(!Has("boss_6")||!Has("desert_report"))
            {
                Dialog(o.name,o.en,
                    "旧沙海测量站仍被军用航路封住。先完成玻璃沙海原来的守护者战斗与供水报告，居民才能取回光井控制权。",
                    "The old desert station remains sealed by military routing. Defeat the original Glasswind guardian and file its water report before civilians can reclaim this well.",
                    C("记录坐标","Mark location","close"));
                return true;
            }
            if(glassLoomStage==0)
            {
                Dialog(o.name,o.en,
                    "光井的旧日晷不是矿脉导航仪。它指向三面透镜和东侧接收杯，曾让迷路商队的孩子认出归途。\n\n真正的光谱：蓝风—透明正午—铜色暮星。先在白天启动光井并转动三面实物透镜，再回这里聚集日光；最后要在真正入夜后到东侧接收台收光。300秒内完成，失败可无材料损失重试。",
                    "This sundial once helped lost caravan children find home. The actual code is BLUE WIND, CLEAR NOON, COPPER DUSK. Begin in real daylight, rotate all three physical lenses, and return to charge the well. At actual nightfall capture the light at the eastern receiver. The 300-second attempt is freely retryable.",
                    C("日光开阵 · 300秒","Begin daylight alignment · 300s",
                        "glassloom_start"),
                    C("等候白昼","Wait for daylight","close"));
                return true;
            }
            if(glassLoomStage==1)
            {
                Dialog(o.name,o.en,
                    "三镜当前的实际面向："+GlassLoomFaceName(glassLoomRotations[0],false)+
                    "—"+GlassLoomFaceName(glassLoomRotations[1],false)+
                    "—"+GlassLoomFaceName(glassLoomRotations[2],false)+
                    "。正确答案是蓝—透明—铜。\n\n"+
                    (Night?"夜幕已至，不能凭空制造日光；等待白昼。":
                    "仍有日光，可检查光束是否真正穿过五个装置。"),
                    "Actual lens faces: "+GlassLoomFaceName(glassLoomRotations[0],true)+
                    "—"+GlassLoomFaceName(glassLoomRotations[1],true)+
                    "—"+GlassLoomFaceName(glassLoomRotations[2],true)+
                    ". Correct: BLUE—CLEAR—COPPER. "+
                    (Night?"It is night; sunlight cannot be invented.":
                        "Daylight is present; validate the actual clear five-device optical path."),
                    C("将三镜日光汇入光井","Focus real daylight","glassloom_focus"),
                    C("撤销本次阵列","Abort this attempt","glassloom_abort"));
                return true;
            }
            if(glassLoomStage==2)
            {
                Dialog(o.name,o.en,
                    "真实日光已沿三镜入射东侧铜杯。请亲自到接收台等待夜色，任何旧对话都不能替代接收器。",
                    "Actual daylight has passed the lenses into the eastern copper cup. Go there in person and wait for night; no stale dialogue can replace a receiver.",
                    C("继续向东","Go east","close"),
                    C("撤销本次阵列","Abort this attempt","glassloom_abort"));
                return true;
            }
            Dialog(o.name,o.en,
                "你已在真实夜色下找回归光。新的归路不应再归旧军方所有。\n\n公开日光路线：给所有商队可读的光路标记，永久增加玻璃沙海白天攻击的基础伤害3点。\n\n保护夜间隐路：只交给居民和救援队，永久提高沙海夜间移速0.5。两个结局互斥。",
                "The real night-light has been recovered. It must not return to military custody.\n\nPublish a daylight lane that all caravans can read: +3 base attack damage in daylight Glasswind.\n\nProtect families' hidden night route: +0.5 actual movement speed at night in Glasswind. The two outcomes are exclusive.",
                C("公开日光路 · 沙海白天攻击+3",
                    "Publish daylight road · +3 daytime desert damage",
                    "glassloom_public"),
                C("保护隐蔽夜路 · 沙海夜间移速+0.5",
                    "Protect hidden trail · +0.5 nighttime desert speed",
                    "glassloom_private"),
                C("稍后再决定","Decide later","close"));
            return true;
        }
        private bool ChooseGlassLoom(string action)
        {
            if(!action.StartsWith("glassloom_",StringComparison.Ordinal))return false;
            if(mode!="world"||Dying||practicing||save.room!=6)return true;
            WorldObject source=pendingDialogueSource;
            if(source==null||pendingDialogueSourceRoom!=6||
                !GlassLoomSourceValid(source))return true;
            if(action=="glassloom_start")
            {
                if(source.id!="glassloom_source"||glassLoomStage!=0||
                   GlassLoomComplete||!Has("boss_6")||!Has("desert_report")||
                   Night||!SafeTravel||FieldWorkPreventsRest||
                   !GlassLoomPathClear)return true;
                glassLoomStage=1;glassLoomTime=300f;
                for(int i=0;i<3;i++)glassLoomRotations[i]=0;
                TrackQuest("glass_loom");revision++;sound="quest";
                Notify("沙海归光阵启动！白天将三镜调为蓝—透明—铜，再回光井聚光。",
                    "Glasswell array active. In daylight tune BLUE—CLEAR—COPPER, then focus at the well.");
                return true;
            }
            if(action=="glassloom_abort")
            {
                if(source.id=="glassloom_source"&&GlassLoomActive)
                {
                    ResetGlassLoom();revision++;
                    Notify("归光阵已关闭，可在白昼重新启动。",
                        "Glasswell circuit withdrawn. Retry in daylight.");
                }
                return true;
            }
            if(action=="glassloom_turn")
            {
                int i=GlassLoomMirrorIndex(source);
                if(glassLoomStage!=1||i<0||save.energy<3f||
                    !CanStand(new Point(source.x,source.y)))return true;
                save.energy-=3f;
                glassLoomRotations[i]=(glassLoomRotations[i]+1)%4;
                effects.Add(new Effect("number",
                    new Point(source.x,source.y),.65f,
                    L("镜面："+GlassLoomFaceName(glassLoomRotations[i],false),
                      "FACE: "+GlassLoomFaceName(glassLoomRotations[i],true))));
                sound="quest";revision++;
                return true;
            }
            if(action=="glassloom_focus")
            {
                if(source.id!="glassloom_source"||glassLoomStage!=1||
                    Night||!GlassLoomOpticsAligned||!GlassLoomPathClear)
                {
                    Notify("三镜尚未按蓝—透明—铜连通，或缺少真实的白昼光线与无遮挡光路。",
                        "The three real lenses must read BLUE-CLEAR-COPPER under true daylight on an unobstructed beam path.");
                    return true;
                }
                glassLoomStage=2;glassLoomHeatClock=.9f;
                revision++;sound="quest";
                RecordMoment("三面透镜在真实白昼接住光井的光，东侧铜杯开始等待夜归人的信号。",
                    "Three real lenses gathered Glasswell's sunlight; the eastern copper cup began waiting for a homing call after dark.");
                Notify("白昼聚光完成！请亲自到东侧接收台等待真正的夜间。",
                    "Daylight focused! Reach the eastern receiver and wait for real night.");
                return true;
            }
            if(action=="glassloom_capture")
            {
                if(source.id!="glassloom_receiver"||glassLoomStage!=2||
                    !Night||!GlassLoomPathClear)return true;
                glassLoomStage=3;hazards.RemoveAll(h=>h.glassLoomOwned);
                revision++;sound="reward";
                Notify("归光已从真实夜空收回！返回西侧光井决定居民共享的路线。",
                    "Homeward light captured at true night! Return to Glasswell to choose a civilian route.");
                return true;
            }
            if(action=="glassloom_public"||action=="glassloom_private")
            {
                if(source.id!="glassloom_source"||glassLoomStage!=3||
                    GlassLoomComplete||glassLoomTime<=0f)return true;
                bool published=action=="glassloom_public";
                ResetGlassLoom();
                Flag(published?"glassloom_public":"glassloom_private");
                CreditCoins(70);CreditShards(3);AddXp(110);
                RecordMoment(published?
                    "归光阵在真正的昼夜交替后复明；你将沙海的日照安全路线公开，所有人都能自行找到归路。":
                    "归光阵在真正的昼夜交替后复明；你把不被军方追踪的夜间路线交回沙海居民。",
                    published?
                    "The Glasswell array recovered after real daylight and nightfall. You published the safe daylight route for all caravans.":
                    "The Glasswell array recovered after true day and night. You returned the hidden moon route to its families rather than military trackers.");
                revision++;sound="reward";
                Notify(published?
                    "归光阵完成！硬币+70、碎片+3、经验+110，沙海白天攻击+3。":
                    "归光阵完成！硬币+70、碎片+3、经验+110，沙海夜间移速+0.5。",
                    published?
                    "Glasswell restored! +70 coins, +3 shards, +110 XP, daytime desert damage +3.":
                    "Glasswell restored! +70 coins, +3 shards, +110 XP, nighttime desert speed +0.5.");
                return true;
            }
            return true;
        }
        private void TickGlassLoom(float dt)
        {
            if(glassLoomStage==0||glassLoomStage==3)return;
            if(save.room!=6||Dying||practicing)
            {
                ResetGlassLoom();revision++;return;
            }
            // A successful daytime alignment creates a REAL exposed light
            // corridor across the plateau until nightfall. The player can
            // avoid it using the two walkable side lanes rather than
            // suffering arbitrary map-wide damage. Each contact is a
            // delayed, dodgeable glass-warning, never an instant hit.
            if(glassLoomStage==2)
            {
                if(Night)
                    hazards.RemoveAll(h=>h.glassLoomOwned);
                else if(GlassLoomBeamActive)
                {
                    glassLoomHeatClock=Math.Max(0f,glassLoomHeatClock-dt);
                    Point foot=Position;
                    if(glassLoomHeatClock<=0f&&
                        foot.x>=61.2f&&foot.x<=75.8f&&
                        Math.Abs(foot.y-50.5f)<.48f&&CanStand(foot))
                    {
                        glassLoomHeatClock=3.35f;
                        hazards.Add(new Hazard{
                            position=foot,radius=.8f,timer=1.35f,
                            damage=12f,kind="glass",glassLoomOwned=true});
                        effects.Add(new Effect("number",foot,.7f,
                            L("强光！离开中轴","SUNBEAM · MOVE OFF AXIS")));
                        Notify("归光束正照亮平台中轴！侧边安全地面可以绕行，夜间光束会自然消散。",
                            "The focused beam exposes the center lane. Use real side ground to pass; it fades at true nightfall.");
                        revision++;
                    }
                }
            }
            glassLoomTime-=dt;
            if(glassLoomTime<=0f)
            {
                ResetGlassLoom();revision++;
                Notify("光线已耗散。旧阵可以在白昼重新校准，无需额外材料。",
                    "The collected light faded. Retry the circuit in daylight at no material cost.");
            }
        }
    }
}
