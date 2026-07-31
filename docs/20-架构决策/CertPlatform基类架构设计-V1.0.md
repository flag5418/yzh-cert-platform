# CertPlatform 基类架构设计文档

> **版本**：V1.0  
> **日期**：2026-07-31  
> **状态**：📋 待评审  
> **定位**：定义 CertPlatform 模块的后端/前端基类生命周期，实现"一套逻辑走天下"

---

## 一、设计目标

### 1.1 核心问题

| 痛点 | 描述 |
|------|------|
| **代码重复** | 30+ 张业务表，每张表需要 5 个后端文件 + 1 个前端文件 |
| **样板代码** | Service/Controller 中 80% 内容是相同的 CRUD 逻辑 |
| **维护成本** | 需求变更时需要修改大量文件，容易遗漏 |
| **树形结构** | 多处使用"左树右表"模式，但每次都重新实现 |

### 1.2 设计目标

| 目标 | 指标 |
|------|------|
| **减少重复代码** | 相同逻辑只写一次，通过继承/组合复用 |
| **生命周期清晰** | 每个操作阶段都有明确的钩子（Hook） |
| **简单场景零代码** | 标准 CRUD 只需配置，无需编写业务代码 |
| **复杂场景可扩展** | 通过重写钩子方法实现特殊逻辑 |
| **树形结构抽象** | 左树右表模式组件化，只需配置 |

### 1.3 设计原则

