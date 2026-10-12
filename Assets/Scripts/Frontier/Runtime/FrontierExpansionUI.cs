using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int journalTab, relicSlot;
        private void DrawRay(Point from, Point to, Color color)
        {
            Point a = ScreenPoint(from), b = ScreenPoint(to); int count = Math.Max(1, (int)((b - a).Length / 5));
            for (int i = 0; i <= count; i++) { Point p = a + (b - a) * (i / (float)count); Box(p.x - 2, p.y - 2, 4, 4, color); }
        }
        private void DrawCompanionActor()
        {
            Point p = ScreenPoint(Game.companionPosition); int id = Game.save.companion;
            if (companionSheets == null || id < 0 || id >= companionSheets.Length || companionSheets[id] == null)
            { DrawNpc("companion", Game.companionPosition, id + 8); return; }
            ActorAction action = Game.companionAction;
            Point direction = action == null ? Game.companionAim : action.direction;
            bool north = direction.y < -Math.Abs(direction.x), south = direction.y > Math.Abs(direction.x);
            int frame = north ? 8 : 0;
            if (action != null)
                frame = north ? (action.released ? 7 : 6) : south ? (action.released ? 10 : 9) : action.released && action.kind == "command" && id != 1 ? 5 : action.released ? 4 : 3;
            Box(p.x - 14, p.y - 3, 28, 6, new Color(.025f, .04f, .07f, .32f));
            bool overlaps = Math.Abs(Game.companionPosition.x - Game.save.x) < .85f && Game.save.y <= Game.companionPosition.y && Game.save.y > Game.companionPosition.y - 1.8f;
            Color tint = overlaps ? new Color(1, 1, 1, .45f) : Color.white;
            if (Game.companionMoving && action == null && companionWalkSheets[id] != null)
            { int row=GameCore.DirectionRow(direction);HumanActor(companionWalkSheets[id],4,3,row*4+GameCore.GaitFrame(Game.companionMoveTime,GameCore.WalkStride),p,.75f,tint,row==0&&direction.x<0,companionWalkSheets[id],row*4,true); }
            else if(south&&action==null&&companionWalkSheets[id]!=null)HumanActor(companionWalkSheets[id],4,3,8,p,.75f,tint,false,companionWalkSheets[id],8,true);
            else HumanActor(companionSheets[id],3,4,frame,p,.75f,tint,!north&&!(south&&action!=null)&&direction.x<-.1f,companionWalkSheets[id],GameCore.DirectionRow(direction)*4,action==null);
            Box(p.x - 13, p.y + 1, 26, 3, mint);
        }
        private void DrawHazardWarnings()
        {
            foreach (Hazard h in Game.hazards)
            {
                Color warning = HazardWarningColor(h);
                Ring(h.position, h.radius, warning); Point p = ScreenPoint(h.position);
                Text(p.x - 25, p.y - 25, 50, 24, h.timer.ToString("0.0"), 13, warning, TextAnchor.MiddleCenter);
            }
        }
        private Color HazardWarningColor(Hazard hazard)
        { return hazard.kind == "glass" ? gold : hazard.kind == "bell" ? PixelArt.C("#9bd8f5") : hazard.kind == "root" ? PixelArt.C("#82d9a3") : hazard.kind == "rift" ? PixelArt.C("#b9a1d1") : PixelArt.C("#d59583"); }
        private void DrawExpansionWorld()
        {
            if (Game.racing)
            {
                Point target = GameCore.RaceRoute[Game.raceCheckpoint]; Ring(target, .65f, gold, 20); Point p = ScreenPoint(target);
                Text(p.x - 20, p.y - 44, 40, 35, (Game.raceCheckpoint + 1).ToString(), 23, gold, TextAnchor.MiddleCenter);
                Box(430, 112, 420, 48, panel); Text(449, 124, 382, 30, Game.L("风旗竞速 ", "WIND RACE ") + Game.raceTime.ToString("0.0") + " / 30s   ·   " + Game.raceCheckpoint + "/5", 17, gold, TextAnchor.MiddleCenter);
            }
            if (Game.reliefMissionRoom == Game.save.room)
            {
                Point rescue = Game.reliefMissionWave <= 2 ? Game.FieldSurveyTarget(Game.save.room) :
                    Game.FieldSurveyTarget(Game.save.room);
                Ring(rescue, .55f, Game.reliefMissionWave == 3 ? mint : gold, 20);
                Point rp = ScreenPoint(rescue);
                Text(rp.x - 56, rp.y - 65, 112, 24,
                    Game.reliefMissionWave == 3 ? Game.L("护送返程", "ESCORT HOME") :
                        Game.L("营救居民", "RESCUE CIVILIANS"), 12, gold, TextAnchor.MiddleCenter);
            }
            if (Game.save.room == 7 && !Game.save.reducedMotion && Game.save.effectsQuality > 0)
                for (int i = 0; i < 28; i++) { float x = (i * 91 + clock * 19) % Width, y = (i * 67 + clock * 32) % Height; Box(x, y, 3, 3, new Color(.9f, .96f, 1, .58f)); }
        }
        private bool DrawExpansionObject(WorldObject o)
        {
            string kind = o.kind; Point p = ScreenPoint(new Point(o.x, o.y));
            if (kind == "archiveecho")
            {
                bool completed = Game.Has("archive_echo_complete"), active = Game.AtlasEchoRoom == Game.save.room;
                Color tint = active ? PixelArt.C("#e7b0f2") : completed ? mint : gold;
                PropImage("column", 0, p.x - 28, p.y - 65, 56, 73, completed ? muted : Color.white);
                Ring(new Point(o.x, o.y), .55f, tint, 20);
                Text(p.x - 53, p.y - 92, 106, 22,
                    active ? Game.L("回声激活", "ECHO ACTIVE") :
                    completed ? Game.L("已完成", "CALIBRATED") :
                    Game.L("方位试炼", "COMPASS TRIAL"),
                    12, tint, TextAnchor.MiddleCenter);
                Enemy guardian = Game.enemies.Find(enemy => enemy.id == 83001);
                if (active && guardian != null)
                    Bar(p.x - 37, p.y + 12, 74,
                        guardian.hp / Math.Max(1, guardian.maxHp), tint, 6);
                return true;
            }
            if (kind == "archivekeeper" || kind.StartsWith("archivefragment"))
            {
                bool collected = kind == "archivekeeper" ? Game.Has("archive_complete") :
                    Game.Has("archive_fragment_" + kind.Substring("archivefragment".Length));
                PropImage(kind == "archivekeeper" ? "post" : "altar", 0,
                    p.x - 26, p.y - 55, 52, 63, collected ? muted : Color.white);
                Ring(new Point(o.x, o.y), .48f, collected ? mint : gold, 16);
                Text(p.x - 18, p.y - 69, 36, 25, collected ? "✓" : "!", 19,
                    collected ? mint : gold, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("fieldstation"))
            {
                int station;
                if (!int.TryParse(kind.Substring("fieldstation".Length), out station) ||
                    station < 5 || station > 7) return false;
                bool powered = Game.Has("fieldstation_" + station);
                bool reinforced = Game.Has("farshore_relief_" + station);
                Color light = reinforced ? PixelArt.C("#a7e8da") : powered ? mint : gold;
                PropImage("lamp", 0, p.x - 26, p.y - 54, 52, 65, powered ? Color.white : muted);
                Ring(new Point(o.x, o.y), .55f, light, 16);
                Text(p.x - 55, p.y - 86, 110, 24,
                    reinforced ? Game.L("民用避难区", "RELIEF REFUGE") :
                    powered ? Game.L("安全观测站", "SHELTER ONLINE") :
                    Game.L("等待维修", "REPAIR STATION"), 11, light, TextAnchor.MiddleCenter);
                if (powered && station >= 6 && Game.NearActiveFieldStation(station, Game.FieldStationRefugeRadius(station)))
                    Ring(new Point(o.x, o.y), reinforced ? 1.4f : 1.1f,
                        new Color(light.r, light.g, light.b, .52f), 24);
                if (reinforced)
                    Text(p.x - 32, p.y + 13, 64, 21, station == 5 ? "90s" : "6.5", 13,
                        light, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind.StartsWith("mirror"))
            {
                int id = int.Parse(kind.Substring(6)); PropImage("mirror", id, p.x - 34, p.y - 68, 68, 79);
                Text(p.x - 25, p.y - 92, 50, 25, new[] { "N", "E", "S", "W" }[Game.save.mirrors[id]], 17, gold, TextAnchor.MiddleCenter); return true;
            }
            if (kind.StartsWith("bell"))
            {
                int id = int.Parse(kind.Substring(4)); PropImage("bell", id, p.x - 36, p.y - 73, 72, 84);
                Text(p.x - 30, p.y - 94, 60, 25, new[] { "I", "II", "III", "IV" }[Game.save.bells[id]], 17, gold, TextAnchor.MiddleCenter); return true;
            }
            if (kind.StartsWith("relay"))
            { bool lit = Game.Has("relay_" + kind.Substring(5)); PropImage("lamp", 0, p.x - 24, p.y - 54, 48, 56, lit ? Color.white : muted); if (lit) Ring(new Point(o.x, o.y), .4f, mint, 12); return true; }
            if (kind == "campfire" || kind == "brazier")
            {
                bool lit = kind == "campfire" || Game.Has("warm_" + o.id);
                PropImage("fire", 0, p.x - 28, p.y - 44, 56, 65, lit ? Color.white : muted); return true;
            }
            if (kind.StartsWith("history") || kind == "manifest")
            {
                PropImage("post", 0, p.x - 24, p.y - 55, 48, 56);
                int letter = -1;
                bool cipher = kind.StartsWith("history") && int.TryParse(kind.Substring(7), out letter) &&
                    letter >= 6 && letter <= 8 && Game.Has("survey_complete");
                bool decoded = cipher && Game.Has("dispatch_" + letter);
                if (cipher) Ring(new Point(o.x, o.y), .43f, decoded ? mint : gold, 14);
                Text(p.x - 16, p.y - 51, 32, 23, cipher ? decoded ? "✓" : "!" : "?", 17,
                    decoded ? mint : gold, TextAnchor.MiddleCenter);
                return true;
            }
            if (kind == "expAltar" || kind == "receiver" || kind == "sluice" || kind == "abyssGate" || kind == "abyssEntry" || kind == "abyssExit")
            {
                bool active = kind == "expAltar" ? Game.GateOpen : kind == "receiver" ? Game.Has("mirrors_lit") : kind == "abyssEntry" ? Game.CanEnter(8) : true;
                PropImage(kind.StartsWith("abyss") ? "root" : "altar", 0, p.x - 34, p.y - 68, 68, 79, active ? Color.white : muted);
                Ring(new Point(o.x, o.y), .6f, active ? mint : muted, 16); return true;
            }
            if (kind == "shade" || kind == "skiff") return true;
            if (kind == "townboard" || kind == "contract")
            { PropImage("post", 0, p.x - 30, p.y - 66, 60, 70); Text(p.x - 14, p.y - 55, 28, 26, kind == "contract" ? "N" : "B", 16, gold, TextAnchor.MiddleCenter); return true; }
            if (kind == "envoy" || kind == "captain" || kind == "tinker" || kind == "keeper" || kind == "council" || kind.StartsWith("castaway"))
            {
                DrawNpc(kind, new Point(o.x, o.y));
                bool complete = kind == "captain" ? Game.Has("harbor_report") : kind == "tinker" ? Game.Has("desert_report") : kind == "keeper" ? Game.Has("mountain_report") : false;
                Text(p.x - 12, p.y - 86, 24, 25, complete ? "+" : "!", 22, complete ? mint : gold, TextAnchor.MiddleCenter); return true;
            }
            return false;
        }
    }
}
