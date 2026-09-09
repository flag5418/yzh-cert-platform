# YZH 架构设计风格 - V2.0

> 核心理念：简单代码结构，实现复杂业务功能  
> 设计原则：适度抽象，拒绝过度设计  
> 目标：形成独树一帜的架构风格

---

## 一、架构哲学

### 1.1 核心设计思想

```
┌─────────────────────────────────────────────────────────────────┐
│                     YZH 架构哲学                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   简单代码结构 + 适度抽象 = 复杂业务功能                          │
│                                                                 │
│   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │
│   │ 高频场景    │    │ 基类封装    │    │ 配置驱动    │         │
│   │ 极致抽象    │───▶│ 控制流程    │───▶│ 灵活扩展    │         │
│   └─────────────┘    └─────────────┘    └─────────────┘         │
│                                                                 │
│   拒绝：零代码、遍地插槽、过度注入、反射滥用                       │
│   追求：简单、可控、统一、高效                                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 设计原则

| 原则 | 说明 | 反例 |
|------|------|------|
| **适度抽象** | 只有2个以上业务使用相同模式时才提取基类 | 为单一场景创建抽象层 |
| **简单优先** | 代码结构清晰，不追求技巧 | 过度使用反射/动态代理 |
| **配置驱动** | 相同布局通过配置复用，而非代码继承 | 每页手写相同代码 |
| **统一控制** | 相似功能统一实现，验证一套逻辑 | 每个页面独立实现 |
| **渐进复杂** | 从简单开始，按需复杂度递增 | 一开始就过度设计 |

---

## 二、架构分层

### 2.1 四层架构模型

```
┌─────────────────────────────────────────────────────────────────┐
│                        表现层 (Presentation)                     │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  页面布局基类   │  │  表格/表单组件   │  │  原子功能函数   │ │
│  │  (PageLayout)   │  │  (YzhTable/     │  │  (useXxx)      │ │
│  │                 │  │   YzhForm)      │  │                 │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                        业务层 (Business)                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Logic基类      │  │  业务基类       │  │  领域实体       │ │
│  │  (CrudPage/     │  │  (CertService/   │  │  (EntityBase    │ │
│  │   TreeTable)    │  │   AuditService)  │  │   + 业务接口)   │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                        框架层 (Framework)                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  配置驱动引擎   │  │  统一异常处理   │  │  审计/权限/日志 │ │
│  │  (GridConfig/   │  │  (GlobalFilter) │  │  (Filter)       │ │
│  │   Workflow)     │  │                 │  │                 │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│                        数据层 (Data)                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  统一仓储       │  │  多租户过滤     │  │  数据库方言     │ │
│  │  (IRepository)  │  │  (TenantFilter) │  │  (Dialect)      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 各层职责

| 层级 | 职责 | 关键技术 |
|------|------|---------|
| **表现层** | UI渲染、用户交互 | Vue3组件、Composition API |
| **业务层** | 业务逻辑、流程控制 | Logic基类、Service基类 |
| **框架层** | 横切关注点、配置驱动 | 过滤器、中间件、配置引擎 |
| **数据层** | 数据访问、持久化 | EF Core、Dapper、Redis |

---

## 三、基类体系设计

### 3.1 后端基类体系

#### 3.1.1 Service基类继承图

```csharp
// 基类继承体系
EntityBase                    // 基础实体（审计字段）
    ├── CertEntityBase       // 认证业务实体（增加OrgCode等）
    │   ├── ISOStandard      // ISO标准
    │   ├── ISOClause        // ISO条款
    │   ├── ValidationRule   // NC规则
    │   └── ...
    │
ServiceBase<TEntity>          // Vol框架基础Service
    └── CertServiceBase      // 认证平台统一Service基类
        ├── CertEntityService<T>           // 通用CRUD Service
        ├── CertTreeService<T>             // 树形结构 Service
        ├── CertWorkflowService            // 工作流 Service
        └── CertAIService                  // AI集成 Service
```

#### 3.1.2 Service基类设计