```
┌─────────────────────────────────────────────────────────────┐
│                     设计原则（优先级从高到低）                 │
│                                                             │
│  1️⃣  约定优于配置     合理的默认值，最小化配置量              │
│  2️⃣  开放-封闭原则   对扩展开放，对修改关闭                   │
│  3️⃣  单一职责       每个钩子方法只做一件事                    │
│  4️⃣  依赖倒置       依赖抽象接口，不依赖具体实现               │
│  5️⃣  Vol 兼容性      复用 Vol 框架已有能力，不重复造轮子        │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、整体架构

### 2.1 分层架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        前端 (Vue 3)                              │
│                                                                 │
│  ┌─────────────────────┐    ┌─────────────────────┐             │
│  │   TreeCrud.vue      │───▶│   GenericCrud.vue   │             │
│  │   (树形 + CRUD)      │    │   (通用 CRUD 页面)   │             │
│  └─────────────────────┘    └─────────────────────┘             │
│            ▲                          ▲                         │
│            │          配置驱动         │                         │
│  ┌─────────┴──────────┐  ┌────────────┴─────────┐               │
│  │  tree-config.ts    │  │  crud-config.ts      │               │
│  │  (树形配置)         │  │  (CRUD 配置)          │               │
│  └────────────────────┘  └──────────────────────┘               │
└─────────────────────────────────────────────────────────────────┘
                                  │ HTTP
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                      后端 (.NET 8)                               │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │              CertControllerBase<TEntity>                │    │
│  │              (控制器基类 - 自动注册路由)                   │    │
│  └───────────────────────┬─────────────────────────────────┘    │
│                          │ 继承                                 │
│  ┌───────────────────────▼─────────────────────────────────┐    │
│  │              CertServiceBase<TEntity>                   │    │
│  │              (服务基类 - 生命周期管理)                     │    │
│  └───────────────────────┬─────────────────────────────────┘    │
│                          │ 继承                                 │
│  ┌───────────────────────▼─────────────────────────────────┐    │
│  │           Vol 框架 (ServiceBase / ApiBaseController)     │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 文件结构对比

| 类型 | 当前方案 | 抽象方案 | 节省 |
|------|---------|---------|------|
| **后端文件/表** | 5 个 | 0-1 个 | -80% |
| **前端文件/表** | 1 个 vue (~300行) | 1 个 config (~80行) | -73% |
| **总代码量/30表** | ~15,000 行 | ~4,000 行 | **-73%** |

---

## 三、后端基类设计

### 3.1 泛型约束

```csharp
/// <summary>
/// CertPlatform 业务实体必须满足的条件
/// </summary>
public class CertServiceBase<TEntity> : ServiceBase<TEntity, ICertRepository<TEntity>>
    where TEntity : BaseEntity, new()
{
    // TEntity 必须继承 BaseEntity（包含 Id, Code, 审计字段等）
}
```

### 3.2 Service 生命周期（完整）

#### 📖 查询生命周期 (GetPageData)

```
请求: POST api/{Entity}/getPageData
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│ ① OnQueryStart(PageDataOptions)                             │
│    用途：权限检查、参数预处理、操作日志记录                     │
│    返回：void（抛出异常则终止）                                │
│    默认实现：空                                              │
├─────────────────────────────────────────────────────────────┤
│ ② OnBuildQuery(IQueryable<TEntity>)                         │
│    用途：构建基础查询（多租户过滤、软删除过滤）                  │
│    返回：修改后的 IQueryable                                  │
│    默认实现：                                                │
│      - 过滤 DeleteTime == null（未删除数据）                   │
│      - 如果有 OrgCode，添加机构过滤                            │
├─────────────────────────────────────────────────────────────┤
│ ③ OnQueryFilter(IQueryable<TEntity>)                        │
│    用途：业务相关的额外过滤条件                                │
│    返回：修改后的 IQueryable                                  │
│    默认实现：返回原查询                                       │
├─────────────────────────────────────────────────────────────┤
│ ④ [框架执行] base.GetPageData()                             │
│    用途：执行分页查询、排序、统计                              │
│    返回：PageGridData<TEntity>                               │
├─────────────────────────────────────────────────────────────┤
│ ⑤ OnQueryExecuted(PageGridData<TEntity>)                    │
│    用途：结果后处理（计算字段、格式转换、附加关联数据）           │
│    返回：修改后的 PageGridData                                │
│    默认实现：返回原数据                                       │
├─────────────────────────────────────────────────────────────┤
│ ⑥ OnResponseBuilding(object)                                │
│    用途：构建最终响应（包装附加信息、字典预加载）                │
│    返回：WebResponseContent                                   │
│    默认实现：返回标准响应                                     │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
响应: { code: 0, data: {...} }
```

#### 💾 保存生命周期 (Add / Update)

```
请求: POST api/{Entity}/Add 或 Update
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① OnSaveStart(SaveModel, SaveMode)                           │
│   用途：判断操作类型、初始化上下文                             │
│   参数：SaveModel = 原始数据, SaveMode = Add | Update        │
│   返回：void                                                │
│   默认实现：空                                               │
├─────────────────────────────────────────────────────────────┤
│② OnValidate(TEntity)                                        │
│   用途：数据校验                                             │
│   - 必填字段检查                                              │
│   - 唯一性约束检查                                           │
│   - 业务规则校验                                             │
│   返回：(bool success, string errorMessage)                  │
│   默认实现：基本必填检查                                      │
│   ⚠️ 如果返回 false，终止保存并返回错误                       │
├─────────────────────────────────────────────────────────────┤
│③ OnBeforeSave(TEntity, SaveMode)                            │
│   用途：保存前处理                                           │
│   - 设置默认值（Status = "active"）                           │
│   - 填充审计字段（CreateBy, CreateTime）                      │
│   - 生成唯一编码（Code = GUID）                               │
│   返回：void                                                │
│   默认实现：自动填充审计字段和 Code                           │
├─────────────────────────────────────────────────────────────┤
│④ [框架执行] base.Add() / base.Update()                      │
│   用途：执行数据库写入                                        │
│   返回：SaveModel                                            │
├─────────────────────────────────────────────────────────────┤
│⑤ OnAfterSave(TEntity, SaveMode)                             │
│   用途：保存后处理（同一事务内）                               │
│   - 写入关联表                                              │
│   - 发送消息/事件                                            │
│   - 记录操作日志                                            │
│   返回：void                                                │
│   默认实现：空                                               │
│   ⚠️ 如果抛出异常，事务回滚                                   │
├─────────────────────────────────────────────────────────────┤
│⑥ OnSaveCompleted(TEntity, SaveMode)                         │
│   用途：保存完成后的清理工作                                   │
│   - 清理缓存                                                │
│   - 刷新字典缓存                                            │
│   返回：void                                                │
│   默认实现：空                                               │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
响应: { code: 0, message: "保存成功" }
```

#### 🗑️ 删除生命周期 (Del)

```
请求: POST api/{Entity}/Del
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① OnDeleteStart(object[] keys)                               │
│   用途：删除开始，记录日志                                    │
│   参数：keys = 要删除的主键数组                               │
│   返回：void                                                │
│   默认实现：空                                               │
├─────────────────────────────────────────────────────────────┤
│② CanDelete(object[] keys) → bool                            │
│   用途：检查是否允许删除                                      │
│   - 检查关联数据是否存在                                     │
│   - 检查业务状态是否允许                                     │
│   返回：true = 可以删除, false = 不允许                      │
│   默认实现：返回 true                                        │
│   ⚠️ 如果返回 false，终止删除并返回错误                      │
├─────────────────────────────────────────────────────────────┤
│③ OnBeforeDelete(object[] keys)                              │
│   用途：删除前处理                                           │
│   - 备份关键数据                                            │
│   - 记录删除原因                                            │
│   返回：void                                                │
│   默认实现：空                                               │
├─────────────────────────────────────────────────────────────┤
│④ [框架执行] base.Del()                                      │
│   用址：执行删除（支持逻辑删除）                              │
│   - 物理删除或设置 DeleteTime                                │
│   返回：WebResponseContent                                   │
├─────────────────────────────────────────────────────────────┤
│⑤ OnAfterDelete(object[] keys)                               │
│   用址：删除后处理                                           │
│   - 清理关联数据                                            │
│   - 刷新缓存                                                │
│   返回：void                                                │
│   默认实现：空                                               │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
响应: { code: 0, message: "删除成功" }
```

### 3.3 基类代码骨架

```csharp
/// <summary>
/// CertPlatform 服务基类 - 提供完整的生命周期管理和通用功能
/// </summary>
/// <typeparam name="TEntity">实体类型，必须继承 BaseEntity</typeparam>
public abstract class CertServiceBase<TEntity> : ServiceBase<TEntity, ICertRepository<TEntity>>
    where TEntity : BaseEntity, new()
{
    #region 依赖注入
    
    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly ICertRepository<TEntity> _repository;
    
    [ActivatorUtilitiesConstructor]
    protected CertServiceBase(
        ICertRepository<TEntity> dbRepository,
        IHttpContextAccessor httpContextAccessor
    ) : base(dbRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _repository = dbRepository;
    }
    
    #endregion

    #region 辅助属性
    
    /// <summary>
    /// 当前用户 ID
    /// </summary>
    protected long? CurrentUserId => UserContext.Current.UserId;
    
    /// <summary>
    /// 当前用户 OrgCode（多租户）
    /// </summary>
    protected string CurrentOrgCode => UserContext.Current.GetOrgCode();
    
    /// <summary>
    /// 是否为超级管理员（跳过多租户过滤）
    /// </summary>
    protected bool IsSuperAdmin => UserContext.Current.IsSuperAdmin();
    
    #endregion

    #region 查询生命周期
    
    /// <summary>
    /// 查询开始 - 权限检查、参数预处理
    /// </summary>
    protected virtual void OnQueryStart(PageDataOptions options) { }
    
    /// <summary>
    /// 构建基础查询 - 多租户过滤、软删除过滤
    /// </summary>
    protected virtual IQueryable<TEntity> OnBuildQuery(IQueryable<TEntity> query)
    {
        // 默认：过滤已删除数据
        query = query.Where(x => x.DeleteTime == null);
        
        // 默认：多租户过滤（非超级管理员）
        if (!IsSuperAdmin && !string.IsNullOrEmpty(CurrentOrgCode))
        {
            // 注意：实体需要有 OrgCode 属性，可通过反射或接口约束
            query = ApplyOrgCodeFilter(query, CurrentOrgCode);
        }
        
        return query;
    }
    
    /// <summary>
    /// 业务过滤条件 - 子类可重写
    /// </summary>
    protected virtual IQueryable<TEntity> OnQueryFilter(IQueryable<TEntity> query)
    {
        return query; // 默认不添加额外条件
    }
    
    /// <summary>
    /// 查询后处理 - 计算字段、格式转换
    /// </summary>
    protected virtual PageGridData<TEntity> OnQueryExecuted(PageGridData<TEntity> result)
    {
        return result; // 默认不做处理
    }
    
    #endregion

    #region 保存生命周期
    
    /// <summary>
    /// 保存开始 - 判断操作类型
    /// </summary>
    protected virtual void OnSaveStart(SaveModel model, SaveMode mode) { }
    
    /// <summary>
    /// 数据校验 - 返回校验结果
    /// </summary>
    protected virtual (bool valid, string error) OnValidate(TEntity entity)
    {
        return (true, null); // 默认校验通过
    }
    
    /// <summary>
    /// 保存前处理 - 设置默认值、审计字段
    /// </summary>
    protected virtual void OnBeforeSave(TEntity entity, SaveMode mode)
    {
        switch (mode)
        {
            case SaveMode.Add:
                entity.SetCreateInfo(CurrentUserId);
                break;
            case SaveMode.Update:
                entity.SetUpdateInfo(CurrentUserId);
                break;
        }
    }
    
    /// <summary>
    /// 保存后处理 - 写关联表（同一事务）
    /// </summary>
    protected virtual void OnAfterSave(TEntity entity, SaveMode mode) { }
    
    /// <summary>
    /// 保存完成 - 清理缓存等
    /// </summary>
    protected virtual void OnSaveCompleted(TEntity entity, SaveMode mode) { }
    
    #endregion

    #region 删除生命周期
    
    /// <summary>
    /// 删除开始
    /// </summary>
    protected virtual void OnDeleteStart(object[] keys) { }
    
    /// <summary>
    /// 是否允许删除
    /// </summary>
    protected virtual bool CanDelete(object[] keys)
    {
        return true; // 默认允许删除
    }
    
    /// <summary>
    /// 删除前处理
    /// </summary>
    protected virtual void OnBeforeDelete(object[] keys) { }
    
    /// <summary>
    /// 删除后处理
    /// </summary>
    protected virtual void OnAfterDelete(object[] keys) { }
    
    #endregion

    #region 重写基类方法（组装生命周期）
    
    public override PageGridData<TEntity> GetPageData(PageDataOptions options)
    {
        // 1. 查询开始
        OnQueryStart(options);
        
        // 2. 构建基础查询
        QueryRelativeExpression = (IQueryable<TEntity> query) =>
        {
            query = OnBuildQuery(query);
            return OnQueryFilter(query);
        };
        
        // 3. 执行查询
        var result = base.GetPageData(options);
        
        // 4. 查询后处理
        return OnQueryExecuted(result);
    }
    
    public override WebResponseContent Add(SaveModel model)
    {
        return ExecuteSave(model, SaveMode.Add);
    }
    
    public override WebResponseContent Update(SaveModel model)
    {
        return ExecuteSave(model, SaveMode.Update);
    }
    
    private WebResponseContent ExecuteSave(SaveModel model, SaveMode mode)
    {
        // 1. 保存开始
        OnSaveStart(model, mode);
        
        // 2. 从 SaveModel 提取实体
        var entity = model.MainData.Deserialize<TEntity>();
        
        // 3. 校验
        var (valid, error) = OnValidate(entity);
        if (!valid) return webResponse.Error(error);
        
        // 4. 保存前处理
        AddOnExecuting = (TEntity e, object list) =>
        {
            OnBeforeSave(e, mode);
            return webResponse.OK();
        };
        
        // 5. 执行保存
        WebResponseContent response;
        if (mode == SaveMode.Add)
            response = base.Add(model);
        else
            response = base.Update(model);
        
        if (!response.Status) return response;
        
        // 6. 保存后处理（同一事务）
        try
        {
            OnAfterSave(entity, mode);
        }
        catch (Exception ex)
        {
            return webResponse.Error($"保存后处理失败: {ex.Message}");
        }
        
        // 7. 完成
        OnSaveCompleted(entity, mode);
        
        return response;
    }
    
    public override WebResponseContent Del(object[] keys, bool delList = false)
    {
        // 1. 删除开始
        OnDeleteStart(keys);
        
        // 2. 检查是否可删除
        if (!CanDelete(keys))
        {
            return webResponse.Error("当前数据不允许删除（存在关联数据或其他限制）");
        }
        
        // 3. 删除前处理
        DelOnExecuting = (object[] delKeys) =>
        {
            OnBeforeDelete(delKeys);
            return webResponse.OK();
        };
        
        // 4. 执行删除
        var response = base.Del(keys, delList);
        if (!response.Status) return response;
        
        // 5. 删除后处理
        OnAfterDelete(keys);
        
        return response;
    }
    
    #endregion

    #region 辅助方法
    
    private IQueryable<TEntity> ApplyOrgCodeFilter(IQueryable<TEntity> query, string orgCode)
    {
        // 通过反射或接口获取 OrgCode 属性
        var orgCodeProperty = typeof(TEntity).GetProperty("OrgCode");
        if (orgCodeProperty != null)
        {
            // 使用 Expression 动态构建 Where 条件
            // ...
        }
        return query;
    }
    
    #endregion
}

