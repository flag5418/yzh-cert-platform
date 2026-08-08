# YZH-Framework V2.0 架构设计升级方案

**版本**: V2.0  
**日期**: 2026-08-08  
**状态**: 设计阶段

---

## 一、核心设计原则

### 1.1 三大核心思想

| 原则 | 说明 | 解决问题 |
|------|------|----------|
| **配置驱动** | 所有 UI 配置存储到数据库，支持管理页面维护 | 避免硬编码，支持动态调整 |
| **视图分离** | 列表展示使用视图（View），编辑操作使用实体（Entity） | 解决视图与实体不一致的问题 |
| **代码极简** | 利用 C# 特性（拦截器、扩展方法、泛型）减少重复代码 | 提高开发效率，降低维护成本 |

### 1.2 设计哲学

```
┌─────────────────────────────────────────────────────────────┐
│                    YZH-Framework V2.0                       │
├─────────────────────────────────────────────────────────────┤
│  核心目标：用最少的代码，完成最多的功能                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. 配置即代码（Configuration as Code）                       │
│     - 数据库配置 → 管理页面维护 → 前端动态加载               │
│     - 支持 XML/JSON 导入导出，便于迁移和备份                 │
│                                                              │
│  2. 视图与实体分离（View-Entity Separation）                 │
│     - 列表展示：视图（View）→ 轻量、高性能                   │
│     - 编辑操作：实体（Entity）→ 完整、可审计                 │
│     - 统一基类：BaseEntity（实体）+ BaseView（视图）         │
│                                                              │
│  3. 拦截器驱动（Interceptor-Driven）                         │
│     - 自动填充审计字段（CreateID, Creator, CreateDate）      │
│     - 自动编码生成（CodeRule）                               │
│     - 自动权限校验（PermissionCheck）                        │
│     - 自动日志记录（AuditLog）                               │
│                                                              │
│  4. 扩展点设计（Extension Points）                           │
│     - 特性（Attribute）声明式配置                            │
│     - 接口（Interface）可插拔实现                            │
│     - 虚方法（Virtual Method）可覆盖扩展                     │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、架构设计升级

### 2.1 整体架构（V2.0）

```
┌─────────────────────────────────────────────────────────────────────┐
│                         前端层（Vue 3）                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                 │
│  │ YzhCrudTable │  │ YzhEditDialog│  │ YzhTreeView │                 │
│  │ (视图渲染)   │  │ (实体编辑)   │  │ (树形展示)  │                 │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘                 │
│         │                │                │                         │
│         └────────────────┴────────────────┘                         │
│                          │                                          │
│                    YZHConfigLoader                                  │
│                    (配置加载器)                                      │
│                          │                                          │
└──────────────────────────┼──────────────────────────────────────────┘
                           │ HTTP REST API
