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
        private bool courierCrossed,courierStamped,courierBridgeEntered;
        private float courierWalked,courierElapsed;
        private float courierIntegrity;
        private bool courierPatched,courierIntegrityWarning;
        private bool courierGuardSpent,courierRaided;
        // Earned only when BOTH actual bridge pursuers die through GameCore.Hit.
        // Never persisted until the original parcel reaches the far refuge.
        private int courierConfirmedRaiderKills;
        public bool FarCourierSecuredInFlight
        {get{return FarCourierActive&&courierRaided&&courierConfirmedRaiderKills==3;}}
        public bool FarCourierSecured(int room)
        {return room>=0&&room<8&&Has("far_courier_secured_"+room);}
        private void OnFarCourierEnemyDefeated(Enemy enemy)
        {
            if(!FarCourierActive||!IsFarCourierRaider(enemy))return;
            int index=enemy.id-(90100+save.room*2);
            if(index>=0&&index<2)courierConfirmedRaiderKills|=1<<index;
        }
        // Mission-owned IDs use a reserved range, never ordinary spawn IDs.
        private bool IsFarCourierRaider(Enemy foe)
        {return foe!=null&&foe.id>=90100&&foe.id<90116;}
        public int FarCourierRaidersAlive
        {
            get
            {
                if(!FarCourierActive)return 0;
                int n=0;foreach(Enemy foe in enemies)if(IsFarCourierRaider(foe))n++;
                return n;
            }
        }
        // Prefer the two original authored ambush locations. If a map's
        // collision has changed, find a reachable nearby tile rather than
        // silently deleting an otherwise playable bridge encounter.
        private bool FindCourierRaiderPosition(Point authored,Point other,
            bool second,out Point chosen)
        {
            for(int radius=0;radius<=5;radius++)
                for(int dy=-radius;dy<=radius;dy++)
                    for(int dx=-radius;dx<=radius;dx++)
                    {
                        if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                        Point p=authored+new Point(dx,dy);
                        if(!CanStand(p)||(p-Position).Length<2.2f||
                            // The optional living companion already occupies
                            // world space beside the courier. Never materialize
                            // a full-sized hostile in their Sprite/attack foot.
                            (save.companion>=0 &&
                             (p-companionPosition).Length<1.25f)||
                            (second&&(p-other).Length<2.6f)||
                            enemies.Exists(e=>e.hp>0 &&
                                (e.Position-p).Length<1.8f))continue;
                        // Even the exact authored tile (radius zero) must be
                        // reachable by a living pursuer. Compass-style
                        // NavigationPath may legally stop on the near side of
                        // sealed cover; that is not a valid physical ambush.
                        // Require a walkable same-side finish from the actual
                        // hero location. Dynamically opened bridges and gates
                        // are evaluated afresh on each attempt.
                        if(NavigationPathFrom(Position,p,true).Count==0)continue;
                        chosen=p;return true;
                    }
            chosen=new Point();return false;
        }
        private void SpawnFarCourierRaiders()
        {
            if(courierRaided||!FarCourierActive)return;
            // Never half-deploy a wave: choose BOTH reachable placements
            // before mutating the enemy roster or its owned telegraphs.
            // Older Unity-compatible C# compilers require explicit assignment
            // even when the second out parameter is guarded by short-circuiting.
            Point melee=new Point(),archer=new Point();
            bool ready=FindCourierRaiderPosition(
                new Point(111.5f,57.5f),new Point(),false,out melee);
            if(ready)ready=FindCourierRaiderPosition(
                new Point(114.5f,59.5f),melee,true,out archer);
            courierRaided=true;
            if(!ready)
            {
                Notify("桥南巡逻位置被阻断。救援包仍可沿安全道路绕行送达。",
                    "Raider approach is blocked. You can still carry the parcel through the safe crossing.");
                return;
            }
            int id=90100+save.room*2;
            enemies.Add(CreatePlacedEnemy(id,0,melee,36+save.room*5));
            enemies.Add(CreatePlacedEnemy(id+1,1,archer,31+save.room*5));
            effects.Add(new Effect("danger",melee,.6f){radius=.55f,courierOwned=true});
            effects.Add(new Effect("danger",archer,.6f){radius=.55f,courierOwned=true});
            revision++;sound="windup";
            Notify("桥南两名劫匪正在拦截救援包！可战斗或迂回，真正受击会损坏货物。",
                "Two raiders intercept the parcel south of the bridge! Fight or evade; real hits damage cargo.");
        }
        private float courierGuardLeft;
        public float FarCourierGuardLeft {get{return FarCourierActive?courierGuardLeft:0;}}
        public float FarCourierIntegrity {get{return FarCourierActive?courierIntegrity:0;}}
        public bool FarCourierPatched {get{return FarCourierActive&&courierPatched;}}
        // Called exclusively from real post-armor player damage. Invulnerable
        // dodges, simulated warnings and environmental exposure never tear cargo.
        private void DamageFarCourierCargo(float actualHealthDamage,string source)
        {
            if(!FarCourierActive||source=="environment"||actualHealthDamage<=0)return;
            float shock=Math.Max(12,Math.Min(38,actualHealthDamage*1.2f));
            // A real energy-powered convoy shield protects the *parcel*, not
            // the hero's health. Real successful hits still hurt the carrier.
            if(courierGuardLeft>0)shock*=.45f;
            courierIntegrity=Math.Max(0,courierIntegrity-shock);
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
        public bool FarCourierOnBridge
        {get{return FarCourierActive&&courierBridgeEntered&&!courierCrossed;}}
        public bool FarCourierStamped {get{return FarCourierActive&&courierStamped;}}
        public bool FarCourierDelivered(int room)
        {return room>=0&&room<8&&Has("far_courier_"+room);}
        // A genuinely well-preserved delivery leaves usable medical supplies
        // at THIS community refuge. Stored in existing SaveData v2 flags.
        public bool FarCourierPristine(int room)
        {return room>=0&&room<8&&Has("far_courier_pristine_"+room);}
        public int FarCourierDeliveredCount
        {get{int n=0;for(int i=0;i<8;i++)if(FarCourierDelivered(i))n++;return n;}}
        private void ResetFarCourier()
        {
            // Retire only route-owned hostile actors, arrows and warnings.
            enemies.RemoveAll(IsFarCourierRaider);
            projectiles.RemoveAll(shot=>shot.courierOwned);
            effects.RemoveAll(effect=>effect.courierOwned);
            courierRoom=-1;courierRaided=false;
            courierConfirmedRaiderKills=0;
            courierWalked=courierElapsed=courierIntegrity=courierGuardLeft=0;
            courierCrossed=courierStamped=courierBridgeEntered=
                courierPatched=courierIntegrityWarning=courierGuardSpent=false;
            courierLast=new Point();
        }
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
            courierGuardLeft=Math.Max(0,courierGuardLeft-dt);
            if(!courierCrossed)
            {
                // The repaired bridge occupies y=52..53 in every authored
                // 120x80 region. Touching its northern lip is not a
                // completed crossing: require real continuous footsteps
                // from the north bank to the south bank before crediting
                // the parcel and deploying its physical raiders.
                bool inLane=now.x>=110.25f&&now.x<=112.75f;
                bool headingSouth=now.y>courierLast.y+.01f;
                if(!FarBridgeOpen(save.room)||!inLane)
                    courierBridgeEntered=false;
                else
                {
                    if(!courierBridgeEntered && headingSouth &&
                        courierLast.y<52.0f && now.y>=52.0f &&
                        now.y<=53.9f)
                        courierBridgeEntered=true;
                    // A return to the northern bank cancels the crossing.
                    // Walking north-to-south again earns a fresh chance,
                    // without inventing any transport or teleport distance.
                    if(now.y<51.95f)courierBridgeEntered=false;
                    if(courierBridgeEntered&&headingSouth&&
                        courierLast.y<54.25f&&now.y>=54.25f)
                    {
                        courierCrossed=true;
                        courierBridgeEntered=false;
                        SpawnFarCourierRaiders();
                    }
                }
            }
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
            if(room<0||room>=8||!HasPhysicalInteractionReach(o)||
               !FarBridgeOpen(room)||!FarShelterBuilt(room)||FarCourierDelivered(room))return false;
            if(checkpoint)
            {
                bool eligible=FarCourierActive&&courierCrossed&&!courierStamped;
                bool patch=FarCourierActive&&courierCrossed&&
                    courierIntegrity<99&&!courierPatched;
                bool guard=FarCourierActive&&courierCrossed&&!courierGuardSpent;
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
                        (patch?"可使用一枚零件加固 +35（每趟限一次）。":"")+
                        (guard?"可花费30耐力守护物资12秒（每趟限一次）。":"");
                    noticeEn+="\\n\\nCargo integrity: "+(int)Math.Ceiling(courierIntegrity)+
                        "%. Real enemy hits damage the parcel; dodges do not."+
                        (patch?" One spare part reinforces +35 once per trip.":"")+
                        (guard?" Spend 30 energy to guard the parcel for 12s (once).":"");
                }
                Choice seal=C(eligible?"核验封条":"查看记录",
                    eligible?"Inspect cargo seal":"Read the log",
                    eligible?"far_courier_stamp":"close");
                Choice patchChoice=C("加固货物 · 零件1","Reinforce · 1 spare part","far_courier_patch");
                Choice guardChoice=C("护送结界 · 耐力30","Convoy ward · 30 energy","far_courier_guard");
                Choice leave=C("继续行路","Keep walking","close");
                if(patch&&guard)Dialog(o.name,o.en,noticeZh,noticeEn,
                    seal,patchChoice,guardChoice,leave);
                else if(patch)Dialog(o.name,o.en,noticeZh,noticeEn,
                    seal,patchChoice,leave);
                else if(guard)Dialog(o.name,o.en,noticeZh,noticeEn,
                    seal,guardChoice,leave);
                else Dialog(o.name,o.en,noticeZh,noticeEn,seal,leave);
                return true;
            }
            if(bridge)
            {
                Dialog(o.name,o.en,FarCourierActive?
                    "救援包正在你身上。沿修好的桥面向南步行，并到远端避险棚亲手交货。你也可以把它留在桥头。":
                    "桥已经重新开放，可以真正向远端避险棚运送物资。领取后95秒内步行穿过新桥；桥南可能遭遇真正的劫匪拦截。负重会稍微减慢速度，不能使用传送跳过。",
                    FarCourierActive?
                    "Carry your relief parcel across the restored bridge to the outer refuge, or leave it safely here.":
                    "The reopened bridge can finally deliver aid. Carry a real parcel across it to the public refuge within 95 seconds. Real raiders intercept the crossing; fight or evade them. Weight slows walking and fast travel cannot substitute.",
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
                "封条完整！这份物资确实经由居民修好的桥面送达。"+
                    (FarCourierSecuredInFlight?
                        "你亲自击败了两名劫匪，交接时还能领取一次性安全护送奖金。":
                        "你选择谨慎绕过追兵，仍可正常交付全部物资。")+
                    (courierIntegrity>=90?
                        "药箱仍有至少九成完好，居民能把完整药品留给本地避险棚。":
                        "药箱在途中受损，但居民仍会接收剩余的必需品。"):
                "救援包仍在，但需要从北向南经过新桥，并在中途居民检查台核验封条。",
                ready?
                "The parcel truly crossed the civilian bridge."+
                    (FarCourierSecuredInFlight?
                        " You defeated both real raiders; a one-time secure-route bonus is available at delivery.":
                        " You avoided the pursuers and can still complete the full delivery.")+
                    (courierIntegrity>=90?
                        " At least 90% of the medicine survived; this refuge can keep its treatment stock.":
                        " Some medicine was damaged, but the remaining supplies can still be handed over."):
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
                action!="far_courier_guard"&&
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
            if(action=="far_courier_guard")
            {
                if(!FarCourierActive||!courierCrossed||courierGuardSpent||
                    !HasPhysicalInteractionReach(checkpoint))return true;
                if(save.energy<30)
                {
                    Notify("护送结界需要30耐力；休整后仍可再尝试。",
                        "Convoy ward needs 30 energy. Recover and try again.");return true;
                }
                save.energy-=30;courierGuardSpent=true;courierGuardLeft=12;
                sound="skill";revision++;
                Notify("救援包结界开启12秒：真实攻击的货物冲击减少55%，主角仍会正常受伤。",
                    "Parcel ward active for 12s: 55% less cargo impact, but you still take normal damage.");
                return true;
            }
            if(action=="far_courier_patch")
            {
                if(!FarCourierActive||!courierCrossed||courierPatched||
                    courierIntegrity>=99||
                    !HasPhysicalInteractionReach(checkpoint))return true;
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
                    !HasPhysicalInteractionReach(checkpoint)||
                    (Position-courierLast).Length>.65f)return true;
                courierStamped=true;revision++;sound="quest";
                Notify("封条已由居民核验，继续送往避险棚。",
                    "Parcel seal verified by locals. Continue to the refuge.");
                return true;
            }
            if(action=="far_courier_cancel")
            {
                if(FarCourierActive&&HasPhysicalInteractionReach(bridge))
                {ResetFarCourier();Notify("救援包留在桥头。","Parcel returned to the bridge.");}
                return true;
            }
            if(action=="far_courier_start")
            {
                if(FarCourierActive||!HasPhysicalInteractionReach(bridge))return true;
                // Reuse the same comprehensive live-field exclusion as
                // resting and the minecart/frost mission pickup. A second
                // transport clock must never mask the current quest objective.
                if(FieldWorkPreventsRest)
                {
                    Notify("仍有进行中的现场行动。先完成或在原地点明确取消，再领取救援包。",
                        "Another field mission is active. Finish it or cancel at its original station before collecting a parcel.");
                    return true;
                }
                if(!SafeTravel)
                {Notify("先清除桥边敌人。","Clear nearby hostiles first.");return true;}
                courierRoom=room;courierLast=Position;
                courierWalked=courierElapsed=0;
                courierIntegrity=100;
                courierCrossed=courierStamped=courierBridgeEntered=false;
                courierPatched=courierIntegrityWarning=courierGuardSpent=courierRaided=false;
                courierConfirmedRaiderKills=0;
                courierGuardLeft=0;
                revision++;sound="quest";
                Notify("救援包已背上：95秒内步行过桥并送到避险棚！",
                    "Parcel loaded: cross the bridge on foot and reach the shelter in 95 seconds!");
                return true;
            }
            if(!FarCourierActive||
                !HasPhysicalInteractionReach(shelter)||
                (Position-courierLast).Length>=.65f||
                !courierCrossed||!courierStamped||courierWalked<17||courierElapsed>=95)return true;
            if(!SafeTravel)
            {Notify("避险棚附近仍有敌人。","Hostiles threaten the shelter.");return true;}
            bool secured=FarCourierSecuredInFlight;
            bool pristine=courierIntegrity>=90;
            ResetFarCourier();
            Flag("far_courier_"+room);
            if(pristine)Flag("far_courier_pristine_"+room);
            CreditCoins(25);CreditShards(1);
            AddXp(38);
            if(secured)
            {
                Flag("far_courier_secured_"+room);
                CreditCoins(10);AddXp(15);
            }
            revision++;sound="reward";
            if(FarCourierDeliveredCount==8&&!Has("far_courier_all"))
            {Flag("far_courier_all");CreditSkillPoints(1);revision++;}
            RecordMoment("第 "+(room+1)+" 份居民救援包通过真正的山脊步行路线到达公共避险棚。"+
                    (secured?"两名追兵确实被击败，这条补给路已被护卫清理。":"")+
                    (pristine?"药箱保留九成以上药品，供本地避险棚长期使用。":""),
                "A civilian parcel reached the public refuge on foot."+
                    (secured?" Both pursuing raiders were truly defeated; the road was secured.":"")+
                    (pristine?" At least 90% of the medicines arrived for local refuge care.":""));
            Notify(secured?
                "救援包与安全护送完成：硬币35、碎片1、经验53！":
                "救援包已交接：硬币25、碎片1、经验38！",
                secured?
                "Secure delivery! +35 coins, +1 shard, +53 XP!":
                "Parcel delivered: +25 coins, +1 shard, +38 XP!");
            if(pristine)Notify("药品完好：本地避险棚每次添柴休息额外恢复10生命。",
                "Medicine intact: each timber-fueled rest at this refuge restores 10 extra HP.");
            return true;
        }
    }
}
