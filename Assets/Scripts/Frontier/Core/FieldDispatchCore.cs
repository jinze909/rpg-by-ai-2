using System;
using System.Collections.Generic;
namespace Starfall
{
    // A real optional three-source walking delivery, not a map-click reward.
    // Only signed completion uses existing SaveData v2 string flags.
    public sealed partial class GameCore
    {
        private static readonly int[] dispatchMachine={0,1,0,2,1,1,1,1};
        private static readonly string[] dispatchZh={
            "","断枝曾为逃难的人留下回家的路；这次要亲自沿路送回工具和药品。",
            "不要追着水面的假影走。沿旧索轮和石阶核对涨潮前的安全通道。",
            "有人在矿工名单上画下了最后的撤离路线；药箱必须真正抵达原始标记。",
            "城墙的军令已经过期。担架上的物资应该走居民的坡道，而不是哨兵关口。",
            "雾里的漂流灯会骗人。把救援物资从真正的礁锚送到岸上路标。",
            "沙丘里的高塔只是海市蜃楼。沿水尺和阴影送货，不要错过绿洲。",
            "风暴会把钟声带向悬崖。带着物资穿过背风火盆再抵达真实钟路。"
        };
        private static readonly string[] dispatchEn={
            "","Broken branches once marked a safe way home. Carry real supplies along that route.",
            "Ignore the false reflections. Check the winch and stone steps before the tide rises.",
            "A miner marked the last evacuation route. The medicine must reach the actual survey post.",
            "Military orders have expired. Carry stretcher supplies by the civilian ramp, not the guard's gate.",
            "The drifting harbor lights lie. Take relief past the true anchor and back to the real shore marker.",
            "The tower is a mirage. Trust the water gauge and shadows to carry supplies over the dunes.",
            "The storm carries bells toward the cliffs. Pass the sheltered embers and find the genuine bell road."
        };
        private int dispatchRoom=-1,dispatchStage,dispatchIntegrity;
        private float dispatchSeconds;
        // Transient scouting window after a signed physical checkpoint.
        // The player can stand and intercept the approach, set a snare,
        // or gain distance before any raider is actually spawned.
        private float dispatchAmbushWarningSeconds;
        private const float DispatchAmbushWarningDuration=3.5f;
        private const float DispatchAmbushEscapeDistance=8.5f;
        public float FieldDispatchAmbushWarningSeconds
        {get{return FieldDispatchActive&&dispatchStage==2?
            Math.Max(0f,dispatchAmbushWarningSeconds):0f;}}
        public Point FieldDispatchAmbushOrigin
        {get{return FieldDispatchActive?
            new Point(dispatchCheckpoint.x,dispatchCheckpoint.y):Position;}}
        // Temporary local ambushers make the second physical leg a tactical
        // choice (fight, snare or evade), not an empty timer. They never
        // survive a failed/signed dispatch or serialize to a save.
        private readonly List<Enemy> dispatchRaiders=new List<Enemy>();
        public int FieldDispatchLivingRaiders
        {get{int n=0;if(FieldDispatchActive)foreach(Enemy e in dispatchRaiders)
            if(e!=null&&e.hp>0f&&enemies.Contains(e))n++;return n;}}
        private bool IsFieldDispatchEnemy(Enemy e)
        {return e!=null&&FieldDispatchActive&&dispatchRaiders.Contains(e);}
        // The world actor renderer uses this exact reference, not an enemy
        // kind or reused numeric ID; fake/retired raiders never get a quest cue.
        public bool IsLiveFieldDispatchRaider(Enemy e)
        {return IsFieldDispatchEnemy(e)&&e.hp>0f&&enemies.Contains(e);}
        // Rank genuine connected approaches rather than taking the first
        // north-edge tile found by the scan. A bow gets a physical firing
        // lane; a brawler may exploit nearby cover. The optional second
        // raider should pressure a different approach, not stack behind
        // the first. No actor is created until the whole selection ends.
        private float FieldDispatchPadScore(Point foot,int kind)
        {
            float range=(foot-Position).Length;
            float preferred=kind==1?8f:kind==2?7.2f:6.8f;
            float score=-(float)Math.Abs(range-preferred)*2f;
            bool sight=ClearAttackPath(foot,Position);
            if(kind==1)
                score+=sight?
                    (PhysicalBowLaneFromFoot(foot,Position)?12f:-6f):-9f;
            else if(sight)score+=2f;
            return score;
        }
        private int SpawnFieldDispatchAmbush()
        {
            if(!FieldDispatchActive||dispatchStage!=2)return 0;
            Point center=new Point(dispatchCheckpoint.x,dispatchCheckpoint.y);
            int sx=(int)Math.Floor(center.x),sy=(int)Math.Floor(center.y);
            int count=dispatchRoom==2||dispatchRoom==3||
                dispatchRoom==6?2:1;
            var candidates=new List<Point>();
            var none=new List<Point>(0);
            // Preserve the authentic route, NPC pads and distance limits
            // of the original courier encounter, including the fallback
            // to one raider where the actual terrain cannot fit a pair.
            for(int radius=7;radius<=12;radius++)
            for(int dy=-radius;dy<=radius;dy++)
            for(int dx=-radius;dx<=radius;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                    Math.Abs(dx)+Math.Abs(dy)>16)continue;
                Point foot=new Point(sx+dx+.5f,sy+dy+.5f);
                if(RegionalScoutFootClear(foot,center,true,none))
                    candidates.Add(foot);
            }
            if(candidates.Count==0)return 0;
            int firstKind=dispatchRoom%3,secondKind=(dispatchRoom+1)%3;
            // The two-role ranking must not raycast for every possible
            // pair: candidate visibility and bow clearance are fixed for
            // this snapshot of the same signed physical checkpoint.
            var firstScores=new List<float>(candidates.Count);
            var secondScores=new List<float>(candidates.Count);
            var headings=new List<Point>(candidates.Count);
            int firstIndex=-1,secondIndex=-1;
            float bestOne=float.NegativeInfinity;
            for(int a=0;a<candidates.Count;a++)
            {
                Point foot=candidates[a];
                float score=FieldDispatchPadScore(foot,firstKind);
                firstScores.Add(score);
                secondScores.Add(count==2?
                    FieldDispatchPadScore(foot,secondKind):0f);
                headings.Add((foot-Position).Normalized);
                if(firstIndex<0||score>bestOne+.0001f)
                {firstIndex=a;bestOne=score;}
            }
            // Evaluate both roles jointly so a tempting lone bow perch
            // cannot prevent the full pair from reaching the courier.
            if(count==2)
            {
                float bestPair=float.NegativeInfinity;
                bool foundPair=false;
                for(int a=0;a<candidates.Count;a++)
                for(int b=0;b<candidates.Count;b++)
                {
                    Point first=candidates[a],second=candidates[b];
                    if((first-second).Length<5f)continue;
                    Point f=headings[a],t=headings[b];
                    float spread=1f-f.x*t.x-f.y*t.y;
                    float score=firstScores[a]+secondScores[b]+spread*9f;
                    if(!foundPair||score>bestPair+.0001f)
                    {
                        firstIndex=a;secondIndex=b;
                        bestPair=score;foundPair=true;
                    }
                }
            }
            var pads=new List<Point>(2){candidates[firstIndex]};
            if(secondIndex>=0)pads.Add(candidates[secondIndex]);
            foreach(Point foot in pads)
            {
                int kind=(dispatchRoom+dispatchRaiders.Count)%3;
                Enemy foe=CreatePlacedEnemy(++enemyId,kind,foot,
                    35f+dispatchRoom*7f);
                dispatchRaiders.Add(foe);enemies.Add(foe);
                effects.Add(new Effect("enemyRelease",foot,.65f){variant=kind});
            }
            return dispatchRaiders.Count;
        }
        private MapData dispatchMap;
        private SaveData dispatchSave;
        private WorldObject dispatchOrigin,dispatchCheckpoint,dispatchFinal;
        public bool FieldDispatchActive
        {get{return dispatchStage>0&&dispatchRoom>=1&&dispatchRoom<=7&&
            save.room==dispatchRoom&&Object.ReferenceEquals(dispatchMap,Map)&&
            Object.ReferenceEquals(dispatchSave,save)&&
            dispatchOrigin!=null&&dispatchCheckpoint!=null&&dispatchFinal!=null&&
            SourceBelongsToActiveMap(dispatchOrigin)&&
            SourceBelongsToActiveMap(dispatchCheckpoint)&&
            SourceBelongsToActiveMap(dispatchFinal);}}
        public bool FieldDispatchDone(int room)
        {return room>=1&&room<=7&&FieldOutpostBuilt(room)&&
            Has("field_dispatch_done_"+room);}
        public int FieldDispatchWins
        {get{int n=0;for(int i=1;i<=7;i++)if(FieldDispatchDone(i))n++;return n;}}
        public int FieldDispatchStage {get{return FieldDispatchActive?dispatchStage:0;}}
        public int FieldDispatchIntegrity {get{return FieldDispatchActive?dispatchIntegrity:0;}}
        public float FieldDispatchSeconds {get{return FieldDispatchActive?dispatchSeconds:0f;}}
        public Point FieldDispatchTarget
        {get{
            WorldObject o=FieldDispatchActive?
                (dispatchStage==1?dispatchCheckpoint:dispatchFinal):null;
            return o==null?Position:new Point(o.x,o.y);
        }}
        public bool FieldDispatchIsTarget(WorldObject o)
        {return FieldDispatchActive&&o!=null&&Object.ReferenceEquals(o,
            dispatchStage==1?dispatchCheckpoint:dispatchFinal);}
        private void ResetFieldDispatch()
        {
            projectiles.RemoveAll(p=>p.hostileShooter!=null&&
                dispatchRaiders.Contains(p.hostileShooter));
            foreach(Enemy e in dispatchRaiders)
            {
                RemoveEnemyPendingWarning(e);
                enemies.Remove(e);
            }
            dispatchRaiders.Clear();
            dispatchRoom=-1;dispatchStage=dispatchIntegrity=0;
            dispatchSeconds=0f;dispatchAmbushWarningSeconds=0f;
            dispatchMap=null;dispatchSave=null;
            dispatchOrigin=dispatchCheckpoint=dispatchFinal=null;
        }
        private void TickFieldDispatch(float dt)
        {
            if(dispatchStage==0)return;
            if(!FieldDispatchActive||Dying){ResetFieldDispatch();return;}
            dispatchSeconds=Math.Max(0,dispatchSeconds-dt);
            if(dispatchSeconds<=0f)
            {
                ResetFieldDispatch();revision++;sound="alert";
                Notify("限时补给接力失败，已消耗的物资没有得到签收。",
                    "Courier timed out. Spent supplies cannot become a receipt.");
                return;
            }
            if(dispatchStage!=2||dispatchAmbushWarningSeconds<=0f||dt<=0f)
                return;
            // Fleeing uses real navigable footing, not a dialogue click or
            // a remote receipt. A courier who reaches cover or a side road
            // before the attackers arrive has actually avoided this fight,
            // but still must finish the full paid two-leg delivery on time.
            Point checkpoint=new Point(dispatchCheckpoint.x,dispatchCheckpoint.y);
            if(CanStand(Position)&&
                (Position-checkpoint).Length>DispatchAmbushEscapeDistance)
            {
                dispatchAmbushWarningSeconds=0f;
                revision++;sound="quest";
                Notify("你趁伏击者尚未合围离开中继站。货物仍须送抵原调查石。",
                    "You slipped past the raiders before their encirclement. The cargo still needs delivery.");
                return;
            }
            dispatchAmbushWarningSeconds=Math.Max(0f,
                dispatchAmbushWarningSeconds-dt);
            if(dispatchAmbushWarningSeconds>0f)return;
            int raiders=SpawnFieldDispatchAmbush();
            if(raiders>0)
            {
                // Even a successfully ambushing squad has a visible reaction
                // beat before its first attack; snare and dodge remain viable.
                foreach(Enemy foe in dispatchRaiders)
                    foe.stun=Math.Max(foe.stun,.65f);
                revision++;sound="alert";
                Notify("有人从道路两侧逼近！"+raiders+"名拦路者进入战斗，可战斗、活捉或绕开。",
                    raiders+" raiders emerge from the roadside! Fight, snare, or evade.");
            }
            else
            {
                revision++;
                Notify("脚步声渐远，袭击者没有找到可通行的接近道路。",
                    "Footsteps fade; the raiders found no navigable approach.");
            }
        }
        private void DamageFieldDispatchCargo(float damage)
        {
            if(!FieldDispatchActive||!Finite(damage)||damage<=0f)return;
            dispatchIntegrity--;
            if(dispatchIntegrity<=0)
            {
                ResetFieldDispatch();revision++;
                Notify("货物受真实打击损坏，接力失败。","Real hits destroyed the cargo. Courier failed.");
            }
            else
            {
                revision++;
                Notify("货物受损，剩余完整度"+dispatchIntegrity+"。",
                    "Cargo damaged. Integrity "+dispatchIntegrity+" remains.");
            }
        }
        private WorldObject DispatchFind(string id,string kind)
        {
            foreach(WorldObject o in Map.objects)
                if(o!=null&&o.id==id&&o.kind==kind)return o;
            return null;
        }
        private bool TalkFieldDispatch(WorldObject o)
        {
            if(!FieldDispatchActive||o==null||
                !SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position))return false;
            if(dispatchStage==1&&Object.ReferenceEquals(o,dispatchCheckpoint))
            {
                Dialog(o.name,o.en,dispatchZh[save.room]+
                    "\n\n在这处真实工程装置登记物资，再步行去原探路石。剩余"+
                    (int)dispatchSeconds+"秒，货物完整度"+dispatchIntegrity+"。",
                    dispatchEn[save.room]+
                    "\n\nRegister cargo at this actual machine, then walk to the original survey post. "+
                    (int)dispatchSeconds+"s left; integrity "+dispatchIntegrity+".",
                    C("登记真实中继站","Record this physical waypoint","field_dispatch_checkpoint"),
                    C("稍后再来","Not yet","close"));
                return true;
            }
            if(dispatchStage==2&&Object.ReferenceEquals(o,dispatchFinal))
            {
                Dialog(o.name,o.en,dispatchZh[save.room]+
                    "\n\n物资抵达真实的起始调查地标。登记后该地区前线设施将永久改善。",
                    dispatchEn[save.room]+
                    "\n\nThe supplies reached the original survey post. Signing this genuine delivery permanently strengthens the regional outpost.",
                    C("现场交货并签收","Deliver and sign actual cargo","field_dispatch_finish"),
                    C("再检查一遍","Check the area first","close"));
                return true;
            }
            return false;
        }
        private bool ChooseFieldDispatch(string action)
        {
            if(action!="field_dispatch_start"&&action!="field_dispatch_abort"&&
               action!="field_dispatch_checkpoint"&&action!="field_dispatch_finish")
                return false;
            int room=save.room;
            WorldObject source=pendingDialogueSource;
            if(room<1||room>7||mode!="world"||Dying||practicing||racing||
               source==null||pendingDialogueSourceRoom!=room||
               !SourceBelongsToActiveMap(source)||
               !HasPhysicalInteractionReach(source)||!CanStand(Position))return true;
            if(action=="field_dispatch_start")
            {
                if(source.kind!="fieldKit"||source.id!="fieldKit"+room||
                   !FieldOutpostBuilt(room)||FieldDispatchDone(room)||
                   FieldWorkPreventsRest||!SafeTravel||
                   BlockTravelForProtectedFieldMission(true))return true;
                WorldObject middle=DispatchFind(
                    "regional_machine_"+room+"_"+dispatchMachine[room],"regionalMachine");
                WorldObject end=DispatchFind("biomeSurvey"+room,"biomeSurvey");
                if(middle==null||end==null||
                   !CanStand(new Point(middle.x,middle.y))||
                   !CanStand(new Point(end.x,end.y))||
                   NavigationPathFrom(Position,new Point(middle.x,middle.y),true).Count==0||
                   NavigationPathFrom(new Point(middle.x,middle.y),
                       new Point(end.x,end.y),true).Count==0)
                {
                    Notify("工程装置间的真实道路不通，不能发货，也不收费。",
                        "The physical courier route is disconnected. No cargo charged.");
                    return true;
                }
                bool ward=FieldOutpostWard(room);
                int item=ward?2:4;
                if(save.inventory[item]<1||save.energy<12f)
                {
                    Notify(ward?"需要月光草1份和12点耐力。":"需要机械零件1份和12点耐力。",
                        ward?"Courier costs 1 Moonherb and 12 stamina.":
                             "Courier costs 1 spare part and 12 stamina.");
                    return true;
                }
                save.inventory[item]--;save.energy-=12f;
                dispatchRoom=room;dispatchMap=Map;dispatchSave=save;
                dispatchOrigin=source;dispatchCheckpoint=middle;
                dispatchFinal=end;dispatchStage=1;dispatchIntegrity=3;
                dispatchSeconds=room==2||room==5?300f:260f;
                dispatchAmbushWarningSeconds=0f;
                revision++;sound="quest";
                Notify("补给接力开始。先沿真实道路抵达"+middle.name+
                    "，再走到最初调查石；受伤会损坏货物，不能使用捷径。",
                    "Courier started. Walk to "+middle.en+
                    ", then to the original survey post. Real hits damage cargo.");
                return true;
            }
            if(action=="field_dispatch_abort")
            {
                if(!FieldDispatchActive||
                   !Object.ReferenceEquals(source,dispatchOrigin))return true;
                ResetFieldDispatch();revision++;
                Notify("运送已撤回，原料不会凭空退回或变成结算奖励。",
                    "Courier withdrawn. Spent materials grant no receipt.");
                return true;
            }
            if(!FieldDispatchActive||dispatchSeconds<=0f)return true;
            if(action=="field_dispatch_checkpoint")
            {
                if(dispatchStage!=1||
                    !Object.ReferenceEquals(source,dispatchCheckpoint))return true;
                dispatchStage=2;
                dispatchAmbushWarningSeconds=DispatchAmbushWarningDuration;
                revision++;sound="alert";
                Notify("第一站已登记。附近传来呼哨声；伏击者正在接近！可以提前设陷阱或立刻离开中继站。继续前往原调查石。",
                    "Checkpoint signed. You hear ambush whistles: set a snare or move away before the raiders arrive. Deliver to the original survey post.");
                return true;
            }
            if(dispatchStage!=2||!Object.ReferenceEquals(source,dispatchFinal)||
               FieldDispatchDone(room))return true;
            bool perfect=dispatchIntegrity==3;
            ResetFieldDispatch();
            Flag("field_dispatch_done_"+room);
            if(perfect)Flag("field_dispatch_intact_"+room);
            CreditCoins(20+room*3);CreditShards(perfect?2:1);
            AddXp(65+room*8);
            RecordMoment(dispatchZh[room]+
                " 货物经真实工程装置抵达旧探路石，公开道路终于有了可核实的交接记录。",
                dispatchEn[room]+
                " Supplies traveled through the physical mechanism and original survey post. The public route has its first signed handoff.");
            revision++;sound="reward";
            Notify("民间接力完成！本区医疗或工坊补给永久改善，返程路费下降。"+
                (perfect?"货物完好，额外获得1碎片。":""),
                "Courier completed! Local supplies and return fare improve permanently."+
                (perfect?" Intact cargo earns an extra shard.":""));
            return true;
        }
    }
}
