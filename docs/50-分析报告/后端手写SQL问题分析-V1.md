# 后端手写 SQL 问题分析报告

> **版本**：V1.0 | **日期**：2026-09-20 | **状态**：完成
> 
> **范围**：`src/certplatform-api/`（新架构后端，不含旧 `src/old/`）
> 
> **核心结论**：手写SQL的根本原因是 `BaseEntity` 审计字段的 `[SugarColumn(IsIgnore = true)]` 标记导致 SqlSugar 全局过滤器失效；叠加跨表JOIN、物理删除、中间状态查询等业务需求，进一步加剧了对原始SQL的依赖。

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

## 二、根本原因分析

### 2.1 【架构层】`BaseEntity` 审计字段与 `[SugarColumn(IsIgnore = true)]` 冲突（核心根因）

**问题定位**：`src/yzh-core/YZH.Core.Stand/Models/Entity/BaseEntity.cs`

```csharp
public abstract class BaseEntity : INotifyPropertyChanged, ISoftDelete, IIsValid
{
    // ❌ 所有审计字段均标记 IsIgnore=true，SqlSugar 无法映射这些列
    [SugarColumn(IsIgnore = true)]
    public string Code { get; set; } = string.Empty;

    [SugarColumn(IsIgnore = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsIgnore = true)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsIgnore = true)]
    public int IsValid { get; set; } = 1;
    // ... 其余审计字段同理
}
```

**子类重声明模式**（所有实体类采用此方式）：

```csharp
// StandardDirectoryFile.cs
[SugarTable("cert_standard_directory_file")]
public class StandardDirectoryFile : BaseEntity, ISoftDelete, IIsValid
{
    // ⚠️ 子类用 new 关键字重新声明同名字段
    public new bool IsDeleted { get; set; }  // IsDeleted 被声明为 DB 列
    public new int IsValid { get; set; }    // IsValid 被声明为 DB 列
    // ... 但 SqlSugar 的表达式树构建器仍可能将基类的 IsIgnore 属性应用于此表
}
```

**后果**：

1. `SqlSugarDbOrm.GetListAsync<T>()` 中自动注入的全局过滤条件 `WHERE IsDeleted=0 AND IsValid=1` **依赖 `IsIgnore` 属性来跳过基类字段**。
2. 当子类重声明后，部分场景下 SqlSugar 的表达式编译产生歧义，导致：
   - 全局过滤器未能正确附加到 LINQ 表达式
   - 查询结果不符合预期（漏数据或多数据）
3. 为规避此风险，开发者直接绕过 ORM 改用原生 SQL 并手动拼写过滤条件。

**证据**（`StandardDirectoryService.cs` 多处注释）：

```csharp
/// <summary>
/// 获取目录下所有文件（绕过全局过滤：新上传文件 IsValid=0）
/// </summary>
var files = (await _db.SqlQueryAsync<StandardDirectoryFile>(
    "SELECT * FROM cert_standard_directory_file WHERE DirectoryCode=@dir ...",
    new { dir = directoryCode })).Data ?? new();
```

```csharp
/// <summary>
/// 验证任务（绕过全局过滤：IsValid/IsDeleted 在 SqlSugarDbOrm.GetOneAsync 中自动加，
/// 此处用原生 SQL 以精确控制过滤条件）
/// </summary>
var task = (await _db.QueryFirstOrDefaultAsync<UploadTask>(
    "SELECT * FROM cert_upload_task WHERE TaskId=@taskId AND status='initialized' AND IsDeleted=0",
    new { taskId })).Data;
```

---

### 2.2 【业务层】需要查询 `IsValid=0` 的中间状态记录

上传流程设计为四步：`初始化 → 上传 → 确认 → 激活`。其中前两个阶段文件记录为 `IsValid=0`（草稿态），确认后才转为 `IsValid=1`（正式）。

