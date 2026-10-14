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
            if(step==0||save.room!=1||(Position-new Point(o.x,o.y)).Length>1.8f)return true;
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
                RootsongDialog(o,clue+L("\n\n这一声已归档，不再奖励。","\n\nThis tone is archived, with no duplicate rewards."),
                    C("重听记录","Review","close"));
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
            if(!action.StartsWith("rootsong_",StringComparison.Ordinal))return false;
            string[] parts=action.Split('_');
            int step,tone;
            if(parts.Length!=3||!int.TryParse(parts[1],out step)||!int.TryParse(parts[2],out tone)||
                step<1||step>3||tone<0||tone>2)return true;
            WorldObject stone=RootsongStone(step);
            if(stone==null||save.room!=1||!Has("scout_saved")||Dying||practicing||
                RootsongProgress+1!=step||
                (Position-new Point(stone.x,stone.y)).Length>1.8f)return true;
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
                save.coins+=45;save.shards+=2;AddXp(75);
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
                    if(o.kind=="rootsonestone"&&(Position-new Point(o.x,o.y)).Length<3.1f)return true;
                return false;
            }
        }
    }
}
