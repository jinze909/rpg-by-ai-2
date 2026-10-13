using System;
namespace Starfall
{
    // Physical field landmarks, additive to the existing campaign and SaveData v2.
    public sealed partial class GameCore
    {
        private bool TalkAnnexExploration(WorldObject o)
        {
            string kind=o.kind;
            if(kind=="annexinnarchive" || kind=="annexinnmessage")
            {
                if(save.room!=9 || (Position-new Point(o.x,o.y)).Length>2.2f)return true;
                if(kind=="annexinnmessage")
                {
                    bool firstLetter=!Has("annex_inn_letter");
                    if(firstLetter){Flag("annex_inn_letter");AddXp(15);revision++;sound="reward";}
                    Dialog(o.name,o.en,
                        "给每一位夜归的人：如果你迷路了，沿着有人点起营火的方向走。道路不是堡垒的财产，也不是公会的财产。它是记得你还要回家的人留下的。"+
                        (firstLetter?"\\n\\n首次读信：经验 +15。":""),
                        "To every late traveler: when lost, follow the light of a shared campfire. A road belongs neither to a fortress nor a guild; it belongs to those who remember you are coming home."+
                        (firstLetter?"\\n\\nFirst reading: +15 XP.":""),
                        C("留下这封信","Keep the letter","close"));
                    return true;
                }
                int camps=0,cache=0,vistas=0;
                for(int region=0;region<8;region++)
                {
                    if(Has("annex_camp_annex_camp_"+region))camps++;
                    if(Has("annex_vista_annex_vista_"+region))vistas++;
                    if(Has("annex_cache_annex_cache_"+region+"_a"))cache++;
                    if(Has("annex_cache_annex_cache_"+region+"_b"))cache++;
                }
                bool complete=Has("annex_archive_complete");
                bool ready=camps==8&&vistas==8&&cache==16;
                Dialog(o.name,o.en,
                    "这是巡路队留在炉火旅店的真正旅行档案。你的步迹必须是实地走过的，而不是地图上凭空出现的标记。"+
                    "\\n\\n营地 "+camps+"/8  ·  观景点 "+vistas+"/8  ·  隐藏补给 "+cache+"/16。"+
                    (complete?"\\n\\n这份公开地图已经交给所有旅人。":
                        ready?"\\n\\n每一处地点都被亲自验证。可以公开最后的归乡图。":
                        "\\n\\n继续探索全部八个户外区域，回来归档旧路。"),
                    "The Hearth Inn holds the wardens' actual travel record. Every mark must be made by walking, never conjured on a hidden map."+
                    "\\n\\nCamps "+camps+"/8 · Lookouts "+vistas+"/8 · Caches "+cache+"/16."+
                    (complete?"\\n\\nYour route chart is now public.":ready?
                        "\\n\\nAll positions verified. The final homeward chart can be published.":
                        "\\n\\nExplore all eight outdoor regions and return to publish their chart."),
                    C(complete?"重读归乡图":"完成归档 · 公共星图",
                        complete?"Reread the chart":"Publish the public chart","annex_archive_file"),
                    C("继续旅程","Keep exploring","close"));
                return true;
            }
            if(kind!="annexcache"&&kind!="annexcamp"&&kind!="annexvista")return false;
            if(save.room<0||save.room>7||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            string key=(kind=="annexcache"?"annex_cache_":kind=="annexvista"?"annex_vista_":"annex_camp_")+o.id;
            bool first=!Has(key);
            if(first)
            {
                Flag(key);sound="reward";
                if(kind=="annexcache")
                {
                    save.coins+=7;
                    int item=save.room==1||save.room==7?2:save.room==3||save.room==6?1:0;
                    save.inventory[item]+=2;
                }
                else if(kind=="annexvista")
                {
                    AddXp(12);
                    int cx=(int)o.x/6,cy=(int)o.y/6;
                    for(int x=cx-1;x<=cx+1;x++)for(int y=cy-1;y<=cy+1;y++)
                        if(x>=0&&x<12&&y>=0&&y<8&&(x*6>=36||y*6>=24))
                            Flag("annex_seen_"+save.room+"_"+x+"_"+y);
                }
                else
                {
                    save.coins+=5;
                    bool all=true;
                    for(int i=0;i<8;i++)if(!Has("annex_camp_annex_camp_"+i))all=false;
                    if(all&&!Has("annex_camps_complete")){Flag("annex_camps_complete");save.skillPoints++;}
                }
                revision++;
            }
            string[] zh={"外渠的雨脚印一直指向镇里。","荆棘远眺台的树根曾为旅客挡风。",
                "沉桥后湾里有不在公会账本上的船板。","荒矿旧道留下先撤人再开机的划痕。",
                "堡垒外墙的石阶原本供难民歇脚。","灯湾旧船铃是为错过退潮的人准备的。",
                "折光沙脊后藏着商队不曾登记的井。","霜钟背风崖的箭头指向最近的火盆。"};
            string[] en={"Rain-worn prints along the outer canal lead home.","Roots at the Thornwood overlook once sheltered travelers.",
                "Unregistered ferry planks remain in the backwater.","Mine marks command evacuation before restart.",
                "Outer citadel steps once sheltered displaced families.","The old ship bell still guides stranded travelers.",
                "A hidden well lies beyond the dunes.","A frost cliff sign points to the nearest hearth."};
            string noteZh=zh[save.room],noteEn=en[save.room];
            if(kind=="annexcache")
            {
                Dialog(o.name,o.en,noteZh+(first?"\n\n遗失补给：硬币 +7、材料 +2。":"\n\n物资已被取走，记录仍在。"),
                    noteEn+(first?"\n\nRecovered supplies: +7 coins, +2 materials.":"\n\nSupplies already recovered."),
                    C("继续巡路","Continue","close"));
                return true;
            }
            if(kind=="annexvista")
            {
                Dialog(o.name,o.en,noteZh+(first?"\n\n附近九块测绘区已记入星图。经验 +12。":"\n\n附近路线已在星图上。"),
                    noteEn+(first?"\n\nCharted nearby terrain sectors. +12 XP.":"\n\nNearby bearings already charted."),
                    C("收起星图","Fold chart","close"));
                return true;
            }
            Dialog(o.name,o.en,noteZh+
                    "\n\n只有没有敌人追击时才能安全生火。消耗木材 ×1，恢复生命 40 和全部耐力。"+
                    (first?"\n\n首次发现营地，硬币 +5。":"")+
                    (Has("annex_camps_complete")?"\n\n八处营地均已记载！技能点 +1。":""),
                    noteEn+"\n\nRest safely away from enemies. 1 timber restores 40 HP and full energy."+
                    (first?"\n\nFirst discovery: +5 coins.":"")+
                    (Has("annex_camps_complete")?"\n\nAll eight camps mapped: +1 talent point.":""),
                    C("添柴休息 · 木材 ×1","Rest · 1 timber","annex_rest"),
                    C("继续探索","Keep exploring","close"));
            return true;
        }
        private bool ChooseAnnexExploration(string action)
        {
            if(action=="annex_archive_file")
            {
                if(save.room!=9||Dying||practicing||Has("annex_archive_complete"))
                    return true;
                WorldObject archive=null;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="annexinnarchive"){archive=o;break;}
                if(archive==null||(Position-new Point(archive.x,archive.y)).Length>2.2f)
                    return true;
                for(int region=0;region<8;region++)
                    if(!Has("annex_camp_annex_camp_"+region)||
                        !Has("annex_vista_annex_vista_"+region)||
                        !Has("annex_cache_annex_cache_"+region+"_a")||
                        !Has("annex_cache_annex_cache_"+region+"_b"))
                    {
                        Notify("星图还缺少实际走过的地点。","Some routes have yet to be visited.");
                        return true;
                    }
                Flag("annex_archive_complete");
                save.coins+=60;save.shards+=3;save.skillPoints++;
                RecordMoment("八地营地、观景点与补给旧路均已核实。炉火旅店的公开星图不再遗漏任何普通旅人。",
                    "Eight regions of camps, lookouts and recovered supplies are now public on the Hearth Inn's atlas.");
                revision++;sound="reward";
                Notify("归乡图已公开：硬币 +60，碎片 +3，技能点 +1。",
                    "Public chart complete: +60 coins, +3 shards, +1 talent point.");
                return true;
            }
            if(action!="annex_rest")return false;
            if(save.room<0||save.room>7||mode!="world"||Dying||practicing)return true;
            WorldObject camp=null;
            foreach(WorldObject o in Map.objects)if(o.kind=="annexcamp"){camp=o;break;}
            if(camp==null||(Position-new Point(camp.x,camp.y)).Length>2.2f)return true;
            if(!SafeTravel){Notify("战斗中不能休息。","Cannot rest while enemies are nearby.");return true;}
            if(!Spend(1,0,0,0,0))return true;
            save.health=Clamp(save.health+40,0,save.maxHealth);save.energy=100;
            revision++;sound="reward";
            Notify("营火补给：生命恢复 40，耐力全满。","Campfire: +40 HP and full stamina.");
            return true;
        }
    }
}
