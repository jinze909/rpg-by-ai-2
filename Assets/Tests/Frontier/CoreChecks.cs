using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Advance(GameCore g, float seconds)
        { while (seconds > .00001f) { float step = Math.Min(.02f, seconds); g.Tick(step, new Point()); seconds -= step; } }
        private static void AwaitArenaOpening(GameCore g)
        { for(int frame=0;frame<100&&g.GateBarrierActive;frame++)g.Tick(.02f,new Point());Check(!g.GateBarrierActive,"The objective gate must finish opening before its Boss challenge"); }
        private static GameCore Fresh(Campaign data, int profession = 0)
        { var g = new GameCore(data); g.NewGame(profession); g.Close(); return g; }
        private static WorldObject Object(GameCore g, string kind)
        { foreach (var o in g.Map.objects) if (o.kind == kind) return o; throw new Exception("Missing object " + kind); }
        private static Point StandableOutsideTalkReach(GameCore g, WorldObject o)
        {
            Point[] offsets={new Point(-2f,0f),new Point(2f,0f),new Point(0f,-2f),new Point(0f,2f),
                new Point(-1.5f,-1.5f),new Point(-1.5f,1.5f),new Point(1.5f,-1.5f),new Point(1.5f,1.5f)};
            foreach(Point offset in offsets)
            {
                Point p=new Point(o.x+offset.x,o.y+offset.y);
                float d=(p-new Point(o.x,o.y)).Length;
                if(d>=1.75f&&d<=2.2f&&g.CanStand(p))return p;
            }
            throw new Exception("No standable regression point outside physical Talk reach for "+o.id);
        }
        private static Point StandableOutsidePhysicalRadius(GameCore g, WorldObject o,float radius)
        {
            Point[] offsets={new Point(-3f,0f),new Point(3f,0f),new Point(0f,-3f),new Point(0f,3f),
                new Point(-2.5f,-2.5f),new Point(-2.5f,2.5f),new Point(2.5f,-2.5f),new Point(2.5f,2.5f)};
            foreach(Point offset in offsets)
            {
                Point p=new Point(o.x+offset.x,o.y+offset.y);
                if((p-new Point(o.x,o.y)).Length>radius&&g.CanStand(p))return p;
            }
            throw new Exception("No standable regression point beyond radius "+radius+" for "+o.id);
        }
        private static void TalkNearObject(GameCore g, WorldObject target)
        {
            if (!PlaceWithinTalkReach(g, target))
                throw new Exception("No reachable test position for " + target.id);
            g.Talk(target);
        }
        private static void TalkNear(GameCore g, string kind)
        {
            TalkNearObject(g, Object(g, kind));
        }
        // Story outcomes are now optional in the real guest's topic menu,
        // not a compulsory monologue on every E press. Keep every original
        // outcome assertion, but select the in-game news choice with genuine
        // NPC physical reach and source verification before reading it.
        private static void ReadInnNews(GameCore game, WorldObject traveler)
        {
            Check(traveler!=null&&traveler.kind=="innGuest",
                "News must come from the original inn traveler");
            Check(PlaceWithinTalkReach(game,traveler),
                "Inn traveler must have a physically reachable conversation position");
            game.Talk(traveler);
            Check(game.mode=="dialog"&&game.conversation!=null&&
                game.conversationSource==traveler,
                "The original traveler starts the physical dialogue");
            int news=Array.FindIndex(game.conversation.choices,
                choice=>choice.action=="inn_news");
            Check(news>=0,"The news topic is a real playable NPC choice");
            game.Choose(news);
            Check(game.mode=="dialog"&&game.conversation!=null&&
                game.conversationSource==traveler&&
                game.conversation.speaker==traveler.name,
                "The unchanged historical dialogue belongs to the original live traveler");
        }
        private static bool ChoiceNeedsPhysicalSource(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            return action.StartsWith("goto") || action == "routes" ||
                action == "rest" || action == "scout" || action == "engine" ||
                action == "plant" || action == "harvest" || action == "seeds" ||
                action == "upgrade" || action == "bow" || action == "staff" ||
                action == "cook" || action == "buymeal" || action == "buywood" ||
                action == "fish" || action == "deliver" || action.StartsWith("ally") ||
                action == "boss" || action.StartsWith("ending") ||
                action == "epilogue" || action == "continue" ||
                action == "swordlesson_start" ||
                action == "swordlesson_veteran_start" ||
                action == "practice" || action == "scoutgift" ||
                action == "survey_finish" || action == "survey_home" ||
                action.StartsWith("survey_to_") || action == "dispatch_recap" ||
                action == "farshore_relief_routes" ||
                action == "farshore_relief_return" ||
                action.StartsWith("farshore_relief_to_") ||
                action == "exp_begin" || action == "camp" ||
                action == "companions" || action == "parley" ||
                action.StartsWith("pledge") ||
                action.StartsWith("rescue") || action.StartsWith("relay") ||
                action == "holdtide" || action == "harbor_report" ||
                action.StartsWith("desert") || action.StartsWith("mountain") ||
                action.StartsWith("report_") || action == "expboss" ||
                action.StartsWith("warm:") || action == "tea" ||
                action == "drinktea" || action == "fieldrest" || action == "race" ||
                action.StartsWith("personal") || action.StartsWith("gift") ||
                action.StartsWith("join") || action.StartsWith("covenant") ||
                action == "council_epilogue" ||
                action.StartsWith("abyssStart") || action == "abyssLeave" ||
                action == "abyssBank" || action.StartsWith("boon");
        }
        private static bool ImmediateWorldObjectNeedsReach(string kind, string action)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            if (kind.StartsWith("portal") || kind.StartsWith("history") ||
                kind == "routes" || kind.StartsWith("rune") || kind == "lore" ||
                kind == "herb" || kind == "wood" || kind == "ore" ||
                kind == "shrine" || kind.StartsWith("cache") || kind.StartsWith("seal") ||
                kind.StartsWith("mirror") || kind.StartsWith("bell") ||
                kind == "manifest" || kind == "abyssEntry" ||
                kind == "townboard" || kind == "contract") return true;
            return kind.StartsWith("survey") && kind != "surveyDesk" &&
                action == "close";
        }
        private static void Action(GameCore g, string kind, string action)
        {
            if (ChoiceNeedsPhysicalSource(action) ||
                ImmediateWorldObjectNeedsReach(kind, action))
                TalkNear(g, kind);
            else
                g.Talk(Object(g, kind));
            Check(g.mode == "dialog", "Expected dialogue at " + kind);
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { g.Choose(i); return; }
            throw new Exception("Missing action " + action);
        }
        private static void SetOuterPatrolLull(GameCore g, int room)
        {
            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var activeRoom=typeof(GameCore).GetField("outerPatrolRoom",flags);
            var time=typeof(GameCore).GetField("outerPatrolTime",flags);
            var stock=typeof(GameCore).GetField("outerPatrolStock",flags);
            if(activeRoom==null||time==null||stock==null)
                throw new Exception("Outer-patrol transient state fields are missing");
            activeRoom.SetValue(g,room);time.SetValue(g,60f);stock.SetValue(g,100f);
        }
        private static void ClearOuterPatrolLull(GameCore g)
        {
            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            typeof(GameCore).GetField("outerPatrolRoom",flags).SetValue(g,-1);
            typeof(GameCore).GetField("outerPatrolTime",flags).SetValue(g,0f);
            typeof(GameCore).GetField("outerPatrolStock",flags).SetValue(g,0f);
        }
        public static void QueuedDodgeCancelsOnRealSealedFooting(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[18];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] floor=new string('.',18).ToCharArray();
                    for(int x=0;x<18;x++)
                        if(x<2||x>=16||y<2||y>=16)floor[x]='#';
                    rows[y]=new string(floor);
                }
                data.maps[0]=new MapData{
                    id=0,spawnX=6.5f,spawnY=8.5f,
                    rows=rows,objects=new WorldObject[0],
                    decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.x=6.5f;g.save.y=8.5f;
                // Fresh world entry grants an unrelated 1s spawn safety
                // window. Remove it before testing that a sealed queue
                // creates NO new dodge invulnerability.
                g.invulnerable=0f;
                g.save.energy=100f;g.dashCooldown=.10f;
                Check(g.DodgeRootReady&&g.CanBufferDodge,
                    "A physically open floor permits the last 140ms dodge buffer");
                g.Dash(new Point(1,0));
                Check(g.DodgeQueued&&g.dashTime==0f&&
                    g.save.energy==100f,
                    "The first real late dodge tap queues but neither moves nor pays");
                char[] sealedFoot=rows[8].ToCharArray();
                sealedFoot[6]='#';rows[8]=new string(sealedFoot);
                Check(!g.CanStand(g.Position)&&!g.DodgeRootReady&&
                    !g.DodgeQueued&&!g.CanBufferDodge,
                    "A newly sealed foot root must immediately hide pending/available dodge feedback");
                g.Tick(.05f,new Point());
                Check(!g.DodgeQueued&&g.dashTime==0f&&
                    g.save.energy==100f&&g.invulnerable<=0f,
                    "A wall-sealed queue cannot buy phantom invulnerability, movement or stamina cost");
                sealedFoot[6]='.';rows[8]=new string(sealedFoot);
                Check(g.DodgeRootReady&&g.CanBufferDodge,
                    "Reopened foot terrain restores genuine late-dodge eligibility");
                g.Dash(new Point(1,0));
                Check(g.DodgeQueued&&g.save.energy==100f,
                    "Fresh escape after gate reopening creates a NEW valid intent");
                g.Tick(.05f,new Point());
                Check(!g.DodgeQueued&&g.dashTime>0f&&
                    g.save.energy<82f&&g.save.energy>=80f,
                    "Reopened buffer must dispatch exactly one real paid dash");
            }
            finally{data.maps[0]=original;}
        }

        // The last harbor signal stands above water, but its old approach
        // vanished under the tide at certain otherwise legitimate campaign
        // timings. The raised quay must preserve a reachable interaction
        // pad in high water, not require a lucky world-clock phase.
        public static void MistportRelayTwoUsesPermanentPhysicalQuay(Campaign data)
        {
            var g=Fresh(data);
            g.mode="world";g.save.room=5;
            WorldObject relay=Array.Find(g.Map.objects,o=>o.kind=="relay2");
            Check(relay!=null&&relay.x==25f&&relay.y==19f,
                "Original third harbor relay remains at the authored pier");
            // Start two tiles away: NavigationPathFrom intentionally returns
            // an empty route when the actor is already within its finish
            // radius, so a one-step starting pad cannot test this bridge.
            Point landing=new Point(28.5f,19.5f);
            Point talkFoot=new Point(26.5f,19.5f);
            var reach=typeof(GameCore).GetMethod("HasPhysicalInteractionReach",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic,
                null,new Type[]{typeof(WorldObject)},null);
            Check(reach!=null,"The real dialogue reach gate is available");
            for(int i=0;i<8;i++)
            {
                // Different live tide timings; no fake collision flags.
                g.save.totalTime=i*11f;
                Check(g.CanStand(landing)&&g.CanStand(talkFoot)&&
                    g.NavigationPathFrom(landing,talkFoot,true).Count>0,
                    "A dry connected route remains on the third relay quay at tide phase "+i);
                g.save.x=talkFoot.x;g.save.y=talkFoot.y;
                Check((bool)reach.Invoke(g,new object[]{relay}),
                    "The original relay can be physically reached from a real quay tile at phase "+i);
            }
            Check(g.save.version==2,
                "The fixed physical landing needs no new serialized tide state");
        }

        public static void SevenSurveyPatrolContractsRequireRealKillsAndSignedReports(Campaign data)
        {
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var hit=typeof(GameCore).GetMethod("Hit",flags);
            Check(hit!=null,"Patrol kills use the original live combat damage function");
            var visited=new System.Collections.Generic.HashSet<int>();
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);
                g.save.room=room;g.mode="world";
                g.enemies.Clear();
                g.save.flags.Add("biome_field_done_"+room);
                WorldObject source=Array.Find(g.Map.objects,
                    o=>o.kind=="biomeSurvey"&&o.id=="biomeSurvey"+room);
                Check(source!=null&&PlaceWithinTalkReach(g,source),
                    "Regional patrol "+room+" has a reachable original survey post");
                Point origin=g.Position;
                g.Talk(source);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "Original field post opens a real patrol dialogue: "+room);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_begin");
                Check(begin>=0,"Patrol can be started in the real survey dialogue: "+room);
                g.Choose(begin);
                int needed=room==2||room==3||room==6?3:2;
                Check(g.FieldPatrolActive&&g.FieldPatrolRemaining==needed&&
                    g.enemies.Count==needed,
                    "Patrol "+room+" spawns its complete physical raider wave");
                foreach(Enemy foe in g.enemies)
                    Check(g.CanStand(foe.Position)&&
                        g.NavigationPathFrom(origin,foe.Position,true).Count>0&&
                        (foe.Position-origin).Length>=5f,
                        "Every contracted enemy must stand on a genuine reachable attack lane");
                Check(g.GetQuest("field_patrols").id=="field_patrols"&&
                    g.GetQuest("field_patrols").room==room&&
                    Array.Exists(g.enemies.ToArray(),e=>
                        (g.GetQuest("field_patrols").target-e.Position).Length<.001f)&&
                    !g.GetQuest("field_patrols").complete,
                    "The playable journal tracks an active region and the true combat");
                int coins=g.save.coins,shards=g.save.shards;
                g.save.x=origin.x;g.save.y=origin.y;g.Close();g.Talk(source);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "Original post remains a dialogue during live raider combat: "+room);
                int early=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_report");
                Check(early<0&&!g.FieldPatrolReportReady&&
                    g.save.coins==coins&&g.save.shards==shards,
                    "The first post cannot pay a patrol receipt before real deaths");
                g.Close();
                Enemy[] wave=g.enemies.ToArray();
                foreach(Enemy foe in wave)
                    hit.Invoke(g,new object[]{foe,9999f,true,null,null});
                Check(g.FieldPatrolReportReady&&g.FieldPatrolRemaining==0,
                    "All tracked actor identities were actually killed");
                g.invulnerable=5f;
                for(int t=0;t<150;t++)g.Tick(.02f,new Point());
                g.save.x=origin.x;g.save.y=origin.y;
                Check(PlaceWithinTalkReach(g,source),
                    "Hero returns to exactly the original world marker");
                g.Close();g.Talk(source);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "Real patrol can reopen its original post after a cleared fight: "+room);
                int report=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_report");
                Check(report>=0,"The signed report is a genuine live dialogue choice");
                g.Choose(report);
                Check(g.FieldPatrolDone(room)&&!g.FieldPatrolActive&&
                    g.save.coins>=coins+26+2*room&&
                    g.save.shards>=shards+1&&
                    g.GetQuest("field_patrols").steps[room-1].done,
                    "Real kills and actual return grant precisely this district's saved receipt");
                g.PrepareSave();
                // Load() intentionally sanitizes its mutable input slot.
                // Clone the serialized v2 payload before loading a second
                // game, or a still-locked region can rewrite the ORIGINAL
                // live hero's room and break the next survey conversation.
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.FieldPatrolDone(room)&&
                    !restored.FieldPatrolActive&&restored.save.version==2,
                    "Old v2 save state permanently retains one receipt without reviving ephemeral raiders");
                g.Close();g.Talk(source);
                Check(g.mode=="dialog"&&g.conversation!=null,
                    "Already completed patrol site still opens safe world dialogue: "+room);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_begin")<0,
                    "A signed receipt cannot respawn another paid wave");
                int relay=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_relay");
                Check(relay>=0&&
                    g.conversation.choices[relay].label.Contains("硬币2 / 耐力10"),
                    "Every signed, unimproved district must advertise a real 2-coin/10-stamina relay");
                if(room==1)
                {
                    g.save.coins=100;g.save.energy=100f;
                    WorldObject far=Array.Find(g.Map.objects,
                        o=>o.id=="biomeAnswer1");
                    Check(far!=null,"Patrol discount uses the authentic far marker");
                    g.Choose(relay);
                    Check(g.save.coins==98&&
                        Math.Abs(g.save.energy-90f)<.001f&&
                        (g.Position-new Point(far.x,far.y)).Length<2f,
                        "A signed patrol lowers a REAL physical relay price, not merely a dialogue label");
                }
                visited.Add(room);
            }
            // Missing or forged raiders must never pay a signed report.
            GameCore interrupted=Fresh(data);
            interrupted.save.room=1;interrupted.mode="world";
            interrupted.enemies.Clear();
            interrupted.save.flags.Add("biome_field_done_1");
            WorldObject originPost=Array.Find(interrupted.Map.objects,
                o=>o.id=="biomeSurvey1");
            Check(originPost!=null&&PlaceWithinTalkReach(interrupted,originPost),
                "An interruptible patrol starts at the real original post");
            Point originalFoot=interrupted.Position;
            interrupted.Talk(originPost);
            Check(interrupted.conversation!=null,
                "Interruptible patrol opens a legitimate source dialogue");
            int index=Array.FindIndex(interrupted.conversation.choices,
                c=>c.action=="biome_patrol_begin");
            Check(index>=0,"Available uncompleted patrol can be accepted");
            interrupted.Choose(index);
            Check(interrupted.FieldPatrolActive&&
                interrupted.FieldPatrolRemaining==2,
                "Region 1 patrol has two real registered live raiders");
            int originalCoins=interrupted.save.coins;
            interrupted.PrepareSave();
            Check(interrupted.save.version==2&&
                interrupted.save.snapshotRoom==1&&
                interrupted.save.worldEnemies!=null&&
                interrupted.save.worldEnemies.Length==0&&
                interrupted.FieldPatrolRemaining==2,
                "Mid-contract v2 saves exclude temporary patrol raiders without deleting any live actor");
            interrupted.enemies.RemoveAt(0);
            interrupted.Tick(.02f,new Point());
            Check(!interrupted.FieldPatrolActive&&
                !interrupted.FieldPatrolDone(1)&&
                interrupted.enemies.Count==0&&
                interrupted.save.coins==originalCoins,
                "Removing one live raider without a Hit cannot mint a report or leave orphaned companions");
            interrupted.save.x=originalFoot.x;
            interrupted.save.y=originalFoot.y;
            interrupted.Close();interrupted.Talk(originPost);
            Check(interrupted.conversation!=null,
                "Original field post remains usable after an interrupted patrol");
            index=Array.FindIndex(interrupted.conversation.choices,
                c=>c.action=="biome_patrol_begin");
            Check(index>=0,"An incomplete contract is safely retryable");
            interrupted.Choose(index);
            Check(interrupted.FieldPatrolActive&&
                interrupted.FieldPatrolRemaining==2,
                "Retry creates only one fresh, exact actor-owned wave");
            interrupted.Talk(originPost);
            Check(interrupted.conversation!=null,
                "Active patrol offers a real cancellation at its source");
            index=Array.FindIndex(interrupted.conversation.choices,
                c=>c.action=="biome_patrol_abort");
            Check(index>=0,"The real original post offers voluntary withdrawal");
            interrupted.save.x=originalFoot.x+20f;
            interrupted.Choose(index);
            Check(interrupted.FieldPatrolActive&&
                !interrupted.FieldPatrolDone(1),
                "Moving out of range invalidates stale abort without changing the active objective");
            interrupted.save.x=originalFoot.x;
            interrupted.save.y=originalFoot.y;
            interrupted.Close();interrupted.Talk(originPost);
            Check(interrupted.conversation!=null,
                "Returning physically to the post restores its real interaction");
            index=Array.FindIndex(interrupted.conversation.choices,
                c=>c.action=="biome_patrol_abort");
            Check(index>=0,"At-source withdrawal is an actual dialogue choice");
            interrupted.Choose(index);
            Check(!interrupted.FieldPatrolActive&&
                !interrupted.FieldPatrolDone(1)&&
                interrupted.enemies.Count==0,
                "Legitimate cancellation cleans only contracted raiders without completing the quest");

            Check(visited.Count==7,
                "Seven physically distinct original patrol contracts have been completed");
            var completed=Fresh(data);
            for(int room=1;room<=7;room++)
                completed.save.flags.Add("biome_patrol_done_"+room);
            Check(completed.FieldPatrolWins==7&&
                completed.GetQuest("field_patrols").complete&&
                completed.GetQuest("field_patrols").steps.Count==7,
                "Seven independent saved first-clears complete the permanent civilian patrol atlas");
        }

        // A bent but fully walkable field lane is valid even if every old
        // eight-direction ray has become a wall. Exercise the live post,
        // dialogue and registered enemies; never grant a partial free wave.
        public static void WindingSurveyRoadCanStageCompletePhysicalPatrol(Campaign data)
        {
            MapData map=data.maps[1];
            string[] original=map.rows;
            string[] changed=(string[])original.Clone();
            const int sx=72,sy=26;
            int[,] dirs={{1,0},{0,1},{-1,0},{0,-1},
                {1,1},{-1,1},{-1,-1},{1,-1}};
            try
            {
                // Only the local test fixture is edited; the shipped region
                // and all neighboring test runs retain their original rows.
                for(int y=sy-14;y<=sy+14;y++)
                {
                    char[] line=changed[y].ToCharArray();
                    for(int x=sx-14;x<=sx+14;x++)line[x]='.';
                    changed[y]=new string(line);
                }
                for(int d=0;d<8;d++)
                for(int radius=7;radius<=12;radius++)
                {
                    int x=sx+dirs[d,0]*radius;
                    int y=sy+dirs[d,1]*radius;
                    char[] line=changed[y].ToCharArray();
                    line[x]='#';changed[y]=new string(line);
                }
                map.rows=changed;
                GameCore g=Fresh(data);
                g.save.room=1;g.mode="world";g.enemies.Clear();
                g.save.flags.Add("biome_field_done_1");
                WorldObject post=Array.Find(g.Map.objects,
                    o=>o.id=="biomeSurvey1"&&o.kind=="biomeSurvey");
                Check(post!=null&&PlaceWithinTalkReach(g,post),
                    "Winding field post remains authentically reachable");
                Point origin=g.Position;
                g.Talk(post);
                int accept=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="biome_patrol_begin");
                Check(accept>=0,"Authentic patrol dialogue offers a contract");
                g.Choose(accept);
                Check(g.FieldPatrolActive&&g.FieldPatrolRemaining==2&&
                    g.enemies.Count==2,
                    "Winding but connected road stages a complete real wave");
                foreach(Enemy foe in g.enemies)
                    Check(g.CanStand(foe.Position)&&
                        g.NavigationPathFrom(origin,foe.Position,true).Count>0&&
                        (foe.Position-origin).Length>=5f,
                        "Fallback raider is reached by actual collision-aware walking");
                Check((g.enemies[0].Position-g.enemies[1].Position).Length>=5f&&
                    !g.FieldPatrolDone(1)&&g.save.version==2,
                    "Separated raiders grant no receipt before genuine combat");
            }
            finally {map.rows=original;}
        }

        // Nonlethal resolution is a player action, not a flag or removed
        // enemy. Exercise the original live post, real trap, Hit and report.
        public static void CivilianPatrolAcceptsActualNonlethalSnareRestraint(Campaign data)
        {
            GameCore g=Fresh(data);
            g.mode="world";g.save.room=1;g.enemies.Clear();
            g.save.flags.Add("biome_field_done_1");
            WorldObject post=Array.Find(g.Map.objects,o=>
                o.kind=="biomeSurvey"&&o.id=="biomeSurvey1");
            Check(post!=null&&PlaceWithinTalkReach(g,post),
                "Real survey post supports this voluntary patrol");
            Point originalFoot=g.Position;
            g.Talk(post);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_patrol_begin");
            Check(begin>=0,"Patrol accepts original live dialogue");
            g.Choose(begin);
            Check(g.FieldPatrolActive&&g.FieldPatrolRemaining==2&&
                g.enemies.Count==2,"Two real exact enemy identities form this patrol");
            Enemy captive=g.enemies[0],combatant=g.enemies[1];
            int kills=0;foreach(int n in g.save.kills)kills+=n;
            g.save.inventory[0]=10;g.save.inventory[4]=10;
            g.save.energy=100f;
            Point trap=PutRealSnare(g);
            captive.x=trap.x;captive.y=trap.y;
            captive.hitTime=5f;combatant.hitTime=5f;
            Check(g.FieldSnares.Count==1&&!g.CanBindSnaredEnemy,
                "Moving enemy into the trap does not forge an armed contact");
            for(int f=0;f<11;f++)g.Tick(.05f,new Point());
            Check(g.FieldPatrolActive&&g.CanBindSnaredEnemy&&
                object.ReferenceEquals(g.SnaredBindTarget,captive),
                "Only genuine armed snare contact enables living capture");
            Check(g.BindSnaredEnemy()&&!g.enemies.Contains(captive)&&
                g.FieldPatrolRemaining==1&&!g.FieldPatrolReportReady,
                "One living restraint contributes but cannot pay a premature contract");
            int after=0;foreach(int n in g.save.kills)after+=n;
            Check(after==kills&&captive.hp>0f,
                "Nonlethal patrol restraint does not grant enemy kills");
            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var hit=typeof(GameCore).GetMethod("Hit",flags);
            Check(hit!=null&&g.enemies.Contains(combatant),
                "Second genuine raider stays for original combat resolution");
            hit.Invoke(g,new object[]{combatant,9999f,true,null,null});
            Check(g.FieldPatrolReportReady&&g.FieldPatrolRemaining==0,
                "Mixed real death and living capture complete an authentic patrol");
            g.invulnerable=5f;
            for(int frame=0;frame<150;frame++)g.Tick(.02f,new Point());
            g.save.x=originalFoot.x;g.save.y=originalFoot.y;
            Check(PlaceWithinTalkReach(g,post),
                "Hero genuinely returns to the original survey post");
            g.Close();g.Talk(post);
            int report=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_patrol_report");
            Check(report>=0,"Physical post offers a mixed-method signed receipt");
            g.Choose(report);
            Check(g.FieldPatrolDone(1)&&!g.FieldPatrolActive&&
                g.GetQuest("field_patrols").steps[0].done,
                "Recorded civilian receipt recognizes restraint without changing legacy gates");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.FieldPatrolDone(1)&&
                !restored.FieldPatrolActive,
                "V2 reload saves signed achievement but not live temporary captives");
        }

        // Distinguish the hard-earned all-living route from a mixed report.
        // Both original posts, real grounded snare contacts and the actual
        // physical fare/energy transaction participate in the tested loop.
        public static void AllLivingPatrolTestimonyChangesRealRelayFareAndV2Report(Campaign data)
        {
            GameCore g=Fresh(data);
            g.mode="world";g.save.room=1;g.enemies.Clear();
            g.save.flags.Add("biome_field_done_1");
            WorldObject post=Array.Find(g.Map.objects,o=>
                o.kind=="biomeSurvey"&&o.id=="biomeSurvey1");
            Check(post!=null&&PlaceWithinTalkReach(g,post),
                "The authentic field source is physically reachable");
            Point origin=g.Position;
            g.Talk(post);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_patrol_begin");
            Check(begin>=0,"Original survey dialogue offers the live patrol");
            g.Choose(begin);
            Check(g.FieldPatrolActive&&g.enemies.Count==2,
                "Two individual living raiders are assigned to this patrol");
            var foes=g.enemies.ToArray();
            g.save.inventory[0]=12;g.save.inventory[4]=12;
            g.save.energy=100f;
            int shards=g.save.shards;
            for(int i=0;i<foes.Length;i++)
            {
                Point foot=PutRealSnare(g);
                foes[i].x=foot.x;foes[i].y=foot.y;
                foes[i].hitTime=6f;
                foreach(Enemy other in g.enemies)other.hitTime=6f;
                for(int t=0;t<11;t++)g.Tick(.05f,new Point());
                Check(g.CanBindSnaredEnemy&&
                    object.ReferenceEquals(g.SnaredBindTarget,foes[i])&&
                    g.BindSnaredEnemy()&&foes[i].hp>0f,
                    "Each authentic enemy is physically restrained alive after an armed trap");
                Check(g.FieldPatrolRemaining==foes.Length-i-1,
                    "The last captive is not counted again");
            }
            Check(g.FieldPatrolReportReady&&!g.FieldPatrolSpared(1),
                "Provisional all-living success is not yet a signed persistent perk");
            g.invulnerable=5f;
            for(int t=0;t<150;t++)g.Tick(.02f,new Point());
            g.save.x=origin.x;g.save.y=origin.y;
            Check(PlaceWithinTalkReach(g,post),
                "Hero physically returns to the original report site");
            g.Close();g.Talk(post);
            int report=Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_patrol_report");
            Check(report>=0,"Unclaimed authentic all-living receipt is ready");
            g.Choose(report);
            Check(g.FieldPatrolSpared(1)&&g.FieldPatrolSparedCount==1&&
                g.FieldPatrolDone(1)&&g.save.shards==shards+2,
                "Real all-captive report earns one-time additional shard and persistent safe route");
            var fare=typeof(GameCore).GetMethod("FieldRelayFare",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var energy=typeof(GameCore).GetMethod("FieldRelayEnergy",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(fare!=null&&energy!=null,
                "Production field relay uses common exact fare and stamina rules");
            Check((int)fare.Invoke(g,new object[]{1})==2&&
                (int)energy.Invoke(g,new object[]{1})==7,
                "All-captive evidence improves actual default relay stamina from 10 to 7");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.FieldPatrolSpared(1)&&
                restored.FieldPatrolSparedCount==1&&
                (int)energy.Invoke(restored,new object[]{1})==7,
                "V2 load retains earned humanely restored route without transient actors");
            restored.save.flags.Add("biome_supply_road_1");
            Check((int)energy.Invoke(restored,new object[]{1})>=1,
                "Multiple persistent route discounts never produce negative stamina");
            g.Close();g.Talk(post);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="biome_patrol_begin")<0&&
                g.save.shards==shards+2,
                "No paid patrol can be replayed to double-award nonlethal evidence");
        }

        // New region content must be real map objects, not a reward bound
        // to a fabricated dialogue ID. Inventory, physical reach and the
        // original region survey are hard prerequisites of every pickup.
        public static void SevenFieldKitsArePhysicalOnceOnlySalvage(Campaign data)
        {
            var located=new System.Collections.Generic.HashSet<string>();
            for(int room=1;room<=7;room++)
            {
                GameCore g=Fresh(data);
                g.save.room=room;g.mode="world";g.enemies.Clear();
                WorldObject box=Array.Find(g.Map.objects,o=>
                    o.kind=="fieldKit"&&o.id=="fieldKit"+room);
                Check(box!=null&&located.Add(box.id)&&
                    PlaceWithinTalkReach(g,box),
                    "Authored regional field kit must be physically approachable in "+room);
                Check(g.CanStand(g.Position)&&
                    g.GetQuest("field_kits").id=="field_kits",
                    "Existing production world and quest registry recognize the new kit");
                int startWood=g.save.inventory[0],
                    startGear=g.save.inventory[4];
                g.Talk(box);
                Check(g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action=="field_kit_collect")<0,
                    "Unexamined region cannot take strangers' field supplies");
                g.Close();
                g.save.flags.Add("biome_field_done_"+room);
                g.save.energy=100f;
                g.Talk(box);
                int option=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_kit_collect");
                Check(option>=0,"Original kit offers genuine field recovery");
                g.save.inventory[0]=9999;
                g.Choose(option);
                Check(!g.FieldKitRecovered(room)&&
                    g.save.energy==100f&&g.save.inventory[4]==startGear,
                    "Full timber stack cannot consume energy or partially award gears");
                g.save.inventory[0]=startWood;
                g.Close();g.Talk(box);
                option=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_kit_collect");
                Check(option>=0,"Unclaimed case remains claimable after freeing storage");
                g.Choose(option);
                Check(g.FieldKitRecovered(room)&&
                    g.save.inventory[0]==startWood+g.FieldKitTimber(room)&&
                    g.save.inventory[4]==startGear+g.FieldKitParts(room)&&
                    Math.Abs(g.save.energy-94f)<.001f,
                    "Physical dismantling pays exactly real region-specific stock and stamina");
                Check(g.GetQuest("field_kits").steps[room-1].done,
                    "Authenticated pickup advances its own journal entry");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.FieldKitRecovered(room)&&
                    restored.save.inventory[0]==g.save.inventory[0],
                    "V2 save preserves one-time recovered materials and site ownership");
                g.Close();g.Talk(box);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="field_kit_collect")<0,
                    "A recovered physical kit never dispenses a second stack");
            }
            Check(located.Count==7,"Seven distinct source maps carry seven different physical kits");
        }

        // Exercise the actual SpawnRoom -> SpawnExpansionRoom route used by
        // Frontier.unity, not just a helper that returns plausible points.
        public static void SevenRegionsStageDistinctReachableStoryBattles(Campaign data)
        {
            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var spawn=typeof(GameCore).GetMethod("SpawnRoom",flags);
            Check(spawn!=null,"The live room-entry battle factory exists");
            var signatures=new HashSet<string>();
            int changed=0;
            for(int region=1;region<=7;region++)
            {
                GameCore g=Fresh(data);
                MapData map=data.maps[region];
                g.mode="world";g.save.room=region;
                g.save.x=map.spawnX;g.save.y=map.spawnY;
                spawn.Invoke(g,null);
                int needed=region<=2?6:region<=4?8:7;
                Check(g.enemies.Count>=needed,
                    "Region "+region+" retains the complete mandatory battle roster");
                string signature="";
                Point start=new Point(map.spawnX,map.spawnY);
                int[,] old={
                    {11,12},{15,17},{21,15},{24,6},
                    {9,7},{30,16},{23,19},{14,9}
                };
                for(int i=0;i<needed;i++)
                {
                    Enemy foe=g.enemies[i];
                    Check(foe!=null&&!foe.boss&&foe.hp>0&&
                        g.CanStand(foe.Position)&&
                        (foe.Position-start).Length>=4.5f,
                        "Region "+region+" roster starts on legal, fair walking terrain");
                    Check(g.NavigationPathFrom(start,foe.Position,true).Count>0,
                        "Region "+region+" enemy "+i+" can be reached on the true foot collision graph");
                    foreach(WorldObject obj in map.objects)
                        Check((foe.Position-new Point(obj.x,obj.y)).Length>=1.95f,
                            "Region "+region+" must not bury an NPC/quest interaction under an enemy");
                    for(int prev=0;prev<i;prev++)
                        Check((foe.Position-g.enemies[prev].Position).Length>=2.45f,
                            "Region "+region+" has legible separate enemy fronts, not stacked patrols");
                    signature+=(int)Math.Floor(foe.Position.x)+":"+
                        (int)Math.Floor(foe.Position.y)+";";
                    if((foe.Position-new Point(old[i,0],old[i,1])).Length>=2f)
                        changed++;
                }
                Check(signatures.Add(signature),
                    "Seven authored regions must not reuse a clone of one battle formation");
                Check(g.save.version==2&&g.enemies.Count>=needed,
                    "Region battle staging preserves the existing v2 save and actor count");
            }
            Check(changed>=20,
                "The seven opening encounters must meaningfully depart from the legacy shared coordinates");
        }

        public static void RegionalBattleFallbackNeverSpawnsBehindBlockedTerrain(Campaign data)
        {
            MapData map=data.maps[1];
            string[] original=map.rows;
            try
            {
                string[] isolated=new string[original.Length];
                for(int y=0;y<isolated.Length;y++)
                {
                    char[] cells=new string('#',original[y].Length).ToCharArray();
                    if(y>=8&&y<=33)
                        for(int x=36;x<=65;x++)cells[x]='.';
                    isolated[y]=new string(cells);
                }
                map.rows=isolated;
                GameCore g=Fresh(data);
                g.mode="world";g.save.room=1;
                g.save.x=43.5f;g.save.y=20.5f;
                g.enemies.Clear();
                var flags=System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance;
                var reachableFn=typeof(GameCore).GetMethod(
                    "RegionalBattleReachableTiles",flags);
                var footFn=typeof(GameCore).GetMethod("RegionalBattleFoot",flags);
                Check(reachableFn!=null&&footFn!=null,
                    "Authentic regional spawn uses a reachable-tile search");
                bool[] reachable=(bool[])reachableFn.Invoke(g,null);
                Point origin=g.Position;
                for(int rank=0;rank<4;rank++)
                {
                    Point foot=(Point)footFn.Invoke(g,new object[]{rank,reachable});
                    Check(foot.x>=36f&&g.CanStand(foot)&&
                        (foot-origin).Length>=4.5f&&
                        g.NavigationPathFrom(origin,foot,true).Count>0,
                        "Blocked old arena retargets a real accessible battle foot");
                    foreach(Enemy existing in g.enemies)
                        Check((existing.Position-foot).Length>=2.5f,
                            "Fallback enemy keeps real combat separation");
                    foreach(WorldObject o in g.Map.objects)
                        Check((new Point(o.x,o.y)-foot).Length>=2f,
                            "No fallback enemy occupies a physical quest prop");
                    g.enemies.Add(new Enemy(95000+rank,rank%3,
                        foot.x,foot.y,75f));
                }
                Check(g.enemies.Count==4&&g.save.version==2,
                    "Four real distinct actors stage without changing saved schema");
                // A legitimate closed-gate test can also leave only a tiny
                // hero pocket with no room for any enemy at safe distance.
                // Enter and encounter staging must remain possible then.
                string[] sealedRows=new string[original.Length];
                for(int y=0;y<sealedRows.Length;y++)
                {
                    char[] cells=new string('#',original[y].Length).ToCharArray();
                    if(y>=18&&y<=22)
                        for(int x=41;x<=45;x++)cells[x]='.';
                    sealedRows[y]=new string(cells);
                }
                map.rows=sealedRows;
                var pocket=Fresh(data);
                pocket.mode="world";pocket.save.room=1;
                pocket.save.x=43.5f;pocket.save.y=20.5f;
                pocket.enemies.Clear();
                var spawnFn=typeof(GameCore).GetMethod(
                    "SpawnRegionalBaselineEnemies",flags);
                Check(spawnFn!=null&&pocket.CanStand(pocket.Position),
                    "Closed gate still permits actual hero footing");
                spawnFn.Invoke(pocket,new object[]{6,80f});
                Check(pocket.enemies.Count==0&&pocket.save.version==2,
                    "Sealed actor pocket safely skips impossible enemies without fake wall spawns");
            }
            finally{map.rows=original;}
        }

        public static void AuthoredEnemySpawnsAndTideRecoveryAvoidWideGuardianBodies(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[24];
                for(int y=0;y<24;y++)
                {
                    char[] line=new string('.',24).ToCharArray();
                    for(int x=0;x<24;x++)
                        if(x<2||x>=22||y<2||y>=22)line[x]='#';
                    rows[y]=new string(line);
                }
                data.maps[0]=new MapData{id=0,spawnX=15.5f,spawnY=10.5f,
                    rows=rows,objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.x=15.5f;g.save.y=10.5f;
                var create=typeof(GameCore).GetMethod("CreatePlacedEnemy",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var uncrowd=typeof(GameCore).GetMethod("UncrowdedEnemyFoot",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(create!=null&&uncrowd!=null,
                    "Real authored spawn and tide relocation entrypoints are present");
                var guardian=new Enemy(85901,2,10.5f,10.5f,600,true);
                g.enemies.Add(guardian);
                Point near=new Point(11.6f,10.5f);
                Check(g.CanStand(near)&&
                    (near-guardian.Position).Length>1f&&
                    (near-guardian.Position).Length<1.4f,
                    "Legitimate map foot can still be deep inside the guardian's actual body");
                var recruit=(Enemy)create.Invoke(g,new object[]{
                    85902,0,near,120f,false});
                Check(g.CanStand(recruit.Position)&&
                    (recruit.Position-guardian.Position).Length>=1.4f,
                    "A fresh ordinary combatant emerges outside the real guardian rather than inside its published torso");
                g.enemies.Add(recruit);
                var restored=(Point)uncrowd.Invoke(g,new object[]{
                    new Point(11.65f,10.5f),null,false,false});
                Check(g.CanStand(restored)&&
                    (restored-guardian.Position).Length>=1.4f&&
                    (restored-recruit.Position).Length>=.95f,
                    "A legacy or tide-recovered foot is locally repaired without merging with the living Boss or newly spawned raider");
                Point originalValid=new Point(12.5f,10.5f);
                Point unchanged=(Point)uncrowd.Invoke(g,new object[]{
                    originalValid,null,false,false});
                Check((unchanged-originalValid).Length<.0001f,
                    "Already uncrowded authored feet remain unchanged");
                g.enemies.Clear();
                var defender=new Enemy(85903,0,10.5f,10.5f,150f);
                g.enemies.Add(defender);
                var newGuardian=(Enemy)create.Invoke(g,new object[]{
                    85904,2,new Point(11.6f,10.5f),650f,true});
                Check(newGuardian.boss&&
                    (newGuardian.Position-defender.Position).Length>=1.4f&&
                    g.CanStand(newGuardian.Position),
                    "A newly authored Boss must also spawn outside ordinary defenders rather than use small-enemy restoration clearance");
                Check(g.save.version==2,
                    "Spawn repair and rescue relocation change no serialized slot fields");
            }
            finally{data.maps[0]=original;}
        }

        public static void OrdinaryEnemiesActuallyClearBossBodyDuringPursuit(Campaign data)
        {
            MapData old=data.maps[0];
            try
            {
                string[] rows=new string[26];
                for(int y=0;y<26;y++)
                {
                    char[] line=new string('.',26).ToCharArray();
                    for(int x=0;x<26;x++)
                        if(x<2||x>=24||y<2||y>=24)line[x]='#';
                    rows[y]=new string(line);
                }
                data.maps[0]=new MapData{id=0,spawnX=15.5f,spawnY=10.5f,
                    rows=rows,objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.x=15.5f;g.save.y=10.5f;
                g.invulnerable=100f;
                var hunter=new Enemy(84991,0,9.07f,10.5f,300f)
                    {timer=99f};
                var boss=new Enemy(84992,2,10.5f,10.5f,700f,true)
                    {timer=99f,stun=99f};
                g.enemies.Add(hunter);g.enemies.Add(boss);
                var check=typeof(GameCore).GetMethod("EnemyCrowdStepClear",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(check!=null&&g.CanStand(hunter.Position)&&
                    g.CanStand(boss.Position),
                    "Live Boss crowd collision uses genuine walkable combat feet");
                Point forward=hunter.Position+new Point(.105f,0);
                Check((hunter.Position-boss.Position).Length>1.4f&&
                    (forward-boss.Position).Length<1.4f&&
                    !(bool)check.Invoke(g,new object[]{hunter,hunter.Position,forward}),
                    "Full-width Boss body blocks the first real hunter footstep before its outline clips the guardian");
                g.Tick(.05f,new Point());
                Check(hunter.moving&&Math.Abs(hunter.y-10.5f)>.04f&&
                    hunter.x<9.08f&&
                    (hunter.Position-boss.Position).Length>=1.4f-.001f,
                    "Production melee hunter genuinely flanks the Boss-sized body on open ground");
                for(int i=0;i<18;i++)
                {
                    g.Tick(.05f,new Point());
                    Check((hunter.Position-boss.Position).Length>=1.4f-.001f&&
                        g.CanStand(hunter.Position),
                        "Every actual moving frame preserves the guardian's wide living collision radius");
                }
                // A defeated/moved guardian must not create a permanent
                // ghost-body obstacle or a second saved navigation schema.
                g.enemies.Remove(boss);
                Point prior=hunter.Position;
                for(int i=0;i<10;i++)g.Tick(.05f,new Point());
                Check((hunter.Position-g.Position).Length<
                    (prior-g.Position).Length-.3f&&
                    g.save.version==2,
                    "Removing the guardian immediately restores direct pursuit and old-save compatibility");
            }
            finally{data.maps[0]=old;}
        }

        public static void LivingCrowdSweepsRejectGrazingMidstepBodyContact(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[24];
                for(int y=0;y<24;y++)
                {
                    char[] line=new string('.',24).ToCharArray();
                    for(int x=0;x<24;x++)
                        if(x<2||x>=22||y<2||y>=22)line[x]='#';
                    rows[y]=new string(line);
                }
                data.maps[0]=new MapData{id=0,spawnX=14.5f,spawnY=11.2185f,
                    rows=rows,objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.x=14.5f;g.save.y=11.2185f;
                g.invulnerable=100f;
                var hunter=new Enemy(83912,0,10.45f,11.2185f,100)
                    {timer=99f};
                var guard=new Enemy(83913,0,10.5f,10.5f,100)
                    {stun=99f,timer=99f};
                g.enemies.Add(hunter);g.enemies.Add(guard);
                var clear=typeof(GameCore).GetMethod("EnemyCrowdStepClear",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(clear!=null&&g.CanStand(hunter.Position)&&
                    g.CanStand(guard.Position),
                    "Real enemy-body tangency occurs on legal world ground");
                Point start=hunter.Position;
                Point finish=start+new Point(.105f,0f);
                float radius=.72f;
                Check((start-guard.Position).Length>radius&&
                    (finish-guard.Position).Length>radius&&
                    !(bool)clear.Invoke(g,new object[]{hunter,start,finish}),
                    "Both endpoints fit outside a raider's circle, but the true swept segment crosses a narrow living-body chord");
                g.Tick(.05f,new Point());
                Check(hunter.moving&&hunter.y>start.y+.04f&&
                    Math.Abs(hunter.x-start.x)<.01f&&
                    g.CanStand(hunter.Position),
                    "Production pursuit turns onto the actual clear lateral step rather than ghosting through the tangent body");
                g.enemies.Remove(guard);
                hunter.x=start.x;hunter.y=start.y;
                g.Tick(.05f,new Point());
                Check(hunter.x>start.x+.04f&&
                    Math.Abs(hunter.y-start.y)<.01f,
                    "As soon as the other body is gone normal forward pursuit resumes on its original stride");
                guard.x=10.5f;guard.y=10.5f;
                g.enemies.Add(guard);
                Point inside=new Point(10.5f,10.55f);
                Point escape=new Point(10.5f,10.7f);
                Check((bool)clear.Invoke(g,new object[]{hunter,inside,escape})&&
                    !(bool)clear.Invoke(g,new object[]{hunter,escape,inside}),
                    "An already-overlapping enemy can physically escape but cannot drive deeper into that body");
                Check(g.save.version==2,
                    "Swept crowd steering introduces no persistent save fields");
            }
            finally{data.maps[0]=original;}
        }

        public static void BossWideMeleeCoverProtectsHeroUntilARealFlankOpens(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                var rows=new string[20];
                for(int y=0;y<20;y++)
                {
                    char[] line=new string('.',20).ToCharArray();
                    for(int x=0;x<20;x++)
                        if(x<2||y<2||x>=18||y>=18)line[x]='#';
                    rows[y]=new string(line);
                }
                data.maps[0]=new MapData{id=0,spawnX=11.9f,spawnY=10.5f,
                    rows=rows,objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();
                g.save.x=11.9f;g.save.y=10.5f;g.invulnerable=0f;
                var hunter=new Enemy(83231,0,10.5f,10.5f,180)
                    {timer=.001f};
                var guardian=new Enemy(83232,2,11.3f,11.3f,500,true)
                    {stun=100f,timer=100f};
                g.enemies.Add(hunter);g.enemies.Add(guardian);
                var melee=typeof(GameCore).GetMethod("EnemyMeleeLaneClear",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(melee!=null&&g.CanStand(guardian.Position)&&
                    !(bool)melee.Invoke(g,new object[]{hunter,g.Position}),
                    "Guardian silhouette (0.9-tile radius) intercepts a grazing melee lane its narrower center-only test misses");
                g.Tick(.05f,new Point());
                Check(hunter.state==0&&hunter.moving&&
                    g.CanStand(hunter.Position),
                    "Ordinary rear hunter seeks a real flank rather than winding a melee hit through Boss shoulder");
                // A Boss moving away opens the actual player's original
                // timed windup, without any save, reward or permanent flag.
                guardian.y=13f;
                hunter.x=10.5f;hunter.y=10.5f;
                hunter.state=0;hunter.timer=.001f;
                hunter.stun=hunter.releaseTime=hunter.hitTime=0f;
                g.Tick(.05f,new Point());
                Check(hunter.state==1&&
                    (bool)melee.Invoke(g,new object[]{hunter,g.Position}),
                    "Rear melee regains its normal committed warning immediately when Boss cover moves");
                // A wide living Boss shoulder entering AFTER the windup
                // still protects at actual contact. Keep the action clock;
                // never mint a false free hit or an unreadable fake strike.
                guardian.x=11.3f;guardian.y=11.3f;
                float beforeHealth=g.save.health;
                for(int i=0;i<16;i++)g.Tick(.05f,new Point());
                Check(hunter.state==0&&g.save.health==beforeHealth&&
                    g.effects.Exists(fx=>fx.kind=="number"&&
                        fx.text==g.L("队友挡路","ALLY BLOCK")),
                    "Genuine wide Boss cover blocks a late melee contact and produces one real ally-block cue");
                guardian.x=13.2f;guardian.y=10.5f;
                hunter.x=10.5f;hunter.y=10.5f;hunter.state=0;
                hunter.timer=.001f;hunter.stun=hunter.hitTime=hunter.releaseTime=0f;
                Check((bool)melee.Invoke(g,new object[]{hunter,g.Position}),
                    "Boss standing BEHIND the player cannot wrongly claim the first melee contact");
                g.Tick(.05f,new Point());
                Check(hunter.state==1&&g.save.version==2,
                    "Behind-hero Boss cover does not cancel a fair ordinary hunter strike or migrate old saves");
            }
            finally{data.maps[0]=original;}
        }

        public static void BacklineMeleeCannotStrikeThroughLivingEnemyCover(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[24];
                for(int y=0;y<24;y++)
                {
                    char[] cells=new string('.',24).ToCharArray();
                    for(int x=0;x<24;x++)
                        if(x<2||y<2||x>=22||y>=22)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{id=0,spawnX=12.1f,spawnY=10.5f,
                    rows=rows,objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);
                g.enemies.Clear();g.save.x=12.1f;g.save.y=10.5f;
                g.invulnerable=0f;
                var rear=new Enemy(92501,0,9.8f,10.5f,200);
                var front=new Enemy(92502,0,10.9f,10.5f,200);
                rear.timer=.001f;front.timer=100f;front.stun=100f;
                g.enemies.Add(rear);g.enemies.Add(front);
                g.Tick(.05f,new Point());
                Check(rear.state==0 && rear.moving,
                    "A backline ordinary melee enemy must pursue a real flank, not start a lunge through living cover");
                for(int i=0;i<40 && Math.Abs(rear.y-10.5f)<.35f;i++)
                    g.Tick(.05f,new Point());
                Check(Math.Abs(rear.y-10.5f)>.35f&&
                    (rear.Position-front.Position).Length>=.7f,
                    "The rear enemy actually travels around the living frontline on open physical ground");
                // Once cover leaves, the same ordinary hunter regains its
                // original attack at the already-reachable hero.
                g.enemies.Remove(front);
                rear.x=10.9f;rear.y=10.5f;rear.state=0;
                rear.timer=.001f;rear.stun=rear.hitTime=rear.releaseTime=0f;
                g.Tick(.05f,new Point());
                Check(rear.state==1&&
                    (rear.target-g.Position).Length<.001f,
                    "Removing the cover immediately restores the true hero-targeted melee windup");
                // An ally can step into the lane AFTER the real warning is
                // committed. Recheck cover at contact rather than resolving
                // a free strike through the moving living defender.
                front.x=11.5f;front.y=10.5f;front.stun=100f;
                g.enemies.Add(front);
                float health=g.save.health;
                for(int i=0;i<16;i++)g.Tick(.05f,new Point());
                Check(g.save.health==health&&rear.state==0,
                    "A living teammate crossing a released melee lane blocks actual player HP damage without cancelling the original attack clock");
                Check(g.effects.FindAll(fx=>fx.kind=="number"&&
                    fx.text.Contains("队友挡路")).Count==1,
                    "One real blocked contact gives the player a readable, non-spamming combat cue");
                Check(g.save.version==2,
                    "Melee cover is transient combat geometry and does not migrate existing saves");
                // Genuine zero-length melee contact is not occluded by a
                // phantom unit: this occurs in real Abyss defeat resolution
                // and when an ambusher shares the protected traveler foot.
                g.enemies.Remove(front);
                rear.x=g.Position.x;rear.y=g.Position.y;rear.state=0;
                rear.timer=.001f;rear.stun=rear.hitTime=rear.releaseTime=0f;
                g.Tick(.05f,new Point());
                Check(rear.state==1,
                    "Overlap-at-contact preserves real melee windup, not a false body obstruction");
            }
            finally { data.maps[0]=original; }
        }

        public static void ArcherRetreatSidestepsLivingFrontlineCover(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[24];
                for(int y=0;y<24;y++)
                {
                    char[] cells=new string('.',24).ToCharArray();
                    for(int x=0;x<24;x++)
                        if(x<2||y<2||x>=22||y>=22)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{id=0,spawnX=11.8f,spawnY=10.5f,
                    rows=rows,objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);
                g.enemies.Clear();
                g.save.x=11.8f;g.save.y=10.5f;g.invulnerable=100f;
                var archer=new Enemy(91903,1,10.5f,10.5f,100);
                var ally=new Enemy(91904,0,9.75f,10.5f,100);
                archer.timer=100f;ally.timer=100f;ally.stun=100f;
                g.enemies.Add(archer);g.enemies.Add(ally);
                g.Tick(.05f,new Point());
                Check(archer.backpedaling&&archer.moving&&
                    Math.Abs(archer.y-10.5f)>.01f&&
                    Math.Abs(archer.x-10.5f)<.01f&&
                    (archer.Position-ally.Position).Length>.75f,
                    "Ranged enemy facing a close hero actually retreats sideways around the living ally instead of backstepping through its body");
                Check(archer.facing.x>.9f && g.save.version==2,
                    "Retreat still faces the real combat target without mutating the version-two save");
                g.enemies.Remove(ally);
                archer.x=10.5f;archer.y=10.5f;
                g.Tick(.05f,new Point());
                Check(archer.backpedaling&&archer.x<10.49f&&
                    Math.Abs(archer.y-10.5f)<.01f,
                    "With the blocker gone the same ordinary archer immediately resumes the original straight backstep");
            }
            finally{data.maps[0]=original;}
        }

        public static void LivingEnemyCrowdsFlankAndDoNotWalkThroughTeammates(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                string[] rows=new string[24];
                for(int y=0;y<rows.Length;y++)
                {
                    char[] cells=new string('.',24).ToCharArray();
                    for(int x=0;x<24;x++)
                        if(x<2||y<2||x>=22||y>=22)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{id=0,spawnX=15.5f,spawnY=10.5f,
                    rows=rows,objects=new WorldObject[0],decor=new Decoration[0]};
                var g=Fresh(data);
                g.enemies.Clear();g.save.x=15.5f;g.save.y=10.5f;
                g.invulnerable=100;
                var mover=new Enemy(91901,0,8.5f,10.5f,200);
                var ally=new Enemy(91902,0,9.3f,10.5f,200);
                mover.timer=100f;ally.timer=100f;ally.stun=100f;
                g.enemies.Add(mover);g.enemies.Add(ally);
                g.Tick(.05f,new Point());
                Check(mover.moving&&Math.Abs(mover.y-10.5f)>.01f &&
                    mover.x<8.55f &&
                    (mover.Position-ally.Position).Length>.79f,
                    "A melee pursuer with a living ally directly ahead side-steps on real walkable ground instead of phasing into the ally");
                for(int frame=0;frame<75&&mover.x<=9.8f;frame++)
                    g.Tick(.05f,new Point());
                Check(mover.x>9.8f&&g.CanStand(mover.Position)&&
                    (mover.Position-ally.Position).Length>=.71f,
                    "Persistent melee pursuit rounds the ally and makes actual forward progress without overlapping");
                // Narrow stone lane: neither lateral foot is walkable.
                for(int y=2;y<22;y++)
                    if(y!=10)rows[y]=new string('#',24);
                mover.x=8.5f;mover.y=10.5f;ally.x=9.3f;ally.y=10.5f;
                mover.stun=0f;mover.state=0;mover.timer=100f;
                for(int frame=0;frame<20;frame++)g.Tick(.05f,new Point());
                Check(mover.x<=8.66f&&
                    (mover.Position-ally.Position).Length>=.71f&&
                    g.CanStand(mover.Position),
                    "In a one-tile corridor a live enemy cannot pass through its teammate or step into solid masonry");
                g.enemies.Remove(ally);
                float stoppedX=mover.x;
                for(int frame=0;frame<12;frame++)g.Tick(.05f,new Point());
                Check(mover.x>stoppedX+.3f&&g.CanStand(mover.Position),
                    "Removing the physical blocker immediately releases pursuit without fake teleporting or a retained traffic lock");
                Check(g.save.version==2,
                    "Crowd steering adds no new stored fields or save migration");
            }
            finally { data.maps[0]=original; }
        }

        public static void InvalidPursuitTargetsNeverPoisonLiveMovement(Campaign data)
        {
            MapData original = data.maps[0];
            try
            {
                string[] rows = new string[16];
                for(int y=0;y<16;y++)
                {
                    char[] tiles=new string('.',16).ToCharArray();
                    for(int x=0;x<16;x++)
                        if(x<2||x>=14||y<2||y>=14)tiles[x]='#';
                    rows[y]=new string(tiles);
                }
                data.maps[0]=new MapData {
                    id=0,spawnX=5.5f,spawnY=6.5f,rows=rows,
                    objects=new WorldObject[0],decor=new Decoration[0]
                };
                var g=new GameCore(data);
                Point from=new Point(5.5f,6.5f);
                Point[] bad={
                    new Point(float.NaN,6.5f),
                    new Point(float.PositiveInfinity,6.5f),
                    new Point(7.5f,float.NegativeInfinity),
                    new Point(float.MaxValue,6.5f),
                    new Point(100000000f,6.5f),
                    new Point(-.5f,6.5f)
                };
                foreach(Point target in bad)
                {
                    Point heading=g.DirectionTo(from,target);
                    Check(heading.Length==0f && heading.x==0f && heading.y==0f,
                        "Invalid or retired off-map pursuit targets must not move an actual enemy or companion");
                }
                Point after=g.DirectionTo(from,new Point(9.5f,6.5f));
                Check(after.x>.99f && Math.Abs(after.y)<.001f,
                    "Valid on-map pursuit still travels straight immediately after rejected targets");
            }
            finally { data.maps[0]=original; }
        }

        public static void NarrowWallCornersForceRealPursuitDetours(Campaign data)
        {
            // The real collision footprint brushes this single masonry tile
            // for less than the old .4-world-unit pursuit sightline stride.
            // Old steering returns a falsely clear diagonal and walks into it.
            MapData original = data.maps[0];
            try
            {
                string[] rows = new string[25];
                for (int y = 0; y < rows.Length; y++)
                {
                    char[] cells = new string('.', 25).ToCharArray();
                    for (int x = 0; x < 25; x++)
                        if (x < 2 || x >= 23 || y < 2 || y >= 23)
                            cells[x] = '#';
                    if (y == 10) cells[9] = '#';
                    rows[y] = new string(cells);
                }
                data.maps[0] = new MapData {
                    id = 0, spawnX = 7.5f, spawnY = 13.5f,
                    rows = rows, objects = new WorldObject[0],
                    decor = new Decoration[0]
                };
                var g = new GameCore(data);
                Point origin = new Point(7.31417f, 13.53110f);
                Point target = new Point(12.06023f, 9.55577f);
                Point direct = (target - origin).Normalized;
                Check(g.CanStand(origin) && g.CanStand(target),
                    "Both ends of the corner-pursuit fixture must have physical feet");
                bool crossesWall = false;
                float span = (target - origin).Length;
                for (float distance = .06f; distance < span; distance += .06f)
                    if (!g.CanStand(origin + direct * distance))
                    { crossesWall = true; break; }
                Check(crossesWall,
                    "An unobstructed endpoint pair must still intersect the narrow solid corner");
                Point detour = g.DirectionTo(origin, target);
                Check(detour.x > .95f && Math.Abs(detour.y) < .1f,
                    "Live pursuer takes the legal eastern first waypoint, not a diagonal through masonry");
                for (int step = 1; step <= 8; step++)
                    Check(g.CanStand(origin + detour * (.12f * step)),
                        "The actual first pursuit step remains physically walkable");
                char[] opened = rows[10].ToCharArray(); opened[9] = '.';
                rows[10] = new string(opened);
                Point reopened = g.DirectionTo(origin, target);
                Check((reopened - direct).Length < .001f,
                    "Removing the obstruction restores real direct pursuit immediately without a stale cached route");
            }
            finally { data.maps[0] = original; }
        }

        public static void SealedPursuitRoutesToLegalFrontierAndReopensImmediately(Campaign data)
        {
            // Keep all authored campaign maps and story objects unchanged.
            // Temporarily substitute a miniature blocked arena for a real
            // GameCore to exercise the production DirectionTo + Fits path.
            MapData original = data.maps[0];
            try
            {
                string[] rows = new string[16];
                for (int y = 0; y < rows.Length; y++)
                {
                    char[] cells = new string('.', 16).ToCharArray();
                    for (int x = 0; x < 16; x++)
                        if (x < 2 || x >= 14 || y < 2 || y >= 14) cells[x] = '#';
                    if (y >= 2 && y < 14) cells[7] = '#';
                    rows[y] = new string(cells);
                }
                data.maps[0] = new MapData {
                    id = 0, spawnX = 4.5f, spawnY = 8.5f,
                    rows = rows, objects = new WorldObject[0],
                    decor = new Decoration[0]
                };
                GameCore g = new GameCore(data);
                Point target = new Point(10.5f, 8.5f);
                Point approach = g.DirectionTo(new Point(4.5f, 8.5f), target);
                Check(approach.x > .9f && Math.Abs(approach.y) < .1f,
                    "Sealed target first draws pursuit toward the nearest reachable legal frontier");
                Point waiting = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(waiting.Length < .001f,
                    "At a sealed frontier the pursuer must not walk straight into the wall or jitter");
                Check(g.NavigationPath(target).Count == 0,
                    "Navigation HUD cannot draw a physically false route through a sealed wall");

                char[] doorway = rows[12].ToCharArray(); doorway[7] = '.';
                rows[12] = new string(doorway);
                Point detour = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(detour.y > .9f && Math.Abs(detour.x) < .1f,
                    "Opening a distant legal corridor immediately makes AI seek its detour");
                for (int pass = 0; pass < 4; pass++)
                {
                    List<Point> route = g.NavigationPath(target);
                    Check(route.Count > 6 &&
                        (route[route.Count - 1] - target).Length < 1.2f,
                        "Shared HUD and combat BFS workspace must not retain stale parent links, pass " + pass);
                    Point again = g.DirectionTo(new Point(6.5f, 8.5f), target);
                    Check(again.y > .9f,
                        "Shared BFS generations must never corrupt the following enemy detour");
                }
                char[] crossing = rows[8].ToCharArray(); crossing[7] = '.';
                rows[8] = new string(crossing);
                Point direct = g.DirectionTo(new Point(6.5f, 8.5f), target);
                Check(direct.x > .9f && Math.Abs(direct.y) < .1f,
                    "Opening a direct bridge immediately replaces the detour without stale navigation");
            }
            finally { data.maps[0] = original; }
        }

        public static void RealIncomingHitRetiresUnspentDodgeAndLeavesSafeInput(Campaign data)
        {
            GameCore g=Fresh(data);g.enemies.Clear();
            g.save.energy=100f;g.invulnerable=0f;
            g.dashCooldown=.10f;
            g.Dash(new Point(0,-1));
            Check(g.DodgeQueued&&g.dashTime<=0f&&
                Math.Abs(g.save.energy-100f)<.001f,
                "Queued late dodge must hold stamina until a real accepted mobility action");
            var hurt=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hurt!=null,"Production Hero Hurt transaction must exist");
            g.invulnerable=1f;
            hurt.Invoke(g,new object[]{7f,"shot",null});
            Check(g.DodgeQueued&&g.dashTime<=0f,
                "An avoided arrow cannot cancel an accepted cooldown-buffered dodge");
            g.invulnerable=0f;
            float health=g.save.health;
            hurt.Invoke(g,new object[]{7f,"environment",null});
            Check(g.save.health<health&&g.playerHitTime>0f&&
                !g.DodgeQueued&&g.dashTime<=0f,
                "Genuine damage immediately retires unspent dodge before displaying Hit/recoil");
            float stamina=g.save.energy;
            Advance(g,.2f);
            Check(g.dashTime<=0f&&!g.DodgeQueued&&
                g.save.energy>=stamina,
                "Recovery and cooldown completion cannot start a ghost dodge or spend stamina");

            GameCore harmless=Fresh(data);harmless.enemies.Clear();
            harmless.save.energy=100f;
            harmless.dashCooldown=.09f;
            harmless.Dash(new Point(0,-1));
            hurt.Invoke(harmless,new object[]{0f,"environment",null});
            Check(harmless.DodgeQueued,
                "Zero-power warnings cannot consume defensive input");
            Advance(harmless,.091f);
            Check(harmless.dashTime>0f&&
                harmless.invulnerable>0f&&
                harmless.save.energy<100f,
                "After harmless contact a legitimate queued dodge still executes once");
        }

        public static void RepeatedLateDodgeCannotResetDeadlineOrDirection(Campaign data)
        {
            foreach(float step in new[]{.01f,.02f,.05f})
            {
                GameCore g=Fresh(data);g.enemies.Clear();
                g.save.energy=100f;
                g.dashCooldown=.12f;
                g.Dash(new Point(0,-1));
                Check(g.DodgeQueued&&g.dashTime<=0&&g.save.energy==100f,
                    "Initial cooldown-bound dodge must remain an unpaid real intention");
                Check((g.DodgeQueuedDirection-new Point(0,-1)).Length<.001f,
                    "World cue exposes the genuine accepted escape lane before any stamina or immunity starts");
                var pending=typeof(GameCore).GetField("bufferedDodge",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(pending!=null,"Real private dodge intent must remain observable to the test");
                object first=pending.GetValue(g);
                var lifetime=first.GetType().GetField("remaining");
                float initial=(float)lifetime.GetValue(first);
                g.Tick(step,new Point());
                float after=(float)lifetime.GetValue(first);
                Check(after<initial&&after>0f&&g.DodgeQueued,
                    "Buffered dodge's original deadline must decay with real gameplay time");
                g.SetAim(new Point(1f,0f));
                g.Dash(new Point(0,1));
                Check(System.Object.ReferenceEquals(first,pending.GetValue(g))&&
                    Math.Abs((float)lifetime.GetValue(first)-after)<.00001f,
                    "Repeated SPACE cannot refresh buffer expiry or replace the initial escape heading");
                Check((g.DodgeQueuedDirection-new Point(0,-1)).Length<.001f,
                    "Directional dodge preview may not rotate to a later held key or pointer aim");
                for(int i=0;i<30&&g.dashTime<=0f;i++)
                    g.Tick(step,new Point());
                Check(g.dashTime>0f&&!g.DodgeQueued&&
                    g.save.energy>=79.9f&&g.save.energy<=80.1f,
                    "Buffered dodge must eventually launch exactly once at true cooldown readiness");
                Check(g.DodgeQueuedDirection.Length<.001f,
                    "Dodge intent marker must retire as soon as the real movement begins");
                Point heading=(Point)typeof(GameCore).GetField(
                    "dashDirection",System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).GetValue(g);
                Check(heading.y<-.99f&&Math.Abs(heading.x)<.01f,
                    "Actual dash must preserve first physical tap direction, not last auto-repeat");
                float spent=g.save.energy;
                Advance(g,.40f);
                Check(!g.DodgeQueued&&g.dashTime<=0f&&
                    g.save.energy>=spent,
                    "Repeated dodge input cannot later fire a second unrequested dash");
            }
        }

        public static void BufferedDodgeKeepsDirectionAndCommitsOnce(Campaign data)
        {
            GameCore g=Fresh(data);g.enemies.Clear();
            g.save.x=12;g.save.y=14;g.save.energy=100;
            g.dashCooldown=.10f;
            g.Dash(new Point(0,-1));
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Early valid dodge cannot spend stamina or start invulnerability");
            g.SetAim(new Point(1,0));
            g.Tick(.04f,new Point());
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Queued dodge started before actual cooldown completion");
            Advance(g,.061f);
            Check(g.dashTime>.17f&&g.invulnerable>.30f&&
                Math.Abs(g.save.energy-80f)<.001f,
                "Exactly one buffered dodge must start on cooldown readiness");
            Point savedDirection=(Point)typeof(GameCore).GetField(
                "dashDirection",System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic).GetValue(g);
            Check(savedDirection.y<-.99f&&Math.Abs(savedDirection.x)<.01f,
                "Deferred dodge must use original input, not later aim direction");
            g.Tick(.02f,new Point());
            Check(g.save.energy>80f&&g.save.energy<82f,
                "Buffered dodge may not double charge energy on later ticks");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.Attack(new Point(1,0));
            Check(g.playerAction!=null,"Dodge interrupt requires actual attack");
            g.dashCooldown=.07f;
            g.Dash(new Point(0,1));
            Check(g.playerAction!=null,
                "Queued cooldown dodge cancelled original windup too early");
            Advance(g,.071f);
            Check(g.dashTime>.17f&&g.playerAction==null,
                "Ready buffered dodge must cancel attack and begin real mobility");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(-1,0));
            g.mode="inventory";g.mode="world";
            Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Closing menu must retire previously buffered dodge");
            g.dashCooldown=.45f;g.Dash(new Point(-1,0));
            Advance(g,.46f);
            Check(g.dashTime<=0f,
                "Dodge input well outside last 140ms cannot execute later");
            g.save.energy=0;g.dashCooldown=.09f;
            g.Dash(new Point(-1,0));
            g.save.energy=100;Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100f,
                "Dodge input without enough energy cannot create a future free action");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.08f;g.Dash(new Point(0,-1));
            g.Attack(new Point(1,0));
            Check(g.playerAction==null&&g.save.energy==100,
                "Held basic attack cannot spend resources before a queued dodge");
            Advance(g,.081f);
            Check(g.dashTime>.17f&&g.save.energy>=80f&&
                g.save.energy<80.1f,
                "Waiting dodge must outrank a simultaneous ordinary attack");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(0,-1));
            g.Skill(new Point(1,0));
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                g.save.energy==60,
                "Newer deliberate Q must override queued dodge without double payment");
            Advance(g,.10f);
            Check(g.dashTime<=0&&g.playerAction!=null&&
                g.playerAction.kind=="skill",
                "A superseded dodge cannot cancel a newer skill windup");

            g=Fresh(data);g.enemies.Clear();g.save.energy=100;
            g.dashCooldown=.09f;g.Dash(new Point(0,-1));
            g.ClearBufferedCombatInputs();
            Advance(g,.10f);
            Check(g.dashTime<=0f&&g.save.energy==100,
                "Application pause/focus must retire all queued combat inputs");
        }

        public static void DodgeWaitsForHitRecoveryAndCooldown(Campaign data)
        {
            var hitOnly=Fresh(data);hitOnly.enemies.Clear();
            hitOnly.save.energy=100f;hitOnly.invulnerable=0f;
            hitOnly.dashCooldown=0f;hitOnly.playerHitTime=.08f;
            Check(hitOnly.CanBufferDodge,
                "A short real hit recovery exposes a bounded dodge buffer when the cooldown is already ready");
            hitOnly.Dash(new Point(1,0));
            Check(hitOnly.DodgeQueued&&hitOnly.dashTime<=0f&&
                hitOnly.save.energy==100f&&hitOnly.invulnerable<=0f&&
                hitOnly.HeroAnimationState==5&&hitOnly.PlayerPose==10,
                "A dodge tap during hit recovery must keep both production render paths on the actual Hit pose");
            hitOnly.Tick(.04f,new Point());
            Check(hitOnly.playerHitTime>0f&&hitOnly.DodgeQueued&&
                hitOnly.dashTime<=0f&&hitOnly.save.energy==100f&&
                hitOnly.invulnerable<=0f,
                "The buffered dodge cannot move, spend stamina or grant i-frames before the hit pose ends");
            hitOnly.Tick(.05f,new Point());
            Check(hitOnly.playerHitTime<=.00001f&&hitOnly.dashTime>.17f&&
                hitOnly.save.energy<81f&&hitOnly.save.energy>=80f&&
                hitOnly.invulnerable>.30f&&hitOnly.HeroAnimationState==4&&
                hitOnly.PlayerPose==8,
                "A short valid hit-recovery dodge dispatches once after the real recovery boundary");

            var both=Fresh(data);both.enemies.Clear();
            both.save.energy=100f;both.invulnerable=0f;
            both.dashCooldown=.10f;both.playerHitTime=.08f;
            both.Dash(new Point(0,-1));
            Check(both.DodgeQueued&&both.dashTime<=0f&&both.save.energy==100f,
                "Cooldown and hit recovery share one bounded queued dodge intent");
            both.Tick(.04f,new Point());
            both.Tick(.04f,new Point());
            Check(both.playerHitTime<=.00001f&&both.dashCooldown>.01f&&
                both.dashTime<=0f&&both.save.energy==100f&&
                both.invulnerable<=0f,
                "Finishing hit recovery alone cannot bypass the remaining dash cooldown");
            both.Tick(.03f,new Point());
            Check(both.dashTime>.17f&&both.invulnerable>.30f&&
                both.save.energy<81f&&both.save.energy>=80f&&
                !both.DodgeQueued,
                "The same queued input dispatches only after BOTH hit recovery and cooldown finish");

            var frozen=Fresh(data);frozen.enemies.Clear();
            frozen.save.energy=100f;frozen.invulnerable=0f;
            frozen.dashCooldown=0f;frozen.hitStop=.08f;
            Check(frozen.CanBufferDodge,
                "A short hit-stop exposes the same bounded input buffer without starting the dodge early");
            frozen.Dash(new Point(0,-1));
            Check(frozen.DodgeQueued&&frozen.dashTime<=0f&&
                frozen.save.energy==100f&&frozen.invulnerable<=0f,
                "A dodge requested during hit-stop remains unpaid until simulation resumes");
            frozen.Tick(.04f,new Point());
            Check(frozen.hitStop>0f&&frozen.DodgeQueued&&
                frozen.dashTime<=0f&&frozen.save.energy==100f&&
                frozen.invulnerable<=0f,
                "A frozen simulation cannot dispatch the queued dodge or advance its input deadline");
            frozen.Tick(.05f,new Point());
            Check(frozen.hitStop<=.00001f&&frozen.dashTime>.17f&&
                frozen.save.energy<81f&&frozen.save.energy>=80f&&
                frozen.invulnerable>.30f,
                "The queued dodge starts once after hit-stop has fully elapsed");

            var tooEarly=Fresh(data);tooEarly.enemies.Clear();
            tooEarly.save.energy=100f;tooEarly.invulnerable=0f;
            tooEarly.dashCooldown=0f;tooEarly.playerHitTime=.35f;
            tooEarly.Dash(new Point(1,0));
            Check(!tooEarly.DodgeQueued&&tooEarly.dashTime<=0f&&
                tooEarly.save.energy==100f&&tooEarly.invulnerable<=0f,
                "A hit-recovery input outside the 140ms window is rejected, not stored for a later free dodge");
            Advance(tooEarly,.4f);
            Check(tooEarly.dashTime<=0f&&tooEarly.save.energy==100f&&
                tooEarly.invulnerable<=0f,
                "An expired early dodge press cannot execute after recovery ends");

            var longFreeze=Fresh(data);longFreeze.enemies.Clear();
            longFreeze.save.energy=100f;longFreeze.invulnerable=0f;
            longFreeze.dashCooldown=0f;longFreeze.hitStop=.35f;
            longFreeze.Dash(new Point(1,0));
            Check(!longFreeze.DodgeQueued&&longFreeze.dashTime<=0f&&
                longFreeze.save.energy==100f&&longFreeze.invulnerable<=0f,
                "A hit-stop outside the 140ms window cannot store a future free dodge");
            Advance(longFreeze,.4f);
            Check(longFreeze.dashTime<=0f&&longFreeze.save.energy==100f&&
                longFreeze.invulnerable<=0f,
                "An expired early hit-stop dodge cannot start after the world unfreezes");
        }


        public static void LastMomentClassSkillCooldownPressQueuesOnceForAllClasses(Campaign data)
        {
            for(int profession=0;profession<3;profession++)
            {
                var g=Fresh(data,profession);g.enemies.Clear();
                g.save.energy=100;g.skillCooldown=.16f;
                Point aimed=new Point(0,-1f);
                g.SetAimTarget(new Point(23,15));
                g.Skill(aimed);
                Check(g.SkillQueued&&g.playerAction==null&&
                    Math.Abs(g.skillCooldown-.16f)<.0001f&&g.save.energy==100,
                    "Class "+profession+" accepts a late Q intent without paying or starting it prematurely");
                Advance(g,.1f);
                Check(g.SkillQueued&&g.playerAction==null&&g.skillCooldown>.05f&&
                    g.save.energy>=100-.001f,
                    "Class "+profession+" does not fire before the original cooldown expires");
                Advance(g,.07f);
                Check(!g.SkillQueued&&g.playerAction!=null&&
                    g.playerAction.kind=="skill"&&
                    g.playerAction.direction.y<-.99f&&
                    g.skillCooldown>3.4f&&g.skillCooldown<=3.5f&&
                    g.save.energy>59.9f&&g.save.energy<61f,
                    "Class "+profession+" dispatches exactly one Q at its legitimate readiness boundary; "+ 
                    "action="+(g.playerAction==null?"none":g.playerAction.kind)+
                    " cooldown="+g.skillCooldown+" energy="+g.save.energy+
                    " queued="+g.SkillQueued+" position="+g.Position.x+","+g.Position.y+
                    " standable="+g.CanStand(g.Position)+" mode="+g.mode+
                    " hit="+g.playerHitTime+" stop="+g.hitStop+
                    " dash="+g.dashTime+" blocked="+g.ActionTransitionBlocked+
                    " invuln="+g.invulnerable);
                ActorAction first=g.playerAction;
                g.Tick(.09f,new Point());
                Check(g.playerAction==first&&!g.SkillQueued&&
                    g.skillCooldown>3.2f&&g.save.energy>59.9f,
                    "Class "+profession+" does not double-charge or queue another skill after dispatch");
            }
            var tooEarly=Fresh(data,1);tooEarly.enemies.Clear();
            tooEarly.skillCooldown=.36f;tooEarly.save.energy=100f;
            tooEarly.Skill(new Point(0,1));
            Check(!tooEarly.SkillQueued&&tooEarly.playerAction==null&&
                tooEarly.save.energy==100,
                "A Q pressed outside the final 200ms cannot queue a future free cast");
            Advance(tooEarly,.40f);
            Check(!tooEarly.SkillQueued&&tooEarly.playerAction==null,
                "Cooldown expiry alone never synthesizes a rejected Q");
            var stamina=Fresh(data,1);stamina.enemies.Clear();
            stamina.skillCooldown=.12f;stamina.save.energy=10;
            stamina.Skill(new Point(1,0));
            Check(!stamina.SkillQueued&&stamina.save.energy==10,
                "A late cooldown Q without its real stamina cost is rejected immediately");
            var spam=Fresh(data,1);spam.enemies.Clear();
            spam.skillCooldown=.18f;spam.save.energy=100;
            spam.Skill(new Point(0,-1));
            Advance(spam,.10f);
            spam.SetAimTarget(new Point(25,17));
            spam.Skill(new Point(1,0));
            Advance(spam,.09f);
            Check(spam.playerAction!=null&&spam.playerAction.kind=="skill"&&
                spam.playerAction.direction.x>.99f&&
                spam.playerAction.target.HasValue&&
                (spam.playerAction.target.Value-new Point(25,17)).Length<.001f,
                "Repeating Q retargets a real pending cooldown intent without opening another deadline");
            var expired=Fresh(data,1);expired.enemies.Clear();
            expired.skillCooldown=.18f;expired.playerHitTime=.5f;
            expired.Skill(new Point(0,1));
            Advance(expired,.22f);
            Check(!expired.SkillQueued&&expired.playerAction==null&&
                expired.skillCooldown<=.00001f,
                "Cooling down during longer hit recovery cannot prolong a 200ms Q into an eventual phantom cast");
        }

        public static void BufferedSkillRepeatCannotExtendOrRetargetIntent(Campaign data)
        {
            var g=Fresh(data,1);g.enemies.Clear();
            g.save.energy=100;g.playerHitTime=.55f;
            g.Skill(new Point(0,-1));
            Check(g.SkillQueued&&g.playerAction==null&&g.save.energy==100,
                "A Q pressed during real hit recovery must queue without paying early");
            Advance(g,.12f);
            g.SetAim(new Point(1,0));
            g.Skill(new Point(1,0));
            Check(g.SkillQueued&&g.playerAction==null,
                "Repeated Q cannot force a premature skill through hit recoil");
            Advance(g,.09f);
            Check(!g.SkillQueued&&g.playerAction==null&&g.save.energy==100,
                "Repeated Q cannot extend the original 200ms input window");
            Advance(g,.4f);
            Check(g.playerAction==null&&g.skillCooldown<=0&&g.save.energy==100,
                "Expired buffered Q cannot become a free delayed spell");

            g=Fresh(data,1);g.enemies.Clear();g.save.energy=100;
            g.Dash(new Point(-1,0));
            g.Skill(new Point(0,-1));
            Check(g.SkillQueued&&g.save.energy==80,
                "Dodge then Q must preserve energy until Q is actually accepted");
            Advance(g,.06f);
            Point latestTarget=new Point(21,17);
            g.SetAimTarget(latestTarget);
            g.Skill(new Point(1,0));
            Advance(g,.15f);
            Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                g.playerAction.direction.x>.99f&&
                Math.Abs(g.playerAction.direction.y)<.01f&&
                g.playerAction.target.HasValue&&
                (g.playerAction.target.Value-latestTarget).Length<.001f&&
                !g.SkillQueued&&g.skillCooldown>0,
                "Repeated Q must retarget the existing queue, without moving its deadline");
            Check(g.save.energy>=40&&g.save.energy<45,
                "Queued Q commits its cost once, not on every repeated press");
        }

        public static void ClassesAndPause(Campaign data)
        {
            for (int i = 0; i < 3; i++)
            {
                var g = Fresh(data, i); Check(g.save.weapon == i, "Class starter weapon");
                g.save.x = 10; g.save.y = 14; float x = g.save.x;
                g.Tick(.05f, new Point(1, 1)); Check(g.save.x > x, "Movement");
                float time = g.save.totalTime; g.mode = "inventory"; g.Tick(.05f, new Point(1, 0)); Check(g.save.totalTime == time, "Menus must pause the simulation");
                g.Close(); g.save.energy = 100; g.Skill(); Check(g.save.energy == 60 && g.skillCooldown > 0, "Class skill cost");
                g.Skill(); Check(g.save.energy == 60, "Skill cannot repeat during cooldown");
            }
        }
        public static void RepeatablePurchasesCannotConsumeResourcesPastSavedStackCaps(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.coins=500;
            g.save.inventory[0]=9997;g.save.inventory[7]=9998;
            g.save.inventory[6]=9999;g.save.inventory[2]=20;
            g.save.inventory[3]=20;g.save.inventory[1]=20;g.save.tea=999;
            int cash=g.save.coins,wood=g.save.inventory[0],
                seeds=g.save.inventory[7],herbs=g.save.inventory[2],
                fish=g.save.inventory[3],crystal=g.save.inventory[1];
            Action(g,"cook","buywood");
            Action(g,"garden","seeds");
            Action(g,"cook","buymeal");
            Action(g,"cook","cook");
            Action(g,"cook","tea");
            Check(g.save.coins==cash&&g.save.inventory[0]==wood&&
                  g.save.inventory[7]==seeds&&g.save.inventory[6]==9999&&
                  g.save.inventory[2]==herbs&&g.save.inventory[3]==fish&&
                  g.save.inventory[1]==crystal&&g.save.tea==999,
                "Full saved stacks reject timber, seeds, purchased/cooked stew and tea before deducting any ingredients or coins");
            g.save.inventory[0]=9995;g.save.inventory[7]=9996;
            g.save.inventory[6]=9998;g.save.tea=998;
            Action(g,"cook","buywood");
            Action(g,"garden","seeds");
            Action(g,"cook","buymeal");
            Action(g,"cook","tea");
            Check(g.save.inventory[0]==9999&&g.save.inventory[7]==9999&&
                  g.save.inventory[6]==9999&&g.save.tea==999&&
                  g.save.coins==cash-g.ShopTimberCost-5-g.ShopStewCost&&
                  g.save.inventory[2]==herbs-2&&g.save.inventory[1]==crystal-1,
                "Exact last available storage space still permits legitimate paid purchases with correct resource costs");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.inventory[0]==9999&&loaded.save.inventory[7]==9999&&
                  loaded.save.inventory[6]==9999&&loaded.save.tea==999,
                "Full-stack purchases survive v2 save/load without losing paid inventory");
        }
        public static void Transactions(Campaign data)
        {
            var g = Fresh(data); g.save.inventory[1] = 0;
            int wood = g.save.inventory[0], gear = g.save.inventory[4];
            Action(g, "forge", "upgrade"); Check(g.save.inventory[0] == wood && g.save.inventory[4] == gear && g.save.upgrades == 0, "Failed crafting must be atomic");
            g.save.inventory[1] = 10; g.save.inventory[4] = 10; g.save.inventory[0] = 20;
            Action(g, "forge", "upgrade"); Check(g.save.upgrades == 1 && g.save.maxHealth == 140, "Equipment upgrade");
            Action(g, "forge", "bow"); Check(g.Has("weapon_1") && g.save.weapon == 1, "Craft a bow");
            int after = g.save.inventory[0]; Action(g, "forge", "bow"); Check(g.save.inventory[0] == after, "Re-equipping a crafted weapon must be free");
            g.save.inventory[2] = 20; Action(g, "garden", "ally1");
            int coins = g.save.coins; TalkNear(g, "garden");
            foreach (var c in g.conversation.choices) Check(c.action != "ally1", "Completed quest cannot reward twice");
            Check(g.save.coins == coins && g.Allies == 1 && g.save.skillPoints == 1, "Commission rewards");
            g.Learn(1); Check(g.save.skillPoints == 0 && g.save.maxHealth == 152, "Talent allocation");
        }
        public static void PublishingRoadAuditAnnouncesItsRealFieldKitService(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.stage=3;g.save.room=9;g.enemies.Clear();
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            foreach(int room in new int[]{0,3,5})
                g.save.flags.Add("civic_aftercare_waytrail_"+room);
            WorldObject inn=Array.Find(g.Map.objects,o=>
                o.kind=="innGuest"&&o.id=="innTraveler");
            Check(inn!=null,"Actual inn traveler is present at the original story hub");
            g.save.x=inn.x;g.save.y=inn.y;
            g.Talk(inn);
            Check(g.conversation!=null,"The existing guest is an authentic world conversation");
            int publish=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_aftercare_publish");
            Check(publish>=0,"Three genuinely logged regional markers offer the real public publication choice");
            int coins=g.save.coins;
            g.Choose(publish);
            Check(g.Has("civic_aftercare_published")&&g.save.coins==coins+35&&
                g.toast.Contains("药草急救包"),
                "Original publication still pays its exact reward and introduces the new physical service");
            QuestGuide done=g.GetQuest("civic_aftercare");
            Check(done.complete&&done.reward.Contains("药草急救点")&&
                done.next.Contains("急救"),
                "The real tracked quest reports the earned repeatable field service, not merely a one-time currency payout");
            ReadInnNews(g,inn);
            Check(g.conversation!=null&&g.conversation.text.Contains("急救包")&&
                g.conversation.textEn.Contains("field kits"),
                "The original night-traveler now explains earned field treatment in natural bilingual dialogue");
            g.Close();
            g.Talk(inn);
            Check(Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_aftercare_publish")<0&&g.save.coins==coins+35,
                "Published register neither repeats its reward nor reopens a stale publication decision");
        }

        public static void CompanionUsesReachableFrontierUntilRealGateReopens(Campaign data)
        {
            // Isolated, test-only authored corridor. The production maps,
            // scenes, and their original GUIDs are never modified.
            Campaign isolated=new Campaign{title=data.title,titleEn=data.titleEn,
                version=data.version,items=data.items,bosses=data.bosses,
                lore=data.lore,maps=(MapData[])data.maps.Clone()};
            int width=18,height=14;
            string[] rows=new string[height];
            for(int y=0;y<height;y++)
            {
                char[] tiles=new char[width];
                for(int x=0;x<width;x++)
                    tiles[x]=(x<2||y<2||x>=width-2||y>=height-2||x==9)?
                        '#':'.';
                rows[y]=new string(tiles);
            }
            isolated.maps[0]=new MapData{id=0,name="Test locked crossing",
                en="Test locked crossing",rows=rows,
                objects=new WorldObject[0],decor=new Decoration[0],
                spawnX=13.5f,spawnY=7.5f};
            var g=Fresh(isolated);
            g.save.flags.Add("companion_0");g.SelectCompanion(0);
            g.enemies.Clear();g.save.x=13.5f;g.save.y=7.5f;
            g.companionPosition=new Point(4.5f,7.5f);
            Advance(g,1.1f);
            Check(g.CanStand(g.companionPosition)&&
                g.companionPosition.x>6.5f&&g.companionPosition.x<9f,
                "An unreachable formation socket leads the companion along genuine foot travel to the near-side frontier");
            Point landed=g.companionPosition;
            bool blocked=g.CompanionRouteBlocked;
            // A truthful warning only appears after a genuine attempted
            // blocked foot route, not after an arbitrary wall-clock delay.
            for(int frame=0;frame<150&&!blocked;frame++)
            {
                g.Tick(.02f,new Point());
                blocked=g.CompanionRouteBlocked;
            }
            Check(g.CanStand(g.companionPosition)&&
                g.companionPosition.x<9f&&
                (g.companionPosition-landed).Length<1.3f&&blocked,
                "A sealed crossing must eventually report actual unreachable ground without shuffling through tiles; foot="+
                g.companionPosition.x+","+g.companionPosition.y+
                " started="+landed.x+","+landed.y+
                " blocked="+blocked+
                " selectedFollow="+
                ((Point)typeof(GameCore).GetMethod("CompanionFollowTarget",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).Invoke(g,null)).x+","+
                ((Point)typeof(GameCore).GetMethod("CompanionFollowTarget",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).Invoke(g,null)).y+
                " hero="+g.Position.x+","+g.Position.y+
                " gameMode="+g.mode+" practice="+g.practicing+
                " companionAction="+(g.companionAction==null?
                    "none":g.companionAction.kind)+
                " holding="+g.CompanionHoldingPosition+
                " retry="+typeof(GameCore).GetField("companionBlockedRouteRetry",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).GetValue(g)+
                " blockedCore="+typeof(GameCore).GetField("companionUnreachableGoal",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).GetValue(g));
            char[] opened=rows[7].ToCharArray();opened[9]='.';
            rows[7]=new string(opened);
            Advance(g,3.1f);
            Check(g.CanStand(g.companionPosition)&&
                g.companionPosition.x>9.25f&&
                (g.companionPosition-g.Position).Length<3.0f&&
                !g.CompanionRouteBlocked,
                "A reopened physical route rejoins the party and retires its stale obstacle warning");
        }

        public static void CompanionBlockedFootRecoversLocallyWithoutCrossMapWarp(Campaign data)
        {
            var g=Fresh(data);g.save.flags.Add("companion_0");
            g.SelectCompanion(0);g.enemies.Clear();
            Check(g.save.companion==0,"The genuine recruited companion joins the playable party");
            Point distant=new Point(-8,-8);
            g.companionPosition=distant;
            g.Tick(.05f,new Point());
            Check((g.companionPosition-distant).Length<.001f&&
                !g.companionMoving,
                "An off-map invalid ally cannot warp to the hero through the follow-target recovery branch");
            Point invalid=new Point();
            bool found=false;
            for(int y=4;y<g.Map.rows.Length-4&&!found;y++)
                for(int x=4;x<g.Map.rows[y].Length-4&&!found;x++)
                {
                    Point p=new Point(x+.5f,y+.5f);
                    if(g.CanStand(p)||(p-g.Position).Length<4f)continue;
                    for(int d=0;d<4&&!found;d++)
                    {
                        Point[] around={new Point(1,0),new Point(-1,0),
                            new Point(0,1),new Point(0,-1)};
                        if(g.CanStand(p+around[d])){invalid=p;found=true;}
                    }
                }
            Check(found,"A real authored obstacle has a valid neighboring recovery foot");
            g.companionPosition=invalid;
            g.companionMoveTime=37f;
            g.Tick(.02f,new Point());
            Check(g.CanStand(g.companionPosition)&&
                (g.companionPosition-invalid).Length<=1.5001f&&
                !g.companionMoving&&g.companionMoveTime==0f,
                "Terrain-invalidated party member safely replants within 1.5 tiles without a fake walk animation");
        }

        public static void CiviliansStockThreeAuthenticPostAuditWaystations(Campaign data)
        {
            GameCore g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            foreach(int region in new[]{0,3,5})
            {
                g.save.flags.Add("civic_aftercare_waytrail_"+region);
                // A published audit REQUIRES a genuine earlier inspection
                // of each old road marker. Keep its original first-survey
                // coins/shards from being falsely charged to resupply.
                foreach(WorldObject old in data.maps[region].objects)
                    if(old.kind=="waytrail")
                        g.save.flags.Add("waytrail_"+old.id);
            }
            g.save.inventory[0]=12;g.save.inventory[2]=8;
            int initialCoins=g.save.coins,initialShards=g.save.shards;
            int[] regions={0,3,5};
            for(int index=0;index<regions.Length;index++)
            {
                int region=regions[index];
                g.Enter(region);
                Check(g.save.room==region,
                    "Actual post-game trail regions remain accessible");
                g.enemies.Clear();
                WorldObject trail=Array.Find(g.Map.objects,
                    x=>x.kind=="waytrail");
                Check(trail!=null,
                    "Each waystation uses an original existing physical map marker");
                g.save.x=trail.x;g.save.y=trail.y;
                g.Talk(trail);
                int stock=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_waystation_stock");
                Check(stock>=0,
                    "Published original trail talk offers a player-funded station project");
                int timber=g.save.inventory[0],herbs=g.save.inventory[2];
                if(index==0)
                {
                    // The cached dialogue may no longer be reachable.
                    g.save.x=trail.x+12f;
                    g.Choose(stock);
                    Check(g.save.inventory[0]==timber&&
                        g.save.inventory[2]==herbs&&g.CivicWaystationCount==0,
                        "Remote stale marker choices never spend construction materials");
                    g.save.x=trail.x;g.save.y=trail.y;
                    g.Talk(trail);
                    stock=Array.FindIndex(g.conversation.choices,
                        x=>x.action=="civic_waystation_stock");
                    g.enemies.Add(new Enemy(910120,0,trail.x+.7f,trail.y,25f));
                    g.Choose(stock);
                    Check(g.save.inventory[0]==timber&&
                        g.save.inventory[2]==herbs&&g.CivicWaystationCount==0,
                        "A real hostile by the source prevents combat-time construction");
                    g.enemies.Clear();
                    g.Talk(trail);
                    stock=Array.FindIndex(g.conversation.choices,
                        x=>x.action=="civic_waystation_stock");
                    g.save.inventory[0]=1;
                    g.Choose(stock);
                    Check(g.save.inventory[0]==1&&
                        g.save.inventory[2]==herbs&&g.CivicWaystationCount==0,
                        "Insufficient timber never partially consumes a medicinal herb");
                    g.save.inventory[0]=timber;
                    g.Talk(trail);
                    stock=Array.FindIndex(g.conversation.choices,
                        x=>x.action=="civic_waystation_stock");
                }
                g.Choose(stock);
                Check(g.Has("civic_waystation_stocked_"+region)&&
                    g.CivicWaystationCount==index+1&&
                    g.save.inventory[0]==timber-2&&
                    g.save.inventory[2]==herbs-1,
                    "Each unique site consumes exactly two real timber and one herb");
                g.Talk(trail);
                Check(Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_waystation_stock")<0&&
                    Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_waystation_status")>=0,
                    "A supplied marker switches to permanent service without farming stock");
                g.Close();
                if(index==0)
                {
                    g.PrepareSave();
                    GameCore restored=new GameCore(data,CloneSave(g.save));
                    Check(restored.save.version==2&&restored.CivicWaystationCount==1,
                        "The existing V2 save survives a partially provisioned network");
                    g=restored;g.Start();
                }
            }
            QuestGuide q=g.GetQuest("civic_waystations");
            Check(q.complete&&!q.locked&&g.CivicWaystationCount==3&&
                q.steps.Count==3&&g.Has("civic_waystations_complete")&&
                g.save.coins==initialCoins+30&&
                g.save.shards==initialShards+1,
                "Three physically funded regions complete a real journal objective and pay just once");
            g.PrepareSave();
            GameCore saved=new GameCore(data,CloneSave(g.save));saved.Start();
            Check(saved.save.version==2&&
                saved.GetQuest("civic_waystations").complete&&
                saved.CivicWaystationCount==3&&
                saved.save.coins==initialCoins+30,
                "Completion, coin rewards and prior campaign data survive old-format reloads");
        }

        public static void CivicLocalShuttlesHonorTheRealWalkableRoadAndCompanionFoot(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            foreach(int room in new[]{0,3,5})
            {
                g.save.flags.Add("civic_aftercare_waytrail_"+room);
                g.save.flags.Add("civic_waystation_stocked_"+room);
                foreach(WorldObject marker in data.maps[room].objects)
                    if(marker.kind=="waytrail")
                        g.save.flags.Add("waytrail_"+marker.id);
            }
            var arrivalCheck=typeof(GameCore).GetMethod(
                "CivicWaystationArrivalFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(arrivalCheck!=null,
                "The physical shuttle shares the same collision-safe arrival helper");
            WorldObject source=null,destination=null;
            Point? destinationFoot=null;
            // Select a real open signed road, not imaginary coordinates.
            foreach(int region in new[]{0,3,5})
            {
                if(destinationFoot.HasValue)break;
                g.Enter(region);g.enemies.Clear();
                foreach(WorldObject a in g.Map.objects)
                {
                    if(a.kind!="waytrail")continue;
                    foreach(WorldObject b in g.Map.objects)
                    {
                        if(b.kind!="waytrail"||
                            System.Object.ReferenceEquals(a,b))continue;
                        g.save.x=a.x;g.save.y=a.y;
                        if(!g.CanStand(g.Position))continue;
                        var candidate=(Point?)arrivalCheck.Invoke(g,new object[]{b,true});
                        if(!candidate.HasValue)continue;
                        source=a;destination=b;destinationFoot=candidate;
                        break;
                    }
                    if(destinationFoot.HasValue)break;
                }
            }
            Check(source!=null&&destination!=null&&destinationFoot.HasValue,
                "At least one original pair of public road markers has a genuine passable lane");
            int home=g.save.room;
            g.save.x=source.x;g.save.y=source.y;
            g.save.energy=70f;g.save.coins=15;
            g.save.trackedQuest="civic_waystations";
            g.Talk(source);
            string route="civic_waystation_local_"+destination.id;
            int choice=Array.FindIndex(g.conversation.choices,
                x=>x.action==route);
            Check(choice>=0,
                "Only previously physically surveyed sister markers offer a local civic shuttle");
            int coins=g.save.coins;
            g.save.x=source.x+14f;
            g.Choose(choice);
            Check(g.save.coins==coins&&g.save.room==home&&
                Math.Abs(g.save.energy-70f)<.001f,
                "A stale remote menu cannot teleport a passenger across town");
            g.save.x=source.x;g.save.y=source.y;
            g.Talk(source);choice=Array.FindIndex(g.conversation.choices,x=>x.action==route);
            g.save.energy=9f;g.Choose(choice);
            Check(g.save.coins==coins&&g.save.energy==9f,
                "An unaffordable local carriage cannot charge partial fare");
            g.save.energy=70f;
            g.Talk(source);choice=Array.FindIndex(g.conversation.choices,x=>x.action==route);
            g.enemies.Add(new Enemy(97225,0,source.x+.4f,source.y,20));
            g.Choose(choice);
            Check(g.save.coins==coins&&g.save.energy==70f,
                "A live hostile blocks local shuttle departure without changing resources");
            g.enemies.Clear();
            g.Talk(source);choice=Array.FindIndex(g.conversation.choices,x=>x.action==route);
            g.save.flags.Remove("waytrail_"+destination.id);
            g.Choose(choice);
            Check(g.save.coins==coins&&g.save.energy==70f,
                "Revoked destination survey prevents a stale carriage choice");
            g.save.flags.Add("waytrail_"+destination.id);
            // Neither safe station alone makes the INTERVENING lane safe.
            // A hostile planted on a genuine intermediate BFS waypoint
            // blocks a paid carriage without requiring an abstract raid flag.
            List<Point> actualRoad=g.NavigationPathFrom(
                g.Position,destinationFoot.Value,true);
            Point? threatened=null;
            foreach(Point foot in actualRoad)
                if((foot-g.Position).Length>6.5f&&
                    (foot-destinationFoot.Value).Length>6.5f&&
                    g.CanStand(foot))
                {threatened=foot;break;}
            if(threatened.HasValue)
            {
                g.enemies.Add(new Enemy(97226,0,threatened.Value.x,
                    threatened.Value.y,30));
                g.Talk(source);
                choice=Array.FindIndex(g.conversation.choices,x=>x.action==route);
                g.Choose(choice);
                Check(g.save.coins==coins&&
                    Math.Abs(g.save.energy-70f)<.001f&&
                    (g.Position-new Point(source.x,source.y)).Length<.01f,
                    "Real intermediate hostile pressure blocks the whole local route and preserves both currencies");
                g.enemies.Clear();
            }
            g.Talk(source);choice=Array.FindIndex(g.conversation.choices,x=>x.action==route);
            g.Choose(choice);
            Check(g.save.room==home&&g.save.coins==coins-2&&
                Math.Abs(g.save.energy-60f)<.001f&&g.CanStand(g.Position)&&
                (g.Position-new Point(destination.x,destination.y)).Length<1.75f&&
                g.save.trackedQuest=="civic_waystations",
                "One real within-region ride lands at the destination sign, pays exactly once, and keeps the journal");
            g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));reloaded.Start();
            Check(reloaded.save.version==2&&
                reloaded.save.room==home&&
                reloaded.save.coins==coins-2&&
                reloaded.CivicWaystationCount==3,
                "V2 reload preserves paid local transit without new serialized shuttle state");
        }


        public static void CourierAmbushUsesUnoccupiedRoadFeetAcrossRetries(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            g.save.flags.Add("civic_aftercare_waytrail_0");
            g.save.flags.Add("civic_waystation_stocked_0");
            foreach(WorldObject sign in data.maps[0].objects)
                if(sign!=null&&sign.kind=="waytrail")
                    g.save.flags.Add("waytrail_"+sign.id);
            g.Enter(0);g.enemies.Clear();
            WorldObject start=null;
            foreach(WorldObject o in g.Map.objects)
                if(o!=null&&o.kind=="waytrail"){start=o;break;}
            Check(start!=null,"Public medicine contract has a real origin sign");
            g.save.x=start.x;g.save.y=start.y;
            g.save.inventory[2]=9;g.save.companion=-1;
            g.Talk(start);
            int accept=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_courier_start");
            Check(accept>=0,"Physical public medicine route can be accepted");
            g.Choose(accept);
            Enemy original=g.enemies.Find(g.IsCivicCourierGuard);
            Check(original!=null&&g.CanStand(original.Position)&&
                (original.Position-g.Position).Length>=1.3f,
                "First live ambusher is planted on walkable ground away from the courier");
            Point occupied=original.Position;
            g.Talk(start);
            int cancel=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_courier_cancel");
            Check(cancel>=0,"Returning to the actual dispatch sign permits cancellation");
            g.Choose(cancel);
            Check(!g.CivicCourierActive&&!g.enemies.Exists(g.IsCivicCourierGuard),
                "Returning the first parcel retires that genuine ambusher");
            Enemy existingPatrol=new Enemy(919191,0,occupied.x,occupied.y,300f);
            existingPatrol.stun=300f;
            g.enemies.Add(existingPatrol);
            g.Talk(start);
            accept=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_courier_start");
            Check(accept>=0&&g.SafeTravel,
                "The old patrol is far enough down the original surveyed road to keep dispatch safe");
            g.Choose(accept);
            Enemy rerouted=g.enemies.Find(g.IsCivicCourierGuard);
            Check(rerouted!=null&&g.CivicCourierActive&&
                g.enemies.Contains(existingPatrol)&&
                (rerouted.Position-occupied).Length>=1.05f&&
                g.CanStand(rerouted.Position)&&
                (g.GetQuest("civic_courier").target-rerouted.Position).Length<.01f,
                "Retry uses a different real road tile instead of stacking the live archer into the patrol");

            g.Talk(start);
            cancel=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_courier_cancel");
            g.Choose(cancel);
            g.enemies.Clear();
            g.save.companion=0;
            g.companionPosition=occupied;
            g.Talk(start);
            accept=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_courier_start");
            g.Choose(accept);
            Enemy allySafe=g.enemies.Find(g.IsCivicCourierGuard);
            Check(allySafe!=null&&
                (allySafe.Position-g.companionPosition).Length>=1.25f&&
                g.CivicCourierActive,
                "A real following ally is never overwritten by the newly spawned ambusher");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Enemy restored=loaded.enemies.Find(loaded.IsCivicCourierGuard);
            Check(loaded.save.version==2&&loaded.CivicCourierActive&&
                restored!=null&&loaded.CanStand(restored.Position),
                "Existing V2 medical delivery still restores its authentic live ambush");
        }

        public static void CompletedPublicMedicineStrengthensRealOneHerbFieldAid(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            g.save.flags.Add("civic_aftercare_waytrail_0");
            g.save.flags.Add("civic_waystation_stocked_0");
            g.Enter(0);g.enemies.Clear();
            WorldObject marker=Array.Find(g.Map.objects,
                o=>o!=null&&o.kind=="waytrail");
            Check(marker!=null,"The original public-aid trail marker exists");
            g.save.x=marker.x;g.save.y=marker.y;
            g.save.inventory[2]=8;
            g.save.health=g.save.maxHealth-70f;
            g.save.heat=80f;g.save.cold=80f;
            Check(!g.CivicCourierTriageTrained&&
                g.CivicFieldAidHealing==36&&g.CivicFieldAidClimateRelief==25,
                "Unfinished parcel missions keep the original balanced one-herb field kit");
            g.Talk(marker);
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_aftercare_fieldaid");
            Check(choice>=0&&g.conversation.choices[choice].en.Contains("36 HP"),
                "Untrained treatment price and effect are visible in actual sign conversation");
            int herbs=g.save.inventory[2];
            g.Choose(choice);
            Check(g.save.inventory[2]==herbs-1&&
                Math.Abs(g.save.health-(g.save.maxHealth-34f))<.001f&&
                Math.Abs(g.save.heat-55f)<.001f&&
                Math.Abs(g.save.cold-55f)<.001f,
                "Physical field treatment still costs one herb and heals exactly 36 HP and 25 climate");
            g.save.health=g.save.maxHealth-70f;
            g.save.heat=80f;g.save.cold=80f;
            foreach(int region in new[]{0,3,5})
                g.save.flags.Add("civic_courier_done_"+region);
            g.save.flags.Add("civic_courier_all");
            Check(g.CivicCourierTriageTrained&&
                g.CivicFieldAidHealing==48&&g.CivicFieldAidClimateRelief==35&&
                g.GetQuest("civic_courier").reward.Contains("48"),
                "Three authentic permanent handoffs grant a journal-visible health service upgrade");
            g.Talk(marker);
            choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_aftercare_fieldaid");
            Check(choice>=0&&g.conversation.choices[choice].en.Contains("48 HP"),
                "The existing production sign now offers the trained remedy without new NPCs");
            herbs=g.save.inventory[2];
            g.save.x=marker.x+14f;
            g.Choose(choice);
            Check(g.save.inventory[2]==herbs&&
                Math.Abs(g.save.health-(g.save.maxHealth-70f))<.001f,
                "A cached remote treatment choice cannot spend supplies or heal");
            g.save.x=marker.x;g.save.y=marker.y;
            g.Talk(marker);
            choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="civic_aftercare_fieldaid");
            g.Choose(choice);
            Check(g.save.inventory[2]==herbs-1&&
                Math.Abs(g.save.health-(g.save.maxHealth-22f))<.001f&&
                Math.Abs(g.save.heat-45f)<.001f&&
                Math.Abs(g.save.cold-45f)<.001f,
                "Earned medicine training upgrades actual HP and climate relief but never removes herb cost");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.save.version==2&&restored.CivicCourierTriageTrained&&
                restored.CivicFieldAidHealing==48&&
                restored.save.inventory[2]==herbs-1,
                "Existing V2 flags retain trained public care after reload without changing the save schema");
        }

        public static void SignedMedicineActuallyImprovesSafeWaystationRecovery(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_published");
            g.save.flags.Add("civic_aftercare_waytrail_0");
            g.save.flags.Add("civic_waystation_stocked_0");
            g.Enter(0);g.enemies.Clear();
            WorldObject marker=Array.Find(g.Map.objects,
                o=>o!=null&&o.kind=="waytrail");
            Check(marker!=null,"An authentic staffed public road marker is present");
            g.save.x=marker.x;g.save.y=marker.y;
            g.save.health=g.save.maxHealth-60f;
            g.save.energy=30f;g.save.heat=70f;g.save.cold=70f;
            Check(g.CivicWaystationNear&&g.SafeTravel,
                "The staffed station has real safe physical reach for world-time treatment");
            g.Talk(marker);
            Check(g.conversation.textEn.Contains("+36 HP")&&
                g.conversation.textEn.Contains("0.95 HP/sec"),
                "The original real-world sign describes the unchanged base service accurately");
            g.Close();
            float health=g.save.health,energy=g.save.energy,heat=g.save.heat,cold=g.save.cold;
            for(int i=0;i<50;i++)g.Tick(.02f,new Point());
            float basicHp=g.save.health-health,basicEnergy=g.save.energy-energy;
            float basicHeat=heat-g.save.heat,basicCold=cold-g.save.cold;
            foreach(int region in new[]{0,3,5})
                g.save.flags.Add("civic_courier_done_"+region);
            g.save.flags.Add("civic_courier_all");
            Check(g.CivicCourierTriageTrained&&
                Math.Abs(g.CivicWaystationHealingRate-1.45f)<.001f&&
                Math.Abs(g.CivicWaystationEnergyRate-4f)<.001f&&
                Math.Abs(g.CivicWaystationClimateRate-5f)<.001f&&
                g.GetQuest("civic_waystations").reward.Contains("1.45"),
                "Three actually signed deliveries permanently upgrade the existing roadside service");
            g.save.health=health;g.save.energy=energy;
            g.save.heat=heat;g.save.cold=cold;
            g.Talk(marker);
            Check(g.conversation.textEn.Contains("+48 HP")&&
                g.conversation.textEn.Contains("1.45 HP/sec")&&
                g.conversation.text.Contains("生命 +48"),
                "The original sign never advertises stale weaker medicine after training");
            g.Close();
            for(int i=0;i<50;i++)g.Tick(.02f,new Point());
            Check(g.save.health-health>basicHp+.35f&&
                g.save.energy-energy>basicEnergy+.8f&&
                heat-g.save.heat>basicHeat+.7f&&
                cold-g.save.cold>basicCold+.7f,
                "True world ticks deliver stronger HP, stamina and climate respite than the original service");
            g.save.health=health;g.save.energy=energy;
            g.save.heat=heat;g.save.cold=cold;
            g.enemies.Add(new Enemy(90512,0,marker.x+1f,marker.y,100));
            for(int i=0;i<25;i++)g.Tick(.02f,new Point());
            Check(Math.Abs(g.save.health-health)<.01f,
                "Live enemy pressure stops the enhanced medical station from healing in combat");
            g.enemies.Clear();
            g.save.flags.Remove("civic_waystation_stocked_0");
            g.save.health=health;
            for(int i=0;i<25;i++)g.Tick(.02f,new Point());
            Check(Math.Abs(g.save.health-health)<.01f,
                "Unsigned or unstocked markers do not get remote passive medical service");
            g.PrepareSave();
            var resumed=new GameCore(data,CloneSave(g.save));resumed.Start();
            Check(resumed.save.version==2&&resumed.CivicCourierTriageTrained&&
                resumed.CivicWaystationHealingRate==1.45f,
                "The existing V2 save flags alone restore trained civilian staff after reload");
        }


        public static void ReturningMedicalParcelWithdrawsOnlyItsOwnLiveArrows(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            g.save.flags.Add("civic_aftercare_waytrail_0");
            g.save.flags.Add("civic_waystation_stocked_0");
            foreach(WorldObject o in data.maps[0].objects)
                if(o!=null&&o.kind=="waytrail")
                    g.save.flags.Add("waytrail_"+o.id);
            g.Enter(0);g.enemies.Clear();
            WorldObject marker=Array.Find(g.Map.objects,
                o=>o!=null&&o.kind=="waytrail");
            Check(marker!=null,"The public medicine desk is an original world object");
            g.save.x=marker.x;g.save.y=marker.y;
            g.save.inventory[2]=5;
            g.Talk(marker);
            int accept=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_courier_start");
            Check(accept>=0,"An authentic first medical parcel may be accepted");
            int herbs=g.save.inventory[2];
            g.Choose(accept);
            Enemy raider=g.enemies.Find(g.IsCivicCourierGuard);
            Check(raider!=null&&g.CivicCourierActive&&
                g.save.inventory[2]==herbs-1,
                "The active real contract has one shooter identity and a paid parcel");
            Enemy unrelated=new Enemy(raider.id,1,raider.x+2.5f,raider.y,55);
            g.enemies.Add(unrelated);
            // Simulate already-released projectiles with real live shooter
            // ownership. Keep them outside the origin's combat lock, exactly
            // as they would be on the middle of the surveyed walking route.
            var owned=new Projectile{hostileShooter=raider,
                visual="enemyArrow",source="shot",damage=18,life=3,
                position=raider.Position,velocity=new Point(1,0)};
            var other=new Projectile{hostileShooter=unrelated,
                visual="enemyArrow",source="shot",damage=18,life=3,
                position=unrelated.Position,velocity=new Point(1,0)};
            var reflected=new Projectile{hostileShooter=raider,
                visual="arrow",source="shot",friendly=true,damage=9,life=3,
                position=raider.Position,velocity=new Point(-1,0)};
            g.projectiles.Add(owned);g.projectiles.Add(other);
            g.projectiles.Add(reflected);
            Check(g.SafeTravel,
                "The real dispatch sign remains secure despite distant already-fired roadside arrows");
            g.Talk(marker);
            int cancel=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_courier_cancel");
            Check(cancel>=0,"Original dispatch marker can offer the true parcel refund");
            g.Choose(cancel);
            Check(!g.CivicCourierActive&&
                g.save.inventory[2]==herbs&&
                !g.enemies.Contains(raider)&&
                !g.projectiles.Contains(owned)&&
                g.projectiles.Contains(other)&&
                g.projectiles.Contains(reflected)&&
                g.enemies.Contains(unrelated),
                "Withdrawing only this raider cancels its hostile arrow, not a colliding-ID enemy's arrow or an earned player deflection");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.save.version==2&&!restored.CivicCourierActive&&
                restored.save.inventory[2]==herbs,
                "The real V2 cancellation remains a one-herb refund after save reload");
        }


        public static void SkywatchGuardianIdentitySurvivesSameIdEnemyAndRealLethalVictory(Campaign data)
        {
            var g=Unlocked(data);
            foreach(string flag in new[]{"skywatch_started","skywatch_prism_1",
                "skywatch_prism_2","skywatch_prism_3"})
                g.save.flags.Add(flag);
            g.Enter(7);g.enemies.Clear();g.projectiles.Clear();
            WorldObject altar=Array.Find(g.Map.objects,
                o=>o!=null&&o.kind=="skyobservatory");
            Check(altar!=null,"Actual Frostchime observatory exists");
            g.save.x=altar.x;g.save.y=altar.y;
            var choose=typeof(GameCore).GetMethod("ChooseSkywatch",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Actual Skywatch trial action is wired in GameCore");
            choose.Invoke(g,new object[]{"skywatch_start"});
            Enemy guardian=g.enemies.Find(g.IsSkywatchEnemy);
            Check(guardian!=null&&guardian.boss&&g.SkywatchRoom==7,
                "Original physical observatory starts one real authored elite encounter");
            var unrelated=new Enemy(guardian.id,0,guardian.x+5f,
                guardian.y+5f,45);
            g.enemies.Insert(0,unrelated);
            Check(!g.IsSkywatchEnemy(unrelated)&&g.IsSkywatchEnemy(guardian),
                "Identical numeric spawn IDs cannot impersonate the live trial boss");
            var damage=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(damage!=null,"Actual combat damage entry point exists");
            damage.Invoke(g,new object[]{unrelated,999f,false,null,null});
            Check(g.enemies.Contains(guardian)&&!g.Has("skywatch_defeated")&&
                g.SkywatchRoom==7&&g.IsSkywatchEnemy(guardian),
                "Destroying a same-ID stranger cannot complete the observatory or descope its real guardian");
            damage.Invoke(g,new object[]{guardian,999f,false,null,null});
            Check(!g.enemies.Contains(guardian)&&!g.Has("boss_7")&&
                !g.enemies.Exists(g.IsSkywatchEnemy),
                "Genuine optional guardian contact is not a main-story boss victory");
            var tick=typeof(GameCore).GetMethod("TickSkywatch",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            tick.Invoke(g,new object[]{.02f});
            Check(g.Has("skywatch_defeated")&&g.SkywatchRoom==0&&
                !g.Has("boss_7"),
                "Only the actual authored boss death completes its optional trial");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2&&loaded.Has("skywatch_defeated")&&
                !loaded.Has("boss_7")&&loaded.SkywatchRoom==0,
                "Existing V2 save preserves optional completion without serializing encounter identity");
        }


        public static void MainChapterGuardianVictoryKeepsRealNextStepAfterCorpseFades(Campaign data)
        {
            var g=Fresh(data);
            // Assert the actual player-selected English objective; Fresh()
            // inherits Chinese localization from the ordinary starting save.
            g.save.english=true;
            g.save.stage=1;
            g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();g.projectiles.Clear();
            WorldObject shrine=Array.Find(g.Map.objects,
                o=>o!=null&&o.kind=="shrine");
            Check(shrine!=null,"Original Thornwood chapter Boss has a real shrine");
            g.save.x=shrine.x;g.save.y=shrine.y;
            Check(g.CanStand(g.Position)&&g.GateOpen,
                "An actually cleared scout route unlocks the normal chapter guardian");
            Check(!g.BossVictoryFeedback&&g.BossVictoryNextStep=="",
                "An opened shrine alone must never fake a Boss death");
            g.Talk(shrine);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,c=>c.action=="boss")>=0,
                "The real shrine exposes the original authored combat challenge");
            g.Choose(Array.FindIndex(g.conversation.choices,c=>c.action=="boss"));
            Enemy guardian=g.enemies.Find(e=>e!=null&&e.boss);
            Check(guardian!=null&&g.mode=="world"&&
                !g.BossVictoryFeedback,
                "The real shrine spawns an active Boss, not victory feedback");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Main Boss takes the production combat-hit callback");
            hit.Invoke(g,new object[]{guardian,9999f,false,null,null});
            Check(g.Has("boss_1")&&g.BossVictoryFeedback&&
                g.BossVictoryNextStep.Contains("starcore"),
                "Actual lethal Boss contact opens a six-second objective handoff on real gameplay");
            // Corpse only remains ~1.4s, the guidance must outlive it.
            g.enemies.Clear();
            for(int i=0;i<50;i++)g.Tick(.05f,new Point());
            Check(!g.fallen.Exists(f=>f!=null&&f.actor==guardian)&&
                g.BossVictoryFeedback&&
                g.BossVictoryNextStep.Contains("altar"),
                "Normal chapter next-step text survives the full corpse fade");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2&&loaded.Has("boss_1")&&
                !loaded.BossVictoryFeedback&&loaded.BossVictoryNextStep=="",
                "Old V2 boss clear persists, but transient victory panel never leaks into a loaded slot");
            g.Enter(0);
            Check(!g.BossVictoryFeedback&&g.BossVictoryNextStep=="",
                "Travel away from the real completed Boss arena clears the temporary battle handoff");
            g.Enter(1);
            Check(!g.BossVictoryFeedback&&g.BossVictoryNextStep=="",
                "Returning to an already cleared map cannot resurrect yesterday's Boss victory HUD");
        }


        public static void MainBossDeathPreservesOtherLivingShootersAndReflections(Campaign data)
        {
            var g=Fresh(data);
            g.save.english=true;
            g.save.stage=1;g.save.flags.Add("scout_saved");
            g.Enter(1);g.enemies.Clear();g.projectiles.Clear();
            WorldObject altar=Array.Find(g.Map.objects,
                o=>o!=null&&o.kind=="shrine");
            Check(altar!=null,"Actual first region Boss has an original altar");
            g.save.x=altar.x;g.save.y=altar.y;
            g.Talk(altar);
            int start=Array.FindIndex(g.conversation.choices,c=>c.action=="boss");
            Check(start>=0,"Original story unlocked the normal Boss challenge");
            g.Choose(start);
            Enemy guardian=g.enemies.Find(e=>e!=null&&e.boss);
            Check(guardian!=null,"Only genuine altar challenge can start this world Boss");
            Enemy stranger=new Enemy(guardian.id,1,
                guardian.x+3.25f,guardian.y+1.25f,75);
            g.enemies.Add(stranger);
            var fire=typeof(GameCore).GetMethod("Fire",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(fire!=null,"Real combat uses a shared projectile launch method");
            fire.Invoke(g,new object[]{guardian,new Point(-1,0),5f});
            fire.Invoke(g,new object[]{stranger,new Point(-1,0),5f});
            Projectile bossOrb=g.projectiles.Find(p=>p.hostileShooter==guardian);
            Projectile otherArrow=g.projectiles.Find(p=>p.hostileShooter==stranger);
            Check(bossOrb!=null&&otherArrow!=null&&
                !bossOrb.friendly&&!otherArrow.friendly,
                "Both living same-ID actors actually fired their own independent hostile projectiles");
            var reflected=new Projectile{
                hostileShooter=guardian,position=guardian.Position,
                velocity=new Point(1,0),damage=8,life=3,
                visual="arrow",friendly=true};
            g.projectiles.Add(reflected);
            var oldEnvironment=new Projectile{
                position=guardian.Position+new Point(0,1),velocity=new Point(-2,0),
                damage=7,life=3,visual="enemyOrb",friendly=false};
            g.projectiles.Add(oldEnvironment);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            hit.Invoke(g,new object[]{guardian,9999f,false,null,null});
            Check(g.Has("boss_1")&&g.BossVictoryFeedback&&
                !g.enemies.Contains(guardian)&&g.enemies.Contains(stranger),
                "The true chapter guardian dies but its same-ID archer remains alive");
            Check(!g.projectiles.Contains(bossOrb)&&
                g.projectiles.Contains(otherArrow)&&
                g.projectiles.Contains(reflected)&&
                g.projectiles.Contains(oldEnvironment),
                "The dead guardian's hostile orbs retract; an independent living arrow, reflected bolt and unrelated hazard projectile keep their real trajectories");
            Check(g.BossVictoryDangerLine.Contains("ACTIVE THREATS")&&
                g.BossVictoryNextStep.Contains("starcore"),
                "The real victory guidance keeps the altar objective but warns about surviving shooter danger");
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            Check(g.BossVictoryDangerLine==""&&g.BossVictoryFeedback,
                "Only a live nearby threat may append the warning; victory still remains readable after safety returns");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.save.version==2&&restored.Has("boss_1")&&
                !restored.BossVictoryFeedback,
                "All actor ownership was transient and the established V2 story clear stays intact");
        }

        public static void CivilianMedicineMustCrossThreeRealSurveyedRoads(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            g.save.inventory[2]=10;
            g.save.coins=12;
            g.save.english=true;
            int initialCoins=g.save.coins,initialShards=g.save.shards;
            int[] regions={0,3,5};
            foreach(int region in regions)
            {
                g.save.flags.Add("civic_aftercare_waytrail_"+region);
                g.save.flags.Add("civic_waystation_stocked_"+region);
                foreach(WorldObject marker in data.maps[region].objects)
                    if(marker.kind=="waytrail")
                        g.save.flags.Add("waytrail_"+marker.id);
            }
            for(int index=0;index<regions.Length;index++)
            {
                int region=regions[index];
                g.Enter(region);g.enemies.Clear();
                WorldObject origin=null,destination=null;
                foreach(WorldObject marker in g.Map.objects)
                    if(marker.kind=="waytrail")
                    {
                        if(origin==null)origin=marker;
                        else if(destination==null)destination=marker;
                    }
                Check(origin!=null&&destination!=null,
                    "Real stocked regions each have two authentic original public road signs");
                g.save.x=origin.x;g.save.y=origin.y;
                g.Talk(origin);
                int accept=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_courier_start");
                Check(accept>=0&&g.GetQuest("civic_courier").steps.Count==3,
                    "An original physical marker offers the new three-region on-foot courier quest");
                int herbs=g.save.inventory[2];
                if(index==0)
                {
                    g.save.x=origin.x+12f;g.Choose(accept);
                    Check(!g.CivicCourierActive&&g.save.inventory[2]==herbs,
                        "An old faraway menu cannot consume herbs or create invisible cargo");
                    g.save.x=origin.x;g.save.y=origin.y;
                    g.Talk(origin);
                    accept=Array.FindIndex(g.conversation.choices,
                        x=>x.action=="civic_courier_start");
                    g.enemies.Add(new Enemy(99330,0,origin.x+.5f,origin.y,30));
                    g.Choose(accept);
                    Check(!g.CivicCourierActive&&g.save.inventory[2]==herbs,
                        "Live hostile pressure denies the parcel without consuming medical herbs");
                    g.enemies.Clear();
                    g.Talk(origin);
                    accept=Array.FindIndex(g.conversation.choices,
                        x=>x.action=="civic_courier_start");
                }
                g.Choose(accept);
                Enemy liveRoadGuard=g.enemies.Find(g.IsCivicCourierGuard);
                Check(g.CivicCourierActive&&g.save.inventory[2]==herbs-1&&
                    g.save.room==region&&g.save.trackedQuest=="civic_courier"&&
                    liveRoadGuard!=null&&
                    (g.GetQuest("civic_courier").target-
                        liveRoadGuard.Position).Length<.01f&&
                    g.GetQuest("civic_courier").next.Contains("ambush"),
                    "A paid parcel immediately tracks the real ambusher, not a fake completed-sign waypoint");
                int coins=g.save.coins;
                g.ReturnToTown();
                Check(g.save.room==region&&g.CivicCourierActive&&
                    g.save.coins==coins,
                    "Return-to-town fast travel cannot shortcut a physical public-medical delivery");
                g.Talk(origin);
                int blocked=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_waystation_travel_0"||
                        x.action.StartsWith("civic_waystation_local_"));
                Check(blocked<0,
                    "A loaded parcel suppresses every legitimate shortcut option");
                if(index==0)
                {
                    g.PrepareSave();
                    Enemy originalGuard=g.enemies.Find(g.IsCivicCourierGuard);
                    Check(originalGuard!=null&&
                        (g.save.worldEnemies==null||
                         !Array.Exists(g.save.worldEnemies,
                            record=>record!=null&&record.id==originalGuard.id)),
                        "Mission ambusher is NOT serialized as an ordinary V2 roaming enemy");
                    var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
                    Enemy restoredGuard=loaded.enemies.Find(loaded.IsCivicCourierGuard);
                    Check(loaded.save.version==2&&loaded.CivicCourierActive&&
                        loaded.save.inventory[2]==herbs-1&&
                        restoredGuard!=null&&
                        (loaded.GetQuest("civic_courier").target-
                            restoredGuard.Position).Length<.01f,
                        "Old V2 saves reconstruct real ambusher identity and pin it after restoring world");
                    g=loaded;
                    origin=Array.Find(g.Map.objects,
                        x=>x.kind=="waytrail"&&x.id==origin.id);
                    destination=Array.Find(g.Map.objects,
                        x=>x.kind=="waytrail"&&x.id==destination.id);
                    g.save.x=origin.x;g.save.y=origin.y;
                    g.Talk(origin);
                    int giveBack=Array.FindIndex(g.conversation.choices,
                        x=>x.action=="civic_courier_cancel");
                    Check(giveBack>=0,"The exact original staffed source accepts a cancellation");
                    g.Choose(giveBack);
                    Check(!g.CivicCourierActive&&g.save.inventory[2]==herbs&&
                        !g.Has("civic_courier_done_"+region),
                        "Return at the true origin refunds the stake and never forges a completed delivery");
                    g.Talk(origin);
                    accept=Array.FindIndex(g.conversation.choices,
                        x=>x.action=="civic_courier_start");
                    g.Choose(accept);
                }
                g.enemies.RemoveAll(enemy=>!g.IsCivicCourierGuard(enemy));
                g.save.x=destination.x;g.save.y=destination.y;
                g.Talk(destination);
                int deliver=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_courier_deliver");
                Check(deliver>=0,"Only the other physical and previously surveyed marker accepts delivery");
                g.Choose(deliver);
                Check(g.CivicCourierActive&&
                    !g.CivicCourierGuardCleared(region)&&
                    g.save.coins==coins,
                    "Reaching the destination without defeating the real bowman cannot pay a false courier reward");
                Enemy raider=g.enemies.Find(g.IsCivicCourierGuard);
                Check(raider!=null&&raider.hp>0f,
                    "Original assigned hostile is alive, not a fake local combat counter");
                var damage=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                damage.Invoke(g,new object[]{raider,999f,false,null,null});
                Check(g.CivicCourierGuardCleared(region)&&
                    !g.enemies.Exists(g.IsCivicCourierGuard)&&
                    (g.GetQuest("civic_courier").target-
                        new Point(destination.x,destination.y)).Length<.01f&&
                    g.GetQuest("civic_courier").next.Contains("Road secured"),
                    "Real enemy defeat switches the same tracked compass to the actual delivery marker");
                if(index==0)
                {
                    g.PrepareSave();
                    var afterKill=new GameCore(data,CloneSave(g.save));
                    afterKill.Start();
                    Check(afterKill.CivicCourierGuardCleared(region)&&
                        afterKill.CivicCourierActive&&
                        !afterKill.enemies.Exists(afterKill.IsCivicCourierGuard),
                        "V2 reloading AFTER the real raid kill preserves victory without respawning the defeated bowman");
                    g=afterKill;
                    destination=Array.Find(g.Map.objects,
                        x=>x.kind=="waytrail"&&x.id==destination.id);
                    g.save.x=destination.x;g.save.y=destination.y;
                }
                g.Talk(destination);
                deliver=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_courier_deliver");
                g.Choose(deliver);
                Check(!g.CivicCourierActive&&
                    g.Has("civic_courier_done_"+region)&&
                    g.CivicCourierCompleted==index+1&&
                    g.save.coins==coins+18,
                    "Actual other-end contact closes one contract and pays 18 coins exactly once");
                g.Talk(destination);
                Check(Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_courier_deliver"||
                        x.action=="civic_courier_start")<0,
                    "A settled regional courier run cannot be farmed at either marker");
                g.Close();
            }
            Check(g.Has("civic_courier_all")&&
                g.CivicCourierCompleted==3&&
                g.GetQuest("civic_courier").complete&&
                g.save.coins==initialCoins+54&&
                g.save.shards==initialShards+2,
                "Three unique real regional deliveries grant only one final two-shard community reward");
            g.Enter(9);g.enemies.Clear();
            WorldObject guest=Array.Find(g.Map.objects,
                o=>o.kind=="innGuest"&&o.id=="innTraveler");
            Check(guest!=null,
                "The original Hearth Inn night traveler remains the physical reporter");
            g.save.x=guest.x;g.save.y=guest.y;
            ReadInnNews(g,guest);
            Check(g.conversation!=null&&
                g.conversation.textEn.Contains("Foot deliveries: 3/3")&&
                g.conversation.text.Contains("徒步送药：3/3")&&
                g.save.coins==initialCoins+54,
                "The real inn's localized human narrative reflects authentic delivery completions without new rewards");
            g.Close();
            g.PrepareSave();
            var reloadedEnd=new GameCore(data,CloneSave(g.save));reloadedEnd.Start();
            Check(reloadedEnd.save.version==2&&
                reloadedEnd.CivicCourierCompleted==3&&
                reloadedEnd.GetQuest("civic_courier").complete&&
                reloadedEnd.save.coins==initialCoins+54,
                "Original V2 serialization permanently preserves all three honest deliveries");
        }

        public static void CivicShuttleIsExplainedByRealInnAndCompletedBuildingJournal(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            g.save.flags.Add("civic_waystation_stocked_0");
            g.save.flags.Add("civic_waystation_stocked_3");
            // QuestGuide localizes its public next/reward fields through
            // L(...). Select English deliberately for English assertions;
            // the world dialogue below still validates both raw scripts.
            g.save.english=true;
            QuestGuide unfinished=g.GetQuest("civic_waystations");
            Check(!unfinished.locked&&!unfinished.complete&&
                unfinished.next.Contains("Shuttles now run")&&
                unfinished.reward.Contains("Shuttles:"),
                "The genuine journal announces running transit after the second funded station");
            g.Enter(9);g.enemies.Clear();
            WorldObject traveler=Array.Find(g.Map.objects,
                o=>o.kind=="innGuest"&&o.id=="innTraveler");
            Check(traveler!=null,"The original night traveler remains in the real inn hub");
            g.save.x=traveler.x;g.save.y=traveler.y;
            ReadInnNews(g,traveler);
            Check(g.conversation!=null&&
                g.conversation.textEn.Contains("public shuttle")&&
                g.conversation.textEn.Contains("5 coins")&&
                g.conversation.text.Contains("接驳车")&&
                g.save.version==2,
                "The physical inn story explains authenticated fare and active service in both languages");
            g.Close();
            g.save.flags.Add("civic_waystation_stocked_5");
            g.save.flags.Add("civic_waystations_complete");
            QuestGuide completed=g.GetQuest("civic_waystations");
            Check(completed.complete&&
                completed.next.Contains("civic shuttle")&&
                completed.reward.Contains("2 coins/10 energy"),
                "The completed building project retains permanent service and concrete fare information");
        }

        public static void RealWaystationRosterCanBookASecondDialoguePageSecurely(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            foreach(int region in new int[]{0,3})
            {
                g.save.flags.Add("civic_aftercare_waytrail_"+region);
                g.save.flags.Add("civic_waystation_stocked_"+region);
                foreach(WorldObject marker in data.maps[region].objects)
                    if(marker.kind=="waytrail")
                        g.save.flags.Add("waytrail_"+marker.id);
            }
            g.Enter(0);g.enemies.Clear();
            var sign=Array.Find(g.Map.objects,o=>o.kind=="waytrail");
            Check(sign!=null,"An actual original public marker anchors the roster");
            g.save.x=sign.x;g.save.y=sign.y;
            g.save.energy=70f;g.save.coins=30;
            g.Talk(sign);
            int status=Array.FindIndex(g.conversation.choices,
                choice=>choice.action=="civic_waystation_status");
            Check(status>=0,"Original marker offers its existing status interaction");
            g.Choose(status);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.speaker=="居民巡路站"&&
                g.conversation.textEn.Contains("cross-region fares")&&
                Array.FindIndex(g.conversation.choices,
                    choice=>choice.action=="civic_waystation_travel_3")>=0,
                "A genuine staffed marker opens an actionable bilingual paid route roster");
            int ride=Array.FindIndex(g.conversation.choices,
                choice=>choice.action=="civic_waystation_travel_3");
            g.save.x=sign.x+12f;
            g.Choose(ride);
            Check(g.save.room==0&&g.save.coins==30&&g.save.energy==70f,
                "Leaving range invalidates the pinned second-page source without any toll");
            g.save.x=sign.x;g.save.y=sign.y;
            g.Talk(sign);
            status=Array.FindIndex(g.conversation.choices,
                choice=>choice.action=="civic_waystation_status");
            g.Choose(status);
            ride=Array.FindIndex(g.conversation.choices,
                choice=>choice.action=="civic_waystation_travel_3");
            g.Choose(ride);
            Check(g.save.room==3&&g.save.coins==25&&
                Math.Abs(g.save.energy-50f)<.001f,
                "The second-page action rides the same true regional carriage and charges once");
            g.PrepareSave();
            var saved=new GameCore(data,CloneSave(g.save));saved.Start();
            Check(saved.save.version==2&&saved.save.room==3&&
                saved.CivicWaystationCount==2,
                "No pending UI route source is serialized into the original save schema");
        }

        public static void StockedWaystationShuttlesReallyTransferBetweenOriginalRegions(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("civic_conduit_complete");
            g.save.flags.Add("civic_aftercare_started");
            g.save.flags.Add("civic_aftercare_published");
            foreach(int region in new int[]{0,3,5})
            {
                g.save.flags.Add("civic_aftercare_waytrail_"+region);
                g.save.flags.Add("civic_waystation_stocked_"+region);
                foreach(WorldObject sign in data.maps[region].objects)
                    if(sign.kind=="waytrail")
                        g.save.flags.Add("waytrail_"+sign.id);
            }
            g.save.flags.Add("civic_waystations_complete");
            g.save.coins=23;g.save.energy=80f;
            g.Enter(0);g.enemies.Clear();
            WorldObject start=Array.Find(g.Map.objects,x=>x.kind=="waytrail");
            Check(start!=null,"The bus is offered only from a real authored public marker");
            g.save.x=start.x;g.save.y=start.y;
            g.save.trackedQuest="civic_waystations";
            g.Talk(start);
            int travel=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_waystation_travel_3");
            Check(travel>=0&&
                Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_waystation_travel_5")>=0,
                "Published fully stocked stations offer two physical destinations");
            int paid=g.save.coins;float spent=g.save.energy;
            // Stale dialogue cannot be a remote region-entry exploit.
            g.save.x=start.x+14f;g.Choose(travel);
            Check(g.save.room==0&&g.save.coins==paid&&
                Math.Abs(g.save.energy-spent)<.001f,
                "Walking away rejects a stale transit ticket without charging");
            g.save.x=start.x;g.save.y=start.y;
            g.Talk(start);
            travel=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_waystation_travel_3");
            g.save.energy=19f;g.Choose(travel);
            Check(g.save.room==0&&g.save.coins==paid&&g.save.energy==19f,
                "Insufficient stamina cancels shuttle without a partial coin toll");
            g.save.energy=80f;g.save.coins=4;
            g.Talk(start);travel=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_waystation_travel_3");
            g.Choose(travel);
            Check(g.save.room==0&&g.save.coins==4&&g.save.energy==80f,
                "Insufficient currency never consumes the travel energy");
            g.save.coins=23;
            g.Talk(start);travel=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_waystation_travel_3");
            g.enemies.Add(new Enemy(98101,0,start.x+.4f,start.y,40));
            g.Choose(travel);
            Check(g.save.room==0&&g.save.coins==23&&g.save.energy==80f,
                "Real combat danger blocks departures from the staffed road");
            g.enemies.Clear();
            g.Talk(start);travel=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_waystation_travel_3");
            g.save.flags.Remove("civic_waystation_stocked_3");
            g.Choose(travel);
            Check(g.save.room==0&&g.save.coins==23&&g.save.energy==80f,
                "Losing destination supply revokes an already cached route choice");
            g.save.flags.Add("civic_waystation_stocked_3");
            g.Talk(start);travel=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_waystation_travel_3");
            g.Choose(travel);
            Check(g.save.room==3&&g.save.coins==18&&
                Math.Abs(g.save.energy-60f)<.001f&&
                g.save.trackedQuest=="civic_waystations"&&g.CanStand(g.Position),
                "Accepted shuttle pays once and uses the real region transition and valid landing");
            bool byMarker=false;
            foreach(WorldObject marker in g.Map.objects)
                if(marker.kind=="waytrail"&&
                    (g.Position-new Point(marker.x,marker.y)).Length<1.75f)
                    byMarker=true;
            // If tide/sealed terrain makes the destination marker unsafe,
            // the ordinary regional entrance remains the safe fallback.
            Check(byMarker||((g.Position-
                new Point(g.Map.spawnX,g.Map.spawnY)).Length<.01f),
                "Transit cannot plant the hero in an arbitrary untracked unsafe foot");
            // A second real route demonstrates bidirectional civic travel.
            WorldObject inRegion=Array.Find(g.Map.objects,x=>x.kind=="waytrail");
            Check(inRegion!=null,"Destination has its actual original supply sign");
            g.save.x=inRegion.x;g.save.y=inRegion.y;g.enemies.Clear();
            g.Talk(inRegion);
            int onward=Array.FindIndex(g.conversation.choices,
                x=>x.action=="civic_waystation_travel_5");
            Check(onward>=0&&
                Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_waystation_travel_3")<0,
                "An arrived traveler sees only other stocked destinations");
            g.Choose(onward);
            Check(g.save.room==5&&g.save.coins==13&&
                Math.Abs(g.save.energy-40f)<.001f,
                "Second paid shuttle reaches another existing region without hidden repeated fees");
            g.PrepareSave();
            var reload=new GameCore(data,CloneSave(g.save));reload.Start();
            Check(reload.save.version==2&&reload.save.room==5&&
                reload.save.coins==13&&reload.CivicWaystationCount==3&&
                reload.save.trackedQuest=="civic_waystations",
                "Real visited rooms, currencies, history and quest focus survive the original V2 save format");
        }

        public static void StockedWaystationOffersReachableEmergencyCompass(Campaign data)
        {
            GameCore g=Unlocked(data);
            g.save.flags.Add("civic_aftercare_published");
            g.Enter(0);g.enemies.Clear();
            WorldObject sign=Array.Find(g.Map.objects,o=>o.kind=="waytrail");
            Check(sign!=null,"The actual civic map provides original road signs");
            // Choose an accessible real-world foot outside the service
            // radius; never set a fake destination or disable collision.
            Point? far=null;
            for(int radius=4;radius<=12&&!far.HasValue;radius++)
                for(int dx=-radius;dx<=radius&&!far.HasValue;dx++)
                    for(int dy=-radius;dy<=radius&&!far.HasValue;dy++)
                    {
                        if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                        Point site=new Point(sign.x+dx,sign.y+dy);
                        if(!g.CanStand(site))continue;
                        g.save.x=site.x;g.save.y=site.y;
                        if(g.NavigationPath(new Point(sign.x,sign.y)).Count>0)
                            far=site;
                    }
            Check(far.HasValue,
                "The test uses a real walkable player foot outside the original marker refuge");
            g.save.x=far.Value.x;g.save.y=far.Value.y;
            g.save.showGuidance=false;
            g.save.health=g.save.maxHealth*.2f;
            g.save.trackedQuest="main";
            var guide=typeof(GameCore).GetMethod(
                "EmergencyCivicWaystationGuideTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(guide!=null,
                "The production urgent compass uses a single source-owned route selector");
            Check(!((Point?)guide.Invoke(g,null)).HasValue,
                "An unpublished/unsupplied region never fakes a free refuge");
            g.save.flags.Add("civic_waystation_stocked_0");
            Point? target=(Point?)guide.Invoke(g,null);
            Check(target.HasValue&&
                Array.Exists(g.Map.objects,o=>o.kind=="waytrail"&&
                    (new Point(o.x,o.y)-target.Value).Length<.001f)&&
                g.NavigationTarget().HasValue&&
                (g.NavigationTarget().Value-target.Value).Length<.001f&&
                g.save.trackedQuest=="main",
                "A critically injured traveler is routed to a real supplied, reachable marker without changing the journal");
            g.enemies.Add(new Enemy(940001,0,g.Position.x+.5f,g.Position.y,30f));
            Check(!((Point?)guide.Invoke(g,null)).HasValue,
                "Real danger disables the emergency retreat suggestion until it is safe to rest");
            g.enemies.Clear();
            g.save.health=g.save.maxHealth;
            g.save.heat=0;g.save.cold=0;
            Check(!((Point?)guide.Invoke(g,null)).HasValue,
                "Healthy travelers retain their ordinary map pin or quest destination");
            g.save.health=8f;
            g.save.x=target.Value.x;g.save.y=target.Value.y;
            Check(g.CivicWaystationNear&&
                !((Point?)guide.Invoke(g,null)).HasValue,
                "The compass retires the emergency arrow upon actually reaching the stocked service radius");
            g.save.flags.Remove("civic_waystation_stocked_0");
            Check(!((Point?)guide.Invoke(g,null)).HasValue,
                "Missing source supplies immediately revoke emergency navigation");
        }

        public static void AuthoredWaystationRecoveryRequiresLocalSafeWorldTime(Campaign data)
        {
            GameCore g=Unlocked(data);
            g.save.flags.Add("civic_aftercare_published");
            g.save.flags.Add("civic_waystation_stocked_0");
            g.Enter(0);g.enemies.Clear();
            WorldObject marker=Array.Find(g.Map.objects,o=>o.kind=="waytrail");
            Check(marker!=null,"Town has the original civic route marker");
            g.save.x=marker.x;g.save.y=marker.y;
            Check(g.CivicWaystationNear,
                "The service is tied to the existing walkable original marker");
            g.save.health=32f;g.save.energy=20f;
            g.save.heat=48f;g.save.cold=47f;
            float hp=g.save.health,energy=g.save.energy;
            float heat=g.save.heat,cold=g.save.cold;
            g.Tick(.05f,new Point());
            Check(g.save.health>hp&&g.save.energy>energy&&
                g.save.heat<heat&&g.save.cold<cold,
                "Supplied site delivers real health, stamina and climate recovery in live world time");
            g.save.x=marker.x+14f;
            Check(!g.CivicWaystationNear,
                "An off-route hero cannot claim a remote civic refuge");
            g.save.x=marker.x;g.save.y=marker.y;
            g.save.health=45f;
            g.enemies.Add(new Enemy(910121,0,marker.x+.4f,marker.y,30f));
            hp=g.save.health;
            g.Tick(.05f,new Point());
            Check(g.save.health<=hp+.00001f,
                "Hostile pressure suppresses the bonus instead of granting combat healing");
            g.enemies.Clear();
            g.mode="inventory";
            hp=g.save.health;energy=g.save.energy;
            g.Tick(.05f,new Point());
            Check(g.save.health==hp&&g.save.energy==energy,
                "Menu time never mints free stocked-station recovery");
        }

        public static void PublishedRoadAuditOffersRealCostedCivilianFirstAid(Campaign data)
        {
            int[] regions={0,3,5};
            foreach(int region in regions)
            {
                GameCore g=Fresh(data);
                g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.save.room=region;g.enemies.Clear();
                g.save.flags.Add("civic_conduit_complete");
                g.save.flags.Add("civic_aftercare_started");
                g.save.flags.Add("civic_aftercare_waytrail_"+region);
                WorldObject marker=Array.Find(g.Map.objects,
                    o=>o.kind=="waytrail");
                Check(marker!=null,"A genuine existing road marker is present in every audited region");
                g.save.flags.Add("waytrail_"+marker.id);
                g.save.x=marker.x;g.save.y=marker.y;
                g.save.inventory[2]=2;
                g.save.health=18;g.save.heat=38;g.save.cold=30;
                g.Talk(marker);
                Check(Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_aftercare_fieldaid")<0,
                    "An unpublished route audit cannot provide the new staffed field aid");
                g.Close();
                g.save.flags.Add("civic_aftercare_published");
                g.Talk(marker);
                int aid=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_aftercare_fieldaid");
                Check(aid>=0&&g.conversation.text.Contains("一份药草")&&
                    g.conversation.textEn.Contains("one herb"),
                    "Published audit connects one authentic local trail marker to bilingual costed gameplay");
                // A stale choice may not heal the traveler from the far side
                // of the map, or spend herbs on an invalid interaction.
                g.save.x=marker.x+14;
                g.Choose(aid);
                Check(g.save.inventory[2]==2&&g.save.health==18,
                    "Walking away from a field-aid dialogue never consumes a herb or grants free health");
                g.save.x=marker.x;g.save.y=marker.y;g.Talk(marker);
                aid=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_aftercare_fieldaid");
                g.enemies.Add(new Enemy(88001,0,marker.x+.5f,marker.y,30));
                g.Choose(aid);
                Check(g.save.inventory[2]==2&&g.save.health==18,
                    "Real hostile pressure blocks field triage instead of permitting combat-time healing");
                g.enemies.Clear();
                g.Talk(marker);
                aid=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_aftercare_fieldaid");
                g.Choose(aid);
                Check(g.save.inventory[2]==1&&
                    Math.Abs(g.save.health-54)<.01f&&
                    Math.Abs(g.save.heat-13)<.01f&&
                    Math.Abs(g.save.cold-5)<.01f,
                    "Paid field aid genuinely restores 36 HP and relieves 25 exposure with one real herb");
                float healed=g.save.health;
                g.save.health=g.save.maxHealth;
                g.Talk(marker);
                aid=Array.FindIndex(g.conversation.choices,
                    x=>x.action=="civic_aftercare_fieldaid");
                g.Choose(aid);
                Check(g.save.inventory[2]==1&&g.save.health==g.save.maxHealth,
                    "A fully recovered traveler cannot waste an extra medicinal herb");
                // The new remedy has no SaveData field and does not change
                // the original quest publications or world entry gates.
                GameCore reload=new GameCore(data,g.save);
                Check(reload.save.version==2&&
                    reload.Has("civic_aftercare_published")&&
                    reload.save.room==region&&reload.save.inventory[2]==1,
                    "Real v2 reload persists the spent herb and earned audit without a new save schema");
            }
        }

        public static void StewCooldownPreventsCombatSpamAndDoesNotWasteFullMeals(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.inventory[6]=4;
            g.save.health=10;g.save.energy=20;
            int start=g.save.inventory[6];
            g.Eat();
            Check(g.save.inventory[6]==start-1&&
                Math.Abs(g.save.health-65)<.01f&&g.save.energy==100&&
                g.StewCooldownLeft>=2.99f&&g.StewAttackBuffLeft>89,
                "A real stew restores its established resources, buff and starts one recovery clock");
            float buffUntil=g.save.foodUntil;
            g.Eat();
            Check(g.save.inventory[6]==start-1&&
                Math.Abs(g.save.foodUntil-buffUntil)<.001f&&
                g.StewCooldownLeft>2.9f,
                "Repeated pressed F during one combat beat never consumes a second stew or extends the attack buff");
            g.mode="inventory";g.Tick(.05f,new Point());
            Check(g.StewCooldownLeft>=2.99f,
                "Opening the inventory pauses recovery instead of silently advancing food use");
            g.mode="world";
            Advance(g,3.1f);
            Check(g.StewCooldownLeft==0f,
                "Real world frames unlock another meal after exactly three seconds");
            g.save.health=g.save.maxHealth;g.save.energy=100;
            g.Eat();
            Check(g.save.inventory[6]==start-1&&
                Math.Abs(g.save.foodUntil-buffUntil)<.001f,
                "Full health, full energy and a live attack buff preserve costly crafted food");
            g.save.foodUntil=g.save.totalTime-.01f;
            g.Eat();
            Check(g.save.inventory[6]==start-2&&g.StewAttackBuffLeft>89f&&
                g.StewCooldownLeft>=2.99f,
                "A fully healed warrior may consume one meal to regain a genuinely expired attack buff");
            g.Load(g.save);
            Check(g.save.version==2&&g.StewCooldownLeft==0f,
                "Food cooldown is transient and cannot contaminate a loaded v2 slot");
        }


        public static void TeaConsumptionIsTacticalAndProtectsRareSupplies(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.tea=5; g.save.energy=20;
            g.save.heat=45;g.save.cold=27;
            Check(g.CanDrinkTea&&g.TeaProtectionLeft==0f,"Unprotected traveler can drink tea");
            int before=g.save.tea;
            g.DrinkTea();
            Check(g.save.tea==before-1&&g.save.energy==100&&
                g.save.heat==0&&g.save.cold==0&&
                g.TeaCooldownLeft>=GameCore.TeaCooldownSeconds-.01f&&
                g.TeaProtectionLeft>179f&&!g.CanDrinkTea,
                "One cup heals exposure and restores real energy with a finite combat recovery");
            float paidProtectionUntil=g.save.teaUntil;
            g.DrinkTea();
            Check(g.save.tea==before-1&&g.save.teaUntil==paidProtectionUntil,
                "Rapid repeated tea input never pays or extends the same recovery");
            g.mode="inventory";g.Tick(.05f,new Point());
            Check(g.TeaCooldownLeft>=GameCore.TeaCooldownSeconds-.01f,
                "Inventory is a paused state; no hidden tea cooldown progression");
            g.mode="camp";g.DrinkTea();
            Check(g.save.tea==before-1,
                "Camp inventory cannot bypass a pending tea recovery");
            g.mode="world"; Advance(g,GameCore.TeaCooldownSeconds+.1f);
            Check(g.TeaCooldownLeft==0f&&!g.CanDrinkTea,
                "A completed tea recovery cannot waste another cup while protected and nourished");
            g.DrinkTea();
            Check(g.save.tea==before-1&&g.save.teaUntil==paidProtectionUntil,
                "Attempting tea at full benefit preserves both stock and paid expiry");
            g.save.energy=63;
            Check(g.CanDrinkTea,"A fresh real stamina deficit permits emergency tea even under climate protection");
            g.mode="inventory";g.DrinkTea();
            Check(g.save.tea==before-2&&g.save.energy==100&&
                g.TeaCooldownLeft>=GameCore.TeaCooldownSeconds-.01f,
                "Inventory tea uses the same authoritative transaction and recovery clock");
            int heldStock=g.save.tea;
            g.Load(g.save);
            Check(g.save.version==2&&g.TeaCooldownLeft==0f&&
                g.save.tea==heldStock&&g.TeaProtectionLeft>0f&&!g.CanDrinkTea,
                "Loaded v2 slot keeps paid tea protection but never persists a transient recovery lock");
            g.save.teaUntil=g.save.totalTime+GameCore.TeaRefreshWindow-.5f;
            Check(g.CanDrinkTea,"A physically imminent climate-expiry crossing permits deliberate early refill");
            g.DrinkTea();
            Check(g.save.tea==heldStock-1&&g.TeaProtectionLeft>179f,
                "Real tea within refresh window renews protection without changing save schema");
            g.save.tea=0;
            g.Load(g.save);
            Check(!g.CanDrinkTea,"A load cannot invent tea when stock is exhausted");
        }


        public static void InnRestSettlesRealConsumableRecoveryWithoutDuplicatingStock(Campaign data)
        {
            GameCore g=Fresh(data);
            g.save.tea=3;g.save.inventory[6]=3;
            g.save.health=35;g.save.energy=20;
            g.Eat();
            g.save.energy=15;
            g.DrinkTea();
            Check(g.TeaCooldownLeft>=GameCore.TeaCooldownSeconds-.01f&&
                g.StewCooldownLeft>=2.99f,
                "Drinking one tea and eating one stew establishes two real recovery windows");
            int remainingTea=g.save.tea,remainingStew=g.save.inventory[6];
            float teaUntil=g.save.teaUntil,foodUntil=g.save.foodUntil;
            float before=g.save.totalTime;
            Action(g,"rest","rest");
            Check(g.save.totalTime>=before+44.99f,
                "Paid safe inn rest advances the established world clock");
            Check(g.TeaCooldownLeft==0f&&g.StewCooldownLeft==0f,
                "A genuine 45-second rest clears both short consumable recovery windows");
            Check(g.save.tea==remainingTea&&g.save.inventory[6]==remainingStew&&
                g.save.teaUntil==teaUntil&&g.save.foodUntil==foodUntil,
                "Rest settles clocks without giving free supplies or artificially extending buffs");
            g.mode="world";g.save.health=g.save.maxHealth;g.save.energy=100;
            g.DrinkTea();g.Eat();
            Check(g.save.tea==remainingTea&&g.save.inventory[6]==remainingStew,
                "Rest does not allow wasting fully effective consumables");
        }

        public static void DailyLife(Campaign data)
        {
            var g = Fresh(data); Action(g, "garden", "plant"); Check(g.save.plantedAt == 0 && g.save.inventory[7] == 2, "Planting");
            Action(g, "garden", "harvest"); Check(g.save.plantedAt >= 0, "Cannot harvest early");
            Action(g, "rest", "rest"); Action(g, "rest", "rest");
            int herbs = g.save.inventory[2]; Action(g, "garden", "harvest"); Check(g.save.inventory[2] == herbs + 5 && g.save.plantedAt < 0, "Harvest after elapsed time");
            for (int fish = 0; fish < 3; fish++)
            {
                Action(g, "fish", "fish");
                for (int hit = 0; hit < 3; hit++)
                {
                    // Wait through the actual timing simulation rather than patching the cursor.
                    int frames = 0;
                    do { g.Tick(.02f, new Point()); frames++; }
                    while ((!g.FishLineReady ||
                        Math.Abs(g.fishCursor - g.FishTarget) > .07f ||
                        frames < 5) && frames < 1400);
                    Check(frames < 1400, "A real drifting fish bite is reachable before the mini-game times out");
                    g.FishHit();
                }
                Check(g.mode == "world", "Fishing should finish after three good pulls");
            }
            Check(g.save.inventory[3] == 3, "Fishing rewards"); Action(g, "fish", "ally2"); Check(g.Has("ally_2") && g.save.inventory[3] == 0, "Fisher's commission");
            Action(g, "fish", "fish"); g.Close(); Check(g.save.inventory[3] == 0, "Cancelling fishing must not mint a reward");
            g.save.inventory[3] = 1; Action(g, "cook", "cook");
            g.save.health = 10; int meal = g.save.inventory[6]; g.Eat(); Check(g.save.health == 65 && g.save.inventory[6] == meal - 1 && g.save.foodUntil > g.save.totalTime, "Cooking and food buff");
        }
        public static void GatesAndGathering(Campaign data)
        {
            var g = Fresh(data); g.Enter(3); Check(g.save.room == 0, "Locked routes must remain locked");
            g.Enter(1); TalkNear(g, "scout"); Check(!g.GateOpen, "Scout cannot open the gate before the rescue"); g.Close();
            int herbs = g.save.inventory[2]; var node = Object(g, "herb"); TalkNearObject(g, node); g.Talk(node); Check(g.save.inventory[2] == herbs + 3, "A resource node yields once per rest");
            g.save.kills[1] = 4; Action(g, "scout", "scout"); Check(g.GateOpen, "Rescue opens forest gate");
            g.save.stage = 1; g.Enter(2); TalkNear(g, "rune2"); Check(g.runeProgress == 0, "Wrong rune resets puzzle");
            TalkNear(g, "rune0"); TalkNear(g, "rune2"); TalkNear(g, "rune1"); Check(g.GateOpen, "Correct rune order opens ruins");
            g.save.stage = 2; g.Enter(3); g.save.inventory[4] = 0; Action(g, "engine", "engine"); Check(!g.GateOpen, "Mine repair requires parts");
            TalkNear(g, "ore"); Action(g, "engine", "engine"); Check(g.GateOpen && g.save.inventory[4] == 0, "Gathered parts repair elevator");
            for (int room = 0; room < 5; room++)
            {
                g.save.stage = 3; g.save.flags.Add("seal_" + room); g.Enter(room);
                var queue = new Queue<int>(); var seen = new HashSet<int>();
                int width = g.Map.rows[0].Length, height = g.Map.rows.Length;
                int start = (int)g.save.y * width + (int)g.save.x; queue.Enqueue(start); seen.Add(start);
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue(), x = cell % width, y = cell / width;
                    int[,] offsets = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
                    for (int i = 0; i < 4; i++)
                    {
                        int xx = x + offsets[i, 0], yy = y + offsets[i, 1], next = yy * width + xx;
                        if (xx < 0 || xx >= width || yy < 0 || yy >= height || g.Blocked(xx + .5f, yy + .5f) || seen.Contains(next)) continue;
                        seen.Add(next); queue.Enqueue(next);
                    }
                }
                foreach (var o in g.Map.objects)
                {
                    bool reachable = false;
                    foreach (int cell in seen) if ((new Point(cell % width + .5f, cell / width + .5f) - new Point(o.x, o.y)).Length < 1.7f) { reachable = true; break; }
                    Check(reachable, "Unreachable object in room " + room + ": " + o.id);
                }
            }
        }
        public static void CorruptEnemySnapshotsCannotInjectOrHideValidPatrols(Campaign data)
        {
            var old=new SaveData {
                room=1,snapshotRoom=1,
                worldEnemies=new [] {
                    new EnemyRecord { id=5,kind=0,x=float.NaN,y=14,hp=30,maxHp=30 },
                    new EnemyRecord { id=5,kind=0,x=13,y=14,hp=20,maxHp=30 },
                    new EnemyRecord { id=6,kind=1,x=9999,y=9999,hp=30,maxHp=30 },
                    new EnemyRecord { id=7,kind=2,x=14,y=14,hp=65,maxHp=80 }
                }
            };
            var restored=new GameCore(data,old);
            Check(restored.enemies.Count==3,
                "Malformed duplicate loses to a valid same-ID record while the old safe-location recovery still saves isolated off-map actors");
            Enemy valid=restored.enemies.Find(e=>e.id==5);
            Check(valid!=null&&valid.hp==20&&valid.maxHp==30&&
                  (valid.Position-new Point(13,14)).Length<.001f,
                "A malformed duplicate cannot reserve an ID before its genuine original saved entry");
            Enemy rescued=restored.enemies.Find(e=>e.id==6);
            Check(restored.enemies.Exists(e=>e.id==7)&&rescued!=null&&
                  rescued.hp==30&&restored.CanStand(rescued.Position),
                "An isolated damaged-position enemy still recovers with its saved identity and health");
            restored.PrepareSave();
            var again=new GameCore(data,CloneSave(restored.save));
            Check(again.enemies.Count==3&&again.enemies.Exists(e=>e.id==5)&&
                  again.enemies.Exists(e=>e.id==6)&&again.enemies.Exists(e=>e.id==7),
                "Deduplicated and rescued patrols persist normally across the next old-format save and reload");
        }
        public static void SaveValidation(Campaign data)
        {
            var bad = new SaveData { room = 99, stage = -1, x = float.NaN, y = -300, health = float.PositiveInfinity, coins = -100, inventory = null, talents = null, kills = null, flags = null };
            var g = new GameCore(data, bad); Check(g.save.room == 0 && g.save.x == 18 && g.save.coins == 0 && g.save.health == 120, "Damaged saves are sanitized");
            bool rejected = false; try { new GameCore(data, new SaveData { version = 900 }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unsupported save schema is rejected");
            var legacyClass=new SaveData {
                profession=500,weapon=2,technique=1,
                flags=new List<string> { "weapon_2", "technique_2" }
            };
            var corrected=new GameCore(data,legacyClass);
            Check(corrected.save.profession==2&&corrected.save.weapon==2&&
                  corrected.save.technique==1,
                "A corrupted profession number is repaired before validating a legitimately unlocked alternate skill");
            var noUnlock=new SaveData { profession=500,technique=1 };
            var denied=new GameCore(data,noUnlock);
            Check(denied.save.profession==2&&denied.save.technique==0,
                "Early class correction must never unlock an absent alternate profession skill");

            var negativeXp=new GameCore(data,new SaveData{level=6,xp=-900});
            Check(negativeXp.save.level==6&&negativeXp.save.xp==0,
                "Negative XP cannot persist in a valid old v2 save");
            var levelCap=new GameCore(data,new SaveData{level=30,xp=int.MaxValue});
            Check(levelCap.save.level==30&&levelCap.save.xp==0,
                "Already-maxed legacy saves cannot preserve overflowing XP");
            var justBefore=new GameCore(data,new SaveData{level=29,xp=1159});
            Check(justBefore.save.xp==1159,
                "Ordinary near-level progress is preserved byte-for-byte on load");
            justBefore.enemies.Clear();
            var finalFoe=new Enemy(88131,0,18,14,30);
            justBefore.enemies.Add(finalFoe);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            hit.Invoke(justBefore,new object[]{finalFoe,99999f,false,null,null});
            Check(justBefore.save.level==30&&justBefore.save.xp==0,
                "Real combat reaching level 30 resets obsolete excess XP");
            var grant=typeof(GameCore).GetMethod("AddXp",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            grant.Invoke(justBefore,new object[]{int.MaxValue});
            Check(justBefore.save.level==30&&justBefore.save.xp==0,
                "At the cap huge quest/bounty payouts cannot overflow or unlock phantom levels");
            g.Enter(1); g.save.x = 2.3f; g.save.y = 14; g.Dash(new Point(-1, 0));
            for (int i = 0; i < 20; i++) g.Tick(.02f, new Point(-1, 0));
            Check(g.save.x >= 2.2f, "Dash cannot tunnel through the map boundary");
        }
        private static void Combat(GameCore g, bool bossOnly)
        {
            int frames = 0, startingRoom = g.save.room;
            while (g.enemies.Exists(e => !bossOnly || e.boss) && frames++ < 30000)
            {
                Check(g.save.room == startingRoom, "Combat bot died in room " + startingRoom);
                Enemy target = null; float distance = float.MaxValue;
                foreach (Enemy e in g.enemies)
                { if (bossOnly && !e.boss) continue; float d = (e.Position - g.Position).Length; if (d < distance) { target = e; distance = d; } }
                if (target == null) break;
                Point toward = g.DirectionTo(g.Position, target.Position);
                // A ranger is not a melee attacker. Stopping only 1.4 tiles
                // from the foe puts the real bow's muzzle inside the enemy's
                // body or a wall when the target backs into arena corners.
                // Use genuine ranged spacing in the automated combat pilot
                // so the long expedition tests exercise real arrow flight
                // instead of an endless point-blank, obstructed shot.
                bool ranged = g.save.profession==1&&g.save.weapon==1&&!target.boss;
                Point movement = distance > (ranged?3.0f:1.4f) ?
                    toward : ranged && distance < 2.3f ?
                    toward*-1f : new Point();
                if (target.state == 1 && (target.boss || target.kind == 2) && (g.Position - target.target).Length < (target.boss ? 3.4f : 2))
                { movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0); g.Dash(movement); }
                foreach (Hazard h in g.hazards) if (h.timer < .5f && (g.Position - h.position).Length < h.radius + .5f)
                { movement = (g.Position - h.position).Length > .05f ? (g.Position - h.position).Normalized : new Point(-1, 0); g.Dash(movement); break; }
                g.Tick(.02f, movement);
                // A real player cannot hit an enemy across an entire
                // procedural gallery. Spamming offscreen melee attacks at
                // every path step repeatedly locks the test pilot in a swing
                // and turns a walkable detour into a false combat softlock.
                // Let the bot actually cross the physical route first.
                if (distance <= (ranged ? 7f : 2.6f))
                    g.Attack(target.Position - g.Position);
                if (g.save.health < g.save.maxHealth * .8f) g.CommandCompanion();
                if (distance < 3) g.Skill();
                if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) g.Eat();
                if (g.mode == "dialog" && !g.Has("boss_4")) g.Close();
                if (startingRoom == 4 && g.Has("boss_4")) break;
            }
            string remaining = "";
            foreach (var e in g.enemies) remaining += " enemy " + e.kind + " @" + e.x + "," + e.y + " hp=" + e.hp;
            Check(frames < 30000, "Combat failed to converge in room " + g.save.room + " floor=" + g.save.abyssFloor + " profession=" + g.save.profession + " seed=" + g.save.seed + " mode=" + g.mode + " hitstop=" + g.hitStop + " death=" + g.deathTime + " direction=" + (g.enemies.Count>0?g.DirectionTo(g.Position,g.enemies[0].Position).x:0) + "," + (g.enemies.Count>0?g.DirectionTo(g.Position,g.enemies[0].Position).y:0) + " map=" + string.Join("/", g.Map.rows) + " player=" + g.save.x + "," + g.save.y + remaining);
        }
        public static void CompleteCampaign(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) CampaignRun(data, profession);
        }
        private static void CampaignRun(Campaign data, int profession)
        {
            var g = Fresh(data, profession); // Standard difficulty and starter equipment, with public gameplay actions.
            for (int room = 1; room <= 4; room++)
            {
                g.Enter(room); Check(g.save.room == room, "Chapter route unlock");
                Combat(g, false);
                if (room == 1) Action(g, "scout", "scout");
                if (room == 2) { TalkNear(g, "rune0"); TalkNear(g, "rune2"); TalkNear(g, "rune1"); }
                if (room == 3) { TalkNear(g, "ore"); Action(g, "engine", "engine"); }
                Check(g.GateOpen, "Chapter gate");
                AwaitArenaOpening(g); // Hit stop pauses the same opening clock; wait for actual collision.
                // The original direct test teleport (28,10) places the
                // hero's full-body footprint partly in room 2's water
                // even though its CENTER tile is dry. Full campaign QA
                // must fight from valid ground like actual player input.
                g.save.x = 28.5f; g.save.y = 10.5f;
                Check(g.CanStand(g.Position),
                    "Complete campaign boss setup must place the real hero footprint on legal dry ground");
                Action(g, "shrine", "boss"); Combat(g, true);
                Check(g.Has("boss_" + room),
                    "Guardian defeated: room=" + room + " profession=" + profession +
                    " actualRoom=" + g.save.room + " dying=" + g.Dying +
                    " mode=" + g.mode + " health=" + g.save.health +
                    " enemies=" + g.enemies.Count + " state=" +
                    (g.enemies.Count > 0 ? g.enemies[0].id.ToString() : "empty"));
                if (room < 4) { Action(g, "shrine", "goto0"); Action(g, "guild", "deliver"); Check(g.save.stage == room, "Core delivered"); }
            }
            for (int frame=0; frame<200 && g.mode=="world"; frame++) g.Tick(.02f,new Point());
            Check(g.mode == "dialog" && !g.fallen.Exists(f=>f.actor.boss), "Final conversation follows the completed Boss death clock, including hit stop");
            int endingChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending1") endingChoice = i;
            g.Choose(endingChoice); Check(g.save.ending == 1 && g.Has("completed"), "First ending"); g.Choose(0); Check(g.save.room == 0, "Postgame returns to town");
            // Complete the optional commissions after the campaign and verify the earned alternative.
            g.save.inventory[2] += 6; g.save.inventory[3] += 3; g.save.inventory[4] += 5;
            Action(g, "garden", "ally1"); Action(g, "fish", "ally2"); Action(g, "forge", "ally3");
            g.Enter(4); TalkNear(g, "shrine"); endingChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending3") endingChoice = i;
            Check(endingChoice >= 0, "Three allies unlock the alternative ending"); g.Choose(endingChoice); Check(g.save.ending == 3, "Third ending");
            g.Close(); TalkNear(g, "shrine");
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending2") { g.Choose(i); break; }
            Check(g.save.ending == 2, "Second ending");
        }


        public static void ExpandedHearthInnArchiveIsConnectedAndRewardsVerifiedExploration(Campaign data)
        {
            MapData inn=data.maps[9];
            Check(inn.rows.Length==40&&inn.rows[0].Length==60,"Expanded Hearth Inn preserves rooms while adding public archive wings");
            GameCore game=new GameCore(data);game.save.room=9;
            var queue=new System.Collections.Generic.Queue<int>();
            var visited=new System.Collections.Generic.HashSet<int>();
            int w=inn.rows[0].Length,h=inn.rows.Length;
            int start=(int)inn.spawnY*w+(int)inn.spawnX;
            queue.Enqueue(start);visited.Add(start);
            int[,] offsets={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int cell=queue.Dequeue(),x=cell%w,y=cell/w;
                for(int d=0;d<4;d++)
                {
                    int xx=x+offsets[d,0],yy=y+offsets[d,1],next=yy*w+xx;
                    if(xx<0||xx>=w||yy<0||yy>=h||visited.Contains(next)||
                        !game.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    visited.Add(next);queue.Enqueue(next);
                }
            }
            WorldObject archive=Array.Find(inn.objects,o=>o.kind=="annexinnarchive");
            WorldObject letter=Array.Find(inn.objects,o=>o.kind=="annexinnmessage");
            Check(archive!=null&&letter!=null&&visited.Contains((int)archive.y*w+(int)archive.x)&&
                visited.Contains((int)letter.y*w+(int)letter.x),
                "Both new inn annex rooms connect to original spawn through real collision");
            game.save.x=archive.x;game.save.y=archive.y;
            game.Talk(archive);
            int idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            Check(idx>=0,"Inn archive can be filed with a real interactable action");
            int coins=game.save.coins;game.Choose(idx);
            Check(!game.Has("annex_archive_complete")&&game.save.coins==coins,
                "Incomplete outdoor discovery cannot falsely claim the published atlas");
            for(int room=0;room<8;room++)
            {
                game.save.flags.Add("annex_camp_annex_camp_"+room);
                game.save.flags.Add("annex_vista_annex_vista_"+room);
                game.save.flags.Add("annex_cache_annex_cache_"+room+"_a");
                game.save.flags.Add("annex_cache_annex_cache_"+room+"_b");
            }
            game.Talk(archive);
            idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            game.Choose(idx);
            Check(game.Has("annex_archive_complete")&&game.save.coins==coins+60&&
                game.GetQuest("annex_explorer").complete,
                "Physically filing the complete atlas rewards real resources and finishes the quest");
            SaveData save=game.save;
            GameCore loaded=new GameCore(data,save);
            Check(save.version==2&&loaded.Has("annex_archive_complete"),
                "Public atlas persists in old save slots");
            game.Talk(archive);
            idx=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_archive_file");
            game.Choose(idx);
            Check(game.save.coins==save.coins,"Published atlas cannot be farmed by rereading");
        }

        public static void PreparedCampfireWardInterceptsOnlyPhysicalHostileFlight(Campaign data)
        {
            MapData old=data.maps[0];
            try
            {
                string[] rows=new string[26];
                for(int y=0;y<26;y++)
                {
                    char[] cells=new string('#',26).ToCharArray();
                    for(int x=2;x<24&&y>=2&&y<24;x++)cells[x]='.';
                    rows[y]=new string(cells);
                }
                // One solid dividing wall makes a hostile projectile and
                // the distant fire visually adjacent but physically isolated.
                char[] wall=rows[10].ToCharArray();
                wall[13]='#';rows[10]=new string(wall);
                var camp=new WorldObject{id="annex_camp_0",
                    kind="annexcamp",name="Signal camp",en="Signal camp",
                    x=10.5f,y=10.5f};
                data.maps[0]=new MapData{id=0,spawnX=10.5f,spawnY=10.5f,
                    rows=rows,objects=new[]{camp},decor=new Decoration[0]};
                var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
                g.save.x=camp.x;g.save.y=camp.y;
                g.save.inventory[0]=7;g.save.inventory[2]=3;
                g.save.energy=100;
                var forged=typeof(GameCore).GetMethod("ChooseCampWard",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(forged!=null,"Production signal fire is a real game choice handler");
                forged.Invoke(g,new object[]{"annex_ward_light"});
                Check(!g.CampWardActive&&g.save.inventory[0]==7&&
                    g.save.inventory[2]==3,
                    "A fabricated fire action without a living camp dialogue cannot spend supplies");
                g.Talk(camp);
                int light=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="annex_ward_light");
                Check(light>=0&&g.mode=="dialog",
                    "Existing in-world camp now offers a physical signal-fire choice");
                g.Choose(light);
                Check(g.CampWardActive&&g.CampWardCharges==3&&
                    g.CampWardSeconds==90f&&g.save.inventory[0]==5&&
                    g.save.inventory[2]==2&&g.save.version==2,
                    "Preparing a fire spends real supplies and creates three finite timed interceptions");
                Check(g.CampWardVisualSample(new Point(11.5f,10.5f))&&
                    !g.CampWardVisualSample(new Point(13.5f,10.5f))&&
                    !g.CampWardVisualSample(new Point(18.5f,10.5f)),
                    "World overlay shows the actual covered lane but never a solid wall or distant safe halo");
                g.Talk(camp);
                light=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="annex_ward_light");
                if(light>=0)g.Choose(light);else g.Close();
                Check(g.CampWardCharges==3&&g.save.inventory[0]==5,
                    "A lit fire cannot stack new charges or double-spend timber");
                // The projectile engine, not a direct helper call, must
                // actually intercept both hostile magic and enemy arrows.
                var arrow=new Projectile{position=new Point(12f,10.5f),
                    velocity=new Point(-6f,0),damage=10f,life=1f,
                    friendly=false,visual="enemyArrow",source="shot"};
                var orb=new Projectile{position=new Point(10.5f,12f),
                    velocity=new Point(0,-5f),damage=12f,life=1f,
                    friendly=false,visual="enemyOrb",source="shot",explosive=true};
                var ally=new Projectile{position=new Point(11.5f,9.5f),
                    velocity=new Point(3f,0),damage=8f,life=1f,
                    friendly=true,visual="arrow"};
                g.projectiles.Add(arrow);g.projectiles.Add(orb);
                g.projectiles.Add(ally);
                float beforeHp=g.save.health;
                g.Tick(.05f,new Point());
                Check(!g.projectiles.Contains(arrow)&&
                    !g.projectiles.Contains(orb)&&g.projectiles.Contains(ally)&&
                    g.save.health==beforeHp&&g.CampWardCharges==1,
                    "Actual swept hostile arrow and orb are disarmed before hitting the hero; allied arrows survive");
                var blocked=new Projectile{position=new Point(14.5f,10.5f),
                    velocity=new Point(-6f,0f),damage=9f,life=1f,
                    friendly=false,visual="enemyArrow",source="shot"};
                var inert=new Projectile{position=new Point(11.2f,11.5f),
                    velocity=new Point(-4f,0f),damage=0f,life=1f,
                    friendly=false,visual="enemyArrow",source="shot"};
                var stationary=new Projectile{position=new Point(11.8f,11.5f),
                    velocity=new Point(0f,0f),damage=10f,life=1f,
                    friendly=false,visual="enemyOrb",source="shot"};
                g.projectiles.Add(blocked);g.projectiles.Add(inert);
                g.projectiles.Add(stationary);
                g.Tick(.05f,new Point());
                Check(g.CampWardCharges==1&&g.projectiles.Contains(stationary),
                    "Terrain-separated, inert and stationary attack objects cannot consume a genuine airborne interception");
                var final=new Projectile{position=new Point(10.5f,12f),
                    velocity=new Point(0f,-4f),damage=11f,life=1f,
                    friendly=false,visual="enemyArrow",source="shot"};
                g.projectiles.Add(final);
                g.Tick(.05f,new Point());
                Check(!g.CampWardActive&&g.CampWardCharges==0&&
                    !g.projectiles.Contains(final),
                    "Third real contact exhausts the fire exactly once, without unlimited protection");
                g.projectiles.Clear();g.enemies.Clear();
                g.Talk(camp);
                light=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="annex_ward_light");
                Check(light>=0,"An exhausted fire can be deliberately rebuilt with new materials");
                g.Choose(light);
                Check(g.CampWardActive&&g.save.inventory[0]==3&&
                    g.save.inventory[2]==1,"New fire is a separate paid choice");
                // A later loaded v2 save must not inherit an unrecorded,
                // already-spent defensive ward from a previous character.
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(!loaded.CampWardActive&&loaded.CampWardCharges==0&&
                    loaded.save.version==2,"Old save slots remain compatible; ward is session-only");
                var field=typeof(GameCore).GetField("campWardSeconds",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(field!=null,"Real world-time expiration clock exists");
                field.SetValue(g,.02f);
                g.Tick(.05f,new Point());
                Check(!g.CampWardActive&&g.CampWardCharges==0,
                    "Genuine game simulation expires finite signal-fire protection");
            }
            finally { data.maps[0]=old; }
        }

        public static void EightPaidSignalFiresPublishSharedCivilianWatch(Campaign data)
        {
            var g=Fresh(data,0);g.save.room=9;
            WorldObject archive=Array.Find(data.maps[9].objects,
                o=>o.kind=="annexinnarchive");
            Check(archive!=null,"The real Hearth Inn holds a permanent physical archive");
            g.save.x=archive.x;g.save.y=archive.y;
            g.enemies.Clear();
            int coins=g.save.coins,shards=g.save.shards,xp=g.save.xp;
            var commit=typeof(GameCore).GetMethod("ChooseAnnexExploration",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(commit!=null,"Original annex choice dispatch exists");
            commit.Invoke(g,new object[]{"annex_signal_publish"});
            Check(!g.CampSignalNetworkPublished&&g.save.coins==coins,
                "Inventing a publish action without the speaker cannot grant rewards");
            // One missing actual field signal is enough to withhold the
            // public network regardless of the existing exploration atlas.
            for(int room=0;room<7;room++)
                g.save.flags.Add("annex_signal_fire_"+room);
            Check(g.CampSignalCount==7&&!g.CampSignalNetworkPublished,
                "Seven paid regional fires do not pretend to be eight");
            g.Talk(archive);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="annex_signal_publish")<0,
                "Archive dialogue does not offer the public network before the eighth fire");
            g.Close();
            g.save.flags.Add("annex_signal_fire_7");
            g.Talk(archive);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_signal_publish");
            Check(g.CampSignalCount==8&&option>=0&&
                g.conversation.textEn.Contains("eight"),
                "Eight field signals unlock the genuinely spoken publication option");
            g.Choose(option);
            Check(g.CampSignalNetworkPublished&&
                g.save.coins==coins+40&&g.save.shards==shards+2&&
                g.save.xp>=xp&&g.save.version==2,
                "In-person publication rewards one verified civilian watch milestone");
            int afterCoins=g.save.coins,afterShards=g.save.shards;
            g.Talk(archive);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_signal_publish")<0,
                "A published watch never offers a second collection");
            g.Close();
            commit.Invoke(g,new object[]{"annex_signal_publish"});
            Check(g.save.coins==afterCoins&&g.save.shards==afterShards,
                "Repeating the internal publish request cannot farm rewards");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.CampSignalNetworkPublished&&
                restored.CampSignalCount==8&&restored.save.version==2,
                "Civic watch record persists in the existing v2 string flags");
            // The reward is a real improvement to the next purchased
            // ward's time/charges, not merely a dialog/achievement label.
            WorldObject camp=Array.Find(data.maps[0].objects,
                o=>o.id=="annex_camp_0");
            Check(camp!=null,"First outdoor camp is an existing real scene object");
            restored.save.room=0;restored.save.x=camp.x;restored.save.y=camp.y;
            restored.save.inventory[0]=5;restored.save.inventory[2]=3;
            restored.enemies.Clear();
            restored.Talk(camp);
            option=Array.FindIndex(restored.conversation.choices,
                c=>c.action=="annex_ward_light");
            Check(option>=0,"The completed public watch retains the paid campfire service");
            restored.Choose(option);
            Check(restored.CampWardActive&&restored.CampWardCharges==4&&
                restored.CampWardSeconds==120f&&restored.save.inventory[0]==3&&
                restored.save.inventory[2]==2,
                "A completed signal network upgrades a genuine paid ward to 4 contacts/120s");
        }


        public static void SignalRelayRequiresRealTwoWayWorldChoicesAndSavesCompletion(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.flags.Add("annex_signal_network_published");
            g.save.flags.Add("annex_signal_fire_0");
            WorldObject camp=Array.Find(g.Map.objects,
                o=>o.kind=="annexcamp"&&o.id=="annex_camp_0");
            WorldObject marker=Array.Find(g.Map.objects,
                o=>o.kind=="ridgemarker"&&o.id=="ridge_route_0_0");
            Check(camp!=null&&marker!=null&&
                g.CanStand(new Point(camp.x,camp.y))&&
                g.CanStand(new Point(marker.x,marker.y)),
                "Production relay uses two original, standable physical world landmarks");
            Check((new Point(marker.x,marker.y)-
                new Point(camp.x,camp.y)).Length>24f,
                "Real relay target is far across the seventh-ring wilderness, not the nearby lookout");
            g.save.x=camp.x;g.save.y=camp.y;
            var action=typeof(GameCore).GetMethod("ChooseSignalRelay",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(action!=null,"The real relay choice handler exists");
            action.Invoke(g,new object[]{"annex_relay_begin"});
            Check(!g.SignalRelayActive,"A synthesized relay without physical camp dialogue cannot start");
            g.Talk(camp);
            int begin=Array.FindIndex(g.conversation.choices,c=>c.action=="annex_relay_begin");
            Check(begin>=0,"Published network offers a live two-way dispatch at the actual camp");
            g.Choose(begin);
            Check(g.SignalRelayStage==1&&g.SignalRelayIntegrity==3&&
                g.SignalRelaySeconds==300f,"A real camp starts a timed, damage-sensitive first leg");
            int startingCoins=g.save.coins,startingShards=g.save.shards;
            float startingTime=g.SignalRelaySeconds;
            g.Talk(camp);g.Tick(.05f,new Point());
            Check(Math.Abs(g.SignalRelaySeconds-startingTime)<.001f,
                "Opening the actual dialogue pauses both the world and the courier deadline");
            g.Close();
            g.Tick(.05f,new Point());
            Check(g.SignalRelaySeconds<startingTime,
                "Only genuine unpaused world simulation advances the courier deadline");
            g.Enter(1);
            Check(g.save.room==0&&g.SignalRelayStage==1,
                "Region transitions cannot skip a live two-way dispatch");
            g.ReturnToTown();
            Check(g.SignalRelayStage==1,
                "Quick return cannot skip courier route even from the home region");
            g.save.x=marker.x;g.save.y=marker.y;
            g.Talk(marker);
            Check(g.conversation!=null&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="annex_relay_deliver")>=0,
                "The original distant ridge marker offers the physically reached handoff");
            action.Invoke(g,new object[]{"annex_relay_report"});
            Check(g.SignalRelayStage==1&&g.SignalRelayDoneCount==0,
                "A remote lookout cannot counterfeit the camp's return receipt");
            int handoff=Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_deliver");
            g.Choose(handoff);
            Check(g.SignalRelayStage==2&&g.SignalRelayDoneCount==0,
                "One-way delivery alone cannot mint rewards or persistent progress");
            g.Talk(marker);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_deliver")<0,
                "The lookout will not generate duplicate delivery states");
            g.Close();
            g.PrepareSave();
            var premature=new GameCore(data,CloneSave(g.save));
            Check(!premature.SignalRelayActive&&premature.SignalRelayDoneCount==0,
                "Unfinished dispatch is transient and cannot leak through a v2 save slot");
            g.save.x=camp.x;g.save.y=camp.y;
            g.Talk(camp);
            int receipt=Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_report");
            Check(receipt>=0,"Only the original reached camp offers the reply handoff");
            g.Choose(receipt);
            Check(!g.SignalRelayActive&&g.SignalRelayDoneCount==1&&
                g.save.coins==startingCoins+20&&
                g.save.shards==startingShards+1&&g.save.version==2,
                "A full two-way dispatch awards exactly one genuine field reward");
            g.Talk(camp);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_report")<0,
                "A completed dispatch cannot replay its return reward");
            g.Close();
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.SignalRelayDoneCount==1&&
                loaded.Has("annex_relay_done_0")&&loaded.save.version==2,
                "Authenticated regional courier progress persists in original v2 flags");
        }

        public static void SignalRelayActualDamageDeadlineAndFinalEightRegionMoment(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.flags.Add("annex_signal_network_published");
            g.save.flags.Add("annex_signal_fire_0");
            WorldObject camp=Array.Find(g.Map.objects,
                o=>o.id=="annex_camp_0");
            g.save.x=camp.x;g.save.y=camp.y;
            g.Talk(camp);
            int begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_begin");
            g.Choose(begin);
            Check(g.SignalRelayActive,"Damage tests start a real packet from actual dialogue");
            var hit=typeof(GameCore).GetMethod("Hurt",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"A live player HP injury handler exists");
            for(int i=0;i<22;i++)g.Tick(.05f,new Point());
            hit.Invoke(g,new object[]{0f,"shot",null});
            Check(g.SignalRelayIntegrity==3,"Zero-damage warning never damages a physical packet");
            for(int contact=0;contact<3;contact++)
            {
                for(int i=0;i<22;i++)g.Tick(.05f,new Point());
                hit.Invoke(g,new object[]{5f,"shot",null});
                Check(g.SignalRelayIntegrity==Math.Max(0,2-contact),
                    "Only authentic post-armor hero injury reduces relay integrity");
            }
            Check(!g.SignalRelayActive&&!g.Has("annex_relay_done_0"),
                "Three real damaging contacts destroy the dispatch without any free progression");
            g.Talk(camp);
            begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_begin");
            Check(begin>=0,"A damaged dispatch can be restarted at the true source camp");
            g.Choose(begin);
            var seconds=typeof(GameCore).GetField("signalRelaySeconds",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(seconds!=null,"Five-minute dispatch uses a live, nonpersisted world-time clock");
            seconds.SetValue(g,.02f);
            // The preceding physical injury leaves 45ms of hit-stop.
            // Frozen time is not falsely counted as mission simulation;
            // advance enough authentic world frames to cover that pause.
            for(int tick=0;tick<4;tick++)g.Tick(.05f,new Point());
            Check(!g.SignalRelayActive&&!g.Has("annex_relay_done_0"),
                "Real world time can expire an unfinished relay without minting rewards");
            // A complete network is awarded only when the actual final
            // region performs BOTH interactions, not for an invented count.
            var last=Fresh(data);last.save.room=7;last.enemies.Clear();
            last.save.flags.Add("annex_signal_network_published");
            last.save.flags.Add("annex_signal_fire_7");
            for(int room=0;room<7;room++)
                last.save.flags.Add("annex_relay_done_"+room);
            WorldObject origin=Array.Find(last.Map.objects,
                o=>o.id=="annex_camp_7");
            WorldObject destination=Array.Find(last.Map.objects,
                o=>o.id=="ridge_route_7_1");
            Check(origin!=null&&destination!=null&&last.SignalRelayDoneCount==7,
                "Final two-way dispatch uses the eighth original camp and lookout");
            last.save.x=origin.x;last.save.y=origin.y;last.Talk(origin);
            begin=Array.FindIndex(last.conversation.choices,
                c=>c.action=="annex_relay_begin");
            Check(begin>=0,"Final region accepts a real paid-network mission");
            last.Choose(begin);
            last.save.x=destination.x;last.save.y=destination.y;
            last.Talk(destination);
            int handoff=Array.FindIndex(last.conversation.choices,
                c=>c.action=="annex_relay_deliver");
            Check(handoff>=0,"Eighth live lookout takes the outgoing signal");
            last.Choose(handoff);
            last.save.x=origin.x;last.save.y=origin.y;last.Talk(origin);
            int receipt=Array.FindIndex(last.conversation.choices,
                c=>c.action=="annex_relay_report");
            Check(receipt>=0,"Original eighth camp accepts the true return");
            int talents=last.save.skillPoints;
            last.Choose(receipt);
            Check(last.SignalRelayDoneCount==8,
                "Eight distinct region flags must be earned; saw "+
                last.SignalRelayDoneCount+" after the final real round trip");
            Check(last.Has("annex_relay_all"),
                "Eight actual round trips must mint the one-time public network milestone");
            // The return earns 40 XP as well as the network talent. If
            // this particular save crosses a level threshold, levelling
            // may grant ANOTHER skill point. Assert the earned talent, not
            // an impossible assumption about unrelated level-up rewards.
            Check(last.save.skillPoints>=talents+1,
                "Eight real dispatches must grant at least their network skill point");
            last.PrepareSave();
            var loaded=new GameCore(data,CloneSave(last.save));
            Check(loaded.Has("annex_relay_all")&&loaded.SignalRelayDoneCount==8,
                "Final chapter milestone survives SaveData v2 serialization");
        }


        public static void SignalRelayPublicChronicleEpilogueChangesRealOutdoorRecovery(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.room=9;
            WorldObject archive=Array.Find(g.Map.objects,
                o=>o.kind=="annexinnarchive");
            Check(archive!=null,"The public signal epilogue is staged at the existing Hearth Inn");
            g.save.x=archive.x;g.save.y=archive.y;
            for(int room=0;room<7;room++)
                g.save.flags.Add("annex_relay_done_"+room);
            g.save.flags.Add("annex_relay_all");
            int oldCoins=g.save.coins,oldShards=g.save.shards;
            var invoke=typeof(GameCore).GetMethod("ChooseAnnexExploration",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(invoke!=null,"Actual civic chronicle action handler exists");
            invoke.Invoke(g,new object[]{"annex_relay_archive"});
            Check(!g.Has("annex_relay_archived")&&g.save.coins==oldCoins,
                "Neither fabricated archive action nor seven-of-eight relays produce an ending");
            g.Talk(archive);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_archive")<0,
                "Seven field replies do not display a completed epilogue");
            g.Close();g.save.flags.Add("annex_relay_done_7");
            g.Talk(archive);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_relay_archive");
            Check(option>=0,"Eight verified return replies open a real final inn scene");
            g.Choose(option);
            Check(g.Has("annex_relay_archived")&&
                g.save.coins==oldCoins+80&&g.save.shards==oldShards+3&&
                g.conversation!=null&&g.conversation.textEn.Contains("Rain taps"),
                "In-person final act presents authored bilingual story and pays once");
            g.Close();invoke.Invoke(g,new object[]{"annex_relay_archive"});
            Check(g.save.coins==oldCoins+80&&g.save.shards==oldShards+3,
                "Replayed synthetic calls cannot claim a second epilogue reward");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.Has("annex_relay_archived")&&loaded.save.version==2,
                "The concluding civic story persists through the original v2 save schema");
            loaded.save.room=0;loaded.enemies.Clear();
            loaded.save.x=18f;loaded.save.y=15f;
            loaded.mode="world";loaded.save.energy=20f;
            loaded.save.flags.Remove("annex_relay_archived");
            loaded.Tick(.05f,new Point());
            float original=loaded.save.energy-20f;
            loaded.save.energy=20f;
            loaded.save.flags.Add("annex_relay_archived");
            loaded.Tick(.05f,new Point());
            float improved=loaded.save.energy-20f;
            Check(Math.Abs((improved-original)-.1f)<.011f,
                "Epilogue awards an actual +2 stamina-per-second outdoor gameplay benefit");
        }


        public static void AfterglowWatchHasRealTwoWaveCombatAndTacticalBranch(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.flags.Add("annex_relay_archived");
            g.save.flags.Add("annex_relay_done_0");
            WorldObject camp=Array.Find(g.Map.objects,
                o=>o.id=="annex_camp_0");
            Check(camp!=null&&g.CanStand(new Point(camp.x,camp.y)),
                "The live night watch originates at an existing reachable camp");
            g.save.x=camp.x;g.save.y=camp.y;
            var choose=typeof(GameCore).GetMethod("ChooseAfterglow",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"The new nightwatch action exists in the production core");
            choose.Invoke(g,new object[]{"afterglow_begin"});
            Check(!g.AfterglowActive&&g.enemies.Count==0,
                "Forged direct command with no physical camp dialogue cannot spawn combatants");
            g.Talk(camp);
            int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="afterglow_begin");
            Check(choice>=0,"Physical camp offers a real new encounter after the public relay chapter");
            g.Choose(choice);
            Check(g.AfterglowActive&&g.AfterglowPhase==1&&
                g.AfterglowRemaining==2&&g.enemies.Count==2&&g.AfterglowTime==75f,
                "Nightwatch spawns actual two-person first wave with a 75-second clock");
            g.Enter(1);
            Check(g.save.room==0&&g.AfterglowPhase==1,
                "A live nightwatch blocks region transfer instead of escaping the field fight");
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(hit!=null,"Authentic enemy damage transaction is available");
            int coins=g.save.coins,shards=g.save.shards;
            Enemy first=g.enemies[0];Enemy second=g.enemies[1];
            // A fabricated disappearance is never an earned kill.
            g.enemies.Remove(first);
            for(int i=0;i<3;i++)g.Tick(.05f,new Point());
            Check(!g.AfterglowActive&&!g.Has("afterglow_done_0")&&
                g.save.coins==coins&&g.save.shards==shards,
                "Missing enemy aborts without progress, and authentic other actors remain");
            g.enemies.Clear();g.save.x=camp.x;g.save.y=camp.y;
            g.Talk(camp);choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterglow_begin");
            Check(choice>=0,"A failed watch can be retried from the source");
            g.Choose(choice);
            Check(g.AfterglowPhase==1&&g.AfterglowRemaining==2,
                "A retry creates a fresh real combat roster");
            var wave=new System.Collections.Generic.List<Enemy>(g.enemies);
            foreach(Enemy foe in wave)
                hit.Invoke(g,new object[]{foe,10000f,false,null,null});
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(g.AfterglowPhase==2&&g.AfterglowRemaining==0&&
                !g.Has("afterglow_done_0"),
                "Only both lethal callbacks unlock a safe tactical interlude, not first-wave rewards");
            g.Talk(camp);
            int cut=Array.FindIndex(g.conversation.choices,c=>c.action=="afterglow_cut");
            int screen=Array.FindIndex(g.conversation.choices,c=>c.action=="afterglow_screen");
            Check(cut>=0&&screen>=0,"Live camp dialogue offers two different enemy-wave strategies");
            g.Choose(cut);
            Check(g.AfterglowPhase==3&&g.AfterglowRemaining==3&&
                g.AfterglowTime==95f&&
                g.enemies.Exists(e=>e.kind==2),
                "Risky interception physically spawns three live raiders including a heavy");
            wave=new System.Collections.Generic.List<Enemy>(g.enemies);
            foreach(Enemy foe in wave)
                hit.Invoke(g,new object[]{foe,10000f,false,null,null});
            for(int i=0;i<5;i++)g.Tick(.05f,new Point());
            Check(!g.AfterglowActive&&g.AfterglowWins==1&&
                g.Has("afterglow_done_0")&&g.save.coins>=coins+72&&
                g.save.shards>=shards+2,
                "Two truly defeated waves award one nonfarmable risky-watch victory");
            g.Talk(camp);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterglow_begin")<0,
                "After first real clear the regional attack cannot be farmed");
            g.PrepareSave();var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.Has("afterglow_done_0")&&
                !loaded.AfterglowActive&&loaded.save.version==2,
                "Clear persists in existing v2 flags while combat state does not leak across save slots");
        }

        public static void AfterglowWatchTimeoutAndSourceAbortDoNotGrantLoot(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.flags.Add("annex_relay_archived");
            g.save.flags.Add("annex_relay_done_0");
            WorldObject camp=Array.Find(g.Map.objects,o=>o.id=="annex_camp_0");
            g.save.x=camp.x;g.save.y=camp.y;g.Talk(camp);
            int begin=Array.FindIndex(g.conversation.choices,c=>c.action=="afterglow_begin");
            g.Choose(begin);
            int initial=g.save.coins;
            var countdown=typeof(GameCore).GetField("afterglowTime",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(countdown!=null,"A real world-ticked raid deadline exists");
            countdown.SetValue(g,.01f);
            for(int i=0;i<4;i++)g.Tick(.05f,new Point());
            Check(!g.AfterglowActive&&!g.Has("afterglow_done_0")&&
                g.save.coins==initial,
                "Remaining live raiders at deadline cannot collect mission rewards");
            g.enemies.Clear();g.save.x=camp.x;g.save.y=camp.y;
            g.Talk(camp);begin=Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterglow_begin");g.Choose(begin);
            // The attacker's real released arrow must retire with its
            // exact living owner; unrelated wilderness bodies and a
            // player's own shot must not be wiped by the mission reset.
            Enemy shooter=g.enemies[0];
            var enemyArrow=new Projectile{hostileShooter=shooter,
                position=shooter.Position,velocity=new Point(2,0),
                life=2f,damage=10f,visual="enemyArrow",
                afterglowOwned=true};
            var friendly=new Projectile{friendly=true,
                position=g.Position,velocity=new Point(2,0),
                life=2f,damage=4f,visual="arrow"};
            var wilderness=new Enemy(999999,0,camp.x+15f,
                camp.y+15f,30f,false);
            g.enemies.Add(wilderness);g.projectiles.Add(enemyArrow);
            g.projectiles.Add(friendly);
            g.Talk(camp);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterglow_abort");
            Check(abort>=0,"Original physical camp permits explicit withdrawal");
            g.Choose(abort);
            Check(!g.AfterglowActive&&g.enemies.Count==1&&
                g.enemies.Contains(wilderness)&&
                !g.projectiles.Contains(enemyArrow)&&
                g.projectiles.Contains(friendly)&&
                g.save.coins==initial&&!g.Has("afterglow_done_0"),
                "Withdrawal retires only mission-owned enemies and arrows, preserving real unrelated combat");
        }
        public static void EightAfterglowWatchesStrengthenRealCampProtection(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();
            g.save.flags.Add("annex_signal_network_published");
            for(int room=0;room<8;room++)
                g.save.flags.Add("afterglow_done_"+room);
            g.save.flags.Add("afterglow_all");
            WorldObject camp=Array.Find(g.Map.objects,
                o=>o.id=="annex_camp_0");
            Check(camp!=null&&g.AfterglowWins==8,
                "Eight regional authenticated watch flags are recognized");
            g.save.x=camp.x;g.save.y=camp.y;
            g.save.inventory[0]=5;g.save.inventory[2]=2;
            g.Talk(camp);
            int light=Array.FindIndex(g.conversation.choices,
                c=>c.action=="annex_ward_light");
            Check(light>=0,"Completed watch still retains a paid real-camp ward choice");
            g.Choose(light);
            Check(g.CampWardActive&&g.CampWardCharges==5&&
                g.CampWardSeconds==120f&&g.save.inventory[0]==3&&
                g.save.inventory[2]==1,
                "Victory across eight watches grants a real fifth defensive contact, without free materials");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.Has("afterglow_all")&&
                !restored.CampWardActive,
                "Upgraded ward capability persists as legacy flags, not a leaked active fight or new save field");
        }

        public static void EightAfterglowRegionsSpawnRealWalkableTacticalCombat(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"The original lethal contact pathway is available");
            for(int region=0;region<8;region++)
            {
                var g=Fresh(data);g.enemies.Clear();
                g.save.room=region;g.save.flags.Add("annex_relay_archived");
                g.save.flags.Add("annex_relay_done_"+region);
                WorldObject camp=Array.Find(g.Map.objects,
                    o=>o.id=="annex_camp_"+region);
                Check(camp!=null&&g.CanStand(new Point(camp.x,camp.y)),
                    "Each real regional watch camp is on walkable terrain: "+region);
                g.save.x=camp.x;g.save.y=camp.y;
                g.Talk(camp);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="afterglow_begin");
                Check(begin>=0,"Regional camp offers the unlocked nightwatch: "+region);
                g.Choose(begin);
                Check(g.AfterglowActive&&g.AfterglowPhase==1&&
                    g.AfterglowRemaining==2&&g.enemies.Count==2,
                    "Two genuine approach lanes exist in region "+region);
                var first=new System.Collections.Generic.List<Enemy>(g.enemies);
                foreach(Enemy enemy in first)
                    hit.Invoke(g,new object[]{enemy,10000f,false,null,null});
                // At most a 50ms hitstop; never skip frozen simulation.
                for(int i=0;i<5;i++)g.Tick(.05f,new Point());
                Check(g.AfterglowPhase==2,
                    "Real first-wave kills unlock tactics in region "+region);
                g.Talk(camp);
                int cut=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="afterglow_cut");
                Check(cut>=0,"A real second-wave tactical choice exists in region "+region);
                g.Choose(cut);
                Check(g.AfterglowPhase==3&&g.AfterglowRemaining==3&&
                    g.enemies.Count==3,
                    "Three separate heavy-wave approach lanes exist in region "+region);
                foreach(Enemy enemy in g.enemies)
                    Check(g.CanStand(enemy.Position)&&
                        (enemy.Position-g.AfterglowCampFoot).Length<=8f,
                        "Every raid participant occupies real nearby terrain in region "+region);
                g.Talk(camp);
                int abort=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="afterglow_abort");
                Check(abort>=0,"The original camp can withdraw its second wave");
                g.Choose(abort);
                Check(!g.AfterglowActive&&g.enemies.Count==0&&
                    !g.Has("afterglow_done_"+region),
                    "Every regional field retry clears exact attackers without a free win");
            }
        }


        public static void AfterglowCouncilOffersTwoRealPersistentCivilianPolicies(Campaign data)
        {
            for(int policy=0;policy<2;policy++)
            {
                var g=Fresh(data);g.save.room=9;g.mode="world";g.enemies.Clear();
                for(int room=0;room<8;room++)
                {
                    g.save.flags.Add("afterglow_done_"+room);
                    if(room<5)g.save.flags.Add("afterglow_cut_"+room);
                }
                g.save.flags.Add("afterglow_all");
                WorldObject archive=Array.Find(g.Map.objects,
                    o=>o.kind=="annexinnarchive");
                Check(archive!=null&&g.AfterglowWins==8&&g.AfterglowCutWins==5,
                    "Eight live-region completions preserve five authentic interception decisions");
                g.save.x=archive.x;g.save.y=archive.y;
                var handler=typeof(GameCore).GetMethod("ChooseAnnexExploration",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(handler!=null,"Production civic epilogue handler exists");
                int initialCoins=g.save.coins,initialShards=g.save.shards;
                handler.Invoke(g,new object[]{"afterglow_ending_shelter"});
                Check(!g.Has("afterglow_ending_shelter")&&
                    !g.Has("afterglow_ending_watch")&&g.save.coins==initialCoins,
                    "A forged epilogue action without the actual archive cannot claim either policy");
                g.Talk(archive);
                int council=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="afterglow_epilogue");
                Check(council>=0,"Eight real watch flags unlock a physical public-council option");
                g.Choose(council);
                Check(g.mode=="dialog"&&g.conversation.textEn.Contains("5 aggressive interceptions"),
                    "Multi-page public council reflects the player's actual tactical history");
                int option=Array.FindIndex(g.conversation.choices,
                    c=>c.action== (policy==0?"afterglow_ending_shelter":"afterglow_ending_watch"));
                Check(option>=0,"Both meaningful permanent council plans are genuinely selectable");
                g.Choose(option);
                Check(g.save.coins==initialCoins+55&&g.save.shards==initialShards+2&&
                    g.Has(policy==0?"afterglow_ending_shelter":"afterglow_ending_watch")&&
                    !g.Has(policy==0?"afterglow_ending_watch":"afterglow_ending_shelter"),
                    "Each permanent public choice pays exactly once and excludes the other ending");
                g.Close();
                g.Talk(archive);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="afterglow_epilogue")<0,
                    "The already-adopted public policy cannot be selected a second time");
                g.Close();handler.Invoke(g,new object[]{"afterglow_ending_watch"});
                Check(g.save.coins==initialCoins+55,
                    "Synthetic replay of a finished council cannot farm money");
                g.PrepareSave();
                var reload=new GameCore(data,CloneSave(g.save));
                Check(reload.save.version==2&&
                    reload.Has(policy==0?"afterglow_ending_shelter":"afterglow_ending_watch"),
                    "The chosen public chapter ending persists under the original v2 schema");
                g.Enter(0);g.enemies.Clear();
                WorldObject camp=Array.Find(g.Map.objects,
                    o=>o.id=="annex_camp_0");
                Check(camp!=null,"The original outdoor camp remains the reward service");
                g.save.x=camp.x;g.save.y=camp.y;
                g.save.inventory[0]=5;g.save.inventory[2]=3;
                if(policy==0)
                {
                    g.save.health=20f;g.save.energy=70f;
                    g.Talk(camp);
                    int rest=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="annex_rest");
                    Check(rest>=0,"The civilian shelter ending uses the true camp rest option");
                    g.Choose(rest);
                    Check(Math.Abs(g.save.health-80f)<.1f&&g.save.energy==100f&&
                        g.save.inventory[0]==4,
                        "Shelter council ending truly upgrades one paid rest from 40 to 60 HP");
                }
                else
                {
                    g.save.flags.Add("annex_signal_network_published");
                    g.Talk(camp);
                    int ward=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="annex_ward_light");
                    Check(ward>=0,"The night watch ending retains the actual paid signal fire");
                    g.Choose(ward);
                    Check(g.CampWardActive&&g.CampWardSeconds==150f&&
                        g.CampWardCharges==5&&g.save.inventory[0]==3&&
                        g.save.inventory[2]==2,
                        "Early-warning council ending truly extends the bought five-charge fire to 150 seconds");
                }
            }
        }

        public static void AfterglowCouncilRequiresEightLegitimateRegionalWins(Campaign data)
        {
            var g=Fresh(data);g.save.room=9;g.mode="world";g.enemies.Clear();
            for(int room=0;room<7;room++)g.save.flags.Add("afterglow_done_"+room);
            g.save.flags.Add("afterglow_all");
            WorldObject archive=Array.Find(g.Map.objects,
                o=>o.kind=="annexinnarchive");
            g.save.x=archive.x;g.save.y=archive.y;
            g.Talk(archive);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterglow_epilogue")<0,
                "A forged all-watch milestone cannot expose a final decision without eight kills-backed region flags");
            g.Close();g.save.flags.Add("afterglow_done_7");g.Talk(archive);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="afterglow_epilogue")>=0,
                "Eighth genuine regional watch makes the physical closing council available");
        }

        public static void UnlistedThreeActInvestigationUsesPhysicalCachesAndRidgeDeduction(Campaign data)
        {
            var g=Fresh(data);g.enemies.Clear();g.save.room=9;
            WorldObject archive=Array.Find(g.Map.objects,
                o=>o.kind=="annexinnarchive");
            Check(archive!=null&&g.UnlistedAct==-1,
                "The distinct Unlisted investigation begins unaccepted at the real inn archive");
            g.save.x=archive.x;g.save.y=archive.y;
            var action=typeof(GameCore).GetMethod("ChooseUnlisted",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(action!=null,"The investigation's physical choice handler exists");
            action.Invoke(g,new object[]{"unlisted_accept"});
            Check(g.UnlistedAct==-1,
                "Synthetic acceptance outside real archive dialogue cannot start quest");
            g.Talk(archive);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="unlisted_accept");
            Check(start>=0,"The archive offers the investigation as a real quest");
            g.Choose(start);
            Check(g.UnlistedAct==0&&!g.UnlistedDone,
                "Authentic source-bound acceptance begins chapter one");
            int[] regions={0,3,5},ridgeSides={0,1,0};
            for(int act=0;act<3;act++)
            {
                int room=regions[act];g.save.room=room;
                g.save.x=18.5f;g.save.y=15.5f;g.enemies.Clear();
                Check(g.UnlistedFieldObjectiveActive&&
                    g.UnlistedFieldObjective.Length>0,
                    "Each active region provides a live investigation objective");
                WorldObject a=Array.Find(g.Map.objects,
                    o=>o.id=="annex_cache_"+room+"_a");
                WorldObject b=Array.Find(g.Map.objects,
                    o=>o.id=="annex_cache_"+room+"_b");
                WorldObject ridge=Array.Find(g.Map.objects,
                    o=>o.id=="ridge_route_"+room+"_"+ridgeSides[act]);
                Check(a!=null&&b!=null&&ridge!=null&&
                    (g.UnlistedFieldTarget-new Point(a.x,a.y)).Length<.001f&&
                    g.CanStand(new Point(a.x,a.y))&&
                    g.CanStand(new Point(b.x,b.y))&&
                    g.CanStand(new Point(ridge.x,ridge.y)),
                    "Each act has two separate existing cache locations and a distant walkable ridge marker");
                // A direct, source-less field action can never forge evidence.
                action.Invoke(g,new object[]{"unlisted_clue_a"});
                Check(!g.Has("unlisted_a_"+act),
                    "An unspoken fabricated clue cannot count as a real discovery");
                g.save.x=a.x;g.save.y=a.y;g.Talk(a);
                int inspect=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="unlisted_clue_a");
                Check(inspect>=0,"First original cache offers its authentic evidence");
                g.Choose(inspect);
                Check(g.Has("unlisted_a_"+act)&&!g.Has("unlisted_b_"+act)&&
                    (g.UnlistedFieldTarget-new Point(b.x,b.y)).Length<.001f,
                    "A real first clue points to the distinct second cache");
                g.save.x=ridge.x;g.save.y=ridge.y;g.Talk(ridge);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="unlisted_reason")<0,
                    "Ridge deduction is locked until two separate cache documents have been read");
                g.Close();g.save.x=b.x;g.save.y=b.y;g.Talk(b);
                inspect=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="unlisted_clue_b");
                Check(inspect>=0,"The second original cache offers its independent clue");
                g.Choose(inspect);
                Check(g.Has("unlisted_b_"+act)&&
                    (g.UnlistedFieldTarget-new Point(ridge.x,ridge.y)).Length<.001f,
                    "Both genuine clues redirect the world objective to the distant ridge");
                g.save.x=ridge.x;g.save.y=ridge.y;g.Talk(ridge);
                int wrong=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="unlisted_wrong");
                Check(wrong>=0,"Each episode offers a falsifiable wrong hypothesis");
                float energy=g.save.energy;
                g.Choose(wrong);
                Check(g.UnlistedAct==act&&!g.Has("unlisted_solved_"+act)&&
                    Math.Abs(g.save.energy-Math.Max(0f,energy-8f))<.01f,
                    "Wrong deductions cost only bounded effort and do not advance story");
                g.Talk(ridge);
                int reason=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="unlisted_reason");
                Check(reason>=0,"Both verified clues unlock the episode's correct inference");
                g.Choose(reason);
                Check(g.Has("unlisted_solved_"+act)&&g.UnlistedAct==act+1,
                    "Only the real matching ridge answer advances the three-act investigation");
                // A stale previous-act clue must not grant the next chapter's evidence.
                action.Invoke(g,new object[]{"unlisted_clue_b"});
                Check(!g.Has("unlisted_b_"+(act+1)),
                    "Old chapter source cannot impersonate a later episode");
            }
            Check(g.UnlistedAct==3&&!g.UnlistedDone,
                "Three solved field episodes unlock, but do not auto-select, a moral ending");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.save.version==2&&restored.UnlistedAct==3&&
                !restored.UnlistedDone,
                "All three evidence deductions survive v2 save-slot reload before the final choice");
        }

        public static void UnlistedDistinctEndingsAreSourceBoundNonFarmableAndAlterGameplay(Campaign data)
        {
            for(int choice=0;choice<2;choice++)
            {
                var g=Fresh(data);g.save.room=9;g.enemies.Clear();
                g.save.flags.Add("unlisted_started");
                for(int a=0;a<3;a++)g.save.flags.Add("unlisted_solved_"+a);
                WorldObject archive=Array.Find(g.Map.objects,
                    o=>o.kind=="annexinnarchive");
                g.save.x=archive.x;g.save.y=archive.y;
                float healthMax=g.save.maxHealth;
                int coins=g.save.coins,shards=g.save.shards;
                string action=choice==0?"unlisted_public":"unlisted_protect";
                var choiceHandler=typeof(GameCore).GetMethod("ChooseUnlisted",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                choiceHandler.Invoke(g,new object[]{action});
                Check(!g.UnlistedDone&&g.save.coins==coins,
                    "Synthetic ending with no physical archive cannot claim its reward");
                g.Talk(archive);
                int option=Array.FindIndex(g.conversation.choices,c=>c.action==action);
                Check(option>=0,"Each ethically distinct ending is a genuine selectable archive choice");
                g.Choose(option);
                Check(g.UnlistedDone&&g.save.coins==coins+55&&
                    g.save.shards==shards+2&&
                    g.Has(choice==0?"unlisted_public":"unlisted_protected"),
                    "In-person ending preserves the chosen ethical outcome and one-time resources");
                g.Close();g.Talk(archive);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="unlisted_public")<0&&
                    Array.FindIndex(g.conversation.choices,
                    c=>c.action=="unlisted_protect")<0,
                    "No completed ending offers a second payout or reversal");
                g.Close();
                choiceHandler.Invoke(g,new object[]{action});
                Check(g.save.coins==coins+55,
                    "Forged repeat calls cannot double-pay the ending");
                g.PrepareSave();
                var reload=new GameCore(data,CloneSave(g.save));
                Check(reload.UnlistedDone&&reload.save.version==2,
                    "Decision persists without changing existing SaveData v2 schema");
                if(choice==1)
                    Check(Math.Abs(g.save.maxHealth-healthMax-25f)<.1f&&
                        Math.Abs(reload.save.maxHealth-healthMax-25f)<.1f,
                        "Protected registry genuinely grants persistent +25 maximum health");
                else
                {
                    Check(Math.Abs(g.save.maxHealth-healthMax)<.1f,
                        "Published registry does not secretly give the privacy ending's extra HP");
                    g.save.room=0;g.enemies.Clear();g.mode="world";
                    g.save.flags.Add("ridge_seen_0_0");
                    g.save.flags.Add("ridge_seen_0_1");
                    WorldObject marker=Array.Find(g.Map.objects,
                        o=>o.id=="ridge_route_0_0");
                    Check(marker!=null&&g.RidgeLinked(0),
                        "The real regional transport has physically linked ridge endpoints");
                    g.save.x=marker.x;g.save.y=marker.y;g.save.energy=50f;
                    g.Talk(marker);
                    int route=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="ridge_travel");
                    Check(route>=0&&g.conversation.choices[route].en.Contains("6 energy"),
                        "Published documents offer a visible lower-price real ridge crossing");
                    g.Choose(route);
                    Check(Math.Abs(g.save.energy-44f)<.01f,
                        "Publishing names actually reduces the paid ridge energy transaction from 12 to 6");
                }
            }
        }

        public static void NewAnnexSitesAreRealWalkableAndPersistent(Campaign data)
        {
            for(int region=0;region<8;region++)
            {
                MapData m=data.maps[region];
                GameCore g=new GameCore(data);g.save.room=region;
                g.save.flags.Add("gate_"+region);
                Check(!g.InProtectedArena(new Point(65,9)), "Boss arena must remain at its original x-coordinate");
                int count=0;foreach(WorldObject o in m.objects)
                    if(o.kind=="annexcache"||o.kind=="annexcamp"||o.kind=="annexvista")
                    {count++;Check(g.CanStand(new Point(o.x,o.y)), "New site must occupy real walkable tile: "+o.id);}
                Check(count==4,"Two caches, one camp, one vista per outdoor map");
            }
            GameCore game=new GameCore(data);
            WorldObject cache=Array.Find(data.maps[0].objects,o=>o.id=="annex_cache_0_a");
            int coins=game.save.coins,cacheMaterial=game.save.inventory[0];
            Point outsideCache=StandableOutsideTalkReach(game,cache);
            game.save.x=outsideCache.x;game.save.y=outsideCache.y;
            game.Talk(cache);game.Close();
            Check(!game.Has("annex_cache_"+cache.id)&&game.save.coins==coins&&
                game.save.inventory[0]==cacheMaterial,
                "An out-of-reach direct cache call cannot claim supplies or resources");
            game.save.x=cache.x;game.save.y=cache.y;
            game.Talk(cache);
            Check(game.save.coins==coins+7&&game.Has("annex_cache_"+cache.id),
                "Looting a physically reached cache grants its one-time supplies");
            game.Close();game.Talk(cache);game.Close();
            Check(game.save.coins==coins+7,"Cache cannot duplicate real loot");
            WorldObject vista=Array.Find(data.maps[0].objects,o=>o.id=="annex_vista_0");
            game.save.x=vista.x;game.save.y=vista.y;game.Talk(vista);game.Close();
            Check(game.Has("annex_vista_"+vista.id)&&game.Explored(0,65,40),"Lookout records its nearby 6x6 fog cells");
            WorldObject camp=Array.Find(data.maps[0].objects,o=>o.id=="annex_camp_0");
            game.save.x=camp.x;game.save.y=camp.y;game.Talk(camp);
            int choice=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_rest");
            Check(choice>=0,"Real camp offers rest interaction");
            int wood=game.save.inventory[0];
            game.save.health=30;game.save.energy=8;
            game.Choose(choice);
            Check(game.save.inventory[0]==wood-1&&game.save.health==70&&game.save.energy==100,
                "Camp consumes actual timber, restores HP and energy");
            // A stale action with no genuine camp dialogue cannot burn wood
            // even when the hero is physically standing beside this camp.
            game.save.health=20;game.save.energy=15;
            wood=game.save.inventory[0];
            var annexHandler=typeof(GameCore).GetMethod("ChooseAnnexExploration",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(annexHandler!=null,"Live annex choice handler is callable for a safety regression");
            annexHandler.Invoke(game,new object[]{"annex_rest"});
            Check(game.save.health==20&&game.save.energy==15&&
                game.save.inventory[0]==wood,
                "A forged camp choice without its exact spoken source cannot spend timber");
            // A legitimate dialogue still cannot waste scarce wood when no
            // benefit exists. That condition must be rechecked at commit.
            game.save.health=game.save.maxHealth;game.save.energy=100;
            game.Talk(camp);
            choice=Array.FindIndex(game.conversation.choices,c=>c.action=="annex_rest");
            Check(choice>=0,"Safe physical camp still offers its own rest action");
            game.Choose(choice);
            Check(game.save.inventory[0]==wood&&
                game.save.health==game.save.maxHealth&&game.save.energy==100,
                "Full health and energy do not consume timber for a wasted rest");
            // Genuine fieldwork state used by the other rest services must
            // prevent this camp from offering a shortcut into recovery.
            var drillSetter=typeof(GameCore).GetProperty("SwordLessonActive")
                .GetSetMethod(true);
            Check(drillSetter!=null,"GameCore owns the living drill state");
            drillSetter.Invoke(game,new object[]{true});
            game.save.health=game.save.maxHealth-10;game.save.energy=20;
            game.Talk(camp);
            Check(game.conversation!=null&&
                Array.FindIndex(game.conversation.choices,c=>c.action=="annex_rest")<0&&
                game.save.inventory[0]==wood,
                "A live field drill cannot reveal a camp rest that bypasses mission rules");
            game.Close();drillSetter.Invoke(game,new object[]{false});
            GameCore reload=new GameCore(data,game.save);
            Check(reload.save.version==2&&reload.Has("annex_camp_"+camp.id)&&reload.Has("annex_cache_"+cache.id)&&reload.Has("annex_vista_"+vista.id),
                "All exploration progress remains in v2 save");
        }

        public static void EightCircuitRoutesUseActualWalkingOrderAndPersist(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData m=data.maps[room];
                Check(m.rows.Length==80&&m.rows[0].Length==120,
                    "Sixth-ring terrain preserves earlier exploration sites");
                GameCore g=new GameCore(data);
                g.save.room=room;g.save.flags.Add("seal_"+room);
                int width=m.rows[0].Length,height=m.rows.Length;
                var visited=new System.Collections.Generic.HashSet<int>();
                var queue=new System.Collections.Generic.Queue<int>();
                int start=(int)m.spawnY*width+(int)m.spawnX;
                queue.Enqueue(start);visited.Add(start);
                int[,] dir={{1,0},{-1,0},{0,1},{0,-1}};
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%width,y=at/width;
                    for(int k=0;k<4;k++)
                    {
                        int xx=x+dir[k,0],yy=y+dir[k,1],key=yy*width+xx;
                        if(xx<0||yy<0||xx>=width||yy>=height||visited.Contains(key)||
                            !g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                        visited.Add(key);queue.Enqueue(key);
                    }
                }
                WorldObject[] sites=new WorldObject[3];
                for(int k=1;k<=3;k++)
                {
                    sites[k-1]=Array.Find(m.objects,o=>o.id=="trailcircuit_"+room+"_"+k);
                    Check(sites[k-1]!=null &&
                        visited.Contains((int)sites[k-1].y*width+(int)sites[k-1].x),
                        "Real collision and traversal reach every landmark in room "+room);
                }
                int startCoins=g.save.coins,startXp=g.save.xp;
                Point outsideCircuit=StandableOutsideTalkReach(g,sites[0]);
                g.save.x=outsideCircuit.x;g.save.y=outsideCircuit.y;
                g.Talk(sites[0]);g.Close();
                Check(g.TrailCircuitProgress(room)==0&&g.save.coins==startCoins&&
                    g.save.xp==startXp,
                    "A physically standable point outside E-key reach cannot submit a third-ring waypoint");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(g.TrailCircuitProgress(room)==0,"Third landmark cannot bypass first two");
                for(int k=0;k<3;k++)
                {
                    g.save.x=sites[k].x;g.save.y=sites[k].y;
                    g.Talk(sites[k]);
                    Check(g.conversation!=null&&g.conversation.text.Length>55&&
                        g.conversation.textEn.Length>55,
                        "Every visited physical waypoint provides real bilingual lore");
                    g.Close();
                    Check(g.TrailCircuitProgress(room)==k+1,"Each visited stop records real ordered progress");
                }
                Check(g.TrailCircuitComplete(room)&&g.save.coins==startCoins+24,
                    "Third distinct visit grants lasting local route completion");
                g.Talk(sites[2]);g.Close();
                Check(g.save.coins==startCoins+24,"Rereading completed circuit cannot farm rewards");
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&restored.TrailCircuitComplete(room),
                    "Existing SaveData v2 reload preserves circuit milestones");
            }
            GameCore all=new GameCore(data);
            for(int r=0;r<8;r++)for(int k=1;k<=3;k++)
                all.save.flags.Add("trail_circuit_"+r+"_"+k);
            Check(all.TrailCircuitCount==8&&all.GetQuest("trail_circuits").complete,
                "Independent eight-area map quest reflects real stage flags");
        }

        public static void ThirdRingResourcePocketsUseRealRenewableHarvesting(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore region=new GameCore(data);
                region.save.room=room;
                int count=0;
                foreach(WorldObject o in data.maps[room].objects)
                    if(o.id.StartsWith("trail_forager_"+room+"_"))
                    {
                        count++;
                        Check(region.CanStand(new Point(o.x,o.y)),
                            "Every new gatherable site remains on actual playable terrain");
                    }
                Check(count==3,"Each of eight third-ring maps gets three actual field resources");
            }
            GameCore g=new GameCore(data);
            WorldObject node=Array.Find(data.maps[0].objects,o=>o.id=="trail_forager_0_0");
            int herbs=g.save.inventory[2];
            g.save.x=node.x;g.save.y=node.y;g.Talk(node);
            Check(g.Has("node_"+node.id)&&g.save.inventory[2]==herbs+3,
                "New outdoor node yields actual resource through existing gathering code");
            g.Talk(node);
            Check(g.save.inventory[2]==herbs+3,"Second gather cannot duplicate resource");
            g.save.flags.RemoveAll(flag=>flag.StartsWith("node_"));
            g.Talk(node);
            Check(g.save.inventory[2]==herbs+6,
                "Existing rest refresh makes the new site renewable without new save format");
        }

        public static void CachedHouseCollidersRemainCorrectAcrossMapSwitches(Campaign data)
        {
            GameCore g=new GameCore(data);
            int tested=0;
            for(int room=0;room<8;room++)
            {
                g.save.room=room;
                foreach(Decoration d in data.maps[room].decor)
                    if(d.kind=="house")
                    {
                        tested++;
                        Check(g.Blocked(d.x,d.y-.7f),
                            "Solid house footprint remains blocked after world-decoration caching");
                    }
                foreach(WorldObject o in data.maps[room].objects)
                    if(o.kind=="trailcircuit")
                        Check(g.CanStand(new Point(o.x,o.y)),
                            "Third-ring site remains walkable when switching collision maps");
            }
            Check(tested>=4,"World collision regression exercises existing authored houses");
            g.save.room=0;
            Check(g.Blocked(7,5.3f),"Returning to town restores its original solid house colliders");
        }

        public static void FourthRingRescueSignalsRequireRealSurveyAndParts(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData m=data.maps[room];
                Check(m.rows.Length==80&&m.rows[0].Length==120,
                    "Expanded sixth-ring geometry retains existing rescue locations");
                GameCore g=new GameCore(data);
                g.save.room=room;
                g.save.inventory[4]=5;
                WorldObject[] sites=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    int stage=i;
                    sites[i]=Array.Find(m.objects,o=>o.id=="outer_"+room+"_"+stage);
                    Check(sites[i]!=null&&g.CanStand(new Point(sites[i].x,sites[i].y)),
                        "Every fourth-ring rescue object stands on actual accessible terrain");
                }
                int width=m.rows[0].Length,height=m.rows.Length;
                var queue=new System.Collections.Generic.Queue<int>();
                var seen=new System.Collections.Generic.HashSet<int>();
                int spawn=(int)m.spawnY*width+(int)m.spawnX;
                queue.Enqueue(spawn);seen.Add(spawn);
                int[,] dirs={{1,0},{-1,0},{0,1},{0,-1}};
                while(queue.Count>0)
                {
                    int at=queue.Dequeue(),x=at%width,y=at/width;
                    for(int direction=0;direction<4;direction++)
                    {
                        int xx=x+dirs[direction,0],yy=y+dirs[direction,1];
                        if(xx<0||yy<0||xx>=width||yy>=height)continue;
                        int key=yy*width+xx;
                        if(seen.Contains(key)||!g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                        seen.Add(key);queue.Enqueue(key);
                    }
                }
                foreach(WorldObject site in sites)
                    Check(seen.Contains((int)site.y*width+(int)site.x),
                        "New signal route must be reachable from original spawn using real collision");
                int coins=g.save.coins,parts=g.save.inventory[4],xp=g.save.xp;
                Point outsideLog=StandableOutsideTalkReach(g,sites[0]);
                g.save.x=outsideLog.x;g.save.y=outsideLog.y;g.Talk(sites[0]);g.Close();
                Check(!g.OuterLogFound(room)&&g.save.xp==xp,
                    "A distant rescue log cannot award first-discovery XP or advance evidence");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(!g.OuterCacheRecovered(room)&&g.save.coins==coins,
                    "Relief cache cannot be taken before the signal is repaired");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);g.Close();
                Check(!g.OuterRelayRestored(room)&&g.save.inventory[4]==parts,
                    "Beacon cannot be repaired without the original field log");
                g.save.x=sites[0].x;g.save.y=sites[0].y;
                g.Talk(sites[0]);g.Close();
                Check(g.OuterLogFound(room),"Reading the southern evidence advances the real quest");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_repair");
                Check(choice>=0,"Physical repair offers a real material-consuming choice");
                Point outsideRelay=StandableOutsideTalkReach(g,sites[1]);
                g.save.x=outsideRelay.x;g.save.y=outsideRelay.y;g.Choose(choice);g.Close();
                Check(!g.OuterRelayRestored(room)&&g.save.inventory[4]==parts,
                    "A stale relay-repair choice cannot spend a part after leaving physical reach");
                g.save.x=sites[1].x;g.save.y=sites[1].y;g.Talk(sites[1]);
                choice=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_repair");
                Check(choice>=0,"Returning to the physical beacon restores the repair choice");
                g.Choose(choice);
                Check(g.OuterRelayRestored(room)&&g.save.inventory[4]==parts-1,
                    "One actual spare part is consumed when the physical beacon is repaired");
                g.Talk(sites[1]);g.Close();
                Check(g.save.inventory[4]==parts-1,"Replaying a repaired signal cannot double-charge");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(g.OuterCacheRecovered(room)&&g.save.coins==coins+18,
                    "Final physical relief chest pays the correct one-time reward");
                g.Talk(sites[2]);g.Close();
                Check(g.save.coins==coins+18,"Relief chest cannot pay twice");
                GameCore restored=new GameCore(data,g.save);
                Check(restored.save.version==2&&restored.OuterLogFound(room)&&
                    restored.OuterRelayRestored(room)&&restored.OuterCacheRecovered(room),
                    "All three rescue stages survive a legacy SaveData v2 reload");
            }
            GameCore all=new GameCore(data);
            for(int room=0;room<8;room++)all.save.flags.Add("outer_cache_"+room);
            Check(all.OuterRescueCount==8&&all.GetQuest("outer_rescue").complete,
                "The tracked field mission finishes only after eight genuine chests");
        }

        public static void SmoothTerrainAndPropsRemainContinuousAcrossChunkBorders(Campaign data)
        {
            Check(WorldStreaming.Smooth01(-1)==0&&WorldStreaming.Smooth01(2)==1,
                "Streaming fades clamp safely outside their range");
            Check(Math.Abs(WorldStreaming.Smooth01(.5f)-.5f)<.0001f,
                "Chunk readiness uses symmetric smoothstep");
            for(int i=0;i<100;i++)
            {
                float t=i/100f, v=WorldStreaming.Blend(.15f,.3f,.6f,.9f,t,.45f);
                float u=WorldStreaming.Blend(.15f,.3f,.6f,.9f,t+.0001f,.45f);
                Check(v>=0&&v<=1&&Math.Abs(u-v)<.001f,
                    "Chunk blending stays continuous without square block seams");
            }
            float old=-1;
            for(int px=-130;px<=130;px+=5)
            {
                float opacity=WorldStreaming.EdgeOpacity(px,360,1280,720);
                Check(opacity>=0&&opacity<=1&&opacity>=old,
                    "Scenery gradually fades in on approach without a viewport pop-in");
                old=opacity;
            }
            Check(WorldStreaming.EdgeOpacity(-150,360,1280,720)==0,
                "Distant scenery is completely transparent beyond its prewarm apron");
            Check(WorldStreaming.EdgeOpacity(640,360,1280,720)==1,
                "Center-screen scenery stays fully visible");
        }


        public static void FinalFrameWatchVictoryPrecedesTimeoutButRequiresRealKills(Campaign data)
        {
            var post=Array.Find(data.maps[0].objects,o=>o.kind=="outerwatch");
            Check(post!=null,"Original first-region relief post exists on the live map");
            var timer=typeof(GameCore).GetField("outerPatrolTime",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(timer!=null&&hit!=null,
                "Exercise real patrol deadline and lethal combat transaction");
            foreach(bool veteran in new[]{false,true})
            {
                var g=new GameCore(data);
                g.save.flags.Add("intro");g.save.flags.Add("outer_cache_0");
                if(veteran)g.save.flags.Add("outer_patrol_done_0");
                g.Start();g.save.reducedMotion=true;
                g.enemies.Clear();g.projectiles.Clear();
                g.save.x=post.x;g.save.y=post.y;
                g.Talk(post);
                int option=Array.FindIndex(g.conversation.choices,
                    c=>c.action==(veteran?"outer_watch_veteran":"outer_watch_start"));
                Check(option>=0,"Original staffed post offers the real "+(veteran?"veteran":"standard")+" defense");
                g.Choose(option);
                Enemy[] raiders=g.enemies.ToArray();
                Check(g.OuterPatrolActive&&raiders.Length==(veteran?3:2)&&
                    g.OuterPatrolTime>0,
                    "Actual "+(veteran?"three":"two")+" attackers spawn in the timed live siege");
                int before=g.save.coins;
                timer.SetValue(g,.012f);
                foreach(Enemy raider in raiders)
                    hit.Invoke(g,new object[]{raider,10000f,true,null,null});
                Check(g.OuterPatrolActive&&g.enemies.Count==0,
                    "Real lethal Hit callbacks retire every owned attacker before the final deadline tick");
                g.Tick(.05f,new Point());
                Check(!g.OuterPatrolActive&&g.OuterPatrolDone(0)&&
                    g.save.coins==before+(veteran?26:14)&&
                    (!veteran||(g.OuterPatrolVeteranDone(0)&&
                                 g.OuterPatrolFlawlessDone(0))),
                    "Defeating all raiders during the final simulated frame settles the original one-time rewards and flawless medal");
                g.Tick(.05f,new Point());
                Check(g.save.coins==before+(veteran?26:14),
                    "Victory cleanup cannot pay the same field encounter again");
            }
            // The deadline still matters. A single living enemy must lose;
            // a missing but never defeated enemy cannot counterfeit success.
            foreach(bool missing in new[]{false,true})
            {
                var g=new GameCore(data);g.save.flags.Add("intro");
                g.save.flags.Add("outer_cache_0");g.Start();
                g.enemies.Clear();g.projectiles.Clear();
                g.save.x=post.x;g.save.y=post.y;g.Talk(post);
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_start");
                g.Choose(start);
                Check(g.OuterPatrolActive&&g.enemies.Count==2,
                    "Original two-raider relief siege launched for negative deadline proof");
                int coins=g.save.coins;
                if(missing)g.enemies.Clear();
                timer.SetValue(g,.012f);
                g.Tick(.05f,new Point());
                Check(!g.OuterPatrolActive&&!g.OuterPatrolDone(0)&&
                    g.save.coins==coins,
                    "A "+(missing?"despawned":"living")+" raider cannot receive final-frame victory or rewards");
            }
        }

        public static void OutdoorRescueWatchStartsRealCombatAndOnlyRewardsRealWins(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData map=data.maps[room];
                WorldObject o=Array.Find(map.objects,x=>x.id=="outer_watch_"+room);
                GameCore g=new GameCore(data);g.save.room=room;
                Check(o!=null&&g.CanStand(new Point(o.x,o.y)),
                    "Every optional watchpost must be on a walkable physical tile");
                g.save.x=o.x;g.save.y=o.y;
                g.enemies.Clear();
                g.Talk(o);
                Check(g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start")<0,
                    "Outpost cannot be started without first retrieving the relief supplies");
                g.Close();
                g.save.flags.Add("outer_cache_"+room);
                int coins=g.save.coins;
                g.Talk(o);
                int start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                Check(start>=0,"Completed exploration unlocks the voluntary real combat choice");
                if(room==0)
                {
                    Point outsidePost=StandableOutsidePhysicalRadius(g,o,2.2f);
                    g.save.x=outsidePost.x;g.save.y=outsidePost.y;
                    g.Choose(start);g.Close();
                    Check(!g.OuterPatrolActive&&g.enemies.Count==0&&g.save.coins==coins,
                        "A stale outpost dialogue cannot launch a remote defense");
                    g.save.x=o.x;g.save.y=o.y;g.Talk(o);
                    start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                    Check(start>=0,"Returning to the physical post restores the defense choice");
                }
                g.Choose(start);
                Check(g.OuterPatrolActive&&g.enemies.Count==2&&
                    !g.OuterPatrolDone(room),"Defense uses actual two spawned enemies, not a text-only battle");
                int id0=g.enemies[0].id,id1=g.enemies[1].id;
                Check(id0!=id1&&g.enemies[0].hp>0&&g.enemies[1].hp>0,
                    "Distinct attackers spawn with real combat health");
                g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&g.save.coins==coins,
                    "Unbeaten enemies cannot award any defense completion");
                // Regression: forcibly removing actors from the list is NOT
                // damage, and must never counterfeit a successful clear.
                g.enemies.Clear();g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&!g.OuterPatrolActive&&g.save.coins==coins,
                    "Enemy despawn must abort rather than award a counterfeit win");
                g.Talk(o);
                start=Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start");
                Check(start>=0,"Aborted encounter can be retried safely");
                g.Choose(start);
                Check(g.enemies.Count==2,"Retry spawns two live combat actors");
                Enemy attackerA=g.enemies[0],attackerB=g.enemies[1];
                var hit=typeof(GameCore).GetMethod("Hit",
                    System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Check(hit!=null,"Exercise real combat Hit transaction, not a simulated flag");
                hit.Invoke(g,new object[]{attackerA,10000f,true,null,null});
                // Hit-stop is part of actual combat and temporarily defers
                // quest ticks. Run normal real frames, not a magic giant dt.
                for(int frame=0;frame<10;frame++)g.Tick(.02f,new Point());
                Check(!g.OuterPatrolDone(room)&&g.OuterPatrolActive,
                    "Defeating one real attacker is not enough to clear the defense");
                hit.Invoke(g,new object[]{attackerB,10000f,true,null,null});
                for(int frame=0;frame<10;frame++)g.Tick(.02f,new Point());
                Check(g.OuterPatrolDone(room)&&g.save.coins==coins+14,
                    "Both true lethal Hit transactions award the single defense reward");
                // Recreate the persisted v2 state as a separate slot object.
                // The loader sanitizes its OWN save in place. Passing the
                // active g.save aliases the running hero and a locked-region
                // recovery can move that hero away from this watchpost.
                var persisted = new SaveData {
                    version=g.save.version, room=g.save.room, stage=g.save.stage,
                    x=g.save.x, y=g.save.y, coins=g.save.coins,
                    flags=new List<string>(g.save.flags)
                };
                GameCore reload=new GameCore(data,persisted);
                Check(reload.save.version==2&&reload.OuterPatrolDone(room),
                    "Defended watchposts persist with original v2 flags");
                g.Talk(o);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="outer_watch_start")<0,
                    "Completed post remains readable, never starts a duplicating fight");
                g.Close();
                Check(g.save.coins==coins+14,"Repeating a defended post cannot farm rewards");
            }
        }

        public static void CompanionPartySwapCannotInheritAnotherAllysDefenseOrder(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("companion_1");
            g.save.flags.Add("companion_2");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Real guard trial can start at the physical cache");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"The live siege exposes the original guard order");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Original Ivo guard foot remains active before party switch");
            Point postFoot=g.OuterPatrolCompanionGuardPosition;
            g.SelectCompanion(1);
            Check(g.OuterPatrolCompanionGuarding&&
                (g.OuterPatrolCompanionGuardPosition-postFoot).Length<.001f,
                "Repeating same ally selection cannot cancel a valid order");
            Point safe=new Point();bool found=false;
            for(int dx=-11;dx<=11&&!found;dx++)
            for(int dy=-11;dy<=11&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-new Point(post.x,post.y)).Length>11f)continue;
                g.save.x=p.x;g.save.y=p.y;
                if(g.SafeTravel){safe=p;found=true;}
            }
            Check(found,"Real cache map includes a legal safe party-swap foot");
            g.save.x=safe.x;g.save.y=safe.y;
            g.SelectCompanion(2);
            Check(g.save.companion==2&&g.OuterPatrolActive&&
                !g.OuterPatrolCompanionGuarding&&
                !g.OuterPatrolMedicRallying&&g.save.version==2,
                "New Sen party member cannot inherit Ivo's planted stock order");
            var stored=typeof(GameCore).GetField("outerPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(stored!=null&&
                ((Point)stored.GetValue(g)).Length<.001f,
                "Retired tactical waypoint is cleared, not orphaned in the world");
            // The new actor joined the party at the player's actual
            // distant safe foot. Returning the hero to the post does NOT
            // teleport this other actor. Give the real world simulation time
            // to walk the companion toward the watch before assigning a
            // NEW physically reachable defensive position.
            g.save.x=post.x;g.save.y=post.y;
            g.invulnerable=100f;
            foreach(Enemy foe in g.enemies)foe.stun=12f;
            for(int frame=0;frame<520&&g.OuterPatrolActive&&
                (g.companionPosition-new Point(post.x,post.y)).Length>3.5f;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.CanStand(g.companionPosition)&&
                (g.companionPosition-new Point(post.x,post.y)).Length<4f,
                "New companion physically rejoins the genuine post after a party swap");
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"New companion may receive a real fresh watch order");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding&&
                g.CanStand(g.OuterPatrolCompanionGuardPosition),
                "New party member can actually be stationed again from post");
        }

        public static void RealStationedCompanionHasTwoFinitePhysicalStockGuards(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"The physical cache offers its real veteran siege");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"The real fighting party can plant a cache guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding&&
                g.OuterPatrolGuardBlocksLeft==2&&g.OuterPatrolFlawlessEligible,
                "New siege has two finite, unused guard contacts");
            Point foot=g.OuterPatrolCompanionGuardPosition;
            Check(g.CanStand(foot),
                "Original guard target is a physically valid tile");
            // The other companion movement tests exercise SweepActor. This
            // focused contact test places its actor at that already-verified
            // legal endpoint to drive 3 real enemy contact releases.
            g.companionPosition=foot;
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"Ordinary melee attacker really exists");
            bool positioned=false;
            for(float dx=-2f;dx<=2f&&!positioned;dx+=.5f)
            for(float dy=-2f;dy<=2f&&!positioned;dy+=.5f)
            {
                Point candidate=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(candidate)||
                    (candidate-new Point(post.x,post.y)).Length>2.2f||
                    (candidate-foot).Length>2f||
                    (candidate-foot).Length<.8f)
                    continue;
                melee.x=candidate.x;melee.y=candidate.y;
                positioned=true;
            }
            Check(positioned,"Actual cache has a physical melee/guard contact foot");
            Point observer=new Point();bool found=false;
            for(int dx=-10;dx<=10&&!found;dx++)
            for(int dy=-10;dy<=10&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-new Point(post.x,post.y)).Length>11f||
                    (p-melee.Position).Length<4.5f)continue;
                observer=p;found=true;
            }
            Check(found,"Defender can physically observe from out of melee");
            g.save.x=observer.x;g.save.y=observer.y;
            var release=typeof(GameCore).GetMethod("DamageOuterPatrolStock",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var able=typeof(GameCore).GetMethod("OuterPatrolCompanionCanIntercept",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(release!=null&&able!=null,"Actual production stock hit path exists");
            melee.targetingOuterPatrolStock=true;
            if(!(bool)able.Invoke(g,new object[]{melee}))
            {
                // An authored post may have a narrow gate: search a legal
                // raider contact foot only through truly connected paths.
                var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                Check(clear!=null,"Use original physical melee LOS");
                positioned=false;
                for(float dx=-2f;dx<=2f&&!positioned;dx+=.25f)
                for(float dy=-2f;dy<=2f&&!positioned;dy+=.25f)
                {
                    Point p=new Point(post.x+dx,post.y+dy);
                    if(!g.CanStand(p)||
                        (p-new Point(post.x,post.y)).Length>2.2f||
                        (p-foot).Length>2.2f||
                        !(bool)clear.Invoke(g,new object[]{p,foot})||
                        !(bool)clear.Invoke(g,new object[]{
                            p,new Point(post.x,post.y)}))continue;
                    melee.x=p.x;melee.y=p.y;positioned=true;
                }
                Check(positioned,"Valid full melee strike exists on authored cache terrain");
            }
            Check((bool)able.Invoke(g,new object[]{melee})&&
                g.OuterPatrolGuardReady,
                "Living posted ally can physically reach a real melee stock strike");
            var exposed=typeof(GameCore).GetMethod(
                "CompanionCacheContactImminent",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(exposed!=null,"Test the real auto-fire emergency priority path");
            melee.state=1;
            Check(!(bool)exposed.Invoke(g,new object[]{melee}),
                "Already blocked stock threat cannot steal urgent ally target");
            float stock=g.OuterPatrolIntegrity;
            release.Invoke(g,new object[]{melee});
            Check(g.OuterPatrolGuardBlocksLeft==1&&g.OuterPatrolIntegrity==stock&&
                !g.OuterPatrolFlawlessEligible,
                "First real intercepted attack consumes finite ally guard and disqualifies no-aid medal");
            release.Invoke(g,new object[]{melee});
            Check(g.OuterPatrolGuardBlocksLeft==0&&g.OuterPatrolIntegrity==stock,
                "Second intercepted stock strike consumes final finite contact");
            Check(!g.OuterPatrolGuardReady&&
                (bool)exposed.Invoke(g,new object[]{melee}),
                "Depleted guard exposes real incoming cache hit to auto-fire priority");
            release.Invoke(g,new object[]{melee});
            Check(g.OuterPatrolGuardBlocksLeft==0&&g.OuterPatrolIntegrity<stock,
                "Third genuine melee release reaches stock without free bodyguard");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.OuterPatrolGuardBlocksLeft==0&&
                g.save.version==2,"Withdrawal clears unsaved limited guard state");
        }

        public static void GuardMedicPhysicallyRalliesToCriticalHeroAndReturnsToStock(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.companion=0;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Medical guard uses a real watchpost defense");
            g.Choose(start);
            foreach(Enemy foe in g.enemies)foe.stun=20f;
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Mira can be physically assigned to watch stock");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Medical support guard is a valid active tactical order");
            Point foot=g.OuterPatrolCompanionGuardPosition;
            Point remote=new Point();bool found=false;
            for(int dx=-9;dx<=9&&!found;dx++)
            for(int dy=-9;dy<=9&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-foot).Length<7f||
                    (p-new Point(post.x,post.y)).Length>10f)
                    continue;
                remote=p;found=true;
            }
            Check(found,"Real regional defense ring has a separate walkable rescue point");
            g.save.x=remote.x;g.save.y=remote.y;
            g.save.health=g.save.maxHealth*.25f;
            var helper=typeof(GameCore).GetMethod("CompanionOutpostGuardTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(helper!=null,"Actual escort goal is selected by live game logic");
            Point medicTarget=(Point)helper.Invoke(g,null);
            Check((medicTarget-foot).Length>1f&&
                g.OuterPatrolCompanionGuarding&&g.OuterPatrolMedicRallying,
                "Critically injured remote hero draws Mira from her watch foot");
            float before=(g.companionPosition-medicTarget).Length;
            for(int i=0;i<90&&g.OuterPatrolActive;i++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-medicTarget).Length<before-.1f,
                "Medic walks toward physical rescue point, never heals globally");
            g.save.health=g.save.maxHealth;
            Point returnFoot=(Point)helper.Invoke(g,null);
            Check((returnFoot-foot).Length<.01f&&
                g.OuterPatrolCompanionGuarding&&!g.OuterPatrolMedicRallying,
                "After real recovery medic resumes original protected post");
            float gap=(g.companionPosition-foot).Length;
            for(int tick=0;tick<60&&g.OuterPatrolActive;tick++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-foot).Length<=gap+.02f,
                "Recovery truly resumes walking to the ORIGINAL guard foot");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolCompanionGuarding&&
                !g.OuterPatrolActive,
                "Withdrawal removes emergency medic and guard states");
        }

        public static void GuardChoosesRealClearMuzzleCoverageAtEveryRegionalCache(Campaign data)
        {
            var select=typeof(GameCore).GetMethod(
                "TryOuterPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var score=typeof(GameCore).GetMethod(
                "OuterPatrolGuardFiringLanes",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(select!=null&&score!=null,
                "Test real guard placement and actual physically swept muzzle");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null,"Map has a real rescue watch in room "+room);
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();
                g.save.x=post.x;g.save.y=post.y;
                g.save.companion=1;
                g.companionPosition=new Point(post.x,post.y);
                g.save.flags.Add("outer_cache_"+room);
                g.Talk(post);
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_start");
                Check(start>=0,"Regional raid can start with a true supply cache");
                g.Choose(start);
                Check(g.OuterPatrolActive&&g.enemies.Count==2,
                    "Physical guard coverage test uses real enemy positions");
                Point[] original={
                    new Point(0f,2.5f),new Point(-2.5f,0f),
                    new Point(2.5f,0f),new Point(0f,-2.5f),
                    new Point(-1.7f,1.7f),new Point(1.7f,1.7f),
                    new Point(-1.7f,-1.7f),new Point(1.7f,-1.7f)
                };
                var reachable=typeof(GameCore).GetMethod(
                    "ActorHasLocalRoute",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                int maximum=-1;
                foreach(Point offset in original)
                {
                    Point candidate=new Point(post.x+offset.x,post.y+offset.y);
                    if(!g.CanStand(candidate)||
                        (candidate-g.Position).Length<1.15f||
                        g.enemies.Exists(e=>e.hp>0&&
                            (e.Position-candidate).Length<1.2f)||
                        !(bool)reachable.Invoke(g,new object[]{
                            g.companionPosition,candidate,10}))continue;
                    int value=(int)score.Invoke(g,new object[]{candidate});
                    if(value>maximum)maximum=value;
                }
                Check(maximum>=0,
                    "There is a legal reachable guard foot in region "+room);
                object[] args={null};
                bool chosen=(bool)select.Invoke(g,args);
                Check(chosen,"Guard placement finds a real reachable foot");
                Point landing=(Point)args[0];
                Check(g.CanStand(landing)&&
                    (int)score.Invoke(g,new object[]{landing})==maximum,
                    "Guard aims from maximum genuine projectile coverage, region "+room);
            }
        }

        public static void GuardRangerMovesFromWalkableBlindFootToPhysicalFiringLane(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Actual watch encounter is interactable");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Real ranged guard order was accepted");
            var score=typeof(GameCore).GetMethod("OuterPatrolGuardFiringLanes",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var route=typeof(GameCore).GetMethod("ActorHasLocalRoute",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var target=typeof(GameCore).GetMethod("CompanionOutpostGuardTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var foot=typeof(GameCore).GetField("outerPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var clock=typeof(GameCore).GetField("outerPatrolGuardRouteCheck",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(score!=null&&route!=null&&target!=null&&foot!=null&&clock!=null,
                "Exercise the original shooting and local path APIs");
            int best=(int)score.Invoke(g,new object[]{g.OuterPatrolCompanionGuardPosition});
            Check(best>0,"Current real post guard can fire on initial raiders");
            Point blind=new Point();bool found=false;
            for(int dx=-10;dx<=10&&!found;dx++)
            for(int dy=-10;dy<=10&&!found;dy++)
            {
                Point probe=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(probe)||
                   (probe-g.OuterPatrolCompanionGuardPosition).Length<3f||
                   (int)score.Invoke(g,new object[]{probe})>0||
                   !(bool)route.Invoke(g,new object[]{
                       probe,g.OuterPatrolCompanionGuardPosition,10}))continue;
                blind=probe;found=true;
            }
            Check(found,"Real map contains a walkable but blind defender tile");
            g.companionPosition=blind;
            foot.SetValue(g,blind);
            clock.SetValue(g,0f);
            Point before=g.companionPosition;
            Point better=(Point)target.Invoke(g,null);
            Check(g.OuterPatrolCompanionGuarding&&
                (better-blind).Length>.5f&&
                (int)score.Invoke(g,new object[]{better})>0&&
                (g.companionPosition-before).Length<.001f,
                "Blind but walkable guard relocates target to real firing lane without warping");
            Check(g.save.version==2&&g.projectiles.Count==0,
                "Firing-lane discovery never spawns fake arrows or changes old saves");
        }

        public static void GuardReplansBlockedWorldRouteWithoutWarpingOrOrphanOrders(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Real ranged guard is initially stationed");
            var field=typeof(GameCore).GetField("outerPatrolCompanionGuardFoot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var clock=typeof(GameCore).GetField("outerPatrolGuardRouteCheck",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var target=typeof(GameCore).GetMethod("CompanionOutpostGuardTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var route=typeof(GameCore).GetMethod("ActorHasLocalRoute",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(field!=null&&clock!=null&&target!=null&&route!=null,
                "Test live connectedness, not a fake route flag");
            Point stranded=new Point();bool found=false;
            for(int radius=12;radius<=20&&!found;radius++)
            for(int dx=-radius;dx<=radius&&!found;dx++)
            for(int dy=-radius;dy<=radius&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-g.companionPosition).Length<11f||
                    (bool)route.Invoke(g,new object[]{
                        g.companionPosition,p,10}))continue;
                stranded=p;found=true;
            }
            Check(found,"Real world has walkable but locally disconnected foot");
            Point actorBefore=g.companionPosition;
            field.SetValue(g,stranded);clock.SetValue(g,0f);
            Point updated=(Point)target.Invoke(g,null);
            Check(g.OuterPatrolCompanionGuarding&&
                (updated-stranded).Length>1f&&g.CanStand(updated)&&
                (bool)route.Invoke(g,new object[]{
                    g.companionPosition,updated,10})&&
                (g.companionPosition-actorBefore).Length<.001f,
                "Closed route replans to reachable guard position without teleporting actor");
            Check((g.OuterPatrolCompanionGuardPosition-updated).Length<.01f,
                "World guard marker reads recovered actual reachable destination");
            // A tide-covered or sealed ORIGINAL waypoint must not
            // prematurely abandon a useful guard when another REAL
            // connected shooting foot still exists in the same region.
            field.SetValue(g,new Point(-20f,-20f));
            Point relocated=(Point)target.Invoke(g,null);
            Check(g.OuterPatrolCompanionGuarding&&
                g.CanStand(relocated)&&
                (relocated-new Point(-20f,-20f)).Length>1f&&
                (g.companionPosition-actorBefore).Length<.001f,
                "Blocked stock foot chooses genuine alternate without warping");
            // If even the companion itself is buried in invalid terrain,
            // there is no legal local source from which to certify a route.
            // Retire the command rather than keeping a phantom guard.
            field.SetValue(g,new Point(-20f,-20f));
            g.companionPosition=new Point(-20f,-20f);
            target.Invoke(g,null);
            Check(!g.OuterPatrolCompanionGuarding,
                "No physically reachable guard position safely resumes following");
        }

        public static void RealCompanionGuardsSuppliesWithoutWarpingOrPersisting(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            Check(post!=null,"Regional relief post exists on physical map");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            Check(g.CanStand(g.companionPosition),
                "Guard begins at a genuine unblocked companion foot");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Watchpost has a genuine active defense");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"The physical watchpost offers a live companion guard order");
            Point before=g.companionPosition;
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding&&
                g.CanStand(g.OuterPatrolCompanionGuardPosition)&&
                (g.companionPosition-before).Length<.001f,
                "Guard command picks legal foot but never teleports ally");
            Point destination=g.OuterPatrolCompanionGuardPosition;
            // The player can intercept the second raider without dragging
            // the ranged guard away from the vulnerable stock.
            g.save.x=post.x+5f;g.save.y=post.y;
            float distBefore=(g.companionPosition-destination).Length;
            for(int tick=0;tick<90&&g.OuterPatrolActive;tick++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolCompanionGuarding&&
                (g.companionPosition-destination).Length<distBefore+.05f&&
                (g.companionPosition-new Point(post.x,post.y)).Length<5f,
                "Guard follows a physical local stock waypoint, not the distant hero");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Follow option exists while guard is active");
            g.Choose(guard);
            Check(!g.OuterPatrolCompanionGuarding,
                "Player can restore original ordinary companion follow behavior");
            // Independent guard order can be given again in the same siege,
            // but it must disappear on withdrawal and save round-trip.
            g.Talk(post);
            guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Guard can be reassigned by an actual valid local order");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&!g.OuterPatrolCompanionGuarding,
                "Leaving a siege retires ephemeral guard orders");
            SaveData slot=new SaveData {
                version=2,room=0,x=post.x,y=post.y,
                flags=new List<string>(g.save.flags)
            };
            GameCore reload=new GameCore(data,slot);
            Check(!reload.OuterPatrolCompanionGuarding&&
                reload.save.version==2,
                "v2 slots never persist a transient tactical guard command");
        }

        // Older tests asserted "stock windup" on spawned approach actors
        // several meters outside actual melee reach, while the hero was
        // standing in the way. These helpers instead select an AUTHENTIC
        // standable, unobstructed contact plus a real bow muzzle lane.
        private static Point PlaceShootableCacheContact(GameCore g,
            WorldObject post,Enemy attacker,Enemy avoid)
        {
            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",flags);
            var shot=typeof(GameCore).GetMethod("WeaponShot",flags);
            Check(clear!=null&&shot!=null,
                "Use existing combat path and projectile muzzle geometry");
            Point cache=new Point(post.x,post.y);
            for(float dx=-2f;dx<=2f;dx+=.25f)
            for(float dy=-2f;dy<=2f;dy+=.25f)
            {
                Point p=cache+new Point(dx,dy);
                Point aim=p-g.companionPosition;
                if(!g.CanStand(p)||(p-cache).Length>2.2f||
                    aim.Length<1.4f||aim.Length>=6.5f||
                    avoid!=null&&(p-avoid.Position).Length<1.15f||
                    !(bool)clear.Invoke(g,new object[]{p,cache})||
                    !(bool)clear.Invoke(g,new object[]{
                        g.companionPosition,p}))
                    continue;
                Projectile probe=(Projectile)shot.Invoke(g,new object[]{
                    g.companionPosition,1,aim.Normalized,9f,0f,(Point?)p});
                if(probe.blockedAtSpawn)continue;
                attacker.x=p.x;attacker.y=p.y;
                return p;
            }
            throw new Exception(
                "No genuine walkable stock contact with a clear original ally bow muzzle");
        }
        private static void MoveToRealUncoveredStockObserver(GameCore g,
            WorldObject post,Enemy one,Enemy two)
        {
            Point cache=new Point(post.x,post.y);
            for(int dx=-10;dx<=10;dx++)
            for(int dy=-10;dy<=10;dy++)
            {
                Point p=cache+new Point(dx,dy);
                if(!g.CanStand(p)||(p-cache).Length>11f||
                    (p-one.Position).Length<3.5f||
                    two!=null&&(p-two.Position).Length<3.5f)
                    continue;
                g.save.x=p.x;g.save.y=p.y;
                return;
            }
            throw new Exception("No real physical safe observer for outpost warning");
        }

        public static void CompanionProtectsOnlyUncoveredHeroAndCivilianMeleeThreats(Campaign data)
        {
            MapData old=data.maps[0];
            try
            {
                string[] rows=new string[25];
                for(int y=0;y<25;y++)
                {
                    char[] cells=new string('.',25).ToCharArray();
                    for(int x=0;x<25;x++)
                        if(x<2||y<2||x>=23||y>=23)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[0]=new MapData{id=0,spawnX=15.5f,spawnY=10.5f,
                    rows=rows,objects=new WorldObject[0],
                    decor=new Decoration[0]};
                var g=Fresh(data);g.enemies.Clear();g.projectiles.Clear();
                g.save.x=15.5f;g.save.y=10.5f;
                g.save.companion=1;g.companionPosition=new Point(11f,12.5f);
                var hunter=new Enemy(87431,0,14.5f,10.5f,180f)
                    {state=1,timer=.25f,windupDuration=.65f};
                hunter.target=g.Position;hunter.Face(g.Position-hunter.Position);
                var defender=new Enemy(87432,0,15f,10.5f,200f)
                    {stun=100f,timer=100f};
                var idle=new Enemy(87433,0,12.5f,14.5f,100f)
                    {stun=100f,timer=100f};
                g.enemies.Add(hunter);g.enemies.Add(defender);
                g.enemies.Add(idle);
                var hero=typeof(GameCore).GetMethod("CompanionCommittedHeroStrike",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                var select=typeof(GameCore).GetMethod("NearestCompanionThreat",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(hero!=null&&select!=null&&
                    !(bool)hero.Invoke(g,new object[]{hunter}),
                    "The timed hero lunge is not imminent through a living frontline ally");
                Check((Enemy)select.Invoke(g,null)!=hunter,
                    "Companion auto-target must not spend an emergency shot on a body-blocked melee windup");
                defender.y=13.5f;
                Check((bool)hero.Invoke(g,new object[]{hunter}),
                    "A genuinely opened line immediately restores the committed hero-threat classification");
                Check((Enemy)select.Invoke(g,null)==hunter,
                    "Ivo now correctly prioritizes the shootable uncovered real lunge over idle enemies");
                Check(g.save.version==2,
                    "Combat priority uses transient actor identity and changes no old save fields");
            }
            finally{data.maps[0]=old;}

            // Real escort alerts share the authoritative contact rule, not
            // just a flag that happens to say the foe is attacking somebody.
            var escort=Fresh(data);
            escort.save.stage=3;escort.Enter(1);
            escort.enemies.Clear();escort.projectiles.Clear();
            escort.reliefMissionRoom=1;escort.reliefMissionWave=3;
            escort.reliefMissionTime=75f;escort.reliefFollowerSafety=100f;
            escort.reliefFollowerPosition=new Point(15f,14f);
            escort.save.x=20f;escort.save.y=17f;
            var raider=new Enemy(87441,0,14f,14f,140f)
                {state=1,timer=.2f,windupDuration=.65f,
                 targetingReliefCivilian=true};
            raider.target=escort.reliefFollowerPosition;
            raider.Face(raider.target-raider.Position);
            var cover=new Enemy(87442,0,14.5f,14f,160f)
                {stun=100f,timer=100f};
            escort.enemies.Add(raider);escort.enemies.Add(cover);
            var rescue=typeof(GameCore).GetMethod("CompanionCommittedRescueStrike",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(rescue!=null&&escort.CanStand(raider.Position)&&
                escort.CanStand(cover.Position)&&
                !(bool)rescue.Invoke(escort,new object[]{raider})&&
                escort.IncomingRescueCivilianThreat==null,
                "A living raider cover retracts both the companion's rescue priority and the civilian hit-warning");
            cover.y=17f;
            Check((bool)rescue.Invoke(escort,new object[]{raider})&&
                escort.IncomingRescueCivilianThreat==raider,
                "When the line opens, the same genuine timed escort hit reappears as a player-visible threat");
            Check(escort.save.version==2,
                "Escorted civilian threat classification is save-format neutral");
        }

        public static void CompanionInterceptsLiveHeavyWindupBeforeIdleCacheRaider(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.companion=1;
            g.companionPosition=new Point(post.x,post.y);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"An actual veteran three-raider cache siege is available");
            g.Choose(start);
            Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                "Threat priority exercises three authentic enemy bodies");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "Live cache contains both melee raider and heavy slammer");
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(select!=null,"Exercise actual bow ally target-selection code");
            Enemy first=(Enemy)select.Invoke(g,null);
            Check(first==melee||first==heavy,
                "Real sabotage-capable actor outranks wandering ranged raider");
            // Prove both actual muzzle lanes are legal before asserting any
            // priority; a blocked shot must never beat an available one.
            var shot=typeof(GameCore).GetMethod("WeaponShot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(shot!=null,"Real ally projectile sweep is available");
            Point vectorM=(melee.Position-g.companionPosition).Normalized;
            Point vectorH=(heavy.Position-g.companionPosition).Normalized;
            Projectile probeM=(Projectile)shot.Invoke(g,new object[]{
                g.companionPosition,1,vectorM,9f,0f,(Point?)melee.Position});
            Projectile probeH=(Projectile)shot.Invoke(g,new object[]{
                g.companionPosition,1,vectorH,9f,0f,(Point?)heavy.Position});
            Check(!probeM.blockedAtSpawn&&!probeH.blockedAtSpawn,
                "Both actual enemy targets have a valid muzzle lane");
            PlaceShootableCacheContact(g,post,heavy,melee);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            heavy.state=1;heavy.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==heavy,
                "Heavy actually threatens uncovered stock from its physical contact range");
            Check((Enemy)select.Invoke(g,null)==heavy,
                "Imminent heavy hit wins ally's target over idle melee attacker");
            heavy.targetingOuterPatrolStock=false;
            PlaceShootableCacheContact(g,post,melee,heavy);
            MoveToRealUncoveredStockObserver(g,post,melee,heavy);
            melee.state=1;melee.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "The ordinary melee attacker also has a real uncovered strike lane");
            Check((Enemy)select.Invoke(g,null)==melee,
                "Threat priority follows the currently committed raider");
            g.enemies.Remove(heavy);
            Check((Enemy)select.Invoke(g,null)==melee,
                "Another registered attacker vanishing never creates a phantom target");
        }

        public static void OutpostWarningsTrackActualCommittedRaiderNotPhantomThreat(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null&&
                g.OuterPatrolIncomingStockThreat==null,
                "Idle enemies never create counterfeit supply warnings");
            // During a real defense the hero can intercept in melee range.
            // Observe genuine stock threats from a legal but distant post
            // foot, then move inside the raider's actual attack lane to
            // verify the same warning retracts BEFORE genuine release.
            Point observer=new Point();bool foundObserver=false;
            for(int dx=-10;dx<=10&&!foundObserver;dx++)
            for(int dy=-10;dy<=10&&!foundObserver;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if(!g.CanStand(p)||
                    (p-new Point(post.x,post.y)).Length>11f||
                    (p-melee.Position).Length<3.5f||
                    (p-heavy.Position).Length<3.5f)continue;
                observer=p;foundObserver=true;
            }
            Check(foundObserver,
                "Actual watch map provides a legal defender observation foot");
            g.save.x=observer.x;g.save.y=observer.y;
            melee.state=1;melee.timer=.8f;
            melee.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==melee&&
                Math.Abs(g.OuterPatrolIncomingStockSeconds-.8f)<.01f&&
                !g.OuterPatrolIncomingStockHeavy,
                "Active real melee stock windup produces grounded timing");
            g.save.x=melee.x;g.save.y=melee.y;
            Check(g.OuterPatrolIncomingStockThreat==null&&
                g.OuterPatrolIncomingStockSeconds<0f,
                "Hero physically entering an already aimed swing cancels false stock-hit alarm");
            g.save.x=observer.x;g.save.y=observer.y;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "Warning resumes if the physical defender abandons interception");
            // The initial heavy spawn is a valid approach, not yet a
            // legal contact foot. Move this controlled integration fixture
            // to an actually walkable, unobstructed melee range BEFORE
            // asserting that a stock-hit telegraph is truthful.
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(clear!=null,"Verify the physical stock strike lane");
            bool contactFoot=false;
            for(float dx=-2f;dx<=2f&&!contactFoot;dx+=.5f)
            for(float dy=-2f;dy<=2f&&!contactFoot;dy+=.5f)
            {
                Point candidate=new Point(post.x+dx,post.y+dy);
                if((candidate-new Point(post.x,post.y)).Length>2.2f||
                    (candidate-melee.Position).Length<1.1f||
                    !g.CanStand(candidate)||
                    !(bool)clear.Invoke(g,new object[]{
                        candidate,new Point(post.x,post.y)}))
                    continue;
                heavy.x=candidate.x;heavy.y=candidate.y;
                contactFoot=true;
            }
            Check(contactFoot,"Actual map supplies an honest second stock contact foot");
            heavy.state=1;heavy.timer=.28f;
            heavy.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==heavy&&
                g.OuterPatrolIncomingStockHeavy,
                "Warning follows the earliest genuine heavy contact");
            heavy.stun=.8f;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "Stunned heavy cannot falsely warn of an imminent contact");
            heavy.stun=0f;heavy.targetingOuterPatrolStock=false;
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "Heavy aimed elsewhere cannot counterfeit cache impact");
            melee.hp=0f;
            Check(g.OuterPatrolIncomingStockThreat==null&&
                g.OuterPatrolIncomingStockSeconds<0f,
                "Dead or canceled raiders instantly retract warning");
            // The observer is deliberately away from the cache during
            // warning verification. Return to its real interactable foot
            // before opening the withdrawal dialogue; Talk cannot create
            // an out-of-range conversation by design.
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&
                g.OuterPatrolIncomingStockThreat==null,
                "Leaving real siege removes all pending stock cues");
        }

        public static void CompanionCancelledAutoContactNeverFiresAtDeadOrLostTarget(Campaign data)
        {
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.companion=1;
            g.save.x=data.maps[0].spawnX;
            g.save.y=data.maps[0].spawnY;
            g.companionPosition=g.Position;
            Point foot=new Point();bool found=false;
            var shot=typeof(GameCore).GetMethod("WeaponShot",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var target=typeof(GameCore).GetField("companionAutoTarget",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var next=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var tick=typeof(GameCore).GetMethod("TickCompanion",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(shot!=null&&target!=null&&next!=null&&tick!=null,
                "Test existing physical companion production release");
            for(int dx=-5;dx<=5&&!found;dx++)
            for(int dy=-5;dy<=5&&!found;dy++)
            {
                Point p=g.companionPosition+new Point(dx,dy);
                Point delta=p-g.companionPosition;
                if(delta.Length<2f||delta.Length>5f||!g.CanStand(p))
                    continue;
                Projectile probe=(Projectile)shot.Invoke(g,new object[]{
                    g.companionPosition,1,delta.Normalized,9f,0f,(Point?)p});
                if(probe.blockedAtSpawn)continue;
                foot=p;found=true;
            }
            Check(found,"Original town terrain has a genuine clear ally bow lane");
            Enemy foe=new Enemy(920601,0,foot.x,foot.y,30f);
            g.enemies.Add(foe);
            Point aim=(foe.Position-g.companionPosition).Normalized;
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=aim,target=foe.Position
            };
            target.SetValue(g,foe);next.SetValue(g,2.8f);
            // During the already-paid bow draw the hero killed the mark.
            foe.hp=0f;
            int effects=g.effects.Count;
            tick.Invoke(g,new object[]{.17f});
            Check(g.companionAction.released&&g.projectiles.Count==0&&
                g.effects.Count==effects&&
                (float)next.GetValue(g)<=.22f,
                "A dead auto-target cannot emit a phantom arrow or muzzle flash");

            // Test an occluded/stale release separately from death:
            // moving out of the real seven-unit attack envelope should
            // never still shoot toward the former locked position.
            foe.hp=30f;foe.x=foot.x+20f;
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=aim,target=foot
            };
            target.SetValue(g,foe);next.SetValue(g,2.8f);
            tick.Invoke(g,new object[]{.17f});
            Check(g.projectiles.Count==0&&g.effects.Count==effects&&
                (float)next.GetValue(g)<=.22f,
                "A target escaping physical range cancels old arrow release");

            // A subsequent genuinely shootable foe remains available,
            // without consuming a second false 2.8s cooldown.
            foe.x=foot.x;foe.y=foot.y;
            g.companionAction=null;next.SetValue(g,0f);
            tick.Invoke(g,new object[]{.02f});
            Check(g.companionAction!=null&&
                g.companionAction.kind=="attack"&&
                (Enemy)target.GetValue(g)==foe,
                "Short retry locks a freshly reachable, living target");
            tick.Invoke(g,new object[]{.16f});
            Check(g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "A recovered original bow release still creates a real arrow");
        }

        public static void GuardedAllyRetargetsWhenLockedMuzzleBecomesBlocked(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Real watchpost offers the veteran defense");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Ranger can take the physical guard order");
            g.Choose(guard);
            Check(g.OuterPatrolCompanionGuarding,
                "Release-time retargeting runs only under a real guard order");
            Point guardFoot=g.OuterPatrolCompanionGuardPosition;
            g.enemies.Clear();g.projectiles.Clear();

            var flags=System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance;
            var clear=typeof(GameCore).GetMethod("ClearAttackPath",flags);
            var shot=typeof(GameCore).GetMethod("WeaponShot",flags);
            var tick=typeof(GameCore).GetMethod("TickCompanion",flags);
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",flags);
            var target=typeof(GameCore).GetField("companionAutoTarget",flags);
            var cooldown=typeof(GameCore).GetField("companionAttack",flags);
            Check(clear!=null&&shot!=null&&tick!=null&&select!=null&&
                target!=null&&cooldown!=null,
                "Exercise real path, muzzle, target and release APIs");

            // The post's preferred guard foot is intentionally an open firing
            // lane. Find a nearby REAL walkable corner for the adversarial
            // case, rather than assuming the best guard tile has a blocked
            // muzzle. A live guard order remains active while the ally is
            // physically walking back to that saved post.
            List<Point> origins=new List<Point>();
            for(float dx=-8f;dx<=8f;dx+=.5f)
            for(float dy=-8f;dy<=8f;dy+=.5f)
            {
                Point candidate=guardFoot+new Point(dx,dy);
                if(!g.CanStand(candidate))continue;
                bool nearBlockedTile=false;
                for(int sx=-1;sx<=1&&!nearBlockedTile;sx++)
                for(int sy=-1;sy<=1;sy++)
                {
                    if(sx==0&&sy==0)continue;
                    if(!g.CanStand(candidate+new Point(sx*.7f,sy*.7f)))
                    {nearBlockedTile=true;break;}
                }
                if(nearBlockedTile)origins.Add(candidate);
            }
            origins.Sort((a,b)=>(a-guardFoot).Length.CompareTo((b-guardFoot).Length));
            Point origin=new Point(),first=new Point(),alternate=new Point(),
                corner=new Point();
            bool foundLayout=false;
            List<Point> open=new List<Point>();
            List<Point> muzzleBlocked=new List<Point>();
            foreach(Point candidateOrigin in origins)
            {
                open.Clear();muzzleBlocked.Clear();
                for(float dx=-6.5f;dx<=6.5f;dx+=.5f)
                for(float dy=-6.5f;dy<=6.5f;dy+=.5f)
                {
                    Point p=candidateOrigin+new Point(dx,dy);
                    float distance=(p-candidateOrigin).Length;
                    if(distance<1.4f||distance>=6.9f||!g.CanStand(p)||
                        !(bool)clear.Invoke(g,new object[]{candidateOrigin,p}))
                        continue;
                    Projectile probe=(Projectile)shot.Invoke(g,new object[]{
                        candidateOrigin,1,(p-candidateOrigin).Normalized,
                        9f,0f,(Point?)p});
                    if(probe.blockedAtSpawn)muzzleBlocked.Add(p);
                    else open.Add(p);
                }
                if(open.Count<2||muzzleBlocked.Count==0)continue;
                open.Sort((a,b)=>(a-candidateOrigin).Length.CompareTo(
                    (b-candidateOrigin).Length));
                Point candidateFirst=open[0],candidateAlternate=new Point(),
                    candidateCorner=new Point();
                bool foundAlternate=false,foundCorner=false;
                float firstDistance=(candidateFirst-candidateOrigin).Length;
                foreach(Point p in open)
                    if((p-candidateOrigin).Length>=firstDistance+1f&&
                        (p-candidateFirst).Length>=1.25f)
                    {candidateAlternate=p;foundAlternate=true;break;}
                foreach(Point p in muzzleBlocked)
                    if((p-candidateOrigin).Length<6.9f&&
                        (p-candidateFirst).Length>1f&&
                        (p-candidateAlternate).Length>1.25f)
                    {candidateCorner=p;foundCorner=true;break;}
                if(foundAlternate&&foundCorner)
                {
                    origin=candidateOrigin;first=candidateFirst;
                    alternate=candidateAlternate;corner=candidateCorner;
                    foundLayout=true;break;
                }
            }
            Check(foundLayout,
                "Find a real walkable origin with both a valid backup and a clear-feet/blocked-muzzle corner");
            g.companionPosition=origin;

            Enemy primary=new Enemy(920610,0,first.x,first.y,30f);
            Enemy backup=new Enemy(920611,0,alternate.x,alternate.y,30f);
            g.enemies.Add(primary);g.enemies.Add(backup);
            Check((Enemy)select.Invoke(g,null)==primary,
                "Automatic target selection chooses the nearest genuinely shootable foe");
            target.SetValue(g,primary);cooldown.SetValue(g,2.8f);
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(primary.Position-origin).Normalized,
                target=primary.Position
            };

            primary.x=corner.x;primary.y=corner.y;
            Point blockedAim=primary.Position-origin;
            Projectile blockedProbe=(Projectile)shot.Invoke(g,new object[]{
                origin,1,blockedAim.Normalized,9f,0f,(Point?)primary.Position});
            Check((bool)clear.Invoke(g,new object[]{origin,primary.Position})&&
                blockedProbe.blockedAtSpawn,
                "Locked foe keeps clear foot LOS while its real muzzle becomes blocked");
            tick.Invoke(g,new object[]{.17f});
            Check(g.companionAction!=null&&g.companionAction.released&&
                (Enemy)target.GetValue(g)==backup&&
                g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "Prepared shot redirects at release to the valid backup instead of wasting its contact");
            Check(g.companionAction.target.HasValue&&
                (g.companionAction.target.Value-backup.Position).Length<.01f,
                "Released projectile target and live aim stay on the replacement foe");

            // Ordinary field combat uses the same release-time muzzle
            // recovery, without relying on an active outpost guard order.
            GameCore field=new GameCore(data);field.mode="world";
            field.save.room=0;field.save.companion=1;
            field.save.x=data.maps[0].spawnX;field.save.y=data.maps[0].spawnY;
            field.companionPosition=origin;
            field.enemies.Clear();field.projectiles.Clear();
            Enemy fieldPrimary=new Enemy(920612,0,first.x,first.y,30f);
            Enemy fieldBackup=new Enemy(920613,0,alternate.x,alternate.y,30f);
            field.enemies.Add(fieldPrimary);field.enemies.Add(fieldBackup);
            var fieldTarget=typeof(GameCore).GetField("companionAutoTarget",flags);
            var fieldCooldown=typeof(GameCore).GetField("companionAttack",flags);
            var fieldTick=typeof(GameCore).GetMethod("TickCompanion",flags);
            Check(fieldTarget!=null&&fieldCooldown!=null&&fieldTick!=null,
                "Ordinary companion combat exposes its production release state");
            fieldTarget.SetValue(field,fieldPrimary);
            fieldCooldown.SetValue(field,2.8f);
            field.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(fieldPrimary.Position-origin).Normalized,
                target=fieldPrimary.Position
            };
            fieldPrimary.x=corner.x;fieldPrimary.y=corner.y;
            fieldTick.Invoke(field,new object[]{.17f});
            Check(field.companionAction!=null&&field.companionAction.released&&
                (Enemy)fieldTarget.GetValue(field)==fieldBackup&&
                field.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "Ordinary field auto-fire reuses its prepared contact on the legal backup");
            Check(field.companionAction.target.HasValue&&
                (field.companionAction.target.Value-fieldBackup.Position).Length<.01f,
                "Field release updates aim and projectile target together");

            // A foe can die after the companion has committed its draw.
            // The release must reacquire a living, actually shootable backup
            // instead of firing at the dead foe's stale coordinates.
            GameCore fieldKilled=new GameCore(data);fieldKilled.mode="world";
            fieldKilled.save.room=0;fieldKilled.save.companion=1;
            fieldKilled.companionPosition=origin;
            fieldKilled.enemies.Clear();fieldKilled.projectiles.Clear();
            Enemy doomed=new Enemy(920614,0,first.x,first.y,30f);
            Enemy survivor=new Enemy(920615,0,alternate.x,alternate.y,30f);
            fieldKilled.enemies.Add(doomed);fieldKilled.enemies.Add(survivor);
            fieldTarget.SetValue(fieldKilled,doomed);
            fieldCooldown.SetValue(fieldKilled,2.8f);
            fieldKilled.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(doomed.Position-origin).Normalized,
                target=doomed.Position
            };
            fieldKilled.enemies.Remove(doomed);
            fieldTick.Invoke(fieldKilled,new object[]{.17f});
            Check(fieldKilled.companionAction!=null&&
                fieldKilled.companionAction.released&&
                (Enemy)fieldTarget.GetValue(fieldKilled)==survivor&&
                fieldKilled.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "Ordinary field auto-fire reacquires a living backup when its locked foe dies mid-windup");
            Check(fieldKilled.companionAction.target.HasValue&&
                (fieldKilled.companionAction.target.Value-survivor.Position).Length<.01f,
                "Target death cannot leave the replacement arrow aimed at stale coordinates");

            // Automatic recovery must never rewrite an explicit user order.
            // Move its marked foe into the same muzzle-only corner after the
            // command starts: the command still fires along the captured aim.
            GameCore manual=new GameCore(data);manual.mode="world";
            manual.save.room=0;manual.save.companion=1;
            manual.companionPosition=origin;
            manual.enemies.Clear();manual.projectiles.Clear();
            Enemy commanded=new Enemy(920616,0,first.x,first.y,30f);
            Enemy bystander=new Enemy(920617,0,alternate.x,alternate.y,30f);
            manual.enemies.Add(commanded);manual.enemies.Add(bystander);
            manual.SetAimTarget(first);
            manual.CommandCompanion();
            Check(manual.companionAction!=null&&
                manual.companionAction.kind=="command"&&
                manual.companionAction.target.HasValue,
                "Player command captures its explicit world target");
            Point explicitTarget=manual.companionAction.target.Value;
            commanded.x=corner.x;commanded.y=corner.y;
            tick.Invoke(manual,new object[]{.21f});
            Check(manual.companionAction.released&&
                manual.companionAction.target.HasValue&&
                (manual.companionAction.target.Value-explicitTarget).Length<.01f&&
                manual.projectiles.Count==3&&
                manual.projectiles.TrueForAll(p=>p.friendly&&!p.blockedAtSpawn)&&
                target.GetValue(manual)==null,
                "Automatic retargeting never steals an explicit volley or changes its captured aim");
        }

        public static void GuardedAllyRedirectsPreparedRealArrowToIncomingStockThreat(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.companion=1;
            g.save.x=post.x;g.save.y=post.y;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Veteran defense is offered at the real post");
            g.Choose(start);
            g.Talk(post);
            int guard=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_guard");
            Check(guard>=0,"Ranger can guard stock during real siege");
            g.Choose(guard);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,"Both live melee and heavy exist");
            var selection=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var tick=typeof(GameCore).GetMethod("TickCompanion",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var target=typeof(GameCore).GetField("companionAutoTarget",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var cooldown=typeof(GameCore).GetField("companionAttack",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(selection!=null&&tick!=null&&target!=null&&cooldown!=null,
                "Regress actual automatic draw, target and released projectile");
            // Both shots must be possible before measuring target priority.
            Enemy available=(Enemy)selection.Invoke(g,null);
            Check(available==melee||available==heavy,
                "Registered stock raider is shootable through a real muzzle");
            Enemy alternate=available==melee?heavy:melee;
            Check(!alternate.targetingOuterPatrolStock,
                "Secondary saboteur starts without a telegraphed stock hit");
            cooldown.SetValue(g,0f);
            tick.Invoke(g,new object[]{.02f});
            Check(g.companionAction!=null&&
                g.companionAction.kind=="attack"&&
                (Enemy)target.GetValue(g)==available,
                "A real ally starts a planted, automatic bow windup");
            // Other live raider commits after bow drawing began; retarget
            // before physical release instead of firing into old stock lane.
            // The guard has already exhausted its finite two real body
            // contacts (tested by the genuine three-hit stock test).
            // This validates urgent auto-fire against an UNGUARDED lane.
            var blocks=typeof(GameCore).GetField("outerPatrolGuardBlocksLeft",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(blocks!=null,"Finite bodyguard shield is registered");
            blocks.SetValue(g,0);
            PlaceShootableCacheContact(g,post,alternate,available);
            MoveToRealUncoveredStockObserver(g,post,alternate,available);
            alternate.state=1;alternate.targetingOuterPatrolStock=true;
            Check(g.OuterPatrolIncomingStockThreat==alternate,
                "The late alternative has a real unprotected and visible stock strike");
            tick.Invoke(g,new object[]{.16f});
            Check(g.companionAction.released&&
                (Enemy)target.GetValue(g)==alternate&&
                g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "One genuine released projectile changes to urgent stock attacker");
            Check((g.companionAction.target.Value-alternate.Position).Length<.01f,
                "Prepared shot's direction and target both follow actual threat");

            // A second genuine stock attack can become more urgent while
            // the companion is already committed to another stock attacker.
            // Being a valid target is not enough: choose the earlier release.
            PlaceShootableCacheContact(g,post,alternate,available);
            PlaceShootableCacheContact(g,post,available,alternate);
            MoveToRealUncoveredStockObserver(g,post,alternate,available);
            alternate.state=1;alternate.targetingOuterPatrolStock=true;
            available.state=1;available.targetingOuterPatrolStock=true;
            alternate.timer=.9f;available.timer=.12f;
            Check(g.OuterPatrolIncomingStockThreat==available,
                "The second unguarded stock strike has the earlier real deadline");
            target.SetValue(g,alternate);
            cooldown.SetValue(g,2.8f);
            g.companionAction=new ActorAction {
                kind="attack",weapon=1,duration=.42f,contact=.16f,
                damage=12f,direction=(alternate.Position-
                    g.companionPosition).Normalized,target=alternate.Position
            };
            tick.Invoke(g,new object[]{.17f});
            Check(g.companionAction.released&&
                (Enemy)target.GetValue(g)==available&&
                g.projectiles.Exists(p=>p.friendly&&!p.blockedAtSpawn),
                "A faster stock strike supersedes an already-valid slower stock lock");
            Check((g.companionAction.target.Value-available.Position).Length<.01f,
                "Emergency retarget updates the real projectile aim and target together");
        }

        public static void StockWarningsAndCompanionCacheTargetsIgnoreBodyBlockedRaids(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            Check(post!=null,"The original active patrol cache exists");
            var g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Live veteran stock-defense can begin");
            g.Choose(start);
            Enemy rear=g.enemies.Find(e=>e.kind==0);
            Enemy front=g.enemies.Find(e=>e.kind==2);
            Check(g.OuterPatrolVeteranActive&&rear!=null&&front!=null,
                "Use the actual two raider archetypes from the active real stock siege");
            PlaceShootableCacheContact(g,post,rear,front);
            MoveToRealUncoveredStockObserver(g,post,rear,front);
            Point stock=new Point(post.x,post.y);
            rear.state=1;rear.timer=.4f;rear.windupDuration=.65f;
            rear.targetingOuterPatrolStock=true;rear.target=stock;
            rear.Face(stock-rear.Position);
            front.state=0;front.stun=100f;front.timer=100f;
            var lane=typeof(GameCore).GetMethod("EnemyMeleeLaneClear",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(lane!=null&&g.OuterPatrolIncomingStockThreat==rear,
                "A genuinely exposed near-stock melee warning is visible");
            Point oldFront=front.Position;
            bool covered=false;
            for(int i=3;i<=8&&!covered;i++)
            {
                Point position=rear.Position+(stock-rear.Position)*(i*.1f);
                if(!g.CanStand(position))continue;
                front.x=position.x;front.y=position.y;
                covered=!(bool)lane.Invoke(g,new object[]{rear,stock});
            }
            Check(covered&&g.CanStand(front.Position),
                "A living front raider occupies genuine standable ground on the rear saboteur's melee contact lane");
            Check(g.OuterPatrolIncomingStockThreat==null&&
                g.OuterPatrolIncomingStockSeconds<0f,
                "The actual cache HUD retracts the impossible body-blocked pending stock hit");
            float stockBefore=g.OuterPatrolIntegrity;
            rear.timer=.001f;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity==stockBefore&&
                rear.state==0&&
                g.effects.Exists(e=>e.kind=="number"&&
                    e.text==g.L("队友挡路","ALLY BLOCK")&&e.outerPatrolOwned),
                "The real blocked melee release consumes its attack without any fake stock loss and shows one encounter-owned ally-cover cue");
            front.x=oldFront.x;front.y=oldFront.y;
            rear.state=1;rear.timer=.001f;rear.windupDuration=.65f;
            rear.releaseTime=rear.hitTime=rear.stun=0f;
            rear.targetingOuterPatrolStock=true;rear.target=stock;
            Check((bool)lane.Invoke(g,new object[]{rear,stock})&&
                g.OuterPatrolIncomingStockThreat==rear,
                "Opening the same real lane restores the true imminent stock-hit warning");
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolIntegrity<stockBefore&&
                g.save.version==2,
                "An unobstructed released strike again takes actual protected stock rather than creating a permanent free shield");
        }

        public static void CompanionPrioritizesSoonestAuthenticStockStrike(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"A real veteran cache defense can be started");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "Both authentic stock-raiding enemy classes exist");
            PlaceShootableCacheContact(g,post,heavy,melee);
            PlaceShootableCacheContact(g,post,melee,heavy);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            heavy.state=1;heavy.targetingOuterPatrolStock=true;
            melee.state=1;melee.targetingOuterPatrolStock=true;
            Enemy near=(heavy.Position-g.companionPosition).Length<
                (melee.Position-g.companionPosition).Length?heavy:melee;
            Enemy far=near==heavy?melee:heavy;
            near.timer=.9f;far.timer=.12f;
            Check(g.OuterPatrolIncomingStockThreat==far,
                "Real stock-warning selector confirms the farther strike lands first");
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(select!=null,"Use the production companion target selector");
            Check((Enemy)select.Invoke(g,null)==far,
                "Companion chooses earliest real stock contact over the nearer slower raider");
            near.timer=far.timer=.4f;
            Check((Enemy)select.Invoke(g,null)==near,
                "Equal imminent contact times retain nearest-target tie breaking");
            near.timer=-.02f;far.timer=-.15f;
            Check((Enemy)select.Invoke(g,null)==far,
                "A farther but already-overdue stock hit outranks a less-overdue nearby raider");
        }

        public static void OuterPatrolWarningSelectsFiniteEarliestThreatAndStableTie(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"A real veteran cache warning can be exercised");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "Real melee and heavy stock attackers are present");
            PlaceShootableCacheContact(g,post,heavy,melee);
            PlaceShootableCacheContact(g,post,melee,heavy);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            heavy.state=melee.state=1;
            heavy.targetingOuterPatrolStock=melee.targetingOuterPatrolStock=true;
            heavy.timer=float.NaN;melee.timer=.2f;
            // Put the malformed timer first to prove it cannot poison the
            // warning selector before a later authentic finite threat.
            g.enemies.Remove(heavy);g.enemies.Insert(0,heavy);
            Check(g.OuterPatrolIncomingStockThreat==melee,
                "A NaN attack clock cannot hide the next real stock impact");
            Check(Math.Abs(g.OuterPatrolIncomingStockSeconds-.2f)<.001f,
                "Incoming-stock HUD exposes a finite countdown for a real strike");
            var select=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(select!=null,"Use the production companion threat selector");
            Check((Enemy)select.Invoke(g,null)==melee,
                "An invalid stock timer cannot monopolize the companion's target priority");
            heavy.timer=float.PositiveInfinity;
            Check(g.OuterPatrolIncomingStockThreat==melee&&
                (Enemy)select.Invoke(g,null)==melee,
                "Infinite stock timers are excluded from both warnings and emergency target priority");

            // Equal deadlines use physical distance to the protected stock,
            // then favor the heavy if both are at the same distance. Reorder
            // the live actors so list insertion order cannot choose the HUD.
            heavy.timer=melee.timer=.2f;
            Point cache=new Point(post.x,post.y);
            Point h=heavy.Position-cache,m=melee.Position-cache;
            float hd=h.x*h.x+h.y*h.y,md=m.x*m.x+m.y*m.y;
            Enemy expected=Math.Abs(hd-md)<.0001f?heavy:(hd<md?heavy:melee);
            Enemy other=expected==heavy?melee:heavy;
            g.enemies.Remove(other);g.enemies.Remove(expected);
            g.enemies.Insert(0,other);g.enemies.Insert(1,expected);
            Check(g.OuterPatrolIncomingStockThreat==expected,
                "Equal-deadline warnings choose the nearest physical stock contact independent of list order");
            Check(g.OuterPatrolIncomingStockHeavy==(expected.kind==2),
                "Heavy warning metadata matches the deterministically selected threat");

            Enemy fartherCache=Math.Abs(hd-md)<.0001f?melee:
                (hd>md?heavy:melee);
            Enemy nearerCache=fartherCache==heavy?melee:heavy;
            fartherCache.timer=-.15f;nearerCache.timer=-.02f;
            Check(g.OuterPatrolIncomingStockThreat==fartherCache&&
                g.OuterPatrolIncomingStockSeconds==0f,
                "Raw negative deadlines prioritize an overdue strike while the HUD clamps its display to zero");
        }

        public static void OuterPatrolInvalidWindupsRecoverAndRetractWarnings(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;g.save.companion=1;
            g.companionPosition=new Point(post.x+1.1f,post.y+1.1f);
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"A real cache defense starts the recovery test");
            g.Choose(start);
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Enemy heavy=Array.Find(g.enemies.ToArray(),e=>e.kind==2);
            Check(melee!=null&&heavy!=null,
                "The real patrol enemy roster is available");
            PlaceShootableCacheContact(g,post,heavy,melee);
            MoveToRealUncoveredStockObserver(g,post,heavy,melee);
            g.save.companion=-1;
            foreach(Enemy foe in g.enemies)
            {
                if(foe==heavy)continue;
                foe.state=0;foe.timer=5f;
                foe.targetingWayfarer=foe.targetingReliefCivilian=
                    foe.targetingAfterlightStock=foe.targetingOuterPatrolStock=false;
            }
            heavy.state=1;heavy.timer=float.NaN;
            heavy.targetingOuterPatrolStock=true;
            heavy.target=new Point(post.x,post.y);
            float integrity=g.OuterPatrolIntegrity;
            var flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic;
            var pendingField=typeof(GameCore).GetField("rescueWindupEffects",flags);
            Check(pendingField!=null,
                "The real pending-windup warning registry is available");
            object table=pendingField.GetValue(g);
            var add=table.GetType().GetMethod("Add");
            Check(add!=null,"Pending warning ownership can be registered");
            Effect nanWarning=new Effect("danger",heavy.target,1f);
            g.effects.Add(nanWarning);
            add.Invoke(table,new object[]{heavy,nanWarning});
            g.Tick(.02f,new Point());
            Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                !heavy.targetingOuterPatrolStock,
                "A NaN windup recovers into a short safe reacquisition instead of becoming immortal");
            Check(!g.effects.Contains(nanWarning),
                "The invalid enemy's stale danger warning is removed by owner identity");
            Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                "Invalid enemy clocks cannot cause an untelegraphed stock hit");

            heavy.state=1;heavy.timer=float.PositiveInfinity;
            heavy.targetingOuterPatrolStock=true;
            Effect infiniteWarning=new Effect("danger",heavy.target,1f);
            g.effects.Add(infiniteWarning);
            add.Invoke(table,new object[]{heavy,infiniteWarning});
            g.Tick(.02f,new Point());
            Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                !heavy.targetingOuterPatrolStock&&
                !g.effects.Contains(infiniteWarning),
                "An infinite windup also retires its target lock and warning cleanly");
            Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                "Neither malformed clock can bypass the real stock-contact checks");

            // Idle cooldowns use the same transient clock and must recover too;
            // otherwise an enemy silently stops attacking for the rest of combat.
            heavy.state=0;heavy.timer=float.NaN;
            heavy.targetingOuterPatrolStock=true;
            Effect idleWarning=new Effect("danger",heavy.target,1f);
            g.effects.Add(idleWarning);
            add.Invoke(table,new object[]{heavy,idleWarning});
            g.Tick(.02f,new Point());
            Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                !heavy.targetingOuterPatrolStock&&!g.effects.Contains(idleWarning),
                "A malformed idle cooldown also resets and retires stale objective cues");
            Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                "Idle timer recovery cannot create an untelegraphed stock hit");

            // A malformed reaction clock must be repaired BEFORE it enters
            // Math.Max/Min lock-budget math. Exercise actual patrol windups and
            // owner-keyed warning cleanup, including the next safe tick.
            System.Reflection.FieldInfo clockField;
            foreach (string clockName in new[] {
                "hitTime", "stun", "releaseTime", "windupDuration" })
            foreach (float malformed in new[] {
                float.NaN, float.PositiveInfinity })
            {
                heavy.state=1;heavy.timer=.8f;heavy.hitTime=0f;
                heavy.stun=0f;heavy.releaseTime=0f;heavy.windupDuration=.7f;
                heavy.flash=.1f;heavy.stagger=0f;heavy.staggerRecovery=0f;
                heavy.moveTime=.2f;heavy.recoil=new Point(1f,0f);
                heavy.targetingWayfarer=heavy.targetingReliefCivilian=false;
                heavy.targetingAfterlightStock=false;
                heavy.targetingOuterPatrolStock=true;
                heavy.target=new Point(post.x,post.y);
                clockField=typeof(Enemy).GetField(clockName);
                Check(clockField!=null,"The production enemy reaction clock exists: "+clockName);
                clockField.SetValue(heavy,malformed);
                Effect warning=new Effect("danger",heavy.target,1f);
                g.effects.Add(warning);add.Invoke(table,new object[]{heavy,warning});
                g.Tick(.02f,new Point());
                Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                    !heavy.targetingOuterPatrolStock&&
                    !g.effects.Contains(warning),
                    "Malformed "+clockName+" must cancel the unsafe windup and retract only its own warning");
                Check(!float.IsNaN(heavy.timer)&&!float.IsInfinity(heavy.timer)&&
                    !float.IsNaN(heavy.hitTime)&&!float.IsInfinity(heavy.hitTime)&&
                    !float.IsNaN(heavy.stun)&&!float.IsInfinity(heavy.stun)&&
                    !float.IsNaN(heavy.releaseTime)&&!float.IsInfinity(heavy.releaseTime)&&
                    !float.IsNaN(heavy.windupDuration)&&!float.IsInfinity(heavy.windupDuration),
                    "Reaction-clock repair leaves every action deadline finite");
                Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                    "Malformed "+clockName+" cannot produce an untelegraphed stock impact");
                g.Tick(.02f,new Point());
                Check(!float.IsNaN(heavy.timer)&&!float.IsInfinity(heavy.timer)&&
                    heavy.state==0,
                    "A repaired "+clockName+" remains recoverable on the following real world tick");
            }

            // Cosmetic/poise clocks should be normalized without cancelling a
            // valid, already-readable windup or changing its real deadline.
            foreach (string clockName in new[] {
                "flash", "stagger", "staggerRecovery", "moveTime" })
            foreach (float malformed in new[] {
                float.NaN, float.NegativeInfinity })
            {
                heavy.state=1;heavy.timer=5f;heavy.hitTime=0f;heavy.stun=0f;
                heavy.releaseTime=0f;heavy.windupDuration=.7f;
                heavy.recoil=new Point(1f,0f);
                heavy.targetingOuterPatrolStock=true;
                clockField=typeof(Enemy).GetField(clockName);
                Check(clockField!=null,"The production enemy presentation clock exists: "+clockName);
                clockField.SetValue(heavy,malformed);
                g.Tick(.02f,new Point());
                Check(heavy.state==1&&Math.Abs(heavy.timer-4.98f)<.01f,
                    "Repairing cosmetic/poise "+clockName+" must not interrupt a valid telegraphed attack");
                float repaired=(float)clockField.GetValue(heavy);
                Check(!float.IsNaN(repaired)&&!float.IsInfinity(repaired),
                    "Cosmetic/poise clock "+clockName+" must remain finite after a real tick");
                Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                    "Repairing "+clockName+" cannot alter attack release timing or damage");
            }

            // A malformed recoil vector is just as dangerous as a bad timer:
            // it must not feed NaN into the real swept collision solver.
            foreach (Point malformedRecoil in new[] {
                new Point(float.NaN,0f), new Point(0f,float.PositiveInfinity) })
            {
                heavy.state=1;heavy.timer=.8f;heavy.hitTime=.1f;
                heavy.stun=0f;heavy.releaseTime=0f;heavy.windupDuration=.7f;
                heavy.recoil=malformedRecoil;
                heavy.targetingOuterPatrolStock=true;
                heavy.target=new Point(post.x,post.y);
                Effect warning=new Effect("danger",heavy.target,1f);
                g.effects.Add(warning);add.Invoke(table,new object[]{heavy,warning});
                float x=heavy.x,y=heavy.y;
                g.Tick(.02f,new Point());
                Check(heavy.state==0&&Math.Abs(heavy.timer-.22f)<.001f&&
                    !g.effects.Contains(warning)&&
                    Math.Abs(heavy.x-x)<.0001f&&Math.Abs(heavy.y-y)<.0001f,
                    "Malformed recoil must cancel safely before corrupting the actor's swept world position");
                Check(Math.Abs(g.OuterPatrolIntegrity-integrity)<.001f,
                    "Malformed recoil cannot create a false objective contact");
            }
        }

        public static void CompanionPrioritizesRealOutpostSaboteursOverPassingFoes(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.save.x=post.x;g.save.y=post.y;
            g.enemies.Clear();
            g.save.flags.Add("outer_cache_0");
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"An eligible real watchpost can start combat");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "Raider priority depends on a genuine live defense");
            Enemy saboteur=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(saboteur!=null,"A cache saboteur has a genuine world body");
            g.save.companion=1;
            g.companionPosition=new Point(post.x,post.y);
            var chooser=typeof(GameCore).GetMethod("NearestCompanionThreat",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(chooser!=null,"Actual companion targeting is exercised");
            Enemy picked=(Enemy)chooser.Invoke(g,null);
            Check(picked==saboteur,
                "A visible melee stock saboteur beats a ranged raid distractor");
            // An unrelated foe even closer to Ivo should not draw the
            // bodyguard away from the cache while the defense is live.
            Enemy decoy=new Enemy(8111,0,post.x+.3f,post.y,30f);
            g.enemies.Add(decoy);
            picked=(Enemy)chooser.Invoke(g,null);
            Check(picked==saboteur,
                "Cache defense prioritizes its actual protected objective");
            g.enemies.Remove(decoy);
            // When the actual attack is obstructed, do not aim forever
            // through a closed wall: the standard line/muzzle validation
            // must still choose a legal unrelated enemy.
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            Check(abort>=0,"The defended physical post offers withdrawal");
            g.Choose(abort);
            g.enemies.Add(decoy);
            Check((Enemy)chooser.Invoke(g,null)==decoy,
                "After the real defense is withdrawn auto targeting remains normal");
        }

        public static void VeteranFlawlessMedalsRequireRealThreeKillsAndAreEarnableOnReplay(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Flawless medals rely on genuine Hit transactions");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();g.projectiles.Clear();
                g.save.level=25;g.save.xp=0;
                g.save.x=post.x;g.save.y=post.y;
                g.save.flags.Add("outer_cache_"+room);
                g.save.flags.Add("outer_patrol_done_"+room);
                g.save.inventory[0]=6;g.save.inventory[4]=4;
                int coins=g.save.coins,shards=g.save.shards;
                // Eight-region mastery reward is independent of initial
                // eight-veteran bonuses and ordinary XP leveling.
                if(room==7)
                    for(int i=0;i<7;i++)
                    {
                        g.save.flags.Add("outer_patrol_done_"+i);
                        g.save.flags.Add("outer_patrol_veteran_"+i);
                        g.save.flags.Add("outer_patrol_flawless_"+i);
                    }
                int talent=g.save.skillPoints;
                g.Talk(post);
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(start>=0,"Veteran fight is physically offered");
                g.Choose(start);
                Check(g.OuterPatrolFlawlessEligible,
                    "Clean challenge is eligible at untouched 100% stock");
                g.Talk(post);
                int brace=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_brace");
                Check(brace>=0,"Veteran post allows voluntary protective cover");
                g.Choose(brace);
                Check(!g.OuterPatrolFlawlessEligible,
                    "Deploying tactical cover forfeits this run's no-assist medal");
                Enemy[] foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolVeteranDone(room)&&
                    !g.OuterPatrolFlawlessDone(room),
                    "Three genuine kills with a barricade pay base win but not a medal");
                g.Talk(post);
                start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                g.Choose(start);
                Check(g.OuterPatrolFlawlessEligible,
                    "Replay has a fresh honest no-assist challenge");
                foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolFlawlessDone(room)&&
                    !g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shards+2,
                    "Clean real replay awards permanent medal without duplicate first-clear loot");
                if(room==7)
                    Check(g.OuterPatrolFlawlessWins==8&&
                        g.Has("outer_patrol_flawless_all")&&
                        g.save.skillPoints==talent+2,
                        "Genuine eighth veteran and eighth flawless each grant one skill point");
                else Check(g.save.skillPoints==talent,
                    "Partial flawless collection cannot farm talent points");
                SaveData persisted=new SaveData{
                    version=2,room=g.save.room,stage=g.save.stage,
                    x=g.save.x,y=g.save.y,
                    presentationVersion=g.save.presentationVersion,
                    flags=new List<string>(g.save.flags),
                    trackedQuest="outer_watch_flawless"
                };
                GameCore reload=new GameCore(data,persisted);
                Check(reload.save.version==2&&
                    reload.OuterPatrolFlawlessDone(room)&&
                    reload.save.trackedQuest=="outer_watch_flawless",
                    "New mastery medal is an ordinary legacy-compatible v2 flag and journal");
                g.Talk(post);
                start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                g.Choose(start);
                foes=g.enemies.ToArray();
                foreach(Enemy e in foes)
                {
                    hit.Invoke(g,new object[]{e,10000f,true,null,null});
                    for(int i=0;i<12;i++)g.Tick(.02f,new Point());
                }
                Check(g.OuterPatrolFlawlessDone(room)&&
                    g.save.coins==coins+26&&g.save.shards==shards+2,
                    "Third full authentic clean win cannot repeat rewards");
            }
        }

        public static void FlawlessFieldCareActuallyImprovesSafeTickAndStopsDuringRaids(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_patrol_done_0");
            g.save.flags.Add("outer_patrol_veteran_0");
            g.save.health=45f;g.save.energy=30f;
            Check(g.OuterPatrolRefuge,"Defended post is a real place of recovery");
            g.Tick(.05f,new Point());
            float veteranHp=g.save.health-45f;
            float veteranEnergy=g.save.energy-30f;
            g.save.flags.Add("outer_patrol_flawless_0");
            g.save.health=45f;g.save.energy=30f;
            g.Tick(.05f,new Point());
            float flawlessHp=g.save.health-45f;
            float flawlessEnergy=g.save.energy-30f;
            Check(flawlessHp>veteranHp+.025f&&
                flawlessEnergy>veteranEnergy+.05f,
                "Real 50ms game tick grants strictly better local care after medal");
            g.enemies.Add(new Enemy(9911,0,post.x+.7f,post.y,32f));
            g.save.health=45f;g.save.energy=30f;
            Check(!g.OuterPatrolRefuge,
                "Real nearby raider disables medal-powered refuge care");
            g.Tick(.05f,new Point());
            float blockedHp=g.save.health;
            float blockedEnergy=g.save.energy;
            // The hero has native combat energy regeneration even with no
            // refuge. Compare the SAME real threat with and without the medal
            // instead of incorrectly requiring all regeneration to stop.
            g.save.flags.Remove("outer_patrol_flawless_0");
            g.save.health=45f;g.save.energy=30f;
            g.Tick(.05f,new Point());
            Check(blockedHp<=45f&&g.save.health<=45f&&
                Math.Abs(blockedEnergy-g.save.energy)<.01f,
                "A hostile raider suppresses medal care without removing ordinary stamina recovery");
            g.save.flags.Add("outer_patrol_flawless_0");
            g.enemies.Clear();
            g.save.x=post.x+20f;g.save.y=post.y+20f;
            Check(!g.OuterPatrolRefuge,
                "Flawless reward never grants region-wide free healing");
            GameCore reloaded=new GameCore(data,new SaveData{
                version=2,room=0,x=post.x,y=post.y,
                flags=new List<string>(g.save.flags),
                presentationVersion=g.save.presentationVersion
            });
            Check(reloaded.OuterPatrolFlawlessDone(0)&&
                reloaded.OuterPatrolRefuge,
                "Old v2 save retains the field care upgrade without new fields");
        }

        public static void LateLiveInterceptionProtectsCacheOnRealEnemyRelease(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_cache_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Defend an actual supply cache");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "An authentic raid has two real enemy actors");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null&&(melee.Position-new Point(post.x,post.y)).Length<=2.35f,
                "Raid melee attacker starts at genuine contact range");
            // The target lock and telegraph are already committed to stock.
            // A defender enters AFTER the windup began but BEFORE the
            // real release, so it must still be possible to block the cache.
            melee.state=1;melee.timer=.02f;
            melee.target=new Point(post.x,post.y);
            melee.targetingOuterPatrolStock=true;
            melee.Face(melee.target-melee.Position);
            melee.stun=0f;melee.releaseTime=0f;
            float before=g.OuterPatrolIntegrity;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity==before,
                "Defender arriving during authentic attack windup intercepts its stock hit");
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.outerPatrolOwned&&e.text.Contains(
                    g.save.english?"INTERCEPTED":"拦截成功")),
                "Actual blocked release gives localized, owned readable combat feedback");
            // The SAME attacker and identical release must damage an
            // unobstructed physical store once the hero leaves the lane.
            Point retreat=new Point();bool found=false;
            for(int dx=-10;dx<=10&&!found;dx++)
            for(int dy=-10;dy<=10&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if((p-new Point(post.x,post.y)).Length>10f||
                    (p-melee.Position).Length<5f||!g.CanStand(p))continue;
                retreat=p;found=true;
            }
            Check(found,"Find genuine unoccupied map floor away from melee");
            g.save.x=retreat.x;g.save.y=retreat.y;
            melee.state=1;melee.timer=.02f;
            melee.target=new Point(post.x,post.y);
            melee.targetingOuterPatrolStock=true;
            melee.Face(melee.target-melee.Position);
            melee.stun=0f;melee.releaseTime=0f;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity<before,
                "With no defender in range, an identical real contact damages stock");
            // Ordinary regional defense and old saves retain their original
            // one-time reward/failure flags; this is transient interception.
            Check(!g.OuterPatrolDone(0)&&g.save.version==2,
                "Interception is not a forged victory or a save schema migration");
        }

        public static void VeteranBarricadeSpendsRealSuppliesAndAbsorbsReleasedStockHits(Campaign data)
        {
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.enemies.Clear();g.projectiles.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            g.save.flags.Add("outer_patrol_done_0");
            g.save.inventory[0]=8;g.save.inventory[4]=4;
            // The ordinary first-time encounter must not pretend that a
            // veteran barricade is a usable menu command.
            GameCore ordinary=new GameCore(data);ordinary.mode="world";
            ordinary.save.room=0;ordinary.enemies.Clear();
            ordinary.save.x=post.x;ordinary.save.y=post.y;
            ordinary.save.flags.Add("outer_cache_0");
            ordinary.save.inventory[0]=8;ordinary.save.inventory[4]=4;
            ordinary.Talk(post);
            int normal=Array.FindIndex(ordinary.conversation.choices,
                c=>c.action=="outer_watch_start");
            ordinary.Choose(normal);
            ordinary.Talk(post);
            Check(Array.FindIndex(ordinary.conversation.choices,
                c=>c.action=="outer_watch_brace")<0,
                "Standard two-raider raid never advertises veteran-only cover");
            ordinary.Close();
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_veteran");
            Check(start>=0,"Completed relief post offers actual veteran raid");
            g.Choose(start);
            Check(g.OuterPatrolVeteranActive&&g.OuterPatrolBraceIntegrity==0&&
                g.OuterPatrolBraceAvailable,"Veteran assault offers an unused physical brace");
            g.Talk(post);
            int brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace>=0,"Barricade is a genuine post interaction");
            g.Choose(brace);
            Check(g.OuterPatrolBraceIntegrity==48f&&
                g.OuterPatrolBraceStanding&&
                g.OuterPatrolBraceHitFlash==0f&&
                !g.OuterPatrolBraceAvailable&&
                g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "First barricade spends two real timber and one spare part");
            g.Talk(post);
            brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace<0,"An already built barricade is not offered again");
            var invoke=typeof(GameCore).GetMethod("ChooseOuterPatrol",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(invoke!=null,"Old cached attack menu is still defended");
            invoke.Invoke(g,new object[]{"outer_watch_brace"});
            Check(g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "Forged cached brace choice cannot buy a second barricade");
            // A private handler call bypasses Choose(index)'s automatic
            // Close(); restore gameplay mode before advancing real AI time.
            g.Close();
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"Veteran raid owns an authentic melee stock attacker");
            Point safe=new Point();bool found=false;
            for(int radius=7;radius<=10&&!found;radius++)
            for(int dx=-radius;dx<=radius&&!found;dx++)
            for(int dy=-radius;dy<=radius&&!found;dy++)
            {
                Point p=new Point(post.x+dx,post.y+dy);
                if((p-new Point(post.x,post.y)).Length>11f||
                    (p-melee.Position).Length<6f||!g.CanStand(p))
                    continue;
                safe=p;found=true;
            }
            Check(found,"A real walkable observation foot exists near cache");
            g.save.x=safe.x;g.save.y=safe.y;
            float stock=g.OuterPatrolIntegrity;
            for(int frame=0;frame<1100&&g.OuterPatrolActive&&
                g.OuterPatrolBraceIntegrity==48f;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolBraceIntegrity<48f,
                "Unintercepted real raider winds up and releases into the barricade");
            Check(g.OuterPatrolBraceStanding&&g.OuterPatrolBraceHitFlash>0f,
                "A real stock contact drives visible barricade impact feedback");
            Check(g.OuterPatrolIntegrity==stock,
                "A real stock strike first consumes physical cover, not protected cache HP");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            brace=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_brace");
            Check(brace<0,"Broken or damaged used cover stays unavailable");
            invoke.Invoke(g,new object[]{"outer_watch_brace"});
            Check(g.save.inventory[0]==6&&g.save.inventory[4]==3,
                "A damaged cover cannot be re-purchased during same siege");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.OuterPatrolBraceIntegrity==0f&&
                !g.OuterPatrolBraceStanding&&g.OuterPatrolBraceHitFlash==0f,
                "Withdrawal retires live barricade artwork cues and attackers");
            GameCore restored=new GameCore(data,g.save);
            Check(!restored.OuterPatrolActive&&restored.OuterPatrolBraceIntegrity==0&&
                restored.save.version==2,"Old v2 saves never persist temporary cover");
        }

        public static void VeteranReliefDefenseHasThreeRealKillsOneTimeRewardAndSafeReplay(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(hit!=null,"Use genuine combat Hit, not fake victory flags");
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null,"Veteran post is authored in room "+room);
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.enemies.Clear();g.projectiles.Clear();
                // Isolate the distinct veteran "eight defended" talent
                // from ordinary XP level-ups caused by four-to-six real
                // kill/reward transactions in this same integration test.
                // Level 25 begins at zero XP, far from its next threshold.
                g.save.level=25;g.save.xp=0;
                g.save.x=post.x;g.save.y=post.y;
                g.save.flags.Add("outer_cache_"+room);
                g.save.flags.Add("outer_patrol_done_"+room);
                int coins=g.save.coins;
                // Final veteran clear must really award exactly one talent
                // after *eight* regional victories, never before.
                if(room==7)
                    for(int previous=0;previous<7;previous++)
                    {
                        g.save.flags.Add("outer_patrol_done_"+previous);
                        g.save.flags.Add("outer_patrol_veteran_"+previous);
                    }
                int talents=g.save.skillPoints;
                g.Talk(post);
                Check(g.conversation!=null,
                    "Veteran battle is offered at a physical post in region "+room);
                int veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,
                    "Already guarded supply depot offers actual veteran combat");
                g.Choose(veteran);
                Check(g.OuterPatrolActive&&g.OuterPatrolVeteranActive&&
                    g.OuterPatrolTime>109f&&g.enemies.Count==3,
                    "Veteran raid spawns three genuine enemy bodies and a full cache clock");
                Enemy[] raiders=g.enemies.ToArray();
                Check(raiders[0].kind==0&&raiders[1].kind==1&&
                    raiders[2].kind==2&&raiders[2].hp>raiders[0].hp,
                    "Veteran squad has melee, real ranged, and tougher slammer");
                int shard=g.save.shards;
                hit.Invoke(g,new object[]{raiders[0],10000f,true,null,null});
                for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                hit.Invoke(g,new object[]{raiders[1],10000f,true,null,null});
                for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                Check(!g.OuterPatrolVeteranDone(room)&&g.OuterPatrolActive,
                    "Two kills cannot counterfeit a three-member veteran victory");
                hit.Invoke(g,new object[]{raiders[2],10000f,true,null,null});
                for(int n=0;n<15;n++)g.Tick(.02f,new Point());
                Check(g.OuterPatrolVeteranDone(room)&&
                    !g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shard+2,
                    "Three genuine lethal transactions award exactly one veteran clear");
                Check(room==7?
                    g.OuterPatrolVeteranWins==8&&
                    g.Has("outer_patrol_veteran_all")&&
                    g.save.skillPoints==talents+1:
                    g.save.skillPoints==talents,
                    "Exactly the eighth genuine regional veteran win awards the extra talent");
                // Loading a NEW hero may sanitize an otherwise locked
                // region. Don't alias the current active hero's SaveData:
                // this check must not teleport the current challenger.
                SaveData persisted=new SaveData {
                    version=g.save.version, room=g.save.room,
                    stage=g.save.stage,x=g.save.x,y=g.save.y,
                    flags=new List<string>(g.save.flags)
                };
                GameCore reloaded=new GameCore(data,persisted);
                Check(reloaded.save.version==2&&
                    reloaded.OuterPatrolVeteranDone(room)&&
                    !reloaded.OuterPatrolVeteranActive,
                    "Veteran clear persists in existing v2 flags with no transient raid leak");
                g.Talk(post);
                Check(g.conversation!=null,"Completed veteran post "+
                    room+" remains physically interactable");
                veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,"Veteran combat remains playable after victory");
                g.Choose(veteran);
                Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                    "Replay actually spawns combat again, not a free menu reward");
                Enemy[] replay=g.enemies.ToArray();
                foreach(Enemy raider in replay)
                {
                    hit.Invoke(g,new object[]{raider,10000f,true,null,null});
                    for(int n=0;n<12;n++)g.Tick(.02f,new Point());
                }
                Check(!g.OuterPatrolActive&&g.save.coins==coins+26&&
                    g.save.shards==shard+2,
                    "Actual lethal veteran replay produces no duplicate coins or shards");
                g.Talk(post);
                veteran=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_veteran");
                Check(veteran>=0,"Veteran post stays replayable after a full second victory");
                g.Choose(veteran);
                Check(g.OuterPatrolVeteranActive&&g.enemies.Count==3,
                    "A third veteran attempt still creates a real three-raider fight");
                g.enemies.Clear();g.Tick(.02f,new Point());
                Check(!g.OuterPatrolActive&&g.save.coins==coins+26,
                    "Interrupted subsequent attempts cannot farm a reward");
            }
        }

        public static void VeteranOutpostJournalTracksTrueRegionalFlagsAndLiveObjective(Campaign data)
        {
            GameCore g=new GameCore(data);g.mode="world";
            QuestGuide q=g.GetQuest("outer_watch_veteran");
            Check(q.locked&&!q.complete&&q.steps.Count==8,
                "New quest is dormant until a real ordinary supply defense");
            g.save.flags.Add("outer_patrol_done_0");
            q=g.GetQuest("outer_watch_veteran");
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            Check(!q.locked&&!q.complete&&q.room==0&&
                (q.target-new Point(post.x,post.y)).Length<.1f,
                "Journal points to the actual unlocked region-zero veteran post");
            g.save.flags.Add("outer_patrol_veteran_0");
            q=g.GetQuest("outer_watch_veteran");
            Check(q.steps[0].done&&q.Progress>.12f&&
                !q.complete,"Genuine permanent veteran flag advances the tracked quest");
            for(int room=1;room<8;room++)
            {
                g.save.flags.Add("outer_patrol_done_"+room);
                g.save.flags.Add("outer_patrol_veteran_"+room);
            }
            q=g.GetQuest("outer_watch_veteran");
            Check(q.complete&&q.Progress==1f,
                "All eight persisted veteran victories complete the in-game quest");
            SaveData persisted=new SaveData {
                version=2,room=0,trackedQuest="outer_watch_veteran",
                // Preserve modern v2 journal presentation. A fresh
                // presentationVersion=0 correctly migrates a LEGACY UI
                // and resets old tracking, as the original game requires.
                presentationVersion=g.save.presentationVersion,
                flags=new List<string>(g.save.flags)
            };
            GameCore restored=new GameCore(data,persisted);
            Check(restored.save.trackedQuest=="outer_watch_veteran"&&
                restored.GetQuest("outer_watch_veteran").complete,
                "Legacy v2 journal selection survives loading a separate slot");

            // A real running siege should outrank the tracked objective
            // while retaining that tracked quest for the next safe moment.
            GameCore siege=new GameCore(data);siege.mode="world";
            siege.save.room=0;siege.enemies.Clear();
            siege.save.x=post.x;siege.save.y=post.y;
            siege.save.flags.Add("outer_cache_0");
            siege.save.trackedQuest="outer_watch_veteran";
            siege.Talk(post);
            int start=Array.FindIndex(siege.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Real outpost is combat-ready");
            siege.Choose(start);
            Point? marker=siege.NavigationTarget();
            Check(siege.OuterPatrolActive&&marker.HasValue&&
                (marker.Value-new Point(post.x,post.y)).Length<.1f&&
                siege.save.trackedQuest=="outer_watch_veteran",
                "World objective marker tracks live cache without rewriting saved quest");
        }

        public static void OuterPatrolCacheDefenseUsesRealTimedStockCombat(Campaign data)
        {
            // The existing eight-map witness proves physically playable roots.
            // Here a real melee unit must telegraph, wind up and reach the
            // actual store instead of magically draining it or only chasing
            // a distant hero. Old v2 flags stay unaltered on withdrawal.
            int room=0;
            WorldObject post=Array.Find(data.maps[room].objects,
                o=>o.kind=="outerwatch");
            Check(post!=null,"Canal has a real physical relief watchpost");
            GameCore g=new GameCore(data);
            g.save.room=room;g.enemies.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_"+room);
            g.save.inventory[0]=5;g.save.inventory[2]=5;
            int coins=g.save.coins;
            g.Talk(post);
            int action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(action>=0,"Recovered supplies permit a genuine timed siege");
            g.Choose(action);
            Check(g.OuterPatrolActive&&g.OuterPatrolTime>=94f&&
                g.OuterPatrolIntegrity==100f,
                "New relief siege has real unsaved clock and full cache integrity");
            Enemy melee=Array.Find(g.enemies.ToArray(),e=>e.kind==0);
            Check(melee!=null,"A real melee attacker targets the defended cache");
            // Position hero on a legal tile outside melee interception range.
            // The attacker must walk toward the post, not passively drain HP.
            Point safe=new Point();bool found=false;
            for(int radius=7;radius<=10&&!found;radius++)
            for(int x=-radius;x<=radius&&!found;x++)
            for(int y=-radius;y<=radius&&!found;y++)
            {
                Point candidate=new Point(post.x+x,post.y+y);
                if((candidate-new Point(post.x,post.y)).Length>11f||
                    (candidate-melee.Position).Length<6f||!g.CanStand(candidate))
                    continue;
                safe=candidate;found=true;
            }
            Check(found,"Find an actual legal bodyguard withdrawal foot");
            g.save.x=safe.x;g.save.y=safe.y;
            float before=g.OuterPatrolIntegrity;
            for(int frame=0;frame<1250&&g.OuterPatrolActive&&
                    g.OuterPatrolIntegrity==before;frame++)
                g.Tick(.02f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolIntegrity<before,
                "Unintercepted raider actually completes a timed attack against supplies");
            float damaged=g.OuterPatrolIntegrity;
            // Only the physically present guard can apply the once-per-run
            // emergency patch. Do not touch permanent cache flag.
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            Check(g.conversation!=null,"Live outpost can report raid integrity");
            int patch=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_patch");
            Check(patch>=0,"The ongoing defense offers a player-choice repair");
            g.Choose(patch);
            Check(g.OuterPatrolIntegrity>damaged&&g.OuterPatrolIntegrity<=100f&&
                g.save.inventory[0]==4&&g.save.inventory[2]==4,
                "First legitimate cache patch spends real supplies and repairs durability");
            g.Talk(post);
            patch=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_patch");
            Check(patch>=0,"Patch choice remains visible but is single-use");
            g.Choose(patch);
            Check(g.save.inventory[0]==4&&g.save.inventory[2]==4,
                "Repeating a cached UI action cannot spend a second repair");
            g.Talk(post);
            int abort=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_abort");
            Check(abort>=0,"The post allows explicit safe withdrawal");
            g.Choose(abort);
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(room)&&g.save.coins==coins&&
                g.OuterCacheRecovered(room),
                "Aborted physical siege retires attackers, pays no false reward and preserves v2 cache");
            GameCore reload=new GameCore(data,g.save);
            Check(!reload.OuterPatrolActive&&reload.OuterCacheRecovered(room)&&
                !reload.OuterPatrolDone(room),
                "Transient siege pressure never contaminates legacy save slots");
        }

        public static void OuterPatrolRealTimeoutAndPerimeterDoNotForgeWins(Campaign data)
        {
            // Timeout is a real world clock, not a local-wall-clock task.
            // Stun actual raiders only to isolate the countdown from their
            // otherwise-tested genuine melee pressure.
            WorldObject post=Array.Find(data.maps[0].objects,
                o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);
            g.save.room=0;g.enemies.Clear();
            g.save.x=post.x;g.save.y=post.y;
            g.save.flags.Add("outer_cache_0");
            int coins=g.save.coins;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Eligible physical outpost offers a real defense");
            g.Choose(start);
            foreach(Enemy attacker in g.enemies)attacker.stun=120f;
            for(int i=0;i<1860;i++)g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&g.OuterPatrolTime>1f&&
                g.OuterPatrolTime<3f&&g.OuterPatrolIntegrity==100f,
                "Combat clock advances on genuine simulation time without phantom supply damage");
            for(int i=0;i<80&&g.OuterPatrolActive;i++)
                g.Tick(.05f,new Point());
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(0)&&g.save.coins==coins&&
                g.OuterCacheRecovered(0),
                "Real timeout retires only its raiders with no forged reward or lost v2 cache");
            g.Talk(post);
            start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Timed-out cache defense can be started again");
            g.Choose(start);
            Point away=new Point();bool found=false;
            for(int y=1;y<g.Map.rows.Length-1&&!found;y++)
            for(int x=1;x<g.Map.rows[y].Length-1&&!found;x++)
            {
                Point p=new Point(x+.5f,y+.5f);
                if(g.CanStand(p)&&(p-new Point(post.x,post.y)).Length>13f)
                {away=p;found=true;}
            }
            Check(found,"Map has legal ground beyond the outpost perimeter");
            g.save.x=away.x;g.save.y=away.y;
            g.Tick(.05f,new Point());
            Check(!g.OuterPatrolActive&&g.enemies.Count==0&&
                !g.OuterPatrolDone(0)&&g.save.coins==coins,
                "Leaving the physical defense radius interrupts the run without paying");
            // The v2 save must never record ephemeral siege HP or clock.
            var reload=new GameCore(data,g.save);
            Check(!reload.OuterPatrolActive&&reload.OuterPatrolDone(0)==false&&
                reload.OuterCacheRecovered(0),
                "Pre-existing cache survives both failure paths without schema migration");
        }

        public static void DefendedOuterCachesBecomeRealSafeFieldRefuges(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    x=>x.kind=="outerwatch");
                Check(post!=null,"Each region has an authored supply post");
                GameCore g=new GameCore(data);g.mode="world";g.save.room=room;
                g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
                g.save.x=post.x;g.save.y=post.y;
                g.save.health=Math.Min(g.save.maxHealth-25f,45f);
                g.save.energy=21f;
                Check(!g.OuterPatrolRefuge,
                    "An unprotected outpost must not give free field treatment");
                g.save.flags.Add("outer_patrol_done_"+room);
                Check(g.OuterPatrolRefuge,
                    "Only a legitimately defended reachable outpost becomes a refuge");
                float hp=g.save.health, energy=g.save.energy;
                g.Tick(.05f,new Point());
                Check(g.save.health>hp+.06f&&g.save.energy>energy,
                    "Defended outpost actually replenishes finite combat resources");
                // The status comes from the original v2 flag, not a
                // separate migration or untracked runtime counter.
                GameCore reload=new GameCore(data,g.save);
                reload.save.room=room;reload.enemies.Clear();
                reload.projectiles.Clear();reload.hazards.Clear();
                reload.save.x=post.x;reload.save.y=post.y;
                Check(reload.save.version==2&&reload.OuterPatrolRefuge,
                    "Reloaded legacy save retains a physically usable outpost");
                // A real pursuer forces the field medic to stand down.
                Enemy pursuer=new Enemy(9000,0,post.x+.4f,post.y+.4f,30);
                g.enemies.Add(pursuer);
                Check(!g.OuterPatrolRefuge,
                    "A nearby actual enemy suppresses the otherwise active treatment");
                g.enemies.Clear();
                g.save.flags.Remove("outer_patrol_done_"+room);
                Check(!g.OuterPatrolRefuge,
                    "Removing a victory flag immediately removes its local benefit");
            }
        }

        public static void DefendedOutpostRoutesRespectLiveProgressAndSafeLanding(Campaign data)
        {
            WorldObject[] posts=new WorldObject[8];
            for(int i=0;i<8;i++)
                posts[i]=Array.Find(data.maps[i].objects,o=>o.kind=="outerwatch");
            GameCore g=new GameCore(data);g.mode="world";
            g.save.room=0;g.save.x=posts[0].x;g.save.y=posts[0].y;
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.save.stage=3;
            g.save.flags.Add("outer_patrol_done_0");
            g.save.flags.Add("outer_patrol_done_1");
            int coin=g.save.coins;
            g.Talk(posts[0]);
            int routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_routes");
            Check(routes>=0,"Defended outpost offers a usable travel board");
            g.Choose(routes);
            int west=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_ring_0");
            Check(west>=0,"Travel board has a real regional route selection");
            g.Choose(west);
            int target=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_to_1");
            Check(target>=0,"Another genuinely defended reachable region appears");
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_to_2")<0,
                "Unsecured outposts cannot be selected to skip defense");
            g.Choose(target);
            Check(g.save.room==1&&g.CanStand(g.Position)&&
                g.OuterPatrolDone(0)&&g.OuterPatrolDone(1)&&
                g.save.coins==coin,
                "Existing map Enter and walkable landing preserve rewards and flags");
            GameCore reloaded=new GameCore(data,g.save);
            Check(reloaded.save.version==2&&reloaded.OuterPatrolDone(1),
                "Fast travel introduces no new save schema or unlock reset");

            g.save.x=posts[1].x;g.save.y=posts[1].y;
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.Talk(posts[1]);
            routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_routes");
            g.Choose(routes);
            // The safe outpost menu may remain open while a real raider
            // arrives. Stale travel actions must be reauthenticated.
            Enemy attacker=new Enemy(9111,0,posts[1].x+1f,posts[1].y,40f);
            g.enemies.Add(attacker);
            west=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_ring_0");
            g.Choose(west);
            Check(g.save.room==1&&g.save.coins==coin,
                "An arriving enemy blocks cached route selections");
            g.enemies.Clear();
            g.mode="world";
            // A forged/replayed link to a mission-locked region is not
            // authorized by the old SaveData win flag alone.
            var choose=typeof(GameCore).GetMethod("ChooseOuterPatrol",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(choose!=null,"Live gameplay owns a guarded route handler");
            choose.Invoke(g,new object[]{"outer_watch_to_6"});
            Check(g.save.room==1,
                "Unsecured destination cannot bypass physical campaign travel");
            g.save.flags.Add("outer_patrol_done_6");
            choose.Invoke(g,new object[]{"outer_watch_to_6"});
            Check(g.save.room==1,
                "A won-but-locked region still cannot bypass its campaign entry");
        }

        public static void DefendedDesertAndHighlandPostsShelterAgainstActualWeather(Campaign data)
        {
            for(int room=6;room<=7;room++)
            {
                WorldObject post=Array.Find(data.maps[room].objects,
                    o=>o.kind=="outerwatch");
                Check(post!=null&&post.x>70f,
                    "The field refuge is out on the real exposed regional ring");
                GameCore g=new GameCore(data);g.mode="world";
                g.save.room=room;g.save.x=post.x;g.save.y=post.y;
                g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
                g.save.teaUntil=0f;
                g.save.heat=room==6?65f:0f;
                g.save.cold=room==7?65f:0f;
                float initial=room==6?g.save.heat:g.save.cold;
                g.save.flags.Add("outer_patrol_done_"+room);
                Check(g.OuterPatrolRefuge,
                    "Defended station offers physical local shelter");
                g.Tick(.05f,new Point());
                float cooled=room==6?g.save.heat:g.save.cold;
                Check(cooled<initial,
                    "Real safe outpost suppresses and reduces local heat/cold buildup");
                // The shelter depends on a previously won v2 combat flag.
                // Removing it re-exposes the desert/snow as before.
                g.save.flags.Remove("outer_patrol_done_"+room);
                if(room==6)g.save.heat=initial;
                else g.save.cold=initial;
                g.Tick(.05f,new Point());
                float exposed=room==6?g.save.heat:g.save.cold;
                Check(exposed>initial,
                    "Undeveloped outpost has no free regional weather immunity");
                // Existing combat-threat logic must also suppress refuge.
                g.save.flags.Add("outer_patrol_done_"+room);
                g.enemies.Add(new Enemy(8810,0,post.x+1f,post.y,30f));
                Check(!g.OuterPatrolRefuge,
                    "A true close raider reopens survival pressure even at a rescued post");
            }
        }

        public static void ContinuousBiomeNoiseAvoidsSquareMaterialBoundaries(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                float first=WorldStreaming.BiomeVariation(44,56,room);
                Check(first>=0&&first<=1&&
                    first==WorldStreaming.BiomeVariation(44,56,room),
                    "Biome tint is deterministic for a given saved map coordinate");
                for(int y=48;y<60;y++)
                    for(int x=69;x<89;x++)
                    {
                        float a=WorldStreaming.BiomeVariation(x,y,room);
                        float b=WorldStreaming.BiomeVariation(x+1,y,room);
                        float c=WorldStreaming.BiomeVariation(x,y+1,room);
                        Check(a>=0&&a<=1&&Math.Abs(a-b)<.3f&&Math.Abs(a-c)<.3f,
                            "Adjacent material tints blend gradually rather than popping at eight-tile edges");
                    }
            }
            Check(Math.Abs(WorldStreaming.BiomeVariation(54,52,1)-
                WorldStreaming.BiomeVariation(54,52,7))>.0001f,
                "Forest and frost maps do not reuse identical material clusters");
        }

        // A world-scale map must not place every optional highland
        // challenge in the identical southeast corner of every biome.
        // This runs the REAL SpawnRidgeWildlife method against all seven
        // shipped maps and exercises actual physical navigation.
        // Verify the ACTUAL shipped terrain, world collision and physical
        // routes. These are no longer seven interchangeable wide open maps:
        // each includes real blockage, local environmental silhouettes and
        // a deliberate land route to three physically separated field sites.
        // Old boss/portal/quest objects and dynamic flood seals are retained.
        // These seven paid repairs alter the REAL active GameCore foot
        // collision, not only story prose or the atlas. Original long roads
        // remain available, while source-bound clearance shortens the live
        // physical path between two formerly separated ground cells.
        public static void SevenTerrainClearancesOpenActualWalkableShortcutsWithoutForgedRewards(Campaign data)
        {
            int[,] seams={
                {96,15},{96,8},{23,24},{99,15},
                {72,14},{108,17},{96,15}
            };
            var repair=typeof(GameCore).GetMethod("ChooseRidgeBreach",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(repair!=null,"Native real terrain repair action exists");
            for(int region=1;region<=7;region++)
            {
                GameCore g=new GameCore(data);
                g.mode="world";g.save.room=region;
                MapData m=data.maps[region];
                WorldObject source=Array.Find(m.objects,o=>
                    o.kind=="ridgeBreach"&&o.id=="ridge_breach_"+region);
                int bx=seams[region-1,0],by=seams[region-1,1];
                bool horizontal=region==3;
                Point from=horizontal?
                    new Point(bx-.5f,by+.5f):
                    new Point(bx+.5f,by-.5f);
                Point beyond=horizontal?
                    new Point(bx+1.5f,by+.5f):
                    new Point(bx+.5f,by+1.5f);
                Check(source!=null&&m.rows[by][bx]=='!'&&
                    g.Blocked(bx+.5f,by+.5f)&&
                    g.CanStand(from)&&g.CanStand(beyond),
                    "Each new civilian cut-through starts as a genuine blocked footprint with safely accessible approach sides");
                Check(Math.Abs(source.x-from.x)<.01f&&
                    Math.Abs(source.y-from.y)<.01f,
                    "Only the actual near-side worker owns the original physical repair interaction");
                var oldRoute=g.NavigationPathFrom(from,beyond,true);
                Check(oldRoute.Count>8,
                    "The blocked original passage must genuinely require a long detour, not merely repaint an open tile");
                for(int item=0;item<5;item++)g.save.inventory[item]=25;
                int coins=g.save.coins;
                repair.Invoke(g,new object[]{"ridge_breach_clear"});
                Check(!g.RidgeBreachOpen(region)&&g.save.coins==coins,
                    "A remote forged source-free repair cannot grant new road permissions or coin rewards");
                g.save.x=from.x;g.save.y=from.y;
                g.Talk(source);
                int action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="ridge_breach_clear");
                Check(action>=0&&g.conversation.textEn.Length>95,
                    "Every authentic biome contains its own human story and material-powered physical clearance");
                // Moving away makes even a previously legitimate menu stale.
                g.save.x=from.x+9;g.save.y=from.y;
                g.Choose(action);
                Check(!g.RidgeBreachOpen(region)&&g.save.coins==coins,
                    "Walking away from the source prevents stale dialogue from purchasing a distant road");
                g.save.x=from.x;g.save.y=from.y;
                g.Talk(source);
                action=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="ridge_breach_clear");
                Check(action>=0,"The genuine on-site repair can be reattempted");
                g.Choose(action);
                Check(g.RidgeBreachOpen(region)&&
                    !g.Blocked(bx+.5f,by+.5f)&&
                    g.CanStand(new Point(bx+.5f,by+.5f))&&
                    g.save.coins==coins+8,
                    "Actual payment opens only the region's correct persistent blocked footprint and pays once");
                var restored=g.NavigationPathFrom(from,beyond,true);
                Check(restored.Count>0&&restored.Count<=3&&
                    restored.Count<oldRoute.Count,
                    "The repaired field tile provides an immediate genuinely SHORTER physical walking route");
                g.Talk(source);
                Check(!Array.Exists(g.conversation.choices,
                    c=>c.action=="ridge_breach_clear"),
                    "Completed civilian work never offers a repeatable payout");
                g.Close();
                g.save.room=0;g.save.x=data.maps[0].spawnX;
                g.save.y=data.maps[0].spawnY;
                g.PrepareSave();
                GameCore loaded=new GameCore(data,g.save);
                loaded.mode="world";loaded.save.room=region;
                Check(loaded.save.version==2&&loaded.RidgeBreachOpen(region)&&
                    loaded.CanStand(new Point(bx+.5f,by+.5f)),
                    "Existing SaveData v2 retains real physical passage, not a transient rendering hack");
            }
            GameCore all=new GameCore(data);
            for(int region=1;region<=7;region++)
                all.save.flags.Add("ridge_breach_done_"+region);
            Check(all.RidgeBreachOpenCount==7&&
                all.GetQuest("ridge_breaches").complete&&
                all.GetQuest("ridge_breaches").steps.Count==7,
                "Seven actual open passages complete the separate multi-region journal objective");
        }

        public static void SevenRebuiltBiomesHavePhysicalCoverAndReachableOverlandNetworks(Campaign data)
        {
            int[,] actualSolid={
                {102,14},{93,8},{72,14},{62,7},{72,14},{72,17},{91,15}
            };
            int[,] newWalkways={
                {33,15},{23,15},{33,15},{33,15},{44,13},{44,13},{44,13}
            };
            int[] baselineDecor={250,261,253,247,235,233,235};
            var travelSignatures=new HashSet<string>();
            int totalStages=0,totalNewPhysicalDecor=0;
            for(int region=1;region<=7;region++)
            {
                MapData m=data.maps[region];
                Check(m.rows.Length==80&&m.rows[0].Length==120,
                    "Rebuilding must preserve the original 120x80 map and all existing campaign geography");
                GameCore g=new GameCore(data);
                g.mode="world";g.save.room=region;
                g.save.x=m.spawnX;g.save.y=m.spawnY;
                Check(g.CanStand(new Point(m.spawnX,m.spawnY))&&
                    g.Blocked(actualSolid[region-1,0]+.5f,actualSolid[region-1,1]+.5f)&&
                    !g.CanStand(new Point(actualSolid[region-1,0]+.5f,
                        actualSolid[region-1,1]+.5f)),
                    "Regional terrain structures must be REAL collision cover and not a visual-only decal");
                Check(m.rows[newWalkways[region-1,1]][newWalkways[region-1,0]]=='=',
                    "The new route through each biome is a painted walkable trail, not a fake map line");
                Check(m.decor.Length>=baselineDecor[region-1]+6,
                    "Regional tactical sites contain added original runtime environment props");
                totalNewPhysicalDecor+=m.decor.Length-baselineDecor[region-1];
                // The six historical bridges and each region's seal are
                // real dynamical objects. Stenciling must not overwrite them.
                int bridges=0,seals=0,tides=0;
                foreach(string row in m.rows)
                    foreach(char tile in row)
                    {
                        if(tile=='?')bridges++;
                        if(tile=='v'||tile=='w'||tile=='b')seals++;
                        if(tile=='%')tides++;
                    }
                Check(bridges==6&&
                    (region==4||seals==1)&&
                    (region!=5||tides==125),
                    "Old dynamic bridges, seals and Mistport tide cells must survive map redesign unchanged");
                Point previous=new Point(m.spawnX,m.spawnY);
                string signature="";
                for(int stage=0;stage<3;stage++)
                {
                    string identity="ridge_echo_"+region+"_"+stage;
                    WorldObject site=Array.Find(m.objects,o=>o.id==identity&&o.kind=="ridgeEcho");
                    Check(site!=null&&g.CanStand(new Point(site.x,site.y)),
                        "All original three-stage field relays still have passable real world feet: "+identity);
                    var navigable=g.NavigationPathFrom(previous,
                        new Point(site.x,site.y),true);
                    Check(navigable.Count>0,
                        "The rewritten biomes retain a REAL four-neighbor route to every successive remote story site: "+identity);
                    Check((navigable[navigable.Count-1]-
                        new Point(site.x,site.y)).Length<1.9f,
                        "Physical navigation reaches the original site's actual collision side: "+identity);
                    signature+=site.x+":"+site.y+";";
                    previous=new Point(site.x,site.y);totalStages++;
                }
                Check(travelSignatures.Add(signature),
                    "Seven rebuilt biome routes must have distinct authored mission geometry");
                for(int stage=0;stage<3;stage++)
                {
                    Point foot=g.RidgeWatchDistrict(region,stage);
                    Check(g.CanStand(foot)&&
                        g.NavigationPathFrom(new Point(m.spawnX,m.spawnY),
                            foot,true).Count>0,
                        "Regional new collision formations must not entomb three preexisting real patrols");
                }
            }
            Check(totalStages==21&&totalNewPhysicalDecor>=60,
                "Seven districts each supply all three physical relay objectives and substantial authored scenery");
            Check(data.maps[0].spawnX==18&&data.maps[0].spawnY==15&&
                data.maps[8].id==8&&data.maps[9].id==9,
                "Never silently rewrite the safe town, procedural Abyss or inn while rebuilding outdoor maps");
        }

        public static void SevenRidgeWatchesActuallyOccupyDistinctReachableRegionalDistricts(Campaign data)
        {
            var spawn=typeof(GameCore).GetMethod("SpawnRidgeWildlife",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(spawn!=null,"Real shipped world patrol spawn hook must exist");
            string[] signatures=new string[7];
            for(int room=1;room<=7;room++)
            {
                GameCore game=new GameCore(data);
                game.save.room=room;game.mode="world";
                game.save.x=data.maps[room].spawnX;
                game.save.y=data.maps[room].spawnY;
                game.save.flags.Add("boss_"+room);
                game.save.flags.Add("ridge_seen_"+room+"_0");
                game.save.flags.Add("ridge_seen_"+room+"_1");
                game.enemies.Clear();
                spawn.Invoke(game,null);
                var patrols=new List<Enemy>();
                foreach(Enemy enemy in game.enemies)
                    if(game.IsActiveRidgeWatchPatrol(enemy))
                        patrols.Add(enemy);
                Check(patrols.Count==3,
                    "Each completed regional ridge must instantiate three real live bodies on reachable ground: region "+room);
                for(int i=0;i<patrols.Count;i++)
                {
                    Point foot=patrols[i].Position;
                    Check(game.CanStand(foot)&&
                        (foot-new Point(data.maps[room].spawnX,
                            data.maps[room].spawnY)).Length>40f,
                        "Remote highland actors must not spawn in blocked footing or beside the entrance: region "+room);
                    Check(game.NavigationPathFrom(
                        new Point(data.maps[room].spawnX,
                            data.maps[room].spawnY),foot,true).Count>0,
                        "Original walkable road must reach the authentic remote patrol: region "+room);
                    foreach(WorldObject site in data.maps[room].objects)
                        Check((foot-new Point(site.x,site.y)).Length>=7f,
                            "No live ridge ambush may occupy the interaction reach of an existing story site: region "+room);
                    for(int j=0;j<i;j++)
                        Check((foot-patrols[j].Position).Length>=9.5f,
                            "Different patrols cannot stack their actual living bodies: region "+room);
                }
                Check((patrols[0].Position-patrols[1].Position).Length<=17f&&
                    (patrols[0].Position-patrols[2].Position).Length>=30f&&
                    (patrols[1].Position-patrols[2].Position).Length>=30f,
                    "Every biome needs one physical two-guard squad and one remote combat lookout");
                var whistle=typeof(GameCore).GetMethod("RidgeWatchWhistleReaches",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(whistle!=null&&
                    (bool)whistle.Invoke(game,new object[]{patrols[0],patrols[1]}),
                    "Paired regional guards must truly hear one another across the authored road");
                for(int slot=0;slot<3;slot++)
                {
                    Point mapped=game.RidgeWatchDistrict(room,slot);
                    Check(game.CanStand(mapped)&&
                        (mapped-patrols[slot].Position).Length<=3.01f,
                        "Real world-map ridge pin must identify the same actual original patrol district");
                }
                signatures[room-1]=String.Join(";",
                    patrols.ConvertAll(p=>p.Position.x.ToString("0")+
                        ","+p.Position.y.ToString("0")).ToArray());
                // Completed kills persist on original v2 flag schema.
                game.save.flags.Add("ridge_watch_"+room+"_0");
                Check(game.RidgeWatchProgress(room)==1&&
                    !game.RidgeWatchComplete(room),
                    "Moving a landmark cannot invalidate previously earned old-save patrol progress");
            }
            for(int i=0;i<signatures.Length;i++)
                for(int j=i+1;j<signatures.Length;j++)
                    Check(signatures[i]!=signatures[j],
                        "Two biomes must not reuse the same three patrol places");
        }

        // Twenty-one authored civilian signals: verify real source-bound
        // gameplay, not merely a fresh JSON object counter or free flags.
        public static void SevenBiomeFieldRelaysRequireCorrectEvidenceTimedRealTravelAndPhysicalSupplies(Campaign data)
        {
            var privateChoose=typeof(GameCore).GetMethod("ChooseRidgeEcho",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            var timeField=typeof(GameCore).GetField("echoRunClock",
                System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.NonPublic);
            Check(privateChoose!=null&&timeField!=null,
                "All civilian relays use a real authoritative field-choice and countdown");
            string[] signatures=new string[7];
            for(int room=1;room<=7;room++)
            {
                GameCore g=new GameCore(data);
                g.mode="world";g.save.room=room;g.enemies.Clear();
                g.save.x=data.maps[room].spawnX;
                g.save.y=data.maps[room].spawnY;
                g.save.energy=100;
                for(int item=0;item<5;item++)g.save.inventory[item]=25;
                g.save.flags.Add("biome_field_done_"+room);
                WorldObject[] sites=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    string id="ridge_echo_"+room+"_"+i;
                    sites[i]=Array.Find(data.maps[room].objects,o=>
                        o.id==id&&o.kind=="ridgeEcho");
                    Check(sites[i]!=null&&g.CanStand(new Point(sites[i].x,sites[i].y))&&
                        sites[i].name.Length>8&&sites[i].en.Length>14,
                        "Every biome supplies three unique bilingual playable and standable sites: "+id);
                    var route=g.NavigationPathFrom(
                        new Point(data.maps[room].spawnX,data.maps[room].spawnY),
                        new Point(sites[i].x,sites[i].y),true);
                    Check(route.Count>0,
                        "Original map roads physically connect the entrance to each civilian field station: "+id);
                    foreach(WorldObject existing in data.maps[room].objects)
                        if(existing!=sites[i]&&existing.kind!="ridgeEcho")
                            Check((new Point(existing.x,existing.y)-
                                new Point(sites[i].x,sites[i].y)).Length>=8,
                                "Regional signal cannot cover a legacy NPC or active story interaction");
                    for(int j=0;j<i;j++)
                        Check((new Point(sites[i].x,sites[i].y)-
                            new Point(sites[j].x,sites[j].y)).Length>=25,
                            "Three genuine signal sites must make distinct real trips, not three adjacent menus");
                }
                signatures[room-1]=sites[0].x+","+sites[0].y+";"+
                    sites[1].x+","+sites[1].y+";"+sites[2].x+","+sites[2].y;
                // Direct actions with no physical dialogue source do nothing.
                int coins=g.save.coins,shards=g.save.shards;
                privateChoose.Invoke(g,new object[]{"ridge_echo_finish"});
                Check(!g.RidgeEchoComplete(room)&&g.save.coins==coins&&
                    g.save.shards==shards,"Forged remote finish grants neither progress nor payment");
                g.save.x=sites[2].x;g.save.y=sites[2].y;g.Talk(sites[2]);
                Check(g.conversation!=null&&!Array.Exists(g.conversation.choices,
                    c=>c.action=="ridge_echo_finish"),
                    "Reaching final shelter before real courier dispatch cannot buy the reward");
                g.Close();
                g.save.x=sites[0].x;g.save.y=sites[0].y;g.Talk(sites[0]);
                int wrong=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="ridge_echo_false");
                Check(wrong>=0&&
                    g.conversation.textEn.Length>50,
                    "A biome-specific discoverable clue exists before selecting from two genuine answers");
                float stamina=g.save.energy;g.Choose(wrong);
                Check(!g.RidgeEchoSurveyed(room)&&
                    Math.Abs(g.save.energy-(stamina-6f))<.01f,
                    "Reading the wrong evidence loses stamina, never creates a forged survey flag");
                g.save.x=sites[0].x;g.save.y=sites[0].y;g.Talk(sites[0]);
                int correct=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="ridge_echo_true");
                Check(correct>=0,"Original civilian testimony is an actual selectable decision");
                g.Choose(correct);
                Check(g.RidgeEchoSurveyed(room)&&!g.RidgeEchoComplete(room)&&
                    (g.RidgeEchoObjective(room)-new Point(sites[1].x,sites[1].y)).Length<.01f,
                    "Survey unlocks the NEXT genuinely distant site without gifting a completed route");
                g.save.x=sites[1].x;g.save.y=sites[1].y;g.Talk(sites[1]);
                int start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="ridge_echo_launch");
                Check(start>=0&&g.conversation.textEn.Contains("second"),
                    "Intermediate real field terminal offers an explained timed mission");
                g.Choose(start);
                Check(g.RidgeEchoRunActive&&g.RidgeEchoSeconds>=90&&
                    (g.RidgeEchoObjective(room)-new Point(sites[2].x,sites[2].y)).Length<.01f,
                    "Accepted dispatch starts a genuine transient world-clock run to the third real terminal");
                Check((bool)typeof(GameCore).GetProperty("RidgeEchoRunActive").GetValue(g,null)&&
                    g.GetQuest("ridge_echo").steps.Count==7,
                    "Existing native journal and courier state recognize an active field mission");
                // A live courier cannot cross into Emberfall to bypass the
                // authored overland route. The original room must remain.
                g.Enter(0);
                Check(g.save.room==room&&g.RidgeEchoRunActive,
                    "Cross-region fast travel is blocked while the physical delivery is in progress");
                // Timeout is real world simulation, not an action command;
                // accelerate ONLY the transient timer to exercise expiry.
                timeField.SetValue(g,.02f);
                g.mode="world";g.Tick(.05f,new Point());
                Check(!g.RidgeEchoRunActive&&g.RidgeEchoSurveyed(room)&&
                    !g.RidgeEchoComplete(room)&&
                    (g.RidgeEchoObjective(room)-new Point(sites[1].x,sites[1].y)).Length<.01f,
                    "Missing the route window preserves evidence but revokes the live delivery");
                g.save.energy=100;
                g.save.x=sites[1].x;g.save.y=sites[1].y;g.Talk(sites[1]);
                start=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="ridge_echo_launch");g.Choose(start);
                int beforeCoins=g.save.coins,beforeShards=g.save.shards;
                g.save.x=sites[2].x;g.save.y=sites[2].y;g.Talk(sites[2]);
                int finish=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="ridge_echo_finish");
                Check(finish>=0,"Authentic timed delivery can be handed over at real final shelter");
                g.Choose(finish);
                Check(g.RidgeEchoComplete(room)&&!g.RidgeEchoRunActive&&
                    g.save.coins==beforeCoins+30+room*3&&
                    g.save.shards==beforeShards+1,
                    "A paid real overland courier completion unlocks its durable one-time site and reward");
                g.save.x=sites[2].x;g.save.y=sites[2].y;g.Talk(sites[2]);
                Check(!Array.Exists(g.conversation.choices,
                    c=>c.action=="ridge_echo_finish"),
                    "Visited completed terminals never duplicate their last payment");
                g.Close();g.enemies.Clear();
                g.save.health=30f;g.save.energy=30f;
                g.Tick(.05f,new Point());
                Check(g.save.health>30.05f&&g.save.energy>30.11f,
                    "Remote civilian refuge restores actual simulation health and stamina at the physical campsite");
                // SaveData v2 unchanged: the durable flags survive load
                // even though the transient courier clock never does.
                g.save.room=0;g.save.x=data.maps[0].spawnX;g.save.y=data.maps[0].spawnY;
                g.PrepareSave();
                GameCore loaded=new GameCore(data,g.save);
                Check(loaded.save.version==2&&loaded.RidgeEchoComplete(room)&&
                    !loaded.RidgeEchoRunActive,
                    "Old SaveData version preserves completed civilian refuge with no transient clock");
            }
            for(int i=0;i<signatures.Length;i++)
                for(int j=i+1;j<signatures.Length;j++)
                    Check(signatures[i]!=signatures[j],
                        "All seven biomes must have separately authored three-leg expeditions");
            GameCore all=new GameCore(data);all.mode="world";
            for(int room=1;room<=7;room++)
                all.save.flags.Add("ridge_echo_"+room+"_done");
            Check(all.RidgeEchoCompleteCount==7&&
                all.GetQuest("ridge_echo").complete&&
                all.GetQuest("ridge_echo").steps.Count==7,
                "All seven persistent regions drive a genuine finished selectable journal quest");
        }

        public static void EightRidgeRoutesNeedBothPhysicalLandmarksAndSafeTravel(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                MapData map=data.maps[room];
                WorldObject east=Array.Find(map.objects,o=>o.id=="ridge_route_"+room+"_0");
                WorldObject south=Array.Find(map.objects,o=>o.id=="ridge_route_"+room+"_1");
                Check(east!=null&&south!=null&&g.CanStand(new Point(east.x,east.y))&&
                    g.CanStand(new Point(south.x,south.y)),
                    "Fifth ring links use two real standable authored locations");
                int coins=g.save.coins,shards=g.save.shards,xp=g.save.xp;
                Point outsideRidge=StandableOutsideTalkReach(g,east);
                g.save.x=outsideRidge.x;g.save.y=outsideRidge.y;
                g.Talk(east);g.Close();
                Check(!g.RidgeSeen(room,0)&&g.save.coins==coins&&
                    g.save.shards==shards&&g.save.xp==xp,
                    "A nearby but out-of-reach ridge marker cannot grant survey XP or shortcut credit");
                g.save.x=east.x;g.save.y=east.y;
                g.Talk(east);
                Check(g.RidgeSeen(room,0)&&!g.RidgeLinked(room),
                    "Visiting only one marker never unlocks a free crossing");
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel")<0,
                    "An incomplete survey offers no teleport");
                g.Close();
                g.save.x=south.x;g.save.y=south.y;g.Talk(south);
                Check(g.RidgeLinked(room)&&g.save.coins==coins+20&&
                    g.save.shards==shards+1,"Second distinct physical visit enables permanent safe shortcut");
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel");
                Check(choice>=0,"Completed survey offers actionable crossing choice");
                g.save.energy=36;g.Choose(choice);
                Check(Math.Abs(g.save.x-east.x)<.01f&&Math.Abs(g.save.y-east.y)<.01f&&
                    Math.Abs(g.save.energy-24)<.01f,"Crossing moves the actual player and costs real energy");
                // The old implementation chose any nearby ridge marker and
                // would travel even without the original marker's dialogue.
                // Its source-free code path must not spend stamina or move.
                var ridgeAction=typeof(GameCore).GetMethod("ChooseRidge",
                    System.Reflection.BindingFlags.Instance|
                    System.Reflection.BindingFlags.NonPublic);
                Check(ridgeAction!=null,"Live ridge choice handler exists");
                ridgeAction.Invoke(g,new object[]{"ridge_travel"});
                Check(Math.Abs(g.save.x-east.x)<.01f&&Math.Abs(g.save.y-east.y)<.01f&&
                    Math.Abs(g.save.energy-24)<.01f,
                    "A forged source-free ridge action cannot teleport the hero or spend energy");
                g.Talk(east);choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel");
                g.save.energy=3;g.Choose(choice);
                Check(Math.Abs(g.save.x-east.x)<.01f,
                    "Insufficient energy cannot cross the ridge");
                g.Talk(east);choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_travel");
                g.save.energy=24;g.Choose(choice);
                Check(Math.Abs(g.save.x-south.x)<.01f&&Math.Abs(g.save.y-south.y)<.01f&&
                    Math.Abs(g.save.energy-12)<.01f,
                    "A genuine east-marker dialogue travels toward the opposite surveyed marker");
                g.Talk(south);g.Close();
                Check(g.save.coins==coins+20,
                    "Revisiting completed markers never duplicates quest rewards");
                GameCore saved=new GameCore(data,g.save);
                Check(saved.save.version==2&&saved.RidgeLinked(room)&&
                    saved.save.coins==coins+20,
                    "New ridge progress restores from old SaveData v2");
            }
            GameCore all=new GameCore(data);
            for(int i=0;i<8;i++)
            {all.save.flags.Add("ridge_seen_"+i+"_0");all.save.flags.Add("ridge_seen_"+i+"_1");}
            Check(all.RidgeLinkedCount==8&&all.GetQuest("ridge_routes").complete,
                "All eight distinct ridge paths finish the tracked exploration quest");
        }

        public static void FifthRingWildlifeSpawnsAsActualCombatAwayFromQuestMarkers(Campaign data)
        {
            var spawn=typeof(GameCore).GetMethod("SpawnRoom",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Check(spawn!=null,"Use the real room-spawn transaction");
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);
                g.save.room=room;
                spawn.Invoke(g,null);
                int unlinkedFar=0;
                foreach(Enemy e in g.enemies)if(e.x>=74&&e.y>=60)unlinkedFar++;
                Check(unlinkedFar==0,
                    "Original quest rooms retain their pre-expansion enemy requirements");
                if(room>0)
                {
                    g.save.flags.Add("ridge_seen_"+room+"_0");
                    g.save.flags.Add("ridge_seen_"+room+"_1");
                    spawn.Invoke(g,null);
                    int early=0;
                    foreach(Enemy e in g.enemies)if(e.x>=74&&e.y>=60)early++;
                    Check(early==0,
                        "Early ridge discovery must never expand uncleared guardian encounters");
                    g.save.flags.Add("boss_"+room);
                    spawn.Invoke(g,null);
                }
                int far=0;
                foreach(Enemy e in g.enemies)
                    if(g.IsActiveRidgeWatchPatrol(e))
                    {
                        far++;
                        Check(g.CanStand(e.Position)&&
                            (e.Position-new Point(data.maps[room].spawnX,
                                data.maps[room].spawnY)).Length>40,
                            "Real fifth-ring wildlife occupies passable distant original terrain");
                        foreach(WorldObject o in data.maps[room].objects)
                            Check((e.Position-new Point(o.x,o.y)).Length>=7,
                                "Real wildlife stays beyond authored dialogue reach");
                        Check(e.hp>0&&e.maxHp>0,
                            "Actual regional hostile has real positive combat health");
                    }
                if(room==0)Check(far==0,"Original town remains peaceful");
                else Check(far==3,"All seven regions gain three true remote enemies");
            }
        }

        public static void TerrainVeilNeverCreatesOpaqueLoadingSquares(Campaign data)
        {
            Check(WorldStreaming.TerrainVeilOpacity(0)==.3f,
                "Newly entered world chunks show their actual terrain behind a light atmospheric veil");
            Check(WorldStreaming.TerrainVeilOpacity(1)==0,
                "Fully prewarmed terrain draws without an extra mask");
            float previous=1;
            for(int i=0;i<=100;i++)
            {
                float alpha=WorldStreaming.TerrainVeilOpacity(i/100f);
                Check(alpha>=0&&alpha<=.3f&&alpha<=previous,
                    "Chunk transitions never render opaque blank tiles or increase masking over time");
                previous=alpha;
            }
            for(int region=0;region<8;region++)
                Check(WorldStreaming.BiomeVariation(98,65,region)>=0&&
                    WorldStreaming.BiomeVariation(98,65,region)<=1,
                    "Static biome cache receives legal deterministic material values");
        }

        public static void SixthRingWeatherDialUnlocksRealSheltersAndPreservesSaveV2(Campaign data)
        {
            int[] answers={1,2,0,2,1,0,1,2};
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                // This test deliberately visits every authored region. Preserve
                // the real v2 unlock prerequisites so reloading the shelter save
                // keeps the player in that region instead of safe-respawning
                // them to Emberfall and then trying to talk to a stale object.
                g.save.stage = room <= 4 ? Math.Max(0, room - 1) : 1;
                if (room == 5) g.save.flags.Add("exp_intro");
                if (room == 6) g.save.flags.Add("harbor_report");
                if (room == 7) g.save.flags.Add("desert_report");
                MapData m=data.maps[room];
                WorldObject[] sites=new WorldObject[3];
                for(int i=0;i<3;i++)
                {
                    int stage=i;
                    sites[i]=Array.Find(m.objects,o=>o.id=="farsignal_"+room+"_"+stage);
                    Check(sites[i]!=null&&g.CanStand(new Point(sites[i].x,sites[i].y)),
                        "All new sixth-ring weather sites occupy actual passable map tiles");
                }
                g.save.inventory[0]=5;g.save.inventory[4]=5;
                int coins=g.save.coins;
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);g.Close();
                Check(!g.FarShelterBuilt(room),"Untuned refuge cannot be rebuilt");
                g.save.x=sites[1].x;g.save.y=sites[1].y;g.Talk(sites[1]);g.Close();
                Check(!g.FarDialReady(room),"An unread log forbids calibration");
                g.save.x=sites[0].x;g.save.y=sites[0].y;
                g.Talk(sites[0]);g.Close();
                Check(g.FarLogFound(room),"Real east field log unlocks clue");
                g.save.x=sites[1].x;g.save.y=sites[1].y;
                g.Talk(sites[1]);
                int wrong=(answers[room]+1)%3;
                int idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_dial_"+wrong);
                Check(idx>=0,"Real three-choice puzzle displays incorrect options");
                g.Choose(idx);
                Check(!g.FarDialReady(room),"Incorrect observation cannot unlock shared refuge");
                g.Talk(sites[1]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_dial_"+answers[room]);
                Check(idx>=0,"Real field log's answer exists among the choices");
                g.Choose(idx);
                Check(g.FarDialReady(room),"Correct local glyph genuinely calibrates the dial");
                g.save.x=sites[2].x;g.save.y=sites[2].y;
                g.Talk(sites[2]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_build");
                Check(idx>=0,"Calibrated shelter offers actual build action");
                g.Choose(idx);
                Check(g.FarShelterBuilt(room)&&g.save.inventory[0]==4&&
                    g.save.inventory[4]==4&&g.save.coins==coins+22,
                    "Building refuge consumes real materials and pays once");
                g.save.health=Math.Max(1,g.save.maxHealth-50);g.save.energy=4;g.save.cold=40;
                g.Talk(sites[2]);
                idx=Array.FindIndex(g.conversation.choices,c=>c.action=="far_rest");
                Check(idx>=0,"Completed real shelter offers reusable warmth");
                g.Choose(idx);
                Check(g.save.inventory[0]==3&&g.save.energy==100&&
                    g.save.cold==15&&g.save.health>g.save.maxHealth-50,
                    "Timber-fed warm shelter restores real player stats");
                GameCore loaded=new GameCore(data,g.save);
                Check(loaded.save.room==room,
                    "Authorized shelter save reloads into the same reachable region");
                Check(loaded.save.version==2&&loaded.FarShelterBuilt(room)&&
                    loaded.save.coins==coins+22,
                    "Old save v2 persists the actual field puzzle and construction");
                loaded.save.x=sites[2].x;loaded.save.y=sites[2].y;
                loaded.Talk(sites[2]);
                Check(Array.FindIndex(loaded.conversation.choices,c=>c.action=="far_build")<0,
                    "Reading a restored shelter cannot farm the one-time build reward");
            }
            GameCore full=new GameCore(data);
            for(int room=0;room<8;room++)full.save.flags.Add("far_shelter_"+room);
            Check(full.FarShelterCount==8&&!full.GetQuest("far_refuges").complete,
                "Eight restored shelters still need real public filing at the Inn");
            full.save.room=9;
            MapData inn=data.maps[9];
            Check(inn.rows.Length==40&&inn.rows[0].Length==60,
                "Expanded Inn adds a real southern public records wing");
            var reach=new System.Collections.Generic.HashSet<int>();
            var queue=new System.Collections.Generic.Queue<int>();
            int w=inn.rows[0].Length,h=inn.rows.Length;
            int first=(int)inn.spawnY*w+(int)inn.spawnX;
            queue.Enqueue(first);reach.Add(first);
            int[,] dir={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int cell=queue.Dequeue(),x=cell%w,y=cell/w;
                for(int k=0;k<4;k++)
                {
                    int xx=x+dir[k,0],yy=y+dir[k,1],key=yy*w+xx;
                    if(xx<0||yy<0||xx>=w||yy>=h||reach.Contains(key)||
                        !full.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    reach.Add(key);queue.Enqueue(key);
                }
            }
            WorldObject atlas=Array.Find(inn.objects,o=>o.kind=="faratlasdesk");
            WorldObject guest=Array.Find(inn.objects,o=>o.kind=="farrefugerecord");
            Check(atlas!=null&&guest!=null&&
                reach.Contains((int)atlas.y*w+(int)atlas.x)&&
                reach.Contains((int)guest.y*w+(int)guest.x),
                "Both Inn annex chambers are physically connected through real collision");
            full.save.x=atlas.x;full.save.y=atlas.y;
            int startCoins=full.save.coins;
            full.Talk(atlas);
            Check(full.conversation.text.IndexOf('\n')>=0&&
                full.conversation.textEn.IndexOf('\n')>=0,
                "Public inn atlas dialogue must format paragraphs as genuine newlines");
            int publish=Array.FindIndex(full.conversation.choices,c=>c.action=="far_publish");
            Check(publish>=0,"An actual Inn desk offers civic publication");
            full.Choose(publish);
            Check(full.Has("far_refuge_atlas")&&full.GetQuest("far_refuges").complete&&
                full.save.coins==startCoins+80,
                "In-person publication finishes quest with authentic once-only reward");
            GameCore reloaded=new GameCore(data,full.save);
            Check(reloaded.save.version==2&&reloaded.Has("far_refuge_atlas"),
                "Public civic atlas persists in legacy save slots");
            full.Talk(atlas);publish=Array.FindIndex(full.conversation.choices,
                c=>c.action=="far_publish");
            full.Choose(publish);
            Check(full.save.coins==startCoins+80,"Publishing twice cannot farm another grant");
        }

        public static void CameraLeadPrewarmsMovementWithoutUnboundedMapScans(Campaign data)
        {
            Check(WorldStreaming.PredictPrewarmLead(0)==0,
                "Still camera requests no unneeded directional prewarm");
            Check(WorldStreaming.PredictPrewarmLead(3)==4,
                "Following camera prepares forward chunks before arrival");
            Check(WorldStreaming.PredictPrewarmLead(-4)==-6,
                "Backward movement prepares the trailing viewport side");
            Check(WorldStreaming.PredictPrewarmLead(1000)==8&&
                WorldStreaming.PredictPrewarmLead(-1000)==-8,
                "Teleport or huge map never launches unbounded preloading");
            Check(WorldStreaming.PredictPrewarmLead(float.NaN)==0&&
                WorldStreaming.PredictPrewarmLead(float.PositiveInfinity)==0,
                "Bad camera input cannot corrupt streamed terrain bounds");
        }

        public static void SixthRingHiddenSuppliesAndLookoutsActuallyRevealMapSectors(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                var hidden=Array.Find(data.maps[room].objects,o=>o.kind=="farhidden");
                var vista=Array.Find(data.maps[room].objects,o=>o.kind=="farlookout");
                Check(hidden!=null&&vista!=null&&
                    g.CanStand(new Point(hidden.x,hidden.y))&&
                    g.CanStand(new Point(vista.x,vista.y)),
                    "Every sixth-ring secret must be physically accessible");
                Check(!g.Explored(room,83,75)&&!g.Explored(room,115,58),
                    "Remote viewpoints and hidden cache begin unexplored");
                Point outsideVista=StandableOutsideTalkReach(g,vista);
                int beforeVistaXp=g.save.xp;
                g.save.x=outsideVista.x;g.save.y=outsideVista.y;g.Talk(vista);g.Close();
                Check(!g.Has("far_vista_"+room)&&!g.Explored(room,83,75)&&
                    g.save.xp==beforeVistaXp,
                    "A lookout cannot award XP or reveal map sectors from outside physical reach");
                g.save.x=vista.x;g.save.y=vista.y;g.Talk(vista);g.Close();
                Check(g.Explored(room,83,75)&&g.Explored(room,80,70)&&
                    !g.Explored(room,115,58),
                    "Real lookout reveals a bounded surrounding region without discovering distant secrets");
                int coins=g.save.coins,resources=g.save.inventory[room%3];
                Point outsideHidden=StandableOutsideTalkReach(g,hidden);
                g.save.x=outsideHidden.x;g.save.y=outsideHidden.y;g.Talk(hidden);g.Close();
                Check(!g.Has("far_secret_"+room)&&g.save.coins==coins&&
                    g.save.inventory[room%3]==resources,
                    "An out-of-reach hidden-cache call cannot claim supplies or flag the secret");
                g.save.x=hidden.x;g.save.y=hidden.y;g.Talk(hidden);g.Close();
                Check(g.save.coins==coins+9&&g.save.inventory[room%3]==resources+2,
                    "Physically reaching the concealed supplies pays its actual one-time materials");
                g.Talk(hidden);g.Close();
                Check(g.save.coins==coins+9,"Hidden cache cannot duplicate rewards");
                GameCore loaded=new GameCore(data,g.save);
                Check(loaded.save.version==2&&loaded.Has("far_secret_"+room)&&
                    loaded.Has("far_vista_"+room)&&loaded.Explored(room,83,75),
                    "Hidden sixth-ring exploration survives legacy v2 save reload");
            }
        }

        public static void MapPreviewScalesToRealDynamicFloorAndRegionDimensions(Campaign data)
        {
            Check(Math.Abs(WorldStreaming.PreviewScale(120,80,306,207)-2.55f)<.001f,
                "Large sixth-ring map fits its world-map preview panel");
            Check(Math.Abs(WorldStreaming.PreviewScale(66,44,306,207)-
                (306f/66f))<.001f,
                "Runtime 66x44 abyss floor uses its real dimensions instead of old stub");
            Check(WorldStreaming.PreviewScale(0,44,306,207)==0,
                "Corrupt map dimensions cannot force an infinite texture allocation");
        }

        private static int BridgeDistance(GameCore g,int sx,int sy,int gx,int gy)
        {
            int w=g.Map.rows[0].Length,h=g.Map.rows.Length;
            var distances=new Dictionary<int,int>();var queue=new Queue<int>();
            int start=sy*w+sx;distances[start]=0;queue.Enqueue(start);
            int[,] directions={{1,0},{-1,0},{0,1},{0,-1}};
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),x=current%w,y=current/w;
                if(x==gx&&y==gy)return distances[current];
                for(int k=0;k<4;k++)
                {
                    int xx=x+directions[k,0],yy=y+directions[k,1],at=yy*w+xx;
                    if(xx<2||yy<2||xx>=w-2||yy>=h-2||distances.ContainsKey(at)||
                        !g.CanStand(new Point(xx+.5f,yy+.5f)))continue;
                    distances[at]=distances[current]+1;queue.Enqueue(at);
                }
            }
            return -1;
        }
        public static void RepairedBridgesOpenRealTerrainAndShortenWalkingDistance(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                var g=new GameCore(data);g.save.room=room;
                var bridge=Array.Find(g.Map.objects,o=>o.id=="far_bridge_"+room);
                Check(bridge!=null&&g.CanStand(new Point(bridge.x,bridge.y)),
                    "All bridge repair posts are physically accessible");
                Check(g.Blocked(111.5f,52.5f),"Broken spans block actual collision");
                int longRoute=BridgeDistance(g,111,49,112,73);
                Check(longRoute>0,"The old long route is not destructively removed");
                g.save.inventory[0]=5;g.save.inventory[4]=5;g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair");
                Check(choice>=0,"Bridge has a legitimate material-consuming interaction");
                g.Choose(choice);
                Check(g.FarBridgeOpen(room)&&!g.Blocked(111.5f,52.5f)&&
                    g.save.inventory[0]==3&&g.save.inventory[4]==4,
                    "One repair consumes the actual resources and unlocks actual terrain");
                int shortRoute=BridgeDistance(g,111,49,112,73);
                Check(shortRoute>0&&shortRoute<longRoute/2,
                    "Repair creates a physically shorter route, not a fake quest reward");
                g.Talk(bridge);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair")<0,
                    "Bridge repair cannot duplicate materials or rewards");
                var reload=new GameCore(data,g.save);
                Check(reload.save.version==2&&reload.FarBridgeOpen(room),
                    "Old v2 save reload preserves the constructed bridge flag even if story gates redirect the player");
                // In an unprogressed test save the normal story gates may
                // return to the safe town; only test collision after entering
                // the matching author's region again.
                reload.save.room=room;
                Check(!reload.Blocked(111.5f,52.5f),
                    "Returning to the reconstructed region restores real walkable collision");
            }
        }

        public static void AllEightRepairedBridgesAppearInQuestTracking(Campaign data)
        {
            var g=new GameCore(data);
            Check(Array.IndexOf(GameCore.QuestIds,"far_bridges")>=0,
                "Real bridge construction has a separate selectable journal quest");
            Check(!g.GetQuest("far_bridges").complete,
                "A new save has never repaired any bridge");
            for(int room=0;room<8;room++)g.save.flags.Add("far_bridge_"+room);
            Check(g.FarBridgeCount==8&&g.GetQuest("far_bridges").complete,
                "All eight repaired routes finish the tracked journal");
        }

        public static void InstantTravelPreparesVisibleWorldWithoutColdChunkPopin(Campaign data)
        {
            Check(!WorldStreaming.LargeCameraJump(1,2),
                "Ordinary walking still uses softened chunk transitions");
            Check(WorldStreaming.LargeCameraJump(20,0)&&
                WorldStreaming.LargeCameraJump(-12,-6),
                "Cross-map and ridge jumps immediately prime visible props");
            Check(!WorldStreaming.LargeCameraJump(float.NaN,0)&&
                !WorldStreaming.LargeCameraJump(float.PositiveInfinity,2),
                "Bad camera state never triggers unbounded terrain work");
        }

        public static void BridgeParcelNeedsRealTravelAndOnlyRewardsDelivery(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                GameCore g=new GameCore(data);g.save.room=room;
                WorldObject bridge=Array.Find(g.Map.objects,o=>o.kind=="farbridge");
                WorldObject shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
                Check(bridge!=null&&shelter!=null,
                    "Courier actually uses existing physical bridge and refuge");
                if(room==0)
                {
                    g.enemies.Clear();g.save.inventory[0]=2;g.save.inventory[4]=1;
                    g.save.x=bridge.x;g.save.y=bridge.y;g.Talk(bridge);
                    int repair=Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair");
                    Check(repair>=0,"An unrepaired physical bridge offers its authored repair");
                    Point outsideBridge=StandableOutsideTalkReach(g,bridge);
                    g.save.x=outsideBridge.x;g.save.y=outsideBridge.y;g.Choose(repair);g.Close();
                    Check(!g.FarBridgeOpen(room)&&g.save.inventory[0]==2&&g.save.inventory[4]==1,
                        "A stale bridge dialog cannot spend materials or repair after leaving interaction reach");
                    g.save.x=bridge.x;g.save.y=bridge.y;g.Talk(bridge);
                    repair=Array.FindIndex(g.conversation.choices,c=>c.action=="far_bridge_repair");
                    g.Choose(repair);g.Close();
                    Check(g.FarBridgeOpen(room)&&g.save.inventory[0]==0&&g.save.inventory[4]==0,
                        "Repairing at the bridge consumes real materials and opens the physical crossing");
                }
                g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start")<0,
                    "Unbuilt bridge or shelter cannot create supply rewards");
                g.Close();g.save.flags.Add("far_bridge_"+room);
                g.save.flags.Add("far_shelter_"+room);
                int coins=g.save.coins,shards=g.save.shards;
                float unloadedSpeed=g.Speed;
                g.Talk(bridge);
                int choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start");
                Check(choose>=0,"Repaired bridge offers a physical parcel trip");
                g.Choose(choose);
                Check(g.FarCourierActive&&g.Speed<unloadedSpeed,
                    "A real carried parcel visibly affects character speed");
                g.save.x=shelter.x;g.save.y=shelter.y;
                g.Talk(shelter);g.Close();
                Check(!g.FarCourierDelivered(room)&&g.save.coins==coins,
                    "Teleporting to the shelter cannot fake the foot journey");
                g.Tick(.02f,new Point());
                Check(!g.FarCourierActive&&!g.FarCourierDelivered(room),
                    "Long position jump invalidates transient cargo");
                g.save.x=bridge.x;g.save.y=bridge.y;
                g.Talk(bridge);
                choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_start");
                g.Choose(choose);
                for(int step=0;step<100&&g.save.y<61.35f;step++)
                    g.Tick(.045f,new Point(0,1));
                Check(g.FarCourierActive&&g.FarCourierCrossed,
                    "Engine ticks record actual southbound contact with rebuilt bridge tiles");
                WorldObject post=Array.Find(g.Map.objects,o=>o.kind=="farcheck");
                Check(post!=null&&g.CanStand(new Point(post.x,post.y))&&
                    (g.Position-new Point(post.x,post.y)).Length<2.2f,
                    "Intermediate cargo inspection is a reachable real-world station");
                g.Talk(post);
                int seal=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="far_courier_stamp");
                Check(seal>=0,"Checkpoint offers a genuine physical seal action");
                g.Choose(seal);
                Check(g.FarCourierStamped,"Civilian inspector actually stamps the carried cargo");
                for(int step=0;step<110&&g.save.y<73.2f;step++)
                    g.Tick(.045f,new Point(0,1));
                Check((g.Position-new Point(shelter.x,shelter.y)).Length<2.2f,
                    "Normal real movement reaches the distant shelter before timeout");
                g.Talk(shelter);
                choose=Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_deliver");
                Check(choose>=0,"Actual refuge dialogue offers parcel delivery");
                bool medicineIntact=g.FarCourierIntegrity>=90;
                g.Choose(choose);
                if(medicineIntact)
                    Check(g.toast.Contains(g.save.english?"coins":"硬币")&&
                        g.toast.Contains(g.save.english?"Medicine intact":"药品完好"),
                        "A real intact handoff must present the original money reward and local-care bonus in one toast: room="+room+
                        " toast="+g.toast.Replace("\n"," / ")+
                        " integrity="+g.FarCourierIntegrity+
                        " delivered="+g.FarCourierDelivered(room)+
                        " pristine="+g.FarCourierPristine(room));
                Check(g.FarCourierPristine(room)==medicineIntact,
                    "Only genuinely delivered cargo with at least 90 percent integrity preserves local medical stock");
                Check(g.FarCourierDelivered(room)&&!g.FarCourierActive&&
                    g.save.coins==coins+25&&g.save.shards==shards+1,
                    "Only legitimate foot delivery grants one real permanent reward");
                GameCore reload=new GameCore(data,g.save);
                Check(reload.save.version==2&&reload.FarCourierDelivered(room)&&
                    reload.FarCourierPristine(room)==medicineIntact&&
                    !reload.FarCourierActive,
                    "SaveData v2 restores only earned completed delivery and medicine, never held cargo");
                g.Talk(shelter);g.Close();
                Check(g.save.coins==coins+25,"Parcel delivery rewards cannot repeat");
            }
            GameCore all=new GameCore(data);
            for(int i=0;i<8;i++)all.save.flags.Add("far_courier_"+i);
            Check(!all.GetQuest("far_couriers").complete&&all.FarCourierDeliveredCount==8,
                "Eight delivered parcels still require formal publication at the existing Inn");
        }


        public static void EightCourierReceiptsAppearOnlyAfterActualRegionalDeliveryFlags(Campaign data)
        {
            string[] voicesZh={"小林的烧","树根扎得深","潮水带走","别先记工分",
                "城墙里还有空床","航灯坏了三个","发热的孩子","冻伤的人"};
            string[] voicesEn={"Lin's fever","roots cut deep","tide took","shift ledger",
                "bed inside the wall","harbor lamps","feverish girl","frostbitten fingers"};
            for(int room=0;room<8;room++)
            {
                GameCore g=Fresh(data);
                // Reopening a genuine old SaveData v2 requires its original
                // chapter and report gates, not just an injected room index.
                // Never weaken GameCore.Load's valid-world authorization.
                g.save.stage=3;
                g.save.flags.Add("exp_intro");
                g.save.flags.Add("harbor_report");
                g.save.flags.Add("desert_report");
                g.save.room=room;g.enemies.Clear();
                g.save.flags.Add("far_dial_"+room);
                g.save.flags.Add("far_shelter_"+room);
                WorldObject refuge=Object(g,"farshelter");
                g.save.x=refuge.x;g.save.y=refuge.y;
                g.Talk(refuge);
                Check(g.conversation!=null&&
                    !g.conversation.text.Contains(voicesZh[room])&&
                    !g.conversation.textEn.Contains(voicesEn[room]),
                    "Undelivered medicines cannot invent a regional patient's testimony");
                g.Close();
                // These are the exact flags the genuine physical handoff
                // commits; the existing 8-region walk test proves earning them.
                g.save.flags.Add("far_courier_"+room);
                g.Talk(refuge);
                Check(g.conversation.text.Contains(voicesZh[room])&&
                    g.conversation.textEn.Contains(voicesEn[room]),
                    "A delivered regional parcel adds the receiving civilian's authored, bilingual response");
                g.Close();
                g.save.flags.Add("far_courier_pristine_"+room);
                g.Talk(refuge);
                Check(g.conversation.text.Contains(voicesZh[room])&&
                    g.conversation.text.Contains("额外恢复生命 +10")&&
                    g.conversation.textEn.Contains("10 extra HP"),
                    "The local human handoff and intact-medicine rest reward remain visible together");
                GameCore reload=new GameCore(data,g.save);
                reload.mode="world";reload.save.x=refuge.x;reload.save.y=refuge.y;
                reload.Talk(refuge);
                Check(reload.save.version==2&&reload.conversation!=null&&
                    reload.conversation.text.Contains(voicesZh[room]),
                    "Original v2 saves recover exactly their completed regional receipt after reload");
            }
        }

        public static void IntactMedicineImprovesPaidRestOnlyAtReceivingRefuge(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.room=0;
            g.save.flags.Add("far_dial_0");
            g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_courier_pristine_0");
            g.save.inventory[0]=3;
            g.enemies.Clear();
            WorldObject first=Object(g,"farshelter");
            g.save.x=first.x;g.save.y=first.y;
            g.save.health=15;
            g.Talk(first);
            Check(g.conversation!=null&&g.conversation.text.Contains("额外恢复生命 +10"),
                "Refuge dialogue announces the real, locally earned medicine bonus");
            int rest=Array.FindIndex(g.conversation.choices,x=>x.action=="far_rest");
            Check(rest>=0,"Actual restored refuge exposes its existing paid rest");
            g.Choose(rest);
            Check(Math.Abs(g.save.health-60)<.01f&&g.save.inventory[0]==2,
                "Intact supply adds exactly 10 HP to the actual 35 HP rest at its receiving refuge");
            g.Close();
            g.save.room=1;g.save.flags.Add("far_dial_1");g.save.flags.Add("far_shelter_1");
            WorldObject second=Object(g,"farshelter");
            g.save.x=second.x;g.save.y=second.y;g.save.health=15;
            g.Talk(second);
            Check(!g.conversation.text.Contains("额外恢复生命 +10"),
                "One region's intact medicine cannot leak across the eight local refuges");
            rest=Array.FindIndex(g.conversation.choices,x=>x.action=="far_rest");
            g.Choose(rest);
            Check(Math.Abs(g.save.health-50)<.01f&&g.save.inventory[0]==1,
                "Unserved or old-v2 refuge retains its full original 35 HP timber cost");
            GameCore reload=new GameCore(data,g.save);
            Check(reload.save.version==2&&reload.FarCourierPristine(0)&&
                  !reload.FarCourierPristine(1),
                "Pristine-local medicine survives the original v2 save format only where earned");
        }

        public static void PublicParcelCharterRequiresEveryGenuineDeliveryAndImprovesShelters(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.flags.Add("far_refuge_atlas");
            g.save.room=9;
            WorldObject desk=Array.Find(g.Map.objects,o=>o.kind=="faratlasdesk");
            Check(desk!=null,"Original inn atlas remains an actual interactable desk");
            g.save.x=desk.x;g.save.y=desk.y;
            g.Talk(desk);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_charter")<0,
                "Cannot publish civic supply charter without physical deliveries");
            g.Close();
            for(int room=0;room<8;room++)g.save.flags.Add("far_courier_"+room);
            int coins=g.save.coins;
            g.Talk(desk);
            int option=Array.FindIndex(g.conversation.choices,
                c=>c.action=="far_courier_charter");
            Check(option>=0,"Completing all deliveries unlocks public filing at the actual inn");
            g.Choose(option);
            Check(g.Has("far_courier_charter")&&g.save.coins==coins+65&&
                g.GetQuest("far_couriers").complete,
                "Real public filing grants a single permanent network upgrade");
            GameCore reload=new GameCore(data,g.save);
            Check(reload.save.version==2&&reload.Has("far_courier_charter"),
                "Completed charter remains in old SaveData v2 after reload");
            g.Talk(desk);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="far_courier_charter")<0&&
                g.save.coins==coins+65,
                "Publishing civic accounts twice never duplicates rewards");
            g.Close();
            g.save.room=0;g.save.flags.Add("far_shelter_0");
            g.save.flags.Add("far_dial_0"); // a shelter cannot be built without its earlier real calibration
            WorldObject shelter=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
            g.save.x=shelter.x;g.save.y=shelter.y;
            g.save.inventory[0]=3;g.save.health=Math.Max(1,g.save.maxHealth-75);
            g.save.energy=8;g.save.cold=60;
            g.Talk(shelter);
            option=Array.FindIndex(g.conversation.choices,c=>c.action=="far_rest");
            Check(option>=0,"Existing public refuge still offers a repeatable physical rest");
            g.Choose(option);
            Check(g.save.energy==100&&g.save.cold==20&&
                g.save.health>=g.save.maxHealth-20&&g.save.inventory[0]==2,
                "Charter materially improves real healing and cold relief for the old wood price");
        }

        public static void NewTrailRamblingsKeepSaveV2AndPermanentRewards(Campaign data)
        {
            for (int room = 0; room < 8; room++)
            {
                MapData map = data.maps[room];
                Check(map.rows.Length == 80 && map.rows[0].Length == 120,
                    "Outdoor sixth-ring dimensions must be 120x80 in room " + room);
                foreach (string row in map.rows)
                    Check(row.Length == 120, "Every terrain row remains rectangular");
                Check(map.rows[13][17] == '=' && map.rows[14][18] == '=',
                    "Original central campaign road is retained");
                foreach (WorldObject o in map.objects)
                    if (o.kind == "waytrail")
                    {
                        Check(o.x >= 14 && o.y >= 22,
                            "New exploration marker stays beyond the old central authored area");
                        int x = (int)o.x, y = (int)o.y;
                        Check(map.rows[y][x] != '#' && map.rows[y][x] != '~' &&
                            map.rows[y][x] != 'f', "Trail marker is not walled into impassable terrain");
                    }
                Check(Array.FindAll(map.objects, o => o.kind == "waytrail").Length == 2,
                    "Each outdoor region gets two distinct readable waypoints");
            }
            GameCore game = new GameCore(data);
            WorldObject landmark = Array.Find(data.maps[0].objects, o => o.id == "waytrail0s");
            int coins = game.save.coins, shards = game.save.shards;
            game.save.x = 18f; game.save.y = 18f;
            game.Talk(landmark);
            Check(!game.Has("waytrail_waytrail0s") &&
                game.save.coins == coins && game.save.shards == shards,
                "A direct remote Talk call cannot discover a waypoint or grant its reward");
            game.Close();
            game.save.x = landmark.x; game.save.y = landmark.y;
            game.Talk(landmark);
            Check(game.Has("waytrail_waytrail0s") &&
                game.save.coins == coins + 8 && game.save.shards == shards + 1,
                "Recording a physically reached waypoint grants a one-time survey reward");
            game.Close(); game.Talk(landmark);
            Check(game.save.coins == coins + 8 && game.save.shards == shards + 1,
                "Rereading a waypoint never duplicates rewards");
            SaveData stored = game.save;
            Check(stored.version == 2, "Waypoint flags must not alter SaveData version");
            // Annex fog has independent 6x6 flags; legacy 24-bit interior
            // exploration remains unchanged after the world expands.
            game.Close();
            game.save.x = 14.5f; game.save.y = 30.5f;
            Check(!game.Explored(0, 14, 30), "Unknown annex stays fogged before walking there");
            game.Tick(.02f, new Point());
            Check(game.Explored(0, 14, 30) && !game.Explored(0, 47, 23),
                "Visiting one annex must not reveal the distant eastern branch");
            GameCore loaded = new GameCore(data, stored);
            Check(loaded.Explored(0, 14, 30) && !loaded.Explored(0, 47, 23),
                "Annex exploration persists in existing save string flags");
            Check(loaded.Has("waytrail_waytrail0s") &&
                loaded.save.coins == coins + 8 && loaded.save.shards == shards + 1,
                "Waypoint discovery and rewards survive a normal save reload");
        }

        public static void ChapterEightRoadCharterUsesNewMapsAndChangesActualProgression(Campaign data)
        {
            var g = new GameCore(data);
            g.mode = "world";
            g.save.stage = 3; g.save.ending = 1; g.save.level = 10;
            WorldObject guild = Array.Find(data.maps[0].objects, o => o.kind == "guild");
            g.save.x = guild.x; g.save.y = guild.y;
            g.Talk(guild);
            Check(Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild") < 0,
                "Chapter Eight remains locked before the Chapter Seven hearing");
            g.Close();
            g.save.flags.Add("hearing_complete");
            g.save.flags.Add("hearing_orrin_aid");
            Check(g.RoadCharterReady && !g.RoadCharterStarted, "Signed hearing unlocks the road chapter");
            g.Talk(guild);
            int idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            Check(idx >= 0, "Guild presents a new chapter without removing prior dialogue choices");
            g.Choose(idx);
            Check(g.mode == "dialog", "Chapter Eight introduction is playable dialogue");
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_begin");
            Check(idx >= 0, "Lyra offers real expedition acceptance");
            g.Choose(idx);
            Check(g.RoadCharterStarted && g.save.trackedQuest == "roadcharter" &&
                g.RoadCharterReports == 0, "Starting new main chapter records a trackable objective");
            int preCoins = g.save.coins, preShards = g.save.shards;
            for (int room = 1; room <= 3; room++)
            {
                WorldObject marker = Array.Find(data.maps[room].objects,
                    o => o.id == "waytrail" + room + "e");
                g.save.room = room; g.save.x = 18f; g.save.y = 18f;
                g.mode = "world";
                g.Talk(marker);
                Check(g.RoadCharterReports == room - 1 &&
                    !g.Has("roadcharter_report_" + room) &&
                    !g.Has("waytrail_" + marker.id),
                    "A distant direct Talk call cannot submit Chapter Eight or survey evidence");
                g.Close();
                g.save.x = marker.x; g.save.y = marker.y;
                g.Talk(marker);
                Check(g.RoadCharterReports == room &&
                    g.Has("roadcharter_report_" + room),
                    "Physical eastern survey advances exactly one regional main-story report");
                g.Close();
                g.Talk(marker); g.Close();
                Check(g.RoadCharterReports == room, "Rereading one marker cannot duplicate evidence");
            }
            Check(g.GetQuest("roadcharter").room == 0 &&
                g.GetQuest("roadcharter").target.x == guild.x,
                "Three surveyed regions route player back to Lyra");
            g.save.room = 0; g.save.x = guild.x; g.save.y = guild.y;
            g.Talk(guild); idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            Check(idx >= 0, "Guild retains Chapter Eight resolution");
            g.Choose(idx);
            float priorSpeed = g.Speed;
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_public");
            Check(idx >= 0, "Civilian priority is a real player decision");
            g.Choose(idx);
            Check(g.RoadCharterComplete && g.Has("roadcharter_public") &&
                Math.Abs(g.Speed - priorSpeed - .25f) < .001f &&
                g.save.coins == preCoins + 3 * 8 + 45 &&
                g.save.shards == preShards + 3 + 3,
                "Chapter Eight's first ending rewards once and grants a real speed upgrade");
            SaveData snapshot = g.save;
            var loaded = new GameCore(data, snapshot);
            Check(snapshot.version == 2 && loaded.RoadCharterComplete &&
                loaded.Has("roadcharter_public") && loaded.RoadCharterReports == 3,
                "Road charter and collected testimony persist without a SaveData migration");
            g.Talk(guild);
            idx = Array.FindIndex(g.conversation.choices, c => c.action == "roadcharter_guild");
            g.Choose(idx); g.Close();
            Check(g.save.coins == snapshot.coins && g.save.shards == snapshot.shards,
                "Rereading completed public roads cannot farm the main-story payout");

            var reserve = new GameCore(data);
            reserve.save.flags.Add("hearing_complete");
            reserve.save.flags.Add("hearing_orrin_patrol");
            reserve.save.flags.Add("roadcharter_started");
            for (int room = 1; room <= 3; room++)
                reserve.save.flags.Add("roadcharter_report_" + room);
            reserve.save.stage = 3; reserve.save.ending = 1; reserve.save.level = 10;
            reserve.save.x = guild.x; reserve.save.y = guild.y;
            reserve.mode = "world";
            float speedBefore = reserve.Speed;
            reserve.Talk(guild);
            idx = Array.FindIndex(reserve.conversation.choices, c => c.action == "roadcharter_guild");
            reserve.Choose(idx);
            idx = Array.FindIndex(reserve.conversation.choices, c => c.action == "roadcharter_reserve");
            reserve.Choose(idx);
            Check(reserve.Has("roadcharter_reserve") && reserve.RoadCharterComplete &&
                Math.Abs(reserve.Speed - speedBefore) < .001f &&
                reserve.save.version == 2,
                "Alternative ending grants a distinct choice without quietly changing movement");
            float startEnergy = 15f;
            reserve.save.energy = startEnergy;
            // The production simulation deliberately clamps each frame to
            // 50 ms. Ten ordinary frames, not one artificial 500 ms tick,
            // must accumulate half a second of real regeneration.
            for (int frame = 0; frame < 10; frame++)
                reserve.Tick(.05f, new Point());
            Check(reserve.save.energy >= startEnergy + 8.4f &&
                reserve.save.energy < startEnergy + 8.7f,
                "Emergency-road charter adds +3 per second to real frame-by-frame stamina recovery");
        }

        public static void RidgeCrossingRejectsLiveEscortAndRemoteCombatThreats(Campaign data)
        {
            var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
            g.save.flags.Add("ridge_seen_0_0");
            g.save.flags.Add("ridge_seen_0_1");
            WorldObject north=Object(g,"ridgemarker");
            WorldObject east=null,west=null;
            foreach(WorldObject o in g.Map.objects)
            {
                if(o.id=="ridge_route_0_0")east=o;
                if(o.id=="ridge_route_0_1")west=o;
            }
            Check(north!=null&&east!=null&&west!=null&&g.RidgeLinked(0),
                "Original linked ridge uses two distinct real map marker objects");
            g.save.x=west.x;g.save.y=west.y;g.save.energy=95;
            float energy=g.save.energy;
            // Issue real marker dialogue/choice dispatch, including the
            // authoritative source identity required by the travel action.
            // Reflection-only calls bypass that contract and are not a
            // playable crossing, even if the far end is safe.
            Action<GameCore> choose = candidate =>
            {
                candidate.Talk(west);
                Check(candidate.mode=="dialog"&&candidate.conversation!=null,
                    "Reachable ridge marker opens a real dialogue");
                int option=Array.FindIndex(candidate.conversation.choices,
                    c=>c.action=="ridge_travel");
                Check(option>=0,"The linked marker offers its live travel choice");
                candidate.Choose(option);
            };
            var shot=new Projectile{position=new Point(east.x,east.y),
                velocity=new Point(1,0),damage=8,life=12,friendly=false,source="shot"};
            g.projectiles.Add(shot);
            choose(g);
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "Incoming live enemy projectile at the destination forbids unsafe remote travel");
            g.projectiles.Remove(shot);
            var warning=new Hazard{position=new Point(east.x,east.y),
                radius=1.2f,timer=4f,damage=15,kind="rift"};
            g.hazards.Add(warning);
            choose(g);
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "A still-armed delayed blast at the landing also blocks ridge travel");
            g.hazards.Remove(warning);
            var enemy=new Enemy(92921,0,east.x+1,east.y,70){timer=90};
            g.enemies.Add(enemy);
            choose(g);
            Check((g.Position-new Point(west.x,west.y)).Length<.01f&&
                g.save.energy==energy,
                "An actual hostile defender at destination blocks shortcut traversal");
            g.enemies.Clear();
            choose(g);
            Check((g.Position-new Point(east.x,east.y)).Length<.01f&&
                g.save.energy==energy-12,
                "Once the far end is truly safe, the original paid crossing still works");

            // A separate original Noll course is a genuine field mission,
            // even when ordinary enemies and both ridge endpoints are safe.
            var training=Fresh(data,0);training.Enter(0);training.enemies.Clear();
            training.save.flags.Add("ridge_seen_0_0");
            training.save.flags.Add("ridge_seen_0_1");
            WorldObject forge=Object(training,"forge");
            training.save.x=forge.x;training.save.y=forge.y;
            Advance(training,1.1f);training.save.energy=100;
            Action(training,"forge","swordlesson_start");
            Check(training.SwordLessonActive,
                "The actual blacksmith opens a timed class-only projectile lesson");
            training.save.x=west.x;training.save.y=west.y;
            energy=training.save.energy;
            choose(training);
            Check(training.SwordLessonActive&&
                (training.Position-new Point(west.x,west.y)).Length<.01f&&
                training.save.energy==energy,
                "Even a temporarily nearby shortcut cannot bypass or erase a running live training clock");
            training.PrepareSave();var copy=new GameCore(data,CloneSave(training.save));
            Check(copy.save.version==2&&copy.RidgeLinked(0)&&!copy.SwordLessonActive,
                "SaveData v2 preserves the earned linked route without serializing timed drills");
        }

        public static void CompanionButtonGestureDistinguishesTapFromHoldAndCancelsSafely(Campaign data)
        {
            var gesture=new CompanionButtonGesture();
            Check(gesture.Advance(true,.10f)==CompanionButtonAction.None&&
                gesture.Advance(true,.12f)==CompanionButtonAction.None&&
                gesture.Advance(false,.01f)==CompanionButtonAction.Command,
                "A short RB tap produces exactly one ordinary companion command on release");
            Check(gesture.Advance(true,.10f)==CompanionButtonAction.None&&
                gesture.Advance(true,.10f)==CompanionButtonAction.None&&
                gesture.Advance(true,.10f)==CompanionButtonAction.None&&
                gesture.Advance(true,.10f)==CompanionButtonAction.ToggleHold&&
                gesture.Advance(true,.10f)==CompanionButtonAction.None&&
                gesture.Advance(false,.01f)==CompanionButtonAction.None,
                "A sustained RB press toggles hold once and cannot also issue a command on release");
            gesture.Advance(true,.1f);gesture.Cancel();
            Check(gesture.Advance(false,.01f)==CompanionButtonAction.None,
                "Leaving world mode cancels an unfinished companion-button tap");
            var malformed=new CompanionButtonGesture();
            Check(malformed.Advance(true,float.NaN)==CompanionButtonAction.None&&
                malformed.Advance(false,float.PositiveInfinity)==CompanionButtonAction.Command,
                "Malformed frame time cannot create a phantom hold or repeated command");
        }


        public static void ReliefWatchSideOnProximityCannotBodyguardRealMeleeStock(Campaign data)
        {
            var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
            var post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
            Check(post!=null,"The actual physical outpost is available");
            g.save.flags.Add("outer_cache_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"Regional relief watch can start normally");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "The test exercises a real two-raider relief siege");
            var foe=g.enemies.Find(e=>e.kind==0);
            var other=g.enemies.Find(e=>e!=foe);
            Check(foe!=null&&other!=null,
                "Real melee and bowmen retain their original archetypes");
            // Keep the live archer in the encounter but safely away from
            // the melee corridor so another body does not impersonate
            // the hero's claimed physical protection.
            other.x=post.x+7f;other.y=post.y+7f;other.stun=30f;
            var target=new Point(post.x,post.y);
            var bodyguard=typeof(GameCore).GetMethod("OuterPatrolHeroIntercepts",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var committed=typeof(GameCore).GetMethod("OuterPatrolUncoveredStockStrike",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var release=typeof(GameCore).GetMethod("DamageOuterPatrolStock",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(bodyguard!=null&&committed!=null&&release!=null,
                "Actual collision, imminent cache pressure and damage functions are used");
            bool found=false;
            Point side=new Point();
            Point[] offsets={new Point(1.5f,0),new Point(-1.5f,0),
                new Point(0,1.5f),new Point(0,-1.5f)};
            foreach(Point off in offsets)
            {
                Point enemyFoot=target+off;
                if(!g.CanStand(enemyFoot))continue;
                foe.x=enemyFoot.x;foe.y=enemyFoot.y;
                foe.state=1;foe.timer=.28f;foe.windupDuration=.65f;
                foe.stun=foe.hitTime=0f;
                foe.targetingOuterPatrolStock=true;foe.target=target;
                foe.Face(target-foe.Position);
                // Tangential defender is physically near the attacker,
                // but not in the line where the blade hits the cache.
                Point tangent=new Point(-off.y,off.x).Normalized;
                foreach(float d in new[]{.9f,-.9f,1.1f,-1.1f})
                {
                    Point candidate=enemyFoot+tangent*d;
                    if(!g.CanStand(candidate))continue;
                    g.save.x=candidate.x;g.save.y=candidate.y;
                    if((candidate-enemyFoot).Length>2.2f||
                        (bool)bodyguard.Invoke(g,new object[]{foe})||
                        !(bool)committed.Invoke(g,new object[]{foe}))
                        continue;
                    side=candidate;found=true;break;
                }
                if(found)break;
            }
            Check(found,"Real map offers a near but off-axis defender while one stock strike remains physically uncovered");
            float stock=g.OuterPatrolIntegrity;
            Check(g.OuterPatrolIncomingStockThreat==foe&&
                g.OuterPatrolBodyguardedThreat==null,
                "A sideways defender cannot erase the real danger HUD or falsely arm the bodyguard indicator");
            release.Invoke(g,new object[]{foe});
            Check(g.OuterPatrolIntegrity<stock,
                "An unguarded actual cache strike costs real supply integrity even with the hero nearby");
            float after=g.OuterPatrolIntegrity;
            g.save.x=target.x;g.save.y=target.y;
            Check((bool)bodyguard.Invoke(g,new object[]{foe})&&
                System.Object.ReferenceEquals(g.OuterPatrolBodyguardedThreat,foe)&&
                (g.OuterPatrolCacheFoot-target).Length<.0001f,
                "Only exact physical bodyguarding turns the live HUD and grounded world lane ready");
            Check(g.OuterPatrolIncomingStockThreat==null,
                "The live combat warning retracts only for real physical bodyguarding");
            release.Invoke(g,new object[]{foe});
            Check(g.OuterPatrolIntegrity==after&&g.save.version==2,
                "The hero blocks only in the actual objective contact corridor; SaveData v2 is unchanged");
        }


        public static void ReliefWatchOffAxisCannotAvoidActualReleasedSupplyHit(Campaign data)
        {
            var g=Fresh(data,0);g.Enter(0);g.enemies.Clear();
            var post=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
            Check(post!=null,"The original physical regional watch exists");
            g.save.flags.Add("outer_cache_0");
            g.save.x=post.x;g.save.y=post.y;
            g.Talk(post);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_watch_start");
            Check(start>=0,"The actual rescue siege is physically startable");
            g.Choose(start);
            Check(g.OuterPatrolActive&&g.enemies.Count==2,
                "The real siege must run both enemy actors");
            var melee=g.enemies.Find(e=>e.kind==0);
            var bow=g.enemies.Find(e=>e!=melee);
            Check(melee!=null&&bow!=null,"The original melee and bow combat roles survive");
            bow.stun=300f;
            bow.x=post.x+8;bow.y=post.y+8;
            Point station=new Point(post.x,post.y);
            bool found=false;
            foreach(Point off in new[]{new Point(-1.5f,0),new Point(1.5f,0),
                new Point(0,-1.5f),new Point(0,1.5f)})
            {
                Point origin=station+off;
                if(!g.CanStand(origin))continue;
                melee.x=origin.x;melee.y=origin.y;
                melee.state=1;melee.timer=.035f;
                melee.windupDuration=.65f;
                melee.stun=melee.hitTime=melee.releaseTime=0f;
                melee.target=station;melee.targetingOuterPatrolStock=true;
                melee.Face(station-origin);
                Point lateral=new Point(-off.y,off.x).Normalized;
                foreach(float length in new[]{.9f,-.9f,1.1f,-1.1f})
                {
                    Point side=origin+lateral*length;
                    if(!g.CanStand(side))continue;
                    g.save.x=side.x;g.save.y=side.y;
                    if(g.OuterPatrolIncomingStockThreat==melee&&
                        g.OuterPatrolBodyguardedThreat==null)
                    {found=true;break;}
                }
                if(found)break;
            }
            Check(found,"The physical watch offers a genuine near-but-off-axis hero observation foot");
            g.invulnerable=1000f;
            float first=g.OuterPatrolIntegrity;
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolActive&&melee.state==0&&
                g.OuterPatrolIntegrity<first,
                "A real released outpost melee attack must damage the cache when hero is merely adjacent");
            float damaged=g.OuterPatrolIntegrity;
            // Keep the same registered enemy, let its real recovery end,
            // then physically occupy the cache's actual contact lane.
            melee.stun=1f;
            Advance(g,.35f);
            Check(melee.releaseTime<=.001f&&melee.state==0,
                "The raider must finish genuine post-contact recovery before a new attack");
            g.save.x=station.x;g.save.y=station.y;
            melee.stun=0f;melee.hitTime=0f;melee.releaseTime=0f;
            melee.state=1;melee.timer=.035f;
            melee.target=station;melee.targetingOuterPatrolStock=true;
            melee.Face(station-melee.Position);
            Check(System.Object.ReferenceEquals(
                    g.OuterPatrolBodyguardedThreat,melee)&&
                g.OuterPatrolIncomingStockThreat==null,
                "The same physical raider becomes truly bodyguarded only when the hero occupies its contact lane");
            g.Tick(.05f,new Point());
            Check(g.OuterPatrolIntegrity==damaged&&melee.state==0&&
                g.effects.Exists(e=>e.outerPatrolOwned&&
                    e.text==g.L("拦截成功","INTERCEPTED"))&&
                g.save.version==2,
                "The genuine second strike is intercepted at release without restoring stock or rewriting v2 saves");
        }

    }
}
