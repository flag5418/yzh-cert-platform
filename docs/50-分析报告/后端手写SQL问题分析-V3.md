# 后端手写 SQL 问题分析报告（修正版）

> **版本**：V3.0 | **日期**：2026-09-20 | **状态**：已修正
>
> **修正说明**：V2 版本分析基于错误的前提（BaseEntity字段IsIgnore）。本次V3新增核心原则：**视图是数据库和架构中不可缺少的核心组件，复杂查询应通过数据库视图实现，而非在后端编写手写SQL**。
>
> **范围**：`src/certplatform-api/`（新架构后端，不含旧 `src/old/`）
>
> **核心原则**：
> - ✅ 允许使用数据库视图（View）作为复杂查询的载体
> - ❌ 不允许在后端代码中出现明显的手写SQL字符串
> - ✅ LINQ表达式是标准CRUD查询的首选方式
> - ✅ 视图+实体映射是跨表JOIN查询的标准方案

---

## 一、现状统计

### 1.1 手写SQL分布

| 文件 | 手写SQL数量 | SQL方法 | 主要用途 |
|------|------------|---------|---------|
| `CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs` | ~30处 | `SqlQueryAsync` / `SqlExecuteAsync` / `QueryFirstOrDefaultAsync` | 上传流程、文件树、队列管理、转换重试 |
| `CertPlatform.Admin/Services/DocExtraction/DocExtractionRuleService.cs` | ~3处 | `SqlQueryAsync` / LINQ `ExecuteCommandAsync` | 规则列表查询、提取结果同步 |
| **合计** | **~33处** | - | - |

### 1.2 SQL 使用比例估算

| 维度 | 数据 |
|------|------|
| 业务 Service 文件总数 | ~15个（含 Admin/Auditor/Enterprise） |
| 手写SQL文件数 | 2个 |
| 手写SQL占比 | ~13%（按文件计），但集中度高（这两个文件承担了核心业务流） |
| 标准CRUD方法调用数 | ~80+ 处（来自 `GetListAsync`/`GetOneAsync`/`InsertAsync`/`UpdateAsync`/`DeleteByCodeAsync`） |

---

## 二、`BaseEntity` 架构确认

### 2.1 强制字段设计（正确且稳固）

`src/yzh-core/YZH.Core.Stand/Models/Entity/BaseEntity.cs` 的强制字段设计遵循 YZH 架构规范：

```csharp
public abstract class BaseEntity : INotifyPropertyChanged
{
    // ── 强制字段（所有表必须存在，显式映射到 DB 列）──
    
    /// <summary>自增主键（物理自增 bigint，所有表必须存在）</summary>
    [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>业务编码（GUID 业务键，新增时框架自动生成，不可修改）</summary>
    [SugarColumn(ColumnName = "Code")]
    [StringLength(64)]
    public string? Code { get; set; }

    /// <summary>创建时间（创建时赋值，后续不变）</summary>
    [SugarColumn(ColumnName = "CreateTime")]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>创建人 Code（存储用户业务编码，非自增 ID）</summary>
    [SugarColumn(ColumnName = "CreateBy")]
    [StringLength(64)]
    public string? CreateBy { get; set; }

    /// <summary>更新时间（仅更新时赋值，新建时为 null）</summary>
    [SugarColumn(ColumnName = "UpdateTime")]
    public DateTime? UpdateTime { get; set; }

    /// <summary>更新人 Code（仅更新时赋值，存储用户业务编码）</summary>
    [SugarColumn(ColumnName = "UpdateBy")]
    [StringLength(64)]
    public string? UpdateBy { get; set; }
}
```

**设计原则**：
- 这些字段通过 `[SugarColumn(ColumnName = "...")]` 显式映射，不是 `IsIgnore`
- 是 YZH 架构底座，**不允许子类覆盖或忽略**
- 其他业务实体（如 `ISOStandard`、`StandardDirectoryFile`）**正确继承并复用**这些字段

### 2.2 可选接口字段（按需实现）

```csharp
// 接口 ISoftDelete - 软删除标记
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    string? DeleteBy { get; set; }
    DateTime? DeleteTime { get; set; }
}

// 接口 IIsValid - 有效性标记
public interface IIsValid
{
    int IsValid { get; set; }
}
```

子类通过实现接口来提供 `IsDeleted`/`IsValid` 等字段，例如：

