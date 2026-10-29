using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool drawingHud, drawingOutline;
        private float combatHudTime;
        private readonly List<Rect> hudRegions=new List<Rect>(10),hudPointerRegions=new List<Rect>(3);
        private readonly List<Rect> worldOverlayRegions = new List<Rect>(8);
        private static readonly Rect CombatJournalButton=new Rect(1185,58,65,24);
        private static readonly Rect CombatPebbleButton=new Rect(32,737,284,36);
        private bool CombatClimate { get { return Game.save.room == 6 || Game.save.room == 7 || Game.save.teaUntil > Game.save.totalTime; } }
        private bool CounterHud
        { get { return Game.riposteTime > .001f ||
            (Game.playerAction!=null&&Game.playerAction.riposteArmed); } }
        private float CounterHudY { get { return CombatClimate ? 136 : 103; } }
        private float CombatToastY
        { get { return CombatClimate ? (CounterHud ? 170 : 135) :
            (CounterHud ? 137 : 108); } }
        private bool HasLiveBoss { get {foreach(Enemy e in Game.enemies)if(e.boss)return true;return false;} }
        private bool HasFallenBoss { get {foreach(FallenActor f in Game.fallen)if(f.actor.boss)return true;return false;} }
        private bool CombatHud
        { get {if(Game.mode!="world")return false;if(Game.BossVictoryFeedback||Game.AfterlightWatchActive||Game.OuterPatrolActive||(Game.reliefMissionRoom!=0&&Game.reliefMissionRoom==Game.save.room)||combatHudTime>0||CounterHud||Game.Dying||HasFallenBoss)return true;foreach(Enemy e in Game.enemies)if(e.boss||(e.Position-Game.Position).Length<8)return true;return false;} }
        private static bool Contains(Rect r, Point p) { return p.x >= r.x && p.x < r.x + r.width && p.y >= r.y && p.y < r.y + r.height; }
        private static bool Overlap(Rect a, Rect b) { return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y; }
        private Rect ActorBounds(Point feet, bool boss = false)
        { Point p = ScreenPoint(feet); return new Rect(p.x - (boss ? 88 : 35), p.y - (boss ? 185 : 76), boss ? 176 : 70, boss ? 190 : 80); }
        private Rect PlayerHudBounds()
        {Point p=ScreenPoint(Game.Position);Rect b=ActorVisibilityBounds.PlayerEnvelope;return new Rect(p.x-96+b.x*.75f-2,p.y-180+b.y*.75f-2,b.width*.75f+4,b.height*.75f+4);}
        private Rect EnemyHudBounds(Enemy enemy)
        {
            int pose=EnemyPose(enemy),columns=6,frame=Math.Min(2,enemy.kind)*6+pose;
            Texture2D sheet=monsterSheet;float density=(enemy.kind==2?106:96)/128f;
            bool flip=enemy.VisualFlip;
            if(Game.practicing){frame=12+pose;density=106/128f;}
            else if(enemy.boss)
            {
                int row=Math.Max(0,Game.save.room-1);columns=4;density=BossCanvasSize(Game.save.room)/320f;
                if(pose==1&&bossWalkSheets[row/4]!=null){sheet=bossWalkSheets[row/4];frame=row%4*4+GameCore.GaitFrame(enemy.moveTime,2.8f);}
                else{sheet=bossSheet;frame=row*4+(pose==2?1:pose==3?2:0);}
            }
            else if(pose==1&&monsterWalkSheet!=null)
            {sheet=monsterWalkSheet;columns=4;int gait=GameCore.GaitFrame(enemy.moveTime,enemy.kind==0?1.35f:1.8f);frame=Math.Min(2,enemy.kind)*4+(enemy.backpedaling?(4-gait)%4:gait);}
            else if(enemy.kind==1&&enemyArcherVertical!=null&&(pose==2||pose==3)&&Math.Abs(enemy.facing.y)>Math.Abs(enemy.facing.x))
            {sheet=enemyArcherVertical;columns=3;frame=(enemy.facing.y<0?0:2)+pose-2;flip=false;}
            Rect bounds;if(!ActorVisibilityBounds.TryBounds(sheet,frame,out bounds))return ActorBounds(enemy.Position,enemy.boss);
            Point foot=ScreenPoint(enemy.Position);float cell=sheet.width/(float)columns,x=flip?cell-bounds.x-bounds.width:bounds.x;
            return new Rect(foot.x-cell*density/2+x*density-2,foot.y-cell*density*120/128+bounds.y*density-2,bounds.width*density+4,bounds.height*density+4);
        }
        private List<Rect> HudRegions()
        {
            List<Rect> rects=hudRegions;rects.Clear();
            if (CombatHud)
            {
                rects.Add(new Rect(20, 20, 260, 77)); rects.Add(new Rect(1030, 22, 230, 70));
                rects.Add(new Rect(426, 730, 428, 54)); rects.Add(new Rect(1134, 746, 126, 36));
                rects.Add(CombatPebbleButton);
                if (CombatClimate) rects.Add(new Rect(20,103,260,26));
                // A real audible sprint receives a distinct battle HUD
                // panel. Include it in the same actor-reveal region list:
                // enemy or hero sprites overlapping the status retain
                // their identity/visibility, just like Boss and health HUD.
                if (Game.Sprinting) rects.Add(new Rect(380,108,520,27));
                if (CounterHud) rects.Add(new Rect(20,CounterHudY,260,27));
                if (Game.toastTime > 0) rects.Add(new Rect(20,CombatToastY,260,62));
                if (HasLiveBoss || HasFallenBoss || Game.BossVictoryFeedback ||
                    Game.AfterlightWatchActive ||
                    Game.OuterPatrolActive || Game.MineConvoyActive ||
                    (Game.reliefMissionRoom!=0&&Game.reliefMissionRoom==Game.save.room) ||
                    Game.TideSalvageActive || Game.FrostSignalActive)
                    rects.Add(new Rect(380, 22, 520,
                        Game.SkywatchRoom==7 ||
                        Game.BossVictoryDangerLine.Length>0 ? 85 :
                        (Game.AfterlightWatchActive&&
                            (Game.AfterlightWatchIncomingStockThreat!=null||
                             Game.CompanionStockCoverInFlight||
                             Game.AfterlightWatchHeroBodyguarding) ||
                         Game.OuterPatrolActive&&
                            (Game.CompanionPatrolCoverInFlight||
                             Game.OuterPatrolBodyguardedThreat!=null))?88:65));
            }
            else
            {
                rects.Add(new Rect(24,24,298,104)); rects.Add(new Rect(1008,24,248,191)); rects.Add(new Rect(1008,233,248,248));
                rects.Add(new Rect(342,696,596,80)); rects.Add(new Rect(972,718,284,52));
                if (Game.save.room >= 5 || Game.MineConvoyActive ||
                    Game.save.teaUntil > Game.save.totalTime)
                    rects.Add(new Rect(24,145,298,60));
                if (Game.save.showGuidance && Game.TutorialStep < 8 && !Game.practicing) rects.Add(new Rect(24,474,298,204));
            }
            rects.AddRange(worldOverlayRegions); return rects;
        }
        private List<Rect> HudPointerRegions()
        {
            List<Rect> regions=HudRegions();
            // Read-only status, Boss health and toast pass world input through.
            // The real action controls retain their pointer protection.
            hudPointerRegions.Clear();
            if (!CombatHud) for (int i = 0; i < regions.Count - worldOverlayRegions.Count; i++) hudPointerRegions.Add(regions[i]);
            else { hudPointerRegions.Add(regions[2]); hudPointerRegions.Add(regions[3]); hudPointerRegions.Add(CombatJournalButton); hudPointerRegions.Add(CombatPebbleButton); }
            return hudPointerRegions;
        }
        private static bool OverlapsRegions(List<Rect> regions,Rect bounds)
        {foreach(Rect region in regions)if(Overlap(region,bounds))return true;return false;}
        private Rect EnemyWarningBounds(Enemy enemy)
        {
            if(!enemy.boss&&enemy.kind==1)
            {
                Point lift=new Point(0,GameCore.WeaponHeight(1,enemy.facing)/Tile),from=ScreenPoint(GameCore.WeaponOrigin(enemy.Position,1,enemy.facing)-lift),to=ScreenPoint(enemy.target-lift);
                return new Rect(Math.Min(from.x,to.x)-3,Math.Min(from.y,to.y)-3,Math.Abs(to.x-from.x)+6,Math.Abs(to.y-from.y)+6);
            }
            Point p=ScreenPoint(enemy.boss||enemy.kind==2?enemy.target:enemy.Position);float size=GameCore.EnemyAreaRadius(enemy)*Tile;
            return new Rect(p.x-size,p.y-size,size*2,size*2);
        }
        private bool ThreatOver(Rect rect)
        {
            if (Overlap(rect, PlayerHudBounds())) return true;
            foreach (Enemy enemy in Game.enemies) if (Overlap(rect, EnemyHudBounds(enemy))) return true;
            foreach (Projectile shot in Game.projectiles)
            { Point p = ScreenPoint(shot.position); p.y -= shot.height; if (Overlap(rect, new Rect(p.x-14,p.y-14,28,28))) return true; }
            foreach (Hazard hazard in Game.hazards)
            { Point p = ScreenPoint(hazard.position); float size = hazard.radius * Tile; if (Overlap(rect, new Rect(p.x-size,p.y-size,size*2,size*2))) return true; }
            foreach (Enemy enemy in Game.enemies) if (enemy.state == 1 && Overlap(rect,EnemyWarningBounds(enemy))) return true;
            return false;
        }
        private void DrawHud()
        {
            drawingHud = true;
            try
            {
                if (CombatHud) DrawCombatHud(); else DrawExplorationHud();
                // A live relief contract survives the transition into
                // real combat HUD mode. Its distant delivery target must
                // never vanish merely because an enemy comes on camera.
                DrawBiomeCrisisHud();
            }
            finally { drawingHud = false; }
        }
        private void DrawBiomeCrisisHud()
        {
            if(!Game.BiomeCrisisActive)return;
            int stage=Game.BiomeCrisisStage;
            Point delta=Game.BiomeCrisisTarget-Game.Position;
            float distance=delta.Length;
            int seconds=(int)Math.Ceiling(Game.BiomeCrisisSeconds);
            Color tone=Game.BiomeCrisisIntegrity<=1?
                PixelArt.C("#ec7668"):gold;
            Panel(380,141,520,63,
                new Color(.04f,.065f,.11f,.85f),
                new Color(tone.r,tone.g,tone.b,.74f));
            string objective=stage==1?
                Game.L("前往远端 · 击退两名袭击者",
                    "GO REMOTE · DEFEAT TWO RAIDERS"):
                stage==2?
                Game.L("袭击者已退 · 现场交付物资",
                    "RAIDERS DEFEATED · HAND OFF CARGO"):
                Game.L("带签收单返回真实出发地标",
                    "RETURN SIGNED RECEIPT TO ORIGIN");
            Text(392,143,495,26,
                Game.L("居民救援 · ","CIVILIAN RELIEF · ")+objective,
                14,tone,TextAnchor.MiddleCenter);
            Text(392,170,495,23,
                Game.L("剩余 "+seconds+"秒 · 物资"+
                    Game.BiomeCrisisIntegrity+"/3 · 目标距离 "+
                    (int)distance+"格 · 地图查看路线",
                    seconds+"S LEFT · CARGO "+Game.BiomeCrisisIntegrity+
                    "/3 · TARGET "+(int)distance+
                    " TILES · CHECK MAP"),
                12,paper,TextAnchor.MiddleCenter);
        }

        private void DrawHudThreatWitness()
        {
            if (Game.mode != "world") return;
            // Only the alpha edge is redrawn above the HUD. World art remains
            // visible through the faded background; resource text stays legible.
            List<Rect> regions = HudRegions();drawingOutline = true;
            try
            {
                foreach (Enemy e in Game.enemies) if (OverlapsRegions(regions,EnemyHudBounds(e))) DrawEnemyActor(e);
                if (OverlapsRegions(regions,PlayerHudBounds())) DrawPlayerActor();
            }
            finally { drawingOutline = false; }
            foreach (Projectile shot in Game.projectiles)
            {
                Point p = ScreenPoint(shot.position); p.y -= shot.height;
                foreach (Rect rect in regions) if (Contains(rect,p)) { Frame(p.x-9,p.y-9,18,18,shot.friendly?mint:PixelArt.C("#df8a68")); break; }
            }
            foreach (Enemy enemy in Game.enemies) if (enemy.state == 1 && OverlapsRegions(regions,EnemyWarningBounds(enemy)))
            {
                if(!enemy.boss&&enemy.kind!=2){DrawEnemyWarning(enemy);continue;}
                Point p=ScreenPoint(enemy.target);float radius=GameCore.EnemyAreaRadius(enemy);
                foreach(Rect rect in regions) if(Overlap(rect,new Rect(p.x-radius*Tile,p.y-radius*Tile,radius*Tile*2,radius*Tile*2)))
                { Ring(enemy.target,radius,new Color(.87f,.54f,.41f,.8f),48);break; }
            }
            foreach (Hazard hazard in Game.hazards)
            {
                Point p = ScreenPoint(hazard.position); float radius = hazard.radius * Tile;
                if (OverlapsRegions(regions, new Rect(p.x-radius,p.y-radius,radius*2,radius*2)))
                { Color color = HazardWarningColor(hazard); color.a = .8f; Ring(hazard.position,hazard.radius,color,48); }
            }
        }
        private void DrawCombatHud()
        {
            Panel(20,20,260,77,new Color(.045f,.08f,.13f,.64f),PixelArt.C("#456074"));
            Icon(Game.AppearanceWeapon == 0 ? "sword" : Game.AppearanceWeapon == 1 ? "bow" : "staff",30,29,25);
            Text(66,25,199,23,"LV " + Game.save.level + "  ·  " + (int)Game.save.health + "/" + (int)Game.save.maxHealth,14,paper);
            Bar(34,56,232,Game.save.health/Game.save.maxHealth,PixelArt.C("#d28b86"),6);
            Bar(34,72,232,Game.save.energy/100,mint,4); Text(34,78,232,17,Game.L("耐力 ","ENERGY ")+(int)Game.save.energy,10,mint);
            // Battle mode replaces the entire exploration HUD whenever a
            // living enemy approaches. Do not hide the meaningful stealth
            // choice precisely when a noisy sprint alerts a real guard.
            // The count is the last genuine acoustic notification, NOT
            // all enemies within straight-line distance.
            if (Game.Sprinting)
            {
                int listeners=Game.RecentSprintFootstepHearers;
                Color soundTint=listeners>0?gold:mint;
                Panel(380,108,520,27,
                    new Color(.045f,.08f,.13f,.68f),
                    new Color(soundTint.r,soundTint.g,soundTint.b,.65f));
                Text(394,112,492,19,listeners>0?
                    Game.L("疾跑脚步 · "+listeners+"名守卫听见脚步",
                        "SPRINT ×1.5 · "+listeners+" GUARD(S) HEARD FOOTSTEPS"):
                    Game.L("疾跑 ×1.5 · 脚步声可能惊动附近守卫",
                        "SPRINT ×1.5 · footsteps may alert patrols"),
                    12,soundTint,TextAnchor.MiddleCenter);
            }
            if (CombatClimate)
            {
                Panel(20,103,260,26,new Color(.045f,.08f,.13f,.45f),new Color(mint.r,mint.g,mint.b,.3f));
                Text(30,107,240,20,Game.save.teaUntil>Game.save.totalTime?Game.L("凉茶保护 · ","Tea ward · ")+(int)(Game.save.teaUntil-Game.save.totalTime)+"s":Game.save.room==6?Game.L("炎热 ","Heat ")+(int)Game.save.heat+"%":Game.L("寒冷 ","Cold ")+(int)Game.save.cold+"%",12,Game.save.heat>75||Game.save.cold>75?gold:mint);
            }
            if (CounterHud)
            {
                // A real dodge/parry earned this opening. Keep the countdown
                // legible beside climate and toast rather than hiding it
                // behind the standard exploration HUD during a brief lull.
                float y=CounterHudY;
                bool armed=Game.playerAction!=null&&
                    Game.playerAction.riposteArmed;
                Panel(20,y,260,27,new Color(.075f,.09f,.12f,.68f),
                    new Color(gold.r,gold.g,gold.b,.68f));
                Text(30,y+3,242,21,
                    armed ? Game.L("反击已蓄力 · 命中释放","RIPOSTE CHARGED · RELEASE TO STRIKE") :
                        Game.L("反击就绪 · ","RIPOSTE READY · ")+
                        Math.Ceiling(Game.riposteTime*10f)/10f+"s",
                    12,gold);
            }
            QuestGuide guide = Game.SkywatchRoom==7?
                Game.GetQuest("skywatch"):
                Game.MineConvoyActive?
                Game.GetQuest("mine_convoy"):
                Game.TideSalvageActive?Game.GetQuest("tide_salvage"):
                Game.FrostSignalActive?Game.GetQuest("frost_signal"):
                Game.CurrentGuide;
            Panel(1030,22,230,70,new Color(.045f,.08f,.13f,.45f),new Color(gold.r,gold.g,gold.b,.4f));
            Text(1041,27,208,27,Game.practicing ? Game.L("实战教学","COMBAT PRACTICE") : guide.title,12,gold);
            Text(1041,56,136,23,(int)(guide.Progress*100)+"%  ·  "+Game.L("查看步骤","View steps"),11,paper);
            if (Button(CombatJournalButton.x,CombatJournalButton.y,CombatJournalButton.width,CombatJournalButton.height,ControlKey("TAB","VIEW"))) OpenMenu("journal");
            // CombatHud is the ACTUAL shipped battle overlay; exploration
            // HUD is not drawn once a living Boss enters camera threat range.
            // Use exact instance identity so a same-ID stranger never steals
            // the optional observatory Boss title or its tactical clock.
            Enemy boss=Game.enemies.Find(e=>e!=null&&e.hp>0f&&
                e.boss&&Game.IsSkywatchEnemy(e))??
                Game.enemies.Find(e=>e!=null&&e.hp>0f&&e.boss);
            if (boss != null)
            {
                bool skywatch=Game.IsSkywatchEnemy(boss);
                Panel(380,22,520,skywatch?85:65,
                    new Color(.04f,.065f,.11f,.6f),
                    new Color(.6f,.4f,.42f,.45f));
                Text(396,26,488,skywatch?23:27,
                    Game.LiveBossName(boss)+"  /  "+boss.BossPhase,
                    16,gold,TextAnchor.MiddleCenter);
                if(skywatch)
                    Text(396,51,488,15,Game.SkywatchCombatCue(boss),
                        11,Game.SkywatchTime<=15f?gold:mint,
                        TextAnchor.MiddleCenter);
                Bar(399,skywatch?72:58,482,boss.hp/boss.maxHp,
                    PixelArt.C("#d28b86"),6);
                Bar(399,skywatch?87:73,482,boss.stagger/95,mint,3);
            }
            else if(Game.fallen.Exists(f=>f!=null&&f.actor!=null&&
                Game.IsSkywatchCorpse(f.actor)))
            {
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.6f),
                    new Color(gold.r,gold.g,gold.b,.7f));
                Text(396,28,488,26,Game.L("古观星守卫已停机",
                    "Skywatch guardian defeated"),18,gold,
                    TextAnchor.MiddleCenter);
                Text(396,57,488,23,Game.L("带原始光谱回余烬镇找伊澜",
                    "Return the spectrum to Yilan at Emberfall"),
                    12,paper,TextAnchor.MiddleCenter);
            }
            else if(Game.BossVictoryFeedback)
            {
                // Stay in the real battle HUD after the 1.4-second corpse
                // fade, instead of leaving the player without the next
                // chapter step. The content comes from authoritative core
                // quest state; no teleport or reward can occur here.
                string residualDanger=Game.BossVictoryDangerLine;
                bool stillDangerous=residualDanger.Length>0;
                Panel(380,22,520,stillDangerous?85:65,
                    new Color(.04f,.065f,.11f,.74f),
                    new Color(gold.r,gold.g,gold.b,.66f));
                Text(396,26,488,25,
                    Game.L("地区守护者已倒下","GUARDIAN DEFEATED"),
                    17,gold,TextAnchor.MiddleCenter);
                Text(394,51,492,23,Game.BossVictoryNextStep,
                    12,paper,TextAnchor.MiddleCenter);
                if(stillDangerous)
                    Text(394,76,492,22,residualDanger,12,
                        PixelArt.C("#ec7668"),TextAnchor.MiddleCenter);
            }
            else if (Game.AfterlightWatchActive)
            {
                // This is an active, damageable OBJECTIVE, not a boss:
                // the receiver must remain readable even when it is far
                // outside the player's camera or between enemy waves.
                float stock=Game.AfterlightWatchStock;
                // Read the real locked receiver threat once, matching
                // the HP contact, including walls and live body cover.
                // In-flight support is explicitly NOT a completed save.
                Enemy reserveThreat=Game.AfterlightWatchIncomingStockThreat;
                bool aidFlying=Game.CompanionStockCoverInFlight;
                bool heroBodyguard=Game.AfterlightWatchHeroBodyguarding;
                bool watchPressure=reserveThreat!=null||aidFlying||heroBodyguard;
                Color accent=stock<=30f?PixelArt.C("#ec7668"):stock<=60f?gold:mint;
                Panel(380,22,520,watchPressure?88:65,
                    new Color(.04f,.065f,.11f,.76f),
                    new Color(accent.r,accent.g,accent.b,.7f));
                Text(393,25,494,27,
                    Game.L("公共储备防卫 · 第","CIVIC RESERVE DEFENSE · WAVE ")+
                    Game.AfterlightWatchWave+"/2  ·  "+
                    Math.Ceiling(Game.AfterlightWatchTime)+"s",
                    15,paper,TextAnchor.MiddleCenter);
                Text(398,49,481,18,
                    Game.L("设施耐久 ","STOCK INTEGRITY ")+
                    Math.Ceiling(stock)+"%"+
                    (Game.AfterlightWatchPatchAvailable?
                        Game.L(" · 抢修 E"," · PATCH E"):""),
                    11,accent,TextAnchor.MiddleCenter);
                Bar(401,71,478,stock/100f,accent,7);
                if(watchPressure)
                    Text(398,82,481,18,
                        (heroBodyguard?
                            Game.L("已进入袭击线路 · 守住位置",
                                "IN STRIKE LANE · HOLD POSITION"):
                            reserveThreat!=null?
                            Game.L("来袭将命中储备 · ","STOCK STRIKE IN ")+
                            Math.Max(0f,reserveThreat.timer).ToString("0.0")+"s":
                            Game.L("当前无有效储备攻击","NO ACTIVE STOCK STRIKE"))+
                        (aidFlying?Game.L(" · 援射在途，需命中",
                            " · ALLY SHOT IN FLIGHT"):""),
                        11,reserveThreat!=null?gold:mint,
                        TextAnchor.MiddleCenter);
            }
            else if (Game.OuterPatrolActive)
            {
                // Read the expensive physical-threat selector once per IMGUI
                // pass. Its result feeds the ETA and heavy-hit label together,
                // avoiding three repeated enemy/LOS/muzzle scans in one panel.
                Enemy incomingStockThreat=Game.OuterPatrolIncomingStockThreat;
                float incomingStockSeconds=incomingStockThreat==null?-1f:
                    Math.Max(0f,incomingStockThreat.timer);
                bool incomingStockHeavy=incomingStockThreat!=null&&
                    incomingStockThreat.kind==2;
                // The threatened relief cache must be readable even when
                // the hero is intercepting a raider beyond the watchpost.
                // Register this panel as HUD geometry so warning overlays
                // remain visible above its translucent body.
                float stock=Game.OuterPatrolIntegrity;
                bool realPatrolCover=Game.CompanionPatrolCoverInFlight;
                bool heroInCorridor=Game.OuterPatrolBodyguardedThreat!=null;
                Color accent=stock<=30f?PixelArt.C("#ec7668"):stock<=60f?gold:mint;
                Panel(380,22,520,realPatrolCover||heroInCorridor?88:65,
                    new Color(.04f,.065f,.11f,.76f),
                    new Color(accent.r,accent.g,accent.b,.7f));
                Text(393,25,494,27,
                    (Game.OuterPatrolVeteranActive?
                        Game.L("精英补给保卫 · 倒计时 ","VETERAN CACHE SIEGE · "):
                        Game.L("救援岗防卫 · 倒计时 ","RELIEF OUTPOST DEFENSE · "))+
                    Math.Ceiling(Game.OuterPatrolTime)+"s"+
                    Game.L(" · 敌人剩余 "," · RAIDERS LEFT ")+
                    Game.OuterPatrolRaidersRemaining+
                    (incomingStockSeconds>=0f?
                        (incomingStockHeavy?
                            Game.L(" · 重甲即将砸击 "," · HEAVY HIT IN "):
                            Game.L(" · 物资即将遇袭 "," · CACHE HIT IN "))+
                        incomingStockSeconds.ToString("0.0")+"s":
                        Game.save.companion>=0?
                        (Game.OuterPatrolMedicRallying?
                            Game.L(" · 救援中 · 召回 Y"," · MEDIC · RECALL Y"):
                            Game.OuterPatrolCompanionGuarding?
                                Game.L(" · 召回 Y"," · RECALL Y"):
                                Game.L(" · 驻守 Y"," · GUARD Y")):""),
                    15,paper,TextAnchor.MiddleCenter);
                Text(398,49,481,18,
                    Game.L("物资耐久 ","CACHE INTEGRITY ")+
                    Math.Ceiling(stock)+"%"+
                    (Game.OuterPatrolCompanionGuarding?
                        Game.L(" · 同伴格挡 "," · ALLY BLOCKS ")+
                        Game.OuterPatrolGuardBlocksLeft:"")+
                    (Game.OuterPatrolVeteranActive?
                        Game.L(" · 路障 "," · BARRICADE ")+
                        Math.Ceiling(Game.OuterPatrolBraceIntegrity)+
                        (Game.OuterPatrolFlawlessEligible?
                            Game.L(" · 无损挑战中"," · FLAWLESS ACTIVE"):""):"")+
                    (Game.OuterPatrolPatchAvailable?
                        Game.L(" · 靠近哨岗按 X 抢修"," · NEAR POST: X TO PATCH"):"")+
                    (Game.OuterPatrolBraceAvailable?
                        Game.L(" · 靠近哨岗按 V 架路障"," · NEAR POST: V TO BRACE"):""),
                    11,accent,TextAnchor.MiddleCenter);
                Bar(401,71,478,stock/100f,accent,7);
                if(realPatrolCover||heroInCorridor)
                    Text(398,82,481,18,
                        heroInCorridor?
                            Game.L("站位正确 · 守住物资攻击线路",
                                "IN MELEE STRIKE LANE · HOLD POSITION"):
                            Game.L("同伴援射在途 · 命中才可阻止袭击",
                                "ALLY CACHE SHOT · MUST HIT BEFORE STRIKE"),
                        11,mint,TextAnchor.MiddleCenter);
            }
            else if (Game.reliefMissionRoom!=0 &&
                     Game.reliefMissionRoom==Game.save.room)
            {
                // Combat mode hides the large exploration objective. The
                // timed living escort and its real safety must stay visible,
                // particularly while the player is drawing aggro or moving
                // the wounded survivor past a shelter.
                int wave=Game.reliefMissionWave;
                float safe=Game.reliefFollowerSafety;
                Color accent=safe<=30f?PixelArt.C("#ec7668"):
                    safe<=65f?gold:mint;
                Enemy imminent=Game.IncomingRescueCivilianThreat;
                string stage=wave==0?Game.L("搜寻居民","SEARCH"):
                    wave==1?Game.L("首波伏击","FIRST AMBUSH"):
                    wave==2?Game.L("重甲伏击","HEAVY AMBUSH"):
                    Game.L("护送返程","ESCORT HOME");
                string status=imminent!=null?
                    Game.L(" · 居民遇袭倒计时 "," · CIVILIAN HIT IN ")+
                    Math.Max(0f,imminent.timer).ToString("0.0")+"s":
                    Game.CompanionRescueMedicalCastPending?
                    Game.L(" · 米拉正在包扎"," · MIRA TREATING"):
                    Game.CompanionRescueCoverInFlight?
                    Game.L(" · 掩护箭矢飞行中"," · COVER SHOT EN ROUTE"):"";
                if(Game.ReliefCompanionArrowGuardReady)
                    status+=Game.L(" · 同伴挡箭待命"," · ALLY GUARD READY");
                else if(Game.ReliefCompanionArrowGuardPositioned)
                    status+=Game.L(" · 挡箭冷却 "," · GUARD RECHARGE ")+
                        Game.ReliefCompanionArrowGuardCooldown.ToString("0.0")+"s";
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.76f),
                    new Color(accent.r,accent.g,accent.b,.7f));
                Text(394,25,492,26,
                    Game.L("居民救援 · ","CIVILIAN RESCUE · ")+stage+
                    " · "+Math.Ceiling(Math.Max(0f,Game.reliefMissionTime))+"s",
                    15,paper,TextAnchor.MiddleCenter);
                Text(398,49,480,18,
                    (wave==0?Game.L("到现场寻找失联居民","Reach the field site to locate civilians"):
                        Game.L("居民安全 ","CIVILIAN SAFETY ")+Math.Ceiling(safe)+"%")+
                    status,11,accent,TextAnchor.MiddleCenter);
                if(wave>0)Bar(401,71,478,safe/100f,accent,7);
            }
            else if (Game.MineConvoyActive && Game.save.room==3)
            {
                // During real combat, the compact HUD replaces exploration.
                // The physical cart, its independent raider pressure and
                // bodyguard contacts must still remain visible to players.
                float hull=Game.MineConvoyIntegrity;
                Color accent=hull<25f?PixelArt.C("#ec7668"):hull<45f?gold:mint;
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.76f),
                    new Color(accent.r,accent.g,accent.b,.7f));
                Text(393,25,494,26,
                    Game.MineConvoyStage==4?
                        Game.L("矿车已到站 · 前往南侧矿棚登记",
                            "CART ARRIVED · REGISTER AT DEPOT"):
                        Game.L("矿工护送 · ","MINECART ESCORT · ")+
                            Math.Ceiling(Game.MineConvoyTime)+"s",
                    15,paper,TextAnchor.MiddleCenter);
                Text(398,49,481,18,
                    Game.L("车厢 ","HULL ")+Math.Ceiling(hull)+"/80"+
                    (Game.MineConvoyStage!=2?"":
                        Game.MineConvoyBodyguarding?
                            Game.L(" · 正在拦截 "," · INTERCEPTING ")+
                                Game.MineConvoyProtectedLanes+"/"+Game.MineConvoyThreatLanes:
                            Game.L(" · 挡在袭击者与矿车之间",
                                " · STAND BETWEEN RAIDER AND CART")),
                    11,accent,TextAnchor.MiddleCenter);
                Bar(401,71,478,hull/80f,accent,7);
            }
            else if(Game.TideSalvageActive && Game.save.room==5)
            {
                // The compact HUD replaces the exploration objective during
                // live raider combat. Tide, cargo and real remaining attackers
                // must not disappear when the player needs them most.
                float remaining=Game.TideSalvageTime;
                Color accent=remaining<20f?PixelArt.C("#ec7668"):gold;
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.76f),
                    new Color(accent.r,accent.g,accent.b,.7f));
                Text(394,25,492,26,
                    Game.L("潮滩打捞 · ","TIDEBOUND SALVAGE · ")+
                    Math.Max(0,Game.TideSalvagePhase-1)+"/3 · "+
                    Math.Ceiling(remaining)+"s",
                    15,paper,TextAnchor.MiddleCenter);
                Text(398,49,480,20,
                    (Game.LowTide?Game.L("低潮 · ","LOW TIDE · "):
                        Game.L("高潮 · ","HIGH TIDE · "))+
                    (Game.TideSalvageCargoReady?
                        Game.L("货物就绪 · 返回码头交付",
                            "CARGO SECURED · RETURN TO DOCK"):
                        Game.TideSalvageRaidersAlive>0?
                            Game.L("追兵剩余 ","RAIDERS LEFT ")+
                                Game.TideSalvageRaidersAlive:
                            Game.L("追踪下一处货箱","FIND NEXT MANIFEST")),
                    11,accent,TextAnchor.MiddleCenter);
                Bar(401,72,478,(Game.TideSalvagePhase-1)/3f,accent,6);
            }
            else if(Game.FrostSignalActive && Game.save.room==7)
            {
                // Combat mode replaces the exploration HUD. Keep the living
                // ember and physically screened guard lanes readable here.
                float flame=Game.FrostSignalFlame;
                Color accent=flame<25?PixelArt.C("#ec7668"):
                    flame<50?gold:mint;
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.76f),
                    new Color(accent.r,accent.g,accent.b,.7f));
                Text(393,25,494,26,
                    Game.FrostSignalAtHearth?
                        Game.L("公共余火已到站","CIVIC EMBER BANKED"):
                        Game.L("霜钟余火护送 · ","FROSTCHIME EMBER · ")+
                            Math.Ceiling(Game.FrostSignalTime)+"s",
                    15,paper,TextAnchor.MiddleCenter);
                Text(398,49,481,18,
                    Game.L("余温 ","FLAME ")+Math.Ceiling(flame)+"/100"+
                    (Game.FrostSignalStage==2?
                        Game.FrostSignalGuardsScreened>0?
                            Game.L(" · 同伴挡风 "," · ALLY SCREENING ")+
                                Game.FrostSignalGuardsScreened:
                            Game.L(" · 让同伴挡住守卫方向",
                                " · PLACE ALLY BETWEEN GUARD & EMBER"):
                        Game.FrostSignalWindbreakTime>0?
                            Game.L(" · 防风 "," · WINDBREAK ")+
                                Math.Ceiling(Game.FrostSignalWindbreakTime)+"s":""),
                    11,accent,TextAnchor.MiddleCenter);
                Bar(401,71,478,flame/100f,accent,7);
            }
            else if (HasFallenBoss) Text(380,29,520,31,Game.L("守护者已倒下 · 战利品已收取","Guardian defeated · loot secured"),18,gold,TextAnchor.MiddleCenter);
            // Near a visible enemy the compact CombatHud replaces the
            // exploration controls. U is still an actual gameplay action:
            // keep its risk feedback and clickable physical release
            // here too, without covering the established Q/F/T/Z/dodge.
            if(Button(CombatPebbleButton.x,CombatPebbleButton.y,
                CombatPebbleButton.width,CombatPebbleButton.height,
                LivePebbleHudLabel(),false,
                Game.mode=="world"&&!Game.Dying&&!Game.practicing&&
                Game.pebbleCooldown<=0f&&
                Game.save.energy>=GameCore.PebbleEnergyCost))
                Game.ThrowPebble(Game.aim);
            Panel(426,730,428,54,new Color(.045f,.08f,.13f,.65f),new Color(.3f,.4f,.46f,.4f));
            ActionSlot(430,ControlKey("Q","Y"),SkillName(),Game.save.profession == 0 ? "sword" : Game.save.profession == 1 ? "bow" : "staff",Game.skillCooldown,Game.save.energy >= (Game.Relic(4)?50:40),()=>Game.Skill(InputAim(true)),3.5f,Game.L("耐力不足","Low energy"));
            ActionSlot(515,ControlKey("F","LB"),Game.L("汤 ","Stew ")+Game.save.inventory[6],"stew",0,Game.save.inventory[6]>0&&!Game.practicing,()=>Game.Eat());
            ActionSlot(600,ControlKey("T","PACK"),Game.L("茶 ","Tea ")+Game.save.tea,"tea",0,Game.save.tea>0&&!Game.practicing,()=>Game.DrinkTea());
            ActionSlot(685,ControlKey("Z","RB"),Game.L("同伴","Command"),"companion",Game.companionCooldown,Game.save.companion>=0&&!Game.practicing,()=>Game.CommandCompanion(),Game.save.frontierEnding==3?12:18);
            ActionSlot(770,ControlKey("SPACE","A"),Game.L("闪避","Dodge"),"shield",Game.dashCooldown,Game.save.energy>=(Game.Relic(2)?25:20),()=>Game.Dash(HudDodgeDirection()),.75f-Game.save.talents[2]*.06f,Game.L("耐力不足","Low energy"));
            if (Button(1134,746,126,36,Game.L("暂停 ","Pause ")+ControlKey("ESC","START"))) { OpenMenu("pause"); Save(); }
        }
    }
}
