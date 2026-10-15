# Canonical blonde hero — Stage 3 migration lane (work in progress)

**Target:** Unity 2022.3.53f1; repo `jinze909/rpg-by-ai-2`, branch `chatgpt/dev`.

The authoritative playable game currently renders actors inside an opaque IMGUI world, with existing 256×256 atlas cells and a shared 120/128 screen-space foot anchor. Putting a regular SpriteRenderer behind that opaque IMGUI canvas **does not show the sprite**. Therefore the new renderer is an **opt-in laboratory** until the game-world compositing/order has an actual native PlayMode solution.

## Run the first real engineering pilot

1. Open the project in Unity **2022.3.53f1**.
2. Run **Starfall → Animation → Build Three-Class Hero Preview**.
3. Generated artifacts: under `Assets/Resources/Frontier/Art/HeroAnimator/Sources/`, `Assets/Animations/Frontier/HeroPreview/`, and `Assets/Prefabs/Frontier/HeroPreview/`.
4. Add `Vanguard.prefab`, `Ranger.prefab`, `Arcanist.prefab` to an empty orthographic preview scene with spatial separation. In PlayMode use **1 Idle, 2 Run, 3 Attack, 4 Cast, 5 Dodge** and arrow keys for north/south/east/west.
5. The generated SpriteImporter slices use **256×256 Full Rect cells**, point sampling, no mipmaps, 128 PPU, same **(128,16)** bottom-left pixel pivot. Grid row order matches the existing IMGUI `Atlas` UV mapping: top row E/W, middle N, bottom S.
6. Verify 3 classes × 3 preview directions × 5 actions = **45 actual Animator clips**, a Vanguard controller, Ranger/Arcanist override controllers and three prefabs.
7. Existing original PNGs, scene, quest/boss/combat timing, SaveData v2, GUIDs and production renderer remain untouched.

### Do not mislabel this as completed art

This pilot reuses **accepted existing art**, not newly invented frames. `Cast` / `Dodge` use existing reaction poses as explicit TEMPORARY stand-ins. North attack has two accepted source poses, not artificially duplicated poses. West uses flipped East temporarily; check asymmetric weapons. The live IMGUI art itself still assembles the accepted hero head over the torso, so the lab needs full-body golden-haired baking / identity checks before promotion to production. A green offline CI cannot prove good animation motion or in-game occlusion.

**Stage 3 remains open until:** redesigned canonical full-body hero sets for all professions/directions and Idle/Run/Attack/Cast/Dodge; correct non-duplicate in-betweens and coherent golden hair; real SpriteRenderer + Animator runtime bridge with IMGUI-world occlusion handling and fallback; asset/pivot/frame/weapon QA, Editor/PlayMode and real Game View screenshots; gameplay regression green.

Aseprite CLI reference: https://www.aseprite.org/docs/cli/ — use frame tags and frame durations, not repeated raster files to inflate FPS.


## 2026-10-14 — Stage 3 silhouette continuity repair (in progress)

The shipped 63 class/action/facing PNG sheets have an opt-in, **real gameplay**
`SpriteRenderer + Animator` capture path; the established `OnGUI` path is still
the production-safe fallback. Existing approved source poses at even indices
and original textures/Unity GUIDs remain unchanged.

**Defect repaired in the exporter and Unity Editor recovery baker:** the first
motion-assisted in-betweens used 50/50 Bayer mosaics of two distant source
poses. That created multiple overlapping leg/weapon outlines and colored
half-tone seams. Intermediate cells now deform the **single preceding approved
silhouette** using separately bounded torso and outer-arm motion estimates.
Motion tapers to zero across the fixed `(128,240)` root and the complete
canonical golden-haired head guard, protecting protagonist identity. The
algorithm never averages colors from competing silhouettes.

CI regression now checks: all 63 sheets, both original frames and generated
midpoints, source-palette-only pixels, no change to the head-guard raster,
unchanged boot-contact band, and a synthetic pose that provably moves.
The existing `[publish-hero-art]` gated workflow rebuilds PNGs and changes
**only `chatgpt/dev`** after core checks, using non-force fast-forward and
refusing to publish if another developer has moved the branch.

