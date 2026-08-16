---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_0a3e9c9d991d11f19467525400287e28
    ReservedCode1: UR7zodeiNrsYk02PBBUWCT9auOjbF/I/0j0Ha5vLcXtVOmenfhWm0raYcoFIrwqnL7Mbe3beq5zA1MXmTy0qwfQJzVyS8FylrfNBYiq5EgJ3bpBIFK5NAlw2GOjk8IrQTt7WUPZwsUEi0Z/d1T0A/OekMn4psLrzeI+R+euBgpAh+vH6D8+wYFM/420=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_0a3e9c9d991d11f19467525400287e28
    ReservedCode2: UR7zodeiNrsYk02PBBUWCT9auOjbF/I/0j0Ha5vLcXtVOmenfhWm0raYcoFIrwqnL7Mbe3beq5zA1MXmTy0qwfQJzVyS8FylrfNBYiq5EgJ3bpBIFK5NAlw2GOjk8IrQTt7WUPZwsUEi0Z/d1T0A/OekMn4psLrzeI+R+euBgpAh+vH6D8+wYFM/420=
---

# 20-架构决策

> **作用**：**系统全局概览**——存放与系统全局有关的内容：总体设计、数据库设计、整体架构/流程、技术选型。**打开本文件夹即知项目概况**（全局数据库、大的功能设计及流程的初步了解）；功能细节一律下沉到 `80-功能设计/`（改功能设计快速查 80）。
>
> **2026-08-16 目录重构**：定位由"架构权威基线"调整为"系统全局概览"；原置于本目录的功能设计类文档已迁出至 `80-功能设计/`（审核端与后台管理端功能设计-V2.2、文件转换队列化设计方案 V1/V2、队列中心通用设计方案-V3）。

---

## 文档清单

| 文件 | 职责 | 状态 |
|------|------|------|
| 总体设计-V3.md | **总体设计（最新版）**：企业中心模型、架构图、模块划分、功能清单、API 总纲、部署、Phase 1 清单 | 成熟态 |
| 数据库表设计-V2.md | **数据库表设计**：五大数据域、41 张表全貌、字段级定义、三链路数据流（提取/校验/报告）、Phase 划分 | 成熟态 |
| CertPlatform基类架构设计-V1.0.md | YZH-Framework 基类设计、Vol 集成方案 | 成熟态 |
| 核心工作原理-V1.md | 审核流程、数据流转、关键业务逻辑 | 成熟态 |
| CertPlatform业务流程与数据链路-V2.1.md | 业务流程与数据链路（联调版） | 成熟参考 |
| Phase1_实施报告.md | Phase 1 实施总结、经验教训 | 成熟态 |
| YZH-Framework架构设计评审报告-V1.md | YZH-Framework 架构设计评审分析 | 成熟态 |
| cert_phase2_implementation_summary.md | Phase 2 实施总结 | 成熟态 |

> 注：功能细节设计（标准目录、文档提取、Office 转换、队列、工作流、审核端前端等）已移至 `80-功能设计/`，本目录不再存放功能细节。

---

## 关键词索引

`架构` `总体设计` `数据库` `表设计` `SQL` `ER` `技术栈` `分层` `部署` `服务器` `性能` `Docker` `Nginx` `企业` `审核` `NC` `证据` `报告` `数据域` `字段` `编码规则` `评审` `架构评估` `风险分析` `Vol` `边界` `YZH` `基类` `Phase` `实施`

---

## 依赖关系

- 依赖 `00-工程体系/`：技术选型必须对齐宪法中的技术栈锁定
- 依赖 `50-规划与优先级/`：排期基于架构复杂度

## 被哪些文件夹依赖

- `80-功能设计/`：功能设计必须对齐全局架构/数据库设计
- `50-规划与优先级/`：排期基于架构复杂度
- `60-AI工程设计/`：宪法中的技术栈来源于本文件夹
*（内容由AI生成，仅供参考）*