```csharp
// 核心：CertServiceBase - 统一CRUD+生命周期+校验
public abstract class CertServiceBase<TEntity, TRepository> 
    : ServiceBase<TEntity, TRepository>
    where TEntity : CertEntityBase
    where TRepository : IRepository<TEntity>
{
    // 1. 生命周期钩子
    protected virtual EntityValidationResult OnAdding(TEntity entity) => OK;
    protected virtual void OnAdded(TEntity entity) { }
    protected virtual EntityValidationResult OnUpdating(TEntity entity, string? excludeCode) => OK;
    protected virtual void OnUpdated(TEntity entity) { }
    protected virtual EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities) => OK;
    protected virtual void OnDeleted(object[] keys) { }

    // 2. 校验器创建
    protected virtual EntityValidationHandler<TEntity> CreateValidator()
        => new EntityValidationHandler<TEntity>();

    // 3. 配置生成
    public PageUIConfig GetPageConfig() => EntityConfigGenerator.Generate<TEntity>();
}

// 扩展：CertTreeService - 树形结构Service
public class CertTreeService<T> : CertServiceBase<T, IRepository<T>>
    where T : CertEntityBase, ITreeNode
{
    // 树形特有方法
    public async Task<List<T>> GetChildrenAsync(string parentCode) { ... }
    public async Task<bool> HasChildrenAsync(string code) { ... }
    public async Task<int> GetDepthAsync(string code) { ... }
}

// 扩展：CertWorkflowService - 工作流Service
public class CertWorkflowService : CertServiceBase<WorkflowDefinition, ...>
{
    // 工作流特有方法
    public async Task<WorkflowRunResult> ExecuteAsync(string workflowCode, WorkflowContext context) { ... }
    public async Task<List<ExecutionHistory>> GetHistoryAsync(string workflowCode) { ... }
}
```

#### 3.1.3 Controller基类设计

```csharp
// 核心：YzhControllerBase - 统一REST API
public abstract class YzhControllerBase<TEntity, TService> 
    : ControllerBase
    where TEntity : CertEntityBase
    where TService : CertServiceBase<TEntity, ?>
{
    protected readonly TService _service;

    // 通用CRUD
    [HttpPost("getPageData")]
    public async Task<WebResponseContent> GetPageData([FromBody] PageRequest request) { ... }

    [HttpPost("add")]
    public async Task<WebResponseContent> Add([FromBody] SaveModel saveData) { ... }

    [HttpPost("update")]
    public async Task<WebResponseContent> Update([FromBody] SaveModel saveData) { ... }

    [HttpPost("delete")]
    public async Task<WebResponseContent> Delete([FromBody] object[] keys) { ... }

    // 配置获取
    [HttpGet("getConfig")]
    public PageUIConfig GetConfig() => _service.GetPageConfig();
}

// 扩展：YzhTreeControllerBase - 树形Controller
public abstract class YzhTreeControllerBase<TEntity, TService> 
    : YzhControllerBase<TEntity, TService>
    where TEntity : CertEntityBase, ITreeNode
    where TService : CertTreeService<TEntity>
{
    [HttpGet("getTree")]
    public async Task<List<TreeNode>> GetTree([FromQuery] string? parentCode = null) { ... }

    [HttpPost("moveNode")]
    public async Task<WebResponseContent> MoveNode([FromBody] MoveNodeRequest request) { ... }
}

// 扩展：YzhWorkflowControllerBase - 工作流Controller
public abstract class YzhWorkflowControllerBase : ControllerBase
{
    protected readonly CertWorkflowService _workflowService;

    [HttpPost("execute")]
    public async Task<WebResponseContent> Execute([FromBody] ExecuteWorkflowRequest request) { ... }

    [HttpGet("getHistory")]
    public async Task<List<ExecutionHistory>> GetHistory(string workflowCode) { ... }
}
```

### 3.2 前端基类体系

#### 3.2.1 Logic基类继承图

```typescript
// 前端Logic基类继承体系
CrudPageLogic<T>           // 单表CRUD基类
    ├── SysUserPageLogic   // 用户管理
    ├── ISOStandardPageLogic // 标准管理
    └── ...

TreeTableLogic<T>          // 左树右表基类
    ├── SysDeptPageLogic   // 部门管理
    ├── ISOClausePageLogic // 条款管理
    └── ...

// 业务特定基类（未来扩展）
CertAuditPageLogic         // 审核业务基类
    └── AuditorWorkspaceLogic // 审核员工作台

CertRulePageLogic          // 规则管理基类
    └── NCConfigPageLogic   // NC配置管理
```

