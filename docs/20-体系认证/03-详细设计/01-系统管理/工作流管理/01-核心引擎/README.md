---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_6cb8a1ab9c6511f184de525400f8a581
    ReservedCode1: RHapbaKVoO7LuSA6cJ6M4uosuA5h+pA+YFF+peqkiD4EVg9vVgLdukXpHUZOaVdwoLHhksb0Z2fxG+lOQO1wP4q4Fh/k+2lerAx85yti+wgz3qYLaIDHuKzvcLbCUGzWNYVMkaDqIG5Yd8zMBj8uTEOAALrsECJpyJJ7UQsuC2bHjnx68jgBkZJuPNo=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_6cb8a1ab9c6511f184de525400f8a581
    ReservedCode2: RHapbaKVoO7LuSA6cJ6M4uosuA5h+pA+YFF+peqkiD4EVg9vVgLdukXpHUZOaVdwoLHhksb0Z2fxG+lOQO1wP4q4Fh/k+2lerAx85yti+wgz3qYLaIDHuKzvcLbCUGzWNYVMkaDqIG5Yd8zMBj8uTEOAALrsECJpyJJ7UQsuC2bHjnx68jgBkZJuPNo=
---

# 01-核心引擎

## 作用

工作流核心引擎与设计器的权威设计文档目录。涵盖：总体架构设计（V3）、执行引擎数据模型（V3）、AI 节点设计（V2）、AI 提示词规则、审核规则库与工作流设计器功能设计（V4）。

## 文件清单

| 文件 | 作用 | 状态 |
|------|------|------|
| 工作流引擎-总体架构设计-V3.md | **权威引擎总纲**（V3）：五条核心理念、路径驱动执行、三层任务模型、5 表存储、队列执行、路线图 | 权威稿 |
| 工作流执行引擎-数据模型与接口设计-V3.md | **权威数据模型**：rule_json/layout_json、ALTER 语句、任务级缓存 | 权威稿 |
| 审核规则库与工作流设计器-功能设计-V4.md | **权威总功能设计**（V4）：11 章模板、JSON 规范、校验规则、图形化界面 | 权威稿 |
| AI节点-详细设计-V2.md | **V2 AI 节点设计**：节点引用+隐式依赖、customParams 模型 | 权威稿 |
| AI提示词规则-功能设计-V1.md | AI 提示词三段式契约：占位符、输出契约、依赖图 | 参考稿 |

## 已归档文档

> 以下文档已过期或被新版替代，归档至 `99-归档/`。

| 文件 | 归档原因 |
|------|----------|
| 工作流引擎选型与技术研究-V1.md | ADR 多个决策已过期（Skill 接口、双重态 JSON、节点 ID 格式） |
| AI节点-详细设计-V1.md | V1 已废弃，被 V2 替代 |
| 图形化设计器-前端组件设计方案-V2.md | M1-M6 里程碑与实现严重脱节，实际代码已实现大部分功能 |
| 自定义工作流引擎-功能设计-V1.md | V1.4 引擎设计，部分概念被 V3 替代 |
| 工作流节点定义与属性抽象-V1.md | 节点定义部分过期（loop 节点已移除） |
| LogicFlow工作流设计器实施分析与建议-V1.md | Phase E/F/G 实施指南，引用已过期文档 |
| NCConfig工作流设计器-实施TODO-V1.md | V1.1 旧版本，被 V1.2 替代 |

## 已归档讨论文档

> 以下文档是架构讨论过程中的重要成果，其核心设计思想已吸收进权威文档。

| 文件 | 讨论焦点 | 归档位置 |
|------|---------|---------|
| 工作流引擎完整架构设计-V1.md | 任务驱动架构、队列驱动执行、wf_execution_task 表设计 | docs/90-归档/旧版本/归档-2026-08-xx-工作流架构讨论/ |
| 工作流节点参数组织与执行协议-V1.md | 前端节点元数据模型（NodeMeta/PortDef/PanelField） | docs/90-归档/旧版本/归档-2026-08-xx-工作流架构讨论/ |
| 统一Skill执行器与AI-ToolUse设计-V1.md | ISkillExecutor 统一接口、Function Calling 机制 | docs/90-归档/旧版本/归档-2026-08-xx-工作流架构讨论/ |

## 组织规则

- 命名：`{主题}-功能设计-V{N}.md`、`{主题}-技术研究-V{N}.md`。
- 设计变更 → 升版本号（V1→V2），旧版移 `docs/90-归档/旧版本/`（扁平存放）。
- 本目录只保留最新版设计稿与 README。

## 依赖关系

- `工作流引擎-总体架构设计-V3.md` 为架构权威文档，定义 5 表模型、队列执行、路线图。
- `工作流执行引擎-数据模型与接口设计-V3.md` 依赖 V3 架构（表结构、ALTER 语句）。
- `审核规则库与工作流设计器-功能设计-V4.md` 定义 JSON 规范、校验规则、图形化界面。
- `AI节点-详细设计-V2.md` 依赖 V3 架构（执行模型）和 AI 提示词规则。
- 前端实现代码位于 `src/certplatform-web/cert/cert-admin/src/pages/workflow/nc-config/`。
- 实施与验证类文档见 `../04-设计器实施与验证/` 和 `../06-图形化设计器/`。

## 维护约定

- 设计稿必须按 `80-功能设计/README.md` 11 章模板编写，TODO 执行计划（第十章）必填。
- 进入实施前须完成本目录设计文档（文档先行）；实施后逻辑变化必须同步更新文档。
*（内容由AI生成，仅供参考）*
