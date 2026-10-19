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
        { get {if(Game.mode!="world")return false;if(Game.AfterlightWatchActive||Game.OuterPatrolActive||combatHudTime>0||CounterHud||Game.Dying||HasFallenBoss)return true;foreach(Enemy e in Game.enemies)if(e.boss||(e.Position-Game.Position).Length<8)return true;return false;} }
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
                if (CombatClimate) rects.Add(new Rect(20,103,260,26));
                if (CounterHud) rects.Add(new Rect(20,CounterHudY,260,27));
                if (Game.toastTime > 0) rects.Add(new Rect(20,CombatToastY,260,62));
                if (HasLiveBoss || HasFallenBoss || Game.AfterlightWatchActive || Game.OuterPatrolActive)
                    rects.Add(new Rect(380, 22, 520, 65));
            }
            else
            {
                rects.Add(new Rect(24,24,298,104)); rects.Add(new Rect(1008,24,248,191)); rects.Add(new Rect(1008,233,248,248));
                rects.Add(new Rect(342,696,596,80)); rects.Add(new Rect(972,718,284,52));
                if (Game.save.room >= 5 || Game.save.teaUntil > Game.save.totalTime) rects.Add(new Rect(24,145,298,60));
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
            else { hudPointerRegions.Add(regions[2]); hudPointerRegions.Add(regions[3]); hudPointerRegions.Add(CombatJournalButton); }
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
            try { if (CombatHud) DrawCombatHud(); else DrawExplorationHud(); }
            finally { drawingHud = false; }
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
            QuestGuide guide = Game.CurrentGuide;
            Panel(1030,22,230,70,new Color(.045f,.08f,.13f,.45f),new Color(gold.r,gold.g,gold.b,.4f));
            Text(1041,27,208,27,Game.practicing ? Game.L("实战教学","COMBAT PRACTICE") : guide.title,12,gold);
            Text(1041,56,136,23,(int)(guide.Progress*100)+"%  ·  "+Game.L("查看步骤","View steps"),11,paper);
            if (Button(CombatJournalButton.x,CombatJournalButton.y,CombatJournalButton.width,CombatJournalButton.height,ControlKey("TAB","VIEW"))) OpenMenu("journal");
            Enemy boss = Game.enemies.Find(e => e.boss);
            if (boss != null)
            {
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.6f),new Color(.6f,.4f,.42f,.45f));
                Text(396,26,488,27,Game.L(Game.CurrentBoss.name,Game.CurrentBoss.en)+"  /  "+boss.BossPhase,16,gold,TextAnchor.MiddleCenter);
                Bar(399,58,482,boss.hp/boss.maxHp,PixelArt.C("#d28b86"),6); Bar(399,73,482,boss.stagger/95,mint,3);
            }
            else if (Game.AfterlightWatchActive)
            {
                // This is an active, damageable OBJECTIVE, not a boss:
                // the receiver must remain readable even when it is far
                // outside the player's camera or between enemy waves.
                float stock=Game.AfterlightWatchStock;
                Color accent=stock<=30f?PixelArt.C("#ec7668"):stock<=60f?gold:mint;
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.76f),
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
            }
            else if (Game.OuterPatrolActive)
            {
                // The threatened relief cache must be readable even when
                // the hero is intercepting a raider beyond the watchpost.
                // Register this panel as HUD geometry so warning overlays
                // remain visible above its translucent body.
                float stock=Game.OuterPatrolIntegrity;
                Color accent=stock<=30f?PixelArt.C("#ec7668"):stock<=60f?gold:mint;
                Panel(380,22,520,65,new Color(.04f,.065f,.11f,.76f),
                    new Color(accent.r,accent.g,accent.b,.7f));
                Text(393,25,494,27,
                    (Game.OuterPatrolVeteranActive?
                        Game.L("精英补给保卫 · 倒计时 ","VETERAN CACHE SIEGE · "):
                        Game.L("救援岗防卫 · 倒计时 ","RELIEF OUTPOST DEFENSE · "))+
                    Math.Ceiling(Game.OuterPatrolTime)+"s"+
                    Game.L(" · 敌人剩余 "," · RAIDERS LEFT ")+
                    Game.OuterPatrolRaidersRemaining+
                    (Game.OuterPatrolIncomingStockSeconds>=0f?
                        (Game.OuterPatrolIncomingStockHeavy?
                            Game.L(" · 重甲即将砸击 "," · HEAVY HIT IN "):
                            Game.L(" · 物资即将遇袭 "," · CACHE HIT IN "))+
                        Game.OuterPatrolIncomingStockSeconds.ToString("0.0")+"s":
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
            }
            else if (HasFallenBoss) Text(380,29,520,31,Game.L("守护者已倒下 · 战利品已收取","Guardian defeated · loot secured"),18,gold,TextAnchor.MiddleCenter);
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
