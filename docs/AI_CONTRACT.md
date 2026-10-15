> **范围说明（2026-10-15）：这份 v1.5 规范是另一套 Muse × Codex CLI 开发/调度契约，目标是 `jinze909/rpg-by-ai` / `codex-cli/dev`，不是本 AI-2 的主动部署说明。** 在 `jinze909/rpg-by-ai-2/chatgpt/dev` 中保留此历史材料用于只读参考；**不得**按其内容重建任务、安装环境、迁移文件或写入其他分支。AI-2 的当前工作范围与状态见 [AGENTS.md](../AGENTS.md) 和 [AI2-STATUS.md](AI2-STATUS.md)。\n\n---\n\n# 《星陨边境》Muse × Codex CLI 长期开发权威契约 v1.5

**版本：v1.5 · 2026-10-11 · 待 Muse 实际部署与回执**  
**用户操作：将本 MD（以及随附的 `sprite-animation-qa-skill.zip`，或本文件所在完整交付包）发送给原 Muse 会话，并要求执行。**  
**唯一目的：**在完整继承 Work RPG、旧 Codex 代码/会话接力和原有 Skills 的基础上，让 Muse 低开销、可靠地调度全新 Codex CLI 自主开发游戏。避免重复测试、文档、全量打包、长 Prompt 与重复环境探测消耗额度。

> **执行身份与版本优先级：** Muse 是唯一主动调度器；Codex CLI 是实际开发者；用户是设计方向和验收的最终决定者。此文件是**替代**尚未发送的 v1.3、v1.4，以及先前 v1.1/v1.2 的相冲突开发要求的**完整合并版**，**不是**要求重建已经部署好的自动化。用户最新明确决定 > 当前真实代码与已验证环境事实 > 本长期契约 > 历史阶段性 Prompt。历史文档作为证据，不作为每轮都加载的并行指令。若本文件与已经完成的实际状态重复，先核查并跳过，不重复执行。

## 1. 已知系统与本次部署方式（先查状态、按差异更新）

- 主调度名称 `codex-rpg-hourly-check`，虽含 hourly，**真实检查频率是每 30 分钟**，时区 `America/Los_Angeles`；既有任务**就地更新**，不要再建主动调度。30 分钟是**巡检**频率，绝非每 30 分钟都强制给 Codex 发送新 Prompt。
- `codex-rpg-watchdog` 是独立**只读**看门狗，既有 30 分钟错峰巡检（若实际为 `:15/:45` 则保持），绝不能充当第二个 Codex 开发派发者。只有确认第一项**真正游戏代码/资源改动**已成功推送后，才把它的 GitHub 检查目标从旧仓库切至新仓库的开发分支；不以文档/Skills 提交充数。
- 当前执行架构是 **Muse 托管的云端 VM 里运行 Codex CLI**，并非使用原网页 Codex Cloud 的任务会话；既有网页 Codex 派发路径继续停用。已报告 CLI `0.149.0`、ChatGPT 账户认证、`git-lfs 3.8.0`、可用 GitHub HTTPS 推送、持久工作目录 `/home/hatch/workspace/starfall-frontier`。这些是既往实测结果，执行时以实际状态为准，**不用每轮完整重查已稳定的静态配置**。
- GitHub **主要项目**：`https://github.com/jinze909/rpg-by-ai`，工作分支 `codex-cli/dev`，历史 `main` 基线 `b0aae3d329c5b0b200b30b1457d0f692257b9f06`，Unity 国际版 **2022.3.53f1**、Built-in 2D。已验证拉取 91 个真实 LFS 文件；最近已知 `021e7597cf1c3993394b83e133d7a11be04f6fb0` 增加八套 Skills 主文件及融合记录。**执行时读取真实 HEAD，不能假定该 SHA 永远最新**。
- 旧 Codex 成果库：`https://github.com/jinze909/2D-RPG-Project`，最后已知 `master=fe82190a63590d84824ef313dade4758906d99ea`。仅用作**只读接力、比较、可复用代码/Skills 来源**；不可把旧 CombatClearing/Thornwood 小原型当成主游戏或直接合并两套 Unity 工程。
- 原有工作树、Git 认证、远端分支、八个已迁移 Skill 主文件、旧 Handoff 阅读和接力台账等既有成果**不重做**。先核对当前 CLI 是否已创建真实新 Session；若已存在，则保留现有 `thread_id`，在安全检查点传入新契约；若没有，再用 `codex exec` **创建全新 CLI 会话**。**旧网页 Codex 的 Session ID 从来不能当 CLI resume ID。**

