using System;
namespace Starfall
{
    // Original art, coordinates, save format and story decisions are untouched.
    // New physical locations are reached in-world, not auto-awarded on load.
    public sealed partial class GameCore
    {
        private static readonly string[,] OuterLogZh = {
            {"旧运河的潮水表与镇内档案不符。有人把居民自建的泄洪渠从图上删掉了。", "引水警灯的灯座完好，只少了一枚零件。把警报还给住在城外的人。", "储物箱里有洪水夜留下的草药与旧手套，它们属于当时没被登记的守渠人。"},
            {"断裂树根里夹着避难名单。风暴期间有陌生人替伤者引路，却未留下姓名。", "一盏林梢灯仍能连接上一个庇护点。修好它，晚归的人就不会走向荆棘。", "巡林物资封在潮湿木匣里。没人来收，但补给永远比一份好看的统计表重要。"},
            {"老船票背面写着真实退潮时间，比运输船队的报表早整整一刻钟。", "修复低潮信标，下一艘摆渡船就不用再听从错误的航路命令。", "药材和船钉被装进同一个救援箱——救回岸边的人，比恢复旧秩序更重要。"},
            {"停机名单里有十二个签名，第一位签署者是矿场最年轻的学徒。", "工棚号灯只差一枚零件；工人不应再等远处的监工允许撤离。", "救援物资被锁在矿车后，里面还存着最后一班工人的护目镜。"},
            {"民用避难登记只写着人数，没有留下多少人曾为他人打开城门。", "外墙的疏散灯本应由居民维护，而不是由一个秘密命令决定何时亮起。", "医药箱表面有磨损的徽章；守卫和难民曾同时使用它。"},
            {"船难手记详细记录了转向北岸的最后一次求救，不是船员逃亡的证明。", "归航灯可以从远处看见，恢复它意味着不用再靠被篡改的名册认路。", "救援箱的浮木刻有失踪者名字，里面的食物原本准备给获救的孩子。"},
            {"旧配水笔记把每一口井分为商队与平民，后者的数字被空白替代。", "点亮绿洲信号灯，缺水的人才能在风暴里找到共享水站。", "沙海救援箱装着未交付的草药，也装着无声的供水请求。"},
            {"求救纸条被冰雪压住，留下的是附近有人生火的方向，不是错误的撤离路线。", "风雪指路灯被拆走一枚零件，只要重新点亮，下一位旅人就有路可回。", "高地补给箱里还有未用完的取暖材料，适合转送到最近的避寒棚。"}
        };
        private static readonly string[,] OuterLogEn = {
            {"The canal's flood gauge contradicts the official chart. Someone erased a civilian-built spillway.", "The warning lamp needs one spare part. Give the flood alert back to families living outside the walls.", "Medicinal supplies were left for flood wardens whose names never entered the town ledger."},
            {"A shelter roll is wedged between broken roots. Anonymous neighbors guided the injured here.", "One repaired canopy lamp can connect two safe shelters, rather than send late travelers into thorns.", "The wardens left real supplies behind; they matter more than a flattering report."},
            {"The handwritten ebb tide precedes the shipping schedule by fifteen minutes.", "Repairing the low-tide signal means the next ferry need not follow a false navigation order.", "The chest combines herbs with ferry rivets. Rescue matters more than restoring the old manifest."},
            {"Twelve miners signed the shutdown list. The youngest apprentice signed first.", "The evacuation lamp lacks one part. Workers should not need an overseer's approval to escape.", "The chest holds emergency worksite supplies and the goggles of the last shift."},
            {"The civilian roster counted bodies, but not those who opened the gates for others.", "This evacuation light belongs to the residents, not another sealed military order.", "An old, worn badge marks the medical chest shared by guards and refugees."},
            {"A survivor's letter records the last boat's turn north. It was rescue, not desertion.", "Restore the homeward lamp so vessels no longer rely on an altered passenger list.", "Names are carved into the rescue float, beside provisions for children taken ashore."},
            {"The water ledger counted merchants and families differently; residents were left as blanks.", "Relight the oasis signal so thirsty families can find the shared well during a sandstorm.", "The desert chest holds undelivered medicine and another quiet request for fair water."},
            {"A distress note points to the nearest hearth, not to the official evacuation corridor.", "Replace the snowline lamp's missing part so the next traveler has a safe bearing.", "The mountain supplies still hold fuel for a nearby warming shelter."}
        };
        public bool OuterLogFound(int room)
        { return room>=0&&room<8&&Has("outer_log_"+room); }
        public bool OuterRelayRestored(int room)
        { return room>=0&&room<8&&Has("outer_relay_"+room); }
        public bool OuterCacheRecovered(int room)
        { return room>=0&&room<8&&Has("outer_cache_"+room); }
        public int OuterRescueCount
        {
            get { int n=0; for(int room=0;room<8;room++)
                if(OuterCacheRecovered(room))n++;
                return n; }
        }

