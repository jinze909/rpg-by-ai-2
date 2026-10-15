using System;

namespace Starfall
{
    // An original post-citadel public-infrastructure story. The player fights
    // through three distinct circuits in one authored map, rather than
    // retuning the existing cross-region Counterpoint or rescuing civilians.
    public sealed partial class GameCore
    {
        private int commonsRelayRoom, commonsRelayStep;
        private float commonsRelayTime, commonsRelayPulseTime;
        public int CommonsRelayRoom { get { return commonsRelayRoom; } }
        public int CommonsRelayStep { get { return commonsRelayStep; } }
        public float CommonsRelayTime { get { return commonsRelayTime; } }
        public bool IsCommonsRelayEnemy(Enemy e)
        { return e != null && e.id >= 89500 && e.id < 89600; }

        private Point CommonsRelayPoint(int step)
        { return ObjectPoint(4, "commonsrelay" + step, new Point(-100, -100)); }
        private void ResetCommonsRelayTrial()
        {
            enemies.RemoveAll(IsCommonsRelayEnemy);
            projectiles.RemoveAll(p => p.commonsRelayOwned);
            effects.RemoveAll(e => e.commonsRelayOwned);
            hazards.RemoveAll(h => h.commonsRelayOwned);
            commonsRelayRoom = commonsRelayStep = 0;
            commonsRelayTime = commonsRelayPulseTime = 0;
        }
        private void TickCommonsRelayTrial(float dt)
        {
            if (commonsRelayRoom != 4) return;
            if (save.room != 4 || Dying || practicing)
            { ResetCommonsRelayTrial(); revision++; return; }
            commonsRelayTime -= dt;
            // Medical-circuit security fires telegraphed floor-surge pairs.
            // Normal movement and dodge contact can avoid both warnings.
            if (commonsRelayStep == 3 && enemies.Exists(IsCommonsRelayEnemy))
            {
                commonsRelayPulseTime -= dt;
                if (commonsRelayPulseTime <= 0)
                {
                    Point at = Position;
                    Point[] exits = { new Point(1.9f,0), new Point(-1.9f,0),
                        new Point(0,1.9f), new Point(0,-1.9f) };
                    bool canEscape = false;
                    foreach (Point direction in exits)
                        if (CanStand(at + direction) && ClearAttackPath(at, at + direction))
                        { canEscape = true; break; }
                    if (canEscape)
                    {
                        hazards.Add(new Hazard {position = at, timer=1.25f,
                            radius=1.08f,damage=15,kind="glass",commonsRelayOwned=true});
                        Point other = at + new Point(2.25f,0);
                        if (CanStand(other))
                            hazards.Add(new Hazard {position=other,timer=1.65f,
                                radius=1.05f,damage=13,kind="glass",commonsRelayOwned=true});
                    }
                    commonsRelayPulseTime = 5.4f;
                }
            }
            else hazards.RemoveAll(h => h.commonsRelayOwned);
            if (commonsRelayTime <= 0)
            {
                ResetCommonsRelayTrial(); revision++;
                Notify("公共线路重新被旧守卫锁定。已保留证词，回第一座信标可以重试。",
                    "Old security reclaimed the circuits. Your testimony is safe; retry at the first console.");
            }
        }
        private bool TalkCommonsRelay(WorldObject o)
        {
            if (o.kind == "commonswitness")
            {
                if (save.room != 0) return true;
                if (!Has("boss_4"))
                {
                    Dialog(o.name, o.en,
                        "堡垒的旧调度日志不许外界抄录。先弄清三枚星核与屏障究竟发生了什么，等守卫不再阻拦，我们才能取得公共供给密钥。",
                        "The citadel still seals its allocation records. First confront the fourth starcore guardian. Only then can we recover the public supply key.",
                        C("我会调查堡垒", "I'll investigate the citadel", "close"));
                }
                else if (Has("commons_relay_complete"))
                    Dialog(o.name, o.en,
                        "粮路、医护和撤离灯线现在由城镇共同见证。你没有把堡垒拆成废铁，而是让每一个依靠它生活的人都有权知道线路何时会关闭。",
                        "Food, care and evacuation lamps now remain in public view. You did not destroy the citadel; you made its circuits answerable to everyone who depends on them.",
                        C("保管好记录", "Keep the public record", "close"));
                else if (Has("commons_relay_sealed"))
                    Dialog(o.name, o.en,
                        "三道线路都已重新接通。最后一步不能再留给沉默的调度机——请把民用控制密钥交给所有人共同保管。",
                        "All three circuits are reconnected. The last step cannot belong to an unanswerable machine. Let the community hold the key together.",
                        C("公开民用密钥", "Publish the civilian access key", "commons_publish"),
                        C("稍后再来", "Later", "close"));
                else if (!Has("commons_relay_started"))
                    Dialog(o.name, o.en,
                        "我是艾拉，替城镇保存堡垒证词。守卫已经倒下，自动防御却仍将粮路、医护和撤离灯线视为私人特权。去堡垒顺序接通三座公共信标，每重连一段，都要挡住旧协议派来的真实守卫。最终记录必须带回镇里公开。",
                        "I'm Ayla, keeper of testimony. Even after the guardian fell, the citadel's obsolete security treats food routes, treatment and evacuation lamps as private privileges. Reconnect three consoles in order and survive their actual defenders. Bring the final key back to town for public oversight.",
                        C("接下公共信标委托", "Accept the Commons Relay commission", "commons_accept"),
                        C("先看看周围", "Not yet", "close"));
                else
                    Dialog(o.name, o.en,
                        "旧线还有三个断点。先从堡垒西侧的粮道信标开始，再到东侧的医护线路，最后去东南的撤离灯线。已结束的试次不会夺走任务线索，失败可以重新开始。",
                        "Three broken circuits remain. Begin at the western food relay, cross to the eastern infirmary circuit, then reach the southeastern evacuation lamps. Failed attempts do not lose your lead.",
                        C("追踪委托", "Track the commission", "commons_track"),
                        C("我继续行动", "Continue", "close"));
                return true;
            }
            if (!o.kind.StartsWith("commonsrelay")) return false;
            int step;
            if (!int.TryParse(o.kind.Substring("commonsrelay".Length), out step) ||
                step < 1 || step > 3 || save.room != 4) return true;
            if (!Has("commons_relay_started"))
            {
                Notify("封闭的民用控制台。镇上的证词保管员也许能解释旧权限。",
                    "A locked civilian console. Ayla in town may explain the old permissions.");
                return true;
            }
            if (Has("commons_relay_sealed"))
            {
                Dialog(o.name, o.en, "这条民用线路已永久开放，公开授权无需重复领取。",
                    "This civilian circuit is open. The public key cannot be claimed twice.",
                    C("查看灯线", "Inspect the open circuit", "close"));
                return true;
            }
            if (commonsRelayRoom == 4 && commonsRelayStep != step)
            {
                Dialog(o.name, o.en, "按顺序完成粮路、医护、撤离灯线。旧协议仍在阻断当前断点。",
                    "Restore food, care and evacuation in order. The previous circuit is still contested.",
                    C("回到当前信标", "Return to the current console", "close"));
                return true;
            }
            if (commonsRelayRoom == 0 && step != 1)
            {
                Dialog(o.name, o.en, "先从西侧粮道信标取得公共访问令牌，才能解锁后续线路。",
                    "Start at the western food relay to retrieve the public access token.",
                    C("找到第一座信标", "Find the first console", "close"));
                return true;
            }
            if (commonsRelayRoom == 4 && enemies.Exists(IsCommonsRelayEnemy))
            {
                Dialog(o.name, o.en, "旧防御守卫还在执行封闭命令。击退它们，再回来连接下一段线路。",
                    "Obsolete sentinels are still enforcing the lockdown. Defeat them, then return to the console.",
                    C("继续战斗", "Keep fighting", "close"),
                    C("终止本次重连", "Withdraw from reconnection", "commons_withdraw"));
                return true;
            }
            Dialog(o.name, o.en,
                step == 1 ? "粮食原本沿这条线路送出，却被自动协议优先分配给城内粮仓。重新接通后，旧守卫将试图关闭它。" :
                step == 2 ? "医护仓的能量仍按照九年前的病房名单分配。那些被排除的人，也需要活下去。" :
                    "最后是撤离灯线。它不应该只在城门内发光。",
                step == 1 ? "Food was routed to the inner citadel stores first. Opening the road will wake its defense protocol." :
                step == 2 ? "Medical energy is still reserved for a nine-year-old patient list. The excluded deserve care as well." :
                    "The last circuit controls the evacuation lamps. Their light must reach beyond the inner gate.",
                C(step == 3 ? "封存民用密钥" : "重新接通线路",
                  step == 3 ? "Seal the public access key" : "Reconnect the circuit",
                  "commons_activate_" + step),
                C("稍后回来", "Return later", "close"),
                C("终止本次重连", "Withdraw", "commons_withdraw"));
            return true;
        }
        private bool ChooseCommonsRelay(string action)
        {
            if (action == "commons_accept")
            {
                if (save.room == 0 && Has("boss_4") &&
                    !Has("commons_relay_started") &&
                    (Position - ObjectPoint(0, "commonswitness", new Point(-100, -100))).Length <= 2.2f)
                {
                    Flag("commons_relay_started"); TrackQuest("commons_relay");
                    Notify("新委托：堡垒公共信标。前往堡垒西侧重连粮道线路。",
                        "New quest: Commons Relay. Restore the western citadel food circuit first.");
                }
                return true;
            }
            if (action == "commons_track")
            { if (Has("commons_relay_started")) TrackQuest("commons_relay"); return true; }
            if (action == "commons_withdraw")
            {
                if (commonsRelayRoom == 4 && save.room == 4 &&
                    (Position - CommonsRelayPoint(commonsRelayStep)).Length <= 2.2f)
                {
                    ResetCommonsRelayTrial(); revision++;
                    Notify("已安全撤销本轮调度。重连进度可以重新开始，既有故事与奖励不丢失。",
                        "Circuit run withdrawn safely. Restart from the first console; story progress and rewards remain.");
                }
                return true;
            }
            if (action == "commons_publish")
            {
                if (save.room == 0 && Has("commons_relay_sealed") &&
                    !Has("commons_relay_complete") &&
                    (Position - ObjectPoint(0, "commonswitness", new Point(-100, -100))).Length <= 2.2f)
                {
                    Flag("commons_relay_complete"); save.coins += 80;
                    save.skillPoints++; AddXp(90); sound = "reward"; revision++;
                    RecordMoment("艾拉公开了堡垒民用密钥。通往粮食、医护与撤离灯线的路不再属于少数人。",
                        "Ayla published the citadel's civilian access key. Food, care and evacuation routes no longer belong to a few.");
                    Notify("公共授权已发布！+80硬币、1技能点、90经验，永久耐力恢复+2/秒。",
                        "Public key published! +80 coins, 1 talent, 90 XP and permanent +2 energy/sec.");
                }
                return true;
            }
            if (!action.StartsWith("commons_activate_")) return false;
            int step;
            if (!int.TryParse(action.Substring("commons_activate_".Length), out step) ||
                step < 1 || step > 3 || save.room != 4 || !Has("boss_4") ||
                !Has("commons_relay_started") || Has("commons_relay_sealed") ||
                Dying || practicing || racing || reliefMissionRoom != 0 ||
                RootHarmonyRoom != 0 || atlasEchoRoom != 0 ||
                roadTrialRoom != 0 || wayfarerEncounterRoom != 0 ||
                (Position - CommonsRelayPoint(step)).Length > 2.2f) return true;
            if (commonsRelayRoom == 0)
            {
                if (step != 1 || !SafeTravel) return true;
                commonsRelayRoom = 4; commonsRelayStep = 1;
                commonsRelayTime = 110;
            }
            if (commonsRelayStep != step || enemies.Exists(IsCommonsRelayEnemy)) return true;
            if (step < 3)
            {
                Point console = CommonsRelayPoint(step);
                // Fixed authored walkable spaces; use regular collision-safe
                // placement, not free enemy teleportation into walls.
                Point[] positions = step == 1 ?
                    new[] { new Point(11.5f, 8.5f), new Point(9.5f, 11.5f) } :
                    new[] { new Point(29.5f, 9.5f), new Point(24.5f, 11.5f),
                            new Point(25.5f, 6.5f) };
                for (int i = 0; i < positions.Length; i++)
                {
                    enemies.Add(CreatePlacedEnemy(89500 + step * 10 + i,
                        i == positions.Length - 1 ? 2 : 0, positions[i],
                        45 + step * 16));
                }
                effects.Add(new Effect("discover", console, .6f));
                commonsRelayStep = step + 1;
                if (commonsRelayStep == 3) commonsRelayPulseTime = 3.2f;
                revision++; sound = "windup";
                Notify(step == 1 ? "粮路已接通！击退守卫，前往东侧医护信标。" :
                    "医护线路已恢复！击退第二道守卫，封存东南的撤离灯线。",
                    step == 1 ? "Food circuit restored! Defeat security, then reach the eastern medical console." :
                    "Medical circuit restored! Defeat the second wave, then seal the southeastern evacuation lamps.");
                return true;
            }
            ResetCommonsRelayTrial(); Flag("commons_relay_sealed");
            save.coins += 60; save.shards += 3; AddXp(75);
            sound = "reward"; revision++;
            effects.Add(new Effect("discover", CommonsRelayPoint(3), .8f));
            RecordMoment("堡垒三道公共线路重连成功。城市内外可以共享灯光与供给。等待公开授权。",
                "The citadel's three civilian circuits are reconnected. Only public authorization remains.");
            Notify("三条民用线路重连！获得60硬币、3碎片、75经验。回镇里向艾拉公开记录。",
                "Three civilian circuits live! +60 coins, +3 shards, +75 XP. Return to Ayla to publish.");
            return true;
        }
    }
}