#### 3.2.2 Logic基类设计

```typescript
// 核心：CrudPageLogic - 单表CRUD
export class CrudPageLogic<T extends Record<string, any>> {
  // 配置
  readonly controllerName: string
  
  // 状态
  config = ref<GridConfig | null>(null)
  rows = ref<T[]>([])
  loading = ref(false)
  pagination = reactive({ page: 1, pageSize: 20, total: 0 })
  
  // 核心方法
  async init() { await this.loadConfig(); await this.loadData(); }
  async loadData() { ... }
  async add(entity: Partial<T>) { ... }
  async update(entity: Partial<T>) { ... }
  async delete(codes: string[]) { ... }
  
  // 钩子（子类可覆盖）
  protected onPrepareAdd(entity: Partial<T>) {}
  protected onBeforeAdd(entity: Partial<T>) {}
  protected onAfterAdd(entity: Partial<T>) {}
}

// 扩展：TreeTableLogic - 左树右表
export abstract class TreeTableLogic<V extends Record<string, any>> {
  // 配置
  abstract controllerName: string
  abstract treeConfig: TreeConfig
  
  // 树状态
  treeData = ref<TreeNode[]>([])
  selectedNode = ref<TreeNode | null>(null)
  
  // 表格状态
  rows = ref<V[]>([])
  loading = ref(false)
  
  // 核心方法
  async init(autoLoadTable = false) { ... }
  async onNodeClick(node: TreeNode) { ... }
  async addTreeNode(parent: TreeNode, entity: any) { ... }
  async deleteTreeNode(node: TreeNode) { ... }
  
  // 抽象方法（子类必须实现）
  abstract loadTree(parentCode?: string): Promise<any[]>
  abstract entityToTreeNode(entity: any, level: number): TreeNode
}
```

#### 3.2.3 原子组件设计

```typescript
// 核心原子组件
YzhTable              // 统一表格组件
YzhForm              // 统一表单组件
YzhSearch            // 统一搜索组件
YzhTree              // 统一树组件
YzhDialog            // 统一对话框
YzhUpload            // 统一上传组件

// 业务原子组件
CertDirectoryTree     // 标准目录树
CertStatusBadge       // 认证状态徽章
CertAuditTimeline     // 审核时间线

// 组合组件（未来）
CertPageLayout        // 认证业务页面布局
AuditWorkspaceLayout  // 审核工作台布局
```

---

## 四、配置驱动设计

### 4.1 GridConfig 配置体系

#### 4.1.1 配置结构

```json
{
  "controllerName": "ISOStandard",
  "primaryKey": "code",
  "table": {
    "columns": [
      {
        "fieldName": "StandardCode",
        "desName": "标准编号",
        "width": 150,
        "xsFlag": true,
        "bcFlag": true,
        "sortable": true,
        "dictCode": null,
        "type": "TextBox"
      },
      {
        "fieldName": "StandardName",
        "desName": "标准名称",
        "width": 200,
        "xsFlag": true,
        "bcFlag": true,
        "sortable": true,
        "type": "TextBox"
      },
      {
        "fieldName": "VersionYear",
        "desName": "版本年份",
        "width": 100,
        "xsFlag": true,
        "bcFlag": true,
        "type": "NumberBox"
      },
      {
        "fieldName": "Category",
        "desName": "类别",
        "width": 120,
        "xsFlag": true,
        "bcFlag": true,
        "dictCode": "iso_category",
        "type": "ComboBox"
      },
      {
        "fieldName": "Enable",
        "desName": "启用状态",
        "width": 80,
        "xsFlag": true,
        "bcFlag": false,
        "type": "Switch"
      }
    ],
    "layoutColumns": 2,
    "toolbar": {
      "add": true,
      "delete": true,
      "export": true,
      "refresh": true
    }
  },
  "search": {
    "fields": [
      { "fieldName": "StandardCode", "type": "TextBox" },
      { "fieldName": "StandardName", "type": "TextBox" },
      { "fieldName": "Category", "type": "ComboBox", "dictCode": "iso_category" }
    ]
  },
  "form": {
    "fields": [
      { "fieldName": "StandardCode", "type": "TextBox", "required": true },
      { "fieldName": "StandardName", "type": "TextBox", "required": true },
      { "fieldName": "VersionYear", "type": "NumberBox" },
      { "fieldName": "Category", "type": "ComboBox", "dictCode": "iso_category" }
    ]
  }
}
```