## 2. 游戏终极目标与 Codex 自主开发权（不可误删）

- 真正要开发的是 Work 的完整 **《星陨边境 / Starfall Frontier》**，不是新建单房间演示；保留其世界观、九地区加炉火旅店、主线与《远岸来信》、原有三种结局、八个 Boss、任务/NPC/三位同行者、职业/装备/成长/遗物、探索生活系统、中英文本、难度及存档价值。可自主加入**新剧情、地区、职业、角色、技能、怪物、玩法、系统、支线或结局**，无需每轮请示；但新内容必须融入现有世界与游戏循环，不得为开发方便删掉旧内容。
- Work 2026-10-07 的“当前先打磨、不新增”只是当时的阶段安排，**不是永久禁令**。Codex 有充分的设计和技术自主权，可以合理地在修复、打磨、扩展和重构之间选取最有玩家价值的任务，不应为了容易通过离线测试而只做琐碎更改。
- **主角身份不可变**：当前可操控的金发主角是唯一 canonical 身份。先锋、游侠、星术师以及以后新增职业都是**同一个人**换装备/战斗方式，不能变成不同主角。头像、选择界面、对话、HUD、世界 Sprite、背包和装备预览一致；**职业决定职业技能/Q、装备决定武器外观和普通攻击**，跨职业换装备不能暗改职业技能。
- 可重构技术框架，但必须先盘点生产链。Work 真正入口是 `Assets/Scenes/Frontier.unity`，主循环主要是 **`GameCore` / `ExpansionCore` → `FrontierRuntime.Update` → OnGUI 绘制**，数据在 `Assets/Resources/Frontier/Campaign.json`。旧 `SampleScene`、旧 `Assets/Scripts/Player/`、旧 Animator/Rigidbody2D 并非完整 RPG 的权威运行链。**不要仅修改未使用的系统就宣称游戏已修复。**
- 保留实际玩法连贯性：移动腿部真实迈步，走→攻/射/施法→收招→走自然；不能抽搐、滑冰或四肢不连；手腕、握弓、箭杆、衣服、箭袋、武器数量和方向一致。攻击的**准备→有效命中/释放→收招**应与武器口、弹体、VFX、Hitbox、伤害结算同步；投射物实际飞行，Boss 有清楚前摇/预警、受击、打断、死亡而非突然消失。敌人/NPC/同伴也遵守同类质量标准。
- 地面与摄像机稳定、树根固定而树冠自然运动；地图清楚标识行走/障碍/目标/交互；HUD 在战斗中不得挡住敌人和危险。荆棘密林第一关目标墙要**真的挡住**绕边、缝隙、闪避、攻击位移与读档越界，不只是画出一堵墙；封印与其他任务门不能错误封路。控制脚步声强度、不同表面的音效及反馈级别。
- 战斗底层逻辑不能因重构倒退：动作起手冻结必要的方向/装备/属性/归属，正确接触帧命中；飞行中换装备不能修改已发射弹体的属性；暂停、Hit Stop、死亡、状态边界及旧技能终止遵守一致的模拟规则。可改实现，不可悄悄破坏玩家体验。
- **存档是重要产品资产**：已有 `SaveData.version=2`、v1/v2 迁移、自动档 `starfall-v1.json`、三个手动槽 `starfall-slot-1.json`—`starfall-slot-3.json`、备份与临时文件恢复、`PrepareSave()` 对世界/敌人/任务/深渊进度的捕获。具体以当前源码核实；改变场景 ID、任务数据、背包、世界状态或存档 schema 时，必须保留旧档可迁移、备份和相关回归，不得让玩家进度清零。
- 不可把“工程中存在”“离线测试通过”“Unity 原生验证”和“用户本人接受”混为一谈。已被用户明确否决的 Work 北向游侠错误握弓/折箭/额外肢体不能复活；南向主角和石像 Boss 的内部登记也不等于用户验收。需要相关美术时，**若实际存在**才读 `RECOVERY-STATE.json`、`CANDIDATE-DECISIONS.json`、`PER-FRAME-ACTOR-REBUILD.json`、`POSE-REPAIRS.json` 等接受/拒绝/替换/未恢复台账，不能凭相似文件名或新画面看着好看就解除拒绝。
- 角色素材可用分层 Sprite、逐帧修复、合法资产制作流程等策略，别无限重复生图。若两次相似生成仍未解决解剖问题，优先改变方法，而不是继续消耗额度抽卡。正式发布时核查原 Viola 素材使用许可和 Noto 字体字形/许可证；**不用每轮检查发布许可证**。