/// <summary>
/// 保存模式枚举
/// </summary>
public enum SaveMode
{
    Add,
    Update
}
```

### 3.4 Controller 基类设计

```csharp
/// <summary>
/// CertPlatform 控制器基类 - 自动注册路由和标准端点
/// </summary>
[Route("api/Cert{EntityName}")]
[PermissionTable(Name = "Cert{EntityName}")]
public abstract class CertControllerBase<TEntity, TService> 
    : ApiBaseController<TService>
    where TEntity : BaseEntity, new()
    where TService : CertServiceBase<TEntity>
{
    protected readonly TService _service;
    protected readonly IHttpContextAccessor _httpContextAccessor;

    [ActivatorUtilitiesConstructor]
    protected CertControllerBase(
        TService service,
        IHttpContextAccessor httpContextAccessor
    ) : base(service)
    {
        _service = service;
        _httpContextAccessor = httpContextAccessor;
    }

    #region 标准端点（自动可用，无需手写）

    // GET/POST api/Cert{Entity}/getPageData  ← 框架自动提供
    // POST   api/Cert{Entity}/Add           ← 框架自动提供
    // POST   api/Cert{Entity}/Update        ← 框架自动提供
    // POST   api/Cert{Entity}/Del           ← 框架自动提供
    // POST   api/Cert{Entity}/Export        ← 框架自动提供
    // POST   api/Cert{Entity}/Import        ← 框架自动提供

    #endregion

    #region 可选扩展端点（在 Partial 中按需添加）

    /// <summary>
    /// 获取启用列表（下拉选择用）
    /// 示例：POST api/CertCertificationBody/GetActiveList
    /// </summary>
    [HttpPost("GetActiveList")]
    [ApiActionPermission()]
    public virtual async Task<IActionResult> GetActiveList()
    {
        var data = await _service.FindAsync(x => x.Status == "active");
        return JsonNormal(data);
    }

    /// <summary>
    /// 根据 ID 获取详情
    /// 示例：POST api/CertCertificationBody/GetById
    /// </summary>
    [HttpPost("GetById")]
    [ApiActionPermission()]
    public virtual async Task<IActionResult> GetById([FromBody] long id)
    {
        var data = await _service.FindFirstAsync(x => x.Id == id);
        return JsonNormal(data);
    }

    #endregion
}
```

### 3.5 使用示例

```csharp
// ====== 简单场景：只需继承，无需写任何代码 ======