#### 4.1.2 配置生成器

```csharp
// 自动从实体生成GridConfig
public class EntityConfigGenerator
{
    public static PageUIConfig Generate<TEntity>()
    {
        var entityType = typeof(TEntity);
        var config = new PageUIConfig();
        
        // 1. 扫描实体属性
        var properties = entityType.GetProperties()
            .Where(p => p.HasCustomAttribute<ColumnAttribute>())
            .ToList();
        
        // 2. 生成表格列配置
        config.Columns = properties.Select(p => new DefineColumn
        {
            FieldName = p.Name,
            DesName = p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName 
                      ?? p.Name,
            Type = MapControlType(p),
            XsFlag = IsSearchable(p),
            BcFlag = IsBindable(p),
            Sortable = p.HasCustomAttribute<SortableAttribute>(),
            DictCode = GetDictCode(p)
        }).ToList();
        
        // 3. 生成表单配置
        config.FormFields = properties
            .Where(p => !IsPrimaryKey(p) && !IsAuditField(p))
            .Select(p => new FormFieldConfig
            {
                FieldName = p.Name,
                Type = MapControlType(p),
                Required = p.HasCustomAttribute<RequiredAttribute>(),
                MaxLength = p.GetCustomAttribute<StringLengthAttribute>()?.Length
            }).ToList();
        
        return config;
    }
    
    // 控制类型映射
    private static string MapControlType(PropertyInfo prop)
    {
        return prop.PropertyType.Name switch
        {
            "String" => "TextBox",
            "Int32" or "Int64" => "NumberBox",
            "DateTime" => "DatePicker",
            "Boolean" => "Switch",
            _ => "TextBox"
        };
    }
}
```

### 4.2 配置复用机制

```
配置复用场景：
├── 场景1：相同布局不同实体
│   └── 解决方案：配置继承 + 字段覆盖
│
├── 场景2：相似功能不同页面
│   └── 解决方案：共享Logic基类 + 配置差异
│
└── 场景3：复杂业务页面
    └── 解决方案：组合原子组件 + 自定义Logic
```

#### 4.2.1 配置继承示例

```json
// base-entity-config.json（基础配置）
{
  "table": {
    "columns": [
      { "fieldName": "Code", "desName": "编码", "width": 150 },
      { "fieldName": "Name", "desName": "名称", "width": 200 },
      { "fieldName": "Enable", "desName": "启用", "width": 80, "type": "Switch" },
      { "fieldName": "Remark", "desName": "备注", "width": 200 }
    ],
    "toolbar": { "add": true, "delete": true, "export": true }
  }
}

// iso-standard-config.json（继承基础配置）
{
  "extends": "base-entity-config",
  "controllerName": "ISOStandard",
  "table": {
    "columns": [
      { "fieldName": "StandardCode", "desName": "标准编号", "width": 150 },
      { "fieldName": "StandardName", "desName": "标准名称", "width": 200 },
      { "fieldName": "VersionYear", "desName": "版本年份", "width": 100 },
      { "fieldName": "Category", "desName": "类别", "width": 120, "dictCode": "iso_category" }
    ]
  }
}
```

---

## 五、领域实体设计

### 5.1 实体继承体系

