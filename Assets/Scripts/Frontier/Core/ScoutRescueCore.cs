using System;
using System.Collections.Generic;

namespace Starfall
{
    // Opening-chapter rescue: the first meaningful choice connects an
    // existing Thornwood scout, an actual road beacon and real live combat.
    // Transient attackers are not saved; only ordinary v2 quest flags persist.
    public sealed partial class GameCore
    {
        private MapData scoutDefenseMap;
        private SaveData scoutDefenseSave;
        private WorldObject scoutDefenseSource;
        private float scoutDefensePressure;
        private bool scoutDefenseWarning,scoutDefenseHeroCover,scoutDefenseThreatening;
        private readonly List<Enemy> scoutDefenseFoes=new List<Enemy>();
        private readonly HashSet<Enemy> scoutDefenseDefeated=new HashSet<Enemy>();
        public bool ScoutDefenseActive
        {
            get { return scoutDefenseSource!=null&&save.room==1&&
                Object.ReferenceEquals(scoutDefenseMap,Map)&&
                Object.ReferenceEquals(scoutDefenseSave,save)&&
                SourceBelongsToActiveMap(scoutDefenseSource)&&
                !Has("scout_saved"); }
        }
        public int ScoutDefenseRemaining
        {
            get {
                int n=0;
                if(ScoutDefenseActive)foreach(Enemy e in scoutDefenseFoes)
                    if(e!=null&&e.hp>0&&enemies.Contains(e))n++;
                return n;
            }
        }
        public int ScoutDefenseSafety
        { get { return ScoutDefenseActive?
            Math.Max(0,100-(int)Math.Ceiling(scoutDefensePressure)):100; } }
        public bool ScoutDefenseUnderAttack
        { get { return ScoutDefenseActive&&scoutDefenseThreatening; } }
        public bool ScoutDefenseHeroCover
        { get { return ScoutDefenseActive&&scoutDefenseHeroCover; } }
        public Point ScoutDefenseGuideTarget
        {
            get {
                if(ScoutDefenseActive)foreach(Enemy e in scoutDefenseFoes)
                    if(e!=null&&e.hp>0&&enemies.Contains(e))return e.Position;
                return Position;
            }
        }
        private void ResetScoutDefense()
        {
            foreach(Enemy e in scoutDefenseFoes)
            {
                RemoveEnemyPendingWarning(e);
                enemies.Remove(e);regionalAmbientEnemies.Remove(e);
                projectiles.RemoveAll(p=>Object.ReferenceEquals(p.hostileShooter,e));
            }
            scoutDefenseFoes.Clear();scoutDefenseDefeated.Clear();
            scoutDefenseSource=null;scoutDefenseMap=null;scoutDefenseSave=null;
            scoutDefensePressure=0f;scoutDefenseWarning=false;scoutDefenseHeroCover=false;
            scoutDefenseThreatening=false;
        }
        private bool ScoutAttackFootSafe(Point foot,List<Point> placed)
        {
            if(!CanStand(foot)||(foot-Position).Length<3.5f||
                NavigationPathFrom(Position,foot,true).Count==0)return false;
            foreach(WorldObject o in Map.objects)
                if(o!=null&&(foot-new Point(o.x,o.y)).Length<2f)return false;
            foreach(Enemy e in enemies)
                if(e!=null&&e.hp>0&&(e.Position-foot).Length<2.6f)return false;
            foreach(Point p in placed)
                if((p-foot).Length<3.5f)return false;
            return true;
        }
        private bool BeginScoutDefense(WorldObject scout)
        {
            if(ScoutDefenseActive||scout==null||save.room!=1)return false;
            var feet=new List<Point>();
            int cx=(int)Math.Floor(scout.x),cy=(int)Math.Floor(scout.y);
            // No remote kill counter: the two attackers must be physically
            // reachable from the real scout in the shipped Thornwood map.
            for(int r=4;r<=16&&feet.Count<2;r++)
            for(int dy=-r;dy<=r&&feet.Count<2;dy++)
            for(int dx=-r;dx<=r&&feet.Count<2;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r)continue;
                Point foot=new Point(cx+dx+.5f,cy+dy+.5f);
                if(ScoutAttackFootSafe(foot,feet))feet.Add(foot);
            }
            if(feet.Count!=2)return false;
            scoutDefenseSource=scout;scoutDefenseMap=Map;scoutDefenseSave=save;
            scoutDefensePressure=0f;scoutDefenseWarning=false;scoutDefenseHeroCover=false;
            scoutDefenseThreatening=false;
            for(int i=0;i<feet.Count;i++)
            {
                Enemy foe=CreatePlacedEnemy(++enemyId,i==0?0:1,
                    feet[i],i==0?36f:32f);
                foe.stun=Math.Max(.6f,foe.stun);
                scoutDefenseFoes.Add(foe);enemies.Add(foe);
                effects.Add(new Effect("enemyRelease",foe.Position,.6f)
                    {variant=foe.kind});
            }
            revision++;sound="alert";
            return true;
        }
        private void OnScoutDefenseEnemyDefeated(Enemy foe)
        {
            if(ScoutDefenseActive&&scoutDefenseFoes.Contains(foe))
                scoutDefenseDefeated.Add(foe);
        }
        private void TickScoutDefense(float dt)
        {
            if(scoutDefenseSource==null)return;
            if(!ScoutDefenseActive||Dying||practicing)
            { ResetScoutDefense();return; }
            // Only the actual Hit() death hook counts. Teleport, a missing
            // enemy or an artificially reduced HP can never forge rescue.
            foreach(Enemy e in scoutDefenseFoes)
                if(e==null||(!scoutDefenseDefeated.Contains(e)&&
                    (!enemies.Contains(e)||e.hp<=0)))
                {
                    ResetScoutDefense();revision++;
                    Notify("威胁失去踪迹，护送尚未完成。回侦察员处重新组织，不会重复收费。",
                        "An attacker vanished without a verified defeat. Regroup at the scout for free.");
                    return;
                }
            bool cleared=scoutDefenseFoes.Count==2;
            foreach(Enemy e in scoutDefenseFoes)
                if(!scoutDefenseDefeated.Contains(e)||e.hp>0||
                    enemies.Contains(e)){cleared=false;break;}
            if(cleared)
            {
                bool safe=ScoutDefenseSafety>=70;
                ResetScoutDefense();Flag("scout_defense_cleared");
                if(!safe)Flag("scout_defense_scarred");
                effects.Add(new Effect("discover",Position,.6f));
                RecordMoment("密林 · 你亲自击退了威胁侦察员的两名追兵。道路安全，但他仍需要你回去确认。",
                    "Thornwood: you defeated the two raiders threatening the scout. Return to check on him.");
                Notify("两名追兵已被击退。回到侦察员身边，确认他可以安全返镇。",
                    "Both threats defeated. Return to the scout and confirm his safe route.");
                return;
            }
            if(dt<=0||mode!="world")return;
            if((Position-new Point(scoutDefenseSource.x,scoutDefenseSource.y)).Length>26f)
            {
                ResetScoutDefense();revision++;
                Notify("离开侦察员防线太远。本次交战撤销，可免费再试。",
                    "You left the scout perimeter. Return to retry without a penalty.");
                return;
            }
            Point witness=new Point(scoutDefenseSource.x,scoutDefenseSource.y);
            bool covered=false;
            int threatening=0;
            foreach(Enemy e in scoutDefenseFoes)
            {
                if(e==null||e.hp<=0||!enemies.Contains(e)||
                    e.stun>0f||e.stagger>0f||e.hitTime>0f||
                    !CanStand(e.Position)||
                    (e.Position-witness).Length>5.5f||
                    !ClearAttackPath(e.Position,witness))continue;
                // Body-blocking is physical: the hero must stand in a clear
                // lane between this actual enemy and the actual scout.
                if(SagaGuardHeroInterposes(e,witness))covered=true;
                else threatening++;
            }
            if(covered&&!scoutDefenseHeroCover)
            {
                effects.Add(new Effect("number",Position,.7f,
                    L("舍身拦截！","BODYGUARD!")));
                revision++;sound="guard";
            }
            scoutDefenseHeroCover=covered;
            scoutDefenseThreatening=threatening>0;
            // Only genuine world time degrades the route. Bound frame spikes
            // so a dropped frame cannot erase an entire rescue instantly.
            float slice=Math.Min(.1f,dt);
            scoutDefensePressure=Math.Max(0f,Math.Min(100f,
                scoutDefensePressure+slice*(threatening>0?threatening*10f:-4f)));
            if(scoutDefensePressure>=100f)
            {
                ResetScoutDefense();revision++;sound="alert";
                Notify("追兵冲到侦察员身边，他退回隐蔽处。防线需重新组织，已完成的调查不会丢失。",
                    "Raiders reached the scout. He fell back to cover; regroup and retry without losing investigation progress.");
                return;
            }
            if(scoutDefensePressure>=55f&&!scoutDefenseWarning)
            {
                scoutDefenseWarning=true;revision++;sound="alert";
                effects.Add(new Effect("number",witness+new Point(0,-.8f),
                    1f,L("侦察员暴露！","SCOUT EXPOSED!")));
                Notify("侦察员受到威胁！击退追兵，或站在敌人和侦察员之间。",
                    "Scout exposed! Stagger the raiders or physically stand between them and the scout.");
            }
        }
        private bool TalkScoutRescue(WorldObject o)
        {
            if(o.kind=="returnscout")
            {
                if(!Has("scout_saved")||save.room!=0)return true;
                // Keep the original read-only town greeting available
                // from a remote NPC reference. Claiming the stew is a
                // separate source-validated action and still requires reach.
                bool aid=Has("scout_rescue_aid");
                Dialog(o.name,o.en,
                    aid?
                        "侦察员重新绑紧那块木制路标。“你没有拿命去赌。那晚的灯亮起来后，我能沿旧巡路标自己走回来。莉雅已经把那条撤离路线加进了镇上的地图。”":
                        Has("scout_rescue_defense")?
                        "侦察员将断裂的箭杆放在桌上。“两名追兵倒下后，我才敢把地图拿出来。你回来确认我还活着——这比那些赏金有分量。”":
                        "侦察员将折皱的测绘纸压平。“你回来找我的那天，我就知道还有人记得这条路。”",
                    aid?
                        "The scout ties a wooden waymark back in place. “You didn't gamble my life on another fight. Once the beacon was lit, I found my way home. Lyra has added that route to the town chart.”":
                        Has("scout_rescue_defense")?
                        "The scout lays a broken arrow on the table. “Only after the two raiders fell could I unfold the chart. You came back to check I was alive. That mattered more than the bounty.”":
                        "The scout flattens his torn survey. “When you came for me, I knew the road hadn't been forgotten.”",
                    C(Has("scout_gift")?"一路平安":"收下谢礼：炖汤1",
                      Has("scout_gift")?"Stay safe":"Accept thanks: 1 stew",
                      "scoutgift"),
                    C("下次路上见","See you on the road","close"));
                return true;
            }
            if(o.kind!="scout")return false;
            if(Has("scout_saved"))return true;
            if(!HasPhysicalInteractionReach(o))
                {Notify("走近侦察员才能组织救援。","Approach the scout to organize his rescue.");return true;}
            if(ScoutDefenseActive)
            {
                Dialog(o.name,o.en,
                    "“追兵就在外头。”他把测绘纸压在胸前。“还有"+ScoutDefenseRemaining+"名，撤离安全度"+ScoutDefenseSafety+"%。你可以继续战斗，挡在我身前，或者撤回重新部署。”",
                    "“They're still out there.” He holds the map tight. "+
                    ScoutDefenseRemaining+" raiders remain. Safety "+ScoutDefenseSafety+"%. Fight on, screen me in the lane, or withdraw and regroup.",
                    C("继续保护侦察员","Keep defending","close"),
                    C("撤回本次防御（无惩罚）","Withdraw without penalty","scout_abort"));
                return true;
            }
            bool ready=Has("scout_defense_cleared")||
                Has("scout_beacon_lit")||save.kills[1]>=4;
            if(ready)
            {
                string method=Has("scout_beacon_lit")?
                    "“那座旧灯亮了！你沿着我画的路走了一遍，这回我敢跟着它回去。”":
                    Has("scout_defense_cleared")?
                    (Has("scout_defense_scarred")?
                        "“他们几乎冲到了我身边。谢谢你在最后一刻守住了测绘图。”":
                        "“两名追兵都不在了。谢谢你回来确认。现在可以把这份原始记录带回去。”"):
                    "“你已经替旧巡路清出一条路。地图还是我亲手交给你。”";
                string methodEn=Has("scout_beacon_lit")?
                    "“The old beacon is lit. You walked the trail I marked. Now I can follow it home.”":
                    Has("scout_defense_cleared")?
                    (Has("scout_defense_scarred")?
                        "“They almost reached me. You held the line before they took the survey.”":
                        "“Both raiders are gone. Thanks for returning to check. Take the original record home.”"):
                    "“You cleared a way through the old patrol. Here is the map, from my own hands.”";
                Dialog(o.name,o.en,
                    method+"\n\n“根脉下的切口不是兽爪，是矿镐。有人把原本属于森林的星核能量接去了堡垒。我回镇告诉莉雅，你去祭坛查清真相。”",
                    methodEn+"\n\n“Those cuts under the roots aren't claws. They're pickaxe marks. Someone diverted the forest's starcore power to the citadel. I'll tell Lyra; find the truth at the altar.”",
                    C("确认返镇并接过地图","Send scout home · take map","scout"));
                return true;
            }
            if(Has("scout_aid_prepared"))
            {
                Dialog(o.name,o.en,
                    "“药和木板都准备好了。去西侧苔根巡路信标，亲手点亮回镇标记；不要只告诉我灯已经亮了。”",
                    "“We've got the bandage and timber. Reach the Mossroot road beacon west of here and mark the safe way home. Don't just tell me it was lit.”",
                    C("沿旧路去信标","Follow the trail to the beacon","close"));
                return true;
            }
            Dialog(o.name,o.en,
                "侦察员握着断裂的木棍，手背都是血。“他们把老路堵住了。我带着一张证明森林被人改过能源线路的图，却没法安全返镇。”\n\n"+
                "“你可以挡住那两名追兵，或者留下一份木材和药草，修好路标，再亲手到苔根信标点亮撤离线。我不想让你无缘无故去猎杀四只野兽。”",
                "The scout grips a broken branch. “The old road is blocked. I found evidence someone rerouted the forest's power, but I can't get it back to town.”\n\n"+
                "“You can fight the two raiders here, or spare one timber and one herb, repair the waymark, then actually light the Mossroot beacon west of here. Don't kill four unrelated beasts for me.”",
                C("击退现场的两名追兵","Defend against two real raiders","scout_defend"),
                C("留下木材×1、药草×1，修复撤离标记",
                  "Use 1 timber and 1 herb to mark a safe exit","scout_aid"),
                C("先准备一下","Prepare first","close"));
            return true;
        }
        private void AppendScoutBeaconChoice(WorldObject o)
        {
            if(o.kind!="survey1"||save.room!=1||
                !Has("scout_aid_prepared")||Has("scout_beacon_lit")||
                Has("scout_saved")||!HasPhysicalInteractionReach(o)||
                mode!="dialog"||conversation==null)return;
            var options=new List<Choice>(conversation.choices);
            options.Insert(0,C("点亮侦察员的安全返镇标记",
                "Light the scout's safe return signal","scout_beacon"));
            conversation.choices=options.ToArray();
        }
        private bool ChooseScoutRescue(string action)
        {
            if(action!="scout_defend"&&action!="scout_aid"&&
                action!="scout_beacon"&&action!="scout_abort"&&action!="scout")
                return false;
            WorldObject source=pendingDialogueSource;
            bool beacon=action=="scout_beacon";
            if(save.room!=1||save.stage!=0||Has("scout_saved")||
                Dying||practicing||source==null||
                pendingDialogueSourceRoom!=1||
                !SourceBelongsToActiveMap(source)||
                source.kind!=(beacon?"survey1":"scout")||
                !HasPhysicalInteractionReach(source)||!CanStand(Position))
                return true;
            if(beacon)
            {
                if(!Has("scout_aid_prepared")||Has("scout_beacon_lit"))return true;
                Flag("scout_beacon_lit");revision++;sound="quest";
                effects.Add(new Effect("discover",new Point(source.x,source.y),.6f));
                RecordMoment("密林 · 亲自沿旧巡路抵达苔根信标，重新点亮受伤侦察员的回家路。",
                    "Thornwood: walked the old trail and relit the Mossroot beacon for the stranded scout.");
                Dialog(source.name,source.en,
                    "苔根灯重新亮起，木板箭头指向镇子。远处的侦察员能看见这道光。现在回去接他。",
                    "The Mossroot light catches. Its wooden arrow points to town. The scout can see the signal from here. Return to him.",
                    C("回去接人","Return for the scout","close"));
                return true;
            }
            if(action=="scout_abort")
            {
                if(ScoutDefenseActive)ResetScoutDefense();
                revision++;sound="ui";return true;
            }
            if(action=="scout_defend")
            {
                if(Has("scout_defense_cleared")||Has("scout_beacon_lit")||
                    ScoutDefenseActive)return true;
                if(!BeginScoutDefense(source))
                {
                    Notify("这里暂时没有能安全生成敌人的真实道路。先清理附近拥堵再试。",
                        "No safe walkable enemy approach. Clear the nearby route and retry.");
                    return true;
                }
                Notify("两名追兵正从林间接近！挡住他们，再回到侦察员身边。",
                    "Two raiders are closing in! Defeat them, then return to the scout.");
                return true;
            }
            if(action=="scout_aid")
            {
                if(ScoutDefenseActive||Has("scout_aid_prepared"))return true;
                if(save.inventory[0]<1||save.inventory[2]<1)
                {
                    Notify("需要木材1和月光草1。可在林间采集，或选择直接防守。",
                        "You need 1 timber and 1 Moonherb. Gather them here or defend instead.");
                    return true;
                }
                save.inventory[0]--;save.inventory[2]--;
                Flag("scout_aid_prepared");revision++;sound="quest";
                Dialog(source.name,source.en,
                    "侦察员用药草裹好手，扶正你加固的路标。“苔根信标在西边。你亲手点亮它，我就走得回去。”",
                    "The scout bandages his hand and steadies the repaired marker. “Mossroot beacon lies west. Light it in person, and I can walk home.”",
                    C("去点亮信标","Light the beacon","close"));
                return true;
            }
            if(action=="scout"&&
                (Has("scout_defense_cleared")||Has("scout_beacon_lit")||
                 save.kills[1]>=4))
            {
                Flag("scout_saved");
                if(Has("scout_beacon_lit"))Flag("scout_rescue_aid");
                else if(Has("scout_defense_cleared"))Flag("scout_rescue_defense");
                CreditCoins(20);AddXp(20);revision++;sound="reward";
                RecordMoment("第一章 · 侦察员带着森林被改接供能的证据平安回镇。"+
                    (Has("scout_rescue_aid")?"你修复了信标撤离线。":"你亲自守住了林间道路。"),
                    "Chapter One: the scout reached town with evidence that the forest's energy was diverted."+
                    (Has("scout_rescue_aid")?" You repaired the beacon route.":" You protected the actual road."));
                // This choice has already closed its real NPC dialogue.
                // Do not reopen a modal after the gate-opening flag: world
                // ticks must dissolve the gate before its Boss can spawn,
                // and the vacated scout must not leave a stale conversation.
                // The player's spoken outcome is recorded above; the lived
                // response appears here and later at the town scout.
                Notify("侦察员带着原始调查图平安返镇！星核入口开启。获得20硬币与20经验。",
                    "Scout home with the original survey! Altar open. +20 coins and +20 XP.");
            }
            return true;
        }
    }
}
