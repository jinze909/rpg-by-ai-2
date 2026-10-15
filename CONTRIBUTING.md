# Contributing to Starfall Frontier AI-2

This repository is a complete Unity game, **not** a small animation-only demo.

Read [AGENTS.md](AGENTS.md), [the GitHub operations runbook](docs/GITHUB-OPERATIONS.md), and [the current AI-2 handoff](docs/AI2-STATUS.md) before editing.

## Safe scope

The AI-2 writable ref is `jinze909/rpg-by-ai-2:chatgpt/dev`. The default `main`, other teams' branches and the original/legacy GitHub repositories are not implicit merge/push targets. Always fetch and compare the remote HEAD, use non-forced fast-forward updates, and preserve any concurrent work. Do not include generated Unity `Library`, credentials, old source replacements or test files from a different repository.

## Pull request / commit acceptance

1. Identify a *real production path* being changed: Core gameplay, `FrontierRuntime`, the actual Frontier scene, UI/renderer or a verified resource publisher. Avoid modifying an unused preview as proof of a fix.
2. Preserve player state: auto/manual v1/v2 save support, migrations, quest flags/world progression, item IDs, collisions, damage-contact mechanics and Unity `.meta` GUIDs. Describe the exact backward-compatibility surface.
3. For protagonist animation: same blonde hero, equipped appearance independent of vocation skill, 256×256 published **whole-body** sprites, foot top-origin (128,240), 128 PPU and no old character/graft fallback. Character-specific original art for companions/enemies/Bosses is a separate migration.
4. Add tests that **fail on a deliberately broken variant** when feasible; do not relax a deterministic gate merely to get green. For fake or missing art, fail the release path rather than silently duplicating frames.
5. Verify the new **exact commit SHA** in GitHub Actions, not an older successful run. State what was **not run**. Offline compiler/mutation, filmstrips and native Unity GPU PlayMode have different proof standards.
6. Submit a short summary of production edits, updated relevant docs, test outcomes, any blockers and next steps. Do not label art as complete without authored frames and review.

See [UNITY-NATIVE-QA.md](docs/UNITY-NATIVE-QA.md) for real Editor steps and [GITHUB-OPERATIONS.md](docs/GITHUB-OPERATIONS.md) for the full CI/publication policy.

## Useful commands

```bash
git fetch origin chatgpt/dev
git status --short
python3 tools/check_repository_maintenance.py
python3 tools/check_unity_assembly_graph.py
python3 tools/run_core_tests.py
python3 tools/run_combat_input_checks.py
python3 tools/check_native_hero_capture_compile.py
```

The full authoritative suite is `.github/workflows/core-tests.yml`, which also builds and audits the accepted production full-body PNGs and publishes review artifacts. No managed runner in the normal suite claims licensed Unity Editor tests passed.
