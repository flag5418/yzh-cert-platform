# YZH 前端框架 V1.0 —— MVP 落地说明

> **目录所有者**：所有文件 **只增不删原 Vol 代码**；绝不修改 `components/basic/` 下的 Vol 核心源码。
> **首个落地页**：`views/cert/CertificationBody/CertificationBody.vue`（认证机构管理，单表）

---

## 一、目录总览（本框架新增的文件）

```
vol.web/src/
├── types/yzh/
│   ├── index.ts                       导出入口
│   ├── YZHEntitySchema.ts             实体元信息 Schema + Action 常量
│   ├── YZHLifecycles.ts               13+ 生命周期 Hook 类型
│   └── YZHPageProps.ts                4 类基类 Props 类型（单表/左树右表/主从/工作流）
│
└── components/yzh/
    ├── README.md                      ← 本文档
    ├── YZHBaseApiClient.ts            ★ TS 泛型 API 客户端（按 controllerName 自动拼路由）
    ├── YZHPageLifecycle.ts            生命周期接口 + 默认空钩子 + runGuard
    ├── YZHRowDiff.ts                  ★ 行级增量 3 算法（新增插 / 修改换 / 删除移）
    ├── YZHEditGuard.ts                保存/删除前置二次确认 + 必填快速校验
    │
    ├── base/                          ★ 基类 Vue 组件目录（用户 §4 调整）
    │   ├── YzhBaseSingleTable.vue     ★★★ 单表基类窗体（MVP 首发，命名规范：YzhBaseXxx）
    │   ├── YzhBaseTreeTable.vue       ⏳ 左树右表（M3 待写）
    │   ├── YzhBaseMasterDetail.vue    ⏳ 主从表（M3 待写）
    │   └── YzhBaseWorkflow.vue        ⏳ 工作流页（M3 待写）
    │
    ├── composables/
    │   ├── useYZHEditMode.ts          编辑模式 + 多选 + 批量删 状态机
    │   └── useYZHIncrementSync.ts     ★ CRUD 增量同步：不 search() reload，直接 patch 内存 rows
    │
    └── presets/
        ├── defaultButtons.ts          顶部 8 按钮预设（新增/刷新/导入/导出/列/编辑/删除/排序）
        └── defaultActionColumn.ts     ★ 行级「修改/删除」操作列（formatter 是函数，规避 P2-05）

旧的反例组件（Phase 2 联调期间废弃）保留在本目录根：YZHBaseCrud.{jsx,vue}，不再引用。
```

---

## 二、快速上手（3 行代码用起）

一个业务页 = 1 个 Vue 文件 + 1 份 options.js（options.js 继续用生成器标准输出，**无需重写**）。

```vue
<template>
  <YzhBaseSingleTable :schema="schema" :options="viewOptions" :lifecycles="lifecycles">
    <template #toolbarLeft="{ selectedRow }">
      <div>
        <el-button type="success" size="small" :disabled="!selectedRow" @click="xxx">
          业务按钮
        </el-button>
      </div>
    </template>
  </YzhBaseSingleTable>
</template>

<script setup lang="jsx">
import YzhBaseSingleTable from '@/components/yzh/base/YzhBaseSingleTable.vue'
import viewOptions from './options.js'

const schema = Object.freeze({
  keyField:         'Id',
  keyType:          'number',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName:   'CertCertificationBody', // 后端类名去 Controller 后缀
})

const lifecycles = {
  onAddSaveBefore(main) { /* 补默认值，return false 阻断 */ return true },
  onDeleteBefore(rows, ids) { /* 二次确认，return false 阻断 */ return true },
}
</script>
```

---

## 三、本框架解决的用户 5 条核心需求

| # | 需求 | 实现位置 | 说明 |
|---|------|----------|------|
| 1 | **顶部取消标题，改查询条 + 操作按钮** | `base/YzhBaseSingleTable.vue` 合并配置 | 取消原 Vol `desc-text`（`table.cnName` 置空）；`searchMode="fixed"` 默认展开查询；工具条 8 按钮在 `btnLeft` 槽渲染 |
| 2 | **✎ 编辑按钮 = 切换多选框显示（删除常显，用户 §1 调整）** | `useYZHEditMode.ts` + `presets/defaultButtons.ts` | 点击 ✎编辑 → 仅切换多选列显示/隐藏；**顶部删除按钮常显**；不选行时提示「请先选择一行（点击行选中或进入编辑多选）」 |
| 3 | **每行独立「修改/删除」按钮** | `presets/defaultActionColumn.ts` → 在 columns 末尾 push 1 列 | 不依赖 Vol 原生顶部按钮；点击后直接调 `handleRowEdit(row)` / `handleRowDelete(row)`，无需进编辑模式 |
| 4 | **新增/修改/删除 → 不 reload，局部 patch（删除逻辑简化，用户 §2 调整）** | `YZHRowDiff.ts` 3 算法 + `useYZHIncrementSync.ts` + `base/YzhBaseSingleTable.vue` saveAfter/deleteAfter | ① 新增 → `insertByOrder` 按当前排序找正确位置 splice；② 修改 → `replaceByKey` 同主键换对象；③ 删除 → `removeByKeys` 把选中行从本页列表移除，**不做跳页补拉**（删空就空着，用户点「刷新」补数据，KISS） |
| 5 | **TS 泛型 + 后端 Controller 自动拼路由** | `types/yzh/YZHEntitySchema.ts` + `YZHBaseApiClient.ts` | `schema.controllerName = 'CertCertificationBody'` → 自动拼 `POST /api/CertCertificationBody/GetPageData`（末尾 `/`，规避 P2-06 404） |

