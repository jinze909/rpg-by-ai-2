using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static SaveData CloneSave(SaveData source)
        {
            // Copy actual public save fields, including arrays, to check reconstruction independently of Unity JSON.
            var copy = new SaveData();
            foreach (FieldInfo f in typeof(SaveData).GetFields())
            {
                object value = f.GetValue(source);
                if (value is int[]) value = ((int[])value).Clone();
                else if (value is EnemyRecord[])
                {
                    var original = (EnemyRecord[])value; var records = new EnemyRecord[original.Length];
                    for (int i = 0; i < records.Length; i++) if (original[i] != null) { var r = original[i]; records[i] = new EnemyRecord { id = r.id, kind = r.kind, boss = r.boss, x = r.x, y = r.y, hp = r.hp, maxHp = r.maxHp }; }
                    value = records;
                }
                else if (value is List<string>) value = new List<string>((List<string>)value);
                f.SetValue(copy, value);
            }
            return copy;
        }
        private static GameCore Unlocked(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3;
            foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report", "mountain_report", "gate_5", "gate_6", "gate_7", "companion_0", "companion_1", "companion_2", "seal_5", "seal_6", "seal_7" }) g.save.flags.Add(flag);
            g.save.frontierEnding = 1; return g;
        }
        private static Point StandableOutsideInteractionReach(GameCore g, WorldObject target)
        {
            for (int ix = -8; ix <= 8; ix++)
            for (int iy = -8; iy <= 8; iy++)
            {
                float dx = ix * .25f, dy = iy * .25f;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance < 1.8f || distance > 2.15f) continue;
                Point candidate = new Point(target.x + dx, target.y + dy);
                if (g.CanStand(candidate)) return candidate;
            }
            throw new InvalidOperationException("No standable out-of-range test position was found.");
        }

        private static bool TryFindWallBlockedApproach(GameCore g, WorldObject target,
            out Point playerPosition)
        {
            string[] rows = g.Map.rows;
            int height = rows.Length, width = rows[0].Length;
            for (int y = 3; y < height - 3; y++)
            for (int x = 4; x < width - 3; x++)
            {
                // A solid tile between two otherwise standable cells gives a
                // deterministic line-of-sight fixture without changing terrain.
                if (rows[y][x - 1] != '#') continue;
                Point targetPoint = new Point(x + .3f, y + .5f);
                Point approach = new Point(x - 1.25f, y + .5f);
                if (!g.CanStand(targetPoint) || !g.CanStand(approach) ||
                    (targetPoint - approach).Length >= 1.75f) continue;
                target.x = targetPoint.x;
                target.y = targetPoint.y;
                playerPosition = approach;
                return true;
            }
            playerPosition = new Point();
            return false;
        }


        public static void EightRegionRescueRewardsSurviveTalentCapAndV2Reload(Campaign data)
        {
            var g=Unlocked(data);
            for(int r=0;r<7;r++)g.save.flags.Add("outer_cache_"+r);
            g.save.flags.Add("outer_log_7");
            g.save.flags.Add("outer_relay_7");
            g.save.skillPoints=99;
            g.Enter(7);g.enemies.Clear();g.projectiles.Clear();
            WorldObject cache=Array.Find(g.Map.objects,o=>o.kind=="outercache");
            Check(cache!=null,"Actual eighth-region rescue chest remains present");
            TalkNearObject(g,cache);
            Check(g.Has("outer_rescues_complete")&&g.OuterRescueCount==8&&
                g.OuterMilestoneRewardPending("outer_rescues_complete")&&
                g.save.skillPoints==99,
                "The real eighth chest owes its capped, one-time earned +1 talent rather than erasing it");
            g.Close();g.PrepareSave();
            g=new GameCore(data,CloneSave(g.save));g.Start();g.enemies.Clear();
            Check(g.save.version==2&&g.OuterMilestoneRewardPending("outer_rescues_complete"),
                "Original SaveData v2 persists unpaid skill point without a new serialized field");
            WorldObject lamp=Array.Find(g.Map.objects,o=>o.kind=="outerrelay");
            TalkNearObject(g,lamp);
            int claim=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim");
            Check(claim>=0&&g.conversation.text.Contains("八地账册"),
                "The real repaired relay exposes a signed outstanding completion reward alongside ordinary options");
            g.Choose(claim);
            Check(g.save.skillPoints==99&&g.OuterMilestoneRewardPending("outer_rescues_complete"),
                "Trying to claim at full talent capacity leaves the real IOU intact");
            g.save.skillPoints=98;
            TalkNearObject(g,lamp);
            claim=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim");
            Check(claim>=0,"The original relay continues to recognize the signed reward after capacity clears");
            g.Choose(claim);
            Check(g.save.skillPoints==99&&!g.OuterMilestoneRewardPending("outer_rescues_complete")&&
                g.Has("outer_rescues_complete"),
                "The original worker pays exactly one talent and never revokes or reruns the completed regional rescue");
            TalkNearObject(g,lamp);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_milestone_claim")<0,
                "An already signed talent cannot be duplicated by reopening the original relay dialogue");
            g.Close();
        }

        public static void EighthRoadwardenDeliveryAndAllWatchMedalsKeepRealIndependentClaims(Campaign data)
        {
            var g=Unlocked(data);
            for(int r=0;r<7;r++)g.save.flags.Add("outer_delivery_done_"+r);
            g.save.flags.Add("outer_log_7");g.save.flags.Add("outer_relay_7");
            g.save.flags.Add("outer_cache_7");g.save.flags.Add("outer_patrol_done_7");
            g.save.skillPoints=99;g.Enter(7);g.enemies.Clear();g.projectiles.Clear();
            WorldObject watch=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
            WorldObject lamp=Array.Find(g.Map.objects,o=>o.kind=="outerrelay");
            Check(watch!=null&&lamp!=null,"Real watch, bridge supply network and rescue lamp are present");
            // Reconstruct only the already-earned TRANSIENT final timed
            // parcel leg; the original watchkeeper Choice still executes
            // every real delivery, ownership and payout check.
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(GameCore).GetField("outerDeliveryRoom",flags).SetValue(g,7);
            typeof(GameCore).GetField("outerDeliveryStage",flags).SetValue(g,2);
            typeof(GameCore).GetField("outerDeliverySeconds",flags).SetValue(g,30f);
            TalkNearObject(g,watch);
            int deliver=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_delivery_deliver");
            Check(deliver>=0,"Eighth real local watchkeeper offers the authenticated original final handoff");
            g.Choose(deliver);
            Check(g.Has("outer_deliveries_complete")&&g.OuterDeliveryWins==8&&
                g.OuterMilestoneRewardPending("outer_deliveries_complete")&&
                g.save.skillPoints==99,
                "Eight genuine medicine receipts are completed, while one permanently earned capped talent stays payable");
            TalkNearObject(g,lamp);
            Check(Array.FindIndex(g.conversation.choices,c=>c.action=="outer_milestone_claim")<0,
                "The wrong staffed relay cannot impersonate the watch for an eight-delivery talent");
            g.Close();
            string[] otherMilestones={"outer_patrol_all","outer_patrol_veteran_all",
                "outer_patrol_flawless_all"};
            var award=typeof(GameCore).GetMethod("AwardOuterMilestoneTalent",flags);
            Check(award!=null,"All three rank-specific watch completions use the exact production reward transfer");
            foreach(string id in otherMilestones)
            {
                g.save.flags.Add(id);award.Invoke(g,new object[]{id});
                Check(g.OuterMilestoneRewardPending(id),
                    "Ordinary, veteran and flawless ranks independently reserve their earned capped completion prize: "+id);
            }
            string[] all={"outer_deliveries_complete","outer_patrol_all",
                "outer_patrol_veteran_all","outer_patrol_flawless_all"};
            int initialCoins=g.save.coins;
            for(int i=0;i<all.Length;i++)
            {
                g.save.skillPoints=98;TalkNearObject(g,watch);
                int claim=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_milestone_claim");
                Check(claim>=0&&Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_routes")>=0,
                    "Authentic staffed post preserves existing road/veteran actions while exposing a real pending claim");
                if(i==0)
                {
                    g.save.x+=12f;g.save.y+=12f;
                    g.Choose(claim);
                    Check(g.OuterMilestoneRewardPending(all[0]),
                        "A cached watch conversation cannot remotely authorize even an earned completion prize");
                    TalkNearObject(g,watch);
                    claim=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="outer_milestone_claim");
                }
                g.Choose(claim);
                Check(g.save.skillPoints==99&&
                    !g.OuterMilestoneRewardPending(all[i])&&
                    g.save.coins==initialCoins,
                    "Each physical receipt pays precisely one talent in order without minting extra currency or replaying another rank");
            }
            TalkNearObject(g,watch);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim")<0&&g.save.version==2,
                "All four actual watch debts are retired after one authentic signed claim each");
            g.Close();
        }


        public static void CombinedWatchReceiptsRespectPhysicalSourceCapacityAndReload(Campaign data)
        {
            var g=Unlocked(data);
            g.Enter(7);g.enemies.Clear();g.projectiles.Clear();
            g.save.flags.Add("outer_patrol_done_7");
            string[] debts={"outer_deliveries_complete","outer_patrol_all",
                "outer_patrol_veteran_all","outer_patrol_flawless_all"};
            foreach(string id in debts)
            {
                g.save.flags.Add(id);
                g.save.flags.Add("outer_talent_due_"+id);
            }
            int coins=g.save.coins;
            g.save.skillPoints=97;
            WorldObject watch=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
            WorldObject lamp=Array.Find(g.Map.objects,o=>o.kind=="outerrelay");
            Check(watch!=null&&lamp!=null,"Original eighth-region watch and rescue relay are present");
            TalkNearObject(g,lamp);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim_all")<0,
                "Restored relay cannot impersonate the staffed watch for combined receipts");
            g.Close();
            TalkNearObject(g,watch);
            int all=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim_all");
            Check(all>=0&&g.conversation.choices[all].en.Contains("4 owed")&&
                g.conversation.text.Contains("4 笔未结")&&
                g.conversation.textEn.Contains("4 unpaid")&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_milestone_claim")>=0&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_watch_routes")>=0,
                "Four owed watch medals offer optional combined settlement without removing ordinary actions");
            g.save.x+=12f;g.save.y+=12f;
            g.Choose(all);
            Check(g.save.skillPoints==97&&g.OuterMilestoneRewardPending(debts[0])&&
                g.OuterMilestoneRewardPending(debts[3]),
                "Walking out of the old conversation cannot remotely claim any of four owed medals");
            TalkNearObject(g,watch);
            all=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim_all");
            g.Choose(all);
            Check(g.save.skillPoints==99&&g.save.coins==coins&&
                !g.OuterMilestoneRewardPending(debts[0])&&
                !g.OuterMilestoneRewardPending(debts[1])&&
                g.OuterMilestoneRewardPending(debts[2])&&
                g.OuterMilestoneRewardPending(debts[3]),
                "Authentic signed batch pays exactly two points into remaining capacity and keeps two unpaid IOUs");
            TalkNearObject(g,watch);
            all=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim_all");
            Check(all>=0&&g.conversation.choices[all].en.Contains("2 owed"),
                "Remaining two earned receipts are visible and correctly counted");
            g.Choose(all);
            Check(g.save.skillPoints==99&&g.OuterMilestoneRewardPending(debts[2])&&
                g.OuterMilestoneRewardPending(debts[3]),
                "Full talent wallet cannot erase, overpay or retire either remaining medal");
            g.PrepareSave();
            g=new GameCore(data,CloneSave(g.save));g.Start();g.enemies.Clear();
            Check(g.save.version==2&&g.OuterMilestoneRewardPending(debts[2])&&
                g.OuterMilestoneRewardPending(debts[3]),
                "Unpaid half of an authentic combined receipt survives v2 save reconstruction");
            g.save.skillPoints=97;
            watch=Array.Find(g.Map.objects,o=>o.kind=="outerwatch");
            TalkNearObject(g,watch);
            all=Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim_all");
            Check(all>=0,"Restored outpost offers the remaining two debts after reload");
            g.Choose(all);
            Check(g.save.skillPoints==99&&g.save.coins==coins&&
                Array.TrueForAll(debts,id=>!g.OuterMilestoneRewardPending(id)),
                "Second physical visit settles remaining earned ranks exactly once with no duplicated currency");
            TalkNearObject(g,watch);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="outer_milestone_claim_all")<0&&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="outer_milestone_claim")<0,
                "Once settled, no further single or combined receipt action is offered");
            g.Close();
        }

        public static void AllFiveOuterMilestoneJournalHintsRouteToRealLocalClaimWorkers(Campaign data)
        {
            var g=Unlocked(data);
            string[] achievements={"outer_rescues_complete","outer_deliveries_complete",
                "outer_patrol_all","outer_patrol_veteran_all","outer_patrol_flawless_all"};
            string[] quests={"outer_rescue","outer_delivery",
                "outer_watch","outer_watch_veteran","outer_watch_flawless"};
            for(int room=0;room<8;room++)
            {
                g.save.flags.Add("outer_cache_"+room);
                g.save.flags.Add("outer_relay_"+room);
                g.save.flags.Add("outer_patrol_done_"+room);
                g.save.flags.Add("outer_delivery_done_"+room);
                g.save.flags.Add("outer_patrol_veteran_"+room);
                g.save.flags.Add("outer_patrol_flawless_"+room);
            }
            g.Enter(3);g.enemies.Clear();
            int flagsBefore=g.save.flags.Count,coins=g.save.coins;
            for(int i=0;i<quests.Length;i++)
            {
                var ordinary=g.GetQuest(quests[i]);
                Check(ordinary.complete&&!ordinary.next.Contains("欠付技能点"),
                    "Real eight-region completed quest initially reports history, not a fabricated unpaid talent");
                g.save.flags.Add(achievements[i]);
                g.save.flags.Add("outer_talent_due_"+achievements[i]);
                QuestGuide debt=g.GetQuest(quests[i]);
                string worker=i==0?"outerrelay":"outerwatch";
                WorldObject expected=Array.Find(g.Map.objects,o=>o.kind==worker);
                Check(debt.complete&&debt.room==3&&expected!=null&&
                    (debt.target-new Point(expected.x,expected.y)).Length<.001f&&
                    debt.next.Contains("欠付技能点1"),
                    "Every paid-in-combat but capped final prize directs the existing in-game journal toward the ACTUAL local claim worker");
                Check(!debt.next.Contains("先使用已有技能点")&&
                    debt.next.Contains(i==0?"修复的求援灯":"已守住的值守岗"),
                    "With wallet space available, live journal directs an immediate correct physical receipt instead of unnecessary skill spending");
                int availableWallet=g.save.skillPoints;
                g.save.skillPoints=99;
                Check(g.GetQuest(quests[i]).next.Contains("先使用已有技能点"),
                    "A full talent wallet correctly asks the player to free room before collecting the authentic IOU");
                g.save.skillPoints=availableWallet;
                g.save.english=true;
                Check(g.GetQuest(quests[i]).next.Contains("talent is owed")&&
                    g.GetQuest(quests[i]).room==3,
                    "Live English quest guidance gives the same original physical worker instead of replaying completed combat");
                g.save.english=false;
                g.save.flags.Remove("outer_talent_due_"+achievements[i]);
                Check(!g.GetQuest(quests[i]).next.Contains("欠付技能点"),
                    "Paying a saved IOU immediately returns its completed quest to regular historical guidance");
            }
            Check(g.save.coins==coins&&g.save.version==2&&
                g.save.flags.Count==flagsBefore+achievements.Length,
                "Journal reads never mutate wallet or completion records; only test-owned signed milestone flags are added");
        }

        public static void RemoteSoupRouteUsesThreeRealSheltersAndProvidesPhysicalCare(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("fish_kitchen_supplied");
            g.save.inventory[6]=3;
            Check(g.GetQuest("soup_route").locked==false&&
                !g.GetQuest("soup_route").complete,
                "A stocked public kitchen exposes optional remote delivery without a new v2 save field");
            int[] rooms={1,5,7};
            for(int i=0;i<rooms.Length;i++)
            {
                int room=rooms[i];
                g.save.flags.Add("far_dial_"+room);
                g.save.flags.Add("far_shelter_"+room);
                g.Enter(room);g.enemies.Clear();g.projectiles.Clear();
                Check(g.save.room==room,"Actual unlocked campaign enters each existing outer world");
                WorldObject shelter=Object(g,"farshelter");
                TalkNearObject(g,shelter);
                int index=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="soup_route_deliver");
                Check(index>=0,"Existing FarRefuge shelter menu preserves its original options and appends a real food delivery");
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="far_rest")>=0,
                    "New hot meal choice never deletes the original timber-fueled shelter rest");
                int stew=g.save.inventory[6];
                g.Choose(index);
                Check(g.save.inventory[6]==stew-1&&
                    g.SoupRouteDelivered(room)&&g.SoupRouteDeliveredCount==i+1,
                    "Each actual far-ring shelter consumes one real stew and credits exactly one local delivery");
                float health=g.save.health,energy=g.save.energy;
                g.save.health=45;g.save.energy=25;
                for(int tick=0;tick<40;tick++)g.Tick(.05f,new Point());
                Check(g.save.health>45.5f&&g.save.energy>28,
                    "An authentically stocked and physically near shelter now heals HP and energy in real world ticks");
                g.save.health=health;g.save.energy=energy;
                TalkNearObject(g,shelter);
                Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="soup_route_deliver")<0,
                    "A delivered shelter's menu cannot duplicate its original one-time handoff");
                g.Close();
            }
            Check(g.SoupRouteComplete&&g.Has("soup_route_complete")&&
                g.GetQuest("soup_route").complete&&g.save.skillPoints>=1&&
                g.WorldResponse().Contains("3/3")&&
                g.WorldResponse().Contains("守棚人"),
                "All three remote deliveries pay once, finish the real quest and change the in-game civic response");
            g.Enter(0);g.enemies.Clear();
            WorldObject fisherman=Object(g,"fish");
            TalkNearObject(g,fisherman);
            Check(g.conversation.text.Contains("都报了平安")&&
                g.conversation.textEn.Contains("sent word"),
                "Actual old-town fisherman acknowledges all three distant keepers by name without reusing a generic completion toast");
            g.Close();
            g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));reloaded.Start();
            Check(reloaded.SoupRouteDeliveredCount==3&&
                reloaded.SoupRouteComplete&&reloaded.save.version==2,
                "All three remote outposts and their actual health services persist in unchanged v2 saves");
        }

        public static void SoupRouteGuardsCachedRemoteAndCombatHandoffs(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("fish_kitchen_supplied");
            g.save.flags.Add("far_dial_1");
            g.save.flags.Add("far_shelter_1");
            g.save.inventory[6]=2;
            g.Enter(1);g.enemies.Clear();
            WorldObject shelter=Object(g,"farshelter");
            TalkNearObject(g,shelter);
            int index=Array.FindIndex(g.conversation.choices,
                c=>c.action=="soup_route_deliver");
            Check(index>=0,"Genuine repaired refuge exposes optional supply choice");
            int stew=g.save.inventory[6];
            g.save.x=shelter.x+15f;g.save.y=shelter.y+15f;
            g.Choose(index);
            Check(g.save.inventory[6]==stew&&!g.SoupRouteDelivered(1),
                "Cached shelter interaction cannot consume a stew from outside physical reach");
            TalkNearObject(g,shelter);
            index=Array.FindIndex(g.conversation.choices,
                c=>c.action=="soup_route_deliver");
            g.save.inventory[6]=0;g.Choose(index);
            Check(!g.SoupRouteDelivered(1)&&g.save.inventory[6]==0,
                "An empty inventory cannot fake the delivery or claim XP");
            g.save.inventory[6]=2;TalkNearObject(g,shelter);
            index=Array.FindIndex(g.conversation.choices,
                c=>c.action=="soup_route_deliver");
            g.enemies.Add(new Enemy(880100,0,g.Position.x+1,g.Position.y,50));
            g.Choose(index);
            Check(!g.SoupRouteDelivered(1)&&g.save.inventory[6]==2,
                "Real nearby combat prevents humanitarian supply handoffs");
            g.enemies.Clear();
            TalkNearObject(g,shelter);
            index=Array.FindIndex(g.conversation.choices,c=>c.action=="soup_route_deliver");
            g.Choose(index);
            Check(g.SoupRouteDelivered(1)&&g.save.inventory[6]==1&&
                g.WorldResponse().Contains("1/3"),
                "Safely restored local interaction commits one real stew and updates the public regional supply record");
            g.save.health=50;g.save.energy=25;
            g.save.x=shelter.x+15f;g.save.y=shelter.y+15f;
            for(int i=0;i<30;i++)g.Tick(.05f,new Point());
            Check(g.save.health<=50.01f,
                "A remote supplied shelter never heals a player standing far outside its true local radius");
        }
        public static void RegionalReportRewardsRequireOriginalPresentSpeaker(Campaign data)
        {
            var g=Fresh(data);
            g.save.stage=3;g.save.flags.Add("exp_intro");g.Enter(5);
            g.enemies.Clear();
            g.save.flags.Add("gate_5");g.save.flags.Add("boss_5");
            var altar=Object(g,"expAltar");
            var captain=Object(g,"captain");
            int coins=g.save.coins;
            // A direct callback that never came from a live dialogue must
            // never perform an official report or recruit a companion.
            var dispatch=typeof(GameCore).GetMethod("ChooseExpansion",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(dispatch!=null,"Real regional action dispatcher exists");
            dispatch.Invoke(g,new object[]{"harbor_report"});
            Check(!g.Has("harbor_report")&&g.save.coins==coins,
                "Unowned report action cannot mint companion or campaign rewards");
            g.save.x=altar.x;g.save.y=altar.y;
            g.Talk(altar);
            g.Choose(StoryChoiceIndex(g,"report_mira"));
            int submit=StoryChoiceIndex(g,"harbor_report");
            g.save.room=6;
            g.Choose(submit);
            Check(!g.Has("harbor_report")&&g.save.coins==coins,
                "An altar menu from another room cannot release a regional reward");
            g.save.room=5;
            g.save.x=captain.x;g.save.y=captain.y;
            g.Talk(captain);
            Check(StoryChoicePresent(g,"harbor_report"),
                "The actual companion still offers their normal valid report action");
            g.Choose(StoryChoiceIndex(g,"harbor_report"));
            Check(g.Has("harbor_report")&&g.save.coins>coins,
                "Authentic face-to-face filing preserves the original companion reward");
        }

        public static void ClearedRegionalAltarsFileRealReportsWithoutBorrowingNpcFaces(Campaign data)
        {
            for(int room=5;room<=7;room++)
            {
                var g=Fresh(data);
                g.save.stage=3;g.save.level=10;
                g.save.flags.Add("intro");
                g.save.flags.Add("exp_intro");
                // Travel progression must be legitimate: the desert only
                // opens after the harbor record, and Frostchime only after
                // the desert record. Do not test a different map by
                // teleporting through a real locked campaign gate.
                if(room>=6)g.save.flags.Add("harbor_report");
                if(room>=7)g.save.flags.Add("desert_report");
                g.Enter(room);
                Check(g.save.room==room,"Actual regional report test enters the intended unlocked map");
                g.enemies.Clear();
                g.save.flags.Add("gate_"+room);
                g.save.flags.Add("boss_"+room);
                if(room==6)g.save.flags.Add("mirrors_lit");
                WorldObject altar=Object(g,"expAltar");
                WorldObject companion=Object(g,room==5?"captain":room==6?"tinker":"keeper");
                g.save.x=altar.x;g.save.y=altar.y;
                Check(g.CanStand(g.Position)&&g.GateOpen,
                    "The actual regional altar is reachable after the guardian falls");
                QuestGuide relayGuide=g.GetQuest("shore");
                Check(relayGuide.room==room &&
                    (relayGuide.target-new Point(altar.x,altar.y)).Length<.01f &&
                    relayGuide.next.Contains("祭坛"),
                    "Real tracked farshore quest guides to the closer field relay after the guardian falls");
                g.save.x=companion.x;g.save.y=companion.y;
                QuestGuide companionGuide=g.GetQuest("shore");
                Check((companionGuide.target-new Point(companion.x,companion.y)).Length<.01f &&
                    companionGuide.next.Contains("同伴"),
                    "Walking to the original companion keeps the personal handoff as a legitimate live option");
                g.save.x=altar.x;g.save.y=altar.y;
                int coins=g.save.coins;
                g.Talk(altar);
                string relay=room==5?"report_mira":room==6?"report_ivo":"report_sen";
                Check(g.conversation!=null&&g.conversation.speaker==altar.name&&
                    StoryChoicePresent(g,relay),
                    "The local altar presents an authored field report choice");
                g.Choose(StoryChoiceIndex(g,relay));
                Check(g.conversation!=null&&g.conversation.speaker==altar.name&&
                    g.conversationSource==altar,
                    "Transmitting the report remains anchored to the real altar, never opens distant companion dialogue");
                string filing=room==5?"harbor_report":room==6?"desert1":"mountain2";
                int first=StoryChoiceIndex(g,filing);
                Check(first>=0,"Actual first-time choice remains present at the altar");
                // Leaving the relay disables its stale button and cannot
                // award a remote report or a fake NPC dialogue.
                g.save.x=altar.x-4;g.save.y=altar.y;
                g.Choose(first);
                Check(!g.Has(room==5?"harbor_report":
                    room==6?"desert_report":"mountain_report")&&g.save.coins==coins,
                    "Stale altar report cannot pay from another position");
                g.save.x=altar.x;g.save.y=altar.y;
                g.Talk(altar);
                g.Choose(StoryChoiceIndex(g,relay));
                g.Choose(StoryChoiceIndex(g,filing));
                string flag=room==5?"harbor_report":room==6?"desert_report":"mountain_report";
                Check(g.Has(flag)&&g.Has("companion_"+(room-5))&&
                    g.save.coins>coins,
                    "Genuine on-site relay completes original regional story and rewards exactly once");
                if(room==6)Check(g.save.desertChoice==1,
                    "Actual oasis-versus-lights decision survives remote civic filing");
                if(room==7)Check(g.save.mountainChoice==2,
                    "Public mountain testimony still selects original memorial outcome");
                int paid=g.save.coins;
                g.Talk(altar);
                Check(!StoryChoicePresent(g,relay)&&g.save.coins==paid,
                    "After filing, the altar cannot generate a second companion reward");
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&restored.Has(flag)&&
                    restored.Has("companion_"+(room-5)),
                    "Existing v2 save schema retains all three genuine regional report outcomes");
            }
        }

        public static void ClearedAbyssFloorReloadRetainsEarnedUnbankedRewards(Campaign data)
        {
            var g=Unlocked(data);
            g.StartAbyss(0);
            Check(g.InAbyss&&g.enemies.Count>0,
                "Physical abyss test starts with a real uncleared enemy roster");
            // This is the genuine post-combat/pre-gate snapshot. Floor
            // rewards and optional caches must not be banked yet.
            g.enemies.Clear();
            g.save.runCoins=48;g.save.runShards=3;g.save.runXp=27;
            int permanentCoins=g.save.coins,permanentShards=g.save.shards;
            g.PrepareSave();
            Check(g.save.abyssSnapshotFloor==1&&g.save.abyssEnemies!=null&&
                g.save.abyssEnemies.Length==0&&!g.Has("abyss_clear_1"),
                "Preparing a v2 save after the final enemy preserves a VALID empty encounter roster before bank");
            var resumed=new GameCore(data,CloneSave(g.save));
            resumed.Start();
            Check(resumed.InAbyss&&resumed.enemies.Count==0&&
                resumed.save.runCoins==48&&resumed.save.runShards==3&&
                resumed.save.runXp==27&&resumed.save.coins==permanentCoins&&
                resumed.save.shards==permanentShards&&
                !resumed.Has("abyss_clear_1"),
                "Reloading an earned empty floor no longer respawns enemies or destroys unbanked exploration");
            WorldObject gate=Array.Find(resumed.Map.objects,
                o=>o.kind=="abyssGate");
            resumed.save.x=gate.x;resumed.save.y=gate.y;
            resumed.Talk(gate);
            Check(resumed.Has("abyss_clear_1")&&resumed.mode=="dialog",
                "The actually cleared, restored floor can still be banked through its existing physical root gate");
            resumed.Close();
            resumed.PrepareSave();
            var twice=new GameCore(data,CloneSave(resumed.save));
            twice.Start();
            Check(twice.enemies.Count==0&&twice.save.runCoins==48&&
                twice.save.runShards==3&&twice.save.runXp==27&&
                twice.save.version==2,
                "Repeated before-extraction saves cannot mint a replacement enemy wave or discard pending rewards");
            var last=Unlocked(data);
            last.StartAbyss(0);
            last.save.abyssFloor=6;last.enemies.Clear();
            last.save.abyssSnapshotFloor=6;
            last.save.abyssEnemies=new EnemyRecord[0];
            last.save.runCoins=73;
            var restoredBoss=new GameCore(data,CloneSave(last.save));
            restoredBoss.Start();
            Check(restoredBoss.enemies.Count==1&&restoredBoss.enemies[0].boss&&
                restoredBoss.save.runCoins==73,
                "An inconsistent empty sixth-floor roster cannot bypass the real Root Reaper; restores ONLY its required guardian");
            last.save.flags.Add("boss_8");
            var defeatedBoss=new GameCore(data,CloneSave(last.save));
            defeatedBoss.Start();
            Check(defeatedBoss.enemies.Count==0&&defeatedBoss.save.runCoins==73,
                "A legitimately defeated sixth-floor guardian does not resurrect on reload");
        }

        public static void RootGateCannotBankDuringRealLingeringMissilesOrRifts(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            WorldObject gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;
            g.save.runCoins=48;g.save.runShards=2;g.save.runXp=20;
            int originalCoins=g.save.coins;
            var threat=new Projectile {
                position=g.Position+new Point(1.3f,0f),
                velocity=new Point(-9f,0f),life=.75f,
                damage=14f,visual="enemyArrow",friendly=false
            };
            g.projectiles.Add(threat);
            Check(!g.SafeTravel,"A live nearby enemy arrow remains physically threatening even after every foe dies");
            g.Talk(gate);
            Check(g.mode=="world"&&!g.Has("abyss_clear_1")&&
                g.save.runCoins==48,
                "Root gate refuses early banking while an actual incoming arrow still occupies the cleared room");
            threat.friendly=true;
            g.Talk(gate);
            Check(g.mode=="dialog"&&g.Has("abyss_clear_1"),
                "A reflected harmless arrow restores legitimate gate access without changing saved loot");
            g.projectiles.Clear();
            var blast=new Hazard { position=g.Position,
                radius=1.5f,timer=.6f,damage=12f,kind="rift" };
            g.hazards.Add(blast);
            Check(!g.SafeTravel&&g.save.runCoins==48,
                "A real rift warning may appear after the root gate menu opens");
            int bank=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action=="abyssBank")bank=i;
            Check(bank>=0,"Live gate still exposes original return-and-bank action");
            g.Choose(bank);
            Check(g.InAbyss&&g.save.abyssFloor==1&&
                g.save.runCoins==48&&g.save.coins==originalCoins,
                "Late incoming rift invalidates stale banking before spending, leaving world and unbanked rewards intact");
            g.hazards.Clear();
            g.Talk(gate);
            int boon=-1;
            for(int i=0;i<g.conversation.choices.Length;i++)
                if(g.conversation.choices[i].action=="boon0")boon=i;
            Check(boon>=0,"After actual warning ends the original onward boon route is usable");
            g.Choose(boon);
            Check(g.InAbyss&&g.save.abyssFloor==2&&
                g.save.coins>=originalCoins+48&&g.save.boons[0]==1&&
                g.save.runCoins==0&&g.save.version==2,
                "Only safe physical clearance permits ordinary banking and an earned next-floor boon");
        }

        public static void AbyssFieldHudMatchesTrueLootGateSafety(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            Check(g.enemies.Count>0&&
                g.Conditions().Contains("击退本层敌人")&&
                !g.Conditions().Contains("地脉门可结算"),
                "Real-time abyss HUD never advertises a bank gate while genuine enemies survive");
            g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();
            g.save.runShards=5;
            Check(g.SafeTravel&&g.Conditions().Contains("地脉门可结算")&&
                g.Conditions().Contains("未结算碎片 5"),
                "A truly clear and safe floor shows usable gate guidance without banking or losing real shard count");
            var danger=new Hazard{position=g.Position,
                radius=1.2f,timer=.8f,damage=16f,kind="rift"};
            g.hazards.Add(danger);
            Check(!g.SafeTravel&&
                g.Conditions().Contains("危险未退")&&
                !g.Conditions().Contains("地脉门可结算"),
                "A newly warned authentic rift immediately retracts false bank-ready HUD guidance");
            g.save.english=true;
            Check(g.Conditions().Contains("LIVE THREAT")&&
                !g.Conditions().Contains("GATE READY"),
                "The same real-danger warning is visible in the English abyss HUD");
            g.hazards.Clear();
            Check(g.SafeTravel&&g.Conditions().Contains("GATE READY")&&
                g.save.runShards==5&&g.save.version==2,
                "When physical danger ends the in-game HUD immediately returns to a real safe settlement state with no save mutation");
        }

        public static void AbyssOptionalInteractionsRequirePhysicalReach(Campaign data)
        {
            var g = new GameCore(data);
            g.save.frontierEnding = 1;
            g.StartAbyss(0);
            WorldObject survey = Array.Find(g.Map.objects, o => o.kind == "abyssSurvey");
            WorldObject farCache = Array.Find(g.Map.objects, o => o.kind == "abyssFarCache");
            WorldObject gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            WorldObject exit = Array.Find(g.Map.objects, o => o.kind == "abyssExit");
            Check(survey != null && farCache != null && gate != null && exit != null,
                "Abyss survey, lower-gallery cache, gate and exit are real world interactions");

            // A cleared floor still needs a physical visit to its gate. Opening
            // its remote dialogue must not set the bankable-clear flag as a side effect.
            g.enemies.Clear();
            g.save.x = 5; g.save.y = 14;
            g.Talk(gate);
            Check(!g.Has("abyss_clear_1") && g.mode == "world" && g.toast.Contains("走近"),
                "The root gate cannot mark a floor cleared before physical arrival");
            g.Talk(exit);
            Check(g.mode == "world" && g.toast.Contains("走近"),
                "The abyss exit cannot open an escape interaction from across the floor");
            TalkNear(g, "abyssGate");
            Check(g.Has("abyss_clear_1") && g.mode == "dialog",
                "A reachable root gate still opens the existing boon and banking choices");
            g.Close();

            int startXp = g.save.runXp;
            Point remoteSurvey = StandableOutsideInteractionReach(g, survey);
            g.save.x = remoteSurvey.x;
            g.save.y = remoteSurvey.y;
            g.Talk(survey);
            Check(!g.Has("abyss_annex_survey_1") && g.save.runXp == startXp &&
                g.mode == "world" && g.toast.Contains("走近"),
                "Abyss survey cannot record or award XP outside physical interaction reach");

            float oldX = farCache.x, oldY = farCache.y;
            Point blockedApproach;
            bool foundBlockedApproach = TryFindWallBlockedApproach(g, farCache,
                out blockedApproach);
            Check(foundBlockedApproach,
                "The generated abyss contains a standable point separated from a gallery target by a wall");
            if (foundBlockedApproach)
            {
                int coins = g.save.runCoins, shards = g.save.runShards;
                g.save.x = blockedApproach.x;
                g.save.y = blockedApproach.y;
                g.Talk(farCache);
                Check(!g.Has("abyss_annex_far_cache_1") &&
                    g.save.runCoins == coins && g.save.runShards == shards &&
                    g.mode == "world" && g.toast.Contains("走近"),
                    "Lower-gallery cache cannot award loot through a blocking wall");
            }
            farCache.x = oldX;
            farCache.y = oldY;

            g.save.x = survey.x;
            g.save.y = survey.y;
            g.Talk(survey);
            Check(g.Has("abyss_annex_survey_1") && g.save.runXp == startXp + 15,
                "A physically reachable abyss survey retains its intended reward");
            g.Close();

            int beforeCoins = g.save.runCoins, beforeShards = g.save.runShards;
            g.save.x = farCache.x;
            g.save.y = farCache.y;
            g.Talk(farCache);
            Check(g.Has("abyss_annex_far_cache_1") &&
                g.save.runCoins == beforeCoins + 30 &&
                g.save.runShards == beforeShards + 1,
                "A physically reachable lower-gallery cache retains its intended reward");
        }

        public static void CivicConduitCannotOverlapActiveFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"gate_4","core_1","core_2","core_3",
                "last_directive_complete","last_directive_open"})
                g.save.flags.Add(flag);
            g.save.inventory[4]=1;
            g.Enter(4);g.enemies.Clear();
            WorldObject master=Object(g,"civicconduitmaster");
            g.save.x=master.x;g.save.y=master.y;
            SetOuterPatrolLull(g,4);
            Check(g.OuterPatrolActive&&g.SafeTravel&&g.LastDirectiveComplete&&
                g.LastDirectiveChoice==1,
                "The western conductor is available during a locally safe patrol lull");
            Action(g,master.kind,"civic_conduit_start");
            Check(!g.CivicConduitActive&&g.save.inventory[4]==1,
                "The timed civic conductor cannot overlap patrol fieldwork or consume its fuse");
            ClearOuterPatrolLull(g);
        }

        public static void LastDirectiveCannotStartDuringActiveFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"gate_4","core_1","core_2","core_3",
                "last_directive_started","last_directive_clue_0",
                "last_directive_clue_1","last_directive_clue_2"})
                g.save.flags.Add(flag);
            g.Enter(4);g.enemies.Clear();
            WorldObject core=Object(g,"lastdirectivecore");
            g.save.x=core.x;g.save.y=core.y;
            SetOuterPatrolLull(g,4);
            Check(g.LastDirectiveReady&&g.OuterPatrolActive&&g.SafeTravel,
                "The directive console is safe locally while another patrol still owns the fieldwork slot");
            Action(g,core.kind,"last_directive_code_0");
            Check(g.LastDirectiveWave==0&&
                !g.enemies.Exists(e=>e.id>=90300&&e.id<=90304),
                "The first directive wave cannot start during an active patrol lull");
            ClearOuterPatrolLull(g);
            Action(g,core.kind,"last_directive_code_0");
            Check(g.LastDirectiveWave==1&&
                g.enemies.FindAll(e=>e.id>=90300&&e.id<=90304).Count==2,
                "The directive's real first wave remains available once the other fieldwork ends");
        }

        public static void RoadwardenDefenseCannotOverlapUnrelatedFieldwork(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=20;
            foreach(string flag in new[]{"dispatch_complete","survey_1"})
                g.save.flags.Add(flag);
            g.Enter(1);g.enemies.Clear();
            WorldObject beacon=Object(g,"survey1");
            g.save.x=beacon.x;g.save.y=beacon.y;
            SetOuterPatrolLull(g,1);
            Check(g.OuterPatrolActive&&g.SafeTravel,
                "The beacon is locally safe during an active patrol lull");
            Action(g,beacon.kind,"road_trial_start_1");
            Check(g.RoadwardenTrialRoom==0&&
                !g.enemies.Exists(e=>e.id>=81000&&e.id<81100),
                "A roadwarden defense cannot spawn a wave over unrelated fieldwork");
            ClearOuterPatrolLull(g);
            Action(g,beacon.kind,"road_trial_start_1");
            Check(g.RoadwardenTrialRoom==1&&
                g.enemies.FindAll(e=>e.id>=81000&&e.id<81100).Count==2,
                "The real two-wave defense remains startable after the competing mission ends");
        }

        public static void RegionalBossesSweepAlternatingLanes(Campaign data)
        {
            var g = Unlocked(data);
            MethodInfo release = typeof(GameCore).GetMethod("BossExpansionAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "Regional Boss attacks must use the live production release");
            foreach (int room in new[] { 5, 6 })
            {
                g.Enter(room);
                var boss = new Enemy(901, 3, 29, 9, 600, true) { target = new Point(20, 12) };
                for (int phase = 1; phase <= 3; phase++)
                for (int cycle = 1; cycle <= 2; cycle++)
                {
                    boss.hp = phase == 1 ? 500 : phase == 2 ? 300 : 100;
                    boss.cycle = cycle;
                    g.hazards.Clear();
                    release.Invoke(g, new object[] { boss });
                    Check(g.hazards.Count == 3, "Regional release must warn all three lane contacts");
                    bool horizontal = room == 5 ? phase == 1 || cycle % 2 == 1
                                                : phase > 1 && cycle % 2 == 0;
                    for (int i = -1; i <= 1; i++)
                    {
                        Hazard h = g.hazards[i + 1];
                        float expectedX = 20 + (horizontal ? i * 2 : 0);
                        float expectedY = 12 + (horizontal ? 0 : i * 2);
                        float expectedDelay = (room == 5 ? 1.2f : .9f) + (phase == 3 ? (i + 1) * .22f : 0);
                        Check(Math.Abs(h.position.x - expectedX) < .001f && Math.Abs(h.position.y - expectedY) < .001f,
                            "The warning lane must rotate around the locked attack target");
                        Check(Math.Abs(h.timer - expectedDelay) < .001f && Math.Abs(h.radius - (room == 5 ? 1.35f : 1.1f)) < .001f,
                            "Each phase uses visible, dodgeable, correctly timed warning circles");
                        Check(h.kind == (room == 5 ? "tide" : "glass") && Math.Abs(h.damage - (room == 5 ? 17 : 18)) < .001f,
                            "Regional hazard identity and damage remain compatible");
                    }
                    Check(phase != 3 || g.hazards[0].timer < g.hazards[1].timer && g.hazards[1].timer < g.hazards[2].timer,
                        "Final-phase sweeps release in a visible sequence");
                }
            }
        }
        public static void FrostPrelateRotatesSafeBellExit(Campaign data)
        {
            var g = Unlocked(data);
            g.Enter(7);
            g.save.x = 20; g.save.y = 12;
            MethodInfo release = typeof(GameCore).GetMethod("BossExpansionAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "Frost Prelate must use the live regional Boss attack");
            var boss = new Enemy(902, 3, 29, 9, 600, true) { target = new Point(15, 8) };
            Point[] directions = { new Point(1, 0), new Point(0, 1), new Point(-1, 0), new Point(0, -1) };
            for (int phase = 1; phase <= 3; phase++)
            for (int cycle = 1; cycle <= 4; cycle++)
            {
                boss.hp = phase == 1 ? 500 : phase == 2 ? 300 : 100;
                boss.cycle = cycle;
                g.hazards.Clear();
                release.Invoke(g, new object[] { boss });
                Check(g.hazards.Count == (phase == 1 ? 1 : 4),
                    "First phase preserves one frost mark; later phases add three bells");
                Hazard mark = g.hazards[0];
                Check(mark.kind == "rift" && Math.Abs(mark.position.x - 15) < .001f &&
                      Math.Abs(mark.position.y - 8) < .001f && Math.Abs(mark.radius - 2) < .001f &&
                      Math.Abs(mark.timer - 1.5f) < .001f && Math.Abs(mark.damage - (20 + phase * 2)) < .001f,
                    "The frost mark keeps its original damage and the locked windup position");
                if (phase == 1) continue;
                int safe = (cycle - 1) % 4, strike = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (i == safe) continue;
                    Hazard bell = g.hazards[++strike];
                    Point expected = boss.target + directions[i] * 3.5f;
                    float delay = 1.75f + (phase == 3 ? (strike - 1) * .22f : 0);
                    Check(Math.Abs(bell.position.x - expected.x) < .001f &&
                          Math.Abs(bell.position.y - expected.y) < .001f,
                        "The unmarked safe exit rotates through all four directions");
                    Check(bell.kind == "bell" && GameCore.HazardTheme(bell) == 7 &&
                          Math.Abs(bell.radius - 1.1f) < .001f &&
                          Math.Abs(bell.timer - delay) < .001f && Math.Abs(bell.damage - 14) < .001f,
                        "Blue bell warnings keep their timing and damage");
                }
                if (phase == 3)
                    Check(g.hazards[1].timer < g.hazards[2].timer &&
                          g.hazards[2].timer < g.hazards[3].timer,
                        "Final phase bell strikes must ring one after another");
            }
        }
        public static void RootReaperClosesRootPincers(Campaign data)
        {
            var g = Unlocked(data);
            g.StartAbyss(0);
            g.save.abyssFloor = 6;
            g.save.x = 29; g.save.y = 9;
            MethodInfo release = typeof(GameCore).GetMethod("BossExpansionAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(release != null, "Root Reaper uses the production Boss attack release");
            var boss = new Enemy(903, 3, 29, 9, 680, true) { target = new Point(29, 9) };
            for (int risk = 0; risk <= 2; risk++)
            for (int phase = 1; phase <= 3; phase++)
            for (int cycle = 1; cycle <= 2; cycle++)
            {
                g.save.abyssRisk = risk;
                boss.hp = phase == 1 ? 680 : phase == 2 ? 340 : 120;
                boss.cycle = cycle;
                g.hazards.Clear();
                release.Invoke(g, new object[] { boss });
                Check(g.hazards.Count == (phase == 1 ? 1 : phase == 2 ? 3 : 5),
                    "The Reaper gains two outer roots and then two inward roots");
                Hazard mark = g.hazards[0];
                Check(mark.kind == "rift" && Math.Abs(mark.timer - 1.5f) < .001f &&
                      Math.Abs(mark.radius - 2) < .001f && Math.Abs(mark.damage - (20 + phase * 2)) < .001f,
                    "The existing abyss mark is preserved");
                if (phase == 1) continue;
                Point axis = cycle % 2 == 1 ? new Point(1, 0) : new Point(0, 1);
                int index = 1;
                for (int side = -1; side <= 1; side += 2)
                {
                    Point outer = new Point(29, 9) + axis * (side * 2.8f);
                    Hazard h = g.hazards[index++];
                    Check(h.kind == "root" && GameCore.HazardTheme(h) == 8 &&
                          Math.Abs(h.position.x - outer.x) < .001f && Math.Abs(h.position.y - outer.y) < .001f &&
                          Math.Abs(h.radius - 1.05f) < .001f && Math.Abs(h.timer - 1.85f) < .001f &&
                          Math.Abs(h.damage - (16 + risk * 3)) < .001f,
                        "Outer roots alternate axes with a warning and risk-scaled damage");
                    if (phase == 3)
                    {
                        Point inner = new Point(29, 9) + axis * (side * 1.5f);
                        Hazard h2 = g.hazards[index++];
                        Check(h2.kind == "root" && Math.Abs(h2.position.x - inner.x) < .001f &&
                              Math.Abs(h2.position.y - inner.y) < .001f &&
                              Math.Abs(h2.radius - .9f) < .001f && Math.Abs(h2.timer - 2.25f) < .001f &&
                              Math.Abs(h2.damage - (16 + risk * 3)) < .001f,
                            "Final-phase roots close inward after the first warning");
                    }
                }
            }
            // Moving the hero after a marked cast must not move the
            // Root Reaper's original collapse footprint.
            g.save.abyssFloor = 6;
            g.save.x = 35; g.save.y = 15;
            boss.target = new Point(29, 9);
            boss.hp = 120; boss.cycle = 1;
            g.hazards.Clear();
            release.Invoke(g, new object[] { boss });
            Check(g.hazards.Count == 5 &&
                Math.Abs(g.hazards[0].position.x - 29) < .001f &&
                Math.Abs(g.hazards[0].position.y - 9) < .001f &&
                Math.Abs(g.hazards[1].position.x - 26.2f) < .01f,
                "Moving away during the cast cannot secretly pull the Root Reaper's marked pincer to a new foot");
            g.save.abyssFloor = 5;
            boss.hp = 120;
            g.hazards.Clear();
            release.Invoke(g, new object[] { boss });
            Check(g.hazards.Count == 1, "Other floors never spawn final Boss root pincers");
        }
        public static void MigrationAndBuilds(Campaign data)
        {
            var old = new SaveData { version = 1, stage = 1, room = 1, x = 5, y = 14, coins = 91, health = 72, kills = new[] { 0, 6, 0, 0, 0 }, flags = new List<string> { "intro", "scout_saved", "core_1", "lore_1" } };
            var g = new GameCore(data, old);
            Check(g.save.version == 2 && g.save.coins == 91 && g.save.kills[1] == 6 && g.save.kills.Length == data.maps.Length, "Old saves preserve progress and extend kill counters");
            Check(g.Has("lore_1") && g.save.companion == -1 && g.save.relics[0] == -1, "Migration must not invent companions or relics");
            g.save.shards = 60; int before = g.save.shards; Check(!g.RefineRelics() && g.save.shards == before, "Refining needs the town workshop");
            g.Enter(0); g.save.coins = 500; for (int i = 0; i < 5; i++) g.save.inventory[i] = 100;
            Check(g.BuildProject(2), "Workshop builds in town"); Check(g.RefineRelics() && g.save.relicRank == 1 && g.save.shards == 52, "Refining has a real shard cost");
            g.save.flags.Add("relic_0"); g.save.flags.Add("relic_5"); float damage = g.Damage;
            g.EquipRelic(0, 0); Check(g.Damage == damage + 2, "Forging increases damage only while a relic is equipped");
            g.EquipRelic(1, 0); Check(g.save.relics[0] == -1 && g.save.relics[1] == 0, "A relic cannot occupy both slots");
            g.EquipRelic(0, 2); Check(g.save.relics[0] == -1, "Unknown relics cannot be equipped");
            g.save.flags.Add("technique_0"); g.save.level = 5; g.save.skillPoints = 1; g.LearnTechnique(); Check(g.save.technique == 1, "Learned technique can be switched");
            g.save.flags.Add("companion_0"); g.SelectCompanion(0); g.save.health = 20; g.mode = "world"; g.CommandCompanion();
            Check(g.save.health == 20 && g.companionCooldown > 0 && g.companionAction != null, "Companion command starts preparation before its effect");
            Advance(g, .22f); Check(g.save.health > 20, "Companion command has its original gameplay effect on release");
            float hp = g.save.health; g.CommandCompanion(); Check(g.save.health == hp, "Companion command respects cooldown");
            var bad = CloneSave(g.save); bad.relics = new[] { 5, 5 }; bad.heat = float.NaN; bad.cold = float.PositiveInfinity; bad.projects = null; bad.boons = new[] { 500, -20, 30 }; bad.flags.Add(null);
            g = new GameCore(data, bad); Check(g.save.relics[1] == -1 && g.save.heat == 0 && g.save.cold == 0 && g.save.projects.Length == 3 && !g.save.flags.Contains(null), "Expansion save fields are sanitized");
        }
        private static HashSet<int> Reachable(GameCore g)
        {
            int width = g.Map.rows[0].Length, height = g.Map.rows.Length;
            var queue = new Queue<int>(); var seen = new HashSet<int>(); int start = (int)g.save.y * width + (int)g.save.x;
            queue.Enqueue(start); seen.Add(start); int[,] steps = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(), x = cell % width, y = cell / width;
                for (int i = 0; i < 4; i++)
                {
                    int xx = x + steps[i, 0], yy = y + steps[i, 1], next = yy * width + xx;
                    if (xx < 0 || yy < 0 || xx >= width || yy >= height || g.Blocked(xx + .5f, yy + .5f) || seen.Contains(next)) continue;
                    seen.Add(next); queue.Enqueue(next);
                }
            }
            return seen;
        }
        private static void AssertObjectsReachable(GameCore g)
        {
            var seen = Reachable(g); int width = g.Map.rows[0].Length;
            foreach (WorldObject o in g.Map.objects)
            {
                bool near = false; foreach (int cell in seen) if ((new Point(cell % width + .5f, cell / width + .5f) - new Point(o.x, o.y)).Length < 1.7f) { near = true; break; }
                Check(near, "Unreachable distant object in room " + g.save.room + ": " + o.id);
            }
        }
        public static void CompanionPartySwapStartsFromPlantedOriginalGait(Campaign data)
        {
            var g = Unlocked(data);
            g.SelectCompanion(0);
            Check(g.save.companion == 0,
                "Unlocked starting follower must be selectable");
            g.companionMoveTime = 4.25f;
            g.companionMoving = true;
            var pending = new ActorAction {
                kind = "command", weapon = 0, duration = .42f,
                contact = .16f, direction = new Point(1,0)
            };
            g.companionAction = pending;
            int firstGeneration=g.companionPresentationGeneration;
            g.SelectCompanion(0); // Existing command must never be canceled.
            Check(g.companionAction == pending &&
                Math.Abs(g.companionMoveTime-4.25f)<.00001f &&
                g.companionMoving &&
                g.companionPresentationGeneration==firstGeneration,
                "Reselecting the SAME ally may not rewind feet, invalidate pose or cancel commands");
            g.SelectCompanion(1);
            Check(g.save.companion == 1 &&
                g.companionPresentationGeneration==firstGeneration+1 &&
                g.companionAction == null && !g.companionMoving &&
                g.companionMoveTime == 0f &&
                g.companionAim.Length > .9f,
                "A real follower swap must not inherit the old outfit's airborne stride");
            g.companionMoveTime = 2.4f;
            g.companionMoving = true;
            g.SelectCompanion(-1);
            Check(g.save.companion == -1 &&
                g.companionPresentationGeneration==firstGeneration+2 &&
                !g.companionMoving && g.companionMoveTime == 0f,
                "Dismissed follower may not retain phantom walking animation");
            // A same-ID return separated by an unseen dismissal must not
            // inherit the old movement phase merely because the name matches.
            g.SelectCompanion(0);
            Check(g.save.companion == 0 &&
                g.companionPresentationGeneration==firstGeneration+3 &&
                g.companionMoveTime==0f,
                "Recruited-again original companion requires a fresh presentation generation");
        }

        public static void DistantReachability(Campaign data)
        {
            var g = Unlocked(data);
            for (int room = 5; room <= 7; room++)
            {
                g.Enter(room); if (room == 5) g.save.tideHeldUntil = g.save.totalTime + 90;
                AssertObjectsReachable(g);
                foreach (Enemy e in g.enemies) Check(!g.Blocked(e.x, e.y), "Enemy spawned inside blocked terrain");
            }
            g.Enter(5); g.enemies.Clear(); g.save.tideHeldUntil = 0; g.save.totalTime = 21.99f; g.save.x = 5.5f; g.save.y = 8.5f;
            g.Tick(.02f, new Point()); Check(!g.Blocked(g.save.x, g.save.y), "Rising tide retreats safely to a bank");
            g.Enter(0);
            for (int seed = 1; seed <= 4; seed++)
            {
                g.StartAbyss(0); g.save.abyssSeed = seed; g.save.abyssFloor = seed; g = new GameCore(data, CloneSave(g.save)); g.Start();
                AssertObjectsReachable(g);
                string layout = string.Join("", g.Map.rows); var restored = new GameCore(data, CloneSave(g.save));
                Check(string.Join("", restored.Map.rows) == layout, "Seeded dungeon reconstructs the same map");
                g.ReturnToTown(); g.Choose(0);
            }
            g.StartAbyss(0); Enemy target = g.enemies[0]; g.save.x = target.x; g.save.y = target.y; g.Skill(); g.PrepareSave();
            int remaining = g.enemies.Count; var hpById = new Dictionary<int, float>(); foreach (Enemy e in g.enemies) hpById[e.id] = e.hp;
            var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start();
            Check(resumed.enemies.Count == remaining && resumed.save.runCoins == g.save.runCoins, "Partial floor resumes without respawning defeated enemies or resetting pending loot");
            foreach (Enemy e in resumed.enemies) Check(hpById.ContainsKey(e.id) && Math.Abs(e.hp - hpById[e.id]) < .01f, "Enemy health survives the checkpoint");
        }
        public static void TechniquesAndTradeoffs(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Fresh(data, profession); g.save.level = 5; g.save.skillPoints = 1; g.LearnTechnique();
                Check(g.save.technique == 1 && g.save.skillPoints == 0, "New specialization spends a real point");
                g.Enter(1); g.enemies.Clear(); g.save.x = 5; g.save.y = 14; g.aim = new Point(1, 0);
                g.enemies.Add(new Enemy(20, 0, 8, 14, 300)); g.enemies.Add(new Enemy(21, 0, 10, 14, 300));
                g.projectiles.Add(new Projectile { position = new Point(6, 14), velocity = new Point(), damage = 10 });
                g.Skill();
                Advance(g, .3f);
                if (profession == 0) Check(g.enemies[0].hp < 300 && g.projectiles.Count == 0 && g.guardTime > 3.8f, "Vanguard counter staggers and clears shots");
                for (int i = 0; i < 100; i++) g.Tick(.02f, new Point());
                if (profession == 1) Check(g.enemies[0].hp < 300 && g.enemies[1].hp < 300, "Woodpiercer hits multiple targets on one trajectory");
                if (profession == 2) Check(g.enemies[0].hp < 300 && g.effects.Exists(e => e.kind == "frost"), "Frost specialization pulses real damage and control");
            }
            var rpg = Fresh(data); rpg.save.flags.Add("relic_2"); float speed = rpg.Speed; rpg.EquipRelic(0, 2); rpg.Dash(new Point(1, 0));
            Check(rpg.Speed > speed && rpg.save.energy == 75, "Wind Compass trades extra energy for speed");
            rpg.save.flags.Add("companion_0"); rpg.save.inventory[6] = 2; rpg.CompanionStory(0);
            int gift = -1; for (int i = 0; i < rpg.conversation.choices.Length; i++) if (rpg.conversation.choices[i].action == "gift0") gift = i;
            rpg.Choose(gift); rpg.CompanionStory(0); rpg.Choose(gift); Check(rpg.save.inventory[6] == 1 && rpg.save.relationships[0] == 1, "A shared meal builds a bond only once per day");
        }
        public static void ClimatePulsesKeepTheirRealWorldSecondAtVariableFPS(Campaign data)
        {
            // A stable unshaded desert exposure already exists at this
            // playable coordinate. Drive the entire real world Tick path;
            // no fake timer call or manual damage application.
            foreach(float dt in new[]{.05f,.027f})
            {
                var g=Unlocked(data);g.Enter(6);g.enemies.Clear();
                g.save.x=14;g.save.y=10;
                g.save.teaUntil=0;
                g.save.health=g.save.maxHealth=1000;
                g.save.heat=100;
                g.save.reducedMotion=true;
                int frames=dt>.04f?427:791;
                for(int frame=0;frame<frames;frame++)
                    g.Tick(dt,new Point());
                float elapsed=frames*dt;
                float expected=1f-(elapsed%1f);
                var timer=typeof(GameCore).GetField("climateTimer",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                float remaining=(float)timer.GetValue(g);
                Check(Math.Abs(remaining-expected)<.015f,
                    "Heat pulse remaining time must conserve every frame's fractional overshoot at "+dt);
                Check(Math.Abs(g.save.health-916f)<.001f,
                    "Unprotected desert must apply exactly 21 physical heat contacts in 21.35s at "+dt);
            }
        }
        public static void AbyssRiftsKeepTheirSixSecondWorldClock(Campaign data)
        {
            foreach(float dt in new[]{.05f,.027f})
            {
                var g=Unlocked(data);g.StartAbyss(0);
                Check(g.InAbyss,"Test needs the actual running abyss dungeon");
                g.enemies.Clear();g.hazards.Clear();g.effects.Clear();
                // Test the real floor-three storm rather than creating
                // fake hazards. Floor/seed changes are test-local only.
                g.save.abyssFloor=3;g.save.health=g.save.maxHealth=1000;
                g.save.reducedMotion=true;
                var seen=new HashSet<Hazard>();
                int frames=dt>.04f?817:1513;
                for(int frame=0;frame<frames;frame++)
                {
                    g.Tick(dt,new Point());
                    foreach(Hazard h in g.hazards)
                        if(h.kind=="rift")seen.Add(h);
                }
                float elapsed=frames*dt;
                float expected=6f-(elapsed%6f);
                var timer=typeof(GameCore).GetField("stormTimer",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                float remaining=(float)timer.GetValue(g);
                Check(seen.Count==6,
                    "Deep abyss must schedule six actual rift warnings over forty seconds at "+dt);
                Check(Math.Abs(remaining-expected)<.02f,
                    "Deep abyss must conserve fractional six-second storm boundaries at "+dt);
            }
        }

        public static void RiftWarningRetainsItsFullVisibleDodgeWindow(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            Check(g.InAbyss,"Rift warning test must run in the real abyss");
            g.enemies.Clear();g.hazards.Clear();g.effects.Clear();
            g.save.abyssFloor=3;
            g.save.health=g.save.maxHealth=9999;
            g.save.reducedMotion=true;
            Hazard warning=null;
            for(int frame=0;frame<130 && warning==null;frame++)
            {
                g.Tick(.05f,new Point());
                warning=g.hazards.Find(h=>h.kind=="rift");
            }
            Check(warning!=null&&
                Math.Abs(warning.timer-1.4f)<.001f,
                "New abyss rift must give its full 1.4s visible warning after spawning, not immediately lose a frame");
            float health=g.save.health;
            for(int frame=0;frame<27;frame++)
                g.Tick(.05f,new Point());
            Check(g.hazards.Contains(warning)&&
                Math.Abs(g.save.health-health)<.001f,
                "A stationary hero must not take rift damage during the announced reaction window");
            g.Tick(.05f,new Point());g.Tick(.02f,new Point());
            Check(!g.hazards.Contains(warning)&&
                g.effects.Exists(e=>e.kind=="danger"&&
                    Math.Abs(e.radius-1.4f)<.001f)&&
                g.save.health<health,
                "The exact same real rift must finally release one dangerous contact after its warning expires");
        }

        public static void ThermalDangerFeedbackRequiresRealExposureAndDamage(Campaign data)
        {
            var g=Unlocked(data);g.Enter(6);g.enemies.Clear();
            g.save.x=14;g.save.y=10;g.save.teaUntil=0;
            g.save.heat=84.99f;g.save.cold=0;
            g.save.english=true;
            g.save.reducedMotion=true;
            g.save.health=g.save.maxHealth=1000;
            g.Tick(.05f,new Point());
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.text=="HEAT DANGER")&&
                g.toast.Contains("Heat danger"),
                "A first genuine desert overheat crossing needs visible and actionable English feedback");
            g.Tick(.05f,new Point());
            Check(g.effects.FindAll(e=>e.kind=="number"&&
                e.text=="HEAT DANGER").Count==1,
                "Sustained desert exposure cannot spam warnings every frame");
            g.save.heat=79.5f;g.Tick(.05f,new Point());
            g.save.heat=84.99f;g.Tick(.05f,new Point());
            Check(g.effects.FindAll(e=>e.kind=="number"&&
                e.text=="HEAT DANGER").Count==2,
                "Cooling below 80 and heating back to 85 must genuinely re-arm the single crossing alert");
            var timer=typeof(GameCore).GetField("climateTimer",
                BindingFlags.NonPublic|BindingFlags.Instance);
            timer.SetValue(g,.02f);g.invulnerable=0;
            float hp=g.save.health;
            g.Tick(.05f,new Point());
            Check(g.save.health<hp &&
                g.effects.FindAll(e=>e.kind=="number"&&
                    e.text=="BURN").Count==1,
                "Only actual desert HP damage makes the authored BURN contact label");
            hp=g.save.health;
            timer.SetValue(g,.02f);
            g.Tick(.05f,new Point());
            Check(Math.Abs(g.save.health-hp)<.001f &&
                g.effects.FindAll(e=>e.kind=="number"&&
                    e.text=="BURN").Count==1,
                "I-frame protected climate pulses cannot show phantom second damage numbers");

            g.Enter(7);g.enemies.Clear();
            g.save.x=17;g.save.y=14;
            g.save.teaUntil=0;g.save.cold=89.99f;
            g.save.heat=0;g.invulnerable=0;
            g.Tick(.05f,new Point());
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.text=="FROST DANGER") &&
                g.toast.Contains("Freezing danger"),
                "A real Frostchime cold threshold needs biome-specific shelter guidance");
            timer.SetValue(g,.02f);g.invulnerable=0;
            hp=g.save.health;
            g.Tick(.05f,new Point());
            Check(g.save.health<hp &&
                g.effects.Exists(e=>e.kind=="number"&&
                    e.text=="FROST"),
                "Actual frozen-region HP loss displays frost injury, not the desert burn");
        }

        public static void ClimateAndContracts(Campaign data)
        {
            var g = Unlocked(data); g.Enter(6); g.enemies.Clear(); g.save.x = 14; g.save.y = 10;
            for (int i = 0; i < 1450; i++) g.Tick(.05f, new Point());
            Check(g.save.heat > 80 && g.save.health < g.save.maxHealth, "Unprotected desert exposure is hazardous");
            g.save.tea = 1; g.DrinkTea(); Check(g.save.heat == 0 && g.save.tea == 0 && g.save.teaUntil > g.save.totalTime, "Tea has a real climate effect");
            for (int i = 0; i < 100; i++) g.Tick(.05f, new Point()); Check(g.save.heat == 0, "Tea protects during its buff");
            g.Enter(7); g.save.teaUntil = 0; g.enemies.Clear(); g.save.x = 17; g.save.y = 14;
            for (int i = 0; i < 1400; i++) g.Tick(.05f, new Point()); Check(g.save.cold > 70, "Cold accumulates outside shelter");
            g.save.inventory[0] = 2; Action(g, "brazier", "warm:brazier0"); g.save.x = 6; g.save.y = 14;
            for (int i = 0; i < 200; i++) g.Tick(.05f, new Point()); Check(g.save.cold < 5, "Lit braziers warm the player");
            g.Enter(0); g.AcceptBounty(2); Check(!g.ClaimBounty(), "Contract cannot pay before progress");
            for (int fish = 0; fish < 3; fish++)
            {
                Action(g, "fish", "fish");
                for (int hit = 0; hit < 3; hit++)
                {
                    // The contract now requires three real, spaced bites;
                    // assigning the old center cursor cannot counterfeit one.
                    ReachFishingBite(g,true);
                    g.FishHit();
                }
            }
            Check(g.save.bountyProgress == 3, "Actual catches advance the contract"); int coins = g.save.coins; Check(g.ClaimBounty() && g.save.coins == coins + 35, "Completed contract pays once");
            Check(!g.ClaimBounty(), "Claim cannot be repeated");
            g.Enter(6); g.enemies.Clear(); g.save.mirrors = new[] { 1, 2, 1 }; g.save.x = 14; g.save.y = 18;
            RunRace(g); Check(g.Has("race_finished") && g.save.bestRace > 0 && !g.racing, "All race checkpoints finish via movement");
            int shards = g.save.shards; coins = g.save.coins; RunRace(g); Check(g.save.shards == shards && g.save.coins == coins, "Race rewards cannot be farmed by replaying");
        }
        private static void RunRace(GameCore g)
        {
            g.save.x = 14; g.save.y = 18; Action(g, "skiff", "race"); int frames = 0;
            while (g.racing && frames++ < 4000)
            { Point toward = g.DirectionTo(g.Position, GameCore.RaceRoute[g.raceCheckpoint]); g.Dash(toward); g.Tick(.02f, toward); }
            Check(frames < 4000 && g.raceCheckpoint == GameCore.RaceRoute.Length, "Race route is completable within its timer");
        }
        private static GameCore ShoreRun(Campaign data, int profession, int pledge, int covenant)
        {
            var g = Fresh(data, profession);
            g.Enter(5); Check(g.save.room == 0, "The distant campaign needs the first starcore");
            g.Enter(1); Combat(g, false); Action(g, "scout", "scout"); TalkNear(g, "lore"); g.Close(); Gather(g);
            AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "shrine", "boss"); Combat(g, true); Action(g, "shrine", "goto0"); Action(g, "guild", "deliver");
            Action(g, "envoy", "exp_begin"); Check(g.save.room == 5, "Envoy starts the expansion after chapter one"); Gather(g); Action(g, "captain", "pledge" + pledge);
            for (int i = 0; i < 3; i++) { Action(g, "relay" + i, "relay" + i); Action(g, "castaway" + i, "rescue" + i); }
            Check(g.GateOpen, "Every harbor priority reaches the lighthouse");
            TalkNear(g, "manifest"); g.Close(); Action(g, "sluice", "holdtide"); Combat(g, false); g.save.x = 28; g.save.y = 10;
            AwaitArenaOpening(g); Action(g, "expAltar", "expboss"); g.save.x = 28; g.save.y = 10; Combat(g, true); Action(g, "captain", "harbor_report"); Check(g.Has("companion_0") && g.Has("relic_0"), "Harbor report has permanent rewards");
            Action(g, "captain", "personal0"); Check(g.Has("personal_0"), "Mira's story uses the discovered manifest");
            g.Enter(6); Gather(g); Action(g, "campfire", "tea"); g.DrinkTea();
            TalkNear(g, "mirror0"); TalkNear(g, "mirror1"); Check(!g.GateOpen, "A partial light route cannot open the gate");
            TalkNear(g, "mirror1"); TalkNear(g, "mirror2"); Check(g.GateOpen && g.LightRays().Count == 4, "A connected beam opens the glass gate");
            Combat(g, false); AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "expAltar", "expboss"); g.save.x = 28; g.save.y = 10; Combat(g, true); Action(g, "tinker", "desert" + (profession == 1 ? 2 : 1));
            Check(g.Has("companion_1") && g.CanEnter(7), "Desert decision recruits Ivo and opens the highlands"); Action(g, "tinker", "personal1"); Check(g.Has("personal_1"), "Ivo's story repairs a real machine");
            g.Enter(7); Gather(g); Action(g, "campfire", "tea"); g.DrinkTea(); TalkNear(g, "bell0"); TalkNear(g, "bell0"); TalkNear(g, "bell2");
            Check(g.GateOpen, "The three tone settings open Frostchime"); Combat(g, false); AwaitArenaOpening(g); g.save.x = 28; g.save.y = 10; Action(g, "expAltar", "expboss"); g.save.x = 28; g.save.y = 10; Combat(g, true);
            Action(g, "keeper", "mountain" + (profession == 0 ? 1 : 2)); Action(g, "keeper", "personal2"); Check(g.Proofs == 3 && g.Has("personal_2") && g.Has("relic_4"), "Three reports and personal arcs complete");
            g.Enter(0); Check(g.BuildProject(0) && g.BuildProject(0) && g.BuildProject(1), "Recovered resources can fund the shared-rule requirements");
            Check(g.CouncilPactReady, "All factions and projects support shared governance");
            g.Enter(5); Action(g, "council", "covenant" + covenant); Check(g.save.frontierEnding == covenant && g.CanEnter(8), "Each covenant opens the abyss");
            return g;
        }
        private static void Gather(GameCore g)
        {
            foreach (WorldObject o in g.Map.objects)
                if (o.kind == "wood" || o.kind == "ore" || o.kind == "herb")
                    TalkNearObject(g, o);
        }
        public static void DistantCampaign(Campaign data)
        { for (int i = 0; i < 3; i++) ShoreRun(data, i, i + 1, i + 1); }

        public static void ExpandedRootAbyssHasRealDetoursAndUnbankedRewards(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.frontierEnding=1;
            g.StartAbyss(0);
            Check(g.InAbyss&&g.Map.rows.Length==44&&g.Map.rows[0].Length==66,
                "Every newly generated abyss floor has expanded playable galleries");
            WorldObject cache=null,survey=null;
            foreach(WorldObject o in g.Map.objects)
            {
                if(o.kind=="abyssCache")cache=o;
                if(o.kind=="abyssSurvey")survey=o;
            }
            Check(cache!=null&&survey!=null&&g.CanStand(new Point(cache.x,cache.y))&&
                g.CanStand(new Point(survey.x,survey.y)),
                "Both real annex points are reachable standable world objects");
            int startCoins=g.save.coins,startShards=g.save.shards;
            g.save.x=survey.x;g.save.y=survey.y;
            int startRunXp=g.save.runXp;g.Talk(survey);
            Check(g.save.runXp==startRunXp+15,"Survey awards unbanked experience only");
            g.Close();g.Talk(survey);g.Close();
            Check(g.save.runXp==startRunXp+15,"Abyss survey cannot mint XP twice");
            Check(!g.Explored(8,cache.x,cache.y),
                "Procedural annex begins unknown before visiting the side corridor");
            g.save.x=cache.x;g.save.y=cache.y;
            g.Tick(.02f,new Point());
            Check(g.Explored(8,cache.x,cache.y)&&!g.Explored(8,12,28),
                "Abyss fog tracks actual distant rooms separately");
            int runCoins=g.save.runCoins,runShards=g.save.runShards;
            g.Talk(cache);
            Check(g.save.runCoins==runCoins+18&&g.save.runShards==runShards+2&&
                g.save.coins==startCoins&&g.save.shards==startShards,
                "New cache follows the actual expedition banking rules");
            g.Close();
            GameCore reloaded=new GameCore(data,g.save);
            Check(reloaded.InAbyss&&reloaded.save.version==2&&
                reloaded.Map.rows.Length==44&&reloaded.Map.rows[0].Length==66&&
                reloaded.Has("abyss_annex_cache_1")&&
                reloaded.Explored(8,cache.x,cache.y)&&reloaded.save.runCoins==g.save.runCoins,
                "Legacy v2 mid-expedition save restores expanded geometry and pending loot");
            reloaded.StartAbyss(1);
            Check(reloaded.save.runCoins==g.save.runCoins,
                "Cannot restart another expedition while already in the abyss");
        }

        public static void ExpandedLowerAbyssGalleriesHaveRealBandedLootAndSafeV2Reload(Campaign data)
        {
            GameCore g=new GameCore(data);
            g.save.frontierEnding=1;g.StartAbyss(0);
            Check(g.InAbyss&&g.Map.rows.Length==44&&g.Map.rows[0].Length==66,
                "Procedural abyss expands to 66x44 without moving its original boss route");
            var cells=Reachable(g);int width=g.Map.rows[0].Length;
            WorldObject survey=Array.Find(g.Map.objects,o=>o.kind=="abyssFarSurvey");
            WorldObject cache=Array.Find(g.Map.objects,o=>o.kind=="abyssFarCache");
            WorldObject echo=Array.Find(g.Map.objects,o=>o.kind=="abyssFarEcho");
            Check(survey!=null&&cache!=null&&echo!=null,
                "Abyss has three actual new optional lower-gallery interactions");
            foreach(WorldObject point in new[]{survey,cache,echo})
                Check(cells.Contains((int)point.y*width+(int)point.x)&&
                    g.CanStand(new Point(point.x,point.y)),
                    "Real abyss collisions allow the hero to reach every new gallery");
            int coins=g.save.coins,shards=g.save.shards,oldXP=g.save.runXp;
            g.save.x=echo.x;g.save.y=echo.y;g.Talk(echo);g.Close();
            Check(g.save.runXp==oldXP&&
                !g.Has("abyss_annex_far_echo_1"),
                "Echo cannot be opened without actually finding lower testimony");
            g.save.x=survey.x;g.save.y=survey.y;g.Talk(survey);g.Close();
            Check(g.save.runXp==oldXP+25&&
                g.Has("abyss_annex_far_survey_1"),"Physical testimony grants pending XP");
            g.Talk(survey);g.Close();
            Check(g.save.runXp==oldXP+25,"Reading the same tablet never duplicates loot");
            g.save.x=cache.x;g.save.y=cache.y;g.Talk(cache);g.Close();
            Check(g.save.runCoins>=30&&g.save.runShards>=1&&
                g.save.coins==coins&&g.save.shards==shards,
                "Lower gallery supplies follow existing unbanked expedition rules");
            g.save.x=echo.x;g.save.y=echo.y;g.Talk(echo);g.Close();
            Check(g.save.runXp==oldXP+45,"Echo grants pending XP only after prior physical survey");
            g.PrepareSave();
            GameCore restored=new GameCore(data,g.save);
            Check(restored.InAbyss&&restored.save.version==2&&
                restored.Map.rows.Length==44&&restored.Map.rows[0].Length==66&&
                restored.Has("abyss_annex_far_survey_1")&&
                restored.Has("abyss_annex_far_echo_1")&&
                restored.save.runXp==oldXP+45,
                "Mid-floor old-format save restores new gallery geometry and pending loot");
        }

        public static void AbyssWardExtractionAndReentryCannotMintCharter(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            g.save.abyssFloor = 6; regenerate.Invoke(g, null);
            for (int floor = 3; floor <= 6; floor++)
                g.save.flags.Add("abyss_annex_ward_" + floor);
            g.enemies.Clear();
            g.save.runShards = 9;
            g.save.x = 3; g.save.y = 14;
            WorldObject exit = Array.Find(g.Map.objects, o => o.kind == "abyssExit");
            int skills = g.save.skillPoints, shards = g.save.shards;
            g.Talk(exit);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 2,
                "Early-extraction fixture opens the real leave-or-stay dialogue");
            g.Choose(0);
            Check(!g.InAbyss && !g.Has("abyss_ward_charter_complete") &&
                g.save.skillPoints == skills && g.save.shards == shards &&
                g.save.runShards == 0,
                "Leaving a perfect-but-UNBANKED sixth floor forfeits its charter reward");

            var restored = Unlocked(data); restored.StartAbyss(1);
            restored.save.abyssFloor = 3;
            regenerate.Invoke(restored, null);
            WorldObject ward = Array.Find(restored.Map.objects,
                o => o.kind == "abyssRiftWard");
            restored.enemies.Clear(); restored.save.x = ward.x; restored.save.y = ward.y;
            restored.save.runShards = 3;
            restored.Talk(ward); restored.Choose(0);
            Check(restored.AbyssWardActive, "The physical ward was purchased before saving");
            // Clearing the floor preserves legitimate unbanked state when
            // reconstructing this v2 snapshot without an enemy roster.
            restored.save.flags.Add("abyss_clear_3");
            restored.PrepareSave();
            var reloaded = new GameCore(data, CloneSave(restored.save));
            var field = typeof(GameCore).GetField("stormTimer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(reloaded.AbyssWardActive &&
                Math.Abs((float)field.GetValue(reloaded) - 10f) < .001f,
                "Reloaded ward initializes its own TEN-second clock, not an unwarded six");
            // A newly constructed core still owns the title screen. The
            // loaded scene enters world mode before its simulation ticks.
            reloaded.mode = "world";
            reloaded.hazards.Clear(); reloaded.effects.Clear();
            reloaded.save.flags.Remove("abyss_clear_3");
            reloaded.save.health = reloaded.save.maxHealth = 9999;
            reloaded.save.reducedMotion = true;
            for (int i = 0; i < 130; i++) reloaded.Tick(.05f, new Point());
            Check(reloaded.hazards.Find(h => h.kind == "rift") == null,
                "No too-early rift is generated after the loaded six-second mark");
            for (int i = 0; i < 80; i++) reloaded.Tick(.05f, new Point());
            Check(reloaded.hazards.Find(h => h.kind == "rift") != null,
                "The ward resumes real rift spawning after ten seconds on reload");

            var gate = Unlocked(data); gate.StartAbyss(0);
            gate.save.abyssFloor = 6; regenerate.Invoke(gate, null);
            gate.enemies.Clear(); gate.save.x = 31; gate.save.y = 14;
            WorldObject finalGate = Array.Find(gate.Map.objects,
                o => o.kind == "abyssGate");
            gate.Talk(finalGate);
            Check(gate.mode == "dialog" &&
                gate.conversation.text.Contains("\n\n镇流巡路") &&
                gate.conversation.textEn.Contains("\n\nWard charter"),
                "Ward charter progress renders as real separate paragraphs in both languages");
        }

        public static void AbyssFourFloorWardCharterPaysOnlyOnLegitimateFinalBank(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            // Model a replaying adventurer: isolate the NEW charter payout
            // from the older first-time six-floor completion award.
            g.save.flags.Add("abyss_complete");
            g.save.abyssFloor = 3;
            regenerate.Invoke(g, null);
            int beforeSkills = g.save.skillPoints, beforeShards = g.save.shards;
            for (int floor = 3; floor <= 6; floor++)
            {
                Check(g.InAbyss && g.save.abyssFloor == floor,
                    "Each charter leg begins in the actual next abyss floor");
                var ward = Array.Find(g.Map.objects, o => o.kind == "abyssRiftWard");
                Check(ward != null, "A current-floor pylon is available");
                g.enemies.Clear(); g.hazards.Clear();
                g.save.x = ward.x; g.save.y = ward.y;
                g.save.runShards = 3;
                g.Talk(ward); g.Choose(0);
                Check(g.AbyssWardActive && g.AbyssWardCharterProgress == floor - 2,
                    "A physical paid tune records one additional unique floor");
                g.save.flags.Add("abyss_clear_" + floor);
                if (floor < 6)
                {
                    g.TakeBoon(0); // The real descent commits each bank.
                    Check(!g.Has("abyss_ward_charter_complete") &&
                        g.save.skillPoints == beforeSkills,
                        "Early-floor banking never grants the permanent final reward");
                }
                else
                {
                    Check(!g.Has("abyss_ward_charter_complete") &&
                        g.save.skillPoints == beforeSkills,
                        "Four ward activations alone cannot bypass the floor-six bank");
                    g.save.x = 31; g.save.y = 14;
                    WorldObject gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
                    g.Talk(gate);
                    Check(g.mode == "dialog" &&
                        g.conversation.choices[g.conversation.choices.Length-1].action ==
                            "abyssBank",
                        "The sixth-floor gate keeps the final banking choice available");
                    g.Choose(g.conversation.choices.Length-1);
                }
            }
            Check(g.save.room == 0 && g.Has("abyss_ward_charter_complete") &&
                g.save.skillPoints == beforeSkills + 1 &&
                g.save.shards == beforeShards + 8,
                "The last-floor settlement permanently banks the one-time charter award");
            g.StartAbyss(0);
            Check(g.InAbyss && g.AbyssWardCharterProgress == 0 &&
                g.Has("abyss_ward_charter_complete"),
                "A new expedition resets temporary pylon charges but retains the permanent award");
            g.save.abyssFloor = 6;
            regenerate.Invoke(g, null);
            for (int floor = 3; floor <= 6; floor++)
                g.save.flags.Add("abyss_annex_ward_" + floor);
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_6");
            g.save.x = 31; g.save.y = 14;
            int awardedSkills = g.save.skillPoints, awardedShards = g.save.shards;
            WorldObject repeatGate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            g.Talk(repeatGate); g.Choose(0);
            Check(g.save.skillPoints == awardedSkills && g.save.shards == awardedShards,
                "Replaying even a perfect charter cannot mint a second permanent reward");

            var incomplete = Unlocked(data); incomplete.StartAbyss(0);
            incomplete.save.flags.Add("abyss_complete");
            incomplete.save.abyssFloor = 6;
            regenerate.Invoke(incomplete, null);
            for (int floor = 3; floor <= 5; floor++)
                incomplete.save.flags.Add("abyss_annex_ward_" + floor);
            incomplete.enemies.Clear(); incomplete.save.flags.Add("abyss_clear_6");
            incomplete.save.x = 31; incomplete.save.y = 14;
            int skills = incomplete.save.skillPoints;
            WorldObject lastGate = Array.Find(incomplete.Map.objects, o => o.kind == "abyssGate");
            incomplete.Talk(lastGate); incomplete.Choose(0);
            Check(!incomplete.Has("abyss_ward_charter_complete") &&
                incomplete.save.skillPoints == skills,
                "A three-of-four circuit cannot claim the charter by extracting from six");
        }

        public static void AbyssRiftWardRequiresExpeditionLootAndActuallyChangesStorms(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            Check(Array.Find(g.Map.objects, o => o.kind == "abyssRiftWard") == null,
                "Early abyss floors do not display a useless rift pylon");
            g.save.abyssFloor = 3;
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            regenerate.Invoke(g, null);
            WorldObject ward = Array.Find(g.Map.objects, o => o.kind == "abyssRiftWard");
            Check(ward != null && g.CanStand(new Point(ward.x, ward.y)) &&
                Reachable(g).Contains((int)ward.y * g.Map.rows[0].Length + (int)ward.x),
                "The ward is a real reachable side-route object on floor three");
            g.enemies.Clear(); g.hazards.Clear(); g.effects.Clear();
            g.save.runShards = 3; g.save.shards = 37;
            int priorRevision = g.revision;
            g.Talk(ward);
            Check(g.mode == "world" && !g.AbyssWardActive,
                "A distant interaction cannot remotely activate the ward");
            g.save.x = ward.x; g.save.y = ward.y;
            g.Talk(ward);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 2,
                "The pylon offers a paid defensive option and a decline");
            g.save.x = 5; g.save.y = 14;
            g.Choose(0);
            Check(g.save.runShards == 3 && !g.AbyssWardActive,
                "A stale choice cannot activate the pylon after moving away");

            g.save.x = ward.x; g.save.y = ward.y;
            g.Talk(ward); g.Choose(0);
            Check(g.AbyssWardActive && g.save.runShards == 0 &&
                g.save.shards == 37 && g.revision > priorRevision &&
                g.Conditions().Contains("镇流生效"),
                "Tuning spends exactly three unbanked shards and updates the HUD");
            g.Talk(ward);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 1,
                "The floor's existing pylon cannot be bought again");
            g.Close();
            g.save.health = g.save.maxHealth = 9999; g.save.reducedMotion = true;
            var seen = new HashSet<Hazard>();
            for (int n = 0; n < 790; n++)
            {
                g.Tick(.05f, new Point());
                foreach (Hazard h in g.hazards)
                    if (h.kind == "rift") seen.Add(h);
            }
            Check(seen.Count == 3,
                "Ward slows live floor rift births to ten-second intervals");
            foreach (Hazard h in seen)
                Check(Math.Abs(h.damage - 6f) < .01f &&
                    Math.Abs(h.radius - 1.05f) < .01f,
                    "Ward reduces real warning damage and collision radius");
            g.save.flags.Add("abyss_clear_3");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.AbyssWardActive &&
                restored.save.runShards == 0 &&
                restored.Conditions().Contains("镇流生效"),
                "The defensive decision survives SaveData v2 reconstruction");

            var noLoot = Unlocked(data); noLoot.StartAbyss(2);
            noLoot.save.abyssFloor = 3;
            regenerate.Invoke(noLoot, null);
            WorldObject unpaid = Array.Find(noLoot.Map.objects,
                o => o.kind == "abyssRiftWard");
            noLoot.save.x = unpaid.x; noLoot.save.y = unpaid.y;
            noLoot.save.runShards = 0; noLoot.save.shards = 200;
            noLoot.Talk(unpaid); noLoot.Choose(0);
            Check(!noLoot.AbyssWardActive && noLoot.save.shards == 200,
                "Pylon cannot spend already banked shards or activate for free");
        }




        public static void AbyssWardSanctuaryHonorsWallsAndNoPhantomGuards(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.save.abyssFloor=3;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,null);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var ward=Array.Find(g.Map.objects,o=>o.kind=="abyssRiftWard");
            Check(ward!=null,"Pylon exists at the generated deep-floor object");
            g.save.x=ward.x;g.save.y=ward.y;
            g.save.runShards=3;g.Talk(ward);g.Choose(0);
            Check(g.AbyssWardShelter,"Paid pylon shields exposed nearby player");
            // Expiring distant periodic warnings do not claim that the
            // pylon absorbed a hit which could never physically reach us.
            g.hazards.Clear();g.effects.Clear();
            g.hazards.Add(new Hazard{kind="rift",abyssStormOwned=true,
                position=new Point(ward.x+5,ward.y),radius=.9f,
                timer=.01f,damage=9});
            g.sound="travel";
            float health=g.save.health;
            g.Tick(.05f,new Point());
            Check(Math.Abs(g.save.health-health)<.001f &&
                g.effects.Exists(e=>e.kind=="danger") &&
                !g.effects.Exists(e=>e.kind=="spark") &&
                g.sound!="guard",
                "Far-off rift still plays its ordinary contact effect, without a phantom shield sound");

            // Reuse the established blocked-approach fixture to prove
            // proximity alone cannot reach through an actual solid wall.
            float oldX=ward.x,oldY=ward.y;
            Point across;
            bool found=TryFindWallBlockedApproach(g,ward,out across);
            Check(found,"Generated floor offers a real wall-separated approach");
            g.save.x=across.x;g.save.y=across.y;
            Check((g.Position-new Point(ward.x,ward.y)).Length<1.6f &&
                !g.AbyssWardShelter,
                "Nearby player behind solid cover never receives pylon's shelter aura");
            ward.x=oldX;ward.y=oldY;
            g.save.x=ward.x;g.save.y=ward.y;
            Check(g.AbyssWardShelter,"Returning to real pylon restores the active protection");
            g.save.flags.Add("abyss_clear_3");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.AbyssWardShelter &&
                restored.save.runShards==0,
                "Shelter rebuilds from the legitimate floor flag and player foot on SaveData v2 load");
        }

        public static void TunedAbyssPylonSheltersFromWorldStormButNotBossStrike(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.save.abyssFloor=3;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,null);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var ward=Array.Find(g.Map.objects,o=>o.kind=="abyssRiftWard");
            Check(ward!=null,"A real floor-three pylon can provide physical shelter");
            g.save.health=g.save.maxHealth=1000;
            g.save.x=ward.x;g.save.y=ward.y;
            g.save.runShards=3;
            g.Talk(ward);g.Choose(0);
            Check(g.AbyssWardShelter && g.Conditions().Contains("镇流庇护"),
                "Purchased pylon protects a nearby player and publishes live HUD feedback");

            // Wait for the ACTUAL tuned ten-second world clock and contact,
            // not a synthetic damage call.
            Hazard ambient=null;
            for(int i=0;i<225 && ambient==null;i++)
            {
                g.Tick(.05f,new Point());
                ambient=g.hazards.Find(h=>h.abyssStormOwned);
            }
            Check(ambient!=null && ambient.timer>0 &&
                ambient.kind=="rift","Tuned floor still spawns real scheduled storms");
            float shieldedHealth=g.save.health;
            for(int i=0;i<48;i++)g.Tick(.05f,new Point());
            Check(!g.hazards.Contains(ambient) &&
                Math.Abs(g.save.health-shieldedHealth)<.001f,
                "A real periodic storm resolves harmlessly within the nearby ward sanctuary");

            // The very same sanctuary must NOT intercept a Boss warning.
            var boss=new Enemy(917,3,29,9,100,true);
            boss.target=g.Position;
            typeof(GameCore).GetMethod("BossExpansionAttack",
                BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(g,new object[]{boss});
            Hazard bossRift=g.hazards.Find(h=>h.kind=="rift"&&!h.abyssStormOwned);
            Check(bossRift!=null && !bossRift.abyssStormOwned,
                "Real Boss attack remains separate from ambient storms");
            g.invulnerable=0;
            float beforeBoss=g.save.health;
            for(int i=0;i<40;i++)g.Tick(.05f,new Point());
            Check(g.save.health<beforeBoss && !g.hazards.Contains(bossRift),
                "Boss contact still hurts when the player shelters at a tuned ward");

            g.save.x=30.5f;g.save.y=14.5f;g.invulnerable=0;
            Check(!g.AbyssWardShelter &&
                !g.Conditions().Contains("镇流庇护"),
                "Moving away removes only the proximity shelter, not the paid floor upgrade");
            ambient=null;
            for(int i=0;i<235 && ambient==null;i++)
            {
                g.Tick(.05f,new Point());
                ambient=g.hazards.Find(h=>h.abyssStormOwned);
            }
            Check(ambient!=null,"Leaving the pylon still exposes real later storms");
            float exposedHealth=g.save.health;
            for(int i=0;i<48;i++)g.Tick(.05f,new Point());
            Check(g.save.health<exposedHealth,
                "Unshielded periodic rift once again deals the original physical damage");
        }

        public static void AbyssWardPurchaseGroundsOnlyActualAmbientWarnings(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.save.abyssFloor=3;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,null);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.health=g.save.maxHealth=1000;g.invulnerable=0;
            var ward=Array.Find(g.Map.objects,o=>o.kind=="abyssRiftWard");
            Check(ward!=null,"A real deep-floor pylon exists");
            g.save.x=ward.x;g.save.y=ward.y;g.save.runShards=5;
            for(int i=0;i<122;i++)g.Tick(.05f,new Point());
            Hazard ambient=g.hazards.Find(h=>h.abyssStormOwned);
            Check(ambient!=null && ambient.kind=="rift" && ambient.timer>0,
                "Actual six-second storm clock creates an owned live telegraph");
            var boss=new Enemy(918,3,29,9,100,true);
            boss.target=new Point(ward.x,ward.y);
            typeof(GameCore).GetMethod("BossExpansionAttack",
                BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(g,new object[]{boss});
            Hazard bossRift=g.hazards.Find(h=>h.kind=="rift"&&!h.abyssStormOwned);
            Check(bossRift!=null&&bossRift.timer>0,
                "Real Boss phase creates another rift not owned by the storm");
            int coins=g.save.coins,shards=g.save.shards;
            g.Talk(ward);
            Check(g.mode=="dialog"&&g.conversation.choices[0].action=="abyssWardTune",
                "Original three-shard pylon purchase exists in the real world");
            g.Choose(0);
            Check(g.AbyssWardActive&&g.save.runShards==2&&
                g.save.coins==coins&&g.save.shards==shards,
                "Purchase consumes exactly three unbanked shards and persists floor ward");
            Check(!g.hazards.Contains(ambient)&&g.hazards.Contains(bossRift)&&
                bossRift.timer>0&&g.hazards.FindAll(h=>h.abyssStormOwned).Count==0,
                "Stabilizing clears the ambient storm but never cancels the Boss release warning");
            var timer=typeof(GameCore).GetField("stormTimer",
                BindingFlags.Instance|BindingFlags.NonPublic);
            Check((float)timer.GetValue(g)>=10f,
                "New ambient pulse really waits for the ten-second ward clock");
            g.Talk(ward);g.Choose(0);
            Check(g.save.runShards==2&&g.hazards.Contains(bossRift),
                "Repeated pylon use cannot charge again or erase Boss combat");
            // A clean floor is the authoritative SaveData v2 replay
            // contract: otherwise SpawnExpansionRoom legitimately creates
            // a fresh enemy wave and discards unbanked combat loot.
            g.save.flags.Add("abyss_clear_3");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.InAbyss&&restored.AbyssWardActive&&
                restored.save.runShards==2,
                "SaveData v2 reconstructs the paid ward with no new saved fields");
        }

        public static void AbyssWardPylonIsSeededAndTraversableOnAllHazardFloors(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int floor = 3; floor <= 6; floor++)
            for (int seed = 100; seed < 103; seed++)
            {
                g.save.abyssFloor = floor;
                g.save.abyssSeed = seed;
                regenerate.Invoke(g, null);
                WorldObject ward = Array.Find(g.Map.objects,
                    o => o.kind == "abyssRiftWard");
                Check(ward != null && g.CanStand(new Point(ward.x, ward.y)) &&
                    Reachable(g).Contains((int)ward.y * g.Map.rows[0].Length + (int)ward.x),
                    "Every late-floor seeded pylon has a physically traversable location");
                var recreated = new GameCore(data, CloneSave(g.save));
                WorldObject replay = Array.Find(recreated.Map.objects,
                    o => o.kind == "abyssRiftWard");
                Check(replay != null && replay.x == ward.x && replay.y == ward.y,
                    "SaveData v2 recreates exact ward positions on each deep floor");
            }
            g.save.abyssSeed = int.MaxValue; g.save.abyssFloor = 6;
            regenerate.Invoke(g, null);
            WorldObject maxSeed = Array.Find(g.Map.objects,
                o => o.kind == "abyssRiftWard");
            Check(maxSeed != null && g.CanStand(new Point(maxSeed.x, maxSeed.y)),
                "The maximum legal v2 seed does not overflow the ward site index");
        }

        public static void AbyssRootwellSeededRoutesStayReachableAndSaveStable(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(regenerate != null, "Original procedural Abyss generator remains installed");
            var positions = new HashSet<string>();
            for (int seed = 100; seed < 103; seed++)
            {
                g.save.abyssSeed = seed;
                regenerate.Invoke(g, null);
                WorldObject well = Array.Find(g.Map.objects,
                    o => o.kind == "abyssRootwell");
                Check(well != null && g.CanStand(new Point(well.x, well.y)),
                    "Every seeded recovery site is physically standable");
                int width = g.Map.rows[0].Length;
                Check(Reachable(g).Contains((int)well.y * width + (int)well.x),
                    "Every seeded recovery site is walkably connected to the entry corridor");
                positions.Add(well.x + "," + well.y);
                GameCore reloaded = new GameCore(data, CloneSave(g.save));
                WorldObject restored = Array.Find(reloaded.Map.objects,
                    o => o.kind == "abyssRootwell");
                Check(restored != null &&
                    restored.x == well.x && restored.y == well.y &&
                    reloaded.save.abyssSeed == seed,
                    "SaveData v2 regenerates the SAME rootwell site without changing the seed");
            }
            Check(positions.Count == 3,
                "Three successive deterministic seeds place the rootwell in distinct reachable branches");
            // The v2 sanitizer permits the full positive signed seed range.
            // An overflowed negative remainder must never produce a bogus
            // rootwell index or break a deep legacy expedition.
            g.save.abyssSeed = int.MaxValue;
            g.save.abyssFloor = 6;
            regenerate.Invoke(g, null);
            WorldObject extreme = Array.Find(g.Map.objects, o => o.kind == "abyssRootwell");
            Check(extreme != null && g.CanStand(new Point(extreme.x, extreme.y)) &&
                Reachable(g).Contains((int)extreme.y * g.Map.rows[0].Length + (int)extreme.x),
                "Maximum legal v2 seed still places a reachable final-floor rootwell");
            GameCore extremeReload = new GameCore(data, CloneSave(g.save));
            WorldObject extremeRestored = Array.Find(extremeReload.Map.objects,
                o => o.kind == "abyssRootwell");
            Check(extremeRestored != null && extremeRestored.x == extreme.x &&
                extremeRestored.y == extreme.y,
                "The maximum legal seed and sixth-floor rootwell survive SaveData v2 reload");
        }

        public static void RoadwardenFieldRecordMarkersRespectRealProofAndMastery(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            WorldObject tablet=Array.Find(g.Map.objects,
                o=>o.kind=="abyssSurvey");
            WorldObject southern=Array.Find(g.Map.objects,
                o=>o.kind=="abyssFarSurvey");
            WorldObject echo=Array.Find(g.Map.objects,
                o=>o.kind=="abyssFarEcho");
            WorldObject exit=Array.Find(g.Map.objects,
                o=>o.kind=="abyssExit");
            Check(g.AbyssTestimonyRecordState(tablet)==1 &&
                g.AbyssTestimonyRecordState(echo)==3 &&
                g.AbyssTestimonyRecordState(exit)==0,
                "World marker state distinguishes unvisited proof, locked echo and unrelated exit");
            g.save.x=echo.x;g.save.y=echo.y;
            g.Talk(echo);g.Close();
            Check(g.AbyssTestimonyRecordState(echo)==3,
                "Premature echo interaction cannot falsely display a collected marker");
            g.save.x=southern.x;g.save.y=southern.y;
            g.Talk(southern);g.Close();
            Check(g.AbyssTestimonyRecordState(southern)==2 &&
                g.AbyssTestimonyRecordState(echo)==1,
                "Reading the real southern tablet marks it complete and enables the echo");
            g.save.x=echo.x;g.save.y=echo.y;
            g.Talk(echo);g.Close();
            Check(g.AbyssTestimonyRecordState(echo)==2,
                "The echo's world marker changes only after its actual story interaction");
            g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            WorldObject restoredEcho=Array.Find(restored.Map.objects,
                o=>o.kind=="abyssFarEcho");
            Check(restored.AbyssTestimonyRecordState(restoredEcho)==2,
                "The visual record state survives SaveData v2 reload without new fields");
            restored.save.flags.Add("abyss_testimony_codex_complete");
            Check(restored.AbyssTestimonyLegacyComplete &&
                restored.AbyssTestimonyRecordState(
                    Array.Find(restored.Map.objects,o=>o.kind=="abyssSurvey"))==1,
                "Archive mastery reveals real outstanding notes but never auto-collects them");
            restored.save.room=0;
            Check(restored.AbyssTestimonyRecordState(restoredEcho)==0,
                "No Abyss proof highlight leaks into unrelated overworld scenes");
        }

        public static void CompletedArchiveExtendsRealRiftWindupsWithoutChangingDamage(Campaign data)
        {
            var ordinary=Unlocked(data);ordinary.StartAbyss(2);
            var archivist=Unlocked(data);
            archivist.save.flags.Add("abyss_testimony_codex_complete");
            archivist.StartAbyss(2);
            var generate=typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(GameCore g in new[]{ordinary,archivist})
            {
                g.save.abyssFloor=3;
                g.save.abyssSeed=137;
                generate.Invoke(g,null);
                g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
                g.save.x=5;g.save.y=14;
                g.save.maxHealth=g.save.health=9999;
                g.save.reducedMotion=true;
            }
            Check(!ordinary.AbyssArchiveForesight &&
                archivist.AbyssArchiveForesight &&
                archivist.Objective().Contains("巡路预知"),
                "Only the permanently completed archive unlocks its visible Rift foresight");
            for(int i=0;i<120;i++)
            {
                ordinary.Tick(.05f,new Point());
                archivist.Tick(.05f,new Point());
            }
            Hazard a=ordinary.hazards.Find(h=>h.kind=="rift");
            Hazard b=archivist.hazards.Find(h=>h.kind=="rift");
            Check(a!=null&&b!=null&&
                Math.Abs((b.timer-a.timer)-.4f)<.01f &&
                Math.Abs(b.damage-a.damage)<.01f &&
                Math.Abs(b.radius-a.radius)<.01f,
                "Actual six-second spawned rifts gain .4s of warning, not reduced damage or area");
            float life=ordinary.save.health, protectedLife=archivist.save.health;
            for(int i=0;i<29;i++)
            {
                ordinary.Tick(.05f,new Point());
                archivist.Tick(.05f,new Point());
            }
            Check(ordinary.save.health<life &&
                archivist.save.health==protectedLife,
                "An unarchived hero suffers the same real Rift before the archivist's longer warning ends");
            for(int i=0;i<10;i++)archivist.Tick(.05f,new Point());
            Check(archivist.save.health<protectedLife,
                "Foresight delays but never erases an undodged Rift contact");
            archivist.save.flags.Remove("abyss_testimony_codex_complete");
            Check(!archivist.AbyssArchiveForesight,
                "Without a permanent proof there is no free Rift warning buff");

            var early=Unlocked(data);early.StartAbyss(2);
            early.save.flags.Add("abyss_testimony_codex_complete");
            early.save.abyssFloor=3;
            generate.Invoke(early,null);
            early.enemies.Clear();early.hazards.Clear();
            int expected=early.AbyssCircuitNextNode;
            WorldObject wrong=Array.Find(early.Map.objects,
                o=>o.kind=="abyssCircuit"+((expected+1)%3));
            early.save.x=wrong.x;early.save.y=wrong.y;
            early.Talk(wrong);
            Hazard backlash=early.hazards.Find(h=>h.kind=="rift");
            Check(backlash!=null&&Math.Abs(backlash.timer-2f)<.001f&&
                Math.Abs(backlash.damage-18f)<.001f,
                "The same permanent foresight also adds .4s to live miswire backlash warnings");

            early.save.flags.Add("abyss_clear_3");
            early.PrepareSave();
            var reload=new GameCore(data,CloneSave(early.save));
            Check(reload.InAbyss&&reload.AbyssArchiveForesight,
                "The unlocked combat advantage survives the existing SaveData v2 round trip");
            var city=Unlocked(data);
            city.save.flags.Add("abyss_testimony_codex_complete");
            Check(!city.AbyssArchiveForesight,
                "The passive cannot affect ordinary surface hazards outside the Abyss");
        }

        public static void SixFloorRoadwardenArchiveBanksOnceAfterFullPhysicalExpedition(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.save.flags.Add("abyss_complete"); // Isolate the new reward.
            var sites = new [] {
                "abyssSurvey","abyssCache","abyssFarCache",
                "abyssFarSurvey","abyssFarEcho"
            };
            for (int floor = 1; floor <= 6; floor++)
            {
                Check(g.InAbyss && g.save.abyssFloor == floor &&
                    g.AbyssTestimonyFloorsFiled == floor-1,
                    "Each trial leg enters the correct REAL seeded abyss floor");
                g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
                foreach (string kind in sites)
                {
                    WorldObject obj = Array.Find(g.Map.objects, o => o.kind == kind);
                    Check(obj != null && g.CanStand(new Point(obj.x,obj.y)),
                        "The " + kind + " proof is physically present on floor " + floor);
                    g.save.x=obj.x;g.save.y=obj.y;
                    g.Talk(obj);g.Close();
                }
                Check(g.AbyssTestimonySealed &&
                    g.AbyssTestimonyFloorsFiled == floor,
                    "Five real site visits complete the current floor's separate ledger");
                g.save.flags.Add("abyss_clear_" + floor);
                if (floor < 6)
                {
                    g.TakeBoon(0);
                    // Every bank awards real XP, which may level up the hero
                    // and legitimately grant unrelated skill points. Check
                    // the dedicated permanent milestone, not total points.
                    Check(!g.AbyssTestimonyLegacyComplete,
                        "Intermediate floor banking cannot grant the permanent six-floor award");
                }
                else
                {
                    Check(!g.AbyssTestimonyLegacyComplete,
                        "Six sets of recovered notes alone cannot bypass final gate settlement");
                    g.save.x=30.5f;g.save.y=14.5f;
                    WorldObject gate = Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
                    g.Talk(gate);
                    Check(g.mode=="dialog" &&
                        g.conversation.text.Contains("六层证词归档 6/6"),
                        "Real last-floor gate advertises fully earned archival milestone");
                    int oldShards=g.save.shards, unbanked=g.save.runShards;
                    int oldSkills = g.save.skillPoints;
                    g.Choose(g.conversation.choices.Length-1);
                    Check(g.save.room==0 &&
                        g.AbyssTestimonyLegacyComplete &&
                        g.save.skillPoints>=oldSkills+2 &&
                        g.save.shards>=oldShards+unbanked+12,
                        "Only legal final banking converts the full expedition into permanent rewards");
                }
            }
            var loaded = new GameCore(data,CloneSave(g.save));
            Check(loaded.AbyssTestimonyLegacyComplete &&
                !loaded.InAbyss,
                "The one-time permanent milestone survives ordinary SaveData v2 reload");
            loaded.StartAbyss(0);
            Check(loaded.AbyssTestimonyLegacyComplete &&
                loaded.AbyssTestimonyFloorsFiled==0,
                "A second expedition clears per-floor proofs but preserves permanent archive");
            loaded.save.abyssFloor=6;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(loaded,null);
            for(int floor=1;floor<=6;floor++)
                loaded.save.flags.Add("abyss_annex_testimony_complete_"+floor);
            loaded.enemies.Clear();loaded.hazards.Clear();loaded.projectiles.Clear();
            loaded.save.flags.Add("abyss_clear_6");
            loaded.save.x=30.5f;loaded.save.y=14.5f;
            int earned=loaded.save.skillPoints;
            WorldObject secondGate=Array.Find(loaded.Map.objects,
                o=>o.kind=="abyssGate");
            loaded.Talk(secondGate);
            loaded.Choose(loaded.conversation.choices.Length-1);
            Check(loaded.save.skillPoints==earned &&
                loaded.AbyssTestimonyLegacyComplete,
                "Even a perfect replay cannot claim permanent codex skill points twice");

            var partial=Unlocked(data);partial.StartAbyss(0);
            partial.save.flags.Add("abyss_complete");
            for(int floor=1;floor<=5;floor++)
                partial.save.flags.Add("abyss_annex_testimony_complete_"+floor);
            partial.save.abyssFloor=6;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(partial,null);
            partial.enemies.Clear();partial.save.flags.Add("abyss_clear_6");
            partial.save.x=30.5f;partial.save.y=14.5f;
            int previous=partial.save.skillPoints;
            WorldObject partialGate=Array.Find(partial.Map.objects,
                o=>o.kind=="abyssGate");
            partial.Talk(partialGate);
            partial.Choose(partial.conversation.choices.Length-1);
            Check(!partial.AbyssTestimonyLegacyComplete &&
                partial.save.skillPoints==previous,
                "Five completed floors and an unfinished sixth cannot claim the permanent archive");
        }














        public static void AbyssTestimonyRoutePlannerRanksActualWalkingDistance(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.x=30.5f;g.save.y=14.5f;
            WorldObject gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.Talk(gate);
            int list=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyList");
            Check(list>=0,"The completed first-floor gate presents the new planning option");
            g.Choose(list);
            var walking=typeof(GameCore).GetMethod("AbyssTestimonyWalkingSteps",
                BindingFlags.Instance|BindingFlags.NonPublic);
            int previous=-1,count=0;
            foreach(Choice choice in g.conversation.choices)
            {
                if(!choice.action.StartsWith("abyssTestimonyTo:"))continue;
                string id=choice.action.Substring("abyssTestimonyTo:".Length);
                WorldObject site=Array.Find(g.Map.objects,o=>o.id==id);
                Check(site!=null && g.AbyssTestimonyRecordState(site)==1,
                    "Every planner row opens one existing uncollected world object");
                int distance=(int)walking.Invoke(g,new object[]{site});
                Check(distance>=previous && distance>=0 &&
                    choice.label.Contains(distance+"步") &&
                    choice.en.Contains(distance+" steps"),
                    "The real planner sorts physical walking steps and displays matching bilingual distances");
                previous=distance;count++;
            }
            Check(count==4 && previous>0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="abyssTestimonyGateMenu",
                "Four unlocked field records and the real banking return retain their normal actions");
        }

        public static void AbyssGatePlansSpecificPhysicalSurveyAndReturnsToSettlement(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            var cache=Array.Find(g.Map.objects,o=>o.kind=="abyssFarCache");
            Check(gate!=null&&cache!=null,"A real exit and deep cache are present");
            g.save.x=gate.x;g.save.y=gate.y;
            int coins=g.save.coins,shards=g.save.shards;
            int runCoins=g.save.runCoins,runShards=g.save.runShards;
            g.Talk(gate);
            int list=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyList");
            Check(list>=0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="abyssBank",
                "Original cleared gate advertises explicit destination planning before final banking");
            g.Choose(list);
            int far=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTo:"+cache.id);
            Check(far>=0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="abyssTestimonyGateMenu",
                "Nested physically sourced terminal lists the real deep cache and offers a return to banking");
            g.Choose(far);
            Check(System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,cache) &&
                (g.NavigationTarget().Value-new Point(cache.x,cache.y)).Length<.001f &&
                g.save.coins==coins && g.save.shards==shards &&
                g.save.runCoins==runCoins && g.save.runShards==runShards,
                "Player's own target is guided without transport, settlement or bonus loot");
            g.Talk(gate);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyList"));
            int back=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyGateMenu");
            g.save.x=5;g.save.y=14;g.Choose(back);
            Check(g.mode=="world" && g.save.room==8,
                "Old nested dialogue cannot reopen gate banking remotely");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyList"));
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyGateMenu"));
            Check(g.mode=="dialog" &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="abyssBank",
                "Legitimate terminal back action reopens the live banking choices");
            g.Choose(g.conversation.choices.Length-1);
            Check(g.save.room==0 && !g.InAbyss,
                "Explicit final transaction still returns to town normally");
        }

        public static void AbyssRecoveredFieldRecordCanStopActiveRouteWithoutBanking(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var near=Array.Find(g.Map.objects,o=>o.kind=="abyssSurvey");
            var deep=Array.Find(g.Map.objects,o=>o.kind=="abyssFarSurvey");
            Check(near!=null && deep!=null,"Real near and deep recovered source objects exist");
            g.save.x=near.x;g.save.y=near.y;g.Talk(near);
            int startingPending=g.save.runShards,startingCoins=g.save.coins;
            int initialStop=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyStopHere");
            Check(initialStop<0 &&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="abyssTestimonyFollow")>=0,
                "An untracked field record does not advertise a meaningless cancel");
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyFollow"));
            WorldObject initialTarget=g.AbyssTestimonyGuidedSite;
            Check(initialTarget!=null,
                "Following a real recovered record activates a legitimate next waypoint");
            g.Talk(near);
            int stop=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyStopHere");
            Check(stop>=0 &&
                g.conversation.choices[0].action=="close",
                "Original keep action remains while a physical tablet offers route cancellation");
            g.save.x=5;g.save.y=14;g.Choose(stop);
            Check(System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,initialTarget),
                "Stale tablet menu cannot stop a route from across the cavern");
            g.save.x=near.x;g.save.y=near.y;g.Talk(near);
            stop=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyStopHere");
            g.Choose(stop);
            Check(g.AbyssTestimonyGuidedSite==null &&
                g.AbyssTestimonyProgress==1 &&
                g.save.runShards==startingPending &&
                g.save.coins==startingCoins &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f,
                "Field cancellation restores normal exit guidance without consuming proofs or loot");
            g.Talk(near);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyStopHere")<0,
                "Stopped route cannot be cancelled a second time");
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyFollow"));
            Check(g.AbyssTestimonyGuidedSite!=null,
                "A nearby recovered record can resume the unfinished route normally");
            g.save.x=deep.x;g.save.y=deep.y;g.Talk(deep);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyStopHere")>=0,
                "Deep original field records expose the same real cancellation action");
        }

        public static void AbyssAmbientHUDAndFieldMarkersShareShortestReachableTestimony(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;
            int originalCoins=g.save.coins,originalShards=g.save.shards;
            WorldObject first=g.AbyssTestimonySuggestedSite;
            Check(first!=null &&
                g.AbyssTestimonyRecordState(first)==1 &&
                g.Objective().Contains(first.name),
                "Untracked journal uses the same actually reachable account as world field highlights");
            for(int i=0;i<8;i++)
                Check(System.Object.ReferenceEquals(g.AbyssTestimonySuggestedSite,first),
                    "Repeated HUD reads reuse a stable current-tile suggestion without changing rewards");
            // An already filed source must immediately vanish from ambient
            // guidance even if neither the player nor the cave moved.
            string[] kinds={"abyssSurvey","abyssCache",
                "abyssFarSurvey","abyssFarCache","abyssFarEcho"};
            string[] flags={"abyss_annex_survey_1","abyss_annex_cache_1",
                "abyss_annex_far_survey_1","abyss_annex_far_cache_1",
                "abyss_annex_far_echo_1"};
            int index=Array.FindIndex(kinds,k=>k==first.kind);
            Check(index>=0,"Suggested object is one of the actual seeded testimonies");
            g.save.flags.Add(flags[index]);
            WorldObject next=g.AbyssTestimonySuggestedSite;
            Check(!System.Object.ReferenceEquals(next,first) &&
                next!=null && g.Objective().Contains(next.name),
                "Existing per-floor proof immediately invalidates stale HUD and mission target");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.AbyssTestimonySuggestedSite!=null &&
                loaded.AbyssTestimonyProgress==1 &&
                loaded.save.coins==originalCoins &&
                loaded.save.shards==originalShards,
                "No ambient navigation intent is serialized in SaveData v2");
        }

        public static void AbyssTestimonyAutomaticRouteUsesShortestWalkNotCrowFlight(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            WorldObject near=Array.Find(g.Map.objects,o=>o.kind=="abyssSurvey");
            WorldObject far=Array.Find(g.Map.objects,o=>o.kind=="abyssCache");
            Check(near!=null && far!=null,
                "Two original testimony sites are present to compare real travel distance");
            // Select a live source position and use a long wall with a
            // walk-around end. The nearer target is straight across the
            // divider; the other is reachable without crossing it.
            // Test only uses the actual floor map and its collision tiles.
            var map=g.Map;
            string[] old=(string[])map.rows.Clone();
            int y=(int)Math.Floor(near.y);
            int x=(int)Math.Floor(near.x);
            Point origin=new Point(near.x+4,near.y);
            if(!g.CanStand(origin))
                origin=new Point(near.x+4,near.y-2);
            Check(g.CanStand(origin),"The comparison starts from walkable cavern ground");
            g.save.x=origin.x;g.save.y=origin.y;
            // A temporary perpendicular barrier around the near-site
            // forces a detour; never mutate authored assets or SaveData.
            int wallX=x+2;
            for(int yy=y-6;yy<=y+6;yy++)
            {
                if(yy<2||yy>=map.rows.Length-2)continue;
                char[] chars=map.rows[yy].ToCharArray();
                chars[wallX]='#';map.rows[yy]=new string(chars);
            }
            var sel=typeof(GameCore).GetMethod("ReachableAbyssTestimonySite",
                BindingFlags.Instance|BindingFlags.NonPublic);
            WorldObject selected=(WorldObject)sel.Invoke(g,null);
            Check(selected!=null &&
                g.AbyssTestimonyRecordState(selected)==1,
                "An automatic route still reaches a real recoverable record in the detour maze");
            // Independently establish shortest reach among actual targets
            // by walking the same production interaction-range rules.
            var candidates=new List<WorldObject>();
            foreach(WorldObject site in map.objects)
                if(g.AbyssTestimonyRecordState(site)==1)
                    candidates.Add(site);
            var reachable=typeof(GameCore).GetMethod("CanNavigateToAbyssTestimony",
                BindingFlags.Instance|BindingFlags.NonPublic);
            Check((bool)reachable.Invoke(g,new object[]{selected}),
                "The chosen shortest route passes the independent target reachability guard");
            map.rows=old;
        }

        public static void AbyssTestimonyNavigationUsesLegalAdjacentInteractionFoot(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            WorldObject source=Array.Find(g.Map.objects,
                o=>o.kind=="abyssSurvey");
            Check(source!=null,"The original seeded survey tablet exists");
            string[] oldRows=(string[])g.Map.rows.Clone();
            int sx=(int)Math.Floor(source.x),sy=(int)Math.Floor(source.y);
            char[] row=g.Map.rows[sy].ToCharArray();
            row[sx]='#';g.Map.rows[sy]=new string(row);
            Check(!g.CanStand(new Point(source.x,source.y)),
                "This real source tablet now occupies a blocked footprint");
            Point from=new Point();
            bool found=false;
            Point[] directions={new Point(1,0),new Point(-1,0),
                new Point(0,1),new Point(0,-1)};
            foreach(Point d in directions)
            {
                Point probe=new Point(source.x+d.x,source.y+d.y);
                if(!g.CanStand(probe))continue;
                from=probe;found=true;break;
            }
            Check(found,"Tablet still has at least one walkable approach tile");
            g.save.x=from.x;g.save.y=from.y;
            var method=typeof(GameCore).GetMethod("CanNavigateToAbyssTestimony",
                BindingFlags.Instance|BindingFlags.NonPublic);
            Check(method!=null && (bool)method.Invoke(g,new object[]{source}),
                "A legitimate adjacent interaction route remains available when the object cell itself is solid");
            g.Talk(source);
            Check(g.Has("abyss_annex_survey_1") &&
                g.AbyssTestimonyProgress==1,
                "Real object interaction consumes its existing proof from that legal neighboring foot");
            g.Map.rows=oldRows;
        }

        public static void AbyssFieldTabletRoutePickerChoosesActualReachableRecords(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var tablet=Array.Find(g.Map.objects,o=>o.kind=="abyssSurvey");
            var cache=Array.Find(g.Map.objects,o=>o.kind=="abyssFarCache");
            Check(tablet!=null && cache!=null,
                "A recovered source and optional distant supply record exist in the seeded floor");
            g.save.x=tablet.x;g.save.y=tablet.y;
            g.Talk(tablet);
            int chooser=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyList");
            Check(chooser==2 && g.AbyssTestimonyProgress==1 &&
                g.conversation.choices[0].action=="close",
                "An actual collected tablet offers a distinct route picker after normal record and follow actions");
            g.Choose(chooser);
            int chooseCache=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTo:"+cache.id);
            Check(chooseCache>=0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="close",
                "A deep supply site appears in the dynamically generated reachable route list");
            int banked=g.save.shards, pending=g.save.runShards;
            g.Choose(chooseCache);
            Check(System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,cache) &&
                (g.NavigationTarget().Value-new Point(cache.x,cache.y)).Length<.001f &&
                g.save.shards==banked && g.save.runShards==pending,
                "Player-directed selection overrides automatic nearest-site order without rewards");

            // Source-bound nested menus must not work from across the cave.
            g.Talk(tablet);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyList"));
            g.save.x=5;g.save.y=14;
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTo:"+cache.id));
            Check(System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,cache),
                "Walking away before selecting a nested route cannot authorize a remote objective command");
        }

        public static void AbyssBlockedTestimonyMenusNeverOfferDeadTracking(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var map=g.Map;
            string[] original=(string[])map.rows.Clone();
            var gate=Array.Find(map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;
            int blocked=0;
            foreach(WorldObject site in map.objects)
            {
                if(g.AbyssTestimonyRecordState(site)!=1)continue;
                int cx=(int)Math.Floor(site.x),cy=(int)Math.Floor(site.y);
                for(int dy=-1;dy<=1;dy++)
                for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dy==0)continue;
                    int x=cx+dx,y=cy+dy;
                    if(x<2||y<2||x>=map.rows[0].Length-2||
                        y>=map.rows.Length-2)continue;
                    char[] row=map.rows[y].ToCharArray();
                    row[x]='#';map.rows[y]=new string(row);
                }
                blocked++;
            }
            Check(blocked==4 && g.AbyssTestimonyNextSite!=null,
                "Four available testimony sites exist; the fifth echo is legitimately locked");
            g.Talk(gate);
            Check(g.mode=="dialog" &&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="abyssTestimonyTrack")<0 &&
                g.conversation.text.Contains("道路被封阻") &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="abyssBank",
                "Fully obstructed caves suppress dead guidance but retain truthful banking and explanation");
            int shards=g.save.shards, coins=g.save.coins;
            g.Close();map.rows=original;
            g.Talk(gate);
            Check(Array.FindIndex(g.conversation.choices,
                    c=>c.action=="abyssTestimonyTrack")>=0 &&
                !g.conversation.text.Contains("道路被封阻") &&
                g.save.shards==shards && g.save.coins==coins,
                "Clearing real terrain restores the physical menu without altering banked rewards");
        }

        public static void AbyssGuidedTestimonySkipsBlockedCaveAndKeepsSaveRewards(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            var gate = Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;
            WorldObject first=g.AbyssTestimonyNextSite;
            Check(first!=null && (g.Position - new Point(first.x,first.y)).Length>3f,
                "Test isolates a genuinely distant seeded-cavern testimony");
            var map=g.Map;
            string[] original=(string[])map.rows.Clone();
            int fx=(int)Math.Floor(first.x),fy=(int)Math.Floor(first.y);
            for(int dy=-1;dy<=1;dy++)
            for(int dx=-1;dx<=1;dx++)
            {
                if(dx==0&&dy==0)continue;
                int x=fx+dx,y=fy+dy;
                if(x<2||y<2||x>=map.rows[0].Length-2||
                    y>=map.rows.Length-2)continue;
                char[] row=map.rows[y].ToCharArray();
                row[x]='#';map.rows[y]=new string(row);
            }
            Check(g.NavigationPath(new Point(first.x,first.y)).Count==0,
                "A genuine wall ring prevents any traversable approach to nearest account");
            int permanent=g.save.shards,pending=g.save.runShards;
            g.Talk(gate);
            int action=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack");
            Check(action>=0,"Real gate still offers its usual survey action");
            g.Choose(action);
            WorldObject guided=g.AbyssTestimonyGuidedSite;
            Check(guided!=null && !System.Object.ReferenceEquals(guided,first) &&
                g.AbyssTestimonyRecordState(guided)==1 &&
                g.NavigationPath(new Point(guided.x,guided.y)).Count>0 &&
                g.save.shards==permanent && g.save.runShards==pending,
                "Gate switches from blocked nearest to a physically reachable record without rewards");

            // Reverting the terrain doesn't rewrite the active route.
            // Keeping this choice stable stops mid-stride oscillation.
            map.rows=original;
            Check(System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,guided),
                "A valid chosen site stays stable even after a passage changes");
            g.save.x=guided.x;g.save.y=guided.y;g.Talk(guided);g.Close();
            Check(!System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,guided),
                "A recovered clue still advances to a fresh, valid next-site waypoint");
        }

        public static void AbyssFieldRecordsCanDirectlyContinueActualTestimonyTrail(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var near=Array.Find(g.Map.objects,o=>o.kind=="abyssSurvey");
            var far=Array.Find(g.Map.objects,o=>o.kind=="abyssFarSurvey");
            Check(near!=null && far!=null,
                "Near and deep original testimony tablets exist in the current floor");
            int previousShards=g.save.shards;
            g.save.x=near.x;g.save.y=near.y;
            g.Talk(near);
            int follow=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyFollow");
            Check(follow==1 && g.conversation.choices[0].action=="close" &&
                g.AbyssTestimonyProgress==1,
                "Actual near-side tablet keeps its original close and offers follow as a second action");
            g.save.x=5;g.save.y=14;g.Choose(follow);
            Check(g.AbyssTestimonyGuidedSite==null,
                "Leaving the tablet cannot remotely activate a stale follow choice");
            g.save.x=near.x;g.save.y=near.y;g.Talk(near);
            g.Choose(1);
            var destination=g.AbyssTestimonyGuidedSite;
            Check(destination!=null && !System.Object.ReferenceEquals(destination,near) &&
                g.AbyssTestimonyRecordState(destination)==1 &&
                (g.NavigationTarget().Value-new Point(destination.x,destination.y)).Length<.001f,
                "Physical reading immediately selects the next legitimate, unlocked real record");

            // Deep-gallery sources use their own production dialogue branch.
            g.save.x=far.x;g.save.y=far.y;g.Talk(far);
            Check(g.AbyssTestimonyProgress==2 &&
                g.conversation.choices[0].action=="close" &&
                g.conversation.choices[1].action=="abyssTestimonyFollow",
                "Deep-gallery tablet exposes the same physical follow action after granting its normal XP");
            g.Choose(1);
            Check(g.AbyssTestimonyGuidedSite!=null &&
                !System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,far) &&
                g.save.shards==previousShards && g.save.room==8,
                "Deep follow retargets without teleport or permanent reward manipulation");
        }

        public static void AbyssSevenChoiceGateKeepsBankAndCircuitTransfersAfterSurvey(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runCoins=24;g.save.runShards=7;
            int coins=g.save.coins,shards=g.save.shards;
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            var relay=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit1");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            Check(g.conversation.choices.Length==7 &&
                g.conversation.choices[3].action=="abyssTestimonyTrack" &&
                g.conversation.choices[4].action=="abyssTestimonyList" &&
                g.conversation.choices[5].action=="abyssCircuitRoutes" &&
                g.conversation.choices[6].action=="abyssBank",
                "Real cleared-floor dialog exposes seven distinct safe actions: boons, automatic tracking, chosen destination, circuit and bank");
            g.Choose(3);
            Check(g.AbyssTestimonyGuidedSite!=null && g.save.shards==shards,
                "Choice four deliberately starts fieldwork without settlement");
            g.Talk(gate);
            g.Choose(5);
            Check(g.mode=="dialog" &&
                g.conversation.choices[1].action=="abyssCircuitTo1",
                "Seven-choice gate still opens the original physical circuit network");
            g.Choose(1);
            Check(Math.Abs(g.save.x-relay.x)<.01f &&
                Math.Abs(g.save.y-relay.y)<.01f &&
                g.AbyssTestimonyGuidedSite==null &&
                g.save.runShards==7 && g.save.shards==shards,
                "Explicit relay travel supersedes testimony intent without awarding loot");
            g.Talk(relay);
            g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                Math.Abs(g.save.y-14.5f)<.01f,
                "Return via the real relay circuit reaches the unchanged east gate");
            g.Talk(gate);
            g.Choose(g.conversation.choices.Length-1);
            Check(g.save.room==0 && g.save.coins==coins+24 &&
                g.save.shards==shards+7,
                "After both optional routes the original final choice is the only bank");
        }

        public static void AbyssGateTestimonyTrackingCanBeCancelledAndReenabled(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var gate = Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            int start = Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack");
            int initialCoins=g.save.coins, initialShards=g.save.shards;
            g.Choose(start);
            var tracked=g.AbyssTestimonyGuidedSite;
            Check(tracked!=null,
                "Safe gate explicitly activates a real recoverable field record");
            g.Talk(gate);
            int stop = Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyUntrack");
            Check(stop>=0 &&
                Array.FindIndex(g.conversation.choices,
                    c=>c.action=="abyssTestimonyTrack")<0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action=="abyssBank",
                "Live gate offers one toggle rather than hiding the original bank");
            // Source-distance validation also protects deliberate cancellation.
            g.save.x=5;g.save.y=14;g.Choose(stop);
            Check(g.AbyssTestimonyGuidedSite==tracked,
                "Stale distant menu cannot cancel the current field route");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            stop=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyUntrack");
            g.Choose(stop);
            Check(g.AbyssTestimonyGuidedSite==null &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f &&
                g.save.room==8 && g.save.coins==initialCoins &&
                g.save.shards==initialShards && g.AbyssTestimonyProgress==0,
                "Stopping guidance restores the original exit target without rewards");
            g.Talk(gate);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack")>=0,
                "After cancelling, the same physical gate permits a fresh survey");
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack"));
            Check(g.AbyssTestimonyGuidedSite==tracked,
                "Player can resume the same unfinished record without resetting proof flags");
        }

        public static void AbyssGateTestimonyTrackingLeadsThroughRealSitesWithoutBanking(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            WorldObject gate = Array.Find(g.Map.objects,
                o => o.kind == "abyssGate");
            Check(gate != null && g.AbyssTestimonyProgress == 0 &&
                g.AbyssTestimonyNextSite != null,
                "A fresh floor contains the five genuine field records");
            g.save.x = gate.x; g.save.y = gate.y;
            g.Talk(gate);
            int choice = Array.FindIndex(g.conversation.choices,
                c => c.action == "abyssTestimonyTrack");
            Check(choice >= 0 &&
                g.conversation.choices[g.conversation.choices.Length-1].action ==
                    "abyssBank",
                "East gate offers explicit exploration before its original banking option");
            var first = g.AbyssTestimonyNextSite;
            var actualStart = g.NavigationTarget().Value;
            int banked = g.save.shards, pending = g.save.runShards;
            g.Choose(choice);
            WorldObject walkedFirst = g.AbyssTestimonyGuidedSite;
            Check(walkedFirst != null &&
                g.AbyssTestimonyRecordState(walkedFirst) == 1 &&
                g.Objective().Contains(walkedFirst.name) &&
                (g.NavigationTarget().Value - new Point(walkedFirst.x,walkedFirst.y)).Length<.001f &&
                (actualStart-g.NavigationTarget().Value).Length>1f &&
                g.save.shards==banked && g.save.runShards==pending &&
                g.save.room==8,
                "Explicit gate choice changes only live objective, never location or loot");

            // Follow the actual physical five-record chain; the echo unlock
            // remains governed by its southern source testimony.
            for (int i=0;i<5;i++)
            {
                WorldObject target=g.AbyssTestimonyGuidedSite;
                Check(target!=null && g.AbyssTestimonyRecordState(target)==1 &&
                    (g.NavigationTarget().Value-new Point(target.x,target.y)).Length<.001f,
                    "Compass remains pinned to the valid currently recoverable object");
                g.save.x=target.x;g.save.y=target.y;
                g.Talk(target);
                Check(g.Has("abyss_clear_1") && g.AbyssTestimonyProgress==i+1,
                    "Physical testimony interaction advances one real SaveData v2 proof");
                g.Close();
                Check(!System.Object.ReferenceEquals(g.AbyssTestimonyGuidedSite,target),
                    "A recorded field site immediately leaves the compass");
            }
            Check(g.AbyssTestimonySealed && g.AbyssTestimonyGuidedSite==null &&
                !g.Objective().Contains("正在追踪巡路证词") &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f &&
                g.save.shards==banked && g.save.room==8,
                "Complete five-site investigation returns navigation to real banking gate");

            g.PrepareSave();
            var restored = new GameCore(data,CloneSave(g.save));
            Check(restored.AbyssTestimonySealed &&
                restored.AbyssTestimonyGuidedSite==null &&
                (restored.NavigationTarget().Value-new Point(31,14)).Length<.001f,
                "Existing save format remembers proofs, not temporary compass intent");
        }

        public static void AbyssTestimonyTrackingRejectsStaleGateAndResetsOnFloorChange(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            int index=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack");
            g.save.x=5;g.save.y=14;g.Choose(index);
            Check(g.AbyssTestimonyGuidedSite==null &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f,
                "Walking away before menu confirmation cannot activate remote tracking");
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssTestimonyTrack"));
            Check(g.AbyssTestimonyGuidedSite!=null,
                "Tracking legitimately activates at a physically reachable gate");
            g.save.abyssFloor=2;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,null);
            Check(g.AbyssTestimonyGuidedSite==null &&
                g.save.abyssFloor==2 &&
                (g.NavigationTarget().Value-new Point(31,14)).Length<.001f,
                "Floor transition cannot reuse a stale physical testimony or marker");
        }

        public static void AbyssTestimonyCompassFollowsRealUnvisitedSources(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.save.x=5;g.save.y=14;
            WorldObject near = g.AbyssTestimonyNextSite;
            Check(near != null && near.kind == "abyssSurvey" &&
                g.Objective().Contains(near.name),
                "The actual closest unsolved record appears in the mission guidance");
            WorldObject farEcho = Array.Find(g.Map.objects,
                o => o.kind == "abyssFarEcho");
            g.save.x = farEcho.x;g.save.y = farEcho.y;
            Check(g.AbyssTestimonyNextSite != farEcho,
                "An inaccessible echo transcript is never suggested before finding its source");
            g.save.flags.Add("abyss_annex_far_survey_1");
            Check(g.AbyssTestimonyNextSite == farEcho,
                "Decoding the source tablet unlocks the real echo as a compass candidate");
            g.save.flags.Add("abyss_annex_far_echo_1");
            Check(g.AbyssTestimonyNextSite != farEcho,
                "A collected echo is immediately removed from compass targets");
            g.save.flags.Add("abyss_annex_survey_1");
            g.save.flags.Add("abyss_annex_cache_1");
            g.save.flags.Add("abyss_annex_far_cache_1");
            Check(g.AbyssTestimonyProgress == 5,
                "The compass reads all five existing v2 proofs");
            g.save.flags.Add("abyss_annex_testimony_complete_1");
            Check(g.AbyssTestimonyNextSite == null,
                "Completed testimony trail never points to a nonexistent bonus target");
            g.enemies.Clear();g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.AbyssTestimonyNextSite == null &&
                restored.AbyssTestimonySealed,
                "SaveData v2 reload preserves completed compass targeting");
            restored.save.abyssFloor=2;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(restored,null);
            Check(restored.AbyssTestimonyNextSite != null &&
                restored.AbyssTestimonyProgress == 0,
                "A new seeded floor regenerates useful next-site navigation");
        }

        public static void AbyssTestimonyTrailRequiresFivePhysicalRecordsAndBanksOnlyAtGate(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            string[] kinds = {
                "abyssSurvey", "abyssCache", "abyssFarCache",
                "abyssFarSurvey", "abyssFarEcho"
            };
            WorldObject[] sites = new WorldObject[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                sites[i] = Array.Find(g.Map.objects, o => o.kind == kinds[i]);
                Check(sites[i] != null &&
                    g.CanStand(new Point(sites[i].x, sites[i].y)),
                    "Roadwarden record " + i + " is a physical walkable gallery object");
            }
            int coins = g.save.coins, shards = g.save.shards;
            g.save.x = sites[4].x;g.save.y = sites[4].y;
            g.Talk(sites[4]);g.Close();
            Check(g.AbyssTestimonyProgress == 0 && !g.AbyssTestimonySealed,
                "The echo stone refuses to decode before finding original southern testimony");

            for (int i = 0; i < kinds.Length; i++)
            {
                g.save.x = sites[i].x;g.save.y = sites[i].y;
                g.Talk(sites[i]);
                Check(g.AbyssTestimonyProgress == i + 1,
                    "Actual record visits advance a five-part exploration challenge");
                if (i < kinds.Length-1)
                    Check(!g.AbyssTestimonySealed,
                        "Partial trail progress never awards the final bonus");
                g.Close();
            }
            Check(g.AbyssTestimonySealed &&
                g.save.runCoins == 78 && g.save.runShards == 5 &&
                g.save.runXp == 85 &&
                g.save.coins == coins && g.save.shards == shards &&
                !g.Objective().Contains("巡路证词"),
                "All five physical records award exactly one unbanked completion bonus");
            g.save.x = sites[0].x;g.save.y = sites[0].y;
            g.Talk(sites[0]);g.Close();
            g.save.x = sites[4].x;g.save.y = sites[4].y;
            g.Talk(sites[4]);g.Close();
            Check(g.save.runCoins == 78 && g.save.runShards == 5 &&
                g.save.runXp == 85,
                "A repeated record visit cannot mint duplicate bonus loot");
            g.enemies.Clear();g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.InAbyss && loaded.AbyssTestimonyProgress == 5 &&
                loaded.AbyssTestimonySealed &&
                loaded.save.runCoins == 78 && loaded.save.runShards == 5 &&
                loaded.save.runXp == 85,
                "SaveData v2 restores all five records and the exact pending reward");
            loaded.mode = "world";
            WorldObject exit = Array.Find(loaded.Map.objects,
                o => o.kind == "abyssExit");
            loaded.save.x = exit.x;loaded.save.y = exit.y;
            loaded.Talk(exit);loaded.Choose(0);
            Check(!loaded.InAbyss && loaded.save.runCoins == 0 &&
                loaded.save.runShards == 0 && loaded.save.runXp == 0 &&
                loaded.save.coins == coins && loaded.save.shards == shards,
                "Early extraction forfeits all five-site testimony bonus earnings");
            loaded.StartAbyss(0);
            Check(loaded.InAbyss && loaded.AbyssTestimonyProgress == 0 &&
                !loaded.AbyssTestimonySealed,
                "A new expedition resets only temporary trail proof flags");
        }

        public static void AbyssStarrootCircuitRequiresThreeRealSeededRelayVisits(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            int width = g.Map.rows[0].Length;
            var reachable = Reachable(g);
            WorldObject[] relays = new WorldObject[3];
            for (int i = 0; i < 3; i++)
            {
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
                Check(relays[i] != null && g.CanStand(
                    new Point(relays[i].x, relays[i].y)) &&
                    reachable.Contains((int)relays[i].y * width + (int)relays[i].x),
                    "Each new starroot relay has a real traversable branch location");
            }
            g.enemies.Clear();
            g.save.runCoins = 5; g.save.runShards = 2; g.save.runXp = 3;
            g.save.shards = 99; g.save.energy = 100;
            int first = g.AbyssCircuitNextNode;
            Check(first >= 0 && first < 3 && g.AbyssCircuitProgress == 0 &&
                g.Objective().Contains("信标 " + (first == 0 ? "A" : first == 1 ? "B" : "C")),
                "The main task cue gives the actual first seeded target");
            g.save.x = 5; g.save.y = 14;
            g.Talk(relays[first]);
            Check(g.AbyssCircuitProgress == 0 && g.save.runCoins == 5,
                "Remote E interaction cannot fake a circuit node visit");
            int wrong = (first + 1) % 3;
            g.save.x = relays[wrong].x; g.save.y = relays[wrong].y;
            g.Talk(relays[wrong]);
            Check(g.AbyssCircuitProgress == 0 && g.save.energy == 88 &&
                g.save.runShards == 2,
                "Wrong seeded relay costs real energy and leaves rewards untouched");

            g.save.x = relays[first].x; g.save.y = relays[first].y;
            g.Talk(relays[first]);
            Check(g.AbyssCircuitProgress == 1 && g.AbyssCircuitVisited(first),
                "The first physically activated relay records progress");
            g.Talk(relays[first]);
            Check(g.AbyssCircuitProgress == 1,
                "Repeated interaction with an already-linked relay is harmless");
            // Flag-only partial progress must reconstruct in the existing v2
            // save schema without a new serialized field or new game mode.
            g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.AbyssCircuitProgress == 1 &&
                restored.AbyssCircuitNextNode == g.AbyssCircuitNextNode,
                "Mid-circuit SaveData v2 reload preserves the exact next seed target");
            restored.mode = "world";
            int step1 = restored.AbyssCircuitNextNode;
            WorldObject second = Array.Find(restored.Map.objects,
                o => o.kind == "abyssCircuit" + step1);
            restored.save.x = second.x; restored.save.y = second.y;
            restored.Talk(second);
            Check(restored.AbyssCircuitProgress == 2,
                "Second physical relay continues from the loaded partial path");
            int step2 = restored.AbyssCircuitNextNode;
            WorldObject third = Array.Find(restored.Map.objects,
                o => o.kind == "abyssCircuit" + step2);
            restored.save.x = third.x; restored.save.y = third.y;
            restored.Talk(third);
            Check(restored.AbyssCircuitComplete && restored.AbyssCircuitProgress == 3 &&
                restored.save.runCoins == 45 && restored.save.runShards == 6 &&
                restored.save.runXp == 33 && restored.save.shards == 99,
                "Three correct seeded visits award ONLY pending capped coins, shards and XP");
            restored.Talk(third);
            Check(restored.save.runCoins == 45 && restored.save.runShards == 6 &&
                restored.save.runXp == 33,
                "The final relay cannot mint duplicate pending treasure");

            // Extracting early must discard the optional loot, just like every
            // other genuine Abyss branch reward.
            restored.save.x = 3; restored.save.y = 14;
            WorldObject exit = Array.Find(restored.Map.objects,
                o => o.kind == "abyssExit");
            restored.Talk(exit); restored.Choose(0);
            Check(!restored.InAbyss && restored.save.runShards == 0 &&
                restored.save.shards == 99,
                "Early extraction loses pending circuit earnings, never permanent shards");
        }

        public static void LongAbyssCircuitReturnSnapsCameraWithoutBreakingWalkingSmoothing(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit2");
            g.save.x = relay.x; g.save.y = relay.y;
            Point before = g.Position;
            g.Talk(relay); g.Choose(0);
            Point target = g.Position;
            Check(WorldStreaming.LargeCameraJump(
                target.x-before.x,target.y-before.y),
                "Real long-gallery circuit return is classified as a room-scale camera jump");
            Point teleported = WorldStreaming.FollowCamera(before,target,.016f,false);
            Check(teleported.x == target.x && teleported.y == target.y &&
                WorldStreaming.LargeCameraJump(
                    teleported.x-before.x,teleported.y-before.y),
                "Production camera reaches the hero instantly and triggers terrain prewarm on teleport");
            Point beforeWalk = new Point(12,12);
            Point nextWalk = new Point(13,12.5f);
            Point smooth = WorldStreaming.FollowCamera(beforeWalk,nextWalk,.016f,false);
            Check(smooth.x > beforeWalk.x && smooth.x < nextWalk.x &&
                smooth.y > beforeWalk.y && smooth.y < nextWalk.y,
                "Normal walking still uses progressive 12Hz exponential camera tracking");
            Point still = WorldStreaming.FollowCamera(beforeWalk,nextWalk,0,false);
            Check(still.x == beforeWalk.x && still.y == beforeWalk.y,
                "A paused zero-delta frame does not drift the camera");
            Point accessibility = WorldStreaming.FollowCamera(beforeWalk,nextWalk,.016f,true);
            Check(accessibility.x == nextWalk.x && accessibility.y == nextWalk.y,
                "Reduced-motion camera retains immediate accessibility tracking");
        }

        public static void AbyssCircuitRouteRequiresClearedFloorAndPhysicalRelay(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit1");
            g.save.x = relay.x; g.save.y = relay.y;
            int initialRoom = g.save.room, initialShards = g.save.shards;
            g.save.runShards = 7;
            g.Talk(relay);
            Check(g.mode == "world" && g.save.x == relay.x,
                "Circuit alone cannot skip live enemies and offer an evacuation shortcut");
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_1");
            g.hazards.Add(new Hazard {
                position = new Point(relay.x, relay.y), timer = 1.5f,
                radius = 1.4f, damage = 10, kind = "rift"
            });
            g.Talk(relay);
            Check(g.mode == "dialog" &&
                g.conversation.choices.Length == 2 &&
                g.conversation.choices[0].action == "abyssCircuitGround" &&
                g.save.x == relay.x && g.save.runShards == 7,
                "A queued rift can offer emergency grounding but NEVER bypass the safe-return travel lock");
            g.Close();
            g.hazards.Clear();
            g.Talk(relay);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 5 &&
                g.conversation.choices[0].action == "abyssCircuitReturn",
                "A cleared relay offers the east gate and three live branch destinations");

            g.save.x = 5; g.save.y = 14;
            g.Choose(0);
            Check(g.save.x == 5 && g.save.y == 14 &&
                g.save.runShards == 7,
                "Walking away revokes a previously opened relay shortcut choice");
            g.save.x = relay.x; g.save.y = relay.y;
            g.Talk(relay); g.Choose(0);
            Check(g.mode == "world" && g.save.room == initialRoom &&
                Math.Abs(g.save.x - 30.5f) < .01f &&
                Math.Abs(g.save.y - 14.5f) < .01f &&
                g.save.runShards == 7 && g.save.shards == initialShards,
                "Fast return moves within the same dungeon but does not bank or alter any loot");
            WorldObject gate = Array.Find(g.Map.objects,
                o => o.kind == "abyssGate");
            g.Talk(gate);
            Check(g.mode == "dialog" && g.conversation.choices.Length > 0,
                "A relay shortcut reaches the REAL settlement gate interaction");
            // Choose the final 'bank/extract' option; no automatic rewards at
            // teleport time, only the existing protected BankFloor transaction.
            g.Choose(g.conversation.choices.Length - 1);
            Check(g.save.room == 0 && g.save.shards == initialShards + 7,
                "All pending shards bank only after personally using the root gate");
        }

        public static void StarrootRouteMenusCountUnvisitedLiveSideSites(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            int[] counts = new int[3];
            int total = 0;
            for (int i = 0; i < 3; i++)
            {
                counts[i] = g.AbyssCircuitUnvisitedSites(i);
                total += counts[i];
            }
            Check(total == 6,
                "The six real optional floor-one survey/cache/echo/rootwell world sites have branch assignments");
            // An existing SaveData v2 flag, not a new UI counter, is the
            // only authoritative source of a branch's remaining content.
            g.save.flags.Add("abyss_annex_survey_1");
            int reduced = 0;
            for (int i = 0; i < 3; i++)
                reduced += g.AbyssCircuitUnvisitedSites(i);
            Check(reduced == total - 1,
                "Actually recording a real survey immediately removes one route hint");

            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o=>o.kind=="abyssCircuit0");
            g.save.x=relay.x;g.save.y=relay.y;
            g.Talk(relay);
            Check(g.mode=="dialog" && g.conversation.choices.Length==5,
                "The linked physical relay offers its original five route choices");
            for (int i=0;i<3;i++)
            {
                int sites = g.AbyssCircuitUnvisitedSites(i);
                Check(g.conversation.choices[i+1].label.Contains("待探索 " + sites) &&
                    g.conversation.choices[i+1].en.Contains(sites + " sites left"),
                    "Branch " + i + " labels show actual pending sites, not invented rewards");
            }
            g.Close();
            var gate = Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.save.x=30.5f;g.save.y=14.5f;
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            Check(g.mode=="dialog" && g.conversation.choices.Length==4,
                "The real east gate submenu exposes the same live unvisited counters");
            for (int i=0;i<3;i++)
                Check(g.conversation.choices[i].label.Contains("待探索 " +
                    g.AbyssCircuitUnvisitedSites(i)),
                    "East gate and field relays derive branch counters from identical world objects");
            g.Close();g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));
            Check(reloaded.InAbyss && reloaded.AbyssCircuitComplete,
                "Optional route hints remain available after a legitimate v2 reload");
            for (int i=0;i<3;i++)
                Check(reloaded.AbyssCircuitUnvisitedSites(i)==
                    g.AbyssCircuitUnvisitedSites(i),
                    "Every branch's unvisited counter remains stable on v2 reload");

            var deep = Unlocked(data);deep.StartAbyss(0);
            deep.save.abyssFloor=3;
            typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.NonPublic|BindingFlags.Instance).Invoke(deep,null);
            int deepTotal=0;
            for (int i=0;i<3;i++) deepTotal+=deep.AbyssCircuitUnvisitedSites(i);
            Check(deepTotal==7,
                "Deep floors add one real unvisited rift ward site to six optional sources");
            deep.save.flags.Add("abyss_annex_ward_3");
            int tuned=0;
            for (int i=0;i<3;i++) tuned+=deep.AbyssCircuitUnvisitedSites(i);
            Check(tuned==deepTotal-1,
                "Purchasing the actual deep rift ward removes its site from the live route tally");
        }



        public static void StarrootPuzzleWaypointsTrackRealRelaysAcrossMistakesAndReloads(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            Point east = new Point(31, 14);
            Check((g.NavigationTarget().Value - east).Length < .001f &&
                g.AbyssCircuitGuidedNextRelay == -1,
                "Untouched optional circuit preserves the original east-gate guide");

            var relays = new WorldObject[3];
            for (int i = 0; i < 3; i++)
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
            Check(Array.TrueForAll(relays, o => o != null),
                "The puzzle uses the three actually placed game-world relays");
            int first = g.AbyssCircuitNextNode;
            int mistake = (first + 1) % 3;
            g.save.x = relays[mistake].x; g.save.y = relays[mistake].y;
            float startingEnergy = g.save.energy;
            g.Talk(relays[mistake]);
            Check(g.AbyssCircuitProgress == 0 &&
                g.AbyssCircuitGuidedNextRelay == first &&
                (g.NavigationTarget().Value -
                    new Point(relays[first].x, relays[first].y)).Length < .001f &&
                g.save.energy == Math.Max(0, startingEnergy - 12),
                "Wrong first relay charges the original energy penalty and guides the correct restart");

            g.save.x = relays[first].x; g.save.y = relays[first].y;
            g.Talk(relays[first]);
            int second = g.AbyssCircuitNextNode;
            Check(g.AbyssCircuitProgress == 1 &&
                second != first && g.AbyssCircuitGuidedNextRelay == second &&
                (g.NavigationTarget().Value -
                    new Point(relays[second].x, relays[second].y)).Length < .001f,
                "First correct physical interaction guides the next actual relay");

            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.AbyssCircuitProgress == 1 &&
                restored.AbyssCircuitGuidedNextRelay == second &&
                (restored.NavigationTarget().Value -
                    new Point(relays[second].x, relays[second].y)).Length < .001f,
                "Old SaveData v2 step flags resume the puzzle guide without a new field");

            for (int progress = 1; progress < 3; progress++)
            {
                int target = g.AbyssCircuitNextNode;
                g.save.x = relays[target].x; g.save.y = relays[target].y;
                g.Talk(relays[target]);
            }
            Check(g.AbyssCircuitComplete &&
                g.AbyssCircuitGuidedNextRelay == -1 &&
                (g.NavigationTarget().Value - east).Length < .001f &&
                g.save.runShards >= 4,
                "Real three-step completion returns to the gate and awards only its existing unbanked loot");
        }


        public static void StarrootGalleryGuideResolvesLockedEchoPrerequisite(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject echo = Array.Find(g.Map.objects,
                o => o.kind == "abyssFarEcho");
            WorldObject source = Array.Find(g.Map.objects,
                o => o.kind == "abyssFarSurvey");
            WorldObject gate = Array.Find(g.Map.objects,
                o => o.kind == "abyssGate");
            Check(echo != null && source != null &&
                g.AbyssTestimonyRecordState(echo) == 3,
                "The real optional echo begins locked behind its actual source tablet");

            string[] others = { "survey", "cache", "far_cache",
                "rootwell", "ward" };
            foreach (string key in others)
                g.save.flags.Add("abyss_annex_" + key + "_1");
            int branch = -1;
            float best = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                WorldObject relay = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
                float dx = relay.x - echo.x, dy = relay.y - echo.y;
                float length2 = dx * dx + dy * dy;
                if (length2 < best) { best = length2; branch = i; }
            }
            int banked = g.save.shards;
            int pending = g.save.runShards;
            g.save.x = gate.x; g.save.y = gate.y;
            g.Talk(gate); g.Choose(g.conversation.choices.Length - 2);
            g.Choose(branch);
            Point target = g.NavigationTarget().Value;
            Check(g.AbyssCircuitGuideNeedsSource &&
                (target - new Point(source.x, source.y)).Length < .001f &&
                (target - new Point(echo.x, echo.y)).Length > .001f &&
                g.save.shards == banked && g.save.runShards == pending,
                "Locked echo routes to the physically placed source without granting rewards");

            g.save.flags.Add("abyss_annex_far_survey_1");
            Check(!g.AbyssCircuitGuideNeedsSource &&
                (g.NavigationTarget().Value -
                    new Point(echo.x, echo.y)).Length < .001f &&
                g.AbyssTestimonyRecordState(echo) == 1,
                "After actual source progress the live guide returns to now-unlocked echo");
            g.save.flags.Add("abyss_annex_far_echo_1");
            Check(g.AbyssCircuitGuidedBranch == -1 &&
                (g.NavigationTarget().Value - new Point(31, 14)).Length < .001f,
                "Collecting echo completes the selected branch and restores exit guidance");
        }


        public static void StarrootGalleryRouteNeverPointsThroughSealedWalls(Campaign data)
        {
            var g=Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.flags.Add("abyss_clear_1");
            WorldObject survey=Array.Find(g.Map.objects,o=>o.kind=="abyssSurvey");
            WorldObject gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            Check(survey!=null && gate!=null,"Original survey and gate objects exist");
            var map=g.Map;
            string[] old=(string[])map.rows.Clone();
            int x=(int)Math.Floor(survey.x),y=(int)Math.Floor(survey.y);
            for(int dy=-1;dy<=1;dy++)
            for(int dx=-1;dx<=1;dx++)
            {
                if(dx==0&&dy==0)continue;
                int px=x+dx,py=y+dy;
                if(px<2||py<2||px>=map.rows[0].Length-2||
                    py>=map.rows.Length-2)continue;
                var row=map.rows[py].ToCharArray();
                row[px]='#';map.rows[py]=new string(row);
            }
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            int routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssCircuitRoutes");
            Check(routes>=0,"Original cleared-floor gate exposes its real route network");
            g.Choose(routes);
            g.Choose(0);
            Point? goal=g.NavigationTarget();
            Check(goal.HasValue &&
                (goal.Value-new Point(survey.x,survey.y)).Length>.01f &&
                g.AbyssCircuitUnvisitedSites(0)>0,
                "Gallery A never points at its sealed original survey but retains its uncollected proof");
            int visits=g.AbyssCircuitUnvisitedSites(0);
            for(int i=0;i<5;i++)
                Check(g.NavigationTarget().HasValue &&
                    (g.NavigationTarget().Value-goal.Value).Length<.001f &&
                    g.AbyssCircuitUnvisitedSites(0)==visits,
                    "Repeated production navigation reads are stable and do not mutate proof state");
            map.rows=old;
            // Reselecting the physical route is the explicit refresh when
            // a previously inaccessible passage has reopened.
            g.save.x=gate.x;g.save.y=gate.y;g.Talk(gate);
            routes=Array.FindIndex(g.conversation.choices,
                c=>c.action=="abyssCircuitRoutes");
            g.Choose(routes);g.Choose(0);
            Check(g.NavigationTarget().HasValue &&
                (g.NavigationTarget().Value-new Point(survey.x,survey.y)).Length<.001f,
                "A new real route selection restores survey guidance after terrain clears");
        }

        public static void StarrootSelectedRouteGuidesRealUnvisitedSites(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            Point exit = new Point(31, 14);
            g.save.x = gate.x; g.save.y = gate.y;
            Check((g.NavigationTarget().Value - exit).Length < .001f,
                "Before route selection the original gate remains the waypoint");
            int branch = -1;
            for (int i = 0; i < 3; i++)
                if (g.AbyssCircuitUnvisitedSites(i) >= 2) { branch = i; break; }
            Check(branch >= 0, "One floor-one branch has at least two real optional sites");
            int remaining = g.AbyssCircuitUnvisitedSites(branch);
            g.Talk(gate);
            g.Choose(g.conversation.choices.Length - 2);
            g.Choose(branch);
            Point first = g.NavigationTarget().Value;
            Check(g.AbyssCircuitGuidedBranch == branch,
                "The selected physical relay exposes its live branch ID to the world renderer");
            WorldObject site = Array.Find(g.Map.objects,
                o => (new Point(o.x, o.y) - first).Length < .001f);
            Check(site != null && (first - exit).Length > 1f &&
                g.AbyssCircuitUnvisitedSites(branch) == remaining,
                "Actual route travel points to a real gallery site without granting any loot");
            string suffix = site.kind == "abyssSurvey" ? "survey" :
                site.kind == "abyssCache" ? "cache" :
                site.kind == "abyssFarSurvey" ? "far_survey" :
                site.kind == "abyssFarCache" ? "far_cache" :
                site.kind == "abyssFarEcho" ? "far_echo" :
                site.kind == "abyssRootwell" ? "rootwell" :
                site.kind == "abyssRiftWard" ? "ward" : null;
            Check(suffix != null, "The target has a genuine existing optional-site flag");
            g.save.flags.Add("abyss_annex_" + suffix + "_1");
            Check(g.AbyssCircuitUnvisitedSites(branch) == remaining - 1 &&
                (g.NavigationTarget().Value - first).Length > .001f,
                "Claiming the site's existing v2 flag reroutes the waypoint immediately");
            string[] keys = { "survey", "cache", "far_survey", "far_cache",
                "far_echo", "rootwell", "ward" };
            foreach (string key in keys)
                g.save.flags.Add("abyss_annex_" + key + "_1");
            Check(g.AbyssCircuitUnvisitedSites(branch) == 0 &&
                g.AbyssCircuitGuidedBranch == -1 &&
                (g.NavigationTarget().Value - exit).Length < .001f,
                "A cleared branch falls back to the original gate, not an invented marker");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit" + branch);
            g.Talk(relay); g.Choose(0);
            Check((g.NavigationTarget().Value - exit).Length < .001f,
                "Travel back through the actual route clears the local gallery target");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss &&
                (restored.NavigationTarget().Value - exit).Length < .001f,
                "Existing v2 flags survive reload; transient guide intent does not persist");
        }

        public static void AbyssCircuitTravelNeverRelocatesUnreleasedCombatActions(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit2");
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            g.save.x=relay.x;g.save.y=relay.y;
            g.save.energy=100;g.mode="world";
            g.Attack(new Point(1,0));
            Check(g.playerAction != null && !g.playerAction.released,
                "A true player attack has an unreleased live contact");
            g.Talk(relay);
            Check(g.mode == "world" && g.save.x == relay.x,
                "Active player attack cannot open a free relocation menu");
            g.playerAction=null;
            g.companionAction=new ActorAction {
                kind="command",weapon=0,contact=.2f,duration=.4f
            };
            g.Talk(relay);
            Check(g.mode == "world" && g.save.x == relay.x,
                "An unreleased companion command cannot travel across the map");
            g.companionAction=null;
            g.Talk(relay);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 5,
                "Finishing all combat contacts unlocks the legitimate completed-circuit routes");
            // An action may become pending AFTER the route menu was opened;
            // the choice must revalidate current release state.
            g.playerAction=new ActorAction {
                kind="attack",weapon=0,contact=.2f,duration=.4f
            };
            g.Choose(0);
            Check(Math.Abs(g.save.x-relay.x)<.01f &&
                Math.Abs(g.save.y-relay.y)<.01f &&
                g.mode=="world",
                "Choice-time gate check prevents an old menu from moving live windup attacks");
            g.playerAction=null;
            g.Talk(relay);g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                Math.Abs(g.save.y-14.5f)<.01f,
                "Once attacks end, the exact same legal route becomes usable");
            g.playerAction=new ActorAction {
                kind="attack",weapon=0,contact=.2f,duration=.4f
            };
            g.Talk(gate);
            Check(g.mode=="dialog" &&
                !Array.Exists(g.conversation.choices,
                    c=>c.action=="abyssCircuitRoutes"),
                "The east gate does not advertise a circuit transfer during live combat animation");
            g.Close();
            g.playerAction=null;
            g.Talk(gate);
            Check(g.mode=="dialog" &&
                Array.Exists(g.conversation.choices,
                    c=>c.action=="abyssCircuitRoutes"),
                "The protected final banking gate resumes normal network travel after recovery");
        }

        public static void StarrootCircuitRecallsHeldCompanionOnlyOnExplicitSafeChoice(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            var a=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit0");
            var c=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit2");
            var gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate");
            g.mode="world";g.save.x=a.x;g.save.y=a.y;
            g.SelectCompanion(1);
            g.companionCooldown=9f;
            g.ToggleCompanionHold();
            Point anchor=g.companionPosition;
            Check(g.CompanionHoldingPosition,"Original companion receives a real hold order");
            g.Talk(a);g.Choose(3); // Hero travels to relay C.
            Check((g.companionPosition-anchor).Length<.001f&&
                g.CompanionHoldingPosition,
                "The hold remains physical and intact after solo hero transfer");
            g.Talk(c);
            Check(g.conversation.choices.Length==6 &&
                g.conversation.choices[4].action=="abyssCircuitRecall",
                "A held companion adds exactly one explicit recall choice to relay menu");
            g.save.x=5;g.save.y=14;g.Choose(4);
            Check(g.CompanionHoldingPosition &&
                (g.companionPosition-anchor).Length<.001f,
                "Old remote menu cannot teleport a held companion to the hero");
            g.save.x=c.x;g.save.y=c.y;g.Talk(c);
            int generation=g.companionPresentationGeneration;
            g.Choose(4);
            Check(!g.CompanionHoldingPosition &&
                g.companionPresentationGeneration==generation+1 &&
                (g.companionPosition-g.Position).Length<4f &&
                (g.companionPosition-g.Position).Length>.55f &&
                g.CanStand(g.companionPosition)&&
                Math.Abs(g.companionCooldown-9f)<.001f &&
                g.companionMoveTime==0f&&!g.companionMoving,
                "Explicit relay recall rejoins a real legal foot and rebases gait without skill refresh");

            // Also expose recall at the actual settlement GATE, without
            // ever moving the hero or automatically banking their loot.
            g.ToggleCompanionHold();
            Point heldAtC=g.companionPosition;
            g.Talk(c);g.Choose(0);
            Check(g.CompanionHoldingPosition &&
                (g.companionPosition-heldAtC).Length<.001f &&
                Math.Abs(g.save.x-30.5f)<.01f,
                "A second deliberate hold survives a different long route");
            g.save.runShards=6;g.save.runCoins=17;
            int banked=g.save.shards;
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            Check(g.conversation.choices.Length==5 &&
                g.conversation.choices[3].action=="abyssCircuitRecall" &&
                g.conversation.choices[4].action=="abyssCircuitGateMenu",
                "Gate submenu offers recall only while a real ally remains stationed");
            // A hostile that arrives after opening the route UI must still
            // block both ally recalls and hero transfers at choice-time.
            g.enemies.Add(new Enemy(938,0,31.5f,14.5f,25f));
            g.Choose(3);
            Check(g.CompanionHoldingPosition &&
                (g.companionPosition-heldAtC).Length<.001f &&
                g.save.runShards==6,
                "Combat threat arising after menu-open cannot recall a stationed ally");
            g.enemies.Clear();
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            g.Choose(3);
            Check(!g.CompanionHoldingPosition &&
                (g.companionPosition-g.Position).Length<4f &&
                g.CanStand(g.companionPosition) &&
                g.save.runCoins==17&&g.save.runShards==6&&
                g.save.shards==banked,
                "Safe gate recall restores formation without creating or banking loot");
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            Check(g.conversation.choices.Length==4 &&
                g.conversation.choices[3].action=="abyssCircuitGateMenu",
                "After recall, the extra choice disappears and original route options remain");
        }

        public static void StarrootFastTravelBringsUnheldCompanionWithoutFakeGait(Campaign data)
        {
            var g = Unlocked(data);g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            var a=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit0");
            var b=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit1");
            var c=Array.Find(g.Map.objects,o=>o.kind=="abyssCircuit2");
            Check(a!=null&&b!=null&&c!=null,
                "Party co-travel uses actual three world-placed relay stops");
            g.mode="world";g.save.x=a.x;g.save.y=a.y;
            g.SelectCompanion(0);
            Check(g.save.companion==0 &&
                (g.companionPosition-g.Position).Length<4f,
                "Recruited companion begins in the real original formation");
            g.companionCooldown=8f;
            g.companionMoveTime=7.2f;
            g.companionMoving=true;
            int generation=g.companionPresentationGeneration;
            g.Talk(a);g.Choose(3); // The third branch destination is C.
            Check(Math.Abs(g.save.x-c.x)<.01f &&
                Math.Abs(g.save.y-c.y)<.01f &&
                g.CanStand(g.companionPosition) &&
                (g.companionPosition-g.Position).Length>.55f &&
                (g.companionPosition-g.Position).Length<4f,
                "Relay A->C physically rejoins the companion at the new safe hero foot");
            Check(g.companionPresentationGeneration==generation+1 &&
                g.companionMoveTime==0f && !g.companionMoving &&
                Math.Abs(g.companionCooldown-8f)<.001f,
                "Fast party transfer rebases genuine full-body gait without refreshing skills");

            // Explicit companion hold is a real player order: the hero may
            // still travel, but the planted ally must NOT get dragged away.
            g.ToggleCompanionHold();
            Check(g.CompanionHoldingPosition,
                "A party member can receive a physical hold order at relay C");
            Point holdFoot=g.companionPosition;
            int holdGeneration=g.companionPresentationGeneration;
            g.Talk(c);g.Choose(2); // C->B
            Check(Math.Abs(g.save.x-b.x)<.01f &&
                Math.Abs(g.save.y-b.y)<.01f &&
                (g.companionPosition-holdFoot).Length<.001f &&
                g.CompanionHoldingPosition &&
                g.companionPresentationGeneration==holdGeneration,
                "Fast travel preserves deliberate hold and original planted companion gait");
            g.ToggleCompanionHold();
            Check(!g.CompanionHoldingPosition,
                "Explicit resume-follow cancels the held stance");
            g.Talk(b);g.Choose(1); // B->A
            Check(Math.Abs(g.save.x-a.x)<.01f &&
                (g.companionPosition-g.Position).Length<4f &&
                g.companionPresentationGeneration==holdGeneration+1 &&
                g.companionMoveTime==0f,
                "Next legal long transfer rejoins a formerly stationed party member");

            // Companions are optional. Fast travel must not fabricate a
            // body or advance the animation generation without an ally.
            g.SelectCompanion(-1);
            Check(g.save.companion==-1,"Companion can be dismissed normally");
            int soloGeneration=g.companionPresentationGeneration;
            g.Talk(a);g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                g.companionPresentationGeneration==soloGeneration,
                "Solo relay transfer preserves no-companion presentation state");
            g.SelectCompanion(2);
            Check(g.save.companion==2,"The party can choose another valid ally");
            g.save.runShards=4;
            g.PrepareSave();
            var restored = new GameCore(data,CloneSave(g.save));
            Check(restored.InAbyss && restored.save.companion==2 &&
                (restored.companionPosition-restored.Position).Length<4f &&
                restored.save.runShards==4,
                "SaveData v2 reload puts the chosen ally beside the real arrival without changing loot");
        }

        public static void CompletedAbyssCircuitAllowsSafeGateToGalleryRoundTrip(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runCoins = 43; g.save.runShards = 11;
            g.save.runXp = 24;
            int coins = g.save.coins, shards = g.save.shards;
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            WorldObject[] relays = new WorldObject[3];
            for (int i=0;i<3;i++)
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
            g.save.x=30.5f;g.save.y=14.5f;
            g.Talk(gate);
            Check(g.mode == "dialog" &&
                g.conversation.choices[g.conversation.choices.Length-2].action ==
                    "abyssCircuitRoutes" &&
                g.conversation.choices[g.conversation.choices.Length-1].action ==
                    "abyssBank",
                "Original bank action stays LAST when east gate exposes extra circuit route");
            g.Choose(g.conversation.choices.Length-2);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 4 &&
                g.conversation.choices[0].action == "abyssCircuitTo0" &&
                g.conversation.choices[3].action == "abyssCircuitGateMenu",
                "East gate opens a physically sourced three-destination route sub-menu");
            g.save.x=5;g.save.y=14;
            g.Choose(2);
            Check(g.save.x == 5 && g.save.y == 14 &&
                g.save.runShards == 11,
                "Leaving the gate revokes its route sub-menu and prevents remote transfer");

            for (int destination=0;destination<3;destination++)
            {
                g.save.x=30.5f;g.save.y=14.5f;g.Talk(gate);
                g.Choose(g.conversation.choices.Length-2);
                g.Choose(destination);
                Check(g.save.room == 8 &&
                    Math.Abs(g.save.x-relays[destination].x)<.01f &&
                    Math.Abs(g.save.y-relays[destination].y)<.01f &&
                    g.save.runCoins == 43 && g.save.runShards == 11 &&
                    g.save.runXp == 24 && g.save.coins == coins &&
                    g.save.shards == shards,
                    "The real east gate reaches gallery " + destination +
                    " while preserving unbanked and permanent economy");
                g.Talk(relays[destination]);g.Choose(0);
                Check(Math.Abs(g.save.x-30.5f)<.01f &&
                    Math.Abs(g.save.y-14.5f)<.01f,
                    "Route from gallery " + destination +
                    " returns to the same original settlement gate");
            }
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            // A hostile can enter after the player has opened the route UI.
            // Re-evaluate threat state when they actually choose the stop.
            var enemy = new Enemy(910, 0, 31.5f,14.5f,28f);
            g.enemies.Add(enemy);
            g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                g.save.runShards == 11,
                "A newly spawned enemy blocks travel even from an already-open gate route menu");
            g.enemies.Clear();
            g.Talk(gate);g.Choose(g.conversation.choices.Length-2);
            g.Choose(3);
            Check(g.mode == "dialog" &&
                g.conversation.choices[g.conversation.choices.Length-1].action ==
                    "abyssBank",
                "Route sub-menu returns safely to the original settlement dialogue");
            g.Choose(g.conversation.choices.Length-1);
            Check(g.save.room==0 && g.save.shards==shards+11 &&
                g.save.coins==coins+43,
                "Only the original east gate bank action makes final loot permanent");
        }

        public static void ClearedAbyssCircuitRoutesAmongPhysicalRelaysWithoutBanking(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(2);
            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runShards = 8; g.save.runCoins = 31; g.save.runXp = 27;
            int coins = g.save.coins, shards = g.save.shards;
            WorldObject[] relays = new WorldObject[3];
            for (int i = 0; i < 3; i++)
            {
                relays[i] = Array.Find(g.Map.objects,
                    o => o.kind == "abyssCircuit" + i);
                Check(relays[i] != null &&
                    g.CanStand(new Point(relays[i].x, relays[i].y)),
                    "Branch destination " + i + " is a real standable world object");
            }
            g.save.x = relays[0].x; g.save.y = relays[0].y;
            g.Talk(relays[0]);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 5,
                "Completed safe circuit exposes all three navigable branches and the gate");

            g.save.x = 5; g.save.y = 14; g.Choose(3);
            Check(g.save.x == 5 && g.save.y == 14 &&
                g.save.runShards == 8,
                "A remote stale destination choice cannot teleport the hero");
            for (int target = 0; target < 3; target++)
            {
                int from = (target + 2) % 3;
                g.save.x = relays[from].x; g.save.y = relays[from].y;
                g.Talk(relays[from]); g.Choose(target + 1);
                Check(g.save.room == 8 &&
                    Math.Abs(g.save.x-relays[target].x) < .01f &&
                    Math.Abs(g.save.y-relays[target].y) < .01f &&
                    g.save.runShards == 8 && g.save.runCoins == 31 &&
                    g.save.runXp == 27 && g.save.shards == shards &&
                    g.save.coins == coins,
                    "Travel to branch " + target +
                    " preserves room, all pending loot and permanent economy");
            }
            g.save.x = relays[0].x; g.save.y = relays[0].y;
            g.Talk(relays[0]); g.Choose(2);
            Check(Math.Abs(g.save.x-relays[1].x)<.01f,
                "The middle relay remains a repeatable physical destination");
            g.projectiles.Add(new Projectile {
                friendly = false, position = new Point(relays[1].x,relays[1].y),
                velocity = new Point(1,0), life = 1, damage = 5
            });
            g.Talk(relays[1]);
            Check(g.mode == "world" &&
                Math.Abs(g.save.x-relays[1].x)<.01f,
                "Hostile projectiles block both the branch network and the gate route");
            g.projectiles.Clear();
            g.Talk(relays[1]); g.Choose(0);
            Check(Math.Abs(g.save.x-30.5f)<.01f &&
                Math.Abs(g.save.y-14.5f)<.01f &&
                g.save.shards == shards && g.save.runShards == 8,
                "A linked relay still takes the hero to the real gate without banking");
            var gate = Array.Find(g.Map.objects, o => o.kind == "abyssGate");
            g.Talk(gate); g.Choose(g.conversation.choices.Length-1);
            Check(g.save.room == 0 && g.save.shards == shards + 8,
                "Only the original gate banking transaction makes circuit loot permanent");
        }

        public static void CompletedAbyssCircuitCanGroundOnlyLocalRiftsForPendingLoot(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear();g.hazards.Clear();
            g.save.flags.Add("abyss_clear_1");
            g.save.flags.Add("abyss_annex_circuit_complete_1");
            g.save.runShards=3;g.save.shards=19;
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit0");
            Hazard local = new Hazard {
                position = new Point(relay.x,relay.y),radius=1.4f,
                timer=1.5f,damage=15,kind="rift"
            };
            Hazard other = new Hazard {
                position = new Point(relay.x,relay.y),radius=1.4f,
                timer=1.5f,damage=15,kind="bell"
            };
            Hazard distant = new Hazard {
                position = new Point(5,14),radius=1.4f,
                timer=1.5f,damage=15,kind="rift"
            };
            g.hazards.Add(local);g.hazards.Add(other);g.hazards.Add(distant);
            g.save.x=relay.x;g.save.y=relay.y;
            g.Talk(relay);
            Check(g.mode=="dialog"&&g.conversation.choices.Length==2,
                "A completed relay presents a resource-funded emergency grounding during live warnings");
            g.save.x=5;g.save.y=14;
            g.Choose(0);
            Check(g.save.runShards==3&&g.hazards.Count==3,
                "Leaving relay reach voids the old menu without spending loot or erasing attacks");
            g.save.x=relay.x;g.save.y=relay.y;
            g.Talk(relay);g.Choose(0);
            Check(g.Has("abyss_annex_circuit_ground_1") &&
                g.save.runShards==1&&g.save.shards==19&&
                !g.hazards.Contains(local)&&g.hazards.Contains(other)&&
                g.hazards.Contains(distant),
                "Grounding spends exactly two unbanked shards and retires ONLY local genuine rift warnings");
            int revision=g.revision;
            g.Talk(relay);
            Check(g.mode=="world"&&g.save.runShards==1&&
                g.hazards.Contains(distant)&&g.revision==revision,
                "An already-spent grounding charge cannot erase a second rift");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            Check(restored.InAbyss&&restored.Has("abyss_annex_circuit_ground_1")&&
                restored.save.runShards==1,
                "Spent emergency grounding persists across a standard SaveData v2 reload");

            var empty=Unlocked(data);empty.StartAbyss(0);
            empty.enemies.Clear();empty.hazards.Clear();
            empty.save.flags.Add("abyss_clear_1");
            empty.save.flags.Add("abyss_annex_circuit_complete_1");
            WorldObject emptyRelay=Array.Find(empty.Map.objects,
                o=>o.kind=="abyssCircuit0");
            empty.save.x=emptyRelay.x;empty.save.y=emptyRelay.y;
            empty.save.runShards=0;empty.save.shards=200;
            empty.hazards.Add(new Hazard{
                position=new Point(emptyRelay.x,emptyRelay.y),
                timer=1.5f,radius=1.4f,damage=15,kind="rift"
            });
            empty.Talk(emptyRelay);
            Check(empty.mode=="world"&&empty.hazards.Count==1&&
                empty.save.shards==200&&
                !empty.Has("abyss_annex_circuit_ground_1"),
                "Permanent banked shards cannot finance a grounding without unbanked loot");
        }

        public static void MiswiredDeepAbyssCircuitCreatesDodgeableRealBacklash(Campaign data)
        {
            var deep = Unlocked(data); deep.StartAbyss(2);
            var generator = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.NonPublic | BindingFlags.Instance);
            deep.save.abyssFloor = 3;
            generator.Invoke(deep,null);
            deep.enemies.Clear(); deep.hazards.Clear();
            int expected = deep.AbyssCircuitNextNode;
            int wrongNode = (expected + 1) % 3;
            WorldObject wrong = Array.Find(deep.Map.objects,
                o => o.kind == "abyssCircuit" + wrongNode);
            deep.save.x = wrong.x; deep.save.y = wrong.y;
            deep.save.energy = 100;
            deep.Talk(wrong);
            Hazard backlash = deep.hazards.Find(h => h.kind == "rift");
            Check(backlash != null && deep.save.energy == 88 &&
                deep.AbyssCircuitProgress == 0 &&
                Math.Abs(backlash.timer - 1.6f) < .001f &&
                Math.Abs(backlash.radius - 1.35f) < .001f &&
                Math.Abs(backlash.damage - 18f) < .001f,
                "Incorrect deep relay uses a real 1.6s telegraphed rift and original energy reset");

            deep.invulnerable = 0; deep.save.health = deep.save.maxHealth;
            float before = deep.save.health;
            for (int i = 0; i < 20; i++) deep.Tick(.05f,new Point());
            Check(deep.save.health == before && deep.hazards.Contains(backlash),
                "Early reaction frames do not hit before the real rift warning completes");
            for (int i = 0; i < 15; i++) deep.Tick(.05f,new Point());
            Check(deep.save.health < before && !deep.hazards.Contains(backlash),
                "An undodged wrong-order rift delivers real HP damage and retires its warning");

            var dodge = Unlocked(data); dodge.StartAbyss(0);
            dodge.save.abyssFloor = 3; generator.Invoke(dodge,null);
            dodge.enemies.Clear(); dodge.hazards.Clear();
            WorldObject dnode = Array.Find(dodge.Map.objects,
                o => o.kind == "abyssCircuit" + ((dodge.AbyssCircuitNextNode + 1) % 3));
            dodge.save.x = dnode.x; dodge.save.y = dnode.y;
            dodge.Talk(dnode);
            Check(dodge.hazards.Exists(h => h.kind == "rift"),
                "Wrong node creates the normal visible warning in a second run");
            dodge.save.x = 5; dodge.save.y = 14;
            dodge.save.health = dodge.save.maxHealth;
            dodge.invulnerable = 0;
            for (int i = 0; i < 40; i++) dodge.Tick(.05f,new Point());
            Check(dodge.save.health == dodge.save.maxHealth,
                "Leaving the marked circle before contact avoids ALL backlash damage");

            var early = Unlocked(data); early.StartAbyss(0);
            early.enemies.Clear();early.hazards.Clear();
            WorldObject earlyWrong = Array.Find(early.Map.objects,
                o => o.kind == "abyssCircuit" + ((early.AbyssCircuitNextNode + 1) % 3));
            early.save.x = earlyWrong.x; early.save.y = earlyWrong.y;
            early.save.energy = 100; early.Talk(earlyWrong);
            Check(early.save.energy == 88 && early.hazards.Count == 0,
                "Accessible floor-one puzzle retains energy-only teaching penalty");
        }

        public static void LinkedAbyssRelaysShelterAgainstRealRiftContacts(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var u = Unlocked(data); u.StartAbyss(0);
            var generate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (GameCore hero in new [] { g, u })
            {
                hero.save.abyssFloor = 3; hero.save.abyssSeed = 139;
                generate.Invoke(hero, null);
                hero.enemies.Clear(); hero.hazards.Clear();
                hero.save.flags.Add("abyss_clear_3");
                hero.mode = "world"; hero.invulnerable = 0;
                hero.save.health = hero.save.maxHealth;
            }
            int node = g.AbyssCircuitNextNode;
            WorldObject relay = Array.Find(g.Map.objects,
                o => o.kind == "abyssCircuit" + node);
            Check(relay != null && g.CanStand(new Point(relay.x,relay.y)),
                "Shelter fixture uses a genuine standable seeded relay");
            g.save.x = u.save.x = relay.x;
            g.save.y = u.save.y = relay.y;
            Check(!g.AbyssCircuitSheltered && !u.AbyssCircuitSheltered,
                "Unconnected shrines do not provide a free defensive aura");
            g.save.flags.Add("abyss_annex_circuit_3_0");
            Check(g.AbyssCircuitSheltered && !u.AbyssCircuitSheltered &&
                g.Conditions().Contains("信标庇护"),
                "Physically linked circuit relay provides a visible tactical shelter");
            float hp = g.save.health;
            foreach (GameCore hero in new [] { g, u })
                hero.hazards.Add(new Hazard {
                    position = new Point(relay.x,relay.y),
                    radius = 1.2f, timer = .02f, damage = 20,kind = "rift"
                });
            g.Tick(.05f,new Point()); u.Tick(.05f,new Point());
            float shelteredDamage = hp - g.save.health;
            float exposedDamage = hp - u.save.health;
            Check(shelteredDamage > 0 && exposedDamage > shelteredDamage &&
                Math.Abs(shelteredDamage / exposedDamage - .45f) < .01f,
                "A REAL ground rift contact deals exactly 55% less damage within the connected shelter");
            Check(g.hazards.Count == 0 && u.hazards.Count == 0,
                "Both actual hazards complete their ordinary contact and retirement");

            // A different kind of real ground contact must never benefit.
            g.invulnerable = u.invulnerable = 0;
            g.save.health = u.save.health = hp;
            foreach (GameCore hero in new [] { g, u })
                hero.hazards.Add(new Hazard {
                    position = new Point(relay.x,relay.y),
                    radius = 1.2f,timer = .02f,damage = 20,kind = "bell"
                });
            g.Tick(.05f,new Point()); u.Tick(.05f,new Point());
            Check(Math.Abs(g.save.health - u.save.health) < .001f,
                "The circuit protects against Rift contacts only, never unrelated Bell or boss hazards");

            // Old v2 data stores flags but no new serialized object state.
            g.PrepareSave();
            var reload = new GameCore(data,CloneSave(g.save));
            Check(reload.InAbyss && reload.AbyssCircuitSheltered,
                "Shelter is reconstructed from the ordinary v2 partial-circuit flag and position");
            g.save.room = 0;
            Check(!g.AbyssCircuitSheltered,
                "Travel out of the Abyss cannot retain a remote shield");
        }

        public static void AbyssCircuitIncompleteV2FlagsRecoverWithoutFreeRewards(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            g.enemies.Clear(); g.save.flags.Add("abyss_clear_1");
            g.save.runCoins = 1; g.save.runShards = 1; g.save.runXp = 1;
            // Simulate a partially written flags list: three step markers
            // persist, but the final completion transaction did not.
            for (int step = 0; step < 3; step++)
                g.save.flags.Add("abyss_annex_circuit_1_" + step);
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && !restored.AbyssCircuitComplete &&
                restored.AbyssCircuitProgress == 2 &&
                restored.AbyssCircuitNextNode >= 0 &&
                restored.AbyssCircuitNextNode <= 2 &&
                restored.Conditions().Contains("回路 2/3") &&
                restored.Objective().Contains("信标"),
                "An interrupted v2 completion never indexes relay four or crashes the HUD");
            int thirdId = restored.AbyssCircuitNextNode;
            WorldObject third = Array.Find(restored.Map.objects,
                o => o.kind == "abyssCircuit" + thirdId);
            restored.mode = "world";
            restored.save.x = third.x; restored.save.y = third.y;
            restored.Talk(third);
            Check(restored.AbyssCircuitComplete &&
                restored.save.runCoins == 41 &&
                restored.save.runShards == 5 &&
                restored.save.runXp == 31,
                "Revisiting the actual final relay repairs completion and credits only one normal payout");
            restored.Talk(third);
            Check(restored.save.runCoins == 41 && restored.save.runShards == 5,
                "Repaired legacy circuit cannot be claimed a second time");
        }

        public static void AbyssCircuitSeedPermutationAndResetAreDeterministic(Campaign data)
        {
            var g = Unlocked(data); g.StartAbyss(0);
            var regenerate = typeof(GameCore).GetMethod("GenerateAbyss",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var starts = new HashSet<int>();
            for (int seed = 100; seed < 106; seed++)
            {
                g.save.abyssSeed = seed;
                regenerate.Invoke(g, null);
                starts.Add(g.AbyssCircuitNextNode);
                Check(g.AbyssCircuitProgress == 0,
                    "Changing the seed does not grant unsolved circuit progress");
                int a = g.AbyssCircuitNextNode;
                // Each valid seed defines a genuine permutation: targets
                // exhaust A/B/C exactly once without repetition.
                g.save.flags.Add("abyss_annex_circuit_1_0");
                int b = g.AbyssCircuitNextNode;
                g.save.flags.Add("abyss_annex_circuit_1_1");
                int c = g.AbyssCircuitNextNode;
                Check(a != b && a != c && b != c,
                    "Each seeded circuit has three distinct relay targets");
                g.save.flags.Remove("abyss_annex_circuit_1_0");
                g.save.flags.Remove("abyss_annex_circuit_1_1");
            }
            Check(starts.Count == 3, "Six seed variants distribute starting nodes across all three caves");
            g.save.abyssSeed = int.MaxValue;
            regenerate.Invoke(g, null);
            Check(g.AbyssCircuitNextNode >= 0 && g.AbyssCircuitNextNode <= 2,
                "A maximum legal legacy seed cannot overflow the circuit permutation");
        }

        public static void AbyssRootwellTradesPendingLootForSurvival(Campaign data)
        {
            var g = Unlocked(data);
            g.StartAbyss(1);
            WorldObject well = Array.Find(g.Map.objects, o => o.kind == "abyssRootwell");
            Check(well != null && g.CanStand(new Point(well.x, well.y)),
                "The optional rootwell is a real standable object in the Abyss");
            var reachable = Reachable(g);
            int width = g.Map.rows[0].Length;
            Check(reachable.Contains((int)well.y * width + (int)well.x),
                "The new rootwell is walkably connected to the original entrance");

            g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear();
            g.save.health = 20; g.save.energy = 25;
            g.save.runShards = 3; g.save.shards = 40;
            Check(g.Objective().Contains("复苏泉"),
                "Low-health delvers with pending shards see the optional rootwell route");
            int coins = g.save.coins, revision = g.revision;
            float health = g.save.health;
            g.Talk(well);
            Check(g.mode == "world" && g.toast.Contains("走近") &&
                g.save.runShards == 3 && g.save.health == health,
                "Remote interaction cannot open a rootwell restoration menu");

            g.save.x = well.x; g.save.y = well.y;
            g.Talk(well);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 3,
                "The rootwell offers two distinct survival trades or decline");
            g.save.x = 5; g.save.y = 14;
            g.Choose(0);
            Check(g.save.runShards == 3 && g.save.health == health &&
                g.mode == "world",
                "A stale restoration choice cannot spend shards after walking away");

            g.save.x = well.x; g.save.y = well.y;
            g.Talk(well); g.Choose(0);
            Check(g.Has("abyss_annex_rootwell_1") &&
                g.save.runShards == 1 && g.save.shards == 40 &&
                g.save.coins == coins &&
                Math.Abs(g.save.health - Math.Min(g.save.maxHealth,
                    health + .4f * g.save.maxHealth)) < .001f &&
                g.save.energy == 25 && g.revision > revision,
                "Health recovery trades exactly two pending shards for 40% max HP");
            g.Talk(well);
            Check(g.mode == "dialog" && g.conversation.choices.Length == 1,
                "An exhausted font cannot be purchased again on the same floor");
            g.Close();
            Check(!g.Objective().Contains("复苏泉"),
                "The optional rootwell hint disappears after its charge is spent");
            // Real enemy-clear progression sets this flag at the final kill;
            // an empty unfinished floor intentionally invalidates unbanked
            // loot when reconstructed and cannot model a legitimate save.
            g.save.flags.Add("abyss_clear_1");
            g.PrepareSave();
            var restored = new GameCore(data, CloneSave(g.save));
            Check(restored.InAbyss && restored.Has("abyss_clear_1") &&
                restored.Has("abyss_annex_rootwell_1") &&
                restored.save.runShards == 1 &&
                Math.Abs(restored.save.health - g.save.health) < .001f,
                "SaveData v2 restores spent charge and precisely bankable rewards");

            var energy = Unlocked(data); energy.StartAbyss(0);
            var energyWell = Array.Find(energy.Map.objects, o => o.kind == "abyssRootwell");
            energy.enemies.Clear(); energy.hazards.Clear(); energy.projectiles.Clear();
            energy.save.x = energyWell.x; energy.save.y = energyWell.y;
            energy.save.runShards = 1; energy.save.shards = 19;
            energy.save.energy = 15; energy.save.health = 38;
            energy.Talk(energyWell); energy.Choose(1);
            Check(energy.save.energy == 70 && energy.save.health == 38 &&
                energy.save.runShards == 0 && energy.save.shards == 19 &&
                energy.Has("abyss_annex_rootwell_1"),
                "Energy recovery trades one pending shard for 55 energy");

            var empty = Unlocked(data); empty.StartAbyss(2);
            var emptyWell = Array.Find(empty.Map.objects, o => o.kind == "abyssRootwell");
            empty.enemies.Clear(); empty.hazards.Clear(); empty.projectiles.Clear();
            empty.save.x = emptyWell.x; empty.save.y = emptyWell.y;
            empty.save.runShards = 0; empty.save.shards = 200;
            empty.save.health = 12;
            empty.Talk(emptyWell); empty.Choose(0);
            Check(empty.save.health == 12 && empty.save.shards == 200 &&
                empty.save.runShards == 0 && !empty.Has("abyss_annex_rootwell_1"),
                "Permanent shards cannot bypass the unbanked loot cost");
            empty.save.runShards = 2; empty.save.health = empty.save.maxHealth;
            empty.Talk(emptyWell); empty.Choose(0);
            Check(empty.save.runShards == 2 && !empty.Has("abyss_annex_rootwell_1"),
                "Full health cannot consume shards or the floor's only charge");
            empty.save.health = 20;
            empty.enemies.Add(new Enemy(902, 0, emptyWell.x + 1, emptyWell.y, 35));
            empty.Talk(emptyWell);
            Check(empty.mode == "world" && empty.save.runShards == 2 &&
                !empty.Has("abyss_annex_rootwell_1"),
                "Live nearby combat forbids using the rootwell as an instant heal");
        }

        public static void AbyssHonorsProtectedMissionTravelLocks(Campaign data)
        {
            var locked = Unlocked(data);
            locked.Enter(6);
            Check(locked.save.room == 6, "The protected-mission fixture starts in the unlocked highland region");

            FieldInfo tuning = typeof(GameCore).GetField("stormTuning",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(tuning != null, "The civic-grid tuning state remains an explicit runtime field");
            tuning.SetValue(locked, true);

            int room = locked.save.room, floor = locked.save.abyssFloor;
            int seed = locked.save.abyssSeed, coins = locked.save.runCoins;
            int shards = locked.save.runShards, xp = locked.save.runXp;
            int revision = locked.revision;
            locked.Enter(0);
            Check(locked.save.room == room && (bool)tuning.GetValue(locked) &&
                locked.toast.Contains("调谐"),
                "Portal/map travel preserves protected civic-grid tuning and explains the lock");
            locked.ReturnToTown();
            Check(locked.save.room == room && (bool)tuning.GetValue(locked) &&
                locked.toast.Contains("调谐"),
                "Quick return preserves protected civic-grid tuning and explains the lock");
            locked.StartAbyss(1);
            Check(locked.save.room == room && locked.save.abyssFloor == floor &&
                locked.save.abyssSeed == seed && locked.save.runCoins == coins &&
                locked.save.runShards == shards && locked.save.runXp == xp &&
                (bool)tuning.GetValue(locked) && locked.revision == revision,
                "Protected civic-grid tuning blocks Abyss entry without mutating the live mission or expedition save");
            Check(locked.toast.Contains("调谐"),
                "Blocked Abyss entry identifies the live civic-grid tuning mission");

            var cancelable = Unlocked(data);
            cancelable.Enter(6);
            cancelable.racing = true;
            cancelable.StartAbyss(1);
            Check(cancelable.InAbyss && cancelable.save.room == 8 &&
                cancelable.save.abyssFloor == 1 && cancelable.save.abyssRisk == 1 &&
                !cancelable.racing,
                "A skiff race remains cancelable by travel and does not block a valid Abyss expedition");

            var cancelableMap = Unlocked(data);
            cancelableMap.Enter(6);
            cancelableMap.racing = true;
            cancelableMap.raceTime = 12f;
            cancelableMap.raceCheckpoint = 2;
            int mapCoins = cancelableMap.save.coins;
            cancelableMap.Enter(0);
            Check(cancelableMap.save.room == 0 && !cancelableMap.racing &&
                cancelableMap.raceTime == 0 && cancelableMap.raceCheckpoint == 0 &&
                cancelableMap.save.coins == mapCoins,
                "Ordinary map travel cancels an unfinished skiff race without awarding its completion reward");

            var cancelableFastReturn = Unlocked(data);
            cancelableFastReturn.Enter(6);
            cancelableFastReturn.enemies.Clear();
            cancelableFastReturn.projectiles.Clear();
            cancelableFastReturn.hazards.Clear();
            cancelableFastReturn.racing = true;
            cancelableFastReturn.raceTime = 12f;
            cancelableFastReturn.raceCheckpoint = 2;
            int returnCoins = cancelableFastReturn.save.coins;
            Check(cancelableFastReturn.SafeTravel,
                "The fast-return fixture has no nearby enemy, hostile projectile or live hazard");
            cancelableFastReturn.ReturnToTown();
            Check(cancelableFastReturn.save.room == 0 && !cancelableFastReturn.racing &&
                cancelableFastReturn.raceTime == 0 && cancelableFastReturn.raceCheckpoint == 0 &&
                cancelableFastReturn.save.coins == returnCoins,
                "Quick return cancels an unfinished skiff race without awarding its completion reward");
        }

        public static void AbyssSixthFloorOldSnapshotsRestoreWideBossWithoutClipping(Campaign data)
        {
            var g=ShoreRun(data,0,3,3);
            g.Close();g.Enter(0);
            g.StartAbyss(0);
            Check(g.InAbyss&&g.save.room==8,
                "An original old-compatible Abyss run opens the real expedition floor");
            g.enemies.Clear();
            g.save.abyssFloor=6;
            var generate=typeof(GameCore).GetMethod("GenerateAbyss",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            var restore=typeof(GameCore).GetMethod("RestoreAbyssEnemies",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            Check(generate!=null&&restore!=null,
                "Actual sixth-floor geometry and saved-enemy restoration paths remain available");
            generate.Invoke(g,null);
            Point arena=new Point();
            bool located=false;
            for(int y=3;y<g.Map.rows.Length-3&&!located;y++)
            for(int x=3;x<g.Map.rows[y].Length-4&&!located;x++)
            {
                Point candidate=new Point(x+.5f,y+.5f);
                if(g.CanStand(candidate)&&
                    g.CanStand(candidate+new Point(1.1f,0))&&
                    (candidate-g.Position).Length>4f)
                {arena=candidate;located=true;}
            }
            Check(located,"A real walkable Abyss chamber can host two overlapping saved combatants");
            var bossRecord=new EnemyRecord{id=87701,kind=3,boss=true,
                x=arena.x,y=arena.y,hp=420f,maxHp=700f};
            var raiderRecord=new EnemyRecord{id=87702,kind=0,boss=false,
                x=arena.x+1.1f,y=arena.y,hp=30f,maxHp=60f};
            g.save.abyssSnapshotFloor=6;
            foreach(bool bossFirst in new []{true,false})
            {
                g.enemies.Clear();
                g.save.abyssEnemies=bossFirst?
                    new[]{bossRecord,raiderRecord}:
                    new[]{raiderRecord,bossRecord};
                Check((bool)restore.Invoke(g,null)&&g.enemies.Count==2,
                    "Both original live opponents survive their restored sixth-floor roster");
                Enemy boss=g.enemies.Find(e=>e.id==87701);
                Enemy raider=g.enemies.Find(e=>e.id==87702);
                Check(boss!=null&&raider!=null&&boss.boss&&!raider.boss&&
                    Math.Abs(boss.hp-420f)<.01f&&
                    Math.Abs(raider.hp-30f)<.01f&&
                    (boss.Position-raider.Position).Length>=1.4f&&
                    g.CanStand(boss.Position)&&g.CanStand(raider.Position),
                    "Restored Boss and raider use genuine wide-body clearance in either serial roster order without losing health or identity");
                Check(g.save.version==2&&g.save.abyssSnapshotFloor==6,
                    "Restoring old records requires no new fields, loot changes, or migration");
            }
        }

        public static void AbyssExpeditions(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = ShoreRun(data, profession, 3, 3); g.Close(); g.Enter(0); g.EquipRelic(0, 0); g.EquipRelic(1, 4); g.AcceptBounty(5); g.StartAbyss(0);
                for (int floor = 1; floor <= 6; floor++)
                {
                    int coins = g.save.coins, shards = g.save.shards; Combat(g, false);
                    Check(g.save.coins == coins && g.save.shards == shards && g.save.runCoins > 0, "Abyss loot is pending until banked");
                    if (floor == 2) { g = new GameCore(data, CloneSave(g.save)); g.Start(); Check(g.enemies.Count == 0 && g.save.runCoins > 0, "A cleared floor and pending loot survive reconstruction"); }
                    // The original autopilot teleported to the root gate
                    // and immediately pressed bank on the SAME world frame
                    // as the last real enemy death. Lingering hostile
                    // missiles/rift warnings are valid gameplay and can no
                    // longer be escaped via an instant pause-menu payout.
                    // Simulate reaching the gate and waiting for the TRUE
                    // combat threat to clear, instead of faking a safe gate
                    // or clearing real projectile lists in the test.
                    WorldObject gate=Array.Find(g.Map.objects,
                        o=>o.kind=="abyssGate");
                    Check(gate!=null&&g.CanStand(new Point(gate.x,gate.y)),
                        "Every seeded floor has a genuine standable settlement gate");
                    g.save.x=gate.x;g.save.y=gate.y;
                    int wait=0;
                    while(!g.SafeTravel&&wait++<200)
                        g.Tick(.02f,new Point());
                    Check(g.SafeTravel&&g.InAbyss&&g.enemies.Count==0,
                        "The cleared abyss gate must become safe once its real lingering threats expire, floor "+floor);
                    if (floor < 6) Action(g, "abyssGate", "boon" + ((floor - 1) % 3)); else Action(g, "abyssGate", "abyssBank");
                    Check(g.save.coins > coins && g.save.shards > shards, "Every cleared floor can bank rewards");
                }
                Check(g.save.room == 0 && g.save.bestFloor == 6 && g.Has("abyss_complete") && g.Has("relic_5"), "All classes can complete the six-floor expedition");
                Check(g.save.bountyProgress == 2 && g.ClaimBounty(), "Banked floors complete the exploration contract");
                g.save.stage = 3; g.Enter(4); Action(g, "shrine", "parley");
                Check(g.Has("warden_stood_down") && g.Has("boss_4") && !g.enemies.Exists(e => e.boss), "The working covenant can peacefully resolve the original confrontation");
                int rewardCoins = g.save.coins; Action(g, "shrine", "ending1"); g.Choose(0); Check(g.save.coins == rewardCoins, "Peaceful resolution does not reward twice");
                int banked = g.save.shards; g.StartAbyss(2); g.enemies.Clear(); g.save.runShards = 9; g.save.runCoins = 50; g.save.runXp = 40; g.save.health = 1; g.invulnerable = 0;
                g.enemies.Add(new Enemy(900, 0, g.save.x, g.save.y, 999) { state = 1, timer = 0 }); g.Tick(.02f, new Point()); Advance(g, 1);
                Check(g.save.room == 0 && g.save.shards == banked && g.save.runShards == 0 && g.save.abyssFloor == 0, "Failure loses pending loot only and safely returns home");
            }
        }


        public static void RidgeWatchMissingBlockedPatrolRejoinsRealThreeGuardObjective(Campaign data)
        {
            MapData original=data.maps[1];
            var rows=(string[])original.rows.Clone();
            try
            {
                // A temporarily closed 7x7 clearing suppresses ONLY the
                // first patrol. It cannot silently turn 2/3 into a victory.
                for(int y=58;y<=64;y++)
                {
                    char[] cells=rows[y].ToCharArray();
                    for(int x=98;x<=104;x++)cells[x]='#';
                    rows[y]=new string(cells);
                }
                data.maps[1]=new MapData{id=1,spawnX=original.spawnX,
                    spawnY=original.spawnY,rows=rows,objects=original.objects,
                    decor=original.decor};
                var g=Unlocked(data);
                g.save.flags.Add("boss_1");
                g.save.flags.Add("ridge_seen_1_0");
                g.save.flags.Add("ridge_seen_1_1");
                g.Enter(1);g.invulnerable=180f;
                var field=typeof(GameCore).GetField("ridgeWatchIds",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                var tracked=(Dictionary<int,int>)field.GetValue(g);
                Check(tracked.Count==2&&!tracked.ContainsValue(0),
                    "A physically closed ridge clearing spawns no distant or buried fake patrol");
                var strike=typeof(GameCore).GetMethod("Hit",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                int coins=g.save.coins,shards=g.save.shards;
                foreach(int id in new List<int>(tracked.Keys))
                {
                    var body=g.enemies.Find(e=>e.id==id);
                    Check(body!=null&&g.CanStand(body.Position),"Existing patrol actors still stand on their genuine local paths");
                    strike.Invoke(g,new object[]{body,9999f,true,null,null});
                }
                Check(g.RidgeWatchProgress(1)==2&&!g.RidgeWatchComplete(1)&&
                    g.save.coins==coins+12&&g.save.shards==shards,
                    "Only two normal enemy loot payments occur; the unique three-patrol reward is NOT granted");
                g.TrackQuest("ridge_watch");
                var waiting=g.GetQuest("ridge_watch");
                Check(waiting.next.Contains("暂时封闭")&&
                    (waiting.target-new Point(101.5f,61.5f)).Length<.001f,
                    "Real quest compass marks blocked missing clearing, never a dead patrol or misleading completion");
                g.save.english=true;
                Check(g.GetQuest("ridge_watch").next.Contains("last clearing is blocked"),
                    "Actual English quest objective tells the player to return after the route reopens");
                g.save.english=false;
                rows=(string[])original.rows.Clone();
                data.maps[1].rows=rows;
                g.save.x=original.spawnX;g.save.y=original.spawnY;
                for(int tick=0;tick<90&&tracked.Count<3;tick++)
                    g.Tick(.05f,new Point());
                Check(tracked.Count==3&&tracked.ContainsValue(0),
                    "Reopened real clearing allows one missing original patrol to join without a map reload");
                var missing=g.enemies.Find(e=>tracked.ContainsKey(e.id)&&tracked[e.id]==0);
                Check(missing!=null&&g.CanStand(missing.Position)&&
                    (missing.Position-new Point(101.5f,61.5f)).Length<=4.25f,
                    "The recovered patrol stands on genuine local footing, not arbitrary fallback scenery");
                Check((g.GetQuest("ridge_watch").target-missing.Position).Length<.001f,
                    "World compass automatically switches back to the genuine live patrol after terrain reopens");
                strike.Invoke(g,new object[]{missing,9999f,true,null,null});
                Check(g.RidgeWatchProgress(1)==3&&g.RidgeWatchComplete(1)&&
                    g.save.shards==shards+1&&g.save.coins==coins+46,
                    "Only the third confirmed real enemy death pays the original one-time reward");
            }
            finally { data.maps[1]=original; }
        }


        public static void RidgeWatchWorldSnapshotRejectsImpostorWithReusedPatrolId(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var key=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var ids=(Dictionary<int,int>)key.GetValue(g);
            int patrolId=-1;
            foreach(var pair in ids)
                if(pair.Value==0){patrolId=pair.Key;break;}
            Check(patrolId>0,"Actual first highland patrol is present before saving");
            g.PrepareSave();
            SaveData captured=CloneSave(g.save);
            EnemyRecord forged=Array.Find(captured.worldEnemies,
                x=>x!=null&&x.id==patrolId);
            Check(forged!=null&&forged.maxHp==37f,
                "Old v2 world snapshots preserve each patrol's original numeric ID and maximum HP");
            // A corrupted or legacy snapshot reuses the real patrol ID for
            // an unrelated enemy of the very same archetype, on the very
            // same clearing, but with a different true combat profile.
            forged.maxHp=160f;forged.hp=160f;
            var restored=new GameCore(data,captured);
            restored.Start();
            var registered=(Dictionary<int,int>)key.GetValue(restored);
            Enemy impostor=restored.enemies.Find(x=>x.id==patrolId);
            Check(impostor!=null&&impostor.hp==160f&&
                !restored.IsActiveRidgeWatchPatrol(impostor)&&
                !registered.ContainsValue(0),
                "Original numeric ID and position alone cannot impersonate an authentic saved ridge patrol");
            var strike=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            int shards=restored.save.shards;
            strike.Invoke(restored,new object[]{impostor,9999f,true,null,null});
            Check(restored.RidgeWatchProgress(1)==0&&
                restored.save.shards==shards,
                "Killing a wrong-profile world enemy never grants protected patrol credit");
            int frames=0;
            while(!registered.ContainsValue(0)&&frames++<90)
                restored.Tick(.05f,new Point());
            Check(registered.ContainsValue(0),
                "Real patrol recovers on legal local footing after the unsafe saved actor is retired");
            Enemy live=null;
            foreach(var pair in registered)
                if(pair.Value==0){live=restored.enemies.Find(x=>x.id==pair.Key);break;}
            Check(live!=null&&live.maxHp==37f&&
                restored.IsActiveRidgeWatchPatrol(live),
                "Recovered actor is precisely the authored first patrol with real combat identity");
            strike.Invoke(restored,new object[]{live,9999f,true,null,null});
            Check(restored.RidgeWatchProgress(1)==1&&
                !restored.RidgeWatchComplete(1)&&restored.save.version==2,
                "Authentic replacement kill advances exactly one third of the original quest without save migration");
        }
        public static void RidgeWatchCannotCreditAnotherActorWithReusedNumericId(Campaign data)
        {
            var g=Unlocked(data);g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var field=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var tracked=(Dictionary<int,int>)field.GetValue(g);
            Check(tracked.Count==3,"Real ridge watch has all three actors");
            int id=0;foreach(int candidate in tracked.Keys){id=candidate;break;}
            Enemy actual=g.enemies.Find(e=>e.id==id);
            Check(actual!=null&&g.IsActiveRidgeWatchPatrol(actual),
                "Actual registered living patrol carries the legitimate visual marker");
            var impostor=new Enemy(id,0,g.Position.x+2f,g.Position.y,30f);
            g.enemies.Add(impostor);
            Check(!g.IsActiveRidgeWatchPatrol(impostor),
                "Unrelated combat actor with coincidentally reused ID cannot borrow a hunt marker");
            var strike=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            strike.Invoke(g,new object[]{impostor,9999f,true,null,null});
            Check(g.RidgeWatchProgress(1)==0&&!g.RidgeWatchComplete(1),
                "A genuine kill of the wrong ACTOR must not credit the protected patrol");
            strike.Invoke(g,new object[]{actual,9999f,true,null,null});
            Check(g.RidgeWatchProgress(1)==1,
                "Only the original registered combatant can earn the survey kill");
        }
        public static void RidgeWatchSurveysOnlyRealCombatAndPaysOnce(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var idsField=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var strike=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(idsField!=null&&strike!=null&&g.save.room==1,
                "Ridge watch is attached to the real playable combat/world path");
            var tracked=(Dictionary<int,int>)idsField.GetValue(g);
            Check(tracked.Count>0,"Mapped highlands must spawn actual eligible patrols");
            var ids=new List<int>(tracked.Keys);
            var indices=new List<int>();
            foreach(int id in ids)indices.Add(tracked[id]);
            // Merely removing patrols or killing ordinary unrelated foes
            // may not report a real surveyed combat victory.
            int coins=g.save.coins,shards=g.save.shards;
            Enemy other=new Enemy(90001,0,g.Position.x+1,g.Position.y,1);
            g.enemies.Add(other);
            strike.Invoke(g,new object[]{other,50f,false,null,null});
            Check(g.RidgeWatchProgress(1)==0&&!g.RidgeWatchComplete(1),
                "An unrelated world combat kill cannot advance high-trail survey");
            for(int k=0;k<ids.Count;k++)
            {
                // On v2 reload, all Enemy objects are reconstructed; the
                // logical watch index resolves to its new authentic body.
                int liveId=-1;
                foreach(var pair in tracked)
                    if(pair.Value==indices[k]){liveId=pair.Key;break;}
                Enemy target=g.enemies.Find(e=>e.id==liveId);
                Check(target!=null,"The current logical patrol index retains a genuine living actor after any save reload");
                strike.Invoke(g,new object[]{target,999f,true,null,null});
                Check(g.RidgeWatchProgress(1)==k+1,
                    "Each actually defeated mapped patrol advances exactly one stop");
                if(k<ids.Count-1)
                    Check(g.toast.Contains((k+1)+"/3"),
                        "A real partial enemy defeat announces the new objective progress");
                if(k==0&&ids.Count>1)
                {
                    g.PrepareSave();
                    g=new GameCore(data,CloneSave(g.save));
                    g.Start();
                    tracked=(Dictionary<int,int>)idsField.GetValue(g);
                    Check(tracked.Count==ids.Count-1&&
                        !tracked.ContainsValue(indices[0])&&
                        g.RidgeWatchProgress(1)==1,
                        "Reload keeps credited progress and only surviving actual patrol actors, never dead phantom registrations");
                }
            }
            Check(g.RidgeWatchComplete(1)&&g.save.shards==shards+1&&
                g.save.coins>=coins+28,
                "Only clearing all actual ridge guards yields a one-time first-clear reward");
            int earnedShards=g.save.shards;
            g.Enter(1);
            tracked=(Dictionary<int,int>)idsField.GetValue(g);
            foreach(int id in new List<int>(tracked.Keys))
            {
                Enemy target=g.enemies.Find(e=>e.id==id);
                Check(target!=null,"Repeatable existing wilderness combat is preserved");
                strike.Invoke(g,new object[]{target,999f,true,null,null});
            }
            Check(g.RidgeWatchComplete(1)&&g.save.shards==earnedShards&&
                !g.RidgeWatchComplete(2),"Repeated kills never duplicate the survey bonus");
        }

        public static void RidgeWatchJournalTracksLiveEnemiesAndSaveProgress(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var q=g.GetQuest("ridge_watch");
            Check(!q.locked&&!q.complete&&q.room==1&&q.steps.Count==7&&
                Array.IndexOf(GameCore.QuestIds,"ridge_watch")>=0,
                "New optional hunt is a selectable real journal objective");
            var idsField=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var ids=(Dictionary<int,int>)idsField.GetValue(g);
            Check(ids.Count>1,"Highland quest has multiple physical patrols to follow");
            Check(g.enemies.Exists(e=>ids.ContainsKey(e.id)&&
                (e.Position-q.target).Length<.01f),
                "Tracked journal routes toward an actually living hostile actor");
            g.TrackQuest("ridge_watch");
            Check(g.save.trackedQuest=="ridge_watch"&&
                g.NavigationTarget().HasValue,
                "Real world guidance accepts the new journal quest");
            Enemy first=g.enemies.Find(e=>ids.ContainsKey(e.id)&&
                (e.Position-q.target).Length<.01f);
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            hit.Invoke(g,new object[]{first,999f,true,null,null});
            var next=g.GetQuest("ridge_watch");
            Check(next.steps[0].text.Contains("1/3")&&
                g.enemies.Exists(e=>ids.ContainsKey(e.id)&&
                    (e.Position-next.target).Length<.01f)&&
                (first.Position-next.target).Length>.01f,
                "After a combat defeat, the live waypoint changes to an undefeated patrol");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));
            restored.Start();
            Check(restored.save.trackedQuest=="ridge_watch"&&
                restored.GetQuest("ridge_watch").steps[0].text.Contains("1/3"),
                "Existing v2 save files preserve tracked watch and per-actor progress");
        }

        public static void RidgeWatchPermanentlyRestoresHighTrailEndurance(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);g.enemies.Clear();
            g.save.x=89.5f;g.save.y=67.5f;
            g.save.energy=30f;
            g.Tick(.05f,new Point());
            float before=g.save.energy-30f;
            Check(before>0f,"Baseline stamina regeneration must remain alive");
            g.save.flags.Add("ridge_watch_complete_1");
            g.save.energy=30f;g.Tick(.05f,new Point());
            float boosted=g.save.energy-30f;
            Check(Math.Abs(boosted-before-.10f)<.0003f,
                "Only a cleared ridge grants +2 energy/sec along the actual distant walk");
            g.save.x=65.5f;g.save.y=67.5f;g.save.energy=30f;
            g.Tick(.05f,new Point());
            Check(Math.Abs(g.save.energy-30f-before)<.0003f,
                "A cleared ridge must not inflate combat energy in the inner region");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            loaded.Start();loaded.enemies.Clear();
            loaded.save.x=89.5f;loaded.save.y=67.5f;
            loaded.save.energy=30f;loaded.Tick(.05f,new Point());
            Check(loaded.RidgeWatchComplete(1)&&
                Math.Abs(loaded.save.energy-30f-boosted)<.0003f,
                "Original save format retains the actual completed high-trail benefit");
        }

        public static void RidgeWatchActivatesImmediatelyOnBothRealUnlockOrders(Campaign data)
        {
            var key=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var strike=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(key!=null&&strike!=null,"Actual watch spawns and contact damage exist");
            // Case A: guardian is already dead, and the second physical
            // ridge marker is the LAST unlock. The hero stays in this map.
            var linked=Fresh(data);
            linked.save.flags.Add("boss_1");
            linked.Enter(1);
            Check(((Dictionary<int,int>)key.GetValue(linked)).Count==0,
                "A still-unlinked route starts with no late-game watch");
            var markers=new List<WorldObject>();
            foreach(WorldObject o in linked.Map.objects)
                if(o.kind=="ridgemarker")markers.Add(o);
            Check(markers.Count==2,"Two real map markers must gate the watch");
            foreach(WorldObject marker in markers)
            {
                linked.save.x=marker.x;linked.save.y=marker.y;
                linked.Talk(marker);linked.Close();
            }
            Check(linked.RidgeLinked(1)&&
                ((Dictionary<int,int>)key.GetValue(linked)).Count==3,
                "Second real marker immediately activates all three live hostiles");
            int firstCount=linked.enemies.Count;
            linked.save.x=markers[0].x;linked.save.y=markers[0].y;
            linked.Talk(markers[0]);linked.Close();
            Check(linked.enemies.Count==firstCount,
                "Repeated marker interaction cannot duplicate an active patrol");
            // Case B: both route markers are already known, and a real
            // guardian death is the LAST unlock, without region transition.
            var guardian=Fresh(data);
            guardian.save.flags.Add("ridge_seen_1_0");
            guardian.save.flags.Add("ridge_seen_1_1");
            guardian.Enter(1);
            Check(((Dictionary<int,int>)key.GetValue(guardian)).Count==0,
                "Surveying a still-guarded route does not prematurely spawn the watch");
            Enemy boss=guardian.enemies.Find(e=>e.boss);
            if(boss==null)
            {
                boss=new Enemy(7777,0,guardian.Position.x+2f,
                    guardian.Position.y,30f,true);
                guardian.enemies.Add(boss);
            }
            strike.Invoke(guardian,new object[]{boss,9999f,true,null,null});
            Check(guardian.Has("boss_1")&&
                ((Dictionary<int,int>)key.GetValue(guardian)).Count==3,
                "Authoritative Boss hit/defeat transaction unlocks real patrols immediately");
            int afterBoss=guardian.enemies.Count;
            guardian.save.x=markers[0].x;guardian.save.y=markers[0].y;
            guardian.Talk(markers[0]);guardian.Close();
            Check(guardian.enemies.Count==afterBoss,
                "Later ridge dialogues do not duplicate a live post-Boss encounter");
        }

        public static void RidgeWatchRealContactCallsOnlyReachableLivingPatrols(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var idField=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var ids=(Dictionary<int,int>)idField.GetValue(g);
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(ids.Count==3&&!g.RidgeWatchAlarmActive,
                "Three actual highland combat actors are dormant before a hit");
            Enemy victim=null;
            foreach(Enemy e in g.enemies)
                if(ids.ContainsKey(e.id)&&ids[e.id]==0){victim=e;break;}
            Check(victim!=null&&g.IsActiveRidgeWatchPatrol(victim),
                "Actual patrol identity is verified before combat");
            int originalFlags=g.save.flags.Count;
            int originalCoins=g.save.coins;
            int originalHP=(int)victim.hp;
            // Run the original damage / stagger / actual HP transaction.
            // This is not an invented alarm button or a synthetic kill flag.
            hit.Invoke(g,new object[]{victim,1f,false,null,null});
            Check(victim.hp<originalHP&&victim.hp>0f&&
                g.RidgeWatchCallPending&&!g.RidgeWatchAlarmActive&&
                g.RidgeWatchRespondingCount==0&&
                g.save.flags.Count==originalFlags&&
                g.save.coins==originalCoins,
                "Real damage begins an interruptible warning, not instant global enemy pursuit");
            Enemy unrelated=new Enemy(911700,0,victim.x+1f,victim.y,70);
            g.enemies.Add(unrelated);
            hit.Invoke(g,new object[]{unrelated,2f,false,null,null});
            Check(g.RidgeWatchCallPending&&!g.RidgeWatchAlarmActive,
                "Damaging unrelated enemies cannot release an unfinished ridge whistle");
            g.save.x=12.5f;g.save.y=14.5f;
            for(int i=0;i<19;i++)g.Tick(.05f,new Point());
            Check(g.RidgeWatchAlarmActive&&g.RidgeWatchRespondingCount>=1,
                "The living source finishes its actual .85-second signal before squad AI responds");
            for(int i=0;i<125;i++)g.Tick(.05f,new Point());
            Check(!g.RidgeWatchAlarmActive&&g.RidgeWatchRespondingCount==0&&
                g.RidgeWatchProgress(1)==0,
                "A live alarm expires without permanent combat pursuit or fake quest progress");
            hit.Invoke(g,new object[]{victim,1f,false,null,null});
            Check(g.RidgeWatchCallPending&&!g.RidgeWatchAlarmActive,
                "A later genuine injury starts a NEW interruptible warning");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(!restored.RidgeWatchAlarmActive&&
                restored.RidgeWatchRespondingCount==0&&
                restored.save.version==2,
                "Old V2 save reload retains genuine enemies and progress, never a transient alarm");
        }

        public static void RidgeWatchSearchesReportedContactWithoutSeeingThroughCover(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var ids=(Dictionary<int,int>)typeof(GameCore).GetField(
                "ridgeWatchIds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g);
            var damage=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var pursuit=typeof(GameCore).GetMethod("RidgeWatchPursuitFoot",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Enemy source=null;
            foreach(Enemy candidate in g.enemies)
                if(ids.ContainsKey(candidate.id)&&ids[candidate.id]==0)
                {source=candidate;break;}
            Check(source!=null&&pursuit!=null,
                "Reported-foot search is hooked to the actual mapped ridge actors");
            // A real damage transaction reports this original standing
            // position. Relocating the hero afterwards cannot move the
            // previously reported strike location.
            Point seen=g.Position;
            damage.Invoke(g,new object[]{source,1f,false,null,null});
            Check(g.RidgeWatchCallPending,
                "An authentic hit starts a whistle without immediate response");
            g.save.x=12.5f;g.save.y=14.5f;
            for(int i=0;i<19;i++)g.Tick(.05f,new Point());
            Check(g.RidgeWatchAlarmActive&&g.RidgeWatchRespondingCount>=1,
                "An alive caller finishes the signal through the normal world clock");
            Point remote=new Point(12.5f,14.5f);
            Point investigate=(Point)pursuit.Invoke(g,new object[]{source,remote});
            Check((investigate-seen).Length<.001f&&
                (investigate-remote).Length>5f,
                "The squad investigates the genuine old injury position rather than the live distant hero");
            Point close=source.Position+new Point(.6f,0f);
            if(g.CanStand(close))
                Check(((Point)pursuit.Invoke(g,new object[]{source,close})-close).Length<.001f,
                    "Reacquiring a nearby visible hero restores ordinary live combat");
            // A fresh REAL hit while the alarm is active records the new
            // player's foot; merely moving does not change the report.
            Point updated=g.Position;
            damage.Invoke(g,new object[]{source,1f,false,null,null});
            Check(((Point)pursuit.Invoke(g,new object[]{source,seen})-updated).Length<.001f,
                "Only a second actual damage event updates the shared enemy sighting");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(!loaded.RidgeWatchAlarmActive&&loaded.save.version==2,
                "Reload clears only transient squad investigation data, preserving old saves");
        }
        public static void RidgeWatchWhistleCanBeSilencedBeforeBackupArrives(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var field=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var ids=(Dictionary<int,int>)field.GetValue(g);
            Enemy caller=null;
            foreach(Enemy e in g.enemies)
                if(ids.ContainsKey(e.id)){caller=e;break;}
            Check(caller!=null&&g.IsActiveRidgeWatchPatrol(caller),
                "A living mapped patrol is required for an audible call");
            hit.Invoke(g,new object[]{caller,1f,false,null,null});
            Check(g.RidgeWatchCallPending&&g.IsRidgeWatchCaller(caller)&&
                g.RidgeWatchCallSecondsLeft>.75f,
                "A genuine injury gives only its living source a visible counterplay clock");
            var falseCaller=new Enemy(caller.id,caller.kind,
                caller.x,caller.y,caller.maxHp);
            Check(!g.IsRidgeWatchCaller(falseCaller),
                "Another actor cannot borrow the warning via the same numeric ID");
            // A second actual contact can defeat the caller before 850ms.
            hit.Invoke(g,new object[]{caller,999f,true,null,null});
            Check(!g.RidgeWatchCallPending&&
                !g.IsRidgeWatchCaller(caller)&&
                g.RidgeWatchCallSecondsLeft==0f&&
                !g.RidgeWatchAlarmActive&&
                g.RidgeWatchProgress(1)==1,
                "Killing the caller stops the signal but credits only that one genuine patrol");
            g.save.x=12.5f;g.save.y=14.5f;
            for(int tick=0;tick<30;tick++)g.Tick(.05f,new Point());
            Check(!g.RidgeWatchAlarmActive&&
                g.RidgeWatchRespondingCount==0&&
                !g.RidgeWatchComplete(1),
                "A dead caller cannot summon backup during later world ticks");
            // A different surviving patrol can still signal on its own
            // genuine hit; the old victim's transient call is not restored.
            var remaining=(Dictionary<int,int>)field.GetValue(g);
            Enemy live=null;
            foreach(Enemy e in g.enemies)
                if(remaining.ContainsKey(e.id)&&g.IsActiveRidgeWatchPatrol(e))
                {live=e;break;}
            Check(live!=null,"Other original bodies survive a canceled warning");
            hit.Invoke(g,new object[]{live,1f,false,null,null});
            Check(g.RidgeWatchCallPending&&!g.RidgeWatchAlarmActive,
                "A living replacement caller must begin a new full windup");
            live.stun=1.4f;
            g.Tick(.05f,new Point());
            Check(!g.RidgeWatchCallPending&&!g.RidgeWatchAlarmActive,
                "Genuine crowd-control stun during the warning cancels recruitment");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(!loaded.RidgeWatchCallPending&&!loaded.RidgeWatchAlarmActive&&
                loaded.save.version==2,
                "A V2 reload never revives incomplete whistles or transient squad targets");
        }

        public static void RidgeWatchPatrolsWalkUsingRealEnemyMovementAndYieldToCombat(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var field=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.Instance|BindingFlags.NonPublic);
            var tracked=(Dictionary<int,int>)field.GetValue(g);
            Check(tracked.Count==3,"Three real ridge enemies occupy authored foot corridors");
            Enemy scout=null;
            foreach(Enemy e in g.enemies)
                if(tracked.ContainsKey(e.id)&&tracked[e.id]==0){scout=e;break;}
            Check(scout!=null,"Actual first patrol participates in the live enemy list");
            var anchor=new Point(101.5f,61.5f);
            g.save.x=12.5f;g.save.y=14.5f;
            Point before=scout.Position;
            for(int step=0;step<100;step++)
            {
                g.Tick(.05f,new Point());
                Check(g.CanStand(scout.Position)&&
                    (scout.Position-anchor).Length<=2.5f,
                    "Patrol stride remains on walkable ground near its authored high trail");
            }
            Check((scout.Position-before).Length>.35f&&
                scout.moveTime>.35f&&
                (scout.facing).Length>.9f,
                "Actual existing enemy walked on patrol and advanced the body animation gait");
            float oldGait=scout.moveTime;
            Point frozen=scout.Position;
            scout.stun=2f;
            for(int t=0;t<12;t++)g.Tick(.05f,new Point());
            Check((scout.Position-frozen).Length<.00001f&&
                Math.Abs(scout.moveTime-oldGait)<.00001f,
                "A stunned hostile never patrols or manufactures walking frames");
            scout.stun=0f;
            g.save.x=scout.x+2.4f;g.save.y=scout.y;
            var last=scout.Position;
            g.Tick(.05f,new Point());
            Check((scout.Position-last).Length<.2f,
                "Within combat sight, ordinary enemy AI governs the actor instead of double-moving it");
            int count=g.enemies.Count;
            var room=g.save.room;
            g.Enter(0);
            Check(g.save.room==0&&g.enemies.Count<=count,
                "Departing the region retires transient patrol gait without spawning actors in town");
            g.Enter(room);
            Check(((Dictionary<int,int>)field.GetValue(g)).Count==3,
                "Returning to a surveyed highland retains one genuine set of patrols");
        }

        public static void RidgeWatchAllyHitsReportAllyFootNotRemoteHero(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var ids=(Dictionary<int,int>)typeof(GameCore).GetField(
                "ridgeWatchIds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g);
            var damage=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var pursuit=typeof(GameCore).GetMethod("RidgeWatchPursuitFoot",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Enemy struck=null;
            foreach(Enemy body in g.enemies)
                if(ids.ContainsKey(body.id)&&ids[body.id]==0)
                {struck=body;break;}
            Check(struck!=null&&pursuit!=null,
                "The unchanged Hit entrypoint still owns mapped patrol contacts");
            Point allyFoot=g.Position;
            Check(g.CanStand(allyFoot)&&
                (allyFoot-struck.Position).Length>5f,
                "The real party entry foot and patrol clearing are separate");
            g.save.companion=1;
            g.companionPosition=allyFoot;
            // Simulate the existing immutable companion-owned attack
            // profile. No new Hit parameters or save fields are introduced.
            g.save.x=struck.x;g.save.y=struck.y;
            var allyImpact=new ImpactProfile(1,0,1,0,0f,false);
            damage.Invoke(g,new object[]{struck,2f,false,null,allyImpact});
            Check(g.RidgeWatchCallPending&&
                (g.Position-g.companionPosition).Length>5f,
                "A genuine ally-owned HP hit begins the same interruptible whistle");
            Point far=new Point(12.5f,14.5f);
            g.save.x=far.x;g.save.y=far.y;
            for(int tick=0;tick<19;tick++)g.Tick(.05f,new Point());
            Check(g.RidgeWatchAlarmActive&&
                ((Point)pursuit.Invoke(g,new object[]{struck,far})-allyFoot).Length<.001f,
                "Alert responders search the companion's actual source foot, never the unrelated hero foot");
            // A later hero-owned hit must update the report legitimately;
            // old companion targeting cannot leak through that transaction.
            g.save.x=struck.x;g.save.y=struck.y;
            Point heroFoot=g.Position;
            damage.Invoke(g,new object[]{struck,1f,false,null,null});
            Check(((Point)pursuit.Invoke(g,new object[]{struck,far})-heroFoot).Length<.001f,
                "A subsequent real player-owned hit updates the reported foot to the hero");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));loaded.Start();
            Check(loaded.save.version==2&&!loaded.RidgeWatchAlarmActive,
                "Mixed companion/hero attribution is transient and old v2 saves still load");
        }

        public static void RidgeWatchJournalTargetsInterruptibleCallerAndLiveResponders(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            var ids=(Dictionary<int,int>)typeof(GameCore).GetField(
                "ridgeWatchIds",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(g);
            var strike=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Enemy caller=null,nearby=null;
            foreach(Enemy e in g.enemies)
            {
                int index;
                if(!ids.TryGetValue(e.id,out index))continue;
                if(index==0)caller=e;
                if(index==2)nearby=e;
            }
            Check(caller!=null&&nearby!=null,
                "An authentic patrol caller and a distinct nearer guard must exist");
            // The nearest regular target differs from the faraway
            // whistle source. This demonstrates genuine target reprioritization.
            g.save.x=nearby.x+.7f;g.save.y=nearby.y;
            Check((g.GetQuest("ridge_watch").target-nearby.Position).Length<.01f,
                "Before combat the original tracked guidance selects the closest live patrol");
            strike.Invoke(g,new object[]{caller,1f,false,null,null});
            var live=g.GetQuest("ridge_watch");
            Check(g.RidgeWatchCallPending&&
                (live.target-caller.Position).Length<.01f&&
                (live.next.Contains("calling backup")||live.next.Contains("呼援")),
                "A real pending whistle becomes the physical navigation and counterplay priority");
            Enemy impostor=new Enemy(caller.id,caller.kind,
                caller.x,caller.y,caller.maxHp);
            Check(!g.IsRidgeWatchResponder(impostor),
                "Actor identity cannot be forged using another patrol's numeric ID");
            g.save.x=12.5f;g.save.y=14.5f;
            for(int i=0;i<19;i++)g.Tick(.05f,new Point());
            Check(g.RidgeWatchAlarmActive&&
                g.IsRidgeWatchResponder(caller)&&
                g.RidgeWatchRespondingCount>=1&&
                !g.IsRidgeWatchResponder(impostor)&&
                (g.GetQuest("ridge_watch").next.Contains("converging")||
                 g.GetQuest("ridge_watch").next.Contains("集结")),
                "Actual survivors, the tracker and enemy Sprite cues agree only after successful recruitment");
            for(int i=0;i<125;i++)g.Tick(.05f,new Point());
            Check(!g.RidgeWatchAlarmActive&&
                !g.IsRidgeWatchResponder(caller)&&
                g.RidgeWatchProgress(1)==0,
                "Expired patrol response removes amber ownership without fabricating kills");
        }

        public static void RidgeWatchMarkerTracksLivePatrolWithoutBorrowingAnotherDialogue(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.Enter(1);
            WorldObject marker=Array.Find(g.Map.objects,o=>o.kind=="ridgemarker");
            Check(marker!=null,"Real highland marker exists for track choice");
            g.save.x=marker.x;g.save.y=marker.y;
            g.save.trackedQuest="main";
            g.Talk(marker);
            int choice=Array.FindIndex(g.conversation.choices,
                c=>c.action=="ridge_watch_track");
            Check(choice>=0&&g.conversation.textEn.Contains("\n\nRidge watch:")&&
                !g.conversation.textEn.Contains("\\n\\n"),
                "An available ridge marker offers live tracking with real paragraph breaks");
            g.save.x=marker.x+8f;
            g.Choose(choice);
            Check(g.save.trackedQuest=="main"&&g.mode=="world",
                "A stale marker dialogue cannot secretly reassign the active journal");
            g.save.x=marker.x;g.save.y=marker.y;
            g.Talk(marker);
            choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_watch_track");
            g.Choose(choice);
            Check(g.save.trackedQuest=="ridge_watch"&&
                g.GetQuest("ridge_watch").room==1,
                "A genuine physical marker explicitly tracks the field encounter");
            var ids=(Dictionary<int,int>)typeof(GameCore)
                .GetField("ridgeWatchIds",BindingFlags.NonPublic|BindingFlags.Instance)
                .GetValue(g);
            var target=g.NavigationTarget();
            Check(target.HasValue&&g.enemies.Exists(e=>ids.ContainsKey(e.id)&&
                (e.Position-target.Value).Length<.01f),
                "Tracking from the world marker connects to the live enemy navigation system");
            // The handler must never grant the new action from a fabricated
            // choice that is not attached to an actual ridge speaker.
            g.save.trackedQuest="main";
            var handler=typeof(GameCore).GetMethod("ChooseRidge",
                BindingFlags.NonPublic|BindingFlags.Instance);
            handler.Invoke(g,new object[]{"ridge_watch_track"});
            Check(g.save.trackedQuest=="main",
                "Direct unowned commands cannot counterfeit a pinned ridge interaction");
            g.save.flags.Add("ridge_watch_complete_1");
            g.Talk(marker);
            Check(Array.FindIndex(g.conversation.choices,
                c=>c.action=="ridge_watch_track")<0,
                "Completed watch must not advertise another repeatable task");
        }

        public static void RidgeWatchFieldAidPaysForCappedHealthAndBlocksLiveThreats(Campaign data)
        {
            var g=Unlocked(data);
            g.save.flags.Add("boss_1");
            g.save.flags.Add("ridge_seen_1_0");
            g.save.flags.Add("ridge_seen_1_1");
            g.save.flags.Add("ridge_watch_complete_1");
            g.Enter(1);g.enemies.Clear();
            WorldObject marker=Array.Find(g.Map.objects,o=>o.kind=="ridgemarker");
            Check(marker!=null,"Actual highland field marker exists");
            g.save.x=marker.x;g.save.y=marker.y;
            g.save.health=35;g.save.energy=40;g.save.inventory[2]=3;
            g.Talk(marker);
            int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_field_aid");
            Check(choice>=0,"Liberated route provides a real paid field-aid choice");
            g.Choose(choice);
            Check(g.save.health==65&&g.save.energy==70&&g.save.inventory[2]==2,
                "One real Moonherb restores 30 HP and 30 energy");
            g.Talk(marker);
            choice=Array.FindIndex(g.conversation.choices,c=>c.action=="ridge_field_aid");
            g.save.x=marker.x+8f;g.Choose(choice);
            Check(g.save.health==65&&g.save.inventory[2]==2,
                "Stale remote interaction never consumes supplies or heals");
            g.save.x=marker.x;g.save.y=marker.y;
            g.save.health=g.save.maxHealth;g.save.energy=100f;
            g.Talk(marker);g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="ridge_field_aid"));
            Check(g.save.inventory[2]==2,"Full supplies preserve valuable herbs");
            g.save.health=50;g.save.energy=50;
            Enemy nearby=new Enemy(79999,0,marker.x+.7f,marker.y,50);
            g.enemies.Add(nearby);
            Check(!g.SafeTravel,"A hostile in melee range must make the aid station unsafe");
            g.Talk(marker);g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="ridge_field_aid"));
            Check(g.save.inventory[2]==2&&g.save.health==50,
                "Active threats cannot exploit a field aid station");
            g.enemies.Remove(nearby);g.save.inventory[2]=0;
            g.Talk(marker);g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="ridge_field_aid"));
            Check(g.save.health==50,"No herb means no magical free recovery");
            g.save.inventory[2]=1;
            g.Talk(marker);g.Choose(Array.FindIndex(g.conversation.choices,
                c=>c.action=="ridge_field_aid"));
            Check(g.save.health==80&&g.save.energy==80&&g.save.inventory[2]==0,
                "Cleared station correctly resumes paid herbal service");
            g.PrepareSave();
            var restored=new GameCore(data,CloneSave(g.save));restored.Start();
            Check(restored.RidgeWatchComplete(1)&&restored.save.health==80&&
                restored.save.inventory[2]==0,
                "Original save format retains recovered HP and material payment");
        }

        public static void PeacefulCitadelResolutionStillUnlocksRealHighlandWatch(Campaign data)
        {
            var g=Unlocked(data);
            g.save.frontierEnding=3;
            g.save.flags.Add("ridge_seen_4_0");
            g.save.flags.Add("ridge_seen_4_1");
            g.Enter(4);
            Check(g.Proofs==3&&g.RidgeLinked(4)&&!g.Has("boss_4"),
                "All existing civic prerequisites and field bearings are actually satisfied");
            var key=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            Check(((Dictionary<int,int>)key.GetValue(g)).Count==0,
                "A live fortress guardian still prevents high-trail patrol activation");
            Action(g,"shrine","parley");
            var ids=(Dictionary<int,int>)key.GetValue(g);
            Check(g.Has("boss_4")&&g.Has("warden_stood_down")&&
                ids.Count==3&&!g.enemies.Exists(e=>e.boss),
                "Actual nonlethal negotiation preserves peace but opens the distant patrol");
            Check(!g.RidgeWatchComplete(4)&&g.RidgeWatchProgress(4)==0,
                "Negotiating the guardian is not itself a paid watch victory");
            var guide=g.GetQuest("ridge_watch");
            Check(guide.room==4&&g.enemies.Exists(e=>ids.ContainsKey(e.id)&&
                (e.Position-guide.target).Length<.01f),
                "Civilian quest navigation immediately reaches one of three real live enemies");
            g.PrepareSave();
            var resume=new GameCore(data,CloneSave(g.save));resume.Start();
            Check(resume.Has("warden_stood_down")&&!resume.enemies.Exists(e=>e.boss)&&
                ((Dictionary<int,int>)key.GetValue(resume)).Count==3,
                "Unchanged save schema retains both the peaceful ending and the field watch");
        }

        public static void RidgeWatchPublishesSevenDistinctPhysicalFieldAccounts(Campaign data)
        {
            var hit=typeof(GameCore).GetMethod("Hit",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var field=typeof(GameCore).GetField("ridgeWatchIds",
                BindingFlags.NonPublic|BindingFlags.Instance);
            var stories=new HashSet<string>();
            for(int room=1;room<=7;room++)
            {
                var g=Unlocked(data);
                g.save.flags.Add("boss_"+room);
                g.save.flags.Add("ridge_seen_"+room+"_0");
                g.save.flags.Add("ridge_seen_"+room+"_1");
                g.Enter(room);
                var ids=(Dictionary<int,int>)field.GetValue(g);
                Check(ids.Count==3,"Real regional patrols exist before history can be recorded");
                int count=g.save.moments.Count;
                foreach(int id in new List<int>(ids.Keys))
                {
                    Enemy victim=g.enemies.Find(e=>e.id==id);
                    Check(victim!=null,"Physical scout must be alive before victory credit");
                    hit.Invoke(g,new object[]{victim,999f,true,null,null});
                }
                Check(g.RidgeWatchComplete(room)&&g.save.moments.Count==count+1,
                    "Actual three-enemy victory appends exactly one chronicle account");
                string note=g.save.moments[g.save.moments.Count-1].en;
                Check(note.Length>80&&stories.Add(note),
                    "Each liberated region has a distinct, nontrivial authored story");
                WorldObject marker=Array.Find(g.Map.objects,o=>o.kind=="ridgemarker");
                g.save.x=marker.x;g.save.y=marker.y;
                g.Talk(marker);
                Check(g.conversation!=null&&g.conversation.textEn.Contains(note),
                    "The exact original marker can reread the resulting civilian account");
                g.Close();
                g.PrepareSave();
                var restored=new GameCore(data,CloneSave(g.save));restored.Start();
                Check(restored.save.moments.Exists(m=>m.en==note)&&
                    restored.RidgeWatchComplete(room),
                    "Original save schema retains completed route and public memory");
            }
            Check(stories.Count==7,"Every historical region has its own aftermath");
        }


        private static MapData CombatWallTestMap()
        {
            // Reuse the existing production combat fixture's physical wall.
            // Only test-local map data is replaced; originals are restored.
            string[] rows=new string[25];
            for(int y=0;y<25;y++)
            {
                char[] tiles=new string('#',25).ToCharArray();
                if(y>=2&&y<=22)
                    for(int x=2;x<=22;x++)tiles[x]='.';
                if(y==10)tiles[9]='#';
                rows[y]=new string(tiles);
            }
            return new MapData{
                id=0,spawnX=10.5f,spawnY=10.5f,
                rows=rows,objects=new WorldObject[0],
                decor=new Decoration[0]};
        }

        public static void SwordRiposteOnlySpendsOnRealUnblockedRelease(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                data.maps[0]=CombatWallTestMap();
            for(int profession=0;profession<3;profession++)
            {
                // Existing source-art / combat tests already validate this
                // exact 10.5,10.5 foot: legal actor root, real stone WEST,
                // open sword reach EAST. Reuse that map evidence instead of
                // assuming the town contains an arbitrary eastern wall.
                var blocked=Fresh(data,profession);
                blocked.Equip(0); // All three professions, the actual sword.
                blocked.enemies.Clear();blocked.projectiles.Clear();
                blocked.save.x=10.5f;blocked.save.y=10.5f;
                blocked.save.english=true;
                blocked.save.energy=100f;blocked.riposteTime=2f;
                Check(blocked.CanStand(blocked.Position)&&
                    blocked.Blocked(9.5f,10.5f)&&
                    blocked.Blocked(9.8f,10.5f),
                    "Verified source-map wall sits in the physical sword sweep");
                blocked.Attack(new Point(-1f,0f));
                Check(blocked.playerAction!=null&&
                    blocked.playerAction.riposteArmed,
                    "Each canonical hero class carries the real counter to the sword contact");
                for(int i=0;i<20&&blocked.playerAction!=null&&
                    !blocked.playerAction.released;i++)
                    blocked.Tick(.05f,new Point());
                Check(blocked.playerAction!=null&&
                    blocked.playerAction.released&&
                    blocked.playerAction.blockedAtContact&&
                    blocked.riposteTime>1f,
                    "Wall impact does not spend an earned counter");
                Check(!blocked.effects.Exists(e=>e.kind=="number"&&
                    e.text=="RIPOSTE RELEASE"),
                    "No counterfeit counter VFX appears on a wall impact");

                var open=Fresh(data,profession);
                open.Equip(0); // Match the same genuine sword profile.
                open.enemies.Clear();open.projectiles.Clear();
                open.save.x=10.5f;open.save.y=10.5f;
                open.save.english=true;
                open.save.energy=100f;open.riposteTime=2f;
                Check(open.CanStand(open.Position)&&
                    !open.Blocked(11.2f,10.5f),
                    "The opposite direction is an actual open contact");
                open.Attack(new Point(1f,0f));
                Check(open.playerAction!=null,
                    "Unblocked test enters the actual hero action pipeline");
                float originalDamage=open.playerAction.damage;
                for(int i=0;i<20&&open.playerAction!=null&&
                    !open.playerAction.released;i++)
                    open.Tick(.05f,new Point());
                Check(open.playerAction!=null&&open.playerAction.released&&
                    open.riposteTime<=0f&&
                    Math.Abs(open.playerAction.damage-originalDamage*1.5f)<.0001f,
                    "Unobstructed sword release spends one exact 50 percent bonus");
                Check(open.effects.Exists(e=>e.kind=="number"&&
                    e.text=="RIPOSTE RELEASE"),
                    "The real open sword stroke announces its earned counter");
            }
            }
            finally { data.maps[0]=original; }
        }

        public static void AllClassAttacksRejectSealedFootingBeforeResourcePayment(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                data.maps[0]=CombatWallTestMap();
            for(int profession=0;profession<3;profession++)
            for(int weapon=0;weapon<3;weapon++)
            {
                var g=Fresh(data,profession);
                g.enemies.Clear();g.projectiles.Clear();
                g.save.flags.Add("weapon_1");g.save.flags.Add("weapon_2");
                g.Equip(weapon);
                g.save.energy=100f;g.save.english=true;
                // Reuse the existing production source-art fixture:
                // west of the starter clearing is an ACTUAL wall tile.
                g.save.x=9.5f;g.save.y=10.5f;
                Check(!g.CanStand(g.Position)&&g.Blocked(9.5f,10.5f),
                    "Real wall denies the root for every playable class and loadout");
                g.Attack(new Point(1,0));
                Check(g.playerAction==null&&g.save.energy==100f&&
                    g.attackCooldown<=0f&&g.projectiles.Count==0,
                    "Sealed-foot basic attack cannot charge energy, damage or cooldown");
                g.Skill(new Point(1,0));
                Check(g.playerAction==null&&g.save.energy==100f&&
                    g.skillCooldown<=0f&&g.projectiles.Count==0,
                    "Sealed-foot Q cannot mint a spell, immunity or cooldown");

                g.save.x=10.5f;g.save.y=10.5f;
                Check(g.CanStand(g.Position),
                    "Recovering the original authorized foot restores legal combat");
                g.Attack(new Point(1,0));
                Check(g.playerAction!=null&&
                    g.playerAction.kind=="attack"&&
                    g.playerAction.equippedWeapon==weapon&&
                    g.attackCooldown>0f,
                    "All nine class-loadout pairs restore real physical attack and cooldown on legal ground");
            }
            }
            finally { data.maps[0]=original; }
        }

        public static void BufferedQRechecksCurrentFloorBeforeSpendingEnergy(Campaign data)
        {
            MapData original=data.maps[0];
            try
            {
                data.maps[0]=CombatWallTestMap();
            for(int profession=0;profession<3;profession++)
            {
                var g=Fresh(data,profession);
                g.enemies.Clear();g.projectiles.Clear();
                g.save.x=10.5f;g.save.y=10.5f;
                g.save.energy=100f;
                g.playerHitTime=.08f;
                g.Skill(new Point(1,0));
                Check(g.SkillQueued&&g.skillCooldown<=0f&&g.save.energy==100f,
                    "Q can be queued legally during existing real hit recovery");
                g.save.x=9.5f;g.save.y=10.5f;
                for(int i=0;i<7;i++)g.Tick(.02f,new Point());
                Check(!g.SkillQueued&&g.playerAction==null&&
                    g.skillCooldown<=0f&&g.save.energy==100f,
                    "A closing floor during the original Q buffer cannot charge or emit a ghost cast");
                g.save.x=10.5f;g.save.y=10.5f;
                g.Skill(new Point(1,0));
                Check(g.playerAction!=null&&g.playerAction.kind=="skill"&&
                    g.skillCooldown>0f&&g.save.energy==60f,
                    "A new deliberate Q from legal ground still works after the canceled intent");
            }
            }
            finally { data.maps[0]=original; }
        }
    }
}
