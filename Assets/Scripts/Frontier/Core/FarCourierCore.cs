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
        private bool courierCrossed,courierStamped;
        private float courierWalked,courierElapsed;
        private float courierIntegrity;
        private bool courierPatched,courierIntegrityWarning;
        public float FarCourierIntegrity {get{return FarCourierActive?courierIntegrity:0;}}
        public bool FarCourierPatched {get{return FarCourierActive&&courierPatched;}}
        // Called exclusively from real post-armor player damage. Invulnerable
        // dodges, simulated warnings and environmental exposure never tear cargo.
        private void DamageFarCourierCargo(float actualHealthDamage,string source)
        {
            if(!FarCourierActive||source=="environment"||actualHealthDamage<=0)return;
            courierIntegrity=Math.Max(0,courierIntegrity-
                Math.Max(6,Math.Min(24,actualHealthDamage*.65f)));
            if(courierIntegrity<=0)
            {
                ResetFarCourier();revision++;
                Notify("救援包在真正的战斗中损毁了。请回到桥头重新领取。",
                    "The relief parcel was destroyed in combat. Collect a fresh one at the bridge.");
            }
            else if(courierIntegrity<=35&&!courierIntegrityWarning)
            {
                courierIntegrityWarning=true;
                Notify("救援包严重受损！到居民检查站使用一枚零件加固，或谨慎完成交货。",
                    "Parcel badly damaged! Use a spare part at the checkpoint to reinforce it, or deliver carefully.");
            }
        }
        public bool FarCourierActive {get{return courierRoom==save.room&&!Dying;}}
        public float FarCourierTimeLeft {get{return FarCourierActive?Math.Max(0,95-courierElapsed):0;}}
        public bool FarCourierCrossed {get{return FarCourierActive&&courierCrossed;}}
        public bool FarCourierStamped {get{return FarCourierActive&&courierStamped;}}
        public bool FarCourierDelivered(int room)
        {return room>=0&&room<8&&Has("far_courier_"+room);}
        public int FarCourierDeliveredCount
        {get{int n=0;for(int i=0;i<8;i++)if(FarCourierDelivered(i))n++;return n;}}
        private void ResetFarCourier()
        {courierRoom=-1;courierWalked=courierElapsed=courierIntegrity=0;courierCrossed=courierStamped=courierPatched=courierIntegrityWarning=false;courierLast=new Point();}
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
            bool bridge=o.kind=="farbridge",shelter=o.kind=="farshelter",
                checkpoint=o.kind=="farcheck";
            if(!bridge&&!shelter&&!checkpoint)return false;
            int room=save.room;
            if(room<0||room>=8||(Position-new Point(o.x,o.y)).Length>2.2f||
               !FarBridgeOpen(room)||!FarShelterBuilt(room)||FarCourierDelivered(room))return false;
            if(checkpoint)
            {
                bool eligible=FarCourierActive&&courierCrossed&&!courierStamped;
                bool patch=FarCourierActive&&courierCrossed&&
                    courierIntegrity<99&&!courierPatched;
                string noticeZh=courierStamped?
                    "居民已经核验救援包封条，可继续送往避险棚。":
                    eligible?"你真实走过了新桥。请让居民查验救援包封条。":
                    "这里核对亲自携带过桥的救援包，单凭交接表不能算送达。";
                string noticeEn=courierStamped?"This parcel seal is verified. Continue to the refuge.":
                    eligible?"You crossed the rebuilt bridge with real supplies. Ask locals to inspect the parcel.":
                    "This checkpoint verifies only physical bridge deliveries, not paper manifests.";
                if(FarCourierActive)
                {
                    noticeZh+="\\n\\n货物完好度："+(int)Math.Ceiling(courierIntegrity)+
                        "%。敌人真正命中会损坏货物；闪避不会。"+
                        (patch?"可使用一枚零件加固 +35（每趟限一次）。":"");
                    noticeEn+="\\n\\nCargo integrity: "+(int)Math.Ceiling(courierIntegrity)+
                        "%. Real enemy hits damage the parcel; dodges do not."+
                        (patch?" One spare part reinforces +35 once per trip.":"");
                }
                Choice seal=C(eligible?"核验封条":"查看记录",
                    eligible?"Inspect cargo seal":"Read the log",
                    eligible?"far_courier_stamp":"close");
                if(patch)
                    Dialog(o.name,o.en,noticeZh,noticeEn,seal,
                        C("加固货物 · 零件1","Reinforce · 1 spare part","far_courier_patch"),
                        C("继续行路","Keep walking","close"));
                else Dialog(o.name,o.en,noticeZh,noticeEn,seal,
                    C("继续行路","Keep walking","close"));
                return true;
            }
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
            bool ready=courierCrossed&&courierStamped&&courierWalked>=17&&
                (Position-courierLast).Length<.65f&&courierElapsed<95;
            Dialog(o.name,o.en,ready?
                "封条完整！这份物资确实经由居民修好的桥面送达，而不是只在交接簿上写了一个数字。":
                "救援包仍在，但需要从北向南经过新桥，并在中途居民检查台核验封条。",
                ready?
                "The seal is intact. Your parcel truly crossed the civilian bridge instead of arriving only on a paper manifest.":
                "You must cross the real bridge on foot AND obtain a seal from the community checkpoint.",
                C(ready?"亲手交接救援包":"查看运送条件",
                    ready?"Hand over supplies":"Review delivery route","far_courier_deliver"),
                C("继续步行","Keep walking","close"));
            return true;
        }
        private bool ChooseFarCourier(string action)
        {
            if(action!="far_courier_start"&&action!="far_courier_cancel"&&
                action!="far_courier_stamp"&&action!="far_courier_patch"&&
                action!="far_courier_deliver")
                return false;
            int room=save.room;
            if(room<0||room>=8||Dying||practicing)return true;
            WorldObject bridge=null,shelter=null,checkpoint=null;
            foreach(WorldObject obj in Map.objects)
            {
                if(obj.kind=="farbridge")bridge=obj;
                else if(obj.kind=="farshelter")shelter=obj;
                else if(obj.kind=="farcheck")checkpoint=obj;
            }
            if(bridge==null||shelter==null||checkpoint==null||!FarBridgeOpen(room)||
                !FarShelterBuilt(room)||FarCourierDelivered(room))return true;
            if(action=="far_courier_patch")
            {
                if(!FarCourierActive||!courierCrossed||courierPatched||
                    courierIntegrity>=99||
                    (Position-new Point(checkpoint.x,checkpoint.y)).Length>2.2f)return true;
                if(save.inventory[4]<=0)
                {
                    Notify("需要一枚备用零件加固救援包。",
                        "A spare part is required to reinforce the parcel.");return true;
                }
                save.inventory[4]--;courierPatched=true;
                courierIntegrity=Math.Min(100,courierIntegrity+35);
                sound="reward";revision++;
                Notify("货物已加固，完好度恢复 35；本趟不能再次加固。",
                    "Parcel reinforced by 35 integrity; no second repair this trip.");
                return true;
            }
            if(action=="far_courier_stamp")
            {
                if(!FarCourierActive||!courierCrossed||courierStamped||
                    (Position-new Point(checkpoint.x,checkpoint.y)).Length>2.2f||
                    (Position-courierLast).Length>.65f)return true;
                courierStamped=true;revision++;sound="quest";
                Notify("封条已由居民核验，继续送往避险棚。",
                    "Parcel seal verified by locals. Continue to the refuge.");
                return true;
            }
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
                courierWalked=courierElapsed=0;
                courierIntegrity=100;courierCrossed=courierStamped=false;
                courierPatched=courierIntegrityWarning=false;
                revision++;sound="quest";
                Notify("救援包已背上：95秒内步行过桥并送到避险棚！",
                    "Parcel loaded: cross the bridge on foot and reach the shelter in 95 seconds!");
                return true;
            }
            if(!FarCourierActive||
                (Position-new Point(shelter.x,shelter.y)).Length>2.2f||
                (Position-courierLast).Length>=.65f||
                !courierCrossed||!courierStamped||courierWalked<17||courierElapsed>=95)return true;
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
