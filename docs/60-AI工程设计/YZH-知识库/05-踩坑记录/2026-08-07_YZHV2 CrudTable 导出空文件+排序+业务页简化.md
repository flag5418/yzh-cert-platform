# 2026-08-07 YZH V2 CrudTable 三项修复 + 业务页面简化

> **背景**：`CertificationBody.vue`（认证机构管理）作为 YZH V2 架构的首个落地页面，在真实使用中暴露了导出、排序、代码冗余三类问题。本次修复同时推动了「通用逻辑下沉基类」的架构改进。

> **官方文档参考**：
> - searchBefore 查询条件配置：http://v3.volcore.xyz/docs/view-grid/methods/searchBefore.html
> - view-grid 参数属性（boxOptions/sortable 等）：http://v3.volcore.xyz/docs/view-grid/properties.html
> - onInit/onInited 生命周期：http://v3.volcore.xyz/docs/view-grid/methods/onInit.html
> - 后台 Service 导出/导入业务扩展：http://v3.volcore.xyz/docs/cs/service/guid.html

---

## 📝 问题索引

| 编号 | 模块 | 问题现象 | 优先级 |
|------|------|----------|--------|
| P3-01 | 导出 | 点击导出 → 调用接口成功 → 下载的 xlsx 文件**打开为空**（0 行数据）| P0 |
| P3-02 | 排序 | 列设置面板中，只有 CbCode/Name/CreateDate 三个字段有排序按钮，其他字段不可排序 | P1 |
| P3-03 | 代码冗余 | 业务页面 `CertificationBody.vue` 包含空壳 lifecycles、无用的按钮、无用的 onReady 回调 | P2 |

---

## 1️⃣ P3-01 导出 Excel 文件为空

### 问题现象
1. 用户点击「导出」按钮
2. 浏览器下载了 `CertCertificationBody_2026-08-07.xlsx`
3. 用 Excel 打开 → **完全空白**（0 行 0 列或只有表头无数据）

### 根因分析

**前端传参缺失 `columns` 字段**。

