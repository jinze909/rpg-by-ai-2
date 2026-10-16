using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // New 4-frame original 32px contact paintings: sword, arrow, magic.
        // This supplementary texture NEVER replaces approved Effects.png,
        // hero, enemy or Boss source art; it creates no gameplay hit event.
        private Texture2D impactFlourishSheet;
        private float impactFlourishRetryAt=float.NegativeInfinity;
        private float impactFlourishLastChecked=float.NegativeInfinity;
        // World-depth sorted optional pixel VFX: unlike gameplay hazard
        // outlines and hit-stop indicators, these embellishments must be
        // hidden by trees and buildings in FRONT of their contact point.
        private bool CanDrawAuthoredImpactFlourishes()
        {
            if(Game==null||Game.save.effectsQuality<2||
                Game.save.reducedMotion)return false;
            if(impactFlourishSheet==null||
               impactFlourishSheet.width!=128||
               impactFlourishSheet.height!=96)
            {
                if(clock<impactFlourishLastChecked||
                   clock>=impactFlourishRetryAt)
                {
                    impactFlourishLastChecked=clock;
                    impactFlourishRetryAt=clock+2f;
                    impactFlourishSheet=Resources.Load<Texture2D>(
                        "Frontier/Art/ImpactFlourish");
                }
            }
            return impactFlourishSheet!=null &&
                impactFlourishSheet.width==128 &&
                impactFlourishSheet.height==96;
        }
        private static int ImpactFlourishCategory(Effect effect)
        {
            // The same hand-painted contact artwork can echo true player
            // strikes, hits, arrows and Boss magic, not normal patrol bolts.
            return effect.kind=="slash"||
                effect.kind=="impact"||effect.kind=="hurt"?0:
                effect.kind=="projectileImpact"?1:
                effect.kind=="magicImpact"||
                (effect.kind=="enemyRelease"&&effect.variant>=10)?2:-1;
        }
        private void DrawAuthoredImpactFlourish(Effect effect)
        {
            int category=ImpactFlourishCategory(effect);
            if(category<0||effect.maxLife<=0f||
               effect.life<=0f||!WorldDrawable(effect.position.x,
                   effect.position.y,115f,115f))return;
            float phase=Mathf.Clamp01(1f-effect.life/effect.maxLife);
            int frame=Math.Min(3,(int)(phase*4f));
            float size=category==0?
                (effect.kind=="slash"?
                    (effect.variant==2?73f:57f):44f):
                category==1?38f:
                effect.kind=="enemyRelease"?65f:58f;
            Point point=ScreenPoint(effect.position);
            float lift=effect.height>=0f?effect.height:
                effect.kind=="enemyRelease"?38f:30f;
            float opacity=CurrentWorldVisualLook.impactFlourishAlpha*
                (1f-phase);
            float angle=category==0?
                (float)(Math.Atan2(effect.direction.y,
                    effect.direction.x)*180f/Math.PI):0f;
            Atlas(impactFlourishSheet,4,3,category*4+frame,
                new Rect(point.x-size*.5f,point.y-lift-size*.5f,
                    size,size),
                new Color(1f,1f,1f,opacity),false,angle);
        }
        // Independent QA entrypoint: tests sample the real source cells
        // directly, while DrawOrderedWorld owns actual in-scene occlusion.
        private void DrawAuthoredImpactFlourishes()
        {
            if(!CanDrawAuthoredImpactFlourishes())return;
            foreach(Effect effect in Game.effects)
                DrawAuthoredImpactFlourish(effect);
        }

        // A small, source-authored, alpha-only-detail Sprite sheet contains
        // four 24px motifs: forest fireflies, blown sand, coastal spray and
        // snow crystals. VFX remains world-rooted, never a stand-in for the
        // real character SpriteRenderer/Animator production architecture.
        private Texture2D weatherWispSheet;
        private float weatherWispNextImportCheck=float.NegativeInfinity;
        private float weatherWispLastImportClock=float.NegativeInfinity;
        // Biome-specific air motion uses genuine illustrated Effects.png
        // star/speck pixels. The old 18 looping, screen-space square boxes
        // looked identical in forest, snow and volcanic areas, and snapped
        // under camera panning. Samples are world-seeded, pinned near their
        // own source tile, and fade instead of teleported screen confetti.
        // Small drifting flecks are VFX, not gameplay Animator actors.
        private void DrawAuthoredAtmosphericMotes()
        {
            if (Game.save.reducedMotion || Game.save.effectsQuality==0)
                return;
            int region=Game.save.room;
            // Citadel interiors and abyss dungeons have their own authored
            // sources/light cues; do not paint outdoor fireflies across them.
            if (region==4 || region==9) return;
            bool authoredWisps=weatherWispSheet!=null &&
                weatherWispSheet.width==96&&weatherWispSheet.height==24;
            if(!authoredWisps &&
               (clock<weatherWispLastImportClock ||
                clock>=weatherWispNextImportCheck))
            {
                // During Unity art reimport a temporarily missing/wrong-
                // dimension atlas must never leave permanent coarse VFX or
                // trigger Resources.Load on EVERY world repaint. Re-attempt
                // once per two seconds, including timeline/review rewind.
                weatherWispLastImportClock=clock;
                weatherWispNextImportCheck=clock+2f;
                weatherWispSheet=Resources.Load<Texture2D>(
                    "Frontier/Art/WeatherWisps");
                authoredWisps=weatherWispSheet!=null &&
                    weatherWispSheet.width==96&&weatherWispSheet.height==24;
            }
            // Never blank out ambience if Unity has not imported the new
            // art yet. Its original approved Effects.png is a safe fallback.
            if(!authoredWisps)
            {
                if(effectSheet==null)RecoverPublishedVisualAtlases();
                if(effectSheet==null)return;
            }
            int density=Game.save.effectsQuality==1?13:8;
            WorldVisualLook look=CurrentWorldVisualLook;
            float baseOpacity=Game.Night?
                look.ambientMoteNightAlpha:look.ambientMoteDayAlpha;
            Color ink=region==0||region==1?
                new Color(.80f,.95f,.67f,1f):region==2?
                new Color(.77f,.89f,.90f,1f):region==3?
                new Color(1f,.72f,.53f,1f):region==5?
                new Color(.72f,.91f,1f,1f):region==6?
                new Color(1f,.84f,.61f,1f):region==7?
                new Color(.82f,.92f,1f,1f):
                new Color(.88f,.81f,1f,1f);
            int cx=(int)Math.Floor(camera.x/5f);
            int cy=(int)Math.Floor(camera.y/5f);
            for(int gy=cy-5;gy<=cy+5;gy++)
                for(int gx=cx-5;gx<=cx+5;gx++)
                {
                    int seed=TileSeed(gx,gy,region);
                    if(seed%density!=0)continue;
                    Point anchor=new Point(gx*5f+.3f+
                        (seed%337)/100f,gy*5f+.4f+
                        ((seed/337)%371)/110f);
                    // Camera pan changes only the projection, never the
                    // world seed. Snow gusts sideways, warm desert dust
                    // drifts along the horizon, and coast spray lifts;
                    // forest motes retain their existing soft float.
                    // All are smooth, short-range motions of the ORIGINAL
                    // painted source cell, never resynthesized body pixels.
                    float phase=(seed%211)*.071f;
                    float flicker=.5f+.5f*(float)Math.Sin(clock*.9f+phase);
                    float windX=region==7?look.snowMoteWindX:
                        region==6?look.desertMoteWindX:
                        region==5?.10f:.06f;
                    float windY=region==7?.11f:region==6?.035f:
                        region==5?look.coastMoteLift:.08f;
                    Point drift=anchor+new Point(
                        (float)Math.Sin(clock*.35f+phase)*windX,
                        (float)Math.Sin(clock*.44f+phase*1.3f)*windY);
                    if(!WorldDrawable(drift.x,drift.y,18f,18f))continue;
                    Point p=ScreenPoint(drift);
                    float alpha=baseOpacity*(.25f+.75f*flicker);
                    float size=region==7?9f:region==6?7f:8f;
                    int motif=region==7?3:region==6||region==3?1:
                        region==5||region==2?2:0;
                    if(authoredWisps)
                        Atlas(weatherWispSheet,4,1,motif,
                            new Rect(p.x-size*.5f,p.y-size*.5f,
                                size,size),
                            new Color(ink.r,ink.g,ink.b,alpha));
                    else
                        Atlas(effectSheet,4,4,13,
                            new Rect(p.x-size*.5f,p.y-size*.5f,
                                size,size),
                            new Color(ink.r,ink.g,ink.b,alpha));
                }
        }

        private void DrawProjectileVisual(Projectile projectile)
        {
            if (effectSheet == null) RecoverPublishedVisualAtlases();
            Point p = ScreenPoint(projectile.position); string visual = projectile.visual ?? (projectile.friendly ? projectile.explosive ? "starfire" : "arrow" : "enemyOrb");
            float height = projectile.height; p.y -= height;
            bool arrow = visual == "arrow" || visual == "enemyArrow";
            int id = arrow ? 8 : visual == "starfire" ? 9 : 10;
            float angle = (float)(Math.Atan2(projectile.velocity.y, projectile.velocity.x) * 180 / Math.PI);
            float size = arrow ? 30 : projectile.explosive ? 28 : 22;
            Color tint = projectile.friendly ? Color.white : BossEffectColor(projectile.theme);
            // Tail cells are the original illustrated Effects.png pixels.
            // Previously each ghost materialized at FULL alpha as soon as a
            // discrete distance threshold was crossed: fast arrows showed
            // three visible duplicate silhouettes in successive frames.
            // Ease each independent tail IN over 0.18 world units. Nothing
            // changes projectile collision, velocity or real emission time.
            // Reduced motion never adds trailing moving silhouettes.
            if(effectSheet!=null && Game.save.effectsQuality>0 &&
                !Game.save.reducedMotion)
            {
                // Derive tail growth from the CURRENT flight direction,
                // not the inbound age accumulated before a warrior parry.
                // The gameplay projectile (and its hitbox/age) never resets.
                float travelled=Math.Max(0f,
                    projectile.age-projectile.trailResetAt)*
                    projectile.velocity.Length;
                int maxTrail=Game.save.effectsQuality==1?2:3;
                for(int i=maxTrail;i>0;i--)
                {
                    float fade=Mathf.Clamp01(
                        (travelled-i*.18f)/
                        CurrentWorldVisualLook.projectileTrailFadeDistance);
                    if(fade<=0f)continue;
                    Point tail=ScreenPoint(projectile.position-
                        projectile.velocity.Normalized*i*.18f);
                    tail.y-=height;
                    Atlas(effectSheet,4,4,arrow?11:12,
                        new Rect(tail.x-size/2,tail.y-size/2,size,size),
                        new Color(tint.r,tint.g,tint.b,
                            .42f/i*fade),false,angle);
                }
            }
            if (effectSheet != null) Atlas(effectSheet, 4, 4, id, new Rect(p.x - size / 2, p.y - size / 2, size, size), tint, false, arrow ? angle : 0);
            else { Box(p.x - 4, p.y - 4, 8, 8, projectile.friendly ? gold : PixelArt.C("#e39382")); }
        }
        private bool DrawAuthoredEffect(Effect effect)
        {
            if (effectSheet == null || iconSheet == null)
                RecoverPublishedVisualAtlases();
            if (effect.kind == "number" || effect.kind == "break") return false;
            // Loot needs Icons.png, not Effects.png. A missing VFX atlas
            // must not suppress an otherwise valid detailed item icon.
            if (effect.kind != "loot" && effectSheet == null) return false;
            if (effect.kind == "loot" && iconSheet == null &&
                effectSheet == null) return false;
            Point p = ScreenPoint(effect.position); float progress = Mathf.Clamp01(1 - effect.life / Math.Max(.001f, effect.maxLife));
            if (effect.kind == "loot")
            {
                int item = effect.variant == 5 ? 9 : effect.variant == 4 ? 8 : effect.variant == 1 ? 5 : 6;
                float lift = (float)Math.Sin(progress * Math.PI) * 25;
                Rect destination = new Rect(p.x + 12, p.y - 15 - lift, 24, 24);
                Color tint = new Color(1, 1, 1, Math.Min(1, (1 - progress) * 2));
                if (iconSheet != null)
                    Atlas(iconSheet, 4, 4, item, destination, tint);
                else
                    // Previously this silently submitted a NULL texture and
                    // returned success, making a pickup effect invisible.
                    // Retain an original authored Effects.png sparkle until
                    // the illustrated inventory atlas is imported.
                    Atlas(effectSheet, 4, 4, 13, destination, tint);
                return true;
            }
            int id; float size = 45, angle = 0;
            switch (effect.kind)
            {
                // Two real hand-painted contact silhouettes, NOT a moving
                // billboard of the same static frame. The GameCore event
                // still owns timing and damage; only the visual FX lifetime
                // chooses anticipation -> follow-through.
                case "slash":
                    id = progress < .38f ? 0 : 1;
                    size = effect.variant == 2 ? 100 : 78;
                    angle = (float)(Math.Atan2(effect.direction.y,
                        effect.direction.x) * 180 / Math.PI); break;
                case "spark": id = 3; size = 42; break;
                // The source Effects.png already contains a luminous
                // contact spark (cell 3) and weighted dust/splinter burst
                // (cell 2). Keep the same world pivot through the transition.
                case "impact": case "hurt":
                    id = progress < .32f ? 3 : 2;
                    size = 38; break;
                case "projectileImpact":
                    id = progress < .32f ? 3 : 4;
                    size = 35; break;
                case "projectileFade": id = effect.variant; size = 26; angle = (float)(Math.Atan2(effect.direction.y, effect.direction.x) * 180 / Math.PI); break;
                case "magicImpact": id = 5; size = effect.radius > 0 ? effect.radius * Tile * 2 : 67; break;
                case "release": id = 13; size = 33; break;
                case "frost": id = 6; size = 4 * Tile * 2; break;
                case "burst": id = effect.variant == 0 ? 7 : 5; size = effect.radius > 0 ? effect.radius * Tile * 2 : 145; break;
                case "heal": id = 13; size = 65; break;
                case "defeat": id = 15; size = effect.variant == 1 ? 90 : 42; break;
                case "discover": id = 13; size = 56; break;
                case "danger": id = BossEffectFrame(effect.variant); size = effect.radius > 0 ? effect.radius * Tile * 2 : 116; break;
                case "enemyRelease":
                    int room = effect.variant >= 10 ? effect.variant - 10 : 0;
                    id = room > 0 ? BossEffectFrame(room) : effect.variant == 2 ? 7 : effect.variant == 1 ? 13 : 0;
                    size = room > 0 ? 96 : effect.variant == 2 ? 94 : 48;
                    angle = id == 0 || id == 1 || id == 12 ? (float)(Math.Atan2(effect.direction.y, effect.direction.x) * 180 / Math.PI) : 0; break;
                case "dash": id = 11; size = 38; angle = (float)(Math.Atan2(effect.direction.y, effect.direction.x) * 180 / Math.PI); break;
                default: id = 14; size = 52; break;
            }
            // Discrete authored texture plus a short, restrained expansion and fade.
            size *= .8f + progress * .35f;
            Color color = new Color(1, 1, 1, Math.Min(1, (1 - progress) * 1.7f));
            if (effect.kind == "danger" || effect.kind == "enemyRelease" && effect.variant >= 10)
            { Color theme = BossEffectColor(effect.kind == "danger" ? effect.variant : effect.variant - 10); color.r = theme.r; color.g = theme.g; color.b = theme.b; }
            if (effect.kind == "projectileFade") color.a *= .65f;
            if (effect.kind != "slash" && effect.kind != "impact" && effect.kind != "spark" && effect.kind != "projectileImpact") color.a *= Math.Min(1, progress * 12 + .15f);
            if (IsGroundEffect(effect)) color.a *= effect.kind == "frost" ? .42f : .62f;
            if (effect.kind == "heal") color = new Color(.65f, 1, .8f, color.a);
            float height = effect.kind == "impact" || effect.kind == "hurt" || effect.kind == "spark" || effect.kind == "slash" || effect.kind == "projectileImpact" || effect.kind == "magicImpact" || effect.kind == "release" || effect.kind == "heal" ? 30 : effect.kind == "defeat" ? 18 : effect.kind == "enemyRelease" && id != 7 ? 38 : 0;
            if (effect.height >= 0) height = effect.height;
            Rect contactRect=new Rect(p.x-size/2,p.y-size/2-height,
                size,size);
            // Hit reactions previously replaced the bright contact star
            // with an unrelated dust/deflection cell in ONE repaint. This
            // made an otherwise continuous melee/arrow impact visibly pop.
            // Overlap the two ORIGINAL hand-painted whole effect Sprites on
            // the SAME GameCore contact pivot for just 16% of the effect
            // lifetime; no new animation clock, collision or fake body art.
            float contactWindow=CurrentWorldVisualLook.hitContactBlendFraction;
            float beginContact=.32f-contactWindow*.5f;
            float endContact=.32f+contactWindow*.5f;
            bool blendContact=Game.save.effectsQuality>0 &&
                !Game.save.reducedMotion &&
                (effect.kind=="impact"||effect.kind=="hurt"||
                 effect.kind=="projectileImpact") &&
                progress>beginContact&&progress<endContact;
            // A sword previously teleported between its two WHOLE authored
            // slash silhouettes at 38% effect progress. Interleave the
            // original cell 0 and 1 only around that existing seam.
            // No extra hit event, independent Animator, weapon translation,
            // collision extension or invented body parts.
            float slashWindow=CurrentWorldVisualLook.slashBridgeFraction;
            float slashBegin=.38f-slashWindow*.5f;
            bool blendSlash=Game.save.effectsQuality>0 &&
                !Game.save.reducedMotion&&effect.kind=="slash"&&
                progress>slashBegin&&
                progress<slashBegin+slashWindow;
            if(blendContact||blendSlash)
            {
                float start=blendSlash?slashBegin:beginContact;
                float duration=blendSlash?slashWindow:contactWindow;
                float mix=Mathf.Clamp01((progress-start)/duration);
                int sourceCell=blendSlash?0:3;
                int releaseCell=blendSlash?1:
                    effect.kind=="projectileImpact"?4:2;
                Atlas(effectSheet,4,4,sourceCell,contactRect,
                    new Color(color.r,color.g,color.b,color.a*(1f-mix)),
                    false,angle);
                Atlas(effectSheet,4,4,releaseCell,contactRect,
                    new Color(color.r,color.g,color.b,color.a*mix),
                    false,angle);
            }
            else Atlas(effectSheet,4,4,id,contactRect,color,false,angle);
            // A spell currently has a fine, illustrated teal vortex but its
            // point of contact looks static. Use the ORIGINAL authored
            // Effects.png contact star (cell 13) as a short-lived second
            // whole-Sprite light layer, on the SAME contact pivot and on the
            // actual GameCore Effect clock. No new low-res particles, extra
            // damage event, time offset, or free-running Animator is added.
            if (effect.kind == "magicImpact" &&
                Game.save.effectsQuality > 1 && !Game.save.reducedMotion &&
                progress < .60f)
            {
                float sparkle = size * (.44f + progress * .12f);
                float alpha = color.a * .68f * (1f - progress / .60f);
                Atlas(effectSheet, 4, 4, 13,
                    new Rect(p.x - sparkle / 2f,
                        p.y - height - sparkle / 2f,sparkle,sparkle),
                    new Color(.91f,1f,.92f,alpha));
            }
            if (effect.kind == "danger" || effect.kind == "frost" || effect.kind == "burst" || effect.kind == "magicImpact" && effect.radius > 0)
            {
                float radius = effect.kind == "frost" ? 4 : effect.kind == "burst" || effect.kind == "magicImpact" ? effect.radius : effect.radius > 0 ? effect.radius : .4f + progress * 1.3f;
                Color ring = effect.kind == "frost" || effect.kind == "magicImpact" || effect.kind == "burst" && effect.variant == 2 ? mint : effect.kind == "danger" ? PixelArt.C("#df8a68") : gold;
                if (radius > 0) Ring(effect.position, radius, new Color(ring.r, ring.g, ring.b, .48f * (1 - progress)), 32);
            }
            return true;
        }
        private static bool IsGroundEffect(Effect effect)
        { return effect.kind == "danger" || effect.kind == "frost" || effect.kind == "burst"; }
        private static int BossEffectFrame(int room)
        { return room == 1 ? 1 : room == 2 ? 12 : room == 3 ? 7 : room == 4 ? 5 : room == 5 ? 12 : room == 6 ? 7 : room == 7 ? 6 : room == 8 ? 5 : 7; }
        private static Color BossEffectColor(int room)
        { return room == 2 || room == 5 ? new Color(.7f, .9f, .82f) : room == 7 ? new Color(.75f, .87f, 1) : room == 4 || room == 8 ? new Color(.87f, .81f, 1) : room == 6 ? new Color(1, .83f, .6f) : new Color(1, .9f, .78f); }
        // One continuous, thin alert stroke instead of widely separated
        // 2x2 white GUI pixels. Line endpoints are placed in the same
        // GameCore world-space damage geometry as the old warning. Rotating
        // the complete segment in logical-canvas space also works with
        // letterboxing/ultrawide GameView and never mutates collision.
        private void DrawWarningStroke(Point a, Point b, Color color)
        {
            Point delta=b-a;
            float length=delta.Length;
            if(length<.001f)return;
            float theta=(float)(Math.Atan2(delta.y,delta.x)*180/Math.PI);
            Point mid=(a+b)*.5f;
            Matrix4x4 original=GUI.matrix;
            try
            {
                GUI.matrix=GuiGeometry.RotateAroundPivot(original,
                    new Vector2(mid.x,mid.y),theta);
                float width=CurrentWorldVisualLook.combatWarningStrokePixels;
                Box(mid.x-length*.5f-width*.25f,
                    mid.y-width*.5f,
                    length+width*.5f,width,color);
            }
            finally {GUI.matrix=original;}
        }
        private void DrawWarningArc(Point location,float radius,
            float initial,float sweep,Color color,int segments)
        {
            if(radius<=0f||segments<1)return;
            Point center=ScreenPoint(location);
            float pixels=radius*Tile;
            float step=sweep/segments;
            Point prior=new Point(center.x+(float)Math.Cos(initial)*pixels,
                center.y+(float)Math.Sin(initial)*pixels);
            for(int i=1;i<=segments;i++)
            {
                float next=initial+i*step;
                Point end=new Point(center.x+(float)Math.Cos(next)*pixels,
                    center.y+(float)Math.Sin(next)*pixels);
                DrawWarningStroke(prior,end,color);
                prior=end;
            }
        }
        private void DrawTelegraph(Enemy enemy)
        {
            float radius = GameCore.EnemyAreaRadius(enemy);
            float duration = enemy.windupDuration > 0 ? enemy.windupDuration : enemy.boss ? 1.1f : .65f;
            float progress = Mathf.Clamp01(1 - enemy.timer / duration);
            Color warning = PixelArt.C("#df8a68");
            if(enemy.boss && (enemy.target-enemy.Position).Length>3)
                DrawWarningStroke(ScreenPoint(enemy.Position),
                    ScreenPoint(enemy.target),
                    new Color(warning.r,warning.g,warning.b,.18f+progress*.15f));
            DrawWarningArc(enemy.target,radius,0f,
                (float)(Math.PI*2),new Color(warning.r,warning.g,warning.b,.85f),36);
            DrawWarningArc(enemy.target,radius*(.2f+progress*.8f),0f,
                (float)(Math.PI*2),new Color(warning.r,warning.g,warning.b,
                .35f+progress*.35f),24);
            Point p = ScreenPoint(enemy.target);
            Atlas(effectSheet, 4, 4, 14, new Rect(p.x - radius * Tile, p.y - radius * Tile, radius * Tile * 2, radius * Tile * 2), new Color(warning.r, warning.g, warning.b, .1f + progress * .13f));
        }
        private void DrawEnemyWarning(Enemy enemy)
        {
            if(enemy.state!=1)return;
            // A civilian-targeted strike (traveler or rescue) uses the same
            // locked simulation target as its actual damage or projectile.
            // Draw a slender, always-visible interception lane even in reduced
            // motion; never flood the arena with an opaque HUD.
            bool targetedCivilian =
                enemy.targetingWayfarer && Game.WayfarerEncounterRoom == Game.save.room ||
                enemy.targetingReliefCivilian &&
                    Game.reliefMissionRoom == Game.save.room &&
                    (Game.reliefMissionWave >= 1 && Game.reliefMissionWave <= 3);
            if (targetedCivilian)
            {
                float laneProgress = Mathf.Clamp01(1 - enemy.timer / Math.Max(.01f, enemy.windupDuration));
                Color tether = PixelArt.C("#f4b58b");
                float opacity = Game.save.reducedMotion ? .4f : .24f + laneProgress * .25f;
                DrawWarningStroke(ScreenPoint(enemy.Position),
                    ScreenPoint(enemy.target),
                    new Color(tether.r,tether.g,tether.b,opacity));
                DrawWarningArc(enemy.target,.34f,0f,(float)(Math.PI*2),
                    new Color(tether.r,tether.g,tether.b,.65f),16);
                if (enemy.targetingReliefCivilian && enemy.kind == 1)
                {
                    // The projectile can be blocked mid-flight; warn about
                    // the arrow's actual shot lane, not a ground shockwave.
                    Point muzzle = GameCore.WeaponOrigin(enemy.Position, 1, enemy.facing);
                    DrawWarningStroke(ScreenPoint(muzzle),
                        ScreenPoint(enemy.target),
                        new Color(tether.r,tether.g,tether.b,
                            Game.save.reducedMotion ? .6f :
                            .35f+laneProgress*.4f));
                }
            }
            if(enemy.boss||enemy.kind==2){DrawTelegraph(enemy);return;}
            float progress=Mathf.Clamp01(1-enemy.timer/Math.Max(.01f,enemy.windupDuration));
            Color color=new Color(.875f,.54f,.408f,.35f+progress*.4f);
            if(enemy.kind==1)
            {
                Point muzzle=GameCore.WeaponOrigin(enemy.Position,1,enemy.facing),lift=new Point(0,GameCore.WeaponHeight(1,enemy.facing)/Tile);
                DrawWarningStroke(ScreenPoint(muzzle-lift),
                    ScreenPoint(enemy.target-lift),color);
                return;
            }
            // Readability uses the same center, radius and angular bound as damage.
            // This thin open sector supplements the claw wind-up without covering it.
            float angle=(float)Math.Atan2(enemy.facing.y,enemy.facing.x),half=(float)Math.Acos(GameCore.EnemyMeleeCosine);
            // The previous 33 disconnected square dots flickered into a
            // jagged, barely legible arc. The new stitched stroke follows the
            // identical GameCore melee cosine/radius and facing.
            DrawWarningArc(enemy.Position,GameCore.EnemyMeleeRadius,
                angle-half,half*2f,color,24);
            for(int side=-1;side<=1;side+=2)
            {
                float a=angle+half*side;
                Point edge=enemy.Position+new Point((float)Math.Cos(a),(float)Math.Sin(a))*GameCore.EnemyMeleeRadius;
                DrawWarningStroke(ScreenPoint(enemy.Position),
                    ScreenPoint(edge),
                    new Color(color.r,color.g,color.b,color.a*.45f));
            }
        }
    }
}