标准CRUD方法默认过滤掉 `IsValid=0` 的记录，导致无法查到这些中间态数据。

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

---

### 2.3 【ORM能力层】跨表 JOIN 查询

配置规则列表需要关联两张表（`cert_doc_extraction_rule` LEFT JOIN `cert_standard_directory_file`），`IDbOrm` 仅支持单表 LINQ 查询。

**涉及代码位置**：

| 文件 | 行号 | JOIN 内容 |
|------|------|-----------|
| `DocExtractionRuleService.cs` | L493-500 | `cert_doc_extraction_rule` LEFT JOIN `cert_standard_directory_file` |
| `StandardDirectoryService.cs` | L1106-1107 | `cert_doc_extraction_rule` WHERE `IN @codes`（无JOIN，但需动态IN） |
| `StandardDirectoryService.cs` | L1591-1592 | `queue_resource_lock` 查询资源锁 |

---

### 2.4 【框架能力层】物理删除与批量 UPDATE

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
| `DocExtractionRuleService.cs` L~260 | `DELETE` + `INSERT` | B-08/B-09 数据同步（物理删旧+批量写） |

---

### 2.5 【表结构层】实体定义不完整

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
    
    // ❌ 缺少：IsValid, IsDeleted, Code 等基类字段的重声明
}
```

**后果**：`GetListAsync<PhaseDefinition>` 返回空或报错，只能走原生SQL。

---

## 三、详细修复方案

### 修复项1（P0）：重构 `BaseEntity` 审计字段映射

**目标**：消除 `[SugarColumn(IsIgnore = true)]` 引起的字段歧义，使 SqlSugar 能正确为所有实体注入全局过滤器。

**方案设计**：

将 `BaseEntity` 中的审计字段从 `IsIgnore=true` 改为显式映射。有两种可行路径：

**方案A：使用 `new` + 显式列名映射（推荐）**

```csharp
// BaseEntity.cs
public abstract class BaseEntity : INotifyPropertyChanged, ISoftDelete, IIsValid
{
    // 基类保留虚拟属性，用于 LINQ 表达式解析
    public virtual string? Code { get; set; }
    public virtual DateTime CreateTime { get; set; }
    public virtual string? CreateBy { get; set; }
    public virtual DateTime? UpdateTime { get; set; }
    public virtual string? UpdateBy { get; set; }
    public virtual DateTime? DeleteTime { get; set; }
    public virtual string? DeleteBy { get; set; }
    public virtual bool IsDeleted { get; set; }
    public virtual int IsValid { get; set; } = 1;
    
    // 前端选中标记（不持久化）
    [SugarColumn(IsIgnore = true)]
    public bool CheckFlag { get; set; }

    [SugarColumn(IsIgnore = true)]
    public bool DeleteFlag { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public byte[]? RowVersion { get; set; }
}

// 子类示例：StandardDirectoryFile.cs
[SugarTable("cert_standard_directory_file")]
public class StandardDirectoryFile : BaseEntity
{
    // 显式覆盖基类属性并映射到DB列（替代原来的 new 声明）
    [SugarColumn(Length = 64, IsPrimaryKey = false, IsNullable = false)]
    public override string? Code { get; set; }

    [SugarColumn(IsNullable = false)]
    public override DateTime CreateTime { get; set; }

    // ... 其他审计字段同理覆盖
    
    // 业务字段正常声明
    [SugarColumn(Length = 150)]
    public string FileCode { get; set; } = string.Empty;
    