> **官方文档对照**：[searchBefore 文档](http://v3.volcore.xyz/docs/view-grid/methods/searchBefore.html) 中 `param` 包含查询条件、分页、排序，导出时还需传 `columns` 字段告诉后端导出哪些列。`downloadFileName` 属性可自定义导出文件名（见 [properties.html](http://v3.volcore.xyz/docs/view-grid/properties.html)）。

后端 `ServiceBase.ExportBytes()` 的核心逻辑（`VOL.Core/BaseProvider/ServiceBase.cs` L578-L591）：

```csharp
private WebResponseContent ExportBytes(PageDataOptions pageData, List<TEntity> list, List<string> ignoreColumn)
{
    var exportFields = ExportColumns?.GetExpressionToArray() ?? [];
    // 只有当 pageData.Columns 有值时才用它确定导出列
    if ((exportFields?.Length ?? 0) == 0 && (pageData.Columns?.Length ?? 0) > 0)
    {
        exportFields = pageData.Columns;  // ← 关键！
    }
    byte[] bytes = EPPlusHelper.ExportBytes(list, exportFields, ignoreColumn, ExcelHeaderMap);
    return baseWebResponse.OK(null, bytes);
}
```

当 `exportFields` 为空数组时：
- EPPlus 不知道要导出哪些列
- 生成的 Excel 要么完全空白，要么只有审计字段

**前端原来发送的参数**（修复前）：
```js
const param = { filter: buildFilter(), sort: currentSortProp.value, order: currentSortOrder.value }
// ❌ 缺少 columns 字段！
```

**Vol 原生 ViewGridEventButton.jsx 是怎么做的**（L440）：
```jsx
param.columns = _columns.filter(x => { return x })
// ✅ 把当前可见列的 field 名传给后端
```

### 解决方案

在 `YzhCrudTable.vue` 的 `handleExport()` 中补全 `columns` 参数：

```js
const visibleCols = actualVisibleColumns.value
const param: any = {
  filter: buildFilter(),
  sort: currentSortProp.value || defaultSortField.value,
  order: currentSortOrder.value || defaultSortOrder.value || 'desc',
  // ✅ 关键：把可见列的 field 名传给后端
  columns: visibleCols.map((c: any) => c.field).filter(Boolean),
}
```

### 预防措施
- 所有调用后端 `/Export` 接口的前端代码，必须传递 `columns` 字段
- 基类 `handleExport` 已统一处理，新业务页面不需要再单独处理

---

## 2️⃣ P3-02 列设置中不是所有字段都支持排序

### 问题现象
1. 点「列设置」按钮 → 弹出列筛选面板
2. 发现只有 **CbCode、Name、CreateDate** 三个字段旁边有 ↑↓ 排序按钮
3. ShortName、ContactName、ContactPhone 等字段没有排序按钮

### 根因分析

两处限制叠加：

> **官方文档对照**：[properties.html](http://v3.volcore.xyz/docs/view-grid/properties.html) 中 `sortable` 属性是 2024.10.06 新增的「表格拖拽排序」功能，默认 `false`。Element Plus `el-table-column` 的 `sortable` 属性默认也为 `false`，需显式设为 `'custom'` 才启用服务端排序。

**① 列设置面板**（模板 L102）：
```vue
<el-button v-if="col.sortable" ...>  <!-- ← 只有 col.sortable=true 才显示 -->
```

**② 表格列头**（模板 L149）：
```vue
:sortable="col.sortable ? 'custom' : false"  <!-- ← 同样依赖 col.sortable -->
```

而 `options.js` 中只给 3 个字段配了 `sortable: true`，其余未配置。

### 解决方案

**设计决策反转**：默认所有字段可排序，显式 `sortable: false` 才禁用。

```vue
<!-- 列设置面板：移除 v-if 条件 -->
<el-button size="small" link type="primary" ...>

<!-- 表格列头：改为 opt-out 模式 -->
:sortable="col.sortable !== false ? 'custom' : false"
```

### 预防措施
- 新增字段时无需额外配置 `sortable: true`
- 确实不需要排序的字段（如操作列、备注大文本）设 `sortable: false`

---

## 3️⃣ P3-03 业务页面代码简化 + 通用逻辑下沉

### 问题现象

`CertificationBody.vue` 作为 YZH V2 的首个落地页面，包含大量冗余代码：

| 代码 | 行数 | 是否有用 |
|------|------|---------|
| `#toolbarLeft` 查看关联标准按钮 | 10 行 | ❌ 标准不在此页面显示 |
| `lifecycles.onLoadAfter` 空壳 | 4 行 | ❌ 只 return rows，基类已有默认 |
| `lifecycles.onDeleteBefore` 有注释但无实际阻断 | 6 行 | ⚠️ 可作为基类默认 |
| `lifecycles.onAddSaveBefore` ContactName 兜底 | 2 行 | ⚠️ 可下沉到基类 |
| `handleOpenStandards()` | 12 行 | ❌ 随按钮一起删 |
| `onReady()` 空壳 | 3 行 | ❌ 无任何逻辑 |
| `useRouter` import | 1 行 | ❌ 删按钮后不需要 |

**修改前**：111 行 → **修改后**：68 行（减少 **38%**）

### 解决方案

#### 3.1 移除无用代码
- 删除「查看关联标准」按钮及对应的 `handleOpenStandards()` 方法
- 删除空的 `onReady()` 回调
- 删除 `onLoadAfter` 和 `onDeleteBefore` 空壳钩子
- 删除不再需要的 `useRouter` import

#### 3.2 通用逻辑下沉到基类

新增 `applyStringFieldDefaults()` 函数到 `YzhCrudTable.vue`：

```ts
/**
 * 基类默认行为：将表单中 null/undefined 的字符串字段填充为空字符串
 * 
 * 为什么需要：
 * - 前端 v-model 绑定的 input 在用户未输入时值为空字符串 ''
 * - 但某些场景下可能为 null/undefined
 * - 后端 EF Core / MySQL 对字符串字段写入 null 可能导致意外行为
 * - 此函数确保所有字符串字段至少为 ''，业务钩子 onAddSaveBefore 可覆盖
 */
function applyStringFieldDefaults(formData: any) {
  if (!formData || typeof formData !== 'object') return
  const stringFields = new Set<string>()
  editFormOptions.value.forEach((row: any[]) => {
    ;(row || []).forEach((item: any) => {
      if (item.field && ['input', 'textarea', 'text'].includes(item.type)) {
        stringFields.add(item.field)
      }
    })
  })
  stringFields.forEach((field) => {
    if (formData[field] === null || formData[field] === undefined) {
      formData[field] = ''
    }
  })
}
```

在 `handleSave()` 的新增分支中，`onAddSaveBefore` 之前自动调用：

```ts
if (isAdd) {
  // 基类默认行为：将 null/undefined 的字符串字段填充为空字符串
  applyStringFieldDefaults(editForm)

  const ok = await runGuard(lc.onAddSaveBefore, [editForm])
  // ...
}
```

#### 3.3 最终的业务页面代码

```vue
<template>
  <YzhCrudTable ref="crudTable" :schema="schema" :options="viewOptions"
    :lifecycles="lifecycles" :incremental-update="true" :search-mode="'fixed'">
    <template #gridHeader>
      <el-alert title="认证机构管理：..." type="info" :closable="false" show-icon />
    </template>
  </YzhCrudTable>
</template>

<script setup lang="ts">
import { ref, markRaw } from 'vue'
import { YzhCrudTable } from '@/yzh/index'
import viewOptions from './options.js'

const crudTable = ref()

const schema = Object.freeze({
  keyField: 'Code',
  keyType: 'string',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName: 'CertCertificationBody',
  tableName: 'cert_certification_body',
  statusTagColors: { Status: 'org_status' },
})

// 只写有业务逻辑的钩子
const lifecycles = markRaw({
  onAddSaveBefore(main: any) {
    if (!main.CbCode) {
      main.CbCode = `CB${Date.now().toString().slice(-3)}`
    }
    return true
  },
})
</script>
```

### 设计原则总结

| 原则 | 说明 | 官方文档参考 |
|------|------|-------------|
| **差异编程** | 业务页面只写与基类不同的代码，不重复基类已有的默认行为 | Vol 框架设计哲学：生成代码 + Partial 扩展 |
| **空壳禁令** | lifecycles 中没有实际逻辑的钩子一律不写（基类已提供空实现） | [guid.html](http://v3.volcore.xyz/docs/cs/service/guid.html) 生成代码已实现默认 CRUD |
| **通用下沉** | 多个业务页面可能需要的逻辑（如字符串默认值填充）写在基类，而非每个页面复制 | - |
| **Opt-Out 默认值** | 排序等 UI 特性默认开启，通过显式 `false` 禁用，减少配置量 | [properties.html](http://v3.volcore.xyz/docs/view-grid/properties.html) |

---

## 🔧 修改文件清单

| 文件 | 修改类型 | 说明 |
|------|----------|------|
| `vol.web/src/yzh/components/YzhCrudTable.vue` | 修改 | ① handleExport 补充 columns 参数；② 排序改为 opt-out；③ 新增 applyStringFieldDefaults |
| `views/cert/CertificationBody/CertificationBody.vue` | 重写 | 从 111 行精简到 68 行，移除所有空壳和无用代码 |

---

## 🎯 后续推广 Checklist

推广到 ISOStandard / ISOClause / Enterprise / CertApplication / AuditTask 等 5 个待迁移页面时：

- [ ] 确认导出功能是否正常（检查是否走了基类的 handleExport）
- [ ] 确认列设置中所有字段都有排序按钮
- [ ] 清理各页面中的空壳 lifecycles（onLoadAfter 只 return rows 的删掉）
- [ ] 清理各页面中的空壳 onReady 回调
- [ ] 检查是否有类似 ContactName='' 的通用兜底逻辑可以删除（已被基类 applyStringFieldDefaults 覆盖）
