using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed partial class GameCore
    {
        // One retreat per floor: trade UNBANKED loot for survival rather
        // than receiving free heals or spending permanently banked shards.
        private string AbyssRootwellFlag
        {
            get { return "abyss_annex_rootwell_" + save.abyssFloor; }
        }

        private WorldObject AbyssRootwell()
        {
            if (!InAbyss || Map.objects == null) return null;
            return Array.Find(Map.objects, o => o.kind == "abyssRootwell");
        }

        private void TalkAbyssRootwell(WorldObject o)
        {
            if (!InAbyss || Dying || practicing || !EnsurePhysicalInteractionReach(o)) return;
            if (Has(AbyssRootwellFlag))
            {
                Dialog(o.name, o.en,
                    "复苏泉已将这一层的光流交给你。再次使用必须进入下一层。",
                    "The rootwell has shared this floor's light. Descend to find another charge.",
                    C("继续前进", "Keep exploring", "close"));
                return;
            }
            if (!SafeTravel)
            {
                Notify("附近仍有战斗或落印威胁，无法安全接触复苏泉。",
                    "Nearby combat or a live rift warning makes the rootwell unsafe.");
                return;
            }
            Dialog(o.name, o.en,
                "旧巡路人将地脉改成公共避难泉。这里的能量并非免费：你可以拿尚未结算的碎片交换疗伤，或者恢复施法与闪避的耐力。每层只能选择一次。\n\n本层待结算碎片：" +
                    save.runShards + "。永久存入背包的碎片不会被扣除。",
                "Old roadwardens turned this starroot into a public refuge. Spend UNBANKED shards to heal or replenish energy. Choose once per floor; permanent banked shards are never spent.\n\nUnbanked shards: " +
                    save.runShards + ".",
                C("疗伤 · 碎片2 · 回复40%最大生命",
                    "Mend wounds · 2 shards · heal 40% max HP", "abyssRootwellHeal"),
                C("回气 · 碎片1 · 回复55耐力",
                    "Recover energy · 1 shard · +55 energy", "abyssRootwellEnergy"),
                C("暂时不用", "Not yet", "close"));
        }

        private void UseAbyssRootwell(bool heal)
        {
            if (!InAbyss || Dying || practicing || Has(AbyssRootwellFlag)) return;
            WorldObject rootwell = AbyssRootwell();
            // Re-check the LIVE source after the modal choice. A stale or
            // fabricated remote choice must never consume resources.
            if (rootwell == null || !EnsurePhysicalInteractionReach(rootwell)) return;
            if (!SafeTravel)
            {
                Notify("敌人或落印尚未平息，无法安全完成地脉交换。",
                    "Clear nearby threats before exchanging shards at the rootwell.");
                return;
            }
            int cost = heal ? 2 : 1;
            if (save.runShards < cost)
            {
                Notify("本层待结算碎片不足。探索支洞或击败敌人后再来。",
                    "Not enough unbanked shards. Explore the galleries or defeat enemies.");
                return;
            }
            if (heal ? save.health >= save.maxHealth : save.energy >= 100)
            {
                Notify(heal ? "生命已满，无需消耗碎片。" : "耐力已满，无需消耗碎片。",
                    heal ? "Already at full health; no shards spent." :
                        "Already at full energy; no shards spent.");
                return;
            }
            save.runShards -= cost;
            if (heal)
                save.health = Math.Min(save.maxHealth, save.health + save.maxHealth * .4f);
            else
                save.energy = Math.Min(100, save.energy + 55);
            Flag(AbyssRootwellFlag);
            revision++;
            sound = "reward";
            Notify(heal ? "复苏泉缝合了创口。本层已无法再次取用。" :
                    "星根回流补足了耐力。本层已无法再次取用。",
                heal ? "The rootwell mends your wounds. This floor's charge is spent." :
                    "Starroot restores your energy. This floor's charge is spent.");
        }

        public int AbyssWardCharterProgress
        {
            get
            {
                int count = 0;
                for (int floor = 3; floor <= 6; floor++)
                    if (Has("abyss_annex_ward_" + floor)) count++;
                return count;
            }
        }

        // A strategic answer to floor-three-plus rift storms: pay for
        // sustained mitigation with loot from this SAME unbanked floor.
        public bool AbyssWardActive
        {
            get { return InAbyss && save.abyssFloor >= 3 &&
                Has("abyss_annex_ward_" + save.abyssFloor); }
        }

        private WorldObject AbyssWardObject()
        {
            if (!InAbyss || save.abyssFloor < 3 || Map.objects == null) return null;
            return Array.Find(Map.objects, o => o.kind == "abyssRiftWard");
        }

        private void TalkAbyssWard(WorldObject o)
        {
            if (!InAbyss || save.abyssFloor < 3 || Dying || practicing ||
                !EnsurePhysicalInteractionReach(o)) return;
            if (AbyssWardActive)
            {
                Dialog(o.name, o.en,
                    "镇流碑已经与本层地脉共振。新的落印间隔延长至10秒，预警更明显，落印范围与伤害也减轻。",
                    "The ward is tuned for this floor: new rifts arrive every 10 seconds with longer warnings, smaller circles and reduced damage.",
                    C("继续探索", "Keep exploring", "close"));
                return;
            }
            Dialog(o.name, o.en,
                "地下巡路人曾用镇流碑保护救援路线。你可以消耗本层待结算碎片3枚，让本层落印从每6秒一次放缓到10秒一次，并减小其伤害与范围。只有这一层有效；原本已存入背包的碎片不会消耗。\\n\\n当前本层碎片：" +
                    save.runShards + "。",
                "Roadwardens used this pylon to protect evacuation routes. Trade 3 UNBANKED shards to slow this floor's rift pulses from 6 to 10 seconds, shrink their radius and reduce damage. Resets next floor; banked shards are untouched.\\n\\nCurrent unbanked shards: " +
                    save.runShards + ".",
                C("镇流 · 待结算碎片3", "Tune ward · 3 unbanked shards", "abyssWardTune"),
                C("保留战利品", "Keep the loot", "close"));
        }

        private void TuneAbyssWard()
        {
            if (!InAbyss || save.abyssFloor < 3 || Dying || practicing ||
                AbyssWardActive || Has("abyss_clear_" + save.abyssFloor)) return;
            WorldObject ward = AbyssWardObject();
            if (ward == null || !EnsurePhysicalInteractionReach(ward)) return;
            if (save.runShards < 3)
            {
                Notify("本层未结算碎片不足3枚。可先击败敌人或搜寻支洞补给。",
                    "You need 3 unbanked shards. Defeat enemies or search the side galleries.");
                return;
            }
            save.runShards -= 3;
            Flag("abyss_annex_ward_" + save.abyssFloor);
            // Do not advance a pending telegraph or invent an extra hit.
            // Newly scheduled floor hazards use the slower ten-second clock.
            stormTimer = Math.Max(stormTimer, 10f);
            revision++;
            sound = "reward";
            Notify("镇流碑已稳定：本层落印每10秒一次，范围和伤害降低。",
                "Ward tuned: this floor's rifts arrive every 10 seconds with smaller, weaker strikes.");
        }

        // Three distant, physically visited starroot relays form an optional
        // real route puzzle. Progress and one-time rewards live in EXISTING
        // SaveData v2 flags; no new serialized fields or transient shortcuts.
        private static readonly int[,] AbyssCircuitOrders = {
            { 0, 1, 2 }, { 0, 2, 1 }, { 1, 0, 2 },
            { 1, 2, 0 }, { 2, 0, 1 }, { 2, 1, 0 }
        };
        private string AbyssCircuitStepFlag(int step)
        {
            return "abyss_annex_circuit_" + save.abyssFloor + "_" + step;
        }
        private string AbyssCircuitFinishFlag
        {
            get { return "abyss_annex_circuit_complete_" + save.abyssFloor; }
        }
        public bool AbyssCircuitComplete
        {
            get { return InAbyss && Has(AbyssCircuitFinishFlag); }
        }
        public int AbyssCircuitProgress
        {
            get
            {
                if (!InAbyss) return 0;
                if (AbyssCircuitComplete) return 3;
                int count = 0;
                while (count < 3 && Has(AbyssCircuitStepFlag(count))) count++;
                // An interrupted or malformed old v2 flags list may contain
                // all step marks but not the completion transaction. Do not
                // index a nonexistent fourth relay from Objective()/HUD.
                // Requiring the final physical relay again is the safe path.
                return Math.Min(2, count);
            }
        }
        private int AbyssCircuitTarget(int step)
        {
            // Long arithmetic preserves every valid positive v2 seed.
            int variation = (int)(((long)save.abyssSeed +
                save.abyssFloor * 13L) % 6L);
            return AbyssCircuitOrders[variation, step];
        }
        public int AbyssCircuitNextNode
        {
            get { return InAbyss && !AbyssCircuitComplete ?
                AbyssCircuitTarget(AbyssCircuitProgress) : -1; }
        }
        private static string AbyssCircuitSymbol(int node)
        {
            return node == 0 ? "A" : node == 1 ? "B" : "C";
        }
        public bool AbyssCircuitVisited(int node)
        {
            if (!InAbyss || node < 0 || node > 2) return false;
            for (int i = 0; i < AbyssCircuitProgress; i++)
                if (AbyssCircuitTarget(i) == node) return true;
            return false;
        }
        // Linked field relays form a small tactical refuge, not an
        // invulnerability toggle. The benefit applies only to actual Rift
        // ground contacts near a visited relay with an unobstructed route.
        // Flag-backed v2 progress makes the shelter survive reloads.
        public const float AbyssCircuitShelterRadius = 3.25f;
        public bool AbyssCircuitSheltered
        {
            get
            {
                if (!InAbyss || save.abyssFloor < 3 ||
                    Map.objects == null) return false;
                float range2 = AbyssCircuitShelterRadius * AbyssCircuitShelterRadius;
                foreach (WorldObject relay in Map.objects)
                {
                    if (!relay.kind.StartsWith("abyssCircuit")) continue;
                    int node;
                    if (!int.TryParse(relay.kind.Substring("abyssCircuit".Length),
                        out node) || !AbyssCircuitVisited(node)) continue;
                    float dx = relay.x - save.x, dy = relay.y - save.y;
                    if (dx * dx + dy * dy <= range2 &&
                        ClearAttackPath(new Point(relay.x, relay.y), Position))
                        return true;
                }
                return false;
            }
        }
        private float AbyssRiftContactDamage(Hazard h)
        {
            // All other hazards retain their existing balance, windups,
            // parries and real line-of-sight collision contracts.
            return h.kind == "rift" && AbyssCircuitSheltered ?
                h.damage * .45f : h.damage;
        }

        private string AbyssCircuitGroundFlag
        {
            get { return "abyss_annex_circuit_ground_" + save.abyssFloor; }
        }
        private bool GroundableAbyssRift(Hazard warning)
        {
            if (warning.kind != "rift" || warning.damage <= 0) return false;
            float dx=warning.position.x-save.x, dy=warning.position.y-save.y;
            float radius = AbyssCircuitShelterRadius + .3f;
            return dx*dx+dy*dy <= radius*radius &&
                ClearAttackPath(warning.position,Position);
        }
        private bool AbyssCircuitCanGround
        {
            get
            {
                if (!InAbyss || !AbyssCircuitComplete ||
                    Has(AbyssCircuitGroundFlag) || save.runShards < 2 ||
                    !NearLiveAbyssCircuit()) return false;
                foreach (Hazard h in hazards)
                    if (GroundableAbyssRift(h)) return true;
                return false;
            }
        }
        private void GroundAbyssCircuitRifts()
        {
            // Recheck at CHOICE time; walking away, spending loot or a
            // contact expiring during the dialogue must not take payment.
            if (Dying || practicing || !AbyssCircuitCanGround)
            {
                Notify("回路接地不可用：须靠近已接通信标，携带2枚待结算碎片，并有附近落印。",
                    "Circuit grounding needs a nearby linked relay, two unbanked shards and a live local rift.");
                return;
            }
            int grounded = hazards.RemoveAll(GroundableAbyssRift);
            if (grounded <= 0) return;
            save.runShards -= 2;
            Flag(AbyssCircuitGroundFlag);
            revision++;
            sound="reward";
            effects.Add(new Effect("spark", Position, .6f));
            Notify("星根接地成功：清除了 " + grounded +
                    " 道近处落印预警。本层接地能量已经耗尽。",
                "Starroot grounded " + grounded +
                    " nearby rift warnings. This floor's grounding charge is spent.");
        }

        private bool AbyssCircuitRouteSafe()
        {
            // A real windup or buffered command belongs to its ORIGINAL
            // world location. Do not carry a pending sword/staff release,
            // companion arrow or dodge across thirty tiles and complete it
            // from a false new origin after the route transfer.
            return InAbyss && AbyssCircuitComplete && !Dying && !practicing &&
                Has("abyss_clear_" + save.abyssFloor) &&
                playerAction == null && companionAction == null &&
                !SkillQueued && !AttackQueued && !DodgeQueued &&
                !ActionTransitionBlocked &&
                enemies.Count == 0 && hazards.Count == 0 &&
                projectiles.Count == 0 && SafeTravel;
        }
        private bool NearLiveAbyssCircuit()
        {
            if (!InAbyss || Map.objects == null) return false;
            foreach (WorldObject relay in Map.objects)
                if (relay.kind.StartsWith("abyssCircuit") &&
                    HasPhysicalInteractionReach(relay)) return true;
            return false;
        }
        private string AbyssCircuitOptionalFlag(WorldObject o)
        {
            string prefix = null;
            switch (o.kind)
            {
                case "abyssSurvey": prefix = "abyss_annex_survey_"; break;
                case "abyssCache": prefix = "abyss_annex_cache_"; break;
                case "abyssFarSurvey": prefix = "abyss_annex_far_survey_"; break;
                case "abyssFarCache": prefix = "abyss_annex_far_cache_"; break;
                case "abyssFarEcho": prefix = "abyss_annex_far_echo_"; break;
                case "abyssRootwell": prefix = "abyss_annex_rootwell_"; break;
                case "abyssRiftWard": prefix = "abyss_annex_ward_"; break;
            }
            return prefix == null ? null : prefix + save.abyssFloor;
        }
        // Waypoint intent belongs to this visit, not to the saved campaign.
        private int abyssCircuitGuideBranch = -1, abyssCircuitGuideFloor = -1;
        private MapData abyssCircuitGuideMap;
        private WorldObject abyssCircuitGuideSite;
        private int abyssCircuitGuideProofKey = int.MinValue;
        // A missing target is also cached. Otherwise the actual world
        // navigation renderer would re-run cave-wide BFS on every frame.
        private bool abyssCircuitGuideEvaluated;
        // Speaking to an unfinished relay opts in to puzzle guidance;
        // existing persisted step flags also resume mid-circuit on reload.
        private bool abyssCircuitPuzzleGuideActive;
        // Opt-in route to the next physically recoverable testimony. The
        // player's selected world objective is transient, not SaveData v2.
        // Keep the same record until it is actually collected: choosing the
        // nearest site every frame would make the compass flip mid-walk.
        private bool abyssTestimonyGuideActive;
        private int abyssTestimonyGuideFloor = -1;
        private MapData abyssTestimonyGuideMap;
        private WorldObject abyssTestimonyGuideSite;
        // Ambient guide is NOT an active player choice: memoize by live
        // player tile and proof progress so HUD/objects/journal all agree
        // without re-running seeded-cave BFS on every IMGUI repaint.
        private MapData abyssTestimonySuggestionMap;
        private WorldObject abyssTestimonySuggestion;
        private int abyssTestimonySuggestionFloor = -1;
        private int abyssTestimonySuggestionX = int.MinValue;
        private int abyssTestimonySuggestionY = int.MinValue;
        private int abyssTestimonySuggestionProgress = -1;
        public WorldObject AbyssTestimonySuggestedSite
        {
            get
            {
                if (!InAbyss || Map.objects == null ||
                    AbyssTestimonySealed) return null;
                int x = (int)Math.Floor(save.x), y = (int)Math.Floor(save.y);
                int progress = AbyssTestimonyProgress;
                if (!Object.ReferenceEquals(abyssTestimonySuggestionMap, Map) ||
                    abyssTestimonySuggestionFloor != save.abyssFloor ||
                    abyssTestimonySuggestionX != x ||
                    abyssTestimonySuggestionY != y ||
                    abyssTestimonySuggestionProgress != progress ||
                    (abyssTestimonySuggestion != null &&
                        AbyssTestimonyRecordState(abyssTestimonySuggestion) != 1))
                {
                    abyssTestimonySuggestionMap = Map;
                    abyssTestimonySuggestionFloor = save.abyssFloor;
                    abyssTestimonySuggestionX = x;
                    abyssTestimonySuggestionY = y;
                    abyssTestimonySuggestionProgress = progress;
                    abyssTestimonySuggestion = ReachableAbyssTestimonySite();
                }
                return abyssTestimonySuggestion;
            }
        }
        public WorldObject AbyssTestimonyGuidedSite
        {
            get
            {
                if (!abyssTestimonyGuideActive || !InAbyss ||
                    abyssTestimonyGuideFloor != save.abyssFloor ||
                    !Object.ReferenceEquals(abyssTestimonyGuideMap, Map))
                    return null;
                if (abyssTestimonyGuideSite == null ||
                    AbyssTestimonyRecordState(abyssTestimonyGuideSite) != 1 ||
                    Array.IndexOf(Map.objects, abyssTestimonyGuideSite) < 0)
                    abyssTestimonyGuideSite = ReachableAbyssTestimonySite();
                return abyssTestimonyGuideSite;
            }
        }
        // Check arrival at a PHYSICALLY INTERACTABLE foot, not merely
        // near the object's center. Some authored tablets stand inside
        // solid scenery: the player reaches them from adjacent ground, not
        // by occupying the tablet cell. Likewise, a route that ends across
        // a solid wall must never be described as reachable.
        // Synchronous BFS uses the same live Fits and shared search stamps
        // as gameplay pathfinding. Called only for a new route choice or
        // a collected waypoint; no BFS runs on ordinary IMGUI repaint.
        private bool CanNavigateToAbyssTestimony(WorldObject site)
        {
            if (site == null || !InAbyss || Map.rows == null ||
                Map.rows.Length == 0 || Map.rows[0] == null ||
                !Finite(site.x) || !Finite(site.y)) return false;
            Point target = new Point(site.x, site.y);
            if (HasPhysicalInteractionReach(site)) return true;
            int height = Map.rows.Length, width = Map.rows[0].Length;
            int sx = (int)Math.Floor(save.x), sy = (int)Math.Floor(save.y);
            if (sx < 0 || sy < 0 || sx >= width || sy >= height)
                return false;
            PreparePathSearch(width * height);
            int front = 0, back = 0, origin = sy * width + sx;
            pathQueue[back++] = origin;
            VisitPath(origin, -1);
            while (front < back)
            {
                int cell = pathQueue[front++];
                int x = cell % width, y = cell / width;
                Point foot = cell == origin ? Position :
                    new Point(x + .5f, y + .5f);
                Point delta = target - foot;
                float range = delta.Length;
                if (Finite(range) && range < 1.75f)
                {
                    Point contact = range > .55f ?
                        target - delta.Normalized * .55f : foot;
                    if (ClearAttackPath(foot, contact)) return true;
                }
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + guidanceSteps[i, 0];
                    int ny = y + guidanceSteps[i, 1];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;
                    int next = ny * width + nx;
                    if (PathVisited(next) ||
                        !Fits(new Point(nx + .5f, ny + .5f)))
                        continue;
                    VisitPath(next, cell);
                    pathQueue[back++] = next;
                }
            }
            return false;
        }
        private WorldObject ReachableAbyssTestimonySite()
        {
            // A geometrically near account can require a much longer walk
            // around the seeded cavern walls. Search all available accounts
            // in ONE breadth-first walk and return the first physically
            // interactable foot reached. Earlier BFS layers are strictly
            // shorter than later layers; authored object order breaks ties.
            // Unlike a per-site route probe this reuses one search stamp.
            if (!InAbyss || Map.objects == null || Map.rows == null ||
                Map.rows.Length == 0 || Map.rows[0] == null) return null;
            var available = new List<WorldObject>();
            foreach (WorldObject site in Map.objects)
                if (AbyssTestimonyRecordState(site) == 1)
                    available.Add(site);
            if (available.Count == 0) return null;
            int height = Map.rows.Length, width = Map.rows[0].Length;
            int sx = (int)Math.Floor(save.x), sy = (int)Math.Floor(save.y);
            if (sx < 0 || sy < 0 || sx >= width || sy >= height)
                return null;
            PreparePathSearch(width * height);
            int front = 0, back = 0, origin = sy * width + sx;
            pathQueue[back++] = origin;
            VisitPath(origin, -1);
            while (front < back)
            {
                int cell = pathQueue[front++];
                int x = cell % width, y = cell / width;
                Point foot = cell == origin ? Position :
                    new Point(x + .5f, y + .5f);
                foreach (WorldObject site in available)
                {
                    Point goal = new Point(site.x, site.y);
                    Point delta = goal - foot;
                    float distance = delta.Length;
                    if (!Finite(distance) || distance >= 1.75f)
                        continue;
                    Point contact = distance > .55f ?
                        goal - delta.Normalized * .55f : foot;
                    if (ClearAttackPath(foot, contact))
                        return site;
                }
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + guidanceSteps[i, 0];
                    int ny = y + guidanceSteps[i, 1];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;
                    int next = ny * width + nx;
                    if (PathVisited(next) ||
                        !Fits(new Point(nx + .5f, ny + .5f))) continue;
                    VisitPath(next, cell);
                    pathQueue[back++] = next;
                }
            }
            return null;
        }
        private Point? AbyssTestimonyGuideTarget()
        {
            WorldObject next = AbyssTestimonyGuidedSite;
            return next == null ? (Point?)null : new Point(next.x, next.y);
        }
        private void TrackAbyssTestimonyFromGate()
        {
            // A saved/stale gate dialogue cannot remotely command guidance.
            // Tracking changes no encounter, save flag, reward or location.
            if (!CanBankFloor() || !NearLiveAbyssGate())
            {
                Notify("请靠近安全的东侧地脉门重新选择证词路线。",
                    "Return to the safe east gate to select testimony guidance.");
                return;
            }
            if (AbyssTestimonyNextSite == null)
            {
                Notify("本层可回收的证词已全部归档。",
                    "No recoverable testimony remains on this floor.");
                return;
            }
            WorldObject next = ReachableAbyssTestimonySite();
            if (next == null)
            {
                Notify("没有当前可到达的巡路证词；可清除阻碍后重试。",
                    "No reachable testimony right now. Clear the blockage and retry.");
                return;
            }
            ActivateAbyssTestimonyGuidance(next);
        }
        private void ActivateAbyssTestimonyGuidance(WorldObject next)
        {
            abyssTestimonyGuideActive = true;
            abyssTestimonyGuideFloor = save.abyssFloor;
            abyssTestimonyGuideMap = Map;
            abyssTestimonyGuideSite = next;
            abyssCircuitGuideBranch = -1;
            abyssCircuitGuideFloor = -1;
            abyssCircuitGuideMap = null;
            revision++;
            Notify("已追踪巡路证词：" + next.name + "。东门结算仍须手动选择。",
                "Tracking testimony: " + next.en + ". Banking remains manual.");
        }
        private void TrackAbyssTestimonyFromRecord()
        {
            // ChooseCore retains the real dialogue source only for this
            // action. An unrelated object or stale/distant conversation
            // cannot spoof a genuine recovered testimony as a tracker.
            WorldObject source = pendingDialogueSource;
            if (!InAbyss || Dying || practicing ||
                source == null || Map.objects == null ||
                Array.IndexOf(Map.objects, source) < 0 ||
                !HasPhysicalInteractionReach(source) ||
                AbyssTestimonyRecordState(source) != 2)
            {
                Notify("请走近已记录的巡路证词，才能继续追踪。",
                    "Approach a recovered warden record to follow its trail.");
                return;
            }
            if (AbyssTestimonyNextSite == null)
            {
                Notify("本层证词已齐，请到东门手动结算。",
                    "All accounts are filed. Bank manually at the east gate.");
                return;
            }
            WorldObject next = ReachableAbyssTestimonySite();
            if (next == null)
            {
                Notify("当前没有可安全到达的下一份证词，请先检查洞穴道路。",
                    "No reachable next account; check the cavern passages.");
                return;
            }
            ActivateAbyssTestimonyGuidance(next);
        }
        private bool CanChooseAbyssTestimonyFromRecord()
        {
            WorldObject source = pendingDialogueSource;
            return InAbyss && !Dying && !practicing &&
                source != null && Map.objects != null &&
                Array.IndexOf(Map.objects, source) >= 0 &&
                HasPhysicalInteractionReach(source) &&
                AbyssTestimonyRecordState(source) == 2;
        }
        private int AbyssAvailableTestimonyRoutes
        {
            get
            {
                if (!InAbyss || Map.objects == null) return 0;
                int count = 0;
                foreach (WorldObject site in Map.objects)
                    if (AbyssTestimonyRecordState(site) == 1 &&
                        CanNavigateToAbyssTestimony(site)) count++;
                return count;
            }
        }
        private void OpenAbyssTestimonyRouteList()
        {
            if (!CanChooseAbyssTestimonyFromRecord())
            {
                Notify("回到已收录的证词石板旁，再选择路线。",
                    "Return to a recovered field tablet before choosing a route.");
                return;
            }
            var choices = new List<Choice>();
            foreach (WorldObject site in Map.objects)
                if (AbyssTestimonyRecordState(site) == 1 &&
                    CanNavigateToAbyssTestimony(site))
                    choices.Add(C("前往 " + site.name,
                        "Follow " + site.en, "abyssTestimonyTo:" + site.id));
            choices.Add(C("继续当前调查", "Leave routes unchanged", "close"));
            Dialog("巡路证词 · 路线选择", "Warden testimony · route selector",
                "从已经连通的现场道路中选择下一处未归档证词。路线只改变指引，不会自动结算战利品。",
                "Select a reachable, unfiled field record. This changes navigation only; banking remains manual.",
                choices.ToArray());
        }
        private void ChooseAbyssTestimonyRoute(string targetId)
        {
            if (!CanChooseAbyssTestimonyFromRecord())
            {
                Notify("必须靠近原来的石板，路线选择才会生效。",
                    "Return to the original tablet to confirm this route.");
                return;
            }
            WorldObject destination = Array.Find(Map.objects,
                o => o.id == targetId &&
                    AbyssTestimonyRecordState(o) == 1);
            if (!CanNavigateToAbyssTestimony(destination))
            {
                Notify("该证词路线目前不可到达，请重新选路。",
                    "That record is no longer reachable. Choose another route.");
                return;
            }
            ActivateAbyssTestimonyGuidance(destination);
        }
        private void StopAbyssTestimonyTrackingAtGate()
        {
            // Deliberate cancellation requires the same original live gate.
            // No teleport, extra loot, floor banking or saved flag changes.
            if (!CanBankFloor() || !NearLiveAbyssGate())
            {
                Notify("须站在安全的东侧地脉门旁才能取消追踪。",
                    "Return to the safe east gate to stop tracking.");
                return;
            }
            abyssTestimonyGuideActive = false;
            abyssTestimonyGuideSite = null;
            abyssTestimonyGuideFloor = -1;
            abyssTestimonyGuideMap = null;
            revision++;
            Notify("已停止追踪证词。你仍可随时手动结算或重新选择调查。",
                "Testimony tracking stopped. You may bank or investigate again.");
        }
        public int AbyssCircuitGuidedNextRelay
        {
            get { return AbyssCircuitPuzzleTarget().HasValue ?
                AbyssCircuitNextNode : -1; }
        }
        private Point? AbyssCircuitPuzzleTarget()
        {
            if (!InAbyss || AbyssCircuitComplete ||
                (!abyssCircuitPuzzleGuideActive && AbyssCircuitProgress == 0) ||
                Map.objects == null) return null;
            int next = AbyssCircuitNextNode;
            if (next < 0 || next > 2) return null;
            WorldObject relay = Array.Find(Map.objects,
                o => o.kind == "abyssCircuit" + next);
            return relay == null ? (Point?)null : new Point(relay.x, relay.y);
        }
        private WorldObject[] AbyssCircuitRelays()
        {
            var relays = new WorldObject[3];
            for (int i = 0; i < 3; i++)
                relays[i] = Array.Find(Map.objects, o => o.kind == "abyssCircuit" + i);
            return relays;
        }
        private static int AbyssCircuitNearestRelay(WorldObject site, WorldObject[] relays)
        {
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < relays.Length; i++)
            {
                if (relays[i] == null) continue;
                float dx = site.x - relays[i].x, dy = site.y - relays[i].y;
                float distance = dx * dx + dy * dy;
                if (distance < best) { nearest = i; best = distance; }
            }
            return nearest;
        }
        // Read-only, live presentation state: hidden after a gallery is
        // exhausted and never serialized into an existing save slot.
        public int AbyssCircuitGuidedBranch
        {
            get { return AbyssCircuitGuideTarget().HasValue ?
                abyssCircuitGuideBranch : -1; }
        }
        // A sealed echo is not a discoverable reward until its source
        // tablet is recorded. If the chosen branch contains only that
        // echo, guide toward the physically placed prerequisite instead
        // of directing the hero into a knowingly locked interaction.
        public bool AbyssCircuitGuideNeedsSource
        {
            get
            {
                if (!InAbyss || Map.objects == null ||
                    Has("abyss_annex_far_survey_" + save.abyssFloor))
                    return false;
                WorldObject echo = Array.Find(Map.objects,
                    o => o.kind == "abyssFarEcho");
                WorldObject source = Array.Find(Map.objects,
                    o => o.kind == "abyssFarSurvey");
                if (echo == null || source == null ||
                    Has("abyss_annex_far_echo_" + save.abyssFloor) ||
                    AbyssCircuitNearestRelay(echo, AbyssCircuitRelays()) !=
                        abyssCircuitGuideBranch)
                    return false;
                Point? next = AbyssCircuitGuideTarget();
                return next.HasValue &&
                    (next.Value - new Point(source.x, source.y)).Length < .001f;
            }
        }
        private Point? AbyssCircuitGuideTarget()
        {
            if (!InAbyss || !AbyssCircuitComplete ||
                abyssCircuitGuideBranch < 0 || abyssCircuitGuideBranch > 2 ||
                abyssCircuitGuideFloor != save.abyssFloor ||
                !Object.ReferenceEquals(abyssCircuitGuideMap, Map) ||
                Map.objects == null) return null;
            // Live v2 flags are the authority. Recheck only when a real
            // optional site was filed or the echo's source was unlocked.
            int proofKey = AbyssCircuitUnvisitedSites(abyssCircuitGuideBranch) +
                (Has("abyss_annex_far_survey_" + save.abyssFloor) ? 32 : 0);
            if (!abyssCircuitGuideEvaluated ||
                proofKey != abyssCircuitGuideProofKey)
            {
                abyssCircuitGuideEvaluated = true;
                abyssCircuitGuideProofKey = proofKey;
                abyssCircuitGuideSite = null;
                WorldObject[] relays = AbyssCircuitRelays();
                WorldObject origin = relays[abyssCircuitGuideBranch];
                if (origin == null) return null;
                WorldObject lockedEcho = null;
                float best = float.MaxValue;
                foreach (WorldObject site in Map.objects)
                {
                    string flag = AbyssCircuitOptionalFlag(site);
                    if (flag == null || Has(flag) ||
                        AbyssCircuitNearestRelay(site, relays) != abyssCircuitGuideBranch)
                        continue;
                    if (site.kind == "abyssFarEcho" &&
                        AbyssTestimonyRecordState(site) == 3)
                    {
                        lockedEcho = site;
                        continue;
                    }
                    // An optional objective only exists when a real walking
                    // route reaches a legal interaction foot, not just its
                    // icon or a point on the far side of a wall.
                    if (!CanNavigateToAbyssTestimony(site)) continue;
                    float dx = site.x - origin.x, dy = site.y - origin.y;
                    float distance = dx * dx + dy * dy;
                    if (distance < best)
                    { best = distance; abyssCircuitGuideSite = site; }
                }
                if (abyssCircuitGuideSite == null && lockedEcho != null)
                {
                    WorldObject source = Array.Find(Map.objects,
                        o => o.kind == "abyssFarSurvey");
                    if (source != null &&
                        !Has("abyss_annex_far_survey_" + save.abyssFloor) &&
                        CanNavigateToAbyssTestimony(source))
                        abyssCircuitGuideSite = source;
                }
            }
            WorldObject next = abyssCircuitGuideSite;
            return next == null ? (Point?)null : new Point(next.x, next.y);
        }
        public int AbyssCircuitUnvisitedSites(int branch)
        {
            if (!InAbyss || branch < 0 || branch > 2 ||
                Map.objects == null) return 0;
            WorldObject[] relays = AbyssCircuitRelays();
            if (relays[branch] == null) return 0;
            int remaining = 0;
            foreach (WorldObject site in Map.objects)
            {
                string flag = AbyssCircuitOptionalFlag(site);
                if (flag == null || Has(flag)) continue;
                if (AbyssCircuitNearestRelay(site, relays) == branch)
                    remaining++;
            }
            return remaining;
        }
        private string AbyssCircuitRouteLabel(int branch, bool english)
        {
            string name = AbyssCircuitSymbol(branch);
            int remaining = AbyssCircuitUnvisitedSites(branch);
            return english ? "Travel to relay " + name +
                " · " + remaining + " sites left" :
                "前往信标 " + name + " · 待探索 " + remaining;
        }

        private bool NearLiveAbyssGate()
        {
            if (!InAbyss || Map.objects == null) return false;
            WorldObject gate = Array.Find(Map.objects, o => o.kind == "abyssGate");
            return gate != null && HasPhysicalInteractionReach(gate);
        }
        private void OpenAbyssCircuitGateRoutes()
        {
            if (!AbyssCircuitRouteSafe() || !NearLiveAbyssGate())
            {
                Notify("东门光路不可用：须清除本层敌人和落印，并站在地脉门旁。",
                    "Gate circuit unavailable. Clear every threat and approach the east root gate.");
                return;
            }
            var routes = new List<Choice> {
                C(AbyssCircuitRouteLabel(0, false), AbyssCircuitRouteLabel(0, true), "abyssCircuitTo0"),
                C(AbyssCircuitRouteLabel(1, false), AbyssCircuitRouteLabel(1, true), "abyssCircuitTo1"),
                C(AbyssCircuitRouteLabel(2, false), AbyssCircuitRouteLabel(2, true), "abyssCircuitTo2")
            };
            if (CompanionHoldingPosition)
                routes.Add(C("召回驻守的伙伴", "Recall stationed companion", "abyssCircuitRecall"));
            routes.Add(C("返回结算门", "Back to bank menu", "abyssCircuitGateMenu"));
            Dialog("星根巡路网络", "Starroot route network",
                "三座支洞信标已与东门重新联通。选择支洞继续探索；传送不会自动结算本层战利品。" +
                    (CompanionHoldingPosition ? " 也可以明确召回驻守的伙伴。" : ""),
                "The three side relays are reconnected to the east gate. Visit a gallery to finish exploring. Travel never banks unbanked loot." +
                    (CompanionHoldingPosition ? " You can also recall a stationed companion." : ""),
                routes.ToArray());
        }
        private void ReopenAbyssCircuitGateMenu()
        {
            if (AbyssCircuitRouteSafe() && NearLiveAbyssGate())
                AbyssGateConversation();
        }
        private void TravelViaAbyssCircuit(int destination)
        {
            if (!AbyssCircuitRouteSafe() ||
                (!NearLiveAbyssCircuit() && !NearLiveAbyssGate()))
            {
                Notify("光路不可用：请靠近信标，结束当前攻击或指令，并清除敌人和落印。",
                    "Route unavailable. Reach a relay, finish queued attacks or commands, and clear threats.");
                return;
            }
            if (destination < -1 || destination > 2) return;
            WorldObject stop = destination < 0 ? null :
                Array.Find(Map.objects, o => o.kind == "abyssCircuit" + destination);
            if (destination >= 0 && stop == null) return;
            Point arrival = stop == null ? new Point(30.5f, 14.5f) :
                new Point(stop.x, stop.y);
            // The destination must still be a real, free tile on THIS map.
            // Never re-enter the region, clear enemies, grant loot or start a
            // new save slot just to perform a cleared-floor route transfer.
            if (!CanStand(arrival))
            {
                Notify("光路目的地暂时被地形阻断。",
                    "The starroot route destination is blocked by terrain.");
                return;
            }
            Point previousHero = Position;
            save.x = arrival.x; save.y = arrival.y;
            // Choosing a gallery also opts in to its unfinished real sites.
            // A trip to the east gate restores the original exit guidance.
            abyssCircuitGuideBranch = destination;
            abyssTestimonyGuideActive = false;
            abyssTestimonyGuideSite = null;
            abyssCircuitGuideFloor = save.abyssFloor;
            abyssCircuitGuideMap = Map;
            abyssCircuitGuideSite = null;
            abyssCircuitGuideEvaluated = false;
            abyssCircuitGuideProofKey = int.MinValue;
            RejoinCompanionAfterAbyssCircuitTravel(previousHero);
            revision++;
            sound = "travel";
            Notify(stop == null ?
                    "沿星根光路抵达东门。战利品仍须手动结算。" :
                    "沿星根光路抵达信标 " + AbyssCircuitSymbol(destination) +
                        "。可继续搜寻尚未领取的支洞补给。",
                stop == null ?
                    "Reached the east gate. Bank rewards manually." :
                    "Reached relay " + AbyssCircuitSymbol(destination) +
                        ". Continue exploring any remaining side-gallery supplies.");
        }
        private void RecallCompanionViaAbyssCircuit()
        {
            if (!AbyssCircuitRouteSafe() ||
                (!NearLiveAbyssCircuit() && !NearLiveAbyssGate()) ||
                !CompanionHoldingPosition)
            {
                Notify("召回失败：必须靠近安全的光路站点，且确有伙伴在驻守。",
                    "Recall unavailable. Reach a safe Starroot stop with an ally on hold.");
                return;
            }
            Point landing = CompanionFollowTarget();
            // An unsafe destination may be viable for the HERO but not a
            // second full-bodied ally. Never cancel hold and merge sprites.
            if (!Fits(landing) || (landing-Position).Length < .55f)
            {
                Notify("目的地缺少伙伴可站立的位置，驻守指令维持不变。",
                    "No safe companion foot by this relay. The hold order remains.");
                return;
            }
            ClearCompanionHold();
            RejoinCompanionAfterAbyssCircuitTravel(Position, true);
            revision++;
            sound = "travel";
            Notify("伙伴已沿星根光路返回队伍，原有技能冷却保持不变。",
                "Companion recalled through the Starroot route; skill cooldown preserved.");
        }
        private void ReturnViaAbyssCircuit()
        {
            TravelViaAbyssCircuit(-1);
        }

        private void TalkAbyssCircuit(WorldObject o)
        {
            if (!InAbyss || Dying || practicing ||
                !EnsurePhysicalInteractionReach(o)) return;
            int node;
            if (!int.TryParse(o.kind.Substring("abyssCircuit".Length),
                out node) || node < 0 || node > 2) return;
            if (AbyssCircuitComplete)
            {
                if (!AbyssCircuitRouteSafe())
                {
                    if (AbyssCircuitCanGround)
                    {
                        Dialog(o.name, o.en,
                            "回路已接通，但仍有落印即将爆发。你可以消耗本层待结算碎片2枚，接地并消除附近的地脉落印预警。每层只能使用一次；其他敌人和危险仍需亲自应对。",
                            "The circuit is linked but rift warnings are still charging. Trade 2 unbanked shards to ground nearby rift telegraphs, once per floor. Enemies and other hazards remain.",
                            C("接地 · 未结算碎片2", "Ground local rifts · 2 pending shards", "abyssCircuitGround"),
                            C("保持回路", "Leave the circuit", "close"));
                        return;
                    }
                    Notify("回路已接通。须先清除敌人、落印，并完成正在施放的攻击或同伴指令。",
                        "Circuit linked. Clear enemies and rifts, and finish active attacks or ally commands before traveling.");
                    return;
                }
                var relayChoices = new List<Choice> {
                    C("沿光路折返东门", "Travel to east root gate", "abyssCircuitReturn"),
                    C(AbyssCircuitRouteLabel(0, false), AbyssCircuitRouteLabel(0, true), "abyssCircuitTo0"),
                    C(AbyssCircuitRouteLabel(1, false), AbyssCircuitRouteLabel(1, true), "abyssCircuitTo1"),
                    C(AbyssCircuitRouteLabel(2, false), AbyssCircuitRouteLabel(2, true), "abyssCircuitTo2")
                };
                if (CompanionHoldingPosition)
                    relayChoices.Add(C("召回驻守的伙伴", "Recall stationed companion",
                        "abyssCircuitRecall"));
                relayChoices.Add(C("继续探索", "Keep exploring", "close"));
                Dialog(o.name, o.en,
                    "旧巡路光路已恢复：可直达东门，或走访 A/B/C 支洞。安全时使用；传送不自动结算。" +
                        (CompanionHoldingPosition ? " 驻守伙伴可由光路主动召回。" : ""),
                    "The circuit links the east gate and relays A/B/C. Travel requires a clear floor and never banks loot." +
                        (CompanionHoldingPosition ? " A stationed ally can be recalled on demand." : ""),
                    relayChoices.ToArray());
                return;
            }
            // The first real attempt claims guidance, even on a mistake.
            // Re-evaluate each step from authoritative existing floor flags,
            // not a locally cached or persisted target coordinate.
            abyssCircuitPuzzleGuideActive = true;
            int progress = AbyssCircuitProgress;
            int expected = AbyssCircuitTarget(progress);
            if (AbyssCircuitVisited(node))
            {
                Notify("这处信标已经接入回路。下一处为 " +
                    AbyssCircuitSymbol(expected) + "。",
                    "This relay is already connected. Seek node " +
                    AbyssCircuitSymbol(expected) + " next.");
                return;
            }
            if (node != expected)
            {
                for (int i = 0; i < 3; i++)
                    save.flags.Remove(AbyssCircuitStepFlag(i));
                save.energy = Math.Max(0, save.energy - 12);
                effects.Add(new Effect("spark", new Point(o.x, o.y), .45f));
                // In the deep Abyss a miswired ancient relay releases a
                // REAL delayed warning, with a full visible 1.6-second dodge
                // window. The contact goes through TickExpansion/Hurt's
                // terrain, difficulty, invulnerability and riposte rules.
                // Early tutorial floors retain their original energy-only
                // consequence; a successful relay never creates this hazard.
                bool backlash = save.abyssFloor >= 3 &&
                    !Has("abyss_clear_" + save.abyssFloor);
                if (backlash)
                    AddHazard(new Point(o.x,o.y), 1.35f,
                        1.6f + AbyssArchiveWarningLead,
                        12 + save.abyssRisk * 3, "rift");
                revision++;
                Notify("接线顺序错误：回路断开，损失12耐力。" +
                    (backlash ? "地脉反冲即将爆发，快离开预警圈！" : "") +
                    "重新从 " + AbyssCircuitSymbol(AbyssCircuitTarget(0)) + " 开始。",
                    "Wrong order: circuit reset, -12 energy." +
                    (backlash ? " A Rift backlash is charging—dodge the marked zone!" : "") +
                    " Restart at " + AbyssCircuitSymbol(AbyssCircuitTarget(0)) + ".");
                return;
            }
            Flag(AbyssCircuitStepFlag(progress));
            effects.Add(new Effect("spark", new Point(o.x, o.y), .6f));
            sound = "reward";
            if (progress < 2)
            {
                Notify("星根回路 " + (progress + 1) + "/3 已接通。下一信标：" +
                    AbyssCircuitSymbol(AbyssCircuitTarget(progress + 1)) + "。",
                    "Starroot circuit " + (progress + 1) +
                    "/3 connected. Next relay: " +
                    AbyssCircuitSymbol(AbyssCircuitTarget(progress + 1)) + ".");
                return;
            }
            Flag(AbyssCircuitFinishFlag);
            // Same saturating, UNBANKED balances as ordinary side galleries.
            // Death or extraction still discards rewards until floor banking.
            save.runCoins = CappedReward(save.runCoins, 40, 9999);
            save.runShards = CappedReward(save.runShards, 4, 9999);
            save.runXp = CappedReward(save.runXp, 30, 9999);
            revision++;
            Notify("三处星根回路恢复了巡路灯！本层待结算：硬币40、碎片4、经验30。",
                "Three starroot relays light the road! Unbanked: 40 coins, 4 shards, 30 XP.");
        }

        // A complete five-site roadwarden testimony trail rewards actual
        // field investigation; existing per-floor v2 flags own all state.
        public int AbyssTestimonyProgress
        {
            get
            {
                if (!InAbyss) return 0;
                string suffix = save.abyssFloor.ToString();
                string[] proofKinds = {
                    "abyss_annex_survey_", "abyss_annex_cache_",
                    "abyss_annex_far_survey_", "abyss_annex_far_cache_",
                    "abyss_annex_far_echo_"
                };
                int count = 0;
                foreach (string prefix in proofKinds)
                    if (Has(prefix + suffix)) count++;
                return count;
            }
        }
        // Dynamic in-world route guidance. Choose the closest UNSOLVED
        // physically placed record rather than inventing a map waypoint.
        // The echo stone is withheld until its source tablet was found.
        // Real map objects expose an observable field-record state:
        // 0 = not a testimony, 1 = still available, 2 = collected, and
        // 3 = the echo stone requires its southern source record first.
        // Keeping this derived from v2 proof flags avoids stale UI caches.
        public int AbyssTestimonyRecordState(WorldObject site)
        {
            if (!InAbyss || site == null || Map.objects == null) return 0;
            switch (site.kind)
            {
                case "abyssSurvey":
                case "abyssCache":
                case "abyssFarSurvey":
                case "abyssFarCache":
                case "abyssFarEcho": break;
                default: return 0;
            }
            string flag = AbyssCircuitOptionalFlag(site);
            if (flag == null) return 0;
            if (Has(flag)) return 2;
            if (site.kind == "abyssFarEcho" &&
                !Has("abyss_annex_far_survey_" + save.abyssFloor))
                return 3;
            return 1;
        }
        public WorldObject AbyssTestimonyNextSite
        {
            get
            {
                if (!InAbyss || Map.objects == null ||
                    AbyssTestimonySealed) return null;
                WorldObject closest = null;
                float nearest = float.MaxValue;
                foreach (WorldObject candidate in Map.objects)
                {
                    switch (candidate.kind)
                    {
                        case "abyssSurvey":
                        case "abyssCache":
                        case "abyssFarSurvey":
                        case "abyssFarCache":
                        case "abyssFarEcho": break;
                        default: continue;
                    }
                    if (candidate.kind == "abyssFarEcho" &&
                        !Has("abyss_annex_far_survey_" + save.abyssFloor))
                        continue;
                    string flag = AbyssCircuitOptionalFlag(candidate);
                    if (flag == null || Has(flag)) continue;
                    float dx = candidate.x-save.x, dy = candidate.y-save.y;
                    float range2 = dx*dx + dy*dy;
                    if (range2 < nearest)
                    {
                        nearest=range2;
                        closest=candidate;
                    }
                }
                return closest;
            }
        }
        public int AbyssTestimonyFloorsFiled
        {
            get
            {
                if (!InAbyss) return 0;
                int filed = 0;
                for (int floor = 1; floor <= 6; floor++)
                    if (Has("abyss_annex_testimony_complete_" + floor))
                        filed++;
                return filed;
            }
        }
        public bool AbyssTestimonyLegacyComplete
        {
            get { return Has("abyss_testimony_codex_complete"); }
        }
        // Permanent benefit of completing all six field ledgers: the hero
        // recognizes old root-rift patterns earlier. Only REAL rift warning
        // time changes; no damage, area, spawn interval, or other hazards
        // receive a free reduction. No new SaveData v2 field is introduced.
        public bool AbyssArchiveForesight
        {
            get { return InAbyss && AbyssTestimonyLegacyComplete; }
        }
        private float AbyssArchiveWarningLead
        {
            get { return AbyssArchiveForesight ? .4f : 0f; }
        }


        public bool AbyssTestimonySealed
        {
            get { return InAbyss &&
                Has("abyss_annex_testimony_complete_" + save.abyssFloor); }
        }
        private bool TrySealAbyssTestimony()
        {
            if (!InAbyss || Dying || AbyssTestimonySealed ||
                AbyssTestimonyProgress != 5) return false;
            Flag("abyss_annex_testimony_complete_" + save.abyssFloor);
            // Pending only: all gains require the old gate's BankFloor().
            // Avoid bypassing the normal cap on v2 run counters.
            save.runCoins = CappedReward(save.runCoins, 30, 9999);
            save.runShards = CappedReward(save.runShards, 2, 9999);
            save.runXp = CappedReward(save.runXp, 25, 9999);
            revision++;
            sound = "reward";
            return true;
        }

        // Extra seeded galleries are optional. All loot is pending until the
        // original root gate banks the floor; exiting or dying loses it.
        private void TalkAbyssFarGallery(WorldObject o)
        {
            if(!InAbyss||Dying||practicing||
                !EnsurePhysicalInteractionReach(o))return;
            bool survey=o.kind=="abyssFarSurvey";
            bool cache=o.kind=="abyssFarCache";
            bool echo=o.kind=="abyssFarEcho";
            string proof="abyss_annex_far_survey_"+save.abyssFloor;
            if(echo&&!Has(proof))
            {
                Dialog(o.name,o.en,
                    "回声石只重复破碎的呼声。先去南侧发现真正留下这段话的人。",
                    "The echo stone repeats only fragments. Find the original testimony to the south first.",
                    C("继续探查","Seek the testimony","close"));
                return;
            }
            string flag="abyss_annex_far_"+(survey?"survey_":cache?"cache_":"echo_")+save.abyssFloor;
            bool first=!Has(flag);
            if(first)
            {
                Flag(flag);
                // The lower gallery shares the same v2 unbanked ceilings
                // as ordinary enemy defeats and near-side annex discoveries.
                // Saturate BEFORE an autosave so deep expedition progress is
                // never truncated or wraps when this floor is resumed.
                if (survey)
                    save.runXp = CappedReward(save.runXp, 25, 9999);
                else if (cache)
                {
                    save.runCoins = CappedReward(save.runCoins, 30, 9999);
                    save.runShards = CappedReward(save.runShards, 1, 9999);
                }
                else save.runXp = CappedReward(save.runXp, 20, 9999);
                revision++;sound="reward";
            }
            bool trailSealed = first && TrySealAbyssTestimony();
            string zh=survey?
                "这块地脉残碑留下六次撤离的真实坐标。星根并不只供一面城墙发光，它也曾为困在地下的普通人保留回家的路。":
                cache?
                "巡路者在洞底封存的药材和原料，直到今天才重新看见微光。它们依旧属于本层未结算的远征成果。":
                "回声石终于接上地脉残碑的完整证词：原来的地下通道是为分享能源而修，而不是为封锁彼此而生。";
            string en=survey?
                "A lower root tablet records six evacuation bearings. Starroot once guided ordinary people out of the mines, not merely powered a wall.":
                cache?
                "Supplies sealed in this bottom gallery are still unbanked expedition loot until the original root gate settles the floor.":
                "The echo stone completes the testimony: these passageways were built to share energy, not close communities off from one another.";
            var recordChoices = new List<Choice> {
                C("收下记录", "Keep the account", "close")
            };
            if (ReachableAbyssTestimonySite() != null)
                recordChoices.Add(C("继续追寻下一份证词",
                    "Follow the next warden account", "abyssTestimonyFollow"));
            if (AbyssAvailableTestimonyRoutes > 1)
                recordChoices.Add(C("选择具体证词地点",
                    "Choose a specific testimony site", "abyssTestimonyList"));
            Dialog(o.name,o.en,zh+(first?
                    "\n\n此层待结算："+(cache?"硬币 +30、碎片 +1。":survey?"经验 +25。":"经验 +20。"):
                    "\n\n该层记录已保存，无法重复领取。"),
                en+(first?
                    "\n\nUnbanked: "+(cache?"+30 coins, +1 shard.":survey?"+25 XP.":"+20 XP."):
                    "\n\nRecorded for this floor; no duplicate loot."),
                recordChoices.ToArray());
            if (trailSealed)
                Notify("五份巡路证词已归档！额外待结算：硬币30、碎片2、经验25。",
                    "All five roadwarden accounts filed! Bonus unbanked: 30 coins, 2 shards, 25 XP.");
        }
    }
}
