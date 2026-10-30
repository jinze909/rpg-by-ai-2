using System;
using System.Collections.Generic;
namespace Starfall
{
    // Four playable interludes embedded in the original 1–4 chapter maps.
    // They are independently optional: established gates, guardians,
    // endings and old v1/v2 save files NEVER require these new flags.
    // A signed original-world handoff (not a journal click) is the only payout.
    public sealed partial class GameCore
    {
        private static readonly string[] chapterHeadZh={"",
            "第一章 · 苔根之下","第二章 · 涨潮前的渡口",
            "第三章 · 最后一次停机","第四章 · 城门外的名字"};
        private static readonly string[] chapterHeadEn={"",
            "Chapter I · Beneath the Mossroots","Chapter II · Before the Flood",
            "Chapter III · The Last Shutdown","Chapter IV · Names Beyond the Wall"};
        private static readonly string[] chapterOpeningZh={"",
            "公会说侦察员失踪了。可巡林人更害怕另一件事：求救铃被割断，通往草药棚的桥上还有伤者。把铃声接起来，然后亲自穿过林道。",
            "渡口的登记牌上只剩几道被水冲淡的笔画。昨天三名渡工还在值夜，今天他们的名字被当作货物损耗抹掉。赶在潮水重新漫过石阶前找到他们。",
            "矿工们在停工单上按满了手印，监工却没有减压。沿着热管找到最后的接收点，让人先撤出车轨，再去找驱动机器的星核。",
            "城墙下的平民把名字写在布上，守军却要他们撤掉。带着这些声音穿过城墙外围，回到真正的居民证据墙：这不是军令，是求活的请求。"};
        private static readonly string[] chapterOpeningEn={"",
            "The guild reports a missing scout, but the roadwardens fear something else: the rescue bell was cut. Wounded herbalists are stranded beyond the mossroot crossing. Restore the route before seeking the core.",
            "Three ferry workers stood watch yesterday. Their names have since been erased from the loss ledger. Follow the old stairs and find their testimony before the tide turns.",
            "Miners signed a stop-work petition. The overseer kept the boilers running. Follow the burning manifold, rescue the workers and then challenge the machine drawing power from the core.",
            "Civilians pinned their names beneath the citadel wall. The military ordered the notices removed. Bring their voices along the outer road to a place that cannot erase them."};
        private static readonly string[] chapterMiddleZh={"",
            "地上的救援铃绳还带着树脂。有人故意剪断它；伤者和救援者都被隔在了另一头。先挡住追来的劫掠者。",
            "水闸边的绳索缠着三块姓名木牌。涨潮时冰水会夺走体温；别守着深水不动。挡住巡逻者，保住证词。",
            "压力管传来沉闷的敲击声。喷出的热气会烧灼靠近阀门的人。边躲开高温区边挡住雇佣兵，先让伤者撤走。",
            "城墙把居民分成有名册和没名册的两边。卫兵正在扯掉最后一盏撤离灯；不让他们把证据带走。"};
        private static readonly string[] chapterMiddleEn={"",
            "Resin still clings to the severed bell rope. The cut was deliberate. Raiders are now closing on both the stranded patients and the wardens.",
            "Three nameplates are tangled in the sluice rope. Incoming high tide chills anyone fighting in the flooded lane. Keep moving and protect the names.",
            "The pressure pipe knocks from within. Scalding steam builds near the manifold. Leave the hot zone between strikes and clear the workers' escape.",
            "The wall separates listed citizens from the uncounted. Soldiers are tearing down the final evacuation lantern; stop them from taking the evidence."};
        private static readonly string[] chapterEndZh={"",
            "树洞里有两副被鲜血浸透的绷带。巡林员用重新接通的铃告诉镇里有人活着回来。这条路以后仍然有人走。",
            "幸存渡工把自己的名字写回了点名册。水位仍在涨，可渡口终于不再把人算成损坏货物。",
            "最后一辆矿车拖着伤者离开。工棚将停工单贴到门口，明天若有人再次开机，他们至少知道是谁发出的命令。",
            "居民证据墙上钉好了每个名字。即使城内的灯仍在亮，城墙外也有人知道他们不是一串数字。"};
        private static readonly string[] chapterEndEn={"",
            "Two bloodied bandages lie by the roots. A repaired bell tells the town that wounded wardens made it home. The road will be needed again.",
            "Ferry survivors write their own names back into the roll. The tide still rises, but the crossing no longer counts people as broken cargo.",
            "The last minecart carries the wounded away. Workers pin the shutdown order to the shed, so the next person who starts the machinery cannot claim ignorance.",
            "Every civilian name is now pinned to the evidence wall. While the citadel lights still burn, people outside its walls know they have not been reduced to numbers."};
        // Human voices at the three actual walkable rescue sites. These
        // scenes seed the older outer-ring conspiracy WITHOUT spoiling its
        // later witnesses, and keep the original physical rescue gameplay.
        private static readonly string[] chapterOpeningWitnessZh={"",
            "求援牌下面夹着一张沾泥的儿童画：桥的这一边画着草药棚，那一边画着一个举铃的人。纸角写着：“我们不怕森林，我们怕再也听不见有人来。”\n\n巡林人说，侦察员失踪前坚持去找那口被军需队拆下的铃。他本可以带着星核情报直接回镇，却回头寻找伤者。",
            "被水浸皱的值夜簿上，最后一个签名属于阿菱的弟弟。他不是士兵，只是每晚把木板钉回渡口的学徒。官署在签名上盖了“已损耗”，没有通知他的家人。\n\n你听见有人在半塌的楼梯下喊：“别只救名册上还活着的人。”",
            "停工单上压着一副裂开的护目镜。维恩原本想把它还给徒弟，但整夜没人来领取。矿机继续轰响，他每小时都在表格上写一遍“有人尚未撤离”。\n\n监工却只问产量有没有下降。你要找的不止是星核，还有谁有权让机器停下来。",
            "这张求援布是用几件旧军衣缝的。墙内的孩子写“我们需要灯”，墙外的孩子写“我们需要水”。两边用了同一种蓝色墨水，却被军官撕成两份。\n\n留下它的居民不想让你替他们选一边，只希望有人把整幅布带回证据墙。"};
        private static readonly string[] chapterOpeningWitnessEn={"",
            "Beneath the distress post lies a child's muddy drawing: a herb shelter on one side of the bridge, a person with a bell on the other. 'We aren't afraid of the forest,' it says. 'We're afraid nobody will come.'\n\nThe scout went back to find the bell removed by military requisition. He could have carried the core report home. Instead he turned around for the wounded.",
            "The last name in the soaked night ledger belongs to Arin's younger brother. He was no soldier, only an apprentice who replaced the ferry planks each evening. An official stamp marked him 'written off'. Nobody told his family.\n\nSomeone calls from beneath the collapsed steps: 'Don't only rescue the people still counted as alive.'",
            "A cracked pair of goggles weighs down Venn's stop-work petition. He meant to return them to his apprentice, but nobody came. All night he wrote 'workers still inside' on the form while the mining engine roared.\n\nThe overseer asked only whether production had fallen. You are seeking not just a core but the right to stop a machine.",
            "The distress cloth was sewn from old uniforms. Children inside the wall wrote 'we need light'; children outside wrote 'we need water'. They used the same blue ink. An officer tore their words apart.\n\nThe people who left it do not ask you to choose a side for them. They ask you to bring both halves to the evidence wall."};
        private static readonly string[] chapterMidWitnessZh={"",
            "草药棚的门被人从外面用藤蔓缠住。伤者用颤抖的声音说，先前来的守卫把救援铃当成违令的噪音。你现在挡下的劫掠者不过是趁着无人应答而来。\n\n若让铃重新响起，今后任何孩子都能知道这条路仍有人守。",
            "石阶旁有一只上锁的铁箱，里面不是金银，而是渡工家属写下的名字。河水涨得比预报快，是因为有人在上游优先给堡垒蓄能。\n\n幸存者求你把名字带出去，即使没人能保证下一次退潮一定准时。",
            "热管下躲着一名学徒。他悄声说监工也有孩子在堡垒里，所以害怕停机就会让城内断光。但学徒举起满是烫伤的手：“那我们算谁的孩子？”\n\n真正的对手不只是眼前的佣兵，还有那份没有任何工人签过字的供能命令。",
            "守卫中有人认识证据墙上的名字，仍奉命拆下撤离灯。他说若屏障熄灭，城内的小孩会冻死。墙外一名老妇回答：“我们早就一直在冷里。”\n\n你不能用一次胜利来消除两边的恐惧，但可以先阻止今天的驱逐。"};
        private static readonly string[] chapterMidWitnessEn={"",
            "Vines bind the herb-shelter door from outside. A wounded patient says guards called the rescue bell an unlawful disturbance. Raiders arrived only after nobody answered.\n\nIf the bell rings again, the next child on this road will know someone is listening.",
            "A locked iron box beside the stairs holds no treasure: only names written by ferry families. The water rose ahead of the forecast because upstream storage prioritized the citadel shield.\n\nSurvivors ask you to carry their names out, even if nobody can promise the next tide will arrive on time.",
            "An apprentice hides beneath the hot manifold. The foreman has children inside the citadel and fears that stopping the engine will put out their lights. The apprentice raises his burned hands: 'Then whose children are we?'\n\nYour enemy is not only these hired guards. It is an energy order no miner was allowed to sign.",
            "One guard recognizes the names on the wall but obeys orders to tear down the evacuation lamp. He says the shield keeps children alive inside. An old woman beyond the wall answers, 'We've been cold for years.'\n\nOne battle cannot erase both fears. But you can stop today's expulsion."};
        private static readonly string[] chapterClosingWitnessZh={"",
            "获救的巡林人没有感谢你“杀死怪物”。他把已经修好的铜铃放到草药棚前，问你明天是否还有人能听见它。你意识到，森林真正需要的不是一次胜利，而是一条会有人反复走过的路。",
            "阿菱把名字交给活着的渡工自己抄写。她问你：“等堡垒重新要求供能，我们还会不会被擦掉？”你无法许下永不涨潮的诺言，却能决定要不要让那张名单公开留存。",
            "矿工把写满停工要求的布贴在机器门口，没有人立刻欢呼。明天还有工资、灯光和食物的问题要解决。维恩说：“但今天开始，我们至少可以自己说什么时候危险。”",
            "居民把破成两半的蓝字布重新缝好。墙内的孩子站在墙外的老妇身边，没有人要求对方撤回自己的愿望。你带走的证据没有提供简单答案，却让厄洛以后再也不能只听见半句话。"};
        private static readonly string[] chapterClosingWitnessEn={"",
            "The rescued warden does not thank you for 'killing monsters'. He puts the repaired bell outside the herb shelter and asks whether anyone will hear it tomorrow. The forest needs more than one victory; it needs a road people will keep walking.",
            "Arin lets the living ferrymen copy the names in their own hands. 'When the citadel asks for more power,' she asks, 'will we be erased again?' You cannot promise the tide will never rise. You can decide whether their record remains public.",
            "The miners pin their signed shutdown petition to the machinery door. Nobody cheers. Tomorrow still brings questions of wages, light and food. Venn says, 'But today we can at least say when it is dangerous.'",
            "Residents sew the two halves of blue writing together. Children from inside the wall stand beside an elder from outside. Nobody demands the other retract a need. The evidence gives no easy answer, but Orrin can no longer pretend to hear only half the sentence."};
        private int chapterRoadRoom=-1,chapterRoadStage;
        private MapData chapterRoadMap;
        private SaveData chapterRoadSave;
        private WorldObject chapterRoadOrigin;
        private float chapterRoadTime;
        private bool chapterRoadDeviceUsed;
        private bool chapterRoadDiversion;
        private readonly List<Enemy> chapterRoadFoes=new List<Enemy>();
        private readonly HashSet<Enemy> chapterRoadKills=new HashSet<Enemy>();
        private readonly HashSet<Enemy> chapterRoadBinds=new HashSet<Enemy>();
        public int ChapterRoadStage {get{return ChapterRoadActive?chapterRoadStage:0;}}
        public bool ChapterRoadDeviceUsed
        {get{return ChapterRoadActive&&chapterRoadDeviceUsed;}}
        public bool ChapterRoadDeviceReady
        {get{return ChapterRoadActive&&chapterRoadStage==2&&
            !ChapterRoadSecured&&!chapterRoadDeviceUsed;}}
        private static readonly string[] chapterDeviceZh={"",
            "敲响修好的警铃 · 消耗木材1，震慑追兵并为伤者止血",
            "打开临时分流闸 · 耐力18，挡住寒流、争取救援时间",
            "打开紧急泄压阀 · 耐力18，驱散热浪、打断攻击",
            "点亮撤离信号灯 · 耐力15，聚拢居民并打断袭击"};
        private static readonly string[] chapterDeviceEn={"",
            "Ring repaired warning bell · 1 wood, interrupt raiders and aid wounded",
            "Raise bypass sluice · 18 stamina, stop chill and buy rescue time",
            "Vent emergency manifold · 18 stamina, quell steam and interrupt attacks",
            "Light civilian signal · 15 stamina, rally evacuees and interrupt raiders"};
        public float ChapterRoadSeconds {get{return ChapterRoadActive?chapterRoadTime:0f;}}
        public bool ChapterRoadDone(int room)
        {return room>=1&&room<=4&&Has("chapter_road_done_"+room);}
        public int ChapterRoadCompleted
        {get{int n=0;for(int i=1;i<=4;i++)if(ChapterRoadDone(i))n++;return n;}}
        public int ChapterRoadChoice(int room)
        {return room>=1&&room<=4?
            Has("chapter_road_care_"+room)?1:
            Has("chapter_road_witness_"+room)?2:0:0;}
        public int ChapterRoadNodeIndex(WorldObject o)
        {
            if(o==null||o.kind!="chapterroad")return -1;
            for(int n=0;n<3;n++)
                if(o.id=="chapterroad_"+save.room+"_"+n)return n;
            return -1;
        }
        private WorldObject ChapterRoadNode(int room,int n)
        {
            if(room<1||room>4||n<0||n>2)return null;
            foreach(WorldObject o in data.maps[room].objects)
                if(o.kind=="chapterroad"&&o.id=="chapterroad_"+room+"_"+n)
                    return o;
            return null;
        }
        public bool ChapterRoadActive
        {get{return chapterRoadRoom>=1&&chapterRoadRoom<=4&&
            save.room==chapterRoadRoom&&
            Object.ReferenceEquals(chapterRoadMap,Map)&&
            Object.ReferenceEquals(chapterRoadSave,save)&&
            chapterRoadOrigin!=null&&
            SourceBelongsToActiveMap(chapterRoadOrigin);}}
        private bool IsChapterRoadEnemy(Enemy e)
        {return e!=null&&ChapterRoadActive&&chapterRoadFoes.Contains(e);}
        public bool IsLiveChapterRoadEnemy(Enemy e)
        {return IsChapterRoadEnemy(e)&&e.hp>0&&enemies.Contains(e);}
        private void OnChapterRoadEnemyDefeated(Enemy e)
        {if(IsChapterRoadEnemy(e))chapterRoadKills.Add(e);}
        private bool OnChapterRoadEnemyRestrained(Enemy e)
        {
            if(!IsChapterRoadEnemy(e)||e.hp<=0||enemies.Contains(e))return false;
            chapterRoadBinds.Add(e);return true;
        }
        public int ChapterRoadRemaining
        {get{int n=0;if(ChapterRoadActive)foreach(Enemy e in chapterRoadFoes)
            if(e!=null&&e.hp>0&&enemies.Contains(e))n++;return n;}}
        public bool ChapterRoadSecured
        {get{
            if(!ChapterRoadActive||chapterRoadStage!=2||
                chapterRoadFoes.Count<2)return false;
            foreach(Enemy e in chapterRoadFoes)
            {
                if(e==null||enemies.Contains(e))return false;
                if(chapterRoadKills.Contains(e)&&e.hp<=0)continue;
                if(chapterRoadBinds.Contains(e)&&e.hp>0)continue;
                return false;
            }
            return true;
        }}
        public Point ChapterRoadGuideTarget
        {get{
            if(!ChapterRoadActive)return Position;
            if(chapterRoadStage==2)
            {
                foreach(Enemy e in chapterRoadFoes)
                    if(e!=null&&e.hp>0&&enemies.Contains(e))return e.Position;
                WorldObject endpoint=ChapterRoadNode(chapterRoadRoom,2);
                return endpoint!=null?new Point(endpoint.x,endpoint.y):Position;
            }
            int idx=chapterRoadStage==1?1:2;
            WorldObject next=ChapterRoadNode(chapterRoadRoom,idx);
            return next!=null?new Point(next.x,next.y):Position;
        }}
        private void ResetChapterRoad()
        {
            foreach(Enemy e in chapterRoadFoes)
            {
                RemoveEnemyPendingWarning(e);
                enemies.Remove(e);
                regionalAmbientEnemies.Remove(e);
                projectiles.RemoveAll(p=>Object.ReferenceEquals(p.hostileShooter,e));
            }
            chapterRoadRoom=-1;chapterRoadStage=0;
            chapterRoadMap=null;chapterRoadSave=null;chapterRoadOrigin=null;
            chapterRoadTime=0;chapterRoadDeviceUsed=false;chapterRoadDiversion=false;chapterRoadFoes.Clear();
            chapterRoadKills.Clear();chapterRoadBinds.Clear();
        }
        // The four civilian routes now use the ACTUAL geography around
        // their middle sites. Favor different flanking approaches instead
        // of filling the first northern scanline in every region. Any
        // blocked road falls back to the old exhaustive legal-pad search.
        // Nothing is allowed to spawn through a closed original gate.
        private static readonly int[,,] chapterRoadApproaches={
            {{-1,-1},{1,1},{0,-1}}, // Rootwood: pursuit from split tree trails
            {{1,0},{-1,1},{0,1}},   // Floodway: banks and rear water stair
            {{-1,1},{1,-1},{0,1}},  // Mine: opposing carts and manifold lane
            {{1,-1},{-1,1},{1,1}}   // Citadel: staggered wall-side sentries
        };
        private static readonly int[,] chapterRoadRaidKinds={
            {0,1,0}, // Forest: pursuer and bow flanker
            {1,0,0}, // Floodway: ranged watch behind close pursuit
            {2,0,2}, // Mine: armored pressure around a runner
            {2,1,0}  // Citadel: combined arms behind the evacuation wall
        };
        private bool ChapterRoadRaidFootFree(Point p,List<Point> placed)
        {
            if(!CanStand(p)||(p-Position).Length<4f||
               NavigationPathFrom(Position,p,true).Count==0)return false;
            foreach(WorldObject prop in Map.objects)
                if(prop!=null&&(p-new Point(prop.x,prop.y)).Length<3.1f)
                    return false;
            foreach(Enemy e in enemies)
                if(e!=null&&e.hp>0&&(e.Position-p).Length<3.2f)
                    return false;
            foreach(Point previous in placed)
                if((previous-p).Length<5.2f)return false;
            return true;
        }
        private bool SpawnChapterRoadRaiders()
        {
            int room=chapterRoadRoom;
            WorldObject middle=ChapterRoadNode(room,1);
            if(middle==null)return false;
            int count=room>=3?3:2;
            int cx=(int)Math.Floor(middle.x),cy=(int)Math.Floor(middle.y);
            var places=new List<Point>();
            // These are genuine tiles in the live game, not decorative
            // formation markers. An approach is accepted only after the
            // same real player-foot path and live prop/crowd checks.
            for(int rank=0;rank<count;rank++)
            {
                int dx=chapterRoadApproaches[room-1,rank,0];
                int dy=chapterRoadApproaches[room-1,rank,1];
                bool placed=false;
                for(int radius=6;radius<=13&&!placed;radius++)
                for(int sideways=-3;sideways<=3&&!placed;sideways++)
                {
                    Point candidate=new Point(
                        cx+dx*radius-dy*sideways+.5f,
                        cy+dy*radius+dx*sideways+.5f);
                    if(!ChapterRoadRaidFootFree(candidate,places))
                        continue;
                    places.Add(candidate);placed=true;
                }
            }
            // If tides, trees or original locked gates suppress one of
            // those approaches, complete the full wave by the historically
            // valid exhaustive search. Never charge stamina or advance the
            // story for a partial/inaccessible combat wave.
            for(int radius=5;radius<=14&&places.Count<count;radius++)
            for(int dy=-radius;dy<=radius&&places.Count<count;dy++)
            for(int dx=-radius;dx<=radius&&places.Count<count;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius||
                    Math.Abs(dx)+Math.Abs(dy)>19)continue;
                Point p=new Point(cx+dx+.5f,cy+dy+.5f);
                if(ChapterRoadRaidFootFree(p,places))places.Add(p);
            }
            if(places.Count!=count)return false;
            for(int i=0;i<count;i++)
            {
                int kind=chapterRoadRaidKinds[room-1,i];
                Enemy e=CreatePlacedEnemy(++enemyId,kind,places[i],
                    31f+room*10f);
                chapterRoadFoes.Add(e);enemies.Add(e);
                effects.Add(new Effect("enemyRelease",e.Position,.6f){variant=kind});
            }
            return true;
        }
        private void TickChapterRoad(float dt)
        {
            if(chapterRoadRoom>=1)
            {
                if(!ChapterRoadActive||Dying||practicing)
                {
                    ResetChapterRoad();return;
                }
                if(chapterRoadStage==2)
                {
                    foreach(Enemy e in chapterRoadFoes)
                        if(e==null||(!chapterRoadKills.Contains(e)&&
                           !chapterRoadBinds.Contains(e)&&
                           (!enemies.Contains(e)||e.hp<=0)))
                        {
                            ResetChapterRoad();revision++;
                            Notify("护路的敌人无凭据消失，居民拒绝签收。请从原告示重新组织。",
                                "An unverified raider vanished. Restart at the original notice.");
                            return;
                        }
                }
                // Physical, region-specific combat terrain. The genuine
                // ebb/flood clock affects Ruins; the exposed mine manifold
                // heats its immediate vicinity. Stepping away avoids both.
                // No idle damage, no global status and no save migration.
                if(chapterRoadStage==2&&!ChapterRoadSecured&&dt>0f&&
                   mode=="world")
                {
                    WorldObject middle=ChapterRoadNode(chapterRoadRoom,1);
                    if(middle!=null&&
                       (Position-new Point(middle.x,middle.y)).Length<6f)
                    {
                        if(chapterRoadRoom==2&&!LowTide&&!chapterRoadDeviceUsed)
                        {
                            save.cold=Math.Min(100f,save.cold+dt*1.8f);
                            save.energy=Math.Max(0f,save.energy-dt*.55f);
                        }
                        else if(chapterRoadRoom==3&&!chapterRoadDeviceUsed)
                        {
                            save.heat=Math.Min(100f,save.heat+dt*2.4f);
                            save.energy=Math.Max(0f,save.energy-dt*.75f);
                        }
                    }
                }
                if(chapterRoadTime>0f)
                {
                    chapterRoadTime=Math.Max(0,chapterRoadTime-dt);
                    if(chapterRoadTime==0)
                    {
                        ResetChapterRoad();revision++;sound="alert";
                        Notify("现场时间已过。人员安全撤回；补给与战斗奖励均未签收。",
                            "Field window expired. Everyone withdrew; no route reward was earned.");
                    }
                }
            }
            // Distinct real-world upgrades. They apply at the physical LAST
            // chapter site in safe world play, not from global quest completion.
            if(mode!="world"||Dying||practicing||
               FieldWorkPreventsRest||!SafeTravel||
               save.room<1||save.room>4||!ChapterRoadDone(save.room))
                return;
            WorldObject endpoint=ChapterRoadNode(save.room,2);
            if(endpoint==null||
               (Position-new Point(endpoint.x,endpoint.y)).Length>3.2f)
                return;
            switch(save.room)
            {
                case 1:
                    save.health=Math.Min(save.maxHealth,
                        save.health+dt*(ChapterRoadChoice(1)==1?1.6f:.8f));
                    break;
                case 2:
                    float climate=ChapterRoadChoice(2)==2?3.2f:2f;
                    save.cold=Math.Max(0,save.cold-dt*climate);
                    save.heat=Math.Max(0,save.heat-dt*climate);
                    break;
                case 3:
                    save.energy=Math.Min(100,save.energy+
                        dt*(ChapterRoadChoice(3)==2?5.2f:3.6f));
                    break;
                case 4:
                    bool published=ChapterRoadChoice(4)==2;
                    save.health=Math.Min(save.maxHealth,
                        save.health+dt*(published?1.15f:.7f));
                    save.energy=Math.Min(100,save.energy+
                        dt*(published?3f:2f));
                    break;
            }
        }
        private bool TalkChapterRoad(WorldObject o)
        {
            if(o.kind!="chapterroad")return false;
            int room=save.room,slot=ChapterRoadNodeIndex(o);
            if(room<1||room>4||slot<0)return true;
            if(!HasPhysicalInteractionReach(o))
            {
                Notify("走到实际的章节现场才能调查。",
                    "Reach the actual chapter landmark to investigate.");
                return true;
            }
            if(save.ending>0)
            {
                Dialog(o.name,o.en,chapterEndZh[room],
                    chapterEndEn[room],C("记住他们","Remember them","close"));
                return true;
            }
            if(ChapterRoadDone(room))
            {
                Dialog(o.name,o.en,chapterEndZh[room]+
                    "\n\n这里仍有人使用你守住的路，不必再重复旧战斗。",
                    chapterEndEn[room]+
                    "\n\nPeople still use the road you secured. There is no replayable first-clear reward.",
                    C("继续旅程","Continue","close"));
                return true;
            }
            if(!ChapterRoadActive)
            {
                if(slot!=0)
                {
                    Dialog(o.name,o.en,"这里有一段被割断的居民救援线索。先回到本章最西侧的原始求援标记，查明是谁求援。",
                        "This clue belongs to an unfinished civilian rescue. Start at the original western distress notice first.",
                        C("沿路寻找求援人","Find the origin","close"));
                    return true;
                }
                Dialog(o.name,o.en,chapterOpeningZh[room]+
                    "\n\n"+chapterOpeningWitnessZh[room],
                    chapterOpeningEn[room]+
                    "\n\n"+chapterOpeningWitnessEn[room],
                    C("接手本章居民行动 · 实地步行","Begin the civilian chapter route · on foot","chapterroad_begin"),
                    C("现在不出发","Not yet","close"));
                return true;
            }
            if(slot==0)
            {
                Dialog(o.name,o.en,"你已接下这条真实路线。前方有受困的人；如果需要撤回，请在这里明确归还这份委托。",
                    "The live route is underway. Residents are ahead; withdraw here explicitly if you cannot finish.",
                    C("撤回现场行动 · 不领取奖励","Withdraw · no reward","chapterroad_abort"),
                    C("继续向前","Continue ahead","close"));
                return true;
            }
            if(slot==1)
            {
                if(chapterRoadStage==1)
                    Dialog(o.name,o.en,chapterMiddleZh[room]+
                        "\n\n"+chapterMidWitnessZh[room],
                        chapterMiddleEn[room]+
                        "\n\n"+chapterMidWitnessEn[room],
                        C("站稳阵地 · 耐力10，拦截袭击者",
                            "Brace the line · 10 stamina, intercept raiders",
                            "chapterroad_defend"),
                        C(new[]{"","留1份药草燃起安全求援烟，引开追兵",
                            "留1份木材做空载木筏，吸引巡逻者追逐",
                            "留1块矿石卡住计压仪，骗走雇佣守卫",
                            "花14硬币借货车先把平民送走"}[room],
                            new[]{"","Spend 1 herb on rescue smoke and distract pursuers",
                            "Spend 1 wood on an empty raft decoy",
                            "Spend 1 ore jamming the gauge to divert hired guards",
                            "Spend 14 coins on a freight cart to evacuate civilians first"}[room],
                            "chapterroad_divert"),
                        C("先检查地形","Check the approach","close"));
                else
                {
                    var midChoices=new List<Choice>();
                    if(ChapterRoadDeviceReady)
                        midChoices.Add(C(chapterDeviceZh[room],
                            chapterDeviceEn[room],"chapterroad_device"));
                    midChoices.Add(C("继续前进","Continue","close"));
                    string siteZh=ChapterRoadSecured?
                        "袭击者已被真正击退或活捉。现在走到最后的居民安置点签收证词。":
                        "前方仍有"+ChapterRoadRemaining+
                        "名袭击者。保护退路，别让任何人被落在后面。";
                    string siteEn=ChapterRoadSecured?
                        "The original raiders are defeated or restrained. Reach the final civilian handoff.":
                        ChapterRoadRemaining+
                        " genuine raiders still hold the road. Secure it before proceeding.";
                    if(chapterRoadDeviceUsed)
                    {
                        siteZh+="\n\n现场装置已经启动，不能反复领取帮助。";
                        siteEn+="\n\nThe field mechanism has already fired; it cannot be reused.";
                    }
                    Dialog(o.name,o.en,siteZh,siteEn,midChoices.ToArray());
                }
                return true;
            }
            if(!ChapterRoadSecured)
            {
                Dialog(o.name,o.en,"居民接收点尚未安全。不能越过原本的战斗，直接在远端领赏。",
                    "The receiving station is not secure. A distant report cannot bypass the real encounter.",
                    C("回到现场","Return to the road","close"));
                return true;
            }
            var choices=new List<Choice>();
            if(room==1)
            {
                choices.Add(C("留下药草1，为伤者建立常设护理点",
                    "Leave 1 herb · establish better field care","chapterroad_care"));
                choices.Add(C("用已有急救包完成登记","File with existing first-aid supplies",
                    "chapterroad_finish"));
            }
            else if(room==2)
            {
                choices.Add(C("让渡工公开真正的失踪名单",
                    "Let ferrymen publish the missing names","chapterroad_witness"));
                choices.Add(C("保管原始名单，留作日后听证",
                    "Preserve the original roll for the hearing","chapterroad_finish"));
            }
            else if(room==3)
            {
                choices.Add(C("公开矿工停机权的签名",
                    "Publish workers' right-to-stop petition","chapterroad_witness"));
                choices.Add(C("封存工单等待独立复核",
                    "Secure the work order for an independent review","chapterroad_finish"));
            }
            else
            {
                choices.Add(C("将平民姓名贴在城门外",
                    "Publish civilian names outside the citadel","chapterroad_witness"));
                choices.Add(C("抄送原件，让居民带走一份",
                    "Send a duplicate home with the civilians","chapterroad_finish"));
            }
            choices.Add(C("检查安置点","Inspect the handoff","close"));
            Dialog(o.name,o.en,chapterEndZh[room]+
                "\n\n"+chapterClosingWitnessZh[room]+
                (chapterRoadDiversion?new[]{"",
                    "\n\n跟着药草烟找来的巡路人救出另一名伤者；那个孩子却只问，明天铃还会不会响。",
                    "\n\n追兵追着空木筏离开，阿菱的弟弟才敢带着原始值夜簿走出水下楼梯。",
                    "\n\n压力表的误报让监工冲向空车轨。学徒亲手按下停机钮，第一次不必等他的签字。",
                    "\n\n车驶过城门时，一名士兵认出了妹妹的围巾。他仍握着长矛，却没有拦住平民。"}[room]:"")+
                "\n\n请选择居民今后如何保留这份真实证词。",
                chapterEndEn[room]+
                "\n\n"+chapterClosingWitnessEn[room]+
                (chapterRoadDiversion?new[]{"",
                    "\n\nA warden followed the herb smoke and carried out another wounded child. She only asked whether the bell would ring tomorrow.",
                    "\n\nThe patrol followed the empty raft downstream. Arin's brother emerged from the flooded stairs with the untouched night ledger.",
                    "\n\nThe false gauge alarm drew the foreman to an empty rail. His apprentice shut down the engine himself, without waiting for permission.",
                    "\n\nA guard recognized his sister's scarf in the departing cart. He kept his spear but let the civilians pass."}[room]:"")+
                "\n\nChoose how the survivors keep this physical testimony.",
                choices.ToArray());
            return true;
        }
        private bool ChooseChapterRoad(string action)
        {
            if(action=="chapterroad_track")
            {
                WorldObject from=pendingDialogueSource;
                if(save.room==0&&save.ending==0&&from!=null&&
                    from.kind=="guild"&&SourceBelongsToActiveMap(from)&&
                    HasPhysicalInteractionReach(from))
                {
                    TrackQuest("chapter_roads");
                    journalFocusRequest="chapter_roads";mode="journal";
                }
                return true;
            }
            bool start=action=="chapterroad_begin",fight=action=="chapterroad_defend",
                diversion=action=="chapterroad_divert",
                device=action=="chapterroad_device",
                abort=action=="chapterroad_abort",
                care=action=="chapterroad_care",witness=action=="chapterroad_witness",
                finish=action=="chapterroad_finish";
            if(!start&&!fight&&!diversion&&!device&&!abort&&!care&&!witness&&!finish)return false;
            int room=save.room,slot=abort||start?0:fight||diversion||device?1:2;
            WorldObject source=pendingDialogueSource;
            if(room<1||room>4||save.ending>0||Dying||practicing||
               mode!="world"||source==null||source!=ChapterRoadNode(room,slot)||
               pendingDialogueSourceRoom!=room||
               !SourceBelongsToActiveMap(source)||!HasPhysicalInteractionReach(source)||
               !CanStand(Position))return true;
            if(abort)
            {
                if(ChapterRoadActive)
                {
                    ResetChapterRoad();revision++;sound="ui";
                    Notify("居民已从本次行动撤回。没有领取任何报酬。",
                        "Residents withdrew; no first-clear reward was awarded.");
                }
                return true;
            }
            if(start)
            {
                if(ChapterRoadDone(room)||ChapterRoadActive||
                   FieldWorkPreventsRest||!SafeTravel)
                {
                    Notify("必须先完成其他现场行动并脱离战斗，才能接下新的居民委托。",
                        "Finish other live duties and clear threats before starting the chapter route.");
                    return true;
                }
                chapterRoadRoom=room;chapterRoadStage=1;
                chapterRoadMap=Map;chapterRoadSave=save;
                chapterRoadOrigin=source;
                chapterRoadTime=(room==2?165f:room==3?150f:0f);
                revision++;sound="quest";
                Notify("沿真实道路找到下一个现场，按 E 组织防守。",
                    "Walk to the second original site and press E to defend.");
                return true;
            }
            if(fight||diversion)
            {
                if(!ChapterRoadActive||chapterRoadStage!=1||
                   save.energy<(diversion?6f:10f)||!CanStand(Position))return true;
                int material=room==1?2:room==2?0:4;
                if(diversion&&(room==4?save.coins<14:save.inventory[material]<1))
                {
                    Notify("物资不足以布置撤离诱饵。也可以直接守住防线。",
                        "Not enough supplies for the civilian decoy; direct defense remains possible.");
                    return true;
                }
                // An authentic hostile wave must be fully reachable BEFORE
                // the player spends stamina or a state transition is committed.
                if(!SpawnChapterRoadRaiders())
                {
                    Notify("现场暂时没有完整可到达的敌人进路，换个位置再组织。",
                        "The full real enemy approach is blocked. Reposition and retry.");
                    return true;
                }
                if(diversion)
                {
                    if(room==4)save.coins-=14;
                    else save.inventory[material]--;
                    chapterRoadDiversion=true;
                    foreach(Enemy enemy in chapterRoadFoes)
                    {
                        enemy.stun=Math.Max(enemy.stun,1.4f);
                        enemy.armorOpenTime=Math.Max(enemy.armorOpenTime,1.1f);
                    }
                }
                save.energy-=diversion?6f:10f;chapterRoadStage=2;
                sound="alert";revision++;
                Notify(chapterMiddleZh[room]+
                    (diversion?" 你用真实物资吸引守卫转向，平民趁机撤离。但仍必须实际击退敌人。":"")+
                    " 击退或绊索活捉全部"+
                    chapterRoadFoes.Count+"名袭击者，再步行前往接收点。",
                    chapterMiddleEn[room]+
                    (diversion?" Your decoy drew the guards away from civilians; the real attackers still need to be stopped.":"")+
                    " Defeat or restrain all "+
                    chapterRoadFoes.Count+" actual raiders, then walk to the receiver.");
                return true;
            }
            if(device)
            {
                if(!ChapterRoadDeviceReady||!ChapterRoadActive||
                    !Object.ReferenceEquals(chapterRoadMap,Map))return true;
                int timber=room==1?1:0;
                int stamina=room==1?0:room==4?15:18;
                if(save.inventory[0]<timber||save.energy<stamina)
                {
                    Notify("要真正发动现场装置，还缺少木材或耐力。",
                        "The live mechanism requires timber or stamina.");
                    return true;
                }
                save.inventory[0]-=timber;save.energy-=stamina;
                chapterRoadDeviceUsed=true;
                WorldObject mechanism=ChapterRoadNode(room,1);
                Point center=new Point(mechanism.x,mechanism.y);
                int disrupted=0;
                foreach(Enemy foe in chapterRoadFoes)
                    if(foe!=null&&foe.hp>0&&enemies.Contains(foe)&&
                        (foe.Position-center).Length<=14f)
                    {
                        // The existing combat-control transaction cancels
                        // REAL pending windups, not cosmetic status labels.
                        ApplyEnemyControl(foe,room==1?1.4f:1.15f);
                        foe.armorOpenTime=Math.Max(foe.armorOpenTime,.7f);
                        disrupted++;
                    }
                if(room==1)
                    save.health=Math.Min(save.maxHealth,save.health+12f);
                if(room==2)
                {
                    save.cold=Math.Max(0,save.cold-12f);
                    chapterRoadTime+=15f;
                }
                if(room==3)
                {
                    save.heat=Math.Max(0,save.heat-16f);
                    chapterRoadTime+=12f;
                }
                if(room==4)
                    save.energy=Math.Min(100f,save.energy+8f);
                effects.Add(new Effect("burst",center,.6f)
                    {radius=3.2f,variant=room%3});
                sound="reward";revision++;
                Notify(chapterDeviceZh[room]+" · 已激活，干扰"+disrupted+"名真实袭击者。",
                    chapterDeviceEn[room]+" · activated; interrupted "+disrupted+" real attackers.");
                return true;
            }
            if(!ChapterRoadActive||!ChapterRoadSecured||
               ChapterRoadDone(room)||!SafeTravel)return true;
            if(care)
            {
                if(room!=1||save.inventory[2]<1)
                {
                    Notify("护理点还需要一份真实药草。",
                        "One genuine herb is required for improved field care.");
                    return true;
                }
            }
            if(witness&&room==1)return true;
            bool allCaptures=chapterRoadBinds.Count==chapterRoadFoes.Count&&
                chapterRoadKills.Count==0;
            bool decoyUsed=chapterRoadDiversion;
            if(care)save.inventory[2]--;
            ResetChapterRoad();
            Flag("chapter_road_done_"+room);
            if(decoyUsed)Flag("chapter_road_diverted_"+room);
            if(care)Flag("chapter_road_care_"+room);
            if(witness)Flag("chapter_road_witness_"+room);
            CreditCoins(17+room*10);
            CreditShards(allCaptures?2:1);
            AddXp(34+room*15);
            if(ChapterRoadCompleted==4&&!Has("chapter_road_all"))
            {
                Flag("chapter_road_all");CreditSkillPoints(1);
            }
            RecordMoment(chapterEndZh[room]+
                (witness?" 居民选择公开原始证词。":" 居民自己保存了签收证明。"),
                chapterEndEn[room]+
                (witness?" Survivors published the original testimony.":
                    "Survivors kept their signed evidence."));
            sound="reward";revision++;
            Notify("本章居民路线完成！真实安置点已开放公共服务。",
                "Chapter route signed! This real site now offers safe local public care.");
            return true;
        }
        // This is a narrative CONSEQUENCE of the real chapter handoff,
        // not another compulsory quest or an invented extra boss choice.
        // It is attached to the original live shrine conversation, including
        // Orrin's established peaceful alternatives, without changing them.
        private void AppendChapterRoadBossForeword()
        {
            if(save.room<1||save.room>4||!ChapterRoadDone(save.room)||
               mode!="dialog"||conversation==null)return;
            bool publicRecord=ChapterRoadChoice(save.room)==2;
            string zh="",en="";
            switch(save.room)
            {
                case 1:
                    zh="林间的求救铃重新响起，连被荆棘封死的旧路都传来了回音。巨兽脚下的腐根裂开，露出早已失灵的护林铭文。";
                    en="The rescue bell rings again through the forest. Old roots split beneath the beast, exposing the broken warding runes under its feet.";
                    break;
                case 2:
                    zh=publicRecord?
                        "渡工在岸边念出了失踪者的姓名。水声掩不住那些人的回答。守卫回头寻找旧渡船，胸口的潮汐机关因此露出空隙。":
                        "摆渡人将名单收进防水筒。旧渡船的灯仍为他们亮着，潮汐守卫的机关跟着被打乱的水位闪烁。";
                    en=publicRecord?
                        "The ferry crew speak the missing names aloud. Voices answer across the water; the sentinel turns toward the old boat and exposes a gap in its tide mechanism.":
                        "The crew seal their names inside a dry case. Old ferry lamps remain lit, and the sentinel's tide mechanism flickers against the changed current.";
                    break;
                case 3:
                    zh=publicRecord?
                        "矿工把停机联名纸钉在监工的门上。泄压管终于有了喘息的间隙，铁甲后那根一直绷紧的传动轴露了出来。":
                        "独立复核的工单被送到安全的工棚。矿场的泄压管重新开启，监工铁甲里的传动轴不再承受全部压力。";
                    en=publicRecord?
                        "Miners nail their stop-work petition to the overseer's door. Steam escapes through the relief pipe, exposing the drive shaft behind its iron armor.":
                        "Workers carry their protected work order to the safe shed. The reopened vent lowers the pressure across the overseer's armored drive.";
                    break;
                case 4:
                    zh=publicRecord?
                        "厄洛在城门外看见一面新的姓名墙。“这些名字怎么会在外头？”他停顿了片刻。没人替居民回答，墙上的纸却没有被撕掉。":
                        "一名撤离的孩子把家人的名单递给厄洛。他看见同一份记录上城内与城外的地址，终于放下了握着铁门把手的那只手。";
                    en=publicRecord?
                        "Orrin sees the new wall of names outside his gates. 'Why were these people kept out?' No one answers for the civilians. The notices remain nailed in place.":
                        "An evacuated child gives Orrin a copy of the family's names. Inside and outside addresses share the same page. His hand loosens on the gate handle.";
                    break;
            }
            conversation.text+="\n\n"+zh;
            conversation.textEn+="\n\n"+en;
        }
        private void AppendChapterRoadGuildChoice(WorldObject o)
        {
            if(save.room!=0||save.ending>0||o==null||
               o.kind!="guild"||conversation==null||mode!="dialog")return;
            var list=new List<Choice>(conversation.choices);
            int at=list.Count;
            if(at>0&&list[at-1].action=="close")at--;
            list.Insert(at,C("主线1–4 · 居民的未讲完的故事 ("+
                ChapterRoadCompleted+"/4)",
                "Chapters I–IV · Untold civilian stories ("+
                ChapterRoadCompleted+"/4)","chapterroad_track"));
            conversation.choices=list.ToArray();
            if(ChapterRoadCompleted>0)
            {
                conversation.text+="\n\n莉雅把"+ChapterRoadCompleted+
                    "份居民签收放在星核旁：“这些人的名字，比我们的胜利更值得留住。”";
                conversation.textEn+="\n\nLyra lays "+ChapterRoadCompleted+
                    " civilian receipts beside the cores. “Their names deserve to last longer than our victories.”";
            }
        }
    }
}
