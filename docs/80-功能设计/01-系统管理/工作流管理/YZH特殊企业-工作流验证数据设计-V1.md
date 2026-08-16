# YZH 特殊企业 - 工作流验证数据设计

> **状态核对（2026-08-15，对当前代码/DB）**：
> - ✅ B-08/B-09 列已就位：`enterprise_code`、`phase_code`、`standard_file_code`、`standard_code`、`version_number`、`rule_code`、`field_code`、`label_tag`、`extracted_value`、`confidence`、`position_info`、`is_manual_edited`、`extracted_at`（表结构已含，无需迁移）
> - ✅ `ExtractionResult` / `TableExtractionResult` 实体字段齐全
> - ❌ **YZH-STD-ENT 企业记录缺失**（2026-08-15 全量清理时删除，实施时需先执行 §2.3 SQL 重建）
> - ❌ `YZH_STANDARD_ENTERPRISE_CODE` 常量未定义
> - ❌ `SaveExtractionRuleAsync` 第 5 步（同步 B-08/B-09）未实现——当前保存只写规则 + 字段/表格**定义**，提取值不入库
> - ❌ `SaveExtractionRuleRequest.ExtractionData` 未加，前端 saveRule 未提交提取数据
> - ✅ 验证接口已返回原始提取数据（`VerifyPromptResponse.Data = ExtractionData { Fields, Tables }`），前端需保留原始值供保存提交

> **版本**：V1.0 | **日期**：2026-08-14 | **状态**：待评审
>
> **前置文档**：
> - `LogicFlow工作流设计器实施分析与建议-V1.md`（Phase E/F/G 三阶段计划）
> - `YZH-AI引擎详细设计-V1.md`（四件套 + B-08/B-09 落库）
> - `数据库表设计-V2.md`（域 A/B/C/D/F/G 六域表结构）
> - `标准目录-编码体系与上传层级设计.md`（FileCode/StoragePath 规则）
>
> **核心目标**：在保存提取规则时，同步将 AI 提取的数据落入一个特殊的 "YZH 标准企业" 的资料存储表中，一举实现三个目的：
> 1. **提前验证**企业上传文件后内容保存的完整链路（规则 → 提取 → 落库 → 查询）
> 2. **为自研工作流提供真实测试数据**（get_field/get_table 节点能查到数据）
> 3. **积累标准目录的标杆提取值**（可作为后续企业提取的参照基线）

---

## 一、核心设计思路

### 1.1 问题分析

```
当前状态：
  规则配置页 (DocExtractionRule)
    ├── AI 分析 → 推荐字段/表格 → 前端展示
    ├── Prompt 生成 → 验证 → 返回提取结果 → 前端展示
    └── 保存规则 → 只保存规则定义 (cert_doc_*) → ❌ 提取结果未落库

工作流设计器需要：
  get_field[label_tag] → 查 B-08 ent_extraction_result → 必须有数据
  get_table[table_code] → 查 B-09 ent_table_extraction_result → 必须有数据

缺口：
  规则保存时的验证提取数据，只在前端内存中展示了，没有持久化到 B-08/B-09
  → 工作流设计器开发完成后，get_field 节点永远返回"未找到"
```

### 1.2 方案核心：YZH 标准企业

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         规则保存流程（改造后）                                │
│                                                                             │
│  管理员在 DocExtractionRule 页面操作                                        │
│  ├── 1. AI 分析 → 推荐字段/表格                                             │
│  ├── 2. 生成 Prompt → 验证 Prompt → AI 返回提取结果                         │
│  └── 3. 保存规则（SaveExtractionRuleAsync）                                │
│       ├── 3a. 保存规则定义 → cert_doc_extraction_rule + field_def + table_def│
│       └── 3b. 【新增】将验证提取结果落入 B-08/B-09                           │
│                ├── 企业 = YZH 标准企业（enterprise_code = 'YZH-STD-ENT')    │
│                ├── 文件 = 标准目录文件（file_code 已知）                     │
│                ├── 字段 → ent_extraction_result（每字段一条）                │
│                └── 表格 → ent_table_extraction_result（每表格一条）         │
│                                                                             │
│  工作流设计器运行时：                                                        │
│  get_field[label_tag] → 查 B-08 WHERE enterprise_code='YZH-STD-ENT'          │
│    → ✅ 返回标准目录文件的提取值                                             │
│  get_table[table_code] → 查 B-09 WHERE enterprise_code='YZH-STD-ENT'        │
│    → ✅ 返回标准目录文件的表格数据                                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.3 为什么是 "YZH 标准企业" 而非直接落库

