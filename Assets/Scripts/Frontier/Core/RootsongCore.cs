using System;
namespace Starfall
{
    // Three physical Thornwood root instruments. Persistent progress consists
    // exclusively of preexisting SaveData v2 string flags.
    public sealed partial class GameCore
    {
        private static readonly string[] RootsongCluesZh={
            "古根曾为受伤者遮雨。第一声是根底的低音；并非向城墙发出的军号。",
            "无名摆渡人跨过沉桥时以中音回应。第二声来自河水而非警钟。",
            "林中的孩子合唱归乡的高音。这是第三声：回家的召唤，不是开战的命令。"
        };
        private static readonly string[] RootsongCluesEn={
            "An old root sheltered the wounded. The first voice is LOW, not a military horn.",
            "Unnamed ferrymen answered across the river with a MIDDLE tone, not an alarm.",
            "Children sang the final HIGH homeward call, guiding the lost instead of marching to war."
        };
        public int RootsongProgress
        {
            get
            {
                if(Has("rootsong_complete"))return 3;
                for(int i=3;i>=1;i--)if(Has("rootsong_"+i))return i;
                return 0;
            }
        }
        public bool RootsongComplete { get { return Has("rootsong_complete"); } }
        // One non-farmable, transient field-control decision. An old save can
        // still use its energy and the original root flags without migration.
        public float RootsongPulseCooldown { get; private set; }
        private bool RootsongThreat(WorldObject stone)
        {
            if(stone==null||save.room!=1)return false;
            Point position=new Point(stone.x,stone.y);
            foreach(Enemy enemy in enemies)
                if(enemy.hp>0&&(enemy.Position-position).Length<4.1f&&
                    ClearAttackPath(position,enemy.Position))return true;
            return false;
        }
        // A root's puzzle and field skill require real near-side access.
        // Revalidate at button commit; the world may change after Talk.
        private bool CanReachRootsongStone(WorldObject stone)
        { return stone!=null&&PhysicalRefugeReach(
            new Point(stone.x,stone.y),1.8001f); }
        private bool RootsongPulse(string action)
        {
            if(!action.StartsWith("rootsongpulse_",StringComparison.Ordinal))return false;
            int stage;
            if(!int.TryParse(action.Substring("rootsongpulse_".Length),out stage))return true;
            WorldObject stone=RootsongStone(stage);
            if(!RootsongComplete||stone==null||Dying||practicing||save.room!=1||
                !CanReachRootsongStone(stone))return true;
            if(RootsongPulseCooldown>0)
            {
                Notify("古根正在回声，稍后才能再次回应。","This root is still recovering its resonance.");
                return true;
            }
            if(save.energy<30)
            {
                Notify("释放保护根鸣需要30耐力。","A protective root pulse needs 30 energy.");
                return true;
            }
            if(!RootsongThreat(stone))
            {
                Notify("没有可以被根鸣触及的威胁。","There is no reachable threat within the root's song.");
                return true;
            }
            Point origin=new Point(stone.x,stone.y);
            int interrupted=0;
            foreach(Enemy enemy in enemies)
            {
                if(enemy.hp<=0||(enemy.Position-origin).Length>=4.1f||
                    !ClearAttackPath(origin,enemy.Position))continue;
                // Share the exact authoritative interruption transaction
                // used by frost/Break. Otherwise a raider's committed civilian
                // target and its still-red warning survive a Rootguard pulse.
                ApplyEnemyControl(enemy,enemy.boss?.3f:1.2f);
                enemy.timer=Math.Max(enemy.timer,enemy.boss?.4f:.75f);
                interrupted++;
            }
            if(interrupted==0)return true;
            save.energy-=30;RootsongPulseCooldown=8.5f;
            effects.Add(new Effect("burst",origin,.5f){radius=4.1f,variant=1});
            effects.Add(new Effect("number",origin+new Point(0,-.7f),.75f,
                L("古根护歌","ROOTGUARD")));
            revision++;sound="spell";
            Notify("古根震鸣打断了 "+interrupted+" 个敌人。耐力 -30；无法连续释放。",
                "Rootsong interrupted "+interrupted+" foes. -30 energy; the pulse must recharge.");
            return true;
        }
        private WorldObject RootsongStone(int step)
        {
            if(step<1||step>3)return null;
            foreach(WorldObject o in data.maps[1].objects)
                if(o.id=="rootsong_"+step&&o.kind=="rootsonestone")return o;
            return null;
        }
        public float RootsongPhase(int step)
        {
            if(step<1||step>3)return 0;
            float p=(save.totalTime+step*1.25f)%6f;
            return p<0?p+6f:p;
        }
        public bool RootsongOpen(int step)
        { return step>=1&&step<=3&&RootsongPhase(step)>=1.5f&&RootsongPhase(step)<3f; }
        public float RootsongWait(int step)
        {
            if(step<1||step>3)return 0;
            float p=RootsongPhase(step);
            return p<1.5f?1.5f-p:p<3f?0:7.5f-p;
        }
        // Use the existing localized conversation display contract.
        private void RootsongDialog(WorldObject o,string text,params Choice[] choices)
        { Dialog(o.name,o.en,text,text,choices); }
        private bool TalkRootsong(WorldObject o)
        {
            if(o.kind!="rootsonestone")return false;
            int step=0;
            for(int i=1;i<=3;i++)if(o.id=="rootsong_"+i)step=i;
            if(step==0||save.room!=1||!CanReachRootsongStone(o))return true;
            if(!Has("scout_saved"))
            {
                RootsongDialog(o,L("先救回密林侦察员，才能听懂树根里藏着的平民归途。",
                    "Rescue the forest scout before decoding these civilian road songs."),
                    C("以后再来","Return later","close"));
                return true;
            }
            string clue=L(RootsongCluesZh[step-1],RootsongCluesEn[step-1]);
            if(step<=RootsongProgress)
            {
                string history=clue+L("\n\n这一声已归档，不再奖励。","\n\nThis tone is archived, with no duplicate rewards.");
                if(RootsongComplete&&RootsongThreat(o))
                {
                    bool ready=RootsongPulseCooldown<=0&&save.energy>=30;
                    string description=history+(RootsongPulseCooldown>0?
                        L("\n\n护歌仍需冷却 "+RootsongPulseCooldown.ToString("0.0")+" 秒。",
                          "\n\nRootguard recharges for "+RootsongPulseCooldown.ToString("0.0")+"s.") :
                        save.energy<30?
                        L("\n\n护歌需要30耐力，当前不足。","\n\nRootguard needs 30 energy, which is currently unavailable.") :
                        L("\n\n附近有敌人。消耗30耐力释放四格范围的护歌，短暂打断进攻。",
                          "\n\nEnemies are nearby. Spend 30 energy on a four-tile Rootguard to interrupt attacks."));
                    if(ready)
                        RootsongDialog(o,description,
                            C("聆听历史","Review history","close"),
                            C("根鸣护歌 · 30耐力","Rootguard · 30 energy","rootsongpulse_"+step));
                    else RootsongDialog(o,description,C("聆听历史","Review history","close"));
                    return true;
                }
                RootsongDialog(o,history,C("重听记录","Review","close"));
                return true;
            }
            if(step!=RootsongProgress+1)
            {
                RootsongDialog(o,L("先找到上一根古树，根鸣须依次唤醒。","Find the preceding trunk: the three voices must be restored in order."),
                    C("继续探索","Explore","close"));
                return true;
            }
            float wait=RootsongWait(step);
            if(wait>0)
                RootsongDialog(o,clue+L("\n\n石环还未泛绿，约 "+wait.ToString("0.0")+" 秒后再来。交谈期间时间暂停。",
                    "\n\nThe ring is dark. Return in about "+wait.ToString("0.0")+" seconds when it turns green; conversation pauses time."),
                    C("等待石环亮起","Wait for green","close"));
            else
                RootsongDialog(o,clue+L("\n\n现在石环泛绿：选择音调。错误的声音会消耗10耐力并唤起伤人的根刺。",
                    "\n\nThe ring is green: choose a tone. Discord costs 10 energy and summons a damaging root surge."),
                    C("低音","Low","rootsong_"+step+"_0"),
                    C("中音","Middle","rootsong_"+step+"_1"),
                    C("高音","High","rootsong_"+step+"_2"));
            return true;
        }
        private bool ChooseRootsong(string action)
        {
            if(RootsongPulse(action))return true;
            if(!action.StartsWith("rootsong_",StringComparison.Ordinal))return false;
            string[] parts=action.Split('_');
            int step,tone;
            if(parts.Length!=3||!int.TryParse(parts[1],out step)||!int.TryParse(parts[2],out tone)||
                step<1||step>3||tone<0||tone>2)return true;
            WorldObject stone=RootsongStone(step);
            if(stone==null||save.room!=1||!Has("scout_saved")||Dying||practicing||
                RootsongProgress+1!=step||
                !CanReachRootsongStone(stone))return true;
            if(!RootsongOpen(step))
            {
                Notify("绿光窗口已经结束。等待下一次根鸣。","The green window closed. Wait for the next pulse.");
                return true;
            }
            if(tone!=step-1)
            {
                save.energy=Math.Max(0,save.energy-10);
                hazards.Add(new Hazard {position=new Point(stone.x,stone.y),
                    kind="root",radius=1.35f,timer=1.05f,damage=12});
                revision++;sound="deny";
                Notify("音调不合！耐力 -10。离开根刺红圈或及时闪避。",
                    "Discord! -10 energy. Leave the warned root circle or dodge.");
                return true;
            }
            Flag("rootsong_"+step);sound="reward";
            if(step==3)
            {
                Flag("rootsong_complete");
                CreditCoins(45);CreditShards(2);AddXp(75);
                RecordMoment("密林的庇护、摆渡与归乡三声重新合唱，古根旁终于有了安全的休憩地。",
                    "The three forest voices of shelter, ferry and homecoming now make safe resting roots.");
                Notify("三声归一！硬币 +45、碎片 +2、经验 +75；古树附近安全时额外恢复耐力。",
                    "Three voices restored! +45 coins, +2 shards, +75 XP; safe nearby roots restore stamina.");
            }
            else
            {
                AddXp(15);
                Notify("根鸣 "+step+"/3 已记录，经验 +15。前往下一个实地音源。",
                    "Root voice "+step+"/3 recorded. +15 XP. Seek the next physical source.");
            }
            revision++;
            return true;
        }
        public bool RootsongRefuge
        {
            get
            {
                if(!RootsongComplete||save.room!=1)return false;
                foreach(WorldObject o in Map.objects)
                    if(o.kind=="rootsonestone"&&
                        PhysicalRefugeReach(new Point(o.x,o.y),3.1f))return true;
                return false;
            }
        }
    }
}
