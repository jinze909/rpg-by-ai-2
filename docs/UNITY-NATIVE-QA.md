# Unity 2022.3.53f1 native acceptance checklist (AI-2)

**Status as of 2026-10-15: NATIVE_UNVERIFIED.** This file is a test procedure, not a claim that any Unity Editor, GPU scene, actual imported Animator or Player build has been executed in the current AI-2 automation environment.

## Prepare a reproducible checkout

1. Clone/fetch `jinze909/rpg-by-ai-2`, check out **`chatgpt/dev` at a recorded, tested commit**, with Git LFS installed/hydrated if required for your assets.
2. Open the repository **root**, not just `Assets`, in **Unity 2022.3.53f1 (Built-In pipeline)**. Let Package Manager/import complete before assessing Console errors. Do not delete old saves, reset scenes, force reimport unapproved art, or install unknown code as a diagnostic shortcut.
3. Verify `Assets/Scenes/Frontier.unity` is in Build Settings and open it. Enter Play; it should bootstrap `FrontierRuntime`, initialize Campaign and show the **default GPU full-body hero**. `Starfall → Play Adventure` is an alternate entry.
4. Start with **the first Console error** if import/compile fails. Record exact `Assets/...cs(line,column)`, error code, Unity version, branch SHA. Unity asmdef `Starfall.Tests` must reference both `Starfall.Core` and `Starfall.Runtime`.

## Mandatory native tests

- Open **Window → General → Test Runner**, run EditMode `Starfall.Tests` and PlayMode `Starfall.VisualTests`, keeping the Game View visible (not `-nographics`). Do not silently `Ignore` missing Animator controllers.
- In the PlayMode suite, especially inspect `ProductionFrontierSceneUsesTheLiveThreeClassHeroPath`, `ActualFrontierSceneAttackAndSkillContactFramesKeepOneBody`, and `ProductionSceneKeepsEquippedHeroUnderEveryCrossSkillAndAttack`: they load the actual `Frontier.unity` rather than a disconnected preview, check the GPU `SpriteRenderer`, texture identity, Pivot/PPU, locked facing, contact/recovery and 72 cross-vocation/equipment combinations.
- Use **Starfall → Visual QA → Open Review Lab** for focused class/pose/Boss shots; it is supplementary and never replaces the actual production scene.

## Visual inspection matrix

| Scene interaction | Acceptance evidence |
| --- | --- |
| Idle → walk/run → stop, E/W/N/S | Identical gold-haired head and proportions; no foot/ground jump, leg lag or torso drift |
| Vanguard/Ranger/Arcanist equipped LMB | Same equipment throughout windup/contact/recovery (including heavy/combo), real grip and weapon origin; original torso/hips/boots remain attached |
| Every vocation Q while equipped with each of 3 weapons | Vocation controls mechanics; **equipped weapon/armor controls visible full-body Sprite** through cast/release/recovery/hit/death |
| Moving-attack/moving-cast recovery | Note actual root translation, detached planted boots/foot sliding, collision, invisible footstep audio and aim-direction changes; do not call it resolved merely because body remains whole |
| Dodge, damage, death, reopen save | Same character, current equipment snapshot, stable Pivot and correct terminal pose; no impossible clip swap |
| Run behind foliage/structures | The occluded silhouette matches the **same canonical action frame**, not old readable Player sprites |
| Inventory and equipment swap | Full-body canonical preview for same equipment and correct normalized foot root |
| Companions, monsters, Bosses, NPCs | Identify actual production render paths first; they have **not all been migrated** to the protagonist system |

Inspect **screen-space** muzzle against the actual hand, arrows/projection direction, release timing and hitbox separately from Sprite rectangle parity. FPS/frame pacing and input continuity need running builds. Capture E/W/N/S, light/heavy and Q cases before pronouncing artistic quality complete.

## Evidence and sign-off

Save native screenshots under `docs/native-captures/` locally; include exact Git SHA, Unity version, test names, Console errors, screenshots/video, native Tester/OS and remaining art limitations. Generated offline PNG/GIFs in Actions artifact `visual-sprite-qa` are useful reference comparisons, **not native GameView captures**. The code currently supports only all-or-none 18 original central Attack/Cast contact drawings; **unpainted art does not become complete because tests passed**.

Do not change status to native PASS unless a real Editor/Test Runner run confirms it. Mark art acceptance independently as `ART_REVIEW_REQUIRED` until actual new drawings and motion review are complete.
