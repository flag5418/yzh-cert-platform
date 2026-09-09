# 2026-09-07 TreeTable 试点页面与 Vol 适配

## 背景
在 SysDept 试点页面开发中，发现自研 `TreeTableLogic` 基类与 Vol 框架的 TreeTable API 存在不匹配，需要适配。

## 核心问题

### 1. 接口风格不匹配

**TreeTableLogic 假设的接口**：
```
POST /api/{controller}/page     - 分页查询
POST /api/{controller}/add      - 新增
POST /api/{controller}/update   - 修改
POST /api/{controller}/delete   - 删除
POST /api/{controller}/getChildren - 懒加载子节点
```

**Vol 实际提供的接口**：
```
POST /api/Sys_Department/getPageData            - 分页查询
POST /api/Sys_Department/getTreeTableRootData   - 加载根节点
POST /api/Sys_Department/getTreeTableChildrenData - 懒加载子节点（参数：departmentId）
POST /api/Sys_Department/save                   - 新增/修改
POST /api/Sys_Department/delete                 - 删除
```

### 2. 字段命名不匹配

**TreeTableLogic 假设**：
- code / parentCode（string）

**Vol 实际**：
- DepartmentId / ParentId（Guid）

### 3. 解决方案

**方案 A**：修改 TreeTableLogic 基类适配 Vol 风格（❌ 破坏通用性）
**方案 B**：创建 VolTreeTable 适配器基类（✅ 推荐）
**方案 C**：在 DeptLogic 中覆盖所有不匹配的方法（✅ 实际采用）

最终采用方案 C：在 `DeptTreeLogic` 中覆盖 `initTree`、`loadChildren`、`loadDataWithTreeCondition` 方法，适配 Vol 的 API 风格。

## 关键适配点

### 1. 树加载

```typescript
// 覆盖 initTree 使用 Vol 的 getTreeTableRootData
async initTree(): Promise<void> {
  this.treeLoading.value = true
  try {
    const rootItems = await getDeptRootNodes()
    this.treeData.value = rootItems.map(item => {
      const node = this.entityToTreeNode(item, 0)
      node.parentCode = null
      return node
    })
  } finally {
    this.treeLoading.value = false
  }
}
```

### 2. 懒加载

```typescript
// 覆盖 loadChildren 使用 Vol 的 getTreeTableChildrenData
async loadChildren(parentNode: TreeNode): Promise<TreeNode<SysDept>[]> {
  const items = await getDeptChildren(parentNode.code)
  return items.map(item => {
    const node = this.entityToTreeNode(item, (parentNode.extra?.level ?? 0) + 1)
    node.parentCode = parentNode.code
    return node
  })
}
```

### 3. 字段映射

```typescript
// 后端 DTO → TreeNode
entityToTreeNode(dto: DeptTreeNode, level: number): TreeNode<SysDept> {
  return {
    code: dto.departmentId,        // Guid → string code
    name: dto.departmentName,
    parentCode: dto.parentId ?? null,
    isLeaf: !dto.hasChildren,      // hasChildren → !isLeaf
    extra: { icon: '📁', level },
    children: [],
    raw: dto as any
  }
}
```

## TypeScript 踩坑

### 1. Map 解构错误
```typescript
// ❌ 错误
for (const [node] of newMap) {  // node 是 string（key）
  oldMap.get(node.code)         // Error: Property 'code' does not exist on type 'string'
}

// ✅ 正确
for (const [, newNode] of newMap) {  // newNode 是 { node, level }
  oldMap.get(newNode.node.code)
}
```

### 2. el-tree 类型不兼容
```typescript
// ❌ TreeNode 与 el-tree 的 Node 类型不兼容
:props="{ isLeaf: (data: TreeNode) => data.isLeaf }"

// ✅ 使用 Record<string, any> 转型
:props="{ isLeaf: (data: Record<string, any>) => (data as TreeNode).isLeaf }"
```

### 3. setCheckedNodes 参数类型
```typescript
// ❌ 参数类型不匹配
treeRef.value?.setCheckedNodes(nodes)

// ✅ 使用 any 转型
;(treeRef.value as any)?.setCheckedNodes(nodes)
```

### 4. element-plus 图标导入
```typescript
// ❌ Document/Folder 不存在
import { Document, Folder } from 'element-plus'

// ✅ 使用文本图标或 SVG
<span>📁</span>
```

## 文件清单

```
src/certplatform-web/
├── admin/src/pages/system/dept/
│   ├── index.vue              # 试点页面（左树右表布局）
│   ├── logic.ts               # DeptTreeLogic 类
│   └── DeptFormDialog.vue     # 新增部门弹窗
├── share/src/
│   ├── api/system-dept.ts     # 扩展 Vol TreeTable API
│   ├── utils/treeOps.ts       # 修复 diff 函数
│   └── utils/treeUtils.ts     # 修复 getAncestors
└── yzh.vue.core/
    ├── tsconfig.json          # 添加 @share 路径映射
    ├── vite.config.ts         # 添加 @share 别名
    └── src/components/layout/
        ├── YzhTree.vue        # 修复类型错误
        └── YzhTreeTable.vue   # 无需修改
```

## 后续优化建议

1. **抽取 VolTreeTableAdapter**：如果后续有多个页面使用 Vol TreeTable，可抽取公共适配器
2. **统一图标处理**：使用 SVG 图标或图标组件替代 emoji
3. **编辑功能完善**：当前编辑功能待实现
4. **批量操作**：勾选后批量删除、移动功能
