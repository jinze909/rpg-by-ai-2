using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private readonly MenuNavigation navigation = new MenuNavigation();
        private readonly List<FocusControl> drawnControls = new List<FocusControl>();
        private string confirmControl, drawnScope, dialogParent, settingsParent = "world", pressedControl;
        private float navigationRepeat, pressedTime;
        private bool focusVisible, controllerUsed, drawingModal;
        private readonly CompanionButtonGesture companionPadGesture = new CompanionButtonGesture();
        private readonly MapTileCursor mapTileCursor = new MapTileCursor();
        // The same bounded tile selector edits two separate tactical orders.
        // A rejected or cancelled edit must never erase the old player pin
        // or companion hold. Neither target is written to SaveData v2.
        private bool mapCursorCompanionPost;
        private void BeginCompanionMapPost()
        {
            if(Game==null||Game.mode!="map"||
                selectedRegion!=Game.save.room||Game.save.companion<0||
                Game.practicing||Game.Dying||Game.OuterPatrolActive)return;
            MapData map=Game.Map;
            if(map==null||map.rows==null||map.rows.Length==0||
                string.IsNullOrEmpty(map.rows[0]))return;
            Point origin=Game.CompanionHoldingPosition?
                Game.CompanionHoldPosition:Game.companionPosition;
            mapTileCursor.Begin(origin,map.rows[0].Length,map.rows.Length);
            mapCursorCompanionPost=mapTileCursor.Active;
            navigationRepeat=0f;
        }
        private string ControlId(float x, float y, float w, float h) { return x.ToString(CultureInfo.InvariantCulture) + ":" + y.ToString(CultureInfo.InvariantCulture) + ":" + w.ToString(CultureInfo.InvariantCulture) + ":" + h.ToString(CultureInfo.InvariantCulture); }
        private string InputScope()
        { return Game.mode + (Game.mode == "dialog" && Game.conversation != null ? "/" + Game.conversation.GetHashCode() + "/" + dialoguePage + "/choices/" + dialogueChoicePage : "") + (replacePrompt ? "/replace" : "") + (saveConfirmation != null ? "/confirm" : ""); }
        private void BeginControls()
        {
            // A choice may open a new speaker/section during the previous
            // OnGUI callback. Reset BEFORE computing the next focus scope:
            // otherwise the new page is drawn with page-zero geometry but
            // an old page's focus identity for its entire first frame.
            if (Game != null && Game.mode == "dialog" && Game.conversation != lastConversation)
            {
                lastConversation = Game.conversation;
                dialoguePage = dialogueChoicePage = 0;
                dialogueHistoryOpen=false;
                confirmControl = null;
            }
            // Never carry an old journal inspection through a different
            // menu, dialogue or scripted transition into a later map view.
            if(Game!=null&&Game.mode!="map")mapInspectedQuestId=null;
            drawnScope = InputScope(); navigation.Scope(drawnScope);
            drawnControls.Clear(); drawingModal = false;
        }
        private void EndControls()
        {
            if (drawnScope != InputScope()) { confirmControl = null; navigation.Scope(InputScope()); return; }
            bool first = navigation.focused == ""; navigation.SetControls(drawnControls);
            if (first && Game.mode == "dialog" && DialogueChoicesVisible() && Game.conversation.choices.Length > 0)
            {
                // Focus the real first choice on THIS page, not a phantom
                // half-width button inferred from the conversation total.
                int visible = DialogueLayout.VisibleChoiceCount(
                    Game.conversation.choices.Length, dialogueChoicePage, DialogueChoicesPerPage);
                if (visible > 0)
                    navigation.Select(ControlId(320, 539,
                        DialogueLayout.ChoiceWidth(visible, 832f, 14f), 37));
            }
        }
        private bool RegisterControl(float x, float y, float w, float h, bool enabled)
        {
            string id = ControlId(x, y, w, h); drawnControls.Add(new FocusControl { id = id, x = x, y = y, w = w, h = h, enabled = enabled });
            if (enabled && confirmControl == id && drawnScope == InputScope()) { confirmControl = null; pressedControl = id; pressedTime = .12f; Game.sound = "ui"; return true; } return false;
        }
        private bool PadDown(int id) { return Input.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + id)); }
        private void UpdateMenuInput(float dt)
        {
            pressedTime = Math.Max(0, pressedTime - dt); navigationRepeat = Math.Max(0, navigationRepeat - dt); confirmControl = null;
            navigation.Scope(InputScope());
            // The same physically earned single-use line brace is accessible
            // on gamepad X, not just by keyboard F or the on-screen button.
            if(Game.mode=="fish"&&PadDown(2))
            {
                controllerUsed=true;
                Game.BraceTidalLine();
                return;
            }
            // Gamepad and pure keyboard players can place the SAME validated
            // personal map pin as mouse users. While a tile is being selected,
            // d-pad/stick movement MUST NOT move menu focus or fire another
            // mapped action. B/Escape cancel; A/Enter confirm precisely once.
            if(mapTileCursor.Active)
            {
                if(Game.mode!="map"||selectedRegion!=Game.save.room)
                {
                    // A scripted room switch invalidates both editors.
                    // Consume this input frame: A/Enter must not fall
                    // through and activate a different scene's menu.
                    mapTileCursor.Cancel();
                    mapCursorCompanionPost=false;
                    return;
                }
                else
                {
                    if(PadDown(0)||PadDown(1)||PadDown(4)||PadDown(5))
                        controllerUsed=true;
                    if(Input.GetKeyDown(KeyCode.Escape)||PadDown(1))
                    {mapTileCursor.Cancel();mapCursorCompanionPost=false;return;}
                    if(Input.GetKeyDown(KeyCode.Return)||
                        Input.GetKeyDown(KeyCode.Space)||PadDown(0))
                    {
                        bool accepted=mapCursorCompanionPost?
                            Game.AssignCompanionHoldPost(mapTileCursor.Destination):
                            Game.PlaceLocalWaymark(mapTileCursor.Destination);
                        if(accepted)
                        {
                            mapTileCursor.Cancel();
                            mapCursorCompanionPost=false;
                        }
                        return;
                    }
                    // Six-tile map jumps prevent 120x80 annex navigation
                    // becoming hundreds of mandatory d-pad taps. Shoulders
                    // scroll vertically; Shift+arrows works without a pad.
                    int jump=Input.GetKey(KeyCode.LeftShift)||
                        Input.GetKey(KeyCode.RightShift)?6:1;
                    int moveX=0,moveY=0;
                    if(Input.GetKeyDown(KeyCode.LeftArrow))moveX-=jump;
                    if(Input.GetKeyDown(KeyCode.RightArrow))moveX+=jump;
                    if(Input.GetKeyDown(KeyCode.UpArrow))moveY-=jump;
                    if(Input.GetKeyDown(KeyCode.DownArrow))moveY+=jump;
                    if(PadDown(4))moveY-=6;
                    if(PadDown(5))moveY+=6;
                    float mapAxisX=Input.GetAxisRaw("Debug Horizontal"),
                        mapAxisY=Input.GetAxisRaw("Debug Vertical");
                    if(Math.Abs(mapAxisX)<.5f&&Math.Abs(mapAxisY)<.5f)
                    {mapAxisX=Input.GetAxisRaw("Horizontal");mapAxisY=Input.GetAxisRaw("Vertical");}
                    // Track a held axis every frame, including debounce
                    // frames; otherwise it never reaches accelerated travel.
                    // Explicit arrow/shoulder presses always keep their own
                    // precise six-tile modifier and never double-step with
                    // the same physical key's Unity axis mapping.
                    int axisX=0,axisY=0;
                    if(Math.Abs(mapAxisX)>.5f||Math.Abs(mapAxisY)>.5f)
                    {
                        if(Math.Abs(mapAxisX)>Math.Abs(mapAxisY))
                            axisX=Math.Sign(mapAxisX);
                        else axisY=-Math.Sign(mapAxisY);
                    }
                    bool direct=moveX!=0||moveY!=0;
                    bool repeat=!direct&&navigationRepeat<=0f;
                    bool axisMoved=mapTileCursor.MoveAxis(axisX,axisY,dt,repeat);
                    if(direct)
                        mapTileCursor.Move(moveX,moveY);
                    if(direct || repeat&&(axisX!=0||axisY!=0))
                        navigationRepeat=.14f;
                    if(direct||axisMoved)Game.sound="ui";
                    return;
                }
            }
            // Map P / gamepad X issues the companion tactical post selector.
            // Unlike the previous field hold command this can choose real
            // terrain several bends away without teleporting the ally.
            if(Game.mode=="map"&&selectedRegion==Game.save.room&&
                (Input.GetKeyDown(KeyCode.P)||PadDown(2)))
            {
                if(PadDown(2))controllerUsed=true;
                BeginCompanionMapPost();
                return;
            }
            // R / gamepad Y repositions an existing pin WITHOUT clearing
            // it first. Canceled or physically blocked edits retain the old
            // destination and its route; only explicit confirm commits.
            if(Game.mode=="map"&&selectedRegion==Game.save.room&&
                (Input.GetKeyDown(KeyCode.R)||PadDown(3)))
            {
                if(PadDown(3))controllerUsed=true;
                MapData current=Game.Map;
                if(current!=null&&current.rows!=null&&
                    current.rows.Length>0&&current.rows[0]!=null)
                {
                    Point start=Game.LocalWaymark.HasValue?
                        Game.LocalWaymark.Value:Game.Position;
                    mapTileCursor.Begin(start,current.rows[0].Length,
                        current.rows.Length);
                    mapCursorCompanionPost=false;
                    navigationRepeat=0f;
                }
                return;
            }
            if (Game.mode == "world") { if (Input.GetKeyDown(KeyCode.Escape)) Back(); return; }
            // History is a modal, read-only reader layered over the original
            // active NPC. It must consume keyboard/pad input BEFORE normal
            // dialogue choice navigation or world interactions see it.
            if(Game.mode=="dialog"&&Input.GetKeyDown(KeyCode.F2))
            { ToggleDialogueHistory(); return; }
            if(Game.mode=="dialog"&&dialogueHistoryOpen)
            {
                if(Input.GetKeyDown(KeyCode.Escape)||
                   Input.GetKeyDown(KeyCode.Backspace)||
                   PadDown(1)||PadDown(0)||
                   Input.GetKeyDown(KeyCode.Return)||
                   Input.GetKeyDown(KeyCode.Space))
                { ToggleDialogueHistory(); return; }
                if(Input.GetKeyDown(KeyCode.PageUp)||PadDown(4))
                { MoveDialogueHistory(-1); return; }
                if(Input.GetKeyDown(KeyCode.PageDown)||PadDown(5))
                { MoveDialogueHistory(1); return; }
                // Consume other dialogue input. Archived text must never
                // perform hidden on-screen menu choices.
                return;
            }
            if (PadDown(0) || PadDown(1) || PadDown(4) || PadDown(5) || PadDown(7)) controllerUsed = true;
            bool controllerBack=PadDown(1);
            if (controllerBack || Input.GetKeyDown(KeyCode.Escape))
            {
                // B is the controller counterpart to Backspace on a
                // multi-card story, not a command to discard the NPC.
                // Escape retains its explicit close/menu-back semantics.
                // Require the page owner to still be this conversation:
                // a newly opened speaker must never inherit old pages.
                if (controllerBack && Game.mode == "dialog" &&
                    Game.conversation != null && Game.conversation == lastConversation &&
                    dialoguePage > 0)
                    PreviousDialoguePage();
                else
                    Back();
                return;
            }
            if (Game.mode == "dialog" && Game.conversation != null &&
                Game.conversation == lastConversation && dialoguePage > 0 &&
                (Input.GetKeyDown(KeyCode.Backspace) ||
                 (!DialogueChoicesVisible() &&
                  (Input.GetKeyDown(KeyCode.PageUp) || PadDown(4)))))
            { PreviousDialoguePage(); return; }
            if (Game.mode == "dialog" && DialogueChoicesVisible() &&
                DialogueChoicePageCount > 1)
            {
                // Shoulder buttons navigate long story pages. In dialog
                // they never command a companion or cycle unrelated menus.
                if (Input.GetKeyDown(KeyCode.PageUp) || PadDown(4))
                { MoveDialogueChoicePage(-1); return; }
                if (Input.GetKeyDown(KeyCode.PageDown) || PadDown(5))
                { MoveDialogueChoicePage(1); return; }
                if (Input.GetKeyDown(KeyCode.Home))
                { MoveDialogueChoicePage(-DialogueChoicePageCount); return; }
                if (Input.GetKeyDown(KeyCode.End))
                { MoveDialogueChoicePage(DialogueChoicePageCount); return; }
            }
            // The help panel has its own real reading pages, independent
            // from LB/RB menu-tab switching and dialogue choice focus.
            if(Game.mode=="help" && Input.GetKeyDown(KeyCode.PageUp))
            { MoveHelpPage(-1); return; }
            if(Game.mode=="help" && Input.GetKeyDown(KeyCode.PageDown))
            { MoveHelpPage(1); return; }
            bool menu = Array.IndexOf(menuModes, Game.mode) >= 0;
            if (menu && (PadDown(4) || PadDown(5))) { int index = Array.IndexOf(menuModes, Game.mode); OpenMenu(menuModes[(index + (PadDown(5) ? 1 : -1) + menuModes.Length) % menuModes.Length]); return; }
            Point direction = new Point();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) direction.x = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) direction.x = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow)) direction.y = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) direction.y = 1;
            float dx = Input.GetAxisRaw("Debug Horizontal"), dy = Input.GetAxisRaw("Debug Vertical");
            if (Math.Abs(dx) < .5f && Math.Abs(dy) < .5f) { dx = Input.GetAxisRaw("Horizontal"); dy = Input.GetAxisRaw("Vertical"); }
            if (navigationRepeat <= 0 && direction.Length < .1f && (Math.Abs(dx) > .5f || Math.Abs(dy) > .5f)) { direction = Math.Abs(dx) > Math.Abs(dy) ? new Point(Math.Sign(dx), 0) : new Point(0, -Math.Sign(dy)); navigationRepeat = .2f; }
            if (direction.Length > .1f) { focusVisible = true; navigation.Move(direction); Game.sound = "ui"; }
            if (Input.GetKeyDown(KeyCode.Tab)) { focusVisible = true; navigation.Cycle(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) && Game.mode != "fish" || PadDown(0))
            { focusVisible = true; if (Game.mode == "dialog" && !DialogueChoicesVisible()) AdvanceDialogue(); else if (Game.mode == "fish" && PadDown(0)) Game.FishHit(); else if (navigation.CanActivate(navigation.focused)) confirmControl = navigation.focused; }
        }
        private void Back()
        {
            confirmControl = null;
            if (saveConfirmation != null) { saveConfirmation = null; return; }
            if (replacePrompt) { replacePrompt = false; return; }
            if (Game.mode == "title") return;
            if (Game.mode == "world") { OpenMenu("pause"); Save(); return; }
            if (Game.mode == "saves") { Game.mode = "pause"; return; }
            if (Game.mode == "pause" && settingsParent == "title") { Game.mode = "title"; settingsParent = "world"; return; }
            if (Game.mode == "dialog" && dialogParent != null) { string parent = dialogParent; dialogParent = null; Game.Close(); OpenMenu(parent); return; }
            if (Game.mode == "dialog" && Game.conversation != null)
            {
                // The chronicle is a real three-level reader. B/Escape backs
                // from a page into its chapter, then into the directory,
                // instead of discarding 15 unlocked choices and exiting the
                // archive. The canonical GameCore.Choose handler still
                // rechecks story gating; no quest or save state is forged.
                int chapter = -1, directory = -1;
                int rootGateBack = -1;
                Choice[] choices = Game.conversation.choices;
                for (int i = 0; i < choices.Length; i++)
                {
                    if (choices[i].action.StartsWith("story_section_"))
                        chapter = i;
                    if (choices[i].action == "story_menu")
                        directory = i;
                    if (choices[i].action == "abyssTestimonyGateMenu" ||
                        choices[i].action == "abyssCircuitGateMenu")
                        rootGateBack = i;
                }
                if (directory >= 0)
                {
                    ChooseDialogue(chapter >= 0 ? chapter : directory);
                    return;
                }
                // Route selection is a real child of the east banking gate.
                // B/Escape navigates back using the SAME physical source
                // checks as clicking its own "Back to bank" option. It
                // must never bank loot or resurrect a stale remote menu.
                if (rootGateBack >= 0)
                {
                    ChooseDialogue(rootGateBack);
                    return;
                }
            }
            Game.Close(); Save();
        }
        private void ChooseDialogue(int i)
        { string parent = dialogParent; Game.Choose(i); if (Game.mode != "dialog") { dialogParent = null; if (Game.mode == "world" && parent != null) OpenMenu(parent); } }
        private string ControlKey(string keyboard, string pad) { return controllerUsed ? pad : keyboard; }
        private string PracticeInputHint
        { get { return controllerUsed ? Game.L(new[] { "X：命中木桩", "观察红圈，A闪避攻击", "Y：命中目标，完成练习" }[Game.practiceStage], new[] { "X: hit the dummy", "Read the red circle; A to dodge", "Y: hit the target to finish" }[Game.practiceStage]) : Game.PracticeHint; } }
        private string MenuInputHint { get { return controllerUsed ? Game.L("摇杆 / 方向键移动 · A确认 · B返回 · LB/RB切换菜单", "Stick / D-pad moves · A selects · B returns · LB/RB switches menus") : Game.L("方向键移动 · Tab切换焦点 · Enter确认 · Esc返回", "Arrows move · Tab cycles focus · Enter selects · Esc returns"); } }
        private string TutorialInputHint(int step)
        {
            if (!controllerUsed) return Game.L(tutorialTextZh[step], tutorialTextEn[step]);
            return Game.L(new[] { "左摇杆移动。实际走过三格后完成。", "靠近居民或发光物按 B。试着和南侧公会的莉雅交谈。", "按 VIEW 打开手帐，选择任务并确认追踪。金色标记会指向下一步。", "右摇杆独立瞄准，按 X 命中敌人，左摇杆仍可移动。空挥不会完成。", "红圈出现后按 A 闪避。成功避开真实攻击才完成。", "用 Y 战技命中目标。通常耗40耐力并有冷却，可在训练场练习。", "按 START，在菜单中选择冒险背包。选择物品或比较武器与打法。", "向北到密林出口，或在菜单中选择世界地图与荆棘密林。找到侦察员。" }[step], new[] { "Move with the left stick. Walk three tiles to finish.", "Approach a resident or glowing object and press B. Try Lyra at the southern guild hall.", "Press VIEW. Select a quest and confirm Track; gold markers show the next step.", "Aim with the right stick while moving with the left; X must hit a real foe.", "Press A to dodge a red warning. Successfully avoiding the attack counts.", "Land Y on a target. It costs 40 energy and has a cooldown. Try the practice yard.", "Press START and choose Inventory. Inspect items or compare weapons and styles.", "Go north to the forest exit, or open World Map and select Thornwood. Find the scout." }[step]);
        }
    }
}
