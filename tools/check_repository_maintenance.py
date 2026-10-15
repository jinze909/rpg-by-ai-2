#!/usr/bin/env python3
"""Verify maintainable AI-2 GitHub docs, repo scope, and CI publication safety.

Standard-library only. This does NOT verify gameplay, full Unity builds,
external links, branch protection rules, credentials, or artistic quality.
Includes intentionally broken configuration fixtures that must fail.
"""
from pathlib import Path
import re
import subprocess
import urllib.parse

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_DOCS = (
    "AGENTS.md",
    "README.md",
    "START-HERE.md",
    "CONTRIBUTING.md",
    "docs/AI2-STATUS.md",
    "docs/GITHUB-OPERATIONS.md",
    "docs/UNITY-NATIVE-QA.md",
    "docs/HERO-ANIMATION-ROOT-CAUSE-2026-10.md",
    "docs/AI_CONTRACT.md",
    "docs/AI_STATE.md",
    ".agents/skills/README.md",
    ".github/pull_request_template.md",
    ".github/ISSUE_TEMPLATE/bug_report.md",
)
MAINTAINED_LINK_DOCS = (
    "AGENTS.md", "README.md", "START-HERE.md", "CONTRIBUTING.md",
    "docs/AI2-STATUS.md", "docs/GITHUB-OPERATIONS.md",
    "docs/UNITY-NATIVE-QA.md", ".agents/skills/README.md",
)
WORKFLOW = ".github/workflows/core-tests.yml"
LINK = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")


def verify_docs(docs, workflow, tracked):
    for name in REQUIRED_DOCS:
        assert name in docs and docs[name].strip(), (
            "MISSING_AI2_REPOSITORY_DOC", name)
    agent = docs["AGENTS.md"]
    for marker in (
        "jinze909/rpg-by-ai-2", "`chatgpt/dev`",
        "`main`", "`codex-cli/dev`",
        "NATIVE_UNVERIFIED", "ART_REVIEW_REQUIRED",
        "18", "63", "SaveData.version=2",
    ):
        assert marker in agent, ("AI2_RULE_MISSING", marker)
    for name in ("docs/AI_CONTRACT.md", "docs/AI_STATE.md"):
        assert any(x in docs[name].split("\n", 4)[0] for x in ("不是 AI-2", "不是本 AI-2")), (
            "OTHER_TEAM_CONTRACT_IMPERSONATES_AI2", name)
    assert "chatgpt/dev" in docs["README.md"]
    assert "chatgpt/dev" in docs["START-HERE.md"]
    assert "NATIVE_UNVERIFIED" in docs["docs/AI2-STATUS.md"]
    assert "NATIVE_UNVERIFIED" in docs["docs/UNITY-NATIVE-QA.md"]
    assert "release(hero-contacts):" in docs["docs/GITHUB-OPERATIONS.md"]

    # Read the Markdown entrypoints that real users and agents consume.
    # Local links must point to actual tracked paths, not invented manifests.
    for source in MAINTAINED_LINK_DOCS:
        for raw in LINK.findall(docs[source]):
            target = raw.strip().split(" ", 1)[0]
            if (not target or target.startswith((
                "#", "https://", "http://", "mailto:", "sandbox:",
            ))):
                continue
            target = urllib.parse.unquote(target.split("#", 1)[0])
            absolute = (ROOT / source).parent.joinpath(target).resolve()
            assert absolute.is_relative_to(ROOT.resolve()), (
                "LINK_LEAVES_REPOSITORY", source, raw)
            assert absolute.exists(), ("DEAD_LOCAL_MARKDOWN_LINK", source, raw)

    for key in (
        "name: Starfall core checks", "publish-approved-hero-sprites:",
        "refs/heads/chatgpt/dev", "needs: core",
        "python3 tools/check_repository_maintenance.py",
        "python3 tools/check_unity_assembly_graph.py",
        "python3 tools/run_core_tests.py",
        "python3 tools/check_native_hero_capture_compile.py",
        "python3 tools/check_live_sprite_bridge.py",
        "check_hero_contact_art.py --release",
        "git push origin HEAD:refs/heads/chatgpt/dev",
        "contents: read", "contents: write",
    ):
        assert key in workflow, ("MISSING_GITHUB_GUARD", key)
    # The publisher safely *logs* "refusing force push" on a stale
    # branch. Reject actual executable push flags, not that safety message.
    assert not re.search(
        r"(?m)^\s*git\s+push\s+(?:--force(?:-with-lease)?|-f)(?:\s|$)",
        workflow), ("DANGEROUS_FORCE_PUSH_IN_PUBLISH_JOB",)
    if "publish-original-briar-beast:" in workflow:
        briar=workflow.split("  publish-original-briar-beast:",1)[1]
        assert ("needs: core" in briar and
                "refs/heads/chatgpt/dev" in briar and
                "[publish-briar-art]" in briar and
                "git push origin HEAD:refs/heads/chatgpt/dev" in briar), (
                    "UNGUARDED_BRIAR_BOSS_PUBLICATION",)
    # Phase-2 publisher is equally guarded: the same branch, explicit opt-in
    # commit marker and a completed core job are all mandatory.
    if "publish-original-mossbound-art:" in workflow:
        phase2 = workflow.split("  publish-original-mossbound-art:",1)[1]
        assert ("needs: core" in phase2 and
                "refs/heads/chatgpt/dev" in phase2 and
                "[publish-mossbound-art]" in phase2 and
                "git push origin HEAD:refs/heads/chatgpt/dev" in phase2), (
                    "UNGUARDED_PHASE2_SPRITE_PUBLICATION",)
    assert "publish-approved-hero-sprites:" in workflow and (
        "contains(github.event.head_commit.message, '[publish-hero-art]')" in workflow
        and "startsWith(github.event.head_commit.message, 'release(hero-contacts):')" in workflow
    ), ("UNGUARDED_ART_PUBLICATION",)
    assert "if: always()" in workflow and "visual-sprite-qa" in workflow

    gitattrs = (ROOT / ".gitattributes").read_text(encoding="utf-8")
    gitignore = (ROOT / ".gitignore").read_text(encoding="utf-8")
    assert "/Assets/Resources/Frontier/HeroAnimator/Canonical/*.png -filter -diff -merge" in gitattrs, (
        "CANONICAL_PRODUCTION_ART_MUST_BE_REGULAR_GIT",)
    for name in (".ssh/", ".starfall-checks/", "/[Ll]ibrary/"):
        assert name in gitignore, ("UNIGNORED_SENSITIVE_OR_GENERATED_FILE", name)
    for path in tracked:
        simple = path.lower()
        assert not any(segment in simple for segment in (
            ".ssh/", "/.env", ".starfall-checks/", "/library/",
            "/temp/", "id_rsa", "id_ed25519",
        )), ("PRIVATE_OR_GENERATED_FILE_TRACKED", path)


