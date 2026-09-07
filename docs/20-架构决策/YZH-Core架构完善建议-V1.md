# YZH-Core 架构完善建议

> **版本**：V1 | **日期**：2026-09-07 | **状态**：草案
>
> **分析对象**：
> - Ape.Volo (MaiTianWai/sdyy-api) — SqlSugar + .NET 8
> - Vol 框架 (vol.api) — EF Core + Vue.NetCore
> - YZH-Core (yzh-core) — 新架构（Dapper + EF Core 混合）
>
> **目标**：提取可借鉴的设计模式，完善 YZH-Core 新架构

---

## 一、核心发现

### 1.1 Ape.Volo 亮点

| 亮点 | 描述 | 推荐度 |
|------|------|--------|
| **泛型主键** | `RootKey<TKey>` 支持 long/string/Guid，灵活 | ⭐⭐⭐⭐⭐ |
| **接口组合** | `ICreateByEntity` + `ISoftDeletedEntity`，按需组合 | ⭐⭐⭐⭐⭐ |
| **审计过滤器** | `AuditingFilter` 记录 IP/浏览器/耗时/入参出参 | ⭐⭐⭐⭐⭐ |
| **全局异常** | `GlobalExceptionFilter` 区分异常类型返回不同状态码 | ⭐⭐⭐⭐ |
| **数据播种** | `DataSeederMiddleware` 启动时自动初始化基础数据 | ⭐⭐⭐⭐ |
| **响应模型** | `ActionResultVm` 统一格式（Status/Message/ActionError/Timestamp/Path） | ⭐⭐⭐⭐ |
| **Controller 分层** | `BaseController`(通用) → `BaseApiController`(JWT) | ⭐⭐⭐⭐ |
| **AOP 事务** | `[UseTran]` 标记即自动事务，消除样板代码 | ⭐⭐⭐⭐ |
| **AOP 缓存** | `[UseCache]` 标记即自动 Redis 缓存 | ⭐⭐⭐⭐ |
| **SqlSugar 全局过滤** | 自动过滤 `IsDeleted`/`TenantId`/`CreateBy` | ⭐⭐⭐⭐ |

### 1.2 Vol 框架亮点

| 亮点 | 描述 | 推荐度 |
|------|------|--------|
| **PageDataOptions** | 强大的分页 + 过滤 + 排序统一参数 | ⭐⭐⭐⭐ |
| **多表编辑** | `SaveModel` 主从表一次提交 | ⭐⭐⭐ |
| **ViewName 路由** | Entity 视图/表自动路由 | ⭐⭐⭐ |
| **CacheContext** | 手动缓存管理器，灵活控制 | ⭐⭐⭐ |
| **WorkFlow 集成** | 工作流引擎与业务深度集成 | ⭐⭐⭐ |

### 1.3 YZH-Core 现状

| 维度 | 当前状态 | 评价 |
|------|---------|------|
| **实体基类** | `BaseEntity`（固定 string Id） | ⚠️ 需改进 |
| **审计日志** | `IAuditLogger`（基础版） | ✅ 已改进 |
| **全局异常** | `GlobalExceptionMiddleware` | ⚠️ 需增强 |
| **响应模型** | `ApiResponse<T>` | ⚠️ 需增强 |
| **Controller 基类** | `YzhControllerBase<T>` | ✅ 基本合理 |
| **Repository** | EF Core + Dapper 双路 | ❌ 需统一 |
| **事务管理** | 手动 `ExecuteInTransaction` | ⚠️ 需改进 |
| **缓存** | `CacheManager` | ✅ 基本合理 |

---

## 二、可借鉴的设计模式

### 2.1 泛型主键基类（推荐立即实施）

