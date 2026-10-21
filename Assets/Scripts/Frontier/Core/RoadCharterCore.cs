using System;
using System.Collections.Generic;

namespace Starfall
{
    // Chapter Eight begins only after the public hearing and Orrin's signed
    // civilian commitment. Real east-branch field evidence drives the choice.
    // All durable progress remains in SaveData v2 string flags.
    public sealed partial class GameCore
    {
        public bool RoadCharterReady
        { get { return HearingComplete && HearingOrrinResponsibility != 0; } }
        public bool RoadCharterStarted { get { return Has("roadcharter_started"); } }
        public bool RoadCharterComplete { get { return Has("roadcharter_complete"); } }
        public int RoadCharterReports
        {
            get
            {
                int reports = 0;
                for (int i = 1; i <= 3; i++)
                    if (Has("roadcharter_report_" + i)) reports++;
                return reports;
            }
        }
        private void AddRoadCharterGuildChoice()
        {
            if (!RoadCharterReady || conversation == null) return;
            var choices = new List<Choice>(conversation.choices);
            choices.Add(C(RoadCharterComplete ? "重读第八章 · 共同行路" : "第八章 · 共同行路",
                RoadCharterComplete ? "Reread Chapter Eight · Roads for Everyone" :
                "Chapter Eight · Roads for Everyone", "roadcharter_guild"));
            conversation.choices = choices.ToArray();
        }
        private bool RecordRoadCharterWaypoint(WorldObject o)
        {
            int region = save.room;
            if (!RoadCharterReady || !RoadCharterStarted ||
                RoadCharterComplete || region < 1 || region > 3 ||
                o==null || o.kind != "waytrail" || o.id != "waytrail" + region + "e" ||
                !HasPhysicalInteractionReach(o) ||
                Has("roadcharter_report_" + region)) return false;
            Flag("roadcharter_report_" + region);
            RecordMoment(
                new[] { "", "第八章实地证据：密林的旧路原本庇护落难者，树林的照护权不应被剥夺。",
                    "第八章实地证据：沉桥旧钉证明公共渡口曾被挪走，居民并非货单上的数字。",
                    "第八章实地证据：矿场旧铭牌要求先撤人再开机，工人的停机权必须落实。" }[region],
                new[] { "", "Chapter Eight field evidence: forest routes once sheltered travelers; healing belongs to its residents.",
                    "Chapter Eight field evidence: bridge rivets show the public crossing was diverted; people are not freight.",
                    "Chapter Eight field evidence: the old mine order puts evacuation before production and protects shutdown rights." }[region]);
            revision++; sound = "quest";
            return true;
        }
        private bool ChooseRoadCharter(string action)
        {
            if (action != "roadcharter_guild" && action != "roadcharter_begin" &&
                action != "roadcharter_track" && action != "roadcharter_public" &&
                action != "roadcharter_reserve") return false;
            if (!RoadCharterReady || save.room != 0 || Dying || practicing ||
                (Position - ObjectPoint(0, "guild", new Point(-100, -100))).Length > 2.2f)
                return true;
            if (action == "roadcharter_guild")
            {
                if (RoadCharterComplete)
                {
                    Dialog("莉雅 · 第八章旧路公约", "Lyra · Chapter Eight Road Charter",
                        Has("roadcharter_public") ?
                            "三份真实道路证据已在公会公开。道路优先为行人服务；你标记过的旧路如今连脚步都更加轻快。" :
                            "三份道路证据已在公会公开。紧急通道留下了夜间巡路的供能余量，你行走与守护时的耐力正在更快恢复。",
                        Has("roadcharter_public") ?
                            "Three field reports are public. Everyday travelers now lead the way; your walking speed reflects the reopened paths." :
                            "Three field reports are public. Emergency routes retain civilian energy reserves, improving your stamina recovery.",
                        C("继续在新地图探索", "Keep exploring the new routes", "close"));
                    return true;
                }
                if (!RoadCharterStarted)
                {
                    Dialog("莉雅 · 第八章旧路公约", "Lyra · Chapter Eight Road Charter",
                        "莉雅：公开听证解决了谁有权决定，但没有解决一个现实问题——人们要怎样走到那些公共设施？\n\n请你亲自前往密林、沉没遗迹和矿场新开放的东部支路，在旧路石碑上核对巡路队留下的道路证据。我们不会再把谁能通行交给堡垒的秘密地图。",
                        "Lyra: The hearing settled who gets to decide. But how can ordinary people actually reach those public services?\n\nTravel to the newly opened eastern branches in Thornwood, Drowned Ruins and Ironreach. Read the old road markers there. No secret citadel chart should decide who can get home.",
                        C("开启第八章 · 实地测绘", "Begin Chapter Eight · Survey the routes", "roadcharter_begin"),
                        C("我先准备", "Not yet", "close"));
                    return true;
                }
                if (RoadCharterReports < 3)
                {
                    Dialog("莉雅 · 旧路公约", "Lyra · Road Charter",
                        "我们收到 " + RoadCharterReports + "/3 份实地证据。不是只看公会的图纸——亲自走到三个地区东侧的旧路标前，才能知道被遗漏的是谁。",
                        "We have " + RoadCharterReports + "/3 field reports. Guild maps alone are not evidence. Visit the eastern waymarkers in the three original regions.",
                        C("追踪第八章", "Track Chapter Eight", "roadcharter_track"),
                        C("继续探索", "Continue surveying", "close"));
                    return true;
                }
                Dialog("莉雅 · 旧路公约", "Lyra · Road Charter",
                    "三份证据齐了。密林树根、沉桥铁钉和矿工旧令都证明，同一条路曾被写进不同人的账本。\n\n【行人优先】开放通行图，所有职业获得真实的常驻步速提升。\n【民用应急供能】保留救援路线灯，所有职业获得真实的常驻耐力恢复提升。\n\n两种方式都公开地图，不撤销任何既有地方公约。",
                    "All three field reports are here: roots, bridge rivets and the mine safety order tell different sides of the same road.\n\n[Everyday access] Publish walkable civilian paths, granting permanent walking speed.\n[Civic emergency reserves] Keep rescue lights powered, granting permanent stamina regeneration.\n\nBoth publish the maps and preserve every earlier local covenant.",
                    C("公布行人优先的公共道路", "Publish pedestrian-first paths", "roadcharter_public"),
                    C("留出民用救援供能", "Reserve energy for civilian rescue", "roadcharter_reserve"),
                    C("再与居民商量", "Consult residents", "close"));
                return true;
            }
            if (action == "roadcharter_begin")
            {
                if (RoadCharterStarted) return true;
                Flag("roadcharter_started"); TrackQuest("roadcharter");
                RecordMoment("第八章开始：公会不再只绘制道路，而要带上居民的证词亲自检验每条新开放的路线。",
                    "Chapter Eight begins: the guild will verify new public roads using lived evidence from the communities themselves.");
                Notify("第八章已开启：去三处东部旧路标收集证据。", "Chapter Eight started: survey three eastern waymarkers.");
                return true;
            }
            if (action == "roadcharter_track")
            {
                if (RoadCharterStarted) TrackQuest("roadcharter");
                return true;
            }
            if (!RoadCharterStarted || RoadCharterComplete || RoadCharterReports != 3)
                return true;
            bool publicRoute = action == "roadcharter_public";
            Flag(publicRoute ? "roadcharter_public" : "roadcharter_reserve");
            Flag("roadcharter_complete");
            CreditCoins(45); CreditShards(3); AddXp(60);
            revision++; sound = "reward";
            RecordMoment(
                publicRoute ?
                    "第八章公约发布：旧路地图公开给每一个行人；新的平民通道让探索步速永久提升。" :
                    "第八章公约发布：旧路照明保留民用应急储备；救援者的耐力恢复得到永久支持。",
                publicRoute ?
                    "Chapter Eight enacted: road maps belong to everyone, granting lasting movement speed." :
                    "Chapter Eight enacted: public road lamps retain emergency energy, granting lasting stamina recovery.");
            Notify(publicRoute ?
                "第八章完成！公开道路：步速永久提升。获得45硬币、3碎片、60经验。" :
                "第八章完成！民用储备：耐力恢复永久提升。获得45硬币、3碎片、60经验。",
                publicRoute ?
                    "Chapter Eight complete! Walking speed increased. +45 coins, +3 shards, +60 XP." :
                    "Chapter Eight complete! Stamina regeneration increased. +45 coins, +3 shards, +60 XP.");
            return true;
        }
    }
}