---

## 四、生命周期（对齐后端 Partial Service）

在 `lifecycles` 里按需实现，未实现的走默认空钩子（不影响业务）。

| **阶段** | 方法 | 对应后端 Hook | 返回 false 可阻断？ |
|----------|------|---------------|--------------------|
| 查询前 | `onLoadBefore(param)` | ServiceBase.QueryRelativeList | ✅ |
| 查询后 | `onLoadAfter(rows, rawResponse)` | GetPageDataOnExecuted | ❌（可加工返回 rows 供 tag 色转换） |
| 新增弹框前 | `onAddBefore(formData)` | AddOnExecute | ✅ |
| 新增保存前 | `onAddSaveBefore(main, list)` | AddOnExecuting | ✅ |
| 新增保存后 | `onAddSaveAfter(main, list, result)` | AddOnExecuted | ❌ |
| 编辑弹框前 | `onUpdateBefore(row, formData)` | UpdateOnExecute | ✅ |
| 编辑保存前 | `onUpdateSaveBefore(main, list)` | UpdateOnExecuting | ✅ |
| 编辑保存后 | `onUpdateSaveAfter(main, list, result)` | UpdateOnExecuted | ❌ |
| 删除前 | `onDeleteBefore(rows, ids)` | DelOnExecuting | ✅（还会先弹系统二次确认） |
| 删除后 | `onDeleteAfter(ids)` | DelOnExecuted | ❌ |
| UX | `onRowSelect(row, selectedRows)` / `onRowClick(evt)` / `onEditModeChange(flag)` | — | ❌ |

---

## 五、从旧 Vol 原生页迁移到 YZH 单表的「2 步走」模板

1. **改 Vue `<script setup>` 顶部 3 行**
   - 删掉 `import extend from "@/extension/cert/xxx.jsx"`（仍可在 options.extend 引用，基类已透传）
   - 删掉 8 个 `onInit / onInited / searchBefore ...` 的空实现函数
   - 改为 `YzhBaseSingleTable import + schema + lifecycles` 三对象
2. **改 `<template>` 外层标签**
   - 外层包 `<YzhBaseSingleTable ...>` 替换 `<view-grid>`
   - 原业务按钮从 `<template #btnLeft>` 搬到 `<template #toolbarLeft>`（YZH 基类把 #btnLeft 留给默认 8 按钮，业务按钮放工具栏槽，视觉上下分层）
   - `<div>` 单根包裹（P2-04）：YZH 基类内部已包外层 div，**业务插槽仍需业务方自己保证单根**。

---

## 六、已落地样板页

✅ **`views/cert/CertificationBody/CertificationBody.vue`**（认证机构，单表）
- 旧版：156 行，手写 9 个空 Hook + 原生 view-grid
- 新版：110 行（实际业务代码约 30 行 + 注释模板），声明式 Schema + Lifecycles

**未落地（后续 M3 再推广，等你确认 M2 正常后再动）**：
- ISOStandard / Enterprise / CertApplication / AuditProject / AuditTask / ISOClause（ISOClause 是左树右表，等 YZHTreeTable 写完后再迁）

---

## 七、外部参数注入（左树右表场景预告）

后续 YZHTreeTable 直接用下面这两个现成的扩展点，不需要重写 getPageData：

1. **`externalFilter` prop**：传 `[{ name: 'CbCode', value: 'xxx' }]`，基类自动合并到 wheres，变化时自动 reload
2. **`defineExpose().setExternalFilter(wheres, incremental)`**：父组件可主动调用，立即触发查询
3. **schema.controllerName**：左树、右表可各自指定独立 Controller，互不干扰

---

## 八、回退方案（万一增量刷新有 Bug）

业务页只需改一行，立即切回 Vol 原生 `search()` 全量刷新：

```diff
-  <YZHSingleTable
+  <YzhBaseSingleTable
    :schema="schema"
    :options="viewOptions"
-   :incremental-update="true"
+   :incremental-update="false"
   />
```

关掉后，所有 patch 操作都走 Vol 原生 saveAfter → search()。这是本框架内置的「安全开关」。

---

## 九、与 `docs/60-AI工程设计/vol-skill.md` 的衔接

后续建议补充 1 节（等 M3 知识库写好后再补进去）：

```
§12.F  YZH 基类优先原则（新增）
  · 新的单表 / 左树右表页优先使用 YzhBaseSingleTable / YzhBaseTreeTable（路径 components/yzh/base/），不重复原生 view-grid + 8 按钮
  · 使用时必须传 schema，controllerName 必须与后端类名严格一致（去 Controller 后缀）
  · 插槽必须用 <div> 单根包裹（P2-04 硬约束）
  · formatter 必须是函数（P2-05 硬约束）→ preset defaultActionColumn 已规避
  · URL 末尾斜杠由基类保证（P2-06 硬约束），业务不传 table.url
  · 顶部删除常显；点击「✎编辑」只切换多选框；删除只把行从本页 DOM 移除，不跳页补拉
```
