# YZHServiceBase 设计文档 — 统一异常兜底 + 多场景基类体系

> **版本**: V5.0  
> **位置**: `YZH-Framework/YZH.Core/`  
> **性质**: YZH 架构核心 — 跨项目可复用  
> **核心目标**: 统一异常兜底、友好错误输出、减少重复 try-catch、声明式校验

---

## 一、设计目标

### 1.1 要解决的核心问题

| 问题 | 严重程度 | 说明 |
|------|---------|------|
| **异常直接暴露给前端** | ★★★★★ | 后端报异常时前端直接显示原始堆栈、SQL 片段、连接字符串等技术涉密信息 |
| **try-catch 散乱重复** | ★★★★☆ | 每个 Service 的 Add/Update/Del 各自手写 try-catch，catch 逻辑不一致：有的抛 `ex.Message`、有的抛 `ex.InnerException?.Message`、有的直接抛 `ex.ToString()` |
| **Vol 事务兜底不足** | ★★★★☆ | Vol 的 `RepositoryBase.DbContextBeginTransaction` 在非开发环境下只返回 `"处理异常"` 四个字，丢失了所有错误上下文 |
| **校验逻辑分散** | ★★★☆☆ | 每个 Service 各自手写唯一性校验代码，`UniqueValidationExtensions` 有 LINQ 翻译 Bug |
| **无统一生命周期** | ★★★☆☆ | Vol 只有 delegate 方式的钩子（`AddOnExecuting`/`AddOnExecuted`），没有 virtual 方法 |
| **查询视图与保存实体混用** | ★★☆☆☆ | 查询需要 JOIN 翻译字段，保存用实体，当前基类没有区分 |
| **错误提示不分级** | ★★★☆☆ | 业务校验失败和系统异常用同样的方式返回，前端无法区分"用户操作错误"和"系统故障" |

### 1.2 设计原则

1. **异常绝不裸奔**：所有 Service 方法的异常必须在基类层统一捕获，返回给前端的必须是友好提示，绝不包含 SQL/堆栈/连接字符串
2. **分级处理**：区分"业务校验失败"（用户可修正）和"系统异常"（需排查），前端用不同方式展示
3. **声明式优先**：实体通过 `[UniqueField]`、`[QueryView]` 等特性声明行为，零代码即可校验
4. **模板方法**：基类提供默认实现（校验、生命周期、异常兜底），子类按需覆写 virtual 方法
5. **Validator 只校验不保存**：Validator 只执行 `SELECT COUNT`，绝不执行 INSERT/UPDATE/DELETE
6. **SQL 替代 LINQ**：用 Dapper 执行原生 SQL 做唯一性校验，避免 EF Core 表达式树翻译问题
7. **多场景多基类**：不搞一个基类包打天下，按业务场景提供专用基类

### 1.3 核心理念

```
═══════════════════════════════════════════════════════════════
  前端用户看到的：
    "机构编号「CB2026」已存在，请使用其他值"
    "该标准下有 12 个条款，请先删除条款"
    "操作失败，请联系管理员（错误码：ERR-20260825-A1B2C3）"

  前端用户永远看不到的：
    "MySqlConnector.MySqlException: Duplicate entry 'CB2026' for key 'uk_cb_code'"
    "SQL: INSERT INTO cert_certification_body..."
    "at VOL.Core.BaseProvider.RepositoryBase..."
    "Server=yzh-mysql;Port=3307;Database=yzh_cert..."
═══════════════════════════════════════════════════════════════
```

---

## 二、架构定位

### 2.1 多基类继承体系

```
VOL.Core/ServiceBase<TEntity, TRepository>                   ← Vol 框架基类（不动）
    ↑ 继承
YZH.Core/YZHServiceBase<TEntity, TRepository>                ← 通用抽象根
  │ 职责：统一异常兜底 + 校验管道 + 6 生命周期钩子 + QueryView
  │ 不含：场景特化逻辑
  │
  ├── YZHTableServiceBase<TEntity, TRepository>               ← 单表 CRUD（90% 场景）
  │     重写 GetPageData：QueryView 视图查询
  │     新增钩子：OnGetPageDataBefore / OnGetPageDataAfter
  │
  ├── YZHTreeTableServiceBase<TEntity, TRepository>           ← 左树右表
  │     新增方法：GetTreeData()
  │     重写 GetPageData：自动注入 treeKey 过滤
  │     新增钩子：OnTreeSelect(treeKey)
  │
  ├── YZHLinkTableServiceBase<TMain, TLink>                   ← 关联表（双泛型）
  │     新增方法：GetLinkedItems / ToggleLink / BatchSetLinks
  │     不走标准 Add/Update/Del，是勾选即保存模式
  │
  └── YZHTreeServiceBase<TEntity, TRepository>                ← 纯树形管理
        重写 GetPageData → GetTreeList：返回树形结构
        内置级联删除校验 + 移动节点校验
```

### 2.2 文件结构

```
YZH-Framework/YZH.Core/
├── Exception/
│   ├── YZHException.cs                 ← 业务异常（友好提示）
│   ├── YZHDbException.cs               ← 数据库异常（脱敏后提示）
│   ├── ExceptionSanitizer.cs           ← 异常消息脱敏器
│   └── YZHExceptionFilter.cs           ← 全局异常过滤器（Controller 层兜底）
├── Validation/
│   ├── EntityValidationHandler.cs       ← 校验处理器（只做校验）
│   ├── EntityValidationResult.cs        ← 校验结果
│   └── ValidationAction.cs              ← 枚举
├── Attributes/
│   ├── QueryViewAttribute.cs            ← 查询视图声明特性
│   └── TreeSourceAttribute.cs           ← 左树右表数据来源特性
├── YZHServiceBase.cs                    ← 通用抽象根
├── YZHTableServiceBase.cs               ← 单表 CRUD 专用
├── YZHTreeTableServiceBase.cs           ← 左树右表专用
├── YZHLinkTableServiceBase.cs           ← 关联表专用
└── YZHTreeServiceBase.cs                ← 纯树形管理专用
```

---

## 三、异常分级与脱敏体系

### 3.1 异常分类

