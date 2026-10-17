using System;
using System.Collections.Generic;
namespace Starfall
{
    // A distinct Frostchime observatory story: physical sky lenses, a
    // world-space elite encounter, and a lasting cold-weather refuge.
    // All active enemies and warning state are transient; existing saves only
    // receive ordinary, one-time completion flags.
    public sealed partial class GameCore
    {
        public int SkywatchRoom { get; private set; }
        public float SkywatchTime { get; private set; }
        private float skywatchPulseTime;
        private int skywatchPulseCycle;
        private bool skywatchGroundingUsed;
        public float SkywatchGroundingTime { get; private set; }
        public bool SkywatchGroundingAvailable
        {
            get { return SkywatchRoom == 7 && !skywatchGroundingUsed &&
                save.energy >= 25 && !Dying && enemies.Exists(IsSkywatchEnemy); }
        }
        public bool IsSkywatchEnemy(Enemy enemy)
        { return enemy != null && enemy.id == 89701; }
        private void ResetSkywatch()
        {
            enemies.RemoveAll(IsSkywatchEnemy);
            projectiles.RemoveAll(p => p.skywatchOwned);
            effects.RemoveAll(e => e.skywatchOwned);
            hazards.RemoveAll(h => h.skywatchOwned);
            SkywatchRoom = 0; SkywatchTime = 0;
            skywatchPulseTime = 0; skywatchPulseCycle = 0;
            skywatchGroundingUsed = false; SkywatchGroundingTime = 0;
        }
        private bool SkywatchReady()
        {
            return Has("skywatch_started") &&
                Has("skywatch_prism_1") && Has("skywatch_prism_2") &&
                Has("skywatch_prism_3");
        }
        public bool SkywatchEchoRefuge
        {
            get
            {
                if(save.room!=7 || !Has("skywatch_afterglow_complete") ||
                    !SafeTravel || SkywatchRoom!=0) return false;
                for(int i=1;i<=3;i++)
                    if((Position-ObjectPoint(7,"skyprism"+i,
                        new Point(-100,-100))).Length<2.6f) return true;
                return false;
            }
        }
        public bool SkywatchRefuge
        {
            get { return save.room == 7 && Has("skywatch_complete") &&
                SkywatchRoom == 0 && SafeTravel &&
                (Position - ObjectPoint(7,"skyobservatory",
                    new Point(-100,-100))).Length < 3.6f; }
        }
        private bool TalkSkywatch(WorldObject o)
        {
            string kind = o.kind;
            if (kind == "skywatcher")
            {
                if (save.room != 0) return true;
                if (!Has("mountain_report"))
                {
                    Dialog(o.name,o.en,
                        "霜钟高原的古观星台仍在夜里发出警报。先完成高原的原始记录，再调查那场没有结束的星陨预报。",
                        "The old Frostchime observatory still sounds its skyfall alarm. Finish the mountain report before investigating the forecast that never ended.",
                        C("我会再来","I'll return","close"));
                    return true;
                }
                if (Has("skywatch_complete"))
                {
                    if(!Has("skywatch_afterglow_complete"))
                    {
                        Dialog(o.name,o.en,
                            "光谱已经公开，但北、东、南三座棱镜还留下最后的旅人余辉。若能找回并归档，就能在高原点亮三座公开的避寒信标。",
                            "The spectrum is public, but three lenses still contain lost travelers' afterglows. Recover and archive them to activate three public highland warming beacons.",
                            C("追踪余辉调查","Track afterglows","skywatch_afterglow_track"),
                            C("稍后回去","Later","close"));
                    }
                    else
                        Dialog(o.name,o.en,
                            "三枚余辉已公开归档，三座棱镜与观星台都能在寒夜帮助远行者。",
                            "All afterglows are now public. The observatory and three lens sites shelter highland travelers.",
                            C("谢谢","Thank you","close"));
                    return true;
                }
                if (Has("skywatch_defeated"))
                {
                    Dialog(o.name,o.en,
                        "你击败了旧观测守卫。它把暴风雪判成了星陨，把一个个求救信号都当成噪声。现在把原始光谱交给大家吧。",
                        "You silenced the old skywatch guardian. It mistook blizzards for meteor strikes and treated distress calls as noise. Let's publish the original spectrum.",
                        C("公开观测记录","Publish the spectrum","skywatch_publish"),
                        C("稍后再说","Not yet","close"));
                    return true;
                }
                if (!Has("skywatch_started"))
                {
                    Dialog(o.name,o.en,
                        "我是观星师伊澜。霜钟高原的三枚棱镜记录了星陨前最后一夜：北方的霜、东方的光、南方的炉火。按顺序校准它们，再去观星台关停仍在追捕旅人的旧守卫。你愿意帮忙吗？",
                        "I'm Yilan, an astronomer. Three Frostchime prisms remember the last night before Starfall: frost in the north, light in the east, hearthfire in the south. Calibrate them in order, then disable the obsolete guardian still hunting travelers at the observatory.",
                        C("接受夜空观测委托","Accept the skywatch commission","skywatch_accept"),
                        C("暂时不去","Not yet","close"));
                    return true;
                }
                Dialog(o.name,o.en,
                    "先校准北、东、南三座棱镜，最后去东南侧的观星台。守卫会让地面出现延迟的寒光落点，请观察预警移动。",
                    "Calibrate the north, east and south prisms, then visit the southeastern observatory. Its guardian warns of delayed frost strikes on the ground; move out of their circles.",
                    C("追踪观测委托","Track skywatch","skywatch_track"),
                    C("继续探索","Keep exploring","close"));
                return true;
            }
            if (save.room != 7) return kind == "skyobservatory" || kind.StartsWith("skyprism");
            if (kind.StartsWith("skyprism"))
            {
                int lens;
                if (!int.TryParse(kind.Substring("skyprism".Length),out lens) ||
                    lens < 1 || lens > 3) return true;
                if (!Has("skywatch_started"))
                {
                    Notify("棱镜仍然锁着。去余烬镇找观星师伊澜了解古观星台。",
                        "The prism is locked. Speak with Yilan in Emberfall.");
                    return true;
                }
                if (Has("skywatch_prism_"+lens))
                {
                    if(Has("skywatch_complete") &&
                        !Has("skywatch_afterglow_"+lens))
                    {
                        Dialog(o.name,o.en,
                            new[]{"北棱镜还保存着救援者在暴雪中的脚步。","东棱镜揭露误报的星光来自仪器自身。","南棱镜记着守夜者用最后的炉火为旅人引路。"}[lens-1],
                            new[]{"The northern lens remembers rescuers in the blizzard.","The eastern lens reveals the false skyfall was reflected light.","The southern lens recalls the final fire guiding lost travelers."}[lens-1],
                            C("收集余辉记录","Recover afterglow","skywatch_afterglow_"+lens),
                            C("稍后查看","Later","close"));
                        return true;
                    }
                    Dialog(o.name,o.en,
                        "这座棱镜已经对准夜空，读数保存在手帐里。",
                        "This prism is aligned; its reading is safely recorded.",
                        C("查看下一个目标","Next objective","skywatch_track"));
                    return true;
                }
                if (lens > 1 && !Has("skywatch_prism_"+(lens-1)))
                {
                    Dialog(o.name,o.en,
                        "缺少前一枚棱镜的基准，仪器无法对焦。请从北侧开始。",
                        "The previous prism's reference is missing. Begin in the north.",
                        C("先找前一枚","Find the previous prism","close"));
                    return true;
                }
                string[] clue = {
                    "北棱镜的冰面刻着：星陨那夜，先有霜降，再有光。",
                    "东棱镜记录到一道没有温度的长光，穿过了冻结的天幕。",
                    "南棱镜面向营地：直到最后，还有人守着炉火等待归人。"
                };
                string[] clueEn = {
                    "The northern lens reads: frost came before the light on the night of Starfall.",
                    "The eastern lens recorded a cold shaft of light crossing the frozen sky.",
                    "The southern lens points to a camp where someone kept the hearth burning for those not yet home."
                };
                Dialog(o.name,o.en,clue[lens-1],clueEn[lens-1],
                    C("校准：霜","Align: frost","skywatch_align_"+lens+"_1"),
                    C("校准：光","Align: light","skywatch_align_"+lens+"_2"),
                    C("校准：炉火","Align: hearth","skywatch_align_"+lens+"_3"),
                    C("暂时离开","Leave","close"));
                return true;
            }
            if (kind != "skyobservatory") return false;
            if (!SkywatchReady())
            {
                Dialog(o.name,o.en,
                    "古观星台缺少三枚棱镜的完整光谱。请先依次读取北、东、南的记录。",
                    "The observatory needs all three readings. Align north, east and south first.",
                    C("返回寻找棱镜","Find the prisms","close"));
                return true;
            }
            if(Has("skywatch_complete"))
            {
                if(Has("skywatch_afterglow_complete"))
                {
                    Dialog(o.name,o.en,
                        "三道余辉已公开，三座高原棱镜成为永久避寒信标。",
                        "The afterglows are public; all three prisms now warm travelers.",
                        C("继续探索","Continue","close"));
                    return true;
                }
                bool all=Has("skywatch_afterglow_1") &&
                    Has("skywatch_afterglow_2") && Has("skywatch_afterglow_3");
                Dialog(o.name,o.en,
                    all?"三道余辉已经回收。归档后让三座棱镜成为永久避寒信标。":
                        "重新走访北、东、南三座棱镜，回收最后的旅人记录。",
                    all?"All afterglows recovered. Archive them to restore permanent warming beacons.":
                        "Revisit the north, east and south lenses for their last traveler records.",
                    C(all?"归档三道余辉":"稍后回访",
                        all?"Archive afterglows":"Return later",
                        all?"skywatch_afterglow_publish":"close"));
                return true;
            }
            if (Has("skywatch_defeated"))
            {
                Dialog(o.name,o.en,
                    "旧守卫已经安静。带着这份原始光谱回镇里给伊澜，让居民知道真正的气候预报。",
                    "The obsolete guardian is quiet. Take its spectrum back to Yilan so everyone can read the true forecast.",
                    C("返回城镇","Return to town","close"));
                return true;
            }
            if (SkywatchRoom == 7)
            {
                var commands=new List<Choice>();
                commands.Add(C("继续战斗","Continue fighting","close"));
                if (SkywatchGroundingAvailable)
                    commands.Add(C("手动接地 · 25耐力","Ground the prism · 25 energy",
                        "skywatch_ground"));
                commands.Add(C("安全撤出","Withdraw","skywatch_withdraw"));
                Dialog(o.name,o.en,
                    "守卫的寒光正在锁定你的脚下。注意落点，绕过危险区域。接地需要25点耐力，每场只能使用一次。",
                    "The guardian's frost beams target your feet. Dodge the warning circles. Grounding costs 25 energy and can be used once per fight.",
                    commands.ToArray());
                return true;
            }
            Dialog(o.name,o.en,
                "观星台仍将来访者标作闯入者。启动后会出现古代重甲守卫与可躲避的延迟冰光冲击。打败它才能取出原始气候预报，失败可免费重试。",
                "The observatory still classifies travelers as intruders. Its armored guardian unleashes delayed, avoidable frost impacts. Defeat it to recover the original climate forecast; retries cost nothing.",
                C("启动观星台","Activate observatory","skywatch_start"),
                C("稍后准备","Prepare first","close"));
            return true;
        }
        private bool ChooseSkywatch(string action)
        {
            if (action == "skywatch_afterglow_track")
            {
                if (Has("skywatch_complete") && !Has("skywatch_afterglow_complete"))
                    TrackQuest("skywatch_afterglow");
                return true;
            }
            if (action == "skywatch_track")
            {
                if (Has("skywatch_started")) TrackQuest("skywatch");
                return true;
            }
            if (action == "skywatch_accept")
            {
                if (save.room == 0 && Has("mountain_report") &&
                    !Has("skywatch_started") && !Dying &&
                    (Position - ObjectPoint(0,"skywatcher",new Point(-100,-100))).Length < 2.2f)
                {
                    Flag("skywatch_started"); TrackQuest("skywatch");
                    Notify("新委托：霜钟夜空。按北、东、南顺序校准三枚棱镜。",
                        "New quest: Frostchime Skywatch. Calibrate north, east and south.");
                    revision++;
                }
                return true;
            }
            if (action.StartsWith("skywatch_align_"))
            {
                string[] parts=action.Split('_');
                int prism,note;
                if (parts.Length != 4 ||
                    !int.TryParse(parts[2],out prism) ||
                    !int.TryParse(parts[3],out note) ||
                    prism<1 || prism>3 || note<1 || note>3 ||
                    save.room!=7 || Dying || !Has("skywatch_started") ||
                    Has("skywatch_prism_"+prism) ||
                    (prism>1 && !Has("skywatch_prism_"+(prism-1)))) return true;
                Point site=ObjectPoint(7,"skyprism"+prism,new Point(-100,-100));
                if ((Position-site).Length>2.2f || SkywatchRoom!=0) return true;
                if (note!=prism)
                {
                    if (save.energy<8)
                    { Notify("耐力不足，无法再次调焦。","Need 8 energy to realign the lens."); return true; }
                    save.energy-=8; revision++; sound="deny";
                    Notify("错误音序使棱镜失焦：耐力 -8。请重读现场线索。",
                        "Wrong alignment costs 8 energy. Read the site clue and retry.");
                    return true;
                }
                Flag("skywatch_prism_"+prism); sound="reward"; revision++;
                effects.Add(new Effect("discover",site,.65f));
                RecordMoment(new[] {
                    "北棱镜封存了星陨前的第一场霜降。",
                    "东棱镜找到了没有温度的光束轨迹。",
                    "南棱镜留下营地最后的炉火记录。"
                }[prism-1],new[] {
                    "Northern prism: the first frost before Starfall.",
                    "Eastern prism: the path of light without warmth.",
                    "Southern prism: a hearth still burning for late travelers."
                }[prism-1]);
                Notify(prism==3?"三枚棱镜已校准！前往东南的古观星台。":
                    "读数封存！继续寻找下一枚棱镜。",
                    prism==3?"All three aligned! Head to the southeastern observatory.":
                    "Reading secured! Find the next prism.");
                return true;
            }
            if (action.StartsWith("skywatch_afterglow_") &&
                action != "skywatch_afterglow_publish")
            {
                int lens;
                if(!int.TryParse(action.Substring("skywatch_afterglow_".Length),out lens) ||
                    lens<1 || lens>3 || save.room!=7 || SkywatchRoom!=0 || Dying ||
                    !Has("skywatch_complete") || Has("skywatch_afterglow_"+lens) ||
                    (Position-ObjectPoint(7,"skyprism"+lens,
                        new Point(-100,-100))).Length>2.2f) return true;
                Flag("skywatch_afterglow_"+lens); revision++;sound="discover";
                effects.Add(new Effect("discover",Position,.65f));
                RecordMoment("霜钟棱镜余辉 "+lens+"/3：找回失散旅人的真实记录。",
                    "Frostchime afterglow "+lens+"/3: a lost traveler's record recovered.");
                Notify("余辉记录已回收。调查其他棱镜并返回观星台。",
                    "Afterglow recovered. Investigate remaining prisms and return.");
                return true;
            }
            if (action == "skywatch_afterglow_publish")
            {
                if(save.room!=7 || SkywatchRoom!=0 || Dying ||
                    !Has("skywatch_complete") || Has("skywatch_afterglow_complete") ||
                    !Has("skywatch_afterglow_1") || !Has("skywatch_afterglow_2") ||
                    !Has("skywatch_afterglow_3") ||
                    (Position-ObjectPoint(7,"skyobservatory",
                        new Point(-100,-100))).Length>2.2f) return true;
                Flag("skywatch_afterglow_complete");
                save.coins+=45;save.shards+=2;AddXp(60);
                revision++;sound="reward";
                RecordMoment("三座棱镜成为公开的高原避寒信标。",
                    "All three prisms became public warming beacons.");
                Notify("余辉归档！45硬币、2碎片、60经验；三座棱镜永久避寒。",
                    "Afterglows archived! +45 coins, +2 shards, +60 XP; three permanent warm refuges.");
                return true;
            }
            if (action == "skywatch_ground")
            {
                if (!SkywatchGroundingAvailable || save.room != 7 ||
                    (Position - ObjectPoint(7, "skyobservatory",
                        new Point(-100, -100))).Length > 2.2f) return true;
                Enemy guardian = enemies.Find(IsSkywatchEnemy);
                if (guardian == null || guardian.hp <= 0) return true;
                save.energy -= 25;
                skywatchGroundingUsed = true;
                SkywatchGroundingTime = 8f;
                skywatchPulseTime = Math.Max(skywatchPulseTime, 8f);
                hazards.RemoveAll(h => h.skywatchOwned);
                guardian.stun = Math.Max(guardian.stun, 1.8f);
                guardian.state = 0; guardian.releaseTime = 0;
                sound = "guard"; revision++;
                effects.Add(new Effect("discover",
                    ObjectPoint(7,"skyobservatory",Position),.7f));
                Notify("棱镜接地！守卫暂时被打断，冰光预警清除8秒。你仍需亲自击败守卫。",
                    "Prism grounded! Guardian interrupted and frost strikes suppressed for 8 seconds. You must still defeat it.");
                return true;
            }
            if (action == "skywatch_withdraw")
            {
                if (save.room==7 && SkywatchRoom==7)
                {
                    ResetSkywatch(); revision++;
                    Notify("已退出观星试炼，三枚棱镜记录保留。",
                        "Observatory withdrawn. All three prism readings remain.");
                }
                return true;
            }
            if (action == "skywatch_start")
            {
                if (save.room!=7 || !SkywatchReady() ||
                    Has("skywatch_defeated") || SkywatchRoom!=0 ||
                    Dying || practicing || !SafeTravel ||
                    (Position-ObjectPoint(7,"skyobservatory",new Point(-100,-100))).Length>2.2f)
                    return true;
                Point spawn=new Point(27.5f,15.5f);
                if (!CanStand(spawn) || NavigationPath(spawn).Count==0 ||
                    (spawn-Position).Length<2.2f)
                { Notify("观星台附近没有可用的守卫空间。","No valid guardian arena is available."); return true; }
                SkywatchRoom=7; SkywatchTime=85;
                skywatchPulseTime=2.6f; skywatchPulseCycle=0;
                skywatchGroundingUsed=false; SkywatchGroundingTime=0;
                Enemy guardian=new Enemy(89701,2,spawn.x,spawn.y,240,true);
                guardian.timer=1.05f; enemies.Add(guardian);
                TrackQuest("skywatch"); sound="windup"; revision++;
                effects.Add(new Effect("discover",spawn,.65f));
                Notify("古观星守卫已启动！看清冰光落点，利用移动和闪避突破。",
                    "Skywatch guardian activated! Read the frost warnings and dodge.");
                return true;
            }
            if (action == "skywatch_publish")
            {
                if (save.room!=0 || !Has("skywatch_defeated") ||
                    !Has("skywatch_started") || Has("skywatch_complete") ||
                    Dying ||
                    (Position-ObjectPoint(0,"skywatcher",new Point(-100,-100))).Length>2.2f)
                    return true;
                Flag("skywatch_complete"); save.coins+=95; save.shards+=3;
                save.skillPoints++; AddXp(100); revision++; sound="reward";
                RecordMoment("霜钟高原的原始天气光谱已公开，古观星台不再向旅人隐藏冻雨。",
                    "Frostchime's original weather spectrum was published for every traveler.");
                Notify("夜空观测完成！获得95硬币、3碎片、1天赋点、100经验。观星台旁开启避寒区域。",
                    "Skywatch complete! +95 coins, +3 shards, +1 talent, +100 XP. The observatory now shelters travelers.");
                return true;
            }
            return false;
        }
        private void TickSkywatch(float dt)
        {
            if (SkywatchRoom!=7) return;
            if (save.room!=7 || Dying || practicing)
            { ResetSkywatch(); revision++; return; }
            Enemy guardian=enemies.Find(IsSkywatchEnemy);
            if (guardian==null)
            {
                ResetSkywatch();
                if (!Has("skywatch_defeated"))
                {
                    Flag("skywatch_defeated"); revision++; sound="reward";
                    Notify("古观星守卫已停机！回余烬镇向伊澜公开原始光谱。",
                        "The skywatch guardian is down! Return to Yilan with the true spectrum.");
                }
                return;
            }
            SkywatchTime-=dt;
            if (SkywatchGroundingTime>0)
                SkywatchGroundingTime=Math.Max(0,SkywatchGroundingTime-dt);
            if (SkywatchTime<=0)
            {
                ResetSkywatch(); revision++;
                Notify("观星台重新封闭。棱镜读数保留，可免费重试。",
                    "The observatory resealed. Prism readings remain; retry freely.");
                return;
            }
            skywatchPulseTime-=dt;
            if (skywatchPulseTime>0) return;
            Point target=Position;
            Point[] escapes={new Point(2.0f,0),new Point(-2.0f,0),
                new Point(0,2.0f),new Point(0,-2.0f)};
            bool exit=false;
            foreach(Point delta in escapes)
                if (CanStand(target+delta) && ClearAttackPath(target,target+delta))
                { exit=true; break; }
            if(exit)
            {
                hazards.Add(new Hazard { position=target,radius=1.0f,
                    timer=1.3f,damage=guardian.BossPhase==3?20:14,
                    kind="glass",skywatchOwned=true });
                // Half-health adds an offset blast without closing all exits.
                if(guardian.BossPhase>=2)
                {
                    Point second=target+new Point(skywatchPulseCycle%2==0?2.6f:-2.6f,0);
                    if (CanStand(second))
                        hazards.Add(new Hazard { position=second,radius=.9f,
                            timer=1.7f,damage=12,kind="glass",skywatchOwned=true });
                }
                sound="windup";
            }
            skywatchPulseCycle++;
            skywatchPulseTime=guardian.BossPhase==3?3.0f:4.2f;
        }
    }
}
