using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static Projectile ThreateningShot(GameCore g,Point velocity)
        {
            Point approach=velocity.Normalized;
            return new Projectile {
                position=g.Position-approach*.72f,
                velocity=velocity,damage=20,life=2,
                friendly=false,visual="enemyArrow",source="shot"
            };
        }
        public static void SwordGuardReflectsRealHostileShotsWithResourceAndDirection(Campaign data)
        {
            var g=Fresh(data,0);
            g.enemies.Clear();g.projectiles.Clear();
            g.save.x=19.5f;g.save.y=14.5f;
            g.save.energy=100;
            var enemy=new Enemy(91801,1,16.5f,14.5f,300);
            enemy.stun=200;enemy.timer=200;g.enemies.Add(enemy);
            Advance(g,1.1f); // Exit spawn invulnerability; keep real Q.
            g.Skill(new Point(-1,0));
            Advance(g,.38f);
            Check(g.guardTime>2&&g.save.weapon==0&&
                g.save.profession==0,
                "The warrior's original Q actually establishes a timed sword guard");
            float oldHP=g.save.health;
            var arrow=ThreateningShot(g,new Point(6,0));
            g.projectiles.Add(arrow);
            for(int i=0;i<9&&!arrow.friendly;i++)g.Tick(.05f,new Point());
            Check(arrow.friendly&&arrow.velocity.x<0&&
                g.projectiles.Contains(arrow)&&
                g.save.health==oldHP&&g.deflectCooldown>0,
                "A genuinely sweeping frontal hostile shot reverses into a friendly projectile without hurting the hero");
            Check(arrow.age>0f&&
                Math.Abs(arrow.trailResetAt-arrow.age)<.00001f,
                "Real sword reflection must reset outbound visual trail at contact without resetting gameplay age");
            Check(g.effects.Exists(e=>e.kind=="number"&&
                e.text.Contains("弹反")),
                "Sword-guard contact announces its real successful timing");
            Check(g.save.energy<70,
                "A real sword deflection costs seven energy after the original 40-energy class skill");
            for(int i=0;i<35&&g.projectiles.Contains(arrow);i++)
                g.Tick(.05f,new Point());
            Check(enemy.hp<300,
                "The reversed projectile physically flies back and damages a real living enemy");
            Check(g.save.version==2,"A reflected arrow uses no new save fields");
        }
        public static void SwordGuardParriesRealFrontalLungesButNotSlamsOrRearAttacks(Campaign data)
        {
            foreach(bool front in new[]{true,false})
            {
                var g=Fresh(data,0);g.enemies.Clear();
                g.save.x=19.5f;g.save.y=14.5f;
                Advance(g,1.1f);
                g.save.energy=100;g.guardTime=2;g.aim=new Point(-1,0);
                float attackerX=front?18.25f:20.75f;
                var attacker=new Enemy(91891,0,attackerX,g.save.y,180);
                attacker.Face(new Point(front?1:-1,0));
                attacker.target=g.Position;
                attacker.stun=0;attacker.state=1;attacker.timer=.02f;
                attacker.windupDuration=.65f;
                g.enemies.Add(attacker);
                float hp=g.save.health;
                g.Tick(.05f,new Point());
                if(front)
                {
                    Check(g.save.health==hp&&g.riposteTime>1.3f&&
                        g.deflectCooldown>.25f,
                        "A physically committed frontal melee lunge is parried and earns an actual riposte opening");
                    Check(g.save.energy<=92&&g.save.energy>=90,
                        "A real sword counter pays nine energy exactly once");
                    g.Attack(new Point(-1,0));
                    Check(g.riposteTime==0,
                        "A genuine post-parry attack spends the existing empowered riposte window");
                    Advance(g,.45f);
                    Check(attacker.hp<180,
                        "The parry opening boosts a real sword contact, not a cosmetic reward");
                }
                else
                    Check(g.save.health<hp&&g.riposteTime<=0&&
                        g.deflectCooldown==0,
                        "A physically attacking enemy behind the sword guard still inflicts normal mitigated damage");
            }
        }

        public static void MultipleHostileShotsRespectActualGuardCooldown(Campaign data)
        {
            var g=Fresh(data,0);g.enemies.Clear();
            g.save.x=19.5f;g.save.y=14.5f;
            Advance(g,1.1f);
            g.save.energy=100;g.guardTime=2;g.aim=new Point(-1,0);
            var first=ThreateningShot(g,new Point(7,0));
            var second=ThreateningShot(g,new Point(7,0));
            g.projectiles.Add(first);g.projectiles.Add(second);
            for(int i=0;i<8&&!first.friendly&&g.projectiles.Contains(first);i++)
                g.Tick(.05f,new Point());
            Check(first.friendly&&!second.friendly&&g.deflectCooldown>0,
                "Only the first live projectile can be reflected inside a cooldown window");
            Check(g.save.energy>=90&&g.save.energy<=96,
                "A simultaneous arrow volley costs exactly one seven-energy deflection");
            g.PrepareSave();var reloaded=new GameCore(data,CloneSave(g.save));
            Check(!reloaded.SwordGuardReady&&reloaded.deflectCooldown==0,
                "Reload retires transient guard timing without altering old persistent save fields");
        }

        public static void SwordGuardDoesNotReflectSideShotsOrBypassClassAndCooldown(Campaign data)
        {
            foreach(int profession in new[]{0,1,2})
            {
                var g=Fresh(data,profession);g.enemies.Clear();
                g.save.x=19.5f;g.save.y=14.5f;
                Advance(g,1.1f);
                g.save.energy=100;
                g.guardTime=2f;g.aim=new Point(-1,0);
                var arrow=ThreateningShot(g,new Point(6,0));
                g.projectiles.Add(arrow);
                for(int i=0;i<9&&g.projectiles.Contains(arrow);i++)g.Tick(.05f,new Point());
                if(profession==0)
                    Check(!g.projectiles.Contains(arrow)||arrow.friendly,
                        "The eligible warrior reflects the genuine frontal shot");
                else
                    Check(!arrow.friendly,
                        "Companion shield buffs do not grant warrior reflection to the other two hero professions");
            }
            var side=Fresh(data);side.enemies.Clear();
            side.save.x=19.5f;side.save.y=14.5f;
            Advance(side,1.1f);side.guardTime=2;side.aim=new Point(1,0);
            var rear=ThreateningShot(side,new Point(6,0));
            side.projectiles.Add(rear);
            float hp=side.save.health;
            for(int i=0;i<9&&side.projectiles.Contains(rear);i++)side.Tick(.05f,new Point());
            Check(!rear.friendly&&side.save.health<hp,
                "Sword guard fails to reflect a real rear hit when facing away");
            var g2=Fresh(data);g2.enemies.Clear();
            g2.save.x=19.5f;g2.save.y=14.5f;
            Advance(g2,1.1f);g2.guardTime=2;g2.aim=new Point(-1,0);
            // The world recovers energy during flight; zero ensures the
            // real contact still occurs below the seven-energy guard cost.
            g2.save.energy=0;
            var insufficient=ThreateningShot(g2,new Point(6,0));
            g2.projectiles.Add(insufficient);
            for(int i=0;i<9&&g2.projectiles.Contains(insufficient);i++)g2.Tick(.05f,new Point());
            Check(!insufficient.friendly,
                "A guard at insufficient energy cannot mint free counter shots");
            g2.PrepareSave();var old=new GameCore(data,CloneSave(g2.save));
            Check(old.save.version==2&&old.deflectCooldown==0,
                "Old saves never persist temporary guard/deflection timing");
        }
        public static void ReflectedProjectileRetainsWarriorProfileAfterEquipmentSwap(Campaign data)
        {
            var g = Fresh(data);
            g.enemies.Clear(); g.projectiles.Clear();
            g.save.x = 19.5f; g.save.y = 14.5f;
            Advance(g, 1.1f);
            g.guardTime = 2; g.aim = new Point(-1, 0);
            g.save.energy = 100;
            var target = new Enemy(92019, 0, 16.5f, 14.5f, 1);
            target.stun = 99; target.timer = 99;
            g.enemies.Add(target);
            Projectile arrow = ThreateningShot(g, new Point(6, 0));
            g.projectiles.Add(arrow);
            for (int i = 0; i < 10 && !arrow.friendly; i++)
                g.Tick(.05f, new Point());
            Check(arrow.friendly && arrow.impact != null &&
                arrow.impact.playerOwned && arrow.impact.weapon == 0 &&
                arrow.impact.masteryWeapon == 0,
                "A true contact-time sword guard binds warrior impact and mastery to its reflected projectile");

            // The incoming arrow is already in flight. A later equipment
            // change cannot rewrite the outbound shot's owner or stagger.
            int swordMastery = g.save.mastery[0];
            int bowMastery = g.save.mastery[1];
            g.save.flags.Add("weapon_1");
            g.Equip(1);
            Check(g.save.weapon == 1 && arrow.impact.masteryWeapon == 0,
                "Changing to an unlocked bow cannot retarget old sword reflection");
            for (int i = 0; i < 50 && g.enemies.Contains(target); i++)
                g.Tick(.05f, new Point());
            Check(!g.enemies.Contains(target) &&
                g.save.mastery[0] == swordMastery + 2 &&
                g.save.mastery[1] == bowMastery,
                "Reflected arrow kill credits the warrior's original sword mastery, never the weapon equipped during flight");
        }

        public static void VeteranSwordGuardUsesEarnedEnergyDiscountOnRealProjectileAndLunge(Campaign data)
        {
            float[] afterShot=new float[2],afterMelee=new float[2];
            for(int i=0;i<2;i++)
            {
                bool veteran=i==1;
                var g=Fresh(data,0);g.enemies.Clear();g.projectiles.Clear();
                g.save.x=19.5f;g.save.y=14.5f;
                Advance(g,1.1f);g.enemies.Clear();
                if(veteran)g.save.flags.Add("swordlesson_veteran");
                g.save.energy=60;g.guardTime=2;g.aim=new Point(-1,0);
                g.invulnerable=0;
                Check(g.SwordGuardArrowEnergy==(veteran?5:7)&&
                    g.SwordGuardMeleeEnergy==(veteran?7:9),
                    "Only the legitimately completed advanced course adjusts guard stamina requirements");
                var arrow=ThreateningShot(g,new Point(6,0));
                g.projectiles.Add(arrow);
                for(int step=0;step<10&&!arrow.friendly;step++)
                    g.Tick(.05f,new Point());
                Check(arrow.friendly&&arrow.impact!=null&&
                    arrow.impact.playerOwned&&arrow.impact.weapon==0,
                    "Both standard and veteran blocks reflect a real projectile with the same sword owner");
                afterShot[i]=g.save.energy;
                g.PrepareSave();var restored=new GameCore(data,CloneSave(g.save));
                Check(restored.save.version==2&&
                    restored.SwordGuardArrowEnergy==(veteran?5:7),
                    "Legacy v2 retains the earned technique using only existing quest flags");

                var m=Fresh(data,0);m.enemies.Clear();m.save.x=19.5f;m.save.y=14.5f;
                Advance(m,1.1f);m.enemies.Clear();
                if(veteran)m.save.flags.Add("swordlesson_veteran");
                m.save.energy=60;m.guardTime=2;m.aim=new Point(-1,0);
                m.invulnerable=0;
                var attacker=new Enemy(91891+i,0,18.25f,14.5f,180);
                attacker.Face(new Point(1,0));attacker.target=m.Position;
                attacker.state=1;attacker.timer=.02f;attacker.windupDuration=.65f;
                m.enemies.Add(attacker);
                float health=m.save.health;
                m.Tick(.05f,new Point());
                Check(m.save.health==health&&m.riposteTime>1.3f&&
                    m.deflectCooldown>.25f,
                    "Both techniques still counter a true frontal lunge and open the original riposte");
                afterMelee[i]=m.save.energy;
            }
            Check(Math.Abs((afterShot[1]-afterShot[0])-2)<.11f&&
                Math.Abs((afterMelee[1]-afterMelee[0])-2)<.11f,
                "The earned technique saves exactly two energy per actual arrow or melee contact");
            var mage=Fresh(data,2);mage.save.flags.Add("swordlesson_veteran");
            mage.guardTime=2;mage.save.energy=100;
            Check(!mage.SwordGuardReady,
                "No advanced training flag can grant the warrior-exclusive sword guard to another vocation");
        }

    }
}
