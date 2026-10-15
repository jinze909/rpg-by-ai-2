# Canonical hero animation: verified production root causes (2026-10-15)

Scope: `jinze909/rpg-by-ai-2`, `chatgpt/dev`; Vanguard, Ranger, Arcanist only. These findings come from the checked-in runtime, art publisher and the actual shipped Sprite atlases, **not a native Unity GameView capture**.

## Root causes and status

1. **Retired pixel-row gait splice (actual historical defect).** The previous renderer combined attack-upper-body RGBA with walking shins/boots across hard-coded pixel bands, including rows ~205–247. Torso, pelvis and legs came from **different physical poses** and produced visible torn joints. `FrontierUnifiedActorGait.cs` is now a no-op and no longer interpolates pixel rows; `FrontierActors.cs` samples single full-body sheets.
2. **Retired five-part rig / head mask (actual historical defect).** Separately pasted Vanguard head artwork displaced neck and hair against the other profession's own body. `FrontierActorRig.cs` no longer has `HumanActor` or `ActorPart`. `CanonicalHeroSpriteFactory.Bake` copies one whole original 256×256 raster. The agreed protagonist's golden hair remains in every class-owned frame.
3. **Default rendering remained experimental despite shipped art.** `FrontierHeroSpriteCapture.cs` initialized `useSpriteRendererHero` to false. The main scene bootstraps `FrontierRuntime`; the F8 sprite path was opt-in, so earlier CI's proof that a path existed did not prove the shipped scene **used** it. The full-body SpriteRenderer capture is now default, with whole-body OnGUI fallback; the scenery/HUD still use GUI drawing. Direct, independently layered world SpriteRenderer is **not** yet claimed.
4. **Unfinished pose transitions, not only wrong offsets.** All 63 installed sheets are based on accepted complete-body poses. E-facing Attack/Cast visibly have large authored keypose differences; they do **not** yet have an individually hand-painted central contact-transfer for 3 classes × 2 actions × 3 facings (18). Filling those slots with duplicate rasters, speed changes, or mechanical blends is forbidden. The exporter and Animator now support an **atomic** 18-contact, 7-cell rollout while the original six-cell clips remain active until all art exists.
5. **CI formerly could pass while art remained unfinished.** Provenance equality, importer checks and blank-body tests guard deterministic regressions, but cannot certify smooth weapon grips, compelling motion, or native Unity rendering. The independent production-anatomy gate now checks each installed raster, body linkage, gold hair, feet and unique GUIDs and runs intentional severed waist/head/boots negative fixtures. Native/gameplay and art-director approval remain separate.

## Follow-up real production defects found and fixed (2026-10-15)

6. **Moving-recovery character snap.** Previously `PlayerPose` and `DrawPlayerActor` switched to Run when `RecoveryWalking` first became true; `AppearanceWeapon` switched from the active skill's body/weapon to the equipped weapon, and `VisualFacing` began following locomotion. This destroyed continuity of the still-in-progress, full-body Attack/Cast. GameCore now retains the original whole-body action, equipment and locked direction until its true duration completes. Collision-safe reduced root translation still works; separate legs are never composited.
7. **Invisible gait and phantom footsteps.** After the above fix, `TickMovement` continued accumulating `walkDistance` and `footContacts` while showing a physically planted full-body action. This produced out-of-phase footsteps/sfx and arbitrary Run pose selection when the action ended. Starting an action now resets the transient run phase; collision-safe recovery translation does not accumulate invisible running frames. The first visible post-action Run begins from the stable phase zero. The actor's *world position* still moves during recovery; **root translation of an artistically planted sprite can still LOOK like foot sliding** and is explicitly NOT considered resolved until dedicated full-body motion art exists.
8. **Detached weapon accents for non-east attacks.** World action VFX used camera-fixed `p.x ± 22` / `p.y - 63/77`, ignoring the authoritative class- and direction-specific weapon muzzle. The overlays could float away from bow/staff/sword motion (especially north/south); the accent now uses `GameCore.WeaponOrigin` and `GameCore.WeaponHeight` exactly as physical projectile contact does. Footstep accents are emitted only while full-body Run is visible. Native visual inspection of the mount against the artist's *actual drawn hand* remains required: muzzle alignment is a deterministic starting point, not hand-pose artistic approval.
9. **Native test coverage diverged from production.** A legacy `VisualPlayModeTests.cs` assertion still required recovery to switch to Run, and another native test `Ignore`d a missing production Animator Controller. Those tests now match the real full-body contract and fail if the native controller is absent. A new test loads the actual `Assets/Scenes/Frontier.unity` through Unity Editor PlayMode, checks the bootstrapped `FrontierRuntime`, the default live SpriteRenderer, three-class published atlas identity and fixed foot Pivot, and captures scene screenshots. This **test exists in Git** but **has NOT run** in this environment; `NATIVE_UNVERIFIED` remains mandatory.


