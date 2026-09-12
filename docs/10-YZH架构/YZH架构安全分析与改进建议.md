# YZH 架构安全分析与改进建议

> 基于第二轮架构评估报告，结合架构设计意图与安全现状
> 日期：2026-09-12

---

## 〇、评估背景与范围

### 0.1 评估对象

- **后端框架层**：`src/yzh-core`（包含 YZH.Core.Stand / DataBase / Api / Web 四个子工程）
- **前端框架层**：`src/certplatform-web/yzh.vue.core`
- **评估基准**：`docs/10-YZH架构`（01-架构总纲 / 12-框架能力清单-V1 / 09-架构修复清单）

### 0.2 评估口径

只评**架构自身合理性**，不与具体项目业务功能耦合判定。所有结论均附文件/类/行级证据，代码未读到的不作结论。

### 0.3 上一轮评估的纠正

| # | 上一轮结论 | 本轮判定 | 纠正后表述 |
|:--:|-----------|:--------:|-----------|
| 1 | System 域控制器在 `YZH.Core.Web` 内 = 分层倒置/业务耦合 | ❌ 定级错误 | 这些是跨项目通用基础能力，属框架层；问题是**承载位置与依赖方向** |
| 2 | `CrudPageLogic`/`YzhCrudPage`/`useTable` 引用 0 = 主链路空转 | ⚠️ 定性不准 | 框架约定已定义，业务接入面未落地（落地与推行问题） |
| 3 | 明文密钥硬编码 = 框架安全缺陷 | ✅ 维持 | 兜底默认值写在框架启动器内，属框架层安全约定缺失 |
| 4 | 前端 66 端点 404 / 工作流主干无实现 | ✅ 维持 | 能力落地缺口（与能力清单 📋 标记一致） |

---

## 一、结构设计分析

### 1.1 后端工程与依赖方向

#### 已读事实

| 工程 | 引用关系 | 证据 |
|------|---------|------|
| `YZH.Core.Stand` | SqlSugarCore 5.1.4.167、System.IdentityModel.Tokens.Jwt 7.5.1、Caching.Memory 8.0.0 | `YZH.Core.Stand/YZH.Core.Stand.csproj:13-16` |
| `YZH.Core.DataBase` → `YZH.Core.Stand` | 依赖方向正确 | `YZH.Core.DataBase/YZH.Core.DataBase.csproj:18` |
| `YZH.Core.Api` → `DataBase` + `Stand` | 依赖方向正确，未见业务引用 | `YZH.Core.Api/YZH.Core.Api.csproj:18-19` |
| `YZH.Core.Web` | **反向引用** 遗留栈 3 个工程（`Aliases="VolFramework"`） | `YZH.Core.Web/YZH.Core.Web.csproj:26-28` |
| `YZH.Core.Web` | **反向引用** 业务层 4 个工程（`CertPlatform.Shared` 用 `Aliases="CertPlatform"`） | `YZH.Core.Web/YZH.Core.Web.csproj:34-37` |
| 业务侧 | `CertPlatform.Admin` 单向引用 `YZH.Core.Api` / `DataBase` | `CertPlatform.Admin.csproj:20-22` |

#### 设计意图澄清

**评估报告原结论**：`YZH.Core.Web`（框架宿主）→ `certplatform-api`（业务层）是"依赖倒挂"，标为 P0。

**实际设计意图**：
- `YZH.Core.Web` 是**开发期聚合宿主**，通过 `extern alias` 将各业务项目引入同一进程
- 调试时可在同一进程内追踪完整调用链，快速定位问题是框架层还是业务层
- 各业务项目代码保持独立，通过 `extern alias` 桥接
- **这不是架构缺陷，而是有意为之的工程化决策**

#### 评估报告的问题

| 原文描述 | 问题 | 建议修改 |
|---------|------|---------|
| "框架可提取为 NuGet 的结构性阻断" | 错误理解设计意图 | 改为"开发期宿主与生产期宿主分离" |
| "依赖方向错误" | 定性错误 | 改为"开发期宿主聚合模式" |
| P0 优先级 | 过度严重 | 降为 P3（文档澄清类） |

#### 架构文档修正建议

在 `01-架构总纲` 中补充：

```markdown
## 宿主容器定位

### 开发期宿主（YZH.Core.Web）
- 用途：调试、测试、示例
- 特性：通过 extern alias 聚合业务工程
- 限制：不可直接用于生产部署
- 定位：开发期工程便利工具，非生产架构

### 生产期宿主
- 由各项目自行构建（CertPlatform.Host 等）
- 引用 YZH.Core.* 框架包
- 不包含业务代码
```

---

### 1.2 前端工程与模块边界

#### 已读事实

- `yzh.vue.core` 自述为"与项目无关通用库"（`package.json:2-5`）
- 框架内部引用业务共享层：`YzhCrudPage.vue` 引用 `@share/api/generic`、`@share/types/contracts`
- 宿主接入约定未固化：`@yzh-core` / `@share` 两个 alias 由各宿主工程各自配置

#### 问题与改进

| 问题 | 优先级 | 改进建议 |
|------|:------:|---------|
| 框架组件直接引用 `@share/*` | P0 | 通过 props 注入，框架不直接 import 业务包 |
| alias 配置由各项目各自维护 | P1 | 导出统一 vite plugin/preset |
| `utils/treeOps.ts`(483) 与 `utils/treeUtils.ts`(584) 并存 | P1 | 合并为一套 |
| `.DS_Store` 被纳入源码 | P3 | 补 `.gitignore` 并清理 |

---

## 二、理念设计分析

### 2.1 五原则的可判定性

`01-架构总纲 §1.1` 给出五原则：
1. 配置驱动 UI
2. 统一基类
3. API 固定
4. 业务差异隔离
5. 增量更新

**黄金判断法则**："如果明天做一个完全不同的项目，这段代码还需要吗？需要 → 架构层；不需要 → 项目层。"

这是框架最大的资产，使"能力归属"具备可判定性。

### 2.2 缺口一：缺少"框架层内部分层"的裁定规则

黄金判断法则只能判定"在架构层还是项目层"，**不能判定"在架构层内部的哪个工程"**。

实际后果：
- System 域能力落在 `YZH.Core.Web`（宿主）而非 `YZH.Core.Api`（能力层）
- 依赖方向未被规则覆盖

**改进建议**：在 `01-架构总纲` 增补《框架层内部分层准入清单》

---

### 2.3 缺口二：能力状态标记无机制支撑

`12-框架能力清单` 用 ✅/🔄/📋/📦/🗑️ 五态描述 25 项能力，但状态靠人工维护，代码无断言。

**改进建议**：
1. 清单新增"**当前实际行为**"列（与"目标状态"区分）
2. 引入 `[Capability("权限校验")]` 特性或测试断言
3. CI 中校验"标记 ✅ 的能力其关键方法不含 TODO"

### 2.4 正面样本：命名边界已从理念落到机制

`YZH.Core.Stand/BizConventions/BizNamingRules.cs` 是"理念 → 机制"闭环样本：
- 黑名单（13 个类名）
- 启动期抛 `InvalidOperationException`
- 批量校验入口

**瑕疵**：
1. 黑名单含数据库中不存在的类名（`Sys_UserRole`、`Sys_AuditLog`）
2. 命名空间校验只打印 `Console.WriteLine`，不抛异常
3. 调用入口是否挂载未证实

---

## 三、约定设计分析

### 3.1 约定清单（已具备，可遵循）

| 约定 | 落地形态 | 证据 |
|------|---------|------|
| 控制器基类 | 单表 `YzhControllerBase<V>`（757 行）/ 左树右表 `TreeTableControllerBase<T,V>`（680 行） | `Controllers/YzhControllerBase.cs` |
| 路由固定 | `/config /filter /add /update /delete /export /import /action/{methodName}` 等 | `YzhControllerBase.cs:423+` |
| 操作注册制 | 行操作必须 `RegisterRowAction` | `YzhControllerBase.cs:526` |
| 钩子约定 | 单表 9 个钩子、树 6 个钩子 | `YzhControllerBase.cs:622-693` |
| Code 主键/审计字段 | 自动填充审计、软删除/有效标志过滤 | `Services/EntityService.cs:25` |
| 前后端统一响应 | `ApiResponse` / `PagedResult` | `YZH.Core.Stand/Models` |
| 查询字段安全 | 过滤字段名正则白名单 | `FilterOperation.cs:39` |

### 3.2 可遵循性缺陷

| # | 问题 | 优先级 | 证据 |
|:--:|------|:------:|------|
| 1 | `Code` 主键约定在数据访问层被硬编码，无 Code 实体必崩 | P0 | `SqlSugarDbOrm.cs:152-215` |
| 2 | 软删除/有效标志过滤有两套实现，其中一套是方言硬编码 | P0 | `SqlSugarDbOrm.cs:79-88` vs `:243-291` |
| 3 | `GetByCodeAny` 拼表名 + MySQL 方言 `LIMIT 1` | P1 | `EntityService.cs:86` |
| 4 | 接口承诺与实现不符（假参数） | P1 | `includeDeleted` 参数未传递 |
| 5 | 约定外路径无登记（3 个裸 `ControllerBase`） | P1 | `MenuController` / `AuthController` / `ApiSyncController` |
| 6 | 分页上限缺失 | P1 | 未见 `MaxPageSize` 钳制代码 |
| 7 | 前端"必须继承"的约定无违规检测 | P1 | `CrudPageLogic` 0 命中，`TreeTableLogic` 仅 4 命中 |

---

## 四、前后端关系分析

### 4.1 契约对齐方式

| 项 | 现状 | 问题 |
|----|------|------|
| 契约来源 | 前端 `types/contracts.ts` 手写 TS 类型 | 与后端逐字段人工对齐，易漂移 |
| HTTP 入口 | **两套并存**：`utils/http.ts` 与 `api/client.ts` | 错误处理、401 回调逻辑不一致 |
| 契约漂移防护 | 无（无 OpenAPI 生成、无 CI 契约比对） | — |

### 4.2 实际发生的契约漂移

1. 前端调 `/api/Sys_User/*`（后端为 `api/System/User`）
2. 字典页调 `/api/Sys_Dictionary/*`
3. `log/index.vue` import 不存在的 `@share/api/system-log`
4. 同一前端工程内两种 HTTP 客户端并存

### 4.3 改进建议

| 优先级 | 改进项 | 说明 |
|:------:|--------|------|
| P0 | 合并前端 HTTP 入口 | 保留 `api/client.ts`，`utils/http.ts` 标注 deprecated |
| P0 | 收敛契约来源 | 用 OpenAPI → `openapi-typescript` 生成 `contracts.ts` |
| P1 | 双模型归一 | `Result<T>` 与 `ApiResponse<T>` 明确分工 |
| P1 | 契约版本 | 前后端契约加版本号 |

---

## 五、核心能力清单分析

### 5.1 清单统计

25 项能力：
- ✅ 已实现：10 项（40%）
- 🔄 完善中：5 项（20%）
- 📋 规划中：6 项（24%）
- 📦 基础数据：2 项（8%）
- 🗑️ 可弃用：2 项（8%）

### 5.2 必须做实（P0）

| # | 能力 | 代码事实 | 判定 |
|:--:|------|---------|------|
| 1 | 接口级权限校验 | `PermissionFilter.CheckPermission` 硬编码 `return true` | 能力"存在但恒真"，比"未实现"更危险 |
| 2 | 数据权限（水平权限） | `GetDataScope` 中 `RoleId = 0 // TODO` | 必须做实 |
| 3 | 操作日志落库 | `sys_log` 0 行，框架侧实际落点是文件/控制台 | 必须做实 |
| 4 | 字典项加载 | `LoadDictItems()` 为空实现 | 必须做实 |
| 5 | 系统参数双表决策 | `sys_config`（框架，0 行）vs `cert_sys_config`（22 行） | 必须决策 |

### 5.3 应补强（P1）

| # | 能力 | 补强要点 |
|:--:|------|---------|
| 6 | 更改密码 | 补端点 + 改密后失效 Token |
| 7 | 微信注册 | 按清单建议作为框架账号能力实现 |
| 8 | 队列中心 | 把导出/导入/审计落库等长任务统一入队 |
| 9 | 配置驱动 UI | 补"接入度"指标与最小接入示例 |
| 10 | 文件上传 | 补：扩展名/MIME/大小白名单 |
| 11 | 工作流 | 正式标注 `Sys_WorkFlow*` 弃用 |

---

## 六、安全隐患详细分析与改进建议

> 以下均为代码事实；标 🔴 的应视为上线阻断项。

### 6.1 🔴 P0 级安全问题（必须立即修复）

---

#### 问题 1：权限校验恒真（`PermissionFilter.cs:114-122`）

**问题现状**：
- `CheckPermission` 方法硬编码 `return true`
- 所有已认证用户拥有全部接口权限
- 比"未实现"更危险：给人已生效的错觉

**架构反馈**：
- 角色-接口权限功能已完善，可以修复

**改进建议**：

```csharp
// Filters/PermissionFilter.cs
public class PermissionFilter : IAsyncActionFilter
{
    private readonly IRoleService _roleService;
    private readonly IApiAccessService _apiAccessService;
    
    public async ValueTask OnActionExecutionAsync(
        ActionExecutingContext ctx, 
        ActionExecutionDelegate next)
    {
        // 1. 检查匿名特性
        var anonAttr = ctx.ActionDescriptor.GetCustomAttributes<AllowAnonymousAttribute>(true);
        if (anonAttr.Any())
        {
            await next();
            return;
        }
        
        // 2. 权限检查
        var permissionAttr = ctx.ActionDescriptor.GetCustomAttributes<PermissionAttribute>(true);
        if (permissionAttr.Any())
        {
            var attr = permissionAttr.First();
            var hasPermission = await CheckPermissionAsync(ctx.HttpContext, attr);
            if (!hasPermission)
            {
                ctx.Result = new ForbidResult();
                return;
            }
        }
        
        await next();
    }
    
    private async Task<bool> CheckPermissionAsync(HttpContext context, PermissionAttribute attr)
    {
        // 接口级权限：角色-接口映射
        var requiredRole = attr.RequiredRole;
        if (!string.IsNullOrEmpty(requiredRole))
        {
            var userId = context.GetUserId();
            var hasRole = await _roleService.HasRoleAsync(userId, requiredRole);
            if (!hasRole) return false;
        }
        
        // 接口路径权限
        var requiredApi = attr.RequiredApi;
        if (!string.IsNullOrEmpty(requiredApi))
        {
            var userId = context.GetUserId();
            var path = context.Request.Path;
            var hasAccess = await _apiAccessService.HasAccessAsync(userId, path, requiredApi);
            if (!hasAccess) return false;
        }
        
        return true;
    }
}

// 新增特性
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PermissionAttribute : Attribute
{
    public string RequiredRole { get; set; }
    public string RequiredApi { get; set; }
}
```

**测试断言**：
```csharp
[Fact]
public async Task CheckPermission_RejectsUserWithoutRole()
{
    var user = CreateUser("普通用户", roles: ["viewer"]);
    var context = CreateHttpContext(user);
    var filter = new PermissionFilter(_roleService, _apiAccessService);
    var attr = new PermissionAttribute(requiredRole: "admin");
    
    var result = await filter.CheckPermission(context, attr);
    Assert.False(result);
}
```

---

#### 问题 2：数据权限（水平权限）（`PermissionFilter.cs:127-138`）

**问题现状**：
- `RequireDataScope` 只写 `HttpContext.Items`
- `GetDataScope` 中 `RoleId = 0 // TODO`
- 框架层无统一注入

**架构反馈**：
- 架构无法统一实现，不同项目需求不同
- 采用虚企业（attach）+ 虚用户（attach）机制实现
- 不属于框架层职责

**架构建议**：
- 框架提供**数据权限注入点**（hook），不强制实现
- 各项目通过继承或实现接口来自定义数据权限逻辑

```csharp
// 框架层：提供抽象基类
public abstract class DataPermissionHandler
{
    public abstract Task<DataScope> GetDataScopeAsync(HttpContext context);
}

// 项目层：自定义实现
public class CertDataPermissionHandler : DataPermissionHandler
{
    public override async Task<DataScope> GetDataScopeAsync(HttpContext context)
    {
        // 使用虚企业-虚用户机制
        var userId = context.GetUserId();
        return await GetVirtualEnterpriseScopeAsync(userId);
    }
}
```

**数据库表结构**（如需）：
```sql
CREATE TABLE sys_user_org (
    user_id INT NOT NULL,
    org_code VARCHAR(32) NOT NULL,
    PRIMARY KEY (user_id, org_code)
);
```

---

#### 问题 3：CORS 全开放（`Program.cs:55-61/168`）

**问题现状**：
- `AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`
- 等于任何网站可以调你的 API

**架构反馈**：
- 系统需要挂接到外网
- 所有用户可注册、登录、操作
- 有接口权限和 JWT 认证
- 计划后续实现：重复高频攻击防护、黑名单、异常 IP 限制

**改进建议**：

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("CertPlatform", policy =>
    {
        // 支持多域名配置
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();
        
        policy
            .WithOrigins(allowedOrigins ?? Array.Empty<string>())
            .AllowAnyMethod()
            .AllowHeaders("Authorization", "Content-Type")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// appsettings.json
{
  "Cors": {
    "AllowedOrigins": [
      "https://cert.example.com",
      "https://admin.example.com"
    ]
  }
}
```

**后续安全措施（规划中）**：
- [ ] 频率限制中间件
- [ ] IP 黑名单机制
- [ ] 异常请求检测

---

#### 问题 4：硬编码默认密钥（`YzhWebBuilder.cs:68/79`）

**问题现状**：
- `?? "C5ABA9E202D94C43A3CA66002BF77FAF"` 兜底
- 任何新部署都共享同一个密钥
- 一旦泄露所有用户 token 可伪造

**架构反馈**：
- 重要密钥存放在 `appsettings.json` 中
- Docker 部署需进行文件拦截设置
- 需要生成拦截清单

**改进建议**：

**1. 移除硬编码密钥兜底**

```csharp
// YzhWebBuilder.cs
public static YzhWebBuilder UseYzhCore(this WebApplicationBuilder builder)
{
    // 密码加密密钥
    var encryptionKey = builder.Configuration["YZH:EncryptionKey"];
    if (string.IsNullOrWhiteSpace(encryptionKey))
        throw new InvalidOperationException("YZH:EncryptionKey 必须在 appsettings.json 中配置");
    
    // JWT Secret
    var jwtSecret = builder.Configuration["YZH:JwtSecret"];
    if (string.IsNullOrWhiteSpace(jwtSecret))
        throw new InvalidOperationException("YZH:JwtSecret 必须在 appsettings.json 中配置");
    
    return builder;
}
```

**2. Docker 安全拦截清单**

```yaml
# docker-compose.yml
version: '3.8'
services:
  yzh-core:
    image: yzh-core:latest
    environment:
      - YZH_EncryptionKey=${ENCRYPTION_KEY}
      - YZH_JwtSecret=${JWT_SECRET}
    volumes:
      # 拦截敏感配置文件
      - ./secrets:/app/secrets:ro
      - ./config:/app/config:ro
    security_opt:
      - no-new-privileges:true
```

**3. 敏感文件拦截清单**

| 文件路径 | 拦截方式 | 说明 |
|---------|---------|------|
| `appsettings.Production.json` | Docker 卷挂载 | 生产配置 |
| `secrets/*.key` | 环境变量注入 | 密钥文件 |
| `Assets/EntityConfigs/*.json` | 权限限制 | 实体配置 |
| `logs/*.log` | 定期清理 | 日志轮转 |

**4. appsettings.json 加密存储**

```csharp
// 使用 ASP.NET Core Data Protection 保护敏感配置
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/secrets/keyring"));
```

**5. 启动时校验密钥长度**

```csharp
if (encryptionKey.Length < 32)
    throw new InvalidOperationException("YZH:EncryptionKey 长度不得小于 32 字符");
```

**测试断言**：
```csharp
[Fact]
public async Task Startup_ThrowsWhenEncryptionKeyMissing()
{
    var builder = CreateWebHostBuilder(configureApp: cfg =>
    {
        cfg["YZH:EncryptionKey"] = null;
        cfg["YZH:JwtSecret"] = "some-secret";
    });
    
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => builder.Build());
    Assert.Contains("YZH:EncryptionKey", exception.Message);
}
```

---

#### 问题 5：排序字段未做白名单（`SqlSugarDbOrm.cs:99-104`）

**问题现状**：
- `query.OrderBy($"{options.SortField} DESC")` 直接拼 SQL
- 恶意用户可以传 `Code; DROP TABLE Sys_User--` 等注入 payload

**架构反馈**：
- 可以按白名单执行
- 考虑拦截所有 GET 请求、POST 请求参数
- 拦截数据库关键字

**改进建议**：

**1. 排序字段白名单**

```csharp
// Implementations/SqlSugarDbOrm.cs
private void ValidateSortField(string sortField)
{
    // 只允许字母、数字、下划线，且必须以字母开头
    if (string.IsNullOrWhiteSpace(sortField) || 
        !Regex.IsMatch(sortField, "^[a-zA-Z_][a-zA-Z0-9_]*$"))
    {
        throw new InvalidOperationException($"排序字段 '{sortField}' 包含非法字符");
    }
    
    // 检查是否为实体属性
    var entityType = typeof(T);
    var property = entityType.GetProperty(sortField, 
        BindingFlags.Public | BindingFlags.Instance);
    if (property == null)
        throw new InvalidOperationException($"排序字段 '{sortField}' 在实体 {entityType.Name} 中不存在");
}
```

**2. 请求参数安全拦截器**

```csharp
// Middleware/RequestSecurityMiddleware.cs
public class RequestSecurityMiddleware
{
    private static readonly HashSet<string> SqlKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "ALTER", "CREATE", "EXEC", "EXECUTE",
        "DELETE", "TRUNCATE", "INSERT", "UPDATE",
        "--", ";", "UNION", "SELECT", "INTO"
    };
    
    public async Task InvokeAsync(HttpContext context)
    {
        // GET 请求参数检查
        var queryString = context.Request.QueryString.Value;
        if (!string.IsNullOrEmpty(queryString))
        {
            CheckForSqlInjection(queryString, "QueryString");
        }
        
        // POST 请求体检查
        if (context.Request.Method == "POST" || 
            context.Request.Method == "PUT" ||
            context.Request.Method == "PATCH")
        {
            context.Request.EnableBuffering();
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0;
            
            if (!string.IsNullOrEmpty(body))
            {
                CheckForSqlInjection(body, "RequestBody");
            }
        }
        
        await _next(context);
    }
    
    private void CheckForSqlInjection(string input, string source)
    {
        foreach (var keyword in SqlKeywords)
        {
            if (input.Contains(keyword))
            {
                _logger.LogWarning("检测到潜在 SQL 注入：来源={Source}, 内容={Input}", source, input);
                throw new SecurityException("请求参数包含非法字符");
            }
        }
    }
}
```

**测试断言**：
```csharp
[Fact]
public async Task GetPageAsync_RejectsMaliciousSortField()
{
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _orm.GetPageAsync(new PagerOptions { SortField = "Code; DROP TABLE" }));
    Assert.Contains("非法字符", exception.Message);
}
```

---

#### 问题 6：软删除/有效标志过滤两套实现分裂（`SqlSugarDbOrm.cs:79-88` vs `:243-291`）

**问题现状**：
- 表达式路径走 `IsDeletedCondition<T>() / IsValidCondition<T>()`（库无关）
- 分页路径走反引号字符串硬编码列名（MySQL 方言）
- 换库即报错

**架构反馈**：
- 软删除建议做成接口，需要时继承
- 删除时判断表是否存在该字段，控制是否软删除

**改进建议**：

**1. 定义软删除接口**

```csharp
// 框架层：定义接口
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

public interface IValidFlag
{
    bool IsValid { get; set; }
}
```

**2. 实体继承接口**

```csharp
// 需要软删除的实体
[Table("Sys_User")]
public class Sys_User : BaseEntity, ISoftDelete
{
    public string Name { get; set; }
    public bool IsDeleted { get; set; }
}

// 不需要软删除的实体（如字典表）
[Table("Sys_Dictionary")]
public class Sys_Dictionary : BaseEntity
{
    public string Code { get; set; }
}
```

**3. ORM 层判断接口实现过滤**

```csharp
// Implementations/SqlSugarDbOrm.cs
public async Task<PagedResult<T>> GetPageAsync(PagerOptions options, ...)
{
    var query = _db.Queryable<T>();
    
    // 根据实体是否实现接口决定过滤
    if (typeof(T).IsAssignableFrom(typeof(ISoftDelete)))
    {
        query = query.Where(e => !EF.Property<bool?>(e, "IsDeleted") ?? false);
    }
    
    if (typeof(T).IsAssignableFrom(typeof(IValidFlag)))
    {
        query = query.Where(e => EF.Property<bool?>(e, "IsValid") ?? true);
    }
    
    // ... 分页逻辑
}
```

**测试断言**：
```csharp
[Fact]
public async Task GetPageAsync_UsesExpressionNotSqlString()
{
    var result = await _orm.GetPageAsync(new PagerOptions { PageSize = 10 });
    var sql = CaptureLastSql(); // 用 SqlSugar 的日志捕获
    Assert.DoesNotContain("`IsDeleted`", sql);
}
```

---

#### 问题 7：无 `Code` 实体的 Update/Delete 必崩（`SqlSugarDbOrm.cs:152-215`）

**问题现状**：
- `UpdateAsync` 硬编码 `Where("Code = @Code", ...)`
- `Sys_RoleUser` 等无 `Code` 列的实体调用必崩

**架构反馈**：
- 所有表必须存在 `id` 和 `code`
- 全部检查并重新生成修改数据库脚本

**改进建议**：

**1. 数据库迁移脚本**

```sql
-- 检查所有表是否缺少 Code 列
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'dbo' 
  AND COLUMN_NAME = 'Code'
  AND TABLE_NAME NOT IN (
    SELECT TABLE_NAME FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE COLUMN_NAME = 'Code'
  );

-- 为缺少 Code 列的表添加
ALTER TABLE Sys_RoleUser ADD Code NVARCHAR(64) NULL;
UPDATE Sys_RoleUser SET Code = CAST(Id AS NVARCHAR(64));
ALTER TABLE Sys_RoleUser ALTER COLUMN Code NVARCHAR(64) NOT NULL;
```

**2. 代码层面增强**

```csharp
// 新增验证特性
[AttributeUsage(AttributeTargets.Class)]
public class RequireCodeAttribute : Attribute { }

// 实体标注
[RequireCode]
[Table("Sys_User")]
public class Sys_User : BaseEntity
{
    public string Code { get; set; }
    // ...
}
```

**测试断言**：
```csharp
[Fact]
public async Task DeleteByCodeAsync_ThrowsForNoCodeEntity()
{
    var noCodeOrm = CreateOrm<RoleUser>();
    var exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => noCodeOrm.DeleteByCodeAsync("some-code"));
    Assert.Contains("没有 Code 字段", exception.Message);
}
```

---

#### 问题 8：框架 → 业务反向依赖（`YzhCrudPage.vue` 依赖 `@share/*`）

**问题现状**：
- `YzhCrudPage.vue` 直接引用 `@share/api/generic`、`@share/types/contracts`
- 破坏"框架可独立分发"的假设

**架构反馈**：
- `@share` 目录是项目工程间共享组件的约定
- 框架组件不应直接引用业务包

**改进建议**：

**1. 分离共享层**

```
yzh.vue.core/
├── src/
│   ├── components/     # 框架组件
│   ├── logic/         # 框架逻辑
│   └── utils/         # 框架工具
│
cert-share/
├── src/
│   ├── api/           # 业务共享 API
│   └── types/         # 业务共享类型
```

**2. 框架组件通过 props 注入业务逻辑**

```vue
<!-- YzhCrudPage.vue -->
<script setup>
// 删除：import { getGenericApi } from '@share/api/generic'

// 改为：通过 props 注入
const props = defineProps({
  apiClient: {
    type: Object,
    required: true
  },
  dataTransform: {
    type: Function,
    default: null
  },
  schema: {
    type: Object,
    required: true
  }
})
</script>
```

**测试断言**：
```typescript
it('should work without @share dependency', () => {
  const component = mount(YzhCrudPage, {
    props: {
      apiClient: mockApiClient,
      schema: mockSchema
    }
  });
  expect(component.find('.yzh-crud-page').exists()).toBe(true);
});
```

---

#### 问题 9：前端 HTTP 入口归一（`utils/http.ts` 退役）

**问题现状**：
- `utils/http.ts`(98) 与 `api/client.ts`(262) 两套并存
- 业务同时引用两套
- 错误处理、401 回调逻辑不一致

**架构反馈**：
- 只有一套 HTTP 请求方法
- 统一的拦截器
- 不能搞两套

**改进建议**：

**1. 统一 HTTP 客户端**

```typescript
// yzh.vue.core/src/api/client.ts
class YzhHttpClient {
  private axios: AxiosInstance;
  
  constructor() {
    this.axios = axios.create({
      baseURL: import.meta.env.VITE_API_BASE_URL,
      timeout: 10000,
    });
    
    // 请求拦截器
    this.axios.interceptors.request.use(config => {
      const token = sessionStorage.getItem('yzh_token');
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
      return config;
    });
    
    // 响应拦截器
    this.axios.interceptors.response.use(
      response => response.data,
      error => {
        if (error.response?.status === 401) {
          sessionStorage.removeItem('yzh_token');
          window.location.href = '/login';
        }
        return Promise.reject(error);
      }
    );
  }
  
  get<T>(url: string, params?: any): Promise<T> { ... }
  post<T>(url: string, data?: any): Promise<T> { ... }
  put<T>(url: string, data?: any): Promise<T> { ... }
  delete<T>(url: string): Promise<T> { ... }
}

export const yzhApi = new YzhHttpClient();
```

**2. 移除旧 HTTP 客户端**

```typescript
// utils/http.ts — 标记废弃
/**
 * @deprecated 请使用 @yzh-core/api/client 中的 yzhApi
 */
export const http = { ... };
```

**3. 迁移脚本**

```bash
# scripts/verify-http-consistency.sh
grep -r "@yzh-core/utils/http" src/cert-*/src --include="*.ts" --include="*.vue"
# 预期：无任何匹配（或只有 deprecated 注释）
```

---

#### 问题 10：表名拼接（`EntityService.cs:86`）

**问题现状**：
- `SELECT * FROM {tableName} WHERE Code = @code LIMIT 1`
- 表名直接拼接，存在注入风险
- `LIMIT` 为 MySQL 专有

**架构反馈**：
- 采用获取表名的方法
- 分析实体类中是否存在 ViewName
- 如果包含 ViewName 就是视图，否则是表名
- 考虑安全措施

**改进建议**：

```csharp
// Services/EntityService.cs
public async Task<T> GetByCodeAnyAsync<T>(string code) where T : BaseEntity
{
    // 1. 从类型注册表获取元信息
    var entityMeta = _entityMetaRegistry.GetMeta(typeof(T));
    
    // 2. 判断是表还是视图
    if (entityMeta.IsView)
    {
        // 视图查询（只读）
        return await _db.Queryable<T>()
            .Where("Code = @code", new { code })
            .FirstAsync();
    }
    else
    {
        // 表查询（使用 ORM，防注入）
        return await _db.Queryable<T>()
            .Where("Code = @code", new { code })
            .FirstAsync();
    }
}

// 实体元数据注册
public class EntityMetaRegistry
{
    private readonly Dictionary<Type, EntityMeta> _metas = new();
    
    public void Register<T>(bool isView = false) where T : BaseEntity
    {
        _metas[typeof(T)] = new EntityMeta
        {
            TypeName = typeof(T).Name,
            IsView = isView,
            TableName = typeof(T).GetCustomAttribute<TableAttribute>()?.Name 
                       ?? typeof(T).Name
        };
    }
    
    public EntityMeta GetMeta(Type type)
    {
        return _metas.TryGetValue(type, out var meta) ? meta 
            : throw new InvalidOperationException($"实体 {type.Name} 未注册到框架");
    }
}
```

**测试断言**：
```csharp
[Fact]
public async Task GetByCodeAnyAsync_UsesRegisteredTableName()
{
    var result = await _service.GetByCodeAnyAsync<Sys_User>("admin");
    Assert.NotNull(result);
}
```

---

### 6.2 🟠 P1 级安全问题（重要但不阻断）

---

#### 问题 11：审计过滤器记录敏感数据（`YzhAuditingFilter.cs:56/104-105`）

**问题现状**：
- `ActionArguments` 全量序列化，注释称"脱敏"但清单需核对字段
- `StackTrace` + `CallerInfo` 落盘
- 敏感字段可能泄露

**改进建议**：

```csharp
// YzhAuditingFilter.cs
public class AuditingFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "UserPwd", "Password", "Token", "Secret", "ApiKey",
        "IdCard", "Phone", "Email", "BankAccount"
    };
    
    private object SanitizeParams(object parameters)
    {
        if (parameters == null) return null;
        
        var type = parameters.GetType();
        var result = Activator.CreateInstance(type);
        
        foreach (var prop in type.GetProperties())
        {
            var value = prop.GetValue(parameters);
            if (SensitiveFields.Contains(prop.Name))
                prop.SetValue(result, "[REDACTED]");
            else
                prop.SetValue(result, value);
        }
        
        return result;
    }
    
    // StackTrace 只写本地日志，不入审计存储
    private void LogAudit(ActionExecutingContext context, string actionName)
    {
        var auditEntry = new AuditEntry
        {
            ActionName = actionName,
            Parameters = SanitizeParams(context.ActionArguments),
            Timestamp = DateTime.UtcNow,
            UserId = _userManager.GetUserId()
            // 注意：不含 StackTrace、CallerInfo
        };
        
        _auditLogger.LogAsync(auditEntry);
    }
}
```

**测试断言**：
```csharp
[Fact]
public async Task SanitizeParams_HidesSensitiveFields()
{
    var input = new { UserPwd = "secret123", Name = "张三" };
    var result = (dynamic)_filter.SanitizeParams(input);
    
    Assert.Equal("[REDACTED]", result.UserPwd);
    Assert.Equal("张三", result.Name);
}
```

---

#### 问题 12：匿名放行扩展点缺登记（`PermissionFilter.cs:44-45`）

**问题现状**：
- `AllowAnonymousAttribute` 直接跳过授权
- `YZHAnonymousAttribute` 另有一套匿名机制
- 两套匿名机制未统一

**改进建议**：

```csharp
// 1. 统一匿名机制，废弃 YZHAnonymousAttribute
// 2. 建立白名单登记表
public class AnonymousEndpointRegistry
{
    private readonly HashSet<string> _allowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/health",
        "/swagger",
        "/favicon.ico",
        "/api/System/Auth/login",
        "/api/System/Auth/captcha",
        // ... 其他允许的匿名端点
    };
    
    public bool IsAllowed(string path) => _allowedPaths.Contains(path);
    
    public void Register(string path, string reason)
    {
        _allowedPaths.Add(path);
        _logger.LogInformation("匿名端点已注册：{Path}，原因：{Reason}", path, reason);
    }
}

// PermissionFilter 中使用
public async ValueTask<bool> CheckPermission(HttpContext context, ...)
{
    var path = context.Request.Path.Value;
    if (_anonymousRegistry.IsAllowed(path))
        return true;
    
    // 原有权限校验逻辑...
}
```

**测试断言**：
```csharp
[Fact]
public async Task AnonymousEndpoints_AreBlockedByDefault()
{
    var unknownEndpoint = "/api/SecretData";
    var result = await _filter.CheckPermission(CreateContext(), null);
    Assert.False(result);
}
```

---

### 6.3 🟡 P2 级安全问题（改善性修复）

---

#### 问题 13：遗留/框架类型混用

**问题现状**：
- `ISOStandardController : YzhControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard>`
- `extern alias` 混用导致类型歧义

**改进建议**：
1. 新建 `Cert_ISOStandard` 实体（框架侧）
2. 数据迁移脚本
3. 旧控制器标记 `[Obsolete]` 并委托给新控制器

---

#### 问题 14：第三方依赖陈旧且无 SCA

**问题现状**：
- `SkiaSharp 2.88.8`（含 Linux/macOS 原生包）
- `SqlSugarCore 5.1.4.167`
- `Newtonsoft.Json 13.0.3`
- 无集中依赖清单、无漏洞扫描

**改进建议**：

```xml
<!-- 新增 Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="SqlSugarCore" Version="5.1.4.167" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt" Version="7.5.1" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <!-- ... 其他包版本集中管理 -->
  </ItemGroup>
</Project>

<!-- 新增 .github/workflows/sca.yml -->
name: Security Scanning
on:
  push:
    branches: [main]
  schedule:
    - cron: '0 2 * * 1'  # 每周一凌晨 2 点
jobs:
  owasp-dependency-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dependency-check/dependency-check-action@v12
        with:
          project: 'yzh-core'
          path: '.'
          format: 'HTML'
```

---

#### 问题 15：JWT 校验行为不一致

**问题现状**：
- 中间件 `ClockSkew=5min` vs `JwtHelper.ValidateToken` `ClockSkew=0`

**改进建议**：
```csharp
// Program.cs — 统一 ClockSkew
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.Zero  // 统一为 0，或统一为 5 分钟
        };
    });
```

---

#### 问题 16：权限校验状态标记无机制支撑

**改进建议**：
```csharp
// 1. 在 12-框架能力清单 中新增"当前实际行为"列
// 2. 引入 [Capability] 特性
[AttributeUsage(AttributeTargets.Class)]
public class CapabilityAttribute : Attribute
{
    public string Name { get; }
    public CapabilityStatus Status { get; }
    public string CurrentBehavior { get; }
}

// 3. 在 PermissionFilter 上标注
[Capability("权限校验", CapabilityStatus.InProgress, 
    "当前行为：所有已认证用户拥有全部接口权限（CheckPermission 硬编码 return true）")]
