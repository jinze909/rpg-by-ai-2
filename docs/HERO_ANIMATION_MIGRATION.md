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
