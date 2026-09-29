# 后端手写 SQL 问题分析报告（视图方案版）

> **版本**：V4.0 | **日期**：2026-09-20 | **状态**：已确认执行
>
> **核心原则**：
> - ✅ 复杂跨表查询通过数据库视图实现
> - ✅ 视图对应实体类映射到 ViewName
> - ❌ 禁止在后端代码中出现明显的手写SQL字符串
> - ✅ 单表简单查询使用 LINQ + 标准CRUD
>
> **决策记录**：不增加 `withValidZero` 参数，由各业务功能自行决定过滤逻辑。

---

## 一、现状统计

### 1.1 手写SQL分布

| 文件 | 手写SQL数量 | SQL方法 | 主要用途 |
|------|------------|---------|---------|
| `CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs` | ~30处 | `SqlQueryAsync` / `SqlExecuteAsync` / `QueryFirstOrDefaultAsync` | 上传流程、文件树、队列管理、转换重试 |
| `CertPlatform.Admin/Services/DocExtraction/DocExtractionRuleService.cs` | ~3处 | `SqlQueryAsync` / LINQ `ExecuteCommandAsync` | 规则列表查询、提取结果同步 |
| **合计** | **~33处** | - | - |

---

## 二、根因分析

### 2.1 跨表 JOIN 查询无视图支持

`cert_doc_extraction_rule` 需要关联 `cert_standard_directory_file` 获取文件名称等信息，但缺少专用视图。

### 2.2 中间状态查询无视图支持

上传流程需要查询 `IsValid=0` 的中间态文件记录，现有视图未覆盖此场景。

### 2.3 实体定义不完整

`PhaseDefinition` 缺少 `ISoftDelete`/`IIsValid` 接口实现，导致无法使用标准CRUD。

---

## 三、修复方案（视图优先）

### 修复项1（P1）：补全 `PhaseDefinition` 实体类

```csharp
[SugarTable("cert_phase_definition")]
public class PhaseDefinition : BaseEntity, ISoftDelete, IIsValid
{
    [Required]
    [StringLength(20)]
    [UniqueField("阶段编码")]
    public string PhaseCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string PhaseName { get; set; } = string.Empty;

    public int SequenceOrder { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    // ISoftDelete
    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }

    // IIsValid
    public int IsValid { get; set; } = 1;
}
```

**消除手写SQL**：1处

---

### 修复项2（P1）：创建规则列表视图

#### 2.1 数据库视图定义

```sql
-- scripts/db/views/v_cert_configured_rules.sql
-- 已配置规则列表视图（跨表JOIN）
CREATE OR REPLACE VIEW v_cert_configured_rules AS
SELECT 
    r.Code AS RuleCode,
    r.StandardFileCode AS StandardFileCode,
    COALESCE(f.FileName, r.StandardFileCode) AS FileName,
    COALESCE(r.StandardCode, '') AS StandardCode,
    COALESCE(r.PhaseCode, '') AS PhaseCode,
    r.Skill AS Skill,
    r.DocIsValid AS DocIsValid,
    r.Status AS Status,
    r.CreateTime AS CreateTime,
    r.UpdateTime AS UpdateTime
FROM cert_doc_extraction_rule r
LEFT JOIN cert_standard_directory_file f 
    ON r.StandardFileCode = f.FileCode AND f.IsDeleted = 0
WHERE r.IsDeleted = 0 AND r.IsValid = 1;
```

#### 2.2 对应实体类

