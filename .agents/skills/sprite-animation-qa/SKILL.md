---
name: sprite-animation-qa
description: Inspect frame-by-frame pixel sprite animations for temporal continuity, canvas consistency, anchor drift, visual jitter, and action-phase readability without a Unity Editor. Use on imported sprite sheets, individual frames, or animation changes before claiming art is ready.
---

# Sprite Animation QA — offline visual acceptance

## When to use

Use **only** when modifying or checking pixel-character/NPC/monster/Boss animation, weapons, projectiles, animation atlases, or animation transitions. Do not reread this skill on unrelated gameplay, story or build tasks.

## Non-negotiable accuracy

- This is **offline frame preview and candidate-error detection**, **NOT** Unity Editor/PlayMode testing, independent anatomy verification, user acceptance, or proof of natural motion.
- Review the *sequence*, not just attractive individual frames. Keep distinct categories for **accepted, rejected, replaced, missing/unverified** source frames; never reintroduce known rejected assets. For Starfall Frontier, read the relevant `RECOVERY-STATE.json`, `CANDIDATE-DECISIONS.json`, and `POSE-REPAIRS.json` **if they are actually present**. The Git snapshot can be older than the final Work ZIP.
- Verify actual production hookup: current Starfall Frontier uses `Frontier.unity` and `GameCore`/`FrontierRuntime`/OnGUI. A plausible sprite/GIF not wired to production runtime is not a finished game change.
- Keep one canonical blonde protagonist across equipment/profession variants; check hand/weapon continuity, frame direction, foot/root anchor, feet/legs moving, silhouettes, equipment and palette.
- Do **not** infer that all identical pixel frames are bugs: purposeful held poses/idle duplicates are valid. Conversely, a numerically stable bbox does not prove the hand grips the bow.

## Workflow — only for the relevant changed animation

1. Locate the **actual currently used** source frames/atlas, frame order, intended FPS/timing, action and direction. Use existing project metadata. Do not invent frame order from filename sorting if production code specifies it. Note start, anticipation, contact/release, follow-through, recovery, and walk→attack→walk transition where relevant.
2. Create a disposable report **outside tracked assets** (e.g. `/tmp/starfall-animation-qa`). Install only Pillow if not already present and dependency installation is allowed; do not use paid image services.
3. Run one of:

   ```bash
   python3 .agents/skills/sprite-animation-qa/scripts/inspect_animation.py \
     --frames Assets/ArtSource/Frontier/Frames/Ranger/North/Idle.png \
              Assets/ArtSource/Frontier/Frames/Ranger/North/Walk0.png \
              Assets/ArtSource/Frontier/Frames/Ranger/North/Walk1.png \
              Assets/ArtSource/Frontier/Frames/Ranger/North/Walk2.png \
              Assets/ArtSource/Frontier/Frames/Ranger/North/Walk3.png \
     --fps 8 --scale 4 --out /tmp/starfall-animation-qa/ranger-north
   ```

   Or an equally sized rectangular atlas:

   ```bash
   python3 .agents/skills/sprite-animation-qa/scripts/inspect_animation.py \
     --sheet Assets/Resources/Frontier/Art/HeroRangerSouth.png \
     --cols 4 --rows 4 --indices 0,1,2,3,4,5,6,7,8,9,10,11,12,13 \
     --fps 8 --scale 3 --out /tmp/starfall-animation-qa/ranger-south
   ```

   `--indices` is **not** inferred action playback order; select the actual indices for an action, not all atlas padding cells. `--contact-index N` marks the frame in **the chosen sequence** at which the action should connect. `--anchor X,Y` draws the intended root point on the visual report; it is not used to recenter frames.
4. Open `filmstrip.png` and `preview.gif`, plus `onion_skin.png`. Check **both** full-resolution nearest-neighbor enlargement and apparent in-game size; look at last→first continuity on loops and the sequence boundary when chaining walk→attack→walk. With a visual-capable model, inspect images directly. If the execution environment cannot display images, mark visual review as unverified, even when file metrics pass.
5. Read `metrics.json` / `report.md` for *candidate* errors: empty cells, unexpected image dimensions, sharp opaque-bbox shifts and large changes in silhouette. These are heuristic flags with false positives (weapons, particles, dash, crouch). Review flags manually; do not suppress by moving the canvas or scaling individual frames.
6. Specifically check hands/shoulders/elbows/wrists, bow grip, straight arrow/arrowhead, weapon-hand switch, feet/leg gait, head/face stability, equipment appearance, camera/root position, animation phase and release/Hitbox/Projectile correspondence. The script cannot certify these semantically.
7. Correct only bad frames or timing, maintain known good pixels and anchors, rerun the targeted report, update true source/atlas references and record what remains **NATIVE_UNVERIFIED**. Do not automatically regenerate whole atlases or demand a complete ZIP/CI for a small isolated animation correction.

## Outputs

- `filmstrip.png`: every selected frame at nearest-neighbor scale, labeled and kept at its original cell location.
- `onion_skin.png`: overlay of consecutive frames for spotting popping/jumps.
- `preview.gif`: animation playback (repeat for motion inspection, not proof of good looping).
- `metrics.json`: reproducible measurements and warning candidates.
- `report.md`: compact review checklist. Fill in the *actual* inspection outcome rather than equating absence of warnings with acceptance.

**Performance:** report only changed actions and neighboring transitions. Store previews in an untracked temporary directory unless explicitly preparing a milestone review; do not make Muse read the report at every 30-minute巡检.