// Service（如果完全没有特殊逻辑，甚至不需要这个文件）
public partial class CertificationBodyService 
    : CertServiceBase<CertificationBody>
{
    // 空实现 - 所有 CRUD 逻辑由基类处理
}

// Controller（同上）
public partial class CertificationBodyController 
    : CertControllerBase<CertificationBody, CertificationBodyService>
{
    // 空实现 - 所有路由由基类注册
}


// ====== 复杂场景：只重写需要的钩子 ======

public partial class CertificationBodyService 
    : CertServiceBase<CertificationBody>
{
    /// <summary>
    /// 重写校验逻辑：CNAS 编号必须唯一
    /// </summary>
    protected override (bool valid, string error) OnValidate(CertificationBody entity)
    {
        // 调用基类校验
        var (valid, error) = base.OnValidate(entity);
        if (!valid) return (valid, error);
        
        // 业务校验：CNAS 编号唯一性
        if (!string.IsNullOrEmpty(entity.CbCode) && 
            _repository.Exists(x => x.CbCode == entity.CbCode && x.Id != entity.Id))
        {
            return (false, $"CNAS编号 [{entity.CbCode}] 已存在");
        }
        
        return (true, null);
    }
    
    /// <summary>
    /// 重写保存后逻辑：记录操作日志
    /// </summary>
    protected override void OnAfterSave(CertificationBody entity, SaveMode mode)
    {
        base.OnAfterSave(entity, mode);
        
        // 记录日志
        Logger.Info($"{(mode == SaveMode.Add ? "新建" : "编辑")}认证机构: {entity.Name}");
    }
}
```

---

## 四、前端组件设计

### 4.1 GenericCrud.vue 生命周期

#### 🔄 页面加载生命周期

```
组件创建 (onMounted)
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① onInit(config: CrudConfig)                                │
│   用途：接收配置，初始化内部状态                              │
│   参数：完整的页面配置对象                                    │
│   默认行为：解析配置，设置默认值                              │
├─────────────────────────────────────────────────────────────┤
│② onLoadDictionary(dictKeys: string[])                      │
│   用途：异步加载字典数据                                     │
│   参数：需要加载的字典编号列表                                │
│   默认行为：调用后端 GetVueDictionary 接口                   │
├─────────────────────────────────────────────────────────────┤
│③ onInitColumns(columns: ColumnConfig[])                    │
│   用途：处理列配置                                          │
│   - 隐藏列处理                                              │
│   - 格式化函数绑定                                          │
│   - 点击事件绑定                                            │
│   默认行为：根据配置生成表格列                                │
├─────────────────────────────────────────────────────────────┤
│④ onInitFormOptions(fields: FormFieldConfig[])              │
│   用途：处理编辑表单配置                                     │
│   - 字典选项填充                                            │
│   - 联动规则设置                                            │
│   - 默认值设置                                              │
│   默认行为：根据列配置自动生成表单                            │
├─────────────────────────────────────────────────────────────┤
│⑤ onInitSearchOptions(fields: SearchFieldConfig[])          │
│   用途：处理搜索表单配置                                     │
│   默认行为：根据 searchFields 生成搜索栏                      │
├─────────────────────────────────────────────────────────────┤
│⑥ onReady()                                                 │
│   用途：所有初始化完成，可以访问 DOM 和调用 API               │
│   默认行为：触发首次查询（如果 autoLoad = true）              │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
页面渲染完成 ✅
```

#### 🔍 查询生命周期

```
用户操作：点击搜索 / 切换页码 / 切换排序 / 切换树节点
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① onSearchBefore(params: object): boolean | object           │
│   用途：查询前参数处理                                       │
│   - 添加额外的过滤条件                                       │
│   - 参数格式转换                                            │
│   返回：true = 继续, false = 取消, object = 修改后的参数     │
│   默认行为：返回 true                                        │
├─────────────────────────────────────────────────────────────┤
│② [组件内部] 调用后端 API                                     │
│   POST /api/{entity}/getPageData                            │
├─────────────────────────────────────────────────────────────┤
│③ onSearchAfter(data: PageResult)                            │
│   用途：查询后数据处理                                       │
│   - 数据格式转换                                            │
│   - 计算汇总字段                                            │
│   - 附加显示信息                                            │
│   默认行为：返回原数据                                       │
├─────────────────────────────────────────────────────────────┤
│④ [组件内部] 更新表格显示                                     │
└─────────────────────────────────────────────────────────────┘
```

#### 💾 编辑生命周期（新建/编辑）

```
用户操作：点击新增 / 点击编辑按钮
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① onEditOpen(mode: 'add' | 'edit', row?: object)            │
│   用途：弹框打开前                                           │
│   - 根据模式设置不同的表单行为                                │
│   - 加载编辑时的现有数据                                     │
│   默认行为：打开弹框                                         │
├─────────────────────────────────────────────────────────────┤
│② onFormLoad(formData: object)                               │
│   用途：表单数据加载完成                                      │
│   - 设置联动字段的初始值                                     │
│   - 根据权限禁用某些字段                                     │
│   默认行为：填充表单                                         │
├─────────────────────────────────────────────────────────────┤
│   ... 用户填写/修改表单 ...                                   │
│                                                              │
│ 用户点击保存按钮                                             │
│        │                                                     │
│        ▼                                                     │
├─────────────────────────────────────────────────────────────┤
│③ onSaveBefore(formData: object): boolean | object           │
│   用途：保存前校验和处理                                     │
│   - 前端自定义校验                                          │
│   - 数据格式转换                                            │
│   - 添加额外字段                                            │
│   返回：true = 继续, false = 阻止保存                        │
│   默认行为：返回 true                                        │
├─────────────────────────────────────────────────────────────┤
│④ [组件内部] 调用后端 API                                     │
│   POST /api/{entity}/Add 或 Update                          │
├─────────────────────────────────────────────────────────────┤
│⑤ onSaveAfter(result: ApiResponse)                           │
│   用途：保存后处理                                           │
│   - 显示成功提示                                            │
│   - 关闭弹框                                                │
│   - 刷新列表                                                │
│   - 触发其他联动操作                                         │
│   默认行为：提示成功 + 刷新列表                               │
└─────────────────────────────────────────────────────────────┘
```

#### 🗑️ 删除生命周期

```
用户操作：点击删除按钮
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① onDeleteBefore(rows: object[]): boolean | string           │
│   用途：删除前确认/检查                                      │
│   - 弹出确认框（可选）                                       │
│   - 检查业务规则                                            │
│   返回：true = 继续, false = 取消, string = 自定义确认文案    │
│   默认行为：弹出标准确认框                                    │
├─────────────────────────────────────────────────────────────┤
│② [组件内部] 调用后端 API                                     │
│   POST /api/{entity}/Del                                    │
├─────────────────────────────────────────────────────────────┤
│③ onDeleteAfter()                                            │
│   用途：删除后处理                                           │
│   - 刷新列表                                                │
│   - 清理相关缓存                                            │
│   默认行为：刷新列表                                         │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 TreeCrud.vue 特有生命周期

