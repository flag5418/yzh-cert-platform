# 后端手写 SQL → SqlSugar LINQ 迁移方案

> **文档版本**: V1
> **创建时间**: 2026-09-20
> **状态**: 待审批
> **核心目标**: 消灭手写 SQL → Service 层直接注入 SqlSugarClient 使用 LINQ → 统一数据访问层

---

## 一、问题现状

### 1.1 扫描结果

| 风险等级 | 数量 | 分布 |
|---------|------|------|
| **高危（SQL 注入）** | **2 处** | `EntityService.cs:298`、`StandardDirectoryService.cs:798` |
| **中危（ORM 失效）** | **85+ 处** | 16 个文件，参数化正确但绕过 ORM |
| 合计 | **87+ 处** | 覆盖框架层、认证层、业务层 |

### 1.2 高危详情

| ID | 文件 | 行号 | 问题 | 注入向量 |
|----|------|------|------|---------|
| H-1 | `EntityService.cs` | 298 | `string.Join("','", parentCodes)` 拼 IN 子句 | GBK 编码下单引号转义绕过 |
| H-2 | `StandardDirectoryService.cs` | 798 | `$"'{f.FileCode}'"` 拼 IN 子句 | 数据库字段被污染时注入 |

### 1.3 中危分布（按文件）

| 文件 | SQL 数量 | 类型 |
|------|---------|------|
| `ApiRepository.cs` | 18 | CRUD 全部手写 SQL |
| `StandardDirectoryService.cs` | 20+ | 上传任务/文件/文件夹 CRUD |
| `DocExtractionRuleService.cs` | 8 | 提取结果 CRUD + 规则查询 |
| `WfExecutionTaskService.cs` | 7 | 工作流执行任务 CRUD |
| `AIUsageController.cs` | 5 | 分页/统计/趋势查询 |
| `PermissionCacheService.cs` | 3 | 权限缓存重建 |
| `AuthController.cs` | 2 | 登录查用户 + 更新 Token |
| `YZHAnonymousAttribute.cs` | 2 | 鉴权查用户/角色 |
| `RoleService.cs` | 2 | 角色名批量查询 |
| `AiNodeExecutor.cs` | 1 | AI 配置查询 |
| `CertSkillRegistry.cs` | 1 | 技能反射表查询 |
| `MenuController.cs` | 1 | 菜单列表查询 |
| `RoleMenuController.cs` | 1 | 删除角色菜单关联 |
| `RoleController.cs` | 1 | 删除角色用户关联 |
| `RoleApiController.cs` | 1 | 删除角色接口关联 |
| `QueueManager.cs` | 10 | 队列任务/锁 CRUD |

---

## 二、架构决策

### 2.1 决策：Service 层直接注入 SqlSugarClient

**对比方案：**

| 方案 | 优点 | 缺点 | 结论 |
|------|------|------|------|
| A. 扩展 `IDbOrm` 接口 | 保持抽象层 | 破坏 ORM 可替换性；接口膨胀 | ❌ 不采用 |
| **B. Service 注入 SqlSugarClient** | LINQ 原生支持；不破坏 `IDbOrm` 抽象；按需使用 | Service 与 SqlSugar 耦合 | ✅ **采用** |

**理由：**
1. `IDbOrm` 定位是"原子能力层"（`EntityService` 用），不是 LINQ 查询层
2. Service 层需要 JOIN、GroupBy、In 等复杂查询，`IDbOrm` 无法优雅支持
3. SqlSugar 已是项目唯一 ORM，短期内不会替换
4. `SqlSugarClient` 已通过 `IDbContextFactory` 管理生命周期，直接注入 Scoped 实例即可

### 2.2 DI 注册方式

当前 `IDbOrm` 注册（`YzhWebBuilder.cs:67`）：
```csharp
builder.Services.AddScoped<IDbOrm>(sp => {
    var factory = sp.GetRequiredService<IDbContextFactory>();
    return factory.GetDefault();
});
```

新增 `SqlSugarClient` 直接注册：
```csharp
// 新增：Service 层可直接注入 SqlSugarClient 使用 LINQ
builder.Services.AddScoped<SqlSugarClient>(sp => {
    var factory = sp.GetRequiredService<IDbContextFactory>();
    return factory.GetDefault().Client;  // 需暴露 Client 属性
});
```

**或者**（更简单）：Service 层通过 `IDbOrm` 获取内部 `_client`，但需要暴露接口。

**推荐方案**：在 `IDbOrm` 新增一个 `Client` 属性暴露 `SqlSugarClient`，Service 层通过 `_dbOrm.Client` 使用 LINQ。

---

## 三、改造模式

### 模式 A：简单查询

