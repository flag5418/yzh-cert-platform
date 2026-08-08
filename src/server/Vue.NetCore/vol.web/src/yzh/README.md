# YZH Framework V2.0 —— 纯净 Element Plus 前端框架

> **从 Vol 框架解耦，基于 Element Plus 原生组件重写**
> **设计理念：输入是高频操作，v-model 即时响应；增删改局部刷新；手动查询模式**

---

## 一、目录总览

```
src/yzh/
├── index.ts                          统一出口
├── README.md                         本文档
│
├── types/                            类型定义（纯 TS）
│   ├── index.ts
│   ├── YZHEntitySchema.ts            实体 Schema + Action 常量
│   ├── YZHLifecycles.ts              13+ 生命周期 Hook 类型
│   └── YZHPageProps.ts               Props 类型（单表 / 左树右表）
│
├── core/                             核心逻辑（纯 TS，无 Vue 依赖，可单测）
│   ├── YZHBaseApiClient.ts           泛型 HTTP 客户端（自动拼 URL）
│   ├── YZHEditGuard.ts               保存/删除前置校验 + 二次确认
│   ├── YZHRowDiff.ts                 行级增量更新算法（插入/替换/删除）
│   └── YZHPageLifecycle.ts           生命周期接口 + 默认空钩子 + runGuard
│
├── components/                       Vue 组件（基于 Element Plus）
│   └── YzhCrudTable.vue              ★★★ 核心单表 CRUD 基类
│
├── composables/                      Vue 3 Composables
│   ├── useYZHEditMode.ts             编辑模式 + 多选状态机
│   └── useYZHIncrementSync.ts        CRUD 增量同步 orchestrator
│
└── presets/                          预设配置
    └── defaultButtons.ts             工具栏按钮预设
```

---

## 二、快速上手

### 2.1 业务页面模板

```vue
<template>
  <YzhCrudTable
    ref="crudTable"
    :schema="schema"
    :options="viewOptions"
    :lifecycles="lifecycles"
    :incremental-update="true"
    @ready="onReady"
  >
    <template #toolbarLeft="{ selectedRow }">
      <!-- 业务按钮 -->
    </template>
    <template #gridHeader>
      <!-- 头部提示 -->
    </template>
  </YzhCrudTable>
</template>

<script setup lang="ts">
import { ref, markRaw } from 'vue'
import { YzhCrudTable } from '@/yzh/index'
import viewOptions from './options.js'

const crudTable = ref()

const schema = Object.freeze({
  keyField: 'Id',
  keyType: 'number',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName: 'CertCertificationBody', // 后端 Controller 名
})

const lifecycles = markRaw({
  onLoadAfter(rows) { return rows },
  onAddSaveBefore(main) { return true },
})
</script>
```

### 2.2 options.js 格式（兼容 Vol 生成器输出）

```javascript
export default function () {
  const table = {
    name: 'CertificationBody',
    cnName: '认证机构管理',
    url: '/CertCertificationBody/',
    sortName: 'CreateDate',
    key: 'Id',
  }

  const editFormFields = { Name: '', Status: 'active', /* ... */ }
  const editFormOptions = [
    [
      { field: 'Name', title: '机构全称', type: 'input', required: true, colSize: 12 },
      { field: 'Status', title: '状态', type: 'select', dataKey: 'org_status', data: [], colSize: 12 },
    ],
    // ...
  ]

  const searchFormFields = { Name: '', Status: '' }
  const searchFormOptions = [
    [
      { field: 'Name', title: '关键词', type: 'input', placeholder: '名称/简称' },
      { field: 'Status', title: '状态', type: 'select', dataKey: 'org_status', data: [] },
    ],
  ]

  const columns = [
    { field: 'Id', title: 'ID', hidden: true },
    { field: 'Name', title: '机构全称', width: 250, sortable: true },
    { field: 'Status', title: '状态', width: 100, bind: { key: 'org_status' } },
    // ...
  ]

  return { table, key: table.key, tableName: table.name, /* ... */ editFormFields, editFormOptions, searchFormFields, searchFormOptions, columns }
}
```

---

## 三、核心设计决策

| 决策 | 说明 |
|------|------|
| **手动搜索** | 用户填写条件 → 点「查询」按钮 → 执行搜索。不自动触发 |
| **v-model 双向绑定** | 搜索区、弹窗表单全部使用 `v-model`，输入即时回显 |
| **增量刷新** | 新增→按排序插入；修改→同主键替换；删除→本页移除。不做全表 reload |
| **Element Plus 原生** | el-table + el-form + el-dialog + el-pagination。零 Vol 依赖 |
| **字典自动加载** | 弹窗打开时自动加载 select 的 dataKey 字典数据 |
| **后端兼容** | API 接口格式完全对齐 Vol ApiBaseController（GetPageData/Add/Update/Del） |

---

## 四、生命周期钩子

| 阶段 | 方法 | 可阻断？ |
|------|------|---------|
| 查询前 | `onLoadBefore(param)` | ✅ |
| 查询后 | `onLoadAfter(rows, raw)` | ❌ |
| 新增前 | `onAddBefore(formData)` | ✅ |
| 新增保存前 | `onAddSaveBefore(main)` | ✅ |
| 新增保存后 | `onAddSaveAfter(main, result)` | ❌ |
| 编辑前 | `onUpdateBefore(row, formData)` | ✅ |
| 编辑保存前 | `onUpdateSaveBefore(main)` | ✅ |
| 编辑保存后 | `onUpdateSaveAfter(main, result)` | ❌ |
| 删除前 | `onDeleteBefore(rows, ids)` | ✅ |
| 删除后 | `onDeleteAfter(ids)` | ❌ |

---

## 五、与 V1.0 (Vol 适配层) 的区别

| 维度 | V1.0 (components/yzh/base/) | V2.0 (src/yzh/) |
|------|---------------------------|-------------------|
| 核心 ViewGrid | ✅ 依赖 Vol 2000+ 行黑盒 | ❌ 纯 Element Plus |
| 输入框 v-model | ⚠️ 通过 props 传递对象引用 | ✅ 直接 reactive binding |
| 搜索行为 | 自动 blur/change 触发 | ✅ 手动点查询按钮 |
| 刷新机制 | Vol 内部 refresh | ✅ 清空条件 + reload |
| 代码量 | 962 行 workaround | ~700 行纯净代码 |
| 调试难度 | 高（Vol 内部状态机不透明） | 低（完全可控） |

---

## 六、迁移指南（V1 → V2）

1. 改 import：`@/components/yzh/base/YzhBaseSingleTable` → `@/yzh/index` 的 `YzhCrudTable`
2. options.js **不需要改**（完全兼容）
3. schema/lifecycles 接口不变
4. 插槽名不变：`#toolbarLeft` / `#gridHeader` / `#btnLeft`

---

## 七、后续规划

- [ ] V2.1: 左树右表基类 (`YzhTreeTable`)
- [ ] V2.2: 主从表基类 (`YzhMasterDetail`)
- [ ] V2.3: 工作流基类 (`YzhWorkflow`)
- [ ] V2.4: 列设置弹窗（拖拽排序列显隐）
- [ ] V2.5: 导入导出增强（模板下载、批量导入进度）