| 方案 | 优势 | 劣势 |
|------|------|------|
| **A. 直接落 B-08/B-09 无企业** | 简单 | GetFieldSkill 运行时按 enterprise_code 过滤，无企业编码的数据不会被查到；且无法区分"标准数据"和"企业数据" |
| **B. 落入 YZH 标准企业** ✅ | 与企业数据完全同构，GetFieldSkill 查询逻辑一致；可作为参照基线；工作流设计器可指定 enterprise_code='YZH-STD-ENT' 做测试运行 | 需创建一条虚拟企业记录 |
| **C. 单独建表** | 隔离性好 | GetFieldSkill 需增加分支查询逻辑，破坏统一性；工作流引擎需感知"标准数据源"和"企业数据源"区别 |

**决策：采用方案 B**——在 `ent_enterprise` 表中创建一条特殊的 "YZH 标准企业" 记录，所有标准目录文件的提取结果都关联到这个企业。这保证了：

1. **数据结构完全一致**：B-08/B-09 不需要增加特殊字段区分数据来源
2. **查询逻辑统一**：GetFieldSkill/GetTableSkill 不需要修改查询逻辑
3. **工作流测试**：设计器测试运行时注入 `enterprise_code='YZH-STD-ENT'` 即可
4. **参照基线**：标准目录的提取值可作为标杆，后续企业提取后可对比差异

---

## 二、数据库变更

### 2.1 创建 YZH 标准企业记录

```sql
-- YZH 标准企业：虚拟企业，用于存储标准目录文件的提取结果
-- enterprise_code 使用 Code 字段值，便于工作流引擎统一按 enterprise_code 过滤
INSERT INTO ent_enterprise (
  Code, Name, ShortName, CreditCode, LegalPerson,
  Address, CertScope, ContactName, ContactPhone, ContactEmail,
  Status, ArchiveDate,
  OrgCode, CreateID, Creator, CreateDate, Enable
) VALUES (
  'YZH-STD-ENT',                          -- Code: 全局唯一编码，工作流引擎用此值过滤
  'YZH标准企业（标准目录数据）',            -- Name: 明确标识
  'YZH标准',                               -- ShortName
  'YZH-STD-000000',                        -- CreditCode: 特殊格式，不与真实企业冲突
  '系统',                                  -- LegalPerson
  '系统内置',                              -- Address
  '全部标准',                              -- CertScope
  '系统',                                  -- ContactName
  '',                                     -- ContactPhone
  '',                                     -- ContactEmail
  'active',                               -- Status
  NULL,                                   -- ArchiveDate
  'YZH',                                  -- OrgCode: YZH 框架保留
  1,                                      -- CreateID: 超级管理员
  '系统初始化',                            -- Creator
  NOW(),                                  -- CreateDate
  1                                       -- Enable
)
ON DUPLICATE KEY UPDATE Name = VALUES(Name);
```

### 2.2 B-08/B-09 新增企业/阶段上下文字段

按照 `LogicFlow工作流设计器实施分析与建议-V1.md` §六 的设计，B-08/B-09 需新增 `enterprise_code` 和 `phase_code` 字段：

```sql
-- B-08 ent_extraction_result 新增企业/阶段上下文字段
ALTER TABLE ent_extraction_result
  ADD COLUMN IF NOT EXISTS enterprise_code VARCHAR(50) NULL
    COMMENT '所属企业编码（YZH-STD-ENT=标准目录数据；真实企业=运行时注入）',
  ADD COLUMN IF NOT EXISTS phase_code VARCHAR(20) NULL
    COMMENT '所属阶段编码（运行时注入，标准目录数据可填 S1/Surv1 等代表阶段）';

-- B-09 ent_table_extraction_result 同步
ALTER TABLE ent_table_extraction_result
  ADD COLUMN IF NOT EXISTS enterprise_code VARCHAR(50) NULL
    COMMENT '所属企业编码',
  ADD COLUMN IF NOT EXISTS phase_code VARCHAR(20) NULL
    COMMENT '所属阶段编码';

-- 高频查询索引
CREATE INDEX IF NOT EXISTS idx_ent_ext_result_ent_phase_label
  ON ent_extraction_result(enterprise_code, phase_code, label_tag);

CREATE INDEX IF NOT EXISTS idx_ent_table_ext_result_ent_phase
  ON ent_table_extraction_result(enterprise_code, phase_code, RuleCode);
```

