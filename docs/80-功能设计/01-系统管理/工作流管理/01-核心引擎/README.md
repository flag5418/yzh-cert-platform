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
| 审核规则库与工作流设计器-功能设计-V4.md | 当前唯一权威设计稿（11 章模板）：JSON 规范、校验规则、图形化界面 | 权威稿 |
| 自定义工作流引擎-功能设计-V1.md | 解释器、节点体系、单步调试 | 设计稿 |
| 工作流引擎选型与技术研究-V1.md | 引擎选型与技术研究（ADR） | 设计稿 |
| 工作流节点定义与属性抽象-V1.md | 节点定义与属性抽象（含 loop 移除、end 语义收敛、AI 节点默认结果端口、§7.5 死循环防护） | 设计稿 |
| AI提示词规则-功能设计-V1.md | AI 提示词三段式契约：占位符 {{别名}}→{{code.result}}、输出契约、依赖图=边∪引用 | 设计稿 |
| 图形化设计器-前端组件设计方案-V2.md | 当前权威设计稿（11 章模板）：节点三层模型、同类编号、连线即数据绑定、多 end 提前结束、目录驱动、M1-M6 里程碑 | 设计稿 |
| 图形化设计器-前端组件设计方案-V1.md | V1 稿（已归档至 docs/历史文档/） | 已归档 |

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
