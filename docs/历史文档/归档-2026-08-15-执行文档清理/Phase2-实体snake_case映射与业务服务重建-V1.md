# Phase 2 实施方案：实体 snake_case 映射 + 业务服务重建 + 文档内容保存链路

> **版本**：V1.0  
> **创建日期**：2026-08-14  
> **前置完成**：Phase 1 大改造（commit `68119f1`），已完成：MinIO 清空、34 张表 V3 重建、废弃代码删除、7 个核心实体 + YZHBaseEntity 基类 `[Column]` 映射、CodeGeneratorService V3 路径生成  
> **本方案定位**：Phase 1 的延续，补全剩余 26 个实体类的 `[Column]` 映射、重建核心业务服务、打通文档提取规则→提取结果保存的完整链路

---

## 一、当前状态盘点

### 1.1 Phase 1 已完成清单

| 项目 | 状态 | 说明 |
|------|:---:|------|
| MinIO 存储清空 | ✅ | `cert-platform` bucket 完全清空 |
| 数据库 34 张表 V3 重建 | ✅ | 全部 snake_case 列名，审计字段统一 |
| 废弃 C# 代码删除 | ✅ | Entity 11 个 + Service 8 个 + Interface 7 个 + Controller 4 个 |
| YZHBaseEntity 基类 `[Column]` | ✅ | 14 个基类属性已添加 snake_case 映射 |
| 7 个核心实体类重建 | ✅ | Enterprise/EnterpriseFile/EnterprisePhase/EnterpriseDocument/AuditTask/CertificationBody/AuditorProfile |
| CodeGeneratorService V3 | ✅ | 3 个新方法：标准目录/企业文档/转换路径 |
| 后端编译通过 | ✅ | 0 错误 0 警告 |
| 后端启动成功 | ✅ | 端口 9992 正常监听 |

### 1.2 Phase 2 待完成清单

| # | 任务 | 文件数 | 优先级 |
|---|------|:---:|:---:|
| 1 | 剩余 26 个实体类添加 `[Column("snake_case")]` | 26 | P0 |
| 2 | 旧实体类字段对齐 V3 表结构（补字段/改类型） | ~10 | P0 |
| 3 | `cert_doc_*` 系列表/实体对齐（审计字段 new 覆盖→统一） | 4 | P1 |
| 4 | 企业文件上传服务重建 `EnterpriseFileService` | 3 | P1 |
| 5 | 标准目录管理服务重建 `DirectoryTemplateService` | 3 | P1 |
| 6 | 文档提取规则→提取结果保存链路打通 | 2 | P1 |
| 7 | `DocExtractionRuleService.AI.cs` TempFileInfo→EnterpriseFile 替换 | 1 | P2 |
| 8 | 前端页面适配新 API | ~5 | P2 |
| 9 | 编译验证 + 端到端测试 | — | P0 |

---

## 二、剩余 26 个实体类 `[Column]` 映射详单

### 2.1 域 A：认证体系配置（12 个待修复）

> 路径：`VOL.Entity/CertPlatform/Cert/`

| # | 文件名 | 表名 | 当前状态 | 需要做的事 |
|---|--------|------|----------|-----------|
| 1 | `ISOStandard.cs` | `cert_iso_standard` | 属性无 `[Column]` | 给 `StandardCode`/`StandardName`/`VersionYear` 添加 `[Column]`，补 `Category`/`Description` 字段 |
| 2 | `ISOClause.cs` | `cert_iso_clause` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `ParentCode`/`ClauseNumber`/`Title`/`Description`/`SortOrder` |
| 3 | `PhaseDefinition.cs` | `cert_phase_definition` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `PhaseCode`/`PhaseName`/`SequenceOrder`/`Description` |
| 4 | `StandardPhaseConfig.cs` | `cert_standard_phase_config` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `RequiredClauses`/`RequiredFiles` JSON 字段 |
| 5 | `DirectoryTemplate.cs` | `cert_directory_template` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `ConfigCode`/`ParentCode`/`FolderName`/`SortOrder` |
| 6 | `FileRequirement.cs` | `cert_file_requirement` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `FolderCode`/`FileNameTemplate`/`FileType`/`IsRequired`/`MaxSizeMb`/`Description`/`SortOrder` |
| 7 | `ExtractionRule.cs` | `cert_extraction_rule` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `FileRequirementCode`/`SkillCode`/`RuleType`/`RuleConfig`/`Description`/`IsActive` |
| 8 | `ExtractionField.cs` | `cert_extraction_field` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `RuleCode`/`SkillCode`/`FieldCode`/`LabelTag`/`FieldName`/`FieldType`/`EnumValues`/`SortOrder` |
| 9 | `ValidationRule.cs` | `cert_validation_rule` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `StandardCode`/`PhaseCode`/`ClauseCode`/`WorkflowCode`/`RuleCode`/`RuleName`/`SeverityIfViolated`/`NcDescriptionTemplate`/`IsActive` |
| 10 | `ValidationRuleSource.cs` | `cert_validation_rule_source` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `RuleCode`/`FileRequirementCode`/`SourcePath` |
| 11 | `ReportTemplate.cs` | `cert_report_template` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `CbCode`/`StandardCode`/`PhaseCode`/`TemplateName`/`TemplateFilePath`/`SectionConfig`/`IsDefault` |
| 12 | `ClauseExtractionRule.cs` | `cert_clause_extraction_rule` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `ClauseCode`/`WorkflowCode`/`Description` |

### 2.2 域 B：企业档案（5 个待修复）

> 路径：`VOL.Entity/CertPlatform/Ent/`

