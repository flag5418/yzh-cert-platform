# Phase 3：标准文件 code 枢纽改造设计文档

> **版本**：V1.1 | **日期**：2026-08-15 | **状态**：AI 分析链路已实施（fileCode 双查询）；企业提取链路（4.3）待实施
> **前置文档**：`OSS存储结构重新设计-V1.md`、`数据库大改造-OSS存储-审核员业务链路-V1.md`、`标准目录-编码体系与上传层级设计.md`
> **核心原则**：文档先行，再实施
>
> **实施状态修订（2026-08-15）**：
> - ✅ **规则/AI 分析链路已按实际文件打通**：前端目录树选实际标准目录文件（FileCode=FL-xxx），
>   `DocExtractionRuleService.GetFileInfoAsync` 两级查询：① `cert_file_requirement` 模板（Code=FR-xxx）→ ② 兜底 `cert_standard_directory_file` 实际文件（FileCode）；
> - ✅ 请求 DTO 统一使用 `fileCode` 字段（`AIAnalyzeRequest / GeneratePromptRequest / VerifyPromptRequest / SaveExtractionRuleRequest`）；
> - ⚠️ 企业文件上传→自动提取（4.3）与数据消费（4.4）仍待实施；模板上传前端入口（4.1）后端 API 齐全、前端待接通。

---

## 一、改造背景与目标

### 1.1 问题

当前提取结果表（`ent_extraction_result`、`ent_table_extraction_result`）和提取规则表（`cert_doc_extraction_rule`）通过 `file_code` 关联企业上传文件，存在以下问题：

1. **提取规则无法复用**：规则绑定到企业文件，不同企业上传同类文件需要重复配置规则
2. **缺乏标准维度**：无法通过标准文件快速获取字段定义、表格定义、提取规则
3. **工作流引用困难**：自定义工作流需要通过标准文件 code 关联字段进行逻辑解释，当前设计不支持

### 1.2 目标

引入 **`standard_file_code`** 作为核心枢纽字段，建立完整的冗余字段体系：

```
standard_file_code（关联 cert_file_requirement.code）
    ├── 获取标准的字段定义（cert_doc_field_def）
    ├── 获取标准的表格定义（cert_doc_table_def + cert_doc_table_field_def）
    ├── 获取提取规则（cert_doc_extraction_rule）
    └── 工作流引用字段进行逻辑解释
```

### 1.3 核心设计理念

> **企业保存的字段和表格数据是真正的数据核心**，所有的 NC 和报告都依赖这些真正存储的数据。
>
> `standard_file_code` 是连接"标准定义"和"企业实际数据"的桥梁：
> - 标准文件定义了"应该提取什么"（字段/表格/规则）
> - 企业文件产生了"实际提取了什么"（提取结果）
> - `standard_file_code` 让两者可以通过同一个 code 关联起来

---

## 二、冗余字段体系设计

### 2.1 设计原则

> **所有表关联通过 `code`（GUID），不通过 `id`（自增主键）** — 项目统一规范

冗余字段的目的：
1. **方便过滤**：不需要 JOIN 多表即可按机构/标准/阶段过滤数据
2. **工作流引用**：自定义工作流可以通过冗余字段快速定位数据
3. **数据完整性**：即使关联表数据变更，冗余字段仍保持原始关联信息

### 2.2 完整冗余字段体系

每张涉及提取的表都包含以下冗余字段（含义一致）：

| 字段 | 类型 | 说明 | 关联 |
|------|------|------|------|
| `standard_file_code` | varchar(36) | **核心枢纽** — 标准文件编码 | → `cert_file_requirement.code` |
| `org_code` | varchar(50) | 机构编码（冗余） | → `cert_certification_body.code`（继承自 YZHBaseEntity） |
| `standard_code` | varchar(36) | 标准编码（冗余） | → `cert_iso_standard.code` |
| `phase_code` | varchar(36) | 阶段编码（冗余） | → `cert_phase_definition.code` |

### 2.3 冗余字段数据流向