```csharp
[SugarTable("cert_standard_directory_file")]
public class StandardDirectoryFile : BaseEntity, ISoftDelete, IIsValid
{
    // IsDeleted/IsValid 由接口契约提供
    public bool IsDeleted { get; set; }
    public int IsValid { get; set; } = 1;
    
    // ... 业务字段
}
```

---

## 三、手写SQL真正原因分析

### 3.1 【查询层】需要跨表 JOIN 查询

**问题描述**：配置规则列表需要关联两张表（`cert_doc_extraction_rule` LEFT JOIN `cert_standard_directory_file`），`IDbOrm` 仅支持单表 LINQ 查询。

**当前写法**（`DocExtractionRuleService.cs:493-500`）：

```csharp
var result = await _db.Client.Ado.SqlQueryAsync<ConfiguredRuleRow>(
    @"SELECT r.Code AS RuleCode, r.StandardFileCode AS StandardFileCode,
             COALESCE(f.FileName, r.StandardFileCode) AS FileName,
             COALESCE(r.StandardCode, '') AS StandardCode,
             COALESCE(r.PhaseCode, '') AS PhaseCode,
             r.Skill AS Skill, r.DocIsValid AS DocIsValid, r.Status AS Status,
             r.CreateTime AS CreateTime, r.UpdateTime AS UpdateTime
      FROM cert_doc_extraction_rule r
      LEFT JOIN cert_standard_directory_file f ON r.StandardFileCode = f.FileCode AND f.IsDeleted = 0
      WHERE r.IsDeleted = 0 AND r.IsValid = 1
      ORDER BY COALESCE(r.UpdateTime, r.CreateTime) DESC");
```

**正确做法**：在数据库创建视图，实体映射到视图。

---

### 3.2 【查询层】需要查询 `IsValid=0` 的中间状态记录

**问题描述**：上传流程设计为四步：`初始化 → 上传 → 确认 → 激活`。其中前两个阶段文件记录为 `IsValid=0`（草稿态），确认后才转为 `IsValid=1`（正式）。

`SqlSugarDbOrm.GetListAsync<T>()` 默认注入 `WHERE IsValid=1`，无法查到中间态数据。

**涉及代码位置**：

| 行号 | 操作 | 需要查询的条件 |
|------|------|---------------|
| L389-395 | `GetRootFilesAsync` | `IsValid=0` 或 `1`，带 `UploadStatus` 过滤 |
| L717 | `UploadFileAsync` | `IsValid=0` + `status='initialized'` |
| L723 | `UploadFileAsync` | `IsValid=0` + `TaskId` 匹配 |
| L787-788 | `UploadConfirmAsync` | `IsValid=0` + `TaskId` 匹配 |
| L882-883 | `UploadCancelAsync` | `IsValid=0` + `TaskId` 匹配 |
| L953-954 | `GetUploadStatusAsync` | `IsValid=0` + `TaskId` 匹配 |
| L1039-1045 | `CleanupOrphanDataAsync` | `IsValid=0` 孤儿数据清理 |
| L1580-1582 | `RetryFailedConversionsAsync` | `IsValid=0/1` 候选文件 |

**当前写法示例**：

```csharp
/// <summary>验证任务（绕过全局过滤：IsValid/IsDeleted 在 SqlSugarDbOrm.GetOneAsync 中自动加，
/// 此处用原生 SQL 以精确控制过滤条件）</summary>
var task = (await _db.QueryFirstOrDefaultAsync<UploadTask>(
    "SELECT * FROM cert_upload_task WHERE TaskId=@taskId AND status='initialized' AND IsDeleted=0",
    new { taskId })).Data;
```

---

### 3.3 【写入层】物理删除与批量 UPDATE

**问题描述**：标准CRUD只支持软删除（设置 `IsDeleted=true`），不支持物理删除；`UpdateAsync` 是单条更新，批量更新需要原始SQL。

**涉及代码位置**：

| 行号 | 操作类型 | 目的 |
|------|---------|------|
| L755 | `UPDATE` 多条 | 更新文件 `UploadStatus` |
| L760 | `UPDATE` 单条 | 更新任务计数 `SuccessCount` |
| L803 | `UPDATE` 多条 | 激活文件（`IsValid=0→1`） |
| L810 | `UPDATE` 多条 | 设置可转换文件 `ConvertStatus=pending` |
| L816 | `UPDATE` 多条 | 激活文件夹 |
| L906 | `UPDATE` 单条 | 回滚单个文件状态 |
| L914 | `DELETE` 单条 | 物理删除单个文件记录 |
| L931 | `DELETE` 单条 | 物理删除空文件夹记录 |
| L937 | `DELETE` 多条 | 物理删除上传任务 |
| L1039-1045 | `DELETE` 多条 | 清理孤儿数据 |

