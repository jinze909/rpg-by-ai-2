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
