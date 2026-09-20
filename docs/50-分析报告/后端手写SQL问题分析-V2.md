# 后端手写 SQL 问题分析报告（修正版）

> **版本**：V2.0 | **日期**：2026-09-20 | **状态**：已修正
>
> **修正说明**：V1 版本错误地将 `BaseEntity` 的强制字段描述为 `[SugarColumn(IsIgnore = true)]`，导致分析结论偏差。实际架构中 `Id/Code/CreateTime/CreateBy/UpdateTime/UpdateBy` 均为**显式映射的基础字段**，设计正确。
>
> **范围**：`src/certplatform-api/`（新架构后端，不含旧 `src/old/`）

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

## 二、`BaseEntity` 架构正确性确认

### 2.1 强制字段设计（正确）

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
    // IsDeleted/IsValid 由接口契约提供，底层 ORM 自动处理全局过滤
    public bool IsDeleted { get; set; }
    public int IsValid { get; set; } = 1;
    
    // ... 业务字段
}
```

---

## 三、手写SQL真正原因分析

### 3.1 【业务层】需要查询 `IsValid=0` 的中间状态记录

上传流程设计为四步：`初始化 → 上传 → 确认 → 激活`。其中前两个阶段文件记录为 `IsValid=0`（草稿态），确认后才转为 `IsValid=1`（正式）。

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

**代码证据**：

```csharp
// StandardDirectoryService.cs L717-720
/// <summary>验证任务（绕过全局过滤：IsValid/IsDeleted 在 SqlSugarDbOrm.GetOneAsync 中自动加，
/// 此处用原生 SQL 以精确控制过滤条件）</summary>
var task = (await _db.QueryFirstOrDefaultAsync<UploadTask>(
    "SELECT * FROM cert_upload_task WHERE TaskId=@taskId AND status='initialized' AND IsDeleted=0",
    new { taskId })).Data;
```

---

### 3.2 【ORM能力层】跨表 JOIN 查询

配置规则列表需要关联两张表（`cert_doc_extraction_rule` LEFT JOIN `cert_standard_directory_file`），`IDbOrm` 仅支持单表 LINQ 查询。

**涉及代码位置**：

| 文件 | 行号 | JOIN 内容 |
|------|------|-----------|
| `DocExtractionRuleService.cs` | L493-500 | `cert_doc_extraction_rule` LEFT JOIN `cert_standard_directory_file` |
| `StandardDirectoryService.cs` | L1106-1107 | `cert_doc_extraction_rule` WHERE `IN @codes`（无JOIN，但需动态IN） |
| `StandardDirectoryService.cs` | L1591-1592 | `queue_resource_lock` 查询资源锁 |

---

### 3.3 【框架能力层】物理删除与批量 UPDATE

标准CRUD只支持软删除（设置 `IsDeleted=true`），不支持物理删除；`UpdateAsync` 是单条更新，批量更新需要原始SQL。

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
| `DocExtractionRuleService.cs` | L~260 | `DELETE` + `INSERT` | B-08/B-09 数据同步（物理删旧+批量写） |

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

**后果**：`GetListAsync<PhaseDefinition>` 返回空或报错，只能走原生SQL。

---

## 四、修复方案

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

### 修复项2（P1）：为 `IDbOrm` 增加 `withValidZero` 参数

**目标**：让标准CRUD支持查询 `IsValid=0` 的中间态数据，替代绕过 ORM 的手写SQL。

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

**使用示例**（替代多个手写SQL）：

```csharp
// StandardDirectoryService.cs — 原 L389-395
// 原：_db.SqlQueryAsync<StandardDirectoryFile>("SELECT * FROM ...")
var files = (await _db.GetListAsync<StandardDirectoryFile>(
    x => x.DirectoryCode == directoryCode 
        && x.UploadStatus == "uploaded" || x.UploadStatus == "active",
    withValidZero: true)).Data ?? new();

// StandardDirectoryService.cs — 原 L717
// 原：_db.QueryFirstOrDefaultAsync<UploadTask>("SELECT * FROM ...")
var task = (await _db.GetOneAsync<UploadTask>(
    x => x.TaskId == taskId && x.Status == "initialized",
    withValidZero: true)).Data;