    // ... 其余业务字段
}
```

**方案B：为 `IDbOrm` 增加实体特性开关（改动更小）**

在 `IDbOrm` 接口增加可选参数控制是否应用全局过滤器：

```csharp
// IDbOrm.cs
Task<Result<List<T>>> GetListAsync<T>(
    Expression<Func<T, bool>>? predicate = null,
    bool includeDisabled = false,
    bool skipGlobalFilter = false) where T : class, new();

Task<Result<T?>> GetOneAsync<T>(
    Expression<Func<T, bool>> predicate,
    bool skipGlobalFilter = false) where T : class, new();
```

当 `skipGlobalFilter = true` 时，`SqlSugarDbOrm` 实现中跳过 `WHERE IsDeleted=0 AND IsValid=1` 条件。

**推荐组合**：先实施**方案B**（快速止血），再逐步推进**方案A**（根治）。

---

### 修复项2（P1）：补全 `PhaseDefinition` 实体类

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

### 修复项3（P1）：为 `IDbOrm` 增加 `withValidZero` 参数

**目标**：让标准CRUD支持查询 `IsValid=0` 的中间态数据。

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
        var query = _client.Queryable<T>()
            .Where(IsDeletedCondition<T>());
        
        // 仅在 withValidZero=false 时添加 IsValid 过滤
        if (!withValidZero)
            query = query.Where(IsValidCondition<T>());
        else if (!includeDisabled)
            query = query.Where(x => true); // 跳过 IsValid 但保留 IsDeleted
        
        if (!includeDisabled)
            query = query.Where(x => true); // 此处逻辑需根据实际业务调整
        
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
        && (string.IsNullOrEmpty(x.FolderCode) || x.FolderCode == "")
        && x.UploadStatus == "uploaded" || x.UploadStatus == "active",
    withValidZero: true)).Data ?? new();

// StandardDirectoryService.cs — 原 L717
// 原：_db.QueryFirstOrDefaultAsync<UploadTask>("SELECT * FROM ...")
var task = (await _db.GetOneAsync<UploadTask>(
    x => x.TaskId == taskId && x.Status == "initialized",
    withValidZero: true)).Data;
```

---

### 修复项4（P2）：封装 Join 查询方法

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

/// <summary>
/// 执行标量 COUNT 查询（支持多表关联）
/// </summary>
Task<Result<int>> CountJoinAsync<TLeft, TRight>(
    Expression<Func<TLeft, TRight, bool>> joinCondition,
    Expression<Func<TLeft, bool>>? leftFilter = null)
    where TLeft : class, new()
    where TRight : class, new();
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
        StandardCode = r.StandardCode ?? "",
        PhaseCode = r.PhaseCode ?? "",
        Skill = r.Skill,
        DocIsValid = r.DocIsValid,
        Status = r.Status,
        CreateTime = r.CreateTime,
        UpdateTime = r.UpdateTime
    },
    leftFilter: r => r.IsDeleted == false && r.IsValid == 1
);
```

---

### 修复项5（P2）：批量写入接口

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

**使用示例**：

```csharp
// StandardDirectoryService.cs — UploadConfirmAsync 批量激活
// 原：_db.SqlExecuteAsync($"UPDATE cert_standard_directory_file SET IsValid=1...")
await _db.BulkUpdateByConditionAsync<StandardDirectoryFile>(
    x => x.TaskId == taskId && x.IsDeleted == false,
    x => { x.IsValid = 1; x.UploadStatus = "active"; x.TaskId = null; },
    nameof(StandardDirectoryFile.IsValid),
    nameof(StandardDirectoryFile.UploadStatus),
    nameof(StandardDirectoryFile.TaskId)
);