```csharp
// 基础实体
public abstract class BaseEntity
{
    public long Id { get; set; }
    public string Code { get; set; }
    public string OrgCode { get; set; }
    public int? CreateID { get; set; }
    public string Creator { get; set; }
    public DateTime? CreateDate { get; set; }
    public int? ModifyID { get; set; }
    public string Modifier { get; set; }
    public DateTime? ModifyDate { get; set; }
    public int? DeleteID { get; set; }
    public string Deleter { get; set; }
    public DateTime? DeleteTime { get; set; }
    public bool Enable { get; set; } = true;
    public string Remark { get; set; }
}

// 认证业务实体基类
public abstract class CertEntityBase : BaseEntity
{
    // 认证业务特有字段
    public string Status { get; set; } = "active";
    public int Sort { get; set; }
}

// 树形结构实体接口
public interface ITreeNode
{
    string ParentCode { get; set; }
    int Depth { get; set; }
    string FullPath { get; set; }
}

// 可审核实体接口
public interface IAuditable
{
    long? AuditorId { get; set; }
    DateTime? AuditDate { get; set; }
    int? AuditStatus { get; set; }
}

// 可工作流实体接口
public interface IWorkflowable
{
    string WorkflowCode { get; set; }
    string WorkflowInstanceId { get; set; }
}
```

### 5.2 实体实现示例

```csharp
// ISO标准实体
[Table("cert_iso_standard")]
public class ISOStandard : CertEntityBase, ITreeNode
{
    [Required, StringLength(50)]
    [UniqueField("标准编号")]
    [Column("standard_code")]
    public string StandardCode { get; set; }

    [Required, StringLength(200)]
    [Column("standard_name")]
    public string StandardName { get; set; }

    [Column("version_year")]
    public int VersionYear { get; set; }

    [StringLength(50)]
    [Column("category")]
    public string Category { get; set; } = "quality";

    // ITreeNode 实现
    [StringLength(36)]
    [Column("parent_code")]
    public string ParentCode { get; set; }

    [Column("depth")]
    public int Depth { get; set; } = 1;

    [StringLength(1024)]
    [Column("full_path")]
    public string FullPath { get; set; }

    // 业务方法
    public void UpdateName(string newName)
    {
        StandardName = newName;
    }

    public bool IsDuplicate(ISOStandard other)
    {
        return StandardCode == other.StandardCode;
    }
}

// NC规则实体
[Table("cert_validation_rule")]
public class ValidationRule : CertEntityBase, IWorkflowable
{
    [Required, StringLength(50)]
    [UniqueField("规则编码")]
    [Column("rule_code")]
    public string RuleCode { get; set; }

    [Required, StringLength(200)]
    [Column("rule_name")]
    public string RuleName { get; set; }

    [Required, StringLength(36)]
    [Column("standard_code")]
    public string StandardCode { get; set; }

    [Required, StringLength(36)]
    [Column("clause_code")]
    public string ClauseCode { get; set; }

    [Required, StringLength(20)]
    [Column("severity_if_violated")]
    public string SeverityIfViolated { get; set; }

    [Column("rule_json")]
    public string RuleJson { get; set; }

    // IWorkflowable 实现
    [Required, StringLength(36)]
    [Column("workflow_code")]
    public string WorkflowCode { get; set; }

    [StringLength(36)]
    [Column("workflow_instance_id")]
    public string WorkflowInstanceId { get; set; }

    // 业务方法
    public void UpdateRule(string newRuleJson)
    {
        RuleJson = newRuleJson;
    }

    public bool HasCondition(string condition)
    {
        return RuleJson.Contains(condition);
    }
}
```

---

## 六、业务基类设计模式

### 6.1 基类提取原则

```
何时提取基类？
├── ✅ 2个以上业务使用相同布局/流程
├── ✅ 代码重复率>30%
├── ✅ 业务逻辑高度相似
└── ❌ 单一业务场景（不要过度设计）

基类设计原则：
├── 职责单一：每个基类只解决一类问题
├── 可覆盖：子类可覆盖关键方法
├── 可配置：通过配置调整行为
└── 可测试：基类逻辑可独立测试
```

### 6.2 常见基类模板

#### 6.2.1 实体管理基类

