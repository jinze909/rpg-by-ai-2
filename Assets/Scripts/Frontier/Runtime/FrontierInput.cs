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
                confirmControl = null;
            }
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
            navigation.Scope(InputScope()); if (Game.mode == "world") { if (Input.GetKeyDown(KeyCode.Escape)) Back(); return; }
            if (PadDown(0) || PadDown(1) || PadDown(4) || PadDown(5) || PadDown(7)) controllerUsed = true;
            if (PadDown(1) || Input.GetKeyDown(KeyCode.Escape)) { Back(); return; }
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
            return Game.L(new[] { "左摇杆移动。实际走过三格后完成。", "靠近居民或发光物按 B。试着和南侧公会的莉雅交谈。", "按 VIEW 打开手帐，选择任务并确认追踪。金色标记会指向下一步。", "去镇内教官处练习，或用 X 命中敌人。空挥不会完成。", "红圈出现后按 A 闪避。成功避开真实攻击才完成。", "用 Y 战技命中目标。通常耗40耐力并有冷却，可在训练场练习。", "按 START，在菜单中选择冒险背包。选择物品或比较武器与打法。", "向北到密林出口，或在菜单中选择世界地图与荆棘密林。找到侦察员。" }[step], new[] { "Move with the left stick. Walk three tiles to finish.", "Approach a resident or glowing object and press B. Try Lyra at the southern guild hall.", "Press VIEW. Select a quest and confirm Track; gold markers show the next step.", "Visit the mentor or hit a real foe with X. Empty swings do not count.", "Press A to dodge a red warning. Successfully avoiding the attack counts.", "Land Y on a target. It costs 40 energy and has a cooldown. Try the practice yard.", "Press START and choose Inventory. Inspect items or compare weapons and styles.", "Go north to the forest exit, or open World Map and select Thornwood. Find the scout." }[step]);
        }
    }
}