```csharp
// CertPlatform.Shared/Entities/Doc/ConfiguredRuleView.cs
namespace CertPlatform.Shared.Entities.Doc;

[SugarTable("v_cert_configured_rules")]
public class ConfiguredRuleView : BaseEntity
{
    // 视图不支持基础审计字段，标记为忽略
    [SugarColumn(IsIgnore = true)]
    public new long Id { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new string? Code { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new DateTime CreateTime { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new string? CreateBy { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new DateTime? UpdateTime { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new string? UpdateBy { get; set; }

    /// <summary>规则编码</summary>
    [SugarColumn(ColumnName = "RuleCode")]
    public string RuleCode { get; set; } = "";

    /// <summary>标准文件编码</summary>
    [SugarColumn(ColumnName = "StandardFileCode")]
    public string StandardFileCode { get; set; } = "";

    /// <summary>文件名称</summary>
    [SugarColumn(ColumnName = "FileName")]
    public string FileName { get; set; } = "";

    /// <summary>标准编码</summary>
    [SugarColumn(ColumnName = "StandardCode")]
    public string StandardCode { get; set; } = "";

    /// <summary>阶段编码</summary>
    [SugarColumn(ColumnName = "PhaseCode")]
    public string PhaseCode { get; set; } = "";

    /// <summary>技能类型</summary>
    [SugarColumn(ColumnName = "Skill")]
    public string Skill { get; set; } = "";

    /// <summary>是否有效</summary>
    [SugarColumn(ColumnName = "DocIsValid")]
    public bool DocIsValid { get; set; }

    /// <summary>状态</summary>
    [SugarColumn(ColumnName = "Status")]
    public string Status { get; set; } = "";

    // 视图只读
    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsIgnore = true)]
    public int IsValid { get; set; } = 1;
}
```

**消除手写SQL**：~3处（DocExtractionRuleService.cs）

---

### 修复项3（P1）：创建上传任务详情视图

#### 3.1 数据库视图定义

```sql
-- scripts/db/views/v_upload_task_detail.sql
-- 上传任务详情视图（包含文件状态）
CREATE OR REPLACE VIEW v_upload_task_detail AS
SELECT 
    t.TaskId,
    t.DirectoryCode,
    t.TotalFiles,
    t.SuccessCount,
    t.Status,
    t.ExpireTime,
    f.FileCode,
    f.FileName,
    f.UploadStatus,
    f.StoragePath,
    f.IsValid AS FileIsValid,
    f.IsDeleted AS FileIsDeleted
FROM cert_upload_task t
LEFT JOIN cert_standard_directory_file f 
    ON t.TaskId = f.TaskId AND f.IsDeleted = 0;
```

#### 3.2 对应实体类

```csharp
// CertPlatform.Shared/Entities/Dir/UploadTaskDetailView.cs
namespace CertPlatform.Shared.Entities.Dir;

[SugarTable("v_upload_task_detail")]
public class UploadTaskDetailView : BaseEntity
{
    [SugarColumn(IsIgnore = true)]
    public new long Id { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new string? Code { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new DateTime CreateTime { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new string? CreateBy { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new DateTime? UpdateTime { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public new string? UpdateBy { get; set; }

    [SugarColumn(ColumnName = "TaskId")]
    public string TaskId { get; set; } = "";

    [SugarColumn(ColumnName = "DirectoryCode")]
    public string DirectoryCode { get; set; } = "";

    [SugarColumn(ColumnName = "TotalFiles")]
    public int TotalFiles { get; set; }

    [SugarColumn(ColumnName = "SuccessCount")]
    public int SuccessCount { get; set; }

    [SugarColumn(ColumnName = "Status")]
    public string Status { get; set; } = "";

    [SugarColumn(ColumnName = "ExpireTime")]
    public DateTime? ExpireTime { get; set; }

    [SugarColumn(ColumnName = "FileCode")]
    public string? FileCode { get; set; }

    [SugarColumn(ColumnName = "FileName")]
    public string? FileName { get; set; }

    [SugarColumn(ColumnName = "UploadStatus")]
    public string? UploadStatus { get; set; }

    [SugarColumn(ColumnName = "StoragePath")]
    public string? StoragePath { get; set; }

    [SugarColumn(ColumnName = "FileIsValid")]
    public int? FileIsValid { get; set; }

    [SugarColumn(ColumnName = "FileIsDeleted")]
    public bool? FileIsDeleted { get; set; }

    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsIgnore = true)]
    public int IsValid { get; set; } = 1;
}
```

**消除手写SQL**：~12处（StandardDirectoryService.cs 上传流程）

---

### 修复项4（P2）：批量写入接口封装

在 `IDbOrm` 中增加批量操作方法，封装原始SQL，对外暴露业务语义清晰的方法。