**Limitations:** deforming one accepted sprite is not a replacement for
hand-authored limb articulation, real weapon grips, carefully redrawn
`Cast` anticipation/contact/recovery or Unity PlayMode footage. Clip counts
must not be represented as third-stage visual acceptance. Next art pass should
replace the most abrupt `Arcanist/Cast` and sword/bow weapon motion with
authored, manually inspected poses before graduating from the fallback.

## 2026-10-14 — Cast contact and rooted recovery integrated

All three classes × three facings now use FOUR accepted source poses:
`Idle -> CastWindup -> CastContact -> IdleRecovery`. The final idle pose
reuses the first exact 256px raster; foot `(128,240)`, head, outfit,
and shared Pivot are unchanged. Three coherent single-source in-betweens
expand each skill strip to SEVEN real Sprite cells. The 9 Cast PNG
assets are regenerated through the existing non-force `[publish-hero-art]`
workflow, without deleting prior art.

The corresponding authoritative `GameCore.HeroAnimationFrameCount` is 4
for skills; the preexisting elapsed/contact/recovery curve now maps onto
a charge pose, actual contact, and a settling pose before action expiry.
The skill's actual damage clock, reduced-speed moving recovery gait,
combat input, quests and persisted saves are unchanged.

The live render bridge invalidates its RenderTexture on a same-frame
Sprite or facing flip change instead of reusing a stale previous pose.
Lost graphics-context textures are re-created before rendering.
OnGUI still remains the default and fallback until native PlayMode QA.

CI now stages regenerated PNGs into its disposable checkout before
checking actual runtime atlas frame counts and checksums. The guarded
publisher then fast-forwards only `chatgpt/dev` on success.

**Not yet an artistic approval:** no genuinely new hand-drawn joint or
weapon-grip poses are claimed. Native Unity 2022.3.53f1 Game View,
complete motion anatomy, casting movement, alpha/occlusion, and 60fps
remain to be verified before stage-three completion.

## 2026-10-14 — Per-limb optical motion, exact-frame native rebake gate

Replaced the previous shared 'reach' translation with independently
registered left/right shin/boot and left/right shoulder/weapon movement,
blending each motion into the original rooted waist. Golden hair/face
raster and all 256px foot-root geometry remain untouched. Intermediate
pixels are sampled only from their preceding accepted source frame:
**no semi-transparent silhouette overlays, no artificial palette colors**.
The Python production exporter and Unity Editor backup compositor use
the same region boundaries and rounded pixel offsets. This is still
procedural raster scaffolding, not professional hand-animated artwork.

Added opposing-arm/leg synthetic regression to catch canceled global
centroids. Full offline exporter check measured **89/90** generated
intermediates different from their preceding pose, and **one** explicit
Run hold. This reports real uniqueness rather than calling all new
cells new artwork. The exact published atlas check and QA remain required.

Critical native Editor migration fix: `EnsureBuilt()` now audits all 63
existing Sprite curves and shipped source PNG frame dimensions. Existing
`.anim` assets for three-frame Cast are **automatically rebaked** when
seven-frame Cast sheets arrive. This preserves assets' paths, GUIDs,
Animator controller overrides and existing gameplay/save timing, while
avoiding a visual mismatch between the current GameCore clock and old clips.

**Native gate remains open:** Unity 2022.3.53f1 import, PlayMode,
projectile grips, anatomical art direction, stable occlusion and
frame-rate measurement still need recorded evidence before stage three
can be marked accepted.

## 2026-10-15 — Full-body five-frame Dodge and Hit

The 3 professions × 3 facings × Dodge/Hit previously jumped between
three accepted poses: Idle -> Dodge/Hit -> Idle. Each of these 18
shipped reaction PNGs now contains two extra full-body motion frames,
making **five Sprite cells**, preserving accepted originals bit-for-bit
at indices **0, 2, 4**. Shared root (128,240), original 56px
golden hair source, class clothing and pixel palette remain fixed.

Runtime SpriteRenderer and Animator map GameCore's existing 0..2
reaction time onto five Sprite cells by doubling read-only pose time.
The Unity Editor fallback baker, clip dimension validator, all class
override controllers and PlayMode sprite frame audit share the new
five-cell contract. Damage, stun, invulnerability, dodge distance,
save schema and quest state are untouched; legacy OnGUI fallback
remains available.