```csharp
// ❌ 原始 SQL
var result = await _dbOrm.QueryFirstOrDefaultAsync<SysApi>(
    "SELECT * FROM sys_api WHERE code = @code", new { code });

// ✅ SqlSugar LINQ（通过 _dbOrm.Client）
var result = await _dbOrm.Client.Queryable<SysApi>()
    .Where(x => x.Code == code)
    .FirstAsync();
```

### 模式 B：IN 子句

```csharp
// ❌ 高危拼接
var inParams = string.Join("','", codes.Select(c => c.Replace("'", "''")));
var sql = $"SELECT * FROM table WHERE code IN ('{inParams}')";

// ✅ SqlSugar LINQ
var list = await _dbOrm.Client.Queryable<SysApi>()
    .In(x => x.Code, codes)
    .ToListAsync();
```

### 模式 C：聚合查询

```csharp
// ❌ GROUP BY 原始 SQL
var sql = $"SELECT ParentCode, COUNT(*) FROM {view} WHERE ... GROUP BY ParentCode";

// ✅ SqlSugar LINQ
var dict = await _dbOrm.Client.Queryable<Sys_Organization>()
    .Where(x => parentCodes.Contains(x.ParentCode))
    .GroupBy(x => x.ParentCode)
    .Select(x => new { x.ParentCode, Cnt = SqlFunc.AggregateCount(x.Code) })
    .ToDictionaryAsync(x => x.ParentCode, x => x.Cnt);
```

### 模式 D：分页+动态条件

```csharp
// ❌ WHERE 拼接
var sql = "SELECT ... WHERE IsDeleted = 0";
if (!string.IsNullOrEmpty(startDate)) sql += " AND CreateTime >= @StartDate";

// ✅ SqlSugar LINQ
var query = _dbOrm.Client.Queryable<CertAiUsageLog>()
    .Where(x => !x.IsDeleted)
    .WhereIF(!string.IsNullOrEmpty(startDate), x => x.CreateTime >= DateTime.Parse(startDate))
    .WhereIF(!string.IsNullOrEmpty(endDate), x => x.CreateTime <= DateTime.Parse(endDate))
    .OrderByDescending(x => x.CreateTime);
var total = await query.CountAsync();
var list = await query.Skip(offset).Take(rows).ToListAsync();
```

### 模式 E：批量操作

```csharp
// ❌ 循环逐条 INSERT
foreach (var api in apis) {
    await _dbOrm.SqlExecuteAsync(sql, api);
}

// ✅ SqlSugar 批量
await _dbOrm.Client.Insertable(apis).ExecuteCommandAsync();
```

### 模式 F：JOIN 查询

```csharp
// ❌ 多表 JOIN 原始 SQL
var sql = @"SELECT ru.UserCode, r.RoleName FROM Sys_RoleUser ru
            INNER JOIN Sys_Role r ON ru.RoleCode = r.Code WHERE ...";

// ✅ SqlSugar LINQ
var list = await _dbOrm.Client.Queryable<Sys_RoleUser, Sys_Role>(
        (ru, r) => ru.RoleCode == r.Code)
    .Where(ru => userCodes.Contains(ru.UserCode))
    .Select((ru, r) => new { ru.UserCode, r.RoleName })
    .ToListAsync();
```

### 模式 G：INSERT IGNORE / ON DUPLICATE KEY

```csharp
// ✅ SqlSugar Storageable（自动处理重复键）
await _dbOrm.Client.Storageable(entities).ExecuteCommandAsync();
```

---

## 四、IDbOrm 扩展方案

在 `IDbOrm` 接口新增 `Client` 属性：

```csharp
public interface IDbOrm
{
    // 现有方法保持不变...
    
    /// <summary>
    ///     暴露底层 SqlSugarClient，供 Service 层使用 LINQ
    ///     仅限需要 JOIN/GroupBy/In 等复杂查询的场景
    ///     简单 CRUD 仍建议通过 EntityService 或 GetOneAsync/GetListAsync
    /// </summary>
    SqlSugarClient Client { get; }
}
```

`SqlSugarDbOrm` 实现：
```csharp
public SqlSugarClient Client => _client;
```

---

## 五、无需改造的场景

| 场景 | 原因 |
|------|------|
| `QueueManager` 的 `FOR UPDATE SKIP LOCKED` | MySQL 特有锁语法，SqlSugar LINQ 不支持 |
| `PermissionCacheService` 的 `INSERT ... SELECT ... ON DUPLICATE KEY` | 批量同步 SQL，性能关键且逻辑清晰 |
| `scripts/db/*.sql` 数据库迁移脚本 | 非运行时代码 |
| `EntityService` 基类的原子方法 | 已通过 `IDbOrm` LINQ 封装 |

这些场景应加注释 `// ORM 不支持此语法，保留原始 SQL` 说明原因。

---

