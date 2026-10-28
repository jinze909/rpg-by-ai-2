using System;
namespace Starfall.Tests
{
    // Actual C# GameCore and published Campaign.json; never simulate flags
    // in place of the timed lighthouse world interaction.
    public static partial class CoreChecks
    {
        private static GameCore MistlightReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.level=9;
            // Existing expansion-region route unlock needs the first core
            // AND Lune's actual introduction flag; stage alone is not enough.
            g.save.flags.Add("exp_intro");
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("boss_5");
            g.Enter(5);
            Check(g.save.room==5,"Mistport chapter's real main map is accessible");
            g.enemies.Clear();g.projectiles.Clear();g.invulnerable=140f;
            g.save.energy=100f;return g;
        }
        private static WorldObject MistlightSite(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)if(o.id==id)return o;
            throw new Exception("Missing actual Mistport last-light object "+id);
        }
        private static void MistlightAt(GameCore g,WorldObject site)
        {
            Point foot=new Point(site.x,site.y);
            Check(g.CanStand(foot),"Physical Mistport lamp station is original walkable terrain: "+site.id);
            g.save.x=foot.x;g.save.y=foot.y;
        }
        private static bool MistlightChoose(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(i<0)return false;
            g.Choose(i);return true;
        }
        private static GameCore MistlightBegin(Campaign data)
        {
            var g=MistlightReady(data);
            var notice=MistlightSite(g,"mistlight_notice");
            MistlightAt(g,notice);g.Talk(notice);
            Check(g.mode=="dialog"&&g.conversation!=null&&
                g.conversation.textEn.Contains("There are people aboard"),
                "New original last-light dialogue contains concrete keeper testimony");
            Check(MistlightChoose(g,"mistlight_start")&&g.MistlightStage==1&&
                g.MistlightTime==110f&&g.MistlightIntegrity==76f&&
                Math.Abs(g.MistlightNextGust-9f)<.01f,
                "Live rescue clock, damageable signal and first real objective actually start");
            g.Enter(0);
            Check(g.save.room==5&&g.MistlightActive,
                "Active real storm rescue cannot silently fast travel and erase its stage");
            g.ReturnToTown();
            Check(g.save.room==5&&g.MistlightActive,
                "Fast return to Emberfall cannot bypass active beacon storm or cancel paid progress");
            Check(g.save.trackedQuest=="mistport_lastlight"&&
                g.GetQuest("mistport_lastlight").room==5&&
                (g.GetQuest("mistport_lastlight").target-new Point(60.5f,37.5f)).Length<.01f,
                "Journal really routes to the physical northern beacon");
            return g;
        }
        public static void MistportLastLightTimedStormAndPhysicalWindWindow(Campaign data)
        {
            var g=MistlightBegin(data);
            var lamp=MistlightSite(g,"mistlight_lamp");
            var mirror=MistlightSite(g,"mistlight_mirror");
            var pier=MistlightSite(g,"mistlight_pier");
            MistlightAt(g,mirror);g.Talk(mirror);
            Check(MistlightChoose(g,"mistlight_align")&&g.MistlightStage==1,
                "Cannot skip the original unlit beacon by touching the sea mirror");
            MistlightAt(g,lamp);g.save.energy=11f;g.Talk(lamp);
            Check(MistlightChoose(g,"mistlight_ignite")&&g.MistlightStage==1,
                "Ignition uses actual stamina and rejects an unaffordable action");
            g.save.energy=100f;g.Talk(lamp);
            Check(MistlightChoose(g,"mistlight_ignite")&&g.MistlightStage==2&&
                g.save.energy==88f,
                "Physical lamp spends exactly 12 stamina and enables timed sea mirror stage");
            // A stale preloaded menu cannot operate another real world foot.
            MistlightAt(g,mirror);g.Talk(mirror);
            int choice=Array.FindIndex(g.conversation.choices,c=>c.action=="mistlight_align");
            Check(choice>=0,"Original mirror menu offers an actual alignment option");
            g.save.x=mirror.x-8f;g.Choose(choice);
            Check(g.MistlightStage==2,
                "Leaving the real mirror rejects a stale and distant menu callback");
            MistlightAt(g,mirror);
            for(int i=0;i<154;i++)g.Tick(.05f,new Point());
            Check(g.MistlightNextGust<2f&&g.MistlightNextGust>0f,
                "Wind warning truly counts down in live 50ms world ticks");
            g.Talk(mirror);
            Check(MistlightChoose(g,"mistlight_align")&&g.MistlightStage==2,
                "Gale timing blocks a real mirror rotation during the danger window");
            float before=g.MistlightIntegrity;
            for(int i=0;i<28;i++)g.Tick(.05f,new Point());
            Check(g.MistlightIntegrity==before-22f&&
                g.MistlightNextGust>8f&&g.MistlightStage==2,
                "A real gust damages the installed beacon by exactly 22 and restores a new alignment window");
            g.Talk(mirror);
            Check(MistlightChoose(g,"mistlight_align")&&g.MistlightStage==3&&
                (g.GetQuest("mistport_lastlight").target-new Point(pier.x,pier.y)).Length<.01f,
                "After weather clears, the physical mirror sends the genuine quest to the south pier");
            MistlightAt(g,pier);g.Talk(pier);
            Check(MistlightChoose(g,"mistlight_public")&&g.MistlightComplete&&
                g.MistlightPublic&&!g.MistlightPrivate&&
                !g.MistlightActive&&g.GetQuest("mistport_lastlight").complete,
                "Player can publicly rescue all boats only after actual three-stage weather navigation");
        }
        public static void MistportLastLightPartBracingSavesRealLampAndPersistsExclusiveOutcomes(Campaign data)
        {
            foreach(bool publicSignal in new[]{true,false})
            {
                var g=MistlightBegin(data);
                var lamp=MistlightSite(g,"mistlight_lamp");
                var mirror=MistlightSite(g,"mistlight_mirror");
                var pier=MistlightSite(g,"mistlight_pier");
                MistlightAt(g,lamp);g.Talk(lamp);
                Check(MistlightChoose(g,"mistlight_ignite"),"Physically light beacon before bracing");
                Check(g.MistlightStage==2,"Bracing has a live lamp stage");
                g.save.inventory[4]=1;g.Talk(lamp);
                Check(MistlightChoose(g,"mistlight_brace")&&
                    g.MistlightRepairUsed&&g.save.inventory[4]==0&&
                    g.MistlightIntegrity==100f&&g.MistlightShield==12f,
                    "One original machine part increases actual lamp integrity and grants 12s storm shield");
                g.Talk(lamp);
                Check(!MistlightChoose(g,"mistlight_brace")&&g.save.inventory[4]==0,
                    "No renewable brace option, free resource duplication or infinite defense");
                float starting=g.MistlightIntegrity;
                MistlightAt(g,mirror);
                for(int i=0;i<181;i++)g.Tick(.05f,new Point());
                Check(g.MistlightIntegrity==starting&&
                    g.MistlightShield>2f&&g.MistlightNextGust>8f,
                    "Protected genuine nine-second storm consumes NO integrity during its actual hit");
                g.Talk(mirror);
                Check(MistlightChoose(g,"mistlight_align")&&g.MistlightStage==3,
                    "Real aligned sea mirror opens physical pier decision");
                MistlightAt(g,pier);g.Talk(pier);
                int beforeCoins=g.save.coins,beforeShards=g.save.shards;
                Check(MistlightChoose(g,publicSignal?
                    "mistlight_public":"mistlight_private")&&
                    g.MistlightComplete&&g.MistlightPublic==publicSignal&&
                    g.MistlightPrivate!=publicSignal&&g.save.coins==beforeCoins+60&&
                    g.save.shards==beforeShards+2,
                    "Real pier pays once and selects exactly one lasting public/private rescue policy");
                g.Talk(pier);
                Check(!MistlightChoose(g,"mistlight_public")&&
                    g.save.coins==beforeCoins+60,
                    "Completed harbor cannot farm a second public-light reward");
                if(!publicSignal)
                {
                    float speed=g.Speed;
                    var controlSave=CloneSave(g.save);
                    controlSave.flags.Remove("mistlight_private");
                    var control=new GameCore(data,controlSave);
                    Check(Math.Abs(speed-control.Speed-.25f)<.01f,
                        "Private rescue route grants exactly +0.25 speed to original Mistport movement");
                }
                SilentLedgerVisitRealInnFromEmberfall(g);
                ReadInnNews(g,Object(g,"innGuest"));
                Check(g.conversation!=null&&
                    g.conversation.textEn.Contains(publicSignal?
                        "the harbor is welcoming every returning vessel":
                        "the hidden way home")&&
                    !g.conversation.textEn.Contains(publicSignal?
                        "the hidden way home":"the harbor is welcoming every returning vessel"),
                    "Real Hearth Inn guest tells only the player-earned public or secret maritime aftermath");
                g.Close();
                g.PrepareSave();
                var loaded=new GameCore(data,CloneSave(g.save));
                Check(loaded.save.version==2&&loaded.MistlightComplete&&
                    loaded.MistlightPublic==publicSignal&&loaded.MistlightPrivate!=publicSignal&&
                    loaded.GetQuest("mistport_lastlight").complete,
                    "Branch-specific outcome is the original v2 flag, with no added save schema");
            }
        }
        public static void MistportLastLightStormCanDestroyBeaconAndFreeRetryRemainsReal(Campaign data)
        {
            var g=MistlightBegin(data);
            var notice=MistlightSite(g,"mistlight_notice");
            g.save.x=notice.x;g.save.y=notice.y;
            // 4 real storms x22 must destroy the initial 76-point lamp.
            for(int i=0;i<735;i++)g.Tick(.05f,new Point());
            Check(!g.MistlightActive&&!g.MistlightComplete&&
                g.MistlightStage==0&&g.GetQuest("mistport_lastlight").target.x==54.5f,
                "Four physical 9s storm pulses destroy an unbraced lamp, fail honestly, and reoffer original notice");
            g.Talk(notice);
            Check(MistlightChoose(g,"mistlight_start")&&g.MistlightStage==1&&
                g.MistlightIntegrity==76f&&g.MistlightTime==110f,
                "After actual failure, retry restores the complete playable mission without a new save");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(!loaded.MistlightActive&&loaded.MistlightStage==0&&
                !loaded.MistlightComplete&&loaded.save.version==2,
                "An unfinished storm mission is transient on reload and cannot fabricate an earned victory");
        }
    }
}
