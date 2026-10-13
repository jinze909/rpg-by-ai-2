using System;

namespace Starfall
{
    // Tidebound Manifest is a genuine tide-window retrieval expedition.
    // Progress, live pursuers and pressure timers never enter v2 save data.
    public sealed partial class GameCore
    {
        public int TideSalvagePhase { get; private set; }
        public float TideSalvageTime { get; private set; }
        private bool tideSalvageHoldUsed;
        public bool TideSalvageActive { get { return TideSalvagePhase > 0; } }
        public bool IsTideSalvageEnemy(Enemy e)
        { return e != null && e.id >= 89920 && e.id < 89930; }

        private Point TideSalvageSite(int site)
        { return ObjectPoint(5,"tidecargo"+site,new Point(-100,-100)); }

        private void ResetTideSalvage()
        {
            enemies.RemoveAll(IsTideSalvageEnemy);
            projectiles.RemoveAll(p => p.tideSalvageOwned);
            effects.RemoveAll(e => e.tideSalvageOwned);
            TideSalvagePhase=0;TideSalvageTime=0;tideSalvageHoldUsed=false;
        }
        private void TickTideSalvage(float dt)
        {
            if(!TideSalvageActive)return;
            if(save.room!=5 || Dying || practicing)
            {ResetTideSalvage();revision++;return;}
            TideSalvageTime-=dt;
            if(TideSalvageTime<=0)
            {
                ResetTideSalvage();revision++;
                Notify("潮滩航道已经关闭。货物尚未交付，可以重新出发。",
                    "The shoal route closed. Undelivered cargo was lost; retry from the dock.");
            }
        }
        private bool SpawnTideSalvageRaiders(int stage)
        {
            // Validate all actor feet before mutating the world, so a failed
            // spawn cannot leave half a wave alive or hand out free credit.
            Point[] places=stage==2?
                new[]{new Point(11.5f,12.5f),new Point(13.5f,11.5f)}:
                new[]{new Point(28.5f,18.5f)};
            foreach(Point p in places)
                if(!CanStand(p) || NavigationPath(p).Count==0)
                    return false;
            for(int i=0;i<places.Length;i++)
            {
                int kind=stage==2?i:2;
                Enemy foe=new Enemy(89920+stage*2+i,kind,
                    places[i].x,places[i].y,stage==3?125:stage==2?75:50);
                foe.timer=.85f+i*.25f;
                enemies.Add(foe);
            }
            return true;
        }

