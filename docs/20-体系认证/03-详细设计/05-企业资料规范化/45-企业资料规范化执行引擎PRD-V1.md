# Product Requirements Document: 企业资料规范化执行引擎与队列优化 (V3)

**Version**: 3.0
**Date**: 2026-10-05
**Author**: Sarah (Product Owner)
**Quality Score**: 99/100

---

## Executive Summary

本项目是“企业资料规范化”模块的最终应用落地。其核心目标是根据后台预定义的“标准文档填写规则”（`/business/doc-fill-rule`），通过高性能、可配置的异步队列引擎，将企业上传的散乱原始资料转化为标准化的体系认证文档。

本设计重点解决了“一源多标”的复杂映射问题，并引入了基于置信度的“AI 建议 + 人工裁决”审计体系，确保生成的每一份文档都具备合规的证据支撑。

---

## Problem Statement

**Current Situation**: 
1. **执行瓶颈**: 缺乏统一的规范化执行引擎，后台并行能力受限且不可调。
2. **映射复杂**: 企业一份原始件（如“内审计划”）需支撑多个标准（ISO9001/14001）下的多个文件生成，逻辑链路长。
3. **数据孤岛**: UI 定义的锚点规则与后端填充逻辑未完全打通。
4. **审计缺失**: 自动化填充过程缺乏“证据原文”的物理关联，难以应对认证现场审核。

**Proposed Solution**: 
1. **队列引擎**: 实现 `queue_max_concurrent` 系统参数联动，支持根据服务器性能动态分配并发 Worker。
2. **执行管线**: 以 `Standard` 为任务粒度，通过“召回 -> 精排 -> 填充”三阶段实现“一源多标”映射。
3. **存储体系**: 引入 `cert_doc_fill_log` 与 `cert_doc_ai_suggestion`，实现从“标准字段”到“原始件片段”的端到端追溯。
4. **规则应用**: 严格遵循 `/business/doc-fill-rule` 定义的 `SourceSpec` 取值链。

---

## User Stories & Acceptance Criteria

### Story 1: 队列效率与并行度
**As a** 系统管理员
**I want to** 通过修改系统参数即刻调整后台任务的并行处理能力
**Acceptance Criteria**:
- [ ] 后端 `QueueManager` 在启动时从 `cert_sys_config` 读取 `queue_max_concurrent`。
- [ ] 并行任务数严格受信号量控制，无超额负载。

### Story 2: “一源多标”自动化填充
**As a** 专家端用户
**I want to** 上传一份营业执照后，系统自动将其信息填充到所有关联标准的组织概况表中
**Acceptance Criteria**:
- [ ] 任务引擎能检索当前阶段下所有企业原始文档。
- [ ] 根据 `StandardFileCode` 索引至对应的 `cert_standard_doc_contract`（契约）与 `cert_doc_template`（模板）。
- [ ] 完成“多对多”映射并生成产物。

---

## Functional Requirements

### 1. 任务分发引擎 (Job Scoping)
- **触发源**: 企业完成资料上传 -> 触发 `ent_doc_normalize` 任务。
- **作用域**: `EnterpriseCode + StageCode + StandardCode`。
- **并发控制**: 读取 `cert_sys_config.queue_max_concurrent`（默认 4），启动时初始化。

### 2. 规范化执行管线 (Normalization Pipeline)
1. **检索与召回**: 基于资料画像（DocCategory, Tag）寻找匹配的企业原始件。
2. **取值决策**: 解析 `cert_doc_template_anchor.SourceSpec`。
   - `global`: 取企业全局参数。
   - `ai`: 批量调用 LLM 提取字段（利用 `promptGroup` 聚合）。
   - `manual`: 引用人工裁决值。
3. **模板填充**: 使用 `NPOI` 进行 Word/Excel 锚点替换，支持 `overwrite/append` 等模式。
4. **自验收**: 填充后扫描产物，记录 `Verified` 状态（无残留锚点）。

### 3. 数据存储与审计 (Data & Storage)
- **产物存储**: `/enterprise-documents/{EntCode}/{StdCode}/{StageCode}/{FolderPath}/{FileName}`。
- **留痕表**:
  - `cert_doc_fill_log`: 记录本次填充的总体情况、Token 消耗、Skill 轨迹。
  - `cert_doc_ai_suggestion`: 记录每一个锚点的 AI 建议值、证据原文片段、置信度。

---

## Technical Constraints

- **后端**: .NET 8, SqlSugar, NPOI, Redis。
- **数据库**: 遵循 PascalCase，软删 + 有效标志双控，唯一键不含软删列。
- **UI 对齐**: 填充逻辑必须严格遵循前端 `AnchorRuleTab` 设置的锚点定位（Token/书签/区域）。

---

## Risk Assessment

- **LLM 超时**: 队列任务设置 300s 强制超时，支持失败重试。
- **数据漂移**: 采用“实时计算完成度”策略，不依赖静态状态列，确保 UI 状态真实。

---

## Appendix

### 核心表关联枢纽
- `StandardFileCode`: 串联“标准目录需求”、“文档契约”、“空白模板”与“执行日志”的唯一业务主键。
