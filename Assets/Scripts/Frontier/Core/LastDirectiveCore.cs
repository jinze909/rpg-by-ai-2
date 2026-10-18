using System;
namespace Starfall {
public sealed partial class GameCore {
    private int directiveRoom=-1, directivePhase, directiveKills;
    private float directiveSurgeLeft;
    private static readonly Point[] directiveSpawns={new Point(98.5f,74.5f),new Point(104.5f,74.5f),new Point(98.5f,74.5f),new Point(104.5f,74.5f),new Point(106.5f,73.5f)};
    public bool LastDirectiveStarted {get{return Has("last_directive_started");}}
    public bool LastDirectiveComplete {get{return Has("last_directive_complete");}}
    public int LastDirectiveClues {get{int n=0;for(int i=0;i<3;i++)if(Has("last_directive_clue_"+i))n++;return n;}}
    public int LastDirectiveWave {get{return directiveRoom==4?directivePhase:0;}}
    public int LastDirectiveEnemies {get{int n=0;foreach(Enemy e in enemies)if(IsLastDirectiveEnemy(e))n++;return n;}}
    public bool LastDirectiveReady {get{return LastDirectiveStarted&&LastDirectiveClues==3;}}
    public int LastDirectiveChoice {get{return Has("last_directive_open")?1:Has("last_directive_shelter")?2:0;}}
    private bool IsLastDirectiveEnemy(Enemy e){return e!=null&&e.id>=90300&&e.id<=90304;}
    private void ResetLastDirective(){enemies.RemoveAll(IsLastDirectiveEnemy);projectiles.RemoveAll(p=>p.lastDirectiveOwned);effects.RemoveAll(e=>e.lastDirectiveOwned);hazards.RemoveAll(h=>h.lastDirectiveOwned);
        directiveRoom=-1;directivePhase=directiveKills=0;directiveSurgeLeft=0;}
    private void OnLastDirectiveEnemyDefeated(Enemy e){
        if(directiveRoom!=4||save.room!=4||(directivePhase!=1&&directivePhase!=3)||!IsLastDirectiveEnemy(e))return;
        int first=directivePhase==1?90300:90302;int count=directivePhase==1?2:3;
        if(e.id>=first&&e.id<first+count)directiveKills|=1<<(e.id-first);
    }
    private void TickLastDirective(float dt){
        if(directiveRoom<0)return;
        if(save.room!=4||Dying||practicing){ResetLastDirective();return;}
        if(directivePhase!=1&&directivePhase!=3)return;
        int first=directivePhase==1?90300:90302,count=directivePhase==1?2:3;
        bool clear=true;
        for(int i=0;i<count;i++){
            bool present=enemies.Exists(e=>e.id==first+i);
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
        projectiles.RemoveAll(p=>p.lastDirectiveOwned);effects.RemoveAll(e=>e.lastDirectiveOwned);
        hazards.RemoveAll(h=>h.lastDirectiveOwned);revision++;sound="reward";
        Notify(directivePhase==2?"第一道保密卫队已击败。在终端以25耐力解除二级封锁。":"三名最终守卫已击败，回控制台决定公共权力归属。",
            directivePhase==2?"First wave defeated. Spend 25 energy at the terminal to lift the second lock.":"All three final guards defeated. Choose public control at the terminal.");
    }
    private bool SpawnLastDirective(int wave){
        if(!LastDirectiveReady||save.room!=4||directiveRoom>=0||Dying||practicing||!SafeTravel||LastDirectiveChoice>0)return false;
        int first=wave==1?0:2,count=wave==1?2:3;
        for(int i=0;i<count;i++)if(!CanStand(directiveSpawns[first+i]))return false;
        directiveRoom=4;directivePhase=wave==1?1:3;directiveKills=0;
        directiveSurgeLeft=wave==2?2.6f:0;
        for(int i=0;i<count;i++){
            Point p=directiveSpawns[first+i];int kind=wave==1?i:i==2?2:i;
            enemies.Add(CreatePlacedEnemy(90300+first+i,kind,p,wave==1?79+12*i:111+17*i));
            effects.Add(new Effect("danger",p,.8f){radius=.6f,lastDirectiveOwned=true});
        }
        revision++;sound="windup";
        Notify(wave==1?"堡垒二名保密卫兵真实出击！":"二级封锁启动：近战、弓手和重甲守卫共同袭击！",
            wave==1?"Two real citadel guardians engage.":"Second lock: melee, archer and armored guardians engage!");
        return true;
    }
    private bool TalkLastDirective(WorldObject o){
        if(!o.kind.StartsWith("lastdirective"))return false;
        if((Position-new Point(o.x,o.y)).Length>2.2f)return true;
        if(o.kind=="lastdirectiveboard"){
            if(save.room!=0)return true;
            if(!Has("civic_response_ledger")){
                Dialog(o.name,o.en,"先完成三地的居民救援实战，再公开最后的保密令。","Publish all three civilian service records before investigating the unsigned order.",C("继续调查","Keep investigating","close"));return true;
            }
            if(LastDirectiveComplete){
                Dialog(o.name,o.en,LastDirectiveChoice==1?"民间监督与停机申诉权已经公开。":"民用紧急庇护已成为任何人的权利。",
                    LastDirectiveChoice==1?"Public civilian oversight and shutdown appeals are now law.":"Unconditional emergency shelter is now guaranteed.",C("查阅记录","Review","close"));return true;
            }
            if(LastDirectiveChoice>0){
                Dialog(o.name,o.en,"你在真正的堡垒战斗后选定了最后一道权限。现在让所有人读到它。","The last directive was reclaimed through real battles. Publish the choice for all.",
                    C("公开最后指令","Publish the last directive","last_directive_publish"),C("稍后","Later","close"));return true;
            }
            Dialog(o.name,o.en,LastDirectiveStarted?
                "矿工、摆渡人和巡路队的证词散落在堡垒外围。收回三份原始记录，解开九年前封存的最后指令。":
                "莉雅找到九年前没有署名的军用指令。被封锁的停机按钮究竟属于谁？沿堡垒外环走访当事人的证据，而不是依照旧长官的命令。",
                LastDirectiveStarted?"Three authentic testimonies survive on the citadel's outer trails. Read them at the source.":
                "Lyra discovered an unsigned nine-year-old order. Follow three physical testimonies in the citadel's outer region.",
                C(LastDirectiveStarted?"追踪现场证据":"接取第十章 · 最后指令",
                    LastDirectiveStarted?"Track the testimonies":"Begin Chapter Ten · Last Directive",
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
                Dialog(o.name,o.en,
                    "守卫正在真实战斗。若需要撤退，在这个原始终端明确撤销本轮即可，已取得的证词仍保留。",
                    "Authentic security units are attacking. Withdraw only here at the original terminal; earned evidence stays recorded.",
                    C("返回战斗","Return to fight","close"),
                    C("撤销本轮守卫战","Withdraw from defense","last_directive_abort"));return true;
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
        Dialog(o.name,o.en,zh[index]+(known?"":"\n首次记录经验+14。"),en[index]+(known?"":"\nFirst discovery: +14 XP."),
            C("收起证词","Keep testimony","close"));return true;
    }
    private bool ChooseLastDirective(string action){
        if(!action.StartsWith("last_directive_"))return false;
        if(Dying||practicing)return true;
        WorldObject board=null,core=null;foreach(WorldObject o in Map.objects){
            if(o.kind=="lastdirectiveboard")board=o;if(o.kind=="lastdirectivecore")core=o;
        }
        bool atBoard=save.room==0&&board!=null&&(Position-new Point(board.x,board.y)).Length<=2.2f;
        bool atCore=save.room==4&&core!=null&&(Position-new Point(core.x,core.y)).Length<=2.2f;
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
        if(action=="last_directive_publish"){
            if(!atBoard||!Has("civic_response_ledger")||LastDirectiveChoice==0||LastDirectiveComplete)return true;
            Flag("last_directive_complete");CreditCoins(95);CreditShards(5);CreditSkillPoints(1);AddXp(140);revision++;sound="reward";
            RecordMoment("第十章终结：旧堡垒被迫公布最后一道隐秘指令，真正的居民决定自己的安全。",
                "Chapter Ten: the final hidden citadel order became a public civilian decision.");
            Notify("第十章完成：硬币95、碎片5、技能点1、经验140。","Chapter Ten complete: 95 coins, 5 shards, 1 talent, 140 XP.");return true;
        }
        if(!atCore||!LastDirectiveReady||LastDirectiveChoice>0)return true;
        if(action.StartsWith("last_directive_code_")){
            if(directivePhase!=0||!SafeTravel)return true;
            if(action!="last_directive_code_0"){
                Notify("时间顺序与现场证词不符。","Sequence contradicts the original testimony.");return true;
            }
            SpawnLastDirective(1);return true;
        }
        if(action=="last_directive_second"){
            if(directiveRoom!=4||directivePhase!=2||!SafeTravel)return true;
            if(save.energy<25){Notify("需要25耐力。","25 energy required.");return true;}
            ResetLastDirective();if(SpawnLastDirective(2)){save.energy-=25;revision++;}return true;
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