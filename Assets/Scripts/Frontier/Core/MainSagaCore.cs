using System;
using System.Collections.Generic;

namespace Starfall
{
    // A genuinely playable second act through the shipped regional maps.
    // All milestones are existing SaveData v2 flags; existing completed
    // four-chapter saves remain complete and their ending is never rewritten.
    public sealed partial class GameCore
    {
        public bool MainSagaActive { get { return Has("saga_act_two"); } }
        public int MainSagaVoices
        {
            get { int total=0;for(int i=5;i<=7;i++)
                if(Has("saga_voice_"+i))total++;return total; }
        }
        public int MainSagaTraces
        {
            get { int total=0;for(int i=5;i<=7;i++)
                if(Has("saga_trace_"+i))total++;return total; }
        }
        public bool MainSagaCanConclude
        {
            get { return MainSagaActive&&Has("boss_4")&&
                MainSagaVoices==3&&MainSagaTraces==3&&
                Proofs==3&&save.frontierEnding>0; }
        }
        private static string SagaPerson(int room)
        { return room==5?"captain":room==6?"tinker":room==7?"keeper":""; }
        private static string SagaPersonId(int room)
        { return room==5?"mira":room==6?"ivo":room==7?"sen":""; }
        private static string SagaReport(int room)
        { return room==5?"harbor_report":room==6?"desert_report":
            room==7?"mountain_report":""; }
        private static string SagaEvidenceId(int room)
        { return room==5?"passengers":room==6?"receiver":
            room==7?"bell2":""; }
        public Point MainSagaEvidencePoint(int room)
        {
            if(room<5||room>7)return Position;
            foreach(WorldObject objectAt in data.maps[room].objects)
                if(objectAt!=null&&objectAt.id==SagaEvidenceId(room))
                    return new Point(objectAt.x,objectAt.y);
            return new Point(data.maps[room].spawnX,data.maps[room].spawnY);
        }
        private bool SagaValidSpeaker(WorldObject source,int room)
        {
            return room>=5&&room<=7&&source!=null&&
                save.room==room&&source.id==SagaPersonId(room)&&
                source.kind==SagaPerson(room)&&
                pendingDialogueSourceRoom==room&&
                SourceBelongsToActiveMap(source)&&
                HasPhysicalInteractionReach(source)&&CanStand(Position)&&
                !Dying&&!practicing&&mode=="world";
        }
        // A real world-object interaction at each ORIGINAL evidence site.
        // Physical path, current room and named source are verified BEFORE
        // assigning the durable trace flag. Nothing can be forged via journal.
        private bool TryReadMainSagaTrace(WorldObject o)
        {
            if(!MainSagaActive||save.ending>0||save.stage<3||
                !Has("boss_4")||save.room<5||save.room>7||
                !Has(SagaReport(save.room))||
                Has("saga_trace_"+save.room)||o==null||
                o.id!=SagaEvidenceId(save.room)||
                !SourceBelongsToActiveMap(o)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position)||
                Dying||practicing)return false;
            int room=save.room;
            string[] zh={
                "","", "", "", "",
                "名册的最后一页压着两层不同颜色的墨。上层是港务处的“事故损失”，下层却写着米拉亲笔抄下的救援船号。她没有向任何人说过这第二份名单。\n\n北岸不是所有乘客的归途：有人没有等到船。现在你知道为何米拉每次说“全员”都会突然停下来。",
                "蓄光池背面的散热铭牌不是制造商的标志，而是一串改写过的供水优先级。最老的编号指向九年前，一行字被手工刮掉：先供儿童避暑棚。\n\n伊沃在自己的设计图上改写了它。他曾经知道这座机器会牺牲谁，却没能把真相告诉家人。",
                "第三座旧钟的背后嵌着一枚没有刻完的名牌。钟锤内侧缠着染血的绳。那天的钟声不是机器坏了，是有人亲手捂住警报，让一群孩子避开了即将关闭的山门。\n\n森也在撤离名单上。他不是旁观者。"
            };
            string[] en={
                "","", "", "", "",
                "Two inks stain the last manifest page. The upper ledger calls the passengers 'losses'. Beneath it, Mira copied the names of a rescue boat in her own hand. She has never told anyone about that second list.\n\nThe north bank was not everyone's way home. Some never reached the boat. Now you know why Mira stops whenever she says 'all passengers'.",
                "Behind the light reservoir, a cooling plate carries nine-year-old supply priorities. One line is scraped almost clean: CHILDREN'S SHADE SHELTER FIRST.\n\nIvo rewrote the mechanism himself. He knew whom it would deprive of water. His family never heard him explain why.",
                "A half-engraved nameplate is wedged behind the third chime. Blood darkens a rope wound around its hammer. The alarm did not break: somebody muffled it to keep children out of a closing mountain gate.\n\nSen appears on the evacuation roll. He was never only a witness."
            };
            Flag("saga_trace_"+room);
            RecordMoment("第二幕 · "+new[]{"","","","","","港口第二份名单","沙海被刮去的供水令","高原钟锤中的名牌"}[room]+
                "：在真实现场发现了尚未解释的证据。",
                "Act Two: recovered physical evidence of an untold decision in "+
                new[]{"","","","","","Mistport","the Glass Desert","Frostchime"}[room]+".");
            revision++;sound="quest";
            Dialog(o.name,o.en,zh[room],en[room],
                C("带证据回去听当事人讲完","Ask the witness what happened","close"));
            return true;
        }
        private void AppendMainSagaChoice(WorldObject source)
        {
            if(conversation==null||conversation.choices==null||mode!="dialog"||
                source==null||save.ending>0||save.stage<3)return;
            string action=null,zh=null,en=null;
            if(source.kind=="guild"&&save.room==0)
            {
                action=MainSagaActive?"saga_debrief":"saga_begin";
                zh=MainSagaActive?"主线 · 与莉雅追查旧供能命令":
                    "主线 · 追查第四章背后的真相";
                en=MainSagaActive?"Main story · investigate the old orders":
                    "Main story · uncover the truth beyond Chapter Four";
            }
            else if(MainSagaActive&&Has("boss_4")&&save.room>=5&&
                save.room<=7&&source.id==SagaPersonId(save.room)&&
                source.kind==SagaPerson(save.room)&&
                Has(SagaReport(save.room)))
            {
                action="saga_account_"+save.room;
                zh=Has("saga_voice_"+save.room)?"重访我们签下的证词":
                    "主线 · 听完未曾公开的证词";
                en=Has("saga_voice_"+save.room)?"Revisit our signed testimony":
                    "Main story · hear the unspoken testimony";
            }
            if(action==null)return;
            Choice[] before=conversation.choices;
            foreach(Choice c in before)if(c!=null&&c.action==action)return;
            int insert=before.Length;
            if(insert>0&&before[insert-1]!=null&&
                before[insert-1].action=="close")insert--;
            var after=new Choice[before.Length+1];
            Array.Copy(before,0,after,0,insert);
            after[insert]=C(zh,en,action);
            Array.Copy(before,insert,after,insert+1,before.Length-insert);
            conversation.choices=after;
        }
        private void MainSagaDebrief(WorldObject guild)
        {
            Dialog(guild.name,guild.en,
                "莉雅从三份星核收据中抽出同一张红色复写单。上面没有厄洛的签名，只有一道被划去的命令：“外环停供，保全中央。”\n\n“我过去也以为一切都从堡垒开始。可雾港的失踪日期更早，沙海的配给单则早了九年。有人在灾难发生前就重新排序了谁能分到水和光。”\n\n“先听米拉、伊沃和森自己讲。去他们的真实现场，再带着证据回来。别让一个新的英雄替他们决定什么可以公开。”",
                "Lyra draws a red carbon copy from the three starcore receipts. Orrin's signature is missing; one crossed-out order reads: CUT THE OUTER RING. SAVE THE CENTER.\n\n“I thought everything began at the citadel. But Mistport's disappearances came earlier, and the desert ration order is nine years older. Someone ranked the people who would receive water and light before the disaster.”\n\n“Hear Mira, Ivo and Sen in their own words. Visit their actual evidence sites, then ask them. We cannot put a new hero in charge of which truth may be told.”",
                C("追踪真正的第二幕主线","Track the second act","saga_track"),
                C("我会带他们的回答回来","I'll bring their answers","close"));
        }
        private void MainSagaAccount(WorldObject host,int room)
        {
            if(!Has("saga_trace_"+room))
            {
                string[] sitesZh={"","","","","","西北码头的旧乘客名册","蓄光池东侧的接收台","高原第三座旧钟"};
                string[] sitesEn={"","","","","","the old northwest passenger manifest","the eastern reservoir receiver","the third mountain chime"};
                Dialog(host.name,host.en,
                    "“你带来了公开的调查记录，但我还有一件不能藏在报告里的事。先去看"+sitesZh[room]+"。不看见现场，我说什么都只像是在替自己开脱。”",
                    "“You've read the public report. There is something I left out. First go see "+
                    sitesEn[room]+". Without that evidence, everything I say will sound like an excuse.”",
                    C("亲自去调查","Investigate in person","close"));
                return;
            }
            if(Has("saga_voice_"+room))
            {
                string note=Has("saga_choice_"+room+"_a")?
                    "那份原始供词已经公开。居民可以追问，也可以不同意。":
                    "你尊重了证人的保留与说明方式，但没有抹去真实的事件。";
                string noteEn=Has("saga_choice_"+room+"_a")?
                    "The first-hand account is public. Residents may question it or disagree.":
                    "You respected the witness's right to explain, without erasing what happened.";
                Dialog(host.name,host.en,note,noteEn,
                    C("带上这份证词继续前行","Carry the testimony onward","close"));
                return;
            }
            string[] zh={"","","","","",
                "米拉终于把手从船舵上移开。“我确实在北岸藏过一艘救援船。我想保住船上几个没有通行证的孩子，就把他们的名字从官方名册里删掉了。后来有人死在另一条航路上。我不敢把两种事放在同一页。”\n\n“你可以把我的签名也附在公开名单上，让别人知道是谁撒了谎。也可以先让失踪者家属拿到原件，我自己去向他们解释。你来作见证，不要替我说‘没有代价’。”",
                "伊沃把设计图翻到背面。“九年前，我把救急用水改送到了城里。我的儿子当时就躺在避暑棚，我以为只是一夜。第二天外环的井就干了。”\n\n“我画过一份改回去的线路图，却不敢盖自己的印。公开我当年的决定，或者把完整的改造方案交给所有井站，让它们不再依赖我的勇气。无论选哪条，我都得站在这里承认是我做的。”",
                "森取下那枚断裂的钟牌。“当年我阻止了警报。若守军听见，就会封闭北门，把躲进去的孩子赶出来。我救了他们，但后面的撤离者因此错过钟声。”\n\n“我不想让死去的人替我背负‘钟坏了’的谎。我能当众讲出自己的名字；也能先带你去找那群孩子，让他们说那一夜是什么样。不要把这两种人写成敌人。”"};
            string[] en={"","","","","",
                "Mira releases the ship's wheel. “I hid a rescue boat on the north bank. To protect children without travel permits, I struck their names from the official roll. Others died on a different crossing. I could not bring myself to put both truths on one page.”\n\n“You can attach my signature to the public manifest. Or give the originals to the missing families first, and I'll explain it to them myself. Witness what happened. Do not call it a rescue without a cost.”",
                "Ivo turns over his diagrams. “Nine years ago I diverted emergency water toward the city. My son was lying in the shade shelter. I thought it would last one night. By morning the outer wells were dry.”\n\n“I drew a route to reverse the diversion but never signed it. Publish my decision, or distribute the complete repair plans to every well station so no one needs to depend on my courage again. Either way I will admit what I did.”",
                "Sen removes the broken chime tag. “I stopped the alarm. If the guards had heard it, they would have sealed the north gate and expelled the children sheltering inside. I saved them. The evacuees behind them missed the signal.”\n\n“I won't let the dead carry the lie that the bell simply broke. I can name myself in public. Or we can find those children first and let them describe that night. Please don't turn these two groups into enemies.”"};
            string[] first={"","","","","",
                "公开米拉署名的完整名册",
                "公布伊沃亲自改写的配额决定",
                "请森公开承认敲钟之夜的决定"};
            string[] firstEn={"","","","","",
                "Publish the complete manifest under Mira's name",
                "Publish Ivo's signed ration decision",
                "Ask Sen to testify publicly about the silenced bell"};
            string[] second={"","","","","",
                "让失踪者家属先读原件，再共同公开",
                "把修复图交给所有井站并保存他的供词",
                "先听获救孩子和迟到的撤离者一起讲述"};
            string[] secondEn={"","","","","",
                "Give the originals to families first, then publish together",
                "Distribute repair diagrams and preserve his confession",
                "Hear survivors and late evacuees together first"};
            Dialog(host.name,host.en,zh[room],en[room],
                C(first[room],firstEn[room],"saga_sign_"+room+"_a"),
                C(second[room],secondEn[room],"saga_sign_"+room+"_b"),
                C("再想一想","I need to think","close"));
        }
        private string MainSagaFinalReveal(bool english)
        {
            if(!MainSagaCanConclude)return "";
            return english?
                "\n\nOrrin unfolds the old supply order beside the new covenant. Under the scraped-off military heading lie three older civilian routing stamps. The first outer-ring diversion predates his command by nine years.\n\n“I signed orders that hurt people,” he says. “But the machine I inherited made every bad choice look like nobody's choice. Mira hid names to save one boat. Ivo diverted water to save his child. Sen silenced a bell to protect children at a gate. Others paid for each act. None is a monster; none may vanish from the record.”\n\nLyra lays down the witnesses' own signatures. The question now is not who rules three cores, but which risks the people themselves will agree to carry.":
                "\n\n厄洛把旧供能令摊在新的协定旁。刮去的军方标题下，有三个更早的民用调度印。外环第一次被牺牲，比他接管堡垒早九年。\n\n“伤人的命令，我确实签过。但我继承的也是一部把每个坏决定变成‘没人负责’的机器。米拉为了救一艘船藏起名字，伊沃为了儿子改了供水，森为了保住孩子捂住警钟。代价却落在另一群人身上。他们谁都不是怪物，也谁都不能从记录里消失。”\n\n莉雅放下三份当事者亲签的证词。现在决定的不再是三枚星核归哪个统治者，而是这些居民愿意共同承担怎样的风险。";
        }
        private string MainSagaEndingAfterword(bool english)
        {
            if(!MainSagaActive||MainSagaVoices<3||MainSagaTraces<3)return "";
            return english?
                "\n\nA second volume of first-hand accounts enters the public archive. "+
                (Has("saga_choice_5_a")?"Mira signs the open manifest. ":"Mira lets families read it first. ")+
                (Has("saga_choice_6_a")?"Ivo publishes his old ration decision. ":"Every well receives Ivo's repair plan. ")+
                (Has("saga_choice_7_a")?"Sen names his choice at the public hearing. ":"Rescued children and late evacuees tell their story together. ")+
                "No signature erases a death, but people can now challenge the next secret order before it takes effect.":
                "\n\n边境公共档案终于添上第二册。"+
                (Has("saga_choice_5_a")?"米拉在公开的名册上亲笔署名。":"米拉先让失踪者家属读完原件。")+
                (Has("saga_choice_6_a")?"伊沃公开了自己九年前改写的配额。":"每座井站都收到了伊沃的维修图。")+
                (Has("saga_choice_7_a")?"森在听证会上说出自己的选择。":"获救的孩子与错过撤离的人一起讲完那夜。")+
                "任何签名都无法抹平伤亡，但居民终于能在下一道秘密命令生效前提出质疑。";
        }
        private void MainSagaGate()
        {
            string state=MainSagaVoices<3?
                "三位当事人的证词还没有全部带来。":
                save.frontierEnding==0?
                "三地证词已经在手，但边境的共同供能方案尚未由三方议会签署。":
                "你仍需带回各地的原始调查记录。";
            string enState=MainSagaVoices<3?
                "The three first-hand accounts are not yet complete.":
                save.frontierEnding==0?
                "The testimonies are here, but the three-party council has not signed an energy covenant.":
                "The original regional investigations are still incomplete.";
            Dialog("厄洛 · 星卫统领","Orrin · Starwarden",
                "厄洛看着堡垒墙上的旧供能图。“我愿意为自己的选择负责。但你从远岸带回的证据说明，牺牲外环的决定比我的命令还要早。不能只在这里决定一次，就让所有人以为事情已经结束。”\n\n"+state+
                "\n\n“去问那些曾经被算作数字的人。等他们也有权决定，才谈星核归属。”",
                "Orrin studies the old supply map. “I will answer for my choices. But your farshore evidence shows the outer ring was sacrificed long before my command. We cannot settle this with one decision here and pretend that is the end.”\n\n"+enState+
                "\n\n“Ask those once reduced to numbers. When they can take part in the decision, come back for the starcores.”",
                C("查看主线下一步","Review the next main chapter","saga_track"),
                C("回镇上找莉雅","Return to Lyra","goto0"),
                C("继续调查","Continue investigating","close"));
        }
        private bool ChooseMainSaga(string action)
        {
            if(string.IsNullOrEmpty(action)||
               !action.StartsWith("saga_",StringComparison.Ordinal))return false;
            WorldObject host=pendingDialogueSource;
            if(action=="saga_begin"||action=="saga_debrief")
            {
                if(host==null||host.kind!="guild"||
                    save.room!=0||pendingDialogueSourceRoom!=0||
                    !SourceBelongsToActiveMap(host)||
                    !HasPhysicalInteractionReach(host)||!CanStand(Position)||
                    Dying||practicing||save.stage<3||save.ending>0)return true;
                if(!MainSagaActive)
                {
                    Flag("saga_act_two");
                    TrackQuest("main");revision++;sound="quest";
                }
                MainSagaDebrief(host);
                return true;
            }
            if(action=="saga_track")
            {
                // A previously validated physical guild/citadel conversation
                // can track the real main quest; no milestones are fabricated.
                if(!MainSagaActive||save.ending>0)return true;
                TrackQuest("main");
                journalFocusRequest="main";mode="journal";
                return true;
            }
            if(action.StartsWith("saga_account_",StringComparison.Ordinal))
            {
                int room;
                if(!int.TryParse(action.Substring(13),out room)||
                    !MainSagaActive||save.ending>0||
                    !Has("boss_4")||!SagaValidSpeaker(host,room)||
                    !Has(SagaReport(room)))return true;
                MainSagaAccount(host,room);
                return true;
            }
            if(action.StartsWith("saga_sign_",StringComparison.Ordinal))
            {
                string[] bits=action.Substring(10).Split('_');
                int room;
                if(bits.Length!=2||!int.TryParse(bits[0],out room)||
                    (bits[1]!="a"&&bits[1]!="b")||
                    !MainSagaActive||save.ending>0||
                    !Has("boss_4")||!SagaValidSpeaker(host,room)||
                    !Has(SagaReport(room))||!Has("saga_trace_"+room)||
                    Has("saga_voice_"+room))return true;
                Flag("saga_voice_"+room);
                Flag("saga_choice_"+room+"_"+bits[1]);
                RecordMoment("第二幕 · "+host.name+
                    "在亲眼查验的证据前签下证词。"+
                    (bits[1]=="a"?"原件与责任一同公开。":"先让当事者参与决定证词的发布。"),
                    "Act Two: "+host.en+
                    " signed a first-hand account against inspected evidence. "+
                    (bits[1]=="a"?"The original record and responsibility were made public.":
                    "The affected people helped decide how to release the record."));
                revision++;sound="quest";
                Dialog(host.name,host.en,
                    "“谢谢你没有只在公会账上给我画一个勾。我的名字会跟着这份供词，不会因为星核终于归位就消失。”\n\n"+
                    "三份证词进度："+MainSagaVoices+"/3。",
                    "“Thank you for not treating me as one more checked box in a guild ledger. My name stays with this account, even when the starcores find their place.”\n\n"+
                    "First-hand accounts: "+MainSagaVoices+"/3.",
                    C("继续第二幕","Continue Act Two","close"));
                return true;
            }
            return true;
        }
    }
}
