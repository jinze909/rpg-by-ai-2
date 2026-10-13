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
            Point screen=ScreenPoint(new Point(x,y));
            float edge=Game.save.reducedMotion||Game.save.effectsQuality==0 ?
                1f:WorldStreaming.EdgeOpacity(screen.x,screen.y,Width,Height);
            int tileX=Math.Max(0,Math.Min(Game.Map.rows[0].Length-1,(int)Math.Floor(x)));
            int tileY=Math.Max(0,Math.Min(Game.Map.rows.Length-1,(int)Math.Floor(y)));
            return edge*TerrainTileOpacity(tileX,tileY);
        }
        private void DrawOrderedWorld()
        {
            // Reuse storage and explicit references. Captured per-actor callbacks
            // used to allocate on every IMGUI pass; equal-depth ties also flickered.
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
                    AddWorldEntry(new WorldDrawEntry{depth=o.y+.01f,kind=WorldDrawKind.Object,obj=o});
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
                        case WorldDrawKind.Player:DrawHero();break;
                        case WorldDrawKind.Companion:DrawCompanionActor();break;
                        case WorldDrawKind.Survivor:DrawRescueSurvivorActor();break;
                    }
                }
            }
            finally{worldActorOpacity=1f;worldDrawEntries.Clear();}
        }
        private void DrawWorldDecoration(Decoration prop)
        {
            Point p=ScreenPoint(new Point(prop.x,prop.y));
            float w=prop.kind=="house"?170:prop.kind=="tree"?100:prop.kind=="ruin"?150:prop.kind=="bed"||prop.kind=="bookshelf"?90:prop.kind=="table"||prop.kind=="counter"?80:76;
            int variant=prop.variant;
            if(prop.kind=="garden")variant=Game.save.plantedAt<0?0:Game.save.totalTime-Game.save.plantedAt>=60?2:1;
            float h=w*112/96;
            bool canopy=prop.kind=="tree"||prop.kind=="palm"||prop.kind=="snowpine",cover=canopy||prop.kind=="ruin";
            bool occludes=cover&&Math.Abs(Game.save.x-prop.x)<1.4f&&Game.save.y<prop.y&&Game.save.y>prop.y-2.7f;
            if(cover)foreach(Enemy actor in Game.enemies)occludes|=Math.Abs(actor.x-prop.x)<1.6f&&actor.y<prop.y&&actor.y>prop.y-2.9f;
            Color tint=occludes?new Color(1,1,1,.4f):Color.white;
            Rect rect=new Rect(p.x-w/2,p.y-h+8,w,h);
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
