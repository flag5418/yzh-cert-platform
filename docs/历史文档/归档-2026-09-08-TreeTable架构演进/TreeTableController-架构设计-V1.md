# TreeTableController 架构设计 V1

## 一、定位

左树右表（TreeTable）模式的统一控制器基类，用于组织机构管理、标准目录等场景。

```
┌────────────────────────────────────────────────────────────────┐
│  左侧树                      右侧表格                           │
│  ┌─────────────┐             ┌──────────────────────────────┐ │
│  │ 🔍 搜索...  │             │ [新增] [编辑] [删除] [刷新]   │ │
│  ├─────────────┤             ├──────────────────────────────┤ │
│  │ ▶ 总公司    │ ──────────► │ 编码  │ 名称    │ 状态 │ 操作│ │
│  │   ├ 子公司A │             │ D001  │ 研发部  │ 启用 │ ✎ ✕│ │
│  │   │  ├ 部门1│             │ D002  │ 市场部  │ 启用 │ ✎ ✕│ │
│  │   │  └ 部门2│             │ D003  │ 财务部  │ 禁用 │ ✎ ✕│ │
│  │   └ 子公司B │             ├──────────────────────────────┤ │
│  │ ▶ 分公司C   │             │        分页：1 2 3 ...        │ │
│  └─────────────┘             └──────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

## 二、核心设计

### 2.1 接口协议

| 功能 | 方法 | 路径 | 请求体 | 响应 |
|------|------|------|--------|------|
| 加载根节点 | POST | `/getTreeTableRootData` | `{}` | `{ total, rows: [{ departmentId, departmentName, parentId, hasChildren }] }` |
| 懒加载子节点 | POST | `/getTreeTableChildrenData` | `{ departmentId }` | `{ rows: [{ departmentId, departmentName, parentId, hasChildren }] }` |
| 表格分页 | POST | `/getPageData` | `{ page, rows, parentId, ...filters }` | `{ total, rows: [...] }` |
| 新增 | POST | `/save` | `{ entity }` | `{ status, data }` |
| 修改 | POST | `/save` | `{ entity }` | `{ status, data }` |
| 删除 | POST | `/delete` | `{ keys: [id1, id2] }` | `{ status }` |

### 2.2 局部刷新策略

| 操作 | 树刷新 | 表格刷新 |
|------|--------|----------|
| 新增子节点 | ❌ 不刷新 | ✅ 刷新当前表格 |
| 编辑节点 | ❌ 不刷新 | ✅ 刷新当前表格 |
| 删除节点 | ❌ 不刷新 | ✅ 刷新当前表格 |
| 展开节点 | ✅ 加载子节点 | ❌ 不刷新 |
| 点击节点 | ❌ 不刷新 | ✅ 加载表格数据 |

### 2.3 前后端协议

```typescript
// ===== 后端返回格式（Vol JsonNormal） =====

// 树节点
interface TreeNodeDTO {
  departmentId: string
  departmentName: string
  departmentCode?: string
  parentId?: string | null
  enable?: number
  hasChildren: boolean
}

// 表格行
interface TableRowDTO {
  departmentId: string
  departmentName: string
  departmentCode?: string
  parentId?: string
  departmentType?: string
  enable?: number
  remark?: string
  creator?: string
  createDate?: string
  modifier?: string
  modifyDate?: string
}

// ===== 前端请求格式 =====

// 树请求
interface TreeRootRequest { /* 空或过滤条件 */
}
interface TreeChildrenRequest {
  departmentId: string
}

