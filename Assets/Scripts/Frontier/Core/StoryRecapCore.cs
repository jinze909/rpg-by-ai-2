using System;
using System.Collections.Generic;

namespace Starfall
{
    // Read-only journal projection from actual permanent campaign milestones.
    // It does not award progression, invent dialogue or alter SaveData v2.
    public sealed class StoryBeat
    {
        public readonly int chapter;
        public readonly string title, summary, next, questId;
        public readonly bool complete;
        public StoryBeat(int chapter, string title, string summary,
            string next, string questId, bool complete)
        {
            this.chapter=chapter;this.title=title;this.summary=summary;
            this.next=next;this.questId=questId;this.complete=complete;
        }
    }

    public sealed partial class GameCore
    {
        private void AddRecapBeat(List<StoryBeat> beats,int chapter,
            string nameZh,string nameEn,string summaryZh,string summaryEn,
            string questId,bool complete,string finishedZh,string finishedEn)
        {
            // Look up the actual quest at the time the player reads this page,
            // rather than snapshotting a stale hint when the journal opened.
            QuestGuide quest=GetQuest(questId);
            string next=complete?L(finishedZh,finishedEn):quest.next;
            beats.Add(new StoryBeat(chapter,L(nameZh,nameEn),
                L(summaryZh,summaryEn),next,questId,complete));
        }

        // Only after the original finale may the chronicle name WHICH ending
        // was chosen. This reflects the actual Epilogue, not a generic
        // "victory" that erases its distinct ecological and civic effects.
        private string EndingRecapSuffix()
        {
            if(save.ending==1)return L(
                " 你归还星核，堡垒屏障逐渐熄灭，河流与土地开始恢复；居民建立公共避难所承担新的风险。",
                " You returned the cores. The citadel shield faded as rivers recovered; residents built shared shelters to face the remaining risks.");
            if(save.ending==2)return L(
                " 你保留了屏障，也要求堡垒对城外人开放庇护；外部河流仍在变浅，莉雅公开屏障耗能的账目。",
                " You kept the shield but opened its refuge to outsiders. Rivers beyond the wall still decline, so Lyra publishes its energy cost.");
            if(save.ending==3)return L(
                " 你和同伴把星核能源逐步改为公共水泵、渡船与道路，并由居民共同维护，不再由堡垒独自决定。",
                " You and your allies turned core power toward public pumps, ferries and roads under resident stewardship, not the citadel alone.");
            return "";
        }

        // Resolve the Journey panel's real clickable continuation. A
        // completed old chapter stays historical, except for Chapter Twelve:
        // residents may still need to staff and provision the actual
        // waystations after publishing the initial road audit. Do not
        // re-track a finished audit or invent an unlocked quest.
        public string StoryRecapTrackableQuestId(StoryBeat beat)
        {
            if(beat==null)return null;
            if(!beat.complete)
            {
                QuestGuide current=GetQuest(beat.questId);
                return current.locked||current.complete?null:beat.questId;
            }
            if(beat.chapter==12&&beat.questId=="civic_aftercare")
            {
                QuestGuide next=GetQuest("civic_waystations");
                if(!next.locked&&!next.complete)
                    return next.id;
            }
            return null;
        }