**Ape.Volo 做法**：
```csharp
public class RootKey<TKey> where TKey : IEquatable<TKey>
{
    [SugarColumn(IsPrimaryKey = true)]
    public TKey Id { get; set; }
}

public class BaseEntity : RootKey<long>, ICreateByEntity, ISoftDeletedEntity
{
    public string CreateBy { get; set; }
    public DateTime CreateTime { get; set; }
    // ...
}
```

**YZH-Core 改进建议**：
```csharp
// 泛型主键基类
public abstract class RootKeyBase<TEntity, TKey> : BaseEntity
    where TEntity : RootKeyBase<TEntity, TKey>, new()
    where TKey : IEquatable<TKey>
{
    // 主键由子类定义，支持 long/string/Guid
}

// 或者更简单的做法：保留 string Id，但增加泛型支持
public abstract class BaseEntity<TKey> : BaseEntity
    where TKey : IEquatable<TKey>
{
    public new TKey Id { get; set; }
}

// 使用示例
public class SysUser : BaseEntity<long> { }
public class ISOStandard : BaseEntity<string> { }
```

**收益**：
- 灵活支持不同类型主键
- 未来可按需切换（long/Guid/string）
- 与 Ape.Volo 保持一致性

---

### 2.2 接口组合能力（推荐实施）

**Ape.Volo 做法**：
```csharp
public interface ICreateByEntity
{
    string CreateBy { get; set; }
    DateTime CreateTime { get; set; }
}

public interface ISoftDeletedEntity
{
    bool IsDeleted { get; set; }
}

public interface ITenantEntity
{
    int TenantId { get; set; }
}

// 实体按需组合
public class SysUser : BaseEntity, ICreateByEntity, ISoftDeletedEntity, ITenantEntity
{
    // 实现接口属性
}

public class AuditLog : BaseEntity, ICreateByEntity, ISoftDeletedEntity
{
    // 不需要租户隔离
}
```

**YZH-Core 改进建议**：
```csharp
// 定义接口
public interface I auditable
{
    string CreateBy { get; set; }
    DateTime CreateTime { get; set; }
    string? UpdateBy { get; set; }
    DateTime? UpdateTime { get; set; }
}

public interface I SoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeleteTime { get; set; }
    string? DeleteBy { get; set; }
}

public interface I Tenantable
{
    string OrgCode { get; set; }
}

// 实体按需组合
public class SysUser : BaseEntity, I auditable, I SoftDeletable
{
    // 属性由 BaseEntity 提供，接口标记语义
}

public class AuditLog : BaseEntity, I auditable, I SoftDeletable
{
    // 不需要租户隔离
}
```

**收益**：
- 清晰表达实体能力
- 便于框架识别和处理
- 支持未来多租户扩展

---

### 2.3 结构化错误返回（推荐实施）

**Ape.Volo 做法**：
```csharp
public class ActionError
{
    public Dictionary<string, string> Errors { get; set; } = new();
    
    public string GetFirstError() => Errors?.FirstOrDefault().Value;
}

public class ActionResultVm
{
    public int Status { get; set; } = 200;
    public ActionError ActionError { get; set; }
    public string Message { get; set; }
    public string Timestamp { get; set; }
    public string Path { get; set; }
}

// 字段级错误
public ContentResult Error(ActionError actionError)
{
    var vm = new ActionResultVm
    {
        Status = 400,
        ActionError = actionError,
        Message = actionError.GetFirstError()
    };
    return JsonContent(vm);
}
```

**YZH-Core 改进建议**：
```csharp
// 增强 ApiResponse
public class ApiResponse<T>
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("data")] public T? Data { get; init; }
    [JsonPropertyName("error")] public ActionError? Error { get; init; }
    [JsonPropertyName("code")] public int Code { get; init; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
    // 字段级错误
    public static ApiResponse<T> FieldError(string field, string error) =>
        new() { Success = false, Code = 400, Error = ActionError.Create(field, error) };
    
    public static ApiResponse<T> ValidationFailed(ActionError error) =>
        new() { Success = false, Code = 422, Error = error, Message = "校验失败" };
}

public class ActionError
{
    public Dictionary<string, string> FieldErrors { get; init; } = new();
    public List<string> Messages { get; init; } = new();
    
    public static ActionError Create(string field, string error) =>
        new() { FieldErrors = { [field] = error } };
    
    public static ActionError Create(string message) =>
        new() { Messages = { message } };
    
    public string GetFirstError() => 
        FieldErrors.FirstOrDefault().Value ?? Messages.FirstOrDefault();
}
```

