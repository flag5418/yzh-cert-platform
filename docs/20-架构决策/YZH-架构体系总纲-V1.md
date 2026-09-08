# YZH 架构体系总纲（V1）

> **版本**：V1.0 | **日期**：2026-09-08 | **状态**：已定稿，强制执行
>
> 本文档是 YZH（映智汇）认证平台的**唯一权威架构说明**。
> 未来的 AI 助手只需理解本文档，即可快速掌握整个架构的运行机制。
> 所有编码必须对齐本文，违反本文档的代码不得合入主分支。

---

## 一、架构核心思想

### 1.1 设计哲学

```
┌─────────────────────────────────────────────────────────────────────┐
│                        YZH 架构五原则                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. 配置驱动 UI    → 前端不硬编码，所有行为由后端 JSON 配置决定        │
│  2. 统一基类       → 单表/左树右表均继承统一控制器                     │
│  3. API 固定       → 方法名、请求/响应格式全局统一                    │
│  4. 业务差异隔离   → 差异只通过配置和 virtual 覆盖实现                 │
│  5. 增量更新       → 前端 Split 数据方法，不重新请求                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 1.2 前后端继承体系

```
�─────────────────────────────────────────────────────────────────────────┐
│                              后端继承体系                                │
├─────────────────────────────────────────────────────────────────────────�
│                                                                         │
│   ControllerBase (ASP.NET)                                              │
│       │                                                                 │
│       ▼                                                                 │
│   YzhControllerBase<V>           ← 单表 CRUD                            │
│       │                                   /config /filter /add          │
│       │                                   /update /delete /action       │
│       │ 继承（复用全部单表能力）                                          │
│       ▼                                                                 │
│   TreeTableControllerBase<T, V>  ← 左树右表                              │
│                                      新增 /tree/* 树接口                  │
│                                      继承 /filter /add /update /delete   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────�
│                              前端继承体系                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   CrudPageLogic<V>               ← 单表 CRUD                            │
│       │                                   loadPage / add / update        │
│       │                                   /delete / split 方法           │
│       │ 继承（复用全部单表逻辑）                                          │
│       ▼                                                                 │
│   TreeTableLogic<V>              ← 左树右表                              │
│                                      新增 loadTreeRoot / loadChildren     │
│                                      新增 onNodeClick / 联动表格          │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────�
```

---

## 二、核心概念

### 2.1 Code 作为业务主键

> **铁律**：所有业务操作统一使用 `Code`（Guid）作为业务唯一标识。

| 字段 | 用途 | 说明 |
|------|------|------|
| `Id` | 自增主键 | 仅标识插入顺序，不参与任何业务逻辑 |
| `Code` | 业务唯一标识 | 用于所有关联、引用、查询、更新、删除 |

```csharp
// ❌ 错误：使用 Id 进行业务操作
public async Task<ActionResult> Delete(int id) { ... }

// ✅ 正确：使用 Code 进行业务操作
public async Task<ActionResult> Delete(string[] codes) { ... }
```

### 2.2 EntityConfig 配置驱动

一份 JSON 配置同时驱动**表格列**和**表单字段**：

| 属性 | 表格 | 表单 | 说明 |
|------|------|------|------|
| `sxh` | 列排序号 ☑️ | - | 升序排列 |
| `xsFlag` | 显示 ☑️ | - | 控制列可见性 |
| `bcFlag` | - | 显示/编辑 ☑️ | 控制表单字段可见性 |
| `type` | 列类型 | 控件类型 | `Other`=只读展示 |
| `row/col/colSpan` | - | Grid 布局位置 | CSS Grid 表单布局 |
| `yxk` | - | 允许空 ☑️ | 必填校验依据 |
| `mrz` | - | 默认值 | 新增时的预填充 |

### 2.3 字段控制语义

```
┌─────────────────────────────────────────────────────────────────────┐
│                      DefineColumn 字段控制                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  xsFlag ──→ 表格中是否显示此列                                        │
│  bcFlag ──→ 表单中显示且可编辑（false=隐藏或只读展示）                  │
│  type=Other ──→ 纯展示字段，不在表单渲染为可编辑控件                    │
│  yxk=false ──→ 不允许空，表单必填校验                                 │
│  mrz ──→ 默认值，新增表单时的预填充值                                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 三、统一 API 端点

### 3.1 单表 CRUD API

| API | 方法 | 请求体 | 响应 | 说明 |
|---|---|---|---|---|
| `/config` | GET | - | `ApiResponse<EntityConfig>` | 获取页面配置 |
| `/filter` | POST | `FilterRequest` | `ApiResponse<PagedResult<V>>` | 过滤查询（推荐） |
| `/add` | POST | V entity | `ApiResponse<V>` | 新增 |
| `/update` | POST | V entity | `ApiResponse<V>` | 修改 |
| `/delete` | POST | `string[] codes` | `ApiResponse<string>` | 批量删除 |
| `/action/{methodName}` | POST | V entity | `ApiResponse<object?>` | 自定义行操作 |
| `/export` | POST | `ExportRequest` | `FileStreamResult` | 导出（完整表结构） |
| `/import` | POST | `IFormFile` | `ApiResponse<ImportResult>` | 批量导入 |

### 3.2 左树右表 API（继承 + 新增）

| API | 方法 | 请求体 | 响应 | 说明 |
|---|---|---|---|---|
| `/config` | GET | - | `ApiResponse<TreeTableConfig>` | 获取树+表格配置（覆盖） |
| `/filter` | POST | `FilterRequest` | `ApiResponse<PagedResult<V>>` | 自动注入树过滤 |
| `/tree/root` | POST | - | `ApiResponse<TreeItemDto[]>` | 加载根节点 |
| `/tree/children` | POST | `{ parentCode, level }` | `ApiResponse<TreeItemDto[]>` | 懒加载子节点 |
| `/tree/add` | POST | T entity | `ApiResponse<TreeItemDto>` | 新增树节点 |
| `/tree/update` | POST | T entity | `ApiResponse<TreeItemDto>` | 修改树节点 |
| `/tree/delete` | POST | `string[]` | `ApiResponse<string>` | 删除树节点 |
| `/tree/action/{name}` | POST | T entity | `ApiResponse<object?>` | 树节点自定义操作 |

---

## 四、后端核心类

### 4.1 YzhControllerBase\<V\> — 单表基类

**位置**：`src/yzh-core/YZH.Core.Api/Controllers/YzhControllerBase.cs`

**核心职责**：
- 统一单表 CRUD API
- 自动从 `EntityConfigHelper` 加载 JSON 配置
- 6 个生命周期钩子（Add/Update/Delete 各 Before/After）
- 可配置的字段校验（基于 BCFlag/YXK）

**必须实现的成员**：

```csharp
public abstract class YzhControllerBase<V> : ControllerBase where V : class
{
    /// <summary>加载配置（必须覆盖以指定配置名）</summary>
    protected virtual EntityConfig LoadConfig() { ... }
}
```

**生命周期钩子**：

| 钩子 | 返回值 | 说明 |
|------|--------|------|
| `OnBeforeAdd(V entity)` | `(bool ok, string? msg)` | 校验/填充/可取消 |
| `OnAfterAdd(V entity)` | `Task` | 事务内后置处理 |
| `OnBeforeUpdate(V entity)` | `(bool ok, string? msg)` | 校验/可取消 |
| `OnAfterUpdate(V entity)` | `Task` | 事务内后置处理 |
| `OnBeforeDelete(string[] codes)` | `(bool ok, string? msg)` | 关联检查/级联/可取消 |
| `OnAfterDelete(int count)` | `Task` | 事务内后置处理 |
| `OnAfterCommitted()` | `Task` | 事务外（通知/缓存） |
| `OnQueried(PagedResult<V> result)` | void | 字典翻译/脱敏 |
| `OnBuildingFilter(List<FilterItem> filters)` | `List<FilterItem>` | 自定义过滤逻辑 |

**使用示例**：

```csharp
[Route("api/[controller]")]
public class SysUserController : YzhControllerBase<Sys_User>
{
    public SysUserController(EntityService<Sys_User> service, IUserContext userContext)
        : base(service, userContext) { }

    /// <summary>新增前校验用户名唯一性</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_User entity)
    {
        if (await Entity.ExistsAsync(u => u.UserName == entity.UserName))
            return (false, $"用户名 [{entity.UserName}] 已存在");
        return (true, null);
    }

    /// <summary>查询后翻译角色名称</summary>
    protected override void OnQueried(PagedResult<Sys_User> result)
    {
        foreach (var item in result.Items)
            item.RoleName = item.RoleId switch
            {
                1 => "超级管理员",
                20 => "审核管理员",
                _ => "未知"
            };
    }
}
```

### 4.2 TreeTableControllerBase\<T, V\> — 左树右表基类

**位置**：`src/yzh-core/YZH.Core.Api/Controllers/TreeTableControllerBase.cs`

**泛型约束**：
- `T`：树节点实体（必须实现 `ITreeEntity`）
- `V`：表格实体

**构造函数配置**：

```csharp
public OrganizationController(
    EntityService<Sys_Organization> treeEntityService,
    EntityService<Sys_User> tableEntityService,
    IUserContext userContext)
    : base(treeEntityService, tableEntityService, userContext)
{
    // 1. 树字段映射配置（必须）
    TreeConfig.NameField = "OrgName";
    TreeConfig.CodeField = "Code";
    TreeConfig.ParentCodeField = "ParentCode";
    TreeConfig.RelateField = "OrgCode"; // 表格中关联过滤的字段

    // 2. 注册树节点自定义操作
    RegisterTreeAction("disable", DisableOrgRecursiveAsync);  // POST /tree/action/disable
    RegisterTreeAction("enable", EnableOrgAsync);            // POST /tree/action/enable

    // 3. 注册表格行操作
    RegisterRowAction("disable", DisableUserAsync);  // POST /action/disable
    RegisterRowAction("enable", EnableUserAsync);    // POST /action/enable
}
```

**树节点生命周期钩子**：

| 钩子 | 说明 |
|------|------|
| `OnBeforeAddTree(T entity)` | 同级名称唯一性校验 |
| `OnBeforeUpdateTree(T entity)` | 防环校验 |
| `OnBeforeDeleteTree(string[] codes)` | 检查是否有子节点 |

### 4.3 EntityConfigHelper — 配置加载

**位置**：`src/yzh-core/YZH.Core.Stand/Helpers/EntityConfigHelper.cs`

```csharp
// 静态帮助类，带1小时缓存 + FileWatcher 自动刷新
var config = EntityConfigHelper.GetConfig<Sys_User>();
var byName = EntityConfigHelper.GetConfig("Sys_User");
```

**配置文件约定**：
- 文件名 = 类名（如 `Sys_User.json`）
- 路径：`YZH.Core.Web/Assets/EntityConfigs/`
- JSON 结构 = `EntityConfig` 类

### 4.4 EntityService\<T\> — 原子服务

**核心方法**：

| 方法 | 说明 |
|------|------|
| `Insert(entity, clientIp)` | 新增 |
| `Update(entity, clientIp, updateFields?)` | 修改（支持 BCFlag 过滤） |
| `DeleteBatch(codes, hardDelete, clientIp)` | 批量删除 |
| `GetByCode(code)` | 按 Code 查询 |
| `ExistsAsync(predicate)` | 存在性检查 |
| `GetPageAsync(pagerOptions)` | 分页查询 |
| `GetRootNodes()` | 获取根节点 |
| `GetChildren(parentCode)` | 获取子节点 |

---

## 五、前端核心类

### 5.1 CrudPageLogic\<V\> — 单表 Logic 基类

**位置**：`src/certplatform-web/share/src/logic/CrudPageLogic.ts`

**核心状态**：

```typescript
export abstract class CrudPageLogic<V extends Record<string, any>> {
    // 必须设置
    abstract controllerName: string

    // 从 config 自动派生的 UI 结构
    get columns(): YzhTableColumn<V>[]      // 表格列（xsFlag=true）
    get formFields(): YzhFormField[]         // 表单字段（bcFlag=true）
    get searchFields(): SearchField[]       // 搜索栏
    get toolbarButtons(): ToolbarButton[]   // 工具按钮
    get rowButtons(): RowButton[]           // 行操作按钮

    // 数据状态
    rows = ref<V[]>([])
    pagination = reactive({ page: 1, pageSize: 20, total: 0 })
    loading = ref(false)
    config = ref<EntityConfigDto | null>(null)

    // 核心方法
    async init(): Promise<void>
    async loadPage(): Promise<void>
    async add(entity: Partial<V>): Promise<V>
    async update(entity: Partial<V>): Promise<V>
    async delete(codes: string[]): Promise<void>
    async executeAction(methodName: string, row: V): Promise<void>

    // Split 增量更新方法
    protected removeRowByCode(code: string): void
    protected replaceRowByCode(code: string, row: V): void
    protected insertRow(row: V): void
}
```

### 5.2 TreeTableLogic\<V\> — 左树右表 Logic 基类

**位置**：`src/certplatform-web/share/src/logic/TreeTableLogic.ts`

**继承关系**：`TreeTableLogic<V> extends CrudPageLogic<V>`

**新增状态**：

```typescript
export abstract class TreeTableLogic<V> extends CrudPageLogic<V> {
    // 树状态
    treeData = ref<TreeNode[]>([])
    treeLoading = ref(false)
    selectedNode = ref<TreeNode | null>(null)
    treeTableConfig = ref<TreeTableConfigDto | null>(null)

    // 新增方法
    async loadTreeRoot(): Promise<void>
    async loadChildren(node: TreeNode): Promise<TreeNode[]>
    async onNodeClick(node: TreeNode): Promise<void>
    async loadPageWithTree(treeCode: string): Promise<void>

    // 树节点 CRUD
    async addTreeNode(parentNode: TreeNode | null, data: Record<string, any>): Promise<TreeNode | null>
    async updateTreeNode(node: TreeNode, newName: string): Promise<void>
    async deleteTreeNode(node: TreeNode): Promise<void>
    async executeTreeAction(methodName: string, node: TreeNode): Promise<any>

    // 钩子
    protected dtoToNode(dto: TreeItemDto, parent?: TreeNode): TreeNode
}
```

### 5.3 使用示例

**1. 单表页面**：

```typescript
// index.ts
export class UserPageLogic extends CrudPageLogic<Sys_User> {
    controllerName = 'SysUser'
    // 无需其他代码，config 驱动全部 UI
}

// index.vue
const page = new UserPageLogic()
await page.init()

// <YzhTable :columns="page.columns" :data-loader="loadData" />
// <YzhForm :fields="page.formFields" v-model="formData" />
```

**2. 左树右表页面**：

```typescript
// index.ts
export class OrgPageLogic extends TreeTableLogic<Record<string, any>> {
    controllerName = 'Organization'
    // 树配置从后端获取，无需额外代码
}

// index.vue
const page = new OrgPageLogic()
await page.init()

// <YzhTreeTable :tree-data="page.treeData.value" @tree-node-click="handleNodeClick" />
// <YzhTable :columns="page.columns" :data-loader="loadTableData" />
```

---

## 六、统一数据契约

### 6.1 请求模型

```typescript
// 过滤请求
interface FilterRequest {
    page: number
    pageSize: number
    sortField?: string
    sortOrder?: 'asc' | 'desc'
    filters: FilterItem[]
}

interface FilterItem {
    field: string
    operator: 'eq' | 'neq' | 'gt' | 'gte' | 'lt' | 'lte' | 'like' | 'in'
    value?: any
}

// 树懒加载请求
interface TreeChildrenRequest {
    parentCode: string
    level: number
}
```

### 6.2 响应模型

```typescript
// 统一响应包装
interface ApiResponse<T> {
    success: boolean
    message: string
    data: T
    code: number
}

// 分页结果
interface PagedResult<T> {
    items: T[]
    total: number
    page: number
    pageSize: number
}

// 树节点 DTO
interface TreeItemDto {
    code: string
    name: string
    parentCode: string | null
    nodeType?: string
    isLeaf: boolean
    level: number
    extra?: Record<string, any>  // 扩展字段（enable、remark等）
}
```

### 6.3 EntityConfig 响应

```typescript
interface EntityConfigDto {
    configName: string
    tableName: string
    title: string
    columns: ColumnConfig[]
    toolbar: ToolbarConfigDto
    rowButtons: RowButtonConfigDto
    searchFields?: SearchFieldConfig[]
    readOnly?: boolean
    checkboxSelection?: boolean
}

interface ColumnConfig {
    fieldName: string        // 字段名（PascalCase）
    desName: string          // 显示标题
    type: string             // 控件类型
    xsFlag: boolean          // 表格显示
    bcFlag: boolean          // 表单编辑
    yxk: boolean             // 允许空
    width?: number
    enable: boolean
    sortable: boolean
}
```

### 6.4 TreeTableConfig 响应

```typescript
interface TreeTableConfigDto {
    tableConfig: EntityConfigDto
    treeConfig: TreeBehaviorConfig
}

interface TreeBehaviorConfig {
    lazy: boolean
    allowEdit: boolean
    allowDelete: boolean
    allowRename: boolean
    rootParentCode: string | null
    nameField: string
    codeField: string
    parentCodeField: string
    relateField: string           // 关联字段（表格过滤用）
    noSelectionBehavior: 'empty' | 'all'
    maxLevel: number
    allowDeleteWithChildren: boolean
}
```

---

## 七、前端组件体系

### 7.1 YzhTreeTable — 左树右表骨架

**位置**：`src/certplatform-web/yzh.vue.core/src/components/layout/YzhTreeTable.vue`

```vue
<YzhTreeTable
    :tree-data="logic.treeData.value"
    :tree-width="260"
    :tree-toolbar="true"
    :tree-searchable="true"
    :tree-lazy="true"
    :tree-load-data="logic.loadChildren.bind(logic)"
    @tree-node-click="handleNodeClick"
>
    <template #default>
        <!-- 右侧表格内容 -->
        <YzhTable ... />
    </template>
</YzhTreeTable>
```

### 7.2 YzhTable — 数据表格

**位置**：`src/certplatform-web/yzh.vue.core/src/components/table/`

```vue
<YzhTable
    :columns="logic.columns"           // 自动从 config 派生
    :data-loader="loadTableData"
    :search-fields="logic.searchFields"
    :selectable="true"
    @selection-change="handleSelectionChange"
>
    <!-- 工具栏 -->
    <template #toolbar-left>
        <el-button @click="handleAdd">新增</el-button>
    </template>

    <!-- 自定义列 -->
    <template #column-enable="{ value }">
        <el-tag :type="value === 1 ? 'success' : 'info'">
            {{ value === 1 ? '启用' : '禁用' }}
        </el-tag>
    </template>
</YzhTable>
```

### 7.3 YzhForm — 表单

**位置**：`src/certplatform-web/yzh.vue.core/src/components/form/`

```vue
<YzhForm
    v-model="formData"
    :fields="logic.formFields"         // 自动从 config 派生
    :loading="submitting"
    @submit="handleSubmit"
    @reset="handleReset"
/>
```

---

## 八、开发约束清单

### 8.1 后端约束

| # | 约束 | 说明 |
|---|---|---|
| 1 | 单表控制器必须继承 `YzhControllerBase<V>` | 不允许直接继承 ControllerBase |
| 2 | 左树右表控制器必须继承 `TreeTableControllerBase<T,V>` | 不允许直接继承 ControllerBase |
| 3 | 业务差异只能通过覆盖 virtual 方法实现 | 不允许绕过基类直接暴露新 API |
| 4 | 所有 API 必须遵循统一命名规范 | `/config /filter /add /update /delete /tree/*` |
| 5 | 树接口返回必须是 `TreeItemDto` | 不允许返回数据库实体 |
| 6 | `/config` 必须返回完整配置 | 前端据此渲染页面，不允许前端硬编码 |
| 7 | 行操作必须通过 `RegisterRowAction` 注册 | 不允许在 Controller 中暴露额外 POST 方法 |
| 8 | 树节点操作必须通过 `RegisterTreeAction` 注册 | 使用 `/tree/action/{name}` 统一入口 |
| 9 | 统一使用 Code 作为业务主键 | 废除 `GetById/DeleteById`，使用 `GetByCode/DeleteByCode` |
| 10 | Entity 应有 `[Column("snake_case")]` 映射 | snake_case 表实体必须显式覆盖基类审计属性 |

### 8.2 前端约束

| # | 约束 | 说明 |
|---|---|---|
| 1 | 单表 Logic 必须继承 `CrudPageLogic<V>` | 不允许重新实现 loadPage/add/update/delete |
| 2 | 左树右表 Logic 必须继承 `TreeTableLogic<V>` | 不允许重新实现树加载/联动逻辑 |
| 3 | 工具栏/搜索栏/行操作必须由配置驱动 | 不允许在 Vue 模板中硬编码按钮 |
| 4 | 操作列按钮必须由配置驱动 | 不允许在模板中固定"编辑""删除" |
| 5 | 树操作按钮必须由配置驱动 | 不允许在模板中固定 |
| 6 | 页面加载后必须先调 `loadConfig()` | 获取配置后再渲染页面 |
| 7 | 树与表格必须使用分离的 API 接口 | 不允许同一接口同时服务树加载和表格加载 |
| 8 | 删除/修改后使用 Split 方法增量更新 | 不允许重新请求整个 `/filter` |
| 9 | Props 名使用 camelCase | PascalCase 字段在前端自动转换 |
| 10 | 表单字段从 `logic.formFields` 获取 | 不允许硬编码字段列表 |

---

## 九、常用开发场景

### 9.1 新增单表页面（3 步）

```
Step 1: 创建 EntityConfig JSON 配置（如 Sys_Xxx.json）
Step 2: 创建控制器继承 YzhControllerBase<Xxx>
Step 3: 创建 Vue 页面继承 CrudPageLogic<Xxx>

共计约 30 行代码（实体 + JSON + 5 行 Controller + 10 行 Vue）
```

### 9.2 新增左树右表页面（4 步）

```
Step 1: 创建 EntityConfig JSON 配置（表格列+表单字段）
Step 2: 创建 TreeConfig（在构造函数中配置字段映射）
Step 3: 创建控制器继承 TreeTableControllerBase<T, V>
Step 4: 注册自定义操作（启用/禁用等）
Step 5: 创建 Vue 页面继承 TreeTableLogic<V>

共计约 50 行代码
```

### 9.3 添加启用/禁用功能

**后端**：

```csharp
// 1. 在构造函数注册操作
RegisterRowAction("disable", DisableUserAsync);
RegisterRowAction("enable", EnableUserAsync);

// 2. 实现操作方法
private async Task<Result<ApiResponse<object?>>> DisableUserAsync(Sys_User entity)
{
    var result = await Entity.GetByCode(entity.Code);
    if (!result.Success) return Result<ApiResponse<object?>>.Fail("用户不存在");

    var user = result.Data!;
    user.Enable = 0;
    await Entity.Update(user, UserContext.ClientIp);
    return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用"));
}
```

**前端**：

```typescript
// 调用
await logic.executeAction('disable', row)

// 或使用泛型 API
await yzhApi.post('/api/Organization/action/disable', row)
```

### 9.4 实现 ShowDisabled 开关

**后端**（覆盖 `OnBuildingFilter`）：

```csharp
protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
{
    var showDisabled = false;
    var sdFilter = filters.FirstOrDefault(f => f.Field == "ShowDisabled");
    if (sdFilter != null && bool.TryParse(sdFilter.Value?.ToString(), out var sd))
    {
        showDisabled = sd;
        filters.Remove(sdFilter);
    }

    if (!showDisabled)
    {
        filters.RemoveAll(f => f.Field == "Enable");
        filters.Add(new FilterItem { Field = "Enable", Operator = "eq", Value = "1" });
    }

    return filters;
}
```

**前端**：

```typescript
// 在 SearchField 中添加一个 Switch 开关
// 开关变化时，在 filters 中加入 { field: 'ShowDisabled', value: 'true' }
```

---

## 十、核心文件索引

### 10.1 后端关键文件

| 文件 | 职责 |
|------|------|
| `YZH.Core.Api/Controllers/YzhControllerBase.cs` | 单表 CRUD 基类 |
| `YZH.Core.Api/Controllers/TreeTableControllerBase.cs` | 左树右表基类 |
| `YZH.Core.Stand/Models/EntityConfig.cs` | 配置模型 |
| `YZH.Core.Stand/Models/DefineColumn.cs` | 列定义模型 |
| `YZH.Core.Stand/Models/Contracts.cs` | 统一契约 |
| `YZH.Core.Stand/Helpers/EntityConfigHelper.cs` | 配置加载器 |
| `YZH.Core.Api/Services/EntityService.cs` | 原子服务 |
| `YZH.Core.Web/Assets/EntityConfigs/*.json` | 配置 JSON |

### 10.2 前端关键文件

| 文件 | 职责 |
|------|------|
| `share/src/logic/CrudPageLogic.ts` | 单表 Logic 基类 |
| `share/src/logic/TreeTableLogic.ts` | 左树右表 Logic 基类 |
| `share/src/types/contracts.ts` | TypeScript 契约 |
| `share/src/types/tree.ts` | 树类型定义 |
| `yzh.vue.core/src/components/layout/YzhTreeTable.vue` | 左树右表骨架 |
| `yzh.vue.core/src/components/table/` | YzhTable 组件 |
| `yzh.vue.core/src/components/form/` | YzhForm 组件 |
| `share/src/api/generic.ts` | 泛型 API 接口 |

### 10.3 配 JSONObject 示例

**Sys_User.json**（人员管理）：

```json
{
  "title": "人员管理",
  "fillMode": "AutoFix",
  "columns": [
    { "fieldName": "UserName", "desName": "登录账号", "type": "TextBox",
      "sxh": 1, "xsFlag": true, "bcFlag": true, "yxk": false, "width": 140 },
    { "fieldName": "UserTrueName", "desName": "真实姓名", "type": "TextBox",
      "sxh": 2, "xsFlag": true, "bcFlag": true, "yxk": false, "width": 120 },
    { "fieldName": "RoleName", "desName": "角色", "type": "Other",
      "sxh": 3, "xsFlag": true, "bcFlag": false, "yxk": true, "width": 120 },
    { "fieldName": "Enable", "desName": "状态", "type": "Switch",
      "sxh": 4, "xsFlag": true, "bcFlag": true, "yxk": false,
      "mrz": "1", "width": 80 },
    { "fieldName": "UserPwd", "desName": "密码", "type": "PasswordBox",
      "sxh": 0, "xsFlag": false, "bcFlag": true, "yxk": false, "width": 140,
      "row": 1, "col": 0 }
  ]
}
```

---

## 十一、架构迁移指南

### 11.1 从 Vol 框架迁移到 YZH 架构

> **核心差异**：Vol 框架使用前端硬编码（options.js）定义表格/表单，YZH 架构使用后端 JSON 配置驱动 UI。

| 维度 | Vol 框架 | YZH 架构 |
|------|----------|----------|
| 表格列定义 | 前端 options.js 硬编码 | 后端 JSON，前端从 config 获取 |
| 表单字段定义 | 前端 VolForm 硬编码 | 后端 JSON，bcFlag 控制 |
| API 调用 | 多个不同接口 | 统一 `/filter` / `add` / `update` / `delete` |
| 树→表格联动 | 前端手动拼条件 | 后端自动注入 RelateField 过滤 |
| 行操作 | 直接调用方法 | `RegisterRowAction` 注册 |
| 数据更新 | 全量刷新 | Split 增量更新 |

### 11.2 归档文档

以下 Vol 框架相关文档已归档至 `docs/历史文档/归档-Vol框架弃用/`，仅供参考：

- `08-Vol框架实战速查手册.md`
- `09-常见错误对照表.md`
- `05-踩坑记录/`（Vol 相关）
- `07-标准页面开发流程.md`
- `vol-skill.md`

---

## 十二、常见问题

### Q1：如何知道某个字段是否在表格中显示？

**A**：查看 EntityConfig JSON 中的 `xsFlag` 属性。`xsFlag=true` 显示，`xsFlag=false` 隐藏。

### Q2：如何在表单中隐藏某个字段但仍提交？

**A**：设置 `bcFlag=true`（参与提交）但在模板中不使用该字段。或者在模板中通过 `v-if` 条件控制显示。

### Q3：如何添加新的行操作按钮？

**A**：
1. 后端：`RegisterRowAction("methodName", Handler)`
2. 前端：自动从 `rowButtons` 获取，无需额外配置
3. 调用：`logic.executeAction('methodName', row)`

### Q4：如何实现级联禁用？

**A**：在树节点的自定义操作中递归处理：

```csharp
RegisterTreeAction("disable", async (entity) => {
    // 1. 禁用当前节点
    // 2. 递归禁用所有子节点
    // 3. 禁用关联的所有表格记录（用户）
});
```

---

*文档结束 — 本文件为 YZH 架构体系的唯一权威描述。*
