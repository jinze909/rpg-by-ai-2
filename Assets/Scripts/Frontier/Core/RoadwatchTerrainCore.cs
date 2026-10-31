using System;
using System.Collections.Generic;

namespace Starfall
{
    // The roadside fighting mechanic uses the original ridgemarker, the
    // two genuinely spawned ambushers and the shipped control/FX pipeline.
    // It never makes an offscreen actor, new map prop or SaveData field.
    public sealed partial class GameCore
    {
        public int RemoteRoadSignalEnergyCost(int room)
        {
            if(room<1||room>7)return 12;
            // A complete civilian foot survey now changes what the player
            // can DO during actual combat instead of being only map lore.
            return TrailCircuitComplete(room)?8:12;
        }

        private bool RoadSignalTargetVisible(Enemy foe,Point marker)
        {
            return foe!=null&&IsRemoteRoadThreat(foe)&&
                !foe.boss&&CanStand(foe.Position)&&
                (foe.Position-marker).Length<=8.6f&&
                ClearAttackPath(marker,foe.Position);
        }

        private bool RoadSignalArrowVisible(Projectile shot,Point marker)
        {
            return shot!=null&&!shot.friendly&&
                shot.hostileShooter!=null&&
                IsRemoteRoadThreat(shot.hostileShooter)&&
                (shot.position-marker).Length<=8.6f&&
                ClearAttackPath(marker,shot.position);
        }

        private bool RoadSignalSource(WorldObject marker)
        {
            return RemoteRoadEncounterActive&&marker!=null&&
                marker.kind=="ridgemarker"&&
                System.Object.ReferenceEquals(marker,remoteRoadSource)&&
                SourceBelongsToActiveMap(marker)&&
                IsObjectPresent(marker)&&
                marker.id=="ridge_route_"+save.room+"_"+remoteRoadDistrict;
        }

        public int RemoteRoadSignalThreatCount(WorldObject marker)
        {
            if(!RoadSignalSource(marker))return 0;
            Point origin=new Point(marker.x,marker.y);
            int count=0;
            foreach(Enemy foe in remoteRoadFoes)
                if(RoadSignalTargetVisible(foe,origin))count++;
            if(save.room==4||save.room==5)
                foreach(Projectile shot in projectiles)
                    if(RoadSignalArrowVisible(shot,origin))count++;
            return count;
        }

        // A deliberate choice from the live marker is REQUIRED. One signal
        // per battle, no auto-attack, no damage, no success receipt, and no
        // spending energy when the terrain is blocked or threats are gone.
        private bool ActivateRemoteRoadSignal(WorldObject marker)
        {
            if(mode!="world"||Dying||practicing||
                pendingDialogueSourceRoom!=save.room||
                !RoadSignalSource(marker)||
                !HasPhysicalInteractionReach(marker)||
                !CanStand(Position))return true;
            if(remoteRoadSignalUsed)
            {
                Notify("这场交战中原路标的信号已经用过。",
                    "This road signal was already used in this fight.");
                return true;
            }
            int room=save.room;
            int cost=RemoteRoadSignalEnergyCost(room);
            if(save.energy<cost)
            {
                Notify("现场反制需要 "+cost+" 耐力。",
                    "Using this terrain counter requires "+cost+" energy.");
                return true;
            }
            Point origin=new Point(marker.x,marker.y);
            Enemy target=null;
            float best=float.NegativeInfinity;
            // Different terrain favors different REAL fighting roles:
            // forests/highlands break melee, ruins/harbor disrupt bows,
            // mines/desert open armor, citadel counters ranged pressure.
            int preferred=room==1||room==7?0:
                room==2||room==4||room==5?1:2;
            foreach(Enemy foe in remoteRoadFoes)
            {
                if(!RoadSignalTargetVisible(foe,origin))continue;
                float score=(foe.kind==preferred?20f:0f)-
                    (foe.Position-origin).Length;
                if(score>best){best=score;target=foe;}
            }
            var blockedArrows=new List<Projectile>();
            if(room==4||room==5)
                foreach(Projectile shot in projectiles)
                    if(RoadSignalArrowVisible(shot,origin))
                        blockedArrows.Add(shot);
            if(target==null&&blockedArrows.Count==0)
            {
                Notify("路标附近没有视线可及的真实伏击者或飞弹；原信号保留。",
                    "No exposed ambusher or incoming shot. The signal stays available.");
                return true;
            }
            save.energy-=cost;
            remoteRoadSignalUsed=true;
            if(target!=null)
            {
                bool interrupted=target.state==1;
                if(room==1)
                {
                    ApplyEnemyControl(target,2.2f);
                    target.stagger=Math.Max(target.stagger,1.2f);
                }
                else if(room==2)
                {
                    Point away=(target.Position-origin).Normalized;
                    Point swept=target.Position+away*1.1f;
                    if(away.Length>.1f&&CanStand(swept)&&
                        ClearAttackPath(target.Position,swept))
                    {target.x=swept.x;target.y=swept.y;}
                    ApplyEnemyControl(target,1.1f);
                }
                else if(room==3||room==6)
                {
                    if(target.kind==2)
                        target.armorOpenTime=Math.Max(target.armorOpenTime,
                            room==3?4f:3.4f);
                    ApplyEnemyControl(target,room==3?1.5f:1.3f);
                }
                else if(room==4||room==5)
                {
                    ApplyEnemyControl(target,target.kind==1?2.1f:.9f);
                    target.stagger=Math.Max(target.stagger,.7f);
                }
                else
                {
                    ApplyEnemyControl(target,2.1f);
                    target.stagger=Math.Max(target.stagger,1f);
                    // Highland echo briefly disrupts the OTHER real
                    // attacker, not a phantom wave or nearby story enemy.
                    foreach(Enemy other in remoteRoadFoes)
                        if(other!=target&&RoadSignalTargetVisible(other,origin))
                        {ApplyEnemyControl(other,.75f);break;}
                }
                if(interrupted)
                    effects.Add(new Effect("number",
                        target.Position+new Point(0,.5f),.75f,
                        L("攻击中断！","ATTACK INTERRUPTED")));
                string[] zh={"","根缚","退潮冲击","矿汽破甲","外墙断弧",
                    "潮雾扰射","棱光暴露","霜钟震慑"};
                string[] en={"","ROOTSNARE","TIDE SWEEP","ARMOR EXPOSED",
                    "ARC BREAK","FOG DISRUPTION","PRISM EXPOSE","BELL ECHO"};
                effects.Add(new Effect("number",target.Position,.9f,
                    L(zh[room],en[room])));
            }
            foreach(Projectile shot in blockedArrows)
                projectiles.Remove(shot);
            effects.Add(new Effect("burst",origin,.7f)
                {radius=3f,variant=room});
            sound=room==2||room==5?"splash":
                room==3||room==6?"explosion":"alert";
            revision++;
            Notify("原路标的战术信号已发动，反制真正的伏击者。此战不可再次使用。",
                "Roadside terrain counter activated on genuine ambushers. One use this fight.");
            return true;
        }
    }
}