| # | 文件名 | 表名 | 当前状态 | 需要做的事 |
|---|--------|------|----------|-----------|
| 13 | `ExtractionResult.cs` | `ent_extraction_result` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `EnterpriseCode`/`PhaseCode` 字段 |
| 14 | `TableExtractionResult.cs` | `ent_table_extraction_result` | 属性无 `[Column]` | 给所有属性添加 `[Column]`，补 `EnterpriseCode`/`PhaseCode` 字段 |
| 15 | `FileVersion.cs` | `ent_file_version` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 16 | `FilePreCheckResult.cs` | `ent_file_pre_check_result` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 17 | `FileComplianceCheck.cs` | `ent_file_compliance_check` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |

### 2.3 域 C：审核执行（5 个待修复）

> 路径：`VOL.Entity/CertPlatform/Audit/`

| # | 文件名 | 表名 | 当前状态 | 需要做的事 |
|---|--------|------|----------|-----------|
| 18 | `ChecklistItem.cs` | `audit_checklist_item` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 19 | `NonConformity.cs` | `audit_nonconformity` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 20 | `AuditFinding.cs` | `audit_finding` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 21 | `AuditEvidence.cs` | `audit_evidence` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 22 | `Rectification.cs` | `audit_rectification` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |

### 2.4 域 D：报告生成（4 个待修复）

> 路径：`VOL.Entity/CertPlatform/Rpt/`

| # | 文件名 | 表名 | 当前状态 | 需要做的事 |
|---|--------|------|----------|-----------|
| 23 | `ReportTask.cs` | `rpt_report_task` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 24 | `AuditReport.cs` | `rpt_audit_report` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 25 | `ReportSection.cs` | `rpt_report_section` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |
| 26 | `ReportSectionSource.cs` | `rpt_report_section_source` | 属性无 `[Column]` | 给所有属性添加 `[Column]` |

### 2.5 旧实体类兼容性修复（4 个）

| # | 文件名 | 表名 | 问题 | 修复方案 |
|---|--------|------|------|----------|
| 27 | `CertStage.cs` | `cert_cert_stage` | 表已 DROP，V3 中用 `cert_phase_definition` 替代 | 删除文件，或改为引用 `cert_phase_definition` 表 |
| 28 | `ISOStandardView.cs` | `v_iso_standard` | 视图已 DROP | 删除文件，后续按需重建视图 |
| 29 | `CertStageView.cs` | `v_cert_stage` | 视图已 DROP | 删除文件 |
| 30 | `FieldLabelMapping.cs` | `wf_field_label_mapping` | 外键已删除，表仍存在 | 检查列名是否需要 `[Column]` 映射 |

---

## 三、`cert_doc_*` 系列实体统一改造

### 3.1 问题描述

当前 `cert_doc_*` 系列 4 张表使用 `new` 关键字覆盖基类属性来适配 snake_case 列名（如 `[Column("create_id")] public new int? CreateID`），这种方式有如下问题：

1. `new` 覆盖会阻断基类方法（`FillCreateInfo`/`FillModifyInfo`）对属性的赋值
2. 部分属性被 `[NotMapped]` 标记为不映射，导致数据库列无法通过 EF Core 读写
3. `update_id`/`update_date` 列名与 V3 中的 `modify_id`/`modify_date` 不一致

### 3.2 改造方案

**方案 A（推荐）**：修改 `cert_doc_*` 表结构，使审计字段列名与 YZHBaseEntity 一致（`create_id`/`creator`/`create_date`/`modify_id`/`modifier`/`modify_date`/`delete_id`/`deleter`/`delete_time`），然后删除实体类中所有 `new` 覆盖。

**方案 B**：保持表结构不变，在实体类中继续使用 `new` 覆盖（不推荐，维护成本高）。

### 3.3 方案 A 执行步骤

```sql
-- 1. cert_doc_extraction_rule 表
ALTER TABLE cert_doc_extraction_rule 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL;

-- 2. cert_doc_field_def 表
ALTER TABLE cert_doc_field_def 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL;

-- 3. cert_doc_table_def 表
ALTER TABLE cert_doc_table_def 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL;

-- 4. cert_doc_table_field_def 表
ALTER TABLE cert_doc_table_field_def 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL;
```

然后在 C# 实体类中删除所有 `new` 覆盖行：

```csharp
// 删除这些行：
[Column("create_id")] public new int? CreateID { get; set; }
[NotMapped] public new string Creator { get; set; }
[Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
[Column("update_id")] public new int? ModifyID { get; set; }
// ... 等等

// 基类 YZHBaseEntity 已经有这些属性且带了 [Column] 映射，子类不需要再覆盖
```

---

## 四、核心业务服务重建

### 4.1 EnterpriseFileService（企业文件上传服务）

> **职责**：企业文件的上传、查询、删除、版本管理  
> **OSS 路径**：`/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}`

#### 4.1.1 接口定义 `IEnterpriseFileService.cs`

```
路径：VOL.Builder/IServices/CertPlatform/IEnterpriseFileService.cs
```

```csharp
public interface IEnterpriseFileService : IDependency
{
    /// <summary>
    /// 上传企业文件（分片续传，支持 SignalR 进度推送）
    /// </summary>
    Task<WebResponseContent> UploadAsync(EnterpriseFileUploadRequest request);

    /// <summary>
    /// 获取企业文档目录树（基于模板实例化）
    /// </summary>
    Task<List<EnterpriseDocumentTreeNode>> GetDocumentTreeAsync(string enterpriseCode, string phaseCode);

    /// <summary>
    /// 获取文件列表（按文件夹）
    /// </summary>
    Task<(List<EnterpriseFileListDto> items, int total)> GetFileListAsync(string folderCode, int page, int rows);

    /// <summary>
    /// 删除文件（软删除 + 保留版本记录）
    /// </summary>
    Task<WebResponseContent> DeleteFileAsync(string fileCode);

    /// <summary>
    /// 获取文件版本历史
    /// </summary>
    Task<List<FileVersionDto>> GetFileVersionsAsync(string fileCode);

    /// <summary>
    /// 触发文件转换（.doc→.docx, .xls→.xlsx）
    /// </summary>
    Task<WebResponseContent> TriggerConversionAsync(string fileCode);
}
```

