using System;
namespace Starfall
{
    // The only persistent cargo state is successful delivery in SaveData v2
    // flags. Active packages are temporary, and cannot survive a map warp,
    // death, timeout, story reset, or slot reload.
    public sealed partial class GameCore
    {
        private int courierRoom=-1;
        private Point courierLast;
        private bool courierCrossed;
        private float courierWalked,courierElapsed;
        public bool FarCourierActive {get{return courierRoom==save.room&&!Dying;}}
        public float FarCourierTimeLeft {get{return FarCourierActive?Math.Max(0,95-courierElapsed):0;}}
        public bool FarCourierCrossed {get{return FarCourierActive&&courierCrossed;}}
        public bool FarCourierDelivered(int room)
        {return room>=0&&room<8&&Has("far_courier_"+room);}
        public int FarCourierDeliveredCount
        {get{int n=0;for(int i=0;i<8;i++)if(FarCourierDelivered(i))n++;return n;}}
        private void ResetFarCourier()
        {courierRoom=-1;courierWalked=courierElapsed=0;courierCrossed=false;courierLast=new Point();}
        private void TickFarCourier(float dt)
        {
            if(courierRoom<0)return;
            if(courierRoom!=save.room||Dying||practicing)
            {ResetFarCourier();return;}
            Point now=Position;
            float step=(now-courierLast).Length;
            if(!Finite(now.x)||!Finite(now.y)||step>2.4f)
            {
                ResetFarCourier();
                Notify("救援包在中途丢失，请返回桥头重新领取。",
                    "The relief parcel was lost in transit. Return to the bridge.");
                return;
            }
            courierElapsed+=Math.Max(0,dt);courierWalked+=step;
            if(FarBridgeOpen(save.room)&&now.x>=110.25f&&now.x<=112.75f&&
               now.y>=52.2f&&now.y<=53.9f&&now.y>courierLast.y+.01f)
                courierCrossed=true;
            courierLast=now;
            if(courierElapsed>=95)
            {
                ResetFarCourier();
                Notify("物资超时，请回桥头重新组织运送。",
                    "The supply run expired. Recollect the parcel at the bridge.");
            }
        }
        private bool TalkFarCourier(WorldObject o)
        {
            bool bridge=o.kind=="farbridge",shelter=o.kind=="farshelter";
            if(!bridge&&!shelter)return false;
            int room=save.room;
            if(room<0||room>=8||(Position-new Point(o.x,o.y)).Length>2.2f||
               !FarBridgeOpen(room)||!FarShelterBuilt(room)||FarCourierDelivered(room))return false;
            if(bridge)
            {
                Dialog(o.name,o.en,FarCourierActive?
                    "救援包正在你身上。沿修好的桥面向南步行，并到远端避险棚亲手交货。你也可以把它留在桥头。":
                    "桥已经重新开放，可以真正向远端避险棚运送物资。领取后95秒内步行穿过新桥；负重会稍微减慢速度，不能使用传送跳过。",
                    FarCourierActive?
                    "Carry your relief parcel across the restored bridge to the outer refuge, or leave it safely here.":
                    "The reopened bridge can finally deliver aid. Carry a real parcel across it to the public refuge within 95 seconds. Weight slows walking and fast travel cannot substitute.",
                    C(FarCourierActive?"放回物资":"背起救援包",
                        FarCourierActive?"Set parcel down":"Carry relief parcel",
                        FarCourierActive?"far_courier_cancel":"far_courier_start"),
                    C("稍后再来","Later","close"));
                return true;
            }
            if(!FarCourierActive)return false;
            bool ready=courierCrossed&&courierWalked>=17&&
                (Position-courierLast).Length<.65f&&courierElapsed<95;
            Dialog(o.name,o.en,ready?
                "封条完整！这份物资确实经由居民修好的桥面送达，而不是只在交接簿上写了一个数字。":
                "救援包仍在，但运送尚未走过桥面。真正从北向南走过新桥，再到这里交付。",
                ready?
                "The seal is intact. Your parcel truly crossed the civilian bridge instead of arriving only on a paper manifest.":
                "The parcel has not yet traveled over the new span. Walk south across the real bridge before delivering.",
                C(ready?"亲手交接救援包":"查看运送条件",
                    ready?"Hand over supplies":"Review delivery route","far_courier_deliver"),
                C("继续步行","Keep walking","close"));
            return true;
        }
        private bool ChooseFarCourier(string action)
        {
            if(action!="far_courier_start"&&action!="far_courier_cancel"&&action!="far_courier_deliver")
                return false;
            int room=save.room;
            if(room<0||room>=8||Dying||practicing)return true;
            WorldObject bridge=null,shelter=null;
            foreach(WorldObject obj in Map.objects)
            {if(obj.kind=="farbridge")bridge=obj;else if(obj.kind=="farshelter")shelter=obj;}
            if(bridge==null||shelter==null||!FarBridgeOpen(room)||
                !FarShelterBuilt(room)||FarCourierDelivered(room))return true;
            if(action=="far_courier_cancel")
            {
                if(FarCourierActive&&(Position-new Point(bridge.x,bridge.y)).Length<=2.2f)
                {ResetFarCourier();Notify("救援包留在桥头。","Parcel returned to the bridge.");}
                return true;
            }
            if(action=="far_courier_start")
            {
                if(FarCourierActive||(Position-new Point(bridge.x,bridge.y)).Length>2.2f)return true;
                if(!SafeTravel)
                {Notify("先清除桥边敌人。","Clear nearby hostiles first.");return true;}
                courierRoom=room;courierLast=Position;
                courierWalked=courierElapsed=0;courierCrossed=false;
                revision++;sound="quest";
                Notify("救援包已背上：95秒内步行过桥并送到避险棚！",
                    "Parcel loaded: cross the bridge on foot and reach the shelter in 95 seconds!");
                return true;
            }
            if(!FarCourierActive||
                (Position-new Point(shelter.x,shelter.y)).Length>2.2f||
                (Position-courierLast).Length>=.65f||
                !courierCrossed||courierWalked<17||courierElapsed>=95)return true;
            if(!SafeTravel)
            {Notify("避险棚附近仍有敌人。","Hostiles threaten the shelter.");return true;}
            ResetFarCourier();
            Flag("far_courier_"+room);save.coins+=25;save.shards++;
            AddXp(38);revision++;sound="reward";
            if(FarCourierDeliveredCount==8&&!Has("far_courier_all"))
            {Flag("far_courier_all");save.skillPoints++;revision++;}
            RecordMoment("第 "+(room+1)+" 份居民救援包通过真正的山脊步行路线到达公共避险棚。",
                "A civilian parcel reached a distant public refuge by the actual rebuilt footbridge.");
            Notify("救援包已交接：硬币25、碎片1、经验38！",
                "Parcel delivered: +25 coins, +1 shard, +38 XP!");
            return true;
        }
    }
}