| 类别 | 类型 | 前端展示 | 日志记录 | 典型场景 |
|------|------|---------|---------|---------|
| **业务校验失败** | `EntityValidationResult.Error()` | 直接显示错误消息 | Info 级 | 唯一性冲突、必填为空、关联数据阻止删除 |
| **业务异常** | `YZHException(message)` | 直接显示错误消息 | Warn 级 | 业务规则不满足（如：编号不允许修改） |
| **数据库异常** | `YZHDbException(message)` | 友好提示 + 错误码 | Error 级 + 完整堆栈 | 唯一键冲突、外键约束、连接超时 |
| **系统异常** | 原始 `Exception` | "操作失败，请联系管理员" + 错误码 | Error 级 + 完整堆栈 | Null 引用、序列化失败、未预期异常 |

### 3.2 异常类定义

```csharp
// YZH.Core/Exception/YZHException.cs
namespace YZH.Core.Exception
{
    /// <summary>
    /// 业务异常。消息内容可以直接展示给前端用户。
    /// 使用：throw new YZHException("机构编号不允许修改");
    /// </summary>
    public class YZHException : System.Exception
    {
        /// <summary>错误码（自动生成，格式：ERR-yyyyMMdd-NNNNNN）</summary>
        public string ErrorCode { get; }

        public YZHException(string message) : base(message)
        {
            ErrorCode = $"ERR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
        }

        public YZHException(string message, System.Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = $"ERR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
        }
    }

    /// <summary>
    /// 数据库异常（脱敏后展示给前端）。
    /// 由 ExceptionSanitizer 从原始 DbException 转换而来。
    /// </summary>
    public class YZHDbException : YZHException
    {
        public string OriginalExceptionType { get; }
        public YZHDbException(string friendlyMessage, System.Exception original)
            : base(friendlyMessage, original)
        {
            OriginalExceptionType = original?.GetType().Name ?? "Unknown";
        }
    }
}
```

### 3.3 异常脱敏器

```csharp
// YZH.Core/Exception/ExceptionSanitizer.cs
namespace YZH.Core.Exception
{
    /// <summary>
    /// 异常消息脱敏器。
    ///
    /// 核心职责：
    /// 1. 识别数据库异常类型，匹配友好提示
    /// 2. 过滤敏感信息（SQL/连接字符串/表名/堆栈）
    /// 3. 生成错误码用于日志关联
    ///
    /// 绝不向前端暴露：
    /// - SQL 语句（SELECT/INSERT/UPDATE/DELETE ...）
    /// - 连接字符串（Server=/Password=/Database=）
    /// - 表结构信息（列名、约束名、索引名）
    /// - 堆栈信息（at VOL.Core...）
    /// </summary>
    public static class ExceptionSanitizer
    {
        /// <summary>
        /// 将原始异常转换为安全的 WebResponseContent。
        ///
        /// 处理顺序：
        /// 1. YZHException → 直接使用 Message（已经是友好提示）
        /// 2. DbException → 脱敏匹配
        /// 3. 其他异常 → 统一兜底
        /// </summary>
        public static WebResponseContent Sanitize(
            System.Exception ex,
            string operation = "操作")
        {
            // 1. 业务异常：直接使用消息
            if (ex is YZHException bizEx)
            {
                Logger.Info($"业务异常 [{bizEx.ErrorCode}]: {bizEx.Message}");
                return new WebResponseContent().Error(bizEx.Message);
            }

            // 2. 数据库异常：脱敏匹配
            var dbFriendly = TryGetDbFriendlyMessage(ex);
            if (dbFriendly != null)
            {
                var errorCode = GenerateErrorCode();
                Logger.Error(
                    $"数据库异常 [{errorCode}] 类型:{ex.GetType().Name} " +
                    $"原始:{ex.Message}\n{ex.StackTrace}");
                return new WebResponseContent().Error(
                    $"{dbFriendly}（错误码：{errorCode}）");
            }

            // 3. 系统异常：完全脱敏
            var sysErrorCode = GenerateErrorCode();
            Logger.Error(
                $"系统异常 [{sysErrorCode}] 类型:{ex.GetType().Name} " +
                $"消息:{ex.Message}\n{ex.StackTrace}");
            return new WebResponseContent().Error(
                $"{operation}失败，请联系管理员（错误码：{sysErrorCode}）");
        }

        /// <summary>
        /// 尝试从数据库异常中提取友好提示。
        ///
        /// MySQL 错误码匹配表：
        /// - 1062 Duplicate entry → "数据重复，请检查唯一性字段"
        /// - 1451 Foreign key constraint → "存在关联数据，无法删除"
        /// - 1213 Deadlock → "系统繁忙，请重试"
        /// - 1205 Lock wait timeout → "操作超时，请重试"
        /// - 1040 Too many connections → "系统繁忙，请稍后重试"
        /// - 1048 Cannot be null → "必填字段不能为空"
        /// - 1406 Data too long → "数据长度超出限制"
        /// </summary>
        private static string TryGetDbFriendlyMessage(System.Exception ex)
        {
            if (ex == null) return null;

            var message = (ex.Message ?? "").ToLower();

            // 唯一键冲突
            if (message.Contains("duplicate entry") || message.Contains("1062"))
                return "数据重复，请检查唯一性字段";

            // 外键约束
            if (message.Contains("foreign key constraint") || message.Contains("1451"))
                return "存在关联数据，无法删除，请先处理关联项";

            // 死锁
            if (message.Contains("deadlock") || message.Contains("1213"))
                return "系统繁忙，请重试";

            // 锁超时
            if (message.Contains("lock wait timeout") || message.Contains("1205"))
                return "操作超时，请重试";

            // 连接过多
            if (message.Contains("too many connections") || message.Contains("1040"))
                return "系统繁忙，请稍后重试";

            // 字段不能为空
            if (message.Contains("cannot be null") || message.Contains("1048"))
                return "必填字段不能为空";

            // 数据过长
            if (message.Contains("data too long") || message.Contains("1406"))
                return "数据长度超出限制，请检查输入";

            // EF Core 包装的 DbUpdateException → 递归检查内部异常
            if (ex is Microsoft.EntityFrameworkCore.DbUpdateException)
                return TryGetDbFriendlyMessage(ex.InnerException) ?? "数据保存失败";

            // 其他数据库异常类型
            var typeName = ex.GetType().Name;
            if (typeName.Contains("MySql") || typeName.Contains("SqlException"))
                return "数据库操作失败，请检查数据或联系管理员";

            return null;
        }

        private static string GenerateErrorCode()
            => $"ERR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
    }
}
```

### 3.4 全局异常过滤器（Controller 层兜底）

