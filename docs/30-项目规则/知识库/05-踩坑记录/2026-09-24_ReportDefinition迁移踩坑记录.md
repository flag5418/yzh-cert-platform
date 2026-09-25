---
status: living
tags: [踩坑记录, 报告生成, EntityConfig, 外键, SqlSugar]
来源: 原 docs/30-项目规则/知识库/AGENTS.md 内嵌章节（2026-09-24 迁出）
---

# 2026-09-24 · ReportDefinition 迁移踩坑记录

> **背景**：报告模板/章节（`cert_report_template` / `rpt_audit_report` / `rpt_report_section`）
> 从历史架构迁移到 YZH 新架构时，在实体设计、外键、前后端映射、后端配置、序列化 5 类问题上连续踩坑。
> **迁移原因**：AGENTS.md 只放"AI 编码前必读"，踩坑明细属于知识库。

## 实体设计

- **`BaseEntity` 继承冲突**：`BaseEntity.Id` 是 `string`，但 `cert_report_template.Id` 是 `bigint auto_increment`，SqlSugar 报 `Ambiguous match found`。
  **解决**：不继承 `BaseEntity`，手动定义所有字段 + `[SugarColumn]`。
- **`rpt_report_section` 历史仅 `IsActive` + 可空 `enable`**：旧表曾无 `IsValid`。
  2026-09-24 起该表已有 `IsValid`（int），`enable` 已 DROP；Entity 仍只实现 `ISoftDelete`（章节开关继续用 `IsActive`）。
  > ✅ 2026-09-24 复核：`rpt_report_section` = `IsActive tinyint(1)` + `IsValid int`，无 `enable`，与上文一致。
  > ⚠️ 但同族的 `rpt_report_section_source` **仍有 `enable tinyint`**（与 `IsValid` 并存）→ 属待清理的 25 张表之一。

## 外键约束

- **`cert_report_template.CbCode` FK 失败**：`CbCode` 必须对应 `cert_certification_body.Code`。
  **解决**：显式设置 `CbCode = OrgCode`（同一 GUID）。
- **`rpt_report_section` FK 阻止插入**：`fk_section_clause`（→`cert_iso_clause`）、`fk_section_workflow`（→`wf_workflow_definition`）、`fk_section_report`（→`rpt_audit_report`）均指向空表或无意义。
  **解决**：`ALTER TABLE rpt_report_section DROP FOREIGN KEY fk_section_clause, fk_section_workflow, fk_section_report`。
- **`fk_rpttmpl_phase` 阻止保存**：FK 引用空表 `cert_phase_definition(Code)`，实际阶段数据在 `cert_cert_stage`。
  **解决**：`DROP FOREIGN KEY fk_rpttmpl_phase`。
- **`fk_rpttmpl_standard` 阻止保存**：FK 引用 `cert_iso_standard(Code)`，数据不一致。
  **解决**：`DROP FOREIGN KEY fk_rpttmpl_standard`。

## 前后端映射

- **树节点 `standardCode` 是显示码**（"ISO 13485:2016"），FK 需 GUID。**解决**：使用 `phase.stdCode`（GUID）。
- **树节点 `phaseCode` 是阶段码**（"S1"），FK 需 `cert_phase_definition.Code`（GUID）。**解决**：使用 `phase.phaseDefinitionCode`。
- **`YzhForm` 字段 `prop` 与 `reactive` 对象属性名不匹配**：`prop` 用 camelCase，但 `sectionForm` 用 PascalCase。
  **解决**：统一 PascalCase（呼应铁律七）。

## 后端配置

- **`[ApiController]` 导致 400**：框架自动把非空属性当必填校验。**解决**：移除 `[ApiController]`。
- **`InsertAsync` 不返回 ID**：`ExecuteCommandAsync` 不回填自增 ID。**解决**：改用 `ExecuteReturnEntityAsync()`。
- **`Result<T>` 无 `.Message` 属性**：YZH.Core 的 `Result<T>` 用 `.Error`。**解决**：改用 `Result<T>.Error`。
  > 关联：判成功只能用 `result.Success`（`=> Error == null`），**不是** `Code == 200`（`Ok()` 不设 `Code`）。

## 前端表单与后端序列化

- **模板保存 500 + `NullReferenceException`**：`templateForm` 用 camelCase，后端 `PropertyNamingPolicy = null` 期望 PascalCase。
  **解决**：`templateForm` 改 PascalCase 属性名，`handleSaveTemplate` 显式构建 PascalCase payload。
- **`YzhForm` 章节表单无响应**：`sectionFormFields` 的 `prop` 用 camelCase，`sectionForm` 用 PascalCase。**解决**：`prop` 改 PascalCase。
