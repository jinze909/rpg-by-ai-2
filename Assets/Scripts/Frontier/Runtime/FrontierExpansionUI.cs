using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int journalTab, relicSlot;
        private void DrawRay(Point from, Point to, Color color)
        {
            Point a = ScreenPoint(from), b = ScreenPoint(to); int count = Math.Max(1, (int)((b - a).Length / 5));
            for (int i = 0; i <= count; i++) { Point p = a + (b - a) * (i / (float)count); Box(p.x - 2, p.y - 2, 4, 4, color); }
        }
        private void DrawCompanionActor()
        {
            Point p = ScreenPoint(Game.companionPosition); int id = Game.save.companion;
            if (companionSheets == null || id < 0 || id >= companionSheets.Length || companionSheets[id] == null)
            { DrawNpc("companion", Game.companionPosition, id + 8); return; }
            ActorAction action = Game.companionAction;
            Point direction = action == null ? Game.companionAim : action.direction;
            bool north = direction.y < -Math.Abs(direction.x), south = direction.y > Math.Abs(direction.x);
            int frame = north ? 8 : 0;
            if (action != null)
                frame = north ? (action.released ? 7 : 6) : south ? (action.released ? 10 : 9) : action.released && action.kind == "command" && id != 1 ? 5 : action.released ? 4 : 3;
            Box(p.x - 14, p.y - 3, 28, 6, new Color(.025f, .04f, .07f, .32f));
            bool overlaps = Math.Abs(Game.companionPosition.x - Game.save.x) < .85f && Game.save.y <= Game.companionPosition.y && Game.save.y > Game.companionPosition.y - 1.8f;
            Color tint = overlaps ? new Color(1, 1, 1, .45f) : Color.white;
            if (Game.companionMoving && action == null && companionWalkSheets[id] != null)
            { int row=GameCore.DirectionRow(direction);HumanActor(companionWalkSheets[id],4,3,row*4+GameCore.GaitFrame(Game.companionMoveTime,GameCore.WalkStride),p,.75f,tint,row==0&&direction.x<0,companionWalkSheets[id],row*4,true); }
            else if(south&&action==null&&companionWalkSheets[id]!=null)HumanActor(companionWalkSheets[id],4,3,8,p,.75f,tint,false,companionWalkSheets[id],8,true);
            else HumanActor(companionSheets[id],3,4,frame,p,.75f,tint,!north&&!(south&&action!=null)&&direction.x<-.1f,companionWalkSheets[id],GameCore.DirectionRow(direction)*4,action==null);
            Box(p.x - 13, p.y + 1, 26, 3, mint);
        }
        private void DrawHazardWarnings()
        {
            foreach (Hazard h in Game.hazards)
            {
                Color warning = HazardWarningColor(h);
                Ring(h.position, h.radius, warning); Point p = ScreenPoint(h.position);
                Text(p.x - 25, p.y - 25, 50, 24, h.timer.ToString("0.0"), 13, warning, TextAnchor.MiddleCenter);
            }
        }
        private Color HazardWarningColor(Hazard hazard)
        { return hazard.glasswindOwned ? PixelArt.C("#ffb76c") : hazard.kind == "glass" ? gold : hazard.kind == "bell" ? PixelArt.C("#9bd8f5") : hazard.kind == "root" ? PixelArt.C("#82d9a3") : hazard.kind == "rift" ? PixelArt.C("#b9a1d1") : PixelArt.C("#d59583"); }
        private void DrawRescueSurvivorActor()
        {
            if (!Game.ReliefCivilianVisible) return;
            Point p = ScreenPoint(Game.reliefFollowerPosition);
            bool walking = Game.ReliefEscortActive && Game.reliefFollowerMoving &&
                !Game.save.reducedMotion;
            float cycle = Game.reliefFollowerWalkDistance * 8.2f;
            float step = walking ? (float)Math.Abs(Math.Sin(cycle)) : 0;
            float bob = step * 3.2f;
            float lean = walking ? (float)Math.Sin(cycle) * 2.5f : 0;
            if (Game.ReliefEscortUnderAttack) lean = -4f;
            if (!drawingOutline)
                Box(p.x - 13, p.y - 2, 26, 5, new Color(.025f, .04f, .07f, .32f));
            // Frame 14 is the canonical rescue civilian, not an animation
            // sheet. Procedural footfall bounce and tiny body lean preserve
            // visual identity and follow distance, without changing frames.
            bool justHit = Game.reliefCivilianHitTime > 0;
            Color tint = justHit ? PixelArt.C("#ffe2bd") : Color.white;
            float reaction = justHit && !Game.save.reducedMotion ?
                Game.reliefCivilianHitTime * 7f : 0;
            Actor(npcSheet, 4, 4, 14, new Point(p.x, p.y - bob - reaction), 96,
                tint, Game.reliefFollowerFacingLeft, lean);
        }
        private void DrawExpansionWorld()
        {
            if(Game.BlackoutActive && Game.BlackoutFollowing!=0)
            {
                Point pos=Game.BlackoutFollowerPosition,screen=ScreenPoint(pos);
                DrawNpc("innGuest",pos,Game.BlackoutFollowing+11);
                Ring(pos,.42f,Game.BlackoutFollowerHealth<18?
                    PixelArt.C("#e27a78"):mint,18);
                Bar(screen.x-19,screen.y+9,38,
                    Game.BlackoutFollowerHealth/48f,mint,5);
            }
            if(Game.StormTuning)
            {
                Point pos=ScreenPoint(Game.Position);
                Text(pos.x-80,pos.y-126,160,20,
                    Game.L("星灯调谐 ","GRID TUNING ")+Game.StormTuneTime.ToString("0")+"s · "+
                    Game.StormTuneStep+"/3",12,gold,TextAnchor.MiddleCenter);
            }
            if(Game.BlackoutActive && !Game.save.reducedMotion)
            {
                Point p=ScreenPoint(Game.Position);
                Text(p.x-72,p.y-110,144,19,
                    Game.L("长夜撤离 "+Game.BlackoutTime.ToString("0")+"s",
                        "BLACKOUT "+Game.BlackoutTime.ToString("0")+"s"),
                    12,gold,TextAnchor.MiddleCenter);
            }
            if (Game.racing)
            {
                Point target = GameCore.RaceRoute[Game.raceCheckpoint]; Ring(target, .65f, gold, 20); Point p = ScreenPoint(target);
                Text(p.x - 20, p.y - 44, 40, 35, (Game.raceCheckpoint + 1).ToString(), 23, gold, TextAnchor.MiddleCenter);
                Box(430, 112, 420, 48, panel); Text(449, 124, 382, 30, Game.L("风旗竞速 ", "WIND RACE ") + Game.raceTime.ToString("0.0") + " / 30s   ·   " + Game.raceCheckpoint + "/5", 17, gold, TextAnchor.MiddleCenter);
            }
            if (Game.reliefMissionRoom == Game.save.room)
            {
                Point rescue = Game.reliefMissionWave == 0 ?
                    Game.FieldSurveyTarget(Game.save.room) : Game.reliefFollowerPosition;
                if (Game.reliefMissionWave == 3)
                    foreach (WorldObject o in Game.Map.objects)
                        if (o.kind == "fieldstation" + Game.save.room)
                        { rescue = new Point(o.x, o.y); break; }
                Ring(rescue, .55f, Game.reliefMissionWave == 3 ? mint : gold, 20);
                Point rp = ScreenPoint(rescue);
                Text(rp.x - 56, rp.y - 65, 112, 24,
                    Game.reliefMissionWave == 3 ? Game.L("护送返程", "ESCORT HOME") :
                        Game.reliefMissionWave == 0 ? Game.L("寻找居民", "FIND CIVILIANS") :
                        Game.L("守护居民", "DEFEND CIVILIANS"), 12, gold, TextAnchor.MiddleCenter);
                if (Game.reliefMissionWave >= 1 && Game.reliefMissionWave <= 2)
                {
                    if ((Game.Position - rescue).Length < 1.7f &&
                        Game.ReliefFieldGuardAvailable)
                        Text(rp.x - 72, rp.y - 94, 144, 23,
                            ControlKey("E", "B") + Game.L(" · 紧急护卫", " · Rally guard"),
                            11, gold, TextAnchor.MiddleCenter);
                    if (Game.reliefWardTime > 0)
                    {
                        Ring(rescue, .8f, mint, 24);
                        Text(rp.x - 40, rp.y - 103, 80, 22,
                            Game.L("护卫 ", "GUARD ") +
                                Game.reliefWardTime.ToString("0.0") + "s",
                            11, mint, TextAnchor.MiddleCenter);
                    }
                    Color protection = Game.reliefFollowerSafety < 30 ? PixelArt.C("#f27d7d") :
                        Game.reliefFollowerSafety < 65 ? gold : mint;
                    Bar(rp.x - 33, rp.y + 10, 66,
                        Game.reliefFollowerSafety / 100f, protection, 6);
                    Text(rp.x - 42, rp.y + 17, 84, 18,
                        Game.L("安全 ", "SAFE ") + (int)Math.Ceiling(Game.reliefFollowerSafety) + "%",
                        10, protection, TextAnchor.MiddleCenter);
                }
            }
            if (Game.ReliefEscortActive)
            {
                Point escort = Game.reliefFollowerPosition;
                // Sprite is depth-sorted with the world, not drawn over hero.
                Ring(escort, .35f, mint, 16);
                if (Game.reliefClimateKitTime > 0)
                {
                    Ring(escort, 1.1f, PixelArt.C("#7dc8d4"), 24);
                    Point weatherHud = ScreenPoint(escort);
                    Text(weatherHud.x - 45, weatherHud.y - 150, 90, 20,
                        Game.L("气候防护 ", "SHELTER ") +
                            Game.reliefClimateKitTime.ToString("0.0") + "s",
                        11, mint, TextAnchor.MiddleCenter);
                }
                if (Game.reliefHurryTime > 0)
                {
                    Point pace = ScreenPoint(escort);
                    Text(pace.x - 41, pace.y - 130, 82, 20,
                        Game.L("急行 ", "HURRY ") +
                            Game.reliefHurryTime.ToString("0.0") + "s",
                        11, gold, TextAnchor.MiddleCenter);
                }
                if (Game.reliefWardTime > 0)
                {
                    Ring(escort, .88f, mint, 24);
                    Point guardScreen = ScreenPoint(escort);
                    Text(guardScreen.x - 44, guardScreen.y - 110, 88, 23,
                        Game.L("护卫 ", "GUARD ") + Game.reliefWardTime.ToString("0.0") + "s",
                        11, mint, TextAnchor.MiddleCenter);
                }
                Point escortScreen = ScreenPoint(escort);
                if (Game.ReliefEscortUnderAttack)
                    Text(escortScreen.x - 60, escortScreen.y - 89, 120, 26,
                        Game.L("警戒！来袭", "INCOMING"), 11, gold,
                        TextAnchor.MiddleCenter);
                else if (Game.ReliefEscortGap < 1.7f)
                    Text(escortScreen.x - 60, escortScreen.y - 89, 120, 26,
                        ControlKey("E", "B") + Game.L(" · 指挥 / 敷药", " · Command / care"),
                        11, gold, TextAnchor.MiddleCenter);
                else
                    Text(escortScreen.x - 50, escortScreen.y - 84, 100, 22,
                        Game.L(Game.reliefFollowerWaiting ? "等待接应" : "获救居民",
                            Game.reliefFollowerWaiting ? "WAITING" : "SURVIVORS"),
                        11, mint, TextAnchor.MiddleCenter);
                if (Game.reliefFollowerWaiting)
                    Text(escortScreen.x - 22, escortScreen.y - 99, 44, 20,
                        "II", 15, gold, TextAnchor.MiddleCenter);
                Color safetyColor = Game.reliefFollowerSafety < 30 ? PixelArt.C("#f27d7d") : mint;
                Bar(escortScreen.x - 30, escortScreen.y + 10, 60,
                    Game.reliefFollowerSafety / 100f, safetyColor, 6);
            }
            if (Game.save.room == 7 && !Game.save.reducedMotion && Game.save.effectsQuality > 0)
                for (int i = 0; i < 28; i++) { float x = (i * 91 + clock * 19) % Width, y = (i * 67 + clock * 32) % Height; Box(x, y, 3, 3, new Color(.9f, .96f, 1, .58f)); }
        }
        private bool DrawExpansionObject(WorldObject o)
        {
            string kind = o.kind; Point p = ScreenPoint(new Point(o.x, o.y));
            if(kind=="stormcouncil")
            {
                Color tint=Game.StormChapterComplete?mint:
                    Game.Has("storm_started")?gold:muted;
                PropImage("witnessledger",0,p.x-30,p.y-70,60,73,Color.white);
                Ring(new Point(o.x,o.y),Game.StormTuning?1.12f:.74f,tint,27);
                Text(p.x-78,p.y-91,156,20,
                    Game.StormChapterComplete?Game.L("星雨公约生效","STORM CHARTER IN EFFECT"):
                    Game.L("第六章 · 星雨议事","FIRST STORM COUNCIL"),
                    11,tint,TextAnchor.MiddleCenter);
                if(Game.Has("storm_started")&&!Game.StormChapterComplete)
                    Text(p.x-78,p.y-110,156,19,
                        Game.StormBudgetSpent+"/8 · "+Game.StormAllocations+"/3",
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("stormrelay"))
            {
                int region;
                if(!int.TryParse(kind.Substring("stormrelay".Length),out region)||
                    region<1||region>3)return true;
                int policy=Game.StormPolicy(region);
                Color tint=policy==1?mint:policy==2?PixelArt.C("#b5cce5"):
                    Game.Has("storm_started")?PixelArt.C("#eaa381"):muted;
                PropImage("stormrelay",region,p.x-30,p.y-73,60,75,Color.white);
                Ring(new Point(o.x,o.y),policy!=0?.95f:1.17f,tint,23);
                Text(p.x-76,p.y-94,152,19,
                    policy==1?Game.L("公共庇护已通电","SHELTER PROTECTED"):
                    policy==2?Game.L("公共供能已通电","UTILITIES PROTECTED"):
                    Game.L("星雨配电未稳定","STORM GRID UNSTABLE"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("stormnode"))
            {
                int region;
                if(!int.TryParse(kind.Substring("stormnode".Length),out region)||
                    region<1||region>3)return true;
                Color tint=Game.StormChapterComplete||Game.Has("storm_tuned")?mint:
                    Game.StormTuning&&Game.StormNextNode==region?gold:muted;
                PropImage("stormlamp",region,p.x-27,p.y-74,54,78,Color.white);
                Ring(new Point(o.x,o.y),Game.StormTuning&&Game.StormNextNode==region?1.1f:.65f,tint,23);
                Text(p.x-74,p.y-94,148,20,
                    Game.StormTuning&&Game.StormNextNode==region?
                        Game.L("下一个节点 · 按E","NEXT GRID NODE · E"):
                    Game.StormChapterComplete?
                        Game.L("公共星灯运行中","CIVIC LAMP ONLINE"):
                        Game.L("公共星灯","CIVIC LAMP"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("afterlightwaypoint"))
            {
                int region;
                if(!int.TryParse(kind.Substring("afterlightwaypoint".Length),out region) ||
                    region<1||region>3)return true;
                PropImage("afterlightanchor",region,p.x-26,p.y-69,52,69,Color.white);
                Ring(new Point(o.x,o.y),.67f,mint,26);
                Text(p.x-78,p.y-91,156,22,
                    Game.L(new[]{"密林公共星灯","遗迹公共星灯","矿场公共星灯"}[region-1],
                        new[]{"GROVE STARROAD","RUINS STARROAD","MINE STARROAD"}[region-1]),
                    11,mint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="afterlightcouncil")
            {
                Color tint=Game.Has("afterlight_complete")?mint:
                    Game.Has("afterlight_started")?gold:muted;
                PropImage("witnessledger",0,p.x-31,p.y-67,62,72,Color.white);
                Ring(new Point(o.x,o.y),Game.Has("afterlight_complete")?.95f:.65f,tint,27);
                Text(p.x-78,p.y-90,156,22,
                    Game.Has("afterlight_complete")?Game.L("三地重建公约","RECONSTRUCTION CHARTER"):
                    Game.L("战后重建议事台","AFTERLIGHT COUNCIL"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="afterlightanchor"+Game.save.room ||
                kind=="afterlightoutlet"+Game.save.room)
            {
                int region=Game.save.room;
                bool anchor=kind=="afterlightanchor"+region;
                bool complete=Game.Has("afterlight_done_"+region);
                bool active=Game.AfterlightChargeRegion==region;
                Color tint=complete?mint:active?PixelArt.C("#f1d39a"):
                    Game.AfterlightMode(region)!=0?gold:muted;
                PropImage(anchor?"afterlightanchor":"afterlightoutlet",region,
                    p.x-32,p.y-78,64,83,complete?Color.white:muted);
                Ring(new Point(o.x,o.y),complete?.9f:active?1.2f:.62f,tint,22);
                Text(p.x-79,p.y-104,158,22,
                    complete?Game.L(anchor?"公共原始锚点":"公共设施运转中",
                        anchor?"CIVIC ROOT ANCHOR":"PUBLIC SERVICE ONLINE"):
                    active?Game.L(anchor?"余光输送中":"稳定负载待接通",
                        anchor?"TRANSFER ACTIVE":"CHARGE ARRIVING"):
                    Game.L(anchor?"旧星根供能锚点":"断开的公共接收台",
                        anchor?"OLD STARROOT ANCHOR":"CIVIC RECEIVER"),
                    11,tint,TextAnchor.MiddleCenter);
                if(active)
                    Text(p.x-47,p.y-122,94,19,
                        Game.AfterlightChargeLeft.ToString("0")+"s",
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="witnessnpc"+Game.save.room)
            {
                int chapter=Game.save.room;
                bool filed=Game.Has("witness_complete_"+chapter);
                bool repaired=Game.Has("afterlight_done_"+chapter);
                bool steward=Game.Has("afterlight_steward_"+chapter);
                bool stormActive=Game.Has("storm_started")&&Game.StormPolicy(chapter)!=0;
                bool helped=Game.Has("storm_relief_"+chapter);
                Color tint=steward?mint:repaired?gold:filed?mint:Game.Has("witness_started_"+chapter)?gold:muted;
                DrawNpc("innGuest",new Point(o.x,o.y),11+chapter);
                Text(p.x-66,p.y-86,132,20,
                    stormActive?helped?Game.L("星雨热汤已送达","STORM RELIEF DELIVERED"):
                        Game.L("邻居需要热汤","NEIGHBORS NEED STEW"):
                    repaired?steward?Game.L("维护承诺已署名","STEWARD SIGNED"):
                        Game.L("维护者待署名","SIGN MAINTENANCE"):
                    filed?Game.L("证词已公开","TESTIMONY FILED"):
                    Game.L(new[]{"受伤根系证人","渡口幸存证人","矿工停机证人"}[chapter-1],
                        new[]{"GROVE WITNESS","CROSSING WITNESS","MINER WITNESS"}[chapter-1]),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="witnesssite1"+Game.save.room ||
                kind=="witnesssite2"+Game.save.room)
            {
                int chapter=Game.save.room;
                bool second=kind=="witnesssite2"+chapter;
                bool done=second?Game.Has("witness_sealed_"+chapter):
                    Game.Has("witness_read_"+chapter);
                bool active=Game.WitnessDefenseRoom==chapter && second;
                Color tint=done?mint:active?PixelArt.C("#dd9678"):gold;
                PropImage("witnessledger",chapter,p.x-28,p.y-65,56,68,done?Color.white:muted);
                Ring(new Point(o.x,o.y),active?1f:.55f,tint,24);
                Text(p.x-70,p.y-88,140,20,
                    done?Game.L("原始证据已保全","RECORD PRESERVED"):
                    active?Game.L("击退封锁卫兵","DEFEAT SECURITY"):
                    second?Game.L("现场保全印记","FIELD PRESERVATION"):
                    Game.L("证人原始记录","WITNESS LEDGER"),
                    11,tint,TextAnchor.MiddleCenter);
                if(active)
                    Text(p.x-48,p.y-108,96,19,
                        Game.WitnessDefenseTime.ToString("0")+"s",
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="blackoutshelter")
            {
                Color tint=Game.Has("blackout_complete")?mint:
                    Game.BlackoutActive?PixelArt.C("#f7c783"):gold;
                PropImage("blackoutrefuge",0,p.x-38,p.y-75,76,77,Color.white);
                Ring(new Point(o.x,o.y),Game.BlackoutActive?1.25f:.75f,tint,28);
                Text(p.x-76,p.y-98,152,23,
                    Game.Has("blackout_complete")?Game.L("公开民用避难所","PUBLIC REFUGE"):
                    Game.L("长夜公共避难所","BLACKOUT SHELTER"),
                    12,tint,TextAnchor.MiddleCenter);
                if(Game.BlackoutActive)
                    Text(p.x-80,p.y-117,160,20,
                        Game.BlackoutTime.ToString("0")+"s · "+
                        Game.BlackoutCount+"/3",12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("blackoutcivilian"))
            {
                int id;
                if(!int.TryParse(kind.Substring("blackoutcivilian".Length),out id)
                    || id<1 || id>3)return true;
                if(!Game.BlackoutCivilianVisible(id))return true;
                DrawNpc("innGuest",new Point(o.x,o.y),id+11);
                Color tint=Game.BlackoutActive?gold:muted;
                Ring(new Point(o.x,o.y),.56f,tint,22);
                Text(p.x-72,p.y-80,144,20,
                    Game.L(new[]{"莱娜 · 母亲","伊恩 · 孩子","奥姆 · 守灯人"}[id-1],
                        new[]{"LENA · MOTHER","IAN · CHILD","ORME · LAMPKEEPER"}[id-1]),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("triadmemory"))
            {
                int site;
                if(!int.TryParse(kind.Substring("triadmemory".Length),out site) ||
                    site<1 || site>3)return true;
                bool seen=Game.Has("triad_evidence_"+site);
                Color tint=seen?mint:Game.Has("triad_started")?gold:muted;
                PropImage("column",site%2,p.x-27,p.y-65,54,72,seen?Color.white:muted);
                Ring(new Point(o.x,o.y),seen?.75f:.5f,tint,24);
                Text(p.x-67,p.y-85,134,20,
                    seen?Game.L("原始星核账已记录","EVIDENCE SECURED"):
                    Game.L(new[]{"森林供能记录","遗迹水闸记录","矿场机器记录"}[site-1],
                        new[]{"FOREST LEDGER","SLUICE LEDGER","MINE LEDGER"}[site-1]),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="triadnexus")
            {
                Color tint=Game.TriadBossInterruptReady?PixelArt.C("#efc57e"):
                    Game.Has("triad_sealed")?mint:
                    Game.TriadDefenseActive?PixelArt.C("#d28b86"):gold;
                PropImage("altar",0,p.x-30,p.y-73,60,80,
                    Game.Has("triad_sealed")?Color.white:muted);
                Ring(new Point(o.x,o.y),Game.TriadDefenseActive?1.3f:.7f,tint,30);
                Text(p.x-75,p.y-90,150,20,
                    Game.TriadBossInterruptReady?Game.L("按E · 中断屏障","E · INTERRUPT SHIELD"):
                    Game.Has("triad_sealed")?Game.L("真相已保全","AUDIT PRESERVED"):
                    Game.TriadDefenseActive?Game.L("保密防卫启动","SECURITY ACTIVE"):
                    Game.L("三核中央审计","THREE-CORE AUDIT"),
                    11,tint,TextAnchor.MiddleCenter);
                if(Game.TriadDefenseActive)
                    Text(p.x-43,p.y-110,86,19,Game.TriadTime.ToString("0")+"s",
                        12,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="ironfoundry")
            {
                bool done=Game.Has("iron_temper_complete");
                Color tint=done?mint:Game.TemperActive?PixelArt.C("#e3a66b"):gold;
                PropImage("foundry",0,p.x-35,p.y-74,70,82,done?Color.white:muted);
                Ring(new Point(o.x,o.y),Game.TemperActive?1.1f:.6f,tint,22);
                Text(p.x-70,p.y-91,140,22,done?
                    Game.L("公共回火工房","PUBLIC FOUNDRY"):
                    Game.L("旧矿场锻炉","OLD FOUNDRY"),
                    12,tint,TextAnchor.MiddleCenter);
                if(Game.TemperActive)
                {
                    Bar(p.x-42,p.y+10,84,Game.TemperPressure/95f,tint,7);
                    Text(p.x-70,p.y-112,140,19,
                        Game.TemperTime.ToString("0")+"s · "+
                        Game.TemperPressure.ToString("0")+"/95",11,tint,
                        TextAnchor.MiddleCenter);
                }
                return true;
            }
            if(kind=="ironvent1" || kind=="ironvent2")
            {
                int stage=kind=="ironvent1"?1:2;
                bool done=Game.Has("iron_temper_complete") ||
                    Game.TemperActive && Game.TemperStage>stage;
                bool next=Game.TemperActive && Game.TemperStage==stage;
                Color tint=done?mint:next?PixelArt.C("#f3ad75"):muted;
                PropImage("pressurevent",0,p.x-25,p.y-59,50,65,done?Color.white:muted);
                Ring(new Point(o.x,o.y),next?.85f:.42f,tint,20);
                Text(p.x-67,p.y-79,134,20,
                    done?Game.L("管线已降温","PRESSURE RELEASED"):
                    next?Game.L("击败守卫，手动泄压","CLEAR GUARDS · VENT"):
                    Game.L("封锁的散热管","LOCKED COOLANT"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="glasswindkeeper")
            {
                DrawNpc("innGuest",new Point(o.x,o.y),9);
                Text(p.x-30,p.y-85,60,20,
                    Game.Has("glasswind_complete")?"✓":Game.Has("desert_report")?"!":"·",
                    17,Game.Has("glasswind_complete")?mint:gold,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("glasswindrelay"))
            {
                int id;
                if(!int.TryParse(kind.Substring("glasswindrelay".Length),out id) ||
                    id<1 || id>3) return true;
                bool done=Game.Has("glasswind_"+id),active=Game.GlasswindSite==id;
                Color tint=done?mint:active?PixelArt.C("#faaf70"):gold;
                PropImage("column",0,p.x-29,p.y-67,58,76,done?Color.white:muted);
                Ring(new Point(o.x,o.y),active?2.55f:done?1f:.62f,tint,32);
                Text(p.x-61,p.y-89,122,20,
                    done?Game.L("公共避暑风脉","PUBLIC SHADE"):
                    active?Game.L("风脉正在校准","RELAY CHARGING"):
                    Game.L("旧风压中继","WIND RELAY"),
                    11,tint,TextAnchor.MiddleCenter);
                if(active)
                {
                    Bar(p.x-41,p.y+11,82,Game.GlasswindCharge/8f,tint,7);
                    Text(p.x-45,p.y-109,90,20,
                        Game.GlasswindTime.ToString("0")+"s · "+
                        Game.GlasswindWave+"/2",11,tint,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if(kind=="tidedock")
            {
                DrawNpc("innGuest",new Point(o.x,o.y),11);
                Color tint=Game.Has("tide_salvage_complete")?mint:gold;
                Text(p.x-63,p.y-85,126,20,
                    Game.Has("tide_salvage_complete")?Game.L("公共货单已归档","CARGO PUBLIC"):
                    Game.TideSalvageActive?Game.L("等待运回货物","DELIVER CARGO"):
                    Game.L("潮汐打捞码头","TIDAL SALVAGE"),
                    11,tint,TextAnchor.MiddleCenter);
                if(Game.TideSalvageActive)
                {
                    Text(p.x-50,p.y-106,100,20,
                        Game.TideSalvageTime.ToString("0")+"s · "+
                        (Game.TideSalvagePhase-1)+"/3",12,gold,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if(kind.StartsWith("tidecargo"))
            {
                int site;
                if(!int.TryParse(kind.Substring("tidecargo".Length),out site) ||
                    site<1 || site>3)return true;
                bool done=Game.Has("tide_salvage_complete") ||
                    Game.TideSalvageActive && Game.TideSalvagePhase>site;
                bool next=Game.TideSalvageActive && Game.TideSalvagePhase==site;
                Color tint=done?mint:next?gold:muted;
                PropImage("cache",0,p.x-22,p.y-41,44,49,
                    done?Color.white:Game.LowTide?Color.white:muted);
                Ring(new Point(o.x,o.y),next?.78f:.42f,tint,20);
                Text(p.x-65,p.y-71,130,20,
                    done?Game.L("已打捞","RECOVERED"):
                    next?Game.LowTide?Game.L("可收回货单","SALVAGE NOW"):
                    Game.L("等待退潮","WAIT FOR EBB"):
                    Game.L("封存货箱","SEALED CRATE"),
                    11,tint,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="skywatcher")
            {
                DrawNpc("innGuest",new Point(o.x,o.y),12);
                Text(p.x-30,p.y-86,60,22,Game.Has("skywatch_complete")?"✓":"!",
                    18,Game.Has("skywatch_complete")?mint:gold,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind.StartsWith("skyprism"))
            {
                string id=kind.Substring("skyprism".Length);
                bool aligned=Game.Has("skywatch_prism_"+id);
                Color light=aligned?mint:gold;
                PropImage("altar",0,p.x-29,p.y-57,58,67,
                    aligned?Color.white:muted);
                Ring(new Point(o.x,o.y),aligned?.72f:.5f,light,20);
                if(Game.SkywatchEchoRefuge)
                    Ring(new Point(o.x,o.y),1.12f,mint,24);
                string legend=!aligned?Game.L("观测棱镜","SKY PRISM"):
                    Game.Has("skywatch_afterglow_complete")?
                        Game.L("永久避寒","WARM REFUGE"):
                    Game.Has("skywatch_complete") &&
                        !Game.Has("skywatch_afterglow_"+id)?
                        Game.L("余辉待收集","AFTERGLOW"):Game.L("已校准","ALIGNED");
                Text(p.x-53,p.y-85,106,21,legend,
                    11,light,TextAnchor.MiddleCenter);
                return true;
            }
            if(kind=="skyobservatory")
            {
                bool active=Game.SkywatchRoom==7;
                bool sealedSky=Game.Has("skywatch_defeated");
                Color tint=active?PixelArt.C("#e8b4ff"):sealedSky?mint:gold;
                PropImage("column",0,p.x-34,p.y-76,68,82,Color.white);
                Ring(new Point(o.x,o.y),active?.95f:.65f,tint,24);
                Text(p.x-70,p.y-104,140,23,
                    active?Game.L("古观星守卫","SKYWATCH GUARDIAN"):
                    sealedSky?Game.L("守卫已停止","GUARDIAN DISABLED"):
                    Game.L("古观星台","OBSERVATORY"),12,tint,TextAnchor.MiddleCenter);
                if(active)
                {
                    Enemy boss=Game.enemies.Find(Game.IsSkywatchEnemy);
                    if(boss!=null) Bar(p.x-43,p.y+12,86,boss.hp/Math.Max(1,boss.maxHp),tint,7);
                    Text(p.x-43,p.y-124,86,18,Game.SkywatchTime.ToString("0")+"s",
                        11,tint,TextAnchor.MiddleCenter);
                    if (Game.SkywatchGroundingTime>0)
                    {
                        Ring(new Point(o.x,o.y),1.2f,mint,24);
                        Text(p.x-63,p.y-143,126,17,
                            Game.L("接地 ", "GROUNDED ") +
                                Game.SkywatchGroundingTime.ToString("0.0")+"s",
                            10,mint,TextAnchor.MiddleCenter);
                    }
                    else if (Game.SkywatchGroundingAvailable)
                        Text(p.x-70,p.y-143,140,18,
                            Game.L("E · 棱镜接地", "E · Ground prism"),
                            10,gold,TextAnchor.MiddleCenter);
                }
                return true;
            }
            if (kind == "commonswitness")
            {
                DrawNpc("innGuest", new Point(o.x, o.y), 5);
                Text(p.x - 35, p.y - 85, 70, 22,
                    Game.Has("commons_relay_complete") ? "✓" :
                    Game.Has("boss_4") ? "!" : "·", 18,
                    Game.Has("commons_relay_complete") ? mint : gold,
                    TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("commonsrelay"))
            {
                int circuit;
                if (!int.TryParse(kind.Substring("commonsrelay".Length), out circuit) ||
                    circuit < 1 || circuit > 3) return true;
                bool done = Game.Has("commons_relay_sealed") ||
                    Game.CommonsRelayRoom == Game.save.room && Game.CommonsRelayStep > circuit;
                bool next = !done && Game.Has("commons_relay_started") &&
                    (Game.CommonsRelayRoom == 0 ? circuit == 1 :
                        Game.CommonsRelayStep == circuit);
                Color light = done ? mint : next ? gold : muted;
                PropImage("column", 0, p.x - 30, p.y - 65, 60, 73,
                    done || next ? Color.white : muted);
                Ring(new Point(o.x,o.y),done?.8f:.55f,
                    new Color(light.r,light.g,light.b,.75f),20);
                Text(p.x - 55,p.y - 94,110,23,
                    done ? Game.L("公共线路在线","PUBLIC CIRCUIT") :
                    next ? Game.L("等待重连","RECONNECT") :
                           Game.L("封闭通道","LOCKED"),
                    11,light,TextAnchor.MiddleCenter);
                if (next && Game.CommonsRelayRoom == Game.save.room)
                    Text(p.x - 40,p.y - 113,80,22,
                        Game.CommonsRelayTime.ToString("0") + "s",
                        12,gold,TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "archiveecho")
            {
                bool completed = Game.Has("archive_echo_complete"), active = Game.AtlasEchoRoom == Game.save.room;
                Color tint = active ? PixelArt.C("#e7b0f2") : completed ? mint : gold;
                PropImage("column", 0, p.x - 28, p.y - 65, 56, 73, completed ? muted : Color.white);
                Ring(new Point(o.x, o.y), .55f, tint, 20);
                Text(p.x - 53, p.y - 92, 106, 22,
                    active ? Game.L("回声激活", "ECHO ACTIVE") :
                    completed ? Game.L("已完成", "CALIBRATED") :
                    Game.L("方位试炼", "COMPASS TRIAL"),
                    12, tint, TextAnchor.MiddleCenter);
                Enemy guardian = Game.enemies.Find(enemy => enemy.id == 83001);
                if (active && guardian != null)
                    Bar(p.x - 37, p.y + 12, 74,
                        guardian.hp / Math.Max(1, guardian.maxHp), tint, 6);
                return true;
            }
            if (kind == "archivekeeper" || kind.StartsWith("archivefragment"))
            {
                bool collected = kind == "archivekeeper" ? Game.Has("archive_complete") :
                    Game.Has("archive_fragment_" + kind.Substring("archivefragment".Length));
                PropImage(kind == "archivekeeper" ? "post" : "altar", 0,
                    p.x - 26, p.y - 55, 52, 63, collected ? muted : Color.white);
                Ring(new Point(o.x, o.y), .48f, collected ? mint : gold, 16);
                Text(p.x - 18, p.y - 69, 36, 25, collected ? "✓" : "!", 19,
                    collected ? mint : gold, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("fieldstation"))
            {
                int station;
                if (!int.TryParse(kind.Substring("fieldstation".Length), out station) ||
                    station < 5 || station > 7) return false;
                bool powered = Game.Has("fieldstation_" + station);
                bool reinforced = Game.Has("farshore_relief_" + station);
                Color light = reinforced ? PixelArt.C("#a7e8da") : powered ? mint : gold;
                PropImage("lamp", 0, p.x - 26, p.y - 54, 52, 65, powered ? Color.white : muted);
                Ring(new Point(o.x, o.y), .55f, light, 16);
                Text(p.x - 55, p.y - 86, 110, 24,
                    reinforced ? Game.L("民用避难区", "RELIEF REFUGE") :
                    powered ? Game.L("安全观测站", "SHELTER ONLINE") :
                    Game.L("等待维修", "REPAIR STATION"), 11, light, TextAnchor.MiddleCenter);
                if (powered && station >= 6 && Game.NearActiveFieldStation(station, Game.FieldStationRefugeRadius(station)))
                    Ring(new Point(o.x, o.y), reinforced ? 1.4f : 1.1f,
                        new Color(light.r, light.g, light.b, .52f), 24);
                if (reinforced)
                    Text(p.x - 32, p.y + 13, 64, 21, station == 5 ? "90s" : "6.5", 13,
                        light, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("mirror"))
            {
                int id = int.Parse(kind.Substring(6)); PropImage("mirror", id, p.x - 34, p.y - 68, 68, 79);
                Text(p.x - 25, p.y - 92, 50, 25, new[] { "N", "E", "S", "W" }[Game.save.mirrors[id]], 17, gold, TextAnchor.MiddleCenter); return true;
            }
            if (kind.StartsWith("bell"))
            {
                int id = int.Parse(kind.Substring(4)); PropImage("bell", id, p.x - 36, p.y - 73, 72, 84);
                Text(p.x - 30, p.y - 94, 60, 25, new[] { "I", "II", "III", "IV" }[Game.save.bells[id]], 17, gold, TextAnchor.MiddleCenter); return true;
            }
            if (kind.StartsWith("relay"))
            { bool lit = Game.Has("relay_" + kind.Substring(5)); PropImage("lamp", 0, p.x - 24, p.y - 54, 48, 56, lit ? Color.white : muted); if (lit) Ring(new Point(o.x, o.y), .4f, mint, 12); return true; }
            if (kind == "campfire" || kind == "brazier")
            {
                bool lit = kind == "campfire" || Game.Has("warm_" + o.id);
                PropImage("fire", 0, p.x - 28, p.y - 44, 56, 65, lit ? Color.white : muted); return true;
            }
            if (kind.StartsWith("history") || kind == "manifest")
            {
                PropImage("post", 0, p.x - 24, p.y - 55, 48, 56);
                int letter = -1;
                bool cipher = kind.StartsWith("history") && int.TryParse(kind.Substring(7), out letter) &&
                    letter >= 6 && letter <= 8 && Game.Has("survey_complete");
                bool decoded = cipher && Game.Has("dispatch_" + letter);
                if (cipher) Ring(new Point(o.x, o.y), .43f, decoded ? mint : gold, 14);
                Text(p.x - 16, p.y - 51, 32, 23, cipher ? decoded ? "✓" : "!" : "?", 17,
                    decoded ? mint : gold, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "expAltar" || kind == "receiver" || kind == "sluice" || kind == "abyssGate" || kind == "abyssEntry" || kind == "abyssExit")
            {
                bool active = kind == "expAltar" ? Game.GateOpen : kind == "receiver" ? Game.Has("mirrors_lit") : kind == "abyssEntry" ? Game.CanEnter(8) : true;
                PropImage(kind.StartsWith("abyss") ? "root" : "altar", 0, p.x - 34, p.y - 68, 68, 79, active ? Color.white : muted);
                Ring(new Point(o.x, o.y), .6f, active ? mint : muted, 16); return true;
            }
            if (kind == "shade" || kind == "skiff") return true;
            if (kind == "townboard" || kind == "contract")
            { PropImage("post", 0, p.x - 30, p.y - 66, 60, 70); Text(p.x - 14, p.y - 55, 28, 26, kind == "contract" ? "N" : "B", 16, gold, TextAnchor.MiddleCenter); return true; }
            if (kind == "envoy" || kind == "captain" || kind == "tinker" || kind == "keeper" || kind == "council" || kind.StartsWith("castaway"))
            {
                DrawNpc(kind, new Point(o.x, o.y));
                bool complete = kind == "captain" ? Game.Has("harbor_report") : kind == "tinker" ? Game.Has("desert_report") : kind == "keeper" ? Game.Has("mountain_report") : false;
                Text(p.x - 12, p.y - 86, 24, 25, complete ? "+" : "!", 22, complete ? mint : gold, TextAnchor.MiddleCenter); return true;
            }
            return false;
        }
    }
}