## Further audited production defects and migration (2026-10-15)

10. **Redundant old 4×3 pose selector still ran in production even after full-body art became default.** The live player renderer still chose old Player, Walk, Rear, South, and reaction sheets on every action, passing those frame references into the new renderer. Canonical atlases usually masked the mismatch, but it maintained a second body/weapon timeline in failure handling. The ENTIRE older protagonist pose selector is now removed from the player drawing entry. A single GameCore action chooses the published, complete-body raster, with identical frame and foot anchor for native GPU and OnGUI safety fallback. No offset, head graft or waist crop is used.

11. **Native GPU rendering retained a legacy emergency bake path.** When an action resource failed to load, it could invoke the old pre-remaster Player sheet through a runtime Sprite.Create and old per-pose frame number, contradicting the no-old-model requirement. This route is now deleted from the live protagonist SpriteRenderer. It accepts only the published complete-body equipment/action sprite, or the SAME equipment's canonical whole-body Idle. The original canonical Sprite factory is retained for Editor compatibility only; it is not used for a player runtime fallback.

12. **Inventory/loadout previews could silently draw the old Walk model.** The preview now uses the same published south-facing whole-body Idle and normalized foot pivot, including the same full-body amber silhouette in foreground/outline review. If canonical art is absent, it logs the resource defect rather than substituting older equipment pixels.

13. **A failed native texture import was previously blacklisted for the entire play session.** Published Sprite misses now retry at a bounded 120-frame cadence. Corrected GPU resources can become available following Editor import. Unity-imported subassets retain Unity ownership; only wrapper Sprites created by the runtime are freed at shutdown, including partial imports interrupted before they could be added to the complete-atlas cache.

14. **Earlier CI silently skipped actual Unity native conditional C# code.** A new test compiles the real renderer and canonical factory under UNITY_5_3_OR_NEWER against strict offline Unity C# declarations, and deliberately invalidates a SpriteRenderer API to ensure compile regression causes a hard failure. Separately, the native Unity test is prepared to load the real Frontier.unity scene, remove old player art, and verify all 72 profession/equipment/LMB-or-Q/facing cases against actual imported whole-body GPU frames. These native PlayMode tests are present in source but cannot be executed in this environment.

**Remaining artistic blockers:** The 63 shipped canonical atlases contain existing accepted full-body drawings, not newly commissioned action art. Eighteen genuinely new central Attack/Cast contacts remain absent. The 216 game-root movement samples with planted recovery poses remain an ART_REVIEW_REQUIRED slip risk. No animation-rate, limb masking, duplicated frame or coordinate-offset trick was introduced as a substitute. Native Unity 2022.3.53f1 GameView remains NATIVE_UNVERIFIED.


## Immutable rendering contract

Every production frame: 256×256 RGBA; foot at (128,240) in top-origin pixels; Unity bottom-origin normalized Pivot (0.5, 0.0625); 128 PPU; three equipment variants of the **same** blonde protagonist. No mid-frame disassembly or class-crossing parts. One GameCore clock drives frame choice and attack/skill release. Missing artwork remains missing, never synthesized at runtime.

## Review evidence and remaining blockers

`tools/check_hero_runtime_continuity.py` generates actual-production GIFs, filmstrips, onion skins, and **historical algorithm reconstructions** in `.starfall-checks/hero-runtime-qa/`; the reconstructions are not old Unity screenshots. CI publishes these assets as the `visual-sprite-qa` artifact. The new `production-anatomy-qa.json` reports connected-body measurements per published cell.