┌──────────────────────────┼──────────────────────────────────────────┐
│                         后端层（.NET 8）                              │
│                          │                                          │
│  ┌───────────────────────┼───────────────────────┐                  │
│  │     YZH.WebApi（控制器层）                      │                  │
│  │  ┌─────────────┐  ┌─────────────┐            │                  │
│  │  │ ApiBaseCtrl │  │ YZHBaseCtrl │            │                  │
│  │  │  (Vol原生)   │  │ (YZH增强)   │            │                  │
│  │  └──────┬──────┘  └──────┬──────┘            │                  │
│  └─────────┼────────────────┼───────────────────┘                  │
│            │                │                                       │
│  ┌─────────┼────────────────┼───────────────────┐                  │
│  │     YZH.CertPlatform（业务服务层）              │                  │
│  │  ┌─────────────┐  ┌─────────────┐            │                  │
│  │  │YzhPageConfig│  │YzhFieldConfig│            │                  │
│  │  │   Service   │  │   Service   │            │                  │
│  │  └──────┬──────┘  └──────┬──────┘            │                  │
│  │         │                │                   │                  │
│  │  ┌─────────────────────────────────────┐     │                  │
│  │  │    YZHServiceBase<T>（统一服务基类）   │     │                  │
│  │  │  - 自动填充审计字段                   │     │                  │
│  │  │  - 自动编码生成                       │     │                  │
│  │  │  - 自动权限校验                       │     │                  │
│  │  │  - 自动日志记录                       │     │                  │
│  │  └─────────────────────────────────────┘     │                  │
│  └───────────────────────────────────────────────┘                  │
│                          │                                          │
│  ┌───────────────────────┼───────────────────────┐                  │
│  │     YZH.Core（核心框架层）                        │                  │
│  │  ┌─────────────┐  ┌─────────────┐            │                  │
│  │  │  Entities   │  │ Attributes  │            │                  │
│  │  │  (实体基类)  │  │ (特性定义)   │            │                  │
│  │  └──────┬──────┘  └──────┬──────┘            │                  │
│  │         │                │                   │                  │
│  │  ┌─────────────────────────────────────┐     │                  │
│  │  │    YZHBaseEntity（统一实体基类）       │     │                  │
│  │  │  - 12 审计字段                       │     │                  │
│  │  │  - Code 业务编码                     │     │                  │
│  │  │  - OrgCode 多租户隔离                │     │                  │
│  │  └─────────────────────────────────────┘     │                  │
│  │                                              │                  │
│  │  ┌─────────────────────────────────────┐     │                  │
│  │  │    拦截器/特性体系（Phase 2）          │     │                  │
│  │  │  - [YZHAudited] 审计追踪             │     │                  │
│  │  │  - [YZHCodeRule] 编码规则            │     │                  │
│  │  │  - [YZHValidation] 校验规则          │     │                  │
│  │  │  - [YZHPermission] 权限控制          │     │                  │
│  │  └─────────────────────────────────────┘     │                  │
│  └───────────────────────────────────────────────┘                  │
│                          │                                          │
│  ┌───────────────────────┼───────────────────────┐                  │
│  │     VOL.Entity（实体定义层）                        │                  │
│  │  - 继承 YZHBaseEntity                          │                  │
│  │  - 使用 EF Core 注解                           │                  │
│  │  - 定义业务字段                                 │                  │
│  └───────────────────────────────────────────────┘                  │
│                          │                                          │
└──────────────────────────┼──────────────────────────────────────────┘
                           │ EF Core
┌──────────────────────────┼──────────────────────────────────────────┐
│                         数据层                                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                 │
│  │ 业务数据表   │  │ 配置数据表   │  │ 审计日志表   │                 │
│  │ cert_*      │  │ yzh_*       │  │ audit_*     │                 │
│  └─────────────┘  └─────────────┘  └─────────────┘                 │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 三、核心组件设计

### 3.1 实体与视图分离设计

#### 3.1.1 实体基类（BaseEntity）

```csharp
// YZH.Core/Entities/YZHBaseEntity.cs
public abstract class YZHBaseEntity : BaseEntity
{
    #region 主键
    [Key]
    [Column(TypeName = "bigint")]
    public long Id { get; set; }
    #endregion

    #region 业务编码
    [MaxLength(100)]
    public string Code { get; set; } = Guid.NewGuid().ToString("N");
    #endregion

    #region 多租户
    [MaxLength(50)]
    public string OrgCode { get; set; }
    #endregion

    #region 审计字段 - 创建信息
    public int? CreateID { get; set; }
    [MaxLength(50)]
    public string Creator { get; set; }
    public DateTime? CreateDate { get; set; } = DateTime.Now;
    #endregion

    #region 审计字段 - 修改信息
    public int? ModifyID { get; set; }
    [MaxLength(50)]
    public string Modifier { get; set; }
    public DateTime? ModifyDate { get; set; } = DateTime.Now;
    #endregion

    #region 审计字段 - 删除信息
    public int? DeleteID { get; set; }
    [MaxLength(50)]
    public string Deleter { get; set; }
    public DateTime? DeleteTime { get; set; } = DateTime.Now;
    #endregion

    #region 状态字段
    [MaxLength(50)]
    public string Status { get; set; } = "active";
    public bool Enable { get; set; } = true;
    #endregion

    #region 辅助字段
    [MaxLength(500)]
    public string Remark { get; set; }
    #endregion

    #region 前端常用字段（NotMapped）
    [NotMapped]
    public bool CheckFlag { get; set; }  // 表格选择框

    [NotMapped]
    public bool DeleteFlag { get; set; } = false;  // 逻辑删除标志
    #endregion

    #region 辅助方法
    public void FillCreateInfo(int userId, string userName, string orgCode = null)
    {
        CreateID = userId;
        Creator = userName;
        CreateDate = DateTime.Now;
        if (!string.IsNullOrEmpty(orgCode)) OrgCode = orgCode;
    }

    public void FillModifyInfo(int userId, string userName)
    {
        ModifyID = userId;
        Modifier = userName;
        ModifyDate = DateTime.Now;
    }

    public void MarkAsDeleted(int userId, string userName)
    {
        Enable = false;
        DeleteID = userId;
        Deleter = userName;
        DeleteTime = DateTime.Now;
    }

    public bool IsDeleted => !Enable && DeleteTime.HasValue;
    public bool IsDisabled => !Enable && !DeleteTime.HasValue;
    #endregion
}
```