#### 4.1.2 实现 `EnterpriseFileService.cs`

```
路径：VOL.Builder/Services/CertPlatform/EnterpriseFileService.cs
```

核心逻辑：
1. 接收上传请求（含 `enterpriseCode`、`folderCode`、文件流）
2. 通过 `enterpriseCode` 查询企业信息获取 `enterpriseNo`、`orgCode`
3. 通过 `folderCode` 查询目录层级推导 `standardCode`、`phaseCode`、`folderPath`
4. 调用 `ICodeGeneratorService.GenerateEnterpriseDocumentPath()` 生成 OSS 路径
5. 调用 `IMinIOHelper.UploadAsync()` 上传到 MinIO
6. 写入 `ent_enterprise_file` 表（含 `storage_path`、`file_hash`、`current_version=1`）
7. 如果是 `.doc`/`.xls` 文件，自动触发转换任务（写入 `yzh_queue_task`）

#### 4.1.3 Controller `EnterpriseFileController.cs`

```
路径：VOL.WebApi/Controllers/CertPlatform/EnterpriseFileController.cs
```

端点设计：
- `POST api/enterprise-file/upload` — 上传文件
- `POST api/enterprise-file/tree` — 获取目录树
- `POST api/enterprise-file/list` — 文件列表
- `POST api/enterprise-file/delete` — 删除文件
- `POST api/enterprise-file/versions` — 版本历史
- `POST api/enterprise-file/convert` — 触发转换

### 4.2 DirectoryTemplateService（标准目录模板管理服务）

> **职责**：管理员维护标准目录模板（文件夹树 + 文件要求）  
> **OSS 路径**：`/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}`（仅模板参考文件）

#### 4.2.1 接口定义 `IDirectoryTemplateService.cs`

```csharp
public interface IDirectoryTemplateService : IDependency
{
    /// <summary>
    /// 获取标准-阶段配置下的目录树
    /// </summary>
    Task<List<DirectoryTreeNode>> GetTreeAsync(string configCode);

    /// <summary>
    /// 新增文件夹
    /// </summary>
    Task<WebResponseContent> AddFolderAsync(DirectoryTemplate entity);

    /// <summary>
    /// 修改文件夹
    /// </summary>
    Task<WebResponseContent> UpdateFolderAsync(DirectoryTemplate entity);

    /// <summary>
    /// 删除文件夹（级联删除子文件夹和文件要求）
    /// </summary>
    Task<WebResponseContent> DeleteFolderAsync(string folderCode);

    /// <summary>
    /// 获取文件夹下的文件要求列表
    /// </summary>
    Task<List<FileRequirement>> GetFileRequirementsAsync(string folderCode);

    /// <summary>
    /// 新增/修改文件要求
    /// </summary>
    Task<WebResponseContent> SaveFileRequirementAsync(FileRequirement entity);

    /// <summary>
    /// 上传模板参考文件到 standard-directory 路径
    /// </summary>
    Task<WebResponseContent> UploadTemplateFileAsync(string fileRequirementCode, IFormFile file);
}
```

### 4.3 审核员注册服务

> **职责**：审核员注册（手机号+验证码），写入 Sys_User + cert_auditor_profile

#### 4.3.1 接口 `IAuditorService.cs`

```csharp
public interface IAuditorService : IDependency
{
    /// <summary>
    /// 发送手机验证码
    /// </summary>
    Task<WebResponseContent> SendSmsCodeAsync(string phone);

    /// <summary>
    /// 审核员注册
    /// </summary>
    Task<WebResponseContent> RegisterAsync(AuditorRegisterRequest request);

    /// <summary>
    /// 获取审核员列表（分页，按机构过滤）
    /// </summary>
    Task<(List<AuditorListDto> items, int total)> GetListAsync(string orgCode, int page, int rows);

    /// <summary>
    /// 获取审核员详情
    /// </summary>
    Task<AuditorProfile> GetDetailAsync(long userId);

    /// <summary>
    /// 更新审核员资质信息
    /// </summary>
    Task<WebResponseContent> UpdateProfileAsync(AuditorProfile profile);
}
```

---

## 五、文档提取规则→提取结果保存链路

### 5.1 当前链路问题

当前 `DocExtractionRuleService` 中的 `GetFileInfoAsync` 方法使用了临时 DTO `TempFileInfo` + 原始 SQL 查询 `ent_enterprise_file` 表。这是因为 Phase 1 删除了旧的 `StandardDirectoryFile` 实体后，尚未完成向 `EnterpriseFile` 实体的切换。

### 5.2 目标链路

```
审核员上传文件 → ent_enterprise_file.storage_path
                    ↓
配置提取规则 → cert_doc_extraction_rule（含 Prompt + 字段定义）
                    ↓
验证 Prompt → DocExtractionRuleService.VerifyPromptAsync()
  ├── GetFileInfoAsync(fileCode) → 查询 ent_enterprise_file 获取 storage_path
  ├── IFileExtractor.ExtractAsync() → 提取文档结构化内容
  ├── 缓存到 cert_doc_extraction_rule.doc_content
  └── AI 执行提取 → 返回字段值/表格数据
                    ↓
保存规则 → SaveExtractionRuleAsync()
  ├── 保存 cert_doc_extraction_rule（Prompt/IsValid）
  ├── 保存 cert_doc_field_def（字段定义）
  └── 保存 cert_doc_table_def + cert_doc_table_field_def（表格定义）
                    ↓
企业上传文件后触发提取（异步队列）
  ├── yzh_queue_task 入队（type=extraction）
  ├── 执行 Skill 提取
  └── 结果写入 ent_extraction_result + ent_table_extraction_result
```