// 表格请求
interface TablePageRequest {
  page: number
  rows: number
  parentId?: string  // 树节点联动
  searchKey?: string // 搜索关键字
}
```

## 三、后端实现

### 3.1 TreeTableController 基类

位置：`src/server/Vue.NetCore/vol.api/YZH.WebApi/Controllers/TreeTableControllerBase.cs`

```csharp
/// <summary>
/// 左树右表控制器基类
/// 适用于组织机构、标准目录等场景
/// </summary>
public abstract class TreeTableControllerBase<T> : ApiBaseController<IService<T>>
    where T : ITreeTableEntity
{
    // 加载根节点
    [HttpPost, Route("getTreeTableRootData")]
    public abstract Task<ActionResult> GetTreeTableRootData(PageDataOptions options);
    
    // 懒加载子节点
    [HttpPost, Route("getTreeTableChildrenData")]
    public abstract Task<ActionResult> GetTreeTableChildrenData(Guid departmentId);
    
    // 表格分页（带 parentId 条件）
    [HttpPost, Route("getPageData")]
    public abstract ActionResult GetPageData(PageDataOptions options);
}
```

### 3.2 实体约定

```csharp
public interface ITreeTableEntity
{
    Guid Id { get; set; }           // 主键
    string Name { get; set; }       // 显示名称
    string Code { get; set; }       // 编码
    Guid? ParentId { get; set; }    // 父节点 ID
    int? Enable { get; set; }       // 是否启用
    string Remark { get; set; }     // 备注
    string Creator { get; set; }    // 创建人
    DateTime? CreateDate { get; set; } // 创建时间
    string Modifier { get; set; }   // 修改人
    DateTime? ModifyDate { get; set; } // 修改时间
}
```

## 四、前端实现

### 4.1 前端 Logic 设计

```typescript
// 基类：TreeTableLogic<T>
abstract class TreeTableLogic<T> {
  // ===== 树状态 =====
  treeData: Ref<TreeNode[]>
  treeLoading: Ref<boolean>
  selectedNode: Ref<TreeNode | null>
  
  // ===== 表格状态 =====
  tableData: Ref<T[]>
  tableLoading: Ref<boolean>
  pagination: { page: number; pageSize: number; total: number }
  
  // ===== 抽象方法 =====
  abstract loadRootNodes(): Promise<TreeNodeDTO[]>
  abstract loadChildren(parentId: string): Promise<TreeNodeDTO[]>
  abstract loadTable(params: TablePageRequest): Promise<{ rows: T[], total: number }>
  
  // ===== 通用方法 =====
  async initTree(): Promise<void>              // 加载根节点
  async onNodeClick(node: TreeNode): Promise<void> // 点击树节点 → 加载表格
  async loadChildrenLazy(node: TreeNode): Promise<TreeNode[]> // 懒加载
}
```

### 4.2 局部刷新实现

```typescript
// 新增成功后
async handleAdd(data: Partial<T>): Promise<void> {
  await api.addDept(data)
  // 只刷新表格
  await this.loadTable({
    page: this.pagination.page,
    rows: this.pagination.pageSize,
    parentId: this.selectedNode.value?.code
  })
}

// 编辑成功后
async handleEdit(data: Partial<T>): Promise<void> {
  await api.updateDept(data)
  // 只刷新表格
  await this.loadTable({ ... })
}

// 删除成功后
async handleDelete(id: string): Promise<void> {
  await api.deleteDept([id])
  // 只刷新表格
  await this.loadTable({ ... })
}
```

## 五、文件结构

```
src/server/Vue.NetCore/vol.api/
├── YZH.WebApi/Controllers/
│   ├── TreeTableControllerBase.cs     # 树表控制器基类
│   └── Admin/Sys/
│       └── Sys_DepartmentController.cs # 继承基类

src/certplatform-web/
├── yzh.vue.core/src/
│   ├── components/
│   │   ├── layout/
│   │   │   ├── YzhTree.vue           # 树组件
│   │   │   └── YzhTreeTable.vue      # 左树右表骨架
│   │   └── table/
│   │       ├── YzhTable.vue          # 表格组件
│   │       └── types.ts              # 类型定义
│   └── logic/
│       └── TreeTableLogic.ts         # Logic 基类
├── share/src/
│   ├── api/system-dept.ts            # 部门 API
│   └── types/tree.ts                 # 树类型定义
└── admin/src/pages/system/dept/
    ├── index.vue                     # 部门管理页面
    ├── logic.ts                      # DeptTreeTableLogic
    └── DeptFormDialog.vue            # 表单弹窗
```

## 六、实施计划

### Phase 1: 后端（已完成 ✓）
- [x] Sys_DepartmentController 已有 getTreeTableRootData
- [x] Sys_DepartmentController 已有 getTreeTableChildrenData
- [x] Sys_DepartmentController 已有 getPageData（带分页）
- [x] Sys_DepartmentController 已有 save/delete

### Phase 2: 前端组件（已完成 ✓）
- [x] YzhTree 组件
- [x] YzhTreeTable 骨架
- [x] YzhTable 组件
- [x] YzhDialog 组件

### Phase 3: 部门管理实现（进行中 🔄）
- [ ] 完善 DeptTreeTableLogic
- [ ] 实现局部刷新逻辑
- [ ] 完善表单弹窗
- [ ] 联调测试