### 2.3 SQL 脚本文件

**文件位置**：`src/server/Vue.NetCore/DB/mysql/cert_yzh_standard_enterprise.sql`

```sql
-- ============================================================
-- YZH 特殊企业 + B-08/B-09 上下文字段
-- 用途：为自研工作流提供验证数据基础
-- 日期：2026-08-14
-- ============================================================

-- 1. 创建 YZH 标准企业（幂等）
INSERT INTO ent_enterprise (
  Code, Name, ShortName, CreditCode, LegalPerson,
  Address, CertScope, ContactName, ContactPhone, ContactEmail,
  Status, ArchiveDate,
  OrgCode, CreateID, Creator, CreateDate, Enable
) VALUES (
  'YZH-STD-ENT',
  'YZH标准企业（标准目录数据）',
  'YZH标准',
  'YZH-STD-000000',
  '系统',
  '系统内置',
  '全部标准',
  '系统',
  '',
  '',
  'active',
  NULL,
  'YZH',
  1,
  '系统初始化',
  NOW(),
  1
)
ON DUPLICATE KEY UPDATE Name = VALUES(Name), ShortName = VALUES(ShortName);

-- 2. B-08 新增企业/阶段上下文字段（幂等）
ALTER TABLE ent_extraction_result
  ADD COLUMN IF NOT EXISTS enterprise_code VARCHAR(50) NULL
    COMMENT '所属企业编码',
  ADD COLUMN IF NOT EXISTS phase_code VARCHAR(20) NULL
    COMMENT '所属阶段编码';

-- 3. B-09 同步
ALTER TABLE ent_table_extraction_result
  ADD COLUMN IF NOT EXISTS enterprise_code VARCHAR(50) NULL
    COMMENT '所属企业编码',
  ADD COLUMN IF NOT EXISTS phase_code VARCHAR(20) NULL
    COMMENT '所属阶段编码';

-- 4. 索引（幂等）
CREATE INDEX IF NOT EXISTS idx_ent_ext_result_ent_phase_label
  ON ent_extraction_result(enterprise_code, phase_code, label_tag);

CREATE INDEX IF NOT EXISTS idx_ent_table_ext_result_ent_phase
  ON ent_table_extraction_result(enterprise_code, phase_code, RuleCode);
```

---

## 三、C# 实体同步修改

### 3.1 ExtractionResult 新增字段

**文件**：`VOL.Entity/CertPlatform/Ent/ExtractionResult.cs`

```csharp
// 新增字段（在现有实体基础上追加）
[Column("enterprise_code")]
[StringLength(50)]
public string EnterpriseCode { get; set; }

[Column("phase_code")]
[StringLength(20)]
public string PhaseCode { get; set; }
```

### 3.2 TableExtractionResult 新增字段

**文件**：`VOL.Entity/CertPlatform/Ent/TableExtractionResult.cs`

```csharp
// 新增字段
[Column("enterprise_code")]
[StringLength(50)]
public string EnterpriseCode { get; set; }

[Column("phase_code")]
[StringLength(20)]
public string PhaseCode { get; set; }
```

### 3.3 常量定义

**文件**：`VOL.Builder/Services/CertPlatform/DocExtractionRuleService.cs`（或新建 `Constants/YzhStdConstants.cs`）

```csharp
/// <summary>
/// YZH 标准企业编码常量
/// 用途：标准目录文件提取结果落入 B-08/B-09 时，enterprise_code 固定为此值
/// 工作流设计器测试运行时，注入此值即可查询到标准目录的提取数据
/// </summary>
public const string YZH_STANDARD_ENTERPRISE_CODE = "YZH-STD-ENT";
```

---

## 四、SaveExtractionRuleAsync 改造方案