def main():
    docs = {p: (ROOT / p).read_text(encoding="utf-8")
            for p in REQUIRED_DOCS}
    workflow = (ROOT / WORKFLOW).read_text(encoding="utf-8")
    try:
        tracked = subprocess.check_output(
            ["git", "ls-files"], cwd=ROOT, text=True).splitlines()
    except (OSError, subprocess.CalledProcessError) as error:
        raise AssertionError("TRACKED_FILES_AUDIT_UNAVAILABLE") from error
    verify_docs(docs, workflow, tracked)
    # Prove the checker rejects unsafe changes; never 'warn and PASS'.
    broken_docs = dict(docs)
    broken_docs["AGENTS.md"] = broken_docs["AGENTS.md"].replace(
        "NATIVE_UNVERIFIED", "fake_native_pass")
    try:
        verify_docs(broken_docs, workflow, tracked)
    except AssertionError as error:
        assert "AI2_RULE_MISSING" in str(error), error
    else:
        raise AssertionError("UNSAFE_MAINTENANCE_CI_ACCEPTED_FALSE_NATIVE_STATUS")
    unsafe = workflow.replace(
        "needs: core", "# deliberately removed dependency")
    try:
        verify_docs(docs, unsafe, tracked)
    except AssertionError as error:
        assert "MISSING_GITHUB_GUARD" in str(error), error
    else:
        raise AssertionError("UNSAFE_MAINTENANCE_CI_ACCEPTED_UNGUARDED_PUBLICATION")
    unsafe_force = workflow + "\n          git push --force origin HEAD:chatgpt/dev\n"
    try:
        verify_docs(docs, unsafe_force, tracked)
    except AssertionError as error:
        assert "DANGEROUS_FORCE_PUSH_IN_PUBLISH_JOB" in str(error), error
    else:
        raise AssertionError("UNSAFE_MAINTENANCE_CI_ACCEPTED_FORCE_PUSH")
    print("PASS AI-2 docs, active branch safety, local links, Git LFS/art, "
          "CI hard gates and tracked secrets/path hygiene")
    print("PASS injected loss of native-status guard and release dependency rejected")
    print("NATIVE_UNVERIFIED: documentation check is NOT Unity Editor validation")


if __name__ == "__main__":
    main()