public class PermissionFilter : IAsyncActionFilter { }

// 4. CI 脚本校验
// scripts/ci/validate-capability-status.ps1
$capabilities = Get-ChildItem -Recurse -Filter "*.cs" | 
    Select-String -Pattern "\[Capability" | 
    Where-Object { $_.Line -match "InProgress|Planned" }

if ($capabilities.Count -gt 0)
{
    Write-Warning "发现未完成的能力实现"
    exit 1
}
```

---

#### 问题 17：配置驱动 UI 仅框架自身消费

**改进建议**：
```typescript
// 1. 新增最小接入示例
// docs/examples/config-driven-ui-minimal.md

// 2. 在框架层提供示例页面
// src/yzh.vue.core/src/examples/ConfigDrivenDemo.vue
<template>
  <YzhCrudPage
    api-client="system/user"
    :schema="userPageSchema"
  />
</template>

<script setup>
const userPageSchema = {
  title: '用户管理',
  entity: 'Sys_User',
  columns: [
    { prop: 'Name', label: '姓名' },
    { prop: 'OrgCode', label: '机构' },
  ]
}
</script>
```

---

#### 问题 18：工作流双套并存

**改进建议**：
```csharp
// 1. 明确标注旧工作流为弃用
[Obsolete("Sys_WorkFlow* 系列已弃用，请使用 Wf* 系列。将在 v2.0 移除。")]
public class Sys_WorkFlowController : YzhControllerBase<Sys_WorkFlow> { }

// 2. 在 12-框架能力清单 中标注为 🗑️
```

---

#### 问题 19：审计写入机制低效

**问题现状**：
- `BackgroundService` 主循环 `TryDequeue` else `Task.Delay(100)`（无信号量唤醒）
- `EnqueueAndTryFlush` 内 `_ = Task.Run(...)` 每次入队可能起线程
- 队列 `ConcurrentQueue` 无上限

**改进建议**：
```csharp
// 1. 使用 System.Threading.Channels
public class YzhAuditLogger : BackgroundService
{
    private readonly Channel<AuditEntry> _channel;
    
    public YzhAuditLogger()
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<AuditEntry>(options);
    }
    
    public void Enqueue(AuditEntry entry)
    {
        _channel.Writer.WriteAsync(entry).Wait();
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await FlushBatchAsync(new[] { entry });
        }
    }
}
```

---

#### 问题 20：缓存策略分散

**改进建议**：
```csharp
// 1. 统一缓存门面
public interface ICacheManager
{
    T Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? ttl = null);
    void Invalidate(string key);
    void InvalidateByPattern(string pattern);
}

// 2. 缓存键集中声明
public static class CacheKeys
{
    public const string DictItems = "dict:items";
    public const string UserPreferences = "user:preferences";
    public const string GridConfig = "grid:config";
}
```

---

#### 问题 21：改密/登出即时失效能力弱

**改进建议**：
```csharp
// 1. 修改密码后立即失效旧 Token
[HttpPost("change-password")]
public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
{
    var user = await _userManager.GetAsync(User.GetUserId());
    await _userManager.ChangePasswordAsync(user, request.NewPassword);
    await _tokenVersionService.IncrementAsync(user.Id);  // 关键！
    return Ok();
}