### 4.1 当前流程 vs 改造后流程

```
当前流程：
  SaveExtractionRuleAsync(request)
    ├── 1. 查找/创建 CertDocExtractionRule
    ├── 2. 更新规则信息 (skill/prompt/isValid/status)
    ├── 3. 删除旧字段定义 → 保存新字段定义 (cert_doc_field_def)
    ├── 4. 删除旧表格定义 → 保存新表格定义 (cert_doc_table_def + table_field_def)
    └── 5. 提交事务
    ❌ 验证时 AI 提取的数据没有落库

改造后流程：
  SaveExtractionRuleAsync(request)
    ├── 1. 查找/创建 CertDocExtractionRule（不变）
    ├── 2. 更新规则信息（不变）
    ├── 3. 删除旧字段定义 → 保存新字段定义（不变）
    ├── 4. 删除旧表格定义 → 保存新表格定义（不变）
    ├── 5.【新增】同步提取结果到 B-08/B-09（YZH 标准企业）
    │     ├── 5a. 删除该 file_code 在 B-08/B-09 中 YZH 标准企业的旧提取结果
    │     ├── 5b. 如果 request 中有验证提取数据（verifyResult），写入 B-08
    │     └── 5c. 如果 request 中有验证提取表格数据，写入 B-09
    └── 6. 提交事务
```

### 4.2 提取数据来源

提取数据有两个来源，按优先级使用：

| 来源 | 时机 | 数据格式 | 说明 |
|------|------|----------|------|
| **验证结果**（优先） | 用户点击"验证 Prompt"时 | `ExtractionData { Fields, Tables }` | AI 实际提取的结构化数据，最准确 |
| **分析结果**（兜底） | 用户点击"AI 分析"时 | `FieldDefDto.ExtractedValue` / `TableDefDto.ExtractedData` | AI 推荐时附带的预览值 |

**关键决策**：在 `SaveExtractionRuleRequest` 中新增 `ExtractionData` 字段，前端保存时将最近一次验证（或分析）的提取结果一并提交。

### 4.3 DTO 扩展

**文件**：`VOL.Entity/CertPlatform/DocExtraction/DTOs/ExtractionRuleDto.cs`

```csharp
public class SaveExtractionRuleRequest
{
    // ... 现有字段不变 ...

    /// <summary>
    /// 验证/分析时的提取数据（可选）
    /// 如果有值，保存规则时同步落入 B-08/B-09（YZH 标准企业）
    /// </summary>
    public ExtractionData ExtractionData { get; set; }
}
```

### 4.4 后端 SaveExtractionRuleAsync 改造

