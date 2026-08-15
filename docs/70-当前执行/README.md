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
>
> **2026-08-15 清理**：无效/过时/已执行/原型/研究类文档共 16 份已归档至 `../历史文档/归档-2026-08-15-执行文档清理/`。

---

## 文档清单

### A. 标准目录 / OSS 存储（核心逻辑线）

| 文件 | 职责 | 状态 |
|------|------|------|
| **OSS存储结构重新设计-V1.md** | OSS 双顶层结构（standard-directory / enterprise-documents）唯一标准；V3 路径生成、文件名保留、后端单一约束 | **已全量实施**（2026-08-15） |
| 标准目录-编码体系与上传层级设计.md | SDC/FD/FL 编码体系、StoragePath V3 格式、FileCode 更新策略、BusinessKey（待实现） | 编码体系已实施 |
| 批量上传架构设计.md | 批量上传四段式（upload-init/v2/confirm/cancel），cancel=彻底清理 | 已实施 |
| 标准目录管理系统详细设计-V2.md | 标准目录管理系统详细设计 | 成熟态 |
| 标准目录管理-开发指南.md | 标准目录管理前端开发指南（DirectoryManager） | 已实现 |
| 数据库大改造-OSS存储-审核员业务链路-V1.md | 数据库大改造 + 企业资料审核员业务链路设计 | 设计 |

### B. 文档提取 / AI 分析（执行线）

| 文件 | 职责 | 状态 |
|------|------|------|
| 文档数据提取系统-设计文档-V3.md | 文档数据提取系统完整设计 | 待审核 |
| Phase3-标准文件code枢纽改造设计-V1.md | standard_file_code 枢纽改造：AI 分析 fileCode 双查询已实施；企业自动提取链路待实施 | 执行中（V1.1） |
| 文件数据提取能力落地-V1.md | 提取引擎基础能力（Extractor 模块） | 已实现（基础）/ 研究中（OCR） |

### C. Office 转换（执行线）

| 文件 | 职责 | 状态 |
|------|------|------|
| Office文档自动转换与MinIO路径重构实施文档-V2.md | Office 自动转换（yzh 队列 + LibreOffice 独立 profile）+ `.converted` 双存 | 已实施 |

### D. YZH-Framework / AI 引擎（执行线）

| 文件 | 职责 | 状态 |
|------|------|------|
| YZH-V3.0-架构设计文档.md | YZH-Framework V3.0 架构设计 | 开发中 |
| YZH-AI引擎详细设计-V1.md | AI 引擎四件套（SkillRegistry/LLM Gateway/PromptInterpreter/WorkflowEngine）详细设计 | 实施中（S5 已接入） |
| YZH-AI引擎-实施文档-V1.md | AI 引擎后台文件清单、运行逻辑、主链路时序、测试覆盖 | 已完成 V1.0 |
| YZH-前端架构v3设计-V1.md | 前端架构（样式令牌/组件库）+ certcore 通用层设计 | 待实施 |
| yzh-基础组件标准规范-V1.md | yzh 基础组件使用规范 | 待实施 |

### E. 自定义工作流（规划线）

| 文件 | 职责 | 状态 |
|------|------|------|
| LogicFlow工作流设计器实施分析与建议-V1.md | Phase E/F/G 完整实施计划（先接通 B-08/B-09 数据再启动设计器） | 成熟态 V1.0 |
| 工作流引擎选型与技术研究-V1.md | 自定义工作流技术研究（LogicFlow、双态 JSON、DAG） | 研究中/草案 |
| YZH特殊企业-工作流验证数据设计-V1.md | 特殊企业工作流验证数据设计 | 设计 |

---

## 关键词索引

`标准目录` `OSS存储` `standard-directory` `enterprise-documents` `StoragePath` `批量上传` `upload-init` `upload-cancel` `Office转换` `LibreOffice` `.converted` `文档提取` `AI分析` `standard_file_code` `fileCode` `YZH` `工作流` `AI引擎` `SkillRegistry` `LogicFlow` `B-08` `B-09`

---

## 依赖关系

- 依赖 `20-架构决策/`：执行方案基于架构决策的选型
- 依赖 `60-AI工程设计/`：AI 开发遵循方法论与知识库
- 完成后结论沉淀至 `40-领域设计/`、`20-架构决策/` 等正式目录
*（内容由AI生成，仅供参考）*