```csharp
// YZH.Core/Exception/YZHExceptionFilter.cs
namespace YZH.Core.Exception
{
    /// <summary>
    /// 全局异常过滤器。
    /// 作用范围：所有 Controller 的所有 Action。
    /// 当 Service 层的 try-catch 遗漏时，这里做最终兜底。
    ///
    /// 注册方式（Program.cs）：
    ///   builder.Services.AddControllers()
    ///       .AddMvcOptions(options => options.Filters.Add<YZHExceptionFilter>());
    /// </summary>
    public class YZHExceptionFilter : Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter
    {
        public void OnException(Microsoft.AspNetCore.Mvc.Filters.ExceptionContext context)
        {
            var response = ExceptionSanitizer.Sanitize(context.Exception, "操作");

            context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(new
            {
                Status = false,
                Message = response.Message,
                // 开发环境下附带错误类型名（不附带堆栈和 SQL）
                ErrorType = context.HttpContext.RequestServices
                    .GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()?.IsDevelopment() == true
                    ? context.Exception.GetType().Name
                    : null
            })
            { StatusCode = 200 };

            context.ExceptionHandled = true;
        }
    }
}
```

---

## 四、特性设计

### 4.1 [QueryView] 查询视图声明特性

```csharp
// YZH.Core/Attributes/QueryViewAttribute.cs
namespace YZH.Core.Attributes
{
    /// <summary>
    /// 声明实体对应的查询视图类型。
    /// 标记在实体上，YZHTableServiceBase.GetPageData 会自动查询视图类型。
    ///
    /// 示例：
    ///   [Entity(TableName = "cert_iso_standard")]
    ///   [QueryView(typeof(ISOStandardView))]
    ///   public class ISOStandard : YZHBaseEntity { ... }
    ///
    ///   [NotMapped]
    ///   public class ISOStandardView : ISOStandard
    ///   {
    ///       public string CategoryName { get; set; }  // 字典翻译
    ///   }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class QueryViewAttribute : Attribute
    {
        public Type ViewType { get; }
        public QueryViewAttribute(Type viewType) => ViewType = viewType;
    }
}
```

### 4.2 [TreeSource] 左树右表数据来源特性

```csharp
// YZH.Core/Attributes/TreeSourceAttribute.cs
namespace YZH.Core.Attributes
{
    /// <summary>
    /// 声明左树右表场景的树数据来源。
    /// 标记在右表实体上，告知基类如何自动加载左树数据 + 如何联动过滤。
    ///
    /// 示例：
    ///   [TreeSource("CertCertificationBody", "CbCode", "Code", "Name")]
    ///   public class ISOStandard : YZHBaseEntity { ... }
    ///   // 含义：左树从认证机构表加载，右表按 CbCode 过滤
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TreeSourceAttribute : Attribute
    {
        public string TreeController { get; }   // 树数据来源 Controller
        public string FilterField { get; }      // 右表过滤字段名
        public string TreeKeyField { get; }     // 树节点 Key 字段
        public string TreeLabelField { get; }   // 树节点显示字段

        public TreeSourceAttribute(
            string treeController, string filterField,
            string treeKeyField = "Code", string treeLabelField = "Name")
        {
            TreeController = treeController;
            FilterField = filterField;
            TreeKeyField = treeKeyField;
            TreeLabelField = treeLabelField;
        }
    }
}
```

### 4.3 特性汇总

| 特性 | 用途 | 位置 |
|------|------|------|
| `[UniqueField("描述")]` | 单字段唯一性校验 | `VOL.Entity.CertPlatform` |
| `[UniqueField("描述", WithFields = new[] { "字段" })]` | 联合唯一性校验 | `VOL.Entity.CertPlatform` |
| `[QueryView(typeof(TView))]` | 声明查询视图类型 | `YZH.Core.Attributes` |
| `[TreeSource("controller", "filterField")]` | 声明左树右表数据来源 | `YZH.Core.Attributes` |
| `[Column("列名")]` | 数据库列名映射 | `System.ComponentModel` |
| `[NotMapped]` | 排除 EF Core 映射 | `System.ComponentModel` |
| `[Required]` | 非空校验 | `System.ComponentModel` |
| `[StringLength(n)]` | 长度校验 | `System.ComponentModel` |

---

## 五、类定义

### 5.1 ValidationAction 枚举

```csharp
namespace YZH.Core.Validation
{
    public enum ValidationAction { Add, Update, Delete }
}
```

### 5.2 EntityValidationResult 校验结果

```csharp
namespace YZH.Core.Validation
{
    public class EntityValidationResult
    {
        public bool IsValid { get; set; } = true;
        public string ErrorMessage { get; set; }

        public EntityValidationResult OK()
        { IsValid = true; ErrorMessage = null; return this; }

        public EntityValidationResult Error(string message)
        { IsValid = false; ErrorMessage = message; return this; }

        public WebResponseContent ToWebResponse()
            => IsValid ? WebResponseContent.Instance.OK()
                       : WebResponseContent.Instance.Error(ErrorMessage);
    }
}
```

### 5.3 EntityValidationHandler 校验处理器

> 完整代码与 V4.0 一致，此处不再重复。核心方法：
> - `Validate(entity, action, excludeCode)` — 入口
> - `ValidateRequiredFields(entity)` — 自动 `[Required]`/`[StringLength]` 校验
> - `ValidateUniqueFields(entity, isAdd, excludeCode)` — 自动 `[UniqueField]` + SQL 唯一性校验
> - 只做 `SELECT COUNT`，不做 INSERT/UPDATE/DELETE

### 5.4 YZHServiceBase 通用抽象根