```
树形组件加载
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① onTreeLoad()                                              │
│   用途：加载树形数据                                         │
│   默认行为：调用 treeConfig.url 加载数据                      │
├─────────────────────────────────────────────────────────────┤
│   ... 树形渲染完成 ...                                        │
│                                                              │
│ 用户点击树节点                                               │
│        │                                                     │
│        ▼                                                     │
├─────────────────────────────────────────────────────────────┤
│② onTreeSelect(node: TreeNode)                              │
│   用途：树节点选中处理                                       │
│   - 高亮选中节点                                            │
│   - 记录选中状态                                            │
│   默认行为：更新选中状态                                     │
├─────────────────────────────────────────────────────────────┤
│③ onTreeFilterChanged(filter: object)                       │
│   用途：计算表格过滤条件                                     │
│   - 根据 treeConfig.linkage 生成过滤对象                     │
│   默认行为：使用 treeField 的值作为 filterField 的条件        │
├─────────────────────────────────────────────────────────────┤
│④ [自动触发] 表格重新查询                                     │
│   调用 onSearchBefore → API → onSearchAfter                 │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 配置文件格式定义

```typescript
// ====== 基础类型定义 ======

/** 列类型 */
type ColumnType = 
  | 'text'        // 文本输入
  | 'textarea'    // 多行文本
  | 'number'      // 数字
  | 'select'      // 下拉选择
  | 'date'        // 日期
  | 'datetime'    // 日期时间
  | 'switch'      // 开关
  | 'radio'       // 单选
  | 'checkbox'    // 多选
  | 'file'        // 文件上传
  | 'image'       // 图片上传
  | 'link'        // 超链接
  | 'custom';     // 自定义（使用 slot）

