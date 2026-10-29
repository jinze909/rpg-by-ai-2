using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // This runs the shipped bridge, the genuine moving raider actors,
        // armed physical snares, the inspector and the original refuge.
        public static void RealCourierRaiderCapturesSecureAndStockTheReceivingRefuge(Campaign data)
        {
            var g=new GameCore(data);g.save.room=0;
            g.enemies.Clear();
            g.save.flags.Add("far_bridge_0");
            g.save.flags.Add("far_shelter_0");
            g.save.inventory[0]=12;g.save.inventory[4]=12;
            g.save.energy=100f;
            WorldObject bridge=Array.Find(g.Map.objects,o=>o.kind=="farbridge");
            WorldObject post=Array.Find(g.Map.objects,o=>o.kind=="farcheck");
            WorldObject refuge=Array.Find(g.Map.objects,o=>o.kind=="farshelter");
            Check(bridge!=null&&post!=null&&refuge!=null,
                "Courier uses the three original physically authored map objects");
            g.save.x=bridge.x;g.save.y=bridge.y;g.Talk(bridge);
            int pickup=Array.FindIndex(g.conversation.choices,
                x=>x.action=="far_courier_start");
            Check(pickup>=0,"The repaired bridge physically offers the parcel");
            g.Choose(pickup);
            for(int i=0;i<100&&!g.FarCourierCrossed;i++)
                g.Tick(.045f,new Point(0,1));
            Check(g.FarCourierActive&&g.FarCourierCrossed&&
                g.FarCourierRaidersAlive==2,
                "Real southbound bridge crossing creates both reachable original pursuers");
            Enemy[] raiders=g.enemies.ToArray();
            Check(raiders.Length==2&&!g.FarCourierSecuredInFlight,
                "Two genuine live raiders must be resolved before delivery");
            foreach(Enemy raider in raiders)raider.hitTime=8f;
            int coins=g.save.coins,kills=g.save.kills[0];
            // A duplicate numeric ID is NOT this mission's enemy. It gets
            // the same genuine physical snare test and must grant no credit.
            for(int capture=0;capture<3;capture++)
            {
                bool laid=false;
                foreach(Point direction in new[]{new Point(0,1),
                    new Point(1,0),new Point(-1,0),new Point(0,-1)})
                    if(g.LayFieldSnare(direction)){laid=true;break;}
                Check(laid&&g.FieldSnares.Count==1,
                    "A real in-world snare is physically placed and paid for");
                Point foot=g.FieldSnares[0].foot;
                Enemy target=capture==0?
                    new Enemy(raiders[0].id,0,foot.x,foot.y,60f):
                    raiders[capture-1];
                target.x=foot.x;target.y=foot.y;target.hitTime=8f;
                if(capture==0)g.enemies.Add(target);
                Check(!g.CanBindSnaredEnemy,
                    "Restraint is impossible until actual armed snare contact");
                for(int frame=0;frame<11;frame++)
                    g.Tick(.05f,new Point());
                Check(System.Object.ReferenceEquals(g.SnaredBindTarget,target)&&
                    g.BindSnaredEnemy()&&!g.enemies.Contains(target),
                    "A real snare triggers on the exact living actor and allows paid nonlethal capture");
                Check(!g.FarCourierHumane(0)&&!g.FarCourierSecured(0)&&
                    g.save.coins==coins&&g.save.kills[0]==kills,
                    "Neither unrelated captures nor genuine in-flight captures mint courier money or kill loot");
                if(capture<2)
                    Check(!g.FarCourierSecuredInFlight,
                        "Duplicate ID and single genuine restraint cannot count as two secured pursuers");
            }
            Check(g.FarCourierRaidersAlive==0&&
                g.FarCourierSecuredInFlight&&g.FarCourierHumaneInFlight,
                "Only two physically restrained original pursuers complete a secure humane flight");
            Check(!g.FarCourierDelivered(0)&&!g.FarCourierHumane(0),
                "Provisional tactical victory is not a real civilian handoff");
            for(int i=0;i<100&&g.save.y<61.35f;i++)
                g.Tick(.045f,new Point(0,1));
            Check((g.Position-new Point(post.x,post.y)).Length<2.2f,
                "The civilian checkpoint is physically reached");
            g.Talk(post);
            int stamp=Array.FindIndex(g.conversation.choices,
                x=>x.action=="far_courier_stamp");
            Check(stamp>=0,"Inspector offers real parcel seal");
            g.Choose(stamp);
            for(int i=0;i<110&&g.save.y<73.2f;i++)
                g.Tick(.045f,new Point(0,1));
            g.Talk(refuge);
            int handoff=Array.FindIndex(g.conversation.choices,
                x=>x.action=="far_courier_deliver");
            Check(handoff>=0,"Original civilian refuge accepts the parcel");
            g.Choose(handoff);
            Check(g.FarCourierDelivered(0)&&g.FarCourierSecured(0)&&
                g.FarCourierHumane(0)&&!g.FarCourierActive&&
                g.save.coins==coins+35,
                "Real handoff registers one paid secure and living-captive receipt");
            g.save.health=20f;
            g.Talk(refuge);
            Check(g.conversation.text.Contains("非致命护送")&&
                g.conversation.textEn.Contains("Both taken alive"),
                "Existing refuge actor explains actual regional civilian care upgrade");
            int rest=Array.FindIndex(g.conversation.choices,
                x=>x.action=="far_rest");
            Check(rest>=0,"Original paid shelter rest remains interactive");
            int wood=g.save.inventory[0];
            g.Choose(rest);
            Check(Math.Abs(g.save.health-75f)<.01f&&
                g.save.inventory[0]==wood-1,
                "Actual paid rest restores base 35 plus pristine 10 plus humane 10 HP");
            g.PrepareSave();
            var loaded=new GameCore(data,CloneSave(g.save));
            Check(loaded.save.version==2&&loaded.FarCourierHumane(0)&&
                !loaded.FarCourierHumane(1)&&!loaded.FarCourierActive,
                "Existing v2 save persists only earned local humanitarian care");
        }
    }
}
