# Canonical hero animation: verified production root causes (2026-10-15)

Scope: `jinze909/rpg-by-ai-2`, `chatgpt/dev`; Vanguard, Ranger, Arcanist only. These findings come from the checked-in runtime, art publisher and the actual shipped Sprite atlases, **not a native Unity GameView capture**.

## Root causes and status

1. **Retired pixel-row gait splice (actual historical defect).** The previous renderer combined attack-upper-body RGBA with walking shins/boots across hard-coded pixel bands, including rows ~205–247. Torso, pelvis and legs came from **different physical poses** and produced visible torn joints. `FrontierUnifiedActorGait.cs` is now a no-op and no longer interpolates pixel rows; `FrontierActors.cs` samples single full-body sheets.
2. **Retired five-part rig / head mask (actual historical defect).** Separately pasted Vanguard head artwork displaced neck and hair against the other profession's own body. `FrontierActorRig.cs` no longer has `HumanActor` or `ActorPart`. `CanonicalHeroSpriteFactory.Bake` copies one whole original 256×256 raster. The agreed protagonist's golden hair remains in every class-owned frame.
3. **Default rendering remained experimental despite shipped art.** `FrontierHeroSpriteCapture.cs` initialized `useSpriteRendererHero` to false. The main scene bootstraps `FrontierRuntime`; the F8 sprite path was opt-in, so earlier CI's proof that a path existed did not prove the shipped scene **used** it. The full-body SpriteRenderer capture is now default, with whole-body OnGUI fallback; the scenery/HUD still use GUI drawing. Direct, independently layered world SpriteRenderer is **not** yet claimed.
4. **Unfinished pose transitions, not only wrong offsets.** All 63 installed sheets are based on accepted complete-body poses. E-facing Attack/Cast visibly have large authored keypose differences; they do **not** yet have an individually hand-painted central contact-transfer for 3 classes × 2 actions × 3 facings (18). Filling those slots with duplicate rasters, speed changes, or mechanical blends is forbidden. The exporter and Animator now support an **atomic** 18-contact, 7-cell rollout while the original six-cell clips remain active until all art exists.
5. **CI formerly could pass while art remained unfinished.** Provenance equality, importer checks and blank-body tests guard deterministic regressions, but cannot certify smooth weapon grips, compelling motion, or native Unity rendering. The independent production-anatomy gate now checks each installed raster, body linkage, gold hair, feet and unique GUIDs and runs intentional severed waist/head/boots negative fixtures. Native/gameplay and art-director approval remain separate.

## Immutable rendering contract

Every production frame: 256×256 RGBA; foot at (128,240) in top-origin pixels; Unity bottom-origin normalized Pivot (0.5, 0.0625); 128 PPU; three equipment variants of the **same** blonde protagonist. No mid-frame disassembly or class-crossing parts. One GameCore clock drives frame choice and attack/skill release. Missing artwork remains missing, never synthesized at runtime.

## Review evidence and remaining blockers

`tools/check_hero_runtime_continuity.py` generates actual-production GIFs, filmstrips, onion skins, and **historical algorithm reconstructions** in `.starfall-checks/hero-runtime-qa/`; the reconstructions are not old Unity screenshots. CI publishes these assets as the `visual-sprite-qa` artifact. The new `production-anatomy-qa.json` reports connected-body measurements per published cell.

- Pending: create and validate 18 genuinely new central Attack/Cast contact-transfer full-body paintings in `Assets/ArtSource/Frontier/HeroContact/Hero_<Class>_<Action>_<Facing>.png`, preserving the complete golden-head / foot / grip relationship. Partial staging is non-production; final publication is all-or-none.
- Pending: hand/weapon/grip, bow release axis, casting arm motion and all action-entry/exit artistic review in the **real** Unity 2022.3.53f1 scene, including three-class swap, moving skills, hit and death.
- Pending: Unity Editor native PlayMode, GPU RenderTexture alpha, Animator clip/override correctness and frame pacing. **NATIVE_UNVERIFIED**; no assertion that CI substitutes for actual GameView inspection.
