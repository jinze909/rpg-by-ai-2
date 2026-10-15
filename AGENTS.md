# Starfall Frontier AI-2 — repository instructions

This file governs AI work **in this checkout**. Read it alongside the user's most recent request. It is not an instruction to install plugins, create new scheduled tasks, spend money, or modify another repository.

## Ownership and safe Git scope

- **Writable project:** `jinze909/rpg-by-ai-2`, **branch `chatgpt/dev` only** unless the user explicitly changes scope.
- The default `main`, sibling `codex-cli/dev`, `jinze909/rpg-by-ai`, and `jinze909/2D-RPG-Project` are **read-only** for AI-2. Never force-push, reset, overwrite or implicitly merge them.
- Before any edit, resolve `chatgpt/dev`'s *actual remote HEAD*, inspect files at that revision, and use a non-forced, compare-and-swap fast-forward update. If the branch advances, stop and reconcile rather than rewriting it.
- Protect the `.meta` GUIDs, source sprites, Git LFS pointers, scene, gameplay data, existing quests, player saves, branches and credentials. Do not delete/replace accepted source artwork merely to make a check green. No secrets or copied private files in Git.
- The product is the existing **Unity 2022.3.53f1** 2D project. Production scene: `Assets/Scenes/Frontier.unity`; engine-independent game: `Assets/Scripts/Frontier/Core/`; playable renderer/input/UI: `Assets/Scripts/Frontier/Runtime/`.

## Current technical and design invariants

- One canonical blonde protagonist across **Vanguard, Ranger and Arcanist**. *Equipped weapon/armor* determines visible body; vocation-specific LMB/Q/skill **mechanics do not change the actor's costume**. This applies to windup, contact, recovery, dodge, hit, death, blocked outlines and inventory preview.
- The player renders **one published full-body Sprite** at a time. Its canonical sheet is 256px high with 256px-wide cells, 128 PPU, bottom-origin Unity Pivot `(0.5, 0.0625)`, top-origin foot `(128, 240)`. The live `SpriteRenderer`/Animator route and full-body OnGUI fallback must select the same GameCore-owned action/frame/facing. Do not restore `Player*`/old `Walk*` body selectors, separated heads/legs, waist scanline grafting, mechanical in-betweens or outdated heavy-attack special cases.
- Unavailable action art may hold the **same equipped class's published whole-body Idle** with an explicit diagnostic; it must not resurrect an old protagonist or silently be counted as a finished action.
- The 63 published canonical atlases exist, but **18 genuinely authored Attack/Cast contact artworks** and motion-complete moving recovery still require original art and real visual review (**`ART_REVIEW_REQUIRED`**). Structural/passive CI, provenance or generated filmstrips are *not* artist approval. Do not publish partial/fake contact art or use `release(hero-contacts):` or `[publish-hero-art]` without complete legitimate resources and their guards.
- Expand the same **whole-body / single-identity** approach to companions, monsters, Bosses and other animated actors only after tracing their **actual in-game draw paths**. Do not declare these other actor systems migrated on the strength of the protagonist's pipeline.

## Verification and reporting

- Main workflow: `.github/workflows/core-tests.yml`. It checks gameplay, input, native-conditional SpriteRenderer C# **offline**, assembly edges, Sprite provenance/anatomy/frame continuity and adversarial mutations. Run and check the *exact pushed commit*'s Actions conclusion; a previous commit's green status does not verify the next one.
- For changes to shaders, imported Animator controllers, true RenderTextures/GameView, playability, audio, performance, real Unity compilation or visual movement quality, report **`NATIVE_UNVERIFIED`** unless those specific checks actually ran under a licensed Unity Editor. EditMode / PlayMode tests are authored but not magically executed by offline Mono.
- Save gameplay state (`SaveData.version=2`, older v1/v2 reads and existing auto/manual slots), combat hit timing/projectile snapshots, collisions, quest data and asset IDs. Run targeted regression tests for actual changed behavior; never hide failures or convert deterministic errors into warning-only green status.
- Produce real code/resource changes when requested, not status-only loops. State exactly what changed, what CI proved, and what still needs native/art validation.
- **AI-2 handoff/status:** `docs/AI2-STATUS.md`; **workflow/runbook:** `docs/GITHUB-OPERATIONS.md`; **Unity QA:** `docs/UNITY-NATIVE-QA.md`; **historical root causes:** `docs/HERO-ANIMATION-ROOT-CAUSE-2026-10.md`.
- `docs/AI_CONTRACT.md` and `docs/AI_STATE.md` describe a *separate Muse/Codex workflow*. They are preserved for history but **not the AI-2 work branch, active state or deployment authority**. User's current instructions supersede any historical notes.