Pixel continuity is programmatic deformation from ACCEPTED sprites,
not newly drawn anatomy or expert approval. CI validates 63 atlases
and 126 generated midpoints, including the 36 newly generated
reaction transitions. Native Unity graphics/PlayMode, manual joint
articulation and visual quality acceptance remain pending.

### Proven offline QA and guarded Sprite publication

Latest offline core run before publishing:
- All 63 regenerated canonical sheets validated.
- All **333** production Sprite frames validated against source poses and sheet dimensions.
- **125/126** generated transitions differ pixel-for-pixel from the preceding
  source (the remaining one is a known Run hold, explicitly reported).
- All 63 existing .meta GUIDs verified stable; canonical head and foot root unchanged.
- All 21/21 production combat input suites passed. New
  `ReactionClockTraversesBothNewMotionCells` checks five Sprite
  samples over unmodified authoritative dodge/hit clocks.

The controlled `[publish-hero-art]` workflow will publish only generated
Sprite PNGs to `chatgpt/dev` with a non-force fast-forward guard.
Do not mark Unity PlayMode or hand-drawn art quality as accepted until
the native test runner and Game View visual inspection actually pass.

## 2026-10-15 — Root-cause elimination: one class owns the COMPLETE body

**Diagnosed rendering defect (not a guess about a PNG):**
`FrontierUnifiedActorGait.cs` copied the bottom 205–247px band from
`WalkVanguard/WalkRanger/WalkArcanist` onto the active attack/cast
`Player*/PlayerRear/PlayerSouth` action torso with a hard
219–225px hem threshold. Even if both sheets were selected with the
same class index, different body poses, cloth, waist armor, weapons and
hip geometry caused a visible stitched mannequin at the belt. The
renderer also intentionally copied ONLY the canonical 56px blonde
face/hair source from `WalkVanguard`; that identity exception
was misread as the entire warrior torso being the template.
**The shared golden head is not permission to share warrior armor.**

**Actual production runtime fix:** `UnifiedActionGait` is now a
single-source whole-body pixel deformation. Every nontransparent
torso, belt, sleeve, lower body and boot pixel comes exclusively from
the ACTION frame belonging to the selected profession; walk sprites
are used solely to choose the distance-based four-step gait phase.
No second sheet is pasted onto the 256px source, no straight waist
threshold exists, and the foot root remains (128,240).
All shared north/south action-sheet frame indices must belong to
the requested class's 4-cell row or composition refuses to run.
The same composed whole-frame texture feeds both active
`SpriteRenderer` fallback and mature OnGUI drawing; no changes
to damage, cast timing, movement, quests or saves.

**Hard pipeline guarantees:** Unity Editor `Build()` and
`EnsureBuilt()` now reject foreign class-owned / shared-row
frame sources. `tools/export_canonical_hero_atlases.py`
executes a release-blocking integrity gate on all 63 production
sheets and all 333 Sprite cells. This checks class/source ownership,
no injected class palette colors, row-by-row waist occupancy,
torso–hip registration and full-body silhouette height. CI and
the guarded PNG publisher explicitly run the same gate.
The gate's adversarial self-tests inject foreign-class pants,
a transparent horizontal waist cut, and shifted hips: all three
must be rejected. The real C# runtime test inserts synthetic
walk colors and confirms no walk color enters the complete action
frame, asserts fixed pelvis, moving original boots, wrong-class
rejection, and bounded texture cache.

**Materialized assets:** the 63 existing production PNGs were already
full-body class-approved frames with only the canonical face/hair
exception. The live split was caused by gameplay's dynamic cached
`Unified_` textures, not corrupted source files. Existing production
atlases are regenerated and byte-compared in CI; do not alter
unaffected approved images merely to create a Git diff. Any actual
asset change is published only by the safe, non-force
`[publish-hero-art]` path after all gates pass.