## 3. Work 最终工程、旧 Codex Handoff 与融合边界

- Work 较新的最终源工程 `StarfallFrontier-UnityProject.zip`：**429644156 字节**，SHA-256 `b19da262df72eead2d07ac6d8c208c598a908c63b185d494eda42cb58115000c`；Work 最终接力 `RPG-Codex-Handoff-2026-10-07.md`。这个 ZIP 与 GitHub 早期快照**不能假定完全相同**。目前已拥有可安全开发的 GitHub 基线，**不因 Muse 暂时无法取得用户 Library 的 ZIP 而停工**；如果将来确实取得 ZIP，**只做一次有价值的版本差异审计**，先校验，再挑出有效源码/素材/修复并排除废弃美术，永不盲目覆盖新分支。不要求用户每轮上传约 410MB 归档。
- 旧 Codex 最近真实 Git 与交接源：`2D-RPG-Project/docs/iterations/2026-10-11-iteration-012/handoff.md`；按需向前追溯历史 Handoff 和代码，重点理解森林探索、战斗反馈、Boss 预警、奖励/存档、可选 NPC 对话等已实现成果。不需要为了交接再打开旧网页会话、重新跑旧工程所有 CI 或重新生成旧全量 ZIP。只有确有关键的**未保存**决策/未提交成果时再追加一次短 Handoff。
- **融合 ≠ 两仓库 merge**。先比较 Work 原有功能和旧实现的增益，再选择“采用/适配重构/只借鉴设计/放弃”，保留原数据、剧情和玩法。移植旧 Codex 纯逻辑状态机或 NPC 模式前，核对 Work 的任务/存档/输入 API；此前台账声称“RangerDialogue 无缝兼容”是**待验证假设**，并非事实。
- 旧 Codex 的 Mira 游侠引导角色与 Work 已有的红辫同行者米拉可能有设定重叠。**不禁止 Codex 自主创作新 NPC 或支线**：可以整合、改名、改写成独立人物或只提取交互机制，但先确认设定自洽和重复功能价值，不能把旧原型无思考塞进主线。
- 已提交的 `docs/LEGACY_CODEX_INTEGRATION.md` 作为已有接力台账继续维护，**只需修正错误判断和真正新的融合成果**；不要另起多份重复迁移表。若当前 Git 已有这些成果，不再读取/重写同样的内容。

## 4. Skills 完整恢复 + 新动画离线 QA（本版唯一重要一次性缺项）

### 4.1 已做完的事情，不准重复

现有 `codex-cli/dev` 已迁入八套 `.agents/skills/` Skill 主目录：
`session-handoff`、`systematic-debugging`、`verification-before-completion`、`game-design`、`game-art`、`game-audio`、`unity-mcp-orchestrator`、`imagegen`。既有提交 `021e759` 已包含 **57 个文件**。不要重新安装这八套、复制旧工程或复制累赘的旧 `AGENTS.md`。

### 4.2 已真实发现的遗漏：只补 9 个文件

对比旧仓库当时 66 个文件，新仓库迁移后 57 个：**八个 Skill 主文件都在，但 `imagegen` 缺少下面 9 个附件**。请先在实时 Git 核实缺项，缺哪个补哪个，**不要重复复制整个 Skill**：

```
.agents/skills/imagegen/assets/imagegen.png
.agents/skills/imagegen/references/cli.md
.agents/skills/imagegen/references/codex-network.md
.agents/skills/imagegen/references/image-api.md
.agents/skills/imagegen/references/pixel-animation-production.md
.agents/skills/imagegen/references/prompting.md
.agents/skills/imagegen/references/sample-prompts.md
.agents/skills/imagegen/references/unity-character-frames.md
.agents/skills/imagegen/scripts/image_gen.py
```

