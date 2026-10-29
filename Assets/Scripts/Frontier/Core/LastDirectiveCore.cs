using System;
namespace Starfall {
public sealed partial class GameCore {
    private int directiveRoom=-1, directivePhase, directiveKills;
    // A resident's final battle is owned by its ACTUAL spawned actors.
    // Reused numeric identifiers must never stand in for a vanished
    // defender, count as a verified lethal kill or absorb mission-only
    // cleanup and visual effects.
    private readonly Enemy[] directiveActors=new Enemy[3];
    private float directiveSurgeLeft;
    // When all three original witness records were physically audited, the
    // hero can ground exactly one final-lock grid surge at the live console.
    // The tactical order expires with the encounter and is never saved.
    private bool directiveEvidenceGroundUsed;
    public bool LastDirectiveEvidenceGroundAvailable
    {get{return directiveRoom==4&&directivePhase==3&&
        LastDirectiveTraces==3&&!directiveEvidenceGroundUsed;}}
    private static readonly Point[] directiveSpawns={new Point(98.5f,74.5f),new Point(104.5f,74.5f),new Point(98.5f,74.5f),new Point(104.5f,74.5f),new Point(106.5f,73.5f)};
    // A signed regional service ledger already unlocks the historical
    // evidence. No second trip to a town signboard is required, including
    // on an existing v2 save made after the three civic services.
    public bool LastDirectiveStarted {get{return Has("last_directive_started")||
        (save.ending>0&&Has("civic_response_ledger"));}}
    public bool LastDirectiveComplete {get{return Has("last_directive_complete");}}
    // Signed, unpaid Chapter Ten talent is represented by an existing
    // SaveData v2 string flag. The civic decision can publish even when
    // the saved 99-point talent wallet has no room.
    public bool LastDirectiveTalentDue
    {get{return LastDirectiveComplete&&Has("last_directive_talent_due");}}
    public int LastDirectiveClues {get{int n=0;for(int i=0;i<3;i++)if(Has("last_directive_clue_"+i))n++;return n;}}
    // Optional, physically audited witness details deepen the final
    // investigation without invalidating old saves or gating its outcome.
    public int LastDirectiveTraces
    {get{int n=0;for(int i=0;i<3;i++)if(Has("last_directive_trace_"+i))n++;return n;}}
    public int LastDirectiveWave {get{return directiveRoom==4?directivePhase:0;}}
    public int LastDirectiveEnemies {get{int n=0;foreach(Enemy e in enemies)if(IsLastDirectiveEnemy(e))n++;return n;}}
    public bool LastDirectiveReady {get{return LastDirectiveStarted&&LastDirectiveClues==3;}}
    public int LastDirectiveChoice {get{return Has("last_directive_open")?1:Has("last_directive_shelter")?2:0;}}
    private bool IsLastDirectiveEnemy(Enemy e){
        if(e==null||directiveRoom!=4)return false;
        foreach(Enemy guard in directiveActors)
            if(System.Object.ReferenceEquals(e,guard))return true;
        return false;
    }
    private void ResetLastDirective(){enemies.RemoveAll(IsLastDirectiveEnemy);projectiles.RemoveAll(p=>p.lastDirectiveOwned);effects.RemoveAll(e=>e.lastDirectiveOwned);hazards.RemoveAll(h=>h.lastDirectiveOwned);
        for(int i=0;i<directiveActors.Length;i++)directiveActors[i]=null;
        directiveRoom=-1;directivePhase=directiveKills=0;directiveSurgeLeft=0;
        directiveEvidenceGroundUsed=false;}
    private void OnLastDirectiveEnemyDefeated(Enemy e){
        if(directiveRoom!=4||save.room!=4||(directivePhase!=1&&directivePhase!=3)||!IsLastDirectiveEnemy(e))return;
        int count=directivePhase==1?2:3;
        for(int i=0;i<count;i++)
            if(System.Object.ReferenceEquals(e,directiveActors[i]))
            {directiveKills|=1<<i;break;}
    }
    private void TickLastDirective(float dt){
        if(directiveRoom<0)return;
        if(save.room!=4||Dying||practicing){ResetLastDirective();return;}
        if(directivePhase!=1&&directivePhase!=3)return;
        int count=directivePhase==1?2:3;
        bool clear=true;
        for(int i=0;i<count;i++){
            bool present=directiveActors[i]!=null&&
                enemies.Contains(directiveActors[i]);
            if(!present&&(directiveKills&(1<<i))==0){ResetLastDirective();revision++;Notify("守卫异常离场，真实战斗必须重试。","Guardian vanished without defeat; retry combat.");return;}
            if(present||(directiveKills&(1<<i))==0)clear=false;
        }
        if(!clear)
        {
            if(directivePhase==3)
            {
                directiveSurgeLeft-=dt;
                if(directiveSurgeLeft<=0)
                {
                    // Same real delayed/collision-checked boss hazard system:
                    // warnings remain visible and can be dodged before contact.
                    directiveSurgeLeft=6.1f;
                    hazards.Add(new Hazard{position=Position,radius=1.45f,
                        timer=1.15f,damage=18,kind="rift",lastDirectiveOwned=true});
                    sound="windup";revision++;
                    Notify("终端发出真实的脉冲落点预警！离开红色圈，或在命中前闪避。",
                        "Actual terminal surge telegraphed! Leave the warning circle or dodge before impact.");
                }
            }
            return;
        }
        directivePhase=directivePhase==1?2:4;directiveKills=0;
        // This wave is finished: no old guard reference may own the next
        // wave's projectiles or be mistaken for a later enemy.
        for(int i=0;i<directiveActors.Length;i++)directiveActors[i]=null;
        projectiles.RemoveAll(p=>p.lastDirectiveOwned);effects.RemoveAll(e=>e.lastDirectiveOwned);
        hazards.RemoveAll(h=>h.lastDirectiveOwned);revision++;sound="reward";
        Notify(directivePhase==2?"第一道保密卫队已击败。在终端以25耐力解除二级封锁。":"三名最终守卫已击败，回控制台决定公共权力归属。",
            directivePhase==2?"First wave defeated. Spend 25 energy at the terminal to lift the second lock.":"All three final guards defeated. Choose public control at the terminal.");
    }
    // A defended terminal requires accessible, distinct living guard feet.
    // Preflight the entire original formation before constructing enemies.
    private static readonly Point[] directiveFootOffsets={
        new Point(0f,0f),new Point(0f,1.5f),new Point(0f,-1.5f),
        new Point(1.5f,0f),new Point(-1.5f,0f),
        new Point(1.5f,1.5f),new Point(-1.5f,1.5f),
        new Point(1.5f,-1.5f),new Point(-1.5f,-1.5f),
        new Point(0f,2.5f),new Point(0f,-2.5f),
        new Point(2.5f,0f),new Point(-2.5f,0f)
    };
    private bool TryLastDirectiveGuardianFeet(int first,int count,out Point[] feet){
        feet=new Point[count];
        if(!CanStand(Position))return false;
        for(int i=0;i<count;i++){
            bool chosen=false;
            foreach(Point offset in directiveFootOffsets){
                Point foot=directiveSpawns[first+i]+offset;
                if(!CanStand(foot)||
                    (foot-Position).Length<1.75f||
                    (save.companion>=0&&(foot-companionPosition).Length<1.5f)||
                    // The third authored citadel guard is six cells from
                    // the terminal. Boss summons deliberately use radius 5,
                    // but this fixed three-guard battle spans a wider room.
                    // Use the same collision-stamped FOUR-WAY route graph
                    // across twelve local cells; never accept straight-line
                    // sight or a blocked / isolated island instead.
                    !ActorHasLocalRoute(foot,Position,12))continue;
                bool occupied=false;
                foreach(Enemy living in enemies)
                    if(living!=null&&living.hp>0&&(foot-living.Position).Length<1.5f)
                    {occupied=true;break;}
                if(occupied)continue;
                for(int j=0;j<i;j++)
                    if((foot-feet[j]).Length<1.5f){occupied=true;break;}
                if(occupied)continue;
                feet[i]=foot;chosen=true;break;
            }
            if(!chosen)return false;
        }
        return true;
    }
    private bool SpawnLastDirective(int wave){
        // The earned first-wave clear is a valid intermission state.
        // Do not destroy it until the full second wave can spawn.
        bool secondWave=wave==2&&directiveRoom==4&&directivePhase==2;
        if((wave!=1&&wave!=2)||!LastDirectiveReady||save.room!=4||
            (directiveRoom>=0&&!secondWave)||Dying||practicing||
            !SafeTravel||LastDirectiveChoice>0)return false;
        int first=wave==1?0:2,count=wave==1?2:3;
        Point[] feet;
        if(!TryLastDirectiveGuardianFeet(first,count,out feet))return false;
        directiveRoom=4;directivePhase=wave==1?1:3;directiveKills=0;
        directiveSurgeLeft=wave==2?2.6f:0;
        directiveEvidenceGroundUsed=false;
        for(int i=0;i<directiveActors.Length;i++)directiveActors[i]=null;
        for(int i=0;i<count;i++){
            Point p=feet[i];int kind=wave==1?i:i==2?2:i;
            Enemy guard=CreatePlacedEnemy(90300+first+i,kind,p,
                wave==1?79+12*i:111+17*i);
            directiveActors[i]=guard;
            enemies.Add(guard);
            effects.Add(new Effect("danger",p,.8f){radius=.6f,lastDirectiveOwned=true});
        }
        revision++;sound="windup";
        Notify(wave==1?"堡垒二名保密卫兵真实出击！":"二级封锁启动：近战、弓手和重甲守卫共同袭击！",
            wave==1?"Two real citadel guardians engage.":"Second lock: melee, archer and armored guardians engage!");
        return true;
    }
    private void AddLastDirectiveGuildChoice()
    {
        if(save.ending==0||!Has("civic_response_ledger")||
            LastDirectiveComplete||conversation==null)return;
        var choices=new System.Collections.Generic.List<Choice>(conversation.choices);
        choices.Add(C("莉雅的可选线索 · 未署名军令",
            "Lyra's optional lead · unsigned order",
            "last_directive_guild"));
        conversation.choices=choices.ToArray();
    }
    private bool TalkLastDirective(WorldObject o){
        if(!o.kind.StartsWith("lastdirective"))return false;
        if(!HasPhysicalInteractionReach(o))return true;
        if(o.kind=="lastdirectiveboard"){
            if(save.room!=0)return true;
            if(!Has("civic_response_ledger")){
                Dialog(o.name,o.en,"先完成三地的居民救援实战，再公开最后的保密令。","Publish all three civilian service records before investigating the unsigned order.",C("继续调查","Keep investigating","close"));return true;
            }
            if(LastDirectiveComplete){
                string verdictZh=LastDirectiveChoice==1?
                    "民间监督与停机申诉权已经公开。":"民用紧急庇护已成为任何人的权利。";
                string verdictEn=LastDirectiveChoice==1?
                    "Public civilian oversight and shutdown appeals are now law.":
                    "Unconditional emergency shelter is now guaranteed.";
                // Optional evidence is not a new ending or a mandatory key.
                // But residents who traced each original record deserve to
                // hear what their extra work changed in the public world.
                if(LastDirectiveTraces==3)
                {
                    verdictZh+=LastDirectiveChoice==1?
                        "\n\n莉雅把矿工的停机纸、船工的姓名单和炉火记录并排钉在墙上。\n\n“以后谁想关掉一座城的灯，先到这间屋里，把话对着这些名字说清楚。”":
                        "\n\n议事台上的旧名单被送往每一处避难所。莉雅把最后一页压平。\n\n“等人来求救时，先让他进门。名字和理由，可以等火暖了再问。”";
                    verdictEn+=LastDirectiveChoice==1?
                        "\n\nLyra pins the miners' petition, the ferry names and the hearth book side by side.\n\n“Next time someone wants to turn out a town's lights, they'll answer here, with these names on the wall.”":
                        "\n\nCopies of the old names now hang in every refuge. Lyra smooths the last page.\n\n“When someone knocks for shelter, let them in first. We can ask their name when they're warm.”";
                }
                if(LastDirectiveTalentDue)
                    Dialog(o.name,o.en,verdictZh+"\n\n你挣得的最后一课还在居民公示簿上。技能点上限99；留出容量后亲自签领。",
                        verdictEn+"\n\nYour earned final lesson remains in the public ledger. Talent capacity is 99; make room, then sign for it here.",
                        C("签领最后指令报酬 · 技能点1",
                            "Sign final directive lesson · +1 talent","last_directive_claim"),
                        C("查阅记录","Review","close"));
                else Dialog(o.name,o.en,verdictZh,verdictEn,
                    C("查阅记录","Review","close"));
                return true;
            }
            if(LastDirectiveChoice>0){
                Dialog(o.name,o.en,"你在真正的堡垒战斗后选定了最后一道权限。现在让所有人读到它。","The last directive was reclaimed through real battles. Publish the choice for all.",
                    C("公开最后指令","Publish the last directive","last_directive_publish"),C("稍后","Later","close"));return true;
            }
            Dialog(o.name,o.en,LastDirectiveStarted?
                (LastDirectiveTraces==3?
                    "莉雅把三份拓印压在镇民簿上。\n\n“停机请求先被压下，船工的名单才遭涂改。炉边的人后来把名字一个个写回来。现在他们没法说那些人从来不存在了。”":
                    "“原稿比军方抄本薄三页。”莉雅用指节敲了敲桌面。\n\n“矿工的停机请求、渡船的名单、巡路队的炉火记录都在堡垒外环。把日期对起来，谁先做了什么，就骗不了人。”"):
                "莉雅把一封没有署名的旧命令放在桌上。\n\n“上面写着堡垒有权关掉所有供能。可九年前，谁让那些居民留在黑暗里的？找出原稿，别只听我的推测。”",
                LastDirectiveStarted?
                    (LastDirectiveTraces==3?
                        "Lyra lays three rubbings across the council ledger.\n\n“They buried the miners' petition, then changed the ferry list. The people at the hearth wrote those names back. No one can pretend they never lived now.”":
                        "“The military copy is three pages shorter,” Lyra says, tapping the desk.\n\n“The miners' shutdown petition, the ferry list, and the roadwardens' hearth book are still outside the citadel. Compare the dates. Then we can prove what came first.”"):
                    "Lyra lays an unsigned order on the desk.\n\n“It says the citadel could cut power to anyone. Nine years ago, who decided those people would be left in darkness? Find the original records. Don't rely on my guesses.",
                C(LastDirectiveStarted?"标记可选调查路线":"接取旧军令调查",
                    LastDirectiveStarted?"Mark optional evidence route":"Begin unsigned-order inquiry",
                    LastDirectiveStarted?"last_directive_track":"last_directive_begin"),C("稍后再谈","Later","close"));return true;
        }
        if(save.room!=4)return true;
        if(!LastDirectiveStarted){
            Dialog(o.name,o.en,"需要余烬镇的居民调查授权。","Begin the civilian inquiry in Emberfall.",C("继续旅行","Continue","close"));return true;
        }
        if(o.kind=="lastdirectivecore"){
            if(LastDirectiveChoice>0){
                Dialog(o.name,o.en,"九年前的绝对权限已经被改写。","The old absolute command was rewritten.",C("记录","Record","close"));return true;
            }
            if(!LastDirectiveReady){
                Dialog(o.name,o.en,"终端要求三份实地取得的原始证词，不能凭猜测解除。","The terminal requires three original field records.",C("继续探索","Explore","close"));return true;
            }
            if(directivePhase==1||directivePhase==3){
                if(LastDirectiveEvidenceGroundAvailable)
                {
                    Dialog(o.name,o.en,
                        "终端正向守卫释放脉冲。三处证词的背页拼出了九年前的手动断路办法：靠近控制台，花20耐力接地一次，便能清除当前脉冲预警，延后下一次。仍必须亲自击败三名守卫。",
                        "The old console drives pulses into this fight. Notes on all three original records reveal a manual breaker: spend 20 energy here once to clear the current grid warning and delay its next strike. You must still defeat all three guards.",
                        C("接地一次 · 耐力20",
                            "Ground one grid surge · 20 energy","last_directive_ground"),
                        C("返回战斗","Return to fight","close"),
                        C("撤销本轮守卫战","Withdraw from defense","last_directive_abort"));
                }
                else
                {
                    Dialog(o.name,o.en,
                        directivePhase==3&&directiveEvidenceGroundUsed?
                            "手动断路已经用过一次。脉冲仍会继续，先闪避预警，再击败守卫。需要撤退可以在控制台终止本轮。":
                            "守卫仍在进攻。要查出旧终端的接地方法，必须在三处原始证词背面都留过调查笔记；否则只能躲开脉冲。也可以在这里撤退。",
                        directivePhase==3&&directiveEvidenceGroundUsed?
                            "The manual breaker has been spent. Dodge later warned pulses and defeat the remaining guardians, or withdraw here.":
                            "The guardians are still fighting. The emergency breaker needs field notes from all three original testimonies; otherwise evade the pulses or withdraw.",
                        C("返回战斗","Return to fight","close"),
                        C("撤销本轮守卫战","Withdraw from defense","last_directive_abort"));
                }
                return true;
            }
            if(directivePhase==2){
                Dialog(o.name,o.en,"第一道锁已破。投入25耐力释放最后一道封锁，随后迎战三名真正的敌人。","First lock disabled. Spend 25 energy to release the final three-guardian defense.",
                    C("投入25耐力解锁","Unlock · 25 energy","last_directive_second"),
                    C("撤销并保留证词","Withdraw, keep testimony","last_directive_abort"),
                    C("准备","Prepare","close"));return true;
            }
            if(directivePhase==4){
                Dialog(o.name,o.en,"最后一行写着：任何被强迫付出代价的居民，都有权要求重议。你要把停机权交给公开的巡路网络，还是让紧急庇护成为无条件的责任？",
                    "The missing final line says anyone forced to pay may demand review. Will you return oversight to public wardens or guarantee unconditional emergency shelter?",
                    C("公开巡路与停机权","Public oversight","last_directive_open"),
                    C("紧急救难优先","Emergency shelter first","last_directive_shelter"),
                    C("继续考虑","Decide later","close"),
                    C("撤销本轮并保留证词","Abandon round, keep evidence","last_directive_abort"));return true;
            }
            Dialog(o.name,o.en,"矿工最先要求停机。随后摆渡工带回失联者的名字，最后是巡路队在夜里点燃归乡的炉火。请选择历史发生的顺序。",
                "First came the miners' shutdown petition, then the ferrymen's missing names, and last the roadwardens' refuge hearth. Choose the historical order.",
                C("矿井 → 渡口 → 炉火","Mine → Ferry → Hearth","last_directive_code_0"),
                C("炉火 → 矿井 → 渡口","Hearth → Mine → Ferry","last_directive_code_1"),
                C("渡口 → 炉火 → 矿井","Ferry → Hearth → Mine","last_directive_code_2"));return true;
        }
        if(!o.kind.StartsWith("lastdirectiveclue"))return false;
        int index=int.Parse(o.kind.Substring("lastdirectiveclue".Length));if(index<0||index>=3)return true;
        bool known=Has("last_directive_clue_"+index);
        string[] zh={"矿工留下最早的停机请求：过载不该用人的名字冲销。","随后船工发现改道，失联者不能被写成货损。","最后巡路队和居民点燃炉火，让还未回来的人有地方可去。"};
        string[] en={"The miner first demanded shutdown: human lives cannot be erased as overload expenses.","The ferryman found erased passenger names after the shutdown order.","The roadwardens lit the last hearth so anyone not yet home had a place to go."};
        if(!known){Flag("last_directive_clue_"+index);AddXp(14);sound="quest";}
        bool traced=Has("last_directive_trace_"+index);
        Dialog(o.name,o.en,
            zh[index]+(known?"":"\n首次记录经验+14。")+
                (traced?"\n\n纸边的痕迹已经记录在你的调查笔记中。":
                    "\n\n纸张背面仍有被忽略的痕迹。你可以留在现场查验。"),
            en[index]+(known?"":"\nFirst discovery: +14 XP.")+
                (traced?"\n\nThe marks along the edge are in your field notes.":
                    "\n\nThere are marks on the reverse nobody copied. You can examine them here."),
            C(traced?"重读现场记录":"查看原件背面的痕迹",
                traced?"Review field notes":"Examine marks on the original",
                "last_directive_trace_"+index),
            C("收起证词","Keep testimony","close"));return true;
    }
    private bool ChooseLastDirective(string action){
        if(!action.StartsWith("last_directive_"))return false;
        if(Dying||practicing)return true;
        if(action=="last_directive_guild"||
            action=="last_directive_guild_track")
        {
            WorldObject source=pendingDialogueSource;
            if(save.room!=0||!LastDirectiveStarted||
                LastDirectiveComplete||source==null||source.kind!="guild"||
                pendingDialogueSourceRoom!=0||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source))return true;
            if(action=="last_directive_guild_track")
            {
                TrackQuest("last_directive");return true;
            }
            Dialog(source.name,source.en,
                "“三地居民服务名册公布后，才有人敢寄来这道没有署名的旧军令。”莉雅压低声音。\n\n“矿工停机信、渡船名册和巡路队的炉火记录都在堡垒外环。你已经解决了星核危机，这只是可选的后续调查。想查就直接去现场，不需要再去议事路牌领一次任务。”",
                "“Only after we published the three civic service records did someone dare send this unsigned order,” Lyra says.\n\n“The original miners' petition, ferry list and roadwardens' hearth log remain in the citadel's outer ring. The starcore crisis is resolved; this investigation is optional. You can go straight there—no noticeboard assignment required.”",
                C("追查堡垒原件 · 标记路线",
                    "Investigate original records · Mark route",
                    "last_directive_guild_track"),
                C("先留着线索","Save this lead for later","close"));
            return true;
        }
        WorldObject board=null,core=null;foreach(WorldObject o in Map.objects){
            if(o.kind=="lastdirectiveboard")board=o;if(o.kind=="lastdirectivecore")core=o;
        }
        bool atBoard=save.room==0&&board!=null&&HasPhysicalInteractionReach(board);
        bool atCore=save.room==4&&core!=null&&HasPhysicalInteractionReach(core);
        if(action.StartsWith("last_directive_trace_",StringComparison.Ordinal))
        {
            int index;
            if(!int.TryParse(action.Substring("last_directive_trace_".Length),
                out index)||index<0||index>=3)return true;
            WorldObject source=pendingDialogueSource;
            if(save.room!=4||source==null||
                source.kind!="lastdirectiveclue"+index||
                pendingDialogueSourceRoom!=4||
                !SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source)||
                !Has("last_directive_clue_"+index))return true;
            bool first=!Has("last_directive_trace_"+index);
            if(first)
            {
                Flag("last_directive_trace_"+index);
                AddXp(8);revision++;sound="quest";
            }
            string[] detailsZh={
                "煤灰卡在信纸的折痕里。你把纸贴近光，看见矿工曾写下：\n\n“先停机，让下面的人上来。”\n\n这一行被蓝笔划去，下面的签收栏仍是空白。信没有得到答复，矿井的钟却照常响了。",
                "渡船清单的下半页被水浸透。原先每个姓名旁写着要送去的避难地点；另一支笔后来把它们改成了货号。\n\n有人在页脚添了一句：“我们把人送上了岸，不能拿货损两个字抵账。”",
                "炉火账簿不记军阶，只记夜里还有多少人没回来。最后一页夹着一根焦黑的小树枝。\n\n巡路员写道：“他们关掉了灯，我们就守着这堆火。明天还会有人顺着它找到家。”"
            };
            string[] detailsEn={
                "Coal dust clings to the fold. Held against the light, the miners' original demand appears:\n\n“Shut it down first. Get the people out.”\n\nBlue pencil strikes through the line. The acknowledgment box is blank, but the mine bell rang through another shift.",
                "Water has blurred the lower half of the ferry manifest. Each passenger once had a shelter address; another hand replaced the names with cargo numbers.\n\nSomeone wrote along the margin: “We brought people ashore. You don't get to call them lost freight.”",
                "The hearth book records no ranks. It counts how many people hadn't come home each night. A blackened twig lies between its last pages.\n\nA roadwarden wrote: “They put out the lamps. We'll keep this fire. Someone will find their way by it tomorrow.”"
            };
            Dialog(source.name,source.en,
                detailsZh[index]+(first?"\n\n调查笔记更新，经验 +8。":""),
                detailsEn[index]+(first?"\n\nField notes updated. +8 XP.":""),
                C("合上原稿","Close the record","close"));
            return true;
        }
        if(action=="last_directive_ground")
        {
            WorldObject source=pendingDialogueSource;
            if(!atCore||source==null||
                !System.Object.ReferenceEquals(source,core)||
                pendingDialogueSourceRoom!=4||
                !SourceBelongsToActiveMap(source)||
                !LastDirectiveEvidenceGroundAvailable)return true;
            if(save.energy<20){
                Notify("手动断路需要20耐力。可以继续闪避电弧。",
                    "The manual breaker needs 20 energy. You can still dodge the grid.");
                return true;
            }
            save.energy-=20;directiveEvidenceGroundUsed=true;
            // Retire ONLY this terminal's committed warnings. No living
            // guardian, foreign mission projectile or world hazard changes.
            hazards.RemoveAll(h=>h.lastDirectiveOwned);
            effects.RemoveAll(e=>e.lastDirectiveOwned);
            directiveSurgeLeft=Math.Max(directiveSurgeLeft,4.8f);
            effects.Add(new Effect("discover",Position,.6f)
                {lastDirectiveOwned=true});
            revision++;sound="guard";
            Notify("依据证词接地成功！终端脉冲暂缓，守卫仍需击败。",
                "Manual breaker grounded! The next surge is delayed; the guards still fight.");
            return true;
        }
        if(action=="last_directive_abort")
        {
            if(atCore&&directiveRoom==4&&directivePhase>=1&&directivePhase<=4)
            {
                ResetLastDirective();revision++;
                Notify("守卫战已安全撤销，三份真实证词仍保留，可以重新挑战。",
                    "Security run canceled. Three authentic testimony records remain available for retry.");
            }
            return true;
        }
        if(action=="last_directive_track"){if(atBoard&&LastDirectiveStarted)TrackQuest("last_directive");return true;}
        if(action=="last_directive_begin"){
            if(!atBoard||!Has("civic_response_ledger")||LastDirectiveStarted)return true;
            Flag("last_directive_started");TrackQuest("last_directive");sound="quest";return true;
        }
        if(action=="last_directive_claim"){
            // Only the exact original civic desk may sign an earned debt;
            // an old or forged menu from another room cannot pay it.
            if(!atBoard||!LastDirectiveTalentDue||
                !System.Object.ReferenceEquals(pendingDialogueSource,board)||
                !SourceBelongsToActiveMap(board))return true;
            if(!SafeTravel||FieldWorkPreventsRest||racing){
                Notify("附近尚不安全，公示簿仍保留欠付报酬。",
                    "Nearby fieldwork is unsafe. Your earned talent remains in the ledger.");
                return true;
            }
            if(save.skillPoints>=99){
                Notify("技能点已满99。先使用一点再回来签领。",
                    "Talent wallet full at 99. Spend one point before signing.");
                return true;
            }
            save.flags.Remove("last_directive_talent_due");
            CreditSkillPoints(1);revision++;sound="reward";
            Notify("最后指令报酬签领完成：技能点 +1。",
                "Final directive ledger signed: +1 talent point.");
            return true;
        }
        if(action=="last_directive_publish"){
            if(!atBoard||!Has("civic_response_ledger")||LastDirectiveChoice==0||LastDirectiveComplete)return true;
            bool owed=save.skillPoints>=99;
            Flag("last_directive_complete");CreditCoins(95);CreditShards(5);
            if(owed)Flag("last_directive_talent_due");
            else CreditSkillPoints(1);
            AddXp(140);revision++;sound="reward";
            RecordMoment("第十章终结：旧堡垒被迫公布最后一道隐秘指令，真正的居民决定自己的安全。",
                "Chapter Ten: the final hidden citadel order became a public civilian decision.");
            Notify(owed?
                "第十章完成：硬币95、碎片5、经验140；技能点1已记入公示簿，可稍后签领。":
                "第十章完成：硬币95、碎片5、技能点1、经验140。",
                owed?
                "Chapter Ten complete: 95 coins, 5 shards, 140 XP; +1 talent remains payable at the civic desk.":
                "Chapter Ten complete: 95 coins, 5 shards, 1 talent, 140 XP.");
            return true;
        }
        if(!atCore||!LastDirectiveReady||LastDirectiveChoice>0)return true;
        if(action.StartsWith("last_directive_code_")){
            if(directivePhase!=0||FieldWorkPreventsRest||!SafeTravel)return true;
            if(action!="last_directive_code_0"){
                Notify("时间顺序与现场证词不符。","Sequence contradicts the original testimony.");return true;
            }
            SpawnLastDirective(1);return true;
        }
        if(action=="last_directive_second"){
            if(directiveRoom!=4||directivePhase!=2||!SafeTravel)return true;
            if(save.energy<25){Notify("需要25耐力。","25 energy required.");return true;}
            if(SpawnLastDirective(2)){save.energy-=25;revision++;}
            else Notify("第二波守卫无法安全部署，第一波成果与耐力已保留。请调整战场后重试。",
                "Second-wave guards cannot safely deploy. First-wave progress and energy were preserved; retry.");
            return true;
        }
        if(action=="last_directive_open"||action=="last_directive_shelter"){
            if(directiveRoom!=4||directivePhase!=4||LastDirectiveEnemies>0||!SafeTravel)return true;
            ResetLastDirective();Flag(action);sound="reward";revision++;Notify(action=="last_directive_open"?
                "公开监管和停机申诉权已归还居民。":"无条件紧急庇护已成为公共义务。",
                action=="last_directive_open"?"Civilian oversight is restored.":"Emergency shelter is a public responsibility.");return true;
        }
        return true;
    }
}
}