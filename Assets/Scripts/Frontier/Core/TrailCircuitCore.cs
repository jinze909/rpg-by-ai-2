using System;
namespace Starfall
{
    // Walkable physical third-ring landmarks. No new SaveData fields: survey
    // stages are permanent string flags, including on legacy migrated slots.
    public sealed partial class GameCore
    {
        public int TrailCircuitProgress(int room)
        {
            if(room<0||room>7)return 0;
            if(Has("trail_circuit_"+room+"_3"))return 3;
            if(Has("trail_circuit_"+room+"_2"))return 2;
            if(Has("trail_circuit_"+room+"_1"))return 1;
            return 0;
        }
        public bool TrailCircuitComplete(int room)
        { return TrailCircuitProgress(room)==3; }
        public int TrailCircuitCount
        {
            get { int total=0;for(int i=0;i<8;i++)if(TrailCircuitComplete(i))total++;return total; }
        }
        private bool TalkTrailCircuit(WorldObject o)
        {
            if(o.kind!="trailcircuit")return false;
            int room=save.room;
            if(room<0||room>7||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            int stage=0;
            for(int i=1;i<=3;i++)if(o.id=="trailcircuit_"+room+"_"+i)stage=i;
            if(stage==0)return true;
            int progress=TrailCircuitProgress(room);
            string[] routeZh={"渠外归乡路","林地共生路","沉桥渡口路","矿工安全路",
                "堡垒公共外环","雾港回航路","沙海补水路","霜钟归火路"};
            string[] routeEn={"Canal Homeward Route","Living Thornwood Trail","Open River Crossing",
                "Workers' Safe Line","Citadel Civilian Ring","Mistport Homebound Way",
                "Glasswind Water Line","Frostchime Hearth Trail"};
            if(progress>=stage)
            {
                Dialog(o.name,o.en,"这里已经记下"+routeZh[room]+"的第 "+stage+" 个真实坐标。下一次巡路仍能重读记录。",
                    "Bearing "+stage+" of "+routeEn[room]+" is already recorded. The marker remains readable.",
                    C("继续行路","Continue","close"));
                return true;
            }
            if(stage!=progress+1)
            {
                Dialog(o.name,o.en,"路碑上的信息必须结合前面的记录才能辨认。先到上一处实地路标；已完成 "+progress+"/3。",
                    "This marker relies on the earlier field bearing. Find the previous physical stop first ("+progress+"/3).",
                    C("寻找上一处路标","Find the earlier bearing","close"));
                return true;
            }
            Flag("trail_circuit_"+room+"_"+stage);
            revision++;sound="reward";
            bool done=stage==3;
            if(done)
            {
                save.coins+=24;save.shards+=2;AddXp(35);
                if(TrailCircuitCount==8&&!Has("trail_circuits_all"))
                { Flag("trail_circuits_all");save.skillPoints++;revision++; }
                RecordMoment("巡路队第 "+room+" 地的三处原始地标都经亲自抵达，开放区域的行路补给开始持续生效。",
                    "Three original roadwarden bearings were walked and verified for "+routeEn[room]+
                    "; the annex now supports sustained civilian stamina recovery.");
            }
            else AddXp(8);
            Dialog(o.name,o.en,"你亲自核对"+routeZh[room]+"的第 "+stage+"/3 个路标。"+
                (done?"\n\n完整线路确认：硬币 +24、碎片 +2、经验 +35。本区域的新探索地形额外恢复耐力 +2/秒。":
                "\n\n还有下一段路需要抵达。经验 +8。"),
                "Personally surveyed bearing "+stage+"/3 of "+routeEn[room]+"."+
                (done?"\n\nFull circuit complete: +24 coins, +2 shards and +35 XP. New paths in this region now restore an extra 2 stamina per second.":
                "\n\nContinue along the route. +8 XP."),
                C("记下新坐标","Record the new bearing","close"));
            return true;
        }
    }
}