#### 3.1.2 视图基类（BaseView）- 新增

```csharp
// YZH.Core/Entities/BaseView.cs（新增）
/// <summary>
/// 视图基类，用于列表展示
/// 特点：轻量、只读、高性能
/// </summary>
public class BaseView
{
    /// <summary>
    /// 主键（继承自实体）
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 业务编码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreateDate { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    public string Creator { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// 是否已删除（计算属性）
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 前端选择标志
    /// </summary>
    [NotMapped]
    public bool CheckFlag { get; set; }
}
```

#### 3.1.3 视图与实体映射

```csharp
// 视图定义示例
[Entity(TableCnName = "认证机构视图", TableName = "v_certification_body", DBServer = "VOLContext")]
public class CertificationBodyView : BaseView
{
    public string Name { get; set; }
    public string ShortName { get; set; }
    public string CbCode { get; set; }
    public string ContactName { get; set; }
    public string ContactPhone { get; set; }
}

// 实体定义示例
[Entity(TableCnName = "认证机构", TableName = "cert_certification_body", DBServer = "VOLContext")]
public class CertificationBody : YZHBaseEntity
{
    [Required]
    [StringLength(200)]
    [Editable(true)]
    public string Name { get; set; }

    [StringLength(100)]
    [Editable(true)]
    public string ShortName { get; set; }

    [StringLength(50)]
    [Editable(true)]
    public string CbCode { get; set; }

    [StringLength(50)]
    [Editable(true)]
    public string ContactName { get; set; }

    [StringLength(20)]
    [Editable(true)]
    public string ContactPhone { get; set; }
}
```

---

### 3.2 拦截器设计（Phase 2 实现）

#### 3.2.1 审计拦截器

```csharp
// YZH.Core/Interceptors/YZHAuditedInterceptor.cs（新增）
public class YZHAuditedInterceptor : IInterceptor
{
    private readonly IUserContext _userContext;

    public YZHAuditedInterceptor(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public void Intercept(IInvocation invocation)
    {
        var entity = invocation.Arguments.FirstOrDefault() as YZHBaseEntity;
        if (entity == null)
        {
            invocation.Proceed();
            return;
        }

        // 根据方法名判断操作类型
        switch (invocation.Method.Name)
        {
            case "AddAsync":
            case "InsertAsync":
                entity.FillCreateInfo(
                    _userContext.UserId,
                    _userContext.UserName,
                    _userContext.OrgCode
                );
                break;

            case "UpdateAsync":
            case "EditAsync":
                entity.FillModifyInfo(
                    _userContext.UserId,
                    _userContext.UserName
                );
                break;

            case "DeleteAsync":
            case "RemoveAsync":
                if (entity is YZHBaseEntity baseEntity)
                {
                    baseEntity.MarkAsDeleted(
                        _userContext.UserId,
                        _userContext.UserName
                    );
                }
                break;
        }

        invocation.Proceed();
    }
}
```

#### 3.2.2 编码规则拦截器

```csharp
// YZH.Core/Interceptors/YZHCodeRuleInterceptor.cs（新增）
public class YZHCodeRuleInterceptor : IInterceptor
{
    private readonly ICodeRuleService _codeRuleService;

    public YZHCodeRuleInterceptor(ICodeRuleService codeRuleService)
    {
        _codeRuleService = codeRuleService;
    }

    public void Intercept(IInvocation invocation)
    {
        var entity = invocation.Arguments.FirstOrDefault() as YZHBaseEntity;
        if (entity == null)
        {
            invocation.Proceed();
            return;
        }

        // 检查实体是否有 [YZHCodeRule] 特性
        var codeRuleAttr = invocation.Method.DeclaringType?
            .GetCustomAttribute<YZHCodeRuleAttribute>();

        if (codeRuleAttr != null && string.IsNullOrEmpty(entity.Code))
        {
            var config = new CodeRuleConfig
            {
                Prefix = codeRuleAttr.Prefix,
                DateFormat = codeRuleAttr.DateFormat,
                SerialLength = codeRuleAttr.SerialLength,
                ResetRule = codeRuleAttr.ResetRule
            };

            entity.Code = _codeRuleService.Generate(config);
        }

        invocation.Proceed();
    }
}
```

