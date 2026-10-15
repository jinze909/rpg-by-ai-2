using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private enum WorldDrawKind { Decoration, Object, Enemy, Fallen, Player, Companion, Survivor }
        private struct WorldDrawEntry
        {
            public float depth;
            public int order;
            public WorldDrawKind kind;
            public Decoration decoration;
            public WorldObject obj;
            public Enemy enemy;
            public FallenActor fallen;
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
        private void AddWorldEntry(WorldDrawEntry entry)
        {entry.order=worldDrawEntries.Count;worldDrawEntries.Add(entry);}
        private bool WorldDrawable(float x, float y, float horizontalMargin = 230,
            float verticalMargin = 250)
        {
            Point screen = ScreenPoint(new Point(x, y));
            return screen.x > -horizontalMargin && screen.x < Width + horizontalMargin &&
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
                if(WorldDrawable(d.x,d.y))
                    AddWorldEntry(new WorldDrawEntry{depth=d.y,kind=WorldDrawKind.Decoration,decoration=d});
            foreach(WorldObject o in Game.Map.objects)
                if(WorldDrawable(o.x,o.y))
                {
                    AddWorldEntry(new WorldDrawEntry{depth=o.y+.01f,kind=WorldDrawKind.Object,obj=o});
                    if(IsCoverResident(o.kind) && Game.IsObjectPresent(o))
                        coverResidentsInView.Add(o);
                }
            foreach(Enemy e in Game.enemies)
            {
                // Combat simulation remains active everywhere; visuals still
                // draw well beyond the viewport to keep long-range warnings.
                if(WorldDrawable(e.x,e.y,450,450))
                {
                    DrawEnemyWarning(e);
                    AddWorldEntry(new WorldDrawEntry{depth=e.y,kind=WorldDrawKind.Enemy,enemy=e});
                }
            }
            foreach(FallenActor f in Game.fallen)
                if(WorldDrawable(f.actor.x,f.actor.y))
                    AddWorldEntry(new WorldDrawEntry{depth=f.actor.y-.02f,kind=WorldDrawKind.Fallen,fallen=f});
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
        }
        // Canopy/ruin occlusion is a presentation-only mask. Test every
        // actor that actually enters the depth-sorted world pass; a companion
        // or rescued civilian must never disappear behind opaque foliage
        // while the player/enemies receive the readable translucent canopy.
        private static bool BehindWorldCover(float x,float y,Decoration cover,
            float reachX,float reachY)
        {
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
            if(BehindWorldCover(Game.save.x,Game.save.y,cover,1.4f,2.7f))
                return true;
            foreach(Enemy actor in Game.enemies)
                if(BehindWorldCover(actor.x,actor.y,cover,1.6f,2.9f))
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
                    if(IsCoverResident(resident.kind) &&
                        BehindWorldCover(resident.x,resident.y,cover,1.4f,2.7f))
                        return true;
            }
            else
            {
                // Single-actor Editor/review draws are not inside the sorted
                // frame. Preserve their exact resident visibility semantics.
                foreach(WorldObject resident in Game.Map.objects)
                    if(IsCoverResident(resident.kind) &&
                        Game.IsObjectPresent(resident) &&
                        BehindWorldCover(resident.x,resident.y,cover,1.4f,2.7f))
                        return true;
            }
            if(Game.ReliefCivilianVisible && BehindWorldCover(
                Game.reliefFollowerPosition.x,Game.reliefFollowerPosition.y,
                cover,1.4f,2.7f))
                return true;
            foreach(FallenActor fallen in Game.fallen)
                if(fallen.actor!=null && BehindWorldCover(
                    fallen.actor.x,fallen.actor.y,cover,1.6f,2.9f))
                    return true;
            return false;
        }
        private void DrawWorldDecoration(Decoration prop)
        {
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
            if(canopy)DrawRootedPlant(prop,rect,tint);else PropImage(prop.kind,variant,rect.x,rect.y,rect.width,rect.height,tint);
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