/** 排序方式 */
type SortOrder = 'asc' | 'desc';

/** 列配置 */
interface ColumnConfig {
  field: string;              // 字段名（对应实体属性）
  title: string;              // 列标题
  width?: number;             // 列宽
  minWidth?: number;          // 最小宽度
  type?: ColumnType;          // 字段类型（用于编辑表单）
  
  // 显示控制
  hidden?: boolean;           // 是否隐藏
  fixed?: 'left' | 'right';  // 固定列
  align?: 'left' | 'center' | 'right';  // 对齐方式
  
  // 表格特性
  sort?: boolean;             // 是否可排序
  sortable?: SortOrder;       // 默认排序
  formatter?: Function;       // 格式化函数 (row, column, value) => string
  click?: Function;           // 点击事件 (row, column, event) => void
  
  // 表单特性
  required?: boolean;         // 是否必填
  unique?: boolean;           // 是否唯一
  dictKey?: string;           // 字典编号（select/radio/checkbox 时使用）
  options?: OptionItem[];     // 静态选项（优先于字典）
  placeholder?: string;       // 占位文本
  
  // 联动
  onChange?: Function;        // 值变化回调 (value, form) => void
  visibleIf?: Function;       // 条件显示 (form) => boolean
  
  // 扩展
  slot?: string;              // 自定义插槽名称
  props?: Record<string, any>; // 传递给底层组件的属性
}

/** 选项项 */
interface OptionItem {
  value: string | number;
  label: string;
  disabled?: boolean;
  children?: OptionItem[];    // 级联选项
}

/** 表单字段配置 */
interface FormFieldConfig extends Omit<ColumnConfig, 'width' | 'fixed' | 'align'> {
  colSize?: number;           // 占位栅格数（1-24，默认12即一半）
  defaultValue?: any;         // 默认值
  readonly?: boolean;         // 只读
  disabled?: boolean;         // 禁用
}

/** 搜索字段配置 */
interface SearchFieldConfig extends Pick<ColumnConfig, 'field' | 'title' | 'type' | 'dictKey' | 'options'> {
  operator?: 'eq' | 'like' | 'gt' | 'lt' | 'in' | 'between';  // 查询运算符
  defaultValue?: any;       // 默认值
}