```
cert_standard_phase_config（源头）
    ├── org_code        ← 机构编码
    ├── standard_code   ← 标准编码
    └── phase_code      ← 阶段编码
        ↓
cert_directory_template（文件夹树）
    └── config_code → cert_standard_phase_config.code
        ↓
cert_file_requirement（标准文件 / 文件要求）⭐ 核心
    ├── code                  ← 这就是 standard_file_code
    ├── org_code              ← 从 config 冗余
    ├── standard_code         ← 从 config 冗余
    ├── template_storage_path ← 模板文件 OSS 路径
    └── template_file_name    ← 模板文件原始名
        ↓
cert_doc_extraction_rule（提取规则）
    ├── standard_file_code → cert_file_requirement.code  ⭐ 核心关联
    ├── org_code           ← 冗余
    ├── standard_code      ← 冗余
    └── phase_code         ← 冗余
        ↓
ent_enterprise_file（企业上传文件）
    └── standard_file_code → cert_file_requirement.code  ⭐ 标记企业文件对应的标准文件
        ↓
ent_extraction_result（字段级提取结果）⭐ 企业真正的数据核心
    ├── standard_file_code → cert_file_requirement.code  ⭐ 核心关联
    ├── enterprise_code    → ent_enterprise.code
    ├── file_code          → ent_enterprise_file.code
    ├── rule_code          → cert_doc_extraction_rule.code
    ├── org_code           ← 冗余
    ├── standard_code      ← 冗余
    └── phase_code         ← 冗余
        ↓
ent_table_extraction_result（表格级提取结果）⭐ 企业真正的数据核心
    ├── standard_file_code → cert_file_requirement.code  ⭐ 核心关联
    ├── enterprise_code    → ent_enterprise.code
    ├── file_code          → ent_enterprise_file.code
    ├── rule_code          → cert_doc_extraction_rule.code
    ├── org_code           ← 冗余
    ├── standard_code      ← 冗余
    └── phase_code         ← 冗余
```

---

## 三、表结构变更详情

### 3.1 ent_extraction_result（字段级提取结果）

> ⚠️ **不删除 `phase_code`**，而是在保留原有字段的基础上新增 `standard_file_code` 和 `standard_code`
>
> `org_code` 已从 YZHBaseEntity 继承，`phase_code` 已存在

| 变更类型 | 字段 | 类型 | 说明 |
|---------|------|------|------|
| ✅ 新增 | `standard_file_code` | varchar(36) | 标准文件编码（关联 cert_file_requirement.code） |
| ✅ 新增 | `standard_code` | varchar(36) | 标准编码（冗余） |
| 保留 | `phase_code` | varchar(36) | 阶段编码（冗余，保留不删除） |
| 保留 | `org_code` | varchar(50) | 机构编码（继承自 YZHBaseEntity） |
| 保留 | `enterprise_code` | varchar(36) | 企业编码 |
| 保留 | `file_code` | varchar(36) | 企业文件编码 |
| 保留 | `rule_code` | varchar(36) | 规则编码 |

**索引**：`idx_standard_file_code (standard_file_code)`

### 3.2 ent_table_extraction_result（表格级提取结果）

同 3.1，字段变更完全一致。

### 3.3 cert_doc_extraction_rule（文档提取规则）

> 规则从绑定企业文件（`file_code`）改为绑定标准文件（`standard_file_code`）

| 变更类型 | 字段 | 类型 | 说明 |
|---------|------|------|------|
| ✅ 新增 | `standard_file_code` | varchar(36), UNIQUE | 标准文件编码（一个标准文件对应一个规则） |
| ✅ 新增 | `standard_code` | varchar(36) | 标准编码（冗余） |
| ✅ 新增 | `phase_code` | varchar(36) | 阶段编码（冗余） |
| 保留 | `file_code` | varchar(100) | 旧字段保留（向后兼容，新代码不再使用） |
| 保留 | `org_code` | varchar(50) | 机构编码（继承自 YZHBaseEntity） |

**约束**：`uk_standard_file_code (standard_file_code)` — 一个标准文件对应一个提取规则

### 3.4 cert_file_requirement（文件要求 / 标准文件模板）

> 此表既存储文件要求，也存储标准目录的模板文件信息

