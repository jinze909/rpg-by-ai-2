using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private enum WorldDrawKind { Decoration, Object, Enemy, Fallen, Player, Companion, Survivor, ImpactFlourish, Footfall }
        private struct WorldDrawEntry
        {
            public float depth;
            public int order;
            public WorldDrawKind kind;
            public Decoration decoration;
            public WorldObject obj;
            public Enemy enemy;
            public FallenActor fallen;
            public Effect effect;
            public FootfallMark footfall;
        }
        private sealed class WorldDepthComparer:IComparer<WorldDrawEntry>
        {
            public int Compare(WorldDrawEntry a,WorldDrawEntry b)
            {int depth=a.depth.CompareTo(b.depth);return depth!=0?depth:a.order.CompareTo(b.order);}
        }
        private static readonly WorldDepthComparer worldDepthComparer=new WorldDepthComparer();
        private readonly List<WorldDrawEntry> worldDrawEntries=new List<WorldDrawEntry>(256);
        // The accepted NPC footprints visible in THIS world's depth pass.
        // Collect once, not once for each swaying tree/ruin. The separate
        // DrawWorldDecoration review entrypoint retains its safe map fallback.
        private readonly List<WorldObject> coverResidentsInView=
            new List<WorldObject>(64);
        private bool coverResidentsPrepared;
        private static bool FiniteWorldCoordinate(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool FiniteWorldPoint(Point point)
        { return FiniteWorldCoordinate(point.x) && FiniteWorldCoordinate(point.y); }
        private static bool IsValidWorldEnemy(Enemy enemy)
        { return enemy != null && FiniteWorldCoordinate(enemy.x) &&
            FiniteWorldCoordinate(enemy.y); }
        private static bool IsValidEnemyWarning(Enemy enemy)
        {
            return IsValidWorldEnemy(enemy) &&
                FiniteWorldPoint(enemy.target) &&
                FiniteWorldPoint(enemy.facing);
        }
        private void AddWorldEntry(WorldDrawEntry entry)
        {entry.order=worldDrawEntries.Count;worldDrawEntries.Add(entry);}
        private bool WorldDrawable(float x, float y, float horizontalMargin = 230,
            float verticalMargin = 250)
        {
            if(!FiniteWorldCoordinate(x)||!FiniteWorldCoordinate(y))
                return false;
            Point screen = ScreenPoint(new Point(x, y));
            return FiniteWorldPoint(screen) &&
                screen.x > -horizontalMargin && screen.x < Width + horizontalMargin &&
                screen.y > -verticalMargin && screen.y < Height + verticalMargin;
        }
        private float FadeWorldProp(float x,float y)
        {
            // VISUAL RESTORATION: original detailed WorldProps and FieldProps
            // (lamps, signposts, bridges, buildings and foreground foliage)
            // must never disappear or become translucent merely because
            // their anchor falls near a viewport/chunk edge. WorldDrawable
            // still bounds work; actual actor occlusion remains unchanged.
            return 1f;
        }
        // Filter before reading actor coordinates: DrawFallen's own guard
        // is too late if the depth-sort pass already dereferenced the record.
        private bool CanDrawFallen(FallenActor fallen)
        {
            return fallen != null && fallen.IsValid &&
                fallen.room == Game.save.room &&
                WorldDrawable(fallen.actor.x,fallen.actor.y);
        }

        private void DrawOrderedWorld()
        {
            // Reuse storage and explicit references. Captured per-actor callbacks
            // used to allocate on every IMGUI pass; equal-depth ties also flickered.
            coverResidentsPrepared=false;
            coverResidentsInView.Clear();
            worldDrawEntries.Clear();
            foreach(LightRay ray in Game.LightRays())DrawRay(ray.from,ray.to,gold);
            // Artwork is already loaded and nearby terrain prewarmed. Avoid
            // sorting and invoking GUI props for hundreds of remote actors.
            // The 230x250 margin exceeds even the tallest authored canopy.
            foreach(Decoration d in Game.Map.decor)
                if(d!=null && !String.IsNullOrEmpty(d.kind) &&
                   WorldDrawable(d.x,d.y))
                    AddWorldEntry(new WorldDrawEntry{depth=d.y,kind=WorldDrawKind.Decoration,decoration=d});
            foreach(WorldObject o in Game.Map.objects)
                if(o!=null && !String.IsNullOrEmpty(o.kind) &&
                   WorldDrawable(o.x,o.y))
                {
                    AddWorldEntry(new WorldDrawEntry{depth=o.y+.01f,kind=WorldDrawKind.Object,obj=o});
                    if(IsCoverResident(o.kind) && Game.IsObjectPresent(o))
                        coverResidentsInView.Add(o);
                }
            foreach(Enemy e in Game.enemies)
            {
                // Enemy bodies are culled independently from attack targets.
                // A visible landing zone must NEVER lose its warning merely
                // because its long-range caster is beyond actor draw margin.
                if(IsValidWorldEnemy(e) && WorldDrawable(e.x,e.y,450,450))
                    AddWorldEntry(new WorldDrawEntry{depth=e.y,kind=WorldDrawKind.Enemy,enemy=e});
            }
            foreach(FallenActor f in Game.fallen)
                if(CanDrawFallen(f))
                    AddWorldEntry(new WorldDrawEntry{depth=f.actor.y-.02f,kind=WorldDrawKind.Fallen,fallen=f});
            // Optional impact paint is WORLD scenery, not a gameplay
            // warning. Respect foreground canopies, NPCs and hero weapons
            // through the same source-foot depth ordering as real actors.
            // Actual hit telegraphs stay in DrawVisibleEnemyWarnings.
            if(CanDrawAuthoredImpactFlourishes())
                foreach(Effect effect in Game.effects)
                    if(ValidEffectForDraw(effect) &&
                       ImpactFlourishCategory(effect)>=0 &&
                       WorldDrawable(effect.position.x,effect.position.y,115f,115f))
                        AddWorldEntry(new WorldDrawEntry{
                            depth=effect.position.y+.001f,
                            kind=WorldDrawKind.ImpactFlourish,effect=effect});
            // Material-aware foot contacts are rooted to their ORIGINAL
            // terrain position, not screen-space ankle offsets. Sort below
            // bodies and prop occluders just like other world details.
            foreach(FootfallMark contact in footfallMarks)
                if(IsValidFootfallMark(contact) &&
                   contact.room==Game.save.room &&
                   WorldDrawable(contact.position.x,contact.position.y,40,40))
                    AddWorldEntry(new WorldDrawEntry{
                        depth=contact.position.y-.035f,
                        kind=WorldDrawKind.Footfall,footfall=contact});
            AddWorldEntry(new WorldDrawEntry{depth=Game.save.y+.002f,kind=WorldDrawKind.Player});
            if(Game.save.companion>=0)AddWorldEntry(new WorldDrawEntry{depth=Game.companionPosition.y,kind=WorldDrawKind.Companion});
            // Keep the moving rescue survivor in the same depth pass as every
            // hero, NPC, tree and enemy. Painting her as a late HUD overlay
            // made her appear on top of walls and the golden-haired hero.
            if(Game.ReliefCivilianVisible)
                AddWorldEntry(new WorldDrawEntry{
                    depth=Game.reliefFollowerPosition.y+.001f,kind=WorldDrawKind.Survivor});
            worldDrawEntries.Sort(worldDepthComparer);
            try
            {
                coverResidentsPrepared=true;
                foreach(WorldDrawEntry entry in worldDrawEntries)
                {
                    // No object blinks into being when its base tile first
                    // crosses the view. Long/tall sprites soften outside it.
                    worldActorOpacity=entry.kind==WorldDrawKind.Decoration ?
                        FadeWorldProp(entry.decoration.x,entry.decoration.y):
                        entry.kind==WorldDrawKind.Object ?
                            FadeWorldProp(entry.obj.x,entry.obj.y):1f;
                    switch(entry.kind)
                    {
                        case WorldDrawKind.Decoration:DrawWorldDecoration(entry.decoration);break;
                        case WorldDrawKind.Object:DrawObject(entry.obj);break;
                        case WorldDrawKind.Enemy:DrawEnemyWithBars(entry.enemy);break;
                        case WorldDrawKind.Fallen:DrawFallen(entry.fallen);break;
                        case WorldDrawKind.Player:DrawHero();DrawFarCourierPack();DrawCivicResponsePack();DrawCivicConduitCoil();break;
                        case WorldDrawKind.Companion:DrawCompanionActor();break;
                        case WorldDrawKind.Survivor:DrawRescueSurvivorActor();break;
                        case WorldDrawKind.ImpactFlourish:DrawAuthoredImpactFlourish(entry.effect);break;
                        case WorldDrawKind.Footfall:DrawFootfallMark(entry.footfall);break;
                    }
                }
            }
            finally
            {
                coverResidentsPrepared=false;
                coverResidentsInView.Clear();
                worldActorOpacity=1f;
                worldDrawEntries.Clear();
            }
            DrawVisibleEnemyWarnings();
        }
        // Combat preview outlines are gameplay signals, not scenery. Draw
        // AFTER depth-sorted trees, NPCs, the hero and Boss bodies so opaque
        // canopies/ruins cannot erase a real locked hit radius or rescue
        // interception lane. The existing slim strokes and subdued fill
        // preserve the character silhouette; damage/timing are unchanged.
        // An archer or distant Boss can lock a shot whose ENDPOINTS are
        // both off-screen even though the actual shot lane crosses the hero's
        // whole viewport. Endpoint-only culling silently erased its warning.
        // Liang-Barsky tests the whole world-rooted line against the visible
        // logical game canvas without iterating 36px samples per projectile.
        private static bool ClipWarningLane(float p,float q,
            ref float low,ref float high)
        {
            if(Math.Abs(p)<.00001f)return q>=0f;
            float intersection=q/p;
            if(p<0f)
            {
                if(intersection>high)return false;
                if(intersection>low)low=intersection;
            }
            else
            {
                if(intersection<low)return false;
                if(intersection<high)high=intersection;
            }
            return true;
        }
        // Reuse ONE exact viewport clip for both visibility and painting.
        // Huge distant 2000px+ attack strokes no longer submit unbounded
        // rectangles across an otherwise tiny visible region.
        private bool ClipWarningScreenSegment(ref Point a,ref Point b)
        {
            float dx=b.x-a.x,dy=b.y-a.y;
            const float margin=36f;
            float low=0f,high=1f;
            if(!ClipWarningLane(-dx,a.x+margin,ref low,ref high)||
               !ClipWarningLane(dx,Width+margin-a.x,ref low,ref high)||
               !ClipWarningLane(-dy,a.y+margin,ref low,ref high)||
               !ClipWarningLane(dy,Height+margin-a.y,ref low,ref high)||
               low>high)return false;
            Point start=a;
            a=new Point(start.x+dx*low,start.y+dy*low);
            b=new Point(start.x+dx*high,start.y+dy*high);
            return true;
        }
        private bool WarningLaneCrossesScreen(Point source,Point target)
        {
            Point a=ScreenPoint(source),b=ScreenPoint(target);
            return ClipWarningScreenSegment(ref a,ref b);
        }
        private bool WarningFootprintVisible(Enemy enemy)
        {
            // A circle can touch a screen border with its center outside,
            // but a rectangular center-margin test spuriously flags diagonal
            // screen corners. Measure the true circle-to-viewport distance,
            // using the authoritative GameCore damage radius in canvas px.
            Point center=ScreenPoint(enemy.target);
            float radius=GameCore.EnemyAreaRadius(enemy)*Tile;
            float dx=Math.Max(0f,Math.Max(-center.x,center.x-Width));
            float dy=Math.Max(0f,Math.Max(-center.y,center.y-Height));
            return dx*dx+dy*dy<=(radius+2f)*(radius+2f);
        }
        private void DrawVisibleEnemyWarnings()
        {
            foreach(Enemy e in Game.enemies)
            {
                if(!IsValidEnemyWarning(e)||e.state!=1)continue;
                bool areaAttack=e.boss||e.kind==2;
                bool anchored=WorldDrawable(e.x,e.y,450,450) ||
                    !areaAttack&&WorldDrawable(e.target.x,e.target.y,120,120);
                bool area=areaAttack&&WarningFootprintVisible(e);
                // For melee there is no offscreen line to recover. Archers
                // use the SAME GameCore weapon origin as their emitted bolt;
                // Bosses only draw a long lane if their lock exceeds 3 tiles.
                bool longLane=e.kind==1 ||
                    e.boss&&(e.target-e.Position).Length>3f ||
                    e.targetingWayfarer||e.targetingReliefCivilian;
                Point origin=e.kind==1?
                    GameCore.WeaponOrigin(e.Position,1,e.facing):
                    e.Position;
                if(anchored||area||
                    longLane&&WarningLaneCrossesScreen(origin,e.target))
                    DrawEnemyWarning(e);
            }
        }
        // Canopy/ruin occlusion is a presentation-only mask. Test every
        // actor that actually enters the depth-sorted world pass; a companion
        // or rescued civilian must never disappear behind opaque foliage
        // while the player/enemies receive the readable translucent canopy.
        private static bool BehindWorldCover(float x,float y,Decoration cover,
            float reachX,float reachY)
        {
            if(cover==null||!FiniteWorldCoordinate(x)||
               !FiniteWorldCoordinate(y)||
               !FiniteWorldCoordinate(cover.x)||
               !FiniteWorldCoordinate(cover.y))return false;
            return Math.Abs(x-cover.x)<reachX && y<cover.y &&
                y>cover.y-reachY;
        }
        // These are the accepted full-body Npcs.png world actors actually
        // drawn by DrawObject -> DrawNpc. Do not fade authored foliage for
        // non-character barrels, signs, gates or quest props nearby.
        private static bool IsCoverResident(string kind)
        {
            if(String.IsNullOrEmpty(kind))return false;
            switch(kind)
            {
                case "guild":case "garden":case "forge":
                case "cook":case "fish":case "scout":
                case "returnscout":case "mentor":case "envoy":
                case "captain":case "tinker":case "keeper":
                case "council":case "innGuest":
                    return true;
            }
            return kind.StartsWith("castaway",StringComparison.Ordinal);
        }
        private bool WorldCoverOverlapsActor(Decoration cover)
        {
            if(cover==null||!FiniteWorldCoordinate(cover.x)||
               !FiniteWorldCoordinate(cover.y))return false;
            if(BehindWorldCover(Game.save.x,Game.save.y,cover,1.4f,2.7f))
                return true;
            foreach(Enemy actor in Game.enemies)
                if(IsValidWorldEnemy(actor)&&
                   BehindWorldCover(actor.x,actor.y,cover,1.6f,2.9f))
                    return true;
            if(Game.save.companion>=0 && BehindWorldCover(
                Game.companionPosition.x,Game.companionPosition.y,
                cover,1.4f,2.7f))
                return true;
            if(coverResidentsPrepared)
            {
                // No allocation, no full-map scan per canopy; all candidates
                // were verified present and on-screen in DrawOrderedWorld.
                foreach(WorldObject resident in coverResidentsInView)
                    if(resident!=null && !String.IsNullOrEmpty(resident.kind) &&
                        BehindWorldCover(resident.x,resident.y,cover,1.4f,2.7f))
                        return true;
            }
            else
            {
                // Single-actor Editor/review draws are not inside the sorted
                // frame. Preserve their exact resident visibility semantics.
                foreach(WorldObject resident in Game.Map.objects)
                    if(resident!=null && IsCoverResident(resident.kind) &&
                        Game.IsObjectPresent(resident) &&
                        BehindWorldCover(resident.x,resident.y,cover,1.4f,2.7f))
                        return true;
            }
            if(Game.ReliefCivilianVisible && BehindWorldCover(
                Game.reliefFollowerPosition.x,Game.reliefFollowerPosition.y,
                cover,1.4f,2.7f))
                return true;
            foreach(FallenActor fallen in Game.fallen)
                if(fallen != null && fallen.IsValid &&
                    fallen.room == Game.save.room &&
                    BehindWorldCover(fallen.actor.x,fallen.actor.y,
                        cover,1.6f,2.9f))
                    return true;
            return false;
        }
        private void DrawWorldDecoration(Decoration prop)
        {
            if(prop==null||String.IsNullOrEmpty(prop.kind)||
               !FiniteWorldPoint(new Point(prop.x,prop.y)))return;
            Point p=ScreenPoint(new Point(prop.x,prop.y));
            float w=prop.kind=="house"?170:prop.kind=="tree"?100:prop.kind=="ruin"?150:prop.kind=="bed"||prop.kind=="bookshelf"?90:prop.kind=="table"||prop.kind=="counter"?80:76;
            int variant=prop.variant;
            if(prop.kind=="garden")variant=Game.save.plantedAt<0?0:Game.save.totalTime-Game.save.plantedAt>=60?2:1;
            float h=w*112/96;
            bool canopy=prop.kind=="tree"||prop.kind=="palm"||prop.kind=="snowpine",cover=canopy||prop.kind=="ruin";
            bool occludes=cover&&WorldCoverOverlapsActor(prop);
            Color tint=occludes?new Color(1,1,1,.4f):Color.white;
            Rect rect=new Rect(p.x-w/2,p.y-h+8,w,h);
            // Depth-sort and ground both large scenery and standing props
            // before painting their existing accepted pixels.
            if(canopy||prop.kind=="house"||prop.kind=="ruin")
                DrawActorContactShadow(p,canopy?76:prop.kind=="house"?122:108,
                    canopy?21:27,canopy?12:prop.kind=="house"?13:14,
                    occludes?.48f:.68f);
            if(canopy)
            {
                // DrawRootedPlant displays an intact square 128px atlas
                // Sprite with a painted soil/foot root at pixel 120. The
                // inherited tall-prop rect used its bottom+8 as a proxy
                // pivot: gust rotation kept that *wrong* point still and
                // made trunks skate against the real terrain/shadow anchor.
                // Register the accepted square's exact painted root with
                // the source world coordinate BEFORE applying wind.
                float side=Math.Min(rect.width,rect.height);
                rect.y=p.y-side*120f/128f;
                rect.height=side;
                DrawRootedPlant(prop,rect,tint);
                DrawAuthoredCanopyWhispers(prop,p,side,tint);
            }
            else
            {
                PropImage(prop.kind,variant,rect.x,rect.y,rect.width,rect.height,tint);
                if(prop.kind=="ruin")DrawAuthoredRuinSigils(prop,p,tint);
                if(prop.kind=="garden")
                    DrawAuthoredLivingGarden(prop,p,variant,tint);
            }
            if(prop.kind=="well"&&Game.Has("ally_3"))Box(p.x-5,p.y-16,10,5,mint);
        }
        private void DrawEnemyWithBars(Enemy enemy)
        {
            DrawEnemyActor(enemy);
            if(Game.practicing||!enemy.boss&&enemy.hp>=enemy.maxHp&&enemy.state!=1)return;
            Point p=ScreenPoint(enemy.Position);float width=enemy.boss?104:52,top=p.y-(enemy.boss?135:enemy.kind==0?54:79);
            Bar(p.x-width/2,top,width,enemy.hp/enemy.maxHp,PixelArt.C("#d58e79"),4);
            if(enemy.stagger>0)Bar(p.x-width/2,top+6,width,enemy.stagger/(enemy.boss?95:30),mint,2);
        }
    }
}