#### 3.2.3 拦截器注册

```csharp
// YZH.Core/YZHModule.cs（修改）
protected override void Load(ContainerBuilder builder)
{
    // 注册拦截器
    builder.RegisterModule(new YZHAuditedInterceptorModule());
    builder.RegisterModule(new YZHCodeRuleInterceptorModule());

    // 注册服务
    builder.RegisterType<YzhPageConfigService>()
           .As<IYzhPageConfigService>()
           .InstancePerLifetimeScope();

    // 为 YZHServiceBase 添加拦截器
    builder.RegisterGeneric(typeof(YZHServiceBase<,>))
           .As(typeof(IYZHService<,>))
           .InstancePerLifetimeScope()
           .InterceptedBy(typeof(YZHAuditedInterceptor))
           .EnableInterfaceInterceptors();
}
```

---

### 3.3 扩展方法设计

#### 3.3.1 HTTP 请求扩展方法

```csharp
// YZH.Api.Core/Extensions/HttpRequestExtensions.cs
public static class HttpRequestExtensions
{
    /// <summary>
    /// 获取请求数据并反序列化为实体
    /// </summary>
    public static T GetRequestEntity<T>(this HttpRequest request) where T : BaseEntity, new()
    {
        var data = request.GetRequestData();
        return JsonHelper.Deserialize<T>(data.PostParams);
    }

    /// <summary>
    /// 获取请求数据并反序列化为列表
    /// </summary>
    public static List<T> GetRequestEntities<T>(this HttpRequest request) where T : BaseEntity, new()
    {
        var data = request.GetRequestData();
        return JsonHelper.Deserialize<List<T>>(data.PostParams);
    }

    /// <summary>
    /// 执行插入操作
    /// </summary>
    public static ApiResult Insert<T>(this HttpRequest request) where T : BaseEntity, new()
    {
        var entity = request.GetRequestEntity<T>();
        return DbHelper.Insert(entity);
    }

    /// <summary>
    /// 执行更新操作
    /// </summary>
    public static ApiResult Update<T>(this HttpRequest request) where T : BaseEntity
    {
        var entity = request.GetRequestEntity<T>();
        return DbHelper.Update(entity);
    }

    /// <summary>
    /// 执行删除操作
    /// </summary>
    public static ApiResult Delete<T>(this HttpRequest request) where T : BaseEntity
    {
        var entity = request.GetRequestEntity<T>();
        return DbHelper.Delete(entity);
    }
}
```

#### 3.3.2 实体扩展方法

```csharp
// YZH.Core/Extensions/BaseEntityExtensions.cs
public static class BaseEntityExtensions
{
    /// <summary>
    /// 将实体映射为视图
    /// </summary>
    public static TView ToView<TView>(this YZHBaseEntity entity) where TView : BaseView, new()
    {
        return new TView
        {
            Id = entity.Id,
            Code = entity.Code,
            CreateDate = entity.CreateDate,
            Creator = entity.Creator,
            Status = entity.Status,
            IsDeleted = entity.IsDeleted,
            CheckFlag = entity.CheckFlag
            // 其他字段使用 AutoMapper 或手动映射
        };
    }

    /// <summary>
    /// 批量将实体映射为视图列表
    /// </summary>
    public static List<TView> ToViewList<TView>(this List<YZHBaseEntity> entities) where TView : BaseView, new()
    {
        return entities.Select(e => e.ToView<TView>()).ToList();
    }

    /// <summary>
    /// 判断实体是否允许删除
    /// </summary>
    public static bool CanDelete(this YZHBaseEntity entity)
    {
        // 检查是否有子记录关联
        // 这里可以通过反射或特性声明
        return !entity.IsDeleted;
    }
}
```

---

### 3.4 特性声明设计

#### 3.4.1 审计特性

```csharp
// YZH.Core/Attributes/YZHAuditedAttribute.cs
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHAuditedAttribute : Attribute
{
    public bool TrackChanges { get; set; } = false;
    public AuditCategory Category { get; set; } = AuditCategory.General;
    public AuditScope Scope { get; set; } = AuditScope.Crud;
    public string SensitiveFields { get; set; }
    public string ExcludeFields { get; set; }
}

public enum AuditCategory
{
    General = 0,
    Certification = 100,
    Audit = 200,
    Report = 300,
    System = 400,
    Enterprise = 500
}

public enum AuditScope
{
    Crud = 0,
    Audit = 1,
    All = 2
}
```

