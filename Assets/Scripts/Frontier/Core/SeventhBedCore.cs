using System;
namespace Starfall
{
    // A lived-in inn mystery, not an additional combat patrol. Four original
    // physically separate rooms hold mutually corroborating handwritten
    // evidence; actual cook and traveler retain their existing conversations.
    // All progress fits unmodified SaveData v2 string flags.
    public sealed partial class GameCore
    {
        public bool SeventhBedUnlocked {get{return save.stage>=1;}}
        public bool SeventhBedStarted {get{return Has("seventh_bed_started");}}
        public bool SeventhBedStamp {get{return Has("seventh_bed_stamp");}}
        public bool SeventhBedBlanket {get{return Has("seventh_bed_blanket");}}
        public bool SeventhBedLetter {get{return Has("seventh_bed_letter");}}
        public bool SeventhBedCook {get{return Has("seventh_bed_cook");}}
        public bool SeventhBedDeduction {get{return Has("seventh_bed_truth");}}
        public bool SeventhBedFalseAccusation {get{return Has("seventh_bed_misread");}}
        public bool SeventhBedPublic {get{return Has("seventh_bed_public");}}
        public bool SeventhBedPrivate {get{return Has("seventh_bed_private");}}
        public bool SeventhBedComplete {get{return SeventhBedPublic||SeventhBedPrivate;}}
        // A playable post-mystery delivery, rather than a second detective
        // dialogue. The meal and returned bowls are physical session cargo:
        // loading, changing rooms or expiring the heat abandons that run,
        // while a successful completed delivery uses a normal v2 string flag.
        private int seventhSupperPhase; // 0 idle, 1 hot bowls, 2 empty bowls
        private float seventhSupperHeat;
        private bool seventhSupperWrapped;
        public bool SeventhSupperWrapped
        {get{return SeventhSupperActive&&seventhSupperPhase==1&&seventhSupperWrapped;}}
        public bool SeventhSupperDone {get{return Has("seventh_supper_delivered");}}
        public bool SeventhSupperActive
        {get{return SeventhBedComplete&&!SeventhSupperDone&&
            save.room==9&&seventhSupperPhase>0;}}
        public bool SeventhSupperReturning
        {get{return SeventhSupperActive&&seventhSupperPhase==2;}}
        public float SeventhSupperSeconds
        {get{return SeventhSupperActive&&seventhSupperPhase==1?
            Math.Max(0f,seventhSupperHeat):0f;}}
        public Point SeventhSupperTarget
        {
            get
            {
                WorldObject o=SeventhBedObject(SeventhSupperReturning?
                    "innCook":"seventh_reply");
                return o==null?new Point(11.5f,17.5f):new Point(o.x,o.y);
            }
        }
        public string SeventhSupperObjective
        {get{return SeventhSupperReturning?
            L("带回旧碗，向阿茴交接","RETURN THE BOWLS TO ARI"):
            L("趁热将汤放进附楼的隐蔽交接处",
              "CARRY HOT SUPPER TO THE ANNEX RELAY");}}
        private void ResetSeventhSupper()
        {seventhSupperPhase=0;seventhSupperHeat=0f;seventhSupperWrapped=false;}

