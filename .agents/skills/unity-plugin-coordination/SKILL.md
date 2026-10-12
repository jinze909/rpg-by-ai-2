---
name: unity-plugin-coordination
description: Use the optional @Unity and $unity-essentials integrations during Starfall Frontier development, while verifying actual runtime access and Unity Editor connectivity before relying on them.
---

# Optional Unity plugin coordination

This repository records how to *use* optional external integrations. A GitHub SKILL.md file does **not** install or connect a ChatGPT/Codex plugin, a Unity Editor package, or a Unity MCP server. These integrations are **available but not mandatory**; lack of access must not stall normal game development.

## Named integrations

- **@Unity** — user-selected Unity integration: `plugin://unity@openai-curated-remote`. Use only when the **current** execution environment actually exposes its tools and project connection. Do not assume that typing `@Unity` in a prompt grants Editor access, or that a plugin exposed in ChatGPT is available in Muse's Codex CLI / CI.
- **$unity-essentials** — user-selected Unity development workflow plugin: `app://plugins_6a5bb1f60cec8191ad25c3c57abba544`. When available, use its relevant Unity project, bug, feature, build-validation, and Unity MCP workflow guidance. It is **not** a Unity-side MCP bridge or proof that the Editor is running.
- Existing repository skill `.agents/skills/unity-mcp-orchestrator/SKILL.md` remains the reference for working with a **verified** Unity MCP bridge; it also does not itself install a bridge.

## Efficient capability check

1. When a task would genuinely benefit from Unity Editor operations, inspect the current tool/plugin inventory once for `@Unity`, `$unity-essentials`, and Unity MCP tools. Do not repeatedly probe known-unavailable capabilities on every routine iteration.
2. Verify the real project: Unity 2022.3.53f1, `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, and the production scene `Assets/Scenes/Frontier.unity`. Inspect any existing Unity MCP provider or client configuration before proposing new setup. Do not infer live Editor connectivity from the existence of a skill file.
3. If real Editor/MCP tools are present, first perform a low-risk read (Editor state, hierarchy, or Console). Only then use relevant operations, and verify script compilation, scene references and PlayMode/build results as actually performed.
4. If tools are absent, proceed with available GitHub, code, resource and offline-test workflows. Mark native Unity execution `NATIVE_UNVERIFIED` and do not claim screenshots, PlayMode tests, or native builds were run.
5. Do not silently install or replace Unity packages, configure new paid services, or add multiple MCP bridges. Only set up a bridge when separately authorized and compatible with this Unity version. Unity 6-specific examples are not proof of Unity 2022 compatibility.

## Project safety and development ownership

Work only in the team's authorized repository and development branch. Preserve the canonical blond protagonist's identity; protect mature and player-valuable features from destructive simplification while freely improving immature designs. Protect save migrations, Unity `.meta` GUIDs, asset references and user-rejected art decisions. Never force-push or overwrite another developer's HEAD. No plugin invocation is required merely to produce a status report; prioritize actual production gameplay improvements.

## Quick use

- **Unity Editor present:** use the appropriate exposed integration for scene inspection, Console triage, prefab validation or PlayMode/build checks; report exactly what ran.
- **Only workflow plugin present:** apply the relevant guidance to repository work; do not pretend the Editor is connected.
- **Neither present:** continue substantive source/resource development using repository skills and ordinary checks, and accurately record missing native validation.