| 变更类型 | 字段 | 类型 | 说明 |
|---------|------|------|------|
| ✅ 新增 | `template_storage_path` | varchar(500) | 模板文件 OSS 存储路径 |
| ✅ 新增 | `template_file_name` | varchar(500) | 模板文件原始名 |
| ✅ 新增 | `standard_code` | varchar(36) | 标准编码（关联 cert_iso_standard.code） |
| 保留 | `org_code` | varchar(50) | 机构编码（继承自 YZHBaseEntity） |

**模板文件 OSS 路径**：`/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}`

### 3.5 ent_enterprise_file（企业文件）

| 变更类型 | 字段 | 类型 | 说明 |
|---------|------|------|------|
| ✅ 新增 | `standard_file_code` | varchar(36) | 标准文件编码（标记企业文件对应的标准文件模板） |

**索引**：`idx_standard_file_code (standard_file_code)`

---

## 四、业务链路设计

### 4.1 标准目录模板文件上传流程

```
1. 管理员在标准目录管理页面上传模板文件
2. 前端调用 POST /api/DirectoryTemplate/uploadTemplateFile
   参数：requirementCode + file
3. DirectoryTemplateService.UploadTemplateFileAsync：
   a. 通过 requirementCode 查询 cert_file_requirement 记录
   b. 通过 folder_code → cert_directory_template → config_code → cert_standard_phase_config
      获取 org_code / standard_code / phase_code
   c. 构建完整文件夹路径（从树结构向上遍历）
   d. 生成 OSS 路径：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
   e. 上传到 MinIO
   f. 更新 cert_file_requirement.template_storage_path 和 template_file_name
```

### 4.2 提取规则配置流程（已实施，2026-08-15）

```
1. 管理员在文档提取规则页面选择实际标准目录文件（CertDirectoryTree 树节点）
2. 前端调用 POST /api/DocExtractionRule/analyze
   参数：fileCode + skill
3. DocExtractionRuleService.GetFileInfoAsync 两级查询：
   a. 优先：cert_file_requirement 模板文件（Code=FR-xxx，有 template_storage_path 时命中）
   b. 兜底：cert_standard_directory_file 实际文件（FileCode=FL-xxx，带 .doc/.xls 转换状态）
   c. 从 MinIO 下载文件（.doc 用 .converted/xxx.docx 转换产物）
   d. 调用 IFileExtractor 提取文档内容
   e. 调用 AI 分析推荐字段和表格
4. 管理员确认字段/表格后调用 POST /api/DocExtractionRule/save
   参数：fileCode + orgCode + standardCode + phaseCode + skill + fields + tables + prompt
5. 规则保存到 cert_doc_extraction_rule（standard_file_code 存实际 FileCode，按它查找或创建）
   字段定义保存到 cert_doc_field_def（通过 rule_code 关联）
   表格定义保存到 cert_doc_table_def + cert_doc_table_field_def（通过 rule_code 关联）
```

### 4.3 企业文件上传→自动触发提取流程

```
1. 审核员上传企业文件
2. 前端调用 POST /api/EnterpriseFile/upload
   参数：enterpriseCode + folderCode + standardCode + phaseCode + folderPath + standardFileCode + file
3. EnterpriseFileService.UploadAsync：
   a. 查询企业信息（EnterpriseNo, OrgCode）
   b. 生成 OSS 路径：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
   c. 上传到 MinIO
   d. 写入 ent_enterprise_file（含 standard_file_code）
   e. ★ 自动触发提取：
      - 通过 standard_file_code 查询 cert_doc_extraction_rule
      - 如果规则存在且 is_valid=true：
        - 从 MinIO 下载企业文件
        - 调用 IFileExtractor 提取文档内容
        - 调用 AI 执行字段/表格提取
        - 调用 ExtractionResultService.SaveExtractionResultAsync 保存结果
          写入 ent_extraction_result + ent_table_extraction_result
          （含 standard_file_code / org_code / standard_code / phase_code 冗余字段）
      - 如果规则不存在或未验证：跳过自动提取（文件已上传成功）
```

### 4.4 数据消费流程（NC/报告）