```csharp
public async Task<bool> SaveExtractionRuleAsync(SaveExtractionRuleRequest request)
{
    using var transaction = await repository.DbContext.Database.BeginTransactionAsync();
    try
    {
        // === 1~4: 现有逻辑不变（规则 + 字段定义 + 表格定义） ===
        // ... 省略，保持现有代码不变 ...

        // === 5.【新增】同步提取结果到 B-08/B-09 ===
        await SyncExtractionResultToB08B09Async(rule.Code, request.FileCode, request.ExtractionData);

        await repository.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}

/// <summary>
/// 将验证/分析提取结果同步到 B-08/B-09（YZH 标准企业）
/// </summary>
private async Task SyncExtractionResultToB08B09Async(
    string ruleCode,
    string fileCode,
    ExtractionData extractionData)
{
    if (extractionData == null) return;

    var dbContext = repository.DbContext;
    var now = DateTime.Now;

    // 5a. 删除该 file_code 在 B-08 中 YZH 标准企业的旧提取结果
    var oldFieldResults = await dbContext.Set<ExtractionResult>()
        .Where(x => x.FileCode == fileCode
                 && x.EnterpriseCode == YZH_STANDARD_ENTERPRISE_CODE)
        .ToListAsync();
    dbContext.Set<ExtractionResult>().RemoveRange(oldFieldResults);

    // 5b. 删除该 file_code 在 B-09 中 YZH 标准企业的旧表格提取结果
    var oldTableResults = await dbContext.Set<TableExtractionResult>()
        .Where(x => x.FileCode == fileCode
                 && x.EnterpriseCode == YZH_STANDARD_ENTERPRISE_CODE)
        .ToListAsync();
    dbContext.Set<TableExtractionResult>().RemoveRange(oldTableResults);

    // 5c. 写入新的字段提取结果到 B-08
    if (extractionData.Fields != null && extractionData.Fields.Count > 0)
    {
        foreach (var kv in extractionData.Fields)
        {
            var fieldCode = kv.Key;
            var value = kv.Value?.ToString();

            var result = new ExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                FileCode = fileCode,
                VersionNumber = 1,
                RuleCode = ruleCode,
                FieldCode = fieldCode,
                LabelTag = fieldCode,  // 标准目录数据：label_tag = field_code
                ExtractedValue = value,
                Confidence = null,     // 标准目录数据无可信度（人工确认）
                PositionInfo = null,
                IsManualEdited = false,
                ExtractedAt = now,
                EnterpriseCode = YZH_STANDARD_ENTERPRISE_CODE,
                PhaseCode = null       // 标准目录数据不区分阶段
            };
            dbContext.Set<ExtractionResult>().Add(result);
        }
    }

    // 5d. 写入新的表格提取结果到 B-09
    if (extractionData.Tables != null && extractionData.Tables.Count > 0)
    {
        var tableIndex = 1;
        foreach (var kv in extractionData.Tables)
        {
            var tableCode = kv.Key;
            var rows = kv.Value;

            var result = new TableExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                FileCode = fileCode,
                VersionNumber = 1,
                RuleCode = ruleCode,
                TableIndex = tableIndex++,
                ExtractedJson = System.Text.Json.JsonSerializer.Serialize(rows),
                Confidence = null,
                PositionInfo = null,
                ExtractedAt = now,
                EnterpriseCode = YZH_STANDARD_ENTERPRISE_CODE,
                PhaseCode = null
            };
            dbContext.Set<TableExtractionResult>().Add(result);
        }
    }
}
```

### 4.5 前端改造

**文件**：`src/views/cert/Standard/DocExtractionRule/index.vue`

在 `saveRule` 方法中，将最近一次验证/分析的提取数据一并提交：

```javascript
const saveRule = async () => {
  // ... 现有校验逻辑不变 ...

  // 收集提取数据：优先用验证结果，无验证结果时用分析结果
  let extractionData = null
  if (verifyResult.value?.data) {
    // 验证结果优先（AI 实际提取的结构化数据）
    extractionData = {
      Fields: verifyResult.value.data.fields || {},
      Tables: verifyResult.value.data.tables || {}
    }
  } else {
    // 兜底：从分析结果中提取预览值
    const fields = {}
    analysisFields.value.forEach(f => {
      const code = f.nameEn || f.code
      if (code && f.extractedValue) {
        fields[code] = f.extractedValue
      }
    })
    const tables = {}
    analysisTables.value.forEach(t => {
      const code = t.nameEn || t.code
      if (code && t.extractedData?.length > 0) {
        tables[code] = t.extractedData
      }
    })
    if (Object.keys(fields).length > 0 || Object.keys(tables).length > 0) {
      extractionData = { Fields: fields, Tables: tables }
    }
  }

  saving.value = true
  try {
    const res = await saveExtractionRule({
      fileCode: currentFile.value.fileCode,
      skill,
      fields: analysisFields.value,
      tables: analysisTables.value,
      prompt: generatedPrompt.value,
      isValid,
      extractionData  // ← 新增：传递提取数据
    })
    // ... 后续处理不变 ...
  }
}
```

---