        private bool TalkOuterRescue(WorldObject o)
        {
            if(o.kind!="outerlog"&&o.kind!="outerrelay"&&o.kind!="outercache")return false;
            int room=save.room;
            if(room<0||room>=8||!HasPhysicalInteractionReach(o))return true;
            string titleZh=o.name, titleEn=o.en;
            if(o.kind=="outerlog")
            {
                bool first=!OuterLogFound(room);
                if(first){Flag("outer_log_"+room);AddXp(8);sound="reward";}
                Dialog(titleZh,titleEn,OuterLogZh[room,0]+
                    (first?"\n\n首次记录：经验 +8。继续前往东侧的失明信号灯。":
                    "\n\n线索已记录，继续查找失明的信号灯。"),
                    OuterLogEn[room,0]+
                    (first?"\n\nFirst discovery: +8 XP. Seek the dark eastern beacon.":
                    "\n\nEvidence recorded. Find the unlit rescue beacon."),
                    C("记录线索","Record evidence","close"));
                return true;
            }
            if(o.kind=="outerrelay")
            {
                bool recorded=OuterRelayRestored(room);
                if(!OuterLogFound(room))
                {
                    Dialog(titleZh,titleEn,OuterLogZh[room,1]+
                        "\n\n灯座上的编号已经模糊，先去南边查找它所属的原始记录。",
                        OuterLogEn[room,1]+
                        "\n\nThe serial number is faded. Locate the original document to the south.",
                        C("沿路找记录","Find the log","close"));
                    return true;
                }
                Dialog(titleZh,titleEn,OuterLogZh[room,1]+
                    (recorded?"\n\n灯已经重新亮起。救援箱的位置可以辨认。":
                        "\n\n消耗 1 个零件点亮信号，开放最后一段救援记录。"),
                    OuterLogEn[room,1]+
                    (recorded?"\n\nThe repaired lamp points toward the last rescue chest.":
                        "\n\nSpend one spare part to restore the signal and reveal the final rescue leg."),
                    C(recorded?"保持信号畅通":"维修信号灯 · 零件×1",
                      recorded?"Maintain the signal":"Repair signal · 1 spare part",
                      recorded?"close":"outer_repair"),
                    C("稍后再来","Return later","close"));
                return true;
            }
            if(!OuterRelayRestored(room))
            {
                Dialog(titleZh,titleEn,
                    "救援箱的封条需要信号站的通行标记。先调查南侧旧记录，再修复东侧求援灯。",
                    "The rescue cache requires a verified relay signal. Find the southern log, then repair the eastern beacon.",
                    C("继续调查","Continue searching","close"));
                return true;
            }
            bool firstCache=!OuterCacheRecovered(room);
            if(firstCache)
            {
                int material = room % 3;
                // Chests are one-time physical supplies. Unlike narrative
                // evidence, leave the recovery flag unset until ALL rewards
                // fit; otherwise a full stack erases the chest on reload.
                if (save.coins > 999981 || save.inventory[material] > 9997)
                {
                    Notify("救援箱物资尚未领取。请留出18硬币和2份材料的空间。",
                        "Relief chest still holds supplies. Free room for 18 coins and 2 materials.");
                    return true;
                }
                Flag("outer_cache_"+room);
                CreditCoins(18);
                CreditInventory(material, 2);
                AddXp(22);sound="reward";revision++;
                if(OuterRescueCount==8&&!Has("outer_rescues_complete"))
                { Flag("outer_rescues_complete");CreditSkillPoints(1);revision++; }
                RecordMoment("第 "+(room+1)+" 片边境的求援灯已恢复，救援箱里留下的人名与物资都被找回。",
                    "The remote rescue signal and relief chest were recovered in region "+(room+1)+".");
            }
            Dialog(titleZh,titleEn,OuterLogZh[room,2]+
                (firstCache?"\n\n完成地区救援线路：硬币 +18、材料 +2、经验 +22。":
                "\n\n这里的救援物资已经被登记和转移。"),
                OuterLogEn[room,2]+
                (firstCache?"\n\nRegional rescue complete: +18 coins, +2 materials, +22 XP.":
                "\n\nThese supplies have already been recovered and documented."),
                C("记下归途","Record the route","close"));
            return true;
        }
        private bool ChooseOuterRescue(string action)
        {
            if(action!="outer_repair")return false;
            if(save.room<0||save.room>=8||Dying||practicing||
                !OuterLogFound(save.room)||OuterRelayRestored(save.room))return true;
            WorldObject relay=null;
            foreach(WorldObject o in Map.objects)
                if(o.kind=="outerrelay"){relay=o;break;}
            if(relay==null||!HasPhysicalInteractionReach(relay))return true;
            if(!SafeTravel)
            { Notify("敌人太近，无法安全修理信号灯。","Enemies are too close to safely repair the relay.");return true; }
            if(!Spend(0,0,0,0,1))return true;
            Flag("outer_relay_"+save.room);
            revision++;sound="reward";
            Notify("求援灯已恢复！现在可以前往救援箱。",
                "Rescue beacon restored! The relief cache can now be opened.");
            return true;
        }
    }
}