```csharp
// 通用实体管理Service
public class CertEntityService<T> : CertServiceBase<T, IRepository<T>>
    where T : CertEntityBase
{
    // 通用CRUD已继承，仅需扩展特有方法
    public async Task<T> GetByCodeAsync(string code)
    {
        return await repository.FindByCondition(x => x.Code == code)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CodeExistsAsync(string code, string? excludeCode = null)
    {
        return await repository.ExistsAsync(x => x.Code == code 
            && (excludeCode == null || x.Code != excludeCode));
    }
}

// 通用实体管理Controller
public class CertEntityController<T> : YzhControllerBase<T, CertEntityService<T>>
    where T : CertEntityBase
{
    // 特有接口
    [HttpGet("getByCode")]
    public async Task<WebResponseContent> GetByCode(string code)
    {
        var entity = await _service.GetByCodeAsync(code);
        return entity != null 
            ? new WebResponseContent().OK(entity) 
            : new WebResponseContent().Error("实体不存在");
    }

    [HttpGet("checkCode")]
    public async Task<bool> CheckCode(string code, string? excludeCode = null)
    {
        return await _service.CodeExistsAsync(code, excludeCode);
    }
}
```

#### 6.2.2 树形管理基类

```csharp
// 通用树形Service
public class CertTreeService<T> : CertEntityService<T>
    where T : CertEntityBase, ITreeNode
{
    public async Task<List<T>> GetChildrenAsync(string parentCode)
    {
        return await repository.FindByCondition(x => x.ParentCode == parentCode)
            .OrderBy(x => x.Sort)
            .ToListAsync();
    }

    public async Task<List<T>> GetTreeAsync()
    {
        var roots = await repository.FindByCondition(x => x.ParentCode == null)
            .OrderBy(x => x.Sort)
            .ToListAsync();
        
        return await BuildTreeAsync(roots);
    }

    private async Task<List<T>> BuildTreeAsync(List<T> nodes)
    {
        foreach (var node in nodes)
        {
            node.Children = await GetChildrenAsync(node.Code);
            await BuildTreeAsync(node.Children);
        }
        return nodes;
    }

    public async Task<bool> MoveNodeAsync(string code, string newParentCode)
    {
        var node = await repository.FindAsync(code);
        if (node == null) return false;

        // 防止移动到自己或子节点下
        if (await IsDescendantAsync(newParentCode, code))
            throw new YZHBusinessException("不能移动到子节点下");

        node.ParentCode = newParentCode;
        node.Depth = await GetDepthAsync(newParentCode) + 1;
        node.FullPath = await BuildFullPathAsync(newParentCode, node.Name);
        
        await repository.UpdateAsync(node);
        return true;
    }
}

// 通用树形Controller
public class CertTreeController<T> : CertEntityController<T>
    where T : CertEntityBase, ITreeNode
{
    private readonly CertTreeService<T> _treeService;

    [HttpGet("getTree")]
    public async Task<WebResponseContent> GetTree()
    {
        var tree = await _treeService.GetTreeAsync();
        return new WebResponseContent().OK(tree);
    }

    [HttpPost("moveNode")]
    public async Task<WebResponseContent> MoveNode([FromBody] MoveNodeRequest request)
    {
        await _treeService.MoveNodeAsync(request.Code, request.NewParentCode);
        return new WebResponseContent().OK();
    }
}
```

#### 6.2.3 工作流基类

```csharp
// 工作流执行Service
public class CertWorkflowService : CertServiceBase<WorkflowDefinition, ...>
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ISkillRegistry _skillRegistry;

    public async Task<WorkflowRunResult> ExecuteAsync(
        string workflowCode, 
        WorkflowContext context)
    {
        var workflow = await GetWorkflowAsync(workflowCode);
        if (workflow == null)
            throw new YZHBusinessException($"工作流 {workflowCode} 不存在");

        return await _workflowEngine.RunAsync(workflow.WorkflowConfig, context);
    }

    public async Task<List<ExecutionHistory>> GetHistoryAsync(
        string workflowCode, 
        string businessCode)
    {
        return await _historyRepository
            .FindByCondition(x => x.WorkflowCode == workflowCode 
                && x.BusinessCode == businessCode)
            .OrderByDescending(x => x.StartedAt)
            .Take(50)
            .ToListAsync();
    }
}
```

---

## 七、前端基类设计模式

### 7.1 页面布局基类

```typescript
// 基础页面布局
export class CertPageLayout {
  protected readonly pageId: string
  
  constructor(controllerName: string) {
    this.pageId = `cert_${controllerName.toLowerCase()}`
  }
  
  // 统一页面头部
  get pageHeader() {
    return {
      title: this.getPageTitle(),
      breadcrumb: this.getBreadcrumb(),
      actions: this.getPageActions()
    }
  }
  
  // 统一页面工具栏
  get pageToolbar() {
    return {
      buttons: this.getToolbarButtons(),
      search: this.getSearchConfig()
    }
  }
  
  // 子类可覆盖
  protected getPageTitle(): string { return '' }
  protected getBreadcrumb(): string[] { return [] }
  protected getPageActions(): Array<{ key: string; text: string }> { return [] }
  protected getToolbarButtons(): Array<{ key: string; text: string }> { return [] }
  protected getSearchConfig(): SearchField[] { return [] }
}

// 实体管理页面
export class CertEntityPageLogic<T extends Record<string, any>> 
  extends CrudPageLogic<T> 
  implements CertPageLayout {
  
  // 实现CertPageLayout接口
  getPageTitle() { return `${this.controllerName}管理` }
  
  getBreadcrumb() { 
    return ['系统管理', this.controllerName] 
  }
  
  getPageActions() {
    return [
      { key: 'refresh', text: '刷新' },
      { key: 'export', text: '导出' }
    ]
  }
}

// 树形管理页面
export class CertTreePageLogic<T extends Record<string, any>> 
  extends TreeTableLogic<T>
  implements CertPageLayout {
  
  getPageTitle() { return `${this.controllerName}管理` }
  
  getBreadcrumb() { 
    return ['系统管理', '基础配置', this.controllerName] 
  }
  
  getToolbarButtons() {
    return [
      { key: 'add', text: '新增节点', type: 'primary' },
      { key: 'expand', text: '展开/折叠' },
      { key: 'refresh', text: '刷新' }
    ]
  }
}
```

### 7.2 业务页面基类

```typescript
// 审核员工作台页面
export class AuditorWorkspaceLogic extends CrudPageLogic<AuditTask> {
  controllerName = 'AuditTask'
  
  // 审核员特有逻辑
  override onPrepareAdd(entity: Partial<AuditTask>) {
    entity.plannedDate = new Date()
    entity.auditorId = UserContext.Current?.UserId
  }
  
  // 审核员特有操作
  async onStartAudit(taskCode: string) {
    await this.apiPost('/action/start', { taskCode })
    await this.loadData()
  }
  
  async onCompleteAudit(taskCode: string, result: AuditResult) {
    await this.apiPost('/action/complete', { taskCode, result })
    await this.loadData()
  }
}

// NC规则配置页面
export class NCConfigPageLogic extends TreeTableLogic<ValidationRule> {
  controllerName = 'ValidationRule'
  
  treeConfig = {
    codeField: 'ruleCode',
    nameField: 'ruleName',
    parentCodeField: 'clauseCode',
    lazy: true,
    relateField: 'standardCode'
  }
  
  // NC规则特有逻辑
  async executeRuleCheck(ruleCode: string, documentContent: string) {
    const result = await this.apiPost('/action/execute', { ruleCode, documentContent })
    return result
  }
}
```

---

## 八、扩展机制设计

### 8.1 扩展点设计

```csharp
// 扩展点接口
public interface IEntityValidator<T>
{
    Task<EntityValidationResult> ValidateAsync(T entity, ValidationAction action);
}

public interface IEntityPostProcessor<T>
{
    Task ProcessAfterAddAsync(T entity);
    Task ProcessAfterUpdateAsync(T entity);
    Task ProcessAfterDeleteAsync(T entity);
}

public interface IWorkflowEventListener
{
    Task OnNodeStartedAsync(string workflowCode, string nodeId);
    Task OnNodeCompletedAsync(string workflowCode, string nodeId, SkillResult result);
    Task OnWorkflowCompletedAsync(string workflowCode, WorkflowRunResult result);
}
```

### 8.2 扩展实现示例

```csharp
// ISO标准验证器
public class ISOStandardValidator : IEntityValidator<ISOStandard>
{
    public async Task<EntityValidationResult> ValidateAsync(
        ISOStandard entity, ValidationAction action)
    {
        if (action == ValidationAction.Add)
        {
            // 检查标准编号是否已存在
            var exists = await _db.ISOStandards
                .AnyAsync(x => x.StandardCode == entity.StandardCode);
            
            if (exists)
                return EntityValidationResult.Error("标准编号已存在");
            
            // 检查版本年份合理性
            if (entity.VersionYear < 2000 || entity.VersionYear > DateTime.Now.Year + 1)
                return EntityValidationResult.Error("版本年份不合理");
        }
        
        return EntityValidationResult.OK();
    }
}

// ISO标准后置处理器
public class ISOStandardPostProcessor : IEntityPostProcessor<ISOStandard>
{
    public async Task ProcessAfterAddAsync(ISOStandard entity)
    {
        // 发布领域事件
        await _eventPublisher.PublishAsync(new ISOStandardCreated(entity.Id));
        
        // 缓存刷新
        await _cacheManager.RemoveAsync($"iso_standard_{entity.Code}");
    }
}
```

---

## 九、架构演进路线

### 9.1 当前阶段（V1.0）

```
已完成：
├── ✅ 四层架构分层（Stand→DataBase→Api→Web）
├── ✅ 配置驱动UI（GridConfig）
├── ✅ 基类封装（CrudPageLogic/TreeTableLogic）
├── ✅ 实体继承体系（EntityBase→CertEntityBase）
└── ✅ 工作流引擎 + Skill体系

待完善：
├── ⏳ 业务基类提取（CertEntityService/CertTreeService）
├── ⏳ 前端布局基类（CertPageLayout）
└── ⏳ 扩展点机制（IEntityValidator/IEntityPostProcessor）
```

### 9.2 下一阶段（V2.0）

```
目标：
├── 形成完整的基类体系
├── 建立业务页面模板库
├── 实现扩展点机制
└── 完善文档和示例

具体行动：
├── 从现有业务中提取公共基类
├── 建立2-3个业务页面模板
├── 实现扩展点接口和注册机制
└── 编写架构设计文档
```

### 9.3 未来方向（V3.0）

```
可能演进：
├── 引入垂直切片（按功能组织代码）
├── 引入CQRS（读写分离）
├── 引入DDD领域建模
└── 模块化单体（按业务域拆分）

前提条件：
├── 团队规模扩大（>3人）
├── 业务复杂度提升
└── 性能瓶颈出现
```

---

## 十、总结

### 10.1 架构特色

| 特色 | 说明 |
|------|------|
| **配置驱动** | GridConfig控制UI，减少代码重复 |
| **基类封装** | 80%场景通过基类解决 |
| **适度抽象** | 2个以上业务才提取基类 |
| **简单可控** | 不追求过度设计，保持代码清晰 |
| **渐进演进** | 从简单开始，按需复杂度递增 |

### 10.2 与主流架构对比

| 维度 | 主流架构 | YZH架构 |
|------|---------|---------|
| **组织方式** | 按技术层（Controller/Service/Repository） | 按业务场景（Logic基类） |
| **代码量** | 每页面100-200行重复代码 | 每页面10-20行业务代码 |
| **学习成本** | 高（需理解多层架构） | 低（继承基类即可） |
| **灵活性** | 高（完全自定义） | 中高（基类+配置） |
| **适用场景** | 大型团队、复杂业务 | 单人/小团队、快速迭代 |

### 10.3 最终建议

```
✅ 保持当前架构主体（配置驱动+基类封装）
✅ 逐步提取业务基类（CertEntityService/CertTreeService）
✅ 建立页面模板库（ISOStandardPage/ValidationRulePage等）
⏸️ 延后DDD和CQRS（待业务复杂度提升）
⏸️ 不要引入过多框架（保持简单可控）
```

**你的架构哲学是正确的：简单代码结构，实现复杂业务功能。保持这一理念，逐步形成独树一帜的YZH架构风格。**

---

**文档结束**