### 5.3 修复步骤

1. **`DocExtractionRuleService.AI.cs`** 中的 `GetFileInfoAsync` 方法：
   - 将 `TempFileInfo` + `SqlQueryRaw` 替换为 `EnterpriseFile` 实体查询
   - 使用 EF Core `repository.DbContext.Set<EnterpriseFile>().FirstOrDefaultAsync(x => x.Code == fileCode)`

2. **新增提取结果保存逻辑**：
   - 在 `DocExtractionRuleService` 或新建 `ExtractionResultService` 中
   - 接收 AI 提取结果 + `fileCode` + `enterpriseCode` + `phaseCode`
   - 写入 `ent_extraction_result`（字段级）和 `ent_table_extraction_result`（表格级）
   - 包含 `extracted_value`、`confidence`、`position_info` 等字段

3. **队列任务适配**：
   - 在 `yzh_queue_task` 中新增 `type=extraction` 任务类型
   - 队列执行器调用 `DocExtractionRuleService` 提取方法
   - 提取完成后写入 `ent_extraction_result`

---

## 六、前端页面适配

### 6.1 需要适配的页面

| # | 页面 | 路径 | 改动内容 |
|---|------|------|----------|
| 1 | 认证机构管理 | `views/cert/Standard/CertificationBody/` | 字段适配（补 `legal_person`/`address`/`max_users` 等） |
| 2 | ISO 标准管理 | `views/cert/Standard/ISOStandard/` | 字段适配 |
| 3 | 文档提取规则 | `views/cert/Standard/DocExtractionRule/` | API 适配（`files/tree` 端点已注释，待企业文件服务重建后恢复） |
| 4 | 审核任务 | `views/cert/Audit/AuditTask/` | 字段适配 |
| 5 | 企业管理（新建） | `views/cert/Enterprise/` | 新建企业列表 + 创建/编辑页面 |
| 6 | 审核员注册（新建） | `views/cert/Auditor/` | 新建注册页面 |

### 6.2 前端路由更新

在 `src/router/cert-platform.js` 中：
- 移除已删除页面的路由（`StandardDirectory`、`Message`、`OrgLink`）
- 新增 `Enterprise`、`Auditor` 路由

---

## 七、执行计划与任务分解

### Phase 2-A：实体类 snake_case 映射（P0）

| # | 任务 | 预计文件 | 验证标准 |
|---|------|---------|---------|
| A1 | 域 A 12 个实体添加 `[Column]` | `Cert/*.cs` × 12 | 每个属性都有 `[Column("snake_case")]` |
| A2 | 域 B 5 个实体添加 `[Column]` | `Ent/*.cs` × 5 | 同上 |
| A3 | 域 C 5 个实体添加 `[Column]` | `Audit/*.cs` × 5 | 同上 |
| A4 | 域 D 4 个实体添加 `[Column]` | `Rpt/*.cs` × 4 | 同上 |
| A5 | 删除/适配旧实体（CertStage/View × 3） | `Cert/*.cs` × 3 | 编译通过 |
| A6 | `cert_doc_*` 实体 `new` 覆盖清理 | `DocExtraction/*.cs` × 4 | 无 `new` 关键字 |
| A7 | 编译验证 | `dotnet build` | 0 错误 |

### Phase 2-B：核心业务服务重建（P1）

| # | 任务 | 预计文件 | 验证标准 |
|---|------|---------|---------|
| B1 | 新建 `EnterpriseFileService` + Interface + Controller | 3 个文件 | 上传→MinIO→数据库链路打通 |
| B2 | 新建 `DirectoryTemplateService` + Interface + Controller | 3 个文件 | 目录树 CRUD 链路打通 |
| B3 | 新建 `AuditorService` + Interface + Controller | 3 个文件 | 注册→Sys_User+Profile 链路打通 |
| B4 | 新建 `EnterpriseService`（企业 CRUD） + Interface + Controller | 3 个文件 | 企业创建→create_id 关联审核员 |
| B5 | `DocExtractionRuleService.AI.cs` TempFileInfo→EnterpriseFile 替换 | 1 个文件 | 查询文件用 EF Core |
| B6 | 新增提取结果保存逻辑 `ExtractionResultService` | 3 个文件 | AI 提取→ent_extraction_result 链路打通 |
| B7 | Program.cs 注册新服务 | 1 个文件 | 所有新服务注册到 DI |
| B8 | 编译验证 | `dotnet build` | 0 错误 |

### Phase 2-C：前端适配（P2）

| # | 任务 | 预计文件 | 验证标准 |
|---|------|---------|---------|
| C1 | 路由更新 | `router/cert-platform.js` | 无 404 路由 |
| C2 | 企业管理页面 | `views/cert/Enterprise/` | 列表 + 创建/编辑 |
| C3 | 审核员注册页面 | `views/cert/Auditor/` | 注册表单 |
| C4 | 文档提取规则页面适配 | `DocExtractionRule/` | API 对接新端点 |
| C5 | 前端编译验证 | `npm run build` | 0 错误 |

### Phase 2-D：端到端验证

| # | 任务 | 验证标准 |
|---|------|---------|
| D1 | 数据库脚本执行 | `cert_doc_*` 列名统一 |
| D2 | 后端启动 | 端口 9992 正常 |
| D3 | API 端点验证 | Swagger/Postman 调用核心 API |
| D4 | MinIO 路径验证 | 上传文件后路径格式正确 |
| D5 | Git 提交 | Phase 2 完整提交 |

---

## 八、关键代码示例

### 8.1 实体类 `[Column]` 映射模板

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;

