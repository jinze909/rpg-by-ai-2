# 星陨边境 AI-2：打开、编译与运行工程

**当前有效工程：** `jinze909/rpg-by-ai-2` 的 **`chatgpt/dev`** 分支。默认 `main` 是旧基线；不要用其他团队 `codex-cli/dev` 或更旧 `rpg-by-ai` 的整包覆盖本分支。

## 第一次打开

1. 在本地备份原项目和存档。获取仓库的 **`chatgpt/dev` 最新已验证提交**，并保留 `.meta`；有 Git LFS 的资源需要按你的平台配置安装并 `git lfs pull`。
2. 使用 **Unity 2022.3.53f1 / Built-In Render Pipeline 2D**。在 Unity Hub 中添加工程**根目录**（其中应同时有 `Assets/`、`Packages/`、`ProjectSettings/`），等 Package Manager 与素材导入完成。
3. 打开 **`Assets/Scenes/Frontier.unity`**，再 Play。亦可尝试 **Starfall → Play Adventure**。场景由 `FrontierRuntime` 自动引导游戏世界，不需要把旧示例 Prefab 强行放进场景。
4. 打开 Unity Console。若编译失败，请提供**第一条红色**异常（完整 `Assets/…cs(行号)`、CS 错误编号）和当前 commit SHA；先修编译错误再诊断美术。不要删除老存档、Unity `.meta` 或覆盖其他工程来“清零解决”。
5. 用 **Window → General → Test Runner** 跑 `Starfall.Tests` 的 EditMode 测试和 `Starfall.VisualTests` 的 PlayMode 测试（Game View 可见）。**普通 GitHub Actions 不会运行这些需要 Unity Editor 的 GPU/Animator 原生测试。**

## 当前人物动画规则

- 战士 Vanguard、游侠 Ranger、法师 Arcanist 是**同一个金发主角**；装备决定人物身体和武器可见版本，职业 Q 只决定技能机制。跨职业技能也不能让全身突然变成另一个职业。
- 当前完整身体生产 PNG 共 **63 张**，每个动作单帧画布 **256×256**、PPU **128**、脚底固定在顶原点 **(128,240)**；SpriteRenderer 默认启用，安全 OnGUI 回退必须使用同一完整身体 Sprite。
- **未完成**的仍包括 18 张真实新绘制 Attack/Cast 中间接触原画、移动恢复期自然迈步与实际 Unity 画面验收；CI 全绿也不代表艺术审查或原生编译完成。

## 验证与获取产物

GitHub → Actions → **Starfall core checks** → 选择与实际项目相同的 commit，确认 core job 的最终结论。视觉预览、GIF、Filmstrip 和诊断报告在 `visual-sprite-qa` artifact 中；它们是**离线资源对比**，不是原生 Unity GameView 截图。

- [完整操作手册](docs/GITHUB-OPERATIONS.md)
- [当前 AI-2 接力状态](docs/AI2-STATUS.md)
- [Unity 原生验收步骤](docs/UNITY-NATIVE-QA.md)
- [真实主角动画根因与剩余问题](docs/HERO-ANIMATION-ROOT-CAUSE-2026-10.md)
- [实际贡献／提交检查](CONTRIBUTING.md)

保留旧自动档 `starfall-v1.json`、手动槽 `starfall-slot-1.json` 至 `starfall-slot-3.json` 及其有效备份；无需为更新游戏开始新档。未经原生测试的 Unity 编译与画面必须标注 **`NATIVE_UNVERIFIED`**。
