using System;
using System.Collections.Generic;
namespace Starfall
{
    // Original Q skill action and SaveData v2 flags; no replacement attack loop.
    public sealed partial class GameCore
    {
        public bool ForgeMasterworkUnlocked {get{return Has("ally_3")&&save.stage>=2;}}
        public int ForgeMasterwork {get{return Has("forge_skill_quick")?1:Has("forge_skill_weight")?2:0;}}
        public float ForgeSkillCooldown {get{return ForgeMasterwork==1?2.65f:ForgeMasterwork==2?4.2f:3.5f;}}
        public float ForgeSkillPower {get{return ForgeMasterwork==2?1.3f:1f;}}
        private string ForgeMasterworkDescription(bool english)
        {
            if(!ForgeMasterworkUnlocked)return english?
                "\n\nRepair the town pump and deliver two starcores to unlock advanced Q tuning.":
                "\n\n修复镇口水泵并交回两枚星核后才能校准进阶战技。";
            if(ForgeMasterwork==1)return english?
                "\n\nQuickwork installed: Q skill cooldown reduced to 2.65 seconds, permanently.":
                "\n\n迅捷调谐已装配：Q 战技冷却永久缩短到2.65秒。";
            if(ForgeMasterwork==2)return english?
                "\n\nHeavy impact installed: Q skill damage +30%, cooldown 4.2 seconds, permanently.":
                "\n\n重击校准已装配：Q 战技伤害永久提升30%，冷却延长到4.2秒。";
            return english?
                "\n\nChoose one: Quickwork Q every 2.65s, or Heavy impact +30% damage at 4.2s cooldown. Both require materials.":
                "\n\n两种永久方案只能选一：迅捷调谐使Q冷却变为2.65秒；重击校准令战技伤害+30%但冷却4.2秒。";
        }
        private void AppendForgeMasterworkChoices(List<Choice> options)
        {
            if(!ForgeMasterworkUnlocked||ForgeMasterwork!=0)return;
            options.Add(C("装配迅捷调谐 · 木材2、星晶2、零件2",
                "Quickwork · 2 timber, 2 crystals, 2 parts","forge_skill_quick"));
            options.Add(C("装配重击校准 · 木材1、星晶4、零件3",
                "Heavy impact · 1 timber, 4 crystals, 3 parts","forge_skill_weight"));
        }
        private bool ChooseForgeMasterwork(string action)
        {
            bool quick=action=="forge_skill_quick",heavy=action=="forge_skill_weight";
            if(!quick&&!heavy)return false;
            WorldObject source=pendingDialogueSource;
            if(!ForgeMasterworkUnlocked||ForgeMasterwork!=0||save.room!=0||
                mode!="world"||Dying||practicing||source==null||source.kind!="forge"||
                pendingDialogueSourceRoom!=0||!SourceBelongsToActiveMap(source)||
                !HasPhysicalInteractionReach(source))return true;
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("现场任务或附近敌人未解除，不能安全校准。",
                    "Clear nearby hostiles and active fieldwork before calibration.");
                return true;
            }
            if(!(quick?Spend(2,2,0,0,2):Spend(1,4,0,0,3)))return true;
            Flag(quick?"forge_skill_quick":"forge_skill_weight");
            RecordMoment(quick?
                "诺尔把老水泵轴承改成轻巧调速环。出招更频繁，但仍需要认清敌人的起手。":
                "诺尔把旧矿车重轮改成战技校准器。更重的每一次出手，都留给敌人更长的反击时间。",
                quick?
                "Noll converts a rescued pump bearing into a quick regulator: attacks flow faster, though enemy warnings remain.":
                "Noll refits a heavy mine wheel: each class technique hits harder, but leaves a longer opening.");
            revision++;sound="reward";
            Notify(quick?"迅捷调谐完成：Q 战技冷却2.65秒。":
                "重击校准完成：Q 伤害+30%，冷却4.2秒。",
                quick?"Quickwork forged: Q cooldown 2.65 seconds.":
                "Heavy impact forged: Q damage +30%, cooldown 4.2 seconds.");
            return true;
        }
    }
}