#### 3.4.2 编码规则特性

```csharp
// YZH.Core/Attributes/YZHCodeRuleAttribute.cs
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHCodeRuleAttribute : Attribute
{
    public string Prefix { get; set; }
    public string DateFormat { get; set; } = "yyyyMM";
    public int SerialLength { get; set; } = 4;
    public SerialResetRule ResetRule { get; set; } = SerialResetRule.Monthly;
    public string Separator { get; set; } = "";
}

public enum SerialResetRule
{
    None = 0,
    Daily = 1,
    Monthly = 2,
    Yearly = 3
}
```

#### 3.4.3 权限控制特性

```csharp
// YZH.Core/Attributes/YZHPermissionAttribute.cs
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class YZHPermissionAttribute : Attribute
{
    public string PermissionCode { get; set; }
    public string ErrorMessage { get; set; } = "您没有权限执行此操作";
}

// 使用示例
[YZHPermission(PermissionCode = "Cert:Body:Delete")]
public async Task<IActionResult> Delete(long id)
{
    // ...
}
```

---

## 四、配置管理设计

### 4.1 配置表结构

#### 4.1.1 页面配置表（yzh_page_config）

```sql
CREATE TABLE `yzh_page_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `PageKey` varchar(50) NOT NULL COMMENT '页面唯一标识',
  `PageTitle` varchar(100) NOT NULL COMMENT '页面标题',
  `EntityName` varchar(100) NOT NULL COMMENT '实体名称',
  `TableName` varchar(100) NOT NULL COMMENT '数据库表名',
  `ControllerName` varchar(100) NOT NULL COMMENT '控制器名称',
  `KeyField` varchar(50) DEFAULT 'Id' COMMENT '主键字段',
  `KeyFieldType` varchar(10) DEFAULT 'number' COMMENT '主键类型',
  `SortField` varchar(50) DEFAULT 'Id' COMMENT '默认排序字段',
  `SortOrder` varchar(5) DEFAULT 'desc' COMMENT '默认排序方向',
  `DialogWidth` int DEFAULT 960 COMMENT '弹窗宽度',
  `DialogMaxHeight` varchar(20) DEFAULT '85vh' COMMENT '弹窗最大高度',
  `DialogLabelWidth` int DEFAULT 120 COMMENT '弹窗标签宽度',
  `RowHeight` varchar(10) DEFAULT 'default' COMMENT '行高',
  `Stripe` tinyint DEFAULT 1 COMMENT '是否斑马纹',
  `ShowRowNumber` tinyint DEFAULT 1 COMMENT '是否显示行号',
  `SearchMode` varchar(10) DEFAULT 'fixed' COMMENT '搜索模式',
  `VisibleButtons` varchar(500) DEFAULT '["add","refresh","batchDelete","columnSetting"]' COMMENT '可见按钮',
  `ShowActionColumn` tinyint DEFAULT 1 COMMENT '是否显示操作列',
  `CheckboxSelection` tinyint DEFAULT 1 COMMENT '是否显示选择框',
  `IncrementalUpdate` tinyint DEFAULT 1 COMMENT '是否增量更新',
  `ConfigSource` varchar(20) DEFAULT 'Database' COMMENT '配置来源：Database/Xml/Json',
  `ConfigPath` varchar(255) DEFAULT NULL COMMENT '配置文件路径',
  `IsActive` tinyint DEFAULT 1 COMMENT '是否激活',
  `OrgCode` varchar(50) DEFAULT '' COMMENT '组织编码',
  `CreatedAt` datetime DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_page_key` (`PageKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='YZH 页面配置表';