        public int SeventhBedEvidenceCount
        {get{return (SeventhBedStamp?1:0)+(SeventhBedBlanket?1:0)+
            (SeventhBedLetter?1:0)+(SeventhBedCook?1:0);}}
        private WorldObject SeventhBedObject(string id)
        {
            foreach(WorldObject o in data.maps[9].objects)
                if(o.id==id)return o;
            return null;
        }
        private bool SeventhBedSource(WorldObject o)
        {
            return o!=null&&save.room==9&&
                SourceBelongsToActiveMap(o)&&HasPhysicalInteractionReach(o)&&
                CanStand(Position);
        }
        public Point SeventhBedTarget
        {
            get
            {
                string id=!SeventhBedStarted?"seventh_notice":
                    !SeventhBedStamp?"seventh_stamp":
                    !SeventhBedBlanket?"seventh_cot":
                    !SeventhBedLetter?"seventh_reply":
                    !SeventhBedCook?"innCook":
                    !SeventhBedDeduction?"seventh_notice":
                    "innCook";
                WorldObject o=SeventhBedObject(id);
                return o==null?new Point(11.5f,17.5f):
                    new Point(o.x,o.y);
            }
        }
        public string SeventhBedObjective
        {
            get
            {
                return !SeventhBedStarted?
                    L("读旅店门后的第七张空床启事",
                      "READ THE SEVENTH BED NOTICE"):
                    !SeventhBedStamp?
                    L("查看前厅被重盖日期的登记收据",
                      "INSPECT THE RESTAMPED RECEIPT"):
                    !SeventhBedBlanket?
                    L("在客房查看没收走的毯子","INSPECT THE UNCLAIMED BLANKET"):
                    !SeventhBedLetter?
                    L("沿走廊进入附楼，找到墙缝里的回信",
                      "REACH THE ANNEX LETTER BEHIND THE MORTAR"):
                    !SeventhBedCook?
                    L("向原来的厨师阿茴核对晚饭和床位",
                      "QUESTION ARI ABOUT THE SUPPER AND BEDS"):
                    !SeventhBedDeduction?
                    L("回空床启事前，重新核对四份证词",
                      "RETURN TO THE NOTICE AND RECONCILE FOUR CLUES"):
                    L("向厨师提交真相，决定是否公开避难者的行踪",
                      "TELL ARI THE TRUTH AND CHOOSE THE GUESTS' FUTURE");
            }
        }
        private string SeventhBedInnNews(bool english)
        {
            if(SeventhBedPublic)return english?
                "\n\nThe inn's chalkboard now reads 'Seven reached shelter alive.' No address follows. Ari says the families asked for news to travel farther than their footsteps, but not for strangers to know their door.":
                "\n\n旅店的黑板上写着：‘七人已平安抵达。’后面没有地址。阿茴说，他们希望消息能走得比脚步远，却不想让陌生人知道自家的门。";
            if(SeventhBedPrivate)return english?
                "\n\nThe seventh bed stays unassigned on the public ledger. A small bowl is kept warm beside the cook's door every night. The traveler says it is not a trap: anyone who needs it can ask, and no name will be written.":
                "\n\n公开账册上第七张床仍空着。厨师门边每晚温着一小碗粥。旅人说那不是陷阱：需要的人可以自己来要，不用登记姓名。";
            return "";
        }
        private bool TalkSeventhBed(WorldObject site)
        {
            if(site==null||!site.kind.StartsWith("seventh",StringComparison.Ordinal))
                return false;
            if(!SeventhBedSource(site))return true;
            if(!SeventhBedUnlocked)
            {
                Dialog(site.name,site.en,
                    "启事被折了一角。现在旅店还在忙着安顿第一批旅人。",
                    "The folded notice waits while the inn settles its first guests.",
                    C("收起","Leave","close"));return true;
            }
            if(SeventhBedComplete)
            {
                // The annex is a REAL distant drop point, never a clickable
                // reward from the cook, an abstract quest menu or a teleport.
                if(site.id=="seventh_cot"&&SeventhSupperActive&&
                   !SeventhSupperReturning)
                {
                    Dialog(site.name,site.en,
                        seventhSupperWrapped?
                            "七只汤碗已经用床柱边备用的干净麻布包好。潮湿的旧毯子没有被拿来接触食物。":
                            save.energy<8f?
                                "床柱上有一块干净麻布，可以替汤碗隔冷，但你还需要8点耐力才能系紧。":
                                "床柱边挂着备用的干净麻布。花8点耐力把汤碗包紧，剩余路程就能更慢地散热。那条有雨水的旧毯子仍原封不动。",
                        seventhSupperWrapped?
                            "The seven bowls are already wrapped in clean spare linen from the bedpost. The old rain-soaked blanket never touched the food.":
                            save.energy<8f?
                                "Clean spare linen hangs at the guest cot, but you need 8 stamina to tie an insulating wrap.":
                                "Clean spare linen hangs from the guest cot. Spend 8 stamina to wrap the bowls; they will cool more slowly on the remaining route. Leave the original rain-soaked blanket untouched.",
                        !seventhSupperWrapped&&save.energy>=8f?
                            C("为汤碗保温 · 耐力8","Wrap supper · 8 stamina",
                              "seventh_supper_wrap"):
                            C("继续送餐","Continue delivery","close"),
                        C("离开","Leave","close"));return true;
                }
                if(site.id=="seventh_reply"&&SeventhSupperActive&&
                   !SeventhSupperReturning)
                {
                    Dialog(site.name,site.en,
                        "墙缝的旧信下有一层干净的木架。信使会从这里取走热汤，不必公开任何人的落脚点。旁边已放好昨天洗净的碗。",
                        "Below the old mortar letter is a clean relay shelf. A messenger can collect the hot soup here without exposing anyone's refuge. Yesterday's washed bowls wait beside it.",
                        C("留下热汤，带回洗净的碗","Leave supper and carry clean bowls",
                          "seventh_supper_drop"),
                        C("暂不交接","Wait","close"));return true;
                }
                Dialog(site.name,site.en,
                    SeventhBedPublic?
                    "七个人的平安已经公示，但地址依旧没有写上去。":
                    "没有公开住客的名字，灶台边却总有为晚归者留的一碗粥。",
                    SeventhBedPublic?
                    "Seven survivors are now publicly acknowledged. Their location stays confidential.":
                    "No names went public, but the kitchen keeps a bowl warm for late travelers.",
                    C("收起","Leave","close"));return true;
            }
            if(site.id=="seventh_notice")
            {
                if(!SeventhBedStarted)
                {
                    Dialog(site.name,site.en,
                        "阿茴的字压得很深：\n‘第七张床昨夜空了。有人说七个逃难的人偷走了口粮，也有人说他们连夜被巡兵带走。若你真想问，先把门边的收据、客房毯子和附楼那封信都看一眼。别听我一面之词。’",
                        "Ari pressed hard with her pencil:\n'The seventh bed emptied last night. Some say seven refugees stole rations; others say patrols took them. Before you ask me to decide, check the front receipt, the blanket and the letter in the annex. Don't take my word alone.'",
                        C("调查第七张空床","Investigate the seventh bed","seventh_begin"),
                        C("稍后再问","Not yet","close"));
                    return true;
                }
                if(SeventhBedEvidenceCount<4)
                {
                    Dialog(site.name,site.en,
                        "启事边角已经沾了炉灰。纸上添了你留下的记号：证据还差 "+(4-SeventhBedEvidenceCount)+" 份。别让好奇心变成一张冤枉人的告示。",
                        "Ash darkens the notice. Your pencil marks show "+(4-SeventhBedEvidenceCount)+" missing testimonies. Curiosity is no reason to accuse a stranger.",
                        C("继续调查","Continue","close"));return true;
                }
                if(!SeventhBedDeduction)
                {
                    Dialog(site.name,site.en,
                        "收据改了日期，却没有少一块面包；毯子带着雨水，不带血；附楼的信提到七个人自愿把孩子送去遮雨的旧仓；阿茴亲眼分过七碗汤。\n\n你准备在启事上写下什么？",
                        "The receipt date was altered, but no bread vanished. The blanket is wet, not bloody. The annex letter describes seven workers carrying children to a covered storehouse. Ari served seven bowls.\n\nWhat will you write on the notice?",
                        C("七个人偷走了口粮","Seven guests stole the rations","seventh_accuse"),
                        C("巡兵掳走了全部住客","The patrol abducted the guests","seventh_abduct"),
                        C("他们主动避难，记录被改以保护落脚处",
                          "They chose refuge; the ledger hides their location","seventh_reconstruct"),
                        C("暂不下判断","Leave it unanswered","close"));
                    return true;
                }
                Dialog(site.name,site.en,
                    "你没有把任何人写成贼。真相已经交到阿茴手中：七个人还活着，避难地点应该由他们自己决定是否公开。",
                    "You named no thieves. The truth is ready for Ari: seven people survived, and their refuge location is theirs to disclose.",
                    C("回去找阿茴","Find Ari","close"));return true;
            }
            if(!SeventhBedStarted)
            {
                Dialog(site.name,site.en,"这份旧物还没有人委托你查。先看大门后的启事。",
                    "No one has asked you to examine this old thing. Start with the notice.",
                    C("收起","Leave","close"));return true;
            }
            if(site.id=="seventh_stamp")
            {
                Dialog(site.name,site.en,
                    "收据原来写着‘七份热汤、七张床’，后来日期被擦了重盖。印泥还沾着细沙，但柜台没有一块面包少账。",
                    "The receipt first listed seven bowls and seven beds. Someone rubbed out the date and restamped it. Sand clings to the ink, but every loaf is accounted for.",
                    SeventhBedStamp?C("收起","Close","close"):
                    C("抄下原始日期和份数","Copy the original date and portions","seventh_take_stamp"));
                return true;
            }
            if(site.id=="seventh_cot")
            {
                Dialog(site.name,site.en,
                    "毯子是湿的，边缘粘着盐，并没有血。有人用木炭写了两个字：‘自愿’。床脚留下一小包没吃完的干粮。",
                    "Rain has soaked the blanket. Salt clings to its hem; there is no blood. Two charcoal marks read 'by choice.' A packet of unspent bread rests beneath the frame.",
                    SeventhBedBlanket?C("收起","Close","close"):
                    C("记下毯边的字","Record the charcoal message","seventh_take_blanket"));return true;
            }
            if(site.id=="seventh_reply")
            {
                Dialog(site.name,site.en,
                    "‘阿茴：我们七个没有偷东西。仓库顶棚还能挡雨，孩子们都在睡。你给的汤碗明早让人送回。若有人问去处，先问我们愿不愿说。’\n落款不是名字，是七道短短的铅笔划痕。",
                    "'Ari: None of us stole anything. The storehouse roof still keeps out rain. The children are asleep. We will send your bowls back tomorrow. Ask us before you give anyone our address.'\nThe signature is seven short pencil lines, not a name.",
                    SeventhBedLetter?C("收起","Close","close"):
                    C("抄下七道铅笔划痕","Copy the seven pencil marks","seventh_take_letter"));return true;
            }
            return true;
        }
        private void AppendSeventhBedWitnessChoice(WorldObject o)
        {
            if(!SeventhBedUnlocked||!SeventhBedSource(o)||
               mode!="dialog"||conversation==null)return;
            if(o.kind=="cook"&&o.id=="innCook")
            {
                if(SeventhBedComplete)
                {
                    conversation.text+=SeventhBedPublic?
                        "\n\n阿茴摸了摸灶台上的空碗。‘我只写了他们平安。住哪里，不归我告诉人。’":
                        "\n\n阿茴把粥搅匀。‘人家不愿写名字，就留碗粥，不留一张名单。’";
                    conversation.textEn+=SeventhBedPublic?
                        "\n\nAri touches the empty bowl: 'I wrote that they were safe. Their address is not mine to give away.'":
                        "\n\nAri stirs the porridge: 'If they will not write their names, keep a bowl, not a list.'";
                    // The real cook's original topics remain usable.
                    if(!SeventhSupperDone)
                    {
                        if(SeventhSupperReturning)
                            AddSeventhChoice("seventh_supper_finish",
                                "交回洗净的碗，完成夜宵交接",
                                "Return the clean bowls · finish supper run");
                        else if(!SeventhSupperActive)
                            AddSeventhChoice("seventh_supper_start",
                                "帮阿茴把热汤送到附楼的隐蔽交接处",
                                "Carry Ari's hot supper to the annex relay");
                    }
                    return;
                }
                if(SeventhBedStarted&&!SeventhBedCook)
                    AddSeventhChoice("seventh_ask_ari",
                        "问阿茴第七张床的晚饭","Ask Ari about the seventh bed's supper");
                if(SeventhBedDeduction)
                {
                    AddSeventhChoice("seventh_announce",
                        "公开七人平安，不透露避难地点",
                        "Announce seven safe · do not reveal their location");
                    AddSeventhChoice("seventh_shield",
                        "尊重证人的保密请求 · 为匿名住客留热粥",
                        "Honor anonymity · keep meals for unnamed guests");
                }
            }
            else if(o.kind=="innGuest"&&o.id=="innTraveler"&&
                    SeventhBedStarted&&!SeventhBedComplete)
                AddSeventhChoice("seventh_traveler",
                    "问守夜旅人是否见过七名住客",
                    "Ask the night traveler about the seven guests");
        }
        private void AddSeventhChoice(string action,string zh,string en)
        {
            if(conversation==null||conversation.choices==null)return;
            Choice[] old=conversation.choices;
            foreach(Choice c in old)if(c.action==action)return;
            int at=old.Length;
            if(at>0&&old[at-1].action=="close")at--;
            Choice[] next=new Choice[old.Length+1];
            Array.Copy(old,0,next,0,at);
            next[at]=C(zh,en,action);
            Array.Copy(old,at,next,at+1,old.Length-at);
            conversation.choices=next;
        }
        private bool ChooseSeventhBed(string action)
        {
            if(!action.StartsWith("seventh_",StringComparison.Ordinal))return false;
            WorldObject site=pendingDialogueSource;
            if(site==null||!SeventhBedSource(site)||
               mode!="world"||Dying||practicing)return true;
            if(action=="seventh_supper_start"||
               action=="seventh_supper_wrap"||
               action=="seventh_supper_drop"||
               action=="seventh_supper_finish")
            {
                if(!SeventhBedComplete||SeventhSupperDone||
                   racing)return true;
                if(action=="seventh_supper_start")
                {
                    if(site.id!="innCook"||site.kind!="cook"||
                       seventhSupperPhase!=0||!SafeTravel||
                       FieldWorkPreventsRest)return true;
                    seventhSupperPhase=1;seventhSupperHeat=45f;
                    revision++;sound="quest";
                    Notify("阿茴把七碗热汤交给你。45秒内走到附楼墙缝的交接架；别让汤凉了。",
                        "Ari gives you seven hot bowls. Reach the annex mortar shelf in 45 seconds before the soup cools.");
                    return true;
                }
                if(action=="seventh_supper_wrap")
                {
                    if(site.id!="seventh_cot"||site.kind!="seventhcot"||
                       seventhSupperPhase!=1||!SeventhSupperActive||
                       seventhSupperWrapped||seventhSupperHeat<=0f||
                       save.energy<8f)return true;
                    save.energy-=8f;seventhSupperWrapped=true;
                    revision++;sound="quest";
                    Notify("你用干净麻布包好七碗热汤：剩余路程的散热速度降低45%。",
                        "Clean linen secures the bowls: remaining soup cooling slows by 45%.");
                    return true;
                }
                if(action=="seventh_supper_drop")
                {
                    if(site.id!="seventh_reply"||site.kind!="seventhreply"||
                       !SeventhSupperActive||seventhSupperPhase!=1||
                       seventhSupperHeat<=0f)return true;
                    seventhSupperPhase=2;seventhSupperHeat=0f;
                    revision++;sound="quest";
                    RecordMoment(
                        "你沿着旅店真正的回廊，把热汤留在附楼无人看守的交接架上。没有问任何人的名字。昨天洗净的七只空碗在这里等着被带回厨房。",
                        "You carried supper through the inn's real corridors to the unguarded annex shelf without asking anyone's name. Seven cleaned bowls waited to go back to Ari.");
                    Notify("热汤已送达，带上洗净的七只碗返回阿茴的厨房。",
                        "Hot supper delivered. Return seven clean bowls to Ari's kitchen.");
                    return true;
                }
                if(site.id!="innCook"||site.kind!="cook"||
                   !SeventhSupperReturning||save.coins>999979)
                    return true;
                Flag("seventh_supper_delivered");
                ResetSeventhSupper();
                CreditCoins(20);AddXp(30);
                revision++;sound="reward";
                RecordMoment(SeventhBedPublic?
                    "阿茴数完七只洗净的碗，在黑板的‘七人平安’旁画了一只小汤碗。她没有写下收餐的地址，厨房和客房却添了持续的暖意。":
                    "阿茴数完七只洗净的碗，没有在账册上添一个名字。她把交接架的路线记在心里，匿名旅人的炉火此后更旺。",
                    SeventhBedPublic?
                    "Ari counts seven washed bowls and draws a small soup bowl beside 'Seven safe.' No collection address is published; the guest room grows warmer.":
                    "Ari counts seven washed bowls without adding a single name to the ledger. She remembers the relay route, and keeps the anonymous guests' stove warmer.");
                Notify("夜宵交接完成：硬币20、经验30；已建立更可靠的旅店补给。",
                    "Supper run complete: 20 coins, 30 XP; stronger safe inn relief.");
                return true;
            }
            if(action=="seventh_begin")
            {
                if(site.id!="seventh_notice"||!SeventhBedUnlocked||
                   SeventhBedStarted)return true;
                Flag("seventh_bed_started");TrackQuest("seventh_bed");
                RecordMoment(
                    "旅店第七张床空了，阿茴没有让任何人直接写下‘偷窃’或者‘被掳’。她让你先问完所有人。",
                    "The inn's seventh bed is empty. Ari will not write 'thief' or 'abducted' until someone has actually heard the whole story.");
                Notify("调查第七张床：先读前厅被重盖日期的收据，再去客房与附楼。",
                    "Investigate the seventh bed: inspect the restamped receipt, guest room and annex.");
                revision++;sound="quest";return true;
            }
            if(!SeventhBedStarted||SeventhBedComplete)return true;
            if(action=="seventh_take_stamp"||action=="seventh_take_blanket"||
               action=="seventh_take_letter")
            {
                string id=action=="seventh_take_stamp"?"seventh_stamp":
                    action=="seventh_take_blanket"?"seventh_cot":"seventh_reply";
                string flag=action=="seventh_take_stamp"?"seventh_bed_stamp":
                    action=="seventh_take_blanket"?"seventh_bed_blanket":
                    "seventh_bed_letter";
                if(site.id!=id||Has(flag))return true;
                Flag(flag);revision++;sound="quest";
                Notify("你将原件上的字样记录下来。线索 "+SeventhBedEvidenceCount+"/4。",
                    "You copied the original handwriting. Evidence "+
                    SeventhBedEvidenceCount+"/4.");
                return true;
            }
            if(action=="seventh_ask_ari")
            {
                if(site.kind!="cook"||site.id!="innCook"||SeventhBedCook)return true;
                Flag("seventh_bed_cook");
                RecordMoment(
                    "阿茴拿起一只缺口的碗：‘他们吃完才走的。七碗汤我一碗一碗端出去，孩子们先吃。若是被抓走，哪能把碗洗净送回来？’",
                    "Ari lifts a chipped bowl. 'They ate before leaving. I carried seven bowls outside. The children ate first. If patrols took them, who returned the washed dishes?'");
                revision++;sound="quest";
                Notify("厨师证言已记录：七碗汤全部端出，缺口的碗洗净送回。",
                    "Ari's testimony recorded: all seven bowls were served and returned clean.");
                return true;
            }
            if(action=="seventh_traveler")
            {
                if(site.kind!="innGuest"||site.id!="innTraveler")return true;
                Dialog(site.name,site.en,
                    "‘我见过他们过走廊。有人抱着孩子，有人抱着还温热的碗。没看见锁链，也没看见挨饿的人。想知道他们为什么离开，去看附楼墙缝里的信。’",
                    "'I watched them pass the corridor. One carried a child; another held a warm bowl. No chains. Nobody starving. For why they left, read the letter behind the annex mortar.'",
                    C("回到旅人的话题","Back to the traveler's topics","inn_topics"),
                    C("离开","Leave","close"));return true;
            }
            if(action=="seventh_accuse"||action=="seventh_abduct"||
               action=="seventh_reconstruct")
            {
                if(site.id!="seventh_notice"||SeventhBedEvidenceCount!=4||
                   SeventhBedDeduction)return true;
                if(action!="seventh_reconstruct")
                {
                    Flag("seventh_bed_misread");revision++;sound="alert";
                    Notify(action=="seventh_accuse"?
                        "你把他们叫作贼，阿茴指着整整齐齐的面包账：‘那少了什么？’请重新核对证据。":
                        "你怀疑巡兵掳人，但孩子的信和洗净的碗对不上。请重新核对证据。",
                        action=="seventh_accuse"?
                        "You accused them of theft. Ari points to the intact bread ledger: 'What went missing?' Reconsider.":
                        "You suspected an abduction. The children's letter and washed bowls contradict it. Reconsider.");
                    return true;
                }
                Flag("seventh_bed_truth");revision++;sound="quest";
                RecordMoment(
                    "七个人没有逃账，也没有被带走。他们主动转移孩子避雨，故意模糊日期，是怕巡兵通过账册找到藏身处。你把‘失踪’改成了‘平安’，没有添上地址。",
                    "Seven did not flee their debt or fall to a patrol. They took children out of the rain and blurred the date so the ledger could not reveal their refuge. You wrote 'safe' in place of 'missing,' leaving the address blank.");
                Notify("正确推理！七人活着，住客所在地属于他们自己。回阿茴处作最终决定。",
                    "Deduction solved: seven survived. Their location is theirs to share. Return to Ari.");
                return true;
            }
            if(action=="seventh_announce"||action=="seventh_shield")
            {
                if(site.kind!="cook"||site.id!="innCook"||
                   !SeventhBedDeduction||SeventhBedComplete)return true;
                if(save.coins>999954||save.shards>99998)return true;
                bool publicNews=action=="seventh_announce";
                Flag(publicNews?"seventh_bed_public":"seventh_bed_private");
                CreditCoins(45);CreditShards(1);AddXp(90);
                if(!SeventhBedFalseAccusation)CreditSkillPoints(1);
                revision++;sound="reward";
                RecordMoment(publicNews?
                    "阿茴把‘七人平安’写在黑板上，拒绝写下仓库的位置。晚归住客多了一张不问姓名也能歇脚的床。":
                    "阿茴没有公布七个人的名单。她把炉边一碗热粥留到深夜，让不敢登记姓名的人也敢走进来。",
                    publicNews?
                    "Ari writes 'Seven safe' on the chalkboard, refusing to post their address. The guest room now welcomes survivors without demanding names.":
                    "Ari leaves the seven names off the wall. One bowl stays warm after midnight for guests too afraid to sign a register.");
                Notify(publicNews?
                    "第七张床：硬币45、碎片1、经验90；炉火客房安全缓慢疗伤。":
                    "第七张床：硬币45、碎片1、经验90；厨师灶台安全恢复耐力。",
                    publicNews?
                    "Seventh Bed: 45 coins, 1 shard, 90 XP; safe guest room recovery.":
                    "Seventh Bed: 45 coins, 1 shard, 90 XP; safe energy at Ari's kitchen.");
                return true;
            }
            return true;
        }
        private void TickSeventhBed(float dt)
        {
            if(SeventhSupperActive&&seventhSupperPhase==1&&
               mode=="world"&&dt>0f)
            {
                seventhSupperHeat=Math.Max(0f,seventhSupperHeat-
                    (seventhSupperWrapped?.55f:1f)*dt);
                if(seventhSupperHeat<=0f)
                {
                    ResetSeventhSupper();revision++;sound="alert";
                    Notify("热汤已经凉了，不能再交给夜间信使。回阿茴处取新的热汤重试。",
                        "The supper went cold. Return to Ari for fresh bowls and retry.");
                }
            }
            if(!SeventhBedComplete||save.room!=9||Dying||
               !SafeTravel||FieldWorkPreventsRest)return;
            WorldObject site=SeventhBedObject(SeventhBedPublic?"innRest":"innCook");
            if(site==null||!PhysicalRefugeReach(new Point(site.x,site.y),1.8f))return;
            if(SeventhBedPublic)
                save.health=Math.Min(save.maxHealth,save.health+
                    (SeventhSupperDone?2.5f:1.8f)*dt);
            else if(SeventhBedPrivate)
                save.energy=Math.Min(100f,save.energy+
                    (SeventhSupperDone?4.7f:3.5f)*dt);
        }
    }
}