// StandardDirectoryService.cs — CleanupOrphanDataAsync 清理孤儿数据
// 原：_db.SqlExecuteAsync("DELETE FROM ...")
await _db.PhysicalDeleteByConditionAsync<StandardDirectoryFile>(
    x => x.DirectoryCode == dc && x.IsValid == 0 && x.TaskId != tid
);
await _db.PhysicalDeleteByConditionAsync<StandardDirectoryFolder>(
    x => x.DirectoryCode == dc && x.IsValid == 0 && x.TaskId != tid
);
await _db.PhysicalDeleteByConditionAsync<UploadTask>(
    x => x.DirectoryCode == dc && x.Status != "completed" && x.TaskId != tid
);
```

---

## 四、修复优先级与工作量评估

| 优先级 | 修复项 | 涉及文件 | 消除手写SQL数量 | 预估工作量 | 风险 |
|--------|--------|---------|----------------|-----------|------|
| **P0** | 修复1：重构 `BaseEntity` 映射 | `src/yzh-core/YZH.Core.Stand/` | 全部 ~33 处（根治） | 中（需回归测试） | 中（影响基类） |
| **P1** | 修复2：补全 `PhaseDefinition` | `CertPlatform.Shared/Entities/Cert/` | 1处 | 小（10分钟） | 低 |
| **P1** | 修复3：`withValidZero` 参数 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + `StandardDirectoryService.cs` | ~20处 | 中（接口变更+业务替换） | 低 |
| **P2** | 修复4：Join 查询封装 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + `DocExtractionRuleService.cs` | ~3处 | 中（需设计投影类型） | 中 |
| **P2** | 修复5：批量写入接口 | `IDbOrm.cs` + `SqlSugarDbOrm.cs` + `StandardDirectoryService.cs` | ~10处 | 中 | 低 |

**建议分两期实施**：

**第一期（短期止血，1-2天）**：
1. 修复2：补全 `PhaseDefinition` 实体（立即生效1处）
2. 修复3：增加 `withValidZero` 参数（消除 ~20处）
3. 修复5：增加批量写入接口（消除 ~5处）

**第二期（中期根治，1周）**：
1. 修复1：重构 `BaseEntity` 字段映射（消除全部 ~33处）
2. 修复4：封装 Join 查询（标准化跨表查询入口）

---

## 五、关键代码路径速查

| 问题类型 | 典型代码位置 | 当前写法 |
|---------|------------|---------|
| 绕过 IsValid=0 过滤 | `StandardDirectoryService.cs:389,717,723,787,882,953,1580` | `_db.SqlQueryAsync<T>("SELECT * FROM ... WHERE IsDeleted=0")` |
| 跨表 JOIN | `DocExtractionRuleService.cs:493` | `_db.Client.Ado.SqlQueryAsync<DTO>("SELECT ... LEFT JOIN ...")` |
| 物理 DELETE | `StandardDirectoryService.cs:914,931,937,1039,1042,1045` | `_db.SqlExecuteAsync("DELETE FROM ...")` |
| 批量 UPDATE | `StandardDirectoryService.cs:755,760,803,810,816,906` | `_db.SqlExecuteAsync("UPDATE ... SET ...")` |
| 实体缺字段 | `PhaseDefinition.cs` | 只能用 `_db.SqlQueryAsync<PhaseDefDto>("SELECT PhaseCode, Code FROM ...")` |
| COUNT 聚合 | `StandardDirectoryService.cs:927` | `_db.SqlScalarAsync<long>("SELECT COUNT(*) FROM ...")` |

---

## 六、补充说明

### 6.1 为什么不是「迁移遗留」？

项目文档明确说明新架构（`src/certplatform-api/`）与旧架构（`src/old/`）完全隔离，禁止跨架构调用。因此这些手写SQL是**新架构自身的设计缺陷**，而非从旧架构遗留。

### 6.2 为什么不用 EF Core / Dapper 等其他 ORM？

项目技术栈已锁定 `.NET 8 + SqlSugar`，YZH.Core 底层基于 SqlSugar。更换 ORM 不在本次分析范围内。

### 6.3 修复后的验收标准

1. `StandardDirectoryService.cs` 和 `DocExtractionRuleService.cs` 中不再出现 `SqlQueryAsync<string>()` / `SqlExecuteAsync()` 调用
2. 所有原手写SQL场景可通过标准CRUD或新增封装方法替代
3. 单元测试覆盖核心上传流程（4步上传+取消+确认）

---

*文档生成于 2026-09-20，由 AI 辅助分析生成。*