## 六、改造文件清单

### 阶段 P0：修复注入漏洞（0.5 天）

| 文件 | 改动 |
|------|------|
| `EntityService.cs:298` | IN 子句改为参数化 `@c0,@c1,...` 或 SqlSugar `.In()` |
| `StandardDirectoryService.cs:798` | IN 子句改为参数化 |

### 阶段 P1：框架层改造（2 天）

| 文件 | 改动 |
|------|------|
| `IDbOrm.cs` | 新增 `SqlSugarClient Client { get; }` |
| `SqlSugarDbOrm.cs` | 实现 `Client` 属性 |
| `EntityService.cs` | `GetChildrenCountBatch` 改 LINQ |
| `ApiRepository.cs` | 18 处全部改 ORM |

### 阶段 P2：认证权限层（1 天）

| 文件 | 改动 |
|------|------|
| `AuthController.cs` | 登录查用户 + 更新 Token 改 ORM |
| `YZHAnonymousAttribute.cs` | 鉴权查用户/角色改 ORM |
| `RoleService.cs` | 角色查询改 ORM |
| `PermissionCacheService.cs` | 保留（批量同步 SQL 性能关键） |
| `MenuController.cs` | 菜单列表改 ORM |
| `RoleMenuController.cs` | 删除关联改 ORM |
| `RoleController.cs` | 删除关联改 ORM |
| `RoleApiController.cs` | 删除关联改 ORM |

### 阶段 P3：业务层实体补建（3 天）

需为以下无实体的表创建实体类：

| 表名 | 实体类建议 | 对应 SQL 文件 |
|------|-----------|--------------|
| `cert_upload_task` | `CertUploadTask` | `StandardDirectoryService.cs` |
| `cert_standard_directory_file` | `CertStandardDirectoryFile` | `StandardDirectoryService.cs` |
| `cert_standard_directory_folder` | `CertStandardDirectoryFolder` | `StandardDirectoryService.cs` |
| `ent_extraction_result` | `EntExtractionResult` | `DocExtractionRuleService.cs` |
| `ent_table_extraction_result` | `EntTableExtractionResult` | `DocExtractionRuleService.cs` |
| `wf_execution_task` | `WfExecutionTask` | `WfExecutionTaskService.cs` |
| `wf_execution_task_item` | `WfExecutionTaskItem` | `WfExecutionTaskService.cs` |
| `wf_node_execution` | `WfNodeExecution` | `WfExecutionTaskService.cs` |
| `cert_ai_usage_log` | `CertAiUsageLog` | `AIUsageController.cs` |
| `cert_ai_config` | `CertAiConfig` | `AIUsageController.cs` |
| `cert_sys_config` | `CertSysConfig` | `AiNodeExecutor.cs` |
| `wf_skill_reflection` | `WfSkillReflection` | `CertSkillRegistry.cs` |
| `yzh_queue_task` | `YzhQueueTask` | `QueueManager.cs` |
| `yzh_queue_resource_lock` | `YzhQueueResourceLock` | `QueueManager.cs` |

### 阶段 P4：业务 Service 改造（5-8 天）

| 文件 | SQL 数量 | 改造内容 |
|------|---------|---------|
| `StandardDirectoryService.cs` | 20+ | 实体 CRUD 全改 ORM |
| `DocExtractionRuleService.cs` | 8 | 实体 CRUD + 规则查询改 ORM |
| `DocExtractionRuleService.AI.cs` | 4 | AI 配置/模板查询改 ORM |
| `WfExecutionTaskService.cs` | 7 | 执行任务 CRUD 改 ORM |
| `AiNodeExecutor.cs` | 1 | AI 配置查询改 ORM |
| `CertSkillRegistry.cs` | 1 | 技能查询改 ORM |
| `AIUsageController.cs` | 5 | 分页/统计改 ORM |
| `QueueManager.cs` | 10 | 保留（`FOR UPDATE SKIP LOCKED`） |

---

## 七、验收标准

| 阶段 | 验收标准 |
|------|---------|
| P0 | `grep -rn "string.Join.*'" src/yzh-core/ src/certplatform-api/` 无字符串拼接 IN 子句 |
| P1 | `grep -rn "SqlExecuteAsync\|SqlQueryAsync\|QueryFirstOrDefaultAsync" src/yzh-core/YZH.Core.Api/Repositories/` 为 0 |
| P2 | `grep -rn "SELECT \* FROM\|SELECT .* FROM Sys_" src/yzh-core/YZH.Core.Web/` 为 0 |
| P3 | 14 个新实体类创建完成，`[SugarTable]` 注解正确 |
| P4 | `grep -rn "SqlExecuteAsync\|SqlQueryAsync" src/certplatform-api/` 仅剩 QueueManager + PermissionCacheService |
