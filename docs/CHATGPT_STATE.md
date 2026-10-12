# Starfall Frontier — ChatGPT AI-2 state

- Repository: `jinze909/rpg-by-ai-2` only. Development branch: `chatgpt/dev`. Never write to `jinze909/rpg-by-ai` or `jinze909/2D-RPG-Project`.
- Trusted starting point: `68a40c3bb307286d60b7fbc9d0588000c8da91ea` (full Work RPG). `main` is older; do not reset to it.
- Real entry: `Assets/Scenes/Frontier.unity`; `Assets/Scripts/Frontier/{Core,Runtime}/`; `Assets/Resources/Frontier/Campaign.json`.
- Initial gameplay change: keep the last translating dash frame facing its actual dodge heading rather than the preceding walk heading. Existing collision, dodge duration, energy, actions, save schema, art and GUIDs remain untouched.
- Regression: `CoreChecks.DashExitKeepsVisibleFacing` covers 20 ms and 30 ms simulation ticks and registers an Editor test. Offline `tools/run_core_tests.py` and `tools/run_combat_input_checks.py` CI are the applicable checks; inspect the actual GitHub Actions result before claiming a pass.
- Native: `NATIVE_UNVERIFIED` until Unity 2022.3.53f1 Editor compilation and PlayMode/visual validation are actually run.
- Assets: Git tree contains LFS pointer blobs; remote availability of all binary LFS objects has not been independently verified in this environment.
- Follow-up gameplay fix: cap the final dash translation to the remaining active interval, keeping the authored 2.7-world-unit travel consistent across 10–50 ms tick sizes. Added `DashDistanceIsFrameStepIndependent` offline/Editor coverage; CI result must be checked before calling it passed.
- Latest verified parent before this iteration: `f2e7c22663720a0b70ade688707e3f3884b5c08c` (CI success); always re-read live branch HEAD.
- This iteration: a real dodge that starts inside a heavy/Boss warning or delayed regional hazard and exits its radius within 0.30 s earns the existing one-use riposte (+50% next attack, +8 energy, counter text). Unrelated/stale dodges cannot claim it; environmental periodic damage cannot fake a perfect dodge. No save-schema, hero art, GUID, or quest changes.
- Added offline/Editor coverage `EscapingTelegraphedAttacksEarnsOneCounter` for 20/30/50 ms frames, regional hazards, unrelated and stale dodges. CI and Unity-native validation must be checked after pushing.
- This iteration (parent `c83925544f2e0329efd3d8868dc934e6e3fd5323`): a successful dodge now resets the transient walk stride so the next run starts from the first leg frame instead of an arbitrary pre-dodge gait phase. Dodge travel produces no foot contacts; failed dodges preserve the prior gait. Added `DashToRunRestartsLegCadence` offline/Editor regression across 10/20/30/50 ms ticks. CI pending until the new push is checked.
- Next valuable gameplay work: inspect dodge-to-run visual transitions in native Unity and improve larger enemy/Boss encounter patterns without changing save data or the canonical hero.
- Each iteration: inspect current branch HEAD and CI, choose one player-facing production change, run appropriate real checks, fast-forward commit/push, update this concise state only when facts change. No periodic status-only commits.
