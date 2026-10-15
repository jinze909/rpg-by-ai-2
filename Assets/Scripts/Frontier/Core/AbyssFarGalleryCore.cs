using System;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // Extra seeded galleries are optional. All loot is pending until the
        // original root gate banks the floor; exiting or dying loses it.
        private void TalkAbyssFarGallery(WorldObject o)
        {
            if(!InAbyss||Dying||practicing||
                (Position-new Point(o.x,o.y)).Length>2.2f)return;
            bool survey=o.kind=="abyssFarSurvey";
            bool cache=o.kind=="abyssFarCache";
            bool echo=o.kind=="abyssFarEcho";
            string proof="abyss_annex_far_survey_"+save.abyssFloor;
            if(echo&&!Has(proof))
            {
                Dialog(o.name,o.en,
                    "回声石只重复破碎的呼声。先去南侧发现真正留下这段话的人。",
                    "The echo stone repeats only fragments. Find the original testimony to the south first.",
                    C("继续探查","Seek the testimony","close"));
                return;
            }
            string flag="abyss_annex_far_"+(survey?"survey_":cache?"cache_":"echo_")+save.abyssFloor;
            bool first=!Has(flag);
            if(first)
            {
                Flag(flag);
                if(survey)save.runXp+=25;
                else if(cache){save.runCoins+=30;save.runShards++;}
                else save.runXp+=20;
                revision++;sound="reward";
            }
            string zh=survey?
                "这块地脉残碑留下六次撤离的真实坐标。星根并不只供一面城墙发光，它也曾为困在地下的普通人保留回家的路。":
                cache?
                "巡路者在洞底封存的药材和原料，直到今天才重新看见微光。它们依旧属于本层未结算的远征成果。":
                "回声石终于接上地脉残碑的完整证词：原来的地下通道是为分享能源而修，而不是为封锁彼此而生。";
            string en=survey?
                "A lower root tablet records six evacuation bearings. Starroot once guided ordinary people out of the mines, not merely powered a wall.":
                cache?
                "Supplies sealed in this bottom gallery are still unbanked expedition loot until the original root gate settles the floor.":
                "The echo stone completes the testimony: these passageways were built to share energy, not close communities off from one another.";
            Dialog(o.name,o.en,zh+(first?
                    "\n\n此层待结算："+(cache?"硬币 +30、碎片 +1。":survey?"经验 +25。":"经验 +20。"):
                    "\n\n该层记录已保存，无法重复领取。"),
                en+(first?
                    "\n\nUnbanked: "+(cache?"+30 coins, +1 shard.":survey?"+25 XP.":"+20 XP."):
                    "\n\nRecorded for this floor; no duplicate loot."),
                C("收下记录","Keep the account","close"));
        }
    }
}
