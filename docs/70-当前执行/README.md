---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_1dc5117a953411f1b6b5525400287e28
    ReservedCode1: y8FuTifN8zH8HR4ipUcIbQ2rnuZO1M8ijp5NIHwj2vQ0GzoJ91ORNBD8PHWNNSlV/iAutf/61hVXENi5VYtn4paV1lHLIMrqRiUHtiWS9DQXXMWn3jYiyfKr/S+7hfN8bB5m4i0S2kutNsiUyT4s6NJJ3Z7wsmyFRpDzRfOl+CyPVtg1y9l0YLNnTFg=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_1dc5117a953411f1b6b5525400287e28
    ReservedCode2: y8FuTifN8zH8HR4ipUcIbQ2rnuZO1M8ijp5NIHwj2vQ0GzoJ91ORNBD8PHWNNSlV/iAutf/61hVXENi5VYtn4paV1lHLIMrqRiUHtiWS9DQXXMWn3jYiyfKr/S+7hfN8bB5m4i0S2kutNsiUyT4s6NJJ3Z7wsmyFRpDzRfOl+CyPVtg1y9l0YLNnTFg=
---

# 70-当前执行

> **作用**：当前正在执行（开发中/实施中/待实施/待审核/研究中）的文档集中归集处。方便查阅与审核项目是否按文档推进。
>
> **维护原则**：文档进入实施/开发阶段时移入本目录；完成后将实施报告等结论沉淀到对应领域目录，本目录仅保留进行中文档。

---

## 文档清单

### A. Office 文档转换与 MinIO 路径重构（执行线）

| 文件 | 职责 | 状态 |
|------|------|------|
| 当前项目规则整理 + 下次开发 TODO 清单-V1.md | 下次开发任务分解（DB 4 字段 / NPOI xls→xlsx / LibreOffice doc→docx / ConvertStatus 徽标 / 历史补跑） | 成熟态 V1.0 |
| Office文档自动转换与MinIO路径重构实施文档-V2.md | Office 自动转换 + MinIO 双存路径重构实施步骤 | 实施中 |
| MinIO数据清理与实施前准备清单.md | MinIO 存量数据清理与实施前检查清单 | 等待手动执行 |
| 旧版 Office 文档后端自动转换方案评估-V1（doc→docx xls→xlsx）.md | xls→xlsx（NPOI）vs doc→docx（LibreOffice）方案对比、DB 字段、MinIO 双存约定 | 草案/待开发排期 |

### B. 标准目录管理 / 文档提取（执行线）

| 文件 | 职责 | 状态 |
|------|------|------|
| 标准目录管理系统详细设计-V2.md | 标准目录管理系统最新设计（历史文档替代对象） | 成熟态 |
| 标准目录管理-开发指南.md | 标准目录管理前端开发指南 | 开发中 |
| 标准目录-编码体系与上传层级设计.md | 标准目录编码体系 + 上传层级设计 | 设计讨论稿，待实现 |
| 文档数据提取系统-设计文档-V3.md | 文档数据提取系统完整设计 | 待审核 |
| 文件结构设置开发建议-V1.md | 文件目录结构配置建议 | 建议稿 |
| 批量上传架构设计.md | 批量上传架构设计 | 设计阶段 |
| 前端原型-文档提取规则管理-V2.html | 文档提取规则管理原型（V2） | 原型 |
| 前端原型-文档提取规则管理.html | 文档提取规则管理原型 | 原型 |
| 标准目录管理-原型.html | 标准目录管理原型 | 原型 |
| 原型-标准目录管理系统.html | 标准目录管理系统原型 | 原型 |

### C. YZH-Framework 升级（执行线）

| 文件 | 职责 | 状态 |
|------|------|------|
| YZH-V3.0-架构设计文档.md | YZH-Framework V3.0 架构设计 | 开发中 |
| YZH-AI引擎详细设计-V1.md | YZH-AI引擎四件套（SkillRegistry/LLM Gateway/PromptInterpreter/WorkflowEngine）L2 落地级详细设计，三引擎复用统一基础设施，含 8 张 Mermaid 设计图 | 待实施 |
| YZH-前端框架建设方案-V1.0-待审批版.md | 前端框架建设方案 | 待审批 |
| YZH-Framework-V2.0架构设计升级方案.md | V2.0 架构升级方案 | 设计阶段 |
| YZH-Framework-V2.0-TODO清单.md | V2.0 升级 TODO 清单 | 待实施 |
| BlazorServer与YZH-Framework对比分析.md | BlazorServer 与 YZH-Framework 框架对比 | 分析 |

### D. 技术研究（执行线）

| 文件 | 职责 | 状态 |
|------|------|------|
| 工作流引擎选型与技术研究-V1.md | 自定义工作流技术研究（LogicFlow、双态 JSON、DAG） | 研究中/草案 |
| 文件数据提取能力落地-V1.md | 提取引擎基础能力落地（Extractor 模块） | 已实现（基础）/ 研究中（OCR） |

---

## 关键词索引

`执行中` `开发中` `实施中` `待实施` `待审核` `待审批` `研究中` `草案` `TODO` `Phase` `Office转换` `MinIO` `NPOI` `LibreOffice` `标准目录` `文档提取` `批量上传` `YZH` `工作流` `原型`

---

## 依赖关系

- 依赖 `20-架构决策/`：执行方案基于架构决策的选型
- 依赖 `60-AI工程设计/`：AI 开发遵循方法论与知识库
- 完成后结论沉淀至 `40-领域设计/`、`20-架构决策/` 等正式目录
*（内容由AI生成，仅供参考）*
