using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // The original letters remain readable and keep their original rewards.
        private bool TalkRoadwardenCipher(WorldObject o)
        {
            if (!o.kind.StartsWith("history")) return false;
            int id;
            if (!int.TryParse(o.kind.Substring(7), out id) || id < 6 || id > 8 ||
                !Has("survey_complete")) return false;
            ReadHistory(id);
            if (!Has("dispatch_" + id))
            {
                var options = new List<Choice>(conversation.choices);
                options.Add(C("解读巡路暗号", "Decode roadwarden cipher", "dispatch_open_" + id));
                conversation.choices = options.ToArray();
            }
            return true;
        }

        private bool ChooseRoadwardenCipher(string action)
        {
            if (action.StartsWith("dispatch_open_"))
            {
                int id;
                if (!int.TryParse(action.Substring(14), out id) || !CanDecodeLetter(id)) return true;
                int n = id - 6;
                string[] zh = {
                    "密林旧信说药物不应只沿堡垒的路送。巡路队应该守护谁？",
                    "遗迹船票说没钱的人也能渡河。老摆渡人收什么作为船费？",
                    "矿场图纸上有两句互相矛盾的话。最终谁决定机器的用途？"
                };
                string[] en = {
                    "Medicine should travel beyond the citadel road. Whom should the wardens protect?",
                    "Those without money may still cross the river. What did the old ferryman accept as fare?",
                    "The mine blueprint contradicts itself. Who ultimately decides what a machine is used for?"
                };
                string[,] optionsZh = {
                    { "堡垒商队", "需要帮助的人", "上锁的仓库" },
                    { "硬币", "交出船票", "一个故事" },
                    { "机器自己", "握着图纸的人", "星核" }
                };
                string[,] optionsEn = {
                    { "Citadel caravans", "People in need", "A locked storehouse" },
                    { "Coins", "The ticket itself", "A story" },
                    { "The machine", "The person holding the plan", "The starcore" }
                };
                Dialog("巡路队的暗号", "Roadwarden Cipher", zh[n], en[n],
                    C(optionsZh[n, 0], optionsEn[n, 0], "dispatch_answer_" + id + "_0"),
                    C(optionsZh[n, 1], optionsEn[n, 1], "dispatch_answer_" + id + "_1"),
                    C(optionsZh[n, 2], optionsEn[n, 2], "dispatch_answer_" + id + "_2"));
                return true;
            }
            if (!action.StartsWith("dispatch_answer_")) return false;
            string[] parts = action.Substring(16).Split('_');
            int letter, answer;
            if (parts.Length != 2 || !int.TryParse(parts[0], out letter) ||
                !int.TryParse(parts[1], out answer) || answer < 0 || answer > 2 ||
                !CanDecodeLetter(letter)) return true;
            if (answer != (letter == 7 ? 2 : 1))
            {
                Dialog("暗号尚未解开", "Cipher Unsolved",
                    "再读一遍旧信，想想它真正关心的是谁。答错不会损失物资。",
                    "Reread the letter and think about whom it was written for. Wrong answers cost nothing.",
                    C("再试一次", "Try again", "dispatch_open_" + letter),
                    C("稍后再来", "Return later", "close"));
                return true;
            }
            Flag("dispatch_" + letter);
            save.coins += 20; save.shards++; AddXp(25); revision++; sound = "reward";
            RecordMoment("解读巡路暗号 " + (letter - 5) + "/3。", "Decoded roadwarden cipher " + (letter - 5) + "/3.");
            if (Has("dispatch_6") && Has("dispatch_7") && Has("dispatch_8") && !Has("dispatch_complete"))
            {
                Flag("dispatch_complete"); save.coins += 40; save.skillPoints++; revision++;
                RecordMoment("三封旧信拼成巡路队的誓言：让每个人都能找到归途。",
                    "Three letters reveal the wardens' promise: a way home for everyone.");
                Notify("巡路誓言完成！额外获得40硬币、技能点1。",
                    "Roadwarden promise complete! Bonus: 40 coins and 1 talent point.");
            }
            else Notify("暗号解开！获得20硬币、1碎片、25经验。",
                "Cipher solved! +20 coins, 1 shard, 25 XP.");
            return true;
        }

        private bool CanDecodeLetter(int id)
        {
            return id >= 6 && id <= 8 && save.room == id - 5 &&
                Has("survey_complete") && Has("history_" + id) && !Has("dispatch_" + id);
        }
    }
}