        // Order is historical, visibility is NOT: the journal may only recap
        // chapters already reached or genuinely unlocked in this save slot.
        // No downstream chapter name, choice or outcome leaks on a new game.
        public List<StoryBeat> StoryRecap()
        {
            var beats=new List<StoryBeat>(12);
            AddRecapBeat(beats,1,"第一章 · 密林的星根",
                "Chapter One · The Forest Roots",
                Has("scout_saved")?
                    "侦察员已经找到被屏障抽取能量的地下星根。你要查清巨兽守护的祭坛出了什么问题，再把星核证据交给莉雅。":
                    "镇外侦察员在密林失踪。先找到他并调查异常的祭坛，才能弄清森林为什么枯萎。",
                Has("scout_saved")?
                    "The rescued scout found living roots drained to power the shield. Investigate the guarded altar and take the core evidence to Lyra.":
                    "A scout is missing in Thornwood. Find him and investigate the strange altar to learn why the forest is withering.",
                "main",save.stage>=1,
                "第一枚星核已交到公会，森林失去的能量仍需要追查。",
                "The first core reached the guild; the forest's missing energy still matters.");

            if(save.stage>=1)
                AddRecapBeat(beats,2,"第二章 · 遗迹与渡口",
                    "Chapter Two · The Ruins and the Crossing",
                    "第一枚星核带来新的疑问：旧遗迹的石碑和水路为何受到封锁？沿线索打开遗迹，寻找第二枚星核。",
                    "The first core raises new questions. Why were the old tablets and crossings sealed? Follow the records through the ruins to the second core.",
                    "main",save.stage>=2,
                    "遗迹的记录已找到，第二枚星核交回公会。",
                    "The ruins were investigated and the second core returned to the guild.");

            if(save.stage>=2)
                AddRecapBeat(beats,3,"第三章 · 矿场的代价",
                    "Chapter Three · The Cost of the Mines",
                    "矿场机器仍在运转，矿工却没有安全停机的权利。修好升降机，查明设备在为谁输送星核能量。",
                    "The mines keep running without giving workers a safe shutdown. Repair the lift and investigate who receives the starcore power.",
                    "main",save.stage>=3,
                    "第三枚星核已交回公会，三片地区的损失指向同一道屏障。",
                    "The third core reached the guild; the three regions' losses point to the same shield.");

            if(save.stage>=3)
                AddRecapBeat(beats,4,"第四章 · 三核真相",
                    "Chapter Four · The Three-Core Truth",
                    (Has("triad_complete")?
                        "莉雅已经公开森林、水路与矿工的原始记录。它们都指向同一取舍：屏障保护城内，却让城外居民付出代价。":
                        "三枚星核都被送往堡垒。前往与厄洛对峙之前，可以帮莉雅核对森林、水路和矿工的记录，让决定建立在真实证词上。") +
                        (save.english ? "" : EndingRecapSuffix()),
                    (Has("triad_complete")?
                        "Lyra published the records of forests, crossings and miners. The shield protects the citadel while people outside pay its cost.":
                        "All three cores feed the citadel. Before confronting Orrin, help Lyra verify the forests', crossings' and miners' testimony.") +
                        (save.english ? EndingRecapSuffix() : ""),
                    "main",save.ending>0,
                    "你已作出星核抉择；世界将继续回应这项决定。",
                    "You chose the cores' future; the world continues to respond.");

            if(save.ending<=0) return beats;
            AddUnlockedRecap(beats,5,"afterlight",
                "第五章 · 余光重建","Chapter Five · Afterlight Reconstruction",
                "星核事件之后，恢复土地不能只靠一次决定。重连三地公共设施，并让当地居民承担后续维护。",
                "After the core decision, recovery takes more than one promise. Reconnect three public sites with local residents as their stewards.",
                "重建公约已经公布，三地居民共同维护设施。",
                "The reconstruction charter is public and residents maintain the sites.");
            AddUnlockedRecap(beats,6,"storm_commons",
                "第六章 · 公约第一夜","Chapter Six · First Night of the Commons",
                "战后第一场星雨考验新公约：公共储备有限，避险与供能必须由各地共同协调。",
                "The first postwar starstorm tests the new charter. Limited public reserves must balance shelter and essential power.",
                "第一次公共星雨应对已经完成。",
                "The first shared starstorm response is complete.");
            AddUnlockedRecap(beats,7,"hearing",
                "第七章 · 众人的晨钟","Chapter Seven · The People's Dawn",
                "设施恢复之后，居民仍要决定自己的未来。听取密林、渡口与矿场三位证人的选择，再公布公开公约。",
                "Restoring infrastructure is not enough: residents must choose its future. Hear the forest, ferry and mining witnesses before publishing the civic verdict.",
                "居民听证与三地公共公约已经生效。",
                "The residents' hearing and three-region civic accord are complete.");
            AddUnlockedRecap(beats,8,"roadcharter",
                "第八章 · 共同行路","Chapter Eight · Roads for Everyone",
                "旧地图只记录获准通行的人。重新踏查三地支路，把居民真实走过的路写进公共地图。",
                "Old maps recorded authorized travelers, not every resident. Survey three lived-in roads and put real civilian routes on the public map.",
                "三地道路证据已汇总，公共道路公约已经公布。",
                "Three regions' road evidence has become a public charter.");
            AddUnlockedRecap(beats,9,"civic_supply",
                "第九章 · 居民补给审计","Chapter Nine · People's Supply Inquest",
                "补给不能只记在账本上：先把救援包实际送到居民手中，再让社区选择医疗点或守路岗。",
                "Relief cannot exist only on paper. Deliver real parcels, then let each community choose between clinics and road lookouts.",
                "三地补给与服务选择已公开，居民可核对账目。",
                "Three communities' supply and service choices are on public record.");
            AddUnlockedRecap(beats,10,"last_directive",
                "第十章 · 最后指令","Chapter Ten · The Last Directive",
                "公开账目之后，还有一道无人署名的旧军令。寻找原始证词，解除封锁，决定哪些权利应该归还居民。",
                "A final unsigned military order remains after the supply ledger. Recover first-hand testimony, break its locks and restore civilian rights.",
                "最后指令已经公开，居民获得持续有效的民用权限。",
                "The last directive is public, with lasting civic rights.");
            AddUnlockedRecap(beats,11,"civic_conduit",
                "第十一章 · 公共电网调谐","Chapter Eleven · The Civic Conduit",
                "新权利需要真正运行的电网。亲自校准三处阀门，避开电弧，让公共能源送达各地避险棚。",
                "New civic rights need a working grid. Tune three physical valves, avoid live arcs and restore power to public refuges.",
                "三个阀门已经校准，公共电网持续服务远岸居民。",
                "Three tuned valves keep the public grid serving the farshore.");
            // The town's chronicle ALREADY has Chapter Twelve; the tracked
            // story recap must not strand a player after the grid ending.
            // Reuse the real road-audit quest for both unlock and next foot,
            // so none of the three regional checks can be fabricated here.
            AddUnlockedRecap(beats,12,"civic_aftercare",
                "第十二章 · 电网之后，谁守着路",
                "Chapter Twelve · Who Keeps the Road After the Grid",
                Has("civic_aftercare_started")?
                    "电网重新送来了光。夜行旅人摊开巡路簿，等着引水渠、赤铁矿场和雾港的旧路标留下亲自核验的记号。已确认的路能带人回家，未到过的路仍是空白。":
                    "三道阀门都接通了，但灯亮不代表夜路安全。去炉火旅店找夜行旅人，领走巡路簿，替引水渠、赤铁矿场和雾港重新走一遍旧路标。",
                Has("civic_aftercare_started")?
                    "The grid carries light again. At Hearth Inn the night traveler has spread out the road ledger, waiting for first-hand marks from Canal, Ironreach and Mistport. A checked road brings people home; the unwalked routes remain blank.":
                    "All three valves carry current, but lamplight alone cannot make a night road safe. Find the night traveler at Hearth Inn, take the route ledger and walk the old markers in Canal, Ironreach and Mistport.",
                "三地巡路实录已经公开，守路人会在核验路标旁留下药箱，照顾后来的人。",
                "The three-region road ledger is published. Its surveyed markers now keep herbal field kits for those who come after.");
            // Chapter Twelve can be historically complete while residents
            // still lack actual lumber and medicine. Make its NEXT-step
            // text agree with the Journey panel's actionable follow-up
            // instead of leaving the hero stranded on a disabled button.
            if(beats.Count>0)
            {
                int last=beats.Count-1;
                StoryBeat page=beats[last];
                string nextId=StoryRecapTrackableQuestId(page);
                if(page.chapter==12&&page.complete&&
                   nextId=="civic_waystations")
                {
                    QuestGuide nextQuest=GetQuest(nextId);
                    beats[last]=new StoryBeat(page.chapter,page.title,
                        page.summary,L("巡路簿已经公开。下一步补给路站：",
                            "Road ledger published. Next, stock the waystations: ")+
                        nextQuest.next,page.questId,page.complete);
                }
            }
            return beats;
        }

        private void AddUnlockedRecap(List<StoryBeat> beats,int chapter,string questId,
            string nameZh,string nameEn,string summaryZh,string summaryEn,
            string finishedZh,string finishedEn)
        {
            QuestGuide quest=GetQuest(questId);
            if(quest.locked)return;
            beats.Add(new StoryBeat(chapter,L(nameZh,nameEn),L(summaryZh,summaryEn),
                quest.complete?L(finishedZh,finishedEn):quest.next,
                questId,quest.complete));
        }
    }
}