尤其保留像素动画连续性指南 `pixel-animation-production.md` 与 Unity 角色帧指南 `unity-character-frames.md`。保留原许可证、归属与文件内容的合法来源；检查 Python 脚本安全与环境适配。**imagegen 的 API/CLI 路径可能另行计费：默认只把脚本和文档作为资产，不执行外部计费调用、不注入未授权 API Key。**

### 4.3 新增一个低开销 Skill：`sprite-animation-qa`

如果本消息随附 `sprite-animation-qa-skill.zip`（或在完整交付包内），请在安全检查目录检查 ZIP 成员、不执行不可信脚本后，把 `sprite-animation-qa/` 安装到新仓库 `.agents/skills/sprite-animation-qa/`。保留 `SKILL.md`、`README.md`、`scripts/inspect_animation.py`、`tests/test_animation.py`。

这是 **Python + Pillow 离线工具**，无 Unity Editor、无图像 API 费用；它能输出 `filmstrip.png`、`onion_skin.png`、`preview.gif`、`metrics.json` 和检查报告。安装后**一次性**执行两项自测（必要时允许安装 Pillow），在真正处理 Sprite 动画时才调用；不要每轮巡检都运行或读取完整文档。

**这套工具只是可视化和可疑变化检测**，不能自动判断人体解剖、握弓正确性或 Unity 真实动作时间。需要用实际图像检查连续帧、关键接触姿态和动作过渡；如果当前 CLI 不能查看图片，则明确“视觉未验证”，不要凭 GIF 文件存在就认定通过。预览通常放在未跟踪临时目录，重要里程碑才归档。

**附件不可取得时**：不能声称已安装新 Skill；报告缺失并继续开发。可使用仓库已有的 `game-art` / 补齐后的 imagegen 指南做针对性动画检查，但不能假装新脚本存在。

### 4.4 消除 Skills 隐藏的 Token 负担

旧 `.agents/skills/README.md`、部分 `game-art/SKILL.md`、`imagegen/SKILL.md` 仍有旧项目的“每轮先读 DEVELOPMENT_PROGRESS / KNOWN_ISSUES / NEXT_ITERATION / SKILLS_USAGE 等多份文件”的指示。**只精简这种仓库特定的重复读取/每轮记录条款**，改为：读一次真实生产结构与当前轻量状态，之后只读和当前任务直接相关的 Skill 与文件。保留 Skills 核心专业内容、许可证、脚本和安全注意事项，不能为精简把能力本身删掉。

`session-handoff` **只在会话需要迁移、上下文异常膨胀、长时间中断或真实断点恢复时使用**，不再每轮生成一大份。`verification-before-completion` **按本轮修改风险选检查**，不把几百个测试或多次打包当成必须取得的数量指标。

### 4.5 外部工具状态如实区分

Unity Editor、Unity MCP、Unity Essentials、Unity2D Pixel Perfect、Cloud Environment Runtime、图像生成等：能阅读 Skill 文档不代表工具已连接。已知当前 Muse CLI 云端环境**无法运行 Unity 原生测试，用户已经实际尝试**，因此**不再重复安装、反复探测、反复尝试 Editor/PlayMode 或把它变成游戏开发阻塞项**；只有用户提供新的真实环境/能力时才重新评估。MCP 或生成图片实际调用能力如未经验证，标记不可用/待验证，不自启外部付费 API。

## 5. 现实可执行的 QA 和交付（不再折腾 Unity 原生测试）

