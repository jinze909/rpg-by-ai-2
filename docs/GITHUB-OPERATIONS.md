# AI-2 GitHub operations & CI runbook

Scope: **`jinze909/rpg-by-ai-2`**, **`chatgpt/dev`**. This runbook is for repository operations, not an authorization to change the separate original project or run scheduled jobs. Authoritative per-task instructions: root [AGENTS.md](../AGENTS.md).

## Branches and authorization

| Branch | Purpose | AI-2 policy |
| --- | --- | --- |
| `chatgpt/dev` | Active AI-2 gameplay + renderer + CI | **Only writable ref** |
| `main` | Repository default, an older baseline | Read-only; do not auto-merge |
| `codex-cli/dev` | Other developer/team stream | Read-only; do not interfere |

The other repositories `jinze909/rpg-by-ai` and `jinze909/2D-RPG-Project` are read-only references. Never force push, delete/reset branches, or overwrite a concurrent head. A commit is not delivered until the **remote** ref is confirmed and its **own** workflow has finished.

Suggested CLI audit (requires an authenticated checkout):

```bash
git status --short
git fetch origin chatgpt/dev
git rev-parse origin/chatgpt/dev
git branch --show-current
git log -1 --oneline origin/chatgpt/dev
git lfs ls-files
```

If no CLI checkout exists, use authenticated GitHub branch/commit/Tree API; never infer branch freshness from a previous conversation. Only create an update after comparing the expected parent to current HEAD.

## Unity project, dependencies and LFS

- Open the **repository root** containing `Assets/`, `Packages/` and `ProjectSettings/`, using Unity **2022.3.53f1**. The real scene is `Assets/Scenes/Frontier.unity`.
- Do not copy `Assets` over a newer project or reset the `Library`/scene to conceal errors. Unity regenerates `Library/` locally; it is ignored. Preserve `.meta` GUID continuity.
- `.gitattributes` puts large general images/audio/fonts in Git LFS, but exempts small project resources and the **63 canonical production PNGs**, which must remain normal Git blobs for repeatable runner checks. Do not globally rewrite image LFS settings or replace pointers without verifying imports. `git lfs pull` is needed for LFS-dependent *native* workflows; standard hosted CI intentionally tests the committed non-LFS canonical PNGs and should not claim a full game build.
- `.gitignore` excludes Unity caches, local secrets `.ssh/`, test results `.starfall-checks/`, and generated Python caches. Never commit project logs, personal paths or auth tokens.
- The original repository reportedly contained an old private SSH key in past history; treating a current working tree as clean does not rotate a historical credential. Its owner must ensure it is **revoked/rotated**. Do not republish that key.

## CI: what a green run proves

Main workflow: [Starfall core checks](../.github/workflows/core-tests.yml).

- The CI **first** checks the committed canonical art before regeneration, then exports/stages the publisher candidate, and audits 63 real installed atlases, whole-body anatomy, frame continuity, palette/foot/head identity, GUIDs and comparison filmstrips/GIFs.
- Separate checks validate Unity asmdef references, actual GameCore C# logic, runtime combat/drawing checks, Editor menus/import rules under stubs, and the real `UNITY_5_3_OR_NEWER` conditional hero renderer + Sprite factory under strict offline C# declarations with a deliberately invalid API sample.
- `tools/check_live_sprite_bridge.py` rejects reintroduced old model branches and confirms the **existence** of proper native-scene tests. GitHub Actions still has **NO licensed Unity Editor**: GameView, graphics driver/RenderTexture, actual Animator state, Unity C# import, music and real feel are `NATIVE_UNVERIFIED`.
- All CI-produced image comparisons, contact matrices and diagnosis files appear in `visual-sprite-qa` artifacts. These images come from *actual checked-in Sprite cells* or clearly labeled historical algorithm reconstructions; do not call them native screenshots.
- Report native and artist review independently. `PASS`, `ART_REVIEW_REQUIRED`, `NATIVE_UNVERIFIED`, and `BLOCKED` are distinct statuses. The legacy `docs/QA-REPORT.json` and `BUILD-INFO.json` are timestamped historical reports, **not live latest-commit PASS evidence**.

## Developer checks

Run from repo root with Python 3, Pillow and Mono mcs/mono (Unity is separate):

