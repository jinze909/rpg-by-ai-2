using System;
using System.Collections.Generic;

namespace Starfall
{
    // Seven authored battlefield contraptions: real scene sources, enemy
    // collision, energy cost and one-time story flags, no new save fields.
    public sealed partial class GameCore
    {
        private SaveData fieldTacticSave;
        private MapData fieldTacticMap;
        private readonly Dictionary<WorldObject,float> fieldTacticReady=
            new Dictionary<WorldObject,float>();
        private static readonly string[] TacticZh={"","枯根诱捕铃","遗迹退潮闸","矿场排渣阀","堡垒断弧器","雾港潮哨","沙海折光棱镜","霜钟静风铃"};
        private static readonly string[] TacticEn={"","Rootsnare bell","Tidebreak sluice","Slag relief valve","Citadel arc-breaker","Mistport foghorn","Glasswind prism","Frostchime stillbell"};
        public int FieldTacticMasteryCount
        {
            get { int n=0;for(int i=1;i<=4;i++)if(Has("field_tactic_used_"+i))n++;return n; }
        }
        // Preserve the four-chapter mastery reward and give the outer
        // expedition its own permanent, one-time flag-only milestone.
        public int OuterTacticMasteryCount
        {
            get { int n=0;for(int i=5;i<=7;i++)if(Has("field_tactic_used_"+i))n++;return n; }
        }
        private static bool TacticInterceptsArrows(int room)
        { return room==4||room==5; }