- 每项开发以**真实生产源码、游戏内容、玩家可见变化**为对象。小改做相关代码/资源检查；跨战斗、存档、剧情或场景数据改动做定向回归与必要 CI；重大里程碑才做更全面的覆盖。Unity `.meta`/GUID、场景引用、Sprite 图集尺寸/锚点、输入/命中阶段、文本字形及保存迁移按风险检查。
- 动画改动按需使用 `sprite-animation-qa` 的逐帧表、GIF、onion skin 与异常标记，并**实际观察**人物腿部、手腕、武器、脚根、身份和动作衔接。纯数值检查不能证明动作自然；单帧漂亮不能证明整段正确。
- 不能在当前环境做 Unity 原生编译/PlayMode/真实 GPU 画面/物理手感/听感，因此每次涉及这些的结果记作 **`NATIVE_UNVERIFIED`**；既不虚报通过，也不因已知环境限制反复消耗额度探测 Unity。用户以后自己试玩并提供反馈时，再以此修复；不要把必须人工试玩的任务强推给用户作为日常开发前置条件。
- 小轮次及时 `commit/push` 到授权的 `codex-cli/dev`，保护 `.meta` 与 LFS；既有授权允许合理范围的自主 PR/Review/合并（仅在风险和检查条件满足时）；**严禁 force push、覆盖用户未提交工作、未经允许改写主分支或泄漏凭据**。
- **不强制每轮 ZIP、CRC/SHA、GitHub Actions 全量重跑、HTTPS 下载回验或五份进度报告**。重要可玩里程碑、跨工作区交接、用户要求或重要灾难恢复时，交付**完整可下载的 Unity 源工程**与简短变更/已验证等级；无需把源码包冒称可执行安装包。
- 旧 `2D-RPG-Project` 曾公开跟踪 `.ssh/id_rsa` 私钥；禁止读取其秘密内容、复制到新仓库或将密钥出现在日志。该密钥如果仍有效，须由有权限的账户持有者撤销/轮换；这是真实独立安全事项，不要让 Codex 在每轮游戏开发里重复写安全长报告。

## 6. Muse 的 30 分钟巡检与 CLI 长期续航（已有流程保留）

1. **唯一主动调度**：在 `America/Los_Angeles` 每 30 分钟检查一次。先核对上轮巡检、当前 CLI PID/进程启动时间/心跳、工作区和状态；**正在运行就不干扰、不重复启动**。`check.lock` 由 `run_id` 保护，31 分钟/1860 秒过期仅是参考，活着的长任务绝不能因此被强行杀死。释放锁须核实持有者并先持久化状态。
2. **会话**：首次只在需要时使用 `codex exec` 创建**全新 CLI Session**，存储 JSONL 事件中的真实 `thread.started.thread_id`、仓库、cwd、分支、HEAD、PID、当前 goal、checkpoint 和下一步。后续通过实际支持的 `codex exec resume <CLI_SESSION_ID>` 续接，优先保持同一工作区和 Session；**原网页 Codex Session 不参与 resume**。
3. **进程与异常**：长 CLI 进程由可信的 `setsid/nohup` 或其他已有机制托管，主巡检退出时不中断开发。进程实际死亡则根据真实日志/退出状态和 Git checkpoint 判断是已完成、额度耗尽还是运行故障；不得凭 PID 不存在立即宣布失败、反复重跑刚完成的工作。
4. **模型、权限与额度**：使用 ChatGPT 已授权认证和实际可用的 GPT-6.1 Sol **Ultra（若经真实验证可选）**，在用户许可与可信沙盒范围内使用足够权限；模型和推理档在首次实际开发/配置变更时核验，不能暗中降档、不以网页端 Full Access 假定 CLI 权限、不无差别 `--yolo` 开放宿主机。额度耗尽标记 `PAUSED_QUOTA`，按既有 30 分钟巡检等待有依据的恢复时刻，避免昂贵的反复探针；恢复后自动续接，**不购买额度、不使用用户未授权的 API Key 或按量计费**。
5. **浏览器与 Cloudflare**：Codex CLI 正常执行时不触碰网页 Codex。若管理任务确需浏览器且 Cloudflare 阻塞，仅允许最多 **3 轮温和、间隔合理的原页面重试**，尊重平台退避；永久废止旧“五轮关闭浏览器/重开/换代理”规则，无法通过则 `BLOCKED_CLOUDFLARE`，保持巡检。
6. **无限期但有目的**：本轮目标完成后，在下一合适检查点自动选择下一项有玩家价值的修复、扩展、重构、美术或系统目标；**单轮 COMPLETE 不停止整个游戏项目**。只有用户明确要求停止才停；并发、真实阻塞、额度不足时暂停实际开发但保留正常巡检。
7. **长会话 Token 保护**：正常优先 resume，**但不要求一个 CLI Session 永不结束**。当上下文已经明显冗长、反复重读、反复推理或恢复质量下降时，允许用工具支持的压缩手段，或在已安全 push、保存极简 Handoff 后新建 CLI Session，保持 repo/commit/goal/remaining work 不丢。不是定期强制换 Session，也不是每轮生成交接。
8. **持久状态**：Muse 自己的 PID、锁、Session ID、授权状态和 JSONL 放在 Git 仓库外的受保护目录；Git 仓库内仅维持一份轻量 `docs/AI_STATE.md`（或复用已有单一状态文件）。巡检只读最小状态，**不重新灌入本 v1.5、八套 Skill、旧十二轮 Handoff 和长报告**。每次 resume 给 Codex 3–6 句短任务消息：goal、可信 commit、阻塞、下一行动和验证标准。
9. **watchdog**：只读观察 Muse 和 GitHub；在真正游戏开发 Commit 推送后才修正目标仓库/分支，保留已有 task ID/错峰频率。watchdog 永远不能有第二份 Codex 执行权。

