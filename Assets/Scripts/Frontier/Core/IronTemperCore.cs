using System;
using System.Collections.Generic;

namespace Starfall
{
    // A multi-stage pressure puzzle inside the original Ironreach map.
    // All encounter state is transient; existing SaveData v2 flags own the outcome.
    public sealed partial class GameCore
    {
        public int TemperStage { get; private set; }
        public float TemperTime { get; private set; }
        public float TemperPressure { get; private set; }
        private float temperPulseTime;
        private int temperPulseIndex;
        private int temperConfirmedKills;
        private int TemperWaveFirst { get { return 89940 + TemperStage * 3; } }
        private int TemperWaveCount { get { return TemperStage == 3 ? 1 :
            TemperStage == 1 || TemperStage == 2 ? 2 : 0; } }
        private bool TemperWaveDefeated { get { return TemperWaveCount > 0 &&
            temperConfirmedKills == (1 << TemperWaveCount) - 1; } }
        private void OnTemperEnemyDefeated(Enemy enemy)
        {
            if (!TemperActive || save.room != 3 || !IsTemperEnemy(enemy)) return;
            int index = enemy.id - TemperWaveFirst;
            if (index >= 0 && index < TemperWaveCount)
                temperConfirmedKills |= 1 << index;
        }
        public bool TemperActive { get { return TemperStage!=0; } }
        public bool IsTemperEnemy(Enemy e)
        { return e!=null && e.id>=89940 && e.id<89950; }
        // Rescued workers return reusable timber to Noll's public workshop.
        // The genuine Iron Temper recipe and the escort are independent,
        // permanently stackable benefits, never cheaper than one timber.
        public int TemperUpgradeTimber
        { get { return Math.Max(1,(Has("iron_temper_complete")?2:3)-(MineConvoyComplete?1:0)); } }
        public int TemperUpgradeCrystal { get { return Has("iron_temper_complete") ? 1 : 2; } }
        public int TemperUpgradeParts { get { return Has("iron_temper_complete") ? 1 : 2; } }

