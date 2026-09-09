---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_32e54dcd8ca711f19986525400287e28
    ReservedCode1: n9aw01eNTRlOheWyu6/AP3KrJjdcUlj95HyqGUKqWJmewzv+eW4G8VD0tdY6d5NeQr0bZPFYXD7VRUALiqi5a9GSqd4+peUBt9HPFIYiTvI0zPOGmJiYkTmtL5b4csQiJdtt04Xijv9a04eb13yj96p1yNNCRWmNxG0SfMDg7PCkYSGfmKDKCpd+mqg=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_32e54dcd8ca711f19986525400287e28
    ReservedCode2: n9aw01eNTRlOheWyu6/AP3KrJjdcUlj95HyqGUKqWJmewzv+eW4G8VD0tdY6d5NeQr0bZPFYXD7VRUALiqi5a9GSqd4+peUBt9HPFIYiTvI0zPOGmJiYkTmtL5b4csQiJdtt04Xijv9a04eb13yj96p1yNNCRWmNxG0SfMDg7PCkYSGfmKDKCpd+mqg=
---

# 02 - YZH 增量清单

**版本**：V1.1  
**日期**：2026-07-31  
**最后更新**：2026-07-31（V1.1：同步 Phase 1 实现完成状态，补充完整接口签名）  
**状态**：正式发布

> **说明**：本清单列出 YZH 在 Vol 之上新增的所有能力。每个条目包含完整的接口签名、参数说明、使用示例和当前实现状态。

---

## ✅ 已完成组件（Phase 1）

### 1. YZHBaseEntity（实体基类）

- **文件位置**：`YZH.Core/Entities/YZHBaseEntity.cs`
- **继承关系**：`YZHBaseEntity → Vol.BaseEntity`（空基类）
- **状态**：✅ **DONE**（Phase 1 完整实现）
- **新增字段数量**：12 个统一字段 + 4 个辅助方法

#### 字段列表

| 分类 | 字段名 | 类型 | 默认值 | 说明 | 自动填充 |
|------|--------|------|--------|------|---------|
| **业务编码** | Code | `string` | null | 业务标识（非主键） | ICodeRule |
| **多租户** | OrgCode | `string` | null | 组织编码 | UserContext |
| **创建信息** | CreateID | `int?` | null | 创建人 ID | ✅ 新增时 |
| | Creator | `string` | null | 创建人姓名 | ✅ 新增时 |
| | CreateDate | `DateTime?` | null | 创建时间 | ✅ 新增时 |
| **修改信息** | ModifyID | `int?` | null | 修改人 ID | ✅ 更新时 |
| | Modifier | `string` | null | 修改人姓名 | ✅ 更新时 |
| | ModifyDate | `DateTime?` | null | 修改时间 | ✅ 更新时 |
| **删除信息** | DeleteID | `int?` | null | 删除人 ID | ✅ 删除时 |
| | Deleter | `string` | null | 删除人姓名 | ✅ 删除时 |
| | DeleteTime | `DateTime?` | null | 删除时间 | ✅ 删除时 |
| **状态辅助** | Enable | `bool` | true | 启用状态 | 新增时默认 |
| | Sort | `int` | 0 | 排序号 | 手动设置 |
| | Remark | `string` | null | 备注 | 手动设置 |

#### 辅助方法签名

```csharp
// 判断是否已逻辑删除
public bool IsDeleted { get; }  // !Enable && DeleteTime.HasValue

// 判断是否被禁用但未删除
public bool IsDisabled { get; } // !Enable && !DeleteTime.HasValue

// 标记为逻辑删除（由框架调用）
public void MarkAsDeleted(int userId, string userName)
// 效果：Enable=false, DeleteID=userId, Deleter=userName, DeleteTime=Now

// 标记为禁用（不记录删除信息）
public void MarkAsDisabled()
// 效果：Enable=false，不设置 DeleteID/DeleteTime

// 填充创建信息（由 YZHServiceBase 调用）
public void FillCreateInfo(int userId, string userName, string orgCode = null)

// 填充修改信息（由 YZHServiceBase 调用）
public void FillModifyInfo(int userId, string userName)
```

#### 使用示例

```csharp
// 定义业务实体
[YZHCodeRule(Prefix = "CB", DateFormat = "yyyyMM")]
[YZHAudited(TrackChanges: true)]
public class CertificationBody : YZHBaseEntity
{
    public string Name { get; set; }
}

// 在 Service 中使用（审计字段自动填充）
var entity = new CertificationBody { Name = "测试机构" };
entity.FillCreateInfo(1, "管理员", "CB001");
// 结果：CreateID=1, Creator="管理员", CreateDate=现在, OrgCode="CB001"

entity.FillModifyInfo(1, "管理员");
// 结果：ModifyID=1, Modifier="管理员", ModifyDate=现在

entity.MarkAsDeleted(1, "管理员");
// 结果：Enable=false, DeleteID=1, Deleter="管理员", DeleteTime=现在
```

