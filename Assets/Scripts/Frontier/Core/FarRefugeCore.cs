using System;
namespace Starfall
{
    // Three authentic walk-to sites per exterior. A field log exposes the
    // correct local dial setting; a calibrated dial unlocks an actual refuge.
    // Only old SaveData v2 string flags persist; world/canon remain unchanged.
    public sealed partial class GameCore
    {
        private static readonly int[] FarDialAnswers={1,2,0,2,1,0,1,2};
        private static readonly string[] DialSignsZh={"琥珀","潮蓝","苔绿"};
        private static readonly string[] DialSignsEn={"Amber","Tideblue","Mossgreen"};
        private static readonly string[] FarWitnessZh={
            "镇外的洪水划痕比官方堤图更深，南边的逃生道曾由居民亲手修复。",
            "老根下的庇护记录提到，森林在最冷的时候仍把风挡在孩子身后。",
            "船工把真正的涨潮时刻刻在桩上；这份记录没有进入任何货单。",
            "矿工签署的停工清册写着先护送受伤者，再关闭晶矿抽取机。",
            "紧急疏散日志留下城外难民与守门卫兵共同打开侧门的时刻。",
            "雾港的航路修订来自水手们的口述，而不是过时的海关账簿。",
            "未登记水井旁的石片写着绿洲儿童的用水份额，后来被风沙盖住。",
            "避寒手册记下每个小棚的火种来源，钟声应当引导回家的人。"
        };
        private static readonly string[] FarWitnessEn={
            "The flood scar lies higher than official canal charts; residents rebuilt the southern escape walk themselves.",
            "Records under the old roots say the forest sheltered children through the harshest winds.",
            "Ferrymen scratched the real tide onto a post; the shipping manifest omitted it.",
            "The miners' signed shutdown roll orders evacuation of the injured before restarting the crystal engines.",
            "The evacuation log names refugees and guards who opened the same side gate together.",
            "Mistport's safer course came from sailors' testimony, not a stale customs ledger.",
            "A stone by an unregistered well preserves children's water shares, later buried by the dunes.",
            "Each warming hut's fire is traced in a highland notebook. Bells should lead people home."
        };
        public bool FarLogFound(int room)
        {return room>=0&&room<8&&Has("far_log_"+room);}
        public bool FarDialReady(int room)
        {return room>=0&&room<8&&Has("far_dial_"+room);}
        public bool FarShelterBuilt(int room)
        {return room>=0&&room<8&&Has("far_shelter_"+room);}
        public int FarShelterCount
        {get {int count=0;for(int i=0;i<8;i++)if(FarShelterBuilt(i))count++;return count;}}
        private bool TalkFarStation(WorldObject o)
        {
            if(o.kind=="faratlasdesk" || o.kind=="farrefugerecord")
            {
                if(save.room!=9||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
                if(o.kind=="farrefugerecord")
                {
                    bool first=!Has("far_refuge_guest_record");
                    if(first){Flag("far_refuge_guest_record");AddXp(10);sound="reward";}
                    Dialog(o.name,o.en,
                        "这份旅店住客名单记录了未被计入官方救援数字的老人、矿工和孩子。新的避险棚应为所有走到这里的人敞开。"+
                        (first?"\\n\\n首次记录：经验 +10。":""),
                        "This guest list remembers elders, miners and children missing from official relief totals. Every new refuge must remain open to people who actually reach it."+
                        (first?"\\n\\nFirst reading: +10 XP.":""),
                        C("记下姓名","Remember their names","close"));
                    return true;
                }
                bool ready=FarShelterCount==8,complete=Has("far_refuge_atlas");
                Dialog(o.name,o.en,
                    "炉火旅店的新制图室收集八个地区的气象记录与避险棚实地图。只有全部实地修复后，才能把开放的公共路线印给每一个旅人。"+
                    "\\n\\n已建成公共避险棚："+FarShelterCount+"/8。"+
                    (complete?"\\n\\n总图已经公开，旧名字不会再次消失。":ready?
                        "\\n\\n真实站点全部验证完毕，可以签发公共总图。":"\\n\\n继续前往未完成的远地气象站。"),
                    "The Hearth Inn's new cartography wing collects weather logs and public refuges from all eight regions. Only verified built shelters can enter the open atlas."+
                    "\\n\\nRebuilt shelters: "+FarShelterCount+"/8."+
                    (complete?"\\n\\nThe public atlas is already filed.":ready?
                        "\\n\\nAll stations verified. The civic chart may now be published.":"\\n\\nExplore the remaining weather shelters."),
                    C(complete?"重读公共总图":"签发公开避险总图",
                        complete?"Reread the atlas":"Publish public refuge atlas","far_publish"),
                    C("继续巡路","Keep exploring","close"));
                return true;
            }
            if(o.kind!="farledger"&&o.kind!="farcalibrate"&&o.kind!="farshelter")return false;
            int room=save.room;
            if(room<0||room>7||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            if(o.kind=="farledger")
            {
                bool first=!FarLogFound(room);
                if(first){Flag("far_log_"+room);AddXp(10);sound="reward";}
                Dialog(o.name,o.en,FarWitnessZh[room]+
                    "\n\n此地原来的风向校准符号是「"+DialSignsZh[FarDialAnswers[room]]+
                    "」。去南侧真实校准盘输入它，才可能找到共同避险棚。"+
                    (first?"\n\n首次实地记录：经验 +10。":""),
                    FarWitnessEn[room]+
                    "\n\nThe local calibration glyph is "+DialSignsEn[FarDialAnswers[room]]+
                    ". Enter it at the real southern dial before opening the public shelter."+
                    (first?"\n\nFirst field record: +10 XP.":""),
                    C("记下读数","Record the reading","close"));
                return true;
            }
            if(o.kind=="farcalibrate")
            {
                if(!FarLogFound(room))
                {
                    Dialog(o.name,o.en,"校验盘被风雨洗掉了说明。先沿远东岔道找到原来的气象手记。",
                        "The field dial has lost its instructions. First read the original log on the remote eastern trail.",
                        C("返回调查","Search for the log","close"));return true;
                }
                if(FarDialReady(room))
                {
                    Dialog(o.name,o.en,"气象盘已经校准，沿南部环路前往最后的公共避险棚。",
                        "The weather dial is calibrated. Follow the southern circuit to the public shelter.",
                        C("继续行路","Continue","close"));return true;
                }
                Dialog(o.name,o.en,"手记上的三种古老符号分别表示琥珀、潮蓝与苔绿。选择与原始记录一致的一项。",
                    "The old log describes three glyphs: Amber, Tideblue and Mossgreen. Match its field reading.",
                    C("琥珀刻痕","Amber glyph","far_dial_0"),
                    C("潮蓝刻痕","Tideblue glyph","far_dial_1"),
                    C("苔绿刻痕","Mossgreen glyph","far_dial_2"),
                    C("稍后再来","Return later","close"));
                return true;
            }
            if(!FarDialReady(room))
            {
                Dialog(o.name,o.en,"避险棚的炉火尚未恢复。先找到天气记录，并在南侧校验盘恢复信号。",
                    "This shelter is still unlit. Recover the survey and calibrate the southern weather dial first.",
                    C("继续查找","Continue the survey","close"));return true;
            }
            if(!FarShelterBuilt(room))
            {
                Dialog(o.name,o.en,"这里有可供普通旅人歇脚的旧棚，修复需要木材 ×1 和零件 ×1。"+
                    "\n\n修复后可消耗木材再次生火，治疗、补充耐力并减轻寒冷。",
                    "This unfinished civilian refuge needs one timber and one spare part."+
                    "\n\nOnce built, a timber-fueled rest restores health, energy and warmth.",
                    C("修复避险棚 · 木材1 零件1","Restore refuge · 1 timber 1 part","far_build"),
                    C("以后再来","Return later","close"));
                return true;
            }
            Dialog(o.name,o.en,FarWitnessZh[room]+
                "\n\n公共避险棚已经建好。远行者可消耗木材 ×1 生火休息，恢复生命 35、全部耐力、减轻寒冷 25。",
                FarWitnessEn[room]+
                "\n\nThe shared shelter is open. Spend 1 timber to rest: +35 HP, full stamina and -25 cold.",
                C("添柴休息 · 木材 ×1","Rest · 1 timber","far_rest"),
                C("继续探索","Keep exploring","close"));
            return true;
        }
        private bool ChooseFarStation(string action)
        {
            if(action=="far_publish")
            {
                if(save.room!=9||Dying||practicing||Has("far_refuge_atlas"))return true;
                WorldObject desk=null;
                foreach(WorldObject item in Map.objects)
                    if(item.kind=="faratlasdesk"){desk=item;break;}
                if(desk==null||(Position-new Point(desk.x,desk.y)).Length>2.2f)return true;
                if(FarShelterCount!=8)
                {
                    Notify("仍有避险棚没有被实地修复。","Some remote shelters have yet to be rebuilt.");
                    return true;
                }
                Flag("far_refuge_atlas");save.coins+=80;save.shards+=3;
                AddXp(50);revision++;sound="reward";
                RecordMoment("八处公共避险棚的路线已经在炉火旅店发布，连没有登记的住客也能使用。",
                    "The shared weather atlas now charts eight physical civilian refuges, including for unregistered travelers.");
                Notify("公共避险总图发布：硬币 +80、碎片 +3、经验 +50。",
                    "Public refuge atlas published: +80 coins, +3 shards, +50 XP.");
                return true;
            }
            if(!action.StartsWith("far_dial_")&&action!="far_build"&&action!="far_rest")
                return false;
            int room=save.room;
            if(room<0||room>7||Dying||practicing)return true;
            string required=action.StartsWith("far_dial_")?"farcalibrate":"farshelter";
            WorldObject station=null;
            foreach(WorldObject o in Map.objects)
                if(o.kind==required){station=o;break;}
            if(station==null||(Position-new Point(station.x,station.y)).Length>2.2f)return true;
            if(action.StartsWith("far_dial_"))
            {
                if(!FarLogFound(room)||FarDialReady(room))return true;
                int choice=-1;
                if(!int.TryParse(action.Substring("far_dial_".Length),out choice))return true;
                if(choice!=FarDialAnswers[room])
                {
                    Notify("刻度不匹配；再次查阅原始记录。","The glyph does not match the original field log.");
                    return true;
                }
                Flag("far_dial_"+room);AddXp(15);revision++;sound="reward";
                Notify("气象盘校准完成，新的避险路段已经确认！",
                    "Weather dial calibrated. The last refuge route is now verified.");
                return true;
            }
            if(!FarDialReady(room))return true;
            if(!SafeTravel)
            {
                Notify("敌人正在追击，无法安全修复或生火。","Hostiles are too close to safely build or rest.");
                return true;
            }
            if(action=="far_build")
            {
                if(FarShelterBuilt(room)||!Spend(1,0,0,0,1))return true;
                Flag("far_shelter_"+room);save.coins+=22;save.shards++;
                AddXp(35);revision++;sound="reward";
                RecordMoment("居民共同修复第 "+(room+1)+" 处远端避险棚，留下正确的气象符号与不遗漏普通人的道路。",
                    "The region's remote public refuge is rebuilt with a verified weather sign and an open civilian path.");
                if(FarShelterCount==8&&!Has("far_all_restored"))
                {Flag("far_all_restored");save.skillPoints++;}
                Notify("避险棚开放：硬币 +22、碎片 +1、经验 +35。",
                    "Refuge restored: +22 coins, +1 shard, +35 XP.");
                return true;
            }
            if(!FarShelterBuilt(room)||!Spend(1,0,0,0,0))return true;
            save.health=Clamp(save.health+35,0,save.maxHealth);
            save.energy=100;save.cold=Math.Max(0,save.cold-25);
            revision++;sound="reward";
            Notify("暖棚休息：生命 +35，耐力全满，寒冷 -25。",
                "Refuge rest: +35 health, full energy and -25 cold.");
            return true;
        }
    }
}