```csharp
namespace YZH.Core
{
    /// <summary>
    /// YZH 业务 Service 通用抽象根，继承 Vol 的 ServiceBase。
    ///
    /// 职责（只管通用能力，不含场景特化逻辑）：
    /// 1. 统一异常兜底 — 所有 Add/Update/Del 的异常在此层捕获、脱敏
    /// 2. 自动校验管道 — EntityValidationHandler 自动执行 [Required]/[UniqueField]
    /// 3. 完整生命周期 — 6 个 virtual 方法
    /// 4. QueryView 支持
    ///
    /// 业务 Service 不直接继承此类，应继承场景专用基类：
    /// - 单表 CRUD → YZHTableServiceBase
    /// - 左树右表 → YZHTreeTableServiceBase
    /// - 关联表   → YZHLinkTableServiceBase
    /// - 纯树形   → YZHTreeServiceBase
    /// </summary>
    public abstract class YZHServiceBase<TEntity, TRepository>
        : ServiceBase<TEntity, TRepository>
        where TEntity : BaseEntity
        where TRepository : IRepository<TEntity>
    {
        public YZHServiceBase() { }
        public YZHServiceBase(TRepository repository) : base(repository) { }

        #region 1. 校验器创建（子类可覆写）
        protected virtual EntityValidationHandler<TEntity> CreateValidator()
            => new EntityValidationHandler<TEntity>();
        #endregion

        #region 2. 生命周期虚方法（子类按需覆写）

        // ====== 新增 ======
        protected virtual EntityValidationResult OnAdding(TEntity entity) => OK;
        protected virtual void OnAdded(TEntity entity) { }

        // ====== 修改 ======
        protected virtual EntityValidationResult OnUpdating(TEntity entity, string excludeCode) => OK;
        protected virtual void OnUpdated(TEntity entity) { }

        // ====== 删除 ======
        protected virtual EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities) => OK;
        protected virtual void OnDeleted(object[] keys) { }

        private static readonly EntityValidationResult OK = new EntityValidationResult().OK();

        #endregion

        #region 3. 重写 Add：生命周期 + 校验 + 异常兜底

        public override WebResponseContent Add(SaveModel saveDataModel)
        {
            // 1. 空数据检查
            if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
                return new WebResponseContent().Error("提交数据为空");

            // 2. 转换实体（异常脱敏）
            TEntity entity;
            try { entity = saveDataModel.MainData.DicToEntity<TEntity>(); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "数据解析"); }

            // 3. 自动校验（唯一性 + 必填）
            try
            {
                var validateResult = CreateValidator().Validate(entity, ValidationAction.Add);
                if (!validateResult.IsValid)
                    return validateResult.ToWebResponse();
            }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "数据校验"); }

            // 4. 自定义新增前校验（业务规则）
            try
            {
                var customResult = OnAdding(entity);
                if (!customResult.IsValid)
                    return customResult.ToWebResponse();
            }
            catch (YZHException bizEx) { return ExceptionSanitizer.Sanitize(bizEx, "新增"); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "新增"); }

            // 5. 执行保存（Vol 框架标准流程）— 异常兜底
            try
            {
                var result = base.Add(saveDataModel);

                // 6. 新增后处理（异常不影响已保存数据，仅记录日志）
                if (result.Status)
                {
                    try { OnAdded(entity); }
                    catch (Exception afterEx)
                    { Logger.Error($"OnAdded 后置处理异常: {afterEx.Message}", afterEx); }
                }
                return result;
            }
            catch (YZHException bizEx) { return ExceptionSanitizer.Sanitize(bizEx, "新增"); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "新增"); }
        }

        #endregion

        #region 4. 重写 Update：生命周期 + 校验 + 异常兜底

        public override WebResponseContent Update(SaveModel saveDataModel)
        {
            // 1. 空数据检查
            if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
                return new WebResponseContent().Error("提交数据为空");

            // 2. 转换实体
            TEntity entity;
            try { entity = saveDataModel.MainData.DicToEntity<TEntity>(); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "数据解析"); }

            // 3. 获取 Code（业务主键，用于排除自身）
            string excludeCode = typeof(TEntity)
                .GetProperty("Code")?.GetValue(entity)?.ToString();

            // 4. 自动校验
            try
            {
                var validateResult = CreateValidator()
                    .Validate(entity, ValidationAction.Update, excludeCode);
                if (!validateResult.IsValid)
                    return validateResult.ToWebResponse();
            }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "数据校验"); }

            // 5. 自定义修改前校验
            try
            {
                var customResult = OnUpdating(entity, excludeCode);
                if (!customResult.IsValid)
                    return customResult.ToWebResponse();
            }
            catch (YZHException bizEx) { return ExceptionSanitizer.Sanitize(bizEx, "修改"); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "修改"); }

            // 6. 执行保存 — 异常兜底
            try
            {
                var result = base.Update(saveDataModel);
                if (result.Status)
                {
                    try { OnUpdated(entity); }
                    catch (Exception afterEx)
                    { Logger.Error($"OnUpdated 后置处理异常: {afterEx.Message}", afterEx); }
                }
                return result;
            }
            catch (YZHException bizEx) { return ExceptionSanitizer.Sanitize(bizEx, "修改"); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "修改"); }
        }

        #endregion

        #region 5. 重写 Del：生命周期 + 校验 + 异常兜底

        public override WebResponseContent Del(object[] keys, bool delList = true)
        {
            // 1. 查询待删除实体
            List<TEntity> entityList;
            try
            {
                var keyName = typeof(TEntity).GetKeyName();
                var keyExpression = keyName
                    .CreateExpression<TEntity>(keys[0].ToString(), LinqExpressionType.Equal);
                entityList = repository.FindAsIQueryable(keyExpression).ToList();

                if (entityList == null || entityList.Count == 0)
                    return new WebResponseContent().Error("未找到要删除的数据");
            }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "删除"); }

            // 2. 删除前校验（关联关系校验）
            try
            {
                var customResult = OnDeleting(keys, entityList);
                if (!customResult.IsValid)
                    return customResult.ToWebResponse();
            }
            catch (YZHException bizEx) { return ExceptionSanitizer.Sanitize(bizEx, "删除"); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "删除"); }

            // 3. 执行删除 — 异常兜底
            try
            {
                var result = base.Del(keys, delList);
                if (result.Status)
                {
                    try { OnDeleted(keys); }
                    catch (Exception afterEx)
                    { Logger.Error($"OnDeleted 后置处理异常: {afterEx.Message}", afterEx); }
                }
                return result;
            }
            catch (YZHException bizEx) { return ExceptionSanitizer.Sanitize(bizEx, "删除"); }
            catch (Exception ex) { return ExceptionSanitizer.Sanitize(ex, "删除"); }
        }

        #endregion

        #region 6. 辅助方法

        /// <summary>获取关联表记录数（用于删除校验）</summary>
        protected int GetRelatedCount(string relatedTable, string foreignKeyColumn, object foreignKeyValue)
        {
            var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                      $"WHERE {foreignKeyColumn} = @val AND (IsDeleted = 0 OR IsDeleted IS NULL)";
            return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, new { val = foreignKeyValue }));
        }

        /// <summary>获取关联表记录数（多条件版本）</summary>
        protected int GetRelatedCount(string relatedTable, Dictionary<string, object> conditions)
        {
            var whereClauses = conditions.Select(kv => $"{kv.Key} = @{kv.Key}").ToList();
            var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                      $"WHERE {string.Join(" AND ", whereClauses)} AND (IsDeleted = 0 OR IsDeleted IS NULL)";
            return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, conditions));
        }

        #endregion
    }
}
```

