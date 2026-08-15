# 大改造执行计划 TODO

> **创建日期**：2026-08-14  
> **前置**：已 git commit 备份当前代码 (commit: 5e33d4e)  
> **原则**：废弃的表彻底删除，包括后端 .cs 代码文件

> ⚠️ **状态更新（2026-08-15）**：本计划的 Phase 1.2 / Phase 2（DROP `cert_standard_directory_*`、`cert_upload_task` 及删除 `StandardDirectoryService` 等）**已被后续决策推翻**——标准目录管理保留并持续扩展（批量上传四段式 + 转换队列 + 文档提取均已落地），`cert_upload_task` 已恢复；Phase 3/4 的域 A 实体重建与 snake_case 映射已完成。下方勾选状态不代表当前代码，执行前请以实际代码为准。

---

## Phase 1: 清空环境（MinIO + 数据库废弃表）

### 1.1 清空 MinIO 存储
- [ ] 删除 cert-platform bucket 中所有现有文件（`/CB001/` 下全部内容）

### 1.2 数据库废弃表 DROP（8 张表）
- [ ] DROP TABLE `cert_enterprise`（→ 迁移到 `ent_enterprise`）
- [ ] DROP TABLE `cert_registration`（→ 审核员注册直接写 Sys_User）
- [ ] DROP TABLE `cert_application`（→ 用 `ent_enterprise_phase` 替代）
- [ ] DROP TABLE `cert_upload_task`（→ 用 `yzh_queue_task` 替代）
- [ ] DROP TABLE `cert_standard_directory_config`（→ 合并到 `cert_directory_template`）
- [ ] DROP TABLE `cert_standard_directory_folder`（→ 合并到 `cert_directory_template`）
- [ ] DROP TABLE `cert_standard_directory_file`（拆分：模板→`cert_file_requirement`，实际→`ent_enterprise_file`）
- [ ] DROP TABLE `cert_org_config`（→ 收敛到 `cert_certification_body`）

### 1.3 数据库 V2 空表 DROP（重建用，7 张表）
- [ ] DROP TABLE `ent_enterprise`（重建补字段）
- [ ] DROP TABLE `ent_enterprise_phase`（重建）
- [ ] DROP TABLE `ent_enterprise_document`（重建）
- [ ] DROP TABLE `ent_enterprise_file`（重建）
- [ ] DROP TABLE `ent_extraction_result`（重建补字段）
- [ ] DROP TABLE `ent_table_extraction_result`（重建补字段）
- [ ] DROP TABLE `audit_task`（重建）

---

## Phase 2: 删除废弃表对应的后端 C# 代码

### 2.1 删除 Entity 实体类（8 个文件）
- [ ] 删除 `VOL.Entity/CertPlatform/Dir/StandardDirectoryConfig.cs`
- [ ] 删除 `VOL.Entity/CertPlatform/Dir/StandardDirectoryFolder.cs`
- [ ] 删除 `VOL.Entity/CertPlatform/Dir/StandardDirectoryFile.cs`
- [ ] 删除 `VOL.Entity/CertPlatform/Dir/UploadTask.cs`
- [ ] 删除 `VOL.Entity/CertPlatform/Dir/UploadManifestDto.cs`
- [ ] 删除 `VOL.Entity/CertPlatform/Dir/UploadFileDto.cs`
- [ ] 删除 `VOL.Entity/CertPlatform/Sys/CertMessage.cs`（→ 后续改名 `sys_message`）
- [ ] 删除 `VOL.Entity/CertPlatform/Sys/CertOrgStandard.cs`（→ 改名 `cert_cb_standard`）
- [ ] 删除 `VOL.Entity/CertPlatform/Sys/CertOrgStage.cs`（→ 改名 `cert_cb_stage`）
- [ ] 删除 `VOL.Entity/CertPlatform/Sys/CertOrgStandardView.cs`
- [ ] 删除 `VOL.Entity/CertPlatform/Sys/CertOrgStageView.cs`