        // Live, source-bound combat drill: exactly two genuine enemies, one
        // successful use of this specific battlefield device, then a signed
        // return. No serialized actor/clock and no new SaveData v2 fields.
        private int tacticTrialRoom=-1;
        private WorldObject tacticTrialSource;
        private MapData tacticTrialMap;
        private SaveData tacticTrialSave;
        private readonly List<Enemy> tacticTrialEnemies=new List<Enemy>();
        private readonly HashSet<Enemy> tacticTrialKills=new HashSet<Enemy>();
        private bool tacticTrialUsed;
        private static readonly int[,] tacticTrialRoles={
            {0,1},{1,0},{2,0},{2,1},{1,1},{2,2},{0,0}
        };
        public bool FieldTacticTrialActive
        {get{return tacticTrialRoom>=1&&tacticTrialRoom<=7&&
            tacticTrialRoom==save.room&&
            Object.ReferenceEquals(tacticTrialMap,Map)&&
            Object.ReferenceEquals(tacticTrialSave,save)&&
            FieldTacticSource(tacticTrialSource);}}
        public bool FieldTacticTrialDone(int room)
        {return room>=1&&room<=7&&Has("tactic_trial_done_"+room);}
        public int FieldTacticTrialWins
        {get{int n=0;for(int i=1;i<=7;i++)if(FieldTacticTrialDone(i))n++;return n;}}
        public int FieldTacticTrialRemaining
        {get{int n=0;if(FieldTacticTrialActive)
            foreach(Enemy e in tacticTrialEnemies)
                if(!tacticTrialKills.Contains(e)&&enemies.Contains(e)&&e.hp>0)n++;
            return n;}}
        public bool FieldTacticTrialUsed
        {get{return FieldTacticTrialActive&&tacticTrialUsed;}}
        public bool FieldTacticTrialReady
        {get{return FieldTacticTrialUsed&&tacticTrialEnemies.Count==2&&
            tacticTrialKills.Count==2&&FieldTacticTrialRemaining==0;}}
        public Point FieldTacticTrialGuideTarget
        {get{if(!FieldTacticTrialActive)return Position;
            foreach(Enemy e in tacticTrialEnemies)
                if(!tacticTrialKills.Contains(e)&&enemies.Contains(e)&&e.hp>0)
                    return e.Position;
            return new Point(tacticTrialSource.x,tacticTrialSource.y);}}
        private bool IsFieldTacticTrialEnemy(Enemy e)
        {return e!=null&&FieldTacticTrialActive&&tacticTrialEnemies.Contains(e);}
        private void OnFieldTacticTrialEnemyDefeated(Enemy e)
        {if(IsFieldTacticTrialEnemy(e)&&e.hp<=0)tacticTrialKills.Add(e);}
        private void ResetFieldTacticTrial()
        {
            foreach(Enemy e in tacticTrialEnemies)
            {RemoveEnemyPendingWarning(e);regionalAmbientEnemies.Remove(e);enemies.Remove(e);}
            projectiles.RemoveAll(p=>p!=null&&p.hostileShooter!=null&&
                tacticTrialEnemies.Contains(p.hostileShooter));
            tacticTrialEnemies.Clear();tacticTrialKills.Clear();
            tacticTrialRoom=-1;tacticTrialSource=null;
            tacticTrialMap=null;tacticTrialSave=null;tacticTrialUsed=false;
        }
        private void TickFieldTacticTrial()
        {
            if(tacticTrialRoom<0)return;
            if(!FieldTacticTrialActive){ResetFieldTacticTrial();return;}
            foreach(Enemy e in tacticTrialEnemies)
                if(e==null||(!tacticTrialKills.Contains(e)&&
                    (!enemies.Contains(e)||e.hp<=0)))
                {ResetFieldTacticTrial();revision++;
                 Notify("演练敌人未经真实战斗便消失。委托作废。",
                     "A trial fighter vanished without a true defeat. Contract void.");
                 return;}
        }
        private bool StageFieldTacticTrial(WorldObject source)
        {
            Point center=new Point(source.x,source.y);
            var pads=new List<Point>();
            for(int radius=3;radius<=6&&pads.Count<2;radius++)
            for(int dy=-radius;dy<=radius&&pads.Count<2;dy++)
            for(int dx=-radius;dx<=radius&&pads.Count<2;dx++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                Point p=new Point((float)Math.Floor(center.x)+dx+.5f,
                                  (float)Math.Floor(center.y)+dy+.5f);
                float distance=(p-center).Length;
                // A voluntary two-person drill starts at arm's length
                // from its signed device, not at the remote regional gate.
                // Keep true 1.45-unit hero/enemy body clearance even in
                // the mine's narrow legal shelf; the warning effect cues
                // both spawned actors before they can attack.
                if(distance<2.8f||distance>5.7f||!CanStand(p)||
                   (p-Position).Length<1.45f||
                   !ClearAttackPath(center,p)||
                   NavigationPathFrom(Position,p,true).Count==0)continue;
                bool clear=true;
                foreach(WorldObject obj in Map.objects)
                    if(obj!=null&&obj!=source&&
                        (p-new Point(obj.x,obj.y)).Length<2.25f)
                    {clear=false;break;}
                if(!clear)continue;
                if(Map.decor!=null)
                    foreach(Decoration d in Map.decor)
                        if((p-new Point(d.x,d.y)).Length<
                            RegionalDecorationEnemyPad(d))
                        {clear=false;break;}
                if(!clear)continue;
                foreach(Enemy enemy in enemies)
                    if(enemy!=null&&enemy.hp>0&&
                        (p-enemy.Position).Length<2.5f)
                    {clear=false;break;}
                if(!clear)continue;
                // The sealed Ironreach upper shelf has two safe, exposed
                // ranged pads exactly two whole tiles apart. Keep both
                // genuine feet rather than demanding the much wider 3-tile
                // baseline-army spacing from this TWO-actor local drill.
                // Each actor still has an independent non-overlapping body,
                // real line of sight, and a collision-tested walk to hero.
                foreach(Point old in pads)
                    if((p-old).Length<1.95f){clear=false;break;}
                if(clear)pads.Add(p);
            }
            if(pads.Count!=2)return false;
            tacticTrialRoom=save.room;tacticTrialSource=source;
            tacticTrialMap=Map;tacticTrialSave=save;tacticTrialUsed=false;
            tacticTrialEnemies.Clear();tacticTrialKills.Clear();
            for(int i=0;i<2;i++)
            {
                Enemy e=new Enemy(++enemyId,tacticTrialRoles[save.room-1,i],
                    pads[i].x,pads[i].y,40+save.room*8);
                enemies.Add(e);tacticTrialEnemies.Add(e);
                effects.Add(new Effect("enemyRelease",e.Position,.65f)
                    {variant=e.kind});
            }
            revision++;sound="alert";return true;
        }
        private bool ChooseFieldTacticTrial(string action)
        {
            if(action!="field_tactic_trial_begin"&&
               action!="field_tactic_trial_report"&&
               action!="field_tactic_trial_abort")return false;
            WorldObject source=pendingDialogueSource;
            if(!FieldTacticSource(source)||pendingDialogueSourceRoom!=save.room||
                !HasPhysicalInteractionReach(source)||!CanStand(Position)||
                mode!="world"||Dying||practicing)return true;
            int room=save.room;
            if(action=="field_tactic_trial_abort")
            {
                if(FieldTacticTrialActive&&
                   Object.ReferenceEquals(tacticTrialSource,source))
                {ResetFieldTacticTrial();revision++;sound="ui";
                 Notify("演练已撤回，不结算奖励。","Trial withdrawn without reward.");}
                return true;
            }
            if(action=="field_tactic_trial_report")
            {
                if(!FieldTacticTrialReady||
                   !Object.ReferenceEquals(tacticTrialSource,source)||
                   !SafeTravel)return true;
                ResetFieldTacticTrial();
                if(FieldTacticTrialDone(room))return true;
                Flag("tactic_trial_done_"+room);
                CreditCoins(22+room*4);CreditShards(1);AddXp(48+room*8);
                RecordMoment("第"+room+"区 · 用本地战术装置反制袭击，并亲自击退两名敌人。",
                    "Region "+room+": used the actual field counter and defeated two real trial raiders.");
                revision++;sound="reward";
                Notify("演练回执已签收：硬币"+(22+room*4)+"、碎片1、经验"+(48+room*8)+"。",
                    "Signed trial: "+(22+room*4)+" coins, 1 shard, "+(48+room*8)+" XP.");
                if(FieldTacticTrialWins==7&&!Has("tactic_trials_mastered"))
                {
                    Flag("tactic_trials_mastered");CreditSkillPoints(1);
                    RecordMoment("七地区的现场反制均通过真实实战认证。",
                        "All seven region-specific countermeasures proved in live combat.");
                    Notify("七地实战全部通过！额外技能点1。",
                        "Seven field trials complete! +1 talent point.");
                }
                return true;
            }
            if(FieldTacticTrialDone(room)||FieldTacticTrialActive||
               FieldWorkPreventsRest||!SafeTravel)
            {Notify("先完成其他现场战斗，再开始演练。",
                "Finish current fieldwork and nearby danger before starting.");
             return true;}
            if(!StageFieldTacticTrial(source))
            {Notify("装置附近不足两条安全可步行的敌人进路，清理现场再试。",
                "No complete two-raider walkable lane here. Clear the area and retry.");
             return true;}
            Notify("实战演练开始：用现场装置命中至少一名指定敌人，击败两人后回这里交回回执。",
                "Trial started: counter at least one assigned enemy, defeat both and return here.");
            return true;
        }
        private void ResetFieldTactics()
        {
            ResetFieldTacticTrial();
            fieldTacticReady.Clear();fieldTacticMap=null;fieldTacticSave=null;
        }
        private void EnsureFieldTacticSession()
        {
            if(!Object.ReferenceEquals(fieldTacticMap,Map)||
               !Object.ReferenceEquals(fieldTacticSave,save))
            {
                fieldTacticReady.Clear();fieldTacticMap=Map;fieldTacticSave=save;
            }
        }
        private bool FieldTacticSource(WorldObject o)
        {
            return o!=null&&o.kind=="fielddevice"&&
                save.room>=1&&save.room<=7&&
                o.id=="field_tactic_"+save.room&&
                SourceBelongsToActiveMap(o)&&IsObjectPresent(o);
        }
        public float FieldTacticCooldown(WorldObject o)
        {
            if(!FieldTacticSource(o))return -1f;
            EnsureFieldTacticSession();float time;
            return fieldTacticReady.TryGetValue(o,out time)?
                Math.Max(0f,time-save.totalTime):0f;
        }
        // The exact live, line-of-sight target count used by the real device
        // also drives its world prompt. Never advertise a free-use battlefield
        // counter when the current fight is physically out of range.
        public int FieldTacticLiveThreatCount(WorldObject o)
        {
            if(!FieldTacticSource(o))return 0;
            Point origin=new Point(o.x,o.y);
            int count=0;
            foreach(Enemy e in enemies)
                if(FieldTacticCanAffect(e,origin))count++;
            if(TacticInterceptsArrows(save.room))
                foreach(Projectile shot in projectiles)
                    if(shot!=null&&!shot.friendly&&
                        shot.hostileShooter!=null&&
                        enemies.Contains(shot.hostileShooter)&&
                        (shot.position-origin).Length<=5.8f&&
                        ClearAttackPath(origin,shot.position))count++;
            return count;
        }
        private bool FieldTacticCanAffect(Enemy e,Point source)
        {
            return e!=null&&!e.boss&&e.hp>0&&e.kind>=0&&e.kind<=2&&
                enemies.Contains(e)&&CanStand(e.Position)&&
                (e.Position-source).Length<=5.8f&&
                ClearAttackPath(source,e.Position);
        }
        private bool TalkFieldTactics(WorldObject o)
        {
            if(o==null||o.kind!="fielddevice")return false;
            if(!FieldTacticSource(o)||!HasPhysicalInteractionReach(o)||
                !CanStand(Position))
            {
                Notify("请走近现场装置操作。","Approach the actual mechanism.");return true;
            }
            int room=save.room;Point p=new Point(o.x,o.y);
            int visible=0,arrows=0;
            foreach(Enemy e in enemies)
                if(FieldTacticCanAffect(e,p))visible++;
            if(TacticInterceptsArrows(room))foreach(Projectile a in projectiles)
                if(a!=null&&!a.friendly&&a.hostileShooter!=null&&
                    enemies.Contains(a.hostileShooter)&&
                    (a.position-p).Length<=5.8f&&ClearAttackPath(p,a.position))arrows++;
            string zh=room==1?
                "断根铃可缠住视线内的追兵。你仍需亲自击退他们。":
                room==2?"破旧水闸能把敌人从脚下推走，墙后无人会被凭空冲倒。":
                room==3?"排渣蒸汽会迫使近处重甲卫兵露出护甲破绽。":
                room==4?"断弧器能打断射手的动作并截断附近真正飞来的敌箭。守护者无法被这套装置击倒。":
                room==5?"拉响潮哨，浓雾会打乱近岸弓手的瞄准，并吞没视线内的来箭。":
                room==6?"击亮碎镜棱晶，使近旁重甲护卫的装甲接缝暴露；射手也会被反光短暂致盲。":
                "拨动静风铃，让逼近的狼群与袭击者失去节奏。真正的守护者仍需正面迎战。";
            string en=room==1?
                "Ring the dead-root bell to snare visible raiders. You must still defeat them yourself.":
                room==2?"Open the sluice to push visible foes off their footing. No water passes through solid walls.":
                room==3?"Vent the mining pipe to expose nearby armored soldiers' weak points.":
                room==4?"The arc-breaker interrupts archers and catches actual incoming hostile arrows, not guardian attacks.":
                room==5?"Sound the foghorn: incoming arrows disappear in the sea mist while coastal archers lose their aim.":
                room==6?"Flash the fractured prism to expose armored seams and blind nearby sentries.":
                "Ring the stillbell to break approaching raiders' momentum. Guardians remain immune.";
            float remain=FieldTacticCooldown(o);
            Dialog(o.name,o.en,zh+"\n\n附近可影响敌人 "+visible+
                (TacticInterceptsArrows(room)?" · 敌方飞弹 "+arrows:"")+"。"+
                (remain>0?" 冷却剩余"+(int)Math.Ceiling(remain)+"秒。":
                  " 需要14耐力，冷却24秒。")+
                (room<=4?
                  "\n\n首次实战使用可记录战场经验；前四区全部完成奖励一个技能点。":
                  "\n\n远岸三地首次实战使用均可记录经验，集齐三处另奖一个技能点。"),
                en+"\n\nActual exposed enemies: "+visible+
                (TacticInterceptsArrows(room)?"; hostile arrows: "+arrows:"")+
                (remain>0?"; resets in "+(int)Math.Ceiling(remain)+"s.":
                    "; costs 14 energy, resets in 24s.")+
                (room<=4?
                  "\n\nUse all four chapter mechanisms in real combat to earn one talent point.":
                  "\n\nMaster all three outer-region mechanisms in real combat to earn a separate talent point."),
                C("启动现场装置","Activate mechanism","field_tactic_trigger"),
                FieldTacticTrialDone(room)?
                    C("本地实战回执已签收","Combat trial already signed","close"):
                FieldTacticTrialActive?
                    (FieldTacticTrialReady?
                        C("签收现场演练回执","Sign completed tactical trial","field_tactic_trial_report"):
                        C("演练中 · 还剩"+FieldTacticTrialRemaining+"名 · "+
                            (FieldTacticTrialUsed?"装置已启动":"须启动装置"),
                          "Live drill · "+FieldTacticTrialRemaining+" foes · "+
                            (FieldTacticTrialUsed?"counter used":"counter required"),"close")):
                    C("开启本地实战演练 · 两名袭击者","Start local combat drill · two raiders","field_tactic_trial_begin"),
                FieldTacticTrialActive?
                    C("撤回演练","Withdraw the drill","field_tactic_trial_abort"):
                    C("查看其他路线","Inspect other paths","close"));
            return true;
        }
        // Shift+G uses the same authenticated mechanism as the ordinary E
        // dialogue but leaves the world running: one deliberate input, no
        // separate damage/energy/target pipeline and no hidden auto-target.
        public bool QuickActivateFieldTactic()
        {
            if(mode!="world"||Dying||practicing||Map==null||
                Map.objects==null||!CanStand(Position))return false;
            // E and its O-cycle focus belong to the player's chosen NPC
            // or survivor. A dedicated combat shortcut must not inherit
            // that unrelated menu selection. Find ONLY the current map's
            // authenticated contraption and recheck its real contact foot.
            // No global nearest actor or offscreen/other-room object can
            // redirect a live 14-energy combat action.
            WorldObject source=null;
            foreach(WorldObject candidate in Map.objects)
                if(FieldTacticSource(candidate)&&
                   HasPhysicalInteractionReach(candidate))
                {source=candidate;break;}
            if(source==null)return false;
            return ActivateFieldTactic(source,false);
        }
        private bool ChooseFieldTactics(string action)
        {
            if(action!="field_tactic_trigger")return false;
            return ActivateFieldTactic(pendingDialogueSource,true);
        }
        private bool ActivateFieldTactic(WorldObject o,bool dialogueChoice)
        {
            if(!FieldTacticSource(o)||
                (dialogueChoice&&pendingDialogueSourceRoom!=save.room)||
                !HasPhysicalInteractionReach(o)||!CanStand(Position)||
                Dying||practicing||mode!="world")return true;
            float remain=FieldTacticCooldown(o);
            if(remain>.001f)
            {
                Notify("装置还未完成复位。","The mechanism is still resetting.");return true;
            }
            if(save.energy<14f)
            {
                Notify("启动装置需要14耐力。","Requires 14 energy.");return true;
            }
            Point origin=new Point(o.x,o.y);int room=save.room;
            var targets=new List<Enemy>();
            foreach(Enemy e in enemies)
                if(FieldTacticCanAffect(e,origin))targets.Add(e);
            var arrows=new List<Projectile>();
            if(TacticInterceptsArrows(room))foreach(Projectile shot in projectiles)
                if(shot!=null&&!shot.friendly&&shot.hostileShooter!=null&&
                    enemies.Contains(shot.hostileShooter)&&
                    (shot.position-origin).Length<=5.8f&&
                    ClearAttackPath(origin,shot.position))arrows.Add(shot);
            if(targets.Count==0&&arrows.Count==0)
            {
                Notify("这里没有视线内的真实敌人或飞弹。不扣资源、不触发冷却。",
                    "No reachable foe or hostile shot. No cost or cooldown.");
                return true;
            }
            save.energy-=14f;
            fieldTacticReady[o]=save.totalTime+24f;
            if(FieldTacticTrialActive&&
               Object.ReferenceEquals(tacticTrialSource,o))
                foreach(Enemy enemy in targets)
                    if(tacticTrialEnemies.Contains(enemy))
                    {tacticTrialUsed=true;break;}
            foreach(Enemy e in targets)
            {
                if(room==1)
                {e.stun=Math.Max(e.stun,2.3f);e.stagger=Math.Max(e.stagger,1.1f);}
                else if(room==2)
                {
                    Point dir=(e.Position-origin).Normalized;
                    Point displaced=e.Position+dir*1.25f;
                    if(dir.Length>.1f&&CanStand(displaced)&&
                        ClearAttackPath(e.Position,displaced))
                    {e.x=displaced.x;e.y=displaced.y;}
                    e.stun=Math.Max(e.stun,.9f);
                }
                else if(room==3)
                {
                    if(e.kind==2)e.armorOpenTime=Math.Max(e.armorOpenTime,3.5f);
                    e.stun=Math.Max(e.stun,e.kind==2?1.4f:.7f);
                }
                else if(room==4)
                {
                    e.stun=Math.Max(e.stun,e.kind==1?2.4f:.8f);
                    e.stagger=Math.Max(e.stagger,.8f);
                }
                else if(room==5)
                {
                    e.stun=Math.Max(e.stun,e.kind==1?2.8f:.55f);
                    e.stagger=Math.Max(e.stagger,e.kind==1?1.1f:.35f);
                }
                else if(room==6)
                {
                    if(e.kind==2)
                        e.armorOpenTime=Math.Max(e.armorOpenTime,4f);
                    e.stun=Math.Max(e.stun,e.kind==2?1.9f:
                        e.kind==1?1.5f:.7f);
                }
                else
                {
                    e.stun=Math.Max(e.stun,e.kind==0?2.7f:1.2f);
                    e.stagger=Math.Max(e.stagger,e.kind==0?1.7f:.6f);
                }
                effects.Add(new Effect("number",e.Position,.65f,
                    (room==3||room==6)&&e.kind==2?L("护甲暴露","ARMOR OPEN"):
                    room==2?L("击退","SWEPT"):
                    room==1?L("根缚","ROOTSNARED"):
                    room==5?L("雾中失准","FOG DISRUPTION"):
                    room==6?L("棱镜耀光","PRISM FLASH"):
                    room==7?L("霜铃震慑","FROST CHIME"):L("断弧","ARC BROKEN")));
            }
            foreach(Projectile arrow in arrows)projectiles.Remove(arrow);
            effects.Add(new Effect("burst",origin,.7f)
                {radius=room==2?4f:3f,variant=room});
            sound=room==2||room==5?"splash":room==3||room==6?"explosion":"alert";
            revision++;
            if(!Has("field_tactic_used_"+room))
            {
                Flag("field_tactic_used_"+room);AddXp(12);
                RecordMoment("第"+room+"区 · 亲自利用"+TacticZh[room]+
                    "的环境反制法应对真实敌人。",
                    "Chapter "+room+": used the "+TacticEn[room]+
                    " in an actual encounter.");
                if(room>=5&&OuterTacticMasteryCount==3&&
                    !Has("outer_field_tactics_mastered"))
                {
                    Flag("outer_field_tactics_mastered");CreditSkillPoints(1);
                    RecordMoment("雾港、玻璃沙海与霜钟高原的现场反制均已用于真实战斗。远岸战术手记完成。",
                        "Used the foghorn, prism and stillbell in real encounters. Outer expedition tactics mastered.");
                    Notify("远岸三地战术全部掌握！技能点+1。",
                        "Outer-region tactics mastered! +1 talent point.");
                    return true;
                }
                if(FieldTacticMasteryCount==4&&!Has("field_tactics_mastered"))
                {
                    Flag("field_tactics_mastered");CreditSkillPoints(1);
                    RecordMoment("四区战场装置均已投入实战。获得四地战术记忆与一个技能点。",
                        "All four regional battlefield techniques mastered. Earned one talent point.");
                    Notify("四区战术全部掌握！技能点+1。",
                        "All four tactical devices mastered! +1 talent point.");
                    return true;
                }
            }
            Notify(TacticZh[room]+"启动，影响敌人"+targets.Count+
                "名，截断飞弹"+arrows.Count+"枚；24秒复位。",
                TacticEn[room]+" activated. "+targets.Count+" foes and "+
                arrows.Count+" arrows affected; 24s cooldown.");
            return true;
        }
    }
}