> **关键设计说明 — 异常兜底的三层防线**：
>
> | 层级 | 位置 | 捕获范围 | 处理方式 |
> |------|------|---------|---------|
> | 第一层 | `YZHServiceBase.Add/Update/Del` | Service 方法内的所有异常 | `ExceptionSanitizer.Sanitize` 脱敏后返回 |
> | 第二层 | `YZHExceptionFilter` | Controller Action 的未处理异常 | 脱敏后返回 JSON |
> | 第三层 | `RepositoryBase.DbContextBeginTransaction` | Vol 事务内的异常 | 回滚事务（已有） |
>
> **OnAdded/OnUpdated/OnDeleted 后置处理异常**：数据已保存成功，后置处理异常仅记录日志，不影响已保存的数据和返回结果。

### 5.5 YZHTableServiceBase 单表 CRUD 专用

```csharp
namespace YZH.Core
{
    /// <summary>
    /// 单表 CRUD 专用基类（90% 场景）。
    /// 继承 YZHServiceBase，新增 QueryView 视图查询支持。
    ///
    /// 使用：
    ///   // 零配置
    ///   public class CertStageService
    ///       : YZHTableServiceBase<CertStage, ICertStageRepository>
    ///       , ICertStageService, IDependency
    ///   { }
    ///
    ///   // 声明查询视图
    ///   [QueryView(typeof(ISOStandardView))]
    ///   public class ISOStandard : YZHBaseEntity { ... }
    ///   // Service 自动使用 ISOStandardView 查询，Add/Update/Del 用 ISOStandard
    /// </summary>
    public abstract class YZHTableServiceBase<TEntity, TRepository>
        : YZHServiceBase<TEntity, TRepository>
        where TEntity : BaseEntity
        where TRepository : IRepository<TEntity>
    {
        /// <summary>
        /// 重写 GetPageData：如果实体声明了 [QueryView]，自动查询视图。
        /// </summary>
        public override PageGridData<TEntity> GetPageData(PageDataOptions options)
        {
            // 查询前钩子（可改查询参数）
            try { OnGetPageDataBefore(options); }
            catch (Exception ex) { Logger.Error($"OnGetPageDataBefore 异常: {ex.Message}", ex); }

            var viewType = GetQueryViewType();
            if (viewType != null && viewType != typeof(TEntity))
            {
                var result = GetPageDataFromView(options, viewType);
                // 查询后钩子（可加工数据）
                try { OnGetPageDataAfter(result.rows); }
                catch (Exception ex) { Logger.Error($"OnGetPageDataAfter 异常: {ex.Message}", ex); }
                return result;
            }

            var baseResult = base.GetPageData(options);
            try { OnGetPageDataAfter(baseResult.rows); }
            catch (Exception ex) { Logger.Error($"OnGetPageDataAfter 异常: {ex.Message}", ex); }
            return baseResult;
        }

        /// <summary>查询前钩子（可改 wheres / sort）</summary>
        protected virtual void OnGetPageDataBefore(PageDataOptions options) { }

        /// <summary>查询后钩子（可加工 rows：翻译字典、合并字段）</summary>
        protected virtual void OnGetPageDataAfter(List<TEntity> rows) { }

        /// <summary>获取实体声明的查询视图类型</summary>
        protected Type GetQueryViewType()
            => typeof(TEntity).GetCustomAttribute<QueryViewAttribute>()?.ViewType;

        /// <summary>从视图查询分页数据（子类可覆写自定义逻辑）</summary>
        protected virtual PageGridData<TEntity> GetPageDataFromView(
            PageDataOptions options, Type viewType)
        {
            var tableName = viewType.GetCustomAttribute<EntityAttribute>()?.TableName
                ?? viewType.Name;
            var parameters = new Dictionary<string, object>();
            var whereClause = "1=1";

            var countSql = $"SELECT COUNT(1) FROM {tableName} WHERE {whereClause} " +
                           $"AND (IsDeleted = 0 OR IsDeleted IS NULL)";
            var total = Convert.ToInt32(
                DBServerProvider.SqlDapper.ExecuteScalar(countSql, parameters));

            var dataSql = $"SELECT * FROM {tableName} WHERE {whereClause} " +
                          $"AND (IsDeleted = 0 OR IsDeleted IS NULL) " +
                          $"ORDER BY Id DESC LIMIT @offset, @rows";
            parameters["offset"] = (options.Page - 1) * options.Rows;
            parameters["rows"] = options.Rows;

            var viewData = DBServerProvider.SqlDapper.QueryList<dynamic>(dataSql, parameters);
            var rows = viewData.Select(d => (TEntity)d).ToList();

            return new PageGridData<TEntity> { rows = rows, total = total };
        }
    }
}
```

### 5.6 YZHTreeTableServiceBase 左树右表专用

