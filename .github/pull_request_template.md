## What changed in the real game?

Describe actual production code/resource paths, not preview-only updates.

## Visual identity / art changes

- [ ] None, or explained same-equipped-body / original-art / Pivot / GUI-to-native compatibility
- [ ] No old Player/Walk pose fallback, head graft or waist splice was reintroduced
- [ ] No fabricated Attack/Cast contacts or partial art publication markers

## Player-state safety

- [ ] Save version, inventory, quest flags, collision and combat timing are unchanged or explicitly migrated/tested
- [ ] Unity asset `.meta` GUIDs and LFS/normal-blob conventions are preserved

## Verification on this exact commit

- Commit SHA:
- [ ] GitHub Actions core: link + SUCCESS, or named failing checks
- [ ] Negative control / deliberate bad-case rejection, if applicable
- [ ] Unity Editor native compile / EditMode / PlayMode **actually executed** (add logs), or label `NATIVE_UNVERIFIED`
- [ ] Artistic/GameView motion review actually completed (add evidence), or label `ART_REVIEW_REQUIRED`

## Handoff

Known remaining defects, unpainted art, limitations, next safe step:
