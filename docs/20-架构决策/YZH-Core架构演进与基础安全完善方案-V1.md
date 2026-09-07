# YZH-Core 架构演进与基础安全完善方案

> **版本**：V1.0 | **日期**：2026-09-07 | **状态**：执行中
>
> **目标**：完成基础架构一步到位，启动审核员端开发

---

## 一、架构演进策略

### 1.1 核心思路

```
Step 1: 原子能力 + 基类 → 先实现
Step 2: 业务验证 → 用业务论证成熟度
Step 3: 复杂业务 → 基于原子能力重构
Step 4: 持续完善 → 按需增加原子能力
```

### 1.2 分阶段实施计划

| 阶段 | 时间 | 目标 | 关键产出 |
|------|------|------|---------|
| **Phase 1** | Day 1-10 | 基础架构稳定 | 原子能力 + 基类 + 基础安全 |
| **Phase 2** | Day 11-30 | 审核员端开发 | 业务验证 + 架构调整 |
| **Phase 3** | Day 31-60 | 复杂业务重构 | 旧架构迁移 + 增强能力 |
| **Phase 4** | Day 61+ | 企业端开发 | 架构成熟 + 技术债务清理 |

---

## 二、基础安全一步到位清单

### 2.1 必须完成项（不可妥协）

| 序号 | 项目 | 状态 | 负责人 | 截止时间 |
|------|------|------|--------|---------|
| 1 | 全局异常处理 | ✅ 已完成 | AI | - |
| 2 | JWT 认证中间件 | ✅ 已完成 | AI | - |
| 3 | SQL 注入防护 | ✅ 已完成 | AI | - |
| 4 | 基础审计日志 | ✅ 已完成 | AI | - |
| 5 | YzhControllerBase<T> | ✅ 已完成 | AI | - |
| 6 | EntityService<T> | ✅ 已完成 | AI | - |
| 7 | TreeControllerBase<T> | ⏳ 待完成 | AI | Day 3 |
| 8 | 权限拦截器 | ⏳ 待完成 | AI | Day 4 |
| 9 | 输入验证框架 | ⏳ 待完成 | AI | Day 5 |
| 10 | 前端原子组件 | ⏳ 待完成 | AI | Day 6 |

---

## 三、基础安全详细设计

### 3.1 全局异常处理

**当前实现**：`GlobalExceptionMiddleware.cs`

```csharp
// 功能：
// 1. 捕获所有未处理异常
// 2. 统一错误响应格式
// 3. 记录错误日志
// 4. 开发环境返回详细错误，生产环境返回通用错误

// 已实现：
□ 异常捕获
□ 错误日志记录
□ 统一响应格式
□ 开发/生产环境区分
```

**待完善**：
```csharp
// 新增：业务异常类型
public class YZHBusinessException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }
    
    public YZHBusinessException(string message, int statusCode = 400, string errorCode = "BUSINESS_ERROR")
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

// 新增：参数校验异常
public class YZHValidationException : YZHBusinessException
{
    public Dictionary<string, string> FieldErrors { get; }
    
    public YZHValidationException(Dictionary<string, string> fieldErrors)
        : base("参数校验失败", 422, "VALIDATION_ERROR")
    {
        FieldErrors = fieldErrors;
    }
}
```

---

### 3.2 JWT 认证

**当前实现**：`Program.cs` 中已配置

```csharp
// 已实现：
□ JWT 认证中间件配置
□ Token 生成逻辑
□ 用户信息解析
□ 过期时间处理
```

**待完善**：
```csharp
// 新增：Token 刷新机制
public class TokenRefreshService
{
    public async Task<(string newToken, DateTime expiresIn)> RefreshTokenAsync(string refreshToken)
    {
        // 验证 refresh token
        // 生成新 access token
        // 返回新 token 和过期时间
    }
}

// 新增：接口级权限校验
[AttributeUsage(AttributeTargets.Method)]
public class RequirePermissionAttribute : Attribute
{
    public string PermissionCode { get; }
    
    public RequirePermissionAttribute(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}

// 使用示例
[RequirePermission("sys:user:add")]
[HttpPost("add")]
public async Task<IActionResult> Add([FromBody] Sys_User entity)
{
    // ...
}
```

---

### 3.3 SQL 注入防护

**当前实现**：`SqlSecurityHelper.cs`

```csharp
// 已实现：
□ 表名校验（IsValidIdentifier）
□ SQL 片段校验（ValidateSqlFragment）
□ 标识符转义（EscapeIdentifier）
□ 排序字段校验
```

**待完善**：
```csharp
// 新增：敏感操作审计
public class SqlOperationLogger
{
    public void LogQuery(string sql, object parameters, string controller, string action)
    {
        // 记录 SQL 查询（仅开发环境）
        // 检测潜在危险操作
    }
    
    public void LogSuspiciousQuery(string sql, string reason)
    {
        // 记录可疑查询
        // 触发告警
    }
}

// 新增：SQL 查询白名单机制（可选）
public class SqlWhitelist
{
    private static readonly HashSet<string> AllowedTables = new()
    {
        "Sys_User", "Sys_Role", "Sys_Menu", "Sys_Dictionary"
        // ... 只允许已知表
    };
    
    public static bool IsValidTable(string tableName)
    {
        return AllowedTables.Contains(tableName);
    }
}
```

---

### 3.4 权限拦截器

**当前状态**：待实现

```csharp
// 设计：基于角色的访问控制（RBAC）
public class PermissionFilter : IAuthorizationFilter
{
    private readonly IUserContext _userContext;
    private readonly IPermissionService _permissionService;
    
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // 1. 获取当前用户
        var user = _userContext.User;
        if (user == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        
        // 2. 检查接口权限
        var requiredPermission = context.ActionDescriptor
            .GetMethodAttribute<RequirePermissionAttribute>();
        
        if (requiredPermission != null)
        {
            var hasPermission = _permissionService.HasPermission(
                user.Code, 
                requiredPermission.PermissionCode
            );
            
            if (!hasPermission)
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
        }
        
        // 3. 检查数据权限（如：只能看自己创建的数据）
        var dataScope = context.ActionDescriptor
            .GetMethodAttribute<RequireDataScopeAttribute>();
        
        if (dataScope != null)
        {
            // 添加数据权限过滤条件
            context.HttpContext.Items["DataScope"] = 
                _permissionService.GetDataScope(user);
        }
    }
}

// 使用示例
[RequirePermission("sys:user:view")]
[RequireDataScope(DataScopeType.Self)]
[HttpGet]
public IActionResult GetList()
{
    // ...
}
```

---

### 3.5 输入验证框架

**当前状态**：待实现

```csharp
// 设计：基于 DataAnnotation 的验证
public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // 1. 验证请求体
        foreach (var arg in context.ActionArguments.Values)
        {
            var validationContext = new ValidationContext(arg);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(arg, validationContext, validationResults, true))
            {
                var errors = validationResults
                    .GroupBy(r => r.MemberName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(r => r.ErrorMessage).FirstOrDefault()
                    );
                
                throw new YZHValidationException(errors);
            }
        }
    }
}

// 实体验证示例
public class Sys_User : BaseEntity
{
    [Required(ErrorMessage = "账号不能为空")]
    [StringLength(100, ErrorMessage = "账号长度不能超过100")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "账号只能包含字母、数字和下划线")]
    public string UserName { get; set; }
    
    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(200, ErrorMessage = "密码长度不能超过200")]
    public string UserPwd { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "角色ID必须大于0")]
    public int RoleId { get; set; }
    
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string? Email { get; set; }
    
    [Phone(ErrorMessage = "手机号格式不正确")]
    public string? PhoneNo { get; set; }
}
```

---

## 四、实施计划

### 4.1 Day 1-2：基础安全完善

```
□ 完善业务异常类型（YZHBusinessException/YZHValidationException）
□ 完善全局异常处理（捕获业务异常，返回友好错误）
□ 完善 JWT 认证（Token 刷新机制）
□ 完善权限拦截器（RequirePermission 特性）
□ 完善输入验证（ValidationFilter）
```

### 4.2 Day 3-4：原子能力完善

```
□ 完成 TreeControllerBase<T>
□ 完成 TreeUtils 核心方法
□ 完成 YzhTree 组件基础版
□ 完成 CrudPageLogic<T>
□ 完成 TreeTableLogic<T,V>
```

### 4.3 Day 5-6：前端组件完善

```
□ 完成 YzhTable 组件
□ 完成 YzhForm 组件
□ 完成 YzhApiClient
□ 完善类型定义
```

### 4.4 Day 7-10：业务验证

```
□ 用新架构完成 1 个完整业务模块
□ 验证原子能力成熟度
□ 收集问题并调整
□ 文档化最佳实践
```

---

## 五、验收标准

### 5.1 基础安全验收

| 项目 | 验收标准 | 状态 |
|------|---------|------|
| 全局异常 | 所有异常都能被捕获并返回统一格式 | ⏳ |
| JWT 认证 | Token 生成/验证/刷新正常 | ⏳ |
| SQL 防护 | 所有 SQL 都经过校验 | ⏳ |
| 权限控制 | 接口级权限校验生效 | ⏳ |
| 输入验证 | 参数校验自动触发 | ⏳ |
| 审计日志 | 所有 API 调用都有日志 | ⏳ |

### 5.2 原子能力验收

| 项目 | 验收标准 | 状态 |
|------|---------|------|
| YzhControllerBase | CRUD 接口零代码实现 | ⏳ |
| EntityService | 原子方法返回 Result<T> | ⏳ |
| TreeControllerBase | 树形接口零代码实现 | ⏳ |
| TreeUtils | 纯函数，可单元测试 | ⏳ |
| YzhTree | 支持懒加载/勾选/搜索 | ⏳ |

---

## 六、风险与应对

| 风险 | 等级 | 应对措施 |
|------|------|---------|
| 业务异常类型设计不合理 | 中 | Day 1 完成设计，Day 2 验证 |
| 权限拦截器性能影响 | 低 | 缓存权限数据，异步校验 |
| 输入验证过于严格 | 中 | 提供自定义验证器扩展点 |
| 前端组件与后端不匹配 | 高 | Day 5-6 联调验证 |

---

## 七、下一步行动

```
今天：
□ 确认基础安全清单
□ 开始完善业务异常类型
□ 开始完善权限拦截器

明天：
□ 完成输入验证框架
□ 完成 JWT 刷新机制
□ 联调测试

后天：
□ 完成 TreeControllerBase
□ 完成 TreeUtils
□ 启动前端组件开发
```

---

*（内容由AI生成，仅供参考）*
