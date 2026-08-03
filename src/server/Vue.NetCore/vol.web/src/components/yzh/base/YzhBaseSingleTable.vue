<template>
  <div class="yzh-single-table">
    <!-- FIX-2：真正 DOM 占位（ViewGrid 没匿名 slot，但此处放在外层保证 customSearchRef.value 有 DOM）
         initExtraHeight 会从 gridVM.$refs.customSearchRef 读取，我们在 3 个生命周期手动挂 -->
    <div
      ref="customSearchRef"
      aria-hidden="true"
      style="
        height: 0;
        width: 0;
        border: 0;
        padding: 0;
        margin: 0;
        visibility: hidden;
        pointer-events: none;
        overflow: hidden;
        position: absolute;
      "
    ></div>

    <!-- 业务工具栏：业务按钮放 toolbarLeft/toolbarRight，视觉上在 YZH 8 按钮上方一行 -->
    <div v-if="$slots.toolbarLeft || $slots.toolbarRight" class="yzh-toolbar-extra">
      <div class="yzh-toolbar-extra__left">
        <slot
          name="toolbarLeft"
          :selectedRow="selectedRow"
          :selectedRows="selectedRows"
          :editMode="editMode"
        />
      </div>
      <div class="yzh-toolbar-extra__right">
        <slot
          name="toolbarRight"
          :selectedRow="selectedRow"
          :selectedRows="selectedRows"
          :editMode="editMode"
        />
      </div>
    </div>

    <view-grid
      ref="gridRef"
      :key="schema?.controllerName || schema?.tableName || 'yzh-grid'"
      v-bind="volViewGridProps"
      :table="mergedTable"
      :columns="mergedColumns"
      :boxOptions="opts.boxOptions"
      :detail="opts.detail"
      :details="opts.details"
      :editFormFields="opts.editFormFields"
      :editFormOptions="opts.editFormOptions"
      :searchFormFields="opts.searchFormFields"
      :searchFormOptions="opts.searchFormOptions"
      :extend="mergedExtend"
      :text="undefined"
      :priview="undefined"
      :coderTableId="undefined"
      :loadTreeChildren="opts.loadTreeChildren"
      :searchMode="searchMode"
      :onInit="onInit"
      :onInited="onInited"
      :loadBeforeAsync="loadBeforeAsync"
      :onLoadBefore="onLoadBefore"
      :onLoadAfter="onLoadAfter"
      :searchBefore="searchBefore"
      :searchAfter="searchAfter"
      :addBefore="addBefore"
      :updateBefore="updateBefore"
      :saveBefore="saveBefore"
      :saveAfter="saveAfter"
      :modelOpenBefore="modelOpenBefore"
      :modelOpenAfter="modelOpenAfter"
      :deleteBefore="deleteBefore"
      :deleteAfter="deleteAfter"
      :rowClick="rowClick"
      :selectionChange="onSelectionChange"
    >
      <!-- #btnLeft 槽：YZH 统一 8 按钮工具栏 + 业务插槽（完全替换原生按钮组） -->
      <template #btnLeft>
        <div class="yzh-btn-bar">
          <el-button
            v-for="btn in yzhButtons"
            :key="btn.key"
            :type="btn.type || 'default'"
            :size="btn.size || 'small'"
            :class="btn.key === 'editMode' && editMode ? 'is-active' : ''"
            @click="onYzhToolbarClick(btn)"
          >
            <i :class="btn.icon" style="margin-right: 4px" />
            {{ btn.label }}
          </el-button>
          <slot name="btnLeft" :selectedRow="selectedRow" :editMode="editMode" />
        </div>
      </template>

      <template #gridHeader><slot name="gridHeader" /></template>
      <template #gridBody><slot name="gridBody" /></template>
      <template #gridFooter><slot name="gridFooter" /></template>
      <template #btnRight><slot name="btnRight" /></template>
    </view-grid>
  </div>
</template>

<script setup lang="jsx">
import ViewGrid from '@/components/basic/ViewGrid/ViewGrid.vue'
import {
  computed,
  defineExpose,
  defineProps,
  getCurrentInstance,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  useAttrs,
  watch
} from 'vue'
import { useYZHEditMode } from '../composables/useYZHEditMode'
import { useYZHIncrementSync } from '../composables/useYZHIncrementSync'
import { buildActionColumn } from '../presets/defaultActionColumn'
import { mergeDefaultButtons } from '../presets/defaultButtons'
import { YZHBaseApiClient } from '../YZHBaseApiClient'
import { YZHEditGuard } from '../YZHEditGuard'
import { createDefaultLifecycles, runGuard } from '../YZHPageLifecycle'

/* ————————————————————————————— Props ————————————————————————————— */
const props = defineProps({
  schema: { type: Object, required: true },
  options: { type: Function, required: true },
  lifecycles: { type: Object, default: () => ({}) },
  incrementalUpdate: { type: Boolean, default: true },
  buttons: { type: Object, default: () => ({}) },
  searchMode: { type: String, default: 'fixed' },
  externalFilter: { type: Array, default: null },
  showActionColumn: { type: Boolean, default: true }
})
const attrs = typeof useAttrs === 'function' ? useAttrs() : {}

/* ————————————————————————————— 环境 ————————————————————————————— */
const vm = getCurrentInstance()
const proxy = vm?.proxy
const rawOpts = props.options
  ? typeof props.options === 'function'
    ? props.options()
    : props.options
  : {}