namespace VOL.Entity.CertPlatform.Cert
{
    [Entity(TableCnName = "ISO标准管理", TableName = "cert_iso_standard", DBServer = "VOLContext")]
    [Table("cert_iso_standard")]
    public class ISOStandard : YZHBaseEntity
    {
        [Required, StringLength(50)]
        [Editable(true)]
        [Column("standard_code")]
        public string StandardCode { get; set; }

        [Required, StringLength(200)]
        [Editable(true)]
        [Column("standard_name")]
        public string StandardName { get; set; }

        [Editable(true)]
        [Column("version_year")]
        public int VersionYear { get; set; }

        [StringLength(50)]
        [Editable(true)]
        [Column("category")]
        public string Category { get; set; } = "quality";

        [Column("description")]
        public string Description { get; set; }

        // Status, OrgCode, Code, CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate,
        // DeleteID, Deleter, DeleteTime, Enable, Remark 继承自 YZHBaseEntity
    }
}
```

### 8.2 EnterpriseFileService 上传核心逻辑

```csharp
public async Task<WebResponseContent> UploadAsync(EnterpriseFileUploadRequest request)
{
    // 1. 查询企业信息
    var enterprise = await _db.Set<Enterprise>()
        .Where(x => x.Code == request.EnterpriseCode)
        .Select(x => new { x.EnterpriseNo, x.OrgCode })
        .FirstOrDefaultAsync();
    
    if (enterprise == null)
        return new WebResponseContent().Error("企业不存在");

    // 2. 查询文件夹信息（推导 standardCode/phaseCode/folderPath）
    var folder = await _db.Set<EnterpriseDocument>()
        .Where(x => x.Code == request.FolderCode)
        .FirstOrDefaultAsync();
    
    // 3. 生成 OSS 路径
    var storagePath = _codeGenerator.GenerateEnterpriseDocumentPath(
        enterprise.EnterpriseNo, enterprise.OrgCode,
        request.StandardCode, request.PhaseCode,
        folder?.FolderPath, request.FileName);

    // 4. 上传到 MinIO
    var (success, error) = await _minio.UploadAsync(storagePath, request.FileStream);
    if (!success)
        return new WebResponseContent().Error($"上传失败: {error}");

    // 5. 计算文件哈希
    var fileHash = ComputeSHA256(request.FileStream);

    // 6. 写入数据库
    var file = new EnterpriseFile
    {
        Code = Guid.NewGuid().ToString("N"),
        EnterpriseCode = request.EnterpriseCode,
        FolderCode = request.FolderCode,
        FileName = request.FileName,
        FileType = Path.GetExtension(request.FileName).TrimStart('.'),
        FileSize = request.FileStream.Length,
        StoragePath = storagePath,
        FileHash = fileHash,
        CurrentVersion = 1,
        UploadStatus = "active",
        CreateID = UserContext.Current.UserId,
        Creator = UserContext.Current.UserName,
        CreateDate = DateTime.Now
    };
    
    _db.Set<EnterpriseFile>().Add(file);
    await _db.SaveChangesAsync();

    // 7. 如果是 .doc/.xls 文件，触发转换
    if (IsNeedConversion(request.FileName))
    {
        await TriggerConversionAsync(file.Code);
    }

    return new WebResponseContent().OK("上传成功", null, file.Code);
}
```

### 8.3 提取结果保存逻辑

```csharp
public async Task SaveExtractionResultAsync(
    string fileCode, string enterpriseCode, string phaseCode,
    ExtractionData data, string ruleCode)
{
    // 1. 保存字段级提取结果
    if (data.Fields?.Any() == true)
    {
        foreach (var field in data.Fields)
        {
            var result = new ExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                PhaseCode = phaseCode,
                FileCode = fileCode,
                VersionNumber = 1, // 当前版本
                RuleCode = ruleCode,
                FieldCode = field.Key,
                LabelTag = $"{ruleCode}.{field.Key}",
                ExtractedValue = field.Value?.ToString(),
                Confidence = 0.95m, // AI 返回的置信度
                IsManualEdited = false,
                ExtractedAt = DateTime.Now
            };
            _db.Set<ExtractionResult>().Add(result);
        }
    }

    // 2. 保存表格级提取结果
    if (data.Tables?.Any() == true)
    {
        var tableIndex = 1;
        foreach (var table in data.Tables)
        {
            var result = new TableExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                PhaseCode = phaseCode,
                FileCode = fileCode,
                VersionNumber = 1,
                RuleCode = ruleCode,
                TableIndex = tableIndex++,
                ExtractedJson = JsonConvert.SerializeObject(table.Value),
                Confidence = 0.90m,
                ExtractedAt = DateTime.Now
            };
            _db.Set<TableExtractionResult>().Add(result);
        }
    }

    await _db.SaveChangesAsync();
}
```

---

## 九、风险与注意事项

### 9.1 EF Core snake_case 映射陷阱

1. **基类属性继承**：YZHBaseEntity 已添加 `[Column]` 映射，子类**不要**用 `new` 覆盖基类属性，否则 EF Core 会产生冲突
2. **JSON 字段**：MySQL 的 `json` 类型在 EF Core 中映射为 `string`，读写时需要手动 `JsonConvert.Serialize/Deserialize`
3. **`[Editable(true)]`**：Vol 框架要求可编辑列必须标记此特性，否则保存时 `ValidateDicInEntity` 会报错
4. **`[NotMapped]`**：视图模型的衍生字段（如中文翻译）必须标记 `[NotMapped]`，否则 EF Core 会尝试在数据库中查找对应列

### 9.2 `cert_doc_*` 表审计字段统一

修改 `cert_doc_*` 表的 `update_id`/`update_date` 列名为 `modify_id`/`modify_date` 后，需要同步检查：
- `DocExtractionRuleService.cs` 中所有引用 `ModifyDate`/`ModifyID` 的代码
- `DocExtractionRuleService.AI.cs` 中的引用
- 确认 EF Core 模型缓存不会导致映射冲突（必要时清除 `bin/` 目录重新编译）

### 9.3 数据库脚本执行顺序

1. 先执行 `cert_doc_*` 表 ALTER（修改列名）
2. 再执行 `dotnet build`（编译验证实体映射）
3. 最后启动后端验证（确保 EF Core 能正确读写）

### 9.4 前端 API 对接

文档提取规则的 `files/tree` 和 `files/{fileCode}/content` 端点已在 Phase 1 中注释，待 `EnterpriseFileService` 重建后恢复。恢复时需要：
1. 在 `EnterpriseFileController` 中新增对应端点
2. 在 `DocExtractionRuleController` 中恢复对 `EnterpriseFileService` 的调用
3. 前端 API 调用地址不变

---

## 十、验收标准

| # | 验收项 | 验收方法 | 通过标准 |
|---|--------|---------|---------|
| 1 | 实体类 `[Column]` 映射 | `grep -r "\[Column" VOL.Entity/CertPlatform/` | 33 个实体文件全部包含 `[Column]` |
| 2 | `cert_doc_*` 无 `new` 覆盖 | `grep -r "public new" VOL.Entity/CertPlatform/DocExtraction/` | 0 匹配 |
| 3 | 后端编译 | `dotnet build` | 0 错误 0 警告 |
| 4 | 后端启动 | `curl localhost:9992/api/health` | HTTP 200 |
| 5 | 企业文件上传 | Postman 调用 `api/enterprise-file/upload` | 文件写入 MinIO + 数据库 |
| 6 | MinIO 路径验证 | 检查 MinIO 控制台 | `/enterprise-documents/ENT-2026-0001/CB001/...` |
| 7 | 提取规则保存 | 前端保存规则 | `cert_doc_extraction_rule` + 子表有数据 |
| 8 | 提取结果保存 | 上传文件后触发提取 | `ent_extraction_result` 有数据 |
| 9 | 前端编译 | `npm run build` | 0 错误 |
| 10 | Git 提交 | `git log --oneline -1` | Phase 2 完整提交 |

---

## 十一、参考文档

| 文档 | 路径 | 用途 |
|------|------|------|
| 数据库表设计 V2 | `docs/20-架构决策/数据库表设计-V2.md` | 完整表结构定义 |
| OSS 存储结构设计 | `docs/70-当前执行/OSS存储结构重新设计-V1.md` | 路径结构定义 |
| 数据库大改造方案 | `docs/70-当前执行/数据库大改造-OSS存储-审核员业务链路-V1.md` | 改造方案总纲 |
| 大改造执行计划 TODO | `docs/70-当前执行/大改造执行计划-TODO.md` | Phase 1 执行清单（已全部完成） |
| Vol 框架完整指南 | `docs/60-AI工程设计/vol-framework-complete-guide.md` | 后端开发规范 |
| Vol 框架速查手册 | `docs/60-AI工程设计/YZH-知识库/08-Vol框架实战速查手册.md` | 常见问题速查 |
| 数据库重建 SQL | `src/server/Vue.NetCore/DB/mysql/cert_platform_rebuild_v3.sql` | V3 建表脚本 |

---

## 附录 A：26 个实体类属性→列名映射速查表

> 以下列出每个实体类中**非基类**的属性及其对应的 snake_case 列名，供 AI 执行时直接参照。

### A-02 ISOStandard → cert_iso_standard

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| StandardCode | standard_code | varchar(50) |
| StandardName | standard_name | varchar(200) |
| VersionYear | version_year | year |
| Category | category | varchar(50) |
| Description | description | text |

### A-03 ISOClause → cert_iso_clause

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| StandardCode | standard_code | varchar(36) |
| ParentCode | parent_code | varchar(36) |
| ClauseNumber | clause_number | varchar(20) |
| Title | title | varchar(200) |
| Description | description | text |
| SortOrder | sort_order | int |

### A-04 PhaseDefinition → cert_phase_definition

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| PhaseCode | phase_code | varchar(20) |
| PhaseName | phase_name | varchar(100) |
| SequenceOrder | sequence_order | int |
| Description | description | text |

### A-05 StandardPhaseConfig → cert_standard_phase_config

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| StandardCode | standard_code | varchar(36) |
| PhaseCode | phase_code | varchar(36) |
| RequiredClauses | required_clauses | json (string) |
| RequiredFiles | required_files | json (string) |

### A-06 DirectoryTemplate → cert_directory_template

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| ConfigCode | config_code | varchar(36) |
| ParentCode | parent_code | varchar(36) |
| FolderName | folder_name | varchar(200) |
| SortOrder | sort_order | int |

### A-07 FileRequirement → cert_file_requirement

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| FolderCode | folder_code | varchar(36) |
| FileNameTemplate | file_name_template | varchar(200) |
| FileType | file_type | varchar(50) |
| IsRequired | is_required | tinyint(1) |
| MaxSizeMb | max_size_mb | int |
| Description | description | text |
| SortOrder | sort_order | int |

### A-08 ExtractionRule → cert_extraction_rule

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| FileRequirementCode | file_requirement_code | varchar(36) |
| SkillCode | skill_code | varchar(36) |
| RuleType | rule_type | varchar(20) |
| RuleConfig | rule_config | json (string) |
| Description | description | text |
| IsActive | is_active | tinyint(1) |

### A-09 ExtractionField → cert_extraction_field

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| RuleCode | rule_code | varchar(36) |
| SkillCode | skill_code | varchar(36) |
| FieldCode | field_code | varchar(100) |
| LabelTag | label_tag | varchar(500) |
| FieldName | field_name | varchar(100) |
| FieldType | field_type | varchar(20) |
| EnumValues | enum_values | json (string) |
| SortOrder | sort_order | int |

### A-10 ValidationRule → cert_validation_rule

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| StandardCode | standard_code | varchar(36) |
| PhaseCode | phase_code | varchar(36) |
| ClauseCode | clause_code | varchar(36) |
| WorkflowCode | workflow_code | varchar(36) |
| RuleCode | rule_code | varchar(50) |
| RuleName | rule_name | varchar(200) |
| SeverityIfViolated | severity_if_violated | varchar(20) |
| NcDescriptionTemplate | nc_description_template | text |
| IsActive | is_active | tinyint(1) |

### A-11 ValidationRuleSource → cert_validation_rule_source

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| RuleCode | rule_code | varchar(36) |
| FileRequirementCode | file_requirement_code | varchar(36) |
| SourcePath | source_path | varchar(500) |

### A-12 ReportTemplate → cert_report_template

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| CbCode | cb_code | varchar(36) |
| StandardCode | standard_code | varchar(36) |
| PhaseCode | phase_code | varchar(36) |
| TemplateName | template_name | varchar(200) |
| TemplateFilePath | template_file_path | varchar(500) |
| SectionConfig | section_config | json (string) |
| IsDefault | is_default | tinyint(1) |

### A-13 ClauseExtractionRule → cert_clause_extraction_rule

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| ClauseCode | clause_code | varchar(36) |
| WorkflowCode | workflow_code | varchar(36) |
| Description | description | text |

### B-05 FileVersion → ent_file_version

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| FileCode | file_code | varchar(36) |
| VersionNumber | version_number | int |
| FileSize | file_size | bigint |
| StoragePath | storage_path | varchar(500) |
| FileHash | file_hash | varchar(64) |
| ChangeNotes | change_notes | text |
| UploadBy | upload_by | int |

### B-06 FilePreCheckResult → ent_file_pre_check_result

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| FileCode | file_code | varchar(36) |
| VersionNumber | version_number | int |
| CheckType | check_type | varchar(20) |
| CheckResult | check_result | varchar(20) |
| Message | message | text |
| Detail | detail | json (string) |
| CheckedAt | checked_at | datetime |

### B-07 FileComplianceCheck → ent_file_compliance_check

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| FileCode | file_code | varchar(36) |
| VersionNumber | version_number | int |
| RuleCode | rule_code | varchar(36) |
| WorkflowExecutionCode | workflow_execution_code | varchar(36) |
| CheckStatus | check_status | varchar(20) |
| Message | message | text |
| Detail | detail | json (string) |
| CheckedAt | checked_at | datetime |

### B-08 ExtractionResult → ent_extraction_result

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| EnterpriseCode | enterprise_code | varchar(36) |
| PhaseCode | phase_code | varchar(36) |
| FileCode | file_code | varchar(36) |
| VersionNumber | version_number | int |
| RuleCode | rule_code | varchar(36) |
| FieldCode | field_code | varchar(36) |
| LabelTag | label_tag | varchar(500) |
| ExtractedValue | extracted_value | text |
| Confidence | confidence | decimal(3,2) |
| PositionInfo | position_info | json (string) |
| IsManualEdited | is_manual_edited | tinyint(1) |
| ExtractedAt | extracted_at | datetime |

### B-09 TableExtractionResult → ent_table_extraction_result

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| EnterpriseCode | enterprise_code | varchar(36) |
| PhaseCode | phase_code | varchar(36) |
| FileCode | file_code | varchar(36) |
| VersionNumber | version_number | int |
| RuleCode | rule_code | varchar(36) |
| TableIndex | table_index | int |
| ExtractedJson | extracted_json | json (string) |
| Confidence | confidence | decimal(3,2) |
| PositionInfo | position_info | json (string) |
| ExtractedAt | extracted_at | datetime |

### C-02 ChecklistItem → audit_checklist_item

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| TaskCode | task_code | varchar(36) |
| ClauseCode | clause_code | varchar(36) |
| AuditCriteria | audit_criteria | text |
| FindingDescription | finding_description | text |
| Conformity | conformity | varchar(20) |
| NcsFound | ncs_found | int |
| CheckedBy | checked_by | int |
| CheckedAt | checked_at | datetime |
| SortOrder | sort_order | int |

### C-03 NonConformity → audit_nonconformity

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| TaskCode | task_code | varchar(36) |
| ClauseCode | clause_code | varchar(36) |
| NcNumber | nc_number | varchar(50) |
| Severity | severity | varchar(20) |
| Description | description | text |
| RequirementRef | requirement_ref | text |
| EvidenceRef | evidence_ref | text |
| SourceType | source_type | varchar(20) |
| SourceCheckCode | source_check_code | varchar(36) |
| RuleCode | rule_code | varchar(36) |
| DueDate | due_date | date |
| OpenedBy | opened_by | int |
| OpenedAt | opened_at | datetime |
| ClosedAt | closed_at | datetime |

### C-04 AuditFinding → audit_finding

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| ChecklistItemCode | checklist_item_code | varchar(36) |
| NcCode | nc_code | varchar(36) |
| SourceFileCode | source_file_code | varchar(36) |
| SourcePosition | source_position | varchar(200) |
| SourceContent | source_content | text |
| FindingType | finding_type | varchar(20) |
| Description | description | text |
| Confidence | confidence | decimal(3,2) |
| IsManual | is_manual | tinyint(1) |
| CreatedBy | created_by | int |

### C-05 AuditEvidence → audit_evidence

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| TaskCode | task_code | varchar(36) |
| ClauseCode | clause_code | varchar(36) |
| EvidenceType | evidence_type | varchar(20) |
| StoragePath | storage_path | varchar(500) |
| FileHash | file_hash | varchar(64) |
| IsVoided | is_voided | tinyint(1) |
| VoidedAt | voided_at | datetime |
| VoidedBy | voided_by | int |
| CapturedAt | captured_at | datetime |
| CapturedBy | captured_by | int |

### C-06 Rectification → audit_rectification

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| NcCode | nc_code | varchar(36) |
| Correction | correction | text |
| CorrectiveAction | corrective_action | text |
| EvidenceFiles | evidence_files | json (string) |
| SubmittedBy | submitted_by | int |
| SubmittedAt | submitted_at | datetime |
| VerifiedBy | verified_by | int |
| VerifiedAt | verified_at | datetime |
| VerifyResult | verify_result | varchar(20) |
| VerifyNotes | verify_notes | text |

### D-01 ReportTask → rpt_report_task

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| PhaseCode | phase_code | varchar(36) |
| BasedOnAuditTaskCode | based_on_audit_task_code | varchar(36) |
| TemplateCode | template_code | varchar(36) |
| TaskNumber | task_number | varchar(50) |
| StartedAt | started_at | datetime |
| CompletedAt | completed_at | datetime |

### D-02 AuditReport → rpt_audit_report

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| ReportTaskCode | report_task_code | varchar(36) |
| ReportNumber | report_number | varchar(50) |
| FilePath | file_path | varchar(500) |
| CreatedBy | created_by | int |

### D-03 ReportSection → rpt_report_section

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| ReportCode | report_code | varchar(36) |
| ClauseCode | clause_code | varchar(36) |
| WorkflowCode | workflow_code | varchar(36) |
| SectionName | section_name | varchar(200) |
| Content | content | text |
| SortOrder | sort_order | int |

### D-04 ReportSectionSource → rpt_report_section_source

| C# 属性 | 数据库列名 | 类型 |
|---------|-----------|------|
| SectionCode | section_code | varchar(36) |
| SourceType | source_type | varchar(20) |
| SourceCode | source_code | varchar(36) |
| SourceSummary | source_summary | text |
| SortOrder | sort_order | int |

---

## 附录 B：新服务文件清单

| 服务 | Interface | Service | Controller |
|------|-----------|---------|------------|
| 企业文件 | `IEnterpriseFileService.cs` | `EnterpriseFileService.cs` | `EnterpriseFileController.cs` |
| 目录模板 | `IDirectoryTemplateService.cs` | `DirectoryTemplateService.cs` | `DirectoryTemplateController.cs` |
| 审核员 | `IAuditorService.cs` | `AuditorService.cs` | `AuditorController.cs` |
| 企业 | `IEnterpriseService.cs` | `EnterpriseService.cs` | `EnterpriseController.cs` |
| 提取结果 | `IExtractionResultService.cs` | `ExtractionResultService.cs` | — (内部调用) |

### 文件路径

```
VOL.Builder/IServices/CertPlatform/
├── IEnterpriseFileService.cs      ← 新建
├── IDirectoryTemplateService.cs   ← 新建
├── IAuditorService.cs             ← 新建
├── IEnterpriseService.cs          ← 新建
└── IExtractionResultService.cs   ← 新建

