using System;
namespace Starfall
{
    // Public highland wayfinding unlocked by first visiting both actual sites.
    // Old saves persist discoveries with preexisting v2 flag strings; neither
    // a cloned hero nor new coordinates in the original story are needed.
    public sealed partial class GameCore
    {
        public bool RidgeSeen(int room,int side)
        { return room>=0&&room<8&&side>=0&&side<=1&&Has("ridge_seen_"+room+"_"+side); }
        public bool RidgeLinked(int room)
        { return RidgeSeen(room,0)&&RidgeSeen(room,1); }
        public int RidgeLinkedCount
        {
            get { int n=0;for(int i=0;i<8;i++)if(RidgeLinked(i))n++;return n; }
        }
        private bool TalkRidge(WorldObject o)
        {
            if(o.kind!="ridgemarker")return false;
            int room=save.room;
            if(room<0||room>7||(Position-new Point(o.x,o.y)).Length>2.2f)return true;
            int side=o.id=="ridge_route_"+room+"_0"?0:
                o.id=="ridge_route_"+room+"_1"?1:-1;
            if(side<0)return true;
            bool first=!RidgeSeen(room,side);
            bool linkedBefore=RidgeLinked(room);
            if(first)
            {
                Flag("ridge_seen_"+room+"_"+side);AddXp(9);
                revision++;sound="reward";
            }
            if(!linkedBefore&&RidgeLinked(room))
            {
                save.coins+=20;save.shards++;
                AddXp(24);revision++;sound="reward";
                RecordMoment("第五圈的南北两座旧标记终于被共同标记，居民可以安全地在两条偏远巡路间往返。",
                    "Both remote ridge markers were surveyed by foot, enabling a civilian route across the outer wilderness.");
                if(RidgeLinkedCount==8&&!Has("ridge_all_linked"))
                {
                    Flag("ridge_all_linked");save.skillPoints++;revision++;
                }
            }
            string[] zh={
                "水位变化终于有了远离旧城墙的对照记录。",
                "树梢上的风铃能替误入荆棘的孩子指路。",
                "旧桥上游的水尺记录了那些未登记的民用渡船。",
                "废矿终点的铁轨被改作真正的疏散步道。",
                "外墙的应急路段由居民而不是军队维护。",
                "旧灯塔与岸边栈道终于能互相确认航向。",
                "两口无人登记的井现在连成可共用的取水路。",
                "高处的风铃指向背风暖棚，不再只报告警戒。"
            };
            string[] en={
                "Flood gauges now have a civilian reference beyond the old walls.",
                "A canopy chime guides children away from the thornwood.",
                "An upstream gauge remembers ferries absent from official manifests.",
                "Abandoned mine rails have become an evacuation footpath.",
                "Residents maintain these emergency steps, not the military.",
                "The old lighthouse and coastal walk can finally share bearings.",
                "Two previously unregistered wells now share one public path.",
                "A high chime guides travelers toward warmth instead of another alarm."
            };
            if(RidgeLinked(room))
            {
                Dialog(o.name,o.en,zh[room]+
                    "\n\n两座旧标记已经由你亲自到访。现在可通过安全的高处通道前往另一端，消耗 12 耐力。"+
                    (first?"\n\n这处地标首次记录：经验 +9。线路连通：硬币 +20、碎片 +1、经验 +24。":""),
                    en[room]+
                    "\n\nBoth markers have been visited on foot. Use the now-safe ridge connection to reach the opposite end for 12 energy."+
                    (first?"\n\nFirst visit: +9 XP. Route linked: +20 coins, +1 shard, +24 XP.":""),
                    C("穿越巡路 · 耐力 12","Cross route · 12 energy","ridge_travel"),
                    C("继续步行","Keep walking","close"));
                return true;
            }
            Dialog(o.name,o.en,zh[room]+
                "\n\n旧地标实地记录："+(RidgeSeen(room,0)?1:0)+"/1 北侧，"+
                (RidgeSeen(room,1)?1:0)+"/1 南侧。找到另一端后，才能恢复两处之间的安全巡路。"+
                (first?"\n\n首次到访：经验 +9。":""),
                en[room]+
                "\n\nRidge survey: "+(RidgeSeen(room,0)?1:0)+"/1 east, "+
                (RidgeSeen(room,1)?1:0)+"/1 south. Discover the other endpoint before unlocking safe travel."+
                (first?"\n\nFirst arrival: +9 XP.":""),
                C("记录地标","Record the landmark","close"));
            return true;
        }
        private bool ChooseRidge(string action)
        {
            if(action!="ridge_travel")return false;
            int room=save.room;
            if(room<0||room>7||!RidgeLinked(room)||Dying||practicing)return true;
            WorldObject start=null,end=null;
            foreach(WorldObject o in Map.objects)
            {
                if(o.kind!="ridgemarker")continue;
                if((Position-new Point(o.x,o.y)).Length<=2.2f)start=o;
            }
            if(start==null)return true;
            string targetId=start.id=="ridge_route_"+room+"_0"?
                "ridge_route_"+room+"_1":"ridge_route_"+room+"_0";
            foreach(WorldObject o in Map.objects)if(o.id==targetId){end=o;break;}
            if(end==null)return true;
            if(!SafeTravel)
            {
                Notify("战斗中不能使用巡路捷径。","The ridge crossing is unavailable during combat.");
                return true;
            }
            if(save.energy<12)
            {
                Notify("需要 12 点耐力才能走完这条新通道。","The route requires 12 energy.");
                return true;
            }
            Point destination=new Point(end.x,end.y);
            if(!CanStand(destination))return true;
            foreach(Enemy enemy in enemies)
                if(enemy.hp>0&&(enemy.Position-destination).Length<5f)
                {
                    Notify("另一端有敌人守候，暂时不能安全通过。",
                        "Hostile creatures block the far end of this crossing.");
                    return true;
                }
            save.x=end.x;save.y=end.y;save.energy-=12;
            revision++;sound="reward";
            Notify("安全穿越已经连通的偏远高地路。","Crossed the restored ridge way.");
            return true;
        }
    }
}
