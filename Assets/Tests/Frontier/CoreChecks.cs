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
        private static void Action(GameCore g, string kind, string action)
        {
            g.Talk(Object(g, kind));
            Check(g.mode == "dialog", "Expected dialogue at " + kind);
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { g.Choose(i); return; }
            throw new Exception("Missing action " + action);
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
            int coins = g.save.coins; g.Talk(Object(g, "garden"));
            foreach (var c in g.conversation.choices) Check(c.action != "ally1", "Completed quest cannot reward twice");
            Check(g.save.coins == coins && g.Allies == 1 && g.save.skillPoints == 1, "Commission rewards");
            g.Learn(1); Check(g.save.skillPoints == 0 && g.save.maxHealth == 152, "Talent allocation");
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
                    int frames = 0; do { g.Tick(.02f, new Point()); } while ((Math.Abs(g.fishCursor - .5f) > .08f || frames++ < 5) && frames < 1000);
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
            g.Enter(1); g.Talk(Object(g, "scout")); Check(!g.GateOpen, "Scout cannot open the gate before the rescue"); g.Close();
            int herbs = g.save.inventory[2]; var node = Object(g, "herb"); g.Talk(node); g.Talk(node); Check(g.save.inventory[2] == herbs + 3, "A resource node yields once per rest");
            g.save.kills[1] = 4; Action(g, "scout", "scout"); Check(g.GateOpen, "Rescue opens forest gate");
            g.save.stage = 1; g.Enter(2); g.Talk(Object(g, "rune2")); Check(g.runeProgress == 0, "Wrong rune resets puzzle");
            g.Talk(Object(g, "rune0")); g.Talk(Object(g, "rune2")); g.Talk(Object(g, "rune1")); Check(g.GateOpen, "Correct rune order opens ruins");
            g.save.stage = 2; g.Enter(3); g.save.inventory[4] = 0; Action(g, "engine", "engine"); Check(!g.GateOpen, "Mine repair requires parts");
            g.Talk(Object(g, "ore")); Action(g, "engine", "engine"); Check(g.GateOpen && g.save.inventory[4] == 0, "Gathered parts repair elevator");
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
        public static void SaveValidation(Campaign data)
        {
            var bad = new SaveData { room = 99, stage = -1, x = float.NaN, y = -300, health = float.PositiveInfinity, coins = -100, inventory = null, talents = null, kills = null, flags = null };
            var g = new GameCore(data, bad); Check(g.save.room == 0 && g.save.x == 18 && g.save.coins == 0 && g.save.health == 120, "Damaged saves are sanitized");
            bool rejected = false; try { new GameCore(data, new SaveData { version = 900 }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unsupported save schema is rejected");
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
                Point toward = g.DirectionTo(g.Position, target.Position), movement = distance > 1.4f ? toward : new Point();
                if (target.state == 1 && (target.boss || target.kind == 2) && (g.Position - target.target).Length < (target.boss ? 3.4f : 2))
                { movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0); g.Dash(movement); }
                foreach (Hazard h in g.hazards) if (h.timer < .5f && (g.Position - h.position).Length < h.radius + .5f)
                { movement = (g.Position - h.position).Length > .05f ? (g.Position - h.position).Normalized : new Point(-1, 0); g.Dash(movement); break; }
                g.Tick(.02f, movement); g.Attack(target.Position - g.Position);
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
                if (room == 2) { g.Talk(Object(g, "rune0")); g.Talk(Object(g, "rune2")); g.Talk(Object(g, "rune1")); }
                if (room == 3) { g.Talk(Object(g, "ore")); Action(g, "engine", "engine"); }
                Check(g.GateOpen, "Chapter gate");
                AwaitArenaOpening(g); // Hit stop pauses the same opening clock; wait for actual collision.
                g.save.x = 28; g.save.y = 10; Action(g, "shrine", "boss"); Combat(g, true);
                Check(g.Has("boss_" + room), "Guardian defeated");
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
            g.Enter(4); g.Talk(Object(g, "shrine")); endingChoice = -1;
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending3") endingChoice = i;
            Check(endingChoice >= 0, "Three allies unlock the alternative ending"); g.Choose(endingChoice); Check(g.save.ending == 3, "Third ending");
            g.Close(); g.Talk(Object(g, "shrine"));
            for (int i = 0; i < g.conversation.choices.Length; i++) if (g.conversation.choices[i].action == "ending2") { g.Choose(i); break; }
            Check(g.save.ending == 2, "Second ending");
        }


        public static void ExpandedHearthInnArchiveIsConnectedAndRewardsVerifiedExploration(Campaign data)
        {
            MapData inn=data.maps[9];
            Check(inn.rows.Length==32&&inn.rows[0].Length==48,"Expanded Hearth Inn adds navigable rooms");
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
            game.save.x=cache.x;game.save.y=cache.y;
            int coins=game.save.coins;
            game.Talk(cache);Check(game.save.coins==coins+7 && game.Has("annex_cache_"+cache.id),"Looting physical cache grants once");
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
            GameCore reload=new GameCore(data,game.save);
            Check(reload.save.version==2&&reload.Has("annex_camp_"+camp.id)&&reload.Has("annex_cache_"+cache.id)&&reload.Has("annex_vista_"+vista.id),
                "All exploration progress remains in v2 save");
        }

        public static void EightCircuitRoutesUseActualWalkingOrderAndPersist(Campaign data)
        {
            for(int room=0;room<8;room++)
            {
                MapData m=data.maps[room];
                Check(m.rows.Length==56&&m.rows[0].Length==84,
                    "Third-ring authored terrain is 84x56 in every outdoor area");
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
                g.save.x=sites[2].x;g.save.y=sites[2].y;int startCoins=g.save.coins;
                g.Talk(sites[2]);g.Close();
                Check(g.TrailCircuitProgress(room)==0,"Third landmark cannot bypass first two");
                for(int k=0;k<3;k++)
                {
                    g.save.x=sites[k].x;g.save.y=sites[k].y;
                    g.Talk(sites[k]);g.Close();
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

        public static void NewTrailRamblingsKeepSaveV2AndPermanentRewards(Campaign data)
        {
            for (int room = 0; room < 8; room++)
            {
                MapData map = data.maps[room];
                Check(map.rows.Length == 56 && map.rows[0].Length == 84,
                    "Outdoor expansion dimensions must be 84x56 in room " + room);
                foreach (string row in map.rows)
                    Check(row.Length == 84, "Every terrain row remains rectangular");
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
            game.Talk(landmark);
            Check(game.Has("waytrail_waytrail0s") &&
                game.save.coins == coins + 8 && game.save.shards == shards + 1,
                "Recording a real waypoint grants a one-time survey reward");
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
                g.save.room = room; g.save.x = marker.x; g.save.y = marker.y;
                g.mode = "world";
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

    }
}