        private Point TemperPoint(string kind)
        { return ObjectPoint(3,kind,new Point(-100,-100)); }
        private void ResetTemper()
        {
            enemies.RemoveAll(IsTemperEnemy);
            projectiles.RemoveAll(p=>p.temperOwned);
            hazards.RemoveAll(h=>h.temperOwned);
            effects.RemoveAll(e=>e.temperOwned);
            TemperStage=0;temperConfirmedKills=0;
            TemperTime=TemperPressure=0;
            temperPulseTime=0;temperPulseIndex=0;
        }
        private bool SpawnTemperWave(int stage)
        {
            Point[] points=stage==1?
                new[]{new Point(23.5f,16.5f),new Point(20.5f,19.5f)}:
                stage==2?
                new[]{new Point(15.5f,8.5f),new Point(12.5f,11.5f)}:
                new[]{new Point(27.5f,13.5f)};
            Point origin=TemperPoint(stage==1?"ironfoundry":stage==2?
                "ironvent1":"ironvent2");
            foreach(Point p in points)
            {
                if(!CanStand(p) || !ClearAttackPath(origin,p) ||
                    NavigationPath(p).Count==0)return false;
            }
            for(int i=0;i<points.Length;i++)
            {
                Enemy e=new Enemy(89940+stage*3+i,
                    stage==1?i:stage==2?(i==0?2:0):2,
                    points[i].x,points[i].y,
                    stage==3?210:stage==2?92:74);
                e.timer=.6f+.35f*i;
                enemies.Add(e);
            }
            temperConfirmedKills=0;
            return true;
        }
        private bool TalkTemper(WorldObject o)
        {
            if(o.kind=="ironfoundry")
            {
                if(save.room!=3)return true;
                if(!Has("boss_3"))
                {
                    Dialog(o.name,o.en,
                        "旧工房还受监工升降机的军用热压系统控制。先完成赤铁矿场主线，夺回星核，才有安全地改造工房的机会。",
                        "The overseer's military pressure circuit still owns this foundry. Defeat the original Ironreach guardian before attempting public repairs.",
                        C("稍后回来","Return later","close"));return true;
                }
                if(Has("iron_temper_complete"))
                {
                    Dialog(o.name,o.en,
                        "锻炉已交给矿工管理。诺尔收到公开回火工艺：装备强化现在只用"+TemperUpgradeTimber+"木、1晶、1零件。"+(MineConvoyComplete?"矿车回收的可用木材又额外省下1份。":"")+"任何旧存档里的强化等级都没有重置。",
                        "The miners run the forge now. Noll's upgraded tempering recipe costs "+TemperUpgradeTimber+" timber, 1 crystal and 1 part."+(MineConvoyComplete?" Reclaimed minecart timber saves an additional unit.":"")+" Existing upgrades remain intact.",
                        C("明白了","Understood","close"));return true;
                }
                if(!TemperActive)
                {
                    Dialog(o.name,o.en,
                        "矿工留下了一座仍在运转的过热锻炉。先击退锻炉卫兵，再赶往西北泄压阀，清掉二次增援后启动东侧泄压阀。热压每秒上升，超过95就会停机；炉壁会发出可躲避的热冲击。最后击败重装监工，返回锻炉完成交接。限时110秒，可免费重试。",
                        "The miners' overheated foundry is still operating. Defeat its first guards and open the northwest pressure valve; clear reinforcements, open the eastern valve, then defeat the armored overseer and return to claim the forge. Pressure rises over time and trips at 95, with dodgeable steam surges. You have 110 seconds and free retries.",
                        C("开启回火行动","Begin temper operation","iron_temper_start"),
                        C("暂不行动","Not now","close"));return true;
                }
                Dialog(o.name,o.en,
                    "剩余 "+TemperTime.ToString("0")+" 秒，压力 "+
                    TemperPressure.ToString("0")+"/95。"+
                    (TemperStage==3?"两处泄压阀已开启。击败最后的重装守卫，回到此处关闭旧军用指令。":
                    TemperStage==2?"西北阀已解锁。下一步去东侧泄压阀。":
                    "先击败第一组卫兵，去西北阀泄压。"),
                    TemperTime.ToString("0")+"s, pressure "+
                    TemperPressure.ToString("0")+"/95. "+
                    (TemperStage==3?"Both valves open. Defeat the overseer and return here to remove the military control order.":
                    TemperStage==2?"Northwest valve released. Proceed to the eastern valve.":
                    "Clear the first guards, then reach the northwest release valve."),
                    C(TemperStage==3 && TemperWaveDefeated && !enemies.Exists(IsTemperEnemy)?
                        "向矿工交付回火配方":"继续行动",
                        TemperStage==3 && TemperWaveDefeated && !enemies.Exists(IsTemperEnemy)?
                        "Deliver the public temper recipe":"Continue operation",
                        TemperStage==3 && TemperWaveDefeated && !enemies.Exists(IsTemperEnemy)?
                        "iron_temper_finish":"close"),
                    C("退出并安全停机","Abort and safely shut down","iron_temper_abort"));return true;
            }
            if(o.kind!="ironvent1" && o.kind!="ironvent2")return false;
            int valve=o.kind=="ironvent1"?1:2;
            if(save.room!=3)return true;
            if(!TemperActive)
            {
                Notify("先在旧锻炉启动回火行动。","Start the temper operation at the foundry first.");
                return true;
            }
            if(valve!=TemperStage)
            {
                Notify(valve<TemperStage?"这座阀门已经泄压。":"必须先修复上一段锻炉管线。",
                    valve<TemperStage?"Already vented.":"Release the preceding forge circuit first.");
                return true;
            }
            if(enemies.Exists(IsTemperEnemy) || !TemperWaveDefeated)
            {
                Notify("有锻炉守卫正在争夺阀门。先在实战中击退他们。",
                    "Foundry guards contest this valve. Defeat them in real combat first.");
                return true;
            }
            Dialog(o.name,o.en,
                valve==1?"西北支管被设为全天候高温，以防矿工休息。关闭旧锁就能让炉温下降，但会触发东段增援。":
                "东侧散热支管曾被军队封住以维持熔炼指标。释放它，将迫使最后的重装监工启动。",
                valve==1?"The northwest pipe was locked at full heat to prevent the miners from resting. Releasing it cools the core and summons eastern reinforcements.":
                "The eastern cooling pipe was sealed to sustain military output. Release it to force the final armored overseer online.",
                C("手动打开泄压阀","Manually release the valve",
                    "iron_temper_vent_"+valve),
                C("先检查环境","Inspect area","close"));
            return true;
        }
        private bool ChooseTemper(string action)
        {
            if(action=="iron_temper_start")
            {
                if(save.room!=3 || !Has("boss_3") || TemperActive ||
                    Has("iron_temper_complete") || practicing || Dying ||
                    FieldWorkPreventsRest || !SafeTravel || reliefMissionRoom!=0 || atlasEchoRoom!=0 ||
                    RootHarmonyRoom!=0 || roadTrialRoom!=0 ||
                    wayfarerEncounterRoom!=0 ||
                    (Position-TemperPoint("ironfoundry")).Length>2.2f ||
                    !SpawnTemperWave(1))
                    return true;
                TemperStage=1;TemperTime=110;TemperPressure=17;
                temperPulseTime=3.5f;temperPulseIndex=0;
                Flag("iron_temper_started");TrackQuest("iron_temper");
                revision++;sound="quest";
                Notify("回火已启动：110秒。清掉第一批守卫，去西北阀泄压。",
                    "Temper started: 110 seconds. Clear guards and release the northwest valve.");
                return true;
            }
            if(action=="iron_temper_abort")
            {
                if(!TemperActive || save.room!=3 ||
                    (Position-TemperPoint("ironfoundry")).Length>2.2f)return true;
                ResetTemper();revision++;
                Notify("工房已安全停机。可免费重新尝试。",
                    "Foundry safely shut down; retries are free.");
                return true;
            }
            if(action=="iron_temper_vent_1" || action=="iron_temper_vent_2")
            {
                int index=action=="iron_temper_vent_1"?1:2;
                if(save.room!=3 || TemperStage!=index || Dying ||
                    enemies.Exists(IsTemperEnemy) ||
                    (Position-TemperPoint("ironvent"+index)).Length>2.2f)
                    return true;
                if (!TemperWaveDefeated)
                {
                    ResetTemper(); revision++;
                    Notify("守卫未被确认击败。本轮回火已重置，需从锻炉重新开始。",
                        "A guard vanished without a confirmed defeat. Restart the forge defense.");
                    return true;
                }
                // The next wave must have legal, reachable feet before a
                // pressure checkpoint advances or the old wave is retired.
                if(!SpawnTemperWave(index+1))
                {
                    Notify("守卫通道受阻，请在另一侧调整站位后重试。",
                        "The guard approach is obstructed. Reposition before releasing the valve.");
                    return true;
                }
                Flag("iron_temper_vent_seen_"+index);
                TemperStage++;
                TemperPressure=Math.Max(8,TemperPressure-29);
                // Player-operated cooling is genuine tactical counterplay:
                // it cancels only this forge's live pressure tells, allows a
                // short breathing window and leaves unrelated hazards intact.
                hazards.RemoveAll(h=>h.temperOwned);
                effects.RemoveAll(e=>e.temperOwned);
                temperPulseTime=Math.Max(temperPulseTime,2.8f);
                effects.Add(new Effect("discover",Position,.65f));
                revision++;sound="guard";
                Notify(index==1?"西北支管泄压，守卫从东边增援！":
                    "东部散热阀已经接通。重装监工正在逼近锻炉！",
                    index==1?"Northwest vent released. Reinforcements arrive from the east!":
                    "East vent released. The armored overseer approaches the foundry!");
                return true;
            }
            if(action=="iron_temper_finish")
            {
                if(save.room!=3 || TemperStage!=3 ||
                    enemies.Exists(IsTemperEnemy) || Dying ||
                    Has("iron_temper_complete") ||
                    (Position-TemperPoint("ironfoundry")).Length>2.2f)return true;
                if (!TemperWaveDefeated)
                {
                    ResetTemper(); revision++;
                    Notify("重装监工未被确认击败。回火未完成，请重试。",
                        "The armored overseer was not defeated in combat. Retry the forge.");
                    return true;
                }
                ResetTemper();
                Flag("iron_temper_complete");
                CreditCoins(75);CreditShards(3);CreditSkillPoints(1);
                AddXp(85);revision++;sound="reward";
                RecordMoment("赤铁工房被矿工接管。诺尔获得公开回火配方，装备强化用料永久减少。",
                    "Ironreach foundry restored to miners. Noll's public temper recipe permanently reduces upgrade materials.");
                Notify("余烬回火完成！75硬币、3碎片、1天赋点、85经验；诺尔的装备强化永久省材料。",
                    "Ember Temper complete! +75 coins, +3 shards, +1 talent, +85 XP. Noll's upgrades permanently cost less.");
                return true;
            }
            return false;
        }
        private void TickTemper(float dt)
        {
            if(!TemperActive)return;
            if(save.room!=3 || Dying || practicing)
            {ResetTemper();revision++;return;}
            // Three fixed authored waves must remain intact until their
            // actual lethal Hit callbacks confirm each defeated guard.
            for (int index = 0; index < TemperWaveCount; index++)
            {
                if ((temperConfirmedKills & (1 << index)) != 0 ||
                    enemies.Exists(e => e.id == TemperWaveFirst + index)) continue;
                ResetTemper(); revision++;
                Notify("锻炉守卫异常离场，未确认击败。已安全停机，可重新尝试。",
                    "A foundry sentinel vanished without combat defeat. The forge shut down; retry freely.");
                return;
            }
            TemperTime-=dt;
            TemperPressure+=dt*(TemperStage==3?1.05f:1.3f);
            if(TemperTime<=0 || TemperPressure>=95)
            {
                ResetTemper();revision++;
                Notify("锻炉过压停机。守卫撤退，可重新启动回火。",
                    "The forge overheated and shut down. Guards withdrew; retry freely.");
                return;
            }
            temperPulseTime-=dt;
            if(temperPulseTime>0)return;
            temperPulseTime=3.8f-(TemperStage-1)*.38f;
            Point center=Position;
            if(!CanStand(center))return;
            // One visible delayed blast follows the hero's current footprint.
            // At least one cardinal step must be walkable before the tell is armed.
            Point[] cardinal={new Point(1.9f,0),new Point(-1.9f,0),
                new Point(0,1.9f),new Point(0,-1.9f)};
            bool escape=false;
            foreach(Point step in cardinal)
                if(CanStand(center+step) && ClearAttackPath(center,center+step))
                {escape=true;break;}
            if(!escape)return;
            float radius=TemperStage==3?1.18f:.92f;
            hazards.Add(new Hazard{position=center,radius=radius,
                timer=1.35f,damage=TemperStage==3?20:12+TemperStage*3,
                kind="iron",temperOwned=true});
            effects.Add(new Effect("warn",center,1.28f){
                radius=radius,variant=4,temperOwned=true});
            temperPulseIndex++;
        }
    }
}