## 7. 本次 v1.5 唯一需要做的实际变更（差量、一次性）

**按顺序先核查，已做成的项目跳过：**

1. 读回已部署的 Muse 主调度/看门狗、当前 CLI Session、进程/锁、真实 Git HEAD 和工作树状态；**不创建第二调度器、不擅自重置正在跑的 CLI**。
2. 在现有开发分支只补 `.agents/skills/imagegen/` 当下**真正缺失**的 9 个附件文件；不重新迁移八套主 Skills。对应文件从旧仓库的实际 Git 树安全提取，保留来源与许可，不能复制密钥、不能发起外部计费。
3. 若提供附件 `sprite-animation-qa-skill.zip`，安全检查后安装 `.agents/skills/sprite-animation-qa/`，运行一次两项自测并记录结果；如果没收到附件报告缺失，**不可假称安装**。
4. 仅定点修正旧 Skills/README 里造成每轮重读五份报告、每轮详记 SKILLS_USAGE 等**仓库特定冗余**。保留 Skill 内容。纠正融合台账中“无缝兼容”这样的未经证实说法，但**不夺走 Codex 自主新增 NPC/支线的决定权**。
5. 使新 CLI 的**首次初始化**或下一个安全续接点读取一次 v1.5 的持久版本（可保存为 `docs/AI_CONTRACT.md` 或现有稳定契约文件，不要再生成五份副本）。Muse 巡检本身只读最小状态，不在每轮重复附上 v1.5 全文。此前未送的 v1.3/v1.4 无需再执行；此版覆盖它们。
6. 对本次实际改动做**定向**文件/Skill 测试、安全核对、`git diff`，正常提交并推送到 `codex-cli/dev`；不做全量游戏 CI、全量 ZIP 或无关的旧交接工作。若已有 CLI 开发正在运行，**不要同时修改它正在使用的工作区**；等它的安全检查点或安排串行更新。
7. 随后继续自主开发**真正的 Work RPG**。不要求先机械完成全部旧 Codex 功能移植；Codex 根据实际游戏收益自主选择下一项任务。原生 Unity 不可用已确认，按本版 QA 体系处理。

## 8. 一次性回执与持续报告格式

**这次实施后只报告真实差量，避免长报告：**

- Muse 主调度/Watchdog 是否仍为原任务、30 分钟巡检是否保持；旧网页派发是否继续关闭。
- 当前真实 `rpg-by-ai/codex-cli/dev` HEAD、工作树状态；本次**实际新增/修改**的文件和新 Commit；9 个 imagegen 附件的恢复数量与遗留原因。
- `sprite-animation-qa` 是否收到/安装/测试，不能把“文档已复制”当成“脚本可用”。
- 当前 CLI **真实** Session ID（若尚无就写尚无）、进程/额度/模型确认情况；是否已经读取一次新契约。
- 下一步**玩家可感知的开发目标**和确有必要的阻塞。不要写未实际执行的成功报告。

> **最后底线**：保护已经一点一点完成的 Work 游戏、Codex 源码/Handoff/Skills、用户审美否决与 Git 检查点；给 Codex 充分自主创作空间；把 Token 花在真实游戏质量上，而不是无限审计、重复总结、不断安装、无效 Unity 探针、长 Prompt、五份进度记录和每轮 ZIP 上。**请实际执行以上差量迁移和开发配置，完成后长期自动开发，直到用户明确停止。**
