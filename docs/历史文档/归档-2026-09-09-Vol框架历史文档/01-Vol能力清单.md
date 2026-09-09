---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_834e67fc8c9e11f19986525400287e28
    ReservedCode1: dgCqcwXOYUMCh2p0hdx9pF4cQ82hPdiKTzY03gPmZ6CTXgvdz4G6q0nLu21yOCIswKCF6ZuEw4CYBBH3Xwd4EN0AZKZ77d20Z1btbkOEZt6ncr/8skoVEGcCGMWxNN7jrUvdQa69AvVwiVxDB/z0YgfmYkCtk3WIjZpd+bGboM6WlZ/waGzrqx97JtI=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_834e67fc8c9e11f19986525400287e28
    ReservedCode2: dgCqcwXOYUMCh2p0hdx9pF4cQ82hPdiKTzY03gPmZ6CTXgvdz4G6q0nLu21yOCIswKCF6ZuEw4CYBBH3Xwd4EN0AZKZ77d20Z1btbkOEZt6ncr/8skoVEGcCGMWxNN7jrUvdQa69AvVwiVxDB/z0YgfmYkCtk3WIjZpd+bGboM6WlZ/waGzrqx97JtI=
---

# 01 - Vol 能力清单

**版本**：V1  
**日期**：2026-07-31  
**状态**：正式发布  
**数据来源**：vol.api（EF Core 8.0）和 vol.api.sqlsugar（SqlSugar 5.1）源码探索

## 0. 项目位置

| 项目 | 基路径 |
|---|---|
| vol.api（EF Core 版） | /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api/ |
| vol.api.sqlsugar（SqlSugar 版） | /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api.sqlsugar/ |

## 1. 项目结构

两个版本共享同构分层架构。

| 项目 | .csproj 路径 | 用途 |
|---|---|---|
| VOL.Entity | `VOL.Entity/VOL.Entity.csproj` | 实体定义、DomainModels、SystemModels、AttributeManager |
| VOL.Core | `VOL.Core/VOL.Core.csproj` | 核心基础设施：基类、中间件、过滤器、DBManager、日志等 |
| VOL.Builder | `VOL.Builder/VOL.Builder.csproj` | 代码生成器：IRepositories/IServices/Repositories/Services |
| VOL.Sys | `VOL.Sys/VOL.Sys.csproj` | 系统管理业务层（用户、角色、菜单、字典等） |
| VOL.MES | `VOL.MES/VOL.MES.csproj` | MES 业务模块（示例） |
| VOL.WebApi | `VOL.WebApi/VOL.WebApi.csproj` | WebApi 启动入口（Program/Startup、Controllers） |

### VOL.Core 目录差异对比

| 目录 | vol.api (EF Core) | vol.api.sqlsugar (SqlSugar) | 注 |
|---|---|---|---|
| EFDbContext | ✅ (含 BaseDbContext、VOLContext、EFLoggerProvider) | ❌ | EF Core 专用 |
| DbContext | ❌ | ✅ (含 VOLContext、BaseDbContext) | SqlSugar 专用 |
| DBManager | ✅ (DBConnectionAttribute、DBServerProvider、DbName、Partial) | ✅ (额外含 DbManger、SqlSugarDbType、SqlSugarExtension、SqlSugarRegister) | 结构不同 |
| Dapper | ✅ (ISqlDapper、SqlDapper、Guid 处理器) | ❌ | EF 版额外引入 Dapper 做手写 SQL |
| Tenancy | ✅ | ✅ | 内容相同 |
| 其余目录 | 基本一致 | 基本一致 | BaseProvider、Controllers、Filters、Middleware、Services 等 |

## 2. 版本差异速查

| 维度 | vol.api (EF Core) | vol.api.sqlsugar (SqlSugar) |
|---|---|---|
| 目标框架 | net8.0 | net10.0 |
| ORM | Entity Framework Core 8.0（多库：SQL Server / MySQL / PostgreSQL / Oracle / DM） | SqlSugarCore 5.1.4.214（多库） |
| 手写 SQL 辅助 | Dapper 2.1.35 | 原生 SqlSugar `SqlQueryable` |
| 审计日志写库方式 | `DBServerProvider.SqlDapper.BulkInsert` | `DBServerProvider.SqlDapper.BulkInsert`（均用 Dapper 批量写入） |
| 业务实体 | 含 CertPlatform（体系认证平台）定制实体（Enterprise、CertificationBody、AuditTask 等 30+） | 仅标准 Vol 框架实体（Sys_User、Sys_Role 等） |
| DbManger 实现 | 无独立 DbManger；通过 `DBServerProvider.GetConnectionString` | `DbManger` 静态类：`SqlSugarScope` 单例 + `SqlSugarRegister` 注册 |

## 3. ServiceBase 钩子委托

**文件路径**（两版本相同）：`VOL.Core/BaseProvider/ApplicationServiceBase.cs`
**类签名**：`public abstract class ApplicationServiceBase<TEntity, TRepository> where TEntity : BaseEntity where TRepository : IRepository<TEntity>`

### 查询相关

| 委托名 | 签名 | 说明 | 触发时机 |
|---|---|---|---|
| `QueryRelativeList` | `Action<List<SearchParameters>>` | 修改搜索条件 | 查询前 |
| `QueryRelativeExpression` | `Func<IQueryable<TEntity>, IQueryable<TEntity>>` | 表达式修改 | 查询前 |
| `OrderByExpression` | `Expression<Func<TEntity, Dictionary<object, QueryOrderBy>>>` | 自定义排序 | 查询前 |
| `SummaryExpress` | `Func<IQueryable<TEntity>, object>` | 页面统计/求和/平均值 | 查询后（同步） |
| `SummaryExpressAsync` | `Func<IQueryable<TEntity>, Task<object>>` | 页面统计 | 查询后（异步） |
| `GetPageDataOnExecuted` | `Action<PageGridData<TEntity>>` | 查询后处理 | 查询后（同步） |
| `GetPageDataOnExecutedAsync` | `Func<PageGridData<TEntity>, Task>` | 查询后处理 | 查询后（异步） |

### 新建相关

| 委托名 | 签名 | 说明 | 触发时机 |
|---|---|---|---|
| `AddOnExecute` | `Func<SaveModel, WebResponseContent>` | 新建方法调用前 | 方法入口 |
| `AddOnExecuting` | `Func<TEntity, object, WebResponseContent>` | 新建保存前（主表+明细） | 保存前 |
| `AddOnExecutingAsync` | `Func<TEntity, object, Task<WebResponseContent>>` | 新建保存前（异步） | 保存前 |
| `AddOnExecuted` | `Func<TEntity, object, WebResponseContent>` | 新建保存后（已有 DbContext 事务） | 保存后 |
| `AddOnExecutedAsync` | `Func<TEntity, object, Task<WebResponseContent>>` | 新建保存后（异步） | 保存后 |

### 更新相关

| 委托名 | 签名 | 说明 | 触发时机 |
|---|---|---|---|
| `UpdateOnExecute` | `Func<SaveModel, WebResponseContent>` | 更新方法调用前 | 方法入口 |
| `UpdateOnExecuting` | `Func<TEntity, object, object, List<object>, WebResponseContent>` | 更新保存前（主表+新增明细+更新明细+删除明细 Key） | 保存前 |
| `UpdateOnExecutingAsync` | 同上异步版 | — | 保存前 |
| `UpdateOnExecuted` | 同上签名 | 更新保存后（已有 DbContext 事务） | 保存后 |
| `UpdateOnExecutedAsync` | 同上异步版 | — | 保存后 |

### 删除相关

| 委托名 | 签名 | 说明 | 触发时机 |
|---|---|---|---|
| `DelOnExecuting` | `Func<object[], WebResponseContent>` | 删除前 | 删除前 |
| `DelOnExecutingAsync` | `Func<object[], Task<WebResponseContent>>` | 删除前（异步） | 删除前 |
| `DelOnExecuted` | `Func<object[], WebResponseContent>` | 删除后（已有 DbContext 事务） | 删除后 |
| `DelOnExecutedAsync` | 同上异步版 | — | 删除后 |

### 审核相关

| 委托名 | 签名 | 说明 | 触发时机 |
|---|---|---|---|
| `AuditOnExecuting` | `Func<List<TEntity>, WebResponseContent>` | 审核前 | 审核前 |
| `AuditOnExecuted` | `Func<List<TEntity>, WebResponseContent>` | 审核后 | 审核后 |
| `AntiAuditOnExecuting` | `Func<TEntity, WebResponseContent>` | 反审核前 | 反审核前 |
| `AntiAuditOnExecuted` | `Func<TEntity, WebResponseContent>` | 反审核后 | 反审核后 |

### 导入导出相关

| 委托名 | 签名 | 说明 | 触发时机 |
|---|---|---|---|
| `ImportOnExecuting` | `Func<List<TEntity>, WebResponseContent>` | 导入保存前 | 导入前 |
| `ImportOnExecuted` | `Func<List<TEntity>, WebResponseContent>` | 导入保存后 | 导入后 |
| `ExportColumns` | `Expression<Func<TEntity, object>>` | 指定导出列 | 导出前 |
| `ImportOnReadCellValue` | `Func<string, ExcelWorksheet, ExcelRange, int, int, string>` | 导入时自定义单元格值读取 | 导入中 |

