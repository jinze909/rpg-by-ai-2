# Repository RPG skills

Reviewed skill packages are committed here so a fresh Codex Cloud or GitHub Actions checkout can read them without depending on a previous host snapshot. Use the repository root as the working directory. Enumerate `*/SKILL.md`; read the relevant skill and its references once when a task needs it, then only re-read what the current task directly requires. Do not re-read all skills or log SKILLS_USAGE every round.

The user has authorized game development. Preserve accepted design and user edits, follow the repository root [AGENTS.md](../../AGENTS.md) **for AI-2 only**, validate actual behavior, never force push, and separate unavailable or unrun checks from passes.

| Skill | Intended use | Runtime limit |
| --- | --- | --- |
| session-handoff | Load/check context and record a resumable iteration | Run helper scripts by their repository-relative path; historical handoff status is not current code proof |
| game-design | Choose a complete improvement to explore/fight/progress/reward | Requires real project/playtest evidence |
| game-art | Preserve pixel density, silhouettes, action timing and asset organization | Metadata checks do not prove natural animation |
| game-audio | Audit/add feedback categories and deliberate mix priorities | No audio synthesis tool or audio assets are supplied by the skill |
| systematic-debugging | Reproduce, trace root cause, fix, and retest failures | The bundled npm polluter example is not a Unity test command |
| verification-before-completion | Match every completion claim to fresh evidence | Offline checks cannot substitute for licensed Unity playback |
| unity-mcp-orchestrator | Inspect/editor/compile/test workflows when MCP is connected | Guidance only; compatible plugin, server, licensed editor required; Unity 6 examples require adaptation |
| imagegen | Reference-driven raster assets and consistent character-frame production | A callable image tool must exist; CI runners do not inherit chat image generation; CLI API billing is separate and explicit opt-in |
| unity-plugin-coordination | Coordinate optional @Unity and $unity-essentials integrations when actually available | Writing about plugins in a skill does not install/connect them; no Editor/MCP access is assumed |

Where supplied, skill-specific ATTRIBUTION, INSTALLATION and LICENSE files preserve source provenance and usage limits. **There is no tracked `docs/skills-source-manifest.json` in this checkout**; do not represent that file as verified or fabricate package checksums. Code and documentation licenses differ for the three game-* packages. No models, editor caches, credentials or host registration links are included. Existing same-name user files are never silently overwritten.

The cloud-environment-onboarding:setup skill remains a Codex Cloud connector capability and is not redistributed here: no redistribution license was provided, and its environment draft tools are unavailable in ordinary Actions runners.

Useful repository-relative handoff commands:

```bash
python3 .agents/skills/session-handoff/scripts/list_handoffs.py .
python3 .agents/skills/session-handoff/scripts/check_staleness.py <real-existing-handoff>.md
python3 .agents/skills/session-handoff/scripts/create_handoff.py <round-slug>
python3 .agents/skills/session-handoff/scripts/validate_handoff.py <real-completed-handoff>.md
```

Read the chosen handoff completely and validate current Git/scene/asset state before continuing. Never run an example merely because it appears in a reference.