### 2.2 删除 Service 服务类
- [ ] 删除 `VOL.Builder/Services/CertPlatform/StandardDirectoryService.cs`
- [ ] 删除 `VOL.Builder/Services/CertPlatform/Helpers/FileStorageService.cs`
- [ ] 删除 `VOL.Builder/Services/CertPlatform/Helpers/FolderFileManager.cs`
- [ ] 删除 `VOL.Builder/Services/CertPlatform/MessageService.cs`
- [ ] 删除 `VOL.Builder/Services/CertPlatform/OfficeConvertService.cs`
- [ ] 删除 `VOL.Builder/Services/CertPlatform/OfficeConvertTaskExecutor.cs`
- [ ] 删除 `VOL.Builder/Services/CertPlatform/CertQueueNotifier.cs`
- [ ] 删除 `VOL.Builder/Services/CertPlatform/FileConvertPayload.cs`

### 2.3 删除 Interface 接口
- [ ] 删除 `VOL.Builder/IServices/CertPlatform/IStandardDirectoryService.cs`
- [ ] 删除 `VOL.Builder/IServices/CertPlatform/IFileStorageService.cs`
- [ ] 删除 `VOL.Builder/IServices/CertPlatform/IFolderFileManager.cs`
- [ ] 删除 `VOL.Builder/IServices/CertPlatform/IMessageService.cs`
- [ ] 删除 `VOL.Builder/IServices/CertPlatform/IConvertNotifier.cs`

### 2.4 删除 Controller 控制器
- [ ] 删除 `VOL.WebApi/Controllers/CertPlatform/StandardDirectoryController.cs`

### 2.5 删除其他关联文件
- [ ] 删除 `VOL.Builder/Services/CertPlatform/OrgLinkService.cs`（引用 cert_org_config 等）
- [ ] 删除 `VOL.Builder/IServices/CertPlatform/IOrgLinkService.cs`

---

## Phase 3: 数据库重建（snake_case 列名 + V2 规范）

### 3.1 重建域 A 配置表（改 PascalCase → snake_case）
- [ ] 重建 `cert_certification_body`（收敛 cert_org_config 字段）
- [ ] 重建 `cert_iso_standard`
- [ ] 重建 `cert_iso_clause`
- [ ] 重建 `cert_phase_definition`
- [ ] 重建 `cert_standard_phase_config`
- [ ] 重建 `cert_directory_template`（合并原标准目录文件夹职责）
- [ ] 重建 `cert_file_requirement`（合并原标准目录文件模板职责）
- [ ] 重建 `cert_extraction_rule`
- [ ] 重建 `cert_extraction_field`
- [ ] 重建 `cert_validation_rule`
- [ ] 重建 `cert_validation_rule_source`
- [ ] 重建 `cert_report_template`
- [ ] 重建 `cert_clause_extraction_rule`

### 3.2 重建域 B 企业表
- [ ] 重建 `ent_enterprise`（补 enterprise_no、province、city、industry_type 等字段）
- [ ] 重建 `ent_enterprise_phase`
- [ ] 重建 `ent_enterprise_document`
- [ ] 重建 `ent_enterprise_file`
- [ ] 新建 `ent_file_version`
- [ ] 新建 `ent_file_pre_check_result`
- [ ] 新建 `ent_file_compliance_check`
- [ ] 重建 `ent_extraction_result`（补 enterprise_code、phase_code）
- [ ] 重建 `ent_table_extraction_result`（补 enterprise_code、phase_code）

### 3.3 重建域 C 审核执行表
- [ ] 重建 `audit_task`
- [ ] 新建 `audit_checklist_item`
- [ ] 新建 `audit_nonconformity`
- [ ] 新建 `audit_finding`
- [ ] 新建 `audit_evidence`
- [ ] 新建 `audit_rectification`

### 3.4 新建域 D 报告表
- [ ] 新建 `rpt_report_task`
- [ ] 新建 `rpt_audit_report`
- [ ] 新建 `rpt_report_section`
- [ ] 新建 `rpt_report_section_source`

### 3.5 新建审核员资质表
- [ ] 新建 `cert_auditor_profile`

---

## Phase 4: C# 实体类重建（[Column] snake_case 映射）

### 4.1 重建域 A 实体类（13 个）
- [ ] `CertificationBody.cs`
- [ ] `ISOStandard.cs`
- [ ] `ISOClause.cs`
- [ ] `PhaseDefinition.cs`
- [ ] `StandardPhaseConfig.cs`
- [ ] `DirectoryTemplate.cs`
- [ ] `FileRequirement.cs`
- [ ] `ExtractionRule.cs`
- [ ] `ExtractionField.cs`
- [ ] `ValidationRule.cs`
- [ ] `ValidationRuleSource.cs`
- [ ] `ReportTemplate.cs`
- [ ] `ClauseExtractionRule.cs`