## 五、数据流全景图

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           规则配置阶段                                           │
│                                                                                 │
│  管理员选择标准目录文件 → AI 分析 → 生成 Prompt → 验证 → 保存规则              │
│                                                                                 │
│  保存规则时：                                                                   │
│  ┌──────────────────────────────────────────────────────────────────┐           │
│  │ cert_doc_extraction_rule     ← 规则定义（skill/prompt/status）  │           │
│  │ cert_doc_field_def           ← 字段定义（code/name/type）        │           │
│  │ cert_doc_table_def           ← 表格定义（code/name/columns）     │           │
│  │ cert_doc_table_field_def     ← 表格列定义                        │           │
│  ├──────────────────────────────────────────────────────────────────┤           │
│  │ ent_extraction_result (B-08)  ← 字段提取值（YZH 标准企业）       │ ← 新增    │
│  │   enterprise_code = 'YZH-STD-ENT'                               │           │
│  │   file_code = 标准目录文件编码                                   │           │
│  │   field_code = 字段编码                                         │           │
│  │   label_tag = 字段编码（工作流引用键）                           │           │
│  │   extracted_value = AI 提取值                                    │           │
│  │                                                                  │           │
│  │ ent_table_extraction_result (B-09) ← 表格提取值（YZH 标准企业）  │ ← 新增    │
│  │   enterprise_code = 'YZH-STD-ENT'                               │           │
│  │   file_code = 标准目录文件编码                                   │           │
│  │   extracted_json = 表格数据 JSON                                 │           │
│  └──────────────────────────────────────────────────────────────────┘           │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                           工作流设计器阶段                                       │
│                                                                                 │
│  设计器画布上编排工作流：                                                       │
│  ├── [n1: get_field] inputs.label_tag = "企业名称"                              │
│  ├── [n2: get_field] inputs.label_tag = "统一信用代码"                           │
│  ├── [n3: compare] inputs.rule = "not_empty"                                   │
│  └── [n4: create_nc] condition = equals(false)                                 │
│                                                                                 │
│  测试运行（注入 enterprise_code='YZH-STD-ENT'）：                               │
│  get_field["企业名称"]                                                           │
│    → SELECT * FROM ent_extraction_result                                         │
│      WHERE enterprise_code = 'YZH-STD-ENT'                                       │
│        AND label_tag = '企业名称'                                                │
│    → ✅ 返回标准目录文件的提取值                                                 │
│                                                                                 │
│  线性管道：n1 → n3, n2 → n3 → n4                                               │
│  → compare 节点获取到真实值 → 判定 not_empty → 不触发 NC                        │
│  → 工作流测试成功！                                                             │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                           企业运行阶段（未来）                                   │
│                                                                                 │
│  审核员为真实企业上传文件 → 触发提取 → 落 B-08/B-09                             │
│    enterprise_code = 'CB001'（真实企业编码）                                     │
│    label_tag = '企业名称'（与标准目录相同的标签）                                │
│                                                                                 │
│  审核员点击"审核" → 执行校验工作流                                               │
│    enterprise_code = 'CB001'（运行时注入真实企业编码）                           │
│    get_field["企业名称"] → 查到真实企业的提取值                                  │
│    → compare → 判定 → 可能触发 NC                                               │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 六、label_tag 生成规则

### 6.1 标准目录数据的 label_tag 格式

按照 `数据库表设计-V2.md` §A-09 的设计，`label_tag` 格式为：

```
[{标准编码}_{文档名称}_{字段名称}]
```

**示例**：
```
[ISO9001_管理评审记录_评审日期]
[ISO9001_管理评审记录_评审结论]
[ISO9001_营业执照_企业名称]
[ISO9001_营业执照_统一信用代码]
```

### 6.2 当前阶段的简化方案

当前 `cert_doc_field_def` 中的 `field_code` 字段已包含唯一标识（如 `企业名称`、`统一信用代码`），在标准目录数据场景下：

- **label_tag = field_code**（直接用字段编码作为标签）
- 后续 F-02 `wf_field_label_mapping` 表完善后，可自动生成完整的 `[标准_文档_字段]` 格式标签

### 6.3 与 F-02 的关系

F-02 `wf_field_label_mapping` 表负责建立 `label_tag → field_code` 的映射。在当前阶段：

1. 标准目录数据直接用 `field_code` 作为 `label_tag` 写入 B-08
2. 工作流设计器中 `get_field` 节点的 `inputs.label_tag` 直接填写 `field_code`
3. 后续 F-02 完善后，工作流节点可使用更友好的 `[ISO9001_营业执照_企业名称]` 格式，由 F-02 解析为 `field_code` 再查 B-08

---

## 七、对后续开发的影响分析

### 7.1 正面影响

