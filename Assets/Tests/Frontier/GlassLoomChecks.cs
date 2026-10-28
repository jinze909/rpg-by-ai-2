using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static WorldObject LensSite(GameCore g,string id)
        {
            foreach(WorldObject o in g.Map.objects)
                if(o.id==id)return o;
            throw new Exception("Missing new physically authored optical station "+id);
        }
        private static bool LensChoice(GameCore g,string action)
        {
            if(g.mode!="dialog"||g.conversation==null)return false;
            int n=Array.FindIndex(g.conversation.choices,c=>c.action==action);
            if(n<0)return false;
            g.Choose(n);return true;
        }
        private static GameCore GlasswellReady(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;
            g.save.flags.Add("harbor_report");
            g.save.flags.Add("desert_report");
            g.save.flags.Add("boss_6");
            g.Enter(6);g.enemies.Clear();g.projectiles.Clear();
            g.invulnerable=100;g.save.totalTime=30f;
            g.save.energy=100;
            return g;
        }
        private static void GlasswellTuneAndFocus(GameCore g)
        {
            var well=LensSite(g,"glassloom_source");
            g.save.x=well.x;g.save.y=well.y;
            Check(g.CanStand(g.Position)&&g.GlassLoomPathClear&&
                !g.Night,"New sundial begins on actual clear, walkable desert plateau in real daylight");
            g.Talk(well);
            Check(LensChoice(g,"glassloom_start")&&g.GlassLoomStage==1&&
                g.GlassLoomTime>299f,
                "New physical source activates genuine timer and stage one");
            int[] turns={2,0,1};
            for(int i=0;i<3;i++)
            {
                WorldObject lens=LensSite(g,"glassloom_mirror_"+(i+1));
                g.save.x=lens.x;g.save.y=lens.y;
                Check(g.CanStand(g.Position),
                    "Each physically authored lens has walkable tile and a real original world object");
                for(int k=0;k<turns[i];k++)
                {
                    g.Talk(lens);
                    Check(LensChoice(g,"glassloom_turn"),
                        "Actual close interaction rotates a physical lens");
                }
                Check(g.GlassLoomOrientation(i)==turns[i]&&
                    g.GlassLoomAligned(i),
                    "Each independent mirror reaches its exact original color/orientation code");
            }
            Check(g.GlassLoomOpticsAligned,
                "The three real lens states are collectively aligned");
            g.save.x=well.x;g.save.y=well.y;
            g.Talk(well);
            Check(LensChoice(g,"glassloom_focus")&&g.GlassLoomStage==2,
                "Only a daylight, source-bound, unobstructed optical chain focuses a real signal");
            Check((g.GetQuest("glass_loom").target-
                new Point(76.5f,50.5f)).Length<.01f,
                "The original quest journal points to an actual newly placed receiver");
        }
        public static void GlasswellPhysicalFiveDevicesDayNightSequenceAndBothExclusiveRewards(Campaign data)
        {
            foreach(bool publicRoad in new[]{true,false})
            {
                GameCore g=GlasswellReady(data);
                float baseDayDamage=g.Damage;
                g.save.totalTime=160f;float baseNightSpeed=g.Speed;
                g.save.totalTime=30f;
                GlasswellTuneAndFocus(g);
                WorldObject receiver=LensSite(g,"glassloom_receiver");
                g.save.x=receiver.x;g.save.y=receiver.y;
                Check(g.CanStand(g.Position),"The actual receiver has walkable local ground");
                g.Talk(receiver);
                Check(LensChoice(g,"glassloom_capture")&&
                    g.GlassLoomStage==2,
                    "Real physical receiver cannot synthesize night during daylight");
                g.save.totalTime=160f;
                Check(g.Night,"Actual world night occurs after 150 seconds of daily cycle");
                g.Talk(receiver);
                Check(LensChoice(g,"glassloom_capture")&&g.GlassLoomStage==3,
                    "Nighttime close-range receiver captures the real prior daytime optical charge");
                WorldObject well=LensSite(g,"glassloom_source");
                g.save.x=well.x;g.save.y=well.y;g.Talk(well);
                int money=g.save.coins;
                Check(LensChoice(g,publicRoad?"glassloom_public":"glassloom_private"),
                    "Physically returned well gives a genuinely exclusive civilian route decision");
                Check(g.GlassLoomComplete&&!g.GlassLoomActive&&
                    g.GlassLoomPublic==publicRoad&&g.GlassLoomPrivate!=publicRoad&&
                    g.save.coins==money+70&&g.GetQuest("glass_loom").complete,
                    "First-clear transaction grants precisely one of two permanent earned outcomes");
                g.save.totalTime=30f;
                Check(Math.Abs(g.Damage-(baseDayDamage+(publicRoad?3f:0f)))<.001f,
                    "Public solar routing modifies the ACTUAL daytime desert damage formula");
                g.save.totalTime=160f;
                Check(Math.Abs(g.Speed-(baseNightSpeed+(publicRoad?0f:.5f)))<.001f,
                    "Private moon routing modifies the ACTUAL nighttime desert locomotion");
                g.Talk(well);
                Check(!LensChoice(g,"glassloom_start")&&
                    !LensChoice(g,"glassloom_public")&&
                    !LensChoice(g,"glassloom_private"),
                    "A completed sundial has no repeatable reward or other ending");
                g.Close();g.PrepareSave();
                var reloaded=new GameCore(data,CloneSave(g.save));
                Check(reloaded.save.version==2&&reloaded.GlassLoomComplete&&
                    reloaded.GlassLoomStage==0&&
                    reloaded.GlassLoomPublic==publicRoad&&
                    reloaded.GlassLoomPrivate!=publicRoad,
                    "Old SaveData v2 keeps exactly the chosen public/private light route with no transient active mirrors");
            }
        }
        public static void GlasswellRejectsRemoteCachedLensButtonsAndWrongSunPhase(Campaign data)
        {
            GameCore g=GlasswellReady(data);
            WorldObject well=LensSite(g,"glassloom_source");
            WorldObject lens=LensSite(g,"glassloom_mirror_1");
            g.save.x=well.x;g.save.y=well.y;
            g.Talk(well);
            int start=Array.FindIndex(g.conversation.choices,
                c=>c.action=="glassloom_start");
            Check(start>=0,"Real sunlit Glasswell exposes its authentic begin action");
            g.save.x=well.x+7f;g.Choose(start);
            Check(g.GlassLoomStage==0,
                "Stale menu cannot start the optical puzzle from outside physical reach");
            g.save.x=well.x;g.save.y=well.y;g.Talk(well);
            Check(LensChoice(g,"glassloom_start"),"Real source can begin after genuine approach");
            g.save.x=lens.x;g.save.y=lens.y;g.Talk(lens);
            int rotate=Array.FindIndex(g.conversation.choices,
                c=>c.action=="glassloom_turn");
            Check(rotate>=0,"The first actual mirror offers manual rotation");
            g.save.x=lens.x+8f;g.Choose(rotate);
            Check(g.GlassLoomOrientation(0)==0&&g.save.energy==100f,
                "A remote cached rotation neither changes optics nor spends stamina");
            g.save.x=lens.x;g.save.y=lens.y;g.Talk(lens);
            Check(LensChoice(g,"glassloom_turn")&&
                g.GlassLoomOrientation(0)==1&&g.save.energy==97f,
                "Only physically turning the lens consumes exactly three energy");
            g.save.energy=0;
            g.Talk(lens);LensChoice(g,"glassloom_turn");
            Check(g.GlassLoomOrientation(0)==1&&g.save.energy==0,
                "Without stamina, repeated lens rotations are rejected rather than free");
            g.save.totalTime=160f;
            g.save.x=well.x;g.save.y=well.y;g.Talk(well);
            LensChoice(g,"glassloom_focus");
            Check(g.GlassLoomStage==1,
                "Neither incomplete mirrors nor true night can generate false solar charge");
            g.Close();g.PrepareSave();
            var reloaded=new GameCore(data,CloneSave(g.save));
            Check(!reloaded.GlassLoomActive&&!reloaded.GlassLoomComplete,
                "Saving during an incomplete attempt never serializes temporary puzzle rotation");
        }
        public static void GlasswellPhysicalWallBreaksRealOpticalPropagation(Campaign data)
        {
            MapData original=data.maps[6];
            string[] rows=(string[])original.rows.Clone();
            try
            {
                var g=GlasswellReady(data);
                Check(g.GlassLoomPathClear,
                    "Five original authored devices have an unobstructed default corridor");
                char[] row=rows[50].ToCharArray();
                row[66]='#';rows[50]=new string(row);
                data.maps[6]=new MapData { id=6,spawnX=original.spawnX,
                    spawnY=original.spawnY,rows=rows,objects=original.objects,
                    decor=original.decor };
                var sealedWell=GlasswellReady(data);
                Check(!sealedWell.GlassLoomPathClear,
                    "A real map wall inserted between two mirrors blocks the optical route");
                var source=LensSite(sealedWell,"glassloom_source");
                sealedWell.save.x=source.x;sealedWell.save.y=source.y;
                sealedWell.Talk(source);
                LensChoice(sealedWell,"glassloom_start");
                Check(sealedWell.GlassLoomStage==0,
                    "Cannot launch a supposedly physical puzzle through a new actual wall");
            }
            finally {data.maps[6]=original;}
        }
        public static void GlasswellOriginalReportGateAndTrueClockPreventFakeClear(Campaign data)
        {
            var g=Fresh(data);g.save.stage=3;g.save.flags.Add("harbor_report");
            g.Enter(6);g.enemies.Clear();
            WorldObject well=LensSite(g,"glassloom_source");
            g.save.x=well.x;g.save.y=well.y;g.Talk(well);
            Check(g.GetQuest("glass_loom").locked&&
                !LensChoice(g,"glassloom_start"),
                "Original desert boss/report remain required before civilians can reclaim the prism");
            g.Close();g.save.flags.Add("boss_6");g.save.flags.Add("desert_report");
            g.save.totalTime=30f;g.Talk(well);
            Check(LensChoice(g,"glassloom_start"),"Earned original progress unlocks optical chapter");
            g.TrackQuest("glass_loom");
            var time=typeof(GameCore).GetField("glassLoomTime",
                System.Reflection.BindingFlags.NonPublic|
                System.Reflection.BindingFlags.Instance);
            time.SetValue(g,.01f);
            g.Tick(.05f,new Point());
            Check(!g.GlassLoomActive&&!g.GlassLoomComplete,
                "Truly elapsed world time resets an unfinished optical attempt without rewards");
            g.PrepareSave();
            GameCore reload=new GameCore(data,CloneSave(g.save));
            Check(reload.save.trackedQuest=="glass_loom"&&reload.save.version==2&&
                !reload.GlassLoomComplete&&!reload.GlassLoomActive,
                "The genuine journal remains trackable after old-slot reload; no ghost light persists");
        }
    }
}
