# 08 - Vol 框架实战速查手册

**版本**：V1.0
**日期**：2026-08-11
**状态**：正式发布
**定位**：本项目实际开发中高频使用的 Vol 框架知识点，每条含「正确写法」「常见错误」「踩坑索引」
**官方文档**：http://v3.volcore.xyz

---

## 目录

- [一、后端：Service 业务扩展](#一后端service-业务扩展)
- [二、后端：EF Core 实体与数据库](#二后端ef-core-实体与数据库)
- [三、后端：Controller 与路由](#三后端controller-与路由)
- [四、后端：依赖注入（Autofac）](#四后端依赖注入autofac)
- [五、后端：数据库访问（repository）](#五后端数据库访问repository)
- [六、前端：ViewGrid 生命周期与钩子](#六前端viewgrid-生命周期与钩子)
- [七、前端：options.js 与字段配置](#七前端optionsjs-与字段配置)
- [八、前端：HTTP 请求（http.js）](#八前端http-请求httpjs)
- [九、前端：字段级事件与 formatter](#九前端字段级事件与-formatter)
- [十、菜单与权限系统](#十菜单与权限系统)
- [十一、数据字典系统](#十一数据字典系统)
- [十二、Vol 返回格式规范（JsonNormal）](#十二vol-返回格式规范jsonnormal)

---

## 一、后端：Service 业务扩展

> **官方文档**：http://v3.volcore.xyz/docs/cs/service/guid.html
> **代码位置**：`Services/{folder}/Partial/{表}Service.cs`
> **基类**：`ServiceBase<TEntity, IRepository<TEntity>>`

### 1.1 两种钩子注册方式

```csharp
// 方式 A：构造函数注册（固定逻辑，推荐）
public CertificationBodyService(IRepository<CertificationBody> repo) : base(repo)
{
    // 查询前过滤
    QueryRelativeExpression = q => q.Where(x => x.Enable == 1);
    
    // 新建前校验
    AddOnExecuting = (entity, detail) =>
    {
        if (string.IsNullOrEmpty(entity.Name))
            return webResponse.Error("名称不能为空");
        return webResponse.OK();
    };
}

// 方式 B：override 内赋值（按请求动态切换）
public override WebResponseContent Add(SaveModel saveDataModel)
{
    AddOnExecuting = (entity, detail) =>
    {
        // 可访问 saveDataModel 中的额外参数
        return webResponse.OK();
    };
    return base.Add(saveDataModel);
}
```

### 1.2 钩子完整清单（本项目实际使用频率排序）

| 钩子 | 签名 | 时机 | 高频场景 |
|------|------|------|----------|
| **AddOnExecuting** | `Func<TEntity, object, WebResponseContent>` | 入库**前** | 必填校验、默认值赋值 |
| **AddOnExecuted** | `Func<TEntity, object, WebResponseContent>` | 入库**后**（同事务） | 写关联表、生成编号 |
| **UpdateOnExecuting** | `Func<TEntity, object, object, List<object>, WebResponseContent>` | 更新**前** | 校验状态、字段变更检测 |
| **UpdateOnExecuted** | 同上 | 更新**后** | 同步关联表 |
| **DelOnExecuting** | `Func<object[], WebResponseContent>` | 删除**前** | 状态校验（如审核中不可删） |
| **DelOnExecuted** | `Func<object[], WebResponseContent>` | 删除**后** | 清理关联数据 |
| **QueryRelativeExpression** | `Func<IQueryable<T>, IQueryable<T>>` | 查询**前** | 数据权限过滤 |
| **QueryRelativeList** | `Action<List<SearchParameters>>` | 查询**前** | 修改/删除搜索条件 |
| **GetPageDataOnExecuted** | `Action<PageGridData<T>>` | 查询**后** | 翻译外键名称 |
| **OrderByExpression** | `Expression<Func<T, Dictionary<object, QueryOrderBy>>>` | 查询**前** | 多字段排序 |
| **SummaryExpress** | `Func<IQueryable<T>, object>` | 查询**后** | 合计行 |

### 1.3 Add 流水线

```
SaveModel（前端传入）
  → AddOnExecute（SaveModel 级校验）
  → 转换为 TEntity 实体
  → AddOnExecuting（实体级校验/赋值）  ← 最常用
  → [事务] 入库 + 明细入库
  → AddOnExecuted（同事务，写关联表）  ← return Error 会回滚
  → 审计日志 + 工作流
```

### 1.4 Update 明细参数

```csharp
UpdateOnExecuting = (main, addList, updateList, delKeys) =>
{
    // addList: 新增的明细行 → 强转为 List<TDetail>
    var newDetails = addList as List<DetailEntity>;
    // updateList: 修改的明细行
    var updatedDetails = updateList as List<DetailEntity>;
    // delKeys: 删除的明细主键列表
    return webResponse.OK();
};
```

### ⚠️ 常见错误

| 错误 | 原因 | 正确做法 |
|------|------|----------|
| `AddOnExecuting` 参数个数不对 | 误用了 `(entity) =>` 而非 `(entity, detail) =>` | 必须匹配签名：`Func<TEntity, object, WebResponseContent>` |
| 钩子里用了 `base.repository` 但报 null | 构造函数未正确调用 `base(repo)` | 确保 `: base(repo)` 传入 repository |
| `AddOnExecuted` 返回 Error 但数据已入库 | 误解了事务行为 | **AddOnExecuted 在同事务内**，return Error 会回滚。但如果自己在前面手动 `SaveChanges()` 则无法回滚 |
| 在 Partial Service 中重写了 `Add` 但忘了 `return base.Add()` | base 方法未调用，钩子不触发 | 必须 `return base.Add(saveDataModel)` |

> **踩坑索引**：[2026-08-03 P2-02 构造函数依赖注入丢失](./05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md)

---

## 二、后端：EF Core 实体与数据库

> **官方文档**：http://v3.volcore.xyz/docs/cs/dev/db.html
> **关键文件**：`VOL.Core/EFDbContext/VOLContext.cs`

### 2.1 实体继承链

```
Vol.BaseEntity（空基类，无字段）
  └─ YZHBaseEntity（12 个审计/业务字段）
       └─ 业务实体（如 CertificationBody）
```

> **注意**：Vol 的 `BaseEntity` **是空基类**，不包含任何字段。`YZHBaseEntity` 继承它并添加了 `CreateID`、`Creator`、`CreateDate` 等审计字段。EF Core 的 `OnModelCreating` 通过反射扫描所有 `BaseEntity` 子类自动注册到 DbContext。

### 2.2 实体注册（EF Core 自动发现）

```csharp
// VOLContext.cs → OnModelCreating
// 框架自动扫描所有继承 BaseEntity 的类
modelBuilder.EntitiesOfType<BaseEntity>()...
```

**前提条件**：实体必须继承 `BaseEntity`（直接或间接），否则 EF Core 无法自动发现。

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `InvalidOperationException: 无法映射 object 类型属性` | 实体属性类型 EF Core 不支持（如 `object`、`dynamic`） | 改为具体类型或 `string` |
| 实体未注册到 DbContext | 未继承 `BaseEntity` | 继承 `YZHBaseEntity`（间接继承 `BaseEntity`） |
| `Unknown column 'x.FieldName' in 'field list'` | EF Core 用属性名生成 SQL 列名，但数据库列名不同 | 加 `[Column("实际列名")]` 特性 |

### 2.3 列名映射策略（snake_case vs PascalCase）

**Vol 框架内置表**（`Sys_Menu`、`Sys_User` 等）**全部使用 PascalCase 列名**。YZH 自建表有两种策略：

| 策略 | 做法 | 适用场景 |
|------|------|----------|
| **A：统一 PascalCase**（推荐新表） | 建表时列名用 PascalCase，实体不加 `[Column]` | 新项目，表可重建 |
| **B：snake_case + `[Column]`**（已有表） | 建表时列名用 snake_case，实体每个属性加 `[Column("snake_case")]` | 已有数据库不可改 |

```csharp
// 策略 B 示例（yzh_* 系列表）
[Table("yzh_page_config")]
public class YzhPageConfig : YZHBaseEntity
{
    [Column("page_code")]
    public string PageCode { get; set; }
    
    [Column("checkbox_selection")]
    public bool CheckboxSelection { get; set; }
    // ... 每个属性都必须加 [Column]
}
```

> **踩坑索引**：[2026-08-07 EF Core Column 映射 snake_case 导致 400 错误](./05-踩坑记录/2026-08-07_EF-Core-Column映射snake_case导致400错误.md)

### 2.4 审计字段命名规范

```
创建：CreateID (BIGINT) / Creator (VARCHAR) / CreateDate (DATETIME)
修改：ModifyID (BIGINT) / Modifier (VARCHAR) / ModifyDate (DATETIME)
删除：DeleteID (BIGINT) / Deleter (VARCHAR) / DeleteTime (DATETIME)
```

> **注意**：Vol 框架内置表的审计字段用 `Creator`/`Modifier`（不是 `CreatedBy`/`UpdatedBy`）。YZH 自建表应遵循同一命名。

---

## 三、后端：Controller 与路由

> **官方文档**：http://v3.volcore.xyz/docs/cs/dev/api.html · http://v3.volcore.xyz/docs/cs/dev/case.html
> **基类**：`ApiBaseController<IServiceBase>`（继承 `VolController`）

### 3.1 标准 API 路由（自动生成）

| HTTP | URL | 对应 Service 方法 |
|------|-----|-------------------|
| POST | `api/{表}/getPageData` | `GetPageData(PageDataOptions)` |
| POST | `api/{表}/Add` | `Add(SaveModel)` |
| POST | `api/{表}/Update` | `Update(SaveModel)` |
| POST | `api/{表}/Del` | `Del(object[])` |
| POST | `api/{表}/Export` | `Export` |
| POST | `api/{表}/Import` | `Import` |
| POST | `api/{表}/Audit` | `Audit` |

> **关键**：路由名是 `Del`（不是 `Remove`、`Delete`）。前端删除必须调 `/api/{表}/Del`。

### 3.2 Partial Controller 自定义 API

```csharp
// 文件位置：Controllers/{Module}/Partial/{表}Controller.cs
namespace VOL.WebApi.Controllers.CertPlatform
{
    public partial class CertCertificationBodyController
    {
        private readonly ICertificationBodyService _service;

        // ⚠️ 必须标注 [ActivatorUtilitiesConstructor]
        [ActivatorUtilitiesConstructor]
        public CertCertificationBodyController(
            ICertificationBodyService service,
            IHttpContextAccessor httpContextAccessor)
            : base(service)
        {
            _service = service;
        }

        [HttpPost("GetCustomData")]
        public async Task<object> GetCustomData([FromBody] Dictionary<string, object> data)
        {
            var result = await _service.GetCustomData(data);
            // ⚠️ 返回业务数据必须用 JsonNormal（保持 PascalCase）
            return JsonNormal(result);
        }
    }
}
```

### 3.3 VolController 关键方法

| 方法 | 用途 | 大小写行为 |
|------|------|------------|
| `JsonNormal(object data)` | 返回 JSON | **保持原始 PascalCase**（前端 columns.field 对齐） |
| `Json(object data)` | 返回 JSON | 转为 **小驼峰 camelCase**（前端对不上） |
| `new JsonResult(object)` | ASP.NET Core 原生 | 默认 camelCase |

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| 前端表格有数据但列空白 | Controller 返回用了 `Json()` 而非 `JsonNormal()` | 改用 `return JsonNormal(result)` |
| 自定义 API 404 | Partial Controller 未标注 `[ActivatorUtilitiesConstructor]` | 加上特性，Autofac 才能正确解析构造函数 |
| 编译报错 `ControllerBase 没有 JsonNormal` | 继承了 `ControllerBase` 而非 `VolController`/`ApiBaseController` | 继承 `ApiBaseController<IService>` 或直接用 `VolController` |
| 标准 API 404 | Controller 类名不带 `Controller` 后缀 | 确保类名为 `{表}Controller` |

> **踩坑索引**：[2026-08-11 转换队列化 ControllerBase 缺 JsonNormal](./05-踩坑记录/2026-08-11-转换队列化实施踩坑记录.md)

---

## 四、后端：依赖注入（Autofac）

> **关键文件**：`VOL.Core/Infrastructure/AutofacManager.cs`

### 4.1 注册规则

| 接口 | 标记 | 自动注册 |
|------|------|----------|
| Service | 实现 `IDependency` | ✅ 自动注册为 Scoped |
| Repository | 实现 `IDependency` | ✅ 自动注册为 Scoped |
| 其他服务 | 实现 `IDependency` | ✅ 自动注册 |

### 4.2 构造函数注入

```csharp
// ✅ 正确：单构造函数
public class MyService : ServiceBase<MyEntity, IRepository<MyEntity>>
{
    private readonly IOtherRepository _otherRepo;
    
    public MyService(
        IRepository<MyEntity> repo,
        IOtherRepository otherRepo) : base(repo)
    {
        _otherRepo = otherRepo;
    }
}
```

```csharp
// ✅ 正确：多构造函数（Partial Controller 场景）
public partial class MyController
{
    private readonly IMyService _service;
    
    [ActivatorUtilitiesConstructor]  // ← 告诉 Autofac 用这个构造函数
    public MyController(IMyService service, IHttpContextAccessor httpCtx)
        : base(service)
    {
        _service = service;
    }
}
```

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `NullReferenceException: repository 未初始化` | Partial Controller 多构造函数未标注 `[ActivatorUtilitiesConstructor]` | 加上特性 |
| `Autofac.Core.DependencyResolutionException` | 构造函数参数有未注册的接口 | 确保所有注入的服务实现了 `IDependency` |
| 循环依赖 `A→B→A` | 两个 Service 互相注入 | 提取公共逻辑到第三个 Service，或用 `Lazy<T>` |

> **踩坑索引**：[2026-08-03 P2-02 构造函数依赖注入丢失](./05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md) · [2026-08-11 循环依赖](./05-踩坑记录/2026-08-11-转换队列化实施踩坑记录.md)

---

## 五、后端：数据库访问（repository）

> **官方文档**：http://v3.volcore.xyz/docs/cs/dev/db.html

### 5.1 repository 常用方法

```csharp
// 查询单条
var entity = repository.FindAsIQueryable(x => x.Id == id).FirstOrDefault();

// 查询列表
var list = repository.FindAsIQueryable(x => x.Enable == 1).ToList();

// 条件查询 + 排序
var query = repository.FindAsIQueryable(x => x.Status == "active")
    .OrderByDescending(x => x.CreateDate);

// 新增
repository.Add(new Entity { Name = "test" }, true);  // true = auto save

// 修改（指定字段）
repository.Update(entity, x => new { x.Name, x.ModifyDate }, true);

// 删除
repository.Delete(entity, true);

// EF Core 原生 DbContext
var dbContext = repository.DbContext;
var rawSql = dbContext.Set<Entity>().FromSqlRaw("SELECT * FROM ...");

// 事务
repository.DbContextBeginTransaction(() => {
    // 事务内的多表操作
    // 抛异常自动回滚
});
```

### 5.2 分页查询模式

```csharp
// 自定义分页 API（Partial Service）
public async Task<object> GetCustomPageData(PageDataOptions options)
{
    // 1. 构建查询
    var query = repository.FindAsIQueryable(x => x.Enable == 1);
    
    // 2. 应用前端过滤条件
    query = options.ConvertQueryFilter<Entity>();  // ← Vol 内置过滤转换
    
    // 3. 分页
    var (data, total) = query.TakePage(options.Page, options.Rows);
    
    return new { rows = data, total };
}
```

### 5.3 当前用户

```csharp
var userId = UserContext.Current.UserId;       // int
var userName = UserContext.Current.UserName;   // string
var isSuperAdmin = UserContext.Current.IsSuperAdmin;  // bool
```

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| MySQL 不支持 `RETURNING *` | 误用了 PostgreSQL 语法 | MySQL 用 `SELECT LAST_INSERT_ID()` 或 `repository.Add` 返回的实体已含自增 ID |
| `TakePage` 方法不存在 | 未引入 `VOL.Core.Extensions` | `using VOL.Core.Extensions;` |

> **踩坑索引**：[2026-08-11 MySQL 不支持 RETURNING](./05-踩坑记录/2026-08-11-转换队列化实施踩坑记录.md)

---

## 六、前端：ViewGrid 生命周期与钩子

> **官方文档**：http://v3.volcore.xyz/docs/view-grid/methods/ · http://v3.volcore.xyz/docs/view-grid/properties.html
> **代码位置**：`views/{类库}/{文件夹}/{表}/{表}.vue`

### 6.1 生命周期时序

```
组件创建
  ↓
onInit($vm)          ← 最早，获取 gridRef 实例
  ↓ 字典加载
dicInited(dic)       ← 字典加载完成
  ↓ 数据请求
searchBefore(param)  ← 查询前，可修改过滤条件
  ↓ API 请求
searchAfter(result)  ← 查询后，可处理结果
  ↓
onInited()           ← 数据初始化完成，可改 height/columns
```

### 6.2 onInit — 最常用的初始化

```javascript
const onInit = async ($vm) => {
  gridRef = $vm  // ← 保存实例，后续所有操作都靠它
  
  // 改 URL（自定义查询接口）
  // gridRef.url = 'api/MyTable/getCustomPageData'
  
  // 添加自定义按钮
  gridRef.buttons.splice(1, 0, {
    name: '自定义操作',
    icon: 'el-icon-document',
    type: 'primary',
    plain: true,
    onClick: () => {
      const rows = gridRef.getSelectRows()
      if (!rows.length) return proxy.$message.warning('请先选择行')
      // 业务逻辑...
    }
  })
  
  // 隐藏不需要的按钮
  gridRef.buttons = gridRef.buttons.filter(b => b.name !== '导入')
  
  // 配置明细表
  // gridRef.detail = { enabled: true, loadKey: 'OrderId', url: 'api/Order/getDetailPage' }
}
```

### 6.3 onInited — 数据就绪后调整

```javascript
const onInited = async () => {
  // 调整表格高度（减去查询栏等占用）
  gridRef.height = gridRef.height - 80
  
  // 修改列属性
  gridRef.columns.forEach(c => {
    if (c.field === 'Status') {
      c.formatter = (row) => row.Status === 1 ? '启用' : '禁用'  // ← 必须是函数
    }
    if (c.field === 'Amount') {
      c.summary = true  // 合计
    }
  })
  
  // 明细列属性
  // gridRef.detailOptions.columns.forEach(c => { ... })
}
```

### 6.4 searchBefore — 查询前修改条件

```javascript
const searchBefore = async (param) => {
  // 添加查询条件
  param.wheres.push({
    name: 'Status',
    value: 1,
    displayType: 'equal'  // ← 不是 "operator"！
  })
  
  // 传递额外参数到后端（后端 options.Value 读取）
  param.value = { customParam: 'xxx' }
  
  // return false 取消查询
  return true
}
```

**searchBefore 的 `displayType` 合法值**：

| displayType | 说明 |
|-------------|------|
| `equal` | 等于（默认） |
| `like` | 模糊匹配 |
| `gt` / `gte` | 大于 / 大于等于 |
| `lt` / `lte` | 小于 / 小于等于 |
| `in` | IN 查询（value 为逗号分隔） |
| `daterange` | 日期范围 |
| `select` / `selectList` | 下拉选择 |
| `checkbox` | 多选 |

### 6.5 modelOpenBefore/After — 弹窗打开

```javascript
const modelOpenBefore = async (row) => {
  // 弹窗打开前（row 为 null 表示新增）
}

const modelOpenAfter = async (row, currentAction, isCopyClick) => {
  // 弹窗打开后
  // currentAction: 'add' | 'update' | 'copy'
  
  if (currentAction === 'add') {
    // 设置默认值
    gridRef.editFormFields.Status = 1
  }
  
  // 联动：字段 A 变化时刷新 B 下拉
  const fieldOpt = gridRef.getFormOption('CategoryCode')
  fieldOpt.onChange = (val) => {
    const subOpt = gridRef.getFormOption('SubCategoryCode')
    subOpt.data = getSubCategories(val)
  }
}
```

### 6.6 addBefore / updateBefore — 保存前拦截

```javascript
const addBefore = async (formData, isCopyClick) => {
  // formData 是编辑表单数据对象
  if (!formData.Name) {
    proxy.$message.error('名称不能为空')
    return false  // ← return false 阻止保存
  }
  return true
}

const updateBefore = async (formData) => {
  // 编辑保存前
  formData.ModifyDate = new Date().toISOString()
  return true
}
```

### 6.7 gridRef 常用实例方法

| 方法 | 用途 |
|------|------|
| `gridRef.search()` | 刷新主表查询 |
| `gridRef.refresh()` | 刷新（同 search） |
| `gridRef.getSelectRows()` | 获取选中行数组 |
| `gridRef.getFormOption(field)` | 获取编辑表单字段配置 |
| `gridRef.getSearchFormOption(field)` | 获取查询表单字段配置 |
| `gridRef.add()` | 打开新增弹窗 |
| `gridRef.edit(row)` | 打开编辑弹窗 |
| `gridRef.del(row)` | 删除行 |
| `gridRef.clearSelection()` | 清空选中 |
| `gridRef.getTable()` | 获取 vol-table 实例 |
| `gridRef.getSearchParameters()` | 获取当前查询参数 |

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `column.formatter is not a function` | 在 onInited 中把 formatter 设为了 `true` 或字符串 | formatter **必须是函数**：`c.formatter = (row) => ...` |
| `clientHeight undefined` | 在 onInit 中设置了 height 但此时 DOM 未渲染 | height 设置放 `onInited` 中 |
| 搜索条件不生效 | `displayType` 写成了 `operator` | 改用 `displayType`，值用 `equal`/`like` 等 |
| options.js 修改被覆盖 | 直接改了 options.js 文件 | 在 `onInit` 中动态修改 `gridRef.columns` 等 |
| 按钮点击无反应 | 按钮添加时机不对 | 在 `onInit` 中 `gridRef.buttons.splice()` 添加 |

> **踩坑索引**：[2026-08-03 P2-05 formatter 不是函数](./05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md) · [2026-08-03 P2-07 clientHeight undefined](./05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md)

---

## 七、前端：options.js 与字段配置

> **官方文档**：http://v3.volcore.xyz/docs/view-grid/properties.html
> **关键规则**：options.js 是代码生成器自动生成的，**会被覆盖**，动态修改必须在 `onInit` 中进行

### 7.1 options.js 结构

```javascript
export default function () {
  return {
    // 主键字段（对应数据库主键列名）
    keyField: 'Id',
    
    // 表格列定义
    columns: [
      { field: 'Name', title: '名称', width: 120 },
      { field: 'Status', title: '状态', bind: { key: 'enable', data: [] } },
    ],
    
    // 编辑表单字段值
    editFormFields: { Name: '', Status: 1 },
    
    // 编辑表单布局（二维数组 = 行）
    editFormOptions: [
      [
        { field: 'Name', title: '名称', type: 'input', required: true },
        { field: 'Status', title: '状态', type: 'switch', bind: { key: 'enable' } },
      ]
    ],
    
    // 查询表单字段值
    searchFormFields: { Name: '', Status: null },
    
    // 查询表单布局
    searchFormOptions: [
      [
        { field: 'Name', title: '名称', type: 'input', displayType: 'like' },
        { field: 'Status', title: '状态', type: 'select', bind: { key: 'enable' } },
      ]
    ],
    
    // 表格属性
    table: {
      url: 'api/MyTable/',  // ← 注意末尾斜杠
      pagination: { current: 1, size: 30, total: 0 },
    },
  }
}
```

### 7.2 字段 type 合法值

| type | 控件 | 用途 |
|------|------|------|
| `input` | 文本输入 | 字符串 |
| `textarea` | 多行文本 | 长文本 |
| `number` | 数字输入 | 整数 |
| `decimal` | 数字输入 | 小数 |
| `date` | 日期选择 | YYYY-MM-DD |
| `datetime` | 日期时间 | YYYY-MM-DD HH:mm:ss |
| `select` | 下拉选择 | 单选 |
| `selectList` | 下拉多选 | 多选 |
| `switch` | 开关 | 布尔 |
| `img` | 图片上传 | 图片 |
| `file` | 文件上传 | 文件 |
| `editor` | 富文本编辑器 | HTML |

### 7.3 字典绑定（bind）

```javascript
// 方式 A：使用后台字典编码
{ field: 'Status', title: '状态', type: 'select', bind: { key: 'enable' } }
// 后台需有 Sys_Dictionary WHERE DictionaryCode = 'enable'

// 方式 B：前端静态数据
{ field: 'Type', title: '类型', type: 'select', 
  bind: { data: [{ key: 1, value: '类型A' }, { key: 2, value: '类型B' }] } 
}
```

### 7.4 表格列属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `field` | string | 字段名（与后端实体属性 PascalCase 一致） |
| `title` | string | 列标题 |
| `width` | number | 列宽（px） |
| `sortable` | bool | 是否可排序 |
| `formatter` | function | 格式化函数：`(row) => 返回显示文本` |
| `bind` | object | 字典绑定 |
| `click` | function | 点击事件：`(row, column, event) => {}` |
| `hidden` | bool | 是否隐藏 |
| `fixed` | string | 固定列：`'left'` / `'right'` |
| `summary` | bool | 是否合计 |
| `require` | bool | 明细编辑时是否必填 |
| `edit` | object | 行内编辑配置 `{ type: 'input' }` |

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| 列数据空白 | `field` 大小写与后端返回不一致 | 三端统一 PascalCase |
| 排序不生效 | `sortable` 设为 `true` 但后端未处理 | 后端 `OrderByExpression` 配合，或前端排序 |
| 导出空文件 | 导出时未传 `columns` 参数 | `exportBefore` 中确保 `param.columns` 包含列定义 |

> **踩坑索引**：[2026-08-07 导出空文件+排序](./05-踩坑记录/2026-08-07_YZHV2%20CrudTable%20导出空文件+排序+业务页简化.md)

---

## 八、前端：HTTP 请求（http.js）

> **官方文档**：http://v3.volcore.xyz/docs/cs/dev/api.html
> **封装文件**：`vol.web/src/api/http.js`

### 8.1 基本用法

```javascript
const { proxy } = getCurrentInstance()

// GET 请求
// 参数 3: true = 返回原始 JSON（不弹错误提示）
const result = await proxy.http.get('api/Controller/Action', { param: value }, true)

// POST 请求
const result = await proxy.http.post('api/Controller/Action', { param: value }, true)

// 下载二进制文件（ArrayBuffer）
const buf = await proxy.http.get(
  'api/Controller/Download',
  null,
  false,  // false = 不弹错误提示（手动处理）
  { responseType: 'arraybuffer' }
)

// 上传文件
const formData = new FormData()
formData.append('file', file)
const result = await proxy.http.post('api/Controller/Upload', formData, true)
```

### 8.2 返回值规则（关键！）

```javascript
// http.js 内部实现：
axios.get(url, config).then((response) => {
  resolve(response.data)  // ← 只 resolve 了 response.data
})

// 因此：
const res = await proxy.http.get(url, ...)
// res 就是 response.data 本身，不是完整 response 对象
// res.data 是错误的！会得到 undefined
```

| 调用方式 | res 的值 | res.data 的值 |
|----------|----------|---------------|
| `http.get(url, {}, true)` | response.data（后端返回的 JSON） | ❌ undefined |
| `http.get(url, {}, false, {responseType:'arraybuffer'})` | ArrayBuffer | ❌ undefined |

### 8.3 URL 规则

```javascript
// ✅ 正确：相对路径，以 api/ 开头
proxy.http.post('api/MyTable/getPageData', data, true)

// ✅ 正确：table.url 末尾带斜杠
table: { url: 'api/MyTable/' }  // 框架自动拼接 getPageData

// ❌ 错误：以 / 开头（会变成绝对路径）
proxy.http.post('/api/MyTable/getPageData', data, true)

// ❌ 错误：以 /api/ 开头（会变成 //api/）
table: { url: '/api/MyTable/' }
```

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `res.data` 是 undefined | http.js 已返回 `response.data`，再加 `.data` 多套一层 | 直接用 `res`，不要 `.data` |
| 二进制下载得到空 | 同上，`res?.data` 多套一层 | `const buf = await http.get(url, null, false, {responseType:'arraybuffer'})` |
| 401 跳转登录页 | Token 过期 | 正常行为，重新登录 |
| 原生 `fetch` 请求 401 | fetch 不带 Vol 的 JWT Header | 用 `proxy.http` 而非 `fetch` |
| URL 双斜杠 `//api/` | table.url 以 `/` 开头 | 去掉开头的 `/`，用 `api/xxx/` |
| `未找到请求地址` 弹窗 | 路由不存在或 HTTP 方法不匹配 | 检查 Controller 是否注册、`[HttpPost]` 特性 |

> **踩坑索引**：[2026-08-10 E1 http.js res.data 多套一层](./05-踩坑记录/2026-08-10_DocExtractionRule%20预览链%2012%20类踩坑与根因修复汇总.md) · [2026-08-10 E2 原生 fetch 缺 JWT](./05-踩坑记录/2026-08-10_DocExtractionRule%20预览链%2012%20类踩坑与根因修复汇总.md)

---

## 九、前端：字段级事件与 formatter

> **官方文档**：http://v3.volcore.xyz/docs/view-grid/components.html · http://v3.volcore.xyz/docs/view-grid/event/

### 9.1 编辑表单字段事件

```javascript
// 在 onInit 或 modelOpenAfter 中绑定
const onInit = async ($vm) => {
  gridRef = $vm
}

const modelOpenAfter = async (row, action) => {
  // 获取编辑表单字段配置
  const nameOpt = gridRef.getFormOption('Name')
  
  // onChange：值变化
  nameOpt.onChange = (val) => {
    console.log('Name changed:', val)
  }
  
  // onKeyPress：按键
  nameOpt.onKeyPress = ($event) => {
    if ($event.key === 'Enter') {
      // 回车处理
    }
  }
  
  // blur：失焦
  nameOpt.blur = () => { }
  
  // focus：聚焦
  nameOpt.focus = () => { }
  
  // 联动：A 变化时刷新 B
  const categoryOpt = gridRef.getFormOption('CategoryCode')
  categoryOpt.onChange = async (val) => {
    const subOpt = gridRef.getFormOption('SubCategoryCode')
    subOpt.data = await loadSubCategories(val)
  }
  
  // 动态隐藏/显示字段
  const typeOpt = gridRef.getFormOption('Type')
  typeOpt.onChange = (val) => {
    const extraOpt = gridRef.getFormOption('ExtraField')
    extraOpt.hidden = val !== 'special'
  }
}
```

### 9.2 查询表单字段事件

```javascript
const onInited = async () => {
  // 获取查询表单字段配置
  const searchOpt = gridRef.getSearchFormOption('Name')
  searchOpt.onChange = (val) => {
    // 查询条件变化时的逻辑
  }
}
```

### 9.3 表格列 formatter

```javascript
const onInited = async () => {
  gridRef.columns.forEach(c => {
    // ⚠️ formatter 必须是函数，不能是 true/字符串
    if (c.field === 'Status') {
      c.formatter = (row) => {
        return row.Status === 1 ? '启用' : '禁用'
      }
    }
    
    // 字典翻译
    if (c.field === 'Type') {
      c.formatter = (row) => {
        const dict = c.bind?.data || []
        const item = dict.find(d => d.key === row.Type)
        return item?.value || row.Type
      }
    }
    
    // 日期格式化
    if (c.field === 'CreateDate') {
      c.formatter = (row) => {
        if (!row.CreateDate) return ''
        return new Date(row.CreateDate).toLocaleString('zh-CN')
      }
    }
  })
}
```

### 9.4 明细表格列事件

```javascript
const onInited = async () => {
  gridRef.detailOptions.columns.forEach(c => {
    // 明细列 onChange（行内编辑时触发）
    if (c.field === 'Qty') {
      c.onChange = (row, column, index) => {
        // 实时计算
        row.Amount = row.Qty * row.Price
      }
    }
    
    // 明细列 formatter（实时计算显示）
    if (c.field === 'Amount') {
      c.formatter = (row) => {
        return (row.Qty || 0) * (row.Price || 0)
      }
    }
    
    // 动态控制是否可编辑
    if (c.field === 'Discount') {
      c.checkEdit = (row, column, index) => {
        return row.Status === 'draft'  // 只有草稿状态可编辑
      }
    }
  })
}
```

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `formatter is not a function` | 把 formatter 设为 `true` 或字符串 | 必须设为函数 `c.formatter = (row) => ...` |
| `getFormOption` 返回 undefined | 在 `onInit` 中调用但此时表单未初始化 | 在 `modelOpenAfter` 中调用 |
| 联动不生效 | `onChange` 绑定在 `onInited` 而非 `modelOpenAfter` | 编辑表单字段事件在 `modelOpenAfter` 绑定 |
| 明细实时计算不更新 | 直接赋值 `row.Amount = x` 但未触发响应式 | Vue 3 的 reactive 已支持，确保 row 来自 `gridRef.detailOptions` |

> **踩坑索引**：[2026-08-03 P2-05 formatter 不是函数](./05-踩坑记录/2026-08-03_Phase2联调全栈问题修复记录.md)

---

## 十、菜单与权限系统

> **官方文档**：http://v3.volcore.xyz/docs/view-grid/properties.html（权限部分）
> **关键表**：`Sys_Menu`、`Sys_Role`、`Sys_User`

### 10.1 Sys_Menu 核心字段

| 字段 | 类型 | 说明 | 常见值 |
|------|------|------|--------|
| `Menu_Id` | int | 主键 | 自增 |
| `ParentId` | int | 父菜单 ID | 0 = 顶级 |
| `MenuName` | string | 菜单名称 | "认证机构管理" |
| `Url` | string | 前端路由路径 | "/CertificationBody" |
| `MenuType` | string | 菜单类型 | **见下表** |

### 10.2 MenuType 详解（90% 菜单不显示的原因）

| MenuType 值 | 含义 | 前端渲染 |
|-------------|------|----------|
| `"top"` | 顶级菜单（布局容器） | 渲染为顶部导航项 |
| `"classics"` | 经典布局顶级菜单 | 渲染为左侧菜单分组 |
| `null` / `""` | 普通菜单项 | 渲染为可点击菜单 |
| `"sub"` | 子菜单容器 | 渲染为折叠组 |

**关键规则**：
- 顶级菜单的 `ParentId = 0`
- 子菜单的 `ParentId` = 父菜单的 `Menu_Id`
- `MenuType` 决定布局容器，普通菜单项留空即可
- **如果 MenuType 填错，菜单不显示**

### 10.3 菜单缓存机制

Vol 使用 **Redis + 内存双层缓存**：

```
1. 首次请求 → 查数据库 → 写入 Redis → 写入内存
2. 后续请求 → 读内存（毫秒级）
3. 菜单修改后 → 需要清除缓存
```

**清除缓存命令**：
```bash
# 清除 Redis 菜单缓存
redis-cli -p 6380 DEL "vol:menu:*"

# 或重启后端服务（清除内存缓存）
```

### 10.4 权限控制

```csharp
// Controller 级别权限（自动从表名推导）
[Route("api/CertCertificationBody")]
[ApiController]
public class CertCertificationBodyController : ApiBaseController<ICertificationBodyService>
{
    // 所有 Action 自动检查权限
    // 权限名 = 控制器名（去掉 Controller 后缀）= "CertCertificationBody"
}

// Action 级别权限覆盖
[ApiActionPermission("CertCertificationBody", ActionPermissionOptions.Search)]
[HttpPost, Route("GetCustomData")]
public object GetCustomData() { ... }

// 免登录接口
[AllowAnonymous]
[HttpGet, Route("PublicData")]
public object PublicData() { ... }

// 超级管理员自动放行所有权限
```

### 10.5 前端路由配置

```javascript
// vol.web/src/router/viewGird.js
{
  path: '/CertificationBody',
  name: 'CertificationBody',
  component: () => import('@/views/cert/CertificationBody/index.vue'),
  meta: { keepAlive: true }
}
```

**关键规则**：
- 路由 `path` 必须与 `Sys_Menu.Url` 一致
- 只为**实际存在**的 Vue 组件添加路由
- 未开发的页面不要添加路由（否则 404 白屏）

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| 菜单不显示 | `MenuType` 填错 | 顶级菜单用 `"top"` 或 `"classics"`，普通菜单留空 |
| 菜单不显示 | `ParentId` 指向不存在的菜单 | 检查 ParentId 是否正确 |
| 菜单修改后不生效 | Redis/内存缓存未清除 | 清除 Redis 缓存或重启后端 |
| 点击菜单白屏 | 路由对应的 Vue 组件不存在 | 确保路由 path 对应的 .vue 文件存在 |
| 403 无权限 | 用户角色未分配菜单权限 | 在角色管理中分配菜单权限 |

> **踩坑索引**：[2026-08-11 Vol 框架菜单配置完整指南](./05-踩坑记录/2026-08-11_Vol框架菜单配置完整指南与踩坑记录.md)

---

## 十一、数据字典系统

> **关键表**：`Sys_Dictionary`（字典分类）、`Sys_DictionaryList`（字典项）
> **缓存机制**：版本号驱动，首次加载后缓存

### 11.1 后台配置

```sql
-- 1. 创建字典分类
INSERT INTO Sys_Dictionary (DictionaryCode, DictionaryName, Enable) 
VALUES ('cert_status', '认证状态', 1);

-- 2. 创建字典项
INSERT INTO Sys_DictionaryList (DicNo, DictValue, DictText, Sort, Enable) 
VALUES ('cert_status', '1', '待审核', 1, 1);
INSERT INTO Sys_DictionaryList (DicNo, DictValue, DictText, Sort, Enable) 
VALUES ('cert_status', '2', '审核中', 2, 1);
INSERT INTO Sys_DictionaryList (DicNo, DictValue, DictText, Sort, Enable) 
VALUES ('cert_status', '3', '已认证', 3, 1);
```

### 11.2 前端使用

```javascript
// 方式 A：options.js 中 bind 绑定
{ field: 'Status', title: '状态', type: 'select', bind: { key: 'cert_status' } }
// 框架自动加载字典数据并翻译

// 方式 B：onInit 中手动加载
const onInit = async ($vm) => {
  gridRef = $vm
  const dict = await proxy.http.get('api/Sys_Dictionary/GetVueDictionary', 
    { dicNos: 'cert_status' }, true)
  // dict 是 [{ dictionaryNo: 'cert_status', data: [{key, value}] }]
  
  const statusCol = gridRef.columns.find(c => c.field === 'Status')
  statusCol.bind = { data: dict[0].data }
}

// 方式 C：批量加载多个字典
const onInit = async ($vm) => {
  gridRef = $vm
  const dicts = await proxy.http.get('api/Sys_Dictionary/GetVueDictionary',
    { dicNos: 'cert_status,enable,org_type' }, true)
  // dicts 是数组，每个元素 { dictionaryNo, data }
}
```

### 11.3 字典缓存刷新

```javascript
// 前端刷新字典缓存
gridRef.initDicKeys()
```

---

## 十二、Vol 返回格式规范（JsonNormal）

> **官方文档**：http://v3.volcore.xyz/docs/cs/dev/case.html

### 12.1 两种 JSON 返回方式

```csharp
// JsonNormal：保持 PascalCase（与实体属性名/前端 columns.field 一致）
return JsonNormal(new { Name = "测试", Code = "001" });
// 输出：{ "Name": "测试", "Code": "001" }

// Json：转为 camelCase
return Json(new { Name = "测试", Code = "001" });
// 输出：{ "name": "测试", "code": "001" }
```

### 12.2 何时用哪个

| 场景 | 用哪个 | 原因 |
|------|--------|------|
| 自定义 API 返回业务数据 | **JsonNormal** | 前端 columns.field 是 PascalCase |
| 标准 CRUD（getPageData/Add/Update/Del） | 已内置 JsonNormal | ApiBaseController 自动处理 |
| 返回给第三方系统 | Json | RESTful 惯例 camelCase |
| WebSocket / SignalR 推送 | JsonNormal | 前端按 PascalCase 解析 |

### 12.3 WebResponseContent 格式

```csharp
// Vol 统一的响应格式
return JsonNormal(webResponse.OK(responseData));
// 输出：{ "Status": true, "Message": "操作成功", "Data": responseData }

return JsonNormal(webResponse.Error("名称不能为空"));
// 输出：{ "Status": false, "Message": "名称不能为空", "Data": null }
```

### ⚠️ 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| 前端表格有数据但列空白 | Controller 返回用了 `Json()` | 改用 `JsonNormal()` |
| `ControllerBase` 没有 `JsonNormal` 方法 | 继承了 ASP.NET Core 的 `ControllerBase` | 继承 `VolController` 或 `ApiBaseController<T>` |
| 前端 `res.Status` 是 undefined | 返回用了 `Json()` 转为 camelCase | 用 `JsonNormal()` 保持 `Status` 而非 `status` |

> **踩坑索引**：[2026-08-07 EF Core Column 映射 400 错误](./05-踩坑记录/2026-08-07_EF-Core-Column映射snake_case导致400错误.md) · [2026-08-11 转换队列化 ControllerBase 缺 JsonNormal](./05-踩坑记录/2026-08-11-转换队列化实施踩坑记录.md)

---

## 附录：Vol 框架核心文件路径速查

| 文件 | 路径 | 用途 |
|------|------|------|
| ServiceBase | `VOL.Core/BaseProvider/ServiceBase.cs` | CRUD 虚方法 + 钩子调用 |
| ApplicationServiceBase | `VOL.Core/BaseProvider/ApplicationServiceBase.cs` | 钩子委托定义 |
| ApiBaseController | `VOL.Core/Controllers/Basic/ApiBaseController.cs` | 标准 REST 端点 |
| VolController | `VOL.Core/Controllers/Basic/VolController.cs` | JsonNormal/Json 方法 |
| VOLContext | `VOL.Core/EFDbContext/VOLContext.cs` | EF Core DbContext |
| UserContext | `VOL.Core/UserManager/UserContext.cs` | 当前用户上下文 |
| ActionPermissionFilter | `VOL.Core/Filters/ActionPermissionFilter.cs` | 权限检查 |
| ExceptionHandlerMiddleWare | `VOL.Core/Middleware/ExceptionHandlerMiddleWare.cs` | 全局异常捕获 |
| Logger | `VOL.Core/Services/Logger.cs` | 日志（队列+批量写入） |
| DictionaryManager | `VOL.Core/Infrastructure/DictionaryManager.cs` | 字典缓存 |
| http.js | `vol.web/src/api/http.js` | 前端 HTTP 封装 |
| ViewGrid | `vol.web/src/components/basic/ViewGrid/` | 前端核心组件 |
| ViewGridFilter | `vol.web/src/components/basic/ViewGrid/ViewGridFilter.js` | 前端 Hook 权威列表 |

---

## 附录：官方文档 URL 映射表

| 主题 | URL |
|------|-----|
| 后台 Service 总览 | http://v3.volcore.xyz/docs/cs/service/guid.html |
| 数据库访问 | http://v3.volcore.xyz/docs/cs/dev/db.html |
| 接口返回大小写 | http://v3.volcore.xyz/docs/cs/dev/case.html |
| 前端 API 传参 | http://v3.volcore.xyz/docs/cs/dev/api.html |
| ViewGrid 属性 | http://v3.volcore.xyz/docs/view-grid/properties.html |
| ViewGrid 方法 | http://v3.volcore.xyz/docs/view-grid/methods/ |
| ViewGrid 组件 | http://v3.volcore.xyz/docs/view-grid/components.html |
| ViewGrid 事件 | http://v3.volcore.xyz/docs/view-grid/event/ |
| 新建钩子 | http://v3.volcore.xyz/docs/cs/service/add.html |
| 编辑钩子 | http://v3.volcore.xyz/docs/cs/service/update.html |
| 删除钩子 | http://v3.volcore.xyz/docs/cs/service/del.html |
| searchBefore | http://v3.volcore.xyz/docs/view-grid/methods/searchBefore.html |
| vol-form | http://v3.volcore.xyz/docs/form/ |
| vol-table | http://v3.volcore.xyz/docs/table/ |
| vol-box | http://v3.volcore.xyz/docs/box/ |
| 新页面编辑 | http://v3.volcore.xyz/docs/edit/ |

---

*（内容由 AI 生成，基于 Vol 框架官方文档 + 项目踩坑记录整理。最后更新：2026-08-11）*