---

### 3.4 【表结构层】实体定义不完整

`PhaseDefinition` 实体缺少 `IsValid` 和 `Code` 等关键业务字段，无法使用标准CRUD。

**问题实体**：`src/certplatform-api/CertPlatform.Shared/Entities/Cert/PhaseDefinition.cs`

```csharp
[SugarTable("cert_phase_definition")]
public class PhaseDefinition : BaseEntity  // ⚠️ 未实现 ISoftDelete/IIsValid
{
    public string PhaseCode { get; set; }
    public string PhaseName { get; set; }
    public int SequenceOrder { get; set; }
    public string Description { get; set; }
    
    // ❌ 缺少：IsValid, IsDeleted 等接口字段
}
```

---

## 四、修复方案（基于视图为核心）

### 修复项1（P1）：补全 `PhaseDefinition` 实体类

```csharp
// 完整定义（替代现有 PhaseDefinition.cs）
[SugarTable("cert_phase_definition")]
public class PhaseDefinition : BaseEntity, ISoftDelete, IIsValid
{
    /// <summary>阶段编码（S1/S2/Surv1/Surv2/Recert）</summary>
    [Required]
    [StringLength(20)]
    [UniqueField("阶段编码")]
    public string PhaseCode { get; set; } = string.Empty;

    /// <summary>中文名称</summary>
    [Required]
    [StringLength(100)]
    public string PhaseName { get; set; } = string.Empty;

    /// <summary>顺序（1=S1 2=S2 3=一监 4=二监 5=再认证）</summary>
    public int SequenceOrder { get; set; }

    /// <summary>阶段说明</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    // ──── ISoftDelete 接口实现 ────
    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }

    // ──── IIsValid 接口实现 ────
    public int IsValid { get; set; } = 1;
}
```

**修复后可替换的手写SQL**：

```csharp
// 原手写SQL（StandardDirectoryService.cs L79-80）
// var phaseDefRows = await _db.SqlQueryAsync<PhaseDefDto>(
//     "SELECT PhaseCode, Code FROM cert_phase_definition WHERE IsValid=1 AND IsDeleted=0");
// var phaseDefMap = phaseDefRows.Data?.ToDictionary(x => x.PhaseCode, x => x.Code) ?? new();

// 修复后
var phaseDefs = (await _db.GetListAsync<PhaseDefinition>(x => x.IsValid == 1)).Data ?? new();
var phaseDefMap = phaseDefs.ToDictionary(x => x.PhaseCode, x => x.Code);
```

---

### 修复项2（P1）：创建数据库视图替代跨表查询

**原则**：复杂跨表查询应在数据库中通过视图实现，后端实体映射到视图，使用标准CRUD方法查询。

#### 2.1 规则列表视图

**SQL视图定义**（建议放入 `scripts/db/views/` 目录）：

```sql
-- 已配置规则列表视图
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
WHERE r.IsDeleted = 0 AND r.IsValid = 1
ORDER BY COALESCE(r.UpdateTime, r.CreateTime) DESC;
```

**对应实体类**：

```csharp
// CertPlatform.Shared/Entities/Doc/ConfiguredRuleView.cs
[SugarTable("v_cert_configured_rules")]  // 映射到视图而非表
public class ConfiguredRuleView : BaseEntity
{
    // 视图特有的字段（ BaseEntity 不提供 Code/CreateTime 等基础字段的业务语义）
    [SugarColumn(IsIgnore = true)]  // 视图无自增主键，忽略基类字段
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

    /// <summary>规则编码（来自 cert_doc_extraction_rule.Code）</summary>
    [SugarColumn(ColumnName = "RuleCode")]
    public string RuleCode { get; set; } = "";

    /// <summary>标准文件编码</summary>
    [SugarColumn(ColumnName = "StandardFileCode")]
    public string StandardFileCode { get; set; } = "";

    /// <summary>文件名称（优先取文件名，否则取规则编码）</summary>
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

    // 视图不支持增删改，以下字段标记为忽略
    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsIgnore = true)]
    public int IsValid { get; set; } = 1;
}
```

**后端使用方式**：

```csharp
// DocExtractionRuleService.cs
// 原：_db.Client.Ado.SqlQueryAsync<ConfiguredRuleRow>(...)
// 修复后：
var result = await _db.GetListAsync<ConfiguredRuleView>();
return result.Data?.Cast<object>().ToList() ?? new();
```