| 影响项 | 说明 |
|--------|------|
| **Phase E 提前部分完成** | 规则保存即落 B-08/B-09，不需要等上传链路接通就有数据 |
| **工作流设计器可立即测试** | get_field/get_table 能查到标准目录的提取值 |
| **提取链路验证** | 规则保存时完整走通了"文件 → 提取 → 落库 → 查询"全链路 |
| **标杆数据** | 标准目录的提取值可作为后续企业提取的参照基线 |
| **F-02 标签映射种子数据** | 规则保存时的字段定义可自动生成 F-02 映射记录 |

### 7.2 需要注意的风险

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| **标准企业数据污染** | YZH 标准企业的 B-08/B-09 数据被误查为真实企业数据 | GetFieldSkill 查询时必须传 enterprise_code，不传或传错不会查到 YZH 标准企业数据 |
| **文件重复提取** | 同一文件规则修改后重新保存，需清理旧数据 | SaveExtractionRuleAsync 中已设计"先删旧再写新"逻辑 |
| **验证数据缺失** | 用户未点验证就保存，B-08/B-09 无数据 | 前端兜底：用 AI 分析时的预览值（extractedValue/extractedData）作为兜底数据源 |
| **字段编码不统一** | AI 返回的字段编码可能与 cert_doc_field_def 中的不一致 | 保存规则时已统一生成 field_code，提取结果用同一 field_code 写入 B-08 |

### 7.3 对 Phase E/F/G 的影响

```
Phase E（数据提取管道接通）：
  ├── E1 DocumentExtractSkill 接真实 IFileExtractor → 不受影响，独立推进
  ├── E2 LlmExtractSkill 落 B-08/B-09 → 本方案部分实现了 E2 的目标
  │     区别：E2 是"企业上传触发自动落库"，本方案是"规则保存手动触发落库"
  │     合并：E2 实现时可复用本方案的 SyncExtractionResultToB08B09Async 方法
  └── E3 上传触发提取链路 → 不受影响，独立推进

Phase F（工作流设计器）：
  ├── F1 LogicFlow PoC → 可立即启动，因为 B-08/B-09 已有数据
  ├── F2 workflow-designer 模块 → get_field 节点测试运行时注入 enterprise_code='YZH-STD-ENT'
  ├── F3 后端 Service/Controller → 不受影响
  ├── F4 前端页面 → 测试运行按钮可调用 WorkflowEngine.RunAsync()
  └── F5 与 DocExtractionRule 集成 → 可增加"查看提取数据"入口

Phase G（校验/报告引擎接入）：
  └── 校验规则的工作流可基于 YZH 标准企业的数据做测试运行
```

---

## 八、实施任务分解

### Task 1：数据库变更（自动化执行）

| 步骤 | 文件 | 说明 |
|------|------|------|
| 1.1 | `DB/mysql/cert_yzh_standard_enterprise.sql` | 创建 SQL 脚本 |
| 1.2 | 自动执行 | `docker exec -i yzh-mysql mysql ...` |
| 1.3 | 验证 | 查询 `ent_enterprise WHERE Code='YZH-STD-ENT'` 确认记录存在 |

### Task 2：C# 实体修改

| 步骤 | 文件 | 说明 |
|------|------|------|
| 2.1 | `VOL.Entity/CertPlatform/Ent/ExtractionResult.cs` | 新增 EnterpriseCode/PhaseCode |
| 2.2 | `VOL.Entity/CertPlatform/Ent/TableExtractionResult.cs` | 同上 |
| 2.3 | 编译验证 | `dotnet build` 无错误 |

### Task 3：DTO 扩展

| 步骤 | 文件 | 说明 |
|------|------|------|
| 3.1 | `VOL.Entity/CertPlatform/DocExtraction/DTOs/ExtractionRuleDto.cs` | SaveExtractionRuleRequest 新增 ExtractionData 字段 |

### Task 4：后端 Service 改造

| 步骤 | 文件 | 说明 |
|------|------|------|
| 4.1 | `VOL.Builder/Services/CertPlatform/DocExtractionRuleService.cs` | 新增 SyncExtractionResultToB08B09Async 方法 |
| 4.2 | 同上 | 在 SaveExtractionRuleAsync 末尾调用同步方法 |
| 4.3 | 编译验证 | `dotnet build` 无错误 |

### Task 5：前端改造

