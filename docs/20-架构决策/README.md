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
| 总体设计-V1.md | **顶层总体设计**：架构图、模块划分、ER图、API总纲、路由、部署、数据流、Phase0清单 | 成熟态 |
| 05-技术架构设计与ACM分析-V1.md | 系统分层架构、ACM 级别定位、规则引擎体系 | 成熟态 |
| 06-NETCore迁移与工作流移动端设计-V1.md | .NET Core 迁移方案、Elsa 工作流、移动端架构 | 成熟态 |
| 16-服务器性能与架构深度技术评估-V1.md | 量化性能评估：QPS、数据增量、硬件选型、安全架构 | 成熟态 |

---

## 关键词索引

`架构` `技术栈` `分层` `ACM` `工作流` `Elsa` `移动端` `服务器` `性能` `QPS` `硬件` `安全` `Docker` `Nginx` `部署` `缓存` `Redis` `消息队列` `RabbitMQ`

---

## 依赖关系

- 依赖 `00-工程体系/`：技术选型必须对齐宪法中的技术栈锁定
- 依赖 `30-范围与边界/`：架构设计受 MVP 范围约束

## 被哪些文件夹依赖

- `40-领域设计/`：领域建模需对齐架构分层
- `50-规划与优先级/`：排期基于架构复杂度
- `60-AI工程设计/`：宪法中的技术栈来源于本文件夹
*（内容由AI生成，仅供参考）*