---

### 修复项3（P1）：创建中间状态文件视图

**原则**：需要查询 `IsValid=0` 中间状态的数据，创建专用视图。

#### 3.1 上传任务详情视图

```sql
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

    // 视图不支持增删改
    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsIgnore = true)]
    public int IsValid { get; set; } = 1;
}
```

**后端使用方式**：

```csharp
// StandardDirectoryService.cs — 原 L717-720
// 原：_db.QueryFirstOrDefaultAsync<UploadTask>("SELECT * FROM ...")
// 修复后：
var taskDetail = (await _db.GetOneAsync<UploadTaskDetailView>(
    x => x.TaskId == taskId && x.Status == "initialized")).Data;
if (taskDetail == null) return (false, "上传任务不存在或已过期", null);
```

---

### 修复项4（P1）：为 `IDbOrm` 增加 `withValidZero` 参数

**目标**：让标准CRUD支持查询 `IsValid=0` 的中间态数据，适用于无法创建视图的简单场景。

**接口变更**：

```csharp
// IDbOrm.cs
/// <summary>
/// 获取列表（withValidZero=true 时不过滤 IsValid=0，用于上传等中间状态查询）
/// </summary>
Task<Result<List<T>>> GetListAsync<T>(
    Expression<Func<T, bool>>? predicate = null,
    bool includeDisabled = false,
    bool withValidZero = false) where T : class, new();

/// <summary>
/// 获取单条（withValidZero=true 时不过滤 IsValid=0）
/// </summary>
Task<Result<T?>> GetOneAsync<T>(
    Expression<Func<T, bool>> predicate,
    bool withValidZero = false) where T : class, new();
```

**`SqlSugarDbOrm` 实现变更**：

```csharp
public async Task<Result<List<T>>> GetListAsync<T>(
    Expression<Func<T, bool>>? predicate = null,
    bool includeDisabled = false,
    bool withValidZero = false) where T : class, new()
{
    try
    {
        var query = _client.Queryable<T>().Where(IsDeletedCondition<T>());
        
        // 仅在 withValidZero=false 时添加 IsValid 过滤
        if (!withValidZero)
            query = query.Where(IsValidCondition<T>());
        
        if (predicate != null)
            query = query.Where(predicate);
        
        var list = await query.ToListAsync();
        return Result<List<T>>.Ok(list);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "GetListAsync 失败，Type={Type}", typeof(T).Name);
        return Result<List<T>>.Fail($"获取列表失败：{ex.Message}");
    }
}
```

**使用示例**（适用于简单场景）：

```csharp
// StandardDirectoryService.cs — 原 L389-395
// 原：_db.SqlQueryAsync<StandardDirectoryFile>("SELECT * FROM ...")
// 修复后：
var files = (await _db.GetListAsync<StandardDirectoryFile>(
    x => x.DirectoryCode == directoryCode 
        && (string.IsNullOrEmpty(x.FolderCode) || x.FolderCode == "")
        && (x.UploadStatus == "uploaded" || x.UploadStatus == "active"),
    withValidZero: true)).Data ?? new();
```

---

### 修复项5（P2）：批量写入接口

**目标**：替代零散的 `SqlExecuteAsync` 批量UPDATE/DELETE，保持代码整洁。

```csharp
// IDbOrm.cs
/// <summary>
/// 按条件批量更新指定字段（跳过软删除保护，用于中间状态流转）
/// </summary>
Task<Result<int>> BulkUpdateByConditionAsync<T>(
    Expression<Func<T, bool>> filter,
    Action<T> updater,
    params string[] fields) where T : class, new();

/// <summary>
/// 按条件物理删除（仅用于清理临时/草稿数据，需在方法文档中明确标注用途）
/// </summary>
Task<Result<int>> PhysicalDeleteByConditionAsync<T>(
    Expression<Func<T, bool>> filter) where T : class, new();
```

**使用示例**：

```csharp
// StandardDirectoryService.cs — UploadConfirmAsync 批量激活
// 原：_db.SqlExecuteAsync($"UPDATE cert_standard_directory_file SET IsValid=1...")
// 修复后：
await _db.BulkUpdateByConditionAsync<StandardDirectoryFile>(
    x => x.TaskId == taskId && !x.IsDeleted,
    x => { x.IsValid = 1; x.UploadStatus = "active"; x.TaskId = null; },
    nameof(StandardDirectoryFile.IsValid),
    nameof(StandardDirectoryFile.UploadStatus),
    nameof(StandardDirectoryFile.TaskId)
);
```