```

---

### 修复项3（P2）：封装 Join 查询方法

**目标**：统一跨表查询入口，替代散落在业务层的原生SQL。

```csharp
// IDbOrm.cs
/// <summary>
/// 左连接查询（支持跨表聚合，返回指定投影类型）
/// </summary>
Task<Result<List<TResult>>> QueryJoinAsync<TLeft, TRight, TResult>(
    Expression<Func<TLeft, TRight, bool>> joinCondition,
    Expression<Func<TLeft, TRight, TResult>> selector,
    Expression<Func<TLeft, bool>>? leftFilter = null,
    Expression<Func<TRight, bool>>? rightFilter = null) 
    where TLeft : class, new()
    where TRight : class, new()
    where TResult : class, new();
```

**使用示例**（替代 `DocExtractionRuleService.cs` L493-500）：

```csharp
// 原：_db.Client.Ado.SqlQueryAsync<ConfiguredRuleRow>(...)
var result = await _db.QueryJoinAsync<
    DocExtractionRule, StandardDirectoryFile, ConfiguredRuleRow>(
    (r, f) => r.StandardFileCode == f.FileCode,
    (r, f) => new ConfiguredRuleRow
    {
        RuleCode = r.Code,
        StandardFileCode = r.StandardFileCode,
        FileName = f.FileName ?? r.StandardFileCode ?? "",
        // ...
    },
    leftFilter: r => r.IsDeleted == false && r.IsValid == 1
);
```

---

### 修复项4（P2）：批量写入接口

**目标**：替代零散的 `SqlExecuteAsync` 批量UPDATE/DELETE。

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

---

## 五、修复优先级与工作量评估

| 优先级 | 修复项 | 涉及文件 | 消除手写SQL数量 | 预估工作量 | 风险 |
|--------|--------|---------|----------------|-----------|------|
| **P1** | 修复1：补全 `PhaseDefinition` 实体 | `CertPlatform.Shared/Entities/Cert/` | 1处 | 小（10分钟） | 低 |
| **P1** | 修复2：`withValidZero` 参数 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + `StandardDirectoryService.cs` | ~20处 | 中（接口变更+业务替换） | 低 |
| **P2** | 修复3：Join 查询封装 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + `DocExtractionRuleService.cs` | ~3处 | 中（需设计投影类型） | 中 |
| **P2** | 修复4：批量写入接口 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + `StandardDirectoryService.cs` | ~10处 | 中 | 低 |

**建议分两期实施**：

**第一期（短期止血，1-2天）**：
1. 修复1：补全 `PhaseDefinition` 实体（立即生效1处）
2. 修复2：增加 `withValidZero` 参数（消除 ~20处）
3. 修复4：增加批量写入接口（消除 ~5处）

**第二期（中期完善，1周）**：
1. 修复3：封装 Join 查询（标准化跨表查询入口）
2. 继续推进修复4剩余部分

---

## 六、关键代码路径速查

| 问题类型 | 典型代码位置 | 当前写法 |
|---------|------------|---------|
| 绕过 IsValid=0 过滤 | `StandardDirectoryService.cs:389,717,723,787,882,953,1580` | `_db.SqlQueryAsync<T>("SELECT * FROM ... WHERE IsDeleted=0")` |
| 跨表 JOIN | `DocExtractionRuleService.cs:493` | `_db.Client.Ado.SqlQueryAsync<DTO>("SELECT ... LEFT JOIN ...")` |
| 物理 DELETE | `StandardDirectoryService.cs:914,931,937,1039,1042,1045` | `_db.SqlExecuteAsync("DELETE FROM ...")` |
| 批量 UPDATE | `StandardDirectoryService.cs:755,760,803,810,816,906` | `_db.SqlExecuteAsync("UPDATE ... SET ...")` |
| 实体缺字段 | `PhaseDefinition.cs` | 只能用 `_db.SqlQueryAsync<PhaseDefDto>("SELECT PhaseCode, Code FROM ...")` |
| COUNT 聚合 | `StandardDirectoryService.cs:927` | `_db.SqlScalarAsync<long>("SELECT COUNT(*) FROM ...")` |

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
- 缺少跨表 JOIN 封装，复杂查询需手写SQL
- 缺少批量更新/物理删除接口，业务逻辑碎片化

### 7.3 修复后的验收标准

1. `StandardDirectoryService.cs` 和 `DocExtractionRuleService.cs` 中不再出现 `SqlQueryAsync<string>()` / `SqlExecuteAsync()` 调用
2. 所有原手写SQL场景可通过标准CRUD或新增封装方法替代
3. 单元测试覆盖核心上传流程（4步上传+取消+确认）

---

*文档生成于 2026-09-20，由 AI 辅助分析生成。V2 修正了 V1 中关于 BaseEntity 的错误描述。*
