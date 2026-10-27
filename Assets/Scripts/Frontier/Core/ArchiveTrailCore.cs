using System;

namespace Starfall
{
    // A distinct story expedition spanning the original three campaign regions.
    // Existing save flags own progress; the atlas never bypasses story gates.
    public sealed partial class GameCore
    {
        private bool TalkArchiveTrail(WorldObject o)
        {
            if (o.kind == "archivekeeper")
            {
                if(!PhysicalRefugeReach(new Point(o.x,o.y),2.2001f))return true;
                if (save.room != 0) return true;
                if (Has("archive_complete"))
                {
                    Dialog(o.name, o.en,
                        "你找回的不是一张藏宝图。那是测绘师为每一个后来者留下的撤离路线。如今公会把它挂在大厅里，提醒人们别把归途当作理所当然。",
                        "It was never a treasure map. The surveyor drew escape routes for the people who would follow. The guild now displays the atlas as a promise of safe passage.",
                        C("翻阅修复好的三段星图", "Read the restored three-part atlas", "archive_interpret"),
                        C("谢谢你，琳", "Thank you, Lin", "close"));
                    return true;
                }
                if (!Has("archive_started"))
                {
                    Dialog(o.name, o.en,
                        "我叫琳，负责修复边境的旧地图。最后一位测绘师留下的星图被拆成了三份：林地的残图、遗迹里的潮刻石板、矿场的测距仪。你愿意帮我找回它们吗？\n\n先去荆棘密林东侧寻找折断的航图，余下线索会写在每件物品上。",
                        "I'm Lin, conservator of the frontier maps. The last surveyor left three records: a torn woodland chart, a tide inscription in the ruins, and a rangefinder in the mine. Find the woodland chart first; each artifact points to the next.",
                        C("接受测绘师委托", "Accept the atlas commission", "archive_accept"),
                        C("以后再来", "Maybe later", "close"));
                    return true;
                }
                if (Has("archive_fragment_3"))
                    Dialog(o.name, o.en,
                        "三份记录齐了。测绘师没有标出黄金，而是标出了被洪水切断的安全路、矿坑坍塌前的撤离坡道，以及穿越林地的夜间方向。让我把它们重新拼起来。",
                        "All three records are here. They chart safe routes through floods, collapsing mines and the night forest, not buried gold. Let me restore the atlas.",
                        C("交付三份记录", "Restore and deliver the atlas", "archive_complete"),
                        C("先核对三份记录", "Compare the three records first", "archive_interpret"),
                        C("稍后再来", "Return later", "close"));
                else Dialog(o.name, o.en,
                    "测绘师的三份记录必须按顺序阅读。下一件藏在" +
                    (!Has("archive_fragment_1") ? "荆棘密林的东侧道路。" :
                     !Has("archive_fragment_2") ? "沉没遗迹西侧的潮刻石板。" :
                     "赤铁矿场东侧的旧测距仪。") +
                    "\n\n琳用铅笔在空白地图上画了一个小圈：‘没看见原件以前，我不会替测绘师补上缺失的路。’",
                    "Read the records in sequence. The next is " +
                    (!Has("archive_fragment_1") ? "east of the Thornwood path." :
                     !Has("archive_fragment_2") ? "beside the western tide-carved stones of the Sunken Ruins." :
                     "at the old rangefinder east of the Rediron mine.") +
                    "\n\nLin circles the blank section in pencil. ‘Until we see the original, I won't invent the missing road.’",
                    C("核对已经找到的证据", "Compare the evidence already recovered", "archive_interpret"),
                    C("查看任务手帐", "Check the journal", "archive_track"),
                    C("我继续寻找", "Keep searching", "close"));
                return true;
            }
            if (!o.kind.StartsWith("archivefragment")) return false;
            if(!PhysicalRefugeReach(new Point(o.x,o.y),2.2001f))return true;
            int region;
            if (!int.TryParse(o.kind.Substring("archivefragment".Length), out region) ||
                region < 1 || region > 3 || save.room != region) return true;
            if (!Has("archive_started"))
            {
                Notify("一份被风沙磨损的旧记录。镇上的修复师也许知道它的来历。",
                    "An old expedition record. A conservator in town might recognize it.");
                return true;
            }
            if (Has("archive_fragment_" + region))
            {
                Dialog(o.name, o.en, "你已把这份记录拓印进手帐。原件不必重复领取。",
                    "You already copied this record into the journal. It cannot be claimed twice.",
                    C("细读已留下的现场拓印", "Read the recovered field rubbing", "archive_review_" + region),
                    C("寻找最近未调查的旧路标", "Find the nearest unsurveyed marker", "archive_pin_" + region),
                    C("比较南侧与东侧两条旧路", "Compare the southern and eastern routes", "archive_routes_" + region),
                    C("收起手帐", "Close journal", "close"));
                return true;
            }
            if (region > 1 && !Has("archive_fragment_" + (region - 1)))
            {
                Dialog(o.name, o.en, "密密麻麻的旧符号缺少参照。先找回上一段航图，才能读懂这里的刻痕。",
                    "The marks need an earlier reference. Find the previous chart before deciphering this record.",
                    C("先调查别处", "Search elsewhere first", "close"));
                return true;
            }
            string[] zh = {
                "航图边缘写着：海水倒灌时，去西边寻找潮刻石板。那上面记着第二段归途。",
                "潮刻石板记载着一条撤离矿场的山路。最后的测距仪被留在赤铁矿场东侧。",
                "测距仪上刻着所有工人的名字，背面写着：不要忘记给后来者留下回家的方向。"
            };
            string[] en = {
                "The chart reads: when the waters rise, seek the tide inscription on the western ruins. It carries the second route.",
                "The inscription charts a retreat from the mine. The final rangefinder lies east of the Rediron workings.",
                "Every worker's name is engraved on the rangefinder. Its reverse reads: leave a way home for those who follow."
            };
            Dialog(o.name, o.en, zh[region - 1], en[region - 1],
                C("拓印并收好记录", "Copy and secure the record", "archive_claim_" + region),
                C("暂时放下", "Leave it for now", "close"));
            return true;
        }
        // A real, optional dialogue investigation at Lin's actual town
        // workbench. Never narrate a fragment that has not been physically
        // collected; reopened pages retain Lin as their living speaker.
        // No new rewards, flags, teleports, or save schema changes.
        private void InterpretRecoveredAtlas(WorldObject lin)
        {
            string zh,en;
            if(Has("archive_complete"))
            {
                zh="琳把修复好的星图摊在桌上，三种墨迹在灯下呈现出不同的颜色。\n\n“密林的巡路人记了夜间方位；遗迹的渡工留下涨潮时的绕行线；矿工把逃生坡道和同伴的名字刻进了测距仪。”\n\n她没有把这些路涂成一条漂亮的直线。\n\n“那样会抹掉走过这些路的人。现在每段记录都能由后来者拿着原件核对。”";
                en="Lin spreads the restored atlas across the workbench. Three inks catch the lamplight.\n\n‘The forest surveyor recorded bearings for travel after dark. The ferrymen left a detour for high tide. The miners carved an escape slope and the names of those who used it into their rangefinder.’\n\nShe refuses to turn them into one neat line. ‘That would erase the people who walked them. Now anyone can check each route against the record itself.’";
            }
            else if(Has("archive_fragment_3"))
            {
                zh="琳在三份拓印之间拉起细绳。\n\n“林地指向水路，水路指向矿场。这不是藏宝图，是一份接力传下来的撤离手册。”\n\n她把还没装订的纸压在玻璃下。\n\n“最后一步得把原始出处写清楚。交给我归档，别让后来的人把它读成另一场寻宝。”";
                en="Lin runs a thread between the three rubbings.\n\n‘The forest points toward the water; the water points toward the mine. This isn't a treasure map. It's an evacuation manual passed from one crew to the next.’\n\nShe weighs down the loose pages beneath glass. ‘We still need to credit the original hands. Let me archive them before someone mistakes this for a hunt for gold.’";
            }
            else if(Has("archive_fragment_2"))
            {
                zh="琳指着潮刻石板的拓印，避开上面一道被海盐侵蚀的裂缝。\n\n“渡工记录的不是船能开多快，而是潮水涨起来以后，人该从哪条山路撤走。那条线通向赤铁矿场东侧。”\n\n她在地图的末端留下一格空白。\n\n“最后一份测距仪还没有找到。矿工到底写了谁的名字，我现在不能替他们回答。”";
                en="Lin traces the tide inscription, avoiding a seam eaten away by salt.\n\n‘The ferrymen weren't recording speed. They marked where people could climb when the water rose. That line goes east of the Rediron mine.’\n\nShe leaves the last space blank. ‘We haven't found the rangefinder. I can't tell you whose names the miners wrote until we read them.’";
            }
            else if(Has("archive_fragment_1"))
            {
                zh="琳将密林残图转到灯下，图角露出被水浸过的旧标记。\n\n“你找到的是夜间撤离的起点。这里写着：涨潮时转向西侧遗迹，寻找潮刻石板。”\n\n她没替第二段补线。\n\n“过去的地图最容易把危险画成一条捷径。我们先把那块石板找回来，再决定水路通向哪儿。”";
                en="Lin turns the forest scrap toward the light. An old water stain runs across its edge.\n\n‘You've found the starting bearings for a night evacuation. It says to head toward the western ruins at high tide and look for the tide inscription.’\n\nShe won't draw the next line from memory. ‘Maps are very good at making danger look like a shortcut. Let's find that stone before deciding where the crossing leads.’";
            }
            else
            {
                zh="琳敲了敲地图东侧的空白。\n\n“有人说测绘师把宝藏藏在森林。可最后一封信只问了一件事：下雨以后，走不动的人怎么回家？”\n\n她收起铅笔。\n\n“先去荆棘密林东侧把残图找回来。没有原件，我不打算编一个能让人迷路的答案。”";
                en="Lin taps the empty eastern edge of the map.\n\n‘People say the surveyor buried treasure in the woods. But the last letter asked something else: how does someone who can't walk fast get home after the rain?’\n\nShe puts down her pencil. ‘Bring me the scrap east of Thornwood first. Without the original, I won't invent an answer that could lead somebody astray.’";
            }
            Dialog(lin.name,lin.en,zh,en,
                C("回到琳的委托", "Back to Lin's commission", "archive_return"),
                C("合上记录", "Close the record", "close"));
        }
        // Optional, truly walked atlas survey: completing a specific
        // ORIGINAL waytrail may hand off to the other unvisited marker.
        // This is runtime-only and owned by a deliberately selected personal
        // pin. Manual pins, journal navigation and region transitions cancel.
        private string atlasSurveyRoadId;
        private WorldObject atlasSurveyMarker;
        private int atlasSurveyRoom=-1;
        private MapData atlasSurveyMap;
        private bool atlasSurveyArrived;
        private void ResetAtlasSurvey()
        {
            atlasSurveyRoadId=null;atlasSurveyMarker=null;
            atlasSurveyRoom=-1;atlasSurveyMap=null;atlasSurveyArrived=false;
        }
        private void OwnAtlasSurvey(WorldObject marker)
        {
            atlasSurveyRoadId=marker.id;atlasSurveyMarker=marker;
            atlasSurveyRoom=save.room;
            atlasSurveyMap=Map;atlasSurveyArrived=false;
        }
        private bool AtlasSurveyAtDestination()
        {
            return !string.IsNullOrEmpty(atlasSurveyRoadId) &&
                atlasSurveyMarker!=null &&
                atlasSurveyRoom==save.room &&
                Object.ReferenceEquals(atlasSurveyMap,Map) &&
                atlasSurveyMarker.kind=="waytrail" &&
                atlasSurveyMarker.id==atlasSurveyRoadId &&
                SourceBelongsToActiveMap(atlasSurveyMarker) &&
                IsObjectPresent(atlasSurveyMarker) &&
                !Has("waytrail_"+atlasSurveyRoadId);
        }
        private void AdvanceAtlasSurvey(WorldObject surveyed)
        {
            if(surveyed==null||string.IsNullOrEmpty(atlasSurveyRoadId)||
               atlasSurveyRoom!=save.room||
               !Object.ReferenceEquals(atlasSurveyMap,Map)||
               surveyed.id!=atlasSurveyRoadId||
               !Object.ReferenceEquals(surveyed,atlasSurveyMarker)||
               !SourceBelongsToActiveMap(surveyed)||
               !Has("waytrail_"+surveyed.id))return;
            string otherSide=surveyed.id.EndsWith("s",StringComparison.Ordinal)?
                "e":"s";
            WorldObject next=AtlasRoadMarker(save.room,otherSide);
            Point foot;
            if(next!=null&&!Has("waytrail_"+next.id)&&
               TryQuestApproachFoot(new Point(next.x,next.y),out foot))
            {
                CommitLocalWaymark(foot,null);
                OwnAtlasSurvey(next);
                Notify("旧路标已实地测绘！星图自动标出本地区另一处未调查的路标。",
                    "Road surveyed! The atlas now points to the other unvisited marker.");
            }
            else
            {
                bool unfinished=next!=null&&!Has("waytrail_"+next.id);
                ResetLocalWaymark();
                revision++;
                Notify(unfinished?
                    "旧路标已实地测绘。另一条旧路目前封闭，先寻找通路。":
                    "本地区两处旧路标都已实地测绘，星图路线完成。",
                    unfinished?
                    "Road surveyed. The other route is blocked; find an open crossing.":
                    "Both local old roads surveyed. Atlas route complete.");
            }
        }
        private WorldObject AtlasFieldSource(int region)
        {
            WorldObject o=pendingDialogueSource;
            if(region<1||region>3||save.room!=region||
               pendingDialogueSourceRoom!=region||
               !Has("archive_fragment_"+region)||
               o==null||o.kind!="archivefragment"+region||
               !SourceBelongsToActiveMap(o)||!IsObjectPresent(o)||
               !HasPhysicalInteractionReach(o))return null;
            return o;
        }
        private WorldObject AtlasRoadMarker(int region,string side)
        {
            if((side!="s"&&side!="e")||Map.objects==null)return null;
            string target="waytrail"+region+side;
            foreach(WorldObject o in Map.objects)
                if(o!=null&&o.kind=="waytrail"&&o.id==target&&
                   IsObjectPresent(o))return o;
            return null;
        }
        private bool ChooseArchiveTrail(string action)
        {
            // An explicit second route requires selecting its real road
            // object on the original physical field page, never a remote
            // inventory flag or a copied speaker identity.
            if(action.StartsWith("archive_routes_",StringComparison.Ordinal))
            {
                int selectorRegion;
                if(!int.TryParse(action.Substring("archive_routes_".Length),
                    out selectorRegion))return true;
                WorldObject page=AtlasFieldSource(selectorRegion);
                if(page==null)return true;
                var choices=new System.Collections.Generic.List<Choice>();
                foreach(string side in new[]{"s","e"})
                {
                    WorldObject marker=AtlasRoadMarker(selectorRegion,side);
                    if(marker==null||Has("waytrail_"+marker.id))continue;
                    choices.Add(side=="s"?
                        C("测绘南侧旧路", "Survey the southern trail",
                            "archive_go_"+selectorRegion+"_s"):
                        C("测绘东侧旧路", "Survey the eastern trail",
                            "archive_go_"+selectorRegion+"_e"));
                }
                bool exhausted=choices.Count==0;
                choices.Add(C("回到现场拓印","Back to the field rubbing",
                    "archive_field_return_"+selectorRegion));
                choices.Add(C("收起星图","Close the atlas","close"));
                Dialog(page.name,page.en,
                    exhausted?
                    "本地区两条旧路都已测绘，地图上保留的是已经有人走过的痕迹。":
                    "拓印将南侧驿路与东侧岔路分别标出。选定一处尚未调查的路标；若道路封闭，原有个人导航不会被覆盖。",
                    exhausted?
                    "Both old roads here have been surveyed; the record preserves their real traces.":
                    "The rubbing shows two distinct roads, south and east. Choose an unvisited marker. A blocked road will not overwrite your existing pin.",
                    choices.ToArray());
                return true;
            }
            if(action.StartsWith("archive_go_",StringComparison.Ordinal))
            {
                string suffix=action.Substring("archive_go_".Length);
                int split=suffix.LastIndexOf('_'),chosenRegion;
                if(split<0||!int.TryParse(suffix.Substring(0,split),
                    out chosenRegion))return true;
                string side=suffix.Substring(split+1);
                if(AtlasFieldSource(chosenRegion)==null)return true;
                WorldObject marker=AtlasRoadMarker(chosenRegion,side);
                if(marker==null||Has("waytrail_"+marker.id))
                {
                    Notify("旧路标已经被记录或不再存在，个人导航没有改变。",
                        "That marker was surveyed or is no longer here; your personal route is unchanged.");
                    return true;
                }
                Point foot;
                if(!TryQuestApproachFoot(new Point(marker.x,marker.y),out foot))
                {
                    Notify("所选旧路暂时无法步行抵达；请解除封锁或另找通路。原有标记会保留。",
                        "The chosen road is unreachable on foot. Find another path; your pin is retained.");
                    return true;
                }
                CommitLocalWaymark(foot,null);
                OwnAtlasSurvey(marker);
                Notify(side=="s"?
                    "南侧旧路已标记。沿步行导航前进，到达后检查路标。":
                    "东侧旧路已标记。沿步行导航前进，到达后检查路标。",
                    side=="s"?
                    "Southern road pinned. Follow the walking route and inspect the marker.":
                    "Eastern road pinned. Follow the walking route and inspect the marker.");
                return true;
            }
            // A recovered field object remains a readable, on-site clue.
            // Reopening it cannot claim a second reward or switch to an
            // imagined remote NPC. The actual touched WorldObject and region
            // must match, including after a back/forward dialogue transition.
            // The surveyor's copied bearings now have a real navigation use.
            // From the physical record, choose the nearest still-unmapped
            // old road marker by the player's ACTUAL collision-aware walking
            // route. This sets only an existing transient local waypoint:
            // no teleport, story gate, route completion flag, money or save
            // schema changes. Failed navigation never overwrites a manual pin.
            if(action.StartsWith("archive_pin_",StringComparison.Ordinal))
            {
                int pinRegion;
                if(!int.TryParse(action.Substring("archive_pin_".Length),out pinRegion)||
                   pinRegion<1||pinRegion>3)return true;
                WorldObject record=pendingDialogueSource;
                if(record==null||record.kind!="archivefragment"+pinRegion||
                   save.room!=pinRegion||pendingDialogueSourceRoom!=pinRegion||
                   !SourceBelongsToActiveMap(record)||
                   !IsObjectPresent(record)||
                   !HasPhysicalInteractionReach(record)||
                   !Has("archive_fragment_"+pinRegion))return true;
                WorldObject selected=null;
                Point approach=new Point();
                int bestWalk=int.MaxValue;
                float bestNear=float.MaxValue;
                bool anyUnmapped=false;
                foreach(WorldObject marker in Map.objects)
                {
                    if(marker==null||marker.kind!="waytrail"||
                       !IsObjectPresent(marker)||
                       Has("waytrail_"+marker.id))continue;
                    anyUnmapped=true;
                    Point site=new Point(marker.x,marker.y);
                    Point foot;
                    if(!TryQuestApproachFoot(site,out foot))continue;
                    float direct=(site-Position).Length;
                    int walked=NavigationPathFrom(Position,foot,true).Count;
                    if(selected==null||walked<bestWalk||
                       (walked==bestWalk&&direct<bestNear))
                    {
                        selected=marker;
                        approach=foot;
                        bestWalk=walked;
                        bestNear=direct;
                    }
                }
                if(selected!=null)
                {
                    // The waypoint is deliberately distinct from the NPC
                    // interaction point: it is real walkable ground in reach.
                    CommitLocalWaymark(approach,null);
                    OwnAtlasSurvey(selected);
                    Notify("已按旧星图规划通往未测绘路标的步行路线。返回世界跟随导航，再与路标交互。",
                        "The old atlas charts a walking route to an unvisited marker. Follow your pin and inspect it.");
                }
                else if(anyUnmapped)
                    Notify("旧路标仍在，但现有道路无法步行到达。请先解除封锁或另找通路。",
                        "Unvisited markers remain, but no walking route is open. Clear the obstruction or find another way.");
                else
                    Notify("本地区的旧路标都已实地记录，不必重复测绘。",
                        "All old road markers in this region are already surveyed.");
                return true;
            }
            if(action.StartsWith("archive_review_",StringComparison.Ordinal)||
               action.StartsWith("archive_field_return_",StringComparison.Ordinal))
            {
                bool returning=action.StartsWith("archive_field_return_",StringComparison.Ordinal);
                string prefix=returning?"archive_field_return_":"archive_review_";
                int readRegion;
                if(!int.TryParse(action.Substring(prefix.Length),out readRegion)||
                   readRegion<1||readRegion>3)return true;
                WorldObject record=pendingDialogueSource;
                if(record==null||record.kind!="archivefragment"+readRegion||
                   save.room!=readRegion||pendingDialogueSourceRoom!=readRegion||
                   !SourceBelongsToActiveMap(record)||
                   !IsObjectPresent(record)||
                   !HasPhysicalInteractionReach(record)||
                   !Has("archive_fragment_"+readRegion))return true;
                if(returning){TalkArchiveTrail(record);return true;}
                string[] zh={
                    "树叶压在墨迹上留下一道细纹，像是有人走得太急，连晾纸的时间都没有。\n\n残图标出了林地夜行的方向，却在河岸边断掉。笔迹的最后一句是：‘涨水以后，不要带人走旧桥；先去遗迹核对渡口。’\n\n断裂边缘还有一条手推车的浅划痕。测绘师连伤员由谁推车、车轮在哪里会陷进泥里，都替后来的人想过。",
                    "潮刻石板的刻痕在高水位线之上。磨损的数字并非货物吨数，而是一次次撤离时还能通行的台阶。\n\n渡工在末端划了一道箭头，指向赤铁矿场东侧的高坡。它并没有写下矿工的名字，最后那份测距仪才知道。\n\n石板末端还有三道刻痕。渡工把每次失败的渡口也记下来，好让后来者不必重走险路。",
                    "测距仪的镜筒里嵌着纸片，记录的不是金矿方位，而是工人们在矿道塌落之前标出的逃生坡。\n\n名字被刻在仪器背面，像是怕任何人把走过这条路的人忘掉。三段记录可以带回镇上，请琳把出处一并归档。\n\n镜筒外缘另有一道小箭头。它没有指向矿脉，而是指向地表那条真正能让人出去的路。"
                };
                string[] en={
                    "A pressed leaf leaves its trace across the ink, as though the surveyor had no time to dry the paper.\n\nThe scrap gives bearings for a night crossing of the forest, but stops at the riverbank. Its final note reads: 'When the water rises, do not lead anyone across the old bridge. Verify the ferry at the ruins first.'\n\nA shallow wheel mark crosses the torn edge. The surveyor even accounted for who would push the wounded and where a cart would sink into mud.",
                    "The tide marks are carved above the highest waterline. The worn numbers are not cargo weights, but counts of steps still passable during each evacuation.\n\nThe ferrymen left an arrow toward the high slope east of Rediron. They did not record the miners' names; only the missing rangefinder can explain those.\n\nThree more cuts mark crossings that failed. The ferrymen kept the dangerous routes on the record so nobody would have to rediscover them at the cost of another life.",
                    "A paper slip inside the rangefinder charts the escape slope the workers measured before the galleries fell. No gold seams are marked.\n\nThe names are etched on the reverse, so that nobody forgets who walked that road. The three records can now go back to Lin for a properly attributed atlas.\n\nA final arrow on the lens housing points not to a gold seam but to the surface exit the crew had measured for everyone else."
                };
                Dialog(record.name,record.en,zh[readRegion-1],en[readRegion-1],
                    C("返回现场记录","Return to the field record","archive_field_return_"+readRegion),
                    C("收起拓印","Put away the rubbing","close"));
                return true;
            }
            if(action=="archive_interpret"||action=="archive_return")
            {
                WorldObject lin=pendingDialogueSource;
                if(save.room!=0||lin==null||lin.kind!="archivekeeper"||
                    pendingDialogueSourceRoom!=0||
                    !SourceBelongsToActiveMap(lin)||
                    !HasPhysicalInteractionReach(lin)||
                    !Has("archive_started"))return true;
                if(action=="archive_interpret")InterpretRecoveredAtlas(lin);
                else TalkArchiveTrail(lin);
                return true;
            }
            if (action == "archive_track")
            {
                if (Has("archive_started")) TrackQuest("archive_trail");
                return true;
            }
            if (action == "archive_accept")
            {
                if (save.room == 0 && !Has("archive_started") && !Dying &&
                    PhysicalRefugeReach(ObjectPoint(0, "archivekeeper", new Point(-100, -100)),2.2001f))
                {
                    Flag("archive_started"); TrackQuest("archive_trail"); revision++;
                    Notify("新支线：失落测绘师的星图。先去密林寻找残图。",
                        "New quest: The Lost Surveyor's Atlas. Find the woodland chart.");
                }
                return true;
            }
            if (action == "archive_complete")
            {
                if (save.room != 0 || !Has("archive_started") || Has("archive_complete") ||
                    !Has("archive_fragment_1") || !Has("archive_fragment_2") ||
                    !Has("archive_fragment_3") || Dying ||
                    !PhysicalRefugeReach(ObjectPoint(0, "archivekeeper", new Point(-100, -100)),2.2001f)) return true;
                Flag("archive_complete"); CreditCoins(60); CreditSkillPoints(1);
                AddXp(60); revision++; sound = "reward";
                RecordMoment("失落测绘师的星图已修复：获得60硬币、1技能点。归途的秘密不是宝藏，而是活下来的路。",
                    "The lost atlas is restored: +60 coins, +1 talent point. Its treasure was the road home.");
                Notify("星图已归档！获得60硬币、1技能点、60经验。",
                    "Atlas restored! +60 coins, +1 talent point, +60 XP.");
                return true;
            }
            if (!action.StartsWith("archive_claim_")) return false;
            int region;
            if (!int.TryParse(action.Substring("archive_claim_".Length), out region) ||
                region < 1 || region > 3 || save.room != region || Dying ||
                !Has("archive_started") || Has("archive_fragment_" + region) ||
                (region > 1 && !Has("archive_fragment_" + (region - 1)))) return true;
            WorldObject artifact = null;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "archivefragment" + region) { artifact = o; break; }
            if (artifact == null || !IsObjectPresent(artifact) ||
                !PhysicalRefugeReach(new Point(artifact.x, artifact.y),2.2001f))
                return true;
            Flag("archive_fragment_" + region); CreditCoins(20); CreditShards(2);
            AddXp(20); revision++; sound = "reward";
            effects.Add(new Effect("discover", new Point(artifact.x, artifact.y), .7f));
            RecordMoment(region == 1 ? "林地残图指出遗迹里的潮刻石板。" :
                region == 2 ? "潮刻石板指向赤铁矿场的旧测距仪。" :
                "测距仪留下了矿工们的名字和回家的方向。",
                region == 1 ? "The woodland chart points to the tide inscription." :
                region == 2 ? "The tide inscription points to the mine rangefinder." :
                "The rangefinder preserves the workers' names and their route home.");
            Notify("记录已收集！硬币20、碎片2、经验20。",
                "Record secured! +20 coins, +2 shards, +20 XP.");
            return true;
        }