### 4.2 重建域 B 实体类（9 个）
- [ ] `Enterprise.cs`（补 EnterpriseNo、Province、City 等）
- [ ] `EnterprisePhase.cs`
- [ ] `EnterpriseDocument.cs`
- [ ] `EnterpriseFile.cs`
- [ ] `FileVersion.cs`
- [ ] `FilePreCheckResult.cs`
- [ ] `FileComplianceCheck.cs`
- [ ] `ExtractionResult.cs`
- [ ] `TableExtractionResult.cs`

### 4.3 重建域 C 实体类（6 个）
- [ ] `AuditTask.cs`
- [ ] `ChecklistItem.cs`
- [ ] `NonConformity.cs`
- [ ] `AuditFinding.cs`
- [ ] `AuditEvidence.cs`
- [ ] `Rectification.cs`

### 4.4 新建域 D 实体类（4 个）
- [ ] `ReportTask.cs`
- [ ] `AuditReport.cs`
- [ ] `ReportSection.cs`
- [ ] `ReportSectionSource.cs`

### 4.5 新建审核员资质实体
- [ ] `AuditorProfile.cs`

---

## Phase 5: 路径生成 + 核心服务重建

### 5.1 更新 CodeGeneratorService.cs
- [x] 新增 `GenerateStandardDirectoryPath()` 方法（✅ 已实现）
- [x] 新增 `GenerateEnterpriseDocumentPath()` 方法（✅ 已实现）
- [x] 新增 `GenerateConvertedStoragePath()` 方法（✅ 已实现）
- [ ] 废弃旧 `GenerateStoragePathV2()` 方法（保留兼容，上传流程已切换到 V3）

### 5.2 更新 ICodeGeneratorService.cs 接口
- [x] 同步新增方法签名（✅ 已实现）

### 5.3 重建标准目录相关 Service（精简版）
- [x] 重建 `StandardDirectoryService.cs`（✅ 已实现：标准目录管理 + 批量上传到 standard-directory 路径）
- [x] 重建 `IStandardDirectoryService.cs`

### 5.4 新建企业文件 Service
- [x] 新建 `EnterpriseFileService.cs`（✅ 已实现：企业文件上传到 enterprise-documents 路径）
- [x] 新建 `IEnterpriseFileService.cs`

---

## Phase 6: 编译验证 + 修复错误

### 6.1 后端编译
- [ ] `dotnet build` 编译后端项目
- [ ] 修复 CS0234/CS0246（找不到命名空间/类）
- [ ] 修复 CS0108（隐藏基类成员）
- [ ] 修复其他编译错误
- [ ] 零编译错误通过

### 6.2 数据库脚本执行
- [ ] 执行所有建表 SQL 脚本
- [ ] `DESCRIBE` 验证每张表结构正确
- [ ] 验证 snake_case 列名

### 6.3 端到端验证
- [ ] 后端启动成功（端口 9992）
- [ ] 前端编译通过
- [ ] MinIO 路径验证：标准目录路径 = `/standard-directory/CB001/...`
- [ ] MinIO 路径验证：企业资料路径 = `/enterprise-documents/ENT-2026-0001/CB001/...`
- [ ] 数据库表结构符合 V2 规范（snake_case + 基类字段 + code 关联）

---

## 验证完成情况汇总

| Phase | 任务数 | 完成 | 状态 |
|-------|--------|------|------|
| Phase 1: 清空环境 | 16 | 0 | ⬜ 待执行 |
| Phase 2: 删除废弃代码 | 20 | 0 | ⬜ 待执行 |
| Phase 3: 数据库重建 | 33 | 0 | ⬜ 待执行 |
| Phase 4: C# 实体类重建 | 33 | 0 | ⬜ 待执行 |
| Phase 5: 路径生成+服务重建 | 7 | 0 | ⬜ 待执行 |
| Phase 6: 编译验证 | 8 | 0 | ⬜ 待执行 |
| **合计** | **117** | **0** | ⬜ 待执行 |
