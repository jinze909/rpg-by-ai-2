using System;
using System.Collections.Generic;
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
        // Completing an eight-region public service is irreversible. When
        // the player already holds 99 skill points, CreditSkillPoints would
        // silently clamp the one-time completion prize to zero. Record the
        // unpaid point on the same existing SaveData v2 flag list so it can
        // later be signed for at a REAL staffed relay or watch post.
        private static string OuterMilestoneDebt(string completionFlag)
        {return "outer_talent_due_"+completionFlag;}
        private void AwardOuterMilestoneTalent(string completionFlag)
        {
            if(save.skillPoints>=99)
                Flag(OuterMilestoneDebt(completionFlag));
            else CreditSkillPoints(1);
        }
        public bool OuterMilestoneRewardPending(string completionFlag)
        {
            if(completionFlag!="outer_rescues_complete"&&
               completionFlag!="outer_deliveries_complete"&&
               completionFlag!="outer_patrol_all"&&
               completionFlag!="outer_patrol_veteran_all"&&
               completionFlag!="outer_patrol_flawless_all")return false;
            return Has(completionFlag)&&Has(OuterMilestoneDebt(completionFlag));
        }
        private static readonly string[] OuterWatchTalentMilestones={
            "outer_deliveries_complete","outer_patrol_all",
            "outer_patrol_veteran_all","outer_patrol_flawless_all"
        };
        private string NextOuterMilestoneAt(WorldObject site)
        {
            if(site==null||save.room<0||save.room>=8)return null;
            if(site.kind=="outerrelay"&&OuterRelayRestored(save.room))
                return OuterMilestoneRewardPending("outer_rescues_complete")?
                    "outer_rescues_complete":null;
            if(site.kind=="outerwatch"&&OuterPatrolDone(save.room))
                foreach(string id in OuterWatchTalentMilestones)
                    if(OuterMilestoneRewardPending(id))return id;
            return null;
        }
        private int CountUnpaidWatchTalents()
        {
            int count=0;
            foreach(string id in OuterWatchTalentMilestones)
                if(OuterMilestoneRewardPending(id))count++;
            return count;
        }
        private void AppendOuterMilestoneClaimChoice(WorldObject source)
        {
            if(source==null||mode!="dialog"||conversation==null||
                !SourceBelongsToActiveMap(source)||!HasPhysicalInteractionReach(source))
                return;
            string milestone=NextOuterMilestoneAt(source);
            if(milestone==null)return;
            foreach(Choice choice in conversation.choices)
                if(choice.action=="outer_milestone_claim")return;
            int unpaid=source.kind=="outerwatch"?CountUnpaidWatchTalents():0;
            if(unpaid>1)
            {
                conversation.text+="\n\n值守员摊开厚厚的八地账册，指着其中 "+unpaid+" 笔未结报酬：“每次守岗都有签名，你该拿的，我们一笔都没划掉。”";
                conversation.textEn+="\n\nThe watchkeeper opens the eight-region ledger to "+unpaid+" unpaid entries. “Every defended post has a signature. None of your earned lessons were crossed out.”";
            }
            else if(source.kind=="outerrelay")
            {
                conversation.text+="\n\n修复的灯柱上挂着一册八地账册。封夹里还压着巡路队留给你的欠条：最后一课等你在这盏灯下签领。";
                conversation.textEn+="\n\nAn eight-region ledger hangs beneath the repaired signal. Inside is a roadwarden's signed note: your last lesson remains unclaimed here at the lamp.";
            }
            else
            {
                conversation.text+="\n\n巡路人从装订好的八地账册里抽出一张欠条：“人和货都已经到了，最后那一点技艺传授，还得你亲自回来签领。”";
                conversation.textEn+="\n\nThe roadwarden slips an unpaid note from the eight-region ledger. “The people and supplies are safe. We still owe you the last lesson. Come sign for it yourself.”";
            }
            var options=new List<Choice>(conversation.choices);
            int insert=options.Count;
            if(insert>0&&options[insert-1].action=="close")insert--;
            options.Insert(insert++,C("签领八地巡路报酬 · 技能点1",
                "Claim earned roadwarden talent · +1",
                "outer_milestone_claim"));
            // Several separate watch medals can be owed to the same actual
            // watchkeeper. Preserve the original one-at-a-time receipt, but
            // offer an optional single visit to settle as many as the existing
            // 99-point wallet can hold. Each individual signed debt remains
            // independent in the unchanged v2 flags.
            if(unpaid>1)
                options.Insert(insert,C("合并签领欠付报酬 · 共 "+unpaid+" 点",
                    "Sign available watch rewards · "+unpaid+" owed",
                    "outer_milestone_claim_all"));
            conversation.choices=options.ToArray();
        }
        private bool ChooseOuterMilestoneClaim(string action)
        {
            bool collectAll=action=="outer_milestone_claim_all";
            if(!collectAll&&action!="outer_milestone_claim")return false;
            WorldObject source=pendingDialogueSource;
            if(source==null||Dying||practicing||
                pendingDialogueSourceRoom!=save.room||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source))return true;
            string milestone=NextOuterMilestoneAt(source);
            if(milestone==null)return true;
            if(save.skillPoints>=99)
            {
                Notify("巡路人保留着你的技能点报酬。先使用已有技能点，再回来签领。",
                    "Your earned talent is reserved. Spend a skill point, then return to sign for the lesson.");
                return true;
            }
            if(collectAll)
            {
                // Only an authenticated defended watch may settle multiple
                // completed watches. A restored relay can never claim them.
                if(source.kind!="outerwatch")return true;
                int paid=0;
                foreach(string id in OuterWatchTalentMilestones)
                {
                    if(save.skillPoints>=99)break;
                    if(!OuterMilestoneRewardPending(id))continue;
                    // Retire each earned IOU before the skill wallet changes.
                    // Any remainder survives cap, travel and v2 reload.
                    save.flags.Remove(OuterMilestoneDebt(id));
                    CreditSkillPoints(1);
                    paid++;
                }
                if(paid>0)
                {
                    revision++;sound="reward";
                    Notify("巡路队签领完成：技能点 +"+paid+
                        "。未能装入钱包的报酬仍在账册里。",
                        "Watch rewards signed: +"+paid+
                        " talent points. Any excess remains in the ledger.");
                }
                return true;
            }
            // Consume exactly this milestone's authenticated IOU before
            // paying. Another regional milestone or a forged same-kind
            // actor cannot cause a duplicate / remote reward.
            save.flags.Remove(OuterMilestoneDebt(milestone));
            CreditSkillPoints(1);
            revision++;sound="reward";
            Notify("巡路队已结清一份八地报酬：技能点 +1。",
                "Roadwarden completion payment signed: +1 talent point.");
            return true;
        }
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

        // Runtime and HUD share the same exact source/terrain,
        // safety and inventory predicate. A waypoint outside the actual
        // interaction circle does not advertise an actionable paid rest.
        public bool OuterSignalCampReady
        {
            get
            {
                if(mode!="world"||save.inventory[0]<1||
                    !OuterRelayRestRefuge)return false;
                foreach(WorldObject signal in Map.objects)
                    if(signal.kind=="outerrelay"&&
                        HasPhysicalInteractionReach(signal))return true;
                return false;
            }
        }


        // A real, optional three-stop civilian medicine handoff on each
        // restored regional route. Progress and the live clock are transient;
        // only legitimate completed deliveries add an ordinary v2 flag.
        private int outerDeliveryRoom=-1,outerDeliveryStage;
        private float outerDeliverySeconds;
        // A genuine hit can spill the carried medical dressings onto their
        // ACTUAL physical field foot. Recover with E before delivery, while
        // the unchanged 90-second mission clock keeps running. Transient
        // combat state only; the existing v2 completion flag is preserved.
        private bool outerDeliveryDropped;
        private Point outerDeliveryDropFoot;
        public bool OuterDeliveryParcelDropped
        {get{return OuterDeliveryActive&&outerDeliveryStage==2&&outerDeliveryDropped;}}
        public Point OuterDeliveryParcelFoot
        {get{return OuterDeliveryParcelDropped?outerDeliveryDropFoot:new Point();}}
        public bool OuterDeliveryCanRecoverParcel
        {
            get
            {
                return mode=="world"&&OuterDeliveryParcelDropped&&
                    !Dying&&!practicing&&CanStand(Position)&&
                    CanStand(outerDeliveryDropFoot)&&
                    (Position-outerDeliveryDropFoot).Length<=1.45f&&
                    ClearAttackPath(Position,outerDeliveryDropFoot);
            }
        }
        private void DamageOuterDeliveryDressings(float actualDamage,string source)
        {
            // Invincible / fully parried / zero-power hits never call this.
            // Ambient temperature, ordinary movement and unsupported
            // blocked feet must never teleport or spill the physical box.
            if(outerDeliveryRoom!=save.room||outerDeliveryStage!=2||
                outerDeliveryDropped||source=="environment"||
                !Finite(actualDamage)||actualDamage<=0f||
                !CanStand(Position))return;
            outerDeliveryDropped=true;
            outerDeliveryDropFoot=Position;
            effects.Add(new Effect("discover",outerDeliveryDropFoot,.7f));
            revision++;sound="hurt";
            Notify("药布在战斗中掉落！回到原地按 E 拾起，再送往哨岗。倒计时仍在继续。",
                "Dressings dropped on impact! Return to the original foot and press E to recover them before delivery. The timer continues.");
        }
        private bool TryRecoverOuterDeliveryParcel()
        {
            if(!OuterDeliveryCanRecoverParcel)return false;
            Point location=outerDeliveryDropFoot;
            outerDeliveryDropped=false;
            effects.Add(new Effect("discover",location,.45f));
            revision++;sound="reward";
            Notify("已捡回药布和收据。继续将物资送到哨岗。",
                "Dressings and receipt recovered. Deliver the original parcel to the watch.");
            return true;
        }
        public bool OuterDeliveryActive
        { get{return outerDeliveryRoom==save.room&&outerDeliveryStage>0;} }
        public int OuterDeliveryStage {get{return OuterDeliveryActive?outerDeliveryStage:0;}}
        public float OuterDeliverySeconds {get{return OuterDeliveryActive?outerDeliverySeconds:0f;}}
        public bool OuterDeliveryDone(int room)
        {return room>=0&&room<8&&Has("outer_delivery_done_"+room);}
        public int OuterDeliveryWins
        {get{int n=0;for(int i=0;i<8;i++)if(OuterDeliveryDone(i))n++;return n;}}
        private bool OuterDeliveryAvailable
        {
            get{return save.room>=0&&save.room<8&&
                !OuterDeliveryActive&&!OuterDeliveryDone(save.room)&&
                OuterRelayRestored(save.room)&&OuterCacheRecovered(save.room)&&
                OuterPatrolDone(save.room)&&!Dying&&!practicing&&!racing&&
                !FieldWorkPreventsRest&&SafeTravel;}
        }
        private void ResetOuterDelivery()
        {
            outerDeliveryRoom=-1;outerDeliveryStage=0;outerDeliverySeconds=0;
            outerDeliveryDropped=false;outerDeliveryDropFoot=new Point();
        }
        private void TickOuterDelivery(float dt)
        {
            if(outerDeliveryRoom<0)return;
            // Leaving the physical region or dying retires ALL unsaved
            // cargo state. A return from another region must never resume
            // a paused older stopwatch or find a ghost dropped parcel.
            if(outerDeliveryRoom!=save.room||Dying||practicing)
            {ResetOuterDelivery();revision++;return;}
            outerDeliverySeconds=Math.Max(0f,outerDeliverySeconds-dt);
            if(outerDeliverySeconds>0f)return;
            ResetOuterDelivery();revision++;
            Notify("药品急送超时。补给还在灯下，可重新启程。",
                "The medicine run ran out of time. The supplies remain; depart again when ready.");
        }
        private bool TalkOuterDelivery(WorldObject o)
        {
            if(!OuterDeliveryActive||
                (o.kind!="outercache"&&o.kind!="outerwatch"))
                return false;
            if(!HasPhysicalInteractionReach(o))return true;
            if(o.kind=="outercache")
            {
                Dialog(o.name,o.en,
                    outerDeliveryStage==1?
                        "箱里还有一捆未拆封的药布。巡路队把收据压在箱扣下：‘必须由人亲手送到哨岗，不能放在路上等车。’":
                        outerDeliveryDropped?
                        "药布被撞落在路上，不在箱里。回到掉落地点按 E 捡起，倒计时不会暂停。":
                        "你已经把药布和收据放进背包。哨岗在北边，受伤的人还在那里等着。",
                    outerDeliveryStage==1?
                        "An unopened roll of dressings lies under the lid. The receipt reads: ‘Hand it to the wardens in person. Do not leave it beside the cart road.’":
                        outerDeliveryDropped?
                        "The dressings fell beside the road, not into this chest. Return to the drop and press E; the timer does not pause.":
                        "The dressings and receipt are in your pack. The defended post is north of here, and someone is waiting.",
                    outerDeliveryStage==1?
                        C("取走包扎药布","Take the dressings","outer_delivery_pickup"):
                        C("向北赶路","Head north","close"),
                    C("合上箱盖","Close the chest","close"));
                return true;
            }
            Dialog(o.name,o.en,
                outerDeliveryStage==2&&!outerDeliveryDropped?
                    "哨岗里有人扶着伤员坐下。值守员一眼认出你怀里的药布，伸手接过还未干的收据。":
                    outerDeliveryDropped?
                    "值守员看着你空着的手：药布掉在半路了。你必须先回到落点将药布和收据一起捡回来。":
                    "值守员翻着空白的交接簿。药布还在南边的旧箱里，得亲自带过来。",
                outerDeliveryStage==2&&!outerDeliveryDropped?
                    "Someone helps an injured traveler to a seat. The watchkeeper recognizes the roll in your arms and reaches for the damp receipt.":
                    outerDeliveryDropped?
                    "The watchkeeper sees your empty hands: the dressings were dropped on the road. Recover the parcel and receipt at their original foot before handing them over.":
                    "The watchkeeper checks the empty delivery ledger. The dressings are still in the old chest to the south.",
                outerDeliveryStage==2&&!outerDeliveryDropped?
                    C("交付药布和收据","Deliver dressings and receipt","outer_delivery_deliver"):
                    C("去南边救援箱取药","Retrieve the chest to the south","close"),
                C("继续赶路","Keep moving","close"));
            return true;
        }
        private bool ChooseOuterDelivery(string action)
        {
            bool start=action=="outer_delivery_start";
            bool pickup=action=="outer_delivery_pickup";
            bool deliver=action=="outer_delivery_deliver";
            bool abort=action=="outer_delivery_abort";
            if(!start&&!pickup&&!deliver&&!abort)return false;
            WorldObject source=pendingDialogueSource;
            // The cached dialogue must still belong to the EXACT physical
            // source in this room. A different NPC with identical kind or
            // coordinates cannot accept the medicine on its behalf.
            if(source==null||!SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source)||Dying)return true;
            if(start)
            {
                if(source.kind!="outerrelay"||!OuterDeliveryAvailable)return true;
                outerDeliveryRoom=save.room;outerDeliveryStage=1;
                outerDeliveryDropped=false;outerDeliveryDropFoot=new Point();
                outerDeliverySeconds=90f;revision++;sound="quest";
                TrackQuest("outer_delivery");
                Notify("巡路队急送开始：90秒内到南边旧药箱取药，再送到北边已守住的哨岗。",
                    "Roadwarden run started: collect dressings from the southern chest and deliver to the northern defended post within 90 seconds.");
                return true;
            }
            if(!OuterDeliveryActive)return true;
            if(abort)
            {
                if(source.kind!="outerrelay")return true;
                ResetOuterDelivery();revision++;
                Notify("这趟急送已取消。没有扣除物资，下次仍可从灯下启程。",
                    "Run withdrawn. No supplies lost; the lamp can dispatch again.");
                return true;
            }
            if(pickup)
            {
                if(source.kind!="outercache"||outerDeliveryStage!=1)return true;
                outerDeliveryStage=2;outerDeliveryDropped=false;
                revision++;sound="quest";
                Notify("拿到药布和收据！哨岗就在正北方，继续赶路。",
                    "Dressings and receipt secured. The defended watch is directly north.");
                return true;
            }
            if(source.kind!="outerwatch"||outerDeliveryStage!=2||
                outerDeliveryDropped||
                !OuterPatrolDone(save.room)||!OuterCacheRecovered(save.room))
                return true;
            // A full coin wallet cannot silently erase the one-time payout
            // when the completion flag is saved. Keep the parcel until space
            // exists, or allow the attempt to expire and retry.
            if(save.coins>999975)
            {
                Notify("补给结算需要24硬币的容量；请先腾出钱包空间。",
                    "The reward requires space for 24 coins. Free wallet capacity first.");
                return true;
            }
            int room=save.room;
            ResetOuterDelivery();
            Flag("outer_delivery_done_"+room);
            CreditCoins(24);AddXp(40);
            if(OuterDeliveryWins==8&&!Has("outer_deliveries_complete"))
            {Flag("outer_deliveries_complete");
             AwardOuterMilestoneTalent("outer_deliveries_complete");}
            revision++;sound="reward";
            RecordMoment("第 "+(room+1)+" 片边境，巡路队在九十秒内收到亲手送达的药布和原始收据。",
                "Dressings and their original receipt reached the defended watch in region "+(room+1)+" by hand.");
            Notify("药布已交给值守员 · 硬币 +24、经验 +40。",
                "Dressings delivered to the watchkeeper · +24 coins, +40 XP.");
            return true;
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
                if(first)
                {
                    Flag("outer_log_"+room);AddXp(8);sound="reward";
                    Notify("线索已录入巡路队档案 · 经验 +8",
                        "Clue entered in the roadwardens' ledger · +8 XP");
                }
                Dialog(titleZh,titleEn,OuterLogZh[room,0]+
                    (first?"\n\n册页背面指向东北方的旧信号灯。那里还欠一次修理。":
                    "\n\n这份纸上的字迹你认得。信号灯仍在东北方。"),
                    OuterLogEn[room,0]+
                    (first?"\n\nThe back of the page points toward the old signal light northeast of here. It still needs repair.":
                    "\n\nYou recognize the writing. The signal light is still northeast of here."),
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
                string noteZh=OuterLogZh[room,1]+
                    (recorded?"\n\n灯火稳了。救援箱在正南方；四周安全时，也能在灯下歇一会儿。"+(OuterSignalRouteReady?"\n\n你看见巡路队新贴的行车牌：沿已点亮的灯，可搭他们的车走下一段救援路。":""):
                        "\n\n灯座的线圈少一枚零件。修好它，正南方的救援箱才能按旧规取走。");
                string noteEn=OuterLogEn[room,1]+
                    (recorded?"\n\nThe light holds steady. The rescue chest lies directly south. When the road is quiet, you can rest here."+(OuterSignalRouteReady?"\n\nThe roadwardens posted a new cart schedule. Their couriers travel between repaired lamps.":""):
                        "\n\nOne spare part is missing from the socket. Repair it, and the relief chest directly south of here can be claimed.");
                if(recorded&&OuterDeliveryActive)
                    Dialog(titleZh,titleEn,
                        "计时还剩 "+Math.Ceiling(outerDeliverySeconds)+" 秒。"+
                        (outerDeliveryStage==1?
                            "南边救援箱里有一捆药布，去取回来。":
                            outerDeliveryDropped?
                            "药布在遭袭时掉在路上！赶紧回到落点按 E 拾取，再去哨岗交货。":
                            "药布在你身上，得亲自送到北边的哨岗。"),
                        "Time left: "+Math.Ceiling(outerDeliverySeconds)+"s. "+
                        (outerDeliveryStage==1?
                            "Collect the dressings from the chest south of here.":
                            outerDeliveryDropped?
                            "A real hit dropped your dressings on the trail! Recover them at the marked foot with E before the watch accepts delivery.":
                            "Carry the dressings to the defended watch north of here."),
                        C("取消这趟急送","Cancel this run","outer_delivery_abort"),
                        C("立刻赶路","Back to the road","close"));
                else if(recorded)
                {
                    var options=new List<Choice>();
                    if(OuterDeliveryAvailable)
                        options.Add(C("巡路队 · 九十秒药品急送",
                            "Roadwarden · 90-second medicine run",
                            "outer_delivery_start"));
                    options.Add(C("搭棚休整 · 木材×1",
                        "Rest under the signal · 1 timber","outer_signal_rest"));
                    options.Add(C("查看修复的救援灯路网",
                        "Travel repaired rescue-light routes","outer_signal_routes"));
                    options.Add(C("继续赶路","Keep moving","close"));
                    Dialog(titleZh,titleEn,noteZh+
                            "\n\n如果需要重新包扎伤口，可以花一捆木材在灯下支起短时挡风棚；巡路员会在你休整时替你守着道路。",
                        noteEn+
                            "\n\nA bundle of timber will hold a windbreak beside the lamp long enough to dress wounds. The roadwardens can watch the path while you rest.",
                        options.ToArray());
                }
                else
                    Dialog(titleZh,titleEn,noteZh,noteEn,
                        C("维修信号灯 · 零件×1","Repair signal · 1 spare part","outer_repair"),
                        C("稍后再来","Return later","close"));
                return true;
            }
            if(!OuterRelayRestored(room))
            {
                Dialog(titleZh,titleEn,
                    OuterLogZh[room,2]+
                    (OuterLogFound(room)?
                        "\n\n箱口的封条还压着。北边的求援灯亮起来后，才有人能核对这批物资。":
                        "\n\n封条上的编号被泥盖住了。先去西南边找原始记录，再到东北的灯架核对。"),
                    OuterLogEn[room,2]+
                    (OuterLogFound(room)?
                        "\n\nThe seal is still fastened. Nobody can sign out these supplies until the rescue light to the north is lit.":
                        "\n\nMud covers the seal number. Find the old log southwest of here, then check the light to the northeast."),
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
                { Flag("outer_rescues_complete");
                  AwardOuterMilestoneTalent("outer_rescues_complete");revision++; }
                RecordMoment("第 "+(room+1)+" 片边境的求援灯已恢复，救援箱里留下的人名与物资都被找回。",
                    "The remote rescue signal and relief chest were recovered in region "+(room+1)+".");
                Notify("救援物资已登记 · 硬币 +18、材料 +2、经验 +22",
                    "Relief supplies recorded · +18 coins, +2 materials, +22 XP");
            }
            Dialog(titleZh,titleEn,OuterLogZh[room,2]+
                (firstCache?"\n\n你将箱盖扣好，在巡路簿上抄下了那些没等到归来的人名。":
                "\n\n箱盖上还有你留下的记号；这批物资已被转运。"),
                OuterLogEn[room,2]+
                (firstCache?"\n\nYou close the lid and copy the names of those who never returned into the roadwardens' book.":
                "\n\nYour mark is still on the lid. These supplies have already been carried away."),
                C("记下归途","Record the route","close"));
            return true;
        }

        // Resident couriers can use the restored lamps to maintain real
        // supply routes. This is not a global teleport menu: the current
        // player must stand at their OWN repaired light, with a safe lane,
        // and both endpoints must already be permanently repaired.
        public bool OuterSignalRouteReady
        {
            get
            {
                // OnGUI shows a route cue only at a physically usable
                // restored source, with at least one legal second endpoint.
                if(mode!="world"||!CanUseOuterSignalRoads())return false;
                for(int room=0;room<8;room++)
                    if(room!=save.room&&OuterRelayRestored(room)&&
                        CanEnter(room))return true;
                return false;
            }
        }
        private bool CanUseOuterSignalRoads()
        {
            if(save.room<0||save.room>=8||!OuterRelayRestored(save.room)||
                Dying||practicing||racing||FieldWorkPreventsRest||
                OuterPatrolActive||!SafeTravel)return false;
            foreach(WorldObject lamp in Map.objects)
                if(lamp.kind=="outerrelay"&&
                    HasPhysicalInteractionReach(lamp))return true;
            return false;
        }
        // Both courier lights AND previously defended regional outposts
        // use one authoritative party-safe arrival. A restored v2 journey
        // cannot lose a companion at the spawn or overlap a living enemy.
        private bool TryLandTravelPartyAtPost(string postKind)
        {
            WorldObject lamp=null;
            foreach(WorldObject point in Map.objects)
                if(point.kind==postKind){lamp=point;break;}
            if(lamp==null)return false;
            Point center=new Point(lamp.x,lamp.y);
            Point[] offsets={new Point(0f,1f),new Point(1f,0f),
                new Point(-1f,0f),new Point(0f,-1f),
                new Point(1f,1f),new Point(-1f,1f),
                new Point(1f,-1f),new Point(-1f,-1f),
                new Point(0f,2f),new Point(2f,0f),
                new Point(-2f,0f),new Point(0f,-2f)};
            Point[] allyOffsets={new Point(-.7f,0f),
                new Point(.7f,0f),new Point(0f,1f),
                new Point(0f,-1f)};
            Point best=new Point(),bestAlly=new Point();
            float bestClearance=-1f;
            bool found=false;
            foreach(Point offset in offsets)
            {
                Point foot=center+offset;
                if(!CanStand(foot)||!ActorHasLocalRoute(foot,center,4))
                    continue;
                float clearance=9999f;
                foreach(Enemy foe in enemies)
                    if(foe.hp>0)
                        clearance=Math.Min(clearance,
                            (foe.Position-foot).Length);
                if(clearance<4f||clearance<=bestClearance)continue;
                Point allyFoot=new Point();
                bool allySafe=save.companion<0;
                if(save.companion>=0)
                    foreach(Point allyOffset in allyOffsets)
                    {
                        Point candidate=foot+allyOffset;
                        if(!CanStand(candidate)||
                            !ActorHasLocalRoute(candidate,foot,3))continue;
                        bool occupied=false;
                        foreach(Enemy foe in enemies)
                            if(foe.hp>0&&(foe.Position-candidate).Length<3f)
                            {occupied=true;break;}
                        if(occupied)continue;
                        allySafe=true;allyFoot=candidate;break;
                    }
                if(!allySafe)continue;
                found=true;best=foot;bestAlly=allyFoot;
                bestClearance=clearance;
            }
            // A suddenly sealed or crowded destination retains Enter's
            // existing safe regional spawn. Never spawn an actor in stone,
            // hostile reach, floodwater or on another living body.
            if(!found)return false;
            save.x=best.x;save.y=best.y;
            if(save.companion>=0)
            {
                companionPosition=bestAlly;
                companionMoving=false;companionMoveTime=0f;
                companionPresentationGeneration++;
            }
            return true;
        }
        private bool ChooseOuterSignalRoads(string action)
        {
            bool main=action=="outer_signal_routes";
            bool west=action=="outer_signal_ring_0";
            bool east=action=="outer_signal_ring_1";
            bool going=action.StartsWith("outer_signal_to_");
            if(!main&&!west&&!east&&!going)return false;
            if(!CanUseOuterSignalRoads())
            {
                Notify("只能在已修复的求援灯旁安全通行；敌人和现场任务会阻断道路。",
                    "Use a repaired rescue lamp in person. Enemies and active field missions block the routes.");
                return true;
            }
            if(main)
            {
                Dialog("巡路队 · 求援灯道路","Roadwardens · Signal Light Routes",
                    "修好的灯让巡路队重新认得道路。只可以前往你已经点亮、而且章节允许进入的地区。旅人必须在灯下启程，危险时不能出发。",
                    "With the lamps burning again, local couriers know the roads. Travel only between lights you have personally repaired and regions already unlocked. Depart beside a real signal when the road is safe.",
                    C("西部四地","Western routes","outer_signal_ring_0"),
                    C("远岸四地","Farshore routes","outer_signal_ring_1"),
                    C("暂不启程","Stay here","close"));
                return true;
            }
            if(west||east)
            {
                int first=east?4:0;
                var choices=new List<Choice>();
                for(int i=first;i<first+4;i++)
                    if(i!=save.room&&OuterRelayRestored(i)&&CanEnter(i))
                        choices.Add(C("前往 "+OuterWatchNamesZh[i],
                            "Travel to "+OuterWatchNamesEn[i],
                            "outer_signal_to_"+i));
                bool empty=choices.Count==0;
                choices.Add(C("返回灯路图","Back to signal routes","outer_signal_routes"));
                choices.Add(C("留在原地","Remain here","close"));
                Dialog("救援灯 · "+(east?"远岸":"西部"),
                    "Rescue Lights · "+(east?"Farshore":"West"),
                    empty?"这条道路上还没有第二盏你点亮的求援灯。":
                        "选一处确实修好的信号灯。出发前会重新检查道路安全。",
                    empty?"You have not repaired another reachable light in this group yet.":
                        "Choose a signal you repaired. Route safety is checked again at departure.",
                    choices.ToArray());
                return true;
            }
            int room;
            if(!int.TryParse(action.Substring("outer_signal_to_".Length),out room)||
                room<0||room>=8||room==save.room||
                !OuterRelayRestored(room))return true;
            if(!CanEnter(room))
            {
                Notify(RouteHint(room,false),RouteHint(room,true));
                return true;
            }
            int origin=save.room;
            // Reuse ALL established campaign, mission, save and map-reset
            // protections. Only land at the physical new light afterward.
            Enter(room);
            if(save.room!=room||save.room==origin)return true;
            bool landed=TryLandTravelPartyAtPost("outerrelay");
            revision++;sound="travel";
            Notify(landed?
                    "沿巡路队的救援道抵达下一盏信号灯。":
                    "前方信号灯周围不安全，已在本地区入口落脚。",
                landed?
                    "The courier route ends beside the next repaired light.":
                    "The light's approach is unsafe. You arrived at the regular region entrance.");
            return true;
        }
        private bool ChooseOuterRescue(string action)
        {
            if(ChooseOuterMilestoneClaim(action))return true;
            if(ChooseOuterDelivery(action))return true;
            if(ChooseOuterSignalRoads(action))return true;
            if(action=="outer_signal_rest")
            {
                // A prior world conversation does not authorize remote
                // resting or a free time skip after its source vanishes.
                // Choose() has closed the menu but retains the exact source
                // for this one transaction. Do not trust kind/id alone.
                WorldObject source=pendingDialogueSource;
                bool authentic=source!=null&&source.kind=="outerrelay"&&
                    SourceBelongsToActiveMap(source)&&
                    HasPhysicalInteractionReach(source)&&
                    CanUseOuterSignalRoads();
                if(!authentic)
                {
                    Notify("要在修复的求援灯旁、未被阻隔的位置才能搭棚。",
                        "Reach the real restored signal on open ground before making camp.");
                    return true;
                }
                if(save.inventory[0]<1)
                {
                    Notify("没有足够的木材支起挡风棚。",
                        "You need one timber to set up the windbreak.");
                    return true;
                }
                // Spend exactly once AFTER source/combat/mission validation.
                // Share the same temporal cleanup used by the original
                // oasis, brazier and camp rest. The real tide advances too.
                if(!Spend(1,0,0,0,0))return true;
                ResolveRestTimeSkip(12f);
                save.health=Math.Min(save.maxHealth,save.health+24f);
                save.energy=Math.Min(100f,save.energy+34f);
                if(save.room==6)
                    save.heat=Math.Max(0f,save.heat-28f);
                if(save.room==7)
                    save.cold=Math.Max(0f,save.cold-28f);
                effects.Add(new Effect("heal",Position,.7f));
                sound="reward";revision++;
                Notify("挡风棚已收好：伤口得到包扎，体力恢复，时间前进12秒。",
                    "Signal-side rest complete: wounds dressed, energy restored, twelve seconds passed.");
                return true;
            }
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
            Notify("求援灯重新亮了。安全时可在灯下休整，再向南找救援箱。",
                "The rescue light is burning again. Rest here when safe, then head south for the relief chest.");
            return true;
        }
    }
}
