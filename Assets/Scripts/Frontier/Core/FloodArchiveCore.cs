using System;
namespace Starfall
{
    // The Drowned Ruins remember civilians whom the official cargo manifest
    // erased. This side quest uses ONLY version-two string flags.
    public sealed partial class GameCore
    {
        public bool FloodArchiveComplete
        { get { return Has("floodarchive_public")||Has("floodarchive_chart"); } }
        public int FloodArchiveChoice
        { get { return Has("floodarchive_public")?1:Has("floodarchive_chart")?2:0; } }
        public int FloodArchiveProgress
        {
            get
            {
                if(FloodArchiveComplete)return 3;
                if(Has("floodarchive_2"))return 2;
                if(Has("floodarchive_1"))return 1;
                return 0;
            }
        }
        private WorldObject FloodArchiveObject(int stage)
        {
            if(stage<1||stage>3)return null;
            foreach(WorldObject o in data.maps[2].objects)
                if(o.kind=="floodarchive"&&o.id=="floodarchive_"+stage)return o;
            return null;
        }
        // Tide period and any legitimately held low tide are the existing
        // shared simulation clocks, not a second unsaved puzzle clock.
        public float FloodArchiveWait(int stage)
        {
            if(stage==1)
                return !LowTide?0:
                    save.totalTime<save.tideHeldUntil?
                    Math.Max(0,save.tideHeldUntil-save.totalTime):
                    22f-save.totalTime%36f;
            if(stage==2)
                return LowTide?0:36f-save.totalTime%36f;
            return 0;
        }
        private void FloodArchiveDialog(WorldObject site,string zh,string en,params Choice[] choices)
        { Dialog(site.name,site.en,zh,en,choices); }
        private bool TalkFloodArchive(WorldObject site)
        {
            if(site.kind!="floodarchive")return false;
            int stage=0;
            for(int i=1;i<=3;i++)if(site.id=="floodarchive_"+i)stage=i;
            if(stage==0||save.room!=2||
                (Position-new Point(site.x,site.y)).Length>1.8f)return true;
            if(!Has("scout_saved"))
            {
                FloodArchiveDialog(site,
                    "残存水位刻度提到荆棘密林中的失踪侦察员。先救回他们，才有人能认出这份民用名单。",
                    "The tide scale mentions the missing Thornwood scout. Rescue the scout before anyone can identify the civilian names.",
                    C("记下方位","Mark the location","close"));
                return true;
            }
            if(FloodArchiveComplete)
            {
                string endZh=FloodArchiveChoice==1?
                    "你将名字交还摆渡人。低潮时他们在这里协助伤者，公共潮标附近能安全恢复生命。":
                    "你保留了水位原稿并教会渡船计算潮道，公共潮标附近能安全恢复耐力。";
                string endEn=FloodArchiveChoice==1?
                    "The names returned to the ferrymen. Nearby public tideposts now help wounded travelers recover safely.":
                    "The original tide charts were preserved for the ferry pilots. Nearby posts now help travelers regain stamina safely.";
                bool ferryDock=stage==1||stage==3;
                if(ferryDock&&LowTide)
                    FloodArchiveDialog(site,endZh+"\n\n历史只奖励一次。低潮时可乘公共渡船，花14耐力前往对岸。",
                        endEn+"\n\nThe history never pays twice. At low tide, spend 14 energy on a public ferry to the opposite bank.",
                        C("重读公开记录","Read the record","close"),
                        C("乘渡船 · 耐力14","Cross by ferry · 14 energy","floodarchive_ferry"));
                else FloodArchiveDialog(site,
                    endZh+(ferryDock?"\n\n当前水位过高，公共渡船要等退潮。":"\n\n历史只奖励一次。"),
                    endEn+(ferryDock?"\n\nThe public ferry waits for low tide.":"\n\nThe history never pays twice."),
                    C("重读公开记录","Read the record","close"));
                return true;
            }
            int progress=FloodArchiveProgress;
            if(stage<=progress)
            {
                FloodArchiveDialog(site,
                    "这里已找回原始民用记录。旧港账上漏写的不是货物，而是没有许可证的人。",
                    "The recovered civic record remembers people who had no travel papers, not simply lost cargo.",
                    C("继续寻找证据","Keep searching","close"));
                return true;
            }
            if(stage!=progress+1)
            {
                FloodArchiveDialog(site,
                    "档案缺页。必须按旧桥水尺、摆渡名册、最后公议台的顺序实地核对。",
                    "The archive is incomplete. Survey the flood gauge, ferry roster and public desk in order.",
                    C("返回上一处","Find previous evidence","close"));
                return true;
            }
            if(stage==1)
            {
                string zh="旧桥水尺记着未经许可的救援小艇。这份水线只有**涨潮**时才能辨认。";
                string en="The flood gauge records unlicensed rescue boats. Its upper watermark is legible only at HIGH tide.";
                if(LowTide)
                {
                    float wait=FloodArchiveWait(1);
                    FloodArchiveDialog(site,zh+"\n\n现在仍是低潮，约 "+wait.ToString("0")+" 秒后复查；交谈时潮汐暂停。",
                        en+"\n\nCurrently LOW tide. Return in about "+wait.ToString("0")+" seconds. Dialogue pauses time.",
                        C("等待涨潮","Wait for high tide","close"));
                }
                else FloodArchiveDialog(site,zh+"\n\n现在可以誊写隐藏的水尺刻度。",
                    en+"\n\nThe upper line is visible: copy the recovered scale.",
                    C("誊写涨潮证据","Copy high-tide marks","floodarchive_1"));
                return true;
            }
            if(stage==2)
            {
                string zh="摆渡名单藏在水下。只有**退潮**露出木板时才能读到被抹去的家庭姓名。";
                string en="The ferry roster lies submerged. Only LOW tide reveals the names of the erased families.";
                if(!LowTide)
                {
                    float wait=FloodArchiveWait(2);
                    FloodArchiveDialog(site,zh+"\n\n高潮仍在，约 "+wait.ToString("0")+" 秒后再来。",
                        en+"\n\nHIGH tide persists. Return in about "+wait.ToString("0")+" seconds.",
                        C("等待退潮","Wait for ebb","close"));
                }
                else FloodArchiveDialog(site,zh+"\n\n此时木板露出水面，可以抄录真实姓名。",
                    en+"\n\nThe planks have emerged. Recover the real names.",
                    C("抄录摆渡名册","Recover the ferry roster","floodarchive_2"));
                return true;
            }
            FloodArchiveDialog(site,
                "两份原始记录现在一致：离城者并非私运商人，而是被遗忘的平民。你要怎样修复这里的公共渡口？",
                "The two records agree: those who fled were uncounted civilians, not smugglers. How should this public crossing serve them?",
                C("公开名单 · 救治伤者","Publish names · tend the wounded","floodarchive_public"),
                C("保留水图 · 培训摆渡人","Preserve charts · train pilots","floodarchive_chart"));
            return true;
        }
        private bool FloodArchiveLandingSafe(Point destination)
        {
            if(!CanStand(destination))return false;
            foreach(Enemy enemy in enemies)
                if(enemy.hp>0&&(enemy.Position-destination).Length<6f)return false;
            foreach(Projectile shot in projectiles)
                if(!shot.friendly&&shot.life>0&&shot.damage>0&&
                    (shot.position-destination).Length<6f)return false;
            foreach(Hazard warning in hazards)
                if(warning.damage>0&&
                    (warning.position-destination).Length<4f+Math.Max(0,warning.radius))
                    return false;
            return true;
        }
        private bool ChooseFloodArchiveFerry()
        {
            if(!FloodArchiveComplete||save.room!=2||Dying||practicing||racing)return true;
            WorldObject from=null,to=null;
            WorldObject western=FloodArchiveObject(1),eastern=FloodArchiveObject(3);
            if(western==null||eastern==null)return true;
            if((Position-new Point(western.x,western.y)).Length<=1.8f)
            {from=western;to=eastern;}
            else if((Position-new Point(eastern.x,eastern.y)).Length<=1.8f)
            {from=eastern;to=western;}
            if(from==null)return true;
            if(!LowTide)
            {
                Notify("水位还未退去，公共渡船暂不通航。",
                    "The ferry cannot launch until low tide.");
                return true;
            }
            // Respect all current physical mission locks, actual threats at
            // BOTH banks and real resource costs. This never resets a room.
            if(FieldWorkPreventsRest||!SafeTravel||
                !FloodArchiveLandingSafe(new Point(to.x,to.y)))
            {
                Notify("渡口受到威胁，或仍有现场任务。请先确保两岸安全。",
                    "The crossing is unsafe or fieldwork remains active. Secure both banks.");
                return true;
            }
            if(save.energy<14)
            {
                Notify("安全渡河需要14耐力。","Crossing requires 14 energy.");
                return true;
            }
            save.energy-=14;save.x=to.x;save.y=to.y;
            revision++;sound="travel";
            Notify("低潮渡船把你安全带到了河对岸。","The low-tide public ferry brought you to the opposite bank.");
            return true;
        }
        private bool ChooseFloodArchive(string action)
        {
            if(!action.StartsWith("floodarchive_",StringComparison.Ordinal))return false;
            if(action=="floodarchive_ferry")return ChooseFloodArchiveFerry();
            if(action!="floodarchive_1"&&action!="floodarchive_2"&&
                action!="floodarchive_public"&&action!="floodarchive_chart")return true;
            if(FloodArchiveComplete||save.room!=2||Dying||practicing||!Has("scout_saved"))return true;
            int stage=action=="floodarchive_1"?1:action=="floodarchive_2"?2:3;
            WorldObject site=FloodArchiveObject(stage);
            if(site==null||(Position-new Point(site.x,site.y)).Length>1.8f||
                FloodArchiveProgress+1!=stage)return true;
            if(stage==1&&LowTide||stage==2&&!LowTide)
            {
                Notify("潮位已改变，需在正确的水位才能读取档案。",
                    "The tide changed. Return at the correct level to read this evidence.");
                return true;
            }
            if(stage<=2)
            {
                Flag("floodarchive_"+stage);AddXp(12);revision++;sound="reward";
                Notify("找到第 "+stage+"/2 份被抹去的民用记录，经验 +12。",
                    "Recovered civilian record "+stage+"/2. +12 XP.");
                return true;
            }
            Flag(action);
            CreditCoins(50); CreditShards(2); AddXp(80);
            revision++;sound="reward";
            RecordMoment(action=="floodarchive_public"?
                "沉桥上从未登记的家庭终于写入公共名单，摆渡人在潮标旁照顾伤者。":
                "沉桥的原始水图留给了渡船学徒，他们从此可以安全辨识湍流与航道。",
                action=="floodarchive_public"?
                "The unregistered families were placed on public rolls, with ferrymen tending the wounded.":
                "The preserved original tide charts taught new ferry pilots to read dangerous currents.");
            Notify("公共渡口重新启用！硬币 +50、碎片 +2、经验 +80。选择将持续影响当地安全补给。",
                "Public ferry restored! +50 coins, +2 shards, +80 XP. Your decision now changes local safe provisions.");
            return true;
        }
        // The existing local ferryman reacts to the same actual
        // player-published records. Pure dialogue, no extra quest/reward.
        private string FloodArchiveWayfarerNote(bool english)
        {
            if(save.room!=2)return "";
            if(FloodArchiveChoice==1)
                return english?
                    "\n\nThe families are no longer missing from the ferry register. I can finally put names beside those we treated at the crossing.":
                    "\n\n这些家庭终于不再从渡口名册上消失。我可以在救治记录旁写下真实姓名了。";
            if(FloodArchiveChoice==2)
                return english?
                    "\n\nWith your preserved flood charts, we now teach apprentices to read the current rather than trust the citadel's false cargo timetable.":
                    "\n\n你保住的原始水图已经交到学徒手里。我们现在看水流，而不再相信堡垒失真的货运时刻。";
            if(FloodArchiveProgress==2)
                return english?
                    "\n\nYou found the lost families on both tide records. Take the evidence to the public desk; their fate still needs a decision.":
                    "\n\n你从两份潮汐记录里找到了失踪家庭。请带去公共议事台，还得有人决定怎样帮助他们。";
            if(FloodArchiveProgress==1)
                return english?
                    "\n\nThe high-water gauge confirms a rescue boat passed here. At the next ebb, the missing roster may surface.":
                    "\n\n高水尺证实这里曾有救援小艇。等下一次退潮，沉下去的名册应该就能露出。";
            return "";
        }
        public bool FloodArchiveRefuge
        {
            get
            {
                if(!FloodArchiveComplete||save.room!=2)return false;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="floodarchive"&&
                        (Position-new Point(o.x,o.y)).Length<3f)return true;
                return false;
            }
        }
    }
}
