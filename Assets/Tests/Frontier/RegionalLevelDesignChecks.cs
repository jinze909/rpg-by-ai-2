using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Original map props and existing SaveData v2 are deliberately
        // untouched. Only pre-existing walkable floor ('.') becomes
        // continuous visible '=' road: tests use the LIVE hero collision
        // and real source identities to validate the updated atlas.
        public static void EightBiomesHaveGenuineContinuousSurveyAndCrossingRoadNetworks(Campaign data)
        {
            var signatures=new HashSet<string>();
            for(int room=0;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=3;
                WorldObject[] sites=new WorldObject[5];
                for(int stage=1;stage<=3;stage++)
                    sites[stage-1]=Array.Find(g.Map.objects,
                        o=>o.id=="trailcircuit_"+room+"_"+stage&&
                            o.kind=="trailcircuit");
                for(int district=0;district<2;district++)
                    sites[3+district]=Array.Find(g.Map.objects,
                        o=>o.id=="ridge_route_"+room+"_"+district&&
                            o.kind=="ridgemarker");
                foreach(WorldObject site in sites)
                    Check(site!=null&&
                        g.CanStand(new Point(site.x,site.y)),
                        "Original exact survey/crossing sources remain physically accessible: "+room);
                int[,] legs={{0,1},{1,2},{2,3},{1,4}};
                foreach(int leg in new[]{0,1,2,3})
                {
                    WorldObject first=sites[legs[leg,0]];
                    WorldObject last=sites[legs[leg,1]];
                    int sx=(int)Math.Floor(first.x),
                        sy=(int)Math.Floor(first.y);
                    int tx=(int)Math.Floor(last.x),
                        ty=(int)Math.Floor(last.y);
                    int w=g.Map.rows[0].Length,h=g.Map.rows.Length;
                    var seen=new bool[w*h];
                    var q=new int[w*h];
                    int front=0,back=0,source=sy*w+sx;
                    seen[source]=true;q[back++]=source;
                    bool reached=false;
                    while(front<back)
                    {
                        int n=q[front++],x=n%w,y=n/w;
                        if(x==tx&&y==ty){reached=true;break;}
                        int[,] directions={{1,0},{-1,0},{0,1},{0,-1}};
                        for(int side=0;side<4;side++)
                        {
                            int xx=x+directions[side,0];
                            int yy=y+directions[side,1];
                            if(xx<2||yy<2||xx>=w-2||yy>=h-2)
                                continue;
                            int next=yy*w+xx;
                            if(seen[next]||g.Map.rows[yy][xx]!='='||
                                !g.CanStand(new Point(xx+.5f,yy+.5f)))
                                continue;
                            seen[next]=true;q[back++]=next;
                        }
                    }
                    Check(reached,
                        "The real visible paved road network reaches the authentic next stop without water, wall or collider: "+room+"/"+leg);
                    g.save.x=first.x;g.save.y=first.y;
                    Check(g.NavigationPathFrom(g.Position,
                        new Point(last.x,last.y),true).Count>0,
                        "Original navigation engine also walks the route: "+room+"/"+leg);
                }
                // Distinct local contours: don't merely recolor seven
                // copies of the same urban/causeway/rail/bell road drawing.
                string signature="";
                for(int y=31;y<=66;y++)
                    signature+=g.Map.rows[y].Substring(40,70);
                Check(signatures.Add(signature),
                    "Different biomes preserve non-identical authored paved topology");
                Check(g.save.version==2,
                    "World terrain authoring does not alter the saved schema");
            }
        }

        public static void SurveyedPavedGroundGivesRealTravelRecoveryButNeverOffRoad(Campaign data)
        {
            for(int room=0;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=3;g.enemies.Clear();
                WorldObject marker=Array.Find(g.Map.objects,
                    o=>o.id=="trailcircuit_"+room+"_1");
                Check(marker!=null&&g.CanStand(new Point(marker.x,marker.y)),
                    "An original authentic trail marker anchors this reward");
                g.save.x=marker.x;g.save.y=marker.y;
                Check(g.TrailRoadRecoveryBonus==0f,
                    "Uncompleted survey cannot grant route endurance");
                for(int i=1;i<=3;i++)
                    g.save.flags.Add("trail_circuit_"+room+"_"+i);
                Check(g.TrailRoadRecoveryBonus==2f,
                    "Completed survey restores +2 on real paved ground");
                float px=g.save.x,py=g.save.y;
                bool found=false;
                for(int dy=-6;dy<=6&&!found;dy++)
                for(int dx=-6;dx<=6&&!found;dx++)
                {
                    Point field=new Point(px+dx,py+dy);
                    int x=(int)Math.Floor(field.x),y=(int)Math.Floor(field.y);
                    if(x<2||y<2||x>=118||y>=78||
                       g.Map.rows[y][x]!='.'||!g.CanStand(field))continue;
                    g.save.x=field.x;g.save.y=field.y;found=true;
                }
                Check(found&&g.TrailRoadRecoveryBonus==0f,
                    "Off-road exploration receives no invented paved endurance: "+room);
                g.save.x=px;g.save.y=py;
                Check(g.TrailRoadRecoveryBonus==2f,
                    "The same real marker resumes its intended travel recovery");
                // A v2 save may legitimately stay in the farshore only
                // after earning its actual original chapter-entry reports.
                // Test a legal veteran slot, not an illicit room transfer:
                // Load() correctly sends unauthorized room 5-7 saves home.
                if(room>=5)g.save.flags.Add("exp_intro");
                if(room>=6)g.save.flags.Add("harbor_report");
                if(room>=7)g.save.flags.Add("desert_report");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.mode=="title"&&
                    restored.TrailRoadRecoveryBonus==0f,
                    "Loading a legacy save on the title screen must not grant world energy recovery");
                restored.mode="world";
                Check(restored.save.version==2&&
                    restored.save.room==room&&
                    restored.TrailCircuitComplete(room)&&
                    restored.TrailRoadRecoveryBonus==2f,
                    "Legally unlocked old v2 slot stays in its region and derives paved recovery from real flags and terrain");
            }
        }

        // The tenth gameplay location is not Campaign.maps[8]: live floor
        // layouts are regenerated from the exact persistent v2 seed. Exercise
        // the real generator, not a decorative static preview.
        // The seven original 120x80 landscapes had opposite banks
        // three tiles apart but 37-45 real walking steps apart. These
        // biome-specific cuts change real terrain, not the UI or save flags.
        // The second district passage is a true, local alternate route
        // in each biome. It must work with the live actor collision graph,
        // remain reachable from the original west gate, and never overlap
        // an existing quest object or require new save fields.
        // The real map action must select an original paved passage,
        // prove the complete hero walking route, preserve unrelated quest
        // tracking, and never serialize a temporary map pin.
        public static void FourteenRegionalShortcutMapPinsFollowRealRoads(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);
                g.save.room=room;g.save.stage=3;
                g.mode="map";g.save.x=5.5f;g.save.y=14.5f;
                string tracked=g.save.trackedQuest;
                for(int pass=0;pass<2;pass++)
                {
                    Point? road=g.RegionalShortcutFoot(room,pass);
                    Check(road.HasValue&&g.CanStand(road.Value),
                        "Atlas exposes only an original physically open road: "+room+"/"+pass);
                    var walk=g.NavigationPathFrom(g.Position,road.Value,true);
                    Check(walk.Count>0,
                        "The actual world movement graph reaches its atlas pass: "+room+"/"+pass);
                    Check(g.FollowRegionalShortcut(pass)&&
                        g.HasLocalWaymark&&
                        (g.LocalWaymark.Value-road.Value).Length<.001f,
                        "A deliberate map shortcut button pins its exact walkable road: "+room+"/"+pass);
                    Check(g.save.trackedQuest==tracked&&g.save.version==2,
                        "Optional atlas route never silently tracks a quest or changes SaveData");
                }
                Point previous=g.LocalWaymark.Value;
                // A dynamic map edit (or future authored demolition) can
                // invalidate the bridge while the old personal pin exists.
                // No reward, scene transition, or stale shortcut may be
                // issued just because the atlas cached its old coordinates.
                string roadBefore=g.Map.rows[46];
                try
                {
                    int x=room==1||room==2?60:
                        room==3||room==7?61:room==4?64:
                        room==5?63:59;
                    char[] blocked=roadBefore.ToCharArray();
                    blocked[x]='#';
                    g.Map.rows[46]=new string(blocked);
                    Check(!g.RegionalShortcutFoot(room,0).HasValue&&
                        !g.FollowRegionalShortcut(0)&&
                        (g.LocalWaymark.Value-previous).Length<.001f,
                        "Closed or damaged passage never steals a previous valid route");
                }
                finally {g.Map.rows[46]=roadBefore;}
                Check(!g.FollowRegionalShortcut(-1)&&
                    !g.FollowRegionalShortcut(2)&&
                    !g.RegionalShortcutFoot(room,2).HasValue&&
                    (previous-g.LocalWaymark.Value).Length<.001f,
                    "Invalid route requests must not delete the player's old pin");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(!restored.HasLocalWaymark&&restored.save.version==2,
                    "Old save slots load without transient shortcut navigation state");
            }
        }

        public static void SevenFarDistrictAlternativePassagesStayWalkable(Campaign data)
        {
            int[,] sites={{82,52,1},{87,62,0},{76,54,0},
                {82,45,1},{75,54,0},{25,54,0},{88,62,0}};
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                int x=sites[room-1,0],y=sites[room-1,1];
                bool sideways=sites[room-1,2]==1;
                Point entry=sideways?new Point(x-.5f,y+.5f):
                    new Point(x+.5f,y-.5f);
                Point exit=sideways?new Point(x+2.5f,y+.5f):
                    new Point(x+.5f,y+2.5f);
                Check(g.CanStand(entry)&&g.CanStand(exit),
                    "Original biome path has walkable sides: "+room);
                // Both entrances must be recognizable as the authored
                // road material used elsewhere in this biome. Do not
                // leave a narrow unmarked gap in the water or stone face.
                for(int lane=0;lane<2;lane++)
                {
                    int edge=lane==0?-1:2;
                    for(int shoulder=0;shoulder<2;shoulder++)
                    {
                        int tx=sideways?x+edge:x+shoulder;
                        int ty=sideways?y+shoulder:y+edge;
                        Check(g.Map.rows[ty][tx]=='=',
                            "Outer route mouth joins the existing foot trail: "+room);
                    }
                }
                for(int yy=y;yy<=y+1;yy++)
                for(int xx=x;xx<=x+1;xx++)
                {
                    Point foot=new Point(xx+.5f,yy+.5f);
                    Check(g.Map.rows[yy][xx]=='='&&g.CanStand(foot),
                        "Second passage exists in actual level collision: "+room);
                    foreach(WorldObject prop in g.Map.objects)
                        Check((foot-new Point(prop.x,prop.y)).Length>=3f,
                            "Second passage leaves real quest objects clear: "+
                            room+"/"+prop.id);
                }
                var crossing=g.NavigationPathFrom(entry,exit,true);
                Check(crossing.Count>0&&crossing.Count<=5,
                    "A genuine short route now crosses the distant district: "+room);
                Check(g.NavigationPathFrom(new Point(5.5f,14.5f),
                    entry,true).Count>0,
                    "An ordinary hero can walk to the alternate entry: "+room);
                Check(g.save.version==2,
                    "Terrain pathways never change quest or save format");
            }
        }

        // Actual original trail sources now direct the player to the
        // NEXT stage, instead of replaying a generic hunt instruction.
        // Dialogue itself cannot steal the hero's map marker; an explicit
        // atlas action must create the real walkable personal route.
        public static void EightOrderedTrailRoutesProvideRealNextStopNavigation(Campaign data)
        {
            for(int room=0;room<=7;room++)
            {
                var g=Fresh(data);
                g.save.room=room;g.save.stage=3;g.mode="world";
                WorldObject first=System.Array.Find(g.Map.objects,
                    o=>o.id=="trailcircuit_"+room+"_1");
                Check(first!=null&&g.CanStand(new Point(first.x,first.y)),
                    "Original first trail landmark is real legal terrain: "+room);
                g.save.x=first.x;g.save.y=first.y;
                int money=g.save.coins;
                g.Talk(first);
                Check(g.TrailCircuitProgress(room)==1&&
                    g.mode=="dialog"&&g.conversation!=null&&
                    !g.HasLocalWaymark,
                    "Reading a real field source advances one and ONLY one bearing without pinning");
                WorldObject second=g.NextTrailCircuitMarker(room);
                Check(second!=null&&second.id=="trailcircuit_"+room+"_2",
                    "Next bearing resolves to original authored second source: "+room);
                string coords=second.x.ToString("0.0",
                    System.Globalization.CultureInfo.InvariantCulture)+","+
                    second.y.ToString("0.0",
                    System.Globalization.CultureInfo.InvariantCulture);
                Check(g.conversation.text.Contains(coords)&&
                    g.conversation.textEn.Contains(coords),
                    "Actual next coordinates appear in both story languages: "+room);
                Check(g.save.coins==money&&g.save.version==2,
                    "New guidance cannot mint a reward or change the save contract");
                g.mode="map";
                Check(g.FollowNextTrailCircuitMarker()&&g.HasLocalWaymark,
                    "The explicit map route uses a verified real walking approach: "+room);
                Point? pin=g.LocalWaymark;
                Check(pin.HasValue&&g.CanStand(pin.Value)&&
                    (pin.Value-new Point(second.x,second.y)).Length<2f,
                    "Trail pin stops at true physical E-interaction reach: "+room);
                g.save.flags.Add("trail_circuit_"+room+"_2");
                WorldObject third=g.NextTrailCircuitMarker(room);
                Check(third!=null&&third.id=="trailcircuit_"+room+"_3",
                    "Legacy progress flags select the existing final stop: "+room);
                // Keep real route ownership on refused requests.
                g.mode="world";
                Check(!g.FollowNextTrailCircuitMarker()&&
                    (g.LocalWaymark.Value-pin.Value).Length<.001f,
                    "World background or dialogue cannot steal a map-only pin");
                g.save.flags.Add("trail_circuit_"+room+"_3");
                g.mode="map";
                Check(g.TrailCircuitComplete(room)&&
                    g.NextTrailCircuitMarker(room)==null&&
                    !g.FollowNextTrailCircuitMarker()&&
                    (g.LocalWaymark.Value-pin.Value).Length<.001f,
                    "Completed trails never revive old directions or discard prior routes");
            }
        }

        public static void SevenRegionalTerrainShortcutsCreatePhysicalWalkingLoops(Campaign data)
        {
            int[] centers={0,60,60,61,64,63,59,61};
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                int x=centers[room];
                Point north=new Point(x+.5f,45.5f);
                Point south=new Point(x+.5f,48.5f);
                Check(g.CanStand(north)&&g.CanStand(south),
                    "Both sides of the authored passage need real hero footing: "+room);
                for(int y=46;y<=47;y++)
                for(int xx=x-1;xx<=x+1;xx++)
                {
                    Point foot=new Point(xx+.5f,y+.5f);
                    Check(g.Map.rows[y][xx]=='='&&g.CanStand(foot),
                        "Cut is walkable terrain, not a fake minimap shortcut: "+room);
                    foreach(WorldObject obj in g.Map.objects)
                        Check((foot-new Point(obj.x,obj.y)).Length>=2.5f,
                            "New passage must not cover a quest E source: "+room+"/"+obj.id);
                }
                // The new passage must visually join BOTH walkable
                // approach roads. The existing '=' path tile is rendered by
                // the shipped biome terrain palette; blank isolated cuts
                // would be navigable yet invisible to the player.
                for(int dx=-1;dx<=1;dx++)
                    Check(g.Map.rows[45][x+dx]=='='&&
                        g.Map.rows[48][x+dx]=='=',
                        "Regional shortcut has legible paved mouths on both banks: "+room);
                var path=g.NavigationPathFrom(north,south,true);
                Check(path.Count>0&&path.Count<=5,
                    "Actual collision navigation must cross the new 3-step passage: "+room);
                Check(g.save.version==2,
                    "New terrain changes no quest flag or save schema");
            }
        }

        public static void ProceduralAbyssQuestFramesKeepSixFloorsPlayable(Campaign data)
        {
            var generate=typeof(GameCore).GetMethod("GenerateAbyss",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(generate!=null,"Live procedural level generator remains wired");
            foreach(int seed in new[]{137,417})
            for(int floor=1;floor<=6;floor++)
            {
                var g=new GameCore(data);
                g.save.room=8;g.save.abyssFloor=floor;
                g.save.abyssSeed=seed;
                int coins=g.save.coins,flags=g.save.flags.Count;
                generate.Invoke(g,null);
                MapData map=g.Map;
                Check(map.id==8&&map.rows.Length==44&&
                    map.rows[0].Length==66&&map.decor.Length>=33,
                    "Every genuine floor contains its original connected dungeon and framed branches");
                WorldObject exit=Array.Find(map.objects,o=>o.id=="abyssExit");
                WorldObject gate=Array.Find(map.objects,o=>o.id=="abyssGate");
                Check(exit!=null&&gate!=null&&exit.x==3&&exit.y==14&&
                    gate.x==31&&gate.y==14,
                    "New wayfinding never moves the original unbanked-exit or reward gate");
                foreach(Decoration prop in map.decor)
                {
                    Check(prop!=null&&prop.x>=0&&prop.y>=0&&
                        prop.x<66&&prop.y<44,
                        "Floor-specific scenery is bounded by true terrain");
                    foreach(WorldObject source in map.objects)
                        Check((new Point(prop.x-source.x,
                            prop.y-source.y)).Length>=2.5f,
                            "Cavern visuals do not cover actual quest interaction: "+source.id);
                }
                Check(g.save.coins==coins&&g.save.flags.Count==flags&&
                    g.save.version==2,
                    "Reframing existing root art neither banks loot nor rewrites saves");
            }
        }

        // End-to-end map composition regression. Relocations preserve the
        // original interacting instance and all source IDs. Large world-map
        // silhouettes are visuals; only real terrain determines reachability.
        public static void TenWorldLocationsRetainAccessibleSeparatedQuestSources(Campaign data)
        {
            Check(data.maps.Length==10,
                "All eight campaign zones, the Abyss seed and the Inn are present");
            string[][] relocated={
                new[]{"chef","stormnode2"},
                new string[0],
                new[]{"witnesssite22"},
                new[]{"witnesssite23","stormrelay3","annex_camp_3"},
                new string[0],
                new[]{"seal5","mistlight_mirror"},
                new[]{"glassherb"},
                new[]{"skyprism1","frostwood","annex_vista_7"},
                new string[0],new string[0]
            };
            for(int room=0;room<10;room++)
            {
                MapData map=data.maps[room];
                Check(map!=null&&map.rows!=null&&map.rows.Length>0&&
                    map.objects!=null&&map.decor!=null,
                    "Each shipped map retains its authored terrain and sources");
                // Preserve every original source count and the three
                // deliberate, externally playable new field caches in each
                // outdoor biome. A random padding object cannot substitute
                // for a named, authentic renewable resource interaction.
                // Legacy maps and the safe town/Inn remain unchanged.
                int[] originalSourceCounts={68,66,69,80,76,73,69,66,2,15};
                int expectedAdditions=room>=1&&room<=7?3:0;
                Check(map.objects.Length==
                    originalSourceCounts[room]+expectedAdditions,
                    "Original authored source roster and exactly three regional discoveries must survive");
                if(expectedAdditions>0)
                {
                    for(int slot=0;slot<3;slot++)
                    {
                        string id="fartrail_supply_"+room+"_"+slot;
                        WorldObject resource=Array.Find(map.objects,o=>o.id==id);
                        Check(resource!=null&&
                            (resource.kind=="herb"||resource.kind=="wood"||
                             resource.kind=="ore")&&
                            !String.IsNullOrEmpty(resource.name)&&
                            !String.IsNullOrEmpty(resource.en),
                            "Only three distinct authored renewable field caches may extend biome "+room);
                    }
                }
                foreach(WorldObject o in map.objects)
                    Check(o!=null&&!String.IsNullOrEmpty(o.id),
                        "Every original source keeps a persistent identity");
                foreach(Decoration d in map.decor)
                    Check(d!=null&&d.x>=0&&d.y>=0&&
                        d.y<map.rows.Length&&d.x<map.rows[0].Length,
                        "Every visual footprint remains inside its own map");
                if(room==8)continue; // The live Abyss has a generated map.
                GameCore g=new GameCore(data);
                g.save.room=room;g.mode="world";
                foreach(string id in relocated[room])
                {
                    WorldObject o=Array.Find(map.objects,p=>p.id==id);
                    Check(o!=null&&g.CanStand(new Point(o.x,o.y)),
                        "Moved quest source must retain real legal footing: "+id);
                    foreach(WorldObject other in map.objects)
                        if(other!=o)
                            Check((new Point(o.x-other.x,o.y-other.y)).Length>=2.15f,
                                "No overlapping E target for "+id+"/"+other.id);
                }
            }
        }



        // Reposition seven existing source objects to give each its own
        // physical E reach circle in busy story locations. Keep stable
        // IDs, authored battle objects, source counts and v2 quest flags.
        public static void ThreeDistrictsDecrowdActualQuestSources(Campaign data)
        {
            string[][] moved={
                new string[0],
                new string[0],new string[0],new string[0],
                new[]{"triadmemory3","silentledger_record"},
                new[]{"tidecargo1","tidecargo3","mistlight_lamp"},
                new string[0],new[]{"seal7"},
                new string[0],new string[0]
            };
            for(int room=0;room<data.maps.Length;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.room=room;
                foreach(string id in moved[room])
                {
                    WorldObject focus=Array.Find(g.Map.objects,o=>o.id==id);
                    Check(focus!=null&&g.CanStand(new Point(focus.x,focus.y)),
                        "Moved original source must have legal world footing: "+id);
                    foreach(WorldObject other in g.Map.objects)
                    {
                        if(System.Object.ReferenceEquals(other,focus))continue;
                        float distance=(new Point(other.x-focus.x,
                            other.y-focus.y)).Length;
                        Check(distance>=2.55f,
                            "Existing world interaction sources are separated: "+id+"/"+other.id);
                    }
                    g.save.x=focus.x;g.save.y=focus.y;
                    if(g.IsObjectPresent(focus))
                        Check(g.NearbyInteractionChoices().Contains(focus),
                            "Actual relocated source remains selectable in real E list: "+id);
                }
                Check(g.save.version==2,
                    "Decluttering the map does not change original hero persistence");
            }
        }


        // Moving a live quest source without its guidance arrow creates a
        // false objective and can fail a timed rescue. Both first stages
        // must use the map's REAL authored object, not historical constants.
        public static void RelocatedTimedQuestTargetsMatchRealPhysicalObjects(Campaign data)
        {
            for(int caseId=0;caseId<2;caseId++)
            {
                int room=caseId==0?4:5;
                string noticeId=caseId==0?"silentledger_notice":"mistlight_notice";
                string targetId=caseId==0?"silentledger_record":"mistlight_lamp";
                string questId=caseId==0?"silent_ledger":"mistport_lastlight";
                string begin=caseId==0?"silentledger_start":"mistlight_start";
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=3;g.enemies.Clear();
                g.save.flags.Add("boss_3");
                WorldObject notice=Array.Find(g.Map.objects,o=>o.id==noticeId);
                WorldObject target=Array.Find(g.Map.objects,o=>o.id==targetId);
                Check(notice!=null&&target!=null&&
                    g.CanStand(new Point(target.x,target.y)),
                    "Timed-story target retains its authentic map object and legal footing");
                g.save.x=notice.x;g.save.y=notice.y;
                g.Talk(notice);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "Starting each actual timed story first opens its real notice");
                int option=Array.FindIndex(g.conversation.choices,o=>o.action==begin);
                Check(option>=0,"The physical story notice still offers its original mission");
                g.Choose(option);
                QuestGuide guide=g.GetQuest(questId);
                Point actual=new Point(target.x,target.y);
                Check((guide.target-actual).Length<.001f&&
                    (caseId==0?(g.SilentLedgerTarget-actual).Length:
                        (g.MistlightTarget-actual).Length)<.001f,
                    "Active journal and world guidance both point to the moved real object");
                string coords=target.x.ToString("0.0",
                    System.Globalization.CultureInfo.InvariantCulture)+","+
                    target.y.ToString("0.0",
                    System.Globalization.CultureInfo.InvariantCulture);
                Check(guide.description.Contains(coords),
                    "The player-facing mission description names the REAL current coordinates");
                Check(g.NavigationPathFrom(new Point(notice.x,notice.y),
                    actual,true).Count>0,
                    "The timed route remains physically traversable from the authentic notice");
                Check(g.save.version==2,
                    "Correcting mission navigation does not change the old save schema");
            }
        }

        public static void LiveFieldContractsDoNotFreezeWhileOrdinaryDistrictsSleep(Campaign data)
        {
            var g=Fresh(data);
            g.mode="world";g.save.room=1;g.enemies.Clear();
            g.save.flags.Add("biome_field_done_1");
            WorldObject post=Array.Find(g.Map.objects,
                o=>o!=null&&o.id=="biomeSurvey1");
            Check(post!=null&&PlaceWithinTalkReach(g,post),
                "Original forest survey station accepts a genuine local contract");
            g.Talk(post);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_patrol_begin");
            Check(begin>=0,"A genuine signed survey offers a live patrol");
            g.Choose(begin);
            Check(g.FieldPatrolActive&&g.FieldPatrolRemaining==2&&
                g.enemies.Count==2,
                "Paid contract stages precisely two living opponents");
            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var dormant=typeof(GameCore).GetMethod(
                "RegionalAmbientEncounterDormant",flags);
            Check(dormant!=null,"Production distance sleep selector exists");
            // Walk to an authentic valid distant tile rather than moving
            // actors through gates. No rewards or enemy identities are faked.
            Point far=new Point();bool found=false;
            for(int y=2;y<g.Map.rows.Length-2&&!found;y++)
            for(int x=2;x<g.Map.rows[y].Length-2&&!found;x++)
            {
                Point p=new Point(x+.5f,y+.5f);
                if(!g.CanStand(p))continue;
                bool remote=true;
                foreach(Enemy enemy in g.enemies)
                    if((enemy.Position-p).Length<15f){remote=false;break;}
                if(remote){far=p;found=true;}
            }
            Check(found,"Existing Thornwood contains a physically valid second combat district");
            g.save.x=far.x;g.save.y=far.y;
            Enemy contracted=g.enemies[0];
            contracted.state=0;contracted.stun=0f;
            contracted.hitTime=contracted.releaseTime=0f;
            contracted.timer=2f;
            Check(!(bool)dormant.Invoke(g,new object[]{contracted}),
                "Exact live paid-contract actor never enters idle background sleep");
            float before=contracted.timer;
            g.Tick(.05f,new Point());
            Check(g.FieldPatrolActive&&g.enemies.Contains(contracted)&&
                contracted.timer<before&&g.FieldPatrolRemaining==2,
                "Contracted enemies keep ticking genuine world cooldowns outside passive ambient range");
            Check(!g.FieldPatrolDone(1)&&g.save.version==2,
                "Physical distance alone cannot claim a reward or change the save schema");
            // Historical baseline foes are still permitted to sleep away
            // from the hero, retaining regional streaming performance.
            var ordinary=Fresh(data);
            ordinary.mode="world";ordinary.save.room=1;
            ordinary.save.x=far.x;ordinary.save.y=far.y;
            ordinary.enemies.Clear();
            var spawn=typeof(GameCore).GetMethod(
                "SpawnRegionalBaselineEnemies",flags);
            Check(spawn!=null,"Original regional enemy spawner remains active");
            // Baseline spawning is centered on real player ground; choose
            // an existing opposite pocket as the sleep reference afterward.
            ordinary.save.x=post.x;ordinary.save.y=post.y;
            spawn.Invoke(ordinary,new object[]{6,100f});
            ordinary.save.x=far.x;ordinary.save.y=far.y;
            bool sleeps=false;
            foreach(Enemy enemy in ordinary.enemies)
                if((enemy.Position-far).Length>13f&&
                    (bool)dormant.Invoke(ordinary,new object[]{enemy}))
                    sleeps=true;
            Check(sleeps,
                "Unalerted authored baseline districts still sleep at long distance");
        }


        public static void RegionalOverworldGroupsAttackInReadableMixedLanes(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var rows=new string[32];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] line=new string('.',40).ToCharArray();
                    for(int x=0;x<40;x++)
                        if(x<2||x>=38||y<2||y>=30)line[x]='#';
                    rows[y]=new string(line);
                }
                data.maps[1]=new MapData{
                    id=1,spawnX=20.5f,spawnY=15.5f,
                    rows=rows,objects=new WorldObject[0],
                    decor=new Decoration[0]};
                var g=Fresh(data);
                g.mode="world";g.save.room=1;
                g.save.x=20.5f;g.save.y=15.5f;
                g.enemies.Clear();
                var flags=System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance;
                var spawn=typeof(GameCore).GetMethod(
                    "SpawnRegionalBaselineEnemies",flags);
                var available=typeof(GameCore).GetMethod(
                    "RegionalVolleySlotAvailable",flags);
                Check(spawn!=null&&available!=null,
                    "Actual production spawning and live volley selector exist");
                spawn.Invoke(g,new object[]{6,150f});
                Check(g.enemies.Count==6,
                    "The real forest overworld composition still contains six raiders");
                // Arrange the SAME six authentic enemies around the hero
                // on legal, completely open combat ground. No game mode,
                // damage profile, enemy kind or weapon contract is mocked.
                Point[] feet={
                    new Point(19.1f,15.5f),new Point(21.9f,15.5f),
                    new Point(20.5f,11.6f),new Point(20.5f,17f),
                    new Point(17.2f,12.2f),new Point(18.9f,17.3f)
                };
                for(int i=0;i<6;i++)
                {
                    Enemy raider=g.enemies[i];
                    raider.x=feet[i].x;raider.y=feet[i].y;
                    raider.timer=0f;raider.stun=0f;
                    raider.hitTime=raider.releaseTime=0f;
                    Check(g.CanStand(raider.Position),
                        "Authored overworld raider has genuine legal combat footing");
                }
                Check((bool)available.Invoke(g,
                    new object[]{g.enemies[0]}),
                    "The first physically present attacker may commit freely");
                g.Tick(.04f,new Point());
                int close=0,ranged=0;
                foreach(Enemy e in g.enemies)
                {
                    if(e.state!=1)continue;
                    if(e.kind==1)ranged++;else close++;
                }
                Check(close==1&&ranged==1,
                    "Six active overworld raiders yield one true melee warning and one true bow warning, not simultaneous same-role bursts");
                Check(g.enemies[1].state==0&&
                    Math.Abs(g.enemies[1].y-feet[1].y)>.005f&&
                    (g.enemies[1].Position-g.Position).Length>1.05f,
                    "A waiting second melee fighter genuinely circles the hero on walkable ground instead of freezing in a stacked attack queue");
                Check(g.save.version==2&&g.enemies.Count==6&&
                    !g.save.flags.Contains("biome_patrol_done_1"),
                    "Squad cadence does not despawn foes, claim patrol rewards or migrate old saves");
                // Two ordinary enemies may reuse an integer ID on a
                // restored save; committed actor identity still matters.
                Enemy ready=g.enemies.Find(e=>e.kind!=1&&e.state==1);
                Enemy queued=g.enemies.Find(e=>e.kind!=1&&e.state==0);
                Check(ready!=null&&queued!=null,
                    "The live squad contains one warned and one waiting melee raider");
                queued.id=ready.id;
                Check(!(bool)available.Invoke(g,new object[]{queued}),
                    "A duplicate numeric spawn ID cannot bypass an occupied physical melee warning");
                ready.state=0;ready.stun=3f;
                Check((bool)available.Invoke(g,new object[]{queued}),
                    "Interrupting the actual committed melee releases its lane to a waiting raider");
                // Unscripted independent monsters and Bosses keep the
                // original autonomous combat timing.
                Enemy outsider=new Enemy(999111,0,19.2f,14.8f,100f);
                Enemy guardian=new Enemy(999112,2,19.2f,14.8f,600f,true);
                Check((bool)available.Invoke(g,new object[]{outsider})&&
                    (bool)available.Invoke(g,new object[]{guardian}),
                    "Generic independent encounters and Boss telegraphs are not throttled");
            }
            finally{data.maps[1]=original;}
        }

        // Runs against the real Campaign.json and production collision,
        // traversal, actor placement and original chapter sources.
        public static void RegionalCombatDevicesChangeRealEncountersAndPreserveSaves(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(clear!=null,"Original world collision is used for battlefield counterplay");
            for(int room=1;room<=4;room++)
            {
                g.Enter(room);g.enemies.Clear();
                WorldObject site=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(site!=null&&PlaceWithinTalkReach(g,site),
                    "Each real chapter has a reachable, unique environmental device");
                Point origin=new Point(site.x,site.y),foot=new Point();
                bool found=false;
                for(int dy=-5;dy<=5&&!found;dy++)
                for(int dx=-5;dx<=5&&!found;dx++)
                {
                    Point candidate=new Point(origin.x+dx,origin.y+dy);
                    float dist=(candidate-origin).Length;
                    if(dist<2.5f||dist>5.7f||!g.CanStand(candidate)||
                        !(bool)clear.Invoke(g,new object[]{origin,candidate}))continue;
                    foot=candidate;found=true;
                }
                Check(found,"The placed device has real visible, legal attacker feet in the original level");
                g.save.energy=90f;
                g.Talk(site);
                int action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                Check(action>=0,"The genuine map object offers a playable interaction");
                g.Choose(action);
                Check(g.save.energy==90f&&g.FieldTacticCooldown(site)==0f&&
                    !g.Has("field_tactic_used_"+room),
                    "No enemy means no phantom cost or mastery reward");
                Enemy enemy=new Enemy(93000+room,room==3?2:room==4?1:0,
                    foot.x,foot.y,100f);
                g.enemies.Add(enemy);g.Talk(site);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                g.Choose(action);
                Check(g.save.energy==76f&&g.FieldTacticCooldown(site)>23f&&
                    g.Has("field_tactic_used_"+room)&&enemy.hp==100f,
                    "Actual line-of-sight contact spends 14 energy and persists only genuine first use");
                if(room==1)Check(enemy.stun>=2f&&enemy.stagger>=1f,
                    "Root bell provides a real long snare");
                if(room==2)Check(enemy.stun>=.9f&&g.CanStand(enemy.Position),
                    "Tidal push cannot throw enemies into blocked world");
                if(room==3)Check(enemy.armorOpenTime>=3f,
                    "Mining vent opens actual armor, not a UI label");
                if(room==4)Check(enemy.stun>=2f,
                    "Citadel breaker interrupts the living archer");
                g.Talk(site);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                g.Choose(action);
                Check(g.save.energy==76f,
                    "Cooldown rejects duplicate charge without extra costs");
            }
            Check(g.FieldTacticMasteryCount==4&&
                g.Has("field_tactics_mastered"),
                "Four distinct physical combats earn one total four-region milestone");
            int talent=g.save.skillPoints;
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.FieldTacticMasteryCount==4&&
                restored.Has("field_tactics_mastered")&&
                restored.save.skillPoints==talent,
                "Authored combat consequences persist in ordinary v2 save flags");
        }

        // Exercise the SAME E/Shift+G activation that players use in all
        // seven authored scenes. A device used during an almost-complete
        // enemy attack must CANCEL that original strike and its owner-tagged
        // ground warning, rather than delaying a 20ms contact until stun ends.
        public static void AllSevenDevicesInterruptActualCommittedWindups(Campaign data)
        {
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var warningField=typeof(GameCore).GetField("rescueWindupEffects",flags);
            var lane=typeof(GameCore).GetMethod("ClearAttackPath",flags);
            Check(warningField!=null&&lane!=null,
                "Test the real actor-owned combat-warning and terrain pipelines");
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.Enter(room);g.enemies.Clear();
                WorldObject device=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(device!=null&&PlaceWithinTalkReach(g,device),
                    "Every region exposes its authentic physical tactical device");
                Point origin=new Point(device.x,device.y),foot=new Point();
                bool found=false;
                for(int dy=-5;dy<=5&&!found;dy++)
                for(int dx=-5;dx<=5&&!found;dx++)
                {
                    Point candidate=new Point(origin.x+dx,origin.y+dy);
                    float dist=(candidate-origin).Length;
                    if(dist<2.5f||dist>5.7f||!g.CanStand(candidate)||
                       !(bool)lane.Invoke(g,new object[]{origin,candidate}))
                        continue;
                    foot=candidate;found=true;
                }
                Check(found,"The original field device has a real, exposed attack lane");
                Enemy attacker=new Enemy(94000+room,room%3,
                    foot.x,foot.y,120f);
                g.enemies.Add(attacker);
                attacker.state=1;attacker.timer=.02f;
                attacker.windupDuration=.65f;attacker.target=g.Position;
                attacker.targetingWayfarer=true;
                attacker.targetingReliefCivilian=true;
                attacker.targetingAfterlightStock=true;
                attacker.targetingOuterPatrolStock=true;
                attacker.targetingSagaGuardEvidence=true;
                var ownerWarnings=(System.Runtime.CompilerServices.
                    ConditionalWeakTable<Enemy,Effect>)warningField.GetValue(g);
                Effect warning=new Effect("danger",attacker.target,.65f)
                    {radius=1.2f};
                g.effects.Add(warning);ownerWarnings.Add(attacker,warning);
                Effect unrelated=new Effect("danger",origin,.65f){radius=1f};
                g.effects.Add(unrelated);
                g.mode="world";g.save.energy=100f;
                Check(g.QuickActivateFieldTactic()&&g.save.energy==86f&&
                    g.FieldTacticCooldown(device)>23f,
                    "The actual live device executes one paid, in-range counter");
                Effect stillOwned;
                Check(attacker.state==0&&attacker.stun>=.54f&&
                    attacker.timer>=.45f&&
                    !ownerWarnings.TryGetValue(attacker,out stillOwned)&&
                    !g.effects.Contains(warning)&&g.effects.Contains(unrelated),
                    "Device counter cancels the exact unspent strike and warning, without erasing another effect");
                Check(!attacker.targetingWayfarer&&
                    !attacker.targetingReliefCivilian&&
                    !attacker.targetingAfterlightStock&&
                    !attacker.targetingOuterPatrolStock&&
                    !attacker.targetingSagaGuardEvidence,
                    "Old mission targets cannot survive an interrupted attack");
                Check(g.effects.Exists(e=>e.kind=="number"&&
                    e.text==g.L("攻击中断！","ATTACK INTERRUPTED")),
                    "The actual combat HUD receives an explicit interruption cue");
                float hp=g.save.health;
                for(int frame=0;frame<12;frame++)g.Tick(.05f,new Point());
                Check(g.save.health==hp&&g.projectiles.Count==0&&
                    attacker.state==0,
                    "The delayed pre-counter strike cannot fire when the stun ends");
            }
        }

        // A mechanism can be reachable yet useless if every actual campaign
        // soldier spawns in a different encounter district. Exercise the
        // unchanged 6/8-enemy rosters, not a hand-placed test enemy.
        public static void FourChapterTacticsOccupyLiveBattleLanes(Campaign data)
        {
            for(int room=1;room<=4;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;g.Enter(room);
                WorldObject device=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(device!=null&&g.CanStand(new Point(device.x,device.y)),
                    "Each chapter tactical device stands on actual walkable ground");
                Check(g.NavigationPathFrom(g.Position,
                    new Point(device.x,device.y),true).Count>0,
                    "The original chapter entrance has a real route to its combat mechanism");
                foreach(WorldObject prop in g.Map.objects)
                    if(!System.Object.ReferenceEquals(prop,device))
                        Check((new Point(prop.x,prop.y)-
                            new Point(device.x,device.y)).Length>=3f,
                            "A usable device needs its own interaction clearing");
                Check(g.FieldTacticLiveThreatCount(device)>=1,
                    "The natural required chapter roster must provide real visible targets for the placed mechanism");
                g.enemies.Clear();
                Check(g.FieldTacticLiveThreatCount(device)==0,
                    "An empty battlefield cannot display phantom viable targets");
            }
        }

        // These are accepted quest/resource objects with real E hitboxes,
        // not background scenery. Preserve every identity and story handler
        // while separating formerly overlapping map sources.
        public static void ChapterEvidenceAndResourcesHaveDistinctInteractionPads(Campaign data)
        {
            int[] rooms={2,2,3,3,4,4};
            string[] moved={"archivefragment2","afterlightoutlet2",
                "wood3","ore3","lastDirectiveEvidence0","annex_camp_4"};
            string[] neighbor={"survey2","stormrelay2",
                "witnessnpc3","rootcounter3","ridge_route_4_1",
                "silentledger_record"};
            for(int i=0;i<moved.Length;i++)
            {
                GameCore g=Fresh(data);g.save.stage=3;g.Enter(rooms[i]);
                WorldObject a=Array.Find(g.Map.objects,o=>o.id==moved[i]);
                WorldObject b=Array.Find(g.Map.objects,o=>o.id==neighbor[i]);
                Check(a!=null&&b!=null,
                    "Existing story and resource targets retain their exact authored identities");
                Point foot=new Point(a.x,a.y),other=new Point(b.x,b.y);
                Check(g.CanStand(foot),
                    "Repositioned object must be on actual legal game terrain: "+a.id);
                Check((foot-other).Length>=2.5f,
                    "Nearby quest sources need individually readable E interaction clearings: "+a.id);
            }
        }

        // Different farshore climates must change live roles, not only
        // background colors. Retain exactly seven correctly placed combatants.
        public static void FarshoreOpeningBattlesHaveDistinctBiomeRoles(Campaign data)
        {
            int[][] roles={
                new[]{1,0,1,0,2,1,0},
                new[]{2,1,2,0,1,2,0},
                new[]{0,0,2,0,1,2,0}
            };
            var signatures=new HashSet<string>();
            for(int room=5;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.Enter(room);
                Check(g.enemies.Count==7,
                    "Distinct outer biome compositions never alter required seven-enemy roster");
                string identity="";
                for(int slot=0;slot<7;slot++)
                {
                    Enemy foe=g.enemies[slot];
                    Check(foe.kind==roles[room-5][slot]&&
                        foe.hp>0&&!foe.boss&&g.CanStand(foe.Position)&&
                        g.NavigationPathFrom(g.Position,foe.Position,true).Count>0,
                        "Real room "+room+" role "+slot+" must occupy walkable old progression lane");
                    identity+=foe.kind.ToString();
                    for(int prev=0;prev<slot;prev++)
                        Check((foe.Position-g.enemies[prev].Position).Length>=2.45f,
                            "Distinct biome fighters cannot collapse onto each other");
                }
                Check(signatures.Add(identity),
                    "Three expedition terrains should produce three different combat role profiles");
                WorldObject device=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(device!=null&&g.FieldTacticLiveThreatCount(device)>0,
                    "Distinct biome formations leave real enemies in reach of their unique tactical device");
                g.PrepareSave();
                Check(g.save.version==2,
                    "Authored combat variety does not introduce a new save format");
            }
        }

        // Rendering a monster over a ruin, house, wreck or tall pine
        // looks like overlapping unfinished map props. The authored
        // regional battles must keep their unchanged live counts while
        // reserving the *visual* decoration feet, not block the tiles.
        public static void RegionalBattleFightersNeverSpawnInsideMajorDecor(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.Enter(room);
                int count=room<=2?6:room<=4?8:7;
                Check(g.enemies.Count==count,
                    "Visual readability cannot remove the existing mandatory actors");
                foreach(Enemy enemy in g.enemies)
                {
                    Check(g.CanStand(enemy.Position)&&
                        g.NavigationPathFrom(g.Position,enemy.Position,true).Count>0,
                        "New visual clearance must keep every foe on a genuine accessible road");
                    foreach(Decoration d in g.Map.decor)
                    {
                        float pad=0f;
                        switch(d.kind)
                        {
                            case "tree":case "snowpine":case "column":
                            case "obelisk":case "ruin":case "house":
                            case "lighthouse":case "tent":case "skiff":
                                pad=1.45f;break;
                            case "crystal":case "palm":case "boat":
                            case "barrel":pad=1.1f;break;
                        }
                        Check(pad==0f||
                            (enemy.Position-new Point(d.x,d.y)).Length>=pad,
                            "Room "+room+" battle actor must not appear inside "+d.kind+" at its physical visual foot");
                    }
                }
                Check(g.save.version==2,
                    "Decoration-aware encounter stage cannot migrate an old save");
            }
        }

        // Every region must spawn actual living actors on the original
        // walkable battlefield; only physical counterplay, Hit defeats
        // and source-bound signing may award the first-clear report.
        public static void SevenFieldTacticDrillsRequireRealCounterKillsAndReceipt(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Trials must use the original combat death pipeline");
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.Enter(room);g.enemies.Clear();
                WorldObject source=Array.Find(g.Map.objects,
                    o=>o.id=="field_tactic_"+room&&o.kind=="fielddevice");
                Check(source!=null&&PlaceWithinTalkReach(g,source),
                    "Region "+room+" has a physically interactable original device");
                Point hero=g.Position;
                g.Talk(source);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_begin");
                Check(begin>=0,"Region "+room+" genuinely offers its optional live combat drill");
                int coins=g.save.coins,shards=g.save.shards;
                g.Choose(begin);
                Check(g.FieldTacticTrialAt(source)&&
                    !g.FieldTacticTrialAt(new WorldObject{
                        id=source.id,kind=source.kind,x=source.x,y=source.y}),
                    "Only exact source-bound active mechanism can claim a live trial GameView status");
                Check(g.FieldTacticTrialActive&&
                    g.FieldTacticTrialRemaining==2&&g.enemies.Count==2&&
                    !g.FieldTacticTrialReady&&!g.FieldTacticTrialUsed,
                    "Region "+room+" spawns both real contracted actors");
                Enemy[] assigned=g.enemies.ToArray();
                foreach(Enemy foe in assigned)
                {
                    Check(g.IsFieldTacticTrialTarget(foe),
                        "Only authenticated living contracted actors receive the visible trial marker");
                    Check(foe.hp>0f&&!foe.boss&&g.CanStand(foe.Position)&&
                        g.NavigationPathFrom(hero,foe.Position,true).Count>0&&
                        (foe.Position-new Point(source.x,source.y)).Length<=5.8f,
                        "Live trial attacker must be on authentic reachable counterplay ground");
                }
                g.save.energy=100f;
                Check(g.QuickActivateFieldTactic()&&g.FieldTacticTrialUsed&&
                    g.save.energy==86f&&g.FieldTacticTrialRemaining==2,
                    "Real tactical activation hits at least one assigned living raider");
                foreach(Enemy foe in assigned)
                    hit.Invoke(g,new object[]{foe,600f,false,null,null});
                Check(Array.TrueForAll(assigned,e=>!g.IsFieldTacticTrialTarget(e)),
                    "Defeated registered bodies must lose their live world target marker");
                Check(g.FieldTacticTrialReady&&
                    g.FieldTacticTrialRemaining==0&&!g.FieldTacticTrialDone(room),
                    "Real defeats earn a report opportunity, not immediate free loot");
                g.save.x=hero.x;g.save.y=hero.y;
                g.Close();g.Talk(source);
                int report=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_report");
                Check(report>=0,"The same physical source offers only its earned report");
                g.Choose(report);
                Check(!g.FieldTacticTrialAt(source),
                    "Source-bound GameView trial status clears immediately on signing");
                Check(g.FieldTacticTrialDone(room)&&
                    !g.FieldTacticTrialActive&&
                    g.save.coins>=coins+22+room*4&&
                    g.save.shards>=shards+1&&
                    g.GetQuest("tactic_trials").steps[room-1].done,
                    "Signed trial gives its one-time real reward and journal progress");
                int signedCoins=g.save.coins;
                g.Close();g.Talk(source);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_begin")<0&&
                    g.save.coins==signedCoins,
                    "Veteran trial receipt prevents repeat-farming its first clear");
                g.PrepareSave();
                GameCore restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.FieldTacticTrialDone(room)&&
                    !restored.FieldTacticTrialActive&&
                    restored.GetQuest("tactic_trials").steps[room-1].done,
                    "A legacy v2 save retains only signed truth, not transient foes");
            }
        }

        // A signed field result is not a disposable modal. Protect the
        // LIVE contract during both a portal Enter and the quick town route;
        // after real settlement, normal travel remains possible.
        // A generic main-story marker cannot send a player across the
        // world while a physical local battle needs its counter, opponents
        // or signed return. Preserve their manual tracker across all steps.
        public static void FieldTacticTrialRequiresUsableEnergyAndReadyDevice(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject source=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(source!=null&&PlaceWithinTalkReach(g,source),
                "Preparation requires authentic reachable field equipment");
            g.save.energy=13f;
            g.Talk(source);
            int action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(action>=0,"The real source offers the usual trial dialogue");
            g.Choose(action);
            Check(!g.FieldTacticTrialActive&&g.enemies.Count==0&&
                g.save.energy==13f&&!g.FieldTacticTrialDone(1),
                "Starting without actual counter energy spawns no raiders and spends nothing");
            g.save.energy=14f;
            g.Close();g.Talk(source);
            action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            g.Choose(action);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "At exact legal energy the real walkable drill is available");
            g.Close();g.Talk(source);
            action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_abort");
            Check(action>=0,"The physical station supports a legal trial abort");
            g.Choose(action);
            Check(!g.FieldTacticTrialActive&&!g.FieldTacticTrialDone(1),
                "Preparation and cancellation do not grant a receipt");
            GameCore prior=Fresh(data);prior.save.stage=3;prior.Enter(1);
            WorldObject device=Array.Find(prior.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(prior,device)&&
                prior.FieldTacticLiveThreatCount(device)>0,
                "Original required room roster exposes genuine enemies to the device");
            prior.save.energy=100f;
            Check(prior.QuickActivateFieldTactic()&&
                prior.FieldTacticCooldown(device)>23f,
                "A previous real battlefield activation imposes genuine source cooldown");
            prior.enemies.Clear();
            int energy=(int)prior.save.energy;
            prior.Close();prior.Talk(device);
            action=Array.FindIndex(prior.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(action>=0,"Even cooling equipment describes the trial option");
            prior.Choose(action);
            Check(!prior.FieldTacticTrialActive&&prior.enemies.Count==0&&
                prior.save.energy==energy&&!prior.FieldTacticTrialDone(1),
                "Cooldown prevents an unwinnable ambush without spending energy or signing completion");
        }

        public static void FieldTacticFollowUpRequiresRealExposedActorAndSignedReceipt(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Real combat damage owns tactical follow-up success");
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("exp_intro");g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");g.Enter(room);g.enemies.Clear();
                WorldObject source=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(source!=null&&PlaceWithinTalkReach(g,source),
                    "Follow-up must originate from the actual original field device");
                g.save.energy=100f;g.Talk(source);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_begin");
                Check(begin>=0,"Every region owns a real optional drill");
                g.Choose(begin);
                var assigned=g.enemies.ToArray();
                Check(assigned.Length==2&&g.FieldTacticTrialActive&&
                    !g.FieldTacticTrialChainEarned,
                    "Only authentic assigned actors belong to the combat window");
                Check(g.QuickActivateFieldTactic()&&
                    g.FieldTacticTrialChainSecondsLeft>7.9f,
                    "Device creates a genuine eight-second physics-time counter window");
                Enemy exposed=Array.Find(assigned,e=>
                    g.IsFieldTacticTrialComboTarget(e));
                Check(exposed!=null,
                    "The exposed bonus target is an actual attacked live raider");
                Enemy other=assigned[0]==exposed?assigned[1]:assigned[0];
                Enemy impostor=new Enemy(exposed.id,exposed.kind,
                    exposed.x,exposed.y,exposed.hp);
                Check(!g.IsFieldTacticTrialComboTarget(impostor),
                    "Matching numeric ID cannot fake the exact actor bonus");
                int beforeCoins=g.save.coins, beforeShards=g.save.shards;
                int beforeXp=g.save.xp,beforeLevel=g.save.level;
                hit.Invoke(g,new object[]{exposed,600f,false,null,null});
                Check(g.FieldTacticTrialChainEarned&&
                    g.FieldTacticTrialChainSecondsLeft==0f&&
                    g.save.shards==beforeShards,
                    "Only live exposed combat death arms bonus; never pay before signing");
                hit.Invoke(g,new object[]{other,600f,false,null,null});
                Check(g.FieldTacticTrialReady&&
                    g.save.shards==beforeShards&&g.save.coins==beforeCoins,
                    "Even complete battle cannot auto-credit a claimed precision bonus");
                g.Close();g.Talk(source);
                int report=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trial_report");
                Check(report>=0,"The physical source signs a real cleared counter");
                g.Choose(report);
                Check(g.FieldTacticTrialDone(room)&&
                    !g.FieldTacticTrialChainEarned,
                    "The exact signed follow-up cannot remain active in a completed trial");
                Check(g.save.shards>=beforeShards+2,
                    "The actual exposed combat kill must pay both base and optional shards");
                // AddXp may convert XP into character levels. A real
                // level-up resets the within-level counter; requiring raw
                // beforeXp+reward would reject a correctly paid promotion.
                Check(g.save.level>beforeLevel||
                    g.save.xp>=beforeXp+48+room*8+24,
                    "Real signed precision XP must be credited or advance the actual character level");
                int paid=g.save.shards;
                g.Close();g.Talk(source);
                Check(g.save.shards==paid&&!g.FieldTacticTrialActive,
                    "Repeated interaction cannot farm exposure reward");
                g.PrepareSave();
                GameCore reload=new GameCore(data,CloneSave(g.save));
                Check(reload.save.version==2&&reload.FieldTacticTrialDone(room)&&
                    !reload.FieldTacticTrialChainEarned,
                    "Save v2 stores the signed milestone, never temporary exposure");
            }
        }

        // A legitimate exposed foe must stop offering a precision bonus
        // after EIGHT SECONDS OF REAL WORLD TICKS, but the standard field
        // trial remains solvable. The source can still sign the two kills
        // without upgrading the report to a precision reward.
        public static void FieldTacticFollowUpExpiresWithoutBlockingOrdinaryVictory(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Expiry test must use real combat strikes");
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject source=Array.Find(g.Map.objects,
                o=>o.kind=="fielddevice"&&o.id=="field_tactic_1");
            Check(source!=null&&PlaceWithinTalkReach(g,source),
                "True physical device exists on the first chapter battlefield");
            g.save.energy=100f;g.Talk(source);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"The existing source offers an actual combat contract");
            g.Choose(begin);
            var assigned=g.enemies.ToArray();
            Check(assigned.Length==2&&g.QuickActivateFieldTactic()&&
                g.FieldTacticTrialChainSecondsLeft>7.9f,
                "Real device begins the optional counter window");
            // Preserve the actual world simulation/AI clock and hit-stop;
            // do not claim time passes while reading menus.
            g.save.health=10000f;g.save.maxHealth=10000f;
            for(int frame=0;frame<200;frame++)
                g.Tick(.05f,new Point());
            Check(g.FieldTacticTrialActive&&
                g.FieldTacticTrialChainSecondsLeft==0f&&
                !g.FieldTacticTrialChainEarned,
                "Ten seconds of true world ticks expire an eight-second bonus");
            foreach(Enemy foe in assigned)
                Check(!g.IsFieldTacticTrialComboTarget(foe),
                    "No stale highlighted enemy may advertise an expired follow-up");
            int before=g.save.shards;
            foreach(Enemy foe in assigned)
                hit.Invoke(g,new object[]{foe,600f,false,null,null});
            Check(g.FieldTacticTrialReady&&!g.FieldTacticTrialChainEarned,
                "Late physical kills clear the normal trial, but cannot claim a combo");
            g.Close();g.Talk(source);
            int report=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_report");
            Check(report>=0,"A late standard clear still has a valid source receipt");
            g.Choose(report);
            Check(g.FieldTacticTrialDone(1)&&
                g.save.shards==before+1&&!g.FieldTacticTrialActive,
                "The expired follow-up signs exactly the ordinary first-clear shard");
        }

        public static void FieldTacticTrialWaypointsFollowTheRequiredBattleStage(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject device=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(g,device),
                "Battle navigation uses a real originally authored source");
            string manual=g.save.trackedQuest;
            g.Talk(device);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(start>=0,"The original source actually offers this trial");
            g.Choose(start);
            Point site=new Point(device.x,device.y);
            Check(g.FieldTacticTrialActive&&!g.FieldTacticTrialUsed&&
                (g.FieldTacticTrialGuideTarget-site).Length<.001f&&
                g.RecommendedContinuationGuide.id=="tactic_trials"&&
                (g.RecommendedContinuationGuide.target-site).Length<.001f&&
                g.save.trackedQuest==manual,
                "Before counterplay, the live guide points to the nearby device, not an arbitrary fighter");
            g.save.energy=100f;
            Check(g.QuickActivateFieldTactic()&&g.FieldTacticTrialUsed,
                "Real device activation advances the live trial stage");
            var foes=g.enemies.ToArray();
            Point nearest=foes[0].Position;
            if((foes[1].Position-g.Position).Length<
               (foes[0].Position-g.Position).Length)nearest=foes[1].Position;
            Check((g.FieldTacticTrialGuideTarget-nearest).Length<.001f&&
                (g.RecommendedContinuationGuide.target-nearest).Length<.001f,
                "After actual counterplay, navigation pursues the nearest living designated fighter");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Waypoint progression requires the real combat defeat");
            hit.Invoke(g,new object[]{foes[0],600f,false,null,null});
            Check((g.FieldTacticTrialGuideTarget-foes[1].Position).Length<.001f&&
                g.FieldTacticTrialRemaining==1,
                "After an authentic defeat the marker immediately follows the remaining fighter");
            hit.Invoke(g,new object[]{foes[1],600f,false,null,null});
            Check(g.FieldTacticTrialReady&&
                (g.FieldTacticTrialGuideTarget-site).Length<.001f&&
                (g.RecommendedContinuationGuide.target-site).Length<.001f&&
                g.save.trackedQuest==manual,
                "After clearing both actors, navigation leads to the signed return and does not hijack the manual tracker");
            g.Close();g.Talk(device);
            int sign=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_report");
            Check(sign>=0,"Actual source offers an earned return choice");
            g.Choose(sign);
            Check(!g.FieldTacticTrialActive&&
                g.save.trackedQuest==manual&&
                g.RecommendedContinuationGuide.id==manual,
                "Signing completes the temporary objective and restores the player's original quest");
        }

        public static void FieldTacticTrialsCannotBeLostToUnsignedTravel(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject site=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "Travel lock uses a genuine walkable device, not a synthetic flag");
            g.Talk(site);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"A chapter's real drill starts at its own station");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "Travel test has living assigned opponents");
            g.Enter(0);
            Check(g.save.room==1&&g.FieldTacticTrialActive&&
                g.enemies.Count==2,
                "Direct region switch cannot delete live trial actors silently");
            g.ReturnToTown();
            Check(g.save.room==1&&g.FieldTacticTrialActive&&
                g.enemies.Count==2,
                "Fast-return path is equally protected from live-trial bypass");
            g.Close();g.Talk(site);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_abort");
            Check(abort>=0,"Physical device offers a voluntary release");
            g.Choose(abort);
            Check(!g.FieldTacticTrialActive&&g.enemies.Count==0,
                "Explicit withdrawal truly retires both designated actors");
            g.Enter(0);
            Check(g.save.room==0&&g.save.version==2,
                "Region travel works normally after explicit cancellation");
        }

        public static void FieldTacticTrialWithoutCounterFailsCleanlyAndRestarts(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Failure test uses actual physical combat deaths");
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject site=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "Failure route starts at a real device");
            g.Talk(site);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "Original source spawns a genuine two-foe trial");
            int coins=g.save.coins,shards=g.save.shards,xp=g.save.xp;
            foreach(Enemy e in g.enemies.ToArray())
                hit.Invoke(g,new object[]{e,600f,false,null,null});
            Check(!g.FieldTacticTrialUsed&&g.FieldTacticTrialRemaining==0&&
                !g.FieldTacticTrialReady,
                "Killing without using the field mechanism cannot earn a receipt");
            // Hit() deliberately freezes live world simulation for a
            // short, authored impact stop after each actual kill. Wait for
            // the resumed physical world ticks, not a fictional instant
            // cleanup during the first frozen 20ms frame.
            for(int frame=0;frame<12&&g.FieldTacticTrialActive;frame++)
                g.Tick(.05f,new Point());
            Check(!g.FieldTacticTrialActive,
                "No-counter trial must end promptly after real hit-stop resolves");
            Check(g.enemies.Count==0,
                "No-counter cleanup must retire only the two trial actors: remaining "+
                    g.enemies.Count);
            Check(!g.FieldTacticTrialDone(1),
                "Uncountered fighters cannot sign a saved trial receipt");
            Check(g.save.coins==coins,
                "Failed trial must award zero bonus coins: "+g.save.coins+" versus "+coins);
            Check(g.save.shards==shards,
                "Failed trial must award zero bonus shards: "+g.save.shards+" versus "+shards);
            Check(g.save.xp==xp,
                "Failed trial must award zero trial XP: "+g.save.xp+" versus "+xp);
            g.Close();g.Talk(site);
            begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"After clear failure, the same real trial can be restarted");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "Restart produces exactly two fresh live combat opponents");
        }

        public static void FieldTacticTrialDisappearanceAndWithdrawalCannotClaimReward(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            g.enemies.Clear();
            WorldObject source=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(source!=null&&PlaceWithinTalkReach(g,source),
                "An authentic chapter device supports contract cancellation");
            int coins=g.save.coins;
            g.Talk(source);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"Physical source offers a new real trial");
            g.Choose(begin);
            Check(g.FieldTacticTrialActive&&g.enemies.Count==2,
                "The live contract owns two exact actors");
            g.enemies.RemoveAt(0);
            g.Tick(.02f,new Point());
            Check(!g.FieldTacticTrialActive&&!g.FieldTacticTrialDone(1)&&
                g.save.coins==coins,
                "Deleting an actor without genuine combat must void its claim");
            g.Close();g.Talk(source);
            begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_begin");
            Check(begin>=0,"A failed drill may be physically retried");
            g.Choose(begin);
            g.Close();g.Talk(source);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="field_tactic_trial_abort");
            Check(abort>=0,"Live trial supports voluntary source-bound withdrawal");
            g.Choose(abort);
            Check(!g.FieldTacticTrialActive&&g.enemies.Count==0&&
                !g.FieldTacticTrialDone(1)&&g.save.coins==coins,
                "Withdrawal removes drill actors and yields no false rewards");
        }

        // The three outer regions are distinct optional battlefields, not
        // decoration pasted into the map. Check real story gates, original
        // seven-actor roster, E interaction and one-time saved mastery.
        public static void OuterExpeditionFieldDevicesControlRealBattleAndSaveReward(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.mode="world";
            g.save.flags.Add("exp_intro");
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report");
            for(int room=5;room<=7;room++)
            {
                g.Enter(room);
                WorldObject device=Array.Find(g.Map.objects,
                    o=>o.kind=="fielddevice"&&o.id=="field_tactic_"+room);
                Check(g.save.room==room,"Outer region entry succeeds: "+room);
                Check(device!=null,"Outer region has its real placed device: "+room);
                Check(g.enemies.Count==7,
                    "Outer region "+room+" retains seven required fighters; got "+g.enemies.Count);
                Check(g.CanStand(new Point(device.x,device.y)),
                    "Outer region "+room+" mechanism is physically standable");
                Check(g.NavigationPathFrom(g.Position,
                    new Point(device.x,device.y),true).Count>0,
                    "Outer battlefield has real walkable approach from the story entrance");
                foreach(WorldObject other in g.Map.objects)
                    if(!System.Object.ReferenceEquals(device,other))
                        Check((new Point(other.x,other.y)-
                            new Point(device.x,device.y)).Length>=3f,
                            "Outer devices must not steal crowded NPC E interaction pads");
                Check(g.FieldTacticLiveThreatCount(device)>=1,
                    "Natural regional attackers can be genuinely caught by the device");
                g.enemies.Clear();
                Check(g.FieldTacticLiveThreatCount(device)==0&&
                    PlaceWithinTalkReach(g,device),
                    "An empty combat site stays reachable but reports no ghost threats");
                float energy=90f;g.save.energy=energy;
                g.Talk(device);
                int action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                Check(action>=0,"The in-world outer device opens a real action");
                g.Choose(action);
                Check(g.save.energy==energy&&g.FieldTacticCooldown(device)==0f&&
                    !g.Has("field_tactic_used_"+room),
                    "Empty activation never consumes energy, cooldown or mastery");
                Point center=new Point(device.x,device.y),enemyFoot=new Point();
                var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(clear!=null,"Live projectile LOS remains available");
                bool found=false;
                for(int dy=-5;dy<=5&&!found;dy++)
                for(int dx=-5;dx<=5&&!found;dx++)
                {
                    Point foot=center+new Point(dx,dy);
                    float distance=(foot-center).Length;
                    if(distance<2.5f||distance>5.7f||
                        !g.CanStand(foot)||
                        !(bool)clear.Invoke(g,new object[]{center,foot}))continue;
                    enemyFoot=foot;found=true;
                }
                Check(found,"Each outer device has an actual nearby walkable enemy pad");
                Enemy target=new Enemy(98000+room,room==5?1:room==6?2:0,
                    enemyFoot.x,enemyFoot.y,100f);
                g.enemies.Add(target);
                Check(g.FieldTacticLiveThreatCount(device)==1,
                    "Outer tactic only counts live physically visible attacker");
                g.Talk(device);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                Check(action>=0,"Combat use offers the same real source-bound choice");
                g.Choose(action);
                Check(g.save.energy==energy-14f&&g.FieldTacticCooldown(device)>23f&&
                    g.Has("field_tactic_used_"+room)&&target.hp==100f,
                    "Physical outer counter spends energy and marks exactly one genuine use");
                if(room==5)Check(target.stun>=2.7f,
                    "Fog horn must genuinely interrupt a bowman");
                if(room==6)Check(target.armorOpenTime>=3.9f,
                    "Prism must genuinely expose armored combatants");
                if(room==7)Check(target.stun>=2.6f&&target.stagger>=1.6f,
                    "Cold bell must genuinely stop an advancing melee raider");
                g.Talk(device);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_tactic_trigger");
                g.Choose(action);
                Check(g.save.energy==energy-14f,
                    "An outer device cannot charge twice during cooldown");
                g.Close();
            }
            Check(g.OuterTacticMasteryCount==3&&
                g.Has("outer_field_tactics_mastered")&&
                g.save.skillPoints==1&&g.FieldTacticMasteryCount==0&&
                !g.Has("field_tactics_mastered"),
                "Separate three-region mastery never invents original chapter progress");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                restored.OuterTacticMasteryCount==3&&
                restored.Has("outer_field_tactics_mastered")&&
                restored.save.skillPoints==1,
                "Existing v2 save flags persist expedition rewards without new fields");
        }

        // This is an actual gameplay action: Shift+G shares the E choice's
        // physical LOS, energy and cooldown transaction without a modal.
        public static void TacticalQuickUseSharesRealBattleCostsWithoutStealingOtherInteractions(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            WorldObject device=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(g,device),
                "Quick tactic needs a real walkable reachable map object");
            g.enemies.Clear();g.save.energy=100f;
            Check(g.QuickActivateFieldTactic()&&g.mode=="world"&&
                g.save.energy==100f&&!g.Has("field_tactic_used_1"),
                "Quick use with no genuine foe costs nothing and cannot forge a first-use reward");
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(clear!=null,"Quick use shares the production LOS sampler");
            Point source=new Point(device.x,device.y),foot=new Point();
            bool found=false;
            for(int dy=-5;dy<=5&&!found;dy++)
            for(int dx=-5;dx<=5&&!found;dx++)
            {
                Point p=source+new Point(dx,dy);
                float length=(p-source).Length;
                if(length<2.5f||length>5.7f||!g.CanStand(p)||
                    !(bool)clear.Invoke(g,new object[]{source,p}))continue;
                foot=p;found=true;
            }
            Check(found,"Battlefield quick use can reach actual legal enemy feet");
            Enemy foe=new Enemy(99671,0,foot.x,foot.y,100f);
            g.enemies.Add(foe);
            Check(g.FieldTacticLiveThreatCount(device)==1,
                "The world prompt and quick-use target share the same real attacker");
            Check(g.QuickActivateFieldTactic()&&g.mode=="world"&&
                g.conversation==null&&g.save.energy==86f&&
                foe.stun>=2.29f&&g.FieldTacticCooldown(device)>23f&&
                g.Has("field_tactic_used_1"),
                "Quick activation genuinely ensnares the enemy without switching out of live gameplay");
            Check(g.QuickActivateFieldTactic()&&g.save.energy==86f&&
                g.FieldTacticMasteryCount==1,
                "Cooldown never double-charges or duplicates first use");
            g.save.x=source.x+12f;g.save.y=source.y;
            Check(!g.QuickActivateFieldTactic()&&g.save.energy==86f,
                "Distant input cannot operate a mechanism through a false remote target");
        }

        // A congested chapter source is not permission to pick the closest
        // prop and accidentally advance the wrong story. The original
        // campaign scout is the authoritative guide target: a neighbor may
        // crowd the same physical foot, but Shift+O only focuses the unique
        // matching quest object and leaves E as a separate deliberate press.
        public static void NearbyQuestFocusSelectsUniqueReachableStorySource(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=0;g.Enter(1);
            g.enemies.Clear();g.mode="world";g.TrackQuest("main");
            WorldObject scout=Array.Find(g.Map.objects,
                o=>o.kind=="scout"&&g.IsObjectPresent(o));
            Check(scout!=null&&PlaceWithinTalkReach(g,scout),
                "The authentic first-chapter scout is physically reachable");
            QuestGuide guide=g.RecommendedContinuationGuide;
            Check(guide.id=="main"&&guide.room==1&&
                (guide.target-new Point(scout.x,scout.y)).Length<.01f,
                "Current main quest points to the original living scout");
            MapData region=g.Map;
            WorldObject[] original=region.objects;
            WorldObject decoy=new WorldObject{
                kind="campfire",id="nearby_nonquest_source",
                name="相邻路边火盆",en="Adjacent fire",
                x=g.Position.x,y=g.Position.y
            };
            WorldObject impostor=new WorldObject{
                kind="campfire",id="coincident_quest_spoof",
                name="伪造的同坐标来源",en="Co-located impostor",
                x=scout.x,y=scout.y
            };
            try
            {
                WorldObject[] expanded=new WorldObject[original.Length+1];
                Array.Copy(original,expanded,original.Length);
                expanded[expanded.Length-1]=decoy;region.objects=expanded;
                Check(g.NearbyInteractionCount()>=2&&
                    g.NearbyInteractionChoices().Contains(decoy)&&
                    System.Object.ReferenceEquals(g.NearbyQuestInteraction(),scout),
                    "Only the real current quest source is eligible in a reachable NPC crowd");
                int decoyIndex=g.NearbyInteractionChoices().IndexOf(decoy);
                Check(decoyIndex>=0&&g.FocusNearbyInteraction(decoyIndex)&&
                    System.Object.ReferenceEquals(g.Nearest(),decoy),
                    "Manual selection can still deliberately choose the neighbor");
                Check(g.FocusNearbyQuestInteraction()&&
                    System.Object.ReferenceEquals(g.Nearest(),scout),
                    "One deliberate focus switches E to the authenticated current quest source");
                Check(g.save.version==2&&!g.Has("scout_saved"),
                    "Focusing never opens dialogue or silently awards quest progress");
                g.FocusNearbyInteraction(decoyIndex);
                expanded=new WorldObject[original.Length+2];
                Array.Copy(original,expanded,original.Length);
                expanded[expanded.Length-2]=decoy;
                expanded[expanded.Length-1]=impostor;region.objects=expanded;
                Check(g.NearbyQuestInteraction()==null&&
                    !g.FocusNearbyQuestInteraction()&&
                    System.Object.ReferenceEquals(g.Nearest(),decoy),
                    "Two overlapping plausible targets are ambiguous and cannot steal an explicit player focus");
                region.objects=original;
                g.save.x+=12f;
                Check(g.NearbyQuestInteraction()==null&&
                    !g.FocusNearbyQuestInteraction(),
                    "Cannot focus a remote quest source through scene distance");
                g.save.x=scout.x;g.save.y=scout.y;
                g.save.flags.Add("scout_saved");
                Check(g.NearbyQuestInteraction()==null,
                    "Completed/disappeared scout cannot reappear as a stale quest selection");
            }
            finally{region.objects=original;}
        }

        // Crowded NPC interaction is deliberately independent of the
        // dedicated field-device shortcut. A focused NPC must not hide an
        // authentic reachable contraption or have its E selection stolen.
        public static void QuickTacticsWorkBesideFocusedNpcWithoutHijackingE(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.Enter(1);
            WorldObject device=Array.Find(g.Map.objects,
                o=>o.id=="field_tactic_1"&&o.kind=="fielddevice");
            Check(device!=null&&PlaceWithinTalkReach(g,device),
                "Tactical focus test uses live reachable field mechanism");
            Point player=g.Position;
            MapData map=g.Map;
            WorldObject[] before=map.objects;
            var neighbor=new WorldObject{
                id="test_nearby_civilian",kind="campfire",name="紧邻的居民",
                en="Nearby civilian",x=player.x,y=player.y
            };
            try
            {
                var extended=new WorldObject[before.Length+1];
                Array.Copy(before,extended,before.Length);
                extended[before.Length]=neighbor;
                map.objects=extended;
                int index=g.NearbyInteractionChoices().IndexOf(neighbor);
                Check(index>=0&&g.FocusNearbyInteraction(index)&&
                    System.Object.ReferenceEquals(g.Nearest(),neighbor),
                    "Crowded live scene preserves explicit player choice of the nearer NPC");
                g.enemies.Clear();
                var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(clear!=null,"Real attack LOS verifies crowded tactic enemy");
                Point center=new Point(device.x,device.y),foot=new Point();
                bool found=false;
                for(int dy=-5;dy<=5&&!found;dy++)
                for(int dx=-5;dx<=5&&!found;dx++)
                {
                    Point p=center+new Point(dx,dy);
                    float distance=(p-center).Length;
                    if(distance<2.5f||distance>5.7f||!g.CanStand(p)||
                       !(bool)clear.Invoke(g,new object[]{center,p}))continue;
                    foot=p;found=true;
                }
                Check(found,"Focused civilian does not block genuine combat line");
                Enemy foe=new Enemy(99731,0,foot.x,foot.y,85f);
                g.enemies.Add(foe);g.save.energy=90f;
                Check(g.QuickActivateFieldTactic()&&g.save.energy==76f&&
                    foe.stun>=2.29f&&g.Has("field_tactic_used_1")&&
                    g.mode=="world",
                    "Shift+G fires real adjacent mechanism despite closer selected NPC");
                Check(System.Object.ReferenceEquals(g.Nearest(),neighbor),
                    "Quick tactics never replace the explicit NPC E interaction focus");
                int energy=(int)g.save.energy;
                Check(g.QuickActivateFieldTactic()&&g.save.energy==energy,
                    "Shift+G still honors the shared tactical cooldown");
                g.save.room=0;
                Check(!g.QuickActivateFieldTactic()&&g.save.energy==energy,
                    "Shortcut rejects foreign-room stale objects");
            }
            finally
            {
                map.objects=before;
            }
        }

        public static void FarshoreTacticsJournalRoutesOnlyThroughUnlockedRealDevices(Campaign data)
        {
            GameCore g=Fresh(data);
            QuestGuide closed=g.GetQuest("outer_tactics");
            Check(closed.id=="outer_tactics"&&closed.locked&&
                !closed.complete&&closed.steps.Count==3,
                "Unintroduced farshore adventure is available in the journal but cannot forge travel");
            g.TrackQuest("outer_tactics");
            Check(g.save.trackedQuest!="outer_tactics",
                "New game cannot pin a still-locked farshore route");
            g.save.stage=3;g.save.flags.Add("exp_intro");
            QuestGuide open=g.GetQuest("outer_tactics");
            WorldObject harbor=Array.Find(data.maps[5].objects,
                o=>o.id=="field_tactic_5");
            Check(!open.locked&&open.room==5&&harbor!=null&&
                (open.target-new Point(harbor.x,harbor.y)).Length<.001f,
                "Open harbor guides to the actual unique coastal combat object");
            g.TrackQuest("outer_tactics");
            Check(g.save.trackedQuest=="outer_tactics",
                "Player can voluntarily track real farshore devices");
            g.save.flags.Add("field_tactic_used_5");
            QuestGuide waiting=g.GetQuest("outer_tactics");
            QuestGuide story=g.GetQuest("shore");
            Check(waiting.room==story.room&&waiting.steps[0].done&&
                !waiting.steps[1].done&&!waiting.complete,
                "Locked desert sends the player through the genuine shore story instead of an invalid portal");
            g.save.flags.Add("harbor_report");
            QuestGuide desert=g.GetQuest("outer_tactics");
            WorldObject prism=Array.Find(data.maps[6].objects,
                o=>o.id=="field_tactic_6");
            Check(desert.room==6&&prism!=null&&
                (desert.target-new Point(prism.x,prism.y)).Length<.001f,
                "Harbor report opens the actual desert prism quest marker");
            g.save.flags.Add("field_tactic_used_6");
            g.save.flags.Add("desert_report");
            QuestGuide frost=g.GetQuest("outer_tactics");
            WorldObject stillbell=Array.Find(data.maps[7].objects,
                o=>o.id=="field_tactic_7");
            Check(frost.room==7&&stillbell!=null&&
                (frost.target-new Point(stillbell.x,stillbell.y)).Length<.001f,
                "Desert report opens the actual Frostchime tactical marker");
            g.save.flags.Add("field_tactic_used_7");
            QuestGuide finished=g.GetQuest("outer_tactics");
            Check(finished.complete&&!finished.locked&&
                finished.Progress==1f&&finished.steps.Count==3&&
                g.save.trackedQuest=="outer_tactics",
                "All physical field successes close the journal route without hijacking the tracked quest");
            g.PrepareSave();
            GameCore restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&
                restored.GetQuest("outer_tactics").complete&&
                restored.save.trackedQuest=="outer_tactics",
                "The existing v2 save preserves the player's selected farshore quest and completed steps");
        }

        public static void FourDifferentCampaignLevelsRetainRealOldAndNewWalkingRoutes(Campaign data)
        {
            string[][] originalMustReach={
                new[]{"chapterroad_1_0","chapterroad_1_1","chapterroad_1_2",
                    "scout","wayfarer1","witnessnpc1","wood1","shrine1",
                    "waytrail1e","rootsong_1","outer_watch_1","far_vista_1"},
                new[]{"chapterroad_2_0","chapterroad_2_1","chapterroad_2_2",
                    "rune0","rune1","rune2","wayfarer2","shrine2",
                    "waytrail2e","floodarchive_1","floodarchive_2","outer_watch_2"},
                new[]{"chapterroad_3_0","chapterroad_3_1","chapterroad_3_2",
                    "engine","witnessnpc3","ironfoundry","shrine3",
                    "pressureworks_notice","pressureworks_valve1",
                    "pressureworks_valve2","pressureworks_engine"},
                new[]{"chapterroad_4_0","chapterroad_4_1","chapterroad_4_2",
                    "triadnexus","blackoutshelter","shrine4",
                    "waytrail4e","bridge_heart","silentledger_notice",
                    "silentledger_console","outer_watch_4"}
            };
            var levelSignatures=new HashSet<string>();
            for(int room=1;room<=4;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
                g.Enter(room);
                Check(g.save.room==room&&g.CanStand(g.Position)&&
                    g.Map.rows.Length==80&&g.Map.rows[0].Length==120,
                    "Every original chapter still enters its authored 120 by 80 game world");
                Check(g.enemies.Count==(room<=2?6:8),
                    "Original mandatory live combat roster survives authored level redesign");
                // Distinct original map geography now supports distinct
                // real combat roles, instead of the same 0/1/2 pattern.
                int[][] chapterKinds={
                    new[]{0,0,1,0,1,0},
                    new[]{1,0,1,2,1,0},
                    new[]{2,0,2,1,2,0,2,1},
                    new[]{2,1,0,2,1,0,2,1}
                };
                int rosterIndex=0;
                foreach(Enemy foe in g.enemies)
                {
                    Check(rosterIndex<chapterKinds[room-1].Length&&
                        foe.kind==chapterKinds[room-1][rosterIndex],
                        "Different terrain now produces a deliberately distinct live chapter battle composition");
                    rosterIndex++;
                }
                Check(rosterIndex==chapterKinds[room-1].Length,
                    "Biome-specific tactical variety preserves the mandatory six/eight-actor campaign roster");
                Point regionSpawn=g.Position;
                foreach(Enemy enemy in g.enemies)
                {
                    Check(enemy.hp>0&&g.CanStand(enemy.Position)&&
                        g.NavigationPathFrom(regionSpawn,enemy.Position,true).Count>0,
                        "All original mandatory enemies occupy connected, physically walkable regional ground");
                    foreach(WorldObject prop in g.Map.objects)
                    {
                        float pad=(enemy.Position-new Point(prop.x,prop.y)).Length;
                        Check(pad>=1.9f,
                            "Regional fighters do not block original NPC, resource or chapter investigation props");
                        if(prop.kind=="chapterroad")
                            Check(pad>=4.15f,
                                "Mandatory regional monsters cannot camp a chapter rescue or survivor conversation");
                    }
                }
                // The original seal/barrier prerequisites still control the
                // Boss arena. Decorative new paths must not bypass them.
                if(room<=3)
                {
                    Check(!g.GateOpen,"Reauthored world retains an authentic closed chapter gate");
                    g.save.flags.Add(room==1?"scout_saved":"gate_"+room);
                    Check(g.GateOpen,"Existing old-story solution still unlocks the same guardian");
                }
                foreach(string id in originalMustReach[room-1])
                {
                    WorldObject point=Array.Find(g.Map.objects,o=>o.id==id);
                    Check(point!=null,"No accepted original or new map object was removed: "+id);
                    Point foot=new Point(point.x,point.y);
                    Check(g.CanStand(foot),
                        "Authored "+g.Map.name+" landmark is not buried in tree, flood or slag: "+id);
                    Check(g.NavigationPathFrom(regionSpawn,foot,true).Count>0,
                        "Real navigation must connect the original region entrance to "+id);
                }
                // Every chapter rescue is a real three-location journey.
                for(int n=0;n<2;n++)
                {
                    WorldObject a=Array.Find(g.Map.objects,o=>
                        o.id=="chapterroad_"+room+"_"+n);
                    WorldObject b=Array.Find(g.Map.objects,o=>
                        o.id=="chapterroad_"+room+"_"+(n+1));
                    Check(a!=null&&b!=null&&
                        (new Point(a.x,a.y)-new Point(b.x,b.y)).Length>=4f,
                        "Original civilian stories visit separate, authored physical locations");
                    Check(g.NavigationPathFrom(new Point(a.x,a.y),
                        new Point(b.x,b.y),true).Count>0,
                        "New cover must not make the actual three-stop rescue impossible");
                }
                // The four places must not be the same atlas recolored.
                // Compare a small and remote zone's real terrain pattern.
                string signature="";
                for(int y=4;y<=21;y++)
                    signature+=g.Map.rows[y].Substring(4,30);
                for(int y=55;y<70;y+=3)
                    signature+=g.Map.rows[y].Substring(80,28);
                Check(levelSignatures.Add(signature),
                    "Each chapter has unique traversable geography across the early and remote world");
            }
            // Keep the thematic changes literal, not four generic biome
            // recolors: trees, flooded islands, slag, and fortress walls.
            Check(data.maps[1].rows[4][25]=='#' &&
                data.maps[2].rows[5][24]=='~' &&
                data.maps[3].rows[5][24]=='#' &&
                data.maps[4].rows[20][30]=='#',
                "Forest roots, Ruins flood, Mine slag and Citadel barricades have physically authored distinct topology");
        }
        // These are real signed, optional world contracts: spawn via
        // physical dialogue and check every living actor's actual class.
        // A static table-only assertion would not catch a disconnected
        // placement path, an unspawned third foe, or a dead world hook.

        // A remote wilderness encounter is not a story gate, a duplicate
        // static patrol, or an instant quest reward. It wakes only at the
        // player's physical crossing with two actual reachable combatants.

        // A paid patrol does not become an automatic ambush merely because
        // the player walks past a remote ridge while its enemies are far.
        // Source-owned field contracts remain the only active wave.
        public static void RemoteRoadAmbushDoesNotHijackSignedFieldPatrol(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.room=1;
            g.save.stage=3;g.enemies.Clear();
            g.save.flags.Add("biome_field_done_1");
            WorldObject survey=Array.Find(g.Map.objects,o=>
                o.kind=="biomeSurvey"&&o.id=="biomeSurvey1");
            Check(survey!=null&&PlaceWithinTalkReach(g,survey),
                "Original forest survey remains an actual reachable source");
            g.Talk(survey);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_patrol_begin");
            Check(begin>=0,"Physical survey offers the original combat contract");
            g.Choose(begin);
            Check(g.FieldPatrolActive&&g.FieldPatrolRemaining==2,
                "Two contracted field enemies are actually alive");
            int originalCount=g.enemies.Count;
            WorldObject ridge=Array.Find(g.Map.objects,o=>
                o.kind=="ridgemarker"&&o.id=="ridge_route_1_0");
            Check(ridge!=null&&g.CanStand(new Point(ridge.x,ridge.y)),
                "The remote ridge is still a real physical map destination");
            g.save.x=ridge.x;g.save.y=ridge.y;
            g.Tick(.02f,new Point());
            Check(g.FieldPatrolActive&&!g.RemoteRoadEncounterActive&&
                g.enemies.Count==originalCount&&!g.RemoteRoadCleared(1,0),
                "An already signed patrol cannot receive two unsolicited attackers or a free clear");
        }


        // The player may choose the shipped, fully physical nonlethal snare
        // path instead of lethal combat. Both original enemy references must
        // actually trip separate planted snares and be captured alive before
        // the humane reward can be signed; neither capture is a kill.
        public static void FarRoadPatrolCanBeResolvedWithTwoRealLivingCaptures(Campaign data)
        {
            var g=Fresh(data,0);g.mode="world";g.save.stage=3;
            g.save.room=1;g.enemies.Clear();g.invulnerable=200f;
            g.save.inventory[0]=12;g.save.inventory[4]=12;
            WorldObject ridge=Array.Find(g.Map.objects,o=>
                o.id=="ridge_route_1_0"&&o.kind=="ridgemarker");
            Check(ridge!=null,"Original forest marker exists");
            g.save.x=ridge.x;g.save.y=ridge.y;
            g.Tick(.02f,new Point());
            var raiders=new List<Enemy>(g.enemies);
            Check(g.RemoteRoadEncounterActive&&raiders.Count==2,
                "A full physical outer-road raider pair starts first");
            int kills=g.save.kills[1],coins=g.save.coins,
                crystals=g.save.shards;
            for(int i=0;i<2;i++)
            {
                Point snareFoot=PutRealSnare(g);
                Enemy foe=raiders[i];
                foe.x=snareFoot.x;foe.y=snareFoot.y;
                foe.hitTime=5f;
                for(int step=0;step<11;step++)g.Tick(.05f,new Point());
                Check(g.CanBindSnaredEnemy&&
                    System.Object.ReferenceEquals(g.SnaredBindTarget,foe),
                    "A real placed stake, contact and stun create this living bind window");
                g.save.energy=100f;
                Check(g.BindSnaredEnemy()&&!g.enemies.Contains(foe)&&
                    g.RemoteRoadRemaining==1-i,
                    "Only a paid genuine snare restraint removes its own registered road raider");
            }
            for(int i=0;i<5&&g.RemoteRoadEncounterActive;i++)
                g.Tick(.05f,new Point());
            Check(!g.RemoteRoadEncounterActive&&
                g.RemoteRoadCleared(1,0)&&
                g.save.kills[1]==kills&&
                g.save.coins==coins+21&&
                g.save.shards==crystals+1,
                "Two alive captures sign the one-time humane road reward without any chapter kill");
            g.PrepareSave();
            var saved=new GameCore(data,CloneSave(g.save));
            Check(saved.RemoteRoadCleared(1,0)&&
                saved.save.version==2&&!saved.RemoteRoadEncounterActive,
                "Nonlethal field outcome persists as a completed v2 road without ghost enemies");
        }


        // Mid-ambush saves must not turn its two transient raiders into
        // ordinary persistent killers when RestoreWorld reconstructs a
        // generic EnemyRecord. The legitimate uncompleted crossing can be
        // revisited later on foot, without laundering main-story kills.
        public static void UnfinishedFarRoadSaveNeverRestoresGhostOrdinaryEnemies(Campaign data)
        {
            var g=Fresh(data);g.mode="world";
            g.save.room=1;g.save.stage=3;g.enemies.Clear();
            WorldObject site=Array.Find(g.Map.objects,o=>
                o.kind=="ridgemarker"&&o.id=="ridge_route_1_0");
            Check(site!=null,"Original forest outer-road crossing exists");
            g.save.x=site.x;g.save.y=site.y;
            int oldKills=g.save.kills[1];
            g.Tick(.02f,new Point());
            Check(g.RemoteRoadEncounterActive&&g.RemoteRoadRemaining==2,
                "A real, unfinished two-actor encounter is live before saving");
            g.PrepareSave();
            Check(g.save.snapshotRoom==1&&
                g.save.worldEnemies!=null&&g.save.worldEnemies.Length==0,
                "Both active encounter actors are omitted from the persistent ordinary-world roster");
            var saved=new GameCore(data,CloneSave(g.save));
            Check(saved.save.version==2&&
                !saved.RemoteRoadEncounterActive&&
                !saved.RemoteRoadCleared(1,0)&&
                saved.enemies.Count==0&&saved.save.kills[1]==oldKills,
                "Loading an unfinished ambush neither restores ghost raiders nor grants chapter kills");
            saved.mode="world";
            saved.Tick(.02f,new Point());
            Check(!saved.RemoteRoadEncounterActive&&
                saved.RemoteRoadSafeArrivalActive&&saved.enemies.Count==0,
                "Reloaded unfinished roadside fight leaves a safe saved foot, never resurrects hostile ghosts");
            Point distant=new Point(saved.Map.spawnX,saved.Map.spawnY);
            Check((distant-new Point(site.x,site.y)).Length>10.5f&&
                saved.CanStand(distant),
                "Original reachable spawn lies outside the protected roadside landing");
            saved.save.x=distant.x;saved.save.y=distant.y;
            saved.Tick(.02f,new Point());
            saved.save.x=site.x;saved.save.y=site.y;
            saved.Tick(.02f,new Point());
            Check(saved.RemoteRoadEncounterActive&&
                saved.RemoteRoadRemaining==2&&
                saved.save.kills[1]==oldKills,
                "Physically leaving and returning retries new genuine foes, not stale saved enemy references");
        }


        // Existing maps, actual sight, and the 8-tile enemy perception
        // contract must agree: an announced ambush is playable on screen.

        // Player-facing highlights must be bound to physical enemy
        // references, never to reused serialized IDs or dead opponents.
        public static void RoadsideEnemyCuesBelongOnlyToActualLiveAmbushers(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.room=2;g.enemies.Clear();
            WorldObject site=Array.Find(g.Map.objects,o=>
                o.id=="ridge_route_2_0");
            Check(site!=null,"Existing drowned-road marker is present");
            g.save.x=site.x;g.save.y=site.y;
            g.Tick(.02f,new Point());
            Check(g.RemoteRoadEncounterActive&&g.enemies.Count==2,
                "Actual first encounter begins with two live enemy bodies");
            Enemy first=g.enemies[0],second=g.enemies[1];
            var fake=new Enemy(first.id,first.kind,first.x,first.y,first.hp);
            Check(g.IsRemoteRoadThreat(first)&&g.IsRemoteRoadThreat(second)&&
                !g.IsRemoteRoadThreat(fake),
                "Exact living actor identity, not shared spawn IDs, owns danger labels");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Original actual combat defeat pipeline remains present");
            hit.Invoke(g,new object[]{first,800f,false,null,null});
            Check(!g.IsRemoteRoadThreat(first)&&g.IsRemoteRoadThreat(second),
                "A dead actor loses its label immediately without hiding the surviving foe");
        }


        // The player can actually find and track all 14 optional roads.
        // Live battle routing targets physical actors, not stale markers;
        // completed quest progress is derived from existing v2 receipts.

        // Original safe-travel promise vs. new OPTIONAL battles:
        // authentic two-endpoint teleport cannot spawn compulsory raiders;
        // explicit local dialogue instead creates real attackable bodies.
        public static void LinkedRidgeTransportIsSafeButAllowsVoluntaryRealRoadCombat(Campaign data)
        {
            for(int room=1;room<=7;room++)
            for(int side=0;side<2;side++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=3;g.enemies.Clear();g.save.energy=100f;
                g.save.flags.Add("ridge_seen_"+room+"_0");
                g.save.flags.Add("ridge_seen_"+room+"_1");
                WorldObject from=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_"+side);
                WorldObject end=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_"+(1-side));
                Check(from!=null&&end!=null,"Real ridge pair exists in every wilderness");
                g.save.x=from.x;g.save.y=from.y;
                int money=g.save.coins,kills=g.save.kills[room];
                int energy=g.RidgePassageEnergyCost(room);
                g.Talk(from);
                Check(g.conversation!=null,
                    "Existing transport opens an actual original ridge marker dialogue");
                int crossing=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="ridge_travel");
                Check(crossing>=0,"The original safe crossing remains usable");
                g.Choose(crossing);
                Check(g.mode=="world"&&
                    (g.Position-new Point(end.x,end.y)).Length<.001f&&
                    Math.Abs(g.save.energy-(100-energy))<.01f&&
                    g.RemoteRoadSafeArrivalActive,
                    "Original safe crossing lands at authentic far end for correct energy");
                for(int t=0;t<12;t++)g.Tick(.05f,new Point());
                Check(!g.RemoteRoadEncounterActive&&
                    !g.RemoteRoadCleared(room,1-side)&&
                    g.enemies.Count==0&&g.save.coins==money&&
                    g.save.kills[room]==kills,
                    "Paid safe arrival never starts optional combat or grants fake clearance");
                g.Talk(end);
                Check(g.conversation!=null,
                    "Protected arrival can still speak to the real landing marker");
                int challenge=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="remote_road_challenge");
                Check(challenge>=0,
                    "Uncleared roadside offers voluntary confrontation at existing marker");
                g.Choose(challenge);
                Check(g.RemoteRoadEncounterActive&&g.RemoteRoadRemaining==2&&
                    !g.RemoteRoadSafeArrivalActive&&g.enemies.Count==2&&
                    !g.RemoteRoadCleared(room,1-side),
                    "Voluntary choice spawns exactly two genuine dangerous enemies without reward");
                foreach(Enemy enemy in g.enemies)
                    Check(enemy.hp>0f&&g.CanStand(enemy.Position)&&
                        g.NavigationPathFrom(g.Position,enemy.Position,true).Count>0,
                        "Opt-in squad uses genuine walkable, attackable positions");
                g.PrepareSave();
                Check(g.save.version==2&&
                    !g.RemoteRoadCleared(room,1-side),
                    "Transport immunity and optional fight state never become v2 completion flags");
            }
        }


        // Save-load at the END of a paid ridge crossing is not equivalent
        // to walking back into danger. The same transient courtesy ends
        // after actually leaving the site; legacy v2 slots stay untouched.
        public static void LoadedRidgeCheckpointPreservesArrivalSafetyUntilWalkingBack(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=3;g.enemies.Clear();g.save.energy=100f;
                // Restore from a LEGITIMATELY unlocked v2 region: the
                // loader correctly refuses to enter locked story chapters.
                if(room==5)g.save.flags.Add("exp_intro");
                if(room==6)g.save.flags.Add("harbor_report");
                if(room==7)g.save.flags.Add("desert_report");
                g.save.flags.Add("ridge_seen_"+room+"_0");
                g.save.flags.Add("ridge_seen_"+room+"_1");
                WorldObject start=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_0");
                WorldObject destination=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_1");
                Check(start!=null&&destination!=null,
                    "Both real original ridge ends are present");
                g.save.x=start.x;g.save.y=start.y;
                g.Talk(start);
                Check(g.conversation!=null,
                    "Real original ridge marker supports existing travel");
                int travel=Array.FindIndex(g.conversation.choices,
                    o=>o.action=="ridge_travel");
                Check(travel>=0,"Original passage is unlocked");
                g.Choose(travel);
                Check(g.RemoteRoadSafeArrivalActive&&
                    (g.Position-new Point(destination.x,destination.y)).Length<.01f,
                    "Paid crossing established the actual safe arrival");
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                loaded.Start();loaded.enemies.Clear();
                Check(loaded.save.version==2&&loaded.save.room==room&&
                    loaded.RemoteRoadSafeArrivalActive,
                    "Unlocked version-two recovery recognizes exact saved marker without new flags");
                for(int i=0;i<7;i++)loaded.Tick(.05f,new Point());
                Check(!loaded.RemoteRoadEncounterActive&&
                    !loaded.RemoteRoadCleared(room,1)&&
                    loaded.enemies.Count==0,
                    "Resuming safe transport cannot create an unannounced checkpoint ambush");
                Point depart=new Point(loaded.Map.spawnX,loaded.Map.spawnY);
                Check(loaded.CanStand(depart)&&
                    (depart-new Point(destination.x,destination.y)).Length>10.5f,
                    "Actual original spawn is outside the protected far-road footprint");
                loaded.save.x=depart.x;loaded.save.y=depart.y;
                loaded.Tick(.02f,new Point());
                Check(!loaded.RemoteRoadSafeArrivalActive,
                    "Stepping outside the genuine arrival area rearms exploration");
                loaded.save.x=destination.x;loaded.save.y=destination.y;
                loaded.Tick(.02f,new Point());
                Check(loaded.RemoteRoadEncounterActive&&
                    loaded.RemoteRoadRemaining==2,
                    "Walking back to the EXACT uncleared crossing opens two real attackers");
            }
        }


        // A voluntary fight must also have a real voluntary exit.
        // Retreat at the original road marker cleans exactly its real
        // owned actors and prevents two-second immediate re-ambush.
        public static void PhysicalRoadMarkerCanWithdrawOptionalBattleWithoutFarming(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.stage=3;
                g.save.room=room;g.enemies.Clear();g.save.energy=100f;
                WorldObject marker=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_0");
                Check(marker!=null,"Original terrain marker exists for voluntary exit");
                g.save.x=marker.x;g.save.y=marker.y;
                g.Tick(.02f,new Point());
                Check(g.RemoteRoadEncounterActive&&
                    g.RemoteRoadRemaining==2&&g.enemies.Count==2,
                    "The authentic two-body road encounter is active");
                var owned=new List<Enemy>(g.enemies);
                int coins=g.save.coins,shards=g.save.shards;
                int kills=g.save.kills[room],moments=g.save.moments.Count;
                g.Talk(marker);
                Check(g.conversation!=null,
                    "A real physically reachable marker can speak during the optional fight");
                int withdraw=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="remote_road_withdraw");
                Check(withdraw>=0,
                    "The actual original road marker offers a deliberate no-reward retreat");
                g.Choose(withdraw);
                Check(!g.RemoteRoadEncounterActive&&g.enemies.Count==0&&
                    !g.enemies.Contains(owned[0])&&!g.enemies.Contains(owned[1])&&
                    g.RemoteRoadSafeArrivalActive&&
                    g.save.coins==coins&&g.save.shards==shards&&
                    g.save.kills[room]==kills&&
                    g.save.moments.Count==moments&&
                    !g.RemoteRoadCleared(room,0),
                    "Real withdrawal removes both owned enemies, credits nothing, protects same-foot exit");
                for(int i=0;i<60;i++)g.Tick(.05f,new Point());
                Check(!g.RemoteRoadEncounterActive&&g.enemies.Count==0,
                    "Standing on the surrender marker cannot retrigger a surprise battle");
                Point spawn=new Point(g.Map.spawnX,g.Map.spawnY);
                Check(g.CanStand(spawn)&&
                    (spawn-new Point(marker.x,marker.y)).Length>10.5f,
                    "Actual regional spawn is outside the cleared transit corridor");
                g.save.x=spawn.x;g.save.y=spawn.y;
                g.Tick(.05f,new Point());
                Check(!g.RemoteRoadSafeArrivalActive,
                    "Leaving the actual local crossing rearms only optional combat");
                g.save.x=marker.x;g.save.y=marker.y;
                g.Tick(.05f,new Point());
                Check(g.RemoteRoadEncounterActive&&g.RemoteRoadRemaining==2&&
                    !g.RemoteRoadCleared(room,0),
                    "Revisiting the original road restores a real, replayable two-foe challenge");
            }
        }


        // Journal continuation must not pull the player away from the
        // exact uncleared landing they JUST PAID to reach. In-progress
        // battle still overrides the marker with actual living enemies.
        public static void RoadJournalOffersActionableChallengeAtSafeArrival(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=3;g.enemies.Clear();g.save.energy=100f;
                g.save.flags.Add("ridge_seen_"+room+"_0");
                g.save.flags.Add("ridge_seen_"+room+"_1");
                WorldObject from=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_0");
                WorldObject destination=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_1");
                g.save.x=from.x;g.save.y=from.y;
                g.Talk(from);
                g.Choose(Array.FindIndex(g.conversation.choices,
                    a=>a.action=="ridge_travel"));
                Check(g.RemoteRoadProtectedDistrict==1&&
                    !g.RemoteRoadCleared(room,1),
                    "The actual paid arrival protects its unfinished physical branch");
                QuestGuide guide=g.GetQuest("remote_roads");
                Check(guide.room==room&&
                    (guide.target-new Point(destination.x,destination.y)).Length<.001f&&
                    guide.next.Contains(g.L("主动挑战","challenge")),
                    "Journal now prefers exact actionable protected landing to unrelated unfinished site");
                g.Talk(destination);
                int begin=Array.FindIndex(g.conversation.choices,
                    o=>o.action=="remote_road_challenge");
                Check(begin>=0,
                    "Roadside dialogue offers the original grounded combat choice");
                g.Choose(begin);
                guide=g.GetQuest("remote_roads");
                Check(g.RemoteRoadEncounterActive&&
                    (guide.target-g.RemoteRoadGuideTarget).Length<.001f&&
                    guide.next.Contains(g.L("原路标","original marker")),
                    "Active battle immediately changes objective to real foes and explains physical withdrawal");
            }
        }

        public static void FourteenRoadAmbushesHaveUsableJournalGuidance(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.room=1;
            g.save.stage=3;g.enemies.Clear();
            QuestGuide fresh=g.GetQuest("remote_roads");
            Check(!fresh.locked&&!fresh.complete&&fresh.steps.Count==14&&
                fresh.Progress==0f,
                "The world journal exposes all fourteen genuine optional road sites");
            WorldObject first=Array.Find(g.Map.objects,o=>
                o.id=="ridge_route_1_0");
            Check(first!=null,"The first journal marker is an original map object");
            g.TrackQuest("remote_roads");
            Check(g.save.trackedQuest=="remote_roads",
                "Player may deliberately track road clearances in the original journal");
            g.save.x=first.x;g.save.y=first.y;
            g.Tick(.02f,new Point());
            QuestGuide engaged=g.GetQuest("remote_roads");
            Check(g.RemoteRoadEncounterActive&&engaged.room==1&&
                engaged.steps.Count==14&&
                (engaged.target-g.RemoteRoadGuideTarget).Length<.001f,
                "Active journal compass uses the nearest actual living ambusher");
            for(int room=1;room<=7;room++)
            for(int district=0;district<2;district++)
                g.save.flags.Add("remote_road_clear_"+room+"_"+district);
            QuestGuide completed=g.GetQuest("remote_roads");
            Check(completed.complete&&completed.steps.Count==14&&
                completed.Progress==1f&&
                g.save.trackedQuest=="remote_roads",
                "All fourteen authentic signed routes complete the original selected journal quest");
            g.PrepareSave();
            var saved=new GameCore(data,CloneSave(g.save));
            Check(saved.save.version==2&&
                saved.GetQuest("remote_roads").complete&&
                saved.save.trackedQuest=="remote_roads",
                "Legacy v2 round trip preserves optional road progress and the explicit pin");
        }


        // A safer physical road must actually make the EXISTING ridge
        // passage cheaper, using the same shared calculator as its menu and
        // travel transaction. An old v2 save with no flags pays old prices.
        public static void RoadClearancePermanentlyReducesActualRidgeTravelCost(Campaign data)
        {
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";
                g.save.stage=3;g.save.room=room;g.enemies.Clear();
                int original=g.RidgePassageEnergyCost(room);
                Check(original==12,"Legacy unpaid ridge journey still costs twelve energy");
                g.save.flags.Add("remote_road_clear_"+room+"_0");
                Check(g.RidgePassageEnergyCost(room)==10,
                    "One cleared authentic crossing reduces actual travel stamina by two");
                g.save.flags.Add("remote_road_clear_"+room+"_1");
                Check(g.RidgePassageEnergyCost(room)==8,
                    "Both crossings secured reduce the existing ridge journey to eight");
                g.save.flags.Add("unlisted_public");
                Check(g.RidgePassageEnergyCost(room)==2,
                    "Stacked civilian route costs never become free or negative");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.RidgePassageEnergyCost(room)==2,
                    "Cumulative road security discounts survive original v2 serialization");
            }
            var untouched=Fresh(data);
            Check(untouched.RidgePassageEnergyCost(0)==12,
                "Canal town crossing cost is not mistakenly changed by seven outer-road rewards");
        }

        // On deliberately open, unobstructed battle ground, archers must
        // take a readable *ranged* lane rather than blindly spawning at the
        // first melee-distance tile. Heavy infantry advances from closer.
        // This exercises actual BeginRemoteRoadEncounter with its original
        // enemy roles and 4-way map collision, not a pure scoring helper.
        public static void RoadwatchSquadsSelectRoleAwarePhysicalApproaches(Campaign data)
        {
            for(int region=2;region<=3;region++)
            {
                MapData old=data.maps[region];
                try
                {
                    var rows=new string[55];
                    for(int y=0;y<55;y++)
                    {
                        char[] tiles=new string('.',55).ToCharArray();
                        for(int x=0;x<55;x++)
                            if(x<2||x>=53||y<2||y>=53)tiles[x]='#';
                        rows[y]=new string(tiles);
                    }
                    WorldObject marker=new WorldObject{
                        id="ridge_route_"+region+"_0",kind="ridgemarker",
                        name="Open road",en="Open road",x=27.5f,y=27.5f};
                    data.maps[region]=new MapData{
                        id=region,spawnX=27.5f,spawnY=27.5f,
                        rows=rows,objects=new[]{marker},
                        decor=new Decoration[0]};
                    GameCore game=Fresh(data);game.mode="world";
                    game.save.stage=3;game.save.room=region;
                    game.save.x=marker.x;game.save.y=marker.y;
                    game.enemies.Clear();
                    game.Tick(.02f,new Point());
                    Check(game.RemoteRoadEncounterActive&&game.enemies.Count==2,
                        "Role-aware formation must commit a real complete two-actor wave");
                    Enemy first=game.enemies[0],other=game.enemies[1];
                    float distance=(first.Position-game.Position).Length;
                    Check(region==2?first.kind==1&&distance>=6.5f:
                        first.kind==2&&distance<6.5f,
                        "A longbow holds a true 6-7 tile lane while a plated guard closes in");
                    Point a=(first.Position-game.Position).Normalized;
                    Point b=(other.Position-game.Position).Normalized;
                    Check(a.x*b.x+a.y*b.y<-.2f&&
                        game.CanStand(first.Position)&&game.CanStand(other.Position)&&
                        game.NavigationPathFrom(game.Position,first.Position,true).Count>0&&
                        game.NavigationPathFrom(game.Position,other.Position,true).Count>0&&
                        game.RemoteRoadSignalThreatCount(marker)>0,
                        "Both flanks stay on distinct reachable real ground with a visible ridge counter");
                }
                finally{data.maps[region]=old;}
            }
        }

        // On open authentic walkable terrain, the visible first raider
        // and its companion must NOT stack into an identical approach.
        // This runs the original remote-road event transaction and exact
        // combat body creation, not a coordinate-only helper.
        public static void RemoteRoadAmbushChoosesActualOpposingFlankWhenOpen(Campaign data)
        {
            MapData saved=data.maps[1];
            try
            {
                var rows=new string[55];
                for(int y=0;y<55;y++)
                {
                    char[] line=new string('.',55).ToCharArray();
                    for(int x=0;x<55;x++)
                        if(x<2||x>=53||y<2||y>=53)line[x]='#';
                    rows[y]=new string(line);
                }
                WorldObject marker=new WorldObject{
                    id="ridge_route_1_0",kind="ridgemarker",
                    name="Open Crossing",en="Open Crossing",
                    x=27.5f,y=27.5f};
                data.maps[1]=new MapData{
                    id=1,spawnX=27.5f,spawnY=27.5f,
                    rows=rows,objects=new[]{marker},
                    decor=new Decoration[0]};
                var g=Fresh(data);g.mode="world";g.save.room=1;
                g.save.stage=3;g.save.x=marker.x;g.save.y=marker.y;
                g.enemies.Clear();
                g.Tick(.02f,new Point());
                Check(g.RemoteRoadEncounterActive&&g.enemies.Count==2,
                    "Original real crossing spawns both visible physical foes");
                Point a=(g.enemies[0].Position-g.Position).Normalized;
                Point b=(g.enemies[1].Position-g.Position).Normalized;
                Check(a.x*b.x+a.y*b.y<-.2f,
                    "Second live raider comes from an opposing sector, not the same warning lane");
                foreach(Enemy e in g.enemies)
                    Check(g.CanStand(e.Position)&&
                        g.NavigationPathFrom(g.Position,e.Position,true).Count>0,
                        "Opposing raiders have complete original walking and contact routes");
            }
            finally {data.maps[1]=saved;}
        }

        public static void RemoteRoadOpeningAmbushesEnterPhysicalAwarenessRange(Campaign data)
        {
            var sight=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(sight!=null,"Original terrain line of sight remains wired");
            for(int room=1;room<=7;room++)
            for(int district=0;district<2;district++)
            {
                var g=Fresh(data);g.mode="world";g.save.stage=3;
                g.save.room=room;g.enemies.Clear();
                WorldObject marker=Array.Find(g.Map.objects,o=>
                    o.id=="ridge_route_"+room+"_"+district);
                Check(marker!=null,"Both original ridge crossings exist");
                g.save.x=marker.x;g.save.y=marker.y;
                g.Tick(.02f,new Point());
                Check(g.RemoteRoadEncounterActive&&g.enemies.Count==2,
                    "Every authentic crossing opens a full local ambush");
                Enemy first=g.enemies[0],second=g.enemies[1];
                foreach(Enemy foe in new[]{first,second})
                    Check((foe.Position-g.Position).Length>=4.99f&&
                        (foe.Position-g.Position).Length<=8.01f&&
                        g.NavigationPathFrom(g.Position,foe.Position,true).Count>0,
                        "Real foes enter ordinary perception range on genuinely traversable ground");
                Check((bool)sight.Invoke(g,new object[]{g.Position,first.Position}),
                    "At least the first ambusher has an unobstructed actual sightline");
                Check((first.Position-second.Position).Length>=4.99f,
                    "The two threats use distinct authentic flank approaches");
            }
        }

        public static void RemoteRoadRetreatReleasesOriginalActorsAndAllowsRetry(Campaign data)
        {
            var g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.room=1;g.enemies.Clear();g.invulnerable=200f;
            WorldObject marker=Array.Find(g.Map.objects,o=>
                o.id=="ridge_route_1_0");
            Check(marker!=null,"Original forest outer crossing remains present");
            g.save.x=marker.x;g.save.y=marker.y;
            int kills=g.save.kills[1],coins=g.save.coins;
            g.Tick(.02f,new Point());
            var original=new List<Enemy>(g.enemies);
            Check(g.RemoteRoadEncounterActive&&original.Count==2,
                "Original two live raiders physically appear on site");
            g.save.x=g.Map.spawnX;g.save.y=g.Map.spawnY;
            for(int i=0;i<35;i++)g.Tick(.05f,new Point());
            Check(!g.RemoteRoadEncounterActive&&
                !g.RemoteRoadCleared(1,0)&&
                g.save.kills[1]==kills&&g.save.coins==coins&&
                !g.enemies.Contains(original[0])&&
                !g.enemies.Contains(original[1]),
                "Retreat retires exact raiders without farming money or main kills");
            g.save.x=marker.x;g.save.y=marker.y;
            for(int i=0;i<50&&!g.RemoteRoadEncounterActive;i++)
                g.Tick(.05f,new Point());
            Check(g.RemoteRoadEncounterActive&&g.RemoteRoadRemaining==2&&
                g.enemies.Count==2&&
                !System.Object.ReferenceEquals(g.enemies[0],original[0]),
                "Reentry creates genuine new enemies after world-time rearm");
        }

        public static void FarRoadDistrictsHavePlayableOptionalAmbushes(Campaign data)
        {
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var hit=typeof(GameCore).GetMethod("Hit",flags);
            Check(hit!=null,"Real HP/death drives remote clearing");
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);
                g.mode="world";g.save.stage=3;g.save.room=room;
                g.enemies.Clear();
                int previousKills=g.save.kills[room];
                for(int district=0;district<2;district++)
                {
                    WorldObject post=Array.Find(g.Map.objects,o=>
                        o.kind=="ridgemarker"&&
                        o.id=="ridge_route_"+room+"_"+district);
                    Check(post!=null&&g.CanStand(new Point(post.x,post.y)),
                        "Each outer crossing has an existing reachable landmark");
                    g.save.x=post.x;g.save.y=post.y;
                    int oldMoney=g.save.coins;
                    int oldMoments=g.save.moments.Count;
                    var before=new HashSet<Enemy>(g.enemies);
                    g.Tick(.02f,new Point());
                    var foes=g.enemies.FindAll(e=>!before.Contains(e));
                    Check(g.RemoteRoadEncounterActive&&
                        g.RemoteRoadRemaining==2&&foes.Count==2,
                        "Two full real foes awaken only at the crossed remote landmark");
                    foreach(Enemy e in foes)
                    {
                        Check(e.hp>0f&&!e.boss&&g.CanStand(e.Position)&&
                            g.NavigationPathFrom(g.Position,e.Position,true).Count>0,
                            "Remote combat actors have walkable hero-side routes, not phantom foes");
                        foreach(WorldObject obj in g.Map.objects)
                            Check((e.Position-new Point(obj.x,obj.y)).Length>=4.4f,
                                "The new ambush does not stand on an interactive story site");
                        hit.Invoke(g,new object[]{e,1000f,false,null,null});
                    }
                    for(int i=0;i<16&&g.RemoteRoadEncounterActive;i++)
                        g.Tick(.05f,new Point());
                    Check(!g.RemoteRoadEncounterActive&&
                        g.RemoteRoadCleared(room,district)&&
                        g.save.kills[room]==previousKills&&
                        g.save.coins==oldMoney+18+room*3&&
                        g.save.moments.Count==oldMoments+1,
                        "Only two actual combat deaths sign the reward; main kills remain untouched");
                    g.Tick(.02f,new Point());
                    Check(!g.RemoteRoadEncounterActive,
                        "Completed crossing never spawns another farming wave");
                }
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.RemoteRoadCleared(room,0)&&
                    restored.RemoteRoadCleared(room,1)&&
                    !restored.RemoteRoadEncounterActive,
                    "Both earned remote battle receipts persist in original v2 saves without actor ghosts");
            }
        }

        // A resolved civilian crossing stays safe forever, yet the real
        // post may host a fully voluntary, harder battle and a one-time
        // save-v2 receipt. This calls the ORIGINAL ridge dialogue, original
        // actor HP/death pipeline, and the real world-time settlement tick.
        public static void FourteenSafeRoadsOfferRealVolunteerEliteWatches(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Veteran watch must use actual enemy deaths");
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);g.save.stage=3;
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.save.room=room;g.mode="world";
                g.enemies.Clear();
                g.save.flags.Add("ridge_seen_"+room+"_0");
                g.save.flags.Add("ridge_seen_"+room+"_1");
                for(int side=0;side<2;side++)
                {
                    WorldObject marker=Array.Find(g.Map.objects,o=>o.kind=="ridgemarker"&&
                        o.id=="ridge_route_"+room+"_"+side);
                    Check(marker!=null&&PlaceWithinTalkReach(g,marker),
                        "Every real roadwatch uses a reachable original ridge source");
                    g.enemies.Clear();
                    g.save.flags.Add("remote_road_clear_"+room+"_"+side);
                    int fare=g.RidgePassageEnergyCost(room);
                    g.Tick(.02f,new Point());
                    Check(!g.RemoteRoadEncounterActive&&
                        g.RemoteRoadCleared(room,side),
                        "Cleared civilian roads never auto-spawn elite revenge waves");
                    int prior=g.save.coins, xp=g.save.xp, level=g.save.level;
                    g.Talk(marker);
                    int begin=Array.FindIndex(g.conversation.choices,
                        choice=>choice.action=="remote_road_veteran");
                    Check(begin>=0,
                        "Secure landmark offers an explicit elite challenge");
                    g.Choose(begin);
                    Check(g.RemoteRoadVeteranActive&&
                        g.RemoteRoadRemaining==2&&
                        g.enemies.Count==2&&
                        g.RecommendedContinuationGuide.id=="road_veterans"&&
                        (g.NavigationTarget()-g.RemoteRoadGuideTarget).Value.Length<.001f,
                        "Voluntary watch spawns two real elites and owns the live HUD/compass without overwriting manual tracking");
                    Enemy[] foes=g.enemies.ToArray();
                    foreach(Enemy foe in foes)
                    {
                        Check(g.IsRemoteRoadThreat(foe)&&foe.hp>=84f&&
                            !foe.boss&&g.CanStand(foe.Position)&&
                            g.NavigationPathFrom(g.Position,foe.Position,true).Count>0,
                            "Veteran actors have real fightable feet, stronger HP and identity");
                        hit.Invoke(g,new object[]{foe,2000f,false,null,null});
                    }
                    for(int frame=0;frame<20&&g.RemoteRoadEncounterActive;frame++)
                        g.Tick(.05f,new Point());
                    Check(!g.RemoteRoadEncounterActive&&
                        g.RemoteRoadVeteranDone(room,side)&&
                        g.RemoteRoadCleared(room,side)&&
                        g.save.coins==prior+28+room*4&&
                        (g.save.level>level||
                         g.save.level==level&&g.save.xp>xp)&&
                        g.RidgePassageEnergyCost(room)==fare,
                        "First true elite kills sign a separate receipt without revoking safe travel."+
                        " room="+room+" side="+side+
                        " active="+g.RemoteRoadEncounterActive+
                        " veteran="+g.RemoteRoadVeteranDone(room,side)+
                        " cleared="+g.RemoteRoadCleared(room,side)+
                        " coins="+g.save.coins+" expected="+(prior+28+room*4)+
                        " level="+g.save.level+" initial="+level+
                        " xp="+g.save.xp+" initial="+xp+
                        " fare="+g.RidgePassageEnergyCost(room)+" initial="+fare);
                    Check(g.GetQuest("road_veterans").steps[(room-1)*2+side].done,
                        "New veteran journal follows the real signed source");
                    // Revisit the same authentic marker. It remains
                    // playable but must never mint an additional reward.
                    Check(PlaceWithinTalkReach(g,marker),
                        "Physical source remains available after first victory");
                    if(side==0)
                    {
                        int coins=g.save.coins, xpAgain=g.save.xp;
                        g.Talk(marker);
                        int replay=Array.FindIndex(g.conversation.choices,
                            choice=>choice.action=="remote_road_veteran");
                        Check(replay>=0,"Repeatable watch remains a voluntary player choice");
                        g.Choose(replay);
                        Check(g.RemoteRoadVeteranActive&&g.RemoteRoadRemaining==2,
                            "Replay uses genuinely new elite bodies");
                        foreach(Enemy foe in g.enemies.ToArray())
                            hit.Invoke(g,new object[]{foe,2000f,false,null,null});
                        for(int tick=0;tick<20&&g.RemoteRoadEncounterActive;tick++)
                            g.Tick(.05f,new Point());
                        Check(!g.RemoteRoadEncounterActive&&
                            g.save.coins==coins&&g.save.xp==xpAgain,
                            "Repeat fight pays no additional rewards or progress");
                    }
                }
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.RemoteRoadVeteranDone(room,0)&&
                    restored.RemoteRoadVeteranDone(room,1)&&
                    restored.RemoteRoadCleared(room,0)&&
                    restored.RemoteRoadCleared(room,1)&&
                    !restored.RemoteRoadEncounterActive,
                    "Two veteran receipts survive old v2 slots; enemy actors do not");
            }
        }

        public static void VeteranRoadwatchFourteenReceiptsGrantExactlyOneTalent(Campaign data)
        {
            GameCore g=Fresh(data);g.save.stage=3;g.save.room=1;
            // At the level cap the physical victory still awards the
            // one-time mastery talent, but ordinary combat XP cannot also
            // level up and add an unrelated skill point in this assertion.
            g.save.level=30;g.save.xp=0;
            g.mode="world";g.enemies.Clear();
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            for(int room=1;room<=7;room++)
            for(int side=0;side<2;side++)
            {
                g.save.flags.Add("remote_road_clear_"+room+"_"+side);
                if(room!=1||side!=0)
                    g.save.flags.Add("remote_road_veteran_"+room+"_"+side);
            }
            Check(g.RemoteRoadVeteranWins==13&&
                g.GetQuest("road_veterans").Progress> .92f&&
                !g.GetQuest("road_veterans").complete,
                "Thirteen signed elite watches cannot award final mastery");
            WorldObject post=Array.Find(g.Map.objects,o=>
                o.kind=="ridgemarker"&&o.id=="ridge_route_1_0");
            Check(post!=null&&PlaceWithinTalkReach(g,post),
                "Final mastery must be earned from an authentic physical site");
            int before=g.save.skillPoints;
            g.Talk(post);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="remote_road_veteran");
            Check(begin>=0,"The last elite watch is still a voluntary choice");
            g.Choose(begin);
            Check(g.RemoteRoadVeteranActive&&g.RemoteRoadRemaining==2,
                "The last two foes really enter the physical scene");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            foreach(Enemy foe in g.enemies.ToArray())
                hit.Invoke(g,new object[]{foe,2000f,false,null,null});
            for(int i=0;i<20&&g.RemoteRoadEncounterActive;i++)
                g.Tick(.05f,new Point());
            Check(g.RemoteRoadVeteranWins==14&&
                g.Has("remote_road_veteran_mastered")&&
                g.save.skillPoints==before+1&&
                g.GetQuest("road_veterans").complete,
                "Only fourteen real receipts sign the permanent one-point mastery"+
                " wins="+g.RemoteRoadVeteranWins+
                " mastered="+g.Has("remote_road_veteran_mastered")+
                " before="+before+" after="+g.save.skillPoints+
                " quest="+g.GetQuest("road_veterans").complete);
            int skill=g.save.skillPoints;
            g.PrepareSave();
            GameCore loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&
                loaded.RemoteRoadVeteranWins==14&&
                loaded.GetQuest("road_veterans").complete&&
                loaded.save.skillPoints==skill,
                "Original V2 save keeps the completed elite chapter and exactly one skill reward");
        }

        // Verify the new spawn path itself, not just the declared
        // region-to-enemy type table. Compare its actual first combat foot
        // to the first legacy compass ray that the same live clearance would
        // accept. Ranged sightlines and flanks must be selected from real
        // terrain while retaining the old exact contract rewards/count.
        public static void SevenRegionalContractsRankRealTacticalApproachPads(Campaign data)
        {
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var footCheck=typeof(GameCore).GetMethod(
                "RegionalScoutFootClear",flags);
            var scoreMethod=typeof(GameCore).GetMethod(
                "RegionalScoutTacticalPadScore",flags);
            Check(footCheck!=null&&scoreMethod!=null,
                "The live encounter has the real source/terrain scoring path");
            int[,] bearings={
                {1,0},{0,1},{-1,0},{0,-1},
                {1,1},{-1,1},{-1,-1},{1,-1}
            };
            int[] firstKinds={0,1,2,2,0,0,1};
            int improved=0,seenShots=0;
            for(int room=1;room<=7;room++)
            {
                var g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=3;g.enemies.Clear();
                g.save.flags.Add("biome_field_done_"+room);
                WorldObject marker=Array.Find(g.Map.objects,o=>
                    o.id=="biomeSurvey"+room&&o.kind=="biomeSurvey");
                Check(marker!=null&&PlaceWithinTalkReach(g,marker),
                    "Every original live field post has a genuine E contact");
                Point source=new Point(marker.x,marker.y);
                Point? legacy=null;
                for(int radius=7;radius<=12&&!legacy.HasValue;radius++)
                for(int shift=0;shift<8&&!legacy.HasValue;shift++)
                {
                    int direction=(room*2+shift)%8;
                    Point p=new Point(
                        (float)Math.Floor(source.x)+.5f+
                            bearings[direction,0]*radius,
                        (float)Math.Floor(source.y)+.5f+
                            bearings[direction,1]*radius);
                    bool ok=(bool)footCheck.Invoke(g,new object[]{
                        p,source,true,new List<Point>()});
                    if(ok)legacy=p;
                }
                g.Talk(marker);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_begin");
                Check(begin>=0,"Actual field contract must remain signed");
                g.Choose(begin);
                int expected=(room==2||room==3||room==6)?3:2;
                Check(g.FieldPatrolActive&&g.enemies.Count==expected,
                    "Every signed region patrol must spawn its entire authored squad");
                Enemy first=g.enemies[0];
                Check(first.kind==firstKinds[room-1]&&
                    g.CanStand(first.Position)&&
                    g.NavigationPathFrom(g.Position,first.Position,true).Count>0,
                    "First role occupies legal hero-reachable terrain");
                float selectedScore=(float)scoreMethod.Invoke(g,
                    new object[]{first.Position,source,first.kind,
                        new List<Point>(),0});
                if(legacy.HasValue)
                {
                    float naiveScore=(float)scoreMethod.Invoke(g,
                        new object[]{legacy.Value,source,first.kind,
                            new List<Point>(),0});
                    Check(selectedScore>=naiveScore-.02f,
                        "Real tactical first foot beats the legacy first-free compass ray: "+room);
                    if(selectedScore>naiveScore+.2f)improved++;
                }
                int[,] assignedKinds={
                    {0,0,0},{1,1,0},{2,0,2},{2,1,0},
                    {0,1,0},{0,2,1},{1,2,0}
                };
                var actorFeet=new List<Point>();
                for(int slot=0;slot<g.enemies.Count;slot++)
                {
                    Enemy foe=g.enemies[slot];
                    Check(foe.kind==assignedKinds[room-1,slot],
                        "Regional tactical roles must remain in their authored squad slots");
                    Check(g.CanStand(foe.Position)&&foe.hp>0f&&
                        g.NavigationPathFrom(g.Position,foe.Position,true).Count>0,
                        "All assigned real enemy roles stay within the local combat corridor");
                    foreach(Point earlier in actorFeet)
                        Check((foe.Position-earlier).Length>=5f,
                            "A tactical squad cannot stack its physical attacker bodies");
                    actorFeet.Add(foe.Position);
                    if(foe.kind==1)seenShots++;
                }
                Check(g.save.version==2&&!g.FieldPatrolDone(room),
                    "A tactical pad selection never signs a fake first-clear receipt");
            }
            Check(improved>=2&&seenShots>=5,
                "Across seven authentic regional contracts the ranking improves multiple attack approaches");
        }

        public static void OpeningBattleRangedSlotsTrulyPreferVisibleShotsOverWallFire(Campaign data)
        {
            MapData original=data.maps[1];
            try
            {
                var rows=new string[32];
                for(int y=0;y<32;y++)
                {
                    var line=new string('.',40).ToCharArray();
                    for(int x=0;x<40;x++)
                        if(x<2||x>=38||y<2||y>=30)line[x]='#';
                    rows[y]=new string(line);
                }
                char[] wall=rows[15].ToCharArray();
                wall[17]='#';
                rows[15]=new string(wall);
                data.maps[1]=new MapData{
                    id=1,spawnX=20.5f,spawnY=15.5f,
                    rows=rows,objects=new WorldObject[0],
                    decor=new Decoration[0]};
                var g=Fresh(data);
                g.mode="world";g.save.room=1;g.save.stage=3;
                g.save.x=20.5f;g.save.y=15.5f;
                g.enemies.Clear();
                var score=typeof(GameCore).GetMethod(
                    "RegionalOpeningPadScore",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(score!=null,
                    "Production opening encounters rank real role-aware feet");
                Point clear=new Point(26.5f,15.5f),
                    blocked=new Point(14.5f,15.5f),
                    anchor=g.Position;
                Check(g.CanStand(clear)&&g.CanStand(blocked)&&
                    g.NavigationPathFrom(g.Position,blocked,true).Count>0,
                    "Both candidates are physically reachable but only one has a straight firing line");
                float visible=(float)score.Invoke(g,
                    new object[]{clear,anchor,1});
                float throughWall=(float)score.Invoke(g,
                    new object[]{blocked,anchor,1});
                Check(visible>throughWall+20f,
                    "A ranged enemy must strongly prefer an actual shot over shooting through solid cover");
                float meleeVisible=(float)score.Invoke(g,
                    new object[]{clear,anchor,0});
                float meleeBlocked=(float)score.Invoke(g,
                    new object[]{blocked,anchor,0});
                Check(visible-throughWall>
                    meleeVisible-meleeBlocked+20f,
                    "Ranged sightlines matter more than melee sightlines at otherwise equal positions");
                var spawn=typeof(GameCore).GetMethod(
                    "SpawnRegionalBaselineEnemies",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(spawn!=null,"Original mandatory roster spawner is still the real entry point");
                spawn.Invoke(g,new object[]{6,100f});
                Check(g.enemies.Count==6&&
                    g.enemies.TrueForAll(e=>e.hp>0f&&
                        g.CanStand(e.Position)&&
                        g.NavigationPathFrom(g.Position,e.Position,true).Count>0),
                    "Role-ranked opening still stages a full living six-actor squad on one physical component");
            }
            finally{data.maps[1]=original;}
        }

        public static void SevenFieldPatrolRegionsHaveDifferentPhysicalCombatRoles(Campaign data)
        {
            int[][] expected={
                new[]{0,0}, new[]{1,1,0}, new[]{2,0,2},
                new[]{2,1}, new[]{0,1}, new[]{0,2,1},
                new[]{1,2}
            };
            var signatures=new HashSet<string>();
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);
                g.save.room=room;g.mode="world";g.enemies.Clear();
                g.save.flags.Add("biome_field_done_"+room);
                WorldObject post=Array.Find(g.Map.objects,o=>
                    o.kind=="biomeSurvey"&&o.id=="biomeSurvey"+room);
                Check(post!=null&&PlaceWithinTalkReach(g,post),
                    "Every district retains its physically accessible survey post");
                Point hero=g.Position;
                g.Talk(post);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "The live regional post actually opens the contract");
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_begin");
                Check(begin>=0,"Every original field contract is playable");
                g.Choose(begin);
                Check(g.FieldPatrolActive&&
                    g.FieldPatrolRemaining==expected[room-1].Length&&
                    g.enemies.Count==expected[room-1].Length,
                    "The complete authored tactical wave enters the live world");
                string signature="";
                for(int slot=0;slot<g.enemies.Count;slot++)
                {
                    Enemy e=g.enemies[slot];
                    Check(e.kind==expected[room-1][slot]&&
                        !e.boss&&e.hp>0f&&g.CanStand(e.Position)&&
                        g.NavigationPathFrom(hero,e.Position,true).Count>0,
                        "A district's actual combat role must occupy walkable connected ground");
                    signature+=e.kind.ToString();
                }
                Check(signatures.Add(signature),
                    "All seven contracts must present distinct tactical role sequences");
                Check(g.save.version==2&&!g.FieldPatrolDone(room),
                    "Starting optional combat does not alter legacy saves or sign rewards");
            }
        }

        public static void ThreeOriginalFarshoreHuntsHaveRealTwoWaveCombatAndMoralOutcomes(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Live combat HP/death owns farshore hunt victories");
            int[,] expected={{1,0,2,0},{2,1,1,2},{0,2,2,1}};
            var coordinates=new HashSet<string>();
            for(int room=5;room<=7;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
                g.save.room=room;g.enemies.Clear();g.invulnerable=200f;
                // Live farshore progression, not an impossible save with
                // privileged room coordinates but missing portal credentials.
                g.save.flags.Add("exp_intro");
                if(room>=6)g.save.flags.Add("harbor_report");
                if(room>=7)g.save.flags.Add("desert_report");
                WorldObject source=Array.Find(g.Map.objects,o=>
                    o.id=="wilderness_hunt_"+room);
                Check(source!=null&&source.kind=="wildernessHunt"&&
                    coordinates.Add(source.x+","+source.y),
                    "Each farshore map has its own newly authored physical hunt site");
                Check(g.CanStand(new Point(source.x,source.y))&&
                    g.NavigationPathFrom(new Point(g.Map.spawnX,g.Map.spawnY),
                        new Point(source.x,source.y),true).Count>0,
                    "Original playable map actually connects its distant hunt clearing");
                foreach(WorldObject old in g.Map.objects)
                    if(old!=source)
                        Check((new Point(old.x-source.x,old.y-source.y)).Length>=10f,
                            "Hunts occupy remote unused level ground, not existing quest kiosks");
                Check(PlaceWithinTalkReach(g,source),
                    "The real distant marker has a physically reachable interaction foot");
                g.Talk(source);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="wilderness_hunt_bold");
                Check(begin>=0&&!g.WildernessHuntActive&&
                    !g.Has("wilderness_hunt_fought_"+room),
                    "Resident evidence offers a voluntary hunt, not auto-completion on discovery");
                int supply=room==5?0:room==6?4:2;
                g.save.inventory[supply]=2;
                int before=g.save.inventory[supply];
                g.Choose(begin);
                Check(g.WildernessHuntActive&&g.WildernessHuntWave==1&&
                    g.WildernessHuntRemaining==2&&
                    g.save.inventory[supply]==before&&
                    !g.Has("wilderness_hunt_fought_"+room),
                    "Open pursuit spawns two real raiders without charging ambush materials or awarding unearned progress");
                for(int wave=1;wave<=2;wave++)
                {
                    Enemy[] attackers=g.enemies.ToArray();
                    Check(attackers.Length==2,
                        "Each of the three authored regional encounters has two live foes");
                    for(int i=0;i<2;i++)
                    {
                        Enemy e=attackers[i];
                        Check(e.kind==expected[room-5,(wave-1)*2+i]&&
                            !e.boss&&e.hp>0&&g.CanStand(e.Position)&&
                            g.NavigationPathFrom(new Point(source.x,source.y),
                                e.Position,true).Count>0,
                            "Biome-specific combat rosters occupy real actor-sized connected terrain");
                        hit.Invoke(g,new object[]{e,9999f,true,null,null});
                    }
                    Advance(g,.65f);
                    if(wave==1)
                        Check(g.WildernessHuntActive&&g.WildernessHuntWave==2&&
                            g.WildernessHuntRemaining==2&&
                            !g.Has("wilderness_hunt_fought_"+room),
                            "Only both real contact deaths open the second regional hunting wave");
                }
                Check(!g.WildernessHuntActive&&
                    g.Has("wilderness_hunt_fought_"+room)&&
                    !g.WildernessHuntFinished(room),
                    "Defeating four genuine attackers still requires the civilian decision at source");
                Check(PlaceWithinTalkReach(g,source),
                    "Return on walkable terrain to the same original marker to publish the aftermath");
                g.Talk(source);
                int resolve=Array.FindIndex(g.conversation.choices,
                    c=>c.action== (room==6?
                        "wilderness_hunt_bounty":"wilderness_hunt_refuge"));
                Check(resolve>=0,"Real authored post-hunt choices are reachable");
                int oldCoins=g.save.coins,oldHerbs=g.save.inventory[2];
                g.Choose(resolve);
                bool refuge=room!=6;
                Check(g.WildernessHuntFinished(room)&&
                    g.Has((refuge?"wilderness_hunt_refuge_":
                        "wilderness_hunt_bounty_")+room)&&
                    g.Has("wilderness_hunt_open_"+room)&&
                    g.save.coins>=oldCoins+(refuge?32:70)+12&&
                    (!refuge||g.save.inventory[2]==oldHerbs+2),
                    "One chosen outcome grants the correct permanent civilian or watch benefit");
                g.PrepareSave();
                GameCore restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.WildernessHuntFinished(room)&&
                    !restored.WildernessHuntActive,
                    "Original SaveData v2 round-trip keeps decisions but never resurrects encounter actors");
                Check(PlaceWithinTalkReach(restored,source),
                    "The old marker remains discoverable after a completed hunt");
                restored.Talk(source);
                Check(Array.FindIndex(restored.conversation.choices,
                    c=>c.action=="wilderness_hunt_bold")<0&&
                    Array.FindIndex(restored.conversation.choices,
                    c=>c.action=="wilderness_hunt_refuge")<0,
                    "Veteran saves cannot claim a second hunt or duplicate one-time rewards");
            }
            Check(coordinates.Count==3,"Three real distinct geography-specific hunt locations exist");
        }
        public static void FarshoreHuntAmbushRequiresSupplyAndRejectsPhantomDefeats(Campaign data)
        {
            GameCore g=Fresh(data);g.mode="world";g.save.stage=3;
            g.save.room=6;g.enemies.Clear();g.invulnerable=200f;
            WorldObject site=Array.Find(g.Map.objects,o=>o.id=="wilderness_hunt_6");
            Check(site!=null&&PlaceWithinTalkReach(g,site),
                "The original desert hunt can be reached");
            g.save.inventory[4]=0;g.Talk(site);
            int ambush=Array.FindIndex(g.conversation.choices,
                c=>c.action=="wilderness_hunt_quiet");
            Check(ambush>=0,"Real hunt offers player a tactical paid ambush");
            g.Choose(ambush);
            Check(!g.WildernessHuntActive&&g.enemies.Count==0,
                "No local supply prevents both ghost enemy spawn and free opening");
            g.save.inventory[4]=2;g.Talk(site);
            ambush=Array.FindIndex(g.conversation.choices,
                c=>c.action=="wilderness_hunt_quiet");
            g.Choose(ambush);
            Check(g.WildernessHuntActive&&g.save.inventory[4]==1&&
                !g.Has("wilderness_hunt_open_6")&&
                g.WildernessHuntRemaining==2&&
                g.enemies.TrueForAll(e=>e.stun>=1.39f&&e.armorOpenTime>=1.09f),
                "Paying real desert gears gives a real temporary advantage to the first wave");
            // Removal without authentic hit-death hook is never a victory.
            g.enemies.RemoveAt(0);
            Advance(g,.2f);
            Check(!g.WildernessHuntActive&&g.enemies.Count==0&&
                !g.Has("wilderness_hunt_fought_6")&&
                !g.WildernessHuntFinished(6),
                "Vanished opponents retire every exclusively hunt-owned raider, without forging story progress");
        }
        public static void LegacyV2SavesInsideNewAuthoringCoverRecoverWithoutLosingCampaign(Campaign data)
        {
            // These four tiles were originally walkable empty ground. They
            // now form real roots, water, slag and an outer wall. A veteran's
            // saved character may stand on any of them: repair safely rather
            // than deleting the world, resetting the plot or moving to town.
            int[,] covers={{25,4},{24,5},{24,5},{30,20}};
            char[] terrain={'#','~','#','#'};
            for(int room=1;room<=4;room++)
            {
                GameCore old=Fresh(data);old.save.stage=3;
                old.save.version=2;old.save.room=room;
                old.save.x=covers[room-1,0]+.5f;
                old.save.y=covers[room-1,1]+.5f;
                old.save.health=52f;old.save.inventory[0]=5;
                old.save.flags.Add("chapter_road_done_"+room);
                // Only old SAVES that had already earned the original
                // gated area may legitimately resume behind that gate.
                if(room<=3)
                    old.save.flags.Add(room==1?"scout_saved":"gate_"+room);
                old.save.snapshotRoom=-1;old.save.worldEnemies=null;
                Check(data.maps[room].rows[covers[room-1,1]][covers[room-1,0]]==
                    terrain[room-1],
                    "The deliberately authored new local cover is genuinely impassable");
                GameCore recovered=new GameCore(data,CloneSave(old.save));
                Check(recovered.save.version==2&&recovered.save.room==room&&
                    recovered.CanStand(recovered.Position),
                    "Old v2 world spawn inside newly sculpted terrain recovers to authentic walkable ground");
                Check(recovered.Has("chapter_road_done_"+room)&&
                    recovered.save.stage==3&&recovered.save.inventory[0]==5&&
                    recovered.save.health==52f,
                    "Level remodeling does not erase earned civilian records, supply, health or stage from an old save");
                Check((recovered.Position-new Point(old.save.x,old.save.y)).Length<8f,
                    "One newly blocked tile relocates the returning traveler nearby, never across the 120x80 world");
                // A forged save inside a STILL-CLOSED Boss arena must not
                // select the nearest foothold if the physical gate forbids
                // the path from the original regional entry.
                if(room<=3)
                {
                    SaveData locked=CloneSave(old.save);
                    locked.flags.Remove(room==1?"scout_saved":"gate_"+room);
                    locked.x=29.5f;locked.y=8.5f;
                    GameCore shut=new GameCore(data,locked);
                    Check(!shut.GateOpen&&shut.CanStand(shut.Position),
                        "Recovery never bypasses the original locked Boss arena");
                    Check(shut.NavigationPathFrom(new Point(shut.Map.spawnX,
                        shut.Map.spawnY),shut.Position,true).Count>0,
                        "Recovered foot is on the truly accessible original side of a locked chapter");
                }
            }
        }

        // Public courier ambushers must stand on the genuine walkable road
        // WITHOUT occupying the same painted feet as mission signs or large
        // scenic cover. Exercise the real spawn validator against a shipped
        // outdoor MapData and restore every temporary actor/prop afterwards.
        public static void CivicCourierGuardAvoidsLivingRoadMarkersAndScenery(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.room=5;g.mode="world";
            g.save.x=g.Map.spawnX;g.save.y=g.Map.spawnY;
            g.enemies.Clear();
            MapData map=g.Map;
            WorldObject[] originalObjects=map.objects;
            Decoration[] originalDecor=map.decor;
            var inspect=typeof(GameCore).GetMethod(
                "CivicCourierAmbushFootFree",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(inspect!=null&&g.CanStand(g.Position),
                "Real harbor map provides a legal hero foot and live courier validator");
            try
            {
                Point chosen=new Point();bool found=false;
                for(int y=4;y<map.rows.Length-4&&!found;y++)
                for(int x=4;x<map.rows[0].Length-4&&!found;x++)
                {
                    Point p=new Point(x+.5f,y+.5f);
                    if((bool)inspect.Invoke(g,new object[]{p}))
                    {chosen=p;found=true;}
                }
                Check(found,"Real harbor contains a walkable, unoccupied courier ambush pad");
                // Add a living mission marker, not a made-up collision wall.
                WorldObject[] crowded=new WorldObject[originalObjects.Length+1];
                Array.Copy(originalObjects,crowded,originalObjects.Length);
                crowded[crowded.Length-1]=new WorldObject{
                    id="courier_clearance_probe",name="Medical route marker",
                    en="Medical route marker",kind="waytrail",
                    x=chosen.x,y=chosen.y};
                map.objects=crowded;
                Check(!(bool)inspect.Invoke(g,new object[]{chosen})&&
                    g.CanStand(chosen),
                    "The real courier guard cannot materialize on a valid civilian E target");
                map.objects=originalObjects;
                Decoration[] covered=new Decoration[originalDecor.Length+1];
                Array.Copy(originalDecor,covered,originalDecor.Length);
                covered[covered.Length-1]=new Decoration{
                    kind="tree",x=chosen.x,y=chosen.y};
                map.decor=covered;
                Check(!(bool)inspect.Invoke(g,new object[]{chosen})&&
                    g.CanStand(chosen),
                    "The same ground remains walkable but the guard avoids a large painted tree");
                map.decor=originalDecor;
                Check((bool)inspect.Invoke(g,new object[]{chosen})&&
                    g.save.version==2,
                    "Removing temporary cover restores the exact battle pad without changing saves");
            }
            finally
            {
                map.objects=originalObjects;map.decor=originalDecor;
            }
        }

        // The actual paid field patrol is composed after the player chooses
        // a real survey sign. A following companion or painted cover at a
        // formerly accepted slot should redirect the full encounter, not
        // produce a stacked monster or an invented completion receipt.
        public static void LiveForestPatrolRoutesAroundCompanionAndPaintedTree(Campaign data)
        {
            GameCore first=Fresh(data);
            first.save.room=1;first.mode="world";
            first.save.flags.Add("biome_field_done_1");
            first.enemies.Clear();
            WorldObject site=Array.Find(first.Map.objects,
                o=>o!=null&&o.kind=="biomeSurvey"&&o.id=="biomeSurvey1");
            Check(site!=null&&PlaceWithinTalkReach(first,site),
                "Original forest survey post has legal physical interaction access");
            first.Talk(site);
            int begin=Array.FindIndex(first.conversation.choices,
                c=>c.action=="biome_patrol_begin");
            Check(begin>=0,"Real forest board offers an actual patrol contract");
            first.Choose(begin);
            Check(first.FieldPatrolActive&&first.FieldPatrolRemaining==2,
                "Baseline contracted forest wave spawns two distinct live enemies");
            Point occupied=first.enemies[0].Position;
            GameCore guarded=Fresh(data);
            guarded.save.room=1;guarded.mode="world";
            guarded.save.flags.Add("biome_field_done_1");
            guarded.enemies.Clear();
            MapData map=guarded.Map;
            Decoration[] originalDecor=map.decor;
            try
            {
                Decoration[] crowded=new Decoration[originalDecor.Length+1];
                Array.Copy(originalDecor,crowded,originalDecor.Length);
                crowded[crowded.Length-1]=new Decoration{
                    kind="tree",x=occupied.x,y=occupied.y};
                map.decor=crowded;
                Check(PlaceWithinTalkReach(guarded,site),
                    "Additional scenery must not affect the original walkable sign");
                guarded.save.companion=0;
                guarded.companionPosition=occupied;
                guarded.Talk(site);
                int choice=Array.FindIndex(guarded.conversation.choices,
                    c=>c.action=="biome_patrol_begin");
                Check(choice>=0,"The same forest contract remains playable");
                guarded.Choose(choice);
                Check(guarded.FieldPatrolActive&&
                    guarded.FieldPatrolRemaining==2&&
                    guarded.enemies.Count==2,
                    "The live patrol still deploys its FULL paid wave around occupied cover");
                foreach(Enemy foe in guarded.enemies)
                    Check((foe.Position-occupied).Length>=1.7f&&
                        guarded.CanStand(foe.Position)&&
                        guarded.NavigationPathFrom(guarded.Position,
                            foe.Position,true).Count>0,
                        "Neither a companion nor large tree becomes a monster spawn pad");
                Check(guarded.save.version==2&&!guarded.FieldPatrolDone(1),
                    "Avoidance cannot complete a contract or alter SaveData v2");
            }
            finally { map.decor=originalDecor; }
        }
    }
}