| 步骤 | 文件 | 说明 |
|------|------|------|
| 5.1 | `src/views/cert/Standard/DocExtractionRule/index.vue` | saveRule 方法收集提取数据并传递 |
| 5.2 | `src/views/cert/Standard/DocExtractionRule/api.js`（如有） | 确认 saveExtractionRule 透传 extractionData |

### Task 6：端到端验证

| 步骤 | 说明 |
|------|------|
| 6.1 | 在 DocExtractionRule 页面选择一个标准目录文件 |
| 6.2 | 点击 AI 分析 → 生成 Prompt → 验证 → 保存规则 |
| 6.3 | 查询 `ent_extraction_result WHERE enterprise_code='YZH-STD-ENT'` 确认有数据 |
| 6.4 | 查询 `ent_table_extraction_result WHERE enterprise_code='YZH-STD-ENT'` 确认有数据 |

---

## 九、后续扩展建议

### 9.1 F-02 标签映射自动生成

在规则保存成功后，可自动为每个字段生成 F-02 `wf_field_label_mapping` 记录：

```
field_code = "企业名称"
label_tag = "[ISO9001_营业执照_企业名称]"
→ wf_field_label_mapping 表新增一条映射记录
→ 工作流设计器的标签选择器可直接读取此树形结构
```

### 9.2 标准目录数据的对比基线

当真实企业上传相同文件后，可对比 YZH 标准企业与真实企业的提取结果：

```sql
-- 对比示例：某企业的"企业名称"提取值 vs 标准目录的提取值
SELECT
  std.ExtractedValue AS standard_value,
  ent.ExtractedValue AS enterprise_value,
  CASE
    WHEN std.ExtractedValue = ent.ExtractedValue THEN 'MATCH'
    ELSE 'MISMATCH'
  END AS comparison
FROM ent_extraction_result std
JOIN ent_extraction_result ent
  ON std.FieldCode = ent.FieldCode
  AND ent.EnterpriseCode = 'CB001'  -- 真实企业
WHERE std.EnterpriseCode = 'YZH-STD-ENT'
  AND std.FieldCode = '企业名称';
```

### 9.3 工作流设计器测试运行

工作流设计器的"测试运行"按钮可注入以下上下文：

```javascript
// 前端调用测试运行 API
POST /api/WfWorkflowDefinition/test/{id}
{
  "businessType": "validation",
  "businessId": 0,
  "enterpriseCode": "YZH-STD-ENT",  // 注入 YZH 标准企业
  "phaseCode": null                   // 标准目录数据不区分阶段
}
```

后端 `WorkflowContext` 接收后，`GetFieldSkill` 按 `enterprise_code='YZH-STD-ENT'` 查询 B-08，返回标准目录的提取值。

### 9.4 与 Phase E 的整合

当 Phase E 的 E2（LlmExtractSkill 落 B-08/B-09）实现后，企业上传文件自动提取落库的代码可复用本方案的 `SyncExtractionResultToB08B09Async` 方法，只需将 `enterprise_code` 参数从常量改为运行时注入的企业编码即可。

---

## 十、验收标准

```
[ ] ent_enterprise 表中存在 Code='YZH-STD-ENT' 的记录
[ ] ent_extraction_result 表新增 enterprise_code 和 phase_code 字段
[ ] ent_table_extraction_result 表同上
[ ] C# 实体 ExtractionResult/TableExtractionResult 新增 EnterpriseCode/PhaseCode 属性
[ ] dotnet build 无错误
[ ] 前端保存规则时传递 extractionData
[ ] 保存规则后，B-08 中有 enterprise_code='YZH-STD-ENT' 的提取记录
[ ] 保存规则后，B-09 中有 enterprise_code='YZH-STD-ENT' 的表格记录
[ ] 规则重新保存时，旧提取结果被清理（先删后增，不残留）
[ ] 查询 B-08 WHERE enterprise_code='YZH-STD-ENT' AND label_tag='某字段编码' 能返回提取值
```

---

> **文档版本**：V1.0
> **创建时间**：2026-08-14
> **状态**：待评审——确认后立即进入实施
>
> **下一步行动**：
> 1. 用户评审本设计文档
> 2. 确认后按 Task 1~6 顺序实施
> 3. 端到端验证通过后，可立即启动 Phase F（LogicFlow 工作流设计器）