        // The Starroot Counterpoint: an optional, original three-region
        // investigation with distinct lore readings and real enemy defense.
        // Persistent flags are only written once a physical trial is cleared.
        public int RootHarmonyRoom { get; private set; }
        public float RootHarmonyTime { get; private set; }
        private float rootHarmonyPulseTime;
        public bool IsRootHarmonyEnemy(Enemy e)
        { return e != null && e.id >= 88000 && e.id < 88100; }
        private void ResetRootHarmonyTrial()
        {
            enemies.RemoveAll(IsRootHarmonyEnemy);
            projectiles.RemoveAll(p => p.rootHarmonyOwned);
            hazards.RemoveAll(h => h.rootHarmonyOwned);
            effects.RemoveAll(e => e.rootHarmonyOwned);
            RootHarmonyRoom = 0; RootHarmonyTime = 0;
            rootHarmonyPulseTime = 0;
        }
        public bool RootHarmonyRestoring
        {
            get {
                return save.room >= 1 && save.room <= 3 &&
                    RootHarmonyRoom == 0 && !Dying &&
                    Has("root_harmony_" + save.room) &&
                    (Position - ObjectPoint(save.room, "rootcounter" + save.room,
                        new Point(-100, -100))).Length < 2.75f && SafeTravel;
            }
        }
        private void TickRootHarmonyTrial(float dt)
        {
            if (RootHarmonyRestoring)
            {
                // A calibrated stone remains a tangible world reward.
                // Its short-radius refuge cannot out-heal a nearby battle
                // or silently supply money, items or infinite XP.
                save.health = Math.Min(save.maxHealth, save.health + dt * 1.6f);
                save.energy = Math.Min(100, save.energy + dt * 6f);
            }
            if (RootHarmonyRoom == 0) return;
            if (save.room != RootHarmonyRoom || Dying || practicing)
            { ResetRootHarmonyTrial(); return; }
            RootHarmonyTime -= dt;
            if (enemies.Exists(IsRootHarmonyEnemy))
            {
                rootHarmonyPulseTime -= dt;
                if (rootHarmonyPulseTime <= 0)
                {
                    // Each listening stone has a different hostile pulse
                    // rhythm. It targets the player's actual current feet
                    // with an ordinary world-space delayed hazard, never a
                    // scripted instant hit or opaque screen overlay.
                    Point[] cardinal = { new Point(1, 0), new Point(-1, 0),
                        new Point(0, 1), new Point(0, -1) };
                    bool escapable = false;
                    foreach (Point direction in cardinal)
                    {
                        Point escape = Position + direction * 1.7f;
                        if (CanStand(escape) && ClearAttackPath(Position, escape))
                        { escapable = true; break; }
                    }
                    if (escapable)
                        hazards.Add(new Hazard {
                            position = Position,
                            radius = RootHarmonyRoom == 3 ? 1.45f : 1.15f,
                            damage = RootHarmonyRoom == 3 ? 16f : 10f + RootHarmonyRoom,
                            timer = RootHarmonyRoom == 1 ? 1.2f : 1.4f,
                            kind = RootHarmonyRoom == 2 ? "tide" : "rift",
                            rootHarmonyOwned = true
                        });
                    rootHarmonyPulseTime = RootHarmonyRoom == 1 ? 6.2f :
                        RootHarmonyRoom == 2 ? 5.7f : 5f;
                }
            }
            else
                hazards.RemoveAll(h => h.rootHarmonyOwned);
            if (RootHarmonyTime <= 0)
            {
                ResetRootHarmonyTrial(); revision++;
                Notify("失谐守卫夺回了共鸣石。线索仍在，可以重新调谐。",
                    "The discordant sentinels reclaimed the node. Try tuning again.");
            }
        }
        private int RootHarmonyNext()
        {
            for (int i = 1; i <= 3; i++)
                if (!Has("root_harmony_" + i)) return i;
            return 0;
        }
        private bool TalkRootHarmony(WorldObject o)
        {
            if (o.kind == "rootlistener")
            {
                if(!PhysicalRefugeReach(new Point(o.x,o.y),2.2001f))return true;
                if (save.room != 0) return true;
                if (!Has("scout_saved"))
                {
                    Dialog(o.name, o.en,
                        "密林仍有失踪的巡路员。先去把他救回来，我才敢让你靠近地下的失谐节点。",
                        "A roadwarden remains lost in the forest. Bring him home before approaching the unstable nodes.",
                        C("我先去密林", "I'll go to Thornwood", "close"));
                    return true;
                }
                if (!Has("root_harmony_started"))
                {
                    Dialog(o.name, o.en,
                        "我是芮，负责倾听星根而非抽走它。密林的根音、遗迹的潮音和矿场的铁音已经失去节拍。九年前的调谐命令把它们拆散，星陨只是最后一声巨响。\n\n请听三处共鸣石的证词。调对音节后，失谐守卫会醒来；必须真正击退它们才能安全封存读数。",
                        "I'm Rui. I listen to starroot instead of harvesting it. Root-song in Thornwood, tidal song in the ruins and iron-song in the mine have fallen out of time. A nine-year-old tuning order divided them. Starfall was only the final crack.\n\nListen to three sites. Choosing the right note awakens dissonant sentinels. Defeat them before sealing the reading.",
                        C("接取星根失谐调查", "Investigate the Starroot Counterpoint", "root_harmony_accept"),
                        C("稍后再来", "Later", "close"));
                    return true;
                }
                if (Has("root_harmony_complete"))
                {
                    Dialog(o.name, o.en,
                        "三道音纹合成了第三条路：不再要求一座城市替另一座城市耗尽河流。你把星根的节拍还给了能听见它的人。",
                        "Three readings reveal a third path: no city need drain another city's river. You returned the rhythm to those who can hear it.",
                        C("记住和声", "Remember the counterpoint", "close"));
                    return true;
                }
                if (RootHarmonyNext() == 0)
                {
                    Dialog(o.name, o.en,
                        "根、潮、铁终于能互相听见。它们不是堡垒的燃料，而是三地共用的脉搏。把最后的读数归档吧。",
                        "Root, tide and iron can finally hear one another. They were never fuel for a wall. Archive the complete reading.",
                        C("归档星根和声", "Archive the starroot harmony", "root_harmony_finish"),
                        C("稍后", "Later", "close"));
                    return true;
                }
                int next = RootHarmonyNext();
                Dialog(o.name, o.en,
                    "下一段失谐读数在" + new[] { "", "荆棘密林", "沉没遗迹", "赤铁矿场" }[next] + "。每段唱片都记着真正的地貌：根靠泥土，潮随水道，铁与矿脉共振。错误音节会耗损你的耐力。",
                    "The next reading is in " + new[] { "", "Thornwood", "the Drowned Ruins", "Ironreach" }[next] +
                        ". Root belongs to soil, tide to water and iron to ore. False notes sap energy.",
                    C("追踪失谐读数", "Track the dissonance", "root_harmony_track"),
                    C("继续探索", "Continue exploring", "close"));
                return true;
            }
            if (!o.kind.StartsWith("rootcounter")) return false;
            if(!PhysicalRefugeReach(new Point(o.x,o.y),2.2001f))return true;
            int region;
            if (!int.TryParse(o.kind.Substring("rootcounter".Length), out region) ||
                region < 1 || region > 3 || save.room != region) return true;
            if (!Has("root_harmony_started"))
            { Notify("星根石仍在低鸣。余烬镇的听纹师可能读得懂它。", "The stone hums. Rui in Emberfall may understand it."); return true; }
            if (Has("root_harmony_" + region))
            {
                Dialog(o.name, o.en, "音纹已经归档。周围安全时，这块石头会短暂恢复你失去的生命与耐力。敌人接近便会中断恢复。", "The reading is sealed. In safety this stone restores health and energy nearby; approaching enemies interrupt the respite.",
                    C("离开", "Leave", "close")); return true;
            }
            if (RootHarmonyNext() != region)
            {
                Dialog(o.name, o.en, "缺少前一段节拍，石面上的音纹无法稳定。", "The earlier reading is missing. This node cannot hold its beat.",
                    C("返回调查", "Return to the investigation", "close")); return true;
            }
            if (RootHarmonyRoom == region)
            {
                if (enemies.Exists(IsRootHarmonyEnemy))
                    Dialog(o.name, o.en, "失谐守卫正在攻击。先击败真正的守卫，再封存本地读数。余下 " +
                        (int)Math.Ceiling(RootHarmonyTime) + " 秒。",
                        "Dissonant sentinels are attacking. Defeat them before sealing the reading. " +
                        (int)Math.Ceiling(RootHarmonyTime) + " seconds left.",
                        C("投入战斗", "Defend the node", "close"),
                        C("安全中止调谐", "Withdraw and reset", "root_harmony_abandon_" + region));
                else Dialog(o.name, o.en,
                    "守卫已沉默。你听清了这段音纹，封存它，免得星根再次被迫共振。",
                    "The sentinels are silent. Seal this reading before dissonance returns.",
                    C("封存调谐读数", "Seal the tuned reading", "root_harmony_seal_" + region),
                    C("稍后", "Later", "close"));
                return true;
            }
            if (RootHarmonyRoom != 0)
            { Notify("另一处共鸣石正在调谐。", "Another harmonic trial is active."); return true; }
            string[] zh = {
                "泥下的星根不会说谎。它用湿土的呼吸而不是铜管的回音报出伤口。",
                "被改写的闸门仍记得涨潮的方向，水底的声音在潮汐停歇时最清晰。",
                "矿井深处，铁轨上的锤音与原始矿脉的搏动一致，机器却忘了停下。"
            };
            string[] en = {
                "Under soil, starroot speaks through earth rather than brass.",
                "The ruined sluice still remembers which way the tide once ran.",
                "Beneath the mine, the hammer on iron follows the ore's original pulse."
            };
            Dialog(o.name, o.en,
                zh[region - 1] + "\n\n哪一段音纹属于这里？选对后需守住40秒内的真实战斗。",
                en[region - 1] + "\n\nWhich note belongs here? Tune correctly, then defeat the sentinels before forty seconds elapse.",
                C("潮音：水道", "Tide: waterways", "root_harmony_tune_" + region + "_0"),
                C("铁音：矿脉", "Iron: ore veins", "root_harmony_tune_" + region + "_1"),
                C("根音：土壤", "Root: living soil", "root_harmony_tune_" + region + "_2"),
                C("暂不调谐", "Not yet", "close"));
            return true;
        }
        private bool ChooseRootHarmony(string action)
        {
            if (action == "root_harmony_track")
            { if (Has("root_harmony_started")) TrackQuest("root_harmony"); return true; }
            if (action == "root_harmony_accept")
            {
                if (save.room == 0 && Has("scout_saved") && !Has("root_harmony_started") &&
                    PhysicalRefugeReach(ObjectPoint(0, "rootlistener", new Point(-100, -100)),2.2001f))
                {
                    Flag("root_harmony_started"); TrackQuest("root_harmony");
                    Notify("新任务：星根失谐。去荆棘密林寻找第一处音纹。",
                        "New quest: Starroot Counterpoint. Find the first listening stone in Thornwood.");
                }
                return true;
            }
            if (action == "root_harmony_finish")
            {
                if (save.room == 0 && Has("root_harmony_started") &&
                    !Has("root_harmony_complete") && RootHarmonyNext() == 0 &&
                    PhysicalRefugeReach(ObjectPoint(0, "rootlistener", new Point(-100, -100)),2.2001f))
                {
                    Flag("root_harmony_complete"); CreditCoins(75); CreditSkillPoints(1);
                    AddXp(70); revision++; sound = "reward";
                    RecordMoment("星根失谐调查已归档：根、潮、铁不再被迫为堡垒独奏。",
                        "Starroot Counterpoint archived: root, tide and iron no longer play for the citadel alone.");
                    Notify("三段音纹已归档！获得75硬币、1技能点与70经验。",
                        "Three readings archived! +75 coins, +1 talent and +70 XP.");
                }
                return true;
            }
            if (action.StartsWith("root_harmony_abandon_"))
            {
                int room;
                if (!int.TryParse(action.Substring("root_harmony_abandon_".Length), out room) ||
                    room < 1 || room > 3) return true;
                Point site = ObjectPoint(room, "rootcounter" + room, new Point(-100, -100));
                if (RootHarmonyRoom == room && save.room == room && !Dying &&
                    PhysicalRefugeReach(site,2.2001f))
                {
                    ResetRootHarmonyTrial(); revision++; sound = "quest";
                    Notify("已安全终止调谐。守卫与残留攻击已清除，可重新尝试。",
                        "Tuning withdrawn. Sentinels and encounter attacks retired; retry when ready.");
                }
                return true;
            }
            if (action.StartsWith("root_harmony_tune_"))
            {
                string[] pieces = action.Substring("root_harmony_tune_".Length).Split('_');
                int room, note;
                if (pieces.Length != 2 || !int.TryParse(pieces[0], out room) ||
                    !int.TryParse(pieces[1], out note) || note < 0 || note > 2 ||
                    room < 1 || room > 3) return true;
                Point site = ObjectPoint(room, "rootcounter" + room, new Point(-100, -100));
                if (save.room != room || !Has("root_harmony_started") ||
                    RootHarmonyNext() != room || RootHarmonyRoom != 0 ||
                    !PhysicalRefugeReach(site,2.2001f) || Dying || practicing ||
                    FieldWorkPreventsRest) return true;
                int answer = room == 1 ? 2 : room == 2 ? 0 : 1;
                if (note != answer)
                {
                    save.energy = Math.Max(0, save.energy - 8);
                    effects.Add(new Effect("danger", site, .6f) { radius = .65f });
                    sound = "deny"; revision++;
                    Notify("失谐反馈让耐力减少8点。再听听石面与当地地貌的联系。",
                        "Dissonance costs 8 energy. Listen to the landscape, then try again.");
                    return true;
                }
                RootHarmonyRoom = room; RootHarmonyTime = 40;
                rootHarmonyPulseTime = 2.5f;
                for (int i = 0; i < 3; i++)
                {
                    Point spawn = site + new Point(i == 0 ? -3 : i == 1 ? 3 : 0,
                        i == 2 ? 3 : -1);
                    enemies.Add(CreatePlacedEnemy(88000 + room * 10 + i,
                        i == 2 ? 2 : i, spawn, 35 + room * 13));
                }
                effects.Add(new Effect("discover", site, .8f) { rootHarmonyOwned = true });
                sound = "windup"; revision++;
                Notify("音纹调谐成功！真正击退三名失谐守卫，再回来封存读数。40秒倒计时开始。",
                    "Correct resonance! Defeat three real sentinels and seal the reading within 40 seconds.");
                return true;
            }
            if (action.StartsWith("root_harmony_seal_"))
            {
                int room;
                if (!int.TryParse(action.Substring("root_harmony_seal_".Length), out room) ||
                    room < 1 || room > 3) return true;
                Point site = ObjectPoint(room, "rootcounter" + room, new Point(-100, -100));
                if (save.room != room || RootHarmonyRoom != room || RootHarmonyTime <= 0 ||
                    !PhysicalRefugeReach(site,2.2001f) || enemies.Exists(IsRootHarmonyEnemy) ||
                    Has("root_harmony_" + room)) return true;
                ResetRootHarmonyTrial(); Flag("root_harmony_" + room);
                CreditCoins(25); CreditShards(2); AddXp(35);
                sound = "reward"; revision++;
                effects.Add(new Effect("discover", site, .8f));
                RecordMoment("已封存第" + room + "段失谐音纹，失控的星根正在恢复节拍。",
                    "Sealed counterpoint reading " + room + "; starroot is recovering its rhythm.");
                Notify("读数已封存！硬币25、碎片2、经验35。共鸣石附近已可安全休整。",
                    "Reading sealed! +25 coins, +2 shards, +35 XP. The stone now offers a safe respite.");
                return true;
            }
            return false;
        }
    }
}
