# YZH 组件库 - 认证平台前端基类

## 目录结构

```
components/yzh/
├── README.md              # 本文档
├── YZHBaseCrud.vue        # 基础 CRUD 窗体（★ 核心）
├── YZHBaseCrud.jsx        # 扩展兼容文件
└── index.js               # 统一导出
```

## YZHBaseCrud.vue - 基础 CRUD 窗体

### 设计目标

1. **统一行为**：所有认证平台 CRUD 页面的统一基础组件
2. **增量更新**：新增/编辑/删除后不刷新整个列表，手动操作 grid 数据
3. **行内操作**：编辑/删除按钮在操作列，不在顶部工具栏
4. **标准化布局**：统一 2 列表单布局，隐藏字段用 `type: 'hidden'`
5. **生命周期**：完整的业务钩子函数控制

### 使用示例

```vue
<template>
  <YZHBaseCrud
    :options="viewOptions"
    module-name="ISOStandard"
    description="ISO 标准管理：管理各认证机构可开展认证的ISO标准"
    @on-init="handleInit"
    @add-after="handleAddAfter"
  />
</template>

<script setup lang="jsx">
import YZHBaseCrud from "@/components/yzh/YZHBaseCrud.vue";
import viewOptions from "./options.js";

const handleInit = ($vm) => {
  // 可以在此修改 grid 属性
  // $vm.setFixedSearchForm(true);
};

const handleAddAfter = (result, formData) => {
  console.log("新增成功:", result);
  return true;
};
</script>
```

### Props

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| options | Object | **必须** | options.js 导出的配置对象 |
| moduleName | String | "未命名模块" | 模块名称 |
| description | String | "" | 页面顶部描述文字 |
| enableRowActions | Boolean | true | 是否启用行内操作列 |
| rowActionWidth | Number | 150 | 行内操作列宽度 |
| hideTopEditDelButtons | Boolean | true | 隐藏顶部编辑/删除按钮 |

### Events

| 事件名 | 参数 | 说明 |
|--------|------|------|
| on-init | $vm | ViewGrid 初始化完成 |
| on-inited | - | 初始化后（可操作明细表） |
| add-before | formData | 新增保存前 |
| add-after | result, formData | 新增保存后 |
| update-before | formData | 编辑保存前 |
| update-after | result, formData | 编辑保存后 |
| del-before | delKeys, rows | 删除前 |
| del-after | result, rows | 删除后 |
| row-click | { row, column, event } | 行点击 |

### Expose 方法

| 方法 | 说明 |
|------|------|
| getGrid() | 获取 ViewGrid 引用 |
| refresh() | 手动刷新列表 |
| getSelectedRows() | 获取选中行 |
| getTableData() | 获取表格所有数据 |
| addRow(row) | 增量添加行 |
| updateRow(row) | 增量更新当前行 |
| removeRows(rows) | 增量移除行 |

## options.js 配置规范

### 表单字段规范

```javascript
// ✅ 正确：隐藏字段用 type: 'hidden'
const editFormFields = {
  id: '',           // 主键，不需要显示
  code: '',         // 业务编码，不需要用户看到
  name: '',         // 正常字段
};

const editFormOptions = [
  [
    { field: 'id', type: 'hidden' },      // 隐藏主键
    { field: 'code', type: 'hidden' },    // 隐藏编码
    { title: '名称', field: 'name', required: true, colSize: 12 },
  ],
  [
    { title: '状态', field: 'status', type: 'select', dataKey: 'status_list', colSize: 6 },
    { title: '备注', field: 'notes', type: 'textarea', colSize: 12 },
  ],
];
```

### 列配置规范

```javascript
// ✅ 正确：不需要额外配置操作列，YZHBaseCrud 自动添加
const columns = [
  { field: 'id', title: 'ID', hidden: true },
  { field: 'name', title: '名称', width: 200, sortable: true },
  { field: 'status', title: '状态', width: 100, bind: { key: 'status_list', value: 'status' } },
  // 操作列由 YZHBaseCrud 自动添加
];
```

### 布局规范

- **统一使用 2 列布局**：`colSize: 6` 表示占一半宽度
- **整行字段**：`colSize: 12` 占满一行
- **隐藏字段**：必须设置 `type: 'hidden'`
- **禁止在表单中显示 code/id 等系统字段**

## 业务基类分类

根据业务场景，YZHBaseCrud 可扩展为以下专用基类：

| 基类名 | 适用场景 | 特点 |
|--------|----------|------|
| YZHBaseCrud | 通用 CRUD | 标准增删改查 |
| YZHAuditCrud | 审核任务 | 含审核流程、状态流转 |
| YZHConfigCrud | 基础资料 | 简单配置、启用禁用 |
| YZHMasterDetailCrud | 主从表 | 含明细表操作 |

## 与 Vol 框架的关系

```
Vol view-grid (底层)
    ↓ 封装
YZHBaseCrud (业务基类)
    ↓ 继承
具体页面 (ISOStandard, CertificationBody, ...)
```

## 更新日志

### v1.0.0 (2026-07-31)
- 初始版本
- 支持增量更新（新增/编辑/删除不刷新整个列表）
- 支持行内操作列
- 支持完整生命周期钩子
