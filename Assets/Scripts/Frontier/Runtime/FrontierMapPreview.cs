using System;
using System.Collections.Generic;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // A single menu-map texture replaces thousands of individual IMGUI
        // Box calls per repaint on the now 120x80 outdoor maps. Markers and
        // current position remain live overlays, not baked into the raster.
        private Texture2D terrainPreview;
        private GameCore previewOwner;
        private MapData previewMap;
        private int previewRegion=-1,previewDiscoveryStamp;
        private bool previewFog,previewLowTide;
        // Hash only the fog state belonging to this map, not Game.revision.
        // Combat, movement and HUD updates must not rebuild a 120x80 texture.
        private int PreviewDiscoveryStamp(int region,bool fog)
        {
            int code=Game.FarBridgeOpen(region)?41:17;
            // Locked gate surfaces are rasterized as stone; an unlocked seal
            // must invalidate the same cached map immediately.
            code=code*31+(Game.Has("seal_"+region)?1:0);
            if(!fog)return code;
            string prefix=region==8?
                "abyss_annex_seen_"+Game.save.abyssSeed+"_"+Game.save.abyssFloor+"_":
                "annex_seen_"+region+"_";
            unchecked
            {
                code=code*31+Game.save.explored[region];
                // Commutative: changes to flag-list ordering are not changes
                // to the actual explored terrain.
                int seen=0;
                foreach(string flag in Game.save.flags)
                    if(flag!=null&&flag.StartsWith(prefix,StringComparison.Ordinal))
                        seen^=flag.GetHashCode();
                return code*31+seen;
            }
        }

        // Separate from the active navigation/personal pin: this preview shows
        // where the *inspected* quest can be physically approached. Cache the
        // actual collision-aware search across IMGUI Layout/Repaint passes.
        private List<Point> inspectedQuestWalk = new List<Point>();
        private GameCore inspectedWalkOwner;
        private MapData inspectedWalkMap;
        private string[] inspectedWalkRowSnapshot;
        private Decoration[] inspectedWalkDecor;
        private string inspectedWalkQuestId;
        private int inspectedWalkRoom=-1,inspectedWalkFloor=-1;
        private int inspectedWalkStartX=int.MinValue,inspectedWalkStartY=int.MinValue;
        private Point inspectedWalkTarget;
        private bool inspectedWalkLowTide,inspectedWalkGate,inspectedWalkBridge,inspectedWalkSeal;
        private bool InspectedWalkRowsUnchanged(MapData map)
        {
            if(map==null||map.rows==null||inspectedWalkRowSnapshot==null||
                map.rows.Length!=inspectedWalkRowSnapshot.Length)return false;
            for(int i=0;i<map.rows.Length;i++)
                if(!object.ReferenceEquals(map.rows[i],inspectedWalkRowSnapshot[i]))
                    return false;
            return true;
        }
        private List<Point> InspectedQuestWalkingRoute(QuestGuide quest)
        {
            if(Game==null||quest==null||quest.locked||quest.complete||
                Game.mode!="map"||quest.room!=Game.save.room)
            {
                inspectedQuestWalk.Clear();
                inspectedWalkOwner=null;
                return inspectedQuestWalk;
            }
            MapData map=Game.Map;
            int room=Game.save.room;
            int startX=(int)Math.Floor(Game.Position.x);
            int startY=(int)Math.Floor(Game.Position.y);
            bool lowTide=Game.LowTide,gate=Game.GateBarrierActive;
            bool bridge=Game.Has("far_bridge_"+room),seal=Game.Has("seal_"+room);
            if(!object.ReferenceEquals(inspectedWalkOwner,Game)||
                !object.ReferenceEquals(inspectedWalkMap,map)||
                !object.ReferenceEquals(inspectedWalkDecor,map.decor)||
                inspectedWalkRoom!=room||inspectedWalkFloor!=Game.save.abyssFloor||
                inspectedWalkQuestId!=quest.id||
                inspectedWalkStartX!=startX||inspectedWalkStartY!=startY||
                (inspectedWalkTarget-quest.target).Length>.001f||
                inspectedWalkLowTide!=lowTide||inspectedWalkGate!=gate||
                inspectedWalkBridge!=bridge||inspectedWalkSeal!=seal||
                !InspectedWalkRowsUnchanged(map))
            {
                // requirePhysicalFinish forbids a best-effort line across a
                // closed gate; interaction targets may be approached adjacent
                // to an occupied/object tile without standing on the object.
                inspectedQuestWalk=Game.NavigationPathFrom(
                    Game.Position,quest.target,true);
                inspectedWalkOwner=Game;
                inspectedWalkMap=map;
                inspectedWalkDecor=map.decor;
                inspectedWalkRoom=room;inspectedWalkFloor=Game.save.abyssFloor;
                inspectedWalkQuestId=quest.id;
                inspectedWalkStartX=startX;inspectedWalkStartY=startY;
                inspectedWalkTarget=quest.target;
                inspectedWalkLowTide=lowTide;inspectedWalkGate=gate;
                inspectedWalkBridge=bridge;inspectedWalkSeal=seal;
                inspectedWalkRowSnapshot=map.rows==null?null:(string[])map.rows.Clone();
            }
            return inspectedQuestWalk;
        }

        private void ReleaseTerrainPreview()
        {
            if(terrainPreview!=null)UnityEngine.Object.Destroy(terrainPreview);
            terrainPreview=null;previewOwner=null;previewMap=null;
            previewRegion=-1;previewDiscoveryStamp=0;
        }
        // The strategic map and the live HUD minimap must display the same
        // passability changes as GameCore.Blocked and the world renderer.
        private char TerrainPreviewGlyph(MapData map,int region,int x,int y)
        {
            char tile=map.rows[y][x];
            if(tile=='?')return Game.FarBridgeOpen(region)?'=':'#';
            if(tile=='!')return Game.RidgeBreachOpen(region)?'=':
                (region==2||region==5?'~':'#');
            if(tile=='%'&&!Game.LowTide)return '~';
            if(tile=='v'||tile=='w'||tile=='b')
                return Game.Has("seal_"+region)?'=':'#';
            return tile;
        }
        private void DrawTerrainPreview(MapData map,int region,bool fog,float x,float y,float scale)
        {
            if(map==null||map.rows==null||map.rows.Length==0)return;
            int width=map.rows[0].Length,height=map.rows.Length;
            if(width<=0||height<=0||scale<=0)return;
            bool tide=Game.LowTide;
            int discoveryStamp=PreviewDiscoveryStamp(region,fog);
            if(terrainPreview==null||!object.ReferenceEquals(previewMap,map)||
                !object.ReferenceEquals(previewOwner,Game)||
                previewRegion!=region||previewFog!=fog||
                previewDiscoveryStamp!=discoveryStamp||
                previewLowTide!=tide)
            {
                ReleaseTerrainPreview();
                terrainPreview=new Texture2D(width,height,TextureFormat.RGBA32,false);
                terrainPreview.filterMode=FilterMode.Point;
                terrainPreview.wrapMode=TextureWrapMode.Clamp;
                terrainPreview.name="Starfall explored terrain preview";
                Color ground=PixelArt.C(map.color);
                Color water=PixelArt.C("#315c76"),stone=PixelArt.C("#566879");
                Color road=PixelArt.C("#a09877");
                // Fog is stored in 6x6-tile discovery sectors. Resolve
                // each sector once per rebuild rather than creating thousands
                // of annex flag strings and repeated list searches per pixel.
                int sectorsWide=(width+5)/6;
                bool[] sectorSeen=null;
                if(fog)
                {
                    int sectorsHigh=(height+5)/6;
                    sectorSeen=new bool[sectorsWide*sectorsHigh];
                    for(int sy=0;sy<sectorsHigh;sy++)
                        for(int sx=0;sx<sectorsWide;sx++)
                            sectorSeen[sy*sectorsWide+sx]=Game.Explored(region,sx*6,sy*6);
                }
                Color[] pixels=new Color[width*height];
                for(int yy=0;yy<height;yy++)
                {
                    string row=map.rows[yy];
                    for(int xx=0;xx<width;xx++)
                    {
                        char tile=TerrainPreviewGlyph(map,region,xx,yy);
                        Color color=tile=='~'?water:
                            tile=='#'?stone:tile=='='?road:ground;
                        if(fog&&!sectorSeen[(yy/6)*sectorsWide+xx/6])
                            color=Color.Lerp(color,ink,.68f);
                        // Unity raster origin is bottom-left; map north is GUI top.
                        pixels[(height-1-yy)*width+xx]=color;
                    }
                }
                terrainPreview.SetPixels(pixels);
                terrainPreview.Apply(false,false);
                previewOwner=Game;previewMap=map;previewRegion=region;
                previewFog=fog;previewDiscoveryStamp=discoveryStamp;
                previewLowTide=tide;
            }
            Image(terrainPreview,x,y,width*scale,height*scale);
        }
    }
}