/** 功能开关 */
interface FeatureFlags {
  search?: boolean;          // 显示搜索栏（默认 true）
  add?: boolean;             // 允许新增（默认 true）
  edit?: boolean;            // 允许编辑（默认 true）
  delete?: boolean;          // 允许删除（默认 true）
  export?: boolean;          // 允许导出（默认 false）
  import?: boolean;          // 允许导入（默认 false）
  pagination?: boolean;      // 显示分页（默认 true）
  selection?: boolean;       // 显示复选框（默认 false）
  refresh?: boolean;         // 显示刷新按钮（默认 true）
  columnSetting?: boolean;   // 显示列设置（默认 true）
}

/** 行为配置 */
interface BehaviorConfig {
  autoLoad?: boolean;        // 自动加载数据（默认 true）
  pageSize?: number;         // 每页条数（默认 30）
  pageSizes?: number[];      // 可选每页条数（默认 [10, 20, 30, 50, 100]）
  sortField?: string;        // 默认排序字段
  sortOrder?: SortOrder;     // 默认排序方向
  height?: number | string;  // 表格高度（默认自动）
  maxHeight?: number;        // 表格最大高度
  stripe?: boolean;          // 斑马纹（默认 true）
  border?: boolean;          // 边框（默认 true）
}

/** 主配置 */
interface CrudConfig {
  // 基本信息
  entity: string;             // 实体名称（用于 API 路径）
  title: string;              // 页面标题
  
  // 数据配置
  columns: ColumnConfig[];    // 列配置（同时用于表格和表单）
  
  // 搜索配置（如果不配置，从 columns 中提取有 search: true 的字段）
  searchFields?: SearchFieldConfig[];
  
  // 功能开关
  features?: FeatureFlags;
  
  // 行为配置
  behavior?: BehaviorConfig;
  
  // 明细配置（主从表时使用）
  detail?: DetailConfig;
  
  // 国际化（可选）
  i18n?: Record<string, string>;
}

/** 明细配置（主从表） */
interface DetailConfig {
  key: string;                // 主键字段名
  foreignKey: string;         // 外键字段名（关联主表）
  columns: ColumnConfig[];    // 明细列配置
  editFormFields: Record<string, any>;  // 明细表单字段
  editFormOptions: FormFieldConfig[];   // 明细表单配置
  sortName?: string;          // 排序字段
}

// ====== 树形配置 ======

interface TreeNode {
  id: string | number;
  label: string;
  children?: TreeNode[];
  [key: string]: any;        // 扩展字段
}

interface TreeConfig {
  url: string;                // 树数据 API 地址
  method?: 'GET' | 'POST';   // 请求方法（默认 GET）
  labelField?: string;        // 显示文本字段（默认 'label' 或 'name'）
  childrenField?: string;     // 子节点字段（默认 'children'）
  idField?: string;           // ID 字段（默认 'id'）
  parentField?: string;       // 父级字段（扁平数据时使用）
  
  // 显示控制
  defaultExpandAll?: boolean; // 默认展开全部（默认 false）
  defaultExpandedKeys?: (string | number)[];  // 默认展开的节点
  highlightCurrent?: boolean;// 高亮当前节点（默认 true）
  showCheckbox?: boolean;    // 显示复选框（默认 false）
  
  // 行为
  lazy?: boolean;             // 懒加载（默认 false）
  loadMethod?: string;        // 懒加载方法名
  
  // 过滤
  filterable?: boolean;       // 可筛选（默认 false）
}

interface LinkageConfig {
  treeField: string;          // 树节点的哪个值作为过滤条件
  tableFilterField: string;   // 表格过滤的字段名
  autoQuery?: boolean;        // 选中节点后自动查询（默认 true）
  clearOnRoot?: boolean;      // 选择根节点时清除过滤（默认 true）
}

interface TreeCrudConfig extends Omit<CrudConfig, 'title'> {
  tree: TreeConfig;
  linkage: LinkageConfig;
}
```

### 4.4 使用示例

```vue
<!-- ====== 方式一：简单使用（推荐）====== -->
<!-- 文件：views/cert/certification-body/Index.vue -->
<template>
  <GenericCrud :config="config" />
</template>

<script setup lang="ts">
import GenericCrud from '@/components/GenericCrud.vue'
import { certificationBodyConfig } from '@/config/certification-body'

const config = certificationBodyConfig
</script>


<!-- ====== 方式二：带钩子的使用 ===== -->
<template>
  <GenericCrud 
    :config="config"
    :hooks="hooks"
  />
</template>

<script setup lang="ts">
import GenericCrud from '@/components/GenericCrud.vue'
import { certificationBodyConfig } from '@/config/certification-body'
import type { CrudHooks } from '@/types/crud'

const config = certificationBodyConfig