```
1. 自动校验 / NC 生成：
   - 通过 standard_file_code 关联到提取规则和字段定义
   - 通过 enterprise_code + file_code 定位企业实际数据
   - 对比提取结果与验证规则 → 自动生成 NC

2. 报告生成：
   - 通过 standard_file_code 获取字段定义（中文/英文名/数据类型）
   - 通过 enterprise_code + file_code 获取提取结果
   - 将提取结果填充到报告模板的对应位置

3. 自定义工作流引用：
   - 工作流节点通过 standard_file_code + field_code 引用具体字段
   - 冗余字段（org_code/standard_code/phase_code）方便工作流过滤和路由
```

---

## 五、代码变更影响清单

### 5.1 数据库变更（已完成 ✅）

| 表 | 变更 | SQL 脚本 |
|---|---|---|
| ent_extraction_result | +standard_file_code, +standard_code | `phase3_standard_file_code_refactor.sql` |
| ent_table_extraction_result | +standard_file_code, +standard_code | 同上 |
| cert_doc_extraction_rule | +standard_file_code(UNIQUE), +standard_code, +phase_code | 同上 |
| cert_file_requirement | +template_storage_path, +template_file_name, +standard_code | 同上 |
| ent_enterprise_file | +standard_file_code | 同上 |

### 5.2 实体类变更（已完成 ✅）

| 实体类 | 变更 |
|---|---|
| ExtractionResult.cs | +StandardFileCode, +StandardCode |
| TableExtractionResult.cs | +StandardFileCode, +StandardCode |
| CertDocExtractionRule.cs | +StandardFileCode, +StandardCode, +PhaseCode |
| FileRequirement.cs | +TemplateStoragePath, +TemplateFileName, +StandardCode |
| EnterpriseFile.cs | +StandardFileCode |

### 5.3 服务层变更

| 服务 | 变更 | 状态 |
|---|---|---|
| DirectoryTemplateService | +模板文件上传/下载/删除/改名 | ✅ 完成 |
| DocExtractionRuleService | GetFileInfoAsync 双查询（模板 Code + 实际 FileCode）+ DTO 统一 fileCode | ✅ 完成（2026-08-15） |
| ExtractionResultService | 适配新参数 | 待实施 |
| EnterpriseFileService | +standardFileCode +自动触发提取 | 待实施 |

### 5.4 接口层变更

| Controller | 变更 | 状态 |
|---|---|---|
| DirectoryTemplateController | +模板文件 API | ✅ 完成（前端上传入口待接通） |
| DocExtractionRuleController | 请求体 fileCode（DTO 字段统一），不再依赖 standardFileCode 键名 | ✅ 完成（2026-08-15） |
| EnterpriseFileController | +StandardFileCode 参数 | 待实施 |

---

## 六、从 git 5e33d4e 恢复的已验证方法

> 这些方法因 OSS 存储规则调整而暂时废弃，但代码已验证通过，可直接参考使用

| 方法 | 来源 | 用途 | 适配方式 |
|---|---|---|---|
| FileStorageService.UploadAsync | 5e33d4e | 文件上传到 MinIO | 改用新 V3 路径生成 |
| FileStorageService.DownloadAsync | 5e33d4e | 从 MinIO 下载文件 | 直接复用 |
| FileStorageService.DeleteAsync | 5e33d4e | 删除 MinIO 文件 | 直接复用 |
| FileStorageService.RenameAsync | 5e33d4e | 重命名 MinIO 文件 | 直接复用 |
| FolderFileManager.RenameFolder | 5e33d4e | 文件夹改名（递归重命名子文件） | 参考逻辑 |
| FolderFileManager.DeleteFolder | 5e33d4e | 文件夹删除（递归删除子文件） | 参考逻辑 |

---

## 七、关联文档更新清单

| 文档 | 更新内容 | 状态 |
|---|---|---|
| `数据库表设计-V2.md` | B-08/B-09/A-07 表结构更新 | 本文档替代 |
| `OSS存储结构重新设计-V1.md` | 标准目录模板文件上传路径 | 待更新 |
| `数据库大改造-OSS存储-审核员业务链路-V1.md` | 提取链路更新 | 待更新 |
| `phase3_standard_file_code_refactor.sql` | SQL 脚本注释更新 | 待更新 |