**收益**：
- 表单校验可逐字段返回错误
- 前端可直接高亮错误字段
- 与 Ape.Volo 保持一致

---

### 2.4 全局审计过滤器（已实施，需增强）

**当前 YZH-Core**：
```csharp
public class YzhAuditingFilter : IAsyncActionFilter
{
    // 记录：谁 + 时间 + 操作 + IP + 结果
}
```

**Ape.Volo 增强版**：
```csharp
public class AuditingFilter : IAsyncActionFilter
{
    // 额外记录：
    // - 浏览器信息（OS/DeviceType/Name/Version）
    // - IP 地理位置（IP2Region）
    // - 请求/响应数据
    // - 执行耗时
    // - 异步写入数据库（通过 Redis 队列）
}
```

**YZH-Core 改进建议**：
```csharp
public class YzhAuditingFilter : IAsyncActionFilter
{
    private readonly IYzhAuditLogger _auditLogger;
    private readonly IUserContext _userContext;
    private readonly IBrowserDetector _browserDetector; // 新增
    private readonly IP2RegionSearcher _ipSearcher;    // 新增
    
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var sw = Stopwatch.StartNew();
        var result = await next();
        sw.Stop();
        
        var auditEntry = new AuditLogEntry
        {
            // 基础信息
            Level = LogLevel.Information,
            Timestamp = DateTime.UtcNow,
            UserCode = _userContext.UserCode,
            UserName = _userContext.UserName,
            HttpMethod = context.HttpContext.Request.Method,
            Path = context.HttpContext.Request.Path,
            IpAddress = _userContext.ClientIp,
            UserAgent = context.HttpContext.Request.Headers["User-Agent"].ToString(),
            
            // 新增：浏览器信息
            OperatingSystem = _browserDetector.Detect(context.HttpContext.Request).OS,
            DeviceType = _browserDetector.Detect(context.HttpContext.Request).DeviceType,
            BrowserName = _browserDetector.Detect(context.HttpContext.Request).Name,
            BrowserVersion = _browserDetector.Detect(context.HttpContext.Request).Version,
            
            // 新增：IP 地理位置
            IpLocation = _ipSearcher.Search(_userContext.ClientIp),
            
            // 执行信息
            Action = context.ActionDescriptor.DisplayName,
            RequestParams = SerializeParams(context.ActionArguments),
            StatusCode = GetStatusCode(result),
            DurationMs = sw.ElapsedMilliseconds,
            IsSuccess = result.Exception == null,
            ErrorMessage = result.Exception?.Message,
            ExceptionType = result.Exception?.GetType().FullName,
            StackTrace = result.Exception?.StackTrace
        };
        
        await _auditLogger.LogApiCallAsync(auditEntry);
    }
}
```

**收益**：
- 完整追溯操作来源（设备/IP/位置）
- 便于安全审计和问题排查
- 符合政府项目安全要求

---

### 2.5 数据播种中间件（推荐实施）

