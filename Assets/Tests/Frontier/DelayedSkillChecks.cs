using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void SkillsRetainPreparedCombatValues(Campaign data)
        {
            for(int profession=0;profession<3;profession++)for(int alternate=0;alternate<2;alternate++)
            {
                var g=Quiet(data,profession);g.save.flags.Add("technique_"+profession);g.save.technique=alternate;g.save.seed=1;
                var target=new Enemy(2100,0,15,14,999){stun=99};g.enemies.Add(target);g.Skill(new Point(1,0));var action=g.playerAction;
                Check(action!=null,"Every existing Q starts its prepared action");
                g.mode="talents";g.save.skillPoints=1;g.Learn(0);g.Close();
                for(int frame=0;frame<30&&!action.released;frame++){target.stun=99;g.Tick(.02f,new Point());}
                Check(action.released,"The prepared Q reaches its contact frame");
                if(profession==1)
                {
                    float expected=action.damage*(alternate==0?1.25f:3.4f);
                    Check(g.projectiles.Count==(alternate==0?5:1),"The original ranger volley size is retained");
                    foreach(Projectile shot in g.projectiles)Check(Math.Abs(shot.damage-expected)<.001f,"Q projectiles use prepared damage after a menu talent change");
                }
                else
                {
                    float multiplier=profession==0?(alternate==0?1.9f:1.65f):(alternate==0?2.3f:.4f);
                    Check(Math.Abs(999-target.hp-action.damage*multiplier)<.002f,"Q contact uses prepared damage for profession "+profession+" alternate "+alternate);
                }
            }
            foreach(int profession in new[]{0,2})
            {
                var g=Quiet(data,profession);g.save.seed=1;g.save.xp=39;g.save.mastery[profession]=18;
                g.enemies.Add(new Enemy(2110,0,13.5f,14,1){stun=99});var survivor=new Enemy(2111,0,14,14,999){stun=99};g.enemies.Add(survivor);
                g.Skill(new Point(1,0));float prepared=g.playerAction.damage;
                for(int frame=0;frame<30&&survivor.hp==999;frame++)g.Tick(.02f,new Point());
                Check(g.save.level==2&&g.save.mastery[profession]==20,"The first contact awards level and mastery progress");
                Check(Math.Abs(999-survivor.hp-prepared*(profession==0?1.9f:2.3f))<.002f,"An earlier kill cannot increase the damage of later targets in the same Q release");
            }
        }
        public static void FrostRetainsCasterProfileAndSource(Campaign data)
        {
            var g=Quiet(data,2);g.save.flags.Add("core_1");g.save.flags.Add("technique_2");g.save.technique=1;g.save.seed=1;
            Point center=g.Position;var target=new Enemy(2101,0,14,14,999){stun=99};g.enemies.Add(target);g.Skill(new Point(1,0));float prepared=g.playerAction.damage;
            for(int i=0;i<30&&target.hp==999;i++){target.stun=99;g.Tick(.02f,new Point());}
            Check(target.hp<999,"The real frost action emits its initial pulse");float firstHp=target.hp;
            g.Equip(0);g.SetStyle(2);g.save.mastery[0]=80;g.save.skillPoints=1;g.Learn(0);g.save.flags.Add("relic_5");g.EquipRelic(0,5);target.stagger=0;
            for(int i=0;i<80&&target.hp==firstHp;i++){target.stun=99;g.Tick(.02f,new Point(0,1));}
            float second=firstHp-target.hp;
            Check(Math.Abs(second-prepared*.4f)<.002f&&Math.Abs(target.stagger-second*.45f)<.002f,"A persistent frost pulse cannot borrow later talents, critical chance, sword mastery or stagger style");
            Point expected=(target.Position-center).Normalized;
            Check((target.recoil-expected).Length<.002f&&g.Position.y>center.y+.1f,"Frost recoil originates at the retained field, even after the caster walks away");
            target.hp=1;for(int i=0;i<80&&g.enemies.Contains(target);i++){target.stun=99;g.Tick(.02f,new Point());}
            Check(!g.enemies.Contains(target)&&g.save.mastery[2]==2&&g.save.mastery[0]==80,"A frost kill credits the retained caster weapon rather than equipment changed after casting");
        }
        public static void FrostPulseClockIsFrameRateIndependent(Campaign data)
        {
            foreach(float step in new[]{1f/60,.02f,.04f,.05f})
            {
                var g=Quiet(data,2);g.save.flags.Add("technique_2");g.save.technique=1;
                var target=new Enemy(2102,0,14,14,999){stun=99};g.enemies.Add(target);g.Skill(new Point(1,0));int pulses=0;
                for(int frame=0;frame<Math.Ceiling(5/step);frame++)
                {target.stun=99;float hp=target.hp;g.Tick(step,new Point());if(target.hp<hp)pulses++;}
                Check(pulses==5,"A four-second frost field has exactly five scheduled pulses at timestep "+step+", observed "+pulses);
                g.Skill(new Point(1,0));Advance(g,.4f);g.Enter(0);Advance(g,1);
                Check(!g.effects.Exists(e=>e.kind=="frost"),"Travel cancels the old field and cannot create remote frost effects in the new room");
            }
        }
        private static void LethalShot(GameCore g)
        {
            g.save.health=1;g.invulnerable=0;g.save.reducedMotion=true;
            g.projectiles.Add(new Projectile{position=g.Position-new Point(.2f,0),velocity=new Point(10,0),damage=999,source="shot"});g.Tick(.02f,new Point());
            Check(g.Dying&&g.Has("recovery_pending"),"The actual lethal contact starts recovery");
        }
        public static void CompanionDamageDoesNotCompletePlayerLessons(Campaign data)
        {
            foreach(int companion in new[]{1,2})
            {
                var g=Quiet(data);g.save.flags.Add("companion_"+companion);g.SelectCompanion(companion);g.save.tutorialBits=7;
                var target=new Enemy(2103,0,14,14,999){stun=99};g.enemies.Add(target);
                if(companion==2)g.CommandCompanion();
                for(int frame=0;frame<150&&target.hp==999;frame++){target.stun=99;g.Tick(.02f,new Point());}
                Check(target.hp<999,"An actual companion action lands without a player attack");
                Check((g.save.tutorialBits&((1<<3)|(1<<5)))==0,"Companion damage cannot complete the player's attack or Q practice for companion "+companion);
            }
            var player=Quiet(data);player.save.tutorialBits=7;player.enemies.Add(new Enemy(2104,0,13,14,999){stun=99});player.Attack(new Point(1,0));Advance(player,.3f);
            Check((player.save.tutorialBits&(1<<3))!=0,"The player's own actual attack still completes its lesson");
        }
        public static void RecoveryGuardsExpeditionTransitions(Campaign data)
        {
            var g=Unlocked(data);g.Enter(0);LethalShot(g);int room=g.save.room;g.mode="camp";g.StartAbyss(0);
            Check(g.Dying&&g.save.room==room&&g.save.abyssFloor==0&&g.save.health==0,"Expedition entry cannot cancel a fallen player's recovery");
            g=Unlocked(data);g.StartAbyss(0);g.enemies.Clear();g.save.flags.Add("abyss_clear_1");g.save.runCoins=55;g.save.runShards=9;g.save.runXp=40;LethalShot(g);
            int coins=g.save.coins,shards=g.save.shards;g.mode="dialog";g.TakeBoon(0);
            Check(g.Dying&&g.save.abyssFloor==1&&g.save.boons[0]==0&&g.save.coins==coins&&g.save.shards==shards&&g.save.runCoins==55,"A fallen player cannot bank loot, gain a boon or descend before recovery");
            TalkNear(g, "abyssGate");int bank=-1;
            for(int choice=0;choice<g.conversation.choices.Length;choice++)if(g.conversation.choices[choice].action=="abyssBank")bank=choice;
            Check(bank>=0,"The real root-gate dialogue provides extraction");g.Choose(bank);
            Check(g.Dying&&g.save.abyssFloor==1&&g.save.coins==coins&&g.save.shards==shards&&g.save.runCoins==55&&g.mode=="world","A stale world dialogue cannot bank or clear the run during recovery");
            g.Close();for(int frame=0;frame<25;frame++)g.Tick(.05f,new Point());
            Check(!g.Dying&&g.save.room==0&&g.save.health==g.save.maxHealth&&g.save.abyssFloor==0&&g.save.runCoins==0&&g.save.coins==coins,"Recovery discards only the floor's unbanked loot and completes normally");
        }
    }
}