**Remaining acceptance:** a procedural source-only deformation does
not prove artist-quality knees or hand-to-weapon grips.
Native Unity 2022.3.53f1 EditMode, PlayMode screenshots, GPU
compositing and frame-by-frame subjective assessment remain
NATIVE_UNVERIFIED; stage three is NOT signed off.

## 2026-10-15 — Emergency end-to-end mainline reconstruction (post-splice)

Previous work removed cross-class leg copying but still left TWO
fundamentally unstable runtime operations: `UnifiedActionGait` distorted
the ankles/lower thighs in isolation from the full body, and
`HumanActor` drew four separately cropped torso tiles plus an
independent `WalkVanguard` head on every hero fallback frame.
The native `CanonicalHeroSpriteFactory.Bake` performed similar runtime
face erasure/reattachment when published clips were unavailable.
None of those operations ensures continuous joints, proper grip,
head/neck alignment or artist-authored stride.

**Live code changes:**
- `FrontierActors.DrawPlayerActor` in the actual game now passes ONE
  whole published `Hero_{Vanguard|Ranger|Arcanist}_{action}_{E|N|S}`
  PNG cell to `DrawWholeBodyHeroOnGUI`, or the exact same authored
  full-body cell to the opt-in native `DrawSpriteRendererHero` lane.
- Both routes use the shared `ResolveFullBodyHeroFrame`: existing
  GameCore attack/contact/skill/dash/hit/death timing remains the
  authority; left-facing sprites are flipped as whole units.
- When the released action enters actual moving recovery, switch the
  visual selection to the current class's WHOLE-body Run frame.
  Ordinary attack windup/contact and the Vanguard Q special
  full-body contact are preserved; no independent shins are ever
  animated while the upper body freezes.
- If the 63 shipped canonical resources are absent/stale, OnGUI draws
  ONE complete original class-owned source pose. It never falls back
  to `HumanActor` puppet cutouts or separately draws the shared head.
- The old `UnifiedActionGait` compositor was entirely retired, leaving
  only a no-op cleanup hook for the existing lifecycle. The native
  emergency source Sprite baker also no longer cuts and reattaches a
  warrior head at runtime; the canonical golden face is already baked
  into production Sprites by the offline generator.
- The production `Frontier.unity` contains the Main Camera. Real
  `FrontierRuntime.Boot()` uses `RuntimeInitializeOnLoadMethod` +
  `OnSceneLoaded("Frontier")` to instantiate the MonoBehaviour;
  `FrontierEditor` builds this scene, so these changes are NOT a
  detached `StarfallHeroAnimatorPreview`.

**Reworked release checks:** The previous Mono tests asserted the
presence of head/leg stitching, which was incompatible with the new
pipeline. They now require one class-owned 256px Sprite per real
player draw, the exact 192px/0.75 GUI scale and shared foot anchor,
correct direction/profession/GAMECORE frame selection, preserved Q
contact and moving recovery, and NO `WalkVanguard` head overlay.
`check_live_sprite_bridge.py` fails if the old compositor is
reintroduced. `check_hero_runtime_continuity.py` checks the ACTUAL
installed production PNGs and creates filmstrips/GIFs/onion-skin
diagnostics for 22 high-value clips in
`.starfall-checks/hero-runtime-qa`; severe blank/truncated,
frozen/teleporting Run samples must FAIL, including adversarial
negative-test fixtures.

**Graphics acceptance still open:** The new frame binding eliminates
the runtime split and unsafe leg/head synthesis, but raw existing
artwork itself may have poor hand grips, torso/hip poses or discontinuous
action keys. There are no newly hand-authored keyposes in this commit.
The native capture lane is still opt-in (F8 in editor/development
builds) and remains `NATIVE_UNVERIFIED`, as the current runner does
not have licensed Unity 2022.3.53f1 PlayMode/GPU screenshot evidence.
Do NOT describe CI/raster metrics as visually perfect animation or
silently make an unverified capture the default.

## 2026-10-15 — Published whole-body E-attack / E-cast phase replacement

Real visual diagnosis of `Hero_Vanguard_Cast_E`,
`Hero_Ranger_Attack_E` and `Hero_Arcanist_Run_E` revealed
mechanically generated intermediate frames that were visually nearly
indistinguishable from earlier keys despite passing numerical
"non-identical" tests. Expanding a sheet was not sufficient to
establish animation quality.