// FIX-Dialog + FIX-NullComponent（关键！）
// 1. 绝对禁止使用 structuredClone / JSON.parse(JSON.stringify(...)) 任何深拷贝
//    → 会剥离 Vue SFC 对象（__cc__/__hmrId/component/setup/render）→ <component :is=null> 解析崩 null.component
// 2. 绝对禁止使用 reactive(...) 包装 editFormFields / editFormOptions / searchFormFields / searchFormOptions
//    → Vol ViewGrid / VolForm 的 setup 会保存这些引用，并在内部（initButtonsAuthFields/VolForm 初始化字典时）
//      执行 `item.data = [] / buttons.splice / x.field = '...'` 等原地修改；如果外层是 readonly reactive Proxy，
//      这些操作会静默失败或抛未捕获异常 → 父组件实例构建不完整，defineAsyncComponent resolve 时 parent 链断
//      → locateNonHydratedAsyncRoot 读 instance.parent.component 时 parent=null → TypeError
// 3. 但也不能直接使用 rawOpts（业务 options() 返回字面量单例，多实例场景 v-model 会互相污染）
//    → 使用「普通 JS 对象浅拷贝（非 reactive）」：对象属性独立、普通 set 可写（v-model 正常输入），Vol 内部赋值也正常
function _shallowCloneValueFieldsOnly(val) {
  if (val == null || typeof val !== 'object') return val
  if (Array.isArray(val)) {
    return val.map((item) => {
      if (item == null || typeof item !== 'object') return item
      if (Array.isArray(item)) return item.slice()
      return Object.assign({}, item)
    })
  }
  return Object.assign({}, val)
}

const _defaults = {
  detail: {},
  details: [],
  editFormFields: {},
  editFormOptions: [],
  searchFormFields: {},
  searchFormOptions: [],
  columns: [],
  table: {},
  // 弹窗默认固定宽度：960px（1440 屏宽下 2/3，不随窗口 70% 缩放导致列跳来跳去）
  boxOptions: { width: 960, top: '8vh', closeOnClickModal: false }
}
const opts = Object.assign({}, _defaults, rawOpts || {}, {
  // ===== 普通 JS 对象（非 reactive），逐字段浅拷贝 =====
  editFormFields: Object.assign({}, rawOpts?.editFormFields || _defaults.editFormFields),
  editFormOptions: (rawOpts?.editFormOptions || _defaults.editFormOptions).map((row) =>
    Array.isArray(row) ? row.map((x) => Object.assign({}, x)) : []
  ),
  searchFormFields: Object.assign({}, rawOpts?.searchFormFields || _defaults.searchFormFields),
  searchFormOptions: (rawOpts?.searchFormOptions || _defaults.searchFormOptions).map((row) =>
    Array.isArray(row) ? row.map((x) => Object.assign({}, x)) : []
  ),
  columns: _shallowCloneValueFieldsOnly(rawOpts?.columns || _defaults.columns),
  table: Object.assign({}, rawOpts?.table || _defaults.table),
  boxOptions: Object.assign({}, _defaults.boxOptions, rawOpts?.boxOptions || {}),
  detail: Object.assign({}, rawOpts?.detail || _defaults.detail),
  // extend.components/methods 含 Component SFC → 直接引用，任何形式克隆都禁止
  extend: rawOpts?.extend ? rawOpts.extend : undefined,
  details: Array.isArray(rawOpts?.details) ? rawOpts.details.slice() : _defaults.details
})
const lc = Object.assign(createDefaultLifecycles(), props.lifecycles || {})
const api = proxy ? new YZHBaseApiClient(props.schema, proxy) : null
const guard = proxy ? new YZHEditGuard(proxy) : null

/* ————————————————————————————— 编辑模式 & 多选 ————————————————————————————— */
const {
  editMode,
  toggleEditMode,
  enterEditMode,
  exitEditMode,
  selectedRows,
  hasSelection,
  setSelectedRows,
  setSingleSelected,
  clearSelected,
  selectedKeys,
  selectedRowObjects,
  registerVolSelectionGetter
} = useYZHEditMode(props.schema, {
  onEditModeChange: (editing) => {
    lc.onEditModeChange?.(editing)
    if (mergedTable.value) mergedTable.value.showCheckbox = true
    if (!editing && gridRef.value) gridRef.value.clearSelection?.()
  },
  onSelectChange: (rows) => {
    if (rows && rows.length) lc.onRowSelect?.(rows[0], rows)
  }
})
// 「选择了一行还提示没选 / 勾选了行不能删」的核心根因：
// Vol 原生 showCheckbox + 勾选框的结果只存在 gridVM.getSelected() / gridVM.getSelectRows()，
// 与我们自己 setSelectedRows / rowClick 维护的 YZH state **是两套独立的数据**，之前没同步。
// 现在在 onInit / onInited / onMounted 每次都注册一次 getter（首次没拿到就返回空数组，composables 会继续用自己的 fallback 单选）
function _syncVolSelectionGetter() {
  try {
    if (typeof registerVolSelectionGetter === 'function') {
      registerVolSelectionGetter(() => {
        const vm =
          gridVM ||
          (gridRef.value && typeof gridRef.value.getSelected === 'function' ? gridRef.value : null)
        if (!vm || typeof vm.getSelected !== 'function') return []
        const r = vm.getSelected()
        return Array.isArray(r) ? r : []
      })
    }
  } catch (_) {}
}
const selectedRow = computed(() => {
  const ob = selectedRowObjects()
  return ob?.length ? ob[0] : null
})

/* ————————————————————————————— 增量同步 ————————————————————————————— */
const pageRowsRef = ref([])
const enabledRef = ref(!!props.incrementalUpdate)
watch(
  () => props.incrementalUpdate,
  (v) => {
    enabledRef.value = !!v
  }
)
const incSync = useYZHIncrementSync({
  enabled: enabledRef,
  schema: props.schema,
  pageRows: pageRowsRef
})