// 定义钩子（只实现需要的）
const hooks: CrudHooks<CertificationBody> = {
  // 保存前校验 CNAS 编号
  onSaveBefore(formData) {
    if (formData.cbCode && formData.cbCode.length < 5) {
      ElMessage.warning('CNAS 编号至少 5 位')
      return false
    }
    return true
  },
  
  // 保存后提示
  onSaveAfter(result) {
    ElMessage.success(`保存成功：${result.data.name}`)
  },
  
  // 删除前确认
  onDeleteBefore(rows) {
    if (rows.some(r => r.status === 'active')) {
      return '选中的数据中有启用状态的机构，确定要删除吗？'
    }
    return true
  }
}
</script>


<!-- ====== 方式三：树形结构使用 ===== -->
<!-- 文件：views/cert/iso-standard/Index.vue -->
<template>
  <TreeCrud :config="treeConfig" />
</template>

<script setup lang="ts">
import TreeCrud from '@/components/TreeCrud.vue'

const treeConfig: TreeCrudConfig = {
  entity: 'CertIsoStandard',
  
  // 树形配置
  tree: {
    url: '/api/CertIsoStandard/GetTreeData',
    labelField: 'name',
    childrenField: 'children',
    defaultExpandAll: false,
  },
  
  // 联动配置
  linkage: {
    treeField: 'parentId',       // 树节点的 parentId
    tableFilterField: 'parentId', // 表格按 parentId 过滤
    autoQuery: true,
  },
  
  // 表格配置
  columns: [
    { field: 'code', title: '条款编号', width: 120 },
    { field: 'title', title: '条款名称', width: 300 },
    { field: 'status', title: '状态', type: 'select', dictKey: 'cert_status' },
  ],
  
  features: {
    add: true,
    edit: true,
    delete: true,
  }
}
</script>
```

---

## 五、与 Vol 框架的兼容性

### 5.1 复用 Vol 能力

| Vol 内置能力 | 我们的封装 | 说明 |
|-------------|----------|------|
| `ServiceBase.GetPageData()` | `CertServiceBase.GetPageData()` | 在其基础上增加生命周期 |
| `ApiBaseController` 路由 | `CertControllerBase` 路由 | 保持相同路由规范 |
| `view-grid` 组件 | `GenericCrud` 组件 | 组合 view-grid，非替代 |
| 字典系统 | `dictKey` 配置 | 直接使用，无需改动 |
| 权限系统 | `[ApiActionPermission]` | 保持一致 |

### 5.2 扩展点映射

```
Vol 框架钩子              我们的生命周期           关系
─────────────────────────────────────────────────────
AddOnExecuting    ←→    OnBeforeSave(Add)      包含
AddOnExecuted     ←→    OnAfterSave(Add)       包含
UpdateOnExecuting ←→    OnBeforeSave(Update)   包含
UpdateOnExecuted  ←→    OnAfterSave(Update)    包含
DelOnExecuting    ←→    OnBeforeDelete         包含
DelOnExecuted     ←→    OnAfterDelete          包含
QueryRelativeExpr←→    OnBuildQuery + Filter   拆分为两步
```

---

## 六、实施路径建议

### Phase 0.5：基础架构搭建（1-2 天）

- [ ] 创建 `CertServiceBase<TEntity>` 基类
- [ ] 创建 `CertControllerBase<TEntity>` 基类
- [ ] 创建 `GenericCrud.vue` 组件（基础版）
- [ ] 定义 TypeScript 类型配置文件

### Phase 1：验证案例（1-2 天）

- [ ] 用"认证机构"模块验证完整流程
- [ ] 测试简单 CRUD（无任何特化代码）
- [ ] 测试复杂场景（重写校验、保存后逻辑）

### Phase 2：树形组件（1 天）

- [ ] 创建 `TreeCrud.vue` 组件
- [ ] 用"ISO标准"或"文件目录"验证

### Phase 3：批量迁移（持续）

- [ ] 将其他模块改为配置驱动
- [ ] 移除冗余的手写代码

---

## 七、风险与应对

| 风险 | 影响 | 应对策略 |
|------|------|---------|
| 过度抽象 | 学习成本高 | 保持简单场景零代码，复杂场景可回退到手写 |
| 性能问题 | 反射/泛型开销 | 基准测试，热点路径优化 |
| Vol 版本升级 | 兼容性问题 | 封装层隔离，升级只需适配基类 |
| 调试困难 | 生命周期过长 | 详细日志，支持单步调试 |

---

## 八、待决事项

以下问题需要在实施前确认：

1. **多租户实现方式**
   - A：所有实体强制要求 `OrgCode` 字段
   - B：通过接口 `IOrgCodeAware` 标识
   - C：仅在 Service 层按需过滤

2. **逻辑删除策略**
   - A：全局统一使用 `DeleteTime` 字段
   - B：每个实体可配置是否支持逻辑删除

3. **前端组件库选择**
   - A：完全基于 Vol 的 `view-grid`
   - B：基于 Element Plus Table 封装（更灵活）
   - C：混合方案（简单用 view-grid，复杂用自研）

4. **配置文件存放位置**
   - A：前端 `src/config/` 目录
   - B：后端数据库配置表（运行时可改）
   - C：混合（静态配置 + 动态覆盖）

---

**文档版本**：V1.0  
**创建时间**：2026-07-31  
**作者**：AI Assistant  
**评审状态**：⏳ 待用户确认