- Pending: create and validate 18 genuinely new central Attack/Cast contact-transfer full-body paintings in `Assets/ArtSource/Frontier/HeroContact/Hero_<Class>_<Action>_<Facing>.png`, preserving the complete golden-head / foot / grip relationship. Partial staging is non-production; final publication is all-or-none.
- Pending: hand/weapon/grip, bow release axis, casting arm motion and all action-entry/exit artistic review in the **real** Unity 2022.3.53f1 scene, including three-class swap, moving skills, hit and death.
- Pending: genuinely new full-body Attack/Cast **moving-recovery** body art to stop root-translation foot sliding without sacrificing existing collision, damage timing or mobility. The current locked pose fixes body tearing but is not visually motion-complete.
- Pending: Unity Editor native PlayMode, the production-scene PlayMode test, GPU RenderTexture alpha, Animator clip/override correctness and frame pacing. **NATIVE_UNVERIFIED**; code coverage and GitHub Actions cannot substitute for executed GameView inspection.


## 2026-10-15 targeted north/south LMB and Q visual-flash diagnosis

**Confirmed root cause in actual published production PNGs:** The
\`tools/export_canonical_hero_atlases.py\` \`curated_motion_source\` logic
inserted a movement pose from \`Walk<Class>\` at **production cell 1**
(preparation) and **cell 4** (recovery) for north/south Attack and Cast.
That is 3 outfits × 2 actions × 2 facings × 2 cells = **24 visible
walking-body flashes** in 12 shipped six-cell animation atlases.
Example: \`Hero_Vanguard_Attack_N\` originally ran
\`PlayerVanguard:1 → WalkVanguard:4 → PlayerRear:2 →
PlayerRear:3 → WalkVanguard:6 → PlayerVanguard:1\`.
Those stride poses have moving legs and different waist/weapon contours
from the otherwise planted strike: they create a momentary 1–2-frame
old-action flash, despite correct whole-body runtime rendering.

**Production repair:** The two incompatible Walk source slots for every
N/S Attack and Cast are replaced with **the same outfit's existing,
artist-authored complete-body standing guard** from
\`PlayerReactionsRear/South\`, index \`profession×4+1\`.
The sequence is now genuinely **Idle → planted guard → action windup →
contact → planted guard → Idle**, without cut-and-paste heads, warped
legs, forced fake interpolation or modified hitboxes. Using a planted
guard at two non-adjacent moments is an intentional stance return; it
is **not 24 newly painted frames** and must not be marketed as such.

**Contact timing:** The existing six-cell phase selector had a
\`Math.Min(2f, ...)\` limit; detailed inspection confirmed it normally
already prevented drawing cell 3 before GameCore release. A separate
hard cap keyed on \`ActorAction.released\` has been added as defensive
protection against future interpolation changes and seven-cell
releases. **Do not call a premature-hit flash a proven former runtime
bug**; the confirmed visible source was the walking frames inserted by
the asset exporter.

**Regression coverage:** A committed-resource verifier independently
checks the real 12 PNGs before regeneration; the directional checker
enforces class-owned source pixels for cells 1/4, compares their exact
approved source hashes, rejects an intentionally reinserted old Walk
frame and emits before/after filmstrips. Runtime test cases exercise
108 actual North/South LMB/Q first, last-windup and real-release
draw samples across skill vocations and equipped appearances. The
Animation Editor builder imports the published sheets; Unity Editor
2022.3.53f1 GameView and hand/shoulder/foot aesthetics remain
**NATIVE_UNVERIFIED / ART_REVIEW_REQUIRED** until run by a real editor.

**Remaining limitation:** North-facing normal Attack and Q currently
reuse the same underlying \`PlayerRear\` central windup/contact drawings.
Distinct, genuinely drawn backward-facing spell/bow versus melee motions
and the 18 new Attack/Cast middle-contact paintings are still absent.
The root-moving recovery foot-skate limitation remains. This repair
removes a confirmed old walking-body flash; it does not mean all
directional animation art is naturally continuous or finished.