/* ————————————————————————————— 合并 table ————————————————————————————— */
const mergedTable = ref(null)
function buildMergedTable() {
  const t = Object.assign({}, opts.table || {})
  // FIX-4：隐藏 ViewGrid 顶部「认证机构管理」重复标题（<div class="desc-text" v-if="table.cnName">）
  t.cnName = ''
  t.text = ''
  // FIX-1：URL 必须为相对路径（Vol getUrl() = '/' + 'api' + table.url + action）
  //        禁止写 /api/ 前缀，防止出现 /api/api/... 双前缀 404
  if (props.schema?.controllerName) {
    t.url = `/${props.schema.controllerName}/`
  } else if (typeof t.url === 'string') {
    t.url = String(t.url).replace(/^\/?api\//, '/')
    if (!t.url.startsWith('/')) t.url = '/' + t.url
    if (!t.url.endsWith('/')) t.url = t.url + '/'
  }
  t.showCheckbox = true
  t.key = t.key || props.schema?.keyField || 'Id'
  return t
}
const mergedColumns = ref([])
function rebuildMergedColumns() {
  const cols = (opts.columns || []).slice()
  if (props.showActionColumn)
    return buildActionColumn(
      props.schema,
      { onEdit: (row) => handleRowEdit(row), onDelete: (row) => handleRowDelete(row) },
      cols
    )
  return cols
}
mergedTable.value = buildMergedTable()
mergedColumns.value = rebuildMergedColumns()
watch(editMode, () => {
  if (mergedTable.value) mergedTable.value.showCheckbox = true
})

/* ————————————————————————————— 合并 extend（关闭原生自定义扩展按钮，防止菜单重复） ————————————————————————————— */
const mergedExtend = computed(() => {
  // 关键点：**不传任何空对象**。如果业务 opts.extend == null 就直接返回 {}（等价于 props.extend 为空）
  //         让 Vol setup(props.extend.components) 在 L555 `if (props.extend.components)` 时为 falsey，
  //         跳过所有自定义组件注册/遍历逻辑，避免 AsyncComponent resolve 过程中对象结构异常
  if (!opts.extend && !opts.table?.tableAction) return {}

  const ex = Object.assign({}, opts.extend || {})

  // 只有业务显式传了 components/methods/tableAction 才 Object.assign 一份副本，
  // 否则**删除这些字段**（delete），不要保留 {} 空对象
  if (ex.components != null && Object.keys(ex.components).length > 0)
    ex.components = Object.assign({}, ex.components)
  else delete ex.components
  if (ex.methods != null && Object.keys(ex.methods).length > 0)
    ex.methods = Object.assign({}, ex.methods)
  else delete ex.methods

  const mergedTableAction = Object.assign({}, ex.tableAction || {}, opts.table?.tableAction || {})
  if (Object.keys(mergedTableAction).length > 0) ex.tableAction = mergedTableAction
  else delete ex.tableAction

  // 隐藏灰色描述条：直接 delete，不让 Vol 走到渲染逻辑
  delete ex.text

  // FIX-ExtendButtons：业务没传 extend.buttons 就直接 delete，不要塞任何东西；
  //                   传了才 Object.assign，把 view/box/detail 覆盖为 undefined（typeof undefined !== 'function' 会被 Vol 跳过）
  if (ex.buttons != null) {
    ex.buttons = Object.assign({}, ex.buttons)
    ex.buttons.view = undefined
    ex.buttons.box = undefined
    ex.buttons.detail = undefined
  } else {
    delete ex.buttons
  }
  return ex
})

/* ————————————————————————————— 顶部 YZH 按钮 ————————————————————————————— */
const yzhButtons = computed(() => mergeDefaultButtons(props.buttons, editMode.value))

async function onYzhToolbarClick(btn) {
  const key = btn?.key
  const tbl = gridRef.value?.getTable?.()
  switch (key) {
    case 'add':
      return tbl?.add?.() || gridRef.value?.add?.()
    case 'refresh':
      // 刷新：按当前 wheres + searchFormFields 重新从第 1 页查（与 Vol 原生 refresh 等价，避免只清分页不清缓存）
      try {
        gridVM?.refresh?.()
      } catch (_) {}
      try {
        gridRef.value?.refresh?.()
      } catch (_) {}
      try {
        gridRef.value?.search?.(null, true)
      } catch (_) {}
      return
    case 'import':
      return (
        (gridVM && gridVM.$refs?.volUploadRef?.show && gridVM.$refs.volUploadRef.show()) ||
        (gridVM && gridVM.$refs?.volUpload && gridVM.$refs.volUpload?.show?.()) ||
        gridRef.value?.getTable?.()?.importClick?.() ||
        _runVolImportExportRaw('import')
      )
    case 'export':
      return gridRef.value?.getTable?.()?.exportClick?.() || _runVolImportExportRaw('export')
    case 'column':
      return _openVolCustomColumn()
    case 'editMode':
      return toggleEditMode()
    case 'batchDelete': {
      if (!hasSelection.value) {
        proxy?.$message?.warning?.('请先勾选要删除的行（使用左侧复选框或行选择）')
        return
      }
      await handleBatchDelete()
      return
    }
    case 'sort':
      return proxy?.$message?.info?.('点击列标题进行升/降序排序')
  }
}

/* ————————————————————————————— Vol 原生能力兜底 —————————————————————————————
 *   tbl.importClick/exportClick 在某些 Vol 版本里未暴露，
 *   直接调用 ViewGridEventButton.jsx 导出的 importData / exportData 函数作为兜底，
 *   保证导入/导出/列配置 3 个按钮真的能「开弹窗 / 下载 xlsx」，而不是打印占位提示。 */
function _getDataConfigFromVM() {
  try {
    if (gridVM && (gridVM.dataConfig || gridVM._?.dataConfig))
      return gridVM.dataConfig || gridVM._.dataConfig
    if (gridVM?.$) return gridVM.$.data || null
  } catch (_) {}
  return null
}
function _propsFromVM() {
  try {
    return gridVM?.$?.vnode?.props || gridVM?.props || null
  } catch (_) {
    return null
  }
}
async function _runVolImportExportRaw(kind /* 'import' | 'export' */) {
  try {
    const m = await import('@/components/basic/ViewGrid/ViewGridEventButton.jsx')
    const dc = _getDataConfigFromVM()
    const ps = _propsFromVM()
    if (gridVM && dc && ps) {
      const fn = kind === 'import' ? m.importData : m.exportData
      if (typeof fn === 'function') return fn(gridVM, ps, dc, false)
    }
    proxy?.$message?.warning?.('当前 Vol 版本暂未暴露该按钮，请直接使用表格右上角操作')
  } catch (e) {
    proxy?.$message?.error?.(e?.message || '操作失败')
  }
}
function _openVolCustomColumn() {
  try {
    const dc = _getDataConfigFromVM()
    const ps = _propsFromVM()
    // ViewGrid.vue L726 内部实现：proxy.$refs.customColumnRef.show(columns, orginColumnFields, table.name)
    const ref = gridVM?.$refs?.customColumnRef || gridRef.value?.$refs?.customColumnRef
    if (ref && typeof ref.show === 'function' && ps && ps.columns) {
      const tableName =
        (ps.table && (ps.table.name || ps.table.tableName)) || opts.table?.name || ''
      const cols = Array.isArray(ps.columns) ? ps.columns : []
      const orginFields =
        (dc && (dc.orginColumnFields || dc.columnFields)) ||
        cols
          .map(function (c) {
            return c && c.field
          })
          .filter(function (v) {
            return !!v
          })
      return ref.show(cols, orginFields, tableName)
    }
    proxy?.$message?.warning?.('当前 Vol 版本未暴露列配置，请使用表格右上角的「列设置」按钮')
  } catch (e) {
    proxy?.$message?.error?.(e?.message || '操作失败')
  }
}

/* ————————————————————————————— ViewGrid 实例 & 挂载 customSearchRef ————————————————————————————— */
const gridRef = ref(null)
const customSearchRef = ref(null)
let gridVM = null

/* FIX-A：构造 mock DOM 占位（{clientHeight:0}），挂载到 ViewGrid 组件内部的 refs（不走外层 proxy.$refs setter）
   根因：gridVM 是 Vue 3 的 reactive Proxy(ComponentPublicInstance)，直接 `gridVM.$refs.x = y` 会走 setter trap（readonly 常返回 falsy 抛 TypeError）
   正确路径：Vue 3 的 ComponentPublicInstance 上有 `$` 指向内部 ComponentInternalInstance，其 `.refs` 是**真实可变对象**（proxy.$refs 的底层来源）
   优先级：① `vm.$.refs[key] = mock`（Vue 3 内部结构，跨版本稳定）→ ② 兜底 Object.defineProperty 挂到 proxy 本身 */
function _makeFakeBox() {
  return {
    clientHeight: 0,
    offsetHeight: 0,
    scrollHeight: 0,
    clientWidth: 0,
    offsetWidth: 0,
    style: {},
    getBoundingClientRect: () => ({ height: 0, width: 0, top: 0, left: 0, right: 0, bottom: 0 })
  }
}
function _rawSetRef($vm, key, valueOrEl) {
  try {
    const inst = $vm.$ || $vm._
    if (inst && inst.refs != null) {
      inst.refs[key] = valueOrEl
      return true
    }
  } catch (_) {}
  try {
    $vm.$refs[key] = valueOrEl
    return true
  } catch (_) {}
  try {
    Object.defineProperty($vm, key, {
      configurable: true,
      enumerable: false,
      writable: true,
      value: valueOrEl
    })
    return true
  } catch (_) {
    return false
  }
}
function _mockRefBox($vm, key, preferRealEl) {
  try {
    const cur = $vm.$refs && $vm.$refs[key]
    if (cur && typeof cur.clientHeight === 'number') return cur
  } catch (_) {}
  const fallback =
    preferRealEl && typeof preferRealEl.clientHeight === 'number' ? preferRealEl : _makeFakeBox()
  _rawSetRef($vm, key, fallback)
  return fallback
}

/* FIX-B：禁止在 JS 层清空/修改 dataConfig.buttons / moreButtons / splitButtons / maxBtnLength！
   原因：Vol 的 add()/edit()/deleteSelectRow()/权限校验/表头渲染/内部初始化全部依赖 buttons 数组的 button 对象结构，
        一旦清空成 [] 会让 ViewGridEvent、initButtonsAuthFields、gridButtons 内部报错、实例不完整，
        进而导致 ViewGrid 内部 7 个 defineAsyncComponent 组件（QuickSearch/Audit/UploadExcel/custom-column/vol-header/ViewGridAudit/view-grid-detail-footer）
        在 resolve 时 Vue runtime-core 遍历 instance.parent 链找 locateNonHydratedAsyncRoot，碰到某个 parent 因内部报错实例未构建完整为 null →
        读 null.component 抛：TypeError: Cannot read properties of null (reading 'component')。
   正确做法：UI 隐藏就用纯 CSS `:deep(.view-header > .btn-group) { display:none !important }`（外层已加），不改 Vol 内部运行所需数据结构。 */
function _freezeVolButtons(_dataConfig) {
  // 什么都不做，只保留函数签名兼容老调用点
}

function _dedupSelectData(items) {
  if (!items || !items.forEach) return
  items.forEach((x) => {
    if (!x || !Array.isArray(x.data) || !x.data.length) return
    // select 字典：通用结构是 [{key,value,label?}] 或 [{value,label}]，按 value + label 做 Set 去重
    const seen = new Set()
    const out = []
    x.data.forEach((row, idx) => {
      if (row == null) return
      const v =
        row.value != null ? String(row.value) : row.key != null ? String(row.key) : `__i__${idx}`
      const l = row.label != null ? String(row.label) : row.text != null ? String(row.text) : v
      const k = `${v}||${l}`
      if (seen.has(k)) return
      seen.add(k)
      out.push(row)
    })
    if (out.length !== x.data.length) x.data.splice(0, x.data.length, ...out)
  })
}
function _ensureFixesApplied($vm, _phase) {
  if (!$vm) return
  // FIX-A：initExtraHeight 同时读 fixedSearchBox.clientHeight + customSearchRef.clientHeight，两者都必须保证是 {clientHeight: number}
  const customEl =
    customSearchRef.value && typeof customSearchRef.value.clientHeight === 'number'
      ? customSearchRef.value
      : null
  _mockRefBox($vm, 'customSearchRef', customEl)
  _mockRefBox($vm, 'fixedSearchBox', null)
  // FIX-C：searchFormOptions / editFormOptions 中 select 字典 data 被 onInited + modelOpenBefore 多次 initDicKeys 重复 push → UI 下拉重复
  //        VolFormProvider.bindData 只做 x.data = data.data（非幂等 append），因此每次请求回来都会多一份
  //        在每个 phase（onInit / onInited / onMounted）都去重一次（按 value+label Set）
  try {
    _dedupSelectData(
      opts.searchFormOptions.flat
        ? opts.searchFormOptions.flat()
        : opts.searchFormOptions.reduce((acc, r) => acc.concat(r), [])
    )
  } catch (_) {}
  try {
    _dedupSelectData(
      opts.editFormOptions.flat
        ? opts.editFormOptions.flat()
        : opts.editFormOptions.reduce((acc, r) => acc.concat(r), [])
    )
  } catch (_) {}
  // FIX-E：onInited 后搜索区 DOM 刚出来，也做一次事件绑定（onMounted 再做一次）
  if (_phase === 'onInited' || _phase === 'onMounted') {
    try {
      _ensureSearchAutoTrigger($vm)
    } catch (_) {}
  }
  // FIX-B：Vol 内部 dataConfig 数据结构原样保留，仅外层 CSS 隐藏 UI 原生双排按钮（见 style 末尾）
}

/* ————————————————————————————— 搜索区自动触发搜索 ————————————————————————————— */
const _SEARCH_TRIGGERED = Symbol.for('__yzh_searchTriggered')
function _ensureSearchAutoTrigger($vm) {
  if (!$vm) return
  const root = $vm.$el && typeof $vm.$el.querySelectorAll === 'function' ? $vm.$el : document
  if (!root || typeof root.querySelectorAll !== 'function') return
  // 选择搜索区：fixedSearchBox + customSearchRef + .search-form 三个容器（Vol 不同版本可能放不同容器里）
  const containers = [
    $vm.$refs && $vm.$refs.fixedSearchBox ? $vm.$refs.fixedSearchBox : null,
    typeof customSearchRef?.value?.querySelectorAll === 'function' ? customSearchRef.value : null,
    $vm.$refs && $vm.$refs.customSearchRef ? $vm.$refs.customSearchRef : null
  ].filter(Boolean)
  const scopedRoots = containers.length ? containers : [root]
  scopedRoots.forEach((box) => {
    if (!box || typeof box.querySelectorAll !== 'function') return
    // 1) select / input / textarea（含 Element Plus 的真实节点）
    const actives = Array.from(
      box.querySelectorAll(
        'select, input, textarea, [class*=el-select], [class*=el-cascader], [role=combobox]'
      )
    )
    actives.forEach((el) => {
      if (!el || el[_SEARCH_TRIGGERED]) return
      const triggerSearch = (e) => {
        try {
          // 避免对正在输入中的每一次 keydown 都 search（只有 Enter + 非输入 key 才 search）
          if (e && e.type === 'keydown') {
            const k = e.key || e.keyCode
            if (k !== 'Enter' && k !== 13) return
          }
          // blur/change/Enter 都触发一次 resetPage=true 的 search（按当前 searchFormFields 重查）
          if ($vm && typeof $vm.search === 'function') $vm.search(null, true)
        } catch (_) {}
      }
      el.addEventListener('blur', triggerSearch, true)
      el.addEventListener('change', triggerSearch, true)
      el.addEventListener('keydown', triggerSearch, true)
      try {
        el[_SEARCH_TRIGGERED] = true
      } catch (_) {
        try {
          Object.defineProperty(el, _SEARCH_TRIGGERED, {
            configurable: true,
            value: true,
            writable: false
          })
        } catch (_2) {}
      }
    })
    // 2) Element Plus el-select 是 [class*=el-select] 容器，真实 blur/change 事件会绑定在其内部原生 input 或 wrapper
    //    另外再观察 el-select 下拉选中后的 visible 变化：监听 document click 关闭下拉（因为 change 在 select 容器本身不一定冒泡到原生 change）
    const selectWrappers = Array.from(
      box.querySelectorAll('[class*=el-select-wrap], .el-select, [class*=el-select]')
    )
    selectWrappers.forEach((wrap) => {
      if (!wrap || wrap[_SEARCH_TRIGGERED]) return
      const onVisibleHide = (mutations) => {
        const hide = (m) => {
          const t = m.target
          if (t && /popper|dropdown/i.test(t.className || '')) {
            // visible=false → 触发一次 search
            triggerSearch()
          }
        }
        if (mutations && mutations.forEach) mutations.forEach(hide)
      }
      wrap.addEventListener(
        'mouseup',
        () => {
          setTimeout(() => triggerSearch(), 220)
        },
        true
      )
      wrap.addEventListener(
        'mouseleave',
        () => {
          setTimeout(() => triggerSearch(), 320)
        },
        true
      )
      try {
        wrap[_SEARCH_TRIGGERED] = true
      } catch (_) {}
    })
  })
  function triggerSearch() {
    try {
      if ($vm && typeof $vm.search === 'function') $vm.search(null, true)
    } catch (_) {}
  }
}

onMounted(() => {
  nextTick(() => {
    _ensureFixesApplied(gridVM, 'onMounted')
    // FIX-E：搜索输入/选择后「需要点击其他地方才刷新」
    // quickSearchKeyPress 只处理 searchForm 的第一个 input 的 keyCode 13，且未监听 blur/change；
    // 用户填完直接关输入就不触发 search，因此在搜索区对所有可输入元素统一监听 blur/change/keydown，自动 searchExec
    _ensureSearchAutoTrigger(gridVM)
    // 二次 nextTick，等授权系统 initButtonsAuthFields 跑完再最后清一次
    nextTick(() => _ensureFixesApplied(gridVM, 'onMounted+tick2'))
  })
})

if (typeof defineExpose === 'function') {
  defineExpose({
    get grid() {
      return gridVM
    },
    getTable: () => gridRef.value?.getTable?.(),
    search: (param, resetPage) => gridRef.value?.search?.(param, resetPage),
    refresh: () => gridRef.value?.refresh?.() || gridRef.value?.search?.({}, true),
    add: () => gridRef.value?.getTable?.()?.add?.(),
    getSelected: () => selectedRowObjects(),
    clearSelection: () => clearSelected(),
    toggleEditMode,
    enterEditMode,
    exitEditMode,
    editMode,
    setExternalFilter(wheres) {
      if (gridVM) gridRef.value?.search?.(wheres || {}, true)
    }
  })
}

/* ————————————————————————————— 生命周期钩子 ————————————————————————————— */
function onInit($vm) {
  gridVM = $vm
  _syncVolSelectionGetter()
  _ensureFixesApplied($vm, 'onInit')
  if (props.searchMode === 'fixed') gridVM.setFixedSearchForm?.(true)
  if (props.searchMode === 'hidden') gridVM.setFixedSearchForm?.(false)
  lc.onInit?.($vm)
}
async function onInited($vm) {
  nextTick(() => {
    _syncVolSelectionGetter()
    _ensureFixesApplied($vm, 'onInited')
  })
  nextTick(() => {
    nextTick(() => {
      _syncVolSelectionGetter()
      _ensureFixesApplied($vm, 'onInited+tick2')
    })
  })
  if (props.externalFilter?.length && gridVM) gridRef.value?.search?.(props.externalFilter, true)
  lc.onInited?.($vm)
}
watch(
  () => props.externalFilter,
  (n) => {
    if (gridVM && n != null) gridRef.value?.search?.(n || {}, true)
  },
  { deep: true }
)

/* ————————————————————————————— 查询 ————————————————————————————— */
async function loadBeforeAsync(param) {
  if (props.externalFilter?.length) {
    param.wheres = param.wheres || []
    for (const w of props.externalFilter) param.wheres.push(w)
  }
  return runGuard(lc.onLoadBefore, [param])
}
function _appendOrLikeForKeyword(wheres, keywordField, likeFields, formFields) {
  if (!wheres) return
  const kw =
    formFields && formFields[keywordField] != null ? String(formFields[keywordField]).trim() : ''
  if (!kw) return
  // 对 likeFields（多个 OR 字段）逐个拼 wheres LIKE
  likeFields.forEach((f) => {
    wheres.push({ name: f, value: kw, operator: 'like' })
  })
  // 消费掉字段（不让 Vol 再走默认相等查询）
  try {
    if (formFields && keywordField in formFields) formFields[keywordField] = undefined
  } catch (_) {}
}
function _appendEqual(wheres, field, value) {
  if (value === undefined || value === null || value === '') return
  wheres.push({ name: field, value: value, operator: 'eq' })
}
async function onLoadBefore(param, resolve) {
  const ok = await runGuard(lc.onLoadBefore, [param])
  if (typeof resolve === 'function') resolve?.(ok)
  return ok
}
async function onLoadAfter(rows, resolve, rawData) {
  try {
    const afterRows = (await lc.onLoadAfter?.(rows || [], rawData)) || rows || []
    const pag = _currentPagerRaw()
    if (pag) {
      pageRowsRef.value = afterRows
      const pager = {
        page: { value: pag.page },
        size: { value: pag.rows },
        total: { value: rawData?.total ?? pag.total ?? 0 }
      }
      incSync.setRows(afterRows, pager)
    } else {
      pageRowsRef.value = afterRows
    }
    if (typeof resolve === 'function') resolve?.(afterRows)
    return afterRows
  } catch (e) {
    if (typeof resolve === 'function') resolve?.(rows)
    return rows
  }
}
async function searchBefore(param) {
  // FIX-D：过滤输入「没有效果」—— Vol 默认 search 的 wheres 是把 searchFormFields 的**非空值**当「相等查询」拼到 param（只在 param 自身已有 wheres 时生效）；
  //        我们的搜索字段 Name 是**多字段模糊关键词**（机构全称/简称/CNAS编号 任一 LIKE），Status 是相等值；
  //        因此把 param 标准化成 { wheres: [...] }，再手动注入
  if (param == null) param = {}
  if (!param.wheres) param.wheres = []
  // 兼容 ViewGridExposeMethods.search(wheres[]) 的调用：Array 传进来时外层包成 {wheres: []} 给我们
  if (Array.isArray(param.wheres)) {
    // ok
  } else if (param.wheres && typeof param.wheres === 'object') {
    param.wheres = [param.wheres]
  }
  // 1) 关键词（searchFormFields.Name）→ 三个字段 OR LIKE（由 schema.searchKeywordFields 指定，默认 Name/ShortName/CbCode）
  const kwFields = Array.isArray(props.schema?.searchKeywordFields)
    ? props.schema.searchKeywordFields
    : ['Name', 'ShortName', 'CbCode']
  const kwKey = props.schema?.searchKeywordField || 'Name'
  _appendOrLikeForKeyword(param.wheres, kwKey, kwFields, opts.searchFormFields)
  // 2) Status/其他 select 字段：非空即相等查询
  const searchSelectFields = Array.isArray(props.schema?.searchEqualFields)
    ? props.schema.searchEqualFields
    : ['Status']
  searchSelectFields.forEach((f) => {
    _appendEqual(param.wheres, f, opts.searchFormFields && opts.searchFormFields[f])
  })
  // 3) searchFormFields 中非空的剩余字段（用户自定义拓展）一律按相等拼
  if (opts.searchFormFields && typeof opts.searchFormFields === 'object') {
    Object.keys(opts.searchFormFields).forEach((k) => {
      if (k === kwKey) return // 关键词已处理
      if (searchSelectFields.indexOf(k) !== -1) return // 相等已处理
      const v = opts.searchFormFields[k]
      if (v === undefined || v === null || v === '') return
      param.wheres.push({ name: k, value: v })
    })
  }
  // 交给业务 onLoadBefore 最后再改一遍（与 loadBeforeAsync 一致）
  return runGuard(lc.onLoadBefore, [param])
}
async function searchAfter(result) {
  return true
}

/* ————————————————————————————— 新增 / 修改 ————————————————————————————— */
async function addBefore(formData) {
  if (!(await runGuard(lc.onAddBefore, [formData]))) return false
  return true
}
async function saveBefore(action, saveModel) {
  if (action === 'Add') return runGuard(lc.onAddSaveBefore, [saveModel.main, saveModel.list])
  if (action === 'Update') return runGuard(lc.onUpdateSaveBefore, [saveModel.main, saveModel.list])
  return true
}
async function saveAfter(action, result) {
  const savedEntity = result?.entity || (Array.isArray(result?.rows) ? result.rows[0] : null)
  if (!savedEntity) return
  if (action === 'Add') {
    await runAfter(action, savedEntity, result)
    const pager = _currentPager()
    if (pager) await incSync.applyInsert(savedEntity, pager)
  } else if (action === 'Update') {
    await runAfter(action, savedEntity, result)
    await incSync.applyReplace(savedEntity)
  }
}
async function runAfter(action, entity, result) {
  const list = null
  if (action === 'Add') await lc.onAddSaveAfter?.(entity, list, result)
  if (action === 'Update') await lc.onUpdateSaveAfter?.(entity, list, result)
}
async function updateBefore(row, formData) {
  return runGuard(lc.onUpdateBefore, [row, formData])
}
async function modelOpenBefore(row, action) {
  if (action === 'Add') return runGuard(lc.onAddBefore, [row || {}])
  if (action === 'Update') return runGuard(lc.onUpdateBefore, [row || {}, row || {}])
  return true
}
async function modelOpenAfter(row, action) {}

/* ————————————————————————————— 删除 ————————————————————————————— */
async function deleteBefore(rows, ids) {
  const cnt = (ids && ids.length) || (rows && rows.length) || 0
  if (guard && !(await guard.confirmDeleteBatch(cnt))) return false
  if (!(await runGuard(lc.onDeleteBefore, [rows || [], ids || []]))) return false
  return true
}
async function deleteAfter(rows, ids) {
  await lc.onDeleteAfter?.(ids)
  const k =
    ids && ids.length
      ? ids
      : (rows || [])
          .map((r) => r?.[props.schema?.keyField])
          .filter((x) => x !== undefined && x !== null)
  if (k && k.length) {
    const pager = _currentPager()
    if (pager) await incSync.applyRemove(k, pager)
  }
  clearSelected()
}

/* ————————————————————————————— 行级交互 ————————————————————————————— */
function handleRowEdit(row) {
  // 注意：gridRef = ViewGrid 实例（不是 VolTable）
  // VolTable 上 defineExpose 的 `edit` 是 reactive({columnIndex, rowIndex})（双双击编辑坐标对象，不是 function）
  // ViewGrid 上才对外暴露 async edit(rows)：走 onEdit → 开编辑弹窗 → 填 editFormFields → 显示 v-model 可输入表单
  // 业务 Sys_User.vue / Sys_Department.vue 的调用写法就是 `gridRef.edit(row)`
  const g = gridRef.value
  if (g && typeof g.edit === 'function') {
    g.edit(row)
    return
  }
  // 兼容：Vol 某些版本 getTable() 返回 ViewGrid 自己
  const tbl = g && g.getTable && g.getTable()
  if (tbl && typeof tbl.edit === 'function') {
    tbl.edit(row)
    return
  }
  // 再兜底：mounted 时把 YZH 自己的 edit/delete 挂到 gridVM 上防止版本差异
  if (gridVM && gridVM.__yzhOnEdit) gridVM.__yzhOnEdit(row)
}
async function handleRowDelete(row) {
  const label = row?.Name || row?.Title || row?.[props.schema?.keyField] || ''
  if (guard && !(await guard.confirmDeleteOne(label))) return
  const rows = [row]
  const ids = [row?.[props.schema?.keyField]]
  if (!(await runGuard(lc.onDeleteBefore, [rows, ids]))) return
  try {
    if (!api) return
    const r = await api.del(ids)
    if (r && r.status !== false) {
      await deleteAfter(rows, ids)
      proxy?.$message?.success?.('删除成功')
    } else {
      proxy?.$message?.error?.(r?.message || '删除失败')
    }
  } catch (e) {
    proxy?.$message?.error?.(e?.message || '删除失败')
  }
}
async function handleBatchDelete() {
  const rows = selectedRowObjects()
  const ids = selectedKeys()
  if (!(await deleteBefore(rows, ids))) return
  try {
    if (!api) return
    const r = await api.del(ids)
    if (r && r.status !== false) {
      await deleteAfter(rows, ids)
      proxy?.$message?.success?.(`成功删除 ${ids.length} 条`)
    } else {
      proxy?.$message?.error?.(r?.message || '删除失败')
    }
  } catch (e) {
    proxy?.$message?.error?.(e?.message || '删除失败')
  }
}
function rowClick(evt) {
  const row = evt?.row
  setSingleSelected(row || null)
  if (!editMode.value) setSelectedRows(row ? [row] : [])
  lc.onRowSelect?.(row || null, selectedRowObjects())
  lc.onRowClick?.(evt)
}
function onSelectionChange(sel) {
  setSelectedRows(sel || [])
  lc.onRowSelect?.(sel?.[0] || null, sel || [])
}

/* ————————————————————————————— helpers ————————————————————————————— */
function _currentPagerRaw() {
  try {
    const tbl = gridRef.value?.getTable?.()
    if (!tbl) return null
    return tbl.paginations || tbl.pagination || null
  } catch (_) {
    return null
  }
}
function _currentPager() {
  const pag = _currentPagerRaw()
  if (!pag) return null
  return { page: { value: pag.page }, size: { value: pag.rows }, total: { value: pag.total ?? 0 } }
}
const volViewGridProps = computed(() => {
  const rest = {}
  const overrides = [
    'table',
    'columns',
    'detail',
    'details',
    'editFormFields',
    'editFormOptions',
    'searchFormFields',
    'searchFormOptions',
    'extend',
    'priview',
    'coderTableId',
    'loadTreeChildren',
    'searchMode',
    'text',
    'onInit',
    'onInited',
    'onLoadBefore',
    'onLoadAfter',
    'searchBefore',
    'searchAfter',
    'addBefore',
    'updateBefore',
    'saveBefore',
    'saveAfter',
    'modelOpenBefore',
    'modelOpenAfter',
    'deleteBefore',
    'deleteAfter',
    'rowClick',
    'selectionChange'
  ]
  for (const k of Object.keys(attrs || {})) {
    if (overrides.includes(k)) continue
    rest[k] = attrs[k]
  }
  return rest
})
onBeforeUnmount?.(() => {
  try {
    clearSelected()
  } catch (_) {}
})
</script>

<style lang="less" scoped>
.yzh-single-table {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  position: relative;

  .yzh-toolbar-extra {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 8px 12px 4px;
    &__left,
    &__right {
      display: flex;
      gap: 8px;
      align-items: center;
    }
  }
  .yzh-btn-bar {
    display: flex;
    gap: 6px;
    align-items: center;
    flex-wrap: wrap;
    .el-button.is-active {
      outline: 2px solid #409eff;
    }
  }

  /* FIX-B：隐藏 Vol 原生 view-header 右侧的 btn-group（YZH 已经在 btn-left-slot 完整重绘，
     防止授权系统 initButtonsAuthFields 异步把 Add/Update/Delete/Search/Reset 等重新 push 回来
     又渲染成第二排）。注意：.btn-left-slot / .btn-right-slot 不在此选择器内，YZH 自己的按钮不会被隐藏。 */
  :deep(.view-header > .btn-group) {
    display: none !important;
  }
  /* FIX-B-2：同时把「灰色 notice + desc-text + extend 文本」全部隐藏，YZH 不依赖这些头部描述
     （如果你需要留一个描述条，可在业务页的 <template #gridHeader> 里自己写） */
  :deep(.view-header > .notice),
  :deep(.view-header > .desc-text) {
    display: none !important;
  }

  /* —————————— FIX：弹窗备注挤在一块、没有整行撑满 ——————————
     1) 对所有 textarea：label 在左占一行（当栅格 span=24 时 label 宽度 96 固定，input 在下占满剩余）
     2) min-height 强制 140 像素 + rows=5，显示充裕
     3) 对 colSize=24 的整行 form-item：把 element plus 的 el-col + form-item wrapper 都设 width:100% ，避免因栅格按父容器的 12 为满格导致只占一半  */
  :deep(.el-dialog__body .el-form > .el-row) {
    /* 整行 form-item 容器强制 flex-wrap + 24 栅格每列标准 1:1 分配，避免 VolFormProvider 内部按 12 满格重算 colSize 被覆盖导致第 4 行半行 */
    display: flex;
    flex-wrap: wrap;
    width: 100%;
  }
  :deep(.el-dialog__body .el-col[span='24']:has(.el-form-item textarea)),
  :deep(.el-dialog__body .el-form-item:has(textarea)) {
    width: 100% !important;
    max-width: 100% !important;
    flex: 1 1 100% !important;
  }
  :deep(.el-dialog__body .el-form-item textarea) {
    width: 100% !important;
    min-height: 140px !important;
    max-height: 260px !important;
    resize: vertical !important;
  }
}
</style>