### 关键属性

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsMultiTenancy` | `bool` | 是否开启多租户（默认 `true`） |
| `QuerySql` | `string` | 自定义原生 SQL（绕过表达式，必须返回全部列） |
| `MultipleTableEntity` | `ApplicationServiceBaseMultipleTableEntity` | 主从明细表编辑 |
| `DownLoadTemplateColumns` | `Expression<Func<TEntity, object>>` | 导入模板列定义 |
| `ExcelHeaderMap` | `Expression<Func<TEntity, Dictionary<object, string>>>` | 导入字段名称映射 |
| `ImportStartRowIndex` | `int` | 导入起始行（默认 1） |
| `Limit` | `int` | 导出最大行数（0 不限制） |

## 4. ApiBaseController 路由

**文件路径**：`VOL.Core/Controllers/Basic/ApiBaseController.cs`
**类签名**：`public class ApiBaseController<IServiceBase> : VolController`
**类特性**：`[JWTAuthorize, ApiController]`
**路由机制**：所有 Action 通过反射 `InvokeService(methodName, parameters)` 动态转发到 Service 层。

| Action | Route | HTTP | 权限要求 |
|---|---|---|---|
| GetPageData | `getPageData` | POST | `ActionPermissionOptions.Search` |
| GetPageDataAsync | `getPageDataAsync` | POST | `ActionPermissionOptions.Search` |
| GetDetailPage | `GetDetailPage` | POST | `ActionPermissionOptions.Search` |
| GetDetailPageAsync | `getDetailPageAsync` | POST | `ActionPermissionOptions.Search` |
| Upload | `Upload` | POST | `Upload \| Add \| Update` |
| UploadAsync | `uploadAsync` | POST | `Upload \| Add \| Update` |
| DownLoadTemplate | `DownLoadTemplate` | GET | `Import` |
| DownLoadTemplateAsync | `downLoadTemplateAsync` | GET | `Import` |
| Import | `Import` | POST | `Import` |
| ImportAsync | `importAsync` | POST | `Import` |
| Export | `Export` | POST | `Export` |
| ExportAsync | `exportAsync` | POST | `Export` |
| Del | `Del` | POST | `Delete` |
| DelAsync | `delAsync` | POST | `Delete` |
| Audit | `Audit` | POST | `Audit` |
| AuditAsync | `auditAsync` | POST | `Audit` |
| Add | `Add` | POST | `Add` |
| AddAsync | `addAsync` | POST | `Add` |
| Update | `Update` | POST | `Update` |
| UpdateAsync | `updateAsync` | POST | `Update` |

### VolController

| 属性 | 值 |
|---|---|
| **文件路径** | `VOL.Core/Controllers/Basic/VolController.cs` |
| **类签名** | `[JWTAuthorize, ApiController] public class VolController : Controller` |
| **关键方法** | `JsonNormal(object data, ...)` —— 原格式 JSON 返回（关闭驼峰），带 `LongCovert` 转换器处理 long 类型 |

## 5. 中间件与过滤器

### 5.1 中间件清单

| 中间件 | 文件路径 | 用途 | 状态 |
|---|---|---|---|
| ExceptionHandlerMiddleWare | `VOL.Core/Middleware/ExceptionHandlerMiddleWare.cs` | 全局异常捕获 + 审计日志写入 | 已启用 |
| HttpRequestMiddleware | `VOL.Core/Middleware/HttpRequestMiddleware.cs` | 请求响应头处理（`vol_exp` 动态刷新 Token 标记） | 已启用 |
| ActionLog（Attribute） | `VOL.Core/Middleware/ActionLog.cs` | Action 审计日志标记特性 | 已启用 |

### 5.2 ExceptionHandlerMiddleWare 版本差异

| 维度 | vol.api (EF Core) | vol.api.sqlsugar (SqlSugar) |
|---|---|---|
| 管道注册方式 | `app.UseMiddleware<ExceptionHandlerMiddleWare>()` | 相同 |
| 正常请求日志写入 | `Logger.Add(log?.LogType, null, null, null, status: LoggerStatus.Info)`（无 Write 判断） | `if (log != null && log.Write)` 条件判断后才写日志 |
| 异常信息 | message 包含 `StackTrace` 和 `InnerException` | message 不含 StackTrace |

### 5.3 ActionPermissionFilter

| 属性 | 值 |
|---|---|
| **文件路径** | `VOL.Core/Filters/ActionPermissionFilter.cs` |
| **类签名** | `public class ActionPermissionFilter : IAsyncActionFilter` |
| **权限检查机制** | |
| 1. AllowAnonymous | 放行 |
| 2. 超级管理员 | 放行 (`UserContext.Current.IsSuperAdmin`) |
| 3. 演示环境全局过滤 | `AppSetting.GlobalFilter` 限制增删改 |
| 4. SysController 表名解析 | 优先读 `PermissionTableAttribute`，回退控制器名 |
| 5. RoleIds 检查 | 限制特定角色 |
| 6. TableActions 数组检查 | `CheckPermission(actionsToCheck, table)` 查用户权限数组 |
| 7. 明细表回退主表权限 | `TableColumnContext.TableInfo` 查找明细表对应的主表权限 |
| **状态** | 已启用 |

### 5.4 过滤器文件清单

| 过滤器文件 | 用途 |
|---|---|
| `JWTAuthorize.cs` | JWT 认证特性 `[JWTAuthorize]`（继承 `AuthorizeAttribute`） |
| `ActionPermissionAttribute.cs` | Action 权限声明特性 |
| `ActionPermissionFilter.cs` | 权限检查过滤器（`IAsyncActionFilter`） |
| `ActionPermissionRequirement.cs` | 权限需求实体（TableName / TableActions / SysController / RoleIds） |
| `ApiActionPermissionAttribute.cs` | API 权限特性 `[ApiActionPermission]` |
| `ApiAuthorizeFilter.cs` | API 授权过滤器 |
| `ApiTaskAttribute.cs` | API 任务特性 |
| `ActionExecuteFilter.cs` | Action 执行过滤器 |
| `FixedTokenAttribute.cs` | 固定 Token 特性 |
| `ServiceFunFilter.cs` | Service 功能过滤器 |

## 6. 数据访问

### 6.1 ORM 对比（EF Core vs SqlSugar）

| 维度 | vol.api (EF Core) | vol.api.sqlsugar (SqlSugar) |
|---|---|---|
| ORM 框架 | Entity Framework Core 8.0 | SqlSugarCore 5.1.4.214 |
| NuGet 包 | `Microsoft.EntityFrameworkCore.SqlServer` / `Npgsql.EntityFrameworkCore.PostgreSQL` / `Pomelo.EntityFrameworkCore.MySql` / `Oracle.EntityFrameworkCore` / `DM.Microsoft.EntityFrameworkCore` | `SqlSugarCore` 单一包 + 各数据库 ADO.NET 驱动 |
| 审计日志写库 | Dapper `BulkInsert` 到 `Sys_Log` 表 | 同样用 Dapper `BulkInsert` |
| 手写 SQL | Dapper (`repository.DbContext.Database.ExecuteSqlCommand` / `repository.DbContext.Set<T>().FromSql`) | SqlSugar `ISqlSugarClient.SqlQueryable` + Dapper 兜底 |

### 6.2 DbContext

| 组件 | vol.api (EF Core) | vol.api.sqlsugar (SqlSugar) |
|---|---|---|
| DbContext | `VOLContext : BaseDbContext` | `VOLContext : BaseDbContext` |
| DbContext 文件 | `VOL.Core/EFDbContext/VOLContext.cs` | `VOL.Core/DbContext/VOLContext.cs` |
| 连接获取 | `DBServerProvider.GetConnectionString(null)` | `DbManger.SqlSugarClient`（`SqlSugarScope` 单例） |
| 多库支持 | `OnConfiguring` 按 `Const.DBType.Name` 分支：MySql / PgSql / DM / Oracle / MsSql | `DbManger.GetDbType()` 按 `Const.DBType.Name` 分支 + `SqlSugarRegister.GetSysConnectionConfig()` |
| 实体发现 | `OnModelCreating` 扫描所有项目的 `BaseEntity` 子类反射注册 | SqlSugar 无 Code First —— 通过 `MappingConfiguration` 目录下 MapConfig 手动映射 |
| 跟踪控制 | `QueryTrackingBehavior.NoTracking`（默认禁用） | N/A（SqlSugar 无跟踪概念） |
| Oracle/DM 适配 | 表名/列名自动大写 + `Guid` → `string` 转换 | 通过 `SqlSugarDbType` 适配 |

### 6.3 IRepository / IServices

| 组件 | 说明 |
|---|---|
| `VOL.Core/BaseInterface/IServices.cs` | 空接口 `public interface IServices { }` |
| 仓储/服务层 | 由 `VOL.Builder` 代码生成器生成具体 `IRepository` / `IService` / `Repository` / `Service` |
| 仓储基类 | vol.api: EF Core 的 `IRepository<T>`；vol.api.sqlsugar: SqlSugar 的 `IRepository<T>` |

### 6.4 多租户

| 属性 | 值 |
|---|---|
| **文件路径**（两版本相同） | `VOL.Core/Tenancy/TenancyManager.cs` |
| **类签名** | `public static class TenancyManager<T> where T : class` |
| **核心方法** | `GetSearchQueryable(string tableName)` → 返回 SQL WHERE 条件字符串 |
| **当前状态** | 框架骨架已就绪，租户隔离逻辑被注释（默认返回 `null`）；`ApplicationServiceBase.IsMultiTenancy = true` 默认启用开关 |
| **扩展方式** | 在 `switch(tableName)` 中添加 `case "表名": return "WHERE CreateID=xxx";` 实现表级租户数据隔离 |

## 7. 日志与审计

### 7.1 Logger 实现

| 属性 | 值 |
|---|---|
| **文件路径** | `VOL.Core/Services/Logger.cs` |
| **设计模式** | 静态类 + `ConcurrentQueue<Sys_Log>` 异步队列 + 后台线程每 1 秒批量写库 |
| **日志级别** | `LoggerStatus` 枚举：`Info(3)` / `Success(1)` / `Error(2)` |
| **便捷方法** | `Logger.Info()` / `Logger.OK()` / `Logger.Error()` / `Logger.Add()` / `Logger.AddAsync()` |
| **写库方式** | `DBServerProvider.SqlDapper.BulkInsert(queueTable, "Sys_Log", ...)`（两版本均使用 Dapper） |
| **错误兜底** | `WriteFile()` 写磁盘文件 `Logger\Queue\` |

### 7.2 Sys_Log 字段

| 字段 | 说明 |
|---|---|
| LogType | 日志类型 |
| RequestParameter | 请求参数 |
| ResponseParameter | 响应参数 |
| ExceptionInfo | 异常信息 |
| Success | 是否成功 |
| BeginDate / EndDate / ElapsedTime | 耗时记录 |
| UserIP / ServiceIP | IP 地址 |
| BrowserType | 浏览器类型 |
| Url | 请求地址 |
| User_Id / UserName / Role_Id | 用户信息 |

### 7.3 ActionExecutingLogger

| 属性 | 值 |
|---|---|
| **文件路径** | `VOL.Core/Services/ActionExecutingLogger.cs` |
| **类名** | `ActionObserver` |
| **字段** | `RequestDate`（Action 执行开始时间）、`IsWrite`（防止重复写日志）、`HttpContext` |

## 8. 其他能力模块速查

| 模块 | 路径 | 说明 |
|---|---|---|
| 缓存管理 | `VOL.Core/CacheManager/` | Redis（CSRedisCore）缓存 |
| 消息队列 | `VOL.Core/KafkaManager/` | Confluent.Kafka 消息队列 |
| 定时任务 | `VOL.Core/Quartz/` | Quartz.NET 3.4.0 |
| 工作流 | `VOL.Core/WorkFlow/` | 审批流程引擎 |
| 配置管理 | `VOL.Core/Configuration/` | AppSetting 读取 |
| 用户上下文 | `VOL.Core/UserManager/` | `UserContext.Current` |
| 自动映射 | AutoMapper（EF 版 6.2.2 / SqlSugar 版 8.0.0） | 对象映射 |
| DI 容器 | Autofac（两版本均 8.0.0） | `AutofacManager`、`IDependency` |
| Excel | EPPlus.Core 1.5.4 | 导入导出 |
| 图形 | SkiaSharp 2.88.7 | 图片处理 |
| JWT | `System.IdentityModel.Tokens.Jwt` 6.35.0 | Token 认证 |
| 雪花 ID | Yitter.IdGenerator 1.0.14 | 分布式 ID 生成 |
| 国密 | Arric.Crypto.SM 1.1.2 | SM2/SM3/SM4 加密 |
| 动态 LINQ | System.Linq.Dynamic.Core 1.3.5 | 动态查询表达式 |

## 9. YZH 改造相关要点

- **ServiceBase 的钩子委托模式**：YZH 可以继续用，不需修改。钩子委托是 Vol 框架最核心的扩展点，覆盖了 CRUD 全生命周期。
- **BaseEntity 是空基类**：YZH 可以在不破坏现有体系的情况下扩展（新增 Code / 审计字段 / 辅助方法）。
- **ExceptionHandlerMiddleWare 是中间件**：YZH 应使用 `IAsyncActionFilter` 替代装饰器中间件，与 Vol 现有 `ActionPermissionFilter` 风格一致。
- **多租户骨架已就绪但未启用**：`TenancyManager<T>` 的隔离逻辑被注释，默认返回 `null`。YZH 无需重新设计多租户架构，只需在需要时实现表级 WHERE 条件。
- **认证平台业务实体仅在 vol.api 中**：如果 YZH 选型锁定 SqlSugar，需将 30+ 个 CertPlatform 业务实体（Enterprise、CertificationBody、AuditTask 等）从 vol.api 迁移到 vol.api.sqlsugar。
*（内容由AI生成，仅供参考）*