```

#### 4.1.2 字段配置表（yzh_field_config）

```sql
CREATE TABLE `yzh_field_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `PageKey` varchar(50) NOT NULL COMMENT '页面标识',
  `FieldName` varchar(50) NOT NULL COMMENT '字段名',
  `FieldAlias` varchar(100) DEFAULT '' COMMENT '字段别名',
  
  -- 表格列配置
  `XsFlag` tinyint DEFAULT 1 COMMENT '是否显示',
  `ColumnSxh` int DEFAULT 0 COMMENT '排序号',
  `ColumnTitle` varchar(100) DEFAULT '' COMMENT '列标题',
  `ColumnWidth` int DEFAULT 120 COMMENT '列宽度',
  `ColumnFixed` varchar(10) DEFAULT '' COMMENT '固定列',
  `Sortable` tinyint DEFAULT 1 COMMENT '是否可排序',
  `ColumnFormatter` varchar(50) DEFAULT '' COMMENT '格式化器',
  `ShowOverflow` tinyint DEFAULT 1 COMMENT '是否显示省略号',
  `Align` varchar(10) DEFAULT 'left' COMMENT '对齐方式',
  
  -- 表单配置
  `BcFlag` tinyint DEFAULT 1 COMMENT '是否保存到实体',
  `FormTitle` varchar(100) DEFAULT '' COMMENT '表单标题',
  `ControlType` varchar(20) DEFAULT 'input' COMMENT '控件类型',
  `GridRow` int DEFAULT 0 COMMENT 'Grid 行',
  `GridCol` int DEFAULT 0 COMMENT 'Grid 列',
  `GridRowSpan` int DEFAULT 1 COMMENT 'Grid 行跨度',
  `GridColSpan` int DEFAULT 1 COMMENT 'Grid 列跨度',
  `Required` tinyint DEFAULT 0 COMMENT '是否必填',
  `MaxLength` int DEFAULT 0 COMMENT '最大长度',
  `Placeholder` varchar(200) DEFAULT '' COMMENT '占位符',
  `DefaultValue` varchar(500) DEFAULT '' COMMENT '默认值',
  `Readonly` tinyint DEFAULT 0 COMMENT '是否只读',
  `Disabled` tinyint DEFAULT 0 COMMENT '是否禁用',
  `Precision` int DEFAULT NULL COMMENT '精度',
  `MinVal` decimal(18,2) DEFAULT NULL COMMENT '最小值',
  `MaxVal` decimal(18,2) DEFAULT NULL COMMENT '最大值',
  `TextareaRows` int DEFAULT 3 COMMENT '文本域行数',
  
  -- 数据源
  `DataKey` varchar(50) DEFAULT NULL COMMENT '数据源键',
  `RemoteUrl` varchar(255) DEFAULT NULL COMMENT '远程 URL',
  
  -- 业务控制
  `GroupIndex` int DEFAULT 0 COMMENT '分组索引',
  
  -- 搜索配置
  `SearchFlag` tinyint DEFAULT 0 COMMENT '是否搜索',
  `SearchTitle` varchar(100) DEFAULT '' COMMENT '搜索标题',
  `SearchPlaceholder` varchar(100) DEFAULT '' COMMENT '搜索占位符',
  `SearchControlType` varchar(20) DEFAULT NULL COMMENT '搜索控件类型',
  `SearchWidth` int DEFAULT 180 COMMENT '搜索宽度',
  
  `OrgCode` varchar(50) DEFAULT '' COMMENT '组织编码',
  `CreatedAt` datetime DEFAULT NULL,
  `UpdatedAt` datetime DEFAULT NULL,
  `Remark` varchar(500) DEFAULT NULL,
  
  PRIMARY KEY (`Id`),
  KEY `idx_page_key` (`PageKey`),
  KEY `idx_field_name` (`PageKey`, `FieldName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='YZH 字段配置表';