**Ape.Volo 做法**：
```csharp
public static class DataSeederMiddleware
{
    public static void UseDataSeederMiddleware(this IApplicationBuilder app)
    {
        var settings = App.GetOptions<SettingsOptions>();
        if (settings.IsInitTable)
        {
            var dbContext = app.ApplicationServices.GetRequiredService<DataContext>();
            DataSeeder.InitMasterDataAsync(dbContext, settings.IsInitData, settings.IsQuickDebug).Wait();
            DataSeeder.InitLogData(dbContext);
            DataSeeder.InitTenantDataAsync(dbContext).Wait();
        }
    }
}

// 播种数据
public static class DataSeeder
{
    public static async Task InitMasterDataAsync(DataContext db, bool init, bool quickDebug)
    {
        if (!init) return;
        
        // 确保超级管理员存在
        if (!db.Users.Any(u => u.Account == "admin"))
        {
            db.Users.Add(new User
            {
                Account = "admin",
                Password = PasswordHelper.Encrypt("admin123"),
                Name = "超级管理员",
                IsAdmin = true
            });
            await db.SaveChangesAsync();
        }
        
        // 确保基础菜单存在
        // ...
    }
}
```

**YZH-Core 改进建议**：
```csharp
// 数据播种中间件
public static class YzhDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseDbContext>();
        
        // 1. 确保超级管理员存在
        if (!db.Set<SysUser>().Any(u => u.Code == "admin"))
        {
            db.Set<SysUser>().Add(new SysUser
            {
                Code = "admin",
                UserName = "admin",
                Password = PasswordHelper.Encrypt("admin123"),
                RoleId = 1,
                IsDeleted = false
            });
            await db.SaveChangesAsync();
        }
        
        // 2. 确保基础菜单存在
        await SeedMenusAsync(db);
        
        // 3. 确保字典数据存在
        await SeedDictsAsync(db);
    }
    
    private static async Task SeedMenusAsync(BaseDbContext db)
    {
        // 种子数据逻辑
    }
    
    private static async Task SeedDictsAsync(BaseDbContext db)
    {
        // 种子数据逻辑
    }
}

// 在 Program.cs 中调用
var app = builder.Build();

// 数据播种（仅开发环境）
if (app.Environment.IsDevelopment())
{
    await YzhDataSeeder.SeedAsync(app.Services);
}

app.UseYzhPipeline();
```

**收益**：
- 部署自动化
- 新环境一键初始化
- 避免手动操作失误

---

### 2.6 声明式事务（推荐实施）

**Ape.Volo 做法**：
```csharp
// 特性标记
[AttributeUsage(AttributeTargets.Method)]
public class UseTranAttribute : Attribute { }

// AOP 拦截器
public class TransactionAop : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        if (invocation.Method.GetCustomAttribute<UseTranAttribute>() != null)
        {
            _unitOfWork.BeginTran();
            try
            {
                invocation.Proceed();
                _unitOfWork.CommitTran();
            }
            catch
            {
                _unitOfWork.RollbackTran();
                throw;
            }
        }
        else
        {
            invocation.Proceed();
        }
    }
}

// 使用方式
[UseTran]
public async Task<bool> TransferMoney(decimal amount, string fromAccount, string toAccount)
{
    // 扣款
    await _accountService.DecreaseAsync(fromAccount, amount);
    // 收款
    await _accountService.IncreaseAsync(toAccount, amount);
    return true;
}
```

**YZH-Core 改进建议**：
```csharp
// 轻量级事务特性（无需 AOP 框架）
[AttributeUsage(AttributeTargets.Method)]
public class YzhTransactionAttribute : Attribute { }

// 事务拦截器（通过 DispatchProxy）
public class ServiceProxy<T> : DispatchProxy where T : class
{
    private T _target;
    private IServiceProvider _services;
    
    public static T Create(T target, IServiceProvider services)
    {
        var proxy = Create<ServiceProxy<T>, T>(target, services);
        return proxy;
    }
    
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var hasTran = targetMethod?.GetCustomAttribute<YzhTransactionAttribute>() != null;
        if (!hasTran) return targetMethod?.Invoke(_target, args);
        
        var db = _services.GetRequiredService<BaseDbContext>();
        using var tx = db.Database.BeginTransaction();
        try
        {
            var result = targetMethod?.Invoke(_target, args);
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}

// 使用方式
public class AccountService : BaseService<Account>
{
    [YzhTransaction]
    public async Task<bool> TransferMoney(decimal amount, string fromAccount, string toAccount)
    {
        // 扣款
        await DecreaseAsync(fromAccount, amount);
        // 收款
        await IncreaseAsync(toAccount, amount);
        return true;
    }
}
```

