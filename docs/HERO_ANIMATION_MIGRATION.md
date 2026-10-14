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
