using System;
namespace Starfall
{
    // Original art, coordinates, save format and story decisions are untouched.
    // New physical locations are reached in-world, not auto-awarded on load.
    public sealed partial class GameCore
    {
        // Each regional log is something a traveler can actually find and
        // read: a physical clue, the damaged signal, then its abandoned chest.
        // Existing routes, interactions, rewards and persistent flags stay intact.
        private static readonly string[,] OuterLogZh = {
            {"水位刻线到了第三道铁环，镇里的潮表却只记到第二道。有人用煤灰在石壁上写着：那夜水已经进屋。", "桥东的玻璃灯罩还在，底座只缺一枚零件。灯亮起来，渠外的木屋就能先看见涨水。", "箱盖上刻着“桥下六户”。两双湿手套裹着药草，手套的主人一直没来取。"},
            {"断根里夹着一张避难名单，最后三个名字被雨浸透。旁边有人补写：他们往北走了。", "两棵树间的灯绳还绷着，林梢灯却灭了。修好它，夜归的人才能看清通往庇护点的岔路。", "潮湿木匣里有草药和一张手绘图，三处可以避雨的树洞都被仔细圈了起来。"},
            {"旧船票背面记着一次提前十五分钟的退潮。船长用红笔划掉了印在正面的官方时刻。", "木桩上还钉着渡船的旧航线牌。低潮信标缺一枚零件，修好后，摆渡人就能依真实潮位靠岸。", "药草与船钉塞在同一只救援箱里，下面压着写有四个乘客名字的半张舱单。"},
            {"停机名单有十二个签名，最先签字的是学徒。名字旁边留着一行很小的字：先把伤员送出去。", "工棚号灯的铁架被烟熏黑，线圈还在，只缺替换零件。它曾照着矿工走过紧急出口。", "矿车后锁着几副护目镜和一包绷带。一副小号护目镜的绑带已经缝过两次。"},
            {"旧登记册只写“撤离四十三人”。夹在书页里的便签又添了六个名字：这些人留在门外帮忙开锁。", "外墙疏散灯的开关被封条盖住。撕开封条，换好零件，城门两侧的人都能看见灯。", "药箱上的徽章磨得只剩半圈金边。里面的绷带分别标着守卫营房和城外帐篷的日期。"},
            {"最后一艘船的航海手记写着“北岸有火光，改向救人”。港务记录却把这次转向记成了擅自离航。", "归航灯塔的旧反光镜还对着海面，灯芯却熄了。只要修好它，夜航船就能看见进港水道。", "浮木上刻着失踪船员的名字。木箱底部还压着给获救孩子准备的干粮袋。"},
            {"配水簿上每口井都有两栏：商队和居民。居民那一栏被整页墨迹盖住，页角却还留着井水的数字。", "绿洲路口的信号灯被风沙埋到半腰。替它换好零件，迷路的人才找得到共享水站。", "箱里有包好的药草和一只空水壶。壶口绑着纸条：给南边那户还在等水的人。"},
            {"雪里露出半截求救纸条：往背风坡走，炉火还没灭。箭头却被后来贴上的撤离告示盖住了。", "雪路上的指灯只剩灯架，缺一枚零件。风再大，点亮它也能从远处看见通向暖棚的山脊。", "补给箱里留着干柴、布条和半块火石。盖子内侧画了一条通往最近暖棚的细线。"}
        };
        private static readonly string[,] OuterLogEn = {
            {"The flood reached the third iron ring. The town gauge stops at the second. Someone wrote in charcoal on the stone: the water was already inside our homes.", "The glass lantern still hangs east of the bridge. Its socket needs one spare part. Lit again, it could warn the houses beyond the canal before the water rises.", "SIX HOMES BELOW THE BRIDGE is carved on the lid. Two pairs of wet gloves wrap the herbs. Nobody came back for them."},
            {"A shelter list is wedged into a broken root. Rain washed out the last three names. Someone added: they went north.", "The rope is still taut between two trees, but the canopy lantern has gone dark. Relight it so late travelers can see the fork toward shelter.", "Herbs and a hand-drawn map fill the damp box. Three hollow trees where people could escape the rain are circled carefully."},
            {"A ferry ticket has an ebb time scribbled on the back, fifteen minutes earlier than scheduled. The captain crossed out the printed time in red.", "The ferry's old route sign is still nailed to a post. Its ebb signal needs one part; once repaired, the ferryman can steer by the actual tide.", "Herbs and boat rivets share the rescue chest. Half a passenger manifest lies underneath, with four names still legible."},
            {"Twelve names fill the shutdown petition. An apprentice signed first and wrote in tiny letters: get the injured outside before anything else.", "Soot blackens the work-shed lamp. The coil survived; one replacement part is missing. It once lit the emergency exit for the miners.", "Goggles and bandages were locked behind the mine cart. The strap on one small pair of goggles has been stitched twice."},
            {"The old ledger says FORTY-THREE EVACUATED. A note tucked inside adds six names: the people who stayed outside to work the jammed lock.", "A seal covers the evacuation lamp's switch. Break the seal and replace the part, and people on both sides of the wall can see the light.", "Only half the gold rim of the badge remains. Bandages inside are dated for both the guard barracks and the tents beyond the wall."},
            {"The last boat's log reads: FIRE ON THE NORTH SHORE, TURNING TO RESCUE. The harbor office recorded that same turn as desertion.", "The homeward lantern's old mirror still faces the sea; its wick is dark. Repair it so boats at night can find the harbor channel.", "Missing sailors' names are carved into a float. Below it lies a packet of dry food prepared for children brought ashore."},
            {"Each well has two columns in the water ledger: caravans and residents. Ink blots out the residents' share, but the figures survive in the margin.", "Sand has buried half the oasis signal. A replacement part would let travelers find the shared water station again.", "Wrapped herbs and an empty canteen lie inside. A note tied to its neck says: for the family south of here still waiting for water."},
            {"A distress note sticks out of the snow: take the lee slope; the hearth is still burning. A later evacuation notice was pasted over its arrow.", "Only the snowline lamp's frame remains. One part would restore a light visible along the ridge, even in a high wind.", "Dry firewood, cloth strips and half a flint remain in the chest. A narrow line inside the lid points toward the nearest warming shelter."}
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

        // A repaired rescue light is a REAL field respite, not a map-wide
        // buff. The actual player must stand beside its physical mast on
        // walkable ground and share an unobstructed local route with it.
        // An active raid, timed mission, race, practice or nearby enemy
        // suspends recovery. Existing v2 flags are the only saved state.
        public bool OuterRelayRestRefuge
        {
            get
            {
                if(mode!="world"||save.room<0||save.room>=8||
                    !OuterRelayRestored(save.room)||Dying||practicing||
                    racing||FieldWorkPreventsRest||OuterPatrolActive||
                    !SafeTravel||Map.objects==null)return false;
                foreach(WorldObject light in Map.objects)
                    if(light.kind=="outerrelay"&&
                        PhysicalRefugeReach(new Point(light.x,light.y),3.05f))
                        return true;
                return false;
            }
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