```csharp
// IDbOrm.cs
/// <summary>
/// 按条件批量更新指定字段（用于中间状态流转）
/// </summary>
Task<Result<int>> BulkUpdateByConditionAsync<T>(
    Expression<Func<T, bool>> filter,
    Action<T> updater,
    params string[] fields) where T : class, new();

/// <summary>
/// 按条件物理删除（仅用于清理临时/草稿数据）
/// </summary>
Task<Result<int>> PhysicalDeleteByConditionAsync<T>(
    Expression<Func<T, bool>> filter) where T : class, new();
```

**消除手写SQL**：~10处

---

## 四、修复优先级与工作量

| 优先级 | 修复项 | 涉及文件 | 消除数量 | 工作量 |
|--------|--------|---------|---------|--------|
| **P1** | 补全 `PhaseDefinition` 实体 | `CertPlatform.Shared/Entities/Cert/` | 1处 | 小（10分钟） |
| **P1** | 创建规则列表视图 + 实体 | `scripts/db/views/` + 新实体类 | ~3处 | 中（30分钟） |
| **P1** | 创建上传任务详情视图 + 实体 | `scripts/db/views/` + 新实体类 | ~12处 | 中（30分钟） |
| **P2** | 批量写入接口封装 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` | ~10处 | 中（1小时） |
| **P2** | 其他视图补充 | 按需创建 | ~7处 | 待定 |

**总计可消除约33处手写SQL**。

---

## 五、实施计划

### 第一期（2-3天）

1. **Day 1**：补全 `PhaseDefinition` 实体
2. **Day 1**：创建 `v_cert_configured_rules` 视图及实体
3. **Day 2**：创建 `v_upload_task_detail` 视图及实体
4. **Day 2-3**：替换 `DocExtractionRuleService.cs` 中的手写SQL

### 第二期（1周）

1. **Day 4-5**：替换 `StandardDirectoryService.cs` 中的上传流程手写SQL
2. **Day 6-7**：批量写入接口封装 + 其他视图补充
3. **Day 7**：回归测试上传流程（4步上传+取消+确认）

---

## 六、关键设计原则

### 6.1 视图命名规范

```
v_{业务域}_{业务含义}
```

示例：
- `v_cert_configured_rules` — 认证域-已配置规则列表
- `v_upload_task_detail` — 上传域-任务详情

### 6.2 视图实体类规范

```csharp
// 1. 类名以 View 结尾
[SugarTable("v_xxx")]
public class XxxView : BaseEntity

// 2. 视图无自增主键，忽略基类 Id
[SugarColumn(IsIgnore = true)]
public new long Id { get; set; }

// 3. 视图无业务编码，忽略基类 Code
[SugarColumn(IsIgnore = true)]
public new string? Code { get; set; }

// 4. 视图无审计字段，忽略基类 CreateTime/UpdateBy 等
[SugarColumn(IsIgnore = true)]
public new DateTime CreateTime { get; set; }
// ...

// 5. 显式映射视图返回字段
[SugarColumn(ColumnName = "ViewFieldName")]
public string MappedField { get; set; }

// 6. 视图只读，禁用增删改相关字段
[SugarColumn(IsIgnore = true)]
public bool IsDeleted { get; set; }

[SugarColumn(IsIgnore = true)]
public int IsValid { get; set; } = 1;
```

### 6.3 禁止事项

| 禁止内容 | 原因 |
|---------|------|
| 业务代码中直接写 `"SELECT * FROM table"` | 维护困难，类型不安全 |
| 视图实体类继承 `ISoftDelete` 并实现 `IsDeleted` | 视图不支持软删除操作 |
| 为视图添加 `[SugarTable]` 指向物理表 | 会引发ORM误解 |

---

## 七、验收标准

1. `StandardDirectoryService.cs` 和 `DocExtractionRuleService.cs` 中不再出现 `SqlQueryAsync<string>()` / `SqlExecuteAsync()` 调用
2. 所有跨表查询通过视图实现
3. 单元测试覆盖核心上传流程
4. 新建视图脚本统一存放在 `scripts/db/views/` 目录

---

*文档生成于 2026-09-20，已由用户确认执行。*