**收益**：
- 消除样板代码
- 事务边界清晰
- 异常自动回滚

---

### 2.7 SqlSugar 全局过滤（可选，需评估）

**Ape.Volo 做法**：
```csharp
// SqlSugar 全局过滤器
db.Queryable<User>().Where(s => s.IsDeleted == false); // 自动附加
db.Queryable<User>().Where(s => s.TenantId == currentTenantId); // 自动附加
db.Queryable<User>().Where(s => s.CreateBy == currentUserId); // 自动附加
```

**YZH-Core 现状**：
```csharp
// 手动过滤
var query = DbSet.AsQueryable();
query = query.Where(e => !e.IsDeleted);
```

**建议**：
- 如果切换 SqlSugar，可自动获得全局过滤能力
- 如果保留 EF Core，需手动实现或引入中间件

---

## 三、优先级建议

### P0（立即实施）

| 改进项 | 工作量 | 收益 |
|--------|--------|------|
| **泛型主键** | 1 天 | 灵活性提升 |
| **接口组合** | 0.5 天 | 代码清晰度提升 |
| **结构化错误** | 0.5 天 | 前端体验提升 |
| **审计日志增强** | 1 天 | 安全审计能力 |
| **数据播种** | 0.5 天 | 部署自动化 |

### P1（短期实施）

| 改进项 | 工作量 | 收益 |
|--------|--------|------|
| **声明式事务** | 2 天 | Service 代码简化 |
| **浏览器/IP 检测** | 1 天 | 审计日志完整性 |
| **健康检查增强** | 0.5 天 | 监控运维 |

### P2（中期实施）

| 改进项 | 工作量 | 收益 |
|--------|--------|------|
| **SqlSugar 迁移评估** | 3 天 | 查询能力增强 |
| **AOP 缓存** | 2 天 | 性能优化 |
| **多租户支持** | 5 天 | 业务扩展 |

---

## 四、具体实施步骤

### Phase 1：基础增强（1 周）

```
1. 定义泛型主键基类 RootKey<TKey>
2. 定义接口 I auditable / I SoftDeletable / I Tenantable
3. 增强 ApiResponse 支持 ActionError
4. 增强 YzhAuditingFilter 记录浏览器/IP
5. 实现 YzhDataSeeder 中间件
```

### Phase 2：能力扩展（2 周）

```
1. 实现声明式事务 [YzhTransaction]
2. 引入 IP2Region 地理位置检测
3. 引入 BrowserDetection 浏览器检测
4. 实现 AOP 缓存 [YzhCache]
5. 评估 SqlSugar 迁移可行性
```

### Phase 3：高级特性（持续）

```
1. 多租户支持（如需要）
2. 限流中间件
3. 在线用户统计
4. 慢 SQL 监控
```

---

## 五、总结

### 核心建议

1. **立即实施**：泛型主键 + 接口组合 + 结构化错误 + 审计增强 + 数据播种
2. **短期实施**：声明式事务 + 浏览器/IP 检测
3. **中期评估**：SqlSugar 迁移 + AOP 缓存 + 多租户

### 关键原则

- **不重复造轮子**：借鉴 Ape.Volo 已有成熟设计
- **渐进式完善**：分 Phase 实施，每阶段可验证
- **保持兼容性**：新架构与 Vol 框架共存过渡

### 下一步行动

1. 确认优先级（与用户讨论）
2. 制定详细实施计划
3. 开始 Phase 1 实施

---

*（内容由AI生成，仅供参考）*