First scoped source-art remediation: for ALL THREE professions and
both E-direction Attack and Cast, use existing independently
artist-drawn `Player<profession>:2` as the *entry/weight-shift* body
at expanded slot 1, and `Player<profession>:3` as the
*recovery/step* body at slot 5. These are complete pre-existing
illustrations from the CURRENT class, including actual torso,
weapon, hands, feet, outfit and blonde head, not mechanical limb
motion generated from the adjacent frame. Slots 0,2,4,6 preserve
every original authored action key pixel-for-pixel, including the
true GameCore damage-release contact at slot 4. The intermediate
slot 3 is still algorithmic: pending newly painted key art.
Twelve distinct pose cells in six production sheets are upgraded,
and the exporter records every approved image's exact source
in `curated_full_body_slots`.

Both the exporter and the independent visual-integrity gate
verify their exact underlying class-owned art pixels and their
256x256 dimensions before publishing. CI also enforces 9
class/direction state boundary contracts and real-player
inventory previews at 104px using the same published full-body
golden-head identity.

The Unity Editor builder cannot fall back to its old
`ExpandWithInbetweens/MotionMidpoint` limb algorithm when a
published sheet is missing. It fails loudly instead; all art must
arrive through the guarded `[publish-hero-art]` path. This
preserves GUIDs and avoids silently resurrecting shaky or
noncanonical sprites in the real `Frontier.unity` build.

IMPORTANT: This is approved-existing-keypose reuse, not a claim
of new artist-drawn frames or third-stage animation perfection.
Other directions and reaction transitions still need genuine
artist review and, where necessary, additional full-body art.
Native Unity Editor/GameView remains NATIVE_UNVERIFIED.

## 2026-10-15 — North/South complete-body phase publication

After verifying the real E-facing Sprite results, the same class-locked
artwork selection was extended to BOTH other gameplay facings.
For `Attack` and `Cast`, each of Vanguard/Ranger/Arcanist now
samples ONE approved full-body directional stride at slot 1 and
ONE approved full-body directional recovery at slot 5:
- East: `Player<class>:2` / `Player<class>:3`
- North: `Walk<class>:4` / `Walk<class>:6`
- South: `Walk<class>:9` / `Walk<class>:11`

That is **36 fully artist-drawn, reused individual pose cells**
across **18 live sheets** (3 classes × 3 facings × 2 actions).
The old accepted action keys at slots 0,2,4,6 remain byte-identical.
The actual contact/release is still at slot 4, managed by GameCore.
No cross-class torso/leg cutting, independent face displacement,
or new interpolated palette is introduced. Source identity and
waist/hip/foot consistency gates compare every curated slot byte-for-byte
to the approved class/direction original and reject substitutions.

This stage reuses established high-quality pixel art and does not
constitute a NEWLY hand-painted 36-frame commission. Slot 3 and some
other actions continue to use mechanically assisted intermediary frames,
so aesthetic animation acceptance remains OPEN. Unity graphics native
PlayMode also remains NATIVE_UNVERIFIED.

## 2026-10-15 — Retire ALL head-swapping (offline and gameplay)

The audit found one surviving ROOT-LEVEL split after the previous
gameplay compositor retirement. Production asset generation still
called `canonical_frame`, deleted `ActorFrameRegistration.TryHead`'s
entire head rectangle, then pasted a separate `WalkVanguard` golden
head at an ad hoc x/y position. **This even modified Vanguard's own
accepted keyposes**, and could cut attached collars/neck outlines,
create a head-to-shoulder gap or cause hair/helmet silhouette jumps.
The original `PlayerVanguard`, `PlayerRanger`, `PlayerArcanist`,
`Walk<profession>` and directional shared reaction art already depict
the same blonde protagonist with coherent full bodies. There was no
need to transplant the warrior head.

**Permanent full-body solution:**
- `tools/build_hero_animation_preview.py:canonical_frame` now
  returns the ORIGINAL registered 256x256 source crop, pixel-for-pixel.
  It does not erase/translate/composite ANY head or other body layer.
