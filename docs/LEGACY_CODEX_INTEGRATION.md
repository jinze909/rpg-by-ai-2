# Legacy Codex Integration（旧 Codex 接力记录）

维护日期：2026-10-11。增量补丁 v1.2，不重建调度、不恢复网页开发路径。

## 1. 旧 Codex 最终状态（已核实）

- 旧仓库：https://github.com/jinze909/2D-RPG-Project，`master = fe82190a63590d84824ef313dade4758906d99ea`
- 最终交接来源：`docs/iterations/2026-10-11-iteration-012/handoff.md`（第 12 轮「信标守林人 Mira」NPC，PR #11 普通合并，CI 全绿）
- 交接链完整（第 10/11/12 轮 handoff + delivery receipt 均在 GitHub），**无需唤醒旧网页会话重做 Handoff**
- 缺失项：无。关键决策、约束、未完成事项（原生验收未做、最终 ZIP 发布待补）均在 handoff-012 中明确记录

## 2. 八套 Skills 迁移状态

全部 8 套已从旧仓库 `.agents/skills/` 迁移至本仓库 `.agents/skills/`（随 Git 保存、可恢复）：

| Skill | 状态 | 说明 |
|---|---|---|
| session-handoff | ✅ 已迁移可用 | 交接链方法论；本文件即按其模板维护 |
| systematic-debugging | ✅ 已迁移可用 | 根因追踪、防御性调试 |
| verification-before-completion | ✅ 已迁移可用 | 断言前必须跑验证命令、证据优先 |
| game-design | ✅ 已迁移可用 | 设计基线方法 |
| game-art | ✅ 已迁移可用 | 像素美术规范 |
| game-audio | ✅ 已迁移可用 | 音频规范 |
| unity-mcp-orchestrator | ✅ 已迁移（仅文档） | 需 Unity MCP 连接，当前 CLI 环境无，见 §3 |
| imagegen | ✅ 已迁移（条件可用） | 优先内置 image_gen 工具；API CLI 为显式 opt-in，需单独授权 |

安检结论：57 个文件，无真实密钥/私钥/旧绝对路径；许可证与 Attribution 完整保留；未复制 `.ssh/`、认证文件、旧工程本体。

**使用规则（防 token 浪费）**：启动时确认 skills 可被 CLI 发现即可，之后按任务按需调用；不得每轮重读全部八套。

## 3. 外部插件 Skills 实际能力状态

| 能力 | 状态 | 说明 |
|---|---|---|
| Unity Editor（本机/VM） | ❌ 缺失 | 本 VM 无 Unity Editor；原生验收仍按惯例标注"未验证" |
| Unity MCP 服务 | ❌ 未连接 | skill 文档可读 ≠ 已连接；不得伪造连接状态 |
| Unity Essentials / Unity2D Pixel Perfect | ⚠️ 仅文档 | 旧记录引用过的工作指南保留，工具未连接 |
| Cloud Environment Runtime | ➖ 不适用 | 网页版概念；CLI 在本机终端运行 |
| 图像生成 | ❓ 待实测 | imagegen skill 要求实测内置工具是否存在；缺失则记不可用并继续开发 |

原则：缺失外部工具不阻塞开发，保留可迁移的工作指南，报告受限功能；**未经授权不引入付费 API 或新凭据**。

## 4. 旧 Codex 值得保留的成果

1. **Thornwood 荆棘森林探索循环**：信标入口 → 三处种子采集 → 「荆棘追猎者」敌人 → 奖励宝箱 → 30 金币存档 → 可提前返回保留进度（第 11 轮）
2. **Mira「信标守林人」NPC 委托模式**（第 12 轮）：纯逻辑对话状态机 + 不可变快照权威 + 可选接受/上报，不污染存档货币系统——可直接作为新工程 NPC/任务系统的设计模板
3. **session-handoff 交接链**：每轮简短可恢复的状态记录，已是本工程巡检日志的延续标准
4. **verification-before-completion 纪律**：证据等级标注（逻辑检查 ≠ 原生验收），与本工程现有诚实披露一致
5. **设计基线文档**：DESIGN_BASELINE / RANGER_GUIDE / THORNWOOD / CLEARING_PROGRESSION（旧仓库 docs/game/，按需查阅，不整体复制）

## 5. 重复 / 兼容性 / 移植建议

- **不合并两个 Unity 项目**：旧原型（CombatClearing/Thornwood）是小体量原型，rpg-by-ai 是完整游戏（8 Boss、战役、3 职业）；移植"模式与设计"，不移植整套代码
- **无直接功能重复**：新工程已有战斗/成长/装备体系；旧成果的价值在 NPC 委托模式、探索区循环设计、交接/验证纪律
- **兼容性（待验证假设，非事实）**：skills 均为方法论文档，无代码依赖冲突；RangerDialogue 式纯逻辑状态机**可能**适用于新工程 C# 脚本，但移植前必须先核对 Work 的任务/存档/输入 API，"无缝兼容"未经证实

## 6. 下一项最有价值的融合工作

将 **Mira 式可选 NPC 委托循环**（接受 → 探索中采集/击杀 → 回报结算，不动用存档货币）作为模板，
在 Starfall Frontier 现有 NPC/任务系统上实现第一个"信标式"支线委托。这是旧成果对新工程最直接的增量价值，
且完全在无 Unity Editor 的纯逻辑层可验证。
