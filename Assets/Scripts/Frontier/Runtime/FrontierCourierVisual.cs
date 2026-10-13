using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private void DrawFarCourierPack()
        {
            if(!Game.FarCourierActive)return;
            Point screen=ScreenPoint(Game.Position);
            // Small parcel attached to the canonical playable golden-haired
            // protagonist; no second hero, palette replacement or global HUD.
            float integrity=Game.FarCourierIntegrity;
            Color cargoColor=integrity<=35?new Color(.98f,.38f,.29f):
                integrity<=65?gold:mint;
            PropImage("crate",0,screen.x+8,screen.y-43,23,23,
                integrity<=35?new Color(.99f,.58f,.45f,.95f):
                    new Color(.94f,.76f,.48f,.95f));
            // Small depth-sorted field indicator tied to the *same* playable
            // hero, not a second avatar or a permanently obscuring HUD panel.
            Bar(screen.x-31,screen.y-59,62,integrity/100f,cargoColor,4);
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