VOL.Builder/Services/CertPlatform/
├── EnterpriseFileService.cs       ← 新建
├── DirectoryTemplateService.cs    ← 新建
├── AuditorService.cs              ← 新建
├── EnterpriseService.cs          ← 新建
└── ExtractionResultService.cs    ← 新建

VOL.WebApi/Controllers/CertPlatform/
├── EnterpriseFileController.cs   ← 新建
├── DirectoryTemplateController.cs← 新建
├── AuditorController.cs          ← 新建
└── EnterpriseController.cs       ← 新建
```

---

## 附录 C：Phase 2 任务执行总表

| Phase | 任务 | 文件数 | 优先级 | 依赖 |
|:---:|------|:---:|:---:|:---:|
| 2-A1 | 域 A 12 个实体 `[Column]` | 12 | P0 | — |
| 2-A2 | 域 B 5 个实体 `[Column]` | 5 | P0 | — |
| 2-A3 | 域 C 5 个实体 `[Column]` | 5 | P0 | — |
| 2-A4 | 域 D 4 个实体 `[Column]` | 4 | P0 | — |
| 2-A5 | 删除旧实体（CertStage/View） | 3 | P0 | — |
| 2-A6 | `cert_doc_*` 实体 `new` 清理 + SQL ALTER | 4+1 | P0 | — |
| 2-A7 | 编译验证 | — | P0 | A1-A6 |
| 2-B1 | EnterpriseFileService | 3 | P1 | A7 |
| 2-B2 | DirectoryTemplateService | 3 | P1 | A7 |
| 2-B3 | AuditorService | 3 | P1 | A7 |
| 2-B4 | EnterpriseService | 3 | P1 | A7 |
| 2-B5 | DocExtractionRuleService.AI.cs 替换 | 1 | P1 | B1 |
| 2-B6 | ExtractionResultService | 3 | P1 | B1 |
| 2-B7 | Program.cs 注册 | 1 | P1 | B1-B6 |
| 2-B8 | 编译验证 | — | P1 | B1-B7 |
| 2-C1 | 路由更新 | 1 | P2 | B8 |
| 2-C2 | 企业管理页面 | 3 | P2 | C1 |
| 2-C3 | 审核员注册页面 | 3 | P2 | C1 |
| 2-C4 | 文档提取规则页面适配 | 2 | P2 | C1 |
| 2-C5 | 前端编译验证 | — | P2 | C2-C4 |
| 2-D | 端到端验证 + Git 提交 | — | P0 | 全部 |

**合计**：约 60 项任务，52 个文件修改/新建