---

## 五、修复优先级与工作量评估

| 优先级 | 修复项 | 涉及文件 | 消除手写SQL数量 | 预估工作量 | 风险 |
|--------|--------|---------|----------------|-----------|------|
| **P1** | 修复1：补全 `PhaseDefinition` 实体 | `CertPlatform.Shared/Entities/Cert/` | 1处 | 小（10分钟） | 低 |
| **P1** | 修复2：创建视图 + 实体映射 | `scripts/db/views/` + 新实体类 | ~8处（跨表查询） | 中（需设计视图） | 低 |
| **P1** | 修复3：创建中间状态视图 | `scripts/db/views/` + 新实体类 | ~12处（上传流程） | 中（需设计视图） | 低 |
| **P1** | 修复4：`withValidZero` 参数 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + 业务层 | ~8处 | 中（接口变更） | 低 |
| **P2** | 修复5：批量写入接口 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + 业务层 | ~10处 | 中 | 低 |

**建议分两期实施**：

**第一期（短期止血，2-3天）**：
1. 修复1：补全 `PhaseDefinition` 实体（立即生效1处）
2. 修复4：增加 `withValidZero` 参数（消除 ~8处）
3. 修复2：创建规则列表视图（消除 ~3处跨表查询）

**第二期（中期完善，1周）**：
1. 修复3：创建上传状态视图（消除 ~12处中间状态查询）
2. 修复5：批量写入接口（标准化批量操作）
3. 全面回归测试上传流程

---

## 六、关键设计原则总结

### 6.1 视图优先原则

| 场景 | 推荐方案 | 原因 |
|------|---------|------|
| 跨表JOIN查询 | 创建数据库视图 | 视图语义清晰，可被多个服务复用 |
| 复杂聚合统计 | 创建数据库视图 | 数据库引擎优化聚合性能 |
| 中间状态查询 | 创建专用视图或 `withValidZero` 参数 | 视图方案更优雅，参数方案改动小 |
| 单表简单查询 | LINQ + `GetListAsync` | 标准CRUD，无需额外开发 |

### 6.2 禁止在后端出现的内容

- ❌ `SELECT * FROM table_name WHERE ...` 字符串拼接
- ❌ `INSERT INTO table_name VALUES (...)` 字符串
- ❌ `UPDATE table_name SET ...` 字符串（除非是批量更新接口封装内）
- ❌ `DELETE FROM table_name WHERE ...` 字符串（除非是物理删除接口封装内）

### 6.3 允许的例外情况

- ✅ `SqlExecuteAsync` 封装在专门的 Repository/Service 方法内部，对外暴露的是业务方法而非原始SQL
- ✅ 视图定义SQL写在 `scripts/db/views/` 目录下，作为迁移脚本管理
- ✅ LINQ 表达式内部的 `Where`/`Select`/`Join` 等操作

---

## 七、补充说明

### 7.1 为什么不是「迁移遗留」？

项目文档明确说明新架构（`src/certplatform-api/`）与旧架构（`src/old/`）完全隔离，禁止跨架构调用。因此这些手写SQL是**新架构自身的设计缺陷**，而非从旧架构遗留。

### 7.2 `BaseEntity` 架构评估

**优点**：
- `Id/Code/CreateTime/CreateBy/UpdateTime/UpdateBy` 作为强制字段，确保所有表具有统一的审计能力
- 通过 `[SugarColumn(ColumnName = "...")]` 显式映射，类型安全且无歧义
- 底座稳固，不支持随意更改符合"基础资料不可随意变更"的架构原则

**待改进点**：
- 缺少 `withValidZero` 参数，导致中间状态查询必须绕过 ORM
- 缺少跨表 JOIN 封装，复杂查询需手写SQL（应改为视图方案）
- 缺少批量更新/物理删除接口，业务逻辑碎片化

### 7.3 修复后的验收标准

1. `StandardDirectoryService.cs` 和 `DocExtractionRuleService.cs` 中不再出现明显的手写SQL字符串
2. 跨表查询通过数据库视图 + 实体映射实现
3. 所有原手写SQL场景可通过标准CRUD或新增封装方法替代
4. 单元测试覆盖核心上传流程（4步上传+取消+确认）

---

*文档生成于 2026-09-20，由 AI 辅助分析生成。V3 版本新增了视图为核心的修复策略。*