```csharp
namespace YZH.Core
{
    /// <summary>
    /// 左树右表专用基类。
    /// 继承 YZHTableServiceBase，新增左树数据加载 + 联动过滤。
    ///
    /// 使用：
    ///   [TreeSource("CertCertificationBody", "CbCode", "Code", "Name")]
    ///   [QueryView(typeof(ISOStandardView))]
    ///   public class ISOStandard : YZHBaseEntity { ... }
    ///
    ///   public class ISOStandardService
    ///       : YZHTreeTableServiceBase<ISOStandard, IISOStandardRepository>
    ///       , IISOStandardService, IDependency
    ///   { }
    ///   // 自动：GetTreeData() 返回认证机构列表，GetPageData 按 CbCode 过滤
    /// </summary>
    public abstract class YZHTreeTableServiceBase<TEntity, TRepository>
        : YZHTableServiceBase<TEntity, TRepository>
        where TEntity : BaseEntity
        where TRepository : IRepository<TEntity>
    {
        /// <summary>
        /// 获取左侧树数据。
        /// 根据 [TreeSource] 特性自动查询关联表。
        /// </summary>
        public virtual List<object> GetTreeData()
        {
            var attr = typeof(TEntity).GetCustomAttribute<TreeSourceAttribute>();
            if (attr == null) return new List<object>();

            // 用 Dapper 查询树数据来源表
            var treeTable = GetTableNameByController(attr.TreeController);
            var sql = $"SELECT * FROM {treeTable} " +
                      $"WHERE (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY Id";
            var data = DBServerProvider.SqlDapper.QueryList<dynamic>(sql, null);
            return data.Cast<object>().ToList();
        }

        /// <summary>
        /// 重写 GetPageData：自动注入 treeKey 过滤条件。
        /// </summary>
        public override PageGridData<TEntity> GetPageData(PageDataOptions options)
        {
            var attr = typeof(TEntity).GetCustomAttribute<TreeSourceAttribute>();
            if (attr != null)
            {
                // 从查询参数中提取 treeKey
                var treeKey = ExtractTreeKey(options);
                if (!string.IsNullOrEmpty(treeKey))
                {
                    InjectTreeFilter(options, attr.FilterField, treeKey);
                    // 钩子：树节点选中后
                    try { OnTreeSelect(treeKey, options); }
                    catch (Exception ex) { Logger.Error($"OnTreeSelect 异常: {ex.Message}", ex); }
                }
            }
            return base.GetPageData(options);
        }

        /// <summary>树节点选中后钩子（可改右侧查询参数）</summary>
        protected virtual void OnTreeSelect(string treeKey, PageDataOptions options) { }

        private string ExtractTreeKey(PageDataOptions options)
        {
            // 从 Vol 的 Wheres 参数中提取树节点 key
            if (options.Which != null)
            {
                foreach (var filter in options.WhereEList ?? new List<VOL.Core.Enums.FilterList>())
                {
                    if (filter.Field?.Equals("treeKey", StringComparison.OrdinalIgnoreCase) == true)
                        return filter.Value?.ToString();
                }
            }
            return null;
        }

        private void InjectTreeFilter(PageDataOptions options, string filterField, string treeKey)
        {
            // 注入过滤条件到 Wheres
            if (options.Which == null) options.Which = new Dictionary<string, object>();
            options.Which[filterField] = treeKey;
        }

        private string GetTableNameByController(string controllerName)
        {
            // 约定：Controller 名去掉前缀 Cert → 表名 cert_ + 小写
            // 如 "CertCertificationBody" → "cert_certification_body"
            if (controllerName.StartsWith("Cert"))
                controllerName = controllerName.Substring(4);
            return "cert_" + ToSnakeCase(controllerName);
        }

        private static string ToSnakeCase(string str)
        {
            return string.Concat(str.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c).ToString() : char.ToLower(c).ToString()));
        }
    }
}
```

### 5.7 YZHLinkTableServiceBase 关联表专用

```csharp
namespace YZH.Core
{
    /// <summary>
    /// 关联表专用基类（双泛型）。
    /// 不走标准 Add/Update/Del，是勾选即保存的关联操作模式。
    ///
    /// 使用：
    ///   public class OrgStandardLinkService
    ///       : YZHLinkTableServiceBase<CertificationBody, YzhOrgStandard>
    ///       , IOrgStandardLinkService, IDependency
    ///   {
    ///       public override List<object> GetLinkedItems(string cbCode) { ... }
    ///       public override WebResponseContent ToggleLink(string cbCode, string standardCode, bool isLinked) { ... }
    ///   }
    /// </summary>
    public abstract class YZHLinkTableServiceBase<TMain, TLink>
        where TMain : class
        where TLink : class
    {
        /// <summary>查询已关联的项</summary>
        public abstract List<object> GetLinkedItems(string mainCode);

        /// <summary>查询全部可关联项（标记是否已关联）</summary>
        public abstract List<object> GetLinkableItems(string mainCode);

        /// <summary>切换关联状态（勾选即保存）</summary>
        public abstract WebResponseContent ToggleLink(
            string mainCode, string itemCode, bool isLinked);

        /// <summary>批量设置关联</summary>
        public abstract WebResponseContent BatchSetLinks(
            string mainCode, List<string> itemCodes);

        // 钩子
        protected virtual void OnLinkAdded(string mainCode, string itemCode) { }
        protected virtual void OnLinkRemoved(string mainCode, string itemCode) { }
    }
}
```

### 5.8 YZHTreeServiceBase 纯树形管理专用

```csharp
namespace YZH.Core
{
    /// <summary>
    /// 纯树形管理专用基类。
    /// 重写 GetPageData → 返回树形结构（不分页）。
    /// 内置级联删除校验 + 移动节点校验。
    ///
    /// 使用：
    ///   public class DirectoryTemplateService
    ///       : YZHTreeServiceBase<DirectoryTemplate, IDirectoryTemplateRepository>
    ///       , IDirectoryTemplateService, IDependency
    ///   { }
    /// </summary>
    public abstract class YZHTreeServiceBase<TEntity, TRepository>
        : YZHTableServiceBase<TEntity, TRepository>
        where TEntity : BaseEntity
        where TRepository : IRepository<TEntity>
    {
        /// <summary>重写 GetPageData → 返回树形结构</summary>
        public override PageGridData<TEntity> GetPageData(PageDataOptions options)
        {
            var allData = repository.FindAsIQueryable(options).ToList();
            var treeData = BuildTree(allData);
            return new PageGridData<TEntity> { rows = treeData, total = allData.Count };
        }

        /// <summary>构建树形结构（基于 ParentCode 字段）</summary>
        protected virtual List<TEntity> BuildTree(List<TEntity> flatList)
        {
            var codeProp = typeof(TEntity).GetProperty("Code");
            var parentProp = typeof(TEntity).GetProperty("ParentCode");
            if (codeProp == null || parentProp == null) return flatList;

            var lookup = flatList.ToLookup(
                e => parentProp.GetValue(e)?.ToString() ?? "", e => e);
            var rootItems = flatList.Where(e =>
                string.IsNullOrEmpty(parentProp.GetValue(e)?.ToString())).ToList();
            foreach (var item in rootItems)
                SetChildren(item, lookup, codeProp, parentProp);
            return rootItems;
        }

        private void SetChildren(TEntity parent,
            ILookup<string, TEntity> lookup,
            PropertyInfo codeProp, PropertyInfo parentProp)
        {
            var code = codeProp.GetValue(parent)?.ToString();
            var children = lookup[code ?? ""].ToList();
            // 通过反射设置 Children 属性（如有）
            var childrenProp = typeof(TEntity).GetProperty("Children");
            if (childrenProp != null && childrenProp.PropertyType == typeof(List<TEntity>))
            {
                childrenProp.SetValue(parent, children);
                foreach (var child in children)
                    SetChildren(child, lookup, codeProp, parentProp);
            }
        }

        /// <summary>
        /// 删除前校验：检查子节点
        /// </summary>
        protected override EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities)
        {
            foreach (var entity in entities)
            {
                var codeProp = typeof(TEntity).GetProperty("Code");
                var code = codeProp?.GetValue(entity)?.ToString();
                if (string.IsNullOrEmpty(code)) continue;

                var childCount = GetRelatedCount(GetTableName(), "parent_code", code);
                if (childCount > 0)
                    return new EntityValidationResult()
                        .Error($"该项下有 {childCount} 个子节点，请先删除子节点");
            }
            return new EntityValidationResult().OK();
        }

        /// <summary>移动节点校验（防止循环引用）</summary>
        protected virtual EntityValidationResult ValidateMove(
            string code, string newParentCode)
        {
            if (code == newParentCode)
                return new EntityValidationResult().Error("不能将节点移动到自身下");
            if (IsDescendant(newParentCode, code))
                return new EntityValidationResult().Error("不能将节点移动到其子节点下");
            return new EntityValidationResult().OK();
        }

        private bool IsDescendant(string code, string ancestorCode)
        {
            var sql = $"WITH RECURSIVE TreePath AS (" +
                      $"  SELECT code, parent_code FROM {GetTableName()} WHERE code = @code " +
                      $"  UNION ALL " +
                      $"  SELECT t.code, t.parent_code FROM {GetTableName()} t " +
                      $"  INNER JOIN TreePath tp ON t.code = tp.parent_code" +
                      $") SELECT COUNT(1) FROM TreePath WHERE code = @ancestorCode";
            var count = Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(
                sql, new { code, ancestorCode = ancestorCode }));
            return count > 0;
        }
    }
}
```