// 2. Token 验证时检查版本号
public async ValueTask<bool> CheckPermission(HttpContext context, ...)
{
    var token = context.GetToken();
    var userId = token.GetUserId();
    var tokenVersion = token.GetVersion();
    var userVersion = await _tokenVersionService.GetAsync(userId);
    
    if (tokenVersion < userVersion)
        return false;  // 或抛 401
}
```

---

#### 问题 22：导出非流式

**改进建议**：
```csharp
// 1. 改为流式导出
[HttpGet("export")]
public async Task<IActionResult> Export(FilterRequest request)
{
    var stream = await _exportService.ExportAsync<T>(request);
    return new FileStreamResult(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
    {
        FileDownloadName = $"export_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
    };
}

// 2. 大数据量走队列中心（异步导出）
[HttpPost("export-async")]
public async Task<IActionResult> ExportAsync(FilterRequest request)
{
    var taskId = await _queueCenter.EnqueueAsync("ExportTask", new { Entity = typeof(T).Name, Filter = request });
    return Ok(new { TaskId = taskId });
}
```

---

## 七、性能隐患分析

| # | 隐患 | 证据 | 影响 | 改进建议 |
|:--:|------|------|------|---------|
| 1 | 分页无上限 | `FilterRequest.cs:7`、`PagerOptions.cs:12` | 单请求可拉全表 | 服务端 `MaxPageSize` 钳制 |
| 2 | 分页两次往返，无缓存 | `SqlSugarDbOrm.cs:62-125` | 大表每页都做一次 COUNT | 大表改用近似计数/延迟计数 |
| 3 | 导出整体驻留内存 | `YzhControllerBase.cs:329` | 大表导出 OOM | 改为流式或走队列中心 |
| 4 | 审计写入机制低效 | `YzhAuditLogger.cs:14/23/99-103` | 线程池压力、日志延迟 | 改 `System.Threading.Channels` |
| 5 | 每次请求都做参数序列化 | `YzhAuditingFilter.cs:56` | 高频读接口也付审计成本 | 只审"写操作 + 敏感读" |
| 6 | 缓存策略分散 | 多个缓存实现并存 | 无统一失效契约 | 统一 `ICacheManager` 门面 |
| 7 | 改密/登出即时失效能力弱 | `TokenVersionService` 静态 TTL 30 天 | 安全事件无法立即踢出会话 | 改密/登出递增版本号 |

---

## 八、安全改进优先级汇总

### 立即修复（P0）

| # | 改进项 | 位置 | 类型 |
|:--:|--------|------|------|
| 1 | 实现接口级权限校验 + 数据范围 | `PermissionFilter.cs:114-138` | 安全 |
| 2 | 移除 CORS `AllowAll`，改配置化白名单 | `Program.cs:55-61/168` | 安全 |
| 3 | 移除硬编码默认密钥兜底 | `YzhWebBuilder.cs:68/79` | 安全 |
| 4 | `SortField` 白名单校验 | `SqlSugarDbOrm.cs:99-104` | 安全 |
| 5 | 统一软删除/有效标志过滤实现 | `SqlSugarDbOrm.cs:79-88` vs `:243-291` | 约定 |
| 6 | 补 `Code` 主键例外（无 Code 实体）通道 | `SqlSugarDbOrm.cs:152-215` | 约定 |
| 7 | 框架 → 业务反向依赖修复 | `YzhCrudPage.vue` | 结构 |
| 8 | 通用能力（System 域控制器）从宿主迁入能力层 | `YZH.Core.Web/Controllers/System/*` → `YZH.Core.Api` | 结构 |
| 9 | 前端 HTTP 入口归一 | `yzh.vue.core/src/utils/http.ts` | 前后端关系 |

### 短期规划（P1）

| # | 改进项 | 位置 |
|:--:|--------|------|
| 10 | 操作日志落库 | 清单 §3.3 能力 10 |
| 11 | 字典项加载 | 清单 §3.3 能力 8 |
| 12 | 系统参数双表决策 | 清单 §3.3 能力 9 |
| 13 | 分页 `MaxPageSize` 钳制 | `FilterRequest.cs:7`、`PagerOptions.cs:12` |
| 14 | 契约生成与漂移校验 | 前后端 |
| 15 | 约定外控制器登记白名单 | `MenuController` / `AuthController` / `ApiSyncController` |
| 16 | 上传/导入统一校验策略 | `YzhControllerBase.cs:370-374` |
| 17 | 审计写入改有界 Channel | `YzhAuditLogger.cs` |
| 18 | 缓存统一门面 | `CacheManager` / `INoSql` / `DictService` |
| 19 | 假参数清零 | `EntityService.cs:134-195` |
| 20 | 更改密码 + Token 即时失效 | `AuthController` / `TokenVersionService.cs:23` |
| 21 | 导出改流式 | `YzhControllerBase.cs:329` |
| 22 | 前端基类接入率治理 | `pages/**` |
| 23 | `Sys_WorkFlow*` 正式标注弃用 | 清单 §3.4 |

### 中期规划（P2）

| # | 改进项 |
|:--:|--------|
| 24 | 能力清单加 `code_refs` + CI 一致性校验 |
| 25 | `BizNamingRules` 黑名单与实体清单同源生成 |
| 26 | 依赖集中清单 + SCA 扫描 |
| 27 | 前端树工具合并 |
| 28 | 框架层内部四工程准入清单写入架构文档 |
| 29 | 审计脱敏字段字典 + 单测 |
| 30 | 退役 `YZH.Core.CodeGenerators` 与遗留元数据 |

---

## 九、Docker 部署安全清单

### 9.1 敏感文件拦截

| 文件类型 | 拦截方式 | 说明 |
|---------|---------|------|
| `appsettings.Production.json` | Docker Secrets | 生产配置 |
| `*.key` / `*.cert` | Docker Secrets | 证书文件 |
| `logs/` | 只读挂载 | 日志输出 |
| `data/` | 持久化卷 | 数据库文件 |
| `Assets/EntityConfigs/*.json` | 权限限制 | 实体配置 |

### 9.2 Dockerfile 安全配置

```dockerfile
# 多阶段构建
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["YZH.Web.Core/YZH.Web.Core.csproj", ""]
RUN dotnet restore
COPY . .
RUN dotnet publish "YZH.Web.Core.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 非 root 用户运行
RUN adduser --disabled-password --gecos '' appuser
USER appuser

# 健康检查
HEALTHCHECK --interval=30s --timeout=3s \
  CMD curl -f http://localhost:5000/api/health || exit 1

EXPOSE 5000
ENTRYPOINT ["dotnet", "YZH.Web.Core.dll"]
```

### 9.3 docker-compose.yml 安全配置

```yaml
version: '3.8'
services:
  yzh-core:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - YZH_EncryptionKey=${ENCRYPTION_KEY}
      - YZH_JwtSecret=${JWT_SECRET}
    secrets:
      - encryption_key
      - jwt_secret
    volumes:
      - ./logs:/app/logs:rw
      - ./data:/app/data:rw
    security_opt:
      - no-new-privileges:true
    read_only: true
    tmpfs:
      - /tmp
    
secrets:
  encryption_key:
    file: ./secrets/encryption_key.txt
  jwt_secret:
    file: ./secrets/jwt_secret.txt
```

---

## 十、总结

### 10.1 已明确的设计意图

| 设计决策 | 意图 | 评估报告原判定 | 修正 |
|---------|------|--------------|------|
| 依赖方向 | 开发期宿主聚合模式 | P0 依赖倒挂 | P3 文档澄清 |
| 数据权限 | 各项目自定义实现 | P0 未实现 | 框架提供接口点 |
| CORS | 需要外网访问 | P0 全开放 | 配置化白名单 |

### 10.2 需要立即修复的安全问题（P0）

1. ✅ 权限校验实现（角色-接口映射已完成）
2. ✅ 移除硬编码密钥兜底
3. ✅ 排序字段白名单
4. ✅ 前端 HTTP 入口归一
5. ⏳ 软删除过滤统一
6. ⏳ 表名拼接安全化

### 10.3 文档修正建议

1. 在架构总纲中明确"开发期宿主"与"生产期宿主"的区别
2. 将依赖方向问题从 P0 降为 P3（文档澄清）
3. 新增"数据安全权限实现规范"文档
4. 补充"框架层内部分层准入清单"

### 10.4 框架整体评价

**正面**：
- 框架设计理念清晰（5 大原则 + 黄金判断法则）
- 命名规范、约定体系比较完整
- 已实现的能力质量不错

**问题**：
1. 安全上有很多"假实现"——代码写了但实际没生效
2. 前后端契约靠人工对齐，无生成和校验
3. 约定执行率很低，无检测机制

**判定**：架构设计密度高、理念清晰，当前处于"能力落地不均衡 + 工程承载结构待理顺"阶段。建议按 P0（9 项）→ P1（14 项）顺序收敛。

---

*本文档基于第二轮架构评估报告生成，结合了架构设计意图与安全现状。*
*所有结论均基于代码事实，代码未读到的内容已列为待核项，不作结论。*
