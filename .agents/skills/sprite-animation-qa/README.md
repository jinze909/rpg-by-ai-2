# sprite-animation-qa (Codex repository Skill)

Offline visual review for 2D sprites: a labeled contact sheet, onion-skin comparison, GIF and candidate alignment diagnostics. Requires Python 3.9+ and Pillow. No Unity Editor, internet or paid API needed.

Install once under `.agents/skills/sprite-animation-qa/` in your persistent Git-based game workspace. Use `SKILL.md` only when a task touches animation. Example commands are in that file.

It does NOT automatically understand human anatomy, bow grip, believable walk cycles, Unity runtime timing or user approval. Those need careful inspection and, when available, playable tests.

Run tests: `python3 -m unittest discover -s .agents/skills/sprite-animation-qa/tests -v`