---

### 2. YZHAuditedAttribute（审计标注特性）

- **文件位置**：`YZH.Core/Audit/YZHAuditedAttribute.cs`
- **状态**：✅ **DONE**（接口定义完整，TODO:P2 实现写入逻辑）

#### 完整接口签名

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHAuditedAttribute : Attribute
{
    // 基础配置
    public bool TrackChanges { get; set; } = false;      // 是否记录字段变更
    public string TableName { get; set; } = null;          // 自定义审计表名
    
    // 分类与范围
    public AuditCategory Category { get; set; } = AuditCategory.General;
    public AuditScope Scope { get; set; } = AuditScope.Crud;
    
    // 敏感字段配置
    public string SensitiveFields { get; set; } = null;    // 需脱敏的字段列表
    public string ExcludeFields { get; set; } = null;      // 排除的字段列表
}
```

#### 枚举定义

```csharp
public enum AuditCategory
{
    General = 0,        // 通用
    Certification = 100, // 认证管理
    Audit = 200,         // 审核流程
    Report = 300,        // 报告生成
    System = 400,        // 系统管理
    Enterprise = 500     // 企业端操作
}

public enum AuditScope
{
    Crud = 0,   // 基础 CRUD 操作记录
    Audit = 1,  // 增强（+ 业务上下文）
    All = 2     // 完整（+ 查询 + diff）
}
```

#### 使用示例

```csharp
[YZHAudited(
    TrackChanges: true,
    Category: AuditCategory.Certification,
    Scope: AuditScope.Audit,
    SensitiveFields: "MobilePhone,IDCard",
    ExcludeFields: "Remark")]
public class CertificationBody : YZHBaseEntity { }
```

---

### 3. ICodeRule（编码规则引擎）

- **文件位置**：`YZH.Core/CodeRule/ICodeRule.cs`
- **状态**：✅ **DONE**（接口定义完整，TODO:P2 实现生成算法）

#### 完整接口签名

```csharp
public interface ICodeRule
{
    // 同步生成（单机环境使用 lock 保证并发安全）
    string Generate(CodeRuleConfig config);
    
    // 异步生成（分布式环境使用 Redis 分布式锁）
    Task<string> GenerateAsync(CodeRuleConfig config);
    
    // 验证编码是否符合规则
    bool Validate(string code, CodeRuleConfig config);
    
    // 解析编码，提取各组成部分
    CodeRuleParseResult Parse(string code, CodeRuleConfig config);
}
```

#### 配置类

```csharp
public class CodeRuleConfig
{
    public string Prefix { get; set; }              // 前缀（必填）
    public string DateFormat { get; set; } = "yyyyMM"; // 日期格式
    public int SerialLength { get; set; } = 4;       // 序列号位数
    public SerialResetRule ResetRule { get; set; } = SerialResetRule.Monthly;
    public int StartSerial { get; set; } = 1;        // 起始值
    public string Separator { get; set; } = "";       // 分隔符
    public bool IncludeCheckDigit { get; set; } = false; // 校验位
    public Type EntityType { get; set; }              // 关联实体类型
}

public enum SerialResetRule { None, Daily, Monthly, Yearly }

public class CodeRuleParseResult
{
    public bool Success { get; set; }
    public string Prefix { get; set; }
    public DateTime? Date { get; set; }
    public int? SerialNumber { get; set; }
    public string OriginalCode { get; set; }
}
```

#### 特性声明

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHCodeRuleAttribute : Attribute
{
    public string Prefix { get; set; }           // 必填
    public string DateFormat { get; set; } = "yyyyMM";
    public int SerialLength { get; set; } = 4;
    public SerialResetRule ResetRule { get; set; } = SerialResetRule.Monthly;
    public string Separator { get; set; } = "";
}
```

#### 使用示例

```csharp
[YZHCodeRule(Prefix: "CB", DateFormat: "yyyyMM", SerialLength: 4)]
public class CertificationBody : YZHBaseEntity { }

// 生成结果：
// CB2026070001, CB2026070002, ..., CB2026079999, CB2026080001（每月重置）
```

---

### 4. IDeleteStrategy（删除策略）

- **文件位置**：`YZH.Core/DeleteStrategy/IDeleteStrategy.cs`
- **状态**：✅ **DONE**（接口定义完整，TODO:P3 实现执行逻辑）

#### 完整接口签名

```csharp
public interface IDeleteStrategy
{
    DeleteMode Mode { get; }
    bool CanDelete(object entityId);
    void ExecuteDelete(object entity, int userId, string userName);
}

public enum DeleteMode
{
    Logical = 0,  // 逻辑删除（默认，推荐）
    Physical = 1, // 物理删除
    Cascade = 2   // 级联删除
}
```

#### 特性声明

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHDeleteStrategyAttribute : Attribute
{
    public DeleteMode Mode { get; set; } = DeleteMode.Logical;
    public Type[] CascadeEntities { get; set; } = null;  // 级联实体类型
    public bool ForceDelete { get; set; } = false;        // 强制删除
    public string ValidationMethod { get; set; } = null;  // 自定义校验方法
}
```

#### 使用示例

```csharp
// 默认逻辑删除（无需声明）
public class CertificationBody : YZHBaseEntity { }

// 物理删除
[YZHDeleteStrategy(Mode: DeleteMode.Physical)]
public class TempFile : YZHBaseEntity { }

// 级联删除
[YZHDeleteStrategy(Mode: DeleteMode.Cascade, CascadeEntities: typeof(Detail[]))]
public class Order : YZHBaseEntity { }
```

---

### 5. YZHValidationAttribute（校验特性基类）

- **文件位置**：`YZH.Core/Validation/YZHValidationAttribute.cs`
- **状态**：✅ **DONE**（抽象基类 + 内置特性声明完成，TODO:P2 实现校验逻辑）

#### 完整接口签名

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public abstract class YZHValidationAttribute : Attribute
{
    public string ErrorMessage { get; set; }             // 错误消息（支持 {fieldName} 占位符）
    public int Priority { get; set; } = 100;             // 优先级（越小越先执行）
    public string Group { get; set; } = null;             // 校验分组
    
    // 抽象方法（子类必须实现）
    public abstract ValidationResult Validate(object value, string fieldName, object entity);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
    public string FieldName { get; set; }
    public int Priority { get; set; }
    
    public static ValidationResult Success();
    public static ValidationResult Fail(string errorMessage, string fieldName, int priority = 100);
}
```

#### 内置校验特性（TODO:P2 实现）

```csharp
// 必填校验（支持条件必填）
public class YZHRequiredAttribute : YZHValidationAttribute
{
    public string Condition { get; set; } = null;  // 条件表达式
}

// 唯一性校验（支持联合唯一）
public class YZHUniqueAttribute : YZHValidationAttribute
{
    public string[] WithFields { get; set; } = null;
}

// 长度校验
public class YZHLengthAttribute : YZHValidationAttribute
{
    public int MaximumLength { get; set; }
    public int? MinimumLength { get; set; }
}

// 正则表达式校验（预置常用模式）
public class YZHRegexAttribute : YZHValidationAttribute
{
    public string Pattern { get; set; }
    public string PredefinedPattern { get; set; } = null;  // MobilePhone / Email / IdCard
}

// 范围校验（数值/日期）
public class YZHRangeAttribute : YZHValidationAttribute
{
    public object Minimum { get; set; }
    public object Maximum { get; set; }
}
```

#### 使用示例

```csharp
public class CertificationBody : YZHBaseEntity
{
    [YZHRequired("机构名称不能为空")]
    public string Name { get; set; }
    
    [YZHUnique("统一社会信用代码已存在")]
    public string CreditCode { get; set; }
    
    [YZHLength(50, "机构简称不能超过50个字符")]
    public string ShortName { get; set; }
    
    [YZHRegex(PredefinedPattern: "MobilePhone", ErrorMessage: "手机号格式不正确")]
    public string ContactPhone { get; set; }
}
```

---

## 🔜 待实现组件（Phase 2）

### 6. YZHServiceBase（服务基类）

- **文件位置**：`YZH.Core/BaseProvider/YZHServiceBase.cs`（待创建）
- **继承关系**：`YZHServiceBase<TEntity, TRepository> → Vol.ServiceBase<TEntity, TRepository>`
- **状态**：🔜 TODO:P2

#### 设计目标

```csharp
public abstract class YZHServiceBase<TEntity, TRepository> 
    : ServiceBase<TEntity, TRepository>
    where TEntity : YZHBaseEntity, new()
{
    // 封装 Vol 的 Func 钩子为虚方法（更符合 OOP 风格）
    protected virtual Task OnBeforeSaveAsync(TEntity entity) { ... }
    protected virtual Task OnAfterSaveAsync(TEntity entity) { ... }
    protected virtual Task OnBeforeDeleteAsync(TEntity entity) { ... }
    protected virtual Task OnAfterDeleteAsync(TEntity entity) { ... }
    
    // 读取实体级 YZH 特性并自动执行
    protected virtual void ApplyAttributes(TEntity entity) { ... }
    
    // 查询前自动追加多租户过滤条件
    protected virtual IQueryable<TEntity> ApplyTenantFilter(IQueryable<TEntity> query) { ... }
    
    // 写操作后自动写入审计日志
    protected virtual async Task WriteAuditLogAsync(TEntity oldEntity, TEntity newEntity, string operation) { ... }
    
    // 重写基础 CRUD 方法，在关键节点插入钩子
    public override async Task<TEntity> AddAsync(TEntity entity) { ... }
    public override async Task<bool> UpdateAsync(TEntity entity, List<string> fields = null) { ... }
    public override async Task<bool> DelAsync(object[] keys) { ... }
}
```

---

### 7. YZHControllerBase（控制器基类）

- **文件位置**：`YZH.Core/Controllers/YZHControllerBase.cs`（待创建）
- **状态**：🔜 TODO:P2

#### 设计目标

```csharp
public class YZHControllerBase : Vol.ApiBaseController
{
    // 统一响应格式转换
    protected IActionResult YZHOk(object data = null, string message = "成功");
    protected IActionResult YZHError(string message, int statusCode = 400);
    protected IActionResult YZHNotFound(string message = "资源不存在");
    protected IActionResult YZHForbidden(string message = "无权限访问");
    
    // 分页查询封装
    protected async Task<IActionResult> YZHPagedAsync<T>(IQueryable<T> query, PageRequest request);
}
```

---

### 8. YZHGlobalExceptionFilter（全局异常过滤器）

- **文件位置**：`YZH.Core/Filters/YZHGlobalExceptionFilter.cs`（待创建）
- **接口**：`IAsyncActionFilter`
- **状态**：🔜 TODO:P2

#### 异常层次体系

```csharp
// 业务异常（HTTP 400）
public class YZHBusinessException : Exception { ... }

// 校验异常（HTTP 400）
public class YZHValidationException : Exception 
{ 
    public ValidationResult[] Errors { get; set; }
}

// 认证异常（HTTP 401/403）
public class YZHAuthenticationException : Exception { ... }

// 未找到异常（HTTP 404）
public class YZHNotFoundException : Exception { ... }
```

---

### 9. YZHMultiTenantAttribute（多租户特性）

- **文件位置**：`YZH.Core/Attributes/YZHMultiTenantAttribute.cs`（待创建）
- **状态**：🔜 TODO:P2

#### 设计目标

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHMultiTenantAttribute : Attribute
{
    public string PropertyName { get; set; } = "OrgCode";  // 租户字段名
    public bool AutoFilter { get; set; } = true;            // 是否自动过滤查询
    public bool AutoFill { get; set; } = true;              // 是否自动填充新建数据
}
```

---

### 10. YZHIdempotentAttribute（接口幂等性）

- **文件位置**：`YZH.Core/Attributes/YZHIdempotentAttribute.cs`（待创建）
- **配套**：`YZHIdempotentActionFilter`、`IIdempotentKeyGenerator`
- **状态**：🔜 TODO:P3

#### 设计目标

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class YZHIdempotentAttribute : Attribute
{
    public int DurationSeconds { get; set; } = 3;           // 防重复窗口（秒）
    public string Message { get; set; } = "操作过于频繁，请稍后再试";
    public string KeyPrefix { get; set; } = "yzh:idem:";    // Redis Key 前缀
    public bool IncludeBodyHash { get; set; } = true;       // 是否包含请求体哈希
    public Type KeyGeneratorType { get; set; } = null;      // 自定义键生成器
}
```

---

## 📋 统计汇总

| 组件类别 | 已完成 | 待实现 | 总计 | 完成率 |
|---------|--------|--------|------|--------|
| **实体与基础** | 1 (YZHBaseEntity) | 0 | 1 | 100% |
| **特性定义** | 4 (Audited/CodeRule/DeleteStrategy/Validation) | 2 (MultiTenant/Idempotent) | 6 | 67% |
| **服务基类** | 0 | 2 (ServiceBase/ControllerBase) | 2 | 0% |
| **过滤器** | 0 | 2 (GlobalException/IdempotentFilter) | 2 | 0% |
| **总计** | **5** | **6** | **11** | **45%** |

---

*（内容由 AI 生成，仅供参考。最后更新：2026-07-31 by AI Assistant）*
