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

工作流核心引擎与设计器的权威设计文档目录。涵盖：引擎选型与技术研究（ADR）、权威功能设计稿（V4）、自定义工作流引擎功能设计、工作流节点定义与属性抽象、AI 提示词规则、图形化设计器前端组件设计方案。核心引擎类文档统一收拢于此，实施/验证类文档见 `04-设计器实施与验证/`。

## 文件清单

| 文件 | 作用 | 状态 |
|------|------|------|
| 审核规则库与工作流设计器-功能设计-V4.md | **权威总功能设计**（11 章模板）：JSON 规范、校验规则、图形化界面、NC 规则/报告章节配置 | 权威稿 |
| 自定义工作流引擎-功能设计-V1.md | **权威引擎详细设计**（V1.4）：自研轻量解释器、Skill 静态方法+反射、节点体系、单步调试、队列驱动执行 | 权威稿 |
| 图形化设计器-前端组件设计方案-V2.md | **权威前端设计**（11 章模板）：节点三层模型、同类编号、连线即数据绑定、多 end 提前结束、目录驱动、M1-M6 里程碑 | 权威稿 |
| 工作流引擎-总体架构设计-V2.md | **权威引擎总纲**（V2.1）：五条核心理念、路径驱动执行模型、三层任务模型（任务→任务项→节点）、证据链与审计设计 | 权威稿 |
| 工作流引擎选型与技术研究-V1.md | 引擎选型与技术研究（ADR），选型结论有效，Skill 体系部分见标注 | 参考稿 |
| 工作流节点定义与属性抽象-V1.md | 节点定义与属性抽象（含 loop 移除、end 语义收敛、AI 节点默认结果端口、§7.5 死循环防护） | 参考稿 |
| AI提示词规则-功能设计-V1.md | AI 提示词三段式契约：占位符 {{别名}}→{{code.result}}、输出契约、依赖图=边∪引用 | 参考稿 |

## 已归档讨论文档

> 以下文档是架构讨论过程中的重要成果，其核心设计思想已吸收进权威文档。

| 文件 | 讨论焦点 | 归档位置 |
|------|---------|---------|
| 工作流引擎完整架构设计-V1.md | 任务驱动架构、队列驱动执行、wf_execution_task 表设计 | docs/历史文档/归档-2026-08-xx-工作流架构讨论/ |
| 工作流节点参数组织与执行协议-V1.md | 前端节点元数据模型（NodeMeta/PortDef/PanelField） | docs/历史文档/归档-2026-08-xx-工作流架构讨论/ |
| 统一Skill执行器与AI-ToolUse设计-V1.md | ISkillExecutor 统一接口、Function Calling 机制 | docs/历史文档/归档-2026-08-xx-工作流架构讨论/ |

## 组织规则

- 命名：`{主题}-功能设计-V{N}.md`、`{主题}-技术研究-V{N}.md`。
- 设计变更 → 升版本号（V1→V2），旧版移 `docs/历史文档/`（扁平存放）。
- 本目录只保留最新版设计稿与 README。

## 依赖关系

- `审核规则库与工作流设计器-功能设计-V4.md` 为权威稿，定义 JSON 规范、校验规则、图形化界面。
- `自定义工作流引擎-功能设计-V1.md` 依赖 `工作流节点定义与属性抽象-V1.md`（节点定义/属性抽象）与 `AI提示词规则-功能设计-V1.md`（AI 节点提示词契约）。
- `图形化设计器-前端组件设计方案-V2.md` 依赖 `审核规则库与工作流设计器-功能设计-V4.md`（JSON 规范、校验规则、图形化界面）与 `自定义工作流引擎-功能设计-V1.md`（解释器、节点体系、单步调试），并引用 `AI提示词规则-功能设计-V1.md` 的 AI 节点规则。
- 引擎选型结论（`工作流引擎选型与技术研究-V1.md`）为引擎实现的技术依据。
- 设计器与引擎被 `NCConfig/index.vue`、`ReportDefinition.vue` 等页面引用实现。
- 实施与验证类文档见 `../04-设计器实施与验证/`：LogicFlow 实施分析与建议、实施 TODO、YZH 验证数据设计、V4 评审报告。

## 维护约定

- 设计稿必须按 `80-功能设计/README.md` 11 章模板编写，TODO 执行计划（第十章）必填。
- 进入实施前须完成本目录设计文档（文档先行）；实施后逻辑变化必须同步更新文档。
*（内容由AI生成，仅供参考）*
