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
| 数据库表设计-V1.md | **数据库表设计**：五大数据域、41 张表全貌、字段级定义、三链路数据流（提取/校验/报告）、Phase 划分 | 起步版 |
| 05-技术架构设计与ACM分析-V1.md | 系统分层架构、ACM 级别定位、规则引擎体系 | 成熟态 |
| 06-NETCore迁移与工作流移动端设计-V1.md | .NET Core 迁移方案、Elsa 工作流、移动端架构 | 成熟态 |
| 16-服务器性能与架构深度技术评估-V1.md | 量化性能评估：QPS、数据增量、硬件选型、安全架构 | 成熟态 |

---

## 关键词索引

`架构` `技术栈` `分层` `ACM` `工作流` `Elsa` `移动端` `服务器` `性能` `QPS` `硬件` `安全` `Docker` `Nginx` `部署` `缓存` `Redis` `消息队列` `RabbitMQ` `数据库` `表设计` `SQL` `ER` `企业` `审核` `NC` `证据` `报告` `数据域` `字段`

---

## 依赖关系

- 依赖 `00-工程体系/`：技术选型必须对齐宪法中的技术栈锁定
- 依赖 `30-范围与边界/`：架构设计受 MVP 范围约束

## 被哪些文件夹依赖

- `40-领域设计/`：领域建模需对齐架构分层
- `50-规划与优先级/`：排期基于架构复杂度
- `60-AI工程设计/`：宪法中的技术栈来源于本文件夹
*（内容由AI生成，仅供参考）*