        private bool TalkTideSalvage(WorldObject o)
        {
            if(o.kind=="tidedock")
            {
                if(save.room!=5)return true;
                if(!Has("harbor_report"))
                {
                    Dialog(o.name,o.en,
                        "旧渡船还有一批失踪的民用物资，但海兽仍在守着灯塔。先调查潮汐事故并将记录交给米拉。",
                        "Civilian cargo from the old ferry is still missing. Investigate the tide disaster and report to Mira first.",
                        C("稍后回来","Return later","close"));return true;
                }
                if(Has("tide_salvage_complete"))
                {
                    Dialog(o.name,o.en,
                        "三份失踪货单已经公开，物资回到居民手中。潮汐观测站也更新了模型，每次固定低潮额外延长十五秒。",
                        "All missing cargo manifests are public. The recovered tide model adds fifteen seconds to every Tidewatch low-tide hold.",
                        C("继续出航","Sail safely","close"));return true;
                }
                if(!TideSalvageActive)
                {
                    Dialog(o.name,o.en,
                        "我是码头的托兰。西侧两处潮滩和南侧孤岛藏着撤离物资。只有低潮才能拾取，水位每36秒循环一次，打捞行动限时100秒。找齐三份并击退掠夺者再回码头，我会交出真正的潮汐模型。失败可重新出航。",
                        "I'm Tolan of the ferry dock. Two western shoals and a southern island hide evacuation cargo. Retrieve each only at low tide; the tide cycles every 36 seconds. Return with all three within 100 seconds and defeat any raiders to recover the true tide model. Retries are free.",
                        C(LowTide?"低潮出航 · 100秒":"等待低潮再出航",
                            LowTide?"Launch at low tide · 100s":"Wait for low tide",
                            LowTide?"tide_salvage_start":"close"),
                        C("追踪潮滩货单","Track the manifest","tide_salvage_track"),
                        C("暂不行动","Not now","close"));return true;
                }
                bool delivered=TideSalvagePhase==4 &&
                    !enemies.Exists(IsTideSalvageEnemy);
                Dialog(o.name,o.en,
                    "打捞进度 "+(TideSalvagePhase-1)+"/3；还剩 "+
                        TideSalvageTime.ToString("0")+"秒。"+
                        (enemies.Exists(IsTideSalvageEnemy)?"还有掠夺者追来。":"货物必须亲自交回码头。"),
                    "Recovered "+(TideSalvagePhase-1)+"/3; "+
                        TideSalvageTime.ToString("0")+" seconds remain. "+
                        (enemies.Exists(IsTideSalvageEnemy)?"Raiders still pursue you.":"Bring the cargo back to the dock."),
                    C(delivered?"交回货物并公开潮汐模型":"继续打捞",
                        delivered?"Deliver cargo and publish tide model":"Continue salvage",
                        delivered?"tide_salvage_finish":"close"),
                    C(!tideSalvageHoldUsed && save.shards>0?
                        "星晶碎片×1 · 紧急稳潮18秒":"检查潮汐",
                        !tideSalvageHoldUsed && save.shards>0?
                        "Spend 1 shard · hold tide 18s":"Check tide",
                        !tideSalvageHoldUsed && save.shards>0?
                        "tide_salvage_hold":"close"),
                    C("放弃打捞","Abandon salvage","tide_salvage_abandon"));
                return true;
            }
            if(!o.kind.StartsWith("tidecargo"))return false;
            int site;
            if(!int.TryParse(o.kind.Substring("tidecargo".Length),out site) ||
                site<1 || site>3 || save.room!=5)return true;
            if(!Has("harbor_report"))
            {Notify("先完成雾港的原始水位调查。","Finish Mistport's original tide report first.");return true;}
            if(!TideSalvageActive)
            {Notify("货物还未登记。先到渡船码头领取打捞委托。","Register the salvage at the ferry dock first.");return true;}
            if(site<TideSalvagePhase)
            {
                Notify("这份货单已经打捞。","This manifest has already been retrieved.");
                return true;
            }
            if(site!=TideSalvagePhase)
            {
                Notify("还缺前一处潮滩记录。按手帐顺序调查。",
                    "An earlier shoal reading is missing. Follow the journal route.");
                return true;
            }
            if(!LowTide)
            {
                Notify("高潮淹没了货箱。等待退潮或从码头稳定低潮。",
                    "High tide submerged the crate. Wait for the ebb or stabilize the tide at the dock.");
                return true;
            }
            Dialog(o.name,o.en,
                site==1?"破裂的旧船柜里是药品领取名单，整整一栏被改写成货运损耗。":
                site==2?"第二份货单表明，灯塔仍在替没有归来的船只扣留粮食。":
                         "最后的岛上货箱保存着原始潮汐模型和一条公开的避险航道。",
                site==1?"The first chest holds a medicine distribution list, recoded as freight loss.":
                site==2?"The second log shows the lighthouse still withholding food for vessels that never returned.":
                         "The final island crate preserves the true tide model and a public safe passage.",
                C("收回货单","Recover the manifest","tide_salvage_take_"+site),
                C("先观察潮水","Watch the tide","close"));
            return true;
        }
        private bool ChooseTideSalvage(string action)
        {
            if(action=="tide_salvage_track")
            {
                if(Has("harbor_report"))TrackQuest("tide_salvage");
                return true;
            }
            if(action=="tide_salvage_start")
            {
                if(save.room!=5 || !Has("harbor_report") ||
                    Has("tide_salvage_complete") || TideSalvageActive ||
                    Dying || practicing || racing || reliefMissionRoom!=0 ||
                    GlasswindSite!=0 || commonsRelayRoom!=0 ||
                    !LowTide || !SafeTravel ||
                    (Position-ObjectPoint(5,"tidedock",new Point(-100,-100))).Length>2.2f)
                    return true;
                TideSalvagePhase=1;TideSalvageTime=100;tideSalvageHoldUsed=false;
                TrackQuest("tide_salvage");revision++;sound="quest";
                Notify("已出航！在100秒内找到西侧、内湾、南岛三份货单，避开潮水与掠夺者。",
                    "Salvage launched! Recover three ordered shoal manifests in 100 seconds; beware tides and raiders.");
                return true;
            }
            if(action=="tide_salvage_hold")
            {
                if(!TideSalvageActive || save.room!=5 ||
                    tideSalvageHoldUsed || save.shards<1 ||
                    (Position-ObjectPoint(5,"tidedock",new Point(-100,-100))).Length>2.2f)
                    return true;
                save.shards--;tideSalvageHoldUsed=true;
                save.tideHeldUntil=Math.Max(save.tideHeldUntil,save.totalTime+18f);
                revision++;sound="guard";
                Notify("支付1碎片。低潮暂时稳定18秒，此次出航不可重复购买。",
                    "Spent 1 shard. Low tide held for 18 seconds, once per sortie.");
                return true;
            }
            if(action.StartsWith("tide_salvage_take_"))
            {
                int site;
                if(!int.TryParse(action.Substring("tide_salvage_take_".Length),out site) ||
                    site!=TideSalvagePhase || site<1 || site>3 ||
                    save.room!=5 || Dying || !LowTide ||
                    (Position-TideSalvageSite(site)).Length>2.2f)
                    return true;
                // A new wave must be deployable before advancing a cargo
                // checkpoint: spawning failure never grants a free stage.
                if(site>=2 && !SpawnTideSalvageRaiders(site))
                {
                    Notify("掠夺者来路受阻，请在潮滩另一侧靠近后重试。",
                        "No reachable raider approach. Reposition and retry this crate.");
                    return true;
                }
                TideSalvagePhase++;
                effects.Add(new Effect("discover",Position,.65f));
                revision++;sound="quest";
                Notify(site==3?"全部货单已取回！击退掠夺者，返回码头交付。":
                    "货单收回！继续下一处潮滩，小心掠夺者。",
                    site==3?"All cargo secured! Clear the raiders and return to the dock.":
                    "Manifest recovered! Continue to the next shoal; watch for raiders.");
                return true;
            }
            if(action=="tide_salvage_abandon")
            {
                if(!TideSalvageActive || save.room!=5 ||
                    (Position-ObjectPoint(5,"tidedock",new Point(-100,-100))).Length>2.2f)
                    return true;
                ResetTideSalvage();revision++;
                Notify("打捞已取消。未交付的货物不算完成，可重新出航。",
                    "Salvage canceled. Undelivered cargo does not count; retry freely.");
                return true;
            }
            if(action=="tide_salvage_finish")
            {
                if(save.room!=5 || TideSalvagePhase!=4 || Dying ||
                    enemies.Exists(IsTideSalvageEnemy) || Has("tide_salvage_complete") ||
                    (Position-ObjectPoint(5,"tidedock",new Point(-100,-100))).Length>2.2f)
                    return true;
                ResetTideSalvage();
                Flag("tide_salvage_complete");
                save.coins+=60;save.shards+=3;save.skillPoints++;
                AddXp(75);revision++;sound="reward";
                RecordMoment("潮缚货单公开：医药、粮食、航道不再被算作报废货损。",
                    "Tidebound Manifest published: medicine, food and safe passage no longer count as lost freight.");
                Notify("潮缚货单完成！60硬币、3碎片、1天赋点、75经验；稳潮时长永久+15秒。",
                    "Tidebound Manifest complete! +60 coins, +3 shards, +1 talent, +75 XP; Tidewatch holds +15s.");
                return true;
            }
            return false;
        }
    }
}