---

## 六、前后端模式对应

| 后端基类 | 前端组件 | 关键特性 | 典型页面 |
|---------|---------|---------|---------|
| `YZHTableServiceBase` | `YzhCrudTable.vue` | 标准 CRUD + QueryView | 认证机构、审核阶段、NC规则 |
| `YZHTreeTableServiceBase` | `YzhTreeTable.vue` | 左树数据接口 + 自动过滤 | ISO标准(机构→标准) |
| `YZHLinkTableServiceBase` | `YzhTreeCheckboxTable.vue` | 关联查询 + 勾选即保存 | 机构-标准关联 |
| `YZHTreeServiceBase` | 待开发 `YzhTree.vue` | 树形数据 + 级联删除 | 标准目录模板 |

---

## 七、使用指南

### 7.1 场景一：最简使用（零配置）

```csharp
// 之前：每个 Service 手写 try-catch + 校验
public class CertStageService : ServiceBase<CertStage, ICertStageRepository>
{
    public override WebResponseContent Add(SaveModel saveDataModel)
    {
        // 手动校验...
        // 手动 try-catch...
        return base.Add(saveDataModel);
    }
}

// 之后：继承 YZHTableServiceBase，什么都不用写
public class CertStageService
    : YZHTableServiceBase<CertStage, ICertStageRepository>
    , ICertStageService, IDependency
{
    // 基类自动处理：唯一性校验 + 异常兜底 + 生命周期
}
```

### 7.2 场景二：左树右表

```csharp
// 实体声明
[Entity(TableName = "cert_iso_standard")]
[QueryView(typeof(ISOStandardView))]
[TreeSource("CertCertificationBody", "CbCode", "Code", "Name")]
public class ISOStandard : YZHBaseEntity { ... }

// Service：自动加载左树 + 自动过滤
public class ISOStandardService
    : YZHTreeTableServiceBase<ISOStandard, IISOStandardRepository>
    , IISOStandardService, IDependency
{ }
// GetTreeData() → 返回认证机构列表
// GetPageData() → 自动按 CbCode 过滤
```

### 7.3 场景三：删除校验（关联关系）

```csharp
public class ISOStandardService
    : YZHTreeTableServiceBase<ISOStandard, IISOStandardRepository>
    , IISOStandardService, IDependency
{
    protected override EntityValidationResult OnDeleting(
        object[] keys, List<ISOStandard> entities)
    {
        var entity = entities[0];
        var clauseCount = GetRelatedCount("cert_iso_clause", "standard_code", entity.StandardCode);
        if (clauseCount > 0)
            return new EntityValidationResult().Error($"该标准下有 {clauseCount} 个条款，请先删除条款");
        return new EntityValidationResult().OK();
    }
}
```

### 7.4 场景四：抛出业务异常

```csharp
public class CertCertificationBodyService
    : YZHTableServiceBase<CertificationBody, ICertCertificationBodyRepository>
    , ICertCertificationBodyService, IDependency
{
    protected override EntityValidationResult OnUpdating(
        CertificationBody entity, string excludeCode)
    {
        var dbEntity = DBServerProvider.SqlDapper.QueryFirst<CertificationBody>(
            "SELECT * FROM cert_certification_body WHERE code = @code",
            new { code = excludeCode });

        if (dbEntity != null && dbEntity.CbCode != entity.CbCode)
            // 直接返回校验失败（不需要抛异常）
            return new EntityValidationResult().Error("机构编号不允许修改");

        return new EntityValidationResult().OK();
    }
}
```

---

## 八、完整调用流程

### 8.1 新增流程（异常兜底路径）

```
前端 POST /api/CertCertificationBody/add
  → ApiBaseController.Add(SaveModel)
    → CertCertificationBodyService.Add(SaveModel)  // 继承 YZHTableServiceBase
      │
      ├─ 1. 空数据检查 → "提交数据为空"
      ├─ 2. DicToEntity<CertificationBody>()
      │     └─ 异常？→ ExceptionSanitizer.Sanitize(ex, "数据解析")
      ├─ 3. CreateValidator().Validate(entity, Add)
      │     ├─ ValidateRequiredFields → "XX不能为空"
      │     └─ ValidateUniqueFields → "XX「值」已存在"
      ├─ 4. OnAdding(entity) → 自定义校验
      │     └─ YZHException？→ ExceptionSanitizer.Sanitize(bizEx, "新增")
      ├─ 5. base.Add(saveModel) → Vol 框架流程
      │     └─ 异常？→ ExceptionSanitizer.Sanitize(ex, "新增")
      │           ├─ MySQL 1062 → "数据重复，请检查唯一性字段（错误码：ERR-...）"
      │           ├─ MySQL 1451 → "存在关联数据，无法删除（错误码：ERR-...）"
      │           └─ 其他 → "新增失败，请联系管理员（错误码：ERR-...）"
      └─ 6. OnAdded(entity) → 后置处理（异常仅记录日志）
```

