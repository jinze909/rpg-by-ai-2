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
            PropImage("crate",0,screen.x+8,screen.y-43,23,23,
                new Color(.94f,.76f,.48f,.95f));
            Text(screen.x-68,screen.y-85,136,21,
                Game.L("救援包 "+(int)Game.FarCourierTimeLeft+"秒",
                    "PARCEL "+(int)Game.FarCourierTimeLeft+"s"),
                12,Game.FarCourierTimeLeft<20?
                    new Color(.98f,.38f,.29f):gold,TextAnchor.MiddleCenter);
        }
    }
}