```

---

## 五、TODO 清单

### 5.1 Phase 2.1：核心拦截器实现（优先级：P0）

- [ ] **T2.1.1** 实现 `YZHAuditedInterceptor`（审计拦截器）
  - 自动填充 CreateID/Creator/CreateDate
  - 自动填充 ModifyID/Modifier/ModifyDate
  - 自动填充 DeleteID/Deleter/DeleteTime
  - 位置：`YZH.Core/Interceptors/YZHAuditedInterceptor.cs`

- [ ] **T2.1.2** 实现 `YZHCodeRuleInterceptor`（编码规则拦截器）
  - 读取 [YZHCodeRule] 特性
  - 调用 ICodeRuleService 生成编码
  - 位置：`YZH.Core/Interceptors/YZHCodeRuleInterceptor.cs`

- [ ] **T2.1.3** 实现 `YZHPermissionInterceptor`（权限拦截器）
  - 读取 [YZHPermission] 特性
  - 校验用户权限
  - 位置：`YZH.Core/Interceptors/YZHPermissionInterceptor.cs`

- [ ] **T2.1.4** 注册拦截器到 Autofac 容器
  - 修改 `YZHModule.cs`
  - 为 YZHServiceBase 添加拦截器
  - 位置：`YZH.Core/YZHModule.cs`

- [ ] **T2.1.5** 编写单元测试
  - 测试审计字段自动填充
  - 测试编码规则自动生成
  - 测试权限校验
  - 位置：`YZH.Core.Tests/Interceptors/`

---

### 5.2 Phase 2.2：视图支持（优先级：P0）

- [ ] **T2.2.1** 创建 `BaseView` 基类
  - 定义视图通用字段（Id, Code, CreateDate, Creator, Status）
  - 位置：`YZH.Core/Entities/BaseView.cs`

- [ ] **T2.2.2** 实现实体到视图的映射扩展方法
  - `ToView<TView>()` 单个实体映射
  - `ToViewList<TView>()` 批量映射
  - 位置：`YZH.Core/Extensions/BaseEntityExtensions.cs`

- [ ] **T2.2.3** 创建视图示例（CertificationBodyView）
  - 定义视图实体
  - 编写映射测试
  - 位置：`YZH.CertPlatform/Entities/Cert/CertificationBodyView.cs`

- [ ] **T2.2.4** 更新前端组件支持视图
  - YzhCrudTable 组件增加 ViewMode 支持
  - 配置加载器支持视图类型
  - 位置：`vol.web/src/yzh/components/`

---

### 5.3 Phase 2.3：配置管理页面（优先级：P1）

- [ ] **T2.3.1** 创建配置管理控制器
  - YzhPageConfigController（页面配置）
  - YzhFieldConfigController（字段配置）
  - 位置：`VOL.WebApi/Controllers/CertPlatform/`

- [ ] **T2.3.2** 创建配置管理前端页面
  - 页面配置列表页
  - 字段配置列表页
  - 配置导入/导出功能
  - 位置：`vol.web/src/views/yzh/config/`

- [ ] **T2.3.3** 实现 XML 导入导出功能
  - 导出配置为 XML 文件
  - 导入 XML 文件更新配置
  - 位置：`YZH.CertPlatform/Services/YzhPageConfigService.cs`

- [ ] **T2.3.4** 编写配置管理测试
  - 测试导入导出功能
  - 测试配置加载
  - 位置：`YZH.Core.Tests/Services/`

---

### 5.4 Phase 2.4：扩展方法完善（优先级：P1）

- [ ] **T2.4.1** 实现 HttpRequest 扩展方法
  - GetRequestEntity<T>()
  - GetRequestEntities<T>()
  - Insert<T>()
  - Update<T>()
  - Delete<T>()
  - 位置：`YZH.Api.Core/Extensions/HttpRequestExtensions.cs`

- [ ] **T2.4.2** 实现数据库操作扩展方法
  - Insert<T>()
  - Update<T>()
  - Delete<T>()
  - Query<T>()
  - 位置：`YZH.Core/Extensions/DbHelperExtensions.cs`

- [ ] **T2.4.3** 实现分页查询扩展方法
  - PageQuery<T>()
  - 位置：`YZH.Core/Extensions/PaginationExtensions.cs`

---

### 5.5 Phase 2.5：特性体系完善（优先级：P2）

- [ ] **T2.5.1** 完善 [YZHAudited] 特性
  - 添加 TrackChanges 参数
  - 添加 Category 枚举
  - 添加 Scope 枚举
  - 位置：`YZH.Core/Attributes/YZHAuditedAttribute.cs`

- [ ] **T2.5.2** 完善 [YZHCodeRule] 特性
  - 添加 Prefix 参数
  - 添加 DateFormat 参数
  - 添加 SerialLength 参数
  - 位置：`YZH.Core/Attributes/YZHCodeRuleAttribute.cs`

- [ ] **T2.5.3** 新增 [YZHPermission] 特性
  - 添加 PermissionCode 参数
  - 添加 ErrorMessage 参数
  - 位置：`YZH.Core/Attributes/YZHPermissionAttribute.cs`

- [ ] **T2.5.4** 新增 [YZHValidation] 特性
  - 添加验证规则
  - 位置：`YZH.Core/Attributes/YZHValidationAttribute.cs`

---

### 5.6 Phase 2.6：文档与示例（优先级：P2）

- [ ] **T2.6.1** 更新 README.md
  - 添加拦截器使用说明
  - 添加视图使用示例
  - 添加配置管理说明
  - 位置：`YZH-Framework/README.md`

- [ ] **T2.6.2** 创建使用示例项目
  - 示例：认证机构管理
  - 示例：ISO 标准管理
  - 位置：`YZH.Examples/`

- [ ] **T2.6.3** 编写开发指南
  - 如何创建新实体
  - 如何创建新视图
  - 如何配置管理页面
  - 位置：`docs/60-AI工程设计/YZH-知识库/`

---

## 六、关键技术点

### 6.1 拦截器实现方案

**方案 A：Castle DynamicProxy（推荐）**
```csharp
// 优点：成熟稳定，支持类拦截和接口拦截
// 缺点：需要额外依赖

