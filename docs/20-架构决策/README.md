---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_1037bad0897811f18108525400287e28
    ReservedCode1: 5Uc/e3GL9Z2ssjExxxuN2OlfF3dqWYIoMS4oxFaQ/G5PcoTi4nSE/BwmWgVpU+Jqskfy5myyMvYRMGN3TCGYJRND1f2k9Acqw4tddidE01PPb6+MUSLYDRbAIFVBdywEbGBHiq5Wrtw2xZMFT/P1r4hvy4gqWqWvuSgKxVa56OKPoYLvW4Vq3NNw1cY=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_1037bad0897811f18108525400287e28
    ReservedCode2: 5Uc/e3GL9Z2ssjExxxuN2OlfF3dqWYIoMS4oxFaQ/G5PcoTi4nSE/BwmWgVpU+Jqskfy5myyMvYRMGN3TCGYJRND1f2k9Acqw4tddidE01PPb6+MUSLYDRbAIFVBdywEbGBHiq5Wrtw2xZMFT/P1r4hvy4gqWqWvuSgKxVa56OKPoYLvW4Vq3NNw1cY=
---

# 20-架构决策

> **作用**：技术选型与架构设计的权威来源。定义系统怎么搭、用什么技术、性能目标是什么。

---

## 文档清单

| 文件 | 职责 | 状态 |
|------|------|------|
| 总体设计-V3.md | **总体设计（最新版）**：企业中心模型、架构图、模块划分、功能清单、API 总纲、部署、Phase 1 清单 | 成熟态 |
| 数据库表设计-V2.md | **数据库表设计**：五大数据域、41 张表全貌、字段级定义、三链路数据流（提取/校验/报告）、Phase 划分 | 成熟态 |
| 体系认证平台架构设计-V1.0.md | 系统分层架构、技术选型、部署方案 | 成熟态 |
| CertPlatform基类架构设计-V1.0.md | YZH-Framework 基类设计、Vol 集成方案 | 成熟态 |
| 核心工作原理-V1.md | 审核流程、数据流转、关键业务逻辑 | 成熟态 |
| 审核端与后台管理端功能设计-V2.1.md | 前端功能设计、页面交互、组件规范 | 成熟态 |
| Phase1_实施报告.md | Phase 1 实施总结、经验教训 | 成熟态 |
| YZH-Framework架构设计评审报告-V1.md | YZH-Framework 架构设计评审分析 | 成熟态 |
| cert_phase2_implementation_summary.md | Phase 2 实施总结 | 成熟态 |
| 工作流引擎选型与技术研究-V1.md | 自定义工作流（提取/校验/报告）技术研究：Vol 审批流盘点、前端设计器选型（LogicFlow）、双态 JSON、DAG 解释器设计、四阶段路线 | 研究中/草案 |
| 文件数据提取能力落地-V1.md | 提取引擎基础能力落地：YZH.Core/Extractor 模块（NPOI+PdfPig）、统一结果模型、接口清单、使用示例、第三方 OCR 扩展点、真实样例验证记录、完善建议 | 已实现（基础）/ 研究中（OCR） |

---

## 关键词索引

`架构` `技术栈` `分层` `部署` `服务器` `性能` `Docker` `Nginx` `数据库` `表设计` `SQL` `ER` `企业` `审核` `NC` `证据` `报告` `数据域` `字段` `编码规则` `评审` `架构评估` `风险分析` `Vol` `边界` `YZH` `基类` `Phase` `实施`

---

## 依赖关系

- 依赖 `00-工程体系/`：技术选型必须对齐宪法中的技术栈锁定
- 依赖 `30-范围与边界/`：架构设计受 MVP 范围约束

## 被哪些文件夹依赖

- `40-领域设计/`：领域建模需对齐架构分层
- `50-规划与优先级/`：排期基于架构复杂度
- `60-AI工程设计/`：宪法中的技术栈来源于本文件夹
*（内容由AI生成，仅供参考）*