```bash
python3 tools/check_repository_maintenance.py
python3 tools/check_unity_assembly_graph.py
python3 tools/run_core_tests.py
python3 tools/run_combat_input_checks.py
python3 tools/check_editor_syntax.py
python3 tools/check_native_hero_capture_compile.py
python3 tools/check_live_sprite_bridge.py
python3 tools/check_hero_runtime_continuity.py
```

The heavy visual checks also require actual PNGs and their upstream generation. Prefer invoking the **GitHub workflow** for the authoritative full sequence rather than reordering its guarded publication pipeline.

For native tests: [Unity native validation](UNITY-NATIVE-QA.md).

## Art release gate (not ordinary development)

The `publish-approved-hero-sprites` job runs **only** for a guarded *push on chatgpt/dev* with explicit publication marker `[publish-hero-art]` or message prefix `release(hero-contacts):` and only **after** the core job passes. This is not a routine CI branch; it must never be triggered for ordinary docs, tests, intermediate/incomplete sprites, or fabricated contact in-betweens.

For true 7-cell contact migration: supply and validate all **18** original, genuinely different whole-body source PNGs in `Assets/ArtSource/Frontier/HeroContact/`, pass the explicit release verifier and all production tests; then allow the gated fast-forward artwork publisher to run. It must refuse if another developer advanced the remote HEAD. Never publish partial art or bypass guard failures. Preserve Unity `.meta`/GUIDs and review provenance.

## N/S action-frame regression gate

On 2026-10-15, the one-time 12-PNG publication retired the 24 original walking-stride insertions from north/south LMB Attack and Q Cast. **No legacy source-frame tolerance is permitted anymore.** The checkout-first check_committed_hero_production.py script must byte-compare all 279 accepted production cells before regeneration; a single wrong walking or cross-profession cell fails CI.

The new check_hero_directional_action_bridges.py validates 12 N/S Attack/Cast production strips, performs deliberate first-bridge Walk-frame corruption and emits Hero_*_BEFORE-AFTER.png comparison strips and directional-bridge-qa.json under .starfall-checks/hero-runtime-qa, uploaded in visual-sprite-qa. The replacement is pre-existing authentic planted guard artwork, **not 24 newly created contact frames**. The pre-release sprite selector also asserts ActorAction.released before actual contact index; real Unity Animator/GameView remains NATIVE_UNVERIFIED.

## Troubleshooting

- **Unity CS0246 / CS0103:** verify the full error, owning `.asmdef` references, package installation and real compiler output. Offline mcs alone is not Unity. `Starfall.Tests` must reference `Starfall.Runtime` as well as `Starfall.Core`.
- **Actions green, Unity still fails:** copy the *first red* Console error with filename, line and compiler message; determine whether failure is native Editor/Animator/package/asset import before changing art. Do not present offline C# tests as proof native Editor compiled.
- **Hero reverts to old appearance:** confirm local branch HEAD and `FrontierActors.cs`/`FrontierHeroSpriteCapture.cs` code match the current remote `chatgpt/dev`; inspect loaded `Hero_<Class>_<Action>_<Facing>` import and actual renderer/facing in **Frontier.unity**. Never hide the issue by reintroducing old `Player*` fallback.
- **CI failure:** inspect failed step's logs and reproduce. Do not weaken a deterministic guard simply to turn red green. Negative/mutation tests must fail against the intentionally damaged sample and pass against production.
- **Simultaneous development:** pause write/update and rebase/reconcile with the new HEAD; never set `force=true`. Don't push a second release from an out-of-date tree.
- **Missing native Unity:** use the offline suite and record `NATIVE_UNVERIFIED`; do not imply Unity itself ran.

## Documentation ownership

- `README.md` / `START-HERE.md`: player/developer entry points, not CI result logs.
- `AGENTS.md` / `docs/AI2-STATUS.md`: concise AI-2 rules and current development handoff.
- `docs/HERO-ANIMATION-ROOT-CAUSE-2026-10.md`: verified animation architecture/history + unresolved original art.
- `docs/UNITY-NATIVE-QA.md`: manual Unity scene/Test Runner procedure and evidence bar.
- `docs/AI_CONTRACT.md` / `docs/AI_STATE.md`: historical **other team** material, preserved without making it AI-2 authority.
- Any dated `BUILD-INFO` / JSON QA report is an *immutable historical snapshot*. Avoid rewriting it with a guessed SHA or test count; always link the fresh GitHub run separately.

Update only the document genuinely affected by the next feature. Avoid committing repetitive handoffs or log dumps merely for activity.