builder.RegisterGeneric(typeof(YZHServiceBase<,>))
       .As(typeof(IYZHService<,>))
       .InstancePerLifetimeScope()
       .InterceptedBy(typeof(YZHAuditedInterceptor))
       .EnableInterfaceInterceptors();
```

**方案 B：AspectCore**
```csharp
// 优点：轻量级，基于 DotNetCore 动态代理
// 缺点：需要额外配置

builder.AddAspectCoreInterceptor<YZHAuditedInterceptor>();
```

**方案 C：EF Core Change Tracker**
```csharp
// 优点：无需额外依赖，利用 EF Core 内置机制
// 缺点：只能在 SaveChanges 时触发

public override int SaveChanges()
{
    // 自动填充审计字段
    // ...
    return base.SaveChanges();
}
```

**推荐**：使用方案 A（Castle DynamicProxy），与 Autofac 集成最好。

---

### 6.2 视图与实体映射方案

**方案 A：手动映射（推荐用于简单场景）**
```csharp
public static TView ToView<TView>(this YZHBaseEntity entity) where TView : BaseView, new()
{
    return new TView
    {
        Id = entity.Id,
        Code = entity.Code,
        CreateDate = entity.CreateDate,
        Creator = entity.Creator,
        Status = entity.Status
        // 其他字段...
    };
}
```

**方案 B：AutoMapper（推荐用于复杂场景）**
```csharp
// 配置映射关系
CreateMap<YZHBaseEntity, BaseView>()
    .ForMember(dest => dest.CheckFlag, opt => opt.Ignore());

CreateMap<CertificationBody, CertificationBodyView>();
```

**方案 C：Mapster（推荐用于高性能场景）**
```csharp
// 编译时生成映射代码，性能最优
Configurator.NewConfig<YZHBaseEntity, BaseView>()
    .Ignore(dest => dest.CheckFlag);
```

**推荐**：简单场景用手动手动映射，复杂场景用 AutoMap，高性能需求用 Mapster。

---

### 6.3 配置管理方案

**方案 A：纯数据库配置（推荐）**
- 优点：动态加载，无需重启
- 缺点：需要管理页面维护

**方案 B：XML 文件 + 数据库缓存**
- 优点：版本控制方便，支持导入导出
- 缺点：需要处理缓存同步

**方案 C：混合模式**
- 默认：数据库配置
- 可选：XML 导入导出

**推荐**：使用方案 C（混合模式），兼顾灵活性和易用性。

---

## 七、实施计划

### Week 1：拦截器实现
- [ ] Day 1-2：实现 YZHAuditedInterceptor
- [ ] Day 3-4：实现 YZHCodeRuleInterceptor
- [ ] Day 5：编写单元测试

### Week 2：视图支持
- [ ] Day 1：创建 BaseView 基类
- [ ] Day 2-3：实现映射扩展方法
- [ ] Day 4-5：更新前端组件

### Week 3：配置管理
- [ ] Day 1-2：创建配置管理控制器
- [ ] Day 3-4：创建配置管理前端页面
- [ ] Day 5：实现 XML 导入导出

### Week 4：完善与测试
- [ ] Day 1-2：完善扩展方法
- [ ] Day 3-4：编写集成测试
- [ ] Day 5：更新文档

---

## 八、总结

YZH-Framework V2.0 的核心升级点：

1. **配置驱动**：所有 UI 配置存储到数据库，支持管理页面维护
2. **视图分离**：列表使用视图（轻量），编辑使用实体（完整）
3. **拦截器驱动**：自动填充审计字段、自动生成编码、自动权限校验
4. **扩展方法**：减少重复代码，提高开发效率
5. **特性声明**：声明式配置，代码简洁

通过这些设计，我们可以在保持企业级功能的同时，大幅提高开发效率，减少重复代码。

---

**文档版本**: V2.0  
**最后更新**: 2026-08-08  
**下次更新**: Phase 2 实施完成后
