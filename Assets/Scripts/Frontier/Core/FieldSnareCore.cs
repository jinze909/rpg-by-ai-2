using System;
using System.Collections.Generic;

namespace Starfall
{
    // Persistent gameplay upgrades may use existing v2 flags, but deployed
    // traps themselves are strictly live world actors, never serialized.
    public sealed class FieldSnare
    {
        public readonly Point foot;
        public float seconds,arming;
        public FieldSnare(Point p)
        { foot=p;seconds=25f;arming=.45f; }
    }
    public sealed partial class GameCore
    {
        private readonly List<FieldSnare> fieldSnares=new List<FieldSnare>(3);
        private readonly Dictionary<Enemy,Point> fieldSnarePreviousFeet=
            new Dictionary<Enemy,Point>();
        // Exact live actor references, not recyclable numeric enemy IDs.
        // Only a GENUINELY triggered snare grants a short nonlethal bind.
        private readonly HashSet<Enemy> fieldSnareBindCandidates=new HashSet<Enemy>();
        private int fieldSnareRoom=-1;
        private MapData fieldSnareMap;
        public IList<FieldSnare> FieldSnares
        {
            get
            {
                if(fieldSnareRoom!=save.room||
                    !Object.ReferenceEquals(fieldSnareMap,Map))
                    return new FieldSnare[0];
                return fieldSnares.AsReadOnly();
            }
        }
        public int FieldSnareLimit {get{return Has("field_snare_mastered")?3:2;}}
        public int FieldSnareCaptures
        {
            get
            {
                int n=0;for(int i=1;i<=3;i++)
                    if(Has("field_snare_capture_"+i))n++;
                return n;
            }
        }
        public bool FieldSnareTrainingAvailable
        { get { return FieldSnareCaptures==3&&Has("ally_3")&&
            !Has("field_snare_mastered"); } }
        public bool SnareMercyTrainingAvailable {get{return Has("ally_3")&&
            Has("field_snare_mercy_first")&&
            !Has("field_snare_warden_swift")&&!Has("field_snare_warden_calm");}}
        public int SnareBindEnergyCost {get{return Has("field_snare_warden_swift")?6:10;}}
        public float SnareHoldBonus {get{return Has("field_snare_warden_calm")?.6f:0f;}}
        private string FieldSnareMercyNote(bool english)
        {
            if(!Has("ally_3")||!Has("field_snare_mercy_first"))return "";
            if(Has("field_snare_warden_swift"))return english?
                "\n\nLiving-captive fast knots: binding costs only 6 energy.":
                "\n\n活口快结已练成：非致命制服只消耗6耐力。";
            if(Has("field_snare_warden_calm"))return english?
                "\n\nCalm wardenship: real snares hold raiders 0.6 seconds longer.":
                "\n\n守护稳索已练成：真实绊索束缚时间延长0.6秒。";
            return english?
                "\n\nYou brought a raider in alive. Choose ONE humane-warden technique: fast knots (bind for 6 energy), or steady lines (+0.6s hold). Each costs 1 timber, 2 crystals and 1 machine part.":
                "\n\n你带回了一个活口。只能选一种守护技：快结（制服耗6耐力）或稳索（束缚+0.6秒）；都消耗木材1、星晶2、零件1。";
        }
        private string FieldSnareTrainingNote(bool english)
        {
            if(!Has("ally_3"))return "";
            if(Has("field_snare_mastered"))return english?
                "\n\nNoll's reinforced line is now your own: three active snares, faster arming and longer nonlethal holds."+FieldSnareMercyNote(true):
                "\n\n诺尔的强化绊索已经装好：可同时布置三道，安装更快，牵制更久。"+FieldSnareMercyNote(false);
            return english?
                "\n\nNoll is collecting proof from your field snares ("+
                    FieldSnareCaptures+"/3 real captures). Three contacts unlock a paid reinforced line."+FieldSnareMercyNote(true):
                "\n\n诺尔在收集真实绊索记录（"+FieldSnareCaptures+
                    "/3 次实际束缚）。完成三次后可出资强化。"+FieldSnareMercyNote(false);
        }
        private void AppendFieldSnareTrainingChoice(List<Choice> list)
        {
            if(SnareMercyTrainingAvailable)
            {
                list.Add(C("活口快结 · 木1、晶2、零件1",
                    "Merciful fast knots · 1 timber, 2 crystals, 1 part",
                    "field_snare_warden_swift"));
                list.Add(C("守护稳索 · 木1、晶2、零件1",
                    "Steady restraint · 1 timber, 2 crystals, 1 part",
                    "field_snare_warden_calm"));
            }
            if(FieldSnareTrainingAvailable)
                list.Add(C("强化实战绊索 · 星晶2、零件2",
                    "Reinforce field snares · 2 crystals, 2 parts",
                    "field_snare_masterwork"));
        }
        private bool ChooseFieldSnareTraining(string action)
        {
            bool swift=action=="field_snare_warden_swift";
            bool calm=action=="field_snare_warden_calm";
            if(action!="field_snare_masterwork"&&!swift&&!calm)return false;
            WorldObject smith=pendingDialogueSource;
            if((swift||calm?!SnareMercyTrainingAvailable:
                !FieldSnareTrainingAvailable)||mode!="world"||Dying||
                practicing||save.room!=0||smith==null||
                smith.kind!="forge"||pendingDialogueSourceRoom!=0||
                !SourceBelongsToActiveMap(smith)||
                !HasPhysicalInteractionReach(smith))return true;
            if(FieldWorkPreventsRest||!SafeTravel)
            {
                Notify("还有敌情或现场行动，暂时无法重制绊索。",
                    "Clear nearby threats and fieldwork before reinforcing snares.");
                return true;
            }
            if(swift||calm)
            {
                if(!Spend(1,2,0,0,1))return true;
                Flag(swift?"field_snare_warden_swift":"field_snare_warden_calm");
                RecordMoment(swift?
                    "诺尔教会你如何在敌人挣脱前打出不伤血肉的快结。":
                    "诺尔在绊索上加了一圈缓冲编织，让受制的袭击者得到更长的冷静时间。",
                    swift?
                    "Noll teaches a quicker living-captive knot that protects both prisoner and traveler.":
                    "Noll braids a safer, steadier restraint line that buys time without a killing blow.");
                revision++;sound="reward";
                Notify(swift?"快结学会：非致命制服耐力10→6。":
                    "稳索学会：所有真实绊索束缚延长0.6秒。",
                    swift?"Fast knots learned: bind stamina 10 → 6.":
                    "Steady lines learned: real snares hold 0.6s longer.");
                return true;
            }
            if(!Spend(0,2,0,0,2))return true;
            Flag("field_snare_mastered");
            AddXp(45);revision++;sound="reward";
            RecordMoment(
                "诺尔用废弃车轮的齿片加强木桩。第三根绊索不再容易松脱，工匠写下每一处伤者都能撤离的路口。",
                "Noll braces a third line with salvaged gear teeth, and marks escape crossings where captured foes cannot reach the wounded.");
            Notify("绊索专精：最多三道、布设0.25秒、束缚2.1秒。",
                "Snare mastery: three traps, .25s arming, 2.1s holds.");
            return true;
        }
        public bool CanLayFieldSnare
        {
            get
            {
                return mode=="world"&&!Dying&&!practicing&&!racing&&
                    !ActionTransitionBlocked&&CanStand(Position)&&
                    fieldSnares.Count<FieldSnareLimit&&
                    save.inventory[0]>=1&&
                    save.inventory[4]>=LastLanternSnareGearCost;
            }
        }
        // An untriggered stake can be physically reclaimed for its timber.
        // The mechanism remains spent; there is no kill, XP or trap duplication.
        private FieldSnare RecoverableFieldSnare()
        {
            if(mode!="world"||Dying||practicing||racing||
                ActionTransitionBlocked||save.energy<6f||
                fieldSnareRoom!=save.room||
                !Object.ReferenceEquals(fieldSnareMap,Map)||
                !CanStand(Position))return null;
            FieldSnare nearest=null;float closest=1.8f;
            foreach(FieldSnare snare in fieldSnares)
            {
                float distance=(Position-snare.foot).Length;
                if(snare.seconds<=0f||distance>=closest||
                    !CanStand(snare.foot)||
                    !ClearAttackPath(Position,snare.foot))continue;
                closest=distance;nearest=snare;
            }
            if(nearest==null)return null;
            foreach(Enemy foe in enemies)
                if(foe!=null&&foe.hp>0f&&
                    (foe.Position-Position).Length<3f)return null;
            return nearest;
        }
        // A real planted snare is the ONLY source of a bind opportunity.
        // Other stuns, ordinary attacks, distant enemies and Bosses are not
        // converted into free instant kills or mission progression.
        private Enemy BindableFieldEnemy()
        {
            if(mode!="world"||Dying||practicing||racing||
               ActionTransitionBlocked||!CanStand(Position)||
               save.energy<SnareBindEnergyCost||fieldSnareRoom!=save.room||
               !Object.ReferenceEquals(fieldSnareMap,Map))return null;
            Enemy nearest=null;float best=1.5f;
            foreach(Enemy foe in fieldSnareBindCandidates)
            {
                if(foe==null||foe.hp<=0f||foe.boss||
                    !enemies.Contains(foe)||foe.stun<=.35f||
                    ArrowSoundCannotOverrideObjective(foe)||
                    !CanStand(foe.Position))continue;
                float distance=(Position-foe.Position).Length;
                if(distance>=best||!ClearAttackPath(Position,foe.Position))
                    continue;
                best=distance;nearest=foe;
            }
            return nearest;
        }
        public Enemy SnaredBindTarget {get{return BindableFieldEnemy();}}
        public bool CanBindSnaredEnemy {get{return BindableFieldEnemy()!=null;}}
        public bool BindSnaredEnemy()
        {
            Enemy target=BindableFieldEnemy();
            if(target==null)return false;
            // Remove this one ACTUAL actor, not every enemy with its ID.
            // Pending attack warnings are retired; already released
            // projectiles retain their actual flight and collision.
            save.energy-=SnareBindEnergyCost;
            fieldSnareBindCandidates.Remove(target);
            fieldSnarePreviousFeet.Remove(target);
            RemoveEnemyPendingWarning(target);
            enemies.Remove(target);
            // An actual physical restraint counts for a civilian patrol.
            // Arbitrary actor removals still invalidate its signed receipt.
            bool patrolRestraint=OnFieldPatrolEnemyRestrained(target);
            bool outpostRestraint=OnFieldOutpostEnemyRestrained(target);
            bool watchRestraint=OnVantageWatchEnemyRestrained(target);
            bool courierRestraint=OnFarCourierEnemyRestrained(target);
            effects.Add(new Effect("burst",target.Position,.65f)
                {radius=.85f,variant=0});
            effects.Add(new Effect("number",target.Position,.9f,
                L("非致命制服","RESTRAINED")));
            bool firstBind=!Has("field_snare_mercy_first");
            if(firstBind)
            {
                Flag("field_snare_mercy_first");
                AddXp(25);
                RecordMoment(
                    "绊索拦住了袭击者，旅人亲手收紧绳结，留下活口，不让一次遭遇变成又一份伤亡名单。",
                    "A field snare stopped a raider. The traveler secured the knot and left a living captive rather than adding another name to the dead.");
            }
            sound="counter";revision++;
            if(watchRestraint)
                Notify("远端观测站：一名真实袭击者已被绊索活捉，回原站登记。",
                    "Lookout watch: one original raider captured alive. Return to sign at the original station.");
            else if(courierRestraint)
                Notify("真实劫匪已被绊索活捉。必须亲手将救援包交给守棚人，才能登记安全与治疗奖励。",
                    "Original parcel raider restrained alive. Complete the real delivery to earn security and care rewards.");
            else if(patrolRestraint)
                Notify("巡护委托：已活捉一名真实巡兵，剩余"+
                    FieldPatrolRemaining+"名。回原地标提交回执。",
                    "Patrol: one raider restrained alive. "+
                    FieldPatrolRemaining+" remain; report at the original post.");
            else
                Notify(firstBind?
                    "首次活捉成功！新手帐：诺尔的活口与守护技。没有击杀或战利品。":
                    "袭击者已非致命制服；没有击杀、战利品或 Boss 奖励。",
                    firstBind?
                    "First living capture! New journal chapter: Noll's humane techniques. No kill or loot.":
                    "Raider restrained alive. No kill, loot or Boss credit.");
            return true;
        }
        public bool CanRecoverFieldSnare {get{return RecoverableFieldSnare()!=null;}}
        public bool RecoverFieldSnare()
        {
            FieldSnare snare=RecoverableFieldSnare();
            if(snare==null||!fieldSnares.Remove(snare))return false;
            save.energy-=6f;save.inventory[0]++;
            effects.Add(new Effect("number",snare.foot,.9f,
                L("收回木桩","STAKE RECOVERED")));
            sound="quest";revision++;
            return true;
        }
        private void ResetFieldSnares()
        {
            fieldSnares.Clear();fieldSnarePreviousFeet.Clear();
            fieldSnareBindCandidates.Clear();
            fieldSnareRoom=-1;fieldSnareMap=null;
        }
        private void ValidateFieldSnareWorld()
        {
            if(fieldSnareRoom==save.room&&
                Object.ReferenceEquals(fieldSnareMap,Map))return;
            ResetFieldSnares();
            fieldSnareRoom=save.room;fieldSnareMap=Map;
        }
        public bool LayFieldSnare(Point direction)
        {
            if(mode!="world"||Dying||practicing||racing||
                ActionTransitionBlocked||!CanStand(Position))return false;
            ValidateFieldSnareWorld();
            if(fieldSnares.Count>=FieldSnareLimit)
            {
                Notify("已布置两道绊索；等待敌人触发或自然失效。",
                    "Two snares are set. Wait for contact or expiry.");
                return false;
            }
            if(!Finite(direction.x)||!Finite(direction.y)||
               direction.Length<.05f)direction=aim;
            if(!Finite(direction.x)||!Finite(direction.y)||
               direction.Length<.05f)return false;
            Point foot=Position+direction.Normalized*1.35f;
            if(!CanStand(foot)||!ClearAttackPath(Position,foot))
            {
                Notify("绊索必须布设在无遮挡、可以站立的真实地面。",
                    "Lay a snare on unobstructed, walkable ground.");
                return false;
            }
            foreach(FieldSnare existing in fieldSnares)
                if((existing.foot-foot).Length<1.1f)
                {
                    Notify("陷阱间距不足，请选择另一段通路。",
                        "Leave space between nearby snares.");
                    return false;
                }
            // Never spend supplies before the full terrain and distance
            // checks, and never spawn a trap on top of a living body.
            foreach(Enemy foe in enemies)
                if(foe!=null&&foe.hp>0f&&
                    (foe.Position-foot).Length<.65f)return false;
            if(save.companion>=0&&
                (companionPosition-foot).Length<.65f)return false;
            if(!Spend(1,0,0,0,LastLanternSnareGearCost))return false;
            var snare=new FieldSnare(foot);
            if(Has("field_snare_mastered"))snare.arming=.25f;
            fieldSnares.Add(snare);
            effects.Add(new Effect("number",foot,.75f,
                L("绊索已布置","SNARE SET")));
            sound="quest";revision++;
            return true;
        }
        private void CaptureFieldSnareEnemyFooting()
        {
            ValidateFieldSnareWorld();
            fieldSnarePreviousFeet.Clear();
            if(fieldSnares.Count==0)return;
            foreach(Enemy e in enemies)
                if(e!=null&&e.hp>0f)
                    fieldSnarePreviousFeet[e]=e.Position;
        }
        // Exact closest point along the true enemy stride prevents fast
        // movers from tunneling across the narrow triggered footprint.
        private bool FieldSnareCrossed(FieldSnare snare,Enemy e)
        {
            if(e==null||e.hp<=0f||e.boss||
               !enemies.Contains(e)||ArrowSoundCannotOverrideObjective(e)||
               !CanStand(e.Position)||!CanStand(snare.foot))return false;
            Point start;
            if(!fieldSnarePreviousFeet.TryGetValue(e,out start)||
               !Finite(start.x)||!Finite(start.y))start=e.Position;
            Point end=e.Position,delta=end-start;
            float squared=delta.x*delta.x+delta.y*delta.y;
            float fraction=squared>.00000001f?
                Clamp(((snare.foot.x-start.x)*delta.x+
                       (snare.foot.y-start.y)*delta.y)/squared,0f,1f):0f;
            Point nearest=start+delta*fraction;
            return (nearest-snare.foot).Length<=.55f&&
                ClearAttackPath(snare.foot,nearest);
        }
        private void TickFieldSnares(float dt)
        {
            ValidateFieldSnareWorld();
            fieldSnareBindCandidates.RemoveWhere(e=>e==null||
                e.hp<=0f||e.stun<=.01f||!enemies.Contains(e));
            for(int i=fieldSnares.Count-1;i>=0;i--)
            {
                FieldSnare snare=fieldSnares[i];
                snare.seconds-=dt;
                snare.arming=Math.Max(0f,snare.arming-dt);
                if(snare.seconds<=0f){fieldSnares.RemoveAt(i);continue;}
                if(snare.arming>0f)continue;
                Enemy triggered=null;
                foreach(Enemy e in enemies)
                    if(FieldSnareCrossed(snare,e)){triggered=e;break;}
                if(triggered==null)continue;
                fieldSnares.RemoveAt(i);
                // A grounded, non-lethal contact interrupts an actual
                // enemy. It creates NO boss credit, damage, loot, or kill.
                triggered.stun=Math.Max(triggered.stun,
                    (Has("field_snare_mastered")?2.1f:1.5f)+SnareHoldBonus);
                triggered.armorOpenTime=Math.Max(triggered.armorOpenTime,.7f);
                triggered.state=0;triggered.timer=Math.Max(triggered.timer,.35f);
                fieldSnareBindCandidates.Add(triggered);
                effects.Add(new Effect("burst",snare.foot,.36f)
                    {radius=.8f,variant=0});
                effects.Add(new Effect("number",triggered.Position,.8f,
                    L("绊索束缚！","SNARED!")));
                sound="counter";revision++;
                int next=FieldSnareCaptures+1;
                if(next<=3&&!Has("field_snare_capture_"+next))
                {
                    Flag("field_snare_capture_"+next);
                    AddXp(12);
                    if(next==3)
                    {
                        RecordMoment(
                            "三名沿路巡敌先后被绊索生擒。诺尔愿意把失修的防线改成能反复保护伤者的工具。",
                            "Three separate live patrol contacts proved a safe capture line. Noll offers to reinforce the rig for future travelers.");
                        Notify("绊索实战 3/3：返回诺尔铁匠铺可强化。",
                            "Three real snare captures: visit Noll to reinforce your rig.");
                    }
                }
            }
        }
    }
}