- `tools/check_hero_whole_body_integrity.py` independently checks
  every source pose against its raw original PNG crop (not just an
  intermediate preview), and rejects any future destructive
  `DESTRUCTIVE_HEAD_OR_BODY_RECOMPOSITION`.
- `tools/check_hero_visual_spec.py` validates class-specific
  registered blonde hair, physical head dimensions, 9 profile
  silhouettes, real equipment socket proximity and original full
  sprite bytes; a synthetic dyed/non-blonde head must be rejected.
  It does **not** require all hair pixels to be the warrior's pixels.
- `FrontierActorRig.cs` permanently removes the unused five-piece
  `HumanActor` cutout compositor. Remaining live Companion UI calls
  were migrated to single full-body Actor frames too; monster
  dimensions retain only a simple noncompositing `ActorCanvas`
  helper. The hero remains on the class-owned full-body production
  asset path.
- The CI static gate rejects restored five-piece rendering and
  `canonical_frame` head erasure. Every changed production atlas
  preserves its original meta GUID, foot Pivot and GameCore timings.
- The visual artifact
  `hero-runtime-qa/retired-head-graft-vs-production-full-body.png`
  labels the left image as **legacy algorithm reconstruction**, not a
  fake screenshot, and shows 3 professions against the actual final
  shipped PNGs, in addition to the previous waist-splice A/B.

This supersedes all EARLIER notes saying "canonical warrior blonde
head is pasted offline": that previous procedure was defective
and is no longer a release requirement. The user's same-hero identity
is now established using approved existing blonde art, not an
isolated moving head. CI/mono tests alone do not certify game art.
No native Unity 2022.3.53f1 editor, GPU Game View or PlayMode suite
was run; native testing remains `NATIVE_UNVERIFIED`.

## 2026-10-15 — Replace nine mechanically warped Run clips with approved whole-body art

Root cause confirmed on the published `Hero_Arcanist_Run_N` and
other walk strips: four accepted 256px walking poses were being
expanded into eight cells by `motion_midpoint`, which translated
lower legs, arms and torso regions independently while freezing the
hair and foot band. This was synthetic raster deformation, NOT
newly artist-authored body movement, and could create knee/hip/weapon
seams, frozen leg segments and odd sliding motions despite a fixed
global Pivot. A PASS for "125 non-identical frames" did not prove
anatomically valid motion.

New production contract for ALL NINE class × facing Run sheets:
- Exactly FOUR original, artist-drawn complete `Walk<class>` cells,
  selected as `facing*4 + (0,1,2,3)`; no synthetic midpoints
  whatsoever in Run. RGBA must match original source cell byte-for-byte.
- `ResolveFullBodyHeroFrame` now maps GameCore `WalkPosePhase`
  ONE-TO-ONE onto the four rooted sprites (rather than expanding
  phase by 2). Neither character speed nor movement/collision,
  stamina, attack release or save data changes.
- The Editor's `ExpandedCount`, existing native .anim frame
  verifier and C# PlayMode full-frame Run tests now enforce four
  authentic cells, with unchanged class-controller bindings and
  stable Unity PNG/meta GUIDs.
- The independent published-pixel CI audit now enforces exactly
  **63 sheets, 297 frames**, including 9×4 source-backed Run frames.
  Its adversarial fixtures still reject frozen / teleporting whole
  bodies; both complete-source parity and waist/hip/palette/root
  tests remain blocking rather than warnings.
- GitHub CI has been restructured to GENERATE/INSTALL the candidate
  production resources BEFORE running C# gameplay tests. Previously
  a legitimate image frame-count migration was wrongly tested
  against the OLD committed sprites (8 Run cells), causing a
  test/publisher deadlock. The guarded release job still checks
  upstream SHA before a non-force fast-forward push.

This removes inappropriate fake smoothness; 4 complete bodies might
look less temporally fluid at 12Hz. Further truly hand-authored Run
transition poses, professional grip/foot animation and native Unity
2022.3.53f1 Frame/GPU review remain necessary. ALL earlier notes
stating "Run has eight production cells" or "333 total production
cells" refer to the PRIOR version, superseded here.
**NATIVE_UNVERIFIED; ART_ACCEPTANCE_PENDING.**
