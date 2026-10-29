using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Each field device runs inside real combat, from the second original
        // physical landmark. Never award its effects through invented IDs.
        public static void FourChapterFieldDevicesSpendResourcesAndReallyControlAttackers(Campaign data)
        {
            for(int room=1;room<=4;room++)
            {
                GameCore g=Fresh(data);g.mode="world";g.save.room=room;
                g.save.stage=room-1;g.enemies.Clear();
                g.save.inventory[0]=6;g.save.inventory[4]=6;
                g.save.energy=100;g.save.health=35;
                WorldObject start=Array.Find(g.Map.objects,
                    o=>o.id=="chapterroad_"+room+"_0");
                WorldObject middle=Array.Find(g.Map.objects,
                    o=>o.id=="chapterroad_"+room+"_1");
                Check(start!=null&&middle!=null&&
                    PlaceWithinTalkReach(g,start),"Four authentic rescue origins are walkable");
                g.Talk(start);
                int begin=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_begin");
                Check(begin>=0,"Actual region source offers the real chapter rescue");
                g.Choose(begin);
                Check(g.ChapterRoadActive&&PlaceWithinTalkReach(g,middle),
                    "After accepting, a real original midpoint is reachable");
                g.Talk(middle);
                int defend=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_defend");
                Check(defend>=0,"Real defense is still needed before the device exists");
                g.Choose(defend);
                Check(g.ChapterRoadStage==2&&g.ChapterRoadDeviceReady&&
                    g.ChapterRoadRemaining==(room>=3?3:2),
                    "Only the true original live attackers unlock a device");
                Enemy[] wave=g.enemies.ToArray();
                Enemy spoof=new Enemy(wave[0].id,wave[0].kind,
                    1000f,1000f,40f);
                g.enemies.Add(spoof);
                float timer=g.ChapterRoadSeconds;
                int timber=g.save.inventory[0];float energy=g.save.energy;
                g.save.cold=40;g.save.heat=40;
                Check(PlaceWithinTalkReach(g,middle),
                    "The real battlefield device is approachable even with live raiders");
                g.Talk(middle);
                Check(g.conversation!=null,"Actual mid-battle device opens its local menu");
                int activation=Array.FindIndex(g.conversation.choices,
                    c=>c.action=="chapterroad_device");
                Check(activation>=0,"Chapter "+room+" offers its unique active battlefield mechanism");
                g.Choose(activation);
                Check(g.ChapterRoadDeviceUsed&&!g.ChapterRoadDeviceReady&&
                    g.ChapterRoadRemaining==wave.Length&&
                    !g.ChapterRoadSecured&&spoof.stun==0f,
                    "Device changes authentic combat without paying kills or affecting a duplicate-ID impostor");
                int affected=0;
                foreach(Enemy e in wave)
                    if(e.stun>0&&e.armorOpenTime>0)affected++;
                Check(affected>=1,"Actual defense device disrupts at least one genuine fighter's combat action");
                if(room==1)
                    Check(g.save.inventory[0]==timber-1&&
                        g.save.health==47f&&g.save.energy==energy,
                        "The bell charges a real timber and rescues wounded civilians during combat");
                if(room==2)
                    Check(g.save.inventory[0]==timber&&g.save.energy==energy-18f&&
                        g.save.cold==28f&&g.ChapterRoadSeconds==timer+15f,
                        "Sluice diverts actual cold flood and purchases 15 seconds using stamina");
                if(room==3)
                    Check(g.save.energy==energy-18f&&g.save.heat==24f&&
                        g.ChapterRoadSeconds==timer+12f,
                        "Manifold vent purges real heat and buys 12 seconds");
                if(room==4)
                    Check(g.save.energy==energy-7f&&
                        g.save.inventory[0]==timber,
                        "Civilian signal has unique tactical stamina cost and ally rally");
                g.Close();g.Talk(middle);
                Check(g.conversation!=null&&
                    Array.FindIndex(g.conversation.choices,
                        c=>c.action=="chapterroad_device")<0,
                    "Paid battlefield mechanism cannot be reused on the same encounter");
                // Live enemies remain real and are not stored as ordinary
                // saved patrols, even after their source-specific stun.
                g.PrepareSave();
                GameCore loaded=new GameCore(data,CloneSave(g.save));
                Check(!loaded.ChapterRoadActive&&!loaded.ChapterRoadDeviceUsed&&
                    !loaded.ChapterRoadDone(room),
                    "V2 reload cannot persist a transient active device or mint a saved mission receipt");
            }
        }

        // The choice at the FINAL physical handoff is not just prose:
        // publishing testimony strengthens the actual local field service.
        public static void PublicWitnessChoiceImprovesActualRegionalRecovery(Campaign data)
        {
            for(int room=2;room<=4;room++)
            {
                float[] result=new float[2];
                for(int version=0;version<2;version++)
                {
                    GameCore g=Fresh(data);g.mode="world";g.save.room=room;
                    g.enemies.Clear();g.save.health=35;g.save.energy=45;
                    g.save.cold=55;g.save.heat=55;
                    g.save.flags.Add("chapter_road_done_"+room);
                    if(version==1)g.save.flags.Add("chapter_road_witness_"+room);
                    WorldObject handoff=Array.Find(g.Map.objects,
                        o=>o.id=="chapterroad_"+room+"_2");
                    Check(handoff!=null&&PlaceWithinTalkReach(g,handoff),
                        "Only actual chapter handoff site can provide earned local support");
                    Check(g.SafeTravel,
                        "The signed civilian service requires a safe, idle field");
                    for(int step=0;step<18;step++)g.Tick(.05f,new Point());
                    result[version]=room==2?g.save.cold:
                        room==3?g.save.energy:g.save.health;
                }
                Check(room==2?result[1]<result[0]-.05f:
                    result[1]>result[0]+.05f,
                    "Publishing a civilian testimony improves its own region's genuine physical service: "+room);
            }
        }

        public static void SignedChapterCivilianRoutesChangeActualGuardianBattleHealth(Campaign data)
        {
            for(int room=1;room<=4;room++)
            {
                float[] hp=new float[2];
                for(int version=0;version<2;version++)
                {
                    GameCore g=Fresh(data);g.mode="world";g.save.room=room;
                    g.save.stage=room-1;g.enemies.Clear();
                    g.save.flags.Add("gate_"+room);
                    if(version==1)
                        g.save.flags.Add("chapter_road_done_"+room);
                    WorldObject shrine=Array.Find(g.Map.objects,
                        o=>o.kind=="shrine");
                    Check(shrine!=null&&PlaceWithinTalkReach(g,shrine),
                        "Original chapter guardian altar remains physically reachable");
                    g.Talk(shrine);
                    Check(g.conversation!=null&&g.mode=="dialog",
                        "Each original guardian offers its genuine confrontation menu");
                    int fight=Array.FindIndex(g.conversation.choices,
                        c=>c.action=="boss");
                    Check(fight>=0,"New civilian support cannot remove original Boss confrontation");
                    g.Choose(fight);
                    Enemy guardian=g.enemies.Find(e=>e.boss);
                    Check(guardian!=null&&!g.Has("boss_"+room),
                        "The full genuine Boss actually spawned and remains alive");
                    hp[version]=guardian.hp;
                }
                Check(Math.Abs(hp[1]-hp[0]*.92f)<.1f,
                    "Only this chapter's earned civilian receipt weakens the actual guardian by 8%: "+room);
            }
        }
        public static void FourGuardianShrinesActuallyAcknowledgeSignedResidentsAndChoice(Campaign data)
        {
            string[] publicPhrase={"",
                "rescue bell rings again","ferry crew speak the missing names",
                "Miners nail their stop-work petition",
                "Orrin sees the new wall of names"};
            string[] privatePhrase={"",
                "rescue bell rings again","crew seal their names inside",
                "Workers carry their protected work order",
                "An evacuated child gives Orrin"};
            for(int room=1;room<=4;room++)
                for(int choice=0;choice<3;choice++)
                {
                    GameCore g=Fresh(data);g.mode="world";
                    g.save.room=room;g.save.stage=room-1;g.enemies.Clear();
                    g.save.flags.Add("gate_"+room);
                    if(choice>0)g.save.flags.Add("chapter_road_done_"+room);
                    if(choice==1)g.save.flags.Add("chapter_road_witness_"+room);
                    WorldObject shrine=Array.Find(g.Map.objects,
                        o=>o.kind=="shrine");
                    Check(shrine!=null&&PlaceWithinTalkReach(g,shrine),
                        "Original region's guardian altar remains real and reachable");
                    g.Talk(shrine);
                    Check(g.conversation!=null&&
                        Array.FindIndex(g.conversation.choices,
                            c=>c.action=="boss")>=0,
                        "Original guardian battle menu is never replaced by chapter prose");
                    string speech=g.conversation.textEn;
                    if(choice==0)
                        Check(!speech.Contains(publicPhrase[room])&&
                            !speech.Contains(privatePhrase[room]),
                            "Unfinished civilian story cannot impersonate a signed rescue");
                    else
                        Check(speech.Contains(choice==1?
                            publicPhrase[room]:privatePhrase[room]),
                            "Original boss recognizes actual regional testimony and its distinct published/sealed choice: "+room);
                }
        }
    }
}