### 8.2 前端收到的响应

```json
// 业务校验失败
{ "Status": false, "Message": "机构编号「CB2026」已存在，请使用其他值" }

// 数据库异常（脱敏后）
{ "Status": false, "Message": "数据重复，请检查唯一性字段（错误码：ERR-20260825-A1B2C3）" }

// 系统异常（完全脱敏）
{ "Status": false, "Message": "新增失败，请联系管理员（错误码：ERR-20260825-X9Y8Z7）" }
```

### 8.3 前端异常处理对齐

前端 `YzhCrudTable.vue` / `YzhCrudV3.vue` 已有的 `saving` 状态 + `ElMessage.error` 展示逻辑，与后端 `WebResponseContent.Message` 完全对齐：

```typescript
// 前端接收后端响应
saving.value = true
try {
    const res = await api.add(editForm)
    if (!res.status) {
        // 直接展示后端返回的友好消息
        ElMessage.error(res.message)
        return
    }
    // 成功处理...
} catch (error) {
    // 网络异常等
    ElMessage.error('网络异常，请稍后重试')
} finally {
    saving.value = false
}
```

---

## 九、迁移指南

### 9.1 可删除的代码

| 文件 | 说明 |
|------|------|
| `CertCertificationBodyService` 中的手动 try-catch | 被 `YZHServiceBase` 统一异常兜底替代 |
| `CertCertificationBodyService` 中的 `ValidateUniqueFieldsFromDict` 调用 | 被 `EntityValidationHandler` 自动调用替代 |
| `VOL.CERT/Extensions/UniqueValidationExtensions.cs` | 被 `EntityValidationHandler.ValidateUniqueFields` 替代 |
| `StandardDirectoryService` 中各方法的 try-catch | 被基类统一处理 |

### 9.2 迁移步骤

**第一步**：在 `YZH.Core` 中创建核心类
- `Exception/YZHException.cs`
- `Exception/YZHDbException.cs`
- `Exception/ExceptionSanitizer.cs`
- `Exception/YZHExceptionFilter.cs`
- `Validation/EntityValidationHandler.cs`（已有）
- `Validation/EntityValidationResult.cs`（已有）
- `Validation/ValidationAction.cs`（已有）
- `Attributes/QueryViewAttribute.cs`
- `Attributes/TreeSourceAttribute.cs`
- `YZHServiceBase.cs`
- `YZHTableServiceBase.cs`
- `YZHTreeTableServiceBase.cs`
- `YZHLinkTableServiceBase.cs`
- `YZHTreeServiceBase.cs`

**第二步**：在 `Program.cs` 中注册全局异常过滤器

```csharp
builder.Services.AddControllers()
    .AddMvcOptions(options =>
    {
        options.Filters.Add<YZH.Core.Exception.YZHExceptionFilter>();
    });
```

**第三步**：修改业务 Service 的继承关系

```csharp
// 之前
public class CertCertificationBodyService
    : ServiceBase<CertificationBody, ICertCertificationBodyRepository>

// 之后（单表场景）
public class CertCertificationBodyService
    : YZHTableServiceBase<CertificationBody, ICertCertificationBodyRepository>

// 之后（左树右表场景）
public class ISOStandardService
    : YZHTreeTableServiceBase<ISOStandard, IISOStandardRepository>
```

**第四步**：删除 Service 中的手动 try-catch 和校验代码

### 9.3 迁移优先级

| 优先级 | Service | 基类 | 说明 |
|--------|---------|------|------|
| P0 | `CertCertificationBodyService` | `YZHTableServiceBase` | 当前 try-catch 最多，优先迁移 |
| P0 | `ISOStandardService` | `YZHTreeTableServiceBase` | 左树右表场景 |
| P1 | `CertStageService` | `YZHTableServiceBase` | 有 [UniqueField] |
| P1 | `OrgStageService` | `YZHTreeTableServiceBase` | 左树右表 |
| P2 | `OrgStandardLinkService` | `YZHLinkTableServiceBase` | 关联表场景 |
| P3 | `DirectoryTemplateService` | `YZHTreeServiceBase` | 树形管理 |

---

## 十、注意事项

### 10.1 异常兜底三层防线

| 层级 | 位置 | 捕获范围 | 处理方式 |
|------|------|---------|---------|
| 第一层 | `YZHServiceBase.Add/Update/Del` | Service 方法内所有异常 | `ExceptionSanitizer.Sanitize` 脱敏后返回 |
| 第二层 | `YZHExceptionFilter` | Controller Action 未处理异常 | 脱敏后返回 JSON |
| 第三层 | `RepositoryBase.DbContextBeginTransaction` | Vol 事务内异常 | 回滚事务（Vol 已有） |

### 10.2 后置处理异常不阻断

`OnAdded` / `OnUpdated` / `OnDeleted` 的异常不会影响已保存的数据和返回结果，仅记录错误日志。

### 10.3 生命周期执行顺序

```
Add:
  空数据检查
  → DicToEntity（异常脱敏）
  → Validate（[Required] → [UniqueField]）
  → OnAdding（自定义校验）
  → base.Add（Vol 标准流程，异常脱敏）
  → OnAdded（后置处理，异常仅记录日志）

Update:
  空数据检查
  → DicToEntity（异常脱敏）
  → Validate（[Required] → [UniqueField]，排除自身 Code）
  → OnUpdating（自定义校验）
  → base.Update（Vol 标准流程，异常脱敏）
  → OnUpdated（后置处理，异常仅记录日志）

Delete:
  查询待删除实体（异常脱敏）
  → OnDeleting（关联关系校验）
  → base.Del（Vol 标准流程，异常脱敏）
  → OnDeleted（后置处理，异常仅记录日志）
```

### 10.4 前后端基类对应

| 后端基类 | 前端组件 | 对应关系 |
|---------|---------|---------|
| `YZHTableServiceBase` | `YzhCrudTable` | 单表 CRUD |
| `YZHTreeTableServiceBase` | `YzhTreeTable` | 左树右表 |
| `YZHLinkTableServiceBase` | `YzhTreeCheckboxTable` | 关联表 |
| `YZHTreeServiceBase` | 待开发 `YzhTree` | 树形管理 |