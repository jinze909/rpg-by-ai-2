# AI-2 live development handoff — Starfall Frontier

> This is the authoritative **AI-2** handoff for `jinze909/rpg-by-ai-2/chatgpt/dev`. Update facts only when an actual change occurs; verify live GitHub HEAD / CI before starting, because the snapshot below ages. Do not replace this with `docs/AI_STATE.md` (a different Codex workflow).

## Snapshot verified 2026-10-15

| Item | Fact |
| --- | --- |
| Writable GitHub repository | `jinze909/rpg-by-ai-2` |
| Writable branch | `chatgpt/dev` |
| Last inspected gameplay HEAD before maintenance | `16ae072510f774aace770a80d953c68220eb7062` |
| Verified CI for that commit | [Starfall core checks — SUCCESS](https://github.com/jinze909/rpg-by-ai-2/actions/runs/38697277669) |
| Default `main` | Earlier snapshot; do not silently merge/reset/change |
| `codex-cli/dev` | Another development branch; AI-2 treats it as read-only |
| Unity | `2022.3.53f1`; production scene `Assets/Scenes/Frontier.unity` |
| Native Unity Editor / GameView test in this AI-2 environment | **`NATIVE_UNVERIFIED`** |
| Art review | **`ART_REVIEW_REQUIRED`** |

## Actual working production pipeline

- Main GameCore action/equipment selection: `Assets/Scripts/Frontier/Core/CombatPresentation.cs`.
- Player rendering: `Assets/Scripts/Frontier/Runtime/FrontierActors.cs` and `FrontierHeroSpriteCapture.cs`. The GPU SpriteRenderer path is default, with the **same canonical full-body** OnGUI fallback. No old Player/Walk/Rear/South action selector or runtime old-body bake is accepted on the playable protagonist.
- Source/publisher: `tools/build_hero_animation_preview.py`, `tools/export_canonical_hero_atlases.py`, `tools/install_canonical_hero_atlases.py`. Release gate: `tools/check_hero_contact_art.py`.
- Shipped published assets: `Assets/Resources/Frontier/HeroAnimator/Canonical/Hero_<Class>_<Action>_<Facing>.png`: **63** 256px-foot-registered Sprite sheets for 3 classes × 7 actions × 3 facings; **279** total shipped frame cells before any approved 7-cell migration.
- CI: `tools/check_native_hero_capture_compile.py` compiles `UNITY_5_3_OR_NEWER` C# under offline declarations, with intentional invalid-API failure. `tools/check_live_sprite_bridge.py` checks binding, legacy rejection and native real-scene test presence. Combat input checks test 3-vocation × 3-equipped × LMB/Q and missing old art. These are **not Unity native**.
- Native PlayMode *source* test `Assets/Tests/PlayMode/VisualPlayModeTests.cs` loads **actual** Frontier scene and checks 72 cross-equipped action/facing cases. It still requires a Unity Editor run.

## 2026-10-15 north/south LMB and Q repair

- **Real production root cause:** export_canonical_hero_atlases.py inserted two old walking-stride body poses at cells 1 and 4 in N/S Attack and Cast, for 24 visibly misplaced transition cells across 12 shipped PNGs.
- **Published binary artwork:** [guarded migration](https://github.com/jinze909/rpg-by-ai-2/commit/e85339d0ae07344e3f05a699e96e8c393e35cbc7) followed by [12 actual PNG repairs](https://github.com/jinze909/rpg-by-ai-2/commit/5127e9a5d99a15f1f85b9efc7d6baa60755e12f2), with unchanged Unity asset GUIDs. Both transition slots now reuse the class-owned, originally drawn full-body planted reaction guard instead of a moving Walk sprite. This is **not** 24 newly painted poses.
- **Runtime safety:** the current six-cell phase cap already prevented premature contact in the existing logic; an additional explicit ActorAction.released fence protects future clip changes. 108 actual N/S LMB-or-Q first/late-windup/release draws are tested.
- **CI:** the original committed PNGs are byte-checked before regeneration; check_hero_directional_action_bridges.py rejects 12 injected legacy walking frames and emits 12 labeled before/after comparisons into the visual-sprite-qa artifact. One-time publishing tolerance is retired.
- **Still missing:** 18 original central contact paintings, distinct upward-facing Cast drawings, natural planted-to-moving recovery, and Unity 2022.3.53f1 native GameView verification (ART_REVIEW_REQUIRED / NATIVE_UNVERIFIED).

## Phase 2 — Emberfall NPC guild/forge original sprite delivery (2026-10-15)

- The active `Frontier.unity` runtime now samples `Assets/Resources/Frontier/Art/EmberfallResidents.png` for **Lyra / guild (id 0)** and **Noll / forge (id 2)**; each has 4 independently rasterized full-body Idle keyposes and 4 Talk keyposes (16 total at 128×128, fixed pixel foot (64,120), 96px display).
- Source: `tools/generate_emberfall_residents.py` hand-authored pixel geometry rasterized with Pillow, **not an image model or external artist**. Dialogue state is bound only to the pinned `conversationSource` of the actual interlocutor; nearby NPCs and the other 14 legacy sprites retain their identities and fallback. No newly added `OnGUI` draw calls; existing SpriteRenderer-first `Actor()` handles the full-body sprite.
- Regression: `tools/check_emberfall_residents.py` hard-fails absent shipped PNG, mutated/duplicated/clipped 16 frames, source-pixel mismatch, GUID/import drift, foot-root jumps and incorrect runtime references. `ActorRegistrationChecks.VerifyResidentAnimation` verifies live idle→talk row switching and rejects same-kind bystander switching. Its review artifact exports `resident-filmstrip.png`, `guild-idle-talk.gif`, `forge-idle-talk.gif`. These are previews, not native acceptance.
- Still pending: all other NPC/companion actor animation, additional monster and Boss identities, terrain Tiles/background/props; complete nonhero Animator controllers and Unity Editor PlayMode verification. **`NATIVE_UNVERIFIED`, `ART_REVIEW_REQUIRED`.**

## Phase 2 — Briar Beast Boss visual production (2026-10-15)

- Code milestone `863e9704`; verified real **binary** Sprite commit `a44a4ccde347149a2d2ce6c5fb7a8f367cbd3e4d`: `Assets/Resources/Frontier/Art/BriarBeast.png` is one 72-cell 320×320 PNG (three HP phases, each with four authored keyframes for Idle, Walk, Windup, Release, Hit and Death).
- Source/provenance: `tools/generate_briar_beast.py`, original Pillow pixel-geometry drawings, same warm/cool ART-BIBLE palette and normalized foot at (160,300). These are script-authored raster poses, **not 72 independently painted artist illustrations** or images generated by a connected diffusion service.
- `FrontierActors.DrawEnemyActor` integrates the actual boss in Thornwood (room 1) with original damage/AI timing, phase thresholds, movement-distance clock and SpriteRenderer-first rendering. It no longer exposes the contact pose in the final 0.2 seconds of a still-active windup. `DrawFallen` uses authored phase-3 death sequence; older Boss art is preserved as missing-resource fallback.
- CI: `tools/check_briar_beast.py` performs strict published-PNG / generator parity, 72 unique complete-body silhouettes, 3 silhouette-changing phases, alpha connectivity, fixed foot, frame drift, importer GUID and adversarial mutation of both art and early-release state mapping. `NATIVE_UNVERIFIED`, `ART_REVIEW_REQUIRED` until real Unity PlayMode and artistic review.
- **Still missing:** remastering of other 6+ Boss identities, 2 other ordinary enemy archetypes, residents/companions, the world tiles/background/decoration pipeline, and proper imported Animator controllers for nonhero character actions. Do not report a completed second phase.

## Phase 2 real asset milestone — 2026-10-15

- **Published original nonhero art:** `Assets/Resources/Frontier/Art/MossboundRaider.png` (24 distinct 128×128 whole-body cells, 4 frames each for Idle/Walk/Windup/Attack/Hit/Death). Pillow-authored via `tools/generate_mossbound_raider.py`, not a foundation-model generated image; Git blob publication commit `08f307e8a6a44c4b455b0a8545dc851a2c91e9ee`.
- **Live runtime:** `FrontierActors.DrawEnemyActor` selects action cells for ordinary melee kind 0 using the actual `Enemy` move distance, windup, release and hit timers. `DrawFallen` selects the authored death progression. Existing sprite-renderer actor bridge draws the source cells; older monster art remains only a fallback if a resource is unavailable.
- **Release-blocking CI:** `tools/check_mossbound_raider.py` checks every committed art cell, source-code raster equality, framing, duplicate frames, severe pose translation, real production reference, PNG importer GUID and negative mutation. It is a deterministic check, **not art direction sign-off**.
- **Incomplete:** 2 other normal enemy types, 8 Boss identities, other NPCs/companions, Boss stages and world terrain/environment remain to be migrated or redrawn. Animator controllers for individual nonhero action families still need native publishing. **ART_REVIEW_REQUIRED / NATIVE_UNVERIFIED.**

## Unfinished / do not claim solved

1. Produce **18 genuinely new** original complete-body Attack/Cast contact poses: 3 equipment classes × 2 actions × 3 facings. Do not use mirrored repeats, procedural interpolations, frame multiplication or merely updated timing to impersonate new drawings. The all-or-none publication gate must fail partial releases.
2. Resolve foot sliding in *moving* attack/cast recovery with complete authored body poses and synchronized root-motion/actual contact. Locked action frame under root translation is a **known visual compromise**, not a final animation fix.
3. Validate head-neck-torso-pelvis connection, real hand/weapon hold, projectile origins, death and Dodge in native GameView; visually review three-class identity and all cross-class skill combinations.
4. Expand analogous one-identity frame policy to actual companion, monster, Boss, enemy archer and NPC runtime renderers, with character-specific original action sets and per-actor provenance. **Not yet migrated.**
5. Run licensed Unity Editor `2022.3.53f1` native compile/EditMode/PlayMode and graphics tests. No hosted Action currently asserts native PlayMode PASS.

## Immediate continuation

1. Fetch current `chatgpt/dev` HEAD and newest Actions conclusion.
2. Read `AGENTS.md`, this snapshot, `docs/HERO-ANIMATION-ROOT-CAUSE-2026-10.md`, and **relevant current code**, not entire old transcript/state dumps.
3. Fix meaningful verified runtime bugs and, for art work, ship legitimate complete-body source drawings with independent quality acceptance. A green CI proves structural invariants, **not** uncreated art.
4. Submit small safe fast-forward commits to the only authorized branch, run the workflow, report verified commit and outstanding native/art blockers. Do not fabricate actions or overwrite other branches.
