using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private void DrawCivicConduitCoil()
        {
            if(!Game.CivicConduitActive)return;
            Point screen=ScreenPoint(Game.Position);
            float strength=Game.CivicConduitIntegrity;
            Color tint=strength<=35?new Color(.98f,.37f,.31f):
                Game.CivicConduitWindowOpen?new Color(.54f,.95f,.57f):
                new Color(.49f,.75f,.96f);
            // One compact carried conductor on the SAME canonical golden-haired
            // actor. No duplicated player, sprite swapping or animated flicker.
            PropImage("crystal",0,screen.x+10,screen.y-43,21,22,tint);
            Bar(screen.x-34,screen.y-59,68,strength/100f,tint,4);
            Text(screen.x-88,screen.y-88,176,20,
                Game.L("导体 "+(int)Math.Ceiling(strength)+"% · "+
                    (int)Math.Ceiling(Game.CivicConduitTimeLeft)+"秒",
                    "CONDUCTOR "+(int)Math.Ceiling(strength)+"% · "+
                    (int)Math.Ceiling(Game.CivicConduitTimeLeft)+"s"),
                12,Game.CivicConduitTimeLeft<18?
                    new Color(.98f,.38f,.31f):tint,TextAnchor.MiddleCenter);
        }

        private void DrawCivicResponsePack()
        {
            if(!Game.CivicResponseActive)return;
            Point screen=ScreenPoint(Game.Position);
            if(Game.CivicResponseClinicRun)
            {
                float condition=Game.CivicResponseIntegrity;
                Color health=condition<=35?new Color(.99f,.36f,.38f):
                    condition<=65?gold:new Color(.49f,.94f,.9f);
                // Genuine kit carried by the existing golden-haired hero.
                // World depth, equipment and the canonical sprite stay intact.
                PropImage("crate",0,screen.x+9,screen.y-45,22,22,
                    condition<=35?new Color(1f,.54f,.54f,.95f):
                    new Color(.57f,.92f,.87f,.96f));
                Bar(screen.x-31,screen.y-60,62,condition/100f,health,4);
                Text(screen.x-91,screen.y-88,182,21,
                    Game.L("医疗包 "+(int)Math.Ceiling(condition)+"% · "+
                        (int)Game.CivicResponseTimeLeft+"秒",
                        "MEDICINE "+(int)Math.Ceiling(condition)+"% · "+
                        (int)Game.CivicResponseTimeLeft+"s"),
                    12,Game.CivicResponseTimeLeft<18?
                        new Color(.99f,.38f,.34f):health,TextAnchor.MiddleCenter);
            }
            else if(Game.CivicResponseWatchRun)
            {
                int enemies=Game.CivicResponseEnemiesAlive;
                Text(screen.x-92,screen.y-105,184,20,
                    enemies>0?
                        Game.L("守路敌人 "+enemies+" 名",
                            "WATCH RAIDERS "+enemies):
                        Game.L("返回避险棚确认守卫",
                            "REPORT TO REFUGE"),
                    12,enemies>0?new Color(.99f,.49f,.35f):mint,
                    TextAnchor.MiddleCenter);
            }
        }

        private void DrawFarCourierPack()
        {
            if(!Game.FarCourierActive)return;
            Point screen=ScreenPoint(Game.Position);
            // Small parcel attached to the canonical playable golden-haired
            // protagonist; no second hero, palette replacement or global HUD.
            if(Game.FarCourierRaidersAlive>0)
                Text(screen.x-79,screen.y-130,158,19,
                    Game.L("路上劫匪 ×"+Game.FarCourierRaidersAlive,
                        "RAIDERS ×"+Game.FarCourierRaidersAlive),
                    12,new Color(.98f,.43f,.31f),TextAnchor.MiddleCenter);
            float integrity=Game.FarCourierIntegrity;
            Color cargoColor=integrity<=35?new Color(.98f,.38f,.29f):
                integrity<=65?gold:mint;
            PropImage("crate",0,screen.x+8,screen.y-43,23,23,
                integrity<=35?new Color(.99f,.58f,.45f,.95f):
                    new Color(.94f,.76f,.48f,.95f));
            // Small depth-sorted field indicator tied to the *same* playable
            // hero, not a second avatar or a permanently obscuring HUD panel.
            Bar(screen.x-31,screen.y-59,62,integrity/100f,cargoColor,4);
            if(Game.FarCourierGuardLeft>0)
            {
                // Static even under reduced motion. The original hero
                // animation and appearance equipment remain untouched.
                Ring(Game.Position,.69f,new Color(.46f,.95f,.86f,.67f),18);
                Text(screen.x-72,screen.y-108,144,20,
                    Game.L("结界 "+(int)Math.Ceiling(Game.FarCourierGuardLeft)+"秒",
                        "WARD "+(int)Math.Ceiling(Game.FarCourierGuardLeft)+"s"),
                    12,mint,TextAnchor.MiddleCenter);
            }
            Text(screen.x-83,screen.y-86,166,21,
                Game.L("救援包 "+(int)Math.Ceiling(integrity)+"% · "+
                    (int)Game.FarCourierTimeLeft+"秒",
                    "PARCEL "+(int)Math.Ceiling(integrity)+"% · "+
                    (int)Game.FarCourierTimeLeft+"s"),
                12,Game.FarCourierTimeLeft<20?new Color(.98f,.38f,.29f):
                    cargoColor,TextAnchor.MiddleCenter);
        }
    }
}
